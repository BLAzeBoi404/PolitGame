#!/usr/bin/env bash
# Debian 13 cloud setup without root. APT verifies signed indexes and package hashes.
set -euo pipefail
cd "$(dirname "$0")/.."
tooling=/workspace/tooling
mkdir -p "$tooling/apt/lists/partial" "$tooling/apt/archives/partial" "$tooling/apt/empty" "$tooling/sysroot" "$tooling/font-cache"
if [[ ! -f "$tooling/sysroot/.sovereign-gpu-ready" ]]; then
  cat > "$tooling/apt/sources.list" <<'SOURCES'
deb [signed-by=/usr/share/keyrings/debian-archive-keyring.gpg] https://deb.debian.org/debian trixie main
SOURCES
  cat > "$tooling/apt/apt.conf" <<'CONFIG'
Dir::Etc::parts "/workspace/tooling/apt/empty";
Dir::Etc::main "/workspace/tooling/apt/empty-main";
Dir::Etc::sourcelist "/workspace/tooling/apt/sources.list";
Dir::Etc::sourceparts "/workspace/tooling/apt/empty";
Dir::State::lists "/workspace/tooling/apt/lists";
Dir::Cache::archives "/workspace/tooling/apt/archives";
Dir::State::status "/workspace/tooling/apt/status";
Dir::Log "/workspace/tooling/apt";
APT::Get::List-Cleanup "false";
CONFIG
  touch "$tooling/apt/status"
  APT_CONFIG="$tooling/apt/apt.conf" /usr/bin/apt-get update
  APT_CONFIG="$tooling/apt/apt.conf" /usr/bin/apt-get install --download-only --no-install-recommends -y mono-devel libgdiplus xvfb xauth fonts-dejavu-core libgl1-mesa-dri libglx-mesa0
  python3 - <<'PY'
from pathlib import Path
import subprocess
for package in sorted(Path('/workspace/tooling/apt/archives').glob('*.deb')):
    subprocess.run(['dpkg-deb', '-x', str(package), '/workspace/tooling/sysroot'], check=True)
p = Path('/workspace/tooling/sysroot/etc/mono/config')
p.write_text(p.read_text().replace('$mono_libdir', '/workspace/tooling/sysroot/usr/lib'))
PY
fi
cat > "$tooling/fonts.conf" <<'FONTS'
<?xml version="1.0"?><!DOCTYPE fontconfig SYSTEM "fonts.dtd">
<fontconfig><dir>/workspace/tooling/sysroot/usr/share/fonts</dir><cachedir>/workspace/tooling/font-cache</cachedir><alias><family>Segoe UI</family><prefer><family>DejaVu Sans</family></prefer></alias><alias><family>Georgia</family><prefer><family>DejaVu Serif</family></prefer></alias></fontconfig>
FONTS
cat > "$tooling/env.sh" <<'ENV'
export PATH="/workspace/tooling/sysroot/usr/bin:$PATH"
export MONO_CFG_DIR=/workspace/tooling/sysroot/etc
export MONO_GAC_PREFIX=/workspace/tooling/sysroot/usr
export MONO_PATH=/workspace/tooling/sysroot/usr/lib/mono/4.5
export LD_LIBRARY_PATH=/workspace/tooling/sysroot/usr/lib:/workspace/tooling/sysroot/usr/lib/x86_64-linux-gnu
export FONTCONFIG_FILE=/workspace/tooling/fonts.conf
export XKB_CONFIG_ROOT=/workspace/tooling/sysroot/usr/share/X11/xkb
ENV
source "$tooling/env.sh"
mono --version
bash build.sh
touch "$tooling/sysroot/.sovereign-gpu-ready"
