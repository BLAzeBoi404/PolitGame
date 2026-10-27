"""Rebuild the offline scenario overlay without changing province IDs or borders.
Requires Python 3 + shapely. Inputs and original quotes are in data/scenario-sources.
Population figures are estimates; this is not a census or a real order of battle.
"""
import csv
import json
import math
import re
import struct
from pathlib import Path
from shapely.geometry import Polygon, MultiPolygon, Point

ROOT = Path(__file__).resolve().parent.parent
DATA = ROOT / 'data/scenario-sources'

def table(name, delimiter='\t'):
    with (DATA / name).open(encoding='utf-8') as stream:
        return list(csv.DictReader(stream, delimiter=delimiter))

with (ROOT / 'assets/world.bin').open('rb') as stream:
    def read(fmt):
        return struct.unpack('<' + fmt, stream.read(struct.calcsize('<' + fmt)))
    def string():
        return stream.read(read('i')[0]).decode('utf-8')
    def rings():
        return [[read('ff') for _ in range(read('i')[0])] for _ in range(read('i')[0])]
    assert read('i') == (1,)
    countries, regions = [], []
    for i in range(read('i')[0]):
        code, name = string(), string()
        pop, gdp, x, y = read('dddd'); shape = rings()
        countries.append(dict(code=code, name=name, population=pop, regions=[]))
    identities = table('region-identities.tsv')
    for i in range(read('i')[0]):
        owner, = read('i'); name = string(); pop, x, y = read('ddd'); shape = rings()
        neighbors = read('i' * read('i')[0])
        polygons = [Polygon(r) for r in shape if len(r) >= 3]
        geometry = MultiPolygon(polygons).buffer(0)
        r = dict(id=i, owner=owner, name=name, neighbors=neighbors, geometry=geometry, area=geometry.area * max(.1, math.cos(math.radians(y))), city_population=0., cities=[], basis='settlement estimate', **{k:v for k,v in identities[i].items() if k!='id'})
        assert int(identities[i]['id']) == i
        # Natural Earth reverses these two ISO codes; do not swap Moscow and its oblast.
        if r['code'] == 'RUS-2364': r['iso'] = 'RU-MOS'
        if r['code'] == 'RUS-2365': r['iso'] = 'RU-MOW'
        regions.append(r); countries[owner]['regions'].append(r)

# Original vertex matching missed enclave cities (notably Kyiv): join an isolated
# region only to a same-country polygon that geometrically contains it. Never bridge sea.
with (ROOT/'assets/scenario-adjacency.tsv').open('w') as out:
    writer = csv.writer(out,delimiter='\t',lineterminator='\n'); writer.writerow(['region','neighbor'])
    for r in regions:
        if any(regions[n]['owner'] == r['owner'] for n in r['neighbors']): continue
        for parent in countries[r['owner']]['regions']:
            if parent is r or parent['geometry'].area <= r['geometry'].area: continue
            if parent['geometry'].intersection(r['geometry']).area >= r['geometry'].area * .95:
                writer.writerow([r['id'],parent['id']]); print('Enclave connection',r['name'],'→',parent['name'])

refs = json.loads((DATA / 'military-reference.json').read_text())
pops = {v['code']:v for v in table('national-population.csv', ',')}
bycode = {c['code']:c for c in countries}
for c in countries:
    c['population'] = int(float(pops[c['code']]['population']))
    c['population_year'] = int(pops[c['code']]['year'])
    c['capital'] = -1

# Allocate each settlement once; prefer the smallest enclosing region for enclave cities.
for town in table('settlements.tsv'):
    country = bycode.get(town['country'])
    if country is None: continue
    point = Point(float(town['longitude']), float(town['latitude']))
    found = [r for r in country['regions'] if r['geometry'].covers(point)]
    if not found:
        closest = min(country['regions'], key=lambda r:r['geometry'].distance(point))
        if closest['geometry'].distance(point) > .15: continue
        found = [closest]
    region = min(found, key=lambda r:r['geometry'].area)
    pop = max(0, int(town['population']))
    if country['code'] != 'RUS': region['city_population'] += pop
    region['cities'].append((pop, town['name'], point))
    if town['capital'] == '1': country['capital'] = region['id']

