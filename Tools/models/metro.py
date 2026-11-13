#!/usr/bin/env python3
# The owner's metro car as a city model (2026-11-06). Called from city.py main(). The file is a Blender scene: one mesh of 382,807 triangles, 304,350 of them the running gear, and no animation.
# Its cab is at -y. Only base colours are used, as the city shader takes no other maps. The one word on it, a real place's name on the destination board, is painted out.
import blend, maritime, reduce

LENGTH = 22.5  # A metro car's length: 2.97 m wide at this size, which is a metro car's width too.
PICTURES = {'Front': 'blinn6SG', 'Top': 'blinn4SG', 'wheel': 'blinn1SG', 'Side': 'blinn7SG', 'Bottom': 'blinn5SG'}
# 'Mirror' is the car's glass: its picture is flat grey for a metal shader the city does not have, where it read as grey card. Drawn as dark glass.
PLAIN = {'Metal': (0.42, 0.43, 0.45), 'Mirror.001': (0.1, 0.11, 0.12), 'aiStandardSurface21SG': (0.16, 0.16, 0.17), 'Mirror': (0.1, 0.13, 0.16)}
BOARD = [(0.69, 0.16, 0.74, 0.29)]  # On the Top picture.

def models(city, textures):
    d = city.unpack('metro-train-3d-free-model')
    b = blend.load(city.find(d, 'Metro Train.blend'))
    p = city.Piece('metro-train')
    for _, names, pos, tris in b.meshes():
        seen = {}
        for m, ring in tris:
            key = names[m]
            if key not in p.mats:
                if key in PICTURES:
                    data = open(city.find(d, PICTURES[key] + '_Base_color.png'), 'rb').read()
                    p.mats[key] = (maritime.paint_out(city, data, BOARD) if key == 'Top' else data, (1, 1, 1))
                else: p.mats[key] = (None, PLAIN[key])
            ids = []
            for i, u, w in ring:
                k = (i, round(u, 4), round(w, 4))
                if k not in seen:
                    seen[k] = len(p.v)
                    p.v.append(pos[i] + (0, 1, 0, u, w))
                ids.append(seen[k])
            p.tri(key, *ids)
    v, tris, mats = city.merge([p])
    k = LENGTH / (max(q[1] for q in v) - min(q[1] for q in v))
    v, size = city.place(v, up='z', scale=k)
    # Reduced by edge collapse: vertex clustering tore the cab's mask and frames (2026-11-06). The source has no normals, so they are made here, from the faces, with creases held.
    body = {key: t for key, t in tris.items() if 'wheel' not in key}
    gear = {key: t for key, t in tris.items() if 'wheel' in key}
    a = reduce.collapse(v, body, 24000, reach=0.03, slip=0.003)
    g = reduce.collapse(v, reduce.without_small(v, gear, 0.1)[0], 14000, reach=0.15, hold=3, slip=0.02, loose=True) # 304,350 triangles of bolts: the bogies keep their round wheels at this.
    near = reduce.normals(a[0] + g[0], {**a[1], **{key: [tuple(i + len(a[0]) for i in q) for q in t] for key, t in g[1].items()}})
    far = reduce.normals(*reduce.collapse(v, reduce.without_small(v, tris, 0.25)[0], 3000, reach=0.25, hold=3, slip=0.03, loose=True))
    return [city.finish('metro_car', 'infra', 'train', near[0], near[1], mats, size, textures, levels=[near, far])]
