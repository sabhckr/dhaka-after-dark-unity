// The lane graph of one chunk (F-01), built off the main thread from the road graph in the chunk file.
//
// Bangladesh drives on the left, and so do these lanes: each direction of a road takes the left of its
// travel, lane 0 next to the left kerb. How many lanes a road has comes from RoadMarkings.LaneCount, so
// lanes run between the lines the road shows. Lanes follow the graph edges, offset from the centreline.
// What is left out, and counted:
// - ways that are not for vehicles (footway, steps, pedestrian, path, construction);
// - tunnels. Elevated decks have lanes: a flyover's ramps reach them, and a deck with no ramp is pruned as out of reach;
// - a direction with no room left beside what stands on the road (below).
//
// What stands on the road (ChunkStreet.Blocks: viaduct and flyover piers, median islands, the median) narrows
// the band a direction may use, for the whole edge: the band keeps its wider free side, and lanes are spaced
// across it, fewer if they would be under MinLane. So no lane runs through a pier; the road data stays as it is.
//
// The widths in the data are estimates, so paired one-ways and links can overlap in plan (K-31). Roads that overlap
// share the overlap out between their lanes, and a direction left without room is closed and counted; the surface
// stays as it is.
//
// At a junction (three arms or more) each road lane is cut back clear of the other arms and joined to the lanes
// it may turn into by connectors, short Bezier curves that are lanes too: straight on lane for lane, left from
// the left lane, right from the right lane. Two connectors that cross or share a target conflict (Lane.Conflicts).
// A node with two arms joins lane for lane; a dead end turns traffic round where the road is two-way. A lane
// that can reach nothing is removed, repeatedly. An arm ending on the chunk border is left open (Lane.OpenEnd,
// Lane.OpenStart) for TrafficWorld to join to the neighbour's lanes through the shared graph node.
//
// Junction kinds (F-02): a mapped signal makes a Signal junction, with every signalled node joined by short links
// (a dual road's crossing is several nodes) in one group; a crossing of two major roads with no signal mapped is
// run by a traffic officer (Officer, a labelled rule: the design's "signal or officer"), which times its phases
// the same way; elsewhere the lesser road yields (Priority), and equal roads take turns (AllWay).
//
// Positions are metres from the chunk's south-west corner. No UnityEngine dependency.
using System;
using System.Collections.Generic;

namespace Dad.World
{
    public enum JunctionKind : byte { Priority, AllWay, Officer, Signal }

    public sealed class Lane
    {
        public int Id;
        /// <summary>A connector inside a junction, not a stretch of road.</summary>
        public bool Connector;
        public long Way;
        /// <summary>RoadRank of the road: lower is the greater road.</summary>
        public byte Rank;
        /// <summary>Across its direction, from the left kerb; and how many there are.</summary>
        public byte Index, Count;
        /// <summary>x, z pairs, and the distance along at each point.</summary>
        public double[] Pts, Arc;
        public double Length;
        public float Y, Width;
        /// <summary>A height at each point, on a ramp; null on a level road.</summary>
        public float[] Ys;
        /// <summary>The height at s along the lane.</summary>
        public float YAt(double s)
        {
            if (Ys == null) return Y;
            int i = Array.BinarySearch(Arc, s);
            if (i < 0) i = ~i - 1;
            if (i < 0) return Ys[0];
            if (i >= Ys.Length - 1) return Ys[Ys.Length - 1];
            double t = (s - Arc[i]) / Math.Max(1e-6, Arc[i + 1] - Arc[i]);
            return (float)(Ys[i] + (Ys[i + 1] - Ys[i]) * t);
        }
        /// <summary>The road's speed, metres a second.</summary>
        public float Speed;
        /// <summary>Which direction of which road this lane belongs to: lanes side by side share it. A connector takes its first lane's.</summary>
        public int Direction;
        /// <summary>The lanes beside this one going the same way, -1 for none.</summary>
        public int Left = -1, Right = -1;
        /// <summary>Lanes this one leads to (connectors, or the road lane after a connector), and leads from.</summary>
        public int[] Next = Array.Empty<int>();
        public int[] Prev = Array.Empty<int>();
        /// <summary>The junction at this lane's end, or for a connector the one it is in; -1 for none.</summary>
        public int Junction = -1;
        /// <summary>Which of the junction's phases lets this approach go; -1 inside the group, where traffic is always let clear.</summary>
        public sbyte Phase = -1;
        /// <summary>This lane gives way at its end (a lesser road at a Priority junction).</summary>
        public bool Yield;
        /// <summary>Where a vehicle's front waits on this lane for the junction.</summary>
        public double StopS;
        /// <summary>Connectors of the same node that this one crosses or merges with.</summary>
        public int[] Conflicts = Array.Empty<int>();
        /// <summary>Ends or starts on the chunk border, at this node (world centimetres): the neighbour's lanes join here.</summary>
        public bool OpenEnd, OpenStart;
        public long NodeEast, NodeNorth;
        /// <summary>A connector's turn: +1 left, -1 right, 0 straight on.</summary>
        public sbyte Turn;
        /// <summary>Where a bus stops on this lane, metres along; empty on most.</summary>
        public double[] BusStops = Array.Empty<double>();

        /// <summary>The point s metres along, and the direction there.</summary>
        public void At(double s, out double x, out double z, out double dx, out double dz)
        {
            int n = Arc.Length, lo = 0, hi = n - 1;
            if (s <= 0) hi = 1;
            else if (s >= Length) lo = n - 2;
            else
                while (hi - lo > 1)
                {
                    int m = (lo + hi) >> 1;
                    if (Arc[m] <= s) lo = m; else hi = m;
                }
            if (s <= 0) lo = 0;
            double ax = Pts[2 * lo], az = Pts[2 * lo + 1], bx = Pts[2 * hi], bz = Pts[2 * hi + 1], len = Arc[hi] - Arc[lo];
            dx = bx - ax;
            dz = bz - az;
            double d = Math.Sqrt(dx * dx + dz * dz);
            if (d > 1e-9) { dx /= d; dz /= d; } else { dx = 1; dz = 0; }
            double t = len > 1e-9 ? (s - Arc[lo]) / len : 0;
            t = t < 0 ? 0 : t > 1 ? 1 : t;
            x = ax + (bx - ax) * t;
            z = az + (bz - az) * t;
        }
    }

