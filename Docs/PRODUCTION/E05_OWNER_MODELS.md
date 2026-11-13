# E-05: vehicles from the owner's models

The owner's order, 2026-11-06:

> npc, vehicles, weapons will be designed from my provided 3d models, remove your own genarated npc, vehicles. use mine

And, relayed by the session that rigged the people:

> I added few npc more and rig all of them, and use newly added npc mostly in car, rickshaw, bus, truck driving. and one of them it is rider, so use him in bike

## How it works

There is no glTF package in the project and no Blender on this machine, so `Tools/models/bake.py` reads FBX and GLB with the Python standard library (and `sips` for pictures), straight out of the zips in `~/Downloads/3D assets`. It sorts each model's surfaces into what the vehicle shader knows, takes colours from the model's own pictures, sets it on the road at its real size, cuts it to budget and writes `Assets/_Project/Resources/Vehicles/<Kind>.bytes`. `Runtime/Traffic/VehicleArt.cs` reads them. Nothing is generated in code any more.

    python3 -I Tools/models/bake.py [kinds...]      # everything in about 2.5 minutes

## The families

| Family | Model | Near triangles | Driver |
|---|---|---|---|
| Sedan | Corolla sedan (`lowpoly-sedan-wagon`) | 2,510 | polo |
| Microbus | Corolla wagon (same file) | 2,704 | hoodie |
| Cng | `cng` | 8,296 | shalwar |
| Rickshaw | `rickshaw` | 8,500 | polo |
| Truck | `man-tga-dump-truck` | 7,542 | hoodie |
| Coach | `japanese-bus-nagoya-city-bus-aichi` | 1,558 | none: its windows are painted on |
| Bus | `bus` | 4,554 | polo |
| Motorcycle | `mv-agusta-brutale-1000-serie-oro-2019` | 8,906 | racer |
| Driven/Sedan | `volkswagen-jetta-gli-2025` | 97,614 with its parts | hoodie |

Marks are taken off where they would read: the VW and GLI badges on the Jetta are removed. The traffic budget is 40,000, CNG and motorcycle 24,000, bus and truck 60,000, the driven car 120,000.

## The people in the vehicles

`Tools/models/fbxread.py` reads each rigged model's real skeleton and skin weights. `seated()` in `bake.py` turns the model's limbs to the seat, hands to the controls and feet to the pedals by two-bone reach, moves the mesh by the model's own weights, then cuts it to budget. So a new person needs one line in `PEOPLE` and no hand work. Drivers are posed meshes, not animated, and every vehicle of a family has the same driver.

## Limits

- The Coach has no driver. Its windows are in its picture, not its mesh.
- Closed vehicles are hollow behind their glass, apart from the driver.
- The CNG has no front indicators; the rickshaw has no lamps; the bus has no front indicators.
- The CNG's rear lamps are split by a rule, and the driven car's rear indicators are a rule on its outer red lens.
- The Coach is one livery of a 10.9 m city bus. The Microbus is really a wagon. The Bus is 4.39 m tall and its clearance under low structures has not been checked.
- The rickshaw's back-panel art is lost: its picture is not in the archive.
