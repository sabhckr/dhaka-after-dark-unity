# Player interface: controls, HUD, menus and settings

Owner's brief, 10 November 2026: the controls, settings, HUD and menus an open-world action game is expected to have, complete. They follow the conventions players of the genre already know (where things sit on the screen, which button does what, how the menus are laid out). The look is the game's own: the art direction's colours, plain type, no icons or artwork taken from another game.

Rule for every element: it shows something the game really does. Nothing is drawn for a system that does not exist yet. Where a convention needs a system that is not built, it is listed under **Not built** below, not faked.

## Controls

Keyboard and mouse, and gamepad (Xbox layout names; PlayStation pads map the same buttons). Every binding can be changed in **Settings > Controls**; changes are saved and can be reset.

### On foot

| Action | Keyboard and mouse | Gamepad |
|---|---|---|
| Move | W A S D | Left stick |
| Look | Mouse | Right stick |
| Sprint (hold) | Left Shift | Left stick click |
| Jump | Space | A |
| Crouch (toggle) | C | B |
| Enter vehicle, board, use | F | Y |
| Aim (hold) | Right mouse | Left trigger |
| Fire | Left mouse | Right trigger |
| Reload | R | X |
| Weapon wheel (hold) | Tab | Left bumper |
| Previous / next weapon | Q / E | D-pad left / right |
| Map | M | View (Back) |
| Pause | Esc or P | Menu (Start) |

### Driving

| Action | Keyboard and mouse | Gamepad |
|---|---|---|
| Accelerate | W | Right trigger |
| Brake, reverse | S | Left trigger |
| Steer | A / D | Left stick |
| Handbrake | Space | Right bumper |
| Horn | H | Left stick click |
| Look | Mouse | Right stick |
| Camera view | C | Right stick click |
| Get out | F | Y |
| Indicators left / right | Q / E | |
| Headlamps, hazards, engine | L, K, I | |
| Map | M | View (Back) |

### Menus

Arrow keys or W/S to move, A/D or left/right to change a value, Enter to choose, Esc to go back, Q/E to change tab. Gamepad: left stick or D-pad, A to choose, B or Menu to go back, bumpers to change tab. The mouse works everywhere: pointing chooses a row, a click presses it (on a value: left half back, right half on), the wheel scrolls. Hints at the bottom name the keys of the device last touched.

## HUD

Built in code on one UI Toolkit panel (`Runtime/Core/Hud.cs`, shared parts in `Ui.cs` and `MapView.cs`). Hidden under the menus, in the free camera, and when **Interface > HUD** is off.

Bottom left, as the genre has it:

- **Radar:** a rounded rectangle of the city map round you, turning with the camera (or north up); you as an arrow; north marked on its edge; your waypoint as a gold diamond, held at the edge when it is off the radar, and the route to it along the roads in gold; a delivery's pickup or drop as a red disc, also held at the edge, with its route in red; police officers who are out as blue dots. It zooms out as the car goes faster. The edge flashes red and blue while the police can see you.
- **Health and armour:** two bars under the radar. Health turns red under a quarter and comes back by itself to half after five seconds unhurt. Armour takes a shot first; nothing gives armour yet.
- **Wanted level:** up to five diamonds above the radar. They flash while an officer sees you and dim while the police search; out of sight for 12 s plus 6 s a level, the police lose you (a notice says so).

Elsewhere:

