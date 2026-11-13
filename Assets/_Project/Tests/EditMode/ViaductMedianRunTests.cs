// K-37 P-5a (owner, 2026-11-05: "Yes, build it", a 3.0 m median where both carriageways keep two lanes): a continuous
// raised median along the viaduct's deck line where one-way twin carriageways pass either side of it and each keeps at
// least two 3 m lanes; a pier whose island footprint lies on it stands on it and gets no island of its own. No pier
// moves and no road changes. Made-up chunks check each rule on its own: twins that keep two lanes get it; the two-lane
// limit, with the pier's own test RoadMargin inside it; a two-way road, a single one-way road (the median would split
// it) and twins of which one is two-way get none; it stops short of a crossing road and of a building on the ground,
// not of a raised one; a piece shorter than an island between two stops is not drawn; it runs to the chunk border; it
// keeps an island's margin from a junction pad that stops short of the deck line; it meets the border where the twins
// cross it at an angle; a divider stops at it, and a chunk built next does not inherit it. The slice and Line 6 figures
// are pinned in RoadWidthTests and InfraSurveyTests, and the infrastructure checker judges every median pier and part
// independently.
using System;
using System.Linq;
using Dad.World;
using NUnit.Framework;

namespace Dad.Tests
{
    public class ViaductMedianRunTests
    {
        /// <summary>A made-up chunk at the world origin: one elevated track at 31 m along z = 250 m from x0 to x1 (cm), and the roads given.</summary>
        static DadcChunk Chunk(Road[] roads, Building[] buildings = null, GraphEdge[] edges = null, int x0 = 5000, int x1 = 45000) => new DadcChunk
        {
            Ix = 0, Iy = -1, WidthsResolved = true, Roads = roads, Buildings = buildings ?? new Building[0], Edges = edges,
            Rails = new[] { new Rail { Id = 30, YCm = 3100, Pts = new[] { x0, 25000, x1, 25000 } } },
        };

        static GraphEdge OneWay(Road r, int seq) => new GraphEdge { Seq = seq, A = 2 * seq, B = 2 * seq + 1, Way = r.Id, Cls = r.Cls, Oneway = 1, Pts = r.Pts };

        /// <summary>Twin secondaries w cm wide meeting on the track's line, A south of it running east, B north running west; the median covers 1.5 m of each.</summary>
        static Road[] Twins(int w) => new[]
        {
            new Road { Id = 40, Cls = "secondary", WidthCm = w, YCm = 28, Pts = new[] { 0, 25000 - w / 2, 50000, 25000 - w / 2 } },
            new Road { Id = 41, Cls = "secondary", WidthCm = w, YCm = 28, Pts = new[] { 50000, 25000 + w / 2, 0, 25000 + w / 2 } },
        };

        static ChunkStreet Build(int w, Road[] extra = null, Building[] buildings = null, bool bOneWay = true, int x0 = 5000, int x1 = 45000)
        {
            var t = Twins(w);
            var edges = bOneWay ? new[] { OneWay(t[0], 1), OneWay(t[1], 2) } : new[] { OneWay(t[0], 1) };
            return ChunkRoads.Build(Chunk(t.Concat(extra ?? new Road[0]).ToArray(), buildings, edges, x0, x1), keepMarks: true).Street;
        }

        static ChunkStreet.Item[] Piers(ChunkStreet st) => st.Items.Where(i => i.Kind == ChunkStreet.Kind.Pier && !i.Skipped).ToArray();

        static ChunkStreet.Item At(ChunkStreet st, double x) => Piers(st).Single(p => Math.Abs(p.X - x) < 1e-6);

        /// <summary>Vertices of the structures range (0) at the median's top (1 cm under an island's) on the deck line between the first two piers, where no island stands.</summary>
        static int MedianTopVertices(ChunkStreet st)
        {
            int n = 0;
            for (int v = st.FirstVertex[0]; v < st.FirstVertex[0] + st.Vertices[0]; v++)
            {
                double x = st.Positions[3 * v], y = st.Positions[3 * v + 1], z = st.Positions[3 * v + 2];
                if (x > 65 && x < 85 && Math.Abs(z - 250) <= ChunkRoads.ViaductIslandHalf + 1e-3 && Math.Abs(y - (ChunkRoads.MedianTop - 0.01)) < 1e-3) n++;
            }
            return n;
        }

        static bool PartCovers(ChunkStreet st, double x) => st.MedianRunParts.Any(q => Math.Min(q.ax, q.bx) - 1e-6 < x && x < Math.Max(q.ax, q.bx) + 1e-6);

