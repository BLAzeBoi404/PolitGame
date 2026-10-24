#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
if [[ -f /workspace/tooling/env.sh ]]; then source /workspace/tooling/env.sh; fi
mkdir -p artifacts
if [[ -n "${DISPLAY:-}" ]]; then exec "$@"; fi
# -displayfd selects a free display, preventing interference with user servers.
number_file=$(mktemp /tmp/sovereign-display.XXXXXX)
Xvfb -displayfd 3 -screen 0 1920x1080x24 -nolisten tcp -ac 3>"$number_file" > artifacts/xvfb.log 2>&1 &
server_pid=$!
trap 'kill "$server_pid" 2>/dev/null || true; wait "$server_pid" 2>/dev/null || true; rm -f "$number_file"' EXIT
for attempt in {1..100}; do
  if [[ -s "$number_file" ]]; then break; fi
  if ! kill -0 "$server_pid" 2>/dev/null; then cat artifacts/xvfb.log; exit 1; fi
  sleep 0.05
done
[[ -s "$number_file" ]] || { cat artifacts/xvfb.log; exit 1; }
export DISPLAY=":$(cat "$number_file")"
"$@"
