<?php
/**
 * Plugin Name: OfficeMap
 * Description: OfficeMap nonce starter (Gutenberg dynamic block + ESM view script).
 * Version: 0.1.0
 * Author: You
 */
defined('ABSPATH') || exit;

add_action('init', function () {
    // Register block from metadata (block.json)
    register_block_type(__DIR__ . '/block');
});
