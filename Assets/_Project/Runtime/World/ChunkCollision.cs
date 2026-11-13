// What a vehicle can touch in a chunk (E-04), as one triangle list, built on the worker with the rest of the chunk and
// cooked there (ChunkColliders). No engine dependency, so it is tested without Unity.
//
// - The ground: the chunk's base plane, at y 0, as a grid, for PhysX warns of any triangle over 500 m long. The landuse above it
//   is paint, a few millimetres thick.
// - The road mesh as drawn, carriageways, decks, sidewalks, kerbs and barriers, less its markings. So a wheel rides on what
//   is on screen and nothing is invented: "use the existing road system".
// - Building walls: the footprint raised WallHeight above its base, facing out. The drawn building is far heavier than a
//   vehicle can feel (26,945 triangles a chunk on average, against a few thousand here). A building raised on legs clear of
//   the road (Base above Clearance) is left out, so a vehicle passes under it.
// - The street's solids (ChunkStreet.Solids) as boxes.
//
// Nothing here moves or changes the world: it is read from the same data the chunk draws.
using System;
using System.Collections.Generic;

namespace Dad.World
{
    public sealed class ChunkCollision
    {
        public const double WallHeight = 6, Clearance = 3;
        const int GroundCells = 5;

        /// <summary>Metres from the chunk corner, x y z. Built pooled (MeshArrays), they are longer than the mesh: use the counts.</summary>
        public float[] Positions;
        public int[] Indices;
        public int VertexCount, IndexCount;
        /// <summary>Triangles by what they came from; markings left out of the road mesh; buildings left out as raised.</summary>
        public int GroundTriangles, RoadTriangles, WallTriangles, SolidTriangles, MarkTriangles, Raised;
        bool pooled;

        public static ChunkCollision Build(DadcChunk c, ChunkRoads roads, ChunkBuildings solids, bool pooled = false)
        {
            var scratch = BuildScratch.Take();
            var p = scratch.Positions;
            var idx = scratch.Indices;
            p.Clear();
            idx.Clear();
            var s = new ChunkCollision { pooled = pooled };
            s.Ground(p, idx);
            s.Road(roads, p, idx);
            s.Walls(c, solids, p, idx, scratch.Flat, scratch.Holes);
            int before = idx.Count;
            foreach (var q in roads.Street.Solids) Box(q, p, idx);
            s.SolidTriangles = (idx.Count - before) / 3;
            s.VertexCount = p.Count / 3;
            s.IndexCount = idx.Count;
            s.Positions = MeshArrays.Copy(p, pooled);
            s.Indices = MeshArrays.Copy(idx, pooled);
            return s;
        }

        /// <summary>Hands the pooled arrays back once they are in a mesh; the arrays are gone after.</summary>
        public void Release()
        {
            if (!pooled || Positions == null) return;
            MeshArrays.Return(Positions);
            MeshArrays.Return(Indices);
            Positions = null;
            Indices = null;
        }

        static void Add(List<float> p, double x, double y, double z)
        {
            p.Add((float)x); p.Add((float)y); p.Add((float)z);
        }

        void Ground(List<float> p, List<int> idx)
        {
            int n = GroundCells + 1;
            double step = ChunkCell.Size / GroundCells;
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++) Add(p, i * step, 0, j * step);
            for (int i = 0; i < GroundCells; i++)
                for (int j = 0; j < GroundCells; j++)
                {
                    int a = i * n + j;
                    Quad(idx, a, a + 1, a + n + 1, a + n);
                }
            GroundTriangles = idx.Count / 3;
        }

