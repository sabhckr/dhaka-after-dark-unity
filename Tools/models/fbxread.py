# Binary FBX, read with nothing but the standard library (2026-11-06), for bake.py. Gives each mesh node's triangles in world space, with its normals, UVs and materials,
# and each material's colour and picture. Run on a file to see what is in it:  python3 -I Tools/models/fbxread.py <file.fbx>
import struct, zlib, math, sys, os
from array import array

class Node:
    __slots__ = ('name', 'props', 'kids')
    def __init__(self, name, props, kids): self.name, self.props, self.kids = name, props, kids
    def get(self, name):
        for k in self.kids:
            if k.name == name: return k
    def all(self, name): return [k for k in self.kids if k.name == name]

def _prop(b, o):
    t = b[o:o + 1]; o += 1
    if t == b'Y': return struct.unpack_from('<h', b, o)[0], o + 2
    if t == b'C': return b[o] != 0, o + 1
    if t == b'I': return struct.unpack_from('<i', b, o)[0], o + 4
    if t == b'F': return struct.unpack_from('<f', b, o)[0], o + 4
    if t == b'D': return struct.unpack_from('<d', b, o)[0], o + 8
    if t == b'L': return struct.unpack_from('<q', b, o)[0], o + 8
    if t in (b'S', b'R'):
        n = struct.unpack_from('<I', b, o)[0]; o += 4
        v = b[o:o + n]
        return (v.decode('utf-8', 'replace') if t == b'S' else v), o + n
    if t in (b'f', b'd', b'l', b'i', b'b', b'c'):
        n, enc, size = struct.unpack_from('<III', b, o); o += 12
        raw = b[o:o + size]
        if enc == 1: raw = zlib.decompress(raw)
        code = {b'f': 'f', b'd': 'd', b'l': 'q', b'i': 'i', b'b': 'b', b'c': 'b'}[t]
        return array(code, raw), o + size
    raise ValueError('property type %r at %d' % (t, o))

def _node(b, o, wide):
    if wide: end, count, _, = struct.unpack_from('<QQQ', b, o); o += 24
    else: end, count, _ = struct.unpack_from('<III', b, o); o += 12
    n = b[o]; o += 1
    if end == 0: return None, o
    name = b[o:o + n].decode('ascii', 'replace'); o += n
    props = []
    for _ in range(count):
        v, o = _prop(b, o)
        props.append(v)
    kids = []
    while o < end:
        k, o = _node(b, o, wide)
        if k is None: break
        kids.append(k)
    return Node(name, props, kids), end

def parse(b):
    if b[:20] != b'Kaydara FBX Binary  ': raise ValueError('not a binary FBX')
    version = struct.unpack_from('<I', b, 23)[0]
    wide = version >= 7500
    o, top = 27, []
    while o < len(b) - 13:
        k, o = _node(b, o, wide)
        if k is None: break
        top.append(k)
    return Node('', [], top)

# ---- 4x4 matrices, row major, column vectors ----
I4 = [[1, 0, 0, 0], [0, 1, 0, 0], [0, 0, 1, 0], [0, 0, 0, 1]]
def mul(a, b): return [[sum(a[i][k] * b[k][j] for k in range(4)) for j in range(4)] for i in range(4)]
def trans(t): return [[1, 0, 0, t[0]], [0, 1, 0, t[1]], [0, 0, 1, t[2]], [0, 0, 0, 1]]
def scale(s): return [[s[0], 0, 0, 0], [0, s[1], 0, 0], [0, 0, s[2], 0], [0, 0, 0, 1]]
def euler(r):
    x, y, z = (math.radians(v) for v in r)
    cx, sx, cy, sy, cz, sz = math.cos(x), math.sin(x), math.cos(y), math.sin(y), math.cos(z), math.sin(z)
    rx = [[1, 0, 0, 0], [0, cx, -sx, 0], [0, sx, cx, 0], [0, 0, 0, 1]]
    ry = [[cy, 0, sy, 0], [0, 1, 0, 0], [-sy, 0, cy, 0], [0, 0, 0, 1]]
    rz = [[cz, -sz, 0, 0], [sz, cz, 0, 0], [0, 0, 1, 0], [0, 0, 0, 1]]
    return mul(rz, mul(ry, rx))
def invert_rigid(m):
    r = [[m[j][i] for j in range(3)] for i in range(3)]
    return [r[0] + [0], r[1] + [0], r[2] + [0], [0, 0, 0, 1]]
def neg(t): return [-t[0], -t[1], -t[2]]

def props70(n):
    out = {}
    p = n.get('Properties70')
    if p is None: return out
    for e in p.all('P'):
        out[e.props[0]] = e.props[4:]
    return out

