#!/usr/bin/env python3
# The owner's models as game data (2026-11-06). Reads a model out of its zip in ~/Downloads/3D assets, sorts its surfaces into what the vehicle shader knows (paint, glass,
# lamps and so on, by the recipe below), takes colours from its own pictures, puts it the right way round at the right size on the road, cuts it to what traffic can afford by
# simplifying the model itself, and writes Assets/_Project/Resources/Vehicles/<Kind>.bytes (a .dadv file, see Runtime/Traffic/VehicleArt.cs). Standard library only, and /usr/bin/sips
# for pictures: nothing to download. Run with the CLT python:
#   python3 -I Tools/models/bake.py [kinds...]
import hashlib, heapq, io, json, math, os, re, struct, subprocess, sys, tempfile, zipfile, zlib
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fbxread
from fbxread import Part, Material

SRC = os.path.expanduser('~/Downloads/3D assets')
OUT = os.path.join(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))), 'Assets/_Project/Resources/Vehicles')
CACHE = os.path.join(tempfile.gettempdir(), 'dad-bake')
PAINT, PLASTIC, GLASS, METAL, LENS, ROUGH, RUBBER, TRIM = range(8)
NONE, HEAD, TAIL, BRAKE, LEFT, RIGHT, CABIN, MARKER, DASH = range(9)
SIDE = -1  # a lamp that is LEFT or RIGHT by which side of the vehicle it is on

# ---- Getting a file out of its archive ----

def archive(zip_name, path, inner=None):
    """A file's bytes and a way to find the pictures that came with it, by file name."""
    z = zipfile.ZipFile(os.path.join(SRC, zip_name))
    outer = z
    if inner:
        z = zipfile.ZipFile(io.BytesIO(z.read(inner)))
    def picture(name):
        base = re.split(r'[\\/]', name)[-1].lower()
        for arc in (z, outer):
            for n in arc.namelist():
                if n.lower().endswith('/' + base) or n.lower() == base: return arc.read(n)
        # The archives often rename a .jpg to .jpeg on the way out.
        stem = base.rsplit('.', 1)[0]
        for arc in (z, outer):
            for n in arc.namelist():
                if re.split(r'[\\/]', n)[-1].lower().rsplit('.', 1)[0] == stem: return arc.read(n)
        # And some keep the old name in front of the new: x.tga.png.
        for arc in (z, outer):
            for n in arc.namelist():
                if re.split(r'[\\/]', n)[-1].lower().startswith(base + '.'): return arc.read(n)
    return z.read(path), picture

def fbx_tree(data):
    """An ASCII FBX as the same tree of nodes fbxread makes of a binary one."""
    def value(t):
        t = t.strip()
        if t[:1] == '"': return t[1:-1]
        try: return int(t)
        except ValueError: pass
        try: return float(t)
        except ValueError: return t
    def values(text):
        return [value(t) for t in re.findall(r'"[^"]*"|[^,\s][^,]*', text)]
    root = fbxread.Node('', [], [])
    stack, numbers = [root], None
    for line in data.decode('utf-8', 'replace').split('\n'):
        line = line.strip()
        if not line or line[0] == ';': continue
        if line == '}':
            if numbers is not None:
                stack[-1].props = [[value(t) for t in ''.join(numbers).split(',') if t]]
                numbers = None
            stack.pop()
            continue
        if numbers is not None:
            numbers.append(line[2:].strip() if line.startswith('a:') else line)
            continue
        m = re.match(r'([A-Za-z_][\w ]*):\s*(.*)$', line)
        if m is None:  # the rest of the line before it
            stack[-1].kids[-1].props += values(line)
            continue
        name, rest = m.groups()
        opens = rest.endswith('{')
        if opens: rest = rest[:-1].strip()
        n = fbxread.Node(name, values(rest), [])
        stack[-1].kids.append(n)
        if opens:
            stack.append(n)
            if rest.startswith('*'): numbers = []
    return root

def fbx_scene(data):
    if data[:20] == b'Kaydara FBX Binary  ': return fbxread.scene(data)
    binary = fbxread.parse
    fbxread.parse = lambda _: fbx_tree(data)
    try: return fbxread.scene(data)
    finally: fbxread.parse = binary

def gltf_scene(zip_name, path, inner=None):
    """A scene.gltf with its buffer and the pictures it names, packed as a binary glTF is, in memory: (json, blob) for glb_parts."""
    z = zipfile.ZipFile(os.path.join(SRC, zip_name))
    if inner: z = zipfile.ZipFile(io.BytesIO(z.read(inner)))
    j = json.loads(z.read(path))
    base = path.rsplit('/', 1)[0] + '/' if '/' in path else ''
    blob = bytearray(z.read(base + j['buffers'][0]['uri']))
    names = set(z.namelist())
    for m in j['meshes']: m['primitives'] = [p for p in m['primitives'] if p.get('mode', 4) == 4]  # some hold outlines as lines
    for im in j.get('images', []):
        name = base + im.get('uri', '').replace('%20', ' ')
        if name not in names: continue
        data = z.read(name)
        while len(blob) % 4: blob.append(0)
        j['bufferViews'].append(dict(buffer=0, byteOffset=len(blob), byteLength=len(data)))
        im['bufferView'] = len(j['bufferViews']) - 1
        blob += data
    return j, bytes(blob)

# ---- Pictures: decoded by sips, small, as all a triangle needs is its colour ----

def indexed_png(data):
    """An indexed or grey PNG: sips turns these into broken BMPs, so they are read here. Width, height and rows of (r, g, b)."""
    w, h, depth, kind, _, _, laced = struct.unpack('>IIBBBBB', data[16:29])
    assert not laced and depth <= 8 and kind in (0, 3, 4)
    o, idat, palette = 8, b'', b''
    while o < len(data):
        n = struct.unpack_from('>I', data, o)[0]
        k = data[o + 4:o + 8]
        if k == b'IDAT': idat += data[o + 8:o + 8 + n]
        if k == b'PLTE': palette = data[o + 8:o + 8 + n]
        o += 12 + n
    raw = zlib.decompress(idat)
    channels = 2 if kind == 4 else 1
    stride = (w * channels * depth + 7) // 8
    bpp = max(1, channels * depth // 8)
    prev, rows, o = bytearray(stride), [], 0
    for y in range(h):
        f = raw[o]; line = bytearray(raw[o + 1:o + 1 + stride]); o += 1 + stride
        for i in range(stride):
            a = line[i - bpp] if i >= bpp else 0
            b = prev[i]
            c = prev[i - bpp] if i >= bpp else 0
            if f == 1: add = a
            elif f == 2: add = b
            elif f == 3: add = (a + b) // 2
            elif f == 4:
                p = a + b - c
                pa, pb, pc = abs(p - a), abs(p - b), abs(p - c)
                add = a if pa <= pb and pa <= pc else b if pb <= pc else c
            else: add = 0
            line[i] = (line[i] + add) & 255
        prev = line
        out = []
        for x in range(w):
            if depth == 8: v = line[x * channels]
            else:
                bit = x * depth
                v = (line[bit // 8] >> (8 - depth - bit % 8)) & ((1 << depth) - 1)
            if kind == 3: out.append(tuple(palette[v * 3:v * 3 + 3]))
            else:
                g = v * 255 // ((1 << depth) - 1)
                out.append((g, g, g))
        rows.append(out)
    return w, h, rows

class Picture:
    def __init__(self, data):
        self.rows = None
        if data[:8] == b'\x89PNG\r\n\x1a\n' and data[25] in (0, 3, 4) and data[24] <= 8 and data[28] == 0:
            self.w, self.h, self.rows = indexed_png(data)
            return
        os.makedirs(CACHE, exist_ok=True)
        key = hashlib.md5(data).hexdigest()
        bmp = os.path.join(CACHE, key + '.bmp')
        if not os.path.exists(bmp):
            src = os.path.join(CACHE, key + '.src')
            with open(src, 'wb') as f: f.write(data)
            subprocess.run(['/usr/bin/sips', '-s', 'format', 'bmp'] + (['-Z', '256'] if len(data) > 50000 else []) + [src, '--out', bmp], check=True, capture_output=True)
            os.remove(src)
        b = open(bmp, 'rb').read()
        start = struct.unpack_from('<I', b, 10)[0]
        self.w, h = struct.unpack_from('<ii', b, 18)
        self.depth = struct.unpack_from('<H', b, 28)[0] // 8
        self.h = abs(h)
        self.down = h < 0
        self.stride = (self.w * self.depth + 3) // 4 * 4
        self.px = b[start:]
    def at(self, u, v, flip):
        """u and v as the file gives them; `flip` when v counts up from the bottom, as FBX's does."""
        x = int((u % 1) * self.w) % self.w
        row = int((v % 1) * self.h) % self.h
        if flip: row = self.h - 1 - row
        if self.rows: return self.rows[row][x]
        if not self.down: row = self.h - 1 - row
        o = row * self.stride + x * self.depth
        return self.px[o + 2], self.px[o + 1], self.px[o]

# ---- glTF ----
def load_glb(data):
    assert data[:4] == b'glTF'
    n = struct.unpack_from('<I', data, 12)[0]
    j = json.loads(data[20:20 + n])
    return j, data[20 + n + 8:]

SIZES = {'SCALAR': 1, 'VEC2': 2, 'VEC3': 3, 'VEC4': 4, 'MAT4': 16}
CODES = {5120: 'b', 5121: 'B', 5122: 'h', 5123: 'H', 5125: 'I', 5126: 'f'}

def accessor(j, blob, i):
    a = j['accessors'][i]
    v = j['bufferViews'][a['bufferView']]
    k, code = SIZES[a['type']], CODES[a['componentType']]
    width = struct.calcsize('<' + code)
    start = v.get('byteOffset', 0) + a.get('byteOffset', 0)
    stride = v.get('byteStride', k * width)
    if stride == k * width:
        flat = struct.unpack_from('<%d%s' % (a['count'] * k, code), blob, start)
    else:
        flat = []
        for i2 in range(a['count']): flat.extend(struct.unpack_from('<%d%s' % (k, code), blob, start + i2 * stride))
    return flat if k == 1 else [flat[q:q + k] for q in range(0, len(flat), k)]

def node_matrix(n):
    if 'matrix' in n:
        m = n['matrix']
        return [[m[c * 4 + r] for c in range(4)] for r in range(4)]
    t = n.get('translation', [0, 0, 0]); x, y, z, w = n.get('rotation', [0, 0, 0, 1]); s = n.get('scale', [1, 1, 1])
    r = [[1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)],
         [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)],
         [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)]]
    return [[r[i][0] * s[0], r[i][1] * s[1], r[i][2] * s[2], t[i]] for i in range(3)] + [[0, 0, 0, 1]]

def mul(a, b): return [[sum(a[i][k] * b[k][j] for k in range(4)) for j in range(4)] for i in range(4)]
def apply(m, p): return tuple(m[i][0] * p[0] + m[i][1] * p[1] + m[i][2] * p[2] + m[i][3] for i in range(3))
def turn(m, n):
    v = tuple(m[i][0] * n[0] + m[i][1] * n[1] + m[i][2] * n[2] for i in range(3))
    l = math.sqrt(sum(c * c for c in v)) or 1
    return tuple(c / l for c in v)

def srgb(c):
    c = max(0.0, min(1.0, c))
    return int(round(255 * (12.92 * c if c <= 0.0031308 else 1.055 * c ** (1 / 2.4) - 0.055)))

# ---- A mesh in the kit's terms, one colour and finish a triangle ----

class Soup:
    def __init__(self): self.p = []; self.n = []; self.t = []; self.tri = []  # tri: (a, b, c, r, g, b, material, lamp, surface); t: a vertex's place in the family's picture, where it has one
    def add(self, p, n, tris, t=None):
        o = len(self.p); self.p += p; self.n += n
        if t: self.t += t
        self.tri += [(t[0] + o, t[1] + o, t[2] + o) + tuple(t[3:]) for t in tris]
    def bounds(self):
        return [min(q[i] for q in self.p) for i in range(3)], [max(q[i] for q in self.p) for i in range(3)]

def glb_parts(data):
    j, blob = data if isinstance(data, tuple) else load_glb(data)
    mats = []
    for m in j.get('materials', []):
        pbr = m.get('pbrMetallicRoughness', {})
        f = pbr.get('baseColorFactor', [1, 1, 1, 1])
        picture = None
        t = pbr.get('baseColorTexture')
        if t is not None:
            im = j['images'][j['textures'][t['index']]['source']]
            if 'bufferView' in im:
                v = j['bufferViews'][im['bufferView']]
                picture = blob[v.get('byteOffset', 0):v.get('byteOffset', 0) + v['byteLength']]
        mats.append(Material(m.get('name', ''), tuple(f[:3]), picture, None))
        mats[-1].orm = None
        t = pbr.get('metallicRoughnessTexture')
        if t is not None and 'bufferView' in j['images'][j['textures'][t['index']]['source']]:
            v = j['bufferViews'][j['images'][j['textures'][t['index']]['source']]['bufferView']]
            mats[-1].orm = blob[v.get('byteOffset', 0):v.get('byteOffset', 0) + v['byteLength']]
    parts = []
    def walk(i, world, trail=''):
        n = j['nodes'][i]
        w = mul(world, node_matrix(n))
        if 'mesh' in n:
            part = Part(n.get('name', ''))
            part.mats = mats
            part.trail = trail  # the names above it: a Sketchfab file numbers its meshes and names their parents
            flip = sum(w[0][a] * (w[1][(a + 1) % 3] * w[2][(a + 2) % 3] - w[1][(a + 2) % 3] * w[2][(a + 1) % 3]) for a in range(3)) < 0
            for pr in j['meshes'][n['mesh']]['primitives']:
                pos = accessor(j, blob, pr['attributes']['POSITION'])
                nor = accessor(j, blob, pr['attributes']['NORMAL']) if 'NORMAL' in pr['attributes'] else [(0, 1, 0)] * len(pos)
                uv = accessor(j, blob, pr['attributes']['TEXCOORD_0']) if 'TEXCOORD_0' in pr['attributes'] else [(0, 0)] * len(pos)
                idx = accessor(j, blob, pr['indices']) if 'indices' in pr else list(range(len(pos)))
                o = len(part.p)
                part.p += [apply(w, q) for q in pos]
                part.n += [turn(w, q) for q in nor]
                part.uv += [tuple(q) for q in uv]
                m = pr.get('material', 0)
                for k in range(0, len(idx), 3):
                    a, b, c = o + idx[k], o + idx[k + 1], o + idx[k + 2]
                    part.tris.append((a, c, b, m) if flip else (a, b, c, m))
            parts.append(part)
        for k in n.get('children', []): walk(k, w, trail + '/' + n.get('name', ''))
    eye4 = [[1 if a == b else 0 for b in range(4)] for a in range(4)]
    for r in j['scenes'][j.get('scene', 0)]['nodes']: walk(r, eye4)
    return parts

