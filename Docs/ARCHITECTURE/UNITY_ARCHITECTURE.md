# Unity architecture

Status: **foundation in progress.** Unity 6000.3.27f1 with URP 17.3.0 opens the project in batch mode. Built and *verified*: the `Dad.World` reader, chunk cells and the floating origin, input contexts, the Boot scene with `GameRoot`, and a Windows Mono build (succeeded, not run). Tests: EditMode 9 / 9, PlayMode 11 / 11. Packages in use: URP 17.3.0, Input System 1.20.1, Test Framework 1.6.0, uGUI 2.0.0 and the IDE packages. Everything else that names a Unity API is a plan.

## Engine decision (ADR-U1)

- **Decision:** Unity 6.3 LTS (6000.3), Universal Render Pipeline, Windows PC target. Chosen by the owner on 2026-10-30.
- **Supersedes:** the *Proposed* ADR-001 in `dhaka-after-dark/docs/architecture/adr-001-engine.md`. That ADR preferred Unreal 5 only if a Windows PC was available, and named Unity 6 as the fallback. The development machine is an 8 GB M1, which is below Unreal's macOS minimum of 16 GB but meets Unity's stated recommendation of 8 GB.
- **Why 6.3 LTS:** it is Unity's newest LTS, supported until December 2027 [U1]. Unity 6.0 LTS support ends in October 2026 [U1]. Newer supported update releases exist (the manual's current version is 6.6 [U2]), but an LTS is the stable base for a long project.
- **Pinned patch: 6000.3.27f1** (changeset 672d562a42a7, released 2026-10-20), installed 2026-10-30. URP 17.3.0 and Test Framework 1.6.0 are the versions bundled with it. Upgrade only between milestones.

| Constraint | Consequence |
|---|---|
| macOS editor needs macOS 13+, Apple M1 or later; 8 GB RAM is the recommended minimum [U2] | Works, with no headroom. Close browsers while the editor runs; keep the scene to one district |
| A Windows player from macOS is Mono-only; IL2CPP for Windows needs a Windows machine with Visual Studio [U2][U3] | Development builds: Mono, built on the Mac, **not runnable on the Mac**. Release builds: IL2CPP on a Windows machine or CI runner (Phase J) |
| Realtime GI and the Progressive CPU lightmapper need Rosetta on Apple silicon [U2] | Use baked-light-free lighting (URP realtime main light plus light probes) or the Unity Compute Light Baker |
| No Windows hardware to test on | Every Windows claim waits for a Windows test machine; Mac editor numbers are recorded as Mac numbers |

## Packages

Only Unity-published packages until a third-party one passes a licence, size and compatibility check (recorded in this file).

| Package | Use | When |
|---|---|---|
| Universal RP | Rendering | Phase B |
| Input System | Keyboard, mouse and gamepad with rebinding. *Built (B-06):* `Assets/_Project/Input/DadInput.inputactions` is the project-wide asset. Its maps are Global (Pause), OnFoot, Driving and UI. `Dad.Core.GameInput` keeps a context stack (OnFoot, Driving, Menu, Paused) and enables only that context's maps, so no key is live in two maps. It subscribes its callbacks once, pauses when a gamepad is lost during gameplay, and has no per-frame code | Phase B |
| Test Framework | EditMode and PlayMode tests, command-line runs | Phase B |
| Burst, Collections, Mathematics | Traffic and crowd simulation as jobs | Phase F, after a profile shows the need |
| Cinemachine | Third-person and vehicle cameras | Phase E |
| Profile Analyzer | A/B of profiler captures | Phase B |

Not planned:
- **DOTS/Entities:** too much framework for one developer. Plain C# plus Jobs covers the simulation.
- **Addressables:** chunk data is in StreamingAssets, and the art set is small. Revisit in Phase I if build size or memory says so.
- **glTFast:** geometry is built from DADC data, not imported meshes.

## Project layout

