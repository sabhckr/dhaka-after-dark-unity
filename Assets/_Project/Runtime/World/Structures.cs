// Structures for one chunk (Milestone 2): the Dhaka Metro Line 6 viaduct, footbridges with their
// stairs, and dividers between twin carriageways. Range 0 of ChunkStreet; see StreetFurniture.cs.
//
// Viaduct. The elevated rails (10 m up or more: in the slice, Line 6's two tracks at the
// exporter's 31 m) are sampled every ViaductSample metres. Where the other track is TwinMin to
// TwinMax metres away, one deck carries both, centred between them (built from the track with the
// lower way id); elsewhere a deck carries one track. The deck is a box girder with side wings and
// parapets, swept along that line; each track has a plinth and two rails on it. Its height is the
// rail data's: the alignment and level are never changed (K-15 notes the level rule). Twins are
// paired across chunk borders from the neighbours' elevated rails (NeighbourRails; K-50, V-5), and
// each chunk builds the part of a deck inside it. Piers stand where the deck line crosses a
// PierGrid-metre line of the world grid on its main axis (east or north, whichever the line runs
// along more), decided along the whole deck so neighbouring chunks agree, and built by the chunk
// they fall in; each is a round column with a pier cap. P-2 (Piers): none closer than PierMinSpan
// along the deck; one in a junction or on a crossing road slides along the deck, else is spanned.
// A pier whose foot is on a drawn carriageway the deck runs along (whose width is mostly
// estimated, K-31) or in a building is still drawn, and counted: in a building on the ground (a
// conflict, PiersInBuilding) apart from rising into a raised one such as a station body (PiersUnderRaised,
// K-37 P-3). K-37 P-6 (owner, 2026-11-04,
// "draw medians"): where traffic can pass it on both sides, a pier on a carriageway gets a raised
// median island (ViaductIsland: 3.0 m across, 7.8 m long, a lane of at least 3 m each side, clear of
// junction pads, crossings, bus stops, ground buildings, the chunk border and other islands); the
// pier is never moved and no road is changed. Else it stays on the carriageway, counted with the
// reason (IslandRefusals). A divider stops either side of an island. K-37 P-5a (owner, 2026-11-05: "Yes, build it",
// a 3.0 m median where both carriageways keep two lanes): before a deck run's piers, a continuous raised median, as
// wide as a P-6 island, is drawn along the deck line wherever every 2 m section of it passes (RunSection): a one-way
// carriageway under it, every carriageway under it one-way and none split by it (it stands between twin carriageways,
// as the plan's model counted them), traffic on each side with a lane at least
// MedianRunLanes wide (two LaneMin lanes; a carriageway beside it counts within MedianRunBeside of its edge, the
// twin of the one under it), and, as for an island, clear of ground buildings, junction pads, crossings, bus stops
// and earlier islands. It runs on to the chunk border, where the neighbour judges its own side; a piece shorter than
// an island that touches no border is not drawn. A pier on a carriageway whose whole island footprint lies on the
// median stands on it ("median run") and gets no island of its own, if the median's test also holds over that
// footprint, RoadMargin inside the two-lane limit (as the checker judges it); else P-6 applies. Neither moves a pier or
// changes a road: the median is drawn on the road surface, as an island is.
//
// Footbridges. Every foot way on a bridge 2.5 m or higher that is not steps is a deck at its own
// height: a slab with toe boards, posts and two rails each side, on columns every ColumnStep metres
// and at its ends; a column that would stand on a carriageway moves up to 3 m along, else is left
// out and counted. Steps ways on the bridge are stair flights from the ground (StairBase) up to the
// deck that one of their ends meets; risers of about Riser metres, stringers and handrails.
//
// Dividers. Where a one-way secondary or higher road has a one-way twin running the other way
// whose centre line is within the two half widths plus 1 m (their drawn surfaces meet or overlap,
// K-31), a divider runs midway between them: a raised concrete kerb with its faces painted in
// yellow and black blocks and a steel railing on it. None in junction pads or on a third road.
// Built from the twin with the lower way id, so each pair has one.
using System;
using System.Collections.Generic;

namespace Dad.World
{
    public sealed partial class ChunkRoads
    {
        public const double ViaductMinY = 10, ViaductSample = 2, TwinMin = 1.5, TwinMax = 8, TrackHalf = 2.4, DeckDrop = 0.35, GirderDepth = 2.1;
        public const double PierGrid = 30, PierRadius = 0.9, PierCapDepth = 1.4, WidthJump = 0.75;
        /// <summary>P-6: a viaduct pier's median island, as a flyover's (MedianMargin either side, MedianEnds past it) round the wider pier.</summary>
        public const double ViaductIslandHalf = PierRadius + MedianMargin, ViaductIslandEnds = PierRadius + MedianEnds;
        /// <summary>P-5a: the lane each side of the continuous median (two LaneMin lanes), and how far from its edge a carriageway beside it
        /// still counts (a twin whose edge is within TwinMax of the deck line).</summary>
        public const double MedianRunLanes = 2 * LaneMin, MedianRunBeside = TwinMax - ViaductIslandHalf;
        public const double FootDeckDepth = 0.4, RailingHeight = 1.1, PostStep = 1.5, ColumnStep = 12, Riser = 0.17, StairBase = 0.28, StairReach = 2.5;
        public const double DividerHalf = 0.3, DividerHeight = 0.25, DividerStep = 4, DividerRail = 1.0;
        public const uint ViaductConcrete = 0x9a968d, ViaductSoffit = 0x7c786f, Plinth = 0x8a867e, RailSteel = 0x5e5650,
            FootSteel = 0x56606a, FootDeck = 0x8b867c, DividerYellow = 0xc29a2a, DividerBlack = 0x2b2a28, RailingSteel = 0x8c8f90;

        // ---- Viaduct ----

        /// <summary>
        /// P-2 (approved 2026-11-04): no two piers of a deck closer than PierMinSpan along it (V-6: at a bend the
        /// grid lines change axis); a pier whose foot is in a junction pad or on a carriageway that crosses under the
        /// deck (more than CrossAngle off its line; V-4) slides along the deck by up to PierSlideMax, else the deck
        /// spans the place, but never two places running.
        /// </summary>
        public const double PierMinSpan = 25, PierSlideStep = 0.25, PierSlideMax = 4, CrossAngle = 30, ViaductWindow = 120, OwnSnap = 0.05;

