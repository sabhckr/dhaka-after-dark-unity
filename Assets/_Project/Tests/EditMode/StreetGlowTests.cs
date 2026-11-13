using System.Linq;
using Dad.World;
using NUnit.Framework;

namespace Dad.Tests
{
    public class StreetGlowTests
    {
        static readonly ChunkStreet.LampHead[] Two =
        {
            new ChunkStreet.LampHead { X = 100, Y = 8, Z = 50, Fx = 1, Fz = 0, Seed = 7 },
            new ChunkStreet.LampHead { X = 140, Y = 9, Z = 60, Fx = 0, Fz = -1, Seed = 8 },
        };

        [Test]
        public void EachLampHasAPoolAndAHalo()
        {
            var g = StreetGlow.Build(Two);
            Assert.AreEqual(2, g.Lamps);
            Assert.AreEqual(16, g.VertexCount);
            Assert.AreEqual(24, g.Indices.Length);
            Assert.AreEqual(8, g.Colors.Count(c => c >> 24 == StreetGlow.HaloMark));
        }

        [Test]
        public void ThePoolLiesOnTheGroundAheadOfTheHead()
        {
            var g = StreetGlow.Build(Two);
            double x = 0, z = 0;
            for (int k = 0; k < 4; k++)
            {
                Assert.AreEqual(StreetGlow.PoolHeight, g.Positions[3 * k + 1], 1e-5);
                x += g.Positions[3 * k] / 4;
                z += g.Positions[3 * k + 2] / 4;
            }
            Assert.AreEqual(100 + StreetGlow.PoolAhead, x, 1e-4);
            Assert.AreEqual(50, z, 1e-4);
        }

        [Test]
        public void TheHaloStaysAtTheHead()
        {
            var g = StreetGlow.Build(Two);
            for (int k = 4; k < 8; k++)
            {
                Assert.AreEqual(100, g.Positions[3 * k], 1e-5);
                Assert.AreEqual(8, g.Positions[3 * k + 1], 1e-5);
                Assert.AreEqual(50, g.Positions[3 * k + 2], 1e-5);
            }
        }

        [Test]
        public void TrianglesFaceUp()
        {
            var g = StreetGlow.Build(Two);
            int a = g.Indices[0], b = g.Indices[1], c = g.Indices[2];
            double ux = g.Positions[3 * b] - g.Positions[3 * a], uz = g.Positions[3 * b + 2] - g.Positions[3 * a + 2];
            double vx = g.Positions[3 * c] - g.Positions[3 * a], vz = g.Positions[3 * c + 2] - g.Positions[3 * a + 2];
            Assert.Greater(uz * vx - ux * vz, 0, "seen from above in Unity's left-handed frame");
        }

        [Test]
        public void SomeLampsAreSodiumAndMostAreNot()
        {
            int sodium = 0, n = 4000;
            for (int i = 0; i < n; i++) if (StreetGlow.ColourOf(i * 7919L + 13) == (((StreetGlow.Sodium >> 16) & 0xff) | (StreetGlow.Sodium & 0xff00) | ((StreetGlow.Sodium & 0xff) << 16))) sodium++;
            Assert.That(sodium / (double)n, Is.InRange(StreetGlow.SodiumShare - 0.05, StreetGlow.SodiumShare + 0.05));
        }

        [Test]
        public void NoLampsNoMesh()
        {
            var g = StreetGlow.Build(new ChunkStreet.LampHead[0]);
            Assert.AreEqual(0, g.VertexCount);
            Assert.AreEqual(0, g.Indices.Length);
        }
    }
}
