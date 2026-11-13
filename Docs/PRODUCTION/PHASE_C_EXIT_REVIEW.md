# Phase C exit review (2026-11-02)

Phase C, the Farmgate / Kawran Bazar vertical slice, is reviewed against its four exit criteria from [ROADMAP.md](ROADMAP.md). The evidence comes from the code as committed with this review.

**Closed on 2026-11-02 by the owner's decision** (see the re-check at the end). The verdict at the time of the review was: **not closed yet.** Three criteria are met. The fourth, "no main-thread spike over 4 ms", is met for the streaming work itself but not for the whole frame. The cause is garbage collection from the chunk builders, plus one frame that was not caught. Phase C closes after C-07, the builder allocation pass, if a re-run of the same check passes. C-07 is done; its re-check is at the end of this page, and closing waits on the owner's reading of the rule.

## Evidence used

| What | Where | Result |
|---|---|---|
| EditMode tests | `Tools/run-tests.sh EditMode` | **41 / 41** |
| PlayMode tests, run twice | `Tools/run-tests.sh PlayMode` | **21 / 21** both times; the slice walk's busiest apply frame was 1.95 and 0.72 ms, with no apply step running into a garbage collection |
| Windows Mono build | `ProjectTools.BuildWindows` | Succeeded, 0 errors, 0 C# warnings; Unity reports 115.4 MB. Not run (K-02) |
| Bench `slice-crossing-air`, 3 runs, macOS development player | `Tools/bench.sh slice-crossing-air` | `Evidence/local/bench-slice-crossing-air-20261102-111411`, `-111520`, `-111627` |
| Bench with a profiler capture of frames 3–2002 | `Tools/bench.sh slice-crossing-air --capture`, then `Tools/capture-report.sh` | `-112220` |
| Earlier captures in this review, before the warm-up fix was final | same tools | `-104503` (frames 1003–3002), `-104911` (start, before the fix), `-105346` (start), `-105521` (from frame 1600) |

`Evidence/local` is not in git. The figures below are copied from those folders.

## Criteria

### 1. The 12 chunks stream in and out with no main-thread spike over 4 ms: partly met

**The streaming work: met.**

| Run | Busiest apply frame | Apply steps with a GC (slowest) | Main thread max after set-up | Where that max comes from |
|---|---|---|---|---|
| 1 | 0.87 ms | 0 | **9.85 ms**, frame 62 | Not attributed: no apply, no collection, about 1 s in; this run had no capture |
| 2 | 1.16 ms | 1 (0.57 ms) | 3.57 ms | — |
| 3 | 2.22 ms | 1 (1.94 ms) | **5.73 ms**, frame 310 | First apply batch. The overlay upload step ran into a collection and took 1.94 ms. The rest is not attributed |
| 4, profiled | 0.92 ms | 0 | 4.04 and 4.24 ms; 4.81 ms when the capture stops | Ordinary render work (SSAO 0.65 ms, render-graph recording 0.50 ms) plus profiler overhead. Streaming planning is 0.29 ms of the 4.24 ms frame |

Notes:

- The busiest apply frame in all four runs is 0.87–2.22 ms. A step that meets a garbage collection is the slowest, at up to 1.94 ms.
- In the profiled run, the streaming markers (`Dad.Streaming.Plan` and `Apply`) total at most 1.65 ms in any frame. The earlier start-of-path capture `-105346` gives 1.47 ms.
- "After set-up" leaves out frames 0–4. Those frames are the bench's own set-up, and the frame-timing columns lag by a few frames. They read 3.9–17.1 ms and come before any chunk is requested.

**The whole frame: not met.** Two kinds of frame go over 4 ms in unprofiled runs.

- **Garbage collection from the builders.**
  - The workers allocate about 22.4 MB a run (29–30 collections).
  - Collections bunch up while a batch of chunks builds: 2–7 per frame in the frames just before each of the four apply batches (frames 307–309, 602–604, 885–888 and 1216–1219).
  - Most such frames stay at 1.1–2.7 ms. When a collection lands inside an apply step, the step slows (1.94 ms here). The whole frame can then pass 4 ms (5.73 ms in run 3).
  - The 3000-frame capture `-104503` caught one collection directly: `GC.Collect` took 2.21 ms inside `Dad.Streaming.Apply` (frame 1220, 5.00 ms busy).
  - This is caused by streaming, so it counts against the rule.
