# Third-party licences: the people you play

You play any of the twenty people built in Blender ([README.md](README.md)), who are also the street's people and the fighters. They are made from free assets and free tools only. Nothing was bought, and no account was opened for them.

## What is shipped in the game

### The twenty: MakeHuman assets, through MPFB 2, and a few others

Every part, its author, its licence and who wears it: [../ASSET_INVENTORY/PEOPLE_CREDITS.md](../ASSET_INVENTORY/PEOPLE_CREDITS.md), from `~/Documents/dhaka-character-rebuild/blender/CREDITS.md` as built on 12 November 2026. Most parts are CC0; 21 clothes, hats and shoes are CC-BY and the plate carrier asks for credit. Since 13 November 2026 all of them are on the game's credits screen (start screen, Credits), with title, maker, licence and source page, and `CreditsTests` checks that every CC-BY part of that record stays there.

The credits use the licence each asset's page on the MakeHuman community site gives. Two files say otherwise in their own header, and the owner should know:
- **M1951-72 helmet** (JALdMIC, worn by Man 1): the page says CC-BY; the file's header says AGPL3, with a link to MakeHuman's note on its tools' licence. That line looks like the default header of the old MakeClothes tool, but it is not certain. To be safe before a release, the owner may choose another helmet for Man 1.
- **Old female sweater** (janexx, worn by Woman 9): the page says CC-BY by janexx; the file says CC0, author unknown. It is credited as CC-BY, which covers both.

Licence versions as the files give them: Mindfront's, the EMT uniform's and the male classic jeans' CC BY 4.0; the female tight jeans CC BY 3.0; the coveralls, the beanie, the bandana mask, the helmet and the sweater only "CC-BY", with no version.

The skeleton is MPFB's Mixamo-named rig, a rig definition shipped with MPFB, free to use in an exported character (MPFB FAQ). The face shapes (52 expression units, 15 speech shapes) are from the packs `faceunits01` and `visemes02` by Mika Suominen, CC0. The skin's fine relief is a detail normal map made from numbers by `pores.py` (no photograph, nothing downloaded).

MakeHuman states that a character exported from MakeHuman, and the bundled assets, may be used in closed-source and commercial work without restriction. MPFB states the same for characters exported with it. See the MakeHuman FAQ and the MPFB FAQ under Sources.

Isaac, the player until 13 November 2026, was removed; his parts were all CC0 and are no longer shipped.

### Animation clips

Clips added for the protagonists on 10 November, and used by everyone since, in `Assets/_Project/Resources/People/Clips/`:

| Clip | Mixamo name | Used for |
|---|---|---|
| `getup-back.fbx` | Getting Up (from the back) | Ragdoll rising, lying face up |
| `getup-front.fbx` | Getting Up (from the stomach) | Ragdoll rising, lying face down |
| `fall.fbx` | Falling Idle | imported, not yet used |
| `land.fbx` | Falling To Landing | imported, not yet used |

All four were downloaded from Mixamo (Adobe) on the owner's existing Mixamo sign-in, without skin, at 30 fps. Mixamo's FAQ says its characters and animations are royalty free for personal, commercial and non-profit projects, games included, and that the raw files may not be redistributed or sold on their own. The rest of the project's clips (idle, walk, run, rifle and pistol stances) were already in the project and are used as they were.

### Unity packages

| Package | Version | Licence |
|---|---|---|
| Animation Rigging (`com.unity.animation.rigging`) | 1.4.1 | Unity Companion License (free with the Unity Editor) |

## Tools used, not shipped

| Tool | Version | Licence | What it did |
|---|---|---|---|
| Blender | 4.5 | GPL v2 or later | Built and exported the characters, run without a window |
| MPFB 2 (MakeHuman plugin for Blender) | 2.0.18 | GPL v3 (the code) | Body, skeleton, assets, face shapes. Its code is not in the game; what it exports is CC0 (above) |
| Rhubarb Lip Sync | 1.14.0 | MIT | Mouth cues from recorded lines (`Tools/characters/lipsync.sh`). The binary is not tracked: it sits in `Tools/rhubarb/`, which git ignores |
| Rosetta 2 | macOS's own | Apple's licence, accepted by the owner on 10 November 2026 | Runs Rhubarb's Intel build on Apple silicon. Part of macOS, not of the game |

## Not used, and why

- **Rokoko free mocap:** needs an account. None was opened.
- **CMU motion capture:** not needed. Mixamo had the get-up clips.
- **Rigify:** not needed. MPFB's Mixamo-named skeleton has every finger joint, and Unity's Humanoid maps it directly.
- **Characters or art from commercial games:** not used.

## Sources

- MakeHuman, "Are MakeHuman files free?": https://static.makehumancommunity.org/makehuman/faq/are_makehuman_files_free.html
- MPFB, "Use in closed source": https://static.makehumancommunity.org/mpfb/faq/use_in_closed_source.html
- MakeHuman asset packs: https://static.makehumancommunity.org/assets/assetpacks.html
- MPFB versions: https://extensions.blender.org/add-ons/mpfb/versions/
- Mixamo FAQ: https://helpx.adobe.com/creative-cloud/faq/mixamo-faq.html
- Rhubarb Lip Sync releases and licence: https://github.com/DanielSWolf/rhubarb-lip-sync/releases
