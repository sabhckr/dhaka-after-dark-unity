# Feature Translation Matrix: Vice City design principles to DHAKA: AFTER DARK

Status: research-to-design bridge. Compiled 2026-10-30.
Companion to: `Docs/RESEARCH/VICE_CITY_DESIGN_RESEARCH.md`. Citations `[R-n]` refer to that document's numbered Sources list.

## Rules for this document

- **Point 1 (what it does)** describes the Vice City feature and is cited to [R-n]. Fan-wiki sources are marked "(fan)".
- **Points 2–8 are Interpretation throughout.** They are our design analysis and proposals, not claims about Vice City or Rockstar's intent.
- **Originality.** Every Dhaka version below is a new design. We take no names, map layouts, missions, characters, dialogue, music, station formats or assets from Vice City. Where a mechanic category is generic to the genre (a wanted meter, a save point), we design our own rules, numbers and presentation.
- **Dhaka claims.** These are kept general. Anything specific that should be checked with local sources or residents before it ships is tagged **[verify]**. No real living people are depicted. No real political parties, real broadcasters, real police units or real businesses are named or imitated. Fictional names are used throughout.
- **Sensitivity rule** (from the depiction controversy in [R-1]). Satire aims at institutions, traffic, bureaucracy and the absurdities of city life. It never targets ethnic, religious or regional communities. Religious sites are no-combat zones.

## Project baseline (facts from the brief)

The current prototype is a Three.js browser game on real Dhaka OpenStreetMap data. It has:

- 1,303 chunks of 500 m
- a road graph with traffic agents
- pedestrians with roles
- one global traffic-signal clock

It has no police, missions, economy, audio or save system. Districts in the data include Farmgate, Kawran Bazar, Motijheel, Gulshan, Banani, Dhanmondi, Uttara, Mirpur, Old Dhaka, Tejgaon, Shahbagh and Jatrabari.

Target: Unity URP, Windows PC, one developer with AI tools.

## Quick index

| # | Feature | Priority (Interpretation) |
|---|---|---|
| 1 | District gating | P1 |
| 2 | District identity and density | P1 |
| 3 | Heat (wanted) meter | P1 |
| 4 | Police response tiers | P1 |
| 5 | Heat reset mechanics | P1 |
| 6 | Vehicle roster and handling | P0 |
| 7 | Driving camera | P0 |
| 8 | Traffic | P0 (exists in prototype) |
| 9 | Pedestrians | P0 (exists in prototype) |
| 10 | Mission givers and contacts | P1 |
| 11 | Mission fail, retry and checkpoints | P1 |
| 12 | Vehicle jobs (CNG, rickshaw, ambulance-style) | P2 |
| 13 | Property and business income | P2 |
| 14 | Collectibles | P3 |
| 15 | Stunt jumps and flyovers | P3 |
| 16 | Timed challenges (rampage-style, reframed) | P3 |
| 17 | Radio stations (music) | P2 |
| 18 | Talk radio and city satire | P2 |
| 19 | Safehouses and saving | P1 |
| 20 | Time of day, monsoon and load-shedding | P2 |
| 21 | Map and radar | P0 |
| 22 | Health, armour and perks | P1 |
| 23 | Weapons and combat | P2 |
| 24 | Outfits | P3 |

P0 = needed for a playable vertical slice. P3 = polish/content.

---

## 1. District gating

1. **What it does:** The game starts with the player confined to one island. The in-fiction reason is a storm warning that closes the bridges. An early story mission opens the mainland [R-6, R-15, R-16 (fan)]. The wanted ceiling is also capped at first and raised by story missions [R-6 (fan)].
2. **Why it works:** A small starting area can be learned fully. The unlock becomes a story event, and the threat level grows along with the player's tools.
3. **Enjoyment:** The first trip into the larger city is a moment of anticipation paid off. Places you saw on the horizon become reachable.
4. **Interactions:** Mission unlocks, police ceiling, traffic/pedestrian spawn tables, radio commentary on the reopening, property availability.
5. **Technical:** A region system mapping chunks to districts. Gate colliders or soft barriers on bridges and highway ramps. A save-persisted `WorldState` flag set. Chunk streaming that does not load gated districts beyond the visual horizon.
6. **Dhaka adaptation:** Gate by circumstance, not by geography alone. Dhaka's real districts are connected by many roads, so a single bridge chokepoint is unrealistic. Use believable, temporary city events: a flooded underpass and closed arterial roads during a monsoon surge, or a road closed for a fictional public event or construction. Start in one coherent cluster, such as Farmgate–Kawran Bazar–Tejgaon, and open outward. River crossings toward Old Dhaka and the Buriganga are natural later gates **[verify crossing points on OSM]**.
7. **Original implementation:** A `DistrictDefinition` ScriptableObject for each district: chunk IDs, unlock condition, ambience profile. A `GateController` that spawns road-closure props from a set (barricades, waterlogging decals, traffic cones) at OSM edges crossing the district boundary. Lift the gates with a short scripted beat (rain clears, police remove barricades) rather than a cutscene.
8. **Test:** EditMode test: every road-graph edge that crosses a locked boundary has a gate. Automated: graph search from the start district reaches no locked chunk. PlayMode: setting the unlock flag makes the barricades disappear, and the path then exists. Playtest acceptance: 5 of 5 testers understand why the road is closed without reading a tooltip.