        [Test]
        public void TwinCarriagewaysThatKeepTwoLanesGetAContinuousMedian()
        {
            // 9 m twins: the median keeps 7.5 m of each. One piece along the whole deck, x = 50 to 450, on the track's
            // line; 13 piers stand on it, unmoved. The last pier, at the deck's end (x = 450), has half its footprint past
            // the median's end, so it gets a P-6 island instead.
            var st = Build(900);
            Assert.That(st.MedianRuns, Is.EqualTo(1));
            Assert.That(st.MedianRunLength, Is.EqualTo(400).Within(1e-6));
            Assert.That(st.MedianRunParts.Min(q => Math.Min(q.ax, q.bx)), Is.EqualTo(50).Within(1e-6));
            Assert.That(st.MedianRunParts.Max(q => Math.Max(q.ax, q.bx)), Is.EqualTo(450).Within(1e-6));
            Assert.That(st.MedianRunParts.All(q => Math.Abs(q.az - 250) < 1e-6 && Math.Abs(q.bz - 250) < 1e-6), Is.True, "on the deck line");
            Assert.That(Piers(st).Select(p => p.X), Is.EqualTo(Enumerable.Range(2, 14).Select(k => k * 30.0)).Within(1e-6), "the piers have not moved");
            Assert.That(Piers(st).Select(p => p.Z), Is.All.EqualTo(250).Within(1e-6));
            Assert.That((st.PiersOnMedianRun, st.PiersOnIsland, st.PiersOnRoad), Is.EqualTo((13, 1, 0)));
            Assert.That(Piers(st).Where(p => p.X < 449).Select(p => p.Why), Is.All.EqualTo("median run"));
            Assert.That(At(st, 450).Why, Is.EqualTo("median"));
            var medians = st.Items.Where(i => i.Kind == ChunkStreet.Kind.Median).ToArray();
            Assert.That(medians.Where(m => m.X < 449).Select(m => m.Why), Is.All.EqualTo("run lanes 7.50"));
            // Drawn, not only counted: its top between the piers, and the structures stay closed solids facing out (the
            // median is capped at both ends).
            Assert.That(MedianTopVertices(st), Is.GreaterThan(0));
            var (volume, open) = StreetTests.Solid(st.Positions.Take(3 * st.Vertices[0]).ToList(), st.Indices.Take(st.IndexCounts[0]).ToList(), weld: true);
            Assert.That(open, Is.EqualTo(0), "no open edge");
            Assert.That(volume, Is.GreaterThan(0), "faces out");
            Assert.That(ChunkRoads.MedianRunLanes, Is.EqualTo(6).Within(1e-9));
            Assert.That(ChunkRoads.MedianRunBeside, Is.EqualTo(6.5).Within(1e-9));
        }

        [Test]
        public void TwoLanesEachSideIsTheLimit()
        {
            // 7.6 m twins keep 6.1 m: the median, and the piers on it. 7.4 m keep 5.9 m: no median; the piers keep their
            // P-6 islands. 7.52 m keep 6.02 m: the median is drawn, but each pier's own test over its whole footprint
            // needs RoadMargin more (6.05 m, inside the checker's limit), so the piers stand on P-6 islands on it.
            var over = Build(760);
            Assert.That((over.MedianRuns, over.PiersOnMedianRun, over.PiersOnIsland), Is.EqualTo((1, 13, 1)));
            Assert.That(over.Items.Where(i => i.Kind == ChunkStreet.Kind.Median && i.X < 449).Select(m => m.Why), Is.All.EqualTo("run lanes 6.10"));
            var under = Build(740);
            Assert.That((under.MedianRuns, under.PiersOnMedianRun, under.PiersOnIsland), Is.EqualTo((0, 0, 14)));
            Assert.That(under.Items.Where(i => i.Kind == ChunkStreet.Kind.Median).Select(m => m.Why), Is.All.EqualTo("lanes 5.90"));
            Assert.That(MedianTopVertices(under), Is.EqualTo(0), "nothing at the median's top without it");
            Assert.That(Piers(under).Select(p => p.X), Is.EqualTo(Piers(over).Select(p => p.X)), "no pier moves");
            var margin = Build(752);
            Assert.That(margin.MedianRuns, Is.EqualTo(1));
            Assert.That(margin.MedianRunLength, Is.EqualTo(400).Within(1e-6));
            Assert.That((margin.PiersOnMedianRun, margin.PiersOnIsland), Is.EqualTo((0, 14)));
            Assert.That(ChunkRoads.RoadMargin, Is.EqualTo(0.05).Within(1e-9));
        }

