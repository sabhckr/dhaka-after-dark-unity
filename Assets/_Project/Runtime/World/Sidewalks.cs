// Raised sidewalks and kerbs (D-05), added to the road mesh by ChunkRoads.Build after the markings.
// Which sides: the way's sidewalk tag where it has one (both, left, right; no, none and separate
// give none), else both sides of vehicle roads 6 m or wider other than motorways and construction
// (the width rule of the C-03 port). Bridges get none. A sidewalk is a band beside the carriageway:
// - its inner edge is the carriageway edge; it is KerbHeight above the road, with a kerb face down
//   to the road and an outer face down to the ground, so it never floats;
// - its width is the class's (2.5 m on trunk and primary roads, 2 m on secondary and tertiary,
//   1.5 m on the rest), measured every WalkSample metres (closer where it changes) against the
//   nearest building wall, carriageway, junction pad or earlier band: where one is closer than
//   that width plus FillGap, the band runs to it (to a wall, 2 cm short), so a narrow median
//   between two carriageways is paved across and a band's end follows an oblique road's edge,
//   and where the room is under MinWalk there is none, nor where its edge is inside a building
//   (mapped footprints often reach over a road's estimated edge);
// - it is cut where it would run onto any carriageway, junction pad or the band of a road drawn
//   before it (roads go in order of class rank, then way id), so bands neither cover roads nor
//   overlap one another; an end cut by an oblique edge follows that edge (see Pieces);
// - in line with a zebra it drops to the road, over RampLength each side of the zebra, whichever
//   road's sidewalk it is (the zebra's own, a corner's, a median's within RampReach);
// - its paving is one of three tints, chosen by a hash of the way id; one 10 m slot in four (by a
//   hash of the way, side and slot) has a broken slab, SlabLength long and SlabSink lower, with
//   step faces, except on ramps and bus stops.
// Bus stops (mapped points) take the nearest at-grade vehicle road within StopNear of its edge, on
// the stop's side (the left of travel where it sits on the centre line): StopReach either way of
// it the band is a stop pad with a painted kerb, and a side with no sidewalk gets a band of
// StopWidth there. Lanes with no sidewalk (residential, living street, unclassified) get an open
// drain each side instead, built after every sidewalk: DrainWidth wide, a channel between two
// lips, covered flush in line with a zebra, and cut like a band wherever it has no room (it is
// not widened to walls).
// Like the markings, all of it follows the roads as drawn and is cut on the chunk border, so the
// neighbour builds the rest.
using System;
using System.Collections.Generic;

namespace Dad.World
{
    public sealed partial class ChunkRoads
    {
        public const double KerbHeight = 0.15, MinWalk = 0.6, FillGap = 1.0, WallClear = 0.02, WalkSample = 5;
        /// <summary>Samples are halved, down to MinSample metres apart, where the width changes by more than WidthStep between them or falls below the least a band needs.</summary>
        public const double WidthStep = 0.25, MinSample = 0.1;
        /// <summary>A quad left out by Sound is halved, down to this length, so only the part of it that would overlap is lost.</summary>
        public const double SplitLength = 0.25;
        /// <summary>How far a carriageway cut on the chunk border is taken to run on past it: its cut end runs along the border.</summary>
        const double BorderExtend = 50;
        /// <summary>How far inside a narrowed band its outer edge is tested for carriageways and pads.</summary>
        const double OuterInset = 0.05;
        public const double RampLength = 1.8, RampLift = 0.01;
        /// <summary>How far beyond a zebra's carriageway its ramps reach: the widest band, filled.</summary>
        public const double RampReach = 3.5;
        public const uint KerbStone = 0x9d988e, WalkSlab = RoadPalette.Sidewalk, WalkBrick = 0x8e6e5d, WalkGrey = 0x7e7b75;
        /// <summary>Broken slabs: the chance per SlabSlot metres of a side, and the slab's length and drop.</summary>
        public const double SlabSlot = 10, SlabChance = 0.25, SlabLength = 0.9, SlabSink = 0.04;
        /// <summary>Bus stops: how far beyond the carriageway edge a stop may be mapped, the pad's reach either way of it, and its width where the side has no sidewalk.</summary>
        public const double StopNear = 10, StopReach = 6, StopWidth = 2;
        /// <summary>Open drains: width, each lip's width and height above the road, the channel's depth below it, and the cover's height at a crossing.</summary>
        public const double DrainWidth = 0.6, DrainLip = 0.1, DrainLipHeight = 0.06, DrainDepth = 0.04, DrainCover = 0.01;
        public const uint BrokenSlab = 0x625d55, StopPad = 0x9b968c, StopKerb = 0xb9a548, DrainStone = 0x8f8a80, DrainWall = 0x6b665d, Sludge = 0x2c2a22;

        public enum WalkKind : byte { Paving, Broken, Stop, Drain, Covered }

        /// <summary>A sidewalk or drain quad's top, for tests: inner edge a to b, outer edge a to b, and heights at a and b.</summary>
        public struct WalkPiece
        {
            public long Way;
            public int Side;
            public WalkKind Kind;
            public double InAx, InAz, InBx, InBz, OutAx, OutAz, OutBx, OutBz, Ya, Yb, RoadY;
        }

        /// <summary>Every sidewalk and drain quad, when built with keepMarks.</summary>
        public List<WalkPiece> Walks;

        /// <summary>A carriageway segment as drawn (after chaining, stems and fillets), for tests: centre a to b, half widths there, level, and the directions its edges are set out along at a and b (unit normals, longer at a mitre).</summary>
        public struct RoadLine
        {
            public long Way;
            public bool Bridge;
            public double Ax, Az, Bx, Bz, HalfA, HalfB, Y, Sx, Sz, Ex, Ez;
        }

        /// <summary>Every drawn carriageway segment, when built with keepMarks.</summary>
        public List<RoadLine> Lines;

        /// <summary>Which sides of a road get a sidewalk, from its tag or the width rule.</summary>
        public static void WalkSidesFor(string cls, string tag, double width, out bool left, out bool right)
        {
            switch (tag)
            {
                case "both": left = right = true; return;
                case "left": left = true; right = false; return;
                case "right": left = false; right = true; return;
                case "no": case "none": case "separate": left = right = false; return;
            }
            left = right = cls != null && !RoadPalette.IsFoot(cls) && cls != "construction" && cls != "motorway" && cls != "motorway_link" && width >= 6;
        }

        public static double WalkWidth(string cls)
        {
            switch (cls)
            {
                case "trunk": case "trunk_link": case "primary": case "primary_link": return 2.5;
                case "secondary": case "secondary_link": case "tertiary": case "tertiary_link": return 2;
                default: return 1.5;
            }
        }

        /// <summary>Does a side of this class with no sidewalk have an open drain?</summary>
        public static bool DrainClass(string cls) => cls == "residential" || cls == "living_street" || cls == "unclassified";

        // ---- Building walls, bucketed for the width rays ----

        const int WallCell = 10, WallCells = (int)Size / WallCell;

        void IndexWalls(DadcChunk c)
        {
            var mine = Mine();
            var walls = mine.Walls;
            walls.Clear();
            foreach (int k in mine.WallTouched) mine.WallCellLists[k].Clear();
            mine.WallTouched.Clear();
            if (c.Buildings == null) return;
            foreach (var b in c.Buildings)
            {
                AddWalls(b.Outer, walls);
                if (b.Holes != null) foreach (var h in b.Holes) AddWalls(h, walls);
            }
            for (int w = 0; w < walls.Count; w += 4)
            {
                int x0 = Cell(Math.Min(walls[w], walls[w + 2])), x1 = Cell(Math.Max(walls[w], walls[w + 2]));
                int z0 = Cell(Math.Min(walls[w + 1], walls[w + 3])), z1 = Cell(Math.Max(walls[w + 1], walls[w + 3]));
                for (int cx = x0; cx <= x1; cx++)
                    for (int cz = z0; cz <= z1; cz++)
                    {
                        int k = cx + cz * WallCells;
                        var list = mine.WallCellLists[k] ??= new List<int>(8);
                        if (list.Count == 0) mine.WallTouched.Add(k);
                        list.Add(w);
                    }
            }
        }

        static void AddWalls(int[] ring, List<double> walls)
        {
            if (ring == null) return;
            int n = ring.Length / 2;
            for (int k = 0; k < n; k++)
            {
                int j = (k + 1) % n;
                walls.Add(ring[2 * k] / 100.0);
                walls.Add(ring[2 * k + 1] / 100.0);
                walls.Add(ring[2 * j] / 100.0);
                walls.Add(ring[2 * j + 1] / 100.0);
            }
        }

        static int Cell(double v) => Math.Clamp((int)Math.Floor(v / WallCell), 0, WallCells - 1);

        /// <summary>Distance along (nx, nz) from (px, pz) to the nearest building wall, up to max.</summary>
        double WallGap(double px, double pz, double nx, double nz, double max)
        {
            var mine = Mine();
            var walls = mine.Walls;
            double best = max, ex = px + nx * max, ez = pz + nz * max;
            int x0 = Cell(Math.Min(px, ex)), x1 = Cell(Math.Max(px, ex)), z0 = Cell(Math.Min(pz, ez)), z1 = Cell(Math.Max(pz, ez));
            for (int cx = x0; cx <= x1; cx++)
                for (int cz = z0; cz <= z1; cz++)
                {
                    var list = mine.WallCellLists[cx + cz * WallCells];
                    if (list == null) continue;
                    foreach (int w in list)
                    {
                        double ax = walls[w], az = walls[w + 1], wx = walls[w + 2] - ax, wz = walls[w + 3] - az;
                        double den = nx * wz - nz * wx;
                        if (Math.Abs(den) < 1e-12) continue;
                        double t = ((ax - px) * wz - (az - pz) * wx) / den, u = ((ax - px) * nz - (az - pz) * nx) / den;
                        if (t >= 0 && t < best && u >= 0 && u <= 1) best = t;
                    }
                }
            return best;
        }