```
dhaka-after-dark-unity/          git root (local; no remote yet)
  Assets/
    _Project/
      Runtime/
        Core/         Dad.Core.asmdef        clock, events, game state, settings
        World/        Dad.World.asmdef       DADC reader, streaming, chunk builders, floating origin
        Player/       Dad.Player.asmdef
        Vehicles/     Dad.Vehicles.asmdef
        Traffic/      Dad.Traffic.asmdef     lane graph, agents (jobs)
        People/       Dad.People.asmdef      pedestrians
        Police/       Dad.Police.asmdef
        Combat/       Dad.Combat.asmdef
        Missions/     Dad.Missions.asmdef
        Economy/      Dad.Economy.asmdef     money, properties, businesses
        Save/         Dad.Save.asmdef
        Audio/        Dad.Audio.asmdef
        UI/           Dad.UI.asmdef
      Data/           ScriptableObjects: vehicle specs, crime table, mission scripts, tiers
      Art/            original or licensed only, each folder with a LICENSES.md
      Scenes/         Boot, MainMenu, World
      Tests/
        EditMode/     Dad.Tests.EditMode.asmdef
        PlayMode/     Dad.Tests.PlayMode.asmdef
    StreamingAssets/World/   generated, gitignored (Tools/sync-world-data.sh)
  Docs/  Tools/  WorldData.lock
```

Assembly definitions make the dependency direction a compile error instead of a convention. The allowed references follow the module order below, and `Core` references nothing.

## Modules and ownership

The module split (A–O) comes from `dhaka-after-dark/docs/architecture/module-architecture.md`. Budgets are **targets** for the HIGH tier at 60 FPS, to be measured in Phase B and re-set from measurements.

