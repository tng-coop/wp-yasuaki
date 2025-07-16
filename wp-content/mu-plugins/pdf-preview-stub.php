<?php
/**
 * MU-Plugin: PDF + Preview Bundle Handler
 * Location: wp-content/mu-plugins/pdf_preview_bundle_mu_plugin.php
 *
 * Exposes a REST endpoint at /wp-json/myplugin/v1/media/bundle
 * that accepts a PDF file and multiple preview images in one request,
 * then logs the received data for verification.
 * Access restricted to Editors and Administrators.
 */

add_action( 'rest_api_init', function() {
    register_rest_route( 'myplugin/v1', '/media/bundle', [
        'methods'             => 'POST',
        'callback'            => 'mp_handle_pdf_with_previews',
        'permission_callback' => function() {
            return current_user_can( 'edit_pages' );
        },
        'args'                => [],
    ] );
} );

/**
 * Stub handler: log all received params and files to debug.log
 *
 * @param WP_REST_Request $request
 * @return WP_REST_Response
 */
function mp_handle_pdf_with_previews( WP_REST_Request $request ) {
    if ( defined('WP_DEBUG') && WP_DEBUG && defined('WP_DEBUG_LOG') && WP_DEBUG_LOG ) {
        error_log( '=== mp_handle_pdf_with_previews: params ===\n' . print_r( $request->get_params(), true ) );
        error_log( '=== mp_handle_pdf_with_previews: file params ===\n' . print_r( $request->get_file_params(), true ) );
    }

    // Minimal success response
    return rest_ensure_response( [ 'status' => 'logged' ] );
}