        /// <summary>Is (px, pz) inside a building footprint? Even-odd, along a ray to the nearest chunk edge through the wall cells.</summary>
        bool InBuilding(double px, double pz)
        {
            var mine = Mine();
            var walls = mine.Walls;
            var seen = mine.WalkSeen;
            seen.Clear();
            bool inside = false;
            int cx0 = Cell(px), cz0 = Cell(pz), last = WallCells - 1;
            // Along x (dir +1 east, -1 west) or z (north, south), whichever has the fewest cells.
            int steps = Math.Min(Math.Min(cx0, last - cx0), Math.Min(cz0, last - cz0));
            bool alongX = steps == cx0 || steps == last - cx0;
            int dir = alongX ? (steps == last - cx0 ? 1 : -1) : (steps == last - cz0 ? 1 : -1);
            double u = alongX ? px : pz, v = alongX ? pz : px;
            for (int k = 0; k <= steps; k++)
            {
                int cx = alongX ? cx0 + dir * k : cx0, cz = alongX ? cz0 : cz0 + dir * k;
                var list = mine.WallCellLists[cx + cz * WallCells];
                if (list == null) continue;
                foreach (int w in list)
                {
                    // (a, b) along the ray, (c, d) across it.
                    double c = alongX ? walls[w + 1] : walls[w], d = alongX ? walls[w + 3] : walls[w + 2];
                    if ((c > v) == (d > v) || !seen.Add(w)) continue;
                    double a = alongX ? walls[w] : walls[w + 1], b = alongX ? walls[w + 2] : walls[w + 3];
                    if ((a + (v - c) * (b - a) / (d - c) - u) * dir > 0) inside = !inside;
                }
            }
            return inside;
        }

        // ---- Carriageway segments, bucketed like the walls ----

        /// <summary>
        /// A carriageway piece in the index: segment Seg of Road, or the wedge of a bevelled bend at
        /// its start. Ring (Start, Count points in SegRings) is the piece as other roads' bands are
        /// kept off it, run on past the segment's ends; Exact (ExactCount) is its outline as drawn,
        /// out to the mitres, a little inside: what its own road's bands nearby are kept off, as
        /// they meet it on the mitre.
        /// </summary>
        struct SegItem
        {
            public Drawn Road;
            public int Seg, Start, Count, Exact, ExactCount;
            public bool Wedge;
            public double MinX, MaxX, MinZ, MaxZ;
        }

        /// <summary>How far inside its outline a piece's exact ring is, so a band meeting it on the mitre only touches it.</summary>
        const double ExactInset = 0.02;

        /// <summary>
        /// Indexes the carriageways of the drawn roads (all) as convex rings (see SegItem), by the
        /// cells they touch: each segment as its rectangle (SegmentRect) together with its
        /// outline as drawn; and each bevelled bend's wedge.
        /// </summary>
        void IndexSegments()
        {
            var mine = Mine();
            var items = Emptied(mine.SegItems);
            var rings = Emptied(mine.SegRings);
            foreach (int k in mine.SegTouched) mine.SegCellLists[k].Clear();
            mine.SegTouched.Clear();
            Span<double> pts = stackalloc double[16];
            foreach (var o in all)
                for (int j = 0; j + 1 < o.N; j++)
                {
                    if (o.Arc[j + 1] - o.Arc[j] < 1e-6 || RoadPalette.IsFoot(o.Attrs[j].Cls)) continue;
                    SegmentRect(o, j, pts, double.MinValue, double.MaxValue, double.MinValue, double.MaxValue);
                    double cx = o.Pts[2 * j], cz = o.Pts[2 * j + 1], ex = o.Pts[2 * j + 2], ez = o.Pts[2 * j + 3], ha = o.Halves[j], hb = o.Halves[j + 1];
                    pts[8] = cx + o.Sx[j] * ha; pts[9] = cz + o.Sz[j] * ha;
                    pts[10] = ex + o.Ex[j] * hb; pts[11] = ez + o.Ez[j] * hb;
                    pts[12] = ex - o.Ex[j] * hb; pts[13] = ez - o.Ez[j] * hb;
                    pts[14] = cx - o.Sx[j] * ha; pts[15] = cz - o.Sz[j] * ha;
                    AddRing(o, j, pts, pts.Slice(8, 8), false);
                    // A bevel at the segment's end: the wedge between the two segments' own ends.
                    if (j + 2 < o.N && (o.Ex[j] != o.Sx[j + 1] || o.Ez[j] != o.Sz[j + 1]) && o.Arc[j + 2] - o.Arc[j + 1] >= 1e-6 && !RoadPalette.IsFoot(o.Attrs[j + 1].Cls))
                    {
                        Span<double> w = pts.Slice(0, 8);
                        w[0] = ex + o.Ex[j] * hb; w[1] = ez + o.Ez[j] * hb;
                        w[2] = ex + o.Sx[j + 1] * hb; w[3] = ez + o.Sz[j + 1] * hb;
                        w[4] = ex - o.Ex[j] * hb; w[5] = ez - o.Ez[j] * hb;
                        w[6] = ex - o.Sx[j + 1] * hb; w[7] = ez - o.Sz[j + 1] * hb;
                        AddRing(o, j + 1, w, w, true);
                    }
                }

            void AddRing(Drawn o, int j, Span<double> p, Span<double> exact, bool wedge)
            {
                int start = rings.Count, n = Hull(p, rings);
                int inner = rings.Count, m = Hull(exact, rings);
                if (n < 3) { rings.RemoveRange(start, rings.Count - start); return; }
                // The exact ring drawn in towards its middle.
                double mx = 0, mz = 0;
                for (int k = inner; k < rings.Count; k += 2) { mx += rings[k]; mz += rings[k + 1]; }
                mx /= m;
                mz /= m;
                for (int k = inner; k < rings.Count; k += 2)
                {
                    double dx = mx - rings[k], dz = mz - rings[k + 1], d = Math.Sqrt(dx * dx + dz * dz);
                    if (d < 1e-9) continue;
                    double f = Math.Min(ExactInset, d / 2) / d;
                    rings[k] += dx * f;
                    rings[k + 1] += dz * f;
                }
                double minX = double.MaxValue, maxX = double.MinValue, minZ = double.MaxValue, maxZ = double.MinValue;
                for (int k = start; k < inner; k += 2)
                {
                    minX = Math.Min(minX, rings[k]); maxX = Math.Max(maxX, rings[k]);
                    minZ = Math.Min(minZ, rings[k + 1]); maxZ = Math.Max(maxZ, rings[k + 1]);
                }
                int item = items.Count;
                items.Add(new SegItem { Road = o, Seg = j, Start = start, Count = n, Exact = inner, ExactCount = m, Wedge = wedge, MinX = minX, MaxX = maxX, MinZ = minZ, MaxZ = maxZ });
                for (int x = Cell(minX); x <= Cell(maxX); x++)
                    for (int z = Cell(minZ); z <= Cell(maxZ); z++)
                    {
                        int k = x + z * WallCells;
                        var list = mine.SegCellLists[k] ??= new List<int>(8);
                        if (list.Count == 0) mine.SegTouched.Add(k);
                        list.Add(item);
                    }
            }
        }

        /// <summary>The convex hull of points p (x, z pairs), counter-clockwise without collinear points, appended to ring; returns its number of points.</summary>
        static int Hull(Span<double> p, List<double> ring)
        {
            // Monotone chain over the points in x (then z) order: a few points, so an insertion sort.
            int n = p.Length / 2;
            Span<int> order = stackalloc int[n];
            for (int k = 0; k < n; k++)
            {
                int at = k;
                for (; at > 0 && (p[2 * order[at - 1]] > p[2 * k] || p[2 * order[at - 1]] == p[2 * k] && p[2 * order[at - 1] + 1] > p[2 * k + 1]); at--) order[at] = order[at - 1];
                order[at] = k;
            }
            Span<int> hull = stackalloc int[2 * n];
            int h = 0;
            for (int k = 0; k < n; k++) Add(p, hull, ref h, order[k], 1);
            for (int k = n - 2, lower = h; k >= 0; k--) Add(p, hull, ref h, order[k], lower);
            h--; // the first point, closing the ring
            for (int k = 0; k < h; k++)
            {
                ring.Add(p[2 * hull[k]]);
                ring.Add(p[2 * hull[k] + 1]);
            }
            return h;

            // Drops the last points while they would not turn left on to point v.
            static void Add(Span<double> p, Span<int> hull, ref int h, int v, int floor)
            {
                while (h > floor)
                {
                    int a = hull[h - 2], b = hull[h - 1];
                    double cr = (p[2 * b] - p[2 * a]) * (p[2 * v + 1] - p[2 * a + 1]) - (p[2 * b + 1] - p[2 * a + 1]) * (p[2 * v] - p[2 * a]);
                    if (cr > 1e-9) break;
                    h--;
                }
                hull[h++] = v;
            }
        }

        /// <summary>
        /// The ring (start, n points in SegRings) a band on segment i of road r is kept off for
        /// item q; false when none: another level, or the band's own segment. Its own road's
        /// pieces nearby are their exact rings (see SegItem).
        /// </summary>
        bool RingFor(Drawn r, int i, int q, out int start, out int n)
        {
            var s = Mine().SegItems[q];
            start = n = 0;
            if (!SameLevel(s.Road.Y, r.Y) || s.Road == r && s.Seg == i && !s.Wedge) return false;
            bool own = s.Road == r && OwnNear(r, i, s.Seg);
            start = own ? s.Exact : s.Start;
            n = own ? s.ExactCount : s.Count;
            return n >= 3;
        }

