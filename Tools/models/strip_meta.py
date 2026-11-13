#!/usr/bin/env python3
# Takes text, time and EXIF records out of the project's own picture files without touching their pixels, and writes the metadata record (brief of 2026-11-06, phase 12).
# PNG: drops tEXt, zTXt, iTXt, eXIf and tIME chunks, keeps every other chunk byte for byte. JPEG: drops APP1 to APP15 and COM segments, keeps the image data byte for byte. GUIDs live in the .meta
# files beside them, which are not touched. Files in ~/Downloads are never read here. Reports what it changed; run it with Unity closed.
#   python3 -I Tools/models/strip_meta.py
import json, os, struct, sys

HERE = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OWNER = 'Ishak Hasan Sabbir'
DROP = {b'tEXt', b'zTXt', b'iTXt', b'eXIf', b'tIME'}

def png(b):
    out, o, n = bytearray(b[:8]), 8, 0
    while o < len(b):
        l = struct.unpack('>I', b[o:o + 4])[0]
        kind = b[o + 4:o + 8]
        if kind in DROP: n += 1
        else: out += b[o:o + 12 + l]
        o += 12 + l
    return bytes(out), n

def jpeg(b):
    out, o, n = bytearray(b[:2]), 2, 0
    while o < len(b) - 1:
        if b[o] != 0xFF: return b, 0
        m = b[o + 1]
        if m == 0xDA:
            out += b[o:]
            break
        l = struct.unpack('>H', b[o + 2:o + 4])[0]
        if 0xE1 <= m <= 0xEF or m == 0xFE: n += 1
        else: out += b[o:o + 2 + l]
        o += 2 + l
    return bytes(out), n

def main():
    changed = scanned = 0
    for root in ('Assets/_Project',):
        for r, _, fs in os.walk(os.path.join(HERE, root)):
            for f in fs:
                low = f.lower()
                fn = png if low.endswith('.png') else jpeg if low.endswith(('.jpg', '.jpeg')) else None
                if not fn: continue
                p = os.path.join(r, f)
                b = open(p, 'rb').read()
                scanned += 1
                new, n = fn(b)
                if n:
                    with open(p, 'wb') as o: o.write(new)
                    changed += 1
    table = json.load(open(os.path.join(HERE, 'Tools/worldgen/models.json')))
    record = dict(owner=OWNER, note='Project owner of this game and of the files made for it. Each source model keeps its own credit in Docs/ASSET_INVENTORY/CREDITS.md.', pictures_scanned=scanned, pictures_stripped=changed,
                  models=sorted(table['models']))
    with open(os.path.join(HERE, 'Docs/ASSET_INVENTORY/METADATA_RECORD.json'), 'w') as o: json.dump(record, o, indent=1)
    print('pictures scanned %d, stripped %d' % (scanned, changed))

if __name__ == '__main__': main()
