<?php
/**
 * Plugin Name: Fullbanner
 * Plugin URI: https://example.com/
 * Description: A single-pane banner block sized by the Gutenberg editor.
 * Version: 1.0.0
 * Requires at least: 6.5
 * Requires PHP: 7.4
 * Author: You
 * License: GPL-2.0-or-later
 * License URI: https://www.gnu.org/licenses/gpl-2.0.html
 * Text Domain: fullbanner
 */

if ( ! defined( 'ABSPATH' ) ) {
  exit;
}

add_action( 'init', function () {
  register_block_type( __DIR__ );
} );
