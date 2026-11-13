# World performance audit (owner's brief, section 11)

Written 2026-11-06, from the code and the benches on record. Nothing here is a new measurement unless it says so. **The target machine has never been measured.** This Mac is not an Intel Core i3 with integrated graphics, so nothing below is evidence that 30 FPS is reached there.

Target, as the brief gives it: Intel Core i3 10th generation, 4 GB RAM, integrated graphics, 1280x720, 30 FPS on low settings.

## Item by item

| Brief | State | What is there | What is missing |
|---|---|---|---|
| District-based world streaming | Partly | `ChunkStreamer` streams 500 m chunks in rings: `LoadRadius` 2 (up to 25 chunks), `UnloadRadius` 4, `MaxInFlight` 3 on worker threads. | It knows chunks, not districts. Rings already keep the load flat as the map grows, so districts add nothing to memory on their own. They are useful for choosing what is available at each stage (`Docs/PRODUCTION/WORLD/districts.csv` has each chunk's district). `SliceOnly` holds it to the 12-chunk box until the owner picks a stage. |
| LOD Groups | Different method | Own tiers: `StreetLod` near to 150 m and far to 450 m, near facades dissolving in from `FadeEnd` to `FadeStart`, vehicle near and far meshes. | No Unity `LODGroup`. The tiers are per cell and built on workers, which a `LODGroup` per object would not improve. Trees (D-07) and people will need one. |
| GPU Instancing | Partly | `Graphics.RenderMeshInstanced` draws traffic (`TrafficView.cs:551`). The SRP Batcher is on. | World geometry is not instanced: it is unique per chunk, and AXIOM K.3 recorded why instancing was not pursued. Trees, lamps and street furniture are the candidates once D-07 and D-08 place more of them. |
| Occlusion Culling where beneficial | Not used | Camera frustum culling only. | Unity's baked occlusion cannot work on a world built at run time from chunk files. A run-time scheme would be new work and needs a bench to justify it. At street level, buildings do hide most of the city, so this is the likeliest large win if the target misses 30 FPS. |
| Texture optimization | Done | 24 MB budget kept (revised 2026-11-07 to 48 MB for all art textures, K-63). World set 16.33 MB plus up to 1.33 MB for the coach map. Not re-profiled since E-05. | People and weapons will add to it. The brief's budget stays unless a revised one is approved with profiling. |
| Efficient collision meshes | Done | `ChunkCollision`: simplified walls, ground, road and street solids, built per chunk, only near the player. | None known. |
| Controlled shadow distances | Done | High 150 m, Medium 100 m, Low 60 m (`Assets/Settings/*_RPAsset`). | Benches per level are in `D13_GRAPHICS.md`. |
| Asset pooling | Partly | `MeshArrays` uses `ArrayPool`. Traffic is held to a budget of 300 and reuses its slots. `StreetLamps` is a fixed pool of 12, 6 or 0 lights. | People will need the same. |
| Do not load the entire world | Done | Rings only. Up to 25 of the 1,303 chunks are ever resident. | Only 12 chunks have ever been loaded in practice, so a ring in a wider area has never been benched. |

## What has to happen before the target can be claimed

1. The owner picks a stage (layout page, decision 1).
2. Lift `SliceOnly` for that stage only, then bench a drive across it: chunk load time, peak memory, frame time, at each quality level.
3. Run a build on the target machine, or the closest one to hand. The owner has to do this: a Windows build made on this Mac proves it compiles, not that it runs.
4. If it misses 30 FPS, measure first. Likely order of work: fewer near-tier cells, then a shorter far-tier distance, then run-time occlusion.

Memory is the sharpest risk. The last bench peaked at 980 MB on this Mac with 12 chunks at High. A 4 GB machine has perhaps 1.5 to 2 GB free after Windows, and integrated graphics takes its share from the same RAM.
