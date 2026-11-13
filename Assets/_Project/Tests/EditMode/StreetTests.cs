// Milestone 2 (Farmgate validation) evidence: street furniture and structures checked against the
// slice data and the meshes as built. Poles, lamps and trees stand off the drawn carriageways and
// outside buildings, and every mapped lamp and tree is either placed or listed with its reason;
// cables hang from the poles and sag; the viaduct deck runs under every metre of the elevated
// tracks with piers to the ground; "Foot over Bridge, Farmgate" has its deck at its height and its
// stairs reach the ground; dividers sit on the road between twins. The kit parts are closed,
// face out and keep to the detailing standard's triangle targets (DETAILING_STANDARDS section 6),
// and the far level of detail is lighter than the near one. Facades keep each footprint and top.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dad.World;
using NUnit.Framework;
using UnityEngine;

namespace Dad.Tests
{
    public class StreetTests
    {
        static IEnumerable<(DadcChunk c, ChunkRoads r)> Slice()
        {
            SidewalkTests.LoadSlice();
            foreach (var kv in SidewalkTests.built) yield return (SidewalkTests.chunks[kv.Key], kv.Value);
        }

        static string Where(DadcChunk c, double x, double z) => $"{c.Id} ({c.CornerEastCm / 100.0 + x:F1}, {c.CornerNorthCm / 100.0 + z:F1})";

        /// <summary>Is (x, z) on a road-mesh carriageway face (asphalt or a marking at road level) that is not a footway or pedestrian street's?</summary>
        static bool OnAsphalt(DadcChunk c, SidewalkTests.Probe probe, double x, double z) =>
            probe.Near(x, z, 0.28).Any(h => (h.m == WorldMaterial.Asphalt || h.m == WorldMaterial.Marking) && Math.Abs(h.y - 0.28) < 0.05) && !OnFootway(c, x, z);

        /// <summary>Within a footway, path or pedestrian street's mapped (or 2 m) width: these are drawn in asphalt too, and a pole or tree may stand on one.</summary>
        static bool OnFootway(DadcChunk c, double x, double z)
        {
            foreach (var r in c.Roads)
            {
                if (!RoadPalette.IsFoot(r.Cls) || RoadPalette.IsFootSpan(r)) continue;
                double half = Math.Max(r.WidthCm, 200) / 200.0 + 0.1;
                for (int i = 0; i + 3 < r.Pts.Length; i += 2)
                {
                    double ax = r.Pts[i] / 100.0, az = r.Pts[i + 1] / 100.0, ux = r.Pts[i + 2] / 100.0 - ax, uz = r.Pts[i + 3] / 100.0 - az, ll = ux * ux + uz * uz;
                    double t = ll > 0 ? Math.Clamp(((x - ax) * ux + (z - az) * uz) / ll, 0, 1) : 0;
                    if (Math.Pow(ax + ux * t - x, 2) + Math.Pow(az + uz * t - z, 2) < half * half) return true;
                }
            }
            return false;
        }

        static bool InBuilding(DadcChunk c, double x, double z)
        {
            foreach (var b in c.Buildings)
            {
                if (b.Outer == null || BuildingRules.Place(b).Base > 2) continue; // a raised station has the road under it
                bool inside = false;
                int n = b.Outer.Length / 2;
                for (int i = 0, j = n - 1; i < n; j = i, i++)
                {
                    double xi = b.Outer[2 * i] / 100.0, zi = b.Outer[2 * i + 1] / 100.0, xj = b.Outer[2 * j] / 100.0, zj = b.Outer[2 * j + 1] / 100.0;
                    if ((zi > z) != (zj > z) && x < (xj - xi) * (z - zi) / (zj - zi) + xi) inside = !inside;
                }
                if (inside) return true;
            }
            return false;
        }

        [Test]
        public void PolesLampsAndTreesStandClear()
        {
            var problems = new List<string>();
            int poles = 0, lamps = 0, trees = 0;
            foreach (var (c, r) in Slice())
            {
                var probe = new SidewalkTests.Probe(r);
                foreach (var it in r.Street.Items.Where(i => !i.Skipped && (i.Kind == ChunkStreet.Kind.Pole || i.Kind == ChunkStreet.Kind.Lamp || i.Kind == ChunkStreet.Kind.Tree)))
                {
                    if (it.Kind == ChunkStreet.Kind.Pole) poles++;
                    else if (it.Kind == ChunkStreet.Kind.Lamp) lamps++;
                    else trees++;
                    if (OnAsphalt(c, probe, it.X, it.Z)) problems.Add($"{it.Kind} on a carriageway at {Where(c, it.X, it.Z)}");
                    if (InBuilding(c, it.X, it.Z)) problems.Add($"{it.Kind} in a building at {Where(c, it.X, it.Z)}");
                }
            }
            Debug.Log($"{poles} poles, {lamps} lamps, {trees} trees placed in the slice");
            Assert.That(problems, Is.Empty, string.Join("\n", problems.Take(30)));
            Assert.That(poles, Is.GreaterThan(500));
        }

