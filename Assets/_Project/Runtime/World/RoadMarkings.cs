// Road markings and street details (D-04), added to the road mesh by ChunkRoads.Build, so the
// streaming and apply steps are unchanged. All of it follows the carriageways as drawn (after
// chaining, stem trimming and fillets):
// - lines on marked roads (RoadPalette.IsMajor, not foot or construction ways): lane lines and,
//   on two-way roads, a centre line, from the lanes tag (capped where it would make lanes under
//   2.5 m) or, untagged, from the width (lanes of at least 3 m; two-way roads under 4.5 m get none);
//   edge lines on roads 7 m or wider. One-way comes from the road graph. The centre line is
//   continuous on four lanes or more, dashed below; lane lines are dashed, 3 m on and 4.5 m off,
//   counted along the road from where it crosses the chunk border, so they carry on across it (see
//   Parts). Lines are mitred at bends and stop at junction pads, zebra crossings and stop lines,
//   and where they would run onto another carriageway of equal or higher class rank at the same
//   level (overlapping dual carriageways and links; see CutByCarriageways);
// - zebra crossings where a mapped crossing is marked (crossing = uncontrolled, zebra,
//   traffic_signals or marked), across the road it lies on;
// - at signalled junctions, on each approach, a zebra crossing just outside the junction pad and a
//   stop line 1 m behind it, across the inbound half (traffic keeps left) or, on a one-way road
//   into the junction, its whole width. Links between two signals of one junction get neither, and
//   the box they bound carries no lines or details;
// - by rule, because the data maps none of them (labelled as rules, not data): speed breakers on
//   residential, living-street, service and unclassified roads, about one in 90 m; manhole covers in
//   a lane of marked roads, about one in 55 m; gully grates along both edges of roads 6 m or wider,
//   about one in 30 m; potholes on minor roads, about one in 75 m. Each is counted along the road as
//   dashes are, offset by a hash of its road's id, kept 12 m from road ends inside the chunk, half its
//   spacing from the last one on the road and 1 m from the chunk border, and drawn by the chunk that
//   holds it, so neighbouring chunks agree.
// White lines, 0.12 m lane and centre lines, 0.15 m edge lines, 0.3 m stop lines and 0.5 m zebra
// bars follow common South Asian practice (IRC 35). They are not yet checked against the BRTA
// Traffic Signs Manual's road-marking schedule (KNOWN_ISSUES K-29).
using System;
using System.Collections.Generic;

namespace Dad.World
{
    public enum MarkKind : byte { LaneLine, CentreLine, EdgeLine, Zebra, StopLine, SpeedBreaker, Manhole, Grate, Pothole }

    /// <summary>One marking or detail, kept for tests: a strip from A to B, Width wide (a disc's diameter when A is B), metres from the chunk corner.</summary>
    public struct RoadMark
    {
        public MarkKind Kind;
        public long Way;
        public double Ax, Az, Bx, Bz, Width, Y;
        /// <summary>Lines: true when dashed.</summary>
        public bool Dashed;
    }

    public sealed partial class ChunkRoads
    {
        public const double MarkLift = 0.02, LaneLineWidth = 0.12, EdgeLineWidth = 0.15, EdgeInset = 0.3, EdgeLinesFrom = 3.5;
        public const double DashOn = 3, DashPeriod = 7.5, LaneWidth = 3, MinLane = 2.5, NoCentreBelow = 4.5;
        public const double ZebraDepth = 3, ZebraBar = 0.5, ZebraGap = 0.5, ZebraMargin = 0.2, StopLineWidth = 0.3, StopBehind = 1;
        public const double BreakerPeriod = 90, ManholePeriod = 55, GratePeriod = 30, PotholePeriod = 37;
        public const double BreakerLength = 1.2, BreakerHeight = 0.075, DetailEndClear = 12;
        public const double OverlapShrink = 0.1, OverlapExtend = 0.5;
        public const uint Paint = 0xd6d2c4, BreakerDark = 0x2e2d2b, ManholeIron = 0x4a4844, GrateIron = 0x34322f, PotholeMud = 0x3d362f;

        public static readonly HashSet<string> MarkedCrossings = new HashSet<string> { "uncontrolled", "zebra", "traffic_signals", "marked" };
        public static readonly HashSet<string> BreakerClasses = new HashSet<string> { "residential", "living_street", "service", "unclassified" };
        public static readonly HashSet<string> PotholeClasses = new HashSet<string> { "residential", "living_street", "service", "unclassified", "tertiary" };

        public int MarkLines, Zebras, StopLines, SpeedBreakers, Manholes, Grates, Potholes, LanesCapped;
        /// <summary>Every marking and detail, when Build was asked to keep them (tests); otherwise null.</summary>
        public List<RoadMark> Marks;
        /// <summary>Where lines and details may not go (junction pads, zebras, stop lines), with their level, when Marks is kept.</summary>
        public List<(List<(double x, double z)> ring, double y)> KeepOuts;

        /// <summary>What a segment of a road is: the segment leaving the point it belongs to.</summary>
        struct SegAttr
        {
            public long Way;
            public string Cls;
            /// <summary>The lanes tag (0 when missing), both directions together on a two-way road.</summary>
            public int Lanes;
            /// <summary>+1 one-way along the points' order, -1 against it, 0 two-way.</summary>
            public sbyte Dir;
            public bool Marked;
            /// <summary>ClassRank of the class.</summary>
            public int Rank;
            /// <summary>Sidewalks left and right of the points' order (D-05).</summary>
            public bool WalkLeft, WalkRight;
        }

        /// <summary>A road as finally drawn, for the markings, which need every road at once.</summary>
        sealed class Drawn
        {
            public double[] Pts, Halves, Arc;
            public SegAttr[] Attrs;
            /// <summary>Offset per metre across, at the start and end of each segment: the mitre at a bend, else the segment's left normal.</summary>
            public double[] Sx, Sz, Ex, Ez;
            public double Y;
            public bool Grade, Bridge;
            /// <summary>Counted (dashes, details): from the start anchor below Split, from the end anchor above; each anchor's direction.</summary>
            public double Split;
            public int StartSign, EndSign;
            /// <summary>Bounds of the carriageway, with OverlapExtend.</summary>
            public double MinX, MinZ, MaxX, MaxZ;
            /// <summary>The first and last points lie on the chunk border.</summary>
            public bool StartOn, EndOn;
            /// <summary>Heights, on a ramp.</summary>
            public Ramp Ramp;
            public int N => Pts.Length / 2;
        }

        /// <summary>The ramp whose lines are being drawn, which lifts them.</summary>
        Ramp onRamp;
        List<List<(double x, double z)>> keepOut;
        List<double> keepOutY;
        List<Drawn> all;

