# NPC audit (2026-11-08, wt/npc)

**Superseded, 2026-11-09.** Every model below has been removed from the game at the owner's word, with its source files, to make way for new ones: the roster is empty. This page stays as the record of what was tried, and its verdicts should not be read as verdicts on the owner's originals. The models were judged after the import had damaged them: textures of 2048 and 4096 were shrunk to 512 (eyes and small parts to 128), and meshes were reduced (`polo` from 382,516 triangles to 15,000, `suit` from 85,499 to about 14,000, `casual` from 26,714, `traveler` from 273,896, `npcman` from 1,978,098). The re-audit at source quality was stopped before any render, so no model was ever judged at full quality. The importer no longer shrinks anything.

Every character on the street roster was inspected on face, body, clothing, textures, hair, animation and rigging, and overall presentation, from rendered views rather than from file names. The rule applied was the owner's: one noticeable defect is enough to keep a character out, and quantity does not lower the standard. The renders are in [NPC_AUDIT/](NPC_AUDIT/): `faces-1.png`, `faces-2.png`, `bodies-1.png` to `bodies-3.png`.

The result is hard on the game: one model passed. The streets now hold one person.

## 1. Totals

| | Models | Roster identities at the audit |
|---|---|---|
| Inspected | 10 | 14 walking, 2 driver labels |
| A, approved | 1 | 1 |
| B, needs rework (not in the population) | 3 | 3 |
| C, rejected | 6 | 10 |

`standing_man` is the player. It is not part of the population, was not audited and was not touched.

## 2. Approved

| Model | Asset | Why it passes | Limits |
|---|---|---|---|
| `guard` (NPC_012) | `Assets/_Project/Resources/People/guard.fbx`, pictures in `People/guard/` | Real eyes, good skin, sound proportions, a proper uniform, a real rig (Mixamo skeleton, 65 bones), 13,259 triangles. All 12 clips play | One face only. The uniform is generic, not specifically Bangladeshi |

Approved male: `guard`. Approved female: none; every model that reached the audit is male. Approved drivers: none (section 6).

## 3. Needs rework

Moved out of the game. Kept in `Archive/NPC/rework/`.

| Model | Asset | Defects |
|---|---|---|
| `shalwar` | `Archive/NPC/rework/shalwar.fbx` | The only truly Dhaka outfit, which makes it the most valuable one to repair. Eyelid and lash cards render as opaque yellow patches; plastic-looking hair |
| `suit` | `Archive/NPC/rework/suit.fbx` | Blocky hair cards, stippling on the white collar, blue eyes, arms flared and too long after mesh reduction and auto-rigging |
| `gangster` | `Archive/NPC/rework/gangster.fbx` | The face is wholly hidden by a mask and sunglasses, fingerless gloves, low-resolution picture. Usable only for a story role, never as a passer-by |

## 4. Rejected

Moved out of the game. Kept in `Archive/NPC/rejected/`.

| Model | Asset | Identities | Reasons |
|---|---|---|---|
| `polo` | `Archive/NPC/rejected/polo.fbx` | NPC_001, 006, 007, 008 | Smeared scan face with no real eyes or mouth; a plus-shaped logo on the shirt |
| `hoodie` | `Archive/NPC/rejected/hoodie.fbx` | NPC_002, 009 | A real university name printed on the hoodie and its initial on the cap (trademarks); patchy, torn face |
| `straw` | `Archive/NPC/rejected/straw.fbx` | 1 | Blurred face; a hand-to-chin pose baked into the mesh; the rig has no moving arms |
| `casual` | `Archive/NPC/rejected/casual.fbx` | 1 | Hollow dark eye sockets, bald, bulging shoulders |
| `traveler` | `Archive/NPC/rejected/traveler.fbx` | 1 | Mangled picture, head bowed into the mesh, winter gear that is wrong for Dhaka |
| `npcman` | `Archive/NPC/rejected/npcman.fbx` | 1 | Torn face and hair, holes in the chest, winter military gear |