        /// <summary>Clips the line from (ax, az) along (dx, dz) to the ring of n points from start in SegRings (counter-clockwise).</summary>
        bool ClipRing(int start, int n, double ax, double az, double dx, double dz, out double t0, out double t1)
        {
            var rings = Mine().SegRings;
            t0 = 0;
            t1 = 1;
            for (int k = 0; k < n; k++)
            {
                int a = start + 2 * k, b = start + 2 * ((k + 1) % n);
                if (!Half(ax, az, dx, dz, rings[a], rings[a + 1], rings[b], rings[b + 1], ref t0, ref t1)) return false;
            }
            return true;
        }

        /// <summary>The carriageway pieces (indices into Mine().SegItems) whose rings' bounds reach the box, each once.</summary>
        List<int> SegmentsNear(double minX, double maxX, double minZ, double maxZ)
        {
            var mine = Mine();
            var found = Emptied(mine.SegFound);
            var seen = mine.SegSeen;
            seen.Clear();
            for (int cx = Cell(minX); cx <= Cell(maxX); cx++)
                for (int cz = Cell(minZ); cz <= Cell(maxZ); cz++)
                {
                    var list = mine.SegCellLists[cx + cz * WallCells];
                    if (list == null) continue;
                    foreach (int q in list)
                    {
                        var s = mine.SegItems[q];
                        if (s.MaxX >= minX && s.MinX <= maxX && s.MaxZ >= minZ && s.MinZ <= maxZ && seen.Add(q)) found.Add(q);
                    }
                }
            return found;
        }

        // ---- Bands ----

        struct WalkQuad
        {
            public double X0, Z0, X1, Z1, X2, Z2, X3, Z3, Y, MinX, MinZ, MaxX, MaxZ;
            public Drawn Road;
            public int Side, Seg;
        }

        /// <summary>Adds the sidewalks of every drawn road, after the roads, pads and markings.</summary>
        void BuildSidewalks(DadcChunk c, List<Drawn> drawn, List<Pad> pads, bool keep)
        {
            if (keep)
            {
                Walks = new List<WalkPiece>();
                Lines = new List<RoadLine>();
                foreach (var r in drawn)
                    for (int i = 0; i + 1 < r.N; i++)
                        if (r.Arc[i + 1] - r.Arc[i] >= 1e-6)
                            Lines.Add(new RoadLine { Way = r.Attrs[i].Way, Bridge = r.Bridge, Ax = r.Pts[2 * i], Az = r.Pts[2 * i + 1], Bx = r.Pts[2 * i + 2], Bz = r.Pts[2 * i + 3], HalfA = r.Halves[i], HalfB = r.Halves[i + 1], Y = r.Y, Sx = r.Sx[i], Sz = r.Sz[i], Ex = r.Ex[i], Ez = r.Ez[i] });
            }
            var mine = Mine();
            mine.WalkQuads.Clear();
            mine.StopQuads.Clear();
            foreach (int k in mine.WalkTouched) mine.WalkCellLists[k].Clear();
            mine.WalkTouched.Clear();
            IndexWalls(c);
            all = drawn;
            IndexSegments();
            // Higher class rank first, then way id: the first band at a corner keeps it.
            var order = mine.Order2;
            order.Clear();
            for (int i = 0; i < drawn.Count; i++)
            {
                int j = order.Count;
                order.Add(i);
                while (j > 0 && Before(drawn[i], drawn[order[j - 1]])) { order[j] = order[j - 1]; j--; order[j] = i; }
            }
            FindStops(c, drawn);
            // Sidewalks and stops first, then drains in what room is left.
            for (int pass = 0; pass < 2; pass++)
                foreach (int k in order)
                {
                    var r = drawn[k];
                    if (r.Bridge) continue;
                    for (int side = -1; side <= 1; side += 2) WalkSide(r, side, pads, pass == 1);
                }
            mine.Stops.Clear();
            // The carriageway index and road list stay for the street furniture (BuildStreet), which clears them.
        }

        static bool Before(Drawn a, Drawn b) =>
            a.Attrs[0].Rank != b.Attrs[0].Rank ? a.Attrs[0].Rank > b.Attrs[0].Rank : a.Attrs[0].Way < b.Attrs[0].Way;

        /// <summary>
        /// Each bus stop's stretch: on the at-grade vehicle road whose edge is nearest, within
        /// StopNear; on the side the stop is mapped (the left of travel where it is within a metre
        /// of the centre line); StopReach either way along the road.
        /// </summary>
        void FindStops(DadcChunk c, List<Drawn> drawn)
        {
            var stops = Emptied(Mine().Stops);
            if (c.Points == null) return;
            foreach (var p in c.Points)
            {
                if (p.Kind != "bus_stop") continue;
                double px = p.East / 100.0, pz = p.North / 100.0;
                Drawn best = null;
                double bestOut = double.MaxValue, bestS = 0;
                foreach (var r in drawn)
                {
                    if (r.Bridge) continue;
                    Closest(r, px, pz, out double d, out double s);
                    At(r, s, out _, out _, out _, out _, out double h, out int seg);
                    if (!IsVehicle(r.Attrs[seg].Cls) || d - h > StopNear || d - h >= bestOut) continue;
                    best = r;
                    bestOut = d - h;
                    bestS = s;
                }
                if (best == null) continue;
                At(best, bestS, out double x, out double z, out double tx, out double tz, out _, out int i);
                double across = (px - x) * -tz + (pz - z) * tx;
                int side = Math.Abs(across) >= 1 ? Math.Sign(across) : best.Attrs[i].Dir < 0 ? -1 : 1;
                stops.Add((best, side, bestS - StopReach, bestS + StopReach));
                BusStops++;
            }
        }

        /// <summary>Is there a bus stop on side of segment i? Its stretches (t along the segment, in order) go in spans.</summary>
        bool StopOn(Drawn r, int i, int side, List<(double a, double b)> spans)
        {
            spans?.Clear();
            double s0 = r.Arc[i], len = r.Arc[i + 1] - s0;
            bool any = false;
            if (len < 1e-6) return false;
            foreach (var (o, sd, a, b) in Mine().Stops)
            {
                if (o != r || sd != side || b <= s0 || a >= s0 + len) continue;
                any = true;
                if (spans == null) return true;
                double ta = Math.Max(0, (a - s0) / len), tb = Math.Min(1, (b - s0) / len);
                int at = spans.Count;
                spans.Add((ta, tb));
                for (; at > 0 && spans[at - 1].a > ta; at--) (spans[at - 1], spans[at]) = (spans[at], spans[at - 1]);
            }
            // Two stops' stretches may meet.
            if (spans != null)
                for (int k = 0; k + 1 < spans.Count;)
                    if (spans[k + 1].a <= spans[k].b) { spans[k] = (spans[k].a, Math.Max(spans[k].b, spans[k + 1].b)); spans.RemoveAt(k + 1); }
                    else k++;
            return any;
        }

        /// <summary>Does side of segment i get a band in this pass: a sidewalk or a stop's, or (drain) an open drain?</summary>
        bool Wants(Drawn r, int i, int side, bool drain)
        {
            var a = r.Attrs[i];
            bool walk = side > 0 ? a.WalkLeft : a.WalkRight;
            return drain ? !walk && DrainClass(a.Cls) && !StopOn(r, i, side, null) : walk || StopOn(r, i, side, null);
        }

