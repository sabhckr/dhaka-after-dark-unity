# Milestone 2: Farmgate validation

The detailing directive's Milestone 2 ([DETAILING_DIRECTIVE.md](../ART_DIRECTION/DETAILING_DIRECTIVE.md), Part 29), chosen by the owner on 2026-11-03 to go before D-06. One representative of each listed asset type, built to the detailing standard ([DETAILING_STANDARDS.md](../ART_DIRECTION/DETAILING_STANDARDS.md)) and validated together in Unity at Farmgate.

## Scope

| Directive item | What is built | Placed from | Route |
|---|---|---|---|
| Road surface and junction | Already built (C-03, D-04): surfaces, pads, markings, covers. Validated, not rebuilt | Road data | A |
| Footpath | Already built (D-05). Validated, not rebuilt | Road data, width rule | A |
| Roadside drainage | Already built (D-05). Validated, not rebuilt | Road data, class rule | A |
| Road divider | Raised concrete median with painted kerb faces and a steel railing, on the line between twin one-way carriageways where their drawn surfaces meet | Twin carriageways in the road data | A |
| Utility pole and cables | Tapered concrete pole, two crossarms, insulators, conductors and a sagging telecom bundle between poles | Placement rule along streets (the slice has no mapped power lines) | A + kit |
| Streetlight | Steel pole with arm and LED head at every mapped lamp; an arm and head on one utility pole in two | Mapped lamps; rule on poles | Kit |
| One building facade | Window bays with reveals, frames, sills and glass, floor bands and parapet, on the buildings in the validation area | Footprints and heights unchanged | A |
| One shop | Ground-floor shopfronts with rolling shutters, signboards and awnings on the road-facing walls of those buildings | Road-facing walls | A |
| Metro-related structure | Line 6 viaduct: deck with parapets and track plinths, single-column piers with pier caps, along the mapped alignment at its data height | Rail data (the two elevated tracks) | A |
| Footbridge | Deck, railings, columns and stair flights for every elevated foot way, including "Foot over Bridge, Farmgate" | Road data (foot ways on bridges) | A |
| Street vegetation | Trees at every mapped tree point, in three generic broadleaf forms | Tree data (no species) | Kit |

**Validation area** for facades and shops: east 3,940–4,140 m, north 14,600–14,850 m (77 buildings around the Farmgate footbridge). Everything else applies to the whole slice. D-06 takes facades city-wide once they have levels of detail.

## Rules that keep existing systems intact

- No road, building footprint, height, rail coordinate or chunk boundary changes. New geometry follows the data as drawn.
- Poles, lamps and trees are placed on the streaming workers like everything else and drawn in 125 m cells per chunk, each with a near and a far level of detail (Near to 150 m, Far to 450 m, none beyond).
- The viaduct, footbridges and dividers are one structures mesh per chunk.
- Where data and a structure disagree (a pier landing on a drawn carriageway with an estimated width, a tree on a road), the case is counted and listed; nothing is moved to hide it. Two labelled rules move or drop things: a mapped lamp inside an estimated carriageway moves straight out to 0.45 m behind its kerb when that spot is in the chunk, off every carriageway and outside buildings (else it is listed), and the distance is recorded, and no pier stands in a junction pad (the deck spans it).
- Function is not claimed: lamp heads do not light yet (D-13), shutters do not open.

## Acceptance (directive Part 30)

Closed 2026-11-03. Every figure below was measured on this code; the shots are Unity development-player renders (Apple M1, Metal), not editor or Blender images.

### What was built, and the evidence