def local(model):
    p = props70(model)
    v = lambda k, d: tuple(p[k][:3]) if k in p else d
    z, one = (0, 0, 0), (1, 1, 1)
    t, r, s = v('Lcl Translation', z), v('Lcl Rotation', z), v('Lcl Scaling', one)
    pre, post = euler(v('PreRotation', z)), euler(v('PostRotation', z))
    rp, ro, sp, so = v('RotationPivot', z), v('RotationOffset', z), v('ScalingPivot', z), v('ScalingOffset', z)
    m = mul(trans(t), trans(ro))
    m = mul(m, trans(rp))
    m = mul(m, pre)
    m = mul(m, euler(r))
    m = mul(m, invert_rigid(post))
    m = mul(m, trans(neg(rp)))
    m = mul(m, trans(so))
    m = mul(m, trans(sp))
    m = mul(m, scale(s))
    m = mul(m, trans(neg(sp)))
    geo = mul(trans(v('GeometricTranslation', z)), mul(euler(v('GeometricRotation', z)), scale(v('GeometricScaling', one))))
    return m, geo

class Material:
    def __init__(self, name, colour, picture, file): self.name, self.colour, self.picture, self.file = name, colour, picture, file

class Part:
    """One mesh node: corners in world space; tris are (corner, corner, corner, material index). A skinned mesh also has, per corner, its bones and weights (skin) and where each bone stands at rest (bones)."""
    def __init__(self, name): self.name, self.p, self.n, self.uv, self.tris, self.mats, self.skin, self.bones = name, [], [], [], [], [], [], {}

def layer(g, name, data, index):
    e = g.get(name)
    if e is None or e.get(data) is None: return None
    mapping = e.get('MappingInformationType').props[0]
    ref = e.get('ReferenceInformationType').props[0]
    idx = e.get(index) if ref != 'Direct' else None
    return mapping, e.get(data).props[0], idx.props[0] if idx is not None else None

