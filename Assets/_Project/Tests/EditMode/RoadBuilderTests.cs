// C-03 roads: outlines keep width and centre; a road split by a chunk border meets itself on the
// border without a gap; junctions in the slice are covered; 20 slice roads sit within 1 cm of their
// data; the mesh faces the right way.
using System;
using System.Collections.Generic;
using System.IO;
using Dad.World;
using NUnit.Framework;
using UnityEngine;

namespace Dad.Tests
{
    public class RoadBuilderTests
    {
        static string ChunkDir => Path.Combine(TestWorld.Root, "chunks");

        static DadcChunk Load(string id)
        {
            var f = Path.Combine(ChunkDir, id + ".dadc");
            Assume.That(File.Exists(f), "world data missing: run Tools/worldgen/build.sh");
            return Dadc.Load(File.ReadAllBytes(f));
        }

        static double Dist(double ax, double az, double bx, double bz) => Math.Sqrt((ax - bx) * (ax - bx) + (az - bz) * (az - bz));

        [Test]
        public void OutlinesKeepWidthCentreAndMitre()
        {
            var straight = ChunkRoads.MiterOutline(new double[] { 0, 0, 10, 0, 20, 0 }, 3);
            for (int i = 0; i < straight.Count; i++)
            {
                Assert.AreEqual(6, Dist(straight.Lx[i], straight.Lz[i], straight.Rx[i], straight.Rz[i]), 1e-9);
                Assert.AreEqual(straight.Cx[i], (straight.Lx[i] + straight.Rx[i]) / 2, 1e-9, "centre unmoved");
                Assert.AreEqual(3, straight.Lz[i], 1e-9, "left is north of an eastward road");
            }
            var bend = ChunkRoads.MiterOutline(new double[] { 0, 0, 10, 0, 10, 10 }, 2);
            Assert.AreEqual(3, bend.Count);
            Assert.AreEqual(2 * Math.Sqrt(2), Dist(bend.Cx[1], bend.Cz[1], bend.Lx[1], bend.Lz[1]), 1e-9, "a 90 degree mitre sits at half x sqrt 2");
            var hairpin = ChunkRoads.MiterOutline(new double[] { 0, 0, 10, 0, 0, 1 }, 2);
            Assert.AreEqual(4, hairpin.Count, "a hairpin is bevelled: two samples at the corner");
        }

        /// <summary>Road-level triangles of one chunk, bucketed on a 5 m grid for point queries.</summary>
        sealed class Surface
        {
            public readonly ChunkRoads Roads;
            public readonly double Ox, Oz;
            readonly Dictionary<(int, int), List<int>> grid = new Dictionary<(int, int), List<int>>();

            public Surface(DadcChunk c)
            {
                Roads = ChunkRoads.Build(c);
                Ox = c.CornerEastCm / 100.0;
                Oz = c.CornerNorthCm / 100.0;
                var p = Roads.Positions;
                for (int t = 0; t < Roads.Indices.Length; t += 3)
                {
                    int a = Roads.Indices[t] * 3, b = Roads.Indices[t + 1] * 3, cc = Roads.Indices[t + 2] * 3;
                    if (Math.Abs((p[b] - p[a]) * (p[cc + 2] - p[a + 2]) - (p[cc] - p[a]) * (p[b + 2] - p[a + 2])) < 1e-6) continue; // walls have no footprint; a ramp's slope does
                    int x0 = (int)Math.Floor(Math.Min(p[a], Math.Min(p[b], p[cc])) / 5), x1 = (int)Math.Floor(Math.Max(p[a], Math.Max(p[b], p[cc])) / 5);
                    int z0 = (int)Math.Floor(Math.Min(p[a + 2], Math.Min(p[b + 2], p[cc + 2])) / 5), z1 = (int)Math.Floor(Math.Max(p[a + 2], Math.Max(p[b + 2], p[cc + 2])) / 5);
                    for (int x = x0; x <= x1; x++)
                        for (int z = z0; z <= z1; z++)
                        {
                            if (!grid.TryGetValue((x, z), out var l)) grid[(x, z)] = l = new List<int>();
                            l.Add(t);
                        }
                }
            }