        void Viaduct(DadcChunk c, ChunkStreet st, KitWriter w, StreetScratch ss, bool keep)
        {
            // Cleared before any return: the flyovers read these, and the scratch is shared by every chunk this thread builds.
            ss.PiersPlaced.Clear();
            ss.Islands.Clear();
            ss.Runs.Clear();
            if (c.Rails == null) return;
            var own = new List<(long id, double y, List<double> pts)>();
            foreach (var r in c.Rails)
                if (r.YCm >= ViaductMinY * 100 && r.Pts != null && r.Pts.Length >= 4) own.Add((r.Id, r.YCm / 100.0, Densify(r.Pts, ViaductSample)));
            if (own.Count == 0) return;
            // The decks come from every elevated track this chunk and its neighbours hold, each joined across the
            // borders into one line (NeighbourRails), so both sides of a border pair the same twins and agree on the
            // piers; each side builds the girder and the piers that fall inside it (K-50, V-5).
            var tracks = Joined(c);
            uint concrete = WorldMaterials.Pack(ViaductConcrete, WorldMaterial.Concrete), soffit = WorldMaterials.Pack(ViaductSoffit, WorldMaterial.Concrete);
            var run = ss.Run;
            for (int i = 0; i < tracks.Count; i++)
            {
                var (id, y, pts) = tracks[i];
                int n = pts.Count / 2;
                run.Clear();
                double runHalf = 0;
                for (int k = 0; k <= n; k++)
                {
                    bool lead = false;
                    double cx = 0, cz = 0, half = TrackHalf;
                    if (k < n)
                    {
                        double x = pts[2 * k], z = pts[2 * k + 1];
                        // Only near this chunk: far enough out that the pier sequence has settled where it enters.
                        if (x >= -ViaductWindow && x <= Size + ViaductWindow && z >= -ViaductWindow && z <= Size + ViaductWindow)
                        {
                            Nearest(tracks, i, x, z, out double d, out int j, out double qx, out double qz);
                            bool twin = d >= TwinMin && d <= TwinMax;
                            lead = !twin || id < tracks[j].id;
                            cx = twin ? (x + qx) / 2 : x;
                            cz = twin ? (z + qz) / 2 : z;
                            half = twin ? d / 2 + TrackHalf : TrackHalf;
                        }
                    }
                    // A run ends where this track stops leading or the deck's width jumps.
                    if (run.Count > 0 && (!lead || Math.Abs(half - runHalf) > WidthJump))
                    {
                        if (run.Count >= 6) DeckRun(c, st, w, ss, runHalf, y - DeckDrop, concrete, soffit, keep);
                        run.Clear();
                    }
                    if (!lead) continue;
                    if (run.Count == 0) runHalf = half;
                    run.Add(cx);
                    run.Add(y - DeckDrop);
                    run.Add(cz);
                }
            }
            // Plinths and rails on this chunk's own elevated tracks.
            var path = ss.Path;
            var left = ss.Left;
            uint plinth = WorldMaterials.Pack(Plinth, WorldMaterial.Concrete), steel = WorldMaterials.Pack(RailSteel, WorldMaterial.Rust);
            Span<double> slab = stackalloc double[] { 1.25, 0, -1.25, 0, -1.25, 0.2, 1.25, 0.2 };
            Span<double> railL = stackalloc double[] { 0.79, 0.2, 0.72, 0.2, 0.72, 0.35, 0.79, 0.35 };
            Span<double> railR = stackalloc double[] { -0.72, 0.2, -0.79, 0.2, -0.79, 0.35, -0.72, 0.35 };
            Span<uint> plinthColours = stackalloc uint[] { plinth, plinth, plinth, plinth };
            Span<uint> railColours = stackalloc uint[] { steel, steel, steel, steel };
            foreach (var (_, y, pts) in own)
            {
                path.Clear();
                for (int k = 0; k < pts.Count; k += 2) { path.Add(pts[k]); path.Add(y - DeckDrop); path.Add(pts[k + 1]); }
                LeftNormals(path, left);
                w.Loft(slab, plinthColours, path, left, true, true, plinth, ss.Flat, ss.Tris);
                w.Loft(railL, railColours, path, left, true, true, steel, ss.Flat, ss.Tris);
                w.Loft(railR, railColours, path, left, true, true, steel, ss.Flat, ss.Tris);
            }
        }

        /// <summary>
        /// Every elevated track of the chunk and its neighbours (NeighbourRails) as continuous lines in this chunk's
        /// metres, sampled every ViaductSample metres, ordered by way id. Pieces of a way meet on the borders at the
        /// same centimetre, so they join exactly, and each segment is sampled as in its own chunk.
        /// </summary>
        static List<(long id, double y, List<double> pts)> Joined(DadcChunk c)
        {
            var byId = new SortedDictionary<long, (int y, List<int[]> pieces)>();
            void Add(Rail r)
            {
                if (r.YCm < ViaductMinY * 100 || r.Pts == null || r.Pts.Length < 4) return;
                if (!byId.TryGetValue(r.Id, out var e)) byId[r.Id] = e = (r.YCm, new List<int[]>());
                e.pieces.Add(r.Pts);
            }
            foreach (var r in c.Rails) Add(r);
            if (c.NearRails != null) foreach (var r in c.NearRails) Add(r);
            var o = new List<(long id, double y, List<double> pts)>();
            foreach (var kv in byId)
            {
                var pieces = kv.Value.pieces;
                var used = new bool[pieces.Count];
                for (int s0 = 0; s0 < pieces.Count; s0++)
                {
                    if (used[s0]) continue;
                    used[s0] = true;
                    var chain = new List<int>(pieces[s0]);
                    for (bool grew = true; grew;)
                    {
                        grew = false;
                        for (int q = 0; q < pieces.Count; q++)
                        {
                            if (used[q]) continue;
                            var p = pieces[q];
                            int m = p.Length;
                            int hx = chain[0], hz = chain[1], tx = chain[chain.Count - 2], tz = chain[chain.Count - 1];
                            if (p[0] == tx && p[1] == tz) for (int i = 2; i < m; i += 2) { chain.Add(p[i]); chain.Add(p[i + 1]); }
                            else if (p[m - 2] == tx && p[m - 1] == tz) for (int i = m - 4; i >= 0; i -= 2) { chain.Add(p[i]); chain.Add(p[i + 1]); }
                            else if (p[m - 2] == hx && p[m - 1] == hz) chain.InsertRange(0, new ArraySegment<int>(p, 0, m - 2));
                            else if (p[0] == hx && p[1] == hz)
                            {
                                var rev = new int[m - 2];
                                for (int i = 0; i < m - 2; i += 2) { rev[i] = p[m - 2 - i]; rev[i + 1] = p[m - 1 - i]; }
                                chain.InsertRange(0, rev);
                            }
                            else continue;
                            used[q] = true;
                            grew = true;
                        }
                    }
                    o.Add((kv.Key, kv.Value.y / 100.0, Densify(chain.ToArray(), ViaductSample)));
                }
            }
            o.Sort((a, b) => a.id != b.id ? a.id.CompareTo(b.id) : a.pts[0] != b.pts[0] ? a.pts[0].CompareTo(b.pts[0]) : a.pts[1].CompareTo(b.pts[1]));
            return o;
        }

        /// <summary>One deck run along ss.Run at deck top y: its piers along the whole run, then the girder cut to the chunk.</summary>
        void DeckRun(DadcChunk c, ChunkStreet st, KitWriter w, StreetScratch ss, double half, double y, uint concrete, uint soffit, bool keep)
        {
            MedianRuns(c, st, w, ss, keep);
            Piers(c, st, w, ss, ss.Run, ss.PiersPlaced, half, y, concrete, keep);
            // The girder: the run cut to the chunk square, piece by piece (a run may leave and re-enter).
            var run = ss.Run;
            var path = ss.Path;
            path.Clear();
            int n = run.Count / 3;
            for (int k = 0; k + 1 < n; k++)
            {
                double ax = run[3 * k], az = run[3 * k + 2], bx = run[3 * k + 3], bz = run[3 * k + 5], ay = run[3 * k + 1], by = run[3 * k + 4];
                if (!Clip(ax, az, bx, bz, out double t0, out double t1))
                {
                    if (path.Count >= 6) Girder(st, w, ss, half, concrete, soffit);
                    path.Clear();
                    continue;
                }
                double x0 = ax + (bx - ax) * t0, z0 = az + (bz - az) * t0, x1 = ax + (bx - ax) * t1, z1 = az + (bz - az) * t1;
                if (path.Count > 0 && t0 > 0)
                {
                    if (path.Count >= 6) Girder(st, w, ss, half, concrete, soffit);
                    path.Clear();
                }
                if (path.Count == 0) { path.Add(x0); path.Add(ay + (by - ay) * t0); path.Add(z0); }
                path.Add(x1);
                path.Add(ay + (by - ay) * t1);
                path.Add(z1);
                if (t1 < 1)
                {
                    if (path.Count >= 6) Girder(st, w, ss, half, concrete, soffit);
                    path.Clear();
                }
            }
            if (path.Count >= 6) Girder(st, w, ss, half, concrete, soffit);
            path.Clear();
        }

        /// <summary>The part of segment a-b inside the chunk square, as parameters t0 &lt;= t1 (Liang-Barsky); false if none.</summary>
        static bool Clip(double ax, double az, double bx, double bz, out double t0, out double t1)
        {
            t0 = 0;
            t1 = 1;
            double dx = bx - ax, dz = bz - az;
            Span<double> p = stackalloc double[] { -dx, dx, -dz, dz };
            Span<double> q = stackalloc double[] { ax, Size - ax, az, Size - az };
            for (int i = 0; i < 4; i++)
            {
                if (Math.Abs(p[i]) < 1e-12) { if (q[i] < 0) return false; continue; }
                double r = q[i] / p[i];
                if (p[i] < 0) { if (r > t1) return false; if (r > t0) t0 = r; }
                else { if (r < t0) return false; if (r < t1) t1 = r; }
            }
            return t1 - t0 > 1e-9;
        }