## 2. District identity and world density

1. **What it does:** Districts are distinguished by architecture, palette, landmarks and resident groups. The world is packed with many small interactive "toys" [R-4, R-5 (fan), R-15 (fan)]. There are 110 unique pedestrian models [R-1].
2. **Why it works:** Players navigate by landmark and mood, not street names. Many cheap, readable signals beat a few expensive simulations.
3. **Enjoyment:** Each district feels like a different place to be. Exploring is rewarded with discoveries.
4. **Interactions:** Pedestrian and traffic spawn tables, radio ads, mission themes, property types, police density, ambient audio.
5. **Technical:** Per-district profiles for: building material/colour sets, prop density, ped and vehicle mixes, ambient soundbeds, signage atlases. LOD and impostors for the 500 m chunks. GPU instancing for repeated props.
6. **Dhaka adaptation:** Base the identities on general, observable character. Examples:
   - Motijheel as the commercial and banking core
   - Gulshan and Banani with diplomatic-area roads and newer high-rises
   - Old Dhaka with dense narrow lanes and older buildings
   - Kawran Bazar as a busy wholesale market
   - Dhanmondi with its lake and residential streets
   - Mirpur and Uttara as large planned residential areas
   - Tejgaon as industrial
   - Shahbagh as an area of institutions and culture

   **[verify each characterisation with residents]** Use rickshaw-art-inspired colour and motifs in signage, painted in an original style. No real shop names or brands.
7. **Original implementation:** A `DistrictProfile` asset per district, assigned from the OSM `district` tags already in the data. Procedural facade kits (3–4 per district) with a hand-placed landmark per district, built as an original stylised building rather than a replica. Prop scatter rules (tea stalls, electric-pole wire bundles, shop shutters) driven by OSM land-use tags.
8. **Test:** Automated: every chunk resolves to exactly one profile. Each profile references existing assets, checked with an asset validator in EditMode. Performance budget test per chunk (draw calls, tris). Playtest: blindfold-drop testers into 6 districts. They should name the district type (market, residential, old city, commercial) at least 70% of the time.

## 3. Heat (wanted) meter

1. **What it does:** A star meter on the HUD rises with crimes seen by police or committed in public. In Vice City it runs from 1 to 6 stars [R-1, R-6 (fan)].
2. **Why it works:** It turns abstract "police attention" into a readable, discrete state with known consequences.
3. **Enjoyment:** Self-chosen risk. The player decides how much trouble to court, and escaping is a mini-game.
4. **Interactions:** Police response, traffic (civilians clearing), mission fail conditions, outfit and vehicle reset systems, radio news reactions.
5. **Technical:** A `HeatSystem` service. Crime events (event bus) carry a severity and witness check. Line-of-sight from police and civilian witnesses. Decay timers. A last-known-position search area.
6. **Dhaka adaptation:** Use an original meter of 5 levels shown as a single original icon, not stars. Witness reporting can include civilians phoning it in, giving a delay before police arrive. Minor traffic offences (running a manual traffic-police signal) give low heat. Serious crimes give high heat.
7. **Original implementation:** A data-driven crime table (ScriptableObject): `crimeId`, base heat, needs-witness flag. Heat is a float 0–5 with thresholds, so it can be tuned without code changes. A visible search-radius ring on the minimap while police hunt.
8. **Test:** EditMode unit tests for heat math: thresholds, decay rate, caps under district-gating limits. PlayMode test: firing a weapon in view of a scripted officer raises heat to at least 1 within 1 s. Playtest: testers can explain what raised their heat and how to drop it after one session.

## 4. Police response tiers

1. **What it does:** Each wanted level brings a clearly different response, escalating in steps: foot officer, then active pursuit, then roadblocks, spike strips and a helicopter, then armoured units, then federal agents, then military [R-6 (fan)]. Being arrested fails the mission and costs weapons and money [R-6 (fan)].
2. **Why it works:** Each tier is a visible, new problem. The player feels the escalation rather than reading it.
3. **Enjoyment:** Escalating set-piece chases that come from the player's own actions.
4. **Interactions:** Traffic AI (pulling over, roadblocks on the road graph), heat, vehicles, map, radio.
5. **Technical:** A pursuit director that budgets active units by tier. Roadblock placement on road-graph nodes ahead of the player's predicted path. Pursuit vehicle AI on the road graph with off-graph steering near the player.
6. **Dhaka adaptation:** Stay grounded in a city where heavy traffic is itself an obstacle. Low tiers: traffic police at junctions on foot and police motorbikes. Mid tiers: patrol pickups and junction blockades using existing traffic. High tiers: a fictional armed response unit. Keep every unit fictional in name, livery and insignia. Do not model or imitate real units or uniforms **[verify legal guidance on uniform likeness]**. Avoid any "corrupt cop" stereotype as a mechanic (see feature 5).
7. **Original implementation:** `PursuitTier` assets: unit types, spawn budget, roadblock chance, tactics. Reuse the prototype's traffic agents: police vehicles are traffic agents with a pursuit behaviour layer. Roadblocks are spawned prefabs on graph nodes 300–600 m ahead.
8. **Test:** PlayMode soak test: hold each tier for 120 s and assert unit count never exceeds budget and frame time stays within budget. Automated: roadblocks spawn only on valid graph nodes. Playtest: a heat-5 escape is possible but rare (target 20–40% success for mid-game testers).

