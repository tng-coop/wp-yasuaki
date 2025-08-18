<?php
/**
 * Must-Use Plugin: Pexels Proxy
 * Description: Site-wide REST proxy for Pexels Video API with caching and size optimization.
 * Author: You
 */

defined('ABSPATH') || exit;

/**
 * Resolve API key in order:
 *  1) Filter 'pexels_proxy_api_key'
 *  2) Constant PEXELS_API_KEY
 *  3) Environment variable PEXELS_API_KEY
 */
function pexels_proxy_get_api_key() {
    $key = apply_filters('pexels_proxy_api_key', null);
    if ($key) return $key;
    if (defined('PEXELS_API_KEY') && PEXELS_API_KEY) return PEXELS_API_KEY;
    $env = getenv('PEXELS_API_KEY');
    return $env ?: null;
}

/**
 * Pick best video file dynamically from actual Pexels JSON.
 * Prefers <= target_w when available, else smallest > target_w.
 * $prefer_mimes: e.g. ['video/webm','video/mp4']
 * $max_overshoot: don’t pick a file more than 1.33× target width.
 */
function pexels_proxy_pick_best_file_dynamic(array $files, int $target_w, array $prefer_mimes = ['video/mp4'], float $max_overshoot = 1.33): ?array {
    $norm = [];
    foreach ($files as $f) {
        if (!isset($f['link'], $f['width'])) continue;
        $norm[] = [
            'link'      => $f['link'],
            'width'     => (int) $f['width'],
            'height'    => isset($f['height']) ? (int) $f['height'] : null,
            'file_type' => isset($f['file_type']) ? strtolower($f['file_type']) : 'video/mp4',
        ];
    }
    if (!$norm) return null;

    // Sort by MIME preference first
    usort($norm, function($a, $b) use ($prefer_mimes) {
        $ai = array_search($a['file_type'], $prefer_mimes, true);
        $bi = array_search($b['file_type'], $prefer_mimes, true);
        $ap = ($ai === false) ? PHP_INT_MAX : $ai;
        $bp = ($bi === false) ? PHP_INT_MAX : $bi;
        if ($ap !== $bp) return $ap <=> $bp;
        return $a['width'] <=> $b['width'];
    });

    // Split unders and overs
    $unders = array_values(array_filter($norm, fn($f) => $f['width'] <= $target_w));
    $overs  = array_values(array_filter($norm, fn($f) => $f['width'] >  $target_w));

    // Best under: widest <= target
    if ($unders) {
        usort($unders, fn($a,$b) => $b['width'] <=> $a['width']);
        return $unders[0];
    }

    // Else best over, but avoid giant overshoot
    if ($overs) {
        usort($overs, fn($a,$b) => $a['width'] <=> $b['width']);
        $best_over = $overs[0];
        if ($best_over['width'] <= (int) round($target_w * $max_overshoot)) {
            return $best_over;
        }
        // If still too large, just take narrowest overall
        usort($norm, fn($a,$b) => $a['width'] <=> $b['width']);
        return $norm[0];
    }

    return null;
}

/**
 * Register routes
 */