        [Test]
        public void EveryMappedLampAndTreeIsPlacedOrListed()
        {
            int mappedLamps = 0, mappedTrees = 0, lamps = 0, trees = 0, moved = 0;
            var listed = new List<string>();
            foreach (var (c, r) in Slice())
            {
                mappedLamps += c.Points.Count(p => p.Kind == "lamp");
                mappedTrees += c.Trees.Length;
                var s = r.Street;
                lamps += s.Lamps;
                trees += s.Trees;
                moved += s.LampsMoved;
                Assert.That(s.Lamps + s.LampsOnRoad + s.LampsInBuilding, Is.EqualTo(c.Points.Count(p => p.Kind == "lamp")), c.Id + " lamps");
                Assert.That(s.Trees + s.TreesOnRoad + s.TreesInBuilding, Is.EqualTo(c.Trees.Length), c.Id + " trees");
                foreach (var it in s.Items.Where(i => i.Skipped && (i.Kind == ChunkStreet.Kind.Lamp || i.Kind == ChunkStreet.Kind.Tree)))
                    listed.Add($"{it.Kind} at {Where(c, it.X, it.Z)}: {it.Why}");
                // A tree stands exactly where it is mapped; a lamp at most LampMove from its point, and only straight out from a road.
                foreach (var t in c.Trees)
                {
                    double x = t.East / 100.0, z = t.North / 100.0;
                    Assert.That(s.Items.Any(i => i.Kind == ChunkStreet.Kind.Tree && Math.Abs(i.X - x) < 1e-9 && Math.Abs(i.Z - z) < 1e-9), $"tree at {Where(c, x, z)} neither placed nor listed");
                }
                Assert.That(s.Items.Count(i => i.Why == ChunkRoads.RuleLamp), Is.EqualTo(s.RoadLamps), c.Id + " rule lamps are labelled");
                foreach (var l in s.Items.Where(i => i.Kind == ChunkStreet.Kind.Lamp && !i.Skipped && i.Why != ChunkRoads.RuleLamp))
                {
                    var p = c.Points.First(q => q.Id == l.Way);
                    double d = Math.Sqrt(Math.Pow(p.East / 100.0 - l.X, 2) + Math.Pow(p.North / 100.0 - l.Z, 2));
                    Assert.That(d, Is.LessThanOrEqualTo(l.Why == null ? 1e-9 : ChunkRoads.LampMove + 1e-9), $"lamp {l.Way} {l.Why}");
                }
            }
            Debug.Log($"lamps: {lamps} of {mappedLamps} placed ({moved} moved to the kerb); trees: {trees} of {mappedTrees}\n" + string.Join("\n", listed));
            Assert.That(lamps, Is.GreaterThanOrEqualTo(mappedLamps * 0.85));
            Assert.That(trees, Is.GreaterThanOrEqualTo(mappedTrees * 0.9));
        }

        [Test]
        public void ALampIsMovedToTheKerbAtMostLampMove()
        {
            // Two lamps mapped 2 m off the centre lines of two mapped two-way primaries: the 10 m road's
            // goes out 3.45 m to behind its kerb; the 20 m road's would go 8.45 m, so it is listed.
            var c = new DadcChunk
            {
                Ix = 0, Iy = -1,
                Roads = new[]
                {
                    new Road { Id = 1, Cls = "primary", WidthCm = 1000, YCm = 28, Pts = new[] { 5000, 10000, 45000, 10000 } },
                    new Road { Id = 2, Cls = "primary", WidthCm = 2000, YCm = 28, Pts = new[] { 5000, 30000, 45000, 30000 } },
                },
                Points = new[]
                {
                    new PointFeature { Id = 11, Kind = "lamp", East = 25000, North = 10200 },
                    new PointFeature { Id = 12, Kind = "lamp", East = 25000, North = 30200 },
                },
                Trees = new Dad.World.Tree[0], Nodes = new GraphNode[0], Edges = new GraphEdge[0],
            };
            var s = ChunkRoads.Build(c, keepMarks: true).Street;
            var near = s.Items.Single(i => i.Kind == ChunkStreet.Kind.Lamp && i.Way == 11);
            var far = s.Items.Single(i => i.Kind == ChunkStreet.Kind.Lamp && i.Way == 12);
            Assert.IsFalse(near.Skipped);
            Assert.AreEqual(100 + 5 + ChunkRoads.PoleBehindKerb, near.Z, 1e-6);
            Assert.IsTrue(far.Skipped);
            Assert.AreEqual("on a carriageway", far.Why);
            Assert.AreEqual(1, s.LampsMoved);
            Assert.AreEqual(1, s.LampsOnRoad);
        }

