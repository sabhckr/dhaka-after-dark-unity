# Roadmap

Phases A–J from the production directive, in dependency order. A phase starts only when the one before meets its exit criteria. Effort figures are **estimates** in focused working days for one developer with AI tools. They are re-estimated at each phase exit from the measured pace of the phase before.

| Phase | Status | Depends on | Exit criteria (evidence required) | Effort (est.) |
|---|---|---|---|---|
| A Audit and research | **Done** except the owner review of the design | — | Research docs with sources; translation matrix; original design; storage budget | 2 |
| B Engine foundation | **Done**: project, reader, EditMode and PlayMode tests, Windows build, coordinates, input and benchmarking | A; install approval | Project opens; URP; Input System; DADC C# reader passes the cross-language test on all 1,303 chunks; EditMode and PlayMode tests run from the command line; first Windows Mono build | 6–10 |
| C World vertical slice | **Done** 2026-11-02: C-01 to C-07. Exit review and re-check in [PHASE_C_EXIT_REVIEW.md](PHASE_C_EXIT_REVIEW.md); closed by the owner's decision, reading the 4 ms rule on the streaming markers and the main thread's own CPU time (not-running frames stay under K-18) | B | Farmgate / Kawran Bazar box (12 chunks) streams in and out with no main-thread spike over 4 ms; roads and buildings align with the data (spot checks ≤ 1 cm); metro stations elevated; free camera and debug views | 10–15 |
| D Visual transformation | **In progress** from 2026-11-02: plan, audit and milestones D-01 to D-14 in [ENVIRONMENT_ROADMAP.md](ENVIRONMENT_ROADMAP.md) (owner's environment directive) | C | Coherent Farmgate environment: real materials and architectural variety; correctly shaped Bengali signs; plausible street infrastructure; 20+ tree species; continuous day and night; no visible chunk seams or popping; player-build performance and memory; regression tests; reproducible visual benchmark | 49–81 (re-set from 20–35 for the larger scope) |
| E Player and vehicles | Not started | C (D in parallel) | Animated original protagonist on foot; enter and exit; 4 vehicle classes with distinct handling (car, CNG, bus, motorcycle); cameras; automated drive-path test | 20–30 |
| F Living city | Not started | E | Traffic in four simulation levels on the lane graph; per-junction signals; pedestrians on sidewalks; time of day; rain; 30-minute soak without stuck junctions | 25–40 |
| G Gameplay systems | Not started | F | Heat (wanted) system, combat, economy ledger, inventory, properties, save slots with migration and corruption recovery | 25–40 |
| H Narrative and content | Not started | G | Act 1 complete (missions, side jobs, radio, audio); then Acts 2–3 | 60–120 |
| I Full world integration | Not started | H (Act 1) | All districts streamed; systems city-wide; per-district identity pass | 30–60 |
| J Polish and release | Not started | I | Windows IL2CPP builds; tier validation on real Windows hardware; accessibility; release checklist complete | 30–45 |

**Total (est.): 257–443 working days** (Phase D re-set to 49–81 on 2026-11-02), before commissioned music and voice. The vertical slice (through Phase F, Farmgate box only) is 112–178 of them.

## Next three tasks

1. **D-06 buildings, second pass** ([ENVIRONMENT_ROADMAP.md](ENVIRONMENT_ROADMAP.md)). D-01 (lit world shader), D-02 (material library), D-03 (time of day), D-04 (roads, second pass) and D-05 (footpaths and kerbs) are done, and the detailing directive's Milestone 2 (one of each Farmgate asset type, [M2_FARMGATE_VALIDATION.md](M2_FARMGATE_VALIDATION.md)) is done; D-06a (classification and a far-tier facade for every building, [D06A_CLASSIFICATION_FAR_TIER.md](D06A_CLASSIFICATION_FAR_TIER.md)) is done; D-06b (roofs and materials, [D06B_ROOFS_MATERIALS.md](D06B_ROOFS_MATERIALS.md)) is done; D-06c (near facades, [D06C_NEAR_FACADES.md](D06C_NEAR_FACADES.md)) is done (2026-11-04), and the D-06 sequence awaits the owner's acceptance before D-07 begins. The owner's detailing directive (2026-11-03) sets the detail standard for this and later work: [DETAILING_STANDARDS.md](../ART_DIRECTION/DETAILING_STANDARDS.md), inventory in [ASSET_INVENTORY](../ASSET_INVENTORY/README.md). The owner's AXIOM master directive (2026-11-03) was audited against the project in [MASTER_DIRECTIVE_AUDIT.md](MASTER_DIRECTIVE_AUDIT.md); the owner approved its D-06 re-scope on 2026-11-03; its other decisions are open.
2. **D-07 trees.**
3. **D-08 street furniture and utilities.**

Details and every later task are in [FEATURE_BACKLOG.md](FEATURE_BACKLOG.md).
