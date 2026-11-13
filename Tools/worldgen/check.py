#!/usr/bin/env python3
# Reads the world as written, not as generated, and checks it against the owner's list (brief of 2026-11-06, phase 11). Exits 1 on a failure.
#   python3 -I Tools/worldgen/check.py [world dir]
import json, math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import dadc
from geo import *
import gen, parking, place

here = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
out = sys.argv[1] if len(sys.argv) > 1 else os.path.join(here, 'Assets/StreamingAssets/World')
world = json.load(open(os.path.join(out, 'world.json')))
chunks = [dadc.decode(open(os.path.join(out, 'chunks', e['id'] + '.dadc'), 'rb').read()) for e in world['chunks']]
models = place.load_models()
fails = []
def check(name, ok, detail=''):
    print('%-4s %-46s %s' % ('ok' if ok else 'FAIL', name, detail))
    if not ok: fails.append(name)

cells = {(c['ix'], -(c['iy'] + 1)) for c in chunks}
check('area is exactly 15.5 km2', len(cells) * 0.25 == 15.5 and abs(area(gen.BOUNDARY) - 15500000) < 1e-6, '%d chunks; boundary shoelace %.1f m2' % (len(cells), area(gen.BOUNDARY)))
check('chunks fill the boundary, no more', all(inside(((x + .5) * 500, (z + .5) * 500), gen.BOUNDARY) for x, z in cells))
roads = [r for c in chunks for r in c['roads']]
ways = {}
for r in roads: ways.setdefault(r['id'], []).append(r)
names = {r.get('nme') for r in roads}
for old in ('Farmgate', 'Karwan Bazar', 'Kawran Bazar', 'Tejgaon'): check('no road called ' + old, old not in names)
fly = {}
for r in roads:
    n = r.get('nme') or ''
    if 'Flyover' in n: fly.setdefault(n.replace(' slip', ''), []).append(r)
check('exactly four flyovers', len(fly) == 4, ', '.join(sorted(fly)))
ends = {}
for c in chunks:
    for e in c['edges']:
        pass
def rounded(p): return (round(p[0], 2), round(p[1], 2))
for name, rs in sorted(fly.items()):
    rs_deck = [r for r in rs if r.get('bridge') and not r.get('ys')]
    ramps = {r['id'] for r in rs if r.get('ys')}
    slips = {r['id'] for r in rs if (r.get('nme') or '').endswith('slip')}
    check(name + ': two ramps, four slips', len(ramps) == 2 and len(slips) == 4, 'ramps %d slips %d' % (len(ramps), len(slips)))
    # Both toes on the ground and meeting their slips; the ramp's top meeting the deck.
    deck_pts = {rounded(p) for r in rs_deck for p in r['pts']}
    slip_pts = {rounded(p) for r in rs if (r.get('nme') or '').endswith('slip') for p in r['pts']}
    good = True
    for rid in ramps:
        parts = list(ways[rid])
        # Pieces of a way come chunk by chunk: chain them end to end.
        chain = [parts.pop(0)]
        while parts:
            for r in parts:
                if rounded(r['pts'][0]) == rounded(chain[-1]['pts'][-1]): chain.append(r)
                elif rounded(r['pts'][-1]) == rounded(chain[0]['pts'][0]): chain.insert(0, r)
                else: continue
                parts.remove(r)
                break
            else: break
        ys = [y for r in chain for y in r['ys']]
        pts = [p for r in chain for p in r['pts']]
        i, j = (0, -1) if min(ys[0], ys[-1]) == ys[0] else (-1, 0)
        toe_ok = ys[i] < 0.4 and rounded(pts[i]) in slip_pts
        top_ok = abs(ys[j] - gen.DECK_Y) < 0.02 and rounded(pts[j]) in deck_pts
        step = max(abs(ys[k + 1] - ys[k]) / max(0.01, dist(pts[k], pts[k + 1])) for k in range(len(ys) - 1) if dist(pts[k], pts[k + 1]) > 0.5)
        good = good and toe_ok and top_ok and step < 0.1
    check(name + ': ramps join ground and deck', good, 'steepest grade under 10%' if good else '')
    under = gen.DECK_Y - 2.0
    check(name + ': truck clearance', under >= 4.8, 'about %.1f m under girders' % under)
