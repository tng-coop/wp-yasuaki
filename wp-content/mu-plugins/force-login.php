<?php
/**
 * Plugin Name: Force Login to Bible Page (MU) with Persistent Admin Bar Toggle
 * Description: Redirect all non-logged-in visitors to a custom Bible page and let logged-in users toggle the front-end admin bar persistently via a cookie.
 * Version:     1.3
 * Author:      Your Name
 * License:     GPL2
 */

// When a toolbar param is present, set (or clear) a cookie to remember the choice
add_action( 'init', function() {
    if ( isset( $_GET['toolbar'] ) ) {
        $val = $_GET['toolbar'] === '1' ? '1' : '0';
        // cookie lasts 7 days; path `/` so it applies everywhere
        setcookie( 'show_toolbar', $val, time() + 7 * DAY_IN_SECONDS, '/' );
        // also update the superglobals for current request
        $_COOKIE['show_toolbar'] = $val;
    }
} );

// Toggle admin bar based on the cookie (and still protect login/ajax)
add_filter( 'show_admin_bar', function( $show ) {
    // always hide toolbar for anonymous, login, cron, AJAX calls
    if (
        ! is_user_logged_in()
        || preg_match( '#wp-(login|cron|admin\.php)#i', $_SERVER['PHP_SELF'] )
        || defined( 'DOING_AJAX' )
    ) {
        return false;
    }

    // if cookie set to '1', show; if '0' or missing, hide
    return ( isset( $_COOKIE['show_toolbar'] ) && $_COOKIE['show_toolbar'] === '1' );
} );

// Redirect anonymous users to your Bible page
add_action( 'template_redirect', function () {
    if (
        is_user_logged_in()
        || preg_match( '#wp-(login|cron|admin\.php)#i', $_SERVER['PHP_SELF'] )
        || defined( 'DOING_AJAX' )
    ) {
        return;
    }

    wp_safe_redirect( 'https://yasuaki.com/bible.html' );
    exit;
} );

