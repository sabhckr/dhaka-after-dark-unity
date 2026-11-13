// Draws the city's models (brief of 2026-11-06). A chunk's Placed records say which model stands where; here they become instances, in 125 m cells so that a cell behind the camera costs one test.
// Nothing is a GameObject: each frame the cells in view and in range add their instances to one batch per model and level of detail, and each batch is one Graphics.RenderMeshInstanced a picture.
// That is GPU instancing, frustum culling, distance culling and level of detail in one pass, with the tint and mirror per instance in a property block. Only resident chunks have instances: the
// world is never held at once, apart from World/fixed.txt, the few things too large for one chunk (the airport and its aircraft), which stay in place while the far side of them is out of range.
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Dad.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace Dad.Core
{
    public sealed class CityView : MonoBehaviour
    {
        public const int BatchSize = 256, Cells = 4;
        /// <summary>Grass: full cards to here, every fifth card to GrassFar, then nothing.</summary>
        public const float GrassNear = 28, GrassFar = 75;
        /// <summary>The terrain lattice: points a side, metres between them. Matches Tools/worldgen/gen.py.</summary>
        public const int Lattice = 51;
        public const float LatticeStep = 10, TerrainY = 0.08f;
        const float CellSize = 125f;
        /// <summary>Where a level ends, in metres: near model, then far model, by quality level.</summary>
        public static float NearRange(Tier t) => t == Tier.Low ? 90 : t == Tier.Medium ? 150 : 220;
        public static float FarRange(Tier t) => t == Tier.Low ? 650 : t == Tier.Medium ? 900 : 1150;

        /// <summary>City models not drawn, by their place in centimetres as their chunk stores it: a downloaded building stands there instead (TrialBuilding).</summary>
        public static readonly HashSet<(int, int)> Hidden = new HashSet<(int, int)>();

        public ChunkStreamer Streamer;
        public Transform Focus;
        public int Drawn { get; private set; }
        public int DrawCalls { get; private set; }
        public int Instances { get; private set; }

        struct Item
        {
            public CityModel Model;
            public Matrix4x4 M; // In the chunk's own frame.
            public Vector4 Tint;
            public float Reach;
        }

        sealed class Cell
        {
            public readonly List<Item> Items = new List<Item>();
            public Vector3 Min = new Vector3(1e9f, 1e9f, 1e9f), Max = new Vector3(-1e9f, -1e9f, -1e9f);
        }

        sealed class Batch
        {
            public readonly Matrix4x4[] M = new Matrix4x4[BatchSize];
            public readonly Vector4[] Tint = new Vector4[BatchSize];
            public readonly MaterialPropertyBlock Props = new MaterialPropertyBlock();
            public int Count;
        }

        static readonly int TintId = Shader.PropertyToID("_Tint");
        readonly Dictionary<ChunkCell, Cell[]> chunks = new Dictionary<ChunkCell, Cell[]>();
        readonly List<Item> fixedItems = new List<Item>();
        readonly List<double> fixedAt = new List<double>();
        Batch[] batches;
        readonly Plane[] planes = new Plane[6];
        float near, far, shadowReach;

        public static CityView Active { get; private set; }

        public static CityView Attach(GameObject host, ChunkStreamer streamer, Transform focus)
        {
            var v = Active = host.AddComponent<CityView>();
            v.Streamer = streamer;
            v.Focus = focus;
            return v;
        }

        void Start()
        {
            near = NearRange(GraphicsTier.Current);
            far = FarRange(GraphicsTier.Current);
            shadowReach = 200;
            batches = new Batch[CityModels.All.Count * 2];
            for (int i = 0; i < batches.Length; i++) batches[i] = new Batch();
            if (Streamer == null) return;
            Streamer.ChunkIn += Add;
            Streamer.ChunkOut += Drop;
            foreach (var c in Streamer.Resident.Keys) Add(c);
            ReadFixed();
        }

        void OnDestroy()
        {
            if (Streamer == null) return;
            Streamer.ChunkIn -= Add;
            Streamer.ChunkOut -= Drop;
        }

        /// <summary>A pose in a frame: size and mirror on x, then turn, then place. The turn is degrees clockwise from north, seen from above.</summary>
        static Matrix4x4 Pose(float x, float y, float z, float turn, float size, bool mirrored) =>
            Matrix4x4.TRS(new Vector3(x, y, z), Quaternion.Euler(0, turn, 0), new Vector3(mirrored ? -size : size, size, size));

        static Vector4 Tint(CityModel m, int rgb, bool mirrored)
        {
            float k = 1f / 200f; // A tint of 200 leaves the picture as it is; above that lightens it.
            var c = new Color(((rgb >> 16) & 255) * k, ((rgb >> 8) & 255) * k, (rgb & 255) * k).linear;
            return new Vector4(c.r, c.g, c.b, (mirrored ? -1 : 1) * (m.Windows ? 2 : 1));
        }

        Vector3[] latticePoints;
        int[] latticeTriangles;
        Material terrain;
        readonly Dictionary<ChunkCell, Mesh> terrains = new Dictionary<ChunkCell, Mesh>();

        /// <summary>A chunk's open ground: World/terrain/&lt;id&gt;.bin, four bytes a point (sand, rock, grass, shown). The points are the same in every chunk; only the weights differ.</summary>
        void AddTerrain(ChunkCell cell, ChunkStreamer.ChunkView view)
        {
            var path = Path.Combine(GameRoot.WorldDir, "terrain", cell.Id + ".bin");
            if (!File.Exists(path)) return;
            var bytes = File.ReadAllBytes(path);
            const int N = Lattice;
            if (bytes.Length != N * N * 4) { Debug.LogError("[Dad] bad terrain file " + path); return; }
            if (latticePoints == null)
            {
                latticePoints = new Vector3[N * N];
                latticeTriangles = new int[(N - 1) * (N - 1) * 6];
                int t = 0;
                for (int j = 0; j < N; j++)
                    for (int i = 0; i < N; i++)
                    {
                        latticePoints[i + j * N] = new Vector3(i * LatticeStep, TerrainY, j * LatticeStep);
                        if (i == N - 1 || j == N - 1) continue;
                        int a = i + j * N;
                        latticeTriangles[t++] = a; latticeTriangles[t++] = a + N; latticeTriangles[t++] = a + 1;
                        latticeTriangles[t++] = a + 1; latticeTriangles[t++] = a + N; latticeTriangles[t++] = a + N + 1;
                    }
                terrain = new Material(Resources.Load<Material>("Materials/Terrain"));
                terrain.SetTexture("_Sand", Resources.Load<Texture2D>("City/Tex/terrain_sand"));
                terrain.SetTexture("_Rock", Resources.Load<Texture2D>("City/Tex/terrain_rock"));
                terrain.SetTexture("_Grass", Resources.Load<Texture2D>("City/Tex/terrain_grass"));
            }
            var colours = new Color32[N * N];
            var uv = new Vector2[N * N];
            float east = (float)cell.CornerEast, north = (float)cell.CornerNorth;
            for (int k = 0; k < colours.Length; k++)
            {
                colours[k] = new Color32(bytes[4 * k], bytes[4 * k + 1], bytes[4 * k + 2], bytes[4 * k + 3]);
                uv[k] = new Vector2(east + latticePoints[k].x, north + latticePoints[k].z);
            }
            var mesh = new Mesh { name = "Terrain " + cell.Id };
            mesh.SetVertices(latticePoints);
            mesh.SetColors(colours);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(latticeTriangles, 0, false);
            mesh.bounds = new Bounds(new Vector3(250, 0, 250), new Vector3(500, 1, 500));
            mesh.UploadMeshData(true);
            var go = new GameObject("Terrain") { layer = WorldLayers.World };
            go.transform.SetParent(view.Root.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = terrain;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = true;
            terrains[cell] = mesh;
        }

        void Add(ChunkCell cell)
        {
            if (!Streamer.Resident.TryGetValue(cell, out var view)) return;
            AddTerrain(cell, view);
            if (view.Placed == null || view.Placed.Length == 0) return;
            var cells = new Cell[Cells * Cells];
            double cornerEast = cell.CornerEast, cornerNorth = cell.CornerNorth;
            foreach (var b in view.Placed)
            {
                var m = CityModels.Get(b.Model);
                if (m == null || b.Pose == null || Hidden.Count > 0 && Hidden.Contains((b.Pose[0], b.Pose[1]))) continue;
                var p = b.Pose;
                float x = (float)(p[0] / 100.0 - cornerEast), y = p[2] / 100f, z = (float)(p[1] / 100.0 - cornerNorth), size = p[4] / 1000f;
                bool mirrored = p[5] != 0;
                var item = new Item { Model = m, M = Pose(x, y, z, p[3] / 100f, size, mirrored), Tint = Tint(m, p[6], mirrored), Reach = 0.5f * size * Mathf.Max(m.Size.x, m.Size.z) };
                int k = Mathf.Clamp((int)(x / CellSize), 0, Cells - 1) + Cells * Mathf.Clamp((int)(z / CellSize), 0, Cells - 1);
                var c = cells[k] ??= new Cell();
                c.Items.Add(item);
                float h = size * m.Size.y;
                c.Min = Vector3.Min(c.Min, new Vector3(x - item.Reach, y, z - item.Reach));
                c.Max = Vector3.Max(c.Max, new Vector3(x + item.Reach, y + h, z + item.Reach));
            }
            chunks[cell] = cells;
        }

        void Drop(ChunkCell cell)
        {
            chunks.Remove(cell);
            if (terrains.TryGetValue(cell, out var mesh))
            {
                Destroy(mesh);
                terrains.Remove(cell);
            }
        }

        void ReadFixed()
        {
            var path = Path.Combine(GameRoot.WorldDir, "fixed.txt");
            if (!File.Exists(path)) return;
            var inv = CultureInfo.InvariantCulture;
            foreach (var line in File.ReadAllLines(path))
            {
                var f = line.Split(' ');
                if (f.Length < 8) continue;
                var m = CityModels.Get(f[0]);
                if (m == null) { Debug.LogError("[Dad] no model " + f[0]); continue; }
                bool mirrored = f[6] != "0";
                float size = float.Parse(f[5], inv);
                fixedItems.Add(new Item { Model = m, M = Pose(0, float.Parse(f[3], inv), 0, float.Parse(f[4], inv), size, mirrored), Tint = Tint(m, int.Parse(f[7], inv), mirrored), Reach = 0.5f * size * Mathf.Max(m.Size.x, m.Size.z) });
                fixedAt.Add(double.Parse(f[1], inv));
                fixedAt.Add(double.Parse(f[2], inv));
            }
        }

        void LateUpdate() => Submit();

        /// <summary>Queues this frame's instances. A still picture calls it just before it renders, as Update comes too late for a manual render.</summary>
        public void Submit()
        {
            var cam = Camera.main;
            if (cam == null || batches == null) return;
            GeometryUtility.CalculateFrustumPlanes(cam, planes);
            var eye = cam.transform.position;
            Drawn = DrawCalls = Instances = 0;
            var bounds = new Bounds(eye, Vector3.one * 6000);
            foreach (var kv in chunks)
            {
                if (!Streamer.Resident.TryGetValue(kv.Key, out var view) || view.Root == null) continue;
                var root = view.Root.transform.position;
                var cells = kv.Value;
                for (int i = 0; i < cells.Length; i++)
                {
                    var c = cells[i];
                    if (c == null) continue;
                    Instances += c.Items.Count;
                    var box = new Bounds();
                    box.SetMinMax(c.Min + root, c.Max + root);
                    float d = Mathf.Sqrt(box.SqrDistance(eye));
                    if (d > far) continue;
                    // Things just out of view still throw shadows into it.
                    if (d > shadowReach && !GeometryUtility.TestPlanesAABB(planes, box)) continue;
                    var items = c.Items;
                    for (int k = 0; k < items.Count; k++)
                    {
                        var it = items[k];
                        var m = it.M;
                        m.m03 += root.x;
                        m.m13 += root.y;
                        m.m23 += root.z;
                        float dx = m.m03 - eye.x, dz = m.m23 - eye.z, reach = Mathf.Sqrt(dx * dx + dz * dz) - it.Reach;
                        if (reach > far) continue;
                        if (it.Model.Grass)
                        {
                            if (reach < GrassFar) Put(it.Model, reach > GrassNear ? 1 : 0, m, it.Tint, bounds);
                            continue;
                        }
                        Put(it.Model, reach > near ? 1 : 0, m, it.Tint, bounds);
                    }
                }
            }
            var origin = FloatingOrigin.Active;
            for (int i = 0; i < fixedItems.Count; i++)
            {
                var it = fixedItems[i];
                var m = it.M;
                float x = (float)fixedAt[2 * i], z = (float)fixedAt[2 * i + 1];
                if (origin != null) origin.Offset.ToScene(fixedAt[2 * i], fixedAt[2 * i + 1], out x, out z);
                m.m03 = x;
                m.m23 = z;
                float dx = x - eye.x, dz = z - eye.z, reach = Mathf.Sqrt(dx * dx + dz * dz) - it.Reach;
                if (reach > 2.5f * far) continue;
                Put(it.Model, reach > near ? 1 : 0, m, it.Tint, bounds);
            }
            var all = CityModels.All;
            for (int i = 0; i < all.Count; i++)
                for (int l = 0; l < 2; l++) Flush(all[i], l, bounds);
        }

        void Put(CityModel model, int lod, in Matrix4x4 m, in Vector4 tint, in Bounds bounds)
        {
            var b = batches[model.Index * 2 + lod];
            b.M[b.Count] = m;
            b.Tint[b.Count] = tint;
            if (++b.Count == BatchSize) Flush(model, lod, bounds);
        }

        void Flush(CityModel model, int lod, in Bounds bounds)
        {
            var b = batches[model.Index * 2 + lod];
            if (b.Count == 0) return;
            b.Props.SetVectorArray(TintId, b.Tint);
            var mats = model.Materials[lod];
            for (int s = 0; s < mats.Length; s++)
            {
                var rp = new RenderParams(mats[s])
                {
                    worldBounds = bounds,
                    shadowCastingMode = lod == 0 && !model.Grass ? ShadowCastingMode.On : ShadowCastingMode.Off,
                    receiveShadows = true,
                    matProps = b.Props,
                    layer = WorldLayers.World,
                    lightProbeUsage = LightProbeUsage.BlendProbes,
                };
                Graphics.RenderMeshInstanced(rp, model.Lods[lod], s, b.M, b.Count);
                DrawCalls++;
            }
            Drawn += b.Count;
            b.Count = 0;
        }
    }
}