placed = [b for c in chunks for b in c['buildings'] if b.get('mdl')]
rails = [r for c in chunks for r in c['rails']]
metro = [r for r in rails if r['y'] >= 10]
ground = [r for r in rails if r['y'] < 1]
check('one elevated metro line', len({r['id'] for r in metro}) == 1, '%.0f m' % sum(length(r['pts']) for r in metro))
tracks = [b for b in placed if b['t'] == 'track']
check('railway on the ground', len(ground) > 0 and len(tracks) * models['rail_long']['size'][2] > 0.9 * sum(length(r['pts']) for r in ground), '%.0f m in %d lines, %d lengths of track' % (sum(length(r['pts']) for r in ground), len({r['id'] for r in ground}), len(tracks)))
in_port = sum(1 for r in ground for p in r['pts'] if inside(p, gen.PORT_Z))
check('railway reaches the port', in_port > 0, '%d points inside the Port zone' % in_port)
st = [b for b in placed if b['mdl'] == 'metro_station']
cars = [b for b in placed if b['mdl'] == 'metro_car']
check('a train parked at every station', len(cars) == 12, '%d cars' % len(cars))
check('exactly three metro stations', len(st) == 3, ', '.join(b.get('nm', '?') for b in st))
at = [(b['pose'][0] / 100, b['pose'][1] / 100) for b in st]
check('one station in the hub, two elsewhere', sum(1 for p in at if gen.zone_at(p) == 'hub') == 1, ', '.join(str(gen.zone_at(p)) for p in at))
line = [r['pts'] for r in metro]
check('stations stand on the line', all(min(seg_point(l[i], l[i + 1], p)[0] for l in line for i in range(len(l) - 1)) < 0.5 for p in at))
missing = sorted({b['mdl'] for b in placed if b['mdl'] not in models and b['mdl'] != 'collider'})
check('every model name exists', not missing, ' '.join(missing))
fixed = [l.split() for l in open(os.path.join(out, 'fixed.txt'))]
names_fixed = [f[0] for f in fixed]
check('airport models at the airport', all(n in names_fixed for n in ('airport_field', 'airport_terminal', 'airport_sheds')))
check('aircraft from the folder', sum(n.startswith('jet') for n in names_fixed) >= 2, '%d aircraft' % sum(n.startswith('jet') for n in names_fixed))
check('airport inside the pink zone', all(inside((float(f[1]), float(f[2])), gen.AIRPORT_Z) for f in fixed))
air_road = [r for r in roads if r.get('nme') == 'Airport Road']
check('airport road built', bool(air_road), '%.0f m' % sum(length(r['pts']) for r in air_road))
hw = {rounded(p) for r in roads if r.get('nme') == 'Airport Highway' for p in r['pts']}
check('airport road meets the highway', any(rounded(p) in hw for r in air_road for p in r['pts']))
real = [b for b in placed if b['t'] not in ('container', 'station', 'prop', 'track', 'grass', 'tree', 'ship', 'boat', 'yard', 'quay', 'crane', 'train')]
share = sum(b['pose'][5] for b in real) / len(real)
check('at least 40% of buildings mirrored', share >= 0.4, '%.1f%% of %d' % (share * 100, len(real)))
minis = [b for b in real if b['mdl'].startswith('mini')]
pt = lambda b: (b['pose'][0] / 100, b['pose'][1] / 100)
bad = [b for b in minis if gen.zone_at(pt(b)) not in ('slum', None)]
check('mini buildings only in the slums', len(bad) <= len(minis) * 0.002, '%d of %d outside the slum outline' % (len(bad), len(minis)))
big = [b for b in real if not b['mdl'].startswith('mini') and gen.zone_at(pt(b)) == 'slum']
check('nothing large in the slums', len(big) <= 5, '%d of %d others inside the slum outline' % (len(big), len(real) - len(minis)))
pairs = same = 0
grid = Grid(30.0)
boxes = []
for b in real:
    if len(b['outer']) != 4: continue
    o = b['outer']
    c = ((o[0][0] + o[2][0]) / 2, (o[0][1] + o[2][1]) / 2)
    l = dist(o[0], o[1]) or 1
    t = ((o[1][0] - o[0][0]) / l, (o[1][1] - o[0][1]) / l)
    x = place.Box(c, t, l / 2, dist(o[1], o[2]) / 2)
    x_b = (x, b)
    boxes.append(x_b)
    r = x.reach()
    grid.add(x_b, c[0] - r, c[1] - r, c[0] + r, c[1] + r)
hit = 0
for x, b in boxes:
    r = x.reach() + 6
    for y, o in grid.near(x.c[0] - r, x.c[1] - r, x.c[0] + r, x.c[1] + r):
        if y is x: continue
        if id(y) > id(x):
            if place.overlap(x, y, -0.01): hit += 1
            if place.overlap(x, y, 6):
                pairs += 1
                if b['mdl'] == o['mdl'] and b['pose'][5] == o['pose'][5] and b['pose'][6] == o['pose'][6] and b['pose'][4] == o['pose'][4]: same += 1