        [Test]
        public void CablesHangFromThePolesAndSag()
        {
            int full = 0, half = 0;
            foreach (var (c, r) in Slice())
            {
                var poles = r.Street.Items.Where(i => i.Kind == ChunkStreet.Kind.Pole && !i.Skipped).ToList();
                foreach (var w in r.Street.Wires)
                {
                    // The first 11 kV conductor: it leaves an insulator on a pole, 0.8 m across from the shaft.
                    Assert.That(w.Ay, Is.EqualTo(StreetKit.HvArm + 0.29).Within(1e-4), "anchor height");
                    Assert.That(poles.Any(p => Math.Abs(Math.Sqrt(Math.Pow(p.X - w.Ax, 2) + Math.Pow(p.Z - w.Az, 2)) - 0.8) < 0.01), $"no pole under the wire start at {Where(c, w.Ax, w.Az)}");
                    Assert.That(w.Low, Is.GreaterThan(8.0), "11 kV conductors stay above 8 m");
                    if (w.Half)
                    {
                        half++;
                        // It ends at the lowest point, as far below its pole as a full span sags at its middle (0.25 m or more).
                        Assert.That(w.By, Is.EqualTo(w.Low).Within(1e-6), "a half span is level at the border");
                        Assert.That(w.By, Is.LessThanOrEqualTo(w.Ay - 0.25 + 1e-6), "a half span sags to the border");
                        Assert.That(Math.Min(Math.Min(w.Bx, 500 - w.Bx), Math.Min(w.Bz, 500 - w.Bz)), Is.LessThan(10), $"half span ends beside the road where it crosses the border, at {Where(c, w.Bx, w.Bz)}");
                    }
                    else
                    {
                        full++;
                        // A parabola's lowest point is at or below its middle, which hangs 0.25 m or more under the chord.
                        Assert.That(w.Low, Is.LessThanOrEqualTo((w.Ay + w.By) / 2 - 0.25 + 1e-6), "a cable sags");
                        Assert.That(poles.Any(p => Math.Abs(Math.Sqrt(Math.Pow(p.X - w.Bx, 2) + Math.Pow(p.Z - w.Bz, 2)) - 0.8) < 0.01), $"no pole under the wire end at {Where(c, w.Bx, w.Bz)}");
                    }
                }
            }
            Debug.Log($"{full} spans, {half} half spans to a chunk border");
            Assert.That(full, Is.GreaterThan(300));
        }

        /// <summary>Up-facing triangles of one range of a street mesh, for height probes.</summary>
        static List<(double x0, double z0, double x1, double z1, double x2, double z2, double y)> UpFaces(ChunkStreet s, int range)
        {
            var list = new List<(double, double, double, double, double, double, double)>();
            var p = s.Positions;
            int v0 = s.FirstVertex[range];
            for (int t = s.FirstIndex[range]; t < s.FirstIndex[range] + s.IndexCounts[range]; t += 3)
            {
                int a = 3 * (v0 + s.Indices[t]), b = 3 * (v0 + s.Indices[t + 1]), c = 3 * (v0 + s.Indices[t + 2]);
                double ux = p[b] - p[a], uy = p[b + 1] - p[a + 1], uz = p[b + 2] - p[a + 2], vx = p[c] - p[a], vy = p[c + 1] - p[a + 1], vz = p[c + 2] - p[a + 2];
                double ny = uz * vx - ux * vz, len = Math.Sqrt(Math.Pow(uy * vz - uz * vy, 2) + ny * ny + Math.Pow(ux * vy - uy * vx, 2));
                if (len < 1e-9 || ny / len < 0.9) continue;
                list.Add((p[a], p[a + 2], p[b], p[b + 2], p[c], p[c + 2], (p[a + 1] + p[b + 1] + p[c + 1]) / 3));
            }
            return list;
        }

        static bool Covered(List<(double x0, double z0, double x1, double z1, double x2, double z2, double y)> faces, double x, double z, double lo, double hi)
        {
            foreach (var f in faces)
            {
                if (f.y < lo || f.y > hi) continue;
                double d1 = (f.x1 - f.x0) * (z - f.z0) - (f.z1 - f.z0) * (x - f.x0), d2 = (f.x2 - f.x1) * (z - f.z1) - (f.z2 - f.z1) * (x - f.x1), d3 = (f.x0 - f.x2) * (z - f.z2) - (f.z0 - f.z2) * (x - f.x2);
                if ((d1 >= -1e-6 && d2 >= -1e-6 && d3 >= -1e-6) || (d1 <= 1e-6 && d2 <= 1e-6 && d3 <= 1e-6)) return true;
            }
            return false;
        }

