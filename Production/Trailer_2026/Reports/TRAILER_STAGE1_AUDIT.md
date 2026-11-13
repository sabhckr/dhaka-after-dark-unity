# Announcement trailer 2026: Stage 1 audit

**DHAKA: AFTER DARK**, AXIOM. Created by Ishak Hasan Sabbir. Trailer date 7 November 2026; first story release 16 December 2026 (as briefed).

| | |
|---|---|
| Date | 2026-11-03 |
| Repository | `~/Documents/dhaka-after-dark-unity`, main at dea36e1 when the audit started |
| Inputs | The master production brief (120 s, five acts, score, stems, SFX, Bangla voice-over, titles, masters, QC), relayed from the session that started this one. The owner's *Director's Story and Psychological Engagement Addendum* (pasted 2026-11-03) |
| Scope | Stage 1 only: audit, shot list, blockers. **No footage has been captured, no trailer edited, no music, SFX or voice-over made.** The one piece of image work is a three-frame, grade-only look test (section 8.2), done because the addendum asks for an image audit with before/after comparisons |
| Rule kept | Genuine game output only. Nothing that the game does not have (vehicles, people, rain, neon, lit windows, signs, gameplay) is shown or painted in |

## 0. Verdict

A genuine 120-second trailer can be made today. It would be an **atmosphere teaser**: the real Farmgate to Kawran Bazar streets, the Line 6 viaduct and its two stations, rooftops, shopfront shutters, and a sunset that turns into a moonlit night on the game's own clock, with every movement coming from the camera.

The brief and the addendum also ask for neon, lit streets, traffic, CNGs, crowds, a vehicle vanishing into the dark, wet reflections, readable signs and gameplay. **None of these exist in the game yet.** At night the city has no artificial light at all: lamps, windows and shops do not emit (D-13 is not built). A colour grade cannot supply them: the look test makes the night bluer, not lit (section 8.2). If we composite them in post, they are fabricated footage, which the master brief forbids.

The owner has three routes (decision D1 in section 7):

- **A. Honest teaser now.** Moonlit and empty, grade only. Fits 7 November.
- **B. Build D-13 night lighting first** (street-light pools, shop bulbs, emissive windows and signs; roadmap estimate 2 to 4 days), and ideally D-09 Bengali signage, then capture. The neon becomes real game output, but 7 November is at risk.
- **C. Paint lights and neon in post.** Not recommended: it contradicts the brief's own rule, and the page would need an "not in-game footage" label.

## 1. Footage that can be captured now

Everything below is in the streamed slice: 12 chunks, about 2 km by 1.5 km, around Farmgate and Kawran Bazar (`ChunkStreamer.Slice`). Every item has a golden view as evidence (contact sheet: `evidence/golden-contact-sheet.jpg`).

| Subject | What the game draws | Evidence |
|---|---|---|
| Ground and land use | Paved and soil ground, parks, generated PBR materials | goldens 1–36 |
| Roads | Kazi Nazrul Islam Avenue and side streets: lane markings, zebras, stop lines, kerbs, footpaths, drains | `kawran-street`, `d05-*`, `signal-junction` |
| Metro Line 6 | Viaduct deck, girders, parapets, rails and piers; the raised Farmgate and Karwan Bazar stations | `d06b-viaduct`, `m2-viaduct`, `kawran-metro`, `k37-*` |
| Buildings | All 5,642 slice buildings classified, with far-tier facades, roofs, parapets and a rooftop kit (water tanks, dishes, condensers, stair rooms) | `d06a-*`, `d06b-*` |
| Validation-area detail | 76 buildings with window bays, 108 shopfronts with shutters, awnings, blank colour boards | `m2-shopfront`, `m2-overview` |
| Street furniture | Poles, lamp heads (unlit), transformers, catenary cables, trees, dividers, footbridges | `m2-street-poles`, `m2-footbridge`, `m2-trees`, `m2-divider` |
| Sky and time | Real sun and moon positions for Dhaka on any date and time; sky, fog, stars, one drifting cloud layer; weather clear, cloudy or overcast. The clock can run, so a sunset time-lapse is genuine game output | `tod-*` (7 views) |