        /// <summary>Adds the markings and details of every drawn road, after the roads and pads.</summary>
        void Markings(DadcChunk c, List<Drawn> drawn, List<Pad> pads, bool keep)
        {
            if (keep)
            {
                Marks = new List<RoadMark>();
                KeepOuts = new List<(List<(double x, double z)> ring, double y)>();
            }
            keepOut = new List<List<(double x, double z)>>(pads.Count + 16);
            keepOutY = new List<double>(pads.Count + 16);
            foreach (var pad in pads) KeepOut(pad.Ring, pad.Y - RoadPalette.PadLift);
            all = drawn;
            Mine().Crossed.Clear();
            // Crossings and stop lines first: lines and details keep out of them.
            CrossingsAndSignals(c, drawn, pads);
            foreach (var r in drawn)
            {
                onRamp = r.Ramp;
                LinesOn(r);
                onRamp = null;
            }
            foreach (var r in drawn) DetailsOn(r);
            // The crossings' keep-outs at grade, for the flyover supports (BuildStreet runs next).
            var grade = Mine().GradeKeepOuts;
            grade.Clear();
            for (int k = pads.Count; k < keepOut.Count; k++) if (SameLevel(keepOutY[k], 0.28)) grade.Add(keepOut[k]);
            keepOut = null;
            keepOutY = null;
            all = null;
        }

        /// <summary>Ways' graph edges by way id, for one-way.</summary>
        static Dictionary<long, List<GraphEdge>> EdgesByWay(DadcChunk c)
        {
            var mine = Mine();
            var byWay = mine.EdgesByWay;
            byWay.Clear();
            if (c.Edges == null) return byWay;
            int used = 0;
            foreach (var e in c.Edges)
            {
                if (!byWay.TryGetValue(e.Way, out var list))
                {
                    if (used == mine.EdgeLists.Count) mine.EdgeLists.Add(new List<GraphEdge>(2));
                    list = mine.EdgeLists[used++];
                    list.Clear();
                    byWay[e.Way] = list;
                }
                list.Add(e);
            }
            return byWay;
        }

        static SegAttr[] SegsFor(Road r, double[] pts, double width, Dictionary<long, List<GraphEdge>> edges)
        {
            var seg = new SegAttr
            {
                Way = r.Id, Cls = r.Cls, Lanes = r.Lanes ?? 0, Dir = OnewayAlong(r.Id, pts, edges),
                Marked = RoadPalette.IsMajor(r.Cls, width) && IsVehicle(r.Cls), Rank = ClassRank(r.Cls),
            };
            WalkSidesFor(r.Cls, r.Sidewalk, width, out seg.WalkLeft, out seg.WalkRight);
            var a = new SegAttr[pts.Length / 2];
            for (int i = 0; i < a.Length; i++) a[i] = seg;
            return a;
        }

        /// <summary>One-way direction of a road relative to its points, from its way's graph edges.</summary>
        static sbyte OnewayAlong(long way, double[] pts, Dictionary<long, List<GraphEdge>> edges)
        {
            if (!edges.TryGetValue(way, out var list)) return 0;
            foreach (var e in list)
            {
                if (e.Oneway == 0 || e.Pts == null || e.Pts.Length < 4) continue;
                double ex = (e.Pts[2] - e.Pts[0]) / 100.0, ez = (e.Pts[3] - e.Pts[1]) / 100.0;
                double mx = (e.Pts[0] + e.Pts[2]) / 200.0, mz = (e.Pts[1] + e.Pts[3]) / 200.0;
                double best = double.MaxValue, dot = 0;
                for (int i = 0; i + 3 < pts.Length; i += 2)
                {
                    ClosestOnSegment(mx, mz, pts[i], pts[i + 1], pts[i + 2], pts[i + 3], out double d, out _);
                    if (d < best) { best = d; dot = ex * (pts[i + 2] - pts[i]) + ez * (pts[i + 3] - pts[i + 1]); }
                }
                if (best > 2 || dot == 0) continue;
                return (sbyte)(Math.Sign(dot) * Math.Sign(e.Oneway));
            }
            return 0;
        }

        /// <summary>A road's segments in reverse order, one-way turned round, as Reverse turns its points.</summary>
        static SegAttr[] ReverseSegs(SegAttr[] a)
        {
            int n = a.Length;
            var r = new SegAttr[n];
            for (int j = 0; j + 1 < n; j++)
            {
                r[j] = a[n - 2 - j];
                r[j].Dir = (sbyte)-r[j].Dir;
                (r[j].WalkLeft, r[j].WalkRight) = (r[j].WalkRight, r[j].WalkLeft);
            }
            r[n - 1] = r[Math.Max(0, n - 2)];
            return r;
        }

        /// <summary>Two roads' segments joined at their shared point, as ChainRoads joins their points.</summary>
        static SegAttr[] JoinSegs(SegAttr[] left, SegAttr[] right)
        {
            var a = new SegAttr[left.Length + right.Length - 1];
            Array.Copy(left, a, left.Length - 1);
            Array.Copy(right, 0, a, left.Length - 1, right.Length);
            return a;
        }