        void WalkSide(Drawn r, int side, List<Pad> pads, bool drain)
        {
            var mine = Mine();
            walkPending = false; // the last piece ended at the end of the previous segment, its end face held back
            for (int i = 0; i + 1 < r.N; i++)
            {
                var a = r.Attrs[i];
                double len = r.Arc[i + 1] - r.Arc[i];
                if (len < 0.05 || !Wants(r, i, side, drain)) { FlushEnd(); continue; }
                bool walk = side > 0 ? a.WalkLeft : a.WalkRight, stopped = !drain && StopOn(r, i, side, mine.StopSpans);
                if (!stopped) mine.StopSpans.Clear();
                // Drains are not widened to what is beside them, and need nearly their full width.
                double wr = drain ? DrainWidth : walk ? WalkWidth(a.Cls) : StopWidth, fill = drain ? 0 : FillGap, least = drain ? DrainWidth - 0.05 : MinWalk;
                double cx = r.Pts[2 * i], cz = r.Pts[2 * i + 1], ex = r.Pts[2 * i + 2], ez = r.Pts[2 * i + 3];
                double ha = r.Halves[i] * side, hb = r.Halves[i + 1] * side;
                // Widths at samples along the segment: to a building wall, carriageway, pad or
                // earlier band, when one is closer than the class width plus FillGap. Samples every
                // WalkSample metres, beside every corner of a pad or earlier band within reach (so
                // one between two samples is not missed), and closer together where the width
                // changes or falls too narrow, so a band narrows and stops where its obstacle does.
                int m = Math.Max(1, (int)Math.Ceiling(len / WalkSample));
                var widths = mine.Widths;
                var ts = mine.WidthTs;
                widths.Clear();
                ts.Clear();
                for (int k = 0; k <= m; k++) ts.Add((double)k / m);
                double iax = cx + r.Sx[i] * ha, iaz = cz + r.Sz[i] * ha, ibx = ex + r.Ex[i] * hb, ibz = ez + r.Ez[i] * hb;
                CornerSamples(r, i, side, iax, iaz, ibx, ibz, wr + fill, pads);
                for (int k = 0; k < ts.Count; k++)
                    // Where a piece carries on from the last segment it keeps that piece's end width.
                    widths.Add(k == 0 && walkPending ? pendingWidth : WidthAt(r, i, ha, hb, side, wr, fill, ts[k], pads));
                for (int k = 0; k + 1 < ts.Count;)
                {
                    if ((Math.Abs(widths[k + 1] - widths[k]) > WidthStep || widths[k] < least != widths[k + 1] < least) && (ts[k + 1] - ts[k]) * len > MinSample)
                    {
                        double t = (ts[k] + ts[k + 1]) / 2;
                        ts.Insert(k + 1, t);
                        widths.Insert(k + 1, WidthAt(r, i, ha, hb, side, wr, fill, t, pads));
                    }
                    else k++;
                }
                m = ts.Count - 1;
                // An outer edge that would turn back on itself (an obstacle edge running back along
                // the road) is too narrow there.
                double ttx = (ex - cx) / len, ttz = (ez - cz) / len, lastAlong = double.MinValue;
                for (int k = 0; k <= m; k++)
                {
                    Edge(r, i, ha, hb, side, ts[k], ts[k], out _, out _, out double ox, out double oz);
                    double along = (ox - cx) * ttx + (oz - cz) * ttz;
                    if (along < lastAlong - 1e-6) widths[k] = 0;
                    else lastAlong = along;
                }
                bool narrowed = false;
                foreach (double w in widths) narrowed |= w != wr;
                // Free stretches of the inner and outer edges; a narrowed outer edge is tested
                // between samples, just inside the band.
                var fin = Emptied(mine.FreeIn);
                var fout = Emptied(mine.FreeOut);
                Free(r, i, side, iax, iaz, ibx, ibz, pads, fin);
                if (!narrowed)
                {
                    Edge(r, i, ha, hb, side, 0, 0, out _, out _, out double oax, out double oaz);
                    Edge(r, i, ha, hb, side, 1, 1, out _, out _, out double obx, out double obz);
                    Free(r, i, side, oax, oaz, obx, obz, pads, fout);
                }
                else
                {
                    var part = mine.FreePart;
                    for (int k = 0; k < m; k++)
                    {
                        Edge(r, i, ha, hb, side, ts[k], ts[k], out _, out _, out double oax, out double oaz, -OuterInset);
                        Edge(r, i, ha, hb, side, ts[k + 1], ts[k + 1], out _, out _, out double obx, out double obz, -OuterInset);
                        Free(r, i, side, oax, oaz, obx, obz, pads, part);
                        foreach (var (pa, pb) in part)
                        {
                            double ta = ts[k] + pa * (ts[k + 1] - ts[k]), tb = ts[k] + pb * (ts[k + 1] - ts[k]);
                            if (fout.Count > 0 && ta - fout[fout.Count - 1].b < 1e-9) fout[fout.Count - 1] = (fout[fout.Count - 1].a, tb);
                            else fout.Add((ta, tb));
                        }
                    }
                }
                // Too narrow at a sample: none from halfway to the sample before it to halfway to the one after.
                var next = Emptied(mine.Spans2);
                for (int k = 0; k <= m; k++)
                    if (widths[k] < least)
                    {
                        double c0 = k == 0 ? -1 : (ts[k - 1] + ts[k]) / 2, c1 = k == m ? 2 : (ts[k] + ts[k + 1]) / 2;
                        Cut(fin, next, c0, c1);
                        Cut(fout, next, c0, c1);
                    }
                // A side with no sidewalk has a band only at its stops.
                if (!drain && !walk)
                {
                    var kept = Emptied(mine.Kept);
                    foreach (var (fa, fb) in fin)
                        foreach (var (sa, sb) in mine.StopSpans)
                            if (Math.Min(fb, sb) > Math.Max(fa, sa)) kept.Add((Math.Max(fa, sa), Math.Min(fb, sb)));
                    fin.Clear();
                    fin.AddRange(kept);
                }
                double skew = 4 * wr / len;
                bool nextWants = i + 2 < r.N && r.Arc[i + 2] - r.Arc[i + 1] >= 0.05 && Wants(r, i + 1, side, drain);
                var cuts = Emptied(mine.WalkCuts);
                bool joined = false;
                foreach (var (fa, fb) in fin)
                    foreach (var (ga, gb) in fout)
                    {
                        double a0 = Math.Max(fa, ga), b0 = Math.Min(fb, gb);
                        if (b0 - a0 < 0.05 / len) continue;
                        // An end cut by an oblique edge: the other edge runs on to its own cut, within
                        // the skew, unless the end between them would cross something; then it is square.
                        double ai = Math.Max(fa, a0 - skew), ao = Math.Max(ga, a0 - skew);
                        double bi = Math.Min(fb, b0 + skew), bo = Math.Min(gb, b0 + skew);
                        int at = cuts.Count;
                        cuts.Add((ai, ao, bi, bo, a0, b0));
                        for (; at > 0 && cuts[at - 1].a0 > a0; at--) (cuts[at - 1], cuts[at]) = (cuts[at], cuts[at - 1]);
                    }
                for (int p = 0; p < cuts.Count; p++)
                {
                    var (ai, ao, bi, bo, a0, b0) = cuts[p];
                    // Neither runs on past where its neighbour on the segment begins or ends.
                    if (p > 0) { ai = Math.Max(ai, cuts[p - 1].bi); ao = Math.Max(ao, cuts[p - 1].bo); }
                    if (p + 1 < cuts.Count) { bi = Math.Min(bi, cuts[p + 1].a0); bo = Math.Min(bo, cuts[p + 1].a0); }
                    if (ai != ao && !EndClear(r, i, ha, hb, side, ai, ao, pads)) ai = ao = a0;
                    if (bi != bo && !EndClear(r, i, ha, hb, side, bi, bo, pads)) bi = bo = b0;
                    cuts[p] = (ai, ao, bi, bo, a0, b0);
                    joined |= walkPending && ai <= 1e-9 && ao <= 1e-9;
                }
                // The last segment's piece gets its end face unless one here carries on from it
                // (and then only if that one's first quad is left out; see Piece).
                if (!joined) FlushEnd();
                foreach (var (ai, ao, bi, bo, a0, b0) in cuts)
                {
                    bool startJoined = ai <= 1e-9 && ao <= 1e-9 && joined;
                    bool endJoined = bi >= 1 - 1e-9 && bo >= 1 - 1e-9 && nextWants;
                    Piece(r, i, side, ha, hb, ai, ao, bi, bo, a0, b0, !startJoined, !endJoined, a, pads, drain);
                }
                if (cuts.Count > 0)
                {
                    if (drain) Drains++;
                    else Sidewalks++;
                }
            }
            FlushEnd();
        }

        /// <summary>
        /// Adds samples (t along the inner edge a to b) beside each corner of a carriageway, pad,
        /// earlier band or building within reach outside it, and where a building wall crosses it,
        /// keeping them in order: a band starts, stops or narrows there, maybe between two samples.
        /// </summary>
        void CornerSamples(Drawn r, int i, int side, double ax, double az, double bx, double bz, double reach, List<Pad> pads)
        {
            var mine = Mine();
            var ts = mine.WidthTs;
            double dx = bx - ax, dz = bz - az, l2 = dx * dx + dz * dz;
            if (l2 < 1e-8) return;
            double len = Math.Sqrt(l2), nx = -dz / len * side, nz = dx / len * side;
            double minX = Math.Min(ax, bx) - reach, maxX = Math.Max(ax, bx) + reach, minZ = Math.Min(az, bz) - reach, maxZ = Math.Max(az, bz) + reach;
            int before = ts.Count;
            var seen = mine.WalkSeen;
            seen.Clear();
            for (int cx = Cell(minX); cx <= Cell(maxX); cx++)
                for (int cz = Cell(minZ); cz <= Cell(maxZ); cz++)
                {
                    var list = mine.WalkCellLists[cx + cz * WallCells];
                    if (list == null) continue;
                    foreach (int q in list)
                    {
                        var w = mine.WalkQuads[q];
                        if (!SameLevel(w.Y, r.Y) || w.MaxX < minX || w.MinX > maxX || w.MaxZ < minZ || w.MinZ > maxZ || !seen.Add(q)) continue;
                        if (w.Road == r && w.Side == side && OwnNear(r, i, w.Seg)) continue;
                        Corner(w.X0, w.Z0);
                        Corner(w.X1, w.Z1);
                        Corner(w.X2, w.Z2);
                        Corner(w.X3, w.Z3);
                    }
                }
            foreach (var pad in pads)
            {
                if (!SameLevel(pad.Y - RoadPalette.PadLift, r.Y)) continue;
                foreach (var (x, z) in pad.Ring) if (x >= minX && x <= maxX && z >= minZ && z <= maxZ) Corner(x, z);
            }
            var rings = mine.SegRings;
            foreach (int q in SegmentsNear(minX, maxX, minZ, maxZ))
            {
                if (!RingFor(r, i, q, out int start, out int count)) continue;
                for (int k = start; k < start + 2 * count; k += 2)
                    if (rings[k] >= minX && rings[k] <= maxX && rings[k + 1] >= minZ && rings[k + 1] <= maxZ) Corner(rings[k], rings[k + 1]);
            }
            // Every building corner is the start of one of its walls.
            var walls = mine.Walls;
            seen.Clear();
            for (int cx = Cell(minX); cx <= Cell(maxX); cx++)
                for (int cz = Cell(minZ); cz <= Cell(maxZ); cz++)
                {
                    var list = mine.WallCellLists[cx + cz * WallCells];
                    if (list == null) continue;
                    foreach (int w in list)
                    {
                        if (!seen.Add(w)) continue;
                        double wx0 = walls[w], wz0 = walls[w + 1], wx = walls[w + 2] - wx0, wz = walls[w + 3] - wz0;
                        if (wx0 >= minX && wx0 <= maxX && wz0 >= minZ && wz0 <= maxZ) Corner(wx0, wz0);
                        double den = dx * wz - dz * wx;
                        if (Math.Abs(den) < 1e-12) continue;
                        double t = ((wx0 - ax) * wz - (wz0 - az) * wx) / den, u = ((wx0 - ax) * dz - (wz0 - az) * dx) / den;
                        if (t > 0 && t < 1 && u >= 0 && u <= 1) Corner(ax + dx * t, az + dz * t);
                    }
                }
            if (ts.Count == before) return;
            ts.Sort();
            int n = 1;
            for (int k = 1; k < ts.Count; k++) if (ts[k] - ts[n - 1] > 1e-6) ts[n++] = ts[k];
            ts.RemoveRange(n, ts.Count - n);

            void Corner(double x, double z)
            {
                double t = ((x - ax) * dx + (z - az) * dz) / l2, d = (x - ax) * nx + (z - az) * nz;
                if (t <= 0 || t >= 1 || d < -0.05 || d > reach) return;
                double e = 0.05 / len;
                ts.Add(t);
                if (t - e > 0) ts.Add(t - e);
                if (t + e < 1) ts.Add(t + e);
            }
        }

