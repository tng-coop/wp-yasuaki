<?php
/**
 * Plugin Name: Hero Video
 * Description: Gutenberg block that plays alternating Pexels videos (with server-optimized sources).
 * Version:     1.0.0
 * Author:      Your Name
 */

defined('ABSPATH') || exit;

/**
 * Register the block (block.json handles assets like style.css, index.js, render.php).
 */
add_action('init', function () {
    register_block_type(__DIR__ . '/block');
});

/**
 * Register the view module handle (WP 6.5+). It depends on the shared preload module.
 * The shared module 'shared/preload' is registered by your MU plugin.
 */
add_action('init', function () {
    if (!function_exists('wp_register_script_module')) return; // Guard for WP < 6.5
    wp_register_script_module(
        'hero/video-view',                      // handle for your block's view.js
        plugins_url('block/view.js', __FILE__), // URL to your ESM file
        ['shared/preload']                      // dependency (from MU plugin)
    );
});

/**
 * FRONTEND: enqueue the view module exactly when the block renders.
 * This is more reliable than checking has_block() on complex templates.
 */
add_filter('render_block', function ($content, $block) {
    if (!function_exists('wp_enqueue_script_module')) return $content;
    if (!empty($block['blockName']) && $block['blockName'] === 'hero/video') {
        wp_enqueue_script_module('hero/video-view');
    }
    return $content;
}, 10, 2);

/**
 * EDITOR: enqueue the view module so the block preview works in the editor.
 */
add_action('enqueue_block_editor_assets', function () {
    if (!function_exists('wp_enqueue_script_module')) return;
    wp_enqueue_script_module('hero/video-view');
});
