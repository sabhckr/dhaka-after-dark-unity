"""K-37 plan sheets: north-up plans of the deck, roads, buildings and piers, current against proposed.
   plans.py <out dir>   (reads k37-final.json and geom.json there; writes plan-<n>.svg)"""
import json, math, os, sys

out = sys.argv[1]
rows = json.load(open(os.path.join(out, "k37-final.json")))
geom = json.load(open(os.path.join(out, "geom.json")))
R = 0.9
COL = {"A": "#2e8b57", "A'": "#9a9a2e", "B": "#7b3fa0", "C": "#2f6db5", "D": "#a0522d", "E": "#c0392b", "clear": "#ffffff", "spanned": "#ffffff"}
def gkey(g):
    return g.split(" ")[0] if g[0] in "ABCDE" else g

SHEETS = [
    ("Farmgate, Khamar Bari Road and the station (west)", ["K37-%02d" % i for i in range(1, 8)] + ["K37-61", "K37-62"]),
    ("Farmgate station (east) and the bend", ["K37-07", "K37-08", "K37-09", "K37-10", "K37-11"] + ["K37-%02d" % i for i in range(62, 68)]),
    ("The bend onto Kazi Nazrul Islam Avenue", ["K37-%02d" % i for i in range(9, 18)]),
    ("Kazi Nazrul Islam Avenue, north of Karwan Bazar", ["K37-%02d" % i for i in range(17, 28)]),
    ("Kazi Nazrul Islam Avenue to Karwan Bazar station", ["K37-%02d" % i for i in range(27, 38)]),
    ("Karwan Bazar station and Sonargaon", ["K37-%02d" % i for i in range(36, 48)]),
    ("South to the junction at N 13,530", ["K37-%02d" % i for i in range(46, 61)]),
]
by = {r["pid"]: r for r in rows}

def esc(s):
    return (s or "").replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")