**What moves:** only the camera, the clock (sun, moon, light, sky) and the clouds. Nothing else in the world moves.

## 2. Cinematic shots: feasible and not feasible

**Feasible, genuine:** aerial establishing shots, pushes and dollies down the avenue under the viaduct, cranes past the station masses, rooftop drifts, lateral dollies along the shutter rows, POV walks at eye height, high "watcher" angles over junctions, whip pans, fast fly-throughs, a static sky with moon and stars, and a sunset-to-night time-lapse. Long shadows exist at dawn and dusk (sun shadows reach 150 m); at night the moon is the shadowing light.

**Feasible only with care:**

- **High wide aerials.** The edge of the slice shows as flat empty ground (`d06b-air-far`). Keep aerials inside the slice or let the fog and night hide the edge.
- **Close shots under the viaduct.** 44 of 66 piers stand in drawn traffic lanes (K-37; P-2 awaits the owner). The station height rule (K-15) and 3 of 7 footbridge stairs (K-39) are also visible close up. Frame away from them.
- **Near facade detail.** It exists only in the 76-building validation area (K-42); elsewhere facades are far-tier textures.
- **Cables and thin edges** alias, because MSAA is off (K-40). Render at 2× and downscale (section 3).
- **Dutch angles, FOV moves and handheld feel.** `CameraPath` has no roll, FOV or shake; the capture tool would add them.

**Not feasible (the game does not have them):** vehicles of any kind (cars, buses, CNGs, rickshaws, the metro train), people and crowds, a protagonist, any gameplay or HUD, street lights or lit windows, neon and lit signs, readable signs (boards are blank, K-41), rain, wet roads and reflections, smoke and steam, police, and working traffic signals.

## 3. Tools available

Each line says whether it was verified in this audit.

### Unity side

| Tool | What it does | Limits for a trailer |
|---|---|---|
| `ViewShot` + `Tools/shot.sh` (verified: 36 goldens) | One still from a JSON pose (position, look target, FOV, size, date, time, weather) once streaming settles | Single frames only; 1280×720 unless the JSON says otherwise; ARGB32 sRGB render texture, no anti-aliasing. Every view in `Resources/Views` needs a golden (`GoldenViewTests`), so trailer poses must not go there |
| `CameraPath` + `CameraPathPlayer` | Catmull-Rom position and look target over timed points | Advances in real time (`unscaledDeltaTime`), so not frame-exact; no roll, FOV or shake. `Resources/CameraPaths` are checked by `CameraPathTests` (20 m minimum, or 1.5–3 m street paths), so trailer paths must live elsewhere |
| `Tools/bench.sh` / `BenchmarkRunner` | Plays a path in a 1280×720 window | Writes timings, no images |
| `FreeCamera` (plain play, F1) | Interactive flight | Hand-flown; recording it means real-time screen capture with dropped frames. Not for final footage |
| `DayCycle` | Date, time and weather per shot; running clock | Weather changes at once (K-27); fixed exposure, no tone mapping (K-24) |
| Packages | URP 17.3 (its Volume overrides include bloom, tonemapping, colour adjustments, motion blur, film grain and depth of field); HDR on; colour grading mode LDR | **No Unity Recorder, Timeline or Cinemachine.** Each is a package download, which needs approval |

**Missing:** a deterministic frame-sequence capture.

**Proposal for Stage 2,** pending approval (decision D2): a capture component in its own assembly, switched on only by a `-dad-film <shot.json>` player flag through `RuntimeInitializeOnLoadMethod`. No existing file, scene or test changes. It would:

- step time with `Time.captureFramerate`;
- wait each frame until the streamer is idle, so nothing pops in;
- render to a 3840×2160 target, writing 4K frames or 2× supersampled 1080p frames;
- animate the clock, roll, FOV and a procedural handheld;
- optionally write a depth pass for depth-correct haze;
- read its shot files from `Production/Trailer_2026/Editing/shots/`, outside `Resources`.

