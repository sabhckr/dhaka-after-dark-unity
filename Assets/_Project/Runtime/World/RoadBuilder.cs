// Road surfaces for one chunk, built off the main thread: carriageways from centrelines,
// junction pads, and barriers and soffits on vehicle bridges.
//
// Ported from the browser build (dhaka-after-dark src/viewer/roads.ts and meshes.ts):
//   - half width = max(0.4 m, width / 2); the width is the exporter's (measured, or the labelled
//     class rule when WidthEstimated), except that an estimated one-way road with a lanes tag takes
//     lanes x 3.5 m (RoadWidths, K-37); y is the exporter's (0.28 m at grade, 6 / 11 / 16 m bridges);
//   - footway, path, steps, pedestrian and cycleway bridges 2.5 m up are footbridges, not roads;
//   - way ends that meet end to end at the same level and similar width are chained;
//   - T-junction stems are pulled back to the through road's edge;
//   - junction pads (convex hull of the arms' edges) where roads meet or cross at an angle;
//   - gentle bends get short fillets; graph nodes and ends stay put;
//   - mitred outlines, bevelled past 2.2 x the half width;
//   - bridge barriers 0.9 m high and a soffit 0.42 m below the deck, 0.72 x as wide, on bridges
//     2.5 m or higher. (The port's flat sidewalk strip, 0.16 m up under the road, gave way to raised
//     sidewalks with kerbs in D-05: see Sidewalks.cs.)
// Changed here:
//   - a road end on the chunk border is cut along the border (crossings within 60 degrees of
//     square) or pushed past it (flatter crossings), always 2 cm into the neighbour, so the halves
//     of a road split between two chunks meet or overlap and never leave a gap (see Cut);
//   - ends on the chunk border are never chained, and a chained road keeps each part's own half
//     width per vertex (the browser build averaged them), so both chunks end a road at one width.
// Colours are this port's own albedo for the lit world shader (the browser build uses textures):
// newer, darker asphalt on marked roads, older and dustier on the rest, concrete for decks,
// barriers and soffits. Markings and street details (D-04) and sidewalks (D-05) are added to the
// same mesh at the end of Build: see RoadMarkings.cs and Sidewalks.cs.
//
// Vertices are x, y, z floats in metres from the chunk's south-west corner; colours are sRGB with
// the material in alpha, packed as WorldMaterials.Pack. Flat faces are clockwise seen from above (normals up); soffits face
// down; barriers are double-sided.
using System;
using System.Collections.Generic;

namespace Dad.World
{
    public static class RoadPalette
    {
        // Albedo, sRGB: asphalt about 0.06-0.09 linear, concrete 0.2-0.3.
        public const uint Major = 0x4a4a48, Minor = 0x5a5853, BridgeDeck = 0x8d877c, Sidewalk = 0x8a8378,
            Barrier = 0x9a958c, Soffit = 0x6a665f;

        public const double MinHalf = 0.4, PadLift = 0.012;
        public const double BarrierHeight = 0.9, SoffitDrop = 0.42, SoffitScale = 0.72, ElevatedY = 2.5;
        public const double MiterLimit = 2.2;

        static readonly HashSet<string> MajorClasses = new HashSet<string>
            { "motorway", "trunk", "primary", "secondary", "tertiary", "motorway_link", "trunk_link", "primary_link" };
        static readonly HashSet<string> FootClasses = new HashSet<string> { "footway", "path", "steps", "pedestrian", "cycleway" };

        /// <summary>The browser build's roadWantsMarks: 6.5 m or wider, or a major class.</summary>
        public static bool IsMajor(string cls, double width) => width >= 6.5 || (cls != null && MajorClasses.Contains(cls));

        public static bool IsFoot(string cls) => cls != null && FootClasses.Contains(cls);

        /// <summary>A footbridge: a foot-class way on a bridge 2.5 m or higher. Drawn later as a structure, not a road.</summary>
        public static bool IsFootSpan(Road r) =>
            r.Bridge && r.Cls != null && FootClasses.Contains(r.Cls) && r.Pts != null && r.Pts.Length >= 4 && r.YCm >= ElevatedY * 100;
    }

    public sealed partial class ChunkRoads
    {
        /// <summary>The mesh arrays. Built pooled (MeshArrays), they are longer than the mesh: use the counts.</summary>
        public float[] Positions;
        public uint[] Colors;
        public int[] Indices;
        public int VertexCount, IndexCount;
        /// <summary>Where the paint lies in Indices: flat on the road, so nothing a wheel can feel (ChunkCollision).</summary>
        public int MarkFirstIndex, MarkEndIndex;

        public int Roads, Bridges, Sidewalks, Pads, FootSpans, Chained, Trimmed, BorderCuts;
        /// <summary>Sidewalk and drain quads left out because they were twisted or would overlap (see ChunkRoads.Sound).</summary>
        public int Dropped;
        /// <summary>Segment sides with an open drain, bus stops placed on a road, and broken slabs drawn (see Sidewalks.cs).</summary>
        public int Drains, BusStops, BrokenSlabs;

        /// <summary>A carriageway end on the chunk border, metres from the chunk corner.</summary>
        public struct EndCap
        {
            public long RoadId;
            /// <summary>Centre on the border, and where the carriageway edges cross it.</summary>
            public double X, Z, Lx, Lz, Rx, Rz, Y, Half;
            /// <summary>On a ramp: Y is the road's level, not the height of the surface here.</summary>
            public bool Ramp;
            /// <summary>A flat crossing: square end pushed past the border instead of a cut.</summary>
            public bool Extended;
        }

        public readonly List<EndCap> Caps = new List<EndCap>();

        const double Size = 500, Snap = 1, BorderZone = 2.5, OnEdge = 0.005;

        /// <summary>The heights of a ramp along its line, read by position: what is drawn on a ramp, and lanes on it, take their height from here.</summary>
        public sealed class Ramp
        {
            readonly double[] pts, ys;
            public Ramp(Road r)
            {
                pts = new double[r.Pts.Length];
                for (int i = 0; i < pts.Length; i++) pts[i] = r.Pts[i] / 100.0;
                ys = new double[r.YsCm.Length];
                for (int i = 0; i < ys.Length; i++) ys[i] = r.YsCm[i] / 100.0;
            }
            /// <summary>The height of the nearest point of the line.</summary>
            public double HeightAt(double x, double z)
            {
                double best = double.MaxValue, y = ys[0];
                for (int i = 0; i + 1 < ys.Length; i++)
                {
                    double ax = pts[2 * i], az = pts[2 * i + 1], ux = pts[2 * i + 2] - ax, uz = pts[2 * i + 3] - az, l = ux * ux + uz * uz;
                    double t = l < 1e-9 ? 0 : Math.Max(0, Math.Min(1, ((x - ax) * ux + (z - az) * uz) / l));
                    double dx = ax + ux * t - x, dz = az + uz * t - z, d = dx * dx + dz * dz;
                    if (d < best) { best = d; y = ys[i] + (ys[i + 1] - ys[i]) * t; }
                }
                return y;
            }
        }