        /// <summary>The box girder along ss.Path (inside the chunk): open where it is cut on the border.</summary>
        void Girder(ChunkStreet st, KitWriter w, StreetScratch ss, double half, uint concrete, uint soffit)
        {
            var path = ss.Path;
            var left = ss.Left;
            LeftNormals(path, left);
            double W = half, P = W - 0.2;
            Span<double> profile = stackalloc double[]
            {
                W, 1.1, P, 1.1, P, 0, -P, 0, -P, 1.1, -W, 1.1, -W, -0.3, -0.55 * W, -0.55, -0.38 * W, -GirderDepth, 0.38 * W, -GirderDepth, 0.55 * W, -0.55, W, -0.3,
            };
            Span<uint> colours = stackalloc uint[] { concrete, concrete, concrete, concrete, concrete, concrete, soffit, soffit, soffit, soffit, soffit, concrete };
            int n = path.Count / 3;
            bool startOn = AtBorder(path[0], path[2]), endOn = AtBorder(path[3 * n - 3], path[3 * n - 1]);
            w.Loft(profile, colours, path, left, !startOn, !endOn, concrete, ss.Flat, ss.Tris);
            st.ViaductRuns++;
            double length = 0;
            for (int k = 0; k + 1 < n; k++) length += Dist(path[3 * k], path[3 * k + 2], path[3 * k + 3], path[3 * k + 5]);
            st.ViaductLength += (int)Math.Round(length);
        }

        /// <summary>Is (x, z) this chunk's (south-west edges in, north-east edges out, within OwnSnap)? A place on a border is the chunk's on its far side.</summary>
        static bool Owned(double x, double z)
        {
            static double Snap(double v) => Math.Abs(v) < OwnSnap ? 0 : Math.Abs(v - Size) < OwnSnap ? Size : v;
            x = Snap(x);
            z = Snap(z);
            return x >= 0 && x < Size && z >= 0 && z < Size;
        }

        /// <summary>
        /// The piers of one deck run (path of x, deck top, z), decided along the whole run so that neighbouring
        /// chunks, which see the same run, agree; built only where they fall in this chunk. A pier stands where the
        /// run crosses a PierGrid line of the world grid on its main axis (east or north, whichever it runs along
        /// more), at least PierMinSpan after the last. One in a junction pad or on a crossing carriageway slides
        /// along the deck to the nearest clear place within PierSlideMax, else the place is spanned, unless the
        /// place before was spanned too (then it stays, counted). A neighbour's place is taken as built.
        /// </summary>
        void Piers(DadcChunk c, ChunkStreet st, KitWriter w, StreetScratch ss, List<double> run, List<(double x, double z)> placed, double half, double y, uint concrete, bool keep)
        {
            int n = run.Count / 3;
            double length = 0, lastPier = double.NegativeInfinity;
            bool lastSpanned = false;
            for (int k = 0; k + 1 < n; k++)
            {
                double ax = run[3 * k], az = run[3 * k + 2], bx = run[3 * k + 3], bz = run[3 * k + 5];
                double seg = Math.Sqrt((bx - ax) * (bx - ax) + (bz - az) * (bz - az));
                if (seg < 1e-9) continue;
                // World grid lines crossed on the main axis, each once: (low, high] in world metres.
                bool alongEast = Math.Abs(bx - ax) >= Math.Abs(bz - az);
                double corner = alongEast ? c.CornerEastCm / 100.0 : c.CornerNorthCm / 100.0;
                double a = corner + (alongEast ? ax : az), b = corner + (alongEast ? bx : bz);
                double lo = Math.Min(a, b), hi = Math.Max(a, b);
                for (double g = Math.Floor(lo / PierGrid) * PierGrid + PierGrid; g <= hi; g += PierGrid)
                {
                    double t = (g - a) / (b - a);
                    if (t < 0 || t > 1) continue;
                    double along = length + t * seg;
                    if (along - lastPier < PierMinSpan) continue;
                    double px = ax + (bx - ax) * t, pz = az + (bz - az) * t, dx = (bx - ax) / seg, dz = (bz - az) / seg;
                    // One pier per place: where a deck is split into runs (a width jump, the lead passing to the
                    // other track), the next run's first grid place can be one the last run already took.
                    if (Taken(placed, px, pz)) continue;
                    if (!Owned(px, pz)) { lastPier = along; lastSpanned = false; placed.Add((px, pz)); continue; }
                    if (Blocked(px, pz, dx, dz, out bool inPad))
                    {
                        bool moved = false;
                        bool startIn = InBuilding(px, pz);
                        for (double s = PierSlideStep; s <= PierSlideMax + 1e-9 && !moved; s += PierSlideStep)
                            for (int sign = 1; sign >= -1 && !moved; sign -= 2)
                            {
                                double at = along + sign * s;
                                if (at - lastPier < PierMinSpan || at < 0 || at > PathLength(run)) continue;
                                PathAt(run, at, out double qx, out double qz, out double qdx, out double qdz);
                                if (!Owned(qx, qz) || Taken(placed, qx, qz) || Blocked(qx, qz, qdx, qdz, out _) || (!startIn && InBuilding(qx, qz))) continue;
                                px = qx;
                                pz = qz;
                                dx = qdx;
                                dz = qdz;
                                along = at;
                                moved = true;
                            }
                        if (moved) st.PiersSlid++;
                        else if (!lastSpanned)
                        {
                            if (inPad) st.PiersInJunction++;
                            else st.PiersOverRoad++;
                            if (keep) st.Items.Add(new ChunkStreet.Item { Kind = ChunkStreet.Kind.Pier, X = px, Z = pz, Skipped = true, Why = inPad ? "in a junction: spanned" : "on a crossing road: spanned" });
                            lastSpanned = true;
                            continue;
                        }
                        else st.PiersKept++;
                    }
                    lastPier = along;
                    lastSpanned = false;
                    placed.Add((px, pz));
                    Pier(c, st, w, ss, px, pz, dx, dz, half, y, concrete, keep);
                }
                length += seg;
            }
        }

        /// <summary>
        /// P-6 (owner, 2026-11-04: "draw medians"): a raised median island under a viaduct pier whose foot is on a drawn
        /// carriageway, ViaductIslandHalf across and ViaductIslandEnds past the pier each way along the deck. The pier is
        /// never moved. Built where the island lies wholly in this chunk, off buildings, junction pads, crossing keep-outs,
        /// bus stop pads and other islands, and where every carriageway under it runs along the deck and keeps a lane of
        /// at least LaneMin each side (one-way: one side may be covered), as for a flyover's median (MedianFits), and where
        /// traffic passes on both sides of it (the owner's decision): a lane of at least LaneMin each side at every
        /// cross-section, of the road under it or of a carriageway along the deck within IslandBeside of its edge (a
        /// pier between twin one-way carriageways). Null when built; else the reason it is not: "border", "building",
        /// "junction", "crossing", "bus stop", "island", "crossing road", "road end", "no road", "narrow" or "one side".
        /// </summary>
        string ViaductIsland(DadcChunk c, ChunkStreet st, KitWriter w, StreetScratch ss, double x, double z, double dx, double dz, bool keep)
        {
            double h = ViaductIslandHalf, a = ViaductIslandEnds, lx = -dz, lz = dx;
            for (int i = -1; i <= 1; i += 2)
                for (int j = -1; j <= 1; j += 2)
                {
                    double cx = x + dx * a * i + lx * h * j, cz = z + dz * a * i + lz * h * j;
                    if (cx < 0 || cx > Size || cz < 0 || cz > Size) return "border";
                }
            LoadGroundBuildings(c);
            if (IslandInGroundBuilding(x, z, dx, dz, h, a)) return "building";
            // Pads, crossing keep-outs and bus stops: discs of the half width along its axis, every half width.
            int nd = (int)Math.Ceiling(2 * a / h);
            for (int i = 0; i <= nd; i++)
            {
                double u = -a + 2 * a * i / nd, px = x + dx * u, pz = z + dz * u;
                if (InAnyPad(px, pz, 0.28, h)) return "junction";
                foreach (var ring in Mine().GradeKeepOuts) if (InOrNear(ring, px, pz, h)) return "crossing";
                if (OnStopPad(px, pz, h)) return "bus stop";
            }
            if (IslandOverlaps(ss, x, z, dx, dz, h, a)) return "island";
            if (!MedianFits(x, z, dx, dz, h, a, IslandBeside, out double lane, out string why)) return why;
            Median(st, w, x, z, dx, dz, h, a, lane, keep);
            ss.Islands.Add((x, z, dx, dz, h, a));
            return null;
        }

