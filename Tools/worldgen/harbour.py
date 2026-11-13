#!/usr/bin/env python3
# The port's water side (owner's brief, waterfront and dockyard): the shipyard, the ships at their berths, the small boats, and the water's edge. Called from gen.py; nothing here moves a road, a rail or a building.
# Vessels are ordinary placed models, so they stream with their chunks, thin out with distance and draw instanced like everything else. They sit in the water by their draught.
import json, math, os
from geo import *
from place import Box, overlap

WATER_Y = 0.14                  # The surface WaterView draws, just over the ground layer's water at 0.12.
DRAUGHT = {'vessel_container': 10.9, 'vessel_liberty': 6.0} # From each hull's paint line. The yard's ships and walls come from port.json, which the model bake writes.
PORT = json.load(open(os.path.join(os.path.dirname(os.path.abspath(__file__)), 'port.json')))
DRAUGHT.update({p['model']: p['draught'] for p in PORT['ships']})
DRAUGHT.update({'vessel_yard': PORT['wall_foot'], 'quay_wharf': PORT['wall_foot'], 'quay_ramp': PORT['wall_foot']})
DECK_Y = WATER_Y + PORT['deck']
GREY = (200, 200, 200)
YARD_SHORE = ((1790.0, 148.0), (1771.0, 489.0))  # The straightest long shore on the bay's east side.
# Where each ship berths, as a point on the shore: the sheltered sides of the bay and the lee of the west port.
BERTHS = [('vessel_container', (1212, 446)), ('vessel_container', (843, 370)), ('vessel_liberty', (1501, 670)), ('vessel_liberty', (1682, 990)), ('vessel_liberty', (2180, 945))]
BOATS = [((2076, 1273), 6), ((2280, 1073), 5), ((1909, 1253), 4)]
FENDER = 2.5                    # Water between a hull and a wharf.
WHARF_GAP = 3.0                 # Water between a wharf and the shore, where it comes closest.
CRANE_BACK = 0.7                # The crane rails' middle, landward of a wharf's.
CRANES = {'vessel_container': (-0.22, 0.22), 'vessel_liberty': (0.0,)} # Along each ship, as parts of its length from the middle.
BOAT_PITCH = 7.5
BOAT_SIZE = (1.55, 1.41, 4.13)  # The owner's zodiac: width, height, length.

def shore_at(land, p, half):
    """The shore either side of the point nearest p: (point on the shore, direction along it, the side the water is on)."""
    n = len(land)
    i = min(range(n), key=lambda k: dist(land[k], p))
    def walk(step):
        at, k, left = land[i], i, half
        while left > 0:
            q = land[(k + step) % n]
            d = dist(at, q)
            if d >= left: return (at[0] + (q[0] - at[0]) * left / d, at[1] + (q[1] - at[1]) * left / d)
            at, k, left = q, (k + step) % n, left - d
        return at
    a, b = walk(-1), walk(1)
    l = dist(a, b)
    f = ((b[0] - a[0]) / l, (b[1] - a[1]) / l)
    w = (-f[1], f[0])
    mid = ((a[0] + b[0]) / 2, (a[1] + b[1]) / 2)
    if inside((land[i][0] + w[0] * 3, land[i][1] + w[1] * 3), land): w = (-w[0], -w[1])
    return mid, f, w

def outline(b, step=8.0):
    """Points round a box."""
    c = b.corners()
    out = []
    for p, q in zip(c, c[1:] + c[:1]):
        n = max(1, int(dist(p, q) / step))
        out += [(p[0] + (q[0] - p[0]) * i / n, p[1] + (q[1] - p[1]) * i / n) for i in range(n)]
    return out

def clear_of_land(land, b, gap):
    pts = outline(b)
    for p in pts:
        if inside(p, land): return False
    r = b.reach() + gap
    near = [(a, e) for a, e in zip(land, land[1:] + land[:1]) if dist(a, b.c) < r + 60]
    return all(seg_point(a, e, p)[0] >= gap for p in pts for a, e in near)

def in_bounds(b, boundary): return all(inside(p, boundary) for p in outline(b, 30.0))

