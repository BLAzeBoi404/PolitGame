#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"
mkdir -p build
if [[ -f /workspace/tooling/env.sh ]]; then source /workspace/tooling/env.sh; fi
if command -v mcs >/dev/null && [[ ! -f /workspace/tooling/sysroot/usr/lib/mono/4.5/mcs.exe ]]; then
  compiler=(mcs)
else
  compiler=(mono /workspace/tooling/sysroot/usr/lib/mono/4.5/mcs.exe)
fi
"${compiler[@]}" -langversion:7.2 -target:winexe -optimize+ -out:build/Sovereign.exe \
  -r:System.Windows.Forms -r:System.Drawing -r:System.Xml -r:System.Core \
  -resource:assets/world.bin,world.bin -resource:assets/tank.png,tank.png \
  -win32icon:assets/app.ico src/*.cs