        /// <summary>The band's width at t along segment i: the width wr, or to a wall or other obstacle within wr + fill; none where it would start inside a building.</summary>
        double WidthAt(Drawn r, int i, double ha, double hb, int side, double wr, double fill, double t, List<Pad> pads)
        {
            InnerAt(r, i, ha, hb, t, out double px, out double pz, out double nx, out double nz, out double scale, side);
            if (InBuilding(px + nx * 0.05, pz + nz * 0.05)) return 0;
            double reach = (wr + fill) * scale;
            double g = Math.Min(WallGap(px, pz, nx, nz, reach) - WallClear, ObstacleGap(r, i, side, px, pz, nx, nz, reach, pads)) / scale;
            return g < wr + fill - WallClear ? g : wr;
        }

        // The end of the last piece, held back until the next segment shows whether a piece carries on
        // from it: its cross-section (in Mine().PendP and PendH), direction and width.
        bool walkPending;
        int pendingN;
        double pendingTx, pendingTz, pendingWidth;
        uint pendingFace;

        void FlushEnd()
        {
            if (walkPending) End(Mine().PendP, Mine().PendH, pendingN, pendingTx, pendingTz, pendingFace);
            walkPending = false;
        }

        /// <summary>Is the end of a piece, from the inner edge at tin to the outer edge at tout, clear of carriageways, pads and earlier bands (within 5 cm of its ends)?</summary>
        bool EndClear(Drawn r, int i, double ha, double hb, int side, double tin, double tout, List<Pad> pads)
        {
            Edge(r, i, ha, hb, side, tin, tout, out double ix, out double iz, out double ox, out double oz);
            double l = Math.Sqrt((ox - ix) * (ox - ix) + (oz - iz) * (oz - iz));
            if (l < 0.1) return true;
            var part = Mine().FreePart;
            Free(r, i, side, ix, iz, ox, oz, pads, part, false);
            return part.Count == 1 && part[0].a <= 0.05 / l && part[0].b >= 1 - 0.05 / l;
        }

        /// <summary>The point of the carriageway edge at t along segment i (offset h at the ends), with the direction its band's width is set out along there.</summary>
        static void InnerAt(Drawn r, int i, double ha, double hb, double t, out double px, out double pz, out double nx, out double nz, out double scale, int side)
        {
            double cx = r.Pts[2 * i], cz = r.Pts[2 * i + 1], ex = r.Pts[2 * i + 2], ez = r.Pts[2 * i + 3];
            double ax = cx + r.Sx[i] * ha, az = cz + r.Sz[i] * ha, bx = ex + r.Ex[i] * hb, bz = ez + r.Ez[i] * hb;
            px = ax + (bx - ax) * t;
            pz = az + (bz - az) * t;
            // The way Edge sets the outer edge out: the mitre direction, interpolated. Its length
            // (scale) is the distance along it per metre of width straight out from the segment.
            double ux = (r.Sx[i] + (r.Ex[i] - r.Sx[i]) * t) * side, uz = (r.Sz[i] + (r.Ez[i] - r.Sz[i]) * t) * side;
            scale = Math.Sqrt(ux * ux + uz * uz);
            nx = ux / scale;
            nz = uz / scale;
        }

        /// <summary>
        /// Distance along (nx, nz) from (px, pz) to the nearest carriageway, pad or earlier band at
        /// the road's level, up to max. One the ray starts inside is left to the cuts.
        /// </summary>
        double ObstacleGap(Drawn r, int i, int side, double px, double pz, double nx, double nz, double max, List<Pad> pads)
        {
            var mine = Mine();
            // From just inside the carriageway, so a band or carriageway touching its edge counts.
            const double back = 0.05;
            px -= nx * back;
            pz -= nz * back;
            max += back;
            double best = max, dx = nx * max, dz = nz * max;
            double minX = Math.Min(px, px + dx), maxX = Math.Max(px, px + dx), minZ = Math.Min(pz, pz + dz), maxZ = Math.Max(pz, pz + dz);
            foreach (int q in SegmentsNear(minX, maxX, minZ, maxZ))
            {
                if (RingFor(r, i, q, out int start, out int n) && ClipRing(start, n, px, pz, dx, dz, out double c0, out _) && c0 > 1e-9) best = Math.Min(best, c0 * max);
            }
            var quads = mine.WalkQuads;
            var seen = mine.WalkSeen;
            seen.Clear();
            for (int cx = Cell(minX); cx <= Cell(maxX); cx++)
                for (int cz = Cell(minZ); cz <= Cell(maxZ); cz++)
                {
                    var list = mine.WalkCellLists[cx + cz * WallCells];
                    if (list == null) continue;
                    foreach (int q in list)
                    {
                        var w = quads[q];
                        if (!SameLevel(w.Y, r.Y) || w.MaxX < minX || w.MinX > maxX || w.MaxZ < minZ || w.MinZ > maxZ || !seen.Add(q)) continue;
                        if (w.Road == r && w.Side == side && OwnNear(r, i, w.Seg)) continue;
                        double c0 = 0, c1 = 1;
                        if (Half(px, pz, dx, dz, w.X0, w.Z0, w.X1, w.Z1, ref c0, ref c1) && Half(px, pz, dx, dz, w.X1, w.Z1, w.X2, w.Z2, ref c0, ref c1)
                            && Half(px, pz, dx, dz, w.X2, w.Z2, w.X3, w.Z3, ref c0, ref c1) && Half(px, pz, dx, dz, w.X3, w.Z3, w.X0, w.Z0, ref c0, ref c1) && c0 > 1e-9)
                            best = Math.Min(best, c0 * max);
                    }
                }
            foreach (var pad in pads)
                if (SameLevel(pad.Y - RoadPalette.PadLift, r.Y) && ClipConvex(px, pz, dx, dz, pad.Ring, out double c0, out _) && c0 > 1e-9)
                    best = Math.Min(best, c0 * max);
            return Math.Max(0, best - back);
        }

        /// <summary>Stretches of the line (t in [0, 1]) inside the chunk and clear of every carriageway, pad and earlier band at the road's level.</summary>
        void Free(Drawn r, int i, int side, double ax, double az, double bx, double bz, List<Pad> pads, List<(double a, double b)> spans, bool inChunk = true)
        {
            spans.Clear();
            double t0 = 0, t1 = 1, dx = bx - ax, dz = bz - az;
            if (inChunk && (!Slab(ax, dx, 0, Size, ref t0, ref t1) || !Slab(az, dz, 0, Size, ref t0, ref t1))) return;
            spans.Add((t0, t1));
            var mine = Mine();
            var next = Emptied(mine.Spans2);
            double minX = Math.Min(ax, bx), maxX = Math.Max(ax, bx), minZ = Math.Min(az, bz), maxZ = Math.Max(az, bz);
            foreach (int q in SegmentsNear(minX, maxX, minZ, maxZ))
            {
                if (spans.Count == 0) return;
                if (RingFor(r, i, q, out int start, out int n) && ClipRing(start, n, ax, az, dx, dz, out double c0, out double c1)) Cut(spans, next, c0, c1);
            }
            foreach (var pad in pads)
            {
                if (spans.Count == 0) return;
                if (SameLevel(pad.Y - RoadPalette.PadLift, r.Y) && ClipConvex(ax, az, dx, dz, pad.Ring, out double c0, out double c1)) Cut(spans, next, c0, c1);
            }
            var quads = mine.WalkQuads;
            int x0 = Cell(minX), x1 = Cell(maxX), z0 = Cell(minZ), z1 = Cell(maxZ);
            var seen = mine.WalkSeen;
            seen.Clear();
            for (int cx = x0; cx <= x1; cx++)
                for (int cz = z0; cz <= z1; cz++)
                {
                    var list = mine.WalkCellLists[cx + cz * WallCells];
                    if (list == null) continue;
                    foreach (int q in list)
                    {
                        if (spans.Count == 0) return;
                        var w = quads[q];
                        if (!SameLevel(w.Y, r.Y) || w.MaxX < minX || w.MinX > maxX || w.MaxZ < minZ || w.MinZ > maxZ || !seen.Add(q)) continue;
                        double c0 = 0, c1 = 1;
                        if (Half(ax, az, dx, dz, w.X0, w.Z0, w.X1, w.Z1, ref c0, ref c1) && Half(ax, az, dx, dz, w.X1, w.Z1, w.X2, w.Z2, ref c0, ref c1)
                            && Half(ax, az, dx, dz, w.X2, w.Z2, w.X3, w.Z3, ref c0, ref c1) && Half(ax, az, dx, dz, w.X3, w.Z3, w.X0, w.Z0, ref c0, ref c1))
                            Cut(spans, next, c0, c1);
                    }
                }
        }

