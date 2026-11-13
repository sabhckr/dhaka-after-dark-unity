// D-05 exit evidence: sidewalks and kerbs along every slice road. Which sides have one is read from
// the data (tag, else the width rule); the mesh is then probed beside each carriageway edge: the
// sidewalk is there, KerbHeight above the road, unless another carriageway, a junction, a crossing's
// ramp or a building leaves no room; the carriageway itself is never covered. Every edge of a
// sidewalk top is closed by a kerb, outer or end face reaching the road or the ground (no floating
// pieces, no open edges), no two sidewalks overlap, and the kerb drops to the road where a zebra
// meets it. Every bus stop has its stop pad, and lanes with no sidewalk have their open drains,
// held to the same edge and overlap checks.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dad.World;
using NUnit.Framework;
using UnityEngine;

namespace Dad.Tests
{
    public class SidewalkTests
    {
        internal static string ChunkDir => Path.Combine(TestWorld.Root, "chunks");

        internal static Dictionary<(int ix, int iy), DadcChunk> chunks;
        internal static Dictionary<(int ix, int iy), ChunkRoads> built;

        internal static void LoadSlice()
        {
            if (built != null) return;
            chunks = new Dictionary<(int, int), DadcChunk>();
            built = new Dictionary<(int, int), ChunkRoads>();
            for (int ix = 6; ix <= 9; ix++)
                for (int iy = -30; iy <= -28; iy++)
                {
                    var f = Path.Combine(ChunkDir, $"c_{ix}_{iy}.dadc");
                    Assume.That(File.Exists(f), "world data missing: run Tools/worldgen/build.sh");
                    var c = Dadc.Load(File.ReadAllBytes(f));
                    NeighbourRails.Attach(c, ChunkDir); // as the streamer loads it (P-2, K-50)
                    chunks[(ix, iy)] = c;
                    built[(ix, iy)] = ChunkRoads.Build(c, keepMarks: true);
                }
        }

        internal static double HalfOf(Road r) => Math.Max(RoadPalette.MinHalf, r.WidthCm / 200.0);

        /// <summary>Faces of a road mesh: up-facing triangles bucketed by 2 m cells, for height probes.</summary>
        internal sealed class Probe
        {
            readonly ChunkRoads r;
            readonly Dictionary<(int, int), List<int>> cells = new Dictionary<(int, int), List<int>>();

            public Probe(ChunkRoads r)
            {
                this.r = r;
                var p = r.Positions;
                for (int t = 0; t < r.IndexCount; t += 3)
                {
                    int a = r.Indices[t] * 3, b = r.Indices[t + 1] * 3, c = r.Indices[t + 2] * 3;
                    double area = (p[b] - p[a]) * (p[c + 2] - p[a + 2]) - (p[b + 2] - p[a + 2]) * (p[c] - p[a]);
                    if (Math.Abs(area) < 1e-6) continue; // vertical
                    int x0 = (int)Math.Floor(Math.Min(p[a], Math.Min(p[b], p[c])) / 2), x1 = (int)Math.Floor(Math.Max(p[a], Math.Max(p[b], p[c])) / 2);
                    int z0 = (int)Math.Floor(Math.Min(p[a + 2], Math.Min(p[b + 2], p[c + 2])) / 2), z1 = (int)Math.Floor(Math.Max(p[a + 2], Math.Max(p[b + 2], p[c + 2])) / 2);
                    for (int x = x0; x <= x1; x++)
                        for (int z = z0; z <= z1; z++)
                        {
                            if (!cells.TryGetValue((x, z), out var list)) cells[(x, z)] = list = new List<int>();
                            list.Add(t);
                        }
                }
            }

            /// <summary>The faces over (x, z) within half a metre of height y (not a flyover above it).</summary>
            public IEnumerable<(double y, WorldMaterial m, int tri)> Near(double x, double z, double y) => At(x, z).Where(h => Math.Abs(h.y - y) < 0.5);

            /// <summary>Every face over (x, z): its height there and material.</summary>
            public List<(double y, WorldMaterial m, int tri)> At(double x, double z)
            {
                var hits = new List<(double, WorldMaterial, int)>();
                if (!cells.TryGetValue(((int)Math.Floor(x / 2), (int)Math.Floor(z / 2)), out var list)) return hits;
                var p = r.Positions;
                foreach (int t in list)
                {
                    int a = r.Indices[t] * 3, b = r.Indices[t + 1] * 3, c = r.Indices[t + 2] * 3;
                    double ax = p[a], az = p[a + 2], bx = p[b], bz = p[b + 2], cx = p[c], cz = p[c + 2];
                    double den = (bz - cz) * (ax - cx) + (cx - bx) * (az - cz);
                    double l1 = ((bz - cz) * (x - cx) + (cx - bx) * (z - cz)) / den, l2 = ((cz - az) * (x - cx) + (ax - cx) * (z - cz)) / den, l3 = 1 - l1 - l2;
                    if (l1 < -1e-6 || l2 < -1e-6 || l3 < -1e-6) continue;
                    hits.Add((l1 * p[a + 1] + l2 * p[b + 1] + l3 * p[c + 1], WorldMaterials.Of(r.Colors[r.Indices[t]]), t));
                }
                return hits;
            }
        }

        /// <summary>Distance from (x, z) to segment a-b and the parameter along it.</summary>
        internal static double SegDist(double x, double z, double ax, double az, double bx, double bz, out double t)
        {
            double dx = bx - ax, dz = bz - az, l2 = dx * dx + dz * dz;
            t = l2 > 0 ? ((x - ax) * dx + (z - az) * dz) / l2 : 0;
            double c = Math.Clamp(t, 0, 1), qx = ax + c * dx - x, qz = az + c * dz - z;
            return Math.Sqrt(qx * qx + qz * qz);
        }

