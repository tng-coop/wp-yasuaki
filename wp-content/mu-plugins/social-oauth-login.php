<?php
/**
 * Plugin Name: Social OAuth Login
 * Description: Enables Google and GitHub OAuth login & registration on WP login screens,
 *              styled with Bootstrap via esm.sh and custom brand colors, with HEREDOC
 *              separators. Includes enhanced error handling and account linking.
 * Version:     1.5.8
 * Author:      Your Name
 */

// Prevent direct access
defined('ABSPATH') || exit;

// -----------------------------------------------------------------------------
// UTILITY: REDIRECT TO LOGIN WITH ERROR MESSAGE
// -----------------------------------------------------------------------------
function sol_redirect_with_error($msg) {
    $url = add_query_arg('sol_err', rawurlencode($msg), wp_login_url());
    wp_safe_redirect($url);
    exit;
}

add_filter('login_errors', function($errors) {
    if (! empty($_GET['sol_err'])) {
        $err_msg = sanitize_text_field(wp_unslash($_GET['sol_err']));
        $errors .= '<div id="login_error" class="notice notice-error"><p>' . esc_html($err_msg) . '</p></div>';
    }
    return $errors;
});

// -----------------------------------------------------------------------------
// SHOW QUERY ERROR EVEN WHEN NO LOGIN ATTEMPT
// -----------------------------------------------------------------------------
add_filter('login_message', function($message) {
    if (! empty($_GET['sol_err'])) {
        $err_msg = sanitize_text_field(wp_unslash($_GET['sol_err']));
        $message = '<div id="login_error" class="notice notice-error"><p>' . esc_html($err_msg) . '</p></div>' . $message;
    }
    return $message;
}, 5);

// -----------------------------------------------------------------------------
// CONFIG: Fixed redirect URI (must match OAuth provider settings)
// -----------------------------------------------------------------------------
define('SOL_OAUTH_REDIRECT_URI', site_url('wp-login.php'));

// -----------------------------------------------------------------------------
// LOAD CREDENTIALS FROM ENVIRONMENT VARIABLES
// -----------------------------------------------------------------------------
$google_client_id     = getenv('GOOGLE_CLIENT_ID');
$google_client_secret = getenv('GOOGLE_CLIENT_SECRET');
$github_client_id     = getenv('GITHUB_CLIENT_ID');
$github_client_secret = getenv('GITHUB_CLIENT_SECRET');

if (!$google_client_id || !$google_client_secret || !$github_client_id || !$github_client_secret) {
    error_log('Social OAuth Login: Missing OAuth env vars ' .
        json_encode(compact('google_client_id','google_client_secret','github_client_id','github_client_secret'))
    );
}

// Define constants
!defined('SOL_GOOGLE_CLIENT_ID')     && define('SOL_GOOGLE_CLIENT_ID', $google_client_id);
!defined('SOL_GOOGLE_CLIENT_SECRET') && define('SOL_GOOGLE_CLIENT_SECRET', $google_client_secret);
!defined('SOL_GITHUB_CLIENT_ID')     && define('SOL_GITHUB_CLIENT_ID', $github_client_id);
!defined('SOL_GITHUB_CLIENT_SECRET') && define('SOL_GITHUB_CLIENT_SECRET', $github_client_secret);

// -----------------------------------------------------------------------------
// ENQUEUE STYLES (login only) with Bootstrap from esm.sh
// -----------------------------------------------------------------------------
add_action('login_enqueue_scripts', function() {
    wp_enqueue_style('sol-bootstrap-css', 'https://esm.sh/bootstrap@5.3.0/dist/css/bootstrap.min.css', [], null);
}, 20);

