<?php
/**
 * Plugin Name: Site Language Switcher (Block)
 * Description: Adds a header language switcher (EN / JP) as a Gutenberg block.
 * Version:     1.0.0
 * Author:      Your Name
 * License:     GPL‑2.0+
 */

defined( 'ABSPATH' ) || exit;

function sls_register_block() {
	$block_dir = __DIR__ . '/block';

	// Automatically registers script & style handles from block.json.
	register_block_type( $block_dir );
}
add_action( 'init', 'sls_register_block' );