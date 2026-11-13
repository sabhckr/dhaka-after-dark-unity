// K-37 P-6 (owner, 2026-11-04: "draw medians"): a raised median island under each viaduct pier whose foot is on a
// drawn carriageway, where the road leaves a lane each side and traffic passes on both sides of it; the pier is never
// moved and the road data is unchanged. Made-up chunks check each rule on its own: a wide road gets islands; the lane
// limit, at and just under 3 m; an island against one edge of a one-way road (traffic on one side only) is refused,
// and so is one that would cover a two-way road's lane; twin one-way carriageways count either side, with an open
// strip of up to 1 m between the island and the other one; an island never crosses the chunk border; a
// building on the ground refuses it, a raised one (a station body) does not; a divider stops either side of an
// island. Also the scratch that carries pier places from chunk to chunk (found during P-6): a chunk's supports must
// not depend on the chunk built before it. The slice and Line 6 figures are pinned in RoadWidthTests and
// InfraSurveyTests, and the infrastructure checker judges every island independently.
using System;
using System.Linq;
using Dad.World;
using NUnit.Framework;
using UnityEngine;

namespace Dad.Tests
{
    public class ViaductIslandTests
    {
        /// <summary>A made-up chunk at the world origin: one elevated track at 31 m along z = railZ (cm) from x = 50 to 450 m, and the roads given.</summary>
        static DadcChunk Chunk(Road[] roads, int railZ = 25000, Building[] buildings = null, GraphEdge[] edges = null) => new DadcChunk
        {
            Ix = 0, Iy = -1, WidthsResolved = true, Roads = roads, Buildings = buildings ?? new Building[0], Edges = edges,
            Rails = new[] { new Rail { Id = 30, YCm = 3100, Pts = new[] { 5000, railZ, 45000, railZ } } },
        };

        static Road Along(long id, int widthCm, int zCm, string cls = "primary") => new Road { Id = id, Cls = cls, WidthCm = widthCm, YCm = 28, Pts = new[] { 0, zCm, 50000, zCm } };

        static GraphEdge OneWay(Road r, int seq) => new GraphEdge { Seq = seq, A = 2 * seq, B = 2 * seq + 1, Way = r.Id, Cls = r.Cls, Oneway = 1, Pts = r.Pts };

        static ChunkStreet.Item[] Piers(ChunkStreet st) => st.Items.Where(i => i.Kind == ChunkStreet.Kind.Pier && !i.Skipped).ToArray();

        static ChunkStreet.Item[] Islands(ChunkStreet st) => st.Items.Where(i => i.Kind == ChunkStreet.Kind.Median).ToArray();

        [Test]
        public void AWideRoadUnderTheViaductGetsAnIslandUnderEveryPier()
        {
            // A 14 m road along the track: 14 piers (x = 60 to 450), each on an island 3.0 m wide (the 0.9 m pier and
            // 0.6 m each side) and 7.8 m long, leaving 5.5 m of road each side. The piers stay on the grid.
            var st = ChunkRoads.Build(Chunk(new[] { Along(20, 1400, 25000) }), keepMarks: true).Street;
            var piers = Piers(st);
            var islands = Islands(st);
            Assert.That(piers.Select(p => p.X), Is.EqualTo(Enumerable.Range(2, 14).Select(k => k * 30.0)).Within(1e-6), "the piers have not moved");
            Assert.That(st.PiersOnIsland, Is.EqualTo(14));
            Assert.That(st.PiersOnRoad, Is.EqualTo(0));
            Assert.That(st.IslandRefusals, Is.Null);
            foreach (var p in piers)
            {
                Assert.That(p.Z, Is.EqualTo(250).Within(1e-6));
                Assert.That(p.Why, Is.EqualTo("median"));
                Assert.That(islands.Count(m => Math.Abs(m.X - p.X) < 1e-6 && Math.Abs(m.Z - p.Z) < 1e-6), Is.EqualTo(1), "an island under each");
            }
            Assert.That(islands.Select(m => m.Why), Is.All.EqualTo("lanes 5.50"));
            Assert.That(ChunkRoads.ViaductIslandHalf, Is.EqualTo(1.5).Within(1e-9));
            Assert.That(ChunkRoads.ViaductIslandEnds, Is.EqualTo(3.9).Within(1e-9));
        }

