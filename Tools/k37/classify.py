"""K-37: classify every viaduct pier from piers.json (written by K37.cs). Read-only; writes k37.json."""
import json, math, sys

R = 0.9                    # ChunkRoads.PierRadius
LANE_LO, LANE_HI = 3.0, 3.5  # assumed lane width range (not checked against a Bangladeshi standard)
SLIDE = 12.0               # how far along the deck a pier may slide in the search, each way
MAJOR = {"motorway", "trunk", "primary", "secondary", "tertiary", "trunk_link", "primary_link", "secondary_link", "tertiary_link"}

piers = json.load(open(sys.argv[1]))

def near(pts, x, z):
    best = (1e18, 0, 0.0, 0.0, 0.0, 0.0)
    acc = 0.0
    for i in range(0, len(pts) - 3, 2):
        ax, az, bx, bz = pts[i:i + 4]
        ex, ez = bx - ax, bz - az
        ll = ex * ex + ez * ez
        if ll < 1e-12:
            continue
        L = math.sqrt(ll)
        t = max(0.0, min(1.0, ((x - ax) * ex + (z - az) * ez) / ll))
        qx, qz = ax + ex * t, az + ez * t
        d = math.hypot(qx - x, qz - z)
        if d < best[0]:
            best = (d, i // 2, acc + t * L, ex / L, ez / L, qx, qz)[:6] + ((qx, qz),) if False else (d, i // 2, acc + t * L, ex / L, ez / L, (qx, qz))
        acc += L
    return best  # d, seg, along, ux, uz, (qx, qz)

def in_poly(flat, x, z):
    ins, n = False, len(flat) // 2
    j = n - 1
    for i in range(n):
        xi, zi, xj, zj = flat[2 * i], flat[2 * i + 1], flat[2 * j], flat[2 * j + 1]
        if (zi > z) != (zj > z) and x < (xj - xi) * (z - zi) / (zj - zi) + xi:
            ins = not ins
        j = i
    return ins

def lanes_half(r, lane):
    return r["lanes"] * lane / 2 if r["lanes"] else None

out = []
for p in piers:
    x, z = p["x"], p["z"]
    trk = min(p["tracks"], key=lambda t: t["d"])
    ux, uz = trk["ux"], trk["uz"]
    nx, nz = -uz, ux
    along = near(trk["pts"], x, z)[2]
    roads = []
    for r in p["roads"]:
        if r["y"] > 1:
            continue
        d, seg, _, rux, ruz, (qx, qz) = near(r["pts"], x, z)
        half = max(0.4, r["width"] / 2)
        if d > half + 25:
            continue
        par = abs(rux * ux + ruz * uz) > 0.8
        roads.append(dict(id=r["id"], cls=r["cls"], name=r["nameEn"] or r["name"], nameBn=r["name"], width=r["width"], est=r["est"],
                          lanes=r["lanes"], oneway=r["oneway"], foot=r["foot"], d=round(d, 2), seg=seg,
                          off=round((qx - x) * nx + (qz - z) * nz, 2), parallel=par, half=half,
                          overlap=round(half + R - d, 2), pts=r["pts"]))
    conflicts = [r for r in roads if not r["foot"] and r["overlap"] > 0]
    bld_in = [b for b in p["buildings"] if b["inside"]]
    stations = [b for b in bld_in if b["type"] == "train_station" and b["base"] > 2]
    ground_bld = [b for b in bld_in if not (b["type"] == "train_station" and b["base"] > 2)]

    # Width evidence per conflicting road: does the pier clear it at lane-based widths?
    for r in conflicts:
        r["clear_lanes_hi"] = r["lanes"] is not None and lanes_half(r, LANE_HI) + R <= r["d"]
        r["clear_lanes_lo"] = r["lanes"] is not None and lanes_half(r, LANE_LO) + R <= r["d"]
        r["need_half"] = round(r["d"] - R, 2)  # the widest half width that clears the pier
        r["need_lane"] = round(2 * (r["d"] - R) / r["lanes"], 2) if r["lanes"] else None

    # Twin check: one-way major roads parallel to the deck on both sides of the pier.
    par_major = [r for r in roads if r["parallel"] and r["cls"] in MAJOR and r["oneway"] != 0 and not r["foot"]]
    left = [r for r in par_major if r["off"] > 0]
    right = [r for r in par_major if r["off"] < 0]
    twin = None
    if left and right:
        a = min(left, key=lambda r: r["off"])
        b = max(right, key=lambda r: r["off"])
        spacing = a["off"] - b["off"]
        twin = dict(a=a["id"], b=b["id"], spacing=round(spacing, 2), est_sum=a["half"] + b["half"],
                    lanes_lo_sum=(lanes_half(a, LANE_LO) + lanes_half(b, LANE_LO)) if a["lanes"] and b["lanes"] else None,
                    median_at_lanes_lo=round(spacing - lanes_half(a, LANE_LO) - lanes_half(b, LANE_LO), 2) if a["lanes"] and b["lanes"] else None)

    # Slide search along the deck: smallest |s| that clears all at-grade non-foot roads and ground buildings.
    def clear_at(s, mode):
        px, pz = x + ux * s, z + uz * s
        for r in roads:
            if r["foot"]:
                continue
            h = r["half"]
            if mode != "est" and r["lanes"]:
                h = lanes_half(r, LANE_HI if mode == "hi" else LANE_LO)
            if near(r["pts"], px, pz)[0] < h + R:
                return False
        for b in p["buildings"]:
            if b["type"] == "train_station" and b["base"] > 2:
                continue
            if in_poly(b["outer"], px, pz):
                return False
        return True
    slide = {}
    for mode in ("est", "hi", "lo"):
        best = None
        k = 0
        while k * 0.25 <= SLIDE:
            for s in ((k * 0.25,) if k == 0 else (k * 0.25, -k * 0.25)):
                if clear_at(s, mode):
                    best = s
                    break
            if best is not None:
                break
            k += 1
        slide[mode] = best

    def clear_lat(o, mode):
        px, pz = x + nx * o, z + nz * o
        for r in roads:
            if r["foot"]:
                continue
            h = r["half"]
            if mode != "est" and r["lanes"]:
                h = lanes_half(r, LANE_HI if mode == "hi" else LANE_LO)
            if near(r["pts"], px, pz)[0] < h + R:
                return False
        for b in p["buildings"]:
            if not (b["type"] == "train_station" and b["base"] > 2) and in_poly(b["outer"], px, pz):
                return False
        return True
    lateral = {}
    for mode in ("est", "hi", "lo"):
        best = None
        for k in range(0, 49):
            for o in ((0.0,) if k == 0 else (k * 0.25, -k * 0.25)):
                if clear_lat(o, mode):
                    best = o
                    break
            if best is not None:
                break
        lateral[mode] = best
    nearby_b = sorted([dict(id=b["id"], type=b["type"], name=b["name"], inside=b["inside"], edge=round(b["edge"], 2), h=b["h"], base=b["base"])
                       for b in p["buildings"] if b["inside"] or b["edge"] < 10], key=lambda b: (not b["inside"], b["edge"]))
    rec = dict(lateral=lateral, nearby_buildings=nearby_b, chunk=p["chunk"], e=round(p["e"], 2), n=round(p["n"], 2), x=x, z=z, cx=p["cx"], cz=p["cz"], skipped=p["skipped"], why=p["why"],
               track=trk["id"], track_seg=trk["seg"], track_d=round(trk["d"], 2), along=round(along, 1), ux=ux, uz=uz,
               tracks=[dict(id=t["id"], d=round(t["d"], 2), seg=t["seg"]) for t in p["tracks"]],
               conflicts=[{k: v for k, v in r.items() if k != "pts"} for r in conflicts],
               roads=[{k: v for k, v in r.items() if k != "pts"} for r in roads if abs(r["off"]) < 25 or r in conflicts],
               twin=twin, slide=slide,
               stations=[dict(id=b["id"], name=b["name"], base=b["base"], top=b["top"], edge=round(b["edge"], 2)) for b in stations],
               ground_buildings=[dict(id=b["id"], type=b["type"], name=b["name"], h=b["h"], hs=b["hs"], edge=round(b["edge"], 2)) for b in ground_bld])

    # Classification.
    causes = []
    par_c = [r for r in conflicts if r["parallel"]]
    cross_c = [r for r in conflicts if not r["parallel"]]
    if p["skipped"]:
        cls = "J"  # junction place, spanned (no pier built)
    elif not conflicts and not bld_in:
        cls = "C"  # clear
    else:
        for r in par_c:
            if r["clear_lanes_hi"]:
                causes.append(("W", r["id"], "clears at %d lanes x %.1f m" % (r["lanes"], LANE_HI)))
            elif r["clear_lanes_lo"]:
                causes.append(("W-", r["id"], "clears only at %d lanes x %.1f m (needs lanes <= %.2f m)" % (r["lanes"], LANE_LO, r["need_lane"])))
            else:
                why = ("lanes %d need <= %.2f m each" % (r["lanes"], r["need_lane"])) if r["lanes"] else "no lanes tag"
                causes.append(("U", r["id"], "inside the carriageway even at narrow lanes (%s); overlap at est. width %.2f m" % (why, r["overlap"])))
        for r in cross_c:
            causes.append(("P", r["id"], "%s crosses under the deck %.2f m from the pier centre" % (r["cls"], r["d"])))
        for b in ground_bld:
            causes.append(("B", b["id"], "inside a ground building (%s), %.2f m from its edge" % (b["type"], b["edge"])))
        for b in stations:
            causes.append(("S", b["id"], "under the raised station (base %.0f m), %.2f m from its edge" % (b["base"], b["edge"])))
        kinds = {c[0] for c in causes}
        if "U" in kinds:
            cls = "U"
        elif "B" in kinds:
            cls = "B"
        elif "P" in kinds:
            cls = "P"
        elif "W-" in kinds:
            cls = "W-"
        elif "W" in kinds:
            cls = "W"
        else:
            cls = "S"
    rec["cls"] = cls
    rec["causes"] = causes
    out.append(rec)

# Deck: one deck centred between twin tracks (both about the same distance away), else the nearest track's own.
for r in out:
    ts = sorted(r["tracks"], key=lambda t: t["d"])
    twin_deck = len(ts) > 1 and ts[1]["d"] < 6 and abs(ts[1]["d"] - ts[0]["d"]) < 0.5
    r["deck"] = "twin %d+%d" % tuple(sorted(t["id"] for t in ts[:2])) if twin_deck else "single %d" % ts[0]["id"]
    r["deck_offset"] = round(ts[0]["d"], 2)
# IDs along the line from the west end at Farmgate: the east-west stretch by easting, then south by northing.
def gparam(r):
    return r["e"] if r["n"] > 14965 else 3900 + (14975 - r["n"])
for r in out:
    r["gparam"] = round(gparam(r), 1)
out.sort(key=lambda r: (r["deck"].startswith("single 671984898"), r["gparam"]))
for i, r in enumerate(out, 1):
    r["pid"] = "K37-%02d" % i
# Spans: the nearest built pier ahead and behind on the same deck (within 4 m of this pier's deck line),
# counting the piers just outside the slice (north.json) so a deck leaving the slice does not read as a gap.
extra = json.load(open(sys.argv[3])) if len(sys.argv) > 3 else []
allp = [(r["e"], r["n"]) for r in out if not r["skipped"]] + [(q["e"], q["n"]) for q in extra if not q["skipped"]]
for r in out:
    prev = nxt = None
    for (e, n) in allp:
        de, dn = e - r["e"], n - r["n"]
        a = de * r["ux"] + dn * r["uz"]
        l = -de * r["uz"] + dn * r["ux"]
        if abs(l) > 6 or abs(a) < 0.5 or abs(a) > 80:
            continue
        if a > 0 and (nxt is None or a < nxt):
            nxt = a
        if a < 0 and (prev is None or -a < prev):
            prev = -a
    r["nearest_other"] = round(min(math.hypot(e - r["e"], n - r["n"]) for (e, n) in allp if (e, n) != (r["e"], r["n"])), 2)
    r["prev_span"] = round(prev, 1) if prev else None
    r["next_span"] = round(nxt, 1) if nxt else None
json.dump(out, open(sys.argv[2], "w"), indent=1, ensure_ascii=False)
import collections
print(collections.Counter(r["cls"] for r in out))
print(collections.Counter(c[0] for r in out for c in r["causes"]))
for r in out:
    print(r["pid"], r["chunk"], "E%.1f N%.1f" % (r["e"], r["n"]), r["cls"], r["deck"], "slide", r["slide"], "lat", r["lateral"], "span", r.get("prev_span"), r.get("next_span"), "tw", r["twin"] and (r["twin"]["spacing"], r["twin"]["median_at_lanes_lo"]), "|", "; ".join("%s %s %s" % c for c in r["causes"]))