        [Test]
        public void OnlyOneWayTwinsGetIt()
        {
            // A 16 m two-way road on the track's line: no median (the data shows no divided road); P-6 islands keep 6.5 m
            // each side. The same road one-way: none either, the median would split one carriageway in two. Twins of
            // which one is two-way: none; the P-6 island would take a lane of the two-way one, so the piers stay listed.
            var twoWay = new Road { Id = 20, Cls = "primary", WidthCm = 1600, YCm = 28, Pts = new[] { 0, 25000, 50000, 25000 } };
            var a = ChunkRoads.Build(Chunk(new[] { twoWay }), keepMarks: true).Street;
            Assert.That((a.MedianRuns, a.PiersOnMedianRun, a.PiersOnIsland), Is.EqualTo((0, 0, 14)));
            Assert.That(a.MedianRunParts, Is.Empty);
            var oneWay = new Road { Id = 20, Cls = "primary", WidthCm = 1600, YCm = 28, Pts = new[] { 0, 25000, 50000, 25000 } };
            var b = ChunkRoads.Build(Chunk(new[] { oneWay }, edges: new[] { OneWay(oneWay, 1) }), keepMarks: true).Street;
            Assert.That((b.MedianRuns, b.PiersOnMedianRun, b.PiersOnIsland), Is.EqualTo((0, 0, 14)));
            Assert.That(b.Items.Where(i => i.Kind == ChunkStreet.Kind.Median).Select(m => m.Why), Is.All.EqualTo("lanes 6.50"));
            var mixed = Build(900, bOneWay: false);
            Assert.That((mixed.MedianRuns, mixed.PiersOnMedianRun, mixed.PiersOnIsland, mixed.PiersOnRoad), Is.EqualTo((0, 0, 0, 14)));
            Assert.That(mixed.IslandRefusals["narrow"], Is.EqualTo(14));
        }

        [Test]
        public void TheMedianStopsShortOfACrossingRoadAndAGroundBuilding()
        {
            // A 6 m road crossing at x = 200: two pieces, 390 m, none within 5 m of its centre line; the pier at x = 210
            // (its footprint from 206.1) still stands on the median.
            var cross = new Road { Id = 21, Cls = "residential", WidthCm = 600, YCm = 28, Pts = new[] { 20000, 0, 20000, 50000 } };
            var c = Build(900, extra: new[] { cross });
            Assert.That(c.MedianRuns, Is.EqualTo(2));
            Assert.That(c.MedianRunLength, Is.EqualTo(390).Within(1e-6));
            Assert.That(c.MedianRunParts.Min(q => Math.Min(Math.Abs(q.ax - 200), Math.Abs(q.bx - 200))), Is.GreaterThanOrEqualTo(5 - 1e-6));
            Assert.That(PartCovers(c, 200), Is.False);
            Assert.That(At(c, 210).Why, Is.EqualTo("median run"));
            // A ground building over the median's north edge from x = 92.5 to 100: the median stops either side; the pier
            // at x = 90 is off it, and its P-6 island would reach the building too, so it stays on the carriageway. A
            // raised building (a station body) in the same place stops nothing.
            var ground = new Building { Id = 70, HeightCm = 900, Outer = new[] { 9250, 25120, 10000, 25120, 10000, 26000, 9250, 26000 } };
            var g = Build(900, buildings: new[] { ground });
            Assert.That(g.MedianRuns, Is.EqualTo(2));
            Assert.That(g.MedianRunLength, Is.EqualTo(390).Within(1e-6));
            Assert.That(PartCovers(g, 96), Is.False);
            Assert.That(At(g, 90).Why, Is.EqualTo("on a carriageway; no island: building"));
            Assert.That((g.PiersOnMedianRun, g.PiersOnIsland, g.PiersOnRoad), Is.EqualTo((12, 1, 1)));
            var raised = new Building { Id = 71, HeightCm = 2400, MinHeightCm = 1600, Outer = new[] { 9250, 25120, 10000, 25120, 10000, 26000, 9250, 26000 } };
            var r = Build(900, buildings: new[] { raised });
            Assert.That((r.MedianRuns, r.PiersOnMedianRun), Is.EqualTo((1, 13)));
            Assert.That(r.MedianRunLength, Is.EqualTo(400).Within(1e-6));
        }

