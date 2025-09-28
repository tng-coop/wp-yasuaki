<?php
/**
 * MU Plugin File
 * Path: wp-content/mu-plugins/rex-fork.php
 * Purpose: REST endpoint to fork posts with a single original marker.
 * Notes:
 *  - Permission callback is coarse (edit_posts). Per-post capability is enforced
 *    inside the handler after confirming the source exists so 404 vs 403 are correct.
 *  - PATCH: Allow Contributors to fork PUBLISHED posts they can read, without requiring
 *    edit permission on the SOURCE post. Private/unreadable content remains protected.
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

final class REX_Fork_API {
    const META_ORIGINAL = '_rex_original_post_id';
    const REST_NS = 'rex/v1';

    public static function init() : void {
        add_action('rest_api_init', [__CLASS__, 'register_routes']);
    }

    public static function register_routes() : void {
        register_rest_route(self::REST_NS, '/fork', [
            'methods' => 'POST',
            'callback' => [__CLASS__, 'route_fork'],
            // Coarse capability here; fine-grained check happens inside after existence check
            'permission_callback' => function( WP_REST_Request $req ) {
                return current_user_can('edit_posts');
            },
            'args' => [
                'source_id' => ['required' => true, 'type' => 'integer'],
                'status'    => ['required' => false, 'type' => 'string', 'default' => 'draft'],
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
            foreach ($values as $v) {
                add_post_meta($to_id, $key, maybe_unserialize($v));
            }
        }
    }

    /** POST /rex/v1/fork */
    public static function route_fork( WP_REST_Request $req ) {
        $source_id = (int) $req->get_param('source_id');
        $status    = $req->get_param('status') ?: 'draft';

        $source = get_post($source_id);
        if (!$source) {
            return new WP_Error('not_found', 'Source post not found.', ['status' => 404]);
        }
        // Fork permission model (revised):
        //  • must be able to CREATE posts of this type (edit_posts/edit_pages/etc.)
        //  • must be able to READ the source; PUBLISHED content is readable by any logged-in user
        $pto        = get_post_type_object($source->post_type);
        $can_create = $pto ? current_user_can($pto->cap->edit_posts) : current_user_can('edit_posts');
        $is_public  = ($source->post_status === 'publish');
        $can_read   = $is_public || current_user_can('read_post', $source_id);
        if (!($can_create && $can_read)) {
            return new WP_Error(
                'forbidden',
                'Cannot fork: insufficient permissions to read source or create posts.',
                ['status' => 403]
            );
        }

        $new_post = [
            'post_title'   => $source->post_title,
            'post_content' => $source->post_content,
            'post_excerpt' => $source->post_excerpt,
            'post_status'  => $status,
            'post_type'    => $source->post_type,
            'post_author'  => get_current_user_id(),
        ];

        $new_id = wp_insert_post(wp_slash($new_post), true);
        if (is_wp_error($new_id)) { return $new_id; }

        self::copy_taxonomies($source_id, $new_id, $source->post_type);
        self::copy_meta_to_new($source_id, $new_id);

        $root_original_id = self::root_original_id($source_id);
        if ($root_original_id) { update_post_meta($new_id, self::META_ORIGINAL, $root_original_id); }

        return rest_ensure_response([
            'id' => $new_id,
            'status' => get_post_status($new_id),
            'original_post_id' => $root_original_id ?: null,
            'modified_gmt' => (string) get_post($new_id)->post_modified_gmt,
        ]);
    }
}

REX_Fork_API::init();
