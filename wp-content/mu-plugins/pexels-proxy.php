<?php
/**
 * Must-Use Plugin: Pexels Proxy (Catalog Only)
 * Description: Site-wide REST proxy for Pexels Video API with caching. Returns raw catalog JSON only.
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
 * Register routes
 */
add_action('rest_api_init', function () {

    // Route: full JSON catalog passthrough (cached)
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
});