        sealed class VRoad
        {
            public long Id;
            public int Uid;
            public double Half, Y;
            public bool Bridge, Marked, Alive = true;
            public double[] Pts;
            /// <summary>Half width at each point; Half is the road's representative value (pads, stems).</summary>
            public double[] Halves;
            /// <summary>A ramp's heights (null on a level road): the road is laid out at Y, then lifted to these.</summary>
            public Ramp Ramp;
            /// <summary>Source road id at the first and last point (they differ once roads are chained).</summary>
            public long StartId, EndId;
            /// <summary>What each segment is (way, class, lanes, one-way), for the markings.</summary>
            public SegAttr[] Attrs;
            public int N => Pts.Length / 2;
        }

        sealed class Pad
        {
            public double Y;
            public bool Marked;
            public List<(double x, double z)> Ring;
        }

        // The thread's BuildScratch lists, held only while Build runs.
        List<float> p;
        List<uint> col;
        List<int> idx;
        bool pooled;

        /// <summary>Builds a chunk's roads; keepMarks keeps every marking and keep-out in Marks and KeepOuts, for tests.</summary>
        public static ChunkRoads Build(DadcChunk c, bool pooled = false, bool keepMarks = false, bool keepWalks = false)
        {
            RoadWidths.Resolve(c);
            var scratch = BuildScratch.Take();
            var mine = Mine();
            var s = new ChunkRoads { p = scratch.Positions, col = scratch.Colors, idx = scratch.Indices, pooled = pooled };
            if (keepWalks) s.Walks = new List<WalkPiece>(); // what people walk on (ChunkWalks)
            var edges = EdgesByWay(c);
            var roads = new List<VRoad>();
            if (c.Roads != null)
                foreach (var r in c.Roads)
                {
                    if (RoadPalette.IsFootSpan(r)) { s.FootSpans++; continue; }
                    if (r.Pts == null || r.Pts.Length < 4) continue;
                    var pts = new double[r.Pts.Length];
                    for (int i = 0; i < pts.Length; i++) pts[i] = r.Pts[i] / 100.0;
                    // The carriageway is the resolved width; the road's kind (sidewalks, markings) is judged by its class width.
                    double width = r.WidthCm / 100.0, kind = r.ClassWidthCm / 100.0, half = Math.Max(RoadPalette.MinHalf, width / 2);
                    var halves = new double[pts.Length / 2];
                    for (int i = 0; i < halves.Length; i++) halves[i] = half;
                    roads.Add(new VRoad
                    {
                        Id = r.Id, Uid = roads.Count, Half = half, Y = r.YCm / 100.0,
                        Bridge = r.Bridge, Marked = RoadPalette.IsMajor(r.Cls, kind), Pts = pts, Halves = halves,
                        StartId = r.Id, EndId = r.Id, Attrs = SegsFor(r, pts, kind, edges), Ramp = r.YsCm != null ? new Ramp(r) : null,
                    });
                }
            s.Chained = ChainRoads(roads);
            s.Trimmed = TrimStems(roads);
            var live = roads.FindAll(r => r.Alive && r.N >= 2);
            var pads = PadsFor(live);

            var nodes = new List<(double x, double z)>();
            if (c.Nodes != null) foreach (var n in c.Nodes) nodes.Add((n.East / 100.0, n.North / 100.0));
            var near = new NodeIndex(nodes, 1.25);

            var drawn = new List<Drawn>(live.Count);
            foreach (var r in live)
            {
                var pts = r.Pts;
                var halves = r.Halves;
                var attrs = r.Attrs;
                if (!r.Bridge)
                {
                    pts = FilletGentleBends(r.Pts, Pins(r.Pts, near), r.Halves, out halves, out var source);
                    attrs = new SegAttr[source.Length];
                    for (int i = 0; i < source.Length; i++) attrs[i] = r.Attrs[source[i]];
                }
                uint surface = r.Bridge ? Pack(RoadPalette.BridgeDeck, WorldMaterial.Concrete) : Pack(r.Marked ? RoadPalette.Major : RoadPalette.Minor, WorldMaterial.Asphalt);
                var o = MiterOutline(pts, halves, 0, 1, Reused(mine.Road));
                if (o == null) continue;
                drawn.Add(MakeDrawn(pts, halves, attrs, r.Y, r.Bridge));
                drawn[drawn.Count - 1].Ramp = r.Ramp;
                int e = o.Count - 1;
                o.Extended = false;
                if (o.CutStart = Cut(o, 0, 1))
                {
                    s.Caps.Add(new EndCap { RoadId = r.StartId, X = o.Cx[0], Z = o.Cz[0], Lx = o.BorderLx, Lz = o.BorderLz, Rx = o.BorderRx, Rz = o.BorderRz, Y = r.Y, Ramp = r.Ramp != null, Half = o.H[0], Extended = o.Extended });
                    s.BorderCuts++;
                }
                o.Extended = false;
                if (o.CutEnd = Cut(o, e, -1))
                {
                    s.Caps.Add(new EndCap { RoadId = r.EndId, X = o.Cx[e], Z = o.Cz[e], Lx = o.BorderLx, Lz = o.BorderLz, Rx = o.BorderRx, Rz = o.BorderRz, Y = r.Y, Ramp = r.Ramp != null, Half = o.H[e], Extended = o.Extended });
                    s.BorderCuts++;
                }
                int firstVertex = s.col.Count;
                s.Strip(o, r.Y, surface, false);
                if (r.Ramp != null)
                {
                    for (int v = firstVertex; v < s.col.Count; v++) s.p[3 * v + 1] = (float)r.Ramp.HeightAt(s.p[3 * v], s.p[3 * v + 2]);
                    s.RampWalls(o, r.Ramp, Pack(RoadPalette.Barrier, WorldMaterial.Concrete));
                }
                s.Roads++;
                if (r.Bridge)
                {
                    s.Bridges++;
                    if (r.Y >= RoadPalette.ElevatedY)
                    {
                        s.Walls(o, r.Y, r.Y + RoadPalette.BarrierHeight, Pack(RoadPalette.Barrier, WorldMaterial.Concrete));
                        var under = MiterOutline(pts, halves, 0, RoadPalette.SoffitScale, Reused(mine.Soffit));
                        if (under != null) s.Strip(under, r.Y - RoadPalette.SoffitDrop, Pack(RoadPalette.Soffit, WorldMaterial.Concrete), true);
                    }
                }
            }
            foreach (var pad in pads)
            {
                uint colour = Pack(pad.Marked ? RoadPalette.Major : RoadPalette.Minor, WorldMaterial.Asphalt);
                int first = s.col.Count;
                foreach (var (x, z) in pad.Ring) s.Vertex(x, pad.Y, z, colour);
                for (int i = 1; i + 1 < pad.Ring.Count; i++) s.TriUp(first, first + i, first + i + 1);
                s.Pads++;
            }
            s.MarkFirstIndex = s.idx.Count;
            s.Markings(c, drawn, pads, keepMarks);
            s.MarkEndIndex = s.idx.Count;
            s.BuildSidewalks(c, drawn, pads, keepMarks);
            s.VertexCount = s.col.Count;
            s.IndexCount = s.idx.Count;
            s.Positions = MeshArrays.Copy(s.p, pooled);
            s.Colors = MeshArrays.Copy(s.col, pooled);
            s.Indices = MeshArrays.Copy(s.idx, pooled);
            // The street furniture and structures (Milestone 2) reuse the lists, now copied out.
            s.Street = s.BuildStreet(c, drawn, pads, s.p, s.col, s.idx, pooled, keepMarks);
            s.p = null;
            s.col = null;
            s.idx = null;
            return s;
        }

