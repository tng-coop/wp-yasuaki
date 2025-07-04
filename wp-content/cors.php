<?php
// File: wp-content/mu-plugins/cors.php

add_action( 'rest_api_init', function() {
    // Whitelisted origins
    $allowed_origins = [
        'https://tng.coop',
        'https://aspnet.lan',
    ];

    // ===== 1) CATCH PRE-FLIGHT =====
    // Short-circuit OPTIONS before WP ever authenticates.
    add_action( 'rest_pre_serve_request', function( $served, $result, $request ) use ( $allowed_origins ) {
        if ( 'OPTIONS' === $request->get_method() ) {
            $origin = $request->get_header( 'origin' ) ?: '';
            if ( in_array( $origin, $allowed_origins, true ) ) {
                header( 'Access-Control-Allow-Origin: '   . esc_url_raw( $origin ) );
                header( 'Vary: Origin' );
                header( 'Access-Control-Allow-Credentials: true' );
                header( 'Access-Control-Allow-Methods: GET, POST, PUT, PATCH, DELETE, OPTIONS' );
                header( 'Access-Control-Allow-Headers: Authorization, Content-Type, X-WP-Nonce' );
            }
            // stop here — return a 204 No Content
            http_response_code( 204 );
            exit;
        }
        return $served;
    }, 0, 3 );

    // ===== 2) ADD CORS TO _ALL_ REST RESPONSES =====
    // After WP has handled auth & routing, but before it sends headers.
    add_filter( 'rest_post_dispatch', function( $response, $server, $request ) use ( $allowed_origins ) {
        $origin = $request->get_header( 'origin' ) ?: '';
        if ( in_array( $origin, $allowed_origins, true ) ) {
            header( 'Access-Control-Allow-Origin: '   . esc_url_raw( $origin ) );
            header( 'Vary: Origin' );
            header( 'Access-Control-Allow-Credentials: true' );
            // expose these so JS can read paging info if you need it
            header( 'Access-Control-Expose-Headers: X-WP-Total, X-WP-TotalPages, Link' );
            // you already handled OPTIONS above, but these won’t hurt here
            header( 'Access-Control-Allow-Methods: GET, POST, PUT, PATCH, DELETE, OPTIONS' );
            header( 'Access-Control-Allow-Headers: Authorization, Content-Type, X-WP-Nonce' );
        }
        return $response;
    }, 10, 3 );
}, 0 );

