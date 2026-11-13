// The lanes-based width rule (RoadWidths, K-37 proposal P-1, approved 2026-11-03, narrowing only): an
// estimated, one-way, lanes-tagged road takes lanes x 3.5 m where that is narrower; everything else
// keeps the exporter's width; the file format reader is untouched; a road's sidewalks and lines still
// follow its class width; and the eight piers the rule was approved to clear stand off the road.
using System;
using System.IO;
using System.Linq;
using Dad.World;
using NUnit.Framework;
using UnityEngine;

namespace Dad.Tests
{
    public class RoadWidthTests
    {
        static DadcChunk Chunk(params Road[] roads) => new DadcChunk
        {
            Roads = roads,
            Edges = roads.Select(r => new GraphEdge { Way = r.Id, Oneway = r.Id % 2 == 1 ? 1 : 0 }).ToArray(), // odd ids one-way
        };

        [Test]
        public void OnlyEstimatedOneWayLanesTaggedRoadsChange()
        {
            var oneWay3 = new Road { Id = 1, Cls = "trunk", WidthCm = 1400, WidthEstimated = true, Lanes = 3 };
            var mapped = new Road { Id = 3, Cls = "trunk", WidthCm = 1200, WidthEstimated = false, Lanes = 3 };
            var twoWay = new Road { Id = 2, Cls = "trunk", WidthCm = 1400, WidthEstimated = true, Lanes = 3 };
            var noLanes = new Road { Id = 5, Cls = "primary", WidthCm = 1100, WidthEstimated = true };
            var wider = new Road { Id = 7, Cls = "residential", WidthCm = 500, WidthEstimated = true, Lanes = 2 };
            var same = new Road { Id = 11, Cls = "service", WidthCm = 350, WidthEstimated = true, Lanes = 1 };
            var zero = new Road { Id = 9, Cls = "primary", WidthCm = 1100, WidthEstimated = true, Lanes = 0 };
            var c = Chunk(oneWay3, mapped, twoWay, noLanes, wider, zero, same);
            RoadWidths.Resolve(c);
            Assert.AreEqual(1050, oneWay3.WidthCm); Assert.IsTrue(oneWay3.WidthFromLanes); Assert.AreEqual(1400, oneWay3.ClassWidthCm);
            Assert.AreEqual(1200, mapped.WidthCm); Assert.IsFalse(mapped.WidthFromLanes);
            Assert.AreEqual(1400, twoWay.WidthCm); Assert.IsFalse(twoWay.WidthFromLanes);
            Assert.AreEqual(1100, noLanes.WidthCm); Assert.IsFalse(noLanes.WidthFromLanes);
            Assert.AreEqual(500, wider.WidthCm, "never wider than the class width (owner's choice)"); Assert.IsFalse(wider.WidthFromLanes);
            Assert.AreEqual(350, same.WidthCm); Assert.IsFalse(same.WidthFromLanes);
            Assert.AreEqual(1100, zero.WidthCm);
            // Once only.
            RoadWidths.Resolve(c);
            Assert.AreEqual(1050, oneWay3.WidthCm); Assert.AreEqual(1400, oneWay3.ClassWidthCm);
        }

        [Test]
        public void DecodeKeepsTheFileAndLoadAppliesTheRule()
        {
            var bytes = File.ReadAllBytes(Path.Combine(SidewalkTests.ChunkDir, "c_8_-28.dadc"));
            var raw = Dadc.Decode(bytes).Roads.First(r => r.Id == 155271803); // Kazi Nazrul Islam Avenue, 3 lanes, one-way
            var loaded = Dadc.Load(bytes).Roads.First(r => r.Id == 155271803);
            Assert.AreEqual(1400, raw.WidthCm);
            Assert.IsFalse(raw.WidthFromLanes);
            Assert.AreEqual(1050, loaded.WidthCm);
            Assert.AreEqual(1400, loaded.ClassWidthCm);
            Assert.IsTrue(loaded.WidthFromLanes);
        }

