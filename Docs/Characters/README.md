# Who you play: any of the twenty

Since 13 November 2026 you choose who you play from the twenty people built in Blender, ten men and ten women. The owner's words: "remove isaac, use one of our 20 character, so player can select whatever they wanna play, male or female". Licences: [LICENSES.md](LICENSES.md).

Developer: Ishak Hasan Sabbir.

## Choosing

- **Where:** the **Character** row on the start screen and in the pause menu. Left and right (or Enter) go through Man 1 to Man 10, then Woman 1 to Woman 10. The body standing behind the menu changes as you go.
- **First run:** Man 8, in shirt and jeans (`Cast.First`).
- **Kept:** the choice is saved with your progress (`Progress.Character` in `progress.json`) as soon as you make it, and read when the game starts.
- **When:** on foot, or before you stand. In a car or while down the row says so and nothing changes.
- **What carries over:** the new body stands where the old one stood, with your health, armour, guns and the gun in hand.

The code is in `Runtime/Core/Cast.cs` (the list, names, clothes, the rule below) and `Player.Become`.

**Nobody else wears your body.** The street's people and the fighters are all made from the same twenty. While you are someone, the crowd leaves out the walker who has that body. A gang member or officer who has it is given the next person of the same sex instead (`Cast.Other`, in `Fighter.Spawn`), so you never fight yourself.

| | Men | Women |
|---|---|---|
| 1 | Plate carrier, T-shirt and cargo trousers | Plate carrier, T-shirt and cargo trousers |
| 2 | Plate carrier, helmet and bandana mask | Plate carrier, helmet and cargo trousers |
| 3 | Plate carrier, patrol cap and trousers | Fisherman's sweater, cargo trousers and bandana mask |
| 4 | Jacket, jeans, beanie and bandana mask | Paramedic's uniform |
| 5 | Paramedic's uniform | T-shirt, wool trousers and patrol cap |
| 6 | Coveralls and a knitted hat | Coveralls |
| 7 | Polo shirt, trousers and patrol cap | Long open cardigan and jeans |
| 8 | Shirt and jeans | Knitted sweater and wool trousers |
| 9 | Polo shirt and jeans | Sweater and jeans |
| 10 | Suit | Jacket and skirt |

The files are `Resources/People/npc_m01` to `npc_m10` and `npc_f01` to `npc_f10`.

## How the twenty are made

The pipeline lives outside the repository, in `~/Documents/dhaka-character-rebuild/blender/`: `make_roster.py` writes their recipes, `build_roster.py` builds them in Blender with MPFB, and the FBX files and the maps in `Resources/People/Wardrobe/` are copied in. With the editor closed, the Explicit test `PeoplePoseTests.StandThePeopleAsTheIdol` (run by name) turns the stored pose of each to the clips' reference pose. Their quality against the bar is in `~/Documents/dhaka-character-rebuild/AAA_COMPARISON.md`.

**In play, as the player:**
- `OnFoot.Spawn` dresses them as the street does (`NpcCrowd.Dress`: cut-out hair and lashes, the eye shells) and sizes the capsule to their height.
- `PersonLife` gives them blinks, eye glances and swinging hair. They talk only when told to.
- They are drawn at the size they were built, not at a roster height.

**Reference pose:** the clips were made on marker_man. Unity measures a clip's muscles from the pose stored with each avatar, so every one of the twenty has its shoulders, arms, hands and fingers stored as he stood, within 1 degree (EditMode tests). His pose is kept in `Assets/_Project/Editor/ReferencePose.json`, which `PeoplePose` reads.

## History

- **10 November:** Isaac and a female protagonist were built with `Tools/characters/protagonist.py` (MakeHuman's 163-bone rig), but not switched on.
- **12 November:** at the owner's word, every person was replaced by marker_man, the Mixamo character the 87-clip library was made on. He became the player, and the twenty street people were rebuilt to his proportions and reference pose.
- **13 November, morning:** at the owner's word, marker_man was deleted and Isaac rebuilt as the player through the twenty's pipeline.
- **13 November, later:** at the owner's word, Isaac was removed and the player became a choice of the twenty.

## Isaac (removed 13 November 2026)

What went to the Trash:
- from the game: `MalePlayer.fbx` and the three maps only he used (`male_casualsuit01_diffuse`, `male_casualsuit01_diffuse_normal`, `skin_young_lightskinned_male_diffuse_Bronze`);
- `Docs/Characters/MalePlayer-build.json`;
- from the pipeline folder: his build (`stage/isaac`), his renders (`roster20e/MalePlayer`), and his arm and build logs.

What is kept, a few kilobytes: his recipe, `make_isaac.py` and `presets/isaac.json`, in the pipeline folder, so he can be built again.

The story draft still names its protagonist Isaac Mondol ([../RESEARCH/ORIGINAL_GAME_DESIGN.md](../RESEARCH/ORIGINAL_GAME_DESIGN.md)). How the story reads now that you choose who you play is the owner's decision; nothing in it was changed.

## marker_man (deleted 13 November 2026)

His FBX and its four maps went to the Trash. None of the clip files carries his mesh: the clips were downloaded without skin. What the game still needs of him was measured from his model first:
- **His stored pose:** the way each of 47 bones points, and the palms' turn, in `Assets/_Project/Editor/ReferencePose.json`.
- **His arm proportions:** written into `arm_probe.py`.
