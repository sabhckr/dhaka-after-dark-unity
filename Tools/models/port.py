#!/usr/bin/env python3
# The shipyard scan taken apart, and the docks every ship lies against (owner's port repair). The scan is one fused mesh: two piers, a basin, a bulk carrier and a green ship standing high
# in it, two cranes. Called from maritime.models(). Its ships and a crane come out as models of their own, each set where it belongs; its piers, which are ragged at every edge, are replaced by
# straight walls with the scan's own deck laid on top as one picture taken from above. No dock, quay or crane came in the owner's folder apart from this scan, so walls, fenders and the crane's
# boom (the scan keeps only dots of it) are drawn here. All measures are metres in the scan's frame: x along the piers, z across, y up, as maritime places it.
import json, math, os, random, struct, subprocess, tempfile, zlib
from array import array
from collections import deque
import reduce

WATER, DECK, TOE = 7.03, 10.25, 5.0         # The sea, the top of the piers, the foot of the walls.
X0, X1, Z0, Z1 = -172.0, 173.5, -100.5, 92.0   # The dock, square: the scan runs out beyond this in ragged corners.
GREEN_BASIN = (-160.5, 169.0, -73.0, -14.0)
BULK_BASIN = (-161.5, 135.0, 13.0, 62.0)
PIC_W, PIC_H = 2048, 1024
FENDER_GAP = 2.5                            # Water between a hull and a wall.
# Each ship's box in the scan; the top of the paint below its waterline, measured from the hull's colour by height; the wall it lies against and which side; how far along it moves.
SHIPS = {'vessel_bulk': dict(box=(-152.0, 104.0, 18.9, 55.9), paint=13.5, rim=19.0, wall=BULK_BASIN[3], side=1, along=0.0, near=50000, far=9000),
         'vessel_green': dict(box=(-146.0, 30.0, -57.2, -29.4), paint=10.0, rim=14.5, wall=GREEN_BASIN[3], side=1, along=45.0, near=30000, far=6000,
                       shape=((-146.0, 4.0), (-142.0, 7.0), (-132.0, 14.2), (10.0, 14.2), (23.0, 9.5), (30.0, 1.0)))} # Half its beam along it: its mooring lines leave the hull inside its box.
CRANE_BOX = (58.0, 125.0, 62.5, 84.0)       # The whole crane on the wide quay.
CRANE_SPOTS = ((72.6, 72.7), (31.0, -0.3))  # Where the scan's two stood, on their rails.
SLEW = 41.0                                 # The top of the crane's pedestal: all above it turns.
BOOM_FOOT, BOOM_HEAD, STAY_TOP = (77.5, 48.7), (104.3, 93.8), (76.6, 70.3)  # x, y of the boom's ends and the top of the A-frame.
WHARF_LEN, WHARF_WIDE = 40.0, 20.0          # One length of wharf; the crane's rails lie 12.8 m apart on it.
WHARF_STRIP = (100.0, 72.0)                 # The piece of the scan's quay its deck is cut from, rails and all: the middle of it.
RAMP_LEN, RAMP_WIDE, LAND_Y = 16.0, 10.0, 0.3
WATER_Y = 0.14                              # World height of the sea, as harbour.py has it.
CONCRETE, RUBBER, IRON, BLUE, LINE = (0.66, 0.64, 0.6), (0.07, 0.07, 0.08), (0.2, 0.21, 0.22), (0.13, 0.36, 0.72), (0.62, 0.53, 0.2)

def png(w, h, rgb):
    raw = bytearray()
    for r in range(h):
        raw.append(0)
        raw += rgb[r * w * 3:(r + 1) * w * 3]
    def chunk(t, d): return struct.pack('>I', len(d)) + t + d + struct.pack('>I', zlib.crc32(t + d))
    return b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', w, h, 8, 2, 0, 0, 0)) + chunk(b'IDAT', zlib.compress(bytes(raw), 9)) + chunk(b'IEND', b'')

