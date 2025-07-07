<?php
/**
 * Plugin Name: Goat Video Config Block
 * Description: Displays the JSON configuration for the Pexels "goat" video (ID 30646036) in both editor and front-end.
 * Version:     1.0.0
 * Author:      Your Name
 * License:     GPL-2.0+
 */

defined( 'ABSPATH' ) || exit;

function goat_register_video_config_block() {
    register_block_type( __DIR__ . '/block' );
}
add_action( 'init', 'goat_register_video_config_block' );
