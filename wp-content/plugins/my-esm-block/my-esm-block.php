<?php
/**
 * Plugin Name:  My ESM Block
 * Description:  Native‑ESM Gutenberg block (no build tools, direct esm.sh imports).
 * Version:      1.0.0
 * Requires at least: 6.5
 * Author:       Your Name
 * Text Domain:  my-esm-block
 */

add_action(
	'init',
	function () {
		// Register metadata & server‑side config from block.json.
		register_block_type( __DIR__ );
	}
);
error_log( 'My ESM Block plugin loaded.' );
add_action(
	'enqueue_block_editor_assets',
	function () {
		// Enqueue index.js *as a module* – no dependencies declared here.
		wp_enqueue_script_module(
			'my-esm-block-editor',
			plugins_url( 'index.js', __FILE__ ),
			array(),                                // no PHP‑side deps
			filemtime( __DIR__ . '/index.js' )
		);
	}
);