        /// <summary>
        /// Segment j of road r is part of the same strip as segment i: a neighbour, or one of the
        /// short pieces of a fillet, nearer along the road than its width. Its band does not cut
        /// or narrow segment i's band, and its carriageway does only as drawn (its exact ring, see
        /// SegItem), as the two meet on the mitre.
        /// </summary>
        static bool OwnNear(Drawn r, int i, int j)
        {
            if (Math.Abs(j - i) <= 1) return true;
            double gap = j > i ? r.Arc[j] - r.Arc[i + 1] : r.Arc[i] - r.Arc[j + 1];
            return gap < 2 * Math.Max(r.Halves[i], r.Halves[j]) + 1;
        }

        /// <summary>
        /// One piece of band on segment i: the inner edge from ai to bi, the outer from ao to bo (t
        /// along the segment), cut into quads at the width samples, the ramp breaks and the ends of
        /// stops and broken slabs between a0 and b0. Each quad's cross-section is one strip of
        /// paving or, for a drain, two lips and a channel (see Section). Faces: the tops, the kerb,
        /// the outer face down to the ground, steps where the top's height changes, and the ends.
        /// </summary>
        void Piece(Drawn r, int i, int side, double ha, double hb, double ai, double ao, double bi, double bo, double a0, double b0,
            bool startFace, bool endFace, SegAttr attr, List<Pad> pads, bool drain)
        {
            var mine = Mine();
            var breaks = mine.Breaks;
            breaks.Clear();
            var widths = mine.Widths;
            var ts = mine.WidthTs;
            int m = widths.Count - 1;
            double len = r.Arc[i + 1] - r.Arc[i];
            breaks.Add(a0);
            // At the regular samples (so a quad left out by Sound is never long), and at the others
            // where the width changes; between samples of one width the outer edge is straight.
            int regular = Math.Max(1, (int)Math.Ceiling(len / WalkSample));
            for (int k = 1; k < m; k++)
                if (ts[k] > a0 && ts[k] < b0 && (widths[k] != widths[k - 1] || widths[k] != widths[k + 1] || Math.Abs(ts[k] * regular - Math.Round(ts[k] * regular)) < 1e-9))
                    breaks.Add(ts[k]);
            // Where the inner and outer edges pass the ends of each nearby crossing's ramps.
            Edge(r, i, ha, hb, side, 0, 0, out double i0x, out double i0z, out double o0x, out double o0z);
            Edge(r, i, ha, hb, side, 1, 1, out double i1x, out double i1z, out double o1x, out double o1z);
            foreach (var z in mine.Crossed)
            {
                if (!SameLevel(z.y, r.Y)) continue;
                for (int line = 0; line < 2; line++)
                {
                    double px = line == 0 ? i0x : o0x, pz = line == 0 ? i0z : o0z, qx = line == 0 ? i1x : o1x, qz = line == 0 ? i1z : o1z;
                    double along0 = (px - z.x) * z.tx + (pz - z.z) * z.tz, slope = (qx - px) * z.tx + (qz - pz) * z.tz;
                    if (Math.Abs(slope) < 1e-9) continue;
                    for (int e = 0; e < 4; e++)
                    {
                        double t = ((e < 2 ? -1 : 1) * (ZebraDepth / 2 + (e == 0 || e == 3 ? RampLength : 0)) - along0) / slope;
                        if (t > a0 && t < b0) breaks.Add(t);
                    }
                }
            }
            // The ends of the stops, and of the broken slabs: per SlabSlot metres along the road, by
            // a hash of the way, side and slot.
            var stops = mine.StopSpans;
            var slabs = Emptied(mine.SlabSpans);
            foreach (var (sa, sb) in stops) { Within(sa); Within(sb); }
            if (!drain)
                for (long slot = (long)Math.Floor((r.Arc[i] + a0 * len) / SlabSlot); slot * SlabSlot < r.Arc[i] + b0 * len; slot++)
                {
                    long salt = 2 * slot + (side > 0 ? 1 : 0);
                    if (Hash(attr.Way, 7000 + salt) >= SlabChance) continue;
                    double from = slot * SlabSlot + 1 + Hash(attr.Way, 9000 + salt) * (SlabSlot - 2 - SlabLength);
                    double ta = (from - r.Arc[i]) / len, tb = (from + SlabLength - r.Arc[i]) / len;
                    if (tb <= a0 || ta >= b0) continue;
                    slabs.Add((ta, tb));
                    Within(ta);
                    Within(tb);
                }
            breaks.Add(b0);
            breaks.Sort();
            // Breaks within 1e-6 of each other are one (a quad between them would be left out, and
            // with it the end face); the last is b0.
            int kept = 1;
            for (int k = 1; k < breaks.Count; k++)
                if (breaks[k] - breaks[kept - 1] > 1e-6) breaks[kept++] = breaks[k];
            breaks.RemoveRange(kept, breaks.Count - kept);
            breaks[kept - 1] = b0;
            uint face = Pack(drain ? DrainStone : KerbStone, WorldMaterial.Concrete), paint = Pack(StopKerb, WorldMaterial.Concrete), wall = Pack(DrainWall, WorldMaterial.Concrete);
            double tx = (r.Pts[2 * i + 2] - r.Pts[2 * i]) / len, tz = (r.Pts[2 * i + 3] - r.Pts[2 * i + 1]) / len;
            int n = drain ? 3 : 1;
            Span<double> p0 = stackalloc double[8], h0 = stackalloc double[6], p1 = stackalloc double[8], h1 = stackalloc double[6];
            // The last quad drawn, while the one after it is still to come: its end section, and the
            // indices of its end vertices (tops: inner and outer of each strip; faces: kerb, outer,
            // channel sides), which the next quad shares where it carries on unchanged.
            Span<double> lp = stackalloc double[8], lh = stackalloc double[6];
            Span<int> lv = stackalloc int[6], fv = stackalloc int[4];
            bool drawn = false;
            var last = WalkKind.Paving;
            for (int k = 0; k + 1 < breaks.Count; k++)
            {
                double t0 = breaks[k], t1 = breaks[k + 1];
                double tin0 = k == 0 ? ai : t0, tout0 = k == 0 ? ao : t0, tin1 = k + 2 == breaks.Count ? bi : t1, tout1 = k + 2 == breaks.Count ? bo : t1;
                Edge(r, i, ha, hb, side, tin0, tout0, out double ix0, out double iz0, out double ox0, out double oz0);
                Edge(r, i, ha, hb, side, tin1, tout1, out double ix1, out double iz1, out double ox1, out double oz1);
                double yi0 = r.Y + WalkHeight(ix0, iz0, r.Y), yo0 = r.Y + WalkHeight(ox0, oz0, r.Y);
                double yi1 = r.Y + WalkHeight(ix1, iz1, r.Y), yo1 = r.Y + WalkHeight(ox1, oz1, r.Y);
                // A quad that is twisted or would overlap a carriageway, pad or band is halved, and
                // what is still unsound at SplitLength is left out; the quads either side of it get
                // end faces.
                if (!Sound(r, i, side, ix0, iz0, ox0, oz0, ox1, oz1, ix1, iz1, pads))
                {
                    if ((t1 - t0) * len > SplitLength)
                    {
                        breaks.Insert(k + 1, (t0 + t1) / 2);
                        k--;
                        continue;
                    }
                    Dropped++;
                    if (drawn) End(lp, lh, n, tx, tz, face);
                    else if (k == 0 && !startFace) FlushEnd();
                    drawn = false;
                    continue;
                }
                // A drain is covered in line with a zebra; a slab is broken off the ramps and stops.
                double tm = (t0 + t1) / 2;
                WalkKind kind;
                if (drain) kind = WalkHeight((ix0 + ox0 + ix1 + ox1) / 4, (iz0 + oz0 + iz1 + oz1) / 4, r.Y) < KerbHeight - 1e-9 ? WalkKind.Covered : WalkKind.Drain;
                else if (In(stops, tm)) kind = WalkKind.Stop;
                else if (In(slabs, tm) && Math.Min(Math.Min(yi0, yo0), Math.Min(yi1, yo1)) >= r.Y + KerbHeight - 1e-9) kind = WalkKind.Broken;
                else kind = WalkKind.Paving;
                Section(kind, ix0, iz0, ox0, oz0, yi0, yo0, r.Y, p0, h0);
                Section(kind, ix1, iz1, ox1, oz1, yi1, yo1, r.Y, p1, h1);
                bool share = false;
                if (k == 0 && !startFace)
                {
                    // Carries on from the last segment's piece.
                    if (walkPending) Steps(p0, mine.PendH, h0, n, tx, tz, face);
                    walkPending = false;
                }
                else if (drawn) share = !Steps(p0, lh, h0, n, tx, tz, face) && kind == last;
                for (int s = 0; s < n; s++)
                {
                    uint top = Top(kind, s, attr.Way);
                    int f0 = lv[2 * s], f1 = lv[2 * s + 1];
                    if (!share)
                    {
                        f0 = col.Count;
                        f1 = f0 + 1;
                        Vertex(p0[2 * s], h0[2 * s], p0[2 * s + 1], top);
                        Vertex(p0[2 * s + 2], h0[2 * s + 1], p0[2 * s + 3], top);
                    }
                    int e = col.Count;
                    Vertex(p1[2 * s], h1[2 * s], p1[2 * s + 1], top);
                    Vertex(p1[2 * s + 2], h1[2 * s + 1], p1[2 * s + 3], top);
                    TriUp(f0, f1, e);
                    TriUp(f1, e + 1, e);
                    lv[2 * s] = e;
                    lv[2 * s + 1] = e + 1;
                }
                // Kerb towards the road, outer face away from it, and a channel's sides.
                double fx = -tz * side, fz = tx * side;
                fv[0] = FaceOn(share ? fv[0] : -1, p0[0], p0[1], p1[0], p1[1], r.Y, h0[0], r.Y, h1[0], -fx, -fz, kind == WalkKind.Stop ? paint : face);
                fv[1] = FaceOn(share ? fv[1] : -1, p0[2 * n], p0[2 * n + 1], p1[2 * n], p1[2 * n + 1], 0, h0[2 * n - 1], 0, h1[2 * n - 1], fx, fz, face);
                for (int s = 1; s < n; s++)
                {
                    double l0 = h0[2 * s - 1], r0 = h0[2 * s], l1 = h1[2 * s - 1], r1 = h1[2 * s];
                    fv[1 + s] = FaceOn(share ? fv[1 + s] : -1, p0[2 * s], p0[2 * s + 1], p1[2 * s], p1[2 * s + 1], Math.Min(l0, r0), Math.Max(l0, r0), Math.Min(l1, r1), Math.Max(l1, r1),
                        r0 < l0 ? fx : -fx, r0 < l0 ? fz : -fz, wall);
                }
                if (k == 0 ? startFace : !drawn) End(p0, h0, n, -tx, -tz, face);
                if (k + 2 == breaks.Count)
                {
                    if (endFace) End(p1, h1, n, tx, tz, face);
                    else
                    {
                        walkPending = true;
                        p1.CopyTo(mine.PendP);
                        h1.CopyTo(mine.PendH);
                        (pendingN, pendingTx, pendingTz, pendingWidth, pendingFace) = (n, tx, tz, widths[m], face);
                    }
                }
                AddWalkQuad(ix0, iz0, ox0, oz0, ox1, oz1, ix1, iz1, r, side, i);
                if (kind == WalkKind.Stop && !r.Bridge) mine.StopQuads.Add((ix0, iz0, ix1, iz1, ox1, oz1, ox0, oz0)); // for the flyover supports
                Walks?.Add(new WalkPiece { Way = attr.Way, Side = side, Kind = kind, InAx = ix0, InAz = iz0, InBx = ix1, InBz = iz1, OutAx = ox0, OutAz = oz0, OutBx = ox1, OutBz = oz1, Ya = h0[0], Yb = h1[0], RoadY = r.Y });
                if (kind == WalkKind.Broken && (!drawn || last != WalkKind.Broken)) BrokenSlabs++;
                drawn = true;
                last = kind;
                p1.CopyTo(lp);
                h1.CopyTo(lh);
            }

            void Within(double t)
            {
                if (t > a0 && t < b0) breaks.Add(t);
            }
        }

