// Ground surfaces for one chunk: every feature drawn on its layer, in its colour, facing up,
// inside the chunk; canals as strips of their width; the palette covers the city's types.
using System;
using System.Collections.Generic;
using System.IO;
using Dad.World;
using NUnit.Framework;
using UnityEngine;

namespace Dad.Tests
{
    public class GroundBuilderTests
    {
        static string ChunkDir => Path.Combine(TestWorld.Root, "chunks");

        static DadcChunk Load(string id)
        {
            var f = Path.Combine(ChunkDir, id + ".dadc");
            Assume.That(File.Exists(f), "world data missing: run Tools/worldgen/build.sh");
            return Dadc.Load(File.ReadAllBytes(f));
        }

        static void AssertFacingUp(ChunkSurfaces s)
        {
            var p = s.Positions;
            for (int t = 0; t < s.Indices.Length; t += 3)
            {
                int a = s.Indices[t] * 3, b = s.Indices[t + 1] * 3, c = s.Indices[t + 2] * 3;
                // Unity's front face is clockwise seen from above: cross of (b - a, c - a) in (x, z) < 0.
                double cross = (p[b] - p[a]) * (p[c + 2] - p[a + 2]) - (p[b + 2] - p[a + 2]) * (p[c] - p[a]);
                Assert.LessOrEqual(cross, 1e-6, $"triangle {t / 3} faces down");
            }
        }

        [Test]
        public void KawranBazarChunkDrawsEveryFeatureOnItsLayer()
        {
            var c = Load("c_8_-29"); // Kawran Bazar, east 4357, north 14176
            var s = ChunkSurfaces.Build(c);
            Assert.AreEqual(0, s.Failed);
            Assert.AreEqual(1, s.Features[(int)GroundLayer.Base]);
            int landuse = s.Features[(int)GroundLayer.LanduseBroad] + s.Features[(int)GroundLayer.LanduseDetail];
            int poolsAsWater = 0;
            foreach (var l in c.Landuse) if (GroundPalette.Landuse(l.Type).layer == GroundLayer.Water) poolsAsWater++;
            Assert.AreEqual(c.Landuse.Length - poolsAsWater, landuse);
            Assert.AreEqual(c.Parks.Length, s.Features[(int)GroundLayer.Park]);
            Assert.AreEqual(c.Water.Length + poolsAsWater, s.Features[(int)GroundLayer.Water]);
            Assert.AreEqual(c.Plazas.Length, s.Features[(int)GroundLayer.Plaza]);
            Assert.AreEqual(s.Positions.Length, s.VertexCount * 3);
            Assert.AreEqual(0, s.Indices.Length % 3);
            AssertFacingUp(s);

            var heights = new HashSet<float>(GroundPalette.Height);
            for (int v = 0; v < s.VertexCount; v++)
            {
                float x = s.Positions[v * 3], y = s.Positions[v * 3 + 1], z = s.Positions[v * 3 + 2];
                Assert.IsTrue(x >= -0.01f && x <= 500.01f && z >= -0.01f && z <= 500.01f, $"vertex {v} at {x}, {z} outside the chunk");
                Assert.IsTrue(heights.Contains(y), $"vertex {v} at height {y}, not a layer height");
                var m = WorldMaterials.Of(s.Colors[v]);
                Assert.IsTrue(m == WorldMaterial.Soil || m == WorldMaterial.Grass || m == WorldMaterial.Water || m == WorldMaterial.Paving, $"vertex {v}: ground material {m}");
            }
            Debug.Log($"c_8_-29 ground: {s.VertexCount} vertices, {s.Indices.Length / 3} triangles; features by layer {string.Join(", ", s.Features)}");
        }

        [Test]
        public void ColoursArePackedAsStored()
        {
            Assert.AreEqual(0x03544c2cu, GroundPalette.Pack(0x2c4c54, WorldMaterial.Water), "r in the low byte, the material in alpha");
            var c = new DadcChunk { Ix = 0, Iy = -1, Water = new[] { new Poly { Outer = new[] { 0, 0, 1000, 0, 1000, 1000 } } } };
            var s = ChunkSurfaces.Build(c);
            Assert.AreEqual(GroundPalette.Pack(GroundPalette.Water, WorldMaterial.Water), s.Colors[s.VertexCount - 1]);
            Assert.AreEqual(GroundPalette.Height[(int)GroundLayer.Water], s.Positions[s.Positions.Length - 2]);
        }

        [Test]
        public void CanalIsAStripOfItsWidth()
        {
            // An L: 100 m east, then 50 m north, 4 m wide.
            var c = new DadcChunk { Ix = 0, Iy = -1, Canals = new[] { new Canal { WidthCm = 400, Pts = new[] { 10000, 10000, 20000, 10000, 20000, 15000 } } } };
            var s = ChunkSurfaces.Build(c);
            Assert.AreEqual(1, s.Features[(int)GroundLayer.Canal]);
            AssertFacingUp(s);
            double area = 0;
            var p = s.Positions;
            for (int t = 6; t < s.Indices.Length; t += 3) // after the base plate
            {
                int a = s.Indices[t] * 3, b = s.Indices[t + 1] * 3, cc = s.Indices[t + 2] * 3;
                area += Math.Abs((p[b] - p[a]) * (p[cc + 2] - p[a + 2]) - (p[b + 2] - p[a + 2]) * (p[cc] - p[a])) / 2;
            }
            Assert.AreEqual(150 * 4, area, 0.5, "length x width; the mitred corner adds and removes the same area");
            for (int v = 4; v < s.VertexCount; v++) Assert.AreEqual(GroundPalette.Height[(int)GroundLayer.Canal], p[v * 3 + 1]);
        }

        [Test]
        public void PaletteCoversTheCityLanduseTypes()
        {
            Assume.That(Directory.Exists(ChunkDir), "world data missing: run Tools/worldgen/build.sh");
            var other = new SortedDictionary<string, int>();
            int total = 0;
            foreach (var f in Directory.GetFiles(ChunkDir, "*.dadc"))
                foreach (var l in Dadc.Load(File.ReadAllBytes(f)).Landuse)
                {
                    total++;
                    if (GroundPalette.Landuse(l.Type).rgb == GroundPalette.LanduseOther) other[l.Type] = other.TryGetValue(l.Type, out var n) ? n + 1 : 1;
                }
            int others = 0;
            foreach (var n in other.Values) others += n;
            Debug.Log($"Landuse: {total} polygons, {others} in the fallback colour: {string.Join(", ", other)}");
            Assert.Less(others, total / 100, "fewer than 1 % fall back to the default colour");
        }
    }
}