def parts_of(recipe):
    data, picture = archive(recipe['zip'], recipe['file'], recipe.get('inner')) if 'zip' in recipe else (None, None)
    if 'rigged' in recipe:
        data = open(os.path.join(SRC, recipe['rigged']), 'rb').read()
        parts, up = fbxread.scene(data), True
    elif recipe['file'].lower().endswith('.fbx'):
        parts, up = fbx_scene(data), True
    elif recipe['file'].lower().endswith('.gltf'):
        parts, up = glb_parts(gltf_scene(recipe['zip'], recipe['file'], recipe.get('inner'))), False
    else:
        parts, up = glb_parts(data), False
    for p in parts:
        for m in p.mats:
            if m.picture is None and m.file and picture:
                m.picture = picture(m.file)
    parts = [p for p in parts if p.p]
    if recipe.get('by_parent'):  # a Sketchfab file numbers its meshes and names their parents
        for p in parts:
            if 'GLTF_created' not in p.trail.rsplit('/', 1)[-1]: p.name = p.trail.rsplit('/', 1)[-1]
    if recipe.get('numbered'):  # every mesh in the file has one name
        for i, p in enumerate(parts): p.name = '%03d' % i
    if 'pose' in recipe: recipe['pose'](parts)
    return parts, up

# ---- Simplifying: quadric edge collapse, per triangle colours kept ----

def plane(a, b, c):
    ux, uy, uz = b[0] - a[0], b[1] - a[1], b[2] - a[2]
    vx, vy, vz = c[0] - a[0], c[1] - a[1], c[2] - a[2]
    nx, ny, nz = uy * vz - uz * vy, uz * vx - ux * vz, ux * vy - uy * vx
    l = math.sqrt(nx * nx + ny * ny + nz * nz)
    if l < 1e-14: return None
    nx, ny, nz = nx / l, ny / l, nz / l
    return nx, ny, nz, -(nx * a[0] + ny * a[1] + nz * a[2]), l / 2

def quadric(a, b, c, d, w):
    return [w * q for q in (a * a, a * b, a * c, a * d, b * b, b * c, b * d, c * c, c * d, d * d)]

def cost(q, p):
    x, y, z = p
    return (q[0] * x * x + 2 * q[1] * x * y + 2 * q[2] * x * z + 2 * q[3] * x + q[4] * y * y + 2 * q[5] * y * z + 2 * q[6] * y + q[7] * z * z + 2 * q[8] * z + q[9])

def simplify(soup, target, edge_weight=12.0, weld=None):
    # Weld by position: the model's vertices are split at every seam. `weld` closes the gaps between panels too, in metres, for a far mesh that has to hold together as one surface.
    index, rep, pos = {}, [], []
    for p in soup.p:
        k = (round(p[0] / weld), round(p[1] / weld), round(p[2] / weld)) if weld else (round(p[0], 5), round(p[1], 5), round(p[2], 5))
        if k not in index: index[k] = len(pos); pos.append(p)
        rep.append(index[k])
    tris = []
    for t in soup.tri:
        a, b, c = rep[t[0]], rep[t[1]], rep[t[2]]
        if a != b and b != c and a != c: tris.append([a, b, c, t[3:]])
    # What holds a line is the surface a triangle belongs to, not its colour: a picture makes every triangle a colour of its own.
    q = [[0.0] * 10 for _ in pos]
    around = [set() for _ in pos]
    edges = {}
    for i, t in enumerate(tris):
        pl = plane(pos[t[0]], pos[t[1]], pos[t[2]])
        for v in t[:3]: around[v].add(i)
        if pl is None: continue
        k = quadric(pl[0], pl[1], pl[2], pl[3], pl[4])
        for v in t[:3]: q[v] = [x + y for x, y in zip(q[v], k)]
        for a, b in ((t[0], t[1]), (t[1], t[2]), (t[2], t[0])):
            edges.setdefault((min(a, b), max(a, b)), []).append(i)
    # An open edge, or the line between two colours, is held by a plane through it: a lamp keeps its shape as the rest goes.
    for (a, b), who in edges.items():
        if len(who) == 2 and tris[who[0]][3][5] == tris[who[1]][3][5]: continue
        pl = plane(pos[tris[who[0]][0]], pos[tris[who[0]][1]], pos[tris[who[0]][2]])
        if pl is None: continue
        ex, ey, ez = pos[b][0] - pos[a][0], pos[b][1] - pos[a][1], pos[b][2] - pos[a][2]
        nx, ny, nz = ey * pl[2] - ez * pl[1], ez * pl[0] - ex * pl[2], ex * pl[1] - ey * pl[0]
        l = math.sqrt(nx * nx + ny * ny + nz * nz)
        if l < 1e-12: continue
        nx, ny, nz = nx / l, ny / l, nz / l
        k = quadric(nx, ny, nz, -(nx * pos[a][0] + ny * pos[a][1] + nz * pos[a][2]), edge_weight * l * l)
        q[a] = [x + y for x, y in zip(q[a], k)]
        q[b] = [x + y for x, y in zip(q[b], k)]
    version = [0] * len(pos)
    heap = []
    def push(a, b):
        s = [x + y for x, y in zip(q[a], q[b])]
        m = tuple((pos[a][i] + pos[b][i]) / 2 for i in range(3))
        best = min((cost(s, p), i) for i, p in enumerate((pos[a], pos[b], m)))
        heapq.heappush(heap, (best[0], a, b, version[a], version[b], best[1]))
    for a, b in edges: push(a, b)
    alive = len(tris)
    dead = [False] * len(tris)
    while alive > target and heap:
        _, a, b, va, vb, which = heapq.heappop(heap)
        if version[a] != va or version[b] != vb: continue
        to = (pos[a], pos[b], tuple((pos[a][i] + pos[b][i]) / 2 for i in range(3)))[which]
        # Refuse a collapse that turns a triangle over.
        flipped = False
        for v in (a, b):
            for i in around[v]:
                if dead[i]: continue
                t = tris[i]
                if a in t[:3] and b in t[:3]: continue
                before = plane(pos[t[0]], pos[t[1]], pos[t[2]])
                after = plane(*[to if x == v else pos[x] for x in t[:3]])
                if before and (after is None or before[0] * after[0] + before[1] * after[1] + before[2] * after[2] < 0.2): flipped = True; break
            if flipped: break
        if flipped: continue
        pos[a] = to
        q[a] = [x + y for x, y in zip(q[a], q[b])]
        version[a] += 1
        version[b] += 1
        for i in list(around[b]):
            if dead[i]: continue
            t = tris[i]
            if a in t[:3]:
                dead[i] = True; alive -= 1
                for v in t[:3]: around[v].discard(i)
                continue
            t[t.index(b)] = a
            around[a].add(i)
        around[b] = set()
        near = set()
        for i in around[a]:
            if not dead[i]: near.update(tris[i][:3])
        near.discard(a)
        for v in near: push(min(a, v), max(a, v))
    out = Soup()
    for i, t in enumerate(tris):
        if dead[i]: continue
        pl = plane(pos[t[0]], pos[t[1]], pos[t[2]])
        if pl is None: continue
        o = len(out.p)
        out.p += [pos[t[0]], pos[t[1]], pos[t[2]]]
        out.n += [pl[:3]] * 3
        out.tri.append((o, o + 1, o + 2) + tuple(t[3]))
    return out

def smooth(s, degrees=40):
    """Normals that round a surface over and keep its creases, for a mesh the simplifier left flat."""
    around, faces = {}, []
    for i, t in enumerate(s.tri):
        pl = plane(s.p[t[0]], s.p[t[1]], s.p[t[2]])
        faces.append(pl)
        if pl is None: continue
        for v in t[:3]: around.setdefault(tuple(round(c, 5) for c in s.p[v]), []).append(i)
    limit = math.cos(math.radians(degrees))
    out = Soup()
    for i, t in enumerate(s.tri):
        f = faces[i]
        if f is None: continue
        o = len(out.p)
        for v in t[:3]:
            x = y = z = 0.0
            for k in around[tuple(round(c, 5) for c in s.p[v])]:
                g = faces[k]
                if s.tri[k][8] == t[8] and g[0] * f[0] + g[1] * f[1] + g[2] * f[2] > limit:
                    x += g[0] * g[4]; y += g[1] * g[4]; z += g[2] * g[4]
            l = math.sqrt(x * x + y * y + z * z) or 1
            out.p.append(s.p[v]); out.n.append((x / l, y / l, z / l))
        out.tri.append((o, o + 1, o + 2) + tuple(t[3:]))
    return out

# ---- Writing ----

def pack_mesh(s):
    """One vertex per position, normal, colour and lamp: a soup's triangles share where they can."""
    index, pos, nor, rgba, lamp, uv, out = {}, [], [], [], [], [], []
    for t in s.tri:
        r, g, b, mat, l = t[3:8]
        for v in t[:3]:
            k = (tuple(round(c, 5) for c in s.p[v]), tuple(round(c, 3) for c in s.n[v]), r, g, b, mat, l, tuple(round(c, 5) for c in s.t[v]) if s.t else None)
            if k not in index:
                index[k] = len(pos)
                pos.append(s.p[v]); nor.append(s.n[v]); rgba.append((r, g, b, mat * 32 + 16)); lamp.append(l)
                if s.t: uv.append(s.t[v])
            out.append(index[k])
    b = struct.pack('<ii', len(pos), len(out))
    b += struct.pack('<%df' % (3 * len(pos)), *[c for p in pos for c in p])
    b += struct.pack('<%df' % (3 * len(pos)), *[c for p in nor for c in p])
    b += bytes(c for p in rgba for c in p)
    b += struct.pack('<%df' % len(lamp), *lamp)
    b += struct.pack('<%di' % len(out), *out)
    b += struct.pack('<i', len(uv))
    b += struct.pack('<%df' % (2 * len(uv)), *[c for p in uv for c in p])
    return b

def write(kind, near, glass, far, wheels, spots, eye=(0, 0, 0), head_top=0, size=(0, 0, 0), hubs=(), parts=()):
    """`spots`: x, y, z, steers, flip, wheel mesh and, on a driven car, the mesh that turns with the wheel and does not spin. `parts`: name, pivot, axis, swing in degrees, glass or not, mesh."""
    path = os.path.join(OUT, kind + '.bytes')
    os.makedirs(os.path.dirname(path), exist_ok=True)
    b = b'DADV' + struct.pack('<i', 4)
    b += struct.pack('<4f', eye[0], eye[1], eye[2], head_top)
    b += struct.pack('<3f', *size)
    b += struct.pack('<i', len(spots))
    for s in spots:
        x, y, z, steers, flip, mesh = s[:6]
        b += struct.pack('<3fBBBB', x, y, z, 1 if steers else 0, 1 if flip else 0, mesh, s[6] if len(s) > 6 else 255)
    for m in (near, glass, far): b += pack_mesh(m)
    b += struct.pack('<i', len(wheels))
    for m in wheels: b += pack_mesh(m)
    b += struct.pack('<i', len(hubs))
    for m in hubs: b += pack_mesh(m)
    b += struct.pack('<i', len(parts))
    for name, pivot, axis, swing, clear, m in parts:
        b += struct.pack('<B', len(name)) + name.encode('ascii') + struct.pack('<7fB', *pivot, *axis, swing, 1 if clear else 0) + pack_mesh(m)
    with open(path, 'wb') as f: f.write(b)
    print('%-10s near %5d tris, glass %4d, far %4d, wheels %s, %d spots, %d parts %d tris, %.1f KB' % (kind, len(near.tri), len(glass.tri), len(far.tri), [len(w.tri) for w in wheels], len(spots), len(parts), sum(len(p[5].tri) for p in parts), len(b) / 1024))

# ---- A family's picture ----

def png(path, w, h, rows):
    def chunk(k, d): return struct.pack('>I', len(d)) + k + d + struct.pack('>I', zlib.crc32(k + d))
    raw = b''.join(b'\x00' + r for r in rows)
    with open(path, 'wb') as f:
        f.write(b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', w, h, 8, 2, 0, 0, 0)) + chunk(b'IDAT', zlib.compress(raw, 9)) + chunk(b'IEND', b''))

def repaint(kind, data, size, blanks):
    """The model's picture at the size the game keeps, with its writing painted out: each blank is a box and the point it takes its colour from."""
    os.makedirs(CACHE, exist_ok=True)
    src, bmp = os.path.join(CACHE, kind + '.src'), os.path.join(CACHE, kind + '.bmp')
    with open(src, 'wb') as f: f.write(data)
    subprocess.run(['/usr/bin/sips', '-s', 'format', 'bmp', '-Z', str(size), src, '--out', bmp], check=True, capture_output=True)
    b = open(bmp, 'rb').read()
    start = struct.unpack_from('<I', b, 10)[0]
    w, h = struct.unpack_from('<ii', b, 18)
    depth = struct.unpack_from('<H', b, 28)[0] // 8
    stride = (w * depth + 3) // 4 * 4
    down = h < 0
    h = abs(h)
    rows = []
    for y in range(h):
        o = start + (y if down else h - 1 - y) * stride
        r = bytearray(w * 3)
        r[0::3] = b[o + 2:o + w * depth:depth]; r[1::3] = b[o + 1:o + w * depth:depth]; r[2::3] = b[o:o + w * depth:depth]
        rows.append(r)
    for x0, y0, x1, y1, sx, sy in blanks:
        c = bytes(rows[sy][sx * 3:sx * 3 + 3])
        for y in range(y0, y1): rows[y][x0 * 3:x1 * 3] = c * (x1 - x0)
    png(os.path.join(OUT, kind + 'Map.png'), w, h, [bytes(r) for r in rows])

# ---- People ----
# No person in the set has a skeleton, so one is laid over the T-pose here and the model bent to a seated pose, once, at bake time: a pose, not an animation.

V = lambda a, b: tuple(x - y for x, y in zip(a, b))
A = lambda a, b, k=1: tuple(x + y * k for x, y in zip(a, b))
dot = lambda a, b: sum(x * y for x, y in zip(a, b))
size = lambda a: math.sqrt(dot(a, a))
unit = lambda a: tuple(x / (size(a) or 1) for x in a)
cross = lambda a, b: (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])

def frame(x, y, z):
    return ((x[0], y[0], z[0]), (x[1], y[1], z[1]), (x[2], y[2], z[2]))

def spin(m, v): return tuple(dot(r, v) for r in m)

def between(a, b):
    """The shortest turn from one direction to another."""
    a, b = unit(a), unit(b)
    c = dot(a, b)
    if c > 0.99999: return ((1, 0, 0), (0, 1, 0), (0, 0, 1))
    k = cross(a, b)
    if size(k) < 1e-6: k = (1, 0, 0) if abs(a[0]) < 0.9 else (0, 1, 0)
    k = unit(k)
    s = math.sqrt(max(0, 1 - c * c))
    t = 1 - c
    x, y, z = k
    return ((t * x * x + c, t * x * y - s * z, t * x * z + s * y), (t * x * y + s * z, t * y * y + c, t * y * z - s * x), (t * x * z - s * y, t * y * z + s * x, t * z * z + c))

def reach(start, target, l1, l2, hint):
    """Two bones from `start` towards `target`, bending towards `hint`: where the joint and the end are."""
    v = V(target, start)
    d = min(size(v), (l1 + l2) * 0.999)
    e = unit(v)
    x = (l1 * l1 - l2 * l2 + d * d) / (2 * d)
    h = math.sqrt(max(0, l1 * l1 - x * x))
    p = unit(A(hint, e, -dot(hint, e)))
    return A(A(start, e, x), p, h), A(start, e, d)

