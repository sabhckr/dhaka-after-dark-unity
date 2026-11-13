# Master directive audit (2026-11-03)

The initial audit that Part 25 of the owner's AXIOM master directive asks for ([ENVIRONMENT_MASTER_DIRECTIVE.md](../ART_DIRECTION/ENVIRONMENT_MASTER_DIRECTIVE.md)), sections A to L. Nothing in the game was changed for it. It was written at commit a066c87 (K-37 P-1).

**What was added:**

- the directive, verbatim;
- this report and its evidence in [AUDIT_2026-11-03/](AUDIT_2026-11-03/);
- a read-only census tool, [Tools/census](../../Tools/census/README.md).

Nine inventory validation notes that P-1 had left out of date were also corrected (section A).

**What was inspected:**

- both directives and the detailing standard;
- all 16 inventory files;
- the roadmaps, the M2 and K-37 reports and the known issues;
- the world builders and the sky code;
- the 29 golden views and the latest test logs;
- the repository state and disk use.

## Summary

**What the game streams.** The game streams only the 12-chunk Farmgate–Kawran Bazar slice (`ChunkStreamer.SliceOnly`, cells 6–9 × 27–29). The exported world is 1,303 chunks. Both are reported below.

**Buildings.** The slice has 5,642 buildings and the exported world 494,978.

- 67 have facade detail: all of them in the 200 × 250 m validation area.
- 10 have ground-floor shops.
- None has roof equipment, a far level of detail or a material chosen from the data.
- So **5,575 of 5,642 slice buildings (98.8 %) and 494,911 of 494,978 city buildings (99.99 %) are still basic placeholders.** In the directive's terms, building detailing has barely started.
- Classification is the first obstacle. Only 6.7 % of buildings carry their own OSM type, and 7.4 % more can be inferred from the landuse polygon they stand in. 85.9 % have no category in the data at all.

**Inventory.** It covers most of the directive's physical requirements; section B lists the 112 genuinely new ones. The gaps are:

- flyovers, ramps and interchanges;
- metro trains and operations;
- weather;
- cinema posters and the July 2024 graffiti;
- hawker behaviour;
- water edges;
- the per-building audit and its detail levels.

**July 2024 graffiti.** The documented paintings on the Farmgate–Karwan Bazar metro pillars are a government commemorative series painted in July 2025, not 2024 movement graffiti. The only 2024 Farmgate record is student graffiti on a road-divider wall, with no text or position given. Even where pillar numbers are known (464, 466, 467), the drawn piers stand on a 30 m grid, not at their real positions (K-37). No openly licensed photograph of any of it was found. Section J has the register.

**Storage.** About 18.9 GB of the 25 GB budget is in use. Since D-05 the growth has been mostly bench evidence.

## Two discrepancies the directive names

**Chunk counts.** These were checked rather than assumed.

| | Chunks | Note |
|---|---|---|
| Inside the world boundary (both manifests) | 1,333 | — |
| Source repository, `dhaka-after-dark` (6f320b0, generated 2026-10-30 04:47) | 1,298 | 37 boundary chunks hold nothing |
| Unity export (`dev/architecture` cd3035d, 08:52 the same day; `WorldData.lock`) | 1,303 | 32 are empty |

The export adds landuse, barriers, power lines and more point kinds, so 5 chunks that were empty in the source now hold something.

Both manifests count 495,002 buildings. The C# reader finds 494,978 distinct building ids with a usable outline in 519,573 pieces, and no id with only unusable outlines. The 24-building gap between the manifest's count and the reader's was not traced: likely ids counted twice by the exporter, but not verified.

**What is "active".** The streamer's `SliceOnly` is on by default, so only the slice is playable today. "Citywide" in this report means the 1,303 exported chunks.

## A. Consolidated inventory audit

**Status today.** `Tools/inventory-check.py` passes on 890 rows:

