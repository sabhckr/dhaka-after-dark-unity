# AXIOM directive v3.0: first-action audit

This is the audit the owner's directive v3.0 (2026-11-03, Part 0 and Part 23) asks for before any new production work. It was done on 2026-11-03 at `main` 35e0e88 (D-06b).

**No production system was changed.** These were experiments only: each ran in the player, was measured and was then reverted.
- Releasing mesh CPU copies (two variants).
- The rooftop kit switched off.

The one code change kept is a measuring instrument. The bench now also records mesh memory and the managed heap (`FrameRecorder`: `meshMemory*`, `gcReserved*`).

Nothing was pushed, downloaded or deleted. Every figure below was measured today unless it is marked *reported*. Files: [AXIOM_V3/](AXIOM_V3/).

## A. Current verified project state

Re-checked today:

| Item | State |
|---|---|
| Repository | `main` 35e0e88 (D-06b); no remote branches; nothing pushed. The tree was clean before this audit; now `FrameRecorder.cs` (bench counters) and these docs |
| Tests | EditMode and PlayMode re-run today: see section L |
| Metro piers | `Tools/k37/run.sh` re-run on the current code: register and groups **byte-identical** to the post-P-1 register. 67 pier places: 66 built, 1 junction spanned. **44 in conflict, 22 clear** (section F) |
| Road flyovers | 27 elevated vehicle ways in the slice, 10.06 km (section E). All are flat decks with **no supports of any kind** in the code (`RoadBuilder`; `Structures.cs` builds piers only for the rail viaduct and columns for footbridges) |
| Collision | **None.** No collider, rigidbody or character controller in `Runtime`. No project layers beyond Unity's defaults. No player character and no vehicle: the free camera is the only movement |
| Storage | Unity project 5.5 GB: Library 2.3 GB, `Evidence/local` 2.6 GB, Builds 0.35 GB. Allocation in use about 18.8 GB of 25 GB, the same as the D-06b record. The disk has 50 GB free |
| Bench player | macOS development player built 4 times today (Mono, 0 errors) |
| Memory pressure | During today's runs the Mac had 1.9 GB of swap in use, with the Claude app (0.7 GB), Chrome and WhatsApp open |
| Bench validity | Two runs were killed by the 180 s watchdog while the lid was closed. The player sat at 0.2 % CPU, waiting on a sleeping display. They are discarded. All later runs held the display awake (`caffeinate -dimsu`) |

## B. Reported but not revalidated today

These come from earlier milestone records and were not re-measured in this audit:

- **D-06b:**
  - 36 goldens.
  - Windows build: 138,385,807 bytes, 0 errors. Built, never run, since there is no Windows machine (K-02).
  - City-wide census: 494,978 buildings; kit counts 466,687 roofs and 630,668 tanks; material shares.
  - D-06b bench figures in [D06B_ROOFS_MATERIALS.md](D06B_ROOFS_MATERIALS.md).
- **D-06a:** classification shares and the far-tier figures.
- **Phase C:** closed by the owner on 2026-11-02.
- **The directive's "66 / 52 / 14" metro figures.** These are the M2 baseline and are superseded:
  - The 14 "in a building" were always among the 52 (K-37, V-1).
  - After P-1 there are 44 conflicts, revalidated today (section F).

## C. K-45: rooftop kit memory and garbage, investigation

### What the kit costs (measured, kit on against kit off)

All runs used the development player on an Apple M1 at 1280×720. Each figure is the range over 2 runs. The first run after each build is left out. All runs are listed in [k45-bench-variants.txt](AXIOM_V3/k45-bench-variants.txt).

| End of run | Street, kit on | Street, kit off | Kit share | Air, kit on | Air, kit off | Kit share |
|---|---|---|---|---|---|---|
| System memory | 708–710 MB | 603–606 MB | **+104 MB** | 597–599 MB | 528–531 MB | **+69 MB** |
| of which managed heap (GC reserved) | 200–201 MB | 132 MB | **+68 MB** | 116–117 MB | 81–82 MB | +35 MB |
| of which mesh memory (CPU side) | 127 MB | 89 MB | **+37 MB** | 17 MB | 11 MB | +6 MB |
| Graphics memory | 142 MB | 136 MB | +6 MB | 103 MB | 101 MB | +2 MB |
| System memory peak | 848–851 MB | 699–753 MB | +100–150 MB | 652–653 MB | 573–577 MB | +77 MB |
| Garbage per run | 0 (street loads before recording) | 0 | — | 136–137 MB | 93–98 MB | +40–43 MB |
| Batches p50 | 115–118 | 102–105 | +13 | 28–29 | 28 | 0 |
| GPU p50 | 7.90–8.07 ms | 7.67–8.20 ms | within noise | 3.17–3.22 ms | 3.06–3.13 ms | +0.1 ms |

**The largest share is the managed heap, not the meshes.** The street builder's pooled arrays are rented at power-of-two sizes. For the 12 slice chunks they hold 79.3 MiB for 54.7 MiB of data, against 32.9 MiB with the kit off. On the street path all 12 chunks are in flight together at the start. The pool keeps those arrays afterwards, and Mono does not hand reserved heap back.

Each of the 3 worker threads' reusable lists also grows to the largest chunk: 242,318 street vertices and 1,075,086 indices with the kit, 163,498 and 686,406 without.

Mesh memory is the streamer's readable CPU copy of every mesh. The GPU copy is counted separately under graphics memory.