        /// <summary>Hands pooled mesh arrays back once the mesh is uploaded; the arrays are gone after.</summary>
        public void Release()
        {
            if (!pooled) return;
            pooled = false;
            MeshArrays.Return(Positions);
            MeshArrays.Return(Colors);
            MeshArrays.Return(Indices);
            Positions = null;
            Colors = null;
            Indices = null;
        }

        static uint Pack(uint rgb, WorldMaterial m) => WorldMaterials.Pack(rgb, m);

        void Vertex(double x, double y, double z, uint c)
        {
            p.Add((float)x);
            p.Add((float)y);
            p.Add((float)z);
            col.Add(c);
        }

        /// <summary>Adds a triangle wound clockwise seen from above (or below, with down); degenerate ones are skipped.</summary>
        void TriUp(int a, int b, int c, bool down = false)
        {
            double cross = ((double)p[b * 3] - p[a * 3]) * ((double)p[c * 3 + 2] - p[a * 3 + 2])
                         - ((double)p[b * 3 + 2] - p[a * 3 + 2]) * ((double)p[c * 3] - p[a * 3]);
            if (Math.Abs(cross) < 1e-9) return;
            if ((cross > 0) != down) (b, c) = (c, b);
            idx.Add(a);
            idx.Add(b);
            idx.Add(c);
        }

        void Strip(Outline o, double y, uint colour, bool down)
        {
            int first = col.Count;
            for (int i = 0; i < o.Count; i++)
            {
                Vertex(o.Lx[i], y, o.Lz[i], colour);
                Vertex(o.Rx[i], y, o.Rz[i], colour);
            }
            for (int i = 0; i + 1 < o.Count; i++)
            {
                int l0 = first + 2 * i, r0 = l0 + 1, l1 = l0 + 2, r1 = l0 + 3;
                TriUp(l0, l1, r0, down);
                TriUp(r0, l1, r1, down);
            }
        }

        /// <summary>The sides of a ramp, from the ground to barrier height: a ramp stands on fill between retaining walls, so nothing can pass under it.</summary>
        void RampWalls(Outline o, Ramp ramp, uint colour)
        {
            for (int side = 0; side < 2; side++)
            {
                var xs = side == 0 ? o.Lx : o.Rx;
                var zs = side == 0 ? o.Lz : o.Rz;
                for (int i = 0; i + 1 < o.Count; i++)
                {
                    double ax = xs[i], az = zs[i], bx = xs[i + 1], bz = zs[i + 1];
                    if (Math.Sqrt((bx - ax) * (bx - ax) + (bz - az) * (bz - az)) < 0.05) continue;
                    int f = col.Count;
                    Vertex(ax, 0, az, colour);
                    Vertex(ax, ramp.HeightAt(ax, az) + RoadPalette.BarrierHeight, az, colour);
                    Vertex(bx, 0, bz, colour);
                    Vertex(bx, ramp.HeightAt(bx, bz) + RoadPalette.BarrierHeight, bz, colour);
                    idx.Add(f); idx.Add(f + 1); idx.Add(f + 2);
                    idx.Add(f + 1); idx.Add(f + 3); idx.Add(f + 2);
                    idx.Add(f); idx.Add(f + 2); idx.Add(f + 1);
                    idx.Add(f + 1); idx.Add(f + 2); idx.Add(f + 3);
                }
            }
        }

        void Walls(Outline o, double y0, double y1, uint colour)
        {
            for (int side = 0; side < 2; side++)
            {
                var xs = side == 0 ? o.Lx : o.Rx;
                var zs = side == 0 ? o.Lz : o.Rz;
                for (int i = 0; i + 1 < o.Count; i++)
                {
                    double ax = xs[i], az = zs[i], bx = xs[i + 1], bz = zs[i + 1];
                    if (Math.Sqrt((bx - ax) * (bx - ax) + (bz - az) * (bz - az)) < 0.05) continue;
                    int f = col.Count;
                    Vertex(ax, y0, az, colour);
                    Vertex(ax, y1, az, colour);
                    Vertex(bx, y0, bz, colour);
                    Vertex(bx, y1, bz, colour);
                    // Both faces: barriers are seen from the deck and from the street.
                    idx.Add(f); idx.Add(f + 1); idx.Add(f + 2);
                    idx.Add(f + 1); idx.Add(f + 3); idx.Add(f + 2);
                    idx.Add(f); idx.Add(f + 2); idx.Add(f + 1);
                    idx.Add(f + 1); idx.Add(f + 2); idx.Add(f + 3);
                }
            }
        }

        // ---- Outline ----

        public sealed class Outline
        {
            public readonly List<double> Lx = new List<double>(), Lz = new List<double>(), Rx = new List<double>(), Rz = new List<double>();
            public readonly List<double> Cx = new List<double>(), Cz = new List<double>(), H = new List<double>();
            public int Count => Lx.Count;
            public bool CutStart, CutEnd, Extended;
            /// <summary>Last cut: where the left and right edges cross the border.</summary>
            public double BorderLx, BorderLz, BorderRx, BorderRz;

            public void Add(double cx, double cz, double lx, double lz, double rx, double rz, double h)
            {
                Cx.Add(cx); Cz.Add(cz); Lx.Add(lx); Lz.Add(lz); Rx.Add(rx); Rz.Add(rz); H.Add(h);
            }

            /// <summary>As new, keeping the lists' capacity.</summary>
            public void Clear()
            {
                Lx.Clear(); Lz.Clear(); Rx.Clear(); Rz.Clear(); Cx.Clear(); Cz.Clear(); H.Clear();
                CutStart = CutEnd = Extended = false;
                BorderLx = BorderLz = BorderRx = BorderRz = 0;
            }
        }

        // What Build reuses from chunk to chunk, kept in the current BuildScratch set (C-07): one
        // outline per role (a road's outlines are finished with before the next road starts, and
        // nothing keeps them), the point lists of MiterOutline and the fillets, the chaining table
        // and the pad work lists. Called from outside Build, MiterOutline still returns a new outline.
        /// <summary>A set of small non-negative ints, emptied at once by Clear (a new stamp).</summary>
        sealed class Seen
        {
            int[] at = new int[1024];
            int stamp = 1;

            public void Clear()
            {
                if (++stamp == int.MaxValue)
                {
                    Array.Clear(at, 0, at.Length);
                    stamp = 1;
                }
            }

            /// <summary>Adds k; false when it was already in.</summary>
            public bool Add(int k)
            {
                if (k >= at.Length) Array.Resize(ref at, Math.Max(k + 1, 2 * at.Length));
                if (at[k] == stamp) return false;
                at[k] = stamp;
                return true;
            }
        }

