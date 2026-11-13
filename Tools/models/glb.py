#!/usr/bin/env python3
# Reads a binary glTF (.glb) into plain lists, node transforms applied. Standard library only.
#   python3 -I Tools/models/glb.py <file.glb>   prints what is in it
import json, math, struct, sys

COMP = {5120: ('b', 1), 5121: ('B', 1), 5122: ('h', 2), 5123: ('H', 2), 5125: ('I', 4), 5126: ('f', 4)}
NUM = {'SCALAR': 1, 'VEC2': 2, 'VEC3': 3, 'VEC4': 4, 'MAT4': 16}

def load(data):
    assert data[:4] == b'glTF', 'not a glb'
    o, j, b = 12, None, b''
    while o < len(data):
        n, k = struct.unpack_from('<I4s', data, o)
        if k == b'JSON': j = json.loads(data[o + 8:o + 8 + n])
        elif k[:3] == b'BIN': b = data[o + 8:o + 8 + n]
        o += 8 + n
    return j, b

def accessor(j, b, i):
    a = j['accessors'][i]
    v = j['bufferViews'][a['bufferView']]
    f, size = COMP[a['componentType']]
    n = NUM[a['type']]
    start = v.get('byteOffset', 0) + a.get('byteOffset', 0)
    stride = v.get('byteStride', 0) or size * n
    c = a['count']
    if stride == size * n:
        flat = struct.unpack_from('<%d%s' % (c * n, f), b, start)
    else:
        flat = []
        for k in range(c): flat.extend(struct.unpack_from('<%d%s' % (n, f), b, start + k * stride))
    return flat if n == 1 else [flat[k * n:(k + 1) * n] for k in range(c)]

def matmul(a, b):
    return [sum(a[r + 4 * k] * b[k + 4 * c] for k in range(4)) for c in range(4) for r in range(4)]

def trs(n):
    if 'matrix' in n: return list(n['matrix'])
    t = n.get('translation', (0, 0, 0))
    x, y, z, w = n.get('rotation', (0, 0, 0, 1))
    s = n.get('scale', (1, 1, 1))
    r = [1 - 2 * (y * y + z * z), 2 * (x * y + z * w), 2 * (x * z - y * w), 0,
         2 * (x * y - z * w), 1 - 2 * (x * x + z * z), 2 * (y * z + x * w), 0,
         2 * (x * z + y * w), 2 * (y * z - x * w), 1 - 2 * (x * x + y * y), 0]
    return [r[0] * s[0], r[1] * s[0], r[2] * s[0], 0, r[4] * s[1], r[5] * s[1], r[6] * s[1], 0, r[8] * s[2], r[9] * s[2], r[10] * s[2], 0, t[0], t[1], t[2], 1]

def point(m, p): return (m[0] * p[0] + m[4] * p[1] + m[8] * p[2] + m[12], m[1] * p[0] + m[5] * p[1] + m[9] * p[2] + m[13], m[2] * p[0] + m[6] * p[1] + m[10] * p[2] + m[14])
def vector(m, p): return (m[0] * p[0] + m[4] * p[1] + m[8] * p[2], m[1] * p[0] + m[5] * p[1] + m[9] * p[2], m[2] * p[0] + m[6] * p[1] + m[10] * p[2])

def parts(data):
    """Every drawn primitive: dict(name, material, pos, nrm, uv, tri). World space of the file, y up."""
    j, b = load(data)
    out = []
    ident = [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1]
    def walk(i, m):
        n = j['nodes'][i]
        w = matmul(m, trs(n))
        if 'mesh' in n:
            for pr in j['meshes'][n['mesh']]['primitives']:
                at = pr['attributes']
                pos = [point(w, p) for p in accessor(j, b, at['POSITION'])]
                nrm = [vector(w, p) for p in accessor(j, b, at['NORMAL'])] if 'NORMAL' in at else None
                uv = accessor(j, b, at['TEXCOORD_0']) if 'TEXCOORD_0' in at else None
                tri = accessor(j, b, pr['indices']) if 'indices' in pr else list(range(len(pos)))
                out.append(dict(name=n.get('name', ''), material=pr.get('material'), pos=pos, nrm=nrm, uv=uv, tri=tri))
        for c in n.get('children', ()): walk(c, w)
    for i in j['scenes'][j.get('scene', 0)]['nodes']: walk(i, ident)
    return j, b, out

def image(j, b, i):
    """The bytes of embedded image i."""
    im = j['images'][i]
    if 'bufferView' not in im: return None
    v = j['bufferViews'][im['bufferView']]
    return b[v.get('byteOffset', 0):v.get('byteOffset', 0) + v['byteLength']]

if __name__ == '__main__':
    j, b, ps = parts(open(sys.argv[1], 'rb').read())
    print('nodes', len(j['nodes']), 'meshes', len(j.get('meshes', ())), 'materials', len(j.get('materials', ())), 'images', len(j.get('images', ())), 'parts', len(ps))
    for m, mat in enumerate(j.get('materials', ())):
        pbr = mat.get('pbrMetallicRoughness', {})
        t = pbr.get('baseColorTexture', {}).get('index')
        im = j['textures'][t].get('source') if t is not None else None
        print('  mat', m, mat.get('name'), 'colour', [round(c, 2) for c in pbr.get('baseColorFactor', (1, 1, 1, 1))], 'image', im, j['images'][im].get('mimeType') if im is not None else '', len(image(j, b, im) or b'') if im is not None else '')
    groups = {}
    for p in ps:
        xs = [v[0] for v in p['pos']]; ys = [v[1] for v in p['pos']]; zs = [v[2] for v in p['pos']]
        print('  %-34s mat %-3s tris %6d  x %8.1f %8.1f  y %7.1f %7.1f  z %8.1f %8.1f' % (p['name'][:34], p['material'], len(p['tri']) // 3, min(xs), max(xs), min(ys), max(ys), min(zs), max(zs)))