### Where the extra garbage comes from (traced)

- **Builders, warm.** With the kit on or off, building roads, street and buildings for the 12 chunks allocates the same 2.7 MiB + 0.4 MiB. The kit adds no garbage per chunk once the pools are warm. Measured in the Mono harness: [k45-garbage-trace.txt](AXIOM_V3/k45-garbage-trace.txt), probe source [K45Probe.cs.txt](AXIOM_V3/K45Probe.cs.txt).
- **Builders, cold.** The first pass allocates 55.1 MiB with the kit, against 32.2 MiB without. This is the pools and lists growing to the larger street arrays, once per thread.
- **In the player, all garbage falls in the first third of the air run.** It is zero in the second half, although streaming goes on: [k45-garbage-by-time.txt](AXIOM_V3/k45-garbage-by-time.txt).

  | Run | Garbage by sixth of the run (MiB) |
  |---|---|
  | D-06a | 51 / 37 / 3 / 0 / 0 / 0 |
  | D-06b | 104 / 27 / 6 / 0 / 0 / 0 |
  | Kit off | 58 / 33 / 3 / 0 / 0 / 0 |

So the "+55 MB garbage a run" in K-45 is **one-time growth when a session starts**, not churn. No apply step met a collection in any run. Collections went from 30 to 32–35 a run.

### Approaches compared

| # | Approach | Status | Result |
|---|---|---|---|
| 1 | **Merged meshes, readable** (D-06b as committed) | Measured | The baseline above |
| 2 | **Release every mesh's CPU copy** 3 frames after it is made (`UploadMeshData(true)`) | Measured, reverted | **Air:** end memory 562–563 MB (−36 MB). **Street:** no gain, 713–721 MB, because mesh memory fell 37 MB but graphics memory rose 52 MB. Releasing a mesh uploads it, so meshes that are never drawn now take GPU memory: hidden tiers (the near kit of far cells) and debug overlays. Uploading at creation instead cost 1.3–1.8 ms per apply step in the editor, too close to the 4 ms frame rule |
| 3 | **Release only meshes that have been drawn** (`renderer.isVisible`; hidden ones keep their copy until shown) | Measured, reverted; patch kept: [k45-release-drawn-meshes.experiment.patch](AXIOM_V3/k45-release-drawn-meshes.experiment.patch) | End memory: **street 678–683 MB (−27 to −32 MB)**, **air 561 MB (−37 MB)**. Peak air 615–616 MB (−37 MB). Graphics memory unchanged. Each release took at most 0.12 ms, at most 0.43 ms in a frame, and 1–3 ms a run in all. Batches, GPU and triangles unchanged. Street main-thread CPU p50 rose by 0.2–0.3 ms (2.57–2.70 against 2.37–2.38 ms). This is likely the per-frame visibility scan over 348 meshes still readable; it is not proven, and air-run noise is ±0.5 ms. A final version would scan a few meshes a frame. It needs one test change: `ChunkStreamerTests` reads meshes back, so a `KeepMeshesReadable` switch for tests, plus a test that streamed meshes keep no copy |
| 4 | **Near-detail generation**: build a cell's near kit only when the cell comes within reach, keep the far boxes merged | Not built; **estimate from measured parts** | The near kit is 80 % of the kit's vertices and indices (744,660 of 927,756 vertices). Taking it out of the chunk payload would remove most of the +68 MB heap and +37 MB mesh share on the street path: **estimated −50 to −80 MB at the end of the street run**, overlapping with approach 3. Generation cost: the kit adds about 11 ms a chunk to the road build on one worker thread (Mono harness, warm: 53.0 against 42.4 ms a chunk), so about 1 ms a cell, off the main thread. Risks: pop-in if a cell's kit is late (build at about 200 m, show at 150 m), a second job type in the streamer, and the kit's placement data kept per chunk (about 20,000 items in the slice, about 1.6 MB). Needs a prototype before any number is claimed |
| 5 | **GPU instancing** | Not built; **analytic only** | 20,270 kit items in the slice. Instance data at about 80 bytes each is about 1.6 MB, against about 33 MB of merged kit mesh, so memory would shrink most. But `Dad/World` has no instancing path today (no `multi_compile_instancing`, no per-instance colour), so it needs shader work plus a second draw path in the streamer, near and far. Batches rise with every kit part type in every visible cell, and again in the shadow pass, where today's merged cells add 13. Not measurable without a prototype of 2–3 days |
| 6 | **Simplified far representation** | Already in D-06b | The far kit is boxes and 6-sided tanks, with no dishes: 183,096 vertices against 744,660 near |

### Recommendation

1. **Apply approach 3**, with the bounded scan and its tests. It is generic: every streamed mesh benefits, not only the kit. It is cheap, and its gain is measured (−27 to −37 MB at run end).
2. **Prototype approach 4 and measure it** against approach 1 and 3 on the same benches. It is the only one that reaches the heap, the largest share. Adopt it only if the measurement holds and no pop-in shows in the near goldens.
3. **Do not build instancing now.** It would cut kit memory most, but it adds a second rendering path and more batches, and approach 4 targets the same memory with the existing path. Revisit it if D-07 to D-10 bring many repeated props (trees, stalls) where instancing pays more.
4. Afterwards, consider finer pool buckets. Power-of-two rounding holds 79.3 MiB for 54.7 MiB of street data. This is not yet measured in the player.