        /// <summary>
        /// P-5a: the continuous median along the deck run in ss.Run. Each point of the run inside the chunk is judged by
        /// RunSection (a 2 m section centred on it); a point outside the chunk is the neighbour's to judge and counts as
        /// passing. A passing section holds the median for ViaductSample / 2 either side of its point, so a segment between
        /// two passing points is median, and one between a passing and a failing point is median for that far from the
        /// passing end; a segment wholly outside the chunk is the neighbour's, and one crossing the border is median only
        /// from a passing point inside. The median is cut to the chunk square and
        /// drawn piece by piece. Its straight parts go in ss.Runs: dividers stop at it, and a P-6 island may meet
        /// it (a pier near the median's end); the median's top is 1 cm under an island's, so where they overlap the island shows.
        /// </summary>
        void MedianRuns(DadcChunk c, ChunkStreet st, KitWriter w, StreetScratch ss, bool keep)
        {
            var run = ss.Run;
            int n = run.Count / 3, before = ss.Islands.Count, beforeRuns = ss.Runs.Count;
            if (n < 2) return;
            var pass = ss.RunPass;
            pass.Clear();
            bool any = false;
            for (int k = 0; k < n; k++)
            {
                double x = run[3 * k], z = run[3 * k + 2];
                bool ok = true;
                if (Inside(x, z))
                {
                    int k0 = Math.Max(0, k - 1), k1 = Math.Min(n - 1, k + 1);
                    double dx = run[3 * k1] - run[3 * k0], dz = run[3 * k1 + 2] - run[3 * k0 + 2], l = Math.Sqrt(dx * dx + dz * dz);
                    ok = l > 1e-9 && RunSection(c, ss, x, z, dx / l, dz / l, before, beforeRuns);
                    any |= ok;
                }
                pass.Add(ok);
            }
            if (!any) return;
            var path = ss.Path;
            path.Clear();
            double length = 0;
            for (int k = 0; k + 1 < n; k++)
            {
                double ax = run[3 * k], az = run[3 * k + 2], bx = run[3 * k + 3], bz = run[3 * k + 5], seg = Dist(ax, az, bx, bz);
                double s0 = 0, s1 = 1, reach = seg > 1e-9 ? ViaductSample / 2 / seg : 1;
                bool ia = Inside(ax, az), ib = Inside(bx, bz);
                // Across the border only from a passing point inside: the neighbour draws its own side.
                if (!pass[k] && !pass[k + 1] || !ia && !ib || !ia && !pass[k + 1] || !ib && !pass[k] || seg < 1e-9) s1 = -1;
                else if (!pass[k + 1]) s1 = Math.Min(1, reach);
                else if (!pass[k]) s0 = Math.Max(0, 1 - reach);
                if (s1 < s0 || !Clip(ax, az, bx, bz, out double t0, out double t1) || (t0 = Math.Max(t0, s0)) >= (t1 = Math.Min(t1, s1)) - 1e-9)
                {
                    RunPiece(st, w, ss, length, keep);
                    length = 0;
                    continue;
                }
                double x0 = ax + (bx - ax) * t0, z0 = az + (bz - az) * t0, x1 = ax + (bx - ax) * t1, z1 = az + (bz - az) * t1;
                if (path.Count > 0 && t0 > 0) { RunPiece(st, w, ss, length, keep); length = 0; }
                if (path.Count == 0) { path.Add(x0); path.Add(0); path.Add(z0); }
                path.Add(x1);
                path.Add(0);
                path.Add(z1);
                length += Dist(x0, z0, x1, z1);
                if (t1 < 1) { RunPiece(st, w, ss, length, keep); length = 0; }
            }
            RunPiece(st, w, ss, length, keep);
        }

        static bool Inside(double x, double z) => x >= 0 && x <= Size && z >= 0 && z <= Size;

        /// <summary>
        /// P-5a: can the continuous median (ViaductIslandHalf each side) run through a 2 m section at (x, z) along (dx, dz)?
        /// A carriageway under it, every one of them one-way and none split by it (twins either side); MedianFits with traffic beside it within MedianRunBeside of its
        /// edge, keeping at least MedianRunLanes each side; off ground buildings, junction pads, crossings and bus stops,
        /// and off the islands and median parts built for earlier deck runs of the chunk (the first `before` and `beforeRuns`).
        /// </summary>
        bool RunSection(DadcChunk c, StreetScratch ss, double x, double z, double dx, double dz, int before, int beforeRuns)
        {
            double h = ViaductIslandHalf, a = ViaductSample / 2;
            if (!OnCarriageway(x, z, 0.28, h)) return false;
            LoadGroundBuildings(c);
            if (IslandInGroundBuilding(x, z, dx, dz, h, a)) return false;
            for (int i = -1; i <= 1; i++)
            {
                double px = x + dx * a * i, pz = z + dz * a * i;
                if (InAnyPad(px, pz, 0.28, h) || OnStopPad(px, pz, h)) return false;
                foreach (var ring in Mine().GradeKeepOuts) if (InOrNear(ring, px, pz, h)) return false;
            }
            if (IslandOverlaps(ss.Islands, x, z, dx, dz, h, a, before) || IslandOverlaps(ss.Runs, x, z, dx, dz, h, a, beforeRuns)) return false;
            return RunFits(x, z, dx, dz, a, MedianRunLanes, out _, inChunk: true);
        }

        /// <summary>P-5a: MedianFits for the median (ViaductIslandHalf each side, a along each way) with traffic beside it within
        /// MedianRunBeside, every carriageway under it one-way and none split by it (it stands between twins), min or more each side.
        /// inChunk: a section's cross-section past the chunk border is left to the neighbour (it sees no road there).</summary>
        bool RunFits(double x, double z, double dx, double dz, double a, double min, out double lane, bool inChunk = false) =>
            MedianFits(x, z, dx, dz, ViaductIslandHalf, a, MedianRunBeside, out lane, out _, inChunk) && fitOneWay && fitTwins && fitSides >= min;

        /// <summary>P-5a: draws the median piece along ss.Path (local metres, y 0) and clears it. One shorter than an island that touches no border is dropped.</summary>
        void RunPiece(ChunkStreet st, KitWriter w, StreetScratch ss, double length, bool keep)
        {
            var path = ss.Path;
            int n = path.Count / 3;
            if (n >= 2)
            {
                bool startOn = AtBorder(path[0], path[2]), endOn = AtBorder(path[3 * n - 3], path[3 * n - 1]);
                if (length >= 2 * ViaductIslandEnds || startOn || endOn)
                {
                    double h = ViaductIslandHalf, t = MedianTop - 0.01;
                    uint kerb = WorldMaterials.Pack(DividerYellow, WorldMaterial.Paint), top = Pack(MedianTopColour);
                    Span<double> profile = stackalloc double[] { h, 0.27, -h, 0.27, -h, t - 0.04, -(h - 0.12), t, h - 0.12, t, h, t - 0.04 };
                    Span<uint> colours = stackalloc uint[] { kerb, kerb, kerb, top, kerb, kerb };
                    LeftNormals(path, ss.Left);
                    // Capped at both ends, the border too: the neighbour may not go on (a crossing just past the border, which this
                    // chunk cannot see); where it does, the two caps meet back to back inside the median.
                    w.Loft(profile, colours, path, ss.Left, true, true, kerb, ss.Flat, ss.Tris);
                    for (int k = 0; k + 1 < n; k++)
                    {
                        double ax = path[3 * k], az = path[3 * k + 2], bx = path[3 * k + 3], bz = path[3 * k + 5], l = Dist(ax, az, bx, bz);
                        if (l < 1e-9) continue;
                        var part = ((ax + bx) / 2, (az + bz) / 2, (bx - ax) / l, (bz - az) / l, h, l / 2);
                        ss.Runs.Add(part);
                        if (keep) st.MedianRunParts.Add((ax, az, bx, bz));
                    }
                    st.MedianRuns++;
                    st.MedianRunLength += length;
                }
            }
            path.Clear();
        }

