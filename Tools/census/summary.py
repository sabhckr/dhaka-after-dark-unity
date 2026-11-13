"""Summary tables for the building census (Tools/census/run.sh), read-only.

summary.py <buildings.csv> <places.json> <out dir>

Adds to each row the district (the nearest named place: suburb, quarter or neighbourhood, a labelled
inference), the proposed detail level and today's status per part, then writes register.csv (every
building), register-slice.csv (the 12 chunks the game streams) and summary.md.
"""
import csv, json, math, sys, collections, os

# The chunks ChunkStreamer streams (SliceOnly, Slice = RectInt(6, 27, 4, 3) in cell space; cell z = -iy - 1).
SLICE = {f"c_{x}_{-z - 1}" for x in range(6, 10) for z in range(27, 30)}
# Directive Part 8.8 / 19 districts, matched to place names in places.json. "Old Dhaka" is not a place name in the
# data; its usual neighbourhoods are listed instead.
DISTRICTS = ["Farmgate", "Kawran Bazar", "Mohammadpur", "Dhanmondi", "Gulshan", "Gulshan 1", "Gulshan 2", "Banani",
             "Shahbag", "Gulistan", "Lalbagh", "Chawkbazar", "Sutrapur", "Kotwali", "Bangshal", "Wari"]
L1_CATS = {"institutional", "religious", "special"}