def alongside(land, mid, f, w, half_len, half_beam, gap, boundary=None, taken=(), paved=()):
    """The box nearest the shore, along f, that clears it, and clears the paved quays built out over the water."""
    for off in range(0, 130):
        c = (mid[0] + w[0] * (off + half_beam), mid[1] + w[1] * (off + half_beam))
        b = Box(c, f, half_len, half_beam)
        if clear_of_land(land, b, gap) and all(clear_of_land(q, b, gap) for q in paved) and all(not overlap(b, o, 12.0) for o in taken): return b
    return None

def add_fixed(s, name, box, f, y, solid, kind):
    """A part of the port, all one colour: add() varies a tint, which would show from one length of wharf to the next."""
    s.add('port', name, box, f, 1.0, y=y, solid=solid, tint=GREY, mirror=False, kind=kind)
    s.out[-1]['tint'] = GREY

def ramps(s, land, back, along, w, half, count, done):
    """The way up from the land to a dock: at the back of it, where the shore is nearest and nothing stands. back is the dock's landward edge, w the way to the sea."""
    wide, long = PORT['ramp']
    spots = []
    for k in range(-int(half), int(half) + 1, 10):
        p = (back[0] + along[0] * k, back[1] + along[1] * k)
        gap = next((d for d in range(0, int(long)) if inside((p[0] - w[0] * d, p[1] - w[1] * d), land)), None)
        if gap is None or gap > long - 3: continue
        b = Box((p[0] - w[0] * long / 2, p[1] - w[1] * long / 2), w, long / 2, wide / 2)
        if s.site.road_clear(b) and s.site.free(b, 1.0): spots.append((gap, k, b))
    made = []
    for gap, k, b in sorted(spots, key=lambda x: x[:2]):
        if len(made) == count: break
        if any(abs(k - o) < 60 for o in made): continue
        add_fixed(s, 'quay_ramp', b, w, WATER_Y - PORT['wall_foot'], True, 'quay')
        s.site.stand(b)
        done.append(b)
        made.append(k)
    return len(made)

def place_vessels(s):
    """Everything afloat, after the buildings. The random stream is put back afterwards, so nothing generated later changes."""
    state = s.rng.getstate()
    land, m = s.city.land, s.models
    done = []
    foot = WATER_Y - PORT['wall_foot']
    # The yard: long side to the shore, its wide quay towards the land, its two ships and cranes in the places the scan gives.
    a, e = YARD_SHORE
    l = dist(a, e)
    t = ((e[0] - a[0]) / l, (e[1] - a[1]) / l)
    mid = ((a[0] + e[0]) / 2, (a[1] + e[1]) / 2)
    w = (-t[1], t[0])
    if inside((mid[0] + w[0] * 150, mid[1] + w[1] * 150), land): w = (-w[0], -w[1])
    size = m['vessel_yard']['size']
    yard = alongside(land, mid, t, w, size[0] / 2, size[2] / 2, WHARF_GAP)
    facing = (-w[0], -w[1]) # Model +z, the wide quay, towards the land.
    right = (facing[1], -facing[0])
    def on_yard(x, z): return (yard.c[0] + right[0] * x + facing[0] * z, yard.c[1] + right[1] * x + facing[1] * z)
    add_fixed(s, 'vessel_yard', yard, facing, foot, True, 'yard')
    done.append(yard)
    for p in PORT['ships']:
        sz = m[p['model']]['size']
        add_fixed(s, p['model'], Box(on_yard(p['x'], p['z']), right, sz[0] / 2, sz[2] / 2), facing, WATER_Y - p['draught'], False, 'ship')
    for x, z in PORT['cranes']: add_fixed(s, 'port_crane', Box(on_yard(x, z), right, 8.0, 8.0), w, DECK_Y, False, 'crane')
    built = ramps(s, land, on_yard(0, size[2] / 2), t, w, size[0] / 2 - 12, 2, done)
    length, wide = PORT['wharf']
    for name, hint in BERTHS:
        size = m[name]['size']
        lengths = math.ceil((size[2] + 20) / length)
        half = lengths * length / 2
        depth = wide + FENDER + size[0] + 1
        mid, f, w = shore_at(land, hint, half + 10)
        b = alongside(land, mid, f, w, half, depth / 2, WHARF_GAP, taken=done)
        if b is None: raise SystemExit('no berth for %s at %s' % (name, hint))
        back = (b.c[0] - w[0] * depth / 2, b.c[1] - w[1] * depth / 2)
        def out(d, along=0.0): return (back[0] + w[0] * d + f[0] * along, back[1] + w[1] * d + f[1] * along)
        for i in range(lengths): add_fixed(s, 'quay_wharf', Box(out(wide / 2, (i + 0.5) * length - half), f, length / 2, wide / 2), w, foot, True, 'quay')
        # Bow towards open water, as ships swing to leave.
        ship = out(wide + FENDER + size[0] / 2)
        sea = (1500.0 - ship[0], 200.0 - ship[1])
        bow = f if f[0] * sea[0] + f[1] * sea[1] >= 0 else (-f[0], -f[1])
        s.add('port', name, Box(ship, f, size[2] / 2, size[0] / 2), bow, 1.0, y=WATER_Y - DRAUGHT[name], solid=True, tint=s.rng.choice([(200, 200, 200), (196, 200, 204), (204, 200, 194)]), mirror=False, kind='ship')
        for part in CRANES[name]: add_fixed(s, 'port_crane', Box(out(wide / 2 - CRANE_BACK, part * size[2]), f, 8.0, 8.0), w, DECK_Y, False, 'crane')
        built += ramps(s, land, back, f, w, half - 8, 1, done)
        done.append(b)
    s.city.ramps = built
    s.city.boats = []
    for hint, count in BOATS:
        size = BOAT_SIZE
        mid, f, w = shore_at(land, hint, count * BOAT_PITCH / 2 + 4)
        # Not on a quay: it is paved out over the water, and the coastline alone (2026-11-06) put five boats on it.
        row = alongside(land, mid, f, w, count * BOAT_PITCH / 2, size[0] / 2 + 0.3, 1.5, taken=done, paved=s.city.quays)
        if row is None: raise SystemExit('no room for boats at %s' % (hint,))
        for i in range(count):
            at = (i + 0.5) * BOAT_PITCH - count * BOAT_PITCH / 2 + s.rng.uniform(-0.5, 0.5)
            c = (row.c[0] + f[0] * at, row.c[1] + f[1] * at)
            way = f if i % 2 else (-f[0], -f[1])
            s.city.boats.append(dict(kind='Boat', x=c[0], z=c[1], y=WATER_Y, turn=math.degrees(math.atan2(way[0], way[1])) % 360)) # A vehicle, not a model: the player can take it.
        done.append(row)
    s.rng.setstate(state)

