<?php
/**
 * Plugin Name: Hero Video
 * Description: Gutenberg block that plays alternating Pexels videos (server-optimized sources). ESM + WP 6.5+.
 * Version:     2.0.0
 * Author:      Your Name
 */

defined('ABSPATH') || exit;

// Register block
add_action('init', function () {
    register_block_type(__DIR__);
});

// Admin notice if build is missing
add_action('admin_notices', function () {
    $path = __DIR__ . '/build/view.js';
    if (!file_exists($path)) {
        echo '<div class="notice notice-error"><p><strong>Hero Video:</strong> '
           . 'build/view.js is missing. Run <code>npm ci && npm run build</code> '
           . 'before deploying.</p></div>';
    }
});
