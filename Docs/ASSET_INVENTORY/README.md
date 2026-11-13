# Asset inventory

Every requirement in the owner's detailing directive ([../ART_DIRECTION/DETAILING_DIRECTIVE.md](../ART_DIRECTION/DETAILING_DIRECTIVE.md)), one row each, with what the project has today. The rules for columns, statuses, priorities and deduplication are in [../ART_DIRECTION/DETAILING_STANDARDS.md](../ART_DIRECTION/DETAILING_STANDARDS.md), section 5; this page only says where things are.

The CSVs are the source. Edit them directly (one row per requirement, ids never reused), then run the check.

## Files

| File | Code | Rows |
|---|---|---|
| Roads.csv | RD | 70 |
| Footpaths.csv | FP | 35 |
| Utilities.csv | UT | 55 |
| Buildings.csv | BD | 102 |
| Metro.csv | MT | 63 |
| Footbridges.csv | FB | 21 |
| ReligiousArchitecture.csv | RL | 72 |
| Vehicles.csv | VH | 194 |
| Characters.csv | CH | 84 |
| Shops.csv | SH | 47 |
| Vegetation.csv | VG | 37 |
| PublicSpaces.csv | PS | 18 |
| Transport.csv | TR | 26 |
| Signage.csv | SG | 20 |
| Construction.csv | CS | 21 |
| Materials.csv | MA | 25 |

Ids are `<CODE>-<SUB>-<NNN>`, for example `RD-SUR-001`.

## Check

```
/Library/Developer/CommandLineTools/usr/bin/python3 -I Tools/inventory-check.py
```

It prints counts by status and file and exits 1 on any problem (bad header, id, status or priority, an empty cell, a repeated description within a subcategory, or a status above NOT STARTED without a validation note).

## Status on 2026-11-06

| NOT STARTED | RESEARCHED | BLOCKED | IN PROGRESS | IMPLEMENTED | TESTED | APPROVED | Rows |
|---|---|---|---|---|---|---|---|
| 673 | 0 | 2 | 62 | 107 | 46 | 0 | 890 |

F-03 (2026-11-06) marked 108 vehicle rows, on NPC vehicles only: see [Docs/PRODUCTION/F03_TRAFFIC.md](../PRODUCTION/F03_TRAFFIC.md). Driving, entering and damage rows stay NOT STARTED.

Everything else above NOT STARTED is earlier work, from C-02 on and the directive's Milestone 2: roads and their markings, footpaths, drains and covers, ground, building walls and plaster, the material layers, utility poles and cables, street lights, dividers, the Line 6 viaduct, footbridges, and the validation area's facades and shopfronts; each such row names its test, golden view or known issue in `Validation`. Vehicles, characters, stations, religious architecture and sign text are NOT STARTED. Only the owner sets APPROVED.