        /// <summary>Does a drawn segment run the way its mapped way does? Chaining can turn a way round, and its left and right with it.</summary>
        internal static bool Along(Road road, ChunkRoads.RoadLine line)
        {
            double mx = (line.Ax + line.Bx) / 2, mz = (line.Az + line.Bz) / 2, best = double.MaxValue, dot = 0;
            for (int k = 0; k + 3 < road.Pts.Length; k += 2)
            {
                double ax = road.Pts[k] / 100.0, az = road.Pts[k + 1] / 100.0, bx = road.Pts[k + 2] / 100.0, bz = road.Pts[k + 3] / 100.0;
                double d = SegDist(mx, mz, ax, az, bx, bz, out _);
                if (d < best) { best = d; dot = (bx - ax) * (line.Bx - line.Ax) + (bz - az) * (line.Bz - line.Az); }
            }
            return dot >= 0;
        }

        /// <summary>Is (x, z) within reach of another way's carriageway at the road's level (its segments half a metre longer at each end)?</summary>
        internal static bool OnOtherRoad(DadcChunk c, Road road, double x, double z, double reach)
        {
            foreach (var o in c.Roads)
            {
                if (o.Id == road.Id || o.Pts == null || o.Cls == null || RoadPalette.IsFoot(o.Cls) || Math.Abs(o.YCm - road.YCm) >= 80) continue;
                double h = HalfOf(o) + reach;
                for (int k = 0; k + 3 < o.Pts.Length; k += 2)
                {
                    double ax = o.Pts[k] / 100.0, az = o.Pts[k + 1] / 100.0, bx = o.Pts[k + 2] / 100.0, bz = o.Pts[k + 3] / 100.0;
                    double len = Math.Sqrt((bx - ax) * (bx - ax) + (bz - az) * (bz - az));
                    if (len < 1e-6) continue;
                    double ex = (bx - ax) / len * 0.5, ez = (bz - az) / len * 0.5;
                    if (SegDist(x, z, ax - ex, az - ez, bx + ex, bz + ez, out _) < h) return true;
                }
            }
            return false;
        }

        /// <summary>Is (x, z) on a foot way as drawn (its half width, at least MinHalf)?</summary>
        internal static bool OnFootWay(DadcChunk c, ChunkRoads r, double x, double z)
        {
            // As drawn, within 5 cm: a foot way's ends are widened where it meets a road, and a
            // sharp bend's mitre reaches well past its half width (a bevel's wedge does not).
            var foot = new HashSet<long>(c.Roads.Where(o => RoadPalette.IsFoot(o.Cls)).Select(o => o.Id));
            foreach (var l in r.Lines)
            {
                if (!foot.Contains(l.Way)) continue;
                double d = SegDist(x, z, l.Ax, l.Az, l.Bx, l.Bz, out double t);
                if (d < l.HalfA + (l.HalfB - l.HalfA) * Math.Clamp(t, 0, 1) + 0.05) return true;
                double x0 = l.Ax + l.Sx * l.HalfA, z0 = l.Az + l.Sz * l.HalfA, x1 = l.Bx + l.Ex * l.HalfB, z1 = l.Bz + l.Ez * l.HalfB;
                double x2 = l.Bx - l.Ex * l.HalfB, z2 = l.Bz - l.Ez * l.HalfB, x3 = l.Ax - l.Sx * l.HalfA, z3 = l.Az - l.Sz * l.HalfA;
                if (InTriangle(x, z, x0, z0, x1, z1, x2, z2) || InTriangle(x, z, x0, z0, x2, z2, x3, z3)
                    || InTriangle(x, z, x0, z0, x1, z1, x3, z3) || InTriangle(x, z, x1, z1, x2, z2, x3, z3)) return true;
            }
            return false;
        }

        static bool InTriangle(double x, double z, double ax, double az, double bx, double bz, double cx, double cz)
        {
            double d1 = (bx - ax) * (z - az) - (bz - az) * (x - ax), d2 = (cx - bx) * (z - bz) - (cz - bz) * (x - bx), d3 = (ax - cx) * (z - cz) - (az - cz) * (x - cx);
            return d1 >= 0 && d2 >= 0 && d3 >= 0 || d1 <= 0 && d2 <= 0 && d3 <= 0;
        }

        /// <summary>
        /// Is a carriageway (not a foot way), as drawn, at the road's level at (x, z) or within
        /// reach further out along (nx, nz)? Where a road's end runs on across the chunk border,
        /// or a sharp bend's mitre reaches out, the mapped lines miss it.
        /// </summary>
        internal static bool OnDrawnRoad(DadcChunk c, ChunkRoads r, Probe probe, double x, double z, double nx, double nz, double reach, double y)
        {
            for (double d = 0; d <= reach + 1e-9; d += 0.1)
            {
                double px = x + nx * d, pz = z + nz * d;
                if (probe.Near(px, pz, y).Any(h => (h.m == WorldMaterial.Asphalt || h.m == WorldMaterial.Marking) && Math.Abs(h.y - y) < 0.02) && !OnFootWay(c, r, px, pz)) return true;
            }
            return false;
        }

        /// <summary>For each triangle of the road mesh, the piece it belongs to: triangles joined through shared vertices.</summary>
        internal static int[] Parts(ChunkRoads r)
        {
            var up = new int[r.Positions.Length / 3];
            for (int v = 0; v < up.Length; v++) up[v] = v;
            int Root(int v) { while (up[v] != v) v = up[v] = up[up[v]]; return v; }
            for (int t = 0; t < r.IndexCount; t += 3)
            {
                int a = Root(r.Indices[t]);
                up[Root(r.Indices[t + 1])] = a;
                up[Root(r.Indices[t + 2])] = a;
            }
            var part = new int[r.IndexCount / 3];
            for (int t = 0; t < part.Length; t++) part[t] = Root(r.Indices[3 * t]);
            return part;
        }

