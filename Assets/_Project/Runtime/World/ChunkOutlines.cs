// Line geometry for a chunk's building outlines and road centrelines, built off the main
// thread. Vertices are x, y, z floats in metres from the chunk's south-west corner (y = Lift);
// indices are line pairs, buildings first, then roads. This is the C-01 debug view that proves
// streamed data lands in the right place; real meshes come from the C-02..C-04 builders.
using System.Collections.Generic;

namespace Dad.World
{
    public sealed class ChunkOutlines
    {
        public const float Lift = 0.3f; // above the highest ground layer (plazas, 0.22 m)

        /// <summary>The mesh arrays. Built pooled (MeshArrays), they are longer than the mesh: use the counts.</summary>
        public float[] Vertices;
        public int[] Indices;
        public int BuildingIndexCount;
        public int RoadIndexCount;
        public int VertexCount;
        public int IndexCount => BuildingIndexCount + RoadIndexCount;
        bool pooled;

        public static ChunkOutlines Build(DadcChunk c, bool pooled = false)
        {
            var scratch = BuildScratch.Take();
            var v = scratch.Positions;
            var idx = scratch.Indices;
            foreach (var b in c.Buildings)
            {
                Loop(b.Outer, v, idx);
                if (b.Holes != null) foreach (var h in b.Holes) Loop(h, v, idx);
            }
            int buildings = idx.Count;
            foreach (var r in c.Roads) Strip(r.Pts, v, idx);
            return new ChunkOutlines
            {
                Vertices = MeshArrays.Copy(v, pooled),
                Indices = MeshArrays.Copy(idx, pooled),
                BuildingIndexCount = buildings,
                RoadIndexCount = idx.Count - buildings,
                VertexCount = v.Count / 3,
                pooled = pooled,
            };
        }

        /// <summary>Hands pooled mesh arrays back once the mesh is uploaded; the arrays are gone after.</summary>
        public void Release()
        {
            if (!pooled) return;
            pooled = false;
            MeshArrays.Return(Vertices);
            MeshArrays.Return(Indices);
            Vertices = null;
            Indices = null;
        }

        static int Add(int[] pts, List<float> v)
        {
            int first = v.Count / 3;
            for (int i = 0; i + 1 < pts.Length; i += 2)
            {
                v.Add(pts[i] / 100f);
                v.Add(Lift);
                v.Add(pts[i + 1] / 100f);
            }
            return first;
        }

        static void Loop(int[] pts, List<float> v, List<int> idx)
        {
            int n = pts.Length / 2;
            if (n < 2) return;
            int first = Add(pts, v);
            for (int i = 0; i < n; i++)
            {
                idx.Add(first + i);
                idx.Add(first + (i + 1) % n);
            }
        }

        static void Strip(int[] pts, List<float> v, List<int> idx)
        {
            int n = pts.Length / 2;
            if (n < 2) return;
            int first = Add(pts, v);
            for (int i = 0; i + 1 < n; i++)
            {
                idx.Add(first + i);
                idx.Add(first + i + 1);
            }
        }
    }
}