        [Test]
        public void APieceShorterThanAnIslandBetweenTwoStopsIsNotDrawn()
        {
            // Ground buildings over the median's edge at x = 100 to 104 and 110 to 114: between them only 4 m would pass
            // (x = 105 to 109), shorter than an island (7.8 m), so it is left out; the divider runs on there instead.
            var g1 = new Building { Id = 72, HeightCm = 900, Outer = new[] { 10000, 25100, 10400, 25100, 10400, 26000, 10000, 26000 } };
            var g2 = new Building { Id = 73, HeightCm = 900, Outer = new[] { 11000, 25100, 11400, 25100, 11400, 26000, 11000, 26000 } };
            var st = Build(900, buildings: new[] { g1, g2 });
            Assert.That(st.MedianRuns, Is.EqualTo(2));
            Assert.That(st.MedianRunLength, Is.EqualTo(384).Within(1e-6), "50 to 99 and 115 to 450");
            Assert.That(PartCovers(st, 107), Is.False);
            Assert.That(st.Items.Any(i => i.Kind == ChunkStreet.Kind.Divider && i.X > 100 && i.X < 114), Is.True, "the divider between the buildings");
            Assert.That(2 * ChunkRoads.ViaductIslandEnds, Is.EqualTo(7.8).Within(1e-9));
        }

        [Test]
        public void TheMedianRunsToTheChunkBorder()
        {
            // The track crosses the whole chunk: the median runs from border to border (500 m), so it meets the
            // neighbour's at the seam. A section near the border is judged inside the chunk only, with the roads cut on the
            // border taken on straight. The pier on the border keeps its reason: an island there would cross the border.
            var st = Build(900, x0: -5000, x1: 55000);
            Assert.That(st.MedianRuns, Is.EqualTo(1));
            Assert.That(st.MedianRunLength, Is.EqualTo(500).Within(1e-6));
            Assert.That(st.MedianRunParts.Min(q => Math.Min(q.ax, q.bx)), Is.EqualTo(0).Within(1e-6));
            Assert.That(st.MedianRunParts.Max(q => Math.Max(q.ax, q.bx)), Is.EqualTo(500).Within(1e-6));
            Assert.That(st.PiersOnMedianRun, Is.EqualTo(16));
            Assert.That(At(st, 0).Why, Is.EqualTo("on a carriageway; no island: border"));
            // Capped at the border too (the neighbour may not go on): the only open edges are the girder's ends, which run on
            // into the neighbour, as in the same chunk with 7.4 m twins (no median).
            int Open(ChunkStreet s) => StreetTests.Solid(s.Positions.Take(3 * s.Vertices[0]).ToList(), s.Indices.Take(s.IndexCounts[0]).ToList(), weld: true).open;
            var without = Build(740, x0: -5000, x1: 55000);
            Assert.That(without.MedianRuns, Is.EqualTo(0));
            Assert.That(Open(st), Is.EqualTo(Open(without)));
            Assert.That(Open(st), Is.GreaterThan(0), "the girder's ends at the border");
        }

        [Test]
        public void TheMedianKeepsAnIslandsMarginFromAJunctionPad()
        {
            // A side road joins A at x = 200 from the south, where A is split into two ways (as the map draws a T-junction):
            // a junction pad. A runs 1 m south of the track's line here, so the pad stops 1 m short of the deck line, inside
            // the median's half width (1.5 m) but beyond the 0.3 m MedianFits looks around the deck line. The section's own
            // test keeps the median a half width off the pad: it stops 5 m either side of the side road (x = 195 and 205).
            // Without it, the road-end rule alone stops it only 3 m either side. The pier at x = 210 still stands on it.
            int w = 900;
            var a1 = new Road { Id = 40, Cls = "secondary", WidthCm = w, YCm = 28, Pts = new[] { 0, 24450, 20000, 24450 } };
            var a2 = new Road { Id = 42, Cls = "secondary", WidthCm = w, YCm = 28, Pts = new[] { 20000, 24450, 50000, 24450 } };
            var b = new Road { Id = 41, Cls = "secondary", WidthCm = w, YCm = 28, Pts = new[] { 50000, 25450, 0, 25450 } };
            var side = new Road { Id = 21, Cls = "residential", WidthCm = 600, YCm = 28, Pts = new[] { 20000, 0, 20000, 24450 } };
            var built = ChunkRoads.Build(Chunk(new[] { a1, a2, b, side }, edges: new[] { OneWay(a1, 1), OneWay(a2, 3), OneWay(b, 2) }), keepMarks: true);
            Assert.That(built.Pads, Is.EqualTo(1), "the junction pad");
            var st = built.Street;
            Assert.That(st.MedianRuns, Is.EqualTo(2));
            Assert.That(st.MedianRunLength, Is.EqualTo(390).Within(1e-6), "50 to 195 and 205 to 450");
            Assert.That(st.MedianRunParts.Min(q => Math.Min(Math.Abs(q.ax - 200), Math.Abs(q.bx - 200))), Is.EqualTo(5).Within(1e-6));
            Assert.That(PartCovers(st, 196) || PartCovers(st, 204), Is.False);
            Assert.That(At(st, 210).Why, Is.EqualTo("median run"));
        }