        /// <summary>Graph nodes where three or more edges meet, with the widest half width there.</summary>
        internal static List<(double x, double z, double half)> Junctions(DadcChunk c)
        {
            var degree = new Dictionary<int, int>();
            if (c.Edges != null) foreach (var e in c.Edges) { degree[e.A] = degree.GetValueOrDefault(e.A) + 1; degree[e.B] = degree.GetValueOrDefault(e.B) + 1; }
            var list = new List<(double, double, double)>();
            foreach (var (n, d) in degree)
            {
                if (d < 3 || n < 0 || n >= c.Nodes.Length) continue;
                double x = c.Nodes[n].East / 100.0, z = c.Nodes[n].North / 100.0, half = 0;
                foreach (var r in c.Roads)
                {
                    if (r.Pts == null || r.Cls == null || RoadPalette.IsFoot(r.Cls)) continue;
                    for (int k = 0; k + 1 < r.Pts.Length; k += 2)
                        if (Math.Abs(r.Pts[k] / 100.0 - x) < 0.05 && Math.Abs(r.Pts[k + 1] / 100.0 - z) < 0.05) half = Math.Max(half, HalfOf(r));
                }
                list.Add((x, z, half));
            }
            return list;
        }

        /// <summary>Distance along (nx, nz) from (px, pz) to the nearest building wall, up to max.</summary>
        internal static double WallGap(DadcChunk c, double px, double pz, double nx, double nz, double max)
        {
            double best = max;
            foreach (var b in c.Buildings)
            {
                int n = b.Outer.Length / 2;
                for (int k = 0; k < n; k++)
                {
                    int j = (k + 1) % n;
                    double ax = b.Outer[2 * k] / 100.0, az = b.Outer[2 * k + 1] / 100.0, wx = b.Outer[2 * j] / 100.0 - ax, wz = b.Outer[2 * j + 1] / 100.0 - az;
                    if (Math.Min(ax, ax + wx) > px + max + 1 || Math.Max(ax, ax + wx) < px - max - 1 || Math.Min(az, az + wz) > pz + max + 1 || Math.Max(az, az + wz) < pz - max - 1) continue;
                    double den = nx * wz - nz * wx;
                    if (Math.Abs(den) < 1e-12) continue;
                    double t = ((ax - px) * wz - (az - pz) * wx) / den, u = ((ax - px) * nz - (az - pz) * nx) / den;
                    if (t >= 0 && t < best && u >= 0 && u <= 1) best = t;
                }
            }
            return best;
        }

        /// <summary>Is (x, z) inside a building footprint (even-odd over its outer ring and holes)?</summary>
        internal static bool InBuilding(DadcChunk c, double x, double z)
        {
            foreach (var b in c.Buildings)
            {
                bool inside = Inside(b.Outer, x, z);
                if (inside && b.Holes != null) foreach (var h in b.Holes) inside ^= Inside(h, x, z);
                if (inside) return true;
            }
            return false;

            static bool Inside(int[] ring, double x, double z)
            {
                bool inside = false;
                int n = ring.Length / 2;
                for (int k = 0, j = n - 1; k < n; j = k++)
                {
                    double az = ring[2 * k + 1] / 100.0, bz = ring[2 * j + 1] / 100.0;
                    if ((az > z) == (bz > z)) continue;
                    double ax = ring[2 * k] / 100.0, bx = ring[2 * j] / 100.0;
                    if (ax + (z - az) * (bx - ax) / (bz - az) > x) inside = !inside;
                }
                return inside;
            }
        }

        [Test]
        public void SidewalkRuleFollowsTheTagAndTheWidth()
        {
            var cases = new (string cls, string tag, double width, bool left, bool right)[]
            {
                ("primary", "both", 4, true, true),          // tagged, even on a narrow road
                ("trunk", "left", 14, true, false),
                ("residential", "right", 5, false, true),
                ("primary", "no", 11, false, false),
                ("primary", "separate", 11, false, false),
                ("secondary", null, 9, true, true),          // width rule
                ("residential", null, 6, true, true),
                ("residential", null, 5.9, false, false),
                ("motorway", null, 16, false, false),
                ("construction", null, 8, false, false),
                ("footway", null, 6, false, false),
            };
            foreach (var k in cases)
            {
                ChunkRoads.WalkSidesFor(k.cls, k.tag, k.width, out bool l, out bool r);
                Assert.AreEqual((k.left, k.right), (l, r), $"{k.cls} {k.tag ?? "untagged"} {k.width} m");
            }
            Assert.AreEqual(2.5, ChunkRoads.WalkWidth("trunk"));
            Assert.AreEqual(2.0, ChunkRoads.WalkWidth("tertiary"));
            Assert.AreEqual(1.5, ChunkRoads.WalkWidth("residential"));
        }

        // MissedCover: a band's width is sampled along the road, every WalkSample metres and
        // beside the corners of what is near, so it stops at most a sample spacing short of an
        // obstacle it only meets between samples (another band just past the probe, the end of a
        // band cut along an obstacle at a slant); and a quad that would still overlap something
        // when halved down to SplitLength is left out. Neither ever overlaps (see
        // SidewalksNeitherOverlapNorCoverRoads); each costs a few points of cover, all logged.
        // At D-05: sidewalks 8772 of 8780 (99.91 %), drains 17652 of 17686 (99.81 %).
        const double MinWalkCover = 0.998, MinDrainCover = 0.997;

