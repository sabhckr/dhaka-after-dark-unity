// D-11a (AXIOM v3 section J step 7, owner 2026-11-04): flyover supports and closed decks (Flyovers.cs). Made-up
// chunks check each rule on its own: grid places found whichever way a deck runs; a pier on a median island where a
// wide road runs under the deck, with a lane kept each side; a portal frame over a road too narrow for an island; an
// offset pier beside the railway; a pier in water; a pier slid off the grid rather than span two places; a place on
// the west border; an island covering one side of a one-way road only; pile caps kept apart; a closed girder cut into
// segments at the supports. The slice's
// figures are pinned with the account of each, and the infrastructure checker judges every support independently
// (InfraSurveyTests).
using System;
using System.Collections.Generic;
using System.Linq;
using Dad.World;
using NUnit.Framework;
using UnityEngine;

namespace Dad.Tests
{
    public class FlyoverTests
    {
        const double DeckZ = 250;

        /// <summary>A made-up chunk at the world origin: a 400 m, 7 m wide flyover deck 6 m up along z = 250, east or west, and what is given under it.</summary>
        static DadcChunk Chunk(bool west, Road[] under = null, Rail[] rails = null, Poly[] water = null)
        {
            int[] pts = west ? new[] { 45000, 25000, 5000, 25000 } : new[] { 5000, 25000, 45000, 25000 };
            var roads = new List<Road> { new Road { Id = 10, Cls = "primary", WidthCm = 700, YCm = 600, Bridge = true, Pts = pts } };
            if (under != null) roads.AddRange(under);
            return new DadcChunk { Ix = 0, Iy = -1, Roads = roads.ToArray(), Rails = rails ?? new Rail[0], Water = water, Buildings = new Building[0], WidthsResolved = true };
        }

        static Road Under(int widthCm) => new Road { Id = 20, Cls = "primary", WidthCm = widthCm, YCm = 28, Pts = new[] { 0, 25000, 50000, 25000 } };

        static List<ChunkStreet.Item> Supports(ChunkRoads r) =>
            r.Street.Items.Where(i => (i.Kind == ChunkStreet.Kind.FlyPier || i.Kind == ChunkStreet.Kind.FlyLeg) && !i.Skipped).OrderBy(i => i.X).ThenBy(i => i.Z).ToList();

        [Test]
        public void GridPlacesAreFoundWhicheverWayTheDeckRuns()
        {
            // On open ground: a pier on every 30 m grid line the deck crosses (x = 60 to 450), on its centre line, and no
            // end pier where one stands within 12 m of the end. A deck drawn west must get the same piers as one drawn
            // east (the places were once visited in grid order, which skipped all but the first on a westward deck).
            var east = Supports(ChunkRoads.Build(Chunk(false), keepMarks: true));
            var west = Supports(ChunkRoads.Build(Chunk(true), keepMarks: true));
            Debug.Log("east: " + string.Join(" ", east.Select(i => i.X.ToString("F1"))) + "\nwest: " + string.Join(" ", west.Select(i => i.X.ToString("F1"))));
            var grid = Enumerable.Range(2, 14).Select(k => k * 30.0).ToArray();
            Assert.That(east.Select(i => i.X), Is.EqualTo(grid).Within(1e-6));
            Assert.That(west.Select(i => i.X), Is.EqualTo(grid).Within(1e-6));
            foreach (var i in east.Concat(west))
            {
                Assert.That(i.Kind, Is.EqualTo(ChunkStreet.Kind.FlyPier));
                Assert.That(i.Z, Is.EqualTo(DeckZ).Within(1e-6), "on the deck's centre line");
                Assert.That(i.Why, Is.Null, "on clear ground: not slid, not on a median, not offset");
                Assert.That(i.Top, Is.EqualTo(6 - ChunkRoads.FlyDepth(6)).Within(1e-6), "up to the girder's underside");
            }
        }

