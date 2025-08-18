<?php
/**
 * Plugin Name: Hero Video
 * Description: Displays the JSON configuration for the Hero Video (Goat or Waterfall video) in both editor and front-end.
 * Version:     1.0.0
 * Author:      Your Name
 * License:     GPL-2.0+
 */

defined( 'ABSPATH' ) || exit;

add_action( 'init', function () {
    register_block_type( __DIR__ . '/block' );
});

add_action( 'rest_api_init', function () {
    register_rest_route( 'hero-video/v1', '/pexels', [
        'methods'  => 'GET',
        'permission_callback' => '__return_true',
        'args' => [
            'id' => [
                'required' => true,
                'sanitize_callback' => function( $v ){ return preg_replace('/\D+/', '', (string)$v ); }
            ]
        ],
        'callback' => function ( WP_REST_Request $req ) {
            $id = $req->get_param('id');
            if ( empty( $id ) ) {
                return new WP_Error( 'bad_request', 'Missing id', [ 'status' => 400 ] );
            }

            $cache_key = "hero_video_pexels_$id";
            if ( $cached = get_transient( $cache_key ) ) {
                return rest_ensure_response( $cached );
            }

            $api_key = getenv('PEXELS_API_KEY'); // set in web server env
            if ( ! $api_key ) {
                return new WP_Error( 'no_api_key', 'PEXELS_API_KEY missing on server', [ 'status' => 500 ] );
            }

            $resp = wp_remote_get( "https://api.pexels.com/videos/videos/$id", [
                'headers' => [ 'Authorization' => $api_key ],
                'timeout' => 12,
            ] );

            if ( is_wp_error( $resp ) ) {
                return new WP_Error( 'pexels_error', $resp->get_error_message(), [ 'status' => 502 ] );
            }

            $code = wp_remote_retrieve_response_code( $resp );
            $body = wp_remote_retrieve_body( $resp );
            if ( $code !== 200 || empty( $body ) ) {
                return new WP_Error( 'pexels_bad_response', 'Upstream error', [ 'status' => 502 ] );
            }

            $json = json_decode( $body, true );
            if ( ! is_array( $json ) ) {
                return new WP_Error( 'pexels_json', 'Invalid JSON', [ 'status' => 502 ] );
            }

            // Cache for 12h
            set_transient( $cache_key, $json, 12 * HOUR_IN_SECONDS );
            return rest_ensure_response( $json );
        }
    ] );
});