            /// <summary>Is the world point under a triangle at height y (carriageway level, not sidewalk or soffit)? NaN: at any height, for a ramp.</summary>
            public bool Covers(double wx, double wz, double y)
            {
                double x = wx - Ox, z = wz - Oz;
                if (!grid.TryGetValue(((int)Math.Floor(x / 5), (int)Math.Floor(z / 5)), out var l)) return false;
                var p = Roads.Positions;
                foreach (int t in l)
                {
                    int a = Roads.Indices[t] * 3, b = Roads.Indices[t + 1] * 3, c = Roads.Indices[t + 2] * 3;
                    if (!double.IsNaN(y) && Math.Abs(p[a + 1] - y) > 0.02) continue;
                    double d1 = (x - p[b]) * (p[a + 2] - p[b + 2]) - (p[a] - p[b]) * (z - p[b + 2]);
                    double d2 = (x - p[c]) * (p[b + 2] - p[c + 2]) - (p[b] - p[c]) * (z - p[c + 2]);
                    double d3 = (x - p[a]) * (p[c + 2] - p[a + 2]) - (p[c] - p[a]) * (z - p[a + 2]);
                    bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
                    if (!(neg && pos)) return true;
                }
                return false;
            }
        }

        static bool AcrossIsMissing(Dictionary<string, Surface> surfaces, double east, double north)
        {
            for (int dx = -1; dx <= 1; dx += 2)
                for (int dz = -1; dz <= 1; dz += 2)
                {
                    int ix = (int)Math.Floor((east + dx) / 500), iy = -((int)Math.Floor((north + dz) / 500) + 1);
                    if (!surfaces.ContainsKey($"c_{ix}_{iy}")) return true;
                }
            return false;
        }