def write_water(city, out, chunks):
    """The water's edge for WaterView: per chunk with any water, a lattice of the signed distance from the shore (metres, water positive, kept to -32..31.75) in one byte a point, at the terrain's 10 m points.
    A distance, unlike a mask, interpolates straight across a lattice, so the shoreline does not show the grid. Second byte: 255 where the shore is paved quay, so no wash is drawn on it."""
    NX, NZ, step, reach = 501, 351, 10.0, 32.0
    near = {}
    quay = {}
    land = city.land
    quads = city.quays
    for a, b in zip(land, land[1:] + land[:1]):
        for i in range(max(0, int((min(a[0], b[0]) - reach) // step)), min(NX - 1, int((max(a[0], b[0]) + reach) // step) + 1) + 1):
            for j in range(max(0, int((min(a[1], b[1]) - reach) // step)), min(NZ - 1, int((max(a[1], b[1]) + reach) // step) + 1) + 1):
                d, _, q = seg_point(a, b, (i * step, j * step))
                if d < near.get((i, j), reach): near[(i, j)] = d; quay[(i, j)] = q
    os.makedirs(os.path.join(out, 'water'), exist_ok=True)
    def code(i, j):
        p = (i * step, j * step)
        d = near.get((i, j), reach - 0.25)
        s = -d if inside(p, land) else d
        return max(0, min(255, round((s + 32) * 4)))
    def paved(i, j):
        q = quay.get((i, j))
        return 255 if q is not None and any(inside((q[0] + dx, q[1] + dz), z) for dx, dz in ((0, 0), (6, 0), (-6, 0), (0, 6), (0, -6)) for z in quads) else 0
    written = 0
    for cx, cz in chunks:
        b, wet = bytearray(), False
        for j in range(51):
            for i in range(51):
                x, z = min(NX - 1, cx * 50 + i), min(NZ - 1, cz * 50 + j)
                v = code(x, z)
                wet = wet or v > 100
                b.append(v)
                b.append(paved(x, z))
        if wet:
            with open(os.path.join(out, 'water', 'c_%d_%d.bin' % (cx, -(cz + 1))), 'wb') as f: f.write(b)
            written += 1
    return written
