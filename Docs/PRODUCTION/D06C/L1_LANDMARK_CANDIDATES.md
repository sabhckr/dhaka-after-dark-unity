# D-06c: Level 1 landmark candidates (for owner review)

Status: **candidate list only, awaiting the owner's review.** No landmark geometry is built. The roadmap row for
D-06c asks for "Level 1 landmarks from an owner-reviewed list": this is the list to review, not the decision.

Generated 2026-11-03 from `Assets/StreamingAssets/World/landmarks.json` (1,986 items, OpenStreetMap-derived, the
same file the game reads). The full table is `L1_LANDMARK_CANDIDATES.csv` beside this file, with an empty
`owner_decision` column to fill in.

## What is listed (271 rows)

- **Every landmark in the playable slice** (chunks c_6..9 / -28..-30, world east 3,000-5,000 m, north
  13,500-15,000 m): 58 items. These are the only ones the player can see today (streaming is limited to the
  slice). By category: places of worship 20, hospitals 19, government 7, hotels 5, universities 3, metro
  stations 2 (Farmgate, Kawran Bazar), apartment hotel 1, attraction 1 (SAARC Fountain).
- **City-wide, the categories most likely to be landmarks** (213 more): universities 57, historic monuments 25,
  stations 24, tourism attractions 23, memorials 19, museums 12, archaeological sites 8, historic pillories 8,
  artworks 6, aquariums 5, historic castles 4, tombs 4, aerodromes 3, stadiums 3, city gates 2, other historic 3,
  viewpoints 2, zoos 2, theme park 1.
- Left out city-wide (not listed, can be added on request): places of worship outside the slice (1,002),
  hospitals (329), government offices (206), hotels, guest houses, hostels and serviced apartments.

## Columns

`id` (OSM id as in the data), `name` (exactly as in the data: often Bengali only; nothing is translated or
invented), `category`, `east_m` / `north_m` (world metres), `chunk`, `in_slice`, `listed_because`,
`owner_decision` (empty).

## Decisions needed from the owner

1. Which rows are Level 1 (modelled individually); the rest stay generic buildings with their far and near
   facades.
2. How references for the chosen ones are sourced (open decision from D-06b): own photographs, licensed images,
   or plan and height data only. No reference has been gathered.
3. Brands: several rows are hotels, hospitals and companies under their trading names. Their buildings can be
   modelled, but no name, logo or livery will appear in the game without authorization (the standing rule on
   real trademarks); signboards stay blank, as everywhere else.
