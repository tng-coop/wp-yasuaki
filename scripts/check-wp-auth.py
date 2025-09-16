#!/usr/bin/env python3
import os
import sys
import base64
import requests
from requests.auth import HTTPBasicAuth

def die(msg, code=1):
    print(msg)
    sys.exit(code)

def main():
    wp_user = os.getenv("WP_USERNAME")
    wp_app_pass_raw = os.getenv("WP_APP_PASSWORD")
    wp_base_url = os.getenv("WP_BASE_URL")
    insecure = os.getenv("WP_INSECURE") == "1"

    if not (wp_user and wp_app_pass_raw and wp_base_url):
        die("❌ Missing env vars. Need WP_USERNAME, WP_APP_PASSWORD, WP_BASE_URL")

    # App passwords are often displayed with spaces — strip them for safety.
    wp_app_pass = wp_app_pass_raw.replace(" ", "")

    url = f"{wp_base_url.rstrip('/')}/wp-json/wp/v2/users/me"

    try:
        # First: HEAD to see if server advertises Basic auth and how it redirects.
        head = requests.head(url, allow_redirects=True, verify=not insecure, timeout=20)
    except requests.exceptions.SSLError as e:
        die(f"❌ TLS/SSL error. If this is a dev box with self-signed certs, set WP_INSECURE=1.\n{e}")
    except Exception as e:
        die(f"❌ Network error reaching {url}\n{e}")

    final_url = head.url
    www_auth = head.headers.get("WWW-Authenticate", "")
    print(f"ℹ️ Final URL after redirects: {final_url}")
    if www_auth:
        print(f"ℹ️ Server WWW-Authenticate header: {www_auth}")
    else:
        print("ℹ️ No WWW-Authenticate header on HEAD. That can be normal, "
              "but if auth fails later, your proxy/webserver might be stripping Authorization.")

    # Now attempt authenticated GET
    try:
        resp = requests.get(final_url, auth=HTTPBasicAuth(wp_user, wp_app_pass),
                            verify=not insecure, timeout=30)
    except requests.exceptions.SSLError as e:
        die(f"❌ TLS/SSL error on GET. Consider WP_INSECURE=1 for local dev.\n{e}")
    except Exception as e:
        die(f"❌ Network error on GET to {final_url}\n{e}")

    print(f"ℹ️ Status: {resp.status_code}")
    # Helpful hint: show auth header shape we sent (not the password)
    token_preview = base64.b64encode(f"{wp_user}:{'•'*8}".encode()).decode()
    print(f"ℹ️ Sent Authorization: Basic {token_preview} (password hidden)")

    if resp.status_code == 200:
        print("✅ Authentication successful!")
        try:
            data = resp.json()
            # Print a minimal summary
            uid = data.get("id")
            uname = data.get("slug") or data.get("name") or data.get("username")
            caps = data.get("capabilities") or {}
            is_admin = any(caps.get(k) for k in ("manage_options", "activate_plugins", "create_users"))
            print(f"User ID: {uid}, Username: {uname}, Admin-like: {is_admin}")
        except Exception:
            print(resp.text[:500])
        sys.exit(0)

    # Diagnostics for common 401 causes
    body = resp.text
    print(body[:1000])  # show a chunk

    if resp.status_code == 401:
        # Check for server refusing/stripping Authorization
        if "rest_not_logged_in" in body:
            print("❌ WordPress says you're not logged in — it likely never received Basic auth.")
            print("🔧 Check these:")
            print("  1) Is WP_BASE_URL correct and HTTPS? Avoid redirect from http→https or to a different host.")
            print("  2) Did you copy the Application Password without spaces? (This script stripped them.)")
            print("  3) Ensure the password is active (Users → Your Profile → Application Passwords).")
            print("  4) Your webserver/proxy must forward the Authorization header.")
            print("     - Apache (FastCGI): add one of:")
            print("       SetEnvIf Authorization \"(.*)\" HTTP_AUTHORIZATION=$1")
            print("       or in .htaccess:")
            print("         RewriteEngine On")
            print("         RewriteCond %%{HTTP:Authorization} .")
            print("         RewriteRule .* - [E=HTTP_AUTHORIZATION:%%{HTTP:Authorization}]")
            print("     - nginx: ensure `proxy_set_header Authorization $http_authorization;`")
            print("  5) Some hosts require HTTPS for Application Passwords.")
        else:
            print("❌ 401 Unauthorized. If not a stripping issue, the app password/user may be wrong or revoked.")
        sys.exit(2)

    if resp.status_code in (301, 302, 307, 308):
        print("❌ Redirect during auth. Use the *final* origin as WP_BASE_URL.")
        sys.exit(3)

    if resp.status_code == 403:
        print("❌ 403 Forbidden — user may lack permissions or a security plugin is blocking Basic auth.")
        sys.exit(4)

    print("❌ Unexpected status.")
    sys.exit(5)

if __name__ == "__main__":
    main()