| Module | Owns (single writer) | Unity form | Thread | Budget |
|---|---|---|---|---|
| Core | Game clock (fixed 30 Hz simulation step), event bus, game state, settings | Plain C# classes created by one `GameRoot` MonoBehaviour in the Boot scene | Main | 0.2 ms |
| World data | DADC reader, chunk records, places | Plain C#, no `UnityEngine` reference: unit-testable and safe on worker threads | Worker | 0 ms main thread |
| Streaming | Resident chunk set, priorities, cancellation, pools | MonoBehaviour driver plus a time-sliced build queue (the three.js `IntegrationQueue` pattern). *Built (C-01):* `StreamPlanner` decides, `ChunkStreamer` reads and decodes on a `Task` with cancellation and applies in steps. *Built (C-07):* builder pools (see Chunk builders); no object pools for chunk roots and meshes yet. Finished chunks wait to be applied while other loads are still building, for at most `MaxApplyHoldFrames` (30); once a wait runs out, the queue is applied until it is empty. The workers' allocations start garbage collections, which stop every thread; this keeps those collections out of apply steps | Main + worker | 2 ms |
| Chunk builders | Meshes for roads, ground, buildings, props | `Mesh.MeshData` written in jobs, applied on main; GPU instancing for repeated props. *Built (C-02):* ground surfaces are plain arrays built on the streaming worker (`ChunkSurfaces`, `Earcut`) and uploaded with `SetVertexBufferData` in one apply step. *Built (C-03):* roads the same way (`ChunkRoads`), cut on chunk borders so neighbours meet. *Built (C-04):* buildings (`ChunkBuildings`, heights from `BuildingRules`). *Built (exit review):* the Mono player compiles a method on its first call, so the streaming warm-up compiles ahead the streamer, the planner, the types their fields hold (chunk views, payloads, results) and the generic and threading types among them. The builders themselves are compiled on the workers, off the main thread (`ChunkStreamer.PrepareCode`, 1,139 methods, through `MethodHandle.GetFunctionPointer`; `RuntimeHelpers.PrepareMethod` does nothing on Mono). Arrays, delegates, pointers and runtime or internal-call methods are skipped: asking Mono to compile those breaks it. Under IL2CPP (Phase J) the call is unneeded. *Built (C-07):* the builders reuse their memory. Each load rents a `BuildScratch` set (mesh and polygon lists, Earcut's nodes, the road builder's outlines, point lists and dictionaries) for its whole build and gives it back on the same worker (`Rent`, then the `Lease` is disposed), so there are only as many sets as loads in flight; tests and the main thread use a set of their own thread. The road, building and line mesh arrays come from `MeshArrays` pools: the streamer builds them `pooled`, uploads the first `VertexCount` and `IndexCount` elements, and hands them back (`Release`) in the same apply step; payloads that are discarded or dropped mid-apply hand theirs back too. Built anywhere else, these arrays are exact copies. Ground and overlay arrays stay exact (they are small). The warm-up builds its made-up chunk on `MaxInFlight` worker tasks, so the thread pool's threads and the completion path exist before the first real load. *Built (D-04):* road markings and street details are part of the road mesh (`RoadMarkings.cs`, a partial of `ChunkRoads`), so streaming and apply are unchanged. After the roads and pads are drawn, every road is kept as drawn (`Drawn`: points after chaining and fillets, half widths, arc length, mitred offsets, and per-segment attributes: way, class, lanes tag, one-way from the graph edges, class rank). Lines are quads with the `Marking` material, 2 cm up; dashes are counted along the road from where it crosses the chunk border, so they carry on into the neighbour. Lines are cut by keep-outs (junction pads, zebras, stop-line blocks, signal junction boxes) and by the carriageways of other roads of equal or higher rank at the same level. Breakers, covers, grates and potholes are placed by rule, by a hash of the way id; each is built by the chunk that holds it. `Build(c, keepMarks: true)` also returns every mark and keep-out, for the tests. *Built (D-05):* sidewalks, kerbs, bus-stop pads and open drains are part of the road mesh too (`Sidewalks.cs`, another partial of `ChunkRoads`), built after the markings: sidewalks for every road in rank order, then drains. Each side of a road is walked as a band along the drawn road: its width is sampled every 5 m (closer where it changes) against building walls, other carriageways, junction pads and earlier bands, which come from a cell index of convex rings (each segment's mitred trapezoid joined with its extended rectangle, and bevel wedges; the road's own nearby segments use the exact, slightly inset trapezoid). The band is cut where it would run onto any of them, and each quad is checked again (`Sound`) and halved down to 0.25 m where it would overlap. Kerb faces, outer faces and end faces close every band. Zebras drop the kerb to the road; broken slabs, paving tints and bus-stop pads are chosen by rule. `keepMarks` also returns the bands (`Walks`), for the tests | Worker → main | inside streaming |
| World rendering | Look of the chunk meshes: shader, materials, lighting | *Built (D-01):* one URP shader, `Dad/World`, PBR-lit (`UniversalFragmentPBR`) with main-light shadows, Forward+ lights, SSAO and fog; SRP Batcher compatible. The mesh format is unchanged (positions, RGBA8 colour): rgb is sRGB albedo, alpha the `WorldMaterial` id, which picks the material's texture layer, tile period, sampling mode and wear from the shader's tables. *Built (D-02):* `MaterialLibrary` (editor) generates 16 tileable layers from seeded recipes (`LayerRecipes`, `TileNoise`): albedo, normal and a mask holding metallic, occlusion, cavity and smoothness. They are imported as three 16-slice BC7 arrays (512 px, 16.3 MB with the macro texture). Stochastic sampling on a 2 m triangle lattice (`WorldUvLattice`, period 4,000 m) blends three randomly offset reads, rotated as well for most layers, and preserves the albedo's variance. A macro texture at 50 and 500 m varies brightness, dirt and fade, and hashed repair patches mark asphalt and concrete. `MaterialSheet` renders the 18 materials to `Docs/ART_DIRECTION/MATERIAL_SHEET.png`. Normals come from screen-space derivatives (exact for these flat faces). UVs are in world metres: plan view on flat faces, box-projected on walls, offset by the wrapped origin (`_DadWorldWrap`, 4,000 m) so textures hold across origin shifts. Buildings cast shadows with front faces culled (closed solids, no acne without a normal bias); ground and roads only receive. `RenderWarmup` draws the streamer's made-up chunk once at start through a hidden camera, so the shader and URP's shadow code are ready before the first real chunk. Plan: [ENVIRONMENT_ROADMAP.md](../PRODUCTION/ENVIRONMENT_ROADMAP.md) | Main (render) | see PERFORMANCE_REPORT |
| Time of day | Clock, sun and moon, sky, ambient, fog, sky reflection, night factor | *Built (D-03):* `DayCycle`, made by `GameRoot` on the scene's directional light, holds a local date and time (Bangladesh, UTC+6) and the weather. Each frame (`Dad.Sky.Apply`) it places the sun and moon (`Celestial`, in `Dad.World`: Meeus' low-precision sun, sidereal time and moon, with refraction and the moon's parallax) and evaluates `SkyModel`, a pure function of the sun's altitude, the moon and the weather. Every value is a curve over the sun's altitude, not a preset: the sun's colour is the transmittance of its air mass, sky colours are keyed by altitude, and so are the night factor, fog, clouds and stars. The sun hands the main light (`RenderSettings.sun`, the one with shadows) to a second directional light, the moon, at −2°, where its own light is already 0; the moon's shadows come in after that. Ambient is trilight; fog is exp². `Dad/Sky` draws the sky from the same globals (`_DadSunDir`, `_DadZenith` and so on): gradient, haze glow, one cloud layer from the periodic macro noise, stars turned by sidereal time, the moon lit from the sun's direction (so its phase is right) and the sun's disc. `SkyReflection` fills a 16-texel cubemap on the CPU as the default reflection: whole at start and after a jump in time, then a face a frame (`Dad.Sky.Reflection`). `_DadNight` is there for D-13. The clock stands still in benches and golden views and runs at half a game minute a second in plain play; F6 adds an hour, F7 changes the weather | Main | 0.07 ms (player); 0.2 ms with a refill face (editor) |
| Floating origin | World offset | Shift everything when the player passes 2 km from the origin. *Built (B-05):* the origin sits on a 500 m cell corner, so each shift is whole cells and exact in float; root objects of all loaded scenes move, height is never shifted, `Dad.FloatingOrigin.Shift` profiler marker. Not yet moved: DontDestroyOnLoad objects, particles, trails, interpolated rigidbodies | Main | spike-free |
| Road and lane graph | Nodes, lanes, signal groups | Plain C# from DADC graph plus rules | Worker build | 0.5 ms queries |
| Player | Movement, camera, interaction, health | CharacterController + Animator + Cinemachine | Main | 0.5 ms |
| Vehicles | Player and near-traffic vehicles | Rigidbody + WheelColliders, per-class ScriptableObject spec. *Built (E-04):* Rigidbody with raycast suspension in plain C# (`Runtime/Vehicles`), so one model runs in tests and in play, not WheelColliders; specs in `HandlingSpec`, not ScriptableObjects | Physics | 1.5 ms |
| Traffic | Near/mid/far/outside agents | Near: physics vehicles. Mid: kinematic on lanes. Far: counts per edge. Outside: per-chunk totals | Jobs | 1.5 ms |
| Pedestrians | Crowd agents | Sidewalk graph, kinematic, GPU-instanced at distance | Jobs | 1.5 ms |
| Police | Heat level, incidents, responders | Plain C# state machine + responders as traffic/ped agents | Main | 0.5 ms |
| Combat | Weapons, damage, hit reactions | Components on player and NPCs, data in ScriptableObjects | Main | 0.3 ms |
| Missions | Mission runner, objectives, flags | Data-driven runner reading mission assets; no per-mission MonoBehaviour code | Main | 0.3 ms |
| Economy | Money, properties, businesses, inventory | Plain C#; ledger of transactions | Main | event-driven |
| Save | Slots, format, migration | Plain C# JSON (versioned), atomic write: temp file → fsync → rename, keep previous as `.bak` | Worker I/O | < 200 ms |
| Audio | Ambience, vehicles, radio | AudioMixer, pooled AudioSources, zones from landuse | Main | 0.3 ms |
| UI | HUD, menus, map, settings | UI Toolkit | Main | 0.5 ms |