It would run in `Builds/MacBench` like `shot.sh`, with the editor closed.

### Post side (macOS, no downloads)

| Tool | State |
|---|---|
| Swift 6.3 (Command Line Tools) | **Verified.** Compiles and runs with `-sdk /Library/Developer/CommandLineTools/SDKs/MacOSX26.sdk -target arm64-apple-macosx14.0`; without these flags it fails ("unable to load standard library for target arm64-apple-macosx28.0"). Used for `Tools/trailer/look.swift` |
| AVFoundation | **Verified** (preset listing only): H.264 1920×1080, HEVC 3840×2160 and HEVC with alpha. Planned for assembly (`AVMutableComposition`) and export; encoding not yet exercised |
| CoreImage | **Verified:** colour cube, bloom, Gaussian blur, colour matrix and vignette exist; blur, matrix and vignette were used in the look test |
| CoreText with the OFL Bengali fonts in `Art/Fonts` | **Verified on one line:** "এই শহরে টিকে থাকা এক কথা।" shapes correctly in all 9 fonts, and all 9 cover the Latin title and credit glyphs (`evidence/font-shaping-test.png`). Conjuncts such as ক্ষ, ন্ত and স্ত are not yet checked |
| `sips`, `afinfo`, `say` | Verified |
| `afconvert`, `avconvert` | Present, not exercised |
| Node 26.9 | Present. Planned for audio synthesis (typed arrays); not exercised |
| Command Line Tools Python 3.9 | `wave` module only: no numpy, scipy or PIL |
| Google Chrome | Present (headless HTML-to-image, as in earlier projects) |
| Not installed | ffmpeg, ffprobe, sox, lame, ImageMagick, Blender, GarageBand, Logic, Audacity, DaVinci Resolve, Final Cut, iMovie |

**Loudness:** with no ffmpeg there is no EBU R128 or true-peak meter. One option is our own ITU-R BS.1770-4 meter (K-weighting, gating, 4× oversampled true peak) in Swift or Node; it would be unvalidated until checked against a reference signal set. The other is installing ffmpeg through Homebrew, a download that needs approval (decision D8).

**Storage, estimated from the goldens' PNG sizes** (0.19–1.22 MB at 1280×720, median 0.88 MB):

- 1080p frames: about 2 MB each, so 120 s at 30 fps is about 7 GB.
- 4K frames: about 8 MB each, about 28 GB.

The disk has 49 GB free, and the project's storage budget is 25 GB. So encode each shot as it is captured, and delete the frame sequence only after the encode is checked (decision D9). These figures are to be measured in Stage 2.

## 4. Audio resources available

