// Earcut: known shapes exactly, then every ground polygon in the city, where the triangles
// must cover the same area as the rings (outer minus holes).
using System;
using System.Collections.Generic;
using System.IO;
using Dad.World;
using NUnit.Framework;
using UnityEngine;

namespace Dad.Tests
{
    public class EarcutTests
    {
        static double TriArea(IReadOnlyList<double> d, List<int> t)
        {
            double sum = 0;
            for (int i = 0; i < t.Count; i += 3)
            {
                int a = t[i] * 2, b = t[i + 1] * 2, c = t[i + 2] * 2;
                sum += Math.Abs((d[b] - d[a]) * (d[c + 1] - d[a + 1]) - (d[b + 1] - d[a + 1]) * (d[c] - d[a])) / 2;
            }
            return sum;
        }

        static double RingArea(IReadOnlyList<double> d, int start, int end)
        {
            double sum = 0;
            for (int i = start, j = end - 2; i < end; j = i, i += 2) sum += (d[j] - d[i]) * (d[i + 1] + d[j + 1]);
            return Math.Abs(sum) / 2;
        }

        static double RingsArea(List<double> d, List<int> holes)
        {
            int outerEnd = holes.Count > 0 ? holes[0] * 2 : d.Count;
            double a = RingArea(d, 0, outerEnd);
            for (int h = 0; h < holes.Count; h++) a -= RingArea(d, holes[h] * 2, h + 1 < holes.Count ? holes[h + 1] * 2 : d.Count);
            return a;
        }

        [Test]
        public void KnownShapes()
        {
            var t = new List<int>();
            var square = new List<double> { 0, 0, 10, 0, 10, 10, 0, 10 };
            Assert.AreEqual(2, Earcut.Triangulate(square, null, t));
            Assert.AreEqual(100, TriArea(square, t), 1e-9);

            t.Clear();
            var withHole = new List<double> { 0, 0, 10, 0, 10, 10, 0, 10, 2, 2, 2, 8, 8, 8, 8, 2 };
            Assert.AreEqual(8, Earcut.Triangulate(withHole, new List<int> { 4 }, t), "a square ring: 8 triangles");
            Assert.AreEqual(64, TriArea(withHole, t), 1e-9);

            t.Clear();
            var ell = new List<double> { 0, 0, 6, 0, 6, 2, 2, 2, 2, 6, 0, 6 };
            Assert.AreEqual(4, Earcut.Triangulate(ell, null, t));
            Assert.AreEqual(20, TriArea(ell, t), 1e-9, "concave L");

            t.Clear();
            var reversed = new List<double> { 0, 10, 10, 10, 10, 0, 0, 0 };
            Earcut.Triangulate(reversed, null, t);
            Assert.AreEqual(100, TriArea(reversed, t), 1e-9, "either winding");

            t.Clear();
            Assert.AreEqual(0, Earcut.Triangulate(new List<double> { 0, 0, 1, 1 }, null, t), "two points");
            Assert.AreEqual(0, Earcut.Triangulate(new List<double> { 0, 0, 1, 1, 2, 2 }, null, t), "collinear");

            // A large polygon takes the z-order hashed path (over 80 points).
            t.Clear();
            var circle = new List<double>();
            for (int i = 0; i < 200; i++) { double a = i * Math.PI * 2 / 200; circle.Add(Math.Cos(a) * 50); circle.Add(Math.Sin(a) * 50); }
            Assert.AreEqual(198, Earcut.Triangulate(circle, null, t));
            Assert.AreEqual(RingArea(circle, 0, circle.Count), TriArea(circle, t), 1e-6);
        }

        [Test]
        public void EveryGroundPolygonInTheCityKeepsItsArea()
        {
            var dir = Path.Combine(TestWorld.City, "chunks");
            Assume.That(Directory.Exists(dir), "world data missing: run Tools/worldgen/build.sh");
            var flat = new List<double>();
            var holes = new List<int>();
            var tris = new List<int>();
            int polygons = 0, empty = 0, emptyWithArea = 0, off = 0, chunks = 0;
            double emptyMaxArea = 0;
            double worstRel = 0, sumRings = 0, sumTris = 0;
            string worst = "";
            var offList = new List<string>();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            foreach (var file in Directory.GetFiles(dir, "*.dadc"))
            {
                var c = Dadc.Load(File.ReadAllBytes(file));
                chunks++;
                void Check(string kind, long id, int[] outer, int[][] h)
                {
                    if (outer == null || outer.Length < 6) return;
                    polygons++;
                    ChunkSurfaces.Flatten(outer, h, flat, holes);
                    tris.Clear();
                    double rings = RingsArea(flat, holes);
                    if (Earcut.Triangulate(flat, holes, tris) == 0)
                    {
                        // Slivers left by clipping to the chunk edge have no area to draw.
                        empty++;
                        emptyMaxArea = Math.Max(emptyMaxArea, rings);
                        if (rings > 0.01) { emptyWithArea++; if (offList.Count < 10) offList.Add($"{c.Id} {kind} {id} empty but {rings:F3} m2"); }
                        return;
                    }
                    double got = TriArea(flat, tris);
                    sumRings += rings;
                    sumTris += got;
                    double rel = Math.Abs(got - rings) / Math.Max(rings, 1e-9);
                    if (rel > worstRel) { worstRel = rel; worst = $"{c.Id} {kind} {id}: rings {rings:F2} m2, triangles {got:F2} m2"; }
                    if (rel > 0.01 && Math.Abs(got - rings) > 1) { off++; if (offList.Count < 10) offList.Add($"{c.Id} {kind} {id} ({rings:F1} vs {got:F1} m2)"); }
                }
                foreach (var p in c.Landuse) Check("landuse", p.Id, p.Outer, p.Holes);
                foreach (var p in c.Parks) Check("park", p.Id, p.Outer, p.Holes);
                foreach (var p in c.Water) Check("water", p.Id, p.Outer, p.Holes);
                foreach (var p in c.Plazas) Check("plaza", p.Id, p.Outer, p.Holes);
            }
            Debug.Log($"Earcut over the city: {chunks} chunks, {polygons} polygons, {empty} without triangles (largest ring area {emptyMaxArea:F4} m2), {off} off by more than 1 % and 1 m2; " +
                      $"total {sumRings / 1e6:F4} km2 rings vs {sumTris / 1e6:F4} km2 triangles; worst {worstRel:P2} ({worst}); {sw.ElapsedMilliseconds} ms" +
                      (offList.Count > 0 ? "; off: " + string.Join(", ", offList) : ""));
            Assert.AreEqual(TestWorld.CityChunks, chunks);
            Assert.Greater(polygons, 1000);
            Assert.AreEqual(0, emptyWithArea, "every polygon with area gives triangles");
            Assert.AreEqual(0, off, "no polygon loses or gains area");
        }
    }
}
