// The water's surface as the game builds it: open sea stays cheap, dry land costs nothing, and the files Tools/worldgen/harbour.py wrote agree with each other across chunk borders.
// Tools/worldgen/check_harbour.py checks where the vessels ended up from the written files.
using System;
using System.IO;
using System.Linq;
using Dad.Core;
using NUnit.Framework;
using UnityEngine;

namespace Dad.Tests
{
    public class WaterViewTests
    {
        const int N = WaterView.Lattice;
        static string Dir => Path.Combine(Application.streamingAssetsPath, "World", "water");

        static byte[] Field(Func<int, int, byte> at)
        {
            var b = new byte[N * N * 2];
            for (int j = 0; j < N; j++)
                for (int i = 0; i < N; i++) b[2 * (i + j * N)] = at(i, j);
            return b;
        }

        [Test]
        public void OpenSeaIsOneQuadPerHundredMetres() => Assert.AreEqual(25 * 6, WaterView.Triangles(Field((i, j) => 255)).Length);

        [Test]
        public void DryLandHasNoTriangles() => Assert.AreEqual(0, WaterView.Triangles(Field((i, j) => 0)).Length);

        [Test]
        public void AShoreOnlyRefinesTheBlocksItTouches()
        {
            // Land up to 195 m east, sea beyond: land blocks are empty, the shore's block is fine, open blocks stay quads.
            var t = WaterView.Triangles(Field((i, j) => (byte)Mathf.Clamp(128 + (i * 10 - 195) * 4, 0, 255)));
            Assert.Greater(t.Length, 25 * 6);
            Assert.Less(t.Length, 50 * 50 * 6 / 2);
        }

        [Test]
        public void EveryFileIsTheRightSize()
        {
            var files = Directory.GetFiles(Dir, "*.bin");
            Assert.Greater(files.Length, 0);
            foreach (var f in files) Assert.AreEqual(N * N * 2, new FileInfo(f).Length, f);
        }

        [Test]
        public void NeighboursAgreeAlongTheirBorder()
        {
            var ids = Directory.GetFiles(Dir, "*.bin").Select(Path.GetFileNameWithoutExtension).ToArray();
            int compared = 0;
            foreach (var id in ids)
            {
                var p = id.Split('_');
                int x = int.Parse(p[1]), y = int.Parse(p[2]);
                var east = Path.Combine(Dir, $"c_{x + 1}_{y}.bin");
                if (!File.Exists(east)) continue;
                byte[] a = File.ReadAllBytes(Path.Combine(Dir, id + ".bin")), b = File.ReadAllBytes(east);
                for (int j = 0; j < N; j++) Assert.AreEqual(a[2 * (N - 1 + j * N)], b[2 * (j * N)], id + " row " + j);
                compared++;
            }
            Assert.Greater(compared, 0);
        }
    }
}