        /// <summary>P-5a: does the continuous median hold a pier at (x, z) along (dx, dz), that is the centre and both ends of its island footprint?</summary>
        static bool OnMedianRun(StreetScratch ss, double x, double z, double dx, double dz)
        {
            for (int i = -1; i <= 1; i++)
            {
                double px = x + dx * ViaductIslandEnds * i, pz = z + dz * ViaductIslandEnds * i;
                bool held = false;
                foreach (var o in ss.Runs)
                {
                    double ex = px - o.x, ez = pz - o.z;
                    if (Math.Abs(ex * o.ux + ez * o.uz) <= o.a + 1e-6 && Math.Abs(-ex * o.uz + ez * o.ux) <= o.half - PierRadius + 1e-6) { held = true; break; }
                }
                if (!held) return false;
            }
            return true;
        }

        /// <summary>
        /// Does an island (h across each side, a along each way, along (dx, dz)) reach into a building on the ground? Its
        /// footprint is sampled every half metre or so. A raised building (a station body on its layer, or one with a
        /// mapped base) has nothing drawn under it at street level. LoadGroundBuildings must have run for the chunk.
        /// </summary>
        bool IslandInGroundBuilding(double x, double z, double dx, double dz, double h, double a)
        {
            double lx = -dz, lz = dx;
            // Only the buildings whose bounds meet the island's bounds are sampled (most islands have none near).
            double ex = Math.Abs(dx) * a + Math.Abs(lx) * h, ez = Math.Abs(dz) * a + Math.Abs(lz) * h;
            near.Clear();
            foreach (var g in groundBuildings)
                if (g.x1 >= x - ex && g.x0 <= x + ex && g.z1 >= z - ez && g.z0 <= z + ez) near.Add(g);
            if (near.Count == 0) return false;
            int na = (int)Math.Ceiling(2 * a / 0.5), nh = (int)Math.Ceiling(2 * h / 0.5);
            for (int i = 0; i <= na; i++)
                for (int j = 0; j <= nh; j++)
                {
                    double u = -a + 2 * a * i / na, v = -h + 2 * h * j / nh, px = x + dx * u + lx * v, pz = z + dz * u + lz * v;
                    foreach (var g in near)
                        if (px >= g.x0 && px <= g.x1 && pz >= g.z0 && pz <= g.z1 && InPolygon(g.ring, px, pz)) return true;
                }
            return false;
        }

        readonly List<(double x0, double z0, double x1, double z1, double[] ring)> near = new List<(double x0, double z0, double x1, double z1, double[] ring)>();

        /// <summary>Is (x, z) inside a building on the ground? LoadGroundBuildings must have run for the chunk.</summary>
        bool InGroundBuilding(double x, double z)
        {
            foreach (var g in groundBuildings)
                if (x >= g.x0 && x <= g.x1 && z >= g.z0 && z <= g.z1 && InPolygon(g.ring, x, z)) return true;
            return false;
        }

        static IEnumerable<Building> AllBuildings(DadcChunk c)
        {
            foreach (var b in c.Buildings ?? Array.Empty<Building>()) yield return b;
            foreach (var b in c.Placed) yield return b;
        }

        /// <summary>The chunk's buildings on the ground (base under GroundBuildingBase), once per chunk.</summary>
        void LoadGroundBuildings(DadcChunk c)
        {
            if (groundBuildings == null)
            {
                groundBuildings = new List<(double x0, double z0, double x1, double z1, double[] ring)>();
                foreach (var b in AllBuildings(c))
                {
                    if (b.Outer == null || b.Outer.Length < 6 || BuildingRules.Place(b).Base >= GroundBuildingBase) continue;
                    var ring = new double[b.Outer.Length];
                    double x0 = double.MaxValue, z0 = double.MaxValue, x1 = double.MinValue, z1 = double.MinValue;
                    for (int k = 0; k < ring.Length; k += 2)
                    {
                        ring[k] = b.Outer[k] / 100.0; ring[k + 1] = b.Outer[k + 1] / 100.0;
                        x0 = Math.Min(x0, ring[k]); x1 = Math.Max(x1, ring[k]); z0 = Math.Min(z0, ring[k + 1]); z1 = Math.Max(z1, ring[k + 1]);
                    }
                    groundBuildings.Add((x0, z0, x1, z1, ring));
                }
            }
        }

        /// <summary>A building whose base is this high or more is raised: nothing of it at street level (the checker's GroundBase).</summary>
        public const double GroundBuildingBase = 0.5;

        List<(double x0, double z0, double x1, double z1, double[] ring)> groundBuildings; // made on first use, for this chunk

        /// <summary>Is (x, z) on a median island or (P-5a) the continuous median built in this chunk, or within margin of one?</summary>
        static bool InIsland(StreetScratch ss, double x, double z, double margin) => InRect(ss.Islands, x, z, margin) || InRect(ss.Runs, x, z, margin);

        static bool InRect(List<(double x, double z, double ux, double uz, double half, double a)> list, double x, double z, double margin)
        {
            foreach (var o in list)
            {
                double ex = x - o.x, ez = z - o.z;
                if (Math.Abs(ex * o.ux + ez * o.uz) < o.a + margin && Math.Abs(-ex * o.uz + ez * o.ux) < o.half + margin) return true;
            }
            return false;
        }

        /// <summary>Would an island at (x, z) along (ux, uz), h across and a along, overlap one already built in this chunk? (Separating axes of the two rectangles.)</summary>
        static bool IslandOverlaps(StreetScratch ss, double x, double z, double ux, double uz, double h, double a) => IslandOverlaps(ss.Islands, x, z, ux, uz, h, a);

        /// <summary>As above against the first count rectangles of a list (P-5a: islands or median parts).</summary>
        static bool IslandOverlaps(List<(double x, double z, double ux, double uz, double half, double a)> list, double x, double z, double ux, double uz, double h, double a, int count = int.MaxValue)
        {
            for (int k = 0; k < Math.Min(count, list.Count); k++)
            {
                var o = list[k];
                double ex = o.x - x, ez = o.z - z;
                bool Apart(double ax, double az)
                {
                    double d = Math.Abs(ex * ax + ez * az);
                    double r1 = a * Math.Abs(ux * ax + uz * az) + h * Math.Abs(-uz * ax + ux * az);
                    double r2 = o.a * Math.Abs(o.ux * ax + o.uz * az) + o.half * Math.Abs(-o.uz * ax + o.ux * az);
                    return d >= r1 + r2;
                }
                if (!Apart(ux, uz) && !Apart(-uz, ux) && !Apart(o.ux, o.uz) && !Apart(-o.uz, o.ux)) return true;
            }
            return false;
        }

        /// <summary>Is there a pier (built here, or a neighbour's) within TwinMax of (x, z)? Tracks closer than that share one deck and one pier.</summary>
        static bool Taken(List<(double x, double z)> placed, double x, double z)
        {
            foreach (var p in placed)
                if ((p.x - x) * (p.x - x) + (p.z - z) * (p.z - z) < TwinMax * TwinMax) return true;
            return false;
        }

        static double PathLength(List<double> path)
        {
            double l = 0;
            for (int i = 0; i + 1 < path.Count / 3; i++) l += Dist(path[3 * i], path[3 * i + 2], path[3 * i + 3], path[3 * i + 5]);
            return l;
        }

        /// <summary>Is a pier's foot at (x, z) in a junction pad (inPad) or on an at-grade carriageway crossing under a deck running (dx, dz)?</summary>
        bool Blocked(double x, double z, double dx, double dz, out bool inPad)
        {
            inPad = InAnyPad(x, z, 0.28, PierRadius + 0.5);
            return inPad || OnCrossingRoad(x, z, dx, dz, PierRadius);
        }

        /// <summary>Is the disc at (x, z) on an at-grade carriageway that runs more than CrossAngle off the deck's direction (dx, dz)?</summary>
        bool OnCrossingRoad(double x, double z, double dx, double dz, double margin)
        {
            var mine = Mine();
            double cos = Math.Cos(CrossAngle * Math.PI / 180);
            foreach (int q in SegmentsNear(x - margin, x + margin, z - margin, z + margin))
            {
                var s = mine.SegItems[q];
                if (!SameLevel(s.Road.Y, 0.28)) continue;
                var p = s.Road.Pts;
                int j = Math.Max(0, Math.Min(s.Seg, s.Road.N - 2));
                double ux = p[2 * j + 2] - p[2 * j], uz = p[2 * j + 3] - p[2 * j + 1], l = Math.Sqrt(ux * ux + uz * uz);
                if (l < 1e-9 || Math.Abs(ux * dx + uz * dz) / l > cos) continue; // runs along the deck: not a crossing
                int start = s.ExactCount >= 3 ? s.Exact : s.Start, n = s.ExactCount >= 3 ? s.ExactCount : s.Count;
                if (InRing(mine.SegRings, start, n, x, z, margin)) return true;
            }
            return false;
        }

