<?php
/**
 * MU-Plugin: Log password reset key validation
 * (Place this file in wp-content/mu-plugins/password-reset-debug.php)
 */

add_action( 'login_init', function() {
    if (
        isset( $_REQUEST['action'], $_REQUEST['key'], $_REQUEST['login'] )
        && $_REQUEST['action'] === 'rp'
    ) {
        $key   = sanitize_text_field( wp_unslash( $_REQUEST['key']  ) );
        $login = sanitize_text_field( wp_unslash( $_REQUEST['login'] ) );
        $result = check_password_reset_key( $key, $login );

        if ( is_wp_error( $result ) ) {
            error_log( sprintf(
                '🔑 reset-key error: user=%s key=%s codes=[%s] msg=%s',
                $login,
                $key,
                implode( ',', $result->get_error_codes() ),
                $result->get_error_message()
            ) );
        } else {
            error_log( sprintf(
                '✅ reset-key valid for user %s',
                $login
            ) );
        }
    }
} );