check('no two buildings overlap', hit == 0, '%d pairs' % hit)
check('no two neighbours identical', same == 0, '%d of %d neighbouring pairs' % (same, pairs))
segs = Grid(40.0)
for r in roads:
    if r.get('bridge') or r.get('ys'): continue
    for i in range(len(r['pts']) - 1):
        a, e = r['pts'][i], r['pts'][i + 1]
        h = r['w'] / 2
        segs.add((a, e, h), min(a[0], e[0]) - h, min(a[1], e[1]) - h, max(a[0], e[0]) + h, max(a[1], e[1]) + h)
on_road = 0
for x, b in boxes:
    q = x.reach()
    for a, e, h in segs.near(x.c[0] - q, x.c[1] - q, x.c[0] + q, x.c[1] + q):
        if place.seg_hits_box(a, e, x, h):
            on_road += 1
            break
check('no building on a road', on_road == 0, '%d' % on_road)
trees = [b for b in placed if b['t'] == 'tree']
bins = {}
for b in trees: bins[gen.zone_at(pt(b)) or 'open land'] = bins.get(gen.zone_at(pt(b)) or 'open land', 0) + 1
kinds = {}
for b in trees: kinds[b['mdl']] = kinds.get(b['mdl'], 0) + 1
check('every tree is one of the owner\'s models', all(k in ('tree_oak', 'tree_broad_a', 'tree_broad_b', 'tree_low_a', 'tree_low_b') for k in kinds) and len(kinds) == 5, str(sorted(kinds.items())))
print('     trees by district: %s' % sorted(bins.items()))
check('no tree floats', all(b['pose'][2] == 0 for b in trees))
def trunk(b): return place.Box(pt(b), (1, 0), 0.5, 0.5)
near_road = 0
for b in trees:
    x = trunk(b)
    if any(place.seg_hits_box(a, e, x, h + 0.2) for a, e, h in segs.near(x.c[0] - 2, x.c[1] - 2, x.c[0] + 2, x.c[1] + 2)): near_road += 1
check('no tree trunk on a road', near_road == 0, '%d of %d' % (near_road, len(trees)))
in_building = 0
for b in trees:
    x = trunk(b)
    if any(place.overlap(x, y, 0.0) for y, _ in grid.near(x.c[0] - 8, x.c[1] - 8, x.c[0] + 8, x.c[1] + 8)): in_building += 1
check('no tree inside a building', in_building == 0, '%d' % in_building)
raised = Grid(40.0)
for r in roads:
    if r.get('bridge') or r.get('ys'):
        for a, e in zip(r['pts'], r['pts'][1:]):
            h = r['w'] / 2 + 1
            raised.add((a, e, h), min(a[0], e[0]) - h, min(a[1], e[1]) - h, max(a[0], e[0]) + h, max(a[1], e[1]) + h)
on_ramp = [b for b in trees if any(seg_point(a, e, pt(b))[0] < h for a, e, h in raised.near(pt(b)[0], pt(b)[1], pt(b)[0], pt(b)[1]))]
check('no tree on a ramp or under a deck', not on_ramp, '%d' % len(on_ramp))
touching = 0
for x, b in boxes:
    q = x.reach()
    if b['t'] != 'station' and any(place.seg_hits_box(a, e, x, h - 1) for a, e, h in raised.near(x.c[0] - q, x.c[1] - q, x.c[0] + q, x.c[1] + q)): touching += 1
check('no building touches a ramp or deck', touching == 0, '%d' % touching)
cut = 0
for r in roads:
    for p in (r['pts'][0], r['pts'][-1]):
        q = [(p[0] + a, p[1] + b) for a, b in ((0.5, 0), (-0.5, 0), (0, 0.5), (0, -0.5))]
        if not all(inside(x, gen.BOUNDARY) for x in q) and all(0 < c < m for c, m in ((p[0], 5000), (p[1], 3500))): cut += 1
check('no road runs off the edge of the world', cut == 0, '%d ends' % cut)
ra, rb = gen.air(-1.06, -26), gen.air(-1.06, 18)
ru = ((rb[0] - ra[0]) / dist(ra, rb), (rb[1] - ra[1]) / dist(ra, rb))
on_approach = [b for b in trees if abs((pt(b)[0] - ra[0]) * ru[1] - (pt(b)[1] - ra[1]) * ru[0]) < 130]
check('no tree on the runway line or its approaches', not on_approach, '%d' % len(on_approach))
field = [gen.air(-3.9, -26.1), gen.air(8.5, -26.1), gen.air(8.5, 18.1), gen.air(-3.9, 18.1)]
check('no tree on the airfield', not [b for b in trees if inside(pt(b), field) or inside(pt(b), gen.AIRPORT_Z)])
near_track = [b for b in trees if any(dist(pt(b), pt(t)) < 10 for t in tracks if abs(t['pose'][0] / 100 - pt(b)[0]) < 12)]
check('no tree by the railway', not near_track, '%d' % len(near_track))
land = gen.catmull(gen.PS(gen.COAST), 30, closed=True)[:-1]
wet = sum(1 for x, b in boxes if not all(inside(p, land) for p in x.corners()))
check('no building in the water', wet == 0, '%d' % wet)
# Rail and roads: only level crossings at grade; metro never meets a deck at its own height.
decks = [r for r in roads if r.get('bridge') and not r.get('ys')]
for r in decks:
    pass
