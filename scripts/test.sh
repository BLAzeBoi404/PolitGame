#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
if [[ -f /workspace/tooling/env.sh ]]; then source /workspace/tooling/env.sh; fi
bash build.sh
mkdir -p artifacts
mono build/Sovereign.exe --test artifacts/tests.txt
if [[ "${1:-}" == "--ui" ]]; then
  # The wrapper owns this X server and kills only that process on exit.
  bash scripts/with-display.sh mono build/Sovereign.exe --ui-test artifacts/ui
fi
