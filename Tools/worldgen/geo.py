#!/usr/bin/env python3
# Plane geometry for the city generator (Tools/worldgen/gen.py). Points are (x, z) tuples in metres, x east, z north. Nothing here knows about roads or chunks.
import math

def dist(a, b): return math.hypot(a[0] - b[0], a[1] - b[1])

def length(p): return sum(dist(p[i], p[i + 1]) for i in range(len(p) - 1))

def area(p):
    """Signed: positive when the points run anticlockwise seen from above with x right and z up."""
    s = 0.0
    for i in range(len(p)):
        a, b = p[i], p[(i + 1) % len(p)]
        s += a[0] * b[1] - b[0] * a[1]
    return s / 2

def centroid(p):
    a = area(p)
    if abs(a) < 1e-9: return (sum(q[0] for q in p) / len(p), sum(q[1] for q in p) / len(p))
    cx = cz = 0.0
    for i in range(len(p)):
        u, v = p[i], p[(i + 1) % len(p)]
        k = u[0] * v[1] - v[0] * u[1]
        cx += (u[0] + v[0]) * k
        cz += (u[1] + v[1]) * k
    return (cx / (6 * a), cz / (6 * a))

def inside(pt, poly):
    x, z = pt
    hit = False
    j = len(poly) - 1
    for i in range(len(poly)):
        xi, zi = poly[i]
        xj, zj = poly[j]
        if (zi > z) != (zj > z) and x < (xj - xi) * (z - zi) / (zj - zi) + xi: hit = not hit
        j = i
    return hit

def seg_point(a, b, p):
    """Nearest point on segment ab to p: (distance, t, point)."""
    ux, uz = b[0] - a[0], b[1] - a[1]
    ll = ux * ux + uz * uz
    t = 0.0 if ll < 1e-12 else max(0.0, min(1.0, ((p[0] - a[0]) * ux + (p[1] - a[1]) * uz) / ll))
    q = (a[0] + ux * t, a[1] + uz * t)
    return dist(p, q), t, q

def seg_cross(a, b, c, d):
    """Where segments ab and cd cross, as (t on ab, u on cd), or None. Parallel segments do not cross."""
    rx, rz = b[0] - a[0], b[1] - a[1]
    sx, sz = d[0] - c[0], d[1] - c[1]
    den = rx * sz - rz * sx
    if abs(den) < 1e-12: return None
    t = ((c[0] - a[0]) * sz - (c[1] - a[1]) * sx) / den
    u = ((c[0] - a[0]) * rz - (c[1] - a[1]) * rx) / den
    if -1e-9 <= t <= 1 + 1e-9 and -1e-9 <= u <= 1 + 1e-9: return t, u
    return None

def catmull(ctrl, step=25.0, closed=False):
    """A smooth line through the control points, about step metres between points."""
    p = list(ctrl)
    n = len(p)
    out = []
    last = n if closed else n - 1
    for i in range(last):
        p0 = p[(i - 1) % n] if (closed or i > 0) else p[0]
        p1, p2 = p[i], p[(i + 1) % n]
        p3 = p[(i + 2) % n] if (closed or i + 2 < n) else p[n - 1]
        k = max(2, int(dist(p1, p2) / step))
        for j in range(k):
            t = j / k
            t2, t3 = t * t, t * t * t
            out.append(tuple(0.5 * (2 * p1[c] + (p2[c] - p0[c]) * t + (2 * p0[c] - 5 * p1[c] + 4 * p2[c] - p3[c]) * t2 + (3 * p1[c] - p0[c] - 3 * p2[c] + p3[c]) * t3) for c in (0, 1)))
    out.append(out[0] if closed else p[n - 1])
    return out

def left_of(a, b):
    """Unit vector to the left of travel from a to b."""
    dx, dz = b[0] - a[0], b[1] - a[1]
    l = math.hypot(dx, dz) or 1.0
    return (-dz / l, dx / l)

def offset(p, d):
    """The line d metres to the left of p (right when negative), keeping its point count."""
    closed = dist(p[0], p[-1]) < 1e-6
    n = len(p)
    out = []
    for i in range(n):
        if closed and i in (0, n - 1): a, b = p[n - 2], p[1]
        else: a, b = p[max(i - 1, 0)], p[min(i + 1, n - 1)]
        l = left_of(a, b)
        out.append((p[i][0] + l[0] * d, p[i][1] + l[1] * d))
    return out

