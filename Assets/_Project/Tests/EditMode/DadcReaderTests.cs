// The C# DADC reader against the Node exporter: every chunk in StreamingAssets/World
// must decode to the counts, hashes and check totals that world.json records.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Dad.World;
using NUnit.Framework;
using UnityEngine;

namespace Dad.Tests
{
    public class DadcReaderTests
    {
        [Serializable] class WorldJson { public string format; public WorldChunk[] chunks; }
        [Serializable] class WorldChunk { public string id; public int ix; public int iy; public int[] cell; public int bytes; public string sha256; public Checks checks; public Counts counts; }
        [Serializable] class Checks { public long positions; public long sumEastCm; public long sumNorthCm; public long sumInts; }
        [Serializable] class Counts { public int buildings; public int roads; public int graphNodes; public int graphEdges; }

        static string WorldDir => TestWorld.City;
        static byte[] Chunk(string id)
        {
            var f = Path.Combine(WorldDir, "chunks", id + ".dadc");
            return File.ReadAllBytes(File.Exists(f) ? f : Path.Combine(TestWorld.Root, "chunks", id + ".dadc")); // The old format's hand-checked chunk stays in the fixture.
        }

        static WorldJson LoadWorld()
        {
            var path = Path.Combine(WorldDir, "world.json");
            Assume.That(File.Exists(path), "world data missing: run Tools/worldgen/build.sh");
            return JsonUtility.FromJson<WorldJson>(File.ReadAllText(path));
        }

        [Test]
        public void EveryChunkMatchesTheExporter()
        {
            var world = LoadWorld();
            Assert.AreEqual("dad-unity-world/1", world.format);
            Assert.AreEqual(TestWorld.CityChunks, world.chunks.Length);
            var failures = new List<string>();
            var ms = new List<double>();
            using var sha = SHA256.Create();
            foreach (var c in world.chunks)
            {
                var data = Chunk(c.id);
                var hash = BitConverter.ToString(sha.ComputeHash(data)).Replace("-", "").ToLowerInvariant();
                if (data.Length != c.bytes || hash != c.sha256) failures.Add($"{c.id}: file differs from world.json");
                var sw = Stopwatch.StartNew();
                var d = Dadc.Decode(data);
                ms.Add(sw.Elapsed.TotalMilliseconds);
                if (d.Ix != c.ix || d.Iy != c.iy || d.Id != c.id) failures.Add($"{c.id}: ix/iy");
                if (d.CornerEastCm != (long)c.cell[0] * DadcChunk.ChunkCm || d.CornerNorthCm != (long)c.cell[1] * DadcChunk.ChunkCm) failures.Add($"{c.id}: cell");
                if (d.Buildings.Length + d.Placed.Length != c.counts.buildings || d.Roads.Length != c.counts.roads || d.Nodes.Length != c.counts.graphNodes || d.Edges.Length != c.counts.graphEdges)
                    failures.Add($"{c.id}: counts");
                var k = d.Checks;
                if (k.Positions != c.checks.positions || k.SumEastCm != c.checks.sumEastCm || k.SumNorthCm != c.checks.sumNorthCm || k.SumInts != c.checks.sumInts)
                    failures.Add($"{c.id}: checks {k.Positions}/{k.SumEastCm}/{k.SumNorthCm}/{k.SumInts}");
            }
            ms.Sort();
            double P(double p) => ms[Math.Min(ms.Count - 1, (int)(p / 100 * ms.Count))];
            // Editor, Mono JIT, this machine: a reference figure, not a player measurement.
            UnityEngine.Debug.Log($"DADC decode, {ms.Count} chunks, editor: p50 {P(50):F3} ms, p95 {P(95):F3} ms, max {ms[ms.Count - 1]:F3} ms, total {ms.Sum():F0} ms");
            Assert.IsEmpty(failures, string.Join("\n", failures.Take(20)));
        }

        [Test]
        public void PositionsAreUnityCentimetresFromTheSouthWestCorner()
        {
            // Road 13848342 (Airport Road) starts at three.js (3964.63, -15000): east 464.63 m and
            // north 500 m from chunk 7,-30's south-west corner (east 3500, north 14500).
            var d = Dadc.Decode(Chunk("c_7_-30"));
            var road = d.Roads.Single(r => r.Id == 13848342);
            Assert.AreEqual(46463, road.Pts[0]);
            Assert.AreEqual(50000, road.Pts[1]);
            Assert.AreEqual("Airport Road", road.NameEn);
            Assert.AreEqual(350000, d.CornerEastCm);
            Assert.AreEqual(1450000, d.CornerNorthCm);
            foreach (var e in d.Edges) StringAssert.IsMatch(@"^w\d+:\d+$", d.EdgeId(e));
            foreach (var n in d.Nodes) StringAssert.IsMatch(@"^n-?\d+_-?\d+$", d.NodeId(n));
        }

        [Test]
        public void CorruptDataThrows()
        {
            var good = Chunk("c_7_-30");
            Assert.Throws<DadcFormatException>(() => Dadc.Decode(good.Take(good.Length - 3).ToArray()));
            var magic = (byte[])good.Clone();
            magic[0] = 0;
            Assert.Throws<DadcFormatException>(() => Dadc.Decode(magic));
            var format = (byte[])good.Clone();
            format[4] = 9;
            Assert.Throws<DadcFormatException>(() => Dadc.Decode(format));
            Assert.Throws<DadcFormatException>(() => Dadc.Decode(good.Concat(new byte[] { 0 }).ToArray()));
        }
    }
}
