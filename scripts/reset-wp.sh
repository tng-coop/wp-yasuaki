# scripts/reset-wp.sh
#!/usr/bin/env bash
set -euo pipefail

WP_PATH="${WP_PATH:-wordpress}"
# Prefer WP_BASE_URL; fallback to WP_URL (back-compat) then default
WP_URL="${WP_BASE_URL:-${WP_URL:-https://example.com}}"
ADMIN_USER="${ADMIN_USER:-admin}"
ADMIN_PASS="${ADMIN_PASS:-a}"
ADMIN_EMAIL="${ADMIN_EMAIL:-admin@example.com}"
APP_NAME="gha"

# Contributor user (override via env if desired)
CONTRIB_USER="${CONTRIB_USER:-contributor}"
CONTRIB_EMAIL="${CONTRIB_EMAIL:-contributor@example.com}"
CONTRIB_PASS="${CONTRIB_PASS:-a}"

# Second contributor user (override via env if desired)
CONTRIB2_USER="${CONTRIB2_USER:-contributor2}"
CONTRIB2_EMAIL="${CONTRIB2_EMAIL:-contributor2@example.com}"
CONTRIB2_PASS="${CONTRIB2_PASS:-a}"

# Editor user (override via env if desired)
EDITOR_USER="${EDITOR_USER:-editor}"
EDITOR_EMAIL="${EDITOR_EMAIL:-editor@example.com}"
EDITOR_PASS="${EDITOR_PASS:-a}"

cd "$WP_PATH"

wp db reset --yes 1>&2 || true

wp core install \
  --url="$WP_URL" \
  --title="wptest" \
  --admin_user="$ADMIN_USER" \
  --admin_password="$ADMIN_PASS" \
  --admin_email="$ADMIN_EMAIL" \
  --skip-email 1>&2

# Create contributor-role user
wp user create "$CONTRIB_USER" "$CONTRIB_EMAIL" \
  --role=contributor \
  --user_pass="$CONTRIB_PASS" 1>&2

# Create second contributor-role user
wp user create "$CONTRIB2_USER" "$CONTRIB2_EMAIL" \
  --role=contributor \
  --user_pass="$CONTRIB2_PASS" 1>&2

# Create editor-role user
wp user create "$EDITOR_USER" "$EDITOR_EMAIL" \
  --role=editor \
  --user_pass="$EDITOR_PASS" 1>&2

wp cap add contributor upload_files edit_others_posts delete_others_posts 1>&2

# Application passwords (porcelain = raw token only)
export WP_APP_PASSWORD="$(wp user application-password create "$ADMIN_USER" "$APP_NAME" --porcelain)"
export WP_APP_CONTRIBUTOR="$(wp user application-password create "$CONTRIB_USER" "$APP_NAME" --porcelain)"
export WP_APP_CONTRIBUTOR2="$(wp user application-password create "$CONTRIB2_USER" "$APP_NAME" --porcelain)"
export WP_APP_EDITOR="$(wp user application-password create "$EDITOR_USER" "$APP_NAME" --porcelain)"

if [ -n "${GITHUB_OUTPUT:-}" ]; then
  echo "WP_APP_PASSWORD=$WP_APP_PASSWORD" >> "$GITHUB_OUTPUT"   # <-- keep this key
  echo "WP_APP_CONTRIBUTOR=$WP_APP_CONTRIBUTOR" >> "$GITHUB_OUTPUT"
  echo "WP_APP_CONTRIBUTOR2=$WP_APP_CONTRIBUTOR2" >> "$GITHUB_OUTPUT"
  echo "WP_APP_EDITOR=$WP_APP_EDITOR" >> "$GITHUB_OUTPUT"
fi

