#!/usr/bin/env python3
# Reads meshes out of a .blend (Blender 2.8 to 3.x, little-endian, 64-bit), with no Blender: the file's own SDNA says where every field sits. Compressed files go through zstd first.
import os, struct, subprocess, tempfile
class Blend:
    def __init__(s, data):
        assert data[:7] == b'BLENDER' and data[7:9] == b'-v', 'not an uncompressed 64-bit little-endian blend'
        s.d = data
        s.version = data[9:12]
        s.blocks, s.at = [], {}
        o = 12
        while True:
            code = data[o:o + 4]
            size, old, sdna, count = struct.unpack_from('<iQii', data, o + 4)
            b = dict(code=code.rstrip(b'\0').decode('latin1'), size=size, old=old, sdna=sdna, count=count, off=o + 24)
            o += 24 + size
            if b['code'] == 'ENDB': break
            s.blocks.append(b)
            s.at[old] = b
            if b['code'] == 'DNA1': s.dna(b)
    def dna(s, b):
        d, o = s.d, b['off'] + 4
        base = b['off']
        al = lambda o: base + ((o - base + 3) & ~3)
        def names(o, tag):
            assert d[o:o + 4] == tag, tag
            n = struct.unpack_from('<i', d, o + 4)[0]; o += 8; out = []
            for _ in range(n):
                e = d.index(b'\0', o); out.append(d[o:e].decode('latin1')); o = e + 1
            return out, o
        s.names, o = names(o, b'NAME')
        o = al(o)
        s.types, o = names(o, b'TYPE')
        o = al(o)
        assert d[o:o + 4] == b'TLEN'; o += 4
        s.lens = list(struct.unpack_from('<%dH' % len(s.types), d, o)); o += 2 * len(s.types)
        o = al(o)
        assert d[o:o + 4] == b'STRC'
        n = struct.unpack_from('<i', d, o + 4)[0]; o += 8
        s.structs = []
        s.byname = {}
        for i in range(n):
            t, c = struct.unpack_from('<HH', d, o); o += 4
            fields = {}
            off = 0
            for _ in range(c):
                ft, fn = struct.unpack_from('<HH', d, o); o += 4
                name = s.names[fn]
                arr = 1
                base = name
                if '[' in name:
                    base = name[:name.index('[')]
                    for part in name[name.index('['):].strip('[]').split(']['): arr *= int(part)
                size = 8 if name.startswith('*') or name.startswith('(') else s.lens[ft]
                fields[base.lstrip('*').split(')')[0].lstrip('(*')] = (off, s.types[ft], name, arr, size)
                off += size * arr
            s.structs.append((s.types[t], fields, s.lens[t]))
            s.byname[s.types[t]] = i
    def field(s, st, base, name):
        off, t, full, arr, size = s.structs[s.byname[st]][1][name]
        o = base + off
        if full.startswith('*'): return struct.unpack_from('<Q', s.d, o)[0]
        fmt = {'int': 'i', 'float': 'f', 'short': 'h', 'char': 'b', 'uchar': 'B'}.get(t)
        if t == 'char' and arr > 1: return s.d[o:o + arr].split(b'\0')[0].decode('latin1')
        if fmt is None: return o
        v = struct.unpack_from('<%d%s' % (arr, fmt), s.d, o)
        return v[0] if arr == 1 else v
    def items(s, code):
        return [b for b in s.blocks if b['code'] == code]
    def name(s, off): return s.field('ID', off, 'name')[2:]
    def array(s, ptr, st):
        b = s.at[ptr]
        return b['off'], b['count'], s.structs[s.byname[st]][2]
    def meshes(s):
        """Every mesh: (name, material names, corners [(x, y, z)], triangles [(material, ((corner, u, v) * 3))])."""
        M = s.structs[s.byname['Mesh']][1]
        CL = s.structs[s.byname['CustomDataLayer']][2]
        out = []
        for x in s.items('ME'):
            off = x['off']
            vo, vn, vs = s.array(s.field('Mesh', off, 'mvert'), 'MVert')
            co = s.structs[s.byname['MVert']][1]['co'][0]
            pos = [struct.unpack_from('<3f', s.d, vo + i * vs + co) for i in range(vn)]
            po, pn, ps = s.array(s.field('Mesh', off, 'mpoly'), 'MPoly')
            lo, ln, ls = s.array(s.field('Mesh', off, 'mloop'), 'MLoop')
            ld = off + M['ldata'][0]
            layers = s.at[s.field('CustomData', ld, 'layers')]['off']
            uv = None
            for i in range(s.field('CustomData', ld, 'totlayer')):
                o = layers + i * CL
                if s.field('CustomDataLayer', o, 'type') == 16 and uv is None: uv = s.field('CustomDataLayer', o, 'data')
            uo, un, us = s.array(uv, 'MLoopUV')
            mats = []
            m = s.at.get(s.field('Mesh', off, 'mat'))
            if m:
                for p in struct.unpack_from('<%dQ' % (m['size'] // 8), s.d, m['off']):
                    if p in s.at: mats.append(s.name(s.at[p]['off']))
            tris = []
            for i in range(pn):
                o = po + i * ps
                start, total, mat = s.field('MPoly', o, 'loopstart'), s.field('MPoly', o, 'totloop'), s.field('MPoly', o, 'mat_nr')
                ring = []
                for l in range(start, start + total):
                    u, v = struct.unpack_from('<2f', s.d, uo + l * us)
                    ring.append((s.field('MLoop', lo + l * ls, 'v'), u, v))
                for k in range(1, total - 1): tris.append((mat, (ring[0], ring[k], ring[k + 1])))
            out.append((s.name(off), mats, pos, tris))
        return out

def load(path):
    data = open(path, 'rb').read()
    if data[:4] == b'\x28\xb5\x2f\xfd':
        with tempfile.TemporaryDirectory() as d:
            q = os.path.join(d, 'x.blend')
            subprocess.run(['/opt/homebrew/bin/zstd', '-d', '-q', path, '-o', q], check=True)
            data = open(q, 'rb').read()
    return Blend(data)
