<?php
/**
 * Plugin Name: Hero Video
 * Description: Gutenberg block that plays alternating Pexels videos (server-optimized sources). ESM + WP 6.5+.
 * Version:     2.0.0
 * Author:      Your Name
 */

defined('ABSPATH') || exit;

/**
 * Registers the block from the flat block.json (WP automatically wires style, editor/view modules, versions via filemtime).
 */
add_action('init', function () {
    register_block_type(__DIR__);
});
