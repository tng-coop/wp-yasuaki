#!/usr/bin/env bash
# wp-yasuaki/scripts/create-wp-symlinks.sh
# Create symlinks for WordPress *files* (wp-*.php) at repo root -> wp/wp-*.php

set -euo pipefail

usage() {
  cat <<'EOF'
Create symlinks for WordPress core files that start with "wp-" at the repo root.

Usage:
  create-wp-symlinks.sh [--force] [--dry-run]

Options:
  --force    Overwrite existing regular files at the destination.
  --dry-run  Show what would happen without making changes.
EOF
}

FORCE=0
DRYRUN=0
for arg in "$@"; do
  case "$arg" in
    --force)   FORCE=1 ;;
    --dry-run) DRYRUN=1 ;;
    -h|--help) usage; exit 0 ;;
    *) echo "Unknown option: $arg" >&2; usage; exit 1 ;;
  esac
done

# Resolve repo root as the parent of this script's dir
SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd -- "$SCRIPT_DIR/.." && pwd)"
cd "$REPO_ROOT"

# Ensure wp/ exists
if [[ ! -d "wp" ]]; then
  echo "Error: 'wp' directory not found at $REPO_ROOT/wp" >&2
  exit 1
fi

# Only wp-* files (no dirs like wp-admin/wp-includes/wp-content, and no xmlrpc.php)
files=(
  wp-activate.php
  wp-blog-header.php
  wp-comments-post.php
  wp-config-sample.php
  wp-cron.php
  wp-links-opml.php
  wp-load.php
  wp-login.php
  wp-mail.php
  wp-settings.php
  wp-signup.php
  wp-trackback.php
  xmlrpc.php
)

changed=0
skipped=0
errors=0

for f in "${files[@]}"; do
  src="wp/$f"
  dst="$f"

  # Source must exist
  if [[ ! -e "$src" ]]; then
    echo "WARN: source missing: $src"
    ((errors++)) || true
    continue
  fi

  # Already a correct symlink?
  if [[ -L "$dst" ]]; then
    current_target="$(readlink "$dst")"
    if [[ "$current_target" == "$src" ]]; then
      echo "OK: $dst -> $src (already correct)"
      ((skipped++)) || true
      continue
    fi
    # Exists as a different symlink; handle like conflict below
  fi

  # Conflict with existing non-symlink?
  if [[ -e "$dst" && ! -L "$dst" ]]; then
    if [[ "$FORCE" -eq 1 ]]; then
      echo "NOTE: removing existing file: $dst"
      [[ "$DRYRUN" -eq 1 ]] || rm -rf -- "$dst"
    else
      echo "SKIP: $dst exists (use --force to overwrite)"
      ((skipped++)) || true
      continue
    fi
  fi

  echo "LINK: $dst -> $src"
  if [[ "$DRYRUN" -eq 0 ]]; then
    ln -sfn "$src" "$dst"
  fi
  ((changed++)) || true
done

echo
echo "Summary: changed=$changed skipped=$skipped warnings=$errors"
[[ "$DRYRUN" -eq 1 ]] && echo "(dry run; no changes made)"
