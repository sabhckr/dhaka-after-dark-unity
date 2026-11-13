# City rebuild benchmark (2026-11-07)

macOS development player (Builds/MacBench, the final build), MacBook Air M1, 8 GB, 1280x720 window, vSync on (so frame time sits at the 16.67 ms cap), AC power, lid open, no other Unity editor or player running. Another session (the world reveal trailer) rendered audio in Node from about 22:49 to 22:53, during the last two `central-avenue-street` runs of round 2 (Medium 22:49, Low 22:50); their figures are no worse than round 1's, but they were not run on a quiet machine. Activity of that session before 22:49 is not known. Each path at each quality level twice; the second round in reverse order. Command: `Tools/bench.sh <path> --quality <low|medium|high>`. No traffic, no colliders. Folders under `Evidence/local/bench-*` (not in git):

- central-avenue-street low: Evidence/local/bench-central-avenue-street-20261107-223528
- central-avenue-street medium: Evidence/local/bench-central-avenue-street-20261107-223636
- central-avenue-street high: Evidence/local/bench-central-avenue-street-20261107-223745
- hub-rich-air low: Evidence/local/bench-hub-rich-air-20261107-223859
- hub-rich-air medium: Evidence/local/bench-hub-rich-air-20261107-223947
- hub-rich-air high: Evidence/local/bench-hub-rich-air-20261107-224033
- slums-airport-air low: Evidence/local/bench-slums-airport-air-20261107-224119
- slums-airport-air medium: Evidence/local/bench-slums-airport-air-20261107-224205
- slums-airport-air high: Evidence/local/bench-slums-airport-air-20261107-224251
- slums-airport-air high: Evidence/local/bench-slums-airport-air-20261107-224337
- slums-airport-air medium: Evidence/local/bench-slums-airport-air-20261107-224422
- slums-airport-air low: Evidence/local/bench-slums-airport-air-20261107-224508
- hub-rich-air high: Evidence/local/bench-hub-rich-air-20261107-224554
- hub-rich-air medium: Evidence/local/bench-hub-rich-air-20261107-224640
- hub-rich-air low: Evidence/local/bench-hub-rich-air-20261107-224726
- central-avenue-street high: Evidence/local/bench-central-avenue-street-20261107-224812
- central-avenue-street medium: Evidence/local/bench-central-avenue-street-20261107-224918
- central-avenue-street low: Evidence/local/bench-central-avenue-street-20261107-225024

| Path | Quality | Run | Complete | Frame p50 / p99 ms | Over 33 ms | Main thread p50 / p95 ms | GPU mean / p95 ms | Batches p50 | Triangles p50 | Graphics memory peak MB | System memory peak MB |
|---|---|---|---|---|---|---|---|---|---|---|---|
| central-avenue-street | low | 1 | yes | 16.67 / 17.65 | 12 | 3.56 / 5.77 | 7.47 / 10.99 | 316 | 2031 k | 401 | 942 |
| central-avenue-street | medium | 1 | yes | 16.67 / 17.66 | 5 | 4.02 / 5.54 | 8.45 / 13.06 | 350 | 2538 k | 404 | 881 |
| central-avenue-street | high | 1 | yes | 16.67 / 33.65 | 109 | 5.01 / 10.84 | 12.84 / 19.31 | 500 | 4492 k | 348 | 922 |
| hub-rich-air | low | 1 | yes | 16.67 / 18.48 | 1 | 3.86 / 5.50 | 6.66 / 10.09 | 297 | 2076 k | 268 | 811 |
| hub-rich-air | medium | 1 | yes | 16.67 / 16.91 | 0 | 3.86 / 5.38 | 7.52 / 10.88 | 376 | 2753 k | 341 | 951 |
| hub-rich-air | high | 1 | yes | 16.67 / 17.63 | 0 | 4.09 / 4.85 | 9.83 / 13.95 | 558 | 4373 k | 300 | 1012 |
| slums-airport-air | low | 1 | yes | 16.67 / 17.59 | 1 | 3.28 / 4.34 | 7.85 / 12.00 | 225 | 2348 k | 318 | 889 |
| slums-airport-air | medium | 1 | yes | 16.67 / 17.42 | 1 | 3.76 / 4.46 | 9.18 / 11.26 | 243 | 4374 k | 291 | 887 |
| slums-airport-air | high | 1 | yes | 16.67 / 17.65 | 0 | 3.97 / 5.11 | 14.54 / 20.21 | 287 | 10566 k | 342 | 966 |
| slums-airport-air | high | 2 | yes | 16.67 / 17.56 | 0 | 3.59 / 4.16 | 14.42 / 20.06 | 289 | 10470 k | 306 | 953 |
| slums-airport-air | medium | 2 | yes | 16.67 / 16.67 | 1 | 3.36 / 4.08 | 8.29 / 10.71 | 242 | 4377 k | 335 | 953 |
| slums-airport-air | low | 2 | yes | 16.67 / 17.56 | 0 | 3.77 / 4.58 | 7.20 / 11.44 | 224 | 2351 k | 314 | 883 |
| hub-rich-air | high | 2 | yes | 16.67 / 17.57 | 1 | 4.59 / 5.57 | 9.52 / 14.28 | 552 | 4272 k | 300 | 957 |
| hub-rich-air | medium | 2 | yes | 16.67 / 16.72 | 0 | 4.59 / 5.21 | 8.00 / 12.25 | 374 | 2739 k | 345 | 950 |
| hub-rich-air | low | 2 | yes | 16.67 / 16.70 | 0 | 3.30 / 4.31 | 7.13 / 12.43 | 303 | 2093 k | 268 | 826 |
| central-avenue-street | high | 2 | yes | 16.67 / 16.69 | 1 | 4.23 / 5.03 | 9.35 / 11.31 | 499 | 4609 k | 316 | 909 |
| central-avenue-street | medium | 2 | yes | 16.67 / 16.67 | 1 | 3.19 / 4.37 | 8.56 / 13.14 | 347 | 2702 k | 360 | 953 |
| central-avenue-street | low | 2 | yes | 16.67 / 16.97 | 1 | 2.95 / 4.46 | 7.47 / 11.95 | 312 | 2046 k | 280 | 845 |

## Texture memory (profiled, 2026-11-07, K-63)

Same build plus the texture census, one run each: `central-avenue-street`, `hub-rich-air`, `slums-airport-air` on High, `hub-rich-air` on Low. All complete, all paths alike.

| Group | Textures | High MB | Low MB |
|---|---|---|---|
| Render targets (shadow maps, screen buffers) | 28 / 26 | 59.0 | 31.4 |
| Owner's city model pictures | 69 | 17.0 | 17.0 |
| World library (WorldAlbedo, WorldNormal, WorldMask, WorldMacro) | 4 | 16.3 | 16.3 |
| Other: terrain 2.7, Coach 0.7, Unity's own at least 1.8, small ones 2.2 | 93 | 7.4 | 7.4 |
| **Texture Memory counter** | 194 / 192 | **99.7** | **72.1** |

Largest pictures: the yard deck 2048² (2.67 MB), the yard's 2048×1024 (1.33 MB), terrain grass 1024² (1.33 MB), three building pictures 1024² (0.67 MB each). The people's pictures are not loaded in a bench.

