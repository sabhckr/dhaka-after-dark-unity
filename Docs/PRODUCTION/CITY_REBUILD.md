# City rebuild (2026-11-06 to 2026-11-07)

The owner's briefs of 2026-11-06: a new 15.5 km² map built from the owner's models, the complete transport system, the metro train, ship and dockyard repair, and the port repair. Branches `wt/remap` (commits `768962d`, `7639f6a`) and `wt/waterfront`. Owner of the project records: Ishak Hasan Sabbir.

Every number on this page was produced by a tool run named next to it. What was not run is said to be not run.

## How the world is made

| Step | Command | Output |
|---|---|---|
| Models | `Tools/models/city.py` (about 19 minutes with the shipyard) | `Assets/_Project/Resources/City/models.bytes`, `Tex/*.png`, `Tools/worldgen/models.json` |
| Port parts | `Tools/models/port.py`, called by `maritime.py` inside the bake | `Tools/worldgen/port.json` (read by `harbour.py`, so bake before generating) |
| Vehicles | `Tools/models/bake.py` | `Assets/_Project/Resources/Vehicles/*.bytes`, `Empty/`, `Driven/` |
| City | `Tools/worldgen/build.sh 1971` | `Assets/StreamingAssets/World` (62 chunks, terrain, water, `transport.json`), `Docs/PRODUCTION/city-plan.svg` |
| Checks | `python3 -I Tools/worldgen/check.py`, `check_harbour.py` | pass/fail per rule, below |

The seed stays 1971 and parking and boats use their own random streams, so regenerating changes only what was changed. This was checked with file hashes before and after each regeneration (7 of 62 chunks for the port repair; only `transport.json` for the boat fix of 2026-11-07).

## What the checkers say (run 2026-11-07, after the last regeneration)

`check.py`: all passed. `check_harbour.py`: all passed.

| Rule | Result |
|---|---|
| Map area | exactly 15.5 km² (boundary shoelace 15,500,000 m²), 62 chunks, chunks fill the boundary and no more |
| Flyovers | exactly four: East, Hub, South, West; each two ramps and four slips, steepest grade under 10 %, about 5.5 m under girders |
| Metro | one elevated line, 2,865 m; three stations (Harbour Gate, Moti Chowk, Lake Road), one in the hub; a parked train at every station (12 cars); 4.0 m between flyover decks and the metro girder |
| Railway | on the ground, 4,037 m in 3 lines, 456 lengths of the owner's track; reaches the port (145 points inside the port zone); three stops in order (North Yard, Harbour Gate, Port Yard); rail heads read along the line at 0.29 to 0.50 m at 703 points; 2 level crossings |
| Airport | inside its zone, 8 aircraft from the owner's folder, a 347 m road that meets the highway, no tree on the airfield or under the approaches |
| Buildings | 14,308 placed from the owner's models; 49.9 % mirrored; 0 overlapping pairs; 0 identical neighbours of 21,074 pairs; 0 on a road, ramp or deck; 0 in the water; small buildings only in the slums |
| Trees | 2,939, all five of the owner's tree models; none on a road, in a building, on a ramp or under a deck, by the railway or on the runway line |
| Parked vehicles | 1,806 (15 boats, 33 buses, 51 choppers, 215 CNGs, 6 coaches, 97 hatchbacks, 69 microbuses, 594 motorcycles, 45 roadsters, 104 saloons, 315 sedans, 47 sports cars, 80 tippers, 135 trucks); clear of carriageways, buildings, trees and the railway; on land, boats on the water; since 2026-11-07 no boat on a quay or other paving |
| Port | 7 ships of 4 kinds at berths, 15 boats, one shipyard holding its two ships, no hull on land, no two vessels touching, every hull at its waterline (water 0.14 m), every ship parallel with its wharf (26 wharf lengths), 9 of 9 cranes on a dock and beside a ship, no road or building in a vessel |
| Terrain and water | a terrain file for every chunk and a water file for each of the 35 chunks with water; both meet across chunk borders (0 points differ) |

## Port repair (owner's message of 2026-11-06)

What was wrong, and what was done:

