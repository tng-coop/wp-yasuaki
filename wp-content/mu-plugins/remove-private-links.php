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


    $user = wp_get_current_user();

    // 2) Build a little debug string
    $roles_str   = ! empty( $user->roles ) ? implode( ',', $user->roles ) : 'none';
    $can_edit    = current_user_can( 'edit_posts' ) ? 'yes' : 'no';

    // 3) Log it (go check your PHP error log)
    error_log( sprintf(
        '[blazorapp‐debug] user_id=%d user_login=%s roles=[%s] can_edit_posts=%s',
        $user->ID,
        $user->user_login,
        $roles_str,
        $can_edit
    ) );



    // Otherwise, leave the HTML intact
    return $html;
}
// Adjust the hook name/place as appropriate for your block/render setup:
add_filter( 'render_block', 'mu_remove_blazorapp_path_links_for_low_roles', 10, 2 );