| NOT STARTED | RESEARCHED | BLOCKED | IN PROGRESS | IMPLEMENTED | TESTED | APPROVED |
|---|---|---|---|---|---|---|
| 813 | 0 | 2 | 30 | 11 | 34 | 0 |

**Corrected in this audit.** P-1 changed figures that 9 validation notes still quoted at their D-05 or M2 values. They now give the post-P-1 counts from the 2026-11-03 15:18 EditMode log, with the M2 figure kept where no re-count exists:

- FP-SUR-001, FP-SUR-005, FP-SUR-011;
- RD-STR-001, RD-DIV-002;
- UT-PWR-001, UT-LGT-001, UT-CBL-001, UT-DRN-001.

No status changed.

**Statuses.** The directive defines five statuses. The inventory has seven: its five plus RESEARCHED and APPROVED. The meanings agree: IN PROGRESS is partly built, IMPLEMENTED is built and seen in a Unity shot, and TESTED has a test or golden that fails if it breaks. No mapping change is needed.

**Columns.** The directive asks every record for geographic relevance, implementation location, known issues and performance considerations.

- The inventory has none of these as columns.
- Known issues and evidence share `Validation`.
- Performance appears only as the `LOD` column.

Adding four columns touches all 890 rows, so it is listed for approval (section I).

**Directive coverage.**

| Directive part | Covered by existing rows | New rows (section B) |
|---|---|---|
| 5 Roads | RD (70), FP (35), UT-COV, UT-DRN | Navigation (3), traffic signal heads |
| 6A, 6C, 6D Flyovers, rail overbridges, interchanges | None | RD-ELV (14) |
| 6B Footbridges | FB (21) | Deck drainage |
| 7A–C Metro track, viaduct, stations | MT (63) | Platform screen doors, viaduct lighting, gradients, real pier positions |
| 7D–E Trains and operations | MT-FUN-004 to 009 only | MT-TRN (7), MT-OPS (4) |
| 8 Buildings | BD (102) | Audit, levels, cinemas, informal, mixed-use, tile, stone, glass, metal, setbacks, interior darkness, rooftop laundry and gardens, roof coatings, interiors, districts (20) |
| 9 Signs | SG (20), SH-PRP-005/006, SH-BRD-001 | Seasonal banners, stickers and standees, illuminated signs, hardware, bilingual layout |
| 10 Cinema posters | None (SG-SGN-018 is printed posters in general) | SG-MOV (5), SG-CIN (2) |
| 11 July 2024 graffiti | None | SG-JUL (6) |
| 12 Hawkers | FP-OCC (12), SH-TYP-003/019, SH-PRP | Flower sellers, repair stalls, seasonal and temporary markets, hanging goods, behaviour (3) |
| 13 Nature | VG (37), PS (18), RD-DIV-012/014 | Weeds and vines, potted plants, water edges (3), variation, wind animation |
| 14 Weather | MA-WEA-004/017/018/019, MA-SYS-001/002 | Weather.csv, a new file (20 rows) |
| 15 Night | UT-LGT-002, BD-WIN-011, SH-PRP-021, SG-SGN-017, MT-FUN-008, VH-FUN-004/005 | Sign, viaduct and flyover lighting; signal heads |
| 16 Utilities | UT (55), FP-OBJ, PS-FUR-006, SG-SGN-001, RD-STR-003/004, CS-KIT | Exposed water pipes |
| 17 NPCs | CH (84) | Role outfits, animation |
| 18 Vehicles | VH (194) | Sounds |
| 19 Districts | BD-FAM-021, VG-LND-010 | District identity profiles |
| 20, 22, 23, 26 Performance, validation, coverage, gates | PERFORMANCE_REPORT, the goldens, this report | No inventory rows: these are process |

**Category codes.** The directive suggests codes such as BR-FLY, BL-RES and NT-TREE, and says to reuse existing ones where they are equivalent. Existing ids are never renamed (standards section 4), so:

