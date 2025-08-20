<?php
/**
 * Plugin Name: Fullshow
 * Plugin URI: https://example.com/
 * Description: A minimal Gutenberg block that shows "hello".
 * Version: 1.0.0
 * Requires at least: 6.5
 * Requires PHP: 7.4
 * Author: You
 * License: GPL-2.0-or-later
 * License URI: https://www.gnu.org/licenses/gpl-2.0.html
 * Text Domain: fullshow
 */

if ( ! defined( 'ABSPATH' ) ) {
	exit;
}

add_action( 'init', function () {
	// Registers the block using the block.json in this directory.
	register_block_type( __DIR__ );
} );
