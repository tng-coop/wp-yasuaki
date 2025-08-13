<?php
/**
 * MU Plugin: Office CPT (public, meta-driven, with editor + robust REST)
 * Description: Public CPT "office-cpt" with address meta. Includes a dedicated REST endpoint to set/get address reliably.
 * Version: 1.7.0
 * Author: You
 */

if (!defined('ABSPATH')) { exit; }

define('OFFICE_CPT_MU_VERSION', '1.7.0');
define('OFFICE_CPT_MU_OPTION',  'office_cpt_mu_version');

/** -----------------------------
 *  CPT + Meta Registration
 * ------------------------------*/
function office_cpt_register() {
    register_post_type('office-cpt', [
        'label'               => 'Offices',
        'labels'              => ['singular_name' => 'Office'],
        'public'              => true,
        'publicly_queryable'  => true,
        'show_ui'             => true,
        'has_archive'         => true,  // set false if you do NOT want /offices/
        'show_in_rest'        => true,
        'rest_base'           => 'offices',  // /wp-json/wp/v2/offices
        'supports'            => ['title', 'editor'],
        'map_meta_cap'        => true,
        'rewrite'             => ['slug' => 'offices', 'with_front' => false],
        'menu_position'       => 20,
    ]);

    // Use register_meta with object_subtype for maximum compatibility
    $common_auth_cb = function($allowed, $meta_key, $post_id, $user_id, $cap, $caps) {
        return current_user_can('edit_post', $post_id);
    };

    register_meta('post', 'address', [
        'object_subtype'   => 'office-cpt',
        'type'             => 'string',
        'single'           => true,
        'show_in_rest'     => [
            'schema' => ['type' => 'string'],
        ],
        'auth_callback'    => $common_auth_cb,
        'sanitize_callback'=> 'sanitize_textarea_field', // preserves newlines
    ]);

    register_meta('post', 'status_tag', [
        'object_subtype'   => 'office-cpt',
        'type'             => 'string',
        'single'           => true,
        'show_in_rest'     => [
            'schema' => ['type' => 'string'],
        ],
        'auth_callback'    => $common_auth_cb,
        'sanitize_callback'=> 'sanitize_text_field',
    ]);
}
add_action('init', 'office_cpt_register', 10);

/** -----------------------------
 *  Frontend Render Helpers
 * ------------------------------*/
function office_cpt_address_html($post_id = null) {
    $post_id = $post_id ?: get_the_ID();
    if (!$post_id) return '';
    $address = get_post_meta($post_id, 'address', true);
    if (!$address) return '';
    return '<address style="margin:0 0 1rem 0; font-style:normal;">'
         . nl2br(esc_html($address))
         . '</address>';
}

// Classic themes: prepend to the_content
add_filter('the_content', function ($content) {
    if (get_post_type() !== 'office-cpt') return $content;
    return office_cpt_address_html() . $content;
}, 1);

// Block themes: inject before core/post-content
add_filter('render_block', function ($block_content, $block) {
    if (get_post_type() !== 'office-cpt') return $block_content;
    if (!empty($block['blockName']) && $block['blockName'] === 'core/post-content') {
        return office_cpt_address_html() . $block_content;
    }
    return $block_content;
}, 1, 2);

// Optional shortcode: [office_address]
add_shortcode('office_address', function () {
    return office_cpt_address_html();
});

/** -----------------------------
 *  Robust REST Endpoint (direct meta set/get)
 *    GET  /wp-json/office/v1/address/<id>
 *    POST /wp-json/office/v1/address/<id> { "address": "..." }
 * ------------------------------*/
add_action('rest_api_init', function () {
    register_rest_route('office/v1', '/address/(?P<id>\d+)', [
        [
            'methods'  => 'GET',
            'permission_callback' => function ($request) {
                $post_id = (int) $request['id'];
                return current_user_can('read_post', $post_id) || current_user_can('edit_post', $post_id);
            },
            'callback' => function ($request) {
                $post_id = (int) $request['id'];
                if (get_post_type($post_id) !== 'office-cpt') {
                    return new WP_Error('invalid_type', 'Not an office-cpt post', ['status' => 400]);
                }
                return [
                    'id'      => $post_id,
                    'address' => get_post_meta($post_id, 'address', true),
                ];
            },
        ],
        [
            'methods'  => 'POST',
            'permission_callback' => function ($request) {
                $post_id = (int) $request['id'];
                return current_user_can('edit_post', $post_id);
            },
            'callback' => function ($request) {
                $post_id = (int) $request['id'];
                if (get_post_type($post_id) !== 'office-cpt') {
                    return new WP_Error('invalid_type', 'Not an office-cpt post', ['status' => 400]);
                }
                $address = $request->get_param('address');
                if (!is_string($address)) {
                    return new WP_Error('invalid_param', 'Address must be a string', ['status' => 400]);
                }
                // Sanitize similarly to the registered meta
                $address = sanitize_textarea_field($address);
                update_post_meta($post_id, 'address', $address);
                return [
                    'id'      => $post_id,
                    'address' => get_post_meta($post_id, 'address', true),
                    'ok'      => true,
                ];
            },
            'args' => [
                'address' => [
                    'type' => 'string',
                    'required' => true,
                ],
            ],
        ],
    ]);
});

/** -----------------------------
 *  One-time rewrite flush (admin-only)
 * ------------------------------*/
add_action('admin_init', function () {
    if (!current_user_can('manage_options')) return;
    $stored_version = get_option(OFFICE_CPT_MU_OPTION);
    if ($stored_version !== OFFICE_CPT_MU_VERSION) {
        office_cpt_register();        // ensure CPT known before flushing
        flush_rewrite_rules(false);
        update_option(OFFICE_CPT_MU_OPTION, OFFICE_CPT_MU_VERSION, true);
    }
});

