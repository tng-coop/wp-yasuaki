<?php
namespace SOL;

use Firebase\JWT\JWT;
use Firebase\JWT\Key;
use WP_REST_Request;
use WP_Error;

class InviteJWT {
  public static function register_rest_routes() {
    add_action('rest_api_init', function() {
      register_rest_route('invite/v1', '/token', [
        'methods'             => 'POST',
        'callback'            => [ __CLASS__, 'issue' ],
        'permission_callback' => [ __CLASS__, 'can_issue' ],
      ]);
    });
  }

  public static function can_issue(WP_REST_Request $req) {
    return current_user_can('create_users');
  }

  public static function issue(WP_REST_Request $req) {
    $secret = defined('INVITE_JWT_SECRET')
            ? INVITE_JWT_SECRET
            : wp_salt('invite_jwt_secret');

    $now = time();
    $exp = $now + 12 * HOUR_IN_SECONDS;
    $jti = wp_generate_uuid4();

    $payload = [
      'iss' => home_url(),
      'iat' => $now,
      'exp' => $exp,
      'jti' => $jti,
    ];

    $token = JWT::encode($payload, $secret, 'HS256');
    return rest_ensure_response([
      'token'     => $token,
      'expiresIn' => 12 * 3600,
      'issuedAt'  => date(DATE_ISO8601, $now),
    ]);
  }
}
