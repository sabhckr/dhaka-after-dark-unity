# Building assets and 2K graphics: audit and shortlist

> **Correction, 2026-11-11 (after the first before-and-after shots).** Section 1 describes the generated buildings of the older map (D-06). The map the game plays since the city rebuild of 6 Nov ([CITY_REBUILD.md](CITY_REBUILD.md)) places **14,308 buildings from the owner's models**, drawn with their own pictures (`Resources/City/Tex`, 77 files), not the world library. So the scanned library changes roads, pavements and ground, not the building walls: measured on six views at 2560 × 1440, walls are pixel-for-pixel the same before and after. And `Tools/models/city.py` re-encodes those pictures at **no more than 512 px** (1,024 for a few), although the sources are up to 4,096 px; that predates the owner's rule of 9 Nov to keep models at source quality.

Status: **audit and research only, 2026-11-11.** Nothing was downloaded, imported or changed in the game. The download list below waits for the owner's approval, along with four decisions (last section).

Brief: upgrade the buildings with realistic, legally usable free 3D assets, a 2K-class graphics pipeline (2560 × 1440 render, 2048 px textures, 4K for hero assets), LODs, culling, instancing and streaming; audit first, research before downloading, report real results only.

## 1. What the project has today

### How buildings are made

Buildings are **not placeholder models**. Every building is generated from its real map footprint and height (494,978 buildings city-wide; 1,899,150 walls), so each one stands where the real one stands:

| Layer | What it does | Source |
|---|---|---|
| Far tier | Walls with painted bays and floors: windows, glass, shutters and signs drawn by the shader | `D06A_CLASSIFICATION_FAR_TIER.md`, `DadWorld.shader` |
| Skins | Each building gets a wall and roof material and a tone (plaster 57 %, paint 24 %, brick 16 %, sheet, concrete, glass) | `D06B_ROOFS_MATERIALS.md`, `BuildingSkin.cs` |
| Parapets and rooftop kit | Parapets on 95 % of buildings; stair and lift rooms, water tanks, satellite dishes, AC condensers | `Rooftops.cs` |
| Near facades | Within about 150 m: real geometry for sills, sunshades (chhajja), grilles, balconies, shopfronts, shutters, signboards, in eight styles (walk-up, shop-house, office, market, industrial, institutional, Old Dhaka, modern) | `D06C_NEAR_FACADES.md`, `NearFacades.cs` |