        [Test]
        public void ALaneOfThreeMetresIsTheLimit()
        {
            // 9.0 m: 3.00 m each side, islands. 8.9 m: 2.95 m, none; the piers stay where they are, on the carriageway,
            // listed with the reason.
            var at = ChunkRoads.Build(Chunk(new[] { Along(20, 900, 25000) }), keepMarks: true).Street;
            Assert.That(at.PiersOnIsland, Is.EqualTo(14));
            Assert.That(Islands(at).Select(m => m.Why), Is.All.EqualTo("lanes 3.00"));
            var under = ChunkRoads.Build(Chunk(new[] { Along(20, 890, 25000) }), keepMarks: true).Street;
            Assert.That(under.PiersOnIsland, Is.EqualTo(0));
            Assert.That(under.PiersOnRoad, Is.EqualTo(14));
            Assert.That(under.IslandRefusals["narrow"], Is.EqualTo(14));
            Assert.That(Islands(under), Is.Empty);
            Assert.That(Piers(under).Select(p => p.Why), Is.All.EqualTo("on a carriageway; no island: narrow"));
            Assert.That(Piers(under).Select(p => p.X), Is.EqualTo(Piers(at).Select(p => p.X)), "no pier moves for want of an island");
        }

        [Test]
        public void AnIslandAgainstOneEdgeOfARoadIsRefused()
        {
            // A 7 m road 2.5 m off the track: an island would cover its near edge and keep 4.5 m on the far side only.
            // One-way, the road keeps a lane, but traffic would pass the island on one side: refused (the owner: "traffic
            // passing on both sides"; a flyover's island may stand so, D-11a). Two-way, one direction would lose its lane.
            foreach (bool oneWay in new[] { true, false })
            {
                var road = Along(22, 700, 25250);
                var st = ChunkRoads.Build(Chunk(new[] { road }, edges: oneWay ? new[] { OneWay(road, 1) } : null), keepMarks: true).Street;
                Assert.That(st.PiersOnIsland, Is.EqualTo(0), oneWay ? "one-way" : "two-way");
                Assert.That(st.IslandRefusals[oneWay ? "one side" : "narrow"], Is.EqualTo(14), oneWay ? "one-way" : "two-way");
                Assert.That(Piers(st).Select(p => p.Why), Is.All.EqualTo("on a carriageway; no island: " + (oneWay ? "one side" : "narrow")));
            }
        }

        [Test]
        public void TwinCarriagewaysCountEitherSideWithAStripOfUpToAMetre()
        {
            // One-way A, 7 m, its north edge 1 m past the track (the island, 1.5 m each side, covers it and keeps 4.5 m of
            // A to the south); one-way B, 7 m, the other way, its south edge 0.8 m past the island's north edge: traffic
            // on both sides, islands. With 1.5 m of open ground
            // between the island and B: traffic on one side only, none.
            foreach (var (gapCm, islands) in new[] { (80, 14), (150, 0) })
            {
                var a = new Road { Id = 40, Cls = "secondary", WidthCm = 700, YCm = 28, Pts = new[] { 0, 24750, 50000, 24750 } };
                int bz = 25150 + gapCm + 350;
                var b = new Road { Id = 41, Cls = "secondary", WidthCm = 700, YCm = 28, Pts = new[] { 50000, bz, 0, bz } };
                var st = ChunkRoads.Build(Chunk(new[] { a, b }, edges: new[] { OneWay(a, 1), OneWay(b, 2) }), keepMarks: true).Street;
                Assert.That(st.PiersOnIsland, Is.EqualTo(islands), $"open strip {gapCm} cm");
                if (islands == 0) Assert.That(st.IslandRefusals["one side"], Is.EqualTo(14));
                else Assert.That(Islands(st).Select(m => m.Why), Is.All.EqualTo("lanes 4.50"), "A keeps 4.5 m south of the island");
            }
            Assert.That(ChunkRoads.IslandBeside, Is.EqualTo(1.0));
        }

        [Test]
        public void AnIslandNeverCrossesTheChunkBorder()
        {
            // The track 1 m in from the south border: an island 1.5 m across would reach past it, where this chunk cannot
            // see the neighbour's roads. The piers stay, listed.
            var st = ChunkRoads.Build(Chunk(new[] { Along(20, 1400, 100) }, railZ: 100), keepMarks: true).Street;
            Assert.That(Piers(st), Has.Length.EqualTo(14));
            Assert.That(st.PiersOnIsland, Is.EqualTo(0));
            Assert.That(st.IslandRefusals["border"], Is.EqualTo(14));
            Assert.That(Piers(st).Select(p => p.Why), Is.All.EqualTo("on a carriageway; no island: border"));
        }

