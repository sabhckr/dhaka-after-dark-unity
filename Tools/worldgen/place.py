#!/usr/bin/env python3
# Stands the owner's models along the streets of the generated city (brief of 2026-11-06, phases 7 to 9). Each block is walked along its road edges first, so buildings front their streets, and its
# inside filled after. Every model is tried against the roads, footpaths, railway and metro, the block, the land and the buildings already standing, and goes in only if it clears them all.
# What a zone may use is in ZONE: slums take the mini buildings and nothing else, and no other zone takes them. Each building gets its own tint, turn, size and a coin toss for mirroring.
import json, math, os
from geo import *

HERE = os.path.dirname(os.path.abspath(__file__))
FOOT = {'path', 'footway', 'steps', 'pedestrian'}

# model: weight. gap: between neighbours along a street. back: set back from the footpath. row: spacing of the rows inside a block; none fills no inside. turn: most a building strays from its street, degrees.
TURN_RADIUS = 5.56 # Of the owner's curved track, on its centre line, at real size (Tools/models/city.py).

ZONE = {
    'hub': dict(models={'tower_glass': 34, 'tower_night': 14, 'asia_block': 28, 'office': 14, 'urban_block': 10}, size=(0.74, 1.3), gap=(1.5, 4), back=(0.4, 1.5), row=24, turn=0, big=['office', 'urban_block', 'urban_block', 'office'], bigshare=0.5),
    'rich': dict(models={'soviet_block': 26, 'asia_block': 24, 'urban_block': 12, 'office': 12, 'school': 3}, size=(0.9, 1.08), gap=(9, 16), back=(4, 8), row=52, turn=0, big=['urban_block', 'office', 'school'], bigshare=0.35),
    'slum': dict(models={'mini_barrack_a': 12, 'mini_barrack_b': 15, 'mini_barrack_c': 15, 'mini_barrack_d': 13, 'mini_barn_b': 11, 'mini_house_a': 13, 'mini_house_c': 15, 'mini_house_b': 4, 'mini_barn_a': 3},
                 size=(0.92, 1.16), gap=(0.5, 1.4), back=(0.1, 0.7), row=7.5, turn=6),
    'crime': dict(models={'abandoned': 40, 'soviet_block': 26, 'asia_block': 24, 'workshop': 4}, size=(0.72, 0.98), gap=(0.6, 2), back=(0.1, 0.5), row=15, turn=3),
    'port': dict(models={'warehouse': 66, 'workshop': 34}, size=(0.95, 1.25), gap=(7, 14), back=(3, 7), row=70, turn=0),
}
# Tints as r, g, b around 200, which leaves a picture as it is.
TINTS = {
    'hub': [(200, 202, 208), (190, 196, 204), (206, 202, 194), (182, 188, 196), (212, 208, 200), (196, 190, 184)],
    'rich': [(214, 206, 190), (206, 208, 200), (216, 200, 184), (196, 204, 200), (220, 214, 200), (204, 196, 186)],
    'slum': [(214, 178, 150), (172, 196, 186), (200, 170, 176), (214, 198, 150), (168, 180, 204), (196, 196, 170), (186, 160, 138), (206, 186, 172)],
    'crime': [(206, 198, 186), (196, 196, 190), (214, 200, 182), (188, 186, 180), (204, 200, 190), (198, 186, 172)],
    'port': [(190, 190, 186), (176, 180, 182), (196, 186, 170), (170, 170, 164), (186, 176, 164)],
}
# Pictures that came very dark: lifted so they read in daylight.
LIFT = {'abandoned': 1.3, 'tower_night': 1.25}
CONTAINER_SCALE = 1.18
CONTAINER_TINTS = [(205, 92, 70), (70, 112, 170), (70, 142, 110), (210, 170, 80), (170, 170, 170), (150, 90, 70), (96, 120, 130)]