        sealed class Scratch
        {
            public readonly Outline Road = new Outline(), Soffit = new Outline();
            public readonly List<double> Xs = new List<double>(64), Zs = new List<double>(64), Hs = new List<double>(64);
            public readonly List<double> FilletPoints = new List<double>(128), FilletHalves = new List<double>(64);
            public readonly Dictionary<(long, long), List<(VRoad road, int end)>> Ends = new Dictionary<(long, long), List<(VRoad road, int end)>>();
            public readonly List<List<(VRoad road, int end)>> EndLists = new List<List<(VRoad road, int end)>>();
            public readonly Dictionary<(long, long), (double x, double z, List<(VRoad road, int index)> hits)> Groups = new Dictionary<(long, long), (double x, double z, List<(VRoad road, int index)> hits)>();
            public readonly List<(long, long)> Order = new List<(long, long)>();
            public readonly List<List<(VRoad road, int index)>> HitLists = new List<List<(VRoad road, int index)>>();
            public readonly List<(VRoad road, int index)> Items = new List<(VRoad road, int index)>();
            public readonly List<(double x, double z, double half)> Tangents = new List<(double x, double z, double half)>(), Arms = new List<(double x, double z, double half)>(2);
            public readonly List<(double, double)> Raw = new List<(double, double)>(32);
            public readonly Dictionary<(long, long), (double, double)> Unique = new Dictionary<(long, long), (double, double)>();
            public readonly List<(double x, double z)> Sorted = new List<(double x, double z)>(32), Lower = new List<(double x, double z)>(32), Upper = new List<(double x, double z)>(32);
            public readonly List<int> FilletSources = new List<int>(64);
            // Markings (RoadMarkings.cs).
            public readonly Dictionary<long, List<GraphEdge>> EdgesByWay = new Dictionary<long, List<GraphEdge>>();
            public readonly List<List<GraphEdge>> EdgeLists = new List<List<GraphEdge>>();
            public readonly List<(double a, double b)> Spans = new List<(double a, double b)>(4), Spans2 = new List<(double a, double b)>(4),
                Pieces = new List<(double a, double b)>(16);
            public readonly List<(double x, double z)> Signals = new List<(double x, double z)>(8);
            public readonly double[] LastPlaced = new double[DetailKinds], Profile = new double[4], Heights = new double[4];
            // Sidewalks (Sidewalks.cs).
            public readonly List<(double x, double z, double tx, double tz, double half, double y)> Crossed = new List<(double x, double z, double tx, double tz, double half, double y)>(16);
            public readonly List<(double ai, double ao, double bi, double bo, double a0, double b0)> WalkCuts = new List<(double ai, double ao, double bi, double bo, double a0, double b0)>(4);
            public readonly List<double> Walls = new List<double>(4096), Widths = new List<double>(32), WidthTs = new List<double>(32), Breaks = new List<double>(32);
            public readonly List<int>[] WallCellLists = new List<int>[WallCells * WallCells], WalkCellLists = new List<int>[WallCells * WallCells];
            public readonly List<int> WallTouched = new List<int>(256), WalkTouched = new List<int>(256), Order2 = new List<int>(64);
            public readonly List<WalkQuad> WalkQuads = new List<WalkQuad>(1024);
            public readonly Seen WalkSeen = new Seen();
            public readonly List<(double a, double b)> FreeIn = new List<(double a, double b)>(4), FreeOut = new List<(double a, double b)>(4), FreePart = new List<(double a, double b)>(4);
            public readonly List<(Drawn road, int side, double a, double b)> Stops = new List<(Drawn road, int side, double a, double b)>(8);
            /// <summary>Crossing and stop-line keep-outs at grade (not junction pads), kept from Markings for the flyover supports (D-11a).</summary>
            public readonly List<List<(double x, double z)>> GradeKeepOuts = new List<List<(double x, double z)>>(16);
            /// <summary>The bus stop pads' quads at grade (inner a, inner b, outer b, outer a), for the flyover supports.</summary>
            public readonly List<(double ax, double az, double bx, double bz, double cx, double cz, double dx, double dz)> StopQuads = new List<(double, double, double, double, double, double, double, double)>(16);
            public readonly List<(double a, double b)> StopSpans = new List<(double a, double b)>(4), SlabSpans = new List<(double a, double b)>(8), Kept = new List<(double a, double b)>(4);
            public readonly double[] PendP = new double[8], PendH = new double[6];
            public readonly List<SegItem> SegItems = new List<SegItem>(1024);
            public readonly List<double> SegRings = new List<double>(8192);
            public readonly List<int>[] SegCellLists = new List<int>[WallCells * WallCells];
            public readonly List<int> SegTouched = new List<int>(256), SegFound = new List<int>(64);
            public readonly Seen SegSeen = new Seen();
        }

        static Scratch Mine()
        {
            var set = BuildScratch.Current;
            return (Scratch)(set.Roads ??= new Scratch());
        }

        static Outline Reused(Outline o)
        {
            o.Clear();
            return o;
        }

        static List<T> Emptied<T>(List<T> list)
        {
            list.Clear();
            return list;
        }

        /// <summary>
        /// The browser build's miterOutline: left is the centreline normal turned counter-clockwise
        /// (east, north). Corners are mitred; mitres longer than MiterLimit x half become bevels.
        /// Points closer than 5 cm to the previous one are dropped, except the last.
        /// </summary>
        public static Outline MiterOutline(double[] flat, double half)
        {
            var halves = new double[flat.Length / 2];
            for (int i = 0; i < halves.Length; i++) halves[i] = half;
            return MiterOutline(flat, halves, 0, 1);
        }

        /// <summary>As above with a half width per point: (halves[i] + extra) x scale, written into
        /// <paramref name="into"/> (emptied) when given.</summary>
        public static Outline MiterOutline(double[] flat, double[] halves, double extra, double scale, Outline into = null)
        {
            int nIn = flat.Length / 2;
            var mine = Mine();
            var xs = Emptied(mine.Xs);
            var zs = Emptied(mine.Zs);
            var hs = Emptied(mine.Hs);
            for (int i = 0; i < nIn; i++)
            {
                double x = flat[2 * i], z = flat[2 * i + 1];
                if (xs.Count > 0 && Dist(x, z, xs[xs.Count - 1], zs[zs.Count - 1]) < 0.05 && i != nIn - 1) continue;
                xs.Add(x);
                zs.Add(z);
                hs.Add((halves[i] + extra) * scale);
            }
            int n = xs.Count;
            if (n < 2) return null;
            var o = into ?? new Outline();
            for (int i = 0; i < n; i++)
            {
                double half = hs[i];
                if (half <= 0) return null;
                double inx = 0, inz = 0, outx = 0, outz = 0;
                bool hasIn = i > 0 && Seg(xs, zs, i - 1, i, out inx, out inz);
                bool hasOut = i + 1 < n && Seg(xs, zs, i, i + 1, out outx, out outz);
                if (!hasIn || !hasOut)
                {
                    double dx, dz;
                    if (hasIn) { dx = inx; dz = inz; }
                    else if (hasOut) { dx = outx; dz = outz; }
                    else continue;
                    double nx = -dz * half, nz = dx * half;
                    o.Add(xs[i], zs[i], xs[i] + nx, zs[i] + nz, xs[i] - nx, zs[i] - nz, half);
                    continue;
                }
                double ax = -inz, az = inx, bx = -outz, bz = outx;
                double mx = ax + bx, mz = az + bz, ml = Math.Sqrt(mx * mx + mz * mz);
                bool bevel = ml < 0.2;
                double miter = 0;
                if (!bevel)
                {
                    mx /= ml;
                    mz /= ml;
                    double denom = mx * ax + mz * az;
                    bevel = denom < 0.2;
                    if (!bevel)
                    {
                        miter = half / denom;
                        bevel = miter > half * RoadPalette.MiterLimit;
                    }
                }
                if (bevel)
                {
                    o.Add(xs[i], zs[i], xs[i] + ax * half, zs[i] + az * half, xs[i] - ax * half, zs[i] - az * half, half);
                    o.Add(xs[i], zs[i], xs[i] + bx * half, zs[i] + bz * half, xs[i] - bx * half, zs[i] - bz * half, half);
                }
                else o.Add(xs[i], zs[i], xs[i] + mx * miter, zs[i] + mz * miter, xs[i] - mx * miter, zs[i] - mz * miter, half);
            }
            return o.Count >= 2 ? o : null;
        }

