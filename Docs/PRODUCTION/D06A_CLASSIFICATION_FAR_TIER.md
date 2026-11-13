# D-06a: classification and the far tier

The first part of D-06 as re-scoped by the owner on 2026-11-03 ([MASTER_DIRECTIVE_AUDIT.md](MASTER_DIRECTIVE_AUDIT.md) decision 3, [ENVIRONMENT_ROADMAP.md](ENVIRONMENT_ROADMAP.md)): a category for every building, and a facade for every building that is drawn in the shader, so the whole city reads as buildings with windows and shops from a distance. Nothing is added to the mesh. Footprints, heights, roads, rails and chunk boundaries are unchanged. Roofs and materials are D-06b; facades in geometry near the camera are D-06c.

## Classification (`Runtime/World/BuildingClass.cs`)

Each piece of a building gets a category and the source of it, in this order:

| Step | Source label | Rule |
|---|---|---|
| 1 | `OsmType` | The building's own `building=*` value, by a table of 90 types. `yes` is no type; an unlisted type is `Other` |
| 2 | `RuleSplit` | An untyped piece cut by the chunk grid (K-16) sees only part of its footprint, so it is `Residential`. Every piece of the building then agrees, whichever chunk it is in |
| 3 | `Landuse` | The landuse polygon its centre stands in (residential, commercial, retail, industrial, education, religious, military, railway) |
| 4 | `RuleTiny` | Under 15 m²: `Informal` (kiosk, shed, shack) |
| 5 | `RuleNamedLarge` | Named and 3,000 m² or more: `Special`, drawn plain. In the data these are notable buildings (the National Parliament, the Supreme Court, the conference centre); their look is not guessed |
| 6 | `RuleLarge` | Unnamed and 3,000 m² or more: `Commercial` (market, mall or office block) |
| 7 | `RuleMainRoad` | A corner within 11 m of the carriageway of a tertiary or higher road, at grade: `MixedUse` (shops below, homes above) |
| 8 | `RuleDefault` | Otherwise `Residential` (a walk-up) |

A building of several parts in one chunk (a multipolygon) is classified as one: by its total area, as split if any part is on the border, else as its largest part says. Before that rule, three buildings city-wide had parts in different categories, the National Parliament among them; now none do.

**City-wide** (census, all 1,303 chunks, 494,978 buildings): own type 6.7 %, landuse 7.0 %, labelled rules 86.2 % (walk-up default 74.0 %, main road 5.5 %, split 4.4 %, tiny 2.3 %, large 81, named large 48). Categories: residential 90.6 %, mixed use 5.5 %, informal 2.4 %, institutional 0.8 %, commercial 0.4 %, industrial 0.2 %, special 0.2 %, religious 242, other 43. Pieces of one building that disagree: 0. Time: 0.6 µs a piece, 333 ms for the city's 519,573 pieces on one thread.

**Slice** (12 chunks, 5,642 buildings): own type 7.3 %, split 6.4 %, main road 6.0 %, default 79.4 %, tiny 40, large 7, named large 1, landuse 1. Every building classified (test).

## The far tier

`BuildingBuilder` writes four floats a vertex in a new mesh stream (TEXCOORD0) on every wall it draws:

- **u**: bays along the wall, a whole number at its end (bays of about 3 m; 3.6 m commercial and institutional; 6 m industrial);
- **v**: floors up from the base, the height divided evenly into the building's levels;
- **w**: the facade family, 8 if the wall faces a vehicle road (not a service road) within 11 m straight out, a seed from the building's id, and a wall flag;
- **s**: metres along the ring, from a per-building offset.

The building meshes use `Resources/Materials/WorldFacades.mat`: `World.mat` with the `_DAD_FACADE` keyword. The shader paints each wall from u, v and w:

| Family | From | Upper floors | Ground floor on a road-facing wall |
|---|---|---|---|
| Walk-up | Residential, Other | Window with frame and steel grille, about half the bay | Same |
| Shop-house | Mixed use | As walk-up | Rolling shutter (closed, half or open by hash), dark shop inside, blank signboard |
| Commercial | Commercial | Wide glazing with a mullion | Glazed shopfront, blank signboard |
| Industrial | Industrial | High strip windows | Same |
| Institutional | Institutional | Window with a concrete band at each floor | Same |
| None | Religious, Special, Informal | Plain | Plain |

