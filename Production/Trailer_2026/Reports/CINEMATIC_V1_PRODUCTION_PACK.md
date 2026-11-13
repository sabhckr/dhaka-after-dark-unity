# DHAKA: AFTER DARK, 90-second cinematic v1: production pack

Isolated in `Assets/Trailer` and `Production/Trailer_2026`. The game's own scenes, shaders and code are untouched. In-engine cinematic, labelled as not gameplay.

Render with the PlayMode test `Dad.Trailer.TrailerRender.Frames` (`DAD_TRAILER_ACT=all`), then `Tools/trailer/cinematic_assemble.swift`. Sound comes from `Tools/trailer/cinematic_audio.mjs`.

## 1. Asset audit

| Class | Assets |
|---|---|
| READY | Warehouse, penthouse, garage, lift, diner booth; Fastback (midnight blue on a trailer copy only); SUV; Glock with its own magazines; SCAR; UMP; knife; decanter; ledger; papers; flash drive; crates; conveyor; 21 movement, gun and sit clips; fall and hit clips |
| ADAPTABLE | Elara (rigged, jaw and eye bones, no eyelids); Marcus (no mouth rig); Leo, roster `survival` (no mouth rig); Vance, RenderPeople scan Eric (full face rig); table used as the bar; rain and wet street approximated |
| MISSING | Recorded voices; facial capture; food props; photo front; mezzanine; tackle, choke, knife-slash, decanter-strike, page-turn and car-entry clips |

Vance is a real scan from the free RenderPeople rigged pack (8192 px colour, normal and gloss). No faces were generated.

## 2 and 3. Storyboard and exact timeline

2700 frames at 30 fps. Each row is one shot, a storyboard panel and a timeline entry.