- **The big ship floated.** The shipyard scan is one fused mesh with its two ships standing high in the basin. `port.py` takes the bulk carrier and the green ship out of it as models of their own and sets each at its waterline: the bulk carrier 6.5 m lower, the green ship 3.0 m lower, each against its dock wall with a 2.5 m fender gap.
- **Dock edges were broken.** The scan's piers are ragged at every edge. They are replaced by straight concrete walls with fenders, bollards and a yellow edge line; the deck keeps the scan's own picture, taken from above (2048 × 1024) with its holes filled from the deck beside them.
- **Ships and cranes did not line up.** Every berth now has wharf lengths (40 × 20 m, 26 in all) along the shore, the ship beside them, cranes on the wharf beside the ship, and a ramp up from the land (5 in all). One of the scan's cranes, cleaned, is the crane at every berth; its boom is drawn, as the scan's boom is a smear.
- **No cleaner models existed.** `~/Downloads/3D assets/` has no other dock, quay or crane model, so the scan's own parts were used.
- **Nothing else moved.** Regeneration changed 7 of 62 chunks, all in the port.

Verified in Unity shots `q-*`, `r-ct-*`, `r-lb-*`, `r-yd-*` (reviewed by eye, accepted as goldens on 2026-11-07).

## Metro train, ships and dockyard (the repair brief)

- **Cause of the corruption:** not the owner's files. `maritime.atlas_cluster` reduced meshes by vertex clustering, which tore thin hard-surfaced parts (the train's body and bogies, the ships' rails and masts, the cranes' lattices). Rendering each source next to its baked level showed it.
- **Repair:** `Tools/models/reduce.py`, edge-collapse reduction (`loose=True` only for scans and far levels), then `reduce.normals`, since the blend sources carry no normals. Nothing is clustered at near levels any more.
- **Train:** the owner's train and the owner's track; the metro is single track, with trains parked at its three stations (owner: "parked at stations"). `TrainRun` brakes on a profile; its jerk test passes since 2026-11-07.
- **Ships and boats:** 7 ships of 4 kinds and 15 boats, all from the owner's folder. The boats are vehicles the player can take.

## Transport (the integration brief)

- **Families from the owner's models:** sedan (the owner's Jetta when driven), hatchback, microbus, saloon, roadster, sports car, motorcycle, chopper, police bike, bus, coach, truck, tipper, CNG, rickshaw, and the boat. My own generated vehicles were removed.
- **Twenty base colours:** `VehiclePaint.Count = 20` in `Runtime/Traffic/VehiclePaint.cs`, each family with its own list.
- **Drivable:** the 13 land families in `TransportPlayTests` (`Tools/run-tests.sh PlayMode`) each stand on their wheels, pull away, steer, stop and reverse, and you get on and off each through the same Player; the boat floats at its draught and answers the helm. The CNG and rickshaw are traffic and parked only.
- **Traffic:** the lane simulation of F-01 to F-03 with the owner's models; level crossings close for the train.

### Vehicle physics found and fixed on 2026-11-07

All found by tests or by looking at Unity shots, then measured with logs in the real physics engine:

| Fault | Cause | Fix |
|---|---|---|
| Every vehicle had its centre of mass on the road (the sedan sat 1.5° nose-up, 46 % of its weight on the front against 56 % specified) | the vehicle is built while its object is inactive, and an inactive body reads back a centre of mass of zero | the mass is set from the stored value whenever the body comes into the scene (`PlayerVehicle.Weigh`) |
| A motorcycle braking from a slide drove off backwards at 90 km/h | "stopped" read forward speed only, so a bike sliding sideways counted as stopped and the brake selected reverse | stopped means slow over the ground in any direction; ABS uses the same speed |
| Roadster and sports car reached 40 and 44 km/h in 8 s, slower than the sedan | wheelspin: rear drive, high torque, no traction control | traction control (`HandlingSpec.Tcs`) for both and the bikes; now 121 and 134 km/h |
| Bikes scrubbed speed and slid in turns once their centre of mass was real | their box collider was handlebar-wide all the way down and scraped at 44° of lean; the balance aimed at the lean the bars asked for, not the turn the bike was making, and ignored the height of the mass | bike box 0.45 m wide (clears to 56°); balance about the tyres' line, leaning for speed × yaw rate, at most what 80 % of the grip holds |

Drive results after the fixes, from the PlayMode log: sedan 81 km/h in 8 s, stops from 92 km/h in 38 m; motorcycle 128 km/h, 42° of lean in a held turn without sliding; bus 30 km/h in 8 s. Cars now roll 5 to 8° in the test's turn (2° before, with the mass on the road).

## The player in the city

Plain play starts the owner's standing_man beside the Jetta in a parking space. He walks, gets in, drives and gets out. On this map he gets out 0.3 m from the corner of an apartment block; the test now walks whichever way is open, and its pictures now draw the city, the parked vehicles and the trains (they had shown an empty plaza).

