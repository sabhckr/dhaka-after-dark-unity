# World layout and reference

Owner's world brief of 2026-11-06, sections 10 and 12. **Proposal, waiting for the owner's approval.** Nothing outside the Farmgate box is built or streamed until it is approved. The owner reads this as a page: https://claude.ai/artifact/TGiGAJquXdVgeEFdcCyaGr (private).

Made with `Tools/layout/run.sh` (reads the chunks, writes `layout.json` and `validate.txt`) and `Tools/layout/plan.py` (districts, stages, zones). Both are read-only. `WORLD/districts.csv` lists every chunk; `WORLD/validate.txt` is the raw check output. No road, rail, building or chunk data was changed.

## Coordinates

- 1 Unity unit = 1 metre. X is east, Z is north, Y is up.
- Origin: UTM zone 46N (EPSG:32646), easting 230,000, northing 2,615,000.
- Fixed world limits: E -2,500 to 16,500, N 4,500 to 30,500 (19 x 26 km). 1,303 chunks of 500 m.
- Chunk `c_ix_iy` has its south-west corner at (ix * 500, -(iy + 1) * 500). A chunk file's positions are centimetres from that corner.
- Ground 0 m, road 0.28 m, footpath 0.43 m, median 0.48 m. The ground is flat. Bridge and viaduct heights are rules from map tags, not measurements.
- Streaming: `ChunkStreamer.LoadRadius` 2, so at most 25 chunks load around the player. Only the 12 box chunks have ever loaded (`SliceOnly`, Slice 6,27,4,3).

## Stages

| Stage | Chunks | km² | Buildings | Road km | Data MB |
|---|---:|---:|---:|---:|---:|
| 1 The box | 12 | 3.0 | 5,893 | 71 | 0.2 |
| 2 Core | 160 | 40.0 | 69,214 | 806 | 3.1 |
| 3 City | 737 | 184.2 | 363,773 | 3,097 | 13.8 |
| 4 Fringe | 1,303 | 325.8 | 519,573 | 4,578 | 19.2 |

Figures are running totals. Stage 1 is what plays today.

## Districts

Every chunk is in exactly one district: the first whose rectangles hold its south-west corner (`Tools/layout/plan.py`).

| ID | District | Stage | Rectangles (E from-to, N from-to) | Chunks | km² | Buildings | Road km | Built on |
|---|---|---:|---|---:|---:|---:|---:|---:|
| D01 | Farmgate and Karwan Bazar | 1 | 3000..5000, 13500..15000 | 12 | 3.0 | 5,893 | 71 | 38.0 % |
| D02 | Dhanmondi and Lalmatia | 2 | 1000..3000, 12000..15000; 1500..3000, 11500..12000 | 27 | 6.8 | 15,780 | 128 | 37.1 % |
| D03 | Shahbag and Ramna | 2 | 3000..5500, 11500..13500 | 20 | 5.0 | 4,102 | 88 | 25.2 % |
| D04 | Moghbazar and Malibagh | 2 | 5000..7000, 13500..15000 | 12 | 3.0 | 8,623 | 76 | 37.4 % |
| D05 | Tejgaon and Hatirjheel | 2 | 4500..7500, 15000..17000 | 24 | 6.0 | 11,560 | 116 | 32.6 % |
| D06 | Agargaon and Sher-e-Bangla Nagar | 2 | 1000..4500, 15000..17500 | 35 | 8.8 | 10,458 | 157 | 22.5 % |
| D07 | Motijheel and Paltan | 2 | 5500..8000, 10500..13500 | 30 | 7.5 | 12,798 | 170 | 33.3 % |
| D08 | Old Dhaka | 3 | 1500..8000, 8000..11500; 500..1500, 9000..12000 | 70 | 17.5 | 61,736 | 342 | 34.1 % |
| D09 | Mohammadpur and Bosila | 3 | -2500..1000, 10500..17500 | 54 | 13.5 | 27,070 | 219 | 23.6 % |
| D10 | Mirpur | 3 | -2500..4000, 17500..24000 | 141 | 35.2 | 93,258 | 630 | 29.5 % |
| D11 | Cantonment and Airport | 3 | 4000..7000, 19500..25500 | 71 | 17.8 | 15,486 | 217 | 14.7 % |
| D12 | Banani, Gulshan and Baridhara | 3 | 4000..8500, 17000..21500 | 56 | 14.0 | 34,553 | 277 | 33.6 % |
| D13 | Uttara | 3 | -2500..8500, 24000..30500 | 185 | 46.2 | 62,456 | 606 | 17.5 % |
| D14 | Rampura, Badda and Khilgaon | 4 | 7000..11000, 13000..17000 | 58 | 14.5 | 29,230 | 237 | 23.1 % |
| D15 | Bashundhara and the north-east | 4 | 7000..16500, 17000..30500 | 252 | 63.0 | 35,063 | 601 | 6.4 % |
| D16 | Jatrabari, Demra and the south-east | 4 | 8000..16500, 8000..13000; 11000..16500, 13000..17000 | 202 | 50.5 | 67,905 | 470 | 14.5 % |
| D17 | South of the Buriganga | 4 | -2500..16500, 4500..8000 | 54 | 13.5 | 23,602 | 173 | 19.0 % |