- flyovers become a new subcategory, RD-ELV, of Roads;
- footbridges stay FB, buildings BD, nature VG and street life FP-OCC and SH;
- weather becomes a new file code, WX.

## B. Genuinely new requirements

112 rows, in [AUDIT_2026-11-03/proposed-inventory-rows.csv](AUDIT_2026-11-03/proposed-inventory-rows.csv), with the status the evidence supports today.

| Status | Rows |
|---|---|
| NOT STARTED | 89 |
| IN PROGRESS | 10 |
| BLOCKED | 5 |
| IMPLEMENTED | 4 |
| TESTED | 4 |

By file:

| File | New rows |
|---|---|
| Buildings | 20 |
| Signage | 20 |
| Weather (new file) | 20 |
| Roads | 18 |
| Metro | 15 |
| Shops | 7 |
| Vegetation | 7 |
| Others | 5 |

They are proposals and not yet in the inventory. On approval they go in with full columns and `inventory-check.py` re-run. The ones that change what comes next:

- **RD-ELV-001 to 014: flyovers.** City-wide, the data has 801 elevated vehicle ways at layer 1 or above (198.8 km); the slice has 27 (10.1 km). They are drawn as flat decks with no supports, ramps or shadows (K-13, K-20).
- **BD-AUD and BD-LVL: the per-building register and the four detail levels.**
- **MT-VIA-023: piers at their real positions.** BLOCKED, and it blocks SG-JUL-004.
- **SG-JUL-001 to 006: the July 2024 graffiti.**
- **WX: weather.**
  - In place: clear, cloudy and overcast; golden hour from the real sun; drifting clouds; haze.
  - None of the rest: rain, storms, fog, wind, post-rain and blended transitions.
- **RD-ELV-013: rail overbridges.** The ground railway itself is not drawn: 98.7 km of ground rail city-wide and 5.1 km in the slice are in the data and unused.

## C. Duplicates to merge

The directive repeats requirements the inventory already has. Each one keeps its existing row, and the row's `Directive` column gains the new part number on approval. The main ones:

- **Dust, mud, puddles, wet asphalt and splashes** (Parts 5 and 14) → MA-WEA-001, 015, 017, 018, 019.
- **Wet concrete** → MA-WEA-004.
- **Construction areas and barriers** (5, 16) → CS-KIT-003, 017, 018.
- **Bus stops, bins, seating** (16) → PS-FUR-006, FP-OBJ-003, FP-OBJ-001.
- **Traffic signs and barriers** (16) → SG-SGN-001, RD-STR-003, RD-STR-004.
- **Phone and internet cables** (16) → UT-CBL-003, 004.
- **Night lights** (15):
  - street lights → UT-LGT-002;
  - windows → BD-WIN-011;
  - shops → SH-PRP-021;
  - LED displays → SG-SGN-017;
  - stations → MT-FUN-008;
  - head and brake lights → VH-FUN-005, 004.
- **Old Dhaka** (8.8) → BD-FAM-001.
- **Transport terminals** (8.1) → TR-BUS-001, TR-RLW-001.
- **Rooftop rooms, parapets, water stains** (8.5) → BD-ROF-009, BD-EXT-012, MA-WEA-022.
- **Utility meters** (8.6) → BD-SVC-001.
- **Wall advertisements, rooftop advertising, LED scrolling** (9) → SG-SGN-006, 016, 017.
- **Menu boards and price lists** (9, 12) → SH-PRP-005.
- **Plastic stools, umbrellas, pushcarts, tea stalls, fruit, vegetable and clothing vendors** (12) → SH-PRP-016, FP-OCC-007, FP-OCC-009, SH-TYP-003, FP-OCC-003, 004, 005.
- **Landscaped medians** (13C) → RD-DIV-012, 014.
- **Pathways, gardens, park furniture** (13C) → PS-SPC-006, PS-SPC-003, PS-FUR.
- **Platform information, announcements, boarding** (7C, 7E) → MT-STI-009, MT-FUN-009, 006, 007.
- **The metro power arrangement** (7A) → MT-VIA-014. The real system's supply has to be sourced before it is modelled.
- **Within the new directive itself:**
  - Parts 8.8 and 19 (districts) → one row, BD-DST-001;
  - Part 14's lighting and Part 15's night lighting → the same rows;
  - Part 22's golden list is a validation plan, not inventory.