# More complete Russian city statistics, using administrative identifiers rather than
# bounding-box area. Keep capitals already read from NE, replace population weights.
for r in bycode['RUS']['regions']:
    r['cities'] = []; r['basis'] = 'Russian cities 2020 estimate'
for town in table('russian-towns-2020.tsv'):
    found = [r for r in bycode['RUS']['regions'] if r['iso'] == town['iso']]
    if not found: continue
    point = Point(float(town['longitude']), float(town['latitude']))
    r = min(found, key=lambda r:r['geometry'].distance(point))
    pop = int(town['population']); r['city_population'] += pop; r['cities'].append((pop,town['name'],point))

# Rural distribution favours settled and habitable land, not bounding-box size.
# This is deliberately labelled an estimate: cities alone are not a regional census.
for c in countries:
    urban_fraction = refs.get(c['code'],{}).get('urban_fraction',.6)
    urban_total = sum(r['city_population'] for r in c['regions'])
    if urban_total == 0: urban_fraction = 0
    # NE does not enumerate every town. Do not inflate the few known cities to absorb
    # all of the national urban population; distribute the unobserved share regionally.
    urban_fraction = min(urban_fraction, urban_total * 1.35 / c['population'])
    for r in c['regions']:
        latitude = abs(r['geometry'].representative_point().y)
        climate = max(.06, math.exp(-max(0, latitude-52)/9))
        r['rural_weight'] = (min(r['city_population'],c['population']/len(c['regions'])*.5) + c['population']/len(c['regions'])*.15)**.4 * max(.0001,r['area'])**.4 * climate
        if r['geometry'].area < .04 or r['iso'] in ['RU-MOW','RU-SPE']: r['rural_weight'] *= .1  # city municipalities
    rural_total = sum(r['rural_weight'] for r in c['regions'])
    for r in c['regions']:
        r['population'] = c['population'] * (urban_fraction * r['city_population']/max(1,urban_total) + (1-urban_fraction)*r['rural_weight']/rural_total)

# Historical state census estimates give a better distribution than city weighting.
usa = {v['Postal']:float(v['Population']) for v in table('us-state-population-2014.csv',',')}
total = sum(usa.values())
for r in bycode['USA']['regions']:
    state = r['iso'].split('-')[-1]
    assert state in usa, state
    r['population'] = bycode['USA']['population'] * usa[state] / total
    r['basis'] = 'US state shares 2014, national total 2024'

weights = {r['iso']:r for r in table('regional-weights.csv',',')}
for code in ['CHN','IND']:
    c = bycode[code]
    assert all(r['iso'] in weights for r in c['regions'])
    total = sum(float(weights[r['iso']]['weight']) for r in c['regions'])
    for r in c['regions']:
        row = weights[r['iso']]; r['population'] = c['population'] * float(row['weight']) / total
        r['basis'] = row['basis'] + '; normalized to national 2024 total'

# Explicit rounded scenario anchors correct sparse-city sampling in the Far North and
# large municipal/oblast overlaps. They are estimates, not a claimed 2026 census.
anchors = {r['iso']:r for r in table('region-population-anchors.csv',',')}
for c in countries:
    fixed = [r for r in c['regions'] if r['iso'] in anchors]
    if not fixed: continue
    available = c['population'] - sum(int(anchors[r['iso']]['population']) for r in fixed)
    free = [r for r in c['regions'] if r['iso'] not in anchors]
    total = sum(r['population'] for r in free)
    for r in free: r['population'] *= available/total
    for r in fixed:
        row = anchors[r['iso']]; r['population'] = int(row['population']); r['basis'] = row['basis']