- **A 9.85 ms frame with no visible cause.** It came about 1 s in, in run 1, with no apply and no collection. None of the five captures has a frame like it. It stays open under K-18.

**Fixed during the review:**

- **First-call compilation (Mono JIT).** The Mono player compiles each method the first time it runs. The capture before the fix (`-104911`) showed `Mono.JIT` at 3.46 ms in the first apply batch (6.42 ms busy). The 3000-frame capture showed 2.08 ms at the first unload (5.15 ms busy).
  - The warm-up now compiles 1,139 streaming and builder methods ahead (`ChunkStreamer.PrepareCode`).
  - In the start-of-path capture after the fix, the first apply batch is 3.08 ms busy, with 0.70 ms of JIT left. The slowest profiled frame is now 4.24 ms.
  - Warm-up before play grew from 32 ms to 82–86 ms.
- **A crash from that fix.** Its first version also asked Mono to compile array, delegate and runtime methods. That printed "Mono runtime and class libraries are out of sync" 180 times, and one run ended early. Those method kinds are now skipped: 0 such messages in the 4 final runs, all of which finished.

**Not streaming (K-11):**

- The long frames are stalls in presentation. The capture from frame 1600 (`-105521`) caught one of 575.5 ms.
  - The main thread was busy for 3.23 ms of it.
  - For the rest, it waited in `WaitForTargetFPS` (572 ms), while the render thread waited in `Semaphore.WaitForSignal` (575 ms).
  - Neither thread was working. The frame was held up outside the game, between Unity, Metal and the window compositor.
- In the final runs, the CPU frame time reached 610.6 ms (run 1) and 590.4 ms (run 2).
- `frameMs` can read 16.7 ms through such a stall. The bench now also counts frames by CPU frame time: 1, 2, 1 and 1 over 33.3 ms.

### 2. Roads and buildings align with the data, spot checks ≤ 1 cm: met

- **Buildings (C-04):** 20 slice buildings, spread evenly by id over 5,893. Every corner is within 0.02 mm, and base and top are exact.
- **Roads (new, `RoadBuilderTests.TwentySliceRoadsSitOnTheirData`):** 20 roads, spread by id over 472 candidates in the 12 chunks. Each test point is on the longest straight segment of 12 m or more, away from chunk edges.
  - The centre is covered at the road's height.
  - Each side is covered 1 cm inside the half width: 40 of 40.
  - Each side is uncovered 1 cm outside: 36 of 40. The other 4 meet another road at the same height, within its half width plus 2 cm.
  - So every edge checked on both sides sits within 1 cm of its data width. The 4 sides that meet another road are checked on the inside only.
  - Checked to fail: widening each side by 2 cm fails the test.

### 3. Metro stations elevated: met (C-04)

- The 16 Line 6 stations stand on the layer rule.
- Farmgate's two pieces and Kawran Bazar's stand at 16–28 m, clear of the trunk roads under them. Kazi Nazrul Islam Avenue, found by name, runs under Kawran Bazar's (`BuildingBuilderTests`, golden view `kawran-metro`).
- K-15 stays open: the track is drawn 3 m above the station roofs, and the stations have no piers.

### 4. Free camera and debug views: met (C-05)

- F1 is the free camera; F2 the chunk grid and ids; F3 the road graph; F4 the debug lines.
- Checked by `FreeCameraTests`, `ChunkDebugTests` and the golden view `farmgate-debug`.

## What closes Phase C

**C-07, builder allocation pass.**

- Reuse the builders' lists and arrays per worker, so a run allocates far less than 22.4 MB.
- Make the main-thread part of an apply step allocate nothing.
- Add a bench figure for main-thread frames over 4 ms after set-up, so this check reads straight from `summary.json`.
- **Acceptance:** 3 unprofiled runs and 1 profiled run with no main-thread frame over 4 ms after set-up that streaming causes. Any frame left over must be attributed in a capture; a cause outside the game, as in K-11, is recorded rather than counted.