## D. Implemented features (built, seen in a Unity shot, no test of their own)

11 inventory rows. Plus 4 among the proposed new rows: cloudy weather, cloud drift, haze, and weather-scaled light.

- **Footbridge railings:** FB-STR-007.
- **Pier caps, viaduct beams, viaduct safety barriers:** MT-VIA-002, 003, 016.
- **Cracks, repair patches, macro wear:** MA-WEA-009, 013, 014.
- **Asphalt repairs and cracks:** RD-SUR-011, RD-SUR-016.
- **Roundabouts:** RD-STR-007.
- **Standing water in drains:** UT-DRN-012.

## E. Tested features

34 inventory rows, each naming its test or golden in `Validation`. Plus 4 proposed weather rows: clear, overcast, golden hour, and the sky through the day (DayCycleTests).

| Area | Tested rows |
|---|---|
| Roads | 17 rows: surfaces, potholes, markings, stop lines, zebras, speed breakers, kerbs, junctions, concrete and painted dividers |
| Footpaths | 4 rows: raised pavement, slabs, broken slabs, ramps |
| Utilities | 8 rows: poles, crossarms, street lights, cables, manholes, gullies, drains |
| Footbridges | 2 rows: decks, stairs |
| Metro | 2 rows: piers, viaduct slabs |
| Materials | MA-SYS-002, day and night |

**Last full run** (K-37 P-1, a066c87):

- EditMode 96/96 and PlayMode 25/25.
- 29 golden views.
- The Windows build is **built, not launched or tested on Windows** (K-02).

## F. Unresolved blockers

Each with what would unblock it.

| Blocker | What it blocks | Evidence | Unblocked by |
|---|---|---|---|
| No elevation source (K-05) | Flyover ramps (RD-ELV-009), interchanges, track gradients, uneven roads (RD-SUR-014), flood dips (UT-DRN-009), runoff | World is flat; inventory BLOCKED rows | Owner choice and approval of a DEM download |
| Real metro pier positions unknown (K-37) | Piers in traffic lanes (44 of 66 in the slice); July graffiti placement | K37_PIER_INVESTIGATION.md; piers on a 30 m grid | P-2 decision (pending); then a source of pier positions for P-4/P-5 that the owner approves (field photos with pillar numbers, or a licensed survey) |
| Station heights (K-15) | Station kit, train stopping positions | Track 3 m above the station roofs in the slice | A sourced or labelled design height |
| Rights for July 2024 artwork | Any historical texture (SG-JUL-003) | Section J | Owner's rights route; permissions where required |
| No building types for 85.9 % of buildings | Category-correct facades city-wide | Section K | Approved, labelled inference rules |
| No tree species, no power lines in the slice (42 lines city-wide) | Species-correct trees; real pole routes | K-42 | A survey or imagery source, or keep the labelled rules |
| No Windows machine (K-02) | "Build tested" and "performance validated" on the target | — | A Windows PC or an approved cloud instance |
| No audio (K-07) | Thunder, announcements, vehicle sounds | — | Commissioned or licensed audio (cost needs approval) |
| BRTA manual not sourced (K-29) | Checking markings and signs against Dhaka's standard | — | A copy of the manual |
| Blender not installed | Modelled kits (route B) | Standards section 9 | Owner approval and a storage figure |

## G. Dependency-aware roadmap

The directive's phases A–K are a different list from the project's Phases A–J in [ROADMAP.md](ROADMAP.md): its "Phase C" is buildings, the project's is the finished vertical slice. To keep one numbering, this report calls the directive's phases **tracks DA to DK** and maps them onto existing work. Renaming either list needs the owner (section I).