**Already built, of what the brief asks for:** level-of-detail tiers (far tier, near tier faded in by dither between 140 m and 110 m, no popping, tested), chunk streaming and unloading, near kits built on worker threads and dropped beyond 350 m, mesh collision per chunk separate from the near-facade dressing, frustum culling (Unity's). Not built: occlusion culling, GPU instancing of repeated props (the kit is merged into per-cell meshes instead).

### Materials and textures

All walls, roofs and streets draw from one **material library**: 16 layers in three texture arrays (albedo; normal plus height; metallic, occlusion, cavity, smoothness). The layers were **generated in code** (D-02), not scanned:

`Asphalt, Concrete, Brick, Plaster, Paint, Metal, Rust, Glass, RoofSheet, Tiles, Paving, Soil, Grass, Sand, Water, Marking`

- **512 × 512 per layer.** GPU memory: 16.33 MB for the library; its own test limits it to 24 MB (`MaterialLibraryTests`).
- The shader adds wear, dirt, fading, cavity, road repairs and two-scale macro variation (`WorldMacro`).
- The art texture test (`ArtTexturesAreWithinTheBudget`) now allows 2,500 MB in all (owner, 9 Nov, for full-quality NPCs).

So the textures, not the shapes, are the weakest part close up: 512 px over 2 to 4 m of wall is about 128 to 256 px per metre.

### Graphics settings (D-13, look pass)

| | Low | Medium | High |
|---|---|---|---|
| Renderer | Forward | Forward+ | Forward+ |
| Shadows | 60 m, 1024, 2 cascades | 100 m, 2048, 2 cascades | 150 m, 2048, 4 cascades, soft |
| Ambient occlusion (SSAO) | Off | Downsampled | Full |
| Antialiasing | FXAA | FXAA | SMAA |
| Bloom | Off | On | On |
| Reflections | Sky only (no probes; URP has no screen-space reflections built in) | | |

Low is picked automatically on integrated graphics; High is never picked for you.

### Measured performance (the latest real numbers)

Development player, MacBook Air M1 8 GB, **1280 × 720, High**, street path, 4 Nov (D-06c bench): GPU mean 7.73–8.25 ms, triangles p50 2.34–2.38 M, batches p50 158–161, graphics memory 150 MB, end memory 672–674 MB. Look pass (later): texture memory 100 MB on High, 72 MB on Low.

**Nothing has been measured at 2560 × 1440.** It is four times the pixels of 1280 × 720; how the GPU time scales has to be benched, not guessed.

## 2. Where the brief clashes with standing decisions

1. **Target hardware.** The owner's world brief (6 Nov) sets the target as an i3 10th gen with 4 GB RAM and integrated graphics, at **720p, 30 FPS, Low**. A 1440p, 2K-texture pipeline can only be the **High** preset on stronger machines; Low must stay where it is.
2. **Downloaded buildings cannot replace the generated ones.** A downloaded model has its own footprint and height; the city's buildings follow the real map. Swapping them would break the map and repeat a few models across 495,000 sites. What can come from libraries: **materials** (every building at once), **props and details** (AC units, cables, poles, shutters), and **landmarks** placed one by one.
3. **Texture resolution and the world library limit.** 2048 px layers cost 16 times the memory of 512 (below); the library's own 24 MB test must be raised by the owner.
4. **"Wait for my approval before mass building placement"** and "nothing past the box" still hold: any landmark outside the Farmgate box waits for the layout decisions.

## 3. Sources checked

| Library | Licence | Fit |
|---|---|---|
| **Poly Haven** (869 textures, 521 models; catalogue read through its public API) | **CC0** for every asset, no attribution needed | Best fit: scanned, real-world PBR with colour, normal, roughness, AO and height maps, up to 8K or 16K; strong urban, weathered and South-Asia-like surfaces |
| **ambientCG** (2,000 materials read through its API) | **CC0** for every asset | Good second source for materials; no individual pick yet |
| **Sketchfab** (searched by API: Dhaka, Bangladesh, Old Dhaka, South Asian apartment, Kolkata) | Per model: CC BY, Free Standard, CC BY-NC and others; downloads need a signed-in account | Few usable buildings (below) |
| Kenney, OpenGameArt | CC0 or mixed | Not used: stylised, low-poly, against the realism direction |
| CGTrader free section | Per model, own licence | Not searched in this pass |

### Sketchfab, Dhaka-specific: none recommended now

| Model | Licence | Faces | Verdict |
|---|---|---|---|
| [Lalbagh Fort Mosque](https://sketchfab.com/3d-models/fe0cefe46cb940c580ff2cba0f20aa77) by talatmahmud81 | CC BY 4.0 (credit required, commercial use allowed) | 51,166 | **Rejected for realism**: preview shows flat colours, no textures. Lalbagh Fort is in the data (c_7_-22) but outside the box |
| [Moder Gorob](https://sketchfab.com/3d-models/cea4c4611f684f5c944a779f2bc02fcf) by talatmahmud81 | CC BY 4.0 | 294,889 | Sculpture, not a building; not previewed; outside the box |
| [Old Office Building](https://sketchfab.com/3d-models/9d1f6baae89e42df9639233dd310a4a4) by Potato_Pizza | Sketchfab Free Standard (commercial use allowed, basic restrictions) | 6,837 | Made after a real Dhaka office; not previewed; its footprint will not match a mapped building |
| [Bangladesh Building](https://sketchfab.com/3d-models/97a24d85c2414d0e854165ab5cbe21b3) by AtikHassan | CC BY 4.0 | 3,068 | Low-poly; not previewed |

## 4. Shortlist (all Poly Haven, CC0, no attribution required)

### 4a. Materials: replace the generated library layers

| Library layer | Poly Haven set | Creator | Real size | Max source | 2K download |
|---|---|---|---|---|---|
| Asphalt | [road_damaged](https://polyhaven.com/a/road_damaged) | Dimitrios Savva | 2.2 m | 8K | 83.6 MB |
| Concrete | [concrete_wall_008](https://polyhaven.com/a/concrete_wall_008) | Dario Barresi, Charlotte Baglioni | 2.71 m | 8K | 57.5 MB |
| Brick | [red_brick_plaster_patch_02](https://polyhaven.com/a/red_brick_plaster_patch_02) | Rob Tuytel | 1.5 m | 8K | 37.8 MB |
| Plaster | [worn_plaster_wall](https://polyhaven.com/a/worn_plaster_wall) | Dimitrios Savva | 1.8 m | 8K | 75.8 MB |
| Paint | [painted_concrete](https://polyhaven.com/a/painted_concrete) | Dario Barresi, Dimitrios Savva | 2.0 m | 8K | 86.7 MB |
| Metal | [metal_plate](https://polyhaven.com/a/metal_plate) | Rob Tuytel | 0.5 m | 8K | 93.7 MB |
| Rust | [rusty_metal_02](https://polyhaven.com/a/rusty_metal_02) | Rob Tuytel | 1.0 m | 8K | 33.0 MB |
| RoofSheet | [rusty_corrugated_iron](https://polyhaven.com/a/rusty_corrugated_iron) | Charlotte Baglioni | 2.0 m | 16K | 76.8 MB |
| Tiles | [terrazzo_tiles](https://polyhaven.com/a/terrazzo_tiles) | Amal Kumar | 2.0 m | 16K | 78.0 MB |
| Paving | [concrete_pavers_03](https://polyhaven.com/a/concrete_pavers_03) | Charlotte Baglioni | 2.0 m | 8K | 77.5 MB |
| Soil | [dirt](https://polyhaven.com/a/dirt) | Charlotte Baglioni | 2.0 m | 8K | 81.9 MB |
| (optional) tar roofs | [bitumen](https://polyhaven.com/a/bitumen) | Rob Tuytel | 20 m | 8K | 79.0 MB |

Kept as generated: Glass, Water, Marking (no scanned equivalent would work better in this shader), Grass and Sand (rare close up in the box).

Download sizes are Poly Haven's PNG files with every map they offer: about **0.86 GB at 2K, 0.22 GB at 1K** for the 12 sets, well inside the 35 GB project limit.

**Modifications needed (to be listed per set in the manifest):**
- **Colour.** The skins tint each layer to a building's tone (six paints, three bricks and so on); a scanned colour map must be made neutral around the layer's mean, or every painted wall turns the scan's colour. The manifest's mean colours are the reference.
- **Packing.** Each set's maps are repacked into the library's three arrays (albedo; normal plus height; metallic, occlusion, cavity, smoothness), with the same layer order, so no shader or building code changes.
- **Resolution.** Taken at the chosen size straight from the source (decision 2); never upscaled.

**GPU memory (calculation from today's 16.33 MB at 512 px, not measured):**

| Layer size | Library | Change |
|---|---|---|
| 512 (today) | 16.3 MB | |
| 1024 | about 65 MB | +49 MB |
| 2048 | about 261 MB | +245 MB |

### 4b. Props and details (for the near kit and streets)

| Model | Creator | Triangles (source) | 2K download | Use |
|---|---|---|---|---|
| [exterior_aircon_unit](https://polyhaven.com/a/exterior_aircon_unit) | Monsta3D | 18,986 | 37.5 MB | Wall and roof AC units (today 52 triangles each) |
| [modular_electric_cables](https://polyhaven.com/a/modular_electric_cables) | Kuutti Siitonen | 42,078 | 54.6 MB | Overhead cable bundles, a defining Dhaka street feature |
| [modular_electricity_poles](https://polyhaven.com/a/modular_electricity_poles) | James Ray Cock | 200,610 | 61.4 MB | Poles (the set has several; one pole is a fraction of this) |
| [rollershutter_door](https://polyhaven.com/a/rollershutter_door) | MP | 1,104 | 12.9 MB | Shopfront shutters |
| [rollershutter_window_01](https://polyhaven.com/a/rollershutter_window_01) | MP | 1,104 | 12.4 MB | Window shutters |
| [utility_box_01](https://polyhaven.com/a/utility_box_01), [utility_box_02](https://polyhaven.com/a/utility_box_02) | James Ray Cock | 4,404 / 6,268 | 13.9 / 13.7 MB | Street cabinets |
| [modular_metal_gutter](https://polyhaven.com/a/modular_metal_gutter) | Maxim Domnin | 16,490 | 10.6 MB | Downpipes |
| [water_manhole_cover](https://polyhaven.com/a/water_manhole_cover) | Raunox | 6,301 | 17.0 MB | Streets |
| [security_light](https://polyhaven.com/a/security_light) | Maximilian Schuster | 4,668 | 14.2 MB | Wall lights at night |

These are heavy compared with today's kit, which draws a condenser in 52 triangles. They would go in **only within the near range**, with the source model as the nearest level and lighter levels made from it with `Tools/models/reduce.py` (the owner's rule: the source stays untouched, and any reduction is measured and reported, never silent).

## 5. Proposed order of work

1. **Materials** (one change upgrades every building): download the chosen sets, neutralise and pack, rebuild the library, run the material tests and goldens (reviewed, not accepted automatically), bench on Low, Medium and High, take before-and-after pictures from the same views.
2. **Quality presets for 1440p**: a High setting with render scale 1.0 at 2560 × 1440, anisotropic filtering on the library, and bench numbers on the M1; Low unchanged for the target machine.
3. **Props**, one at a time in the near kit, each benched.
4. **Landmarks** only after the layout decisions, from an owner-reviewed list.

New assets go in `Assets/_Project/Art/Environment/` (`textures/`, `materials/`, `props/`, `landmarks/`), with `ASSET_MANIFEST.md` beside them: source URL, creator, licence, attribution, files taken, modifications.

## 6. Decisions for the owner

1. **Approach:** materials first, then props; no downloaded whole buildings in place of the mapped ones.
2. **Layer resolution:** 1024 on every preset (+49 MB), or 2048 on High with 1024 on Medium and Low (two libraries, only one loaded at a time: about +245 MB on High, +49 MB on Medium and Low). Either way the library's 24 MB test limit must be raised.
3. **1440p:** a High-only target, with Low kept at the 720p target machine.
4. **Downloads:** approve the 12 material sets (about 0.86 GB at 2K) and, later, the props list.

## 7. New buildings: search and shortlist (2026-11-11, owner chose "find new buildings")

The map places 14,308 buildings from 9 large models (office, two towers, three apartment blocks, school, abandoned block, workshop) and 9 small slum houses, by group (hub, rich, crime, port, slum; `Tools/models/city.py` MODELS and MINI). New models would take those places through the same tool.

Searched: Sketchfab public API, 53 queries (apartments, scans, South Asian cities, slums, shops, offices, warehouses), downloadable only. 421 + about 300 results; kept only CC0, CC BY, CC BY-SA and Sketchfab Free Standard (all allow commercial use; BY and BY-SA need a credit). Dropped: non-commercial and no-derivatives licences; "Sleeping Dogs Universe" and "Indian Bike Driving 3d Building" (named after games, so likely rips); the factory with its author's name on the facade. 25 previewed: [BUILDING_ASSETS/candidates.png](BUILDING_ASSETS/candidates.png). Archive sizes and texture resolutions are only shown to a signed-in account, so they are not known yet.

| Role | Model | Licence | Faces | Verdict |
|---|---|---|---|---|
| Apartments | [Old Residential Building [4k]](https://sketchfab.com/3d-models/dd400ed693cf416e940c5b8e3107205e), andrej.grave | CC BY | 276,689 | **Recommended**: weathered concrete walk-up, balconies, AC units |
| Apartments | [Procedural Hong Kong building](https://sketchfab.com/3d-models/528a732e84c44fd49c4726f341014a23), udayjeet | CC BY | 197,097 | **Recommended**: 10 floors over shops |
| Apartments | [Hong Kong Monster building Modular](https://sketchfab.com/3d-models/cf337d8398274b23acdb1d10c72924f4), abhayexe | Free Standard | 253,351 | **Recommended**: dense, grimy, AC units; already has levels of detail |
| Apartments | [Hong kong monster building 3](https://sketchfab.com/3d-models/8ad38d2c3cdd49aea07017379c31d238), Pasha. | CC BY | 72,588 | **Recommended**: colourful balconies, lighter |
| Apartments + shops | [Full Gameready City Buildings IV](https://sketchfab.com/3d-models/31c1ede1fcbd49c7af85825905446ec5), abhayexe | Free Standard | 369,465 | Good; a whole block; Chinese signs |
| Commercial | [Atlanta Corporate Office Building](https://sketchfab.com/3d-models/d96380fb001345cca9a9be121f3e43d5), 99.Miles | CC BY | 37,290 | **Recommended** for the hub towers |
| Commercial | [Old Office Building](https://sketchfab.com/3d-models/9d1f6baae89e42df9639233dd310a4a4), Potato_Pizza | Free Standard | 6,837 | **Recommended**: made after a real Dhaka office |
| Slum | [Modular Slums](https://sketchfab.com/3d-models/e2b6f532e1fe4f7280890971d78183f4), alexferrart3D | CC BY | 17,328 | **Recommended**: small brick and concrete houses |
| Slum | [Slum house](https://sketchfab.com/3d-models/e902e0af80804d01924387da6af8eecf), designbureau | CC BY | 127,846 | Good, a little stylised |
| Slum | [Tin-wood Shed](https://sketchfab.com/3d-models/ffa56b73db14448a8307a20f2341f681), Juwel_Mahmud | CC BY | 2,831 | Good as a prop |
| Slum | [Chennai Slum](https://sketchfab.com/3d-models/66bc17683176497894ee025972b6258d), aswin4550 | CC BY | 13,296 | Maybe; a whole scene |
| Shops | [Asian Shop Pack](https://sketchfab.com/3d-models/2f603ad8d5c640f7b9794f6916f9fbdd), abhayexe | CC BY | 39,203 | Maybe; Korean and Chinese signs |
| Warehouse | [Warehouse FBX Model Free](https://sketchfab.com/3d-models/daa7fd3ff88945298d00045ca40a4c03), Nicholas01 | CC BY | 11,106 | Maybe; preview shows the inside only |

Not found anywhere: a free, realistic, textured Dhaka apartment block or Bangladeshi shophouse with Bangla signs.

**What it would cost (to measure, not measured):**
- **Triangles.** Today's models are low-poly; the recommended apartments are 73k to 277k faces each. Hundreds stand in view at once, so each needs the source as its nearest level and lighter levels made from it (`Tools/models/reduce.py`), measured and reported per the owner's rule.
- **Textures.** Likely 2K to 4K sets per model, at source size; the target machine (Low) will need a mipmap limit for city pictures as the world library has.
- **Signs.** Hong Kong models carry Chinese signs; blanking or replacing them is a change to the model (allowed by both licences), to be listed in the manifest.
- **Credits.** CC BY models need the author's name and link in the game's credits.
- **Downloads.** Sketchfab needs a signed-in account to download.

## 8. First trial: Old Residential Building in the city (2026-11-11, measured)

All seven shortlisted buildings were downloaded in source format, and only Old Residential was imported. The download record is `~/Downloads/3D assets/buildings-new/MANIFEST.md`. The licence and the changes made are in `BUILDING_ASSETS/LICENSES.md`.

**How it was imported**
- **Tool:** `Editor/BuildingImport.cs` put the model into `Resources/Buildings/old_residential/`.
- **Files:** the FBX, colour maps and normal maps are unchanged. A metallic, occlusion and smoothness mask was made for each material.
- **Texture size:** full size (two materials at 4096, three at 2048), in the "World" mip group.
- **Size in the repo:** 130 MB.
- **Measured at import:** 28.99 × 21.37 × 17.08 m, 22 renderers, 276,684 triangles, 348,706 vertices, 5 materials. No LODs yet.

**How it was placed**
- **Flag:** the player flag `-dad-building old_residential@2211.82,1745.96,135.39` (`Runtime/Core/TrialBuilding.cs`).
- **What it replaces:** it stands in for the asia_block at that place, 20 m beside Central Avenue. That block is no longer drawn (`CityView.Hidden`), and its footprint collider stays.
- **Turn:** 135.39° puts the balcony front facing the street. The city's own turn for that place (225.39°) puts it facing the next tower.
- **Height:** the new block is 21 m tall, where the block it replaces is 30 m.

**Pictures (2560×1440, reviewed by eye, not golden).** These are in `Evidence/local/building-{without,with,turn135}/`. Views can now come from a JSON file on disk (`-dad-shot <path>.json`), so camera spots can be tried without rebuilding the player.

**Cost** (A-B-B-A on central-avenue-street, no vsync, 2560×1440 window, this Mac, default quality; `Evidence/local/bench-trial-*`):

| Run | Texture memory | Graphics memory (peak) | Mesh memory (peak) | Triangles drawn (mean) | GPU ms (mean) |
|---|---|---|---|---|---|
| A, city as it is | 491.8 MB | 769.4 MB | 148.6 MB | 4,671,349 | 39.72 |
| B, with the building | 667.8 MB | 950.2 MB | 162.8 MB | 4,794,280 | 42.01 |
| B, with the building | 667.8 MB | 954.5 MB | 165.0 MB | 4,767,621 | 41.78 |
| A, city as it is | 491.8 MB | 765.2 MB | 149.8 MB | 4,668,844 | 49.96 |

**What the numbers show**
- **Texture memory:** this one building adds **176 MB**, a 36% increase over the whole city's 491.8 MB. That matches its maps at full size: 15 compressed maps, of which 6 are 4096².
- **Mesh memory:** it adds about 15 MB.
- **Triangles:** about 100,000 to 125,000 more on average along the path. The building is in view for only part of the path.
- **GPU time:** the two A runs differ by 10 ms (39.7 against 50.0), so the GPU cost of one building is smaller than this Mac's run-to-run drift. **It has not been measured.**
- **Instances:** copies of the same model would share their textures. Each new model adds its own.

**Open before any swap across the map**
1. **Texture budget per building model.** At full size, 4 to 7 distinct models would add 0.7 to 1.2 GB. Medium and Low already draw one mip down, about a quarter of the memory.
2. **LODs.** Make lighter levels from the source and measure them.
3. **Windows.** They draw as flat grey: the creator's glass is see-through, and there is no interior behind it.
4. **Turn per model.** The city's turn suits its own models, not these.
5. **Height.** The new block is lower than the one it replaces.
