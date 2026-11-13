# D-06b: roofs and materials

The second part of D-06 as re-scoped by the owner on 2026-11-03 ([ENVIRONMENT_ROADMAP.md](ENVIRONMENT_ROADMAP.md), after [D06A_CLASSIFICATION_FAR_TIER.md](D06A_CLASSIFICATION_FAR_TIER.md)): each building's walls and roof get a material family, flat roofs get parapets and a rooftop kit (stair rooms, water tanks, satellite dishes, air-conditioner condensers). Footprints, heights, roads, rails, piers and chunk boundaries are unchanged; no texture was added. Near facades in geometry are D-06c.

## Materials (`Runtime/World/BuildingSkin.cs`)

Every building gets a skin: wall material and tone, roof material and tone, whether it has a parapet and a rooftop kit. It depends on the building's id, tags, category (D-06a) and placement only, so every piece of a building cut by the chunk grid gets the same one.

**Walls**, in order:

| Step | Source label | Rule |
|---|---|---|
| 1 | `Tag` | `building:material` from the data: brick, masonry, exposed brick, brick;tin → brick; plaster → plaster; tin, tinshed, tin shed, metal → corrugated sheet; concrete, cement_block, stone → concrete; glass → glass. `mixed`, bamboo and wood fall to the rule |
| 2 | `Rule` | By category, picked by a hash of the id: homes and mixed use plaster 60 %, paint 25 %, bare brick 15 %; commercial of 20 m and more glass 35 %, concrete 40 %, paint 25 %, lower ones concrete 60 %, paint 40 %; industrial concrete 50 %, sheet 30 %, brick 20 %; institutional brick 40 %, plaster 40 %, paint 20 %; informal sheet 80 %, brick 20 %; special concrete; religious plaster |

M2's 76 facade buildings stay plaster, as built. **City-wide** (census, 494,978 buildings): plaster 57.3 %, paint 23.5 %, brick 16.2 %, sheet 2.5 %, concrete 0.5 %, glass 56 buildings; 3.4 % from the tag, 96.6 % by the rule (K-46).

**Roofs** are all drawn flat: `roof:shape` is on 169 buildings city-wide and is not read. A sheet-walled or informal building has a corrugated sheet roof, and so do six industrial buildings in ten; the others are a concrete slab (60 %), white waterproofing (12 %), tar (13 %) or mosaic tiles (15 %), by hash. City-wide: slab or tar 70.8 %, mosaic 14.5 %, white coat 11.6 %, sheet 3.1 %.

Colours are this port's choices (sRGB albedo): six paint tones, three brick, three concrete, four sheet, two glass, three slab tones and one each for the white coat, the tar and the parapet's coping. They use the existing material layers (Plaster, Paint, Brick, Concrete, Glass, RoofSheet, Roof, Tiles); no texture was made or downloaded.

## Parapets (`BuildingBuilder.Parapet`)

Walk-ups, shop-houses, commercial and institutional buildings on the ground, 5 m or more tall, with a slab roof, have a parapet: 95.2 % of buildings city-wide. The walls still rise to the height the data gives; the roof is drawn `ParapetHeight` (0.9 m) below it. Around every ring, courtyards included, a face `ParapetThickness` (0.15 m) inside the wall turns to the roof, mitred at the corners (at most 3 × the thickness at sharp ones), with a concrete coping between it and the wall's top. Not drawn: edges cut along the chunk border (the neighbouring piece's roof continues there) and edges whose inner edge would fold back or be under 5 cm (slivers and short jogs left by clipping), counted. City-wide 2,070,734 parapet pieces.

The far tier now divides the wall into floors up to the roof, not the top, and the builder writes the floors below the parapet into w (8192 × floors). The shader draws the wall above them plain, with the roof's slab line at its foot, so no window is painted on a parapet.

## The rooftop kit (`Runtime/World/Rooftops.cs`, `StreetKit`)