| Track | Content | Existing work | Depends on | State |
|---|---|---|---|---|
| DA Infrastructure validation | K-37 and other conflicts | K-37 report; P-1 applied | — | P-2 awaiting the owner; P-4/P-5 need evidence |
| DB City infrastructure | Roads, flyovers, footbridges, rail, metro, drainage, utilities, lights | D-04, D-05, M2 done; D-08, D-11, D-13 planned; RD-ELV and MT-TRN/OPS new | DA; K-05 for ramps | Partly done |
| DC City-wide buildings | Classification, levels, facades, roofs, ground floors, materials, LODs | M2 one family on 67 buildings; D-06 next on the roadmap | Classification rules; far tier before near detail | Barely started |
| DD Commercial and cultural | Shops, signs, posters, graffiti | D-09, D-10 planned; fonts in place | DC (walls to hang them on); rights; approved fictional names | Not started; the graffiti register is in progress |
| DE Nature | Trees, vegetation, parks, water | D-07 planned; M2 three generic trees | DC for building edges; species source | Not started beyond M2 |
| DF Weather | Rain, storms, wind, fog, wet surfaces | D-03 done (clear, cloudy, overcast); D-12 planned | DE for wind on foliage | Partly done |
| DG–DK NPCs, vehicles, city life, simulation, gameplay | — | Project Phases E–I | DB–DF | Not started (the owner deferred them in Phase D) |

## H. Prioritised implementation sequence

A proposal. It follows the directive's production order (structure, then materials, secondary geometry, function, variation, weathering, optimisation) and the existing plan, where **D-06 (buildings) is already next**.

1. **K-37 P-2.** An owner decision, already asked.
2. **D-06a: classification and the far tier for every building.**
   - Labelled inference rules give every building a category.
   - Facade UVs from the builder (K-21).
   - A far-tier facade read from texture: window grid, floor bands, material family per category and district. This adds almost no triangles.
   - The census becomes a test that counts coverage.
   - This turns 494,911 placeholders into buildings at a distance first, as Part 8.2 Level 4 asks.
3. **D-06b: roofs and materials.**
   - Instanced rooftop kit (ED-5): tanks, stair rooms, parapets, dishes, AC units.
   - New layers: tile, glass, brick, concrete and roof coatings.
   - Measured against the 24 MB texture budget.
4. **D-06c: near facades.**
   - Eight facade families at Level 2 within about 150 m, switched by level of detail.
   - Mixed-use ground floors.
   - Level 1 landmarks from an owner-reviewed list.
5. **D-11 with RD-ELV:**
   - footbridge portal frames (K-39);
   - closed flyover decks with columns and shadows (K-20);
   - ramps only once K-05 is decided.
6. **D-08: street furniture and utilities.** Signal heads, signs, bins, bollards and cabinets.
7. **D-07: trees.** Species, variation and wind.
8. **D-09 and D-10: signs, shops and stalls,** plus cinema posters with approved fictional titles.
9. **July 2024 graffiti.** Only after the owner approves the register and the rights route, and the real pier positions are known.
10. **D-12 and D-13: wet surfaces and night lights,** then rain, storms, fog and transitions.
11. **Metro trains and operations,** after the station kit and K-15.

## I. Decisions for the owner

1. **P-2:** the K-37 pier rule fixes (already asked).
2. **Inventory additions:**
   - adopt the 112 proposed rows and the new Weather.csv;
   - add the four directive columns (geographic relevance, implementation location, known issues, performance);
   - add the merges in section C.
3. **D-06 re-scope:** classification and the city-wide far tier first, then roofs and materials, then near facades (H 2–4). **Approved by the owner on 2026-11-03**; D-06 is split into D-06a, D-06b and D-06c in ENVIRONMENT_ROADMAP.
4. **Classification rules for untyped buildings:**
   - footprint, height, road frontage, landuse and named places, each labelled "inferred";
   - review of the 1,938 Level 1 candidates (50 in the slice).