MAN_MATS = {
    'outfit.004': (ROUGH, NONE, None), 'AvatarBody.001': (ROUGH, NONE, None), 'Std_Skin_Head': (ROUGH, NONE, None), 'Material.136': (ROUGH, NONE, (22, 20, 20)), 'Material.137': (ROUGH, NONE, (22, 20, 20)),
    'Material.138': (ROUGH, NONE, (22, 20, 20)), 'Material.141': (ROUGH, NONE, (22, 20, 20)), 'Male_Bushy.001': (ROUGH, NONE, (22, 20, 20)), 'Beard.003': (ROUGH, NONE, (30, 26, 24)),
    'Std_Eye_R': (ROUGH, NONE, (30, 26, 24)), 'Std_Eye_L': (ROUGH, NONE, (30, 26, 24)), 'Std_Eyelash': 'drop', 'Std_Cornea_R': 'drop', 'Std_Cornea_L': 'drop', 'Std_Tearline_R': 'drop', 'Std_Tearline_L': 'drop',
}
RACER_MATS = {'Wolf3D_Eye': 'drop', 'Wolf3D_Teeth': 'drop', 'Wolf3D_Skin': (ROUGH, NONE, None), 'Wolf3D_Body': (ROUGH, NONE, None), 'Wolf3D_Headwear': (PLASTIC, NONE, None),
              'Wolf3D_Outfit_Bottom': (ROUGH, NONE, None), 'Wolf3D_Outfit_Footwear': (ROUGH, NONE, None), 'Wolf3D_Outfit_Top': (ROUGH, NONE, None)}
# The owner's rigged people, all in centimetres, all facing +z. Mixamo's files keep the bones and lose some pictures: those still come from the model's own archive.
PEOPLE = {
    'shalwar': dict(rigged='mixamo/out/shalwar-man-rigged.fbx', zip='muslim-man-in-shalwar-kameez.zip', file='source/muslim man in shalwar kameez.fbx', forward='+z', mats=MAN_MATS,
                    # Under the clothes there is a whole body; only the hands and feet show.
                    fix=lambda name, part, c: 'drop' if name == 'AvatarBody.001' and abs(c[0]) < 64 and c[1] > 10 else None),
    'gangster': dict(rigged='mixamo/out/gangster-rigged.fbx', forward='+z', mats={'Material': (ROUGH, NONE, None)}),
    'hoodie': dict(rigged='mixamo/out/hoodie-rigged.fbx', forward='+z', mats={'Material': (ROUGH, NONE, None)}),
    'polo': dict(rigged='mixamo/out/red-polo-rigged.fbx', forward='+z', mats={'Material': (ROUGH, NONE, None)}),
    'racer': dict(zip='race-driver.zip', inner='source/motoman.zip', file='motoman.fbx', forward='+z', mats=RACER_MATS),
    # Walking only: the straw hat man's rig has no arms that move.
    'straw': dict(rigged='mixamo/out/straw-hat-rigged.fbx', forward='+z', mats={'Material': (ROUGH, NONE, None)}),
    'guard': dict(zip='security-guard-rigged-3d-model.zip', file='source/Guard.fbx', forward='+z', mats={'Material': (ROUGH, NONE, None), 'Material.001': (ROUGH, NONE, None), 'Material.002': (ROUGH, NONE, None)}),
}
SCALE = 0.01

# What each of the model's bones is to a seated pose. The joints are where those bones stand at rest; every other bone goes with its neighbour.
JOINTS = dict(hips='Hips', chest='Spine2', neck='Neck')
PIECES = dict(pelvis='hips', upper='chest', head='neck')
BONE_PIECE = {'Hips': 'pelvis', 'Spine': 'pelvis', 'Spine1': 'upper', 'Spine2': 'upper', 'Neck': 'head', 'Head': 'head', 'HeadTop_End': 'head', 'LeftEye': 'head', 'RightEye': 'head'}
for name, side in (('Left', 'L'), ('Right', 'R')):
    JOINTS.update({'sh' + side: name + 'Arm', 'el' + side: name + 'ForeArm', 'wr' + side: name + 'Hand', 'hp' + side: name + 'UpLeg', 'kn' + side: name + 'Leg', 'an' + side: name + 'Foot'})
    PIECES.update({'arm' + side: 'sh' + side, 'fore' + side: 'el' + side, 'hand' + side: 'wr' + side, 'leg' + side: 'hp' + side, 'shin' + side: 'kn' + side, 'foot' + side: 'an' + side})
    BONE_PIECE.update({name + 'Shoulder': 'upper', name + 'Arm': 'arm' + side, name + 'ForeArm': 'fore' + side, name + 'Hand': 'hand' + side, name + 'UpLeg': 'leg' + side, name + 'Leg': 'shin' + side,
                       name + 'Foot': 'foot' + side, name + 'ToeBase': 'foot' + side, name + 'Toe_End': 'foot' + side})

def piece_of(bone):
    if bone in BONE_PIECE: return BONE_PIECE[bone]
    finger = re.match('(Left|Right)Hand', bone)
    if finger: return 'hand' + finger.group(1)[0]
    raise SystemExit('nothing moves bone ' + bone)

_people = {}
def person(who):
    """The model at rest in metres, in the game's axes: its surfaces, its joints, and each corner's own skin weights."""
    if who in _people: return _people[who]
    recipe = PEOPLE[who]
    parts, flip_v = parts_of(recipe)
    body, _, _ = surfaces(recipe, parts, flip_v)
    turned = FORWARD[recipe['forward']]
    place = lambda q: tuple(c * SCALE for c in turned(q))
    body.p = [tuple(c * SCALE for c in p) for p in body.p]
    bones = next(p.bones for p in parts if p.bones)
    j = {k: place(bones[b]) for k, b in JOINTS.items()}
    ties = {}
    for part in parts:
        for q, skin in zip(part.p, part.skin):
            k = tuple(round(c, 4) for c in place(q))
            if k in ties: continue
            total = {}
            for bone, w in skin:
                name = piece_of(bone)
                total[name] = total.get(name, 0) + w
            s = sum(total.values())
            ties[k] = tuple((name, w / s) for name, w in total.items())
    _people[who] = body, j, ties
    return _people[who]

def seated(who, hip, lean, hands, feet, target=1800, elbow=(0, -1, -0.4), knee=(0, 0.4, 1)):
    """The person sitting: `hip` the middle of the pelvis, `lean` forward in radians, hands and feet where they go, all in the vehicle's frame. The model's own skin weights carry the mesh."""
    body, j, ties = person(who)
    up = (0, math.cos(lean), math.sin(lean))
    fwd = (0, -math.sin(lean), math.cos(lean))
    torso = frame((1, 0, 0), up, fwd)
    keep = frame((1, 0, 0), (0, 1, 0), (0, 0, 1))
    at = {'hips': hip}
    at['chest'] = A(hip, spin(torso, V(j['chest'], j['hips'])))
    at['neck'] = A(at['chest'], spin(torso, V(j['neck'], j['chest'])))
    turn = {'pelvis': torso, 'upper': torso, 'head': keep}
    for side, k in (('L', 0), ('R', 1)):
        sign = -1 if side == 'L' else 1
        at['sh' + side] = A(at['neck'], spin(torso, V(j['sh' + side], j['neck'])))
        l1, l2 = size(V(j['el' + side], j['sh' + side])), size(V(j['wr' + side], j['el' + side]))
        at['el' + side], at['wr' + side] = reach(at['sh' + side], hands[k], l1, l2, (sign * 0.5, elbow[1], elbow[2]))
        rest = V(j['el' + side], j['sh' + side])
        turn['arm' + side] = between(rest, V(at['el' + side], at['sh' + side]))
        turn['fore' + side] = between(V(j['wr' + side], j['el' + side]), V(at['wr' + side], at['el' + side]))
        turn['hand' + side] = turn['fore' + side]
        at['hp' + side] = A(hip, spin(torso, V(j['hp' + side], j['hips'])))
        l1, l2 = size(V(j['kn' + side], j['hp' + side])), size(V(j['an' + side], j['kn' + side]))
        ankle = A(feet[k], (0, j['an' + side][1], 0))
        at['kn' + side], at['an' + side] = reach(at['hp' + side], ankle, l1, l2, knee)
        turn['leg' + side] = between(V(j['kn' + side], j['hp' + side]), V(at['kn' + side], at['hp' + side]))
        turn['shin' + side] = between(V(j['an' + side], j['kn' + side]), V(at['an' + side], at['kn' + side]))
        turn['foot' + side] = keep
    out = Soup()
    out.tri = list(body.tri)
    moved = {}
    for p in body.p:
        k = tuple(round(c, 4) for c in p)
        if k not in moved:
            q = (0.0, 0.0, 0.0)
            for name, w in ties[k]:
                a = PIECES[name]
                q = A(q, A(at[a], spin(turn[name], V(p, j[a]))), w)
            moved[k] = q
        out.p.append(moved[k])
    out.n = list(body.n)
    return smooth(simplify(out, target))

# ---- A vehicle ----

FORWARD = {'+z': lambda p: (-p[0], p[1], p[2]), '-z': lambda p: (p[0], p[1], -p[2]), '+x': lambda p: (p[2], p[1], p[0]), '-x': lambda p: (-p[2], p[1], -p[0])}
WHITE, BLACK, TYRE, WINDOW = (255, 255, 255), (20, 20, 22), (26, 26, 28), (30, 36, 40)

def shell_wheels(part, is_wheel):
    """The triangles of the separate shells of a one-piece model that the recipe calls wheels."""
    keys = [tuple(round(c, 1) for c in q) for q in part.p]
    up = {}
    def find(a):
        up.setdefault(a, a)
        while up[a] != a:
            up[a] = up[up[a]]; a = up[a]
        return a
    for a, b, c, _ in part.tris:
        r = find(keys[a]); up[find(keys[b])] = r; up[find(keys[c])] = r
    groups = {}
    for i, t in enumerate(part.tris): groups.setdefault(find(keys[t[0]]), []).append(i)
    out = set()
    for tris in groups.values():
        vs = [part.p[v] for i in tris for v in part.tris[i][:3]]
        if is_wheel([min(q[k] for q in vs) for k in range(3)], [max(q[k] for q in vs) for k in range(3)]): out.update(tris)
    return out

def lamp_patches(recipe, part, flip, size):
    """Lamps that a model only has painted on: a thin patch over the paint, found by where the picture puts them, so the shader can light them."""
    def locate(u, v):
        for a, b, c, _ in part.tris:
            ua, va = part.uv[a]; ub, vb = part.uv[b]; uc, vc = part.uv[c]
            d = (vb - vc) * (ua - uc) + (uc - ub) * (va - vc)
            if abs(d) < 1e-12: continue
            l1 = ((vb - vc) * (u - uc) + (uc - ub) * (v - vc)) / d
            l2 = ((vc - va) * (u - uc) + (ua - uc) * (v - vc)) / d
            l3 = 1 - l1 - l2
            if min(l1, l2, l3) >= -0.001:
                return tuple(l1 * part.p[a][i] + l2 * part.p[b][i] + l3 * part.p[c][i] for i in range(3)), part.n[a]
        raise SystemExit('nothing at %.3f %.3f in the picture' % (u, v))
    out = Soup()
    for x0, y0, x1, y1, rule in recipe['patches']:
        finish, lamp, colour = rule
        corners, normal = [], None
        for x, y in ((x0, y0), (x1, y0), (x1, y1), (x0, y1)):
            at, normal = locate(x / size, 1 - y / size)
            corners.append(flip(at))
        n = flip(normal)
        p = [tuple(q[i] + n[i] * recipe['proud'] for i in range(3)) for q in corners]
        l = lamp
        o = len(out.p)
        out.p += p; out.n += [n] * 4; out.t += [recipe['plain']] * 4
        out.tri += [(o, o + 1, o + 2) + colour + (finish, l, (finish, l, 'patch')), (o, o + 2, o + 3) + colour + (finish, l, (finish, l, 'patch'))]
    return out

def lamp_boards(recipe):
    """Lamps a model does not have: thin boards on its surfaces. Each is two opposite corners in the file's units, standing across the vehicle, the way it faces along it, and what it is."""
    flip = FORWARD[recipe['forward']]
    out = Soup()
    for (x0, y0), (x1, y1), z, facing, rule in recipe['lamps']:
        finish, lamp, colour = rule
        o = len(out.p)
        out.p += [flip(q) for q in ((x0, y0, z), (x1, y0, z), (x1, y1, z), (x0, y1, z))]
        out.n += [flip((0, 0, facing))] * 4
        for t in ((0, 1, 2), (0, 2, 3)): out.tri.append((o + t[0], o + t[1], o + t[2]) + colour + (finish, lamp, (finish, lamp, 'board')))
    return out

