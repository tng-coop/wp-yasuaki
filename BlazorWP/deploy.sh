#!/usr/bin/env bash
set -euo pipefail

# Ensure required environment variables are set
: "${Server__User:?Environment variable Server__User must be set}"
: "${Server__Host:?Environment variable Server__Host must be set}"
: "${Server__RemoteBlazorDir:?Environment variable Server__RemoteBlazorDir must be set}"

# Configuration
PROJECT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_FILE="$PROJECT_DIR/BlazorWP.csproj"

# Publish output outside of project to avoid contaminating the source tree
PUBLISH_DIR="$PROJECT_DIR/../blazor-publish"
WWWROOT_DIR="$PUBLISH_DIR/wwwroot"
DESIRED_BASE="/blazorapp/"

# Remote deployment info
REMOTE_USER="$Server__User"
REMOTE_HOST="$Server__Host"
REMOTE_WEBPATH="$Server__RemoteBlazorDir"

# 1) Clean previous publish
echo "→ Cleaning old publish…"
rm -rf "$PUBLISH_DIR"

# 2) Restore client-side libraries into wwwroot/libman
echo "→ Restoring client-side libraries…"
libman restore

# 3) Publish the project (implicit NuGet restore, build, and pack into PUBLISH_DIR)
echo "→ Publishing $PROJECT_FILE to $PUBLISH_DIR…"
set +o pipefail
PUBLISH_OUTPUT=$(dotnet publish "$PROJECT_FILE" -c Release -o "$PUBLISH_DIR" 2>&1)
PUBLISH_EXIT=$?
set -o pipefail

# Filter out the specific WASM warnings/optimizations if desired
echo "$PUBLISH_OUTPUT" | sed -E '/WASM0001|WASM0060|WASM0062|Optimizing assemblies for size/d'
if [[ $PUBLISH_EXIT -ne 0 ]]; then
  exit $PUBLISH_EXIT
fi

# 5) Patch <base> href in the generated index.html
echo "→ Patching <base> href in $WWWROOT_DIR/index.html…"
if [[ -f "$WWWROOT_DIR/index.html" ]]; then
  sed -i -E \
    "s|<base href=\"[^\"]*\" ?/?>|<base href=\"$DESIRED_BASE\" />|" \
    "$WWWROOT_DIR/index.html"
else
  echo "❌ ERROR: $WWWROOT_DIR/index.html not found"
  exit 1
fi

# 6) Deploy via rsync (quiet mode to reduce verbosity)
echo "→ Rsyncing to $REMOTE_HOST:$REMOTE_WEBPATH…"
rsync -azq --delete \
  "$WWWROOT_DIR/" \
  "${REMOTE_USER}@${REMOTE_HOST}:${REMOTE_WEBPATH}/"

echo "✅ Deployment complete!"