        /// <summary>
        /// Up faces of the viaduct in this chunk and in each neighbour that has an elevated track, in this chunk's
        /// metres: since P-2 a deck is built by the chunk its centre line is in, so the deck over a track near a border
        /// can be the neighbour's (the Farmgate twin over the c_7_-30/-31 border).
        /// </summary>
        static List<(double x0, double z0, double x1, double z1, double x2, double z2, double y)> UpFacesAround(DadcChunk c, ChunkRoads r)
        {
            var faces = UpFaces(r.Street, 0);
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    var n = Neighbour(c.Ix + dx, c.Iy + dy);
                    if (n == null) continue;
                    double ox = dx * DadcChunk.ChunkCm / 100.0, oz = -dy * DadcChunk.ChunkCm / 100.0;
                    foreach (var f in UpFaces(n.Street, 0))
                        faces.Add((f.x0 + ox, f.z0 + oz, f.x1 + ox, f.z1 + oz, f.x2 + ox, f.z2 + oz, f.y));
                }
            return faces;
        }

        static readonly Dictionary<(int, int), ChunkRoads> neighbours = new Dictionary<(int, int), ChunkRoads>();

        /// <summary>A chunk's build: the slice's own, or a ring chunk's built here; null if it has no elevated track.</summary>
        static ChunkRoads Neighbour(int ix, int iy)
        {
            if (SidewalkTests.built.TryGetValue((ix, iy), out var b)) return b;
            if (neighbours.TryGetValue((ix, iy), out b)) return b;
            var f = Path.Combine(SidewalkTests.ChunkDir, $"c_{ix}_{iy}.dadc");
            if (File.Exists(f))
            {
                var c = Dadc.Load(File.ReadAllBytes(f));
                if (c.Rails != null && c.Rails.Any(x => x.YCm >= ChunkRoads.ViaductMinY * 100))
                {
                    NeighbourRails.Attach(c, SidewalkTests.ChunkDir);
                    b = ChunkRoads.Build(c, keepMarks: true);
                }
            }
            return neighbours[(ix, iy)] = b;
        }

        [Test]
        public void ViaductCarriesEveryElevatedTrackOnPiers()
        {
            int samples = 0, covered = 0, piers = 0, onRoad = 0, length = 0, spanned = 0, inBuilding = 0, underRaised = 0;
            var misses = new List<string>();
            foreach (var (c, r) in Slice())
            {
                var faces = UpFacesAround(c, r);
                foreach (var rail in c.Rails.Where(x => x.YCm >= ChunkRoads.ViaductMinY * 100))
                {
                    double y = rail.YCm / 100.0;
                    for (int i = 0; i + 3 < rail.Pts.Length; i += 2)
                    {
                        double ax = rail.Pts[i] / 100.0, az = rail.Pts[i + 1] / 100.0, bx = rail.Pts[i + 2] / 100.0, bz = rail.Pts[i + 3] / 100.0;
                        int n = Math.Max(1, (int)(Math.Sqrt((bx - ax) * (bx - ax) + (bz - az) * (bz - az)) / 2));
                        for (int k = 0; k < n; k++)
                        {
                            double x = ax + (bx - ax) * (k + 0.5) / n, z = az + (bz - az) * (k + 0.5) / n;
                            samples++;
                            // The rail head, the plinth or the deck top under the track (this chunk's deck or a neighbour's).
                            if (Covered(faces, x, z, y - ChunkRoads.DeckDrop - 0.01, y + 0.01)) covered++;
                            else misses.Add(Where(c, x, z));
                        }
                    }
                }
                foreach (var p in r.Street.Items.Where(i => i.Kind == ChunkStreet.Kind.Pier && !i.Skipped))
                {
                    Assert.That(p.Top, Is.GreaterThan(5), "a pier reaches up to its deck");
                    if (p.Why != null && p.Why.StartsWith("on a carriageway", StringComparison.Ordinal)) onRoad++;
                }
                piers += r.Street.Piers;
                inBuilding += r.Street.PiersInBuilding;
                underRaised += r.Street.PiersUnderRaised;
                // A pier is in at most one of the two (K-37 P-3).
                Assert.That(r.Street.Items.Count(i => i.Kind == ChunkStreet.Kind.Pier && !i.Skipped && (i.Why == "in a building" || i.Why == "under a raised building")),
                    Is.LessThanOrEqualTo(r.Street.PiersInBuilding + r.Street.PiersUnderRaised));
                spanned += r.Street.PiersInJunction + r.Street.PiersOverRoad;
                // Every grid place left out is in a junction or on a crossing road, and is counted (P-2).
                Assert.That(r.Street.Items.Count(i => i.Kind == ChunkStreet.Kind.Pier && i.Skipped), Is.EqualTo(r.Street.PiersInJunction + r.Street.PiersOverRoad));
                length += r.Street.ViaductLength;
            }
            Debug.Log($"elevated track sampled at {samples} points, {covered} on the viaduct; {length} m of deck, {piers} piers, {onRoad} with a foot on a drawn carriageway (K-37), {inBuilding} in a building on the ground and {underRaised} under a raised building (a station body; counted apart, K-37 P-3), {spanned} grid places in a junction or on a crossing road left out (spanned)\nmisses: {string.Join(", ", misses.Take(20))}");
            Assert.That(samples, Is.GreaterThan(500));
            Assert.That(covered, Is.EqualTo(samples));
            Assert.That(piers, Is.GreaterThanOrEqualTo(length / (2 * ChunkRoads.PierGrid)));
            // K-37 P-3, measured 2026-11-05: of the 13 the builder counted "in a building" before, 2 stand in a building on
            // the ground and 11 rise into a raised one.
            Assert.That(inBuilding, Is.EqualTo(2));
            Assert.That(underRaised, Is.EqualTo(11));
        }

