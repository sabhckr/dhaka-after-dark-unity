// D-06a: every building gets a category by the labelled rules, in order (BuildingClass); the parts of
// one building agree; the far tier's facade coordinates are whole bays and floors on walls only, with
// shops on the walls facing a road, and every wall's texture runs along it at its true length (K-21); the slice's buildings are all classified and their pieces agree
// across the chunk grid; WorldFacades.mat is World.mat with the facade keyword, and compiles.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Dad.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace Dad.Tests
{
    public class BuildingClassTests
    {
        static string ChunkDir => Path.Combine(TestWorld.Root, "chunks");
        const string ShaderPath = "Assets/_Project/Shaders/DadWorld.shader";

        /// <summary>A rectangle x0..x0+w, z0..z0+d metres in the chunk, counter-clockwise, 9.6 m tall (estimated: 3 floors).</summary>
        static Building Rect(long id, double x0, double z0, double w, double d, string type = null, string name = null) =>
            new Building
            {
                Id = id, HeightCm = 960, HeightSource = "estimated", Type = type, Name = name,
                Outer = new[] { Cm(x0), Cm(z0), Cm(x0 + w), Cm(z0), Cm(x0 + w), Cm(z0 + d), Cm(x0), Cm(z0 + d) },
            };

        static int Cm(double m) => (int)Math.Round(m * 100);

        /// <summary>A road along z = 100 m from x = 0 to 500 m, 10 m wide.</summary>
        static Road Street(string cls, bool bridge = false) =>
            new Road { Id = 9, Cls = cls, WidthCm = 1000, YCm = bridge ? 600 : 0, Bridge = bridge, Pts = new[] { 0, 10000, 50000, 10000 } };

        static DadcChunk Chunk(Building[] buildings, Road[] roads = null, Landuse[] landuse = null) =>
            new DadcChunk { Ix = 0, Iy = -1, Buildings = buildings, Roads = roads ?? new Road[0], Landuse = landuse, WidthsResolved = true };

        static (BuildingCategory, ClassSource) One(DadcChunk c, int i = 0)
        {
            var cats = new BuildingCategory[c.Buildings.Length];
            var srcs = new ClassSource[c.Buildings.Length];
            BuildingClass.ClassifyChunk(c, new BuildingClass.Near(), cats, srcs);
            return (cats[i], srcs[i]);
        }

        [Test]
        public void RulesApplyInOrder()
        {
            var B = BuildingCategory.Residential;
            // 1. The building's own type, wherever it stands; "yes" is no type; an unlisted type is Other.
            Assert.AreEqual((BuildingCategory.Religious, ClassSource.OsmType), One(Chunk(new[] { Rect(1, 200, 200, 20, 20, "mosque") })));
            Assert.AreEqual((BuildingCategory.Institutional, ClassSource.OsmType), One(Chunk(new[] { Rect(1, 0, 200, 20, 20, "school") })));
            Assert.AreEqual((BuildingCategory.Other, ClassSource.OsmType), One(Chunk(new[] { Rect(1, 200, 200, 20, 20, "chicken_coop") })));
            Assert.AreEqual((B, ClassSource.RuleDefault), One(Chunk(new[] { Rect(1, 200, 200, 20, 20, "yes") })));
            // Cut by the chunk grid and untyped: split, before anything that depends on the piece.
            Assert.AreEqual((B, ClassSource.RuleSplit), One(Chunk(new[] { Rect(1, 0, 200, 2, 2) })));
            Assert.AreEqual((B, ClassSource.RuleSplit), One(Chunk(new[] { Rect(1, 480, 495, 20, 5) })));
            // 2. Landuse, by the centre.
            var lu = new[] { new Landuse { Id = 5, Type = "industrial", Outer = new[] { 30000, 30000, 40000, 30000, 40000, 40000, 30000, 40000 } } };
            Assert.AreEqual((BuildingCategory.Industrial, ClassSource.Landuse), One(Chunk(new[] { Rect(1, 350, 350, 20, 20) }, null, lu)));
            Assert.AreEqual((B, ClassSource.RuleDefault), One(Chunk(new[] { Rect(1, 250, 350, 20, 20) }, null, lu)));
            var grass = new[] { new Landuse { Id = 5, Type = "grass", Outer = lu[0].Outer } };
            Assert.AreEqual((B, ClassSource.RuleDefault), One(Chunk(new[] { Rect(1, 350, 350, 20, 20) }, null, grass)), "an unmapped landuse type says nothing");
            // 3. Size.
            Assert.AreEqual((BuildingCategory.Informal, ClassSource.RuleTiny), One(Chunk(new[] { Rect(1, 200, 200, 3, 3) })));
            Assert.AreEqual((B, ClassSource.RuleDefault), One(Chunk(new[] { Rect(1, 200, 200, 4, 4) })), "16 m² is not tiny");
            Assert.AreEqual((BuildingCategory.Commercial, ClassSource.RuleLarge), One(Chunk(new[] { Rect(1, 200, 200, 60, 50) })));
            Assert.AreEqual((BuildingCategory.Special, ClassSource.RuleNamedLarge), One(Chunk(new[] { Rect(1, 200, 200, 60, 50, null, "Hall") })));
            Assert.AreEqual((B, ClassSource.RuleDefault), One(Chunk(new[] { Rect(1, 200, 200, 50, 50, null, "Hall") })), "2,500 m² is not large");
            // 4. A corner within 11 m of a main road's carriageway (edge at z = 105 m).
            Assert.AreEqual((BuildingCategory.MixedUse, ClassSource.RuleMainRoad), One(Chunk(new[] { Rect(1, 200, 111, 12, 9) }, new[] { Street("primary") })));
            Assert.AreEqual((BuildingCategory.MixedUse, ClassSource.RuleMainRoad), One(Chunk(new[] { Rect(1, 200, 115.9, 12, 9) }, new[] { Street("tertiary") })));
            Assert.AreEqual((B, ClassSource.RuleDefault), One(Chunk(new[] { Rect(1, 200, 116.5, 12, 9) }, new[] { Street("primary") })), "11.5 m away");
            Assert.AreEqual((B, ClassSource.RuleDefault), One(Chunk(new[] { Rect(1, 200, 111, 12, 9) }, new[] { Street("residential") })), "not a main road");
            Assert.AreEqual((B, ClassSource.RuleDefault), One(Chunk(new[] { Rect(1, 200, 111, 12, 9) }, new[] { Street("primary", bridge: true) })), "a flyover is not frontage");
            // Landuse comes before size and roads.
            var shops = new[] { new Landuse { Id = 5, Type = "retail", Outer = new[] { 15000, 10500, 30000, 10500, 30000, 20000, 15000, 20000 } } };
            Assert.AreEqual((BuildingCategory.Commercial, ClassSource.Landuse), One(Chunk(new[] { Rect(1, 200, 111, 3, 3) }, new[] { Street("primary") }, shops)));
        }

        [Test]
        public void PartsOfOneBuildingAgree()
        {
            // Two parts in the chunk: each alone is 1,800 m², together 3,600 m² (large); named: plain.
            var two = Chunk(new[] { Rect(7, 100, 200, 60, 30, null, "Bhaban"), Rect(7, 200, 200, 60, 30, null, "Bhaban") });
            var cats = new BuildingCategory[2];
            var srcs = new ClassSource[2];
            BuildingClass.ClassifyChunk(two, new BuildingClass.Near(), cats, srcs);
            Assert.AreEqual(new[] { BuildingCategory.Special, BuildingCategory.Special }, cats);
            Assert.AreEqual(new[] { ClassSource.RuleNamedLarge, ClassSource.RuleNamedLarge }, srcs);
            // One part near a main road, the larger one not: the larger part decides.
            var mixed = Chunk(new[] { Rect(8, 200, 111, 5, 5), Rect(8, 200, 300, 20, 20) }, new[] { Street("primary") });
            BuildingClass.ClassifyChunk(mixed, new BuildingClass.Near(), cats, srcs);
            Assert.AreEqual(new[] { ClassSource.RuleDefault, ClassSource.RuleDefault }, srcs);
            // One part on the chunk border: all split, as the other chunk's pieces will be.
            var cut = Chunk(new[] { Rect(9, 0, 200, 20, 20), Rect(9, 200, 111, 20, 20) }, new[] { Street("primary") });
            BuildingClass.ClassifyChunk(cut, new BuildingClass.Near(), cats, srcs);
            Assert.AreEqual(new[] { ClassSource.RuleSplit, ClassSource.RuleSplit }, srcs);
        }

        static (float u, float v, int w, float s)[] Coordinates(ChunkBuildings s) =>
            Enumerable.Range(0, s.VertexCount).Select(i => (s.Facade[4 * i], s.Facade[4 * i + 1], (int)s.Facade[4 * i + 2], s.Facade[4 * i + 3])).ToArray();

        [Test]
        public void FacadeCoordinatesAreWholeBaysAndFloors()
        {
            // 21 x 9 m, 9.6 m tall: 7 bays on the long walls, 3 on the short, 3 floors up to the roof, which is
            // 0.9 m under the parapet's top since D-06b (RoofTests).
            var s = ChunkBuildings.Build(Chunk(new[] { Rect(42, 200, 200, 21, 9) }));
            Assert.AreEqual(4 * s.VertexCount, s.Facade.Length);
            Assert.AreEqual(4, s.PatternWalls);
            Assert.AreEqual(0, s.ShopWalls);
            var f = Coordinates(s);
            var walls = f.Where(x => x.w != 0).ToArray();
            Assert.AreEqual(16, walls.Length, "four walls of four vertices");
            int seed = (walls[0].w >> 4) & 255;
            foreach (var x in walls)
            {
                Assert.AreEqual((int)FacadeFamily.WalkUp, x.w & 7);
                Assert.AreEqual(0, x.w & 8);
                Assert.AreEqual(ChunkBuildings.WallFlag, x.w & ChunkBuildings.WallFlag);
                Assert.AreEqual(seed, (x.w >> 4) & 255, "one seed a building");
                Assert.That(x.u, Is.EqualTo(0).Or.EqualTo(7).Or.EqualTo(3));
                Assert.That(x.v, Is.EqualTo(0).Or.EqualTo(9.6 / (8.7 / 3)).Within(1e-5));
            }
            Assert.AreEqual(new[] { 3f, 7f }, walls.Select(x => x.u).Where(u => u > 0).Distinct().OrderBy(u => u).ToArray());
            // The roof and the parapet (four faces and four copings) carry nothing.
            Assert.AreEqual(4 + 4 * 8, f.Count(x => x.w == 0));

            // Walls shorter than 2.5 m and walls cut along the chunk border stay plain.
            Assert.AreEqual(2, ChunkBuildings.Build(Chunk(new[] { Rect(43, 200, 200, 2, 10) })).PatternWalls);
            Assert.AreEqual(3, ChunkBuildings.Build(Chunk(new[] { Rect(44, 0, 200, 10, 10) })).PatternWalls);
            // Religious, special and informal buildings stay plain.
            Assert.AreEqual(0, ChunkBuildings.Build(Chunk(new[] { Rect(45, 200, 200, 21, 9, "mosque") })).PatternWalls);
            Assert.AreEqual(0, ChunkBuildings.Build(Chunk(new[] { Rect(46, 200, 200, 3, 3) })).PatternWalls);
            // Other families' bays.
            var hall = Coordinates(ChunkBuildings.Build(Chunk(new[] { Rect(47, 200, 200, 21, 9, "warehouse") })));
            Assert.AreEqual(new[] { 1f, 3f }, hall.Select(x => x.u).Where(u => u > 0).Distinct().OrderBy(u => u).ToArray(), "6 m bays");
            Assert.IsTrue(hall.Where(x => x.w != 0).All(x => (x.w & 7) == (int)FacadeFamily.Industrial));
            // A plain building's walls still carry the wall flag and metres, with no family.
            var mosque = Coordinates(ChunkBuildings.Build(Chunk(new[] { Rect(48, 200, 200, 21, 9, "mosque") })));
            Assert.AreEqual(16, mosque.Count(x => x.w != 0));
            Assert.IsTrue(mosque.Where(x => x.w != 0).All(x => (x.w & 7) == 0 && (x.w & ChunkBuildings.WallFlag) != 0));
        }

        [Test]
        public void WallTexturesRunAlongTheWallAtItsLength()
        {
            // K-21: a square turned 45 degrees, 10 m a side. Box projection gave each wall 7.07 m of
            // texture; the builder's metres along the ring give 10, continuous around the corners.
            double h = 10 / Math.Sqrt(2);
            var b = new Building
            {
                Id = 60, HeightCm = 960, HeightSource = "estimated",
                Outer = new[] { Cm(200), Cm(200 - h), Cm(200 + h), Cm(200), Cm(200), Cm(200 + h), Cm(200 - h), Cm(200) },
            };
            var s = ChunkBuildings.Build(Chunk(new[] { b }));
            var f = Coordinates(s);
            float start = float.NaN;
            for (int wall = 0; wall < 4; wall++)
            {
                int v = 4 * wall;
                Assert.AreNotEqual(0, f[v].w & ChunkBuildings.WallFlag);
                float a = f[v].s, e = f[v + 1].s;
                Assert.AreEqual(10.0, e - a, 0.02, $"wall {wall}: metres along it");
                Assert.AreEqual(a, f[v + 3].s, "top p");
                Assert.AreEqual(e, f[v + 2].s, "top q");
                if (wall == 0) start = a;
                else Assert.AreEqual(start + 10 * wall, a, 0.05, "continuous from the last corner");
            }
            Assert.AreEqual(8f * ((f[0].w >> 4) & 255), start, "starts at 8 m x the seed");
        }

        [Test]
        public void ShopsOnTheWallFacingTheRoad()
        {
            // A shop-house 6 m from a primary road's carriageway: its front wall (z = 111 m) has shops, the rest not.
            var s = ChunkBuildings.Build(Chunk(new[] { Rect(50, 200, 111, 12, 9) }, new[] { Street("primary") }));
            Assert.AreEqual(BuildingCategory.MixedUse, s.Categories[0]);
            Assert.AreEqual(4, s.PatternWalls);
            Assert.AreEqual(1, s.ShopWalls);
            for (int i = 0; i < s.VertexCount; i++)
            {
                int w = (int)s.Facade[4 * i + 2];
                if (w == 0) continue;
                Assert.AreEqual((int)FacadeFamily.ShopHouse, w & 7);
                // Every vertex of the shop wall is on z = 111; no other wall has all four there.
                if ((w & 8) != 0) Assert.AreEqual(111f, s.Positions[3 * i + 2], 1e-3f);
            }
            // A walk-up on the same spot by type gets no shops: only shop-houses and commercial buildings have them.
            Assert.AreEqual(0, ChunkBuildings.Build(Chunk(new[] { Rect(51, 200, 111, 12, 9, "apartments") }, new[] { Street("primary") })).ShopWalls);
            // A service road is not frontage for shops.
            Assert.AreEqual(0, ChunkBuildings.Build(Chunk(new[] { Rect(52, 200, 111, 12, 9, "commercial") }, new[] { Street("service") })).ShopWalls);
            Assert.AreEqual(1, ChunkBuildings.Build(Chunk(new[] { Rect(53, 200, 111, 12, 9, "commercial") }, new[] { Street("residential") })).ShopWalls);
        }

        [Test]
        public void TheSliceIsClassifiedAndItsFacadesSitOnWalls()
        {
            var sw = new Stopwatch();
            var byId = new Dictionary<long, BuildingCategory>();
            var cats = new Dictionary<BuildingCategory, int>();
            var srcs = new Dictionary<ClassSource, int>();
            int pieces = 0, patternWalls = 0, shopWalls = 0, m2 = 0, chunks = 0;
            for (int ix = 6; ix <= 9; ix++)
                for (int iy = -30; iy <= -28; iy++)
                {
                    var file = Path.Combine(ChunkDir, $"c_{ix}_{iy}.dadc");
                    Assume.That(File.Exists(file), "world data missing: run Tools/worldgen/build.sh");
                    var c = Dadc.Load(File.ReadAllBytes(file));
                    sw.Start();
                    var s = ChunkBuildings.Build(c);
                    sw.Stop();
                    chunks++;
                    Assert.AreEqual(4 * s.VertexCount, s.Facade.Length);
                    patternWalls += s.PatternWalls;
                    shopWalls += s.ShopWalls;
                    for (int i = 0; i < c.Buildings.Length; i++)
                    {
                        var b = c.Buildings[i];
                        if (b.Outer == null || b.Outer.Length < 6) continue;
                        pieces++;
                        var k = s.Categories[i];
                        cats[k] = cats.TryGetValue(k, out int n) ? n + 1 : 1;
                        srcs[s.Sources[i]] = srcs.TryGetValue(s.Sources[i], out n) ? n + 1 : 1;
                        if (byId.TryGetValue(b.Id, out var seen)) Assert.AreEqual(seen, k, $"building {b.Id}: its pieces disagree");
                        else byId[b.Id] = k;
                        // M2's geometry facades carry no far-tier coordinates.
                        if (s.VertexCounts[i] > 0 && ChunkBuildings.Facaded(c, b, s.Placements[i]))
                        {
                            m2++;
                            for (int v = s.FirstVertex[i]; v < s.FirstVertex[i] + s.VertexCounts[i]; v++)
                                Assert.AreEqual(0, (int)s.Facade[4 * v + 2] & 15, "M2 geometry facades carry no far-tier family or shops");
                        }
                    }
                    // Wall-coded triangles are vertical, with one code on all three corners.
                    for (int t = 0; t < s.IndexCount; t += 3)
                    {
                        int a = s.Indices[t], b = s.Indices[t + 1], d = s.Indices[t + 2];
                        float w = s.Facade[4 * a + 2];
                        if (w == 0 && s.Facade[4 * b + 2] == 0 && s.Facade[4 * d + 2] == 0) continue;
                        Assert.AreEqual(w, s.Facade[4 * b + 2]);
                        Assert.AreEqual(w, s.Facade[4 * d + 2]);
                        Assert.AreNotEqual(0, (int)w & ChunkBuildings.WallFlag);
                        Assert.That((int)w & 7, Is.InRange(0, 5));
                        var p0 = P(s, a);
                        var n = Vector3.Cross(P(s, b) - p0, P(s, d) - p0);
                        Assert.Less(Mathf.Abs(n.normalized.y), 1e-3f, $"chunk c_{ix}_{iy} triangle {t / 3} is not vertical");
                    }
                }
            Assert.AreEqual(12, chunks);
            Assert.Greater(patternWalls, 0);
            Assert.Greater(shopWalls, 0);
            Assert.Greater(m2, 0, "the validation area is in the slice");
            Assert.AreEqual(byId.Count, byId.Count(kv => Enum.IsDefined(typeof(BuildingCategory), kv.Value)));
            Debug.Log($"[D-06a] slice: {pieces} pieces, {byId.Count} buildings; " +
                      string.Join(", ", cats.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} {kv.Value}")) + "; sources " +
                      string.Join(", ", srcs.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} {kv.Value}")) +
                      $"; far-tier walls {patternWalls}, with shops {shopWalls}; M2 pieces {m2}; build {sw.Elapsed.TotalMilliseconds:F0} ms for 12 chunks");
        }

        static Vector3 P(ChunkBuildings s, int v) => new Vector3(s.Positions[3 * v], s.Positions[3 * v + 1], s.Positions[3 * v + 2]);

        [Test]
        public void ShaderFamiliesMatchTheEnum()
        {
            string src = File.ReadAllText(ShaderPath);
            var defines = Regex.Matches(src, @"#define FAMILY_(\w+) (\d+)").Cast<Match>().ToDictionary(m => m.Groups[1].Value, m => int.Parse(m.Groups[2].Value));
            foreach (FacadeFamily f in Enum.GetValues(typeof(FacadeFamily)))
            {
                if (f == FacadeFamily.None) continue;
                Assert.IsTrue(defines.TryGetValue(f.ToString().ToUpperInvariant(), out int n), $"FAMILY_{f.ToString().ToUpperInvariant()} missing");
                Assert.AreEqual((int)f, n, f.ToString());
            }
            Assert.AreEqual(Enum.GetValues(typeof(FacadeFamily)).Length - 1, defines.Count);
        }

        [Test]
        public void FacadeMaterialIsTheWorldMaterialWithItsKeyword()
        {
            var world = Resources.Load<Material>("Materials/World");
            var facades = Resources.Load<Material>("Materials/WorldFacades");
            Assert.IsNotNull(facades, "Resources/Materials/WorldFacades.mat");
            Assert.AreSame(world.shader, facades.shader);
            Assert.IsTrue(facades.IsKeywordEnabled("_DAD_FACADE"));
            Assert.IsFalse(world.IsKeywordEnabled("_DAD_FACADE"));
            foreach (var t in new[] { "_WorldAlbedo", "_WorldNormal", "_WorldMask", "_WorldMacro" })
                Assert.AreSame(world.GetTexture(t), facades.GetTexture(t), t);
            Assert.AreEqual(world.GetFloat("_ShadowCull"), facades.GetFloat("_ShadowCull"));

            var shader = facades.shader;
            var copy = new Material(facades);
            try
            {
                ShaderUtil.ClearShaderMessages(shader);
                foreach (var k in new[] { "_MAIN_LIGHT_SHADOWS_CASCADE", "_CLUSTER_LIGHT_LOOP", "_SHADOWS_SOFT", "_SHADOWS_SOFT_HIGH", "_SCREEN_SPACE_OCCLUSION", "FOG_EXP2", "_ADDITIONAL_LIGHTS" })
                    copy.EnableKeyword(k);
                int pass = copy.FindPass("ForwardLit");
                ShaderUtil.CompilePass(copy, pass, true);
                Assert.IsTrue(ShaderUtil.IsPassCompiled(copy, pass));
                var errors = ShaderUtil.GetShaderMessages(shader).Where(m => m.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error)
                    .Select(m => $"{m.platform} line {m.line}: {m.message}").ToArray();
                Assert.IsEmpty(errors, string.Join("\n", errors));
            }
            finally { Object.DestroyImmediate(copy); }
        }
    }
}