        // The road's triangles without the paint, and only the vertices they use.
        void Road(ChunkRoads r, List<float> p, List<int> idx)
        {
            var map = new int[r.VertexCount];
            int first = p.Count / 3, used = 0;
            for (int i = 0; i < r.IndexCount; i++)
            {
                if (i == r.MarkFirstIndex) { i = r.MarkEndIndex - 1; continue; }
                int v = r.Indices[i];
                if (map[v] == 0)
                {
                    map[v] = ++used;
                    p.Add(r.Positions[3 * v]); p.Add(r.Positions[3 * v + 1]); p.Add(r.Positions[3 * v + 2]);
                }
                idx.Add(first + map[v] - 1);
            }
            RoadTriangles = (r.IndexCount - (r.MarkEndIndex - r.MarkFirstIndex)) / 3;
            MarkTriangles = (r.MarkEndIndex - r.MarkFirstIndex) / 3;
        }

        void Walls(DadcChunk c, ChunkBuildings solids, List<float> p, List<int> idx, List<double> flat, List<int> holes)
        {
            if (c.Buildings == null) return;
            int before = idx.Count;
            for (int i = 0; i < c.Buildings.Length; i++)
            {
                if (solids.VertexCounts[i] == 0) continue;
                var pl = solids.Placements[i];
                if (pl.Base > Clearance) { Raised++; continue; }
                var b = c.Buildings[i];
                ChunkSurfaces.Flatten(b.Outer, null, flat, holes);
                Ring(flat, pl.Base, Math.Min(pl.Top, pl.Base + WallHeight), p, idx);
            }
            // Models stand on their footprints too.
            foreach (var b in c.Placed)
            {
                if (b.Outer == null || b.Outer.Length < 6 || b.Pose == null) continue;
                ChunkSurfaces.Flatten(b.Outer, null, flat, holes);
                double foot = b.Pose[2] / 100.0;
                Ring(flat, foot, foot + Math.Min(b.HeightCm / 100.0, WallHeight), p, idx);
            }
            WallTriangles = (idx.Count - before) / 3;
        }

        // One outer ring as walls facing out, whichever way round it was drawn.
        static void Ring(List<double> f, double y0, double y1, List<float> p, List<int> idx)
        {
            int n = f.Count / 2;
            double area = 0;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                area += f[2 * i] * f[2 * j + 1] - f[2 * j] * f[2 * i + 1];
            }
            int first = p.Count / 3;
            for (int i = 0; i < n; i++)
            {
                Add(p, f[2 * i], y0, f[2 * i + 1]);
                Add(p, f[2 * i], y1, f[2 * i + 1]);
            }
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n, a = first + 2 * i, b = first + 2 * j;
                // Corners a, a+1 (top) then b, b+1: wound so the normal looks away from the ring's inside.
                if (area > 0) Quad(idx, a, a + 1, b + 1, b);
                else Quad(idx, b, b + 1, a + 1, a);
            }
        }

        static void Quad(List<int> idx, int a, int b, int c, int d)
        {
            idx.Add(a); idx.Add(b); idx.Add(c);
            idx.Add(a); idx.Add(c); idx.Add(d);
        }

        static void Box(in ChunkStreet.Solid q, List<float> p, List<int> idx)
        {
            double ax = q.Ux * q.Along, az = q.Uz * q.Along, bx = -q.Uz * q.Half, bz = q.Ux * q.Half;
            int first = p.Count / 3;
            for (int k = 0; k < 2; k++)
            {
                double y = k == 0 ? q.Y : q.Top;
                Add(p, q.X - ax - bx, y, q.Z - az - bz);
                Add(p, q.X + ax - bx, y, q.Z + az - bz);
                Add(p, q.X + ax + bx, y, q.Z + az + bz);
                Add(p, q.X - ax + bx, y, q.Z - az + bz);
            }
            // The foot ring runs anticlockwise seen with x right and z up, so each side looks out as in Ring.
            for (int i = 0; i < 4; i++)
            {
                int j = (i + 1) % 4;
                Quad(idx, first + i, first + 4 + i, first + 4 + j, first + j);
            }
            Quad(idx, first + 4, first + 7, first + 6, first + 5);
        }
    }
}