5. **Elevation source (K-05):** which DEM, and approval to download it.
6. **July 2024 graffiti:**
   - sign-off of the register (section J);
   - which era the world shows: 2024 (the pillars then carried posters and, as far as anyone recorded, no documented graffiti at Farmgate), or after July 2025 (the government series);
   - the rights route: openly licensed sources only, or seeking permission from photographers and artists;
   - approval before any reference image is downloaded into the project.
7. **Real pier positions:** which evidence the owner accepts, and from where.
8. **Cinema posters and signs:** an approved list of fictional film titles, actors and shop names, and real Bengali text.
9. **Blender:** install or not (open since Milestone 1).
10. **Evidence retention:** `Evidence/local` is 2.7 GB and grows 110–150 MB per bench run. Old runs back figures in the reports, so trashing any needs the owner.
11. **Phase naming:** keep tracks DA–DK, or rename the project's phases.

## J. Farmgate July 2024 graffiti reconstruction register

The full register, with every source opened (41 pages), the blocked pages, each artwork's fields and the rights notes, is [AUDIT_2026-11-03/graffiti-register.md](AUDIT_2026-11-03/graffiti-register.md).

- It is web research from page text only. No image was downloaded and no texture was made.
- Two of its key sources were re-read independently before it was accepted: Dhaka Tribune of 11 July 2025, and NTV Online of 9 August 2024.

**The main finding changes the task as written.** The documented paintings on the Line 6 pillars between Karwan Bazar and Farmgate are not 2024 movement graffiti.

- **What they are:** a government series, "ফিরে দেখা ফ্যাসিস্ট রেজিম" ("Revisiting the Fascist Regime"). It commemorates the uprising.
- **Who made it:** a team of about 50, approved by Dhaka North City Corporation.
- **When:** painted in July 2025 on a white base coat, and inaugurated on 1 August 2025.
- **What happened to earlier work:** bdnews24 reports that citizens' art painted on metro pillars after 5 August 2024 was removed about a year later. That report covers metro pillars in general; no source names a Farmgate or Karwan Bazar pillar that carried 2024 work.

So a reconstruction of "July 2024 graffiti on the Farmgate pillars" has no documented artwork to reconstruct. A game set in 2024 showing the 2025 series would be an anachronism. Which era the world shows is the owner's decision (section I).

**Register by category** (27 rows):

| Category | Rows | What "confirmed" rests on |
|---|---|---|
| 1 Confirmed Farmgate | 2 | Two August 2024 reports. NTV Online: student graffiti on a road-divider wall in the Farmgate area, 10 photos, no text transcribed. DBC News: students painting at Farmgate, no artwork identified. No pillar, no text, no precise position |
| 2 Confirmed Karwan Bazar | 10 | All 2025 series. Numbered pillars: 464 (northern face: the blogger-killings panel, overwritten on 10–11 July 2025, whitewashed, later "সন্ত্রাস" on two faces), 466 (Shapla Chattar killings) and 467 (Sagar–Runi, caption "2012: Revisiting the fascist regime"). Five more at area level only |
| 3 Nearby, location uncertain | 12 | 2025 series themes given for the whole stretch, without a pillar. Includes the series layout: year and title on one face, three events on the other three. Also the CC BY 4.0 WallVoice24BD dataset's Mirpur-Uttara-Tejgaon folder, images not checked |
| 4 No identifiable location | 3 | Openly licensed Commons series with no location, and 2024 press galleries |

**Placement.**

- The only pillar numbers any source states are 432/433 near Farmgate station (an accident report) and 464, 466 and 467 at Karwan Bazar.
- **The drawn piers cannot carry them.** They stand on a 30 m world grid, not at the real positions (K-37, MT-VIA-023), and the data has no pillar numbers.
- Placement on the "correct pillar face" needs:
  - dated street-level photographs showing the stencilled pier number beside the panel;
  - or the city corporation's pillar list;
  - and then real pier positions.