        [Test]
        public void RoadsCrossChunkBordersWithoutGaps()
        {
            Assume.That(Directory.Exists(ChunkDir), "world data missing: run Tools/worldgen/build.sh");
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var surfaces = new Dictionary<string, Surface>();
            // Border point (world mm) and level -> the ends there. Matched by point, not id: OSM ways
            // often end on a chunk border, and the way across it has another id.
            var caps = new Dictionary<(long, long, long), List<(ChunkRoads.EndCap cap, Surface s, string chunk)>>();
            foreach (var f in Directory.GetFiles(Path.Combine(TestWorld.City, "chunks"), "*.dadc"))
            {
                var c = Dadc.Load(File.ReadAllBytes(f));
                var s = new Surface(c);
                surfaces[c.Id] = s;
                foreach (var cap in s.Roads.Caps)
                {
                    var key = ((long)Math.Round(cap.Y * 10), (long)Math.Round((s.Ox + cap.X) * 1000), (long)Math.Round((s.Oz + cap.Z) * 1000));
                    if (!caps.TryGetValue(key, out var list)) caps[key] = list = new List<(ChunkRoads.EndCap, Surface, string)>();
                    list.Add((cap, s, c.Id));
                }
            }
            int ends = 0, pairs = 0, single = 0, mapEdge = 0, crowded = 0, cutPairs = 0, extended = 0, samples = 0, gaps = 0;
            var alone = new List<string>();
            var within = new int[4];
            double worst = 0;
            string worstAt = "";
            var gapList = new List<string>();
            foreach (var kv in caps)
            {
                var l = kv.Value;
                ends += l.Count;
                if (l.Count == 1)
                {
                    // A street that runs to the edge of the map has nothing to meet: only a chunk that exists across the border can leave a gap.
                    double ex = l[0].s.Ox + l[0].cap.X, ez = l[0].s.Oz + l[0].cap.Z;
                    if (AcrossIsMissing(surfaces, ex, ez)) { mapEdge++; continue; }
                    single++;
                    alone.Add($"{l[0].chunk} {kv.Key}");
                    continue;
                }
                if (l.Count > 2 || l[0].chunk == l[1].chunk) { crowded++; continue; }
                pairs++;
                var (a, sa, ca) = l[0];
                var (b, sb, cb) = l[1];
                // Where the carriageway edges cross the border, in world metres; the neighbour runs
                // the other way, so its left edge is this one's right.
                double alx = sa.Ox + a.Lx, alz = sa.Oz + a.Lz, arx = sa.Ox + a.Rx, arz = sa.Oz + a.Rz;
                double blx = sb.Ox + b.Lx, blz = sb.Oz + b.Lz, brx = sb.Ox + b.Rx, brz = sb.Oz + b.Rz;
                double d = Math.Min(Math.Max(Dist(alx, alz, blx, blz), Dist(arx, arz, brx, brz)), Math.Max(Dist(alx, alz, brx, brz), Dist(arx, arz, blx, blz)));
                if (a.Extended || b.Extended) extended++;
                else
                {
                    cutPairs++;
                    if (d <= 0.001) within[0]++; else if (d <= 0.01) within[1]++; else if (d <= 0.05) within[2]++; else within[3]++;
                    if (d > worst) { worst = d; worstAt = $"{ca}/{cb} at {sa.Ox + a.X:F2}, {sa.Oz + a.Z:F2}"; }
                }
                // No gaps: across the section both chunks agree is road (the narrower one, 2 cm in
                // from its edges), points 1 cm either side of the border are road in one chunk or the other.
                double cx = sa.Ox + a.X, cz = sa.Oz + a.Z;
                bool alongZ = Math.Abs(alx - arx) < 1e-6; // border is a line of constant x
                double Along(double x, double z) => alongZ ? z : x;
                // Inset 5 cm square to the road edge: along a flat crossing that is 5 cm / sin(angle).
                double spanA = Math.Abs(Along(alx, alz) - Along(arx, arz)), spanB = Math.Abs(Along(blx, blz) - Along(brx, brz));
                double inset = 0.05 * Math.Max(spanA / (2 * a.Half), spanB / (2 * b.Half));
                double lo = Math.Max(Math.Min(Along(alx, alz), Along(arx, arz)), Math.Min(Along(blx, blz), Along(brx, brz))) + inset;
                double hi = Math.Min(Math.Max(Along(alx, alz), Along(arx, arz)), Math.Max(Along(blx, blz), Along(brx, brz))) - inset;
                for (int k = 0; k <= 8 && hi > lo; k++)
                {
                    double u = lo + (hi - lo) * k / 8;
                    foreach (double off in new[] { -0.01, 0.01 })
                    {
                        double x = alongZ ? cx + off : u, z = alongZ ? u : cz + off;
                        samples++;
                        if (sa.Covers(x, z, a.Ramp ? double.NaN : a.Y) || sb.Covers(x, z, b.Ramp ? double.NaN : b.Y)) continue;
                        gaps++;
                        if (gapList.Count < 10) gapList.Add($"{ca}/{cb} at {x:F2}, {z:F2}");
                    }
                }
            }
            Debug.Log($"Road borders over the city: {surfaces.Count} chunks, {ends} ends on a border; {pairs} pairs, {single} alone, {crowded} at border junctions; " +
                      $"{extended} flat crossings extended; of {cutPairs} cut pairs: <=1 mm {within[0]}, <=1 cm {within[1]}, <=5 cm {within[2]}, more {within[3]}, worst {worst:F3} m ({worstAt}); " +
                      $"gap samples {gaps} / {samples}; {sw.ElapsedMilliseconds} ms" + (gapList.Count > 0 ? "; gaps: " + string.Join(", ", gapList) : ""));
            Assert.AreEqual(TestWorld.CityChunks, surfaces.Count);
            Assert.Greater(pairs, 500);
            Assert.LessOrEqual(single, 10, "almost every border end has the other half across the border: " + string.Join("; ", alone));
            Assert.AreEqual(0, gaps, "no gaps where roads cross chunk borders");
            Assert.GreaterOrEqual(within[0] + within[1] + within[2], cutPairs * 0.99, "99 % of cut pairs meet within 5 cm");
        }