        [Test]
        public void AWideRoadUnderTheDeckGetsMedianIslands()
        {
            // A 14 m road runs under the deck: each pier stands on a raised island 2.8 m wide, leaving 5.6 m of road on
            // each side.
            var r = ChunkRoads.Build(Chunk(true, new[] { Under(1400) }), keepMarks: true);
            var piers = Supports(r);
            var islands = r.Street.Items.Where(i => i.Kind == ChunkStreet.Kind.Median).ToList();
            Assert.That(piers, Has.Count.EqualTo(14));
            Assert.That(r.Street.FlyMedians, Is.EqualTo(14));
            foreach (var p in piers)
            {
                Assert.That(p.Why, Is.EqualTo("median"));
                Assert.That(islands.Count(m => Math.Abs(m.X - p.X) < 1e-6 && Math.Abs(m.Z - p.Z) < 1e-6), Is.EqualTo(1), "an island under each");
            }
            foreach (var m in islands) Assert.That(m.Why, Is.EqualTo("lanes 5.60"));
        }

        [Test]
        public void AnIslandSlidesAcrossClearOfABuildingOnTheGround()
        {
            // A small building on the ground (1 m by 0.7 m) stands on the 14 m road, 2.5 m past the pier at x = 90 and
            // 0.5 m off the deck's line: inside the island that would stand there, but clear of the pier. The island
            // slides across until its footprint is clear (P-6; on main it was drawn into the building, as at
            // FP-7710.0-9823.1 and FP-6988.9-10265.8). The other islands do not move.
            var c = Chunk(true, new[] { Under(1400) });
            c.Buildings = new[] { new Building { Id = 70, HeightCm = 300, Outer = new[] { 9250, 25050, 9350, 25050, 9350, 25120, 9250, 25120 } } };
            var r = ChunkRoads.Build(c, keepMarks: true);
            var islands = r.Street.Items.Where(i => i.Kind == ChunkStreet.Kind.Median).OrderBy(i => i.X).ToList();
            Assert.That(r.Street.FlyMedians, Is.EqualTo(14));
            var moved = islands.Single(m => Math.Abs(m.X - 90) < 1e-6);
            Assert.That(moved.Z + ChunkRoads.MedianHalf, Is.LessThanOrEqualTo(250.5), "the island's edge short of the building");
            Assert.That(moved.Why, Is.EqualTo("lanes 4.60"));
            foreach (var m in islands.Where(m => Math.Abs(m.X - 90) > 1e-6)) Assert.That(m.Z, Is.EqualTo(DeckZ).Within(1e-6));
        }

        [Test]
        public void ARoadTooNarrowForAnIslandGetsPortalFrames()
        {
            // A 7 m road would keep only 2.1 m beside an island: each place gets a portal frame instead, its two legs off
            // the road (half width 3.5 m, leg radius 0.6 m, 5 cm margin).
            var r = ChunkRoads.Build(Chunk(true, new[] { Under(700) }), keepMarks: true);
            var legs = Supports(r);
            Assert.That(r.Street.FlyPortals, Is.EqualTo(14));
            Assert.That(r.Street.FlyMedians, Is.EqualTo(0));
            Assert.That(legs, Has.Count.EqualTo(28));
            foreach (var l in legs)
            {
                Assert.That(l.Kind, Is.EqualTo(ChunkStreet.Kind.FlyLeg));
                Assert.That(l.Why, Is.EqualTo("portal"));
                Assert.That(Math.Abs(l.Z - DeckZ), Is.GreaterThanOrEqualTo(3.5 + ChunkRoads.FlyLegRadius), "off the road");
                Assert.That(Math.Abs(l.Z - DeckZ), Is.LessThanOrEqualTo(ChunkRoads.PortalReach));
                Assert.That(l.Top, Is.EqualTo(6 - ChunkRoads.FlyDepth(6) - ChunkRoads.FlyBeamDepth).Within(1e-6), "up to the beam");
            }
        }

        [Test]
        public void TheRailwayUnderTheDeckGetsOffsetPiers()
        {
            // An at-grade track along the deck's line: each pier stands beside it, its edge at least 2.5 m from the
            // track's centre line, within the cap's reach (half width + 2 m), with a cap reaching under the deck.
            var rail = new Rail { Id = 30, YCm = 0, Pts = new[] { 0, 25000, 50000, 25000 } };
            var r = ChunkRoads.Build(Chunk(true, rails: new[] { rail }), keepMarks: true);
            var piers = Supports(r);
            Assert.That(piers, Has.Count.EqualTo(14));
            Assert.That(r.Street.FlyOffsets, Is.EqualTo(14));
            foreach (var p in piers)
            {
                Assert.That(p.Why, Is.EqualTo("offset"));
                Assert.That(Math.Abs(p.Z - DeckZ), Is.GreaterThanOrEqualTo(ChunkRoads.RailClear + ChunkRoads.FlyPierRadius - 1e-9));
                Assert.That(Math.Abs(p.Z - DeckZ), Is.LessThanOrEqualTo(3.5 + ChunkRoads.OffsetReach + 1e-9));
            }
        }

