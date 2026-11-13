#!/usr/bin/env python3
# The owner's ships, boats, shipyard and water pictures as city models. Called from city.py main(); kept apart because nothing else in the city needs it.
# Every file here is a Sketchfab export: scene.gltf with its buffer and pictures side by side. They are packed into a binary glTF in memory so city.py's own reader takes them.
import json, math, os, struct, subprocess, zlib
import port, reduce

CONTAINER_SCALE = 2.6     # Its boxes are 0.97 high and 4.97 long a bay: at this size a 2.6 m box in a 12.9 m bay, which makes a 237 m Panamax ship.
LIBERTY_LENGTH = 134.6    # A Liberty ship's length. The file is about a hundredth of a metre long.
ZODIAC_SCALE = 0.01       # In centimetres: 4.1 m.
YARD_SCALE = 0.55         # Its ship in the dock is 460 units long: at this size 253 m, a bulk carrier.
YARD_WATER = 1.5          # Where the water stood in the scan, in the file's units: the scan stops there on the piers.

def pack(folder, pictures):
    """scene.gltf and its bin, with the named pictures only, as a .glb on disk. Returns its path."""
    path = os.path.join(folder, 'packed.glb')
    if os.path.exists(path): return path
    j = json.load(open(os.path.join(folder, 'scene.gltf')))
    for m in j['meshes']: m['primitives'] = [p for p in m['primitives'] if p.get('mode', 4) == 4] # Some hold outlines as lines.
    b = bytearray(open(os.path.join(folder, j['buffers'][0]['uri']), 'rb').read())
    for im in j.get('images', []):
        uri = im.get('uri', '')
        if not any(p in uri for p in pictures): continue
        data = open(os.path.join(folder, uri), 'rb').read()
        while len(b) % 4: b.append(0)
        j['bufferViews'].append(dict(buffer=0, byteOffset=len(b), byteLength=len(data)))
        im['bufferView'] = len(j['bufferViews']) - 1
        del im['uri']
        b += data
    while len(b) % 4: b.append(0)
    j['buffers'] = [dict(byteLength=len(b))]
    t = json.dumps(j).encode()
    t += b' ' * (-len(t) % 4)
    with open(path, 'wb') as f:
        f.write(struct.pack('<4sII', b'glTF', 2, 12 + 8 + len(t) + 8 + len(b)))
        f.write(struct.pack('<I4s', len(t), b'JSON') + t)
        f.write(struct.pack('<I4s', len(b), b'BIN\0') + bytes(b))
    return path

