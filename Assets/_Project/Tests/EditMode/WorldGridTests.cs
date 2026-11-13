// Chunk ids, cells and world metres agree with world.json, and the floating origin keeps
// positions to 1 mm anywhere in the city.
using System;
using System.IO;
using Dad.World;
using NUnit.Framework;
using UnityEngine;

namespace Dad.Tests
{
    public class WorldGridTests
    {
        [Serializable] class WorldJson { public WorldChunk[] chunks; }
        [Serializable] class WorldChunk { public string id; public int ix; public int iy; public int[] cell; }

        const double Mm = 0.001;

        static WorldJson LoadWorld()
        {
            var path = Path.Combine(TestWorld.Root, "world.json");
            Assume.That(File.Exists(path), "world data missing: run Tools/worldgen/build.sh");
            return JsonUtility.FromJson<WorldJson>(File.ReadAllText(path));
        }

        [Test]
        public void EveryChunkIdCellAndCornerAgree()
        {
            var world = LoadWorld();
            Assert.AreEqual(1303, world.chunks.Length);
            foreach (var c in world.chunks)
            {
                Assert.IsTrue(ChunkCell.TryParse(c.id, out var cell), c.id);
                Assert.AreEqual(new ChunkCell(c.cell[0], c.cell[1]), cell, c.id);
                Assert.AreEqual((c.ix, c.iy), (cell.Ix, cell.Iy), c.id);
                Assert.AreEqual(c.id, cell.Id);
                Assert.AreEqual(cell, ChunkCell.Of(cell.CornerEast, cell.CornerNorth), c.id + " SW corner");
                Assert.AreEqual(cell, ChunkCell.Of(cell.CornerEast + 499.999, cell.CornerNorth + 499.999), c.id + " NE inside");
                Assert.AreEqual(new ChunkCell(cell.X + 1, cell.Z + 1), ChunkCell.Of(cell.CornerEast + 500, cell.CornerNorth + 500), c.id + " NE edge");
            }
        }

        [Test]
        public void CellsAndFrameAtKnownPoints()
        {
            Assert.AreEqual("c_0_-1", ChunkCell.Of(0, 0).Id);
            Assert.AreEqual("c_-1_0", ChunkCell.Of(-0.0001, -0.0001).Id);
            // Farmgate smoke chunk: SW corner 3500 m east, 14500 m north.
            Assert.AreEqual("c_7_-30", ChunkCell.Of(3750, 14750).Id);
            Assert.AreEqual((3500.0, 14500.0), (ChunkCell.FromIxIy(7, -30).CornerEast, ChunkCell.FromIxIy(7, -30).CornerNorth));
            foreach (var bad in new[] { null, "", "c_7", "c_7_", "c_x_1", "d_7_-30", "c_07_-30", "c_7_-30_1" })
                Assert.IsFalse(ChunkCell.TryParse(bad, out _), bad ?? "null");
            WorldFrame.ToUtm(3750, 14750, out double e, out double n);
            Assert.AreEqual((233750.0, 2629750.0), (e, n));
            WorldFrame.FromUtm(e, n, out double east, out double north);
            Assert.AreEqual((3750.0, 14750.0), (east, north));
        }

        [Test]
        public void OriginShiftKeepsPositionsToOneMillimetre()
        {
            // City extent from world.json: cells x -5..32, z 9..60.
            var rng = new System.Random(20261030);
            double worstAbs = 0, worstRel = 0;
            for (int i = 0; i < 20000; i++)
            {
                double east = -2500 + rng.NextDouble() * 19000, north = 4500 + rng.NextDouble() * 26000;
                var origin = new OriginOffset();
                origin.Reset(east + (rng.NextDouble() - 0.5) * 4000, north + (rng.NextDouble() - 0.5) * 4000);
                Assert.AreEqual(0, origin.East % ChunkCell.Size);
                Assert.AreEqual(0, origin.North % ChunkCell.Size);
                origin.ToScene(east, north, out float x, out float z);
                origin.ToWorld(x, z, out double e2, out double n2);
                worstAbs = Math.Max(worstAbs, Math.Max(Math.Abs(e2 - east), Math.Abs(n2 - north)));

                // A neighbour 0-20 m away, moved with the scene by a shift: relative offset survives.
                double de = rng.NextDouble() * 20, dn = rng.NextDouble() * 20;
                origin.ToScene(east + de, north + dn, out float x2, out float z2);
                Assert.IsTrue(origin.TryShift(x, z, 0f, out double se, out double sn));
                Assert.AreEqual(Math.Round(se / ChunkCell.Size), se / ChunkCell.Size);
                Assert.AreEqual(Math.Round(sn / ChunkCell.Size), sn / ChunkCell.Size);
                float ax = x - (float)se, az = z - (float)sn, bx = x2 - (float)se, bz = z2 - (float)sn;
                worstRel = Math.Max(worstRel, Math.Max(Math.Abs((bx - ax) - de), Math.Abs((bz - az) - dn)));
                origin.ToWorld(ax, az, out e2, out n2);
                worstAbs = Math.Max(worstAbs, Math.Max(Math.Abs(e2 - east), Math.Abs(n2 - north)));
                Assert.AreEqual(ChunkCell.Of(east, north), origin.Cell);
            }
            Debug.Log($"Floating origin, 20000 samples: worst absolute {worstAbs * 1000:F3} mm, worst relative {worstRel * 1000:F3} mm");
            Assert.Less(worstAbs, Mm);
            Assert.Less(worstRel, Mm);
        }

        [Test]
        public void NoShiftInsideThreshold()
        {
            var origin = new OriginOffset();
            Assert.IsFalse(origin.TryShift(1999f, -2000f, 2000f, out _, out _));
            Assert.IsTrue(origin.TryShift(2000.5f, 0f, 2000f, out double e, out double n));
            Assert.AreEqual((2000.0, 0.0), (e, n));
            Assert.AreEqual((2000.0, 0.0), (origin.East, origin.North));
            Assert.IsTrue(origin.TryShift(-2600f, -10f, 2000f, out e, out n));
            Assert.AreEqual((-3000.0, -500.0), (e, n));
            Assert.AreEqual((-1000.0, -500.0), (origin.East, origin.North));
        }
    }
}
