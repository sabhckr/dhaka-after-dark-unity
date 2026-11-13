# DHAKA: AFTER DARK, 90-second cinematic v1: quality report

Developer: Ishak Hasan Sabbir. In-engine cinematic, not gameplay.

## Delivered

| Item | Result |
|---|---|
| File | `Production/Trailer_2026/Exports/DAD_Cinematic_v1.mp4` |
| Duration | 90.0 s, video and audio tracks both measured from the file |
| Frames | 2700 at 30 fps |
| Picture | 1920×1080, H.264, about 21 Mbit/s |
| Sound | AAC stereo 48 kHz, about 320 kbit/s; master -16.02 LUFS, -1.50 dBTP |
| Language | English only, 22 subtitled lines in screenplay wording |
| Title card | DHAKA: AFTER DARK / A CITY THAT NEVER SLEEPS. / in-engine label, checked in frames taken from the finished file |

Production detail (deliverables 1 to 8 and 12) is in `CINEMATIC_V1_PRODUCTION_PACK.md`.

## How it was checked

- Whole trailer viewed as a contact sheet of every 30th frame, then every 26th frame of the action from 53 s to 88 s, with close views of flagged shots.
- File sizes of all rendered frames scanned for black or blown-out runs. The only ones left are deliberate: the opening fade, the flashbang (F2), and the cut to black on the decanter (P10).
- Frames extracted from the encoded MP4 itself at nine points including the title.
- No one has listened to the mix in this review. Loudness is measured; the balance has not been judged by ear.
- Frame pacing is by construction: each frame is rendered offline and written once, so there are no dropped or repeated frames.

## Faults found in the last review and fixed

| Shot | Fault | Fix |
|---|---|---|
| P1b-doors | Black for 24 frames: camera was behind the lift's back wall | New shot from outside; the doors slide open on Leo and Elara |
| P11-after | Elara's head out of frame | Reframed |
| P11-after | Leo's left hand 74 mm off the pistol | Same low-ready hold as the lift |
| All | Vance's face stretched into spikes, jaw drifting | Face bones return to rest every frame |
| F5, F7 | A rifle floating over the dying gunmen | The contractor model's own rifle hidden |

## Grip map

Each hold is defined per weapon in `GripMap` (`Assets/Trailer/Code/Holds.cs`), read from the owner's reference photos: thumbs-forward pistol, rifle with hand at the magazine well, forward and reverse knife. As instructed there is no transition animation: the hold is applied directly when the character points the gun.

After every render the audit measures each visible hold against the map and writes close-ups with an overlay (yellow right hand, cyan left) and `grip_audit.csv`. Run with `DAD_TRAILER_MAP=1`.

**Result on the final code: 827 of 877 measurements in tolerance, 94.3%. It is not 100%.**

| Part | In tolerance |
|---|---|
| Warehouse | 516 of 557 |
| Lift and penthouse | 311 of 320 |

Measured at the first, middle and last frame of each shot.

Still out of tolerance:

| Character | Measure | Frames out | Worst | Limit |
|---|---|---|---|---|
| Leo | Support index finger gap to pistol | 15 | 14.0 mm | 9 mm |
| Vance | Firing wrist, rifle, against photo position | 6 | 39.4 mm | 28 mm |
| Vance | Support ring finger gap | 6 | 14.0 mm | 9 mm |
| Elara | Firing wrist, rifle, against photo position | 5 | 44.8 mm | 28 mm |
| Elara | Firing middle finger joint inside the grip | 5 | 7.5 mm | 5 mm |
| Elara | Support middle finger gap | 5 | 10.2 mm | 9 mm |
| Elara | Knife, index finger gap | 3 | 12.4 mm | 12 mm |
| Elara | Knife, little finger inside the handle | 3 | 5.6 mm | 2 mm |
| Leo | Firing forearm bend, on a recoil frame | 2 | 62.2° | 62° |

Why it stops here: moving the rifle wrist to the photo position (raising it) pulls the ring and little fingers 35 mm off the grip. The two requirements conflict on these hands and this rifle shape, and contact was kept.

What the number means: it measures the match to a grip map read from photographs with perspective uncertainty, against weapons approximated by boxes. It is not a pixel match to the photos, and it does not prove the holds look perfect.

Finger length against the open-hand photo (index, ring, little as a share of the middle finger; photo .93, .91, .76): Elara .94, .95, .68; Vance .83, .93, .66; Leo .91, .94, .79. Reported only. The models are unchanged.

Evidence: `Evidence/local/gripfinal` and `Evidence/local/gripfinalp2` (local, not in the repository).

## Checklist from the brief

| Check | Status |
|---|---|
| Exactly 90 s | Pass, measured |
| English only | Pass |
| Subtitle timing | Pass by construction; timed to the shot list |
| Lip sync | Not possible: no voices exist. Mouths follow the written lines |
| Identity | Pass: each character is one model throughout |
| Facial animation | Limited: see production pack, section 12 |
| Movement | Clip-based. No clips exist for tackle, choke, slash or car entry |
| Clipping | Small finger intersections remain, listed above |
| Floating props | None found in the frames reviewed |
| Gore, ragdolls | None |
| Title readable | Pass |
| Ending | Ends on the title at exactly 90 s |
| Sound and music by ear | Not reviewed |

## Tests

EditMode: 303, 300 passed, 0 failed, 3 inconclusive. PlayMode: 108, 107 passed, 0 failed, 1 skipped (the render test, by design). The last one-line pose change to P11 came after these runs; it is in trailer-only code and compiled and rendered. Not benchmarked. Windows hardware not tested.

## Limitations

Listed in full in `CINEMATIC_V1_PRODUCTION_PACK.md`, section 12. The largest: no voices; Elara's face is a small part of her texture and her hair a solid shell; only Vance has lids and brows. "Perfect" cannot be claimed.