## D. K-46: material classification confidence

| Source of a building's wall material | Slice (5,642) | City (494,978; category shares overlap the tag row) | Confidence |
|---|---|---|---|
| `building:material` tag | **4 (0.07 %)** | 18,868 (3.8 %) | High: mapped, unverified |
| Rule on a category from its own OSM type | 412 (7.3 %) | category: 6.7 % | Medium: the category is mapped, the material share is an estimate |
| Rule on a category from the landuse it stands in | 1 | category: 7.0 % | Low to medium |
| Rule on a category that is itself a rule (walk-up default, split piece, main road, size) | **5,225 (92.6 %)** | category: 86.2 % (K-44) | Low: two inferences stacked |

- The rule's shares are not from any source; they are this port's estimate:
  - homes: plaster 60 %, paint 25 %, brick 15 %;
  - commercial of 20 m and more: glass 35 %, concrete 40 %, paint 25 %.
- No photo reference was used.
- Result in the slice: plaster 3,263, paint 1,452, brick 812, concrete 70, sheet 33, glass 12.

Recommendations:
- **Census.** Add a `d06b_wall_confidence` column (High / Medium / Low, as above) beside `d06b_wall_source`, so no inferred material reads as fact. This is a census and documentation change only, with no visual change.
- **Owner review.** Either the owner reviews the shares per area against their own photos, or the shares stay labelled as estimates.

No reference will be downloaded without approval.

## E. Flyover inventory and known defects (slice)

**Ways.** 27 elevated vehicle ways, 10.06 km in the slice; the full list is in [flyovers-slice.txt](AXIOM_V3/flyovers-slice.txt). All widths are estimated by class.

| Kind (OSM) | Ways | Names in the data |
|---|---|---|
| `construction`, layer 1–3 | 10 | Dhaka Elevated Expressway (2 named) |
| `motorway` / `motorway_link`, layer 1–2 | 2 / 4 | Dhaka Elevated Expressway |
| `primary`, layer 1–3 | 5 | Hatirjheel-Karwanbazar Flyover, Shahid Tajuddin Ahmad Avenue flyover (2 ways), Moghbazar-Mouchak Flyover, Malibagh Mouchak Flyover |
| `service`, `residential`, `tertiary`, layer 1 | 6 | Kutubbag goli (1 named) |

**Verified defects,** from code and data:
1. **No supports.** No pier, column or pier cap under any road deck. So "pillar in a lane" is zero by absence, not by design.
2. **No ramps.** Decks sit flat at the layer height (6, 11 or 16 m) and end in the air where they meet a road at grade (K-13). Ramps need an elevation decision (K-05).
3. **No closed structure.** Decks are open strips: no girders or underside, and no shadows (K-20).
4. **Decks pass through buildings.** Along the deck centre lines, sampled every 2 m, decks run inside the drawn volume of buildings in 8 way–building pairs. This is a lower bound, because deck width is not counted. Most are the expressway works under construction (ways 1568247194, 1568247191 and 1249807413, 2–20 m each through 9 m buildings); one is the Kutubbag goli service bridge through a 12 m commercial building. Details: [flyover-deck-building-clashes.txt](AXIOM_V3/flyover-deck-building-clashes.txt).
5. **No collision and no navigation**, as for everything else (section A).

**Decision needed.** 10 of the 27 ways are tagged `construction` in the data. Should they be drawn as finished flyovers, as works in progress, or left out of the playable area?

## F. Metro Line 6 pier inventory and known defects

Re-run today on the current code; identical to the post-P-1 register ([K37_PIER_INVESTIGATION.md](K37_PIER_INVESTIGATION.md) section 9, register [K37/after-p1](K37/after-p1/)).

| Group | Cause | Piers |
|---|---|---|
| A′ | Clears only if lanes are 3.0–3.3 m wide | 4 |
| B | Pier rule ignores side roads (V-4) | 2 |
| C | The mapped station stands over the carriageway | 12 |
| D | A building footprint reaches into the carriageway | 2 |
| E | Rail and road lines disagree; the real pier positions are needed | 24 |
| Clear | | 22 (8 of them since P-1) |
| Junction place, spanned | | 1 |

Known code defects, not fixed:
- V-4: the rule ignores side roads.
- V-5: a duplicate pier 3.6 m away at the c_6 border (K37-61).
- V-6: a 17.7 m span at the bend.
- V-8: counter wording.

All four are in **P-2, which is not approved**. Stations float 3 m below the track with no piers (K-15).

**Detailing missing** against the directive's Part 7 list:
- track beds beyond plinths;
- expansion joints;
- drainage, lighting and conduits;
- maintenance access;
- weathering.

The deck has a box girder, wings, parapets, plinths and rails; piers are round columns with caps.

## G. Infrastructure placement validation strategy

1. **One checker for both systems.** Generalise `Tools/k37` into `Tools/infra`: one read-only survey for metro piers, footbridge columns and future flyover supports. Every support gets a stable ID and a register row with the directive's Part 6.1 fields: coordinates, structure and segment, road relation, building and pedestrian clearance, status, source and confidence.
2. **Checks that can run on today's data:**
   - support against carriageways (centre line ± drawn width) and junction pads;
   - support against building footprints;
   - support against sidewalks, crossings and bus-stop pads;
   - support to deck and support to ground (top and foot heights);
   - duplicate and overlapping supports, including across chunk borders;
   - deck against building volumes (as in section E);
   - spans too long or too short;
   - a missing support where a span exceeds the girder's limit.