        static bool In(List<(double a, double b)> spans, double t)
        {
            foreach (var (a, b) in spans) if (t > a && t < b) return true;
            return false;
        }

        /// <summary>
        /// A band's cross-section at one end of a quad, inner edge (ix, iz) to outer (ox, oz): its
        /// points across, inner to outer, in p (x, z pairs), and each strip's top at its inner and
        /// outer side in h. Paving is one strip, its heights yi and yo (a broken slab SlabSink
        /// lower); a drain is three, lip, channel and lip, over the road at y (covered: one level).
        /// Returns the number of strips.
        /// </summary>
        static int Section(WalkKind kind, double ix, double iz, double ox, double oz, double yi, double yo, double y, Span<double> p, Span<double> h)
        {
            p[0] = ix;
            p[1] = iz;
            if (kind != WalkKind.Drain && kind != WalkKind.Covered)
            {
                double sink = kind == WalkKind.Broken ? SlabSink : 0;
                p[2] = ox;
                p[3] = oz;
                h[0] = yi - sink;
                h[1] = yo - sink;
                return 1;
            }
            double w = Math.Sqrt((ox - ix) * (ox - ix) + (oz - iz) * (oz - iz)), f = w > 1e-9 ? Math.Min(DrainLip / w, 0.3) : 0.3;
            p[2] = ix + (ox - ix) * f;
            p[3] = iz + (oz - iz) * f;
            p[4] = ox - (ox - ix) * f;
            p[5] = oz - (oz - iz) * f;
            p[6] = ox;
            p[7] = oz;
            bool open = kind == WalkKind.Drain;
            h[0] = h[1] = h[4] = h[5] = y + (open ? DrainLipHeight : DrainCover);
            h[2] = h[3] = y + (open ? -DrainDepth : DrainCover);
            return 3;
        }

        static uint Top(WalkKind kind, int strip, long way) => kind switch
        {
            WalkKind.Broken => Pack(BrokenSlab, WorldMaterial.Paving),
            WalkKind.Stop => Pack(StopPad, WorldMaterial.Paving),
            WalkKind.Drain => strip == 1 ? Pack(Sludge, WorldMaterial.Water) : Pack(DrainStone, WorldMaterial.Concrete),
            WalkKind.Covered => Pack(DrainStone, WorldMaterial.Concrete),
            _ => Pack(Tint(way), WorldMaterial.Paving),
        };

        /// <summary>Steps at section p where the top changes from one quad to the next: each strip's face from the lower top to the higher, facing the lower quad. True if there are any.</summary>
        bool Steps(Span<double> p, Span<double> before, Span<double> after, int n, double tx, double tz, uint colour)
        {
            bool any = false;
            for (int s = 0; s < n; s++)
            {
                double bl = before[2 * s], br = before[2 * s + 1], al = after[2 * s], ar = after[2 * s + 1];
                if (Math.Abs(bl - al) < 1e-4 && Math.Abs(br - ar) < 1e-4) continue;
                bool down = al + ar < bl + br;
                Wall(p[2 * s], p[2 * s + 1], p[2 * s + 2], p[2 * s + 3], Math.Min(bl, al), Math.Max(bl, al), Math.Min(br, ar), Math.Max(br, ar), down ? tx : -tx, down ? tz : -tz, colour);
                any = true;
            }
            return any;
        }

        /// <summary>A band's end at section p, facing (dx, dz): each strip's face down to the ground; an open channel is walled up to its lips, inside as well.</summary>
        void End(Span<double> p, Span<double> h, int n, double dx, double dz, uint colour)
        {
            for (int s = 0; s < n; s++)
            {
                if (n == 3 && s == 1 && h[2] < h[1] - 1e-4)
                {
                    Wall(p[2], p[3], p[4], p[5], 0, h[1], 0, h[4], dx, dz, colour);
                    Wall(p[2], p[3], p[4], p[5], h[2], h[1], h[3], h[4], -dx, -dz, colour);
                }
                else Wall(p[2 * s], p[2 * s + 1], p[2 * s + 2], p[2 * s + 3], 0, h[2 * s], 0, h[2 * s + 1], dx, dz, colour);
            }
        }

        /// <summary>How far two shapes may overlap and still count as touching.</summary>
        const double TouchTolerance = 0.01;

        /// <summary>
        /// Is the top quad (inner a, outer a, outer b, inner b) untwisted, and clear of every other
        /// carriageway, pad and earlier band at the road's level (beyond TouchTolerance)?
        /// </summary>
        bool Sound(Drawn r, int i, int side, double x0, double z0, double x1, double z1, double x2, double z2, double x3, double z3, List<Pad> pads)
        {
            Span<double> q = stackalloc double[8];
            q[0] = x0; q[1] = z0; q[2] = x1; q[3] = z1; q[4] = x2; q[5] = z2; q[6] = x3; q[7] = z3;
            // Untwisted: every turn the same way (or none, where two corners meet).
            int turns = 0;
            for (int k = 0; k < 4; k++)
            {
                int a = 2 * k, b = 2 * ((k + 1) % 4), c = 2 * ((k + 2) % 4);
                double cr = (q[b] - q[a]) * (q[c + 1] - q[b + 1]) - (q[b + 1] - q[a + 1]) * (q[c] - q[b]);
                if (cr > 1e-9) turns |= 1;
                else if (cr < -1e-9) turns |= 2;
            }
            if (turns == 3) return false;
            double minX = Math.Min(Math.Min(x0, x1), Math.Min(x2, x3)), maxX = Math.Max(Math.Max(x0, x1), Math.Max(x2, x3));
            double minZ = Math.Min(Math.Min(z0, z1), Math.Min(z2, z3)), maxZ = Math.Max(Math.Max(z0, z1), Math.Max(z2, z3));
            var mine = Mine();
            var seen = mine.WalkSeen;
            seen.Clear();
            Span<double> o = stackalloc double[8], ring = stackalloc double[64];
            for (int cx = Cell(minX); cx <= Cell(maxX); cx++)
                for (int cz = Cell(minZ); cz <= Cell(maxZ); cz++)
                {
                    var list = mine.WalkCellLists[cx + cz * WallCells];
                    if (list == null) continue;
                    foreach (int n in list)
                    {
                        var w = mine.WalkQuads[n];
                        if (!SameLevel(w.Y, r.Y) || w.MaxX < minX || w.MinX > maxX || w.MaxZ < minZ || w.MinZ > maxZ || !seen.Add(n)) continue;
                        o[0] = w.X0; o[1] = w.Z0; o[2] = w.X1; o[3] = w.Z1; o[4] = w.X2; o[5] = w.Z2; o[6] = w.X3; o[7] = w.Z3;
                        if (Overlaps(q, o)) return false;
                    }
                }
            foreach (int k in SegmentsNear(minX, maxX, minZ, maxZ))
            {
                if (!RingFor(r, i, k, out int start, out int n)) continue;
                for (int c = 0; c < 2 * n; c++) ring[c] = mine.SegRings[start + c];
                if (Overlaps(q, ring.Slice(0, 2 * n))) return false;
            }
            foreach (var pad in pads)
            {
                if (!SameLevel(pad.Y - RoadPalette.PadLift, r.Y) || pad.Ring.Count > 32) continue;
                for (int k = 0; k < pad.Ring.Count; k++) { ring[2 * k] = pad.Ring[k].x; ring[2 * k + 1] = pad.Ring[k].z; }
                if (Overlaps(q, ring.Slice(0, 2 * pad.Ring.Count))) return false;
            }
            return true;
        }