gap = min(m['y'] - gen.DECK_Y for m in metro)
check('metro clears the flyover decks', gap - 2.45 > 3.5, '%.1f m under the metro girder' % (gap - 2.45))
walks = [r for r in roads if r['cls'] in ('trunk', 'primary', 'secondary') and not (r.get('bridge') or r.get('ys') or (r.get('nme') or '').endswith('slip'))]
check('footpaths both sides of major and secondary roads', all(r.get('sw') == 'both' for r in walks), '%d road pieces' % len(walks))
terrain = [open(os.path.join(out, 'terrain', e['id'] + '.bin'), 'rb').read() for e in world['chunks']]
check('terrain for every chunk', all(len(t) == 51 * 51 * 4 for t in terrain), '%d files' % len(terrain))
edge = 0
byc = {(c['ix'], c['iy']): t for c, t in zip(chunks, terrain)}
for (ix, iy), t in byc.items():
    e = byc.get((ix + 1, iy))
    if e:
        edge += sum(t[(50 + j * 51) * 4:(50 + j * 51) * 4 + 4] != e[(j * 51) * 4:(j * 51) * 4 + 4] for j in range(51))
check('terrain meets across chunk borders', edge == 0, '%d points differ' % edge)
# Parked vehicles and the railway's data (transport brief, 2026-11-06).
tr = json.load(open(os.path.join(out, 'transport.json')))
pk = tr['parked']
parked = [(pk['kinds'][k], e, n, t) for k, e, n, t in zip(pk['kind'], pk['east'], pk['north'], pk['turn'])]
counts = {}
for k, *_ in parked: counts[k] = counts.get(k, 0) + 1
print('     parked: %d %s' % (len(parked), sorted(counts.items())))
sizes = {'Boat': (4.2, 1.6), **parking.SIZES}
def spot(k, e, n, t):
    a = math.radians(t)
    return place.Box((e, n), (math.sin(a), math.cos(a)), sizes[k][0] / 2, sizes[k][1] / 2)
cars = [spot(*p) for p in parked if p[0] != 'Boat']
check('every parked vehicle is clear of the carriageways', not [x for x in cars if any(place.seg_hits_box(a, e, x, h) for a, e, h in segs.near(x.c[0] - 8, x.c[1] - 8, x.c[0] + 8, x.c[1] + 8))])
check('no parked vehicle inside a building', not [x for x in cars if any(place.overlap(x, y, 0.0) for y, _ in grid.near(x.c[0] - 12, x.c[1] - 12, x.c[0] + 12, x.c[1] + 12))])
check('no parked vehicle on a tree', not [x for x in cars if any(place.overlap(x, trunk(b), 0.0) for b in trees if abs(pt(b)[0] - x.c[0]) < 8 and abs(pt(b)[1] - x.c[1]) < 8)])
check('no parked vehicle on the railway', not [x for x in cars if any(dist(x.c, pt(t)) < 8 for t in tracks if abs(t['pose'][0] / 100 - x.c[0]) < 10)])
check('parked vehicles stand on land and boats on the water', all(inside(x.c, land) for x in cars) and all(not inside((p[1], p[2]), land) for p in parked if p[0] == 'Boat'))
paved = [pl['outer'] for c in chunks for pl in c['plazas']]
check('no boat on a quay or other paving', not [p for p in parked if p[0] == 'Boat' and any(inside((p[1], p[2]), q) for q in paved)])
rw = tr['railway']
check('the railway has its three stops, in order along the line', len(rw['stops']) == 3 and rw['stops'] == sorted(rw['stops']), str(rw['stopNames']))
check('the rail heads are read all along the line', rw['levels'] and max(rw['levels']) < 0.6 and min(rw['levels']) > 0.2, '%.2f to %.2f m at %d points' % (min(rw['levels']), max(rw['levels']), len(rw['levels'])))
print('     level crossings: %d' % len(rw['crossingX']))
print('world owner: %s' % world.get('owner'))
print('FAILED: ' + ', '.join(fails) if fails else 'all passed')
sys.exit(1 if fails else 0)
