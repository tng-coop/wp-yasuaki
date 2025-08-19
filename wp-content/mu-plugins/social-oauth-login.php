<?php
/**
 * Plugin Name: Social OAuth Login
 * Description: Enables Google and GitHub OAuth login & registration on WP login screens,
 *              styled with Bootstrap via esm.sh and custom brand colors, with HEREDOC
 *              separators. Includes enhanced error handling and account linking.
 * Version:     1.5.9
 * Author:      Your Name
 */

defined('ABSPATH') || exit;

// -----------------------------------------------------------------------------
// CONFIG: Fixed redirect URI (must match OAuth provider settings)
// -----------------------------------------------------------------------------
if ( ! defined('SOL_OAUTH_REDIRECT_URI') ) {
    define('SOL_OAUTH_REDIRECT_URI', site_url('wp-login.php'));
}

// -----------------------------------------------------------------------------
// HELPERS (lazy, only used during login/register)
// -----------------------------------------------------------------------------
function sol_is_login_request(): bool {
    if (defined('WP_CLI') && WP_CLI) return false;
    if (wp_doing_cron() || wp_doing_ajax()) return false;
    if (defined('REST_REQUEST') && REST_REQUEST) return false;
    $script = $_SERVER['SCRIPT_NAME'] ?? '';
    return (substr($script, -14) === '/wp-login.php') || (basename($script) === 'wp-login.php');
}

function sol_env($key) {
    // Read from getenv/$_SERVER/$_ENV or a constant of the same name.
    $v = getenv($key);
    if ($v === false && isset($_SERVER[$key])) $v = $_SERVER[$key];
    if ($v === false && isset($_ENV[$key]))    $v = $_ENV[$key];
    if ($v === false && defined($key))         $v = constant($key);
    return $v ?: false;
}

function sol_get_creds(string $provider): array {
    $p = strtoupper($provider);
    return [
        'id'     => sol_env("{$p}_CLIENT_ID"),
        'secret' => sol_env("{$p}_CLIENT_SECRET"),
    ];
}

function sol_require_creds_or_redirect(string $provider) {
    $c = sol_get_creds($provider);
    if ($c['id'] && $c['secret']) return $c;

    // Log only on actual login requests to avoid spam
    if (sol_is_login_request()) {
        error_log('Social OAuth Login: Missing OAuth config for ' . $provider . ' ' . json_encode([
            strtolower("{$provider}_client_id")     => (bool) $c['id'],
            strtolower("{$provider}_client_secret") => (bool) $c['secret'],
        ]));
    }

    // Bounce back to login with a friendly error
    $login_url = add_query_arg(
        ['auth_error' => 'missing_config', 'auth_provider' => rawurlencode($provider)],
        wp_login_url()
    );
    wp_safe_redirect($login_url);
    exit;
}

// -----------------------------------------------------------------------------
// ENQUEUE STYLES (login only) with Bootstrap from esm.sh
// -----------------------------------------------------------------------------
add_action('login_enqueue_scripts', function () {
    wp_enqueue_style('sol-bootstrap-css', 'https://esm.sh/bootstrap@5.3.0/dist/css/bootstrap.min.css', [], null);
}, 20);

// -----------------------------------------------------------------------------
// RENDER LOGIN BUTTONS + SEPARATOR
// -----------------------------------------------------------------------------
add_filter('login_message', function ($message) {
    $action = $_GET['action'] ?? 'login';
    if (!in_array($action, ['login', 'register'], true)) return $message;

    $gurl = sol_get_oauth_url('google');
    $hurl = sol_get_oauth_url('github');

    $html  = "<p><a class='btn btn-primary w-100 mb-2' href='" . esc_url($gurl) . "'>Continue with Google</a></p>";
    $html .= "<p><a class='btn btn-dark w-100 mb-2'   href='" . esc_url($hurl) . "'>Continue with GitHub</a></p>";
    $html .= "<div class='d-flex align-items-center my-3'><hr class='flex-grow-1'/><span class='mx-2 text-muted'>OR</span><hr class='flex-grow-1'/></div>";
    return $html . $message;
});

