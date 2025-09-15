#!/usr/bin/env bash
# BlazorWP/scripts/check-wp-rest.sh
set -euo pipefail

: "${WP_BASE_URL:?Need WP_BASE_URL (e.g. https://aspnet.lan:8444 or https://wp.lan)}"
: "${WP_USERNAME:?Need WP_USERNAME}"
: "${WP_APP_PASSWORD:?Need WP_APP_PASSWORD}"

BASE="$WP_BASE_URL"
USER="$WP_USERNAME"
PASS="$WP_APP_PASSWORD"

# behavior tuning (optional)
STRICT_CANONICAL="${STRICT_CANONICAL:-0}"   # 1 => require 301/308 for no-slash; 0 => allow 401/403 too
SHOW_DIAG="${SHOW_DIAG:-1}"                 # print brief diagnostics on failures

ok()   { echo "✅ $1"; }
fail() { echo "❌ $1"; exit 1; }
peek() { sed -n '1,8p'; }
hdr()  { tr -d '\r' | sed -n '1,12p'; }

code_of() { curl -ks -o /dev/null -w '%{http_code}' "$@"; }

echo "== A) Root should be HTML =="
html=$(curl -ks "$BASE/")
if echo "$html" | grep -qi "<!DOCTYPE html"; then
  ok "Root serves HTML"
else
  [ "$SHOW_DIAG" = "1" ] && echo "$html" | peek
  fail "Root did not look like HTML"
fi

echo "== B) /wp-json/ index (public JSON) =="
# check headers first so we can show status/headers on error
h=$(curl -ksI -H 'Accept: application/json' "$BASE/wp-json/")
status=$(printf '%s\n' "$h" | awk 'NR==1{print $2}')
if [ "$status" = "200" ]; then
  body=$(curl -ks -H 'Accept: application/json' "$BASE/wp-json/")
  if jq -e 'has("namespaces")' >/dev/null 2>&1 <<<"$body"; then
    ok "/wp-json/ returns JSON with namespaces"
  else
    [ "$SHOW_DIAG" = "1" ] && { echo "--- headers ---"; printf '%s\n' "$h" | hdr; echo "--- body ---"; printf '%s\n' "$body" | peek; }
    fail "/wp-json/ not valid JSON"
  fi
else
  [ "$SHOW_DIAG" = "1" ] && { echo "--- headers ---"; printf '%s\n' "$h" | hdr; }
  fail "Unexpected status from /wp-json/: $status"
fi

echo "== C) /wp-json/wp/v2/users/me/ (authed JSON) =="
me=$(curl -ks -u "$USER:$PASS" -H 'Accept: application/json' "$BASE/wp-json/wp/v2/users/me/")
if jq -e '.id and .name' >/dev/null 2>&1 <<<"$me"; then
  ok "users/me returned id+name"
else
  [ "$SHOW_DIAG" = "1" ] && { echo "--- body ---"; printf '%s\n' "$me" | peek; }
  fail "users/me missing id/name"
fi

echo "== D) /wp-json/wp/v2/settings/ (authed JSON) =="
settings=$(curl -ks -u "$USER:$PASS" -H 'Accept: application/json' "$BASE/wp-json/wp/v2/settings/")
if jq -e '.title' >/dev/null 2>&1 <<<"$settings"; then
  ok "settings returned title"
else
  [ "$SHOW_DIAG" = "1" ] && { echo "--- body ---"; printf '%s\n' "$settings" | peek; }
  fail "settings missing title"
fi

echo "== E) /wp-json/wp/v2/settings (no slash) canonical behavior =="
ns_code=$(code_of "$BASE/wp-json/wp/v2/settings")  # use GET (not HEAD)
if [ "$STRICT_CANONICAL" = "1" ]; then
  case "$ns_code" in
    301|308) ok "no-slash redirected ($ns_code) to trailing slash (STRICT)";;
    *)       fail "Expected 301/308 (STRICT), got $ns_code";;
  esac
else
  case "$ns_code" in
    301|308) ok "no-slash redirected ($ns_code) to trailing slash";;
    401|403) ok "no-slash returned auth challenge ($ns_code) — acceptable";;
    *)       fail "Expected 301/308/401/403, got $ns_code";;
  esac
fi

echo "== F) /wp-json/wp/v2/settings/ (bad creds) =="
bad_code=$(curl -ks -o /dev/null -w '%{http_code}' \
           -u "$USER:DefinitelyWrongPassword123!" \
           -H 'Accept: application/json' "$BASE/wp-json/wp/v2/settings/")
case "$bad_code" in
  401|403) ok "bad creds returned $bad_code";;
  *)       fail "Expected 401/403 for bad creds, got $bad_code";;
esac

echo "🎉 All checks passed"