    public sealed class LaneJunction
    {
        public int Id;
        public JunctionKind Kind;
        public double X, Z;
        /// <summary>Phases a controlled junction cycles through (0 for the others), and each one's green, seconds.</summary>
        public int Phases;
        public float Green;
        /// <summary>The main road's line through the junction: arms along it go in phase 0.</summary>
        public double AxisX = 1, AxisZ;
        /// <summary>The graph nodes of a group, for reports.</summary>
        public int Nodes;
    }

    /// <summary>The kinds of road, greatest first, and what each is good for. Labelled rules: no speeds are mapped on most roads.</summary>
    public static class RoadRank
    {
        public const byte None = 255;
        static readonly Dictionary<string, (byte rank, float speed)> Table = new Dictionary<string, (byte, float)>
        {
            ["motorway"] = (0, 22), ["trunk"] = (1, 16), ["primary"] = (2, 14), ["secondary"] = (3, 12), ["tertiary"] = (4, 11),
            ["motorway_link"] = (2, 11), ["trunk_link"] = (2, 10), ["primary_link"] = (3, 10), ["secondary_link"] = (4, 9), ["tertiary_link"] = (5, 8),
            ["unclassified"] = (5, 8), ["residential"] = (6, 7), ["living_street"] = (7, 5), ["service"] = (8, 4.5f),
        };

        public static bool Drivable(string cls) => cls != null && Table.ContainsKey(cls);
        public static byte Of(string cls) => cls != null && Table.TryGetValue(cls, out var v) ? v.rank : None;
        public static float Speed(string cls) => cls != null && Table.TryGetValue(cls, out var v) ? v.speed : 5;
        /// <summary>Secondary and up: the roads an officer's crossing is made of.</summary>
        public static bool Major(byte rank) => rank <= 3;
    }

    public sealed class ChunkLanes
    {
        public const double MinLane = 2.3, MinBand = 2.0, BlockClear = 0.35, CentreClear = 0.15, Sample = 1.5, GroupLink = 35, ElevatedY = 2.5;
        /// <summary>How close two connectors may pass: a bus is 2.5 m across; lanes side by side keep their own width.</summary>
        public const double Apart = 2.8, SideBySide = 1.4;
        public const double InsideLane = 20, StopBack = 1;
        public const double OverlapStep = 4, OverlapMin = 24, ParallelCos = 0.92;
        public const double StraightAngle = 40, UTurnAngle = 150, JoinTrim = 2.5, TrimMin = 3, TrimMax = 32, TrimMargin = 1, MinTurnSpeed = 2.2;
        /// <summary>How far behind the end of a cut-back lane the drawn stop line lies (RoadMarkings: pad edge, zebra, gap, line).</summary>
        public const double SignalStop = 5.3;

        public int ChunkX, ChunkY;
        public Lane[] Lanes = Array.Empty<Lane>();
        public LaneJunction[] Junctions = Array.Empty<LaneJunction>();
        public int RoadLanes, Connectors, Directions, ClosedDirections, NarrowedDirections, LanesLost, SkippedElevated, SkippedClass, Pruned, ConnectorsBlocked, OpenEnds, OpenStarts, Turnarounds, BusStops;
        public long CornerEastCm, CornerNorthCm;
        public double RoadMetres;

        sealed class Arm
        {
            public int Node;
            /// <summary>Lanes leaving the node on this arm, and lanes coming to it (each lists left kerb first).</summary>
            public List<Lane> Out = new List<Lane>(), In = new List<Lane>();
            /// <summary>Heading away from the node.</summary>
            public double Hx, Hz, Half;
            public byte Rank;
            public double Trim;
        }

        /// <summary>A road at ground level, as another road sees it.</summary>
        sealed class Neighbour
        {
            public GraphEdge Edge;
            public double[] Pts;
            public double Half, X0, Z0, X1, Z1;
        }

        List<Neighbour> ground;

        sealed class Direction
        {
            public GraphEdge Edge;
            public Road Road;
            public double[] Pts;
            public double Length;
            public int From, To;
            public double Lo, Hi;
            public int Lanes, Number;
            public Arm Start, End;
        }

        /// <summary>The lanes of c; blocks is what stands on the road (ChunkStreet.Blocks), or null.</summary>
        public static ChunkLanes Build(DadcChunk c, IReadOnlyList<ChunkStreet.Block> blocks = null)
        {
            var g = new ChunkLanes { ChunkX = c.Ix, ChunkY = c.Iy, CornerEastCm = c.CornerEastCm, CornerNorthCm = c.CornerNorthCm };
            if (c.Edges == null || c.Nodes == null) return g;
            RoadWidths.Resolve(c);
            var roads = new Dictionary<long, Road>();
            if (c.Roads != null) foreach (var r in c.Roads) roads[r.Id] = r;
            var arms = new List<Arm>[c.Nodes.Length];
            var dirs = new List<Direction>();
            var ground = new List<Neighbour>();
            foreach (var e in c.Edges)
            {
                if (!RoadRank.Drivable(e.Cls) || !roads.TryGetValue(e.Way, out var near) || e.Pts == null || e.Pts.Length < 4 || near.YCm >= ElevatedY * 100 || near.YsCm != null) continue;
                var n = new Neighbour { Edge = e, Half = Math.Max(RoadPalette.MinHalf, near.WidthCm / 200.0), Pts = new double[e.Pts.Length], X0 = double.MaxValue, Z0 = double.MaxValue, X1 = double.MinValue, Z1 = double.MinValue };
                for (int i = 0; i < n.Pts.Length; i += 2)
                {
                    n.Pts[i] = e.Pts[i] / 100.0;
                    n.Pts[i + 1] = e.Pts[i + 1] / 100.0;
                    n.X0 = Math.Min(n.X0, n.Pts[i]); n.X1 = Math.Max(n.X1, n.Pts[i]);
                    n.Z0 = Math.Min(n.Z0, n.Pts[i + 1]); n.Z1 = Math.Max(n.Z1, n.Pts[i + 1]);
                }
                ground.Add(n);
            }
            g.ground = ground;
            foreach (var e in c.Edges)
            {
                if (!RoadRank.Drivable(e.Cls)) { g.SkippedClass++; continue; }
                if (!roads.TryGetValue(e.Way, out var road) || e.Pts == null || e.Pts.Length < 4) continue;
                if ((road.Tunnel ?? 0) != 0) { g.SkippedElevated++; continue; }
                double half = Math.Max(RoadPalette.MinHalf, road.WidthCm / 200.0);
                int total = ChunkRoads.LaneCount(e.Cls, road.Lanes ?? 0, e.Oneway, half, out int centre);
                var pts = new double[e.Pts.Length];
                for (int i = 0; i < pts.Length; i++) pts[i] = e.Pts[i] / 100.0;
                var a = NewArm(arms, e.A, pts, false, half, road, e);
                var b = NewArm(arms, e.B, pts, true, half, road, e);
                // The centre line, as the markings draw it: boundary `centre` from the right of the way, on the middle of the road for an even count.
                double mid = centre > 0 ? -half + 2 * half * centre / total : 0;
                int forward = centre > 0 ? total - centre : Math.Max(1, total / 2), back = centre > 0 ? centre : forward;
                if (e.Oneway != 0) dirs.Add(g.MakeDirection(e, road, e.Oneway > 0 ? pts : Reversed(pts), e.Oneway > 0 ? e.A : e.B, e.Oneway > 0 ? e.B : e.A, e.Oneway > 0 ? a : b, e.Oneway > 0 ? b : a, -half, half, total, blocks));
                else
                {
                    dirs.Add(g.MakeDirection(e, road, pts, e.A, e.B, a, b, mid + CentreClear, half, forward, blocks));
                    dirs.Add(g.MakeDirection(e, road, Reversed(pts), e.B, e.A, b, a, CentreClear - mid, half, back, blocks));
                }
            }
            dirs.RemoveAll(d => d == null);
            var list = new List<Lane>();
            var degree = new int[c.Nodes.Length];
            for (int i = 0; i < arms.Length; i++) degree[i] = arms[i]?.Count ?? 0;
            for (int i = 0; i < arms.Length; i++) if (degree[i] >= 3) foreach (var a in arms[i]) a.Trim = TrimFor(a, arms[i]);
                else if (degree[i] == 2 && !OnBorder(c.Nodes[i])) foreach (var a in arms[i]) a.Trim = JoinTrim;
            foreach (var d in dirs) g.MakeLanes(c, d, list);
            var junctionOf = g.MakeJunctions(c, arms, degree, list);
            for (int i = 0; i < arms.Length; i++) if (arms[i] != null) g.Join(c, i, arms[i], degree[i], junctionOf, list, blocks);
            g.Prune(list);
            g.PlaceBusStops(c);
            g.ground = null;
            return g;
        }