def atlas_cluster(v, tris, target, patch=96):
    """city.cluster for a model on a picture atlas: corners only merge within one patch of the picture, or a merged corner would pull its triangles across unrelated parts of the atlas."""
    total = sum(len(t) for t in tris.values())
    if total <= target: return v, tris
    xs = [p[0] for p in v]; ys = [p[1] for p in v]; zs = [p[2] for p in v]
    span = max(max(xs) - min(xs), max(ys) - min(ys), max(zs) - min(zs))
    cells, best = 5.0, None
    for _ in range(24):
        c = span / cells
        out, slot, new, spot = [], {}, {}, {}
        for k, t in tris.items():
            for tri in t:
                r, at = [], []
                for i in tri:
                    p = v[i]
                    cell = (int(p[0] // c), int(p[1] // c), int(p[2] // c))
                    where = spot.setdefault(cell, p[:3]) # Every corner in a cell moves to one point, so patches of the picture stay joined.
                    key = (k, cell, int(p[6] * patch), int(p[7] * patch))
                    j = slot.get(key)
                    if j is None:
                        j = slot[key] = len(out)
                        out.append(where + p[3:])
                    r.append(j)
                    at.append(cell)
                if at[0] != at[1] and at[1] != at[2] and at[0] != at[2]: new.setdefault(k, []).append(tuple(r))
        n = sum(len(t) for t in new.values())
        if n > target and best is not None: break
        best = (out, new)
        if n > target: break
        cells *= 1.25
    return best

def paint_out(city, data, boxes):
    """A picture at 1024 with these boxes (fractions of its width and height) filled with the colour just outside them. Returns PNG bytes."""
    src, png = os.path.join(city.WORK, 'paint.src'), os.path.join(city.WORK, 'paint.png')
    with open(src, 'wb') as f: f.write(data)
    subprocess.run(['/usr/bin/sips', '-s', 'format', 'png', '-s', 'formatOptions', 'none', '-Z', '1024', src, '--out', png], check=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    b = open(png, 'rb').read()
    o, idat = 8, b''
    while o < len(b):
        n, t = struct.unpack_from('>I4s', b, o)
        d = b[o + 8:o + 8 + n]
        o += 12 + n
        if t == b'IHDR': w, h, depth, kind = struct.unpack('>IIBB', d[:10])
        elif t == b'IDAT': idat += d
    ch = {2: 3, 6: 4}[kind]
    raw, st, rows, prev, o = zlib.decompress(idat), w * ch, [], bytearray(w * ch), 0
    for y in range(h):
        f, r = raw[o], bytearray(raw[o + 1:o + 1 + st])
        o += 1 + st
        if f == 1:
            for i in range(ch, st): r[i] = (r[i] + r[i - ch]) & 255
        elif f == 2:
            for i in range(st): r[i] = (r[i] + prev[i]) & 255
        elif f == 3:
            for i in range(st): r[i] = (r[i] + (((r[i - ch] if i >= ch else 0) + prev[i]) >> 1)) & 255
        elif f == 4:
            for i in range(st):
                a, u, c = (r[i - ch] if i >= ch else 0), prev[i], (prev[i - ch] if i >= ch else 0)
                p = a + u - c
                pa, pb, pc = abs(p - a), abs(p - u), abs(p - c)
                r[i] = (r[i] + (a if pa <= pb and pa <= pc else u if pb <= pc else c)) & 255
        rows.append(r)
        prev = r
    for x0, y0, x1, y1 in boxes:
        ax, ay, bx, by = int(x0 * w), int(y0 * h), int(x1 * w), int(y1 * h)
        c = rows[max(0, ay - 3)][ax * ch:ax * ch + ch]
        for y in range(ay, by): rows[y][ax * ch:bx * ch] = bytes(c) * (bx - ax)
    def chunk(t, d): return struct.pack('>I', len(d)) + t + d + struct.pack('>I', zlib.crc32(t + d))
    return b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', w, h, 8, kind, 0, 0, 0)) + chunk(b'IDAT', zlib.compress(b''.join(b'\0' + bytes(r) for r in rows))) + chunk(b'IEND', b'')

LOGOS = [(0.0, 0.19, 0.212, 0.3), (0.64, 0.175, 0.85, 0.325), (0.912, 0.815, 0.95, 0.845), (0.822, 0.942, 0.862, 0.97)] # Two real shipping lines' marks on the boxes.
HULL_NAMES = [(0.41, 0.885, 0.60, 0.99), (0.0, 0.008, 0.06, 0.035), (0.43, 0.008, 0.48, 0.035), (0.59, 0.565, 0.63, 0.585)] # The line's letters along the hull and the ship's name at bow and stern.

def turned(v, quarter):
    """Corners turned about y by quarter turns, so a hull's bow comes to +z."""
    out = []
    for x, y, z, a, b, c, u, w in v:
        for _ in range(quarter % 4): x, z, a, c = -z, x, -c, a
        out.append((x, y, z, a, b, c, u, w))
    return out

def named(model, textures, own, prefix, keep=None):
    """A model's pictures under a prefix write() and CityModels recognise. keep names which source picture a prefix belongs to."""
    table = {}
    for name, data in own.items():
        p = prefix(data) if callable(prefix) else prefix
        table[name] = p + name[2:]
        textures[table[name]] = data
    model.lods = [(lv, [(table.get(t, t), ids) for t, ids in lt], c) for lv, lt, c in model.lods]
    return model

def joined(city, mesh, v, boxes):
    """A reduced mesh with the normals it now needs, and these triangles of v added as they came."""
    a, t = reduce.normals(*mesh)
    n = len(a)
    return city.compact(a + v, {**t, **{k: [tuple(i + n for i in q) for q in b] for k, b in boxes.items()}})

def drop_parts(v, tris, gone):
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
            x = root(at[a])
            up[root(at[b])] = x
            up[root(at[c])] = x
    box = {}
    for i, p in enumerate(v):
        b = box.setdefault(root(at[i]), [1e9, -1e9, 1e9, -1e9, 1e9, -1e9])
        for k in range(3): b[2 * k], b[2 * k + 1] = min(b[2 * k], p[k]), max(b[2 * k + 1], p[k])
    out = {r for r, b in box.items() if gone(b[2], b[3], max(b[1] - b[0], b[3] - b[2], b[5] - b[4]))}
    return {k: [q for q in t if root(at[q[0]]) not in out] for k, t in tris.items()}, len(out)

def models(city, textures):
    out = []
    # Container ship: the file's bow is +x.
    d = city.unpack('container_ship')
    v, tris, mats = city.merge(city.from_glb(d, pack(d, ['Hull_baseColor', 'Bridge_baseColor', 'Containers_baseColor'])))
    v, size = city.place(turned(v, 3), scale=CONTAINER_SCALE)
    boxes = {k: t for k, t in tris.items() if 'Containers' in k}
    for k in boxes: mats[k] = (paint_out(city, mats[k][0], LOGOS), mats[k][1])
    for k in mats:
        if 'Hull' in k: mats[k] = (paint_out(city, mats[k][0], HULL_NAMES), mats[k][1])
    # Reduced by edge collapse, not atlas_cluster: clustering tore the bridge and rippled the deck edge. The boxes are few and square, so they stay as they came.
    hull = {k: t for k, t in tris.items() if k not in boxes}
    near = joined(city, reduce.collapse(v, hull, 40000, reach=0.5, hold=12, slip=0.006, loose=True), v, boxes)
    far = joined(city, reduce.collapse(v, reduce.without_small(v, hull, 2.0)[0], 9000, reach=2.5, hold=2, slip=0.03, loose=True), v, boxes)
    own = {}
    m = city.finish('vessel_container', 'port', 'ship', near[0], near[1], mats, size, own, levels=[near, far])
    bridge = next(p for k, (p, c) in mats.items() if 'Bridge' in k)
    out.append(named(m, textures, own, lambda data: 'leaf_' if data == bridge else 'big_'))
    # Liberty ship: bow already +z.
    d = city.unpack('liberty_ship')
    v, tris, mats = city.merge(city.from_glb(d, pack(d, ['DefaultMaterial_baseColor'])))
    k = LIBERTY_LENGTH / (max(p[2] for p in v) - min(p[2] for p in v))
    v, size = city.place(v, scale=k)
    near = reduce.normals(*reduce.collapse(v, tris, 20000, reach=0.3, hold=12, slip=0.006, loose=True))
    far = reduce.normals(*reduce.collapse(v, reduce.without_small(v, tris, 2.0)[0], 5000, reach=1.5, hold=2, slip=0.03, loose=True))
    own = {}
    out.append(named(city.finish('vessel_liberty', 'port', 'ship', near[0], near[1], mats, size, own, levels=[near, far]), textures, own, 'big_'))
    # Shipyard scan: everything under its water goes, as it could never be seen. Its long side is x; the basin is on -z and the wide apron on +z.
    d = city.unpack('shipyard_dry_dock_cargo_ship_giant_vessel')
    v, tris, mats = city.merge(city.from_glb(d, pack(d, ['baseColor'])))
    tris = {k: [t for t in ts if max(v[i][1] for i in t) > YARD_WATER - 1] for k, ts in tris.items()}
    v, tris = city.compact(v, tris)
    v, size = city.place(v, scale=YARD_SCALE)
    out += port.models(city, v, tris, mats, textures, named, os.path.join(city.HERE, 'Tools/worldgen/port.json'))
    # Water: the file is a cube, so only its surface pictures are used. The emissive picture's top half is a photograph of sea; its base colour is cloud-like noise.
    d = city.unpack('small_flat_cube_of_water')
    sea = os.path.join(city.WORK, 'water_sea.png')
    subprocess.run(['/usr/bin/sips', '-s', 'format', 'png', '-c', '64', '256', '--cropOffset', '0', '0', city.find(d, 'material_0_emissive.jpeg'), '--out', sea], check=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    textures['water_sea'] = open(sea, 'rb').read()
    textures['water_noise'] = open(city.find(d, 'material_0_baseColor.png'), 'rb').read()
    return out
