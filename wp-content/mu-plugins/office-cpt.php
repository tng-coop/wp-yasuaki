<?php
/**
 * Plugin Name: Office CPT + JSON Meta
 * Description: Registers the "office-cpt" post type and exposes a flexible JSON meta field via REST (under meta.data and top-level data).
 * Author: Your Team
 * Version: 1.0.1
 *
 * Place this file in wp-content/mu-plugins/ so it always loads.
 */

if ( ! defined('ABSPATH') ) exit;

add_action('init', function () {
    // Register CPT
    register_post_type('office-cpt', [
        'labels'       => [
            'name'          => __('Offices','your-textdomain'),
            'singular_name' => __('Office','your-textdomain'),
        ],
        'public'       => true,
        'show_in_rest' => true,
        'rest_base'    => 'office-cpt',     // /wp-json/wp/v2/office-cpt
        'supports'     => [ 'title' ],
        'has_archive'  => true,
        'rewrite'      => [ 'slug' => 'offices' ],
        'menu_icon'    => 'dashicons-building',
    ]);

    // Register JSON meta: exposed under meta.data
    register_post_meta('office-cpt', 'data', [
        'object_subtype'   => 'office-cpt',      // scope to this CPT
        'type'             => 'object',
        'single'           => true,
        'show_in_rest'     => [
            'schema' => [
                'type'                 => 'object',
                'additionalProperties' => true,
            ],
        ],
        'default'          => [],
        'sanitize_callback'=> function ($value /*, $meta_key, $object_type */) {
            return (is_array($value) || is_object($value)) ? $value : [];
        },
        'auth_callback'    => function ($allowed, $meta_key, $post_id, $user_id, $cap /*, $caps */) {
            if ($cap === 'read_post_meta') {
                return current_user_can('read_post', $post_id) || current_user_can('edit_post', $post_id);
            }
            if ($cap === 'edit_post_meta') {
                return current_user_can('edit_post', $post_id);
            }
            return (bool) $allowed;
        },
    ]);
}, 5);

// Also expose top-level `data` field for convenience (view/edit)
add_action('rest_api_init', function () {
    register_rest_field('office-cpt', 'data', [
        'get_callback' => function ($obj) {
            $v = get_post_meta((int)$obj['id'], 'data', true);
            return $v ?: (object)[];
        },
        'update_callback' => function ($value, $obj) {
            $id = (int) $obj->ID;
            if ( ! current_user_can('edit_post', $id) ) {
                return new WP_Error('forbidden', 'Cannot edit.', [ 'status' => 403 ]);
            }
            update_post_meta($id, 'data', (array) $value);
            return true;
        },
        'schema' => [
            'type'                 => 'object',
            'additionalProperties' => true,
            'context'              => [ 'view', 'edit' ],
        ],
    ]);
});