        static bool OnBorder(GraphNode n) => n.East <= 0 || n.East >= DadcChunk.ChunkCm || n.North <= 0 || n.North >= DadcChunk.ChunkCm;

        static double[] Reversed(double[] p)
        {
            var r = new double[p.Length];
            for (int i = 0, n = p.Length / 2; i < n; i++) { r[2 * i] = p[2 * (n - 1 - i)]; r[2 * i + 1] = p[2 * (n - 1 - i) + 1]; }
            return r;
        }

        static Arm NewArm(List<Arm>[] arms, int node, double[] pts, bool fromEnd, double half, Road road, GraphEdge e)
        {
            int n = pts.Length / 2;
            int i0 = fromEnd ? n - 1 : 0;
            // The heading a few metres out, not the first short segment's.
            double sx = pts[2 * i0], sz = pts[2 * i0 + 1], hx = 0, hz = 0, gone = 0;
            for (int i = i0, step = fromEnd ? -1 : 1; i + step >= 0 && i + step < n && gone < 8; i += step)
            {
                double dx = pts[2 * (i + step)] - pts[2 * i], dz = pts[2 * (i + step) + 1] - pts[2 * i + 1];
                gone += Math.Sqrt(dx * dx + dz * dz);
                hx = pts[2 * (i + step)] - sx;
                hz = pts[2 * (i + step) + 1] - sz;
            }
            double d = Math.Sqrt(hx * hx + hz * hz);
            var a = new Arm { Node = node, Hx = d > 0 ? hx / d : 1, Hz = d > 0 ? hz / d : 0, Half = half, Rank = RoadRank.Of(e.Cls) };
            (arms[node] ??= new List<Arm>(4)).Add(a);
            return a;
        }

        /// <summary>How far along arm a its edges stay inside the other arms, plus a margin.</summary>
        static double TrimFor(Arm a, List<Arm> all)
        {
            double trim = TrimMin;
            foreach (var o in all)
            {
                if (o == a) continue;
                double cos = a.Hx * o.Hx + a.Hz * o.Hz, sin = Math.Abs(a.Hx * o.Hz - a.Hz * o.Hx);
                if (sin < 0.2 && cos < 0) continue; // straight on: no corner to clear
                // Two arms leaving at a narrow angle run side by side a long way: the merge is part of the junction.
                trim = Math.Max(trim, (o.Half + a.Half * Math.Abs(cos)) / Math.Max(sin, 0.1) + TrimMargin);
            }
            return Math.Min(trim, TrimMax);
        }

        Direction MakeDirection(GraphEdge e, Road road, double[] pts, int from, int to, Arm start, Arm end, double lo, double hi, int lanes, IReadOnlyList<ChunkStreet.Block> blocks)
        {
            Directions++;
            double len = 0;
            for (int i = 2; i < pts.Length; i += 2) len += Math.Sqrt((pts[i] - pts[i - 2]) * (pts[i] - pts[i - 2]) + (pts[i + 1] - pts[i - 1]) * (pts[i + 1] - pts[i - 1]));
            // [lo, hi] is measured left of travel. A narrow road keeps its lanes however thin they are: Lane.Width says what fits.
            // A deck or a ramp is clear of what stands below it and of the roads beside it.
            bool raised = road.YCm >= ElevatedY * 100 || road.YsCm != null;
            bool narrowed = !raised && blocks != null && Narrow(pts, blocks, ref lo, ref hi);
            if (!raised) narrowed |= Overlaps(e, pts, Math.Max(RoadPalette.MinHalf, road.WidthCm / 200.0), len, ref lo, ref hi);
            double band = hi - lo;
            if (narrowed)
            {
                if (band < MinBand) { ClosedDirections++; return null; }
                NarrowedDirections++;
                int fit = Math.Max(1, (int)Math.Floor(band / MinLane));
                if (fit < lanes) { LanesLost += lanes - fit; lanes = fit; }
            }
            return new Direction { Number = Directions, Edge = e, Road = road, Pts = pts, Length = len, From = from, To = to, Lo = lo, Hi = hi, Lanes = lanes, Start = start, End = end };
        }

