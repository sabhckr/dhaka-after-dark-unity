// Every golden view loads, names itself, looks at world data and has a golden image.
using System.IO;
using Dad.Core;
using Dad.World;
using NUnit.Framework;
using UnityEngine;

namespace Dad.Tests
{
    public class GoldenViewTests
    {
        [Test]
        public void EveryViewIsValidAndHasAGolden()
        {
            var views = Resources.LoadAll<TextAsset>("Views");
            Assert.IsNotEmpty(views);
            var golden = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Tests", "Golden"));
            var chunks = Path.Combine(TestWorld.City, "chunks");
            foreach (var a in views)
            {
                var v = ViewSpec.Load(a.name);
                Assert.Greater(v.height, 1f, a.name);
                Assert.That(v.fov, Is.InRange(10f, 120f), a.name);
                Assert.AreEqual((1280, 720), (v.width, v.height_px), $"{a.name}: shots are 1280x720");
                Assert.IsTrue(File.Exists(Path.Combine(golden, a.name + ".png")), $"{a.name}: no golden; run Tools/shot.sh {a.name} --accept after checking the shot");
                Assert.IsTrue(File.Exists(Path.Combine(chunks, ChunkCell.Of(v.lookEast, v.lookNorth).Id + ".dadc")), $"{a.name} looks at a world chunk");
            }
            Assert.Throws<FileNotFoundException>(() => ViewSpec.Load("no-such-view"));
        }
    }
}