        /// <summary>
        /// Moves the end corners of an outline whose centre end lies on the chunk border onto the
        /// border line, along the outline's own edge lines. Both chunks share the border point and
        /// the end segment's direction, so the two cuts meet exactly.
        /// </summary>
        static void CutAtBorder(Outline o)
        {
            o.CutStart = Cut(o, 0, 1);
            o.CutEnd = Cut(o, o.Count - 1, -1);
        }

        /// <summary>Crossings flatter than this (|cos| of the angle to the border normal) are not cut but extended.</summary>
        public const double MinCutCos = 0.5;
        /// <summary>Every border end reaches this far into the neighbour, so shared edges cannot crack.</summary>
        public const double Overlap = 0.02;

        /// <summary>
        /// Ends a road whose centre end lies on the chunk border. A crossing within 60 degrees of
        /// square is cut along the border: both chunks share the border point and, mostly, the end
        /// segment's direction, so the cuts meet. A flatter crossing is ill-conditioned (a centimetre
        /// of rounding moves the cut corners by decimetres), so its square end is pushed past the
        /// border instead; the two chunks then overlap. Records where the edge lines cross the border.
        /// </summary>
        static bool Cut(Outline o, int end, int step)
        {
            double cx = o.Cx[end], cz = o.Cz[end];
            bool west = Math.Abs(cx) < OnEdge, east = Math.Abs(cx - Size) < OnEdge;
            bool south = Math.Abs(cz) < OnEdge, north = Math.Abs(cz - Size) < OnEdge;
            if (!(west || east || south || north)) return false;
            int inner = end + step;
            double dx = o.Cx[inner] - cx, dz = o.Cz[inner] - cz, len = Math.Sqrt(dx * dx + dz * dz);
            if (len < 1e-9) return false;
            dx /= len;
            dz /= len;
            // On a corner, use the border the road crosses more squarely.
            bool alongX = (west || east) && (!(south || north) || Math.Abs(dx) >= Math.Abs(dz));
            double line = alongX ? (west ? 0 : Size) : (south ? 0 : Size);
            double d = alongX ? dx : dz;
            if (Math.Abs(d) < 0.05) return false; // running along the border
            double outward = alongX ? (west ? -1 : 1) : (south ? -1 : 1);
            double lx = o.Lx[end], lz = o.Lz[end], rx = o.Rx[end], rz = o.Rz[end];
            // Where the left and right edge lines (direction d) cross the border line.
            double tl = alongX ? (line - lx) / dx : (line - lz) / dz;
            double tr = alongX ? (line - rx) / dx : (line - rz) / dz;
            o.BorderLx = lx + dx * tl; o.BorderLz = lz + dz * tl;
            o.BorderRx = rx + dx * tr; o.BorderRz = rz + dz * tr;
            if (alongX) { o.BorderLx = o.BorderRx = line; }
            else { o.BorderLz = o.BorderRz = line; }
            if (Math.Abs(d) >= MinCutCos)
            {
                o.Lx[end] = o.BorderLx; o.Lz[end] = o.BorderLz;
                o.Rx[end] = o.BorderRx; o.Rz[end] = o.BorderRz;
                if (alongX) { o.Lx[end] += outward * Overlap; o.Rx[end] += outward * Overlap; }
                else { o.Lz[end] += outward * Overlap; o.Rz[end] += outward * Overlap; }
            }
            else
            {
                // Square end, moved outwards (against d, which points into the chunk) until both
                // corners are Overlap past the border.
                double pl = alongX ? (lx - line) * outward : (lz - line) * outward;
                double pr = alongX ? (rx - line) * outward : (rz - line) * outward;
                double need = Math.Max(0, Overlap - Math.Min(pl, pr)); // still to go, measured across the border
                double t = need / Math.Abs(d);
                o.Lx[end] -= dx * t; o.Lz[end] -= dz * t;
                o.Rx[end] -= dx * t; o.Rz[end] -= dz * t;
                o.Extended = true;
            }
            return true;
        }

        static bool Seg(List<double> xs, List<double> zs, int i, int j, out double ux, out double uz)
        {
            double dx = xs[j] - xs[i], dz = zs[j] - zs[i], len = Math.Sqrt(dx * dx + dz * dz);
            if (len < 1e-6) { ux = uz = 0; return false; }
            ux = dx / len;
            uz = dz / len;
            return true;
        }

        static double Dist(double ax, double az, double bx, double bz) => Math.Sqrt((ax - bx) * (ax - bx) + (az - bz) * (az - bz));

        // ---- Chaining, stems and pads (roads.ts prepareVisualRoads) ----

        static (long, long) Key(double x, double z) => ((long)Math.Floor(x / Snap + 0.5), (long)Math.Floor(z / Snap + 0.5));

        static bool OnBorder(double x, double z) =>
            x <= BorderZone || z <= BorderZone || x >= Size - BorderZone || z >= Size - BorderZone;

        static bool SameLevel(double a, double b) => Math.Abs(a - b) < 0.8;

        static bool OnEdgeLine(double x, double z) =>
            Math.Abs(x) < OnEdge || Math.Abs(z) < OnEdge || Math.Abs(x - Size) < OnEdge || Math.Abs(z - Size) < OnEdge;

        static bool DirAway(double[] pts, int end, out double x, out double z, out double dx, out double dz)
        {
            int n = pts.Length / 2;
            x = z = dx = dz = 0;
            if (n < 2) return false;
            int i = end == 0 ? 0 : n - 1, j = end == 0 ? 1 : n - 2;
            x = pts[2 * i];
            z = pts[2 * i + 1];
            dx = pts[2 * j] - x;
            dz = pts[2 * j + 1] - z;
            double len = Math.Sqrt(dx * dx + dz * dz);
            if (len < 1e-4) return false;
            dx /= len;
            dz /= len;
            return true;
        }

        static double[] Reverse(double[] pts)
        {
            var o = new double[pts.Length];
            int n = pts.Length / 2;
            for (int i = 0; i < n; i++)
            {
                o[2 * i] = pts[2 * (n - 1 - i)];
                o[2 * i + 1] = pts[2 * (n - 1 - i) + 1];
            }
            return o;
        }

        static double[] ReverseHalves(double[] h)
        {
            var o = (double[])h.Clone();
            Array.Reverse(o);
            return o;
        }