        /// <summary>
        /// Two ways drawn over each other for a stretch (K-31: estimated widths, a link beside its road): each gives up its share of the overlap,
        /// so traffic on one does not run through traffic on the other. The road surface and the data stay as they are; only the lanes move.
        /// </summary>
        bool Overlaps(GraphEdge e, double[] pts, double half, double len, ref double lo, ref double hi)
        {
            bool changed = false;
            double minX = double.MaxValue, maxX = double.MinValue, minZ = double.MaxValue, maxZ = double.MinValue;
            for (int i = 0; i < pts.Length; i += 2)
            {
                minX = Math.Min(minX, pts[i]); maxX = Math.Max(maxX, pts[i]);
                minZ = Math.Min(minZ, pts[i + 1]); maxZ = Math.Max(maxZ, pts[i + 1]);
            }
            foreach (var o in ground)
            {
                if (o.Edge == e) continue;
                double pad = half + o.Half;
                if (o.X1 + pad < minX || o.X0 - pad > maxX || o.Z1 + pad < minZ || o.Z0 - pad > maxZ) continue;
                bool shared = o.Edge.A == e.A || o.Edge.A == e.B || o.Edge.B == e.A || o.Edge.B == e.B;
                int leftN = 0, rightN = 0;
                double leftSum = 0, rightSum = 0, gone = 0, next = 0;
                for (int i = 2; i < pts.Length; i += 2)
                {
                    double ax = pts[i - 2], az = pts[i - 1], dx = pts[i] - ax, dz = pts[i + 1] - az, seg = Math.Sqrt(dx * dx + dz * dz);
                    if (seg < 1e-9) continue;
                    dx /= seg;
                    dz /= seg;
                    for (; next <= gone + seg; next += OverlapStep)
                    {
                        // Where the two part at a shared node, the junction's cut-back deals with it.
                        if (shared && (next < TrimMax || next > len - TrimMax)) continue;
                        double t = next - gone, x = ax + dx * t, z = az + dz * t;
                        if (!Beside(o.Pts, x, z, dx, dz, out double lat) || Math.Abs(lat) >= half + o.Half - 0.2) continue;
                        if (lat > 0) { leftN++; leftSum += lat; } else { rightN++; rightSum += lat; }
                    }
                    gone += seg;
                }
                if (leftN * OverlapStep >= OverlapMin)
                {
                    double limit = Share(half, o.Half, leftSum / leftN);
                    if (limit < hi) { hi = limit; changed = true; }
                }
                if (rightN * OverlapStep >= OverlapMin)
                {
                    double limit = Share(half, o.Half, -rightSum / rightN);
                    if (-limit > lo) { lo = -limit; changed = true; }
                }
            }
            return changed;
        }

        /// <summary>How far this road's half keeps out towards a neighbour `apart` metres off, the overlap shared by width.</summary>
        static double Share(double half, double other, double apart) => half - (half + other - apart) * half / (half + other);

        /// <summary>Does the line pts run alongside the point (x, z), heading (dx, dz) either way? And how far to the left of that heading.</summary>
        static bool Beside(double[] pts, double x, double z, double dx, double dz, out double lat)
        {
            double best = double.MaxValue;
            lat = 0;
            bool found = false;
            for (int i = 2; i < pts.Length; i += 2)
            {
                double ax = pts[i - 2], az = pts[i - 1], ex = pts[i] - ax, ez = pts[i + 1] - az, len2 = ex * ex + ez * ez;
                if (len2 < 1e-9) continue;
                double t = ((x - ax) * ex + (z - az) * ez) / len2;
                if (t < 0 || t > 1) continue;
                double len = Math.Sqrt(len2);
                if (Math.Abs(ex * dx + ez * dz) / len < ParallelCos) continue;
                double px = ax + ex * t - x, pz = az + ez * t - z, side = dx * pz - dz * px;
                if (Math.Abs(side) < best) { best = Math.Abs(side); lat = side; found = true; }
            }
            return found;
        }

        /// <summary>Shrinks [lo, hi] (metres left of the line pts) clear of every block, keeping the wider free side; true if it changed.</summary>
        static bool Narrow(double[] pts, IReadOnlyList<ChunkStreet.Block> blocks, ref double lo, ref double hi)
        {
            bool changed = false;
            double minX = double.MaxValue, maxX = double.MinValue, minZ = double.MaxValue, maxZ = double.MinValue;
            for (int i = 0; i < pts.Length; i += 2)
            {
                minX = Math.Min(minX, pts[i]); maxX = Math.Max(maxX, pts[i]);
                minZ = Math.Min(minZ, pts[i + 1]); maxZ = Math.Max(maxZ, pts[i + 1]);
            }
            double pad = Math.Max(Math.Abs(lo), Math.Abs(hi)) + 8;
            for (int b = 0; b < blocks.Count; b++)
            {
                var k = blocks[b];
                double reach = Math.Max(k.Along, k.Half);
                if (k.X + reach < minX - pad || k.X - reach > maxX + pad || k.Z + reach < minZ - pad || k.Z - reach > maxZ + pad) continue;
                double l0 = double.MaxValue, l1 = double.MinValue;
                bool beside = false;
                // Sample along the block: a long median part may lie beside only part of this edge.
                int steps = Math.Max(1, (int)Math.Ceiling(2 * k.Along / 2));
                for (int si = 0; si <= steps; si++)
                {
                    double a = -k.Along + 2 * k.Along * si / steps;
                    for (int side = -1; side <= 1; side += 2)
                    {
                        double x = k.X + k.Ux * a - k.Uz * k.Half * side, z = k.Z + k.Uz * a + k.Ux * k.Half * side;
                        if (!Lateral(pts, x, z, out double l)) continue;
                        beside = true;
                        l0 = Math.Min(l0, l);
                        l1 = Math.Max(l1, l);
                    }
                }
                if (!beside) continue;
                l0 -= BlockClear;
                l1 += BlockClear;
                if (l1 <= lo || l0 >= hi) continue;
                changed = true;
                double left = hi - Math.Max(l1, lo), right = Math.Min(l0, hi) - lo;
                if (left >= right) lo = Math.Max(l1, lo); else hi = Math.Min(l0, hi);
            }
            return changed;
        }

