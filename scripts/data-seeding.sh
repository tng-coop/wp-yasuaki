#!/usr/bin/env bash
set -euo pipefail
shopt -s nullglob

# ========= Preflight: env =========
: "${WP_BASE_URL:?Set WP_BASE_URL (e.g. https://wp.lan)}"
: "${WP_USERNAME:?Set WP_USERNAME}"
: "${WP_APP_PASSWORD:?Set WP_APP_PASSWORD}"

BASE="$WP_BASE_URL/wp-json/wp/v2"
AUTH=(-u "$WP_USERNAME:$WP_APP_PASSWORD")

# If you plan to let the nav seeder insert a Navigation block into the active header,
# export ALLOW_HEADER_NAV_INSERT=1 before running this script.
ALLOW_INSERT="${ALLOW_HEADER_NAV_INSERT:-}"

# ========= Preflight: capability checks (no writes) =========
http_code() { curl -sS -o /dev/null -w "%{http_code}" "${AUTH[@]}" "$1" || echo "000"; }

code_rest=$(http_code "$BASE")
code_pages_view=$(http_code "$BASE/pages?per_page=1&_fields=id")
code_pages_edit=$(http_code "$BASE/pages?per_page=1&context=edit&_fields=id")
code_nav_edit=$(http_code "$BASE/navigation?per_page=1&context=edit&_fields=id")

if [[ "$ALLOW_INSERT" =~ ^(1|true|yes)$ ]]; then
  code_tplparts_edit=$(http_code "$BASE/template-parts?per_page=1&context=edit&_fields=id")
else
  code_tplparts_edit="N/A"
fi

echo "=== data-seeding preflight ==="
printf "%-38s %s\n" "REST root"                              "$code_rest"
printf "%-38s %s\n" "Pages (view context)"                   "$code_pages_view"
printf "%-38s %s\n" "Pages (edit context)"                   "$code_pages_edit"
printf "%-38s %s\n" "Navigation (edit context)"              "$code_nav_edit"
printf "%-38s %s\n" "Template parts (edit ctx, if insert)"   "$code_tplparts_edit"
echo

# ========= Policy: decide if we proceed =========
# Minimum to run all seeders safely:
#  - pages edit  == 200 (can create/update /branches and branch pages)
#  - navigation  == 200 (can add "Branches" to nav)
#  - if ALLOW_HEADER_NAV_INSERT=1 then template-parts edit == 200
proceed=true
reasons=()

[[ "$code_rest"        == 200 ]] || { proceed=false; reasons+=("REST not reachable (HTTP $code_rest)"); }
[[ "$code_pages_view"  == 200 ]] || { proceed=false; reasons+=("Cannot list pages (view) (HTTP $code_pages_view)"); }
[[ "$code_pages_edit"  == 200 ]] || { proceed=false; reasons+=("No page edit capability (HTTP $code_pages_edit)"); }
[[ "$code_nav_edit"    == 200 ]] || { proceed=false; reasons+=("No navigation edit capability (HTTP $code_nav_edit)"); }
if [[ "$ALLOW_INSERT" =~ ^(1|true|yes)$ ]]; then
  [[ "$code_tplparts_edit" == 200 ]] || { proceed=false; reasons+=("ALLOW_HEADER_NAV_INSERT set, but cannot edit template parts (HTTP $code_tplparts_edit)"); }
fi

if ! $proceed; then
  echo "❌ Preflight failed. Exiting before running seeders."
  for r in "${reasons[@]}"; do echo " - $r"; done
  echo
  echo "Tips:"
  echo " • Use an Administrator app password to gain page/nav/template-part access."
  echo " • If you don't want header edits, unset ALLOW_HEADER_NAV_INSERT."
  echo " • You can still run read-only scripts manually if needed."
  exit 10
fi

echo "✅ Preflight OK. Running seeders…"
echo

# ========= Run all data seeding scripts =========
for f in ./data-seeding*.py; do
  [ -f "$f" ] || continue
  echo "→ python3 $f"
  python3 "$f"
done

echo
echo "✅ All seeders completed."
