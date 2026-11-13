// The water's surface: one mesh per chunk that has water, over the flat ground layer, which stays underneath as its depth. World/water/<id>.bin holds, at the terrain's 10 m points,
// each point's signed distance from the shore and whether that shore is paved (Tools/worldgen/harbour.py). Open water far from any shore is drawn in 100 m quads, so a chunk of sea costs 50 triangles.
// Pictures are the owner's small_flat_cube_of_water; the movement is in Dad/Water. Nothing here touches collision.
using System.Collections.Generic;
using System.IO;
using Dad.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace Dad.Core
{
    public class WaterView : MonoBehaviour
    {
        public const int Lattice = 51, Block = 10;
        public const float Step = 10, Height = 0.14f;
        /// <summary>A point counts as open water from this far out, in the file's quarter metres above -32.</summary>
        const int Open = 255;

        /// <summary>The view in play: boats ask it where the shore is.</summary>
        public static WaterView Active { get; private set; }

        public ChunkStreamer Streamer;
        readonly Dictionary<ChunkCell, byte[]> lattice = new Dictionary<ChunkCell, byte[]>();
        readonly Dictionary<ChunkCell, Mesh> meshes = new Dictionary<ChunkCell, Mesh>();
        Material material;
        Vector3[] points;

        public static WaterView Attach(GameObject host, ChunkStreamer streamer)
        {
            var v = host.AddComponent<WaterView>();
            v.Streamer = streamer;
            return v;
        }

        void Start()
        {
            Active = this;
            if (Streamer == null) return;
            Streamer.ChunkIn += Add;
            Streamer.ChunkOut += Drop;
            foreach (var c in Streamer.Resident.Keys) Add(c);
        }

        void OnDestroy()
        {
            if (Active == this) Active = null;
            if (Streamer != null)
            {
                Streamer.ChunkIn -= Add;
                Streamer.ChunkOut -= Drop;
            }
            foreach (var m in meshes.Values) Destroy(m);
            if (material != null) Destroy(material);
        }

        /// <summary>Metres from the shore here, positive on the water. Land with no water near has no file: it reads as far inland.</summary>
        public double Distance(double east, double north)
        {
            var cell = ChunkCell.Of(east, north);
            if (!lattice.TryGetValue(cell, out var b)) return -32;
            double x = (east - cell.CornerEast) / Step, z = (north - cell.CornerNorth) / Step;
            int i = System.Math.Clamp((int)x, 0, Lattice - 2), j = System.Math.Clamp((int)z, 0, Lattice - 2);
            double u = x - i, w = z - j;
            double At(int a, int c) => b[2 * (a + c * Lattice)] / 4.0 - 32;
            return (At(i, j) * (1 - u) + At(i + 1, j) * u) * (1 - w) + (At(i, j + 1) * (1 - u) + At(i + 1, j + 1) * u) * w;
        }

        /// <summary>How far the shore is and which way is out to open water, for a boat. False where nothing is known of the place.</summary>
        public bool Shore(double east, double north, out double distance, out double outX, out double outZ)
        {
            outX = outZ = 0;
            distance = Distance(east, north);
            if (!lattice.ContainsKey(ChunkCell.Of(east, north))) return false;
            double gx = Distance(east + Step, north) - Distance(east - Step, north), gz = Distance(east, north + Step) - Distance(east, north - Step);
            double l = System.Math.Sqrt(gx * gx + gz * gz);
            if (l < 1e-6) return true;
            outX = gx / l;
            outZ = gz / l;
            return true;
        }

        /// <summary>The nearest dry point within reach of this one, world metres, for someone stepping ashore.</summary>
        public bool Landing(double east, double north, double reach, out double landEast, out double landNorth)
        {
            landEast = east;
            landNorth = north;
            double best = double.MaxValue;
            for (double r = 2; r <= reach; r += 1)
                for (int k = 0; k < 16; k++)
                {
                    double t = k * System.Math.PI / 8, x = east + r * System.Math.Cos(t), z = north + r * System.Math.Sin(t);
                    if (Distance(x, z) < -0.8 && r < best) { best = r; landEast = x; landNorth = z; }
                }
            return best < double.MaxValue;
        }

        void Prepare()
        {
            const int N = Lattice;
            points = new Vector3[N * N];
            for (int j = 0; j < N; j++)
                for (int i = 0; i < N; i++) points[i + j * N] = new Vector3(i * Step, Height, j * Step);
            material = new Material(Resources.Load<Material>("Materials/Water"));
            var sea = Resources.Load<Texture2D>("City/Tex/water_sea");
            var noise = Resources.Load<Texture2D>("City/Tex/water_noise");
            if (sea == null || noise == null) Debug.LogError("[Dad] missing water pictures");
            else
            {
                sea.wrapMode = TextureWrapMode.Mirror; // A photograph, not a tile: mirrored, its edges meet.
                material.SetTexture("_Sea", sea);
                material.SetTexture("_Noise", noise);
            }
        }

        /// <summary>The triangles of one chunk: full 10 m cells only where a shore is near, and a single quad for each 100 m block of open water.</summary>
        public static int[] Triangles(byte[] bytes)
        {
            const int N = Lattice;
            var t = new List<int>();
            void Quad(int a, int b, int c, int d) { t.Add(a); t.Add(b); t.Add(c); t.Add(c); t.Add(b); t.Add(d); }
            byte At(int i, int j) => bytes[2 * (i + j * N)];
            for (int bj = 0; bj < N - 1; bj += Block)
                for (int bi = 0; bi < N - 1; bi += Block)
                {
                    bool open = true;
                    for (int j = bj; j <= bj + Block && open; j++)
                        for (int i = bi; i <= bi + Block; i++)
                            if (At(i, j) != Open) { open = false; break; }
                    if (open)
                    {
                        Quad(bi + bj * N, bi + (bj + Block) * N, bi + Block + bj * N, bi + Block + (bj + Block) * N);
                        continue;
                    }
                    for (int j = bj; j < bj + Block; j++)
                        for (int i = bi; i < bi + Block; i++)
                        {
                            int most = Mathf.Max(Mathf.Max(At(i, j), At(i + 1, j)), Mathf.Max(At(i, j + 1), At(i + 1, j + 1)));
                            if (most < 80) continue; // All of it more than 12 m inland: nothing shows.
                            Quad(i + j * N, i + (j + 1) * N, i + 1 + j * N, i + 1 + (j + 1) * N);
                        }
                }
            return t.ToArray();
        }

        void Add(ChunkCell cell)
        {
            if (meshes.ContainsKey(cell) || !Streamer.Resident.TryGetValue(cell, out var view)) return;
            var path = Path.Combine(GameRoot.WorldDir, "water", cell.Id + ".bin");
            if (!File.Exists(path)) return;
            var bytes = File.ReadAllBytes(path);
            const int N = Lattice;
            if (bytes.Length != N * N * 2) { Debug.LogError("[Dad] bad water file " + path); return; }
            if (points == null) Prepare();
            lattice[cell] = bytes;
            var colours = new Color32[N * N];
            var uv = new Vector2[N * N];
            float east = (float)cell.CornerEast, north = (float)cell.CornerNorth;
            for (int k = 0; k < colours.Length; k++)
            {
                colours[k] = new Color32(bytes[2 * k], bytes[2 * k + 1], 0, 255);
                uv[k] = new Vector2(east + points[k].x, north + points[k].z);
            }
            var mesh = new Mesh { name = "Water " + cell.Id };
            mesh.SetVertices(points);
            mesh.SetColors(colours);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(Triangles(bytes), 0, false);
            mesh.bounds = new Bounds(new Vector3(250, 0, 250), new Vector3(500, 1, 500));
            mesh.UploadMeshData(true);
            var go = new GameObject("Water") { layer = WorldLayers.World };
            go.transform.SetParent(view.Root.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = true;
            meshes[cell] = mesh;
        }

        void Drop(ChunkCell cell)
        {
            if (!meshes.TryGetValue(cell, out var mesh)) return;
            Destroy(mesh);
            meshes.Remove(cell);
            lattice.Remove(cell);
        }
    }
}
