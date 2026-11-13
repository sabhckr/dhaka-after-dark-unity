"""K-37 register: pier-register.csv and the per-pier markdown table from k37-final.json.
   register.py <out dir>"""
import csv, json, os, sys

out = sys.argv[1]
rows = json.load(open(os.path.join(out, "k37-final.json")))
LANE_LO = 3.0
GROUP_SHORT = {"A": "A width estimate", "A'": "A' width estimate (narrow lanes)", "B": "B pier placement", "C": "C station over road",
               "D": "D building footprint", "E": "E insufficient evidence", "clear": "clear", "spanned": "junction, spanned"}

def g(r):
    k = r["group"].split(" ")[0] if r["group"][0] in "ABCDE" else r["group"]
    return k

def fmt_m(v):
    return "" if v is None else ("%.2f" % v).rstrip("0").rstrip(".")

def road_s(c):
    return "%d %s%s w %s m %s, lanes %s, %s; %.2f m from centre line, %s m into it" % (
        c["id"], c["cls"], (" (" + c["name"] + ")") if c["name"] else "", fmt_m(c["width"]), "est." if c["est"] else "mapped",
        c["lanes"] if c["lanes"] else "untagged", "one-way" if c["oneway"] else "two-way", c["d"], fmt_m(c["overlap"]))

with open(os.path.join(out, "pier-register.csv"), "w", newline="") as f:
    w = csv.writer(f)
    w.writerow(["pier_id", "chunk", "east_m", "north_m", "chunk_x_m", "chunk_z_m", "deck", "rail_ways_and_segment", "deck_centre_to_track_m",
                "built", "builder_verdict", "group", "causes", "conflicting_roads", "roads_within_25m_lateral", "twin_centreline_spacing_m",
                "median_at_3.0m_lanes_m", "inside_raised_station", "inside_ground_building", "buildings_within_10m", "inside_narrowest_carriageway_m",
                "slide_to_clear_est_m", "slide_to_clear_3.5m_lanes_m", "slide_to_clear_3.0m_lanes_m", "shift_across_to_clear_est_m",
                "shift_across_3.5m_lanes_m", "shift_across_3.0m_lanes_m", "span_to_next_m", "next_pier", "nearest_other_pier_m"])
    for r in rows:
        tw = r["twin"]
        w.writerow([r["pid"], r["chunk"], "%.2f" % r["e"], "%.2f" % r["n"], "%.2f" % r["x"], "%.2f" % r["z"], r["deck"],
                    "; ".join("%d seg %d (%.2f m)" % (t["id"], t["seg"], t["d"]) for t in r["tracks"]), "%.2f" % r["deck_offset"],
                    "no" if r["skipped"] else "yes", r["why"] or "clear", GROUP_SHORT[g(r)],
                    " | ".join("%s %d: %s" % tuple(c) for c in r["causes"]),
                    " | ".join(road_s(c) for c in r["conflicts"]),
                    " | ".join("%d %s %+.1f m%s" % (x["id"], x["cls"], x["off"], " (parallel)" if x["parallel"] else " (crossing)") for x in sorted(r["roads"], key=lambda x: x["off"]) if not x["foot"] and abs(x["off"]) < 25),
                    fmt_m(tw["spacing"]) if tw else "", fmt_m(tw["median_at_lanes_lo"]) if tw and tw["median_at_lanes_lo"] is not None else "",
                    " | ".join("%d %s base %g m, %.2f m from edge" % (b["id"], b["name"] or "", b["base"], b["edge"]) for b in r["stations"]),
                    " | ".join("%d %s %s, %.2f m from edge; reaches %s m into the 3.0 m-lane carriageway" % (b["id"], b["type"], b["name"] or "", b["edge"], ", ".join(fmt_m(v) for v in b["intrusion"].values())) for b in r["ground_buildings"]),
                    " | ".join("%d %s%s %.1f m" % (b["id"], b["type"] or "building", " inside," if b["inside"] else "", b["edge"]) for b in r["nearby_buildings"]),
                    fmt_m(r["inside_at_narrow_lanes"]),
                    fmt_m(r["slide"]["est"]), fmt_m(r["slide"]["hi"]), fmt_m(r["slide"]["lo"]),
                    fmt_m(r["lateral"]["est"]), fmt_m(r["lateral"]["hi"]), fmt_m(r["lateral"]["lo"]),
                    fmt_m(r.get("span_next")), r.get("span_next_to") or "", fmt_m(r["nearest_other"])])

# Markdown table, one row per pier.
L = ["| Pier | Chunk | East, north (m) | Rail way, segment | Builder | Group | Road conflicts (way, class, width, lanes, centre-line distance, overlap) | Buildings | Into narrowest carriageway | Clears with |",
     "|---|---|---|---|---|---|---|---|---|---|"]
for r in rows:
    rails = ", ".join("%d s%d" % (t["id"], t["seg"]) for t in sorted(r["tracks"], key=lambda t: t["d"])[:2] if t["d"] < 12)
    confl = "; ".join("%d %s %s m%s, %s, %.1f m, **%s m**" % (c["id"], c["cls"], fmt_m(c["width"]), " est." if c["est"] else " mapped", ("%d lanes" % c["lanes"]) if c["lanes"] else "no lanes tag", c["d"], fmt_m(c["overlap"])) for c in r["conflicts"]) or "none"
    bl = "; ".join(["station %d (%.1f m in)" % (b["id"], b["edge"]) for b in r["stations"]] + ["%d %s (%.2f m in)" % (b["id"], b["type"], b["edge"]) for b in r["ground_buildings"]]) or "none"
    fixes = []
    if r["skipped"]:
        fixes.append("not built (junction spanned)")
    elif not r["conflicts"] and not r["stations"] and not r["ground_buildings"]:
        fixes.append("-")
    else:
        if r["slide"]["hi"] == 0 and r["lateral"]["hi"] == 0:
            fixes.append("lanes x 3.5 m")
        elif r["slide"]["lo"] == 0 and r["lateral"]["lo"] == 0:
            fixes.append("lanes x 3.0 m")
        else:
            for k, lab in (("est", "est. width"), ("hi", "3.5 m lanes"), ("lo", "3.0 m lanes")):
                if r["slide"][k] not in (None, 0):
                    fixes.append("slide %s m (%s)" % (fmt_m(r["slide"][k]), lab))
                    break
            sh = r["lateral"]["lo"]
            if sh not in (None, 0):
                fixes.append("shift across %s m (3.0 m lanes)" % fmt_m(sh))
            if not fixes:
                fixes.append("none found within 12 m")
    L.append("| %s | %s | %.1f, %.1f | %s | %s | %s | %s | %s | %s | %s |" % (
        r["pid"], r["chunk"], r["e"], r["n"], rails, r["why"] or "clear", GROUP_SHORT[g(r)], confl, bl,
        fmt_m(r["inside_at_narrow_lanes"]) if r["inside_at_narrow_lanes"] is not None and r["inside_at_narrow_lanes"] > 0 else "-", "; ".join(fixes)))
open(os.path.join(out, "register-table.md"), "w").write("\n".join(L) + "\n")
print(len(rows), "rows")
