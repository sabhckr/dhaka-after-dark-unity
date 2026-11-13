# DHAKA: AFTER DARK — Official World Reveal Trailer, cut v1 (7 November 2026)

Deliverable: `Production/Trailer_2026/Exports/DAD_WorldReveal_v1_1080p.mp4` (copy at `~/Movies/DHAKA_AFTER_DARK_WORLD_REVEAL_TRAILER.mp4`).
Source: `~/Movies/DHAKA_AFTER_DARK_CINEMATIC_SHOWCASE.mp4` (120.000 s, 1920x1080, 30 fps, H.264, no audio), read only; unchanged
(size 355,020,462 bytes, modified 7 Nov 22:16, SHA-256 starts f30a8ca6745727a4).

## Export

| | |
|---|---|
| Duration | 85.000 s |
| Picture | 1920x1080, 30 fps, H.264 High, ~21.5 Mbit/s, Rec.709 tags, 2.39:1 letterbox inside the 16:9 frame |
| Sound | AAC 48 kHz stereo 318 kbit/s |
| Loudness of the delivered AAC | −14.06 LUFS integrated, −1.26 dBTP, max short-term −10.5 LUFS, LRA 8 LU |
| Master WAV | −14.05 LUFS, −1.50 dBTP, limiter max 4.0 dB |
| File size | 231,890,779 bytes |

## What was executed, and with what

The project has no video editor of its own in this session (no ffmpeg, no NLE). The whole trailer was produced with code written for it,
using only macOS frameworks and Node:

- `Tools/trailer/reveal.swift` — the editor: decodes the recording clip by clip (AVFoundation), trims, reorders, fades, applies the
  per-clip and global grade (Core Image), sets the typography (Core Text), writes the picture (H.264), muxes the sound (AAC) and
  provides QC commands (probe, decoded PCM, frame grabs).
- `Production/Trailer_2026/Editing/edl_reveal_v1.json` — the editable timeline: 25 clips, 16 text events, grade settings.
- `Tools/trailer/reveal_audio.mjs` — the score, sound design and master, synthesised from the project's own trailer v1 synthesiser
  (no samples, no third-party music, no voices). Stems: drones/pads, percussion, bass, synths/arps, swells/hits, Dhaka sound design.
- `Production/Trailer_2026/Reports/WORLD_REVEAL_V1_SHOT_LOG.csv` — every clip with source and timeline timestamps.

Rebuild: compile `reveal.swift` (see its header), then `reveal render edl_reveal_v1.json work/picture.mov`,
`node reveal_audio.mjs render Audio/Reveal_v1`, `reveal mux picture.mov master.wav out.mp4`. Picture renders in ~18 s, sound in ~12 s.

## Structure (timeline seconds)

| Act | Time | Picture | Type |
|---|---|---|---|
| Mystery | 0.0–8.4 | black | AXIOM PRESENTS · A CITY… |
| First reveal | 8.4–16.8 | hazy noon aerial over the whole city (source 7.0–15.4) | …THAT NEVER SLEEPS. |
| Scale | 16.8–24.0 | glass towers ×2, low-rise districts | HUNDREDS OF STORIES. |
| Pulse | 24.0–37.8 | street drive under the metro viaduct, four cuts on the 100 BPM grid | ONE CITY. |
| Identity | 38.4–49.8 | dusk street / waterfront / night ship, each separated by 0.6 s of black | THE GANGSTER? · THE SAVIOUR? · OR THE DEVIL HIMSELF? |
| Acceleration | 50.4–66.0 | 13 shots, 2.4 s down to 0.6 s, day → dusk → night | YOUR CITY. · YOUR STORY. · YOUR CHOICES. |
| Drop | 66.0–67.2 | black, near silence | |
| Title | 67.2–77.0 | title on black, night city fades up behind it | DHAKA: AFTER DARK · A CITY THAT NEVER SLEEPS. |
| Release card | 77.4–84.0 | black | MADE BY ISHAK HASAN SABBiR · AXIOM · FIRST RELEASE — DECEMBER 16, 2026 |

