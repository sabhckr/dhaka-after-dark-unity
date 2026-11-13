#!/usr/bin/env python3
# The city, generated. Owner's brief of 2026-11-06: one new map of exactly 15.5 km2, laid out to the owner's zone map (five zones and an airport, highways, four flyovers, one metro line,
# a railway to the port). Writes the DADC chunks the game has always read, so roads, lanes, traffic, collision and streaming stay the systems already tested; only the data is new.
#
# The world is a 10 x 7 grid of 500 m chunks less eight: 62 x 0.25 km2 = 15.5 km2 exactly, water included. Everything hand-placed below is in pixels of the owner's map, through P(), so it can
# be checked against that picture. In order: the coast; the roads drawn by hand; the faces between them; each face cut into blocks by the street level its zone asks for; every crossing and
# T made into a graph node; then flyovers, metro, railway, airport and port on top. Same seed, same city, byte for byte. Standard library only:
#   python3 -I Tools/worldgen/gen.py <out dir> [seed]
import hashlib, json, math, os, random, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import dadc, harbour, parking, place
from geo import *

CELL, GRID_X, GRID_Z = 500.0, 10, 7
MISSING = {(0, 6), (1, 6), (2, 6), (0, 5), (1, 5), (0, 4), (9, 6), (9, 5)}
BOUNDARY = [(0, 0), (5000, 0), (5000, 2500), (4500, 2500), (4500, 3500), (1500, 3500), (1500, 3000), (1000, 3000), (1000, 2500), (500, 2500), (500, 2000), (0, 2000)]
PIXEL = 4.72
ROAD_Y = 0.28
DECK_Y = 7.5    # Flyover deck surface: girder and cap under it still leave a truck 5.5 m.
METRO_Y = 14.0  # Clear of any flyover.

def P(x, y): return ((x - 60) * PIXEL, (745 - y) * PIXEL)
def PS(pts): return [P(*p) for p in pts]

# Width, lanes. Lanes None leaves it to RoadMarkings' rule for the class.
CLASSES = {
    'trunk': (10.5, 3), 'primary': (14.0, 4), 'secondary': (9.0, 2), 'tertiary': (7.0, 2),
    'residential': (5.0, None), 'living_street': (4.5, None), 'service': (3.5, None), 'path': (2.0, None),
}
FOOT = {'path', 'footway', 'steps', 'pedestrian'}
RANK = {'trunk': 6, 'primary': 5, 'secondary': 4, 'tertiary': 3, 'residential': 2, 'living_street': 2, 'service': 1, 'path': 0}

# The five zones of the brief. block: the area, m2, a block stops being cut at. finest: the least street allowed. green: no streets, no buildings.
ZONES = {
    'hub':   dict(name='Central Commercial Hub', block=10500, finest='residential', wiggle=0.6),
    'rich':  dict(name='Rich Residential Area', block=13000, finest='residential', wiggle=0.3),
    'slum':  dict(name='Slum Areas', block=850, finest='path', wiggle=2.2),
    'crime': dict(name='Crime Area', block=1500, finest='service', wiggle=1.8),
    'port':  dict(name='Port Area', block=16000, finest='residential', wiggle=0.3),
}
# Zone outlines on the owner's map. Anything in none of them is green land.
AIRPORT = [(925, 445), (1055, 445), (1098, 505), (1060, 630), (880, 722), (842, 722), (818, 690), (828, 605), (900, 512)]
RICH_LOOP = [(742, 205), (770, 150), (800, 90), (850, 42), (930, 30), (1000, 50), (1040, 105), (1045, 165), (1010, 220), (950, 250), (880, 262), (800, 250), (760, 232)]
# The zone reaches past the world's east edge there; the highway round it must not, or the ring is cut at the edge of the map.
RICH_ROAD = [(742, 205), (770, 150), (800, 90), (850, 42), (930, 40), (966, 52), (982, 105), (984, 165), (1002, 216), (950, 250), (880, 262), (800, 250), (760, 232)]
SLUM = [(668, 436), (690, 318), (800, 252), (1010, 225), (1045, 340), (990, 408), (935, 452), (860, 440), (760, 418)]
CRIME = [(668, 436), (760, 418), (860, 440), (935, 452), (925, 462), (868, 522), (812, 585), (770, 640), (730, 570), (690, 490)]
PORT = [(60, 420), (240, 425), (330, 480), (412, 446), (470, 452), (560, 452), (625, 440), (668, 436), (690, 490), (730, 570), (770, 640), (795, 745), (60, 745)]
HUB = [(412, 446), (392, 405), (372, 368), (350, 320), (340, 262), (352, 196), (392, 128), (448, 78), (540, 48), (640, 46), (705, 66), (748, 100), (786, 128), (742, 205), (760, 232), (800, 252), (690, 318), (668, 436), (625, 440), (560, 452), (470, 452)]

def zone_at(p):
    for name, poly in (('rich', RICH_Z), ('slum', SLUM_Z), ('crime', CRIME_Z), ('port', PORT_Z), ('hub', HUB_Z)):
        if inside(p, poly): return name
    return None

RICH_Z, SLUM_Z, CRIME_Z, PORT_Z, HUB_Z, AIRPORT_Z = PS(RICH_LOOP), PS(SLUM), PS(CRIME), PS(PORT), PS(HUB), PS(AIRPORT)

class Way:
    __slots__ = ('id', 'cls', 'w', 'ln', 'pts', 'oneway', 'name', 'y', 'ys', 'bridge', 'kind', 'meet')
    def __init__(s, cls, pts, name=None, oneway=0, w=None, ln=None, kind=None, meet=(None, None)):
        s.id, s.cls, s.pts, s.oneway, s.name, s.kind, s.meet = 0, cls, list(pts), oneway, name, kind, meet
        s.w = w if w is not None else CLASSES[cls][0]
        s.ln = ln if ln is not None else CLASSES[cls][1]
        s.y, s.ys, s.bridge = ROAD_Y, None, False

COAST = [(400, 22), (560, 16), (700, 20), (850, 18), (985, 26), (998, 120), (1000, 196), (1040, 226), (1104, 236), (1106, 360), (1106, 500), (1104, 640), (1060, 722), (880, 734), (760, 730), (640, 732), (560, 736), (480, 732),
         (432, 702), (440, 622), (470, 566), (530, 546), (545, 495), (482, 472), (432, 502), (400, 572), (340, 640), (232, 668), (130, 622), (84, 540), (140, 470), (232, 432), (216, 382), (232, 342), (200, 304), (186, 236), (288, 222), (292, 128), (392, 118)]


RAIL_MAIN = [(402, 114), (345, 160), (318, 262), (325, 320), (345, 372), (368, 420), (345, 462), (300, 492), (262, 508), (215, 530), (176, 562)]
RAIL_SIDINGS = [[(262, 508), (232, 512), (190, 536), (150, 570)], [(262, 508), (226, 534), (196, 566), (164, 590)]]
# The line runs up the West Highway's median from the port, along Central Avenue through the hub, and on along Lake Road into the rich quarter: one station in each.
METRO = [(352, 498), (374, 480), (412, 446), (440, 436), (470, 425), (520, 392), (560, 350), (598, 305), (640, 285), (682, 262), (712, 232), (740, 205), (800, 190), (850, 190)]
STATIONS = [('Harbour Gate', (374, 480)), ('Moti Chowk', (624, 293)), ('Lake Road', (812, 190))]
FLYOVERS = [('Hub Flyover', 'Central Highway', (640, 340), (770, 268)), ('East Flyover', 'East Road', (718, 310), (900, 290)),
            ('South Flyover', 'Central Highway', (648, 404), (716, 545)), ('West Flyover', 'Station Road', (392, 369), (548, 352))]
RAMP = 150.0
SLIP = 90.0
# Where the airport model goes: its middle, the way its runway runs, and how many metres one of its units is. The terminal faces the city.
AIR_SCALE = 30.0
AIR_CENTRE = P(962, 588)
AIR_DIR = (-0.64, -0.768)

