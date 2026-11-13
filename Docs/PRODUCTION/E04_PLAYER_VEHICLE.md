# E-04: the driven vehicle

The owner pasted a brief for a heavy, detailed vehicle system on 2026-11-06, written for the Three.js build, and then answered:

> Build in Unity

So it is built here. Two parts of the brief are changed, and the owner has been told:

- **The look is original.** No game's models, art or audio are recreated. Heavy, soft-sprung handling is a genre mechanic and is kept. "Looks like the reference game" is replaced by measured targets per vehicle and the owner's own test drives.
- **Models.** The owner then ordered, on 2026-11-06: "npc, vehicles, weapons will be designed from my provided 3d models, remove your own genarated npc, vehicles. use mine". So the car you drive is the owner's Jetta model, every traffic family is one of the owner's models, and the generated vehicles and figures are gone from the code (they stay in git history). `Tools/models/bake.py` reads the models and writes `Resources/Vehicles`; see [E05_OWNER_MODELS.md](E05_OWNER_MODELS.md).

The brief says to finish the most common car first. The AXIOM audit said the CNG first; the newer brief wins, so this is the sedan.

## What was built

**Physics (`Runtime/Vehicles`, no engine dependency).** One model runs in tests (`TestBody`) and in play (Rigidbody, `AddForceAtPosition`, `Physics.Raycast`).

- Raycast suspension, three probes a wheel so a hub rolls over a kerb, asymmetric damping, bump stops and anti-roll bars.
- Tyres on normalised combined slip, with load fade and ABS.
- Automatic gearbox, clutch launch, fuel burn. An empty tank stops the engine.
- Fixed step 0.01 s (100 Hz).

**World collision (`Runtime/World/ChunkCollision.cs`, `Runtime/Core/Streaming/ChunkColliders.cs`).** One triangle list a chunk, built on the worker, cooked on a worker with `Physics.BakeMesh` and attached one a frame. It reads the data the chunk draws and changes none of it.

| Part | Source | Note |
|---|---|---|
| Ground | The chunk's base plane, y 0 | Landuse is paint, a few millimetres thick |
| Road | The road mesh as drawn, less its markings | Carriageways, decks, sidewalks, kerbs, barriers |
| Walls | Footprints raised 6 m, facing out | Buildings based above 3 m are left out, so a vehicle passes under |
| Street | `ChunkStreet.Solids` as boxes | Piers, median parts, columns, dividers, poles, lamps, trunks |

Slice mean: 35,381 triangles a chunk (walls 4,290), against 60,069 drawn for roads and buildings.

**Traffic.** `TrafficSim.Driven` is a box in the path: traffic follows it at the speed it makes along that path, which across the path is none. A pool of 24 kinematic boxes (`TrafficColliders`) gives the nearest vehicles bodies out to 45 m.

**Cabin.** The cabin, the steering wheel, the doors and the brakes are the model's own parts, baked as separate pieces with their own pivots (`.dadv` version 4). The door hinges and swings come from the model's own door animation, and the baker checks each door opens outward. The steering column's axis is read off the model. The model's cluster is a logo screen, so the baker lays ticks and needles over it; that is the only part of the cabin not from the model.

## Temporary compromises

| Compromise | Why | Ends with |
|---|---|---|
| Doors open (`SwingDoor`, shot in `e04-door-day`) but no key or walker uses them | Nobody walks yet | E-06 |
| No damage | Needs separate panels or morphs | Later vehicle work |
| Only the driven car has real doors and a cabin | Only the Jetta model has them | The owner's choice of driven vehicles |
| F8 from the free camera, not walking to a door | No protagonist or rig | E-06 entry and exit |
| Driver's hands do not turn with the wheel | The driver is a posed mesh, baked from the rigged model. The figure is hidden in the driver's view | E-06 |
| A hit stops the driven car and does not move the other | Traffic runs on lanes at 10 Hz | A near physics level |
| Traffic waits behind a parked player and does not go round | Lane changes read the lead, and the driver is not a lead | Later traffic work |
| Water is driven on as ground | The ground is one plane; water needs its own surface | A water pass |
| One grip everywhere | The collision mesh keeps no surface type | Surface types |
| Dials have ticks, no numbers, and are laid over the model's logo screen | Writing waits for D-09 | D-09 |
| Raycast suspension, not WheelCollider | One model for tests and play. Differs from UNITY_ARCHITECTURE line 84 | A note there |
| Hand-written camera, not Cinemachine | Cinemachine would be a download | Owner's choice |

