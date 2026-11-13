// The world material library (D-02, ENVIRONMENT_ROADMAP ED-2). Generates the 16 layers of
// LayerRecipes into three texture arrays and a macro texture under Assets/_Project/Textures/World,
// and hands them to Resources/Materials/World.mat (and the macro texture to Sky.mat, D-03).
//   Dad.EditorTools.Materials.MaterialLibrary.GenerateBatch   (batch mode: -executeMethod)
//   Dad.EditorTools.Materials.MaterialLibrary.LinkBatch       (the materials only)
//   menu Dad > Generate world materials
//
// Files (WorldTextureImport sets how they import):
//   WorldAlbedo.png  sRGB rgb albedo
//   WorldNormal.png  rg tangent-space normal (x along u, y along v), b height in the layer's range
//   WorldMask.png    r metallic, g occlusion, b cavity (cracks, joints, hollows), a smoothness
//                    These three are 4 x 4 flipbooks of 512-pixel layers: layer k at column k % 4,
//                    row k / 4 from the top. They import as 16-slice Texture2DArrays.
//   WorldMacro.png   512 px, tiled every 500 m (and again every 50 m) by the shader: r brightness,
//                    g grime, b fading, a low spots (kept for D-12 puddles)
//   MANIFEST.json    seed, each layer's period and mean colour, and the files' SHA-256
// Deterministic: the same code gives the same bytes on this machine (MaterialLibraryTests).
using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Dad.World;
using UnityEditor;
using UnityEngine;
using static Dad.EditorTools.Materials.TileNoise;

namespace Dad.EditorTools.Materials
{
    public static class MaterialLibrary
    {
        public const string Dir = "Assets/_Project/Textures/World";
        public const string WorldMaterialPath = "Assets/_Project/Resources/Materials/World.mat";
        /// <summary>World.mat with the far-tier facade keyword (D-06a), for the building meshes; same textures.</summary>
        public const string FacadeMaterialPath = "Assets/_Project/Resources/Materials/WorldFacades.mat";
        /// <summary>World.mat with the near-facade keyword (D-06c), for the near facades; same textures.</summary>
        public const string NearMaterialPath = "Assets/_Project/Resources/Materials/WorldNear.mat";
        public const string SkyMaterialPath = "Assets/_Project/Resources/Materials/Sky.mat";
        // 2048 since the scanned layers (owner 2026-11-11): High draws 2048, Medium and Low skip the top mip
        // through the "World" mipmap limit group (MipmapGroup), so they hold 1024.
        public const int LayerSize = 2048, Columns = 4, Rows = 4, MacroSize = 512;
        public const string MipGroup = "World";
        /// <summary>The Poly Haven sets, one folder per id with its 2K PNGs as downloaded; DAD_SCANS overrides.</summary>
        public static string ScanDir => Environment.GetEnvironmentVariable("DAD_SCANS")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "3D assets", "polyhaven");
        public const string Albedo = "WorldAlbedo.png", Normal = "WorldNormal.png", Mask = "WorldMask.png", Macro = "WorldMacro.png";

        public struct LayerStats
        {
            public MaterialLayer Layer;
            public float Period;
            public float R, G, B;          // mean linear albedo
            public float Smooth, Metal;    // means
            public float HMin, HMax;       // height range, metres
        }

        public sealed class Output
        {
            public int Width, Height, MacroSize;
            public byte[] Albedo, Normal, Mask, Macro;   // texture rows, bottom row first
            public LayerStats[] Stats;
        }

        /// <summary>Generates every layer and the macro texture in memory.</summary>
        public static Output Generate(int layerSize = LayerSize, int macroSize = MacroSize)
        {
            int w = layerSize * Columns, h = layerSize * Rows;
            var o = new Output
            {
                Width = w, Height = h, MacroSize = macroSize,
                Albedo = new byte[w * h * 3], Normal = new byte[w * h * 4], Mask = new byte[w * h * 4],
                Stats = new LayerStats[LayerRecipes.All.Length],
            };
            if (LayerRecipes.All.Length != WorldMaterials.LayerCount || LayerRecipes.All.Length != Columns * Rows)
                throw new InvalidOperationException($"{LayerRecipes.All.Length} recipes for {WorldMaterials.LayerCount} layers");
            for (int k = 0; k < LayerRecipes.All.Length; k++)
            {
                var r = LayerRecipes.All[k];
                if ((int)r.Layer != k) throw new InvalidOperationException($"recipe {k} is {r.Layer}");
                // Texture rows run bottom-up, so flipbook row k / 4 from the top starts this far up.
                o.Stats[k] = Bake(r, layerSize, o, (k % Columns) * layerSize, (Rows - 1 - k / Columns) * layerSize);
            }
            o.Macro = MacroTexture(macroSize);
            return o;
        }

