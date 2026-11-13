#!/usr/bin/env python3
# DADC chunk files in Python: the writer for the generated city, and a reader to check it and to look at old chunks. The specification is the field table in
# dhaka-after-dark/scripts/unity/dadc.ts, and Assets/_Project/Runtime/World/Dadc.cs is the reader the game uses; this mirrors both, field for field.
# Positions here are metres in the Unity frame (x east, z north). The file holds integer centimetres from the chunk's south-west corner.
CHUNK_CM = 50000

ROAD_OPT = ('ln', 'sw', 'sf', 'lit', 'mx', 'nm', 'nme', 'ly', 'tn', 'ys')
BUILDING_OPT = ('holes', 't', 'lv', 'mlv', 'mh', 'mat', 'roof', 'ly', 'nm', 'mdl', 'pose')
FORMAT = 2

class Writer:
    def __init__(s, ix, iy):
        s.ix, s.iy = ix, iy
        s.e0, s.n0 = ix * CHUNK_CM, -(iy + 1) * CHUNK_CM
        s.b = bytearray()
        s.strings, s.index = [], {}
        s.sum_e = s.sum_n = s.positions = s.sum_ints = 0
    def uvar(s, n):
        assert n >= 0
        while n >= 128:
            s.b.append((n & 127) | 128)
            n >>= 7
        s.b.append(n)
    def svar(s, n): s.uvar(n * 2 if n >= 0 else -n * 2 - 1)
    def cm(s, v): s.svar(int(round(v * 100)))
    def half(s, v): s.svar(int(round(v * 2)))
    def int(s, n):
        s.svar(n)
        s.sum_ints += n
    def str(s, t):
        i = s.index.get(t)
        if i is None:
            i = s.index[t] = len(s.strings)
            s.strings.append(t)
        s.uvar(i)
    def bool(s, v): s.b.append(1 if v else 0)
    def xy(s, x, z):
        e, n = int(round(x * 100)), int(round(z * 100))
        s.svar(e - s.e0)
        s.svar(n - s.n0)
        s.counted(e - s.e0, n - s.n0)
    def counted(s, e, n):
        s.positions += 1
        s.sum_e += e
        s.sum_n += n
    def pts(s, p):
        s.uvar(len(p))
        pe, pn = s.e0, s.n0
        for x, z in p:
            e, n = int(round(x * 100)), int(round(z * 100))
            s.svar(e - pe)
            s.svar(n - pn)
            pe, pn = e, n
            s.counted(e - s.e0, n - s.n0)
    def rings(s, rings):
        s.uvar(len(rings))
        for r in rings: s.pts(r)
    def mask(s, rec, names):
        m = 0
        for i, k in enumerate(names):
            if rec.get(k) is not None: m |= 1 << i
        s.uvar(m)