**Rights.**

- No openly licensed photograph places any graffiti at Farmgate or Karwan Bazar. Commons searches in English and Bangla found none.
- The press photos carry no licence.
- A photograph's licence would not cover the mural in any case: the 2025 panels are commissioned work whose rights holder is not stated.
- They also portray real, identifiable people (murdered bloggers, political figures, victims), which adds likeness and defamation questions.
- Reproduction would need:
  - permission from photographers or publishers;
  - permission from the artists or the city corporation, or legal advice on public-art exceptions in Bangladesh, which this audit did not check;
  - a review of the portraits.

**Comparison gallery (Part 11G).** Not possible yet: there is no cleared photograph and no placed artwork to compare with.

**Next checks, if the owner wants them:**

- the blocked pages: bd-pratidin's November 2024 report "graffiti overlaid by posters", Somoy, Jamuna and Kalbela;
- a YouTube report on the series, whose frames may show pillar numbers;
- two openly licensed Commons station photographs from September 2024 that may show the pillars;
- the WallVoice24BD images.

The pages could be read in the browser without downloading anything.

## K. Building coverage report

From [Tools/census](../../Tools/census/README.md), run 2026-11-03 15:33 at a066c87.

- The slice register (5,642 rows, every field Part 8.10 asks for) is [AUDIT_2026-11-03/building-register-slice.csv](AUDIT_2026-11-03/building-register-slice.csv).
- The full register (494,978 rows, 10 MB gzipped) is in `Evidence/local/census-20261103/register.csv.gz`. It is not committed because of its size; `Tools/census/run.sh` rebuilds it in about 10 seconds.
- Counts: [AUDIT_2026-11-03/building-census.txt](AUDIT_2026-11-03/building-census.txt).

| Part 8.10 report | Slice (streamed) | Exported world |
|---|---|---|
| 1 Total buildings | 5,642 (400 of them cut by the chunk grid) | 494,978 (in 519,573 pieces; 23,922 cut) |
| 2 Classified | 414 (7.3 %): 413 by own type, 1 by landuse | 69,713 (14.1 %): 33,277 by own type, 36,436 by landuse (inferred) |
| 3 With structural geometry | 5,642 as extruded footprints at data heights (3 raised). Heights are mostly estimated: 6 measured, 144 with levels | 494,978 likewise (38 raised; 627 measured, 29,604 with levels) |
| 4 With facade detail | 67 (M2 walk-up family: 3,616 windows in 77 pieces) | 67 |
| 5 With textures | All have the generic Plaster layer in one of six tones by id. An appropriate material pass (Part 8.7): 0. Material tags in the data, unused: 4 | Same. Material tags unused: 18,868 |
| 6 With roof detail | 0 (the 67 facade buildings have a parapet only). Roof-shape tags unused: 5 | 0. Roof tags unused: 169 |
| 7 With ground-floor detail | 10 (108 shopfronts with shutters, blank boards and awnings) | 10 |
| 8 With levels of detail | 0: one mesh at every distance (K-42) | 0 |
| 9 Visually validated | 0 per building. The 67 are seen together in `m2-shopfront` and `m2-overview` | 0 |
| 10 Still basic placeholders | **5,575 (98.8 %)** | **494,911 (99.99 %)** |

**Categories** (own OSM type, else landuse).

| Category | Slice | World |
|---|---|---|
| Residential | 255 | 61,300 |
| Commercial | 120 | 1,844 |
| Institutional | 21 | 4,190 |
| Religious | 8 | 242 |
| Special | 5 | 800 |
| Industrial | 3 | 1,078 |
| Informal | 2 | 180 |
| Other | 0 | 79 |
| Unclassified | 5,228 | 425,265 |

**Proposed levels.** These are candidates for the owner, not decisions.

