<?php
/**
 * MU-Plugin: force WP auth cookies to SameSite=None for cross-site REST calls.
 */

if ( PHP_VERSION_ID < 70300 ) {
    // PHP < 7.3 doesn’t support the options array syntax,
    // so you’d need to backport or skip. Most hosts are ≥7.3.
    return;
}

// 1) After WP sets the auth cookie, resend it with SameSite=None
add_action( 'set_auth_cookie', function( $auth_cookie, $expire, $expiration, $user_id, $scheme ) {
    setcookie(
        AUTH_COOKIE,
        $auth_cookie,
        [
            'expires'  => $expire,
            'path'     => SITECOOKIEPATH,
            'domain'   => COOKIE_DOMAIN,
            'secure'   => is_ssl(),
            'httponly' => true,
            'samesite' => 'None',
        ]
    );
}, 10, 5 );

// 2) Likewise for the “logged_in” cookie
add_action( 'set_logged_in_cookie', function( $login_cookie, $expire, $expiration, $user_id, $scheme ) {
    setcookie(
        LOGGED_IN_COOKIE,
        $login_cookie,
        [
            'expires'  => $expire,
            'path'     => COOKIEPATH,
            'domain'   => COOKIE_DOMAIN,
            'secure'   => is_ssl(),
            'httponly' => false,
            'samesite' => 'None',
        ]
    );
}, 10, 5 );

