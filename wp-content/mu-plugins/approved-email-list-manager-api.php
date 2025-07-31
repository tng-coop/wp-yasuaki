<?php
/**
 * File: approved-email-list-manager-api.php
 * Location: wp-content/mu-plugins/
 *
 * Plugin Name: Approved Email List Manager (API Only)
 * Description: MU plugin to manage an approved email list via REST API endpoints. Accessible to Editors and above.
 * Version: 1.3
 * Author: ChatGPT
 *
 * Usage Examples:
 *
 * 1. Retrieve all approved emails (GET):
 *    curl -u editor_username:editor_password \
 *         -X GET https://example.com/wp-json/approved-email-list/v1/approved-emails
 *
 * 2. Add a new approved email (POST):
 *    curl -u editor_username:editor_password \
 *         -X POST https://example.com/wp-json/approved-email-list/v1/approved-emails \
 *         -H "Content-Type: application/json" \
 *         -d '{"email":"newuser@example.com"}'
 *
 * 3. Remove an approved email (DELETE):
 *    curl -u editor_username:editor_password \
 *         -X DELETE https://example.com/wp-json/approved-email-list/v1/approved-emails/newuser@example.com
 *
 * JavaScript (Fetch) Example:
 *    fetch('/wp-json/approved-email-list/v1/approved-emails', {
 *        method: 'POST',
 *        credentials: 'include',
 *        headers: { 'Content-Type': 'application/json' },
 *        body: JSON.stringify({ email: 'user@example.com' })
 *    })
 *    .then(res => res.json())
 *    .then(data => console.log(data));
 */

// Prevent direct access
if ( ! defined( 'ABSPATH' ) ) {
    exit;
}

class Approved_Email_List_Manager_API {
    const OPTION_NAME    = 'approved_email_list';
    const REST_NAMESPACE = 'approved-email-list/v1';

    public function __construct() {
        add_action( 'rest_api_init', array( $this, 'register_routes' ) );
    }

    /**
     * Register REST API routes
     */
    public function register_routes() {
        register_rest_route( self::REST_NAMESPACE, '/approved-emails', array(
            'methods'             => 'GET',
            'callback'            => array( $this, 'get_emails' ),
            'permission_callback' => array( $this, 'permissions_check' ),
        ) );

        register_rest_route( self::REST_NAMESPACE, '/approved-emails', array(
            'methods'             => 'POST',
            'callback'            => array( $this, 'add_email' ),
            'permission_callback' => array( $this, 'permissions_check' ),
            'args'                => array(
                'email' => array(
                    'required'          => true,
                    'validate_callback' => function( $param ) {
                        return is_email( $param );
                    },
                ),
            ),
        ) );

        register_rest_route( self::REST_NAMESPACE, '/approved-emails/(?P<email>[^/]+)', array(
            'methods'             => 'DELETE',
            'callback'            => array( $this, 'remove_email' ),
            'permission_callback' => array( $this, 'permissions_check' ),
            'args'                => array(
                'email' => array(
                    'validate_callback' => function( $param ) {
                        return is_email( $param );
                    },
                ),
            ),
        ) );
    }

    /**
     * Permission check: only Editors and above
     */
    public function permissions_check( $request ) {
        return current_user_can( 'edit_pages' );
    }

    /**
     * Retrieve approved emails
     */
    public function get_emails( $request ) {
        $emails = get_option( self::OPTION_NAME, array() );
        return rest_ensure_response( array_values( $emails ) );
    }

    /**
     * Add a new approved email
     */
    public function add_email( $request ) {
        $email  = strtolower( $request->get_param( 'email' ) );
        $emails = get_option( self::OPTION_NAME, array() );

        if ( in_array( $email, $emails, true ) ) {
            return new WP_Error( 'email_exists', 'Email is already approved.', array( 'status' => 409 ) );
        }

        $emails[] = $email;
        update_option( self::OPTION_NAME, array_unique( $emails ) );

        return rest_ensure_response( array( 'email' => $email, 'added' => true ) );
    }

    /**
     * Remove an approved email
     */
    public function remove_email( $request ) {
        $email = $request->get_param( 'email' );

        // WP doesn't parse JSON bodies for DELETE by default, so check manually.
        if ( empty( $email ) ) {
            $json = $request->get_json_params();
            if ( isset( $json['email'] ) ) {
                $email = $json['email'];
            }
        }

        if ( empty( $email ) ) {
            return new WP_Error( 'missing_email', 'Email parameter is required.', array( 'status' => 400 ) );
        }

        $email  = strtolower( $email );
        $emails = get_option( self::OPTION_NAME, array() );

        if ( ! in_array( $email, $emails, true ) ) {
            return new WP_Error( 'email_not_found', 'Email is not in the approved list.', array( 'status' => 404 ) );
        }

        $updated = array_diff( $emails, array( $email ) );
        update_option( self::OPTION_NAME, array_values( $updated ) );

        return rest_ensure_response( array( 'email' => $email, 'removed' => true ) );
    }
}

// Initialize the API-only MU plugin
new Approved_Email_List_Manager_API();

