# K-37 P-5a: a continuous median under the Line 6 viaduct

The owner decided this on 2026-11-05, answering the plan in [K37_PIER_INVESTIGATION.md](K37_PIER_INVESTIGATION.md), section 16 ("Build P-5a at 3.0 m where both carriageways keep two lanes?"):

> Yes, build it.

So: decision 1, a median 3.0 m wide; decision 2, only where both twin carriageways keep two lanes (6 m or more each). Decision 3, the avenue between Farmgate and Kawran Bazar, was settled by section 17: its piers stay listed. Branch `wt/p5a`, cut from `main` at `88b7a22`.

## What was built

Under the viaduct, a **raised, kerbed median runs along the deck line** wherever one-way twin carriageways pass on either side of it and each keeps at least two 3 m lanes. A viaduct pier whose whole island footprint lies on the median stands on it, and gets no island of its own.

| | Continuous median (P-5a) | Pier island (P-6, unchanged) |
|---|---|---|
| Across | 3.0 m (`ViaductIslandHalf` 1.5 m each side of the deck line) | 3.0 m |
| Along | wherever every 2 m section passes; a piece shorter than an island (7.8 m) between two stops is not drawn | 7.8 m |
| Traffic each side | a lane of at least 6 m (`MedianRunLanes`, two 3 m lanes) | at least 3 m |
| A twin beside it counts | within 6.5 m of its edge (`MedianRunBeside`: 8 m from the deck line, `TwinMax`) | within 1 m |
| Carriageways under it | every one one-way, and none split by it: each keeps its lanes on one side only | one-way or two-way |
| Form | yellow kerb, concrete top 0.47 m up (1 cm under an island's, so an island on it shows), capped at both ends | the same, 0.48 m |

**No pier moves,** and no road, rail, building or chunk file is changed. The median is drawn over what is there, as P-6's islands are. Where it runs between twin carriageways, their divider stops at it and goes on past its ends.

The plan (section 16) described trimming each twin's inner edge to the median kerb. That is not built: the median is drawn over the carriageways as an island is, so the road outlines, kerbs, markings and sidewalks are untouched. The plan's per-side half widths in the road builder were not needed.

**Where the twins do not meet** (found in the golden review, measured after), the median also covers the strip between them. That follows the plan's rule: a twin's near edge within 8 m of the pier line, on each side. The strip is bare ground, or the twins' inner sidewalks.
- **Most of the median is on such a strip.** On Line 6, 713 of the 948 passing deck points (75 %) have the deck line off the drawn carriageway.
- **About a third of its area covers sidewalk paving:** 8,638 of 26,327 samples (0.5 m along, 7 across), 8,448 of them paving and 190 broken slabs. That comes from 17 Line 6 chunks; the most is in c_4_-39, where the east twin's inner sidewalk lies under the median's eastern half for about 250 m.
- **Nothing is cut or deleted.** The median top stands 0.47 m above the road, about 0.3 m above the paving, so the paving is hidden where the median covers it and shows beside it where it is wider. The `p5a-sidewalk` view shows one place at Farmgate, where the median starts with a capped end on such a strip.
- **The evidence fits:** section 15 shows the real piers standing in a kerbed median between the carriageways, not on a sidewalk.
- **The owner's choice (K-55), decided 2026-11-05: "Keep the median".** The median stays over these strips, and the inner sidewalks are not trimmed. The checker does not judge the median against sidewalks.
- Tools and records: [INFRA/p5a_gap.sh.txt](INFRA/p5a_gap.sh.txt) with [INFRA/p5a/off-carriageway.txt](INFRA/p5a/off-carriageway.txt), and [INFRA/P5aWalk.cs.txt](INFRA/P5aWalk.cs.txt) with [INFRA/p5a/sidewalk-overlap.txt](INFRA/p5a/sidewalk-overlap.txt).

## The rules

Every point of a deck run inside the chunk is judged by a **section** 2 m long, centred on it (`RunSection`). The section passes only if all of these hold; a passing section holds the median 1 m either side of its point.
1. A carriageway lies under it, or within the median's half width (1.5 m) of its centre.
2. It is clear of ground buildings (a raised one, a station body, does not stop it), junction pads, bus stop pads, and crossing and stop-line keep-outs, with the island's margins.
3. It does not overlap an island or median part already built for another deck run of the chunk.
4. `MedianFits`, at the section's centre and both ends: no road crosses under it or ends under it; every carriageway under it is one-way and none is split by it; and the widest lane on each side, counting a twin within 6.5 m of the median's edge, is at least 6 m.

**At a chunk border** the median runs on to the border, where the neighbour judges its own side. A section within 1 m of the border is judged inside the chunk only, and a road cut on the border is taken on straight past it. Without this, every seam showed a gap of 2 to 7 m (found while building; below).

**A pier on the median** stands on it if the median holds its whole island footprint (its centre and 3.9 m either way along the deck), and `MedianFits` holds over that footprint with at least 6.05 m each side: `RoadMargin` (5 cm) inside the checker's 6 m, as P-6 does with its lane limit. Its builder reason is `median run`. Otherwise P-6 applies to it as before: an island where the road leaves a lane each side, else it stays on the carriageway with its reason.

## Found and fixed while building it

- **A median down the middle of one one-way road.** The first build accepted any one-way carriageway under the median. At c_8_-29 (E 4223.3, N 14190) it split a single one-way road and left 3.40 m on one side. The plan's model counted only twins, one carriageway on each side, so the builder now refuses a section where any carriageway keeps lanes on both sides of the median (`fitTwins`), and the checker gained `median-split`. That pier is back on its P-6 island.
- **Gaps at every chunk seam.** A section near the border also sampled a cross-section just outside the chunk, where this chunk sees no road, so it failed as "one side". Then, near the border, the twin beside the median is cut on the border, and the foot of a cross-section could land just past its cut end. Both are fixed for the median only (P-6 and flyover islands judge as before). Line 6's three seams with a median on both sides now meet exactly (c_3_-43/-44 at E 1629.6, c_5_-37/-38 at E 2588.2, c_8_-27/-28 at E 4441.8). The median's length on Line 6 went from 1,785 to 1,835 m.
- **An open end at a seam.** In c_8_-29 the median reaches the N 14,000 border, but in c_8_-28 a zebra crossing just past it stops it. The neighbour cannot be seen from the chunk, so every median end is now capped, the border too. Where both sides go on, the two caps meet back to back inside the median.
- **The checker trusted the builder's marker.** A run pier counted as on the median if the builder had put a median item at it. The checker now tests the pier's footprint against the drawn median parts itself (`OnRunParts`).

## Results

### The checker's carriageway findings, before and after

| | Before (main 88b7a22) | After P-5a |
|---|---|---|
| Slice: viaduct piers on a carriageway | 28 | **27**; 7 on P-6 islands (10 before), 4 on the median |
| Slice: violations against the known list | 68 | 67, 0 new, 0 gone (K37-59's carriageway row removed) |
| Line 6 (64 chunks): piers on a carriageway | 331 (332 findings) | **311** (312 findings); 20 on P-6 islands (23 before), 23 on the median |
| Line 6: median or run findings | none | none (`median-one-side`, `median-narrow`, `median-two-way`, `median-split`, `median-missing`, `run-*`) |
| Line 6: the median | none | 37 pieces, 1,835 m |
| City (1,303 chunks, builder count) | 331 on a carriageway, 23 on islands | 311, 20 on islands, 23 on the median; only 13 Line 6 chunks change; divider pieces 7,353 → 7,346 (7 under the median at Kawran Bazar); flyover supports (6,750) and flyover islands (492) unchanged |

**In the slice:**
- **K37-59** (c_8_-28, E 4423.2, N 13560), south of Kawran Bazar, stood 0.03 m into one-way 155271803. P-6 refused it (traffic on one side only: the near edge of its twin, 235293033, is about 5.2 m from the pier line, 3.7 m beyond an island's edge, where P-6 counts a twin only within 1 m). It now stands on the median, with 9.8 m and 10.5 m of lane either side by the plan's model.
- **K37-40, -41, -42** (c_8_-29), the station piers at Kawran Bazar, stood on P-6 islands. They now stand on the median, which runs 141 m there in two pieces (E 4244 to 4289).
- **K37-02** (Khamar Bari Road) does not get it: its twin keeps 4.3 m, one lane, as section 16 said.
- The pier at N 14,190 keeps its P-6 island: the road there is a single one-way carriageway that the median would split.

**Against the plan's model** (`INFRA/p5a_reconcile.py.txt`, output in `INFRA/p5a/reconcile.txt`):
- The model cleared 31 Line 6 piers with both twins keeping two lanes. **19 of them stand on the median.**
- The other 12 do not:
  - 4 keep 5.83 to 5.99 m at the pier's footprint, just under 6 m (the model, which measured at the pier only, gave 6.1 to 6.2 m): c_9_-24 E 4833.0, c_10_-24 E 5409.1, c_11_-24 E 5580.0, c_12_-24 E 6180.0;
  - 3 have a carriageway under the median keeping less than 3 m (`narrow`; the twins overlap there): c_6_-32 E 3311.1, c_9_-26 E 4567.8, and K37-39 (c_8_-29 E 4235.1);
  - 3 have traffic on one side only at the footprint's end: c_5_-35 E 2992.9, c_6_-32 E 3308.9, c_8_-28 E 4441.8 (the last also stands on the chunk border);
  - 2 pass at the pier but the median does not reach over the whole footprint: c_3_-54 E 1803.4 (the second track's median would overlap the first's, rule 3) and c_5_-36 E 2817.6 (the median ends 3.5 m from it, one side beyond).
- **4 piers stand on the median that the model did not clear from the carriageway:** K37-40, -41 and -42 (on P-6 islands before), and c_8_-28 E 4451.1 (the model judged it too narrow; the builder, which measures the drawn twin beside it, finds two lanes).
- So **20 piers leave the carriageway**, not 31. Each of the 311 left has its reason in the register.

### Tests

- **Unity EditMode: 165 of 165** on the final code (156 before), with the 49 views and their goldens.
- **Unity PlayMode: 30 of 30** on an idle machine. A first run at the same commit failed `ChunkStreamerTests.WalkAcrossTheSliceKeepsRingsAndBudget`: one apply frame took 10.64 ms against the 4 ms budget, in a single 9.21 ms step. It ran while Mono probes were compiling and running beside it. The idle re-run gave 2.13 ms, inside the 2.05 to 2.31 ms of the four runs before P-5a. Both runs are listed in the test report.
- **`ViaductMedianRunTests` (new, 9 tests),** made-up chunks:
  - twins that keep two lanes get the median and the piers stand on it, drawn as a closed solid;
  - the two-lane limit, with the pier test's `RoadMargin`;
  - a two-way road, a single one-way road, and twins of which one is two-way get none;
  - it stops short of a crossing road and of a ground building, not of a raised one;
  - a piece shorter than an island is not drawn;
  - it runs to the chunk border and is capped there;
  - it keeps an island's margin from a junction pad;
  - it meets the border where the twins cross it at 45 degrees;
  - a divider stops at it, and the next chunk does not inherit it.
- **`InfraSurveyTests` (changed):** the slice gate (67, all listed) and Line 6: 311 on a carriageway, 20 on P-6 islands, 23 on the median, 37 median pieces, every median pier on a drawn median part, and no `median-*` or `run-*` violation.
- **`RoadWidthTests` (changed):** the slice has 27 piers on a carriageway, 7 on islands and 4 on the median.
- **Mono harness (all seven files above, 67 tests):** 67 of 67, the same tests the mutations use.

### Mutations

31 mutants of the builder (R1 to R24) and the checker (S1 to S7). Each is made in a copy of the sources, and the real EditMode test files are run on it in the Mono harness: `ViaductMedianRunTests`, `ViaductIslandTests`, `StreetTests`, `SidewalkTests`, `RoadWidthTests`, `InfraSurveyTests` (the slice gate and the Line 6 figures) and `FlyoverTests`. On the final code all 67 tests pass. The record is [INFRA/mutations-p5a.txt](INFRA/mutations-p5a.txt), the script [INFRA/mut_p5a.py.txt](INFRA/mut_p5a.py.txt) and the runner [INFRA/TestRunner.cs.txt](INFRA/TestRunner.cs.txt).

The first run caught **24 of 31**. Two of the 7 missed showed real gaps in the tests, so two tests were added and the 7 were run again: **26 of 31 caught.**
- **R9, junction pads and bus stops not avoided (now caught).** `MedianFits` refuses a pad only within 0.3 m of the deck line, so every pad in the tests was refused twice. The new test `TheMedianKeepsAnIslandsMarginFromAJunctionPad` puts a T-junction pad 1 m short of the deck line. With the rule, the median stops 5 m either side of the side road. Without it, it stops 3 m either side (390 m against 394 m). Bus stops share the same line of code and have no test of their own.
- **R14, roads cut on the border not taken on (now caught).** With it, Line 6's median shrinks from 1,835 to 1,785 m: every seam gap comes back. The border test had the track crossing the border square, so a cross-section's foot never landed past a cut end. The new test `TheMedianMeetsTheBorderWhereTheTwinsCrossItAtAnAngle` crosses at 45 degrees. Without the rule the median stops 4.4 m short of each border (553.3 m against 565.7 m).

The 5 still missed, each for a stated reason:
- **R2, two-way carriageways allowed: equivalent.** A two-way carriageway under the median must keep 3 m on both sides (`MedianFits`), so the median always splits it, and the split rule refuses it.
- **R11, the carriageway test removed: redundant by construction.** `MedianFits` refuses a section with no road under it (its own "no road").
- **R13, cross-sections past the border judged: a guard behind R14.** Once a road cut on the border is taken on straight, a cross-section just past the border sees the same roads. It changes nothing on Line 6 (1,835 m) or in any test. It stays, so that near the border a section judges only what the chunk holds.
- **S6 and S7 (checker: median parts not judged; run piers trusted to the builder's item): guards with nothing to find on the final code.** They do fire. Builder mutant R10 gives a `run-crossing` finding on Line 6 (from the part checks S6 removes), and R7 gives a `median-missing` finding (from the footprint test S7 bypasses).

### Worker time (K-38)

The median costs a little roads worker time on chunks with a viaduct, measured in every round. Mono harness, best of 5 passes a run, 4 rounds (3 for the flyover chunks), main and P-5a run for run, on battery with the lid open. Script [INFRA/time-p5a.sh.txt](INFRA/time-p5a.sh.txt), record [INFRA/worker-p5a.txt](INFRA/worker-p5a.txt).

| Chunks | main 88b7a22 | P-5a | Change, round by round |
|---|---|---|---|
| Slice (12) | 88.75 to 89.57 ms a chunk | 90.33 to 91.01 ms | +1.4 to +1.7 ms; mean **+1.55 ms (+1.7 %)** |
| Line 6 (64) | 87.22 to 87.74 ms | 89.81 to 90.04 ms | +2.2 to +2.8 ms; mean **+2.42 ms (+2.8 %)** |
| Flyover chunks (461) | 59.67 to 59.75 ms | 59.85 to 59.87 ms | +0.11 to +0.18 ms; mean +0.15 ms (+0.2 %) |

Every round's difference is larger than either build's own spread (at most 0.82 ms), so this is a cost, not noise. It comes from the test at every deck point inside the chunk: a section judged at three cross-sections, and the median drawn where it passes. Chunks without an elevated track barely pay it. It is not optimized in this milestone and is recorded under K-38, next to P-2 (+23 % on the same 64 chunks) and D-11a (+10 to 12 % on the slice).

### Goldens

All 49 views were rendered with the P-5a player, and the 46 that existed before also with main's player (88b7a22). The two renders were compared pixel for pixel, so a change made by P-5a is told apart from older drift between the goldens and both players. Renders are deterministic: three views rendered twice with each player gave identical figures. Each changed view was looked at, main above P-5a ([P5A/golden-review/](P5A/golden-review/), with `accepted.txt`).
- **10 views fail their old golden because of P-5a; accepted.**
  - p6-divider: the divider along the deck line now runs into the median, which carries on under the deck with the piers on it.
  - signal-junction: K37-59's median, stopping short of the zebra.
  - d05-bus-stop: the strip between the twins carries the median.
  - kawran-metro: the median starts at a pier base.
  - kawran-street, tod-night-street, m2-divider, m2-viaduct, k37-karwan-station, k37-side-street: the divider's yellow and black stripes shift phase. Seven pieces fewer under the median flip the alternation further along, as with P-6; no piece moved.
- **2 views change but stay within their golden's limit; goldens kept:** kawran-bazar (aerial, 0.117 %) and d06a-street (0.100 % against main).
- **The other 34 views render the same with both players** (at most 0.019 % between them).
- **3 new views:**
  - `p5a-median`: K37-59 on the median.
  - `p5a-seam-end`: the capped end at the c_8_-29 / c_8_-28 seam.
  - `p5a-sidewalk`: the median meeting a paved strip between the twins at Farmgate (below). It was aimed first at c_4_-39, which is outside the area the development player streams, so it was moved into the slice.

### Player bench and builds

Main's player (88b7a22) and the P-5a player were run run for run on the MacBook Air (M1): one discarded air run of each, then 3 air and 3 street runs each with P-5a first in each pair, then 3 more air runs each with main first to check for a run-order effect (as P-6 did). The machine was on battery (56 % and 51 % at the starts), the lid was open throughout, and nothing else was running. Record: [P5A/bench.txt](P5A/bench.txt), tables made by `benchtab.py.txt` and `steady.py.txt`.

| Measure | P-5a | main | Reading |
|---|---|---|---|
| GPU mean, air (6 runs each) | 4.77 to 5.15 ms | 4.84 to 5.11 ms | no change measured |
| GPU mean, street, from frame 300 (3 each) | 8.43 to 8.83 ms | 8.46 to 8.58 ms | no change measured |
| Main thread p50, air, P-5a first | 2.13, 2.52, 2.50 ms | 1.83, 1.99, 1.90 ms | +0.30 to +0.60 ms |
| Main thread p50, air, main first | 2.30, 2.48, 2.27 ms | 2.14, 2.37, 2.20 ms | +0.07 to +0.16 ms |
| Main thread p50, street, P-5a first | 2.38, 2.72, 2.38 ms | 2.20, 2.64, 2.17 ms | +0.08 to +0.21 ms |
| Triangles, batches, system memory, air garbage | | | the same within the spread |

- **Main thread: P-5a was higher in all 9 pairs of these two sets**: by 0.30–0.60 ms with P-5a run first, 0.07–0.16 ms with main first. The gap held over the whole path, after streaming ends too; P-5a adds no objects or renderers, and batches are the same. Recorded as K-56, followed up the same day: **not reproduced** ([K56/result.txt](K56/result.txt)).
  - Profiler captures in the order main, P-5a, P-5a, main: busy main-thread time 2.54 ms a frame for P-5a, 2.56 ms for main. No marker differs beyond the spread between captures.
  - A balanced set without the profiler (main, P-5a, P-5a, main, P-5a, main, main, P-5a): means 2.09 ms for P-5a, 2.13 ms for main. P-5a was higher in 2 of the 4 pairs, and runs of one player differed by up to 0.6 ms.
  - **No main-thread cost is measured; K-56 is closed.** The first write-up read "a residual of about 0.1 ms" from set 2 alone; that does not hold. Why sets 1 and 2 both fell P-5a's way is not known.
- **Street start-up.** In all 3 of P-5a's street runs and 1 of main's, the start area was still loading when recording began after the bench's fixed 2 s warm-up: 10–14 chunk applies in the first 43–141 frames. That loading brought those runs' garbage (30–59 MB against 57 KiB) and most of their slow frames (14 and 26). Three runs against one is too few to tell whether P-5a's longer worker time (+1.7 % a chunk) causes it. Not measured.
- **Frames over 20 ms, air:** one outlier for each player (P-5a 24, main 14); the rest are 1 to 3.

**Windows build:** succeeded (`ProjectTools.BuildWindows`, Mono): 138,487,151 bytes, 0 errors, 0 C# warnings, 20 URP shader-stripping warnings, 23.3 s. Built on macOS, not run on Windows (K-02). Log `Evidence/local/build-windows-p5a-20261105-214046.log`.

### Not verified

- The Windows build is not run on Windows hardware (K-02).
- The median over the twins' inner sidewalks (K-55) is looked at in one view only (`p5a-sidewalk`, Farmgate). The largest stretch, c_4_-39, lies outside the area the development player streams and was not seen. The checker has no median-against-sidewalk check.
- Real median positions and widths are not known: the median follows the drawn, mostly estimated carriageway widths (K-31). The evidence pass (section 15) shows real piers standing in a raised median; P-5a draws one only where the data supports two lanes each side.
- Across a chunk border the median cannot see the neighbour's roads, crossings or buildings: at a seam it ends at the border, capped. One such end is on Line 6 (c_8_-29 at N 14,000, a zebra just past it in c_8_-28; the zebra's keep-out was not measured across the border).
- A pier on a chunk border keeps its reason ("border") even where the median runs under it on both sides: an island there would cross the border, and the pier test, like the checker, judges its whole footprint in one chunk.
- Whether P-5a's worker time makes the street path's start-up loading run past the bench's 2 s warm-up more often is not measured (3 runs against 1).

## Open

- **K-55: closed.** The owner decided on 2026-11-05 to keep the median over the twins' inner sidewalks ("Keep the median"); nothing changes.
- **K-56: closed** (not reproduced in profiler captures or in a balanced bench; [K56/result.txt](K56/result.txt)).
- **K-38:** P-5a's worker time is not optimized.
- **P-4** (portal-style station supports, [K37_PIER_INVESTIGATION.md](K37_PIER_INVESTIGATION.md) section 15) still waits for the owner.