        static Drawn MakeDrawn(double[] pts, double[] halves, SegAttr[] attrs, double y, bool bridge)
        {
            var d = new Drawn { Pts = pts, Halves = halves, Attrs = attrs, Y = y, Grade = !bridge || y < RoadPalette.ElevatedY, Bridge = bridge };
            int n = d.N;
            d.Arc = new double[n];
            for (int i = 1; i < n; i++) d.Arc[i] = d.Arc[i - 1] + Dist(pts[2 * i - 2], pts[2 * i - 1], pts[2 * i], pts[2 * i + 1]);
            bool startOn = OnEdgeLine(pts[0], pts[1]), endOn = OnEdgeLine(pts[2 * n - 2], pts[2 * n - 1]);
            d.StartSign = startOn ? AcrossSign(pts[0], pts[1], pts[2] - pts[0], pts[3] - pts[1]) : 1;
            d.EndSign = endOn ? AcrossSign(pts[2 * n - 2], pts[2 * n - 1], pts[2 * n - 2] - pts[2 * n - 4], pts[2 * n - 1] - pts[2 * n - 3]) : 1;
            d.Split = startOn && endOn ? d.Arc[n - 1] / 2 : endOn ? double.NegativeInfinity : double.PositiveInfinity;
            double reach = OverlapExtend;
            foreach (double h in halves) reach = Math.Max(reach, h + OverlapExtend);
            d.MinX = d.MinZ = double.MaxValue;
            d.MaxX = d.MaxZ = double.MinValue;
            for (int i = 0; i < n; i++)
            {
                d.MinX = Math.Min(d.MinX, pts[2 * i] - reach);
                d.MaxX = Math.Max(d.MaxX, pts[2 * i] + reach);
                d.MinZ = Math.Min(d.MinZ, pts[2 * i + 1] - reach);
                d.MaxZ = Math.Max(d.MaxZ, pts[2 * i + 1] + reach);
            }
            // An end cut on the chunk border runs on along the border (see SegmentRect): the box reaches it.
            d.StartOn = startOn;
            d.EndOn = endOn;
            if (startOn || endOn) { d.MinX -= BorderExtend; d.MinZ -= BorderExtend; d.MaxX += BorderExtend; d.MaxZ += BorderExtend; }
            d.Sx = new double[n - 1];
            d.Sz = new double[n - 1];
            d.Ex = new double[n - 1];
            d.Ez = new double[n - 1];
            for (int i = 0; i + 1 < n; i++)
            {
                double len = d.Arc[i + 1] - d.Arc[i];
                if (len < 1e-6) continue;
                double nx = -(pts[2 * i + 3] - pts[2 * i + 1]) / len, nz = (pts[2 * i + 2] - pts[2 * i]) / len;
                d.Sx[i] = d.Ex[i] = nx;
                d.Sz[i] = d.Ez[i] = nz;
            }
            // At each bend the lines of both segments meet on the mitre, as the outline's edges do;
            // past the outline's mitre limit they keep their own normals.
            for (int i = 1; i + 1 < n; i++)
            {
                double ax = d.Ex[i - 1], az = d.Ez[i - 1], bx = d.Sx[i], bz = d.Sz[i];
                if ((ax == 0 && az == 0) || (bx == 0 && bz == 0)) continue;
                double mx = ax + bx, mz = az + bz, ml = Math.Sqrt(mx * mx + mz * mz);
                if (ml < 0.2) continue;
                mx /= ml;
                mz /= ml;
                double cos = mx * ax + mz * az;
                if (cos < 1 / RoadPalette.MiterLimit) continue;
                d.Ex[i - 1] = d.Sx[i] = mx / cos;
                d.Ez[i - 1] = d.Sz[i] = mz / cos;
            }
            return d;
        }

        /// <summary>+1 when moving along (dx, dz) at (x, z) on the chunk border crosses that border eastwards or northwards.</summary>
        static int AcrossSign(double x, double z, double dx, double dz)
        {
            bool onX = Math.Abs(x) < OnEdge || Math.Abs(x - Size) < OnEdge, onZ = Math.Abs(z) < OnEdge || Math.Abs(z - Size) < OnEdge;
            bool useX = onX && (!onZ || Math.Abs(dx) >= Math.Abs(dz));
            double across = useX ? dx : dz, along = useX ? dz : dx;
            return across > 0 || (across == 0 && along >= 0) ? 1 : -1;
        }

        /// <summary>
        /// Where dashes and details are counted from: the parts of arc range [s0, s1] each anchor
        /// counts (start, end, the anchor's sign and arc), in arc order; how many. Phasing them by world
        /// position would make them agree across chunks, but 15 km from the origin a direction 0.2
        /// degrees off (a centimetre of rounding on a short segment) moves that phase by 50 m. So a
        /// road end on the chunk border is an anchor instead: 0 there, counting up eastwards (or
        /// northwards) across that border, which both chunks' halves of the road share. A road with
        /// both ends on the border counts from each to half way, one irregular dash in mid-chunk; one
        /// with neither counts from its start.
        /// </summary>
        static int Parts(Drawn r, double s0, double s1, Span<(double p0, double p1, int sign, double origin)> parts)
        {
            int count = 0;
            if (Math.Min(s1, r.Split) > s0) parts[count++] = (s0, Math.Min(s1, r.Split), r.StartSign, 0);
            if (s1 > Math.Max(s0, r.Split)) parts[count++] = (Math.Max(s0, r.Split), s1, r.EndSign, r.Arc[r.N - 1]);
            return count;
        }

        /// <summary>Lanes on a segment: the tag when it leaves lanes of 2.5 m or more, else from the width.</summary>
        static int LanesFor(SegAttr a, double half, out bool capped)
        {
            double width = 2 * half;
            capped = false;
            if (a.Lanes > 0)
            {
                if (width / a.Lanes >= MinLane) return a.Lanes;
                capped = true;
                return a.Dir != 0 ? Math.Max(1, (int)Math.Floor(width / MinLane)) : 2 * Math.Max(1, (int)Math.Floor(width / (2 * MinLane)));
            }
            if (a.Dir != 0) return Math.Max(1, (int)Math.Floor(width / LaneWidth));
            return width < NoCentreBelow ? 1 : 2 * Math.Max(1, (int)Math.Floor(width / (2 * LaneWidth)));
        }

        /// <summary>Lanes across a road by the rule above, and which boundary (1..lanes-1, from the right edge) is the centre line, -1 for none.</summary>
        public static int LaneCount(string cls, int lanesTag, int dir, double half, out int centre)
        {
            int n = LanesFor(new SegAttr { Cls = cls, Lanes = lanesTag, Dir = (sbyte)dir }, half, out _);
            centre = dir == 0 && n >= 2 ? n / 2 : -1;
            return n;
        }

        // ---- Spans: parts of a segment, as t in [0, 1] ----

        /// <summary>Clips a segment to a convex ring wound counter-clockwise; false when it misses.</summary>
        static bool ClipConvex(double ax, double az, double dx, double dz, List<(double x, double z)> ring, out double t0, out double t1)
        {
            t0 = 0;
            t1 = 1;
            for (int i = 0; i < ring.Count; i++)
            {
                var p = ring[i];
                var q = ring[(i + 1) % ring.Count];
                if (!Half(ax, az, dx, dz, p.x, p.z, q.x, q.z, ref t0, ref t1)) return false;
            }
            return true;
        }

        /// <summary>Narrows [t0, t1] to the part of the line left of the edge p-q (inside a counter-clockwise ring); false when nothing is left.</summary>
        static bool Half(double ax, double az, double dx, double dz, double px, double pz, double qx, double qz, ref double t0, ref double t1)
        {
            double nx = -(qz - pz), nz = qx - px;
            double f0 = nx * (ax - px) + nz * (az - pz), fd = nx * dx + nz * dz;
            if (Math.Abs(fd) < 1e-12) return f0 >= 0;
            double t = -f0 / fd;
            if (fd > 0) t0 = Math.Max(t0, t); else t1 = Math.Min(t1, t);
            return t0 < t1;
        }

        static bool Slab(double a, double d, double lo, double hi, ref double t0, ref double t1)
        {
            if (Math.Abs(d) < 1e-12) return a >= lo && a <= hi;
            double u0 = (lo - a) / d, u1 = (hi - a) / d;
            if (u0 > u1) (u0, u1) = (u1, u0);
            t0 = Math.Max(t0, u0);
            t1 = Math.Min(t1, u1);
            return t0 < t1;
        }