        [Test]
        public void PiersMayStandInWater()
        {
            // A lake under the middle of the deck (x 160 to 290): bridge piers stand in it (owner, 2026-11-04), noted.
            var lake = new Poly { Outer = new[] { 16000, 20000, 29000, 20000, 29000, 30000, 16000, 30000 } };
            var r = ChunkRoads.Build(Chunk(false, water: new[] { lake }), keepMarks: true);
            var piers = Supports(r);
            Assert.That(piers, Has.Count.EqualTo(14));
            Assert.That(piers.Where(p => p.Why == "water").Select(p => p.X), Is.EqualTo(new[] { 180.0, 210, 240, 270 }).Within(1e-6));
            Assert.That(r.Street.FlyInWater, Is.EqualTo(4));
        }

        [Test]
        public void ARoadAcrossTheDeckTooWideToSlideOffMovesThePierOffTheGrid()
        {
            // A 14 m road crosses under the deck at x = 240. The 4 m slide cannot clear it, a portal's legs would stand on
            // it, so the pier slides further, off the grid, to the road's edge (7 m + 0.8 m + 5 cm, in 0.25 m steps):
            // x = 248. The place at 270 is then under 25 m on and is not built; nothing is spanned, and no gap between
            // supports is over 61 m or under 25 m.
            var across = new Road { Id = 21, Cls = "primary", WidthCm = 1400, YCm = 28, Pts = new[] { 24000, 0, 24000, 50000 } };
            var r = ChunkRoads.Build(Chunk(false, new[] { across }), keepMarks: true);
            var piers = Supports(r);
            Debug.Log(string.Join(" ", piers.Select(i => $"{i.X:F2}:{i.Why}")));
            Assert.That(r.Street.FlySpanned, Is.EqualTo(0));
            Assert.That(r.Street.FlySlidFar, Is.EqualTo(1));
            var far = piers.Single(i => i.Why == "slid-far");
            Assert.That(far.X, Is.EqualTo(248).Within(1e-6));
            Assert.That(piers.Select(i => i.X), Has.None.EqualTo(270.0).Within(1e-6));
            for (int k = 1; k < piers.Count; k++)
                Assert.That(piers[k].X - piers[k - 1].X, Is.InRange(ChunkRoads.FlyMinSpan, 61.0));
        }

        [Test]
        public void TwoPlacesSpannedInARowAreFilledBetween()
        {
            // Two 26 m roads cross under the deck at x = 240 and 270, 4 m apart: neither place can slide off its road
            // (13.85 m needed, 12 m the most), so both are spanned, leaving 90 m from 210 to 300. The gap is filled on the
            // strip between the roads, at x = 255, and no gap is then over 60 m on the main axis.
            Road Across(int id, int x) => new Road { Id = id, Cls = "primary", WidthCm = 2600, YCm = 28, Pts = new[] { x, 0, x, 50000 } };
            var r = ChunkRoads.Build(Chunk(false, new[] { Across(21, 24000), Across(23, 27000) }), keepMarks: true);
            var piers = Supports(r);
            Debug.Log(string.Join(" ", piers.Select(i => $"{i.X:F2}:{i.Why}")));
            Assert.That(r.Street.FlySpanned, Is.EqualTo(2));
            Assert.That(r.Street.FlyFilled, Is.EqualTo(1));
            Assert.That(piers.Single(i => i.Why == "fill").X, Is.EqualTo(255).Within(1e-6));
            for (int k = 1; k < piers.Count; k++) Assert.That(piers[k].X - piers[k - 1].X, Is.LessThanOrEqualTo(60 + 1e-9));
        }