## Graphics (brief section 10, locked)

Checked in the project files on 2026-11-07: URP; soft shadows on every level; SMAA on every level (its quality steps down: `GraphicsTier.Apply`); linear fog (Boot scene); screen-space ambient occlusion active in the Low, Medium and High renderers; full-resolution textures on every level (`globalTextureMipmapLimit: 0`).

## Screenshots

Since 2026-11-07 the shot tool redraws parked vehicles and trains before its render, as it already did the city: they had been missing from every shot. It also holds the water's ripples still (`_DadWaterFrozen` in `DadWater.shader`, set by `ViewShot`): two shots of the same build differed by up to 12 % of their pixels on the water, against the 0.5 % the comparison allows, so a golden with water would have failed at random. Four water views shot twice since then differ by a mean of 0.45 to 0.48 and at most 0.002 % of pixels.

All 138 views were shot in the macOS player from one build and reviewed by eye on 2026-11-07, then accepted as goldens:

- 83 new views (parked families, trains at their stops, the port, the station platforms). Five were re-aimed after review because they showed something else: `p-boat`, `r-box-close` and `r-tr-wheels` after the boat fix, `w-m-boats` (into a container stack) and `w-t-side1` (into a tower; now along the line at the station).
- 55 older views: 45 still matched their goldens; 10 changed, each by what this work added (the repaired port's ships and cranes in `w-quay`, `w-air-port`, `w-air-port2`, `w-n-portedge`, `w-street-port`; a parked truck in `w-airport-road`; the parked metro train in `w-station-hub`; water and parked vehicles in `w-n-coast`, `w-station-rich`, `w-n-tree-port`).

## Tests (2026-11-07, final code)

- EditMode: 290 passed, 0 failed, 3 inconclusive of 293 (the 3 read the old map's Line 6 and Old Dhaka; K-65).
- PlayMode: 103 of 104. The failure was `WalkAcrossTheSliceKeepsRingsAndBudget`, one 8.48 ms apply frame against 4 ms in the editor (K-19, intermittent); it passed in the run before and 3 times alone after.
- Found by the full suite and fixed: plain play found no input actions after the input test fixtures had run (`GameRoot` now falls back on the actions it holds from start-up).
- 138 goldens reviewed and accepted (above). World checkers pass.

## Performance (2026-11-07)

[CITY/bench.md](CITY/bench.md): 18 runs, three paths at Low, Medium and High, in the macOS player on the M1. Frame time p50 at the 16.67 ms vSync cap everywhere; main thread p50 2.95 to 5.01 ms; GPU mean 6.7 to 7.9 ms on Low, 7.5 to 9.2 ms on Medium, 9.4 to 14.5 ms on High. One street run on High had 109 frames over 33 ms, its repeat 1, not traced. There is no earlier bench of this map, so nothing here is a before-and-after claim.

Texture memory, profiled in the player (CITY/bench.md): project art about 36.7 MB (world library 16.3, the owner's city model pictures 17.0, terrain 2.7, Coach 0.7) without the people; render targets add 31.4 MB on Low and 59.0 MB on High. Owner decision 2026-11-07 (K-63): the art texture budget is 48 MB on the GPU, all art textures together (world library, the owner's city model pictures, terrain, vehicles, people); render targets are not counted. The world library keeps its own 24 MB limit. Enforced by `MaterialLibraryTests.ArtTexturesAreWithinTheBudget`: 43.96 MB counted on 2026-11-07 (every file, including 8 city pictures the bench did not load), about 4 MB of headroom.

## Not done, or limits

- Docks are solid, not walkable: the world's land is flat, so a raised dock is a wall.
- The crane boom is drawn, not the scan's.
- The scanned ships are rough up close.
- The metro is single track with parked trains; trains do not run between stations.
- The player has no jump or fall clips, is hidden inside closed vehicles, and is heavy in triangles.
- The owner's models as baked (K-62): 10-sided bus tyres, hollow behind glass, no coach driver.
- The road pack is used only in part.
- Three EditMode tests are inconclusive: they read old-map data (`Tests/Fixtures/OldWorld`) for Line 6 and Old Dhaka, which this map does not have.
- Texture headroom is small: 43.96 MB of 48 MB counted (K-63, resolved).
- Bench camera paths: the street camera passes through traffic it cannot avoid (K-66).
- Plain play was not checked in the built player with its window in front (the check would take over the owner's screen); it is tested in the editor.
- No Windows hardware: Windows builds are never run.