## 5. Heat reset mechanics

1. **What it does:** Ways to lower the wanted level: fixed police-bribe pickups (minus one star), a clothing change (up to two), a paid vehicle respray (with probation stars that return if you reoffend), passing a mission, or waiting out one star [R-6, R-7, R-24, R-25 (fan)].
2. **Why it works:** Each reset is a destination on the map, so escaping becomes navigation under pressure. Each has a cost.
3. **Enjoyment:** A tense dash to a known safe spot. Clever use of the city.
4. **Interactions:** Map POIs, economy (fees), outfits, vehicle damage, mission scripting.
5. **Technical:** POI registry. A `HeatResetZone` component with cost and conditions. Probation state in `HeatSystem`.
6. **Dhaka adaptation:** Do **not** use pickups that read as bribing police. That would play on a stereotype about local institutions and could offend. Original alternatives:
   - Lose the pursuit inside dense market lanes, where cars cannot follow and you can blend into crowds.
   - Swap vehicles inside a garage-workshop district (a "re-paint and number-plate change" body shop, fee-based) **[verify that this framing is acceptable to the target audience]**.
   - Change clothes at a tailor or clothes stall.
   - Ride out the heat as a passenger in public transport.
7. **Original implementation:** `HeatResetZone` variants: `BlendCrowd` (heat decays 3x while on foot in crowd volumes), `Workshop` (fee, 10 s timer, vehicle ID changes), `Tailor` (outfit swap, minus 1 level). Probation: a 60 s flag, during which any crime restores the previous level.
8. **Test:** Unit tests for each reset path and the probation rule. PlayMode: entering a workshop at heat 3 with enough money sets heat to 0 and probation to true. Playtest: at least 3 of 4 reset types are discovered without a tutorial within 2 hours.

## 6. Vehicle roster and handling

1. **What it does:** A wide set of vehicles (cars, motorcycles, scooters, boats, helicopters). Motorcycles were new to the series [R-1, R-4, R-20]. Handling was praised as forgiving and fun, and also called slippery in tight turns [R-1, R-19, R-20]. Keyboard driving on PC was widely criticised [R-1].
2. **Why it works:** Variety gives each trip a choice. Forgiving physics make reckless driving fun, not punishing.
3. **Enjoyment:** Vehicle feel is the most-used verb in the game, so good handling makes everything around it better.
4. **Interactions:** Traffic, jobs, police pursuit, stunts, camera, audio (engine and radio), economy (buying and storing).
5. **Technical:** Arcade vehicle controller (raycast wheels or simplified rigidbody) with per-vehicle `HandlingProfile`. Keyboard steering curve with speed-dependent assist. Gamepad support. Collision damage model (visual plus health).
6. **Dhaka adaptation:** Base the roster on general vehicle categories seen in the city:
   - cycle-rickshaws (slow, narrow, lane-capable)
   - CNG-powered auto-rickshaws (agile, three-wheeled) **[verify the common colour scheme]**
   - motorbikes (ride-share-style riders are common) **[verify]**
   - private cars and SUVs
   - city buses and coaches
   - trucks
   - river boats for Buriganga areas, as a later stage

   All vehicles are original designs with fictional makes.
7. **Original implementation:** Start with 4 handling archetypes: two-wheeler, three-wheeler, car, heavy. Then skin variants. The three-wheeler gets a custom tip-over model (low speed rollover on sharp turns) as a signature feel. Keyboard: smoothed steering input with return-to-centre and lower max angle at speed.
8. **Test:** Automated: handling regression scene runs a scripted slalom and records lap times. Fails if a change moves times more than 10%. Keyboard-only playtest: 5 testers complete a 2 km route with at most 3 wall hits median.

## 7. Driving camera

1. **What it does:** Third-person chase camera. Reviewers criticised awkward angles, catching on objects and erratic swings [R-1, R-19].
2. **Why it works (when done right):** The camera is the player's eyes. A good one reads speed and shows the turn ahead.
3. **Enjoyment:** A smooth camera makes speed feel exciting rather than disorienting.
4. **Interactions:** Vehicles, narrow lanes, pursuit, combat aiming, interiors.
5. **Technical:** Cinemachine follow camera with collision (sphere-cast pull-in, damping). Look-ahead along velocity. Per-vehicle offsets. A separate lane camera profile for narrow streets.
6. **Dhaka adaptation:** Old Dhaka lanes and markets are narrow and covered with overhead wires and awnings. They need a tighter, higher camera profile triggered by street-width data from OSM **[verify that OSM width tags are available; otherwise estimate from building gaps]**.
7. **Original implementation:** `CameraProfile` assets switched by trigger volumes generated from street width. Wires and awnings use a camera-ignore layer, so they never occlude or block.
8. **Test:** Automated camera bot: drive scripted routes through the 20 narrowest streets and log the frames where the player is hidden. Target 0.5% or fewer occluded frames. Playtest: no tester reports motion sickness or "lost the car" in a 30-minute session.

## 8. Traffic

