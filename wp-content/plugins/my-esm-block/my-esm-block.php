<?php
/**
 * Plugin Name:  My ESM Cube Block
 * Description:  Gutenberg block showing a spinning 3‑D cube (Three.js, no build tools).
 * Version:      1.1.0
 * Requires at least: 6.5
 * Author:       Your Name
 * Text Domain:  my-esm-block
 */

add_action(
    'init',
    function () {
        register_block_type( __DIR__ );               // reads block.json
    }
);

/**
 * Editor (back‑office) – load index.js as an ES module.
 */
add_action(
    'enqueue_block_editor_assets',
    function () {
        wp_enqueue_script_module(
            'my-esm-block-editor',
            plugins_url( 'index.js', __FILE__ ),
            array(),                                  // no PHP‑declared deps
            filemtime( __DIR__ . '/index.js' )
        );
    }
);

/**
 * Front‑end – load view.js (lightweight bootstrap) for pages
 * where the cube actually appears.
 */
add_action(
    'enqueue_block_assets',
    function () {
        if ( ! is_admin() ) {
            wp_enqueue_script_module(
                'my-esm-block-view',
                plugins_url( 'view.js', __FILE__ ),
                array(),                              // standalone
                filemtime( __DIR__ . '/view.js' )
            );
        }
    }
);
