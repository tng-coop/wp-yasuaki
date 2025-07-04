<?php
/**
 * MU-plugin: a safe cookie-only “get me a wp_rest nonce” endpoint.
 * Only returns a nonce if the real WP login cookie is valid.
 */

add_action( 'rest_pre_dispatch', function( $result, $server, $request ) {
    // Intercept only GET /wp-json/myplugin/v1/nonce
    if ( $request->get_method() === 'GET'
      && $request->get_route()  === '/myplugin/v1/nonce'
    ) {
        // Manually validate the "logged_in" cookie (no X-WP-Nonce needed here)
        $user_id = wp_validate_auth_cookie( '', 'logged_in' );
        if ( ! $user_id ) {
            return new WP_Error(
              'rest_not_logged_in',
              'You are not currently logged in.',
              [ 'status' => 403 ]
            );
        }

        // Establish that user for the rest of this request
        wp_set_current_user( $user_id );

        // Return a fresh wp_rest nonce
        return rest_ensure_response([
          'nonce' => wp_create_nonce( 'wp_rest' ),
        ]);
    }

    return $result;
}, 0, 3 );

add_action( 'rest_api_init', function() {
    register_rest_route( 'myplugin/v1', '/nonce', [
        'methods'             => 'GET',
        'permission_callback' => '__return_true',
        'callback'            => function() {
            // never called—rest_pre_dispatch already handled it
            return new WP_Error( 'rest_method_not_allowed', 'Method not allowed', [ 'status' => 405 ] );
        },
    ] );
} );

