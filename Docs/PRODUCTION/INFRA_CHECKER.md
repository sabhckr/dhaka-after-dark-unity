# Infrastructure placement checker

Step 5 of [AXIOM_V3_AUDIT.md](AXIOM_V3_AUDIT.md) section J ("New: proposed under K-37 / D-11"), built to section G on the owner's "Build the checker" (2026-11-04). It is one read-only survey of everything that holds a structure up in the slice: metro viaduct piers, footbridge columns, stairs, and the elevated road decks that have no supports yet. Each support gets a stable id and a register row, and an EditMode test fails on any violation that is not in the known list.

**Acceptance is spatial only.** The checks measure positions, heights and clearances on the drawn world. Navigation, collision and driving checks need a drivable vehicle, colliders and a lane-graph user (Phases E and F, section G.4). Lane checks need lane polygons, which do not exist (K-31, section G.5): a "lane" check here is a carriageway check against the drawn width.

**Nothing was moved or changed.** No pier, column, stair, deck, road, building, rail line or chunk file was touched; the metro alignment and the canonical data are as they were. `Runtime/` is unchanged (`git diff` empty), so the player is unchanged. P-2 is not applied. Conflicting structures are listed, not hidden.

## What it reads and writes

| Part | File |
|---|---|
| Survey (Editor assembly, `System` and `Dad.World` only) | `Assets/_Project/Editor/Infra/InfraSurvey.cs` |
| Known list reader and comparison | `Assets/_Project/Editor/Infra/InfraKnown.cs` |
| Known list: 125 rows, each with a reference and a note | `Tests/Infra/known-violations.csv` |
| Test (7 cases) | `Assets/_Project/Tests/EditMode/InfraSurveyTests.cs` |
| Register outside Unity, in the Mono harness (about 2 s) | `Tools/infra/run.sh [out dir] [chunk ids]` with `Tools/infra/Infra.cs` |
| Register of 2026-11-04 | [INFRA/supports.csv](INFRA/supports.csv) (115 rows), [INFRA/decks.csv](INFRA/decks.csv) (38), [INFRA/findings.csv](INFRA/findings.csv) (134), [INFRA/summary.txt](INFRA/summary.txt) |

It surveys what the game builds: for each slice chunk, `ChunkRoads.Build(c, keepMarks: true)` gives:
- the street items: piers, columns, stairs, and why the builder left one out;
- the drawn road quads, junction pads, zebra and stop-line keep-outs, and sidewalk and bus-stop pieces.

`ChunkBuildings.Build(c)` gives the drawn buildings with their base and top. The ring of chunks around the slice is built too, but only as context: spans and duplicates are measured across the slice edge, and nothing outside the slice is reported.

**Ids.** Each id is made from what a support is and where it stands, to 0.1 m:
- `VP-<east>-<north>` for a viaduct pier;
- `FC-<east>-<north>-<way>` for a footbridge column (several ways can meet at one node);
- `ST-<way>-foot` and `ST-<way>-top` for a stair's foot and top;
- `FD-<way>` for a flyover deck, `FB-<way>` for a footbridge deck and `VT-<rail>` for a viaduct girder.

A finding's key is `id|check|other party` (the road, building or support it conflicts with).

**Register fields** (section G.1):
- position: east and north, the chunk and chunk-local position;
- structure: the structure and segment way, whether it is built, and the builder's note;
- heights: foot, top and the deck underside expected above;
- carriageway: the nearest carriageway, its class, whether its width is mapped or estimated, and the clearance;
- pedestrian ground: junction pad, crossing keep-out, bus stop, sidewalk, and the distance to a crossing;
- building: the nearest building, its base and the clearance;
- elevated way: any elevated way the support rises through;
- spacing: the span back and the span ahead, the next support, and the nearest support of the same kind;
- assessment: status, findings, source and confidence.

Every pier and column is "position by rule; real positions not in the data". Stairs are "line mapped; landing by rule".

## Checks

