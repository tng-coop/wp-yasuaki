<?php
/**
 * Plugin Name:     Kanagawa Office List
 * Plugin URI:      https://github.com/yourusername/kanagawa-office-list
 * Description:     Displays a list of service offices across Kanagawa Prefecture in a scrollable HTML table.
 * Version:         1.0.0
 * Author:          Your Name
 * Author URI:      https://yourwebsite.example.com
 * Text Domain:     kanagawa-office-list
 */

defined( 'ABSPATH' ) || exit;

function kanagawa_register_office_list_block() {
    register_block_type( __DIR__ . '/block' );
}
add_action( 'init', 'kanagawa_register_office_list_block' );
