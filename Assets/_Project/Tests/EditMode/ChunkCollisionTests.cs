// What a vehicle can touch (E-04): the chunk's collision mesh against the chunk it came from. No engine needed.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dad.World;
using NUnit.Framework;
using UnityEngine;

namespace Dad.Tests
{
    public class ChunkCollisionTests
    {
        static string ChunkDir => Path.Combine(TestWorld.Root, "chunks");

        sealed class Rig
        {
            public DadcChunk Chunk;
            public ChunkRoads Roads;
            public ChunkBuildings Solids;
            public ChunkCollision Mesh;
            public ChunkLanes Lanes;
        }

        static readonly Dictionary<string, Rig> Rigs = new Dictionary<string, Rig>();

        static Rig Load(int x = 7, int y = -29)
        {
            string name = $"c_{x}_{y}";
            if (Rigs.TryGetValue(name, out var r)) return r;
            var c = Dadc.Load(File.ReadAllBytes(Path.Combine(ChunkDir, name + ".dadc")));
            NeighbourRails.Attach(c, ChunkDir);
            var roads = ChunkRoads.Build(c);
            var solids = ChunkBuildings.Build(c);
            return Rigs[name] = new Rig { Chunk = c, Roads = roads, Solids = solids, Mesh = ChunkCollision.Build(c, roads, solids), Lanes = ChunkLanes.Build(c, roads.Street.Blocks) };
        }

        // The highest surface facing up under a point, and null when there is none: a plain look through every triangle.
        static double? Top(ChunkCollision m, double x, double z, double from = 1e9)
        {
            double? best = null;
            for (int t = 0; t < m.IndexCount; t += 3)
            {
                int a = 3 * m.Indices[t], b = 3 * m.Indices[t + 1], c = 3 * m.Indices[t + 2];
                float[] p = m.Positions;
                double ax = p[a], az = p[a + 2], bx = p[b], bz = p[b + 2], cx = p[c], cz = p[c + 2];
                if (x < Math.Min(ax, Math.Min(bx, cx)) || x > Math.Max(ax, Math.Max(bx, cx)) || z < Math.Min(az, Math.Min(bz, cz)) || z > Math.Max(az, Math.Max(bz, cz))) continue;
                double d = (bx - ax) * (cz - az) - (cx - ax) * (bz - az);
                if (Math.Abs(d) < 1e-9) continue;
                double u = ((x - ax) * (cz - az) - (cx - ax) * (z - az)) / d, v = ((bx - ax) * (z - az) - (x - ax) * (bz - az)) / d;
                if (u < -1e-6 || v < -1e-6 || u + v > 1 + 1e-6) continue;
                double y = p[a + 1] + u * (p[b + 1] - p[a + 1]) + v * (p[c + 1] - p[a + 1]);
                if (y <= from && (best == null || y > best)) best = y;
            }
            return best;
        }

        static (double x, double y, double z) Normal(ChunkCollision m, int t)
        {
            int a = 3 * m.Indices[t], b = 3 * m.Indices[t + 1], c = 3 * m.Indices[t + 2];
            float[] p = m.Positions;
            double ux = p[b] - p[a], uy = p[b + 1] - p[a + 1], uz = p[b + 2] - p[a + 2], vx = p[c] - p[a], vy = p[c + 1] - p[a + 1], vz = p[c + 2] - p[a + 2];
            return (uy * vz - uz * vy, uz * vx - ux * vz, ux * vy - uy * vx);
        }

        [Test]
        public void TheMeshIsSound()
        {
            var m = Load().Mesh;
            Assert.Greater(m.IndexCount, 1000);
            Assert.AreEqual(0, m.IndexCount % 3);
            Assert.AreEqual(m.GroundTriangles + m.RoadTriangles + m.WallTriangles + m.SolidTriangles, m.IndexCount / 3, "every triangle is counted once");
            Assert.Greater(m.GroundTriangles, 2, "PhysX warns of triangles over 500 m");
            for (int i = 0; i < m.IndexCount; i++) Assert.That(m.Indices[i], Is.InRange(0, m.VertexCount - 1));
            for (int i = 0; i < 3 * m.VertexCount; i++) Assert.IsTrue(float.IsFinite(m.Positions[i]));
        }

        [Test]
        public void NoTriangleIsLongEnoughToTroublePhysics()
        {
            var m = Load().Mesh;
            for (int t = 0; t < m.IndexCount; t += 3)
                for (int k = 0; k < 3; k++)
                {
                    int a = 3 * m.Indices[t + k], b = 3 * m.Indices[t + (k + 1) % 3];
                    double dx = m.Positions[a] - m.Positions[b], dz = m.Positions[a + 2] - m.Positions[b + 2];
                    Assert.Less(dx * dx + dz * dz, 500.0 * 500.0);
                }
        }

