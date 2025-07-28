<?php
/**
 * Plugin Name: Invite JWT Gatekeeper (MU)
 * Description: Issues 12‑hour invite tokens via /invite/v1/token.
 */

defined( 'ABSPATH' ) || exit;

// 1) Load Composer’s autoloader (adjust path if vendor/ is elsewhere)
require_once ABSPATH . 'vendor/autoload.php';

use Firebase\JWT\JWT;

/**
 * A tiny MU‑plugin to issue short‑lived JWT invite tokens.
 */
class InviteJWT_Gatekeeper {
  /**
   * Hook into rest_api_init to register our route.
   */
  public static function init() {
    add_action( 'rest_api_init', [ __CLASS__, 'register_routes' ] );
  }

  /**
   * Register POST /invite/v1/token
   */
  public static function register_routes() {
    register_rest_route( 'invite/v1', '/token', [
      'methods'             => 'POST',
      'callback'            => [ __CLASS__, 'issue_token' ],
      'permission_callback' => [ __CLASS__, 'can_issue' ],
    ] );
  }

  /**
   * Only users who can create users (admins) may request a token.
   */
  public static function can_issue( WP_REST_Request $req ) {
    return current_user_can( 'create_users' );
  }

  /**
   * Generate and return a JWT valid for 12 hours.
   */
  public static function issue_token( WP_REST_Request $req ) {
    // Use a custom constant if defined, otherwise fallback to WP salt:
    $secret = defined( 'INVITE_JWT_SECRET' )
            ? INVITE_JWT_SECRET
            : wp_salt( 'invite_jwt_secret' );

    $now = time();
    $exp = $now + 12 * HOUR_IN_SECONDS;
    $jti = wp_generate_uuid4();

    $payload = [
      'iss' => home_url(),
      'iat' => $now,
      'exp' => $exp,
      'jti' => $jti,
    ];

    // HS256 encode
    $token = JWT::encode( $payload, $secret, 'HS256' );

    return rest_ensure_response( [
      'token'     => $token,
      'expiresIn' => 12 * 3600,
      'issuedAt'  => date( DATE_ISO8601, $now ),
    ] );
  }
}

// Bootstrap it:
InviteJWT_Gatekeeper::init();

