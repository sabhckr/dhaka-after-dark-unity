# D-11a: flyover supports and closed decks (AXIOM v3, section J step 7)

The owner approved this on 2026-11-04: "Start J step 7 now", with the world transformation brief ([WORLD_TRANSFORMATION_PLAN.md](WORLD_TRANSFORMATION_PLAN.md), section 3a). The same day they decided four questions for it:
- K.5: the 10 `construction` expressway ways are built as **finished flyovers**, and their deck-through-building clashes stay listed;
- over roads: **"Median, else portal"**;
- water and rail: **"Water piers + rail-side"**;
- the identity and gate decisions of the plan.

Branch `wt/d11a`, cut from `main` at `3348cc6`.

## What was built

Every elevated vehicle deck now stands on supports and has a closed girder under it: a bridge way at 2.5 m or higher that is not a foot way. Before, there were 27 such ways in the slice, all with **no supports** (AXIOM v3 E.1). RoadBuilder still draws the deck's surface, barriers and soffit, unchanged. The new pieces are in `Runtime/World/Flyovers.cs` and go into the chunk's structure mesh (range 0, which casts shadows).

| Piece | Form |
|---|---|
| Girder | A closed box under the deck: from 5 cm under the surface down the fascias to a soffit 0.55 of the deck's width. It is 1.4 m deep under a 6 m deck and 1.8 m under an 11 or 16 m deck. It is cut at every support into segments with a 2 cm joint, and at any jump in the deck's width. Ends are capped except on a chunk border, where the neighbour's segment continues it |
| Pier | A pile cap 0.55 m wider than the column on each side, from 0.3 m below ground to 0.32 m above it; a round column (0.8 m radius); a drain pipe down its side; and a hammerhead cap under the girder (0.6 m deep at 6 m, 1.1 m above). Where the pier stands off the deck's line, it carries an outrigger beam instead |
| Portal frame | Two legs (0.6 m radius, with pile caps) on clear ground either side, within 20 m of the deck line, and a cross-beam 0.9 m deep under the girder |
| Median island | A raised island along the deck under a pier: a yellow kerb and a concrete top 0.48 m up. It is 2.8 m across and 7.6 m long. It is drawn on the road; the road data is not changed |

## The rules

A support stands where the deck line meets a line of the 30 m world grid on its main axis, at least 25 m along the deck after the last one. There is also one near each free end: an end not cut on the border and not meeting another deck, with no support within 12 m. At each place the builder tries, in order:

1. **Clear ground** (water allowed: "Water piers"): a pier on the deck's centre line.
2. **Slid** along the deck by up to 4 m (6 m for an end support) to clear ground.
3. **The median.** Where only a road running along the deck is under the place:
   - first, clear ground between two carriageways running along the deck (the gap of a dual carriageway), within the cap's reach;
   - else a **raised island** on the road, where it leaves at least 3 m beside it. **A two-way road keeps a 3 m lane each way.** On a one-way road one side may be covered instead (less than 0.3 m left), as long as one 3 m lane is kept. The island is checked at its centre and both ends. It may not stand over a crossing road, or over the end of a road (a T-junction mouth).
4. **The railway** under the place: an offset pier beside the tracks, its edge at least 2.5 m from a track's centre line, with a cap reaching under the deck ("rail-side").
5. A **portal frame**.
6. A building, a lower deck, another support, the chunk border or a bus stop in the way: an **offset pier** up to the girder's half width plus 2 m out.
7. **Off the grid:** slid along the deck by up to 12 m to clear ground, rather than leave a grid place spanned.
8. Else the place is **spanned**, and counted.

Also:
- **Where a support never stands:**
  - on a carriageway, except on its own island;
  - in a junction pad, a crossing's or stop line's keep-out, or on a bus stop pad (the pads the sidewalks drew);
  - in a building;
  - within the rail clearance;
  - within 0.3 m of a lower deck, or under one;
  - on another support;
  - **within 4 m of its chunk's border near a deck cut** (`BorderKeep`): within 15 m along the border of the edge of any elevated deck that the border cuts (`BorderCutReach` plus the deck's half width), or within 30 m in a straight line of its own deck's cut.