3. **Wired into tests.** An EditMode test fails on any new unlisted violation, so no placement mistake can come back silently.
4. **Checks that cannot run yet.** Navigation, collision and "actual driving tests" (Part 8, acceptance 5 and 8) need a drivable vehicle, colliders and a lane graph user (Phase E/F). Until then the infrastructure can be accepted on spatial checks only, and must be labelled that way.
5. **Lane polygons do not exist.** Carriageways are centre lines with estimated widths (K-31), so "lane" checks are carriageway checks. Lane-level checks need lane polygons from the lanes tag, an engineering change for approval.

## H. Performance risks

- **8 GB RAM.** The street bench peaks at 850 MB of system memory. The Mac swapped today with ordinary apps open. NPCs, vehicles, lights and rain all add to this.
- **Managed heap.** 200 MB on the street path, which does not shrink (section C).
- **Streaming bursts.** Every chunk in flight at once holds its pooled arrays. More detail per chunk raises the heap in step.
- **GPU.** Stochastic texture sampling costs 2–3 ms (K-22). The street path's GPU p50 is about 8 ms on the M1, before night lights (D-13) and wet surfaces (D-12).
- **Batches.** The street path draws 115–118 batches. Instanced trees, signs and stalls, or a second kit path, would add more.
- **Windows.** No tier is measured on Windows hardware (K-02).
- **Bench conditions.** Main-thread figures vary ±0.5 ms between identical air runs. A closed lid stalls the player, so benches must hold the display awake.

## I. Missing assets and systems

Asset inventory, 890 rows:

| Status | Rows |
|---|---|
| Not started | 806 |
| In progress | 37 |
| Implemented | 11 |
| Tested | 34 |
| Blocked | 2 |

Missing entirely:
- **Physics and collision layers.** No colliders, no collision matrix.
- **Player and vehicles.** No player character, no vehicle, no CNG.
- **City life.** No NPCs, no traffic AI.
- **The playable boundary** (Part 15).
- **Commerce.** No shop or business register (Part 4): the inventory's Shops rows are kit types, not real businesses. Only blank signboards exist (K-41).
- **Rooftop kit:** pumps, pipes, antennas, solar panels and railings.
- **Flyover structure.**
- **Night lighting** (D-13) and rain (D-12).
- **Audio.**
- **Interiors.**

## J. Proposed sequence (reconciled with the roadmap)

**Numbering conflict.** The directive's "D-07 NPC" and "D-08 vehicles" do not match the project's milestones:
- in [ENVIRONMENT_ROADMAP.md](ENVIRONMENT_ROADMAP.md), D-07 is trees and D-08 is street furniture and utilities;
- in [ROADMAP.md](ROADMAP.md), player and vehicles are **Phase E** and pedestrians and traffic are **Phase F**.

Nothing has been renamed. The proposal keeps the existing numbers:

| Step | Work | Existing id |
|---|---|---|
| 1 | K-45: approach 3, then the approach 4 prototype measured | K-45 |
| 2 | K-46: confidence column in the census; owner review of shares | K-46 |
| 3 | Near facades | D-06c |
| 4 | D-06 acceptance: goldens, bench, census | D-06 |
| 5 | Infrastructure checker (section G). **Done 2026-11-04** on the owner's "Build the checker": [INFRA_CHECKER.md](INFRA_CHECKER.md), spatial checks only, nothing moved; K-50 opened | New: proposed under K-37 / D-11 |
| 6 | Metro pier corrections, as approved (P-2, then evidence for groups C, D and E). **P-2 done 2026-11-04** with K-50 a, b on the owner's "Approve P-2 with K-50": [P2_PIER_RULES.md](P2_PIER_RULES.md); K-51 opened; evidence for C, D and E still open | K-37 |
| 7 | Flyover supports and closed decks, with shadows; ramps after the elevation decision. **Approved to start 2026-11-04** with the world transformation brief ([WORLD_TRANSFORMATION_PLAN.md](WORLD_TRANSFORMATION_PLAN.md)). **Done 2026-11-04 as D-11a** ([D11A_FLYOVER_SUPPORTS.md](D11A_FLYOVER_SUPPORTS.md)): supports and closed girders; no-supports 27 → 0 in the slice; residuals K-53, headroom K-52; ramps still wait for K-05 | D-11 extended (RD-ELV rows); K-05, K-13, K-20 |
| 8 | Playable boundary with the exact Bengali warning, data-driven segments | New: proposed id needed |
| 9 | Trees, street furniture, signage, stalls, rain, night: as the roadmap orders them | D-07 to D-13 |
| 10 | Player, the CNG prototype first, collision layers and matrix | Phase E |
| 11 | Pedestrians and traffic | Phase F |

## K. Approval required before implementation

