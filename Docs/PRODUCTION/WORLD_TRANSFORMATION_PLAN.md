# World transformation: reconciliation and proposed order

The owner's brief of 2026-11-04 is kept verbatim in [../ART_DIRECTION/WORLD_TRANSFORMATION_BRIEF.md](../ART_DIRECTION/WORLD_TRANSFORMATION_BRIEF.md).

This file sets that brief against the directives, gates and rules already in force, and proposes an order. **Status: the order and the four decisions were approved by the owner on 2026-11-04 (section 3a). Row 1 (D-11a) is done ([D11A_FLYOVER_SUPPORTS.md](D11A_FLYOVER_SUPPORTS.md)); the other rows are not started.** The baseline is `bf7a38d` (K-37 P-2).

## 1. Pre-start checks (brief, section 10)

| Check | Result |
|---|---|
| Git status and HEAD | `main` at `bf7a38d`. The tree was clean apart from the brief file. The repository has no remote; nothing is pushed or published without approval. |
| Other sessions | No other Claude session is working in this repository (2026-11-04). There is nothing to coordinate before editing. |
| Roadmap | Read: [ENVIRONMENT_ROADMAP.md](ENVIRONMENT_ROADMAP.md) D-07 to D-14 and [AXIOM_V3_AUDIT.md](AXIOM_V3_AUDIT.md), section J. J steps 1–6 are done. Steps 7 (flyover supports) and 8 (playable boundary) are neither done nor approved. |
| Performance reports | Read: [../PERFORMANCE/PERFORMANCE_REPORT.md](../PERFORMANCE/PERFORMANCE_REPORT.md) and [P2/bench.txt](P2/bench.txt). Player main thread p50 is about 1.9–2.5 ms; GPU mean is about 5–6 ms in the air and 8–9 ms on the street (M1, 1280×720). The roads worker costs 53 ms a chunk on Line 6 (K-38). |
| Lighting architecture | URP **Forward+** on PC (`PC_Renderer`, rendering mode 2), HDR on. One `DadWorld.shader` takes additional lights, but has **no emissive input** and there is **no exposure control or tone mapping** (K-24). The night is held artificially light so that it stays readable. The sky reflection is a 16-texel cubemap (K-26). |
| Material architecture | 16 generated layers: asphalt, concrete, brick, plaster, paint, metal, rust, glass, roof sheet, tiles and others. They sit in three BC7 2D arrays (albedo; normal; mask = metallic, occlusion, cavity, smoothness) plus a macro map. Stochastic sampling is used (K-22). The total is **16.33 MB of the 24 MB budget**, about 0.33 MB per layer per array, so **one new layer costs 1.0 MB**. Every texture is generated in the editor: no texture is downloaded. |

## 2. What the brief asks, and where it already lives

| Brief section | Existing milestone or issue | Where it stands |
|---|---|---|
| 3: flyover decks, undersides, columns, capitals, crossbeams, joints, railings, drainage | AXIOM J step 7 (D-11 extended); K-20 (decks cast no shadow), K-13 (no ramps), K-05 (no elevation) | Not started. The checker finds 27 flyover decks with **no supports** at all in the slice (AXIOM v3 E.1). Ramps wait for the elevation decision (K-05). |
| 3: metro viaduct and stations | K-37 (P-1 and P-2 done); K-51; groups C, D and E await evidence (P-4, P-5) | On Line 6, 825 piers are built; 354 stand on a drawn carriageway; there are no duplicates and no out-of-range spans. Station structures are boxes on stilts. |
| 3: footbridges with stairs, railings, supports | D-11; K-39; K-50 (d), (e) | Columns are incomplete where the data stops: 26 placed and 8 left out in the slice. |
| 3: entrances, balconies, recessed windows, roofs | D-06b (roofs), D-06c (near facades) | Done and accepted 2026-11-04. Joints, cracks and dirt are part of row 4 below. |
| 3: support rules (lane, building, junction, intersection, floating) | The infrastructure checker ([INFRA_CHECKER.md](INFRA_CHECKER.md)) | Already checks: carriageway, junction, crossing, bus stop, building, raised building, deck top (floating or sunk), through-deck, duplicate, span short or long, and decks inside buildings. **It cannot tell an active lane from a median:** the data has no lane or median polygons (AXIOM v3 G.5). |
| 4: material families, normal, roughness, AO, dirt, rain streaks, edge wear | The material library (D-02, D-06b); D-12 for wet | Most families exist as layers: concrete, plaster, paint, brick, metal, rust, glass and roof sheet. Missing: a waterproofed roof membrane, curtain-wall glass and painted steel. Dirt, streaks and edge wear can come from the cavity channel and from height in the shader, which costs GPU time and no texture memory. |
| 4: emissive signs, windows, adverts | D-13 | Not started. The shader needs an emissive path first. |
| 5: shops, malls, signs | D-09 (signage; the OFL fonts are already in `Art/Fonts`), D-10 (stalls and shops); K-41 | Not started. The signboards are blank colour boards. Names need owner approval (K-41). |
| 6: markings, crossings, footpaths, curbs | D-04, D-05 (done) | Done for the slice. |
| 6: drains, manholes, signals, lamps, poles, cables, bus stops, barriers | D-08 (gated), D-11 | Not started, except bus stop pads (D-05). |
| 7: unified lighting, day and night | D-13; K-24; K-26 | Not started. Tone mapping comes first: neon in an untonemapped HDR frame clips to white. |
| 7: headlights and taillights | Phase F (traffic) | There are no vehicles yet. D-13 can make the lights and the light budget ready, but no headlight can be shown before Phase F. |
| 8: streaming, LOD, instancing, atlases, budgets | In place: chunk streaming, the far and near tiers, instancing for kits, and the shared arrays | **Occlusion culling is not in use.** Unity's baked occlusion needs static scenes, and this world is generated as it streams. Any approach would need its own investigation and measurement. It is not assumed. |
| 9: verification list | The milestone discipline already in use | Same steps. **New:** a dedicated local branch per milestone (section 5). |

