#!/usr/bin/env bash
# scripts/create-wp-users.sh
set -euo pipefail

# Path to your WP install (same default as reset-wp.sh)
WP_PATH="${WP_PATH:-wordpress}"

# Default password for all non-admin accounts
WP_PASS="${WP_PASS:-a}"

# Editor account
EDITOR_USER="${EDITOR_USER:-editor}"
EDITOR_EMAIL="${EDITOR_EMAIL:-editor@example.com}"

# Author account
AUTHOR_USER="${AUTHOR_USER:-author}"
AUTHOR_EMAIL="${AUTHOR_EMAIL:-author@example.com}"

cd "$WP_PATH"

create_or_update_user() {
  local username="$1"
  local email="$2"
  local role="$3"

  if wp user get "$username" >/dev/null 2>&1; then
    # Update password & email if the user already exists
    wp user update "$username" \
      --user_pass="$WP_PASS" \
      --user_email="$email" 1>&2

    # Ensure the intended role
    wp user set-role "$username" "$role" 1>&2
    echo "Updated user '$username' with role '$role'."
  else
    # Create fresh user
    wp user create "$username" "$email" \
      --role="$role" \
      --user_pass="$WP_PASS" \
      --display_name="$username" 1>&2
    echo "Created user '$username' with role '$role'."
  fi
}

create_or_update_user "$EDITOR_USER" "$EDITOR_EMAIL" editor
create_or_update_user "$AUTHOR_USER" "$AUTHOR_EMAIL" author
