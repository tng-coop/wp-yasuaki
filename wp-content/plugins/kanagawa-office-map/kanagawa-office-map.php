<?php
/**
 * Plugin Name: Hero Video
 * Description: Displays the JSON configuration for the Hero Video (Goat or Waterfall video) in both editor and front-end.
 * Version:     1.0.0
 * Author:      Your Name
 * License:     GPL-2.0+
 */

defined( 'ABSPATH' ) || exit;

function kanagawa_register_video_block() {
    register_block_type( __DIR__ . '/block' );
}
add_action( 'init', 'kanagawa_register_video_block' );
