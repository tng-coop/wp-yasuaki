<?php
/**
 * MU: Shared ESM
 * Registers a shared ESM handle other modules can depend on.
 */
defined('ABSPATH') || exit;

add_action('init', function () {
  if (!function_exists('wp_register_script_module')) return; // WP < 6.5 guard
  wp_register_script_module(
    'shared/preload',                                   // handle
    content_url('mu-plugins/shared-esm/preload.js'),    // URL to your shared ESM
    []                                                  // deps
  );
});

