# scripts/reset-wp.sh
#!/usr/bin/env bash
set -euo pipefail

WP_PATH="${WP_PATH:-wordpress}"
# Prefer WP_BASE_URL; fallback to WP_URL (back-compat) then default
WP_URL="${WP_BASE_URL:-${WP_URL:-https://wp.lan}}"
ADMIN_USER="${ADMIN_USER:-admin}"
ADMIN_PASS="${ADMIN_PASS:-a}"
ADMIN_EMAIL="${ADMIN_EMAIL:-admin@example.com}"
APP_NAME="gha"

cd "$WP_PATH"

wp db reset --yes 1>&2 || true

wp core install \
  --url="$WP_URL" \
  --title="wptest" \
  --admin_user="$ADMIN_USER" \
  --admin_password="$ADMIN_PASS" \
  --admin_email="$ADMIN_EMAIL" \
  --skip-email 1>&2

export WP_APP_PASSWORD="$(wp user application-password create "$ADMIN_USER" "$APP_NAME" --porcelain)"

if [ -n "${GITHUB_OUTPUT:-}" ]; then
  echo "WP_APP_PASSWORD=$WP_APP_PASSWORD" >> "$GITHUB_OUTPUT"   # <-- keep this key
fi

# ~/.bashrc update unchanged (optional in CI)
BASHRC="$HOME/.bashrc"
if [ -f "$BASHRC" ]; then
  if grep -q '^export WP_APP_PASSWORD=' "$BASHRC"; then
    sed -i.bak "s|^export WP_APP_PASSWORD=.*|export WP_APP_PASSWORD=\"$WP_APP_PASSWORD\"|" "$BASHRC" && rm -f "$BASHRC.bak"
  else
    echo "export WP_APP_PASSWORD=\"$WP_APP_PASSWORD\"" >> "$BASHRC"
  fi
fi