def encode(c):
    """A chunk as a dict: ix, iy and any of the layers below. Returns (bytes, checks)."""
    w = Writer(c['ix'], c['iy'])
    def layer(name, fn):
        rows = c.get(name, [])
        w.uvar(len(rows))
        for r in rows: fn(r)
    def road(r):
        w.int(r['id']); w.str(r['cls']); w.cm(r['w']); w.bool(r.get('we', False)); w.cm(r.get('y', 0)); w.bool(r.get('bridge', False)); w.pts(r['pts'])
        w.mask(r, ROAD_OPT)
        if r.get('ln') is not None: w.int(r['ln'])
        if r.get('sw') is not None: w.str(r['sw'])
        if r.get('sf') is not None: w.str(r['sf'])
        if r.get('lit') is not None: w.int(r['lit'])
        if r.get('mx') is not None: w.cm(r['mx'])
        if r.get('nm') is not None: w.str(r['nm'])
        if r.get('nme') is not None: w.str(r['nme'])
        if r.get('ly') is not None: w.int(r['ly'])
        if r.get('tn') is not None: w.int(r['tn'])
        if r.get('ys') is not None:
            assert len(r['ys']) == len(r['pts'])
            w.uvar(len(r['ys']))
            for y in r['ys']: w.cm(y)
    def building(r):
        w.int(r['id']); w.cm(r['h']); w.str(r.get('hs', 'generated')); w.pts(r['outer'])
        w.mask(r, BUILDING_OPT)
        if r.get('holes') is not None: w.rings(r['holes'])
        if r.get('t') is not None: w.str(r['t'])
        if r.get('lv') is not None: w.half(r['lv'])
        if r.get('mlv') is not None: w.half(r['mlv'])
        if r.get('mh') is not None: w.cm(r['mh'])
        if r.get('mat') is not None: w.str(r['mat'])
        if r.get('roof') is not None: w.str(r['roof'])
        if r.get('ly') is not None: w.int(r['ly'])
        if r.get('nm') is not None: w.str(r['nm'])
        if r.get('mdl') is not None: w.str(r['mdl'])
        if r.get('pose') is not None:
            w.uvar(len(r['pose']))
            for v in r['pose']: w.svar(v)
    def poly(r):
        w.int(r['id']); w.pts(r['outer'])
        w.mask(r, ('holes',))
        if r.get('holes') is not None: w.rings(r['holes'])
    def canal(r):
        w.int(r['id']); w.cm(r['w']); w.pts(r['pts']); w.uvar(0)
    def rail(r):
        w.int(r['id']); w.cm(r.get('y', 0)); w.pts(r['pts']); w.uvar(0)
    def tree(r):
        w.xy(r['x'], r['z']); w.cm(r['h']); w.str(r['k']); w.uvar(0)
    def landmark(r):
        w.int(r['id']); w.str(r['name']); w.str(r['cat']); w.xy(r['x'], r['z']); w.uvar(0)
    nodes = c.get('nodes', [])
    index = {n['key']: i for i, n in enumerate(nodes)}
    def node(r):
        w.xy(r['x'], r['z'])
        m = (1 if r.get('sig') is not None else 0) | (2 if r.get('cr') is not None else 0)
        w.uvar(m)
        if r.get('sig') is not None: w.int(r['sig'])
        if r.get('cr') is not None: w.int(r['cr'])
    def edge(r):
        w.int(r['seq']); w.uvar(index[r['a']]); w.uvar(index[r['b']]); w.int(r['way']); w.str(r['cls']); w.int(r['oneway']); w.pts(r['pts']); w.uvar(0)
    def point(r):
        w.int(r['id']); w.str(r['k']); w.xy(r['x'], r['z'])
        m = (1 if r.get('ct') is not None else 0) | (2 if r.get('nm') is not None else 0)
        w.uvar(m)
        if r.get('ct') is not None: w.str(r['ct'])
        if r.get('nm') is not None: w.str(r['nm'])
    def landuse(r):
        w.int(r['id']); w.str(r['t']); w.pts(r['outer'])
        w.mask(r, ('holes',))
        if r.get('holes') is not None: w.rings(r['holes'])
    def barrier(r):
        w.int(r['id']); w.str(r['t']); w.pts(r['pts']); w.uvar(0)
    def power(r):
        w.int(r['id']); w.str(r['t']); w.pts(r['pts'])
        w.mask(r, ('kv',))
        if r.get('kv') is not None: w.cm(r['kv'])
    layer('roads', road); layer('buildings', building); layer('water', poly); layer('canals', canal); layer('parks', poly); layer('rails', rail)
    layer('plazas', poly); layer('bridges', poly); layer('trees', tree); layer('landmarks', landmark); layer('nodes', node); layer('edges', edge)
    layer('points', point); layer('landuse', landuse); layer('barriers', barrier); layer('power', power)
    head = Writer(c['ix'], c['iy'])
    head.b += b'DADC'
    head.b.append(FORMAT)
    head.b.append(c.get('schema', 2))
    head.svar(c['ix']); head.svar(c['iy'])
    head.uvar(len(w.strings))
    for t in w.strings:
        d = t.encode('utf-8')
        head.uvar(len(d))
        head.b += d
    return bytes(head.b + w.b), {'positions': w.positions, 'sumEastCm': w.sum_e, 'sumNorthCm': w.sum_n, 'sumInts': w.sum_ints}

class Reader:
    def __init__(s, b):
        s.b, s.p = b, 0
    def u8(s):
        v = s.b[s.p]
        s.p += 1
        return v
    def uvar(s):
        n = sh = 0
        while True:
            v = s.u8()
            n |= (v & 127) << sh
            sh += 7
            if v < 128: return n
    def svar(s):
        n = s.uvar()
        return n // 2 if n % 2 == 0 else -(n + 1) // 2
    def pts(s):
        out, e, n = [], s.e0, s.n0
        for _ in range(s.uvar()):
            e += s.svar()
            n += s.svar()
            out.append((e / 100.0, n / 100.0))
        return out
    def xy(s): return ((s.svar() + s.e0) / 100.0, (s.svar() + s.n0) / 100.0)
    def rings(s): return [s.pts() for _ in range(s.uvar())]
    def st(s): return s.strings[s.uvar()]

