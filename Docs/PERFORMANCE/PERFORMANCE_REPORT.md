# Performance report

**First Unity measurements exist (2026-10-30), for an empty scene only.** They are from a macOS development player on the M1, not from Windows. They form the baseline that every later scene is compared against. They say nothing yet about the game's frame rate. No Windows figure appears here, because none has been measured.

## Development machine (measured 2026-10-30)

| Item | Value |
|---|---|
| Model | MacBook Air, Apple M1, 8 GB unified memory |
| OS | macOS 27.0 |
| Free disk | 76 GB of 228 GB on the data volume |
| Swap at time of check | 0 MB |
| Unity state | Before the install: no editor; Hub 3.21.3 had been removed, its settings left (337 MB). Installed 2026-10-30: Hub 3.22.4 (Homebrew cask from Unity's CDN, SHA-256 checked), editor 6000.3.27f1 arm64 + Windows Build Support (Mono). Licence not activated: the entitlement file from 2026-09-22 is not enough without a signed-in account |
| Other toolchains | No .NET SDK, Mono or Git LFS. Node 26.9.0 and Homebrew are present |

## Storage budget (25 GB hard ceiling)

Sizes marked *est.* are not official. Measured sizes are from `du` after the install on 2026-10-30.

| Item | Budget | Basis |
|---|---|---|
| Unity Hub 3.22.4 app + settings | 0.88 GB | **measured**: app 0.54 GB, settings 0.34 GB |
| Editor 6000.3.27f1, macOS arm64 | 9.0 GB | **measured** (Unity release API: 9.60 GB installed size, 5.22 GB download) |
| Windows Build Support (Mono) module | 0.98 GB | **measured** (API: 1.00 GB installed, 0.38 GB download) |
| Package and download caches | 1.5 GB | **measured 0.42 GB** (`~/Library/Caches/UnityHub`); Hub `downloads/` is 0 B |
| Project: Assets, Packages, ProjectSettings | 0.5 GB | Graybox only through Phase C. World data measured at 22 MB |
| `Library/` (import cache) | 3.0 GB | **measured 1.9 GB** for the bare URP project; the rest is room for a district |
| `Temp/`, `Logs/`, `obj/` | 0.5 GB | *est.* |
| Builds (at most 2 kept) | 1.0 GB | **measured 118 MB** for the Boot-scene Windows Mono build, 22 MB of it world data |
| Profiler captures, test results, screenshots | 1.0 GB | Limit; old captures pruned by hand |
| **Steady total** | **18.4 GB** | **6.6 GB headroom** |
| Install transient (download + unpack) | | **observed**: disk use rose about 19 GiB above the pre-install level mid-install (sampled once, so the true peak may be higher), then settled at +11 GiB |

**Gate before installing (passed 2026-10-30):**
1. The Unity release API listed editor plus Windows Mono module at **10.6 GB installed**, under the **11 GB** gate.
2. After install, measure with `du`. If the steady total is projected over 20 GB, stop and re-plan before importing any assets.
3. Measure `du -sh Library Temp Builds` at each milestone and log it in [../PRODUCTION/PROGRESS_LOG.md](../PRODUCTION/PROGRESS_LOG.md).

The 25 GB covers the Unity workspace only. The existing three.js repositories, worktrees and staging data (about 2.3 GB in total) sit outside it and are not touched.

**Measured total on 2026-10-30, after project creation and the first build: about 15.4 GB.** That is Hub 1.3 GB, editor 10 GB and project 2.1 GB, plus 2.0 GB of throwaway template projects in the session scratchpad, which are not part of the repository and can be removed.

## Empty-scene baseline (B-08, 2026-10-30)

Boot scene (camera, sun, GameRoot, FloatingOrigin; no world geometry) flying the fixed path `farmgate-shahbag-air` (40 s, one origin shift). macOS **development** player, Mono, Metal, Apple M1, 8 GB, 1280×720 window, vsync on (60 Hz). `Tools/bench.sh`; results in `Evidence/local/bench-*`, not committed.

| Run | Frames | Frame p50 / p95 / max | Main thread p50 / p95 / max | Render thread p50 / p95 | GPU p50 / p95 / max | Frames over 20 ms | GC alloc | System memory |
|---|---|---|---|---|---|---|---|---|
| Clean, final code (19:22) | 2,398 in 40.0 s | 16.67 / 17.37 / 50.0 ms | 1.71 / 2.09 / 3.78 ms | 0.99 / 1.25 ms | 1.47 / 1.63 / 5.16 ms | 2 | 264 B in 2 frames | 473 MB |
| Clean (19:19) | 2,398 in 40.0 s | 16.67 / 17.53 / 33.4 ms | 1.63 / 2.60 / 7.59 ms | 0.93 / 1.30 ms | 4.03 / 5.77 / 7.29 ms | 3 (from CSV) | 702 B in 3 frames | 422 MB |
| With profiler capture (19:18) | 2,387 in 40.4 s | 16.67 / 16.94 / 199.7 ms | 1.80 / 2.49 / 15.9 ms | 0.97 / 1.31 ms | 1.46 / 1.68 / 7.76 ms | 6 (from CSV) | 1.7 KB in 4 frames | 460 MB |

Notes:

- **Main-thread time excludes the vsync wait.** It reads 1.7 ms while frames take 16.7 ms. This is the figure to hold against the 10 ms budget; the empty scene uses about 17 % of it.
- **GPU time changes between runs at this low load:** p50 of 1.5 ms in one run and 4.0 ms in another. This fits the M1 changing clock speed, but it has not been checked. Compare GPU only within one session, alternating A and B runs.
- **The 200 ms frame appeared only with the capture on,** with ordinary CPU and GPU work in that frame. Most likely it is the profiler writing to disk. That rests on one run each, so it is not proven. For timing, use runs without capture; use the capture for where time goes.
- **The capture costs about 44 KB a frame,** 106 MB for the 40 s path. `CaptureMaxFrames` (3,000) bounds it.
- **Uncapped (vsync off):** an earlier run reached about 1,560 frames per second, with main thread p50 0.35 ms. That run filled its buffer after 12.8 s and is the reason the summary now has a `complete` flag. Uncapped figures are not a baseline.

## Streaming the slice (C-01, 2026-10-30)

Boot scene with `ChunkStreamer`, flying `slice-crossing-air` (60 s, east 1500 to 6500 along north 14250, 60–80 m up). All 12 slice chunks stream in and out; each is drawn as **debug lines only** (building outlines, road centrelines), so this measures the driver, not real meshes. Same player, machine and settings as the baseline.

| Runs | Max apply frame | Max apply step | Main thread p50 / p95 | GPU p50 | Frames over 20 ms | System memory |
|---|---|---|---|---|---|---|
| 3 after the warm-up (19:43–19:45) | 1.37–1.94 ms | 0.11–0.14 ms | 1.79–1.95 / 2.54–2.79 ms | 1.53–1.60 ms | 0, 1, 1 (each a single 50 ms frame) | 425–426 MB |
| 5 before the warm-up (19:34–19:41) | 3.27–4.19 ms | 2.61–3.41 ms (the first apply) | 1.67–2.46 / 2.34–3.14 ms | 1.51–6.12 ms | 0–6 | 426–620 MB (620 MB and GPU 6.1 ms in the first run after the build) |

Notes:

- **The first apply paid a one-off cost** of about 3 ms (first mesh and renderer of the session). `WarmUp()` now pays it at start, 2.6–4.3 ms before play. Later steps cost about 0.1 ms.
- **Against the baseline:** main thread p50 rises from 1.7 to about 1.9 ms with 12 chunks of lines.
- **GC:** about 3.8 MB in 14–16 frames per run. That is most likely the worker decode allocations, which the GC recorder counts; not checked. It is a target for C-02 (pooled buffers).
- **K-11:** a single 380–420 ms frame in 3 of the 8 runs, cause unknown.
- **The 4 ms rule must be re-measured** with the C-02..C-04 builders, which do far more work per chunk.

## Ground surfaces (C-02, 2026-10-30)

Same path, player and settings as C-01. Each chunk now also uploads its ground mesh, a few dozen to a few thousand vertices with vertex colour, in its own apply step.

| Runs | Max apply frame | Ground step max | Main thread p50 / p95 | GPU p50 | Frames over 20 ms | GC alloc | System memory |
|---|---|---|---|---|---|---|---|
| 4 (20:13–20:27) | 1.48–1.83 ms | 0.12–0.16 ms | 1.52–2.38 / 2.48–3.52 ms | 1.47–1.51 ms | 1, 12, 1, 3 | 4.4 MB in 12–16 frames; 7 collections in the run checked | 424–475 MB |

Notes:

- **The 4 ms rule holds** with ground meshes: the whole apply frame stays under 2 ms.
- **Main-thread p50 varied from 1.52 to 2.38 ms between identical runs.** The machine had Chrome and the Claude app open, with 1 GB of swap in use. Compare runs only within one session.
- **The 12-slow-frame run:** every one was a single 33 ms frame (a missed vsync) with ordinary main and GPU time; none was in an apply frame.
- **GC:** about 4.4 MB a run, in the apply batches (C-01: 3.8 MB). The 7 collections land in those frames. Pooled buffers are a later optimisation, not needed for the budget.

## Roads (C-03, 2026-10-30)

Same path, player and settings. Each chunk now also uploads a road mesh in its own apply step: carriageways, pads, sidewalks and bridge decks. Farmgate c_7_-30 has 974 road vertices.

| Runs | Max apply frame | Apply step max | Main thread p50 / p95 / max | GPU p50 / p95 | Frames over 33 ms | GC alloc | System memory |
|---|---|---|---|---|---|---|---|
| 3 (20:58–21:07) | 0.79–1.41 ms | 0.07–0.11 ms | 1.62–1.90 / 2.22–2.79 / 4.9–6.6 ms | 1.50–1.66 / 3.45–4.41 ms | 4, 0, 1 | 10.5 MB in 14–16 frames; 16–17 collections | 447–502 MB |

Notes:

- **The 4 ms rule holds** with roads. No apply step costs more than 0.11 ms.
- **Main-thread max 4.9–6.6 ms** in single frames. The apply marker in the same runs never exceeds 1.45 ms in a frame, so the streaming code does not cause them; the cause is not identified yet.
- **K-11, again:** a single missed frame at frame 662–663 in 2 of 3 runs, as in C-02. One run also had frames of 84, 42 and 191 ms late in the path. Every one had ordinary main and GPU time and no apply.
- **GC grew** from 4.4 MB to 10.5 MB a run, most likely from the road builder's lists on the worker (not profiled). Pooled buffers remain a later optimisation; the budget is not at risk.
- **Warm-up before play** now takes 14–15 ms (C-01: 2.6–4.3 ms). That is most likely first use of the road builder code; not profiled. It happens before the first frame of play.

## Buildings (C-04, 2026-11-02)

Same path, player and settings. Each chunk now also uploads its building mesh in its own apply step. Farmgate c_7_-30 has 17,194 building vertices; the densest chunk in the city, c_12_-36 (outside the slice), has 91,852.

| Runs | Max apply frame | Apply step max | Main thread p50 / p95 / max | GPU p50 / p95 | Frames over 33 ms | GC alloc | System memory |
|---|---|---|---|---|---|---|---|
| 3 (09:49–09:52) | 1.12–1.32 ms | 0.10–0.21 ms | 1.57–2.36 / 2.27–3.01 / 4.6–12.1 ms | 1.48–1.58 / 1.68–2.13 ms | 1, 0, 1 | 21.8 MB; 31–32 collections | 444–500 MB |

Notes:

- **The 4 ms rule holds** with buildings. No apply step costs more than 0.21 ms.
- **K-18:** the 12.1 ms main-thread frame came before the first apply, while the workers built the first chunks; frame time stayed at 16.7 ms. Frames of 5–6 ms appear around the first applies, outside the apply marker. Not yet attributed.
- **K-11:** a single 33–34 ms frame at frame 656–660 in 2 of 3 runs, as before.
- **GC doubled again**, to 21.8 MB a run (C-03: 10.5 MB), from the builders' lists on the workers. Pooling is the fix if GC frames show up; none were long.
- **Warm-up before play:** 25–26 ms (C-03: 14–15 ms), most likely first use of the building builder; not profiled.
- **Before city-wide streaming (Phase I):** the densest chunk's building mesh is 5× Farmgate's and must be measured; it may need splitting into several apply steps.

## Debug views (C-05, 2026-11-02)

Same path, player and settings. Each chunk also builds its grid, id and graph overlay on the worker and uploads it in its own apply step. The overlays and the C-01 debug lines are uploaded but hidden, as in plain play.

| Runs | Max apply frame | Apply step max (overlay step) | Main thread p50 / p95 / max | GPU p50 / p95 | Frames over 33 ms | GC alloc | System memory |
|---|---|---|---|---|---|---|---|
| 3 (10:36–10:39) | 1.52–1.65 ms | 0.09–0.17 ms (0.06–0.10 ms) | 1.47–2.06 / 2.00–2.85 / 6.6–12.2 ms | 1.46–1.56 / 1.57–2.33 ms | 1, 3, 1 | 22.4 MB; 32–34 collections | 421–427 MB |

Notes:

- **The 4 ms rule holds.** The overlay step costs at most 0.10 ms; the busiest apply frame is 1.65 ms.
- **K-11 grew:**
  - The 33 ms frame at frame 650–654 is now in all 3 runs.
  - Run 2 had 26 frames of 32.3–33.4 ms (one missed vsync each) between frames 176 and 855. Each had ordinary main and GPU time.
  - Run 3 had a **199.8 ms frame** at frame 2813, about 47 s in.
  - The FrameTimingManager CPU frame time peaked at **600 ms** (run 2, frame 2682) and **572 ms** (run 3, frame 2816), while `frameMs` there reads 16.7 ms. The C-04 runs peaked at 40–45 ms.
  - None of these is in an apply frame. Whether C-05 is involved is not known until a profiler capture.
- **K-18:**
  - Main-thread frames of 4.5–6.7 ms at the start (frames 1–4) and two frames before the first apply.
  - Frames of 11.6–12.2 ms late in the path (frames 2518 and 2658) with frame time at 16.7 ms.
  - All are outside the apply marker.
- **GC** is 22.4 MB a run (C-04: 21.8 MB). The overlay arrays add little.
- **Warm-up before play:** 31.6–32.5 ms (C-04: 25–26 ms), most likely first use of the overlay builder; not profiled.
- **System memory:** 421–427 MB, lower than C-04 (444–500 MB) from the very first frame, so not because of streaming; not explained.

## Phase C exit review (2026-11-02)

Same path, player and settings, with the code as committed with the review. Verdict and per-criterion evidence: [../PRODUCTION/PHASE_C_EXIT_REVIEW.md](../PRODUCTION/PHASE_C_EXIT_REVIEW.md).

| Runs | Max apply frame | Apply steps with a GC (slowest) | Main thread p50 / p95 / max after set-up | CPU frame time max | Frames over 33.3 ms (by CPU frame time) | GC alloc | System memory | Warm-up |
|---|---|---|---|---|---|---|---|---|
| 3 (11:14–11:16) | 0.87, 1.16, 2.22 ms | 0, 1 (0.57 ms), 1 (1.94 ms) | 1.71–1.94 / 2.41–2.56 / 9.85, 3.57, 5.73 ms | 610.6, 590.4, 43.9 ms | 0 (1), 2 (2), 1 (1) | 22.4 MB; 29–30 collections | 423–480 MB | 83.5–86.1 ms, 1,139 methods compiled ahead |
| 1 profiled, frames 3–2002 (11:22) | 0.92 ms | 0 | 2.23 / 2.91 / 4.81 ms | 53.0 ms | 1 (1) | 22.4 MB; 30 collections | 521 MB | 82.5 ms |

"After set-up" leaves out frames 0–4. The bench is still setting up in those frames, and the frame-timing columns lag by a few frames. They read 3.9–17.1 ms, before any chunk is requested.

**What the captures show:**

| Capture | Frames | Busiest frame (main thread minus waits) | What was in it |
|---|---|---|---|
| `-104503`, 3,000 frames asked for | 1003–3002 (the editor keeps 2,000) | 5.15 ms | Mono JIT 2.08 ms at the first unload. Also a 5.00 ms frame with `GC.Collect` 2.21 ms inside `Dad.Streaming.Apply` |
| `-104911`, start, before the warm-up compiled code | 3–2002 | 6.42 ms | Mono JIT 3.46 ms in the first apply batch |
| `-105346`, start, with it | 3–2002 | 3.59 ms | Frame 3 (JIT 1.76 ms in set-up); the first apply batch is 3.08 ms with 0.70 ms of JIT left |
| `-105521`, from frame 1600 | 1600–3599 | 3.89 ms | Render work only. Its frame 48 lasted **575.5 ms**: main thread busy 3.23 ms, then `WaitForTargetFPS` 572 ms; render thread `Semaphore.WaitForSignal` 575 ms (K-11) |
| `-112220`, start, final code | 3–2002 | 4.24 ms | SSAO 0.65 ms, render-graph recording 0.50–0.54 ms; streaming markers at most 1.65 ms in any frame; no `GC.Collect` on the main thread |

Notes:

- **The streaming work holds the 4 ms rule.**
  - The busiest apply frame is 2.22 ms. That was the frame where the overlay step met a collection and took 1.94 ms; without one, at most 1.16 ms.
  - The streaming markers total at most 1.65 ms in any captured frame.
- **The whole frame does not hold it yet (K-18).**
  - Run 3 has a 5.73 ms frame in the first apply batch, the frame with that collection.
  - Run 1 has a 9.85 ms frame about 1 s in, with no apply and no collection. No capture has a frame like it.
  - The builders allocate 22.4 MB a run. The collections bunch up just before each apply batch: 2–7 a frame, in frames 307–309, 602–604, 885–888 and 1216–1219. C-07 is the pooling pass that should remove them.
- **First-call compilation is fixed.**
  - The Mono player compiled each streaming method on its first call, up to 3.46 ms at once.
  - The warm-up now compiles 1,139 methods ahead (`ChunkStreamer.PrepareCode`). That costs about 50 ms before play (C-05: 32 ms warm-up; now 82–86 ms).
  - Array, delegate and runtime methods are skipped. Compiling them broke the Mono runtime ("runtime and class libraries are out of sync"), and one run ended early.
- **K-11 is presentation, not game work.** In the 575 ms frame, both the main thread and the render thread were waiting. `frameMs` read 16.7 ms through 572–610 ms stalls, so the bench now also counts frames over 33.3 ms by CPU frame time.
- **No effect: reserving the managed heap.** A 64 MB heap reserved at start left the collections at 30 a run. It was removed.
- **System memory** is 423–480 MB unprofiled and 521 MB with the profiler on. It varies from the very first frame, so it is not streaming.

## Builder allocation pass (C-07, 2026-11-02)

Same path, player and settings. Three changes, measured one after another:

1. Scratch reuse: each load rents a `BuildScratch` set, the builders' working lists and pools.
2. Mesh array pools: road, building and line arrays from `MeshArrays`, handed back after upload.
3. The apply hold: finished chunks wait while other loads still build, for at most 30 frames.

| Code | Runs | GC alloc a run | Collections | Max apply frame | Apply steps with a GC (slowest) | Frames over 4 ms after set-up (near streaming) |
|---|---|---|---|---|---|---|
| Exit review | 3 + 1 profiled | 22.4 MB | 29–30 | 0.87–2.22 ms | 0, 1 (0.57 ms), 1 (1.94 ms), 0 | up to 9.85 and 5.73 ms (the count came with C-07) |
| 1. Scratch reuse | 4 + 1 profiled (`-121401` to `-122204`) | 9.61–9.63 MB | 10–11 | 0.96–1.11 ms; 3.38 ms profiled | 0 in 4; 1 (3.21 ms) profiled | 3 (1), 4 (0), 1 (0), 0, profiled 3 (1) |
| 2. + mesh array pools | 3 (`-123324` to `-123539`) | 8.51–8.84 MB | 9–10 | 0.88, 1.13, 2.70 ms | 0, 0, 1 (2.66 ms) | 0, 0, 3 (0) |
| 3. + apply hold (final) | 6 + 2 profiled (`-124053` to `-125328`) | 8.82–8.84 MB | 9–11 | 0.92–1.17 ms; once 3.66 ms | 0 in all 8 | 2 (0), 1 (0), 1 (0), 2 (1), 1 (0), 0; profiled: the capture's start or stop, and one frame before a capture began (`-124640`, row 436) |

**What the builders allocate, 12 slice chunks, warm second pass (`BuilderAllocationTests`, "GC Allocated In Frame"):**

| Stage | Before C-07 | After | Mesh arrays handed on |
|---|---|---|---|
| Ground | 676 KB | 18 KB | 15 KB (exact arrays) |
| Roads | 5,077 KB | 994 KB | 388 KB (pooled) |
| Buildings | 9,360 KB | 184 KB | 2,784 KB (pooled) |
| Overlays | 752 KB | 143 KB | 140 KB (exact arrays) |
| Outlines | 1,980 KB | 0 KB | 554 KB (pooled) |
| Read and decode (unchanged) | 272 + 1,605 KB | 269 + 1,600 KB | — |

**Allocation by batch in a run (bench, MB):**

| Batch | 1 | 2 | 3 | 4 |
|---|---|---|---|---|
| Scratch reuse | 3.85 | 2.74 | 1.71 | 1.31 |
| Final | 4.36 | 2.03 | 1.60 | 0.85 |

Notes:

- **Allocation is down 61%, collections by two thirds.** A run allocates 8.8 MB instead of 22.4 MB, with 9–11 collections instead of 29–30.
  - The pools cut batch 2 by 26% and batch 4 by 35%, batch 3 by only 6%.
  - The first batch rose. Every pool starts empty, and pooled arrays are rounded up to a power of two.
  - What is left per 12 warm chunks is mostly decode (1.6 MB) and the roads' per-road point arrays (1.0 MB).
  - The collector here runs after roughly every 0.7–1 MB allocated, because the live heap is small. So any batch still triggers collections, 1–6 of them.
- **No collection inside an apply step in the final 8 runs.** Before the hold, 2 of 8 C-07 runs had one, with steps of 3.21 and 2.66 ms (the frames reached 4.83 ms and stayed under 4 ms).
  - The hold engaged only 3 times in those 8 runs, 1 frame each: on this machine a batch usually builds within the frame it starts in.
  - So these runs do not show whether the hold, or chance, kept collections out of apply steps. The hold matters more where builds take several frames (`ChunkStreamerTests.FinishedChunksWaitForLoadsStillBuilding`).
- **Apply steps still allocate on the main thread:** 2.7–7.6 KB in a frame. That is the new `GameObject`s, meshes and their names; only pooled chunk objects would remove it. No collection was seen to start there.
- **Frames over 4 ms after set-up:** 0–2 per unprofiled run.
  - The bench now records the main thread's own CPU time per frame (`mainCpuMs`, `mainThreadCpuMs`; macOS thread CPU clock).
  - In the three slow frames measured with it, the thread used 1.6–2.2 ms of CPU, its usual amount (p50 1.8–2.3 ms). It was not running for the other 2.3–4.1 ms.
  - No collection finished and nothing was allocated within a frame of any of the eight. Two captures (4,000 frames) caught none; their busiest frames were 3.64 and 3.01 ms.
  - One of them fell within 3 frames of an apply batch. So did the one 3.66 ms apply frame, whose steps took at most 0.13 ms each. See [../PRODUCTION/PHASE_C_EXIT_REVIEW.md](../PRODUCTION/PHASE_C_EXIT_REVIEW.md) (re-check).
- **Warm-up before play:** 105–123 ms, with 1,573–1,581 methods compiled ahead (exit review: 82–86 ms). It now builds the made-up chunk on 3 worker tasks, so the thread pool's threads exist before the first load.
- **System memory:** 418–483 MB, the same range as before.
- **K-11 stalls are unchanged:** CPU frame time reached 596–622 ms in 4 of the 11 runs.

## Lit world shader (D-01, 2026-11-02)

Same path (`slice-crossing-air`), development player at 1280×720, vSync on, on the M1. D-01 changes what the GPU draws:

- the lit `Dad/World` shader (URP PBR, Forward+, SSAO, fog);
- building shadows, with the shadow distance raised from 50 to 150 m and 4 cascades.

The mesh format and the apply step are unchanged.

| Run | Code | Metal shader cache | Main-thread max | Frames over 4 ms after set-up (near streaming) | GPU p50 / p95 / max | Max apply frame |
|---|---|---|---|---|---|---|
| `-125328` | C-07 final, unlit | warm | 6.68 ms | 0 | 1.55 / 1.68 / 2.28 ms | 1.01 ms |
| `-133429` | D-01, no render warm-up | cold (new build) | **299.57 ms** (row 312, the first lit chunk) | 3 (2) | 1.75 / 4.18 / 7.73 ms | 0.82 ms |
| `-133601` | same, profiled | warm | 14.29 ms | 9 (1) | 1.85 / 4.18 / 5.80 ms | 1.09 ms |
| `-133956` | + render warm-up, one camera | warm | 5.61 ms | 5 (2) | 1.80 / 4.91 / 12.28 ms | 1.18 ms |
| `-134224` | same; render counters as block means | warm | 5.71 ms | 5 (1) | 1.75 / 4.50 / 7.10 ms | 0.88 ms |
| `-134748` | one camera | cold (cache moved to the Trash) | **108.07 ms** | 17 (2) | 1.83 / 4.23 / 8.89 ms | 0.93 ms |
| `-135007` | one camera | cold | **102.38 ms** | 1 (1) | 1.71 / 4.15 / 7.38 ms | 0.88 ms |
| `-135155` | two cameras (final) | cold | 5.68 ms | 36 (1), machine busy | 2.66 / 8.88 / 12.02 ms | 1.53 ms |
| `-135310` | two cameras (final) | warm | 5.48 ms | 3 (1) | 1.76 / 4.83 / 6.09 ms | 1.09 ms |
| `-140059` | final code, committed build | warm | 3.24 ms | 0 | 1.73 / 4.67 / 7.63 ms | 0.79 ms |

`-134331` is not a measurement. The display had gone to sleep and the frame clock stood still: 8,400 frames, 0.01 ms each, and the camera never moved. `bench.sh` now prints a warning when this happens, and benches run under `caffeinate -d -u`.

**GPU over the slice.** Rows 900–2399 are the stretch with world data in view; outside it only the base plate is drawn.

| Run | GPU mean / p50 / p95 / max over the slice | Outside the slice | Main thread p50 over the slice |
|---|---|---|---|
| C-07 (`-125328`) | 1.55 / 1.56 / 1.66 / 2.06 ms | 1.55 ms mean | 1.95 ms |
| D-01 (`-134224`) | 3.89 / 3.96 / 4.82 / 7.10 ms | 1.43 ms mean | 2.04 ms |
| D-01 (`-135310`) | 4.07 / 4.17 / 5.16 / 6.09 ms | 1.44 ms mean | 1.92 ms |

**Render counters** (`-135310`, means of 10-frame blocks over the whole path):

| Counter | p50 / p95 / max |
|---|---|
| Batches | 26 / 76 / 86 |
| SetPass calls | 10 / 15 / 15 |
| Triangles | 43,287 / 196,009 / 213,136 |
| Shadow casters (renderers) | 0 / 9 / 11 |
| Graphics memory, peak / end | 185 MB / 81 MB (237 MB peak in 4 of the 8 complete runs) |

Notes:

- **The lit world costs the GPU about 2.5 ms a frame over the slice** at 1280×720 on the M1: p50 1.56 → 3.96–4.17 ms.
  - The main and render threads are unchanged: main-thread p50 1.9–2.0 ms, render-thread p50 1.1–1.2 ms.
  - The HIGH-tier GPU budget is 12 ms at 1440p on Windows hardware we do not have (K-02). 1440p has 4 times the pixels of this run, so the M1 is not a guide to that tier.
  - D-02 (texture arrays) and D-03 (sky) add to this. Each milestone re-measures it. D-02 measured 6.9–7.9 ms; see below.
- **Shadow casters.** The counter reads 0 for most of the path because the path starts and ends outside the slice, where nothing casts.
  - In rows 900–2399, 80% of frames have casters, up to 22 a frame (per-frame values).
  - Each chunk's buildings are one renderer, so the count is chunk renderers per cascade.
  - The path flies at 60–80 m. No path measures street level yet: the path tests require every shipped path to stay above 20 m. A street path, with that rule relaxed for it, comes with D-03, when the sun moves and shadow cost changes with it.
- **Cold shader cache.** The first run of a new build compiles the Metal shaders as they are first drawn.
  - Without the warm-up, the first frame with lit chunks took 300 ms.
  - The capture (`-133601`) showed the first frame with a shadow caster in range spending 6 ms in Mono JIT for URP's shadow code and 1.4 ms creating the pipeline state.
  - `RenderWarmup` draws the streamer's made-up chunk with two hidden cameras before play: one near enough for shadows, one beyond the shadow distance. With one camera, cold runs still had a 102–108 ms frame at the first real chunk, from the variant without shadows. With two, the cold run's slowest main-thread frame was 5.68 ms.
  - The cold two-camera run was noisy. GPU p95 was 8.88 ms and 36 frames passed 4 ms, all with normal main-thread CPU time (busiest 7.58 ms). The browser and the Claude app were busy at the time.
  - The bench's Metal cache is the player's own, in `$(getconf DARWIN_USER_CACHE_DIR)/com.Unity-Technologies.com.unity.template.urp-blank/com.apple.metal`, about 8 MB. Moving it to the Trash forces a cold run; the player rebuilds it.
- **Apply budget re-measured.** The apply step is unchanged: max apply frame 0.79–1.18 ms in the warm runs, 1.53 ms in the busy cold one (C-07: 0.92–1.17 ms). The final run (`-140059`) had no main-thread frame over 4 ms after set-up; its slowest was 3.24 ms. The builders pack a material id into the colour's alpha byte where they used to bake a light factor into the colour.
- **Builders on the city, in the editor** (EditMode tests, Mono debug mode):
  - all buildings: 3,127 ms at C-07, 1,667 ms at D-01;
  - all roads and border checks: 4,428 ms at C-07, 2,904 ms at D-01.
  - Editor timings swing with machine load, so this shows the builders did not get slower, not that they got faster. Neither the bench nor the capture reader sees worker threads, so there is no player figure per builder.
- **Allocation and collections unchanged:** 8.79–8.84 MB a run, 9–11 collections (C-07: 8.82–8.84 MB, 9–11).
- **Warm-up before play:** 71–81 ms, with 1,582–1,586 methods compiled ahead. That includes the render warm-up's set-up; the drawing happens in the first frame.
- **System memory:** peak 428–496 MB (C-07: 418–483 MB).
- **K-11:** none of the ~600 ms presentation stalls appeared in these 9 runs. The longest CPU frames were 317 ms (the cold compile) and 117 ms (the one-camera cold runs); otherwise 39–54 ms.
- **Builds:** macOS development player 205.7 MB; Windows Mono release 115.4 MB by Unity's report (121,392 KB on disk), 0 errors, 0 C# warnings, not run (K-02).

## Material library (D-02, 2026-11-02)

Same path (`slice-crossing-air`), development player at 1280×720, vSync on, on the M1. D-02 gives `Dad/World` its textures:

- each pixel reads its layer's albedo, normal and mask arrays three times (stochastic sampling, one read per corner of a 2 m lattice triangle);
- it also reads the layer's mean once and the macro texture twice: about 12 texture reads a pixel, where D-01 had none.

The mesh format, the apply step and the draw calls are unchanged.

**Texture memory**

| Texture | Format | Size | GPU memory |
|---|---|---|---|
| `WorldAlbedo` | BC7 sRGB, 2D array | 16 × 512², 10 mips | 5.33 MB |
| `WorldNormal` | BC7, 2D array | 16 × 512², 10 mips | 5.33 MB |
| `WorldMask` | BC7, 2D array | 16 × 512², 10 mips | 5.33 MB |
| `WorldMacro` | BC7, 2D | 512², 10 mips | 0.33 MB |
| **Total** | | | **16.33 MB** (budget 24 MB) |

- The figure is computed from format, size, mips and slices (`MaterialLibraryTests.TextureMemoryIsWithinBudget`).
- The editor's profiler reads 32.67 MB, because it also counts the editor's CPU copy.
- In the player, graphics memory at the end of the path went from 81 to 98 MB (+17 MB).
- The 24 MB budget leaves room for the facade, prop and sign atlases of later milestones within the LOW tier's 1 GB of texture memory. Since 2026-11-07 it is the world library's own limit inside a 48 MB budget for all art textures (owner, K-63).
- On disk: 12.5 MB of generated PNGs in the repository. They regenerate in 7.8 s, and a test checks that a regeneration matches them byte for byte (SHA-256).

**Runs.** All runs are warm. "GPU over the slice" covers rows 900–2399, as in D-01; the D-01 runs were re-read with the same script.

| Run | Code | GPU over the slice mean / p50 / p95 / max | Outside the slice (mean) | Main-thread frames over 4 ms after set-up (near streaming) | Max apply frame |
|---|---|---|---|---|---|
| `-135310` | D-01 | 4.07 / 4.17 / 5.15 / 6.09 ms | 1.54 ms | 3 (1) | 1.09 ms |
| `-140059` | D-01 final | 3.84 / 3.73 / 6.39 / 7.63 ms | 1.54 ms | 0 | 0.79 ms |
| `-143535` | D-02 | 7.63 / 7.83 / 10.12 / 11.38 ms | 2.68 ms | 1 (0) | 1.05 ms |
| `-143640` | D-02 | 7.62 / 7.90 / 10.24 / 11.27 ms | 2.31 ms | 1 (1) | 0.86 ms |
| `-143746` | D-02 | 6.58 / 6.93 / 8.32 / 9.69 ms | 2.07 ms | 0 | 1.13 ms |
| `-144048` | diagnostic: anisotropic filtering off | 7.60 / 7.83 / 10.24 / 11.07 ms | 2.26 ms | 2 (1) | 0.87 ms |
| `-144155` | same | 6.57 / 6.88 / 8.25 / 9.29 ms | 2.07 ms | 1 (1) | 3.12 ms |
| `-144406` | diagnostic: one read per array, no stochastic blend | 4.83 / 5.04 / 6.06 / 7.17 ms | 1.78 ms | 1 (0) | 1.12 ms |
| `-144513` | same | 4.86 / 5.04 / 6.08 / 8.57 ms | 1.71 ms | 0 | 2.70 ms |
| `-144618` | same | 4.85 / 5.04 / 6.09 / 8.20 ms | 1.71 ms | 1 (1) | 1.00 ms |
| `-144823` | trial: corners under 5 % weight dropped (1.8 reads a pixel) | 6.03 / 6.38 / 7.53 / 8.22 ms | 2.38 ms | 3 (1) | 0.92 ms |
| `-144929` | same | 7.29 / 7.62 / 9.25 / 10.29 ms | 2.17 ms | 0 | 0.89 ms |
| `-145035` | same | 6.97 / 7.52 / 9.29 / 10.99 ms | 1.88 ms | 0 | 0.90 ms |
| `-145248` | D-02 final, committed build | 7.64 / 7.89 / 10.13 / 11.40 ms | 2.33 ms | 0 | 0.92 ms |

Notes:

- **The textures cost the GPU about 3–4 ms a frame over the slice** at 1280×720 on the M1: p50 3.7–4.2 → 6.9–7.9 ms. The diagnostic builds split this cost:
  - reading each array once costs about 1 ms (p50 5.04 ms in all three single-read runs);
  - the stochastic blend, which reads each array three times, costs about 2–3 ms more;
  - anisotropic filtering is not the cost: with it off, two runs read 6.88 and 7.83 ms.
- **Dropping low-weight corners was tried and reverted.** It took 1.8 reads a pixel instead of 3, with no measurable gain (6.38–7.62 ms). The shader keeps all three reads. K-22 lists the options.
- **Final-code runs fall into two groups, about 6.9 and 7.9 ms, with no code change between them.** The three single-read runs read 5.04 ms each.
  - The M1 MacBook Air has no fan, and back-to-back runs heat it. The two groups may be its GPU clock.
  - The bench reads neither clock nor temperature, so this is not established.
- **Draw calls unchanged:** one material on one SRP Batcher shader. `-145248`, p50 / p95 / max:
  - batches 26 / 75 / 85 (D-01: 26 / 76 / 86);
  - SetPass calls 10 / 15 / 15 (D-01: the same);
  - triangles 43,287 / 191,976 / 234,178;
  - shadow casters 0 / 9 / 12.
- **Outside the slice:** 1.54 → 2.07–2.68 ms mean. Only the base plate is drawn there; it covers most of the screen and now pays for its Soil texture.
- **HIGH tier:** the GPU budget (12 ms at 1440p on Windows) stays unmeasured (K-02).
- **Main thread and apply unchanged:**
  - final-code runs had 0–1 main-thread frames over 4 ms after set-up, and apply frames of 0.86–1.13 ms;
  - two diagnostic runs had an apply frame of 2.70 and 3.12 ms, under the 4 ms limit. The apply step does not touch textures, and no final-code run repeated it.
- **Graphics memory:** end of path 81 → 98 MB, the 16.3 MB of textures; peak 201–253 MB (D-01: 185–237).
- **System memory peak:** 467–518 MB. D-01's two runs, re-read the same way, gave 436 and 489 MB.
- **Allocation:** 8.82 MB a run, 9–10 collections (D-01: 8.79–8.84 MB, 9–11).
- **Builds:**
  - macOS development player: 212.5 MB (D-01: 205.7 MB);
  - Windows Mono release: 131.8 MB by Unity's report (138,128 KB on disk; D-01: 115.4 MB), 0 errors, 0 C# warnings, not run (K-02);
  - the Windows build's 20 warnings are URP shaders that the project does not use being stripped.

## Time of day (D-03, 2026-11-02)

Development player at 1280×720, vSync on, on the M1. D-03 adds three things to a frame:

- `DayCycle.Apply` on the main thread: the sun and moon for the moment, `SkyModel`, the two directional lights, ambient, fog and 12 shader globals;
- the `Dad/Sky` skybox, drawn on the pixels the city leaves: a gradient, haze glow round the sun and moon, two noise reads for clouds, hashed stars, and the moon and sun discs;
- the sky reflection, a 16-texel HDR cubemap filled on the CPU (`SkyReflection`).

All benches hold the clock at 12:00 on 28 January, so they never refill the reflection; the refill is measured in the editor (below).

**Main thread.**

- In the player, capture `-155356` (2,000 frames over the slice): `Dad.Sky.Apply` p50 0.067, p95 0.126, max 0.175 ms. It was under the capture reader's 0.05 ms listing threshold in 315 of the 2,000 frames.
- In the editor, `DayCycleTests` applies the clock six times a game minute, so a refill is under way in most of its frames. Over two runs of both views (8,640 frames each): per frame p50 0.19 ms, p99 0.34–0.45 ms, max 0.39–0.81 ms, except one frame of 4.89 ms in the final full-suite run (air view). That frame was not traced: the clock does no whole fill in that loop, so an editor collection or descheduling is likely. The test fails if p99 reaches 1 ms.
- **The first cut stalled the main thread.** It refilled a whole 32-texel cube on the frame the sky changed enough: 4.7 ms in the editor, about once a game minute at dusk (every 2 s of play). The day test's timing found it. Measured parts:

| Cube | Radiance of every texel | `SetPixels` | Upload with mips (`Apply(true)`) |
|---|---|---|---|
| 32 texels (6,144) | 4.35 ms | 0.04 ms | 0.16 ms |
| 16 texels (1,536) | 1.09 ms | 0.011 ms | 0.076 ms |

  Now the cube has 16-texel faces (5.6° a texel, enough for a smooth sky), the texel directions are a table, and a running clock refills one face a frame and uploads the six together. A whole fill (about 1.1 ms) happens only at start and after a jump in time (F6, a golden view). All 14 goldens still matched after the change (mean difference 0.000–0.021).
- **Allocation:** none per frame. The street runs allocated 96 bytes in 60 s with 0 collections. The air runs allocated 8.41–8.43 MB, the same as D-02 (the streaming builders).

**Runs, `slice-crossing-air`.** Warm runs; GPU over rows 900–2399, read with the same script as D-01 and D-02.

| Run | Code | GPU over the slice mean / p50 / p95 / max | Outside the slice (mean) | Main-thread frames over 4 ms after set-up (near streaming) | Max apply frame |
|---|---|---|---|---|---|
| `-145248` | D-02 final, for comparison | 7.64 / 7.89 / 10.13 / 11.40 ms | 2.33 ms | 0 | 0.92 ms |
| `-154951` | D-03 | 6.92 / 7.28 / 8.83 / 9.99 ms | 2.65 ms | 19 (1) | 1.05 ms |
| `-155058` | D-03 | 6.92 / 7.27 / 8.68 / 10.10 ms | 2.55 ms | 27 (1) | 1.62 ms |
| `-155204` | D-03 | 7.08 / 7.41 / 9.05 / 10.44 ms | 2.57 ms | 0 | 0.91 ms |
| `-155356` | D-03, profiled from frame 900 | 6.92 / 6.69 / 10.40 / 12.63 ms | 2.56 ms | 2 (0): the capture starting (581.8 ms) and an Input System JIT (7.86 ms) | 1.06 ms |

Notes:

- **The sky's GPU cost is inside the run-to-run spread.** p50 over the slice is 7.27–7.41 ms, against 6.93–7.90 ms for D-02. Outside the slice, where the base plate and the sky fill the screen, it is 2.55–2.65 ms against 2.07–2.68 ms. There was no build without the sky to compare against, so the cost is not isolated, only bounded by the spread.
- **Draw calls:** batches p50 26 and SetPass calls p50 10, as at D-02. Triangles and shadow casters are unchanged.
- **Memory:** graphics memory peak and end 253 / 98 MB, as at D-02. The cubemap is 16 KB with its mips. The Windows build is 131.8 MB, as at D-02; all shaders together are 653.5 KB.
- **Main-thread frames over 4 ms: 19 and 27 in two runs, 0 in the third.** D-02's four final-code runs had 0–1.
  - In every such frame the main thread's CPU clock read 1.6–2.9 ms, its usual amount, so it was not running (K-18).
  - During these runs the owner's Chrome had a renderer at about 105 % CPU and WindowServer was at 42 % (`ps`, just after the third run). The D-02 runs may not have had that load. The cause is not proven.
  - The capture measures busy time (the player loop minus waiting for vsync and the GPU): p50 2.09 ms, p95 3.19 ms. One frame went over 4 ms: 6.26 ms of Mono JIT inside the Input System's update (`NativeInputSystem.NotifyUpdate`, 6.55 ms) at capture frame 1678. No earlier capture has it, and the bench sends no input. An OS input or focus event reaching the window is the likely cause, not proven (K-18).

**Street level, `kawran-farmgate-street` (new).** 60 s, 966 m north along Kazi Nazrul Islam Avenue (OSM way 528310083) from Kawran Bazar to Farmgate. The eye is 1.8 m up, looking 40 m ahead. All 12 chunks stream in during the warm-up; none are applied after.

| Run | GPU after set-up p50 / p95 / max | Main thread p50 / max | Main-thread frames over 4 ms (near streaming) | Batches p50 / max | Triangles p50 / max | Graphics memory peak / end |
|---|---|---|---|---|---|---|
| `-155604` | 8.42 / 11.16 / 15.43 ms | 2.07 / 4.52 ms | 1 (0) | 45 / 77 | 104,702 / 159,619 | 211 / 107 MB |
| `-155710` | 8.23 / 11.30 / 15.42 ms | 2.11 / 9.67 ms | 11 (0) | 45 / 77 | 105,350 / 159,619 | 211 / 107 MB |

- Street level is the heaviest view so far: near walls and road fill the screen with textured, shadowed pixels (K-22). The 15.4 ms maximum leaves 1.3 ms of the 60 Hz frame on the M1 at 1280×720, before D-04 to D-13 add markings, kerbs, props and lights.
- The frames over 4 ms all had normal CPU time (2.3–3.0 ms): not running, as above.

## Roads, second pass (D-04, 2026-11-02)

Development player at 1280×720, vSync on, on the M1, clock held at noon. D-04 puts the road markings and street details into the road mesh, so they add vertices, worker time and garbage, but no draw calls or apply steps.

**Mesh.** Over the 12 slice chunks the road mesh went from 14,843 to 84,183 vertices:

| Material | D-03 | D-04 | What is new |
|---|---|---|---|
| Marking | 0 | 42,832 | 7,172 line pieces, 290 zebra bars, 4 stop lines, the white bands of 351 speed breakers |
| Asphalt | 8,493 | 22,141 | the breakers' dark bands and end faces |
| Metal | 0 | 8,560 | 360 manhole covers, 1,330 gully grates |
| Soil | 0 | 4,300 | 430 potholes |
| Paving, Concrete | 6,350 | 6,350 | unchanged |

**Worker time.** The builders run on the streaming workers, which neither the bench nor the capture reader sees. The city test (`RoadBuilderTests`, all 1,303 chunks: build and border checks, editor, Mono debug) was run twice with the markings call taken out and twice with it, back to back: 4,541 and 4,622 ms without, 6,984 and 7,401 ms with. So the markings add about 2.4–2.8 s over the city, about 2 ms a chunk on a worker in the editor. The cut against overlapping carriageways is part of that: the runs just before it gave 6.5–6.8 s. Editor timings swing with load (the same test gave 4.7 s and 6.8 s earlier today), which is why the comparison was run back to back.

**Garbage.** The roads stage allocates 1,612 KB for 1,999 KB of mesh arrays over the 12 chunks (D-03: 994 / 388 KB), from the per-road marking arrays (`Drawn`, segment attributes, keep-out rings). The air runs allocated 9.56–9.59 MB (D-03: 8.41–8.43 MB), with the same 10 collections. In the four air runs one apply step ran into a collection (3.44 ms, run `-171105`); the editor walk had none. `BuilderAllocationTests`' roads limit fell from 4× to 1× the mesh arrays, so that road outlines not reused (2,800 KB, checked) still fail it.

**Runs, `slice-crossing-air`.** Warm runs; GPU over rows 900–2399, read with the same script as D-01 to D-03.

| Run | Code | GPU over the slice mean / p50 / p95 / max | Outside the slice (mean) | Main-thread frames over 4 ms after set-up (near streaming) | Max apply frame |
|---|---|---|---|---|---|
| `-155204` | D-03, for comparison | 7.08 / 7.41 / 9.05 / 10.44 ms | 2.57 ms | 0 | 0.91 ms |
| `-171105` | D-04 | 7.06 / 7.18 / 11.17 / 12.55 ms | 2.59 ms | 5 (1) | 3.67 ms (a step with a collection) |
| `-171211` | D-04 | 6.99 / 6.97 / 11.24 / 12.75 ms | 2.57 ms | 3 (0) | 1.09 ms |
| `-171317` | D-04 | 6.36 / 6.61 / 8.54 / 9.71 ms | 2.56 ms | 0 | 1.01 ms |
| `-171441` | D-04, profiled from frame 900 | 6.37 / 6.60 / 8.66 / 11.89 ms | 2.57 ms | 3 (2): the capture starting (17.46 ms) and the frame after it | 1.18 ms |

- **GPU:** p50 over the slice 6.60–7.18 ms, inside the earlier spread (D-03 7.27–7.41, D-02 6.9–7.9). p95 rose in two runs (11.2 ms against 8.7–9.1); the other two match D-03. The markings are a few more thin triangles on the same draw calls, so no large cost was expected; the p95 change is not explained.
- **Draw calls:** batches p50 26, SetPass calls p50 10, as at D-03. Triangles in the frame p50 65,168 (D-03 45,356), peak 575–583 thousand at set-up.
- **Memory:** graphics memory peak 201–253 MB, end 98 MB (D-03 253 / 98). System memory peak 463–516 MB.
- **Main thread:** the capture's busy time p50 2.46 ms, p95 2.89 ms (D-03 2.09 / 3.19). Two capture frames were busy over 4 ms: frame 3, a 1.29 ms collection as the capture started, and frame 1531, 4.84 ms with 1.12 ms in URP's `Setup Camera Properties`, away from streaming. Every frame over 4 ms in the table had normal main-thread CPU (2.1–3.4 ms, 4.2 ms at the capture's start), so the thread was mostly not running (K-18); row 1221 of the first run and rows 903 and 906 of the profiled run were near streaming.
- **Machine:** just before the runs, WindowServer was at 25 % CPU and a Chrome renderer at 12 %.

**Street level, `kawran-farmgate-street`.** 60 s along Kazi Nazrul Islam Avenue at 1.8 m, where the markings fill the lower half of the screen.

| Run | GPU after set-up p50 / p95 / max | Main thread p50 / max | Main-thread frames over 4 ms (near streaming) | Batches p50 / max | Triangles p50 / max | Graphics memory peak / end |
|---|---|---|---|---|---|---|
| D-03 (`-155604`, `-155710`) | 8.23–8.42 / 11.16–11.30 / 15.42–15.43 ms | 2.07–2.11 / 4.52–9.67 ms | 1 and 11 (0) | 45 / 77 | 104,702–105,350 / 159,619 | 211 / 107 MB |
| `-171612` | 7.50 / 12.35 / 14.72 ms | 2.40 / 5.00 ms | 1 (0) | 46 / 77 | 147,730 / 235,037 | 213 / 108 MB |
| `-171718` | 8.09 / 12.65 / 15.01 ms | 2.45 / 5.97 ms | 3 (0) | 46 / 77 | 147,572 / 235,037 | 265 / 108 MB |

- Triangles at street level are up 41 % (p50) and 47 % (peak); batches are unchanged. GPU p50 and max are within D-03's figures; p95 is up about 1.2 ms. The frame keeps at least 1.6 ms of the 60 Hz budget at its slowest.
- Main-thread p50 rose by about 0.3 ms in both runs. The main thread does no marking work, so the likely cause is culling and submitting the larger road meshes; not profiled.
- No allocation in either run.

**Builds:** macOS development player 212.6 MB (D-03 213 MB); Windows Mono release 131.8 MB by Unity's report (138,176 KB on disk), 17.7 s, 0 errors, 0 C# warnings, the same 20 URP shader-stripping warnings, not run (K-02).

## Footpaths and kerbs (D-05, 2026-11-03)

Development player at 1280×720, vSync on, on the M1, clock held at noon. Sidewalks, kerbs, bus-stop pads and drains are part of the road mesh, so they add vertices, worker time and garbage, but no draw calls or apply steps.

**Mesh.** Over the 12 slice chunks the road mesh went from 84,183 to 521,375 vertices (390,707 triangles). Without the sidewalk pass it is 81,297: the flat sidewalk strips of C-03 are gone.

| Material | D-04 | D-05 | What is new |
|---|---|---|---|
| Concrete | 3,464 | 371,744 | kerb faces, outer and end faces, drain lips and walls, slab steps |
| Marking | 42,832 | 42,832 | unchanged |
| Water | 0 | 39,502 | drain channels |
| Paving | 2,886 | 32,296 | sidewalk tops (three tints, broken slabs, stop pads with painted kerbs) |
| Asphalt, Metal, Soil | 35,001 | 35,001 | unchanged |

Most of the vertices are the vertical faces: each band piece (sampled every 5 m, closer where its width changes) carries its own kerb, outer and end faces with their own normals.

**Worker time.** In the Mono harness (the same sources compiled optimised, run under Mono with `--debug`, best of 3, back to back), the slice's roads take 3.7 ms a chunk without the sidewalk pass and 49.1–49.3 ms with it: about 45.5 ms a chunk on a worker. The editor agrees: `RoadsCrossChunkBordersWithoutGaps` (all 1,303 chunks) took 9.5 s at D-04 and 68.2 s now. A pass during D-05 brought the harness from 57.6 to 49.1 ms (a bounding-box filter on the carriageway index; stamp sets in place of hash sets). What is left is spread: width sampling 14.4 ms (obstacle gaps 9.1, inside a building 3.6), free spans 8.8, pieces 14.8 (the overlap check 8.5), corner samples 2.9 (instrumented build, so the parts add to more than the total). See K-36.

**Garbage.** In the editor's second pass the roads stage allocates 2,584 KB for 12,725 KB of mesh arrays over the 12 chunks (D-04: 1,612 / 1,999 KB). The air runs allocated 33.1 MiB with 16–17 collections (D-04: 9.6 MiB, 10). The likely cause is the pooled mesh arrays and per-thread lists growing to the larger meshes on their first use; not measured separately. No apply step met a collection in any run.

**Loads held.** The streamer holds the apply while loads are still building. Air runs held 7–8 frames, at most 4–5 in a row (D-04: 1–2, 1). The street runs held 15–17 frames at the start (D-04: 3), when the first ring builds. This is where the worker time shows.

**Runs, `slice-crossing-air`.** Warm runs; GPU over rows 900–2399, with the same reading as D-01 to D-04 (re-checked on D-04's `-171317` and `-171211`, which it reproduces exactly).

| Run | Code | GPU over the slice mean / p50 / p95 / max | Outside the slice (mean) | Main-thread frames over 4 ms after set-up (near streaming) | Max apply frame |
|---|---|---|---|---|---|
| `-121740` | D-05 | 6.81 / 6.75 / 10.60 / 11.97 ms | 2.70 ms | 0 | 1.17 ms |
| `-121846` | D-05 | 6.91 / 7.17 / 9.19 / 12.02 ms | 2.74 ms | 0 | 1.28 ms |
| `-121952` | D-05 | 7.03 / 7.11 / 9.56 / 11.83 ms | 3.50 ms | 2 (1): row 888 4.96 ms near streaming; row 1341 12.06 ms with 2.16 ms of CPU | 1.21 ms |
| `-122341` | D-05, profiled from frame 900 | 7.29 / 7.51 / 10.64 / 12.65 ms | 2.78 ms | 1 (0): row 903, 13.31 ms with 1.83 ms of CPU, as the capture started | 1.59 ms |

- **GPU:** p50 over the slice 6.75–7.17 ms unprofiled, inside D-04's 6.60–7.18; the profiled run 7.51 ms, inside D-02's 6.9–7.9. p95 9.2–10.6 ms, between D-04's two groups (8.5–8.7 and 11.2). Six times the road vertices did not show as GPU time from the air.
- **Draw calls:** batches p50 26, SetPass calls p50 10, as at D-04. Triangles in the frame p50 170,908–176,373 (D-04 65,168), peak 963–969 thousand at set-up (D-04 575–583 thousand).
- **Memory:** graphics memory peak 201–253 MB, end 100–111 MB (D-04 201–253 / 98). System memory peak 466–519 MB.
- **Main thread:** p50 1.71–2.21 ms. The capture's busy time p50 2.15 ms, p95 3.25, max 3.97 ms: no captured frame busy over 4 ms. The frames over 4 ms in the table had normal CPU, so the thread was not running (K-18).
- **Machine:** other load was not recorded for these runs (a single `top` sample reads 0 % for every process, so it was not used).

**Street level, `kawran-farmgate-street`.**

| Run | GPU after set-up p50 / p95 / max | Main thread p50 / max | Main-thread frames over 4 ms (near streaming) | Batches p50 / max | Triangles p50 / max | Graphics memory peak / end |
|---|---|---|---|---|---|---|
| D-04 (`-171612`, `-171718`) | 7.50–8.09 / 12.35–12.65 / 14.72–15.01 ms | 2.40–2.45 / 5.00–5.97 ms | 1 and 3 (0) | 46 / 77 | 147,572–147,730 / 235,037 | 213–265 / 108 MB |
| `-122103` | 8.41 / 12.27 / 15.41 ms | 2.17 / 9.66 ms | 2 (0): rows 48 and 166, 5.31 and 9.66 ms with 2.02 and 2.24 ms of CPU | 46 / 77 | 423,182 / 739,301 | 220 / 116 MB |
| `-122209` | 7.87 / 11.30 / 14.13 ms | 2.33 / 6.02 ms | 0 | 46 / 77 | 435,704 / 739,301 | 220 / 116 MB |

- Triangles at street level are 2.9 times D-04's (p50) and 3.1 times at peak; batches are unchanged. GPU p50, p95 and max stay within D-04's figures. The slowest frame keeps 1.3 ms of the 60 Hz budget, as at D-03.
- The first run had one presentation stall (CPU frame time 637.89 ms, K-11).
- No garbage collection in either run.

**Builds:** macOS development player 212.7 MB (223,004,681 bytes); Windows Mono release 131.9 MB by Unity's report (138,208 KB on disk), 18.2 s, 0 errors, 0 C# warnings, the same 20 URP shader-stripping warnings, not run (K-02).

## Farmgate representative assets (directive Milestone 2, 2026-11-03)

Development player at 1280×720, vSync on, on the M1, clock held at noon. The street pass adds a structures mesh a chunk (viaduct, footbridges, dividers; always drawn, casts shadows) and a near and a far mesh for each 125 m cell (poles, cables, lamps, trees); facades go into the building mesh. Each is a new draw and a new apply step (street cells two a step).

**Mesh, over the 12 slice chunks** (scratchpad survey on the final code): structures 131,860 triangles; near cells 844,226; far cells 40,380; street vertices 686,960. Facades: 76 buildings in the validation area, 81,824 of the area's 92,016 building triangles. Kit parts near / far: utility pole with arm and transformer 350 / 24; street light 114 / 36; trees 1,472–1,832 / 68.

**Worker time and garbage** (Mono harness, best of 3, same sources against HEAD's): roads with the street pass 38.3 ms a chunk against 30.5; buildings with facades 2.1 against 0.6 ms. Garbage over 80 city chunks: warm 12.3 against 12.1 MiB; cold 58.3 against 26.2 MiB (pools and per-thread lists growing on first use). Over the 12 slice chunks, roads and street allocate 2,700 KB against 2,592. In the editor's allocation test the roads stage allocates 2,691 KB for 30,758 KB of mesh arrays (D-05: 2,584 for 12,725). See K-38.

**Runs, `slice-crossing-air`** (GPU over rows 900–2399, as before).

| Run | GPU over the slice mean / p50 / p95 / max | Outside the slice (mean) | Main-thread frames over 4 ms (near streaming) | Max apply frame |
|---|---|---|---|---|
| `-135013` | 7.83 / 7.91 / 11.30 / 13.39 ms | 2.90 ms | 2 | 2.05–2.25 ms over the three runs |
| `-135119` | 7.73 / 7.82 / 11.43 / 13.98 ms | 2.92 ms | 1 | |
| `-135225` | 7.40 / 7.53 / 10.29 / 11.33 ms | 2.78 ms | 26 (4); the rest had normal CPU time, so the thread was not running (K-18) | |

- **GPU:** p50 over the slice 7.53–7.91 ms against D-05's 6.75–7.17; p95 10.3–11.4 ms against 9.2–10.6. Outside the slice unchanged (2.78–2.92 against 2.70–3.50).
- **Draw calls:** batches p50 32–35 (D-05 26); SetPass p50 10, unchanged. Triangles in the frame p50 204–212 thousand (171–176), peak 1.65–1.69 million at set-up (963–969 thousand).
- **Memory:** graphics memory peak 211–265 MB, end 105 MB; system memory peak 544–551 MB.
- **Main thread:** p50 1.79–2.56 ms. Busiest apply frame 2.05–2.25 ms (D-05 1.17–1.28), the busiest apply step 0.43–0.69 ms (a street cell), inside the 4 ms rule.
- **Garbage:** 72.5 MiB a run with 26–27 collections (D-05 33.1 MiB, 16–17); no apply step met a collection. Loads held 8–9 frames, at most 4 in a row (7–8).

**Street level, `kawran-farmgate-street`.**

| Run | GPU p50 / p95 / max | Main thread p50 | Frames over 4 ms (near streaming) | Batches p50 / max | Triangles p50 / max | Graphics memory peak / end | Held frames | Max apply frame |
|---|---|---|---|---|---|---|---|---|
| D-05 (`-122103`, `-122209`) | 7.87–8.41 / 11.30–12.27 / 14.13–15.41 ms | 2.17–2.33 ms | 2 and 0 (0) | 46 / 77 | 423–436 thousand / 739 thousand | 220 / 116 MB | 15–17 | — |
| `-135331` | 7.57 / 11.85 / 17.46 ms | 1.99 ms | 1 (0) | 122 / 160–164 | 1.52–1.56 million / 2.02–2.06 million over both runs | 240–294 / 133 MB over both | 14 | 2.07–2.40 ms over both |
| `-135436` | 8.19 / 12.67 / 15.87 ms | 2.31 ms | 3 (0) | | | | 16 | |

- GPU p50 is within D-05's spread; p95 rises about 0.4 ms. One frame of 7,199 in `-135331` went over the 60 Hz budget (row 2625, 17.46 ms); none in `-135436`.
- Triangles are 3.6 times D-05's at p50 and stay under the 3 million budget; batches 2.7 times (each cell's near mesh is its own draw).
- No garbage collection in either run (96 bytes).

**Builds:** Windows Mono release succeeded, 131.9 MB by Unity's report (138,310,735 bytes; 138,268 KB on disk), 13.5 s, 0 errors, 0 C# warnings, the same 20 URP stripping warnings, not run (K-02). macOS development player 212.8 MB (223,087,601 bytes).

## K-37 P-1: lane-based widths, narrowing only (2026-11-03)

Same player and settings as M2. Narrower carriageways change the road, sidewalk, divider and street meshes; street cell meshes are now bounded by their own extent instead of the cell square plus 20 m, so cells out of view are culled more often.

**Runs, `slice-crossing-air`** (GPU over rows 900–2399):

| Run | GPU over the slice mean / p50 / p95 / max | Outside the slice (mean) | Main-thread frames over 4 ms (near streaming) | Max apply frame |
|---|---|---|---|---|
| `-151133` | 7.06 / 6.50 / 11.30 / 23.35 ms | 2.70 ms | 2 (1) | 2.32 ms |
| `-151240` | 7.11 / 7.11 / 9.81 / 12.10 ms | 2.95 ms | 0 | 2.45 ms |
| `-151347` | 8.15 / 8.09 / 11.45 / 14.15 ms | 4.45 ms | 1 (0) | 2.11 ms |

- **GPU:** p50 over the slice 6.50–8.09 ms, against M2's 7.53–7.91: no measurable change. The third run was slower everywhere: 4.45 ms outside the slice against 2.70–2.95, the fanless machine warming over back-to-back runs. The first run had one burst of 6 frames over 16.7 ms (rows 1225–1230, up to 23.35 ms, main thread 1.2–3.4 ms), not seen in the other two runs.
- **Draw calls:** batches p50 28 (M2 32–35); SetPass p50 10, unchanged. Triangles p50 198–201 thousand (M2 204–212), peak 1.61–1.64 million.
- **Memory:** graphics memory peak 211–265 MB, end 105 MB; system memory peak 534–550 MB.
- **Main thread:** p50 1.84–2.01 ms; busiest apply frame 2.11–2.45 ms (M2 2.05–2.25), busiest apply step 0.60–0.64 ms.
- **Garbage:** 70.0–72.8 MiB a run with 23–25 collections (M2 72.5 MiB, 26–27); no apply step met a collection.

**Street level, `kawran-farmgate-street`:**

| Run | GPU p50 / p95 / max | Main thread p50 | Frames over 4 ms (near streaming) | Batches p50 / max | Triangles p50 / max | Graphics memory peak / end | Held frames | Max apply frame |
|---|---|---|---|---|---|---|---|---|
| `-151453` | 7.74 / 11.91 / 20.10 ms | 2.47 ms | 1 (0) | 105 / 134 | 1.47 / 1.78 million | 294 / 133 MB | 22 | 2.26 ms |
| `-151559` | 7.48 / 12.65 / 15.92 ms | 1.85 ms | 0 | 101 / 138 | 1.44 / 1.96 million | 294 / 133 MB | 22 | 2.21 ms |

- GPU p50 7.48–7.74 ms against M2's 7.57–8.19. The first run had 7 frames over 16.7 ms in a row (rows 2602–2608, up to 20.10 ms, main thread 2.1–2.5 ms); the second had none. M2 had 1 over 2 runs.
- Batches p50 101–105 against 120–122, triangles p50 1.44–1.47 million against 1.52–1.56: tighter cell bounds cull more.
- No garbage collection in either run. Held frames 22 against 14–16.

Worker time was not re-measured.

**Builds:** Windows Mono release succeeded, 131.9 MB (138,312,943 bytes), 19.0 s, 0 errors, 0 C# warnings, the same 20 URP stripping warnings, not run (K-02).

## K-37 P-2 pier rules (2026-11-04)

Record: [P2/bench.txt](../PRODUCTION/P2/bench.txt).
- **Player:** HEAD's (2dd7472) and the P-2 development player, run for run, 3 air and 3 street each, after a discarded run of each (AC power, lid open). **No change measured:**
  - main thread p50: air 1.86–2.09 ms (HEAD 1.91–2.16), street 2.08–2.45 ms (2.00–2.44);
  - GPU mean: air 5.03–6.29 ms (4.32–6.26), street 7.92–8.83 ms (8.00–8.53);
  - apply max frame 2.54–3.01 ms (2.06–3.26);
  - kits and garbage the same.
  - One HEAD street run is anomalous (906 MB system memory, GPU samples missing) and is kept in the record.
  - An earlier sequential pair showed the P-2 main thread 0.3–0.6 ms slower. The P-2 runs there followed an hour of editor runs, and the interleaved runs do not reproduce it.
- **Roads worker (K-38),** Mono harness, best of 5 passes, 3 runs:
  - the 64 chunks with an elevated track: 42.97 → 53.04 ms a chunk (+23 %);
  - the slice: 46.2 → 49.9 ms (+8 %);
  - reading the neighbours' tracks: about 1.5 ms a chunk once, then cached.
- **Builds:** macOS development player 223,431,133 bytes. Windows 138,452,463 bytes, 0 errors, not run (K-02).

## D-11a flyover supports and closed decks (2026-11-04)

- **Roads worker (K-38),** Mono harness, best of 5 passes, 3 runs interleaved with main (`3348cc6`), record [INFRA/worker-d11a.txt](../PRODUCTION/INFRA/worker-d11a.txt):
  - the slice: 49.48–49.63 → 54.63–55.49 ms a chunk (+5.0–6.0 ms, +10–12 %);
  - the 461 chunks with a flyover deck: 34.33–34.60 → 36.72–36.80 ms (+2.2–2.4 ms, +6–7 %).
- **Geometry:** in the slice, 10,047 m of girder, 291 piers and 24 portal frames, in the structure mesh (range 0, casts shadows).
- **Player bench** ([D11A/bench.txt](../PRODUCTION/D11A/bench.txt)): HEAD's (`3348cc6`) and the D-11a player, run for run, 3 air and 3 street runs each, after a discarded run of each. Lid open, AC power, 17:43–17:58.
  - **GPU, air path:** whole-path mean 4.93–5.68 ms, against 4.54–4.89 ms. The ranges do not overlap: about +0.4 to +0.8 ms, over the slice.
  - **GPU, street path:** 7.76–8.82 ms, against 8.41–8.87 ms: no change measured.
  - **Triangles p50:** street 2.44–2.46 M, against 2.35–2.39 M (+3 to +4 %); air 358 k, against 357 k.
  - **Batches p50:** street 158.2–160, against 155.5–158.8; air the same.
  - **Memory:** mesh end +1 MB; system memory inside the spread.
  - **Main thread p50:** overlapping and noisy, no change claimed. Apply max frame 2.14–3.05 ms (2.04–3.12).
  - **Garbage, collections and kits:** the same.
  - An earlier attempt (16:31–17:11) ran with the lid closed. 13 of 14 runs did not finish, and it is not used.
- **Builds:** macOS development player 223,468,469 bytes, 0 errors. Windows 138,475,983 bytes, 0 errors, not run (K-02).

## K-37 P-6 median islands (2026-11-05)

- **Roads worker** (Mono harness, best of 5, interleaved with main, [INFRA/worker-p6.txt](../PRODUCTION/INFRA/worker-p6.txt)): the first version cost +16.8 ms a chunk on the slice (+31 %), from sampling each island's footprint against every building. With a bounding-box prefilter (same output in all 1,303 chunks): slice +0.4 ms, Line 6 +0.8 ms, the 461 flyover chunks −0.1 ms, each inside the run-to-run spread. No change measured. After the machine restarted, absolute times are about 1.6× earlier reports (main's slice 89 ms a chunk, 55 ms before); only the interleaved comparison is used.
- **Player bench** ([P6/bench.txt](../PRODUCTION/P6/bench.txt)): main's (`79cc798`) and the P-6 player, run for run, 3 air and 3 street runs each, after a discarded run of each. Lid open, **on battery** (earlier benches were on AC). Air GPU mean 4.80–5.21 ms against 5.00–5.09: no change. Street: one of main's runs was disturbed (406 frames over 20 ms); no gain claimed. Street triangles +1–2 % at most. Garbage, collections and memory unchanged.
- **Re-run on AC** ([P6/bench-ac.txt](../PRODUCTION/P6/bench-ac.txt)): 6 air and 3 street runs of each player, two sets with the pair order swapped. Frames over 20 ms 0–2 for both, so the battery set's slow frames were not P-6. Air GPU mean in the normal GPU state P-6 4.10–4.20 ms, main 4.06–5.17; street 7.58–8.78 against 7.28–8.21. No change measured. Runs of both players can have GPU time raised over the whole path (a clock or power state: the start and end of the path, where nothing changed, rise too); such runs are not compared.

## E-04 and E-05 the driven car, the owner's models (2026-11-06)

- **The 0.01 s step when nobody drives** ([E04/bench.txt](../PRODUCTION/E04/bench.txt)): F-03 player against E-04 player, 8 runs a path in balanced order, AC, lid open, from frame 300. Street main-thread p50 2.43 → 2.46 ms, air 2.33 → 2.35 ms, inside the noise (K-56).
- **Driving against traffic alone:** street main-thread p50 3.06 → 3.36 ms, air 2.38 → 2.94 ms; GPU mean street 8.69 → 8.79 ms, air 4.99 → 5.27 ms; highest peak memory street 888 → 980 MB, air 711 → 771 MB. The colliders are most of the memory.
- **Against the 1.5 ms budget** (capture, profiler running, street, 2,000 frames): `Dad.Vehicle.Step` mean 0.07 ms, p99 0.17 ms, max 0.55 ms; all physics mean 0.35 ms, p99 0.86 ms, max 3.66 ms.
- **Not measured:** a Windows player, any machine but this M1, the air path under the profiler, texture memory in a profiler (computed: at most 1.33 MB more than the world's 16.33 MB).

## F-01 to F-03 traffic (2026-11-06)

- **Traffic off against on** ([F03/bench.txt](../PRODUCTION/F03/bench.txt)): one player, 8 runs a path in balanced order, AC, lid open, from frame 300. `slice-crossing-air`: main-thread p50 2.27 → 2.39 ms, GPU mean 4.87 → 5.01 ms, both inside the run-to-run spread. `kawran-farmgate-street`: main-thread p50 2.40 → 3.09 ms, every "on" run above every "off" run; GPU mean 8.45 → 8.55 ms. A first pass was discarded because other work ran beside it.
- **Against the 1.5 ms budget** (captures, profiler running): all traffic markers mean 0.10 ms in the air and 0.45 ms on the street. On the street 94 of 2,000 frames go over the budget (p99 2.03 ms, max 2.79 ms), because each of the 10 simulation steps a second runs whole in one frame: K-59, not fixed. The air capture has single frames of 8.0, 6.9 and 5.4 ms, not traced.
- **Not measured:** traffic in a Windows player, on any machine but this M1, or with the player driving.

## City rebuild, the 15.5 km² map (2026-11-07)

First measurements on the new map ([../PRODUCTION/CITY/bench.md](../PRODUCTION/CITY/bench.md)): final build, macOS development player, M1, 1280×720, vSync on, AC, lid open; three paths at Low, Medium and High, two runs each, the second round reversed. No earlier bench exists on this map, so there is no before and after. Another session (the world reveal trailer) rendered audio in Node from about 22:49 to 22:53, during the last two `central-avenue-street` runs of round 2 (Medium 22:49, Low 22:50); their figures are no worse than round 1's, but they were not run on a quiet machine. Activity of that session before 22:49 is not known.

- **Frame time:** p50 at the 16.67 ms cap on every path and level; all 18 runs complete.
- **Main thread p50:** 2.95 to 5.01 ms; GPU mean: Low 6.7 to 7.9 ms, Medium 7.5 to 9.2 ms, High 9.4 to 14.5 ms (the slums-airport flight on High draws about 10.5 million triangles, p50).
- **Slow frames:** 0 to 12 frames over 33 ms a run, except the first `central-avenue-street` High run: 109 (main thread p95 10.8 ms); its repeat had 1. Not traced.
- **Memory:** graphics memory peak 268 to 404 MB, system memory peak 811 to 1,012 MB.
- **Texture memory, profiled** (bench texture census): 99.7 MB on High and 72.1 MB on Low in all. Render targets 59.0 / 31.4 MB; world library 16.3 MB; the owner's city model pictures 17.0 MB; terrain, Coach, Unity's own and small textures 7.4 MB. Project art about 36.7 MB, plus the people. Owner decision 2026-11-07 (K-63): the art texture budget is 48 MB on the GPU, all art textures together (world library, the owner's city model pictures, terrain, vehicles, people); render targets are not counted. The world library keeps its own 24 MB limit. Enforced by `MaterialLibraryTests.ArtTexturesAreWithinTheBudget`: 43.96 MB counted on 2026-11-07 (every file, including 8 city pictures the bench did not load), about 4 MB of headroom.
- `hub-rich-air` was raised the same day (it flew through the hub's towers, K-66), so its figures are for the raised path only.
- **Not measured:** a Windows player, the target i3 laptop, traffic or colliders on this map, the player driving.

## Measured so far

| Metric | Value | Where |
|---|---|---|
| City export, 1,303 chunks | 1.9 s, 179 MB peak | Node 26.9, M1 |
| World data in Unity | 19.3 MB (22 MB on disk) | `Assets/StreamingAssets/World` |
| Decode per chunk, Node reference decoder | p50 0.36 ms, p95 1.24 ms, max 4.1 ms | Node |
| Decode per chunk, C# `Dad.World` reader | p50 0.061 ms, p95 0.22 ms, max 1.6 ms; 113 ms for all 1,303 | Unity's bundled Mono, outside the editor, M1. Not a player-build measurement |
| Same, inside the editor (EditMode test) | p50 0.124 ms, p95 0.41 ms, max 8.0 ms; 224 ms for all 1,303 | Editor Mono JIT in debug mode, M1 |
| Project creation and import | template project 98 s; repo project first import + Boot scene 76 s; EditMode run 13 s; Windows build 69 s (118 s with editor start) | M1, batch mode |
| three.js build, for context | 16.7 ms median frame at the 60 Hz cap, teleport 807–817 ms | Chrome on the M1, `dhaka-after-dark/docs/p2-record.md` |
| Builds | Windows Mono release 138 MB on disk at M2 (13.5 s incremental); macOS arm64 development 213 MB at M2 (91 s first at D-03) | M1, batch mode |

## Budgets (targets, HIGH tier, 60 FPS, to be measured)

| Budget | Target | How it is measured |
|---|---|---|
| CPU main thread | ≤ 10 ms; per-system split in [../ARCHITECTURE/UNITY_ARCHITECTURE.md](../ARCHITECTURE/UNITY_ARCHITECTURE.md) | Profiler, fixed camera paths |
| GPU | ≤ 12 ms at 1440p | Profiler GPU module or RenderDoc on Windows |
| Chunk streaming | No main-thread spike over 4 ms from a chunk; cell visible within 1 s at 15 m/s | `Dad.Streaming.Apply` marker; `streamingMaxApplyFrameMs` in the bench summary (1.4–1.9 ms with debug lines; 1.5–1.8 ms with ground; 0.8–1.4 ms with roads; 1.1–1.3 ms with buildings; 1.5–1.7 ms with overlays; 0.9–2.2 ms at the Phase C exit; 0.9–1.2 ms after C-07, once 3.66 ms while the main thread was not running; 0.8–1.2 ms with the lit shader, D-01). The whole main-thread frame is checked too: `mainThreadMs` after the set-up frames, with frames over 4 ms attributed in a capture (`Tools/capture-report.sh`) |
| Memory | ≤ 6 GB process on HIGH; ≤ 3 GB on LOW | Memory Profiler |
| Draw calls (SRP batches) | ≤ 2,000 on HIGH | Frame Debugger |
| Triangles in view | ≤ 3 M on HIGH | Rendering statistics |
| Texture memory | ≤ 2 GB on HIGH; ≤ 1 GB on LOW | Memory Profiler |
| Save | ≤ 200 ms, off the main thread | Test timing |
| Loading, boot to play | ≤ 20 s on an SSD | Stopwatch log in build |
| Build size | ≤ 4 GB for the full game | Build report |

## Quality tiers (targets; no hardware tested)

| Tier | Intended hardware (to be validated) | Render scale | Shadows | Traffic and crowd caps | Draw distance |
|---|---|---|---|---|---|
| LOW | 4–6 cores, 8 GB RAM, 4 GB VRAM integrated or entry GPU | 0.75 | Main light, 1 cascade, 50 m | 25 % | 600 m |
| MEDIUM | 6 cores, 16 GB, 6–8 GB VRAM (GTX 1660 / RX 6600 class) | 1.0 at 1080p | 2 cascades, 120 m | 50 % | 1,000 m |
| HIGH | 8 cores, 16–32 GB, 8–12 GB VRAM (RTX 3060–4070 class) | 1.0 at 1440p | 4 cascades, 250 m | 100 % | 1,500 m |
| ULTRA | 8+ cores, 32 GB, 12 GB+ VRAM | 1.0 at 4K or 1440p high refresh | 4 cascades, 400 m, soft | 125 % | 2,500 m |

**What cannot be tested locally:**
- Any Windows frame time, GPU or driver behaviour.
- IL2CPP builds.
- Gamepad behaviour on Windows.

These need a Windows machine. Cloud Windows GPU instances are an option, but they cost money and need owner approval.