| Check | Fails when | Limit (from the builder's own rules, not engineering data) |
|---|---|---|
| carriageway | the support's disc overlaps a drawn at-grade carriageway; one row per road, footways and steps excluded | pier radius 0.9 m, column 0.22 m |
| junction | it overlaps a junction pad | |
| crossing | it stands in a zebra or stop-line keep-out | |
| bus-stop | it stands on a bus stop pad | |
| building | it overlaps a drawn building standing on the ground | base under 0.5 m |
| raised | it rises through a raised building (a station on stilts) below its top | |
| deck-top | its top is not the deck underside the builder sets, or no deck is found above | 0.05 m |
| through-deck | it rises through a drawn elevated carriageway below its top | |
| duplicate | another support of its kind is closer than the limit, across chunk borders and, for columns, across ways | piers 8 m (TwinMax: one pier per grid line serves tracks that close); columns 2 m; flyover supports: their pile caps (0.55 m past the column) must not meet |
| span-short, span-long | a viaduct pier's gap to the next pier ahead on its deck (sharing a track, within 45° of the deck's direction) | at least 25 m along the deck (P-2); at most 61 m on the deck's main axis (30 m grid; a junction spanned in two steps). Since 2026-11-04 (P-2): 20 m and 61 m along the deck before |
| span-long (footbridge) | a gap along the deck with nothing under it: columns of any way, attached stair tops and joined decks hold it | 25 m (columns every 12 m, moved up to 3 m) |
| unsupported-end | a footbridge deck end with no column, stair or other deck within reach | 6.5 m |
| stair-unattached | a steps way that reaches no deck | the builder's 2.5 m |
| no-supports | an elevated road deck with no supports | any piece in the surveyed chunks (all 27 in the slice before D-11a; none since) |
| span-short, span-long (flyover) | the gap between consecutive flyover supports along a deck (a portal frame counts at its midpoint) | at least 8 m along the deck; at most 61 m on the main axis (D-11a) |
| unsupported-end (flyover) | a free flyover deck end (not cut on the border, not meeting another deck) with no support within reach along it | 20 m (D-11a) |
| median-narrow, median-missing | a pier on a median island leaves less than a lane beside it (on a two-way road, on each side), or stands with no island built | lane 3 m (D-11a) |
| rail (flyover) | a flyover support's edge is closer to an at-grade track's centre line than the limit | 2.5 m (D-11a) |
| deck-in-building | a deck centre line, sampled every 2 m, inside a drawn building's volume (flyover 1 m slab, footbridge 0.4 m, viaduct girder to parapet) | |

Notes, never failures: a pier left out over a junction (the span check judges the gap) and a footbridge column left out over a carriageway.

## Result (slice, 2026-11-04)

115 supports. Of the 66 built piers, 18 are clear; 8 piers and columns are left out by the builder; 38 decks. 125 violations, all in the known list:

| Check | Rows | Reference |
|---|---|---|
| carriageway | 48 (44 piers) | K-37, each row with its K37 id and group; K37-29's living street is V-4 |
| raised | 12 | K-37 group C (V-8): stations standing on their own piers, expected |
| building | 2 | K-37 V-7 (K37-27, K37-32) |
| crossing | 1 | K-37 V-4 (K37-46, the Sonargaon roundabout ring) |
| span-short | 1 | K-37 V-6 (K37-12 to K37-13, 17.7 m) |
| duplicate | 1 + 6 + 6 | V-5 (K37-61, 3.58 m); K-50 (a), 6 piers; K-50 (d), 6 columns |
| span-long | 1 + 1 | K-50 (b), 94.2 m; K-39, 30.9 m footbridge gap |
| through-deck | 2 | K-50 (c) |
| bus-stop | 1 | K-50 (e) |
| unsupported-end | 2 | K-39 |
| stair-unattached | 6 (3 stairs) | K-39 |
| no-supports | 27 | AXIOM v3 E.1 |
| deck-in-building | 8 | AXIOM v3 E.4 |

