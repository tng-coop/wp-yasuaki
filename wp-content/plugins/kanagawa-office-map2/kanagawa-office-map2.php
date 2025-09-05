<?php
/**
* Plugin Name:     Kanagawa Office Map2
* Plugin URI:      https://github.com/yourusername/kanagawa-office-map
* Description:     Displays a hexbin mosaic of service offices across Kanagawa Prefecture using Leaflet, D3, and PapaParse.
* Version:         1.0.0
* Author:          Your Name
* Author URI:      https://yourwebsite.example.com
* Text Domain:     kanagawa-office-map
*/

defined( 'ABSPATH' ) || exit;

function kanagawa_register_office_map_block2() {
    register_block_type( __DIR__ . '/block' );
}
add_action( 'init', 'kanagawa_register_office_map_block2' );
