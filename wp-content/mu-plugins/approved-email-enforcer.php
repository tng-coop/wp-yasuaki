<?php
/**
 * Plugin Name: Approved Email Enforcer
 * Description: Prevents new user registration unless the email is on the approved list.
 */

defined( 'ABSPATH' ) || exit;

/**
 * Fetch the list of approved emails via the internal REST API.
 *
 * @return array List of lowercase approved emails.
 */
function ael_fetch_emails_via_api() {
    // Temporarily switch to a privileged user to satisfy permission checks.
    $admins  = get_users( [ 'role' => 'administrator', 'number' => 1, 'fields' => 'ID' ] );
    $current = get_current_user_id();
    if ( ! empty( $admins ) ) {
        wp_set_current_user( $admins[0] );
    }

    $request  = new WP_REST_Request( 'GET', '/approved-email-list/v1/approved-emails' );
    $response = rest_do_request( $request );

    // Restore previous user.
    wp_set_current_user( $current );

    if ( is_wp_error( $response ) ) {
        return [];
    }

    $data = $response->get_data();
    return is_array( $data ) ? array_map( 'strtolower', $data ) : [];
}

/**
 * Check registration email against approved list.
 */
function ael_block_unapproved_registration( $errors, $sanitized_user_login, $user_email ) {
    $approved = ael_fetch_emails_via_api();
    if ( $user_email && ! in_array( strtolower( $user_email ), $approved, true ) ) {
        $errors->add( 'email_not_approved', __( 'This email address is not approved for registration.' ) );
    }
    return $errors;
}
add_filter( 'registration_errors', 'ael_block_unapproved_registration', 10, 3 );

/**
 * Utility for other plugins to verify an email.
 *
 * @param string $email Email to check.
 * @return bool Whether the email is approved.
 */
function ael_is_email_approved( $email ) {
    static $cache = null;
    if ( null === $cache ) {
        $cache = ael_fetch_emails_via_api();
    }
    return in_array( strtolower( $email ), $cache, true );
}

/**
 * If a new user's email is approved, default their role to Author.
 */
add_action( 'user_register', function( $user_id ) {
    // Make sure helper is available
    if ( ! function_exists( 'ael_is_email_approved' ) ) {
        return;
    }

    $user = get_user_by( 'id', $user_id );
    if ( ! $user || empty( $user->user_email ) ) {
        return;
    }

    if ( ael_is_email_approved( $user->user_email ) ) {
        $roles = (array) $user->roles;

        // Only promote if they’re role-less or a Subscriber.
        if ( empty( $roles ) || in_array( 'subscriber', $roles, true ) ) {
            $user->set_role( 'author' );
        }
    }
}, 20 );