## Density zones

Share of a chunk's ground under buildings. The right column is a proposal (decision 10), for buildings with no mapped height only. About 94 % of buildings have none, and most of those get about 9 m today.

| Zone | Ground built on | Chunks | km² | Buildings a chunk | Proposed unmapped height |
|---|---|---:|---:|---:|---|
| dense | 35 % or more | 316 | 79.0 | 869 | 15 to 21 m |
| urban | 22 to 35 % | 250 | 62.5 | 589 | 12 to 18 m |
| mixed | 10 to 22 % | 221 | 55.2 | 283 | 9 to 15 m |
| open | Under 10 % | 516 | 129.0 | 68 | 6 to 9 m |

## Check results (section 10)

Unity has only run the box. Everything else was checked by reading the data.

| Check | Result | Found |
|---|---|---|
| Connectivity | Pass | 68,629 edges; 99.6 % of nodes in one network; box has nothing cut off |
| Intersections | Partly | 441 junctions in the box; 41 closed directions from F-03's LaneProbe (decision 7) |
| Left-hand traffic | Pass | `TrafficKeepsToTheLeft` |
| Turning clearance | Partly | Greater roads in the box: 245 of 797 turns under 8 m, 24 at 8 to 12 m; radii estimated from lane curves |
| Flyovers connect | Fail | No ramps. City 1,620 raised ends meet a ground road with a step, 86 stop in the air; box 23 and 1; largest step 16 m |
| Floating, overlapping roads | Partly | Infra checker 67 known items in the box, none new; 1,065 of 65,235 m of box road line inside a ground building |
| Metro alignment | Pass | Two 22.0 km runs continuous in plan. Way 24226259 (3.3 km, 11 m, E 5,837 to 6,039) reads as the mainline railway: it meets lower rail at both ends |
| Station platforms | Not built | 16 stations stand as buildings only |
| Train clearance | Partly | Clears all 8 road decks it crosses by 25 m. Track centres under 3.5 m at 53 of 825 samples, tightest 1.54 m |
| Track elevation | Fail | 8 height steps between 11, 16, 21 and 31 m, largest 20 m |
| Train route | Not built | No train |
| Buildings on the ground | Pass | Box 5,893, 3 raised on purpose; city 50 of 519,573 raised |
| Road obstruction | Partly | The overlap above is mostly residential lanes; no footprint changed |
| Building scale | Partly | 6 % have a mapped height; a 260 m tower (Rahman Dom-Inno Avanzar, c_11_-54) looks wrong |
| Controlled variation | Pass | D-06, box only |

## Open decisions

1 to 9 are the list in the owner's MAP_DETAILS. Recommendations are on the page.

1. Play area (recommend Stage 2, benched first). 2. Heights and ramps (chosen heights, built ramps, flat ground). 3. Metro height (one height, 16 m). 4. Line 6 north twin heights (one height). 5. Station supports, P-4 (yes, after 3). 6. Farmgate frame (leave). 7. Road width caps (yes, after Stage 2). 8. Small bridges low (yes). 9. Piers on roads (keep listed, recount after 5 and 7). 10. Height rule by zone (yes). 11. Clean running line for the train (yes).
