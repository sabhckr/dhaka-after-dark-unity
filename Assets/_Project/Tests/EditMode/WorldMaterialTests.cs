// D-01 world materials: the material id rides in the vertex colour's alpha; every slice vertex
// carries the material its surface should have; the shader's material tables match the enum, and
// their texture layers and tile periods the C# ones (D-02); the shader compiles, in the variants
// the Farmgate views use; the world material casts shadows from back faces; world UVs stay put
// across floating-origin shifts.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Dad.World;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Dad.Tests
{
    public class WorldMaterialTests
    {
        const string ShaderPath = "Assets/_Project/Shaders/DadWorld.shader";

        [Test]
        public void MaterialRidesInAlpha()
        {
            uint packed = WorldMaterials.Pack(0x123456, WorldMaterial.Roof);
            Assert.AreEqual(0x08563412u, packed, "r in the low byte, the material in alpha");
            Assert.AreEqual(WorldMaterial.Roof, WorldMaterials.Of(packed));
            uint marked = WorldMaterials.Night(packed, WorldMaterials.NightSign);
            Assert.AreEqual(WorldMaterial.Roof, WorldMaterials.Of(marked), "a night mark leaves the material");
            Assert.AreEqual(WorldMaterials.NightSign, WorldMaterials.NightOf(marked));
            Assert.AreEqual(0u, WorldMaterials.NightOf(packed));
            Assert.AreEqual(packed & 0xffffffu, marked & 0xffffffu, "or the colour");
            Assert.AreEqual(packed, BuildingPalette.Shade(0x123456, 1, WorldMaterial.Roof));
            Assert.AreEqual(WorldMaterials.Pack(0x091a2b, WorldMaterial.Plaster), BuildingPalette.Shade(0x123456, 0.5, WorldMaterial.Plaster), "shade scales rgb only");
        }

        [Test]
        public void SliceVerticesHaveTheirMaterials()
        {
            string dir = Path.Combine(TestWorld.Root, "chunks");
            Assume.That(File.Exists(Path.Combine(dir, "c_7_-30.dadc")), "world data missing: run Tools/worldgen/build.sh");
            var ground = new Dictionary<WorldMaterial, int>();
            var roads = new Dictionary<WorldMaterial, int>();
            var buildings = new Dictionary<WorldMaterial, int>();
            var street = new HashSet<WorldMaterial>();
            var facade = new HashSet<WorldMaterial>();
            int walls = 0, caps = 0, wrong = 0;
            string firstWrong = null;
            for (int ix = 6; ix <= 9; ix++)
            for (int iy = -30; iy <= -28; iy++)
            {
                var c = Dadc.Load(File.ReadAllBytes(Path.Combine(dir, $"c_{ix}_{iy}.dadc")));
                var g = ChunkSurfaces.Build(c);
                for (int v = 0; v < g.VertexCount; v++) Count(ground, WorldMaterials.Of(g.Colors[v]));
                var r = ChunkRoads.Build(c);
                for (int v = 0; v < r.VertexCount; v++) Count(roads, WorldMaterials.Of(r.Colors[v]));
                for (int v = 0; v < r.Street.VertexCount; v++) street.Add(WorldMaterials.Of(r.Street.Colors[v]));
                var b = ChunkBuildings.Build(c);
                for (int v = 0; v < b.VertexCount; v++) Count(buildings, WorldMaterials.Of(b.Colors[v]));
                // Since M2 the buildings in the Farmgate validation area have facades (Facades.cs): window glass and
                // grilles, shop shutters, interiors, signs and awnings. Those are checked against the facade set.
                var detailed = new bool[b.VertexCount];
                for (int k = 0; k < c.Buildings.Length; k++)
                    if (b.VertexCounts[k] > 0 && ChunkBuildings.Facaded(c, c.Buildings[k], b.Placements[k]))
                        for (int v = b.FirstVertex[k]; v < b.FirstVertex[k] + b.VertexCounts[k]; v++) detailed[v] = true;
                for (int v = 0; v < b.VertexCount; v++)
                    if (detailed[v]) facade.Add(WorldMaterials.Of(b.Colors[v]));
                // Since D-06b walls are in the building's skin material (BuildingSkin); flat faces are its roof, the
                // parapet's concrete coping, or concrete undersides under raised buildings.
                var owner = new int[b.VertexCount];
                for (int k = 0; k < c.Buildings.Length; k++)
                    for (int v = b.FirstVertex[k]; v < b.FirstVertex[k] + b.VertexCounts[k]; v++) owner[v] = k;
                for (int t = 0; t < b.IndexCount; t += 3)
                {
                    int i0 = b.Indices[t], i1 = b.Indices[t + 1], i2 = b.Indices[t + 2];
                    if (detailed[i0]) continue;
                    bool flat = b.Positions[3 * i0 + 1] == b.Positions[3 * i1 + 1] && b.Positions[3 * i1 + 1] == b.Positions[3 * i2 + 1];
                    var m = WorldMaterials.Of(b.Colors[i0]);
                    var skin = b.Skins[owner[i0]];
                    if (flat) caps++; else walls++;
                    bool ok = flat ? m == skin.Roof || m == WorldMaterial.Concrete : m == skin.Wall;
                    ok &= WorldMaterials.Of(b.Colors[i1]) == m && WorldMaterials.Of(b.Colors[i2]) == m;
                    if (!ok && wrong++ == 0) firstWrong = $"c_{ix}_{iy} triangle {t / 3}: {m}, flat {flat}";
                }
            }
            Debug.Log($"Slice vertices by material: ground {Show(ground)}; roads {Show(roads)}; buildings {Show(buildings)}; building triangles {walls} wall, {caps} flat (outside the facades); facades {string.Join(", ", facade)}; street {string.Join(", ", street)}");
            CollectionAssert.IsSubsetOf(ground.Keys, new[] { WorldMaterial.Soil, WorldMaterial.Grass, WorldMaterial.Water, WorldMaterial.Paving });
            CollectionAssert.IsSubsetOf(new[] { WorldMaterial.Soil, WorldMaterial.Grass }, ground.Keys);
            // Since D-04 the road mesh also carries its markings and details: paint, iron covers and grates,
            // and the mud in potholes; since D-05 its sidewalks, kerbs and drains (the water in their channels).
            CollectionAssert.IsSubsetOf(roads.Keys, new[] { WorldMaterial.Asphalt, WorldMaterial.Concrete, WorldMaterial.Paving,
                WorldMaterial.Marking, WorldMaterial.Metal, WorldMaterial.Soil, WorldMaterial.Water });
            CollectionAssert.IsSubsetOf(new[] { WorldMaterial.Asphalt, WorldMaterial.Paving, WorldMaterial.Marking }, roads.Keys);
            // D-06b: brick, corrugated sheet and roof tiles join the building materials.
            CollectionAssert.AreEquivalent(new[] { WorldMaterial.Plaster, WorldMaterial.Roof, WorldMaterial.Concrete, WorldMaterial.Default, WorldMaterial.Metal, WorldMaterial.Paint, WorldMaterial.Glass,
                WorldMaterial.Brick, WorldMaterial.RoofSheet, WorldMaterial.Tiles }, buildings.Keys);
            // M2's facaded buildings take D-06b's roof coatings (BuildingSkin): mosaic tiles among them in the slice.
            CollectionAssert.AreEquivalent(new[] { WorldMaterial.Plaster, WorldMaterial.Roof, WorldMaterial.Concrete, WorldMaterial.Default, WorldMaterial.Metal, WorldMaterial.Paint, WorldMaterial.Glass, WorldMaterial.Tiles }, facade);
            // Street furniture and structures (M2): concrete and metal poles, porcelain insulators, telecom cable and bark,
            // painted transformers and dividers, leaves, lamp glass and the viaduct's weathered running rails.
            // D-06b's rooftop kit: stair rooms in their building's wall material (plaster, paint, brick, concrete).
            CollectionAssert.AreEquivalent(new[] { WorldMaterial.Concrete, WorldMaterial.Metal, WorldMaterial.Tiles, WorldMaterial.Default, WorldMaterial.Paint, WorldMaterial.Grass, WorldMaterial.Glass, WorldMaterial.Rust,
                WorldMaterial.Plaster, WorldMaterial.Brick }, street);
            Assert.AreEqual(0, wrong, firstWrong);
        }

        static void Count(Dictionary<WorldMaterial, int> d, WorldMaterial m) => d[m] = d.TryGetValue(m, out int n) ? n + 1 : 1;

        static string Show(Dictionary<WorldMaterial, int> d) => string.Join(", ", d.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key} {kv.Value}"));

        [Test]
        public void ShaderTableMatchesTheEnum()
        {
            var text = File.ReadAllText(ShaderPath);
            int count = int.Parse(Regex.Match(text, @"#define MATERIAL_COUNT (\d+)").Groups[1].Value);
            var ids = Enum.GetValues(typeof(WorldMaterial)).Cast<WorldMaterial>().OrderBy(m => (byte)m).ToArray();
            Assert.AreEqual(ids.Length, count, "MATERIAL_COUNT");
            for (int i = 0; i < ids.Length; i++) Assert.AreEqual(i, (int)ids[i], "ids run 0..n-1");
            foreach (var table in new[] { "MaterialTable", "MaterialWear" })
            {
                var block = Regex.Match(text, table + @"\[MATERIAL_COUNT\] =\s*\{(.*?)\};", RegexOptions.Singleline).Groups[1].Value;
                var names = Regex.Matches(block, @"//\s*(\w+)").Cast<Match>().Select(m => m.Groups[1].Value).ToArray();
                CollectionAssert.AreEqual(ids.Select(m => m.ToString()).ToArray(), names, $"{table} rows in enum order");
            }

            // Each row's texture layer and tile period are the C# ones (WorldMaterials.LayerOf, LayerRecipes).
            var rows = Regex.Matches(Regex.Match(text, @"MaterialTable\[MATERIAL_COUNT\] =\s*\{(.*?)\};", RegexOptions.Singleline).Groups[1].Value,
                @"float4\(([^)]*)\),\s*//\s*(\w+)").Cast<Match>().ToArray();
            for (int i = 0; i < ids.Length; i++)
            {
                var v = rows[i].Groups[1].Value.Split(',').Select(s => float.Parse(s, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
                var layer = WorldMaterials.LayerOf(ids[i]);
                Assert.AreEqual((int)layer, (int)v[0], $"{ids[i]} layer");
                Assert.AreEqual(Dad.EditorTools.Materials.LayerRecipes.All[(int)layer].Period, v[1], $"{ids[i]} period");
                Assert.That((int)v[2], Is.InRange(0, 2), $"{ids[i]} sampling mode");
                Assert.AreEqual(0.0, WorldMaterials.WrapPeriod % v[1], $"{ids[i]} period divides the wrap period");
            }
        }

        [Test]
        public void ShaderCompilesAndCastsFromBackFaces()
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            Assert.IsNotNull(shader);
            Assert.AreEqual("Dad/World", shader.name);
            var world = Resources.Load<Material>("Materials/World");
            Assert.AreSame(shader, world.shader);
            Assert.AreEqual((float)CullMode.Front, world.GetFloat("_ShadowCull"), "shadow casters cull front faces");
            var passes = new[] { "ForwardLit", "ShadowCaster", "DepthOnly", "DepthNormals" };
            foreach (var p in passes) Assert.GreaterOrEqual(world.FindPass(p), 0, p);

            // The plain variant of every pass, then the forward pass with the keywords a Farmgate
            // view turns on (cascaded soft shadows, Forward+, SSAO, fog), compiled now.
            var copy = new Material(world);
            try
            {
                ShaderUtil.ClearShaderMessages(shader);
                for (int i = 0; i < copy.passCount; i++) ShaderUtil.CompilePass(copy, i, true);
                foreach (var k in new[] { "_MAIN_LIGHT_SHADOWS_CASCADE", "_CLUSTER_LIGHT_LOOP", "_SHADOWS_SOFT", "_SHADOWS_SOFT_HIGH", "_SCREEN_SPACE_OCCLUSION", "FOG_EXP2", "_ADDITIONAL_LIGHTS" })
                    copy.EnableKeyword(k);
                ShaderUtil.CompilePass(copy, copy.FindPass("ForwardLit"), true);
                for (int i = 0; i < copy.passCount; i++) Assert.IsTrue(ShaderUtil.IsPassCompiled(copy, i), $"pass {i} compiled");
                var errors = ShaderUtil.GetShaderMessages(shader).Where(m => m.severity == ShaderCompilerMessageSeverity.Error)
                    .Select(m => $"{m.platform} line {m.line}: {m.message}").ToArray();
                Assert.IsEmpty(errors, string.Join("\n", errors));
                Assert.IsFalse(ShaderUtil.ShaderHasError(shader));
            }
            finally { Object.DestroyImmediate(copy); }
        }

        [Test]
        public void WorldUvsHoldAcrossOriginShifts()
        {
            // The origin moves in whole cells; the shader adds Wrap(offset) to the scene position.
            // For any shift, the wrapped sum must equal the true world position modulo 4,000 m,
            // and stay small enough for float precision.
            var rng = new System.Random(1);
            for (int i = 0; i < 2000; i++)
            {
                double offset = rng.Next(-200, 200) * ChunkCell.Size;
                double world = offset + rng.NextDouble() * 4000 - 2000;
                float scene = (float)(world - offset);
                double wrapped = WorldMaterials.Wrap(offset);
                Assert.That(wrapped, Is.InRange(0.0, WorldMaterials.WrapPeriod - 1));
                Assert.AreEqual(0.0, wrapped % ChunkCell.Size, "a whole number of cells");
                double sum = scene + (float)wrapped, diff = Math.IEEERemainder(sum - world, WorldMaterials.WrapPeriod);
                Assert.AreEqual(0.0, diff, 1e-3, $"offset {offset}, world {world}");
                Assert.Less(Math.Abs(sum), 6100, "stays small");
            }
            // Tile periods (0.5 to 8), the macro texture's two scales (50, 500), the repair grid (8)
            // and the sampling lattice (2) all divide the wrap period.
            foreach (var period in new[] { 0.5, 1.0, 1.25, 2, 2.5, 4, 8, 16, 32, 50, 125, 250, 500, 1000 })
                Assert.AreEqual(0.0, WorldMaterials.WrapPeriod % period, $"{period} m divides the wrap period");
        }
    }
}