        static int ChainRoads(List<VRoad> roads)
        {
            int merges = 0, guard = roads.Count;
            // Each pass joins one pair and starts over, so the end table and its lists are reused
            // across passes and chunks rather than made again (C-07). A cleared dictionary enumerates in
            // insertion order as a new one does, so the pair each pass finds is unchanged.
            var mine = Mine();
            var ends = mine.Ends;
            var lists = mine.EndLists;
            while (guard-- > 0)
            {
                ends.Clear();
                int used = 0;
                foreach (var road in roads)
                {
                    if (!road.Alive) continue;
                    for (int end = 0; end < 2; end++)
                    {
                        if (!DirAway(road.Pts, end, out double x, out double z, out _, out _)) continue;
                        // An end on the chunk border stays an end: the neighbour cuts its half there too.
                        if (OnEdgeLine(x, z)) continue;
                        var k = Key(x, z);
                        if (!ends.TryGetValue(k, out var list))
                        {
                            if (used == lists.Count) lists.Add(new List<(VRoad, int)>(2));
                            list = lists[used++];
                            list.Clear();
                            ends[k] = list;
                        }
                        list.Add((road, end));
                    }
                }
                bool merged = false;
                foreach (var list in ends.Values)
                {
                    if (list.Count != 2 || list[0].road == list[1].road) continue;
                    var (a, ae) = list[0];
                    var (b, be) = list[1];
                    if (a.Bridge != b.Bridge || !SameLevel(a.Y, b.Y)) continue;
                    double maxH = Math.Max(a.Half, b.Half);
                    if (Math.Abs(a.Half - b.Half) / maxH > 0.25) continue;
                    if (!DirAway(a.Pts, ae, out _, out _, out double adx, out double adz)) continue;
                    if (!DirAway(b.Pts, be, out _, out _, out double bdx, out double bdz)) continue;
                    if (adx * bdx + adz * bdz > 0.75) continue;
                    var left = ae == 1 ? a.Pts : Reverse(a.Pts);
                    var right = be == 0 ? b.Pts : Reverse(b.Pts);
                    var joined = new double[left.Length + right.Length - 2];
                    Array.Copy(left, joined, left.Length);
                    Array.Copy(right, 2, joined, left.Length, right.Length - 2);
                    var lh = ae == 1 ? a.Halves : ReverseHalves(a.Halves);
                    var rh = be == 0 ? b.Halves : ReverseHalves(b.Halves);
                    a.Attrs = JoinSegs(ae == 1 ? a.Attrs : ReverseSegs(a.Attrs), be == 0 ? b.Attrs : ReverseSegs(b.Attrs));
                    var halves = new double[lh.Length + rh.Length - 1];
                    Array.Copy(lh, halves, lh.Length);
                    Array.Copy(rh, 1, halves, lh.Length, rh.Length - 1);
                    halves[lh.Length - 1] = (lh[lh.Length - 1] + rh[0]) / 2;
                    a.StartId = ae == 1 ? a.StartId : a.EndId;
                    a.EndId = be == 0 ? b.EndId : b.StartId;
                    a.Pts = joined;
                    a.Halves = halves;
                    a.Half = (a.Half + b.Half) / 2;
                    a.Y = (a.Y + b.Y) / 2;
                    a.Marked |= b.Marked;
                    b.Alive = false;
                    merged = true;
                    merges++;
                    break;
                }
                if (!merged) break;
            }
            return merges;
        }

        static void ClosestOnSegment(double px, double pz, double ax, double az, double bx, double bz, out double d, out double t)
        {
            double dx = bx - ax, dz = bz - az, len2 = dx * dx + dz * dz;
            if (len2 == 0) len2 = 1;
            t = Math.Max(0, Math.Min(1, ((px - ax) * dx + (pz - az) * dz) / len2));
            d = Dist(px, pz, ax + dx * t, az + dz * t);
        }

        static void MoveToward(double[] pts, int index, int toward, double dist)
        {
            double dx = pts[2 * toward] - pts[2 * index], dz = pts[2 * toward + 1] - pts[2 * index + 1];
            double len = Math.Sqrt(dx * dx + dz * dz);
            if (len < 0.2) return;
            double d = Math.Min(Math.Max(0, dist), len * 0.55);
            if (d < 0.08) return;
            pts[2 * index] += dx / len * d;
            pts[2 * index + 1] += dz / len * d;
        }

        static int TrimStems(List<VRoad> roads)
        {
            int trimmed = 0;
            var interiors = new Dictionary<(long, long), List<VRoad>>();
            foreach (var road in roads)
            {
                if (!road.Alive) continue;
                for (int i = 1; i < road.N - 1; i++)
                {
                    var k = Key(road.Pts[2 * i], road.Pts[2 * i + 1]);
                    if (!interiors.TryGetValue(k, out var list)) interiors[k] = list = new List<VRoad>();
                    list.Add(road);
                }
            }
            foreach (var road in roads)
            {
                if (!road.Alive || road.N < 2) continue;
                for (int end = 0; end < 2; end++)
                {
                    if (!DirAway(road.Pts, end, out double x, out double z, out _, out _) || OnBorder(x, z)) continue;
                    double inset = 0;
                    bool hit = false;
                    if (interiors.TryGetValue(Key(x, z), out var others))
                        foreach (var o in others)
                        {
                            if (o.Uid == road.Uid || !SameLevel(o.Y, road.Y)) continue;
                            if (!hit) { inset = o.Half - 0.12; hit = true; }
                            else inset = Math.Max(inset, o.Half - 0.12);
                        }
                    if (!hit)
                        foreach (var other in roads)
                        {
                            if (!other.Alive || other.Id == road.Id || !SameLevel(other.Y, road.Y)) continue;
                            for (int s = 0; s < other.N - 1; s++)
                            {
                                ClosestOnSegment(x, z, other.Pts[2 * s], other.Pts[2 * s + 1], other.Pts[2 * s + 2], other.Pts[2 * s + 3], out double d, out double t);
                                if (t < 0.04 || t > 0.96 || d > 1.25) continue;
                                inset = Math.Max(inset, other.Half - 0.12);
                            }
                        }
                    if (inset < 0.2) continue;
                    int index = end == 0 ? 0 : road.N - 1, toward = end == 0 ? 1 : road.N - 2;
                    double bx = road.Pts[2 * index], bz = road.Pts[2 * index + 1];
                    MoveToward(road.Pts, index, toward, inset);
                    if (road.Pts[2 * index] != bx || road.Pts[2 * index + 1] != bz) trimmed++;
                }
            }
            return trimmed;
        }

        static bool TangentAt(double[] pts, int index, out double tx, out double tz)
        {
            int n = pts.Length / 2, i0 = Math.Max(0, index - 1), i1 = Math.Min(n - 1, index + 1);
            tx = tz = 0;
            if (i0 == i1) return false;
            double dx = pts[2 * i1] - pts[2 * i0], dz = pts[2 * i1 + 1] - pts[2 * i0 + 1], len = Math.Sqrt(dx * dx + dz * dz);
            if (len < 1e-4) return false;
            tx = dx / len;
            tz = dz / len;
            return true;
        }

