# Art direction references

## Detailing directive and standard (owner, 2026-11-03)

- [DETAILING_DIRECTIVE.md](DETAILING_DIRECTIVE.md): the owner's directive on world, character and vehicle detail, verbatim.
- [DETAILING_STANDARDS.md](DETAILING_STANDARDS.md): how it applies here (geometry rules, naming, LOD, references, licensing, milestone mapping, open decisions).
- [../ASSET_INVENTORY](../ASSET_INVENTORY/README.md): every requirement with its status and evidence.

## Material sheet (D-02, 2026-11-02)

[MATERIAL_SHEET.png](MATERIAL_SHEET.png) shows the 18 world material ids, each on a 4 m floor and a 2.6 m wall, lit by the scene's fixed sun.

- **Order** (left to right, top to bottom): Default, Soil, Grass, Water, Paving, Asphalt; Concrete, Plaster, Roof, Brick, Paint, Metal; Rust, Glass, RoofSheet, Tiles, Sand, Marking.
- **Source:** every texture is generated from code: `Assets/_Project/Editor/Materials`, menu *Dad / Generate world materials*. No photo or downloaded texture is used.
- **Re-render:** `-executeMethod Dad.EditorTools.Materials.MaterialSheet.RenderBatch`.
- **Colour:** each swatch takes its layer's mean colour as its vertex colour. In the city, the builders' palettes set the colour and the texture adds detail around it.

## Character and vehicle references (owner, 2026-11-02)

[CHARACTER_VEHICLE_REFERENCES.html](CHARACTER_VEHICLE_REFERENCES.html) is the owner's art-direction brief for people and vehicles. It is a copy of `dhaka_after_dark_visual_references.html` as supplied (9,451 bytes, SHA-256 prefix 99b02d2eef19d2c8).

**What it is for**

- It is the visual target for **Phase E** (player and vehicles), and for the people and traffic of **Phase F** ([ROADMAP.md](../PRODUCTION/ROADMAP.md)).
- It does not change Phase D. The owner said so when sending it: do not switch milestones, download anything or start modelling because of it.

**About its images**

- Its 9 images are hotlinked from third-party sites (gtainside, igrandtheftauto, tweaktown, cloudinary, 80.lv, gumroad, turbosquid, pinterest, tapimg). None is stored in this repository.
- The brief itself says they are references to study, not assets to copy or import, and that copyright stays with their owners.
- The copy here was read as text only. Opening it in a browser fetches the images from those sites.

### Standards taken from it

**People**

- Grounded, everyday Bangladeshi people, with original faces, bodies and clothing. GTA IV is a reference for mood and lived-in presence, not for its characters.
- Faces need believable proportions, subtle skin variation, stubble and eye detail, and a restrained expression.
- People belong to the street: lighting and atmosphere sit on them as on the city. They are never showroom models.
- Meshes need clean deformation topology at the shoulders, elbows, hips, knees and face.
- UVs are deliberate, with consistent texel density and no stretching. Every texture is checked on the model in Unity.
- High-poly detail is baked down, never carried into runtime meshes.
- Skin is not plastic, and pore contrast is kept low.

**Vehicles**

- Convincing proportions, distinct silhouettes and parts that look functional. Designs are locally appropriate and original, built from photographic reference.
- CNG three-wheeler: canopy, windshield, passenger opening, grille, wheel placement and body construction.
- Intercity coach: window rhythm, luggage and body sections, trim; an original Bangladeshi-market coach.
- Interiors and the passenger experience matter: the BUSSID-style ambition.

**Materials (URP)**

| Material | Standard |
|---|---|
| Skin | Base colour, normal and roughness, with a restrained URP-compatible skin response |
| Fabric | Cotton, denim, knitwear and uniforms, with real folds |
| Vehicle paint | Controlled reflections, plausible roughness, subtle wear |
| Rubber | Tyre sidewall and tread detail, high roughness |
| Glass | Correct transparency and reflection, never mirror-like |
| Metal and plastic | Separate responses for trim, grilles, dashboards and fittings |

**Acceptance**

- An asset is judged in this project's Unity 6 URP scene, not in a Blender viewport.
- It needs LODs, texture atlases, shared materials, culling and a measured frame time.

**First production slice in the brief**

- Three NPC prototypes: a young male pedestrian, a middle-aged shopkeeper and a young female pedestrian.
- Three vehicle prototypes: a sedan, a CNG auto-rickshaw and an intercity coach, each with exterior, interior, collision and distinct handling.
- One validation scene in the Farmgate test area, checked by day and night, for shadows, road contact, camera distance and performance.

**Constraints restated in the brief**

- Unity 6 with URP, on an 8 GB M1 MacBook Air, within the 25 GB storage budget.
- Existing world data, streaming and finished systems are kept.
- No downloads or purchases without approval.

### How it fits the architecture

**Assemblies and data**

- `Dad.Vehicles` and `Dad.People` are planned assemblies ([UNITY_ARCHITECTURE.md](../ARCHITECTURE/UNITY_ARCHITECTURE.md)).
- Vehicle specs are ScriptableObjects; vehicles use Rigidbody and WheelColliders.
- Pedestrians run as kinematic agents on a sidewalk graph, GPU-instanced at a distance.
- Art lives under `Art/` with a `LICENSES.md` per folder, as the fonts do.

**Rendering**

- People and vehicles get their own URP shaders or materials, separate from `Dad/World`.
- They share its lighting: the sun, sky, fog and night factor from D-03, and the wet surfaces from D-12.
- The validation scene can use the golden-view tooling: fixed views at set times of day, compared on every run.

**Performance and storage**

- The bench already records batches, SetPass calls, triangles, shadow casters and graphics memory (D-01). That figures each prototype's cost.
- Phase E's backlog rows (E-01 to E-06) cover the protagonist, locomotion, cameras, the vehicle controller and specs. The brief's six prototypes fit E-01 and E-04 to E-06.

### Limits and dependencies

**Hardware and storage**

- The 8 GB M1 is the only test machine. Skinned meshes, cloth-like fabric and glass are GPU and memory costs, and the Windows tiers are unmeasured (K-02).
- Skin shading in URP has no built-in subsurface model. A restrained custom skin response is the realistic aim.
- High-poly sculpts and bakes need Blender and disk space outside the 25 GB project budget, or a share of it agreed first.

**Sourcing**

- Original models need modelling time or commissioned work.
- Licensed models, such as the TurboSquid CNG in the brief, need a purchase and the owner's approval.
- Faces need photographic references with the right permissions.

**Order**

- Characters need animation: a locomotion set and a rig, E-01 and E-02.
- Vehicle interiors need cameras (E-03) to be seen.
- Night validation needs D-13, wet validation D-12.