        [Test]
        public void ViaductPiersFollowTheP2Rules()
        {
            // K-37 P-2 (approved 2026-11-04), in the slice: one pier per place across chunk borders (no two piers
            // closer than the twin limit, wherever they are built), no two spans in a row, and the slides and spans
            // counted where the builder made them.
            var all = new List<(double e, double n, string chunk)>();
            int slid = 0, overRoad = 0, kept = 0;
            foreach (var (c, r) in Slice())
            {
                double ce = c.CornerEastCm / 100.0, cn = c.CornerNorthCm / 100.0;
                foreach (var p in r.Street.Items.Where(i => i.Kind == ChunkStreet.Kind.Pier && !i.Skipped))
                {
                    // A pier on the south or west border is this chunk's; ownership snaps within 5 cm of a border, so
                    // such a pier can be a rounding error outside it (seen: -6.5e-13 m).
                    Assert.That(p.X, Is.InRange(-ChunkRoads.OwnSnap, DadcChunk.ChunkCm / 100.0), "a chunk builds only its own piers");
                    Assert.That(p.Z, Is.InRange(-ChunkRoads.OwnSnap, DadcChunk.ChunkCm / 100.0), "a chunk builds only its own piers");
                    all.Add((ce + p.X, cn + p.Z, c.Id));
                }
                slid += r.Street.PiersSlid;
                overRoad += r.Street.PiersOverRoad;
                kept += r.Street.PiersKept;
            }
            var close = new List<string>();
            for (int i = 0; i < all.Count; i++)
                for (int j = i + 1; j < all.Count; j++)
                {
                    double d = Math.Sqrt(Math.Pow(all[i].e - all[j].e, 2) + Math.Pow(all[i].n - all[j].n, 2));
                    if (d < ChunkRoads.TwinMax) close.Add($"{all[i].chunk} ({all[i].e:F1}, {all[i].n:F1}) and {all[j].chunk} ({all[j].e:F1}, {all[j].n:F1}): {d:F2} m");
                }
            Debug.Log($"{all.Count} piers; slid {slid}, spanned over a crossing road {overRoad}, kept for want of a second span {kept}");
            Assert.That(close, Is.Empty, string.Join("\n", close));
            Assert.That(slid, Is.EqualTo(2), "K37-29 and K37-46 slide along the deck off their crossing roads");
            Assert.That(overRoad, Is.EqualTo(1), "K37-16 over trunk_link 223100550 is spanned");
            Assert.That(kept, Is.EqualTo(0));
        }

        [Test]
        public void FarmgateFootbridgeHasItsDeckAndStairs()
        {
            SidewalkTests.LoadSlice();
            var c = SidewalkTests.chunks[(8, -30)];
            var s = SidewalkTests.built[(8, -30)].Street;
            var deck = c.Roads.Single(r => r.Id == 223101282);
            Assert.That(deck.Name, Is.EqualTo("Foot over Bridge, Farmgate"));
            var faces = UpFaces(s, 0);
            double y = deck.YCm / 100.0;
            for (int i = 0; i + 3 < deck.Pts.Length; i += 2)
                for (int k = 0; k <= 10; k++)
                {
                    double x = (deck.Pts[i] + (deck.Pts[i + 2] - deck.Pts[i]) * k / 10.0) / 100, z = (deck.Pts[i + 1] + (deck.Pts[i + 3] - deck.Pts[i + 1]) * k / 10.0) / 100;
                    Assert.That(Covered(faces, x, z, y - 0.01, y + 0.01), $"deck missing at {Where(c, x, z)}");
                }
            foreach (long id in new long[] { 1561039834, 1561039835, 1561039836, 1561039837 })
            {
                var top = s.Items.Single(i => i.Kind == ChunkStreet.Kind.StairTop && i.Way == id);
                var foot = s.Items.Single(i => i.Kind == ChunkStreet.Kind.StairFoot && i.Way == id);
                Assert.That(top.Why, Is.Null, $"stair {id} meets the deck");
                Assert.That(top.Y, Is.EqualTo(y).Within(1e-9));
                Assert.That(foot.Y, Is.EqualTo(ChunkRoads.StairBase).Within(1e-9));
            }
            Assert.That(s.Items.Count(i => i.Kind == ChunkStreet.Kind.Column && !i.Skipped && i.Way == 223101282), Is.GreaterThanOrEqualTo(1));
            int steps = 0, stairs = 0;
            foreach (var (cc, r) in Slice())
            {
                steps += cc.Roads.Count(x => RoadPalette.IsFootSpan(x) && x.Cls == "steps");
                stairs += r.Street.Stairs;
            }
            Assert.That(stairs, Is.EqualTo(steps), "every elevated steps way is a stair flight");
        }