// -----------------------------------------------------------------------------
// BUILD OAUTH URL (only called on login screen)
// -----------------------------------------------------------------------------
function sol_get_oauth_url($provider) {
    $creds = sol_get_creds($provider);
    if (!$creds['id']) {
        // No hard redirect here; just show a link that returns with a message
        return add_query_arg(
            ['auth_error' => 'missing_config', 'auth_provider' => rawurlencode($provider)],
            wp_login_url()
        );
    }

    if ($provider === 'google') {
        $base   = 'https://accounts.google.com/o/oauth2/v2/auth';
        $params = [
            'client_id'     => $creds['id'],
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
            'client_id'    => $creds['id'],
            'redirect_uri' => SOL_OAUTH_REDIRECT_URI,
            'scope'        => 'read:user user:email',
            'state'        => 'github',
        ];
    }

    $url = add_query_arg($params, $base);
    // Safe to log (contains client_id but not secret)
    error_log(sprintf('Social OAuth Login: Generated %s URL', $provider));
    return $url;
}

// -----------------------------------------------------------------------------
// LOGIN LOGGING: record who attempted to log in and status
// -----------------------------------------------------------------------------
function sol_log_login_attempt($provider, $email, $status, $extra = '') {
    $provider = sanitize_text_field($provider);
    $email    = sanitize_email($email ?: '');
    $status   = strtoupper(sanitize_text_field($status));
    $extra    = is_scalar($extra) ? sanitize_text_field((string) $extra) : '';

    $log_entry = sprintf(
        '[%s] Provider:%s | Email:%s | Status:%s | Extra:%s',
        gmdate('Y-m-d H:i:s'),
        $provider ?: 'unknown',
        $email ?: 'N/A',
        $status,
        $extra
    );
    error_log('Social OAuth Login Attempt: ' . $log_entry);
    // Optional dedicated file:
    // @file_put_contents(WP_CONTENT_DIR . '/sol-login.log', $log_entry . PHP_EOL, FILE_APPEND | LOCK_EX);
}

// -----------------------------------------------------------------------------
// HANDLE OAUTH CALLBACK (runs during wp-login.php only)
// -----------------------------------------------------------------------------
add_action('login_init', 'sol_handle_callback');
function sol_handle_callback() {
    if (empty($_GET['state']) || empty($_GET['code'])) return;

    $provider = sanitize_text_field(wp_unslash($_GET['state']));
    $code     = sanitize_text_field(wp_unslash($_GET['code']));

    // Ensure credentials exist (will redirect with error if not)
    $creds = sol_require_creds_or_redirect($provider);

    $token = sol_exchange_code_for_token($provider, $code, $creds);
    if (!$token) {
        sol_log_login_attempt($provider, '', 'failure', 'no_token');
        error_log('Social OAuth Login: No token for ' . $provider);
        return;
    }

    $profile = sol_fetch_user_profile($provider, $token);
    error_log('Social OAuth Login: Retrieved profile for ' . $provider);

    if ($provider === 'google' && isset($profile->sub)) {
        $profile->id = $profile->sub;
    }
    if (empty($profile->id)) {
        sol_log_login_attempt($provider, $profile->email ?? '', 'failure', 'missing_profile_id');
        error_log('Social OAuth Login: Missing profile ID for ' . $provider);
        return;
    }

    $uid = sol_find_or_create_wp_user($provider, $profile);
    if (is_wp_error($uid)) {
        $code_err = $uid->get_error_code(); // e.g. 'email_not_approved'
        $email    = sanitize_email($profile->email ?? '');

        sol_log_login_attempt($provider, $email, 'failure', $code_err);

        $login_url = add_query_arg(
            ['auth_error' => $code_err, 'auth_email' => rawurlencode($email)],
            wp_login_url()
        );
        wp_safe_redirect($login_url);
        exit;
    }

    // Success
    sol_log_login_attempt($provider, $profile->email ?? '', 'success', 'user_id=' . $uid);
    wp_set_current_user($uid);
    wp_set_auth_cookie($uid);
    wp_redirect(home_url());
    exit;
}

// -----------------------------------------------------------------------------
// EXCHANGE CODE FOR TOKEN (no secrets in logs)
// -----------------------------------------------------------------------------
function sol_exchange_code_for_token($provider, $code, array $creds) {
    $endpoint = ($provider === 'google')
        ? 'https://oauth2.googleapis.com/token'
        : 'https://github.com/login/oauth/access_token';

    $fields = [
        'code'          => $code,
        'client_id'     => $creds['id'],
        'client_secret' => $creds['secret'],
    ];
    if ($provider === 'google') {
        $fields['redirect_uri'] = SOL_OAUTH_REDIRECT_URI;
        $fields['grant_type']   = 'authorization_code';
    }

    // Log without secrets
    $log_fields = $fields;
    unset($log_fields['client_secret']);
    error_log('Social OAuth Login: Token request for ' . $provider . ': ' . json_encode($log_fields));

    $resp = wp_remote_post($endpoint, [
        'body'    => $fields,
        'headers' => ['Accept' => 'application/json'],
        'timeout' => 15,
    ]);
    if (is_wp_error($resp)) {
        error_log('Social OAuth Login: HTTP error: ' . $resp->get_error_message());
        return null;
    }
    $code_http = wp_remote_retrieve_response_code($resp);
    $body      = wp_remote_retrieve_body($resp);
    error_log('Social OAuth Login: Token response ' . $provider . ' HTTP ' . $code_http);

    $data = json_decode($body, true);
    return $data['access_token'] ?? null;
}