def air(mx, mz):
    """A point of the airport model in world metres."""
    ux, uz = AIR_DIR
    rx, rz = uz, -ux  # Unity's +x is to the right of +z, seen from above.
    a, b = (mx - 2.3) * AIR_SCALE, (mz + 4.0) * AIR_SCALE
    return (AIR_CENTRE[0] + rx * a + ux * b, AIR_CENTRE[1] + rz * a + uz * b)

def nearest_on(lines, p):
    best = (1e18, None)
    for l in lines:
        for i in range(len(l) - 1):
            d, _, q = seg_point(l[i], l[i + 1], p)
            if d < best[0]: best = (d, q)
    return best[1]

def avenue(axis_pts, name, cls, gap, carriage, lanes, kind, meet):
    """Twin one-way carriageways either side of an axis. Each keeps left of its own travel: Bangladesh drives on the left."""
    d = gap / 2 + carriage / 2
    fwd = Way(cls, offset(axis_pts, d), name, oneway=1, w=carriage, ln=lanes, kind=kind, meet=meet)
    back = Way(cls, list(reversed(offset(axis_pts, -d))), name, oneway=1, w=carriage, ln=lanes, kind=kind, meet=(meet[1], meet[0]))
    return [fwd, back]

def smooth(px, closed=False): return catmull(PS(px), 25, closed=closed)

def skeleton():
    """The roads drawn by hand: highways are purple on the owner's map, roads yellow. Axes are kept for the flyovers and the metro. meet names what each end was drawn to reach."""
    ways, ax = [], {}
    def twin(name, px, meet=(None, None), cls='trunk', gap=6.0, carriage=10.5, lanes=3, closed=False):
        axis = smooth(px, closed)
        ax[name] = axis
        ways.extend(avenue(axis, name, cls, gap, carriage, lanes, 'highway' if cls == 'trunk' else 'avenue', meet))
    def road(name, px, meet=(None, None), cls='primary', closed=False):
        ways.append(Way(cls, smooth(px, closed), name, meet=meet))
    twin('Rich Loop', RICH_ROAD, gap=4.0, carriage=7.0, lanes=2, closed=True)
    twin('West Highway', [(300, 528), (352, 498), (412, 446), (392, 405), (372, 368), (350, 320), (340, 262), (352, 196), (392, 128), (448, 78), (540, 48), (640, 46), (705, 66), (748, 100), (786, 128)], ('Port Ring', 'Rich Loop'))
    twin('Central Highway', [(800, 250), (735, 285), (690, 318), (625, 348), (655, 420), (690, 490), (730, 570), (770, 650), (792, 716)], ('Rich Loop', 'South Road'), gap=16.0)
    twin('Airport Highway', [(770, 646), (812, 585), (868, 522), (925, 462), (990, 408), (1042, 340), (1036, 270), (1002, 216)], ('Central Highway', 'Rich Loop'), carriage=7.0, lanes=2)
    twin('Central Avenue', [(432, 450), (470, 425), (520, 392), (560, 350), (598, 305), (640, 285), (682, 262), (712, 232), (740, 205)], ('Bay Road', 'Rich Loop'), cls='primary', gap=10.0, carriage=7.0, lanes=2)
    twin('Lake Road', [(740, 205), (800, 190), (884, 190)], ('Rich Loop', 'Lake Drive'), cls='primary', gap=10.0, carriage=7.0, lanes=2)
    twin('Station Road', [(374, 368), (440, 372), (505, 362), (560, 350), (625, 348)], ('West Highway', 'Central Highway'), cls='primary', gap=16.0, carriage=7.0, lanes=2)
    twin('East Road', [(690, 318), (770, 294), (880, 285), (960, 320), (990, 405)], ('Central Highway', 'Airport Highway'), cls='primary', gap=16.0, carriage=7.0, lanes=2)
    road('Bay Road', [(412, 446), (470, 452), (560, 452), (625, 440), (668, 436), (760, 418), (860, 440), (930, 456)], ('West Highway', 'Airport Highway'))
    road('South Road', [(850, 716), (790, 714), (700, 712), (600, 712), (500, 708), (452, 690), (458, 628), (486, 586), (552, 560), (566, 500), (574, 452)], (None, 'Bay Road'))
    road('Port Ring', [(300, 528), (360, 562), (332, 622), (240, 640), (150, 606), (116, 540), (170, 492), (240, 476)], closed=True)
    road('Circular Road', [(424, 236), (452, 160), (540, 118), (640, 122), (690, 186), (644, 262), (560, 300), (470, 300)], closed=True)
    road('Grand Avenue', [(552, 46), (556, 120), (560, 200), (575, 240), (598, 305), (612, 346)], ('West Highway', 'Station Road'))
    road('Market Road', [(470, 452), (462, 372), (470, 300), (452, 240), (342, 262)], ('Bay Road', 'West Highway'), cls='secondary')
    road('Temple Road', [(640, 46), (640, 122), (640, 190), (644, 262), (640, 285)], ('West Highway', 'Central Avenue'), cls='secondary')
    road('Mill Road', [(346, 300), (424, 288), (505, 362)], ('West Highway', 'Station Road'), cls='secondary')
    road('Slum Road', [(830, 256), (825, 288), (815, 360), (812, 428)], ('Rich Loop', 'Bay Road'), cls='secondary')
    road('Basti Lane', [(900, 262), (905, 330), (896, 448)], ('Rich Loop', 'Bay Road'), cls='secondary')
    road('Crime Cross', [(700, 500), (770, 500), (840, 500), (868, 522)], ('Central Highway', 'Airport Highway'), cls='secondary')
    road('Dock Road', [(610, 712), (620, 620), (650, 560), (690, 490)], ('South Road', 'Central Highway'), cls='secondary')
    road('Rich Avenue', [(800, 92), (880, 130), (960, 150), (984, 150)], ('Rich Loop', 'Rich Loop'), cls='secondary')
    road('Lake Drive', [(850, 42), (880, 130), (900, 262)], ('Rich Loop', 'Rich Loop'), cls='secondary')
    a = nearest_on([ax['Airport Highway']], P(902, 486))
    ways.append(Way('primary', catmull([a, P(925, 520), air(9.3, -6.5)], 25), 'Airport Road', meet=('Airport Highway', None)))
    for w in list(ways):
        for end in (0, 1):
            if w.meet[end]: meet(w, end, [o.pts for o in ways if o.name == w.meet[end] and o is not w])
    return ways, ax

def meet(w, end, targets, reach=90.0):
    """Makes one end of a way finish exactly on what it was drawn to reach, through to the far carriageway of a twin: run on past the end, and cut at the last crossing near it."""
    p = w.pts if end == 1 else list(reversed(w.pts))
    a, b = p[-2], p[-1]
    l = dist(a, b) or 1
    run = p + [(b[0] + (b[0] - a[0]) / l * reach, b[1] + (b[1] - a[1]) / l * reach)]
    best = None
    for i in range(max(0, len(run) - 8), len(run) - 1):
        for t in targets:
            for j in range(len(t) - 1):
                h = seg_cross(run[i], run[i + 1], t[j], t[j + 1])
                if h and (best is None or (i, h[0]) > best[:2]): best = (i, h[0])
    if best is None:
        print('warning: %s does not reach its target at end %d' % (w.name, end))
        return
    i, t = best
    u, v = run[i], run[i + 1]
    p = run[:i + 1] + [(u[0] + (v[0] - u[0]) * t, u[1] + (v[1] - u[1]) * t)]
    w.pts = p if end == 1 else list(reversed(p))

# ---- Making a plane graph of lines ----

def q(p): return (round(p[0], 2), round(p[1], 2))

