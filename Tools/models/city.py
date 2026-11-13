#!/usr/bin/env python3
# The owner's building, airport and street models as game data (brief of 2026-11-06). Reads each model out of its archive in ~/Downloads/3D assets, stands it on the ground at real size,
# centres it, makes a distant version, and writes:
#   Assets/_Project/Resources/City/models.bytes   every mesh (see Runtime/Core/CityModels.cs)
#   Assets/_Project/Resources/City/Tex/*.png      its pictures, re-encoded by sips at no more than 512 pixels, which also drops their metadata
#   Tools/worldgen/models.json                    sizes and groups, for the placer in gen.py
# Standard library, /usr/bin/sips and bsdtar: nothing to download.
#   python3 -I Tools/models/city.py
import hashlib, json, math, os, re, struct, subprocess, sys, tempfile, zlib
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import blend, fbxread, glb, bake, maritime, metro

HERE = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SRC = os.path.expanduser('~/Downloads/3D assets')
OUT = os.environ.get('DAD_CITY_OUT') or os.path.join(HERE, 'Assets/_Project/Resources/City')
WORK = os.path.join(tempfile.gettempdir(), 'dad-city')
OWNER = 'Ishak Hasan Sabbir'
PICTURE_SIZE = {'terrain': '1024', 'big': '1024', 'yard': '2048', 'water': '256'} # By the name's first word; the rest are 512.

def unpack(name):
    """An archive unpacked, nested archives too, into its own folder. Returns the folder."""
    d = os.path.join(WORK, name)
    if not os.path.isdir(d):
        os.makedirs(d)
        subprocess.run(['/usr/bin/bsdtar', '-xf', os.path.join(SRC, name + '.zip'), '-C', d], check=True)
        done = set()
        for _ in range(3):
            for r, _, fs in os.walk(d):
                for f in fs:
                    p = os.path.join(r, f)
                    if p not in done and f.lower().endswith(('.zip', '.rar', '.7z')):
                        done.add(p)
                        subprocess.run(['/usr/bin/bsdtar', '-xf', p, '-C', r])
    return d

def find(folder, base):
    """A file by name anywhere under folder, ignoring case and the extension's spelling."""
    base = re.split(r'[\\/]', base)[-1].lower()
    stem = base.rsplit('.', 1)[0]
    hit = None
    for r, _, fs in os.walk(folder):
        for f in fs:
            if f.lower() == base: return os.path.join(r, f)
            if f.lower().rsplit('.', 1)[0] == stem: hit = os.path.join(r, f)
    return hit

class Piece:
    """Corners and triangles by material. A corner is (x, y, z, nx, ny, nz, u, v); a material is (picture bytes or None, colour)."""
    def __init__(s, name):
        s.name, s.v, s.tris, s.mats = name, [], {}, {}
    def tri(s, key, a, b, c): s.tris.setdefault(key, []).append((a, b, c))

def from_fbx(folder, path):
    data = open(path, 'rb').read()
    parts = fbxread.scene(data)
    parts = parts if isinstance(parts, list) else parts[0]
    out = []
    for q in parts:
        s = Piece(q.name)
        for i in range(len(q.p)):
            s.v.append(tuple(q.p[i]) + tuple(q.n[i]) + (q.uv[i][0], q.uv[i][1]))
        for t in q.tris:
            m = q.mats[t[3]] if t[3] < len(q.mats) else (q.mats[0] if q.mats else None)
            key = m.name if m else 'none'
            if key not in s.mats:
                pic = m.picture if m else None
                if pic is None and m and m.file:
                    f = find(folder, m.file)
                    if f: pic = open(f, 'rb').read()
                s.mats[key] = (pic, m.colour if m else (0.7, 0.7, 0.7))
            s.tri(key, t[0], t[1], t[2])
        out.append(s)
    return out

