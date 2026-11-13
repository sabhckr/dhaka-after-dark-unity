#!/usr/bin/env python3
# A lighter mesh by edge collapse, for hard-surfaced models on a picture atlas (2026-11-06). Written after the metro car's cab tore: vertex clustering snaps every corner in a
# grid cell to one point, so a painted window frame or a mask's edge, whose lines run across cells, comes out ragged. Here a corner either stays exactly where the source has it
# or goes, on to a neighbour along an edge. Seams of the picture, open edges, colour borders and creases are held by planes, so outlines stay, and a collapse that would slide
# the picture under the surface, flip a face or tear a seam is refused.
import heapq, math

def plane(p, q, r):
    ux, uy, uz = q[0] - p[0], q[1] - p[1], q[2] - p[2]
    wx, wy, wz = r[0] - p[0], r[1] - p[1], r[2] - p[2]
    nx, ny, nz = uy * wz - uz * wy, uz * wx - ux * wz, ux * wy - uy * wx
    l = math.sqrt(nx * nx + ny * ny + nz * nz)
    if l < 1e-14: return None
    nx, ny, nz = nx / l, ny / l, nz / l
    return nx, ny, nz, -(nx * p[0] + ny * p[1] + nz * p[2])

def quadric(a, b, c, d, w=1.0):
    return [w * a * a, w * a * b, w * a * c, w * a * d, w * b * b, w * b * c, w * b * d, w * c * c, w * c * d, w * d * d]

def cost(q, p):
    x, y, z = p
    return q[0] * x * x + 2 * q[1] * x * y + 2 * q[2] * x * z + 2 * q[3] * x + q[4] * y * y + 2 * q[5] * y * z + 2 * q[6] * y + q[7] * z * z + 2 * q[8] * z + q[9]

def weld(v, tris, fix=True):
    """Corners with the same place and picture spot become one, whatever normals they came with. Faces wound against the normals they came with are turned, and counted."""
    spot, out, new, turned = {}, [], {}, 0
    fix = fix and len({tuple(p[3:6]) for p in v[:4000]}) > 1 # Corners that all share one normal never came with any.
    for k, t in tris.items():
        for tri in t:
            a, b, c = (v[i] for i in tri)
            n = plane(a, b, c)
            if n is None: continue
            s = [(x[3], x[4], x[5]) for x in (a, b, c)]
            r = []
            for p in (a, b, c):
                key = (k, round(p[0], 4), round(p[1], 4), round(p[2], 4), round(p[6], 5), round(p[7], 5))
                i = spot.get(key)
                if i is None:
                    i = spot[key] = len(out)
                    out.append(p)
                r.append(i)
            if r[0] == r[1] or r[1] == r[2] or r[0] == r[2]: continue
            if fix and sum(n[0] * x[0] + n[1] * x[1] + n[2] * x[2] for x in s) < 0:
                r.reverse()
                turned += 1
            new.setdefault(k, []).append(tuple(r))
    return out, new, turned

def normals(v, tris, crease=38.0):
    """Corner normals from the faces round each place, across seams of the picture and not across creases. Corners split where a crease needs it."""
    cos = math.cos(math.radians(crease))
    node, at = {}, []
    for p in v: at.append(node.setdefault((round(p[0], 4), round(p[1], 4), round(p[2], 4)), len(node)))
    faces = [[] for _ in node]
    for k, t in tris.items():
        for tri in t:
            n = plane(v[tri[0]], v[tri[1]], v[tri[2]])
            if n is None: continue
            p, q, r = (v[i] for i in tri)
            a = math.sqrt(sum(x * x for x in ((q[1] - p[1]) * (r[2] - p[2]) - (q[2] - p[2]) * (r[1] - p[1]), (q[2] - p[2]) * (r[0] - p[0]) - (q[0] - p[0]) * (r[2] - p[2]), (q[0] - p[0]) * (r[1] - p[1]) - (q[1] - p[1]) * (r[0] - p[0]))))
            for i in tri: faces[at[i]].append((n[0], n[1], n[2], a))
    out, slot, new = [], {}, {}
    for k, t in tris.items():
        for tri in t:
            n = plane(v[tri[0]], v[tri[1]], v[tri[2]])
            if n is None: continue
            r = []
            for i in tri:
                x = y = z = 0.0
                for f in faces[at[i]]:
                    if f[0] * n[0] + f[1] * n[1] + f[2] * n[2] >= cos: x += f[0] * f[3]; y += f[1] * f[3]; z += f[2] * f[3]
                l = math.sqrt(x * x + y * y + z * z) or 1.0
                m = (round(x / l, 3), round(y / l, 3), round(z / l, 3))
                j = slot.get((i, m))
                if j is None:
                    j = slot[(i, m)] = len(out)
                    p = v[i]
                    out.append(tuple(p[:3]) + m + tuple(p[6:]))
                r.append(j)
            new.setdefault(k, []).append(tuple(r))
    return out, new

