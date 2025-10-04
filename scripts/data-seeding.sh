# runs each matching file (if any), safely handles "no matches"
for f in ./data-seeding*.py; do
  [ -f "$f" ] || continue
  python3 "$f" || exit $?
done