1. **What it does:** Ambient traffic fills the roads and becomes an obstacle in chases. Civilians clear the streets at very high wanted levels [R-6 (fan)]. Most traffic can be carjacked [R-20].
2. **Why it works:** Traffic makes the city feel alive and turns every chase into a fresh, unscripted obstacle course.
3. **Enjoyment:** Weaving through traffic and near-misses. Emergent crashes.
4. **Interactions:** Police, jobs, heat (carjacking), radio (traffic reports), time of day, weather.
5. **Technical:** The prototype already has a road graph with agents and one global signal clock. In Unity: DOTS/Jobs or pooled MonoBehaviours, density by chunk distance, simple lane-following plus local avoidance.
6. **Dhaka adaptation:** Traffic culture is the signature system. Mixed modes share a lane: rickshaws, CNGs, buses, cars. Heavy congestion at peak hours. Buses stop wherever passengers wait. Manual control at junctions by traffic police **[verify how common manual control is versus signals today]**. Replace the single global signal clock with per-junction controllers. Some are signal-based, some police-controlled.
7. **Original implementation:**
   - Keep the road graph.
   - Add per-junction `JunctionController` (signal or officer).
   - Add mode-specific behaviours (rickshaws keep left and slow; buses pull in at stops).
   - Add a congestion field that slows the agent target speed by time of day.
   - Pool 200–400 agents near the player.
8. **Test:** Automated: 10-minute headless sim with no agent stuck more than 60 s and no overlapping agents beyond tolerance. Gridlock detector: a test fails if more than 15% of agents in a junction radius are at speed 0 for more than 90 s. Playtest: testers describe traffic as "busy but passable".

## 9. Pedestrians

1. **What it does:** Many pedestrian types. Allied gang members defend the player and rivals attack on sight [R-1]. Pedestrians act as witnesses for the wanted system [R-6 (fan)].
2. **Why it works:** Pedestrians signal what a district is like and react to the player, which makes the world believable.
3. **Enjoyment:** Overheard moments and reactions give the city personality.
4. **Interactions:** Heat (witnesses), jobs (passengers, patients), markets, crowds for hiding, audio barks.
5. **Technical:** The prototype has pedestrians with roles. Unity: animation LOD, crowd flow fields on sidewalks and lanes, role-based schedules, a reaction state machine (idle, curious, flee, report).
6. **Dhaka adaptation:** Roles grounded in everyday city life: vendors, porters at markets, office workers, students, tea-stall customers, bus helpers calling out stops **[verify the role list with residents]**. Bangla barks written originally by native speakers. Avoid caricature. Represent a range of ages, genders and incomes.
7. **Original implementation:** `PedRole` assets (schedule, idle animations, barks, reaction thresholds). Market crowd volumes with high density and slow flow. Witness reporting with a phone-call delay.
8. **Test:** Automated: crowd density per district within target ranges. Reaction tests (gunshot leads peds within 30 m to flee within 1 s). Native-speaker review sign-off for every bark. Playtest: no tester flags a pedestrian as a stereotype.

## 10. Mission givers and contacts

1. **What it does:** Story missions come from character contacts, using a go-to-contact loop. Several can be open at once [R-1, R-22]. The story and mission flow were designed first, then new features were introduced at deliberate points [R-3].
2. **Why it works:** Contacts give missions a face and a motive. Parallel contacts give the player choice of order.
3. **Enjoyment:** Getting to know characters. A sense of a network growing around the player.
4. **Interactions:** District gating, property (business missions), economy, radio (reactions to events), phone or message system.
5. **Technical:** Mission graph (nodes with prerequisites). Contact markers on the map. A scripting layer for objectives: data-driven steps, with code for special cases.
6. **Dhaka adaptation:** Entirely fictional contacts rooted in city life: a garage owner in a workshop district, a wholesale trader at a market, a bus-company dispatcher, a fixer in Old Dhaka, a journalist. No real people and no real political parties or factions. Missions are delivered in person, by phone or by messaging app (original UI).
7. **Original implementation:** A `MissionDefinition` ScriptableObject made of an ordered list of `ObjectiveStep` assets (GoTo, Deliver, Chase, Escape, Talk, Survive). A mission graph validator. Start with 3 contacts and 4 missions each for the vertical slice.
8. **Test:** EditMode: mission graph has no cycles, every mission is reachable, every referenced location exists on the road graph. PlayMode "mission bot" runs each mission's objectives with a debug autopilot to completion. Playtest: average time to the first mission marker is under 3 minutes.

## 11. Mission fail, retry and checkpoints

