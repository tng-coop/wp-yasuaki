# save as run_tests.sh
#!/usr/bin/env bash
set -uE -o pipefail

# Directory this script lives in
SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" >/dev/null 2>&1 && pwd -P)"
cd "$SCRIPT_DIR"

# Change this to 'test.*py' if you literally want a dot after 'test'
PATTERN="${1:-test*.py}"

# Collect files
mapfile -d '' FILES < <(find . -maxdepth 1 -type f -name "$PATTERN" -print0 | sort -z)

if ((${#FILES[@]} == 0)); then
  echo "No files matching '$PATTERN' in $SCRIPT_DIR" >&2
  exit 1
fi

pass=0; fail=0; failed=()
for f in "${FILES[@]}"; do
  f="${f#./}"
  echo "==> Running $f"
  if python3 "$f"; then
    ((pass++))
  else
    ((fail++))
    failed+=("$f")
  fi
done

echo
echo "Summary: $pass passed, $fail failed."
if ((fail > 0)); then
  printf 'Failed files:\n'; printf '  %s\n' "${failed[@]}"
  exit 1
fi
