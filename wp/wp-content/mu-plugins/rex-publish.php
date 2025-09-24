<?php
/**
 * MU Plugin File
 * Path: wp-content/mu-plugins/rex-publish.php
 * Purpose: REST endpoint to publish a staging draft; if it references an original, overwrite & (if needed) untrash that original.
 */

if (!defined('ABSPATH')) { exit; }

// --- Shared registration for the original meta (idempotent across mu-files) ---
if (!function_exists('rex_register_original_meta')) {
    function rex_register_original_meta() : void {
        register_post_meta('post', '_rex_original_post_id', [
            'type' => 'integer',
            'single' => true,
            'show_in_rest' => true,
            'description' => 'ID of the original published post if this is a fork.',
            'auth_callback' => function() { return current_user_can('edit_posts'); },
        ]);
    }
}
if (false === has_action('init', 'rex_register_original_meta')) {
    add_action('init', 'rex_register_original_meta');
}

final class REX_Publish_API {
    const META_ORIGINAL = '_rex_original_post_id';
    const REST_NS = 'rex/v1';

    public static function init() : void {
        add_action('rest_api_init', [__CLASS__, 'register_routes']);
    }

    public static function register_routes() : void {
        register_rest_route(self::REST_NS, '/publish', [
            'methods' => 'POST',
            'callback' => [__CLASS__, 'route_publish'],
            'permission_callback' => function( WP_REST_Request $req ) {
                $staging_id = (int) $req->get_param('staging_id');
                return current_user_can('publish_posts') && current_user_can('edit_post', $staging_id);
            },
            'args' => [
                'staging_id' => ['required' => true, 'type' => 'integer'],
            ],
        ]);
    }

    /** Replace non-protected meta from one post to another (drop original marker). */
    private static function replace_meta_from(int $from_id, int $to_id) : void {
        $all_meta = get_post_meta($from_id);
        if (!is_array($all_meta)) { return; }
        foreach ($all_meta as $key => $values) {
            if (is_protected_meta($key, 'post')) { continue; }
            if ($key === self::META_ORIGINAL) { continue; }
            delete_post_meta($to_id, $key);
            foreach ($values as $v) { add_post_meta($to_id, $key, maybe_unserialize($v)); }
        }
    }

    /** Duplicate taxonomies from one post to another. */
    private static function copy_taxonomies(int $from_id, int $to_id, string $post_type) : void {
        $taxes = get_object_taxonomies($post_type, 'names');
        foreach ($taxes as $tax) {
            $terms = wp_get_object_terms($from_id, $tax, ['fields' => 'ids']);
            if (!is_wp_error($terms)) { wp_set_object_terms($to_id, $terms, $tax, false); }
        }
    }

    /** POST /rex/v1/publish */
    public static function route_publish( WP_REST_Request $req ) {
        $staging_id = (int) $req->get_param('staging_id');
        $staging = get_post($staging_id);
        if (!$staging) { return new WP_Error('not_found', 'Staging post not found.', ['status' => 404]); }

        $root_original_id = (int) get_post_meta($staging_id, self::META_ORIGINAL, true);
        if ($root_original_id) {
            $target = get_post($root_original_id);
            if ($target) {
                if ($target->post_status === 'trash') { wp_untrash_post($root_original_id); }

                $update = [
                    'ID'           => $root_original_id,
                    'post_title'   => $staging->post_title,
                    'post_content' => $staging->post_content,
                    'post_excerpt' => $staging->post_excerpt,
                    'post_status'  => 'publish',
                ];
                $r = wp_update_post(wp_slash($update), true);
                if (is_wp_error($r)) { return $r; }

                self::copy_taxonomies($staging_id, $root_original_id, $staging->post_type);
                self::replace_meta_from($staging_id, $root_original_id);

                return rest_ensure_response([
                    'published_id' => $root_original_id,
                    'used_original' => true,
                ]);
            } else {
                // original was hard-deleted; publish staging as new
                delete_post_meta($staging_id, self::META_ORIGINAL);
            }
        }

        // No usable original; just publish the staging post
        $r = wp_update_post([
            'ID' => $staging_id,
            'post_status' => 'publish',
        ], true);
        if (is_wp_error($r)) { return $r; }

        return rest_ensure_response([
            'published_id' => $staging_id,
            'used_original' => false,
        ]);
    }
}

REX_Publish_API::init();