None of these was found by the deck-top check, so every top meets its deck. No viaduct girder runs through a building.

### Checked against the earlier surveys

- **Builder:** of the 44 piers the builder marks "on a carriageway", the survey finds all 44 on one (a test asserts this).
- **K-37 register** (after P-1): all 67 piers (66 built, K37-60 spanned) match by position within 0.01 m.
  - V-4: K37-29 is 0.02 m into living street 297117157, and K37-46 is in the roundabout's crossing keep-out.
  - V-5: the two piers 3.58 m apart at E 3,390.
  - V-6: the 17.7 m span at the bend.
  - V-7: the survey measures K37-27 and K37-32 as 1.55 m and 1.91 m into their footprints. That is K-37's 0.65 m and 1.01 m from the centre, plus the 0.9 m pier radius.
  - V-8: all 12 station piers.
- **AXIOM E:** 27 elevated road ways with no supports, and the same 8 deck–building pairs at the same lengths (20, 16, 14, 10, 6, 4, 2 and 2 m).
- **K-39:** the same 3 unattached stairs of 7. The column counts differ from K-39's 16 placed and 18 left out: the builder now places 26 and leaves 8 out.
  - The change came with K-37 P-1 (a066c87, narrower estimated widths).
  - Measured by compiling `Runtime/World` at 441a101 (M2), at a066c87^ and at a066c87, and building the slice from the same chunk files: 16 / 18, 16 / 18, then 26 / 8.
  - K-39 is updated.

### Found by the checker (K-50, new)

- **(a) Twin track split by a chunk border.** From E 3,651 to 3,840, track 612562211 runs just north of the c_7_-30 / c_7_-31 border and 671984898 just south of it, 5–6 m apart. Each chunk sees one track and builds a single-track deck with its own piers. So K37-62 to K37-67 each have a second pier 5.2–7.0 m away, with two decks side by side. It is V-5's mechanism over 6 grid lines; K-37 recorded the 6–7 m "nearest other pier" distances but not the cause.
- **(b) A grid line on a chunk border with no pier.** N 13,500 is the c_8_-28 / c_8_-27 border. c_8_-28's deck starts exactly on it and the grid rule takes lines in (low, high]; c_8_-27 does not build it either (not traced further). With K37-60 spanned over its junction, the gap from K37-59 to the next pier is 94.2 m.
- **(c) Piers through flyover decks.** K37-13 and K37-54 rise through decks drawn at their 6 m layer height (motorway link 1558255166, works 1568247194). The real levels are not in the data (K-05, K-13).
- **(d) Coincident footbridge columns.** At two nodes in c_9_-30 where three footbridge ways meet, each way puts a column on the shared node: 6 columns in 2 places.
- **(e) A column on a bus stop pad** (way 343249097): the column rule avoids carriageways, not stop pads.

All five need the owner: (a) and (b) belong with P-2, (c) with the elevation decision, and (d) and (e) with the footbridge kit (D-11). Nothing was moved.

### Errors in the checker, found and fixed before it was accepted

The first run was checked line by line against the data and the K-37 register. Five things were wrong in the checker, not the world:
1. Steps ways counted as carriageways: 5 columns were flagged against their own stairs. The builder's own test skips foot ways, and now the survey does too.
2. A span was measured to another deck's pier. A 0.2 m "span" at E 3,810 led to K-50 (a). Spans now require a shared track.
3. Column ids were not unique where ways meet; the way is now part of the id, which exposed K-50 (d).
4. Footbridge spans ignored stair tops and joined decks. A 2.1 m stub deck was reported as two unsupported ends.
5. Only the deepest carriageway under a support was reported, which hid K37-29's side street (V-4). Now there is one row per road.

The order of supports at the same distance along a deck is now fixed (by id), so keys are stable.

## Tests and evidence

