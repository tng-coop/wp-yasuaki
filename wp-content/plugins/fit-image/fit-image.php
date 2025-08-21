<?php
/**
 * Plugin Name: Fit Image
 * Description: Provides the <fit-image> custom element and a block that displays the Kanagawa demo map.
 * Version: 1.0.0
 * Requires at least: 6.5
 * Requires PHP: 7.4
 * Author: You
 */

if (!defined('ABSPATH')) exit;

add_action('init', function () {
  // Registers the block from block.json in this directory
  register_block_type(__DIR__);
});