        [Test]
        public void ABuildingOnTheGroundRefusesAnIslandARaisedOneDoesNot()
        {
            // A ground building over the corner of the island at x = 90 (from x 92.5, z 251.2), clear of the pier
            // itself: that pier is refused, the other 13 get islands. A raised building (base 16 m, a station body) over
            // the pier at x = 90 refuses nothing: nothing of it is at street level.
            var ground = new Building { Id = 70, HeightCm = 900, Outer = new[] { 9250, 25120, 10000, 25120, 10000, 26000, 9250, 26000 } };
            var st = ChunkRoads.Build(Chunk(new[] { Along(20, 1400, 25000) }, buildings: new[] { ground }), keepMarks: true).Street;
            Assert.That(st.PiersOnIsland, Is.EqualTo(13));
            Assert.That(st.IslandRefusals["building"], Is.EqualTo(1));
            Assert.That(Piers(st).Single(p => Math.Abs(p.X - 90) < 1e-6).Why, Is.EqualTo("on a carriageway; no island: building"));
            var raised = new Building { Id = 71, HeightCm = 2400, MinHeightCm = 1600, Outer = new[] { 8000, 24000, 10000, 24000, 10000, 26000, 8000, 26000 } };
            var under = ChunkRoads.Build(Chunk(new[] { Along(20, 1400, 25000) }, buildings: new[] { raised }), keepMarks: true).Street;
            Assert.That(under.PiersOnIsland, Is.EqualTo(14));
        }

        [Test]
        public void APierInABuildingOnTheGroundIsCountedApartFromOneUnderARaisedBuilding()
        {
            // K-37 P-3: no road, two buildings over the track. One on the ground over the pier at x = 90: a conflict,
            // "in a building". One raised (base 16 m, a station body) over the pier at x = 150: the pier rises into it as a
            // real station's piers do, "under a raised building", counted apart. No pier moves and none is left out.
            var ground = new Building { Id = 70, HeightCm = 900, Outer = new[] { 8000, 24000, 10000, 24000, 10000, 26000, 8000, 26000 } };
            var raised = new Building { Id = 71, HeightCm = 2400, MinHeightCm = 1600, Outer = new[] { 14000, 24000, 16000, 24000, 16000, 26000, 14000, 26000 } };
            var alone = ChunkRoads.Build(Chunk(new Road[0]), keepMarks: true).Street;
            var st = ChunkRoads.Build(Chunk(new Road[0], buildings: new[] { ground, raised }), keepMarks: true).Street;
            Assert.That(st.Piers, Is.EqualTo(14));
            Assert.That(Piers(st).Select(p => p.X), Is.EqualTo(Piers(alone).Select(p => p.X)), "no pier moves");
            Assert.That(st.PiersInBuilding, Is.EqualTo(1));
            Assert.That(st.PiersUnderRaised, Is.EqualTo(1));
            Assert.That(Piers(st).Single(p => Math.Abs(p.X - 90) < 1e-6).Why, Is.EqualTo("in a building"));
            Assert.That(Piers(st).Single(p => Math.Abs(p.X - 150) < 1e-6).Why, Is.EqualTo("under a raised building"));
            Assert.That(Piers(st).Count(p => p.Why != null), Is.EqualTo(2));
            Assert.That(alone.PiersInBuilding + alone.PiersUnderRaised, Is.EqualTo(0));
            // A raised building in the notch of an L-shaped building on the ground: the pier at x = 240 is inside the L's
            // bounding box but not the L, so it stands under the raised one; the pier at x = 210 is in the L.
            var ell = new Building { Id = 72, HeightCm = 900, Outer = new[] { 20000, 24000, 25000, 24000, 25000, 24600, 22000, 24600, 22000, 26000, 20000, 26000 } };
            var notch = new Building { Id = 73, HeightCm = 2400, MinHeightCm = 1600, Outer = new[] { 23000, 24700, 25000, 24700, 25000, 26000, 23000, 26000 } };
            var l = ChunkRoads.Build(Chunk(new Road[0], buildings: new[] { ell, notch }), keepMarks: true).Street;
            Assert.That(Piers(l).Single(p => Math.Abs(p.X - 210) < 1e-6).Why, Is.EqualTo("in a building"));
            Assert.That(Piers(l).Single(p => Math.Abs(p.X - 240) < 1e-6).Why, Is.EqualTo("under a raised building"));
            Assert.That((l.PiersInBuilding, l.PiersUnderRaised), Is.EqualTo((1, 1)));
        }