def planarize(ways, tol=0.06):
    """Puts a shared vertex wherever two ways cross or one ends on another. Coordinates are rounded to the centimetre the files hold, first, so what touches here touches there."""
    segs = Grid(30.0)
    for w in ways:
        w.pts = [q(p) for p in w.pts]
        w.pts = [p for i, p in enumerate(w.pts) if i == 0 or p != w.pts[i - 1]]
        for i in range(len(w.pts) - 1):
            a, b = w.pts[i], w.pts[i + 1]
            segs.add((w, i), min(a[0], b[0]) - tol, min(a[1], b[1]) - tol, max(a[0], b[0]) + tol, max(a[1], b[1]) + tol)
    cuts = {}  # (way id, seg) -> [(t, point)]
    def cut(w, i, t, p):
        cuts.setdefault((id(w), i), []).append((t, p))
    byid = {id(w): w for w in ways}
    for key, items in segs.cells.items():
        for ai in range(len(items)):
            w1, i1 = items[ai]
            a, b = w1.pts[i1], w1.pts[i1 + 1]
            for bi in range(ai + 1, len(items)):
                w2, i2 = items[bi]
                if w1 is w2 and abs(i1 - i2) <= 1: continue
                c, d = w2.pts[i2], w2.pts[i2 + 1]
                h = seg_cross(a, b, c, d)
                if h:
                    p = q((a[0] + (b[0] - a[0]) * h[0], a[1] + (b[1] - a[1]) * h[0]))
                    # A crossing at an end is that end: a new point a centimetre off it would leave the two roads apart.
                    for e in (a, b, c, d):
                        if dist(p, e) <= 0.6:
                            p = e
                            break
                    cut(w1, i1, h[0], p)
                    cut(w2, i2, h[1], p)
                    continue
                for e, ow, oi, oa, ob in ((a, w2, i2, c, d), (b, w2, i2, c, d), (c, w1, i1, a, b), (d, w1, i1, a, b)):
                    dd, t, _ = seg_point(oa, ob, e)
                    if dd <= tol: cut(ow, oi, t, e)
    for w in ways:
        out = []
        for i in range(len(w.pts) - 1):
            out.append(w.pts[i])
            for t, p in sorted(set(cuts.get((id(w), i), ()))): out.append(p)
        out.append(w.pts[-1])
        w.pts = [p for i, p in enumerate(out) if i == 0 or p != out[i - 1]]

def faces(ways):
    """The closed shapes between the lines, each a list of points anticlockwise. The outside face is left out."""
    adj = {}
    for w in ways:
        for i in range(len(w.pts) - 1):
            a, b = w.pts[i], w.pts[i + 1]
            adj.setdefault(a, set()).add(b)
            adj.setdefault(b, set()).add(a)
    # Loose ends go, so a road that stops short does not fold a face over itself.
    changed = True
    while changed:
        changed = False
        for p in [p for p, n in adj.items() if len(n) == 1]:
            for o in adj[p]: adj[o].discard(p)
            del adj[p]
            changed = True
    order = {p: sorted(n, key=lambda o: math.atan2(o[1] - p[1], o[0] - p[0])) for p, n in adj.items()}
    seen, out = set(), []
    for a in order:
        for b in order[a]:
            if (a, b) in seen: continue
            face, u, v = [], a, b
            while (u, v) not in seen:
                seen.add((u, v))
                face.append(u)
                n = order[v]
                k = n.index(u)
                u, v = v, n[(k - 1) % len(n)]
            if len(face) >= 3 and area(face) > 0: out.append(face)
    return out

def street_class(zone, a):
    """The class of street that cuts a block of area a in this district."""
    finest = ZONES[zone]['finest']
    if a > 90000: c = 'tertiary'
    elif a > 22000: c = 'residential'
    elif a > 6000: c = 'living_street'
    else: c = 'service'
    if RANK[c] < RANK[finest]: c = finest
    if finest == 'path' and a < 2600: c = 'path'
    return c

def main_dir(poly):
    """The way a shape's sides mostly run, weighted by length: a block lines up with the long road it sits on."""
    bins = [0.0] * 36
    for i in range(len(poly)):
        a, b = poly[i], poly[(i + 1) % len(poly)]
        t = math.atan2(b[1] - a[1], b[0] - a[0]) % math.pi
        bins[min(35, int(t / math.pi * 36))] += dist(a, b)
    k = max(range(36), key=lambda i: bins[i] + 0.5 * (bins[i - 1] + bins[(i + 1) % 36]))
    t = (k + 0.5) / 36 * math.pi
    return (math.cos(t), math.sin(t))

class Cutter:
    def __init__(s, rng):
        s.rng, s.ways, s.blocks = rng, [], []
        s.nodes = Grid(30.0)
        s.failed = []
        s.rails = []
    def clear_of_nodes(s, p, gap):
        for n in s.nodes.near(p[0] - gap, p[1] - gap, p[0] + gap, p[1] + gap):
            if dist(n, p) < gap: return False
        return True
    def settle(s, p, poly, i, gap):
        """Where a new street may meet the edge it reaches: at p if nothing is close; at a street coming in from the other side, if one is, to make a crossroads; otherwise nowhere."""
        a, b = poly[i], poly[(i + 1) % len(poly)]
        best, far = None, gap
        for n in s.nodes.near(p[0] - gap, p[1] - gap, p[0] + gap, p[1] + gap):
            d = dist(n, p)
            if d >= gap: continue
            if seg_point(a, b, n)[0] > 0.03: return None
            if d < far: best, far = n, d
        return best if best is not None else p
    def add_node(s, p): s.nodes.add(p, p[0], p[1], p[0], p[1])
    def chord(s, poly, c, d):
        """Where the line through c along d meets the shape nearest either side: (edge a, point a, edge b, point b)."""
        far = 1e5
        a, b = (c[0] - d[0] * far, c[1] - d[1] * far), (c[0] + d[0] * far, c[1] + d[1] * far)
        hits = []
        for i in range(len(poly)):
            h = seg_cross(a, b, poly[i], poly[(i + 1) % len(poly)])
            if h: hits.append((h[0], i, (a[0] + (b[0] - a[0]) * h[0], a[1] + (b[1] - a[1]) * h[0])))
        hits.sort()
        mid = 0.5
        lo = [h for h in hits if h[0] <= mid]
        hi = [h for h in hits if h[0] > mid]
        if not lo or not hi: return None
        return lo[-1][1], lo[-1][2], hi[0][1], hi[0][2]
    def cut(s, poly, parent, depth=0):
        zone = zone_at(centroid(poly)) or parent
        z = ZONES[zone]
        a = area(poly)
        goal = z['block'] * s.rng.uniform(0.75, 1.35)
        if a <= goal or depth > 11:
            s.blocks.append((poly, zone))
            return
        u = main_dir(poly)
        v = (-u[1], u[0])
        lo, hi = extent(poly, u)
        wlo, whi = extent(poly, v)
        if whi - wlo > hi - lo: u, v, lo, hi = v, (-v[1], v[0]), wlo, whi
        cls = street_class(zone, a)
        gap = 15.0 if RANK[cls] >= 2 else 9.0
        cx, cz = centroid(poly)
        for attempt in range(36):
            # The first tries want a clean square cut through the middle; later ones will settle for less, because a block too big to walk round is worse than a skewed street.
            loose = attempt / 36.0
            if attempt == 18: u, v = v, (-v[1], v[0])
            f = s.rng.uniform(-0.12 - 0.25 * loose, 0.12 + 0.25 * loose)
            turn = s.rng.uniform(-0.05 - 0.2 * loose, 0.05 + 0.2 * loose)
            c = (cx + u[0] * f * (hi - lo), cz + u[1] * f * (hi - lo))
            d = (v[0] * math.cos(turn) - v[1] * math.sin(turn), v[0] * math.sin(turn) + v[1] * math.cos(turn))
            h = s.chord(poly, c, d)
            if not h: continue
            ia, pa, ib, pb = h
            if ia == ib or dist(pa, pb) < 18: continue
            ea = (poly[(ia + 1) % len(poly)][0] - poly[ia][0], poly[(ia + 1) % len(poly)][1] - poly[ia][1])
            eb = (poly[(ib + 1) % len(poly)][0] - poly[ib][0], poly[(ib + 1) % len(poly)][1] - poly[ib][1])
            def steep(e):
                l = math.hypot(*e) or 1
                return abs(e[0] * d[1] - e[1] * d[0]) / l
            need = 0.8 - 0.3 * loose
            if steep(ea) < need or steep(eb) < need: continue
            pa, pb = s.settle(pa, poly, ia, gap), s.settle(pb, poly, ib, gap)
            if pa is None or pb is None: continue
            one = [pa] + [poly[k % len(poly)] for k in range(ia + 1, ia + 1 + ((ib - ia) % len(poly)))] + [pb]
            two = [pb] + [poly[k % len(poly)] for k in range(ib + 1, ib + 1 + ((ia - ib) % len(poly)))] + [pa]
            if min(area(one), area(two)) < z['block'] * (0.3 - 0.18 * loose): continue
            if any(seg_cross(pa, pb, r[i], r[i + 1]) for r in s.rails for i in range(len(r) - 1)): continue
            path = s.wiggle(pa, pb, cls, z['wiggle'])
            s.add_node(pa)
            s.add_node(pb)
            one = one[:-1] + list(reversed(path))[:-1]
            two = two[:-1] + path[:-1]
            s.street(path, cls, zone)
            s.cut(one, zone, depth + 1)
            s.cut(two, zone, depth + 1)
            return
        s.failed.append(a)
        s.blocks.append((poly, zone))
    def wiggle(s, a, b, cls, amp):
        n = max(1, int(dist(a, b) / (22 if RANK[cls] <= 1 else 60)))
        l = left_of(a, b)
        if RANK[cls] >= 3: amp = 0
        out = [a]
        for i in range(1, n):
            t = i / n
            o = s.rng.uniform(-amp, amp)
            out.append((a[0] + (b[0] - a[0]) * t + l[0] * o, a[1] + (b[1] - a[1]) * t + l[1] * o))
        out.append(b)
        return out
    def street(s, path, cls, zone):
        # Some lanes in the old town and the slum stop short and go on as a footpath only people can use.
        if RANK[cls] <= 1 and cls != 'path' and zone in ('slum', 'crime') and len(path) >= 4 and s.rng.random() < 0.25:
            k = len(path) * 2 // 3
            s.ways.append(Way(cls, path[:k + 1]))
            s.ways.append(Way('path', path[k:]))
            return
        s.ways.append(Way(cls, path))