        [Test]
        public void ThereIsGroundEverywhere()
        {
            var m = Load().Mesh;
            for (double x = 5; x < 500; x += 70) for (double z = 5; z < 500; z += 70) Assert.IsNotNull(Top(m, x, z), $"{x}, {z}");
        }

        [Test]
        public void EveryLaneHasTheRoadUnderIt()
        {
            int tested = 0, off = 0;
            foreach (var (x, y) in new[] { (7, -29), (8, -29), (7, -30) })
            {
                var r = Load(x, y);
                foreach (var l in r.Lanes.Lanes.Take(120))
                {
                    if (l.Connector) continue;
                    int mid = l.Pts.Length / 4;
                    double px = l.Pts[2 * mid], pz = l.Pts[2 * mid + 1], top = Top(r.Mesh, px, pz, l.Y + 1) ?? -1;
                    // A lane's end can reach a little over the border, onto the next chunk's road.
                    if (px < 0 || pz < 0 || px > 500 || pz > 500) continue;
                    tested++;
                    // The road as drawn: a lane near the edge can cross a drain, 4 cm down, or its 6 cm lip (K-31, the widths are estimates).
                    if (Math.Abs(top - l.Y) > ChunkRoads.DrainLipHeight + 0.01) { off++; Console.WriteLine($"  c_{x}_{y} lane {l.Id} way {l.Way} at {px:0.0}, {pz:0.0}: lane y {l.Y:0.00}, surface {top:0.00}"); }
                }
            }
            Console.WriteLine($"lanes tested {tested}, off the surface {off}");
            Assert.Greater(tested, 100);
            Assert.AreEqual(0, off);
        }

        [Test]
        public void ThePaintIsLeftOutAndCounted()
        {
            var r = Load();
            Assert.Greater(r.Mesh.MarkTriangles, 100);
            Assert.AreEqual(r.Roads.IndexCount / 3, r.Mesh.RoadTriangles + r.Mesh.MarkTriangles);
        }

        [Test]
        public void WallsAndSolidsLookOutward()
        {
            var r = Load();
            var m = r.Mesh;
            // The walls come after the ground and the road, in the order of the chunk's buildings.
            int first = 3 * (m.GroundTriangles + m.RoadTriangles), walls = 0;
            for (int t = first; t < first + 3 * m.WallTriangles; t += 3)
            {
                var n = Normal(m, t);
                if (n.x * n.x + n.z * n.z < 1e-10) continue;
                Assert.Less(Math.Abs(n.y), 1e-3 * Math.Sqrt(n.x * n.x + n.z * n.z), "a wall stands upright");
                walls++;
            }
            Assert.Greater(walls, 100);
            // A step out from the middle of each wall leaves the building, and a step in stays in it.
            int outward = 0, inward = 0, at = first;
            var flat = new List<double>();
            var holes = new List<int>();
            for (int i = 0; i < r.Chunk.Buildings.Length; i++)
            {
                if (r.Solids.VertexCounts[i] == 0 || r.Solids.Placements[i].Base > ChunkCollision.Clearance) continue;
                ChunkSurfaces.Flatten(r.Chunk.Buildings[i].Outer, null, flat, holes);
                int n = flat.Count / 2;
                for (int e = 0; e < n; e++, at += 6)
                {
                    var nn = Normal(m, at);
                    double len = Math.Sqrt(nn.x * nn.x + nn.z * nn.z);
                    int j = (e + 1) % n;
                    double ex = flat[2 * j] - flat[2 * e], ez = flat[2 * j + 1] - flat[2 * e + 1];
                    if (len < 1e-9 || ex * ex + ez * ez < 1) continue;
                    double mx = (flat[2 * e] + flat[2 * j]) / 2, mz = (flat[2 * e + 1] + flat[2 * j + 1]) / 2, ux = nn.x / len * 0.02, uz = nn.z / len * 0.02;
                    if (!Inside(flat, mx + ux, mz + uz) && Inside(flat, mx - ux, mz - uz)) outward++; else inward++;
                }
            }
            Assert.AreEqual(first + 3 * m.WallTriangles, at, "two triangles an edge, in the order of the buildings");
            Console.WriteLine($"walls looking out {outward}, not {inward}");
            Assert.Greater(outward, 200);
            // A ring can fold back on itself within the 2 cm stepped: one in the slice.
            Assert.LessOrEqual(inward, outward / 500);
            for (int t = first + 3 * m.WallTriangles; t < 3 * (m.IndexCount / 3); t += 3)
            {
                var n = Normal(m, t);
                Assert.GreaterOrEqual(n.y, -1e-6, "nothing of a box looks down");
            }
        }