        [Test]
        public void SliceJunctionsAreCovered()
        {
            int junctions = 0, centres = 0, samples = 0, covered = 0;
            var misses = new List<string>();
            for (int ix = 6; ix <= 9; ix++)
                for (int iy = -30; iy <= -28; iy++)
                {
                    var c = Load($"c_{ix}_{iy}");
                    var r = ChunkRoads.Build(c);
                    var half = new Dictionary<long, double>();
                    var ground = new HashSet<long>();
                    foreach (var road in c.Roads)
                    {
                        half[road.Id] = Math.Max(RoadPalette.MinHalf, road.WidthCm / 200.0);
                        if (!road.Bridge) ground.Add(road.Id);
                    }
                    var degree = new int[c.Nodes.Length];
                    var narrow = new double[c.Nodes.Length];
                    var allGround = new bool[c.Nodes.Length];
                    for (int i = 0; i < narrow.Length; i++) { narrow[i] = double.MaxValue; allGround[i] = true; }
                    foreach (var e in c.Edges)
                        foreach (int n in new[] { e.A, e.B })
                        {
                            degree[n]++;
                            narrow[n] = Math.Min(narrow[n], half.TryGetValue(e.Way, out var h) ? h : RoadPalette.MinHalf);
                            if (!ground.Contains(e.Way)) allGround[n] = false;
                        }
                    for (int n = 0; n < c.Nodes.Length; n++)
                    {
                        double x = c.Nodes[n].East / 100.0, z = c.Nodes[n].North / 100.0;
                        if (degree[n] < 3 || !allGround[n] || x < 10 || z < 10 || x > 490 || z > 490) continue;
                        junctions++;
                        double rad = 0.6 * narrow[n];
                        bool centre = Covered(r, x, z);
                        if (centre) centres++;
                        else if (misses.Count < 10) misses.Add($"{c.Id} node {n} at {x:F2}, {z:F2}");
                        for (int k = 0; k < 8; k++)
                        {
                            double a = k * Math.PI / 4;
                            samples++;
                            if (Covered(r, x + Math.Cos(a) * rad, z + Math.Sin(a) * rad)) covered++;
                        }
                    }
                }
            Debug.Log($"Slice junctions: {junctions} at grade with 3+ arms; centre covered at {centres}; ring at 0.6 x the narrowest half width: {covered} / {samples} covered" +
                      (misses.Count > 0 ? "; centre misses: " + string.Join(", ", misses) : ""));
            Assert.Greater(junctions, 300);
            Assert.AreEqual(junctions, centres, "every junction centre is road surface");
            // Arms alone cover all but one sample (3,295 of 3,296 when this was written); pads close the last corner.
            Assert.AreEqual(samples, covered, "the ring around every junction is road surface");
        }

        /// <summary>Is (x, z) under a road-level triangle (0.2..1 m up, carriageway or pad)?</summary>
        static bool Covered(ChunkRoads r, double x, double z)
        {
            var p = r.Positions;
            var idx = r.Indices;
            for (int t = 0; t < idx.Length; t += 3)
            {
                int a = idx[t] * 3, b = idx[t + 1] * 3, c = idx[t + 2] * 3;
                if (p[a + 1] < 0.2f || p[a + 1] > 1f || p[a + 1] != p[b + 1] || p[a + 1] != p[c + 1]) continue;
                double d1 = (x - p[b]) * (p[a + 2] - p[b + 2]) - (p[a] - p[b]) * (z - p[b + 2]);
                double d2 = (x - p[c]) * (p[b + 2] - p[c + 2]) - (p[b] - p[c]) * (z - p[c + 2]);
                double d3 = (x - p[a]) * (p[c + 2] - p[a + 2]) - (p[c] - p[a]) * (z - p[a + 2]);
                bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
                if (!(neg && pos)) return true;
            }
            return false;
        }

        // Is (x, z) inside a triangle of the road mesh lying flat at height y?
        static bool CoveredAt(ChunkRoads r, double x, double z, double y)
        {
            var p = r.Positions;
            var idx = r.Indices;
            for (int t = 0; t < idx.Length; t += 3)
            {
                int a = idx[t] * 3, b = idx[t + 1] * 3, c = idx[t + 2] * 3;
                if (Math.Abs(p[a + 1] - y) > 1e-3 || Math.Abs(p[b + 1] - y) > 1e-3 || Math.Abs(p[c + 1] - y) > 1e-3) continue;
                double d1 = (x - p[b]) * (p[a + 2] - p[b + 2]) - (p[a] - p[b]) * (z - p[b + 2]);
                double d2 = (x - p[c]) * (p[b + 2] - p[c + 2]) - (p[b] - p[c]) * (z - p[c + 2]);
                double d3 = (x - p[a]) * (p[c + 2] - p[a + 2]) - (p[c] - p[a]) * (z - p[a + 2]);
                bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
                if (!(neg && pos)) return true;
            }
            return false;
        }