# ---- Chunks ----

def cell_of(p):
    return (min(GRID_X - 1, max(0, int(p[0] // CELL))), min(GRID_Z - 1, max(0, int(p[1] // CELL))))

def in_world(cx, cz): return 0 <= cx < GRID_X and 0 <= cz < GRID_Z and (cx, cz) not in MISSING

def split_at_borders(pts):
    out = [pts[0]]
    for i in range(len(pts) - 1):
        a, b = pts[i], pts[i + 1]
        ts = []
        for axis_i in (0, 1):
            lo, hi = sorted((a[axis_i], b[axis_i]))
            k = math.floor(lo / CELL) + 1
            while k * CELL < hi:
                ts.append(((k * CELL - a[axis_i]) / (b[axis_i] - a[axis_i]), axis_i, k * CELL))
                k += 1
        for t, axis_i, v in sorted(ts):
            p = [a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t]
            p[axis_i] = v
            out.append(q(p))
        out.append(b)
    return [p for i, p in enumerate(out) if i == 0 or p != out[i - 1]]

class City:
    pass

def along(axis, s):
    '''Point and left normal at arc position s of a line.'''
    for i in range(len(axis) - 1):
        l = dist(axis[i], axis[i + 1])
        if s <= l or i == len(axis) - 2:
            t = s / l if l else 0
            a, b = axis[i], axis[i + 1]
            return (a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t), left_of(a, b)
        s -= l
    return axis[-1], left_of(axis[-2], axis[-1])

def arc_of(axis, p):
    best, s, at = 1e18, 0.0, 0.0
    for i in range(len(axis) - 1):
        d, t, _ = seg_point(axis[i], axis[i + 1], p)
        l = dist(axis[i], axis[i + 1])
        if d < best: best, at = d, s + t * l
        s += l
    return at

def ease(t):
    t = max(0.0, min(1.0, t))
    return t * t * (3 - 2 * t)

def flyover(name, on, ax, a_px, b_px, ways):
    '''A two-way deck over the median of a twin road: a ramp at each end, slip roads from and to both carriageways. Left-hand traffic, so nothing crosses: each direction joins from its own side.'''
    axis = ax[on]
    s0, s1 = sorted((arc_of(axis, P(*a_px)), arc_of(axis, P(*b_px))))
    pair = [w for w in ways if w.name == on]
    fwd, back = pair[0], pair[1]
    d = dist(along(axis, s0 + 1)[0], nearest_on([fwd.pts], along(axis, s0 + 1)[0]))
    n = max(8, int((s1 - s0) / 12.5))
    pts = [q(along(axis, s0 + (s1 - s0) * i / n)[0]) for i in range(n + 1)]
    arcs = [(s1 - s0) * i / n for i in range(n + 1)]
    length = s1 - s0
    def height(s): return ROAD_Y + (DECK_Y - ROAD_Y) * ease(min(s, length - s) / RAMP)
    i = next(k for k in range(n + 1) if arcs[k] >= RAMP)
    j = next(k for k in range(n, -1, -1) if arcs[k] <= length - RAMP)
    up = Way('primary', pts[:i + 1], name, w=14.0, ln=4, kind='ramp')
    up.ys = [height(arcs[k]) for k in range(i + 1)]
    deck = Way('primary', pts[i:j + 1], name, w=14.0, ln=4, kind='deck')
    deck.y, deck.bridge = DECK_Y, True
    down = Way('primary', pts[j:], name, w=14.0, ln=4, kind='ramp')
    down.ys = [height(arcs[k]) for k in range(j, n + 1)]
    up.ys[-1] = down.ys[0] = DECK_Y
    def slip(toe_s, away, side, into):
        '''From the carriageway on one side to the toe, or back: a smooth sideways move along the axis.'''
        out = []
        m = 8
        for k in range(m + 1):
            t = k / m
            at, l = along(axis, toe_s + away * SLIP * (1 - t))
            lat = d * side * (1 - ease(t))
            out.append((at[0] + l[0] * lat, at[1] + l[1] * lat))
        cw = fwd if side > 0 else back
        out[0] = nearest_on([cw.pts], out[0])
        out[-1] = along(axis, toe_s)[0]
        if not into: out.reverse()
        w = Way('secondary', out, name + ' slip', oneway=1, w=7.0, ln=2, kind='slip')
        return w
    slips = [slip(s0, -1, 1, True), slip(s0, -1, -1, False), slip(s1, 1, 1, False), slip(s1, 1, -1, True)]
    for w in slips:
        if w.pts[-1] == q(along(axis, s0)[0]) or dist(w.pts[-1], pts[0]) < 0.1: w.pts[-1] = pts[0]
        elif dist(w.pts[0], pts[0]) < 0.1: w.pts[0] = pts[0]
        elif dist(w.pts[-1], pts[-1]) < 0.1: w.pts[-1] = pts[-1]
        elif dist(w.pts[0], pts[-1]) < 0.1: w.pts[0] = pts[-1]
    return [up, deck, down], slips

def build(seed):
    rng = random.Random(seed)
    city = City()
    city.seed = seed
    city.land = catmull(PS(COAST), 30, closed=True)[:-1]
    if area(city.land) < 0: city.land.reverse()
    arterial, ax = skeleton()
    city.axes = ax
    rails = [catmull(PS(RAIL_MAIN), 15)]
    rails += [catmull(PS(r), 15) for r in RAIL_SIDINGS]
    for r in rails[1:]: r[0] = nearest_on([rails[0]], r[0])
    city.rails = rails
    metro = catmull(PS(METRO), 12.5)
    city.metro = metro
    city.stations = [(n, P(*p)) for n, p in STATIONS]
    # The slip roads go in with the at-grade network, so they get real junctions with their carriageways; the decks and ramps stay above it, and join only at their toes.
    city.flyovers, slips = [], []
    planarize(arterial)
    for name, on, a, b in FLYOVERS:
        ramps, sl = flyover(name, on, ax, a, b, arterial)
        city.flyovers.append((name, ramps, sl))
        slips += sl
    fs = faces(arterial)
    median = medians(arterial)
    city.lake = None
    city.green, city.airport_faces = [], []
    cutter = Cutter(rng)
    cutter.rails = rails
    for p, e in node_map(arterial).items():
        if len(e[2]) >= 2 or e[1]: cutter.add_node(p)
    for f in sorted(fs, key=lambda f: (round(area(f)), f[0])):
        if median(f): continue
        c = centroid(f)
        if inside(c, AIRPORT_Z):
            city.airport_faces.append(f)
            continue
        z = zone_at(c)
        if z is None:
            city.green.append(f)
            continue
        cutter.cut(f, z)
    ways = arterial + slips + cutter.ways
    planarize(ways)
    above = [w for _, r, _ in city.flyovers for w in r]
    ways += above
    for i, w in enumerate(ways): w.id = i + 1
    city.ways, city.blocks, city.failed = ways, cutter.blocks, cutter.failed
    city.green += city.airport_faces
    city.airport = AIRPORT_Z
    city.runway = (air(-1.06, -26), air(-1.06, 18))
    city.air_rect = [air(-3.9, -26.1), air(8.5, -26.1), air(8.5, 18.1), air(-3.9, 18.1)]
    placer = place.Placer(city, rng, place.load_models(), zone_at)
    stations(city, placer)
    city.placed = placer.run()
    harbour.place_vessels(placer)
    city.parked = city.boats + parking.spaces(placer, city, zone_at, random.Random(seed + 1))
    city.track = [(p['x'], p['z'], p['y'] + placer.models['rail_long']['size'][1]) for p in city.placed if p['mdl'] == 'rail_long']
    city.fixed = airport()
    return city

AIR_LIFT = -2.2  # The airport model's ground is 2.4 m above its origin here; this leaves it just over the land.

def airport():
    '''The airport as one piece, its parts at one pose, and its aircraft on the apron and at the runway end. Too large for a chunk, so it stands in fixed.txt.'''
    ux, uz = AIR_DIR
    turn = math.degrees(math.atan2(ux, uz)) % 360
    out = [(m, AIR_CENTRE[0], AIR_CENTRE[1], AIR_LIFT, turn, 1.0, 0, 0xC8C8C8) for m in ('airport_field', 'airport_terminal', 'airport_sheds')]
    # At the gates, nose to the terminal; one on the taxiway, two fighters on the far apron.
    for mx, mz, spin, name, tint in ((3.45, -13.5, 90, 'jet_tanker', 0xD6D6D2), (3.45, -9.6, 90, 'jet_tanker', 0xC8CCD2), (3.45, -5.7, 90, 'jet_tanker', 0xD8D2C8), (3.45, -1.8, 90, 'jet_tanker', 0xCCD0CE),
                                    (3.45, 2.1, 90, 'jet_tanker', 0xD2D2D6), (1.1, 6.2, 180, 'jet_tanker', 0xC8C8CC), (0.6, -16.5, 0, 'jet_fighter', 0xC0C4C8), (1.3, -16.5, 0, 'jet_fighter', 0xB8BCC0)):
        x, z = air(mx, mz)
        y = 0.45 + (0.0 if name != 'jet_fighter' else 0.0)
        out.append((name, x, z, y, (turn + spin) % 360, 1.0, 0, tint))
    return out

STATION_HALF = (24.0, 62.0)
CAR_PITCH, CARS = 23.0, 4  # The owner's metro car, 22.5 m, parked four to a train; the platform is 112 m.

def stations(city, placer):
    '''Each station stands across its avenue, on the line: stairs and a lift each side, on the footpath's far edge. Its model carries platforms, canopy and bridges; only the lifts and stairs reach the ground, so only they are solid.'''
    for name, c in city.stations:
        p = nearest_on([city.metro], c)
        s0 = arc_of(city.metro, p)
        a, _ = along(city.metro, s0 - 20)
        b, _ = along(city.metro, s0 + 20)
        l = dist(a, b)
        f = ((b[0] - a[0]) / l, (b[1] - a[1]) / l)
        t = (f[1], -f[0])  # The model's +x.
        box = place.Box(p, f, STATION_HALF[1], STATION_HALF[0])
        placer.site.stand(box)
        for side in (1, -1):
            c2 = (p[0] + t[0] * side * 18.0 + f[0] * -12.0, p[1] + t[1] * side * 18.0 + f[1] * -12.0)
            foot = place.Box(c2, f, 16.0, 1.7)
            placer.out.append(dict(mdl='metro_station' if side == 1 else 'collider', x=p[0], z=p[1], y=0.0, turn=place.turn_of(f), size=1.0, mirror=False, tint=(200, 200, 200), h=16.0, t='station', outer=foot.corners(), zone='hub', name=name))
        # A train stands at the platform. The file's car has one cab, so the car at the far end is turned round, and both ends of the train show a cab.
        for k in range(CARS):
            s = s0 + (k - (CARS - 1) / 2) * CAR_PITCH
            c1, _ = along(city.metro, s)
            a1, _ = along(city.metro, s - 8)
            b1, _ = along(city.metro, s + 8)
            l1 = dist(a1, b1)
            turn = place.turn_of(((b1[0] - a1[0]) / l1, (b1[1] - a1[1]) / l1))
            placer.out.append(dict(mdl='metro_car', x=c1[0], z=c1[1], y=METRO_Y, turn=(turn + (180 if k == 0 else 0)) % 360, size=1.0, mirror=False, tint=(255, 255, 255), h=4.7, t='train', outer=[], zone='hub', name=''))

def medians(ways):
    '''A test for the strip between a twin's carriageways: a face whose outline is almost all one road's name.'''
    owner = {}
    for w in ways:
        if not w.name or not w.oneway: continue
        for p in w.pts: owner.setdefault(p, set()).add((w.name, id(w)))
    def test(f):
        by, sides, total = {}, {}, 0.0
        for i in range(len(f)):
            a, b = f[i], f[(i + 1) % len(f)]
            l = dist(a, b)
            total += l
            for n, w in owner.get(a, set()) & owner.get(b, set()):
                by[n] = by.get(n, 0) + l
                sides.setdefault(n, set()).add(w)
        return any(v > 0.8 * total and len(sides[n]) >= 2 for n, v in by.items())
    return test

def node_map(ways):
    """How many ways use each point, and of what rank."""
    n = {}
    for w in ways:
        for i, p in enumerate(w.pts):
            e = n.setdefault(p, [0, 0, set(), []])
            e[0] += 1
            if i == 0 or i == len(w.pts) - 1: e[1] += 1
            e[2].add(id(w))
            e[3].append(w)
    return n

def chunks(city):
    out = {}
    def chunk(cx, cz):
        k = (cx, cz)
        if k not in out:
            out[k] = dict(ix=cx, iy=-(cz + 1), roads=[], buildings=[], water=[], parks=[], nodes=[], edges=[], points=[], rails=[], trees=[], landmarks=[], plazas=[], landuse=[])
            out[k]['_nodes'] = {}
        return out[k]
    for cx in range(GRID_X):
        for cz in range(GRID_Z):
            if in_world(cx, cz): chunk(cx, cz)
    junction = node_map(city.ways)
    def is_node(p):
        e = junction.get(p)
        return e is not None and (len(e[2]) >= 2 or e[1] >= 1)
    fid = 1
    for w in city.ways:
        pts = split_at_borders(w.pts)
        vehicle = w.cls not in FOOT
        run, seq = [pts[0]], 0
        cur = None
        edge = [pts[0]]
        for i in range(len(pts) - 1):
            a, b = pts[i], pts[i + 1]
            m = ((a[0] + b[0]) / 2, (a[1] + b[1]) / 2)
            c = cell_of(m)
            if cur is None: cur = c
            if c != cur:
                emit_road(chunk, cur, w, run, in_world)
                emit_edge(chunk, cur, w, edge, seq, in_world, vehicle)
                seq += 1
                run, edge, cur = [a], [a], c
            run.append(b)
            edge.append(b)
            if is_node(b) and i + 1 < len(pts) - 1:
                emit_edge(chunk, cur, w, edge, seq, in_world, vehicle)
                seq += 1
                edge = [b]
        emit_road(chunk, cur, w, run, in_world)
        emit_edge(chunk, cur, w, edge, seq, in_world, vehicle)
    # Signals where two major roads meet.
    for k, c in out.items():
        for key, n in c['_nodes'].items():
            e = junction.get((key[0] / 100.0, key[1] / 100.0))
            if e is None: continue
            ranks = sorted((RANK[w.cls] for w in e[3]), reverse=True)
            if len(e[2]) >= 2 and len(ranks) >= 2 and ranks[0] >= 5 and ranks[1] >= 4 and not any(w.kind in ('ramp', 'deck', 'slip') for w in e[3]): n['sig'] = 1
    # Water is everything inside the boundary that is not land, as convex pieces cut to the chunks.
    wid = 1
    for t in triangulate(BOUNDARY, city.land): wid = add_clipped(out, 'water', list(t), wid)
    for f in city.green:
        for t in triangulate(f): wid = add_clipped(out, 'parks', list(t), wid)
    # Ground: all land starts green; blocks are paved in the hub and port, lawn in the rich quarter, bare earth in the slums and the crime area.
    def landuse(poly, kind):
        nonlocal wid
        before = {k: len(c['landuse']) for k, c in out.items()}
        wid = add_clipped(out, 'landuse', list(poly), wid)
        for k, c in out.items():
            for r in c['landuse'][before[k]:]: r['t'] = kind
    for t in triangulate(city.land): landuse(t, 'city-green')
    for poly, z in city.blocks:
        layer = {'hub': 'plazas', 'port': 'plazas', 'rich': 'parks'}.get(z)
        for t in triangulate(poly):
            if layer: wid = add_clipped(out, layer, list(t), wid)
            else: landuse(t, 'city-soil')
    for quad in city.quays:
        for t in triangulate(quad): wid = add_clipped(out, 'plazas', list(t), wid)
    for t in city.trees:
        k = out.get(cell_of((t['x'], t['z'])))
        if k is not None: k['trees'].append(t)
    bid = 1
    for b in city.placed:
        k = out.get(cell_of((b['x'], b['z'])))
        if k is None: continue
        tint = b['tint'][0] << 16 | b['tint'][1] << 8 | b['tint'][2]
        pose = [round(b['x'] * 100), round(b['z'] * 100), round(b['y'] * 100), round(b['turn'] * 100), round(b['size'] * 1000), 1 if b['mirror'] else 0, tint]
        r = dict(id=bid, h=b['h'], outer=[q(p) for p in b['outer']], t=b['t'], mdl=b['mdl'], pose=pose)
        if b.get('name'): r['nm'] = b['name']
        k['buildings'].append(r)
        bid += 1
    rid = 1
    def rail(pts, y, name):
        nonlocal rid
        pts = split_at_borders([q(p) for p in pts])
        run, cur = [pts[0]], None
        for i in range(len(pts) - 1):
            c = cell_of(((pts[i][0] + pts[i + 1][0]) / 2, (pts[i][1] + pts[i + 1][1]) / 2))
            if cur is None: cur = c
            if c != cur:
                if in_world(*cur): chunk(*cur)['rails'].append(dict(id=rid, y=y, pts=run))
                run, cur = [pts[i]], c
            run.append(pts[i + 1])
        if in_world(*cur): chunk(*cur)['rails'].append(dict(id=rid, y=y, pts=run))
        rid += 1
    for r in city.rails: rail(r, 0.0, 'rail')
    rail(city.metro, METRO_Y, 'metro')
    return out

def blob(poly, c, k, rng):
    """A smooth shape inside poly, as a radius at each bearing from c."""
    n = 72
    r = []
    for i in range(n):
        t = i * math.tau / n
        far = (c[0] + math.cos(t) * 5000, c[1] + math.sin(t) * 5000)
        hit = 5000.0
        for j in range(len(poly)):
            h = seg_cross(c, far, poly[j], poly[(j + 1) % len(poly)])
            if h: hit = min(hit, h[0] * 5000)
        r.append(hit)
    a, b = rng.uniform(0, math.tau), rng.uniform(0, math.tau)
    out = []
    for i in range(n):
        t = i * math.tau / n
        m = min(sum(r[(i + j) % n] for j in range(-4, 5)) / 9, r[i] * 1.02)
        m *= k * (1 + 0.05 * math.sin(3 * t + a) + 0.04 * math.sin(5 * t + b))
        out.append((c[0] + m * math.cos(t), c[1] + m * math.sin(t)))
    return out

def shrink(poly, c, k):
    return [(c[0] + (p[0] - c[0]) * k, c[1] + (p[1] - c[1]) * k) for p in poly]

def add_clipped(out, layer, poly, fid):
    x0, x1 = min(p[0] for p in poly), max(p[0] for p in poly)
    z0, z1 = min(p[1] for p in poly), max(p[1] for p in poly)
    for cx in range(int(x0 // CELL), int(x1 // CELL) + 1):
        for cz in range(int(z0 // CELL), int(z1 // CELL) + 1):
            if (cx, cz) not in out: continue
            p = clip(poly, cx * CELL, cz * CELL, (cx + 1) * CELL, (cz + 1) * CELL)
            p = [q(v) for v in p]
            p = [v for i, v in enumerate(p) if v != p[i - 1]]
            if len(p) < 3 or abs(area(p)) < 0.01: continue
            if area(p) < 0: p.reverse()
            out[(cx, cz)][layer].append(dict(id=fid, outer=p))
            fid += 1
    return fid

def height_on(w, p):
    best, y = 1e18, w.ys[0]
    for i in range(len(w.pts) - 1):
        d, t, _ = seg_point(w.pts[i], w.pts[i + 1], p)
        if d < best: best, y = d, w.ys[i] + (w.ys[i + 1] - w.ys[i]) * t
    return y

def emit_road(chunk, c, w, run, ok):
    if len(run) < 2 or not ok(*c): return
    r = dict(id=w.id, cls=w.cls, w=w.w, we=False, y=w.y, bridge=w.bridge, pts=list(run), sf='asphalt' if w.cls not in FOOT else 'paved')
    if w.ln: r['ln'] = w.ln
    if w.ys:
        # Heights go with the points: a run cut at a chunk border needs them at the cut too.
        full = [(dist(w.pts[i], w.pts[i + 1])) for i in range(len(w.pts) - 1)]
        r['ys'] = [height_on(w, p) for p in run]
    if RANK[w.cls] >= 4 and w.kind not in ('ramp', 'deck', 'slip'): r['sw'] = 'both'
    if w.name: r['nme'] = w.name
    if RANK[w.cls] >= 2: r['lit'] = 1
    chunk(*c)['roads'].append(r)

def emit_edge(chunk, c, w, pts, seq, ok, vehicle):
    if len(pts) < 2 or not ok(*c): return
    k = chunk(*c)
    def node(p):
        key = (round(p[0] * 100), round(p[1] * 100))
        if key not in k['_nodes']:
            n = dict(key=key, x=p[0], z=p[1])
            k['_nodes'][key] = n
            k['nodes'].append(n)
        return key
    a, b = node(pts[0]), node(pts[-1])
    k['edges'].append(dict(seq=seq, a=a, b=b, way=w.id, cls=w.cls, oneway=w.oneway, pts=list(pts)))

def write(city, cs, out):
    os.makedirs(os.path.join(out, 'chunks'), exist_ok=True)
    entries = []
    total = 0
    for (cx, cz) in sorted(cs):
        c = cs[(cx, cz)]
        cid = 'c_%d_%d' % (c['ix'], c['iy'])
        data, checks = dadc.encode(c)
        with open(os.path.join(out, 'chunks', cid + '.dadc'), 'wb') as f: f.write(data)
        total += len(data)
        entries.append(dict(id=cid, ix=c['ix'], iy=c['iy'], cell=[c['ix'], -(c['iy'] + 1)], bytes=len(data), sha256=hashlib.sha256(data).hexdigest(), checks=checks,
                            counts=dict(buildings=len(c['buildings']), roads=len(c['roads']), graphNodes=len(c['nodes']), graphEdges=len(c['edges']))))
    files = {}
    places = []
    for k, poly in (('hub', HUB_Z), ('rich', RICH_Z), ('slum', SLUM_Z), ('crime', CRIME_Z), ('port', PORT_Z)):
        c = centroid(poly)
        places.append(dict(id=len(places) + 1, kind='suburb', name=ZONES[k]['name'], nameEn=ZONES[k]['name'], x=round(c[0], 2), z=round(c[1], 2)))
    for name, items in (('places.json', places), ('landmarks.json', [])):
        b = (json.dumps(dict(frame='x = east, z = north, metres from the world origin', items=items)) + '\n').encode()
        with open(os.path.join(out, name), 'wb') as f: f.write(b)
        files[name] = dict(items=len(items), bytes=len(b), sha256=hashlib.sha256(b).hexdigest())
    world = dict(format='dad-unity-world/1', generator='Tools/worldgen/gen.py', seed=city.seed, owner='Ishak Hasan Sabbir', areaKm2=len(cs) * CELL * CELL / 1e6,
                 frame=dict(units='metres in Unity; centimetres in DADC files', axes='x = east, y = up, z = north (Unity, left-handed)', chunkSize=500,
                            chunkCorner='DADC positions are relative to the chunk south-west corner: (ix * 500, -(iy + 1) * 500) in Unity metres'),
                 totals=dict(chunks=len(entries), dadcBytes=total), files=files, chunks=entries)
    with open(os.path.join(out, 'world.json'), 'w') as f: json.dump(world, f, indent=1)
    write_transport(city, out)
    write_terrain(city, out)
    harbour.write_water(city, out, [(x, z) for x in range(GRID_X) for z in range(GRID_Z) if in_world(x, z)])
    with open(os.path.join(out, 'fixed.txt'), 'w') as f:
        for m, x, z, y, turn, size, mirror, tint in city.fixed: f.write('%s %.2f %.2f %.2f %.2f %.3f %d %d\n' % (m, x, z, y, turn, size, mirror, tint))
    return world

def write_transport(city, out):
    '''What moves that is not a chunk's: the railway as one line, with where trains stop and how high the rail heads are along it, and every parked vehicle.'''
    main = city.rails[0]
    total = length(main)
    half = CARS * CAR_PITCH / 2 + 8
    gate = nearest_on([main], dict(city.stations)['Harbour Gate'])
    stops = [half, arc_of(main, gate), total - half]
    crossings = []
    for w in city.ways:
        if w.cls in FOOT or w.kind in ('deck', 'ramp'): continue
        for i in range(len(w.pts) - 1):
            for j in range(len(main) - 1):
                h = seg_cross(w.pts[i], w.pts[i + 1], main[j], main[j + 1])
                if h: crossings.append(dict(x=round(main[j][0] + (main[j + 1][0] - main[j][0]) * h[1], 2), z=round(main[j][1] + (main[j + 1][1] - main[j][1]) * h[1], 2), half=round(w.w / 2 + 1, 1)))
    grid = Grid(20.0)
    for t in city.track: grid.add(t, t[0], t[1], t[0], t[1])
    levels = []
    for k in range(int(total / 4) + 1):
        p, _ = along(main, k * 4.0)
        near = sorted(grid.near(p[0] - 10, p[1] - 10, p[0] + 10, p[1] + 10), key=lambda t: dist(t, p))
        levels.append(round(near[0][2], 3) if near else 0.5)
    kinds = sorted({p['kind'] for p in city.parked})
    pk = city.parked
    data = dict(owner='Ishak Hasan Sabbir', railway=dict(name='Port Line', points=[round(c, 2) for q in main for c in q], stops=[round(s, 2) for s in stops], stopNames=['North Yard', 'Harbour Gate', 'Port Yard'], levels=levels, levelStep=4.0, crossingX=[c['x'] for c in crossings], crossingZ=[c['z'] for c in crossings], crossingHalf=[c['half'] for c in crossings]),
                parked=dict(kinds=kinds, kind=[kinds.index(p['kind']) for p in pk], east=[round(p['x'], 2) for p in pk], north=[round(p['z'], 2) for p in pk], height=[round(p.get('y', 0.02), 2) for p in pk], turn=[round(p['turn'], 1) for p in pk]))
    with open(os.path.join(out, 'transport.json'), 'w') as f: json.dump(data, f)

def noise(x, z, cell, seed):
    '''Smooth value noise, 0 to 1, the same at any chunk.'''
    def h(i, j): return int.from_bytes(hashlib.md5(b'%d %d %d' % (i, j, seed)).digest()[:2], 'little') / 65535
    i, j = math.floor(x / cell), math.floor(z / cell)
    u, v = ease(x / cell - i), ease(z / cell - j)
    return (h(i, j) * (1 - u) + h(i + 1, j) * u) * (1 - v) + (h(i, j + 1) * (1 - u) + h(i + 1, j + 1) * u) * v

def write_terrain(city, out):
    '''The open ground, by what is there: one lattice of points 10 m apart over the world, written a chunk at a time. Sand along the water, round the airport and on the port's waterfront; rocky soil along the
    railway, on the crime area's bare ground and the port's rough edges; grass on the rich quarter's lawns, the parks and unused land. The slums keep the earth they have and the hub is paved, so neither shows.'''
    NX, NZ, step = 501, 351, 10.0
    kind = {}
    def lattice(poly):
        x0, x1 = min(p[0] for p in poly), max(p[0] for p in poly)
        z0, z1 = min(p[1] for p in poly), max(p[1] for p in poly)
        for i in range(max(0, int(x0 // step)), min(NX - 1, int(x1 // step) + 1) + 1):
            for j in range(max(0, int(z0 // step)), min(NZ - 1, int(z1 // step) + 1) + 1):
                if inside((i * step, j * step), poly): yield i, j
    for poly, zone in city.blocks:
        for k in lattice(poly): kind[k] = zone
    for quad in city.quays:
        for k in lattice(quad): kind[k] = 'port'
    for k in lattice(AIRPORT_Z): kind[k] = 'air'
    def spread(lines, reach):
        '''How near each lattice point is to these lines, up to reach.'''
        near = {}
        for line in lines:
            for a, b in zip(line, line[1:]):
                for i in range(max(0, int((min(a[0], b[0]) - reach) // step)), min(NX - 1, int((max(a[0], b[0]) + reach) // step) + 1) + 1):
                    for j in range(max(0, int((min(a[1], b[1]) - reach) // step)), min(NZ - 1, int((max(a[1], b[1]) + reach) // step) + 1) + 1):
                        d = seg_point(a, b, (i * step, j * step))[0]
                        if d < reach and d < near.get((i, j), 1e9): near[(i, j)] = d
        return near
    coast = spread([city.land + city.land[:1]], 90.0)
    rail = spread(city.rails, 16.0)
    def weights(i, j):
        p = (i * step, j * step)
        k = kind.get((i, j))
        rough = noise(p[0], p[1], 130.0, 7) * 0.7 + noise(p[0], p[1], 37.0, 8) * 0.3
        if not inside(p, city.land): return (1, 0, 0, 1 if coast.get((i, j), 99) < 30 else 0) # Under the water's edge only.
        if k == 'slum': return (0, 0, 0, 0)
        if k == 'hub': return (0, 1, 0, 0)
        if k == 'rich': s, r, g = 0, 0, 1
        elif k == 'crime': s, r, g = 0.2, 0.8, 0
        elif k == 'port': s, r, g = 0.35, 0.65, 0
        elif k == 'air': s, r, g = (0.85, 0.05, 0.1) if rough < 0.55 else (0.4, 0.05, 0.55)
        elif zone_at(p) == 'port': s, r, g = (0.75, 0.25, 0) if rough < 0.42 else (0.1, 0.9, 0)
        else:
            # Unused land: grass, with the rough patches a site left alone gets.
            s, r, g = (0, 0, 1) if rough < 0.62 else (0.15, 0.7, 0.15)
        c = coast.get((i, j))
        if c is not None:
            t = 1 - ease((c - 42) / 46)
            s, r, g = s + (1 - s) * t, r * (1 - t), g * (1 - t)
        return (s, r, g, 1)
    def bed(i, j, w):
        '''The railway's stone over whatever ground it crosses, narrow enough to read as its bed.'''
        d = rail.get((i, j))
        if d is None or w[3] == 0: return w
        t = 1 - ease((d - 2) / 8)
        return (w[0] * (1 - t), w[1] + (1 - w[1]) * t, w[2] * (1 - t), w[3])
    os.makedirs(os.path.join(out, 'terrain'), exist_ok=True)
    raw = {(i, j): weights(i, j) for i in range(NX) for j in range(NZ)}
    def blur(w):
        '''Softens every border between grounds by two lattice steps, so a polygon's edge never shows as the lattice's teeth. Whether a ground shows at all is not softened.'''
        out = {}
        for (i, j), v in w.items():
            if v[3] == 0:
                out[(i, j)] = v
                continue
            a = [0.0, 0.0, 0.0]
            n = 0
            for x in (i - 1, i, i + 1):
                for z in (j - 1, j, j + 1):
                    q = w.get((x, z))
                    if q is None or q[3] == 0: continue
                    a[0] += q[0]; a[1] += q[1]; a[2] += q[2]
                    n += 1
            out[(i, j)] = (a[0] / n, a[1] / n, a[2] / n, v[3])
        return out
    raw = blur(blur(raw))
    cells = {k: bytes(max(0, min(255, round(x * 255))) for x in bed(k[0], k[1], v)) for k, v in raw.items()}
    for cx in range(GRID_X):
        for cz in range(GRID_Z):
            if not in_world(cx, cz): continue
            b = bytearray()
            for j in range(51):
                for i in range(51): b += cells[(min(NX - 1, cx * 50 + i), min(NZ - 1, cz * 50 + j))]
            with open(os.path.join(out, 'terrain', 'c_%d_%d.bin' % (cx, -(cz + 1))), 'wb') as f: f.write(b)

def report(city, cs):
    r = {}
    r['chunks'] = len(cs)
    r['area_km2'] = len(cs) * CELL * CELL / 1e6
    r['boundary_m2'] = area(BOUNDARY)
    r['land_km2'] = round(area(city.land) / 1e6, 2)
    r['flyovers'] = [dict(name=n, ramps=sum(1 for w in rs if w.kind == 'ramp'), slips=len(sl), deck_m=round(length([w for w in rs if w.kind == 'deck'][0].pts))) for n, rs, sl in city.flyovers]
    r['roads_off_land'] = sum(1 for w in city.ways for p in w.pts[::4] if not inside(p, city.land))
    r['stations'] = [n for n, _ in city.stations]
    by = {}
    for w in city.ways: by[w.cls] = by.get(w.cls, 0) + length(w.pts)
    r['km_by_class'] = {k: round(v / 1000, 1) for k, v in sorted(by.items(), key=lambda x: -RANK[x[0]])}
    r['km_total'] = round(sum(by.values()) / 1000, 1)
    z = {}
    for p, k in city.blocks: z[k] = z.get(k, 0) + 1
    r['blocks'] = z
    # One connected network for vehicles?
    parent = {}
    def find(a):
        while parent.setdefault(a, a) != a:
            parent[a] = parent[parent[a]]
            a = parent[a]
        return a
    for w in city.ways:
        if w.cls in FOOT: continue
        for i in range(len(w.pts) - 1): parent[find(w.pts[i])] = find(w.pts[i + 1])
    groups = {}
    for p in list(parent): groups[find(p)] = groups.get(find(p), 0) + 1
    r['vehicle_networks'] = sorted(groups.values(), reverse=True)[:6]
    # Can a car get everywhere, and back? One-way carriageways that end wrongly only show up with direction counted.
    fwd, back = {}, {}
    for w in city.ways:
        if w.cls in FOOT: continue
        for i in range(len(w.pts) - 1):
            a, b = w.pts[i], w.pts[i + 1]
            fwd.setdefault(a, []).append(b)
            back.setdefault(b, []).append(a)
            if not w.oneway:
                fwd.setdefault(b, []).append(a)
                back.setdefault(a, []).append(b)
    def reach_from(adj, s0):
        seen, todo = {s0}, [s0]
        while todo:
            for n in adj.get(todo.pop(), ()):
                if n not in seen:
                    seen.add(n)
                    todo.append(n)
        return seen
    start = max(fwd, key=lambda p: len(fwd[p]))
    both = reach_from(fwd, start) & reach_from(back, start)
    allv = set(fwd) | set(back)
    r['stuck_points'] = len(allv - both)
    r['stuck_sample'] = sorted(allv - both)[:6]
    r['cuts_given_up'] = [round(a) for a in sorted(city.failed, reverse=True)[:8]]
    big = max(city.blocks, key=lambda b: area(b[0]))
    r['largest_block'] = dict(zone=big[1], at=[round(x) for x in centroid(big[0])], points=len(big[0]))
    r['placed'] = place.report(city.placed)
    sizes = sorted(area(p) for p, _ in city.blocks)
    r['block_m2'] = dict(median=round(sizes[len(sizes) // 2]), p90=round(sizes[len(sizes) * 9 // 10]), max=round(sizes[-1]))
    return r

def plan_svg(city, path):
    s = 0.2
    h = 3500
    zc = {'hub': '#e5d6cc', 'rich': '#d6e6cf', 'slum': '#ecd9a8', 'crime': '#e2b8a6', 'port': '#d0d3da'}
    pt = lambda p: '%.1f,%.1f' % (p[0] * s, (h - p[1]) * s)
    join = lambda pts: ' '.join(pt(p) for p in pts)
    o = ['<svg xmlns="http://www.w3.org/2000/svg" width="1000" height="700" viewBox="0 0 1000 700"><rect width="1000" height="700" fill="#171b1d"/>']
    o.append('<polygon points="%s" fill="#26485a"/>' % join(BOUNDARY))
    o.append('<polygon points="%s" fill="#55704c"/>' % join(city.land))
    for poly, z in city.blocks: o.append('<polygon points="%s" fill="%s"/>' % (join(poly), zc[z]))
    o.append('<polygon points="%s" fill="#f0a8c8" fill-opacity=".8"/>' % join(AIRPORT_Z))
    o.append('<polygon points="%s" fill="#66606a"/>' % join([air(-3.9, -26.1), air(8.5, -26.1), air(8.5, 18.1), air(-3.9, 18.1)]))
    cols = {'trunk': ('#9a62d8', 3.0), 'primary': ('#e0b52a', 2.2), 'secondary': ('#e0c95a', 1.5), 'tertiary': ('#8d8d84', 0.9), 'residential': ('#a0a097', 0.6), 'living_street': ('#a8a89f', 0.5),
            'service': ('#b5b5ab', 0.4), 'path': ('#7fae78', 0.3)}
    for w in sorted(city.ways, key=lambda w: RANK[w.cls]):
        if w.kind in ('ramp', 'deck', 'slip'): continue
        c, wd = cols[w.cls]
        if w.kind == 'avenue': c = '#e0b52a'
        o.append('<polyline points="%s" fill="none" stroke="%s" stroke-width="%s"/>' % (join(w.pts), c, wd))
    for r in city.rails: o.append('<polyline points="%s" fill="none" stroke="#fff" stroke-width="1.6"/>' % join(r))
    for n, rs, sl in city.flyovers:
        for w in rs + sl: o.append('<polyline points="%s" fill="none" stroke="#3fd06a" stroke-width="2.6"/>' % join(w.pts))
    o.append('<polyline points="%s" fill="none" stroke="#e8352e" stroke-width="3"/>' % join(city.metro))
    for n, p in city.stations: o.append('<circle cx="%.1f" cy="%.1f" r="6" fill="#e8352e" stroke="#fff" stroke-width="1.5"/>' % (p[0] * s, (h - p[1]) * s))
    for i in range(GRID_X + 1): o.append('<path d="M %g 0 V 700" stroke="#000" stroke-opacity=".15"/>' % (i * 100))
    for i in range(GRID_Z + 1): o.append('<path d="M 0 %g H 1000" stroke="#000" stroke-opacity=".15"/>' % (i * 100))
    o.append('</svg>')
    with open(path, 'w') as f: f.write('\n'.join(o))

if __name__ == '__main__':
    out = sys.argv[1]
    seed = int(sys.argv[2]) if len(sys.argv) > 2 else 1971
    city = build(seed)
    cs = chunks(city)
    write(city, cs, out)
    plan_svg(city, os.path.join(out, 'plan.svg'))
    print(json.dumps(report(city, cs), indent=1))