        [Test]
        public void AGridLineOnTheWestBorderIsAPlaceWhicheverWayTheDeckRuns()
        {
            // A deck from the chunk's west border (x = 0, a grid line) to x = 400: the place on the border is this chunk's
            // (the south and west edges are owned), drawn east or west. It was once lost, the line at a segment's low end
            // not counted, as the neighbour does not own its east edge either. No support stands within BorderKeep (4 m) of
            // the border, so the pier slides in to x = 4.
            foreach (bool west in new[] { false, true })
            {
                int[] pts = west ? new[] { 40000, 25000, 0, 25000 } : new[] { 0, 25000, 40000, 25000 };
                var c = new DadcChunk
                {
                    Ix = 0, Iy = -1, WidthsResolved = true, Rails = new Rail[0], Buildings = new Building[0],
                    Roads = new[] { new Road { Id = 10, Cls = "primary", WidthCm = 700, YCm = 600, Bridge = true, Pts = pts } },
                };
                var piers = Supports(ChunkRoads.Build(c, keepMarks: true));
                Assert.That(piers[0].X, Is.EqualTo(ChunkRoads.BorderKeep).Within(1e-6), west ? "drawn west" : "drawn east");
                Assert.That(piers[0].Why, Is.EqualTo("slid"));
                Assert.That(piers[1].X, Is.EqualTo(30).Within(1e-6), "the next grid place");
                Assert.That(piers.Select(i => i.X).Distinct().Count(), Is.EqualTo(piers.Count), "each place once");
            }
        }

        [Test]
        public void ADeckThatOnlyRunsAlongTheBorderStandsOnItsGridPlaces()
        {
            // A deck 2 m inside the west border, from z = 50 to 450, with free ends: no deck is cut on that border, so no
            // neighbour's support can come near and the keep-out does not apply. Once every place on it was "at the chunk
            // border" and the deck had no support at all.
            var c = new DadcChunk
            {
                Ix = 0, Iy = -1, WidthsResolved = true, Rails = new Rail[0], Buildings = new Building[0],
                Roads = new[] { new Road { Id = 11, Cls = "primary", WidthCm = 700, YCm = 600, Bridge = true, Pts = new[] { 200, 5000, 200, 45000 } } },
            };
            var r = ChunkRoads.Build(c, keepMarks: true);
            var piers = Supports(r);
            Assert.That(r.Street.FlySpanned, Is.EqualTo(0));
            Assert.That(piers.Count, Is.EqualTo(14), "the lines z = 60 to 450");
            foreach (var p in piers)
            {
                Assert.That(p.X, Is.EqualTo(2).Within(1e-6));
                Assert.That(p.Z % 30, Is.EqualTo(0).Within(1e-6), "on the grid, not slid");
            }
        }

        [Test]
        public void NearAnotherDecksBorderCutTheStripIsKeptOut()
        {
            // The same deck 2 m inside the west border (6 m, z = 240 to 400), and an 11 m deck cut by that border at z = 250.
            // The neighbour may stand a support beside that cut, so within BorderCutReach plus the cut deck's half width
            // (18.5 m) along the border the strip is closed: the place at z = 240 is offset out of it. Further on, the
            // strip is open again.
            var c = new DadcChunk
            {
                Ix = 0, Iy = -1, WidthsResolved = true, Rails = new Rail[0], Buildings = new Building[0],
                Roads = new[]
                {
                    new Road { Id = 12, Cls = "primary", WidthCm = 700, YCm = 600, Bridge = true, Pts = new[] { 200, 24000, 200, 40000 } },
                    new Road { Id = 13, Cls = "primary", WidthCm = 700, YCm = 1100, Bridge = true, Pts = new[] { 0, 25000, 30000, 25000 } },
                },
            };
            var piers = Supports(ChunkRoads.Build(c, keepMarks: true)).Where(i => i.Way == 12).OrderBy(i => i.Z).ToList();
            Assert.That(piers.Count, Is.EqualTo(6));
            Assert.That(piers[0].Z, Is.EqualTo(240).Within(1e-6));
            Assert.That(piers[0].X, Is.GreaterThanOrEqualTo(ChunkRoads.BorderKeep), "out of the strip");
            Assert.That(piers[0].Why, Is.EqualTo("offset"));
            foreach (var p in piers.Skip(1)) Assert.That(p.X, Is.EqualTo(2).Within(1e-6), "z " + p.Z);
        }

