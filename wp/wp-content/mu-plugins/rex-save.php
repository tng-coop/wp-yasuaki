<?php
/**
 * MU Plugin File
 * Path: wp-content/mu-plugins/rex-save.php
 * Purpose: REST endpoint to save posts with auto-fork fallback on failure/conflict.
 * Notes:
 *  - Permission callback is coarse (edit_posts). Per-post capability is enforced
 *    inside the handler after confirming the post exists so 404 vs 403 are correct.
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

final class REX_Save_API {
    const META_ORIGINAL = '_rex_original_post_id';
    const REST_NS = 'rex/v1';

    public static function init() : void {
        add_action('rest_api_init', [__CLASS__, 'register_routes']);
    }

    public static function register_routes() : void {
        register_rest_route(self::REST_NS, '/save', [
            'methods' => 'POST',
            'callback' => [__CLASS__, 'route_save'],
            // Coarse capability; per-post check occurs inside after existence
            'permission_callback' => function( WP_REST_Request $req ) {
                return current_user_can('edit_posts');
            },
            'args' => [
                'id'   => ['required' => true, 'type' => 'integer'],
                'data' => ['required' => true, 'type' => 'object'],
            ],
        ]);
    }

    /** Determine the root original per spec (3a/3b). */
    private static function root_original_id(int $post_id) : int {
        $orig = (int) get_post_meta($post_id, self::META_ORIGINAL, true);
        if ($orig) { return $orig; }
        $p = get_post($post_id);
        if ($p && $p->post_status === 'publish') { return (int) $p->ID; }
        return 0;
    }

    /** Duplicate taxonomies from one post to another. */
    private static function copy_taxonomies(int $from_id, int $to_id, string $post_type) : void {
        $taxes = get_object_taxonomies($post_type, 'names');
        foreach ($taxes as $tax) {
            $terms = wp_get_object_terms($from_id, $tax, ['fields' => 'ids']);
            if (!is_wp_error($terms)) { wp_set_object_terms($to_id, $terms, $tax, false); }
        }
    }

    /** Copy non-protected meta to a new post (excluding original marker). */
    private static function copy_meta_to_new(int $from_id, int $to_id) : void {
        $all_meta = get_post_meta($from_id);
        if (!is_array($all_meta)) { return; }
        foreach ($all_meta as $key => $values) {
            if (is_protected_meta($key, 'post')) { continue; }
            if ($key === self::META_ORIGINAL) { continue; }
            foreach ($values as $v) { add_post_meta($to_id, $key, maybe_unserialize($v)); }
        }
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

    /** POST /rex/v1/save */
    public static function route_save( WP_REST_Request $req ) {
        $id   = (int) $req->get_param('id');
        $data = (array) $req->get_param('data');

        $existing = get_post($id);
        if (!$existing) {
            return new WP_Error('not_found', 'Post not found for save.', ['status' => 404]);
        }
        // Enforce per-post permission only after the post is known to exist
        if (! current_user_can('edit_post', $id)) {
            return new WP_Error('forbidden', 'Cannot edit this post.', ['status' => 403]);
        }

        // Optional optimistic concurrency using modified_gmt
        $expected = isset($data['expected_modified_gmt']) ? (string) $data['expected_modified_gmt'] : '';
        if ($expected !== '') {
            $cur_gmt = (string) $existing->post_modified_gmt; // GMT string
            if ($cur_gmt !== $expected) {
                return self::fork_on_save_failure($id, $data, 'conflict');
            }
            unset($data['expected_modified_gmt']);
        }

        $update = ['ID' => $id];
        foreach (['post_title','post_content','post_excerpt','post_status','post_name'] as $field) {
            if (array_key_exists($field, $data)) { $update[$field] = $data[$field]; }
        }

        $result = wp_update_post(wp_slash($update), true);
        if (is_wp_error($result)) {
            return self::fork_on_save_failure($id, $data, $result->get_error_code());
        }

        // Meta (ignore META_ORIGINAL from client)
        if (isset($data['meta']) && is_array($data['meta'])) {
            foreach ($data['meta'] as $k => $v) {
                if ($k === self::META_ORIGINAL) { continue; }
                update_post_meta($id, $k, $v);
            }
        }

        // Taxonomies
        if (isset($data['tax_input']) && is_array($data['tax_input'])) {
            foreach ($data['tax_input'] as $tax => $terms) {
                wp_set_object_terms($id, $terms, $tax, false);
            }
        }

        return rest_ensure_response([
            'id' => $id,
            'status' => get_post_status($id),
            'saved' => true,
            'modified_gmt' => (string) get_post($id)->post_modified_gmt,
        ]);
    }

    private static function fork_on_save_failure(int $id, array $data, string $reason) {
        $source = get_post($id);
        if (!$source) { return new WP_Error('not_found', 'Post not found for fork-on-save.', ['status' => 404]); }

        $new = [
            'post_title'   => array_key_exists('post_title', $data)   ? $data['post_title']   : $source->post_title,
            'post_content' => array_key_exists('post_content', $data) ? $data['post_content'] : $source->post_content,
            'post_excerpt' => array_key_exists('post_excerpt', $data) ? $data['post_excerpt'] : $source->post_excerpt,
            'post_status'  => 'draft',
            'post_type'    => $source->post_type,
            'post_author'  => get_current_user_id(),
        ];

        $new_id = wp_insert_post(wp_slash($new), true);
        if (is_wp_error($new_id)) { return $new_id; }

        self::copy_taxonomies($id, $new_id, $source->post_type);
        self::copy_meta_to_new($id, $new_id);

        $root_original_id = self::root_original_id($id);
        if ($root_original_id) {
            update_post_meta($new_id, self::META_ORIGINAL, $root_original_id);
        } else {
            delete_post_meta($new_id, self::META_ORIGINAL);
        }

        return rest_ensure_response([
            'id' => $new_id,
            'status' => get_post_status($new_id),
            'forked' => true,
            'reason' => $reason,
            'original_post_id' => $root_original_id ?: null,
        ]);
    }
}

REX_Save_API::init();
