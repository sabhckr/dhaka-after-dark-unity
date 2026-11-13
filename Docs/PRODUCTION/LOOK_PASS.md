# Look pass (2026-11-08, wt/look)

A pass over colour, haze, weathering, glass and ambient occlusion. Nothing about layout, buildings, roads, placement, vehicles, gameplay or the player changed. No asset was added, deleted or re-imported; no `.mat` file changed.

## What changed

### Grade (`Runtime/Core/Grade.cs`, `GradeModel.At`)

| Setting | Day, old | Day, new | Night, old | Night, new |
|---|---|---|---|---|
| Exposure | 0.05 | -0.08 | 0.32 | 0.18 |
| Contrast | 8 | 11 | 5 | 7 |
| Saturation | -16 (low sun -7) | -26 (low sun -15) | -10 | -16 |
| Temperature | 7 (low sun 10) | 3 (low sun 7) | -7 | -5 |
| Bloom | 0.10 | 0.05 | 0.28 | 0.20 |
| Vignette | 0.14 | 0.08 | 0.20 | 0.12 |

The day is greyer and less warm, so the city's own paint and dirt carry the colour; the night keeps more of the dark. `NightLifeTests.GradeIsGentleAndNeverSaturates` now allows saturation down to -30 (it was -20).

### Haze (`Runtime/Core/Sky/DayCycle.cs`, `SkyModel.cs`)

Fog stays Linear and still follows `SkyModel`'s density. Start is `0.05 / density` (was `0.15 / density`), about 45 m instead of 136 m at the base density; end is unchanged, about 1,300 m. So the air now shows in the middle distance as well as on the horizon. `SkyKeys` were desaturated: at 40 degrees of sun the sky goes from `4a78b8` to `5f7a9b`, the horizon from `c4cad0` to `c4c5c3`.

### Surfaces

`Shaders/DadCity.shader` (the owner's instanced models):

- Dirt at the foot of each building, varied per building.
- Weathering on walls only: streaks, blotches, grime and fading, varied per building; fine noise fades out by distance so it does not shimmer.
- A glass mask from a blurred texture sample. Glass is darkened, keeps less of the weathering and is the only part that gets a sharper highlight (`smoothness` 0.18 walls, 0.62 glass).
- Lit windows are fewer (`_DadLights.x * 0.2`, was 0.3), confined to the glass or dark parts of the picture, and dimmer.

`Shaders/DadWorld.shader`, the `MaterialWear` table (variation, dirt, fading):

| Material | Old | New |
|---|---|---|
| Paving | .12 .45 .10 | .20 .62 .20 |
| Asphalt | .14 .35 .25 | .22 .55 .35 |
| Concrete | .12 .40 .10 | .18 .60 .15 |
| Plaster | .10 .45 .25 | .16 .62 .35 |
| Roof | .15 .80 .10 | .18 .85 .15 |
| Brick | .10 .35 .10 | .14 .48 .15 |
| Paint | .06 .25 .40 | .10 .40 .55 |
| Metal | .05 .30 .00 | .08 .42 .05 |
| Marking | .05 .30 .30 | .10 .60 .60 |

Asphalt's bright stone chips are clamped to the surface colour; road markings are 28 % darker; facade glass smoothness 0.85 to 0.62.

The materials affected through those shaders: City, CityCut, ChunkBuildings, ChunkGround, ChunkRoads, ChunkSurfaces, World, WorldNear, WorldFacades.

### Render settings

SSAO on `High_Renderer` and `Medium_Renderer`: intensity 0.4 to 0.7, radius 0.3 to 0.5. Low keeps 0.4 and 0.3. High already had soft shadows, 2048, 150 m, 4 cascades; not changed.

### New views

`l-road`, `l-wall`, `l-glass`, `l-skyline`, `l-overcast` (`Resources/Views`, with their metas).

## Performance

Paired runs on the M1 Air (8 GB), 1280x720, on mains, lid open, vSync on, in the order old, new, new, old. All 16 completed. Texture memory (100 MB High, 72 MB Low), set passes and batches did not change. With vSync, frame p50 is 16.67 ms on every run, so the GPU figures say more.

| Route, level | Measure | Old, two runs | New, two runs |
|---|---|---|---|
| hub-rich-air, High | GPU p50 | 16.61, 13.22 | 14.05, 15.00 |
| | GPU p95 | 31.46, 31.11 | 28.44, 30.38 |
| | Main thread p50 | 5.22, 5.10 | 5.00, 5.13 |
| | Frames over 33 ms | 112, 0 | 41, 38 |
| central-avenue-street, High | GPU p50 | 15.32, 16.50 | 18.13, 17.92 |
| | GPU p95 | 28.84, 28.31 | 30.06, 29.34 |
| | Main thread p50 | 4.79, 5.02 | 4.93, 5.07 |
| | Frames over 20 ms | 121, 127 | 147, 206 |
| | Frames over 33 ms | 55, 7 | 63, 22 |
| slums-airport-air, High | GPU p50 | 23.01, 21.70 | 23.86, 23.69 |
| | GPU p95 | 33.31, 32.13 | 34.18, 34.22 |
| | Main thread p50 | 4.70, 4.64 | 4.74, 4.78 |
| | Frames over 20 ms | 375, 340 | 398, 396 |
| | Frames over 33 ms | 240, 239 | 269, 288 |
| hub-rich-air, Low | GPU p50 | 6.78, 6.92 | 7.60, 6.82 |
| | Frame p95 | 16.73, 16.89 | 16.71, 16.75 |

Reading: Low and the main thread are unchanged. High costs about 2 ms of GPU at the median on the street and about 1.4 ms on the airport route. The hub route's old runs disagree with each other too much to say anything. The airport route was over budget before this pass and is slightly worse. See K-67.

## Validation

EditMode 294: 291 passed, 0 failed, 3 inconclusive (K-65). PlayMode 104/104. Run after the last change, on the final goldens.

All 143 views were rendered from the final build with 0 failed polygons and all 143 were reviewed by eye on contact sheets before being accepted as goldens (138 changed, 5 new). Old and new players were built from the same commit apart from this pass. The picture quality is a judgement made from those stills, not a measurement; plain play in the built player was not walked through for this pass.

Remaining limits: K-67 to K-70.