        [Test]
        public void AnIslandMayCoverOneSideOfAOneWayRoadOnly()
        {
            // A 7 m road under the deck, its centre line 2.5 m off the deck's. An island on the deck's line covers the
            // road's near edge and keeps 4.6 m on the far side. One-way (the graph edge says so), that is a road narrowed
            // to one lane past the pier: islands. Two-way, one direction would lose its lane: portal frames instead.
            foreach (bool oneWay in new[] { true, false })
            {
                var c = Chunk(true, new[] { new Road { Id = 22, Cls = "primary", WidthCm = 700, YCm = 28, Pts = new[] { 0, 25250, 50000, 25250 } } });
                if (oneWay) c.Edges = new[] { new GraphEdge { Seq = 1, A = 0, B = 1, Way = 22, Cls = "primary", Oneway = 1, Pts = new[] { 0, 25250, 50000, 25250 } } };
                var r = ChunkRoads.Build(c, keepMarks: true);
                Assert.That(r.Street.FlyMedians, Is.EqualTo(oneWay ? 14 : 0), oneWay ? "one-way" : "two-way");
                Assert.That(r.Street.FlyPortals, Is.EqualTo(oneWay ? 0 : 14), oneWay ? "one-way" : "two-way");
                foreach (var m in r.Street.Items.Where(i => i.Kind == ChunkStreet.Kind.Median)) Assert.That(m.Why, Is.EqualTo("lanes 4.60"));
            }
        }

        [Test]
        public void NoTwoSupportsShareAPileCap()
        {
            // Two decks side by side, 2.5 m apart centre to centre: a support stands only where its pile cap stays 0.2 m
            // clear of every other (radius + 0.55 m each, so 2.9 m between two piers), so the second deck's piers slide
            // 1.5 m along it. In the slice the checker judges the same rule (InfraSurveyTests).
            var c = Chunk(false);
            var twin = new Road { Id = 11, Cls = "primary", WidthCm = 700, YCm = 600, Bridge = true, Pts = new[] { 5000, 25250, 45000, 25250 } };
            c.Roads = c.Roads.Append(twin).ToArray();
            var all = Supports(ChunkRoads.Build(c, keepMarks: true));
            for (int i = 0; i < all.Count; i++)
                for (int j = i + 1; j < all.Count; j++)
                {
                    double ri = all[i].Kind == ChunkStreet.Kind.FlyLeg ? ChunkRoads.FlyLegRadius : ChunkRoads.FlyPierRadius;
                    double rj = all[j].Kind == ChunkStreet.Kind.FlyLeg ? ChunkRoads.FlyLegRadius : ChunkRoads.FlyPierRadius;
                    double d = Math.Sqrt((all[i].X - all[j].X) * (all[i].X - all[j].X) + (all[i].Z - all[j].Z) * (all[i].Z - all[j].Z));
                    Assert.That(d, Is.GreaterThanOrEqualTo(ri + rj + 2 * ChunkRoads.CapRim + ChunkRoads.CapGap - 1e-9), $"{all[i].X:F1},{all[i].Z:F1} and {all[j].X:F1},{all[j].Z:F1}");
                }
            Assert.That(all, Has.Count.EqualTo(28));
            Assert.That(all.Count(i => i.Why == "slid"), Is.EqualTo(14));
        }

        [Test]
        public void TheGirderIsAClosedBoxCutAtTheSupports()
        {
            // The structures are closed solids facing out (the girder casts shadows with the front-face-culled shadow
            // pass, K-20). The girder runs the deck's 400 m less a 4 cm joint at each of the 13 supports inside it (the
            // one at the deck's end needs none), in 14 segments.
            var r = ChunkRoads.Build(Chunk(true), keepMarks: true);
            var s = r.Street;
            var p = s.Positions.Take(3 * s.Vertices[0]).ToList();
            var idx = s.Indices.Take(s.IndexCounts[0]).ToList();
            // The loft splits vertices for flat shading, so the edges are matched by position.
            var (volume, open) = StreetTests.Solid(p, idx, weld: true);
            Assert.That(open, Is.EqualTo(0), "no open edge");
            Assert.That(volume, Is.GreaterThan(0), "faces out");
            Assert.That(s.FlySegments, Is.EqualTo(14));
            // The joint is written out, not taken from the builder: a builder with no joint must fail here.
            Assert.That(s.FlyLength, Is.EqualTo(400 - 13 * 2 * 0.02).Within(1e-6), "a 2 cm joint each side of the 13 supports");
            double low = double.MaxValue;
            for (int v = 1; v < p.Count; v += 3) if (p[v] > 1) low = Math.Min(low, p[v]);
            Assert.That(low, Is.EqualTo(6 - ChunkRoads.FlyDepth(6) - ChunkRoads.FlyCapDepth(6)).Within(1e-4), "the lowest point above the ground is a pier cap's base");
        }

