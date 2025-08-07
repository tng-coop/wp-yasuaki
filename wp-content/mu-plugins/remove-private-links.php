<?php
/**
 * Plugin Name: MU – Remove BlazorApp Links
 * Description: Strips out any core/navigation-link block items linking to "/blazorapp" (or any subpath), only for logged-out users.
 * Author: Your Name
 * Network: true
 */

/**
 * Remove <a> tags pointing at /blazorapp/* for anyone who cannot edit posts.
 */
function mu_remove_blazorapp_path_links_for_low_roles( $html, $block ) {
    $url = $block['attrs']['url'] ?? '';
    if ( ! $url ) {
        return $html;
    }

    // Only target links whose path begins with /blazorapp
    $path = parse_url( $url, PHP_URL_PATH ) ?: '';
    if ( ! preg_match( '#^/blazorapp(/|$)#i', $path ) ) {
        return $html;
    }

    // If the current user cannot edit posts (i.e. is not a Contributor or higher), strip the link
    if ( ! current_user_can( 'edit_posts' ) ) {
        return '';
    }

    // Otherwise, leave the HTML intact
    return $html;
}
// Adjust the hook name/place as appropriate for your block/render setup:
add_filter( 'render_block', 'mu_remove_blazorapp_path_links_for_low_roles', 10, 2 );