class Box:
    __slots__ = ('c', 't', 'n', 'hx', 'hz')
    def __init__(s, c, t, hx, hz):
        s.c, s.t, s.hx, s.hz = c, t, hx, hz
        s.n = (-t[1], t[0])
    def corners(s):
        c, t, n, a, b = s.c, s.t, s.n, s.hx, s.hz
        return [(c[0] + t[0] * x * a + n[0] * z * b, c[1] + t[1] * x * a + n[1] * z * b) for x, z in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
    def reach(s): return math.hypot(s.hx, s.hz)

def overlap(a, b, gap=0.0):
    """Two rectangles, by separating axes, each grown by gap."""
    dx, dz = b.c[0] - a.c[0], b.c[1] - a.c[1]
    for ax, ha, hb in ((a.t, a.hx, None), (a.n, a.hz, None), (b.t, None, b.hx), (b.n, None, b.hz)):
        d = abs(dx * ax[0] + dz * ax[1])
        ra = a.hx * abs(a.t[0] * ax[0] + a.t[1] * ax[1]) + a.hz * abs(a.n[0] * ax[0] + a.n[1] * ax[1])
        rb = b.hx * abs(b.t[0] * ax[0] + b.t[1] * ax[1]) + b.hz * abs(b.n[0] * ax[0] + b.n[1] * ax[1])
        if d >= ra + rb + gap: return False
    return True

def seg_hits_box(p, q, b, grow):
    """Whether a segment comes within grow of a rectangle (a square-cornered grow, so a little cautious)."""
    px, pz = p[0] - b.c[0], p[1] - b.c[1]
    qx, qz = q[0] - b.c[0], q[1] - b.c[1]
    u0, v0 = px * b.t[0] + pz * b.t[1], px * b.n[0] + pz * b.n[1]
    u1, v1 = qx * b.t[0] + qz * b.t[1], qx * b.n[0] + qz * b.n[1]
    t0, t1 = 0.0, 1.0
    for s, d, h in ((u0, u1 - u0, b.hx + grow), (v0, v1 - v0, b.hz + grow)):
        if abs(d) < 1e-12:
            if abs(s) > h: return False
            continue
        a, c = (-h - s) / d, (h - s) / d
        if a > c: a, c = c, a
        t0, t1 = max(t0, a), min(t1, c)
        if t0 > t1: return False
    return True

class Site:
    """What stands in the way."""
    def __init__(s, city):
        s.segs, s.solid, s.over = Grid(40.0), Grid(40.0), Grid(40.0)
        s.land = city.land
        for w in city.ways:
            if w.kind in ('deck', 'ramp'):
                for a, b in zip(w.pts, w.pts[1:]):
                    k = w.w / 2 + 1.5
                    s.over.add((a, b, k), min(a[0], b[0]) - k, min(a[1], b[1]) - k, max(a[0], b[0]) + k, max(a[1], b[1]) + k)
                continue
            walk = 2.7 if w.cls in ('trunk', 'primary') else 2.2 if w.cls in ('secondary', 'tertiary') else 0.9 if w.cls not in FOOT else 0.2
            s.line(w.pts, w.w / 2 + walk)
        for r in city.rails: s.line(r, 5.5)
        s.line(city.metro, 6.5)
    def line(s, pts, clear):
        for i in range(len(pts) - 1):
            a, b = pts[i], pts[i + 1]
            s.segs.add((a, b, clear), min(a[0], b[0]) - clear, min(a[1], b[1]) - clear, max(a[0], b[0]) + clear, max(a[1], b[1]) + clear)
    def road_clear(s, b):
        r = b.reach()
        for a, e, clear in s.segs.near(b.c[0] - r, b.c[1] - r, b.c[0] + r, b.c[1] + r):
            if seg_hits_box(a, e, b, clear): return False
        for a, e, clear in s.over.near(b.c[0] - r, b.c[1] - r, b.c[0] + r, b.c[1] + r):
            if seg_hits_box(a, e, b, clear): return False
        return True
    def free(s, b, gap):
        r = b.reach() + gap
        for o in s.solid.near(b.c[0] - r, b.c[1] - r, b.c[0] + r, b.c[1] + r):
            if overlap(b, o, gap): return False
        return True
    def stand(s, b):
        r = b.reach()
        s.solid.add(b, b.c[0] - r, b.c[1] - r, b.c[0] + r, b.c[1] + r)

def walk_edge(poly, step):
    """Along a closed outline: (point, direction) every step metres, anticlockwise, so the inside is on the left."""
    n = len(poly)
    run = 0.0
    for i in range(n):
        a, b = poly[i], poly[(i + 1) % n]
        l = dist(a, b)
        if l < 1e-6: continue
        d = ((b[0] - a[0]) / l, (b[1] - a[1]) / l)
        while run <= l:
            yield (a[0] + d[0] * run, a[1] + d[1] * run), d, i
            run += step
        run -= l

def turn_of(f): return math.degrees(math.atan2(f[0], f[1])) % 360

class Placer:
    def __init__(s, city, rng, models, zone_at):
        s.city, s.rng, s.models, s.zone_at = city, rng, models, zone_at
        s.coastal = False
        s.site = Site(city)
        s.out = []
        s.counts = {}
    def allowed(s, zone, b):
        """The zone lines and the roads do not quite agree, so a block can reach over a line: the slums keep their rule either way, and nothing stands in the water."""
        at = s.zone_at(b.c)
        if (zone == 'slum') != (at == 'slum') and at is not None: return False
        return not s.coastal or all(inside(q, s.city.land) for q in b.corners())
    def pick(s, zone, last):
        z = ZONE[zone]
        names = [m for m in z['models'] if m != last or len(z['models']) == 1]
        return s.rng.choices(names, [z['models'][m] for m in names])[0]
    def add(s, zone, name, box, f, size, y=0.0, solid=True, tint=None, mirror=None, kind=None):
        r = s.rng
        m = s.models[name]
        mirrored = (r.random() < 0.5) if mirror is None else mirror
        tint = tint or r.choice(TINTS[zone])
        tint = tuple(max(0, min(255, int(c * LIFT.get(name, 1)) + r.randint(-6, 6))) for c in tint)
        s.out.append(dict(mdl=name, x=box.c[0], z=box.c[1], y=y, turn=turn_of(f), size=size, mirror=mirrored, tint=tint, h=m['size'][1] * size, t=kind or m['use'], outer=box.corners() if solid else [], zone=zone))
        s.counts[zone] = s.counts.get(zone, 0) + 1
    def try_front(s, zone, poly, p, d, last):
        """One building on the inside of an edge at p: the first size and way round that fits. Returns how far along it took, and its name."""
        r = s.rng
        z = ZONE[zone]
        n = (-d[1], d[0])
        for _ in range(4):
            name = s.pick(zone, last)
            m = s.models[name]['size']
            size = r.uniform(*z['size'])
            sx, sz = m[0] * size, m[2] * size
            side = r.random() < (0.75 if sx < sz else 0.25)  # Long side to the street, mostly, so a block closes up.
            front, deep = (sz, sx) if side else (sx, sz)
            jitter = math.radians(r.uniform(-z['turn'], z['turn'])) if z['turn'] else 0.0
            t = (d[0] * math.cos(jitter) - d[1] * math.sin(jitter), d[0] * math.sin(jitter) + d[1] * math.cos(jitter))
            back = r.uniform(*z['back'])
            for k in range(10):
                c = (p[0] + d[0] * front / 2 + n[0] * (back + deep / 2 + k * 1.5), p[1] + d[1] * front / 2 + n[1] * (back + deep / 2 + k * 1.5))
                b = Box(c, t, front / 2, deep / 2)
                if s.site.road_clear(b): break
            else: continue
            if not all(inside(q, poly) for q in b.corners()) or not s.allowed(zone, b): continue
            if not s.site.free(b, r.uniform(*z['gap']) * 0.5): continue
            nn = (-t[1], t[0])
            f = (t if side else nn)
            if r.random() < 0.5: f = (-f[0], -f[1])
            s.add(zone, name, b, f, size)
            s.site.stand(b)
            return front, name
        return None, last
    def frontage(s, zone, poly):
        z = ZONE[zone]
        last = None
        skip = 0.0
        step = 2.5
        for p, d, _ in walk_edge(poly, step):
            if skip > 0:
                skip -= step
                continue
            used, last = s.try_front(zone, poly, p, d, last)
            if used: skip = used + s.rng.uniform(*z['gap'])
    def inner(s, zone, poly):
        z = ZONE[zone]
        if not z['row']: return
        u = main_dirn(poly)
        v = (-u[1], u[0])
        lo, hi = extent(poly, u)
        wlo, whi = extent(poly, v)
        r = s.rng
        row = wlo + z['row'] * 0.5
        last = None
        while row < whi:
            at = lo + r.uniform(0, 3)
            while at < hi:
                p = (u[0] * at + v[0] * row, u[1] * at + v[1] * row)
                used = None
                if inside(p, poly):
                    used, last = s.try_inner(zone, poly, p, u, last)
                at += (used + r.uniform(*z['gap'])) if used else 3.5
            row += z['row'] * r.uniform(0.9, 1.1)
    def try_inner(s, zone, poly, p, u, last):
        r = s.rng
        z = ZONE[zone]
        name = s.pick(zone, last)
        m = s.models[name]['size']
        size = r.uniform(*z['size'])
        front, deep = m[0] * size, m[2] * size
        jitter = math.radians(r.uniform(-z['turn'] * 1.6, z['turn'] * 1.6)) if z['turn'] else 0.0
        t = (u[0] * math.cos(jitter) - u[1] * math.sin(jitter), u[0] * math.sin(jitter) + u[1] * math.cos(jitter))
        b = Box(p, t, front / 2, deep / 2)
        if not all(inside(q, poly) for q in b.corners()) or not s.allowed(zone, b): return None, last
        if not s.site.road_clear(b) or not s.site.free(b, r.uniform(*z['gap']) * 0.5): return None, last
        f = (-t[1], t[0])
        if r.random() < 0.5: f = (-f[0], -f[1])
        s.add(zone, name, b, f, size)
        s.site.stand(b)
        return front, name
    def big(s, zone, poly):
        """The largest models need a block to themselves, so they go first, in the middle, while a block is empty: towers and offices in the hub, and so on. Most blocks stay for the street walk."""
        r = s.rng
        z = ZONE[zone]
        wants = z.get('big')
        if not wants or r.random() > z['bigshare']: return
        c = centroid(poly)
        if not inside(c, poly): return
        u = main_dirn(poly)
        n = (-u[1], u[0])
        for name in r.sample(wants, len(wants)):
            m = s.models[name]['size']
            size = r.uniform(*z['size'])
            for t, f, a, b in ((u, n, m[0], m[2]), (n, u, m[2], m[0])):
                box = Box(c, t, a * size / 2, b * size / 2)
                if all(inside(q, poly) for q in box.corners()) and s.allowed(zone, box) and s.site.road_clear(box) and s.site.free(box, 2.0):
                    s.add(zone, name, box, f if r.random() < 0.5 else (-f[0], -f[1]), size)
                    s.site.stand(box)
                    return
    def quays(s):
        """The port's waterfront: the land between the Port Ring and the water, in lengths, each paved, with sheds on the edge and containers behind."""
        land, r = s.city.land, s.rng
        out = []
        run = []
        n = len(land)
        def close():
            nonlocal run
            if len(run) >= 2:
                a, b = run[0], run[-1]
                if dist(a, b) > 60:
                    l = left_of(a, b)
                    quad = [a, b, (b[0] + l[0] * 110, b[1] + l[1] * 110), (a[0] + l[0] * 110, a[1] + l[1] * 110)]
                    out.append(quad)
                    s.coastal = True
                    s.frontage('port', quad)
                    s.yard(quad, True)
            run = []
        for i in range(n):
            p = land[i]
            if s.zone_at(p) == 'port' or s.zone_at((p[0] + 0, p[1] + 60)) == 'port':
                run.append(p)
                if length(run) > 170: 
                    q = run[-1]
                    close()
                    run = [q]
            else: close()
        close()
        s.city.quays = out
    def rails(s):
        """The railway in the owner's track. Each length starts exactly where the last one ended and points at the line further on, so the rails meet all the way and the alignment stays. Where a siding leaves the main line a cut of their curved track takes it off.
        Over a road the track sinks until its rail heads are level with the surface."""
        L = s.models['rail_long']['size'][2]
        main = s.city.rails[0]
        def lay(cur, q):
            f = ((q[0] - cur[0]) / L, (q[1] - cur[1]) / L)
            f = (f[0] / math.hypot(*f), f[1] / math.hypot(*f))
            p = ((cur[0] + q[0]) / 2, (cur[1] + q[1]) / 2)
            b = Box(p, f, 2.0, 2.0)
            on_road = any(seg_hits_box(a, e, b, c - 2.0) for a, e, c in s.road_segs(p))
            s.add('port', 'rail_long', Box(p, f, 1.5, L / 2), f, 1.0, y=-0.16 if on_road else 0.05, solid=False, tint=(200, 200, 200), mirror=False, kind='track')
        for n, line in enumerate(s.city.rails):
            cur, at, total = line[0], 0.0, length(line)
            if n:
                cur, at = s.turnout(line, main)
            while True:
                while at < total and dist(cur, s.point(line, at)[0]) < L: at += 0.25
                if at >= total: break
                q = s.point(line, at)[0]
                k = L / dist(cur, q)
                q = (cur[0] + (q[0] - cur[0]) * k, cur[1] + (q[1] - cur[1]) * k)
                lay(cur, q)
                cur = q
    def turnout(s, line, main):
        """Where a siding leaves the main line: the curve that takes it from the main line's heading to its own. Returns where the curve ends and how far along the siding that is."""
        j = line[0]
        i = min(range(len(main) - 1), key=lambda i: seg_point(main[i], main[i + 1], j)[0])
        h = math.atan2(main[i + 1][1] - main[i][1], main[i + 1][0] - main[i][0])
        q = s.point(line, 40.0)[0]
        turn = (math.atan2(q[1] - j[1], q[0] - j[0]) - h + math.pi) % math.tau - math.pi
        name, arc = ('rail_turn_21', 20.7) if abs(turn) > math.radians(15) else ('rail_turn_11', 11.0)
        f = (math.cos(h), math.sin(h))
        s.add('port', name, Box(j, f, 1.5, 2.0), f, 1.0, y=0.05, solid=False, tint=(200, 200, 200), mirror=turn < 0, kind='track')
        a = math.radians(arc)
        side = 1 if turn > 0 else -1
        left = (-f[1], f[0])
        ahead, across = TURN_RADIUS * math.sin(a), TURN_RADIUS * (1 - math.cos(a)) * side
        return (j[0] + f[0] * ahead + left[0] * across, j[1] + f[1] * ahead + left[1] * across), ahead
    def road_segs(s, p):
        """The roads near a point, as (a, b, half width + 2): the railway's own clearance lines are left out."""
        for a, e, c in s.site.segs.near(p[0] - 12, p[1] - 12, p[0] + 12, p[1] + 12):
            if c != 5.5 and c != 6.5: yield a, e, c - 2.7 + 2.0
    @staticmethod
    def point(line, at):
        for i in range(len(line) - 1):
            l = dist(line[i], line[i + 1])
            if at <= l or i == len(line) - 2:
                t = at / l if l else 0
                a, b = line[i], line[i + 1]
                return (a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t), ((b[0] - a[0]) / (l or 1), (b[1] - a[1]) / (l or 1))
            at -= l
    def grass(s):
        """The owner's grass cards where people would see grass: verges of roads in the rich quarter and through open land, the rich quarter's lawns, and meadows of the land nobody built on. Never on anything paved, and not in the port or on the sand."""
        r = s.rng
        spots = []
        for w in s.city.ways:
            if w.kind in ('deck', 'ramp') or w.cls not in ('primary', 'secondary', 'tertiary', 'trunk'): continue
            for side in (1, -1):
                line = offset(w.pts, side * (w.w / 2 + 7))
                at, total = r.uniform(0, 12), length(line)
                while at < total:
                    spots.append(s.point(line, at)[0])
                    at += r.uniform(9, 15)
        for x in range(0, 5000, 18):
            for z in range(0, 3500, 18):
                spots.append((x + r.uniform(-7, 7), z + r.uniform(-7, 7)))
        r.shuffle(spots)
        coast = s.city.land[::3]
        n = 0
        for p in spots:
            if n >= 9000: break
            if s.zone_at(p) not in ('rich', None) or not inside(p, s.city.land) or inside(p, s.city.airport): continue
            if s.paved(p) or s.under_raised(p, 3.0) or any(abs(q[0] - p[0]) < 70 and dist(p, q) < 70 for q in coast): continue
            b = Box(p, (1, 0), 3.0, 3.0)
            if s.site.road_clear(b) and s.site.free(b, 0.0):
                a = r.uniform(0, math.tau)
                s.add('rich', 'grass_patch', b, (math.sin(a), math.cos(a)), r.uniform(0.55, 0.8), y=0.05, solid=False, tint=(200, 205, 190), kind='grass')
                n += 1
        return n
    def under_raised(s, p, reach):
        return any(seg_point(a, b, p)[0] < k + reach for a, b, k in s.raised.near(p[0] - reach, p[1] - reach, p[0] + reach, p[1] + reach))
    def paved(s, p):
        """Inside a block of a kind that is paved over or left as earth rather than the rich quarter's lawns."""
        for poly, zone in s.cells.near(p[0], p[1], p[0], p[1]):
            if inside(p, poly): return zone != 'rich'
        return False
    def trees(s):
        """The owner's trees, by what each district is. Rows stand behind the pavement, clear of junctions and of the way into a building; the rest grow in clumps of one kind, so nothing reads as a pattern.
        Hub: a few small ones. Rich: rows and gardens. Slums and crime: odd single trees, kept out of the gullies. Port: a few along the main roads, clear of freight and rail. Airport: the outer edge only, never under the runway's approach."""
        r = s.rng
        cells = s.cells = Grid(120.0)
        for poly, zone in s.city.blocks:
            x = [p[0] for p in poly]; z = [p[1] for p in poly]
            cells.add((poly, zone), min(x), min(z), max(x), max(z))
        s.city.trees = [] # The street kit's drawn trees: the owner's models stand in their place.
        coast = s.city.land[::3]
        rails = Grid(40.0)
        for line in s.city.rails:
            for a, b in zip(line, line[1:]): rails.add((a, b), min(a[0], b[0]) - 14, min(a[1], b[1]) - 14, max(a[0], b[0]) + 14, max(a[1], b[1]) + 14)
        raised = Grid(40.0) # Ramps, decks and slips are left out of the site's road lines, as buildings may stand under a deck. A tree may not, and nothing stands on a ramp.
        for w in s.city.ways:
            if w.kind in ('deck', 'ramp', 'slip') or (w.ys and w.kind != 'deck'):
                for a, b in zip(w.pts, w.pts[1:]):
                    k = w.w / 2 + 3
                    raised.add((a, b, k), min(a[0], b[0]) - k, min(a[1], b[1]) - k, max(a[0], b[0]) + k, max(a[1], b[1]) + k)
        s.raised = raised
        ra, rb = s.city.runway
        ru = ((rb[0] - ra[0]) / dist(ra, rb), (rb[1] - ra[1]) / dist(ra, rb))
        counts = {}
        def block_of(p):
            for poly, zone in cells.near(p[0], p[1], p[0], p[1]):
                if inside(p, poly): return poly, zone
            return None, None
        def stand(zone, p, name, scale, tint, road_gap=0.5, gap=0.5):
            """One tree here if it fits: its trunk off every road, pavement, rail and metro pier, its crown out of every building."""
            m = s.models[name]['size']
            crown = min(m[0], m[2]) * scale * 0.32
            if not inside(p, s.city.land) or inside(p, s.city.airport) or inside(p, s.city.air_rect): return False
            if abs((p[0] - ra[0]) * ru[1] - (p[1] - ra[1]) * ru[0]) < 140: return False # The runway's line, both ways out.
            if any(abs(q[0] - p[0]) < 70 and dist(p, q) < 70 for q in coast): return False
            if any(seg_point(a, b, p)[0] < 13 for a, b in rails.near(p[0], p[1], p[0], p[1])): return False
            for quad in s.city.quays:
                if inside(p, quad): return False
            if not s.site.road_clear(Box(p, (1, 0), road_gap, road_gap)): return False
            if s.under_raised(p, crown): return False
            top = Box(p, (1, 0), crown, crown)
            if not s.site.free(top, gap): return False
            s.site.stand(top)
            a = r.uniform(0, math.tau)
            s.add(zone, name, Box(p, (1, 0), 0.3 * scale + 0.1, 0.3 * scale + 0.1), (math.sin(a), math.cos(a)), scale, y=0.0, solid=True, tint=tuple(c + r.randint(-8, 8) for c in tint), kind='tree')
            counts[zone] = counts.get(zone, 0) + 1
            return True
        def clump(zone, c, n, radius, kinds, sizes, tint, within=None, **k):
            kind = r.choice(kinds)
            got = 0
            for _ in range(n * 3):
                if got == n: break
                p = (c[0] + r.gauss(0, radius), c[1] + r.gauss(0, radius))
                if within and not inside(p, within): continue
                if stand(zone, p, kind if r.random() < 0.8 else r.choice(kinds), r.uniform(*sizes), tint, **k): got += 1
            return got
        def row(zone, classes, steps, kinds, sizes, tint, limit):
            """Along pavements, in runs of one kind with breaks between them."""
            n = 0
            ways = [w for w in s.city.ways if w.cls in classes and w.kind not in ('deck', 'ramp', 'slip')]
            r.shuffle(ways)
            for w in ways:
                walk = 2.7 if w.cls in ('trunk', 'primary') else 2.2 if w.cls in ('secondary', 'tertiary') else 0.9
                for side in (1, -1):
                    line = offset(w.pts, side * (w.w / 2 + walk + 1.3))
                    total = length(line)
                    at = 17 + r.uniform(0, steps[0])
                    kind, left = r.choice(kinds), r.randint(3, 7)
                    while at < total - 17 and n < limit:
                        p, d = s.point(line, at)
                        at += r.uniform(*steps)
                        if s.zone_at(p) != zone: continue
                        left -= 1
                        if left <= 0:
                            kind, left = r.choice(kinds), r.randint(3, 7)
                            at += r.uniform(*steps) * r.choice((0, 1, 2))
                        door = False
                        for o in s.site.solid.near(p[0] - 30, p[1] - 30, p[0] + 30, p[1] + 30):
                            along = (o.c[0] - p[0]) * d[0] + (o.c[1] - p[1]) * d[1]
                            if abs(along) < 3.5 and o.hx > 6: door = True # Straight in front of a building: that is where its way in is.
                        if not door and stand(zone, p, kind, r.uniform(*sizes), tint): n += 1
            return n
        big = ['tree_oak', 'tree_broad_a', 'tree_broad_b', 'tree_low_a', 'tree_low_b']
        small = ['tree_low_a', 'tree_low_b']
        green = (195, 210, 190)
        row('rich', ('primary', 'secondary', 'tertiary', 'residential', 'living_street'), (11, 22), big, (0.7, 1.05), green, 1500)
        for poly, zone in s.city.blocks:
            if zone == 'rich':
                for _ in range(max(1, min(5, int(abs(area(poly)) / 6000)))):
                    c = poly[r.randrange(len(poly))]
                    m = centroid(poly)
                    clump('rich', ((c[0] + 2 * m[0]) / 3, (c[1] + 2 * m[1]) / 3), r.randint(2, 5), 9, big, (0.7, 1.15), green, within=poly)
            elif zone == 'hub' and abs(area(poly)) > 3500 and r.random() < 0.25:
                clump('hub', centroid(poly), r.randint(2, 3), 6, small, (0.6, 0.85), (190, 205, 185), within=poly)
            elif zone == 'slum' and r.random() < 0.2 and counts.get('slum', 0) < 150:
                clump('slum', centroid(poly), 1, 5, small, (0.45, 0.7), (190, 190, 165), within=poly, road_gap=1.6, gap=1.0)
            elif zone == 'crime' and r.random() < 0.3 and counts.get('crime', 0) < 200:
                clump('crime', centroid(poly), r.randint(1, 2), 10, small + ['tree_broad_a'], (0.55, 0.85), (205, 195, 160), within=poly, gap=0.8)
        row('hub', ('primary', 'secondary'), (34, 70), small, (0.6, 0.85), (190, 205, 185), 150)
        row('port', ('trunk', 'primary'), (60, 120), small, (0.6, 0.85), (200, 200, 170), 60)
        ring = list(s.city.airport) + [s.city.airport[0]]
        edge = offset(ring, -32 if area(s.city.airport) > 0 else 32)
        at, total = r.uniform(0, 30), length(edge)
        while at < total:
            clump('air', s.point(edge, at)[0], r.randint(2, 4), 9, big, (0.7, 1.0), green)
            at += r.uniform(30, 75)
        for _ in range(4000):
            p = (r.uniform(0, 5000), r.uniform(0, 3500))
            if s.zone_at(p) is None and block_of(p)[0] is None and counts.get(None, 0) < 1900:
                clump(None, p, r.randint(3, 9), 20, big, (0.7, 1.2), (198, 208, 188))
        s.tree_counts = {k or 'open land': v for k, v in counts.items()}
    def yard(s, poly, always=False):
        """Container stacks in what the warehouses left of a port block."""
        r = s.rng
        if not always and r.random() > 0.3: return
        u = main_dirn(poly)
        v = (-u[1], u[0])
        lo, hi = extent(poly, u)
        wlo, whi = extent(poly, v)
        m = s.models['container_b']['size']
        k = 1.3     # The grid's pitch. Drawn at CONTAINER_SCALE: 7.2 x 2.44 x 2.44 m, a box's real width. The grid stays as it was so that nothing else in the city moves.
        L, W, H = m[0] * k, m[2] * k, m[1] * CONTAINER_SCALE
        row = wlo + 8
        while row < whi - 6:
            at = lo + 8
            while at < hi - 6:
                p = (u[0] * at + v[0] * row, u[1] * at + v[1] * row)
                b = Box(p, u, L / 2, W / 2)
                if inside(p, poly) and r.random() < 0.55 and all(inside(q, poly) for q in b.corners()) and s.allowed('port', b) and s.site.road_clear(b) and s.site.free(b, 0.6):
                    s.site.stand(b)
                    for level in range(r.choice((1, 1, 2, 2, 3))):
                        s.add('port', 'container_b', Box(p, u, m[0] * CONTAINER_SCALE / 2, m[2] * CONTAINER_SCALE / 2), v, CONTAINER_SCALE, y=0.2 + level * H, solid=level == 0, tint=r.choice(CONTAINER_TINTS), kind='container')
                at += L + 0.7
            row += W + (0.6 if int(row) % 3 else 9.0)
    def run(s):
        for poly, zone in sorted(s.city.blocks, key=lambda b: (b[1], round(b[0][0][0], 2), round(b[0][0][1], 2))):
            s.coastal = any(not inside(p, s.city.land) for p in poly[::3]) or any(dist(p, q) < 120 for p in poly[::6] for q in s.city.land[::4])
            s.big(zone, poly)
            s.frontage(zone, poly)
            s.inner(zone, poly)
            if zone == 'port': s.yard(poly)
        s.quays()
        s.rails()
        s.trees()
        s.grass()
        return s.out

def main_dirn(poly):
    bins = [0.0] * 36
    for i in range(len(poly)):
        a, b = poly[i], poly[(i + 1) % len(poly)]
        t = math.atan2(b[1] - a[1], b[0] - a[0]) % math.pi
        bins[min(35, int(t / math.pi * 36))] += dist(a, b)
    k = max(range(36), key=lambda i: bins[i] + 0.5 * (bins[i - 1] + bins[(i + 1) % 36]))
    t = (k + 0.5) / 36 * math.pi
    return (math.cos(t), math.sin(t))

def load_models():
    with open(os.path.join(HERE, 'models.json')) as f: return json.load(f)['models']

def report(placed):
    r = {}
    real = [b for b in placed if b['t'] not in ('container', 'station', 'prop', 'track', 'grass', 'tree', 'ship', 'boat', 'yard', 'quay', 'crane', 'train')]
    r['grass_patches'] = sum(1 for b in placed if b['t'] == 'grass')
    r['trees'] = sum(1 for b in placed if b['t'] == 'tree')
    r['buildings'] = len(real)
    r['mirrored_share'] = round(sum(1 for b in real if b['mirror']) / max(1, len(real)), 3)
    by = {}
    for b in real: by.setdefault(b['zone'], {}).setdefault(b['mdl'], 0); by[b['zone']][b['mdl']] += 1
    r['by_zone'] = by
    r['minis_outside_slums'] = sum(1 for b in real if b['mdl'].startswith('mini') and b['zone'] != 'slum')
    r['large_in_slums'] = sum(1 for b in real if b['zone'] == 'slum' and not b['mdl'].startswith('mini'))
    r['containers'] = sum(1 for b in placed if b['t'] == 'container')
    return r