If C-07 does not get there, the choice is the owner's:

- accept the rule as measured on the streaming markers;
- or move the rule to the Windows hardware tiers (K-02).

That would be a change to an exit criterion, so it is asked, not assumed.

## Effort

- **Estimate for Phase C:** 10–15 days.
- **Actual:** C-01 and C-02 were built on 2026-10-30; C-03 to C-05 and this review on 2026-11-02. The exporter work came earlier, in Phase A. Elapsed days are not working days at one developer's pace, so they do not re-set the estimate on their own.
- Phase D's estimate (20–35 days) is kept until D-01 shows a pace. Art-heavy work is unlike the data-driven builders of Phase C.

## Re-check after C-07 (2026-11-02)

**Verdict: the streaming work passes; the whole frame still needs the owner's reading of the rule.** Phase C is not marked closed.

C-07 did what it set out to do:

- The builders reuse their working memory, and the road, building and line mesh arrays come from pools.
- Finished chunks wait while other loads are still building.
- The bench counts main-thread frames over 4 ms after set-up.
- Details are in [../PERFORMANCE/PERFORMANCE_REPORT.md](../PERFORMANCE/PERFORMANCE_REPORT.md) (C-07).

**Final code, the agreed check:**

| Run | Frames over 4 ms after set-up | Near streaming | Main-thread CPU in those frames (row ±1) | Max apply frame | Apply steps with a GC |
|---|---|---|---|---|---|
| `-124949` | 2: rows 607 (5.05 ms), 3573 (5.73 ms) | 1 (row 607) | 2.24 and 1.63 ms | 3.66 ms | 0 |
| `-125222` | 1: row 1110 (4.33 ms) | 0 | 1.98 ms | 1.17 ms | 0 |
| `-125328` | 0 | 0 | — | 1.01 ms | 0 |
| `-124414`, profiled 3–2002 | 1: row 2003 (7.95 ms), the frame the capture stops | 0 | — | 0.96 ms | 0 |

- The profiled run's capture has no frame busy over 4 ms; the busiest is 3.64 ms.
- A second capture (`-124640`, frames 1600–3599) has none either; its busiest is 3.01 ms.
- Three more unprofiled runs on the same game code, before the CPU column existed (`-124053`, `-124202`, `-124308`), had 2, 1 and 1 such frames. None was near streaming.

**What the frames left over are:**

- The three measured with the CPU column used the main thread's usual CPU time: 1.6–2.2 ms, against a p50 of 1.8–2.3 ms.
  - So for 2.3–4.1 ms of the frame the thread was not running. It was not computing anything, the game's work included.
  - The earlier five (4.29–9.69 ms) have no CPU figure.
- No garbage collection finished, and nothing was allocated, within a frame of any of the eight.
- They come at random times, with or without streaming nearby.
- None turned up in the two captures (4,000 frames).
- The 3.66 ms apply frame in `-124949` is the same thing. Each of its steps took at most 0.13 ms; the thread lost at least 1.4 ms between them.
- One of these frames (row 607) fell within 3 frames of an apply batch, so the bench marks it "near streaming". It used 1.4–2.3 ms of main-thread CPU and no collection finished near it.
- The likely cause is the operating system descheduling the thread. Background load is high on this machine (Chrome about 34% CPU, WindowServer about 43%). This is not proven: a wait inside Unity that uses no CPU would look the same.

**What the rule as written needs:** "Any frame left over must be attributed in a capture." These frames are attributed by the thread's CPU clock instead, and no capture has caught one. Reading that as enough would change an exit criterion, so it is asked, not assumed.

**The owner's choice:**

1. Close Phase C, reading the 4 ms rule on the streaming markers and the main thread's own CPU time. Frames in which the main thread was not running stay under K-18 as non-game stalls, as K-11 is.
2. Move the whole-frame rule to the Windows hardware tiers (K-02).
3. Keep Phase C open and chase the not-running frames first. That needs a system-level trace, which the profiler does not give.

**Owner's decision (2026-11-02): option 1.** Phase C is closed. The 4 ms rule is read on the streaming markers and the main thread's own CPU time. Frames in which the main thread was not running stay open under K-18, as non-game stalls like K-11.
