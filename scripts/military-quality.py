#!/usr/bin/env python3
"""Rebuild the bounded scenario training proxy from a pinned public source.

Not a rating of actual units. Missing or mismatched expenditure/GDP observations
use a neutral baseline, never a continent or nationality adjustment.
"""
import concurrent.futures
import csv
import hashlib
import json
import math
from pathlib import Path
import re
import sys
import urllib.request

ROOT = Path(__file__).resolve().parents[1]
COMMIT = '144d6977b2b01ac1cbd220de754c0a005616760b'
SOURCE = ROOT / 'data/scenario-sources/military-funding.json'


def fetch(item):
    code, reference = item
    path = reference.get('source', '')
    if not path:
        return code, {'source': '', 'expenditure': {}, 'nominal_gdp': {}}
    url = f'https://raw.githubusercontent.com/factbook/factbook.json/{COMMIT}/{path}'
    with urllib.request.urlopen(url, timeout=30) as response:
        data = json.load(response)
    return code, {'source': url,
                  'expenditure': data.get('Military and Security', {}).get('Military expenditures', {}),
                  'nominal_gdp': data.get('Economy', {}).get('GDP (official exchange rate)', {})}


def main():
    if '--fetch' in sys.argv:
        refs = json.loads((ROOT / 'data/scenario-sources/military-reference.json').read_text())
        with concurrent.futures.ThreadPoolExecutor(max_workers=6) as pool:
            rows = dict(pool.map(fetch, refs.items()))
        SOURCE.write_text(json.dumps({'commit': COMMIT, 'retrieved_utc': '2026-10-28', 'countries': rows}, indent=2) + '\n')
    data = json.loads(SOURCE.read_text())['countries']
    # Preserve the game's country order; column names are intentionally not guessed.
    raw = list(csv.reader((ROOT / 'assets/scenario-countries.tsv').open(), delimiter='\t'))[1:]
    output = ['code\ttraining_baseline\tfunding_per_person_usd\texpenditure_year\tgdp_year\tbasis']
    sourced = 0
    for row in raw:
        code, strength = row[0], int(row[3])
        record = data.get(code, {})
        gdp = re.search(r'\$([\d.,]+) (trillion|billion|million).*?\((\d{4})', record.get('nominal_gdp', {}).get('text', ''))
        expenditure = []
        for entry in record.get('expenditure', {}).values():
            if not isinstance(entry, dict):
                continue
            match = re.search(r'([\d.]+)% of GDP \((\d{4})', entry.get('text', ''))
            if match:
                expenditure.append((int(match[2]), float(match[1])))
        baseline, per_person, ey, gy, basis = 45, 0, 0, 0, 'neutral fallback; insufficient compatible source data'
        if gdp and expenditure and strength > 0:
            gy = int(gdp[3]); ey, percent = min(expenditure, key=lambda x: (abs(x[0] - gy), -x[0]))
            if abs(ey - gy) <= 1 and percent > 0:
                nominal = float(gdp[1].replace(',', '')) * {'trillion': 1e12, 'billion': 1e9, 'million': 1e6}[gdp[2]]
                per_person = round(nominal * percent / 100 / strength)
                baseline = round(max(30, min(70, 30 + 16 * math.log10(max(1000, per_person) / 1000))))
                basis = 'modeled funding proxy; not an observed military skill rating'
                sourced += 1
        output.append(f'{code}\t{baseline}\t{per_person}\t{ey}\t{gy}\t{basis}')
    target = ROOT / 'assets/scenario-military-quality.tsv'
    target.write_text('\n'.join(output) + '\n')
    print(f'{len(raw)} countries; {sourced} sourced funding proxies; {len(raw)-sourced} neutral fallbacks')
    print('source sha256:', hashlib.sha256(SOURCE.read_bytes()).hexdigest())


if __name__ == '__main__':
    main()
