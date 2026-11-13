"""K-37 report data: groups every pier, writes the register CSV, the plan sheets (SVG) and shot views.
   report.py k37.json piers.json north.json <out dir>"""
import csv, json, math, os, sys, collections

R, LANE_LO, LANE_HI = 0.9, 3.0, 3.5
rows = json.load(open(sys.argv[1]))
raw = json.load(open(sys.argv[2]))
north = json.load(open(sys.argv[3]))
out = sys.argv[4]
os.makedirs(out, exist_ok=True)
by_pid = {r["pid"]: r for r in rows}

def near(pts, x, z):
    best = 1e18
    for i in range(0, len(pts) - 3, 2):
        ax, az, bx, bz = pts[i:i + 4]
        ex, ez = bx - ax, bz - az
        ll = ex * ex + ez * ez
        if ll < 1e-12:
            continue
        t = max(0.0, min(1.0, ((x - ax) * ex + (z - az) * ez) / ll))
        best = min(best, math.hypot(ax + ex * t - x, az + ez * t - z))
    return best

# Raw geometry in world metres, one copy per (chunk, id).
roads, blds, tracks = {}, {}, {}
for p in raw + north:
    cx, cz = p["cx"], p["cz"]
    for r in p["roads"]:
        roads.setdefault((p["chunk"], r["id"]), dict(r, w=[v + (cx if i % 2 == 0 else cz) for i, v in enumerate(r["pts"])]))
    for b in p["buildings"]:
        blds.setdefault(b["id"], dict(b, w=[v + (cx if i % 2 == 0 else cz) for i, v in enumerate(b["outer"])]))
    for t in p["tracks"]:
        k = (p["chunk"], t["id"], tuple(t["pts"][:2]))
        tracks.setdefault(k, [v + (cx if i % 2 == 0 else cz) for i, v in enumerate(t["pts"])])

# Building intrusion: does a ground building's outline reach into a conflicting road's lane-based surface?
def intrusion(b, r):
    lanes_half = (r["lanes"] * LANE_LO / 2) if r["lanes"] else max(0.4, r["width"] / 2)
    rw = [rr for (ch, rid), rr in roads.items() if rid == r["id"]]
    pts = b["w"]
    worst = None
    for rr in rw:
        for i in range(0, len(pts), 2):
            d = near(rr["w"], pts[i], pts[i + 1])
            inside = lanes_half - d
            worst = inside if worst is None else max(worst, inside)
    return round(worst, 2) if worst is not None else None

# Groups (the owner's five causes). Each conflicted pier gets one primary group; every cause stays listed.
for r in rows:
    kinds = {c[0] for c in r["causes"]}
    station = bool(r["stations"])
    if r["skipped"]:
        g = "spanned"
    elif not r["conflicts"] and not r["stations"] and not r["ground_buildings"]:
        g = "clear"
    elif station:
        g = "C genuine overlap: station over the carriageway"
    elif "B" in kinds:
        g = "D building footprint"
    elif "U" in kinds:
        g = "E insufficient evidence"
    elif "P" in kinds:
        g = "B pier placement rule"
    elif kinds <= {"W"}:
        g = "A road-width estimate (lanes tag)"
    elif kinds <= {"W", "W-"}:
        g = "A' road-width estimate (only at lanes of 3.0-3.3 m)"
    else:
        g = "E insufficient evidence"
    r["group"] = g
    # How far the pier sits inside the narrowest plausible carriageway (lanes x 3.0 m; the estimate if untagged).
    worst = None
    for c in r["conflicts"]:
        h = c["lanes"] * LANE_LO / 2 if c["lanes"] else c["half"]
        v = round(h + R - c["d"], 2)
        worst = v if worst is None else max(worst, v)
    r["inside_at_narrow_lanes"] = worst
    for b in r["ground_buildings"]:
        b["intrusion"] = {c["id"]: intrusion(blds[b["id"]], c) for c in r["conflicts"]}

# Spans along each deck in sequence (IDs are in line order); the twin deck splits at Farmgate station.
seq_twin = [r for r in rows if r["deck"].startswith("twin") and not r["skipped"]]
seq_single = [r for r in rows if r["deck"].startswith("single") and not r["skipped"]]
for seq in (seq_twin, seq_single):
    for i, r in enumerate(seq):
        nxt = seq[i + 1] if i + 1 < len(seq) else None
        span = round(math.hypot(nxt["e"] - r["e"], nxt["n"] - r["n"]), 1) if nxt else None
        # Over 60 m the next pier in sequence is on another deck: the twin deck splits into two single-track
        # decks across Farmgate station, and the eastbound one is in c_7_-31, outside the slice.
        r["span_next"] = span if span is not None and span <= 60 else None
        r["span_next_to"] = (nxt["pid"] if span <= 60 else "deck splits at Farmgate station") if nxt else None

cnt = collections.Counter(r["group"] for r in rows)
for k, v in sorted(cnt.items()):
    print("%3d  %s" % (v, k))
for g in sorted(cnt):
    print(g, ", ".join(r["pid"] for r in rows if r["group"] == g))
for r in rows:
    if r["group"].startswith("E") or r["group"].startswith("D"):
        tw = r["twin"]
        print(r["pid"], r["group"][:1], "inside@3.0 %.2f" % r["inside_at_narrow_lanes"], "lat@3.0", r["lateral"]["lo"], "twin", tw and (tw["spacing"], tw["median_at_lanes_lo"]),
              [(c["id"], c["d"], c["lanes"]) for c in r["conflicts"]], [(b["id"], b["edge"], b["intrusion"]) for b in r["ground_buildings"]])

json.dump(rows, open(os.path.join(out, "k37-final.json"), "w"), indent=1, ensure_ascii=False)
json.dump(dict(roads={"%s|%d" % k: v for k, v in roads.items()}, blds=blds, tracks=[v for v in tracks.values()]), open(os.path.join(out, "geom.json"), "w"))
