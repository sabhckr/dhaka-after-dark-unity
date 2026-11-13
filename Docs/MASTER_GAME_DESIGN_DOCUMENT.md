# DHAKA: AFTER DARK — master game design document

Open-world action-adventure, urban crime drama, driving and life simulation, set in a geographically grounded Dhaka. Windows PC, Unity 6.3 LTS with URP, one developer working with AI tools.

**State on 2026-10-30:**
- **Done:** design, research and the data pipeline.
- **Not started:** game code. Unity is not installed yet, pending approval.
- **Not lost:** the three.js prototype in `dhaka-after-dark` is unchanged and stays the reference.

## Pillars

1. **The city is the game.** Real streets from OpenStreetMap, real districts, and systems that come from Dhaka life (mixed traffic, night markets, power cuts, monsoon) rather than generic city props.
2. **Systems that meet.** Traffic, police, economy and missions share one world and one event bus. A crash in traffic creates witnesses; witnesses create heat; heat changes routes and prices.
3. **A story worth finishing.** One protagonist, a family stake, three acts, two choices and three endings ([RESEARCH/ORIGINAL_GAME_DESIGN.md](RESEARCH/ORIGINAL_GAME_DESIGN.md)).
4. **Verified, not claimed.** Every feature has acceptance criteria and evidence ([PRODUCTION/FEATURE_BACKLOG.md](PRODUCTION/FEATURE_BACKLOG.md)).

## Core loops

| Loop | Length | Shape |
|---|---|---|
| Moment | Seconds | Drive or walk → react to traffic, people and police |
| Job | 2–10 min | Pick up a fare, delivery or mission → complete under city pressure → money and reputation |
| Night | 20–40 min | Collect business income at the safehouse → choose jobs and missions → save |
| Act | Hours | Unlock districts' content, businesses and contacts → act-ending mission |

## Systems summary

| System | Design source | Architecture | Phase |
|---|---|---|---|
| World and streaming | Real OSM data, 500 m chunks | [ARCHITECTURE/WORLD_DATA_PIPELINE.md](ARCHITECTURE/WORLD_DATA_PIPELINE.md) | B–C, I |
| Visual identity | Facade families, rooftops, balconies, signage, lighting tiers | Building kit plus rules from OSM tags | D |
| Player | Third person, on foot and in vehicles, health, inventory, clothing | CharacterController, Animator, Cinemachine | E |
| Vehicles | 10 classes with distinct specs | WheelColliders plus spec ScriptableObjects | E |
| Traffic and people | Four simulation levels; per-junction signals; pedestrians with roles | Lane graph plus jobs | F |
| Time and weather | 24-hour clock, compressed seasons, rain and waterlogging | Lighting tiers, wet-surface shader | F |
| Police (heat) | Five levels, witnesses, search areas, exits | State machine plus responders | G |
| Combat | Melee first, ranged later, clear hit reactions | Components plus data | G |
| Economy | Fares, deliveries, businesses, properties, a ledger | Plain C# ledger, saved | G |
| Saves | Slots, versioned, atomic writes, backups, migration | Plain C# JSON | G |
| Missions | Data-driven runner, checkpoints, failure as story | Mission assets | H |
| Radio and audio | Six fictional stations; ambience by landuse and time | AudioMixer, zones | H |
| UI | HUD, map, radio wheel, menus, settings, accessibility | UI Toolkit | B (shell) to J |

## Content scope (target, re-estimated at each phase exit)

| Content | Target |
|---|---|
| Districts with full content | 10 (Farmgate, Kawran Bazar, Tejgaon, Motijheel, Old Dhaka, Mirpur, Dhanmondi, Gulshan, Banani, Uttara), within the data's 1,303 chunks |
| Main missions | 24–30 across three acts |
| Side job types | 7 (fares, deliveries, ambulance, rescue, races, route shifts, collectibles) |
| Businesses | 5 |
| Vehicle classes | 10 |
| Radio stations | 6, with commissioned or licensed music |

## Definition of done

The checklist from the production directive is kept in [RELEASE/RELEASE_CHECKLIST.md](RELEASE/RELEASE_CHECKLIST.md). Each item needs linked evidence before it is ticked.

## Constraints

- **Storage:** 25 GB for the Unity workspace ([PERFORMANCE/PERFORMANCE_REPORT.md](PERFORMANCE/PERFORMANCE_REPORT.md)).
- **Hardware:** an 8 GB M1 for development. No Windows test machine yet.
- **Assets:** original or licensed only. Every art folder has a `LICENSES.md`, and nothing is downloaded without approval.
- **Real brands:** the coach and airline names already in the three.js project stay as they are, by owner decision.
- **Repositories:** this repository is local and has no remote. Pushing anything public needs approval.
