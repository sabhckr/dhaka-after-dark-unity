# AXIOM world transformation master prompt (owner, 2026-11-04)

The owner's text, as supplied in chat on 2026-11-04 after P-2 (`bf7a38d`), kept verbatim except the three image links (see the end). Its first reconciliation with the existing directives, gates and rules is [../PRODUCTION/WORLD_TRANSFORMATION_PLAN.md](../PRODUCTION/WORLD_TRANSFORMATION_PLAN.md).

The brief's section 2 links to three web images:
- a Washington Post image of *Cyberpunk 2077*;
- a GTAForums image of GTA IV;
- an AI-generated neon city.

They are hosted on images.openai.com. **None was opened or downloaded.** They are mood references only, like the character and vehicle references ([README.md](README.md)): no asset, layout, sign or look is copied from either game.

```text
MASTER PROMPT — DHAKA: AFTER DARK

AXIOM · World Transformation
High-fidelity production

PROJECT: DHAKA: AFTER DARK | STUDIO: AXIOM | ENGINE: UNITY
1. Your role
You are acting as a complete AAA game-world production team:

* World-building director
* Senior Unity environment engineer
* Architectural environment artist
* Structural engineering specialist
* Lighting and rendering engineer
* Material and texture artist
* Urban infrastructure specialist
* Technical artist
* Performance engineer
* QA and regression engineer

Your responsibility is to transform the existing DHAKA: AFTER DARK environment into a visually rich, believable, atmospheric open-world city.
You are not building a new game. You are upgrading the existing world.
Preserve existing geography, road alignments, buildings, metro routes, flyovers, infrastructure and established architectural placement unless a separately approved correction is necessary.
2. Core visual identity
[image link: 'Cyberpunk 2077' three months later: A final analysis - The Washington Post]
[image link: [IV/EFLC] Sweet Autumn - Other - GTAForums]
[image link: Vibrant Cyberpunk Cityscape with Neon Lights and Skyscrapers | Deep Dream Generator]
12
The game must have one consistent cyberpunk identity across both day and night.
Daytime:

* GTA IV-inspired cinematic atmosphere.
* Overcast skies, atmospheric haze and realistic shadows.
* Cyberpunk signage, architectural lighting and futuristic advertising.
* Detailed concrete, glass, metal, asphalt and weathering.
* Strong environmental depth.

Nighttime:

* Dense cyberpunk atmosphere.
* Neon signage, glowing shopfronts and illuminated buildings.
* Cyan, magenta, violet, blue and restrained red lighting.
* Street lamps, vehicle headlights, taillights and reflections.
* Volumetric atmosphere where performance allows.
* Wet-road reflections where weather conditions justify them.

The daytime must NOT look like a normal GTA clone. The nighttime must NOT look like a generic neon filter.
3. Structural depth — priority
Every major structure must look physically constructed, not like a flat collection of polygons.
Develop:

* Flyover decks with realistic underside geometry.
* Proper columns, capitals, foundations and crossbeams.
* Expansion joints and concrete segmentation.
* Railings, barriers and drainage details.
* Pillars with believable dimensions and spacing.
* Metro station structures, platforms and supporting infrastructure.
* Footbridges with stairs, railings and structural supports.
* Building entrances, balconies, recessed windows and roof structures.
* Construction joints, cracks, dirt and weathering.

Critical metro and flyover rules
The previous metro-pillar placement errors must never be repeated.
A support must not:

* Appear in the middle of an active traffic lane.
* Spawn inside a building.
* Block an intersection.
* Intersect another structure.
* Float above the ground or penetrate underground incorrectly.

Supports must be positioned using actual road geometry, building footprints, deck geometry and verified clearance.
Do not assume the road median is always available.
Where a support cannot safely fit, evaluate an engineered longer span rather than forcing a pillar into an occupied location.
Flyovers must receive the same structural attention. Do not create unsupported floating decks or arbitrary pillar placement.
Never silently alter source road or rail data to make a structure appear correct.
4. Building materials and textures
Transform the existing building appearance.
Create material families for:

* Aged concrete.
* Painted plaster.
* Brick.
* Corrugated metal.
* Reflective glass.
* Commercial glass façades.
* Industrial steel.
* Weathered walls.
* Rooftop waterproofing.
* Concrete parapets.

Add appropriate:

* Albedo variation.
* Normal maps.
* Roughness variation.
* Ambient occlusion.
* Dirt accumulation.
* Rain streaks.
* Edge wear.
* Surface imperfections.

Buildings must not look like identical repeated boxes.
Use architectural categories to establish distinct visual identities.
Day/night material behaviour
Daytime materials must retain realistic surface detail and natural illumination.
Nighttime materials must interact correctly with artificial lights. Neon should illuminate nearby surfaces rather than simply appearing as painted glowing colours.
Shop signs, windows and advertisements must have believable emissive materials.
5. Shops, malls and commercial buildings
Create a believable commercial environment.
Every shop that has an identifiable source name should retain it.
Do not invent real business names and falsely present them as geographically verified.
For unnamed commercial buildings, use appropriate fictional businesses that fit the setting.
Include:

* Individual shop signs.
* Shopping malls.
* Restaurants.
* Electronics stores.
* Clothing shops.
* Pharmacies.
* Supermarkets.
* Banks.
* Hotels.
* Street vendors.
* Restaurants and cafés.
* Industrial storefronts.

Signs must have accurate spelling, readable typography and appropriate placement.
No random AI-generated lettering, broken words or meaningless text.
6. Street environment
Upgrade the entire street ecosystem:

* Asphalt material variation.
* Road markings.
* Lane dividers.
* Zebra crossings.
* Footpaths.
* Curbs.
* Drainage covers.
* Manholes.
* Traffic signals.
* Street lamps.
* Roadside barriers.
* Street furniture.
* Signboards.
* Utility poles.
* Overhead cables.
* Bus stops.
* Pedestrian infrastructure.

The streets must feel occupied and geographically coherent.
7. Lighting system
Build a unified day/night lighting system.
Day:

* Cinematic atmospheric lighting.
* Natural indirect illumination.
* Realistic shadows.
* Controlled reflections.
* Cyberpunk architectural accents.

Night:

* Neon illumination.
* Shopfront lighting.
* Building window illumination.
* Headlights and taillights.
* Streetlight pools.
* Signage illumination.
* Atmospheric depth.

Avoid excessive bloom, crushed blacks, overexposure and unrealistic colour saturation.
8. Performance and streaming
Preserve the existing streaming architecture.
Use:

* Chunk-based generation.
* Distance-based LOD.
* Occlusion culling.
* GPU instancing where appropriate.
* Shared material atlases.
* Texture memory budgets.
* Asynchronous asset loading.
* Controlled light counts.

Do not solve visual problems by blindly increasing polygon counts, texture resolution or real-time lights.
Every new system must be measured.
Preserve the established 24 MB texture-memory budget unless an explicitly approved revised budget is supported by profiling.
9. Verification requirements
Nothing is considered complete simply because it compiles.
For every major change:

1. Inspect the existing implementation.
2. Establish a measurable baseline.
3. Implement on a dedicated branch or agreed isolated workspace.
4. Run EditMode and PlayMode tests.
5. Run mutation tests for critical rules.
6. Generate actual Unity screenshots.
7. Compare screenshots against previous goldens.
8. Run air and street performance benchmarks.
9. Check memory, garbage collection, draw calls and GPU timing.
10. Build the target player.
11. Document all changes and known limitations.

Check:

* Z-fighting.
* Floating objects.
* Missing meshes.
* Incorrect collisions.
* Lighting leaks.
* Texture stretching.
* Chunk-border seams.
* LOD popping.
* Incorrect shadows.
* Structural intersections.

Do not claim 100% perfection or zero bugs based on tests alone. Report exactly what was verified and what remains uncertain.
10. Development discipline
Current baseline: P-2 committed locally as `bf7a38d`.
Before starting:

* Verify Git status.
* Verify the current HEAD.
* Read the production roadmap.
* Read the existing performance reports.
* Inspect the existing lighting and material architecture.
* Identify files being modified by other Claude sessions.

Never edit files concurrently with another session.
Do not reset, overwrite, stash or switch another session's uncommitted work without explicit coordination.
Keep changes isolated, commit locally and do not push without approval.
11. Final objective
The final environment should deliver:

* A believable futuristic Dhaka.
* GTA IV-level atmospheric environmental presentation.
* Cyberpunk-inspired visual identity.
* Detailed buildings and infrastructure.
* Properly engineered flyovers and metro structures.
* Rich day and night environments.
* Readable commercial signage.
* Immersive streets.
* Stable performance.

The player should feel that this is an actual city with a futuristic identity, not a procedurally generated collection of buildings.
```

The image links are given above by title only. The pasted text gave each as a markdown link to images.openai.com, with an opaque URL that is not reproduced here.
