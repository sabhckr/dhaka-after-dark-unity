# Credits

Owner of the project's own records and generated data: Ishak Hasan Sabbir. Models and pictures made by other people stay theirs; the owner field is not written over their work.

## Credit asked for by the author

All of these are on the game's credits screen (start screen, Credits; `Resources/Credits.txt`), and `CreditsTests` checks that they stay there.

| Asset | Author | Licence | Used as |
|---|---|---|---|
| Map data: the city's streets, buildings, railways and water | OpenStreetMap contributors | ODbL 1.0 | every chunk of the world (`StreamingAssets/World`), derived from the OpenStreetMap extract; shown as "© OpenStreetMap contributors" with openstreetmap.org/copyright |
| Rocky soil tile texture (`rocky_soil__tile_texture`) | CATholic | CC-BY-4.0 | `Resources/City/Tex/terrain_rock.png`, the rock ground layer and the terrain's large-scale variation |
| Container Ship | RM02 | CC-BY-4.0 | `vessel_container`, the two container ships in port |
| Liberty ship | AlanTinka | CC-BY-4.0 | `vessel_liberty`, the three cargo ships in the bay |
| Shipyard, Dry dock, Cargo ship, Giant vessel | Metazeon | CC-BY-4.0 | the shipyard on the east side of the bay, taken apart (2026-11-06): its deck as `vessel_yard`'s picture from above, its ships as `vessel_bulk` and `vessel_green`, one of its cranes as `port_crane` at every berth. The dock walls, fenders, bollards, wharves, ramps and the crane's boom are drawn by `Tools/models/port.py`, not part of this model |
| small flat cube of water | organ collector (jtaylor27) | CC-BY-4.0 | `water_sea.png` and `water_noise.png`, the pictures in `Dad/Water` |
| Zodiac boat | RedC130 | CC-BY-4.0 | the `Boat` vehicle, the small boats at the east port, which the player can take |
| Standing Man (https://sketchfab.com/3d-models/standing-man-8401da7cb2564fc08681836cbeff39bc) | zhuoyi0904 (https://sketchfab.com/zhuoyi0904) | CC-BY-4.0 | the player's figure until November 2026; no longer in the game |
| G17 Gen 5 Handgun + Olight - NSW Police Force (https://sketchfab.com/3d-models/g17-gen-5-handgun-olight-nsw-police-force-0200c248eba14385ad2bee8d4dc4e586) | 8sianDude | CC-BY-4.0 | `Resources/Weapons/glock`, the pistol |
| Ump40 (https://sketchfab.com/3d-models/ump40-91ac3865fa784e12b8960babadd8c6ed) | Ngkua06 | CC-BY-4.0 | `Resources/Weapons/ump.glb`, the submachine gun |
| FN Herstal Scar Light (https://sketchfab.com/3d-models/fn-herstal-scar-light-ec72c65287234d61b7a2f5a7840f7861) | Mateusz Woliński | **CC-BY-NC-4.0: not for commercial use** | `Resources/Weapons/scar.glb`, the rifle. The game cannot be sold with it. Owner, 2026-11-13: "Keep it for now" (K-94) |
| Old Residential Building [4k] (https://sketchfab.com/3d-models/old-residential-building-4k-dd400ed693cf416e940c5b8e3107205e) | Andrej Grave | CC-BY-4.0 | `Resources/Buildings/old_residential` ([../PRODUCTION/BUILDING_ASSETS/LICENSES.md](../PRODUCTION/BUILDING_ASSETS/LICENSES.md)) |
| The twenty Blender people's skins, clothes, hair, brows and lashes, the plate carrier and the fabric weaves | many (see `PEOPLE_CREDITS.md`) | CC0, CC-BY-4.0 and Sketchfab Free Standard, per asset | `Resources/People/npc_m01`-`npc_f10` and `Resources/People/Wardrobe`; every author whose asset is worn is listed there, with what was changed |

## Used without a credit file in the archive

These came in the owner's folder with no licence or author file inside, so no credit line can be copied from them. They stay their makers' work.

| Archive | Used as |
|---|---|
| `metro-train-3d-free-model.zip` | `metro_car`, the elevated metro's cars, parked at the three stations |
| the vehicle archives in Vehicles.csv without a licence file | the traffic, parked and player vehicles |
