<?php
/**
 * Plugin Name: Fullshow Cyan Box
 * Plugin URI: https://example.com/
 * Description: A minimal Gutenberg block that displays a cyan-bordered box (no editor inputs).
 * Version: 1.0.0
 * Requires at least: 6.5
 * Requires PHP: 7.4
 * Author: You
 * Text Domain: fullshow
 */

if ( ! defined( 'ABSPATH' ) ) {
	exit;
}

add_action( 'init', function () {
	register_block_type( __DIR__ );
} );