        static double DistToPolyline(int[] ptsCm, double x, double z)
        {
            double best = double.MaxValue;
            for (int k = 0; k + 3 < ptsCm.Length; k += 2)
            {
                double ax = ptsCm[k] / 100.0, az = ptsCm[k + 1] / 100.0, bx = ptsCm[k + 2] / 100.0, bz = ptsCm[k + 3] / 100.0;
                double dx = bx - ax, dz = bz - az, len2 = dx * dx + dz * dz;
                double t = len2 > 0 ? Math.Max(0, Math.Min(1, ((x - ax) * dx + (z - az) * dz) / len2)) : 0;
                best = Math.Min(best, Dist(x, z, ax + t * dx, az + t * dz));
            }
            return best;
        }

        [Test]
        public void TwentySliceRoadsSitOnTheirData()
        {
            // Phase C exit check: 20 slice roads, evenly spread by id. At the middle of each one's
            // longest segment (at least 12 m, clear of the chunk edge), the carriageway at the road's own
            // height covers the centreline and reaches to 1 cm inside both edges, and stops by 1 cm
            // outside them unless another road at that height is there. A gentle-bend fillet (the
            // browser build's rule) would show as a miss and is reported.
            var all = new List<(string chunk, Road road, double mx, double mz, double nx, double nz)>();
            var chunks = new Dictionary<string, DadcChunk>();
            for (int ix = 6; ix <= 9; ix++)
            for (int iy = -30; iy <= -28; iy++)
            {
                string id = $"c_{ix}_{iy}";
                var c = Load(id);
                chunks[id] = c;
                foreach (var r in c.Roads)
                {
                    if (RoadPalette.IsFootSpan(r) || r.Pts == null || r.Pts.Length < 4) continue;
                    double half = Math.Max(RoadPalette.MinHalf, r.WidthCm / 200.0), longest = 0;
                    (double mx, double mz, double nx, double nz) pick = default;
                    for (int k = 0; k + 3 < r.Pts.Length; k += 2)
                    {
                        double ax = r.Pts[k] / 100.0, az = r.Pts[k + 1] / 100.0, bx = r.Pts[k + 2] / 100.0, bz = r.Pts[k + 3] / 100.0;
                        double len = Dist(ax, az, bx, bz), mx = (ax + bx) / 2, mz = (az + bz) / 2, margin = half + 1;
                        if (len < 12 || len <= longest || mx < margin || mx > 500 - margin || mz < margin || mz > 500 - margin) continue;
                        longest = len;
                        pick = (mx, mz, -(bz - az) / len, (bx - ax) / len);
                    }
                    if (longest > 0) all.Add((id, r, pick.mx, pick.mz, pick.nx, pick.nz));
                }
            }
            all.Sort((a, b) => a.road.Id.CompareTo(b.road.Id));
            Assert.Greater(all.Count, 100);
            var built = new Dictionary<string, ChunkRoads>();
            int inside = 0, edges = 0, shared = 0;
            var misses = new List<string>();
            for (int s = 0; s < 20; s++)
            {
                var (id, road, mx, mz, nx, nz) = all[s * all.Count / 20];
                if (!built.TryGetValue(id, out var r)) built[id] = r = ChunkRoads.Build(chunks[id]);
                double half = Math.Max(RoadPalette.MinHalf, road.WidthCm / 200.0), y = road.YCm / 100.0;
                if (!CoveredAt(r, mx, mz, y)) misses.Add($"{road.Id} centre");
                foreach (int side in new[] { -1, 1 })
                {
                    if (CoveredAt(r, mx + side * nx * (half - 0.01), mz + side * nz * (half - 0.01), y)) inside++;
                    else misses.Add($"{road.Id} side {side} inside");
                    double ox = mx + side * nx * (half + 0.01), oz = mz + side * nz * (half + 0.01);
                    if (!CoveredAt(r, ox, oz, y)) { edges++; continue; }
                    bool other = false;
                    foreach (var o in chunks[id].Roads)
                        if (o.Id != road.Id && Math.Abs(o.YCm - road.YCm) < 1 && o.Pts != null &&
                            DistToPolyline(o.Pts, ox, oz) <= Math.Max(RoadPalette.MinHalf, o.WidthCm / 200.0) + 0.02) other = true;
                    if (other) shared++;
                    else misses.Add($"{road.Id} side {side} outside");
                }
            }
            Debug.Log($"Slice road spot check: {all.Count} candidates, 20 checked; {inside} / 40 edges reach 1 cm inside, {edges} stop within 1 cm outside, {shared} meet another road" +
                      (misses.Count > 0 ? "; misses: " + string.Join(", ", misses) : ""));
            CollectionAssert.IsEmpty(misses, "every centre and edge within 1 cm of the data");
            Assert.AreEqual(40, edges + shared);
        }