        [Test]
        public void EverySliceRoadHasItsSidewalksAtKerbHeight()
        {
            // Every 2 m along each wanted side of every at-grade slice road, as drawn (gentle bends
            // are filleted, up to about half a metre off the mapped line): 0.3 m outside the
            // carriageway edge the top face is paving KerbHeight above the road (or lower within a
            // crossing's ramps, or SlabSink lower on a broken slab), and 0.3 m inside it the top
            // face is the road. Which sides is read from the data. Points are left out where the
            // data leaves no room: within reach of another carriageway (mapped, or as drawn) or a
            // junction, with a building wall closer than MinWalk, or inside a building's footprint;
            // and within 1 m of the chunk border or 2 m of the segment's ends. A band whose outer
            // edge would leave the chunk is not built (K-33, a builder rule); those points are
            // counted apart, not as misses. A few points are missed (see MissedCover), so at
            // least MinWalkCover of them must be covered.
            LoadSlice();
            int wanted = 0, sampled = 0, covered = 0, ramps = 0, broken = 0, roadOk = 0, roadSampled = 0;
            var excused = new SortedDictionary<string, int>();
            var misses = new List<string>();
            foreach (var (key, c) in chunks)
            {
                var probe = new Probe(built[key]);
                var junctions = Junctions(c);
                var crossings = new List<(double x, double z)>();
                if (c.Points != null)
                    foreach (var p in c.Points)
                        if (p.CrossingType != null && ChunkRoads.MarkedCrossings.Contains(p.CrossingType)) crossings.Add((p.East / 100.0, p.North / 100.0));
                var signals = c.Nodes.Where(n => n.Signal.HasValue && n.Signal.Value != 0).Select(n => (x: n.East / 100.0, z: n.North / 100.0)).ToList();
                var roads = c.Roads.GroupBy(q => q.Id).ToDictionary(g => g.Key, g => g.First()); // a way can be in a chunk twice
                var seen = new HashSet<long>();
                foreach (var line in built[key].Lines)
                {
                    var road = roads[line.Way];
                    if (line.Bridge || road.Cls == null) continue;
                    double width = road.WidthCm / 100.0, y = line.Y;
                    ChunkRoads.WalkSidesFor(road.Cls, road.Sidewalk, road.ClassWidthCm / 100.0, out bool left, out bool right);
                    if (!Along(road, line)) (left, right) = (right, left);
                    if (!left && !right) continue;
                    if (seen.Add(road.Id)) wanted++;
                    double wr = ChunkRoads.WalkWidth(road.Cls);
                    {
                        double ax = line.Ax, az = line.Az, bx = line.Bx, bz = line.Bz;
                        double len = Math.Sqrt((bx - ax) * (bx - ax) + (bz - az) * (bz - az));
                        if (len < 4) continue;
                        double tx = (bx - ax) / len, tz = (bz - az) / len;
                        for (double s = 2; s <= len - 2; s += 2)
                            foreach (int side in new[] { 1, -1 })
                            {
                                if (side > 0 ? !left : !right) continue;
                                double half = line.HalfA + (line.HalfB - line.HalfA) * s / len;
                                double nx = -tz * side, nz = tx * side;
                                double ex = ax + tx * s + nx * half, ez = az + tz * s + nz * half;
                                double wx = ex + nx * 0.3, wz = ez + nz * 0.3, rx = ex - nx * 0.3, rz = ez - nz * 0.3;
                                string why = null;
                                double ox = ex + nx * wr, oz = ez + nz * wr;
                                if (Math.Min(Math.Min(wx, wz), Math.Min(rx, rz)) < 1 || Math.Max(Math.Max(wx, wz), Math.Max(rx, rz)) > 499) why = "chunk border";
                                else if (junctions.Any(j => Math.Sqrt((j.x - wx) * (j.x - wx) + (j.z - wz) * (j.z - wz)) < j.half + wr + 3)) why = "junction";
                                else if (OnOtherRoad(c, road, wx, wz, 0.3)) why = "another carriageway";
                                else if (Math.Min(ox, oz) < 0 || Math.Max(ox, oz) > 500) why = "outer edge leaves the chunk (K-33)";
                                else if (WallGap(c, ex, ez, nx, nz, ChunkRoads.MinWalk + 0.5) < ChunkRoads.MinWalk + 0.5) why = "building";
                                else if (InBuilding(c, ex + nx * 0.05, ez + nz * 0.05) || InBuilding(c, wx, wz)) why = "inside a building";
                                else if (OnDrawnRoad(c, built[key], probe, wx, wz, nx, nz, 0.3, y)) why = "another carriageway as drawn";
                                if (why != null) { excused[why] = excused.GetValueOrDefault(why) + 1; continue; }
                                sampled++;
                                bool nearCrossing = crossings.Any(q => Math.Sqrt((q.x - wx) * (q.x - wx) + (q.z - wz) * (q.z - wz)) < half + wr + 1 + ChunkRoads.ZebraDepth / 2 + ChunkRoads.RampLength)
                                    || signals.Any(q => Math.Sqrt((q.x - wx) * (q.x - wx) + (q.z - wz) * (q.z - wz)) < 30);
                                var top = probe.Near(wx, wz, y).OrderByDescending(h => h.y).FirstOrDefault();
                                bool kerb = top.m == WorldMaterial.Paving && Math.Abs(top.y - (y + ChunkRoads.KerbHeight)) < 0.005;
                                bool ramp = nearCrossing && top.m == WorldMaterial.Paving && top.y >= y + ChunkRoads.RampLift - 0.005 && top.y < y + ChunkRoads.KerbHeight;
                                bool slab = top.m == WorldMaterial.Paving && Math.Abs(top.y - (y + ChunkRoads.KerbHeight - ChunkRoads.SlabSink)) < 0.005;
                                if (kerb) covered++;
                                else if (ramp) { covered++; ramps++; }
                                else if (slab) { covered++; broken++; }
                                else if (misses.Count < 12) misses.Add($"c_{key.Item1}_{key.Item2} {road.Id} ({road.Cls}, side {side}) at {wx:F1}, {wz:F1}: top {(top.tri == 0 && top.y == 0 ? "none" : $"{top.m} {top.y - y:F3} above the road")}");
                                // The carriageway beside it is not covered.
                                var under = probe.Near(rx, rz, y).OrderByDescending(h => h.y).FirstOrDefault();
                                roadSampled++;
                                if (under.m != WorldMaterial.Paving && under.y < y + 0.05) roadOk++;
                                else if (misses.Count < 12) misses.Add($"c_{key.Item1}_{key.Item2} {road.Id} carriageway at {rx:F1}, {rz:F1} covered by {under.m} {under.y - y:F3}");
                            }
                    }
                }
            }
            Debug.Log($"Sidewalk coverage: {wanted} roads with sidewalks wanted; {sampled} points sampled, {covered} with a sidewalk ({ramps} on a ramp, {broken} on a broken slab); carriageway clear at {roadOk} of {roadSampled}; left out: " +
                      string.Join(", ", excused.Select(p => $"{p.Key} {p.Value}")) + (misses.Count > 0 ? "; misses: " + string.Join("; ", misses) : ""));
            Assert.Greater(sampled, 1000);
            Assert.GreaterOrEqual(covered, sampled * MinWalkCover, "sampled points with their sidewalk");
            Assert.AreEqual(roadSampled, roadOk, "no sidewalk over a carriageway");
        }