- **Clock** (top right): the time of day.
- **Cash** (top right, under the clock): taka in hand; what it just gained or lost shows beside it for three seconds.
- **Job** (top centre, while a delivery is offered or carried): what to do and on which street; while you carry a parcel, its clock (red in the last 20 seconds) and what it pays if delivered now.
- **Weapon** (top right, under the cash): the gun in hand, rounds in it (red at none) and spare; a bar while reloading; hidden with bare hands.
- **Crosshair** (centre, armed on foot): four ticks that open with the gun's spread, your movement and firing fast, closer while aiming; a hit mark when a shot strikes someone.
- **Prompt** (centre, above the bottom): what the use button does where you are (Get in, Board, Get out, Get off), with the key or button as bound, for the device last touched.
- **Vehicle** (bottom right, in a vehicle): speed in km/h or mph, gear (R, N, 1…), indicators and hazards blinking, headlamps, engine off, fuel.
- **Street and district** (bottom right): the street you are on (from the roads' names) and the district (the nearest of the five); the district's name large for four seconds when you come into a new one.
- **Notices** (top left): short messages, three at most, five seconds each (waypoint set, you have arrived, settings saved, you lost the police, you are down; a parcel waiting, taken, delivered, lost or held while you are wanted; what a treatment cost).
- **Damage:** the screen's edges flash red when you are hurt, and pulse red under 30 health.
- **Down:** at no health you fall by a death clip; "DOWN" and a countdown; six seconds later you stand again where the game began, healed, no longer wanted, with your guns. A parcel you carried is lost, and the treatment costs a tenth of your cash.

In the street, a delivery's pickup or drop is a column of red light over a ring on the road (`Shaders/DadBeacon.shader`), seen across town.

## Menus

`Runtime/Core/Menus.cs` and `WeaponWheel.cs`.

- **Start:** the title, Play (with your cash and deliveries so far), Character, Settings, Credits, Quit, and the credit. The world loads behind it and the city map is drawn meanwhile. Not shown in batch runs (tests, benches).
- **Pause** (the game and its sound stop): Resume, Map, Character, Settings, Quit to desktop (a second press confirms; cash and deliveries are saved as you go, a parcel you carry is not).
- **Character** (Start and Pause): who you play, one of the twenty people of the street, ten men and ten women ([../Characters/README.md](../Characters/README.md)). Left and right (or Enter) step through Man 1 to Man 10 and Woman 1 to Woman 10, and the body changes at once, with your health, armour and guns. The line under the menu says what they wear. Not in a car or while down. The choice is kept with your progress; the first is Man 8.
- **Credits** (Start): who made the people's clothes, the guns, the ships, the building and the map data, in sections, each with its maker and licence under its title; choosing one shows its source page and what was changed for the game; the licences and their links at the end. Read from `Resources/Credits.txt` (`Credits.cs`). Nothing to press; Escape goes back.
- **Map** (M or View, or from the pause menu; the game stops): the whole city with the five districts named; move with the stick, keys or by dragging; zoom with the triggers, = and -, or the wheel. A press sets a waypoint under the cursor (the centre on a gamepad, the pointer with a mouse) and a route is found along the roads; a press on the waypoint clears it (a delivery's own mark goes when the delivery does). Legend: you, waypoint, route, job, route to the job, police.
- **Weapon wheel** (hold Tab or left bumper, on foot): what you carry in a ring, time at a quarter; point with the mouse or right stick, let go to take it. The view does not turn and you cannot fire while it is open.
- **Settings:** six tabs; each change acts at once and the file is written when you leave the settings.

## Settings

| Tab | Settings |
|---|---|
| Display | Resolution, window mode (full screen, borderless, windowed), vertical sync, frame-rate limit |
| Graphics | Quality (Low, Medium, High: the three levels the game has), field of view |
| Audio | Master, vehicles, world, voices |
| Controls | Mouse look speed, gamepad look speed, aiming look speed, invert look, vibration, aim (hold or toggle), sprint (hold or toggle); the keys of every on-foot, driving and pause action, for keyboard and mouse or for gamepad, each rebindable (select, then press the new key; Escape keeps the old); a key used twice in the same mode is marked red; reset keys |
| Gameplay | Speed in km/h or mph; restore all defaults (keys kept) |
| Interface | HUD on or off, HUD size, radar on or off, radar turns with camera or north up, crosshair, prompts, notices |

Settings are saved to the player's own folder (`settings.json` in the persistent data path) with a version number and a backup copy (`settings.json.bak`); a file that cannot be read falls back to the backup and then to the defaults, and `GameSettings.Note` says which; values out of range are brought in. Quality is kept by GraphicsTier as before. Batch runs keep the defaults unless a test points `GameSettings.Folder` at its own folder.

Progress (cash, taka earned, deliveries made and lost) is kept the same way in `progress.json`, with `progress.json.bak` (`Runtime/Core/Progress.cs`), and written each time it changes. Deliveries: [FIRST_PLAYABLE.md](FIRST_PLAYABLE.md).

## Not built (needs systems from later phases)

- Shops and anything to spend money on except treatment, phone, story missions, radio stations, save slots (progress is one file, kept as you go): Phases G and H.
- Police on foot are the only police; no police cars, helicopters or road blocks.
- Hurting or killing crowd people: they run from gunfire but have no health.
- Subtitles: there is no dialogue to subtitle.
- Aim assist: the settings file has a field for it, but there is no aim assist, so the setting is not shown.
- Pickups: nothing gives armour, rounds or guns yet; you start with the pistol, SMG and rifle and their spare rounds.
- Hospitals: you stand again where the game began.
- Melee: bare hands do nothing.
- Gang members are not shown on the radar.
- Rebinding the sticks and the mouse's movement (they are axes); swapping two keys when one is taken (it is marked instead).
- Font: the default UI Toolkit font; the art direction's fonts are not installed (downloading one needs the owner's word).
