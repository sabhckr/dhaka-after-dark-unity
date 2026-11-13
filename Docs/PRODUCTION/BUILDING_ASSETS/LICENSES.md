# Downloaded buildings: licences and credits

These are the buildings under `Assets/_Project/Resources/Buildings/`. The full download record for all seven candidates is `~/Downloads/3D assets/buildings-new/MANIFEST.md` on the owner's Mac.

## old_residential

- **Model:** "Old Residential Building [4k]" by Andrej Grave (@andrej.grave).
- **Source:** https://sketchfab.com/3d-models/old-residential-building-4k-dd400ed693cf416e940c5b8e3107205e
- **Licence:** CC BY 4.0 (Sketchfab "CC Attribution"). Commercial use is allowed, and the author must be credited.
- **Credit line for the game's credits:**

  > "Old Residential Building [4k]" by Andrej Grave, licensed under CC BY 4.0: https://sketchfab.com/3d-models/old-residential-building-4k-dd400ed693cf416e940c5b8e3107205e

- **Downloaded:** 11 November 2026, the creator's original upload ("source" format, 179,976,294 bytes).
- **Modifications** (by `Assets/_Project/Editor/BuildingImport.cs`):
  - **Unchanged:** the FBX, the colour maps (JPEG) and the normal maps (PNG) are byte-for-byte copies.
  - **New mask per material:** made from the creator's metallic, AO and roughness maps. R holds metallic, G holds occlusion, and A holds 1 − roughness.
  - **Window colour map:** the creator's opacity map is written into its alpha channel.
  - **Unity import:** compressed (high quality), at the creator's full size (4096 or 2048). The textures are in the "World" mip group, so Medium and Low quality draw them one mip smaller.