for n, (title, pids) in enumerate(SHEETS, 1):
    ps = [by[p] for p in pids]
    m = 28
    e0, e1 = min(p["e"] for p in ps) - m, max(p["e"] for p in ps) + m
    n0, n1 = min(p["n"] for p in ps) - m, max(p["n"] for p in ps) + m
    W = 1100
    s = min(W / (e1 - e0), 1300 / (n1 - n0))
    w, h = (e1 - e0) * s, (n1 - n0) * s
    top = 64
    X = lambda e: (e - e0) * s
    Y = lambda nn: top + (n1 - nn) * s
    def path(flat):
        return " ".join(("M" if i == 0 else "L") + "%.1f %.1f" % (X(flat[i]), Y(flat[i + 1])) for i in range(0, len(flat), 2))
    def visible(flat):
        return any(e0 - 60 < flat[i] < e1 + 60 and n0 - 60 < flat[i + 1] < n1 + 60 for i in range(0, len(flat), 2))
    L = []
    CW = max(w, 1100)
    L.append('<svg xmlns="http://www.w3.org/2000/svg" width="%d" height="%d" viewBox="0 0 %d %d" font-family="Helvetica, Arial, sans-serif">' % (CW, h + top + 92, CW, h + top + 92))
    L.append('<rect width="100%%" height="100%%" fill="#fbfaf7"/>')
    L.append('<text x="10" y="22" font-size="16" font-weight="bold" fill="#222">K-37 plan %d: %s</text>' % (n, esc(title)))
    L.append('<text x="10" y="44" font-size="12" fill="#444">North up. World metres (east %d-%d, north %d-%d). Data as exported; no pier, road or alignment moved.</text>' % (e0, e1, n0, n1))
    L.append('<clipPath id="c"><rect x="0" y="%d" width="%.0f" height="%.0f"/></clipPath><g clip-path="url(#c)">' % (top, w, h))
    for b in geom["blds"].values():
        if not visible(b["w"]):
            continue
        st = b["type"] == "train_station" and b["base"] > 2
        L.append('<path d="%s Z" fill="%s" stroke="%s" stroke-width="%s"%s/>' % (path(b["w"]), "#dfe8f3" if st else "#ddd8cf", "#3b6ea8" if st else "#8f897f", "1.5" if st else "0.6", ' stroke-dasharray="6 3" fill-opacity="0.7"' if st else ""))
    rds = [r for r in geom["roads"].values() if r["y"] < 1 and visible(r["w"])]
    for r in rds:  # estimated surface
        if r["foot"]:
            continue
        L.append('<path d="%s" fill="none" stroke="#e0533d" stroke-opacity="0.26" stroke-width="%.1f" stroke-linejoin="round"/>' % (path(r["w"]), r["width"] * s))
    for r in rds:  # lane-based surface (3.0 m lanes) where the way has a lanes tag
        if r["foot"] or not r["lanes"]:
            continue
        L.append('<path d="%s" fill="none" stroke="#5d6066" stroke-opacity="0.30" stroke-width="%.1f" stroke-linejoin="round"/>' % (path(r["w"]), r["lanes"] * 3.0 * s))
    for r in rds:
        L.append('<path d="%s" fill="none" stroke="%s" stroke-width="%s" %s/>' % (path(r["w"]), "#3c8d3c" if r["foot"] else "#555", "1" if r["foot"] else "0.7", 'stroke-dasharray="2 2"' if r["foot"] else 'stroke-dasharray="5 4"'))
    for t in geom["tracks"]:
        if visible(t):
            L.append('<path d="%s" fill="none" stroke="#1f3d7a" stroke-width="1.6"/>' % path(t))
    # Proposed positions (outline only) where a slide along the deck is the proposal.
    for p in rows:
        g = gkey(p["group"])
        prop = None
        if g == "B":
            sl = p["slide"]["lo"] if p["slide"]["lo"] is not None else p["slide"]["hi"]
            if sl:
                prop = sl
        if g == "D" and p["slide"]["lo"]:
            prop = p["slide"]["lo"]
        if prop:
            px, pz = p["e"] + p["ux"] * prop, p["n"] + p["uz"] * prop
            L.append('<circle cx="%.1f" cy="%.1f" r="%.1f" fill="none" stroke="%s" stroke-width="1.6" stroke-dasharray="3 2"/>' % (X(px), Y(pz), max(R * s, 4), COL[g]))
            L.append('<line x1="%.1f" y1="%.1f" x2="%.1f" y2="%.1f" stroke="%s" stroke-width="1"/>' % (X(p["e"]), Y(p["n"]), X(px), Y(pz), COL[g]))
    for p in rows:
        if not (e0 - 5 < p["e"] < e1 + 5 and n0 - 5 < p["n"] < n1 + 5):
            continue
        g = gkey(p["group"])
        cx, cy, rr = X(p["e"]), Y(p["n"]), max(R * s, 4)
        if g == "spanned":
            L.append('<path d="M%.1f %.1f l8 8 M%.1f %.1f l-8 8" stroke="#333" stroke-width="1.6"/>' % (cx - 4, cy - 4, cx + 4, cy - 4))
        else:
            L.append('<circle cx="%.1f" cy="%.1f" r="%.1f" fill="%s" stroke="#222" stroke-width="1"/>' % (cx, cy, rr, COL[g]))
        # Label to the deck's left, away from the pier.
        lx, ly = cx - p["uz"] * 14, cy - p["ux"] * 14
        L.append('<text x="%.1f" y="%.1f" font-size="10.5" fill="#111" text-anchor="middle" stroke="#fbfaf7" stroke-width="3" paint-order="stroke">%s</text>' % (lx, ly + 3, p["pid"][4:]))
    L.append("</g>")
    L.append('<rect x="0.5" y="%d" width="%.0f" height="%.0f" fill="none" stroke="#999"/>' % (top, w - 1, h))
    # Scale bar and legend.
    yb = top + h + 22
    L.append('<line x1="10" y1="%.0f" x2="%.1f" y2="%.0f" stroke="#222" stroke-width="2"/><text x="%.1f" y="%.0f" font-size="11">20 m</text>' % (yb, 10 + 20 * s, yb, 16 + 20 * s, yb + 4))
    items = [("A", "width estimate (lanes tag)"), ("A'", "width estimate (3.0-3.3 m lanes)"), ("B", "pier placement rule"), ("C", "station over the road"),
             ("D", "building footprint"), ("E", "insufficient evidence"), ("clear", "clear")]
    x, yl = 90 + 20 * s, yb
    for k, label in items:
        if x + 30 + 6.2 * len(label) > CW:
            x, yl = 10, yl + 20
        L.append('<circle cx="%.0f" cy="%.0f" r="5" fill="%s" stroke="#222"/><text x="%.0f" y="%.0f" font-size="11">%s %s</text>' % (x, yl, COL[k], x + 9, yl + 4, "" if k == "clear" else k, label))
        x += 30 + 6.2 * len(label)
    y2 = yl + 22
    L.append('<rect x="10" y="%.0f" width="22" height="9" fill="#e0533d" fill-opacity="0.26"/><text x="38" y="%.0f" font-size="11">carriageway at the estimated width (now)</text>' % (y2 - 8, y2))
    L.append('<rect x="270" y="%.0f" width="22" height="9" fill="#5d6066" fill-opacity="0.30"/><text x="298" y="%.0f" font-size="11">at its lanes tag x 3.0 m (proposal A)</text>' % (y2 - 8, y2))
    L.append('<line x1="510" y1="%.0f" x2="532" y2="%.0f" stroke="#1f3d7a" stroke-width="1.6"/><text x="538" y="%.0f" font-size="11">elevated track</text>' % (y2 - 4, y2 - 4, y2))
    L.append('<rect x="630" y="%.0f" width="22" height="9" fill="#dfe8f3" stroke="#3b6ea8" stroke-dasharray="4 2"/><text x="658" y="%.0f" font-size="11">raised station</text>' % (y2 - 8, y2))
    L.append('<circle cx="760" cy="%.0f" r="5" fill="none" stroke="#7b3fa0" stroke-dasharray="3 2"/><text x="769" y="%.0f" font-size="11">proposed slide along the deck</text>' % (y2 - 4, y2))
    L.append("</svg>")
    open(os.path.join(out, "plan-%d.svg" % n), "w").write("\n".join(L))
    print("plan-%d.svg" % n, "%.0fx%.0f" % (w, h), "scale %.2f px/m" % s)