def surfaces(recipe, parts, flip_v):
    """Every triangle of the model, sorted: the body, the glass and each wheel, in the game's axes but the model's units."""
    flip = FORWARD[recipe['forward']]
    if recipe.get('mirror'):  # a left-hand-drive model, for a right-hand-drive country
        turned = flip
        flip = lambda p: (-turned(p)[0], turned(p)[1], turned(p)[2])
    body, glass = Soup(), Soup()
    wheel_parts = recipe.get('wheel_parts', ())
    wheel_mats = recipe.get('wheel_mats', ())
    wheel_names = re.compile(recipe['wheel_names']) if recipe.get('wheel_names') else None
    centres = [flip(c) for c in recipe.get('wheel_at', ())]
    wheels = {}
    drop = re.compile(recipe['drop']) if recipe.get('drop') else None
    keep = re.compile(recipe['keep']) if recipe.get('keep') else None
    pictures = {}
    missing = set()
    def sheet(pic):
        if id(pic) not in pictures: pictures[id(pic)] = Picture(pic)
        return pictures[id(pic)]
    for part in parts:
        if drop and drop.search(part.name): continue
        if keep and not keep.search(part.name): continue
        p = [flip(q) for q in part.p]
        n = [flip(q) for q in part.n]
        into = {}
        loose = shell_wheels(part, recipe['wheel_shell']) if 'wheel_shell' in recipe else ()
        gone = recipe['strip'](part) if 'strip' in recipe else ()
        for number, (a, b, c, m) in enumerate(part.tris):
            if number in gone: continue
            mat = part.mats[m] if m < len(part.mats) else None
            name = mat.name if mat else ''
            centroid = tuple((p[a][i] + p[b][i] + p[c][i]) / 3 for i in range(3))
            rule = recipe['fix'](name, part.name, centroid) if 'fix' in recipe else None
            if rule is None: rule = recipe.get('parts', {}).get(part.name)
            if rule is None: rule = recipe['mats'].get(name)
            if rule is None: rule = recipe.get('other')
            if rule is None:
                missing.add(name)
                continue
            if rule == 'drop': continue
            finish, lamp, colour = rule
            if colour is None:
                if mat is not None and mat.picture:
                    pic = sheet(mat.picture)
                    u = sum(part.uv[q][0] for q in (a, b, c)) / 3
                    v = sum(part.uv[q][1] for q in (a, b, c)) / 3
                    samples = [pic.at(u, v, flip_v)] + [pic.at((part.uv[q][0] + u) / 2, (part.uv[q][1] + v) / 2, flip_v) for q in (a, b, c)]
                    colour = tuple(sum(s[i] for s in samples) // 4 for i in range(3))
                    sampled = colour
                elif mat is not None: colour = (srgb(mat.colour[0]), srgb(mat.colour[1]), srgb(mat.colour[2]))
                else: colour = (128, 128, 128)
            if finish is None:  # the model's own metal and roughness say what the surface is
                finish = PLASTIC
                if getattr(mat, 'orm', None):
                    o = sheet(mat.orm).at(sum(part.uv[q][0] for q in (a, b, c)) / 3, sum(part.uv[q][1] for q in (a, b, c)) / 3, flip_v)
                    finish = METAL if o[2] > 128 else ROUGH if o[1] > 170 else PLASTIC
            if 'tone' in recipe:
                again = recipe['tone'](colour, centroid, part.name)
                if again is not None:
                    finish, lamp, colour = again
                    if colour is None: colour = sampled
            surface = (finish, lamp, name, colour) if recipe.get('hold_colour') else (finish, lamp, name)
            if part.name in wheel_parts or number in loose or name in wheel_mats or (wheel_names and wheel_names.search(part.name)):
                if centres:
                    w = min(range(len(centres)), key=lambda i: sum((centres[i][k] - centroid[k]) ** 2 for k in range(3)))
                else:
                    w = part.name
                dest = wheels.setdefault(w, Soup())
            else:
                dest = glass if finish == GLASS and recipe.get('clear') else body
            into.setdefault(id(dest), [dest, {}, []])
            d = into[id(dest)]
            for q in (a, c, b):  # the axes turned over, so the winding does
                if q not in d[1]: d[1][q] = len(d[1])
            if recipe.get('mirror'): d[2].append((d[1][a], d[1][b], d[1][c]) + colour + (finish, lamp, surface))
            else: d[2].append((d[1][a], d[1][c], d[1][b]) + colour + (finish, lamp, surface))
        for dest, order, tris in into.values():
            keys = sorted(order, key=order.get)
            dest.add([p[q] for q in keys], [n[q] for q in keys], tris, [part.uv[q] for q in keys] if recipe.get('picture') else None)
    if missing: raise SystemExit('no rule for materials %s' % sorted(missing))
    return body, glass, wheels

def square(body, glass, wheels, kind):
    """Some models stand at an angle in their file. The wheels say which way the vehicle points: turn it to face +z."""
    mids = {w: [(a + b) / 2 for a, b in zip(*s.bounds())] for w, s in wheels.items()}
    span = sum(c[2] for c in mids.values()) / len(mids)
    front = [c for c in mids.values() if c[2] > span]
    back = [c for c in mids.values() if c[2] <= span]
    dx = sum(c[0] for c in front) / len(front) - sum(c[0] for c in back) / len(back)
    dz = sum(c[2] for c in front) / len(front) - sum(c[2] for c in back) / len(back)
    yaw = math.atan2(dx, dz)
    if abs(yaw) < 0.002: return
    print(kind, 'stood %.1f degrees off square in its file' % math.degrees(yaw))
    co, si = math.cos(yaw), math.sin(yaw)
    def fix(p): return (p[0] * co - p[2] * si, p[1], p[0] * si + p[2] * co)
    for s in [body, glass] + list(wheels.values()):
        s.p = [fix(p) for p in s.p]
        s.n = [fix(n) for n in s.n]

def Soup_of(s):
    c = Soup()
    c.p, c.n, c.t, c.tri = list(s.p), list(s.n), list(s.t), list(s.tri)
    return c

def cut(whole, target, lamp, weld=None, edge_weight=12.0):
    """The simplifier would take a lamp away for being small. Each lamp is cut down by itself and the rest around it, so a far vehicle still brakes and indicates."""
    groups = {}
    for t in whole.tri: groups.setdefault(t[7], []).append(t)
    out = Soup()
    lamps = [l for l in groups if l != NONE]
    for l in lamps + [NONE]:
        s = Soup()
        s.p, s.n, s.tri = whole.p, whole.n, groups.get(l, [])
        if not s.tri: continue
        part = simplify(s, lamp if l != NONE else target - lamp * len(lamps), edge_weight, weld)
        out.add(part.p, part.n, part.tri)
    return out

def far_tubes(tubes):
    """Thin bars for the far mesh, in metres in the game's axes: from, to, radius, finish, colour. The simplifier takes a thin tube first, and a bike with no fork is two wheels and a headlamp."""
    out = Soup()
    for a, b, r, finish, colour in tubes:
        along = [q - p for p, q in zip(a, b)]
        side = (along[1], -along[0], 0) if abs(along[2]) < abs(along[1]) + abs(along[0]) else (0, along[2], -along[1])
        l = math.sqrt(sum(c * c for c in side)) or 1
        side = tuple(c / l for c in side)
        l = math.sqrt(sum(c * c for c in along))
        other = tuple((along[(i + 1) % 3] * side[(i + 2) % 3] - along[(i + 2) % 3] * side[(i + 1) % 3]) / l for i in range(3))
        round_it = [tuple(side[i] * math.cos(k * 2.094) + other[i] * math.sin(k * 2.094) for i in range(3)) for k in range(3)]
        o = len(out.p)
        for n in round_it:
            out.p += [tuple(a[i] + n[i] * r for i in range(3)), tuple(b[i] + n[i] * r for i in range(3))]
            out.n += [n, n]
        for k in range(3):
            i, j = o + 2 * k, o + 2 * ((k + 1) % 3)
            for t in ((i, j, i + 1), (j, j + 1, i + 1)): out.tri.append(t + colour + (finish, NONE, (finish, NONE, 'far tube')))
    return out

def vehicle(kind, recipe, empty=None):
    """`empty`: the same vehicle with no one on it, for the player to take: Empty/<kind>.bytes, with its eye and head_top, and a richer near mesh where the cut allows."""
    parts, flip_v = parts_of(recipe)
    if recipe.get('picture') and not empty:  # Empty/<kind> is drawn with the family's own picture
        repaint(kind, parts[0].mats[0].picture, recipe['picture'], recipe.get('blanks', ()))
    body, glass, wheels = surfaces(recipe, parts, flip_v)
    if 'patches' in recipe:
        s = lamp_patches(recipe, parts[0], FORWARD[recipe['forward']], recipe['sheet'])
        body.add(s.p, s.n, s.tri, s.t)
    if 'lamps' in recipe:
        s = lamp_boards(recipe)
        body.add(s.p, s.n, s.tri)
    square(body, glass, wheels, kind)
    lo, hi = body.bounds()
    measures = {w: s.bounds() for w, s in wheels.items()}
    for w, (a, b) in measures.items(): lo, hi = [min(x, y) for x, y in zip(lo, a)], [max(x, y) for x, y in zip(hi, b)]
    scale = recipe['length'] / (hi[2] - lo[2])
    # Centred between the wheels across, as a mirror can stick out further on one side; along, end to end.
    across = [(a[0] + b[0]) / 2 for a, b in measures.values()]
    cx, cz = (min(across) + max(across)) / 2, (lo[2] + hi[2]) / 2
    ground = min(a[1] for a, _ in measures.values())
    def place(p): return ((p[0] - cx) * scale, (p[1] - ground) * scale, (p[2] - cz) * scale)
    body.p = [place(p) for p in body.p]
    glass.p = [place(p) for p in glass.p]
    # Only now is it known which side of the vehicle a lamp is on.
    def sided(t):
        if t[7] != SIDE: return t
        l = LEFT if body.p[t[0]][0] + body.p[t[1]][0] + body.p[t[2]][0] < 0 else RIGHT
        return t[:7] + (l, (t[6], l) + t[8][2:])
    body.tri = [sided(t) for t in body.tri]
    # A wheel about its own axle; wheels of one size and width share a mesh; the left ones are turned round in the game.
    spots, kinds, shapes = [], {}, []
    for w, s in wheels.items():
        a, b = measures[w]
        c = place([(u + v) / 2 for u, v in zip(a, b)])
        radius = (b[1] - a[1]) / 2 * scale
        size = (round(radius * 25), round((b[0] - a[0]) * scale * 25))
        if size not in kinds or (c[0] > 0 and kinds[size][1] <= 0):
            mesh = Soup()
            mid = [(u + v) / 2 for u, v in zip(a, b)]
            mesh.p = [tuple((p[i] - mid[i]) * scale for i in range(3)) for p in s.p]
            mesh.n = list(s.n); mesh.t = list(s.t); mesh.tri = list(s.tri)
            kinds[size] = (mesh, c[0])
        spots.append([c[0], radius, c[2], c[2] > recipe.get('steer_from', 0), c[0] < -0.02, size])
    if recipe.get('centre_wheels'):
        for s in spots: s[0] = 0.0; s[4] = False
    order = list(kinds)
    shapes = []
    for k in order:
        m = kinds[k][0]
        limit = recipe.get('wheel', 520)
        if len(m.tri) > limit: m = smooth(simplify(m, limit), 50)
        shapes.append(m)
    spots.sort(key=lambda w: (-round(w[2], 1), w[0]))  # rounded: a millimetre between two axle ends must not put the right wheel first
    spots = [tuple(s[:5]) + (order.index(s[5]),) for s in spots]
    near = body
    target = empty.get('near', 10500) if empty else recipe.get('near', 8000)
    if len(near.tri) > target: near = smooth(cut(near, target, 160))
    rider = seated(**recipe['rider']) if 'rider' in recipe and not empty else None
    if rider:
        near = Soup_of(near)
        near.add(rider.p, rider.n, rider.tri)
    if len(glass.tri) > recipe.get('glass', 400): glass = smooth(simplify(glass, recipe.get('glass', 400)))
    # The far mesh has the wheels and the glass in it, as the game draws neither at that range.
    whole = Soup()
    whole.add(body.p, body.n, body.tri)
    if rider and not recipe.get('closed'): whole.add(rider.p, rider.n, rider.tri)  # far off, no one sees into a closed vehicle
    whole.add(glass.p, glass.n, [t[:3] + WINDOW + (GLASS, NONE, (GLASS, NONE, 'far glass')) for t in glass.tri])
    # Each wheel is cut down by itself: left in with the rest, a rickshaw's front wheel and fork went first, and it was a metre short from a distance.
    far_budget = recipe.get('far', 260)
    each = max(10, far_budget * 3 // (10 * len(spots)))
    small = {}
    far_wheels = Soup()
    for x, y, z, _, left, mesh in spots:
        k = -1 if left else 1
        if mesh not in small: small[mesh] = simplify(shapes[mesh], each)
        m = small[mesh]
        far_wheels.add([(x + k * p[0], y + p[1], z + k * p[2]) for p in m.p], [(k * n[0], n[1], k * n[2]) for n in m.n], m.tri)
    if 'far_keep' in recipe: whole.tri = [t for t in whole.tri if recipe['far_keep'](t)]
    tubes = far_tubes(recipe.get('far_tubes', ()))
    far = cut(whole, far_budget - len(far_wheels.tri) - len(tubes.tri), 16, recipe.get('far_weld'), recipe.get('far_edge', 12.0))
    far.add(far_wheels.p, far_wheels.n, far_wheels.tri)
    far.add(tubes.p, tubes.n, tubes.tri)
    if recipe.get('picture'):
        # Near, the picture does the colouring; only the patches keep their own.
        plain = lambda m: [t[:3] + (t[3:6] if t[8][2] == 'patch' else WHITE) + t[6:] for t in m.tri]
        near.tri = plain(near)
        for m in shapes: m.tri = plain(m)
    a, b = near.bounds()
    print(kind, 'is %.2f wide, %.2f tall, %.2f long; wheels %s' % (b[0] - a[0], b[1], b[2] - a[2], ['%.2f %.2f r%.2f' % (s[0], s[2], s[1]) for s in spots]))
    if empty:
        # The whole length with its wheels; across, the body without the mirrors that stand off it, which is what a driver has to get through a gap.
        ends = [s[2] + k * c for s in spots for k in (-1, 1) for c in (max(abs(q[2]) for q in shapes[s[5]].p),)]
        length = max(ends + [b[2]]) - min(ends + [a[2]])
        limit = 2.2 * max(s[1] for s in spots)
        low = [abs(p[0]) for p in near.p if p[1] < limit] + [abs(s[0]) + max(abs(q[0]) for q in shapes[s[5]].p) for s in spots]
        width = empty.get('width', 2 * max(low))
        print('Empty/' + kind, 'is %.2f long, %.2f across the body, %.2f tall; eye %s, head %.2f' % (length, width, b[1], empty['eye'], empty['head_top']))
        write('Empty/' + kind, near, glass, far, shapes, spots, empty['eye'], empty['head_top'], (length, width, b[1]))
        return
    write(kind, near, glass, far, shapes, spots, size=(b[2] - a[2], b[0] - a[0], b[1]))

def boat(kind, recipe, eye, head_top):
    """A vessel: no wheels, and its origin on the water, with the keel `draft` under it."""
    parts, flip_v = parts_of(recipe)
    body, glass, _ = surfaces(recipe, parts, flip_v)
    lo, hi = body.bounds()
    a, b = glass.bounds()
    lo, hi = [min(x, y) for x, y in zip(lo, a)], [max(x, y) for x, y in zip(hi, b)]
    scale = recipe['length'] / (hi[2] - lo[2])
    def place(p): return ((p[0] - (lo[0] + hi[0]) / 2) * scale, (p[1] - lo[1]) * scale - recipe['draft'], (p[2] - (lo[2] + hi[2]) / 2) * scale)
    body.p = [place(p) for p in body.p]
    glass.p = [place(p) for p in glass.p]
    near = smooth(cut(body, recipe['near'], 0))
    if len(glass.tri) > recipe['glass']: glass = smooth(simplify(glass, recipe['glass']))
    whole = Soup()
    whole.add(body.p, body.n, body.tri)
    whole.add(glass.p, glass.n, [t[:3] + WINDOW + (GLASS, NONE, (GLASS, NONE, 'far glass')) for t in glass.tri])
    tubes = far_tubes(recipe.get('far_tubes', ()))
    far = cut(whole, recipe['far'] - len(tubes.tri), 0, recipe.get('far_weld'), recipe.get('far_edge', 12.0))
    far.add(tubes.p, tubes.n, tubes.tri)
    size = ((hi[2] - lo[2]) * scale, (hi[0] - lo[0]) * scale, (hi[1] - lo[1]) * scale)
    print(kind, 'is %.2f long, %.2f wide, %.2f keel to top; eye %s, head %.2f' % (size + (eye, head_top)))
    write(kind, near, glass, far, [], [], eye, head_top, size)

# ---- The car you drive ----
# Kept at the model's own size and detail, with its cabin and its doors as parts that move.

def shells(part, digits=4):
    """The triangles of each separate piece of a part."""
    keys = [tuple(round(c, digits) for c in q) for q in part.p]
    up = {}
    def find(a):
        up.setdefault(a, a)
        while up[a] != a:
            up[a] = up[up[a]]; a = up[a]
        return a
    for a, b, c, _ in part.tris:
        r = find(keys[a]); up[find(keys[b])] = r; up[find(keys[c])] = r
    groups = {}
    for i, t in enumerate(part.tris): groups.setdefault(find(keys[t[0]]), []).append(i)
    return list(groups.values())

def quat_mul(a, b):
    ax, ay, az, aw = a; bx, by, bz, bw = b
    return (aw * bx + ax * bw + ay * bz - az * by, aw * by - ax * bz + ay * bw + az * bx, aw * bz + ax * by - ay * bx + az * bw, aw * bw - ax * bx - ay * by - az * bz)

def about(axis, degrees, v):
    """v turned about an axis, the sum Unity's Quaternion.AngleAxis does."""
    t = math.radians(degrees)
    k = unit(axis)
    return tuple(v[i] * math.cos(t) + cross(k, v)[i] * math.sin(t) + k[i] * dot(k, v) * (1 - math.cos(t)) for i in range(3))

def hinge(data, node, clip):
    """Where a door turns and how far, from the model's own animation: the point, the axis and the degrees."""
    j, blob = load_glb(data)
    i = [k for k, n in enumerate(j['nodes']) if n.get('name') == node][0]
    a = [x for x in j['animations'] if x['name'] == clip][0]
    ch = [c for c in a['channels'] if c['target']['node'] == i and c['target']['path'] == 'rotation'][0]
    out = accessor(j, blob, a['samplers'][ch['sampler']]['output'])
    rest, shut = out[-1], out[0]
    d = quat_mul(rest, (-shut[0], -shut[1], -shut[2], shut[3]))
    if d[3] < 0: d = tuple(-c for c in d)
    return tuple(j['nodes'][i]['translation']), unit(d[:3]), math.degrees(2 * math.acos(min(1, d[3])))

def dials(screen, eye):
    """The car's own cluster is a screen that shows a maker's logo. In its place, dials the game can drive: returns the ticks and each needle's name, pivot, axis and mesh."""
    c = tuple(sum(p[i] for p in screen) / len(screen) for i in range(3))
    n = unit(cross(V(screen[1], screen[0]), V(screen[2], screen[0])))
    if dot(n, V(eye, c)) < 0: n = tuple(-x for x in n)
    u = unit(A((1, 0, 0), n, -n[0]))
    v = unit(cross(n, u))
    if v[1] < 0: v = tuple(-x for x in v)
    into = tuple(-x for x in n)
    ticks, needles = Soup(), []
    def quad(s, a, b, w, proud, colour):
        side = unit(cross(V(b, a), n))
        p = [A(A(a, side, w), n, proud), A(A(a, side, -w), n, proud), A(A(b, side, -w), n, proud), A(A(b, side, w), n, proud)]
        o = len(s.p)
        s.p += p; s.n += [n] * 4
        surface = (TRIM, DASH, 'dial')
        s.tri += [(o, o + 1, o + 2) + colour + (TRIM, DASH, surface), (o, o + 2, o + 3) + colour + (TRIM, DASH, surface)]
    for name, across, down, radius, marks in (('SpeedNeedle', -0.072, 0, 0.038, 11), ('RevNeedle', 0.072, 0, 0.038, 9), ('FuelNeedle', 0, -0.012, 0.022, 5)):
        pivot = A(A(c, u, across), v, down)
        for k in range(marks):
            d = about(into, 130 - 260 * k / (marks - 1), v)
            quad(ticks, A(pivot, d, radius * 0.84), A(pivot, d, radius), 0.0009, 0.0012, (226, 226, 216))
        s = Soup()
        quad(s, tuple(-0.0 * x for x in v), tuple(x * radius * 0.92 for x in v), 0.0011, 0.0024, (232, 96, 34))
        needles.append((name, pivot, into, s))
    return ticks, needles

def driven(kind, recipe):
    parts, flip_v = parts_of(recipe)
    data = archive(recipe['zip'], recipe['file'])[0]
    def group(keep, **more): return surfaces(dict(recipe, keep=keep, **more), parts, flip_v)
    wheel_names = {p.name for p in parts if re.search(recipe['wheels'], p.name)}
    hub_names = {p.name for p in parts if re.search(recipe['hubs'], p.name)}
    body, glass, _ = group(recipe['body'])
    cabin, _, _ = group(recipe['cabin'])
    steering, _, _ = group(recipe['steering'])
    _, _, wheels = group(recipe['wheels'], wheel_parts=wheel_names)
    _, _, hubs = group(recipe['hubs'], wheel_parts=hub_names)
    doors = {name: group(keep)[:2] for name, keep, node, clip in recipe['doors']}
    flip = FORWARD[recipe['forward']]
    game = lambda p: (-flip(p)[0], flip(p)[1], flip(p)[2])
    lo, hi = body.bounds()
    for s in list(wheels.values()) + [d[0] for d in doors.values()]:
        a, b = s.bounds()
        lo, hi = [min(x, y) for x, y in zip(lo, a)], [max(x, y) for x, y in zip(hi, b)]
    measures = {w: s.bounds() for w, s in wheels.items()}
    across = [(a[0] + b[0]) / 2 for a, b in measures.values()]
    cx, cz = (min(across) + max(across)) / 2, (lo[2] + hi[2]) / 2
    ground = min(a[1] for a, _ in measures.values())
    def place(p): return (p[0] - cx, p[1] - ground, p[2] - cz)
    at = lambda p: place(game(p))  # a point in the file, where it ends up on the car
    def fix(s):
        s.p = [place(p) for p in s.p]
        s.tri = [t if t[7] != SIDE else t[:7] + ((LEFT if s.p[t[0]][0] < 0 else RIGHT),) + ((t[6], LEFT if s.p[t[0]][0] < 0 else RIGHT) + t[8][2:],) for t in s.tri]
        return s
    def down(s, target, lamp=320, degrees=40):
        return smooth(cut(s, target, lamp), degrees) if len(s.tri) > target else s
    for s in (body, glass, cabin, steering): fix(s)
    near = down(body, recipe['near'])
    eye = at(recipe['eye'])
    screen = [q for q in parts if q.name == recipe['screen']][0]
    ticks, needles = dials([at(p) for p in screen.p], eye)
    inside = down(cabin, recipe['inside'], 200)
    inside = Soup_of(inside)
    inside.add(ticks.p, ticks.n, ticks.tri)
    out = [('Cabin', (0, 0, 0), (0, 1, 0), 0, False, inside)]
    pivot = at(recipe['column'][0])
    axis = unit(V(at(recipe['column'][1]), pivot))
    def about_pivot(s, pv):
        m = Soup()
        m.p = [V(p, pv) for p in s.p]; m.n = list(s.n); m.tri = list(s.tri)
        return m
    out.append(('SteeringWheel', pivot, axis, 0, False, about_pivot(down(steering, recipe['wheel_rim'], 200), pivot)))
    for name, pv, into, s in needles: out.append((name, pv, into, 0, False, s))
    for name, keep, node, clip in recipe['doors']:
        shell, pane = doors[name]
        fix(shell); fix(pane)
        file_pivot, file_axis, degrees = hinge(data, node, clip)
        pv = at(file_pivot)
        ax = unit(V(at(A(file_pivot, file_axis)), pv))
        # Check the swing against the file: a corner of the door turned there and here must land in the same place.
        test = (file_pivot[0], file_pivot[1] + 0.3, file_pivot[2] + 0.8)
        there = at(A(file_pivot, about(file_axis, degrees, V(test, file_pivot))))
        here = A(pv, about(ax, degrees, V(at(test), pv)))
        if size(V(there, here)) > 1e-6: raise SystemExit('%s turns the wrong way' % name)
        out.append((name, pv, ax, degrees, False, about_pivot(down(shell, recipe['door'], 120), pv)))
        if pane.tri: out.append((name + 'Glass', pv, ax, degrees, True, about_pivot(pane, pv)))
    if 'driver' in recipe:
        d = dict(recipe['driver'])
        d['hip'] = at(d['hip'])
        d['hands'] = tuple(at(h) for h in d['hands'])
        d['feet'] = tuple(at(h) for h in d['feet'])
        out.append(('Driver', (0, 0, 0), (0, 1, 0), 0, False, seated(**d)))
    # Wheels: a pair of each, the left ones turned round in the game. What holds a brake does not spin, so it has a mesh of its own, left and right being different.
    spots, kinds = [], {}
    fixed = []
    for w, s in wheels.items():
        a, b = measures[w]
        mid = [(u + v) / 2 for u, v in zip(a, b)]
        c = place(mid)
        radius = (b[1] - a[1]) / 2
        key = (round(radius * 25), round((b[0] - a[0]) * 25))
        if key not in kinds or (c[0] > 0 and kinds[key][1] <= 0):
            m = Soup()
            m.p = [V(p, mid) for p in s.p]; m.n = list(s.n); m.tri = list(s.tri)
            kinds[key] = (m, c[0])
        hub = 255
        if w in hubs:
            m = Soup()
            m.p = [V(p, mid) for p in hubs[w].p]; m.n = list(hubs[w].n); m.tri = list(hubs[w].tri)
            hub = len(fixed)
            fixed.append(smooth(simplify(m, recipe['caliper']), 50))
        spots.append([c[0], radius, c[2], c[2] > 0, c[0] < -0.02, key, hub])
    order = list(kinds)
    shapes = [smooth(simplify(kinds[k][0], recipe['wheel']), 50) if len(kinds[k][0].tri) > recipe['wheel'] else kinds[k][0] for k in order]
    spots.sort(key=lambda w: (-round(w[2], 1), w[0]))  # rounded: a millimetre between two axle ends must not put the right wheel first
    spots = [tuple(s[:5]) + (order.index(s[5]), s[6]) for s in spots]
    a, b = near.bounds()
    width = 2 * max(abs(a[0]), abs(b[0]))
    size_of = (hi[2] - lo[2], recipe['width'], b[1])
    print(kind, 'is %.2f wide, %.2f tall, %.2f long; eye %s; wheels %s' % (size_of[1], size_of[2], size_of[0], ['%.2f' % c for c in eye], ['%.2f %.2f r%.2f' % (s[0], s[2], s[1]) for s in spots]))
    empty = Soup()
    write(kind, near, glass, empty, shapes, spots, eye, recipe['head_top'], size_of, fixed, out)

COROLLA = {
    'tire.001': (RUBBER, NONE, TYRE), 'rims.001': (METAL, NONE, (170, 172, 176)), 'black1': (PLASTIC, NONE, BLACK), 'black2': (PLASTIC, NONE, BLACK),
    'body': (PAINT, NONE, (255, 255, 255)), 'body.001': (PAINT, NONE, (255, 255, 255)), 'windows.001': (GLASS, NONE, WINDOW),
    'headlight.001': (LENS, HEAD, (236, 236, 228)), 'rev light': (LENS, NONE, (225, 225, 220)), 'turn light': (LENS, SIDE, (232, 120, 20)),
    'Material.001': (METAL, NONE, (205, 208, 212)), 'taillight.002': (LENS, BRAKE, (170, 16, 14)), 'taillight.001': (LENS, TAIL, (200, 18, 16)), 'Material.002': (PLASTIC, NONE, (38, 38, 40)),
}
CNG = {
    'main base': (PAINT, NONE, WHITE), 'green': (PAINT, NONE, WHITE), 'Bumper and safety': (METAL, NONE, None), 'Tire': (RUBBER, NONE, None), 'Rim': (METAL, NONE, None),
    'Black plastic': (PLASTIC, NONE, None), 'Rubber flap': (RUBBER, NONE, None), 'Hood': (ROUGH, NONE, (24, 24, 26)), 'Seat': (ROUGH, NONE, None), 'Supportrubber': (RUBBER, NONE, TYRE),
    'Exaust': (METAL, NONE, None), 'TIre support': (METAL, NONE, (96, 96, 100)), 'Material.003': (LENS, HEAD, (236, 236, 228)), 'Material.001': (LENS, BRAKE, (170, 16, 14)),
    'Frame support': (METAL, NONE, None), 'Steer': (PLASTIC, NONE, None), 'Spring': (METAL, NONE, None), 'Engine cover': (PLASTIC, NONE, None), 'glass': (GLASS, NONE, WINDOW),
    'Dash': (PLASTIC, NONE, None), 'Spring support': (METAL, NONE, None), 'seat support': (METAL, NONE, None),
}
RICKSHAW = {
    'wheel spork': (METAL, NONE, (178, 180, 186)), 'tire': (RUBBER, NONE, TYRE), 'steel base': (METAL, NONE, (140, 142, 148)), 'seat': (ROUGH, NONE, (28, 26, 26)),
    'Material.006': (PAINT, NONE, WHITE), 'mudguard': (PAINT, NONE, WHITE), 'back axis': (METAL, NONE, (120, 112, 100)), 'Material.003': (TRIM, NONE, (116, 44, 30)), 'Material.007': (TRIM, NONE, (108, 62, 34)),
    'wood base': (ROUGH, NONE, (110, 84, 60)), 'Material.010': (METAL, NONE, (56, 56, 60)), 'Material.004': (ROUGH, NONE, (216, 170, 40)), 'Material.005': (PLASTIC, NONE, BLACK), 'rickshaw paint': (PAINT, NONE, WHITE),
}
TRUCK = {
    'dumper_truck_main_color': (PAINT, NONE, WHITE), 'dumper_truck_metal_dark': (METAL, NONE, (62, 62, 66)), 'dumper_truck_plastic': (PLASTIC, NONE, (34, 34, 36)), 'dumper_truck_wheels': (RUBBER, NONE, TYRE),
    'dumper_truck_wheels_metal': (METAL, NONE, (150, 150, 154)), 'dumper_truck_metal_chromed': (METAL, NONE, (205, 208, 212)), 'dumper_truck_lights': (LENS, HEAD, (236, 236, 228)),
    'dumper_truck_windows': (GLASS, NONE, WINDOW), 'dumper_truck_mirrors': (METAL, NONE, (205, 208, 212)),
}
def truck_lamps(name, part, c):
    if name != 'dumper_truck_lights': return None
    if c[2] < -200: return (LENS, SIDE, (232, 120, 20)) if abs(c[0]) > 72 else (LENS, BRAKE, (170, 16, 14))
    if c[1] < 55 or abs(c[0]) > 86: return LENS, SIDE, (232, 120, 20)
    return None

def bus_tone(c, at, part):
    r, g, b = c
    x, y, z = at
    if part != 'BusBody' and part != 'Mirror': return (RUBBER, NONE, TYRE) if r < 40 else (METAL, NONE, None)
    if r > 200 and g > 200 and z > 10 and y < -1.5: return LENS, HEAD, (236, 236, 228)
    if r > 230 and g < 20 and z < -11: return LENS, BRAKE, (170, 16, 14)
    if r > 230 and 60 < g < 130 and z < -11: return LENS, SIDE, (232, 120, 20)
    if r > 140 and g < 40: return PAINT, NONE, WHITE
    if 90 <= r <= 150 and abs(r - g) < 40: return GLASS, NONE, (44, 52, 58)
    return None

# Boxes on the sheet with writing in them (route signs, plates, stickers, an emblem), each with the point it is painted over from.
DARK, GLAZED, BLUE, PALE = (120, 300), (120, 300), (700, 700), (500, 470)
NAGOYA_BLANKS = [(x0, y0, x1, y1) + src for x0, y0, x1, y1, src in [
    (86, 224, 298, 257, (70, 500)), (100, 352, 134, 366, GLAZED), (168, 384, 214, 410, BLUE), (150, 421, 234, 438, BLUE), (234, 413, 272, 434, BLUE),
    (412, 231, 547, 256, (70, 500)), (370, 274, 409, 294, GLAZED), (556, 281, 581, 294, GLAZED), (366, 327, 392, 351, PALE), (392, 334, 444, 352, PALE),
    (450, 409, 503, 437, PALE), (504, 398, 562, 416, PALE),
    (348, 520, 409, 557, (170, 520)), (203, 603, 283, 616, (170, 520)), (350, 603, 406, 616, (170, 520)), (417, 600, 456, 621, (170, 520)), (472, 606, 497, 621, (170, 520)),
    (166, 624, 200, 641, PALE), (166, 641, 192, 657, PALE), (348, 619, 408, 631, PALE), (282, 641, 321, 655, BLUE), (604, 621, 662, 632, PALE), (604, 632, 662, 642, BLUE),
    (168, 798, 188, 816, (100, 810)), (738, 869, 818, 882, (170, 520)), (622, 882, 682, 896, PALE), (622, 896, 682, 907, BLUE), (882, 927, 922, 941, BLUE), (166, 894, 182, 913, PALE),
    (806, 12, 838, 28, (770, 42)), (836, 35, 862, 45, (770, 42))]]

BIKE = {
    'glass_b': (GLASS, NONE, (60, 66, 70)), 'glass_85': (GLASS, NONE, (60, 66, 70)), 'mvls': (LENS, NONE, (200, 200, 200)), 'carbon': (PLASTIC, NONE, (26, 26, 28)),
    'tyre_f': (RUBBER, NONE, TYRE), 'tyre_r': (RUBBER, NONE, TYRE), 'rim454_001_d': (METAL, NONE, (170, 150, 90)), 'lights_n': (LENS, NONE, (200, 200, 200)),
}
def bike_finish(name, part, c):
    """Most of this model's colour is in its material names; the pictures on those are plain white."""
    m = re.match(r'rgb([0-9a-f]{6})', name)
    if m:
        h = m.group(1)
        colour = (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16))
        return (METAL if h[:2] in ('8a', '78', 'a6', 'ce') else PLASTIC), NONE, colour
    if name.startswith('black'): return PLASTIC, NONE, BLACK
    if name == 'vehicle_generic_smallspecmap [PRIMARY]': return PAINT, NONE, WHITE
    if name == 'vehicle_generic_smallspecmap': return METAL, NONE, (176, 178, 182)
    if name == 'plastic': return PLASTIC, NONE, (34, 34, 36)
    if name == 'exmanifold454_001_d': return METAL, NONE, (120, 112, 100)
    return None

BIKE_PARTS = {
    'headlight_l': (LENS, HEAD, (236, 236, 228)), 'headlight_r': (LENS, HEAD, (236, 236, 228)), 'extralight_1': (LENS, HEAD, (236, 236, 228)),
    'taillight_l': (LENS, TAIL, (150, 16, 14)), 'taillight_r': (LENS, TAIL, (150, 16, 14)), 'brakelight_l': (LENS, BRAKE, (170, 16, 14)), 'brakelight_r': (LENS, BRAKE, (170, 16, 14)),
    'indicator_lf.001': (LENS, SIDE, (232, 120, 20)), 'indicator_rf.001': (LENS, SIDE, (232, 120, 20)),
}
AMBER, RED, LAMP_WHITE = (232, 120, 20), (170, 16, 14), (236, 236, 228)

def jetta_finish(name, part, c):
    m = name.split(' [')[0]
    if m == 'Gris Franela': return PAINT, NONE, WHITE
    if m.startswith('Windows'): return GLASS, NONE, WINDOW
    if m == 'Glass Red': return 'drop'
    if m.startswith('Glass Clear'): return (LENS, SIDE, AMBER) if part.startswith('Ext_Door_') else 'drop'
    if m == 'Glow DRL': return LENS, HEAD, LAMP_WHITE
    if m == 'Reflector Orange': return LENS, SIDE, AMBER
    if m == 'Glow Red': return (LENS, SIDE, AMBER) if part.startswith('Ext_Taillight') and abs(c[0]) > 0.66 else (LENS, BRAKE, RED)
    if m == 'Glow Red Darker': return LENS, TAIL, RED
    if m == 'Material Atlas Taillight AR': return LENS, TAIL, None
    if m == 'Reflector White': return LENS, NONE, (214, 214, 208)
    if m.startswith('Reflector Red'): return LENS, NONE, (120, 12, 10)
    if m.startswith('Chrome'): return METAL, NONE, (205, 208, 212)
    if m == 'Glow Plate Lights': return LENS, MARKER, LAMP_WHITE
    if m == 'Ambilight Cross Sport': return TRIM, DASH, (150, 40, 34)
    if m.startswith('Display'): return PLASTIC, (DASH if 'Radio' in m else NONE), (14, 16, 18)
    if m == 'Brake Rotor Dark': return METAL, NONE, None
    if m == 'Tires': return RUBBER, NONE, None
    if m == 'Leather Smooth Perforated Dark Grey': return ROUGH, NONE, (44, 44, 46)  # the picture is the holes, near white, and they do not survive one colour a triangle
    if m.startswith(('Leather', 'Int Carpet', 'Stitching', 'Int Seatbelt', 'Roofliner', 'Sun Visor', 'Pillars', 'Nubs', 'Speaker', 'Plastic Stripes', 'Wheelhouse')): return ROUGH, NONE, None
    if m.startswith('Material Atlas'): return None, NONE, None
    return None

def jetta_tone(colour, c, part):
    if part.startswith('Ext_Headlight') and min(colour) > 190: return LENS, HEAD, None
    return None

# Pieces of the model that carry the maker's marks and the model's name. Everything that spells or shows one comes off: boxes in the file's metres, a piece wholly inside goes.
JETTA_BADGES = {
    'Ext_Body#Material_Atlas': [((-0.08, 0.56, -2.22), (0.08, 0.72, -2.17)), ((0.32, 0.64, -2.11), (0.43, 0.69, -2.06)), ((-0.13, 0.42, -2.27), (0.13, 0.51, -2.25))],
    'Ext_Door_FL#Material_Atlas': [((-0.89, 0.73, -0.88), (-0.86, 0.78, -0.74))],
    'Ext_Door_FR#Material_Atlas': [((0.86, 0.73, -0.88), (0.89, 0.78, -0.74))],
    'Ext_Trunk_Lid#Material_Atlas': [((-0.05, 0.95, 2.35), (0.05, 1.05, 2.40)), ((-0.07, 0.905, 2.38), (0.07, 0.945, 2.40)), ((-0.13, 0.775, 2.33), (0.13, 0.85, 2.37))],
    'Int_SW#Material_Atlas': [((-0.38, 0.89, -0.38), (-0.32, 0.95, -0.33)), ((-0.41, 0.795, -0.375), (-0.29, 0.865, -0.305))],
    'Int_SW#Leather_Rough_1': [((-0.38, 0.89, -0.38), (-0.32, 0.945, -0.34))],
}
def jetta_strip(part):
    out = set()
    boxes = JETTA_BADGES.get(part.name, ())
    rim = part.name.startswith('Rim_')
    crumbs = part.name.startswith(('Int', 'Rim_'))
    if not (boxes or rim or crumbs): return out
    found = 0
    for tris in shells(part):
        vs = [part.p[v] for i in tris for v in part.tris[i][:3]]
        a = [min(q[k] for q in vs) for k in range(3)]; b = [max(q[k] for q in vs) for k in range(3)]
        if any(all(lo[k] <= a[k] and b[k] <= hi[k] for k in range(3)) for lo, hi in boxes): out.update(tris); found += 1
        # The hub's mark: a thin disc 6 cm across at the middle of the wheel.
        elif rim and b[0] - a[0] < 0.012 and 0.05 < b[1] - a[1] < 0.075: out.update(tris); found += 1
        # Switch legends and other crumbs too small to see, some of which are writing.
        elif crumbs and max(b[k] - a[k] for k in range(3)) < 0.014: out.update(tris)
    if boxes and found < len(boxes): raise SystemExit('badges not found on ' + part.name)
    return out

PAINTED = (235, 235, 235)  # near white: the game multiplies paint by each vehicle's colour

def inside(lo, hi):
    return lambda a, b: all(lo[k] <= a[k] and b[k] <= hi[k] for k in range(3))

def shell_strip(*rules):
    """What comes off a model, piece by piece: a part's name and a test on the bounds of each separate piece of it, in the file's units."""
    def strip(part):
        out = set()
        for pattern, test in rules:
            if not re.search(pattern, part.name): continue
            for tris in shells(part):
                vs = [part.p[v] for i in tris for v in part.tris[i][:3]]
                if test([min(q[k] for q in vs) for k in range(3)], [max(q[k] for q in vs) for k in range(3)]): out.update(tris)
        return out
    return strip

def eqs_finish(name, part, c):
    x, y, z = c
    x = abs(x)
    if 'CarPaint' in part: return PAINT, NONE, PAINTED
    if part.startswith('glasses_pivot_Glass__'): return GLASS, NONE, WINDOW
    if 'Glass_Front_Light' in part: return PLASTIC, NONE, (16, 17, 19)  # the black panel across the nose
    if 'Tires' in part: return RUBBER, NONE, TYRE
    if 'Rims' in part: return METAL, NONE, (172, 174, 178)
    if part.startswith('chassis_pivot_Bottom'): return PLASTIC, NONE, (24, 24, 26)
    if part.startswith('detach_door') and 'Details' in part: return METAL, NONE, (205, 208, 212)
    if part.startswith('lights_brakes'): return (LENS, SIDE, AMBER) if x > 0.74 else (LENS, BRAKE, RED)
    if part.startswith('lights_position_front'):
        if z < -1: return LENS, TAIL, RED
        if z < 1.5: return LENS, MARKER, LAMP_WHITE  # the line of light along the shoulder
        if y >= 0.685: return (LENS, SIDE, AMBER) if x > 0.5 else (LENS, MARKER, LAMP_WHITE)
        return LENS, HEAD, LAMP_WHITE
    if 'Gold' in part: return METAL, NONE, (150, 152, 156)
    return (PLASTIC, NONE, None)

def eqs_tone(colour, c, part):
    if part.startswith('chassis_pivot_Details') and sum(colour) > 420: return METAL, NONE, None
    return None

def eqs_pose(parts):
    """The headlamps stand behind the glass of the black panel across the nose, which is opaque here: they come forward through it to be seen."""
    box = inside((-0.83, 0.6, 2.0), (0.83, 0.71, 2.25))
    for part in parts:
        if not part.name.startswith('lights_position_front'): continue
        for tris in shells(part):
            corners = {v for i in tris for v in part.tris[i][:3]}
            vs = [part.p[v] for v in corners]
            if abs(sum(q[0] for q in vs) / len(vs)) > 0.5 and box([min(q[k] for q in vs) for k in range(3)], [max(q[k] for q in vs) for k in range(3)]):
                for v in corners: part.p[v] = (part.p[v][0], part.p[v][1], part.p[v][2] + 0.065)

EQS_STAR = (inside((-0.13, 0.42, 2.36), (0.13, 0.65, 2.41)), inside((-0.03, 0.73, 2.25), (0.03, 0.78, 2.31)), inside((-0.05, 0.92, -2.42), (0.05, 0.99, -2.35)), inside((-0.13, 0.70, -2.50), (0.13, 0.84, -2.46)))
EQS_WHEELS = tuple(w + k for w in ('WheelBL_pivot', 'WheelBR_pivot', 'WheelFL_pivot', 'WheelFR_pivot') for k in ('_Rims_Material.002_0', '_Tires_Material.005_0'))

def soul_finish(name, part, c):
    x, y, z = c
    x = abs(x)
    if name == 'body.002': return PAINT, NONE, PAINTED
    if name == '16in':  # one picture for tyre and wheel: the tyre is what lies outside the rim
        r = math.hypot(y - 0.085, z - (0.9 if z > 0 else -1.02))
        return (RUBBER, NONE, TYRE) if r > 0.172 else (METAL, NONE, (176, 178, 182))
    if name == 'glass_ex.001':
        if z > 1: return (LENS, SIDE, AMBER) if x > 0.5 else (LENS, HEAD, LAMP_WHITE)  # the covers of the headlamps; what they cover is hidden by them
        return GLASS, NONE, WINDOW
    if name == 'rear':
        if z < -1.2 and y > 0.4: return (LENS, BRAKE, RED) if y > 0.78 else (LENS, SIDE, AMBER) if y > 0.63 else (LENS, TAIL, RED)
        if y > 0.95: return LENS, BRAKE, RED
        if y > 0.6: return LENS, SIDE, AMBER  # on the mirrors
        return LENS, NONE, (120, 12, 10)
    if name == 'chrom': return METAL, NONE, (200, 203, 207)
    if name == 'grill': return PLASTIC, NONE, (30, 30, 32)
    if name == 'black_s': return PLASTIC, NONE, (34, 34, 36)
    return None

SLK_GONE = re.compile('mer_badge|sticker|doortags|int_symbols|embossed_alpha|chrome_alpha|gaugeface|gaugeneedle|screw_010|speaker|seat_belt|rotor_drilled|emitter|glass\\.0(0[13]|1[6-9]|2[0-3])|shared_(21|55|56|63)__')
SLK_DARK, SLK_SEAT = (30, 30, 32), (40, 34, 30)

def slk_strip(part):
    """What cannot be seen from outside: the engine, the boot, and the insides of the lamps under their covers, which are the lamps here."""
    out = set()
    m = part.mats[part.tris[0][3]].name
    if 'shared_0__carpaint' in m or 'shared_52' in m or part.name.startswith(('rim_', 'tyre_')): return out
    for tris in shells(part):
        vs = [part.p[v] for i in tris for v in part.tris[i][:3]]
        a = [min(q[k] for q in vs) for k in range(3)]; b = [max(q[k] for q in vs) for k in range(3)]
        x0, x1 = min(abs(a[0]), abs(b[0])), max(abs(a[0]), abs(b[0]))
        if a[2] > 0.45 and b[1] < 0.8 and b[2] < 1.93 and x1 < 0.66 and 'shared_50' not in m: out.update(tris); continue
        if a[2] > -1.97 and b[2] < -1 and b[1] < 0.74 and 'shared_50' not in m: out.update(tris); continue
        if a[0] * b[0] < 0 or len(tris) in (980, 244, 298): continue
        head = 0.5 <= x0 and x1 <= 0.84 and 0.4 <= a[1] and b[1] <= 0.65 and 1.45 <= a[2] and b[2] <= 1.9
        tail = 0.4 <= x0 and x1 <= 0.83 and 0.49 <= a[1] and b[1] <= 0.71 and -2.0 <= a[2] and b[2] <= -1.57
        if head or tail: out.update(tris)
    return out

def slk_finish(name, part, c):
    x, y, z = c
    x = abs(x)
    n = name.replace('__native_UV_glTF', '')
    if part == 'Object_103': return PAINT, NONE, PAINTED  # the roof
    if part in ('Object_724', 'Object_697', 'Object_706', 'Object_673', 'Object_661', 'Object_685'): return 'drop'  # each window twice over
    if part.startswith('rim_') and 'backfacing' in n: return METAL, NONE, (46, 46, 50)
    if SLK_GONE.search(n): return 'drop'
    if 'carpaint.001' in n: return PLASTIC, NONE, (26, 26, 28)  # the shuts and the insides of the wings
    if 'carpaint' in n: return PAINT, NONE, PAINTED
    if 'glass_windshield' in n: return GLASS, NONE, WINDOW
    if 'shared_52' in n: return (LENS, SIDE, AMBER) if x > 0.74 else (LENS, HEAD, LAMP_WHITE)
    if 'shared_50' in n:
        if y < 0.3: return LENS, HEAD, LAMP_WHITE  # the strip of lamps in the bumper
        if z < -1.4:
            if y > 0.76: return LENS, BRAKE, RED
            return (LENS, BRAKE, RED) if y > 0.61 else (LENS, SIDE, AMBER) if x > 0.66 else (LENS, TAIL, RED)
        if x > 0.85: return LENS, SIDE, AMBER  # on the mirrors
        return 'drop'
    if part.startswith('tyre_') or 'ridgescapsules' in n: return RUBBER, NONE, TYRE
    if part.startswith('rim_'):
        if 'matteblack' in n or 'backfacing' in n: return METAL, NONE, (46, 46, 50)
        return METAL, NONE, (196, 198, 202)
    if re.search('aluminum|chrome|metal_brushed', n): return METAL, NONE, (196, 198, 202)
    if re.search('metal|carbonfiber|rubbertrim', n): return PLASTIC, NONE, (44, 44, 46)
    if re.search('leather|alcantara|fabric|carpet', n): return ROUGH, NONE, SLK_SEAT
    return PLASTIC, NONE, SLK_DARK

def db11_finish(name, part, c):
    x, y, z = c
    x = abs(x)
    if name == 'CarPaint_1': return PAINT, NONE, PAINTED
    if name == 'CarPaint_B2': return PLASTIC, NONE, (22, 22, 24)  # the tonneau, the sills and the mirror backs
    if part.startswith('polySurface') or name.startswith('db11:'): return RUBBER, NONE, TYRE
    if name == 'Rim_Color_1': return METAL, NONE, (58, 58, 62)
    if name.startswith('Rim_Color'): return METAL, NONE, (186, 188, 192)
    if name == 'Light_Glass':
        if z > 1: return (LENS, SIDE, AMBER) if x > 0.77 else (LENS, HEAD, LAMP_WHITE)
        if y > 0.95 and x < 0.5: return LENS, BRAKE, RED
        if y > 0.95: return LENS, SIDE, AMBER  # on the mirrors
        return (LENS, TAIL, RED) if y < 0.5 else 'drop'
    if name == 'orange_glass': return LENS, SIDE, AMBER
    if name == 'red_glass':
        if y < 0.6: return LENS, TAIL, RED
        if x < 0.52: return LENS, TAIL, RED
        return (LENS, SIDE, AMBER) if x > 0.74 else (LENS, BRAKE, RED)
    if name == 'Badges': return (PLASTIC, NONE, None) if z < -1.8 and y < 0.5 else 'drop'  # the tail end of the bumper shares the badges' picture
    return None

DB11 = {
    'Interior': (ROUGH, NONE, None), 'Trim_Pno': (PLASTIC, NONE, (16, 16, 18)), 'Plastic_R': (PLASTIC, NONE, (28, 28, 30)), 'Chassis': (PLASTIC, NONE, (26, 26, 28)), 'Alum': (METAL, NONE, (66, 68, 72)),
    'chromeS': (METAL, NONE, (200, 203, 207)), 'GrillA': (PLASTIC, NONE, (24, 24, 26)), 'Grille_C': (PLASTIC, NONE, (24, 24, 26)), 'window': (GLASS, NONE, WINDOW), 'glass_surr': (GLASS, NONE, WINDOW),
    'mirror': (METAL, NONE, (205, 208, 212)), 'plate': 'drop', 'Engine': 'drop', 'Light': 'drop', 'rim_B': 'drop', 'Caliper_Color': 'drop',
}
DB11_MARKS = (inside((-0.09, 0.76, -2.33), (0.09, 0.82, -2.3)), inside((-0.09, 0.64, 2.25), (0.09, 0.68, 2.31)), inside((-0.19, 0.57, -2.38), (0.19, 0.59, -2.32)))
DB11_INNER = lambda a, b: inside((0.51, 0.77, -2.225), (0.845, 0.875, -1.84))(a, b) or inside((-0.845, 0.77, -2.225), (-0.51, 0.875, -1.84))(a, b)

TIPPER_AXLES = (217.8, -86.7, -229.4)

def tipper_finish(name, part, c):
    x, y, z = c
    if name == 'M_dump_truck_E_01':
        r = math.hypot(y - 51.63, z - min(TIPPER_AXLES, key=lambda a: abs(a - z)))
        return (METAL, NONE, (150, 150, 154)) if r < 26.6 else (RUBBER, NONE, TYRE)
    if name == 'old_lamp_glass_inst':
        if y > 100: return GLASS, NONE, WINDOW
        if z < 326: return 'drop'  # the bulbs, behind their covers
        return (LENS, SIDE, AMBER) if abs(x) > 86 else (LENS, HEAD, LAMP_WHITE)
    return None

def tipper_pose(parts):
    """The bar under the tail stands 12 cm to one side of the truck in the file."""
    part = parts[0]
    for tris in shells(part):
        corners = {v for i in tris for v in part.tris[i][:3]}
        if min(part.p[v][0] for v in corners) < -130 and min(part.p[v][2] for v in corners) < -360:
            for v in corners: part.p[v] = (part.p[v][0] + 10, part.p[v][1], part.p[v][2])

TIPPER = {'M_dumb_truck_B_01.1': (PAINT, NONE, PAINTED), 'M_dump_truck_A_01': (PAINT, NONE, PAINTED), 'M_dumb_truck_B_01': (METAL, NONE, (62, 62, 66)), 'M_dump_truck_D_01': (PLASTIC, NONE, (36, 36, 38))}

# The chopper: 208 meshes with one name, white, parked on its stand with the bars on full lock. Everything is by mesh number, and it is stood up and straightened first.
CHOPPER_FORK = {7, 8, 9, 10, 11, 13, 14, 15, 16, 17, 18, 26, 27, 28, 173, 174, 175, 176, 177, 178, 179, 180, 183, 184, 185, 186, 191, 192, 193, 194, 195, 197}

def chopper_pose(parts):
    def dot(a, b): return sum(x * y for x, y in zip(a, b))
    def unit(a): return tuple(x / math.sqrt(dot(a, a)) for x in a)
    def cross(a, b): return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])
    def about(q, axis, angle, at=(0, 0, 0)):
        v = [a - b for a, b in zip(q, at)]
        c, s, k, x = math.cos(angle), math.sin(angle), dot(axis, v), cross(axis, v)
        return tuple(at[i] + v[i] * c + x[i] * s + axis[i] * k * (1 - c) for i in range(3))
    def swing(which, axis, angle, at=(0, 0, 0)):
        for p in which:
            p.p = [about(q, axis, angle, at) for q in p.p]
            p.n = [about(q, axis, angle) for q in p.n]
    def middle(p): return [(min(q[i] for q in p.p) + max(q[i] for q in p.p)) / 2 for i in range(3)]
    across = (1, 0, 0)
    rear, neck, fork = unit((0.993, 0.122, -0.008)), unit((0.016, 0.627, 0.779)), unit((0.912, 0.31, -0.268))  # the rear axle, the steering head and the line between the fork legs, fitted to the meshes
    axis, angle = unit(cross(rear, across)), math.acos(dot(rear, across))
    swing(parts, axis, angle)  # off its stand
    neck, fork = about(neck, axis, angle), about(fork, axis, angle)
    at = middle(parts[26])
    steer = math.atan2(dot(cross(fork, across), neck), dot(fork, across) - dot(fork, neck) * dot(across, neck))
    front = [p for i, p in enumerate(parts) if i in CHOPPER_FORK]
    swing(front, neck, steer, at)  # the bars straight
    fork = about(fork, neck, steer)
    swing(front, unit(cross(fork, across)), math.acos(dot(fork, across)), at)  # and the fork in the same plane as the rear wheel, which the file's head tube is not quite
    shift = middle(parts[29])[0] - middle(parts[26])[0]
    for p in front: p.p = [(q[0] + shift, q[1], q[2]) for q in p.p]
    a, b = middle(parts[27]), middle(parts[189])
    swing(parts, (0, 1, 0), math.atan((b[0] - a[0]) / (a[2] - b[2])))  # both wheels on one line
    low = lambda p: min(q[1] for q in p.p)
    a, b = middle(parts[27]), middle(parts[189])
    swing(parts, (1, 0, 0), math.atan((low(parts[27]) - low(parts[189])) / (a[2] - b[2])))  # and both on the road