1. K-45: apply approach 3 (release drawn meshes' CPU copies, with a bounded scan and the test switch)?
2. K-45: build the approach 4 prototype (near kit generated per cell) for measurement? **Approved and built** on branch `proto/k45-near-kit` (section N), then **adopted into main** with the kit scratch pre-sized, on the owner's "Adopt + pre-size scratch" (section O).
3. K-45: confirm instancing is not pursued now.
4. K-46: add the confidence column? Review the shares yourself, or keep them as labelled estimates?
5. Flyovers: how should the 10 `construction` ways be shown? And do supports for road flyovers join D-11 (RD-ELV)? **Decided 2026-11-04:** finished flyovers with their clashes listed; supports built as D-11a.
6. Metro: P-2 (pier rule fixes, along the deck only)? **Approved and applied 2026-11-04** (with K-50 a, b; [P2_PIER_RULES.md](P2_PIER_RULES.md)). The evidence source for groups C, D and E is still open (K-37).
7. Numbering: keep D-07 and D-08 as trees and street furniture, and run NPCs and vehicles as Phases F and E?
8. Boundary: which milestone id, and when?
9. Then: start D-06c?

## L. Test and build evidence for this audit

**Tests and builds**

| Check | Result |
|---|---|
| EditMode | 111 / 111 (`tests-EditMode-20261103-202410`) |
| PlayMode | 25 / 25 (`tests-PlayMode-20261103-202611`); streaming walk max apply frame 2.13 ms, max step 0.66 ms |
| macOS bench player | 4 builds, each Mono, 0 errors, 1 warning |

The suites ran on the tree as committed with this audit: D-06b plus the two bench counters. The experiments were reverted before the runs.

**Bench runs**

| Variant | Runs |
|---|---|
| A, merged and readable | air `194520`, `194731`; street `194626`, `194837` |
| B, kit off | air `201853`, `202105`; street `201959`, `202210` |
| C, release every mesh | air `195544`, `195756`; street `195650`, `195902` |
| D, release drawn meshes | air `201247`, `201459`; street `201353`, `201605` |

- Every run is under `Evidence/local/bench-*-20261103-*`.
- The first run after each build is left out: air `194412`, `195436`, `201746`.
- One baseline run (`194731`) had 44 main-thread frames over 4 ms where the others had 1–9. It is kept and shown as a range, not averaged away.
- The two runs that stalled with the lid closed are discarded: `200336`, `200636`.

**Not done in this audit**
- No goldens: nothing that renders changed.
- No Windows build: no runtime change.
- No mutation tests: the only code kept is a bench counter. A test of it would have to read profiler counters in the player.

## M. Applied after the owner's approval: approach 3 (2026-11-03)

The owner approved approach 3 ("Yes, apply it"). Approaches 4 and 5 and the other section K decisions are still open.

**What changed** (`ChunkStreamer`):
- Every streamed mesh is listed with its renderer when it is made.
- Each frame the streamer looks at up to `ReleaseScan` (32) listed meshes, going round the list. It drops the CPU copy (`UploadMeshData(true)`) of each one whose renderer was drawn (`isVisible`), until `ReleaseBudgetMs` (0.5 ms) is spent. The work runs under its own marker, `Dad.Streaming.Release`.
- Meshes never drawn keep their copies: hidden cell tiers and debug views not switched on.
- Meshes destroyed with their chunk leave the list.
- `KeepMeshesReadable` keeps every copy, for tests that read meshes back.
- The bench reports copies dropped, copies still held, and the slowest drop and frame of drops.
- The experiment's 3-frame wait after a mesh is made was removed: its mutation survived, because a mesh can only be visible once it has been drawn.

**Tests**

| Check | Result |
|---|---|
| New PlayMode test `DrawnMeshesDropTheirCopy` | The test draws its own camera, since batch-mode runs draw none. Buildings and ground in view drop their copies. Hidden cell tiers and debug views keep theirs. The grid, once switched on and drawn, drops its copy. Counts and bounds stay. The list empties when every chunk unloads. A frame of drops stays inside the frame rule |
| `FarmgateChunkMatchesItsData` | Uses `KeepMeshesReadable` and checks that nothing is listed |
| Suites | EditMode 111 / 111, PlayMode 26 / 26 |
| Mutations | 4 of 4 caught: release regardless of visibility; never release; the switch ignored; unloaded meshes kept on the list |

**Bench**

Development player, Apple M1, display held awake. The first run after the build (air `203830`) is left out. Run ids are under `Evidence/local/bench-*-20261103-*`.

| | Before (A, `194520` / `194731`; street `194626` / `194837`) | After |
|---|---|---|
| Air: system memory end | 597–599 MB | **568 MB** (3 runs: `203937`, `204148`, `204359`) |
| Air: system memory peak | 652–653 MB | **622 MB** |
| Air: mesh memory end / graphics end / managed heap | 17 / 103 / 116–117 MB | 12 / 103 / 116–117 MB |
| Street: system memory end | 708–710 MB | **683 / 688 MB** (2 runs: `204043`, `204254`) |
| Street: system memory peak | 848–851 MB | 773 / 830 MB |
| Street: mesh memory end / graphics end / managed heap | 127 / 142 / 200–201 MB | 90 / 142 / 202–203 MB |
| Copies dropped / still held at the end | — | air 202–203 / 64; street 112–113 / 348–349 |
| Slowest drop / slowest frame of drops | — | 0.21 ms / 0.26 ms |
| GPU p50 | air 3.17–3.22, street 7.90–8.07 ms | air 3.06 / 6.12 / 3.35, street 7.46 / 8.30 ms |
| Batches p50 / triangles p50 | air 28–29 / 322–344 k; street 115–118 / 1.81–1.83 M | air 28 / 331–349 k; street 117–118 / 1.80–1.81 M |
| Main thread p50 (CPU) | air 1.79–2.82 (2.02–2.57), street 2.31 (2.37–2.38) ms | air 2.13–2.43 (2.22–2.44), street 2.38–2.40 (2.46–2.48) ms |
| Frames over 4 ms | air 3 / 44, street 9 / 2 | air 2 / 0 / 0, street 0 / 0 |

Notes on the bench:
- **The 6.12 ms GPU p50** in air run `204148` is out of line with the other runs (3.06 and 3.35 ms) and with every earlier air run. Nothing in it draws more: batches and triangles are the same. It is kept and reported, not explained.
- **The street managed heap is unchanged** (approach 3 does not reach it). It remains the largest share of the kit's cost; approach 4 is the measured next step if approved.

**Other evidence**

| Check | Result |
|---|---|
| Goldens | All 36 rendered and compared: 36 match, none changed |
| Windows build | Succeeded: Mono, 138,388,367 bytes, 0 errors, 20 warnings, 13.0 s. Not run, since there is no Windows machine (K-02) |

## N. Prototype after the owner's approval: approach 4, the near kit built per cell (2026-11-03)

The owner approved building the approach 4 prototype for measurement ("Yes, prototype it"). It is on the local branch **`proto/k45-near-kit`**, not merged: main stays at `d6ec614` (approach 3) until the owner decides. Nothing pushed.

**What changed** (on the branch)
- `ChunkRoads` no longer writes the near rooftop kit into the near cell ranges. It keeps the kit's items by cell (`ChunkStreet.RoofKit`, from a pool, handed back when the chunk unloads). The far kit stays in the far ranges.
- `ChunkStreet.BuildNearKit(cell)` builds one cell's near kit from those items, on any thread (per-thread scratch, pooled output arrays).
- `ChunkStreamer`:
  - starts a cell's near-kit build on a worker once the cell is within 250 m (`KitBuildDistance`, the distance `StreetLod` measures). Cells drawn near go first; at most 4 build at once.
  - uploads at most 2 finished kits a frame, under `Dad.Streaming.Kit`, as one more mesh for the cell, drawn only with the cell's near tier;
  - destroys the kit beyond 350 m (`KitDropDistance`).
  - hands back, unused, a kit finished for a chunk already gone, and the chunk's kit items once nothing is building from them.
  - starts kits only in resident chunks and before the apply steps; the apply waits for kit builds as it waits for loads.
- Counters reach the bench: kits built, dropped and held; failures; the slowest upload and frame of uploads. Also **late frames**: frames in which a cell drawn near has no kit yet, which is the pop-in measure. They are counted from the streamer's start and, separately, during the sampled path.

**Is it the same picture?**
- The probe `Equiv.cs` hashed every cell's near and far triangles on main and on the branch. On the branch, near means the near range plus `BuildNearKit`.
- The 396 lines are byte-identical across the 12 slice chunks ([k45-approach4-equivalence.txt](AXIOM_V3/k45-approach4-equivalence.txt)).
- Goldens: all 36 rendered and compared, 36 match, none changed.

**Tests**

| Check | Result |
|---|---|
| `RoofTests.TheRooftopKitStandsOnItsRoofs` | Now checks the near kit per cell: every vertex on a roof, none in the near ranges |
| New `RoofTests.NearKitsAreBuiltPerCell` | Items kept in their foot's cell; kits within 3 m of the cell's square; 64 builds on 4 threads equal to one-thread builds; pooled arrays handed back |
| New `NearRooftopKitsComeAndGoWithDistance` | Kits held within 250 m and gone beyond 350 m; drawn with the near tier only; the builder's vertices; 16 held of 96 cells with a kit at Farmgate |
| New `KitsFinishingForUnloadedChunksAreHandedBack` | A chunk unloads while its kits build, 300 m away: kits handed back, not uploaded; the chunk's items handed back; the next chunk waits for the kits |
| New `CellsDrawnNearWithoutTheirKitAreCounted` | The late-frame counter counts and stops |
| `WalkAcrossTheSliceKeepsRingsAndBudget` | Also asserts no kit failures and kit uploads within 4 ms a frame |
| Suites | EditMode 112 / 112, PlayMode 29 / 29 |
| Mutations | 12 of 12 caught. Two survived on the first pass and the tests were tightened: the gone-chunk test now starts by the chunk's east edge, and a kit-items hand-back no longer zeroes the cell starts, so a premature hand-back fails loudly |

**A regression found and fixed on the way**
- The first version allocated the kit items fresh each load: +1.5 MiB garbage per 12 slice loads, 4.2 MiB against main's 2.7.
- It could also start kits in the middle of an apply. One test walk then had a collection inside an apply step: 5.31 ms, against the 4 ms rule.
- The fixes are the pool, starting kits only before the apply, and the apply waiting for kits. Per-load garbage is back to 2.7 MiB ([k45-approach4-garbage.txt](AXIOM_V3/k45-approach4-garbage.txt)).

**Bench**
- Development player, Apple M1, display held awake.
- Before = approach 3 runs on `d6ec614`. After = 4 air and 4 street runs; the first runs after each of the two builds are left out.
- All figures are from each run's `summary.json` ([k45-approach4-bench.txt](AXIOM_V3/k45-approach4-bench.txt)).

| | Before (approach 3) | After (approach 4 prototype) |
|---|---|---|
| Air: system memory end / peak | 568 / 622 MB | **525–530 / 575–580 MB** |
| Street: system memory end / peak | 683–688 / 773–830 MB | **617–626 / 712–767 MB** |
| Managed heap end (air / street) | 116–117 / 202–203 MB | 97–99 / 151–164 MB |
| Mesh memory end (air / street) | 12 / 90 MB | 8 / 67 MB |
| Graphics memory end (air / street) | 103 / 142 MB | 102 / 140 MB |
| Street batches p50 | 117–118 | **130–134** (a batch per near cell with a kit) |
| Air batches p50; triangles p50 | 28; air 331–349 k, street 1.80–1.81 M | 28–31; air 322–329 k, street 1.78 M |
| GPU p50 (air / street) | 3.06 / 6.12 / 3.35; 7.46 / 8.30 ms | 4.60 / 5.79 / 3.09 / 3.43; 7.70 / 7.17 / 7.67 / 7.72 ms |
| Main thread p50 (air / street) | 2.13–2.43 / 2.38–2.40 ms | 1.90–2.36 / 2.11–2.54 ms |
| Main-thread frames over 4 ms (of them near streaming) | air 2 (2), 0, 0; street 0, 0 | air 1 (1), 7 (3), 5 (3), 0; street 4 (0), 12 (2), 4 (0), 2 (0) |
| Garbage a run | air 142.0–142.5 MB; street 96 B | air 119.5–119.9 MB; street **2.5–3.4 MB** (0 collections) |
| Apply held frames (air / street) | 9–10 / 18–19 | 9–10 / 26–28 |
| Kits built / dropped / held at the end | — | air 78 / 78 / 0; street 54 / 33 / 21 |
| Late frames (pop-in) | — | **none on the path** in any run measured (air 4 runs, street 2). Street 6–10 frames during the warm-up, while the first chunks load |
| Slowest kit upload / frame of uploads | — | 0.25–0.76 / 0.28–0.80 ms; once 1.26 / 1.30 ms (street `212058`) |
| Worker time (Mono probe, one thread) | `ChunkRoads.Build` 54.5 ms a chunk | 48.1 ms a chunk; near kits 0.41 ms a cell on average, only for cells that come near |

The estimate was −50 to −80 MB at the end of the street run. Measured: −57 to −71 MB on the street path and −38 to −43 MB on the air path.

**Costs and open points, not hidden**
- **Batches.** Street batches p50 rose by 13–16, one per near cell with a kit. GPU p50 shows no change beyond its usual spread.
- **Frames over 4 ms.** Main-thread frames over 4 ms after set-up rose on the street path, from 0 / 0 to 2–12 a run. Most are not near streaming work, and their CPU time is 2.2–3.7 ms, so the thread was waiting, not working. The same count has varied this much before: 9 / 2 on the approach 3 "before" runs, and 3 / 44 on air. **Not explained, and not shown to be caused by the prototype.** It needs watching in the D-06 acceptance bench.
- **Street garbage during the path.** It rose from 96 B to 2.5–3.4 MB, because kits are now built while moving. No collection happened. The likely source, per-thread scratch lists growing the first time a thread builds a large cell, is **not traced**. Pre-sizing them is the obvious fix if the prototype is adopted.
- **The street apply holds longer** at start (26–28 frames against 18–19), because the apply now also waits for kit builds. All of it falls in the warm-up.
- **Only the near kit is deferred.** The far kit (183,096 slice vertices) is still in every resident chunk's far cell meshes.

**Other evidence**

| Check | Result |
|---|---|
| Windows build | Succeeded: Mono, 138,395,567 bytes, 0 errors, 20 warnings. Not run, since there is no Windows machine (K-02) |
| Not changed | Roads, piers, buildings, the metro, world data, chunk streaming rings, goldens |

**Decision for the owner**

Adopt approach 4 into main as it stands? Or adopt it with the scratch lists pre-sized first? Or keep approach 3 alone and leave the branch unmerged?

The recommendation is to adopt it, with the pre-sizing as a small follow-up. It saves 38–71 MB at no visible cost, with no pop-in on either path. Its cost is about 15 more street batches.

## O. Adopted into main after the owner's approval: approach 4, with the kit scratch pre-sized (2026-11-03)

The owner chose "Adopt + pre-size scratch". main was fast-forwarded to the prototype (`e00619d`); the branch `proto/k45-near-kit` is kept. The scratch change is one more commit on main. Nothing pushed.

**What changed** (on top of section N)
- `ChunkStreet.BuildNearKit` borrows a scratch set from one shared pool instead of keeping one per thread. The thread pool's threads come and go, and each new one grew its own lists again (section N's untraced street garbage; traced below).
- A set is made at the largest cell's near kit in the city, from all 15,603 cells with a kit: 19,840 vertices and 99,216 indices, rounded up to 20,480 and 102,400. No build grows one.
- The streamer's warm-up makes 4 sets ahead (`MaxKitJobs`, the builds that may run at once). A build that finds none free makes one more, so it still works if the limit is raised.
- Two counters for tests: sets made, and builds that grew a set.