def scene(data):
    root = parse(data)
    objects = {}
    for o in root.get('Objects').kids:
        if o.props: objects[o.props[0]] = o
    up, down = {}, {}
    for c in root.get('Connections').all('C'):
        child, parent = c.props[1], c.props[2]
        prop = c.props[3] if len(c.props) > 3 else None
        up.setdefault(child, []).append((parent, prop))
        down.setdefault(parent, []).append((child, prop))
    clean = lambda o: o.props[1].split('\x00')[0].split('::')[-1]
    def world(id):
        o = objects[id]
        m, _ = local(o)
        for parent, _ in up.get(id, []):
            if parent in objects and objects[parent].name == 'Model': return mul(world(parent), m)
        return m
    mats = {}
    def material(id):
        if id in mats: return mats[id]
        o = objects[id]
        p = props70(o)
        colour = tuple(p.get('DiffuseColor', p.get('Diffuse', (0.8, 0.8, 0.8)))[:3])
        picture = file = None
        for child, prop in down.get(id, []):
            t = objects.get(child)
            if t is None or t.name != 'Texture' or prop != 'DiffuseColor': continue
            f = t.get('RelativeFilename') or t.get('FileName')
            file = f.props[0] if f is not None else None
            for v, _ in down.get(child, []):
                if v in objects and objects[v].name == 'Video' and objects[v].get('Content') is not None and objects[v].get('Content').props:
                    c = objects[v].get('Content').props[0]
                    if isinstance(c, (bytes, bytearray)) and len(c): picture = bytes(c)
        mats[id] = Material(clean(o), colour, picture, file)
        return mats[id]
    parts = []
    for id, o in objects.items():
        if o.name != 'Model': continue
        geom = None
        slots = []
        for child, _ in down.get(id, []):
            c = objects.get(child)
            if c is None: continue
            if c.name == 'Geometry' and c.get('Vertices') is not None: geom, geom_id = c, child
            elif c.name == 'Material': slots.append(material(child))
        if geom is None: continue
        w = world(id)
        _, geo = local(o)
        w = mul(w, geo)
        part = Part(clean(o))
        part.mats = slots
        v = geom.get('Vertices').props[0]
        pv = geom.get('PolygonVertexIndex').props[0]
        nor = layer(geom, 'LayerElementNormal', 'Normals', 'NormalsIndex')
        uv = layer(geom, 'LayerElementUV', 'UV', 'UVIndex')
        mat = geom.get('LayerElementMaterial')
        mmap = mat.get('MappingInformationType').props[0] if mat is not None else 'AllSame'
        marr = mat.get('Materials').props[0] if mat is not None else [0]
        pos = []
        for i in range(0, len(v), 3):
            x, y, z = v[i], v[i + 1], v[i + 2]
            pos.append((w[0][0] * x + w[0][1] * y + w[0][2] * z + w[0][3], w[1][0] * x + w[1][1] * y + w[1][2] * z + w[1][3], w[2][0] * x + w[2][1] * y + w[2][2] * z + w[2][3]))
        det = (w[0][0] * (w[1][1] * w[2][2] - w[1][2] * w[2][1]) - w[0][1] * (w[1][0] * w[2][2] - w[1][2] * w[2][0]) + w[0][2] * (w[1][0] * w[2][1] - w[1][1] * w[2][0]))
        # Normals go through the inverse transpose; the cofactors of the 3x3 part are that, up to a scale.
        cof = [[w[1][1] * w[2][2] - w[1][2] * w[2][1], w[1][2] * w[2][0] - w[1][0] * w[2][2], w[1][0] * w[2][1] - w[1][1] * w[2][0]],
               [w[0][2] * w[2][1] - w[0][1] * w[2][2], w[0][0] * w[2][2] - w[0][2] * w[2][0], w[0][1] * w[2][0] - w[0][0] * w[2][1]],
               [w[0][1] * w[1][2] - w[0][2] * w[1][1], w[0][2] * w[1][0] - w[0][0] * w[1][2], w[0][0] * w[1][1] - w[0][1] * w[1][0]]]
        sign = 1 if det >= 0 else -1
        def turned(n):
            x, y, z = n
            a = sign * (cof[0][0] * x + cof[0][1] * y + cof[0][2] * z)
            b = sign * (cof[1][0] * x + cof[1][1] * y + cof[1][2] * z)
            c = sign * (cof[2][0] * x + cof[2][1] * y + cof[2][2] * z)
            l = math.sqrt(a * a + b * b + c * c) or 1
            return a / l, b / l, c / l
        def fetch(el, width, corner, vertex, poly):
            mapping, arr, idx = el
            i = corner if mapping == 'ByPolygonVertex' else vertex if mapping in ('ByVertice', 'ByVertex') else poly if mapping == 'ByPolygon' else 0
            if idx is not None: i = idx[i]
            return tuple(arr[i * width + k] for k in range(width))
        # Each cluster ties some vertices to one bone; TransformLink is that bone in the world at rest. Bones without a cluster (the ends of chains) stand where the scene puts them.
        ties = {}
        for d, _ in down.get(geom_id, []):
            if d not in objects or objects[d].name != 'Deformer': continue
            for c, _ in down.get(d, []):
                cl = objects.get(c)
                if cl is None or cl.name != 'Deformer' or cl.get('Indexes') is None: continue
                bone = next((clean(objects[b]).split(':')[-1] for b, _ in down.get(c, []) if b in objects and objects[b].name == 'Model'), None)
                if bone is None: continue
                link = cl.get('TransformLink').props[0]
                part.bones[bone] = (link[12], link[13], link[14])
                for i, wt in zip(cl.get('Indexes').props[0], cl.get('Weights').props[0]): ties.setdefault(i, []).append((bone, wt))
        if ties:
            for b, o2 in objects.items():
                if o2.name == 'Model' and o2.props[2] == 'LimbNode':
                    m = world(b)
                    part.bones.setdefault(clean(o2).split(':')[-1], (m[0][3], m[1][3], m[2][3]))
        poly, start, ring = 0, 0, []
        for k, q in enumerate(pv):
            last = q < 0
            vi = ~q if last else q
            ring.append((k, vi))
            if not last: continue
            m = marr[poly] if mmap == 'ByPolygon' else marr[0]
            base = len(part.p)
            for c, vv in ring:
                part.p.append(pos[vv])
                part.n.append(turned(fetch(nor, 3, c, vv, poly)) if nor else (0, 1, 0))
                part.uv.append(fetch(uv, 2, c, vv, poly) if uv else (0, 0))
                if ties: part.skin.append(ties.get(vv, ()))
            for t in range(1, len(ring) - 1):
                tri = (base, base + t, base + t + 1) if det >= 0 else (base, base + t + 1, base + t)
                part.tris.append(tri + (m,))
            ring = []
            poly += 1
        parts.append(part)
    return parts

if __name__ == '__main__':
    parts = scene(open(sys.argv[1], 'rb').read())
    total = 0
    for p in parts:
        lo = [min(q[i] for q in p.p) for i in range(3)]
        hi = [max(q[i] for q in p.p) for i in range(3)]
        used = sorted({t[3] for t in p.tris})
        names = [p.mats[i].name + ('*' if p.mats[i].picture else '+' if p.mats[i].file else '') if i < len(p.mats) else '?' for i in used]
        print('%-34s %7d  x %8.2f %8.2f  y %8.2f %8.2f  z %8.2f %8.2f  %s' % (p.name[:34], len(p.tris), lo[0], hi[0], lo[1], hi[1], lo[2], hi[2], ', '.join(names)[:90]))
        total += len(p.tris)
    print(len(parts), 'parts', total, 'triangles')