CHOPPER_RIM, CHOPPER_DARK = (172, 174, 178), (84, 84, 88)
def chopper_finish(name, part, c):
    i = int(part)
    if i in (23, 12, 0, 1, 29, 170, 171): return PAINT, NONE, PAINTED
    if i in (27, 189): return RUBBER, NONE, TYRE
    if i in (19, 172, 173, 174, 175, 179, 5, 4, 6): return METAL, NONE, CHOPPER_RIM
    if i == 2: return ROUGH, NONE, (30, 28, 26)
    if i in (15, 192): return PLASTIC, NONE, (26, 26, 28)
    if i in (104, 105, 106, 107, 110, 111, 112, 113): return METAL, NONE, CHOPPER_DARK
    if i == 177: return LENS, HEAD, LAMP_WHITE
    return METAL, NONE, CHOPPER_RIM

def chopper_strip(part):
    if part.name != '034': return ()
    return {i for tris in shells(part) for i in tris if min(part.p[v][0] for t in tris for v in part.tris[t][:3]) < -5}

DOOR_PARTS = lambda d: '^(Ext_Door_%s|Int_Door_%s|Ext_Door_Blocker_%s)#' % (d, d, d)
RECIPES = {
    'Sedan': dict(zip='lowpoly-sedan-wagon.zip', file='source/corolla.glb', keep='^(body_sedan|Cylinder\\.00[4-7])$', wheel_parts=('Cylinder.004', 'Cylinder.005', 'Cylinder.006', 'Cylinder.007'),
                  length=4.4, forward='+z', mats=COROLLA, rider=dict(who='polo', hip=(0.36, 0.42, -0.25), lean=-0.22, hands=((0.2, 0.86, 0.3), (0.52, 0.86, 0.3)), feet=((0.28, 0.27, 0.78), (0.46, 0.27, 0.78)), target=1000), closed=True, clear=True),
    'Microbus': dict(zip='lowpoly-sedan-wagon.zip', file='source/corolla.glb', keep='^(body_wagon|Cylinder(\\.00[1-3])?)$', wheel_parts=('Cylinder', 'Cylinder.001', 'Cylinder.002', 'Cylinder.003'),
                     length=4.5, forward='+z', mats=COROLLA, rider=dict(who='hoodie', hip=(0.36, 0.42, -0.25), lean=-0.22, hands=((0.2, 0.86, 0.3), (0.52, 0.86, 0.3)), feet=((0.28, 0.27, 0.78), (0.46, 0.27, 0.78)), target=1000), closed=True, clear=True),
    'Cng': dict(zip='cng.zip', file='source/CNG.fbx', length=2.7, forward='+x', drop='^(tree_small|Bolt)', clear=True, mats=CNG, near=6500, wheel=420,
                rider=dict(who='shalwar', hip=(0, 0.92, 0.2), lean=0.05, hands=((-0.3, 0.92, 0.72), (0.3, 0.92, 0.72)), feet=((-0.14, 0.5, 0.78), (0.14, 0.5, 0.78))),
                wheel_parts=('Tire', 'Rim'), wheel_at=((187, -52, 0), (0, -52, 58), (0, -52, -58)),
                parts={'battery': (PLASTIC, NONE, None), 'dash meter': (PLASTIC, NONE, None)},
                fix=lambda name, part, c: (LENS, SIDE, (232, 120, 20)) if part == 'back light' and c[1] > -14 else None),
    'Rickshaw': dict(zip='rickshaw.zip', file='source/rickshaw (1).fbx', length=2.25, forward='-x', mats=RICKSHAW, near=6000, wheel=700,
                     rider=dict(who='polo', hip=(0, 1.0, -0.1), lean=0.34, hands=((-0.175, 0.95, 0.39), (0.175, 0.95, 0.39)), feet=((-0.12, 0.42, -0.04), (0.12, 0.2, 0.3)), target=2500),
                     wheel_parts=('tire front', 'wheel spoke front', 'tire back left', 'wheel spoke back left', 'tire back right', 'wheel spoke back right'),
                     wheel_at=((-147.6, -87.7, 0), (132.9, -90.7, 76.6), (132.9, -90.7, -76.3))),
    'Truck': dict(zip='man-tga-dump-truck.zip', file='man tga.fbx', inner='source/man tga.zip', length=7.0, forward='+z', mats=TRUCK, clear=True, near=6500, wheel=420,
                  mirror=True, rider=dict(who='hoodie', hip=(0.49, 1.6, 2.4), lean=0.03, hands=((0.29, 1.9, 2.85), (0.69, 1.9, 2.85)), feet=((0.37, 1.12, 3.05), (0.61, 1.12, 3.05)), target=1200),
                  wheel_parts=('wheel_back_axis1_left', 'wheel_back_axis1_right', 'wheel_back_axis2_left', 'wheel_back_axis2_right', 'wheel_front_left', 'wheel_front_right'),
                  parts={'platform': (ROUGH, NONE, (112, 74, 48)), 'platform_door': (ROUGH, NONE, (112, 74, 48))}, fix=truck_lamps),
    'Coach': dict(zip='japanese-bus-nagoya-city-bus-aichi.zip', file='source/nagoyacitybus.fbx', length=10.9, forward='-x', mats={'Nagoya': (PLASTIC, NONE, None)}, near=8000, wheel=2000, picture=1024, sheet=1024,
                  wheel_shell=lambda lo, hi: hi[1] < 100, wheel_at=((1290.8, 48, -3958), (1290.8, 48, -4184), (1828, 48, -3974), (1828, 48, -4168)), blanks=NAGOYA_BLANKS, proud=0.6, plain=(0.5, 0.54),
                  patches=[(65, 415, 88, 435, (LENS, HEAD, (236, 236, 228))), (293, 415, 317, 435, (LENS, HEAD, (236, 236, 228))),
                           (365, 354, 383, 375, (LENS, SIDE, (232, 120, 20))), (383, 354, 400, 375, (LENS, BRAKE, (170, 16, 14))),
                           (555, 354, 571, 375, (LENS, BRAKE, (170, 16, 14))), (571, 354, 588, 375, (LENS, SIDE, (232, 120, 20)))]),
    'Bus': dict(zip='bus.zip', file='source/Bus4glb.glb', length=10.2, forward='+z', other=(PLASTIC, NONE, None), near=8000, wheel=420, tone=bus_tone, clear=True, closed=True,
                rider=dict(who='polo', hip=(0.7, 1.5, 3.85), lean=-0.1, hands=((0.52, 1.78, 4.3), (0.88, 1.78, 4.3)), feet=((0.6, 1.05, 4.6), (0.8, 1.05, 4.6)), target=1000),
                wheel_parts=('WheelRightFront', 'WheelBack', 'WheelLeftFront'),
                wheel_at=((-2.17, -1.8, 5.62), (2.28, -1.8, 5.62), (-2.17, -1.8, -4.25), (2.28, -1.8, -4.25), (-2.17, -1.8, -7.7), (2.28, -1.8, -7.7)), mats={}),
    'Motorcycle': dict(zip='mv-agusta-brutale-1000-serie-oro-2019.zip', file='source/88741s98.glb', length=1.98, forward='-z', near=6500, far=240, wheel=700, clear=True,
                       rider=dict(who='racer', hip=(0, 0.95, -0.3), lean=0.42, hands=((-0.38, 0.97, 0.3), (0.38, 0.97, 0.3)), feet=((-0.2, 0.33, -0.2), (0.2, 0.33, -0.2)), elbow=(0, -0.6, -0.8), target=2500),
                       drop='^(dials|steeringwAWDheel|bodyshell\\.002|bodyshell\\.001|bodyshell\\.003|handlebars\\.002|mvso)',
                       wheel_parts=('wheel_front [wheel_lf]', 'tyre_front [wheel_lf]', 'brake_front [wheel_lf]', 'wheel_rear [wheel_lr]', 'tyre_rear [wheel_lr]', 'brake_rear [wheel_lr]'),
                       wheel_at=((0, -0.26, -0.72), (0, -0.26, 0.76)), centre_wheels=True, mats=BIKE, other=(PLASTIC, NONE, None), parts=BIKE_PARTS, fix=bike_finish),
    'Saloon': dict(zip='mercedes_benz_vision_eqs.zip', file='scene.gltf', length=5.2, forward='+z', clear=True, closed=True, mats={}, near=7500, far=290, far_weld=0.04, far_edge=1.0, wheel=620, fix=eqs_finish, tone=eqs_tone, pose=eqs_pose,
                   drop='^(bone_caliper|chassis_pivot_RearLight|lights_position_back|lights_reverse)',
                   strip=shell_strip(('^(chassis_pivot_Details|lights_position_front)', lambda a, b: any(t(a, b) for t in EQS_STAR)), ('^lights_position_front', lambda a, b: b[2] > 2.5 or (a[1] < 0.4 and b[2] > 1))),
                   wheel_parts=EQS_WHEELS, wheel_at=((0.81, 0.403, 1.616), (-0.81, 0.403, 1.616), (0.81, 0.403, -1.616), (-0.81, 0.403, -1.616)),
                   rider=dict(who='polo', hip=(0.42, 0.44, -0.05), lean=-0.22, hands=((0.26, 0.9, 0.5), (0.58, 0.9, 0.5)), feet=((0.34, 0.27, 1.0), (0.52, 0.27, 1.0)), target=1000)),
    'Hatch': dict(zip='2016-kia-soul.zip', file='source/2016 Kia Soul.glb', length=4.15, forward='-z', mirror=True, clear=True, closed=True, near=7500, far=290, far_weld=0.04, far_edge=1.0, wheel=420,
                  mats={'kia': 'drop', 'front': 'drop'}, fix=soul_finish, wheel_mats=('16in',), wheel_at=((0.49, 0.085, -0.9), (-0.49, 0.085, -0.9), (0.49, 0.085, 1.02), (-0.49, 0.085, 1.02)),
                  strip=shell_strip(('^vitals', lambda a, b: b[1] > 1.09 or (a[1] < 0.435 and inside((-0.59, 0.42, -1.42), (0.59, 0.61, -1.02))(a, b)))),
                  rider=dict(who='hoodie', hip=(0.36, 0.62, 0.2), lean=-0.15, hands=((0.2, 1.02, 0.72), (0.52, 1.02, 0.72)), feet=((0.28, 0.33, 1.1), (0.46, 0.33, 1.1)), target=1000)),
    'Roadster': dict(zip='mercedes-benz_slk_55_amg_2012.zip', file='scene.gltf', by_parent=True, length=4.1, forward='+z', mirror=True, clear=True, closed=True, near=7500, far=290, far_weld=0.04, far_edge=1.0, wheel=640, glass=640,
                     mats={}, fix=slk_finish, strip=slk_strip, drop='^(MER_caliper|MER_rotor|controlArm)', wheel_names='^(rim|tyre)_(LF|RF|LR|RR)_',
                     wheel_at=((0.769, 0.188, 1.19), (-0.769, 0.188, 1.19), (0.77, 0.184, -1.234), (-0.77, 0.184, -1.234)),
                     rider=dict(who='polo', hip=(0.36, 0.38, -0.3), lean=-0.3, hands=((0.2, 0.8, 0.2), (0.52, 0.8, 0.2)), feet=((0.28, 0.25, 0.75), (0.46, 0.25, 0.75)), target=1000)),
    'Sports': dict(zip='2019-aston-martin-db11-volante.zip', inner='source/FINAL_MODEL_19.zip', file='FINAL_MODEL_19.fbx', length=4.75, forward='+z', mirror=True, clear=True, near=7500, far=290, far_weld=0.04, far_edge=1.0, wheel=680,
                   mats=DB11, fix=db11_finish, drop='^(CalliperParking|polySurface(1|2|3|4|5|28|51|76)$|(VOL:)?M_RimBadge)', strip=shell_strip(('^VOL:M_CarPaint_Max', lambda a, b: any(t(a, b) for t in DB11_MARKS)), ('^VOL:M_LightGlass', DB11_INNER)),
                   wheel_parts=('polySurface77', 'polySurface230', 'polySurface389', 'polySurface542', 'VOL:M_Rim_NoTint_Max', 'M_Rim_NoTint_Max', 'VOL:M_Rim_Main_SetB_Max', 'M_Rim_Main_SetB_Max'),
                   wheel_at=((0.8365, 0.35, 1.405), (-0.8365, 0.35, 1.405), (0.8155, 0.353, -1.394), (-0.8155, 0.353, -1.394)),
                   rider=dict(who='gangster', hip=(0.38, 0.35, -0.35), lean=-0.25, hands=((0.22, 0.86, 0.18), (0.54, 0.86, 0.18)), feet=((0.3, 0.27, 0.72), (0.48, 0.27, 0.72)), target=1000)),
    'Chopper': dict(zip='chopper-motorcycle.zip', file='source/Chopper .fbx', numbered=True, length=2.45, forward='-z', clear=True, near=6800, far=290, far_weld=0.03, far_edge=1.0, wheel=700, mats={}, fix=chopper_finish, pose=chopper_pose, strip=chopper_strip,
                    drop='^196$', wheel_parts=('027', '173', '174', '175', '179', '189', '172', '019', '004', '005', '006'), wheel_at=((-1.13, -4.81, -17.63), (-1.15, -5.49, 18.31)), centre_wheels=True,
                    lamps=[((-2.1, -2.25), (-0.2, -1.8), 23.05, 1, (LENS, BRAKE, RED)), ((-2.1, -2.7), (-0.2, -2.25), 23.05, 1, (LENS, TAIL, RED)),
                           ((-5.6, -6.2), (-4.6, -5.4), 21.0, 1, (LENS, SIDE, AMBER)), ((2.3, -6.2), (3.3, -5.4), 21.0, 1, (LENS, SIDE, AMBER)),
                           ((-8.6, 6.55), (-7.6, 7.15), -2.7, -1, (LENS, SIDE, AMBER)), ((4.4, 6.55), (5.4, 7.15), -2.7, -1, (LENS, SIDE, AMBER))],
                    far_tubes=[((x, 0.315, 0.91), (x, 0.9, 0.22), 0.022, METAL, CHOPPER_RIM) for x in (-0.12, 0.12)] + [((0, 0.86, 0.22), (0, 0.1, 0.27), 0.025, PAINT, PAINTED), ((0, 0.07, 0.27), (0, 0.07, -0.6), 0.03, PAINT, PAINTED), ((0, 0.07, -0.6), (0, 0.3, -0.95), 0.03, PAINT, PAINTED)]
                              + [((0, 0.92, 0.25), (x, 0.92, 0.08), 0.015, METAL, CHOPPER_RIM) for x in (-0.37, 0.37)],
                    rider=dict(who='racer', hip=(0, 0.62, -0.6), lean=0.3, hands=((-0.26, 0.91, 0.07), (0.26, 0.91, 0.07)), feet=((-0.3, 0.17, 0.08), (0.3, 0.17, 0.08)), elbow=(0, -0.6, -0.8), target=2200)),
    'Tipper': dict(zip='dump-truck.zip', file='source/dump_truck_01.fbx', length=7.2, forward='+z', clear=True, closed=True, near=7500, far=290, far_weld=0.05, far_edge=1.0, wheel=520, mats=TIPPER, fix=tipper_finish, wheel_mats=('M_dump_truck_E_01',),
                   wheel_at=tuple((x, 51.63, z) for z in TIPPER_AXLES for x in (86.9, -84.1)),
                   # It has no lamps at the back: these are boards on the bar under the tail.
                   lamps=[((-112, 45), (-80, 62), -364, -1, (LENS, BRAKE, RED)), ((75, 45), (107, 62), -364, -1, (LENS, BRAKE, RED)), ((-126, 45), (-112, 62), -364, -1, (LENS, SIDE, AMBER)), ((107, 45), (121, 62), -364, -1, (LENS, SIDE, AMBER)),
                          ((-80, 45), (-60, 62), -364, -1, (LENS, TAIL, RED)), ((55, 45), (75, 62), -364, -1, (LENS, TAIL, RED))], pose=tipper_pose,
                   rider=dict(who='hoodie', hip=(0.6, 1.6, 2.5), lean=0.03, hands=((0.4, 1.9, 2.95), (0.8, 1.9, 2.95)), feet=((0.48, 1.12, 3.15), (0.72, 1.12, 3.15)), target=1200)),
}