        [Test]
        public void EverySidewalkEdgeIsClosedByAFace()
        {
            // The top's outline edges (edges used by one top triangle only, matched by position)
            // must each be the top edge of a vertical face that reaches the road or the ground,
            // except on the chunk border, where the neighbour carries on. Tops are the paving and
            // the open drains (channels and lips); a face's top or bottom edge closes them.
            LoadSlice();
            int edges = 0, closed = 0, border = 0;
            var misses = new List<string>();
            foreach (var (key, r) in built)
            {
                var p = r.Positions;
                (long, long, long) K(int v) => ((long)Math.Round(p[v * 3] * 1000), (long)Math.Round(p[v * 3 + 1] * 1000), (long)Math.Round(p[v * 3 + 2] * 1000));
                ((long, long, long), (long, long, long)) E(int a, int b) { var ka = K(a); var kb = K(b); return ka.CompareTo(kb) < 0 ? (ka, kb) : (kb, ka); }
                var topUse = new Dictionary<((long, long, long), (long, long, long)), int>();
                var faceTops = new Dictionary<((long, long, long), (long, long, long)), double>();
                double roadY = 0.28;
                for (int t = 0; t < r.IndexCount; t += 3)
                {
                    int a = r.Indices[t], b = r.Indices[t + 1], c = r.Indices[t + 2];
                    var m = WorldMaterials.Of(r.Colors[a]);
                    double area = (p[b * 3] - p[a * 3]) * (p[c * 3 + 2] - p[a * 3 + 2]) - (p[b * 3 + 2] - p[a * 3 + 2]) * (p[c * 3] - p[a * 3]);
                    bool vertical = Math.Abs(area) < 1e-6;
                    bool lip = m == WorldMaterial.Concrete && Math.Abs(p[a * 3 + 1] - (roadY + ChunkRoads.DrainLipHeight)) < 1e-3;
                    if (!vertical && ((m == WorldMaterial.Paving && p[a * 3 + 1] > roadY + 0.005 - 1e-4 && p[a * 3 + 1] < 1) || m == WorldMaterial.Water || lip))
                    {
                        foreach (var (u, v) in new[] { (a, b), (b, c), (c, a) }) { var e = E(u, v); topUse[e] = topUse.GetValueOrDefault(e) + 1; }
                    }
                    else if (vertical && m == WorldMaterial.Concrete)
                    {
                        double low = Math.Min(p[a * 3 + 1], Math.Min(p[b * 3 + 1], p[c * 3 + 1]));
                        foreach (var (u, v) in new[] { (a, b), (b, c), (c, a) })
                        {
                            var e = E(u, v);
                            faceTops[e] = Math.Min(faceTops.TryGetValue(e, out var old) ? old : double.MaxValue, low);
                        }
                    }
                }
                // A face's quad is two triangles: the top edge's own triangle may not hold the bottom; take the lowest over the quad.
                foreach (var (e, n) in topUse)
                {
                    if (n != 1) continue;
                    double ax = e.Item1.Item1 / 1000.0, az = e.Item1.Item3 / 1000.0, bx = e.Item2.Item1 / 1000.0, bz = e.Item2.Item3 / 1000.0;
                    bool onBorder = (Math.Abs(ax - bx) < 0.002 && (Math.Abs(ax) < 0.002 || Math.Abs(ax - 500) < 0.002)) || (Math.Abs(az - bz) < 0.002 && (Math.Abs(az) < 0.002 || Math.Abs(az - 500) < 0.002));
                    if (onBorder) { border++; continue; }
                    if (Math.Abs(ax - bx) + Math.Abs(az - bz) < 0.002) continue; // a sliver's edge
                    edges++;
                    if (faceTops.ContainsKey(e)) closed++;
                    else if (misses.Count < 10) misses.Add($"c_{key.Item1}_{key.Item2} edge {ax:F2},{az:F2} to {bx:F2},{bz:F2} at {e.Item1.Item2 / 1000.0:F3}");
                }
            }
            Debug.Log($"Sidewalk edges: {edges} outline edges, {closed} closed by a face, {border} on the chunk border" + (misses.Count > 0 ? "; open: " + string.Join("; ", misses) : ""));
            Assert.Greater(edges, 1000);
            Assert.AreEqual(edges, closed, "every sidewalk outline edge has its face");
        }

