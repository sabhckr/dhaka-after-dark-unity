# DHAKA: AFTER DARK announcement trailer: cut v1 report

Date: 2026-11-04. Status: **first watchable cut**. It is not a release candidate (see section 6).

The owner's instruction of 2026-11-03 ("TRAILER FIRST. DEADLINE: 7 NOVEMBER") asked for an edited trailer now, built from the existing renders, with artistic additions clearly marked and no gameplay claimed. This is that cut. The Stage 1 audit ([TRAILER_STAGE1_AUDIT.md](TRAILER_STAGE1_AUDIT.md)) still describes what the game itself can show.

## 1. Deliverables

Media files are local only (gitignored, about 1.3 GB) and rebuild from the tools and data in git (section 7).

| File | What it is |
|---|---|
| `Exports/DAD_Trailer_v1_1080p.mp4` | Delivery: 1920×1080, 24 fps, H.264 about 9.5 Mbit/s, AAC 48 kHz stereo 258 kbit/s, 146 MB |
| `Exports/work/DAD_Trailer_v1_master_PCM.mov` | Mezzanine: H.264 30 Mbit/s + 48 kHz PCM |
| `Audio/Mix_v1/DAD_Trailer_v1_master_48k24.wav` | Master mix, 48 kHz 24-bit |
| `Audio/Mix_v1/DAD_Trailer_v1_stem_*.wav` (6) | Stems (float WAV, at master gain, before the limiter): drones/pads, percussion, bass, synths/arps, swells/hits, Dhaka SFX |
| `Audio/Mix_v1/loudness.json` | Meter readings, in git |
| `Reports/evidence/v1-cut-contact-sheet.jpg` | 29 frames decoded from the delivered MP4, in git |

## 2. Honesty measures

- **Opening note** (0.6–6.2 s, top bar): "Concept visualisation built from in-development DHAKA: AFTER DARK renders. Lighting, neon, rain and reflections are artistic additions, not gameplay."
- **Standing label** on every frame that carries additions (top bar): "CONCEPT VISUALISATION FROM IN-DEVELOPMENT GAME RENDERS · NIGHT LIGHTING, NEON, RAIN AND REFLECTIONS ADDED · NOT GAMEPLAY".
- **Closing note** (117.4 s): "Concept visualisation from in-development renders. Not gameplay."
- The four day shots (8.5–20 s) are genuine renders, colour graded only, and carry no label.
- No HUD, characters, vehicles, weapons or mission text appear on screen. Motion is camera moves over stills, never simulated play.
- **Owner check:** the sound design (horns, cycle-rickshaw bells, a CNG, a motorbike, footsteps, a siren, wordless radio static) suggests street life the game does not render yet. It sits in its own SFX stem, so a stricter mix can drop those elements without touching the music.

## 3. Images (addendum section 8)

- **Inspected:** all 36 goldens in `Resources/Views` (IMAGE_REGISTER.csv) and the 3 earlier look tests.
- **Edited:** 33 treated stills in `Graphics/Treated_v1` (recipes in `Editing/recipes_v1.json`, tool `Tools/trailer/treat.swift`). The source goldens are only read, never written.
  - *Night mode* (day-for-night, 22 shots): the **sky is replaced** with a generated night sky (gradient, clouds, stars). That is the one reconstruction, and it is reported here as one. Buildings, roads and structures are the render's own pixels, regraded.
  - *Keep mode* (3 shots: night street, night sky, dusk): the render's own night or dusk, with additions on top.
  - *Additions* in both modes: lit windows (detected window shapes), glowing signs on the existing coloured board panels (no text invented, since the boards are blank: K-41), wet-road reflections, haze and bloom.
  - *Day mode* (7 shots): grade only.
- **Weak or incomplete:** n-poles, n-junction and n-kawran-bazar come out dim. n-junction stays in at 45–50 s as a dark beat. n-redbrick (grey and washed out) was taken out of the montage. Every still is 1280×720 upscaled to 1080p, so a 4K version would only be an upscale (section 6).

## 4. Footage, edit and sound

- **Footage captured:** none new. No Unity capture was run, because D-06c owns the shared Unity files and no competing build was allowed. All motion is moves, fades, rain, shake and flicker over the treated stills (`Editing/edl_v1.json`, renderer `Tools/trailer/cut.swift`).
- **Edit (exactly 120.000 s, 2,880 frames):**