        [Test]
        public void FarmgateRoadsMeshFacesTheRightWay()
        {
            var c = Load("c_7_-30");
            var r = ChunkRoads.Build(c);
            Debug.Log($"c_7_-30 input: {c.Roads.Length} ways");
            Assert.AreEqual(0, r.Indices.Length % 3);
            Assert.AreEqual(r.Positions.Length, r.VertexCount * 3);
            var p = r.Positions;
            int up = 0, down = 0, walls = 0;
            for (int t = 0; t < r.Indices.Length; t += 3)
            {
                int a = r.Indices[t] * 3, b = r.Indices[t + 1] * 3, cc = r.Indices[t + 2] * 3;
                Assert.Less(r.Indices[t], r.VertexCount);
                if (p[a + 1] != p[b + 1] || p[a + 1] != p[cc + 1]) { walls++; continue; }
                double cross = (p[b] - p[a]) * (p[cc + 2] - p[a + 2]) - (p[b + 2] - p[a + 2]) * (p[cc] - p[a]);
                if (cross < 0) up++;
                else down++;
            }
            Debug.Log($"c_7_-30 roads: {r.Roads} carriageways, {r.Pads} pads, {r.Sidewalks} sidewalks, {r.Drains} drains, {r.BusStops} bus stops, {r.BrokenSlabs} broken slabs, {r.Bridges} bridges, {r.FootSpans} footbridges left out, " +
                      $"{r.Chained} chained, {r.Trimmed} stems trimmed, {r.BorderCuts} border cuts; {r.VertexCount} vertices; triangles up {up}, down {down}, walls {walls}");
            Assert.AreEqual(c.Roads.Length - r.FootSpans - r.Chained, r.Roads, "every way drawn, once chains are joined");
            // 6 when this was written; 5 since K-37 P-1 (2026-11-03): Khamar Bari Road's two pieces
            // (343016048 at 7 m from its lanes, 698209679 at 14 m) no longer chain across way
            // 142049043, so each is a stem pulled back to its edge and that crossing needs no pad.
            Assert.GreaterOrEqual(r.Pads, 5, "the Farmgate crossings get pads");
            Assert.Greater(r.Sidewalks, 10);
            Assert.Greater(r.BorderCuts, 10);
            // Only soffits face down: one strip under each elevated bridge.
            if (r.Bridges == 0) Assert.AreEqual(0, down);

            // Road 13848342 starts on the north border at 464.63 m east: its cap lies on the border.
            var cap = r.Caps.Find(k => k.RoadId == 13848342 && Math.Abs(k.Z - 500) < 1e-6);
            Assert.AreEqual(464.63, cap.X, 0.001);
            Assert.AreEqual(500, cap.Lz, 1e-9);
            Assert.AreEqual(500, cap.Rz, 1e-9);
            var road = Array.Find(c.Roads, k => k.Id == 13848342);
            Assert.GreaterOrEqual(Math.Abs(cap.Lx - cap.Rx), Math.Max(RoadPalette.MinHalf, road.WidthCm / 200.0) * 2 - 1e-6, "cut along the border, never narrower than the road");
        }
    }
}
