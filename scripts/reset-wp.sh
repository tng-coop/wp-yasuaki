#!/usr/bin/env bash
set -euo pipefail

# ---- Defaults (override in CI via env) ----
WP_PATH="${WP_PATH:-wordpress}"
WP_URL="${WP_URL:-https://wp.lan}"
ADMIN_USER="${ADMIN_USER:-admin}"
ADMIN_PASS="${ADMIN_PASS:-a}"
ADMIN_EMAIL="${ADMIN_EMAIL:-admin@example.com}"

cd "$WP_PATH"

# Reset DB (ignore error if it's fresh)
wp db reset --yes || true

# Fresh install
wp core install \
  --url="$WP_URL" \
  --title="wptest" \
  --admin_user="$ADMIN_USER" \
  --admin_password="$ADMIN_PASS" \
  --admin_email="$ADMIN_EMAIL"

# If WP_APP_PASSWORD is already defined, just reuse it
if [ -n "${WP_APP_PASSWORD:-}" ]; then
  echo "Using existing WP_APP_PASSWORD"
else
  # Otherwise, create a new one and print it
  ADMIN_APP=$(wp user application-password create "$ADMIN_USER" gha --porcelain)
  echo "$ADMIN_APP"
fi
