# K-37 P-6: median islands under Line 6 piers on a carriageway

The owner decided this on 2026-11-04 ([WORLD_TRANSFORMATION_PLAN.md](WORLD_TRANSFORMATION_PLAN.md), section 3a, decision 3, "Draw medians"):

> A raised median island is built under piers where the road is wide enough, with traffic passing on both sides. The source road data is untouched, and each case goes in the register. Where the road is too narrow, the pier stays listed and is reported.

It is row 1b of the plan's section 4. Branch `wt/p6`, cut from `main` at `79cc798` (D-11a).

## What was built

A viaduct pier whose foot is on a drawn carriageway now gets a **raised median island** under it where the rules below allow. The island is the D-11a flyover island, sized round the viaduct's wider pier:

| | Viaduct island (P-6) | Flyover island (D-11a, unchanged) |
|---|---|---|
| Pier radius | 0.9 m | 0.8 m |
| Across | 3.0 m (0.6 m each side of the pier) | 2.8 m |
| Along the deck | 7.8 m (3 m past the pier each way) | 7.6 m |
| Form | yellow kerb, concrete top 0.48 m up | the same |

**The pier never moves,** and no road, rail, building or chunk file is changed. The island is drawn on the road surface as it is drawn. Where an island stands on a divided road, the twin carriageways' divider stops either side of it.

## The rules

An island is built under a viaduct pier on a carriageway only if every one of these holds. Otherwise the pier stays where it is, on the carriageway, and the builder records why (`on a carriageway; no island: <reason>`). The checker lists it as before.