        [Test]
        public void AnIslandKeepsClearOfZebrasAndJunctionPads()
        {
            // A zebra mapped at x = 95.5: its keep-out (2 m either side along the road) is within the island's half width
            // of the end of the island at x = 90 (to x = 93.9). A road crossing at x = 98 without a node: its junction
            // pad is past the island's end, within its half width. Each refuses that one island, and the pier stays.
            var zebra = new PointFeature { Id = 5, Kind = "crossing", CrossingType = "zebra", East = 9550, North = 25000 };
            var z = ChunkRoads.Build(new DadcChunk
            {
                Ix = 0, Iy = -1, WidthsResolved = true, Roads = new[] { Along(20, 1400, 25000) }, Buildings = new Building[0], Points = new[] { zebra },
                Rails = new[] { new Rail { Id = 30, YCm = 3100, Pts = new[] { 5000, 25000, 45000, 25000 } } },
            }, keepMarks: true).Street;
            Assert.That(z.PiersOnIsland, Is.EqualTo(13));
            Assert.That(Piers(z).Single(p => Math.Abs(p.X - 90) < 1e-6).Why, Is.EqualTo("on a carriageway; no island: crossing"));
            var side = new Road { Id = 21, Cls = "residential", WidthCm = 600, YCm = 28, Pts = new[] { 9800, 0, 9800, 50000 } };
            var j = ChunkRoads.Build(Chunk(new[] { Along(20, 1400, 25000), side }), keepMarks: true).Street;
            Assert.That(j.PiersOnIsland, Is.EqualTo(13));
            Assert.That(Piers(j).Single(p => Math.Abs(p.X - 90) < 1e-6).Why, Is.EqualTo("on a carriageway; no island: junction"));
        }

        [Test]
        public void ADividerStopsEitherSideOfAnIsland()
        {
            // Twin one-way secondaries, 7 m each, meeting on the track's line: a divider runs between them. Each pier's
            // island covers the near side of both and keeps 5.5 m of each; the divider stops short of every island and
            // goes on past it.
            var a = new Road { Id = 40, Cls = "secondary", WidthCm = 700, YCm = 28, Pts = new[] { 0, 24650, 50000, 24650 } };
            var b = new Road { Id = 41, Cls = "secondary", WidthCm = 700, YCm = 28, Pts = new[] { 50000, 25350, 0, 25350 } };
            var st = ChunkRoads.Build(Chunk(new[] { a, b }, edges: new[] { OneWay(a, 1), OneWay(b, 2) }), keepMarks: true).Street;
            Assert.That(st.PiersOnIsland, Is.EqualTo(14));
            Assert.That(Islands(st).Select(m => m.Why), Is.All.EqualTo("lanes 5.50"));
            var dividers = st.Items.Where(i => i.Kind == ChunkStreet.Kind.Divider).ToArray();
            Assert.That(dividers, Is.Not.Empty, "the divider is built between the islands");
            Assert.That(st.DividerIslandGaps, Is.GreaterThan(0));
            foreach (var p in Piers(st))
                foreach (var d in dividers)
                    Assert.That(Math.Abs(d.X - p.X) >= ChunkRoads.ViaductIslandEnds || Math.Abs(d.Z - p.Z) >= ChunkRoads.ViaductIslandHalf, Is.True,
                        $"divider piece at ({d.X:F1}, {d.Z:F1}) on the island at x = {p.X}");
        }

        [Test]
        public void AChunksSupportsDoNotDependOnTheChunkBuiltBeforeIt()
        {
            // Found during P-6: the viaduct's list of pier places, which the flyovers keep clear of, was cleared only in
            // chunks with an elevated track, so a chunk built next on the same thread saw the last viaduct chunk's piers
            // as its own (as if at its own local coordinates). On main at 79cc798 the flyover below gets 16 supports, 13
            // of them slid 3.25 m, after a viaduct chunk; alone, 14 on the grid.
            DadcChunk Fly() => new DadcChunk
            {
                Ix = 0, Iy = -1, WidthsResolved = true, Rails = new Rail[0], Buildings = new Building[0],
                Roads = new[] { new Road { Id = 10, Cls = "primary", WidthCm = 700, YCm = 600, Bridge = true, Pts = new[] { 5000, 25000, 45000, 25000 } } },
            };
            string Sig(ChunkRoads r) => string.Join(" ", r.Street.Items.Where(i => i.Kind == ChunkStreet.Kind.FlyPier || i.Kind == ChunkStreet.Kind.FlyLeg).Select(i => $"{i.X:F2},{i.Z:F2}"));
            ChunkRoads.Build(new DadcChunk { Ix = 5, Iy = -5, Roads = new Road[0], Buildings = new Building[0], WidthsResolved = true }, keepMarks: true);
            string alone = Sig(ChunkRoads.Build(Fly(), keepMarks: true));
            var viaduct = ChunkRoads.Build(Chunk(new Road[0]), keepMarks: true);
            Assert.That(viaduct.Street.Piers, Is.EqualTo(14), "the viaduct chunk has piers on the flyover's places");
            string after = Sig(ChunkRoads.Build(Fly(), keepMarks: true));
            Debug.Log($"alone: {alone}\nafter a viaduct chunk: {after}");
            Assert.That(after, Is.EqualTo(alone));
            Assert.That(alone.Split(' '), Has.Length.EqualTo(14));
        }
    }
}
