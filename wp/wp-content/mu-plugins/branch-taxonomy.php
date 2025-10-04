<?php
/**
 * Plugin Name: Branch Taxonomy
 * Description: Registers the 'branch' taxonomy and REST-visible term meta used by seeding + assignment.
 */

add_action('init', function () {
    // Avoid double-registration if a theme/plugin already did it.
    if (!taxonomy_exists('branch')) {
        register_taxonomy('branch', ['post','page'], [
            'labels' => [
                'name'          => 'Branches',
                'singular_name' => 'Branch',
            ],
            'public'            => true,
            'show_ui'           => true,
            'show_in_rest'      => true,            // needed for REST seeding
            'hierarchical'      => false,           // set true if you want Region > Branch trees
            'rewrite'           => ['slug' => 'branch'],
            'show_admin_column' => true,
        ]);
    }

    // REST-visible term meta to link → Address CPT and hold external code
    $meta = ['single'=>true, 'show_in_rest'=>true, 'auth_callback'=>'__return_true'];
    register_term_meta('branch', 'csv_id',          array_merge($meta, ['type'=>'string']));
    register_term_meta('branch', 'address_post_id', array_merge($meta, ['type'=>'integer']));
});
