<?php
/**
 * Plugin Name: Registration Query Gatekeeper
 * Description: Blocks user registration unless the URL includes abc=123.
 */

// Exit if accessed directly.
defined( 'ABSPATH' ) || exit;

add_filter( 'registration_errors', function( $errors, $user_login, $user_email ) {
    if ( ! isset( $_GET['abc'] ) || $_GET['abc'] !== '123' ) {
        $errors->add( 'registration_blocked', __( 'Registration is restricted.', 'restrict-registration' ) );
    }
    return $errors;
}, 10, 3 );
