<?php
/**
 * MU Plugin: Office CPT (API + Web)
 * Description: "office-cpt" with public web views and JSON API. One unified "data" meta exposed via REST + custom endpoints.
 * Version: 3.0.0
 * Author: You
 */

if (!defined('ABSPATH')) { exit; }

define('OFFICE_CPT_MU_VERSION', '3.0.0');
define('OFFICE_CPT_MU_OPTION',  'office_cpt_mu_version');
define('OFFICE_CPT_SLUG',       'offices'); // public URL base: /offices/*

/** Register CPT and a single "data" meta object */
function office_cpt_register_web_enabled() {
    register_post_type('office-cpt', [
        'label'               => 'Offices',
        'labels'              => ['singular_name' => 'Office'],
        // Web-enabled + API
        'public'              => true,
        'publicly_queryable'  => true,
        'exclude_from_search' => false,                 // include in site search
        'show_ui'             => true,
        'show_in_menu'        => true,
        'menu_icon'           => 'dashicons-database',
        'has_archive'         => true,                  // /offices/
        'rewrite'             => [
            'slug'       => OFFICE_CPT_SLUG,           // /offices/{post-slug}
            'with_front' => false,
        ],
        'show_in_rest'        => true,                  // /wp-json/wp/v2/offices
        'rest_base'           => 'offices',
        'supports'            => ['title'],             // keep minimal; expand if needed
        'map_meta_cap'        => true,
    ]);

    // Auth for writes (reads via our custom GET endpoints can be public)
    $auth_write_cb = function($allowed, $meta_key, $post_id, $user_id, $cap, $caps) {
        return current_user_can('edit_post', $post_id);
    };

    // Single "data" object to hold arbitrary fields (TEL/FAX/Email/etc.)
    register_meta('post', 'data', [
        'object_subtype'   => 'office-cpt',
        'type'             => 'object',
        'single'           => true,
        'show_in_rest'     => [
            'schema' => [
                'type'                 => 'object',
                'additionalProperties' => true, // accept any fields
            ],
        ],
        'auth_callback'    => $auth_write_cb,     // controls write via core routes
        'sanitize_callback'=> function($value) {
            if (!is_array($value)) return [];
            $clean = [];
            foreach ($value as $k => $v) {
                if (is_string($v))        { $clean[$k] = sanitize_textarea_field($v); }
                else if (is_array($v))    { $clean[$k] = $v; }
                else if (is_bool($v))     { $clean[$k] = (bool)$v; }
                else if (is_numeric($v))  { $clean[$k] = 0 + $v; }
                else                      { $clean[$k] = $v; }
            }
            return $clean;
        },
    ]);
}
add_action('init', 'office_cpt_register_web_enabled', 10);

/** Add permalink to core REST objects for convenience */
add_action('rest_api_init', function () {
    register_rest_field('office-cpt', 'permalink', [
        'get_callback' => function($obj) {
            return isset($obj['id']) ? get_permalink((int)$obj['id']) : '';
        },
        'schema' => [
            'description' => 'Public URL for this Office',
            'type'        => 'string',
            'context'     => ['view'],
        ],
    ]);
});