# Neither police archive holds a bike: both are the same man standing with his arms out, a figure from a bike game, with no skeleton to sit him by. Until there is one, this is the MV Agusta at the size the police bike was meant to be.
RECIPES['PoliceBike'] = dict(RECIPES['Motorcycle'], length=2.15, rider=dict(who='racer', hip=(0, 1.03, -0.33), lean=0.3, hands=((-0.41, 1.05, 0.33), (0.41, 1.05, 0.33)), feet=((-0.22, 0.36, -0.22), (0.22, 0.36, -0.22)), elbow=(0, -0.6, -0.8), target=2500))

DRIVEN = {
    'Driven/Sedan': dict(zip='volkswagen-jetta-gli-2025.zip', file='source/jetta_gli_my25_lights_on.glb', forward='-z', mirror=True, clear=True, hold_colour=True, mats={}, fix=jetta_finish, tone=jetta_tone, strip=jetta_strip,
                         drop='^(Int_Airbag|Int#Phone|Int#Carpet_Trunk|Int_Trunk_Lid|Int_Window_|Int_Controls|Int_Door_..#Windows|Ext_Door_Limiter|Ext_Sun_Roof#Windows_Tinted|Wind_Deflector|Ext_Body_Exhaust_System|Int_Cupholder_Strap)',
                         body='^(Ext_Body|Ext_Body_Blocker|Ext_Body_Exhaust|Ext_Body_GrilleInterior|Ext_Body_Grille_Shadow|Ext_Headlight|Ext_Taillight|Ext_Trunk_Lid|Ext_Trunk_Lid_Light|Ext_Sun_Roof|Ext_Window_\\w+)#',
                         cabin='^(Int|Int_Center|Int_Cupholder_Rest|Int_Seats_SCB)#', steering='^Int_SW#', wheels='^(Rim_|Tire_|Ext_Brake_(FL|FR|Rear)#(Brake_Rotor|Material_Atlas))', hubs='^Ext_Brake_(FL|FR|Rear)#Caliper',
                         wheel_at=((-0.775, 0.33, -1.34), (0.775, 0.33, -1.34), (-0.77, 0.33, 1.34), (0.77, 0.33, 1.34)),
                         doors=[('DoorFrontRight', DOOR_PARTS('FL'), 'EXT_DOOR_FL', 'Front left door'), ('DoorFrontLeft', DOOR_PARTS('FR'), 'EXT_DOOR_FR', 'Front right door'),
                                ('DoorRearRight', DOOR_PARTS('RL'), 'EXT_DOOR_RL', 'Rear left door'), ('DoorRearLeft', DOOR_PARTS('RR'), 'EXT_DOOR_RR', 'Rear right door')],
                         screen='Int#IP_Screen_Welcome', column=((-0.348, 0.916, -0.380), (-0.348, 0.871, -0.469)), eye=(-0.348, 1.20, 0.07), head_top=1.33, width=1.8,
                         near=40000, inside=28000, wheel_rim=3500, door=4200, wheel=1800, caliper=160,
                         driver=dict(who='hoodie', hip=(-0.348, 0.56, 0.11), lean=-0.3, hands=((-0.178, 0.91, -0.385), (-0.518, 0.91, -0.385)), feet=((-0.26, 0.32, -0.78), (-0.44, 0.31, -0.80)), elbow=(0, -1, -0.4), target=1800)),
}