Every family has a slab line at each floor. About one window in five is lighter glass (a lit room or curtains), and about one home window in fifteen is bricked up. Edges are antialiased by the coordinates' screen derivatives; from about six pixels a bay down to two, the pattern fades to its average cover, so distant blocks read as rows of windows without moiré. Walls shorter than 2.5 m and walls cut along the chunk border stay plain, and so do the 76 pieces with M2 geometry facades in the validation area. Signboards carry no lettering (K-41); glass does not light at night (D-13).

**City-wide**: 2,042,092 walls carry a pattern, 42,292 of them with shops. **Slice**: 23,878 walls, 628 with shops. Building every chunk of the city takes 1.7 s on one thread (Mono, outside Unity); the 12 slice chunks 48 ms in the editor.

**K-21 resolved for building walls.** Walls take their texture coordinates from s and the height instead of the box projection, so a wall at 45 degrees is no longer stretched 1.41x and no wall flips between projections. Golden `farmgate-grid` shows square 1 m cells on walls at every angle.

**Debug view.** F5 now also cycles to `families` (walk-up tan, shop-house red, commercial blue, industrial grey-blue, institutional green, plain grey); golden `d06a-families`.

## Evidence

| Check | Result |
|---|---|
| EditMode tests | 104 / 104 (96 before, plus `BuildingClassTests`, 8) |
| PlayMode tests | 25 / 25 |
| New tests (`BuildingClassTests`, 8) | Rules in order; parts agree; whole bays and floors on walls only; shops on the road-facing wall only; wall metres at true length around a 45-degree square (K-21); the slice classified, pieces agreeing across chunks, patterned triangles vertical, M2 pieces carrying no family; shader family numbers match the enum; `WorldFacades.mat` matches `World.mat` with its keyword and compiles |
| Mutations | 7 of 7 caught: border rule removed (`RulesApplyInOrder`, `PartsOfOneBuildingAgree`); parts not merged (`PartsOfOneBuildingAgree`); shop normal reversed (`ShopsOnTheWallFacingTheRoad`); wall metres not advanced (`WallTexturesRunAlongTheWallAtItsLength`); shader family number changed (`ShaderFamiliesMatchTheEnum`); M2 kit vertices left without coordinates (`TheSliceIsClassifiedAndItsFacadesSitOnWalls`); material keyword removed (`FacadeMaterialIsTheWorldMaterialWithItsKeyword`) |
| Corner and height checks | Unchanged and passing (`TwentySliceBuildingsSitOnTheirData`, `FarmgateWallsFaceOutAndRoofsUp`): no vertex position moved |
| Goldens | 3 new (`d06a-far`, `d06a-families`, `d06a-street`); 24 re-accepted after looking at each (every building wall changed); 5 unchanged within tolerance (`k37-kni-kerb`, `m2-footbridge`, `m2-shopfront`, `m2-street-poles`, `tod-night`) |
| Performance | Development player, Apple M1, Metal, 1280x720, the same paths as K-37 P-1. Air (`slice-crossing-air`, 3 runs): GPU p50 3.36 / 3.27 / 3.27 ms (P-1 5.39 / 3.49 / 3.20), p95 9.99 / 9.93 / 10.35 ms; batches p50 28, triangles p50 about 199,000 (both as P-1); apply frames at most 2.39 to 2.75 ms (P-1 2.11 to 2.45); garbage 91.4 to 95.2 MB a run (P-1 73.4 to 76.4 MB), none in an apply step; frames over 20 ms 3 / 1 / 1. Street (`kawran-farmgate-street`, 2 runs): GPU p50 8.20 / 8.33 ms (P-1 7.48 / 7.75), so the facade shader costs about 0.6 ms where buildings fill the screen; p95 12.44 / 12.51 ms; batches p50 104, triangles p50 1.44 M (as P-1); no frame over 20 ms. The extra garbage is not yet traced; likely the facade arrays at new pool sizes and the per-chunk class arrays |
| Windows build | Succeeded, Mono, 138,369,919 bytes (132.0 MiB), 0 errors, 20 warnings, 41.9 s. Not run: no Windows machine (K-02) |

All shots are Unity development-player renders (Apple M1, Metal).

## Not done here, and why

- **Near detail.** At street level the far tier reads as painted (K-43). Geometry within about 150 m is D-06c.
- **Night windows.** No emission until the night systems (D-13).
- **Names.** Only the named-large rule reads names. Smaller named halls such as the Police Convention Hall (1,708 m²) get a walk-up facade (K-44). Level 1 landmarks come from an owner-reviewed list in D-06c.
- **Palette by category.** Wall colours are still the six plaster tones by id. Category tones and materials belong with D-06b's material families.
- **Courtyard walls.** Their normal-map tangent runs against s, so the plaster's relief is mirrored there. Not checked in a shot; left for D-06c.