        /// <summary>Segment j of road o as a rectangle, OverlapExtend longer at each end (BorderExtend at an end on the chunk border, where the carriageway runs on to the border line past its square end); false when it is clear of the box.</summary>
        static bool SegmentRect(Drawn o, int j, Span<double> rect, double minX, double maxX, double minZ, double maxZ)
        {
            double len = o.Arc[j + 1] - o.Arc[j];
            double px = o.Pts[2 * j], pz = o.Pts[2 * j + 1], qx = o.Pts[2 * j + 2], qz = o.Pts[2 * j + 3];
            double back = j == 0 && o.StartOn ? BorderExtend : OverlapExtend, ahead = j == o.N - 2 && o.EndOn ? BorderExtend : OverlapExtend;
            double reach = Math.Max(o.Halves[j], o.Halves[j + 1]) + Math.Max(back, ahead);
            if (Math.Max(px, qx) + reach < minX || Math.Min(px, qx) - reach > maxX || Math.Max(pz, qz) + reach < minZ || Math.Min(pz, qz) - reach > maxZ) return false;
            double ux = (qx - px) / len, uz = (qz - pz) / len, nx = -uz, nz = ux, ha = o.Halves[j], hb = o.Halves[j + 1];
            px -= ux * back;
            pz -= uz * back;
            qx += ux * ahead;
            qz += uz * ahead;
            rect[0] = px - nx * ha; rect[1] = pz - nz * ha;
            rect[2] = qx - nx * hb; rect[3] = qz - nz * hb;
            rect[4] = qx + nx * hb; rect[5] = qz + nz * hb;
            rect[6] = px + nx * ha; rect[7] = pz + nz * ha;
            return true;
        }

        /// <summary>Do two convex polygons (x, z pairs) overlap by more than TouchTolerance on every separating axis?</summary>
        static bool Overlaps(Span<double> a, Span<double> b)
        {
            return Axes(a, a, b) && Axes(b, a, b);

            static bool Axes(Span<double> edges, Span<double> a, Span<double> b)
            {
                int n = edges.Length / 2;
                for (int k = 0; k < n; k++)
                {
                    int j = (k + 1) % n;
                    double ex = edges[2 * j] - edges[2 * k], ez = edges[2 * j + 1] - edges[2 * k + 1], l = Math.Sqrt(ex * ex + ez * ez);
                    if (l < 1e-9) continue;
                    double nx = -ez / l, nz = ex / l;
                    Project(a, nx, nz, out double a0, out double a1);
                    Project(b, nx, nz, out double b0, out double b1);
                    if (Math.Min(a1, b1) - Math.Max(a0, b0) < TouchTolerance) return false;
                }
                return true;
            }

            static void Project(Span<double> p, double nx, double nz, out double lo, out double hi)
            {
                lo = double.MaxValue;
                hi = double.MinValue;
                for (int k = 0; k < p.Length; k += 2)
                {
                    double d = p[k] * nx + p[k + 1] * nz;
                    lo = Math.Min(lo, d);
                    hi = Math.Max(hi, d);
                }
            }
        }

        /// <summary>The band's inner point at tin and outer point at tout along segment i (the width interpolated between samples).</summary>
        void Edge(Drawn r, int i, double ha, double hb, int side, double tin, double tout, out double ix, out double iz, out double ox, out double oz, double dw = 0)
        {
            var widths = Mine().Widths;
            var ts = Mine().WidthTs;
            int m = widths.Count - 1;
            double cx = r.Pts[2 * i], cz = r.Pts[2 * i + 1], ex = r.Pts[2 * i + 2], ez = r.Pts[2 * i + 3];
            double ax = cx + r.Sx[i] * ha, az = cz + r.Sz[i] * ha, bx = ex + r.Ex[i] * hb, bz = ez + r.Ez[i] * hb;
            ix = ax + (bx - ax) * tin;
            iz = az + (bz - az) * tin;
            double px = ax + (bx - ax) * tout, pz = az + (bz - az) * tout;
            double tc = Math.Clamp(tout, 0, 1);
            int k = 0;
            while (k + 1 < m && ts[k + 1] <= tc) k++;
            double w = (m == 0 ? widths[0] : widths[k] + (widths[k + 1] - widths[k]) * (tc - ts[k]) / (ts[k + 1] - ts[k])) + dw;
            double ux = (r.Sx[i] + (r.Ex[i] - r.Sx[i]) * tout) * side, uz = (r.Sz[i] + (r.Ez[i] - r.Sz[i]) * tout) * side;
            ox = px + ux * w;
            oz = pz + uz * w;
        }

        /// <summary>
        /// Height of a sidewalk above the road y at (x, z): the kerb, or less in line with a zebra,
        /// for any sidewalk within reach of the zebra's road (its own, a corner's, a median's).
        /// </summary>
        double WalkHeight(double x, double z, double y)
        {
            double h = KerbHeight;
            foreach (var c in Mine().Crossed)
            {
                if (!SameLevel(c.y, y)) continue;
                double dx = x - c.x, dz = z - c.z;
                if (Math.Abs(dz * c.tx - dx * c.tz) > c.half + RampReach) continue;
                double d = Math.Abs(dx * c.tx + dz * c.tz) - ZebraDepth / 2;
                if (d <= 0) return RampLift;
                if (d < RampLength) h = Math.Min(h, RampLift + (KerbHeight - RampLift) * d / RampLength);
            }
            return h;
        }

        /// <summary>
        /// A vertical face from (ax, az) to (bx, bz), bottom ya0/yb0 to top ya1/yb1, facing (nx, nz),
        /// carrying on from the face whose end vertices (bottom, top) start at index from, or new
        /// when from is -1. Returns the index of its own end vertices, or -1 when it has no height.
        /// </summary>
        int FaceOn(int from, double ax, double az, double bx, double bz, double ya0, double ya1, double yb0, double yb1, double nx, double nz, uint colour)
        {
            if (ya1 - ya0 < 1e-4 && yb1 - yb0 < 1e-4) return -1;
            double dx = bx - ax, dz = bz - az;
            if (dx * dx + dz * dz < 1e-8) return from;
            int f = from;
            if (f < 0)
            {
                f = col.Count;
                Vertex(ax, ya0, az, colour);
                Vertex(ax, ya1, az, colour);
            }
            int g = col.Count;
            Vertex(bx, yb0, bz, colour);
            Vertex(bx, yb1, bz, colour);
            // As in Wall.
            if (dz * nx - dx * nz >= 0)
            {
                idx.Add(f); idx.Add(f + 1); idx.Add(g);
                idx.Add(f + 1); idx.Add(g + 1); idx.Add(g);
            }
            else
            {
                idx.Add(f); idx.Add(g); idx.Add(f + 1);
                idx.Add(f + 1); idx.Add(g); idx.Add(g + 1);
            }
            return g;
        }

        /// <summary>A single-sided vertical quad whose front faces (nx, nz).</summary>
        void Wall(double ax, double az, double bx, double bz, double ya0, double ya1, double yb0, double yb1, double nx, double nz, uint colour)
        {
            double dx = bx - ax, dz = bz - az;
            if (dx * dx + dz * dz < 1e-8) return;
            int f = col.Count;
            Vertex(ax, ya0, az, colour);
            Vertex(ax, ya1, az, colour);
            Vertex(bx, yb0, bz, colour);
            Vertex(bx, yb1, bz, colour);
            // (a0, a1, b0) faces (dz, -dx): Unity's front face for that winding.
            if (dz * nx - dx * nz >= 0)
            {
                idx.Add(f); idx.Add(f + 1); idx.Add(f + 2);
                idx.Add(f + 1); idx.Add(f + 3); idx.Add(f + 2);
            }
            else
            {
                idx.Add(f); idx.Add(f + 2); idx.Add(f + 1);
                idx.Add(f + 1); idx.Add(f + 2); idx.Add(f + 3);
            }
        }

        void AddWalkQuad(double x0, double z0, double x1, double z1, double x2, double z2, double x3, double z3, Drawn r, int side, int seg)
        {
            // Counter-clockwise for ClipConvex's half planes.
            double area = (x1 - x0) * (z2 - z0) - (z1 - z0) * (x2 - x0) + (x2 - x0) * (z3 - z0) - (z2 - z0) * (x3 - x0);
            if (area < 0) { (x1, z1, x3, z3) = (x3, z3, x1, z1); }
            var q = new WalkQuad
            {
                X0 = x0, Z0 = z0, X1 = x1, Z1 = z1, X2 = x2, Z2 = z2, X3 = x3, Z3 = z3, Y = r.Y, Road = r, Side = side, Seg = seg,
                MinX = Math.Min(Math.Min(x0, x1), Math.Min(x2, x3)), MaxX = Math.Max(Math.Max(x0, x1), Math.Max(x2, x3)),
                MinZ = Math.Min(Math.Min(z0, z1), Math.Min(z2, z3)), MaxZ = Math.Max(Math.Max(z0, z1), Math.Max(z2, z3)),
            };
            var mine = Mine();
            int n = mine.WalkQuads.Count;
            mine.WalkQuads.Add(q);
            for (int cx = Cell(q.MinX); cx <= Cell(q.MaxX); cx++)
                for (int cz = Cell(q.MinZ); cz <= Cell(q.MaxZ); cz++)
                {
                    int k = cx + cz * WallCells;
                    var list = mine.WalkCellLists[k] ??= new List<int>(8);
                    if (list.Count == 0) mine.WalkTouched.Add(k);
                    list.Add(n);
                }
        }

        static uint Tint(long way)
        {
            double h = Hash(way, 700);
            return h < 0.5 ? WalkSlab : h < 0.8 ? WalkGrey : WalkBrick;
        }
    }
}