## Not built

Mirrors that reflect (a second pass, not costed), wipers, damage, driving the motorcycle, CNG, bus or truck, a reverse lamp in the shader. Target hardware has never been measured.

## How to test it

F8 starts a drive from the free camera (real entry is E-06). Each row can be tried alone.

| Phase | Steps | Expected |
|---|---|---|
| Physics | Hold W from rest, then S | Pulls away, nose rises, changes up; brakes with the nose down, no wheel locks; a second S at a stop selects reverse |
| Steering | At 100 km/h, turn hard | The front wheels turn far less than at a crawl; body rolls outward |
| Kerb | Drive slowly at a 15 cm kerb | The nose lifts as the front wheel meets it, before the axle is over |
| World | Drive into a building, a pier, a pole | Each stops the car; it does not pass through |
| Traffic | Stop in a lane | Traffic stops behind you; a car a lane away does not |
| Cameras | C | Chase and driver views change; the driver figure hides in the driver's view |
| Cabin | Driver view, drive | Wheel turns with steering, speed and rev needles move, dials light with the lamps at night |
| Lamps | L, K, Q, E, brake | Head and tail lamps, hazards, indicators that cancel as the wheel comes back, brake lamps |
| Fuel | Run the tank out (the test does it) | Engine stops |

## Results

All measured 2026-11-06 on the MacBook Air M1, 1280x720, AC, lid open, on the owner's models.

| Check | Result |
|---|---|
| EditMode, all | 256 of 256 |
| PlayMode, all | 51 of 51 |
| Mutations ([E04/mut_result.txt](E04/mut_result.txt)) | 49 run, 48 caught. D8 survives: a guard no test can show changing a result. D1 survived the first run; `TheBarsHoldTheBodyUpInATurn` now catches it |
| Goldens | All 23 e04 and f03 views looked at and accepted ([E04/golden-review.txt](E04/golden-review.txt), [F03/golden-review.txt](F03/golden-review.txt)) |
| The 0.01 s step, not driving ([E04/bench.txt](E04/bench.txt)) | 8 runs a side, balanced order. Main-thread p50: street 2.43 to 2.46 ms, air 2.33 to 2.35 ms. Inside the noise |
| Driving, against traffic alone | Main-thread p50: street 3.06 to 3.36 ms, air 2.38 to 2.94 ms. GPU mean: street 8.69 to 8.79 ms, air 4.99 to 5.27 ms. Peak memory: street 888 to 980 MB, air 711 to 771 MB |
| Against the 1.5 ms vehicle budget (profiler capture, street, 2,000 frames) | `Dad.Vehicle.Step` mean 0.07 ms, p99 0.17 ms, max 0.55 ms. All physics mean 0.35 ms, p99 0.86 ms, max 3.66 ms |
| Texture memory | Computed, not profiled: the Coach's picture is one 1,024 square, at most 1.33 MB with mips. World 16.33 MB plus that is within the 24 MB budget. Vehicles otherwise use vertex colour |
| Windows build | Succeeded on macOS, Mono, 148 MB, 0 errors. **Not run on Windows.** Target hardware not measured |

Peak memory is the highest of the runs. The profiler capture has no `Dad.Streaming.Colliders` frame of 0.05 ms or more on the main thread once the street path is under way.