## 5. Duplicates and minor variations

- `polo`: NPC_006, 007 and 008 were recolours of NPC_001, so one body and face four times.
- `hoodie`: NPC_009 was a recolour of NPC_002.
- `security-guard-rigged-3d-model (1).zip` in the owner's asset folder is identical to `security-guard-rigged-3d-model.zip`.
- `rugged-winter-traveler.zip` and `rugged_winter_traveler.zip` are two downloads of the traveler.

Not imported, so not part of the counts: `human.zip` (a 2 million triangle scan with no UVs or picture), `goat-simulator-3-og-npc.zip` (a character from an existing game), `Unconfirmed 131999.crdownload` (an unfinished download).

## 6. What the cleanup could not reach: drivers baked into vehicles

Seated riders are part of the vehicle meshes, baked by `Tools/models/bake.py`: `polo` in 5 vehicles, `hoodie` in 4 and as 1 further driver, `racer` in 3, `gangster` in 1, `shalwar` in 1. So rejected figures can still be seen through a windscreen or on a bike. `racer` is a helmeted rider used only there and was not a street model in this audit.

Removing them means re-baking vehicles, which is outside NPC-only work, so nothing was changed. See K-73.

## 7. Cleanup

- `Assets/_Project/Resources/People/roster.json` holds one record, NPC_012.
- The nine other models moved to `Archive/` at the repository root with their `.meta` files, so their GUIDs are intact and a move back restores them. `Archive/` is ignored by git, so it exists only on this machine.
- No code or data in `Assets/_Project` names a removed model. Original downloads were not touched. The vehicle baker reads from the owner's asset folder, so it is unaffected.
- `Tests/EditMode/NpcTests.cs` allows only `guard` on the roster, so a rejected model cannot come back unnoticed.

## 8. Final active population

One person: NPC_012, `guard`, male, security, walking the sidewalks near the camera. Pedestrians are effectively absent from the streets until more models pass.

Behaviours available to anyone on the roster: walk, stand, talk, run. Clips available: drive, enter-car, exit-car, horn, idle, passenger, run, sit, talk, turn-left, turn-right, walk. There is no fight clip anywhere in the project (K-74).

## 9. Verification (final state, guard only)

- EditMode 298: 295 passed, 0 failed, 3 inconclusive (the K-65 old-map tests). PlayMode 105/105. `NpcClipTests` plays all 12 clips on every roster model; before the cleanup it passed on all 10 models, and it passes on `guard` now.
- Ten views were looked at and accepted as goldens: `n-lineup`, `n-close-a`, `n-act-run`, `n-act-talk`, `n-street-near`, `n-street-hub`, `n-driver-a` to `n-driver-d`. A fresh shot of `n-close-a` and `n-driver-c` then matched its golden (mean diff 0.499 and 0.739, limit 1). The street views show empty sidewalks; that is the true state.
- Benches on `central-avenue-street`, High, this M1 Air on AC, crowd off and on. Six runs finished, in the order off, on, on, off, on, off. Two further runs were cut off when the session ended and are not used.

| | Off | On |
|---|---|---|
| GPU p50 (ms) | 12.03, 11.93, 11.35 | 13.20, 14.70, 9.83 |
| Main thread p50 (ms) | 5.15, 5.13, 5.41 | 5.85, 5.10, 4.83 |
| Frames over 33.3 ms | 3, 5, 45 | 28, 13, 2 |

These do not show a cost or the absence of one: the "on" GPU medians run from 9.83 to 14.70 ms, and the worst run for frames over 33.3 ms was an "off" run. One person is too small an effect for this bench on this machine. Mesh memory at the end was 144 MB off and 145 MB on in the four runs of the first set. See K-76.

No claim is made about a full crowd: the population is one, so a full crowd could not be measured.