| Level | Rule | Slice | World |
|---|---|---|---|
| L1 | Landmark point inside, a station, or named civic | 50 | 1,938 |
| L2 | Street-facing: a corner within 11 m of a carriageway | 3,465 | 310,155 |
| L3 | Block interior | 2,127 | 182,885 |
| L4 | Far tier | Every building | Every building |

**Heights as drawn (world).**

| Height | Buildings |
|---|---|
| Under 6 m | 10,216 |
| 6–12 m | 468,897 |
| 12–24 m | 14,625 |
| 24–45 m | 1,043 |
| 45 m or more | 197 |

The 6–12 m band is mostly the exporter's default height. Without levels data, a city of three-storey boxes is what the data gives: the far tier and the classification rules have to vary it, labelled as inference.

**Districts.** The data has no district boundaries. Each building is given the nearest named suburb, quarter or neighbourhood, a labelled inference.

| District (nearest named place) | Buildings |
|---|---|
| Farmgate | 451 |
| Kawran Bazar | 460 |
| Mohammadpur | 1,441 |
| Dhanmondi | 551 |
| Gulshan (with Gulshan 1 and 2) | 2,013 |
| Banani | 887 |
| Shahbag | 158 |
| Gulistan | 85 |
| Wari | 1,178 |
| Chawkbazar | 335 |
| Sutrapur | 124 |

"Old Dhaka" is not a place name in the data. These counts are small because the 732 named places are dense, so a district needs a polygon before district identity can be applied. That is part of BD-DST-001.

**Scalable completion strategy.** Section H, steps 2–4. Coverage is far first, then roofs and materials, then near:

- every building gets a believable distant appearance at almost no triangle cost;
- near geometry only appears where the camera is.

Every step re-runs the census, so the coverage figures above become the measure of progress.

## L. Performance and storage impact

**Storage now** (measured 2026-11-03): about **18.9 GB of 25 GB**, leaving about 6.1 GB of headroom.

| Item | Size |
|---|---|
| Unity editor | 10.0 GB |
| Hub | 0.54 GB, plus 0.35 GB of its data |
| Editor cache | 0.12 GB |
| Unity project | 5.6 GB |
| — of which Library | 2.3 GB |
| — of which `Evidence/local` | 2.7 GB (1.8 GB at the Phase D audit, 2026-11-02) |
| — of which Builds | 0.35 GB |
| — of which git | 0.09 GB |
| Three.js repositories and worktrees | 2.3 GB |

The world is generated in code, so geometry costs no disk. Disk growth comes from evidence (110–150 MB a bench run) and imported textures.

**Measured cost of building detail.** Built outside Unity with Dad.World, a066c87.

- An M2 facade piece averages **2,089 triangles against 12 for the same plain block**.
- The slice's buildings draw 225,546 triangles today, 160,834 of them the 77 facade pieces.
- At M2 density, the slice's 3,465 street-facing buildings would come to about 7.2 million triangles (an extrapolation, not a measurement). The air path now draws about 0.2 million triangles at a GPU p50 of 6.5–8.1 ms on the M1.
- **So near facades can only follow a level-of-detail system**, and the far tier has to come from textures, not geometry.

**Expected costs.** Estimates, to be measured when built.

| Item | Expected cost |
|---|---|
| Far-tier facades | Close to zero triangles. Extra texture reads in `Dad/World`, where stochastic sampling already costs 2–3 ms (K-22); a facade atlas set fits the 24 MB texture budget |
| Rooftop kit | Instanced (ED-5), so batches stay flat. Triangles depend on the distance cut-off |
| July 2024 artwork | A 2K BC7 texture with mips is about 5.3 MB. Twenty artworks would be about 110 MB of disk and GPU memory, if rights allow them at all |
| Weather (rain, wet surfaces) | GPU cost unknown; D-12 measures the wet path |
| Windows | Every figure here is from the M1 (K-02) |
