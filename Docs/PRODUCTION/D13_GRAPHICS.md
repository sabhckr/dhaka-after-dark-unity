# D-13 Graphics: day, night and quality levels (owner's brief, section 13)

Started 2026-11-06 on `wt/graphics`. Built on the URP project as it stood: no pipeline change. Before this work the project had no Volume at all, so no grading or bloom ever ran.

The brief names a reference game for mood. The work follows the look the brief spells out (grounded, muted, believable first), not that game's own art.

## What was added

| Brief | What is there | Where |
|---|---|---|
| 13.1 URP | Inspected first. Old PC and Mobile assets renamed High and Low (GUIDs kept), Medium added. No material was migrated. | `Assets/Settings/` |
| 13.2 Lighting | Existing `DayCycle` and `SkyModel` kept. Noon sun 1.25 to 1.15, inside the brief's 1.0 to 1.2. Stars dimmed. | `Runtime/Core/Sky/` |
| 13.3 Day and night | `DayCycle` is the brief's controller. `NightLife` says how much of the city is lit at each hour: windows, street lamps, shops, signs. All are curves, so nothing steps. | `Runtime/Core/Sky/NightLife.cs` |
| Street lights | A glow on every lamp: a pool on the road and a halo at the head. A small pool of real lights follows the camera (12 High, 6 Medium, 0 Low). | `StreetGlow.cs`, `DadGlow.shader`, `StreetLamps.cs` |
| Building lights | Painted windows, shops and signs light by their own hash, so a window keeps its rank all night. Built pieces carry a night mark instead. | `DadWorld.shader`, `WorldMaterials.Night` |
| 13.4 and 13.6 Post | One runtime volume: neutral tonemap, colour, white balance, split tones, bloom with a threshold above sunlit surfaces, light vignette. Values come from `GradeModel`, so they can be tested. | `Runtime/Core/Grade.cs` |
| 13.7 Quality | Low, Medium, High. `-dad-quality` wins, then the saved choice, then Low on integrated graphics and Medium elsewhere. High is never chosen for you. Shots and benches run High. | `Runtime/Core/GraphicsTier.cs` |
| 13.8 Inspection | 15 new views: street, high and shops at sunrise, midday, sunset, early night and midnight. | `Resources/Views/g13-*` |

### Quality levels

| | Low | Medium | High |
|---|---|---|---|
| Renderer | Forward | Forward+ | Forward+ |
| Shadow distance | 60 m | 100 m | 150 m |
| Cascades | 2 | 2 | 4 |
| Soft shadow quality | 1 | 2 | 3 |
| Shadow map | 1024 | 2048 | 2048 |
| Shadows from extra lights | No | No | Yes |
| Colour lookup size | 16 | 32 | 32 |
| Ambient occlusion | Off | Downsampled, lower blur | Full |
| Bloom | Off | On | On |
| Antialiasing | FXAA | FXAA | SMAA |
| Real lights on street lamps | 0 | 6 | 12 |

## Changes to world content

These are additions to the box, not changes to map data. Road and rail data are untouched.

- **Rule lamps.** Only 58 lamps are mapped in the box, and none on the trunk road, so at night the main avenue had no light. `PlaceRoadLamps` adds columns along trunk and motorway classes, both sides, staggered, every 40 m, keeping clear of every road, building and chunk border. Each is labelled `road lamp, by rule`, so it is never mistaken for a mapped lamp. Mapped lamps are unchanged.
- **Night marks.** Two kinds of built geometry would have stayed dark. Milestone 2's shop street is real geometry, not painted facade. The near tier's signboards cover the painted sign. Both now carry a mark in the top three bits of the material byte. The material and colour are unchanged.

## Not done

- 13.5 Materials: not reworked. The world shader already has wear, dirt, fading, cavity, road repairs and consistent metre-based scale. Decals are limited to road repairs and painted markings.
- Reflection probes: none added. The sky cubemap serves every surface, and a probe per street would need work in a streamed world. Glass shows the sky, not the street.
- Screen-space reflections: URP has none built in. Not added, in line with "only if performance allows".
- Flyovers and metro stations have no lights of their own. Decks and viaducts read as dark shapes at night. Stations are not built yet (layout page).
- Adverts are blank lit panels, on purpose: no invented lettering and no real trademarks.
- Near facades: glass in near windows shows the street lamp's reflection as a bright spot, which reads as a real reflection. Left in.

## Inspection findings

See `D13/golden-review.txt` for what each round of shots found and what changed because of it. Tests that caught real faults:

- `PolesLampsAndTreesStandClear` found rule lamps standing on asphalt, between the halves of a dual carriageway and at a chunk border. Lamps now check every road and stay 6 m off the border.
- `AWholeDayHasNoStepChanges` jumped at 08:00. The grade only updated on a frame and the test renders between frames, so this was the test, not the game: the test now applies the grade as a frame does. 51/51 PlayMode afterwards, largest change 2.33 a minute against a limit of 4.

## Measurements

See `D13/bench.txt`. All figures are from this Mac (Apple M1). None are from the target machine.