| # | Shot | In (s) | Out (s) | Frames | Picture | Camera |
|---|---|---|---|---|---|---|
| 1 | A0-black | 0.00 | 2.00 | 60 | Black, "in-engine cinematic" label | — |
| 2 | A1-eyes | 2.00 | 4.50 | 75 | Elara's eyes, extreme close-up | 85 mm, slow push |
| 3 | A2-envelope | 4.50 | 6.50 | 60 | Papers on the diner table, her gloved hands | Tabletop |
| 4 | A3-hands | 6.50 | 8.50 | 60 | Her hands, a tremor | Tabletop, tighter |
| 5 | A4-slides-in | 8.50 | 11.00 | 75 | Marcus arrives, slides the photo | Wide on the booth |
| 6 | A5-marcus | 11.00 | 13.40 | 72 | Marcus close-up: "sloppy" | 85 mm |
| 7 | A6-photo | 13.40 | 15.40 | 60 | The photo crosses the table | Tabletop |
| 8 | A7-reaction | 15.40 | 19.00 | 108 | Elara takes it in | 85 mm, slow push |
| 9 | A8-drive | 19.00 | 21.40 | 72 | Flash drive slides over | Tabletop, racked focus |
| 10 | A9-stands | 21.40 | 23.50 | 63 | Marcus stands to go | Booth, low |
| 11 | A10-takes-it | 23.50 | 25.00 | 45 | Her hand takes the drive | Tabletop |
| 12 | B1-street | 25.00 | 27.50 | 75 | Night street, rain, Fastback at the kerb | Wide locked |
| 13 | B2-watching | 27.50 | 30.00 | 75 | Leo watching from the car, through the side window | Long lens through glass |
| 14 | B3-into-rain | 30.00 | 32.50 | 75 | Elara walks into the rain | Behind, walking |
| 15 | B4-follows | 32.50 | 34.50 | 60 | Leo follows on foot | Behind, over the shoulder |
| 16 | W1-wide | 34.50 | 36.50 | 60 | Warehouse, Elara aims into the dark | Wide |
| 17 | W2-stay-back | 36.50 | 38.50 | 60 | "Stay back!" | 50 mm, handheld |
| 18 | W3-leo-enters | 38.50 | 41.00 | 75 | Leo walks in with his hands low | Over her shoulder |
| 19 | W4-ots | 41.00 | 43.50 | 75 | "He's investing in her death." | Over his shoulder |
| 20 | W5-heart | 43.50 | 47.00 | 105 | Elara, the music drops out | 85 mm hold |
| 21 | W6-headlights | 47.00 | 49.00 | 60 | Headlights sweep the room | Wide |
| 22 | W7-suv | 49.00 | 51.00 | 60 | SUV outside | Low, push |
| 23 | W8-not-my-ride | 51.00 | 53.00 | 60 | Leo: "That's not my ride." | 85 mm |
| 24 | W9-rifle | 53.00 | 54.50 | 45 | Elara takes the rifle from the crate | 50 mm handheld |
| 25 | F2-flash | 54.50 | 55.70 | 36 | Flashbang, gunmen silhouetted | Wide |
| 26 | F3-gunmen | 55.70 | 56.90 | 36 | Gunmen advance through smoke | Low |
| 27 | F4-leo-fires | 56.90 | 58.10 | 36 | Leo fires two shots, recoil | Profile |
| 28 | F5-falls | 58.10 | 59.20 | 33 | First gunman falls | Low |
| 29 | F6-elara-high | 59.20 | 60.05 | 26 | Elara fires from the crates | Low, handheld |
| 30 | F7-second | 60.05 | 61.00 | 28 | Second gunman falls | High |
| 31 | F8-vance | 61.00 | 62.00 | 30 | Vance advances | Low |
| 32 | F9-smg | 62.00 | 63.00 | 30 | Vance fires the SMG | Three-quarter, shake |
| 33 | F10-return | 63.00 | 64.00 | 30 | Leo returns fire | Profile, shake |
| 34 | V1-where | 64.00 | 67.00 | 90 | Knife to Vance, "Where is Marcus?" | Two-shot, 50 mm |
| 35 | V2-heights | 67.00 | 70.00 | 90 | Vance: "The Heights..." | Over her shoulder, 35 mm |
| 36 | P1-lift | 70.00 | 71.90 | 57 | Lift, Leo changes the magazine | Wide lens, inside the car |
| 37 | P1b-doors | 71.90 | 72.70 | 24 | The doors slide open on Leo and Elara | From outside the doors, slow push |
| 38 | P2-marcus-waits | 72.70 | 75.00 | 69 | Marcus waits at the table | Long, slow slide |
| 39 | P3-leo-first | 75.00 | 77.00 | 60 | Leo comes in first | Over Marcus |
| 40 | P4-lunge | 77.00 | 77.40 | 12 | Elara lunges | Fast, side |
| 41 | P5-glass | 77.40 | 78.20 | 24 | The blow, glass and scotch | Side, shake |
| 42 | P6-page-forty-two | 78.20 | 79.40 | 36 | "Page... forty-two..." | 85 mm |
| 43 | P7-ledger | 79.40 | 80.40 | 30 | The ledger page | Top-down insert |
| 44 | P8-parasite | 80.40 | 83.20 | 84 | Elara: "You made me a killer." | 85 mm, low |
| 45 | P9-decanter | 83.20 | 83.50 | 9 | The decanter comes down | Side |
| 46 | P10-black | 83.50 | 84.00 | 15 | Black, the score cuts | — |
| 47 | P11-after | 84.00 | 85.00 | 30 | After: Elara and Leo, the ledger | Wide |
| 48 | G1-car | 85.00 | 86.40 | 42 | Garage, they cross to the car | Wide |
| 49 | G2-door | 86.40 | 87.20 | 24 | At the driver's door: "Get in." | 85 mm |
| 50 | G3-start | 87.20 | 88.00 | 24 | Headlights come on, engine | Front three-quarter, low |
| 51 | Title | 88.00 | 90.00 | 60 | DHAKA: AFTER DARK / A CITY THAT NEVER SLEEPS. | Drawn at assembly |

## 4. Character and animation mapping

