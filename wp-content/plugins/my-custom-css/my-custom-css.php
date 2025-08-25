<?php
/**
 * Plugin Name: My Custom CSS
 * Description: Enqueue one CSS file for both frontend and block editor.
 * Author: You
 * Version: 1.0.0
 */

if ( ! defined( 'ABSPATH' ) ) exit;

add_action('init', function () {
    $file = plugin_dir_path(__FILE__) . 'custom.css';
    $url  = plugin_dir_url(__FILE__) . 'custom.css';

    if ( file_exists($file) ) {
        $ver = filemtime($file);

        // 🔹 Frontend
        add_action('wp_enqueue_scripts', function () use ($url, $ver) {
            wp_enqueue_style('my-custom-css', $url, [], $ver);
        });

        // 🔹 Block editor
        add_action('enqueue_block_editor_assets', function () use ($url, $ver) {
            wp_enqueue_style('my-custom-css-editor', $url, [], $ver);
        });
    }
});