def main(csv_path, places_path, out):
    places = [p for p in json.load(open(places_path))["items"] if p["kind"] in ("suburb", "quarter", "neighbourhood")]
    grid = collections.defaultdict(list)
    for p in places:
        grid[(int(p["x"] // 1000), int(p["z"] // 1000))].append(p)

    def district(x, z):
        gx, gz = int(x // 1000), int(z // 1000)
        for r in range(0, 30):
            best = None
            for i in range(gx - r, gx + r + 1):
                for j in range(gz - r, gz + r + 1):
                    for p in grid.get((i, j), ()):
                        d = math.hypot(p["x"] - x, p["z"] - z)
                        if best is None or d < best[0]:
                            best = (d, p)
            if best and best[0] <= r * 1000:
                return best[1].get("nameEn") or best[1]["name"], best[0]
        return "", 0

    rows = list(csv.DictReader(open(csv_path, newline="")))
    cols = list(rows[0].keys()) + ["district_nearest_place", "district_distance_m", "proposed_level", "geometry_status", "texture_status",
                                   "facade_status", "roof_status", "ground_floor_status", "lod_status", "visual_validation", "known_issues"]
    count = collections.Counter()
    by = collections.defaultdict(collections.Counter)
    with open(os.path.join(out, "register.csv"), "w", newline="") as fa, open(os.path.join(out, "register-slice.csv"), "w", newline="") as fs:
        wa, ws = csv.DictWriter(fa, cols), csv.DictWriter(fs, cols)
        wa.writeheader(); ws.writeheader()
        for r in rows:
            x, z = float(r["east_m"]), float(r["north_m"])
            name, dist = district(x, z)
            r["district_nearest_place"], r["district_distance_m"] = name, f"{dist:.0f}"
            named = bool(r["name"])
            if r["landmark"] == "1" or r["osm_type"] == "train_station" or (named and r["category"] in L1_CATS):
                lvl = "L1 landmark"
            elif r["street_facing"] == "1":
                lvl = "L2 street-facing"
            else:
                lvl = "L3 block interior"
            r["proposed_level"] = lvl
            fac = r["facade_family"] == "1"
            r["geometry_status"] = "extruded footprint + facade family (M2)" if fac else "extruded footprint only"
            r["texture_status"] = "generic plaster, 1 of 6 tones by id"
            d6 = r.get("d06a_family", "")
            r["facade_status"] = ("M2 walk-up family (bays, windows, grilles, bands)" if fac else
                                  f"D-06a far tier, {d6} pattern (shader, no geometry)" if d6 and d6 != "None" else "none")
            r["roof_status"] = "flat slab + parapet" if fac else "flat slab"
            r["ground_floor_status"] = "M2 shops (shutters, blank boards)" if r["ground_floor_shops"] == "1" else "none"
            r["lod_status"] = "none (one mesh at every distance)"
            r["visual_validation"] = "area golden views (m2-overview, m2-shopfront); not per building" if fac else "none"
            issues = []
            if r["height_source"] != "measured" and r["levels_tagged"] != "1":
                issues.append("K-06 height estimated")
            if r["pieces"] != "1":
                issues.append("K-16 split by chunk grid")
            r["known_issues"] = "; ".join(issues)
            wa.writerow(r)
            slice_ = any(c in SLICE for c in r["chunks"].split(";"))
            if slice_:
                ws.writerow(r)
            for scope in (("city",) + (("slice",) if slice_ else ())):
                count[scope] += 1
                b = by[scope]
                b["cat:" + r["category"]] += 1
                b["catsrc:" + r["category_source"]] += 1
                b["level:" + lvl] += 1
                b["type:" + (r["osm_type"] or "(none)")] += 1
                b["height:" + (r["height_source"] or "(none)")] += 1
                b["levels_tagged"] += r["levels_tagged"] == "1"
                b["material_tag"] += bool(r["material_tag"])
                b["roof_tag"] += bool(r["roof_tag"])
                b["named"] += named
                b["landmark"] += r["landmark"] == "1"
                b["street"] += r["street_facing"] == "1"
                b["facade"] += fac
                b["shops"] += r["ground_floor_shops"] == "1"
                b["raised"] += r["raised"] == "1"
                b["split"] += r["pieces"] != "1"
                b["district:" + name] += 1
                if "d06a_category" in r:
                    b["d6cat:" + r["d06a_category"]] += 1
                    b["d6src:" + r["d06a_source"]] += 1
                    b["d6fam:" + r["d06a_family"]] += 1
                h = float(r["height_m"])
                b["hband:" + ("<6 m" if h < 6 else "6-12 m" if h < 12 else "12-24 m" if h < 24 else "24-45 m" if h < 45 else ">=45 m")] += 1

    with open(os.path.join(out, "summary.md"), "w") as f:
        def p(s=""):
            print(s); f.write(s + "\n")
        for scope in ("city", "slice"):
            b, n = by[scope], count[scope]
            p(f"## {'Whole exported world (1,303 chunks)' if scope == 'city' else 'Streamed slice (12 chunks)'}: {n:,} buildings")
            p()
            pct = lambda k: f"{b[k]:,} ({100 * b[k] / n:.1f} %)"
            p("| Measure | Count |")
            p("|---|---|")
            for label, k in [("Own OSM type (classified from the data)", "catsrc:osm type"), ("Classified from the landuse they stand in (inferred)", "catsrc:landuse"),
                             ("Unclassified", "catsrc:none"), ("Height tagged or measured", None), ("Levels tagged", "levels_tagged"),
                             ("Material tagged", "material_tag"), ("Roof shape tagged", "roof_tag"), ("Named", "named"), ("Containing a landmark point", "landmark"),
                             ("Street-facing (corner within 11 m of a carriageway)", "street"), ("Raised (base above ground)", "raised"),
                             ("Split by the chunk grid", "split"), ("With the M2 facade family", "facade"), ("With M2 ground-floor shops", "shops")]:
                if k is None:
                    p(f"| {label} | {b['height:measured']:,} measured |")
                else:
                    p(f"| {label} | {pct(k)} |")
            p()
            p("| Category | Buildings |"); p("|---|---|")
            for k, v in sorted(((k[4:], v) for k, v in b.items() if k.startswith("cat:")), key=lambda kv: -kv[1]):
                p(f"| {k} | {v:,} ({100 * v / n:.1f} %) |")
            p()
            if any(k.startswith("d6cat:") for k in b):
                for title, pre in (("D-06a category (BuildingClass)", "d6cat:"), ("D-06a category source", "d6src:"), ("D-06a facade drawn", "d6fam:")):
                    p(f"| {title} | Buildings |"); p("|---|---|")
                    for k, v in sorted(((k[len(pre):], v) for k, v in b.items() if k.startswith(pre)), key=lambda kv: -kv[1]):
                        p(f"| {k} | {v:,} ({100 * v / n:.1f} %) |")
                    p()
            p("| Proposed level | Buildings |"); p("|---|---|")
            for k, v in sorted((k[6:], v) for k, v in b.items() if k.startswith("level:")):
                p(f"| {k} | {v:,} ({100 * v / n:.1f} %) |")
            p()
            p("| Height (drawn) | Buildings |"); p("|---|---|")
            for k in ("<6 m", "6-12 m", "12-24 m", "24-45 m", ">=45 m"):
                p(f"| {k} | {b['hband:' + k]:,} |")
            p()
            p("Most common OSM types: " + ", ".join(f"{k[5:]} {v:,}" for k, v in b.most_common() if k.startswith("type:"))[:900])
            p()
            if scope == "city":
                p("| Directive district (nearest named place) | Buildings |"); p("|---|---|")
                for d in DISTRICTS:
                    p(f"| {d} | {b['district:' + d]:,} |")
                p()
                p("Largest 15 by nearest place: " + ", ".join(f"{k[9:]} {v:,}" for k, v in b.most_common() if k.startswith("district:"))[:900])
                p()


if __name__ == "__main__":
    main(*sys.argv[1:4])