- **Why the border rule.** Each chunk places its own supports without seeing its neighbour's. Near a cut deck, two supports either side of the border therefore stay at least 8 m apart, the shortest span the checker allows, and their pile caps never meet. Before this rule, the city survey found 8 overlapping caps and 11 spans of 1.5–7.8 m across borders. Away from any cut, nothing in the neighbour can come near, so the strip is open.
- **Buildings** are tested against the whole pile cap: a ring of 16 points at the column radius plus 0.55 m, and the centre.
- **Two supports' pile caps stay at least 0.2 m apart,** across decks and against the viaduct's piers.
- **No slide brings a support within 25 m of the last one.**
- **A grid line at a deck piece's start or end is a place too.** Each line is visited once. On the south or west chunk border this chunk owns the place, and on the north or east border the neighbour owns it.
- **Gap filling** (`FillGaps`), after the grid and end supports, on each piece. A support is added:
  - where two supports are more than 60 m apart on the main axis;
  - where a border cut is more than 30 m on the axis from the nearest support (so that with the neighbour's piece, which keeps the same rule, a span over the border stays inside 61 m);
  - where a free end is more than 20 m from one.
  The builder tries targets every 20 m across the allowed range, nearest the middle first, each with the same ground rules and slides. A fill support never comes within 25 m of another along the deck. Where nothing fits, nothing is built, and the checker reports the gap.

**Not changed:** no road, rail, building, water or chunk file. Every deck keeps its drawn line and its layer height (6, 11 or 16 m, K-05). Supports are placed from the ground as it is drawn.

## Found and fixed while building it

The builder and the checker each caught faults in the other before the rules above were settled:
- **Grid places on westward decks.** The places were visited in grid order, so a deck drawn west skipped all but the first: spans up to 254 m. They are now visited in the segment's own direction (mutant F1).
- **A grid line on the chunk border was nobody's place.** A deck piece starting on the west border did not count the line at its low end, and the neighbour does not own its east edge. The result was a 105.7 m span on motorway link 1249814963 at E 4,500, the same pattern as K-50 (b) on the viaduct (mutant F2).
- **Two grid places spanned in a row** over crossing roads: 100.0 m on 1558255166 and 65.2 m on 507171180. Hence rule 7 (mutant F7).
- **Islands on two-way roads.** The first rule let an island cover one side of any road, leaving a single 4.2 m lane on a two-way road. That would block one direction. Hence the two-way rule (mutant F3).
- **Overlapping pile caps.** A portal leg stood 1.91 m from another deck's pier, past the old 0.5 m surface margin, but the two pile caps overlapped by 0.6 m. The checker's 2 m duplicate limit missed it too. Both now judge pile caps.
- **A leg 0.11 m into a building**, between the five points the builder sampled. It now tests the cap's ring.
- **The border rule, first too wide.** At first the 4 m keep-out ran along every border. Then a deck that only runs near a border, or ends there, lost its supports, and a stub left past a cut got an end portal 6 m from the neighbour's pier. Across the 461 chunks it swapped the overlaps for 21 new floating ends and long spans (9 went). It now applies only near a cut (mutants F20 to F22). The worst border spans fell from 123.0 m to 85.5 m and from 94.4 m to 64.5 m.
- **The girder's closure test.** The loft splits vertices for flat shading, so an index-based check found 504 "open" edges. Welded by position there are none. The test now welds, and a mutant with no joint still fails it.

## Results

### The slice

The 12 slice chunks (c_6..9, -28..-30), counted by the builder:

| | Count |
|---|---|
| Flyover decks (pieces) / ways | 44 / 27, every way with supports |
| Girder length | 10,047 m |
| Supports | 315: 291 piers, 24 portal frames (48 legs) |
| Piers slid on the grid / off it (up to 12 m) / gap fills | 28 / 7 / 7 |
| On a median island / offset (rail, building, lower deck) / in water | 20 / 29 / 39 |
| Places spanned | 9 |

### The checker

- **Slice:** 80 violations, 105 before; all are on the known list (0 new, 0 gone). No-supports went from 27 to 0. Every flyover support is clear: 291 piers and 48 legs, with no carriageway, junction, crossing, bus stop, building, rail, duplicate or through-deck finding. Two rows are new and listed:
  - FD-1558255166, span-long, 100.0 m: two trunk roads either side of the c_7_-30/c_8_-30 border, with the border keep-out between them. An engineered span.
  - FD-1249807413, unsupported-end: a `construction` expressway end (K.5).

  The viaduct and footbridge rows are unchanged.
- **City:** the 461 chunks with a flyover deck ([INFRA/d11a-city/](INFRA/d11a-city/)). Every built support is clear: 6,539 piers and 422 portal legs. Left: 39 unsupported ends, 16 decks with no supports, 19 long spans, 1 short span and 280 deck-in-building samples. Every one is described in K-53. The first city survey, before the fixes above, had 103 flyover violations outside deck-in-building, and this one has 75. The headroom under 6 m decks is K-52.

### Tests

- **EditMode 144 of 144** on the final code, and **PlayMode 30 of 30** ([../QA/TEST_REPORT.md](../QA/TEST_REPORT.md)).
- `FlyoverTests` is new, with 14 tests:
  - the grid either way a deck is drawn;
  - medians on wide roads, portals on narrow ones;
  - rail-side offsets and water piers;
  - the slide off the grid and gap filling;
  - the west-border grid line;
  - a deck along a border, and the strip near another deck's cut;
  - islands on one-way and two-way roads;
  - pile caps apart;
  - the closed girder with its joints;
  - the slice pinned.

### Mutations

- 28 mutants in the Mono harness: 22 break a builder rule, 6 a checker rule. **27 are caught** ([INFRA/mutations-d11a.txt](INFRA/mutations-d11a.txt)).
- F21 (other decks' cuts ignored) was first missed, as it gives the same result on today's data. It is caught since its own test.
- S6 (the checker's two-way rule read as one-way) is missed: no island in the slice or the city tells the two apart.

### Goldens

- All 44 views were rendered: 35 match, 4 changed, 5 are new.
- Each changed or new view was reviewed by eye with a diff. The changed ones were also compared with the HEAD player: the only differences there are the flyovers' girders and piers.
- Two new views were re-aimed before they were accepted.
- Record: [D11A/golden-review/](D11A/golden-review/).

### Performance

- **Roads worker:** +5.0–6.0 ms a chunk on the slice (+10–12 %), and +2.2–2.4 ms on the 461 flyover chunks (+6–7 %). These are interleaved against main ([../PERFORMANCE/PERFORMANCE_REPORT.md](../PERFORMANCE/PERFORMANCE_REPORT.md)).
- **Player bench** ([D11A/bench.txt](D11A/bench.txt)): interleaved with HEAD's player, 3 air and 3 street runs each, lid open.
  - **GPU, air path:** +0.4 to +0.8 ms on the whole-path mean (4.93–5.68 against 4.54–4.89 ms).
  - **GPU, street path:** no change measured.
  - **Triangles:** street +3 to +4 %.
  - **Memory:** mesh +1 MB.
  - **Garbage, apply frames and kits:** unchanged.
  - The first attempt ran with the lid closed. It is invalid and not used.

### Builds

- macOS development player: 223,468,469 bytes, 0 errors.
- Windows: 138,475,983 bytes, 0 errors, **not run** (K-02).

### Not verified

- Any Windows run.
- Collisions: no world geometry has colliders yet (no collider in the runtime code), the supports included. Collision layers come with Phase E.
- Clearance for vehicles under 6 m decks (K-52).
- Footbridge deck shadows (K-20).
- The city residuals (K-53) were only surveyed: they are outside the slice the player streams, so none was seen in a shot.
