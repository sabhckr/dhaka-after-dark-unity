# F-01 to F-03: lanes, signals, traffic and the NPC vehicles

The owner asked for this on 2026-11-06:

> go for npc vehicles and other details. do not ask for permission. go as it was asked

That lifts the earlier order not to start vehicles in Phase D, and the D-07 and D-08 gate, for this work. The target is what the briefs asked: FEATURE_TRANSLATION_MATRIX section 8 (F-01 lane graph, F-02 signal groups, F-03 traffic levels), DETAILING_DIRECTIVE parts 14 and 15, and CHARACTER_VEHICLE_REFERENCES. The family models are the NPC half of E-05; driving a vehicle (E-04) is not part of this.

## What was built

**Lanes (F-01, `Runtime/World/Lanes.cs`).** Each chunk's lane graph is built off the main thread from the road graph in the chunk file, as the chunk streams in. Nothing in the chunk file changes.

- **Left-hand traffic.** Each direction takes the left of its travel, lane 0 next to the left kerb. The lane count is the one the road's markings show (`RoadMarkings.LaneCount`).
- **Left out, and counted:** footways and paths, elevated decks (the flyovers have no ramps, K-13), and a direction with no room left.
- **What stands on the road.** Piers, median islands and the median narrow the band a direction may use, so no lane runs through a pier (`ChunkStreet.Blocks`).
- **K-31.** The widths are mostly estimates, so paired one-ways and links overlap in plan. Overlapping roads share the overlap between their lanes; 41 directions in the slice close for lack of room (K-58).
- **Junctions.** Lanes are cut back and joined by connectors, short Bezier lanes. Connectors that cross or share a target conflict. Arms on a chunk border stay open and join the neighbour through the shared graph node.

**Junctions (F-02).** Slice counts: Priority 169, AllWay 166, Officer 21, Signal 1.