| Item | Result |
|---|---|
| `InfraSurveyTests` (7) | the gate (no unlisted violation, none gone); builder agreement; a unique, complete register row for every support; determinism over two runs; the comparison catches a removed and an added row; known-list rows need a reference; the geometry helpers |
| EditMode | 128 of 128 pass (121 before, 7 new), 2026-11-04 12:53 (`tests-EditMode-20261104-125312`). The survey takes 2.4–2.9 s in the editor |
| PlayMode | 30 of 30 pass, 2026-11-04 12:49 (`tests-PlayMode-20261104-124952`), before the last Editor-only change (one row per road). PlayMode does not load the Editor assembly |
| Mutations | 13 of 15 caught on the Mono harness, both in Unity's runner ([INFRA/mutations.txt](INFRA/mutations.txt)); see below |
| Player build and bench | **not run**: no runtime code or data changed, and the Editor assembly is not in players. No performance figure is claimed |

**Mutations.** Each breaks one survey check, or one builder placement rule as a stand-in for a regression in the world:
- **Caught (13):**
  - in the survey: footways counted as carriageways; the duplicate limit at 5 m; spans ignoring the deck with that limit; the raised check removed; decks never joined; a broken deck-top tolerance; the span-short limit at 15 m; only the deepest carriageway reported;
  - in the builder: columns ignoring carriageways; piers not skipping junctions; a 25 m pier grid; columns every 30 m; stairs never attaching.
- **Not caught (2),** because on today's data the mutant gives the same result. Both are listed as missed in the file, with the mutations that replaced them:
  - Spans ignoring the deck: the 8 m duplicate limit already excludes every other-deck pier in the slice.
  - The stair reach cut to 1 m: every attached stair meets its deck at 0 m.
- **In Unity's EditMode runner:** "columns ignore carriageways" and "raised check removed" each fail the gate test, and only that test.
- The runners are [INFRA/mut.py.txt](INFRA/mut.py.txt), [mut2.py.txt](INFRA/mut2.py.txt), [mut3.py.txt](INFRA/mut3.py.txt) and [mut_unity.py.txt](INFRA/mut_unity.py.txt). The known list was generated by [known.py.txt](INFRA/known.py.txt) from the findings and the K-37 register, then read through.

## Changes with P-2 (2026-11-04)

The register in [INFRA/](INFRA/) is now the survey after P-2. The one before is in [INFRA/before-p2/](INFRA/before-p2/), and the Line 6 registers before and after are in [INFRA/line6/](INFRA/line6/).
- **The survey reads the neighbours' elevated tracks**, as the builder now does, so a pier's tracks are the same in both.
- **Span limits are written out:**
  - 25 m along the deck, and 61 m on the main axis.
  - They are no longer taken from the builder's constants. Taken from the builder, a builder mutation to a 10 m minimum moved the survey's limit with it and went unseen.
  - On a skewed deck, one grid place spanned is 62.8–63.8 m along the deck. The 61 m limit along the deck flagged those, while the same span on a straight deck passed, so the limit is on the axis.
- **Only a pier ahead or behind counts as a span** (within 45° of the deck), not one beside it. At the north terminus the tracks fan out, and side-by-side decks that share a track stood in a row 12 m apart, read as 12 m spans.
- **The slice gate:** 105 violations (125 before), all listed. Known list regenerated, rows mapped to the K-37 register by the nearest pier within 8 m.
- **Mutations, re-run** ([INFRA/mutations-p2.txt](INFRA/mutations-p2.txt)):
  - 19 of 23 caught: all 8 new mutants (6 builder rules of P-2, 2 checker rules), and 11 of the 15 earlier ones.
  - 4 missed, because on today's data the mutant gives the same result:
    - the 5 m duplicate limit, and spans ignoring the deck with and without it: no viaduct duplicate is left in the slice or on Line 6. P-2's own mutants (no neighbour tracks, both chunks owning the border, no one-pier-per-place check) bring duplicates back, and the duplicate check catches each;
    - the stair reach at 1 m, as before.

## Changes with D-11a (2026-11-04)