        [Test]
        public void DividersStandOnTheRoadBetweenTwins()
        {
            int pieces = 0;
            var problems = new List<string>();
            foreach (var (c, r) in Slice())
            {
                var probe = new SidewalkTests.Probe(r);
                foreach (var d in r.Street.Items.Where(i => i.Kind == ChunkStreet.Kind.Divider))
                {
                    pieces++;
                    // On the road or a paved median, at road level, never over a gap: it often stands on the seam where the twin carriageways meet,
                    // so at least two of four points a quarter metre around it (its base is 0.6 m wide) are on a face at its height, or up to
                    // 0.05 m below it, where its base reaches (a broken slab on a median band is SlabSink lower).
                    int on = 0;
                    foreach (var (ox, oz) in new[] { (0.25, 0.0), (-0.25, 0.0), (0.0, 0.25), (0.0, -0.25) })
                        if (probe.Near(d.X + ox, d.Z + oz, d.Y).Any(h => h.y > d.Y - 0.05 && h.y < d.Y + 0.02)) on++;
                    if (on < 2) problems.Add($"divider over no road at {Where(c, d.X, d.Z)}, y {d.Y:F2}; faces " + string.Join(" ", probe.At(d.X, d.Z).Select(h => $"{h.m}@{h.y:F2}")));
                    if (InBuilding(c, d.X, d.Z)) problems.Add($"divider in a building at {Where(c, d.X, d.Z)}");
                }
            }
            Debug.Log($"{pieces} divider pieces ({pieces * ChunkRoads.DividerStep / 1000.0:F1} km)");
            Assert.That(problems, Is.Empty, string.Join("\n", problems.Take(30)));
            Assert.That(pieces, Is.GreaterThan(100));
        }

        [Test]
        public void RangesCoverTheMeshAndIndicesStayInside()
        {
            foreach (var (c, r) in Slice())
            {
                var s = r.Street;
                int v = 0, i = 0;
                for (int k = 0; k < ChunkStreet.Ranges; k++)
                {
                    Assert.That(s.FirstVertex[k], Is.EqualTo(v), $"{c.Id} range {k} vertices follow on");
                    Assert.That(s.FirstIndex[k], Is.EqualTo(i), $"{c.Id} range {k} indices follow on");
                    for (int t = s.FirstIndex[k]; t < s.FirstIndex[k] + s.IndexCounts[k]; t++)
                        Assert.That(s.Indices[t], Is.InRange(0, s.Vertices[k] - 1), $"{c.Id} range {k}");
                    v += s.Vertices[k];
                    i += s.IndexCounts[k];
                }
                Assert.That(v, Is.EqualTo(s.VertexCount));
                Assert.That(i, Is.EqualTo(s.IndexCount));
            }
        }

        // ---- Kit parts ----

        static (List<float> p, List<int> i, KitWriter w) Writer()
        {
            var p = new List<float>();
            var i = new List<int>();
            return (p, i, new KitWriter(p, new List<uint>(), i));
        }

        /// <summary>Signed volume of a triangle list (positive when it faces out) and the edges not shared by a reversed twin.</summary>
        internal static (double volume, int open) Solid(List<float> p, List<int> idx) => Solid(p, idx, false);