        [Test]
        public void SidewalksNeitherOverlapNorCoverRoads()
        {
            // The middle of every sidewalk top triangle is covered by no other sidewalk top and lies
            // over no carriageway or pad. Foot ways are drawn like roads; a sidewalk may cover one
            // (where a mapped footway meets or runs along it), so those are counted apart. Bands
            // may meet within the builder's touch tolerance (Touch), so a cover counts only where
            // it is also found Touch either side of the middle, by faces other than the band's own.
            const double Touch = 0.01;
            LoadSlice();
            int tops = 0, overlaps = 0, overRoad = 0, overFoot = 0;
            var misses = new List<string>();
            foreach (var (key, r) in built)
            {
                var probe = new Probe(r);
                var part = Parts(r);
                var p = r.Positions;
                for (int t = 0; t < r.IndexCount; t += 3)
                {
                    int a = r.Indices[t], b = r.Indices[t + 1], c = r.Indices[t + 2];
                    var mat = WorldMaterials.Of(r.Colors[a]);
                    if (mat != WorldMaterial.Paving && mat != WorldMaterial.Water) continue;
                    double area = (p[b * 3] - p[a * 3]) * (p[c * 3 + 2] - p[a * 3 + 2]) - (p[b * 3 + 2] - p[a * 3 + 2]) * (p[c * 3] - p[a * 3]);
                    if (Math.Abs(area) < 1e-4) continue;
                    double x = (p[a * 3] + p[b * 3] + p[c * 3]) / 3, z = (p[a * 3 + 2] + p[b * 3 + 2] + p[c * 3 + 2]) / 3, y = (p[a * 3 + 1] + p[b * 3 + 1] + p[c * 3 + 1]) / 3;
                    tops++;
                    bool Band((double y, WorldMaterial m, int tri) h) => (h.m == WorldMaterial.Paving || h.m == WorldMaterial.Water) && Math.Abs(h.y - y) < 0.3;
                    // Paving is above the road it would cover; a channel below it.
                    bool Road((double y, WorldMaterial m, int tri) h) => (h.m == WorldMaterial.Asphalt || h.m == WorldMaterial.Concrete) && (mat == WorldMaterial.Paving ? y - h.y > 0 && y - h.y < 0.3 : h.y - y > 0 && h.y - y < 0.3);
                    bool Deep(Func<(double y, WorldMaterial m, int tri), bool> cover)
                    {
                        for (int k = 0; k < 4; k++)
                            if (!probe.At(x + (k == 0 ? Touch : k == 1 ? -Touch : 0), z + (k == 2 ? Touch : k == 3 ? -Touch : 0)).Any(h => part[h.tri / 3] != part[t / 3] && cover(h))) return false;
                        return true;
                    }
                    foreach (var h in probe.At(x, z))
                    {
                        if (h.tri == t) continue;
                        if (Band(h) && Deep(Band)) { overlaps++; if (misses.Count < 10) misses.Add($"c_{key.Item1}_{key.Item2} overlap at {x:F2},{z:F2}"); break; }
                        if (Road(h) && Deep(Road))
                        {
                            if (OnFootWay(chunks[key], r, x, z)) { overFoot++; break; }
                            overRoad++;
                            if (misses.Count < 10) misses.Add($"c_{key.Item1}_{key.Item2} over {h.m} at {x:F2},{z:F2}");
                            break;
                        }
                    }
                }
            }
            Debug.Log($"Sidewalk and drain tops: {tops} triangles, {overlaps} overlapping another, {overRoad} over a road, {overFoot} over a foot way; " +
                      $"{built.Values.Sum(b => b.Dropped)} of {built.Values.Sum(b => b.Dropped + b.Walks.Count)} quads left out by the builder" + (misses.Count > 0 ? "; " + string.Join("; ", misses) : ""));
            Assert.Greater(tops, 1000);
            Assert.AreEqual(0, overlaps, "overlapping sidewalks");
            Assert.AreEqual(0, overRoad, "sidewalk over a carriageway or pad");
        }

