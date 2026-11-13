// The material sheet (D-02 exit evidence): every WorldMaterial drawn by Dad/World under the Boot
// scene's sun, sky and fog, as a 4 x 4 m floor and a 4 x 2.6 m wall seen from 4.7 m, each tile
// labelled. Vertex colours are the layers' own means (MANIFEST.json), so the sheet shows the
// layers as generated, plus each material's wear.
//   Dad.EditorTools.Materials.MaterialSheet.RenderBatch   (batch mode: -executeMethod)
// Writes Docs/ART_DIRECTION/MATERIAL_SHEET.png. Opens Boot without saving it.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Dad.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Dad.EditorTools.Materials
{
    public static class MaterialSheet
    {
        public const string Output = "Docs/ART_DIRECTION/MATERIAL_SHEET.png";
        const int Columns = 6, TileW = 400, TileH = 300;
        static readonly Vector3 Origin = new Vector3(0, 0, 0);

        public static void RenderBatch()
        {
            try
            {
                Render(Output);
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        public static void Render(string path)
        {
            EditorSceneManager.OpenScene(ProjectTools.BootScene, OpenSceneMode.Single);
            foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                if (root.GetComponent<Light>() == null) root.SetActive(false);
            Shader.SetGlobalVector("_DadWorldWrap", Vector4.zero);
            Shader.SetGlobalFloat("_DadWorldDebug", 0);

            var world = AssetDatabase.LoadAssetAtPath<Material>(MaterialLibrary.WorldMaterialPath);
            var means = LayerMeans();
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var ids = (WorldMaterial[])Enum.GetValues(typeof(WorldMaterial));
            int rows = (ids.Length + Columns - 1) / Columns;
            var sheet = new Texture2D(Columns * TileW, rows * TileH, TextureFormat.RGBA32, false);
            var rt = new RenderTexture(TileW, TileH, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
            var made = new List<UnityEngine.Object>();
            var cam = new GameObject("Sheet camera").AddComponent<Camera>();
            made.Add(cam.gameObject);
            cam.fieldOfView = 50;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.12f, 0.12f, 0.13f);
            cam.targetTexture = rt;
            try
            {
                for (int k = 0; k < ids.Length; k++)
                {
                    // Swatches stand 100 m apart so no shadow or SSAO reaches the next one.
                    var at = Origin + new Vector3(100 * k, 0, 0);
                    var swatch = Swatch(ids[k], means[(int)WorldMaterials.LayerOf(ids[k])], world, at);
                    made.Add(swatch);
                    var label = new GameObject("Label").AddComponent<TextMesh>();
                    made.Add(label.gameObject);
                    label.font = font;
                    label.GetComponent<MeshRenderer>().sharedMaterial = font.material;
                    label.text = ids[k].ToString();
                    label.fontSize = 64;
                    label.characterSize = 0.03f;
                    label.color = Color.white;
                    label.anchor = TextAnchor.UpperLeft;
                    cam.transform.position = at + new Vector3(0, 2.2f, -4.2f);
                    cam.transform.LookAt(at + new Vector3(0, 0.9f, 0.6f));
                    label.transform.SetPositionAndRotation(cam.transform.TransformPoint(new Vector3(-0.9f, 0.62f, 2f)), cam.transform.rotation);
                    cam.Render();
                    RenderTexture.active = rt;
                    sheet.ReadPixels(new Rect(0, 0, TileW, TileH), (k % Columns) * TileW, (rows - 1 - k / Columns) * TileH);
                    RenderTexture.active = null;
                    swatch.SetActive(false);
                    label.gameObject.SetActive(false);
                }
                sheet.Apply();
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(path, sheet.EncodeToPNG());
                Debug.Log($"[Dad] material sheet: {ids.Length} materials, {sheet.width}x{sheet.height}, {path}");
            }
            finally
            {
                cam.targetTexture = null;
                rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);
                UnityEngine.Object.DestroyImmediate(sheet);
                foreach (var o in made) if (o != null) UnityEngine.Object.DestroyImmediate(o);
            }
        }

        // A floor (4 x 4 m) and a wall behind it (4 x 2.6 m, facing the camera), as closed boxes
        // 10 cm thick, coloured with the layer mean and carrying the material id in alpha.
        static GameObject Swatch(WorldMaterial id, Color32 mean, Material world, Vector3 at)
        {
            var go = new GameObject($"Swatch {id}");
            go.transform.position = at;
            var verts = new List<Vector3>();
            var tris = new List<int>();
            Box(verts, tris, new Vector3(-2, -0.1f, -2), new Vector3(2, 0, 2));
            Box(verts, tris, new Vector3(-2, 0, 2), new Vector3(2, 2.6f, 2.1f));
            var colours = new Color32[verts.Count];
            for (int i = 0; i < colours.Length; i++) colours[i] = new Color32(mean.r, mean.g, mean.b, (byte)id);
            var mesh = new Mesh { name = go.name };
            mesh.SetVertices(verts);
            mesh.SetColors(colours);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = world;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            return go;
        }

        static void Box(List<Vector3> v, List<int> t, Vector3 a, Vector3 b)
        {
            // Six faces, outward, each its own four vertices (flat shading needs none shared anyway).
            void Face(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
            {
                int i = v.Count;
                v.Add(p0); v.Add(p1); v.Add(p2); v.Add(p3);
                t.Add(i); t.Add(i + 1); t.Add(i + 2); t.Add(i); t.Add(i + 2); t.Add(i + 3);
            }
            Face(new Vector3(a.x, b.y, a.z), new Vector3(a.x, b.y, b.z), new Vector3(b.x, b.y, b.z), new Vector3(b.x, b.y, a.z)); // top
            Face(new Vector3(a.x, a.y, a.z), new Vector3(b.x, a.y, a.z), new Vector3(b.x, a.y, b.z), new Vector3(a.x, a.y, b.z)); // bottom
            Face(new Vector3(a.x, a.y, a.z), new Vector3(a.x, b.y, a.z), new Vector3(b.x, b.y, a.z), new Vector3(b.x, a.y, a.z)); // -z
            Face(new Vector3(b.x, a.y, b.z), new Vector3(b.x, b.y, b.z), new Vector3(a.x, b.y, b.z), new Vector3(a.x, a.y, b.z)); // +z
            Face(new Vector3(a.x, a.y, b.z), new Vector3(a.x, b.y, b.z), new Vector3(a.x, b.y, a.z), new Vector3(a.x, a.y, a.z)); // -x
            Face(new Vector3(b.x, a.y, a.z), new Vector3(b.x, b.y, a.z), new Vector3(b.x, b.y, b.z), new Vector3(b.x, a.y, b.z)); // +x
        }

        /// <summary>Each layer's mean sRGB colour, from MANIFEST.json.</summary>
        public static Color32[] LayerMeans()
        {
            var text = File.ReadAllText(Path.Combine(MaterialLibrary.Dir, "MANIFEST.json"));
            var means = new Color32[WorldMaterials.LayerCount];
            foreach (Match m in Regex.Matches(text, "\"index\": (\\d+),[^}]*\"meanSrgb\": \"#([0-9a-f]{6})\""))
            {
                uint rgb = Convert.ToUInt32(m.Groups[2].Value, 16);
                means[int.Parse(m.Groups[1].Value)] = new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);
            }
            return means;
        }
    }
}