The register in [INFRA/](INFRA/) is now the survey after D-11a. The one before is in [INFRA/before-d11a/](INFRA/before-d11a/), and the survey of the 461 chunks with a flyover deck is in [INFRA/d11a-city/](INFRA/d11a-city/) (flyover rows only).
- **Flyover supports are surveyed** like any other: every check of the table above applies, plus the flyover rows (spans, free ends, median islands, rail clearance). The builder's reason for each place left out is kept as a note.
- **Pile caps in the duplicate check.** A portal leg 1.91 m from another deck's pier passed the old 2 m column limit, but the two pile caps overlapped. Flyover supports are now judged by their caps: the gap between columns must be at least 2 × 0.55 m.
- **A portal frame is one support for spans:** its two legs are paired, and the span is measured at the beam's midpoint on the deck.
- **A median island on a two-way road must leave a lane each way.** On a one-way road (the graph's flag) one side may be covered. Another at-grade way continuing within 30° of the road counts as the same road, so an island is not flagged at a road's join.
- **The slice gate:** 80 violations (105 before), all listed. No-supports 27 → 0. Two new rows are listed:
  - FD-1558255166 span-long (100.0 m): two trunk roads either side of the c_7_-30/c_8_-30 border and the 4 m border keep-out; an engineered span;
  - FD-1249807413 unsupported-end: a `construction` expressway end (K.5).
- **The city (461 chunks):** every built flyover support is clear: 6,539 piers and 422 portal legs. What is left is K-53.
- **Mutations:** see [INFRA/mutations-d11a.txt](INFRA/mutations-d11a.txt) and the D-11a doc.

## Changes with P-6 (2026-11-04)

The register in [INFRA/](INFRA/) is now the survey after P-6. The one before is in [INFRA/before-p6/](INFRA/before-p6/); the Line 6 registers before and after are in [INFRA/p6-line6/](INFRA/p6-line6/), and the survey of the 461 flyover chunks after P-6 in [INFRA/p6-city/](INFRA/p6-city/).
- **Islands under viaduct piers are judged** like flyover islands, sized by structure (viaduct 1.5 m half width, 3.9 m past the pier; flyover 1.4 m, 3.8 m). A support is on an island when the builder's reason has `median` in it.
- **The island's footprint is checked** every 0.5 m against junction pads, crossing keep-outs, bus stop walks and buildings on the ground: `island-junction`, `island-crossing`, `island-bus-stop`, `island-building`.
- **No two islands overlap:** `island-overlap`.
- **Traffic on both sides:** `median-one-side` when the island leaves less than 3 m of lane on either side, counting a carriageway along the deck whose edge is within 1 m of the island. A violation for a viaduct island (the owner's P-6 wording), a note for a flyover island (D-11a's rule, kept by the owner on 2026-11-05; K-54).
- **The builder's reason for a pier left on a carriageway** now reads `on a carriageway; no island: <reason>`; the agreement count matches on the prefix.
- **The slice gate:** 68 violations (80 before), all listed; the 12 carriageway rows of the piers now on islands are gone from the known list.
- **Found by the new checks:** two D-11a flyover islands drawn into ground buildings (FP-7710.0-9823.1, FP-6988.9-10265.8), fixed in the builder (see the P-6 doc).
- **Mutations:** see [INFRA/mutations-p6.txt](INFRA/mutations-p6.txt) and the P-6 doc.

## Changes with P-3 (2026-11-05)

- **The summary** has a line "Viaduct piers in buildings": the builder's two counters (in a building on the ground; under a raised building) against the survey's own figures (reaching into a ground building; rising through a raised building; standing clear under one). Slice: builder 2 and 11, survey 2, 11 and 0. Line 6: builder 4 and 97, survey 4, 47 and 58.
- **The builder's reason** for a pier in a raised building reads `under a raised building`; the register in [INFRA/](INFRA/) is the survey after P-3 (2 builder notes changed). Findings and the known list are unchanged: 68, 0 new, 0 gone.
- **Mutations:** [INFRA/mutations-p3.txt](INFRA/mutations-p3.txt), 8 of 8.

## Changes with P-5a (2026-11-05)

The register in [INFRA/](INFRA/) is now the slice survey after P-5a; the one before is in git (main 88b7a22). The Line 6 registers before and after are in [INFRA/p5a-line6/](INFRA/p5a-line6/).
- **A pier on the continuous median** (the builder's reason `median run`) is judged as an island pier, with two lanes each side: `median-one-side` at 6 m, counting a carriageway along the deck whose edge is within 6.5 m of the median's (8 m from the deck line).
- **Twins only:** `median-two-way` when a carriageway under the median is two-way, `median-split` when a one-way carriageway under it keeps more than 0.3 m on both sides of it.
- **On the median as drawn:** the pier's centre and both ends of its island footprint must lie on a drawn part of the median (the builder's `MedianRunParts`), else `median-missing`. The builder's own median item is not trusted for this.
- **The median itself:** every straight part, with ids `MR-<east>-<north>` of its start, is sampled every 0.5 m across its 3.0 m width against junction pads, crossing keep-outs, bus stop walks and buildings on the ground: `run-junction`, `run-crossing`, `run-bus-stop`, `run-building`.
- **A carriageway beside an island** now counts at a bend of its drawn line too, where the way goes on past a segment's end (as in the builder). It changed no P-6 result on Line 6.
- **The summary** has a line "Continuous median (P-5a)": pieces, length and piers on it, as the builder counts them. Slice: 5 pieces, 265 m, 4 piers; Line 6: 37 pieces, 1,835 m, 23 piers.
- **The slice gate:** 67 violations (68 before), all listed; K37-59's carriageway row is gone from the known list.
- **Line 6:** 312 carriageway findings on 311 piers (332 on 331 before); no `median-*` or `run-*` violation; agreement 311 of 311.
- **Mutations:** [INFRA/mutations-p5a.txt](INFRA/mutations-p5a.txt), 26 of 31 (builder and checker together). The two checker guards missed, S6 and S7, have nothing to find on the final code, and both fire on a broken builder; see the P-5a doc.

## Limits

- **Spatial only:** see the top of this page.
- **Widths:** carriageway widths are mostly estimated (K-31), and the register says which. A carriageway finding is against the drawn width.
- **Deck clashes are a lower bound:** decks are sampled on the centre line, so deck width is not counted (as in AXIOM E).
- **Span limits are assumptions,** taken from the builder's grid and column rules, not from engineering data.
- **Flyover supports** (D-11a) are surveyed as `FP-` (pier) and `FL-` (portal leg) items. The checker cannot tell a lane from a median: it judges an island by the drawn carriageway width and the graph's one-way flag only (AXIOM v3 G.5).
- **The slice, and Line 6 for piers:** the gate runs on the slice (with its ring as context). Since P-2 a second test surveys the 64 chunks with an elevated track and fails on any viaduct duplicate or span finding; other findings there have no known list. `Tools/infra/run.sh` takes other chunk ids.

## Owner decisions this does not make

- ~~P-2 (pier rule fixes, along the deck only), now also for K-50 (a) and (b).~~ Approved and applied 2026-11-04: [P2_PIER_RULES.md](P2_PIER_RULES.md).
- The evidence source for K-37 groups C, D and E.
- ~~How the 10 `construction` flyover ways are shown, and whether flyover supports join D-11 (section K.5).~~ Decided 2026-11-04: finished flyovers, their clashes listed; supports built in D-11a ([D11A_FLYOVER_SUPPORTS.md](D11A_FLYOVER_SUPPORTS.md)).
- The flyover residuals of K-53 (exporter flag for stubs past a border; engineered long spans) and the 6 m headroom of K-52.
- The footbridge kit fixes for K-39 and K-50 (d) and (e).
- Milestone numbering (K.7), the playable boundary (K.8), K-46, and that instancing is not pursued (K.3).

D-07 and D-08 still wait for section J steps 6 to 8.
