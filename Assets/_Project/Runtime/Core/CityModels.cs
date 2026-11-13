// The owner's models as the game draws them: Resources/City/models.bytes, written by Tools/models/city.py. Read once, the first time a chunk asks. Each model has a near and a far mesh; each mesh
// one submesh per picture, drawn with one shared material per picture. Colour-only parts use white with their colour in the vertices. Nothing here is instanced yet: CityView does that.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

namespace Dad.Core
{
    public sealed class CityModel
    {
        public string Name;
        public Vector3 Size;
        public Mesh[] Lods;
        public Material[][] Materials;
        public int Index;
        /// <summary>Windows light at night on this one.</summary>
        public bool Windows;
        /// <summary>Grass: drawn close by only, with no shadow, through its pictures' alpha.</summary>
        public bool Grass;
    }

    public static class CityModels
    {
        /// <summary>The owner's pictures loaded so far, by name (for the bench's texture census, K-63).</summary>
        public static readonly HashSet<string> Pictures = new HashSet<string>();

        public const string Asset = "City/models";
        static Dictionary<string, CityModel> byName;
        static readonly List<CityModel> list = new List<CityModel>();
        public static IReadOnlyList<CityModel> All { get { Load(); return list; } }
        public static string Owner { get; private set; }

        public static CityModel Get(string name)
        {
            Load();
            return name != null && byName.TryGetValue(name, out var m) ? m : null;
        }

        static void Load()
        {
            if (byName != null) return;
            byName = new Dictionary<string, CityModel>();
            list.Clear();
            var art = Resources.Load<TextAsset>(Asset);
            if (art == null)
            {
                Debug.LogError("[Dad] no " + Asset + ": run Tools/models/city.py");
                return;
            }
            var bases = Resources.Load<Material>("Materials/City");
            var cut = Resources.Load<Material>("Materials/CityCut");
            var shared = new Dictionary<string, Material>();
            Material For(string picture)
            {
                if (shared.TryGetValue(picture, out var m)) return m;
                m = new Material(picture.StartsWith("grass_", StringComparison.Ordinal) || picture.StartsWith("leaf_", StringComparison.Ordinal) ? cut : bases) { enableInstancing = true, name = "City " + picture };
                if (picture.Length > 0)
                {
                    var t = Resources.Load<Texture2D>("City/Tex/" + picture);
                    Pictures.Add(picture);
                    if (t == null) Debug.LogError("[Dad] missing city picture " + picture);
                    else m.SetTexture("_BaseMap", t);
                }
                return shared[picture] = m;
            }
            using (var r = new BinaryReader(new MemoryStream(art.bytes)))
            {
                if (Encoding.ASCII.GetString(r.ReadBytes(4)) != "DADM") throw new InvalidDataException("not a city model file");
                Owner = Text(r);
                int n = r.ReadInt32();
                for (int i = 0; i < n; i++)
                {
                    var m = new CityModel { Name = Text(r), Size = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle()), Index = i };
                    m.Grass = m.Name.StartsWith("grass");
                    m.Windows = !(m.Grass || m.Name.StartsWith("tree") || m.Name == "tower_night" || m.Name.StartsWith("airport") || m.Name.StartsWith("jet") || m.Name.StartsWith("container") || m.Name.StartsWith("metro") || m.Name.StartsWith("vessel") || m.Name.StartsWith("trailer") || m.Name.StartsWith("lamp")
                        || m.Name.StartsWith("barrier") || m.Name.StartsWith("rail") || m.Name == "watertank" || m.Name == "generator" || m.Name == "warehouse" || m.Name == "workshop" || m.Name.StartsWith("rail"));
                    int lods = r.ReadInt32();
                    m.Lods = new Mesh[lods];
                    m.Materials = new Material[lods][];
                    for (int l = 0; l < lods; l++)
                    {
                        int vertices = r.ReadInt32();
                        var pos = new Vector3[vertices];
                        var nor = new Vector3[vertices];
                        var uv = new Vector2[vertices];
                        var col = new Color32[vertices];
                        for (int v = 0; v < vertices; v++)
                        {
                            pos[v] = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                            nor[v] = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                            uv[v] = new Vector2(r.ReadSingle(), r.ReadSingle());
                        }
                        for (int v = 0; v < vertices; v++) col[v] = new Color32(r.ReadByte(), r.ReadByte(), r.ReadByte(), r.ReadByte());
                        var mesh = new Mesh { name = m.Name + " lod " + l, indexFormat = vertices > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                        mesh.SetVertices(pos);
                        mesh.SetNormals(nor);
                        mesh.SetUVs(0, uv);
                        mesh.SetColors(col);
                        int subs = r.ReadInt32();
                        mesh.subMeshCount = subs;
                        m.Materials[l] = new Material[subs];
                        for (int s = 0; s < subs; s++)
                        {
                            m.Materials[l][s] = For(Text(r));
                            int count = r.ReadInt32();
                            var ids = new int[count];
                            for (int k = 0; k < count; k++) ids[k] = r.ReadInt32();
                            mesh.SetTriangles(ids, s, false);
                        }
                        mesh.RecalculateBounds();
                        mesh.UploadMeshData(true);
                        m.Lods[l] = mesh;
                    }
                    byName[m.Name] = m;
                    list.Add(m);
                }
            }
        }

        static string Text(BinaryReader r) => Encoding.UTF8.GetString(r.ReadBytes(r.ReadInt32()));
    }
}
