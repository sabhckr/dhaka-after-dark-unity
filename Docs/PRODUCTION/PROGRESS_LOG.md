# Progress log

Newest first. Each entry: what changed, the evidence, and the next task.

## 2026-11-06 — E-04 and E-05: the driven car and every vehicle from the owner's models

**Done** (the owner's order: "npc, vehicles, weapons will be designed from my provided 3d models, remove your own genarated npc, vehicles. use mine"). Records in [E04_PLAYER_VEHICLE.md](E04_PLAYER_VEHICLE.md) and [E05_OWNER_MODELS.md](E05_OWNER_MODELS.md).

- **Built:** the driven Jetta with raycast suspension, tyres, gearbox, fuel, its own cabin, doors and brakes; collision on every chunk; traffic that reacts to the driver; chase and driver cameras. All eight traffic families baked from the owner's models with near and far meshes. Drivers and riders posed from the owner's rigged people by their real skeletons. The generated vehicles and figures are gone from the code.
- **Evidence:** EditMode 256 of 256, PlayMode 51 of 51, mutations 48 of 49 (D8 is a guard no test can show), 23 goldens reviewed by eye.
- **Cost:** the 0.01 s step costs 0.02 to 0.03 ms when nobody drives, inside the noise. Driving adds 0.30 ms on the street and 0.56 ms in the air, main-thread p50. `Dad.Vehicle.Step` averages 0.07 ms.
- **Open:** K-60, K-61, K-62. F8 from the free camera is still how you drive until E-06. Only the Jetta is drivable.
- Windows build succeeded on macOS; not run on Windows.

**Old map folders moved to the Trash, on the owner's order (2026-11-06):** `dhaka-after-dark-baseline`, `-p0ref`, `-p1`, `-stage`, `-e04-staging`, and the old players `Builds/MacBench-88b7a22`, `-head`, `-main`, `-p6`, about 1.8 GB. All untouched since 2026-10-30 or superseded. Their commits stay in `dhaka-after-dark/.git`. The one thing only they held: 1,255 uncommitted chunk files in `-stage`, an old map state nothing used. Kept: `dhaka-after-dark` (the repository and the raw OSM file), `dhaka-after-dark-arch` (the source `Tools/sync-world-data.sh` exports from), `dhaka-after-dark-staging` (the canonical Farmgate set, and copies of the current world, not a past map), `Builds/MacBench-f03`.

## 2026-11-06 — F-01 to F-03: lanes, signals, traffic and the NPC vehicles

**Done** (the owner's "go for npc vehicles and other details"). Record in [F03_TRAFFIC.md](F03_TRAFFIC.md).

- **Built:** a left-hand lane graph per chunk, built as chunks stream in with no change to the chunk file; four junction kinds (Signal, Officer, Priority, AllWay); a headless simulation with four levels and a pool of 300; eight assembled vehicle families generated in code, drawn instanced, with working lamps and wheels.
- **Evidence:** EditMode 193 of 193, PlayMode 33 of 33, mutations 26 of 26 (20 at first), 14 new goldens reviewed by eye, 49 existing goldens unchanged.
- **Cost:** street main-thread p50 2.40 → 3.09 ms; air 2.27 → 2.39 ms, inside the noise. Traffic averages 0.45 ms on the street but goes over its 1.5 ms budget on 94 of 2,000 frames (K-59).
- **Open:** K-57 (2 of 6 soak seeds flag a gridlock), K-58 (41 directions closed by overlapping roads), K-59. Nobody can drive a vehicle, officers have no figure, no vehicle uses the elevated decks, and 6 of 13 bus stops are placed.
- Windows build succeeded on macOS; not run on Windows.

**Next:** the player vehicle system, in Unity, which the owner chose on 2026-11-06. D-07 trees and D-08 street furniture come back after it.

## 2026-11-05 — K-56 follow-up: the P-5a main-thread gap, profiled

**Done** (the owner's "Yes, profile it"). Record in [K56/result.txt](K56/result.txt).

- **Profiler captures:** both players, in the order main, P-5a, P-5a, main, 2,000 frames each. Busy main-thread time was 2.54 ms a frame for P-5a and 2.56 ms for main; no marker differs beyond the spread between captures.
- **Balanced bench without the profiler:** 8 runs, each player first and second in a pair equally often. Main-thread means were 2.09 ms for P-5a and 2.13 ms for main, and P-5a was higher in only 2 of the 4 pairs. Runs of one player differ by up to 0.6 ms.
- **Result:** the gap seen in the P-5a bench (P-5a higher in all 9 pairs) does not come back. **K-56 is closed:** no main-thread cost of P-5a is measured. The P-5a write-up's "residual of about 0.1 ms" is corrected in its record, in K-38 and in the ledger. Why the first two sets both fell P-5a's way is not known.
- No code or data changed.

**Next:** P-4 waits for the owner.

## 2026-11-05 — K-37 P-5a: a continuous median under the Line 6 viaduct (plan row 1c)

**Done** (the owner's "Yes, build it": 3.0 m wide, only where both twin carriageways keep two lanes). Record in [P5A_CONTINUOUS_MEDIAN.md](P5A_CONTINUOUS_MEDIAN.md); summary in [K37_PIER_INVESTIGATION.md](K37_PIER_INVESTIGATION.md), section 18.

**What changed:**
- A raised, kerbed median, 3.0 m wide, runs along the deck line wherever one-way twins pass either side and each keeps 6 m or more. It stops short of crossing roads, ground buildings, junction and bus-stop pads, crossings and islands. It runs to a chunk border and is capped there.
- A pier whose whole island footprint lies on the median stands on it; P-6 applies to the rest as before.
- The checker judges the drawn median itself (`median-*`, `run-*`).
- Fixed on the way:
  - a median splitting a single one-way road (`fitTwins`, checker `median-split`);
  - gaps at every chunk seam;
  - open ends at seams;
  - the checker trusting the builder's marker.
- No pier moved; no road, rail, building or chunk data changed.

**Evidence:**
- **Line 6:** 331 → 311 on a carriageway; 23 on the median, 20 on islands; 37 pieces, 1,835 m. **Slice:** 28 → 27 on a carriageway, 7 on islands, 4 on the median. Known list 68 → 67 (0 new, 0 gone).
- **Tests:** EditMode 165 / 165; PlayMode 30 / 30 on an idle machine (one 29 / 30 run beside Mono probes is reported). **Mutations:** 26 of 31 caught; the 5 missed are explained.
- **Goldens:** 49 views. 10 changed by P-5a, looked at and accepted; 3 new; 2 within their limit; 34 the same as main's player.
- **Roads worker:** +1.7 % a chunk on the slice, +2.8 % on Line 6 (K-38).
- **Player bench:** GPU, triangles, batches and memory show no change measured. The main thread was higher in all 9 pairs: +0.30 to +0.60 ms with P-5a run first, +0.07 to +0.16 ms with main first. Cause not found (**K-56**, open).
- **Windows build:** succeeded (138,487,151 bytes, 0 errors), not run.

**Found:** where the twins do not meet, the median also covers the strip between them, a third of it inner-sidewalk paving (**K-55**, the owner's choice).

**Next:**
- Owner: P-4 (portal-style station supports).

**Owner decision (2026-11-05):** K-55, "Keep the median": the median stays over the inner sidewalks; nothing changes.
- K-56: a profiler capture of both players.

## 2026-11-05 — K-37 P-3: counter wording (piers under stations counted apart)

**Done** (the owner's "K-37 P-3 to P-5"). Record in [K37_PIER_INVESTIGATION.md](K37_PIER_INVESTIGATION.md), section 12.

**What changed:**
- The builder counts a pier in a building on the ground (`PiersInBuilding`, a conflict) apart from one rising into a raised building such as a station body (`PiersUnderRaised`, reason `under a raised building`).
- The checker's summary prints both against its own figures. Reporting only: nothing moved, nothing drawn differently, no data changed.

**Evidence:**
- **Slice:** 13 → 2 on the ground and 11 under a raised building. **Line 6:** 101 → 4 and 97. The checker agrees on the ground figures; by disc clearance it finds 47 rising through and 58 clear under raised buildings on Line 6.
- **Tests:** EditMode 156 / 156 (goldens all match); PlayMode 30 / 30. **Mutations:** 8 of 8 caught. **Slice gate:** 68, 0 new, 0 gone.
- **Not run:** benches and worker timing (no geometry change; the added work is a point-in-polygon test for the few piers inside a building). **Windows build:** succeeded (138,482,447 bytes, 0 errors), not run.

**Next:** P-4 and P-5 wait for the owner's decision 3: how real pier positions are sourced. Nothing will be downloaded without approval.

## 2026-11-05 — K-37 P-6: median islands under Line 6 piers (plan row 1b)

**Done** (the owner's "Start P-6", decision 3: islands where the road is wide enough with traffic on both sides; the rest listed). Record in [P6_MEDIAN_ISLANDS.md](P6_MEDIAN_ISLANDS.md).

**What changed:**
- A viaduct pier on a carriageway gets a raised median island (3.0 m by 7.8 m) where traffic keeps a lane of at least 3 m on both sides and the island is clear of the chunk border, ground buildings, junction pads, crossings, bus stops and other islands. Otherwise the pier stays and the builder records why.
- Dividers stop either side of an island.
- Fixed on the way: flyover supports depended on the chunk built before (a stale scratch list, verified); flyover islands could overlap and could reach into ground buildings (two did).
- The checker judges every island (footprint, overlap, both sides). No pier moved; no road, rail, building or chunk data changed.

**Evidence:**
- **Line 6:** piers on a carriageway 354 → 331, 23 on islands. **Slice:** 38 → 28, 10 on islands; 68 violations, all listed (80 before).
- **Flyover chunks (461):** every built support clear.
- **Tests:** EditMode 155 / 155; PlayMode 30 / 30. **Mutations:** 20 of 27 caught, the 7 missed explained.
- **Goldens:** 46 views; 7 changed and 2 new, reviewed and accepted.
- **Roads worker:** no change measured (after a prefilter; the first version was +31 % on the slice).
- **Player bench:** no change measured on AC (re-run 2026-11-05); the battery set's slow frames did not come back.
- **Builds:** macOS; Windows built, not run.

**Owner decision (2026-11-05):** K-54, "Keep D-11a rule": flyover islands unchanged.

**Next:** K-37 P-3 to P-5 or J step 8, as the owner decides. (The owner chose P-3 to P-5; P-3 done the same day.)

## 2026-11-04 — D-11a: flyover supports and closed decks (AXIOM v3 section J step 7)

**Done** (the owner's "Start J step 7 now", with the brief's decisions: finished construction expressways, "Median, else portal", "Water piers + rail-side"). Record in [D11A_FLYOVER_SUPPORTS.md](D11A_FLYOVER_SUPPORTS.md). Built on branch `wt/d11a` and fast-forward merged into main. Nothing pushed.

**What changed:**
- `Runtime/World/Flyovers.cs` is new. Every elevated vehicle deck gets:
  - a closed box girder that casts shadows;
  - supports by the place order: clear ground, slid, median island, rail-side offset, portal, offset, off the grid, else spanned;
  - gap filling.
- The ground tests read the crossings' and bus stops' pads (`RoadMarkings`, `Sidewalks`).
- The border keep-out applies only near a deck cut.
- The checker surveys flyover supports: pile caps, spans, free ends, median lanes and rail clearance.
- No road, rail, building, water or chunk data changed.

**Evidence:**
- **Slice:** no-supports 27 → 0; 80 violations, all listed (105 before).
- **City (461 chunks):** every built support is clear. Residuals are K-53; headroom is K-52.
- **Tests:** EditMode 144 / 144; PlayMode 30 / 30.
- **Mutations:** 27 of 28 caught.
- **Goldens:** 44 views; 4 changed and 5 new, reviewed and accepted.
- **Roads worker:** +10–12 % on the slice and +6–7 % on the flyover chunks.
- **Builds:** macOS and Windows built, 0 errors (Windows not run).

- **Player bench:** interleaved with HEAD's player, lid open. GPU on the air path is up 0.4–0.8 ms on the whole-path mean; the street path shows no change. Street triangles are up 3–4 %, mesh memory up 1 MB, and garbage unchanged. A first attempt with the lid closed is invalid and was discarded.

**Next:** K-37 P-6 (median islands under Line 6 piers, row 1b of the plan). Open for the owner: K-52 and K-53.

## 2026-11-04 — K-37 P-2 pier rules, with K-50 (a) and (b) (AXIOM v3 section J step 6)

**Done** (the owner's "Approve P-2 with K-50"; record in [P2_PIER_RULES.md](P2_PIER_RULES.md)). Local commit on main. Nothing pushed.

**Scope as read:** P-2 (V-4, V-5, V-6) and K-50 (a), (b), which the checker's report put with P-2. Not included:
- K-50 (c), which waits for the elevation decision;
- K-50 (d) and (e), footbridge kit work (D-11);
- P-3, which was not named.

**Change**
- The viaduct builder reads the eight neighbours' elevated tracks (`NeighbourRails`, read-only, cached; only the 64 chunks with an elevated track). It joins each track across borders, pairs twins with the neighbours' tracks, and decides decks and piers along the whole run. Each chunk builds only its own girder pieces and the piers it owns.
- A pier in a junction pad, or on a road crossing under the deck (more than 30° off it), slides along the deck in 0.25 m steps up to 4 m, else it is spanned. Two spans never run in a row. No span is under 25 m. One pier per place.
- No track, road, building or chunk file changed. Piers move along the deck only.
- **Slice:** 61 piers (66), 38 on a carriageway (44).
  - K37-61's duplicate is gone.
  - Farmgate has one twin deck and one pier row (K-50 a).
  - K37-13 is not built (17.7 m span).
  - K37-16 is spanned over its trunk_link.
  - K37-29 slid 0.25 m and K37-46 slid 3.75 m, both clear of their side roads.
  - N 13,500 has its pier (K-50 b), at the trunk's edge.
- **Line 6:** viaduct duplicates 45 → 0, and out-of-range spans 4 → 0.
- **Checker:**
  - it reads the neighbours' tracks;
  - its span limits are written out (25 m, 61 m on the main axis);
  - only a pier ahead or behind counts as a span;
  - the known list is regenerated (125 → 105 rows).
- **Found and corrected:** the K-37 report's 10.5 m Farmgate track spacing is wrong. The tracks are 6.2–7.0 m apart, and the border runs between them.
- **K-51 opened.** One deck is built for side-by-side tracks at different heights (16 m and 11 m, north of Farmgate). P-2 pairs such tracks across borders too, so there are 2 more deck-top findings on Line 6 (248 → 250), none in the slice. Owner decision.

**Evidence**
- EditMode 130 of 130 (2 new: the Line 6 gate and the P-2 rules). PlayMode 30 of 30: `FarmgateChunkMatchesItsData` first failed, because it rebuilt the chunk without the neighbours' tracks; the test now attaches them as the streamer does.
- Mutations:
  - 8 new mutants (6 builder, 2 checker), all caught on the Mono harness; 3 of them in Unity too.
  - The earlier checker mutants: 11 of 15 caught. 4 give the same result on today's data: no viaduct duplicate is left to find, and the stair reach as before.
- Bench: HEAD's and the P-2 player, run for run (3 air + 3 street each). No change measured. A first, sequential comparison showed the P-2 main thread 0.3–0.6 ms slower, which the interleaved runs do not reproduce; both are in [P2/bench.txt](P2/bench.txt).
- Roads worker time: +10.1 ms a chunk (+23 %) on the 64 elevated-track chunks, in the Mono harness (K-38).
- Goldens: 7 changed (every viaduct view), reviewed and accepted; 32 unchanged.
- macOS build succeeded. Windows build succeeded, 138,452,463 bytes, 0 errors; not run (K-02).

**Next:** the owner's decisions:
- K-51;
- P-3;
- evidence for K-37 groups C, D and E;
- flyovers (K.5), section J step 7;
- footbridge fixes (K-50 d, e);
- numbering, boundary, K-46, instancing.

D-07 and D-08 still wait for section J steps 7 and 8.

## 2026-11-04 — Infrastructure placement checker (AXIOM v3 section J step 5)

**Done** (the owner's "Build the checker"; record in [INFRA_CHECKER.md](INFRA_CHECKER.md)). Local commit on main. Nothing pushed.

**Change**
- `InfraSurvey` (Editor assembly) surveys every metro pier, footbridge column, stair and elevated deck in the slice, as the game builds them, with the surrounding ring as context. It is read-only: no pier, road, building, rail line or chunk file was touched, `Runtime/` is unchanged, and P-2 is not applied.
- Stable ids (`VP-`, `FC-`, `ST-`, `FD-`, `FB-`, `VT-`) and a register with section G's fields: [INFRA/](INFRA/) holds 115 supports, 38 decks and 134 findings. `Tools/infra/run.sh` writes the register outside Unity in about 2 s.
- 125 violations, every one listed in `Tests/Infra/known-violations.csv` with its reference: K-37 (65), AXIOM E (35), K-39 (9) and K-50 (16). `InfraSurveyTests` fails on an unlisted violation and on a listed one that is gone.
- It agrees with the builder (44 of 44 carriageway piers), with the K-37 register (all 67 piers; V-4 to V-8 reproduced to the centimetre) and with AXIOM E (27 decks with no supports; the same 8 deck–building pairs).
- Five errors in the checker's first run were found against the data and fixed before acceptance (steps as carriageways, spans to another deck's pier, column ids, stairs not holding decks, one road per support).
- New, as K-50: a twin track split by the c_7_-30 / -31 border doubles 6 piers and the deck; the grid line on the c_8_-28 / -27 border has no pier (94.2 m span); 2 piers through 6 m flyover decks; 6 coincident footbridge columns; a column on a bus stop pad.
- K-39's column counts were stale: 26 placed and 8 left out since K-37 P-1, against 16 and 18 at M2. Measured by building the slice with the builder before and after a066c87.

**Evidence**
- EditMode 128 of 128 (7 new), PlayMode 30 of 30.
- Mutations: 13 of 15 caught on the Mono harness. 2 give the same result on today's data and are recorded as missed. 2 more were caught in Unity's runner.
- **Spatial acceptance only:** no navigation, collision or driving checks (Phases E and F), and no lane polygons.
- No player build or bench: nothing in the player changed, and no performance figure is claimed.

**Next:** the owner's decisions: P-2 (now with K-50 a and b), flyovers (K.5), footbridge fixes, numbering, boundary, K-46, instancing. D-07 and D-08 wait for section J steps 6 to 8.

## 2026-11-04 — D-06 accepted by the owner

**Done:** the owner accepted the D-06 sequence ("Accept D-06") after D-06c (`e021bb3`): D-06a classification and far tier, D-06b roofs and materials, D-06c near facades. Docs only; no runtime change. Nothing pushed.

**Still open from D-06c:** the Level 1 landmark list (271 rows), K-47 (scratch sizing), K-48 (inferred styles; Old Dhaka shutter colours), K-49 (air GPU cost).

**Next:** the infrastructure steps of [AXIOM_V3_AUDIT.md](AXIOM_V3_AUDIT.md) section J (5 to 8), each on the owner's section K decision. D-07 and D-08 do not start before those gates are approved.

## 2026-11-04 — D-06c: near facades

**Done** (the owner's "Start D-06c"; record in [D06C_NEAR_FACADES.md](D06C_NEAR_FACADES.md)). Local commit on main. Nothing pushed.

**Change**
- Within 150 m, the walls the far tier paints now have relief in geometry: sills, sunshades, grilles, balconies, mullions, pilasters, frames, shopfronts, shutters, signboards (blank) and awnings. They sit on the far tier's own bays and floors, and skip the windows the shader bricks up.
- There are eight near styles: WalkUp, ShopHouse, Office, Market, Industrial, Institutional, OldDhaka and Modern. Each is a **labelled inference** from the building's category and type, plus its district: the nearest named place within 1,500 m.
- A room ray per bay keeps pieces out of narrow gaps.
- Each street cell's near kit carries its facades as a second submesh (`WorldNear.mat`). The shader dissolves them in with a screen-door dither from 140 m (none) to 110 m (all), in all four passes. Cells switch at 150 m, and every piece lies within 7.7 m of its cell, so nothing is drawn when a cell switches.
- Pieces that cover much of a wall are in the wall's own material, so buildings keep their colour through the fade (found in the golden review). Old Dhaka's shutter leaves are the exception (K-48).
- Level 1 landmarks: a 271-row candidate list for the owner ([D06C/L1_LANDMARK_CANDIDATES.md](D06C/L1_LANDMARK_CANDIDATES.md)); no landmark geometry.
- Footprints, heights, roads, piers, the metro and the far tier are unchanged: 270 of 270 hash lines are identical.

**Cost**
- The slice draws 6.78 M near-facade triangles over 192 cells; the city has 455.7 M, but only cells within 250 m are ever built.
- Bench (3 air + 3 street runs, against the K-45 adopted runs):

| Measure | Before | D-06c |
|---|---|---|
| Street triangles p50 | 1.78 M | 2.34–2.38 M |
| Street batches p50 | 131.6–133.8 | 158–161 |
| Street end memory | 609–617 MB | 672–674 MB |
| Street GPU mean | 7.97–8.74 ms | 7.73–8.25 ms |
| Air GPU mean | 4.46–4.52 ms | 4.69–5.44 ms (K-49) |
| Kit upload, slowest | 0.30–0.42 ms | 0.67–0.99 ms |

- Two bench runs were thrown out: the Mac slept with its lid closed (pmset: clamshell sleep 11:48–11:52).

**Tests:** EditMode 121 / 121 (8 new), PlayMode 30 / 30 (1 new); 19 of 19 mutations caught.
- `NearFacadesComeInWithoutPopping` first compared frames with and without the near submesh. It failed in the final pass on 2–5 pixels that differ by 1–2 levels, which also differ with every fragment dithered out.
- It now compares against the near facades drawn but fully dithered out: 0 of 230,400 pixels at 149 m, 1,135 at 60 m. The 1–2 level difference is recorded in K-49 and not traced.

**Goldens:** 39 views. 17 match; 19 changed and 3 are new (10, 50 and 200 m). All 22 were reviewed as golden, new and difference sheets before acceptance.

**Windows build:** succeeded, 138,446,143 bytes, 0 errors, 20 warnings (as before). Not run (K-02).

**Not changed:** roads, piers, buildings, metro, world data.

**Next:** owner decisions:
- the landmark list;
- K-47 (scratch sizing);
- K-48 (inferred styles; Old Dhaka shutters);
- the remaining section K decisions.

D-07 and D-08 wait for the owner's approval of the D-06 sequence and the infrastructure gates.

## 2026-11-03 — K-45 approach 4 adopted into main, with the kit build scratch pre-sized

**Done** (the owner's "Adopt + pre-size scratch"; record in [AXIOM_V3_AUDIT.md](AXIOM_V3_AUDIT.md) section O). main was fast-forwarded to the prototype `e00619d`; this commit adds the scratch change. Nothing pushed.

**Change**
- Near-kit builds borrow scratch lists from a shared pool, made ahead by the streamer's warm-up (4 sets) at the city's largest cell (19,840 vertices, 99,216 indices, rounded up). Before, every new worker thread grew its own lists again.
- Traced on Mono: a new thread cost 0.87 MiB of garbage, now 168 B a build. The 4 sets keep about 2.8 MiB.
- Same triangles as before adoption (hash probe, 396 of 396 lines); 36 of 36 goldens match.

**Bench** (3 air + 3 street runs, against the prototype)

| Measure | Prototype | Adopted |
|---|---|---|
| Street garbage over the path | 2.5–3.2 MiB | 88,866–89,242 B, 0 collections |
| Air garbage a run | 119.5–119.9 MiB | 106.7–115.4 MiB |
| End memory air / street | 525–530 / 617–626 MB | 524–528 / 609–617 MB |
| Street managed heap | 151–164 MB | 146–164 MB |
| Pop-in on the path | none | none |

- Not back to approach 3's 96 B: about 1.6 KiB a kit upload remains (likely the new GameObject, renderer, mesh and task; not traced).
- Street frames over 4 ms: 0, 2 and 5 a run (prototype 2–12, approach 3 0 / 0); all 4.01–4.23 ms with normal CPU time. Still not explained.

**Tests:** EditMode 113 / 113, PlayMode 29 / 29 (1 new test, 2 new checks); 4 of 4 mutations caught.

**Windows build:** succeeded, 138,396,591 bytes, 0 errors, 20 warnings. Not run (K-02).

**Not changed:** roads, piers, buildings, metro, world data.

**Next:** the remaining section K decisions (instancing, K-46, flyovers, P-2, numbering, boundary), then D-06c.

## 2026-11-03 — K-45 approach 4 prototype: the near rooftop kit built per cell (branch, not merged)

**Done** (owner-approved prototype; record in [AXIOM_V3_AUDIT.md](AXIOM_V3_AUDIT.md) section N). The work is on the local branch `proto/k45-near-kit`; main stays at `d6ec614`.

**Change**
- The near rooftop kit is no longer in the near cell meshes; each cell keeps its kit items (pooled).
- The streamer builds a cell's near kit on a worker within 250 m, shows it with the near tier and drops it beyond 350 m. A kit finished for a chunk already gone is handed back unused.
- The apply waits for kit builds as for loads.
- The far kit is unchanged.
- The same triangles as main in every slice cell (hash probe); 36 of 36 goldens match.

**Bench** (against approach 3)

| Path | End memory before | End memory after | Peak before | Peak after |
|---|---|---|---|---|
| Air | 568 MB | 525–530 MB | 622 MB | 575–580 MB |
| Street | 683–688 MB | 617–626 MB | 773–830 MB | 712–767 MB |

- Managed heap: street 202–203 → 151–164 MB.
- No near cell was drawn without its kit during any measured path.
- Costs:
  - street batches p50 +13–16;
  - street garbage during the path 96 B → 2.5–3.4 MB, 0 collections (likely per-thread scratch growth, not traced);
  - main-thread frames over 4 ms on the street path, 2–12 a run (before 0 / 0; the count has varied this much before). Not explained.

**Fixed on the way:** the first version allocated kit items each load (+1.5 MiB per 12 loads) and could start kits mid-apply; one test walk had a 5.31 ms apply step with a collection. Now pooled and ordered; per-load garbage is 2.7 MiB as main.

**Tests:** EditMode 112 / 112, PlayMode 29 / 29 (4 new tests); mutations 12 of 12 caught (2 survived at first; the tests were tightened).

**Windows build:** succeeded, 138,395,567 bytes, 0 errors, 20 warnings. Not run (K-02).

**Not changed:** roads, piers, buildings, metro, world data. Nothing pushed or merged.

**Next:** the owner's decision on adopting approach 4. Then the remaining section K decisions, then D-06c.

## 2026-11-03 — K-45 fix: streamed meshes drop their CPU copies once drawn

**Done** (owner-approved approach 3; record in [AXIOM_V3_AUDIT.md](AXIOM_V3_AUDIT.md) section M)

**`ChunkStreamer` change**
- Each frame it looks at up to 32 listed meshes, within 0.5 ms.
- It drops the CPU copy of every mesh a camera or shadow pass has drawn.
- Meshes never shown keep their copies. Dropping those would only move them to the GPU, as measured in the audit.
- `KeepMeshesReadable` is for tests.
- Release time has its own marker, `Dad.Streaming.Release`; the bench reports the release counts.

**Bench**

| Path | End memory before | End memory after | Peak before | Peak after |
|---|---|---|---|---|
| Air | 597–599 MB | 568 MB | 652–653 MB | 622 MB |
| Street | 708–710 MB | 683–688 MB | 848–851 MB | 773–830 MB |

- The slowest frame of releases took 0.26 ms.
- GPU memory, batches and triangles are unchanged.
- One air run had a GPU p50 of 6.12 ms (others 3.06 and 3.35 ms), unexplained and reported.
- The street path's managed heap is unchanged.

**Tests**
- New `DrawnMeshesDropTheirCopy`; `FarmgateChunkMatchesItsData` keeps copies on purpose.
- EditMode 111 / 111, PlayMode 26 / 26.
- Mutations: 4 of 4 caught. The experiment's 3-frame wait was removed: its mutation survived, because a mesh is only visible once drawn.

**Goldens:** 36 of 36 match, none changed.

**Windows build:** succeeded, 138,388,367 bytes, 0 errors, 20 warnings. Not run (K-02).

**Not changed:** the kit, roads, piers, buildings, world data. Nothing pushed.

**Next:** the remaining decisions in section K of the audit: the approach 4 prototype, K-46, flyovers, P-2, numbering, boundary. Then D-06c.

## 2026-11-03 — AXIOM directive v3.0: first-action audit (K-45, K-46, flyovers, piers)

**Done** (record in [AXIOM_V3_AUDIT.md](AXIOM_V3_AUDIT.md))

**K-45**
- Measured against the kit switched off. Street end memory +104 MB: managed heap +68 MB, mesh CPU copies +37 MB, graphics +6 MB. Air end memory +69 MB.
- The extra garbage is traced to one-time pool and list growth at session start. Warm builders allocate the same with or without the kit.

**Approaches compared in the player** (all reverted)

| Approach | Result |
|---|---|
| Release every mesh's CPU copy | No gain on the street path: hidden meshes moved to the GPU |
| Release drawn meshes only | −27 to −37 MB at run end, ≤ 0.43 ms a frame |
| Near-detail generation | Estimated only |
| Instancing | Analytic only: the shader has no instancing path |

**K-46.** In the slice, 4 of 5,642 buildings are tagged. 92.6 % rest on a rule over a rule-based category. A confidence column is proposed.

**Flyovers.** 27 elevated vehicle ways (10.06 km) in the slice:
- no supports, ramps, closed structure, shadows or collision;
- decks run inside 8 buildings' volumes (centre lines, a lower bound);
- 10 ways are tagged `construction`.

**Metro piers.** Survey re-run: identical to the post-P-1 register, 44 of 66 in conflict, 22 clear.

**Numbering.** The directive's D-07 (NPC) and D-08 (vehicles) clash with the roadmap's D-07 (trees) and D-08 (street furniture). Nothing renamed; proposal in section J.

**Bench and tests**
- The bench records mesh memory and the managed heap (`FrameRecorder`).
- EditMode 111 / 111, PlayMode 25 / 25.

**Not changed:** streaming, the kit, roads, piers, buildings, world data. Nothing pushed or downloaded.

**Next:** the owner's decisions in section K of the audit; then K-45 and D-06c as approved.

## 2026-11-03 — D-06b: roofs and materials

**Done** (record in [D06B_ROOFS_MATERIALS.md](D06B_ROOFS_MATERIALS.md))
- **Every building has a skin** (`Runtime/World/BuildingSkin.cs`): wall material from `building:material` where tagged (3.4 %), else a labelled rule by category (96.6 %, K-46); roofs flat, as a slab, white coat, tar, mosaic tiles or corrugated sheet. City-wide walls: plaster 57.3 %, paint 23.5 %, brick 16.2 %, sheet 2.5 %, concrete 0.5 %, glass 56 buildings. No texture added: 16.33 MB on the GPU, within 24 MB.
- **Parapets** on 95.2 % of buildings: the data top kept, the roof 0.9 m lower, a 0.15 m parapet with its coping round every ring, none along the chunk border; the far tier stops its windows at the roof.
- **Rooftop kit** (`Runtime/World/Rooftops.cs`): stair and lift rooms, water tanks, satellite dishes and condensers by labelled rules, wholly on their roofs, near and far in the street cells. City-wide 466,687 roofs, 630,668 tanks. Merged, not instanced (reason recorded).
- **Tests:** `RoofTests` (7), 4 test files updated, 8 of 8 mutations caught. EditMode 111 / 111, PlayMode 25 / 25.
- **Goldens:** 4 new (`d06b-air`, `d06b-roof-close`, `d06b-viaduct`, `d06b-air-far`), 28 re-accepted after looking at each, 4 unchanged.
- **Performance:** GPU p50 unchanged (air 3.05–3.28 ms, street 8.04 / 8.08 ms); triangles p50 +66 % air, +25 % street; street batches p50 104 → 117; system memory peak +110 to +190 MB and air garbage +55 MB a run (K-45, untraced). Windows build 132.0 MiB, 0 errors.
- **Not done:** roof shapes; pumps, pipes, antennas, solar panels, railings; kit memory (K-45); window light (D-13).

**Next:** D-06c near facades. Open owner decisions: K-45 (how to cut the kit's memory), and from the audit P-2, inventory additions, DEM, graffiti, pier positions, fictional names, Blender, evidence retention, phase naming.

## 2026-11-03 — D-06a: classification and the far tier (owner's re-scope of D-06)

**Done** (record in [D06A_CLASSIFICATION_FAR_TIER.md](D06A_CLASSIFICATION_FAR_TIER.md))
- **Every building has a category** (`Runtime/World/BuildingClass.cs`): its own OSM type, else the landuse it stands in, else labelled rules (split by the chunk grid, tiny, named large, large, main-road frontage, walk-up default). City-wide: 6.7 % by type, 7.0 % by landuse, 86.2 % by rules; the pieces of every building agree (0 disagree, after a fix for multipolygons such as the National Parliament).
- **A far-tier facade on every building with a family**, drawn by `Dad/World` from facade coordinates the builder now writes (bays, floors, family and shop flag, metres along the wall): windows, frames, grilles, slab lines, shutters, glazed shopfronts and blank signboards. No triangle added. Religious, special and informal buildings, short walls, walls on the chunk border and the M2 geometry facades stay as they were. City-wide 2,042,092 walls patterned, 42,292 with shops.
- **K-21 resolved for building walls:** wall textures run along each wall at its true length.
- **Tests:** `BuildingClassTests` (8), 7 of 7 mutations caught. EditMode 104 / 104, PlayMode 25 / 25.
- **Goldens:** 3 new (`d06a-far`, `d06a-families`, `d06a-street`), 24 re-accepted after looking at each, 5 unchanged.
- **Performance:** air GPU p50 3.27–3.36 ms (as P-1); street GPU p50 8.20 / 8.33 ms (P-1 7.48 / 7.75), so about 0.6 ms for the facade shader where buildings fill the view; triangles and batches unchanged; garbage up about 18 MB a run (not yet traced), none in an apply step. Windows build 132.0 MiB, 0 errors.
- **Not done:** near facades in geometry (D-06c, K-43); window light at night (D-13); names beyond the large-building rule (K-44); category colours and materials (D-06b).

**Next:** D-06b roofs and materials. Owner decisions from the audit are still open (P-2, inventory additions, DEM, graffiti, pier positions, fictional names, Blender, evidence retention, phase naming).

## 2026-11-03 — Master directive audit (owner's AXIOM directive, Part 25; no production change)

**Done** (report in [MASTER_DIRECTIVE_AUDIT.md](MASTER_DIRECTIVE_AUDIT.md), evidence in [AUDIT_2026-11-03/](AUDIT_2026-11-03/))
- The directive is kept verbatim in [ENVIRONMENT_MASTER_DIRECTIVE.md](../ART_DIRECTION/ENVIRONMENT_MASTER_DIRECTIVE.md).
- **Inventory:** 890 rows checked against the directive; 112 genuinely new requirements proposed (not yet added), the duplicates listed for merging, 9 validation notes made stale by P-1 corrected (no status changed).
- **Buildings:** a read-only census (`Tools/census`) of every exported building: 494,978 city-wide, 5,642 in the streamed slice. 67 have facade detail and 10 ground-floor shops; none has roof equipment, a far level of detail or a data-chosen material. 98.8 % of the slice and 99.99 % of the city are basic placeholders. 85.9 % of buildings have no category in the data.
- **Chunk counts explained:** the source build has 1,298 chunks and the Unity export 1,303 of the same 1,333 inside the boundary; the export's extra layers fill 5 chunks that were empty.
- **July 2024 graffiti:** a source register for Farmgate and Karwan Bazar (section J, 27 rows). The documented pillar paintings there are a government series of July 2025, not 2024 graffiti; the only 2024 Farmgate record is a road-divider wall. No openly licensed photograph found. Nothing downloaded; no texture made.
- **Cost of detail measured:** an M2 facade piece is 2,089 triangles against 12 for a plain block, so near facades need levels of detail first.
- **Storage:** about 18.9 GB of 25 GB.

**Next:** owner decisions (report section I), starting with P-2 and the D-06 re-scope; no new work outside the authorised milestone until then.

## 2026-11-03 — K-37 P-1 applied: lane-based road widths, narrowing only (owner's decision)

**Done** (record in [K37_PIER_INVESTIGATION.md](K37_PIER_INVESTIGATION.md) section 9)
- The owner approved P-1 at 3.5 m lanes. `Runtime/World/RoadWidths.cs` applies it at read time: an estimated, one-way, lanes-tagged road takes lanes × 3.5 m. It runs through `Dadc.Load` and the road and building builders. The DADC data, `Dadc.Decode` and the exporter are unchanged.
- **Narrowing only, by the owner's choice.** Applied in full, the rule widened Manik Mia Avenue's 7-lane carriageways to 24.5 m, over 24 mapped median trees and a lamp. So a road never becomes wider than its class width: 726 ways are narrower city-wide, and 123 keep their class width.
- **Result:** group A's 8 piers are clear: 44 conflict and 22 are clear. No pier, deck, alignment, road centre line or building moved. P-2 is not applied.
- **Kept as it was:** whether a road has sidewalks or major-road markings is still judged by its class width, so only the kerbs move.
- **Side effects, all checked against the pre-P-1 world:**
  - medians are open where twin carriageways used to overlap, and divider pieces fell from 608 to 197;
  - trees placed rose from 189 to 193 of 199, and lamps from 58 to 61 of 64;
  - one Farmgate crossing is now two T-junction stems, with no pad.
- **Code fixes found on the way:**
  - lamps move at most 6 m to the kerb, which is the limit the test already stated;
  - street cell meshes are bounded by their own extent, because a cable span reached past the fixed margin.
- **Tests:**
  - EditMode 96 / 96, PlayMode 25 / 25; 4 new `RoadWidthTests` and a lamp-cap test;
  - 9 mutations, all caught;
  - 11 goldens re-accepted after a look side by side, 18 unchanged.
- **Benchmarks:** GPU within M2's spread, batches down; see PERFORMANCE_REPORT. Windows build succeeded.

**Next:** owner decision on P-2 (pier rule fixes) and how real pier positions are sourced; otherwise D-06.

## 2026-11-03 — K-37 pier investigation (owner's request; no positional change)

**Done** (record in [K37_PIER_INVESTIGATION.md](K37_PIER_INVESTIGATION.md))
- Surveyed all 67 pier places in the slice from the builder and the DADC data. The survey agrees with the builder on every place.
- Each pier is measured against its rail way and segment, every road within 25 m (width, estimate flag, lanes, one-way, distance) and every building within 10 m.
- **Correction to M2:** 52 piers conflict and 14 are clear. The 14 "in a building" are among the 52 (12 under stations). The M2 claims about the avenue's median were wrong and unsourced, and are withdrawn.
- **Groups:**

  | Group | Piers |
  |---|---|
  | A: width estimate (lanes tag) | 8 |
  | A′: width at 3.0–3.3 m lanes only | 4 |
  | B: pier rule ignores side roads | 2 |
  | C: station over the road | 12 |
  | D: building footprint | 2 |
  | E: insufficient evidence | 24 |

- **Verified errors:** 8, including the exporter's width rule (each one-way carriageway gets the full class width), the pier rule's gap for side roads, a duplicate pier at a chunk border, and two footprints reaching into a carriageway.
- **Proposals P-1 to P-6**, none applied. Lane-based widths would change 970 ways city-wide (726 narrower at 3.5 m lanes).
- **Evidence:**
  - pier register CSV;
  - 7 plan sheets (current against proposed widths);
  - 5 Unity player shots (views `k37-*`).
- **Reproducible:** `Tools/k37/run.sh` rebuilds every file byte for byte.
- No code, world data or test changed. The 5 shot views are now goldens (29 in all), because `GoldenViewTests` needs a golden for every view: that test passes, and two re-shots matched exactly. A proposal applied later shows as a golden change for the owner to compare.

**Next:** the owner's decisions on P-1 and P-2, and how real pier positions are to be sourced; then D-06.


**Done** (the owner chose Milestone 2 before D-06; record in [M2_FARMGATE_VALIDATION.md](M2_FARMGATE_VALIDATION.md))
- **Street pass** (`StreetFurniture.cs`, `StreetKit.cs`, `Structures.cs`): a structures mesh a chunk and a near and a far mesh for each 125 m cell, built on the worker after the roads; tiers switch at 150 m and 450 m with 10 m hysteresis.
  - Utility poles by rule along roads, with lamp arms, transformers and cables that sag between them (1,060 poles, 509 spans in the slice).
  - Mapped street lights (58 of 64; 18 moved out of the estimated carriageway to the kerb, 6 listed) and trees (189 of 199) in three generic broadleaf forms.
  - Line 6 viaduct along every elevated track: box girder, parapets, plinths, rails, piers on a grid, no pier in a junction (2,063 m, 66 piers).
  - Footbridge decks, railings, stairs and columns (11 decks, 7 stairs).
  - Road dividers between twin carriageways (608 pieces, 2.4 km).
- **Facades** (`Facades.cs`) for the 76 buildings of the validation area: floor bands, 3,616 windows, 108 shopfronts with shutters, blank signboards and awnings, parapets; footprint and top kept.
- Roads, footpaths and drains are unchanged from D-04 and D-05 and re-validated.
- **Tests:** `StreetTests` (11) new; 4 test files changed; 9 mutations caught. EditMode **91 / 91**, PlayMode **25 / 25**. 24 goldens (7 new, 14 re-accepted).
- **Figures** (PERFORMANCE_REPORT, M2): GPU over the slice p50 7.53–7.91 ms (D-05 6.75–7.17); street level p50 7.57 and 8.19 ms, one frame over 16.7 ms in two runs; triangles at street level 1.5 million p50 (3.6×), batches 122 (46); apply frames up to 2.40 ms; worker roads +7.8 ms a chunk, buildings +1.5 ms; garbage 33.1 → 72.5 MiB an air run, none in an apply step.
- **Windows build:** succeeded, 0 errors, 0 C# warnings, 131.9 MB.
- **Inventory:** 27 rows changed; 813 not started, 2 blocked, 30 in progress, 11 implemented, 34 tested.
- **New known issues:** K-37 (piers on drawn carriageways: owner decision), K-38 (worker time and garbage), K-39 (footbridge columns and stairs), K-40 (cable aliasing, half spans at borders), K-41 (blank signboards), K-42 (facades only in the area, no far tier; generic trees; poles by rule).

**Next:** the owner's decision on K-37; then D-06 buildings, second pass, using the facade work here.

## 2026-11-03 — Detailing standard (owner's directive, Milestone 1)

**Done**
- The owner's directive on world, character and vehicle detailing is kept verbatim in [DETAILING_DIRECTIVE.md](../ART_DIRECTION/DETAILING_DIRECTIVE.md). D-05 was finished first, as it asks.
- [DETAILING_STANDARDS.md](../ART_DIRECTION/DETAILING_STANDARDS.md) applies it to this project: what the slice data holds today; when detail is geometry, texture or function; data-driven builders versus modular kits; naming for ids, assets and folders; inventory rules; LOD tiers with triangle and texel targets (labelled targets, not measurements); references and licensing; the directive's acceptance checks mapped to the project's tools; its milestones mapped to Phase D, with six differences and three open owner decisions.
- [ASSET_INVENTORY](../ASSET_INVENTORY/README.md): 890 rows in 16 CSVs, deduplicated across the directive's parts. 840 NOT STARTED, 2 BLOCKED, 17 IN PROGRESS, 7 IMPLEMENTED, 24 TESTED, 0 APPROVED. Every row above NOT STARTED names its test, golden or known issue; placeholders count as NOT STARTED.
- `Tools/inventory-check.py` checks the files (exit 0).
- Nothing downloaded, no code or world data changed, so no test or bench run was needed.

**Next:** the owner's choice of order (directive Milestone 2 against D-06 to D-08), Blender, and reference sourcing.

## 2026-11-03 — D-05 footpaths and kerbs

**Done**
- **Sidewalks in the road mesh** (`Sidewalks.cs`, part of `ChunkRoads`), built after the markings; streaming and apply are unchanged.
  - Sides from the way's `sidewalk` tag (both, left, right; no, none and separate give none), else both sides of vehicle roads 6 m or wider other than motorways and construction. Bridges get none.
  - Each side is a band along the drawn road, 0.15 m above it: 2.5 m on trunk and primary roads, 2 m on secondary and tertiary, 1.5 m on the rest. The width is sampled against building walls, other carriageways, junction pads and earlier bands, and runs to them where they are close, so a narrow median is paved across.
  - Cut wherever it would run onto a carriageway, pad or earlier band; each quad is checked again and halved down to 0.25 m where it would overlap.
  - Kerb faces down to the road, outer faces down to the ground and end faces close every band, so nothing floats.
- **Ramps:** the kerb drops to the road over 1.8 m either side of a zebra, whichever road's sidewalk meets it (22 of 22 in the slice).
- **Paving:** three tints by a hash of the way; broken slabs (0.9 m, 4 cm lower, with step faces) in one 10 m slot in four.
- **Bus stops:** each mapped stop takes the nearest at-grade road on its side; 6 m either way the band is a stop pad with a painted kerb, or a 2 m pad where the side has no sidewalk (13 of 13 in the slice).
- **Open drains** on residential, living-street and unclassified lanes without a sidewalk: 0.6 m, a channel between two lips, covered flush at zebras.
- **Not done:** vendor spaces, moved to D-10 (K-35).
- **Tests:**
  - `SidewalkTests` (7): the rule; sidewalk and drain coverage every 2 m along every slice road side (99.91 % and 99.81 %, every miss listed); every outline edge closed; no overlaps or bands over roads; bus stops; ramps.
  - 7 mutations caught, in the harness one at a time and in Unity all together. EditMode **80 / 80**, PlayMode **25 / 25**.
  - 17 goldens: `d05-bus-stop` and `d05-drain-lane` new, 15 re-accepted after a look.
- **Found by the tests and fixed:** bands overlapping on their own road's hairpins; drains missing narrow gaps; whole 5 m pieces dropped by the overlap check. A fix that treated the chunk border as a wall was tried and reverted (K-33).
- **Figures:** see PERFORMANCE_REPORT (D-05).
  - Slice road vertices 84,183 → 521,375; no new draw calls.
  - GPU over the slice p50 6.75–7.17 ms, inside D-04's spread. Street level GPU p50 7.9–8.4 ms, max 15.4 ms, at 2.9 times the triangles.
  - About 45 ms more worker time a chunk (3.7 → 49.2 ms in the harness, after a pass from 57.6). Garbage 9.6 → 33.1 MiB a run; loads held for building up to 17 frames at the street start. No apply step met a collection (K-36).
- **Windows build:** succeeded, 0 errors, 0 C# warnings, 131.9 MB.
- **New known issues:** K-33 (coverage misses), K-34 (sides and widths by rule), K-35 (vendor spaces), K-36 (worker time and garbage). K-23 and K-31 updated.

**Next:** D-06 buildings, second pass: facade families, windows lit at night, balconies, shopfronts, facade UVs (K-21).

## 2026-11-02 — D-04 roads, second pass

**Done**
- **Markings in the road mesh** (`RoadMarkings.cs`, part of `ChunkRoads`): streaming and apply are unchanged. Every road is kept as drawn (after chaining and fillets) with its way, class, lanes tag and one-way per segment.
- **Lines** on marked roads:
  - lane lines from the lanes tag, capped where lanes would be under 2.5 m, or from the width (lanes of at least 3 m); one-way from the road graph's edges;
  - a centre line on two-way roads, continuous on four lanes or more, dashed below; edge lines on roads 7 m or wider;
  - dashes 3 m on, 4.5 m off, counted from where the road crosses the chunk border, so they carry on into the neighbour;
  - mitred at bends; cut at junction pads, zebras, stop lines and signal junction boxes, and where they would run onto an overlapping carriageway of equal or higher class rank at the same level (K-31).
- **Crossings:** zebras on the 16 marked crossings in the slice; at the signalled junction a zebra outside the pad on each approach and a stop line 1 m behind it on the inbound side (traffic keeps left; the whole width on a one-way road). The box between the signals has no lines.
- **Details by rule** (K-30): speed breakers (raised, banded) on unmarked minor roads about every 90 m; manhole covers in a lane of marked roads about every 55 m; gully grates on both edges about every 30 m; potholes on minor and tertiary roads. Placed by a hash of the way id, identical on every build, and built by the chunk that holds them.
- **Tests:**
  - `RoadMarkingTests` (8): the lane rule; 20 roads' lines within 2 cm of the data; every mark on its carriageway and out of keep-outs; zebras on the mapped crossings; stop lines inbound and keep-left; dashes across borders; repeatable details.
  - 9 mutations caught. `BuilderAllocationTests`' roads limit tightened to 1× so that its old mutation still fails.
  - EditMode **73 / 73**, PlayMode **25 / 25**.
  - 15 goldens: `signal-junction` new, 14 re-accepted with the markings.
- **Found by the new golden and fixed with tests:** short dashes inside the signal junction box, and edge lines crossing where twin carriageways' estimated widths overlap (5 % of slice line length before the rule).
- **Figures:** see PERFORMANCE_REPORT (D-04).
  - Slice road vertices 14,843 → 84,183; no new draw calls.
  - GPU over the slice p50 6.60–7.18 ms, inside the earlier spread; p95 up in two of four runs. Street level: GPU p50 7.5–8.1 ms, max 15.0 ms; triangles +41 %.
  - About 2 ms more worker time a chunk in the editor; 1.1 MB more garbage a run, same collections.
- **Windows build:** succeeded, 0 errors, 0 C# warnings, 131.8 MB.
- **New known issues:** K-29 (sizes not checked against BRTA), K-30 (details by rule), K-31 (overlapping estimated widths), K-32 (dash breaks at bends near borders).

**Next:** D-05 footpaths and kerbs: raised sidewalks with kerb faces, ramps at crossings, paving variety, drains, joined to roads and building lines.

## 2026-11-02 — D-03 time of day

**Done**
- **Sun and moon** (ED-6, `Celestial` in `Dad.World`):
  - Meeus' low-precision sun and sidereal time, the Astronomical Almanac's low-precision moon, refraction and the moon's parallax.
  - Positions are for Dhaka (23.8° N, 90.4° E) on an in-game date in Bangladesh time. The default is 2026-01-28: dry season, sunrise 06:40, sunset 17:42, a waxing gibbous moon high in the evening.
- **Light** (`SkyModel`): a pure function of the sun's altitude, the moon and the weather, with no presets.
  - The sun's colour is the transmittance of its air mass (Rayleigh plus a dry-season aerosol), so it reddens and dims at the horizon by itself.
  - Sky, horizon, ambient (trilight), fog, clouds, stars and a night factor are curves over the sun's altitude.
  - Clear, cloudy and overcast.
  - The moon takes the main light at −2°, where the sun's light is already 0; its shadows come in after that.
- **Scene** (`DayCycle`, made by `GameRoot`):
  - sets both lights, ambient, fog and the sky shader's globals each frame (0.07 ms in the player);
  - the clock runs at half a game minute a second in plain play and stands still in benches and golden views;
  - F6 adds an hour, F7 changes the weather.
- **Sky** (`Dad/Sky`):
  - a gradient with a warm side towards a low sun, and haze glow round the sun and moon;
  - one cloud layer from the periodic macro noise, drifting with the clock;
  - a few hashed stars turned by sidereal time;
  - the moon lit from the sun's direction, so its phase and bright limb are right;
  - the sun's disc.
- **Sky reflection** (`SkyReflection`): a 16-texel HDR cubemap filled on the CPU as the default reflection, refilled a face a frame as the clock runs. The first cut refilled a 32-texel cube whole and stalled the main thread for 4.7 ms; the day test's timing caught it.
- **Street-level bench path:** `kawran-farmgate-street`, 966 m along Kazi Nazrul Islam Avenue at 1.8 m. `CameraPathTests` keeps `-street` paths at 1.5–3 m.
- **Tests:**
  - `CelestialTests` (7) reproduce Meeus' worked examples, real new and full moons, and Dhaka's noon altitudes.
  - `SkyModelTests` (6): a day without steps in each weather; the hand-over; the cubemap's face layout, checked on the GPU.
  - `DayCycleTests` (PlayMode, 2): a whole rendered day, from the air and the street.
  - 4 mutations caught. Three first versions were too weak or wrong, and were fixed (TEST_REPORT).
  - EditMode **65 / 65**, PlayMode **25 / 25**.
  - 14 goldens: 7 new (dawn, noon, sunset, dusk, night, overcast, night street), 7 re-accepted with the new sky.
- **Figures:** see PERFORMANCE_REPORT (D-03).
  - GPU over the slice p50 7.27–7.41 ms, inside D-02's spread (6.9–7.9 ms).
  - Street level: GPU p50 8.2–8.4 ms, max 15.4 ms.
  - No allocation per frame; memory as at D-02.
  - Two air runs had 19 and 27 main-thread frames over 4 ms with normal CPU time, while another app was busy (K-18).
- **Windows build:** succeeded, 0 errors, 0 C# warnings, 131.8 MB.
- **New known issues:**
  - K-24: no exposure or tone mapping; the night is stylised.
  - K-25: clouds cast no shadows.
  - K-26: the reflection holds only the sky.
  - K-27: weather changes at once.
  - K-28: benches do not measure refills.

**Next:** D-04 roads, second pass: lane, centre and edge lines from the road data, stop lines, zebra crossings, speed breakers, manholes, wear.

## 2026-11-02 — D-02 material library

**Done**
- **Generator** (ED-2, `Assets/_Project/Editor/Materials`):
  - `TileNoise` provides periodic Perlin, fBm, Worley cells and brick/tile units; `LayerRecipes` holds 16 seeded recipes, one per layer.
  - The layers: asphalt, concrete, brick, plaster, paint, metal, rust, glass, roof sheet, tiles, paving, soil, grass, sand, water and marking paint.
  - Each recipe gives albedo, height, smoothness, metallic and cracks. `MaterialLibrary` bakes normals and occlusion from the height and packs a mask (metallic, occlusion, cavity, smoothness).
  - It writes three 4 × 4 flipbook PNGs, a macro texture and `MANIFEST.json` (means and checksums) in 7.8 s.
  - An import rule turns the flipbooks into 16-slice BC7 arrays.
  - No photo or downloaded texture is used.
- **Shader:**
  - `Dad/World` reads the arrays with world UVs, scaled so each layer's mean is the vertex colour.
  - **Anti-tiling:**
    - Stochastic sampling on a 2 m triangle lattice blends three randomly offset reads (rotated as well for most layers) and keeps the albedo's variance.
    - The lattice repeats every 4,000 m, so it holds across origin shifts (`WorldUvLattice`).
  - **Wear:**
    - A macro texture at 50 and 500 m varies brightness, dirt and fade.
    - Hashed 8 m repair patches mark asphalt and concrete.
    - Each material has its own wear row.
  - **Ids:** `WorldMaterial` grows from 9 to 18 ids, mapped to the 16 layers.
- **Material sheet:** `Docs/ART_DIRECTION/MATERIAL_SHEET.png`, all 18 materials.
- **Tests:**
  - `MaterialLibraryTests` (6, new) and a stricter shader-table test; 2 mutations caught.
  - EditMode **52 / 52**, PlayMode **23 / 23**.
  - 7 goldens: 5 re-accepted, 2 new (`kawran-street` at 1.7 m, `farmgate-mid` at 40 m). No repeat visible by eye at 5–200 m.
- **Figures:** see PERFORMANCE_REPORT (D-02).
  - Texture memory 16.3 MB on the GPU (budget 24 MB).
  - GPU over the slice p50 3.7–4.2 → 6.9–7.9 ms (M1, 1280×720). About 1 ms of that is the texture reads; 2–3 ms is the three-read stochastic blend (K-22).
  - Main thread and apply unchanged (apply frames 0.86–1.13 ms); graphics memory at the end of the path +17 MB.
- **Windows build:** succeeded, 0 errors, 0 C# warnings, 131.8 MB.
- **New known issues:**
  - K-22: GPU cost of stochastic sampling.
  - K-23: soil reads blotchy within about 5 m.

**Next:** D-03 time of day: solar position, sky, sun, moon, ambient and fog curves, night factor, and a street-level bench path.

## 2026-11-02 — D-01 lit world shader and mesh data

**Done**
- **`Dad/World` shader** (ED-1): URP PBR lighting with main-light shadows, Forward+ lights, SSAO and fog. It has ShadowCaster, DepthOnly and DepthNormals passes and is SRP Batcher compatible. `World.mat` replaces the unlit chunk material; the unlit one is kept.
- **Material ids in the mesh:**
  - `WorldMaterial` has 9 ids (Default, Soil, Grass, Water, Paving, Asphalt, Concrete, Plaster, Roof). Each rides in the vertex colour's alpha byte, so the mesh format and the apply step are unchanged.
  - The ground, road and building builders set them. Palettes are now albedo: the baked light factors on buildings are gone.
  - The shader's table gives each id its smoothness and metallic.
- **Normals and UVs:**
  - Normals come from screen-space derivatives of the world position. That is exact for these flat-faceted meshes.
  - World UVs are in metres: plan view on flat faces, box-projected on walls. Projecting along the wall turned derivative noise into metres of UV noise at kilometres from the origin.
  - `FloatingOrigin` publishes the origin offset wrapped to 4,000 m (`_DadWorldWrap`), so UVs hold across origin shifts.
- **Shadows:**
  - Buildings cast, with front faces culled in the shadow pass. They are closed solids, so this gives no acne without a normal bias.
  - Ground and roads receive only. Elevated decks cast nothing yet (K-20).
  - Shadow distance went from 50 to 150 m, with 4 cascades.
- **Fog:** exponential squared, density 0.0011, grey-blue.
- **Debug views (F5, `WorldShading`):** lit, normals, material ids, metre grid, albedo. Views take a `shading` field; the new `farmgate-grid` golden uses it.
- **Render warm-up:** two hidden cameras draw the streamer's made-up chunk before play. On a cold Metal cache this removed a 300 ms first lit frame and a 102–108 ms frame for the variant without shadows.
- **Bench additions:**
  - batches, SetPass calls, triangles and shadow casters per frame (means of 10-frame blocks);
  - graphics memory, peak and end;
  - a warning when the frame clock stood still.
- **Tests:** `WorldMaterialTests` (5, new) and changed ground and streamer tests. 3 mutations caught. EditMode **46 / 46**, PlayMode **23 / 23**. 5 goldens re-accepted and passing.
- **Figures:** see PERFORMANCE_REPORT (D-01).
  - GPU over the slice p50 1.56 → 3.96–4.17 ms (M1, 1280×720).
  - Main thread unchanged. The final run had no frame over 4 ms after set-up.
  - Apply frames 0.79–1.18 ms; graphics memory peak 185–237 MB.
- **Windows build:** succeeded, 0 errors, 0 C# warnings, 115.4 MB.
- **New known issues:**
  - K-20: road decks cast no shadows.
  - K-21: box-projected wall UVs stretch up to 1.41x at 45 degrees.
- **Art reference:** the owner's character and vehicle brief is kept in [../ART_DIRECTION/](../ART_DIRECTION/README.md) for Phase E and F. Read as text; its hotlinked images are not stored.

**Next:** D-02 material library: generated texture arrays sampled with the world UVs, about 16 base materials, texture memory measured.

## 2026-11-02 — Phase C closed; Phase D planned

**Done**
- **Phase C closed** by the owner's decision (option 1 of the re-check): the 4 ms rule is read on the streaming markers and the main thread's own CPU time. The 0–2 frames a run in which the main thread was not running stay open under K-18.
- **Phase D plan:** [ENVIRONMENT_ROADMAP.md](ENVIRONMENT_ROADMAP.md), from the owner's environment and visual production directive. It holds the audit (storage, rendering, what the slice data has and lacks), decisions ED-1 to ED-7, and milestones D-01 to D-14 with exit evidence. Phase D is re-estimated at 49–81 days.
- **Fonts:** 9 Bengali font files (8 families, OFL 1.1, 3.3 MB) downloaded with the owner's approval into `Art/Fonts`, each with its licence, source and checksum in `Art/Fonts/README.md`. Not imported into Unity yet: signs are rasterised outside it (ED-3).
- **Names:** the owner rejected হাজী বিরিয়ানি ঘর as too close to a real restaurant brand; the biryani house gets an invented name (ED-7).

**Next:** D-01 lit world shader and mesh data, then D-02 material library and D-03 time of day.

## 2026-11-02 — C-07 builder allocation pass

**Done**
- **Scratch reuse:**
  - `BuildScratch` holds the builders' working memory: mesh and polygon lists, Earcut's nodes, the road builder's outlines, point lists and dictionaries.
  - Each streaming load rents a set for its whole build, so there are only as many sets as loads in flight.
  - Run allocation: 22.4 → 9.6 MB; collections 29–30 → 10–11.
- **Mesh array pools:**
  - Road, building and line arrays come from `MeshArrays` when the streamer builds them. Apply steps upload the counted part and hand them back.
  - Chunks discarded or dropped mid-apply hand theirs back too. Built anywhere else, the arrays stay exact.
  - Run allocation: 9.6 → 8.8 MB; the later batches fell 6–35%.
- **Apply hold:** finished chunks wait while other loads still build, for at most 30 frames (`MaxApplyHoldFrames`).
- **Warm-up round trip:** the made-up chunk is built on 3 worker tasks. That removed 0.94 ms of JIT and 0.76 ms of thread-pool start-up from the first real batch. Warm-up is 105–123 ms.
- **Bench additions:**
  - Main-thread frames over 4 ms after set-up, how many were near streaming, and the first 20 (`row:ms/cpu`).
  - The main thread's own CPU time per frame (macOS); `streamingPlanMs`.
  - `Dad.Streaming.Collect` and `Start` markers; apply-hold counts.
  - `CaptureReport` writes `jit.tsv`.
- **Tests:**
  - New: `BuilderAllocationTests` (3 mutations caught) and `FinishedChunksWaitForLoadsStillBuilding` (1 caught).
  - EditMode **41 / 41**, PlayMode **23 / 23**; goldens exact (0.0).
- **Windows build:** succeeded, 0 errors, 0 C# warnings, 115.4 MB.
- **Exit check re-run:** [PHASE_C_EXIT_REVIEW.md](PHASE_C_EXIT_REVIEW.md), re-check.
  - The streaming work passes: apply frames 0.9–1.2 ms, and 0 apply steps with a collection in 8 runs.
  - 0–2 frames a run still pass 4 ms. In them the main thread was not running: it used its usual 1.6–2.2 ms of CPU, with no collection and nothing allocated.
  - One fell within 3 frames of an apply batch. No capture caught one.

**Not done**
- Apply steps still allocate 2.7–7.6 KB a frame on the main thread (new GameObjects, meshes, names). Removing that needs pooled chunk objects.
- Decode (1.6 MB per 12 chunks) and the roads' per-road arrays (1.0 MB) are not pooled.

**Next:** the owner's reading of the Phase C 4 ms rule (three options in the re-check), then Phase D (D-01 building kit; downloads need approval).

## 2026-11-02 — Phase C exit review

**Done**
- **Review written:** [PHASE_C_EXIT_REVIEW.md](PHASE_C_EXIT_REVIEW.md).
  - Three criteria are met: alignment, metro stations, and the free camera with debug views.
  - The 4 ms rule is met by the streaming work (busiest apply frame 0.87–2.22 ms in 4 runs) but not by the whole frame.
  - Phase C stays open until C-07.
- **Profiler captures in batch mode:**
  - `Tools/capture-report.sh` loads `capture.raw` in the editor without a window (`CaptureReport`) and writes every marker per frame to `markers.tsv`.
  - `Tools/slow_frames.py` then lists the frames over a busy limit, with their largest markers.
  - The bench captures 2,000 frames, the most the editor keeps, from any start frame (`--capture-from`).
- **K-18, first-call compilation fixed:**
  - The Mono player compiled each streaming method on its first call, up to 3.46 ms at once.
  - The warm-up now compiles 1,139 methods ahead (`ChunkStreamer.PrepareCode`). Warm-up: 32 → 82–86 ms.
  - The first version also compiled array, delegate and runtime methods. That broke the Mono runtime: 180 "out of sync" messages, and one run ended early. Those are now skipped, with 0 messages in the 4 final runs.
- **K-18, the rest attributed:**
  - The bench's own set-up frames.
  - Garbage collections from the builders: 2–7 a frame before each apply batch. One `GC.Collect` of 2.21 ms landed inside an apply step, and one frame reached 5.73 ms.
  - One 9.85 ms frame not caught by any capture.
- **K-11 explained:** a capture caught a 575.5 ms frame in which both the main and render threads waited (`WaitForTargetFPS`, `Semaphore.WaitForSignal`). It is a stall in presentation, not game work. `frameMs` can read 16.7 ms through one, so the bench now also counts long frames by CPU frame time.
- **Bench summary additions:** how many apply steps met a garbage collection, and the slowest of them; methods compiled ahead; `cpuFramesOver33_3ms`. The PlayMode walk logs the same.
- **Roads spot check:** `RoadBuilderTests.TwentySliceRoadsSitOnTheirData`. 20 slice roads sit on their data to within 1 cm on both sides (36 of 40 sides checked outside; 4 meet another road). Checked to fail with each side 2 cm wider.
- **Tried and removed:** reserving 64 MB of managed heap at start. Collections stayed at 30.
- **Tests:** EditMode **41 / 41**, PlayMode **21 / 21** twice. One earlier full run in the review failed the walk with a 19.97 ms editor apply step (K-19, new). The 6 full runs since have passed.
- **Windows build:** succeeded, 0 errors, 0 C# warnings.

**Not solved:** whole-frame main-thread spikes over 4 ms (5.73 and 9.85 ms in 2 of 3 unprofiled runs). K-11 stalls up to 610 ms, outside the game.

**Next:** C-07, builder allocation pass, then the exit check again.

## 2026-11-02 — C-05 free camera and debug views

**Done**
- **`FreeCamera`** with its own input context and map. WASD, the arrows or the left stick fly along the view; the mouse or right stick looks; E / Q or the shoulders climb and sink; Shift or a stick press goes fast. Height stays between 2 m and 1,500 m. Until Phase E brings a protagonist, plain play (no bench or shot) starts in it, 95 m over the south-west of the Farmgate junction and looking at it.
- **`DebugViews`** reads F1–F4 from the Global map. F1 goes in and out of the free camera and returns to the context underneath. F2 shows the chunk grid and ids, F3 the road graph, and F4 the C-01 debug lines (building outlines and road centrelines), which are now off by default.
- **`ChunkDebug`** builds on the streaming worker. Each chunk gets its 500 m square and its id in the south-west corner, in a 7-segment line font (no font asset), plus every graph edge, a cross on every node and a square on every signalised node. The new `Dad/Debug Lines` shader draws them over everything. `ChunkStreamer.SetOverlays` applies the switches to resident chunks, to chunks still being applied, and to later ones.
- **Golden views:** new `farmgate-debug`, from 420 m with all overlays on. `kawran-bazar`, `farmgate` and `kawran-metro` were re-accepted without the debug lines. All four were checked by eye; re-renders 0.000.
- **Tests:** EditMode **40 / 40**, PlayMode **21 / 21**. Caught on purpose: overlays not re-applied to resident chunks (1 test fails), not applied to new chunks (1), mouse look read as a stick (1), a wrong glyph (1). Not caught, by design: removing the context checks in `FreeCamera` and `DebugViews`. Those maps are already off outside their contexts (`GameInputTests`), so the checks are a second guard.
- **Test hygiene:** the first mutation run failed 4 unrelated tests. A failed streaming test skipped its own clean-up and left a `FloatingOrigin` behind. Now every PlayMode fixture unloads what its test loaded in `[UnityTearDown]` (`TestScenes`). It never unloads the runner's own scene: an attempt that did hung the run, and that run was stopped. With the fix, the same mutations fail only their own tests.
- **Tools:** `shot.sh` and `bench.sh` no longer hold a caller's pipe open for the watchdog's 90 / 180 s. A piped shot now takes 6 s.
- **Bench:** max apply frame 1.52–1.65 ms, so the 4 ms rule holds; the overlay step itself takes at most 0.10 ms.
- **Windows build:** succeeded, 121 MB, 0 C# warnings.

**Not solved:** K-11 grew. Run 3 had a 199.8 ms frame about 47 s in, and runs 2 and 3 had CPU frame times of 572–600 ms late in the path; the C-04 runs peaked at 45 ms. All of it is outside apply frames. K-18 now also shows 11.6–12.2 ms main-thread frames late in the path. Both go to the profiler capture in the Phase C exit review.

**Next:** Phase C exit review: a profiler capture for K-11 and K-18, then evidence against each exit criterion.

## 2026-11-02 — C-04 buildings

**Done**
- **`BuildingRules`:** a port of `infer.ts` (levels, elevation, base, each with its source) that passes the browser build's own test cases. The browser build draws every building from the ground. This port uses the base (min_height, min_level) and adds one labelled rule: a `building=train_station` with only a `layer` stands on the generator's layer height, 6 + (layer − 1) × 5 m. City-wide, that is exactly the 16 Dhaka Metro Line 6 stations. The rule reads only the building's own tags, so both pieces of a station cut by the chunk grid (Farmgate) agree. The other 869 layer-only pieces stay on the ground. Tops: a measured or levels height is the top above ground; a class default is the body height above the base.
- **`ChunkBuildings`:** walls facing out (or into courtyards), flat roofs and undersides for raised buildings. Grey-box tones: one of six plasters per building, shaded by a fixed light, one roof tone. A new apply step in `ChunkStreamer`.
- **Acceptance:** Farmgate and Kawran Bazar stations stand at 16–28 m. Kawran Bazar's stands over Kazi Nazrul Islam Avenue (by name). Farmgate's stands over the trunk road, which OSM leaves unnamed or calls Khamar Bari Road there, with a footbridge 9.1 m below it. **20 slice buildings sit on their data:** worst corner 0.02 mm, base, top and roof area exact. City-wide: 519,407 of 519,573 pieces drawn, 166 clipping slivers under 0.01 m² skipped, 0 roofs failed, 3.1 s for the city.
- **Golden views:** new `kawran-metro` (on the avenue, looking at the station over it); `kawran-bazar` and `farmgate` re-accepted with buildings. All three checked by eye; re-renders exact.
- **Tests:** EditMode **37 / 37**, PlayMode **17 / 17**. Caught on purpose: flipped walls (1 test fails), no station rule (3 fail).
- **Bench:** max apply frame 1.12–1.32 ms, so the 4 ms rule holds. GPU p50 about 1.5 ms.
- **Windows build:** succeeded, 121 MB, 0 C# warnings.

**Not solved:** K-11, the single missed frame near frame 660, in 2 of 3 runs. New K-18: main-thread frames of 4.6–12.1 ms, not inside the apply marker, around the first chunk loads; it needs a profiler capture before the Phase C exit.

**Next:** C-05 debug views: free camera, chunk grid and ids, road graph, and a toggle for the debug lines.

## 2026-11-02 — C-03 roads

**Done**
- **`RoadBuilder` (`ChunkRoads`):** carriageways from the DADC centrelines, ported from the browser build's `roads.ts` and `meshes.ts`. It chains ways that meet end to end and trims short stems. It adds crossing pads where roads meet and fillets gentle bends. Outlines are mitred, with a bevel past 2.2 × the half width. Roads 3 m or wider (half width) get sidewalks. Bridge decks at 2.5 m or higher get barrier walls on both sides and a soffit. Footbridges are left out.
- **Chunk borders:** each road end on a border is cut square to the border, never narrower than the road. Ends that cross the border at a shallow angle are extended 2 cm past it instead. City-wide: 22,704 border ends form 11,349 pairs (6 have no partner). Of 9,917 cut pairs, 8,196 meet within 1 mm and 44 differ by more than 5 cm (worst 1.22 m, a bend on the border; K-14). A sampled coverage test finds **0 gaps in 204,282 samples**.
- **Road apply step** in `ChunkStreamer` (one mesh per chunk, vertex colour, same shader as the ground).
- **Golden views:** `kawran-bazar` re-accepted with roads after a check by eye: flyover deck, Sonargaon roundabout, junction pads, no seams. The new `farmgate` view shows the junction, an elevated deck with barriers, and the edge of the load ring. Re-renders of both match exactly.
- **Tests:** EditMode **32 / 32**, PlayMode **17 / 17**. Caught on purpose: with no border cut, 2 tests fail; with no pads, the Farmgate test fails (and junction ring coverage drops to 3,295 of 3,296).
- **Bench:** max apply frame 0.79–1.41 ms in 3 runs, so the 4 ms rule holds with roads. The apply step itself is at most 0.11 ms.
- **Windows build:** succeeded, 118 MB, 0 C# warnings.

**Not solved:** K-11 is unchanged: single long frames outside apply frames (frame 662–663 in 2 of 3 runs), plus three more in one run (42–191 ms), with ordinary main and GPU time.

**Next:** C-04 building extrusion. The two metro stations in the slice (Farmgate and Karwan Bazar) are tagged `layer=3` with no `min_height`. So `buildingBase` returns 0 for them, and C-04 needs a labelled rule for their underside.

## 2026-10-30 (night) — C-02 ground and landuse

The owner authorised autonomous work through the roadmap (milestone after milestone, local commits). The ledger is [DEVELOPMENT_LEDGER.md](DEVELOPMENT_LEDGER.md).

**Done**
- **`Earcut`:** a C# port of the mapbox earcut algorithm (the one three.js uses). Over every ground polygon in the city (5,244), the triangles cover exactly the rings' area: 55.3723 km² both. 21 polygons give no triangles; all have zero area (clipping slivers).
- **`ChunkSurfaces`:** base plate, landuse, parks, water, canals and plazas in one vertex-coloured mesh per chunk, on fixed layer heights. Park, water, canal and plaza colours and heights are the browser build's; landuse colours are this port's own (the browser build draws no landuse). Swimming pools and aquaculture draw as water.
- **Shader `Dad/Vertex Color Unlit`** (URP, SRP Batcher compatible) and a ground apply step in `ChunkStreamer`.
- **Golden views:** `ViewShot` (`-dad-shot`) and `Tools/shot.sh`. The first golden, `kawran-bazar`, was checked by eye: Hatirjheel water east of Kawran Bazar, parks north-west, a pool in the compound south-east, outlines on the ground. Re-renders match exactly (mean difference 0.000).
- **Tests:** EditMode **28 / 28**, PlayMode **17 / 17**. Caught on purpose: a changed water colour fails the golden (4.26 % of pixels changed, limit 0.5 %); skipping holes in Earcut fails 2 tests.
- **Bench:** chunk apply stays within budget, max apply frame 1.48–1.83 ms in 4 runs; the ground step costs at most 0.16 ms.
- **Bench recorder:** now records garbage collections per frame (`gcCollections`).
- **Windows build:** succeeded, 0 C# warnings.

**Not solved:** K-11. Single missed frames recur about 1 s after the second batch of chunk applies (frame 660–668 in all 4 runs). They are not GC collections (those land in the apply frames), and main, render and GPU times stay ordinary.

**Next:** C-03 road meshes.

## 2026-10-30 (night) — C-01 streaming driver

**Done**
- **`StreamPlanner`** (pure C#, `Dad.World`): Chebyshev rings with hysteresis (load 2, unload 4), a lead cell from velocity, nearest-first priority, at most 3 loads in flight, one retry before a cell is marked dead.
- **`ChunkStreamer`:** reads and decodes chunks on a worker `Task` with a cancellation token. Loads that leave the rings are cancelled and never applied. Finished chunks are applied in steps, within 2 ms a frame. Chunks are placed at their cell corner through the floating origin; teleports (jumps over 200 m) do not set a lead.
- **`ChunkOutlines`:** building outlines and road centrelines as line meshes, a debug view that proves the data lands in the right place. Real meshes come in C-02..C-04.
- **Warm-up:** the first apply in a player took 3.2–4.2 ms (over 4 ms in 2 of 4 runs), from first-use cost of mesh and renderer creation. `ChunkStreamer.WarmUp()` builds and destroys one tiny chunk at start (2.6–4.3 ms, before play). After it, max apply frame 1.37–1.94 ms in 3 runs.
- **Bench path `slice-crossing-air`** (60 s, west to east across the slice) and streaming fields in `summary.json` and `bench.sh`.
- **Tests:** EditMode **21 / 21**, PlayMode **17 / 17**. The walk test fails with unloading disabled.
- **Windows build:** succeeded, 121 MB, 0 errors, 0 C# warnings.
- **Storage:** project 2.8 GB (Library 2.2 GB, builds 0.32 GB, evidence 0.32 GB); about 16.1 GB in total.

**Not solved:** a single 380–420 ms frame in 3 of 8 slice runs, cause unknown (K-11).

**Next:** C-02 ground and landuse mesh per chunk.

## 2026-10-30 (night) — B-08 markers, camera path and the empty-scene baseline

**Done**
- **Markers:** `DadMarkers` names `Dad.FloatingOrigin.Shift`, `Dad.Input.Context`, `Dad.CameraPath.Apply` and `Dad.Bench.Sample`.
- **Camera path:** `CameraPath` (Catmull-Rom, data in `Resources/CameraPaths`) and `CameraPathPlayer`. The first path, `farmgate-shahbag-air`, is 40 s long and crosses the origin threshold once.
- **Recorder:** `FrameRecorder` uses preallocated buffers, `FrameTimingManager` and memory recorders.
- **Runner:** `BenchmarkRunner` starts on `-dad-bench`. `Tools/bench.sh` builds the macOS development player, runs it with a watchdog and prints the summary.
- **Floating origin:** `FloatingOrigin.Recenter` for start and teleport. Only one origin may be active.
- **Baseline (empty scene, M1 development player, vsync on):** main thread p50 1.7 ms, p95 2.1 ms; GPU p50 1.5 ms; 2 frames over 20 ms in 2,398; 264 B of GC; 473 MB system memory. Capture saved, 106 MB.
- **Tests:** EditMode **12 / 12**, PlayMode **14 / 14**. The marker test fails with a marker removed.
- **Bugs found and fixed on the way:**
  - The player paused when not the front window.
  - The frame buffer filled before the path ended, and the run did not say so.
  - Start-up work was counted in the first sample: a 342 ms frame and 560 KB, which were the recorder's own buffers.
  - "Over 16.7 ms" was meaningless with vsync on.
  - Two floating origins fought each other.
- **Storage:** about 16.1 GB in total. The project is 2.8 GB (Library 2.2 GB, builds 0.32 GB, evidence 0.29 GB, most of it one capture).

**Next:** Phase C, C-01 streaming driver on the Farmgate slice, measured against this baseline.

## 2026-10-30 (night) — B-06 input contexts

**Done**
- **`DadInput.inputactions`:** replaces the template asset and keeps its guid, so it stays the project-wide actions.
  - Global: Pause on Escape, P and Start.
  - OnFoot: 11 actions. Driving: 9 actions; this is the binding layer only, with no vehicle gameplay.
  - UI: the template's map, plus Start as Cancel.
  - Control schemes: Keyboard&Mouse and Gamepad.
- **`GameInput`** keeps a context stack:
  - On foot and driving use Global plus their own map. Menu and paused use UI only.
  - Pause pushes Paused, and Cancel in Paused pops it.
  - A gamepad lost during gameplay pauses.
  - Rebinding stub: interactive rebind, plus saving and loading overrides.
- **`GameRoot`** creates `GameInput` from the project-wide actions.
- **Tests:** EditMode **9 / 9**, PlayMode **11 / 11**. Two deliberate bugs were each caught; details are in the test report.
- **Windows build:** succeeded again.
- **Test-writing lessons from failures in my own tests:**
  - In a `[UnityTest]` the fixture only queues events, so yield a frame after each press.
  - An interactive rebind waits 0.05 s for a better match, so advance the fixture's `currentTime`.

**Next:** B-08 profiler markers and a fixed camera path, then Phase C streaming.

## 2026-10-30 (night) — B-05 coordinates and floating origin

**Done**
- `Dad.World/WorldGrid.cs`: `ChunkCell` (id ↔ Three.js ix/iy ↔ Unity cell ↔ corner metres, half-open cells), `WorldFrame` (UTM 46N), `OriginOffset` (world double ↔ scene float, shift to the cell corner).
- `Dad.Core/FloatingOrigin.cs`: shifts all loaded scenes' root objects when the target passes 2 km; event and profiler marker.
- Tests: EditMode **7 / 7**, PlayMode **2 / 2**. Origin precision over 20,000 samples: worst 0.122 mm absolute, 0.241 mm relative.
- Negative check: with the shift line disabled, the PlayMode test fails; restored and re-run, it passes.

**Next:** B-06 input actions, B-08 profiler markers and camera path, then Phase C streaming.

## 2026-10-30 (evening) — Phase B: project, tests and first Windows build

**Done**
- **Licence:** added by the owner. Batch mode now runs.
- **Project:**
  - Settings taken from Unity's URP template, created in a scratch folder in 98 s.
  - Copied in: ProjectSettings, `Assets/Settings` (URP assets) and the Input System actions.
  - Removed packages: collab-proxy, multiplayer center, visual scripting, timeline and AI navigation.
  - Product name set to DHAKA AFTER DARK.
- **Boot scene:** written by `ProjectTools.CreateBootScene`. `GameRoot` decodes the Farmgate chunk on a worker thread.
- **Tests:** EditMode **3 / 3**, PlayMode **1 / 1**, both through `Tools/run-tests.sh`.
  - The runner exits 1 on a failing test (seen on the first PlayMode run, which had a test bug) and on zero tests.
- **Windows build:** x64 Mono **succeeded**, 118 MB, 0 errors and 0 warnings. Not run (no Windows machine).
- **Disk:** about 15.4 GB in total, including 2.0 GB of scratch template projects.

**Next:** B-05 coordinates and floating origin, B-06 input, B-08 profiling, then Phase C.

## 2026-10-30 (later) — Phase B: Unity installed, C# reader verified

**Done**
- **Install** (owner approved):
  - Hub 3.22.4 via Homebrew from Unity's CDN.
  - Editor 6000.3.27f1 arm64 plus Windows Build Support (Mono), through the Hub CLI.
  - The release API listed 10.6 GB, under the 11 GB gate.
  - Measured with `du`: 10 GB of editor (0.98 GB of it Windows support) and 0.54 GB of Hub.
  - Disk use settled at +11 GiB; mid-install it was +19 GiB (one sample).
- **Licence check:** `Unity -batchmode -createProject` exits with "No valid Unity Editor license found" (access token unavailable). It needs the owner's sign-in (K-01, B-01b).
- **C# reader:** the `Dad.World` DADC reader (`Assets/_Project/Runtime/World/Dadc.cs`) and asmdefs.
  - Compiled with Unity's bundled Roslyn (C# 9) and run on its bundled Mono by `Tools/dadc-check.sh`.
  - **PASS** on all 1,303 chunks against new per-chunk check totals in `world.json` (dhaka-after-dark cd3035d).
  - 33 of 33 corrupt inputs rejected; decode p50 0.061 ms.
- **Tests written, not yet run:** the EditMode `DadcReaderTests` (3 tests) and `Tools/run-tests.sh`.

**Next:** B-01b (owner), then B-02.

## 2026-10-30 — Phase A: audit, research, pipeline

**State verified at start**
- `dhaka-after-dark`: `main` = 0bb9077 (PR #2 merged by the owner; tree equals `dev/engine-core` b326a19).
- `public/world` is schema v2 with 1,303 chunks (not 1,298: v2 added five chunks that hold only v2 features).
- Worktree `~/Documents/dhaka-after-dark-p1` is at `dev/engine-core` b326a19, clean.
- Worktree `~/Documents/dhaka-after-dark-arch` holds branch `dev/architecture` (unpushed).
- Machine: M1, 8 GB RAM, 76 GB free. No Unity editor, .NET or Git LFS.

**Done**
- **Storage budget:** 17.8 GB steady, 22.8 GB peak during install; a gate before installing. See PERFORMANCE_REPORT.md.
- **Engine version:** Unity 6.3 LTS chosen (ADR-U1), with its constraints: Mono-only Windows builds from the Mac, and the 8 GB RAM minimum.
- **World export to Unity** in `dhaka-after-dark` `dev/architecture`:
  - 7b4ea2c: DADC lossless binary chunks, exporter, verifier, 5 tests.
  - 2ebf234: places and landmarks.
  - All 1,303 chunks PASS; 93.2 MB → 19.3 MB; deterministic.
- **Data in place:** `Tools/sync-world-data.sh` puts the data in `Assets/StreamingAssets/World` and writes `WorldData.lock` (source 2ebf234, clean, `world.json` 810d2e7f…).
- **Docs:** architecture, pipeline, performance, roadmap, backlog, known issues, test report, release checklist, original game design, master GDD.
- **Research:** Vice City design research and a 24-feature translation matrix, with 27 sources, all opened by the research pass. 19 of them are fan wikis, labelled as such and used only for feature lists. IGN, GameSpot, Eurogamer and GameSpy returned 403, so their views come through Wikipedia; they are listed for a later pass.
- **Spot checks:** the Benzies interview (difficulty spikes; businesses and the empire story) and the 2003 controversy (Wikipedia) were re-checked. One quote was corrected to the source's wording.
- **Design changes from the research:** no police-bribe exits, and no bribe story choice (Choice 1 is now Kamrul or Faria); religious sites are no-combat zones; no killing-spree challenges.

**Not changed:** the three.js game, `public/world`, `main`, backups and worktrees.

**Next:** B-01, installing Unity, which needs the owner's approval and Unity sign-in. Until then: more Node-side pipeline work that is dependency-ready (none is required for B-01).

## Disk use log

| Date | Unity workspace (`du -sh ~/Documents/dhaka-after-dark-unity`) | Hub, editor and caches | Total vs 25 GB |
|---|---|---|---|
| 2026-10-30 | 22 MB (world data) + docs | 0.34 GB (old Hub settings) | ≈ 0.4 GB |
| 2026-10-30 after install | 22 MB + docs | Hub 0.88 GB, editor 10 GB | ≈ 10.9 GB |
| 2026-10-30 after project + build | 2.1 GB (Library 1.9 GB, Builds 118 MB) | Hub 1.3 GB, editor 10 GB; scratch templates 2.0 GB | ≈ 15.4 GB |
| 2026-11-02 after D-04 | 4.8 GB (Library 2.3 GB, Evidence 2.2 GB, Builds 352 MB) | Hub app 0.54 GB, editor 10 GB; scratch templates not re-measured | ≈ 15.3 GB plus the templates |
| 2026-11-03 after D-05 | 5.1 GB (Library 2.3 GB, Evidence 2.4 GB, Builds 353 MB) | as after D-04 (not re-measured) | ≈ 15.6 GB plus the templates |
| 2026-11-03 master directive audit | 5.6 GB (Library 2.3 GB, Evidence 2.7 GB, Builds 353 MB) | Hub app 0.54 GB and data 0.35 GB, editor 10 GB, editor cache 0.12 GB | ≈ 16.6 GB; ≈ 18.9 GB with the Three.js repositories and worktrees (2.3 GB) |
| 2026-11-03 after D-06a | 5.4 GB (Library 2.3 GB, Evidence 2.6 GB, Builds 353 MB) | as at the audit (not re-measured) | ≈ 16.4 GB; ≈ 18.7 GB with the Three.js repositories and worktrees |
| 2026-11-03 after D-06b | 5.5 GB (Library 2.3 GB, Evidence 2.6 GB, Builds 353 MB) | as at the audit (not re-measured) | ≈ 16.5 GB; ≈ 18.8 GB with the Three.js repositories and worktrees |
| 2026-11-03 AXIOM v3 audit | 5.5 GB (Library 2.3 GB, Evidence 2.6 GB, Builds 353 MB) | as at the audit (not re-measured) | ≈ 16.5 GB; ≈ 18.8 GB with the Three.js repositories and worktrees |
| 2026-11-04 D-11a | 8.3 GB (Library 2.4 GB, Evidence 3.6 GB, Builds 571 MB: the macOS, HEAD macOS and Windows players) | as at the audit (not re-measured) | ≈ 19.3 GB; ≈ 21.6 GB with the Three.js repositories and worktrees |