        [Test]
        public void TheMedianMeetsTheBorderWhereTheTwinsCrossItAtAnAngle()
        {
            // The track and the twins cross the chunk at 45 degrees, the twins cut on the west and north borders. Near a
            // border a cross-section's foot on one twin lands past its cut end; that road is taken on straight past the
            // border, so the median runs from border to border (400 m east, 400 m north) and meets the neighbour's.
            // Without it, it stopped 4.4 m short of each border (a seam gap, as Line 6 showed at every seam).
            int w = 900;
            var a = new Road { Id = 40, Cls = "secondary", WidthCm = w, YCm = 28, Pts = new[] { 0, 9364, 40636, 50000 } };
            var b = new Road { Id = 41, Cls = "secondary", WidthCm = w, YCm = 28, Pts = new[] { 39364, 50000, 0, 10636 } };
            var c = new DadcChunk
            {
                Ix = 0, Iy = -1, WidthsResolved = true, Roads = new[] { a, b }, Buildings = new Building[0], Edges = new[] { OneWay(a, 1), OneWay(b, 2) },
                Rails = new[] { new Rail { Id = 30, YCm = 3100, Pts = new[] { -5000, 5000, 45000, 55000 } } },
            };
            var st = ChunkRoads.Build(c, keepMarks: true).Street;
            Assert.That(st.MedianRuns, Is.EqualTo(1));
            Assert.That(st.MedianRunLength, Is.EqualTo(400 * Math.Sqrt(2)).Within(1e-6));
            Assert.That(st.MedianRunParts.Min(q => Math.Min(q.ax, q.bx)), Is.EqualTo(0).Within(1e-6), "to the west border");
            Assert.That(st.MedianRunParts.Max(q => Math.Max(q.az, q.bz)), Is.EqualTo(500).Within(1e-6), "to the north border");
            Assert.That(st.PiersOnMedianRun, Is.EqualTo(13));
        }

        [Test]
        public void ADividerStopsAtTheMedianAndTheNextChunkDoesNotInheritIt()
        {
            // The twins meet on the track's line, where a divider would run: it stops at the median and runs only past the
            // deck's ends. The same roads without a track, built next on the same thread, get the whole divider, as they do
            // after a chunk with no median: the median's parts are this chunk's only.
            var st = Build(900);
            var dividers = st.Items.Where(i => i.Kind == ChunkStreet.Kind.Divider).ToArray();
            Assert.That(dividers, Is.Not.Empty);
            Assert.That(dividers.All(d => d.X < 50 - 1e-6 || d.X > 450 + 1e-6), Is.True, "no divider piece on the median");
            DadcChunk NoTrack() => new DadcChunk
            {
                Ix = 0, Iy = -1, WidthsResolved = true, Roads = Twins(900), Buildings = new Building[0],
                Edges = new[] { OneWay(Twins(900)[0], 1), OneWay(Twins(900)[1], 2) },
            };
            var after = ChunkRoads.Build(NoTrack(), keepMarks: true).Street;
            ChunkRoads.Build(new DadcChunk { Ix = 5, Iy = -5, Roads = new Road[0], Buildings = new Building[0], WidthsResolved = true }, keepMarks: true);
            var alone = ChunkRoads.Build(NoTrack(), keepMarks: true).Street;
            Assert.That(after.DividerPieces, Is.EqualTo(alone.DividerPieces));
            Assert.That(after.DividerIslandGaps, Is.EqualTo(0));
            Assert.That(after.MedianRuns, Is.EqualTo(0));
            Assert.That(after.Items.Where(i => i.Kind == ChunkStreet.Kind.Divider).Select(i => i.X),
                Is.EqualTo(alone.Items.Where(i => i.Kind == ChunkStreet.Kind.Divider).Select(i => i.X)));
        }
    }
}