        [Test]
        public void EveryBusStopHasItsPad()
        {
            // Each mapped bus stop the builder placed on a road has stop-pad quads within reach
            // (StopNear beyond the carriageway edge, StopReach along it), whose top in the mesh is
            // the stop paving at kerb height; and every stop pad is near a mapped stop.
            LoadSlice();
            int stops = 0, padded = 0, pads = 0, stray = 0, meshOk = 0;
            var misses = new List<string>();
            foreach (var (key, c) in chunks)
            {
                var r = built[key];
                var probe = new Probe(r);
                var points = (c.Points ?? new PointFeature[0]).Where(p => p.Kind == "bus_stop").Select(p => (x: p.East / 100.0, z: p.North / 100.0, name: p.Name)).ToList();
                var mine = r.Walks.Where(w => w.Kind == ChunkRoads.WalkKind.Stop).ToList();
                double reach = 7 + ChunkRoads.StopNear + ChunkRoads.StopWidth + ChunkRoads.StopReach + 1;
                foreach (var (x, z, name) in points)
                {
                    stops++;
                    var near = mine.Where(w => Math.Sqrt(Math.Pow((w.InAx + w.OutBx) / 2 - x, 2) + Math.Pow((w.InAz + w.OutBz) / 2 - z, 2)) < reach).ToList();
                    if (near.Count > 0) padded++;
                    else misses.Add($"c_{key.Item1}_{key.Item2} '{name}' at {x:F1}, {z:F1}: no pad");
                }
                foreach (var w in mine)
                {
                    pads++;
                    double cx = (w.InAx + w.InBx + w.OutAx + w.OutBx) / 4, cz = (w.InAz + w.InBz + w.OutAz + w.OutBz) / 4;
                    if (!points.Any(q => Math.Sqrt((q.x - cx) * (q.x - cx) + (q.z - cz) * (q.z - cz)) < reach)) stray++;
                    var top = probe.Near(cx, cz, w.RoadY).OrderByDescending(h => h.y).FirstOrDefault();
                    uint rgb = top.tri >= 0 && top.m == WorldMaterial.Paving ? r.Colors[r.Indices[top.tri]] : 0;
                    if (rgb == WorldMaterials.Pack(ChunkRoads.StopPad, WorldMaterial.Paving) && top.y > w.RoadY + ChunkRoads.RampLift - 0.005) meshOk++;
                    else if (misses.Count < 12) misses.Add($"c_{key.Item1}_{key.Item2} stop quad at {cx:F2}, {cz:F2}: top {top.m} {top.y - w.RoadY:F3}");
                }
            }
            int placed = built.Values.Sum(b => b.BusStops);
            Debug.Log($"Bus stops: {stops} mapped, {placed} placed on a road, {padded} with a stop pad; {pads} stop quads, {meshOk} with stop paving on top, {stray} away from a stop" + (misses.Count > 0 ? "; " + string.Join("; ", misses) : ""));
            Assert.Greater(stops, 0);
            Assert.AreEqual(placed, padded, "every placed stop has its pad");
            Assert.AreEqual(pads, meshOk, "stop quads are stop paving on top");
            Assert.AreEqual(0, stray, "stop pads away from any stop");
        }

        [Test]
        public void EveryLaneWithoutASidewalkHasItsDrains()
        {
            // As for sidewalks: every 2 m along each side of a residential, living street or
            // unclassified road with no sidewalk, as drawn, 0.3 m outside the carriageway edge the top is the
            // drain's channel, DrainDepth below the road (covered, DrainCover above it, near a
            // crossing). Left out where there is no room: the chunk border, a junction, another
            // carriageway (mapped, or as drawn) or a building wall within DrainWidth, inside a
            // building's footprint, or a sidewalk or stop already there. A few points are missed
            // (see MissedCover), so at least MinDrainCover of them must be covered.
            LoadSlice();
            int sides = 0, sampled = 0, open = 0, covered = 0, roadOk = 0, roadSampled = 0;
            var excused = new SortedDictionary<string, int>();
            var misses = new List<string>();
            foreach (var (key, c) in chunks)
            {
                var probe = new Probe(built[key]);
                var junctions = Junctions(c);
                var crossings = new List<(double x, double z)>();
                if (c.Points != null)
                    foreach (var p in c.Points)
                        if (p.CrossingType != null && ChunkRoads.MarkedCrossings.Contains(p.CrossingType)) crossings.Add((p.East / 100.0, p.North / 100.0));
                var signals = c.Nodes.Where(n => n.Signal.HasValue && n.Signal.Value != 0).Select(n => (x: n.East / 100.0, z: n.North / 100.0)).ToList();
                var roads = c.Roads.GroupBy(q => q.Id).ToDictionary(g => g.Key, g => g.First()); // a way can be in a chunk twice
                var seen = new HashSet<long>();
                foreach (var line in built[key].Lines)
                {
                    var road = roads[line.Way];
                    if (line.Bridge || !ChunkRoads.DrainClass(road.Cls)) continue;
                    double width = road.WidthCm / 100.0, y = line.Y;
                    ChunkRoads.WalkSidesFor(road.Cls, road.Sidewalk, road.ClassWidthCm / 100.0, out bool left, out bool right);
                    if (!Along(road, line)) (left, right) = (right, left);
                    if (left && right) continue;
                    if (seen.Add(road.Id)) sides += (left ? 0 : 1) + (right ? 0 : 1);
                    {
                        double ax = line.Ax, az = line.Az, bx = line.Bx, bz = line.Bz;
                        double len = Math.Sqrt((bx - ax) * (bx - ax) + (bz - az) * (bz - az));
                        if (len < 4) continue;
                        double tx = (bx - ax) / len, tz = (bz - az) / len;
                        for (double s = 2; s <= len - 2; s += 2)
                            foreach (int side in new[] { 1, -1 })
                            {
                                if (side > 0 ? left : right) continue;
                                double half = line.HalfA + (line.HalfB - line.HalfA) * s / len;
                                double nx = -tz * side, nz = tx * side;
                                double ex = ax + tx * s + nx * half, ez = az + tz * s + nz * half;
                                double wx = ex + nx * 0.3, wz = ez + nz * 0.3, rx = ex - nx * 0.3, rz = ez - nz * 0.3;
                                var top = probe.Near(wx, wz, y).OrderByDescending(h => h.y).FirstOrDefault();
                                string why = null;
                                if (Math.Min(Math.Min(wx, wz), Math.Min(rx, rz)) < 1 || Math.Max(Math.Max(wx, wz), Math.Max(rx, rz)) > 499) why = "chunk border";
                                else if (junctions.Any(j => Math.Sqrt((j.x - wx) * (j.x - wx) + (j.z - wz) * (j.z - wz)) < j.half + ChunkRoads.DrainWidth + 3)) why = "junction";
                                else if (OnOtherRoad(c, road, wx, wz, ChunkRoads.DrainWidth)) why = "another carriageway";
                                else if (WallGap(c, ex, ez, nx, nz, ChunkRoads.DrainWidth + 0.5) < ChunkRoads.DrainWidth + 0.5) why = "building";
                                else if (InBuilding(c, ex + nx * 0.05, ez + nz * 0.05) || InBuilding(c, wx, wz)) why = "inside a building";
                                else if (OnDrawnRoad(c, built[key], probe, wx, wz, nx, nz, ChunkRoads.DrainWidth, y)) why = "another carriageway as drawn";
                                else if (top.m == WorldMaterial.Paving) why = "a sidewalk or stop";
                                if (why != null) { excused[why] = excused.GetValueOrDefault(why) + 1; continue; }
                                sampled++;
                                bool nearCrossing = crossings.Any(q => Math.Sqrt((q.x - wx) * (q.x - wx) + (q.z - wz) * (q.z - wz)) < half + ChunkRoads.DrainWidth + 1 + ChunkRoads.ZebraDepth / 2 + ChunkRoads.RampLength)
                                    || signals.Any(q => Math.Sqrt((q.x - wx) * (q.x - wx) + (q.z - wz) * (q.z - wz)) < 30);
                                if (top.m == WorldMaterial.Water && Math.Abs(top.y - (y - ChunkRoads.DrainDepth)) < 0.005) open++;
                                else if (nearCrossing && top.m == WorldMaterial.Concrete && Math.Abs(top.y - (y + ChunkRoads.DrainCover)) < 0.005) covered++;
                                else if (misses.Count < 12) misses.Add($"c_{key.Item1}_{key.Item2} {road.Id} ({road.Cls}, side {side}) at {wx:F1}, {wz:F1}: top {(top.tri == 0 && top.y == 0 ? "none" : $"{top.m} {top.y - y:F3} above the road")}");
                                var under = probe.Near(rx, rz, y).OrderByDescending(h => h.y).FirstOrDefault();
                                roadSampled++;
                                // A speed breaker's paint may stand above the road; a drain's lip or cover may not.
                                if (under.m != WorldMaterial.Paving && under.m != WorldMaterial.Water && !(under.m == WorldMaterial.Concrete && under.y > y + 0.005)) roadOk++;
                                else if (misses.Count < 12) misses.Add($"c_{key.Item1}_{key.Item2} {road.Id} carriageway at {rx:F1}, {rz:F1} covered by {under.m} {under.y - y:F3}");
                            }
                    }
                }
            }
            Debug.Log($"Drain coverage: {sides} road sides with drains wanted, {built.Values.Sum(b => b.Drains)} segment sides built; {sampled} points sampled, {open} open, {covered} covered at a crossing; carriageway clear at {roadOk} of {roadSampled}; left out: " +
                      string.Join(", ", excused.Select(p => $"{p.Key} {p.Value}")) + (misses.Count > 0 ? "; misses: " + string.Join("; ", misses) : ""));
            Assert.Greater(sampled, 500);
            Assert.GreaterOrEqual(open + covered, sampled * MinDrainCover, "sampled points with their drain");
            Assert.AreEqual(roadSampled, roadOk, "no drain over a carriageway");
        }