# ~/.bashrc update (optional in CI)
BASHRC="$HOME/.bashrc"
if [ -f "$BASHRC" ]; then
  if grep -q '^export WP_APP_PASSWORD=' "$BASHRC"; then
    sed -i.bak "s|^export WP_APP_PASSWORD=.*|export WP_APP_PASSWORD=\"$WP_APP_PASSWORD\"|" "$BASHRC" && rm -f "$BASHRC.bak"
  else
    echo "export WP_APP_PASSWORD=\"$WP_APP_PASSWORD\"" >> "$BASHRC"
  fi
  if grep -q '^export WP_APP_CONTRIBUTOR=' "$BASHRC"; then
    sed -i.bak "s|^export WP_APP_CONTRIBUTOR=.*|export WP_APP_CONTRIBUTOR=\"$WP_APP_CONTRIBUTOR\"|" "$BASHRC" && rm -f "$BASHRC.bak"
  else
    echo "export WP_APP_CONTRIBUTOR=\"$WP_APP_CONTRIBUTOR\"" >> "$BASHRC"
  fi
  if grep -q '^export WP_APP_CONTRIBUTOR2=' "$BASHRC"; then
    sed -i.bak "s|^export WP_APP_CONTRIBUTOR2=.*|export WP_APP_CONTRIBUTOR2=\"$WP_APP_CONTRIBUTOR2\"|" "$BASHRC" && rm -f "$BASHRC.bak"
  else
    echo "export WP_APP_CONTRIBUTOR2=\"$WP_APP_CONTRIBUTOR2\"" >> "$BASHRC"
  fi
  if grep -q '^export WP_APP_EDITOR=' "$BASHRC"; then
    sed -i.bak "s|^export WP_APP_EDITOR=.*|export WP_APP_EDITOR=\"$WP_APP_EDITOR\"|" "$BASHRC" && rm -f "$BASHRC.bak"
  else
    echo "export WP_APP_EDITOR=\"$WP_APP_EDITOR\"" >> "$BASHRC"
  fi
fi

# --- Front page + Posts page (idempotent) ---
lookup_page_id() {
  # find page ID by slug without requiring jq
  local slug="$1"
  wp post list --post_type=page --fields=ID,post_name --format=csv \
    | awk -F',' -v s="$slug" 'NR>1 && $2==s {print $1; exit}'
}

HOME_ID="$(lookup_page_id home || true)"
if [ -z "${HOME_ID:-}" ]; then
  HOME_ID="$(wp post create --post_type=page --post_title='Home' --post_name='home' --post_status=publish --porcelain)"
fi

BLOG_ID="$(lookup_page_id blog || true)"
if [ -z "${BLOG_ID:-}" ]; then
  BLOG_ID="$(wp post create --post_type=page --post_title='Blog' --post_name='blog' --post_status=publish --porcelain)"
fi

# Reading settings
wp option update show_on_front page 1>&2
wp option update page_on_front "$HOME_ID" 1>&2
wp option update page_for_posts "$BLOG_ID" 1>&2
echo "[ok] Front page → #$HOME_ID (/home), Posts page → #$BLOG_ID (/blog)" 1>&2

# Export/persist envs expected by scripts/tt25_nav_fix.py
export WP_BASE_URL="$WP_URL"
export WP_USERNAME="$ADMIN_USER"

if [ -f "$BASHRC" ]; then
  if grep -q '^export WP_BASE_URL=' "$BASHRC"; then
    sed -i.bak "s|^export WP_BASE_URL=.*|export WP_BASE_URL=\"$WP_URL\"|" "$BASHRC"
  else
    echo "export WP_BASE_URL=\"$WP_URL\"" >> "$BASHRC"
  fi
  if grep -q '^export WP_USERNAME=' "$BASHRC"; then
    sed -i.bak "s|^export WP_USERNAME=.*|export WP_USERNAME=\"$ADMIN_USER\"|" "$BASHRC"
  else
    echo "export WP_USERNAME=\"$ADMIN_USER\"" >> "$BASHRC"
  fi
  rm -f "$BASHRC.bak"
fi

