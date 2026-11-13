// Downloaded buildings into Resources/Buildings (owner 2026-11-11, Docs/PRODUCTION/BUILDING_ASSETS_AUDIT.md section 7). Each comes in as its creator made it: the model and its
// colour and normal maps are copied unchanged. Only two things are made from the creator's maps, because URP Lit reads them that way: one mask per material (R metallic, G occlusion,
// A smoothness = 1 - roughness), and, where a material has an opacity map, its colour map with that opacity as alpha. Textures keep their full size (4096 at most here), compressed,
// in the "World" mip group (High full size, Medium and Low one mip down). Sources: DAD_BUILDINGS, else ~/Downloads/3D assets/buildings-new (see MANIFEST.md there).
// Batch: Unity -batchmode -quit -projectPath . -executeMethod Dad.EditorTools.BuildingImport.OldResidential
using System;
using System.IO;
using System.Linq;
using Dad.EditorTools.Materials;
using UnityEditor;
using UnityEngine;

namespace Dad.EditorTools
{
    public sealed class BuildingImport : AssetPostprocessor
    {
        public const string Root = "Assets/_Project/Resources/Buildings/";

        static string Sources => Environment.GetEnvironmentVariable("DAD_BUILDINGS")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "3D assets", "buildings-new");

        [MenuItem("Dad/Buildings/Import Old Residential")]
        public static void OldResidential() =>
            Import("old_residential", Path.Combine(Sources, "old-residential-building-4k", "extracted", "source", "old_residential_building"), "old_residential_building.fbx");