        [Test]
        public void EveryFlyoverInTheSliceStandsOnSupports()
        {
            // The slice's 27 elevated vehicle ways (44 pieces across the chunks, 10.05 km). Pinned, with the account:
            // 315 supports: 291 piers and 24 portal frames (48 legs); of the piers, 28 slid along the deck (7 of them off
            // the grid, by up to 12 m, rather than leave two grid places spanned), 7 added to fill a long gap, 20 on a
            // median island (a two-way road keeps a lane each way, else a portal), 29 offset (rail-side, or past a building
            // or a lower deck) and 39 in water; 9 grid places spanned (2 on a crossing road, 1 in a junction, 2 in a
            // building, 2 on a road too narrow for an island where no portal fits, 1 on the railway, 1 at the chunk border).
            // The checker judges each support (InfraSurveyTests); one span is over 61 m on the main axis, listed
            // (1558255166 over two trunk roads either side of a chunk border).
            int decks = 0, supports = 0, piers = 0, portals = 0, slid = 0, far = 0, filled = 0, medians = 0, offsets = 0, water = 0, spanned = 0;
            double length = 0;
            var ways = new HashSet<long>();
            var held = new HashSet<long>();
            foreach (var kv in SidewalkTestsSlice())
            {
                var (c, r) = kv;
                var s = r.Street;
                decks += s.FlyDecks;
                supports += s.FlySupports;
                piers += s.FlyPiers;
                portals += s.FlyPortals;
                slid += s.FlySlid;
                far += s.FlySlidFar;
                filled += s.FlyFilled;
                medians += s.FlyMedians;
                offsets += s.FlyOffsets;
                water += s.FlyInWater;
                spanned += s.FlySpanned;
                length += s.FlyLength;
                foreach (var road in c.Roads.Where(q => q.Bridge && q.YCm >= RoadPalette.ElevatedY * 100 && !RoadPalette.IsFoot(q.Cls))) ways.Add(road.Id);
                foreach (var i in s.Items.Where(i => (i.Kind == ChunkStreet.Kind.FlyPier || i.Kind == ChunkStreet.Kind.FlyLeg) && !i.Skipped)) held.Add(i.Way);
                Assert.That(s.Items.Count(i => i.Kind == ChunkStreet.Kind.FlyPier && i.Skipped), Is.EqualTo(s.FlySpanned), c.Id);
            }
            Debug.Log($"Flyovers in the slice: {ways.Count} ways, {decks} deck pieces, {length:F0} m of girder; {supports} supports: {piers} piers, {portals} portals; " +
                      $"{slid} slid ({far} off the grid), {medians} on a median island, {offsets} offset, {water} in water; {spanned} spanned");
            Assert.That(ways, Has.Count.EqualTo(27));
            Assert.That(held, Is.SupersetOf(ways), "every elevated vehicle way stands on at least one support");
            Assert.That(decks, Is.EqualTo(44));
            Assert.That(length, Is.EqualTo(10047).Within(1));
            Assert.That(supports, Is.EqualTo(315));
            Assert.That(piers, Is.EqualTo(291));
            Assert.That(portals, Is.EqualTo(24));
            Assert.That(slid, Is.EqualTo(28));
            Assert.That(far, Is.EqualTo(7));
            Assert.That(filled, Is.EqualTo(7));
            Assert.That(medians, Is.EqualTo(20));
            Assert.That(offsets, Is.EqualTo(29));
            Assert.That(water, Is.EqualTo(39));
            Assert.That(spanned, Is.EqualTo(9));
        }

        static IEnumerable<(DadcChunk c, ChunkRoads r)> SidewalkTestsSlice()
        {
            SidewalkTests.LoadSlice();
            foreach (var kv in SidewalkTests.built) yield return (SidewalkTests.chunks[kv.Key], kv.Value);
        }
    }
}
