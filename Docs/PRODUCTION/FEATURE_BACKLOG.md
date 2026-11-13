# Feature backlog

Statuses: **done** (with evidence), **ready** (all dependencies done), **blocked** (reason given), or **later**. The backlog is ordered by dependency, not by appeal. Effort is in estimated days.

## Phase A — audit and research

| Id | Task | Depends | Status | Evidence / acceptance |
|---|---|---|---|---|
| A-01 | Inspect machine, disk, Unity state | — | done | PERFORMANCE_REPORT.md, development machine table |
| A-02 | Audit existing project and data | — | done | `dhaka-after-dark/docs/architecture/project-audit.md` (dev/architecture 72976b5), verified today |
| A-03 | Storage budget | A-01 | done | PERFORMANCE_REPORT.md: 17.8 GB steady, 22.8 GB peak, gate before install |
| A-04 | Unity version and constraints | — | done | UNITY_ARCHITECTURE.md ADR-U1 |
| A-05 | DADC export, verifier, tests | A-02 | done | dev/architecture 7b4ea2c; 1,303 chunks PASS; 5 tests |
| A-06 | Vice City design research with sources | — | done | RESEARCH/VICE_CITY_DESIGN_RESEARCH.md |
| A-07 | Feature translation matrix | A-06 | done | RESEARCH/FEATURE_TRANSLATION_MATRIX.md |
| A-08 | Original game design | A-06 | done (first draft) | RESEARCH/ORIGINAL_GAME_DESIGN.md |

## Phase B — engine foundation

| Id | Task | Depends | Status | Acceptance | Effort |
|---|---|---|---|---|---|
| B-01 | Install Unity Hub, 6.3 LTS, Windows Mono module | A-03 | done (Hub 3.22.4, editor 6000.3.27f1 + windows-mono) | 10.6 GB by the release API, under the gate; `du` after install in PERFORMANCE_REPORT.md | 0.5 |
| B-01b | Activate the Unity licence | B-01 | done by the owner, 2026-10-30 | `Unity -batchmode` starts without "No valid Unity Editor license found" | owner |
| B-02 | Create URP project in this repository, packages, asmdefs, Boot scene | B-01b | done: settings from Unity's URP template (3d-cross-platform 17.0.14), unused packages removed, Boot scene by `ProjectTools.CreateBootScene` | Project opens; `git status` shows only intended files | 1 |
| B-03 | C# DADC reader in `Dad.World` (no UnityEngine dependency) | A-05 | done: EditMode 3 / 3 pass in the editor; also PASS outside it | EditMode test decodes all 1,303 chunks; counts match `world.json`; a C# re-hash of the source fields matches a hash written by the Node exporter | 1–2 |
| B-04 | Command-line test runner script (`Tools/run-tests.sh`) | B-02 | done: exits 0 on 3 / 3, exits 1 on a failing test and on zero tests (both checked) | EditMode and PlayMode results XML parsed; exit code honest | 0.5 |
| B-05 | Coordinate service + floating origin | B-03 | done: `Dad.World.ChunkCell`, `WorldFrame`, `OriginOffset`; `Dad.Core.FloatingOrigin`; EditMode 4 tests + PlayMode 1 test pass | Tests: chunk id ↔ cell ↔ world metres; origin shift keeps relative positions to 1 mm | 1 |
| B-06 | Input System actions (keyboard, mouse, gamepad), rebinding stub | B-02 | done: `DadInput` actions (Global, OnFoot, Driving, UI), `GameInput` context stack (on foot, driving, menu, paused), rebinding and saved overrides; 2 EditMode + 9 PlayMode tests. No vehicle gameplay | PlayMode test drives actions | 1 |
| B-07 | Windows Mono build script | B-02 | done: `ProjectTools.BuildWindows` succeeded, 118 MB, 0 errors, 0 warnings; **not run**, as there is no Windows machine | 0.5 |
| B-08 | Profiler markers and a fixed camera path | B-02 | done: `DadMarkers`, `CameraPath` + `farmgate-shahbag-air` (40 s), `FrameRecorder`, `BenchmarkRunner` (`-dad-bench`), `Tools/bench.sh`; capture saved for the empty Boot scene on the M1 player | Capture saved for the empty scene | 0.5 |

## Phase C — Farmgate vertical slice (box x 3000–5000, z −15000 to −13500: chunks 6–9 × −30 to −28)