1. **What it does:** Death or arrest fails the mission. The player respawns at a hospital or police station and loses weapons and money [R-1, R-6 (fan)]. A paid cab can return the player to the mission start [R-17 (fan), R-20]. A lead developer admits some difficulty spikes surprised the team [R-3]. Reviewers found some missions frustrating [R-1, R-19]. *Whether Vice City had mid-mission checkpoints: not verified.*
2. **Why it works (and fails):** A failure penalty gives stakes. The quick-return reduces dead time. Replaying long sections to reach a hard part does not respect the player's time.
3. **Enjoyment:** Fair stakes and quick retries keep players in flow.
4. **Interactions:** Missions, economy (penalties), save system, difficulty tuning, telemetry.
5. **Technical:** Checkpoint snapshots (player state, mission step, relevant world flags). A fail-reason taxonomy. Local analytics: attempts, fail step and time.
6. **Dhaka adaptation:** "Retry from checkpoint" offered at once on the fail screen. If the player chooses to respawn instead, a CNG or ride-share-style lift back to the contact. No cash penalty on mission fail, only on free-roam death or arrest.
7. **Original implementation:** Every `ObjectiveStep` can be flagged as a checkpoint. On fail: restore the snapshot, reset mission-spawned actors, keep ammo spent (or restore it; a tunable). Adaptive assist: after 3 fails on the same step, offer an optional easier variant (more time, fewer enemies). Never auto-apply it.
8. **Test:** PlayMode: forced fail at each checkpoint, then retry restores the expected state (position, health, objective index), checked automatically. Telemetry gate: any step with a fail rate over 40% in playtests is flagged for redesign. Playtest acceptance: median retry-to-action time is under 10 s.

## 12. Vehicle jobs (taxi/ambulance-style, reimagined)

1. **What it does:** Entering certain vehicles starts repeatable jobs: taxi fares, paramedic pickups, firefighting, vigilante pursuit, pizza delivery. Each has levels and a permanent perk when completed [R-10 (fan)].
2. **Why it works:** They reuse vehicles, traffic and the map to make endless content, and they pay in capability as well as cash.
3. **Enjoyment:** Short, self-contained goals and a reason to master each vehicle.
4. **Interactions:** Vehicles, traffic, peds (passengers), economy, perks, map routing.
5. **Technical:** Job state machine (level, timer, target). Fare/pickup generator on the road graph and footpaths. Payout curve.
6. **Dhaka adaptation:** Original jobs grounded in city work:
   - **CNG fares:** haggle-free fixed fare; bonus for comfort, meaning few hard brakes.
   - **Cycle-rickshaw fares:** stamina, narrow lanes, short trips. A strong Old Dhaka job.
   - **Market delivery:** a van or pickup carrying produce from a wholesale market before dawn **[verify market hours]**.
   - **Patient transport:** an ambulance-style job through congestion. Use a fictional service.
   - **Bus helper or route run:** keep to the schedule and pick up at stops.

   Each gives an original perk, such as better stamina, cheaper repairs or a traffic "gap finder".
7. **Original implementation:** A `JobDefinition` asset (vehicle class, generator, timer formula, payout curve, perk at completion). One generic `JobRunner`. Ship two jobs for the vertical slice (CNG fares, rickshaw fares).
8. **Test:** Automated: generator always produces reachable pickups and drops within distance bounds. Payout monotonic per level. Playtest: testers voluntarily replay a job at least 3 times, and the job's earning rate is within ±25% of the target economy curve.

## 13. Property and business income

1. **What it does:** Two property classes. Safehouses for saving and storage, and businesses that each come with a set of missions. Once those are done, a business generates income collected on site. Owning a minimum number of businesses gates the final story mission [R-1, R-12 (fan)]. The design makes the player feel like a kingpin [R-3].
2. **Why it works:** It gives money a purpose, turns each business into a content pack, and makes the map feel owned.
3. **Enjoyment:** Visible growth and passive rewards. Unlocking new content by investing.
4. **Interactions:** Economy, missions, saves (save points), map icons, story progression, radio ads.
5. **Technical:** `PropertyDefinition` (cost, unlock condition, missions, income per in-game day, cap). Economy ticks on the in-game clock. Persistent ownership.
6. **Dhaka adaptation:** Original, legitimate-front businesses typical of city commerce: a vehicle workshop, a tea stall chain, a print shop, a rickshaw garage (rents rickshaws to drivers), a small bus-route operator, a wholesale stall at a market. Income is affected by systemic city events: lower takings in power cuts and floods.
7. **Original implementation:** Accumulated income capped at N days, to encourage visits without punishing absence. 2–3 missions per business, reusing `MissionDefinition`. Income collection at a door marker. Start with 3 properties.
8. **Test:** EditMode economy simulation: run 30 in-game days with scripted play and check total cash stays within target bands (no runaway or starvation). Unit tests: cap and tick logic. Playtest: testers can say what each business does and why they bought it.

## 14. Collectibles

1. **What it does:** 100 hidden packages. Small cash per package, and milestone rewards (weapons, then vehicles) delivered to safehouses, with a big final bonus [R-8 (fan)].
2. **Why it works:** Collectibles reward knowing the map and give a reason to explore odd corners. Milestones break a long task into goals.
3. **Enjoyment:** Discovery and a growing armoury at home.
4. **Interactions:** Map, safehouses (reward delivery), stunt routes, rooftops and lanes, completion tracker.
5. **Technical:** Collectible registry with stable IDs, save flags, and an optional late-game map reveal.
6. **Dhaka adaptation:** Original collectibles with cultural texture:
   - hand-painted rickshaw-art panels (original artwork, ideally commissioned from or credited to local artists) **[verify rights and credit]**
   - lost cinema handbills (original, fictional films)
   - rooftop kite spots

   Rewards are cosmetic (rickshaw paint jobs, outfits) and practical perks.
