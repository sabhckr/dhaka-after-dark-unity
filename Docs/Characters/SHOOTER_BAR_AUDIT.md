# People: function and look against the AAA shooter bar

Audit of 11 November 2026, branch `wt/npc` at dafae79. The owner asked whether the NPCs and characters work and look right, measured against current AAA military shooters, and whether free fully rigged characters exist online.

**How this was checked**
- **Code:** every people system was read, and the key claims were checked again by hand: the player is `standing_man` (OnFoot.cs:11), crowd NPCs have no collider, `Ragdoll.Fall` is called only from a test, there is no `LODGroup`, and no gun plays a sound.
- **Pictures:** fresh renders of 11 Nov, made by the four look-shot tests (4 of 4 passed). They are in `Evidence/local/{npclook,protagonists,armed,fight}`, and the 10 Nov set is kept beside them as `*-20261110`. Each picture was looked at; none was accepted automatically.
- **Textures:** sizes were read from the image headers.

Rules this audit keeps:
- **No copying.** The characters from those games are other studios' property and cannot be copied, ripped or imitated. The target is the same *quality and function*, made from legal free assets and our own code.
- **Fan copies excluded.** The search turned up fan-made copies of commercial game characters. They are listed nowhere below.

## 1. Verdict

| | Works | Only visual, or missing |
|---|---|---|
| **Player** (`standing_man`) | Walk, run and crouch; third-person camera with an over-the-shoulder aim; rifle and pistol stances; shots from the muzzle; a weapon wheel; directional death. | No face, no foot or head IK, no ragdoll, no hit zones. Reload, fire and switch have no animation. No jump clip, no strafe clips, no turn-in-place. No first-person view. Textures 1024 px at most. |
| **Protagonists** (MalePlayer, FemalePlayer) | Face, Footing, Gaze, Ragdoll and Grasp all pass their tests. | **Not used in play.** The owner has not switched them on. Their faces and hair look clearly weaker than the street NPCs' (section 3). |
| **Street NPCs** (4 CC4 people, at most 16 on screen) | Walk on the sidewalks, give way, and do standing acts. Blinks, talk babble and hair motion. Startle, cower and flee at gunfire. | No collider at all: bullets and cars pass through them. No LODs. No ragdoll. Car and crash dangers are never raised. |
| **Police** (spawned after gunfire or while wanted) | See you, aim and fire bursts; die with a directional clip; drop their gun. | One capsule for the whole body. No cover, no navmesh (they walk through walls), no hit reaction. Corpses are never removed. |
| **Gangs** | — | Spawned only by tests. They never appear in plain play. |

**Short answer:** the people walk and look like people, and the basic gunfight runs. They are **not** at the shooter bar in function or in look. The gap is mostly code (hit zones, animation layers, ragdoll in play, AI) rather than models.

The full capability table is in section 5.

## 2. What the shooter bar means here

These are the features that bar is made of, and where we stand.

| Bar feature | Status |
|---|---|
| Per-bone hit zones; head and limb damage differ | **Done 12 Nov** (step 1): 11 parts each; head ×3, chest ×1, arms and legs ×0.75 |
| Ragdoll on death, blended with hit reactions | **Ragdoll on death done 12 Nov** (step 1); hit reactions still missing |
| Upper and lower body animated separately (aim, fire and reload while running) | **Done 12 Nov** (step 2): a waist-up layer |
| 8-way strafe, turn-in-place, jump and land, crossfaded transitions | **Partly done 12 Nov** (step 2): crossfades, turn-in-place steps, jump and land, walking backwards. Side-stepping needs strafe clips (section 8) |
| Spine and head follow the aim; pistol aim | **Done 12 Nov** (step 2): the spine follows the aim; the pistol is aimed |
| Fire, recoil, reload, draw and holster animations | **Partly done 12 Nov** (step 2): gun recoil and a reload clip. No draw or holster |
| First-person arms with a sight picture | Missing (third-person only) |
| Foot IK on slopes and stairs; hand IK on the gun | Code exists on the protagonists only |
| Gun audio, impacts, blood, decals | Muzzle flash only; shots are silent |
| AI: cover, flanking, navmesh | Missing |
| LODs and animation-rate LOD for crowds | Missing |
| Realistic hair, skin and cloth shading | Partial (section 3) |

## 3. Look: what the 11 Nov pictures show

**Street NPCs (CC4)** are the strongest part.
- Faces have real skin detail, wrinkles and teeth.
- Textures are 2048 to 4096 px: alina 31 maps at 2K or more, balthazar 20, bouncer 32, walker 14.

Their problems:
- **Hair:** the hair cards render as jagged grey strands with a shiny streak. This is clearest on walker and alina, who look unkempt instead of soft.
- **Clothes:** the shirts look wet and plastic, especially bouncer's and walker's. Their smoothness is too high for cloth.
- **Clothing gaps:** walker is barefoot in shorts and a T-shirt, and fights barefoot in the gun scenes.
- **Rifle hold:** in the front view, bouncer's rifle is held at the chest and tilted, not set into the shoulder. Pistol holds are acceptable.

