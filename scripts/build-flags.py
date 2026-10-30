#!/usr/bin/env python3
"""Build the offline flag atlas. Requires Pillow and CairoSVG 2.8.2.

Uses flag-icons v7.5.0 (MIT). Empty codes intentionally use a labelled in-game
fallback, rather than assigning another country's flag. Sources are cached.
"""
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path
from urllib.request import urlopen
import hashlib
import io
import json
import cairosvg
from PIL import Image

root = Path(__file__).resolve().parents[1]
cache = root / 'artifacts/flag-sources'
cache.mkdir(parents=True, exist_ok=True)
base = 'https://raw.githubusercontent.com/lipis/flag-icons/v7.5.0/'
codes = [line.split('\t') for line in (root / 'assets/flag-codes.tsv').read_text().splitlines()[1:]]

def fetch(path, name):
    target = cache / name
    if not target.exists():
        target.write_bytes(urlopen(base + path, timeout=30).read())
    return target.read_bytes()

def flag(entry):
    code, iso = entry
    if not iso:
        return code, None, None
    data = fetch('flags/4x3/' + iso + '.svg', iso + '.svg')
    png = cairosvg.svg2png(bytestring=data, output_width=64, output_height=48)
    return code, Image.open(io.BytesIO(png)).convert('RGBA'), hashlib.sha256(data).hexdigest()

sheet = Image.new('RGBA', (16 * 64, ((len(codes) + 15) // 16) * 48))
manifest = {'source': base, 'license': 'MIT', 'size': [64, 48], 'flags': {}}
with ThreadPoolExecutor(max_workers=4) as pool:
    for index, (code, image, checksum) in enumerate(pool.map(flag, codes)):
        if image is not None:
            sheet.paste(image, ((index % 16) * 64, (index // 16) * 48))
        manifest['flags'][code] = {'index': index, 'svg_sha256': checksum}
sheet.save(root / 'assets/flags.png', optimize=True)
(root / 'assets/flags-source.json').write_text(json.dumps(manifest, indent=2) + '\n')
(root / 'assets/FLAGS-LICENSE.txt').write_bytes(fetch('LICENSE', 'LICENSE'))
print('Built', len(codes), 'entries;', sum(bool(code) for _, code in codes), 'flags; two labelled fallbacks.')