| Kind | When | Rule |
|---|---|---|
| Signal | A mapped signal; every signalled node of one crossing in one group | Phases, 26 s green, 2.5 s amber, 3 s all red |
| Officer | Two major roads cross and no signal is mapped (a labelled rule: the design's "signal or officer") | Phases, 24 s |
| Priority | Roads of different rank | The lesser road waits for a gap, and nudges out after 22 s |
| AllWay | Equal roads | Whoever has waited longest |

**Traffic (F-03, `Runtime/Traffic`, no engine dependency).** All of it runs headless in fixed 0.1 s steps.

- **Levels.** Near and Mid (out to 320 m) are driven vehicles; Far is a count per lane; Outside is a total per chunk not in memory, kept for when it comes back. A pool of 300 by default, inside the brief's 200–400.
- **Driving.** Car following is the Intelligent Driver Model with each vehicle's own numbers, so a bus does not pull away like a motorcycle. A body lies back along the lanes it has come by, so a bus swings round a corner.
- **Junction rule.** A vehicle stops at the line unless it holds its connector. A grant needs no crossing connector held, room beyond (no blocking the box), green where there is a controller, and nobody with a better claim.
- **The city's habits.** Rickshaws keep left and stay off trunk and primary roads; motorcycles and CNGs filter; heavy vehicles stay off the lanes; the mix changes by hour, and rush hours fill the pool.
- **Gridlock watch** (`GridlockWatch.cs`). The brief's rule: more than 15 % of the vehicles within 45 m of a junction at a dead stop for over 90 s.

**Vehicles (`VehicleKit.cs`, `VehicleFamilies.cs`).** Eight families generated in code, no downloaded assets: sedan, CNG auto-rickshaw, cycle rickshaw, motorcycle, city bus, intercity coach, truck and microbus.

- **Assembled, not shells.** Panels with real gaps over a dark shell, open wheel arches, separate lamps, handles, mirrors, wipers, doors, seats and a driver behind real glass. The bus has real doorways with steps. `VehiclesAreAssembledNotShells` tests this.
- **No text and no trademarks.** Plates and destination boards are blank until the writing work (D-09). All designs are generic.
- **Levels.** Near mesh to 60 m (2,580–8,198 triangles) with instanced wheels; far mesh (148–270 triangles) with shadows to 160 m and without to 460 m. The directive's "Do not make every NPC a hero-quality mesh" is why the far mesh is one small mesh, wheels and all.
- **Working parts.** Front wheels steer, wheels turn with distance, the body pitches under braking and a motorcycle leans. Brake lights, headlamps and indicators work from lamp numbers in the mesh. The 8 nearest vehicles carry real spot lights at night, which come up with the lamps at dusk.

**In the game (`Runtime/Core/TrafficView.cs`).** Lanes arrive with the chunks (`ChunkStreamer.LanesIn`). Vehicles are drawn with `Graphics.RenderMeshInstanced` per family and level. Traffic is on in plain play and off in existing golden views and benches unless they ask for it (`ViewSpec.traffic`, `-dad-traffic`, `Tools/bench.sh --traffic`), so those stay comparable.

## What is not built

Listed so nothing here reads as more than it is.

- **Nobody can drive a vehicle.** The near physics level waits for the player's vehicle (E-04). Vehicles have no colliders and do not react to the player.
- **Traffic officers have no figure.** An Officer junction runs its phases with nobody standing there.
- **No vehicle uses the elevated decks** (K-13).
- **Only 6 of the 13 bus stops** are placed on a lane; buses stop at those and keep to their lane to do it.
- **No signal heads yet.** That is D-08 work. The stop point of a Signal junction (5.3 m) has not been checked against the drawn stop line.
- **Street density is thin mid-block.** Traffic bunches in queues at the junctions, as the pool allows.
- **Scooters, pickups, vans, minibuses, bicycles, emergency, police and construction vehicles** are not built.

## Results

| Check | Result |
|---|---|
| Unity EditMode | **193 of 193** on the final code (28 new in `TrafficTests`) |
| Unity PlayMode | **33 of 33** on the final code (3 new in `TrafficViewTests`). Its first run was 31 of 32: see below |
| Mutations | 26 mutants, 20 caught at first; tests added or tightened; **26 of 26** ([F03/mut_result.txt](F03/mut_result.txt)) |
| Golden views | 14 new, reviewed by eye ([F03/golden-review.txt](F03/golden-review.txt)), and repeat exactly; **49 existing views match**, traffic being off there |
| 10 minutes, seed 1, morning rush | 1,345 spawned, 32 overlapping pairs, 2 vehicles stuck over 60 s, worst 61.6 s, 19 released by the nudge, 0 gridlock flags |
| 30 minutes, seed 7 | 3,030 spawned, worst stuck 75.8 s, 44 released, 0 gridlock flags. Seeds 11 and 12 do flag: K-57 |
| Junction rule | 0 clashing pairs over 600 samples; 94 grants at the signal, 0 on red |
| Inventory | 108 rows of `Vehicles.csv` marked; check passes |

**Cost, traffic off against on** (one player, 8 runs a path, order off on on off on off off on, MacBook Air M1, AC, lid open; from frame 300; [F03/bench.txt](F03/bench.txt)):

| Path | Main-thread p50 | GPU mean | Frames over 20 ms a run |
|---|---|---|---|
| `slice-crossing-air` | 2.27 → 2.39 ms | 4.87 → 5.01 ms | 1–2 → 1–3 |
| `kawran-farmgate-street` | 2.40 → 3.09 ms | 8.45 → 8.55 ms | 1–2 → 1–2 |

Runs of one player move by up to 0.6 ms (K-56), so the air path's +0.12 ms is inside the noise; one air "off" run read 2.60 ms. The street's +0.69 ms is not: every "on" run is above every "off" run. The GPU differences are also inside the run-to-run spread.

**Against the 1.5 ms traffic budget** (profiler captures, 2,000 frames each, traffic on, profiler running):

| Path | All traffic markers, mean | p99 | Max | Frames over 1.5 ms |
|---|---|---|---|---|
| `slice-crossing-air` | 0.10 ms | 0.94 ms | 8.13 ms | 4 of 2,000 |
| `kawran-farmgate-street` | 0.45 ms | 2.03 ms | 2.79 ms | **94 of 2,000** |

The mean is well inside the budget; the street's worst frames are not. The simulation steps 10 times a second, all in one frame, on top of 0.29 ms of drawing. This is recorded as K-59, not fixed. The markers explain 0.45 ms of the street's +0.69 ms; the rest was not traced.

**Windows build:** succeeded (`ProjectTools.BuildWindows`, Mono), 138,695,828 bytes, 0 errors, 20 warnings, 44.1 s. Built on macOS, not run on Windows (K-02).

**A failure the suite found.** `AWholeDayHasNoStepChanges` failed in the street with a step of 0.641 at 20:00, against a limit of 0.25. Traffic runs in plain play, so its headlamp beams moved each time the test yielded a frame, every two game hours. The test measures the city's light, so it now turns traffic off, and the street's largest step is 0.148. Reading this showed a real fault as well: the eight beams switched on at once at about 17:42. They now come up with the lamps. The lamp ramp has a test (`LampsComeUpOverTheDuskAndGoOverTheDawn`); the beams following it has none and was not checked in play at dusk.

## How to re-run

- Fast mirror of the tests: `Tools/traffic/run.sh`.
- Mutations: `python3 -I Docs/PRODUCTION/F03/mut_f03.py.txt <out dir>`.
- Probes: [F03/](F03/), `*.cs.txt`, built against the harness output.