**Protagonists** look clearly weaker than the street NPCs.
- **Hair:** both heads wear a solid shell of hair that reads like a helmet.
- **Faces:** the shading is flat and the skin has little detail. Textures are 2048 px at most.
- **Rifle:** the head sinks into the rifle when aiming.
- **Ankles:** MalePlayer shows a light gap at the ankle in the side walk.

**The player model in play** (`standing_man`) has 1024 px textures at most, the lowest of all.

**Fight pictures:**
- The scene is an empty white test room, so they show staging, not the look in the city.
- The police arrive far from the camera, and the falls are too small to judge.
- The scene's tally reads: gang fired 29 shots and struck 9; police fired 10 and struck 1; both police died.

## 4. Free rigged characters found online

Searched on 11 November 2026. Nothing has been downloaded. Licences are the uploader's claim, read from the Sketchfab API.

> **Note on "full-body IK".** Full-body IK is not part of a model file. It comes from the engine (Unity Animation Rigging, plus our Footing, Gaze, Grasp and Armed code), and it works on any model with a full humanoid skeleton. What a downloaded model must bring is a clean humanoid rig, ideally with finger, eye and jaw bones and face shapes. A Sketchfab "rigged" label often means a body skeleton only, with no face.

### Already in the project

Reallusion's free CC4 characters (bouncer, balthazar, walker, alina). These are the best rigs we have: 55 humanoid bones, jaw, eyes and face shapes, and 4K maps.

### Candidates for police, soldiers and special units (CC BY 4.0 unless stated)

| Model | Author | Faces | Note |
|---|---|---|---|
| S.W.A.T. Operator | jeandiz | 106,714 | Most liked rigged tactical figure on the site |
| S.W.A.T. Operator, 4k Remaster | jeandiz | 71,818 | Also offered under Free Standard |
| FSB Operator | jeandiz | 114,273 | Masked special-forces look |
| Polish soldier | buh-late | 123,061 | Modern uniform |
| African soldier with Desert Eagle | jeandiz | 121,248 | |
| Military tactical suit (LowPolyGameReady) | 1799danly | 31,771 | Game-ready |
| female specops | bumstrum | 18,878 | |
| Police Man from the U.S.A. (Mixamo rig) | ustoopia | 28,448 | Mixamo skeleton maps to humanoid easily |
| Female Police Officer, fully rigged | AnnonymousCreator | 21,485 | |
| Traffic Cop (animated) | jungle_jim | 85,000 | Same author as balthazar's source |
| Free Armor Plate Carrier (gear for MetaHuman bodies) | amadions | 24,395 | Free Standard; gear only |

### Candidates for civilians

- **Renderpeople free rigged scans** (Eric, Carla, Claudia): about 20,000 faces each, photo-scanned, CC BY.
- **Realistic Old Russian Guy** (jungle_jim): 53,891 faces.
- **Just man** (vrimen): 44,760 faces.

### Unknowns until a file is opened

Sketchfab's public API does not give the texture sizes or the rig contents of these models. Each one must be downloaded and measured before anything is promised: bone count, finger, eye and jaw bones, face shapes, and texture size. Nothing is upscaled.

### Other sources

