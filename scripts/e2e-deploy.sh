#!/usr/bin/env bash
set -euo pipefail

# Minimal fix: repo root + Blazor app path for new layout
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
BLAZORWP_DIR="${REPO_ROOT}/src/BlazorWP"

APP_PROJECT="${APP_PROJECT:-${BLAZORWP_DIR}/BlazorWP.csproj}"
CONFIG="${CONFIG:-Release}"
# Keep old name; your symlink 'blazor-publish' points to artifacts/blazor
OUT_DIR="${OUT_DIR:-${REPO_ROOT}/blazor-publish}"
BASE_HREF="${BASE_HREF:-/blazorapp/}"
TARGET_DIR="${TARGET_DIR:-/var/www/html/wordpress/blazorapp}"
PATCH_HTACCESS="${PATCH_HTACCESS:-1}"
USE_MSBUILD_BASE="${USE_MSBUILD_BASE:-0}"

# ===== SUDO (CI vs local) =====
SUDO=""
if [[ "${GITHUB_ACTIONS:-}" == "true" || "${CI:-}" == "true" ]]; then
  SUDO="sudo"
fi

[[ "$BASE_HREF" == */ ]] || { echo "BASE_HREF must end with '/'."; exit 1; }

echo "==> Publishing $APP_PROJECT ($CONFIG) → $OUT_DIR"
mkdir -p "$OUT_DIR"
if [[ "$USE_MSBUILD_BASE" == "1" ]]; then
  dotnet publish "$APP_PROJECT" -c "$CONFIG" -o "$OUT_DIR" \
    -p:StaticWebAssetBasePath="${BASE_HREF#/}"
else
  dotnet publish "$APP_PROJECT" -c "$CONFIG" -o "$OUT_DIR"
fi

INDEX="$OUT_DIR/wwwroot/index.html"
[[ -f "$INDEX" ]] || { echo "index.html not found at $INDEX"; exit 1; }

if [[ "$USE_MSBUILD_BASE" != "1" ]]; then
  echo "==> Patching <base href> → $BASE_HREF"
  sed -i -E "s#<base[[:space:]]+href=\"[^\"]*\"[[:space:]]*/?>#<base href=\"${BASE_HREF}\" />#i" "$INDEX"
fi

grep -i '<base href' "$INDEX" >/dev/null || { echo "no <base href> found"; exit 1; }

echo "==> Ensuring target dir exists: $TARGET_DIR"
$SUDO mkdir -p "$TARGET_DIR"

echo "==> Deploying to $TARGET_DIR"
$SUDO rsync -az --delete "$OUT_DIR/wwwroot/" "$TARGET_DIR/"

if [[ "$PATCH_HTACCESS" == "1" ]]; then
  echo "==> Writing .htaccess (wasm MIME + SPA fallback @ $BASE_HREF)"
  $SUDO tee "$TARGET_DIR/.htaccess" >/dev/null <<HT
AddType application/wasm .wasm

RewriteEngine On
RewriteBase ${BASE_HREF}
RewriteCond %{REQUEST_FILENAME} !-f
RewriteCond %{REQUEST_FILENAME} !-d
RewriteRule ^ index.html [L]
HT
fi

echo "==> Deployed files (head):"
$SUDO ls -la "$TARGET_DIR" | sed -n '1,80p'

APP_URL="${BASE_HREF}"
# If you want to curl via a full origin, prepend it, e.g. WP_BASE_URL="https://wp.lan"
if [[ -n "${WP_BASE_URL:-}" ]]; then
  APP_URL="${WP_BASE_URL%/}${BASE_HREF}"
fi

echo "==> Testing deployment with curl: ${APP_URL}"
if curl -fsS -o /dev/null -w "%{http_code}\n" "$APP_URL" | grep -q "200"; then
  echo "✅ App responded successfully at ${APP_URL}"
else
  echo "❌ App did not respond as expected at ${APP_URL}"
  exit 1
fi

echo "✅ e2e-deploy done"