# Integers conserve every national total exactly; all map anchors lie inside their region.
for c in countries:
    total = sum(r['population'] for r in c['regions'])
    for r in c['regions']: r['population'] = int(round(r['population']*c['population']/total))
    largest = max(c['regions'],key=lambda r:r['population'])
    largest['population'] += c['population'] - sum(r['population'] for r in c['regions'])
    for r in c['regions']:
        ordered = sorted(r['cities'],key=lambda v:-v[0])
        valid = next((t for t in ordered if r['geometry'].contains(t[2])), None)
        anchor = valid[2] if valid else r['geometry'].representative_point()
        # Some parent exterior rings include separately modelled cities. Avoid anchoring
        # a parent army in a different selectable child region.
        children = [q['geometry'] for q in c['regions'] if q is not r and q['geometry'].area < r['geometry'].area and q['geometry'].contains(anchor)]
        if children:
            usable = r['geometry']
            for child in children: usable = usable.difference(child)
            anchor = usable.representative_point(); valid = None
        r['anchor'] = anchor; r['settlement'] = valid[1] if valid else ''
    if c['capital'] < 0: c['capital'] = largest['id']

# Parse the cited military estimate, preserving ranges rather than misreading "10-12,000".
quantity = r'(\d[\d,.]*)(?:\s*[-–]\s*(\d[\d,.]*))?\s*(million|thousand)?'
def amount(match):
    low = float(match[1].replace(',','')); high = float((match[2] or match[1]).replace(',',''))
    unit = 1000000 if match[3]=='million' else 1000 if match[3]=='thousand' else 1
    if unit > 1:
        if low < 1000: low *= unit
        if high < 1000: high *= unit
    elif high >= 1000 and low < 1000 and match[2]: low *= 1000
    return round((low+high)/2)
no_army = {'CRI','ISL','PAN','SLB','VUT','GRL','NCL','ATF','FLK','ATA','PRI','MUS'}
with (ROOT/'assets/scenario-countries.tsv').open('w') as out:
    writer=csv.writer(out,delimiter='\t',lineterminator='\n');writer.writerow(['code','population','population_year','active_reference','ground_target','military_year','ground_basis','capital'])
    for c in countries:
        ref=refs.get(c['code'],{});txt=ref.get('strength_text','');services=ref.get('services_text','')
        matches=list(re.finditer(quantity,txt)); first=matches[0] if matches else None
        active=amount(first) if first else 0; basis='65% model share'; ground=round(active*.65)
        if c['code']=='SDN': active=200000;ground=130000
        ground_match=re.search(quantity+r'\s*(?:in the )?(?:Army|Ground(?: Forces)?|Land Forces)\b',txt)
        if ground_match: ground=amount(ground_match);basis='reported ground strength'
        if c['code']=='CYN': active=3500;ground=3500;basis='scenario estimate; no reference'
        if c['code'] in no_army or (not txt and re.search(r'no (?:regular )?military',services,re.I)):
            active=ground=0;basis='no standing army'
        if not first and c['code'] not in no_army and basis!='no standing army' and c['code']!='CYN':
            # No numerical estimate is available (e.g. Syria/Libya/Yemen): explicit fallback.
            active=round(min(200000,c['population']*.0015));ground=round(active*.65);basis='population model; no numerical reference'
        ground=min(active,ground)
        years=re.findall(r'\((20\d\d)\)',txt);year=int(years[-1]) if years else 0
        writer.writerow([c['code'],c['population'],c['population_year'],active,ground,year,basis,c['capital']])
with (ROOT/'assets/scenario-regions.tsv').open('w') as out:
    writer=csv.writer(out,delimiter='\t',lineterminator='\n');writer.writerow(['id','country','population','longitude','latitude','settlement','basis'])
    for r in regions:
        writer.writerow([r['id'],countries[r['owner']]['code'],r['population'],format(r['anchor'].x,'.6f'),format(r['anchor'].y,'.6f'),r['settlement'],r['basis']])
for c in countries:
    if c['code'] in ['RUS','MDA','USA','UKR','CHN','IND']:
        top=sorted(c['regions'],key=lambda r:-r['population'])[:5]
        print(c['code'],c['population'],[(r['name'],r['population']) for r in top])
print('Overlay built:',len(countries),'countries;',len(regions),'regions. IDs and boundaries unchanged.')
