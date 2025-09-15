#!/usr/bin/env bash
set -euo pipefail

WP_PATH="${WP_PATH:-wordpress}"
WP_URL="${WP_URL:-https://wp.lan}"
ADMIN_USER="${ADMIN_USER:-admin}"
ADMIN_PASS="${ADMIN_PASS:-a}"
ADMIN_EMAIL="${ADMIN_EMAIL:-admin@example.com}"

cd "$WP_PATH"

# Reset DB (stderr shows "Success:" locally, but won't pollute stdout)
wp db reset --yes 1>&2 || true

# Install core (again, stderr shows status)
wp core install \
  --url="$WP_URL" \
  --title="wptest" \
  --admin_user="$ADMIN_USER" \
  --admin_password="$ADMIN_PASS" \
  --admin_email="$ADMIN_EMAIL" \
  --skip-email 1>&2

# Decide whether to reuse existing token
if [ -n "${WP_APP_PASSWORD:-}" ]; then
  # Print ONLY the token to stdout
  printf '%s\n' "$WP_APP_PASSWORD"
else
  wp user application-password create "$ADMIN_USER" gha --porcelain
fi