| Id | Task | Depends | Status | Acceptance |
|---|---|---|---|---|
| C-01 | Streaming driver: rings, priorities, cancellation, time-sliced apply | B-03, B-05 | done: `StreamPlanner` (Chebyshev rings 2 / 4, lead cell, nearest first, retry once), `ChunkStreamer` (worker decode with cancellation, 2 ms apply slices, warm-up), `ChunkOutlines` debug lines; PlayMode walk and M1 player bench `slice-crossing-air` (max apply frame 1.4–1.9 ms) | PlayMode: walk the slice, no apply over 4 ms, loaded set matches rings |
| C-02 | Ground and landuse mesh per chunk | C-01 | done: `Earcut` (C# port), `ChunkSurfaces` (base plate, landuse, parks, water, canals, plazas on fixed layer heights, vertex colour), `Dad/Vertex Color Unlit` shader, ground apply step; golden view `kawran-bazar` via `Tools/shot.sh` | Golden view at Kawran Bazar |
| C-03 | Road meshes from centrelines (width by class, labelled rule) | C-01 | done: `RoadBuilder` (port of the browser build's `roads.ts`: chaining, stem trimming, crossing pads, gentle-bend fillets, mitred outlines, sidewalks, bridge decks with barriers and soffits), cut square to the chunk border with neighbour-matched caps; road apply step; golden views `kawran-bazar` (re-accepted) and `farmgate` | Junction spot checks; no gaps at chunk edges |
| C-04 | Building extrusion with `buildingLevels` / `buildingElevated` rules ported from `src/geo/infer.ts` | C-01 | done: `BuildingBuilder` (`BuildingRules` port of `infer.ts` levels, elevation and base, plus a labelled station rule: a layer-only `train_station` stands on the generator's layer height; `ChunkBuildings` walls, flat roofs, undersides; grey-box tones). The 16 Line 6 stations are raised (16 m for layer 3); 20 slice buildings match their data within 0.02 mm; golden views `kawran-metro` (new), `kawran-bazar`, `farmgate` | Metro stations elevated over Kazi Nazrul Islam Avenue; 20-building spot check |
| C-05 | Debug overlays: chunk grid, ids, graph | C-01 | done: `ChunkDebug` (cell square, chunk id in a 7-segment line font, road-graph edges, node crosses, signal squares, built on the worker), `Dad/Debug Lines` shader (drawn over everything), `FreeCamera` (WASD / sticks, mouse look, E / Q climb, Shift fast), `DebugViews` (F1 free camera, F2 grid and ids, F3 graph, F4 the C-01 debug lines, now off by default); golden view `farmgate-debug` with all overlays | Screenshot |
| C-06 | Places and landmarks export | A-05 | done | dev/architecture 2ebf234: 732 places, 1,986 landmarks, verifier PASS, test checks the Unity frame |
| C-07 | Builder allocation pass: per-worker reusable buffers in the ground, road, building and overlay builders, no main-thread allocation in apply steps, and a bench count of main-thread frames over 4 ms after set-up | C-02..C-05 | done 2026-11-02, except no main-thread allocation in apply steps (2.7–7.6 KB a frame remain; needs pooled chunk objects). Allocation 22.4 → 8.8 MB a run, collections 29–30 → 9–11, 0 apply steps with a collection in 8 runs, goldens exact. The whole-frame check awaits the owner's reading ([PHASE_C_EXIT_REVIEW.md](PHASE_C_EXIT_REVIEW.md), re-check) | GC allocation a run well under 22.4 MB; no collections bunched before apply batches; 3 unprofiled runs and 1 profiled with no game-caused main-thread frame over 4 ms after set-up; outputs unchanged (all builder tests and goldens exact) |

## Later phases (summary)

| Phase | Ordered tasks |
|---|---|
| D | D-01 to D-14, planned in [ENVIRONMENT_ROADMAP.md](ENVIRONMENT_ROADMAP.md): lit shader, materials, time of day, roads, footpaths, buildings, trees, street furniture and utilities, Bengali signage, stalls and shops, dividers and bridges, wet materials, night lighting, visual benchmark |
| E | E-01 protagonist model and rig (licensed or original); E-02 locomotion set; E-03 cameras; E-04 vehicle controller core (done 2026-11-06, [E04_PLAYER_VEHICLE.md](E04_PLAYER_VEHICLE.md)); E-05 vehicle specs: car, CNG, bus, motorcycle, rickshaw (models done from the owner's files, [E05_OWNER_MODELS.md](E05_OWNER_MODELS.md); only the Jetta is drivable); E-06 enter and exit |
| F | F-01 lane graph builder; F-02 per-junction signal groups; F-03 traffic simulation levels (F-01 to F-03 done 2026-11-06, [F03_TRAFFIC.md](F03_TRAFFIC.md)); F-04 pedestrians; F-05 clock hooks for gameplay (the clock and lighting come in D-03 and D-13); F-06 rain on D-12's wet materials |
| G | G-01 heat system; G-02 police responders; G-03 combat (melee first); G-04 economy ledger; G-05 properties and businesses; G-06 saves |
| H | H-01 mission runner; H-02 Act 1 missions; H-03 side jobs (fares, deliveries); H-04 radio framework; H-05 audio ambience |
| I | I-01 city-wide streaming; I-02 district identity passes |
| J | J-01 Windows IL2CPP pipeline; J-02 tier validation; J-03 accessibility; J-04 release checklist |
