// D-04 exit evidence: road markings line up with the road data. On 20 marked slice roads the lane,
// centre and edge lines lie where the lanes tag (or the width), one-way and the half width put
// them, within 2 cm; every marking lies on its own carriageway and no line runs into a junction pad,
// zebra or stop line; zebras sit on the mapped crossings and span the road; stop lines at the
// signalled junction are on the inbound side; dashes keep their world phase, so they carry on across
// chunk borders; the rule-placed details are the same on every build and keep their rules.
// One-way here is read from the graph edges that repeat the way's own points, independently of the
// builder's nearest-segment match.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dad.World;
using NUnit.Framework;
using UnityEngine;

namespace Dad.Tests
{
    public class RoadMarkingTests
    {
        static string ChunkDir => Path.Combine(TestWorld.Root, "chunks");

        static Dictionary<(int ix, int iy), DadcChunk> chunks;
        static Dictionary<(int ix, int iy), ChunkRoads> built;

        static void LoadSlice()
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
                    chunks[(ix, iy)] = c;
                    built[(ix, iy)] = ChunkRoads.Build(c, keepMarks: true);
                }
        }

        static double Dist(double ax, double az, double bx, double bz) => Math.Sqrt((ax - bx) * (ax - bx) + (az - bz) * (az - bz));

        static bool IsVehicle(string cls) => cls != null && !RoadPalette.IsFoot(cls) && cls != "construction";

        static double HalfOf(Road r) => Math.Max(RoadPalette.MinHalf, r.WidthCm / 200.0);

        /// <summary>One-way of a road relative to its points: from a graph edge segment that repeats two of the way's points. 0 two-way, 2 when no edge repeats them.</summary>
        static int Oneway(DadcChunk c, Road r)
        {
            bool any = false;
            foreach (var e in c.Edges)
            {
                if (e.Way != r.Id || e.Oneway == 0 || e.Pts == null) continue;
                any = true;
                for (int j = 0; j + 3 < e.Pts.Length; j += 2)
                    for (int k = 0; k + 3 < r.Pts.Length; k += 2)
                    {
                        if (e.Pts[j] == r.Pts[k] && e.Pts[j + 1] == r.Pts[k + 1] && e.Pts[j + 2] == r.Pts[k + 2] && e.Pts[j + 3] == r.Pts[k + 3]) return Math.Sign(e.Oneway);
                        if (e.Pts[j] == r.Pts[k + 2] && e.Pts[j + 1] == r.Pts[k + 3] && e.Pts[j + 2] == r.Pts[k] && e.Pts[j + 3] == r.Pts[k + 1]) return -Math.Sign(e.Oneway);
                    }
            }
            return any ? 2 : 0;
        }

        [Test]
        public void LaneRuleFollowsTheTagAndTheWidth()
        {
            // (class, lanes tag, one-way, half width) -> lanes, centre boundary
            var cases = new (string cls, int tag, int dir, double half, int lanes, int centre)[]
            {
                ("primary", 4, 0, 7, 4, 2),      // tagged, 3.5 m lanes
                ("primary", 4, 0, 4, 2, 1),      // tagged 4 on 8 m: 2 m lanes, capped to an even 2
                ("secondary", 3, 1, 3, 2, -1),   // one-way, 3 tagged on 6 m: capped to 2
                ("primary", 2, 1, 3.5, 2, -1),   // one-way, tagged
                ("trunk", 0, 1, 5, 3, -1),       // one-way, 10 m: 3 lanes of at least 3 m
                ("tertiary", 0, 0, 6.5, 4, 2),   // two-way, 13 m: 4 lanes
                ("tertiary", 0, 0, 3.5, 2, 1),   // two-way, 7 m: 2 lanes
                ("tertiary", 0, 0, 2.5, 2, 1),   // two-way, 5 m: 2 lanes
                ("tertiary", 0, 0, 2, 1, -1),    // two-way, 4 m: no centre line
                ("primary", 3, 0, 6, 3, 1),      // tagged odd on a two-way road: the boundary after the first
            };
            foreach (var t in cases)
            {
                int n = ChunkRoads.LaneCount(t.cls, t.tag, t.dir, t.half, out int centre);
                Assert.AreEqual(t.lanes, n, $"{t.cls} lanes={t.tag} one-way={t.dir} half={t.half}");
                Assert.AreEqual(t.centre, centre, $"{t.cls} lanes={t.tag} one-way={t.dir} half={t.half}: centre");
            }
        }

        [Test]
        public void TwentyMarkedRoadsHaveTheirLanes()
        {
            // The middle of each road's longest segment (30 m or more, its middle clear of the chunk
            // edge) is never moved by a fillet. Every line mark of the road that lies along that
            // segment's straight part must sit at an expected offset (signed distance from the data
            // centreline) within 2 cm, with the expected kind, width and dashing; and every expected
            // line must be there. A road the builder drew reversed (chained) mirrors the offsets.
            // An expected line whose middle lies on another carriageway of equal or higher class rank
            // at the same level (overlapping twins, a link on its road) must instead be absent there.
            LoadSlice();
            var all = new List<((int, int) chunk, Road road, int seg, int dir)>();
            int noEdge = 0;
            foreach (var (key, c) in chunks)
                foreach (var r in c.Roads)
                {
                    if (RoadPalette.IsFootSpan(r) || r.Pts == null || r.Pts.Length < 4) continue;
                    double half = HalfOf(r);
                    if (!RoadPalette.IsMajor(r.Cls, r.ClassWidthCm / 100.0) || !IsVehicle(r.Cls)) continue;
                    int pick = -1;
                    double longest = 0;
                    for (int k = 0; k + 3 < r.Pts.Length; k += 2)
                    {
                        double ax = r.Pts[k] / 100.0, az = r.Pts[k + 1] / 100.0, bx = r.Pts[k + 2] / 100.0, bz = r.Pts[k + 3] / 100.0;
                        double len = Dist(ax, az, bx, bz), mx = (ax + bx) / 2, mz = (az + bz) / 2, margin = half + 1;
                        if (len < 30 || len <= longest || mx < margin || mx > 500 - margin || mz < margin || mz > 500 - margin) continue;
                        longest = len;
                        pick = k;
                    }
                    if (pick < 0) continue;
                    int dir = Oneway(c, r);
                    if (dir == 2) { noEdge++; continue; }
                    all.Add((key, r, pick, dir));
                }
            all.Sort((a, b) => a.road.Id.CompareTo(b.road.Id));
            Assert.GreaterOrEqual(all.Count, 20, "marked slice roads with a 30 m segment");
            var misses = new List<string>();
            int marks = 0, expectedLines = 0, oneways = 0, covered = 0;
            var lanesSeen = new SortedDictionary<int, int>();
            for (int s = 0; s < 20; s++)
            {
                var (key, road, k, dir) = all[s * all.Count / 20];
                var r = built[key];
                double ax = road.Pts[k] / 100.0, az = road.Pts[k + 1] / 100.0, bx = road.Pts[k + 2] / 100.0, bz = road.Pts[k + 3] / 100.0;
                double len = Dist(ax, az, bx, bz), tx = (bx - ax) / len, tz = (bz - az) / len, nx = -tz, nz = tx, half = HalfOf(road);
                // The builder lays lines out on the segment's drawn half width at its middle, which is
                // less than the road's where an end is narrowed on the chunk border.
                foreach (var l in r.Lines)
                    if (l.Way == road.Id && ((Dist(l.Ax, l.Az, ax, az) < 0.01 && Dist(l.Bx, l.Bz, bx, bz) < 0.01) || (Dist(l.Ax, l.Az, bx, bz) < 0.01 && Dist(l.Bx, l.Bz, ax, az) < 0.01)))
                        half = (l.HalfA + l.HalfB) / 2;
                int n = ChunkRoads.LaneCount(road.Cls, road.Lanes ?? 0, dir, half, out int centre);
                lanesSeen[n] = lanesSeen.TryGetValue(n, out var seen) ? seen + 1 : 1;
                if (dir != 0) oneways++;
                var expected = new List<(double u, MarkKind kind, bool dashed, double width)>();
                for (int b = 1; b < n; b++)
                    expected.Add(((-1 + 2.0 * b / n) * half, b == centre ? MarkKind.CentreLine : MarkKind.LaneLine, !(b == centre && n >= 4), ChunkRoads.LaneLineWidth));
                if (half >= ChunkRoads.EdgeLinesFrom)
                    foreach (int side in new[] { -1, 1 }) expected.Add((side * (half - ChunkRoads.EdgeInset), MarkKind.EdgeLine, false, ChunkRoads.EdgeLineWidth));
                expectedLines += expected.Count;
                var found = new bool[expected.Count];
                var atMiddle = new bool[expected.Count];
                var on = new long[expected.Count];
                for (int e = 0; e < expected.Count; e++)
                    on[e] = CarriagewayUnder(chunks[key], road, (ax + bx) / 2 + nx * expected[e].u, (az + bz) / 2 + nz * expected[e].u);
                foreach (var m in r.Marks)
                {
                    if (m.Way != road.Id || m.Kind > MarkKind.EdgeLine) continue;
                    double ta = ((m.Ax - ax) * tx + (m.Az - az) * tz) / len, tb = ((m.Bx - ax) * tx + (m.Bz - az) * tz) / len;
                    double ua = (m.Ax - ax) * nx + (m.Az - az) * nz, ub = (m.Bx - ax) * nx + (m.Bz - az) * nz;
                    // Along this segment's straight part: within it, parallel to it, within the road.
                    // (A continuous line runs on to the mitre at a bend, a little past the segment's end.)
                    double reach = (half + 1) / len;
                    if (Math.Min(ta, tb) < -reach || Math.Max(ta, tb) > 1 + reach || Math.Abs(ua - ub) > 0.005 || Math.Abs(ua) > half + 0.5) continue;
                    marks++;
                    double u = (ua + ub) / 2;
                    int match = -1;
                    for (int e = 0; e < expected.Count && match < 0; e++)
                        if (expected[e].kind == m.Kind && Math.Abs(u - expected[e].u) < 0.02) match = e;
                    for (int e = 0; e < expected.Count && match < 0; e++)
                        if (expected[e].kind == m.Kind && Math.Abs(u + expected[e].u) < 0.02) match = e;
                    if (match < 0) { misses.Add($"{road.Id} ({road.Cls}, {n} lanes, one-way {dir}): {m.Kind} at {u:F3} m"); continue; }
                    found[match] = true;
                    if (Math.Min(ta, tb) <= 0.5 && Math.Max(ta, tb) >= 0.5) atMiddle[match] = true;
                    if (m.Dashed != expected[match].dashed || Math.Abs(m.Width - expected[match].width) > 1e-9)
                        misses.Add($"{road.Id}: {m.Kind} at {u:F3} m dashed {m.Dashed}, width {m.Width}");
                }
                for (int e = 0; e < expected.Count; e++)
                {
                    if (on[e] != 0)
                    {
                        covered++;
                        if (atMiddle[e]) misses.Add($"{road.Id} ({road.Cls}): {expected[e].kind} at {expected[e].u:F3} m runs on over way {on[e]}");
                    }
                    else if (!found[e]) misses.Add($"{road.Id} ({road.Cls}, {n} lanes, one-way {dir}, half {half:F2}): no {expected[e].kind} at {expected[e].u:F3} m");
                }
            }
            Debug.Log($"Marking spot check: {all.Count} candidates ({noEdge} one-way roads without a matching edge left out), 20 checked, {oneways} one-way; " +
                      $"lanes {string.Join(", ", lanesSeen.Select(p => $"{p.Key}: {p.Value}"))}; {expectedLines} expected lines ({covered} on another carriageway), {marks} marks on the straight parts" +
                      (misses.Count > 0 ? "; misses: " + string.Join("; ", misses.Take(20)) : ""));
            CollectionAssert.IsEmpty(misses, "every line where the data puts it, within 2 cm");
            Assert.Greater(marks, 100);
        }

        /// <summary>The way whose carriageway (0.2 m inside its edges, between its segment ends) holds the point, among the other vehicle ways of equal or higher class rank at the road's level; 0 when none does.</summary>
        static long CarriagewayUnder(DadcChunk c, Road road, double x, double z)
        {
            int rank = ChunkRoads.ClassRank(road.Cls);
            foreach (var o in c.Roads)
            {
                if (o.Id == road.Id || o.Pts == null || !IsVehicle(o.Cls) || ChunkRoads.ClassRank(o.Cls) < rank || Math.Abs(o.YCm - road.YCm) >= 80) continue;
                double h = HalfOf(o) - 0.2;
                for (int k = 0; k + 3 < o.Pts.Length; k += 2)
                {
                    double ax = o.Pts[k] / 100.0, az = o.Pts[k + 1] / 100.0, bx = o.Pts[k + 2] / 100.0, bz = o.Pts[k + 3] / 100.0;
                    double dx = bx - ax, dz = bz - az, l2 = dx * dx + dz * dz;
                    if (l2 < 1e-6) continue;
                    double t = ((x - ax) * dx + (z - az) * dz) / l2;
                    if (t <= 0 || t >= 1) continue;
                    double qx = ax + t * dx - x, qz = az + t * dz - z;
                    if (qx * qx + qz * qz < h * h) return o.Id;
                }
            }
            return 0;
        }

        /// <summary>Flat carriageway and pad triangles (asphalt or concrete), bucketed by 5 m cells.</summary>
        sealed class Surface
        {
            readonly ChunkRoads r;
            readonly Dictionary<(int, int), List<int>> cells = new Dictionary<(int, int), List<int>>();

            public Surface(ChunkRoads r)
            {
                this.r = r;
                var p = r.Positions;
                for (int t = 0; t < r.IndexCount; t += 3)
                {
                    int a = r.Indices[t], b = r.Indices[t + 1], c = r.Indices[t + 2];
                    var m = WorldMaterials.Of(r.Colors[a]);
                    if (m != WorldMaterial.Asphalt && m != WorldMaterial.Concrete) continue;
                    if (p[a * 3 + 1] != p[b * 3 + 1] || p[a * 3 + 1] != p[c * 3 + 1]) continue;
                    int x0 = (int)Math.Floor(Math.Min(p[a * 3], Math.Min(p[b * 3], p[c * 3])) / 5), x1 = (int)Math.Floor(Math.Max(p[a * 3], Math.Max(p[b * 3], p[c * 3])) / 5);
                    int z0 = (int)Math.Floor(Math.Min(p[a * 3 + 2], Math.Min(p[b * 3 + 2], p[c * 3 + 2])) / 5), z1 = (int)Math.Floor(Math.Max(p[a * 3 + 2], Math.Max(p[b * 3 + 2], p[c * 3 + 2])) / 5);
                    for (int x = x0; x <= x1; x++)
                        for (int z = z0; z <= z1; z++)
                        {
                            if (!cells.TryGetValue((x, z), out var list)) cells[(x, z)] = list = new List<int>();
                            list.Add(t);
                        }
                }
            }

            /// <summary>Is (x, z) on a carriageway or pad at road level y (a pad sits PadLift above)?</summary>
            public bool On(double x, double z, double y)
            {
                if (!cells.TryGetValue(((int)Math.Floor(x / 5), (int)Math.Floor(z / 5)), out var list)) return false;
                var p = r.Positions;
                foreach (int t in list)
                {
                    int a = r.Indices[t] * 3, b = r.Indices[t + 1] * 3, c = r.Indices[t + 2] * 3;
                    double ty = p[a + 1];
                    if (ty < y - 1e-3 || ty > y + RoadPalette.PadLift + 1e-3) continue;
                    double d1 = (x - p[b]) * (p[a + 2] - p[b + 2]) - (p[a] - p[b]) * (z - p[b + 2]);
                    double d2 = (x - p[c]) * (p[b + 2] - p[c + 2]) - (p[b] - p[c]) * (z - p[c + 2]);
                    double d3 = (x - p[a]) * (p[c + 2] - p[a + 2]) - (p[c] - p[a]) * (z - p[a + 2]);
                    bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
                    if (!(neg && pos)) return true;
                }
                return false;
            }
        }

        static bool InsideRing(List<(double x, double z)> ring, double x, double z)
        {
            for (int i = 0; i < ring.Count; i++)
            {
                var p = ring[i];
                var q = ring[(i + 1) % ring.Count];
                if ((q.x - p.x) * (z - p.z) - (q.z - p.z) * (x - p.x) <= 0) return false;
            }
            return true;
        }

        [Test]
        public void EveryMarkingLiesOnItsCarriagewayAndOutOfKeepOuts()
        {
            // Each marking's corners (2 cm in) are on a carriageway or pad of its own level, in its
            // chunk or, where a line runs a little past the border, the neighbour's. No line's middle
            // or ends (2 cm in) and no detail's centre is inside a junction pad, zebra or stop line.
            LoadSlice();
            var surfaces = built.ToDictionary(p => p.Key, p => new Surface(p.Value));
            var counts = new int[Enum.GetValues(typeof(MarkKind)).Length];
            int points = 0, neighbour = 0, outsideSlice = 0;
            var off = new List<string>();
            var inKeepOut = new List<string>();
            foreach (var (key, r) in built)
            {
                foreach (var m in r.Marks)
                {
                    counts[(int)m.Kind]++;
                    double lift = m.Kind == MarkKind.SpeedBreaker ? ChunkRoads.BreakerHeight : ChunkRoads.MarkLift, y = m.Y - lift;
                    var probe = new List<(double x, double z)>();
                    if (m.Ax == m.Bx && m.Az == m.Bz)
                    {
                        // A disc: its centre and a ring at its largest radius (potholes reach 1.25 x).
                        double rad = m.Width / 2 * (m.Kind == MarkKind.Pothole ? 1.25 : 1) - 0.02;
                        probe.Add((m.Ax, m.Az));
                        for (int k = 0; k < 8; k++) probe.Add((m.Ax + Math.Cos(k * Math.PI / 4) * rad, m.Az + Math.Sin(k * Math.PI / 4) * rad));
                    }
                    else
                    {
                        double len = Dist(m.Ax, m.Az, m.Bx, m.Bz), tx = (m.Bx - m.Ax) / len, tz = (m.Bz - m.Az) / len, nx = -tz, nz = tx;
                        double w = m.Width / 2 - 0.02, inset = Math.Min(0.02, len / 2);
                        foreach (var (ex, ez, sign) in new[] { (m.Ax, m.Az, 1), (m.Bx, m.Bz, -1) })
                            foreach (int side in new[] { -1, 1 })
                                probe.Add((ex + sign * tx * inset + side * nx * w, ez + sign * tz * inset + side * nz * w));
                    }
                    foreach (var (x, z) in probe)
                    {
                        points++;
                        var at = key;
                        double lx = x, lz = z;
                        // A carriageway is only cut at the border where its centreline ends there, so
                        // round a bend it can reach past it: try the chunk's own first.
                        if ((x < 0 || x > 500 || z < 0 || z > 500) && !surfaces[key].On(x, z, y))
                        {
                            // Chunk iy grows southwards: north of this chunk is iy - 1.
                            at = (key.Item1 + (x < 0 ? -1 : x > 500 ? 1 : 0), key.Item2 + (z < 0 ? 1 : z > 500 ? -1 : 0));
                            lx = x - 500 * (at.Item1 - key.Item1);
                            lz = z + 500 * (at.Item2 - key.Item2);
                            if (!surfaces.ContainsKey(at)) { outsideSlice++; continue; }
                            neighbour++;
                        }
                        if (!surfaces[at].On(lx, lz, y) && off.Count < 400)
                        {
                            var entries = chunks[key].Roads.Where(q => q.Id == m.Way).Select(q => $"[{q.Pts[0] / 100.0:F1},{q.Pts[1] / 100.0:F1} .. {q.Pts[q.Pts.Length - 2] / 100.0:F1},{q.Pts[q.Pts.Length - 1] / 100.0:F1} {q.Cls} w{q.WidthCm / 100.0} y{q.YCm / 100.0}]");
                            off.Add($"c_{key.Item1}_{key.Item2} {m.Kind} way {m.Way} at {x:F2}, {z:F2} (mark {m.Ax:F2},{m.Az:F2} to {m.Bx:F2},{m.Bz:F2}, y {m.Y:F2}; road {string.Join(" ", entries)})");
                        }
                    }

                    var mids = new List<(double x, double z)>();
                    if (m.Kind <= MarkKind.EdgeLine)
                    {
                        double len = Dist(m.Ax, m.Az, m.Bx, m.Bz), tx = (m.Bx - m.Ax) / len, tz = (m.Bz - m.Az) / len;
                        mids.Add(((m.Ax + m.Bx) / 2, (m.Az + m.Bz) / 2));
                        mids.Add((m.Ax + tx * 0.02, m.Az + tz * 0.02));
                        mids.Add((m.Bx - tx * 0.02, m.Bz - tz * 0.02));
                    }
                    else if (m.Kind >= MarkKind.SpeedBreaker) mids.Add(((m.Ax + m.Bx) / 2, (m.Az + m.Bz) / 2));
                    foreach (var (x, z) in mids)
                        foreach (var (ring, ry) in r.KeepOuts)
                            if (Math.Abs(ry - y) < 0.8 && InsideRing(ring, x, z) && inKeepOut.Count < 50)
                                inKeepOut.Add($"c_{key.Item1}_{key.Item2} {m.Kind} way {m.Way} at {x:F2}, {z:F2}");
                }
            }
            var kinds = (MarkKind[])Enum.GetValues(typeof(MarkKind));
            Debug.Log($"Slice markings: {string.Join(", ", kinds.Select(k => $"{k} {counts[(int)k]}"))}; {points} corner points, {neighbour} checked in the neighbour, {outsideSlice} past the slice; " +
                      $"{off.Count} off the carriageway, {inKeepOut.Count} in a keep-out" +
                      (off.Count > 0 ? "; off: " + string.Join("; ", off.Take(15)) : "") + (inKeepOut.Count > 0 ? "; in: " + string.Join("; ", inKeepOut.Take(10)) : ""));
            foreach (var k in kinds) Assert.Greater(counts[(int)k], 0, $"some {k} in the slice");
            CollectionAssert.IsEmpty(off, "every marking on its carriageway");
            CollectionAssert.IsEmpty(inKeepOut, "no line or detail in a pad, zebra or stop line");
        }

        /// <summary>The zebras in a chunk's marks: consecutive bars 1 m apart. Centre, across span (outer edge to outer edge), way.</summary>
        static List<(double x, double z, double span, long way)> Zebras(ChunkRoads r)
        {
            var zebras = new List<(double, double, double, long)>();
            var bars = new List<RoadMark>();
            void Close()
            {
                if (bars.Count == 0) return;
                double x = bars.Average(b => (b.Ax + b.Bx) / 2), z = bars.Average(b => (b.Az + b.Bz) / 2);
                var f = bars[0];
                var l = bars[bars.Count - 1];
                zebras.Add((x, z, Dist((f.Ax + f.Bx) / 2, (f.Az + f.Bz) / 2, (l.Ax + l.Bx) / 2, (l.Az + l.Bz) / 2) + ChunkRoads.ZebraBar, f.Way));
                bars.Clear();
            }
            foreach (var m in r.Marks)
            {
                if (m.Kind != MarkKind.Zebra) { Close(); continue; }
                if (bars.Count > 0)
                {
                    var p = bars[bars.Count - 1];
                    if (p.Way != m.Way || Math.Abs(Dist((p.Ax + p.Bx) / 2, (p.Az + p.Bz) / 2, (m.Ax + m.Bx) / 2, (m.Az + m.Bz) / 2) - (ChunkRoads.ZebraBar + ChunkRoads.ZebraGap)) > 1e-6) Close();
                }
                bars.Add(m);
            }
            Close();
            return zebras;
        }

        [Test]
        public void ZebrasLieOnTheMappedCrossings()
        {
            // Every marked crossing on a vehicle road has a zebra centred within 0.5 m of it, its bars
            // spanning the road to within a bar and a gap of the 0.2 m margins.
            LoadSlice();
            int crossings = 0, onRoads = 0, ok = 0;
            var misses = new List<string>();
            foreach (var (key, c) in chunks)
            {
                var zebras = Zebras(built[key]);
                foreach (var p in c.Points ?? new PointFeature[0])
                {
                    if (p.CrossingType == null || !ChunkRoads.MarkedCrossings.Contains(p.CrossingType)) continue;
                    crossings++;
                    double px = p.East / 100.0, pz = p.North / 100.0;
                    // The road it is on, from the data.
                    Road on = null;
                    double best = double.MaxValue;
                    foreach (var r in c.Roads)
                    {
                        if (!IsVehicle(r.Cls) || r.Pts == null || (r.Bridge && r.YCm >= RoadPalette.ElevatedY * 100)) continue;
                        double d = DistToPolyline(r.Pts, px, pz);
                        if (d <= HalfOf(r) + 0.5 && d < best) { best = d; on = r; }
                    }
                    if (on == null) continue;
                    onRoads++;
                    var z = zebras.OrderBy(q => Dist(q.x, q.z, px, pz)).FirstOrDefault();
                    double half = HalfOf(on), want = 2 * (half - ChunkRoads.ZebraMargin);
                    if (zebras.Count == 0 || Dist(z.x, z.z, px, pz) > 0.5) { misses.Add($"{p.Id} ({p.CrossingType}) on {on.Id}: nearest zebra {(zebras.Count == 0 ? double.NaN : Dist(z.x, z.z, px, pz)):F2} m away"); continue; }
                    if (z.span > want + 1e-6 || z.span < want - ChunkRoads.ZebraBar - ChunkRoads.ZebraGap) { misses.Add($"{p.Id} on {on.Id}: spans {z.span:F2} m of {want:F2}"); continue; }
                    ok++;
                }
            }
            Debug.Log($"Slice crossings: {crossings} marked, {onRoads} on a vehicle road, {ok} with their zebra" + (misses.Count > 0 ? "; misses: " + string.Join("; ", misses) : ""));
            Assert.Greater(onRoads, 5);
            CollectionAssert.IsEmpty(misses);
        }

        static double DistToPolyline(int[] ptsCm, double x, double z) => ClosestOnPolyline(ptsCm, x, z, out _, out _, out _, out _);

        /// <summary>Distance along a centimetre polyline to the point closest to (x, z).</summary>
        static double ArcAlong(int[] ptsCm, double x, double z)
        {
            double best = double.MaxValue, arc = 0, run = 0;
            for (int k = 0; k + 3 < ptsCm.Length; k += 2)
            {
                double ax = ptsCm[k] / 100.0, az = ptsCm[k + 1] / 100.0, bx = ptsCm[k + 2] / 100.0, bz = ptsCm[k + 3] / 100.0;
                double dx = bx - ax, dz = bz - az, len = Math.Sqrt(dx * dx + dz * dz);
                double t = len > 0 ? Math.Max(0, Math.Min(1, ((x - ax) * dx + (z - az) * dz) / (len * len))) : 0;
                double d = Dist(x, z, ax + t * dx, az + t * dz);
                if (d < best) { best = d; arc = run + t * len; }
                run += len;
            }
            return arc;
        }

        /// <summary>Distance from (x, z) to a centimetre polyline, with the closest point and the unit direction of its segment.</summary>
        static double ClosestOnPolyline(int[] ptsCm, double x, double z, out double cx, out double cz, out double tx, out double tz)
        {
            double best = double.MaxValue;
            cx = cz = tx = tz = 0;
            for (int k = 0; k + 3 < ptsCm.Length; k += 2)
            {
                double ax = ptsCm[k] / 100.0, az = ptsCm[k + 1] / 100.0, bx = ptsCm[k + 2] / 100.0, bz = ptsCm[k + 3] / 100.0;
                double dx = bx - ax, dz = bz - az, len2 = dx * dx + dz * dz;
                if (len2 == 0) continue;
                double t = Math.Max(0, Math.Min(1, ((x - ax) * dx + (z - az) * dz) / len2));
                double d = Dist(x, z, ax + t * dx, az + t * dz);
                if (d < best)
                {
                    best = d;
                    cx = ax + t * dx;
                    cz = az + t * dz;
                    double len = Math.Sqrt(len2);
                    tx = dx / len;
                    tz = dz / len;
                }
            }
            return best;
        }

        [Test]
        public void StopLinesAreOnTheInboundSide()
        {
            // The signalled junction in c_8_-28: four one-way carriageways (two dual carriageways)
            // crossing. Traffic keeps left. A stop line on a two-way road covers the left half of the
            // inbound direction; on a one-way road, the whole width, and only where it flows in. The
            // box between the four signals carries no lines or details.
            LoadSlice();
            var key = (8, -28);
            var c = chunks[key];
            var r = built[key];
            var signals = c.Nodes.Where(n => n.Signal.HasValue && n.Signal.Value != 0).Select(n => (x: n.East / 100.0, z: n.North / 100.0)).ToList();
            Assert.AreEqual(4, signals.Count, "the four signal nodes of the junction");
            var lines = r.Marks.Where(m => m.Kind == MarkKind.StopLine).ToList();
            var misses = new List<string>();
            var report = new List<string>();
            foreach (var m in lines)
            {
                var road = c.Roads.First(q => q.Id == m.Way);
                double mx = (m.Ax + m.Bx) / 2, mz = (m.Az + m.Bz) / 2, half = HalfOf(road), len = Dist(m.Ax, m.Az, m.Bx, m.Bz);
                ClosestOnPolyline(road.Pts, mx, mz, out double px, out double pz, out double tx, out double tz);
                var s = signals.OrderBy(q => Dist(q.x, q.z, mx, mz)).First();
                double toward = Math.Sign((s.x - px) * tx + (s.z - pz) * tz);
                double dx = toward * tx, dz = toward * tz, offset = (mx - px) * -dz + (mz - pz) * dx;
                int dir = Oneway(c, road);
                report.Add($"{road.Id} ({road.Cls}, one-way {dir}) {Dist(mx, mz, s.x, s.z):F1} m out, offset {offset:F2}, length {len:F2}, half {half:F2}");
                if (dir == 2) { misses.Add($"{road.Id}: one-way edge not found"); continue; }
                if (dir != 0)
                {
                    if (dir * toward < 0) misses.Add($"{road.Id}: stop line on a one-way road flowing out");
                    if (Math.Abs(offset) > 0.15 || Math.Abs(len - 2 * (half - ChunkRoads.ZebraMargin)) > 0.1) misses.Add($"{road.Id}: one-way stop line offset {offset:F2}, length {len:F2}");
                }
                else if (Math.Abs(offset - (half - ChunkRoads.ZebraMargin) / 2) > 0.15 || Math.Abs(len - (half - ChunkRoads.ZebraMargin)) > 0.1)
                    misses.Add($"{road.Id}: two-way stop line offset {offset:F2} (left half wants {(half - ChunkRoads.ZebraMargin) / 2:F2}), length {len:F2}");
            }
            double cx = signals.Average(q => q.x), cz = signals.Average(q => q.z);
            var box = signals.OrderBy(q => Math.Atan2(q.z - cz, q.x - cx)).ToList();
            int inBox = 0;
            foreach (var m in r.Marks)
            {
                if (m.Kind == MarkKind.Zebra || m.Kind == MarkKind.StopLine) continue;
                double mx = (m.Ax + m.Bx) / 2, mz = (m.Az + m.Bz) / 2;
                bool inside = true;
                for (int i = 0; i < 4 && inside; i++)
                {
                    var p = box[i];
                    var q = box[(i + 1) % 4];
                    inside = (q.x - p.x) * (mz - p.z) - (q.z - p.z) * (mx - p.x) > 0;
                }
                if (inside && inBox++ < 5) misses.Add($"{m.Kind} of {m.Way} in the junction box at {mx:F1}, {mz:F1}");
            }
            Debug.Log($"c_8_-28 junction: {lines.Count} stop lines, {Zebras(r).Count} zebras, {inBox} marks in the box; " + string.Join("; ", report) + (misses.Count > 0 ? "; misses: " + string.Join("; ", misses) : ""));
            Assert.GreaterOrEqual(lines.Count, 2);
            CollectionAssert.IsEmpty(misses);
        }

        [Test]
        public void StopLinesKeepLeftOnTwoWayRoads()
        {
            // The slice's only signalled junction joins one-way carriageways, so the two-way case is
            // checked on a made-up chunk: two 10 m two-way primary roads crossing at a signal. Traffic
            // keeps left, so each approach's stop line lies on the left of the way in: from the east
            // (heading west) on the south half, from the west on the north, from the north on the
            // east, from the south on the west.
            var c = new DadcChunk
            {
                Ix = 0, Iy = -1,
                Roads = new[]
                {
                    new Road { Id = 1, Cls = "primary", WidthCm = 1000, YCm = 28, Pts = new[] { 10000, 25000, 25000, 25000, 40000, 25000 } },
                    new Road { Id = 2, Cls = "primary", WidthCm = 1000, YCm = 28, Pts = new[] { 25000, 10000, 25000, 25000, 25000, 40000 } },
                },
                Nodes = new[] { new GraphNode { East = 25000, North = 25000, Signal = 1 } },
                Edges = new GraphEdge[0],
            };
            var r = ChunkRoads.Build(c, keepMarks: true);
            var lines = r.Marks.Where(m => m.Kind == MarkKind.StopLine).Select(m => (x: (m.Ax + m.Bx) / 2, z: (m.Az + m.Bz) / 2, len: Dist(m.Ax, m.Az, m.Bx, m.Bz))).ToList();
            Debug.Log($"Two-way signal: {r.Pads} pad, {r.Zebras} zebras, stop lines at " + string.Join("; ", lines.Select(l => $"{l.x:F2}, {l.z:F2} ({l.len:F2} m)")));
            Assert.AreEqual(4, r.Zebras);
            Assert.AreEqual(4, lines.Count);
            bool Has(Func<double, double, bool> where) => lines.Count(l => where(l.x, l.z)) == 1;
            Assert.IsTrue(Has((x, z) => x > 255 && z < 250 && z > 245), "east approach: south half");
            Assert.IsTrue(Has((x, z) => x < 245 && z > 250 && z < 255), "west approach: north half");
            Assert.IsTrue(Has((x, z) => z > 255 && x > 250 && x < 255), "north approach: east half");
            Assert.IsTrue(Has((x, z) => z < 245 && x < 250 && x > 245), "south approach: west half");
            foreach (var l in lines) Assert.AreEqual(5 - ChunkRoads.ZebraMargin, l.len, 1e-6);
        }

        [Test]
        public void DashesCarryOnAcrossChunkBorders()
        {
            // No dash is longer than 3 m. Where a dashed line crosses a chunk border, its nearest
            // pieces on the two sides (in world coordinates, on one line) carry the pattern on: one dash
            // split by the border, 3 m together, or a 4.5 m gap from the last dash on one side to the
            // first on the other. (6 cm allowed: slivers under 5 cm are not drawn.) Dashes are counted
            // at their foot on the centreline, so round a bend the outside of a dash shortens and the
            // inside lengthens; a crossing that breaks the pattern is excused only when its road bends by
            // 2 degrees or more within 15 m of the border.
            LoadSlice();
            var near = new List<(double ax, double az, double bx, double bz, (int, int) chunk)>();
            int dashes = 0, tooLong = 0;
            foreach (var (key, r) in built)
            {
                var c = chunks[key];
                double ex = c.CornerEastCm / 100.0, nz = c.CornerNorthCm / 100.0;
                foreach (var m in r.Marks)
                {
                    if (m.Kind > MarkKind.EdgeLine || !m.Dashed) continue;
                    dashes++;
                    if (Dist(m.Ax, m.Az, m.Bx, m.Bz) > ChunkRoads.DashOn + 0.01) tooLong++;
                    double edge = Math.Min(Math.Min(Math.Min(m.Ax, m.Az), Math.Min(500 - m.Ax, 500 - m.Az)), Math.Min(Math.Min(m.Bx, m.Bz), Math.Min(500 - m.Bx, 500 - m.Bz)));
                    if (edge < 12) near.Add((ex + m.Ax, nz + m.Az, ex + m.Bx, nz + m.Bz, key));
                }
            }
            var bends = new List<(double x, double z)>();
            foreach (var (key, c) in chunks)
            {
                double ex = c.CornerEastCm / 100.0, nz = c.CornerNorthCm / 100.0;
                foreach (var r in c.Roads)
                {
                    if (r.Pts == null || r.Pts.Length < 6) continue;
                    int n = r.Pts.Length;
                    foreach (var (e, o, f) in new[] { (0, 2, 4), (n - 2, n - 4, n - 6) })
                    {
                        // A border end whose next point, under 15 m in, turns the road by 2 degrees or more.
                        bool onBorder = r.Pts[e] == 0 || r.Pts[e] == 50000 || r.Pts[e + 1] == 0 || r.Pts[e + 1] == 50000;
                        double ux = r.Pts[o] - r.Pts[e], uz = r.Pts[o + 1] - r.Pts[e + 1], vx = r.Pts[f] - r.Pts[o], vz = r.Pts[f + 1] - r.Pts[o + 1];
                        double turn = Math.Abs(Math.Atan2(ux * vz - uz * vx, ux * vx + uz * vz)) * 180 / Math.PI;
                        if (onBorder && Math.Sqrt(ux * ux + uz * uz) < 1500 && turn >= 2) bends.Add((ex + r.Pts[e] / 100.0, nz + r.Pts[e + 1] / 100.0));
                    }
                }
            }
            var excused = new List<string>();
            // Each piece's line: the pieces within 12 m on it (ends within 2 cm of it), in order along it.
            // Neighbours from different chunks are where the line crosses a border.
            int joined = 0, gaps = 0;
            var bad = new List<string>();
            var seen = new HashSet<(int, int)>();
            for (int i = 0; i < near.Count; i++)
            {
                var a = near[i];
                double len = Dist(a.ax, a.az, a.bx, a.bz), tx = (a.bx - a.ax) / len, tz = (a.bz - a.az) / len;
                var line = new List<(int index, double lo, double hi)>();
                for (int j = 0; j < near.Count; j++)
                {
                    var b = near[j];
                    double pb0 = (b.ax - a.ax) * -tz + (b.az - a.az) * tx, pb1 = (b.bx - a.ax) * -tz + (b.bz - a.az) * tx;
                    if (Math.Abs(pb0) > 0.02 || Math.Abs(pb1) > 0.02) continue;
                    double b0 = (b.ax - a.ax) * tx + (b.az - a.az) * tz, b1 = (b.bx - a.ax) * tx + (b.bz - a.az) * tz;
                    if (Math.Max(b0, b1) < -12 || Math.Min(b0, b1) > len + 12) continue;
                    line.Add((j, Math.Min(b0, b1), Math.Max(b0, b1)));
                }
                line.Sort((p, q) => p.lo.CompareTo(q.lo));
                for (int k = 0; k + 1 < line.Count; k++)
                {
                    var (p, plo, phi) = line[k];
                    var (q, qlo, qhi) = line[k + 1];
                    if (near[p].chunk.Equals(near[q].chunk) || !seen.Add((Math.Min(p, q), Math.Max(p, q)))) continue;
                    // A bend of this line's road: a border end within 15 m along the line and 8 m across it.
                    bool bent = bends.Any(e => Math.Abs((e.x - a.ax) * -tz + (e.z - a.az) * tx) < 8 && Math.Min(Dist(e.x, e.z, near[p].bx, near[p].bz), Dist(e.x, e.z, near[p].ax, near[p].az)) < 15);
                    var wrong = bent ? excused : bad;
                    double gap = qlo - phi;
                    string where = $"{near[p].chunk}/{near[q].chunk} at {near[p].bx:F1}, {near[p].bz:F1}";
                    if (Math.Abs(gap) < 0.01)
                    {
                        joined++;
                        double union = qhi - plo;
                        if (union > ChunkRoads.DashOn + 0.01 || union < ChunkRoads.DashOn - 0.06) wrong.Add($"{where}: split dash {union:F3} m");
                    }
                    else if (gap > 0)
                    {
                        gaps++;
                        if (Math.Abs(gap - (ChunkRoads.DashPeriod - ChunkRoads.DashOn)) > 0.06) wrong.Add($"{where}: gap {gap:F3} m");
                    }
                    else wrong.Add($"{where}: overlap {gap:F3} m");
                }
            }
            Debug.Log($"Slice dashes: {dashes} pieces, {tooLong} over 3 m; {near.Count} near a border; across borders {joined} split dashes and {gaps} gaps; {excused.Count} broken by a bend at the border ({string.Join("; ", excused)})" +
                      (bad.Count > 0 ? "; wrong: " + string.Join("; ", bad.Take(12)) : ""));
            Assert.AreEqual(0, tooLong);
            Assert.Greater(joined + gaps, 10, "dashed lines crossing chunk borders, to check");
            CollectionAssert.IsEmpty(bad, "the pattern carries on across every border");
        }

        [Test]
        public void RuleDetailsAreRepeatableAndKeepTheirRules()
        {
            // Built again, every chunk gives the same markings. Speed breakers are only on unmarked
            // residential, living-street, service and unclassified roads; manholes only on marked roads;
            // potholes only on unmarked minor roads. Breakers and manholes on one way in a chunk are at
            // least half their spacing apart along it (measured on the data, so 1 m is allowed for
            // fillets and trimmed ends).
            LoadSlice();
            var misses = new List<string>();
            int total = 0;
            foreach (var (key, r) in built)
            {
                var again = ChunkRoads.Build(chunks[key], keepMarks: true);
                Assert.AreEqual(r.Marks.Count, again.Marks.Count, $"c_{key.Item1}_{key.Item2} marks");
                Assert.AreEqual(r.VertexCount, again.VertexCount);
                for (int i = 0; i < r.Marks.Count; i++)
                    if (!r.Marks[i].Equals(again.Marks[i])) { misses.Add($"c_{key.Item1}_{key.Item2} mark {i} differs"); break; }
                var roads = chunks[key].Roads.GroupBy(q => q.Id).ToDictionary(g => g.Key, g => g.First());
                foreach (var m in r.Marks)
                {
                    total++;
                    var road = roads[m.Way];
                    bool marked = RoadPalette.IsMajor(road.Cls, road.ClassWidthCm / 100.0) && IsVehicle(road.Cls);
                    if (m.Kind == MarkKind.SpeedBreaker && (marked || !ChunkRoads.BreakerClasses.Contains(road.Cls))) misses.Add($"breaker on {road.Id} ({road.Cls})");
                    if (m.Kind == MarkKind.Manhole && !marked) misses.Add($"manhole on {road.Id} ({road.Cls})");
                    if (m.Kind == MarkKind.Pothole && (marked || !ChunkRoads.PotholeClasses.Contains(road.Cls))) misses.Add($"pothole on {road.Id} ({road.Cls})");
                }
                foreach (var (kind, period) in new[] { (MarkKind.SpeedBreaker, ChunkRoads.BreakerPeriod), (MarkKind.Manhole, ChunkRoads.ManholePeriod) })
                    foreach (var g in r.Marks.Where(m => m.Kind == kind).GroupBy(m => m.Way))
                    {
                        // A way can enter a chunk more than once; each entry is its own road.
                        var at = g.Select(m =>
                        {
                            double mx = (m.Ax + m.Bx) / 2, mz = (m.Az + m.Bz) / 2;
                            var road = chunks[key].Roads.Where(q => q.Id == g.Key).OrderBy(q => DistToPolyline(q.Pts, mx, mz)).First();
                            return (road, s: ArcAlong(road.Pts, mx, mz));
                        }).ToList();
                        for (int i = 0; i < at.Count; i++)
                            for (int j = i + 1; j < at.Count; j++)
                                if (at[i].road == at[j].road && Math.Abs(at[i].s - at[j].s) < period / 2 - 1)
                                    misses.Add($"{kind} on {g.Key} {Math.Abs(at[i].s - at[j].s):F1} m apart along it");
                    }
            }
            var sum = built.Values.Aggregate(new int[8], (a, r) =>
            {
                a[0] += r.MarkLines; a[1] += r.Zebras; a[2] += r.StopLines; a[3] += r.SpeedBreakers;
                a[4] += r.Manholes; a[5] += r.Grates; a[6] += r.Potholes; a[7] += r.LanesCapped;
                return a;
            });
            Debug.Log($"Slice details: {total} marks; line pieces {sum[0]}, zebras {sum[1]}, stop lines {sum[2]}, speed breakers {sum[3]}, manholes {sum[4]}, grates {sum[5]}, potholes {sum[6]}; " +
                      $"{sum[7]} roads with a lanes tag capped" + (misses.Count > 0 ? "; misses: " + string.Join("; ", misses.Take(20)) : ""));
            CollectionAssert.IsEmpty(misses);
        }
    }
}