## 3. Decisions the brief raises (owner)

These are engineering and direction decisions, not verified errors. Nothing is done on them until the owner answers.

1. **Visual identity.** The brief asks for "one consistent cyberpunk identity" and "a believable futuristic Dhaka". The standing directives say "GTA IV-inspired grounded urban realism" and "authentic Bangladeshi architecture" ([DETAILING_DIRECTIVE.md](../ART_DIRECTION/DETAILING_DIRECTIVE.md), [ENVIRONMENT_MASTER_DIRECTIVE.md](../ART_DIRECTION/ENVIRONMENT_MASTER_DIRECTIVE.md)). That is a direction change. The brief also says to keep geography, buildings and footprints. The proposal that satisfies both is a near-future layer on today's city: the identity comes from lighting, grading, LED and neon signage, and advertising for fictional brands. Buildings and geography are unchanged.
2. **The gate.** "Do not begin D-07, D-08 or large-scale environment production" until the D-06 sequence and the infrastructure gates are approved. D-06 is accepted, and J steps 5 and 6 are done. Steps 7 and 8 are not. The brief asks for the whole street ecosystem, which includes D-08.
3. **Piers on a drawn carriageway.** On Line 6, 354 of 825 piers stand on one; in the slice, 38 of 61 do (K-37 groups C, D and E). The brief says a support must not stand "in the middle of an active traffic lane". The map draws the road as one carriageway, with no median, so the checker cannot say which piers are in a lane and which in a median. On the real line, most piers stand in road medians. The choices are:
   - draw a median island under the piers where the road is wide enough, with the source road data unchanged;
   - wait for evidence or lane data;
   - span the places or use portal frames. That means far longer spans: a single pier left out makes up to 64 m.
4. **Shop names.** The brief says "Every shop that has an identifiable source name should retain it". The standing rule is that businesses are fictional and that no real trademark comes in without approval ([DETAILING_STANDARDS.md](../ART_DIRECTION/DETAILING_STANDARDS.md); K-41). Whether the chunk data carries any shop names at all has not been checked yet.

Also open, and unchanged by the brief: K-51 (one deck over tracks at different heights), K-50 (c) (the elevation decision), the evidence for groups C, D and E, and the roads worker cost (K-38). K-37 P-3 (counter wording) was applied 2026-11-05.

## 3a. Owner decisions (2026-11-04)

The owner chose the recommended answer to each question in section 3:

1. **Identity: a near-future layer.** Today's buildings and geography stay unchanged. The cyberpunk identity comes from lighting, grading, LED and neon signage, and adverts for fictional brands, by day and night. The grounded realism directives stand for everything built.
2. **Gate: start J step 7 now.** The work follows the order in section 4. The playable boundary (J step 8) comes before D-07 and D-08, which stay gated.
3. **Piers on a drawn carriageway: draw medians.** A raised median island is built under piers where the road is wide enough, with traffic passing on both sides. The source road data is untouched, and each case goes in the register. Where the road is too narrow, the pier stays listed and is reported. This is K-37 P-6, row 1b of section 4, applied 2026-11-04.
4. **Shop names: fictional only.** The standing rule (K-41) holds: original fictional names in real Bengali and English, from a list the owner approves. Map names are not used.

