<?php
/**
 * Plugin Name: Fit Image (Web Component)
 * Description: Registers/enqueues the <fit-image> custom element for front-end and block editor.
 * Version: 1.0.0
 * Requires at least: 6.5
 * Requires PHP: 7.4
 * Author: You
 * License: GPL-2.0-or-later
 */

if (!defined('ABSPATH')) exit;

function fit_image_register_modules() {
  // WordPress 6.5+ Script Modules API
  if (!function_exists('wp_register_script_module')) return;

  // Register the root module that defines the custom element.
  wp_register_script_module(
    'fit-image/module',
    plugins_url('fit-image.js', __FILE__),
    array(), // no WP deps; your module imports other local files itself
    filemtime(__DIR__ . '/fit-image.js')
  );
}
add_action('init', 'fit_image_register_modules');

/**
 * Enqueue the module wherever blocks/assets load.
 * This covers both the front-end and the editor iframe so <fit-image> upgrades in both.
 */
function fit_image_enqueue_everywhere() {
  if (function_exists('wp_enqueue_script_module')) {
    wp_enqueue_script_module('fit-image/module');
  } else {
    // Optional legacy fallback for WP < 6.5 (loads as classic script)
    wp_enqueue_script(
      'fit-image/legacy',
      plugins_url('fit-image.js', __FILE__),
      array(),
      filemtime(__DIR__ . '/fit-image.js'),
      array('in_footer' => true)
    );
  }
}
add_action('enqueue_block_assets', 'fit_image_enqueue_everywhere');