        /// <summary>The signed distance left of the line, if (x, z) lies beside one of its segments.</summary>
        static bool Lateral(double[] pts, double x, double z, out double l)
        {
            double best = double.MaxValue;
            l = 0;
            bool found = false;
            for (int i = 2; i < pts.Length; i += 2)
            {
                double ax = pts[i - 2], az = pts[i - 1], dx = pts[i] - ax, dz = pts[i + 1] - az, len2 = dx * dx + dz * dz;
                if (len2 < 1e-9) continue;
                double t = ((x - ax) * dx + (z - az) * dz) / len2;
                if (t < 0 || t > 1) continue;
                double len = Math.Sqrt(len2), side = (dx * (z - az) - dz * (x - ax)) / len;
                if (Math.Abs(side) < best) { best = Math.Abs(side); l = side; found = true; }
            }
            return found;
        }

        void MakeLanes(DadcChunk c, Direction d, List<Lane> list)
        {
            double lw = (d.Hi - d.Lo) / d.Lanes;
            int first = list.Count;
            var road = d.Road;
            float speed = road.MaxSpeedCm is int kmh && kmh > 0 ? kmh / 360f : RoadRank.Speed(d.Edge.Cls);
            double trim0 = d.Start.Trim, trim1 = d.End.Trim;
            // A short link between two junctions: leave it something to drive on.
            if (trim0 + trim1 > 0.8 * d.Length) { double k = 0.8 * d.Length / (trim0 + trim1); trim0 *= k; trim1 *= k; }
            var startNode = c.Nodes[d.From];
            var endNode = c.Nodes[d.To];
            for (int i = 0; i < d.Lanes; i++)
            {
                double off = d.Hi - (i + 0.5) * lw;
                var pts = Trimmed(Offset(d.Pts, off), trim0, trim1, out var arc, out double len);
                var lane = new Lane
                {
                    Id = list.Count, Way = d.Edge.Way, Rank = RoadRank.Of(d.Edge.Cls), Index = (byte)i, Count = (byte)d.Lanes, Pts = pts, Arc = arc, Length = len,
                    Y = road.YCm / 100f, Width = (float)lw, Speed = speed, StopS = len, Direction = d.Number,
                    OpenStart = OnBorder(startNode) && d.Start.Trim == 0, OpenEnd = OnBorder(endNode) && d.End.Trim == 0,
                };
                if (road.YsCm != null)
                {
                    var ramp = new ChunkRoads.Ramp(road);
                    lane.Ys = new float[pts.Length / 2];
                    for (int k = 0; k < lane.Ys.Length; k++) lane.Ys[k] = (float)ramp.HeightAt(pts[2 * k], pts[2 * k + 1]);
                }
                if (i > 0) { lane.Left = first + i - 1; list[first + i - 1].Right = lane.Id; }
                list.Add(lane);
                d.Start.Out.Add(lane);
                d.End.In.Add(lane);
                RoadLanes++;
                RoadMetres += len;
            }
        }

        static double[] Offset(double[] p, double off)
        {
            int n = p.Length / 2;
            var r = new List<double>(p.Length);
            double px = 0, pz = 0, lastDx = 0, lastDz = 0;
            for (int i = 0; i < n; i++)
            {
                double ax = p[2 * Math.Max(i - 1, 0)], az = p[2 * Math.Max(i - 1, 0) + 1], bx = p[2 * i], bz = p[2 * i + 1], cx = p[2 * Math.Min(i + 1, n - 1)], cz = p[2 * Math.Min(i + 1, n - 1) + 1];
                double d1x = bx - ax, d1z = bz - az, d2x = cx - bx, d2z = cz - bz;
                double l1 = Math.Sqrt(d1x * d1x + d1z * d1z), l2 = Math.Sqrt(d2x * d2x + d2z * d2z);
                if (l1 > 1e-9) { d1x /= l1; d1z /= l1; } else { d1x = d2x / Math.Max(l2, 1e-9); d1z = d2z / Math.Max(l2, 1e-9); }
                if (l2 > 1e-9) { d2x /= l2; d2z /= l2; } else { d2x = d1x; d2z = d1z; }
                double tx = d1x + d2x, tz = d1z + d2z, t = Math.Sqrt(tx * tx + tz * tz);
                if (t < 1e-6) { tx = d2x; tz = d2z; t = 1; }
                tx /= t;
                tz /= t;
                double cosHalf = tx * d2x + tz * d2z, scale = Math.Min(1 / Math.Max(cosHalf, 0.5), 1.6);
                double x = bx - tz * off * scale, z = bz + tx * off * scale;
                // Inside a tight bend the offset line can fold back: a point behind the last one is dropped.
                if (r.Count >= 2)
                {
                    double sx = x - px, sz = z - pz;
                    if (sx * lastDx + sz * lastDz <= 0 && i < n - 1) continue;
                    if (sx * sx + sz * sz < 1e-6) continue;
                    lastDx = sx;
                    lastDz = sz;
                }
                else { lastDx = d2x; lastDz = d2z; }
                r.Add(x);
                r.Add(z);
                px = x;
                pz = z;
            }
            return r.ToArray();
        }

        static double[] Trimmed(double[] p, double t0, double t1, out double[] arc, out double length)
        {
            int n = p.Length / 2;
            var full = new double[n];
            for (int i = 1; i < n; i++) full[i] = full[i - 1] + Math.Sqrt((p[2 * i] - p[2 * i - 2]) * (p[2 * i] - p[2 * i - 2]) + (p[2 * i + 1] - p[2 * i - 1]) * (p[2 * i + 1] - p[2 * i - 1]));
            double total = full[n - 1], a = Math.Min(t0, total * 0.45), b = Math.Max(total - t1, total * 0.55);
            var r = new List<double>(p.Length);
            r.AddRange(PointAt(p, full, a));
            for (int i = 0; i < n; i++) if (full[i] > a + 0.01 && full[i] < b - 0.01) { r.Add(p[2 * i]); r.Add(p[2 * i + 1]); }
            r.AddRange(PointAt(p, full, b));
            var pts = r.ToArray();
            arc = ArcOf(pts);
            length = arc[arc.Length - 1];
            return pts;
        }

        static double[] PointAt(double[] p, double[] full, double s)
        {
            int i = 1;
            while (i < full.Length - 1 && full[i] < s) i++;
            double len = full[i] - full[i - 1], t = len > 1e-9 ? (s - full[i - 1]) / len : 0;
            return new[] { p[2 * i - 2] + (p[2 * i] - p[2 * i - 2]) * t, p[2 * i - 1] + (p[2 * i + 1] - p[2 * i - 1]) * t };
        }