- **In the project:** none. There are no audio files, no AudioMixer and no audio code; Unity's audio module is enabled but unused. Radio and ambience are Phase H.
- **Libraries:** no music, SFX or sample libraries and no field recordings on this machine. No DAW is installed.
- **Voice:** macOS has a Bangla system voice, **Piya** (`bn_IN`). It renders the script; all 17 candidate lines were timed in the scratchpad (section 6.2). Its output is 22.05 kHz mono and it was not listened to, so pronunciation is unchecked, and it is not a deep, restrained voice. Apple's macOS licence limits what system voices may be used for, so **Piya is a timing guide only, never in a published trailer** (the owner should read the SLA before any other use).
- **Synthesis possible locally:** drones, pads, pulses, sub-bass, impacts, risers, whooshes, metallic hits, radio static, 50 Hz mains hum (Bangladesh's grid frequency), wind, and a synthetic horn. All are original by construction, written as code in Node or Swift.
- **Hard to synthesise convincingly:** realistic Dhaka traffic beds, CNG two-stroke engines, crowds and footsteps. Orchestral textures would sound synthetic without a sample library or players.

## 5. What must be created

1. **Capture tool and shot files** (section 3; approval D2).
2. **Footage:** about 96.5 s of camera shots plus handles, at 1080p (2× supersampled), and a 4K pass if it holds up.
3. **One approved grade** as a LUT applied to every shot (approval D3), plus a day variant for the day shots.
4. **Score:** original hybrid electronic/orchestral-style music in 5 stems (drones and pads, percussion, bass, synths and arps, hits and brass-like swells), written to the edit. Synthesised; its quality ceiling is noted in section 4.
5. **SFX:** ambience beds, hum, pulses, impacts, risers, whooshes, metal, radio texture, horn, and the AXIOM sonic signature.
6. **Voice-over:** a human Bangla voice recorded with a signed consent and release, imitating no real actor (decision D4). The owner could record it, or a voice actor could be booked. Recording needs a quiet room and a decent microphone, ideally 48 kHz / 24-bit WAV.
7. **Graphics:** act cards, the title, a Bangla subtitle option, credits, a date line, a work-in-progress disclaimer if wanted, and the AXIOM ident. All set in OFL fonts.
8. **Edit:** an EDL in JSON, assembled with AVFoundation to exactly 120.000 s.
9. **Mix and master:** stereo, 48 kHz, about −14 LUFS integrated with true peak at −1 dBTP or lower (a common streaming target). Numbers to be confirmed with the owner's platform.
10. **Exports:** 1080p H.264 master, 4K HEVC if stable, a social cut (30 s, 16:9, plus a 9:16 version rendered from the same paths in portrait rather than cropped), a thumbnail, an end card, and a QC report.

## 6. Proposed 120-second shot list

The full table is in `SHOT_LIST_v1.csv`. It has 30 shots: 25 camera shots (about 96.5 s), 1 re-cut montage and 4 graphics shots. Act cards come from the master brief; the stages and voice-over cues come from the addendum.

Status codes: **F** = needs the Stage 2 film tool (the pose or area already renders as a still); **N** = night, moonlit with no artificial light unless D-13 is built first; **E** = edit of captured footage; **G** = graphics only.

### 6.1 Outline

| Time | Stage, card | Shots | Picture (all genuine) |
|---|---|---|---|
| 0:00–0:20 | 1 Curiosity, "ONE CITY." | S01–S05 | Moon and stars, tilt down to the moonlit city. Then day: the validation area from the air, the shutter row, and the empty avenue under the viaduct |
| 0:20–0:45 | 2 Unease, "A THOUSAND STORIES." | S06–S10 | Sunset-to-night time-lapse on the game clock, dusk rooftops with long shadows, piers receding at night, a low side lane under cables, a footbridge silhouette |
| 0:45–1:10 | 3 Fear, "EVERY STREET HAS A SECRET." | S11–S16 | A watcher's rooftop angle over an empty junction, a POV walk along the shutters, a turn back to an empty avenue, a crane past the station mass, the empty viaduct deck, a black card |
| 1:10–1:42 | 4 Excitement, "NO WAY BACK." | S17–S27 | Fast dolly under the viaduct, a deck rush, rooftop skim, junction pass, station crane, fly under the footbridge, whip pan, aerial arc, a slow push to Farmgate, an 8-cut beat montage, then the card and a sudden drop |
| 1:42–2:00 | 5 Desire, title | S28–S30 | A still wide shot under the moon in silence, the title, then the AXIOM ident and credits |

The excitement is in the cutting, the camera speed and the sound, not in traffic: there is none.

### 6.2 Voice-over timing (addendum script)

Durations are from the scratch Piya renders at its normal rate. The restrained delivery the addendum asks for is estimated at 1.5–1.8 times longer.

| # | Line | Cue | Piya (s) | Restrained est. (s) | Proposed in |
|---|---|---|---|---|---|
| 1 | ঢাকা কখনো ঘুমায় না... | 0:00 | 1.91 | 2.9–3.4 | 0:00.5 |
| 2 | দিনের আলোয় এই শহরকে চেনা যায়। | 0:12 | 2.51 | 3.8–4.5 | 0:12.0 |
| 3 | কিন্তু অন্ধকার নামলে... | 0:25 | 1.66 | 2.5–3.0 | 0:25.0 |
| 4 | প্রতিটি রাস্তার গল্প বদলে যায়। | 0:35 | 2.42 | 3.6–4.4 | 0:35.0 |
| 5 | কিছু মানুষ হারিয়ে যায়। | 0:48 | 1.80 | 2.7–3.2 | 0:48.0 |
| 6 | কিছু মানুষ হারানোর ভয়ে বাঁচে। | 0:57 | 2.26 | 3.4–4.1 | 0:57.0 |
| 7 | আর কেউ কেউ... | 1:08 | 0.90 | 1.4–1.6 | 1:08.0 |
| 8 | এই শহরের নিয়ম বদলাতে আসে। | 1:15 | 2.26 | 3.4–4.1 | 1:15.0 |
| 9 | কিন্তু ক্ষমতার একটা মূল্য আছে। | 1:30 | 2.38 | 3.6–4.3 | 1:30.0 |
| 10 | তুমি কি সেই মূল্য দিতে প্রস্তুত? | 1:42 | 2.31 | 3.5–4.2 | 1:43.0 |
| — | DHAKA: AFTER DARK (spoken) | 1:50 | not timed | about 2 | 1:47.0 |
| 11–12 | এই শহরে টিকে থাকা এক কথা। / এই শহরকে নিজের করে নেওয়া আরেক। | after the title | 2.19 + 2.49 | 7.0–8.4 | 1:49.2–1:56.0 |
| — | AXIOM (spoken, optional) | 1:56 | not timed | about 1 | 1:56.5 |

All lines fit: about 39–47 s of speech in 120 s. **The ending is tight.** The addendum's 1:50 title plus the hook line plus the AXIOM ident at 1:56 only fit if the title lands at 1:46.5 and the hook is spoken at the faster end of the range. If the recorded delivery is slower, drop the spoken title, since it is on screen.

The addendum script replaces the master brief's lines: "এই শহর কখনো ঘুমায় না।" (now line 1), "প্রতিটি রাস্তার আছে একেকটি গল্প।", "প্রতিটি গল্পের আড়ালে লুকিয়ে আছে আরেকটি অন্ধকার।", "এখানে ক্ষমতা কেউ উপহার দেয় না।" and "ক্ষমতা অর্জন করতে হয়।". The owner should confirm this (D4), and a native speaker should review the final script.

## 7. Blockers and owner decisions

### Blockers

| # | Blocker | Effect on the trailer |
|---|---|---|
| B1 | No artificial night light (D-13 not built; only sun and moon lights exist, and the world shader has no emission) | No neon, lit streets, windows or shops; the night is moonlit grey (K-24) |
| B2 | No vehicles, people, trains or protagonist (Phases E and F not started; the owner said no character or vehicle work during Phase D) | No traffic, crowds, "vehicle disappearing", protagonist or gameplay |
| B3 | No rain or wet surfaces (D-12, Phase F) | No wet reflections |
| B4 | Signboards blank (K-41; D-09 not started) | No readable Bengali or English text in the world |
| B5 | No gameplay, HUD or player | The trailer must not imply any |
| B6 | No frame-sequence capture; no Recorder, Timeline or Cinemachine | Stage 2 tool needed (D2) |
| B7 | No ffmpeg; no validated loudness or true-peak meter | Own meter (unvalidated) or install ffmpeg (D8) |
| B8 | No licensed Bangla voice; Piya is a timing guide only | Human recording needed (D4) |
| B9 | No music, SFX or orchestral samples | Everything synthesised (quality ceiling), recorded, or licensed (approval) |
| B10 | Trailer date is 7 November, 4 days away | Route B (D-13 first) puts the date at risk |
| B11 | Release-date claim: "first story release 16 December 2026" | The roadmap shows Phases E–J not started (estimates: F 25–40, G 25–40, H 60–120, I 30–60, J 30–45 working days). A dated public promise needs the owner to say what ships on 16 December |
| B12 | Visible world defects in close shots: K-37 piers, K-15 stations, K-39 stairs, K-13 no ramps, K-40 cable aliasing, K-42 facade detail area, slice edge | Framing constraints (section 2) |
| B13 | Storage: 4K frames about 28 GB (estimate) against 49 GB free and the 25 GB project budget | Encode per shot; delete intermediates (D9) |
| B14 | 8 GB M1: one heavy job at a time; Unity batch runs need the editor closed | Captures run one at a time |

### Decisions for the owner

| # | Decision | Recommendation |
|---|---|---|
| D1 | Night route: A (honest teaser now), B (D-13 night lighting, then capture) or C (paint lights in post) | B if the date can move; otherwise A. Not C |
| D2 | Approve the Stage 2 capture tool (new separate assembly, flag only, no existing file changed) | Approve |
| D3 | Look: grade v1 direction, or ask for v2 | Approve the direction; tune on real 1080p night captures |
| D4 | Voice: who records; addendum script replaces the brief's lines | Owner or a booked actor, with a signed release |
| D5 | Hook line and story framing (section 8.1). The design document's story has not had the owner's review (`ROADMAP.md`, Phase A: "Done except the owner review of the design") | Owner confirms |
| D6 | End card text: release date wording; add "In-engine footage. Work in progress."? | Add the disclaimer |
| D7 | Off-screen sounds of a living city (traffic bed, horn, distant engine, footsteps) over empty pictures | Allow as distant ambience; no engine revs synced to camera moves, which would imply drivable vehicles |
| D8 | Install ffmpeg via Homebrew for metering and encode checks (download; size to be measured before asking) | Yes, if loudness compliance must be proven |
| D9 | Delete frame sequences after a verified encode (with `/usr/bin/trash`) | Yes |

## 8. Addendum integration

### 8.1 Story and psychological stages against the real footage

| Addendum element | Genuine today? | How |
|---|---|---|
| Ordinary daytime Farmgate: traffic, shops, buses, CNGs, crowds | Shops only, shut | Day shots exist, but the roads are empty. Show the empty day honestly; it reads uncanny, which suits the story |
| Protagonist (ordinary person, not a hero) | No | Implied only: second person in the VO ("তুমি") and eye-height POV. No figure, no name |
| Curiosity: unusual light | Partly | Moon and stars only |
| Curiosity: mysterious street; empty road | Yes | Side lane, avenue under the viaduct |
| Curiosity: vehicle disappearing into darkness | No | Not possible |
| Curiosity: empty road surrounded by a living city | Half | The road is empty; the city is not living |
| Unease: distant silhouettes | Architecture only | Piers, stations, footbridge, rooftop kit; no human silhouettes |
| Unease: long shadows | Yes | Dawn and dusk sun; moon at night |
| Unease: uncertain reflections | No | No wet or reflective surfaces; the reflection holds only the sky (K-26) |
| Unease: unusual framing; visual isolation | Yes | Capture tool adds roll and low angles |
| Fear: being watched or followed | By camera only | Rooftop watcher angle, over-the-shoulder turn, POV walk; no figure is ever shown |
| Fear: distant movement | No | Only clouds move |
| Fear: low-frequency sound, silence | Yes | Sound design |
| Excitement: traffic, city movement, gameplay | No | Camera speed, cutting and sound instead |
| Excitement: camera tracking, environmental reveals | Yes | S17–S25 |
| Desire: slow down, title, one question | Yes | S28–S30. "WHAT WOULD YOU BECOME IN THIS CITY?" is better on the end card or in social copy; the 1:56–2:00 window is full |

**Hook line.** "এই শহরে টিকে থাকা এক কথা। এই শহরকে নিজের করে নেওয়া আরেক।" is consistent with the design document's arc, if that arc is the approved direction (D5):

- Rafi goes from "get back what's ours" to deciding what kind of power he wants (`Docs/RESEARCH/ORIGINAL_GAME_DESIGN.md`).
- One ending, "The new Line", has him take the terminals for himself.

The same document calls the tone "an urban crime drama with warmth and humour" in a geographically grounded Dhaka. "Cyberpunk" can be a grade here, but the game has no futuristic elements, so the trailer should not suggest a sci-fi setting.

### 8.2 Existing rendered images: inspection and look test

- **Unity renders.** There are 36 unique Unity renders (the goldens). Each is classified in `IMAGE_REGISTER.csv`, with its deficiencies and treatment.
- **Not inspected one by one:** 512 historical Unity evidence shots (earlier versions of the same 36 poses, in the gitignored `Evidence/local`) and 8 images in `Docs` (copies of K-37 evidence views, plus the material sheet).
- **Prototype captures.** 9 views from the earlier three.js prototype, each in day, night and rain (27 images, 487 files with repeats), sit in `~/Documents/dhaka-after-dark-p1/out/look` (one checked). **They are excluded:** they come from a different engine and build, with HUD overlays, and would misrepresent the Unity game.

| Class | Count | Meaning | Images |
|---|---|---|---|
| A | 5 | Right time of day already; grade directly | tod-night-street, tod-night, tod-dusk (graded), tod-sunset, tod-dawn |
| A-day | 8 | Day establishing; day variant of the grade | farmgate, farmgate-mid, kawran-bazar, m2-overview, d06a-far, d06b-air, tod-noon, tod-overcast |
| B | 14 | Re-render the **same pose** at night and at 1080p/4K: same composition and geometry, no image reconstruction | d05-bus-stop, d05-drain-lane, d06a-street, d06b-roof-close, d06b-viaduct, kawran-metro, kawran-street, m2-divider, m2-footbridge, m2-shopfront, m2-street-poles, m2-trees, m2-viaduct, signal-junction |
| C | 3 | Debug shading; not pictures of the game | farmgate-debug, farmgate-grid, d06a-families |
| D | 6 | Show known defects (slice edge, K-37 piers): reframe or wait for the fix, **never repair the image** | d06b-air-far, k37-farmgate-station, k37-karwan-station, k37-kni-kerb, k37-kni-median, k37-side-street |

**Deficiencies common to all 36:**

- 1280×720 only;
- no anti-aliasing;
- no artificial light at night;
- no vehicles or people;
- blank boards;
- facade detail only in the validation area.

Because every render comes from a JSON pose, "preserve the original composition" is best met by re-rendering that pose at the target size and time. Upscaling or painting over a 720p still loses that.

**Look test v1, grade only** (`Tools/trailer/look.swift`, macOS frameworks only). Each originals file is a byte-identical copy of its golden, and the goldens are untouched. The grade applies:

- a hue-preserving S-curve and an exposure lift;
- 20 % desaturation;
- split toning: blue shadows, violet mids, cyan highlights;
- a cool bloom only from pixels that are already bright;
- a vignette and ±1.5/255 grain.

No geometry, text or light was added, so every edge in the output is an edge the game rendered. The first pass darkened the image and was redone with an exposure lift; only the second pass is kept.

| Image | Original | Graded | Side by side | Reviewed result |
|---|---|---|---|---|
| tod-night-street | `Graphics/LookTest_v1/original/tod-night-street.png` | `…/graded/tod-night-street-look-v1.png` | `…/compare/tod-night-street-compare.png` | Bluer and moodier, geometry intact, but still flat: there is no light to separate, and the building colours sink |
| tod-night | `…/original/tod-night.png` | `…/graded/tod-night-look-v1.png` | `…/compare/tod-night-compare.png` | Strong moon and sky; the city below is nearly black. Needs lit windows (D-13) to read as a city |
| tod-dusk | `…/original/tod-dusk.png` | `…/graded/tod-dusk-look-v1.png` | `…/compare/tod-dusk-compare.png` | The best of the three: violet dusk sky, good depth from the game's own fog |

**Conclusion:** the grade gives the films a consistent colour, but it cannot give "cinematic separation", cyan highlights or neon on these renders, because nothing in the night scene emits light. Each addendum treatment item:

- **Colour grade:** feasible (done).
- **Enhance existing light sources:** only the moon exists.
- **Haze:** the game's own fog, or a depth pass from the capture tool.
- **Wet reflections:** not applicable; the game has no wet conditions.
- **Neon:** not without D-13 and D-09, or fabrication.
- **Signage:** blank, so there is no text to preserve or corrupt.
- **Architecture:** preserved.
- **Vehicles:** none to preserve.
- **Composition:** re-render the poses.
- **Consistency:** one LUT after approval.

### 8.3 Sound progression

All of the addendum's sound beats can be made originally, except the realism of real Dhaka recordings:

- **Opening:** distant traffic as an off-screen bed, a single horn, electrical buzz, drone, heartbeat percussion.
- **Unease:** distant footsteps and passing engines, kept as ambience (D7); metal, distorted radio, low tension.
- **Fear:** pulled-back music, silence, a subtle pulse, stereo-placed distant sounds.
- **Excitement:** percussion, bass, rising synths, orchestral-style tension and impacts. Engine acceleration only as distant ambience, not synced to camera moves (D7).
- **Final reveal:** drop, silence, title impact, and the AXIOM signature, designed as a short original motif.

**The most authentic route** for traffic, horns and CNGs is the owner recording Farmgate on a phone. That gives original, owned recordings; keep any voices unintelligible.

## 9. Status (addendum section 8 format)

| Item | State on 2026-11-03 |
|---|---|
| Existing images inspected | 36 unique Unity renders (register and contact sheet); 1 prototype capture checked and the set excluded; 512 historical evidence shots and 8 doc images not individually inspected |
| Images successfully edited | 3: grade-only look test v1 (tod-night-street, tod-night, tod-dusk). Originals kept; reviewed by me; **not yet approved by the owner** |
| Images still incomplete | 33. They wait for the look approval (D3) and the night route (D1): 14 to re-render at night, 8 day, 2 still to grade, 3 unusable debug views, 6 defect views to reframe |
| Original footage captured | None (Stage 2, capture tool not built) |
| Audio completed | None |
| Voice-over completed | None. Scratch timing renders only, in the session scratchpad, not in the project |
| Trailer editing progress | Shot list v1 and VO timing only |
| Remaining blockers | B1–B14 (section 7) |

## 10. Proposed Stage 2, after approval

1. **Capture tool.** Build it and prove it on one shot (S06, the time-lapse): frame-exact, no pop-in, measured seconds per frame and MB per frame.
2. **Look.** Lock the grade on real 1080p night captures; write a LUT.
3. **Capture.** Shoot S01–S28 at 1080p (2× supersampled); 4K pass if time and storage allow. Encode each shot, check it, then remove its frames (D9).
4. **Audio.** Build the synthesis kit and score to the locked picture, with 5 stems and SFX. Meter with the validated meter, or ffmpeg (D8).
5. **Voice-over.** Record the owner or actor; use the scratch guide meanwhile for timing only.
6. **Titles and assembly.** Titles, AVFoundation assembly to 120.000 s, exports, social cut, thumbnail, end card.
7. **QC report.** Duration and frame count; loudness and true peak; black and flash frames; a native check of every Bangla string; and an honesty checklist per shot listing each shot's pose file and capture log.

## Evidence

- `Reports/evidence/golden-contact-sheet.jpg`: all 36 renders, numbered as in the register.
- `Reports/evidence/font-shaping-test.png`: Bangla and Latin in the 9 OFL fonts.
- `Graphics/LookTest_v1/{original,graded,compare}/`: the look test.
- `Tools/trailer/look.swift`: the contact-sheet and grade tool. Build line in its header.
