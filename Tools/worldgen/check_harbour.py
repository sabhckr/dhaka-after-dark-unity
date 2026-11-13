#!/usr/bin/env python3
# Checks the waterfront from the written world, not from the generator's memory.   python3 -I Tools/worldgen/check_harbour.py [world folder]
import json, math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import dadc, gen
from geo import *
import harbour
here = os.path.dirname(os.path.abspath(__file__))
out = sys.argv[1] if len(sys.argv) > 1 else os.path.join(here, '../../Assets/StreamingAssets/World')
fails = []
def check(name, ok, note=''):
    print('%-4s %-46s %s' % ('ok' if ok else 'FAIL', name, note))
    if not ok: fails.append(name)
world = json.load(open(os.path.join(out, 'world.json')))
models = json.load(open(os.path.join(here, 'models.json')))['models']
chunks = {}
placed, roads = [], []
for f in os.listdir(os.path.join(out, 'chunks')):
    if not f.endswith('.dadc'): continue
    c = dadc.decode(open(os.path.join(out, 'chunks', f), 'rb').read())
    chunks[f[:-5]] = c
    placed += [b for b in c['buildings'] if b.get('pose')]
    e0, n0 = c['ix'] * 50000, -(c['iy'] + 1) * 50000
    for r in c.get('roads', []): roads.append([((e0 + p[0]) / 100, (n0 + p[1]) / 100) for p in r['pts']])
