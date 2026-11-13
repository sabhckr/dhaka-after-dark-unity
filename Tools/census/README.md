# Building census

Read-only. Reads every exported chunk with `Dadc.Load`, merges building pieces cut by the chunk grid by OSM id, and writes one row per building with what the data says about it and what the builders draw for it today. Written for the master directive audit (2026-11-03, `Docs/PRODUCTION/MASTER_DIRECTIVE_AUDIT.md`).

```
Tools/census/run.sh [out dir]      # default Evidence/local/census-<time>; about 10 s
```

Outputs: `census.txt` (counts, and the road, bridge, rail, tree and point tallies used by the coverage report), `summary.md`, `register.csv` (every building; about 130 MB, gzip it) and `register-slice.csv` (the 12 chunks the game streams).

**Columns.** `id` (OSM way or relation id, stable across exports); `chunks`; `east_m`, `north_m` (area-weighted centre, world metres, EPSG:32646 offsets as in `world.json`); `osm_type`; `category` and `category_source` (own OSM type, else the landuse polygon the centre stands in, labelled `landuse`, else `unclassified`); `name`, `material_tag`, `roof_tag`; `height_m` and `height_source` (as drawn, `BuildingRules`); `levels_tagged`; `raised`; `footprint_m2`; `road_gap_m` and `street_facing` (an outline corner within `ChunkBuildings.ShopFacing`, 11 m, of a vehicle carriageway's edge, at grade; roads in the same chunk only); `landmark`, `landmark_cat` (a landmarks.json point inside the footprint); `facade_family`, `ground_floor_shops` (what `Facades.cs` draws today); `pieces`; `d06a_category`, `d06a_source`, `d06a_family` (what the game draws since D-06a: `BuildingClass` and the far-tier family, or `M2`); `d06b_wall`, `d06b_wall_source` (`Tag` or `Rule`), `d06b_roof`, `d06b_parapet`, `d06b_kit` (the building's skin since D-06b, `BuildingSkin`, from its largest piece). The census also builds every chunk and prints the far tier's wall counts and the classification time, and builds every chunk's roads for the rooftop kit's counts, the slice's street-mesh vertices and the time. `summary.py` adds the district (nearest named suburb, quarter or neighbourhood: an inference, since the data has no district boundaries), the proposed detail level and a status per part.

**Proposed levels.** L1: contains a landmark point, is a train station, or is named and institutional, religious or special. L2: street-facing. L3: the rest. Every building also needs the far tier (L4). These are proposals for the owner, not decisions.