        static Pad MakePad(double cx, double cz, List<(double x, double z, double half)> tangents, double y, bool marked)
        {
            if (tangents.Count < 2) return null;
            bool angled = false;
            for (int i = 0; i < tangents.Count && !angled; i++)
                for (int j = i + 1; j < tangents.Count; j++)
                    if (Math.Abs(tangents[i].x * tangents[j].x + tangents[i].z * tangents[j].z) < 0.82) { angled = true; break; }
            if (!angled) return null;
            double maxH = 0;
            foreach (var t in tangents) maxH = Math.Max(maxH, t.half);
            var raw = Emptied(Mine().Raw);
            foreach (var t in tangents)
            {
                raw.Add((cx - t.z * t.half, cz + t.x * t.half));
                raw.Add((cx + t.z * t.half, cz - t.x * t.half));
            }
            for (int i = 0; i < tangents.Count; i++)
                for (int j = i + 1; j < tangents.Count; j++)
                {
                    var a = tangents[i];
                    var b = tangents[j];
                    double det = a.x * b.z - a.z * b.x;
                    if (Math.Abs(det) < 0.2) continue;
                    for (int s1 = 1; s1 >= -1; s1 -= 2)
                        for (int s2 = 1; s2 >= -1; s2 -= 2)
                        {
                            double p1x = cx - a.z * a.half * s1, p1z = cz + a.x * a.half * s1;
                            double p2x = cx - b.z * b.half * s2, p2z = cz + b.x * b.half * s2;
                            double t = ((p2x - p1x) * b.z - (p2z - p1z) * b.x) / det;
                            if (Math.Abs(t) > maxH * 2.4) continue;
                            double x = p1x + a.x * t, z = p1z + a.z * t;
                            if (Dist(x, z, cx, cz) > maxH * 2.6) continue;
                            raw.Add((x, z));
                        }
                }
            var ring = ConvexHull(raw);
            if (ring.Count < 3) return null;
            foreach (var q in ring) if (Dist(q.x, q.z, cx, cz) > maxH * 2.8) return null;
            return new Pad { Y = y, Marked = marked, Ring = ring };
        }

        static List<(double x, double z)> ConvexHull(List<(double x, double z)> points)
        {
            var mine = Mine();
            var uniq = mine.Unique;
            uniq.Clear();
            foreach (var q in points) uniq[((long)Math.Round(q.x * 100), (long)Math.Round(q.z * 100))] = q;
            var pts = Emptied(mine.Sorted);
            foreach (var q in uniq.Values) pts.Add(q);
            pts.Sort(HullOrder);
            if (pts.Count < 3) return new List<(double x, double z)>();
            double Cross((double x, double z) o, (double x, double z) a, (double x, double z) b) => (a.x - o.x) * (b.z - o.z) - (a.z - o.z) * (b.x - o.x);
            var lower = Emptied(mine.Lower);
            foreach (var q in pts)
            {
                while (lower.Count >= 2 && Cross(lower[lower.Count - 2], lower[lower.Count - 1], q) <= 0) lower.RemoveAt(lower.Count - 1);
                lower.Add(q);
            }
            var upper = Emptied(mine.Upper);
            for (int i = pts.Count - 1; i >= 0; i--)
            {
                var q = pts[i];
                while (upper.Count >= 2 && Cross(upper[upper.Count - 2], upper[upper.Count - 1], q) <= 0) upper.RemoveAt(upper.Count - 1);
                upper.Add(q);
            }
            lower.RemoveAt(lower.Count - 1);
            upper.RemoveAt(upper.Count - 1);
            var hull = new List<(double x, double z)>(lower.Count + upper.Count);
            hull.AddRange(lower);
            hull.AddRange(upper);
            return hull;
        }

