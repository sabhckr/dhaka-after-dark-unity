# Where vehicles stand when nobody is driving them (2026-11-06). Behind the footpath, nose to the street, in small groups of real spaces; never on a carriageway, a footpath or a track, so traffic
# and people never meet them. Every space is tried against the same Site the buildings were placed with, and stands in it, so two things never share ground.
import math
from geo import *
from place import Box

# kind: (length, width), metres, with the space's margin. Lengths are the baked models'; a vehicle stands inside its box.
SIZES = {'Sedan': (4.6, 2.1), 'Microbus': (4.7, 2.1), 'Hatch': (4.4, 2.1), 'Saloon': (5.3, 2.2), 'Roadster': (4.4, 2.1), 'Sports': (4.9, 2.2), 'Motorcycle': (2.2, 0.95), 'Chopper': (2.6, 1.0), 'PoliceBike': (2.3, 1.0),
         'Bus': (10.6, 3.0), 'Coach': (11.3, 3.0), 'Truck': (7.4, 3.0), 'Tipper': (7.6, 3.1), 'Cng': (2.9, 1.7)}
# What stands where, by share, and how far apart groups stay along a street.
MIX = {
    'hub': [('Sedan', 5), ('Saloon', 3), ('Hatch', 3), ('Sports', 1), ('Roadster', 1), ('Motorcycle', 6), ('Cng', 3), ('Coach', 1), ('Bus', 1)],
    'rich': [('Saloon', 4), ('Sports', 2), ('Roadster', 2), ('Hatch', 3), ('Sedan', 3), ('Motorcycle', 2)],
    'crime': [('Sedan', 4), ('Microbus', 3), ('Motorcycle', 6), ('Chopper', 2), ('Cng', 3), ('Truck', 1)],
    'port': [('Truck', 6), ('Tipper', 4), ('Sedan', 1), ('Motorcycle', 2), ('Bus', 1)],
    'slum': [('Motorcycle', 5), ('Cng', 3), ('Sedan', 1)],
    None: [('Sedan', 2), ('Truck', 1), ('Motorcycle', 2), ('Bus', 1)],
}
GROUP = {'Bus': 2, 'Coach': 2, 'Truck': 3, 'Tipper': 3, 'Motorcycle': 6, 'Chopper': 3, 'PoliceBike': 3}
SPACING = {'trunk': 150, 'primary': 130, 'secondary': 110, 'tertiary': 90, 'residential': 75, 'living_street': 75, 'service': 75}

def walkway(cls): return 2.7 if cls in ('trunk', 'primary') else 2.2 if cls in ('secondary', 'tertiary') else 0.9

def spaces(placer, city, zone_at, rng, skip=('deck', 'ramp')):
    '''Every parked vehicle: dict(kind, x, z, turn). One pass along each street, both sides.'''
    out = []
    site = placer.site
    for w in city.ways:
        if w.kind in skip or w.cls not in SPACING or w.y is None: continue
        for side in (1, -1):
            run = rng.uniform(10, SPACING[w.cls])
            at = 0.0
            for i in range(len(w.pts) - 1):
                a, b = w.pts[i], w.pts[i + 1]
                l = dist(a, b)
                if l < 1e-6: continue
                d = ((b[0] - a[0]) / l, (b[1] - a[1]) / l)
                left = (-d[1], d[0])
                while at + run <= l + 0:
                    at += run
                    p = (a[0] + d[0] * at, a[1] + d[1] * at)
                    out_way = (left[0] * side, left[1] * side)
                    placed = group(placer, w, p, d, out_way, zone_at, rng, out)
                    run = SPACING[w.cls] * rng.uniform(0.8, 1.5) + placed
                at -= l
                if at < 0: run += at; at = 0.0
    return out

def group(placer, w, p, d, away, zone_at, rng, out):
    '''A few spaces side by side behind the footpath at p. Returns how far along the street it took.'''
    zone = zone_at(p)
    mix = MIX.get(zone, MIX[None])
    kind = rng.choices([k for k, _ in mix], [n for _, n in mix])[0]
    length, width = SIZES[kind]
    n = rng.randint(1, GROUP.get(kind, 4))
    back = w.w / 2 + walkway(w.cls) + 0.4 + length / 2
    took = 0.0
    for k in range(n):
        along = k * (width + 0.25)
        c = (p[0] + d[0] * along + away[0] * back, p[1] + d[1] * along + away[1] * back)
        # t runs along the vehicle, nose to the street; the box is half its width across.
        b = Box(c, (-away[0], -away[1]), length / 2, width / 2)
        if zone_at(c) != zone or not all(inside(q, placer.city.land) for q in b.corners()): break
        if not placer.site.road_clear(b) or not placer.site.free(b, 0.3): break
        placer.site.stand(b)
        out.append(dict(kind=kind, x=c[0], z=c[1], turn=math.degrees(math.atan2(-away[0], -away[1])) % 360))
        took = along + width
    return took