        /// <summary>How a centreline end (x, z), the road running in along (dx, dz), meets the chunk
        /// border: 0 not on it, 1 cut along it (within 60 degrees of square, as Cut does), 2 flatter.</summary>
        static int BorderEnd(double x, double z, double dx, double dz)
        {
            bool west = Math.Abs(x) < OnEdge, east = Math.Abs(x - Size) < OnEdge, south = Math.Abs(z) < OnEdge, north = Math.Abs(z - Size) < OnEdge;
            if (!(west || east || south || north)) return 0;
            double len = Math.Sqrt(dx * dx + dz * dz);
            if (len < 1e-9) return 2;
            bool alongX = (west || east) && (!(south || north) || Math.Abs(dx) >= Math.Abs(dz));
            return Math.Abs(alongX ? dx : dz) / len >= MinCutCos ? 1 : 2;
        }

        /// <summary>
        /// Clips a line (a to b, offset from the centreline segment c to e) to the chunk. A line end
        /// whose centre end lies on the border where the carriageway is cut along it is carried to the
        /// border, so the halves of a line split between two chunks meet there; a flatter crossing
        /// keeps its end, as the carriageway's square end does. Anywhere else a line running past the
        /// border (round a bend near it) is cut there. False when nothing is left, or when the line
        /// runs backwards: the inside of a sharp bend on a segment shorter than the mitre's reach.
        /// </summary>
        static bool ClipToChunk(double cx, double cz, double ex, double ez, ref double ax, ref double az, ref double bx, ref double bz)
        {
            int start = BorderEnd(cx, cz, ex - cx, ez - cz), end = BorderEnd(ex, ez, cx - ex, cz - ez);
            double ux = bx - ax, uz = bz - az, len = Math.Sqrt(ux * ux + uz * uz);
            if (ux * (ex - cx) + uz * (ez - cz) <= 0) return false;
            // Within 60 degrees of square, a line reaches the border within 2 x its offset of the centre's crossing.
            double reach = (2 * Math.Max(Dist(ax, az, cx, cz), Dist(bx, bz, ex, ez)) + 1) / len;
            double t0 = start == 1 ? -reach : 0, t1 = end == 1 ? 1 + reach : 1;
            if (!Slab(ax, ux, 0, Size, ref t0, ref t1) || !Slab(az, uz, 0, Size, ref t0, ref t1))
            {
                if (start != 2 && end != 2) return false;
                t0 = 0;
                t1 = 1;
            }
            if (start == 2) t0 = 0;
            if (end == 2) t1 = 1;
            if (t1 <= t0) return false;
            double x0 = ax + ux * t0, z0 = az + uz * t0;
            bx = ax + ux * t1;
            bz = az + uz * t1;
            ax = x0;
            az = z0;
            return true;
        }

        /// <summary>Takes from spans (t along a to b) every part inside a keep-out at level y.</summary>
        void Subtract(double ax, double az, double bx, double bz, double y, List<(double a, double b)> spans)
        {
            double dx = bx - ax, dz = bz - az;
            var next = Emptied(Mine().Spans2);
            for (int k = 0; k < keepOut.Count && spans.Count > 0; k++)
            {
                if (SameLevel(keepOutY[k], y) && ClipConvex(ax, az, dx, dz, keepOut[k], out double c0, out double c1)) Cut(spans, next, c0, c1);
            }
        }

        static void Cut(List<(double a, double b)> spans, List<(double a, double b)> next, double c0, double c1)
        {
            next.Clear();
            foreach (var (a, b) in spans)
            {
                if (c1 <= a || c0 >= b) { next.Add((a, b)); continue; }
                if (c0 > a) next.Add((a, c0));
                if (c1 < b) next.Add((c1, b));
            }
            spans.Clear();
            spans.AddRange(next);
        }

        /// <summary>
        /// Takes out of spans the parts of a line of road r's segment i that lie on another road's
        /// carriageway of equal or higher rank at the same level. Where carriageways overlap (dual
        /// carriageways whose estimated widths overlap, a link drawn alongside its trunk), the lesser
        /// road's lines stop at the greater one's edge, and twins drop the lines between them. Each
        /// segment is a rectangle, OverlapShrink narrower on each side (so carriageways that only touch
        /// keep their edge lines) and OverlapExtend longer at each end (closing the notches at bends).
        /// </summary>
        void CutByCarriageways(Drawn r, int i, double ax, double az, double bx, double bz, List<(double a, double b)> spans)
        {
            int rank = r.Attrs[i].Rank;
            double dx = bx - ax, dz = bz - az;
            double minX = Math.Min(ax, bx), maxX = Math.Max(ax, bx), minZ = Math.Min(az, bz), maxZ = Math.Max(az, bz);
            var next = Emptied(Mine().Spans2);
            foreach (var o in all)
            {
                if (spans.Count == 0) return;
                if (o == r || !SameLevel(o.Y, r.Y) || o.MaxX < minX || o.MinX > maxX || o.MaxZ < minZ || o.MinZ > maxZ) continue;
                for (int j = 0; j + 1 < o.N && spans.Count > 0; j++)
                {
                    var a = o.Attrs[j];
                    double len = o.Arc[j + 1] - o.Arc[j];
                    if (a.Rank < rank || !IsVehicle(a.Cls) || len < 1e-6) continue;
                    double px = o.Pts[2 * j], pz = o.Pts[2 * j + 1], qx = o.Pts[2 * j + 2], qz = o.Pts[2 * j + 3];
                    double reach = Math.Max(o.Halves[j], o.Halves[j + 1]) + OverlapExtend;
                    if (Math.Max(px, qx) + reach < minX || Math.Min(px, qx) - reach > maxX || Math.Max(pz, qz) + reach < minZ || Math.Min(pz, qz) - reach > maxZ) continue;
                    double ha = o.Halves[j] - OverlapShrink, hb = o.Halves[j + 1] - OverlapShrink;
                    if (ha <= 0 || hb <= 0) continue;
                    double ux = (qx - px) / len, uz = (qz - pz) / len, nx = -uz, nz = ux;
                    px -= ux * OverlapExtend;
                    pz -= uz * OverlapExtend;
                    qx += ux * OverlapExtend;
                    qz += uz * OverlapExtend;
                    // Counter-clockwise: the right side forwards, the left side back.
                    double x0 = px - nx * ha, z0 = pz - nz * ha, x1 = qx - nx * hb, z1 = qz - nz * hb;
                    double x2 = qx + nx * hb, z2 = qz + nz * hb, x3 = px + nx * ha, z3 = pz + nz * ha;
                    double c0 = 0, c1 = 1;
                    if (Half(ax, az, dx, dz, x0, z0, x1, z1, ref c0, ref c1) && Half(ax, az, dx, dz, x1, z1, x2, z2, ref c0, ref c1)
                        && Half(ax, az, dx, dz, x2, z2, x3, z3, ref c0, ref c1) && Half(ax, az, dx, dz, x3, z3, x0, z0, ref c0, ref c1))
                        Cut(spans, next, c0, c1);
                }
            }
        }