        static void Import(string name, string src, string fbx)
        {
            string dir = Root + name + "/", tex = Path.Combine(src, "textures");
            Directory.CreateDirectory(dir);
            File.Copy(Path.Combine(src, fbx), dir + fbx, true);
            string[] mats = Directory.GetFiles(tex, "*_albedo.*").Select(p => Path.GetFileName(p)).Select(f => f.Substring(0, f.IndexOf("_albedo", StringComparison.Ordinal))).ToArray();
            foreach (var m in mats)
            {
                string Find(string map) => Directory.GetFiles(tex, m + "_" + map + ".*").Single();
                string Copy(string from, string to) { File.Copy(from, to + Path.GetExtension(from), true); return to + Path.GetExtension(from); }
                var opacity = Directory.GetFiles(tex, m + "_opacity.*").FirstOrDefault();
                if (opacity == null) Copy(Find("albedo"), dir + m + "_albedo");
                else Pack(dir + m + "_albedo.png", (a, o, _, _) => new Color32(a.r, a.g, a.b, o.r), Find("albedo"), opacity);
                Copy(Find("normal"), dir + m + "_normal");
                Pack(dir + m + "_mask.png", (mt, ao, r, _) => new Color32(mt.r, ao.r, 0, (byte)(255 - r.r)), Find("metallic"), Find("AO"), Find("roughness"));
            }
            AssetDatabase.Refresh();

            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var importer = (ModelImporter)AssetImporter.GetAtPath(dir + fbx);
            foreach (var m in mats)
            {
                T Load<T>(string file) where T : UnityEngine.Object => AssetDatabase.LoadAssetAtPath<T>(Directory.GetFiles(dir, file + ".*").First(f => !f.EndsWith(".meta", StringComparison.Ordinal)));
                var mat = new Material(lit) { name = m, enableInstancing = true };
                mat.SetTexture("_BaseMap", Load<Texture2D>(m + "_albedo"));
                mat.SetTexture("_BumpMap", Load<Texture2D>(m + "_normal"));
                mat.EnableKeyword("_NORMALMAP");
                var mask = Load<Texture2D>(m + "_mask");
                mat.SetTexture("_MetallicGlossMap", mask);
                mat.EnableKeyword("_METALLICSPECGLOSSMAP");
                mat.SetFloat("_Smoothness", 1);
                mat.SetTexture("_OcclusionMap", mask);
                mat.EnableKeyword("_OCCLUSIONMAP");
                if (File.Exists(dir + m + "_albedo.png") && Directory.GetFiles(tex, m + "_opacity.*").Length > 0) Transparent(mat);
                AssetDatabase.CreateAsset(mat, dir + m + ".mat");
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), m), mat);
            }
            importer.SaveAndReimport();

            // The prefab stands on its ground with its middle on the place it is put, as the city's models do.
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(dir + fbx);
            var root = new GameObject(name);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
            inst.transform.SetParent(root.transform, false);
            var rs = root.GetComponentsInChildren<Renderer>();
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            inst.transform.localPosition = new Vector3(-b.center.x, -b.min.y, -b.center.z);
            PrefabUtility.SaveAsPrefabAsset(root, dir + name + ".prefab");
            UnityEngine.Object.DestroyImmediate(root);
            long tris = 0, verts = 0;
            foreach (var f in model.GetComponentsInChildren<MeshFilter>()) { tris += f.sharedMesh.triangles.LongLength / 3; verts += f.sharedMesh.vertexCount; }
            Debug.Log($"[Dad] building {name}: size {b.size.x:F2} x {b.size.y:F2} x {b.size.z:F2} m, {rs.Length} renderers, {tris} triangles, {verts} vertices, {mats.Length} materials ({string.Join(", ", mats)})");
        }

        static void Transparent(Material m)
        {
            m.SetFloat("_Surface", 1);
            m.SetFloat("_Blend", 0);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        // Pixels from up to three of the creator's maps (same size) into one PNG.
        static void Pack(string to, Func<Color32, Color32, Color32, Color32, Color32> f, params string[] from)
        {
            var px = new Color32[from.Length][];
            int w = 0, h = 0;
            for (int i = 0; i < from.Length; i++)
            {
                var t = new Texture2D(2, 2);
                t.LoadImage(File.ReadAllBytes(from[i]));
                if (i > 0 && (t.width != w || t.height != h)) throw new InvalidDataException($"{from[i]} is {t.width}x{t.height}, not {w}x{h}");
                w = t.width; h = t.height;
                px[i] = t.GetPixels32();
                UnityEngine.Object.DestroyImmediate(t);
            }
            var o = new Color32[w * h];
            var none = new Color32();
            for (int k = 0; k < o.Length; k++) o[k] = f(px[0][k], from.Length > 1 ? px[1][k] : none, from.Length > 2 ? px[2][k] : none, none);
            var outT = new Texture2D(w, h, TextureFormat.RGBA32, false);
            outT.SetPixels32(o);
            File.WriteAllBytes(to, outT.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(outT);
        }

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Root, StringComparison.Ordinal)) return;
            var t = (TextureImporter)assetImporter;
            string n = Path.GetFileNameWithoutExtension(assetPath);
            t.textureType = n.EndsWith("_normal", StringComparison.Ordinal) ? TextureImporterType.NormalMap : TextureImporterType.Default;
            t.sRGBTexture = n.EndsWith("_albedo", StringComparison.Ordinal);
            t.alphaSource = n.EndsWith("_albedo", StringComparison.Ordinal) && assetPath.EndsWith(".jpg", StringComparison.Ordinal) ? TextureImporterAlphaSource.None : TextureImporterAlphaSource.FromInput;
            t.alphaIsTransparency = n.EndsWith("_albedo", StringComparison.Ordinal) && assetPath.EndsWith(".png", StringComparison.Ordinal);
            t.mipmapEnabled = true;
            t.mipmapLimitGroupName = MaterialLibrary.MipGroup;
            t.maxTextureSize = 4096;
            t.textureCompression = TextureImporterCompression.CompressedHQ;
        }

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(Root, StringComparison.Ordinal)) return;
            var m = (ModelImporter)assetImporter;
            m.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            m.meshCompression = ModelImporterMeshCompression.Off; // owner, 2026-11-09: a model comes in with all it has
            m.isReadable = false;
            m.importAnimation = false;
            m.animationType = ModelImporterAnimationType.None;
            m.importCameras = false;
            m.importLights = false;
            m.importVisibility = false;
            m.addCollider = false; // its footprint's collider stays (TrialBuilding)
        }
    }
}
