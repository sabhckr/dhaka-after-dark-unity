// C-04 buildings: the height rules match the browser build's; the slice's metro stations stand
// over the trunk road (Kazi Nazrul Islam Avenue at Kawran Bazar); 20 buildings in the slice sit exactly on their data; walls face
// out and roofs up; every building in the city extrudes.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Dad.World;
using NUnit.Framework;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Dad.Tests
{
    public class BuildingBuilderTests
    {
        static string ChunkDir => Path.Combine(TestWorld.Root, "chunks");

        static DadcChunk Load(string id)
        {
            var f = Path.Combine(ChunkDir, id + ".dadc");
            Assume.That(File.Exists(f), "world data missing: run Tools/worldgen/build.sh");
            return Dadc.Load(File.ReadAllBytes(f));
        }

        static Building B(int heightCm = 900, string source = "estimated", int? levelsHalf = null, int? minLevelHalf = null, int? minHeightCm = null, int? layer = null) =>
            new Building
            {
                Id = 1, HeightCm = heightCm, HeightSource = source, LevelsHalf = levelsHalf, MinLevelHalf = minLevelHalf,
                MinHeightCm = minHeightCm, Layer = layer, Outer = new[] { 0, 0, 1000, 0, 1000, 1000, 0, 1000 },
            };

        [Test]
        public void RulesMatchTheBrowserBuild()
        {
            // The browser build's own cases (dhaka-after-dark src/geo/world-v2.test.ts).
            Assert.AreEqual((7.0, Src.Osm), BuildingRules.Levels(B(levelsHalf: 14)));
            Assert.AreEqual((10.0, Src.Rule), BuildingRules.Levels(B(3200, "measured")));
            Assert.AreEqual((3.0, Src.Default), BuildingRules.Levels(B(900)));
            Assert.AreEqual((true, Src.Osm), BuildingRules.Elevated(B(minHeightCm: 800)));
            Assert.AreEqual((true, Src.Rule), BuildingRules.Elevated(B(layer: 1)));
            Assert.AreEqual((false, Src.Default), BuildingRules.Elevated(B()));
            var (baseM, src) = BuildingRules.Base(B(minLevelHalf: 4));
            Assert.AreEqual(6.4, baseM, 1e-9);
            Assert.AreEqual(Src.Rule, src);
            Assert.AreEqual((8.0, Src.Osm), BuildingRules.Base(B(minHeightCm: 800)));
            Assert.AreEqual((0.0, Src.Default), BuildingRules.Base(B()));
            // The generator's layer heights: road bridges at 6, 11, 16 m; Line 6 (layer 6) at 31 m.
            CollectionAssert.AreEqual(new[] { 6.0, 11.0, 16.0, 31.0 }, Array.ConvertAll(new[] { 1, 2, 3, 6 }, BuildingRules.LayerHeight));

            // Tops: measured and levels-derived heights are from the ground; a class default is a body.
            var measured = BuildingRules.Place(B(3200, "measured"));
            Assert.AreEqual((0.0, 32.0), (measured.Base, measured.Top));
            var levels = BuildingRules.Place(B(2240, levelsHalf: 14));
            Assert.AreEqual(22.4, levels.Top, 1e-9);
            var bodyOnStilts = BuildingRules.Place(B(900, minHeightCm: 800));
            Assert.AreEqual((8.0, 17.0, Src.Osm), (bodyOnStilts.Base, bodyOnStilts.Top, bodyOnStilts.BaseSource));
            var tooLow = BuildingRules.Place(B(500, "measured", minHeightCm: 800));
            Assert.IsTrue(tooLow.TopFixed);
            Assert.AreEqual(11.2, tooLow.Top, 1e-9);
            var station = B(1200, layer: 3);
            station.Type = "train_station";
            var raised = BuildingRules.Place(station);
            Assert.AreEqual((16.0, 28.0, true, Src.Rule), (raised.Base, raised.Top, raised.LayerRaised, raised.BaseSource));
            var layerOnly = BuildingRules.Place(B(1200, layer: 3));
            Assert.AreEqual((0.0, 12.0, true), (layerOnly.Base, layerOnly.Top, layerOnly.LayerIgnored), "layer alone does not say how high");
        }

        /// <summary>Roads of any height whose centreline crosses or lies inside the footprint.</summary>
        static List<Road> RoadsUnder(Building b, Road[] roads)
        {
            var under = new List<Road>();
            foreach (var r in roads)
                if (ChunkBuildings.CoversRoad(b, new[] { new Road { Pts = r.Pts, YCm = 0 } })) under.Add(r);
            return under;
        }

        [Test]
        public void MetroStationsStandOverKaziNazrulIslamAvenue()
        {
            var avenueUnder = new HashSet<long>();
            // Farmgate station was cut in two by the chunk grid; Kawran Bazar's lies in one chunk.
            foreach (var (chunkId, id) in new[] { ("c_7_-30", 1125768748L), ("c_7_-31", 1125768748L), ("c_8_-29", 1125773740L) })
            {
                var c = Load(chunkId);
                int i = Array.FindIndex(c.Buildings, k => k.Id == id);
                Assert.GreaterOrEqual(i, 0, $"station {id} in {chunkId}");
                var b = c.Buildings[i];
                Assert.AreEqual("train_station", b.Type);
                Assert.AreEqual(3, b.Layer, "layer=3");
                Assert.IsNull(b.MinHeightCm);
                Assert.IsNull(b.MinLevelHalf);

                var s = ChunkBuildings.Build(c);
                var pl = s.Placements[i];
                Assert.IsTrue(pl.LayerRaised, "raised by the station layer rule");
                Assert.AreEqual(16, pl.Base, 1e-9, "layer 3 height");
                Assert.AreEqual(28, pl.Top, 1e-9, "12 m class default on top of the base");

                var under = RoadsUnder(b, c.Roads);
                var names = new List<string>();
                double highest = 0;
                bool avenue = false, trunk = false;
                foreach (var r in under)
                {
                    names.Add($"{r.NameEn ?? r.Name ?? r.Cls} ({r.Cls}, {r.YCm / 100.0:F2} m{(r.Bridge ? ", bridge" : "")})");
                    highest = Math.Max(highest, r.YCm / 100.0 + (r.Bridge ? RoadPalette.BarrierHeight : 0));
                    avenue |= !r.Bridge && ((r.NameEn ?? "").Contains("Kazi Nazrul Islam") || (r.Name ?? "").Contains("কাজী নজরুল ইসলাম"));
                    trunk |= !r.Bridge && r.Cls == "trunk";
                }
                Debug.Log($"Station {b.Name} ({chunkId}): base {pl.Base} m, top {pl.Top} m; roads under it: {string.Join("; ", names)}");
                if (avenue) avenueUnder.Add(id);
                Assert.IsTrue(trunk, "a trunk road runs under every piece at grade");
                Assert.GreaterOrEqual(pl.Base - highest, 4.5, "clear of the highest road or deck under it");

                // The mesh: the station's vertices span base to top, and it has an underside.
                float lo = float.MaxValue, hi = float.MinValue;
                for (int v = s.FirstVertex[i]; v < s.FirstVertex[i] + s.VertexCounts[i]; v++)
                {
                    lo = Math.Min(lo, s.Positions[3 * v + 1]);
                    hi = Math.Max(hi, s.Positions[3 * v + 1]);
                }
                Assert.AreEqual((16f, 28f), (lo, hi));
                Assert.Greater(CountFacing(s, i, down: true), 0, "an underside facing down");
            }
            // In OSM the trunk under Farmgate station is unnamed or "Khamar Bari Road"; Kawran Bazar's is the avenue by name.
            CollectionAssert.AreEquivalent(new[] { 1125773740L }, avenueUnder, "Kazi Nazrul Islam Avenue, by name, under Kawran Bazar station");
        }

        static int CountFacing(ChunkBuildings s, int building, bool down)
        {
            int n = 0, first = s.FirstVertex[building], end = first + s.VertexCounts[building];
            for (int t = 0; t < s.Indices.Length; t += 3)
            {
                if (s.Indices[t] < first || s.Indices[t] >= end) continue;
                var nrm = Normal(s, t);
                if (down ? nrm.y < -0.99 : nrm.y > 0.99) n++;
            }
            return n;
        }

        /// <summary>Unit normal of triangle t: Cross(b - a, c - a), the side Unity draws.</summary>
        static Vector3d Normal(ChunkBuildings s, int t)
        {
            var a = P(s, s.Indices[t]);
            var b = P(s, s.Indices[t + 1]);
            var c = P(s, s.Indices[t + 2]);
            return Vector3d.Cross(b - a, c - a).Normalized;
        }

        static Vector3d P(ChunkBuildings s, int v) => new Vector3d(s.Positions[3 * v], s.Positions[3 * v + 1], s.Positions[3 * v + 2]);

        struct Vector3d
        {
            public double x, y, z;
            public Vector3d(double x, double y, double z) { this.x = x; this.y = y; this.z = z; }
            public static Vector3d operator -(Vector3d a, Vector3d b) => new Vector3d(a.x - b.x, a.y - b.y, a.z - b.z);
            public static Vector3d Cross(Vector3d a, Vector3d b) => new Vector3d(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);
            public double Length => Math.Sqrt(x * x + y * y + z * z);
            public Vector3d Normalized { get { double l = Length; return l > 0 ? new Vector3d(x / l, y / l, z / l) : this; } }
        }

        static bool InRing(int[] ring, double x, double z)
        {
            bool inside = false;
            int n = ring.Length / 2;
            for (int i = 0, j = n - 1; i < n; j = i, i++)
            {
                double xi = ring[2 * i] / 100.0, zi = ring[2 * i + 1] / 100.0, xj = ring[2 * j] / 100.0, zj = ring[2 * j + 1] / 100.0;
                if ((zi > z) != (zj > z) && x < (xj - xi) * (z - zi) / (zj - zi) + xi) inside = !inside;
            }
            return inside;
        }

        static bool InSolid(Building b, double x, double z)
        {
            if (!InRing(b.Outer, x, z)) return false;
            if (b.Holes != null) foreach (var h in b.Holes) if (h != null && h.Length >= 6 && InRing(h, x, z)) return false;
            return true;
        }

        static bool OnEdge(double a, double b, double c) =>
            (Math.Abs(a) < 1e-3 && Math.Abs(b) < 1e-3 && Math.Abs(c) < 1e-3) ||
            (Math.Abs(a - 500) < 1e-3 && Math.Abs(b - 500) < 1e-3 && Math.Abs(c - 500) < 1e-3);

        static double RingArea(int[] ring)
        {
            double sum = 0;
            int n = ring.Length / 2;
            for (int i = 0, j = n - 1; i < n; j = i, i++)
                sum += ring[2 * j] / 100.0 * (ring[2 * i + 1] / 100.0) - ring[2 * i] / 100.0 * (ring[2 * j + 1] / 100.0);
            return Math.Abs(sum) / 2;
        }

        [Test]
        public void TwentySliceBuildingsSitOnTheirData()
        {
            var all = new List<(long id, string chunk, int index)>();
            var chunks = new Dictionary<string, (DadcChunk c, ChunkBuildings s)>();
            for (int ix = 6; ix <= 9; ix++)
                for (int iy = -30; iy <= -28; iy++)
                {
                    var c = Load($"c_{ix}_{iy}");
                    chunks[c.Id] = (c, ChunkBuildings.Build(c));
                    for (int i = 0; i < c.Buildings.Length; i++) all.Add((c.Buildings[i].Id, c.Id, i));
                }
            all.Sort((a, b) => a.id != b.id ? a.id.CompareTo(b.id) : string.CompareOrdinal(a.chunk, b.chunk));
            var lines = new List<string>();
            double worst = 0;
            for (int k = 0; k < 20; k++)
            {
                var (id, chunkId, i) = all[k * all.Count / 20];
                var (c, s) = chunks[chunkId];
                var b = c.Buildings[i];
                var pl = s.Placements[i];
                int first = s.FirstVertex[i], end = first + s.VertexCounts[i];
                Assert.Greater(s.VertexCounts[i], 0, $"{id} drawn");

                float lo = float.MaxValue, hi = float.MinValue;
                for (int v = first; v < end; v++)
                {
                    lo = Math.Min(lo, s.Positions[3 * v + 1]);
                    hi = Math.Max(hi, s.Positions[3 * v + 1]);
                }
                Assert.AreEqual(pl.Base, lo, 1e-4, $"{id} base");
                Assert.AreEqual(pl.Top, hi, 1e-4, $"{id} top");

                // Every footprint corner has a roof vertex on it.
                for (int p = 0; p + 1 < b.Outer.Length; p += 2)
                {
                    double x = b.Outer[p] / 100.0, z = b.Outer[p + 1] / 100.0, best = double.MaxValue;
                    for (int v = first; v < end; v++)
                        if (Math.Abs(s.Positions[3 * v + 1] - pl.Top) < 1e-4)
                            best = Math.Min(best, Math.Sqrt(Math.Pow(s.Positions[3 * v] - x, 2) + Math.Pow(s.Positions[3 * v + 2] - z, 2)));
                    worst = Math.Max(worst, best);
                    Assert.Less(best, 0.01, $"{id} corner {p / 2} within 1 cm");
                }

                // The roof covers the footprint's area: at the top, or below a parapet (D-06b).
                double roof = 0, roofY = s.Skins[i].RoofY;
                for (int t = 0; t < s.Indices.Length; t += 3)
                {
                    if (s.Indices[t] < first || s.Indices[t] >= end) continue;
                    var a = P(s, s.Indices[t]);
                    if (Math.Abs(a.y - roofY) > 1e-4 || Normal(s, t).y < 0.99) continue;
                    roof += Vector3d.Cross(P(s, s.Indices[t + 1]) - a, P(s, s.Indices[t + 2]) - a).Length / 2;
                }
                double area = RingArea(b.Outer);
                if (b.Holes != null) foreach (var h in b.Holes) area -= RingArea(h);
                Assert.AreEqual(area, roof, Math.Max(0.01, area * 1e-4), $"{id} roof area");
                var (levels, levelSrc) = BuildingRules.Levels(b);
                lines.Add($"{id} {b.Type ?? "yes"} h {b.HeightCm / 100.0} {b.HeightSource}, levels {levels} ({levelSrc}), base {pl.Base} ({pl.BaseSource}), top {pl.Top}, roof {roofY}, {area:F1} m2");
            }
            Debug.Log($"20 slice buildings of {all.Count}, worst corner {worst * 1000:F2} mm:\n" + string.Join("\n", lines));
        }

        [Test]
        public void FarmgateWallsFaceOutAndRoofsUp()
        {
            var c = Load("c_7_-30");
            var s = ChunkBuildings.Build(c);
            var owner = new int[s.VertexCount];
            for (int i = 0; i < c.Buildings.Length; i++)
                for (int v = s.FirstVertex[i]; v < s.FirstVertex[i] + s.VertexCounts[i]; v++) owner[v] = i;
            var facaded = new bool[c.Buildings.Length];
            for (int i = 0; i < c.Buildings.Length; i++) facaded[i] = s.VertexCounts[i] > 0 && ChunkBuildings.Facaded(c, c.Buildings[i], s.Placements[i]);
            int up = 0, down = 0, walls = 0, outward = 0, badDown = 0, tilted = 0, border = 0, borderOdd = 0, facadeTris = 0, parapet = 0, parapetIn = 0, parapetBorder = 0;
            var misses = new List<string>();
            for (int t = 0; t < s.Indices.Length; t += 3)
            {
                int i = owner[s.Indices[t]];
                // A facade's reveals, sills, awnings and shop recesses (M2) are checked by StreetTests.FacadesKeepFootprintAndTop.
                if (facaded[i]) { facadeTris++; continue; }
                var n = Normal(s, t);
                if (n.y > 0.99) { up++; continue; }
                if (n.y < -0.99)
                {
                    down++;
                    if (s.Placements[i].Base <= 0) badDown++;
                    continue;
                }
                if (Math.Abs(n.y) > 1e-3) { tilted++; continue; }
                walls++;
                var a = P(s, s.Indices[t]);
                var b = P(s, s.Indices[t + 1]);
                var cc = P(s, s.Indices[t + 2]);
                double mx = (a.x + b.x + cc.x) / 3, mz = (a.z + b.z + cc.z) / 3;
                // Test at the middle of the wall's foot, 2 cm either side.
                double nx = n.x / Math.Sqrt(n.x * n.x + n.z * n.z), nz = n.z / Math.Sqrt(n.x * n.x + n.z * n.z);
                // A parapet's face (D-06b) stands on the roof ParapetThickness inside the wall and faces the roof:
                // the roof in front of it, the parapet behind it, and outside the building beyond the parapet.
                if (s.Skins[i].Parapet && Math.Min(a.y, Math.Min(b.y, cc.y)) > s.Skins[i].RoofY - 1e-4)
                {
                    walls--;
                    parapet++;
                    double t2 = BuildingSkin.ParapetThickness;
                    if (InSolid(c.Buildings[i], mx + nx * 0.02, mz + nz * 0.02) && InSolid(c.Buildings[i], mx - nx * 0.02, mz - nz * 0.02)
                        && !InSolid(c.Buildings[i], mx - nx * (t2 + 0.02), mz - nz * (t2 + 0.02))) parapetIn++;
                    // Within 0.5 m of the chunk border, clipping leaves jogs shorter than the probe: counted, not judged.
                    else if (NearBorder(a, b, cc)) { parapetIn++; parapetBorder++; }
                    else if (misses.Count < 10) misses.Add($"parapet of {c.Buildings[i].Id} at {mx:F2}, {mz:F2}");
                    continue;
                }
                bool ok = !InSolid(c.Buildings[i], mx + nx * 0.02, mz + nz * 0.02) && InSolid(c.Buildings[i], mx - nx * 0.02, mz - nz * 0.02);
                // Walls on the chunk edge close a building the grid cut in two. Clipping can leave
                // overlapping edges there, so those are counted, not judged.
                if (OnEdge(a.x, b.x, cc.x) || OnEdge(a.z, b.z, cc.z))
                {
                    border++;
                    if (!ok) borderOdd++;
                    walls--;
                    continue;
                }
                if (ok) outward++;
                else if (misses.Count < 10) misses.Add($"{c.Buildings[i].Id} at {mx:F2}, {mz:F2}");
            }
            Debug.Log($"c_7_-30 buildings: {s.Drawn} of {c.Buildings.Length} drawn, {s.VertexCount} vertices, {s.Indices.Length / 3} triangles; " +
                      $"roof {up}, underside {down}, walls {walls} ({outward} face out), parapet faces {parapet} ({parapetIn - parapetBorder} face the roof, {parapetBorder} by the chunk border not judged), on the chunk edge {border} ({borderOdd} on overlapping clipped edges), tilted {tilted}; raised: {s.RaisedOsm} osm, {s.RaisedRule} rule, " +
                      $"{s.RaisedByLayer} stations by layer, {s.LayerIgnored} layer ignored; tops fixed {s.TopsFixed}; {s.Facades} facade buildings ({facadeTris} triangles) left to the facade test" +
                      (misses.Count > 0 ? "; walls not facing out: " + string.Join(", ", misses) : ""));
            Assert.AreEqual(c.Buildings.Length, s.Drawn);
            Assert.AreEqual(0, tilted, "every triangle is a wall or a flat cap");
            Assert.AreEqual(0, badDown, "only raised buildings have a face looking down");
            Assert.Greater(walls, 0);
            Assert.AreEqual(walls, outward, "every wall faces out of its building");
            Assert.Greater(parapet, 0);
            Assert.AreEqual(parapet, parapetIn, "every parapet faces its roof");
        }

        static bool NearBorder(Vector3d a, Vector3d b, Vector3d c) => NearBorder(a) || NearBorder(b) || NearBorder(c);

        static bool NearBorder(Vector3d p) => p.x < 0.5 || p.x > 499.5 || p.z < 0.5 || p.z > 499.5;

        [Test]
        public void EveryBuildingInTheCityExtrudes()
        {
            var sw = Stopwatch.StartNew();
            int chunks = 0, buildings = 0, drawn = 0, slivers = 0, raisedOsm = 0, raisedRule = 0, byLayer = 0, layerIgnored = 0, topsFixed = 0, failed = 0;
            long vertices = 0;
            int maxVertices = 0;
            string maxChunk = null;
            float maxTop = 0;
            var stations = new HashSet<long>();
            var undrawn = new List<string>();
            var overRoad = new HashSet<long>();
            foreach (var f in Directory.GetFiles(Path.Combine(TestWorld.City, "chunks"), "*.dadc"))
            {
                var c = Dadc.Load(File.ReadAllBytes(f));
                var s = ChunkBuildings.Build(c);
                chunks++;
                buildings += c.Buildings.Length;
                drawn += s.Drawn;
                slivers += s.Slivers;
                raisedOsm += s.RaisedOsm;
                raisedRule += s.RaisedRule;
                byLayer += s.RaisedByLayer;
                for (int i = 0; i < c.Buildings.Length; i++)
                    if (s.Placements[i].LayerRaised)
                    {
                        stations.Add(c.Buildings[i].Id);
                        if (ChunkBuildings.CoversRoad(c.Buildings[i], c.Roads)) overRoad.Add(c.Buildings[i].Id);
                    }
                layerIgnored += s.LayerIgnored;
                topsFixed += s.TopsFixed;
                failed += s.RoofFailed;
                vertices += s.VertexCount;
                maxTop = Math.Max(maxTop, s.Max[1]);
                if (s.VertexCount > maxVertices) { maxVertices = s.VertexCount; maxChunk = c.Id; }
                for (int i = 0; i < c.Buildings.Length; i++)
                    if (s.VertexCounts[i] == 0 && undrawn.Count < 5) undrawn.Add($"{c.Id} {c.Buildings[i].Id} [{string.Join(",", c.Buildings[i].Outer ?? new int[0])}]");
            }
            Debug.Log($"City buildings: {chunks} chunks, {drawn} of {buildings} drawn, {slivers} clipping slivers under 0.01 m2 skipped; raised {raisedOsm} by min_height, {raisedRule} by min_level, " +
                      $"{byLayer} station pieces by layer ({stations.Count} stations, {overRoad.Count} over an at-grade road); {layerIgnored} layer-only left on the ground; {topsFixed} tops fixed; {failed} roofs failed; " +
                      $"{vertices} vertices, most in one chunk {maxVertices} ({maxChunk}); highest top {maxTop:F1} m; {sw.ElapsedMilliseconds} ms" +
                      (undrawn.Count > 0 ? "; not drawn: " + string.Join("; ", undrawn) : ""));
            Assert.AreEqual(TestWorld.CityChunks, chunks);
            Assert.AreEqual(buildings, drawn + slivers, "every building with area is drawn");
            Assert.LessOrEqual(slivers, buildings / 1000, "slivers are rare");
            Assert.AreEqual(0, failed, "every roof with area triangulates");
            Assert.AreEqual(0, stations.Count, "the city's stations are models, not outlines");
        }
    }
}