def pixels(data):
    """A picture's rows, top first, as (width, height, rows of RGB bytes in BGR order, bytes a pixel)."""
    with tempfile.TemporaryDirectory() as d:
        a, b = os.path.join(d, 'a'), os.path.join(d, 'b.bmp')
        open(a, 'wb').write(data)
        subprocess.run(['/usr/bin/sips', '-s', 'format', 'bmp', a, '--out', b], check=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        f = open(b, 'rb').read()
    start = struct.unpack_from('<I', f, 10)[0]
    w, h = struct.unpack_from('<ii', f, 18)
    depth = struct.unpack_from('<H', f, 28)[0] // 8
    stride = (w * depth + 3) // 4 * 4
    rows = [f[start + r * stride:start + r * stride + w * depth] for r in range(abs(h))]
    if h > 0: rows.reverse()
    return w, abs(h), rows, depth

def texel(pic, u, v):
    w, h, rows, depth = pic
    r = rows[min(h - 1, int(((1 - v) % 1) * h))]
    k = min(w - 1, int((u % 1) * w)) * depth
    return r[k + 2], r[k + 1], r[k]

def uv(x, z): return (x - X0) / (X1 - X0), 1 - (z - Z0) / (Z1 - Z0)

def centre(v, t):
    a, b, c = v[t[0]], v[t[1]], v[t[2]]
    return (a[0] + b[0] + c[0]) / 3, (a[1] + b[1] + c[1]) / 3, (a[2] + b[2] + c[2]) / 3

def in_box(p, b): return b[0] < p[0] < b[1] and b[2] < p[2] < b[3]

def deck_picture(v, tris, atlas):
    """The piers from straight above: everything that stands on them lies flat in this, which is what cleans them. What the cranes hid is filled from further along the same quay."""
    w, h = PIC_W, PIC_H
    img = bytearray(w * h * 3)
    top = array('f', [-1e9]) * (w * h)
    sx, sz = w / (X1 - X0), h / (Z1 - Z0)
    aw, ah, rows, depth = atlas
    boxes = [s['box'] for s in SHIPS.values()]
    for t in (q for ts in tris.values() for q in ts):
        c = centre(v, t)
        if c[1] < DECK - 1.6 or any(in_box(c, b) for b in boxes): continue
        if c[1] > DECK + 3 and not (c[0] > 110 and c[2] > 70): continue
        A, B, C = v[t[0]], v[t[1]], v[t[2]]
        ax, ay, bx, by, cx, cy = (A[0] - X0) * sx, (A[2] - Z0) * sz, (B[0] - X0) * sx, (B[2] - Z0) * sz, (C[0] - X0) * sx, (C[2] - Z0) * sz
        d = (by - cy) * (ax - cx) + (cx - bx) * (ay - cy)
        if abs(d) < 1e-9: continue
        for y in range(max(0, int(min(ay, by, cy))), min(h - 1, int(max(ay, by, cy)) + 1) + 1):
            for x in range(max(0, int(min(ax, bx, cx))), min(w - 1, int(max(ax, bx, cx)) + 1) + 1):
                px, py = x + 0.5, y + 0.5
                p = ((by - cy) * (px - cx) + (cx - bx) * (py - cy)) / d
                q = ((cy - ay) * (px - cx) + (ax - cx) * (py - cy)) / d
                r = 1 - p - q
                if p < -0.05 or q < -0.05 or r < -0.05: continue
                height = p * A[1] + q * B[1] + r * C[1]
                i = y * w + x
                if height <= top[i]: continue
                top[i] = height
                row = rows[min(ah - 1, int(((1 - (p * A[7] + q * B[7] + r * C[7])) % 1) * ah))]
                k = min(aw - 1, int(((p * A[6] + q * B[6] + r * C[6]) % 1) * aw)) * depth
                img[i * 3:i * 3 + 3] = bytes((row[k + 2], row[k + 1], row[k]))
    # Holes: the same spot 40 m along the quay, where the rails carry straight on; then the nearest colour.
    shift = int(40 * sx)
    hole = [i for i in range(w * h) if top[i] < -1e8]
    for i in hole:
        for j in (i - shift, i + shift):
            if 0 <= j < w * h and top[j] > -1e8 and j // w == i // w:
                img[i * 3:i * 3 + 3] = img[j * 3:j * 3 + 3]
                top[i] = -1e7
                break
    edge = deque(i for i in range(w * h) if top[i] > -1e8)
    while edge:
        i = edge.popleft()
        x = i % w
        for j in ((i - 1) if x else -1, (i + 1) if x < w - 1 else -1, i - w, i + w):
            if 0 <= j < w * h and top[j] < -1e8:
                top[j] = -1e7
                img[j * 3:j * 3 + 3] = img[i * 3:i * 3 + 3]
                edge.append(j)
    return png(w, h, img)

def wall_picture():
    """Quay wall, foot at the bottom: wet and dark to a little over the sea, weathered concrete and two pours above it."""
    w, h = 256, 256
    r = random.Random(7)
    streak = [r.uniform(-14, 10) for _ in range(w)]
    img = bytearray()
    sea = (WATER - TOE) / (DECK - TOE)
    for y in range(h):
        up = 1 - y / (h - 1)
        wet = max(0.0, min(1.0, (sea + 0.14 - up) / 0.1))
        for x in range(w):
            n = r.uniform(-7, 7) + streak[x] * (1 - up * 0.5)
            c = [150 + n, 146 + n, 137 + n]
            c = [c[i] * (1 - wet) + (46, 52, 46)[i] * wet + r.uniform(-3, 3) * wet for i in range(3)]
            if x % 128 < 3 or abs(up - 0.86) < 0.006: c = [q * 0.62 for q in c]
            img += bytes(max(0, min(255, int(q))) for q in c)
    return png(w, h, img)

class Drawn:
    """Faces of a drawn model, each with its own corners."""
    def __init__(s): s.v, s.t = [], {}
    def quad(s, k, q, uvs=((0, 0), (0, 1), (1, 1), (1, 0))):
        a, b, c = q[0], q[1], q[2]
        ux, uy, uz, vx, vy, vz = b[0] - a[0], b[1] - a[1], b[2] - a[2], c[0] - a[0], c[1] - a[1], c[2] - a[2]
        n = (uy * vz - uz * vy, uz * vx - ux * vz, ux * vy - uy * vx)
        l = math.sqrt(sum(x * x for x in n)) or 1
        n = tuple(x / l for x in n)
        i = len(s.v)
        s.v += [tuple(p) + n + tuple(w) for p, w in zip(q, uvs)]
        s.t.setdefault(k, [])
        s.t[k] += [(i, i + 1, i + 2), (i, i + 2, i + 3)]
    def box(s, k, x0, x1, y0, y1, z0, z1):
        s.quad(k, ((x1, y0, z0), (x1, y1, z0), (x1, y1, z1), (x1, y0, z1)))
        s.quad(k, ((x0, y0, z1), (x0, y1, z1), (x0, y1, z0), (x0, y0, z0)))
        s.quad(k, ((x0, y1, z0), (x0, y1, z1), (x1, y1, z1), (x1, y1, z0)))
        s.quad(k, ((x1, y0, z1), (x1, y1, z1), (x0, y1, z1), (x0, y0, z1)))
        s.quad(k, ((x0, y0, z0), (x0, y1, z0), (x1, y1, z0), (x1, y0, z0)))
    def post(s, k, x, z, y0, y1, r, sides=8):
        for i in range(sides):
            a, b = 2 * math.pi * i / sides, 2 * math.pi * (i + 1) / sides
            p, q = (x + r * math.sin(a), z + r * math.cos(a)), (x + r * math.sin(b), z + r * math.cos(b))
            s.quad(k, ((p[0], y0, p[1]), (p[0], y1, p[1]), (q[0], y1, q[1]), (q[0], y0, q[1])))
            s.quad(k, ((x, y1, z), (x, y1, z), (q[0], y1, q[1]), (p[0], y1, p[1])))
    def beam(s, k, p, q, r):
        """A square tube between two points."""
        d = tuple(b - a for a, b in zip(p, q))
        l = math.sqrt(sum(x * x for x in d)) or 1
        d = tuple(x / l for x in d)
        e = (0.0, 0.0, 1.0) if abs(d[2]) < 0.9 else (1.0, 0.0, 0.0)
        a = (d[1] * e[2] - d[2] * e[1], d[2] * e[0] - d[0] * e[2], d[0] * e[1] - d[1] * e[0])
        la = math.sqrt(sum(x * x for x in a))
        a = tuple(x / la for x in a)
        b = (d[1] * a[2] - d[2] * a[1], d[2] * a[0] - d[0] * a[2], d[0] * a[1] - d[1] * a[0])
        ring = [tuple((a[i] * m + b[i] * n) * r for i in range(3)) for m, n in ((1, 1), (-1, 1), (-1, -1), (1, -1))]
        for i in range(4):
            m, n = ring[i], ring[(i + 1) % 4]
            s.quad(k, (tuple(p[j] + m[j] for j in range(3)), tuple(q[j] + m[j] for j in range(3)), tuple(q[j] + n[j] for j in range(3)), tuple(p[j] + n[j] for j in range(3))))

def walls(d, ring, top, foot):
    """Upright concrete along a ring of points, facing the way their order gives."""
    for p, q in zip(ring, ring[1:] + ring[:1]):
        l = math.hypot(q[0] - p[0], q[1] - p[1])
        n = max(1, round(l / 16))
        for i in range(n):
            a = (p[0] + (q[0] - p[0]) * i / n, p[1] + (q[1] - p[1]) * i / n)
            b = (p[0] + (q[0] - p[0]) * (i + 1) / n, p[1] + (q[1] - p[1]) * (i + 1) / n)
            d.quad('wall', ((a[0], foot, a[1]), (a[0], top, a[1]), (b[0], top, b[1]), (b[0], foot, b[1])), ((0, 0), (0, 1), (1, 1), (1, 0)))

def outward(x0, x1, z0, z1): return [(x1, z0), (x1, z1), (x0, z1), (x0, z0)]

def fittings(d, p, q, top, water, side, pitch=12.0, line=True):
    """Coping, a painted line, rubber fenders and bollards along an edge, the water towards `side`, a unit vector."""
    l = math.hypot(q[0] - p[0], q[1] - p[1])
    t = ((q[0] - p[0]) / l, (q[1] - p[1]) / l)
    def at(s, o): return (p[0] + t[0] * s + side[0] * o, p[1] + t[1] * s + side[1] * o)
    def strip(k, o0, o1, y0, y1):
        a, b, c, e = at(0, o0), at(l, o0), at(l, o1), at(0, o1)
        d.box(k, min(a[0], b[0], c[0], e[0]), max(a[0], b[0], c[0], e[0]), y0, y1, min(a[1], b[1], c[1], e[1]), max(a[1], b[1], c[1], e[1]))
    strip('cope', -0.8, 0.05, top - 0.1, top + 0.14)
    if line: strip('line', -1.5, -1.1, top, top + 0.015)
    s = pitch / 2
    while s < l:
        a, b = at(s - 0.6, 0.0), at(s + 0.6, 0.4)
        d.box('rubber', min(a[0], b[0]), max(a[0], b[0]), water - 0.4, top - 0.5, min(a[1], b[1]), max(a[1], b[1]))
        if round((s - pitch / 2) / pitch) % 2 == 0:
            c = at(s, -1.9)
            d.post('iron', c[0], c[1], top, top + 0.55, 0.22)
            d.post('iron', c[0], c[1], top + 0.55, top + 0.68, 0.33)
        s += pitch

def plain(deck, wall): return {'deck': (deck, None), 'wall': (wall, None), 'cope': (None, CONCRETE), 'line': (None, LINE), 'rubber': (None, RUBBER), 'iron': (None, IRON)}

def dock(deck, wall):
    """The piers, square, at their own height. y 0 is the foot of the walls."""
    d = Drawn()
    top, water = DECK - TOE, WATER - TOE
    cx, cz = (X0 + X1) / 2, (Z0 + Z1) / 2
    gx0, gx1, gz0, gz1 = GREEN_BASIN
    bx0, bx1, bz0, bz1 = BULK_BASIN
    def slab(x0, x1, z0, z1):
        a, b, c, e = uv(x0, z0), uv(x0, z1), uv(x1, z1), uv(x1, z0)
        d.quad('deck', ((x0 - cx, top, z0 - cz), (x0 - cx, top, z1 - cz), (x1 - cx, top, z1 - cz), (x1 - cx, top, z0 - cz)), (a, b, c, e))
    slab(X0, X1, Z0, gz0); slab(X0, X1, gz1, bz0); slab(X0, X1, bz1, Z1)
    slab(X0, gx0, gz0, gz1); slab(gx1, X1, gz0, gz1); slab(X0, bx0, bz0, bz1); slab(bx1, X1, bz0, bz1)
    m = lambda ring: [(x - cx, z - cz) for x, z in ring]
    walls(d, m(outward(X0, X1, Z0, Z1)), top, 0.0)
    walls(d, m(outward(gx0, gx1, gz0, gz1))[::-1], top, 0.0)
    walls(d, m(outward(bx0, bx1, bz0, bz1))[::-1], top, 0.0)
    far = {k: list(t) for k, t in d.t.items()}
    for b in (GREEN_BASIN, BULK_BASIN):
        x0, x1, z0, z1 = b[0] - cx, b[1] - cx, b[2] - cz, b[3] - cz
        for p, q, side in (((x0, z0), (x1, z0), (0, 1)), ((x0, z1), (x1, z1), (0, -1)), ((x0, z0), (x0, z1), (1, 0)), ((x1, z0), (x1, z1), (-1, 0))):
            fittings(d, p, q, top, water, side)
    x0, x1, z0, z1 = X0 - cx, X1 - cx, Z0 - cz, Z1 - cz
    for p, q, side in (((x0, z0), (x1, z0), (0, -1)), ((x0, z1), (x1, z1), (0, 1)), ((x0, z0), (x0, z1), (-1, 0)), ((x1, z0), (x1, z1), (1, 0))):
        fittings(d, p, q, top, water, side, pitch=24.0, line=False)
    return d, far, (X1 - X0, top + 0.2, Z1 - Z0)

def wharf():
    """One length of wharf, sea towards +z. Lengths join end to end; a dark joint marks where each stops."""
    d = Drawn()
    top, water = DECK - TOE, WATER - TOE
    hx, hz = WHARF_LEN / 2, WHARF_WIDE / 2
    sx, sz = WHARF_STRIP
    def tex(x, z): return uv(sx + x, sz - z)
    d.quad('deck', ((-hx, top, -hz), (-hx, top, hz), (hx, top, hz), (hx, top, -hz)), (tex(-hx, -hz), tex(-hx, hz), tex(hx, hz), tex(hx, -hz)))
    walls(d, outward(-hx, hx, -hz, hz), top, 0.0)
    d.box('rubber', -hx, -hx + 0.25, top, top + 0.012, -hz, hz)
    far = {k: list(t) for k, t in d.t.items()}
    fittings(d, (-hx, hz), (hx, hz), top, water, (0, 1), pitch=10.0)
    fittings(d, (-hx, -hz), (hx, -hz), top, water, (0, -1), pitch=20.0, line=False)
    return d, far, (WHARF_LEN, top + 0.2, WHARF_WIDE)

def ramp():
    """The way up from the land to a wharf: high end towards +z."""
    d = Drawn()
    top, low = DECK - TOE, LAND_Y - WATER_Y + (WATER - TOE)
    hx, hz = RAMP_WIDE / 2, RAMP_LEN / 2
    sx, sz = WHARF_STRIP
    def tex(x, z): return uv(sx + 8 + x, sz + z * 0.5)
    d.quad('deck', ((-hx, low, -hz), (-hx, top, hz), (hx, top, hz), (hx, low, -hz)), (tex(-hx, -hz), tex(-hx, hz), tex(hx, hz), tex(hx, -hz)))
    for x, flip in ((hx, False), (-hx, True)):
        q = ((x, 0.0, -hz), (x, low, -hz), (x, top, hz), (x, 0.0, hz))
        d.quad('wall', q[::-1] if flip else q, ((0, 0), (0, .5), (1, 1), (1, 0))[::-1] if flip else ((0, 0), (0, .5), (1, 1), (1, 0)))
    d.quad('wall', ((-hx, 0.0, -hz), (-hx, low, -hz), (hx, low, -hz), (hx, 0.0, -hz)), ((0, 0), (0, .4), (1, .4), (1, 0)))
    far = {k: list(t) for k, t in d.t.items()}
    for x in (hx - 0.15, -hx + 0.15):
        d.beam('iron', (x, low + 1.05, -hz), (x, top + 1.05, hz), 0.05)
        for i in range(5):
            z = -hz + RAMP_LEN * i / 4
            y = low + (top - low) * i / 4
            d.beam('iron', (x, y, z), (x, y + 1.05, z), 0.05)
    return d, far, (RAMP_WIDE, top + 1.1, RAMP_LEN)

def boom(d):
    """The crane's boom, of which the scan keeps a few dots: four chords, deepest in the middle, braced, from the machine house to the scan's own boom head; and the stay to the A-frame."""
    (fx, fy), (hx, hy) = BOOM_FOOT, BOOM_HEAD
    l = math.hypot(hx - fx, hy - fy)
    ax, ay = (hx - fx) / l, (hy - fy) / l
    nx, ny = -ay, ax
    nodes = []
    n = 9
    for i in range(n + 1):
        t = i / n
        deep = 3.4 * math.sin(math.pi * t) ** 0.7 / 2 + 0.3
        wide = 2.6 - 1.5 * t
        x, y = fx + ax * l * t, fy + ay * l * t
        nodes.append([(x + nx * deep, y + ny * deep, -wide), (x + nx * deep, y + ny * deep, wide), (x - nx * deep, y - ny * deep, wide), (x - nx * deep, y - ny * deep, -wide)])
    for a, b in zip(nodes, nodes[1:]):
        for i in range(4):
            d.beam('blue', a[i], b[i], 0.2)
            d.beam('blue', a[i], b[(i + 1) % 4], 0.07)
    for a in nodes[1:-1]:
        for i in range(4): d.beam('blue', a[i], a[(i + 1) % 4], 0.08)
    for z in (-0.9, 0.9): d.beam('iron', (STAY_TOP[0], STAY_TOP[1], z * 2), (hx, hy, z), 0.06)

def crane(city, v, tris, mats, atlas):
    """One crane from the scan, with its boom over whatever it is set facing: +z. y 0 is the deck it stands on."""
    from maritime import drop_parts, joined
    t = {k: [q for q in ts if in_box(centre(v, q), CRANE_BOX) and centre(v, q)[1] > DECK + 0.65] for k, ts in tris.items()}
    t = drop_parts(v, t, lambda lo, hi, span: span < 8.0)[0]
    v2, t = city.compact(v, t)
    pole = [p for p in v2 if 22 < p[1] < 38]
    px, pz = sum(p[0] for p in pole) / len(pole), sum(p[2] for p in pole) / len(pole)
    # The maker's name and badge on the machine house's sides go: those faces are plain blue.
    sides = {}
    for k, ts in t.items():
        for q in ts:
            a, b, c = v2[q[0]], v2[q[1]], v2[q[2]]
            ux, uy, uz, wx, wy, wz = b[0] - a[0], b[1] - a[1], b[2] - a[2], c[0] - a[0], c[1] - a[1], c[2] - a[2]
            n = (uy * wz - uz * wy, uz * wx - ux * wz, ux * wy - uy * wx)
            l = math.sqrt(sum(x * x for x in n)) or 1
            sides.setdefault('blue' if 44 < centre(v2, q)[1] < 59 and abs(n[2]) / l > 0.6 else k, []).append(q)
    t = sides
    d = Drawn()
    boom(d)
    for z in (-6.2, 6.2): d.box('blue', px - 5.0, px + 5.0, DECK, DECK + 1.9, z - 0.8, z + 0.8) # The bogies on the rails: the scan's feet stop short of the deck.
    n = len(v2)
    v2 = v2 + [(p[0], p[1], p[2] + pz) + tuple(p[3:]) for p in d.v]
    drawn = {k: [tuple(i + n for i in q) for q in ts] for k, ts in d.t.items()}
    def turned(p):
        x, y, z = p[0] - px, p[1] - DECK, p[2] - pz
        if p[1] <= SLEW: return (x, y, z) + tuple(p[3:])
        return (-z, y, x, -p[5], p[4], p[3], p[6], p[7])
    v2 = [turned(p) for p in v2]
    near = joined(city, reduce.collapse(v2, t, 9000, reach=0.6, hold=3, slip=0.006, loose=True), v2, drawn)
    keep = {k: [q for i, q in enumerate(ts) if i // 2 % 3 == 0 or k == 'iron'] for k, ts in drawn.items()}
    far = joined(city, reduce.collapse(v2, t, 2500, reach=2.0, hold=2, slip=0.02, loose=True), v2, keep)
    xs, ys, zs = [p[0] for p in near[0]], [p[1] for p in near[0]], [p[2] for p in near[0]]
    size = (max(xs) - min(xs), max(ys), max(zs) - min(zs))
    return near, far, size

def smoothed(v, tris, y0, y1, turns=14):
    """The hull's sides, up to the deck edge, with the scan's dents taken out: Taubin's two-step smoothing, which does not shrink it. Pictures stay where they are on the surface."""
    node, at = {}, []
    for p in v: at.append(node.setdefault((round(p[0], 3), round(p[1], 3), round(p[2], 3)), len(node)))
    pos = [list(k) for k in node]
    near = [set() for _ in pos]
    upright = [0] * len(pos)
    for ts in tris.values():
        for q in ts:
            a, b, c = at[q[0]], at[q[1]], at[q[2]]
            near[a] |= {b, c}; near[b] |= {a, c}; near[c] |= {a, b}
            A, B, C = pos[a], pos[b], pos[c]
            ux, uy, uz, wx, wy, wz = B[0] - A[0], B[1] - A[1], B[2] - A[2], C[0] - A[0], C[1] - A[1], C[2] - A[2]
            n = (uy * wz - uz * wy, uz * wx - ux * wz, ux * wy - uy * wx)
            l = math.sqrt(sum(x * x for x in n)) or 1
            if abs(n[1]) / l < 0.75:
                for i in (a, b, c): upright[i] += 1
    free = [i for i, p in enumerate(pos) if y0 < p[1] < y1 and upright[i] and near[i]]
    for turn in range(turns * 2):
        k = 0.55 if turn % 2 == 0 else -0.58
        step = []
        for i in free:
            m = near[i]
            n = len(m)
            step.append((i, [sum(pos[j][d] for j in m) / n - pos[i][d] for d in range(3)]))
        for i, d in step:
            p = pos[i]
            p[0] += k * d[0]; p[1] += k * d[1]; p[2] += k * d[2]
    return [tuple(pos[at[i]]) + tuple(p[3:]) for i, p in enumerate(v)]

def ship(city, name, s, v, tris):
    """One ship out of the scan, straightened, down to its paint line and against its wall. Returns its levels, size and place."""
    from maritime import drop_parts
    mid_z = (s['box'][2] + s['box'][3]) / 2
    def hull(c):
        if not in_box(c, s['box']) or c[1] < s['paint'] - 1.5: return False
        shape = s.get('shape')
        if not shape: return True
        for (x0, h0), (x1, h1) in zip(shape, shape[1:]):
            if x0 <= c[0] <= x1: return abs(c[2] - mid_z - 0.3) <= h0 + (h1 - h0) * (c[0] - x0) / (x1 - x0)
        return False
    t = {k: [q for q in ts if hull(centre(v, q))] for k, ts in tris.items()}
    t = drop_parts(v, t, lambda lo, hi, span: span < 6.0)[0]
    v2, t = city.compact(v, t)
    v2 = smoothed(v2, t, s['paint'] - 1.4, s['rim'])
    # Parallel with the wall: the hull's middle line, from its widest part.
    bins = {}
    for p in v2:
        if p[1] > s['paint'] + 2:
            b = bins.setdefault(int(p[0] // 8), [1e9, -1e9])
            b[0], b[1] = min(b[0], p[2]), max(b[1], p[2])
    xs = sorted(bins)
    mid = xs[len(xs) // 5:-len(xs) // 5]
    pts = [(k * 8 + 4.0, (bins[k][0] + bins[k][1]) / 2) for k in mid]
    mx, mz = sum(p[0] for p in pts) / len(pts), sum(p[1] for p in pts) / len(pts)
    slope = sum((p[0] - mx) * (p[1] - mz) for p in pts) / sum((p[0] - mx) ** 2 for p in pts)
    ca, sa = math.cos(math.atan(slope)), math.sin(math.atan(slope))
    v2 = [(mx + (p[0] - mx) * ca + (p[2] - mz) * sa, p[1], mz - (p[0] - mx) * sa + (p[2] - mz) * ca) + tuple(p[3:]) for p in v2]
    x0, x1 = min(p[0] for p in v2), max(p[0] for p in v2)
    y0, y1 = min(p[1] for p in v2), max(p[1] for p in v2)
    z0, z1 = min(p[2] for p in v2), max(p[2] for p in v2)
    cx, cz = (x0 + x1) / 2, (z0 + z1) / 2
    v2 = [(p[0] - cx, p[1] - y0, p[2] - cz) + tuple(p[3:]) for p in v2]
    near = reduce.normals(*reduce.collapse(v2, t, s['near'], reach=1.2, hold=3, slip=0.006, loose=True), crease=60)
    far = reduce.normals(*reduce.collapse(v2, reduce.without_small(v2, t, 3.0)[0], s['far'], reach=4.0, hold=2, slip=0.02, loose=True), crease=60)
    place_z = s['wall'] - s['side'] * (FENDER_GAP + (z1 - z0) / 2)
    place = dict(model=name, x=cx + s['along'] - (X0 + X1) / 2, z=place_z - (Z0 + Z1) / 2, draught=s['paint'] - y0)
    print('  %s: turned %.2f deg, %.1f m beam, %.1f m of hull under the sea, %.1f m lower than the scan stood it' % (name, math.degrees(math.atan(slope)), z1 - z0, s['paint'] - y0, s['paint'] - WATER))
    return near, far, (x1 - x0, y1 - y0, z1 - z0), place

def models(city, v, tris, mats, textures, named, out_json):
    atlas_data = next(p for p, c in mats.values() if p)
    atlas = pixels(atlas_data)
    deck, wall = deck_picture(v, tris, atlas), wall_picture()
    out = []
    def add(name, use, levels, material, size):
        own = {}
        n, f = levels
        out.append(named(city.finish(name, 'port', use, n[0], n[1], material, size, own, levels=[n, f]), textures, own, lambda data: 'wall_' if data == wall else 'yard_'))
    pm = plain(deck, wall)
    d, far, size = dock(deck, wall)
    add('vessel_yard', 'yard', [(d.v, d.t), city.compact(d.v, far)], pm, size)
    d, far, size = wharf()
    add('quay_wharf', 'quay', [(d.v, d.t), city.compact(d.v, far)], pm, size)
    d, far, size = ramp()
    add('quay_ramp', 'quay', [(d.v, d.t), city.compact(d.v, far)], pm, size)
    near, far, size = crane(city, v, tris, mats, atlas)
    add('port_crane', 'crane', [near, far], {**mats, 'blue': (None, BLUE), 'iron': (None, IRON)}, size)
    cx, cz = (X0 + X1) / 2, (Z0 + Z1) / 2
    table = dict(deck=DECK - WATER, wall_foot=WATER - TOE, wharf=[WHARF_LEN, WHARF_WIDE], ramp=[RAMP_WIDE, RAMP_LEN], cranes=[[x - cx, z - cz] for x, z in CRANE_SPOTS], ships=[])
    for name, s in SHIPS.items():
        near, far, size, place = ship(city, name, s, v, tris)
        add(name, 'ship', [near, far], mats, size)
        table['ships'].append(place)
    with open(out_json, 'w') as f: json.dump(table, f, indent=1)
    return out