        // ---- Lines ----

        void LinesOn(Drawn r)
        {
            var mine = Mine();
            var spans = Emptied(mine.Spans);
            var pieces = Emptied(mine.Pieces);
            bool capped = false;
            for (int i = 0; i + 1 < r.N; i++)
            {
                var a = r.Attrs[i];
                if (!a.Marked) continue;
                double ax = r.Pts[2 * i], az = r.Pts[2 * i + 1], bx = r.Pts[2 * i + 2], bz = r.Pts[2 * i + 3];
                if (r.Arc[i + 1] - r.Arc[i] < 0.05) continue;
                double ha = r.Halves[i], hb = r.Halves[i + 1];
                int n = LanesFor(a, (ha + hb) / 2, out bool c);
                capped |= c;
                int centre = a.Dir == 0 && n >= 2 ? n / 2 : -1;
                for (int k = 1; k < n; k++)
                {
                    double f = -1 + 2.0 * k / n;
                    var kind = k == centre ? MarkKind.CentreLine : MarkKind.LaneLine;
                    Line(r, i, f * ha, f * hb, LaneLineWidth, kind, !(k == centre && n >= 4), spans, pieces, a.Way);
                }
                if ((ha + hb) / 2 >= EdgeLinesFrom)
                    for (int side = -1; side <= 1; side += 2)
                        Line(r, i, side * (ha - EdgeInset), side * (hb - EdgeInset), EdgeLineWidth, MarkKind.EdgeLine, false, spans, pieces, a.Way);
            }
            if (capped) LanesCapped++;
        }

        /// <summary>One line along segment i, ua and ub metres left of the centre at its ends.</summary>
        void Line(Drawn r, int i, double ua, double ub, double width, MarkKind kind, bool dashed,
            List<(double a, double b)> spans, List<(double a, double b)> pieces, long way)
        {
            double cx = r.Pts[2 * i], cz = r.Pts[2 * i + 1], ex = r.Pts[2 * i + 2], ez = r.Pts[2 * i + 3];
            double ax = cx + r.Sx[i] * ua, az = cz + r.Sz[i] * ua, bx = ex + r.Ex[i] * ub, bz = ez + r.Ez[i] * ub;
            if (Dist(ax, az, bx, bz) < 1e-6 || !ClipToChunk(cx, cz, ex, ez, ref ax, ref az, ref bx, ref bz)) return;
            spans.Clear();
            spans.Add((0, 1));
            Subtract(ax, az, bx, bz, r.Y, spans);
            CutByCarriageways(r, i, ax, az, bx, bz, spans);
            pieces.Clear();
            if (!dashed) pieces.AddRange(spans);
            else
            {
                // Dash k covers [k P, k P + on) of the count along the road (Counted), taken at the
                // foot of each point of the line on the centreline: the line runs parallel to it, so
                // dashes are 3 m long on it, and a line split by the border has one foot there.
                double seg = r.Arc[i + 1] - r.Arc[i], dx0 = (ex - cx) / seg, dz0 = (ez - cz) / seg;
                double s0 = r.Arc[i] + (ax - cx) * dx0 + (az - cz) * dz0, s1 = r.Arc[i] + (bx - cx) * dx0 + (bz - cz) * dz0;
                if (s1 - s0 < 0.05) return; // an inside line folded back by a sharp bend on a short segment
                Span<(double p0, double p1, int sign, double origin)> parts = stackalloc (double, double, int, double)[2];
                int count = Parts(r, s0, s1, parts);
                for (int q = 0; q < count; q++)
                {
                    var (p0, p1, sign, origin) = parts[q];
                    double lo = Math.Min(sign * (p0 - origin), sign * (p1 - origin)), hi = Math.Max(sign * (p0 - origin), sign * (p1 - origin));
                    for (long k = (long)Math.Floor(lo / DashPeriod); k * DashPeriod < hi; k++)
                    {
                        double w0 = origin + sign * k * DashPeriod, w1 = origin + sign * (k * DashPeriod + DashOn);
                        double d0 = (Math.Max(p0, Math.Min(w0, w1)) - s0) / (s1 - s0), d1 = (Math.Min(p1, Math.Max(w0, w1)) - s0) / (s1 - s0);
                        foreach (var (a, b) in spans)
                        {
                            double e0 = Math.Max(a, d0), e1 = Math.Min(b, d1);
                            if (e1 > e0) pieces.Add((e0, e1));
                        }
                    }
                }
            }
            double dx = bx - ax, dz = bz - az, len = Math.Sqrt(dx * dx + dz * dz), y = r.Y + MarkLift;
            foreach (var (t0, t1) in pieces)
            {
                if ((t1 - t0) * len < 0.05) continue;
                double x0 = ax + dx * t0, z0 = az + dz * t0, x1 = ax + dx * t1, z1 = az + dz * t1;
                Quad(x0, z0, x1, z1, width, y, Pack(Paint, WorldMaterial.Marking));
                MarkLines++;
                Record(kind, way, x0, z0, x1, z1, width, y, dashed);
            }
        }

        /// <summary>A flat strip from (x0, z0) to (x1, z1), width wide, facing up.</summary>
        void Quad(double x0, double z0, double x1, double z1, double width, double y, uint colour)
        {
            double dx = x1 - x0, dz = z1 - z0, len = Math.Sqrt(dx * dx + dz * dz);
            if (len < 1e-6) return;
            double nx = -dz / len * width / 2, nz = dx / len * width / 2;
            int f = col.Count;
            double y0 = y, y1 = y;
            if (onRamp != null) { y0 = onRamp.HeightAt(x0, z0) + MarkLift; y1 = onRamp.HeightAt(x1, z1) + MarkLift; }
            Vertex(x0 + nx, y0, z0 + nz, colour);
            Vertex(x0 - nx, y0, z0 - nz, colour);
            Vertex(x1 + nx, y1, z1 + nz, colour);
            Vertex(x1 - nx, y1, z1 - nz, colour);
            TriUp(f, f + 2, f + 1);
            TriUp(f + 1, f + 2, f + 3);
        }

        void Record(MarkKind kind, long way, double ax, double az, double bx, double bz, double width, double y, bool dashed = false)
        {
            Marks?.Add(new RoadMark { Kind = kind, Way = way, Ax = ax, Az = az, Bx = bx, Bz = bz, Width = width, Y = y, Dashed = dashed });
        }

        void KeepOut(List<(double x, double z)> ring, double y)
        {
            keepOut.Add(ring);
            keepOutY.Add(y);
            KeepOuts?.Add((ring, y));
        }

        // ---- Crossings and signals ----