add_action('rest_api_init', function () {

    // Route 1: full JSON
    register_rest_route('pexels-proxy/v1', '/video', [
        'methods'  => 'GET',
        'permission_callback' => '__return_true',
        'args' => [
            'id' => [
                'required' => true,
                'sanitize_callback' => fn($v) => preg_replace('/\D+/', '', (string)$v),
            ],
        ],
        'callback' => function (WP_REST_Request $req) {
            $id = $req->get_param('id');
            if (!$id) {
                return new WP_Error('bad_request', 'Missing id', ['status'=>400]);
            }

            $cache_key = "pexels_proxy_video_$id";
            if ($cached = get_transient($cache_key)) {
                return rest_ensure_response($cached);
            }

            $api_key = pexels_proxy_get_api_key();
            if (!$api_key) {
                return new WP_Error('no_api_key', 'PEXELS_API_KEY not configured', ['status'=>500]);
            }

            $resp = wp_remote_get("https://api.pexels.com/videos/videos/$id", [
                'headers' => [ 'Authorization' => $api_key ],
                'timeout' => 12,
            ]);
            if (is_wp_error($resp)) {
                return new WP_Error('pexels_error', $resp->get_error_message(), ['status'=>502]);
            }

            $code = wp_remote_retrieve_response_code($resp);
            $body = wp_remote_retrieve_body($resp);
            if ($code !== 200 || !$body) {
                return new WP_Error('pexels_bad_response', 'Upstream error', ['status'=>502]);
            }

            $json = json_decode($body, true);
            if (!is_array($json)) {
                return new WP_Error('pexels_json', 'Invalid JSON', ['status'=>502]);
            }

            set_transient($cache_key, $json, 12 * HOUR_IN_SECONDS);
            return rest_ensure_response($json);
        }
    ]);

    // Route 2: optimized single best src
    register_rest_route('pexels-proxy/v1', '/video-src', [
        'methods'  => 'GET',
        'permission_callback' => '__return_true',
        'args' => [
            'id'    => ['required'=>true,'sanitize_callback'=>fn($v)=>preg_replace('/\D+/', '', (string)$v)],
            'w'     => ['sanitize_callback'=>'absint'],
            'dpr'   => ['sanitize_callback'=>'absint'],
            'types' => ['sanitize_callback'=>function($v){
                $a = array_filter(array_map('trim', explode(',', (string)$v)));
                return $a ? implode(',', $a) : 'video/mp4';
            }],
            'json'  => ['sanitize_callback'=>fn($v)=> $v ? 1 : 0],
        ],
        'callback' => function (WP_REST_Request $req) {
            $id   = $req->get_param('id');
            $w    = (int) ($req->get_param('w') ?: 1280);
            $dpr  = max(1, (int) ($req->get_param('dpr') ?: 1));
            $types_csv = $req->get_param('types') ?: 'video/mp4';
            $want_json = (int) $req->get_param('json') === 1;

            $target_w = max(320, min(3840, $w * $dpr)); // clamp 320–3840
            $prefer_mimes = array_map('strtolower', array_filter(array_map('trim', explode(',', $types_csv))));

            // Load cached JSON
            $ckey = "pexels_proxy_video_$id";
            $json = get_transient($ckey);
            if (!$json) {
                $api_key = pexels_proxy_get_api_key();
                if (!$api_key) return new WP_Error('no_api_key','PEXELS_API_KEY not configured',['status'=>500]);

                $resp = wp_remote_get("https://api.pexels.com/videos/videos/$id", [
                    'headers' => [ 'Authorization' => $api_key ],
                    'timeout' => 12,
                ]);
                if (is_wp_error($resp)) return new WP_Error('pexels_error',$resp->get_error_message(),['status'=>502]);
                $code = wp_remote_retrieve_response_code($resp);
                $body = wp_remote_retrieve_body($resp);
                if ($code !== 200 || !$body) return new WP_Error('pexels_bad_response','Upstream error',['status'=>502]);
                $json = json_decode($body, true);
                if (!is_array($json)) return new WP_Error('pexels_json','Invalid JSON',['status'=>502]);
                set_transient($ckey, $json, 12 * HOUR_IN_SECONDS);
            }

            if (empty($json['video_files']) || !is_array($json['video_files'])) {
                return new WP_Error('no_files','No video files available',['status'=>404]);
            }

            $best = pexels_proxy_pick_best_file_dynamic($json['video_files'], $target_w, $prefer_mimes, 1.33);
            if (!$best) return new WP_Error('no_match','No suitable file found',['status'=>404]);

            // Cache selected
            $sel_key = 'pexels_proxy_sel_' . md5($id . '|' . $best['width'] . '|' . $best['file_type']);
            set_transient($sel_key, $best, 12 * HOUR_IN_SECONDS);

            if ($want_json) {
                return rest_ensure_response([
                    'src'        => $best['link'],
                    'width'      => $best['width'],
                    'height'     => $best['height'],
                    'file_type'  => $best['file_type'],
                    'target_w'   => $target_w,
                ]);
            }

            return new WP_REST_Response(null, 302, ['Location' => $best['link']]);
        }
    ]);
});

