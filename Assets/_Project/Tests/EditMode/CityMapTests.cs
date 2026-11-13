// The new city as the game reads it (brief of 2026-11-06): every chunk loads in the reader the game uses, the lanes climb the flyovers, and every model named in the data is in the model file.
// Tools/worldgen/check.py checks the plan itself from the written files.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dad.Core;
using Dad.World;
using NUnit.Framework;
using UnityEngine;

namespace Dad.Tests
{
    public class CityMapTests
    {
        static string ChunkDir => Path.Combine(Application.streamingAssetsPath, "World", "chunks");
        static IEnumerable<DadcChunk> All() => Directory.GetFiles(ChunkDir, "*.dadc").Select(f => Dadc.Load(File.ReadAllBytes(f)));

        [Test]
        public void TheWorldIsExactlyFifteenAndAHalfSquareKilometres()
        {
            var chunks = Directory.GetFiles(ChunkDir, "*.dadc");
            Assert.AreEqual(62, chunks.Length);
            Assert.AreEqual(15.5, chunks.Length * ChunkCell.Size * ChunkCell.Size / 1e6, 1e-9);
        }

        [Test]
        public void EveryPlacedModelIsInTheModelFile()
        {
            int placed = 0;
            foreach (var c in All())
                foreach (var b in c.Placed)
                {
                    if (b.Model == "collider") continue;
                    placed++;
                    Assert.NotNull(CityModels.Get(b.Model), b.Model);
                    Assert.AreEqual(7, b.Pose.Length, b.Model);
                }
            Assert.Greater(placed, 10000);
        }

        [Test]
        public void ModelsAreNotAlsoExtruded()
        {
            foreach (var c in All()) Assert.IsTrue(c.Buildings.All(b => b.Model == null), c.Id);
        }

        [Test]
        public void EveryModelHasTwoLevelsAndItsPictures()
        {
            Assert.GreaterOrEqual(CityModels.All.Count, 30);
            foreach (var m in CityModels.All)
            {
                Assert.AreEqual(2, m.Lods.Length, m.Name);
                for (int l = 0; l < 2; l++)
                {
                    Assert.Greater(m.Lods[l].vertexCount, 0, m.Name);
                    Assert.AreEqual(m.Lods[l].subMeshCount, m.Materials[l].Length, m.Name);
                    foreach (var mat in m.Materials[l]) Assert.NotNull(mat, m.Name);
                }
            }
            Assert.AreEqual("Ishak Hasan Sabbir", CityModels.Owner);
        }

        [Test]
        public void LanesClimbTheFlyovers()
        {
            float top = 0;
            int raised = 0;
            foreach (var c in All())
            {
                var lanes = ChunkLanes.Build(c);
                foreach (var l in lanes.Lanes)
                {
                    if (l.Ys == null) continue;
                    raised++;
                    top = Mathf.Max(top, l.YAt(l.Length / 2));
                }
            }
            Assert.Greater(raised, 0);
            Assert.AreEqual(7.5f, top, 0.5f);
        }

        [Test]
        public void TheSlumsHoldOnlyMiniBuildings()
        {
            // The data keeps no zone, so this reads the rule the other way: every mini is small, and the model file has them under that name only.
            foreach (var m in CityModels.All.Where(m => m.Name.StartsWith("mini"))) Assert.Less(Mathf.Max(m.Size.x, m.Size.z), 13, m.Name);
        }
    }
}