        static LayerStats Bake(LayerRecipe recipe, int n, Output o, int x0, int y0)
        {
            var tex = new Texel[n * n];
            float[] scanNormal = null, scanAo = null;
            if (recipe.Scan != null) (tex, scanNormal, scanAo) = Scan(recipe, n);
            else Parallel.For(0, n, y =>
            {
                float t = (y + 0.5f) / n;
                for (int x = 0; x < n; x++)
                {
                    var e = new Texel();
                    recipe.Pixel((x + 0.5f) / n, t, ref e);
                    tex[y * n + x] = e;
                }
            });

            var height = new float[n * n];
            for (int i = 0; i < height.Length; i++) height[i] = tex[i].H;
            float texel = recipe.Period / n;
            // Hollows: how far a point sits below its surroundings (about 1.5 cm around).
            var around = BoxBlur(BoxBlur(height, n, Math.Max(2, (int)MathF.Round(0.015f / texel))), n, Math.Max(2, (int)MathF.Round(0.015f / texel)));
            double sr = 0, sg = 0, sb = 0, ss = 0, sm = 0;
            float hMin = float.MaxValue, hMax = float.MinValue;
            foreach (var hv in height) { hMin = MathF.Min(hMin, hv); hMax = MathF.Max(hMax, hv); }
            float hRange = MathF.Max(hMax - hMin, 1e-6f);
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                int i = y * n + x;
                var e = tex[i];
                float dhdu = (height[y * n + Wrap(x + 1, n)] - height[y * n + Wrap(x - 1, n)]) / (2 * texel);
                float dhdv = (height[Wrap(y + 1, n) * n + x] - height[Wrap(y - 1, n) * n + x]) / (2 * texel);
                float len = MathF.Sqrt(dhdu * dhdu + dhdv * dhdv + 1);
                float hollow = Saturate((around[i] - height[i]) / recipe.Relief);
                float occlusion = scanAo != null ? scanAo[i] : 1 - 0.55f * MathF.Pow(hollow, 0.8f);
                float cavity = MathF.Max(Saturate(e.Crack), hollow);

                int p = (y0 + y) * o.Width + x0 + x;
                o.Albedo[p * 3] = Byte(LinearToSrgb(e.R));
                o.Albedo[p * 3 + 1] = Byte(LinearToSrgb(e.G));
                o.Albedo[p * 3 + 2] = Byte(LinearToSrgb(e.B));
                o.Normal[p * 4] = scanNormal != null ? Byte(scanNormal[i * 2]) : Byte(0.5f - 0.5f * dhdu / len);
                o.Normal[p * 4 + 1] = scanNormal != null ? Byte(scanNormal[i * 2 + 1]) : Byte(0.5f - 0.5f * dhdv / len);
                o.Normal[p * 4 + 2] = Byte((height[i] - hMin) / hRange);
                o.Normal[p * 4 + 3] = 255;
                o.Mask[p * 4] = Byte(e.Metal);
                o.Mask[p * 4 + 1] = Byte(occlusion);
                o.Mask[p * 4 + 2] = Byte(cavity);
                o.Mask[p * 4 + 3] = Byte(e.Smooth);
                sr += e.R; sg += e.G; sb += e.B; ss += e.Smooth; sm += e.Metal;
            }
            double count = n * n;
            return new LayerStats
            {
                Layer = recipe.Layer, Period = recipe.Period,
                R = (float)(sr / count), G = (float)(sg / count), B = (float)(sb / count),
                Smooth = (float)(ss / count), Metal = (float)(sm / count),
                HMin = hMin, HMax = hMax,
            };
        }

        // A scanned layer at n x n, box-averaged down from the 2K source when n is smaller: albedo from the colour map,
        // height from displacement (a labelled guess: Poly Haven gives no depth, so ±2 × the layer's relief), smoothness
        // 1 - roughness, occlusion and metallic from the packed map, normals (encoded 0..1) from the OpenGL normal map.
        static (Texel[], float[], float[]) Scan(LayerRecipe r, int n)
        {
            float[] Read(string map)
            {
                var path = Path.Combine(ScanDir, r.Scan, $"{r.Scan}_{map}_2k.png");
                if (!File.Exists(path)) throw new FileNotFoundException($"scan missing (Docs/PRODUCTION/BUILDING_ASSETS_AUDIT.md): {path}");
                var t = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
                try
                {
                    if (!t.LoadImage(File.ReadAllBytes(path))) throw new InvalidOperationException($"cannot read {path}");
                    int s = t.width, f = s / n;
                    if (t.height != s || s % n != 0) throw new InvalidOperationException($"{path} is {t.width} x {t.height}, not a multiple of {n}");
                    var px = t.GetPixels();
                    var o = new float[n * n * 3];
                    Parallel.For(0, n, y =>
                    {
                        for (int x = 0; x < n; x++)
                        for (int c = 0; c < 3; c++)
                        {
                            float sum = 0;
                            for (int dy = 0; dy < f; dy++)
                            for (int dx = 0; dx < f; dx++) sum += px[(y * f + dy) * s + x * f + dx][c];
                            o[(y * n + x) * 3 + c] = sum / (f * f);
                        }
                    });
                    return o;
                }
                finally { UnityEngine.Object.DestroyImmediate(t); }
            }

            float[] diff = Read("diff"), nor = Read("nor_gl"), arm = Read("arm"), disp = Read("disp");
            var tex = new Texel[n * n];
            var normal = new float[n * n * 2];
            var ao = new float[n * n];
            for (int i = 0; i < tex.Length; i++)
            {
                tex[i] = new Texel
                {
                    R = SrgbToLinear(diff[i * 3]), G = SrgbToLinear(diff[i * 3 + 1]), B = SrgbToLinear(diff[i * 3 + 2]),
                    H = (disp[i * 3] - 0.5f) * 4 * r.Relief,
                    Smooth = 1 - arm[i * 3 + 1], Metal = arm[i * 3 + 2],
                };
                normal[i * 2] = nor[i * 3];
                normal[i * 2 + 1] = nor[i * 3 + 1];
                ao[i] = arm[i * 3];
            }
            return (tex, normal, ao);
        }

        static float SrgbToLinear(float c) => c <= 0.04045f ? c / 12.92f : MathF.Pow((c + 0.055f) / 1.055f, 2.4f);

        /// <summary>The "World" mipmap limit group: High draws the library at 2048, Medium and Low one mip down (1024).</summary>
        public static void MipmapGroup()
        {
            if (!TextureMipmapLimitGroups.HasGroup(MipGroup)) TextureMipmapLimitGroups.CreateGroup(MipGroup);
            int was = QualitySettings.GetQualityLevel();
            for (int q = 0; q < QualitySettings.names.Length; q++)
            {
                QualitySettings.SetQualityLevel(q, false);
                QualitySettings.SetTextureMipmapLimitSettings(MipGroup, new TextureMipmapLimitSettings
                {
                    limitBiasMode = TextureMipmapLimitBiasMode.OffsetGlobalLimit,
                    limitBias = QualitySettings.names[q] == "High" ? 0 : 1,
                });
            }
            QualitySettings.SetQualityLevel(was, false);
        }

        // Wrapped box blur of radius r, one axis at a time.
        static float[] BoxBlur(float[] src, int n, int r)
        {
            var a = new float[src.Length];
            var b = new float[src.Length];
            float k = 1f / (2 * r + 1);
            Parallel.For(0, n, y =>
            {
                for (int x = 0; x < n; x++)
                {
                    float sum = 0;
                    for (int d = -r; d <= r; d++) sum += src[y * n + Wrap(x + d, n)];
                    a[y * n + x] = sum * k;
                }
            });
            Parallel.For(0, n, y =>
            {
                for (int x = 0; x < n; x++)
                {
                    float sum = 0;
                    for (int d = -r; d <= r; d++) sum += a[Wrap(y + d, n) * n + x];
                    b[y * n + x] = sum * k;
                }
            });
            return b;
        }

        static byte[] MacroTexture(int n)
        {
            var px = new byte[n * n * 4];
            Parallel.For(0, n, y =>
            {
                float t = (y + 0.5f) / n;
                for (int x = 0; x < n; x++)
                {
                    float s = (x + 0.5f) / n;
                    int p = (y * n + x) * 4;
                    px[p] = Byte(0.5f + 0.7f * Fbm(s, t, 4, 6, 201));
                    px[p + 1] = Byte(0.5f + 0.7f * Fbm(s, t, 8, 5, 202));
                    px[p + 2] = Byte(0.5f + 0.7f * Fbm(s, t, 2, 6, 203));
                    px[p + 3] = Byte(0.5f + 0.7f * Fbm(s, t, 16, 4, 204));
                }
            });
            return px;
        }

        static float LinearToSrgb(float c) =>
            c <= 0.0031308f ? 12.92f * c : 1.055f * MathF.Pow(c, 1f / 2.4f) - 0.055f;

        static byte Byte(float v) => (byte)MathF.Round(Saturate(v) * 255f);

        /// <summary>Encodes texture rows (bottom first) as PNG, upright.</summary>
        public static byte[] Png(byte[] rows, int w, int h, int channels)
        {
            var t = new Texture2D(w, h, channels == 3 ? TextureFormat.RGB24 : TextureFormat.RGBA32, false, true);
            try
            {
                t.SetPixelData(rows, 0);
                return t.EncodeToPNG();
            }
            finally { UnityEngine.Object.DestroyImmediate(t); }
        }

        /// <summary>Generates the library, writes the files, imports them and sets World.mat's textures.</summary>
        public static void Write()
        {
            var o = Generate();
            Directory.CreateDirectory(Dir);
            var files = new (string name, byte[] png)[]
            {
                (Albedo, Png(o.Albedo, o.Width, o.Height, 3)),
                (Normal, Png(o.Normal, o.Width, o.Height, 4)),
                (Mask, Png(o.Mask, o.Width, o.Height, 4)),
                (Macro, Png(o.Macro, o.MacroSize, o.MacroSize, 4)),
            };
            foreach (var (name, png) in files) File.WriteAllBytes(Path.Combine(Dir, name), png);
            File.WriteAllText(Path.Combine(Dir, "MANIFEST.json"), Manifest(o, files));
            MipmapGroup();
            foreach (var (name, _) in files) AssetDatabase.ImportAsset($"{Dir}/{name}", ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset($"{Dir}/MANIFEST.json", ImportAssetOptions.ForceUpdate);
            LinkMaterials();
        }

        /// <summary>Hands the imported textures to World.mat, WorldFacades.mat and WorldNear.mat, and the macro texture to
        /// Sky.mat as its cloud noise (D-03), creating Sky.mat if it is missing.</summary>
        public static void LinkMaterials()
        {
            foreach (var path in new[] { WorldMaterialPath, FacadeMaterialPath, NearMaterialPath })
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) throw new InvalidOperationException($"{path} missing");
                mat.SetTexture("_WorldAlbedo", Load<Texture2DArray>(Albedo));
                mat.SetTexture("_WorldNormal", Load<Texture2DArray>(Normal));
                mat.SetTexture("_WorldMask", Load<Texture2DArray>(Mask));
                mat.SetTexture("_WorldMacro", Load<Texture2D>(Macro));
                EditorUtility.SetDirty(mat);
            }

            var skyShader = Shader.Find("Dad/Sky");
            if (skyShader == null) throw new InvalidOperationException("Dad/Sky shader not found");
            var sky = AssetDatabase.LoadAssetAtPath<Material>(SkyMaterialPath);
            if (sky == null) { sky = new Material(skyShader); AssetDatabase.CreateAsset(sky, SkyMaterialPath); }
            sky.shader = skyShader;
            sky.SetTexture("_CloudNoise", Load<Texture2D>(Macro));
            EditorUtility.SetDirty(sky);
            AssetDatabase.SaveAssets();
        }

        static T Load<T>(string name) where T : Texture
        {
            var t = AssetDatabase.LoadAssetAtPath<T>($"{Dir}/{name}");
            if (t == null) throw new InvalidOperationException($"{Dir}/{name} did not import as {typeof(T).Name}");
            return t;
        }

        static string Manifest(Output o, (string name, byte[] png)[] files)
        {
            var c = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.Append("{\n  \"generator\": \"Dad.EditorTools.Materials.MaterialLibrary (D-02)\",\n");
            sb.Append($"  \"layerSize\": {LayerSize},\n  \"flipbook\": \"{Columns} x {Rows}, layer k at column k % {Columns}, row k / {Columns} from the top\",\n");
            sb.Append("  \"layers\": [\n");
            for (int k = 0; k < o.Stats.Length; k++)
            {
                var s = o.Stats[k];
                string scan = LayerRecipes.All[k].Scan;
                sb.Append(string.Format(c, "    {{ \"index\": {0}, \"name\": \"{1}\", \"periodM\": {2}, \"meanSrgb\": \"#{3:x2}{4:x2}{5:x2}\", \"meanSmoothness\": {6:F3}, \"meanMetallic\": {7:F3}, \"heightMm\": [{8:F2}, {9:F2}], \"source\": {10} }}{11}\n",
                    k, s.Layer, s.Period, Byte(LinearToSrgb(s.R)), Byte(LinearToSrgb(s.G)), Byte(LinearToSrgb(s.B)), s.Smooth, s.Metal, s.HMin * 1000, s.HMax * 1000,
                    scan == null ? "\"procedural (LayerRecipes)\"" : $"\"https://polyhaven.com/a/{scan} (CC0)\"", k + 1 < o.Stats.Length ? "," : ""));
            }
            sb.Append("  ],\n  \"sha256\": {\n");
            for (int i = 0; i < files.Length; i++)
                sb.Append($"    \"{files[i].name}\": \"{Sha256(files[i].png)}\"{(i + 1 < files.Length ? "," : "")}\n");
            sb.Append("  }\n}\n");
            return sb.ToString();
        }

        public static string Sha256(byte[] bytes)
        {
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(bytes);
            var sb = new StringBuilder(64);
            foreach (var b in hash) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        [MenuItem("Dad/Generate world materials")]
        static void Menu() => Write();

        /// <summary>Batch mode: -executeMethod Dad.EditorTools.Materials.MaterialLibrary.LinkBatch (materials only, no regeneration)</summary>
        public static void LinkBatch()
        {
            try
            {
                LinkMaterials();
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        /// <summary>Batch mode: -executeMethod Dad.EditorTools.Materials.MaterialLibrary.GenerateBatch</summary>
        public static void GenerateBatch()
        {
            try
            {
                var clock = System.Diagnostics.Stopwatch.StartNew();
                Write();
                Debug.Log($"[Dad] world materials generated in {clock.Elapsed.TotalSeconds:F1} s");
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }
    }

    /// <summary>Import settings for the generated world textures: the flipbooks become 16-slice
    /// arrays; all are mipmapped (box filter, so the last mip is the layer's mean), trilinear,
    /// anisotropic, and BC7 on desktop.</summary>
    sealed class WorldTextureImport : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(MaterialLibrary.Dir + "/", StringComparison.Ordinal)) return;
            var ti = (TextureImporter)assetImporter;
            string file = Path.GetFileName(assetPath);
            bool array = file != MaterialLibrary.Macro;
            bool srgb = file == MaterialLibrary.Albedo;
            ti.textureType = TextureImporterType.Default;
            var s = new TextureImporterSettings();
            ti.ReadTextureSettings(s);
            s.textureShape = array ? TextureImporterShape.Texture2DArray : TextureImporterShape.Texture2D;
            s.flipbookColumns = array ? MaterialLibrary.Columns : 1;
            s.flipbookRows = array ? MaterialLibrary.Rows : 1;
            s.sRGBTexture = srgb;
            s.mipmapEnabled = true;
            s.mipmapFilter = TextureImporterMipFilter.BoxFilter;
            s.wrapMode = TextureWrapMode.Repeat;
            s.filterMode = FilterMode.Trilinear;
            s.aniso = 8;
            s.alphaSource = srgb ? TextureImporterAlphaSource.None : TextureImporterAlphaSource.FromInput;
            s.alphaIsTransparency = false;
            s.readable = false;
            s.npotScale = TextureImporterNPOTScale.None;
            ti.SetTextureSettings(s);
            ti.streamingMipmaps = false;
            ti.mipmapLimitGroupName = array ? MaterialLibrary.MipGroup : "";
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
            int max = array ? MaterialLibrary.LayerSize * MaterialLibrary.Columns : MaterialLibrary.MacroSize;
            ti.maxTextureSize = max;
            ti.SetPlatformTextureSettings(new TextureImporterPlatformSettings
            {
                name = "Standalone", overridden = true, maxTextureSize = max,
                format = TextureImporterFormat.BC7, textureCompression = TextureImporterCompression.CompressedHQ, compressionQuality = 100,
            });
        }
    }
}
