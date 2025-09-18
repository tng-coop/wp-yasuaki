#!/usr/bin/env bash
set -euo pipefail

# Adds this repo's scripts/ directory to PATH via ~/.bashrc, idempotently.

# Resolve absolute path to repo/scripts (directory containing this script)
SELF_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SCRIPTS_DIR="$SELF_DIR"  # this script lives inside scripts/, so that's the target

# Target shell rc file
BASHRC="${HOME}/.bashrc"

# Marker block (used for idempotency)
START_MARK="# >>> wp-yasuaki scripts PATH >>>"
END_MARK="# <<< wp-yasuaki scripts PATH <<<"

# The block content we want in ~/.bashrc
BLOCK="$(cat <<EOF
${START_MARK}
# Add repo scripts to PATH (idempotent)
if [ -d "${SCRIPTS_DIR}" ] && [[ ":\$PATH:" != *":${SCRIPTS_DIR}:"* ]]; then
  PATH="${SCRIPTS_DIR}:\$PATH"
fi
${END_MARK}
EOF
)"

# Ensure ~/.bashrc exists
touch "${BASHRC}"

# If a previous block exists, replace it; otherwise append a new block
if grep -Fq "${START_MARK}" "${BASHRC}"; then
  tmpfile="$(mktemp)"
  awk -v start="${START_MARK}" -v end="${END_MARK}" '
    BEGIN { skipping=0 }
    index(\$0, start) { skipping=1; next }
    index(\$0, end)   { skipping=0; next }
    skipping==0 { print }
  ' "${BASHRC}" > "${tmpfile}"
  printf "\n%s\n" "${BLOCK}" >> "${tmpfile}"
  mv "${tmpfile}" "${BASHRC}"
  echo "Updated PATH block in ${BASHRC} → ${SCRIPTS_DIR}"
else
  printf "\n%s\n" "${BLOCK}" >> "${BASHRC}"
  echo "Added PATH block to ${BASHRC} → ${SCRIPTS_DIR}"
fi

echo
echo "Reload your shell or run:  source ~/.bashrc"