Music: D minor, near-silence → cold air and sub drone → low strings at the reveal → Bb/Gm/Eb pads at the cuts → 100 BPM pulse
(heartbeat, kick, ticks, hats, riser) with a hard stop at 37.8 → three identity hits with silences → full groove with chord-per-cut
and a snare roll into the drop at 66.0 → title impact (boom, brass, crash, metal) with a formant "choir" and the night motif →
the AXIOM chimes at 77.4 → fade to silence. Sound design: city bed under the day shots, horns, CNG passes, a rickshaw bell, a
motorbike pass, a distant siren on the street drive; wind on the water; mains hum and a neon strike at night; whooshes only on the
three statements and the drop. No rain or thunder: the footage is dry.

## Source-footage integrity

- Every picture is the user's recording. Nothing was generated, repainted or added to the city.
- Changes to the picture: trims, reordering, fades, a 2.39:1 crop, exposure/contrast/saturation/temperature per clip, a global
  cool grade with a gentle S-curve, fine grain, a mild vignette, typography and thin rules. No stretching, no speed changes.
- Two stills (C11 night ship, C25 night aerial for the title) are held longer than the source holds them, with a 5–6 % push added
  in the edit; the source treats its own night material the same way (stills with a slow push), so nothing new is implied.
- Skipped on purpose: source 1–7 s (carries the showcase's own baked label), 113–120 s (carries the showcase's own baked title),
  the moments where the camera passes through a facade (19.5, 21, 23.5, 25.5–28.5 s), through a rickshaw (46 s) and a bike (55 s),
  and the flat train-on-grass stills (72–76 s).
- No gameplay, features, locations or statistics are claimed. "YOUR CITY. / YOUR STORY. / YOUR CHOICES." are thematic lines.

## Typography

Avenir Next Condensed Heavy (title, identity lines, AXIOM), Avenir Next Condensed DemiBold (statements) and Avenir Next
Medium/DemiBold (presents, tagline, credit, date): one family, as installed on this Mac. Bebas Neue, Oswald, Inter and Manrope
are not installed and were not downloaded. Palette: near-white type, #C3C8CE silver labels, #84909C steel rules, #8D1824 crimson
rule only under "OR THE DEVIL HIMSELF?" and the title. Animations: editorial fades, a 0.4 s rise for reveals, a 2-frame impact
with a 3.5 % settle, a 3 % slow scale on the title, quiet fades on the card.

## QA (on the exported MP4, not the project)

- Spelling and casing checked on grabbed frames: DHAKA: AFTER DARK · A CITY THAT NEVER SLEEPS. · MADE BY ISHAK HASAN SABBiR ·
  AXIOM · FIRST RELEASE — DECEMBER 16, 2026 · THE GANGSTER? · THE SAVIOUR? · OR THE DEVIL HIMSELF?
- 85 frames (one per second) and 16 cut-boundary frames grabbed and reviewed: cuts at the listed times, blacks at 37.80–38.37,
  49.80–50.37, 66.00–68.40 (title on black from 67.20), no stray frames, no freeze, no half-rendered text.
- Audio: integrated −14.06 LUFS, true peak −1.26 dBTP after AAC; the three gaps measure −23 to −32 dB RMS (reverb tails only);
  the file ends at −38 dB RMS and fades, no hard cut. Sync: the picture and the sound are rendered from the same timeline numbers,
  and the mux keeps both at 85.000 s.
- Not done: nobody has listened to the mix on speakers or headphones, and no human has watched the file in real time; the review
  was frame-based and meter-based.

## Open items

- Creator credit: the skill's canonical line is "MADE BY ISHAK HASAN SABBiR" (lower-case i); the request wrote "ISHAK HASAN SABBIR".
  The cut follows the skill. Changing it is one line in the edit list and a 30 s re-render.
- The release date is stated as supplied ("FIRST RELEASE — DECEMBER 16, 2026"); the earlier note that the date was still open (B11)
  is superseded by this request.
- Fonts: if Bebas Neue / Oswald / Inter are wanted, install them (OFL) and change three names in `reveal.swift`.
- Night and waterfront motion footage does not exist in the recording (the player only renders stills there), so the night act
  is built from stills with pushes; new camera paths would need a build.
- A human listen-through and a watch-through at full size.