def collapse(v, tris, target, reach=0.03, slip=0.004, hold=40.0, crease=30.0, loose=False, log=None):
    """v and tris down towards target triangles, no corner leaving the source's surface by more than about reach (metres), the picture sliding at most slip of its width.
    Stops at whichever comes first, so a model can end above its target: the caller reads the count. loose lets a seam of the picture close too, each side keeping its own spot on
    the picture at the new place: for a level seen from further off, or a scan cut into many small patches."""
    v, tris, turned = weld(v, tris)
    node, at = {}, []
    for p in v: at.append(node.setdefault((round(p[0], 4), round(p[1], 4), round(p[2], 4)), len(node)))
    N = len(node)
    place = [None] * N
    for i, p in enumerate(v): place[at[i]] = p[:3]
    verts = [set() for _ in range(N)]
    for i, n in enumerate(at): verts[n].add(i)
    T = [[a, b, c, k] for k, t in tris.items() for a, b, c in t]
    live = len(T)
    around = [set() for _ in v]
    for i, t in enumerate(T):
        for x in t[:3]: around[x].add(i)
    q = [[0.0] * 10 for _ in range(N)]
    edges = {}
    planes = [None] * len(T)
    for i, t in enumerate(T):
        pl = plane(v[t[0]], v[t[1]], v[t[2]])
        planes[i] = pl
        if pl is None: continue
        k = quadric(*pl)
        for x in t[:3]:
            n = at[x]
            q[n] = [s + u for s, u in zip(q[n], k)]
        for a, b in ((t[0], t[1]), (t[1], t[2]), (t[2], t[0])):
            e = (at[a], at[b]) if at[a] < at[b] else (at[b], at[a])
            edges.setdefault(e, []).append((i, min(a, b), max(a, b)))
    held = 0
    cc = math.cos(math.radians(crease))
    for (a, b), who in edges.items():
        same = len(who) == 2 and who[0][1:] == who[1][1:] and T[who[0][0]][3] == T[who[1][0]][3]
        if same:
            p, r = planes[who[0][0]], planes[who[1][0]]
            if p is None or r is None or p[0] * r[0] + p[1] * r[1] + p[2] * r[2] >= cc: continue
        for i, _, _ in who[:2]:
            pl = planes[i]
            if pl is None: continue
            A, B = place[a], place[b]
            ex, ey, ez = B[0] - A[0], B[1] - A[1], B[2] - A[2]
            nx, ny, nz = ey * pl[2] - ez * pl[1], ez * pl[0] - ex * pl[2], ex * pl[1] - ey * pl[0]
            l = math.sqrt(nx * nx + ny * ny + nz * nz)
            if l < 1e-12: continue
            nx, ny, nz = nx / l, ny / l, nz / l
            k = quadric(nx, ny, nz, -(nx * A[0] + ny * A[1] + nz * A[2]), hold)
            q[a] = [s + u for s, u in zip(q[a], k)]
            q[b] = [s + u for s, u in zip(q[b], k)]
            held += 1
    version = [0] * N
    heap = []
    def push(a, b):
        s = [x + y for x, y in zip(q[a], q[b])]
        heapq.heappush(heap, (cost(s, place[b]), a, b, version[a], version[b]))
    for a, b in edges: push(a, b); push(b, a)
    del edges
    limit = reach * reach
    def plan(a, b):
        """Which corner at b each corner at a goes to, or None."""
        move = {}
        for i in verts[a]:
            to = set()
            for t in around[i]:
                for x in T[t][:3]:
                    if at[x] == b: to.add(x)
            if len(to) > 1: return None
            if not to:
                if not loose: return None
                move[i] = None
            else: move[i] = to.pop()
        return move
    def fits(a, b, move):
        B = place[b]
        for i, j in move.items():
            ui, wi = v[i][6], v[i][7]
            uj, wj = v[j][6], v[j][7]
            near = None
            for t in around[i]:
                c = T[t]
                if j in c[:3]: continue
                o = [x for x in c[:3] if x != i]
                if len(o) != 2: return False
                k = c.index(i)
                p0, p1 = v[c[(k + 1) % 3]], v[c[(k + 2) % 3]]
                old = plane(v[i], p0, p1)
                new = plane(v[j], p0, p1)
                if new is None: return False
                if old is not None and old[0] * new[0] + old[1] * new[1] + old[2] * new[2] < 0.5: return False
                s0 = (p0[6] - ui) * (p1[7] - wi) - (p0[7] - wi) * (p1[6] - ui)
                s1 = (p0[6] - uj) * (p1[7] - wj) - (p0[7] - wj) * (p1[6] - uj)
                if s0 * s1 <= 0: return False  # The picture would fold.
                # Where the corner's own picture spot ends up: the new triangle it now falls in says what picture it shows there.
                A = place[a]
                ex, ey, ez = p0[0] - B[0], p0[1] - B[1], p0[2] - B[2]
                fx, fy, fz = p1[0] - B[0], p1[1] - B[1], p1[2] - B[2]
                gx, gy, gz = A[0] - B[0], A[1] - B[1], A[2] - B[2]
                ee, ef, ff = ex * ex + ey * ey + ez * ez, ex * fx + ey * fy + ez * fz, fx * fx + fy * fy + fz * fz
                ge, gf = gx * ex + gy * ey + gz * ez, gx * fx + gy * fy + gz * fz
                d = ee * ff - ef * ef
                if d < 1e-18: continue
                s = (ff * ge - ef * gf) / d
                r = (ee * gf - ef * ge) / d
                if s < -0.05 or r < -0.05 or s + r > 1.05: continue
                du = uj + s * (p0[6] - uj) + r * (p1[6] - uj) - ui
                dw = wj + s * (p0[7] - wj) + r * (p1[7] - wj) - wi
                e = max(abs(du), abs(dw))
                if near is None or e < near: near = e
            if near is not None and near > slip: return False
        return True
    while live > target and heap:
        e, a, b, va, vb = heapq.heappop(heap)
        if va != version[a] or vb != version[b] or not verts[a] or not verts[b]: continue
        if e > limit: break
        move = plan(a, b)
        if move is None: continue
        for i, j in move.items():
            if j is None:
                move[i] = j = len(v)
                v.append(tuple(place[b]) + tuple(v[i][3:]))
                at.append(b)
                around.append(set())
                verts[b].add(j)
        if not fits(a, b, move): continue
        for i, j in move.items():
            for t in list(around[i]):
                c = T[t]
                if j in c[:3]:
                    for x in c[:3]: around[x].discard(t)
                    T[t] = None
                    live -= 1
                else:
                    c[c.index(i)] = j
                    around[j].add(t)
            around[i] = set()
        q[b] = [x + y for x, y in zip(q[a], q[b])]
        verts[a] = set()
        version[b] += 1
        ring = set()
        for j in verts[b]:
            for t in around[j]:
                for x in T[t][:3]:
                    if at[x] != b: ring.add(at[x])
        for n in ring: push(b, n); push(n, b)
    out, slot, new = [], {}, {}
    for t in T:
        if t is None: continue
        r = []
        for x in t[:3]:
            j = slot.get(x)
            if j is None:
                j = slot[x] = len(out)
                out.append(v[x])
            r.append(j)
        new.setdefault(t[3], []).append(tuple(r))
    if log is not None: log.update(turned=turned, held=held, tris=live, stopped=e if heap else 0)
    return out, new

def without_small(v, tris, size):
    """Tris without the separate parts smaller than size (metres) on every side: railings, ladders and aerials, for a level seen from too far to show them."""
    node, at = {}, []
    for p in v: at.append(node.setdefault((round(p[0], 3), round(p[1], 3), round(p[2], 3)), len(node)))
    up = list(range(len(node)))
    def root(a):
        while up[a] != a:
            up[a] = up[up[a]]
            a = up[a]
        return a
    for t in tris.values():
        for a, b, c in t:
            x, y, z = root(at[a]), root(at[b]), root(at[c])
            up[y] = x
            up[root(z)] = x
    box = {}
    for i, p in enumerate(v):
        r = root(at[i])
        b = box.get(r)
        if b is None: box[r] = [p[0], p[1], p[2], p[0], p[1], p[2]]
        else:
            for k in range(3):
                if p[k] < b[k]: b[k] = p[k]
                if p[k] > b[k + 3]: b[k + 3] = p[k]
    big = {r for r, b in box.items() if max(b[3] - b[0], b[4] - b[1], b[5] - b[2]) >= size}
    return {k: [q for q in t if root(at[q[0]]) in big] for k, t in tris.items()}, len(box), len(big)