        [Test]
        public void TheRuleMovesKerbsButAddsOrRemovesNoSidewalksOrLines()
        {
            // A one-lane one-way secondary narrows from 9 to 3.5 m and keeps the sidewalks of its
            // class width (it is below the 6 m width rule now); a two-lane one-way
            // residential lane that the lanes tag would widen to 7 m keeps 5 m and has neither.
            var c = new DadcChunk
            {
                Ix = 0, Iy = -1,
                Roads = new[]
                {
                    new Road { Id = 1, Cls = "residential", WidthCm = 500, WidthEstimated = true, Lanes = 2, YCm = 28, Pts = new[] { 10000, 10000, 40000, 10000 } },
                    new Road { Id = 3, Cls = "secondary", WidthCm = 900, WidthEstimated = true, Lanes = 1, YCm = 28, Pts = new[] { 10000, 30000, 40000, 30000 } },
                },
                Nodes = new GraphNode[0],
                Edges = new[] { new GraphEdge { Way = 1, Oneway = 1 }, new GraphEdge { Way = 3, Oneway = 1 } },
            };
            var r = ChunkRoads.Build(c, keepMarks: true);
            Assert.AreEqual(500, c.Roads[0].WidthCm);
            Assert.AreEqual(350, c.Roads[1].WidthCm);
            bool Paved(long way, int side) => r.Walks.Any(w => w.Way == way && w.Side == side && w.Kind != ChunkRoads.WalkKind.Drain);
            Assert.IsFalse(Paved(1, 1) || Paved(1, -1), "the lane has no sidewalks");
            Assert.IsTrue(r.Walks.Any(w => w.Way == 1 && w.Kind == ChunkRoads.WalkKind.Drain), "it keeps its drains");
            Assert.AreEqual(0, r.Marks.Count(m => m.Way == 1 && m.Kind <= MarkKind.EdgeLine), "nor lines");
            Assert.IsTrue(Paved(3, 1) && Paved(3, -1), "the street keeps both sidewalks at 3.5 m");
            // Its kerbs moved in: the sidewalk starts 1.75 m from the centre line, not 4.5 m.
            double inner = r.Walks.Where(w => w.Way == 3 && w.Kind != ChunkRoads.WalkKind.Drain).Min(w => Math.Abs(w.InAz - 300));
            Assert.AreEqual(1.75, inner, 0.01);
        }

        // K37-10 and K37-52 to K37-58: on the drawn carriageway at the class width, clear at lanes x 3.5 m.
        static readonly (double e, double n)[] Cleared =
        {
            (3870.0, 14988.8), (4362.8, 13770.0), (4371.4, 13740.0), (4380.1, 13710.0),
            (4388.7, 13680.0), (4397.3, 13650.0), (4405.9, 13620.0), (4414.6, 13590.0),
        };

        [Test]
        public void TheApprovedPiersStandOffTheRoad()
        {
            SidewalkTests.LoadSlice();
            int found = 0, onRoad = 0, piers = 0, piersOnRoad = 0, onIsland = 0, onRun = 0;
            foreach (var kv in SidewalkTests.built)
            {
                var c = SidewalkTests.chunks[kv.Key];
                double ce = c.CornerEastCm / 100.0, cn = c.CornerNorthCm / 100.0;
                foreach (var it in kv.Value.Street.Items.Where(i => i.Kind == ChunkStreet.Kind.Pier && !i.Skipped))
                {
                    piers++;
                    if (OnRoad(it)) piersOnRoad++;
                    if (it.Why == "median") onIsland++;
                    if (it.Why == "median run") onRun++;
                    if (!Cleared.Any(p => Math.Abs(p.e - (ce + it.X)) < 0.2 && Math.Abs(p.n - (cn + it.Z)) < 0.2)) continue;
                    found++;
                    if (OnRoad(it)) onRoad++;
                }
            }
            Debug.Log($"Piers {piers}, on a carriageway {piersOnRoad}, on a median island {onIsland}, on the continuous median {onRun}; the eight cleared by P-1: found {found}, on a carriageway {onRoad}");
            Assert.AreEqual(Cleared.Length, found, "the piers have not moved");
            Assert.AreEqual(0, onRoad);
            // P-2 and K-50 (2026-11-04): 66 piers, 44 on a carriageway after P-1. The Farmgate twin, split by the
            // c_7_-30/-31 border, now has one pier row on its centre line: three of its six grid places fall in this
            // slice and only one of them on the road (-3 piers, -5 on a carriageway); the c_6_-30 copy of the border
            // pier at E 3,390 is gone (-1); K37-13, 12.7 m from the next pier at the bend, is spanned (-1); K37-16 on
            // the crossing trunk_link is spanned (-1, -1); K37-46 slides 3.75 m along the deck off its side road (-1 on a carriageway);
            // and the N 13,500 grid line on the c_8_-28/-27 border gets its pier, on an along-road trunk (+1, +1).
            Assert.AreEqual(61, piers);
            // P-6 (2026-11-04): 10 of the 38 stand on a median island with traffic on both sides; 28 stay on the
            // carriageway, each with its reason (12 traffic on one side only, 6 narrow, 4 a crossing road under the
            // island, 3 at the chunk border, 3 a building on the ground).
            // P-5a (2026-11-05): the continuous median runs under the station piers K37-40, -41 and -42 at Kawran Bazar, which
            // stood on P-6 islands, and under K37-59, which stood on the carriageway (traffic on one side of an island only:
            // the twin beside it is 4 to 6 m off, inside the median's reach). The P-6 island under the pier at N 14,190 stays.
            Assert.AreEqual(27, piersOnRoad, "K-37: 52 before P-1, 44 after it, 38 after P-2, 28 after P-6, 27 after P-5a");
            Assert.AreEqual(7, onIsland, "P-6: piers on a median island (10 before P-5a)");
            Assert.AreEqual(4, onRun, "P-5a: piers on the continuous median");
        }

        static bool OnRoad(ChunkStreet.Item it) => it.Why != null && it.Why.StartsWith("on a carriageway", StringComparison.Ordinal);
    }
}