7. **Original implementation:** Place 50, not 100, for a solo scope. Use a placement tool that snaps to rooftops, lanes and dead-ends tagged from OSM. Reward table every 10.
8. **Test:** Automated: each collectible is reachable from the nav graph (on foot or vehicle). No duplicate IDs. Save round-trip keeps collected state. Playtest: median tester finds 5 in the first 2 hours without a guide.

## 15. Stunt jumps and flyovers

1. **What it does:** 36 unique stunt jumps with rising payouts. Motorcycle wheelies and stunts also pay out [R-13 (fan), R-20].
2. **Why it works:** They turn the city's geometry into a playground and reward vehicle mastery.
3. **Enjoyment:** Spectacle and a skill expression moment.
4. **Interactions:** Vehicles, camera (slow-motion replay), map, economy.
5. **Technical:** Launch and landing trigger volumes. Airtime and rotation scoring. Optional slow-motion camera.
6. **Dhaka adaptation:** Use real infrastructure types: flyover gaps, ramps at construction sites, embankments by water bodies, half-built structures **[verify flyover and expressway geometry against OSM]**. Keep jumps away from crowds.
7. **Original implementation:** A `StuntJump` prefab with launch box, landing box and minimum speed. Payout via the economy table. Generate candidates by scanning elevation deltas in the road graph, then hand-pick 15.
8. **Test:** Automated physics test: a scripted car at the specified speed lands in the target volume (the jump is achievable). Playtest: each jump is completed by at least 50% of testers within 5 tries.

## 16. Timed challenges (rampage-style, reframed)

1. **What it does:** 35 skull pickups. Each locks the player to one weapon and sets a 2-minute target: kills, drive-by kills, run-overs or vehicle destruction. They can be restarted, and payouts rise [R-9 (fan)].
2. **Why it works:** A short, intense, clearly scored burst of the core systems.
3. **Enjoyment:** Arcade-style score chasing inside the open world.
4. **Interactions:** Combat, vehicles, heat (suspended or modified), economy.
5. **Technical:** Challenge runner with loadout override, timer and target counter. Restore the previous inventory at the end.
6. **Dhaka adaptation:** **Do not** use civilian-killing sprees in a real city. That content would be offensive and would harm the project's reception. Original non-lethal or gang-only variants:
   - **Rush Hour Run:** deliver within a time limit through congestion.
   - **Chase-down:** stop N fictional gang vehicles.
   - **Lane Sprint:** an on-foot parkour race through market lanes.
   - **Flood Run:** cross waterlogged streets within a time limit.
7. **Original implementation:** A `ChallengeDefinition` (type, loadout, timer, target, reward curve) reusing `JobRunner` patterns. Leaderboard stored locally.
8. **Test:** Unit: timer and counter logic, inventory restore. PlayMode: a challenge started and failed restores the inventory exactly. Playtest: the median first attempt finishes at 60–90% of target (hard but close).

## 17. Radio stations (music)

