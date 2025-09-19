<?php
/**
 * Plugin Name: Admin Toolbar Toggle (MU) — Default Hidden
 * Description: Front-end WordPress admin toolbar is hidden by default. Users can opt in/out with ?toolbar=1 or ?toolbar=0; a cookie remembers the choice for 7 days. No other behavior.
 * Author: Site Ops
 */

if (!defined('ABSPATH')) { exit; }

/**
 * Handle explicit toggle requests via query string:
 *   ?toolbar=1  -> set cookie to show bar on front-end
 *   ?toolbar=0  -> remove/hide
 */
add_action('init', function () {
  if (!isset($_GET['toolbar'])) return;

  $val = $_GET['toolbar'];
  // Normalize to '1' or '0'
  $val = ($val === '1') ? '1' : '0';

  // Cookie settings
  $name   = 'show_toolbar';
  $path   = COOKIEPATH ? COOKIEPATH : '/';
  $secure = is_ssl();
  $httponly = true; // JS doesn't need this
  $samesite = 'Lax'; // safe default for WP front-end

  if ($val === '1') {
    // Persist for 7 days
    setcookie($name, '1', [
      'expires'  => time() + 7 * DAY_IN_SECONDS,
      'path'     => $path,
      'secure'   => $secure,
      'httponly' => $httponly,
      'samesite' => $samesite,
    ]);
    $_COOKIE[$name] = '1';
  } else {
    // Remove cookie
    setcookie($name, '', [
      'expires'  => time() - 3600,
      'path'     => $path,
      'secure'   => $secure,
      'httponly' => $httponly,
      'samesite' => $samesite,
    ]);
    unset($_COOKIE[$name]);
  }
});

/**
 * Front-end toolbar policy:
 *  - Default: HIDE (no cookie or cookie != '1')
 *  - Show only when cookie == '1'
 *  - Admin screens (wp-admin) keep WordPress defaults
 */
add_filter('show_admin_bar', function ($current) {
  // Always keep wp-admin behavior unchanged
  if (is_admin()) return $current;

  // Logged-out users: hide (WordPress would anyway)
  if (!is_user_logged_in()) return false;

  // Default hidden; cookie opt-in shows it
  return (isset($_COOKIE['show_toolbar']) && $_COOKIE['show_toolbar'] === '1');
}, 99);

/**
 * Optional: remove the front-end CSS bump if the toolbar is hidden,
 * so layout doesn’t shift when default-hidden.
 */
add_action('wp_head', function () {
  $show = (isset($_COOKIE['show_toolbar']) && $_COOKIE['show_toolbar'] === '1');
  if (!$show) {
    remove_action('wp_head', '_admin_bar_bump_cb');
  }
}, 1);