        void Pier(DadcChunk c, ChunkStreet st, KitWriter w, StreetScratch ss, double x, double z, double dx, double dz, double W, double deckTop, uint concrete, bool keep)
        {
            double bottom = deckTop - GirderDepth, capBase = bottom - PierCapDepth;
            w.Tube(x, -0.3, z, x, capBase, z, PierRadius, PierRadius * 0.94, 10, concrete);
            // The cap widens from the column to under the girder's webs.
            double lx = -dz, lz = dx, top = 0.42 * W;
            Span<double> k = stackalloc double[]
            {
                x + dx * 0.95 + lx * 0.95, capBase, z + dz * 0.95 + lz * 0.95, x - dx * 0.95 + lx * 0.95, capBase, z - dz * 0.95 + lz * 0.95,
                x - dx * 0.95 - lx * 0.95, capBase, z - dz * 0.95 - lz * 0.95, x + dx * 0.95 - lx * 0.95, capBase, z + dz * 0.95 - lz * 0.95,
                x + dx * 1.2 + lx * top, bottom, z + dz * 1.2 + lz * top, x - dx * 1.2 + lx * top, bottom, z - dz * 1.2 + lz * top,
                x - dx * 1.2 - lx * top, bottom, z - dz * 1.2 - lz * top, x + dx * 1.2 - lx * top, bottom, z + dz * 1.2 - lz * top,
            };
            w.Hex(k, concrete);
            bool onRoad = OnCarriageway(x, z, 0.28, PierRadius), inBuilding = InBuilding(x, z);
            st.Piers++;
            // P-3: a pier in a building on the ground is a conflict; one rising into a raised building (a station body on its
            // layer, or a building with a mapped base) stands under it as a real station's piers do, and is counted apart.
            string why = null;
            if (inBuilding)
            {
                LoadGroundBuildings(c);
                if (InGroundBuilding(x, z)) { st.PiersInBuilding++; why = "in a building"; }
                else { st.PiersUnderRaised++; why = "under a raised building"; }
            }
            double lane = 0;
            if (onRoad && OnMedianRun(ss, x, z, dx, dz) && RunFits(x, z, dx, dz, ViaductIslandEnds, MedianRunLanes + RoadMargin, out lane))
            {
                // P-5a: it stands on the continuous median, judged again over its island footprint (the checker's test), RoadMargin
                // inside the two-lane limit; else P-6 below may still give it an island.
                st.PiersOnMedianRun++;
                why = "median run";
                if (keep) st.Items.Add(new ChunkStreet.Item { Kind = ChunkStreet.Kind.Median, X = x, Z = z, Y = 0.28, Top = MedianTop, Why = "run lanes " + lane.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) });
            }
            else if (onRoad)
            {
                // P-6: a median island under it where the road leaves a lane each side; else it stays on the carriageway, counted with the reason.
                string no = ViaductIsland(c, st, w, ss, x, z, dx, dz, keep);
                if (no == null) { st.PiersOnIsland++; why = "median"; }
                else
                {
                    st.PiersOnRoad++;
                    why = "on a carriageway; no island: " + no;
                    var refusals = st.IslandRefusals ??= new Dictionary<string, int>();
                    refusals[no] = refusals.TryGetValue(no, out int seen) ? seen + 1 : 1;
                }
            }
            if (keep) st.Items.Add(new ChunkStreet.Item { Kind = ChunkStreet.Kind.Pier, X = x, Z = z, Y = 0, Top = bottom, Why = why });
        }

        /// <summary>The nearest point to (x, z) on any track but i: distance, track, point.</summary>
        static void Nearest(List<(long id, double y, List<double> pts)> tracks, int i, double x, double z, out double best, out int bestJ, out double bx, out double bz)
        {
            best = double.MaxValue;
            bestJ = -1;
            bx = bz = 0;
            for (int j = 0; j < tracks.Count; j++)
            {
                if (j == i) continue;
                var p = tracks[j].pts;
                for (int k = 0; k + 3 < p.Count; k += 2)
                {
                    double ax = p[k], az = p[k + 1], ux = p[k + 2] - ax, uz = p[k + 3] - az, ll = ux * ux + uz * uz;
                    double t = ll > 0 ? Math.Clamp(((x - ax) * ux + (z - az) * uz) / ll, 0, 1) : 0;
                    double qx = ax + ux * t, qz = az + uz * t, d = Math.Sqrt((qx - x) * (qx - x) + (qz - z) * (qz - z));
                    if (d < best) { best = d; bestJ = j; bx = qx; bz = qz; }
                }
            }
        }

        /// <summary>A polyline in centimetres as metres, with points added so none is more than step apart.</summary>
        static List<double> Densify(int[] pts, double step)
        {
            var o = new List<double>(pts.Length * 2);
            for (int i = 0; i + 3 < pts.Length; i += 2)
            {
                double ax = pts[i] / 100.0, az = pts[i + 1] / 100.0, bx = pts[i + 2] / 100.0, bz = pts[i + 3] / 100.0;
                int parts = Math.Max(1, (int)Math.Ceiling(Math.Sqrt((bx - ax) * (bx - ax) + (bz - az) * (bz - az)) / step));
                for (int k = 0; k < parts; k++) { o.Add(ax + (bx - ax) * k / parts); o.Add(az + (bz - az) * k / parts); }
            }
            o.Add(pts[pts.Length - 2] / 100.0);
            o.Add(pts[pts.Length - 1] / 100.0);
            return o;
        }

        /// <summary>Unit left normals (x, z pairs) at each point of a path of (x, y, z) triples: the mean of the neighbouring segments'.</summary>
        static void LeftNormals(List<double> path, List<double> left)
        {
            left.Clear();
            int n = path.Count / 3;
            for (int i = 0; i < n; i++)
            {
                int a = Math.Max(0, i - 1), b = Math.Min(n - 1, i + 1);
                double dx = path[3 * b] - path[3 * a], dz = path[3 * b + 2] - path[3 * a + 2], len = Math.Sqrt(dx * dx + dz * dz);
                if (len < 1e-9) { dx = 1; dz = 0; len = 1; }
                left.Add(-dz / len);
                left.Add(dx / len);
            }
        }


        /// <summary>Is (x, z) within 5 cm of the chunk's edge? Paths cut on the border end there.</summary>
        static bool AtBorder(double x, double z) => x < 0.05 || x > Size - 0.05 || z < 0.05 || z > Size - 0.05;

        // ---- Footbridges ----

