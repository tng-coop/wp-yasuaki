<?php
/**
 * Plugin Name: MU – Adjust BlazorApp Links
 * Description: Adds query flags (e.g. ?ja&basic) to /blazorapp links based on user role and locale, or strips links for logged-out users.
 * Author: Your Name
 * Network: true
 */

function mu_adjust_blazorapp_links( $html, $block ) {
    $url = $block['attrs']['url'] ?? '';
    if ( ! $url ) {
        return $html;
    }

    $path = parse_url( $url, PHP_URL_PATH ) ?: '';
    if ( ! preg_match( '#^/blazorapp(/|$)#i', $path ) ) {
        return $html;
    }

    // If not logged in, remove link entirely
    if ( ! is_user_logged_in() ) {
        return '';
    }

    $user = wp_get_current_user();
    $params = [];

    // Locale: user-specific
    $user_locale = get_user_locale( $user->ID );
    if ( $user_locale === 'ja' || strpos( $user_locale, 'ja_' ) === 0 ) {
        $params[] = 'ja';
    }

    // Role: unless admin, mark as basic
    if ( ! in_array( 'administrator', (array) $user->roles, true ) ) {
        $params[] = 'basic';
    }

    if ( ! empty( $params ) ) {
        $separator = ( strpos( $url, '?' ) !== false ) ? '&' : '?';
        $url .= $separator . implode( '&', $params );

        // Replace in HTML output
        $html = preg_replace(
            '#href=(["\'])' . preg_quote( $block['attrs']['url'], '#' ) . '\1#',
            'href=$1' . esc_url( $url ) . '$1',
            $html
        );
    }

    return $html;
}

add_filter( 'render_block', 'mu_adjust_blazorapp_links', 10, 2 );