def from_fbx6(folder, path):
    """The old FBX layout (version 6): the mesh sits inside its Model, and pictures hang on the Model, not on a material."""
    root = fbxread.parse(open(path, 'rb').read())
    objs = root.get('Objects').kids
    models = {o.props[0]: o for o in objs if o.name == 'Model'}
    files = {}
    for o in objs:
        if o.name == 'Texture' and o.get('RelativeFilename') is not None: files[o.props[0]] = o.get('RelativeFilename').props[0]
    parent, hung = {}, {}
    for c in root.get('Connections').kids:
        a, b = c.props[1], c.props[2]
        if a in models and b in models: parent[a] = b
        elif a in files and b in models: hung.setdefault(b, []).append(a)
    def local(o):
        v = {q.props[0]: q.props[3:6] for q in o.get('Properties60').kids if q.props[0].startswith('Lcl')}
        return fbxread.mul(fbxread.mul(fbxread.trans(v.get('Lcl Translation', (0, 0, 0))), fbxread.euler(v.get('Lcl Rotation', (0, 0, 0)))), fbxread.scale(v.get('Lcl Scaling', (1, 1, 1))))
    def world(k):
        m = local(models[k])
        return fbxread.mul(world(parent[k]), m) if parent.get(k) in models else m
    out = []
    pictures = {}
    def picture(t):
        if t not in pictures:
            f = find(folder, files[t])
            pictures[t] = open(f, 'rb').read() if f else None
        return pictures[t]
    for k, o in models.items():
        if o.props[1] != 'Mesh' or o.get('Vertices') is None: continue
        w = world(k)
        v = o.get('Vertices').props
        pv = o.get('PolygonVertexIndex').props
        pos = [tuple(w[r][0] * v[i] + w[r][1] * v[i + 1] + w[r][2] * v[i + 2] + w[r][3] for r in range(3)) for i in range(0, len(v), 3)]
        def layer(name, data, index):
            e = o.get(name)
            if e is None or e.get(data) is None: return None
            i = e.get(index) if e.get('ReferenceInformationType').props[0] != 'Direct' else None
            return e.get('MappingInformationType').props[0], e.get(data).props, i.props if i is not None else None
        nor = layer('LayerElementNormal', 'Normals', 'NormalsIndex')
        uv = layer('LayerElementUV', 'UV', 'UVIndex')
        tex = o.get('LayerElementTexture')
        tmap = tex.get('MappingInformationType').props[0] if tex is not None else 'AllSame'
        tids = tex.get('TextureId').props if tex is not None and tex.get('TextureId') is not None else [0]
        s = Piece(k.split('\x00')[0])
        def fetch(el, width, corner, vertex, poly):
            mapping, arr, idx = el
            i = corner if mapping == 'ByPolygonVertex' else vertex if mapping in ('ByVertice', 'ByVertex') else poly if mapping == 'ByPolygon' else 0
            if idx is not None: i = idx[i]
            return tuple(arr[i * width + c] for c in range(width))
        poly, ring = 0, []
        for c, q in enumerate(pv):
            last = q < 0
            vi = ~q if last else q
            ring.append((c, vi))
            if not last: continue
            t = tids[poly if tmap == 'ByPolygon' and poly < len(tids) else 0]
            names = hung.get(k, [])
            key = names[t] if 0 <= t < len(names) else 'none'
            if key not in s.mats: s.mats[key] = (picture(key) if key != 'none' else None, (0.7, 0.7, 0.7))
            base = len(s.v)
            for cc, vv in ring:
                n = fetch(nor, 3, cc, vv, poly) if nor else (0, 1, 0)
                n = tuple(w[r][0] * n[0] + w[r][1] * n[1] + w[r][2] * n[2] for r in range(3))
                l = math.sqrt(sum(x * x for x in n)) or 1
                u = fetch(uv, 2, cc, vv, poly) if uv else (0, 0)
                s.v.append(pos[vv] + tuple(x / l for x in n) + (u[0], u[1]))
            for i in range(1, len(ring) - 1): s.tri(key, base, base + i, base + i + 1)
            ring = []
            poly += 1
        out.append(s)
    return out

def from_glb(folder, path):
    j, b, ps = glb.parts(open(path, 'rb').read())
    out = []
    for p in ps:
        s = Piece(p['name'])
        n = p['nrm'] or [(0, 1, 0)] * len(p['pos'])
        uv = p['uv'] or [(0, 0)] * len(p['pos'])
        for i in range(len(p['pos'])): s.v.append(tuple(p['pos'][i]) + tuple(n[i]) + (uv[i][0], 1 - uv[i][1]))
        pic, colour, key = None, (0.6, 0.6, 0.6), 'none'
        if p['material'] is not None:
            m = j['materials'][p['material']]
            key = m.get('name', str(p['material']))
            pbr = m.get('pbrMetallicRoughness', {})
            colour = tuple(c ** (1 / 2.2) for c in pbr.get('baseColorFactor', (1, 1, 1, 1))[:3])
            t = pbr.get('baseColorTexture', {}).get('index')
            if t is not None: pic = glb.image(j, b, j['textures'][t]['source'])
        s.mats[key] = (pic, colour)
        tri = p['tri']
        for i in range(0, len(tri), 3): s.tri(key, tri[i], tri[i + 1], tri[i + 2])
        out.append(s)
    return out

def from_obj(folder, path):
    mats, cur = {}, None
    lib = None
    pos, nrm, tex = [], [], []
    s = Piece(os.path.basename(path))
    corner = {}
    key = 'none'
    with open(path, errors='replace') as f:
        for line in f:
            if line.startswith('v '):
                pos.append(tuple(float(x) for x in line.split()[1:4]))
            elif line.startswith('vn '):
                nrm.append(tuple(float(x) for x in line.split()[1:4]))
            elif line.startswith('vt '):
                tex.append(tuple(float(x) for x in line.split()[1:3]))
            elif line.startswith('mtllib'):
                lib = find(folder, line.split(None, 1)[1].strip())
                if lib:
                    with open(lib, errors='replace') as m:
                        for l in m:
                            w = l.split()
                            if not w: continue
                            if w[0] == 'newmtl': cur = ' '.join(w[1:]); mats[cur] = [None, (0.7, 0.7, 0.7)]
                            elif w[0] == 'Kd' and cur: mats[cur][1] = tuple(float(x) for x in w[1:4])
                            elif w[0] == 'map_Kd' and cur: mats[cur][0] = ' '.join(w[1:])
            elif line.startswith('usemtl'):
                key = line.split(None, 1)[1].strip()
                if key not in s.mats:
                    file, colour = mats.get(key, (None, (0.7, 0.7, 0.7)))
                    p = find(folder, file) if file else None
                    s.mats[key] = (open(p, 'rb').read() if p else None, colour)
            elif line.startswith('f '):
                if key not in s.mats: s.mats[key] = (None, (0.7, 0.7, 0.7))
                ring = []
                for w in line.split()[1:]:
                    k = corner.get(w)
                    if k is None:
                        q = (w.split('/') + ['', ''])[:3]
                        a = int(q[0]); a = a - 1 if a > 0 else len(pos) + a
                        t = int(q[1]) - 1 if q[1] else None
                        n = int(q[2]) - 1 if q[2] else None
                        uv = tex[t] if t is not None and t < len(tex) else (0, 0)
                        k = corner[w] = len(s.v)
                        s.v.append(pos[a] + (nrm[n] if n is not None and n < len(nrm) else (0, 1, 0)) + (uv[0], uv[1]))
                    ring.append(k)
                for i in range(1, len(ring) - 1): s.tri(key, ring[0], ring[i], ring[i + 1])
    return [s]