def zodiac_finish(name, part, c):
    if name == 'Zodiac_gris_fonc' and c[2] < 45: return METAL, NONE, (58, 60, 64)  # the motors; the rest of it is the floor and the seats
    return None

ZODIAC = dict(zip='zodiac_boat.zip', file='scene.gltf', forward='-z', clear=True, length=4.13, draft=0.16, near=2500, far=150, far_weld=0.05, far_edge=1.0, glass=160, other=(PLASTIC, NONE, None), fix=zodiac_finish,
              far_tubes=[t for x in (-1, 1) for t in (((x * 0.46, 0.19, -0.37), (x * 0.3, 0.83, -0.43), 0.02, PLASTIC, (40, 40, 44)), ((x * 0.3, 0.83, -0.43), (x * 0.22, 0.96, -0.46), 0.02, PLASTIC, (40, 40, 44)),
                                                  ((x * 0.21, 0.45, -1.96), (x * 0.21, -0.1, -1.96), 0.04, METAL, (58, 60, 64)))] + [((-0.22, 0.96, -0.46), (0.22, 0.96, -0.46), 0.02, PLASTIC, (40, 40, 44))],
              mats={'Zodiac_NEUTRE': 'drop', 'Zodiac_couleur_boudin.': (PAINT, NONE, PAINTED), 'Zodiac_Verre_translucide_gris_clair': (GLASS, NONE, WINDOW), 'Zodiac_Verre_translucide_vert': (GLASS, NONE, WINDOW),
                    'Zodiac_Beige_sombre': (METAL, NONE, None), 'Zodiac_Gris_clair': (METAL, NONE, None)})