// -----------------------------------------------------------------------------
// RENDER LOGIN BUTTONS + SEPARATOR
// -----------------------------------------------------------------------------
add_filter('login_message', function($message) {
    $action = $_GET['action'] ?? 'login';
    if (! in_array($action, ['login','register'], true)) {
        return $message;
    }
    $gurl = sol_get_oauth_url('google');
    $hurl = sol_get_oauth_url('github');
    $html  = "<p><a class='btn btn-primary w-100 mb-2' href='" . esc_url($gurl) . "'>Continue with Google</a></p>";
    $html .= "<p><a class='btn btn-dark w-100 mb-2' href='"   . esc_url($hurl) . "'>Continue with GitHub</a></p>";
    $html .= "<div class='d-flex align-items-center my-3'><hr class='flex-grow-1'/><span class='mx-2 text-muted'>OR</span><hr class='flex-grow-1'/></div>";
    return $html . $message;
});

// -----------------------------------------------------------------------------
// BUILD OAUTH URL
// -----------------------------------------------------------------------------
function sol_get_oauth_url($provider) {
    if ($provider === 'google') {
        $base   = 'https://accounts.google.com/o/oauth2/v2/auth';
        $params = [
            'client_id'     => SOL_GOOGLE_CLIENT_ID,
            'redirect_uri'  => SOL_OAUTH_REDIRECT_URI,
            'response_type' => 'code',
            'scope'         => 'openid email profile',
            'state'         => 'google',
            'access_type'   => 'online',
            'prompt'        => 'consent',
        ];
    } else {
        $base   = 'https://github.com/login/oauth/authorize';
        $params = [
            'client_id'    => SOL_GITHUB_CLIENT_ID,
            'redirect_uri' => SOL_OAUTH_REDIRECT_URI,
            'scope'        => 'read:user user:email',
            'state'        => 'github',
        ];
    }
    $url = add_query_arg($params, $base);
    error_log(sprintf('Social OAuth Login: Generated %s URL: %s', $provider, $url));
    return $url;
}

// -----------------------------------------------------------------------------
// HANDLE OAUTH CALLBACK
// -----------------------------------------------------------------------------
add_action('login_init', 'sol_handle_callback');
function sol_handle_callback() {
    if (empty($_GET['state']) || empty($_GET['code'])) {
        return;
    }
    $provider = sanitize_text_field(wp_unslash($_GET['state']));
    $code     = sanitize_text_field(wp_unslash($_GET['code']));
    $token    = sol_exchange_code_for_token($provider, $code);
    if (! $token) {
        error_log('Social OAuth Login: No token for ' . $provider);
        sol_redirect_with_error(__('OAuth authentication failed.', 'sol'));
    }
    $profile = sol_fetch_user_profile($provider, $token);
    error_log(print_r($profile, true));
    if ($provider==='google' && isset($profile->sub)) {
        $profile->id = $profile->sub;
    }
    if (empty($profile->id)) {
        error_log('Social OAuth Login: Missing profile ID for ' . $provider);
        sol_redirect_with_error(__('Unable to retrieve profile from provider.', 'sol'));
    }
    $uid = sol_find_or_create_wp_user($provider, $profile);
    if (is_wp_error($uid)) {
        error_log('Social OAuth Login: User linking error: ' . $uid->get_error_message());
        sol_redirect_with_error($uid->get_error_message());
    }
    wp_set_current_user($uid);
    wp_set_auth_cookie($uid);
    wp_redirect(home_url());
    exit;
}

// -----------------------------------------------------------------------------
// EXCHANGE CODE FOR TOKEN
// -----------------------------------------------------------------------------
function sol_exchange_code_for_token($provider, $code) {
    $endpoint = $provider==='google'
        ? 'https://oauth2.googleapis.com/token'
        : 'https://github.com/login/oauth/access_token';
    $fields = [
        'code'          => $code,
        'client_id'     => constant('SOL_' . strtoupper($provider) . '_CLIENT_ID'),
        'client_secret' => constant('SOL_' . strtoupper($provider) . '_CLIENT_SECRET'),
    ];
    if ($provider==='google') {
        $fields['redirect_uri'] = SOL_OAUTH_REDIRECT_URI;
        $fields['grant_type']   = 'authorization_code';
    }
    error_log('Social OAuth Login: Token request for ' . $provider . ': ' . json_encode($fields));
    $resp = wp_remote_post($endpoint, ['body'=>$fields,'headers'=>['Accept'=>'application/json'],'timeout'=>15]);
    if (is_wp_error($resp)) {
        error_log('Social OAuth Login: HTTP error: ' . $resp->get_error_message());
        return null;
    }
    $body = wp_remote_retrieve_body($resp);
    error_log('Social OAuth Login: Token response ' . $provider . ' HTTP ' . wp_remote_retrieve_response_code($resp) . ': ' . $body);
    $data = json_decode($body,true);
    return $data['access_token'] ?? null;
}