## Rules

1. **Plain C# first.** Simulation state lives in plain classes and structs that EditMode tests can construct. MonoBehaviours bind them to the scene.
2. **One event bus, typed events** (`CrimeCommitted`, `VehicleEntered`, `MissionObjectiveDone`, …). Upward calls go through events, never through `FindObjectOfType`.
3. **ScriptableObjects for tuning data** (vehicle specs, crime weights, prices, quality tiers). Never for runtime state.
4. **No main-thread parsing during play.** Chunk files are read and decoded on a worker (`File.ReadAllBytes` + DADC decode in a `Task`). The main thread only applies finished meshes within the streaming budget.
5. **Coordinates.** World data is in metres, x = east, z = north, from the world origin (UTM 46N E 230000, N 2615000). Positions stay `double` in data and convert to `float` relative to the floating origin.
6. **Every system has a debug view** (F-keys in development builds): chunk grid, lanes, agent levels, heat radius, mission state. *Built (C-05):* `DebugViews` reads the Global map: F1 the free camera (`FreeCamera`, its own input context and map), F2 the chunk grid and ids, F3 the road graph, F4 the C-01 debug lines, F5 the world shading (lit, normals, material ids, metre grid, albedo: `WorldShading`, one global shader value, D-01), F6 an hour later and F7 the next weather (`DayCycle`, D-03). The overlays are built on the streaming worker (`ChunkDebug`), uploaded in the overlay apply step, drawn with `Dad/Debug Lines` over everything, and all off at start; `ChunkStreamer.SetOverlays` applies a change to resident chunks, chunks mid-apply and later ones. Golden views can turn them on (`overlays` in the view file), and pick a shading (`shading`). Until Phase E brings a protagonist, plain play starts in the free camera over Farmgate, so the F-keys are in every build for now; the release checklist gates them.