        /// <summary>As above; with <paramref name="weld"/>, vertices at the same position (to 0.1 mm) count as one, for meshes
        /// that split vertices for flat shading, such as a loft.</summary>
        internal static (double volume, int open) Solid(List<float> p, List<int> idx, bool weld)
        {
            if (weld)
            {
                var first = new Dictionary<(long, long, long), int>();
                var map = new int[p.Count / 3];
                for (int v = 0; v < map.Length; v++)
                {
                    var key = ((long)Math.Round(p[3 * v] * 1e4), (long)Math.Round(p[3 * v + 1] * 1e4), (long)Math.Round(p[3 * v + 2] * 1e4));
                    if (!first.TryGetValue(key, out int id)) first[key] = id = v;
                    map[v] = id;
                }
                idx = idx.Select(i => map[i]).ToList();
            }
            double vol = 0;
            var edges = new Dictionary<(int, int), int>();
            for (int t = 0; t < idx.Count; t += 3)
            {
                int a = idx[t], b = idx[t + 1], c = idx[t + 2];
                double ax = p[3 * a], ay = p[3 * a + 1], az = p[3 * a + 2], bx = p[3 * b], by = p[3 * b + 1], bz = p[3 * b + 2], cx = p[3 * c], cy = p[3 * c + 1], cz = p[3 * c + 2];
                vol += (ax * (by * cz - bz * cy) + ay * (bz * cx - bx * cz) + az * (bx * cy - by * cx)) / 6.0;
                foreach (var (x, y) in new[] { (a, b), (b, c), (c, a) })
                {
                    if (edges.TryGetValue((y, x), out int n) && n > 0) { if (--edges[(y, x)] == 0) edges.Remove((y, x)); }
                    else edges[(x, y)] = edges.TryGetValue((x, y), out int m) ? m + 1 : 1;
                }
            }
            return (vol, edges.Values.Sum());
        }

        [Test]
        public void KitSolidsAreClosedAndFaceOut()
        {
            var (p, i, w) = Writer();
            Span<double> k = stackalloc double[] { 0, 0, 0, 2, 0, 0, 2, 0, 1, 0, 0, 1, 0, 3, 0, 2, 3, 0, 2, 3, 1, 0, 3, 1 };
            w.Hex(k, 0);
            var (vol, open) = Solid(p, i);
            Assert.That(vol, Is.EqualTo(6).Within(1e-4));
            Assert.That(open, Is.EqualTo(0));
            (p, i, w) = Writer();
            w.Tube(1, 2, 3, 4, 6, 3, 0.5, 0.3, 8, 0);
            (vol, open) = Solid(p, i);
            Assert.That(vol, Is.GreaterThan(0));
            Assert.That(open, Is.EqualTo(0));
            (p, i, w) = Writer();
            w.Blob(5, 5, 5, 1, 0.6, 0.8, 0.7, 0.3, 0);
            (vol, open) = Solid(p, i);
            Assert.That(vol, Is.EqualTo(4.0 / 3 * 1 * 0.6 * 0.8).Within(1e-4), "an octahedron's volume");
            Assert.That(open, Is.EqualTo(0));
            (p, i, w) = Writer();
            Span<double> u = stackalloc double[] { 1, 1, -1, 1, -1, -1, 0, -2, 1, -1 };
            Span<uint> colours = stackalloc uint[] { 0, 0, 0, 0, 0 };
            var path = new List<double> { 0, 0, 0, 5, 0, 1, 10, 0, 0 };
            var left = new List<double>();
            for (int n = 0; n < 3; n++) { left.Add(0); left.Add(1); }
            w.Loft(u, colours, path, left, true, true, 0, new List<double>(), new List<int>());
            (vol, open) = Solid(p, i);
            Assert.That(vol, Is.GreaterThan(0), "a non-convex loft faces out");
        }

        [Test]
        public void KitPartsKeepTheirTriangleTargetsAndSizes()
        {
            int Tris(Action<KitWriter> draw)
            {
                var (_, i, w) = Writer();
                draw(w);
                return i.Count / 3;
            }
            (List<float> p, int tris) Built(Action<KitWriter> draw)
            {
                var (p, i, w) = Writer();
                draw(w);
                return (p, i.Count / 3);
            }
            int poleNear = Tris(w => StreetKit.UtilityPole(w, 0, 0, 0, 1, 0, -1, true, true, 0)), poleFar = Tris(w => StreetKit.UtilityPole(w, 0, 0, 0, 1, 0, -1, true, true, 1));
            int lampNear = Tris(w => StreetKit.StreetLight(w, 0, 0, 0, 1, 0, 0)), lampFar = Tris(w => StreetKit.StreetLight(w, 0, 0, 0, 1, 0, 1));
            Debug.Log($"pole {poleNear} / {poleFar}, street light {lampNear} / {lampFar}");
            Assert.That(poleNear, Is.LessThanOrEqualTo(4000));
            Assert.That(lampNear, Is.LessThanOrEqualTo(1500));
            Assert.That(poleFar, Is.LessThanOrEqualTo(poleNear / 4));
            Assert.That(lampFar, Is.LessThanOrEqualTo(lampNear / 2));
            for (int form = 0; form < StreetKit.TreeForms; form++)
            {
                var (p, near) = Built(w => StreetKit.Tree(w, 0, 0, 0, 12, form, 42, 0));
                int far = Tris(w => StreetKit.Tree(w, 0, 0, 0, 12, form, 42, 1));
                Debug.Log($"tree form {form}: {near} / {far}");
                Assert.That(near, Is.LessThanOrEqualTo(20000));
                Assert.That(far, Is.LessThanOrEqualTo(near / 4));
                double top = 0;
                for (int v = 1; v < p.Count; v += 3) top = Math.Max(top, p[v]);
                Assert.That(top, Is.InRange(12 * 0.8, 12 * 1.15), $"tree form {form} is about its height");
            }
            var (pp, _) = Built(w => StreetKit.UtilityPole(w, 0, 0, 0, 1, 0, -1, false, false, 0));
            double poleTop = 0;
            for (int v = 1; v < pp.Count; v += 3) poleTop = Math.Max(poleTop, pp[v]);
            Assert.That(poleTop, Is.EqualTo(StreetKit.PoleHeight).Within(0.01));
        }