| Rule | Reason when it fails |
|---|---|
| The whole island lies inside the chunk: the chunk cannot see its neighbour's roads | border |
| No part of it, sampled every 0.5 m, is inside a building on the ground. A raised building (a station body on its layer, or one with a mapped base) has nothing at street level and does not count | building |
| It keeps its half width (1.5 m) clear of every junction pad, zebra or stop-line keep-out and bus stop pad, along its whole length and past its ends | junction, crossing, bus stop |
| It does not overlap another island in the chunk | island |
| At its centre and both ends, no road crosses under it (more than 30° off the deck) and no road ends under it | crossing road, road end |
| Every carriageway under it keeps a lane of at least 3 m on each side of it. On a one-way road a side less than 0.25 m wide counts as covered | narrow |
| **Traffic passes on both sides** (the owner's wording): at its centre and both ends, a lane of at least 3 m on each side of the island, of a carriageway under it or of one along the deck whose edge is within 1 m of the island's edge (an open strip between twin carriageways) | one side |

The last rule is where P-6 differs from D-11a. A D-11a flyover island may cover one side of a one-way road and keep a lane on the other side only, so traffic passes it on one side. The owner's P-6 wording rules that out for the viaduct. **This was first built with the D-11a rule.** Line 6 then had 176 islands. The register showed most of them standing against the kerb of a one-way carriageway, so the both-sides rule was added before anything was accepted.

## Found and fixed while building it

- **Supports depended on the chunk built before (verified error, D-11a and P-2 code).** The viaduct's list of pier places, which flyover supports keep clear of, was cleared only after the viaduct's early returns. So a chunk with no elevated track kept the last viaduct chunk's piers, at that chunk's local coordinates, until the next viaduct chunk on the same worker thread. Measured:
  - built in sorted order, main gives different flyover supports from this branch in 21 chunks (27 supports), one of them in the slice (c_9_-29);
  - between sorted and reverse order, main differs in 20 chunks;
  - with the fix, both orders give identical results in every chunk.
  The player builds chunks on worker threads in streaming order, so on main the same chunk could get different flyover supports from run to run. The checker's slice and Line 6 registers were not affected (their supports are identical before and after the fix). Test: `AChunksSupportsDoNotDependOnTheChunkBuiltBeforeIt`; mutant V13.
- **Overlapping flyover islands (D-11a).** Two flyover islands could overlap, with their tops at the same height (z-fighting). An island now never overlaps another island in its chunk (viaduct or flyover), and the checker has an `island-overlap` check. Measured on all 1,303 chunks against main with only the fix above, this guard changes one place: in c_16_-20 a median (local 190.00, 54.43) becomes a portal frame (legs at 183.94, 37.37 and 190.37, 55.47). City-wide flyover medians go from 493 to 492.
- **Flyover islands drawn into buildings (verified error, D-11a).** A flyover island's pier is kept out of buildings, but the island itself, 7.6 m long, was not. The new `island-building` check found two on main, unchanged by P-6: FP-7710.0-9823.1 (c_15_-20) and FP-6988.9-10265.8 (c_13_-21). Flyover islands now get the viaduct island's ground-building footprint test; both slide about 0.2 m across the deck and stay islands, and the survey of the 461 flyover chunks then has no `island-*` finding. Test: `AnIslandSlidesAcrossClearOfABuildingOnTheGround`; mutant V21.
- **The covered margin moves some flyover islands.** The 0.25 m margin below also applies to flyover islands. Against main with the scratch fix and the guard, it moves islands about 0.25 m across their deck in 7 chunks (c_11_-28, c_11_-43, c_12_-28, c_14_-21, c_15_-20, c_15_-27, c_16_-20). No support changes kind.
- **An edge case between builder and checker.** At K37-18 the builder counted a 0.30 m strip beside an island as covered, and the checker did not: the limit is 0.3 m in both, read from slightly different geometry. The builder now uses 0.25 m, 5 cm inside the checker's limit, as with `RoadMargin`. (K37-18 is now refused for traffic on one side only.)

## Results

### The checker's carriageway findings, before and after

| | Before (main 79cc798) | After P-6 |
|---|---|---|
| Slice: viaduct piers on a carriageway | 38 (40 findings) | **28**; 10 on islands |
| Slice: violations against the known list | 80 | 68, 0 new, 0 gone (the 12 carriageway rows of the islanded piers removed) |
| Line 6 (64 chunks): piers on a carriageway | 354 (357 findings) | **331** (332 findings); 23 on islands |
| Line 6: island or median findings on a viaduct pier | n/a | none |
| City (1,303 chunks, builder count) | 354 on a carriageway | 331, 23 on islands; divider pieces 7,364 → 7,353 (9 gaps at islands) |

Why the 331 have no island (builder's reason, Line 6): one side 153, narrow 139, crossing road 13, border 11, junction 8, road end 4, building 3. Each is listed in the register with its reason ([INFRA/p6-line6/after/](INFRA/p6-line6/after/)); the slice's 38 are listed one by one in [K37_PIER_INVESTIGATION.md](K37_PIER_INVESTIGATION.md), section 11. Builder and checker agree on every one (28 of 28 in the slice, 331 of 331 on Line 6).

**Median clearance:** every viaduct island keeps a lane of at least 3 m each side, counting the twin carriageway within 1 m of its edge (the checker's `median-one-side` and `median-narrow`, violations for the viaduct: none). Islands with 0.00 m on one side of their own carriageway (K37-03 to -06, K37-40 to -42) have the twin carriageway on that side; at K37-05 the gap from the island's edge to the twin is 0.34 m (measured from the chunk data).

### The flyover chunks (461)

Every built support is clear: 6,539 flyover piers (6,537 before the building fix) and 422 portal legs. Against D-11a's survey, two flyover findings change: a long span in c_16_-20 (FD-396086149) is gone: with the scratch fix, the pier at E 8,370 stands on its grid place (main slid it to E 8,367.4, away from a stale viaduct pier of another chunk), and the span to the next pier is under the 61 m limit; and FD-654019445's long span is the same span re-keyed (its end pier moved 0.2 m with the covered margin). No new flyover finding. 291 of the 492 flyover islands carry traffic on one side only (a note, K-54). Register: [INFRA/p6-city/](INFRA/p6-city/).

### Tests

- **EditMode 155 of 155** on the final code, after the goldens were accepted (before, 154 of 155: `EveryViewIsValidAndHasAGolden` named the two new views). **PlayMode 30 of 30.**
- New `ViaductIslandTests` (9 tests): wide road; the 3 m lane limit (9.0 m road gives lanes 3.00, 8.9 m none); an island against one edge refused (one-way: one side; two-way: narrow); twin carriageways with a strip of 80 cm (islands, lanes 4.50) and 150 cm (refused, one side); the chunk border; ground and raised buildings; zebras and junction pads; the divider stopping either side; build order not mattering.
- New in `FlyoverTests`: `AnIslandSlidesAcrossClearOfABuildingOnTheGround`. `InfraSurveyTests`: Line 6 pinned at 23 on islands, 331 left, no island or median violation on a viaduct pier. `RoadWidthTests` and `StreetTests` read the new builder reason.

### Mutations

27 mutants of the builder (21) and the checker (6), each in a copy of the sources, judged by the slice gate, the Line 6 figures and a mirror of the island tests ([INFRA/mutations-p6.txt](INFRA/mutations-p6.txt); script [INFRA/mut_p6.py.txt](INFRA/mut_p6.py.txt), mirror [INFRA/P6Check.cs.txt](INFRA/P6Check.cs.txt)). **20 caught.** 7 missed, each because on today's data, or by construction, the mutant gives the same result:
- V9, bus stop pads not avoided: no island under a viaduct pier is near a bus stop once the both-sides rule holds.
- V10, islands may overlap: viaduct piers are at least 8 m apart and an island is 7.8 m long, so two viaduct islands cannot overlap; flyover islands get the guard too, and it changes one place (c_16_-20), outside the slice and Line 6.
- V14, a road crossing under the island allowed: a crossing road makes a junction pad first, which refuses the island.
- V17, the 0.25 m covered margin removed: no case on today's data (K37-18 is refused for one side anyway).
- S10, S11, S12 (checker: footprint, both sides, beside strip): the checks have nothing to find on the final code. They do fire: V18 gives 153 `median-one-side` findings on Line 6, and S8 gives an `island-crossing`.

The mutation run used the code before the bounding-box prefilter (below), which gives the same output in all 1,303 chunks; V5 and V21 were re-run on the final code and are caught.

### Worker time (K-38)

The first version cost **+16.8 ms a chunk on the slice (+31 %)**: the island footprint was sampled against every building in the chunk, for every flyover island candidate as well. Found by the interleaved timing, fixed by testing each island against only the buildings whose bounds meet its own (same output in all 1,303 chunks). Final, Mono harness, best of 5 passes, interleaved with main, runs 1-3 after a discarded round ([INFRA/worker-p6.txt](INFRA/worker-p6.txt)):

| Chunks | main | P-6 | Change |
|---|---|---|---|
| Slice (12) | 89.10 / 89.54 / 88.62 ms | 90.20 / 89.35 / 89.01 ms | +0.4 ms (+0.5 %), inside the spread |
| Line 6 (64) | 87.61 / 90.21 / 86.91 ms | 89.68 / 88.94 / 88.52 ms | +0.8 ms (+0.9 %), inside the spread |
| Flyover chunks (461) | 60.11 / 62.35 ms | 60.27 / 61.98 ms | −0.1 ms, none |

No change is measured. These absolute times are about 1.6× those of earlier reports (main's slice was 54.9 ms a chunk before the machine restarted on 2026-11-05); only the interleaved comparison is used.

### Goldens

All 46 views rendered with the P-6 player: 37 match, 7 differ, 2 are new. Each was looked at before it was accepted ([P6/golden-review/](P6/golden-review/), with `accepted.txt`):
- k37-karwan-station, kawran-street, m2-divider, m2-viaduct, tod-night-street: the new islands under the avenue's piers.
- k37-side-street: the divider's stripes. Pieces alternate yellow and black by count, so a piece left out at an island flips the phase further along; no piece moved. With main's player the view matches its golden exactly.
- kawran-bazar (aerial): 0.501 % changed; main's player gives 0.459 % against the same golden, so this view sits at the limit either way.
- New: `p6-farmgate-islands` (K37-04 to K37-06) and `p6-divider` (the divider stopping either side of K37-40 and K37-41; re-aimed once, as it first showed an island with no divider near).

### Player bench and builds

- **Bench** ([P6/bench.txt](P6/bench.txt)): main's player (79cc798) and the P-6 player, run for run, 3 air and 3 street runs each after a discarded run of each; lid open, **on battery** (earlier milestones were benched on AC). Air path GPU mean: P-6 4.80–5.21 ms, main 5.00–5.09 ms, no change measured. Street path: one of main's runs was disturbed (406 frames over 20 ms), so no gain is claimed. Street triangles +1–2 % at most (the islands). Garbage, collections and memory unchanged.
- **The slow frames were battery noise (re-run on AC, 2026-11-05, [P6/bench-ac.txt](P6/bench-ac.txt)).** On battery, two of three P-6 air runs had more frames over 20 ms (14 and 39, against 0, 1 and 3 for main). On AC, 6 air runs of each player, in two sets with the pair order swapped: P-6 0–2, main 0–2. Air GPU mean in the normal GPU state: P-6 4.10–4.20 ms, main 4.06–5.17 ms; street 7.58–8.78 against 7.28–8.21 ms. No change measured. Some runs of both players have GPU time raised over the whole path, the start and end included, where P-6 changes nothing (3 of P-6's, 1 of main's); that is a GPU clock or power state, not content.
- **Builds:** macOS development player (Builds/MacBench, used for the goldens and the bench). Windows x64 Mono: succeeded, 138,481,679 bytes, 0 errors, 20 warnings; **not run** (no Windows hardware, K-02).
- **Machine restart:** on 2026-11-05 the Mac restarted mid-milestone and the scratch folder was wiped (the code on `wt/p6` was untouched). The mutation tools were restored from the session record and re-run (same result as before the restart); the worker timing and every survey were re-run after it.

### Not verified

- The Windows build is not run on Windows hardware (K-02).
- Real pier and median positions are not known (K-37 evidence, P-4 and P-5): the islands follow the drawn, mostly estimated carriageway widths (K-31), not surveyed medians.
- K-54 (flyover islands with traffic on one side): the owner decided on 2026-11-05 to keep D-11a's rule ("Keep D-11a rule"); nothing changed, and the cases stay listed as notes.
