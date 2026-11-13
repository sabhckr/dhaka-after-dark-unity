#!/usr/bin/env python3
# Rigs the owner's dressed characters that came without a skeleton, or with far too many triangles, for the street crowd (NPC Phase 3, 2026-11-08). No Blender here, so it is done on the mesh:
# each model is lightened keeping its own pictures, its arms posed to the template's, a Mixamo skeleton moved to fit it and skin weights worked out by distance to the bones. The source archives
# are only read. Writes People/<name>.fbx and People/<name>/Material.png. Run with the CLT python:
#   python3 -I Tools/models/npc_rig.py [name...]
import io, math, os, re, struct, subprocess, sys, tempfile, zlib
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bake, fbxread, reduce as lighten

OUT = os.path.join(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))), 'Assets/_Project/Resources/People')
TEMPLATE = os.path.join(bake.SRC, 'mixamo/out/hoodie-rigged.fbx')
H = 170.0  # every figure is built this tall in cm; NpcCrowd scales each person to their height
TRIS = 14000

def under_suit(part, a, b, c):
    # The suit has a whole body under it; only the hands and the neck show.
    if part.name != 'AvatarBody': return True
    x = (a[0] + b[0] + c[0]) / 3; y = (a[1] + b[1] + c[1]) / 3
    return abs(x) > 0.66 or y > 1.42

MODELS = {
    'suit': dict(zip='male-character-in-suit.zip', file='source/model.fbx', size=2048, skip=('AvatarEyelashes', 'AvatarTeethLower', 'AvatarTeethUpper'), keep=under_suit,
                 rects=dict(outfit=(0, 0, 1024, 1024), AvatarHead=(1024, 0, 1024, 1024), AvatarBody=(0, 1024, 512, 512), haircut=(512, 1024, 512, 512), AvatarLeftEyeball=(1024, 1024, 256, 256), AvatarRightEyeball=(1024, 1024, 256, 256))),
    'casual': dict(zip='casual-male-character.zip', inner='source/Casual Male Character.zip', file='Casual Male Character.obj', picture='Casual Male Character_color.png', size=4096),
    'traveler': dict(zip='rugged_winter_traveler.zip', file='scene.gltf', size=2048, down=0.14, flip=True),  # a glTF counts its pictures from the top
    # A raw scan of 1,978,098 triangles: it cannot run as a person, and nothing is reduced without the owner's word (2026-11-09). Not built by default.
    'npcman': dict(zip='npc-man.zip', file='source/0-2.fbx', size=4096, down=0.17, box=1.6, reach=0.06, slip=0.03, loose=False, reduce=True),
}

def welded(v, tris):
    # Corners at the same place on the body and on the picture are one corner: nothing is lost.
    first, out, ids = {}, [], []
    for q in v:
        k = (round(q[0], 5), round(q[1], 5), round(q[2], 5), round(q[6], 5), round(q[7], 5))
        if k not in first: first[k] = len(out); out.append(q)
        ids.append(first[k])
    return out, {m: [(ids[a], ids[b], ids[c]) for a, b, c in t] for m, t in tris.items()}

def obj_part(data):
    part = fbxread.Part('obj'); P, T = [], []; seen = {}
    part.mats = [fbxread.Material('Material', None, None, None)]
    for l in data.decode('latin1').splitlines():
        if l[:2] == 'v ': P.append(tuple(map(float, l.split()[1:4])))
        elif l[:3] == 'vt ': T.append(tuple(map(float, l.split()[1:3])))
        elif l[:2] == 'f ':
            ids = []
            for c in l.split()[1:]:
                q = c.split('/'); k = (int(q[0]), int(q[1]))
                if k not in seen:
                    seen[k] = len(part.p); part.p.append(P[k[0] - 1]); part.uv.append(T[k[1] - 1]); part.n.append((0, 0, 0))
                ids.append(seen[k])
            for i in range(1, len(ids) - 1): part.tris.append((ids[0], ids[i], ids[i + 1], 0))
    return part

def parts_of(recipe):
    if recipe['file'].endswith('.obj'):
        data, picture = bake.archive(recipe['zip'], recipe['file'], recipe.get('inner'))
        part = obj_part(data); part.mats[0].picture = picture(recipe['picture'])
        return [part]
    return [p for p in bake.parts_of(recipe)[0] if p.name not in recipe.get('skip', ())]