**Scratch trace** (standalone Mono probe; 188 slice cells a round, each round on a new thread; [k45-scratch-pool.txt](AXIOM_V3/k45-scratch-pool.txt))

| | Prototype (`e00619d`) | Adopted |
|---|---|---|
| Making the sets ahead | — | 0.70 MiB a set, kept |
| First round, new thread | 1.74 MiB | 0.90 MiB: the pooled output arrays filling, once per process |
| Each later round, new thread | **0.87 MiB** | **0.03 MiB** (168 B a build: the result object) |
| Same thread, second pass | 168 B a build | 168 B a build |

The cost kept is 4 sets × 0.70 MiB, about 2.8 MiB of managed heap. The bench heap figures vary by 18 MB between runs, so it cannot be seen there.

**Is it the same picture?**
- The hash probe gives the same 396 lines as main before adoption (`d6ec614`) and as the prototype.
- Goldens: all 36 rendered and compared, 36 match.

**Tests**

| Check | Result |
|---|---|
| New `RoofTests.NearKitBuildsGrowNoScratch` | One set made ahead; Farmgate's 16 cells with a kit built on a new thread: no set grown, none more made. Largest Farmgate cell 8,164 vertices and 40,344 indices, within the set size |
| `NearRooftopKitsComeAndGoWithDistance` | The warm-up made 4 sets; after the streamer settles at Farmgate, still 4, none grown |
| Suites | EditMode 113 / 113, PlayMode 29 / 29 |
| Mutations | 4 of 4 caught, each by the assertion meant for it: scratch not handed back to the pool (16 sets made, not 1); index list made small, and position list made small (a set grew); the streamer makes no sets ahead (0 after the warm-up, not 4). Section N's mutation of the per-thread scratch no longer applies: that code is gone |