        /// <summary>A point on a drawn road at an arc length: position, unit tangent, half width and segment.</summary>
        static void At(Drawn r, double s, out double x, out double z, out double tx, out double tz, out double half, out int seg)
        {
            s = Math.Max(0, Math.Min(r.Arc[r.N - 1], s));
            int i = 0;
            while (i < r.N - 2 && (r.Arc[i + 1] < s || r.Arc[i + 1] - r.Arc[i] < 1e-6)) i++;
            double len = r.Arc[i + 1] - r.Arc[i], t = len > 1e-9 ? Math.Min(1, (s - r.Arc[i]) / len) : 0;
            double ax = r.Pts[2 * i], az = r.Pts[2 * i + 1], bx = r.Pts[2 * i + 2], bz = r.Pts[2 * i + 3];
            x = ax + (bx - ax) * t;
            z = az + (bz - az) * t;
            double l = Math.Max(1e-9, len);
            tx = (bx - ax) / l;
            tz = (bz - az) / l;
            half = r.Halves[i] + (r.Halves[i + 1] - r.Halves[i]) * t;
            seg = i;
        }

        /// <summary>The closest point of a drawn road to (px, pz): distance and arc length.</summary>
        static void Closest(Drawn r, double px, double pz, out double dist, out double arc)
        {
            dist = double.MaxValue;
            arc = 0;
            for (int i = 0; i + 1 < r.N; i++)
            {
                ClosestOnSegment(px, pz, r.Pts[2 * i], r.Pts[2 * i + 1], r.Pts[2 * i + 2], r.Pts[2 * i + 3], out double d, out double t);
                if (d < dist) { dist = d; arc = r.Arc[i] + t * (r.Arc[i + 1] - r.Arc[i]); }
            }
        }

        /// <summary>A rectangle along a road as a counter-clockwise ring: centre, tangent, half length along, half width across.</summary>
        static List<(double x, double z)> Rect(double cx, double cz, double tx, double tz, double along, double across)
        {
            double nx = -tz, nz = tx;
            return new List<(double x, double z)>(4)
            {
                (cx - tx * along - nx * across, cz - tz * along - nz * across),
                (cx + tx * along - nx * across, cz + tz * along - nz * across),
                (cx + tx * along + nx * across, cz + tz * along + nz * across),
                (cx - tx * along + nx * across, cz - tz * along + nz * across),
            };
        }

        void Zebra(double cx, double cz, double tx, double tz, double half, double y, long way)
        {
            double nx = -tz, nz = tx, reach = half - ZebraMargin;
            int bars = (int)Math.Floor((2 * reach + ZebraGap) / (ZebraBar + ZebraGap));
            if (bars < 2) return;
            // Centred across the road.
            double first = -(bars * (ZebraBar + ZebraGap) - ZebraGap) / 2 + ZebraBar / 2;
            for (int b = 0; b < bars; b++)
            {
                double u = first + b * (ZebraBar + ZebraGap);
                double x0 = cx - tx * ZebraDepth / 2 + nx * u, z0 = cz - tz * ZebraDepth / 2 + nz * u;
                double x1 = cx + tx * ZebraDepth / 2 + nx * u, z1 = cz + tz * ZebraDepth / 2 + nz * u;
                Quad(x0, z0, x1, z1, ZebraBar, y + MarkLift, Pack(Paint, WorldMaterial.Marking));
                Record(MarkKind.Zebra, way, x0, z0, x1, z1, ZebraBar, y + MarkLift);
            }
            Zebras++;
            Mine().Crossed.Add((cx, cz, tx, tz, half, y));
        }

        /// <summary>Zebras on mapped crossings, and zebras and stop lines on signalled approaches; each keeps lines and details out.</summary>
        void CrossingsAndSignals(DadcChunk c, List<Drawn> drawn, List<Pad> pads)
        {
            if (c.Points != null)
                foreach (var p in c.Points)
                {
                    if (p.CrossingType == null || !MarkedCrossings.Contains(p.CrossingType)) continue;
                    double px = p.East / 100.0, pz = p.North / 100.0;
                    Drawn best = null;
                    double bestD = double.MaxValue, bestS = 0;
                    foreach (var r in drawn)
                    {
                        if (!r.Grade) continue;
                        Closest(r, px, pz, out double d, out double s);
                        At(r, s, out _, out _, out _, out _, out double h, out int seg);
                        if (!IsVehicle(r.Attrs[seg].Cls) || d > h + 0.5 || d >= bestD) continue;
                        best = r;
                        bestD = d;
                        bestS = s;
                    }
                    if (best == null) continue;
                    At(best, bestS, out double x, out double z, out double tx, out double tz, out double half, out int i);
                    Zebra(x, z, tx, tz, half, best.Y, best.Attrs[i].Way);
                    KeepOut(Rect(x, z, tx, tz, ZebraDepth / 2 + 0.5, half + 0.5), best.Y);
                }

            if (c.Nodes == null) return;
            var signals = Emptied(Mine().Signals);
            foreach (var n in c.Nodes) if (n.Signal.HasValue && n.Signal.Value != 0) signals.Add((n.East / 100.0, n.North / 100.0));
            foreach (var (sx, sz) in signals)
                foreach (var r in drawn)
                {
                    if (!r.Grade) continue;
                    Closest(r, sx, sz, out double d, out double sc);
                    At(r, sc, out _, out _, out _, out _, out double hc, out int segC);
                    if (d > hc + 1 || !IsVehicle(r.Attrs[segC].Cls)) continue;
                    for (int sigma = -1; sigma <= 1; sigma += 2) Approach(r, sc, sigma, sx, sz, signals, pads);
                }
        }