// -----------------------------------------------------------------------------
// FETCH USER PROFILE
// -----------------------------------------------------------------------------
function sol_fetch_user_profile($provider, $token) {
    if ($provider==='github') {
        $resp = wp_remote_get('https://api.github.com/user', [
            'headers'=>[
                'Authorization'=>'token ' . $token,
                'User-Agent'=>'WP-SOL'
            ],
            'timeout'=>15,
        ]);
        $profile = json_decode(wp_remote_retrieve_body($resp));
        if (empty($profile->email)) {
            $resp2 = wp_remote_get('https://api.github.com/user/emails', [
                'headers'=>[
                    'Authorization'=>'token ' . $token,
                    'User-Agent'=>'WP-SOL'
                ],
                'timeout'=>15,
            ]);
            $emails = json_decode(wp_remote_retrieve_body($resp2),true);
            foreach ($emails as $e) {
                if (!empty($e['primary']) && !empty($e['verified'])) {
                    $profile->email = $e['email'];
                    break;
                }
            }
        }
        return $profile;
    }
    $resp = wp_remote_get('https://www.googleapis.com/oauth2/v3/userinfo', ['headers'=>['Authorization'=>'Bearer ' . $token],'timeout'=>15]);
    return json_decode(wp_remote_retrieve_body($resp));
}

// -----------------------------------------------------------------------------
// FIND, LINK, OR CREATE WP USER (ALLOW DUPLICATE EMAILS)
// -----------------------------------------------------------------------------
function sol_find_or_create_wp_user($provider, $profile) {
    $meta_key   = $provider . '_id';
    $profile_id = sanitize_text_field($profile->id);

    // 1) If a WP user is already linked by this provider ID, return it
    $linked = get_users([ 'meta_key' => $meta_key, 'meta_value' => $profile_id, 'fields' => 'ID' ]);
    if (!empty($linked)) {
        $uid = $linked[0];
        if (!empty($profile->name)) {
            list($first, $last) = array_pad(explode(' ', sanitize_text_field($profile->name), 2), 2, '');
            wp_update_user([ 'ID' => $uid, 'first_name' => $first, 'last_name' => $last, 'display_name' => trim("$first $last") ]);
        }
        if (!empty($profile->avatar_url)) {
            update_user_meta($uid, 'profile_picture', esc_url_raw($profile->avatar_url));
        }
        return $uid;
    }

    // 2) Create a new WP user only if the email is approved
    $email    = sanitize_email($profile->email ?? '');
    if (! empty($email) && function_exists('ael_is_email_approved') && ! ael_is_email_approved($email)) {
        return new WP_Error('email_not_approved', 'This email address is not approved.');
    }
    $username = sanitize_user($provider . '_' . $profile_id, true);
    $password = wp_generate_password();
    $uid      = wp_create_user($username, $password, $email);
    if (is_wp_error($uid)) {
        return $uid;
    }

    // 3) Link provider ID and set meta
    update_user_meta($uid, $meta_key, $profile_id);
    if (!empty($profile->name)) {
        list($first, $last) = array_pad(explode(' ', sanitize_text_field($profile->name), 2), 2, '');
        wp_update_user([ 'ID' => $uid, 'first_name' => $first, 'last_name' => $last, 'display_name' => trim("$first $last") ]);
    }
    if (!empty($profile->avatar_url)) {
        update_user_meta($uid, 'profile_picture', esc_url_raw($profile->avatar_url));
    }

    return $uid;
}

