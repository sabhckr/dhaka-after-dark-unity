// D-02 material library: every generated layer tiles; generation is deterministic and reproduces
// the committed files; the flipbooks import as 16-slice BC7 arrays in layer order; texture memory
// stays within budget; the stochastic sampling lattice holds across floating-origin shifts.
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Dad.EditorTools.Materials;
using Dad.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Profiling;
using Object = UnityEngine.Object;

namespace Dad.Tests
{
    public class MaterialLibraryTests
    {
        [Test]
        public void EveryLayerTiles()
        {
            // Tiling, exactly: every recipe gives the same texel one tile over, in s, in t, and both.
            // (MaterialLibrary's blur and normals wrap their pixel indices too.) Float rounding of
            // s + 1 can tip a sample across a hard edge (a joint, a crack), so a few in a thousand
            // may differ.
            var rng = new System.Random(3);
            const int Samples = 4000;
            foreach (var r in LayerRecipes.All)
            {
                int differ = 0;
                string first = null;
                for (int i = 0; i < Samples; i++)
                {
                    float s = (float)rng.NextDouble(), t = (float)rng.NextDouble();
                    var a = Eval(r, s, t);
                    foreach (var (ds, dt) in new[] { (1f, 0f), (0f, 1f), (-1f, 2f) })
                    {
                        var b = Eval(r, s + ds, t + dt);
                        float d = Math.Max(Math.Max(Math.Abs(a.R - b.R), Math.Abs(a.G - b.G)), Math.Max(Math.Abs(a.B - b.B),
                            Math.Max(Math.Abs(a.H - b.H) * 100, Math.Max(Math.Abs(a.Smooth - b.Smooth), Math.Max(Math.Abs(a.Metal - b.Metal), Math.Abs(a.Crack - b.Crack))))));
                        if (d > 1e-3f && differ++ == 0) first = $"{r.Layer} at ({s}, {t}) + ({ds}, {dt}): differs by {d}";
                    }
                }
                Assert.LessOrEqual(differ, Samples * 3 / 500, first);
            }
        }

        static Texel Eval(LayerRecipe r, float s, float t)
        {
            var e = new Texel();
            r.Pixel(s, t, ref e);
            return e;
        }

        [Test]
        public void GenerationIsDeterministic()
        {
            var a = MaterialLibrary.Generate(64, 32);
            var b = MaterialLibrary.Generate(64, 32);
            CollectionAssert.AreEqual(a.Albedo, b.Albedo);
            CollectionAssert.AreEqual(a.Normal, b.Normal);
            CollectionAssert.AreEqual(a.Mask, b.Mask);
            CollectionAssert.AreEqual(a.Macro, b.Macro);
        }

        [Test]
        public void CommittedFilesAreTheGeneratorsOutput()
        {
            // Regenerates at full size (about 10-30 s) and compares SHA-256 with the manifest and
            // the files on disk: the textures in the repository are reproducible from the code.
            var manifest = File.ReadAllText(Path.Combine(MaterialLibrary.Dir, "MANIFEST.json"));
            var o = MaterialLibrary.Generate();
            foreach (var (name, png) in new[]
            {
                (MaterialLibrary.Albedo, MaterialLibrary.Png(o.Albedo, o.Width, o.Height, 3)),
                (MaterialLibrary.Normal, MaterialLibrary.Png(o.Normal, o.Width, o.Height, 4)),
                (MaterialLibrary.Mask, MaterialLibrary.Png(o.Mask, o.Width, o.Height, 4)),
                (MaterialLibrary.Macro, MaterialLibrary.Png(o.Macro, o.MacroSize, o.MacroSize, 4)),
            })
            {
                string hash = MaterialLibrary.Sha256(png);
                StringAssert.Contains($"\"{name}\": \"{hash}\"", manifest, $"{name} in MANIFEST.json");
                Assert.AreEqual(hash, MaterialLibrary.Sha256(File.ReadAllBytes(Path.Combine(MaterialLibrary.Dir, name))), $"{name} on disk");
            }
        }