        /// <summary>One approach to a signal: along the road from the signal's point on it, outwards in direction sigma.</summary>
        void Approach(Drawn r, double sc, int sigma, double sx, double sz, List<(double x, double z)> signals, List<Pad> pads)
        {
            double total = r.Arc[r.N - 1];
            // Another signal of the junction further along: this is a link inside the junction.
            foreach (var (ox, oz) in signals)
            {
                if (ox == sx && oz == sz) continue;
                Closest(r, ox, oz, out double d2, out double s2);
                At(r, s2, out _, out _, out _, out _, out double h2, out _);
                if (d2 <= h2 + 1 && sigma * (s2 - sc) > 0 && Math.Abs(s2 - sc) < 40)
                {
                    // The box between the two signals carries no lines or details. Each link is met
                    // from both ends; it is kept out once.
                    if (sigma > 0)
                    {
                        At(r, (sc + s2) / 2, out double mx, out double mz, out double mtx, out double mtz, out double mh, out _);
                        KeepOut(Rect(mx, mz, mtx, mtz, (s2 - sc) / 2, mh + 0.5), r.Y);
                    }
                    return;
                }
            }
            // The first point 2 m or more out that is outside every junction pad.
            double sp = double.NaN;
            for (double step = 2; step <= 40; step += 0.25)
            {
                double s = sc + sigma * step;
                if (s < 0 || s > total) return;
                At(r, s, out double x, out double z, out _, out _, out _, out _);
                bool inside = false;
                foreach (var pad in pads)
                    if (SameLevel(pad.Y - RoadPalette.PadLift, r.Y) && Inside(pad.Ring, x, z)) { inside = true; break; }
                if (!inside) { sp = s; break; }
            }
            if (double.IsNaN(sp)) return;
            double depth = 0.5 + ZebraDepth + StopBehind + StopLineWidth;
            double zebraMid = sp + sigma * (0.5 + ZebraDepth / 2), stop = sp + sigma * (depth - StopLineWidth / 2);
            if (stop < 0 || stop > total) return;
            At(r, zebraMid, out double zx, out double zz, out double tx, out double tz, out double zh, out int zi);
            if (zx < 0 || zx > Size || zz < 0 || zz > Size) return;
            Zebra(zx, zz, tx, tz, zh, r.Y, r.Attrs[zi].Way);
            At(r, stop, out double lx, out double lz, out double ltx, out double ltz, out double lh, out int li);
            // Inbound traffic heads -sigma along the road and keeps left: on the side -sigma of the road's left.
            var a = r.Attrs[li];
            if (a.Dir == 0 || a.Dir == -sigma)
            {
                double nx = -ltz, nz = ltx, reach = lh - ZebraMargin;
                double from = a.Dir != 0 ? -reach : sigma < 0 ? 0 : -reach, to = a.Dir != 0 ? reach : sigma < 0 ? reach : 0;
                double ax = lx + nx * from, az = lz + nz * from, bx = lx + nx * to, bz = lz + nz * to;
                Quad(ax, az, bx, bz, StopLineWidth, r.Y + MarkLift, Pack(Paint, WorldMaterial.Marking));
                Record(MarkKind.StopLine, a.Way, ax, az, bx, bz, StopLineWidth, r.Y + MarkLift);
                StopLines++;
            }
            At(r, sp + sigma * depth / 2, out double kx, out double kz, out double ktx, out double ktz, out double kh, out _);
            KeepOut(Rect(kx, kz, ktx, ktz, depth / 2 + 0.3, kh + 0.5), r.Y);
        }

        static bool Inside(List<(double x, double z)> ring, double x, double z)
        {
            for (int i = 0; i < ring.Count; i++)
            {
                var p = ring[i];
                var q = ring[(i + 1) % ring.Count];
                if ((q.x - p.x) * (z - p.z) - (q.z - p.z) * (x - p.x) < 0) return false;
            }
            return true;
        }

        static bool IsVehicle(string cls) => cls != null && !RoadPalette.IsFoot(cls) && cls != "construction";

        /// <summary>A class's standing where carriageways overlap: a road's lines stop on a road of equal or higher rank. Links rank just under their road.</summary>
        public static int ClassRank(string cls)
        {
            switch (cls)
            {
                case "motorway": return 12;
                case "motorway_link": return 11;
                case "trunk": return 10;
                case "trunk_link": return 9;
                case "primary": return 8;
                case "primary_link": return 7;
                case "secondary": return 6;
                case "secondary_link": return 5;
                case "tertiary": return 4;
                case "tertiary_link": return 3;
                case "unclassified": case "residential": case "living_street": return 2;
                case "service": return 1;
                default: return 0;
            }
        }

        // ---- Rule-placed details ----

        /// <summary>A number in [0, 1) from a road id and a salt (SplitMix64).</summary>
        public static double Hash(long way, long salt)
        {
            ulong x = unchecked((ulong)way * 0x9E3779B97F4A7C15UL + (ulong)salt * 0xBF58476D1CE4E5B9UL);
            x = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9UL;
            x = (x ^ (x >> 27)) * 0x94D049BB133111EBUL;
            x ^= x >> 31;
            return (x >> 11) * (1.0 / (1UL << 53));
        }

        const int DetailKinds = 5;

        void DetailsOn(Drawn r)
        {
            if (!r.Grade) return;
            double total = r.Arc[r.N - 1];
            bool startOpen = OnBorder(r.Pts[0], r.Pts[1]), endOpen = OnBorder(r.Pts[2 * r.N - 2], r.Pts[2 * r.N - 1]);
            var last = Mine().LastPlaced;
            for (int k = 0; k < DetailKinds; k++) last[k] = double.MinValue;
            for (int i = 0; i + 1 < r.N; i++)
            {
                var a = r.Attrs[i];
                if (!IsVehicle(a.Cls) || r.Arc[i + 1] - r.Arc[i] < 0.5) continue;
                if (!a.Marked && BreakerClasses.Contains(a.Cls)) Each(r, i, total, startOpen, endOpen, 0, BreakerPeriod);
                if (a.Marked) Each(r, i, total, startOpen, endOpen, 1, ManholePeriod);
                if ((r.Halves[i] + r.Halves[i + 1]) / 2 >= 3)
                {
                    Each(r, i, total, startOpen, endOpen, 2, GratePeriod);
                    Each(r, i, total, startOpen, endOpen, 3, GratePeriod);
                }
                if (!a.Marked && PotholeClasses.Contains(a.Cls)) Each(r, i, total, startOpen, endOpen, 4, PotholePeriod);
            }
        }

        /// <summary>Places detail kind where the count along the road is a multiple of period, offset by a hash of the way, on segment i: in arc order, clear of the ends, the last one, the border and keep-outs.</summary>
        void Each(Drawn r, int i, double total, bool startOpen, bool endOpen, int kind, double period)
        {
            var a = r.Attrs[i];
            double off = Hash(a.Way, kind + 1) * period;
            var last = Mine().LastPlaced;
            Span<(double p0, double p1, int sign, double origin)> parts = stackalloc (double, double, int, double)[2];
            int count = Parts(r, r.Arc[i], r.Arc[i + 1], parts);
            for (int q = 0; q < count; q++)
            {
                var (p0, p1, sign, origin) = parts[q];
                double lo = Math.Min(sign * (p0 - origin), sign * (p1 - origin)), hi = Math.Max(sign * (p0 - origin), sign * (p1 - origin));
                long k0 = (long)Math.Ceiling((lo + off) / period), k1 = (long)Math.Floor((hi + off) / period);
                for (long j = 0; j <= k1 - k0; j++)
                {
                    long k = sign > 0 ? k0 + j : k1 - j;
                    double s = origin + sign * (k * period - off);
                    if (s < p0 || s > p1) continue;
                    Place(r, s, k, total, startOpen, endOpen, kind, period, last);
                }
            }
        }