def decode(data):
    r = Reader(data)
    assert data[:4] == b'DADC'
    r.p = 4
    fmt, schema = r.u8(), r.u8()
    ix, iy = r.svar(), r.svar()
    r.e0, r.n0 = ix * CHUNK_CM, -(iy + 1) * CHUNK_CM
    strings = []
    for _ in range(r.uvar()):
        n = r.uvar()
        strings.append(bytes(r.b[r.p:r.p + n]).decode('utf-8'))
        r.p += n
    r.strings = strings
    c = {'format': fmt, 'ix': ix, 'iy': iy}
    def layer(name, fn): c[name] = [fn() for _ in range(r.uvar())]
    def road():
        x = {'id': r.svar(), 'cls': r.st(), 'w': r.svar() / 100, 'we': bool(r.u8()), 'y': r.svar() / 100, 'bridge': bool(r.u8()), 'pts': r.pts()}
        m = r.uvar()
        if m & 1: x['ln'] = r.svar()
        if m & 2: x['sw'] = r.st()
        if m & 4: x['sf'] = r.st()
        if m & 8: x['lit'] = r.svar()
        if m & 16: x['mx'] = r.svar() / 100
        if m & 32: x['nm'] = r.st()
        if m & 64: x['nme'] = r.st()
        if m & 128: x['ly'] = r.svar()
        if m & 256: x['tn'] = r.svar()
        if m & 512: x['ys'] = [r.svar() / 100 for _ in range(r.uvar())]
        return x
    def building():
        x = {'id': r.svar(), 'h': r.svar() / 100, 'hs': r.st(), 'outer': r.pts()}
        m = r.uvar()
        if m & 1: x['holes'] = r.rings()
        if m & 2: x['t'] = r.st()
        if m & 4: x['lv'] = r.svar() / 2
        if m & 8: x['mlv'] = r.svar() / 2
        if m & 16: x['mh'] = r.svar() / 100
        if m & 32: x['mat'] = r.st()
        if m & 64: x['roof'] = r.st()
        if m & 128: x['ly'] = r.svar()
        if m & 256: x['nm'] = r.st()
        if m & 512: x['mdl'] = r.st()
        if m & 1024: x['pose'] = [r.svar() for _ in range(r.uvar())]
        return x
    def poly():
        x = {'id': r.svar(), 'outer': r.pts()}
        if r.uvar() & 1: x['holes'] = r.rings()
        return x
    def canal():
        x = {'id': r.svar(), 'w': r.svar() / 100, 'pts': r.pts()}; r.uvar(); return x
    def rail():
        x = {'id': r.svar(), 'y': r.svar() / 100, 'pts': r.pts()}; r.uvar(); return x
    def tree():
        p = r.xy(); x = {'x': p[0], 'z': p[1], 'h': r.svar() / 100, 'k': r.st()}; r.uvar(); return x
    def landmark():
        x = {'id': r.svar(), 'name': r.st(), 'cat': r.st()}; p = r.xy(); x['x'], x['z'] = p; r.uvar(); return x
    def node():
        p = r.xy(); x = {'x': p[0], 'z': p[1]}; m = r.uvar()
        if m & 1: x['sig'] = r.svar()
        if m & 2: x['cr'] = r.svar()
        return x
    def edge():
        x = {'seq': r.svar(), 'a': r.uvar(), 'b': r.uvar(), 'way': r.svar(), 'cls': r.st(), 'oneway': r.svar(), 'pts': r.pts()}; r.uvar(); return x
    def point():
        x = {'id': r.svar(), 'k': r.st()}; p = r.xy(); x['x'], x['z'] = p; m = r.uvar()
        if m & 1: x['ct'] = r.st()
        if m & 2: x['nm'] = r.st()
        return x
    def landuse():
        x = {'id': r.svar(), 't': r.st(), 'outer': r.pts()}
        if r.uvar() & 1: x['holes'] = r.rings()
        return x
    def barrier():
        x = {'id': r.svar(), 't': r.st(), 'pts': r.pts()}; r.uvar(); return x
    def power():
        x = {'id': r.svar(), 't': r.st(), 'pts': r.pts()}
        if r.uvar() & 1: x['kv'] = r.svar() / 100
        return x
    layer('roads', road); layer('buildings', building); layer('water', poly); layer('canals', canal); layer('parks', poly); layer('rails', rail)
    layer('plazas', poly); layer('bridges', poly); layer('trees', tree); layer('landmarks', landmark); layer('nodes', node); layer('edges', edge)
    layer('points', point); layer('landuse', landuse); layer('barriers', barrier); layer('power', power)
    assert r.p == len(data), 'trailing bytes in chunk'
    return c