land = gen.catmull(gen.PS(gen.COAST), 30, closed=True)[:-1]
ships = [b for b in placed if b['mdl'] in ('vessel_container', 'vessel_liberty', 'vessel_bulk', 'vessel_green')]
transport = json.load(open(os.path.join(out, 'transport.json')))
pk = transport['parked']
models['boat'] = dict(size=harbour.BOAT_SIZE)
boats = [dict(mdl='boat', pose=[pk['east'][i] * 100, pk['north'][i] * 100, pk['height'][i] * 100, pk['turn'][i] * 100]) for i in range(len(pk['kind'])) if pk['kinds'][pk['kind'][i]] == 'Boat']
yards = [b for b in placed if b['mdl'] == 'vessel_yard']
check('ships from the folder at berths', len({b['mdl'] for b in ships}) == 4 and len(ships) >= 4, '%d ships, %d kinds' % (len(ships), len({b['mdl'] for b in ships})))
check('boats from the folder', len(boats) >= 10, '%d boats' % len(boats))
check('one shipyard', len(yards) == 1)
def box(b):
    m = models[b['mdl']]['size']
    t = math.radians(b['pose'][3] / 100)
    f = (math.sin(t), math.cos(t)) # Model +z.
    r = (f[1], -f[0])
    x, z = b['pose'][0] / 100, b['pose'][1] / 100
    return [(x + r[0] * m[0] / 2 * i + f[0] * m[2] / 2 * j, z + r[1] * m[0] / 2 * i + f[1] * m[2] / 2 * j) for i, j in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
def edge(c, step=6.0):
    o = []
    for p, q in zip(c, c[1:] + c[:1]):
        n = max(1, int(dist(p, q) / step))
        o += [(p[0] + (q[0] - p[0]) * i / n, p[1] + (q[1] - p[1]) * i / n) for i in range(n)]
    return o
docked = [b for b in ships if b['mdl'] in ('vessel_bulk', 'vessel_green')]
free = [b for b in ships if b not in docked]
wharves = [b for b in placed if b['mdl'] == 'quay_wharf']
cranes = [b for b in placed if b['mdl'] == 'port_crane']
afloat = free + boats + yards + wharves
boxes = [box(b) for b in afloat]
check('no hull on land', not any(inside(p, land) for c in boxes for p in edge(c)))
hit = 0
for i in range(len(boxes)):
    for j in range(i + 1, len(boxes)):
        if afloat[i]['mdl'] == afloat[j]['mdl'] == 'quay_wharf': continue # Lengths of wharf join end to end.
        if any(inside(p, boxes[j]) for p in edge(boxes[i])) or any(inside(p, boxes[i]) for p in edge(boxes[j])): hit += 1
check('no two vessels touch', hit == 0, '%d pairs' % hit)
wet = 0
for b in afloat:
    if abs(b['pose'][2] / 100 + harbour.DRAUGHT.get(b['mdl'], 0) - harbour.WATER_Y) > 0.02: wet += 1
for b in docked:
    if abs(b['pose'][2] / 100 + harbour.DRAUGHT[b['mdl']] - harbour.WATER_Y) > 0.02: wet += 1
check('every hull sits at its waterline', wet == 0, 'water at %.2f m' % harbour.WATER_Y)
check('the yard holds its two ships', all(inside(p, box(yards[0])) for b in docked for p in box(b)), '%d' % len(docked))
def along(a, b): # How far two headings differ, ends alike.
    d = abs(a - b) % 180
    return min(d, 180 - d)
square = 0
for b in free:
    w = min(wharves, key=lambda o: dist((o['pose'][0], o['pose'][1]), (b['pose'][0], b['pose'][1])))
    if along(b['pose'][3] / 100, w['pose'][3] / 100 + 90) > 0.1: square += 1
check('every ship lies parallel with its wharf', square == 0, '%d wharf lengths' % len(wharves))
stood = sum(1 for b in cranes if any(inside((b['pose'][0] / 100 + dx, b['pose'][1] / 100 + dz), box(o)) for o in wharves + yards for dx, dz in ((0.7, 0.3), (-0.7, -0.3)))) # A crane may stand over a joint.
check('every crane stands on a dock', stood == len(cranes), '%d of %d cranes' % (stood, len(cranes)))
reach = sum(1 for b in cranes if min(dist((b['pose'][0], b['pose'][1]), (o['pose'][0], o['pose'][1])) for o in ships) < 13000)
check('every crane is beside a ship', reach == len(cranes), '%d of %d' % (reach, len(cranes)))
through = sum(1 for r in roads for c in boxes if any(inside(p, c) for p in r))
check('no road through a vessel', through == 0, '%d' % through)
ground = [b for b in placed if b['mdl'] in models and b['t'] not in ('ship', 'boat', 'yard', 'quay', 'crane', 'grass', 'tree', 'track') and b.get('outer')]
near = 0
for c in boxes:
    cx, cz = sum(p[0] for p in c) / 4, sum(p[1] for p in c) / 4
    for b in ground:
        if abs(b['pose'][0] / 100 - cx) < 260 and abs(b['pose'][1] / 100 - cz) < 260 and any(inside(p, c) for p in edge(box(b), 3.0)): near += 1
check('no building in a vessel', near == 0, '%d' % near)
files = set(f[:-4] for f in os.listdir(os.path.join(out, 'water')) if f.endswith('.bin'))
need = {k for k, c in chunks.items() if c.get('water')}
check('water file for every chunk with water', need <= files, '%d files, %d chunks with water' % (len(files), len(need)))
bad = 0
for k in files:
    if os.path.getsize(os.path.join(out, 'water', k + '.bin')) != 51 * 51 * 2: bad += 1
check('water files the right size', bad == 0)
edges = 0
def cell(k): return k.split('_')[1:]
data = {k: open(os.path.join(out, 'water', k + '.bin'), 'rb').read() for k in files}
for k, b in data.items():
    x, y = (int(v) for v in cell(k))
    e = data.get('c_%d_%d' % (x + 1, y))
    if e: edges += sum(b[(50 + j * 51) * 2] != e[(j * 51) * 2] for j in range(51))
check('water meets across chunk borders', edges == 0, '%d points differ' % edges)
print('all passed' if not fails else 'FAILED: ' + ', '.join(fails))
sys.exit(1 if fails else 0)