        void Place(Drawn r, double s, long k, double total, bool startOpen, bool endOpen, int kind, double period, double[] last)
        {
            if ((!startOpen && s < DetailEndClear) || (!endOpen && total - s < DetailEndClear) || s - last[kind] < period / 2) return;
            At(r, s, out double x, out double z, out double tx, out double tz, out double half, out _);
            if (x < 1 || x > Size - 1 || z < 1 || z > Size - 1 || Blocked(x, z, tx, tz, half, r.Y)) return;
            bool placed = kind switch
            {
                0 => Breaker(r, s),
                1 => Manhole(r, s, k),
                2 => Grate(r, s, 1),
                3 => Grate(r, s, -1),
                _ => Pothole(r, s, k),
            };
            if (placed) last[kind] = s;
        }

        /// <summary>True when a detail's footprint (2 m along, the road's width across) touches a keep-out at its level.</summary>
        bool Blocked(double x, double z, double tx, double tz, double half, double y)
        {
            double nx = -tz, nz = tx;
            for (int k = 0; k < keepOut.Count; k++)
            {
                if (!SameLevel(keepOutY[k], y)) continue;
                for (int a = -1; a <= 1; a++)
                    for (int b = -1; b <= 1; b++)
                        if (Inside(keepOut[k], x + tx * a + nx * b * half, z + tz * a + nz * b * half)) return true;
            }
            return false;
        }

        bool Breaker(Drawn r, double s)
        {
            At(r, s, out double cx, out double cz, out double tx, out double tz, out double half, out int i);
            if (half < 1.5) return false;
            double reach = half - 0.2, nx = -tz, nz = tx, y0 = r.Y + 0.002, y1 = r.Y + BreakerHeight;
            var v = Mine().Profile;
            var h = Mine().Heights;
            v[0] = -BreakerLength / 2; v[1] = -0.15; v[2] = 0.15; v[3] = BreakerLength / 2;
            h[0] = y0; h[1] = y1; h[2] = y1; h[3] = y0;
            // Painted bands across, white and black.
            int bands = Math.Max(1, (int)Math.Round(2 * reach / 0.5));
            for (int b = 0; b < bands; b++)
            {
                double u0 = -reach + 2 * reach * b / bands, u1 = -reach + 2 * reach * (b + 1) / bands;
                uint colour = b % 2 == 0 ? Pack(Paint, WorldMaterial.Marking) : Pack(BreakerDark, WorldMaterial.Asphalt);
                int f = col.Count;
                for (int j = 0; j < 4; j++)
                {
                    Vertex(cx + tx * v[j] + nx * u0, h[j], cz + tz * v[j] + nz * u0, colour);
                    Vertex(cx + tx * v[j] + nx * u1, h[j], cz + tz * v[j] + nz * u1, colour);
                }
                for (int j = 0; j < 3; j++)
                {
                    TriUp(f + 2 * j, f + 2 * j + 2, f + 2 * j + 1);
                    TriUp(f + 2 * j + 1, f + 2 * j + 2, f + 2 * j + 3);
                }
            }
            // The hump's profile at each end, both faces.
            uint end = Pack(BreakerDark, WorldMaterial.Asphalt);
            for (int side = -1; side <= 1; side += 2)
            {
                int f = col.Count;
                for (int j = 0; j < 4; j++) Vertex(cx + tx * v[j] + nx * side * reach, h[j], cz + tz * v[j] + nz * side * reach, end);
                idx.Add(f); idx.Add(f + 1); idx.Add(f + 2);
                idx.Add(f); idx.Add(f + 2); idx.Add(f + 3);
                idx.Add(f); idx.Add(f + 2); idx.Add(f + 1);
                idx.Add(f); idx.Add(f + 3); idx.Add(f + 2);
            }
            SpeedBreakers++;
            Record(MarkKind.SpeedBreaker, r.Attrs[i].Way, cx - nx * reach, cz - nz * reach, cx + nx * reach, cz + nz * reach, BreakerLength, y1);
            return true;
        }

        /// <summary>A flat polygon round a centre; with a seed, its corners are pulled in or out by up to a quarter.</summary>
        void Disc(double cx, double cz, double radius, int sides, double y, uint colour, long seed = -1)
        {
            int f = col.Count;
            Vertex(cx, y, cz, colour);
            for (int j = 0; j < sides; j++)
            {
                double angle = 2 * Math.PI * j / sides, rr = seed >= 0 ? radius * (0.75 + 0.5 * Hash(seed, j)) : radius;
                Vertex(cx + Math.Cos(angle) * rr, y, cz + Math.Sin(angle) * rr, colour);
            }
            for (int j = 0; j < sides; j++) TriUp(f, f + 1 + j, f + 1 + (j + 1) % sides);
        }

        bool Manhole(Drawn r, double s, long k)
        {
            At(r, s, out double cx, out double cz, out double tx, out double tz, out double half, out int i);
            var a = r.Attrs[i];
            int n = LanesFor(a, half, out _);
            int lane = Math.Min(n - 1, (int)(Hash(a.Way, 100 + k) * n));
            double u = -half + 2 * half * (lane + 0.5) / n;
            double x = cx - tz * u, z = cz + tx * u;
            Disc(x, z, 0.32, 8, r.Y + MarkLift, Pack(ManholeIron, WorldMaterial.Metal));
            Manholes++;
            Record(MarkKind.Manhole, a.Way, x, z, x, z, 0.64, r.Y + MarkLift);
            return true;
        }

        bool Grate(Drawn r, double s, int side)
        {
            At(r, s, out double cx, out double cz, out double tx, out double tz, out double half, out int i);
            double u = side * (half - 0.3), x = cx - tz * u, z = cz + tx * u;
            Quad(x - tx * 0.4, z - tz * 0.4, x + tx * 0.4, z + tz * 0.4, 0.35, r.Y + MarkLift, Pack(GrateIron, WorldMaterial.Metal));
            Grates++;
            Record(MarkKind.Grate, r.Attrs[i].Way, x - tx * 0.4, z - tz * 0.4, x + tx * 0.4, z + tz * 0.4, 0.35, r.Y + MarkLift);
            return true;
        }

        bool Pothole(Drawn r, double s, long k)
        {
            At(r, s, out double cx, out double cz, out double tx, out double tz, out double half, out int i);
            var a = r.Attrs[i];
            // Every other place, by hash, so about one in 75 m.
            if (Hash(a.Way, 200 + k) < 0.5) return false;
            double radius = 0.35 + 0.4 * Hash(a.Way, 300 + k), room = half - 1.25 * radius - 0.15;
            if (room < 0) return false;
            double u = (2 * Hash(a.Way, 400 + k) - 1) * room, x = cx - tz * u, z = cz + tx * u;
            Disc(x, z, radius, 9, r.Y + MarkLift, Pack(PotholeMud, WorldMaterial.Soil), (long)(Hash(a.Way, 500 + k) * (1L << 40)));
            Potholes++;
            Record(MarkKind.Pothole, a.Way, x, z, x, z, 2 * radius, r.Y + MarkLift);
            return true;
        }
    }
}
