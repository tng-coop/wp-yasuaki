<?php
/**
 * Plugin Name:     Hello World (Modern)
 * Description:     A minimal ES-module block using the latest block metadata standards.
 * Version:         1.0.0
 * Requires at least: 6.5
 * Requires PHP:    7.4
 * Author:          Master
 * Text Domain:     hello-world-modern
 */

defined( 'ABSPATH' ) || exit;

add_action( 'init', function () {
	// Registers the block from metadata and auto-enqueues module scripts/styles.
	register_block_type( __DIR__ . '/block' );
} );