        [Test]
        public void StreetLodPicksATierWithHysteresis()
        {
            Assert.That(StreetLod.Tier(0, StreetLod.Hidden), Is.EqualTo(StreetLod.Near));
            Assert.That(StreetLod.Tier(149, StreetLod.Far), Is.EqualTo(StreetLod.Near));
            Assert.That(StreetLod.Tier(155, StreetLod.Far), Is.EqualTo(StreetLod.Far));
            Assert.That(StreetLod.Tier(155, StreetLod.Near), Is.EqualTo(StreetLod.Near), "near holds until 10 m past the line");
            Assert.That(StreetLod.Tier(161, StreetLod.Near), Is.EqualTo(StreetLod.Far));
            Assert.That(StreetLod.Tier(455, StreetLod.Far), Is.EqualTo(StreetLod.Far));
            Assert.That(StreetLod.Tier(455, StreetLod.Hidden), Is.EqualTo(StreetLod.Hidden));
            Assert.That(StreetLod.Tier(461, StreetLod.Far), Is.EqualTo(StreetLod.Hidden));
            Assert.That(StreetLod.ToCell(10, 10, 0, 0), Is.EqualTo(0));
            Assert.That(StreetLod.ToCell(-3, 129, 0, 0), Is.EqualTo(5).Within(1e-9));
        }

        // ---- Facades ----

        [Test]
        public void FacadesKeepFootprintAndTop()
        {
            int facades = 0, windows = 0, shops = 0;
            foreach (var key in new[] { (7, -30), (8, -30) })
            {
                SidewalkTests.LoadSlice();
                var c = SidewalkTests.chunks[key];
                var b = ChunkBuildings.Build(c);
                facades += b.Facades;
                windows += b.Windows;
                shops += b.Shops;
                for (int k = 0; k < c.Buildings.Length; k++)
                {
                    var pl = b.Placements[k];
                    if (b.VertexCounts[k] == 0 || !ChunkBuildings.Facaded(c, c.Buildings[k], pl)) continue;
                    var o = c.Buildings[k].Outer;
                    double top = double.MinValue;
                    for (int v = b.FirstVertex[k]; v < b.FirstVertex[k] + b.VertexCounts[k]; v++)
                    {
                        double x = b.Positions[3 * v], y = b.Positions[3 * v + 1], z = b.Positions[3 * v + 2];
                        top = Math.Max(top, y);
                        Assert.That(y, Is.InRange(-1e-4, pl.Top + 1e-4), $"building {c.Buildings[k].Id} stays below its top");
                        // Within the footprint, or at most an awning's reach (0.9 m) outside it.
                        double best = double.MaxValue;
                        for (int i = 0, j = o.Length / 2 - 1; i < o.Length / 2; j = i, i++)
                        {
                            double ax = o[2 * j] / 100.0, az = o[2 * j + 1] / 100.0, ux = o[2 * i] / 100.0 - ax, uz = o[2 * i + 1] / 100.0 - az, ll = ux * ux + uz * uz;
                            double t = ll > 0 ? Math.Clamp(((x - ax) * ux + (z - az) * uz) / ll, 0, 1) : 0;
                            best = Math.Min(best, Math.Sqrt(Math.Pow(ax + ux * t - x, 2) + Math.Pow(az + uz * t - z, 2)));
                        }
                        Assert.That(best, Is.LessThanOrEqualTo(0.95), $"building {c.Buildings[k].Id} keeps to its footprint");
                    }
                    Assert.That(top, Is.EqualTo(pl.Top).Within(1e-3), $"building {c.Buildings[k].Id} reaches its top (the parapet)");
                }
            }
            Debug.Log($"{facades} facades, {windows} windows, {shops} shops");
            Assert.That(facades, Is.GreaterThanOrEqualTo(70));
            Assert.That(shops, Is.GreaterThan(50));
        }
    }
}