| Time | Stage | Content |
|---|---|---|
| 0–8.5 | curiosity | Moon, dissolve to the lit city from above; "ঢাকা কখনো ঘুমায় না..." |
| 8.5–25 | curiosity | Day renders (genuine); "দিনের আলোয় এই শহরকে চেনা যায়।"; card ONE CITY. |
| 20–45 | unease | Sunset, dusk, night falls; rain begins; card A THOUSAND STORIES. |
| 45–67 | fear | Flickering junction, neon street, tremolo cluster, heartbeat; hard cut to black at 67 |
| 67–70 | silence | Card EVERY STREET HAS A SECRET.; "আর কেউ কেউ..." over drips and heartbeat |
| 70–99 | excitement | Drop at 70, 120 BPM, cuts on the beat, montage 95–99 in half-second cuts |
| 99–106 | reveal | Card NO WAY BACK., then the question "তুমি কি সেই মূল্য দিতে প্রস্তুত?" |
| 106.5–120 | desire | Title DHAKA: AFTER DARK, hook line, AXIOM, credit, closing note |

- **Narration:** typographic only. The Bangla lines are set as release-style subtitles in the lower letterbox bar (Tiro Bangla, OFL). **No voice is recorded or synthesised in the deliverable.** A human Bangla voice recording is still needed (section 6). The addendum's hook line is used at 109.4 and 112.7 s.
- **Sound** (`Tools/trailer/audio.mjs`): everything is synthesised in that file, with no samples and no third-party music.
  - The score is in D minor with a flattened-second colour, and the night motif runs D–E♭–D–C–A. It moves from pads through a tremolo fear cluster and a 120 BPM drive to a choir-style title and a three-chime AXIOM signature.
  - The Dhaka sound design covers the city bed, dual-tone horns, cycle-rickshaw bells, a CNG two-stroke putter, 100 Hz mains buzz, rain, thunder, a distant siren, footsteps on a wet road, sign arcing, wordless radio static, motorbike passes and a neon tube striking under the title.

## 5. QC

| Check | Result |
|---|---|
| Duration, frames | 120.0000 s video and audio in both files; 2,880 frames at 24 fps |
| Master WAV | −14.02 LUFS integrated, −1.50 dBTP, LRA 10.2 LU, max short-term −9.5 LUFS |
| Delivered AAC (decoded with `cut pcm`, re-metered) | −14.06 LUFS, **−1.16 dBTP** (under −1 dBTP after encoding) |
| Limiter | at most 5.1 dB reduction, on hits only |
| Meter validation | EBU Tech 3341 cases 1–4 pass (±0.1 LU); true peak passes for a 12 kHz sine at 45° (−0.06 dBTP for 0 dBTP true) and for 997 Hz at −6 dBFS |
| Visual | Frames decoded from the delivered MP4 reviewed (evidence sheet). Fixes made: opening reworked (the sky-only hold became a dissolve to the city, with the first line moved onto it), a grey montage frame swapped, label enlarged to 19 px for phone legibility |

## 6. Open items before release

1. **Release date on the credit card.** "FIRST STORY RELEASE · 16 DECEMBER 2026" is the owner's wording. The roadmap does not support it yet (audit B11). The owner should confirm it before anything is published.
2. **Human Bangla voice-over.** It needs to be recorded to the script and timings above. No voice imitation, and no TTS in a release.
3. **Story review** (audit D5): the narration and cards assume the design's crime-drama tone, which the owner has not reviewed.
4. **Genuine footage:** D-13 night lighting, then the capture tool (audit D2), would replace treated stills with real motion.
5. **Not done yet:** 4K (only meaningful with new captures) and a separate end card. The social cut and thumbnail are done (section 8).
6. **Not published or uploaded anywhere.** That needs the owner's approval.

## 7. Rebuild

