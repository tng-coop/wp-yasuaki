<?php
/**
 * Plugin Name: Invite JWT Gatekeeper (MU)
 * Description: Issues 12‑hour invite tokens via /invite/v1/token.
 */

defined( 'ABSPATH' ) || exit;

// 1) Load Composer’s autoloader (adjust path if vendor/ is elsewhere)
require_once ABSPATH . 'vendor/autoload.php';

// 2) Load your InviteJWT class
require_once __DIR__ . '/invite-jwt/src/InviteJWT.php';

// 3) Hook up the REST route
\SOL\InviteJWT::register_rest_routes();