| Directive item | Built | Slice counts (12 chunks) | Test | Golden |
|---|---|---|---|---|
| Road surface and junction, footpath, drainage | Unchanged (C-03, D-04, D-05), re-validated | as D-05 | existing road, marking and sidewalk tests pass | all 17 earlier views re-shot and re-accepted |
| Road divider | `Structures.cs` | 608 pieces, 2.4 km | `DividersStandOnTheRoadBetweenTwins` | `m2-divider` |
| Utility pole and cables | `StreetKit.UtilityPole`, `Anchor`, `Catenary`; placement in `StreetFurniture.cs` | 1,060 poles (226 spots without room, 11 too close), 115 transformers; 509 spans and 117 half spans to a border, 267 not hung across a building | `PolesLampsAndTreesStandClear`, `CablesHangFromThePolesAndSag` | `m2-street-poles` |
| Streetlight | `StreetKit.StreetLight`; lamp arms on poles | 58 of 64 mapped lamps (18 moved to the kerb, 6 listed); arms on 517 poles | `EveryMappedLampAndTreeIsPlacedOrListed` | `m2-street-poles` |
| Building facade | `Facades.cs` (`ChunkBuildings`) | 76 buildings, 3,616 windows | `FacadesKeepFootprintAndTop` | `m2-shopfront` |
| Shop | Same | 108 shopfronts with shutters (closed, half, open), blank boards, awnings on half | same | `m2-shopfront`, `m2-footbridge` |
| Metro structure | Line 6 viaduct: box girder, parapets, plinths, rails, piers with caps | 2,063 m, 66 piers (52 on a drawn carriageway, K-37; 1 junction spanned) | `ViaductCarriesEveryElevatedTrackOnPiers` (deck under all 1,963 track samples) | `m2-viaduct`, `kawran-metro`, `signal-junction` |
| Footbridge | Deck, toe boards, railings, columns, stairs | 11 decks, 7 stairs (3 with no deck in reach, K-39), 16 columns (18 left out over roads) | `FarmgateFootbridgeHasItsDeckAndStairs` | `m2-footbridge` |
| Street vegetation | `StreetKit.Tree`, three generic broadleaf forms | 189 of 199 mapped trees (10 listed) | `EveryMappedLampAndTreeIsPlacedOrListed`, `PolesLampsAndTreesStandClear` | `m2-trees` |

The kit parts are closed and face out (`KitSolidsAreClosedAndFaceOut`, signed volumes and edge pairing); the meshes' ranges cover the arrays exactly (`RangesCoverTheMeshAndIndicesStayInside`); materials are checked per part (`WorldMaterialTests`). `ChunkStreamerTests.FarmgateChunkMatchesItsData` checks that the streamed chunk's structure and cell meshes are the builder's ranges, in `Dad/World`, casting shadows, one tier per cell. Mutations: 9 deliberate defects, each caught (TEST_REPORT, M2).

### Level of detail

| Part | Near (to 150 m) | Far (to 450 m) |
|---|---|---|
| Utility pole with lamp arm and transformer | 350 triangles | 24 |
| Street light | 114 | 36 |
| Tree (three forms, 12 m) | 1,472–1,832 | 68 |
| Slice totals | 844,226 in the near cells | 40,380 in the far cells |

The viaduct, footbridges and dividers are one structures mesh a chunk (131,860 triangles over the slice), always drawn. The detailing standard's targets (pole 4,000, prop 1,500, tree 20,000) are met with room. Tiers switch per 125 m cell with 10 m hysteresis (`StreetLodPicksATierWithHysteresis`). Facades have no far tier yet (K-42).

### Performance (PERFORMANCE_REPORT, M2)

- From the air (`slice-crossing-air`, 3 runs): GPU p50 over the slice 7.53–7.91 ms (D-05 6.75–7.17), p95 10.3–11.4 ms (9.2–10.6). Batches p50 32–35 (26). Busiest apply frame 2.05–2.25 ms (1.17–1.28), under the 4 ms rule; no collection in an apply step.
- At street level (`kawran-farmgate-street`, 2 runs): GPU p50 7.57 and 8.19 ms (D-05 7.87–8.41), p95 11.9 and 12.7 ms; one frame of 7,199 over 16.7 ms (17.46 ms). Triangles p50 1.52–1.56 million (0.42–0.44), peak 2.06 million, inside the 3 million budget. No garbage.
- Worker time: roads 38.3 ms a chunk against 30.5 without the street pass; buildings 2.1 against 0.6 ms (K-38).

### Not claimed

Lamp heads do not light (D-13); shutters do not move; signboards carry no text (K-41); facades exist only in the validation area (K-42); piers in traffic lanes follow the data as drawn (K-37); trees are not species; cables alias from far off (K-40).