Placed on the roofs of buildings with a parapet (or M2's facades) and 6 m or more, by labelled rules (no rooftop is mapped in the data):

| Item | Rule | Near | Far |
|---|---|---|---|
| Stair room | Walk-ups, shop-houses and institutions of 3 floors or more and 60 m² or more, nine in ten by hash; 3.4 × 2.8 m, 2.7 m tall, in the building's wall material, with a slab and a steel door | 36 triangles | box, 12 |
| Lift room | Instead, on commercial buildings of 6 floors or more and 150 m² or more; 4.2 × 3.2 m, 3.2 m tall | as stair room | box |
| Water tank | 1 + one per 150 m², at most 4; radius 0.55 or 0.68 m; black, blue or white plastic or steel, no maker's marks; the first stands on the stair room's roof six times in ten | 68 triangles, on a steel stand, with its lid | 6-sided, 20 |
| Satellite dish | 35 % of walk-ups and shop-houses; faces azimuth 160°, 55° up (south-south-east, the region's satellite arc) | 100 triangles | not drawn |
| Condenser | Commercial one per 80 m² (at most 12), institutions one per 120 m² (at most 6), shop-houses 1–4, homes 0–2 | 52 triangles, frame and fan grille | box, 12 |

Each item is put at hashed points in the roof's frame (along its longest outer edge), wholly inside the roof, at least 0.5 m from every ring edge (clear of the parapet and out of courtyards) and 0.3 m from the items already placed; one that finds no room in 24 tries is left out and counted. Each piece of a building cut by the chunk grid is a roof of its own (K-16).

**Not instanced.** The roadmap row says "instanced rooftop kit". The kit is merged geometry in the street cells instead, like every other kit in this port: the streamer's 125 m cells already draw it near (to 150 m) or far (to 450 m) by distance, in the cells' existing meshes, so it adds no batch. GPU instancing would add a draw per item type per cell and needs a second path in the streamer. The cost is mesh memory (K-45, below).

**Since K-45 approach 4** (prototyped on branch `proto/k45-near-kit`, adopted into main 2026-11-03 on the owner's approval; [AXIOM_V3_AUDIT.md](AXIOM_V3_AUDIT.md) sections N and O): the near kit is no longer in the near cell meshes. Each cell keeps its kit items and its near kit is built on a worker when the cell comes within 250 m, uploaded as one more mesh for the cell (a batch more per near cell with a kit) and dropped beyond 350 m. The far kit stays in the far cell meshes. The geometry drawn is the same, triangle for triangle.

**City-wide**: 466,687 roofs furnished; 274,780 stair rooms, 224 lift rooms, 630,668 tanks, 160,920 dishes, 362,125 condensers; 5,251 items found no room. **Slice** (12 chunks): 5,629 roofs; 4,062 stair rooms, 32 lift rooms, 9,184 tanks, 1,975 dishes, 5,017 condensers; 38 with no room.

## Cost

| Measure | Result |
|---|---|
| Texture memory | 16.33 MB on the GPU (`MaterialLibraryTests`), unchanged: no texture added. Within 24 MB |
| Slice street meshes | 713,218 → 1,640,974 vertices (census, the same sources with and without the kit). The kit's near ranges hold 744,660 vertices and 1,230,280 triangles, its far ranges 183,096 and 293,012. At 16 bytes a vertex and 4 an index, about 33 MB more mesh for the 12 chunks (K-45) |
| Build time | `ChunkRoads.Build` for every chunk of the city, one thread, Mono outside Unity: 34.4 s without the kit, 43.7 s with it (7 ms a chunk); the 12 slice chunks' roads and street 885 ms in the editor (`TheSliceRooftopsAreFurnished`). Buildings with parapets: 2.9 s for every chunk (D-06a 1.7 s; same run conditions not repeated) |
| Batches, GPU, memory | Development player, Apple M1, Metal, 1280x720, the same paths as D-06a. **Air** (`slice-crossing-air`, 3 runs): GPU p50 3.28 / 3.05 / 3.15 ms (D-06a 3.36 / 3.27 / 3.27), p95 9.91–10.43 ms; batches p50 28 (as D-06a), p95 137–142 (129–132); triangles p50 327,000–345,000 (199,000–203,000), p95 1.97 M (1.43 M); apply frames at most 2.17–2.36 ms (2.39–2.75), longest apply step 0.93 ms; garbage 143.1–149.8 MB a run (91.4–95.2 MB; not traced), none in an apply step; system memory peak 680–686 MB (570–574 MB); frames over 20 ms 2 / 1 / 1. **Street** (`kawran-farmgate-street`, 2 runs): GPU p50 8.04 / 8.08 ms (8.20 / 8.33), p95 12.35 / 12.33 ms; **batches p50 117 / 118 (104)**, because cells that held no furniture now hold rooftop kit and draw; triangles p50 1.80 M (1.44 M); main thread p50 2.35 / 2.36 ms (1.90 / 1.91); apply frames at most 2.89 / 3.10 ms (2.15 / 2.73), longest step 1.79 ms; system memory peak 887 / 834 MB (695 / 699 MB); one frame over 20 ms each (0) |

## Evidence

| Check | Result |
|---|---|
| EditMode tests | 111 / 111 (104 before, plus `RoofTests`, 7) |
| PlayMode tests | 25 / 25 |
| New tests (`RoofTests`, 7) | Skins follow the tag, then the rule's shares (60 / 25 / 15 % for homes within 2 %), the same for every piece; a parapet keeps the top, lowers the roof 0.9 m, the roof covers the footprint, the coping is the ring between wall and face, the faces stand 0.15 m inside and turn to the roof, around a courtyard too, none on the chunk border, none on low, religious or tiny buildings; the far tier's floors stop at the roof and w carries them; the shader reads them from the same bit; the kit's rules (lift room, condenser and tank counts, none on sheds or low houses), every kit vertex on its roof, clear of the parapet and not over a courtyard, the same every build, the far ranges the near kit's boxes and tubes without dishes (exact vertex count); dishes face south-south-east and up; the slice's roofs furnished, every item on a kit roof; every kit part a closed solid facing out, within its triangle target |
| Updated tests | `BuildingClassTests` (floors to the roof; parapet vertices carry no facade code), `BuildingBuilderTests` (roof area at the roof height; every parapet face turns to its roof, 2 within 0.5 m of the chunk border counted, not judged), `WorldMaterialTests` (walls in their skin's material, flat faces the skin's roof or concrete; brick, sheet and tiles join the building set, plaster and brick the street set), `ChunkStreamerTests` (coping corners face up) |
| Mutations | 8 of 8 caught: parapet offset outward (`AParapetKeepsTheTopAndLowersTheRoof`, `FarmgateWallsFaceOutAndRoofsUp`); roof left at the top (5 tests); kit placed without the roof check (`TheRooftopKitStandsOnItsRoofs`, `TheSliceRooftopsAreFurnished`); brick tag read as plaster (`SkinsFollowTheTagThenTheRules`); shader reading the floors from the wrong bit (`TheShaderReadsTheParapetFloors`); dishes drawn far (`TheRooftopKitStandsOnItsRoofs`); parapet floors not written (`AParapetKeepsTheTopAndLowersTheRoof`); folded parapet edges kept (`FarmgateWallsFaceOutAndRoofsUp`) |
| Corner and height checks | Passing (`TwentySliceBuildingsSitOnTheirData`): no footprint corner or top moved |
| Goldens | 4 new: `d06b-air` (45 m over a walk-up district), `d06b-roof-close` (24 m, parapets, stair rooms, tanks, dishes), `d06b-viaduct` (from the deck of the 28.5 m viaduct by Karwan Bazar, at a train window's height), `d06b-air-far` (120 m, the far tier and the kit's far boxes). 28 re-accepted after looking at each beside its old golden (wall materials and roofs changed everywhere; no position moved); 4 unchanged within tolerance (`k37-kni-kerb`, `m2-footbridge`, `m2-shopfront`, `m2-street-poles`). Tall commercial buildings drawn in glass read as dark curtain walls with faint windows (`kawran-metro`, `tod-noon`) |
| Windows build | Succeeded, Mono, 138,385,807 bytes (132.0 MiB), 0 errors, 20 warnings, 47.0 s. Not run: no Windows machine (K-02) |

All shots are Unity development-player renders (Apple M1, Metal).

## Not done here, and why

- **Roof shapes.** All roofs are flat; the 169 tagged roof shapes are not drawn.
- **Materials are mostly inferred** (K-46): 96.6 % of wall materials are by rule. No photo reference was used for the shares; they are this port's estimate and are labelled `Rule` in the census.
- **Kit memory** (K-45): the kit is merged, so every chunk holds its near and far geometry whether shown or not; system memory peaks rose 110–190 MB on the bench paths, and the street path draws 13 more batches. Since 2026-11-03 drawn meshes drop their CPU copies ([AXIOM_V3_AUDIT.md](AXIOM_V3_AUDIT.md) section M), which takes back 20–31 MB at run end. Since approach 4 was adopted (sections N and O there), the near kit is built only for cells within 250 m: end memory air 524–528 MB and street 609–617 MB (before both: 597–599 and 708–710), street managed heap 146–164 MB (200–201). The far kit and the extra batches remain.
- **Garbage** rose about 55 MB a run on the air path (not traced; none in an apply step), on top of D-06a's untraced rise.
- **Pumps, pipes, antennas, solar panels, railings, rooftop gardens, laundry lines** are not in the kit (BD-ROF rows stay open).
- **Window light, signs on roofs:** night systems (D-13) and signage (D-09).