def clip(poly, x0, z0, x1, z1):
    """Sutherland-Hodgman to a rectangle. Exact for convex polygons, which is all it is given."""
    def edge(pts, keep, cut):
        out = []
        for i in range(len(pts)):
            a, b = pts[i], pts[(i + 1) % len(pts)]
            ka, kb = keep(a), keep(b)
            if ka: out.append(a)
            if ka != kb: out.append(cut(a, b))
        return out
    def at_x(x): return lambda a, b: (x, a[1] + (b[1] - a[1]) * (x - a[0]) / (b[0] - a[0]))
    def at_z(z): return lambda a, b: (a[0] + (b[0] - a[0]) * (z - a[1]) / (b[1] - a[1]), z)
    p = edge(poly, lambda q: q[0] >= x0, at_x(x0))
    if p: p = edge(p, lambda q: q[0] <= x1, at_x(x1))
    if p: p = edge(p, lambda q: q[1] >= z0, at_z(z0))
    if p: p = edge(p, lambda q: q[1] <= z1, at_z(z1))
    return p

def axis(poly):
    """The direction a shape is longest in, from the spread of its outline: unit (x, z)."""
    c = (sum(p[0] for p in poly) / len(poly), sum(p[1] for p in poly) / len(poly))
    sxx = szz = sxz = 0.0
    for i in range(len(poly)):
        a, b = poly[i], poly[(i + 1) % len(poly)]
        w = dist(a, b)
        m = ((a[0] + b[0]) / 2 - c[0], (a[1] + b[1]) / 2 - c[1])
        sxx += w * m[0] * m[0]
        szz += w * m[1] * m[1]
        sxz += w * m[0] * m[1]
    t = 0.5 * math.atan2(2 * sxz, sxx - szz)
    return (math.cos(t), math.sin(t))

def extent(poly, u):
    """How far the shape runs along direction u: (lowest, highest)."""
    s = [p[0] * u[0] + p[1] * u[1] for p in poly]
    return min(s), max(s)

class Grid:
    """Things with bounding boxes in square cells, to find what is near a point or box."""
    def __init__(s, cell=40.0):
        s.cell, s.cells = cell, {}
    def keys(s, x0, z0, x1, z1):
        c = s.cell
        for i in range(int(math.floor(x0 / c)), int(math.floor(x1 / c)) + 1):
            for j in range(int(math.floor(z0 / c)), int(math.floor(z1 / c)) + 1):
                yield (i, j)
    def add(s, item, x0, z0, x1, z1):
        for k in s.keys(x0, z0, x1, z1): s.cells.setdefault(k, []).append(item)
    def near(s, x0, z0, x1, z1):
        seen = set()
        for k in s.keys(x0, z0, x1, z1):
            for it in s.cells.get(k, ()):
                if id(it) not in seen:
                    seen.add(id(it))
                    yield it

def triangulate(outer, hole=None):
    """Triangles of a simple polygon, with at most one hole, by ear clipping. outer anticlockwise; the hole any way round."""
    p = list(outer)
    if area(p) < 0: p.reverse()
    if hole:
        h = list(hole)
        if area(h) > 0: h.reverse()
        i = max(range(len(h)), key=lambda k: h[k][0])
        m = h[i]
        best, k = 1e18, 0
        # Nearest vertex of the outline that can see the hole's easternmost point.
        order = sorted(range(len(p)), key=lambda v: dist(p[v], m))
        for v in order:
            ok = True
            for j in range(len(p)):
                a, b = p[j], p[(j + 1) % len(p)]
                if v in (j, (j + 1) % len(p)): continue
                if seg_cross(m, p[v], a, b): ok = False; break
            if ok:
                for j in range(len(h)):
                    a, b = h[j], h[(j + 1) % len(h)]
                    if i in (j, (j + 1) % len(h)): continue
                    if seg_cross(m, p[v], a, b): ok = False; break
            if ok:
                k = v
                break
        p = p[:k + 1] + h[i:] + h[:i + 1] + p[k:]
    idx = list(range(len(p)))
    out = []
    def cross(a, b, c): return (b[0] - a[0]) * (c[1] - a[1]) - (b[1] - a[1]) * (c[0] - a[0])
    guard = 0
    while len(idx) > 3 and guard < 200000:
        n = len(idx)
        clipped = False
        for i in range(n):
            guard += 1
            ia, ib, ic = idx[i - 1], idx[i], idx[(i + 1) % n]
            a, b, c = p[ia], p[ib], p[ic]
            if cross(a, b, c) <= 1e-9: continue
            ear = True
            for j in idx:
                if j in (ia, ib, ic): continue
                q = p[j]
                if q == a or q == b or q == c: continue
                if cross(a, b, q) >= 0 and cross(b, c, q) >= 0 and cross(c, a, q) >= 0:
                    ear = False
                    break
            if ear:
                out.append((a, b, c))
                del idx[i]
                clipped = True
                break
        if not clipped:
            # Only collinear or doubled points remain here: drop the flattest and go on.
            i = min(range(n), key=lambda i: abs(cross(p[idx[i - 1]], p[idx[i]], p[idx[(i + 1) % n]])))
            del idx[i]
    if len(idx) == 3: out.append(tuple(p[i] for i in idx))
    return out