        static double[] ArcOf(double[] p)
        {
            var a = new double[p.Length / 2];
            for (int i = 1; i < a.Length; i++) a[i] = a[i - 1] + Math.Sqrt((p[2 * i] - p[2 * i - 2]) * (p[2 * i] - p[2 * i - 2]) + (p[2 * i + 1] - p[2 * i - 1]) * (p[2 * i + 1] - p[2 * i - 1]));
            return a;
        }

        // ---- Junctions ----

        /// <summary>Groups controlled nodes into junctions and gives every node with three arms or more its junction; the result is by graph node, -1 for none.</summary>
        int[] MakeJunctions(DadcChunk c, List<Arm>[] arms, int[] degree, List<Lane> list)
        {
            int n = c.Nodes.Length;
            var parent = new int[n];
            for (int i = 0; i < n; i++) parent[i] = i;
            int Find(int a) { while (parent[a] != a) { parent[a] = parent[parent[a]]; a = parent[a]; } return a; }
            foreach (var e in c.Edges)
            {
                if (!RoadRank.Drivable(e.Cls) || degree[e.A] < 3 || degree[e.B] < 3 || e.Pts == null) continue;
                double len = 0;
                for (int i = 2; i < e.Pts.Length; i += 2) len += Math.Sqrt(Math.Pow(e.Pts[i] - e.Pts[i - 2], 2) + Math.Pow(e.Pts[i + 1] - e.Pts[i - 1], 2)) / 100;
                if (len <= GroupLink) parent[Find(e.A)] = Find(e.B);
            }
            var ofGroup = new Dictionary<int, List<int>>();
            for (int i = 0; i < n; i++)
            {
                if (degree[i] < 3) continue;
                int r = Find(i);
                if (!ofGroup.TryGetValue(r, out var l)) ofGroup[r] = l = new List<int>(4);
                l.Add(i);
            }
            var junctionOf = new int[n];
            for (int i = 0; i < n; i++) junctionOf[i] = -1;
            var made = new List<LaneJunction>();
            var roots = new List<int>(ofGroup.Keys);
            roots.Sort();
            foreach (int root in roots)
            {
                var nodes = ofGroup[root];
                bool signal = false;
                int major = 0, axes = 0, rank0 = 255, rank1 = 255;
                double sx = 0, sz = 0;
                foreach (int i in nodes)
                {
                    signal |= (c.Nodes[i].Signal ?? 0) != 0;
                    sx += c.Nodes[i].East / 100.0;
                    sz += c.Nodes[i].North / 100.0;
                    foreach (var a in arms[i]) if (RoadRank.Major(a.Rank)) major++;
                }
                int worst = 0;
                foreach (int i in nodes) foreach (var a in arms[i]) { worst = Math.Max(worst, a.Rank); if (a.Rank < rank0) { rank1 = rank0; rank0 = a.Rank; } else if (a.Rank < rank1) rank1 = a.Rank; }
                axes = CountAxes(nodes, arms);
                JunctionKind kind = signal ? JunctionKind.Signal : major >= 3 && axes >= 2 && RoadRank.Major((byte)Math.Min(rank1, 255)) ? JunctionKind.Officer : worst == rank0 ? JunctionKind.AllWay : JunctionKind.Priority;
                if (kind < JunctionKind.Officer && nodes.Count > 1)
                {
                    // An uncontrolled group is only grouped for control: each node runs on its own.
                    foreach (int i in nodes) { junctionOf[i] = made.Count; made.Add(NewJunction(made.Count, RankKind(arms[i]), c.Nodes[i].East / 100.0, c.Nodes[i].North / 100.0, 1)); }
                    continue;
                }
                var j = NewJunction(made.Count, kind, sx / nodes.Count, sz / nodes.Count, nodes.Count);
                if (kind >= JunctionKind.Officer)
                {
                    j.Phases = 2;
                    j.Green = kind == JunctionKind.Signal ? 26 : 24;
                    foreach (int i in nodes)
                        foreach (var a in arms[i])
                            if (a.Rank == rank0) { j.AxisX = a.Hx; j.AxisZ = a.Hz; goto axisFound; }
                    axisFound:;
                }
                foreach (int i in nodes) junctionOf[i] = j.Id;
                made.Add(j);
            }
            Junctions = made.ToArray();
            return junctionOf;
        }

        static JunctionKind RankKind(List<Arm> arms)
        {
            // A through road keeps its way over anything lesser joining it; roads of one rank take turns.
            int best = 255, worst = 0;
            foreach (var a in arms) { best = Math.Min(best, a.Rank); worst = Math.Max(worst, a.Rank); }
            return best == worst ? JunctionKind.AllWay : JunctionKind.Priority;
        }

        static LaneJunction NewJunction(int id, JunctionKind kind, double x, double z, int nodes) => new LaneJunction { Id = id, Kind = kind, X = x, Z = z, Nodes = nodes };

        /// <summary>How many different road directions meet: arms in line count as one axis.</summary>
        static int CountAxes(List<int> nodes, List<Arm>[] arms)
        {
            var axes = new List<(double x, double z)>(4);
            foreach (int i in nodes)
                foreach (var a in arms[i])
                {
                    if (!RoadRank.Major(a.Rank)) continue;
                    bool known = false;
                    foreach (var (x, z) in axes) if (Math.Abs(a.Hx * x + a.Hz * z) > 0.8) { known = true; break; }
                    if (!known) axes.Add((a.Hx, a.Hz));
                }
            return axes.Count;
        }