The editor's per-thread allocation counter read 0 bytes even for the result objects, which must allocate. So the editor test counts sets and growths, and the byte figures come from the standalone Mono probe.

**Bench**
- Development player, Apple M1, display held awake.
- After = 3 air and 3 street runs after one build; the first run after the build (air `215206`) is left out.
- Figures from each run's `summary.json` ([k45-adopted-bench.txt](AXIOM_V3/k45-adopted-bench.txt)).

| | Approach 3 (`d6ec614`) | Prototype (section N) | Adopted |
|---|---|---|---|
| Air: system memory end / peak | 568 / 622 MB | 525–530 / 575–580 MB | 524–528 / 576–580 MB |
| Street: system memory end / peak | 683–688 / 773–830 MB | 617–626 / 712–767 MB | 609–617 / 703–759 MB |
| Managed heap end (air / street) | 116–117 / 202–203 MB | 97–99 / 151–164 MB | 97–99 / 146–164 MB |
| Street garbage over the path | 96 B | 2.5–3.2 MiB (section N's "2.5–3.4 MB" mixed units) | **88,866–89,242 B**, 0 collections |
| Air garbage a run | 142.0–142.5 MiB | 119.5–119.9 MiB | 106.7–115.4 MiB |
| Street batches p50 | 117–118 | 130–134 | 132–134 |
| GPU p50 (air / street) | 3.06–6.12 / 7.46–8.30 ms | 3.09–5.79 / 7.17–7.72 ms | 3.13–3.14 / 7.45–8.24 ms |
| Main thread p50 (air / street) | 2.13–2.43 / 2.38–2.40 ms | 1.90–2.36 / 2.11–2.54 ms | 2.00–2.44 / 2.29–2.69 ms |
| Main-thread frames over 4 ms (of them near streaming) | air 2 (2), 0, 0; street 0, 0 | air 1 (1), 7 (3), 5 (3), 0; street 4 (0), 12 (2), 4 (0), 2 (0) | air 1 (0), 0, 2 (1); street 0, 2 (0), 5 (1) |
| Apply held frames (air / street) | 9–10 / 18–19 | 9–10 / 26–28 | 8–10 / 26–28 |
| Late frames on the path (pop-in) | — | none | none (street 6–8 in the warm-up, as before) |
| Slowest kit upload / frame of uploads | — | 0.25–1.26 / 0.28–1.30 ms | 0.30–0.42 / 0.30–0.50 ms |

**Costs and open points, not hidden**
- **Street garbage is not back to 96 B.** About 87 KiB remain, roughly 1.6 KiB per kit uploaded (54 a run). The frames that allocate (68) match the frames with kit uploads (66). So the likely source is the upload's new GameObject, renderer, mesh and build task. It is not traced to the byte, and no run collected.
- **Frames over 4 ms on the street path:** 0, 2 and 5 a run (prototype 2–12, approach 3 0 / 0). All seven ran 4.01–4.23 ms with 2.15–3.79 ms of CPU, and one was near streaming. Still not explained; the D-06 acceptance bench watches it.
- **Unchanged from the prototype:** the street apply holds 26–28 frames at start, all in the warm-up; street batches +13–16 against approach 3; the far kit is still in every resident chunk (183,096 slice vertices).

**Other evidence**

| Check | Result |
|---|---|
| Windows build | Succeeded: Mono, 138,396,591 bytes, 0 errors, 20 warnings. Not run, since there is no Windows machine (K-02) |
| Not changed | Roads, piers, buildings, the metro, world data, chunk streaming rings, goldens |

**K-45 now.** Approaches 3 and 4 are both on main. Against the merged kit before either (section M's "before"):
- air end memory 597–599 → 524–528 MB;
- street end memory 708–710 → 609–617 MB. (The kit-off figures of section C, 603–606 MB street and 528–531 MB air, predate approach 3, which shrinks every streamed mesh, so they do not say what the kit costs now. That needs a kit-off run on main, not done);
- street managed heap 200–201 → 146–164 MB, against 132 MB with the kit switched off (section C).

What remains of K-45: the far kit in every resident chunk, and 13–16 more street batches. Instancing is still not pursued, awaiting the owner's confirmation (section K item 3).