        static readonly Comparison<(double x, double z)> HullOrder = (a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.z.CompareTo(b.z);

        static List<Pad> PadsFor(List<VRoad> roads)
        {
            var mine = Mine();
            var groups = mine.Groups;
            var order = mine.Order;
            groups.Clear();
            order.Clear();
            int usedHits = 0;
            foreach (var road in roads)
                for (int i = 0; i < road.N; i++)
                {
                    double x = road.Pts[2 * i], z = road.Pts[2 * i + 1];
                    if (OnBorder(x, z)) continue;
                    var k = Key(x, z);
                    if (!groups.TryGetValue(k, out var g))
                    {
                        if (usedHits == mine.HitLists.Count) mine.HitLists.Add(new List<(VRoad road, int index)>(4));
                        var hits = mine.HitLists[usedHits++];
                        hits.Clear();
                        g = (x, z, hits);
                        groups[k] = g;
                        order.Add(k);
                    }
                    g.hits.Add((road, i));
                }
            var pads = new List<Pad>();
            var tangents = mine.Tangents;
            var items = mine.Items;
            foreach (var k in order)
            {
                var g = groups[k];
                items.Clear();
                foreach (var h in g.hits)
                {
                    bool seen = false;
                    foreach (var it in items) if (it.road == h.road) { seen = true; break; }
                    if (!seen) items.Add(h);
                }
                if (items.Count < 2) continue;
                bool level = true;
                double y = double.MinValue;
                bool marked = false;
                foreach (var it in items)
                {
                    if (!SameLevel(it.road.Y, items[0].road.Y)) level = false;
                    y = Math.Max(y, it.road.Y);
                    marked |= it.road.Marked;
                }
                if (!level) continue;
                tangents.Clear();
                foreach (var it in items)
                    if (TangentAt(it.road.Pts, it.index, out double tx, out double tz)) tangents.Add((tx, tz, it.road.Half));
                var pad = MakePad(g.x, g.z, tangents, y + RoadPalette.PadLift, marked);
                if (pad != null) pads.Add(pad);
            }
            // Roads that cross mid-segment without sharing a point.
            for (int i = 0; i < roads.Count; i++)
            {
                var a = roads[i];
                Bounds(a, out double aminx, out double aminz, out double amaxx, out double amaxz);
                for (int j = i + 1; j < roads.Count; j++)
                {
                    var b = roads[j];
                    if (!SameLevel(a.Y, b.Y)) continue;
                    Bounds(b, out double bminx, out double bminz, out double bmaxx, out double bmaxz);
                    if (bminx > amaxx || bmaxx < aminx || bminz > amaxz || bmaxz < aminz) continue;
                    for (int s = 0; s < a.N - 1; s++)
                    {
                        double a1x = a.Pts[2 * s], a1z = a.Pts[2 * s + 1], dax = a.Pts[2 * s + 2] - a1x, daz = a.Pts[2 * s + 3] - a1z;
                        for (int t = 0; t < b.N - 1; t++)
                        {
                            double b1x = b.Pts[2 * t], b1z = b.Pts[2 * t + 1], dbx = b.Pts[2 * t + 2] - b1x, dbz = b.Pts[2 * t + 3] - b1z;
                            double det = dax * dbz - daz * dbx;
                            if (Math.Abs(det) < 1e-4) continue;
                            double ux = b1x - a1x, uz = b1z - a1z;
                            double ta = (ux * dbz - uz * dbx) / det, tb = (ux * daz - uz * dax) / det;
                            if (ta < 0.08 || ta > 0.92 || tb < 0.08 || tb > 0.92) continue;
                            double x = a1x + dax * ta, z = a1z + daz * ta;
                            if (OnBorder(x, z)) continue;
                            bool close = false;
                            foreach (var pd in pads) if (Dist(pd.Ring[0].x, pd.Ring[0].z, x, z) < 3) { close = true; break; }
                            if (close) continue;
                            double al = Math.Sqrt(dax * dax + daz * daz), bl = Math.Sqrt(dbx * dbx + dbz * dbz);
                            if (al == 0) al = 1;
                            if (bl == 0) bl = 1;
                            var arms = Emptied(mine.Arms);
                            arms.Add((dax / al, daz / al, a.Half));
                            arms.Add((dbx / bl, dbz / bl, b.Half));
                            var pad = MakePad(x, z, arms, Math.Max(a.Y, b.Y) + RoadPalette.PadLift, a.Marked || b.Marked);
                            if (pad != null) pads.Add(pad);
                        }
                    }
                }
            }
            return pads;
        }

        static void Bounds(VRoad r, out double minx, out double minz, out double maxx, out double maxz)
        {
            minx = minz = double.MaxValue;
            maxx = maxz = double.MinValue;
            for (int i = 0; i < r.N; i++)
            {
                minx = Math.Min(minx, r.Pts[2 * i]);
                maxx = Math.Max(maxx, r.Pts[2 * i]);
                minz = Math.Min(minz, r.Pts[2 * i + 1]);
                maxz = Math.Max(maxz, r.Pts[2 * i + 1]);
            }
        }

        // ---- Fillets (meshes.ts pinsForRoad, roads.ts filletGentleBends) ----

        sealed class NodeIndex
        {
            readonly Dictionary<(long, long), List<(double x, double z)>> buckets = new Dictionary<(long, long), List<(double, double)>>();
            readonly double r2;
            const double Cell = 4;

            public NodeIndex(List<(double x, double z)> nodes, double radius)
            {
                r2 = radius * radius;
                foreach (var n in nodes)
                {
                    var k = ((long)Math.Floor(n.x / Cell), (long)Math.Floor(n.z / Cell));
                    if (!buckets.TryGetValue(k, out var b)) buckets[k] = b = new List<(double, double)>();
                    b.Add(n);
                }
            }

            public bool Near(double x, double z)
            {
                long cx = (long)Math.Floor(x / Cell), cz = (long)Math.Floor(z / Cell);
                for (long dz = -1; dz <= 1; dz++)
                    for (long dx = -1; dx <= 1; dx++)
                    {
                        if (!buckets.TryGetValue((cx + dx, cz + dz), out var b)) continue;
                        foreach (var n in b)
                            if ((n.x - x) * (n.x - x) + (n.z - z) * (n.z - z) <= r2) return true;
                    }
                return false;
            }
        }

        static bool[] Pins(double[] pts, NodeIndex near)
        {
            int n = pts.Length / 2;
            var pinned = new bool[n];
            if (n > 0) { pinned[0] = true; pinned[n - 1] = true; }
            for (int i = 1; i < n - 1; i++) if (near.Near(pts[2 * i], pts[2 * i + 1])) pinned[i] = true;
            return pinned;
        }

        /// <summary>Short quadratic fillets on gentle bends (cos 0.72..0.992); new points stay within maxMove of the corner.</summary>
        public static double[] FilletGentleBends(double[] flat, bool[] pinned, double maxMove = 1.15) =>
            FilletGentleBends(flat, pinned, new double[flat.Length / 2], out _, maxMove);

        /// <summary>As above, carrying a half width per point: new points take their corner's.</summary>
        public static double[] FilletGentleBends(double[] flat, bool[] pinned, double[] halves, out double[] newHalves, double maxMove = 1.15) =>
            FilletGentleBends(flat, pinned, halves, out newHalves, out _, maxMove);

        /// <summary>As above, also giving for each new point the original point whose segment the
        /// segment leaving it is part of: a fillet's first half belongs to the segment into the
        /// corner, its second half to the segment out.</summary>
        public static double[] FilletGentleBends(double[] flat, bool[] pinned, double[] halves, out double[] newHalves, out int[] source, double maxMove = 1.15)
        {
            int n = flat.Length / 2;
            if (n < 3)
            {
                newHalves = (double[])halves.Clone();
                source = new int[n];
                for (int i = 0; i < n; i++) source[i] = i;
                return (double[])flat.Clone();
            }
            var mine = Mine();
            var o = Emptied(mine.FilletPoints);
            var hs = Emptied(mine.FilletHalves);
            var src = Emptied(mine.FilletSources);
            o.Add(flat[0]);
            o.Add(flat[1]);
            hs.Add(halves[0]);
            src.Add(0);
            for (int i = 1; i < n - 1; i++)
            {
                double ax = flat[2 * i - 2], az = flat[2 * i - 1], bx = flat[2 * i], bz = flat[2 * i + 1], cx = flat[2 * i + 2], cz = flat[2 * i + 3];
                int before = o.Count;
                FilletAt(o, ax, az, bx, bz, cx, cz, pinned[i], maxMove);
                int added = (o.Count - before) / 2;
                for (int k = 0; k < added; k++)
                {
                    hs.Add(halves[i]);
                    src.Add(added == 1 || k >= added / 2 ? i : i - 1);
                }
            }
            o.Add(flat[2 * n - 2]);
            o.Add(flat[2 * n - 1]);
            hs.Add(halves[n - 1]);
            src.Add(n - 1);
            newHalves = hs.ToArray();
            source = src.ToArray();
            return o.ToArray();
        }

        static readonly double[] FilletSteps = { 0.35, 0.65 };

        static void FilletAt(List<double> o, double ax, double az, double bx, double bz, double cx, double cz, bool pinned, double maxMove)
        {
            if (pinned) { o.Add(bx); o.Add(bz); return; }
            double abx = bx - ax, abz = bz - az, bcx = cx - bx, bcz = cz - bz;
            double lab = Math.Sqrt(abx * abx + abz * abz), lbc = Math.Sqrt(bcx * bcx + bcz * bcz);
            if (lab == 0) lab = 1;
            if (lbc == 0) lbc = 1;
            double dot = (abx * bcx + abz * bcz) / (lab * lbc);
            if (dot >= 0.992 || dot <= 0.72) { o.Add(bx); o.Add(bz); return; }
            double sinHalf = Math.Sqrt(Math.Max(0, (1 - dot) / 2));
            double maxT = sinHalf > 1e-4 ? maxMove / (0.5 * sinHalf) : 0;
            double t = Math.Min(Math.Min(lab * 0.45, lbc * 0.45), maxT);
            if (t < 0.45) { o.Add(bx); o.Add(bz); return; }
            double t1x = bx - abx / lab * t, t1z = bz - abz / lab * t, t2x = bx + bcx / lbc * t, t2z = bz + bcz / lbc * t;
            o.Add(t1x); o.Add(t1z);
            foreach (double u in FilletSteps)
            {
                double w = 1 - u;
                o.Add(w * w * t1x + 2 * w * u * bx + u * u * t2x);
                o.Add(w * w * t1z + 2 * w * u * bz + u * u * t2z);
            }
            o.Add(t2x); o.Add(t2z);
        }
    }
}
