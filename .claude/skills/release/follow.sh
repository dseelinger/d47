#!/usr/bin/env bash
# Prints the lines of a release log that no earlier run of this script printed, as they are
# written, and exits once the line starting "release.ps1 exited" has been printed.
# The count of printed lines is kept in <log>.seen, so a re-run carries on where the last one stopped.
log=$1
state=$log.seen
seen=$(cat "$state" 2>/dev/null || echo 0)
until [ -f "$log" ]; do sleep 1; done
while :; do
  total=$(wc -l < "$log")
  if [ "$total" -gt "$seen" ]; then
    sed -n "$((seen + 1)),${total}p" "$log"
    seen=$total
    echo "$seen" > "$state"
  fi
  if grep -q '^release.ps1 exited' "$log" && [ "$seen" -ge "$(wc -l < "$log")" ]; then exit 0; fi
  sleep 1
done
