// C-05 overlays: every chunk id can be written in the segment font; Farmgate's overlay holds its
// cell square, its id in the south-west corner, and its whole road graph; signalised nodes get
// squares.
using System;
using System.Collections.Generic;
using System.IO;
using Dad.World;
using NUnit.Framework;
using UnityEngine;

namespace Dad.Tests
{
    public class ChunkDebugTests
    {
        static string ChunkDir => Path.Combine(TestWorld.Root, "chunks");

        [Test]
        public void SegmentFontWritesEveryChunkId()
        {
            Assert.AreEqual(7, SegmentFont.Of('8').Length);
            Assert.AreEqual(2, SegmentFont.Of('1').Length);
            Assert.AreEqual("g", SegmentFont.Of('-'));
            Assert.IsNull(SegmentFont.Of('x'));
            var files = Directory.GetFiles(Path.Combine(TestWorld.City, "chunks"), "*.dadc");
            Assume.That(files.Length, Is.EqualTo(TestWorld.CityChunks), "world data missing: run Tools/worldgen/build.sh");
            foreach (var f in files)
            {
                var id = Path.GetFileNameWithoutExtension(f);
                foreach (char ch in id) Assert.IsTrue(SegmentFont.Supports(ch), $"{id}: '{ch}'");
            }
            var p = new List<float>();
            var idx = new List<int>();
            Assert.AreEqual(2, SegmentFont.Write("-1", 0, 0, 0, 10, p, idx));
            CollectionAssert.AreEqual(new float[] { 0, 0, 10, 10, 0, 10, 15 + 10, 0, 20, 15 + 10, 0, 10, 25, 0, 10, 25, 0, 0 }, p,
                "'-' is the middle bar; '1' is the right side, one advance (1.5 units) along");
        }

        [Test]
        public void FarmgateOverlayHoldsGridIdAndGraph()
        {
            var f = Path.Combine(ChunkDir, "c_7_-30.dadc");
            Assume.That(File.Exists(f), "world data missing: run Tools/worldgen/build.sh");
            var c = Dadc.Load(File.ReadAllBytes(f));
            var d = ChunkDebug.Build(c);

            // The square, then "c_7_-30": c 3 + _ 1 + 7 3 + _ 1 + - 1 + 3 5 + 0 6 = 20 segments.
            Assert.AreEqual(7, d.Glyphs);
            Assert.AreEqual(2 * (4 + 20), d.GridIndices.Length);
            CollectionAssert.AreEqual(new float[] { 0, ChunkDebug.GridY, 0, 500, ChunkDebug.GridY, 0 }, new ArraySegment<float>(d.GridPositions, 0, 6));
            for (int v = 4 * 2 * 3; v < d.GridPositions.Length; v += 3)
            {
                Assert.That(d.GridPositions[v], Is.InRange(ChunkDebug.LabelInset, ChunkDebug.LabelInset + 7 * SegmentFont.Advance * ChunkDebug.LabelUnit), "label in the south-west corner");
                Assert.That(d.GridPositions[v + 2], Is.InRange(ChunkDebug.LabelInset, ChunkDebug.LabelInset + 2 * ChunkDebug.LabelUnit));
            }

            int segments = 0, signals = 0;
            foreach (var e in c.Edges) segments += e.Pts.Length / 2 - 1;
            foreach (var n in c.Nodes) if ((n.Signal ?? 0) > 0) signals++;
            Debug.Log($"c_7_-30 overlay: {c.Edges.Length} edges in {segments} segments, {c.Nodes.Length} nodes, {signals} signalised");
            Assert.AreEqual((segments, c.Nodes.Length, signals), (d.EdgeSegments, d.Nodes, d.Signals));
            Assert.AreEqual((2 * segments, 4 * c.Nodes.Length, 8 * signals), (d.EdgeIndexCount, d.NodeIndexCount, d.SignalIndexCount));
            Assert.AreEqual(d.GraphIndices.Length, d.EdgeIndexCount + d.NodeIndexCount + d.SignalIndexCount);
            Assert.Greater(segments, 0);
            for (int v = 0; v < d.GraphPositions.Length; v += 3)
            {
                Assert.AreEqual(ChunkDebug.GraphY, d.GraphPositions[v + 1]);
                Assert.That(d.GraphPositions[v], Is.InRange(-ChunkDebug.SignalHalf, 500 + ChunkDebug.SignalHalf));
                Assert.That(d.GraphPositions[v + 2], Is.InRange(-ChunkDebug.SignalHalf, 500 + ChunkDebug.SignalHalf));
            }
            // The first edge's first segment starts on its first point.
            Assert.AreEqual(c.Edges[0].Pts[0] / 100f, d.GraphPositions[0], 1e-4);
            Assert.AreEqual(c.Edges[0].Pts[1] / 100f, d.GraphPositions[2], 1e-4);
        }

        [Test]
        public void SignalisedNodesGetSquares()
        {
            // Farmgate has no signalised node; c_8_-28 holds the slice's only ones (4 in the browser
            // build's chunk file). Each gets a square of four sides, SignalHalf from the node.
            var f = Path.Combine(ChunkDir, "c_8_-28.dadc");
            Assume.That(File.Exists(f), "world data missing: run Tools/worldgen/build.sh");
            var c = Dadc.Load(File.ReadAllBytes(f));
            var d = ChunkDebug.Build(c);
            var lit = new List<GraphNode>();
            foreach (var n in c.Nodes) if ((n.Signal ?? 0) > 0) lit.Add(n);
            Assert.AreEqual(4, lit.Count);
            Assert.AreEqual((4, 4 * 8), (d.Signals, d.SignalIndexCount));
            int first = d.EdgeIndexCount + d.NodeIndexCount;
            for (int s = 0; s < lit.Count; s++)
            {
                float x = lit[s].East / 100f, z = lit[s].North / 100f;
                for (int k = 0; k < 8; k++)
                {
                    int v = d.GraphIndices[first + 8 * s + k] * 3;
                    Assert.AreEqual(ChunkDebug.SignalHalf, Mathf.Abs(d.GraphPositions[v] - x), 1e-3, $"node {s} corner {k} east");
                    Assert.AreEqual(ChunkDebug.SignalHalf, Mathf.Abs(d.GraphPositions[v + 2] - z), 1e-3, $"node {s} corner {k} north");
                }
            }
        }
    }
}