class Model:
    def __init__(s, name, group, lods, mats, size, use):
        s.name, s.group, s.lods, s.mats, s.size, s.use = name, group, lods, mats, size, use

def merge(pieces):
    """Pieces into one: (corners, {material: triangles}, {material: (picture, colour)})."""
    v, tris, mats = [], {}, {}
    for p in pieces:
        o = len(v)
        v += p.v
        for k, t in p.tris.items():
            key = (k, hashlib.md5(p.mats[k][0]).hexdigest() if p.mats[k][0] else p.mats[k][1])
            mats[key] = p.mats[k]
            tris.setdefault(key, []).extend((a + o, b + o, c + o) for a, b, c in t)
    return v, tris, mats

def place(v, spin=None, up='y', size=None, height=None, scale=None, keep=False):
    """Up to y, on the ground, centred, at real size. Returns new corners and the size in metres."""
    if up == 'z': v = [(x, z, -y, a, c, -b, u, w) for x, y, z, a, b, c, u, w in v]
    if up == '-z': v = [(x, -z, y, a, -c, b, u, w) for x, y, z, a, b, c, u, w in v]
    if up == 'x': v = [(y, x, -z, b, a, -c, u, w) for x, y, z, a, b, c, u, w in v]
    xs = [p[0] for p in v]; ys = [p[1] for p in v]; zs = [p[2] for p in v]
    x0, x1, y0, y1, z0, z1 = min(xs), max(xs), min(ys), max(ys), min(zs), max(zs)
    k = scale or (height / (y1 - y0) if height else size / max(x1 - x0, z1 - z0))
    cx, cz = (x0 + x1) / 2, (z0 + z1) / 2
    out = [((x - cx) * k, (y - y0) * k, (z - cz) * k, a, b, c, u, w) for x, y, z, a, b, c, u, w in v]
    return out, ((x1 - x0) * k, (y1 - y0) * k, (z1 - z0) * k)