        static bool Inside(List<double> f, double x, double z)
        {
            bool inside = false;
            int n = f.Count / 2;
            for (int i = 0, j = n - 1; i < n; j = i++)
                if (f[2 * i + 1] > z != f[2 * j + 1] > z && x < (f[2 * j] - f[2 * i]) * (z - f[2 * i + 1]) / (f[2 * j + 1] - f[2 * i + 1]) + f[2 * i]) inside = !inside;
            return inside;
        }

        [Test]
        public void WhatStandsOnTheStreetIsInIt()
        {
            int piers = 0, medians = 0, posts = 0;
            for (int x = 6; x <= 9; x++)
                for (int y = -30; y <= -28; y++)
                {
                    var r = Load(x, y);
                    foreach (var s in r.Roads.Street.Solids)
                    {
                        Assert.Greater(s.Top, s.Y);
                        if (s.Half >= 0.7 && s.Top > 2) piers++;
                        else if (s.Top < 1) medians++;
                        else if (s.Half < 0.25) posts++;
                    }
                    Assert.GreaterOrEqual(r.Roads.Street.Solids.Length, r.Roads.Street.Poles + r.Roads.Street.Lamps + r.Roads.Street.Trees);
                }
            Console.WriteLine($"piers {piers}, median parts {medians}, posts {posts}");
            Assert.Greater(piers, 10);
            Assert.Greater(medians, 10);
            Assert.Greater(posts, 100);
        }

        [Test]
        public void EverySolidIsInTheMesh()
        {
            var r = Load();
            int seen = 0;
            for (int i = 0; i < r.Roads.Street.Solids.Length; i += 7)
            {
                var q = r.Roads.Street.Solids[i];
                var top = Top(r.Mesh, q.X, q.Z);
                Assert.IsTrue(top.HasValue && top.Value >= q.Top - 0.02, $"the solid at {q.X:0.0}, {q.Z:0.0} stands {q.Top:0.00} m up; the mesh there {top}");
                seen++;
            }
            Assert.Greater(seen, 5);
        }

        [Test]
        public void EveryPierTrafficKeepsClearOfIsSolid()
        {
            int piers = 0;
            for (int x = 6; x <= 9; x++)
                for (int y = -30; y <= -28; y++)
                {
                    var st = Load(x, y).Roads.Street;
                    foreach (var b in st.Blocks)
                    {
                        if (b.Along != b.Half) continue;  // round: a pier, not a length of median
                        piers++;
                        Assert.IsTrue(st.Solids.Any(s => Math.Abs(s.X - b.X) < 0.01 && Math.Abs(s.Z - b.Z) < 0.01 && s.Top > 2), $"no solid for the pier at {b.X:0.0}, {b.Z:0.0}");
                    }
                }
            Assert.Greater(piers, 10);
        }

        [Test]
        public void ABuildingOnLegsLetsAVehicleUnder()
        {
            var seen = 0;
            for (int x = 6; x <= 9; x++) for (int y = -30; y <= -28; y++) seen += Load(x, y).Mesh.Raised;
            Console.WriteLine($"raised buildings left out {seen}");
            Assert.Greater(seen, 0);
        }

        [Test]
        public void ItIsMuchLighterThanWhatIsDrawn()
        {
            long mesh = 0, drawn = 0, walls = 0;
            int chunks = 0;
            for (int x = 6; x <= 9; x++)
                for (int y = -30; y <= -28; y++)
                {
                    var r = Load(x, y);
                    mesh += r.Mesh.IndexCount / 3; walls += r.Mesh.WallTriangles;
                    drawn += r.Roads.IndexCount / 3 + r.Solids.IndexCount / 3;
                    chunks++;
                }
            Console.WriteLine($"collision {mesh / chunks} triangles a chunk (walls {walls / chunks}), drawn roads and buildings {drawn / chunks}");
            Assert.Less(mesh, drawn);
        }

        [Test]
        public void ItDoesNotChangeTheDrawnMeshes()
        {
            var c = Dadc.Load(File.ReadAllBytes(Path.Combine(ChunkDir, "c_7_-29.dadc")));
            var a = ChunkRoads.Build(c);
            var before = a.Positions.Take(3 * a.VertexCount).ToArray();
            var idx = a.Indices.Take(a.IndexCount).ToArray();
            ChunkCollision.Build(c, a, ChunkBuildings.Build(c));
            CollectionAssert.AreEqual(before, a.Positions.Take(3 * a.VertexCount).ToArray());
            CollectionAssert.AreEqual(idx, a.Indices.Take(a.IndexCount).ToArray());
        }
    }
}