- **Reallusion:** besides the CC4 people we already use, it offers 5 free realistic and toon characters with full-body and face rigs, 150+ face shapes and 8K UDIM textures. Game use is allowed; resale of the assets is not. Whether a free account is needed is unknown; signing up is the owner's step.
- **MetaHuman:** since June 2025, MetaHumans may be used outside Unreal, free under US$1M revenue. Making one needs Unreal Engine 5.6 and an Epic account (owner's step). Epic's licence page could not be read without a login, so the terms are not confirmed here.

Sources:
- https://www.cgchannel.com/2025/06/you-can-now-sell-metahumans-or-use-them-in-unity-or-godot/
- https://www.cgchannel.com/2024/03/get-reallusions-five-great-free-animation-ready-3d-characters
- https://www.reallusion.com/license/content.html

## 5. Capability table (code)

Legend: WORKS = runs in plain play; PARTIAL = part of it runs; TEST-ONLY = exists, but only tests use it; MISSING = absent.

| Capability | Player | NPCs | Where |
|---|---|---|---|
| Humanoid rig | PARTIAL (52 bones, no eyes, jaw or shapes) | WORKS (55 bones, face shapes) | PeopleImport.cs:24; Protagonists.cs:23-43 |
| LODs | MISSING | MISSING | no LODGroup anywhere |
| Idle, walk, run | WORKS | WORKS | Person.cs:136-146, 241-265 |
| Strafe / 8-way | PARTIAL (feet slide) | MISSING | OnFoot.cs:128-132 |
| Turn in place | MISSING | MISSING | OnFoot.cs:131-132 |
| Crouch | WORKS | MISSING | OnFoot.cs:76-103 |
| Jump and land clips | WORKS (12 Nov) | n/a | OnFoot.Jump |
| Crossfades between actions | PARTIAL (locomotion only) | PARTIAL | Person.cs:150-160, 188-195 |
| First-person arms | MISSING | n/a | FootCamera.cs |
| Third-person camera, shoulder aim | WORKS | n/a | FootCamera.cs:53-74 |
| Weapon hold | WORKS | WORKS | Armed.cs:28-30, 256-279 |
| Aim offset | PARTIAL (rifle arms only) | PARTIAL | Armed.cs:283-298 |
| Recoil | WORKS (12 Nov) | WORKS (12 Nov) | Armed.Recoil; Arsenal.cs |
| Reload, fire, draw animations | MISSING (timers only) | MISSING | Arsenal.cs:75-141 |
| Foot IK | TEST-ONLY | MISSING | Footing.cs |
| Head look | TEST-ONLY | PARTIAL (crowd looks at the camera) | Gaze.cs; Person.cs:273-285 |
| Finger grasp | TEST-ONLY | PARTIAL (fixed curl) | Grasp.cs; Armed.cs:241-249 |
| Hit zones, headshots | WORKS (12 Nov) | WORKS (12 Nov), crowd included | Hitbox.cs; Bystander.cs |
| Hit reactions | WORKS (12 Nov, flinch) | WORKS (12 Nov, flinch) | Person.Upper |
| Death | WORKS | WORKS | Player.cs:186-194; Fighter.cs:196-202 |
| Ragdoll on death | WORKS (12 Nov) | WORKS (12 Nov) | Hitbox.Die; Fighter.Hurt, Bystander.Hurt, Player.Downed |
| Gun audio, impacts, blood | WORKS (12 Nov) | WORKS (12 Nov) | GunSound.cs; Impacts.cs |
| Ambient acts, danger reactions | n/a | WORKS | NpcCrowd.cs:53-104, 285-341 |
| Combat AI, cover, navmesh | n/a | WORKS (12 Nov) | Fighter.cs; Cover.cs; Navigation.cs; Turf.cs |
| Blinks, talk, hair motion | MISSING | WORKS | PersonLife.cs |
| Expressions, lip sync | MISSING (Face.Say has no caller) | PARTIAL (babble) | Face.cs; PersonLife.cs:80-92 |
| Cloth physics | MISSING | MISSING | |

**Test caveat:** ProtagonistRigTests, ArmedTests, FighterTests and the gang parts of PlayerInterfaceTests use bodies or spawns that plain play never creates. Their passes prove the parts work alone, not in the shipped game.

**Risk found by reading the code, not by a run:** the protagonist ragdoll colliders are on layer 0, which the gun's ray hits. Switched on as they are, the player could shoot themselves. The layer must change before the switch.

## 6. Plan, in order of gain

1. **Bodies you can hit. Done 12 Nov 2026** (section 7).
   - Bone hit zones on every person, using the Ragdoll parts.
   - Head and limb damage that differ.
   - Colliders for crowd NPCs.
   - Ragdoll on death for NPCs and the player, and corpse cleanup.
2. **Animation layers. Done 12 Nov 2026, except side-stepping** (section 8).
   - A state machine with crossfades and an 8-way strafe blend.
   - Turn-in-place, jump and land.
   - An upper-body layer for aim, fire and reload.
   - Spine and head that follow the aim; pistol aim.
   - Use the reload, fire and hit clips already imported.
3. **Feel. Done 12 Nov 2026** (section 10; recoil and hit reactions in section 8).
   - Gun audio.
   - Weapon and body recoil.
   - Impact effects, decals and blood.
   - Hit reactions.
4. **AI. Done 12 Nov 2026** (section 11).
   - A navmesh, cover points and a combat state machine.
   - Gangs in plain play.
5. **Look.**
   - A proper hair shader (alpha-to-coverage or dithered with temporal smoothing).
   - Cloth smoothness down on the CC4 clothes.
   - Shoes for walker.
   - LODs made at import, measured and reported.
   - A rifle stock that sits in the shoulder.
6. **New people** from section 4 for police and special units. Each is downloaded, measured and reported before use.
7. **Owner decisions:**
   - **Camera:** first-person, third-person, or both? (The bar named includes first-person arms.)
   - **Player model:** switch the player to MalePlayer, or bring the protagonists' face and hair up to CC4 level first?

## 7. Step 1 done: bodies you can hit (12 Nov 2026)

What changed:
- **Hit parts.** Every person on foot now has 11 hit parts on a new physics layer, Body (layer 10): the street crowd, police, gangs, and the player's standing_man. These are the Ragdoll parts: pelvis, chest, head, and each upper and lower arm and leg (`Hitbox.cs`).
  - **Damage by part:** a shot does the gun's damage times the part's share. Head ×3, chest and pelvis ×1, arms and legs ×0.75. The values are in `Hitbox.Share`, to tune.
- **What the Body layer meets.** It meets the street (World), the default layer, and other bodies. It does not meet cars, the player's capsule or the fighters' walking capsules.
  - Cars are not stopped dead by a pedestrian's limb.
  - Nobody snags on anyone.
- **Hitbox.Cast.** Shots and the crosshair ray leave out the shooter's own body. This closes the self-hit risk noted in section 5.
- **The fighters' walking capsule** is now on Ignore Raycast, so shots pass it and meet the parts inside.
- **Street NPCs can be shot** (`Bystander.cs`, 100 health).
  - A wounded person runs from where the shot came.
  - A killed one lies where they fell, and the crowd no longer moves them.
  - Shooting people on the street raises the wanted level.
- **Death.** Police, gangs, bystanders and the player go limp (ragdoll) with a push along the shot, and stay down. A body without a ragdoll still plays a death clip.
  - The player puts the gun away first.
  - A fighter's dropped gun no longer collides with its own body.
- **Bodies are cleared away** once they have been down 60 s and no camera draws them (`Remains`, and the crowd's own check). Precinct drops cleared officers from its list.

Measured in tests (`HitboxTests`, 6 of 6 passed):
- **Parts and layers:** every body has 11 parts on the Body layer, and the layer matrix is as above.
- **Shares:** each part took its share (head 30, chest 10, leg 7.5, arm 7.5, from a 10-damage shot).
- **Own body:** a ray that leaves out the shooter meets none of its parts.
- **The fall:** a killed person dropped to the street, with the hips 0.18 m up, pushed 0.92 m the way the shot went. They were still down 7 s later.
- **Fighters:** one rifle-level head shot (34 × 3) killed a fighter, who went limp, dropped the gun and was left as remains.
- **The crowd:** on a test sidewalk, the wounded ran (3 m or more further away in 3 s), and the dead were not moved.

Not done in this step:
- **Cars still pass through people.** Being hit by a car is a later step.
- **No hit reactions:** there is no flinch when wounded and alive.
- **Not tuned:** the hit share of each part and the push of a fatal shot are first values. They have not been tuned in play.

## 8. Step 2 done: animation layers (12 Nov 2026)

What changed:
- **Crossfades** (`Person.cs`, rebuilt).
  - Actions come in over 0.2 s and go over 0.25 s. One action to another crossfades too: talk to phone, startle to cower, a fighter's stances, falling to landing.
  - A swapped stance (drawing a gun, crouching) crossfades over 0.25 s, in step with the clip it replaces.
  - Three things still switch at once, because their placement is measured on the clip itself: getting up after a fall, and the car enter, exit and ride clips.
- **Waist-up layer** (`Person.Upper`). A clip plays over the spine, head and arms while the legs go on with their own.
  - **Reload:** the reload clip, timed to the gun's reload time. Meanwhile the aim lets go and the gun stays in the right hand (`Armed.Loose`).
  - **Flinch when hit:** the head-hit clip, partway and quickly. Police, gangs, bystanders and the player flinch when shot and still alive.
- **Recoil** (`Armed.Recoil`). Each shot kicks the gun up and back, and it settles over about a third of a second. The arms follow the gun through their IK.
- **Spine follows the aim** (`Armed.Lean`). The chest bends toward the target, taking 70% of the aim and at most 45°, spread over the spine bones. The arms and gun do the rest.
- **Pistol aim** (`Armed.Point`). The pistol is turned about the right shoulder onto the target, by up to 70° from the clip's hold, and both hands go with it.
- **Player movement** (`OnFoot.cs`):
  - turning on the spot steps the feet;
  - walking backwards while aiming plays the walk in reverse;
  - a jump shows the falling clip at once;
  - after more than 0.6 s in the air, the landing shows the landing clip, until 0.45 s pass or you move.

Measured in tests (`AnimationLayerTests`, 5 of 5 passed):
- **Crossfades:** from one action to another, the hand moved at most 6.0 cm in a frame (11.6 cm in all), so there was no cut.
- **Reload over walking:** while walking and reloading, the feet stayed within 0.01 cm of a twin who was not reloading, and the left hand was 23.6 cm away.
- **Recoil:** one rifle shot gave a peak recoil of 0.50, lifted the muzzle 1.7° and pushed the gun back 0.6 cm. All of it settled within a second.
- **Lean:** for a target well above, the spine leaned 17.7° up.
- **Pistol aim:** the pistol pointed 1.6° off a target up and to its right.
- **Jump and land:** a jump showed at once and ended on landing. A 4 m drop landed with the landing clip, which then ended.
- **Turning:** turning on the spot was seen and stopped when the turn ended.

Not done, and why:
- **Side-stepping (strafing) needs clips we do not have** (done since: section 9). There is no strafe-left, strafe-right or walk-backwards clip in the project, so moving sideways while aiming still slides the feet. Backwards is the walk played in reverse, which is only a stand-in.
  - **Fix:** download free Mixamo clips: strafe left and right, walk and run backwards, the same with a rifle and a pistol, and a jump. Mixamo needs the owner's Adobe sign-in, as on 6 November.
- **No draw or holster animation:** weapon switches are still instant.
- **The rifle-fire and pistol-fire clips** are not used for single shots. Recoil is made in code instead.

## 9. Side-stepping, stepping back and jumping (12 Nov 2026)

The owner signed in to Mixamo, and 16 free clips were downloaded onto the Remy character. Settings: FBX for Unity, without skin, 30 fps, In Place. All are listed in `~/Downloads/3D assets/mixamo/MIXAMO_STEPS.md`. Fifteen are in the game:
- walk and run: left, right and back;
- rifle walk: left, right and back, plus rifle run back;
- pistol walk: left, right and back;
- a standing jump and a running jump.

`pistol-run-back` was downloaded but is not used, because a pistol runs with the plain run.

What changed:
- **Four-way blend for walking and running** (`Person.Directional`, `Person.Going`). For the player, each walk and run clip becomes four clips: ahead, right, back and left.
  - They are blended by the angle between where the body faces and where it goes, so a diagonal is half of each neighbour.
  - All four are kept in step, so the feet do not jump as the angle changes.
  - Where a clip is missing, stepping back plays the clip ahead in reverse, and a side uses the clip ahead.
  - A rifle run has no side-step clip on Mixamo without a crouch, so it uses the rifle walk's.
  - Crouching, and anyone without the clips, still use one clip as before.
- **Jumps** (`OnFoot.Jump`):
  - **Standing jump:** starts late in its crouch (0.50 s in), so the body goes up when you press.
  - **Running jump:** plays from the start.
  - **Time in the air:** both clips are stretched to the jump's 0.51 s.
  - **Height:** the clip's own lift of the hips is taken off, so the body rises with the capsule and no higher. Without this, a running jump would have lifted the body about 1.6 m.
  - **Landing:** a jump lands with the clip's own landing, briefly. Going off a ledge after the jump hands over to the falling clip.

Measured in tests (`AnimationLayerTests`, 8 of 8 passed):
- **Feet go the way you go.** Facing ahead and going right, back or left, a foot swings 72–90 cm along that way and 1–6 cm across. The body stays facing its aim.
- **A diagonal blends evenly:** 0.50 ahead, 0.50 right.
- **No jump between clips.** From a diagonal to a side-step, a foot moved at most 11.8 cm in a frame. Steady side-stepping alone moves it up to 12.1 cm.
- **The gun stays on the aim** while stepping any way: a rifle at most 2.3° off, a pistol at most 2.3°.
- **Jumps match the capsule.** In a standing jump the hips rose 0 cm above where they stand on the capsule; in a running jump, 3 cm.
- **Where each clip leaves the ground:**
  - standing jump: the feet leave at 0.63 s, are highest (57 cm) at 0.83 s and are down at 1.07 s;
  - running jump: they leave at 0.03 s and are down at 0.67 s.
- **Every side-step and back-step clip** carries the feet its own way.

Pictures, looked at, not only generated: `Evidence/local/strafe/`, from the explicit `StrafeLookShots` test.
- **Rifle and pistol:** the body faces the aim in all four directions, and the legs step sideways and back.
- **Jumps:** the standing jump tucks and lands into a slight crouch; the running jump leaps and runs on.

Weak spots, measured:
- **Mixamo's side-steps all turn the body toward where they go.** The shoulders turn 40–65°, with the hips a little further.
  - Ten more side-steps were tried. The squarest pair (Walk Strafe Left/Right, −24° and +34°) steps only about half as far per cycle, so it would slide at walking speed. It was not taken; the trials are in `strafe-clips/trials/`.
  - **With a gun** this does not show: the aim code keeps the gun and chest on the target.
  - **Bare-handed**, side-stepping turns the chest toward where you go. In play this is only seen briefly, because without aiming the body turns to face where it goes.
- **The rifle side-steps and back-steps hold the body bladed** (about 40–60°), while the rifle walk ahead is square (0°). The chest therefore turns a little as the direction changes, and the gun stays on the aim.
- **The capsule rose 38 cm in a jump in the test at 30 fps.** `OnFoot` says about 0.6 m, which is right only at a high frame rate: gravity is taken off the jump speed before the first step, so a slower frame rate gives a lower jump. It was measured, not changed here. Fixed in step 3 (section 10): 58.8 cm at any frame rate.

Still not done: the draw and holster animation, cars hitting people, and the side-steps of the other people (NPCs still turn to face where they walk).

## 10. Step 3 done: feel (12 Nov 2026)

Recoil and flinching were done in step 2 (section 8). Step 3 adds what a shot sounds like and what it leaves, and fixes the jump height.

What changed:
- **Gun sounds** (`GunSound.cs`, called from `Armed.Fire`).
  - **Recordings:** real gunshots from The Free Firearm Sound Library (CC0, opengameart.org, the "Prepared SFX Library", 194 MB). Each gun has a stand-in recording:

    | In game | Recording | Close ("near") | Further off ("far") |
    |---|---|---|---|
    | scar (rifle) | AR-15 | 2 takes, 0.5 s | 2 takes, 1.8 s |
    | ump (sub-machine gun) | Carl Gustav M45 | 3 takes, 0.4 s | 3 takes, 1.4 s |
    | glock (pistol) | Walther PPQ | 3 takes, 0.4 s | 2 takes, 1.0 s |

  - **Cutting:** each take is one shot cut from the library's strings of shots with ffmpeg: it starts 10 ms before the shot, fades in over 3 ms and out over the last 120 ms. They are kept at the source's 96 kHz, 24-bit stereo, and imported uncompressed (`AudioImport.cs`), so nothing is lost.
  - **Playing:** a shot plays one near and one far take, picked at random, 4% off pitch either way, so no two shots sound alike.
    - **Your own gun:** loud, close and mostly in the head (little 3D), with no delay.
    - **Someone else's:** from where they stand. The crack is heard within 90 m, the far sound within 700 m, and both arrive as late as sound travels (343 m/s).
  - **Voices:** 32 shared sources; when all are busy the oldest is taken. The volume follows the World volume setting.
- **Impacts** (`Impacts.cs`, called from `Arsenal.Shoot` and `Fighter.Shoot` for every shot that hits something).
  - **What was hit:** a person's hit part is a body. A car or traffic is metal. Otherwise the name of the thing decides: glass or window, metal, steel, iron, car or gun, wood, plank, tree or crate. Anything else is stone.

    | Surface | Thrown up | Left behind | Sound |
    |---|---|---|---|
    | Stone | dust and chips | a chipped crater, lighter than the wall, with cracks and soot (a 12 cm mark) | stone |
    | Metal | 14 sparks, glancing off, and a little dust | a round hole with a ring of bare metal (10 cm mark) | metal |
    | Wood | splinters and dust | a splintered hole of bare wood (10 cm mark) | wood |
    | Glass | shards | a star of cracks with rings (16 cm mark) | glass |
    | Body | blood mist and drops | blood on the wall behind (within 2.5 m) and on the street below | body |

  - **Impact sounds:** Kenney's Impact Sounds (CC0, kenney.nl), 5 takes for each surface.
  - **Marks** (holes and blood) are drawn on a 160-mark pool; when it is full the oldest is reused. A mark on a car drives off with it. Marks on the street and buildings stay where they are, even when the world chunks under them come and go.
  - **Pictures:** the holes, blood and particles are drawn in code, not from files. They use a new shader, `Dad/Mark`, which lights them with the sun and sky as the street is lit, so a hole is as dark at night as its wall. Sparks light themselves.
- **Jump height fixed** (`OnFoot.cs`). Each frame now moves by the average of the fall speed before and after gravity. The jump is 58.8 cm high at any frame rate. Before, it was 38 cm at 30 fps (section 9).

Measured in tests (`FeelTests`, 5 of 5 passed):
- **The cuts:** every take is a single shot. Its loudest moment is within the first 30 ms, and its last quarter is below 15% of that peak. Every take is 96 kHz stereo; every surface has 5 impact takes.
- **Where shots are heard:** a shot 49 m away was heard 143 ms late, near and far. A shot 200 m away gave only the far sound. Your own shot had no delay.
- **Holes:** every mark lay within 1 cm of the hit, facing out of the surface. Stone threw dust and chips; a car threw sparks, and its hole moved with it. The pool held at 160. The dust had settled 2 s later.
- **Blood:** a chest shot threw mist and drops and left 2 blood marks: one on the wall behind (3.0 m back) and one on the street.
- **Jump:** 58.7 cm at 30 fps and 58.8 cm at 60 fps, against 58.8 cm worked out from the jump speed and gravity.

Whole suites after the change: PlayMode 153 passed, 9 skipped, none failed; EditMode 300 passed, 3 inconclusive (no world data), none failed. A first run had 2 failures. Gun sounds still playing after a test's listener was gone made Unity warn in the next test. Now a sound starts only when someone can hear it, and sounds stop when the listener goes.

Pictures (`ImpactLookShots`, in `Evidence/local/impacts/`), reviewed one by one:
- **First render, not accepted:** the stone, metal and wood holes were small dark dots, close to invisible on a dark wall. No sparks were seen: they started on the wall's face, and the particle collision turned them back into it. Both were fixed:
  - the holes were made larger, with lighter bare material round them;
  - what is thrown up now starts 2 cm off the face.
- **Second render:** the stone holes read as chipped craters, the wood holes as splintered, and the metal as clean punched holes. Sparks fly off as thin streaks. The glass stars and the blood on the wall were good from the first render, and the blood stays after the body falls.

Weak spots:
- **Nobody has listened to the sounds in the game yet.** What was checked: the waveforms (one shot per take, dying away), the playback (which take, how loud, how late) and the import settings. Whether they sound right in the game, and are balanced with the cars and the music, needs a listen by the owner.
- **The recordings are stand-ins.** The library has no SCAR, UMP or Glock. An AR-15, a Carl Gustav M45 and a Walther PPQ are the closest in kind.
- **Recordings only:** there are no reload, empty-click, shell-case or bullet-flyby sounds yet.
- **The blood mist is faint** in the pictures. The drops show as short streaks in flight.
- **The surface is guessed from object names.** The city's buildings are mostly "stone" by this rule. Physics materials per surface would be more exact.
- **Not seen in the city yet:** the pictures are from a test scene with plain walls, not the streets at night.

## 11. Step 4 done: how armed people fight (12 Nov 2026)

What changed:
- **Walking round things** (`Navigation.cs`).
  - The city is streamed and too big to prepare ahead. So a walking mesh is built while the game runs: a 160 m square round where the fighting is.
  - It is built on worker threads from the street's colliders (ground, roads, kerbs, building walls, street solids) and the parked cars. Moving traffic is left out.
  - It is built again when the fighting nears its edge, after the floating origin moves, and when a new fight starts after a quiet spell.
  - Until the mesh is ready, people go straight, as before.
- **Cover** (`Cover.cs`). A cover place is a spot by the walking mesh's edge where all of these hold:
  - something solid is right behind it, on the side the shots come from;
  - crouched down, the top of the head is hidden from the shooter's eyes, at the middle and 40 cm to each side (so not the very end of a wall);
  - there is a place to fire from: standing up (low cover) or a step out to the side (a high wall).
  - Places 2.5–19 m away are tried, 96 in all. The nearest by walking is taken, and two people never take the same one.
- **The way they fight** (`Fighter.cs`, rewritten as a state machine):
  - **Into cover first,** running there by the way round. The last 1.6 m is crouched, so they arrive down.
  - **From cover, in turns:**
    - down for 0.8–2 s, with the gun lowered;
    - up (or out) for 1.4–2.8 s, firing after a 0.3 s moment to stand;
    - down again;
    - hurt while up, they drop back at once.
  - **Reloading:** they reload while down when the magazine is under a third, and always when it is empty. The reload moves the arms only, so the back and head stay down.
  - **Police move up** after two turns, to cover at least 4 m nearer, until within 25 m. With no cover nearer, they walk in firing. A target out of sight for 3 s is moved up on; once lost (8 s), they go to where it was last seen.
  - **A gang member below 35 health** falls back to cover further off.
  - **With no cover,** they fight in the open, stepping 1.5–3.5 m side to side while the gun stays on the mark, and look for cover again every 1.5–2.5 s.
  - **Not seen, not shot:** a target out of sight is remembered for 8 s where it was last seen, but not fired at. A shot needs a clear line from the muzzle.
  - **Stepping every way:** fighters now walk and run with the strafe clips (`Person.Directional`), so they step sideways and back while facing the mark.
  - **Spread by movement:** walking adds 1.5° to the spread and running 6°.
  - **Magazines and reload times** are the same as yours.
- **The gang on the streets** (`Turf.cs`, on in plain play).
  - **Where:** one sidewalk line in 8 (of those at least 20 m long) is a gang's turf, picked by a hash, so the same streets always have them.
  - **When:** coming within 40–120 m of a turf puts its gang there: the three gang members, unarmed, smoking, talking and on the phone. One gang is out at a time.
  - **Calm:** they raise no alarm, and the police do not go after them.
  - **Drawing:** a shot heard within 60 m, or one of them hurt, brings their guns out. They then fight the police, and you if you have shot one of them.
  - **Afterwards:** 30 s after the last shot they walk home and put the guns away.
  - **Leaving:** they are taken away beyond 170 m, or 260 m while fighting. A turf whose gang was shot down stays empty for 5 minutes.
- **A hold-your-ground switch** (`Fighter.Stay`): fights from where they stand. The tests use it to keep the mark still.

Measured in tests (`AiTests`, 6 of 6 passed):
- **Round a wall:** the way from one side of a 14 m wall to the other went round its end in 3 corners, 13.5 m against 10 m straight. No stretch passed through the wall. On a test floor the mesh took 37 ms to build.
- **Cover taken and fired from:** a gang member in the open, shot at from beyond a 1.3 m wall 20 m off:
  - ran to it and crouched behind it (spot 0.4 m behind the wall's back face);
  - came up 3 times and fired 18 shots;
  - reloaded once, while down.
  - Fully down, the very top of the head (the highest point of the posed mesh) was hidden from the officer's eyes in 147 of 147 frames, reloading included. Standing to fire, the head rose to 1.62 m.
- **Police moving up:** an officer 42 m off, with walls every 10 m, took cover at 30.6 m, moved up to 20.5 m, and held there firing (12 shots).
- **In the open:** on a bare floor, a gang member found no cover and fought in the open for all 300 frames. They moved 10 m across, stepping up to 109° off ahead (sideways and back) while facing the mark, and fired 22 shots.
- **The gang on their turf:**
  - calm, unarmed, standing about, raising no alarm; an officer 16 m off neither went after them nor fired;
  - a shot 31 m off brought every gun out within a frame, and both sides fired;
  - with the officer gone, they put the guns away at home after the quiet (shortened to 4 s for the test).
- **In the real city (plain play):**
  - 101 turfs on the streamed chunks, 3 of them within reach of the start;
  - a gang was put out on a sidewalk, standing on the street; a shot brought their guns out;
  - the walking mesh round them took 333–353 ms to build on worker threads, from 1 collider (that chunk's), and a way was found along the street;
  - **no cover was found there** against a shooter 20 m along the street: 96 places tried, none would do (see weak spots).
- **Cost of a cover search** on the main thread: at most 2.0 ms in the test scene. In the city, ten searches took 0.21–1.13 ms (median 0.22 ms). Each fighter searches at most every 1.5 s.

Whole suites after the change: PlayMode 159 passed, 10 skipped, none failed; EditMode 300 passed, 3 inconclusive (no world data), none failed.

Pictures (`CoverLookShots`, `Evidence/local/cover/`), reviewed one by one:
- **First render, not accepted.** Behind a 1.2 m wall, the crouched rifle stance kept the head and gun at the wall's top, and from the officer's view the top of the head showed. The test had passed because it checked the head bone, which is lower than the crown.
  - **Measured top of the head:** `crouch-aim` 1.22–1.26 m; `crouch-idle` 1.07–1.13 m; `cover-idle` is a standing pose (1.63–1.75 m).
  - **The fix:** hiding now uses `crouch-idle` with the gun lowered, and cover is judged to the crown (1.15 m). So a 1.2 m wall is now too low to count as cover, and the tests use 1.3 m walls (a car is about 1.4 m).
- **Second render, not accepted.** The reload clip is a standing one played over the waist up, so a crouched fighter straightened and the head rose over the wall. The test had stopped watching as the reload began. Now the reload moves the arms only (`Person.Upper(arms: true)`), and the test watches every reload to its end.
- **Third render:**
  - down: low behind the wall, nothing showing to the officer;
  - up: standing, the rifle over the wall, firing;
  - reloading: down, the hands working at the gun, nothing showing.
  - The turf gang stand about, then hold their guns ready at a shot, in a test scene and on a real sidewalk (`city_turf_*.jpg`).
  - A small dark streak in one officer's-view picture is a blood drop in flight from the officer being hit: there is blood on the ground in the same picture, and it is gone a moment later.

Weak spots:
- **Ducking takes about half a second.** While going down from standing, the head shows over a 1.3 m wall for a few frames. Arriving at cover, they now crouch for the last steps, so they arrive down.
- **No suppression:** shots striking near someone do not make them duck, only being hit does.
- **No grenades, no flanking on purpose, no team orders.** Police move up one by one, not in pairs covering each other.
- **The cover check is by lines to the head,** so a wall with a gap can be taken as cover. Low cover must be at least about 1.2 m above the street under the spot, from the sidewalk or road; cars count, kerbs and bollards do not.
- **The hiding pose holds the gun loosely** (an unarmed crouch clip with the gun in the hands). The arms are not posed for the gun while down.
- **Few gang people:** there are 3 gang members in the roster, so one gang is out at a time and every gang looks the same. The woman (walker) is still barefoot (step 5).
- **Cover is scarce in the city as built.** The street's colliders are the ground, roads, kerbs and 6 m building walls. There are few solids, and parked cars appear only where the yard puts them. At the one turf tried, nothing gave cover from a shooter along the street, so a fight there is mostly in the open, stepping side to side. Street furniture with colliders (bins, stalls, walls, rickshaws) would give them cover; that is world work, not AI.
- **Not tried at length in the city:** the city test checks that the gang appear calm, draw at a shot, and that the mesh builds round them. A whole street fight there, with the police and you, has not been run, and its frame time has not been measured (only the cover search and the mesh build).
