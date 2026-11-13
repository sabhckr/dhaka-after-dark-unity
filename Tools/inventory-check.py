#!/usr/bin/env python3
# Check the detailing inventory (Docs/ASSET_INVENTORY/*.csv) and print counts by status.
#
#   /Library/Developer/CommandLineTools/usr/bin/python3 -I Tools/inventory-check.py
#
# Checks: the header; ids of the form <FILE>-<SUB>-<NNN> with the file's own code, unique across
# all files; statuses and priorities from the allowed sets; no empty required cell; no repeated
# description within a subcategory (a part on several vehicle families is one row per family);
# every status above NOT STARTED carries a validation note.
# Exits 1 on any problem. Rules: Docs/ART_DIRECTION/DETAILING_STANDARDS.md, section 5.
import csv, os, re, sys
from collections import Counter

here = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
inv = os.path.join(here, 'Docs', 'ASSET_INVENTORY')
COLS = ['ID', 'Category', 'Subcategory', 'Description', 'Geometry', 'Texture', 'Functional', 'Priority',
        'Reusability', 'LOD', 'Status', 'Reference', 'Validation', 'Directive']
STATUSES = ['NOT STARTED', 'RESEARCHED', 'BLOCKED', 'IN PROGRESS', 'IMPLEMENTED', 'TESTED', 'APPROVED']
NEEDS_NOTE = {'RESEARCHED', 'BLOCKED', 'IN PROGRESS', 'IMPLEMENTED', 'TESTED', 'APPROVED'}
CODES = {'Roads.csv': 'RD', 'Footpaths.csv': 'FP', 'Utilities.csv': 'UT', 'Buildings.csv': 'BD', 'Metro.csv': 'MT',
         'Footbridges.csv': 'FB', 'ReligiousArchitecture.csv': 'RL', 'Vehicles.csv': 'VH', 'Characters.csv': 'CH',
         'Shops.csv': 'SH', 'Vegetation.csv': 'VG', 'PublicSpaces.csv': 'PS', 'Transport.csv': 'TR', 'Signage.csv': 'SG',
         'Construction.csv': 'CS', 'Materials.csv': 'MA'}

problems, ids, total = [], set(), Counter()
by_file = {}
for name in sorted(os.listdir(inv)):
    if not name.endswith('.csv'):
        continue
    code = CODES.get(name)
    if code is None:
        problems.append(f'{name}: not a known inventory file')
        continue
    with open(os.path.join(inv, name), newline='') as f:
        rows = list(csv.reader(f))
    if not rows or rows[0] != COLS:
        problems.append(f'{name}: header differs from {COLS}')
        continue
    seen, counts = set(), Counter()
    for i, cells in enumerate(rows[1:], start=2):
        if len(cells) != len(COLS):
            problems.append(f'{name}:{i}: {len(cells)} cells, expected {len(COLS)}')
            continue
        r = dict(zip(COLS, cells))
        where = f'{name}:{i} {r["ID"]}'
        if not re.fullmatch(code + r'-[A-Z]{3}-\d{3}', r['ID']):
            problems.append(f'{where}: id must be {code}-XXX-NNN')
        if r['ID'] in ids:
            problems.append(f'{where}: duplicate id')
        ids.add(r['ID'])
        for c in ['Category', 'Subcategory', 'Description', 'Geometry', 'Texture', 'Functional', 'Reusability', 'LOD', 'Reference', 'Directive']:
            if not r[c].strip():
                problems.append(f'{where}: empty {c}')
        if r['Status'] not in STATUSES:
            problems.append(f'{where}: status "{r["Status"]}" not one of {STATUSES}')
        if r['Priority'] not in ('P1', 'P2', 'P3', 'P4'):
            problems.append(f'{where}: priority "{r["Priority"]}" not P1-P4')
        if r['Status'] in NEEDS_NOTE and not r['Validation'].strip():
            problems.append(f'{where}: {r["Status"]} needs a validation note')
        key = (r['Subcategory'], r['Description'].strip().lower())
        if key in seen:
            problems.append(f'{where}: description repeated in {r["Subcategory"]}')
        seen.add(key)
        counts[r['Status']] += 1
    by_file[name] = counts
    total.update(counts)

print(f'{"file":28}' + ''.join(f'{s:>13}' for s in STATUSES) + f'{"rows":>7}')
for name, counts in by_file.items():
    print(f'{name:28}' + ''.join(f'{counts[s]:>13}' for s in STATUSES) + f'{sum(counts.values()):>7}')
print(f'{"all":28}' + ''.join(f'{total[s]:>13}' for s in STATUSES) + f'{sum(total.values()):>7}')
for p in problems:
    print('PROBLEM', p)
sys.exit(1 if problems else 0)