def cluster(v, tris, target):
    """A lighter mesh by merging corners that fall in one cell of a grid. Enough for a distant view."""
    total = sum(len(t) for t in tris.values())
    if total <= target: return v, tris
    xs = [p[0] for p in v]; ys = [p[1] for p in v]; zs = [p[2] for p in v]
    span = max(max(xs) - min(xs), max(ys) - min(ys), max(zs) - min(zs))
    cells = 8.0
    best = None
    for _ in range(16):
        c = span / cells
        out, slot, new = [], {}, {}
        for k, t in tris.items():
            for tri in t:
                r = []
                for i in tri:
                    p = v[i]
                    key = (k, int(p[0] // c), int(p[1] // c), int(p[2] // c))
                    j = slot.get(key)
                    if j is None:
                        j = slot[key] = len(out)
                        out.append(p)
                    r.append(j)
                if r[0] != r[1] and r[1] != r[2] and r[0] != r[2]: new.setdefault(k, []).append(tuple(r))
        n = sum(len(t) for t in new.values())
        if n <= target or best is None: best = (out, new)
        if n > target: break
        cells *= 1.5
    return best

def flat_normals(v, tris):
    """Corners that came with no normals get their triangle's."""
    if any(p[3] or p[5] or p[4] != 1 for p in v[:64]): return v, tris
    out, new = [], {}
    for k, t in tris.items():
        for a, b, c in t:
            p, q, r = v[a], v[b], v[c]
            ux, uy, uz = q[0] - p[0], q[1] - p[1], q[2] - p[2]
            wx, wy, wz = r[0] - p[0], r[1] - p[1], r[2] - p[2]
            n = (uy * wz - uz * wy, uz * wx - ux * wz, ux * wy - uy * wx)
            l = math.sqrt(sum(x * x for x in n)) or 1
            n = tuple(x / l for x in n)
            i = len(out)
            out += [s[:3] + n + s[6:] for s in (p, q, r)]
            new.setdefault(k, []).append((i, i + 1, i + 2))
    return out, new

# name, archive, file, group, how to size it, what it is, options. Groups: hub rich crime port slum props air.
MODELS = [
    ('office', 'office', 'office.fbx', 'hub rich', dict(height=30), 'commercial', {}),
    ('tower_glass', 'modern-building-2-game-ready', 'Building_v1_1.fbx', 'hub', dict(height=66), 'commercial', {}),
    ('tower_night', 'modern-night-building-game-ready', 'NightBuilding_v1_1.fbx', 'hub', dict(height=48), 'commercial', {}),
    ('asia_block', 'asia-building', 'build_001.fbx', 'hub rich crime', dict(height=36), 'apartments', {}),
    ('urban_block', 'urban-building', '6d00ad905951472989ed60bd3d15cf71.fbx.fbx', 'hub rich', dict(height=42), 'apartments', {}),
    ('soviet_block', 'low-poly-soviet-apartment-building-8k', 'BUILDING.fbx', 'rich crime', dict(height=27), 'apartments', dict(picture='SOVIET_LARGE_lambert1_BaseColor.jpg')),
    ('school', 'school-building', 'schol_19.obj', 'rich', dict(height=14), 'school', {}),
    ('abandoned', 'abandoned-building', 'abandoned building.fbx', 'crime', dict(height=21), 'apartments', {}),
    ('workshop', 'building-workshop-parking-workshop', 'bedune divar.obj', 'port crime', dict(height=9), 'warehouse', {}),
]
# The mini buildings, out of one file: each with the parts that belong to it. These are the only models the slums use, and the slums use nothing else.
MINI = [('mini_barn_a', ['barn01', 'barn01_add']), ('mini_barn_b', ['barn03']), ('mini_barrack_a', ['barrack01']), ('mini_barrack_b', ['barrack02']), ('mini_barrack_c', ['barrack03']),
        ('mini_barrack_d', ['barrack04']), ('mini_house_a', ['house01', 'house01_roof', 'house01_add']), ('mini_house_b', ['house02']), ('mini_house_c', ['house03', 'house03_roof', 'house03_add'])]
PROPS = [('container_a', ['container01']), ('container_b', ['container02']), ('container_c', ['container03']), ('watertank', ['watertank01']), ('generator', ['generator01']), ('trailer_a', ['trailer01']), ('trailer_b', ['trailer02'])]
PACK = [('lamp_single', 'StreetLampSingle'), ('lamp_double', 'StreetLampDouble'), ('barrier_a', 'PVCBarriersA'), ('barrier_b', 'PVCBarriersB'), ('railing', 'Railing2m')]
AIRCRAFT = [('jet_tanker', 'boeing-kc-135r-stratotanker', 'Boeing KC-135R Stratotanker.obj', 41.5), ('jet_fighter', 'mcdonnell-douglas-f-15-eagle-fighter-jet', 'McDonnell Douglas F-15 Eagle Fighter Jet.obj', 19.4)]
AIR_SCALE = 30.0

def vertex_paint(v, tris, mats):
    """Pictures sampled into corner colours, for models too fine to keep their own textures in the budget."""
    colours = [None] * len(v)
    for key, t in tris.items():
        pic, colour = mats[key]
        p = bake.Picture(pic) if pic else None
        for tri in t:
            for i in tri:
                if colours[i] is None:
                    colours[i] = tuple(x / 255 for x in p.at(v[i][6], v[i][7], False))[:3] if p else colour
    return colours

def write(models, textures):
    os.makedirs(os.path.join(OUT, 'Tex'), exist_ok=True)
    keep = set()
    for name, data in textures.items():
        path = os.path.join(OUT, 'Tex', name + '.png')
        keep.add(name + '.png')
        if os.path.exists(path): continue
        src = os.path.join(WORK, name + '.src')
        with open(src, 'wb') as f: f.write(data)
        subprocess.run(['/usr/bin/sips', '-s', 'format', 'png', '-Z', PICTURE_SIZE.get(name.split('_')[0], '512'), src, '--out', path], check=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        os.remove(src)
    b = bytearray(b'DADM')
    def u32(n): b.extend(struct.pack('<I', n))
    def f32(*x): b.extend(struct.pack('<%df' % len(x), *x))
    def text(t):
        d = t.encode('utf-8')
        u32(len(d))
        b.extend(d)
    text(OWNER)
    u32(len(models))
    for m in models:
        text(m.name)
        f32(*m.size)
        u32(len(m.lods))
        for v, tris, paint in m.lods:
            u32(len(v))
            for p in v: f32(*p)
            for i in range(len(v)):
                c = paint[i] if paint else (1, 1, 1)
                b.extend(bytes(max(0, min(255, int(x * 255))) for x in c[:3]) + b'\xff')
            u32(len(tris))
            for tex, ids in tris:
                text(tex)
                u32(len(ids))
                b.extend(struct.pack('<%dI' % len(ids), *ids))
    with open(os.path.join(OUT, 'models.bytes'), 'wb') as f: f.write(b)
    return len(b)

def finish(name, group, use, v, tris, mats, size, textures, far=420, near=16000, paint=False, thin=None, levels=None):
    """One model's two levels of detail. A material with a picture draws with it; one without draws white, with its colour in the corners."""
    v, tris = flat_normals(v, tris)
    lods = []
    for n, limit in enumerate((near, far)):
        lv, lt = cluster(v, tris, limit) if not levels else levels[n]
        if thin and limit == far: lv, lt = v, {k: [t for i, t in enumerate(ts) if (i // 2) % thin == 0] for k, ts in tris.items()}
        colours = [None] * len(lv)
        out = {}
        for key, t in lt.items():
            pic, colour = mats[key]
            if paint or not pic:
                tex = ''
                if pic and paint:
                    p = bake.Picture(pic)
                    for tri in t:
                        for i in tri:
                            if colours[i] is None: colours[i] = tuple(x / 255 for x in p.at(lv[i][6], lv[i][7], True))[:3]
                else:
                    for tri in t:
                        for i in tri: colours[i] = colour
            else:
                tex = 't_' + hashlib.md5(pic).hexdigest()[:12]
                textures[tex] = pic
                for tri in t:
                    for i in tri:
                        if colours[i] is None: colours[i] = (1, 1, 1)
            out.setdefault(tex, []).extend(i for tri in t for i in tri)
        colours = [c or (1, 1, 1) for c in colours]
        lods.append((lv, sorted(out.items()), colours))
    print('  %-16s %-12s %6.1f x %5.1f x %6.1f m  tris %6d / %5d  maps %d' % (name, group, size[0], size[1], size[2], sum(len(i) for _, i in lods[0][1]) // 3, sum(len(i) for _, i in lods[1][1]) // 3, len(lods[0][1])))
    return Model(name, group, lods, None, size, use)

def station():
    """The metro station: no station came in the owner's folder, so this one is drawn here to the size of the line. Platforms each side of the track, a canopy, and over each carriageway a bridge to a stair and a lift."""
    s = Piece('metro_station')
    cols = dict(concrete=(0.62, 0.6, 0.56), roof=(0.36, 0.38, 0.4), floor=(0.5, 0.49, 0.46), glass=(0.42, 0.5, 0.54), steel=(0.3, 0.31, 0.32), edge=(0.72, 0.62, 0.2))
    for k, c in cols.items(): s.mats[k] = (None, c)
    def box(k, x0, x1, y0, y1, z0, z1):
        for n, q in (((1, 0, 0), ((x1, y0, z0), (x1, y1, z0), (x1, y1, z1), (x1, y0, z1))), ((-1, 0, 0), ((x0, y0, z1), (x0, y1, z1), (x0, y1, z0), (x0, y0, z0))),
                     ((0, 1, 0), ((x0, y1, z0), (x0, y1, z1), (x1, y1, z1), (x1, y1, z0))), ((0, -1, 0), ((x0, y0, z1), (x0, y0, z0), (x1, y0, z0), (x1, y0, z1))),
                     ((0, 0, 1), ((x1, y0, z1), (x1, y1, z1), (x0, y1, z1), (x0, y0, z1))), ((0, 0, -1), ((x0, y0, z0), (x0, y1, z0), (x1, y1, z0), (x1, y0, z0)))):
            i = len(s.v)
            s.v += [p + n + (0, 0) for p in q]
            s.tri(k, i, i + 1, i + 2)
            s.tri(k, i, i + 2, i + 3)
    L, top = 56.0, 15.0
    for side in (1, -1):
        def bx(k, a, b, y0, y1, z0, z1): box(k, min(side * a, side * b), max(side * a, side * b), y0, y1, z0, z1)
        bx('floor', 1.75, 7.2, top - 0.5, top, -L, L)
        bx('edge', 1.75, 2.15, top, top + 0.02, -L, L)
        bx('glass', 7.1, 7.2, top, top + 1.2, -L, L)
        for z in range(-54, 55, 18):
            bx('steel', 6.7, 7.0, top, 19.6, z - 0.15, z + 0.15)
        # The way out: a covered bridge to the far side of the road, a stair down along the footpath's edge and a lift.
        bx('floor', 7.2, 19.4, top - 0.4, top, -3.0, 3.0)
        bx('glass', 7.2, 19.4, top, top + 1.2, 2.9, 3.0)
        bx('glass', 7.2, 16.0, top, top + 1.2, -3.0, -2.9)
        bx('roof', 7.2, 19.8, 18.2, 18.4, -3.4, 3.4)
        bx('steel', 19.2, 19.4, top, 18.2, -3.0, 3.0)
        steps = int((top - 0.3) / 0.18)
        for i in range(steps):
            y = top - (i + 1) * 0.18
            z = -3.0 - i * 0.3
            bx('concrete', 16.2, 19.2, y - 0.14, y + 0.04, z - 0.3, z)
        end = -3.0 - steps * 0.3
        for z in (-3.0, -11.0, -19.0, end + 0.4):
            h = top - (-3.0 - z) / 0.3 * 0.18
            bx('steel', 19.2, 19.4, 0, h + 1.1, z - 0.1, z + 0.1)
            bx('steel', 16.0, 16.2, 0, h + 1.1, z - 0.1, z + 0.1)
        bx('glass', 16.4, 19.2, 0, 18.2, 3.0, 6.0)
        bx('concrete', 16.2, 19.4, 0, 0.3, 3.0, 6.2)
    box('roof', -8.2, 8.2, 19.6, 19.9, -L - 1, L + 1)
    for z in range(-54, 55, 18):
        box('concrete', -7.2, 7.2, 12.9, 13.6, z - 0.6, z + 0.6)
        box('concrete', 1.75, 7.2, 13.6, top - 0.5, z - 0.6, z + 0.6)
        box('concrete', -7.2, -1.75, 13.6, top - 0.5, z - 0.6, z + 0.6)
        box('concrete', -0.8, 0.8, 0, 13.6, z - 0.8, z + 0.8)
    return s

def bound_picture(folder, material, fallback):
    """Some archives keep their pictures beside a model that does not name them: by the material's name if one fits, else the one the table gives."""
    want = re.sub(r'[^a-z0-9]', '', material.lower())
    for r, _, fs in os.walk(folder):
        for f in fs:
            low = f.lower()
            if 'basecolor' in low or 'albedo' in low:
                if want and want in re.sub(r'[^a-z0-9]', '', low): return open(os.path.join(r, f), 'rb').read()
    if fallback:
        p = find(folder, fallback)
        if p: return open(p, 'rb').read()
    return None

RAIL_SCALE = 2.466 # Their rails are 0.582 apart between the inner faces: standard gauge, 1.435 m.

def rail_paint(v, tris, top):
    """Neither file has colours or pictures, so the rails are told from the sleepers by height."""
    steel, sleeper = (0.36, 0.35, 0.34), (0.42, 0.4, 0.37)
    out = {('steel', steel): [], ('sleeper', sleeper): []}
    for ts in tris.values():
        for t in ts: out[('steel', steel) if sum(v[i][1] for i in t) / 3 > top else ('sleeper', sleeper)].append(t)
    return out, {k: (None, k[1]) for k in out}

def rail_long():
    """The owner's straight track, the whole railway's length is laid in these."""
    d = unpack('rail-long')
    v, tris, mats = merge(from_obj(d, find(d, 'Railroad113.obj')))
    v, size = place(v, scale=RAIL_SCALE)
    out, m = rail_paint(v, tris, 0.2)
    return finish('rail_long', 'infra', 'track', v, out, m, size, {}, far=300)

def rail_turn(name, arc):
    """The owner's curved track, cut at arc degrees. Whole it turns about 50 degrees on a 5.6 m radius, far tighter than anything on the main line (118 m at its tightest), so it serves where a siding leaves the main line.
    The pivot is where the curve begins, on the centre line: forward is +z and the curve bends to -x."""
    d = unpack('rail-turn')
    v, tris, mats = merge(from_obj(d, find(d, 'Railroad2.obj')))
    cx, cz, radius, start = -2.106, -0.858, 2.254, -3.26 # Fitted to its rail heads.
    k = RAIL_SCALE
    c, sn = math.cos(math.radians(-start)), math.sin(math.radians(-start))
    y0 = min(p[1] for p in v)
    def turned(p):
        x, z = p[0] - cx, p[2] - cz
        return x * c - z * sn, x * sn + z * c
    out = []
    for p in v:
        x, z = turned(p)
        nx, nz = p[3] * c - p[5] * sn, p[3] * sn + p[5] * c
        out.append(((x - radius) * k, (p[1] - y0) * k, z * k, nx, p[4], nz, p[6], p[7]))
    keep = {}
    for key, ts in tris.items():
        for t in ts:
            a = math.degrees(math.atan2(sum(turned(v[i])[1] for i in t), sum(turned(v[i])[0] for i in t)))
            if -1 <= a <= arc: keep.setdefault(key, []).append(t)
    used = sorted({i for ts in keep.values() for t in ts for i in t})
    xs = [out[i][0] for i in used]; ys = [out[i][1] for i in used]; zs = [out[i][2] for i in used]
    paint, m = rail_paint(out, keep, 0.2)
    return finish(name, 'infra', 'track', out, paint, m, (max(xs) - min(xs), max(ys) - min(ys), max(zs) - min(zs)), {}, far=200)

def grass_patch(textures):
    """The owner's grass cards as a ten-metre patch, standing, lit from above as a meadow is. The far level is every fifth card."""
    d = unpack('realtime-grass')
    every = from_fbx(d, find(d, 'FInal.fbx'))
    cards = [p for p in every if '|Dupli|' in p.name]
    v, tris, mats = [], {}, {}
    for p in cards:
        o = len(v)
        v += [q[:3] + (0, 1, 0) + q[6:] for q in p.v]
        for k, ts in p.tris.items():
            tris.setdefault(k, []).extend(tuple(i + o for i in t) for t in ts)
    pics = {}
    for k in tris:
        pics[k] = open(find(d, k + '.png'), 'rb').read()
    xs = [q[0] for q in v]; zs = [q[2] for q in v]
    k = 10.0 / max(max(xs) - min(xs), max(zs) - min(zs))
    v, size = place(v, scale=k)
    names = {}
    for key, t in tris.items():
        names[(key, 'grass')] = t
    m = {nk: (pics[nk[0]], (1, 1, 1)) for nk in names}
    model = finish('grass_patch', 'open', 'grass', v, names, m, size, grass_named := {}, near=9000, far=1, thin=5)
    # Their pictures keep a name CityModels can tell: they are cut out by their alpha.
    rename = {}
    for old, pic in grass_named.items():
        rename[old] = 'grass_' + old
        textures['grass_' + old] = pic
    model.lods = [(lv, [(rename.get(t, t), ids) for t, ids in lt], c) for lv, lt, c in model.lods]
    return model

def with_alpha(colour, alpha, size=512):
    """A picture from a colour file and a separate cut-out file, for a model whose leaves come that way: RGBA PNG bytes."""
    def pixels(data):
        with tempfile.TemporaryDirectory() as d:
            a, b = os.path.join(d, 'a'), os.path.join(d, 'b.bmp')
            open(a, 'wb').write(data)
            subprocess.run(['/usr/bin/sips', '-s', 'format', 'bmp', '-z', str(size), str(size), a, '--out', b], check=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
            f = open(b, 'rb').read()
        start = struct.unpack_from('<I', f, 10)[0]
        h = struct.unpack_from('<i', f, 22)[0]
        depth = struct.unpack_from('<H', f, 28)[0] // 8
        stride = (size * depth + 3) // 4 * 4
        rows = [f[start + r * stride:start + r * stride + size * depth] for r in range(size)]
        if h > 0: rows.reverse()
        return rows, depth
    c, cd = pixels(colour)
    a, ad = pixels(alpha)
    raw = bytearray()
    for y in range(size):
        raw.append(0)
        for x in range(size):
            raw += bytes((c[y][x * cd + 2], c[y][x * cd + 1], c[y][x * cd], a[y][x * ad + 1]))
    def chunk(t, d): return struct.pack('>I', len(d)) + t + d + struct.pack('>I', zlib.crc32(t + d))
    return b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', size, size, 8, 6, 0, 0, 0)) + chunk(b'IDAT', zlib.compress(bytes(raw), 9)) + chunk(b'IEND', b'')

def islands(tris):
    """Triangles grouped by the corners they share: one leaf card, or one branch, each."""
    root = {}
    def find_root(a):
        while root.setdefault(a, a) != a:
            root[a] = root[root[a]]
            a = root[a]
        return a
    for a, b, c in tris:
        root[find_root(b)] = find_root(a)
        root[find_root(c)] = find_root(a)
    out = {}
    for t in tris: out.setdefault(find_root(t[0]), []).append(t)
    return list(out.values())

def compact(v, tris):
    """Only the corners these triangles use."""
    slot, out, new = {}, [], {}
    for k, ts in tris.items():
        for t in ts:
            r = []
            for i in t:
                if i not in slot:
                    slot[i] = len(out)
                    out.append(v[i])
                r.append(slot[i])
            new.setdefault(k, []).append(tuple(r))
    return out, new

def thin_cards(v, tris, keep, every, solid=40):
    """One leaf card in every `every`, each grown about its middle so the crown keeps its mass. Pieces of more than `solid` triangles are wood and stay. keep: the keys this applies to."""
    out, new = list(v), {}
    grow = every ** 0.5
    for k, ts in tris.items():
        if k not in keep:
            new[k] = ts
            continue
        got = []
        for n, isl in enumerate(islands(ts)):
            if len(isl) > solid:
                got += isl
                continue
            if n % every: continue
            ids = sorted({i for t in isl for i in t})
            c = [sum(v[i][a] for i in ids) / len(ids) for a in range(3)]
            m = {}
            for i in ids:
                m[i] = len(out)
                p = v[i]
                out.append(tuple(c[a] + (p[a] - c[a]) * grow for a in range(3)) + p[3:])
            got += [tuple(m[i] for i in t) for t in isl]
        new[k] = got
    return compact(out, new)

def crown_normals(v, tris, keys):
    """Leaf cards lit as one crown: each corner's normal points out from the crown's middle, as a card's own normal would flicker from leaf to leaf."""
    ids = sorted({i for k in keys for t in tris[k] for i in t})
    c = [sum(v[i][a] for i in ids) / len(ids) for a in range(3)]
    out = list(v)
    for i in ids:
        p = v[i]
        d = (p[0] - c[0], (p[1] - c[1]) + 0.25 * abs(c[1]), p[2] - c[2])
        l = math.sqrt(sum(x * x for x in d)) or 1
        out[i] = p[:3] + tuple(x / l for x in d) + p[6:]
    return out

def tree(name, v, tris, mats, leaves, size, textures, near, far, every=(1, 4)):
    """A tree's two levels: wood simplified, leaf cards thinned, every level with the crown's normals. Its leaf pictures are named so they draw cut out by their alpha."""
    v, tris = flat_normals(v, tris)
    v = crown_normals(v, tris, leaves)
    wood = {k: t for k, t in tris.items() if k not in leaves}
    levels = []
    for limit, n in zip((near, far), every):
        lv, lt = thin_cards(v, tris, leaves, n) if n > 1 else (v, tris)
        if wood:
            wv, wt = cluster(v, wood, limit)
            lt = {k: t for k, t in lt.items() if k in leaves}
            lv, lt = compact(lv, lt)
            o = len(wv)
            lv = list(wv) + list(lv)
            lt = {k: [tuple(i + o for i in t) for t in ts] for k, ts in lt.items()}
            lt.update(wt)
        levels.append((lv, lt))
    own = {}
    model = finish(name, 'open', 'tree', v, tris, mats, size, own, levels=levels)
    cut = {'t_' + hashlib.md5(mats[k][0]).hexdigest()[:12] for k in leaves}
    rename = {t: 'leaf_' + t for t in own if t in cut}
    for t, pic in own.items(): textures[rename.get(t, t)] = pic
    model.lods = [(lv, [(rename.get(t, t), ids) for t, ids in lt], c) for lv, lt, c in model.lods]
    return model

def tree_oak(textures):
    d = unpack('oak-tree')
    p = from_obj(d, find(d, 'oaktree.obj'))[0]
    colour, alpha = open(find(d, 'leaf_mask.jpg'), 'rb').read(), open(find(d, 'leaf_alpha.jpg'), 'rb').read()
    p.mats['branches_projection'] = (with_alpha(colour, alpha), (1, 1, 1))
    v, tris, mats = merge([p])
    v, size = place(v, height=13.0)
    leaves = {k for k in tris if k[0] == 'branches_projection'}
    return tree('tree_oak', v, tris, mats, leaves, size, textures, 1600, 220, every=(1, 3))

def tree_broad(textures):
    """tree-animate: the .blend holds two trees side by side, in one bark mesh and one leaf mesh; they are told apart by where they stand. About 9,000 triangles each as they stand, so even the near level keeps one spray in two, grown. The file's wind animation is not brought over."""
    d = unpack('tree-animate')
    b = blend.load(find(d, 'Sketchfab_2022_09_21_06_15_37.blend'))
    p = Piece('tree-animate')
    pics = {'Bark': 'Bark.png', 'Leaf': 'Leaf.png', 'Branch': 'Branch.png'}
    for _, names, pos, tris in b.meshes():
        for m, ring in tris:
            key = names[m]
            if key not in p.mats: p.mats[key] = (open(find(d, pics[key]), 'rb').read(), (1, 1, 1))
            o = len(p.v)
            for i, u, w in ring: p.v.append(pos[i] + (0, 1, 0, u, w))
            p.tri(key, o, o + 1, o + 2)
    # Each triangle came with its own corners, so cards are found again by where their corners sit.
    seen, remap, v = {}, [], []
    for q in p.v:
        k = (round(q[0], 4), round(q[1], 4), round(q[2], 4), round(q[6], 3), round(q[7], 3))
        if k not in seen:
            seen[k] = len(v)
            v.append(q)
        remap.append(seen[k])
    p.v = v
    p.tris = {k: [tuple(remap[i] for i in t) for t in ts] for k, ts in p.tris.items()}
    v, tris, mats = merge([p])
    out = []
    for name, height, here in (('tree_broad_a', 11.0, lambda x: x > -75), ('tree_broad_b', 12.5, lambda x: x <= -75)):
        tv, tt = compact(v, {k: [t for t in ts if here(sum(v[i][0] for i in t) / 3)] for k, ts in tris.items()})
        tv, size = place(tv, up='z', height=height)
        leaves = {k for k in tt if k[0] in ('Leaf', 'Branch')}
        out.append(tree(name, tv, tt, mats, leaves, size, textures, 700, 120, every=(2, 8)))
    return out

def tree_low(textures):
    """tree-low-poly-lowpoly: two trees in one file, sharing one picture of leaves and bark."""
    d = unpack('tree-low-poly-lowpoly')
    pic = open(find(d, 'Tree.png'), 'rb').read()
    out = []
    for p, name, height in zip(from_fbx(d, find(d, 'Tree 1 and 2.fbx')), ('tree_low_a', 'tree_low_b'), (9.0, 7.5)):
        p.mats = {k: (pic, (1, 1, 1)) for k in p.mats}
        v, tris, mats = merge([p])
        v, size = place(v, height=height)
        out.append(tree(name, v, tris, mats, set(tris), size, textures, 1600, 220, every=(1, 3)))
    return out

def terrain_pictures(textures):
    """The ground tiles. The sand and rock archives hold a material and a preview shape, so only their pictures are used; the grass archive's seamless ground goes with its cards."""
    for name, arc, file in (('terrain_sand', 'fine-sand-material', 'TexturePlane_FineSand01_BaseColor.png'), ('terrain_rock', 'rocky_soil__tile_texture', 'Material.001_baseColor.jpeg'), ('terrain_grass', 'realtime-grass', 'TopSeamless.png')):
        d = unpack(arc)
        textures[name] = open(find(d, file), 'rb').read()

def main():
    os.makedirs(WORK, exist_ok=True)
    models, textures = [], {}
    only = set(sys.argv[1:])
    for name, arc, file, group, sizing, use, opt in MODELS:
        d = unpack(arc)
        path = find(d, file)
        try:
            pieces = (from_fbx6(d, path) if struct.unpack_from('<I', open(path, 'rb').read(27), 23)[0] < 7000 else from_fbx(d, path)) if file.endswith('.fbx') else from_glb(d, path) if file.endswith('.glb') else from_obj(d, path)
        except Exception as e:
            print('  %-16s FAILED %r' % (name, e))
            continue
        for p in pieces:
            for k, (pic, colour) in list(p.mats.items()):
                if not pic: p.mats[k] = (bound_picture(d, k, opt.get('picture')), colour)
        v, tris, mats = merge(pieces)
        v, size = place(v, up=opt.get('up', 'y'), **sizing)
        models.append(finish(name, group, use, v, tris, mats, size, textures))
        for key, (pic, colour) in mats.items():
            if not pic and key[0] != 'none': print('    no picture for material %s of %s: drawn in its colour' % (key[0], name))
    d = unpack('lowpoly-buildings')
    every = from_fbx(d, find(d, 'lowpoly.fbx'))
    named = {p.name: p for p in every}
    for name, parts in MINI + PROPS:
        v, tris, mats = merge([named[p] for p in parts])
        v, size = place(v, scale=0.01)
        models.append(finish(name, 'slum' if name.startswith('mini') else 'props', 'house' if name.startswith('mini') else 'prop', v, tris, mats, size, textures))
    d = unpack('road-pack-not-including-the-road')
    every = from_fbx(d, find(d, 'Road Pack.fbx'))
    named = {p.name: p for p in every}
    for name, part in PACK:
        v, tris, mats = merge([named[part]])
        v, size = place(v, scale=0.01)
        models.append(finish(name, 'props', 'prop', v, tris, mats, size, textures, near=1500, far=120))
    d = unpack('low-poly-airport')
    every = from_glb(d, find(d, 'airport.glb'))
    # The airport keeps its own place and size. Split so that each piece can be culled: ground and markings, then the buildings; warehouses also stand alone for the port.
    def air_piece(name, ps):
        v, tris, mats = merge(ps)
        # All pieces share the model's own origin, so they line up when placed at one pose.
        out = [((x - 2.3) * AIR_SCALE, (y - 1.9) * AIR_SCALE, (z + 4.0) * AIR_SCALE, a, b, c, u, w) for x, y, z, a, b, c, u, w in v]
        xs = [p[0] for p in out]; ys = [p[1] for p in out]; zs = [p[2] for p in out]
        size = (max(xs) - min(xs), max(ys) - min(ys), max(zs) - min(zs))
        return finish(name, 'air', 'airport', out, tris, mats, size, textures, far=3000, near=60000)
    ground = [p for p in every if p.name in ('ground', 'runway') or 'sign' in p.name or 'arrow' in p.name or 'light' in p.name]
    rest = [p for p in every if p not in ground]
    models.append(air_piece('airport_field', ground))
    models.append(air_piece('airport_terminal', [p for p in rest if not p.name.startswith('warehouse')]))
    models.append(air_piece('airport_sheds', [p for p in rest if p.name.startswith('warehouse')]))
    v, tris, mats = merge([p for p in every if p.name == 'warehouse1'])
    v, size = place(v, height=11)
    models.append(finish('warehouse', 'port', 'warehouse', v, tris, mats, size, textures))
    for name, arc, file, length in AIRCRAFT:
        d = unpack(arc)
        pieces = from_obj(d, find(d, file))
        v, tris, mats = merge(pieces)
        v, size = place(v, size=length)
        models.append(finish(name, 'air', 'aircraft', v, tris, mats, size, textures, near=14000, far=800, paint=True))
    models.append(rail_long())
    models.append(rail_turn('rail_turn_21', 20.7))
    models.append(rail_turn('rail_turn_11', 11.0))
    models.append(grass_patch(textures))
    terrain_pictures(textures)
    models.append(tree_oak(textures))
    models += tree_broad(textures)
    models += tree_low(textures)
    models += maritime.models(sys.modules[__name__], textures)
    models += metro.models(sys.modules[__name__], textures)
    st = station()
    v, tris, mats = merge([st])
    xs = [p[0] for p in v]; ys = [p[1] for p in v]; zs = [p[2] for p in v]
    models.append(finish('metro_station', 'infra', 'station', v, tris, mats, (max(xs) - min(xs), max(ys) - min(ys), max(zs) - min(zs)), textures, near=30000, far=4000))
    n = write(models, textures)
    print('models', len(models), 'bytes', n, 'pictures', len(textures))
    table = {m.name: dict(size=[round(x, 2) for x in m.size], group=m.group.split(), use=m.use) for m in models}
    with open(os.path.join(HERE, 'Tools/worldgen/models.json'), 'w') as f: json.dump(dict(owner=OWNER, models=table), f, indent=1)

if __name__ == '__main__':
    main()