        [Test]
        public void KerbsDropWhereZebrasMeetThem()
        {
            // Bars run along the road and sit side by side across it. Beside each outermost bar,
            // 1.1 m out from its centre line (past the margin and the leftover, onto any sidewalk
            // there), a sidewalk is at most RampLift above the road.
            LoadSlice();
            int ends = 0, onWalk = 0, lowered = 0;
            var misses = new List<string>();
            foreach (var (key, r) in built)
            {
                var probe = new Probe(r);
                var bars = r.Marks.Where(q => q.Kind == MarkKind.Zebra).ToList();
                foreach (var m in bars)
                {
                    double dx = m.Bx - m.Ax, dz = m.Bz - m.Az, len = Math.Sqrt(dx * dx + dz * dz);
                    double nx = -dz / len, nz = dx / len, mx = (m.Ax + m.Bx) / 2, mz = (m.Az + m.Bz) / 2, y = m.Y - ChunkRoads.MarkLift;
                    foreach (int side in new[] { 1, -1 })
                    {
                        double step = ChunkRoads.ZebraBar + ChunkRoads.ZebraGap;
                        if (bars.Any(o => o.Way == m.Way && Math.Abs((o.Ax + o.Bx) / 2 - (mx + side * nx * step)) < 0.05 && Math.Abs((o.Az + o.Bz) / 2 - (mz + side * nz * step)) < 0.05)) continue;
                        ends++;
                        double x = mx + side * nx * 1.1, z = mz + side * nz * 1.1;
                        var top = probe.Near(x, z, y).OrderByDescending(h => h.y).FirstOrDefault();
                        if (top.m != WorldMaterial.Paving || top.y < y + 0.001) continue;
                        onWalk++;
                        if (top.y <= y + ChunkRoads.RampLift + 0.01) lowered++;
                        else if (misses.Count < 10) misses.Add($"c_{key.Item1}_{key.Item2} way {m.Way} at {x:F2},{z:F2}: {top.y - y:F3} above the road");
                    }
                }
            }
            Debug.Log($"Kerb ramps: {ends} zebra ends, {onWalk} meet a sidewalk, {lowered} of them lowered" + (misses.Count > 0 ? "; " + string.Join("; ", misses) : ""));
            Assert.Greater(onWalk, 20);
            Assert.AreEqual(onWalk, lowered, "a kerb at a zebra");
        }
    }
}