        void Join(DadcChunk c, int node, List<Arm> arms, int degree, int[] junctionOf, List<Lane> list, IReadOnlyList<ChunkStreet.Block> blocks)
        {
            var nd = c.Nodes[node];
            long east = c.CornerEastCm + nd.East, north = c.CornerNorthCm + nd.North;
            foreach (var a in arms)
            {
                foreach (var l in a.In) if (l.OpenEnd) { l.NodeEast = east; l.NodeNorth = north; OpenEnds++; }
                foreach (var l in a.Out) if (l.OpenStart) { l.NodeEast = east; l.NodeNorth = north; OpenStarts++; }
            }
            if (degree == 1)
            {
                var only = arms[0];
                if (OnBorder(nd) || only.In.Count == 0 || only.Out.Count == 0) return;
                // A dead end on a two-way road: traffic turns round.
                Connect(only.In[only.In.Count - 1], only.Out[only.Out.Count - 1], list, -1, blocks);
                Turnarounds++;
                return;
            }
            int first = list.Count;
            int junction = junctionOf[node];
            if (degree == 2 && OnBorder(nd))
            {
                // Two arms meeting on the border, both in this chunk: an ordinary join.
                foreach (var a in arms) { foreach (var l in a.In) if (l.OpenEnd) { l.OpenEnd = false; OpenEnds--; } foreach (var l in a.Out) if (l.OpenStart) { l.OpenStart = false; OpenStarts--; } }
            }
            foreach (var a in arms)
            {
                if (a.In.Count == 0) continue;
                // The way a vehicle on this arm is heading as it arrives is back along the arm's heading.
                double ix = -a.Hx, iz = -a.Hz;
                Arm straight = null;
                double bestAngle = StraightAngle;
                foreach (var b in arms)
                {
                    if (b == a || b.Out.Count == 0) continue;
                    double ang = Math.Abs(Angle(ix, iz, b.Hx, b.Hz));
                    if (ang < bestAngle) { bestAngle = ang; straight = b; }
                }
                foreach (var b in arms)
                {
                    if (b == a || b.Out.Count == 0) continue;
                    double ang = Angle(ix, iz, b.Hx, b.Hz);
                    if (Math.Abs(ang) > UTurnAngle && degree > 2) continue;
                    int k = a.In.Count, m = b.Out.Count;
                    if (b == straight || degree == 2)
                    {
                        for (int i = 0; i < k; i++) Connect(a.In[i], b.Out[Math.Min(i, m - 1)], list, junction, blocks);
                        for (int j = k; j < m; j++) Connect(a.In[k - 1], b.Out[j], list, junction, blocks);
                    }
                    else if (ang > 0) Connect(a.In[0], b.Out[0], list, junction, blocks);
                    else Connect(a.In[k - 1], b.Out[m - 1], list, junction, blocks);
                }
            }
            if (junction >= 0)
            {
                Control(c, node, arms, junction, junctionOf);
                FindConflicts(list, first);
            }
        }

        static double Angle(double ax, double az, double bx, double bz) => Math.Atan2(ax * bz - az * bx, ax * bx + az * bz) * 180 / Math.PI;

        /// <summary>Sets who waits for what on the arms into this node.</summary>
        void Control(DadcChunk c, int node, List<Arm> arms, int junction, int[] junctionOf)
        {
            var j = Junctions[junction];
            int best = 255;
            foreach (var a in arms) if (a.In.Count > 0 && a.Rank < best) best = a.Rank;
            foreach (var a in arms)
                foreach (var l in a.In)
                {
                    l.Junction = junction;
                    if (j.Kind >= JunctionKind.Officer)
                    {
                        // A link inside the group is always let clear, and so is any lane too short for a queue: nobody waits for a phase in the middle of a junction.
                        bool inside = l.Length < InsideLane || LinkInside(c, a, node, junctionOf, junction);
                        l.Phase = inside ? (sbyte)-1 : Math.Abs(a.Hx * j.AxisX + a.Hz * j.AxisZ) >= 0.7071 ? (sbyte)0 : (sbyte)1;
                        if (!inside && j.Kind == JunctionKind.Signal) l.StopS = Math.Max(l.Length - SignalStop, l.Length * 0.5);
                    }
                    else
                    {
                        l.Yield = j.Kind == JunctionKind.Priority && a.Rank > best;
                        // Nose back from the crossing traffic.
                        l.StopS = Math.Max(l.Length - StopBack, l.Length * 0.5);
                    }
                }
        }

        /// <summary>Does this arm come from another node of the same junction, along a short link?</summary>
        bool LinkInside(DadcChunk c, Arm a, int node, int[] junctionOf, int junction)
        {
            foreach (var e in c.Edges)
            {
                if (e.A != node && e.B != node) continue;
                int other = e.A == node ? e.B : e.A;
                if (other == node || junctionOf[other] != junction || e.Pts == null || e.Pts.Length < 4) continue;
                bool fromB = e.A != node;
                int i = fromB ? e.Pts.Length - 2 : 0, k = fromB ? e.Pts.Length - 4 : 2;
                double dx = e.Pts[k] - e.Pts[i], dz = e.Pts[k + 1] - e.Pts[i + 1], d = Math.Sqrt(dx * dx + dz * dz);
                if (d > 0 && (dx / d) * a.Hx + (dz / d) * a.Hz > 0.7) return true;
            }
            return false;
        }

        void Connect(Lane from, Lane to, List<Lane> list, int junction, IReadOnlyList<ChunkStreet.Block> blocks)
        {
            int n = from.Pts.Length;
            double ax = from.Pts[n - 2], az = from.Pts[n - 1], bx = to.Pts[0], bz = to.Pts[1];
            double gap = Math.Sqrt((bx - ax) * (bx - ax) + (bz - az) * (bz - az));
            if (gap < 0.05 && from.Rank == to.Rank) { Link(from, to); return; }
            from.At(from.Length, out _, out _, out double dx, out double dz);
            to.At(0, out _, out _, out double ex, out double ez);
            double h = gap * 0.4;
            int steps = Math.Max(2, (int)Math.Ceiling(gap / Sample));
            var pts = new double[2 * (steps + 1)];
            double c1x = ax + dx * h, c1z = az + dz * h, c2x = bx - ex * h, c2z = bz - ez * h;
            for (int i = 0; i <= steps; i++)
            {
                double t = (double)i / steps, u = 1 - t, b0 = u * u * u, b1 = 3 * u * u * t, b2 = 3 * u * t * t, b3 = t * t * t;
                pts[2 * i] = b0 * ax + b1 * c1x + b2 * c2x + b3 * bx;
                pts[2 * i + 1] = b0 * az + b1 * c1z + b2 * c2z + b3 * bz;
            }
            if (blocks != null && HitsBlock(pts, blocks)) { ConnectorsBlocked++; return; }
            var arc = ArcOf(pts);
            var lane = new Lane
            {
                Id = list.Count, Connector = true, Way = to.Way, Rank = to.Rank, Index = to.Index, Count = to.Count, Pts = pts, Arc = arc, Length = arc[arc.Length - 1],
                Y = Math.Max(from.Y, to.Y), Width = Math.Min(from.Width, to.Width), Junction = junction, Direction = from.Direction,
            };
            double signed = Angle(dx, dz, ex, ez);
            lane.Turn = (sbyte)(Math.Abs(signed) < 30 ? 0 : signed > 0 ? 1 : -1);
            // As fast as the bend allows: a chord and the angle turned through give its radius, and 2.5 m/s2 across is a fair corner.
            double turn = Math.Abs(signed) * Math.PI / 180, radius = turn > 0.05 ? gap / (2 * Math.Sin(Math.Min(turn, 3.0) / 2)) : 1e3;
            lane.Speed = (float)Math.Max(MinTurnSpeed, Math.Min(Math.Min(from.Speed, to.Speed), Math.Sqrt(2.5 * radius)));
            lane.StopS = lane.Length;
            list.Add(lane);
            Connectors++;
            Link(from, lane);
            Link(lane, to);
        }