// -----------------------------------------------------------------------------
// FETCH USER PROFILE
// -----------------------------------------------------------------------------
function sol_fetch_user_profile($provider, $token) {
    if ($provider === 'github') {
        $resp = wp_remote_get('https://api.github.com/user', [
            'headers' => ['Authorization' => 'token ' . $token, 'User-Agent' => 'WP-SOL'],
            'timeout' => 15,
        ]);
        $profile = json_decode(wp_remote_retrieve_body($resp));
        if (empty($profile->email)) {
            $resp2 = wp_remote_get('https://api.github.com/user/emails', [
                'headers' => ['Authorization' => 'token ' . $token, 'User-Agent' => 'WP-SOL'],
                'timeout' => 15,
            ]);
            $emails = json_decode(wp_remote_retrieve_body($resp2), true);
            if (is_array($emails)) {
                foreach ($emails as $e) {
                    if (!empty($e['primary']) && !empty($e['verified'])) {
                        $profile->email = $e['email'];
                        break;
                    }
                }
            }
        }
        return $profile;
    }

    $resp = wp_remote_get('https://www.googleapis.com/oauth2/v3/userinfo', [
        'headers' => ['Authorization' => 'Bearer ' . $token],
        'timeout' => 15,
    ]);
    return json_decode(wp_remote_retrieve_body($resp));
}

// -----------------------------------------------------------------------------
// FIND, LINK, OR CREATE WP USER (ALLOW DUPLICATE EMAILS)
// -----------------------------------------------------------------------------
function sol_find_or_create_wp_user($provider, $profile) {
    $profile_id    = sanitize_text_field($profile->id);
    $provider_login = "{$provider}_" . $profile_id;

    if ($user = get_user_by('login', $provider_login)) {
        return $user->ID;
    }

    $email = sanitize_email($profile->email ?? '');
    if (!empty($email) && function_exists('ael_is_email_approved') && !ael_is_email_approved($email)) {
        return new WP_Error('email_not_approved', 'This email address is not approved.');
    }

    $password = wp_generate_password();
    $uid      = wp_create_user($provider_login, $password, $email);
    if (is_wp_error($uid)) return $uid;

    if (!empty($profile->name)) {
        list($first, $last) = array_pad(explode(' ', sanitize_text_field($profile->name), 2), 2, '');
        wp_update_user([
            'ID'           => $uid,
            'first_name'   => $first,
            'last_name'    => $last,
            'display_name' => trim("$first $last"),
        ]);
    }
    if (!empty($profile->avatar_url)) {
        update_user_meta($uid, 'profile_picture', esc_url_raw($profile->avatar_url));
    }

    return $uid;
}

// -----------------------------------------------------------------------------
// SURFACE FRIENDLY ERRORS ON LOGIN SCREEN
// -----------------------------------------------------------------------------
add_filter('wp_login_errors', function (WP_Error $errors) {
    if (isset($_GET['auth_error']) && 'email_not_approved' === $_GET['auth_error']) {
        $email = isset($_GET['auth_email']) ? sanitize_email(wp_unslash($_GET['auth_email'])) : '';
        $msg   = $email
            ? sprintf(
                /* translators: %s = attempted email address */
                __('The email address <strong>%s</strong> is not approved for registration.', 'social-oauth-login'),
                esc_html($email)
            )
            : __('This email address is not approved for registration.', 'social-oauth-login');
        $errors->add('email_not_approved', wp_kses_post($msg));
    }

    if (isset($_GET['auth_error']) && 'missing_config' === $_GET['auth_error']) {
        $provider = isset($_GET['auth_provider']) ? esc_html(sanitize_text_field(wp_unslash($_GET['auth_provider']))) : '';
        $provider_label = $provider ? ucfirst($provider) : __('OAuth', 'social-oauth-login');
        $errors->add(
            'missing_config',
            wp_kses_post(sprintf(
                /* translators: %s = provider name */
                __('<strong>%s login isn’t available.</strong> The site’s OAuth credentials are not configured.', 'social-oauth-login'),
                $provider_label
            ))
        );
    }

    return $errors;
}, 10, 1);