# The same vehicles with no one on them, for the player to take. eye: the driver's eye; head_top: the top of their head. Right-hand drive, so on the right; on the middle line on a bike or a CNG. Read off the figure each one carries in traffic; the coach has none, so its eye is set by the windscreen in its picture.
EMPTY = {
    'Sedan': dict(eye=(0.36, 1.14, -0.29), head_top=1.25),
    'Microbus': dict(eye=(0.36, 1.15, -0.23), head_top=1.26),
    'Hatch': dict(eye=(0.36, 1.36, 0.26), head_top=1.47),
    'Saloon': dict(eye=(0.42, 1.16, -0.09), head_top=1.27),
    'Roadster': dict(eye=(0.36, 1.08, -0.38), head_top=1.2),
    'Sports': dict(eye=(0.38, 1.09, -0.35), head_top=1.21),
    'Motorcycle': dict(eye=(0, 1.65, 0.03), head_top=1.77, width=0.8),
    'Chopper': dict(eye=(0, 1.34, -0.33), head_top=1.46, width=0.9),
    'PoliceBike': dict(eye=(0, 1.75, -0.06), head_top=1.87, width=0.85),
    'Bus': dict(eye=(0.7, 2.24, 3.87), head_top=2.35),
    'Coach': dict(eye=(0.7, 2.05, 4.5), head_top=2.18),
    'Truck': dict(eye=(0.49, 2.34, 2.55), head_top=2.46),
    'Tipper': dict(eye=(0.6, 2.34, 2.65), head_top=2.46),
    'Cng': dict(eye=(0, 1.45, 0.25), head_top=1.58),
}

if __name__ == '__main__':
    every = list(RECIPES) + list(DRIVEN) + ['Empty/' + k for k in EMPTY] + ['Empty/Boat']
    for kind in sys.argv[1:] or every:
        if kind in DRIVEN: driven(kind, DRIVEN[kind])
        elif kind == 'Empty/Boat': boat(kind, ZODIAC, (0.18, 0.98, -0.78), 1.11)  # on the helm seat, on the right
        elif kind.startswith('Empty/'): vehicle(kind[6:], RECIPES[kind[6:]], EMPTY[kind[6:]])
        else: vehicle(kind, RECIPES[kind])