        void Footbridges(DadcChunk c, ChunkStreet st, KitWriter w, StreetScratch ss, bool keep)
        {
            if (c.Roads == null) return;
            var path = ss.Path;
            var left = ss.Left;
            uint deck = WorldMaterials.Pack(FootDeck, WorldMaterial.Concrete), steel = WorldMaterials.Pack(FootSteel, WorldMaterial.Paint);
            Span<double> rect = stackalloc double[8];
            Span<uint> deckColours = stackalloc uint[] { deck, deck, deck, deck };
            Span<uint> steelColours = stackalloc uint[] { steel, steel, steel, steel };
            foreach (var r in c.Roads)
            {
                if (!RoadPalette.IsFootSpan(r) || r.Cls == "steps") continue;
                double y = r.YCm / 100.0, hw = Math.Max(0.9, r.WidthCm / 200.0);
                path.Clear();
                for (int i = 0; i < r.Pts.Length; i += 2) { path.Add(r.Pts[i] / 100.0); path.Add(y); path.Add(r.Pts[i + 1] / 100.0); }
                LeftNormals(path, left);
                int n = path.Count / 3;
                bool startOn = AtBorder(path[0], path[2]), endOn = AtBorder(path[3 * n - 3], path[3 * n - 1]);
                Rect(rect, -hw, hw, -FootDeckDepth, 0);
                w.Loft(rect, deckColours, path, left, !startOn, !endOn, deck, ss.Flat, ss.Tris);
                for (int s = -1; s <= 1; s += 2)
                {
                    double o = s * hw;
                    Rect(rect, o - s * 0.04, o, 0, 0.15);
                    w.Loft(rect, steelColours, path, left, true, true, steel, ss.Flat, ss.Tris);
                    Rect(rect, o - s * 0.05, o, RailingHeight - 0.06, RailingHeight);
                    w.Loft(rect, steelColours, path, left, true, true, steel, ss.Flat, ss.Tris);
                    Rect(rect, o - s * 0.03, o, 0.5, 0.55);
                    w.Loft(rect, steelColours, path, left, true, true, steel, ss.Flat, ss.Tris);
                }
                // Posts along both sides, and columns.
                double total = 0;
                for (int i = 0; i + 1 < n; i++) total += Dist(path[3 * i], path[3 * i + 2], path[3 * i + 3], path[3 * i + 5]);
                for (double s = 0; s <= total + 1e-6; s += PostStep)
                {
                    PathAt(path, s, out double x, out double z, out double dx, out double dz);
                    for (int side = -1; side <= 1; side += 2)
                        w.Post(x - dz * side * (hw - 0.025), z + dx * side * (hw - 0.025), y, y + RailingHeight, dx, dz, 0.025, 0.025, 0.025, 0.025, steel);
                }
                int columns = Math.Max(1, (int)Math.Round(total / ColumnStep));
                for (int k = 0; k <= columns; k++)
                {
                    double s = total * k / columns;
                    if (k == 0 && startOn || k == columns && endOn) continue;
                    bool placed = false;
                    // Middle columns move up to 3 m; end columns up to 6 m inwards, as the ends often sit over a kerb.
                    int tries = k == 0 || k == columns ? 12 : 6;
                    for (int t = 0; t <= tries && !placed; t++)
                    {
                        double sl = s + (t % 2 == 1 ? 1 : -1) * ((t + 1) / 2);
                        if (sl < 0 || sl > total) continue;
                        PathAt(path, sl, out double x, out double z, out double dx, out double dz);
                        if (!InChunk(x, z) || OnCarriageway(x, z, 0.28, 0.3)) continue;
                        placed = true;
                        w.Tube(x, -0.3, z, x, y - FootDeckDepth - 0.3, z, 0.22, 0.2, 8, steel);
                        w.Beam(x - dz * hw, z + dx * hw, x + dz * hw, z - dx * hw, 0.15, y - FootDeckDepth - 0.3, y - FootDeckDepth, y - FootDeckDepth - 0.3, y - FootDeckDepth, steel);
                        st.Columns++;
                        ss.Solids.Add(new ChunkStreet.Solid { X = x, Z = z, Ux = 1, Along = 0.22, Half = 0.22, Top = SolidHeight });
                        if (keep) st.Items.Add(new ChunkStreet.Item { Kind = ChunkStreet.Kind.Column, X = x, Z = z, Y = 0, Top = y - FootDeckDepth, Way = r.Id });
                    }
                    if (!placed)
                    {
                        st.ColumnsSkipped++;
                        if (keep)
                        {
                            PathAt(path, s, out double x, out double z, out _, out _);
                            st.Items.Add(new ChunkStreet.Item { Kind = ChunkStreet.Kind.Column, X = x, Z = z, Way = r.Id, Skipped = true, Why = "on a carriageway" });
                        }
                    }
                }
                st.FootDecks++;
                if (keep) st.Items.Add(new ChunkStreet.Item { Kind = ChunkStreet.Kind.Deck, X = path[0], Z = path[2], Y = y, Top = y, Way = r.Id });
            }
            foreach (var r in c.Roads)
                if (RoadPalette.IsFootSpan(r) && r.Cls == "steps") Stair(c, r, st, w, ss, keep);
        }

        void Stair(DadcChunk c, Road r, ChunkStreet st, KitWriter w, StreetScratch ss, bool keep)
        {
            var path = ss.Path;
            var left = ss.Left;
            int m = r.Pts.Length / 2;
            double sx = r.Pts[0] / 100.0, sz = r.Pts[1] / 100.0, ex = r.Pts[2 * m - 2] / 100.0, ez = r.Pts[2 * m - 1] / 100.0;
            double ds = DeckDistance(c, sx, sz, out double ys), de = DeckDistance(c, ex, ez, out double ye);
            bool attached = Math.Min(ds, de) <= StairReach;
            bool topAtStart = attached ? ds < de : false;
            double top = attached ? (topAtStart ? ys : ye) : r.YCm / 100.0, bottom = StairBase;
            if (!attached) st.StairsUnattached++;
            // From the foot up to the top.
            path.Clear();
            for (int i = 0; i < m; i++)
            {
                int k = topAtStart ? m - 1 - i : i;
                path.Add(r.Pts[2 * k] / 100.0);
                path.Add(0);
                path.Add(r.Pts[2 * k + 1] / 100.0);
            }
            double total = 0;
            for (int i = 0; i + 1 < m; i++) total += Dist(path[3 * i], path[3 * i + 2], path[3 * i + 3], path[3 * i + 5]);
            if (total < 0.5) return;
            double hw = Math.Max(0.8, r.WidthCm / 200.0), rise = top - bottom;
            int steps = Math.Max(2, (int)Math.Round(rise / Riser));
            uint concrete = WorldMaterials.Pack(FootDeck, WorldMaterial.Concrete), steel = WorldMaterials.Pack(FootSteel, WorldMaterial.Paint);
            for (int k = 0; k < steps; k++)
            {
                PathAt(path, total * k / steps, out double ax, out double az, out _, out _);
                PathAt(path, total * (k + 1) / steps, out double bx, out double bz, out _, out _);
                double yTop = bottom + rise * (k + 1) / steps, yBot = k == 0 ? bottom - 0.2 : yTop - rise / steps - 0.25;
                w.Beam(ax, az, bx, bz, hw, yBot, yTop, yBot, yTop, concrete);
            }
            // The pitch line, for stringers and handrails.
            double acc = 0;
            for (int i = 0; i < m; i++)
            {
                if (i > 0) acc += Dist(path[3 * i - 3], path[3 * i - 1], path[3 * i], path[3 * i + 2]);
                path[3 * i + 1] = bottom + rise * acc / total;
            }
            LeftNormals(path, left);
            Span<double> rect = stackalloc double[8];
            Span<uint> steelColours = stackalloc uint[] { steel, steel, steel, steel };
            Span<uint> concreteColours = stackalloc uint[] { concrete, concrete, concrete, concrete };
            for (int s = -1; s <= 1; s += 2)
            {
                double o = s * hw;
                Rect(rect, o, o + s * 0.1, -0.45, 0.12);
                w.Loft(rect, concreteColours, path, left, true, true, concrete, ss.Flat, ss.Tris);
                Rect(rect, o, o + s * 0.05, 0.9, 0.96);
                w.Loft(rect, steelColours, path, left, true, true, steel, ss.Flat, ss.Tris);
            }
            for (double s = 0; s <= total + 1e-6; s += PostStep)
            {
                PathAt(path, s, out double x, out double z, out double dx, out double dz);
                double y = bottom + rise * s / total;
                for (int side = -1; side <= 1; side += 2)
                    w.Post(x - dz * side * (hw + 0.025), z + dx * side * (hw + 0.025), y, y + 0.93, dx, dz, 0.022, 0.022, 0.022, 0.022, steel);
            }
            st.Stairs++;
            if (keep)
            {
                st.Items.Add(new ChunkStreet.Item { Kind = ChunkStreet.Kind.StairFoot, X = path[0], Z = path[2], Y = bottom, Top = bottom, Way = r.Id, Why = attached ? null : "unattached" });
                st.Items.Add(new ChunkStreet.Item { Kind = ChunkStreet.Kind.StairTop, X = path[3 * m - 3], Z = path[3 * m - 1], Y = top, Top = top, Way = r.Id, Why = attached ? null : "unattached" });
            }
        }

        /// <summary>Distance from (x, z) to the nearest footbridge deck (not steps) in the chunk, and that deck's height.</summary>
        static double DeckDistance(DadcChunk c, double x, double z, out double y)
        {
            double best = double.MaxValue;
            y = 0;
            foreach (var r in c.Roads)
            {
                if (!RoadPalette.IsFootSpan(r) || r.Cls == "steps") continue;
                for (int i = 0; i + 3 < r.Pts.Length; i += 2)
                {
                    double ax = r.Pts[i] / 100.0, az = r.Pts[i + 1] / 100.0, ux = r.Pts[i + 2] / 100.0 - ax, uz = r.Pts[i + 3] / 100.0 - az, ll = ux * ux + uz * uz;
                    double t = ll > 0 ? Math.Clamp(((x - ax) * ux + (z - az) * uz) / ll, 0, 1) : 0;
                    double d = Dist(x, z, ax + ux * t, az + uz * t);
                    if (d < best) { best = d; y = r.YCm / 100.0; }
                }
            }
            return best;
        }