## 4. Proposed order

The existing ids are kept. Sub-steps take the D-06a/b/c pattern, and each is marked **proposed** until the owner approves it.

| # | Work | Id | Why here | Measured by |
|---|---|---|---|---|
| 1 | Flyover supports and closed decks with shadows: piers placed by the same rules and checker as P-2, cap beams, deck undersides, joints, parapets and drains. The viaduct kit gets the same detail (pier capitals, girder segments, parapets). Ramps wait for K-05. | J step 7 = D-11a (**done 2026-11-04**) | The brief calls structure its priority. 27 decks float today. | The checker: no-supports 27 → 0 with no new carriageway or building finding unlisted; K-20 shadows; goldens; worker ms |
| 1b | Median islands under Line 6 piers that stand on a drawn carriageway, where the road is wide enough; the rest stay listed | K-37 P-6, **done 2026-11-04** ([P6_MEDIAN_ISLANDS.md](P6_MEDIAN_ISLANDS.md)): Line 6 354 → 331 on a carriageway, 23 on islands; slice 38 → 28, 10 on islands | Decision 3; the brief's no-pier-in-a-lane rule | The checker: carriageway findings on Line 6 (354) and in the slice (38) before and after; median clearance; goldens |
| 1c | A continuous raised median along the Line 6 viaduct where one-way twins keep two lanes each side; piers on it stand on it | K-37 P-5a, **done 2026-11-05** ([P5A_CONTINUOUS_MEDIAN.md](P5A_CONTINUOUS_MEDIAN.md)): Line 6 331 → 311 on a carriageway, 23 on the median (37 pieces, 1,835 m), 20 on P-6 islands; slice 28 → 27 | The evidence pass (K-37 report section 15): real piers stand in a kerbed median; the owner: "Yes, build it" (3.0 m, two lanes kept) | The checker: carriageway findings before and after; `median-*` and `run-*` checks; goldens |
| 2 | Exposure and tone mapping | K-24 = D-13a (proposed) | Every later light, emissive and material is judged through it | Day and night goldens re-reviewed; GPU ms |
| 3 | Material families and weathering: up to 3 new layers (+3.0 MB, to about 19.3 MB of 24), plus dirt, streaks and edge wear in the shader | D-06d (proposed) | Buildings first, before signs hang on them | The texture memory test; GPU ms; contact sheet; goldens |
| 4 | An emissive path, lit windows, and street-light pools under a Forward+ light budget | D-13 | Night identity | Light count; GPU ms at night; night goldens |
| 5 | Signage and shops: fonts are in place; the names follow decision 4 | D-09, D-10 | Needs decision 4 | A shaping test; every string checked by eye; atlas MB |
| 6 | Wet surfaces and reflections | D-12 | Builds on rows 3 and 4 | Wet goldens; GPU ms |
| 7 | Playable boundary | J step 8 | The last gate before D-07 and D-08 | — |
| 8 | Street furniture, utilities and trees | D-08, D-07 | Gated | Roadmap checks |
| 9 | The Farmgate visual benchmark, then scale | D-14 | Exit | Five times of day plus wet; seams and popping; player bench |

Each row runs the brief's section 9 list: tests, mutations for critical rules, Unity screenshots against reviewed goldens, air and street benches, memory, GC, draw calls and GPU, the macOS and Windows builds, and docs. A Windows build is not a Windows run: there is no Windows machine (K-02).

## 5. Branch workflow (brief, section 9.3)

- Each milestone is done on a local branch named `wt/<id>` (for example `wt/d11a`), cut from `main`.
- It is fast-forward merged into `main` only when its report is done. Nothing is pushed.
- **Branches in this checkout, not git worktrees.** A second worktree needs its own Unity `Library` (several GB, and a full reimport) against the 25 GB allocation. A branch switch with the editor closed costs only a reimport of the files that changed.
- Only one session writes to the repository at a time.

## 6. Not claimed

- No pier, deck, road, rail or building of the source data has been changed for this brief: D-11a adds flyover supports and girders under the decks as drawn. The canonical world data is untouched.
- The two game titles are mood references only. Their images were not opened, and nothing is copied from either game.
- The budget figures in rows 3 and 4 are estimates from format and size. They are not measurements, and the milestone that does the work measures them.
