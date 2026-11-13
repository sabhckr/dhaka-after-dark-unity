# K-37 survey after P-1 (narrowing only)

The same survey as the files one level up, re-run after P-1 was applied on 2026-11-03: an estimated,
one-way, lanes-tagged road takes lanes × 3.5 m when that is narrower than its class width
(`Runtime/World/RoadWidths.cs`). Rebuild with `Tools/k37/run.sh <out>` and copy these files here by hand.

The files one level up are the evidence for the original report (state at commit 441a101) and are left
as they were. In these files, every width labelled "estimated" or "est." is the width the road is now
built at: the lanes-based width where P-1 applies, else the exporter's class width. The plans' grey
band is still lanes × 3.0 m.

- `groups.txt`: groups after P-1. Group A (K37-10, K37-52 to K37-58) is now clear: 22 clear in all.
  A′ 4, B 2, C 12, D 2, E 24 and the spanned junction place are unchanged.
- `lanes.txt`: city-wide count. 726 ways are narrower; 244 one-way, estimated, lanes-tagged ways keep
  their class width (123 because their lanes would widen them, 121 because lanes × 3.5 m equals it).
- `pier-register.csv`, `register-table.md`, `plan-1.svg` to `plan-7.svg`: as one level up.

No pier, deck, rail alignment, road centre line or building moved. P-2 (pier rule fixes) is not applied.
