# Vehicle model spec

What an authored vehicle model must be so it drops into `PlayerVehicle` part by part. The game's own stand-ins are generated in code (`VehicleFamilies`); the brief asks for high-detail models authored in Blender or similar, and these are the terms for them.

All designs must be original: no maker's body shape, badge or lettering, and nothing taken from another game. Number plates and destination boards stay blank until the writing work (D-09).

## Format

- **FBX**, one file per vehicle. Unity imports FBX with no extra package; importing glTF or GLB needs a package download, which needs the owner's approval first.
- Metres, y up, z forward, origin on the road under the middle of the wheelbase, at the height the car sits at rest with its driver in it.
- Right-hand drive. Kerb-side doors are on the left.
- Wheels modelled at their rest position, resting on y = 0.

## Node names

Names are the contract: `PlayerVehicle` finds parts by them.

| Node | Holds | Pivot |
|---|---|---|
| `Chassis` | Everything that moves with the body | Origin |
| `Exterior` | Body panels, bumpers, lamps, trim, undercarriage | |
| `Windows` | Glass, in its own material | |
| `Interior` | Dash, seats, door cards, lining, floor | |
| `SteeringWheel` | Rim and spokes | Centre of the hub, z along the column |
| `SpeedNeedle`, `RevNeedle`, `FuelNeedle` | One needle each | Needle's spindle, pointing up at zero rotation |
| `PedalThrottle`, `PedalBrake` | One pedal each | Hinge |
| `Doors/FrontLeft`, `FrontRight`, `RearLeft`, `RearRight` | Skin, card, glass, handle | Hinge line |
| `Lights/HeadLeft`, `HeadRight`, `Tail`, `Brake`, `Reverse`, `IndicatorLeft`, `IndicatorRight` | Lenses and reflectors | |
| `Mirrors/Left`, `Right`, `Inner` | Housing, with the glass as its own mesh | Mount |
| `Wipers/Left`, `Right` | Arm and blade | Spindle |
| `Wheels/FrontLeft`, `FrontRight`, `RearLeft`, `RearRight` | Tyre, rim, nuts, disc and caliper | Hub centre; the caliper must be a child named `Caliper`, so it steers but does not spin |
| `InteractionPoints/DriverEye`, `DriverDoor` | Empty | The driver's eye; where a person stands to get in |
| `Damage/...` | See below | |

Motorcycles, the CNG and the cycle rickshaw use the same names where a part exists.

## Polygon and texture budget

These are targets, not measurements. They will be revised once the first model is profiled.

| Level | Range | Triangles (sedan) | Notes |
|---|---|---|---|
| LOD0 exterior | To 30 m | 40,000 | Player's vehicle and the nearest few |
| Interior | Driver's view and to 8 m | 20,000 | Off beyond that |
| LOD1 | To 80 m | 12,000 | No interior, glass tinted |
| LOD2 | To 200 m | 3,000 | One mesh, wheels included |

Bus and truck may take 1.5 times these. Motorcycle and CNG should take about 60 %.

- One 2048 px atlas per vehicle for colour, normal and mask, shared across a family where the parts are shared.
- Paint is tinted in the shader, so its colour map must be neutral.
- Keep the number of materials to five or fewer: paint, glass, rubber and plastic, metal, lamps.
- The texture budget of 24 MB stands; a vehicle set above it needs an approved revision backed by profiling. Revised 2026-11-07 after profiling (owner, K-63): 48 MB for all art textures together; the world library keeps its 24 MB.

## Damage

- Morph targets on the bumpers, bonnet, doors and boot, one per corner of the body, for crumple.
- Each of these as its own node, with a broken version alongside it, hidden: mirrors, lamps, bumpers.
- A scratch and dirt mask in the atlas's mask channel.

## Characters

Real getting in and out needs a rigged driver, which does not exist yet (E-01). Any rig should be humanoid, so Unity's retargeting works, with clips for opening a door, getting in, sitting, holding the wheel, getting out and closing the door.

## Reference

Reference photographs of Dhaka's vehicles are for proportions and detail only. Nothing is traced, and no licensed or copyrighted model is to be used without the owner's written approval.