## Testing

| Level | Tool | Runs |
|---|---|---|
| Data contract | Node tests in the dhaka-after-dark repo (`scripts/unity/dadc.test.ts`) | *Verified*: 5 tests |
| C# reader outside the editor | `Tools/dadc-check.sh`: Unity's bundled Roslyn and Mono, all 1,303 chunks against `world.json` | *Verified*: PASS |
| C# logic | Unity Test Framework, EditMode: DADC reader, lane graph, heat transitions, economy ledger, save migration | Command line: `Unity -batchmode -runTests -testPlatform EditMode` |
| Scene behaviour | PlayMode: chunk load/unload, vehicle drive path, mission flow. *Built (C-05):* every fixture unloads the scenes its test loaded in `[UnityTearDown]` (`TestScenes`), so one failed test cannot fail the next | Same, `-testPlatform PlayMode` |
| Build | Windows Mono player build from the Mac; Windows IL2CPP later | `-executeMethod Dad.Build.Windows` |
| Performance | Profiler captures on fixed camera paths; Profile Analyzer A/B. *Built (B-08):* `Tools/bench.sh` runs `BenchmarkRunner` in the macOS development player along `Resources/CameraPaths/*.json`. Its output is `summary.json` (frame, CPU, main-thread, render-thread and GPU percentiles from `FrameTimingManager`, GC per frame, system memory, a `complete` flag), `frames.csv`, and optionally `capture.raw`. Markers are named `Dad.*` (`DadMarkers`). *Built (exit review):* `--capture` keeps 2,000 frames (all the editor holds), from `--capture-from N`. `Tools/capture-report.sh` reads a capture in batch mode (`CaptureReport`) into `markers.tsv`, and `Tools/slow_frames.py` lists the frames whose main thread was busy, PlayerLoop minus vsync and GPU waits, over a limit. Long frames are counted by CPU frame time as well as `frameMs`, which can hide presentation stalls. *Built (C-07):* `summary.json` counts main-thread frames over 4 ms after set-up (`mainFramesOver4ms`), how many of them are within 3 frames of streaming work (planning plus apply 0.3 ms or more, or a garbage collection; `mainFramesOver4msNearStreaming`) and lists the first 20; `frames.csv` adds `streamingPlanMs`; `Dad.Streaming.Collect` and `Dad.Streaming.Start` mark the rest of the streamer's frame. `CaptureReport` also writes `jit.tsv`, the `Mono.JIT` samples by frame and thread. On macOS the recorder also reads the main thread's CPU clock (`clock_gettime`, `CLOCK_THREAD_CPUTIME_ID`) once a frame: `mainCpuMs` in `frames.csv`, `mainThreadCpuMs` in the summary, and the CPU beside each slow frame in the list (`row:ms/cpu`). A slow frame with ordinary CPU time was not running, which no capture shows | Per milestone |

The first C# test is a cross-language check: the C# reader must decode every chunk in `StreamingAssets/World` to the same counts and hashes that `world.json` records.

## Sources

- [U1] Unity 6 releases page (6.3 LTS until December 2027; 6.0 LTS through October 2026): https://unity.com/releases/unity-6
- [U2] System requirements for Unity 6.6 (current manual): https://docs.unity.com/en-us/engine/current/manual/get-started/install-and-upgrade/getting-started-installing-unity/system-requirements
- [U3] Unity forum, "Why is there no Windows (IL2CPP) build module for macOS installations of Unity?" (staff replies: not supported, Mono works): https://discussions.unity.com/t/why-is-there-no-windows-il2cpp-build-module-for-macos-installations-of-unity/919172