/** Custom REST: simple JSON for one post and list (default = show all) */
add_action('rest_api_init', function () {

    // GET/POST one: /wp-json/office/v1/post/{id}
    register_rest_route('office/v1', '/post/(?P<id>\d+)', [
        [
            'methods'  => 'GET',
            'permission_callback' => '__return_true', // public read OK
            'callback' => function ($req) {
                $id = (int) $req['id'];
                if (get_post_type($id) !== 'office-cpt') {
                    return new WP_Error('invalid_type', 'Not an office-cpt', ['status' => 404]);
                }
                $p = get_post($id);
                return [
                    'id'       => $id,
                    'slug'     => $p ? $p->post_name : '',
                    'title'    => $p ? get_the_title($id) : '',
                    'permalink'=> $p ? get_permalink($id) : '',
                    'data'     => get_post_meta($id, 'data', true) ?: (object)[],
                ];
            },
        ],
        [
            'methods'  => 'POST',
            'permission_callback' => function ($req) {
                $id = (int) $req['id'];
                return current_user_can('edit_post', $id);
            },
            'args' => [
                'data' => ['type' => 'object', 'required' => true],
            ],
            'callback' => function ($req) {
                $id   = (int) $req['id'];
                if (get_post_type($id) !== 'office-cpt') {
                    return new WP_Error('invalid_type', 'Not an office-cpt', ['status' => 404]);
                }
                $data = $req->get_param('data');
                update_post_meta($id, 'data', $data);
                $p = get_post($id);
                return [
                    'ok'       => true,
                    'id'       => $id,
                    'slug'     => $p ? $p->post_name : '',
                    'title'    => $p ? get_the_title($id) : '',
                    'permalink'=> $p ? get_permalink($id) : '',
                    'data'     => get_post_meta($id, 'data', true) ?: (object)[],
                ];
            },
        ],
    ]);

    // GET list: /wp-json/office/v1/posts  (defaults to ALL unless per_page is provided)
    register_rest_route('office/v1', '/posts', [
        'methods'  => 'GET',
        'permission_callback' => '__return_true',
        'args' => [
            'per_page' => ['type'=>'integer','minimum'=>-1,'maximum'=>100], // default handled in code
            'page'     => ['type'=>'integer','default'=>1,'minimum'=>1],
            'orderby'  => ['type'=>'string','default'=>'title'], // 'title' or 'date'
            'order'    => ['type'=>'string','default'=>'asc'],   // 'asc' or 'desc'
        ],
        'callback' => function ($req) {
            // Default to ALL if per_page not specified
            $per_param = $req->get_param('per_page');
            $per = ($per_param === null) ? -1 : (int) $per_param;
            if ($per > 100 && $per !== -1) $per = 100; // cap unless -1 (all)
            $pg  = max(1, (int)$req->get_param('page'));

            $orderby = ($req->get_param('orderby') === 'date') ? 'date' : 'title';
            $order   = (strtolower($req->get_param('order')) === 'desc') ? 'DESC' : 'ASC';

            $q = new WP_Query([
                'post_type'      => 'office-cpt',
                'post_status'    => 'any',
                'posts_per_page' => $per,               // -1 => all
                'paged'          => $pg,
                'orderby'        => $orderby,
                'order'          => $order,
                'fields'         => 'ids',
                'no_found_rows'  => ($per === -1),
            ]);

            $items = array_map(function($id){
                $p = get_post($id);
                return [
                    'id'        => $id,
                    'slug'      => $p ? $p->post_name : '',
                    'title'     => $p ? get_the_title($id) : '',
                    'permalink' => $p ? get_permalink($id) : '',
                    'data'      => get_post_meta($id, 'data', true) ?: (object)[],
                ];
            }, $q->posts);

            return [
                'items'       => $items,
                'total'       => ($per === -1) ? count($items) : (int) $q->found_posts,
                'total_pages' => ($per === -1) ? 1 : (int) $q->max_num_pages,
                'page'        => (int) $pg,
                'per_page'    => (int) $per,
                'archive'     => get_post_type_archive_link('office-cpt'),
                'base'        => home_url( '/' . OFFICE_CPT_SLUG . '/' ),
            ];
        }
    ]);

    // Utility: surface base + archive URLs
    register_rest_route('office/v1', '/urls', [
        'methods'  => 'GET',
        'permission_callback' => '__return_true',
        'callback' => function () {
            return [
                'archive' => get_post_type_archive_link('office-cpt'),
                'base'    => home_url( '/' . OFFICE_CPT_SLUG . '/' ),
            ];
        }
    ]);
});

/** One-time maintenance flag + flush rewrites when version changes */
add_action('admin_init', function () {
    if (!current_user_can('manage_options')) return;
    $stored_version = get_option(OFFICE_CPT_MU_OPTION);
    if ($stored_version !== OFFICE_CPT_MU_VERSION) {
        update_option(OFFICE_CPT_MU_OPTION, OFFICE_CPT_MU_VERSION, true);
        // Ensure new /offices/* routes work immediately
        if (function_exists('flush_rewrite_rules')) {
            flush_rewrite_rules(false);
        }
    }
});

add_action('pre_get_posts', function($query) {
    if (!is_admin() && $query->is_main_query() && is_post_type_archive('office-cpt')) {
        $query->set('posts_per_page', -1); // -1 means "no limit"
    }
});
