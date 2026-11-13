# First playable (MVP)

The owner asked on 13 November 2026: "now polish everything, and make it first playable MVP".

**What "first playable" means here.** The master design names four loops ([MASTER_GAME_DESIGN_DOCUMENT.md](../MASTER_GAME_DESIGN_DOCUMENT.md), core loops):

| Loop | Length | What it is |
|---|---|---|
| Moment | seconds | Drive or walk, and react to traffic, people and police |
| Job | 2–10 min | Take a fare, delivery or mission, finish it under city pressure, and get money |
| Night | 20–40 min | Collect income, choose jobs, save |
| Act | hours | Story |

A first playable has the first two loops working end to end, kept between runs, in a build you can start like a game. The translation matrix marks the parts a playable slice needs (P0):
- vehicles and handling;
- driving camera;
- traffic;
- pedestrians;
- map and radar.

## What was already there (13 November, before this work)

Checked by running the game's own PlayMode tests in the real city, with their pictures looked at:
- `PlayerInterfaceShots.TheInterfaceInTheCity`
- `PlayerInCityTests.PlainPlayIsOneManWhoWalksDrivesAndGetsOut`

| Part | State |
|---|---|
| Start screen, pause, settings (6 tabs), rebinding, map with waypoint and route | works |
| On foot: walk, run, crouch, jump; three guns, aim, fire, reload, weapon wheel | works |
| Car: get in, drive, get out (the Jetta); traffic; train boarding | works |
| Street people, gangs on their turf, police with a 5-level wanted meter | works |
| Going down and standing again | works |
| HUD: radar, health, armour, wanted, clock, weapon, prompts, street and district | works |
| **Something to do: a job with a goal, a clock and pay** | **missing** |
| **Money** | **missing** |
| **Anything kept between runs** (except settings) | **missing** |
| The player's body | marker_man, the Mixamo character in a mocap suit, a test dummy |

## What the first playable adds

1. **Who you play:** first Isaac (owner's choice, 13 November, 7cb8764); then, at the owner's word the same day, a choice of the twenty people of the street, ten men and ten women, on the start screen and in the pause menu, kept with your progress ([../Characters/README.md](../Characters/README.md)). Isaac was removed.
2. **Deliveries:** the design's Job loop, with deliveries as the first of its side jobs (H-03).
   - **Offer:** a parcel waits on a street 150 to 450 m away. A column of light marks it in the street, and it is marked on the radar and the map, with the route to it.
   - **Pick up:** stop in the light, on foot or in a car.
   - **Deliver:** take the parcel to a street 600 to 1,400 m on, before the clock runs out, and stop in that light. You cannot hand it over while the police are after you.
   - **Pay:** the sooner you deliver, the more you are paid.
   - **Fail:** running out of time or going down loses the parcel. Then the next parcel is offered.
3. **Money:** cash on the HUD, earned by deliveries. Going down costs a tenth of it in treatment.
4. **Progress kept between runs:** cash, deliveries made and failed, and who you play. Kept in `progress.json` the way the settings are: versioned, written whole, with a backup.
5. **A build to play:** a macOS app of plain play (the game's target stays Windows; there is no Windows machine to run one on).
6. **Polish:** whatever the playthrough shows wrong in the first ten minutes.
7. **Credits** (owner, 13 November): a Credits page on the start screen naming every work the game uses whose licence asks for credit, and the OpenStreetMap data the city is drawn from.

## How it is built

| Part | Where | What it does |
|---|---|---|
| Deliveries | `Runtime/Core/Jobs.cs` | Offers, pickup, clock, delivery, failure; one job at a time. Offers are found on a worker thread from the city's road graph: a pickup at the middle of a road at least 40 m long, 150 to 450 m from you; a drop 600 to 1,400 m from the pickup whose road route is at least 600 m. |
| Pay and time | `Jobs.Pay`, `Jobs.TimeFor` | Pay is Tk 150 plus Tk 0.4 a metre of road, plus up to half again for the share of time left, rounded to Tk 10. Time is the road at 7 m/s (25 km/h) plus 25 s, at least a minute. |
| Stopped in the light | `Jobs.Reach`, `Jobs.Still` | Within 7 m of the light's middle, slower than 4 m/s; on foot or in the car. |
| The light | `Jobs.cs` (`Beacon`), `Shaders/DadBeacon.shader`, `Resources/Materials/Beacon.mat` | A 3.5 m ring on the road and a 70 m column over it, additive, fading going up, breathing slowly. Less faded by fog than the street, so it is seen across town. |
| Route | `Waypoint.Place(..., job: true)` in `MapView.cs` | The job's waypoint gives the route, in red, on the radar and the map. A waypoint of your own wins until you reach it or clear it; then the route goes back to the job. |
| HUD | `Hud.cs` | Cash under the clock, with what it just gained or lost; the job, its clock (red in the last 20 seconds) and its pay at the top centre; the job's red disc on the radar. |
| Menus | `Menus.cs` | The job and its route on the map and in the legend; the start screen says your cash and deliveries; Quit says what is saved. |
| Who you play | `Runtime/Core/Cast.cs`, `Player.Become` | The twenty, their names and clothes; the choice, kept in the progress; the new body where the old one stood, with health, armour and guns. Nobody in the street wears your body: the crowd leaves that walker out, and a fighter who has it gets the next person of the same sex. |
| Progress | `Runtime/Core/Progress.cs` | Cash, taka earned, deliveries made and lost, and who you play. Written to `progress.json` whenever it changes, as the settings are written: versioned, whole, with a backup. |
| Credits | `Resources/Credits.txt`, `Credits.cs`, `Menus.cs` | One line a work: title, maker, licence, source, what was changed. `CreditsTests` checks it against the people's credit record, the licensed models in Resources, the downloaded buildings and the project's credit list. |
| Plain play | `GameRoot.cs` | Reads the progress and adds the jobs. Benches and shots get neither. |

## How it plays (design notes from the tests)

- **Most trips want the car.** The clock allows the road at 7 m/s (25 km/h) plus 25 s. Running is 4.2 m/s, so on foot only the shorter drops can be made in time.
- **The police hold the hand-over.** With a wanted level you can reach the drop but not hand over; lose them first.
- **Going down** loses a carried parcel and a tenth of your cash.

## Owner decisions this does not take

These stay open:
- first or third person;
- the world past the Farmgate box;
- LODs;
- pushing to GitHub.

Nothing here buys, downloads or signs up for anything.
