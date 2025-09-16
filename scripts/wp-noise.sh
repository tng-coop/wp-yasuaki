#!/usr/bin/env bash
# wp-noise.sh — report "residual noise" left in WP test instance
# Usage: WP_PATH=/path/to/wp WP_CLI=wp ./wp-noise.sh

set -euo pipefail

wp=${WP_CLI:-wp}
wp_path=${WP_PATH:-}

if [[ -z "$wp_path" ]]; then
  echo "❌ Please set WP_PATH to your WordPress root (eg: export WP_PATH=/var/www/html)"
  exit 1
fi

run() {
  echo -e "\n▶ $*"
  $wp --path="$wp_path" "$@"
}

echo "=== WP Noise Report ==="
$wp --path="$wp_path" --version

# All posts of any type, any status
run post list --post_type=any --post_status=any --fields=ID,post_type,post_status,post_title --format=table

# All users
run user list --fields=ID,user_login,roles --format=table

# All custom post types (to see if anything non-standard is hanging around)
run post-type list --fields=name,public,show_ui --format=table

# All taxonomies
run taxonomy list --fields=name,object_type,public --format=table

echo -e "\n✅ Done. Review the above for unexpected leftovers."