1. **What it does:** Nine fixed stations covering different genres, with DJ links and spoof ads, plus a user-music slot on PC [R-14 (fan)]. Stations are built around a defined style, using genre experts and consistent mastering [R-2]. Radio was among the most praised elements [R-1]. Later re-releases changed playlists [R-14 (fan)].
2. **Why it works:** Radio carries era, place and humour into every drive, at low content cost per minute of play.
3. **Enjoyment:** Choosing a station is self-expression. Discovering songs and jokes.
4. **Interactions:** Vehicles, missions (news reacting to events), time of day, ads for in-game businesses.
5. **Technical:** Streaming audio with a station clock (a station "plays" even when not tuned). Segment scheduler (song, DJ link, ad, ident). Ducking for dialogue. Vehicle radio and ambient shop radios.
6. **Dhaka adaptation:** Original fictional stations with Bangla identities, for example: a modern Bangla pop and band station, a folk and Baul-influenced station, a film-song-style station of original compositions, and an electronic/hip-hop station of local-scene-style originals **[verify genre labels with local musicians]**. All music is original or commissioned with perpetual game rights, so re-releases never lose content. Invite local independent artists under clear contracts.
7. **Original implementation:** Station = `StationDefinition` (playlist, DJ segment pool, ad pool, ident pool) plus a scheduler with a virtual clock. Ship 2 stations of about 45 minutes each for the first milestone. Add a user-music folder slot (like the PC original's) as a cheap, rights-safe extra.
8. **Test:** Automated: every scheduled clip exists. Loudness-normalisation check (LUFS within target) in a build step. Scheduler never repeats a song within N minutes. Playtest: testers leave the radio on in more than 50% of driving time.

## 18. Talk radio and city satire

1. **What it does:** Two talk stations (celebrity-chat and public call-in formats). Talk content was about three times GTA III's, with callers partly modelled on a producer's experience [R-1, R-14 (fan)]. Reviewers had mixed views on how funny it was [R-19, R-22].
2. **Why it works:** Satire builds the world: it tells you what the city worries about without a single cutscene.
3. **Enjoyment:** Humour, recognition, and the "did they just say that?" moment.
4. **Interactions:** Missions (talk show reacts to player events), world events (floods, power cuts), businesses (ads).
5. **Technical:** A large VO pool with tagging. Event-triggered segments. Subtitles. Bangla and English localisation.
6. **Dhaka adaptation:** An original Bangla call-in show with fictional hosts and callers. Topics: traffic, rents, load-shedding, monsoon flooding, exam season, phone scams, cricket mood (no real players named). The satire punches up at systems and absurdities, never at communities or religion. No real broadcasters, presenters or politicians are imitated. All scripts are reviewed by Bangladeshi writers.
7. **Original implementation:** Segment tags (`topic:flood`, `trigger:mission_x_done`). The scheduler prefers event-relevant segments. Start with about 30 minutes of talk content. AI tools may draft, but humans write and approve, and native voice actors record (no synthetic imitation of real voices).
8. **Test:** Automated: every talk segment has subtitles in both languages and a sensitivity-review sign-off flag in metadata. The build fails without it. Playtest with Bangladeshi testers: a satire panel of at least 5 people. No segment rated offensive by 2 or more panellists ships.

## 19. Safehouses and saving

1. **What it does:** Saving happens by entering a safehouse save point. Some safehouses store vehicles, and the main ones handle outfit changes and receive collectible rewards. Two are unlocked through the story and the rest are bought. Bought businesses can also act as save points [R-12, R-23 (fan)].
2. **Why it works:** Saving becomes a place. More property means shorter trips, which ties saving to the economy.
3. **Enjoyment:** A sense of home. Garages show off your vehicle collection.
4. **Interactions:** Property, economy, collectibles, outfits, vehicle storage, mission availability.
5. **Technical:** Versioned save format (JSON or binary) with schema migrations. Atomic write (temp file plus rename). Save slots. Autosave after missions (a modern comfort beyond the original's model).
6. **Dhaka adaptation:** Safehouses that fit city housing: a rented room in a mess (shared lodging) **[verify the term and how common it is]**, a rooftop room, a flat in a residential block, a room above a workshop. A garage spot in a building's ground-floor parking.
7. **Original implementation:**
   - Manual save at safehouses, plus autosave on mission complete and on property purchase.
   - `SaveData` covers player state, world flags, district gates, properties, collectibles, mission graph state, economy tick, vehicles in garages and settings.
   - Each section of `SaveData` carries a version number.
8. **Test:**
   - Automated round-trip: generate random states, then save, load and compare deep-equal.
   - Migration tests: older-version save fixtures load correctly.
   - Corruption test: a truncated file leads to fallback to the last good slot.
   - Playtest: no lost progress reported across all sessions.

## 20. Time of day, monsoon weather and load-shedding

1. **What it does:** A time cycle with mostly clear weather, rare fog, and rain shown with raindrops on screen (a series first). Some missions script their weather, and the weather report ties into the bridge-closure story beat [R-26 (fan)].
2. **Why it works:** Changing light and weather make one map feel like many and set mission mood.
3. **Enjoyment:** Atmosphere. Seeing familiar streets transformed.
4. **Interactions:** Traffic density, ped schedules, police, jobs, radio, district gating (storms), visuals and performance.
5. **Technical:** Global clock service. URP lighting presets blended over time. Rain VFX. Wet-surface shader. Puddle/flood decals. Light-zone toggling.
6. **Dhaka adaptation:** Two signature, original systems:
   - **Monsoon rain and waterlogging:** low-lying streets flood in heavy rain. This slows vehicles, rickshaws can pass where cars stall, and some underpasses close **[verify which areas commonly waterlog; use as inspiration, not a hazard map]**.
   - **Load-shedding:** scheduled power cuts by area at night. Streetlights and signs go dark, generators hum, traffic signals fail and fall back to officer control, and businesses earn less **[verify current frequency; present it as a world system, not a political statement]**.
7. **Original implementation:** `WeatherState` machine (Clear, Haze, Rain, HeavyRain) with seasonal weights. A flood mask per chunk derived from OSM elevation or low-lying tags, raised and lowered over time. `PowerGrid` service: areas = district clusters. Cut schedules as data. Each light, sign and signal subscribes to its area.
8. **Test:** Automated: during heavy rain, flood depth only rises on chunks with a flood mask. During a power cut, all lights in the area are off and signals switch to fallback within 1 s. Performance: the frame-time budget holds with rain plus the night power-cut lighting set. Playtest: testers name load-shedding and flooding as memorable moments.

## 21. Map and radar

1. **What it does:** A radar or minimap, plus a full city map with a location indicator (noted as new versus the radar-only approach) [R-18]. Contacts and services appear as icons [R-1, R-12 (fan)].
2. **Why it works:** Navigation must be effortless in a big city, or players spend their time lost rather than playing.
3. **Enjoyment:** Planning routes. Spotting new icons.
4. **Interactions:** Every system that places POIs: missions, jobs, properties, heat resets, collectibles.
5. **Technical:** Render the map from OSM vector data (roads, water, land use) to texture tiles. GPS route on the road graph (A*). Icon registry with priorities and filters.
6. **Dhaka adaptation:** Map labels in Bangla and English (player toggle). Route preference by vehicle (a rickshaw route avoids main highways; a car avoids lanes). Show flooded or closed roads live.
7. **Original implementation:** Bake tiles at build time from the existing OSM pipeline. The minimap is a render texture of the tiles. The GPS path line is drawn on the map and as a thin ground ribbon in-world (optional).
8. **Test:** Automated: GPS returns a path for 1,000 random pairs within the unlocked area in under 5 ms each. Icons never overlap beyond tolerance at default zoom. Playtest: no tester reports being lost for more than 2 minutes.

## 22. Health, armour and perks

1. **What it does:** Health pickups and food restore health, and body armour absorbs damage [R-1, R-27 (fan)]. Maximum health and armour rise through side jobs and completion [R-10, R-11 (fan)]. Death means respawn at a hospital, losing weapons, armour and some money [R-1].
2. **Why it works:** Progression comes from playing the world, not from menus.
3. **Enjoyment:** Concrete, felt upgrades ("I can sprint forever now").
4. **Interactions:** Jobs, challenges, combat, economy, saves.
5. **Technical:** `Stats` component with modifiers. Perk registry. Pickup system.
6. **Dhaka adaptation:** Recovery at tea stalls and food stalls (original vendors). Fictional clinics and hospitals as respawn points. Perks from jobs: rickshaw work gives stamina, delivery work gives a traffic-gap sense, patient transport gives faster recovery.
7. **Original implementation:** Modifier stack (base + perk + temporary). Perks are data assets granted by job/challenge completion. Respawn penalty is a tunable percentage of cash, capped.
8. **Test:** Unit: modifier math, cap rules. Save round-trip for perks. Playtest: testers can name at least 2 perks they earned and what they do.

## 23. Weapons and combat

1. **What it does:** Weapons bought, found or taken from enemies [R-1]. Lock-on targeting was criticised as clunky by several outlets. Mouse aiming on PC was praised [R-1, R-19]. Enemy AI was criticised [R-1, R-19].
2. **Why it works (lesson):** Combat that feels unfair or clumsy drags down otherwise good missions.
3. **Enjoyment:** Responsive aim and readable enemy behaviour.
4. **Interactions:** Missions, heat, challenges, health and armour, economy (ammo).
5. **Technical:** Third-person over-the-shoulder free aim (mouse first), optional soft aim assist for gamepad, cover-lite (crouch behind objects), hit reactions, enemy AI with a simple utility or behaviour tree.
6. **Dhaka adaptation:** Keep firearms rare and serious (late-game, mission-bound). Use melee and non-lethal options (shove, grab, break free) for most street conflict. No combat in religious sites. No violence against named or recognisable real groups.
7. **Original implementation:** Small arsenal (6–8 fictional weapons). Enemy archetypes: rusher, shooter, flanker. Combat arenas within missions tuned with telemetry.
8. **Test:** Automated: hit-scan accuracy test at set distances. AI does not shoot through walls (raycast checks in test scenes). Playtest: the combat section fail rate is 40% or lower, and the "felt fair" rating is at least 4 of 5.

## 24. Outfits

1. **What it does:** About 12 outfits, unlocked by missions, purchases or completion, and changed at clothing pickups. Changing outfit can lower the wanted level, and a uniform-style outfit allows access to restricted areas until crimes give you away [R-24 (fan)].
2. **Why it works:** A cosmetic system that also has systemic uses (heat reset, disguise).
3. **Enjoyment:** Self-expression, plus disguise as a clever tactic.
4. **Interactions:** Heat, missions (infiltration), safehouses, economy.
5. **Technical:** Character customisation (modular meshes or outfit prefabs). An outfit flag set read by NPC access checks.
6. **Dhaka adaptation:** Everyday city clothing styles (shirt-trousers, panjabi, lungi for casual/home settings, a courier jacket, a fictional company uniform) **[verify the clothing list and context with residents; avoid costume-like or mocking portrayals]**. A worker uniform (fictional delivery company) gives access to a loading dock or building service areas.
7. **Original implementation:** 6 outfits for scope. The `Outfit` asset has an access tag set. Change at safehouses and tailor stalls.
8. **Test:** Unit: access checks per tag. PlayMode: entering a restricted zone with the right tag gives no heat, and a crime there raises heat. Playtest: at least 1 disguise use per tester in an infiltration mission without prompting.

---

## Cross-system interaction map (Interpretation)

- **Economy hub:** Jobs, challenges and collectibles feed cash. Cash flows to properties, which pay out income and unlock missions, which advance the story, which unlocks districts. This loop is the backbone.
- **Heat loop:** Crime raises heat. Police tiers respond and traffic becomes an obstacle. The player escapes using map POIs (resets), or is arrested or killed, triggering respawn and the retry system.
- **City-state loop:** The clock drives weather (monsoon), power (load-shedding) and traffic density. Those affect jobs, police, business income and radio topics. This is where Dhaka's identity becomes systemic rather than decorative.

## Build order suggestion (Interpretation)

1. Vehicle feel, camera, map/GPS, traffic port (features 6, 7, 8, 21).
2. Save system and one safehouse (19).
3. Heat, police tiers and resets (3, 4, 5).
4. Mission framework with checkpoints, and 1 contact (10, 11).
5. CNG and rickshaw jobs, plus perks (12, 22).
6. One business property and income tick (13).
7. Monsoon and load-shedding (20).
8. Radio (one music station and one talk show) (17, 18).
9. Content layers: collectibles, stunts, challenges, outfits, combat expansion.