| Character | Model | Face | Body |
|---|---|---|---|
| Elara | `elara/olgapose1.fbx` | Jaw bone, eye bones | sit-idle, pistol-fire, idle, walk, run, cross-punch, crouch-idle, plus arm solves |
| Leo | `survival` | Trailer-shader jaw, no bones | drive, walk, pistol-idle, pistol-fire, built reload |
| Marcus | `marcus/Businessman.glb` | Trailer-shader jaw, eye nodes | sit-idle, idle, head-hit |
| Vance | `eric/rp_eric_rigged_001_yup_t.fbx` | Jaw, eyelids, brows, mouth corners, eyes | rifle-walk, rifle-fire, idle |
| Gunmen | `contractor` | None | rifle-walk, death-back, death-chest |

Hands: every gun and the knife are held by a per-weapon grip map taken from the owner's reference photos (`GripMap` in `Assets/Trailer/Code/Holds.cs`). There is no transition animation: the hold is applied directly once a character points the gun. Wrist, knuckles, each finger joint, trigger finger and thumbs are set against the weapon's measured shape. Props are at real size: Glock 21.3 cm, UMP 69 cm, SCAR 96 cm, knife 27 cm. Recoil is added per shot on the arms, hands and chest, timed to the flash. The reload uses the pistol's own magazines: spent one falls, fresh one comes from the belt, seats, slide is racked. Measured accuracy is in `CINEMATIC_V1_QA_REPORT.md`.

## 5. Camera

Per-shot moves are in the table above; code in `Assets/Trailer/Code/Act1.cs`, `Act2.cs`, `Act3.cs`. Close-ups use 85 mm at f/9, so the face stays sharp.

## 6. Lighting and grade

Diner: green-grey practical, warm table key. Street: cold wash, rim light. Warehouse: burning back wall, one hanging lamp, per-shot key. Penthouse: warm interior, cold window fill. Garage: one strip lamp. ACES tonemapping, restrained saturation, light film grain. Skin uses a wrap-diffuse scatter in `Assets/Trailer/Shaders/TrailerPerson.shader`, a copy; the game's `DadPerson` shader is unchanged.

## 7. Sound and music

`Tools/trailer/cinematic_audio.mjs` synthesises the score and effects. Master: `Production/Trailer_2026/Audio/cinematic_v1/DAD_Cinematic_v1_master_48k24.wav`, measured -16.02 LUFS, -1.50 dBTP. Score cuts on the decanter at 83.5 s; the engine carries the end.

## 8. English dialogue

22 lines, screenplay wording, as subtitles: `Production/Trailer_2026/Editing/cinematic_v1_subtitles.json`. No voices exist. Recording plan: `cinematic_v1_voice_recording_sheet.md`. The script stops at `LEO (`, so no last line was invented.

## 12. Limitations

- No voices, so mouths follow the written lines. It is not lip sync and cannot show a real performance.
- Marcus and Leo have no mouth rig. Their mouths are moved by a shader deformation made for this trailer, which reads less well than a real rig.
- Only Vance has eyelids and brows. Elara's expression is limited to jaw and gaze; Marcus and Leo to gaze and jaw.
- Elara's face is a low-resolution part of her texture and her hair is one solid shell. This is the weakest character asset.
- No hug: neither the director brief nor the screenplay has an embrace, so none was staged.
- Finger contact is solved from the weapon's shape approximated by boxes, not motion capture. See the QA report for the holds that remain out of tolerance.
- No tackle, choke or slash clips: those beats are told by cutting. Elara never visibly gets into the car.
- The ledger page is a card with ruled lines and "42". The page does not turn.
- The envelope is plain white; the flash drive is a green circuit stick; the photo is seen only from the back.
- The lift is a separate set from the penthouse, and its outer shell is hidden so the door shot can look in from outside.
- Glass particles are small lit points, not simulated shards.
- Only Avenir Next was installed for type.
- Windows hardware not tested; this render is on the Mac only.
