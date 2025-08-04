<?php
/**
 * Plugin Name: MU – Remove BlazorApp Links
 * Description: Strips out any core/navigation-link block items linking to "/blazorapp" (or any subpath), only for logged-out users.
 * Author: Your Name
 * Network: true
 */

add_filter( 'render_block_core/navigation-link', 'mu_remove_blazorapp_path_links_if_logged_out', 10, 2 );

function mu_remove_blazorapp_path_links_if_logged_out( $html, $block ) {
    // Only suppress for users NOT logged in
    if ( is_user_logged_in() ) {
        return $html;
    }

    $url = $block['attrs']['url'] ?? '';

    if ( $url ) {
        // Parse the path from the URL
        $path = parse_url( $url, PHP_URL_PATH ) ?: '';

        // Normalize leading slash and check if it starts with /blazorapp
        if ( preg_match( '#^/blazorapp(/|$)#i', $path ) ) {
            return '';
        }
    }

    // Keep original HTML if conditions aren't met
    return $html;
}