        [Test]
        public void ArraysImportAsSixteenLayersInOrder()
        {
            var albedo = AssetDatabase.LoadAssetAtPath<Texture2DArray>($"{MaterialLibrary.Dir}/{MaterialLibrary.Albedo}");
            var normal = AssetDatabase.LoadAssetAtPath<Texture2DArray>($"{MaterialLibrary.Dir}/{MaterialLibrary.Normal}");
            var mask = AssetDatabase.LoadAssetAtPath<Texture2DArray>($"{MaterialLibrary.Dir}/{MaterialLibrary.Mask}");
            foreach (var a in new[] { albedo, normal, mask })
            {
                Assert.IsNotNull(a, "imported as a Texture2DArray");
                Assert.AreEqual(WorldMaterials.LayerCount, a.depth, $"{a.name} slices");
                Assert.AreEqual(MaterialLibrary.LayerSize, a.width);
                Assert.AreEqual(12, a.mipmapCount, $"{a.name} mips down to 1 x 1");
                Assert.AreEqual(TextureFormat.BC7, a.format, $"{a.name} format");
                Assert.AreEqual(TextureWrapMode.Repeat, a.wrapMode);
            }
            var world = Resources.Load<Material>("Materials/World");
            Assert.AreSame(albedo, world.GetTexture("_WorldAlbedo"));
            Assert.AreSame(normal, world.GetTexture("_WorldNormal"));
            Assert.AreSame(mask, world.GetTexture("_WorldMask"));
            Assert.IsNotNull(world.GetTexture("_WorldMacro"));

            // Each slice's last mip is its mean colour; it must be the manifest's mean for that layer.
            var means = MaterialSheet.LayerMeans();
            var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/_Project/Editor/Materials/ArraySlice.shader");
            var mat = new Material(shader);
            var rt = RenderTexture.GetTemporary(1, 1, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            var read = new Texture2D(1, 1, TextureFormat.RGBAFloat, false, true);
            try
            {
                mat.SetTexture("_Array", albedo);
                string report = "";
                for (int k = 0; k < WorldMaterials.LayerCount; k++)
                {
                    mat.SetFloat("_Slice", k);
                    Graphics.Blit(null, rt, mat);
                    RenderTexture.active = rt;
                    read.ReadPixels(new Rect(0, 0, 1, 1), 0, 0);
                    RenderTexture.active = null;
                    var got = read.GetPixel(0, 0);
                    int best = -1;
                    double bestD = double.MaxValue;
                    for (int j = 0; j < means.Length; j++)
                    {
                        var m = ((Color)means[j]).linear;
                        double d = Math.Abs(m.r - got.r) + Math.Abs(m.g - got.g) + Math.Abs(m.b - got.b);
                        if (d < bestD) { bestD = d; best = j; }
                    }
                    var want = ((Color)means[k]).linear;
                    report += $"{(MaterialLayer)k} {got.r:F3},{got.g:F3},{got.b:F3} vs {want.r:F3},{want.g:F3},{want.b:F3}; ";
                    Assert.AreEqual(k, best, $"slice {k} looks like layer {(MaterialLayer)best}: {report}");
                    Assert.AreEqual(want.r, got.r, 0.02 + 0.1 * want.r, report);
                    Assert.AreEqual(want.g, got.g, 0.02 + 0.1 * want.g, report);
                    Assert.AreEqual(want.b, got.b, 0.02 + 0.1 * want.b, report);
                }
                Debug.Log($"Imported slice means (linear) vs manifest: {report}");
            }
            finally
            {
                RenderTexture.ReleaseTemporary(rt);
                Object.DestroyImmediate(read);
                Object.DestroyImmediate(mat);
            }
        }

        [Test]
        public void TextureMemoryIsWithinBudget()
        {
            // ENVIRONMENT_ROADMAP, D-02: the base library stays under 24 MB of texture memory, so the
            // facade, prop and sign atlases of later milestones fit the 1 GB LOW-tier texture budget.
            // GPU size from format, size, mips and slices. The editor's profiler figure is logged
            // too: it also counts the editor's CPU copy, so it reads double.
            long total = 0, profiler = 0;
            string detail = "";
            foreach (var name in new[] { MaterialLibrary.Albedo, MaterialLibrary.Normal, MaterialLibrary.Mask, MaterialLibrary.Macro })
            {
                var t = AssetDatabase.LoadAssetAtPath<Texture>($"{MaterialLibrary.Dir}/{name}");
                int depth = t is Texture2DArray a ? a.depth : 1;
                long bytes = 0;
                for (int m = 0; m < t.mipmapCount; m++)
                    bytes += (long)GraphicsFormatUtility.ComputeMipmapSize(Math.Max(1, t.width >> m), Math.Max(1, t.height >> m), t.graphicsFormat) * depth;
                total += bytes;
                profiler += Profiler.GetRuntimeMemorySizeLong(t);
                detail += $"{name} {bytes / 1048576.0:F2} MB ({t.graphicsFormat}, {depth} x {t.width}, {t.mipmapCount} mips); ";
            }
            Debug.Log($"World material texture memory: {total / 1048576.0:F2} MB on the GPU ({detail.TrimEnd(' ', ';')}); editor profiler {profiler / 1048576.0:F2} MB");
            // Owner, 2026-11-11 (BUILDING_ASSETS_AUDIT.md): scanned layers at 2048 on High, 1024 on Medium and Low.
            // Was 24 MB at 512. High holds the full arrays; Medium and Low skip the top mip through the "World" group.
            Assert.Less(total, 280L * 1024 * 1024, detail);
            int was = QualitySettings.GetQualityLevel();
            try
            {
                for (int q = 0; q < QualitySettings.names.Length; q++)
                {
                    QualitySettings.SetQualityLevel(q, false);
                    var s = QualitySettings.GetTextureMipmapLimitSettings(MaterialLibrary.MipGroup);
                    Assert.AreEqual(QualitySettings.names[q] == "High" ? 0 : 1, s.limitBias, $"{QualitySettings.names[q]} skips this many mips of the library");
                }
            }
            finally { QualitySettings.SetQualityLevel(was, false); }
        }

        [Test]
        public void ArtTexturesAreWithinTheBudget()
        {
            // Owner, 2026-11-07 (K-63): all the game's art textures together stay within 48 MB on the GPU. That is the world
            // library above (its own 24 MB limit stays), the owner's city model pictures, terrain, vehicles and people: every
            // texture under Assets/_Project. Render targets (shadow maps, screen buffers) are not art and are not counted.
            // Measured in the player on 2026-11-07: 36.7 MB without the people (Docs/PRODUCTION/CITY/bench.md).
            long total = 0;
            var byFolder = new System.Collections.Generic.SortedDictionary<string, long>();
            foreach (var guid in AssetDatabase.FindAssets("t:Texture", new[] { "Assets/_Project" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("/Tests/") || path.Contains("/Editor/")) continue;
                var t = AssetDatabase.LoadAssetAtPath<Texture>(path);
                if (t == null || t is RenderTexture) continue;
                int depth = t is Texture2DArray a ? a.depth : 1;
                long bytes = 0;
                for (int m = 0; m < t.mipmapCount; m++)
                    bytes += (long)GraphicsFormatUtility.ComputeMipmapSize(Math.Max(1, t.width >> m), Math.Max(1, t.height >> m), t.graphicsFormat) * depth;
                total += bytes;
                var folder = Path.GetDirectoryName(path).Replace('\\', '/');
                byFolder[folder] = (byFolder.TryGetValue(folder, out var b) ? b : 0) + bytes;
            }
            string detail = string.Join("; ", byFolder.Select(kv => $"{kv.Key} {kv.Value / 1048576.0:F2} MB"));
            Debug.Log($"Art texture memory: {total / 1048576.0:F2} MB on the GPU ({detail})");
            Assert.Greater(total, 16L * 1024 * 1024, "the world library alone is 16.33 MB: textures were not found");
            // Owner, 2026-11-09: raised from 48 MB for NPCs at full source quality. 23 complete models measured at 2,341 MB on the GPU; each new one will need this raised again, with its number measured.
            Assert.Less(total, 2500L * 1024 * 1024, detail);
        }

        [Test]
        public void SamplingLatticeHoldsAcrossOriginShifts()
        {
            // The shader picks random offsets per lattice vertex; after an origin shift the world UV of
            // a point moves by a multiple of 4,000 m, and its vertices must keep their ids.
            Assert.AreEqual(0, WorldMaterials.WrapPeriod % (WorldUvLattice.Size * WorldUvLattice.Period));
            var rng = new System.Random(2);
            for (int n = 0; n < 5000; n++)
            {
                double u = rng.NextDouble() * 12000 - 6000, v = rng.NextDouble() * 12000 - 6000;
                var (i, j) = WorldUvLattice.Cell(u, v);
                int kx = rng.Next(-3, 4), kz = rng.Next(-3, 4);
                var (i2, j2) = WorldUvLattice.Cell(u + 4000.0 * kx, v + 4000.0 * kz);
                Assert.AreEqual(j + 2000L * kz, j2, "rows step 2,000 per 4,000 m");
                Assert.AreEqual(i + 2000L * kx - 1000L * kz, i2, "columns");
                foreach (var (di, dj) in new[] { (0L, 0L), (1L, 0L), (0L, 1L), (1L, 1L) })
                    Assert.AreEqual(WorldUvLattice.Canonical(i + di, j + dj), WorldUvLattice.Canonical(i2 + di, j2 + dj), $"vertex ids at {u:F2}, {v:F2} shifted {kx}, {kz}");
            }
            // And the ids differ between distinct vertices of one 4,000 m square.
            var seen = new System.Collections.Generic.HashSet<uint>();
            for (long jj = 0; jj < 40; jj++) for (long ii = 0; ii < 40; ii++) Assert.IsTrue(seen.Add(WorldUvLattice.Canonical(ii * 50, jj * 50)));
        }
    }
}
