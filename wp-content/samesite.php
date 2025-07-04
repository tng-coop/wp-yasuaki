<?php
/**
 * Tell WP to emit its auth cookies with SameSite=None; Secure
 */
add_action( 'send_headers', function() {
    // grab every Set-Cookie header WP is about to send,
    // strip off the old one and re-emit it with SameSite=None
    $list = headers_list();
    foreach( $list as $header ){
        if( stripos( $header, 'Set-Cookie:' ) === 0 ) {
            header_remove( 'Set-Cookie' );
            header( $header . '; SameSite=None; Secure', false );
        }
    }
});