```bash
# from the repo root; build the Swift tools once
SDK=/Library/Developer/CommandLineTools/SDKs/MacOSX26.sdk
swiftc -O -swift-version 5 -sdk $SDK -target arm64-apple-macosx14.0 Tools/trailer/treat.swift -o /tmp/treat
swiftc -O -swift-version 5 -sdk $SDK -target arm64-apple-macosx14.0 Tools/trailer/cut.swift -o /tmp/cut
/tmp/treat Production/Trailer_2026/Editing/recipes_v1.json Production/Trailer_2026/Graphics/Treated_v1
/tmp/cut render Production/Trailer_2026/Editing/edl_v1.json Production/Trailer_2026/Exports/work/v1_picture.mov   # about 30 s
node Tools/trailer/audio.mjs selftest
node Tools/trailer/audio.mjs render Production/Trailer_2026/Audio/Mix_v1                                         # about 20 s
/tmp/cut mux Production/Trailer_2026/Exports/work/v1_picture.mov Production/Trailer_2026/Audio/Mix_v1/DAD_Trailer_v1_master_48k24.wav Production/Trailer_2026/Exports/DAD_Trailer_v1_1080p.mp4
```

## 8. Social cut and thumbnail (2026-11-04)

| File | What it is |
|---|---|
| `Exports/DAD_Trailer_social_v1_9x16.mp4` | Vertical cut for Reels, Shorts and TikTok: 1080×1920, 24 fps, 30.000 s (720 frames), H.264 about 9.5 Mbit/s, AAC 258 kbit/s, 37 MB. Local only. |
| `Audio/Social_v1/DAD_Trailer_social_v1_48k24.wav` | Its mix: −14.00 LUFS, −4.49 dBTP. The decoded AAC reads −14.05 LUFS and −4.14 dBTP. |
| `Thumbnails/DAD_Thumbnail_v1_1280x720.jpg` | YouTube thumbnail (337 KB). The 1920×1080 PNG is kept locally. In git. |
| `Thumbnails/DAD_Social_cover_v1_1080x1920.jpg` | Vertical cover frame (the title), taken from the social MP4. In git. |

- **Edit** (`Editing/edl_social_v1.json`):
  - 0–5 s: hook on the neon shopfront, then the flickering divider.
  - 5–8 s: hard cut to black for EVERY STREET HAS A SECRET.
  - 8–17 s: the drop and the rain-street drive.
  - 17–21 s: half-second montage.
  - 21–23 s: NO WAY BACK.
  - 23–28 s: title, with the question line.
  - 28–30 s: AXIOM, credit and closing note.
- **Picture:** the 16:9 stills are cropped to 9:16 with chosen framings. Text sits in the safe area (narration 470 px above the bottom; label and notes 190 px below the top), so the platform UI does not cover it. The concept label stays on every frame.
- **Sound:** cut from the trailer master on bar lines of the 120 BPM grid (`node audio.mjs splice`). It uses master 62–78 s, 94–101 s, 106.2–111.2 s and 116–118 s, with equal-power crossfades, then re-mastered. Every picture hit lands on the music hit it had in the long cut.
- **Thumbnail** (`Editing/edl_thumbnail_v1.json`): the neon shopfront in rain, the title, the tagline, and "CONCEPT VISUALISATION · NOT GAMEPLAY" across the top.
- **Same owner checks as the long cut:** the release date on the social credit, and publishing approval.

```bash
/tmp/cut render Production/Trailer_2026/Editing/edl_social_v1.json Production/Trailer_2026/Exports/work/social_v1_picture.mov
node Tools/trailer/audio.mjs splice Production/Trailer_2026/Editing/edl_social_v1.json Production/Trailer_2026/Audio/Mix_v1/DAD_Trailer_v1_master_48k24.wav Production/Trailer_2026/Audio/Social_v1/DAD_Trailer_social_v1_48k24.wav
/tmp/cut mux Production/Trailer_2026/Exports/work/social_v1_picture.mov Production/Trailer_2026/Audio/Social_v1/DAD_Trailer_social_v1_48k24.wav Production/Trailer_2026/Exports/DAD_Trailer_social_v1_9x16.mp4
/tmp/cut still Production/Trailer_2026/Editing/edl_thumbnail_v1.json 0.5 /tmp/thumb.png
sips -s format jpeg -s formatOptions 88 -z 720 1280 /tmp/thumb.png --out Production/Trailer_2026/Thumbnails/DAD_Thumbnail_v1_1280x720.jpg
```