        static bool HitsBlock(double[] pts, IReadOnlyList<ChunkStreet.Block> blocks)
        {
            for (int b = 0; b < blocks.Count; b++)
            {
                var k = blocks[b];
                for (int i = 0; i < pts.Length; i += 2)
                {
                    double ex = pts[i] - k.X, ez = pts[i + 1] - k.Z;
                    if (Math.Abs(ex * k.Ux + ez * k.Uz) < k.Along + 0.9 && Math.Abs(-ex * k.Uz + ez * k.Ux) < k.Half + 0.9) return true;
                }
            }
            return false;
        }

        static void Link(Lane from, Lane to)
        {
            from.Next = Append(from.Next, to.Id);
            to.Prev = Append(to.Prev, from.Id);
        }

        static int[] Append(int[] a, int v)
        {
            var r = new int[a.Length + 1];
            Array.Copy(a, r, a.Length);
            r[a.Length] = v;
            return r;
        }

        /// <summary>Which of the connectors from first on cross or merge (not those leaving the same lane).</summary>
        static void FindConflicts(List<Lane> list, int first)
        {
            for (int i = first; i < list.Count; i++)
            {
                var a = list[i];
                if (!a.Connector) continue;
                for (int j = i + 1; j < list.Count; j++)
                {
                    var b = list[j];
                    if (!b.Connector || a.Prev[0] == b.Prev[0]) continue;
                    if (a.Next[0] == b.Next[0] || Cross(a.Pts, b.Pts, a.Direction == b.Direction ? SideBySide : Apart))
                    {
                        a.Conflicts = Append(a.Conflicts, b.Id);
                        b.Conflicts = Append(b.Conflicts, a.Id);
                    }
                }
            }
        }

        /// <summary>Do two polylines come within a vehicle's width of each other?</summary>
        static bool Cross(double[] a, double[] b, double near)
        {
            double Near2 = near * near;
            for (int i = 0; i < a.Length; i += 2)
                for (int j = 0; j < b.Length; j += 2)
                {
                    double dx = a[i] - b[j], dz = a[i + 1] - b[j + 1];
                    if (dx * dx + dz * dz < Near2) return true;
                }
            return false;
        }

        // ---- Finishing ----

        /// <summary>Removes lanes that lead nowhere (not those left open to a neighbour), and numbers the rest.</summary>
        void Prune(List<Lane> list)
        {
            var dead = new bool[list.Count];
            bool any = true;
            while (any)
            {
                any = false;
                for (int i = 0; i < list.Count; i++)
                {
                    var l = list[i];
                    if (dead[i] || l.OpenEnd) continue;
                    bool leads = false;
                    foreach (int n in l.Next) if (!dead[n]) { leads = true; break; }
                    if (leads) continue;
                    dead[i] = true;
                    any = true;
                    Pruned++;
                }
            }
            var map = new int[list.Count];
            var kept = new List<Lane>(list.Count);
            for (int i = 0; i < list.Count; i++)
            {
                map[i] = -1;
                if (dead[i]) continue;
                map[i] = kept.Count;
                kept.Add(list[i]);
            }
            foreach (var l in kept)
            {
                l.Id = map[l.Id];
                l.Next = Remap(l.Next, map);
                l.Prev = Remap(l.Prev, map);
                l.Conflicts = Remap(l.Conflicts, map);
                l.Left = l.Left >= 0 ? map[l.Left] : -1;
                l.Right = l.Right >= 0 ? map[l.Right] : -1;
            }
            Lanes = kept.ToArray();
            RoadLanes = 0;
            Connectors = 0;
            RoadMetres = 0;
            foreach (var l in Lanes)
            {
                if (l.Connector) { Connectors++; continue; }
                RoadLanes++;
                RoadMetres += l.Length;
            }
        }

        static int[] Remap(int[] a, int[] map)
        {
            int n = 0;
            foreach (int v in a) if (map[v] >= 0) n++;
            if (n == 0) return Array.Empty<int>();
            var r = new int[n];
            n = 0;
            foreach (int v in a) if (map[v] >= 0) r[n++] = map[v];
            return r;
        }

        /// <summary>Each mapped bus stop goes on the nearest kerb lane within reach (one lane each way may have it).</summary>
        void PlaceBusStops(DadcChunk c)
        {
            if (c.Points == null) return;
            const double Reach = 16;
            foreach (var p in c.Points)
            {
                if (p.Kind != "bus_stop") continue;
                double x = p.East / 100.0, z = p.North / 100.0, best = Reach, bestS = 0;
                Lane found = null;
                foreach (var l in Lanes)
                {
                    if (l.Connector || l.Index != 0 || l.Rank > 6) continue;
                    for (int i = 2; i < l.Pts.Length; i += 2)
                    {
                        double ax = l.Pts[i - 2], az = l.Pts[i - 1], dx = l.Pts[i] - ax, dz = l.Pts[i + 1] - az, len2 = dx * dx + dz * dz;
                        if (len2 < 1e-9) continue;
                        double t = Math.Max(0, Math.Min(1, ((x - ax) * dx + (z - az) * dz) / len2));
                        double ex = ax + dx * t - x, ez = az + dz * t - z, d = Math.Sqrt(ex * ex + ez * ez);
                        // The stop must be on this lane's left: its kerb.
                        if (d < best && dx * (z - az) - dz * (x - ax) >= 0)
                        {
                            best = d;
                            found = l;
                            bestS = l.Arc[i / 2 - 1] + t * (l.Arc[i / 2] - l.Arc[i / 2 - 1]);
                        }
                    }
                }
                if (found == null) continue;
                if (bestS < 6 || bestS > found.StopS - 6) continue;
                found.BusStops = AppendD(found.BusStops, bestS);
                BusStops++;
            }
        }

        static double[] AppendD(double[] a, double v)
        {
            var r = new double[a.Length + 1];
            Array.Copy(a, r, a.Length);
            r[a.Length] = v;
            return r;
        }
    }
}