def thin(part, step):
    # A scan or a sculpt has more triangles than the reducer can take in pure Python: corners in the same small box, at the same place on the picture, become one first.
    first = {}; new = []
    for i in range(len(part.p)):
        p = part.p[i]; u = part.uv[i]
        new.append(first.setdefault((round(p[0] / step), round(p[1] / step), round(p[2] / step), round(u[0] * 80), round(u[1] * 80)), i))
    part.tris = [(new[a], new[b], new[c], m) for a, b, c, m in part.tris if len({new[a], new[b], new[c]}) == 3]

def pictures(recipe, mats, tmp):
    S = recipe['size']; px = bytearray(S * S * 3)
    done = set()
    for m in mats:
        x0, y0, w, h = recipe.get('rects', {}).get(m.name, (0, 0, S, S))
        if (x0, y0) in done or m.picture is None: continue
        done.add((x0, y0))
        raw = os.path.join(tmp, 'in'); bmp = os.path.join(tmp, 'out.bmp')
        open(raw, 'wb').write(m.picture)
        subprocess.check_call(['/usr/bin/sips', '-s', 'format', 'bmp', '-z', str(h), str(w), raw, '--out', bmp], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        b = open(bmp, 'rb').read(); off = struct.unpack_from('<I', b, 10)[0]; bw, bh = struct.unpack_from('<ii', b, 18); bits = struct.unpack_from('<H', b, 28)[0]
        step = bits // 8; pitch = ((bw * bits + 31) // 32) * 4
        for j in range(h):
            row = off + ((h - 1 - j) if bh > 0 else j) * pitch
            for i in range(w):
                o = row + i * step; d = 3 * ((y0 + j) * S + x0 + i)
                px[d] = b[o + 2]; px[d + 1] = b[o + 1]; px[d + 2] = b[o]
    return S, px

def png(path, S, px):
    raw = b''.join(b'\0' + bytes(px[3 * y * S:3 * (y + 1) * S]) for y in range(S))
    def chunk(t, b): return struct.pack('>I', len(b)) + t + b + struct.pack('>I', zlib.crc32(t + b) & 0xffffffff)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    open(path, 'wb').write(b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', S, S, 8, 2, 0, 0, 0)) + chunk(b'IDAT', zlib.compress(raw, 9)) + chunk(b'IEND', b''))

def lightened(recipe):
    parts = parts_of(recipe)
    if 'keep' in recipe:
        for p in parts: p.tris = [t for t in p.tris if recipe['keep'](p, p.p[t[0]], p.p[t[1]], p.p[t[2]])]
    ys = [c[1] for p in parts for c in p.p]; lo, hi = min(ys), max(ys)
    xs = [c[0] for p in parts for c in p.p]; mid = (min(xs) + max(xs)) / 2
    k = H / (hi - lo) / 100  # metres while it is lightened
    all_tris = sum(len(p.tris) for p in parts)
    P, U, F, mats = [], [], [], []
    S = recipe['size']
    for part in parts:
        if recipe.get('reduce') and len(part.tris) > 120000: thin(part, recipe.get('box', 0.8) / 100 / k)
        tris = {}
        for a, b, c, m in part.tris: tris.setdefault(m, []).append((a, b, c))
        v = [((q[0] - mid) * k, (q[1] - lo) * k, q[2] * k) + (tuple(part.n[i]) if part.n else (0, 0, 0)) + ((part.uv[i][0], 1 - part.uv[i][1]) if recipe.get('flip') else tuple(part.uv[i][:2])) for i, q in enumerate(part.p)]
        if recipe.get('reduce'): out, kept = lighten.collapse(v, tris, max(60, int(TRIS * len(part.tris) / all_tris)), reach=recipe.get('reach', 0.03), slip=recipe.get('slip', 0.02), loose=recipe.get('loose', True))
        else: out, kept = welded(v, tris)
        for m, t in kept.items():
            mat = part.mats[m]; mats.append(mat); x0, y0, w, h = recipe.get('rects', {}).get(mat.name, (0, 0, S, S))
            tile = math.floor(sorted(out[i][6] for f in t for i in f)[len(t)]) if 'rects' in recipe else None
            made = {}
            for f in t:
                ids = []
                for i in f:
                    if i not in made:
                        q = out[i]; made[i] = len(P); P.append(tuple(c * 100 for c in q[:3]))
                        u_ = q[6] % 1.0 if tile is None else min(1, max(0, q[6] - tile)); v_ = q[7] % 1.0 if tile is None else min(1, max(0, q[7]))
                        U.append(((x0 + w * (0.005 + 0.99 * u_)) / S, 1 - (y0 + h * (1 - (0.005 + 0.99 * v_))) / S))
                    ids.append(made[i])
                F.append(tuple(ids))
    print('  %d source triangles to %d' % (all_tris, len(F)), flush=True)
    return P, U, F, mats

def build(name, tpl, out):
    recipe = MODELS[name]
    P, U, F, mats = lightened(recipe)
    with tempfile.TemporaryDirectory() as tmp:
        S, px = pictures(recipe, mats, tmp)
    png(os.path.join(out, name, 'Material.png'), S, px)
    fg = fit(P, F, tpl, recipe.get('down'))
    write(fg, fg.P, U, fg.F, fg.W, name, os.path.join(out, name + '.fbx'))
    print('%-10s %6d triangles, %6d corners, hips at %.1f, arm %.0f deg' % (name, len(fg.F), len(fg.P), fg.J['Hips'][1], math.degrees(fg.arm['Left']['angle'])), flush=True)

def normals(P, F):
    N = [[0.0, 0.0, 0.0] for _ in P]
    for a, b, c in F:
        A, B, C = P[a], P[b], P[c]
        e = [B[i] - A[i] for i in range(3)]; f = [C[i] - A[i] for i in range(3)]
        n = (e[1] * f[2] - e[2] * f[1], e[2] * f[0] - e[0] * f[2], e[0] * f[1] - e[1] * f[0])
        for v in (a, b, c):
            for i in range(3): N[v][i] += n[i]
    for n in N:
        l = math.sqrt(sum(x * x for x in n)) or 1
        n[0] /= l; n[1] /= l; n[2] /= l
    return N

def outward(P, F):
    # Faces wound inwards are turned, so a shell's offset goes out.
    N = normals(P, F)
    cy = sum(p[1] for p in P) / len(P)
    s = sum(n[0] * p[0] + n[1] * (p[1] - cy) + n[2] * p[2] for p, n in zip(P, N))
    return F if s > 0 else [(a, c, b) for a, b, c in F]

# ---- The template's skeleton ----

def template():
    bones = fbxread.scene(open(TEMPLATE, 'rb').read())[0].bones
    return {k: tuple(v) for k, v in bones.items()}

def sub(a, b): return [a[i] - b[i] for i in range(3)]
def add(a, b): return [a[i] + b[i] for i in range(3)]
def scale(a, s): return [c * s for c in a]
def dot(a, b): return sum(a[i] * b[i] for i in range(3))
def cross(a, b): return [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]]
def norm(a):
    l = math.sqrt(dot(a, a)) or 1
    return [c / l for c in a]
def ss(a, b, x):
    t = min(1.0, max(0.0, (x - a) / (b - a)))
    return t * t * (3 - 2 * t)

def rotate(v, axis, angle):
    c, s = math.cos(angle), math.sin(angle)
    return add(add(scale(v, c), scale(cross(axis, v), s)), scale(axis, dot(axis, v) * (1 - c)))

class Figure: pass

def fit(P, F, tpl, down=None):
    # down is for a figure standing with its arms at its sides: how far out from the middle, as a share of its height, only an arm reaches.
    fg = Figure()
    # A foot points the way its owner faces; some models were made looking down -z.
    foot = [p[2] for p in P if p[1] < 0.05 * H]; shin = [p[2] for p in P if 0.14 * H < p[1] < 0.22 * H]
    if sum(foot) / len(foot) < sum(shin) / len(shin): P = [(-p[0], p[1], -p[2]) for p in P]
    F = outward(P, F)
    T = {k: (v[0], v[1] * H / 175.0, v[2]) for k, v in tpl.items()}
    def centre(pred):
        got = [p for p in P if pred(p)]
        return [sum(c) / len(got) for c in zip(*got)] if got else [0, 0, 0]
    arm = {}
    for side, sg in (('Left', 1), ('Right', -1)):
        tip = max(P, key=lambda p: p[0] * sg)
        # The arm's line, from its points well away from the body.
        far = [p for p in P if p[0] * sg > (down or 0.2) * H and (not down or p[1] < T[side + 'Arm'][1])]
        n = len(far); mx = sum(p[0] for p in far) / n; my = sum(p[1] for p in far) / n; mz = sum(p[2] for p in far) / n
        sxx = sum((p[0] - mx) ** 2 for p in far) or 1
        ky = sum((p[0] - mx) * (p[1] - my) for p in far) / sxx
        kz = sum((p[0] - mx) * (p[2] - mz) for p in far) / sxx
        x0 = 0.115 * H * sg
        S = [x0, my + ky * (x0 - mx), mz + kz * (x0 - mx)] if not down else [x0, T[side + 'Arm'][1], mz]
        if down: tip = max(far, key=lambda p: (p[0] - S[0]) ** 2 + (p[1] - S[1]) ** 2 + (p[2] - S[2]) ** 2)
        A = norm(sub(tip, S)); L = dot(sub(tip, S), A)
        goal = norm(sub(T[side + 'Hand'], T[side + 'Arm']))
        ax = norm(cross(A, goal)); ang = 0.0 if down else math.acos(max(-1, min(1, dot(A, goal))))  # arms at the sides stay there: lifting them tears a coat
        arm[side] = dict(S=S, A=A, L=L, axis=ax, angle=ang)
    # Which corners are arm, and how far along it, in the source pose.
    fg.along = [None] * len(P)
    for i, p in enumerate(P):
        for side, a in arm.items():
            d = sub(p, a['S']); t = dot(d, a['A']) / a['L']
            rad = math.sqrt(max(0, dot(d, d) - (t * a['L']) ** 2))
            if t > -0.03 and rad < (0.05 if down else 0.085) * H and (p[0] > 0) == (side == 'Left'):
                fg.along[i] = (side, t)
    # Pose the arms like the template's.
    Q = list(P)
    for i, p in enumerate(P):
        if fg.along[i]:
            a = arm[fg.along[i][0]]
            k = ss(-0.03, 0.08, fg.along[i][1])
            Q[i] = tuple(add(a['S'], rotate(sub(p, a['S']), a['axis'], a['angle'] * k)))
    P = Q
    new = dict(Hips=[0, T['Hips'][1], 0])
    def trunk(y, band=3.0):
        c = centre(lambda p: abs(p[1] - y) < band and fg_arm_free(p))
        return c
    fg_arm_free = lambda p: abs(p[0]) < 0.13 * H
    new['Hips'][2] = trunk(T['Hips'][1])[2]
    for b in ('Spine', 'Spine1', 'Spine2', 'Neck', 'Head'): new[b] = [0, T[b][1], trunk(T[b][1])[2]]
    new['HeadTop_End'] = [0, H, new['Head'][2]]
    for side, sg in (('Left', 1), ('Right', -1)):
        a = arm[side]
        Dir = a['A'] if down else norm(sub(T[side + 'Hand'], T[side + 'Arm']))
        for b, t in (('Arm', 0), ('ForeArm', 0.42), ('Hand', 0.74)): new[side + b] = add(a['S'], scale(Dir, t * a['L']))
        new[side + 'Shoulder'] = [0.04 * H * sg, T[side + 'Shoulder'][1], new['Spine2'][2]]
        lg = lambda y: centre(lambda p: abs(p[1] - y) < 2.5 and p[0] * sg > 0 and abs(p[0]) < 0.16 * H)
        k = lg(T[side + 'Leg'][1]); an = lg(T[side + 'Foot'][1]); up = lg(T[side + 'UpLeg'][1])
        new[side + 'UpLeg'] = [up[0] * 0.9, T[side + 'UpLeg'][1], up[2]]
        new[side + 'Leg'] = [k[0], T[side + 'Leg'][1], k[2]]
        new[side + 'Foot'] = [an[0], T[side + 'Foot'][1], an[2]]
        front = max((p for p in P if p[1] < 12 and p[0] * sg > 0), key=lambda p: p[2])
        new[side + 'ToeBase'] = [an[0], 2.5, an[2] + (front[2] - an[2]) * 0.6]
        new[side + 'Toe_End'] = [an[0], 1.5, front[2]]
    fg.P, fg.F, fg.J, fg.arm = P, F, new, arm
    fg.N = normals(P, F)
    fg.adj = [set() for _ in P]
    for a, b, c in F:
        fg.adj[a] |= {b, c}; fg.adj[b] |= {a, c}; fg.adj[c] |= {a, b}
    # Corners doubled along a seam of the picture are one place on the body.
    same = {}
    for i, p in enumerate(P): same.setdefault((round(p[0], 2), round(p[1], 2), round(p[2], 2)), []).append(i)
    for g in same.values():
        if len(g) > 1:
            ring = set().union(*(fg.adj[i] for i in g))
            for i in g: fg.adj[i] = ring - {i}
    fg.W = weigh(fg)
    for g in same.values():
        if len(g) > 1:
            w = {}
            for i in g:
                for k, x in fg.W[i].items(): w[k] = w.get(k, 0) + x / len(g)
            top = sorted(w.items(), key=lambda kv: -kv[1])[:4]; t = sum(x for _, x in top)
            for i in g: fg.W[i] = {k: x / t for k, x in top}
    return fg

# ---- Skin weights ----

LEG = ('UpLeg', 'Leg', 'Foot', 'ToeBase')
CHILD = dict(Hips='Spine', Spine='Spine1', Spine1='Spine2', Spine2='Neck', Neck='Head', Head='HeadTop_End')
for s in ('Left', 'Right'):
    CHILD.update({s + 'Shoulder': s + 'Arm', s + 'Arm': s + 'ForeArm', s + 'ForeArm': s + 'Hand', s + 'UpLeg': s + 'Leg', s + 'Leg': s + 'Foot', s + 'Foot': s + 'ToeBase', s + 'ToeBase': s + 'Toe_End'})

def seg_dist(p, a, b):
    d = sub(b, a); t = max(0, min(1, dot(sub(p, a), d) / (dot(d, d) or 1)))
    q = add(a, scale(d, t)); e = sub(p, q)
    return math.sqrt(dot(e, e))

def candidates(fg, i):
    p = fg.P[i]; J = fg.J
    crotch = J['LeftUpLeg'][1] - 3
    if fg.along[i] and fg.along[i][1] > 0.02:
        s = fg.along[i][0]
        return [s + 'Shoulder', s + 'Arm', s + 'ForeArm', s + 'Hand']
    if p[1] > J['Neck'][1] - 3: return ['Neck', 'Head', 'Spine2']
    if p[1] < crotch + 6:
        s = 'Left' if p[0] > 0 else 'Right'
        return ['Hips', 'Spine'] + [s + k for k in LEG]
    out = ['Hips', 'Spine', 'Spine1', 'Spine2']
    if p[1] > J['Spine2'][1] - 6: out += ['Neck', 'LeftShoulder' if p[0] > 0 else 'RightShoulder', ('Left' if p[0] > 0 else 'Right') + 'Arm']
    return out

def weigh(fg):
    J = fg.J; W = []
    for i, p in enumerate(fg.P):
        w = {}
        for b in candidates(fg, i):
            end = J[CHILD[b]] if b in CHILD else add(J[b], scale(sub(J[b], J[b.replace('Hand', 'ForeArm')]), 0.55))  # a hand has no bone beyond it
            w[b] = 1 / max(1.5, seg_dist(p, J[b], end)) ** 4
        W.append(w)
    for _ in range(3):
        nw = []
        for i, w in enumerate(W):
            m = dict(w); n = fg.adj[i]
            for j in n:
                for b, v in W[j].items(): m[b] = m.get(b, 0) + v / len(n)
            s = sum(m.values()); nw.append({b: v / s for b, v in m.items()})
        W = nw
    out = []
    for w in W:
        top = sorted(w.items(), key=lambda kv: -kv[1])[:4]; s = sum(v for _, v in top)
        out.append({b: v / s for b, v in top})
    return out

# ---- Writing the FBX: the template's file, with its skeleton moved and its mesh replaced ----

def write(fg, P, U, F, W, name, dst):
    data = open(TEMPLATE, 'rb').read(); ver = struct.unpack('<I', data[23:27])[0]
    class Nd:
        __slots__ = ('name', 'np', 'raw', 'kids', 'sent')
    def rd(pos):
        end, np_, pl = struct.unpack('<QQQ', data[pos:pos + 24]); pos += 24
        nl = data[pos]; pos += 1
        if end == 0: return None, pos
        n = Nd(); n.name = data[pos:pos + nl]; pos += nl; n.np = np_; n.raw = data[pos:pos + pl]; pos += pl; n.kids = []; n.sent = False
        while pos < end:
            if end - pos == 25 and data[pos:pos + 25] == b'\0' * 25: n.sent = True; pos += 25; break
            k, pos = rd(pos)
            if k is None: n.sent = True; break
            n.kids.append(k)
        return n, end
    pos = 27; top_ = []
    while True:
        n, pos = rd(pos)
        if n is None: break
        top_.append(n)
    tail = data[pos:]; foot_id = tail[:16]; magic = tail[-16:]
    def props(raw):
        o = 0; out = []
        while o < len(raw):
            t = chr(raw[o]); o += 1
            if t == 'L': out.append(struct.unpack_from('<q', raw, o)[0]); o += 8
            elif t == 'I': out.append(struct.unpack_from('<i', raw, o)[0]); o += 4
            elif t == 'D': out.append(struct.unpack_from('<d', raw, o)[0]); o += 8
            elif t == 'C': out.append(raw[o]); o += 1
            elif t == 'Y': out.append(struct.unpack_from('<h', raw, o)[0]); o += 2
            elif t == 'F': out.append(struct.unpack_from('<f', raw, o)[0]); o += 4
            elif t in 'SR':
                l = struct.unpack_from('<I', raw, o)[0]; o += 4; out.append(raw[o:o + l].decode('latin1')); o += l
            elif t in 'dfil':
                al, enc, cl = struct.unpack_from('<III', raw, o); o += 12; b = raw[o:o + cl]; o += cl
                out.append(list(struct.unpack('<%d%s' % (al, t), zlib.decompress(b) if enc else b)))
            else: raise ValueError(t)
        return out
    def arr(t, vals):
        b = struct.pack('<%d%s' % (len(vals), t), *vals); z = zlib.compress(b, 6)
        return t.encode() + struct.pack('<III', len(vals), 1, len(z)) + z
    def sb(t):
        b = t.encode('latin1'); return b'S' + struct.pack('<I', len(b)) + b
    def setP(p70, key, vals):
        for x in p70.kids:
            q = props(x.raw)
            if q[0] == key:
                x.raw = sb(q[0]) + sb(q[1]) + sb(q[2]) + sb(q[3]) + b''.join(b'D' + struct.pack('<d', v) for v in vals); x.np = 7; return
        raise KeyError(key)
    T = {n.name: n for n in top_}; O = T[b'Objects']; C = T[b'Connections']
    def mul(a, b): return [[sum(a[i][k] * b[k][j] for k in range(4)) for j in range(4)] for i in range(4)]
    def Rm(v):
        x, y, z = [math.radians(a) for a in v]
        cx, sx, cy, sy, cz, sz = math.cos(x), math.sin(x), math.cos(y), math.sin(y), math.cos(z), math.sin(z)
        X = [[1, 0, 0, 0], [0, cx, -sx, 0], [0, sx, cx, 0], [0, 0, 0, 1]]; Y = [[cy, 0, sy, 0], [0, 1, 0, 0], [-sy, 0, cy, 0], [0, 0, 0, 1]]; Z = [[cz, -sz, 0, 0], [sz, cz, 0, 0], [0, 0, 1, 0], [0, 0, 0, 1]]
        return mul(Z, mul(Y, X))
    def Tm(v): return [[1, 0, 0, v[0]], [0, 1, 0, v[1]], [0, 0, 1, v[2]], [0, 0, 0, 1]]
    def inv(m):
        r = [[m[j][i] for j in range(3)] for i in range(3)]; t = [m[i][3] for i in range(3)]
        q = [-sum(r[i][k] * t[k] for k in range(3)) for i in range(3)]
        return [r[0] + [q[0]], r[1] + [q[1]], r[2] + [q[2]], [0, 0, 0, 1]]
    def flat(m): return [m[r][c] for c in range(4) for r in range(4)]
    B = {}
    for n in O.kids:
        if n.name == b'Model':
            q = props(n.raw)
            if q[2] == 'LimbNode':
                p70 = [k for k in n.kids if k.name == b'Properties70'][0]
                d = {'Lcl Translation': [0, 0, 0], 'PreRotation': [0, 0, 0], 'Lcl Rotation': [0, 0, 0]}
                for x in p70.kids:
                    r = props(x.raw)
                    if r[0] in d: d[r[0]] = r[4:7]
                B[q[0]] = dict(name=q[1].split('\x00')[0].split(':')[-1], p=d, p70=p70)
    par = {}; cluster_bone = {}
    for c in C.kids:
        q = props(c.raw)
        if q[0] == 'OO' and q[1] in B:
            if q[2] in B: par[q[1]] = q[2]
            else: cluster_bone[q[2]] = q[1]
    def local(b):
        p = B[b]['p']; return mul(Tm(p['Lcl Translation']), mul(Rm(p['PreRotation']), Rm(p['Lcl Rotation'])))
    def world(b):
        w = local(b)
        while b in par: b = par[b]; w = mul(local(b), w)
        return w
    W0 = {b: world(b) for b in B}
    new = fg.J
    assert set(new) == {B[b]['name'] for b in B}, set(new) ^ {B[b]['name'] for b in B}
    W1 = {}
    for b in B:
        m = [row[:] for row in W0[b]]
        for i in range(3): m[i][3] = new[B[b]['name']][i]
        W1[b] = m
    for b in B:
        if b in par:
            t = mul(inv(W1[par[b]]), W1[b]); v = [t[i][3] for i in range(3)]
        else: v = list(new[B[b]['name']])
        setP(B[b]['p70'], 'Lcl Translation', v)
    N = normals(P, F)
    same = {}
    for i, p in enumerate(P): same.setdefault((round(p[0], 2), round(p[1], 2), round(p[2], 2)), []).append(i)
    for g in same.values():
        if len(g) > 1:
            n = norm([sum(N[i][k] for i in g) for k in range(3)])
            for i in g: N[i] = n
    per = {}
    for i, w in enumerate(W):
        for k, v in w.items(): per.setdefault(k, []).append((i, v))
    dropped = (b'AnimationStack', b'AnimationLayer', b'AnimationCurveNode', b'AnimationCurve')
    gone = set(); keep = []
    pic = bytes.fromhex('ffd8ffe000104a46494600010100000100010000ffdb004300080606070605080707070909080a0c140d0c0b0b0c1912130f141d1a1f1e1d1a1c1c20242e2720222c231c1c2837292c30313434341f27393d38323c2e333432ffc0000b080001000101011100ffc4001f0000010501010101010100000000000000000102030405060708090a0bffc400b5100002010303020403050504040000017d01020300041105122131410613516107227114328191a1082342b1c11552d1f02433627282090a161718191a25262728292a3435363738393a434445464748494a535455565758595a636465666768696a737475767778797a838485868788898a92939495969798999aa2a3a4a5a6a7a8a9aab2b3b4b5b6b7b8b9bac2c3c4c5c6c7c8c9cad2d3d4d5d6d7d8d9dae1e2e3e4e5e6e7e8e9eaf1f2f3f4f5f6f7f8f9faffda0008010100003f00fb7fffd9')
    jn = name + '.jpg'
    for n in O.kids:
        if n.name in dropped: gone.add(props(n.raw)[0]); continue
        keep.append(n); k = {x.name: x for x in n.kids}; q = props(n.raw); oid = q[0]
        idb = n.raw[:9]
        if n.name == b'Geometry':
            n.raw = idb + sb(name + '\x00\x01Geometry') + sb('Mesh')
            k[b'Vertices'].raw = arr('d', [c for p in P for c in p])
            k[b'PolygonVertexIndex'].raw = arr('i', [i for a, b, c in F for i in (a, b, -c - 1)])
            for x in n.kids:
                if x.name == b'LayerElementNormal':
                    for y in x.kids:
                        if y.name == b'Normals': y.raw = arr('d', [c for f in F for v in f for c in N[v]])
                        if y.name == b'NormalsW': y.raw = arr('d', [1.0] * (3 * len(F)))
                if x.name == b'LayerElementUV':
                    for y in x.kids:
                        if y.name == b'UV': y.raw = arr('d', [c for u in U for c in u])
                        if y.name == b'UVIndex': y.raw = arr('i', [i for f in F for i in f])
        elif n.name == b'Model' and q[2] == 'Mesh':
            n.raw = idb + sb(name + '\x00\x01Model') + sb('Mesh')
        elif n.name == b'Deformer' and q[2] == 'Cluster':
            bn = B[cluster_bone[oid]]['name']; link = W1[cluster_bone[oid]]
            k[b'TransformLink'].raw = arr('d', flat(link)); k[b'Transform'].raw = arr('d', flat(inv(link)))
            if bn in per:
                k[b'Indexes'].raw = arr('i', [i for i, _ in per[bn]]); k[b'Weights'].raw = arr('d', [v for _, v in per[bn]])
            else:
                n.kids = [x for x in n.kids if x.name not in (b'Indexes', b'Weights')]
        elif n.name == b'Pose':
            for pn in n.kids:
                if pn.name != b'PoseNode': continue
                kk = {x.name: x for x in pn.kids}; nid = props(kk[b'Node'].raw)[0]
                if nid in W1: kk[b'Matrix'].raw = arr('d', flat(W1[nid]))
        elif n.name == b'Texture':
            n.raw = idb + sb(name + '\x00\x01Texture') + sb('')
            k[b'TextureName'].raw = sb(name + '\x00\x01Texture'); k[b'Media'].raw = sb(jn + '\x00\x01Video'); k[b'FileName'].raw = sb(jn); k[b'RelativeFilename'].raw = sb(jn)
        elif n.name == b'Video':
            n.raw = idb + sb(jn + '\x00\x01Video') + sb('Clip')
            k[b'Filename'].raw = sb(jn); k[b'RelativeFilename'].raw = sb(jn); k[b'Content'].raw = b'R' + struct.pack('<I', len(pic)) + pic
            for x in [y for y in n.kids if y.name == b'Properties70'][0].kids:
                r = props(x.raw); x.raw = sb(r[0]) + sb(r[1]) + sb(r[2]) + sb(r[3]) + sb(jn)
    O.kids = keep
    C.kids = [c for c in C.kids if not ({props(c.raw)[1], props(c.raw)[2]} & gone)]
    D = T[b'Definitions']; total = 0
    D.kids = [x for x in D.kids if not (x.name == b'ObjectType' and props(x.raw)[0].encode() in dropped)]
    for x in D.kids:
        if x.name == b'ObjectType':
            c = [y for y in x.kids if y.name == b'Count'][0]; total += props(c.raw)[0]
    for x in D.kids:
        if x.name == b'Count': x.raw = b'I' + struct.pack('<i', total)
    top_ = [n for n in top_ if n.name != b'Takes']
    out = bytearray(data[:27])
    def wr(n):
        st = len(out); out.extend(b'\0' * 24); out.append(len(n.name)); out.extend(n.name); out.extend(n.raw)
        for x in n.kids: wr(x)
        if n.kids or n.sent: out.extend(b'\0' * 25)
        struct.pack_into('<QQQ', out, st, len(out), n.np, len(n.raw))
    for n in top_: wr(n)
    out.extend(b'\0' * 25); out.extend(foot_id); out.extend(b'\0' * 4)
    pad = ((len(out) + 15) & ~15) - len(out); pad = pad or 16
    out.extend(b'\0' * pad); out.extend(struct.pack('<I', ver)); out.extend(b'\0' * 120); out.extend(magic)
    open(dst, 'wb').write(out)


if __name__ == '__main__':
    tpl = template()
    out = os.environ.get('NPC_OUT', OUT)
    for name in sys.argv[1:] or [n for n in MODELS if not MODELS[n].get('reduce')]: build(name, tpl, out)