        static void Rect(Span<double> r, double u0, double u1, double v0, double v1)
        {
            r[0] = u0; r[1] = v0; r[2] = u1; r[3] = v0; r[4] = u1; r[5] = v1; r[6] = u0; r[7] = v1;
        }

        /// <summary>The point at arc s along a path of (x, y, z) triples, on the ground plan, and its unit direction.</summary>
        static void PathAt(List<double> path, double s, out double x, out double z, out double dx, out double dz)
        {
            int n = path.Count / 3;
            double acc = 0;
            for (int i = 0; i + 1 < n; i++)
            {
                double ax = path[3 * i], az = path[3 * i + 2], bx = path[3 * i + 3], bz = path[3 * i + 5], len = Dist(ax, az, bx, bz);
                if (len < 1e-9) continue;
                if (s <= acc + len || i + 2 == n)
                {
                    double t = Math.Clamp((s - acc) / len, 0, 1);
                    x = ax + (bx - ax) * t;
                    z = az + (bz - az) * t;
                    dx = (bx - ax) / len;
                    dz = (bz - az) / len;
                    return;
                }
                acc += len;
            }
            x = path[0];
            z = path[2];
            dx = 1;
            dz = 0;
        }

        // ---- Dividers ----

        void Dividers(ChunkStreet st, KitWriter w, StreetScratch ss, bool keep)
        {
            uint yellow = WorldMaterials.Pack(DividerYellow, WorldMaterial.Paint), black = WorldMaterials.Pack(DividerBlack, WorldMaterial.Paint);
            uint rail = WorldMaterials.Pack(RailingSteel, WorldMaterial.Metal);
            foreach (var r in all)
            {
                if (!r.Grade || r.Bridge || r.N < 2) continue;
                var attr = r.Attrs[0];
                if (attr.Dir == 0 || attr.Rank < ClassRank("secondary")) continue;
                double total = r.Arc[r.N - 1];
                bool had = false;
                double px = 0, pz = 0, py = 0;
                int piece = 0;
                for (double s = 2; s <= total - 2; s += DividerStep)
                {
                    AlongRoad(r, s, out double x, out double z, out double dx, out double dz, out double half);
                    bool ok = Twin(r, x, z, dx * attr.Dir, dz * attr.Dir, half, out Drawn q, out double mx, out double mz) && attr.Way < q.Attrs[0].Way
                        && InChunk(mx, mz) && !InAnyPad(mx, mz, r.Y, 0.5) && !OnThirdRoad(mx, mz, r, q);
                    // On the road surface or a raised median band, never over the open ground of a wider median.
                    double band = ok ? BandTop(mx, mz, r.Y) : -1, y = band > 0 ? band : r.Y;
                    if (ok && band <= 0 && !OnCarriageway(mx, mz, r.Y, 0.05)) ok = false;
                    // P-6: a pier's median island takes the divider's place; the divider stops either side of it.
                    if (ok && InIsland(ss, mx, mz, DividerHalf + 0.1)) { ok = false; st.DividerIslandGaps++; }
                    if (!ok) { had = false; continue; }
                    if (had)
                    {
                        double len = Dist(px, pz, mx, mz);
                        if (len > 0.5 && len < 2 * DividerStep)
                        {
                            double hx = (px + mx) / 2, hz = (pz + mz) / 2, hy = (py + y) / 2;
                            w.Beam(px, pz, hx, hz, DividerHalf, py - 0.05, py + DividerHeight, hy - 0.05, hy + DividerHeight, piece % 2 == 0 ? yellow : black);
                            w.Beam(hx, hz, mx, mz, DividerHalf, hy - 0.05, hy + DividerHeight, y - 0.05, y + DividerHeight, piece % 2 == 0 ? black : yellow);
                            double ux = (mx - px) / len, uz = (mz - pz) / len;
                            ss.Solids.Add(new ChunkStreet.Solid { X = hx, Z = hz, Ux = ux, Uz = uz, Along = len / 2, Half = DividerHalf, Y = hy - 0.05, Top = hy + DividerHeight + DividerRail });
                            w.Post(px, pz, py + DividerHeight, py + DividerHeight + DividerRail, ux, uz, 0.03, 0.03, 0.03, 0.03, rail);
                            w.Post(hx, hz, hy + DividerHeight, hy + DividerHeight + DividerRail, ux, uz, 0.03, 0.03, 0.03, 0.03, rail);
                            for (int k = 0; k < 2; k++)
                            {
                                double ya = py + DividerHeight + DividerRail * (k == 0 ? 0.5 : 0.96), yb = y + DividerHeight + DividerRail * (k == 0 ? 0.5 : 0.96);
                                w.Beam(px, pz, mx, mz, 0.02, ya - 0.04, ya, yb - 0.04, yb, rail);
                            }
                            st.DividerPieces++;
                            piece++;
                            if (keep) st.Items.Add(new ChunkStreet.Item { Kind = ChunkStreet.Kind.Divider, X = (px + mx) / 2, Z = (pz + mz) / 2, Y = hy, Top = hy + DividerHeight, Way = attr.Way });
                        }
                    }
                    had = true;
                    px = mx; pz = mz; py = y;
                }
            }
        }

        /// <summary>The one-way road running the other way beside r at (x, z), travelling (tx, tz), within the two half widths plus 1 m; and the point where their edges meet.</summary>
        bool Twin(Drawn r, double x, double z, double tx, double tz, double half, out Drawn twin, out double mx, out double mz)
        {
            twin = null;
            mx = mz = 0;
            double best = double.MaxValue;
            int rank = r.Attrs[0].Rank;
            foreach (var q in all)
            {
                if (q == r || !q.Grade || q.Bridge || q.Attrs[0].Dir == 0 || Math.Abs(q.Attrs[0].Rank - rank) > 1) continue;
                if (x < q.MinX || x > q.MaxX || z < q.MinZ || z > q.MaxZ) continue;
                for (int i = 0; i + 1 < q.N; i++)
                {
                    double ax = q.Pts[2 * i], az = q.Pts[2 * i + 1], ux = q.Pts[2 * i + 2] - ax, uz = q.Pts[2 * i + 3] - az, ll = ux * ux + uz * uz;
                    if (ll < 1e-9) continue;
                    double t = Math.Clamp(((x - ax) * ux + (z - az) * uz) / ll, 0, 1);
                    double qx = ax + ux * t, qz = az + uz * t, d = Dist(x, z, qx, qz);
                    double qh = q.Halves[i] + (q.Halves[i + 1] - q.Halves[i]) * t, l = Math.Sqrt(ll);
                    double dot = (ux / l * q.Attrs[i].Dir) * tx + (uz / l * q.Attrs[i].Dir) * tz;
                    if (d < 1 || d > half + qh + 1 || dot > -0.8 || d >= best) continue;
                    best = d;
                    twin = q;
                    // Where the two carriageway edges meet (or the middle of the overlap or gap between them).
                    double f = (d + half - qh) / (2 * d);
                    mx = x + (qx - x) * f;
                    mz = z + (qz - z) * f;
                }
            }
            return twin != null;
        }

        bool InAnyPad(double x, double z, double y, double margin)
        {
            if (pads == null) return false;
            foreach (var pad in pads)
                if (SameLevel(pad.Y, y) && InPad(pad.Ring, x, z, margin)) return true;
            return false;
        }

        /// <summary>Is (x, z) on a carriageway at grade other than a and b's?</summary>
        bool OnThirdRoad(double x, double z, Drawn a, Drawn b)
        {
            var mine = Mine();
            foreach (int q in SegmentsNear(x, x, z, z))
            {
                var s = mine.SegItems[q];
                if (s.Road == a || s.Road == b || !SameLevel(s.Road.Y, a.Y)) continue;
                int start = s.ExactCount >= 3 ? s.Exact : s.Start, n = s.ExactCount >= 3 ? s.ExactCount : s.Count;
                if (InRing(mine.SegRings, start, n, x, z, 0)) return true;
            }
            return false;
        }
    }
}
