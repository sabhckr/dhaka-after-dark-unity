// Infrastructure placement survey (AXIOM v3 audit, section G; step 5 of section J, approved by the owner on
// 2026-11-04): one read-only check for everything that holds a structure up. It surveys what the game builds:
// the chunk's roads, sidewalks, markings and street kit from ChunkRoads.Build with keepMarks, and the drawn
// buildings from ChunkBuildings.Build. Nothing is moved or changed; it only measures and reports.
//
// Supports: metro viaduct piers (built, and those left out where the deck spans a junction), footbridge
// columns (built, and those left out over a carriageway), stair feet and tops, and road flyover piers and portal
// legs (D-11a, built, and the grid places left out where a deck spans), each flyover way listed as a deck.
//
// Every support gets a stable id from what it is and where it stands (east and north to 0.1 m), and a
// register row with the fields of the audit's section G: coordinates, structure and segment, road relation,
// building and pedestrian clearance, status, source and confidence. Checks on today's data:
// - carriageway: the support's footprint (a disc) overlaps a drawn carriageway at grade (one row per road);
// - junction: it overlaps a junction pad;
// - crossing: it stands in a zebra crossing or stop line's keep-out;
// - bus-stop: it stands on a bus stop pad;
// - building: it overlaps the footprint of a drawn building standing on the ground;
// - raised: it rises through a raised building (a station on stilts) below its top;
// - through-deck: an at-grade support rises through a drawn elevated carriageway below its top;
// - deck-top: its top is not where its deck's underside is (the builder's own constants);
// - duplicate: two supports of one structure closer than DuplicatePier (TwinMax: the builder serves tracks up to
//   that far apart with one pier per grid line) or DuplicateColumn, across chunk borders and, for columns,
//   across ways that meet at a node; for flyover supports (piers and portal legs together), two whose pile caps
//   overlap (FlyCapRim past each column);
// - span-short, span-long: a viaduct pier's gap to the next pier ahead on the same deck (sharing a track) under
//   PierMinSpan along it or over two grid steps on its main axis, or a
//   gap along a footbridge deck with nothing under it (columns, stair tops and joined decks hold it), outside
//   the limits below;
// - unsupported-end: a footbridge deck end with no column, stair or other deck within EndReach;
// - stair-unattached: a stair flight that reaches no deck;
// - no-supports: an elevated vehicle deck with no supports;
// - rail: a support within RailClearMin of an at-grade railway's centre line;
// - median-narrow, median-missing: a flyover or (P-6) viaduct pier on a median island leaves a part of a carriageway
//   beside the island narrower than LaneMin (on a one-way road a part under 0.3 m counts as covered; a two-way road
//   needs a lane each side), or no LaneMin lane on a road; or its island is not built;
// - median-one-side (P-6): a viaduct island without a lane of at least LaneMin on each side of it at its centre and
//   ends (of a carriageway under it, or one along the deck within IslandBesideMax of its edge): the owner's "traffic
//   passing on both sides". For a flyover island (D-11a may cover one side of a one-way road) it is a note;
// - island-junction, island-crossing, island-bus-stop, island-building, island-overlap (P-6): any island's whole
//   footprint, sampled every IslandStep, in a junction pad, a crossing's or stop line's keep-out, on a bus stop pad,
//   in a building on the ground, or over another island in its chunk;
// - P-5a, the continuous median under the viaduct: a pier the builder puts on it ("median run") is judged as an island
//   pier, but with traffic on each side counted within RunBesideMax of the island's edge and at least RunSideMin wide
//   (two lanes; median-one-side), every carriageway under it one-way (median-two-way) and none split by it, a part
//   over 0.3 m left on each side (median-split: it stands between twin carriageways); and run-junction,
//   run-crossing, run-bus-stop, run-building: any straight part of the median itself (its id MR-east-north, of the
//   part's start), sampled every IslandStep, in a junction pad, a crossing's or stop line's keep-out, on a bus stop pad
//   or in a building on the ground. A carriageway beside an island counts at a bend of its drawn line too, where the
//   road goes on, as in the builder (P-5a);
// - for flyovers, span-long and span-short are measured along each way joined across chunks (main-axis limit as
//   for the viaduct; the shortest a free end's support may make), and unsupported-end is a free deck end (not on
//   a chunk border, not meeting another deck) with no support within FlyEndReachMax along it;
// - deck-in-building: a deck (flyover, footbridge or viaduct girder), sampled every DeckSample metres along
//   its centre line, inside a drawn building's volume.
// Notes (recorded, never a failure): a pier left out where the deck spans a junction; a footbridge column
// left out over a carriageway (the span check judges the gap it leaves); a flyover grid place spanned; a flyover
// support standing in water (allowed, owner 2026-11-04) or on a median island (with the lanes it leaves).
//
// The span limits and reaches are assumptions from the builder's own rules, not engineering data: viaduct
// piers stand on a 30 m world grid and a junction is spanned in two steps (60 m); footbridge columns stand
// every 12 m, moved up to 3 m (ends 6 m). Widths of carriageways are mostly estimated (K-31), so a
// carriageway finding is against the drawn width, and the register says whether that width was mapped.
// Navigation, collision and driving checks need Phase E and F (section G.4); lane checks need lane polygons
// (G.5). Neither is here.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Dad.World;

namespace Dad.EditorTools
{
    public static class InfraSurvey
    {
        /// <summary>
        /// Viaduct spans: at least 25 m along the deck (K-37 V-6, P-2), and at most two 30 m grid steps on the deck's
        /// main axis (one grid place spanned, as over a junction), plus a metre; on a skewed deck two steps are longer
        /// than 60 m along it, so the limit is on the axis and the register gives the length along. Written out, not
        /// taken from the builder's constants, so that a change to the builder's rule shows here.
        /// </summary>
        public const double ViaductSpanMin = 25, ViaductAxisMax = 61, FootSpanMax = 25, EndReach = 6.5;
        /// <summary>The builder puts one pier per grid line under tracks up to TwinMax apart, so two piers closer than that are one deck's support doubled.</summary>
        public const double DuplicatePier = ChunkRoads.TwinMax, DuplicateColumn = 2, DeckSample = 2, ColumnRadius = 0.22, StairRadius = 0.6;
        public const double TopTolerance = 0.05, GroundBase = 0.5, JoinReach = 0.5;
        /// <summary>
        /// Flyovers (D-11a), written out as their own numbers: spans on the main axis at most 61 m (one 30 m grid place
        /// spanned) and at least 8 m along; a free end held within 20 m; a support's edge at least 2.5 m from a track's
        /// centre line; a lane beside a median island at least 3 m; the island 0.6 m wider than the pier each side.
        /// Two flyover supports overlap when their pile caps do: each cap reaches FlyCapRim past its column, so the columns'
        /// surfaces must be at least twice that apart.
        /// </summary>
        public const double FlyAxisMax = 61, FlySpanMin = 8, FlyEndReachMax = 20, RailClearMin = 2.5, LaneMinWidth = 3, IslandMargin = 0.6, IslandReach = 3, IslandBesideMax = 1.0, FlyCapRim = 0.55;
        /// <summary>P-5a, the continuous median: a carriageway beside it counts within 6.5 m of its edge (8 m from the deck line), and each side keeps 6 m (two 3 m lanes).</summary>
        public const double RunBesideMax = 6.5, RunSideMin = 6;
        /// <summary>A median island (flyover, D-11a; viaduct, P-6): IslandMargin wider than its pier each side, IslandReach past it each way along the deck.</summary>
        static double IslandHalf(Support s) => s.Radius + IslandMargin;
        static double IslandEnds(Support s) => s.Radius + IslandReach;
        static bool IsIsland(Support s) => s.BuilderWhy != null && s.BuilderWhy.Split(' ').Contains("median");
        /// <summary>P-5a: a pier on the continuous median.</summary>
        static bool IsRun(Support s) => s.BuilderWhy == "median run";

        public enum Structure { Viaduct, Footbridge, Stair, Flyover }

        /// <summary>A violation (gated by the test against the known list) or a note (recorded only).</summary>
        public sealed class Finding
        {
            public string Id, Check, Subject, Detail;
            public bool Note;
            /// <summary>What the known list matches on: support or deck id, check, and the other party (road, building, support) if any.</summary>
            public string Key => Subject == null ? $"{Id}|{Check}" : $"{Id}|{Check}|{Subject}";
        }

        public sealed class Support
        {
            public string Id, Kind, Chunk, BuilderWhy;
            public Structure Structure;
            public double East, North, X, Z, Radius, Foot, Top, ExpectedTop = double.NaN;
            public long Way;
            public bool Built, Context;
            /// <summary>The nearest drawn at-grade carriageway: way, class, whether its width is mapped, and the clearance from the footprint to its edge (negative: overlapping).</summary>
            public long RoadWay;
            public string RoadClass;
            public bool RoadMapped;
            public double RoadClear = double.PositiveInfinity;
            /// <summary>Every drawn at-grade carriageway the footprint overlaps: way, depth into it, class, whether the width is mapped.</summary>
            public readonly List<(long way, double depth, string cls, bool mapped)> RoadHits = new List<(long, double, string, bool)>();
            public bool InPad, InCrossing, OnBusStop, OnSidewalk;
            /// <summary>A drawn elevated carriageway over the support, below its top (0: none).</summary>
            public long ThroughDeck;
            /// <summary>The nearest drawn building: id, clearance (negative: overlapping), its base.</summary>
            public long BuildingId;
            public double BuildingClear = double.PositiveInfinity, BuildingBase;
            public double CrossingDistance = double.PositiveInfinity;
            public double SpanPrev = double.NaN, SpanNext = double.NaN, SpanNextAxis = double.NaN, NearestOther = double.PositiveInfinity;
            public string Next, NearestId;
            /// <summary>A portal leg's other leg: spans are measured from the beam's middle.</summary>
            public Support Partner;
            public double Ux, Uz;
            /// <summary>A pier's deck: the elevated tracks within TwinMax + TrackHalf in its own chunk (what the builder sees there).</summary>
            public HashSet<long> Tracks = new HashSet<long>();
            public string Source, Confidence;
            /// <summary>Distance from the footprint's edge to the nearest at-grade railway centre line; in water; on a built median island.</summary>
            public double RailClear = double.PositiveInfinity;
            public long RailWay;
            public bool InWater, OnIsland;
            /// <summary>For a pier on a median island: each carriageway under the island and the parts of it left either side.</summary>
            public readonly List<(long way, double left, double right, bool oneWay)> IslandParts = new List<(long, double, double, bool)>();
            /// <summary>For a pier on a median island: what its whole footprint lies on or overlaps (check, other party, detail).</summary>
            public readonly List<(string check, string subject, string detail)> IslandHits = new List<(string, string, string)>();
            /// <summary>For a pier on a median island: the narrowest, over its centre and ends, of the widest lane on each side of it (left and right of the deck).</summary>
            public double IslandSideL = double.NaN, IslandSideR = double.NaN;
        }

        public sealed class Deck
        {
            public string Id, Name, Class, Chunks;
            public Structure Structure;
            public long Way;
            public double Y, Length;
            public int Supports;
        }

        public sealed class Result
        {
            public readonly List<Support> Supports = new List<Support>();
            public readonly List<Deck> Decks = new List<Deck>();
            public readonly List<Finding> Findings = new List<Finding>();
            public readonly List<string> Chunks = new List<string>();
            /// <summary>Piers the builder itself marks "on a carriageway", and how many of them the survey finds on a carriageway or pad.</summary>
            public int BuilderOnRoad, BuilderOnRoadAgreed;
            /// <summary>The builder's own counts of viaduct piers in a building on the ground and under a raised building (K-37 P-3).</summary>
            public int BuilderInBuilding, BuilderUnderRaised;
            /// <summary>P-5a: the builder's piers on the continuous median, its pieces and their length (reported chunks).</summary>
            public int BuilderOnRun, RunPieces;
            public double RunLength;
        }

        /// <summary>The playable slice: chunks c_6 to c_9, -30 to -28.</summary>
        public static readonly string[] Slice = new[] { 6, 7, 8, 9 }.SelectMany(ix => new[] { -30, -29, -28 }.Select(iy => $"c_{ix}_{iy}")).ToArray();

        /// <summary>The ring of chunks around ids: surveyed for spans and duplicates across the edge, not reported.</summary>
        public static string[] Ring(IEnumerable<string> ids)
        {
            var set = new HashSet<string>(ids);
            var o = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var id in set)
            {
                var p = id.Split('_');
                int ix = int.Parse(p[1], CultureInfo.InvariantCulture), iy = int.Parse(p[2], CultureInfo.InvariantCulture);
                for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        string n = $"c_{ix + dx}_{iy + dy}";
                        if (!set.Contains(n)) o.Add(n);
                    }
            }
            return o.ToArray();
        }

        sealed class Chunk
        {
            public DadcChunk C;
            public ChunkRoads R;
            public ChunkBuildings B;
            public double E0, N0;
            public bool Context;
        }

        /// <summary>Surveys the chunks in ids (reported) and context (only for spans and duplicates across their edges).</summary>
        public static Result Run(string chunkDir, IEnumerable<string> ids, IEnumerable<string> context = null)
        {
            var res = new Result();
            var chunks = new List<Chunk>();
            foreach (var (id, ctx) in ids.Select(i => (i, false)).Concat((context ?? Enumerable.Empty<string>()).Select(i => (i, true))))
            {
                var f = Path.Combine(chunkDir, id + ".dadc");
                if (!File.Exists(f)) continue;
                var c = Dadc.Load(File.ReadAllBytes(f));
                NeighbourRails.Attach(c, chunkDir); // as the streamer loads it: the viaduct sees the tracks across the borders
                var ch = new Chunk { C = c, Context = ctx, E0 = c.CornerEastCm / 100.0, N0 = c.CornerNorthCm / 100.0 };
                ch.R = ChunkRoads.Build(c, keepMarks: true);
                ch.B = ChunkBuildings.Build(c);
                chunks.Add(ch);
                if (!ctx) res.Chunks.Add(id);
            }
            foreach (var ch in chunks) Supports(ch, res);
            Neighbours(res);
            IslandOverlaps(res);
            Decks(chunks, res);
            foreach (var s in res.Supports.Where(s => !s.Context)) Judge(s, res);
            res.Supports.RemoveAll(s => s.Context);
            res.Supports.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            res.Findings.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
            return res;
        }

        static string F1(double v) => v.ToString("0.0", CultureInfo.InvariantCulture);
        static string F2(double v) => v.ToString("0.00", CultureInfo.InvariantCulture);

        // ---- Supports and their relations ----

        static void Supports(Chunk ch, Result res)
        {
            var c = ch.C;
            // The chunk's elevated tracks and its neighbours' (in its centimetres): a deck may pair tracks across a border.
            var tracks = (c.Rails ?? new Rail[0]).Concat(c.NearRails ?? new Rail[0]).Where(t => t.YCm >= ChunkRoads.ViaductMinY * 100 && t.Pts != null && t.Pts.Length >= 4).ToList();
            var ways = (c.Roads ?? new Road[0]).Where(r => r.Pts != null).GroupBy(r => r.Id).ToDictionary(g => g.Key, g => g.First());
            Support openLeg = null; // a portal's legs are written as a pair, one after the other
            if (!ch.Context)
            {
                res.BuilderInBuilding += ch.R.Street.PiersInBuilding;
                res.BuilderUnderRaised += ch.R.Street.PiersUnderRaised;
                res.BuilderOnRun += ch.R.Street.PiersOnMedianRun;
                res.RunPieces += ch.R.Street.MedianRuns;
                res.RunLength += ch.R.Street.MedianRunLength;
                RunParts(ch, res);
            }
            foreach (var it in ch.R.Street.Items)
            {
                Structure st;
                string kind;
                double radius;
                switch (it.Kind)
                {
                    case ChunkStreet.Kind.Pier: st = Structure.Viaduct; kind = "pier"; radius = ChunkRoads.PierRadius; break;
                    case ChunkStreet.Kind.Column: st = Structure.Footbridge; kind = "column"; radius = ColumnRadius; break;
                    case ChunkStreet.Kind.StairFoot: st = Structure.Stair; kind = "stair foot"; radius = StairRadius; break;
                    case ChunkStreet.Kind.StairTop: st = Structure.Stair; kind = "stair top"; radius = StairRadius; break;
                    case ChunkStreet.Kind.FlyPier: st = Structure.Flyover; kind = "flyover pier"; radius = ChunkRoads.FlyPierRadius; break;
                    case ChunkStreet.Kind.FlyLeg: st = Structure.Flyover; kind = "portal leg"; radius = ChunkRoads.FlyLegRadius; break;
                    default: continue;
                }
                var s = new Support
                {
                    Structure = st, Kind = kind, Chunk = c.Id, X = it.X, Z = it.Z, East = ch.E0 + it.X, North = ch.N0 + it.Z, Radius = radius,
                    Foot = it.Y, Top = it.Top, Way = it.Way, Built = !it.Skipped, BuilderWhy = it.Why, Context = ch.Context,
                };
                if (it.Kind == ChunkStreet.Kind.FlyLeg)
                {
                    if (openLeg == null) openLeg = s;
                    else { s.Partner = openLeg; openLeg.Partner = s; openLeg = null; }
                }
                string at = $"{F1(s.East)}-{F1(s.North)}";
                s.Id = st switch
                {
                    Structure.Viaduct => "VP-" + at,
                    Structure.Footbridge => $"FC-{at}-{it.Way}", // ways meeting at a node each put a column there
                    Structure.Flyover => (it.Kind == ChunkStreet.Kind.FlyLeg ? "FL-" : "FP-") + at,
                    _ => $"ST-{it.Way}-{(it.Kind == ChunkStreet.Kind.StairFoot ? "foot" : "top")}",
                };
                // Direction of the line the support carries, for spans: the nearest elevated track, or the footway.
                if (st == Structure.Viaduct)
                {
                    double best = double.MaxValue;
                    foreach (var t in tracks)
                    {
                        var n = Near(t.Pts, s.X, s.Z);
                        if (n.d < best) { best = n.d; s.Ux = n.ux; s.Uz = n.uz; s.Way = t.Id; }
                    }
                    // The deck's underside as the builder sets it: the rail level less the drop and the girder (Structures.cs).
                    var carried = tracks.Where(t => Near(t.Pts, s.X, s.Z).d < ChunkRoads.TwinMax + ChunkRoads.TrackHalf).ToList();
                    foreach (var t in carried) s.Tracks.Add(t.Id);
                    if (carried.Count > 0) s.ExpectedTop = carried.Min(t => t.YCm / 100.0) - ChunkRoads.DeckDrop - ChunkRoads.GirderDepth;
                    s.Source = "builder rule: 30 m world grid along the deck (Structures.cs), on the exporter's rail line";
                    s.Confidence = "position by rule; real pier positions not in the data";
                }
                else if (st == Structure.Footbridge)
                {
                    if (ways.TryGetValue(it.Way, out var r))
                    {
                        var n = Near(r.Pts, s.X, s.Z);
                        s.Ux = n.ux;
                        s.Uz = n.uz;
                        s.ExpectedTop = r.YCm / 100.0 - ChunkRoads.FootDeckDepth;
                    }
                    s.Source = "builder rule: every 12 m along the mapped footbridge way, moved off carriageways (Structures.cs)";
                    s.Confidence = "position by rule; real column positions not in the data";
                }
                else if (st == Structure.Flyover)
                {
                    // A way may have more than one piece in the chunk: the one the support stands under.
                    var r = (c.Roads ?? new Road[0]).Where(q => q.Id == it.Way && q.Pts != null && q.Pts.Length >= 4).OrderBy(q => Near(q.Pts, s.X, s.Z).d).FirstOrDefault();
                    if (r != null)
                    {
                        var n = Near(r.Pts, s.X, s.Z);
                        s.Ux = n.ux;
                        s.Uz = n.uz;
                        // The girder's underside (pier caps) or the portal beam's underside (legs), from the deck's level.
                        double under = r.YCm / 100.0 - ChunkRoads.FlyDepth(r.YCm / 100.0);
                        s.ExpectedTop = it.Kind == ChunkStreet.Kind.FlyLeg ? under - ChunkRoads.FlyBeamDepth : under;
                    }
                    s.Source = "builder rule: 30 m world grid along the deck and near free ends (Flyovers.cs), on the exporter's way and layer height";
                    s.Confidence = "position by rule; real flyover pier positions not in the data; deck height by layer (K-05)";
                }
                else
                {
                    s.Source = "mapped steps way (OpenStreetMap), flight drawn by the builder";
                    s.Confidence = "line mapped; landing by rule";
                }
                if (IsRun(s))
                    s.OnIsland = OnRunParts(ch, s); // P-5a: on the median as drawn, not by the builder's own marker
                else if (IsIsland(s))
                    s.OnIsland = ch.R.Street.Items.Any(m => m.Kind == ChunkStreet.Kind.Median && Math.Abs(m.X - s.X) < 0.05 && Math.Abs(m.Z - s.Z) < 0.05);
                Relate(ch, s);
                res.Supports.Add(s);
                if (st == Structure.Viaduct && s.Built && !s.Context && s.BuilderWhy != null && s.BuilderWhy.StartsWith("on a carriageway", StringComparison.Ordinal))
                {
                    res.BuilderOnRoad++;
                    if (s.RoadClear < 0 || s.InPad) res.BuilderOnRoadAgreed++;
                }
            }
        }

        /// <summary>Roads, pads, crossings, bus stops, sidewalks and buildings around a support, all as drawn.</summary>
        static void Relate(Chunk ch, Support s)
        {
            var R = ch.R;
            double x = s.X, z = s.Z, r = s.Radius;
            var quad = new double[8];
            var hits = new List<(long way, double clear)>();
            var foot = new HashSet<long>((ch.C.Roads ?? new Road[0]).Where(rd => RoadPalette.IsFoot(rd.Cls)).Select(rd => rd.Id));
            foreach (var l in R.Lines)
            {
                if (foot.Contains(l.Way)) continue; // footways and steps are not carriageways (the builder's own test skips them too)
                bool elevated = Math.Abs(l.Y) > 0.8;
                if (elevated && (l.Y >= s.Top - 0.1 || s.ThroughDeck != 0)) continue;
                if (!elevated && l.Bridge) continue;
                quad[0] = l.Ax + l.Sx * l.HalfA; quad[1] = l.Az + l.Sz * l.HalfA;
                quad[2] = l.Bx + l.Ex * l.HalfB; quad[3] = l.Bz + l.Ez * l.HalfB;
                quad[4] = l.Bx - l.Ex * l.HalfB; quad[5] = l.Bz - l.Ez * l.HalfB;
                quad[6] = l.Ax - l.Sx * l.HalfA; quad[7] = l.Az - l.Sz * l.HalfA;
                double clear = Clearance(quad, x, z) - r;
                if (elevated) { if (clear < 0) s.ThroughDeck = l.Way; continue; }
                if (clear < 0) hits.Add((l.Way, clear));
                if (clear < s.RoadClear)
                {
                    s.RoadClear = clear;
                    s.RoadWay = l.Way;
                }
            }
            if (s.RoadWay != 0)
                foreach (var rd in ch.C.Roads)
                    if (rd.Id == s.RoadWay) { s.RoadClass = rd.Cls; s.RoadMapped = !rd.WidthEstimated; break; }
            foreach (var g in hits.GroupBy(h => h.way).OrderBy(g => g.Key))
            {
                var rd = ch.C.Roads.First(q => q.Id == g.Key);
                s.RoadHits.Add((g.Key, -g.Min(h => h.clear), rd.Cls, !rd.WidthEstimated));
            }
            for (int k = 0; k < R.KeepOuts.Count; k++)
            {
                var (ring, y) = R.KeepOuts[k];
                if (Math.Abs(y) > 0.8) continue;
                var poly = Flat(ring);
                double clear = Clearance(poly, x, z) - r;
                if (k < R.Pads) { if (clear < 0) s.InPad = true; }
                else
                {
                    if (clear < 0) s.InCrossing = true;
                    s.CrossingDistance = Math.Min(s.CrossingDistance, Math.Max(0, clear));
                }
            }
            foreach (var w in R.Walks)
            {
                if (Math.Abs(w.RoadY) > 0.8) continue;
                quad[0] = w.InAx; quad[1] = w.InAz; quad[2] = w.InBx; quad[3] = w.InBz;
                quad[4] = w.OutBx; quad[5] = w.OutBz; quad[6] = w.OutAx; quad[7] = w.OutAz;
                if (Clearance(quad, x, z) - r >= 0) continue;
                if (w.Kind == ChunkRoads.WalkKind.Stop) s.OnBusStop = true;
                else s.OnSidewalk = true;
            }
            foreach (var t in ch.C.Rails ?? new Rail[0])
            {
                if (t.Pts == null || t.Pts.Length < 4 || t.YCm >= RoadPalette.ElevatedY * 100) continue; // at grade only
                double clear = Near(t.Pts, x, z).d - r;
                if (clear < s.RailClear) { s.RailClear = clear; s.RailWay = t.Id; }
            }
            foreach (var w in ch.C.Water ?? new Poly[0])
                if (w.Outer != null && w.Outer.Length >= 6 && Clearance(Metres(w.Outer), x, z) < 0) s.InWater = true;
            foreach (var k in ch.C.Canals ?? new Canal[0])
                if (k.Pts != null && k.Pts.Length >= 4 && Near(k.Pts, x, z).d < k.WidthCm / 200.0) s.InWater = true;
            if (IsIsland(s))
            {
                // At the island's centre and at its two ends (IslandEnds along the deck), each at-grade carriageway within
                // the island's half width of the point: the parts of it left beside the island, from its drawn centre line
                // and half width there, on the segment nearest the point. A point past the drawn line's end is reported as
                // the island standing over the road's end (both parts 0). The narrowest of the three is kept per road.
                var best = new Dictionary<long, (double left, double right)>();
                s.IslandSideL = s.IslandSideR = double.PositiveInfinity;
                for (int k = -1; k <= 1; k++)
                {
                    double sx = x + s.Ux * IslandEnds(s) * k, sz = z + s.Uz * IslandEnds(s) * k;
                    // Traffic on each side of the island here (P-6): the widest lane of any at-grade carriageway running
                    // within 30 degrees of the deck, beyond the island's edge on that side, whose own edge is within
                    // IslandBesideMax of the island's (0 when it overlaps the island).
                    double wl = 0, wr = 0, beside = IsRun(s) ? RunBesideMax : IslandBesideMax;
                    foreach (var l in R.Lines)
                    {
                        if (foot.Contains(l.Way) || l.Bridge || Math.Abs(l.Y) > 0.8) continue;
                        double ex = l.Bx - l.Ax, ez = l.Bz - l.Az, len = Math.Sqrt(ex * ex + ez * ez);
                        if (len < 1e-9) continue;
                        double cosd = (ex * s.Ux + ez * s.Uz) / len;
                        if (Math.Abs(cosd) < Math.Cos(Math.PI / 6)) continue;
                        double raw = ((sx - l.Ax) * ex + (sz - l.Az) * ez) / (len * len);
                        if (raw < 0 || raw > 1)
                        {
                            // Past this segment's end: still beside where the same way goes on from that end (outside a bend).
                            double qx = raw < 0 ? l.Ax : l.Bx, qz = raw < 0 ? l.Az : l.Bz;
                            if (!R.Lines.Any(o => o.Way == l.Way && !o.Equals(l) && (Math.Abs(o.Ax - qx) < 0.05 && Math.Abs(o.Az - qz) < 0.05 || Math.Abs(o.Bx - qx) < 0.05 && Math.Abs(o.Bz - qz) < 0.05))) continue;
                            double cx = qx - sx, cz = qz - sz;
                            if (Math.Sqrt(cx * cx + cz * cz) > (raw < 0 ? l.HalfA : l.HalfB) + IslandHalf(s) + beside) continue;
                        }
                        double h = l.HalfA + (l.HalfB - l.HalfA) * Math.Clamp(raw, 0, 1), off = ((sx - l.Ax) * -ez + (sz - l.Az) * ex) / len;
                        double pl = h - (off + IslandHalf(s)), pr = off - IslandHalf(s) + h;
                        double onL = cosd > 0 ? pl : pr, onR = cosd > 0 ? pr : pl;
                        if (onL > 0 && onL - 2 * h <= beside) wl = Math.Max(wl, Math.Min(onL, 2 * h));
                        if (onR > 0 && onR - 2 * h <= beside) wr = Math.Max(wr, Math.Min(onR, 2 * h));
                    }
                    s.IslandSideL = Math.Min(s.IslandSideL, wl);
                    s.IslandSideR = Math.Min(s.IslandSideR, wr);
                    var here = new Dictionary<long, (double d, double left, double right)>();
                    foreach (var l in R.Lines)
                    {
                        if (foot.Contains(l.Way) || l.Bridge || Math.Abs(l.Y) > 0.8) continue;
                        double ex = l.Bx - l.Ax, ez = l.Bz - l.Az, ll = ex * ex + ez * ez;
                        if (ll < 1e-12) continue;
                        double raw = ((sx - l.Ax) * ex + (sz - l.Az) * ez) / ll, t = Math.Clamp(raw, 0, 1), len = Math.Sqrt(ll);
                        double off = ((sx - l.Ax) * -ez + (sz - l.Az) * ex) / len, h = l.HalfA + (l.HalfB - l.HalfA) * t;
                        double qx = l.Ax + ex * t - sx, qz = l.Az + ez * t - sz, d = Math.Sqrt(qx * qx + qz * qz);
                        if (d > h + IslandHalf(s)) continue;
                        var v = (d, h - (off + IslandHalf(s)), off - IslandHalf(s) + h);
                        if (raw < -1e-6 || raw > 1 + 1e-6)
                        {
                            // Past this segment's end: if the road goes on (another segment of it starts there, or the end
                            // is cut on the chunk border), that segment judges it; else the island stands over the road's end.
                            double px = raw < 0 ? l.Ax : l.Bx, pz = raw < 0 ? l.Az : l.Bz;
                            // Another way may carry the road on (a way that changes its name or id there), straight on
                            // within 30 degrees: then it is not the road's end either.
                            bool Straight(ChunkRoads.RoadLine o) => o.Way == l.Way || !foot.Contains(o.Way) && !o.Bridge && Math.Abs(o.Y) <= 0.8 &&
                                Math.Abs((o.Bx - o.Ax) * ex + (o.Bz - o.Az) * ez) >= Math.Cos(Math.PI / 6) * Math.Sqrt(((o.Bx - o.Ax) * (o.Bx - o.Ax) + (o.Bz - o.Az) * (o.Bz - o.Az)) * ll);
                            bool goesOn = px < 0.05 || px > 499.95 || pz < 0.05 || pz > 499.95 || R.Lines.Any(o => !o.Equals(l) && Straight(o) &&
                                (Math.Abs(o.Ax - px) < 0.05 && Math.Abs(o.Az - pz) < 0.05 || Math.Abs(o.Bx - px) < 0.05 && Math.Abs(o.Bz - pz) < 0.05));
                            if (goesOn) continue;
                            v = (d, 0, 0);
                        }
                        if (!here.TryGetValue(l.Way, out var e) || d < e.d) here[l.Way] = v;
                    }
                    foreach (var kv in here)
                        best[kv.Key] = best.TryGetValue(kv.Key, out var o) ? (Math.Min(o.left, kv.Value.left), Math.Min(o.right, kv.Value.right)) : (kv.Value.left, kv.Value.right);
                }
                // One-way from the routing graph, read on its own (the builder reads the same tag per drawn segment).
                var oneWay = new HashSet<long>((ch.C.Edges ?? new GraphEdge[0]).Where(e => e.Oneway != 0).Select(e => e.Way));
                foreach (var kv in best.OrderBy(k => k.Key)) s.IslandParts.Add((kv.Key, kv.Value.left, kv.Value.right, oneWay.Contains(kv.Key)));
                IslandFootprint(ch, s);
            }
            var bs = ch.C.Buildings ?? new Building[0];
            for (int i = 0; i < bs.Length; i++)
            {
                var b = bs[i];
                if (b.Outer == null || b.Outer.Length < 6 || ch.B.VertexCounts[i] == 0) continue;
                double clear = BuildingClearance(b, x, z) - r;
                if (clear < s.BuildingClear)
                {
                    s.BuildingClear = clear;
                    s.BuildingId = b.Id;
                    s.BuildingBase = ch.B.Placements[i].Base;
                }
            }
        }

        // ---- Spans and duplicates, across chunk edges ----

        /// <summary>cos 45°: a pier is ahead of or behind another (a span) only within 45° of the deck's direction.</summary>
        const double SpanAhead = 0.7071;

        /// <summary>
        /// The whole footprint of a support's median island (P-6), sampled every IslandStep metres: it must not lie in a
        /// junction pad, a crossing's or stop line's keep-out, on a bus stop pad or in a building on the ground (the pier's own disc is
        /// judged by the usual checks; the island is longer and wider than it).
        /// </summary>
        static void IslandFootprint(Chunk ch, Support s)
        {
            double a = IslandEnds(s), h = IslandHalf(s), ux = s.Ux, uz = s.Uz, lx = -uz, lz = ux;
            int na = (int)Math.Ceiling(2 * a / IslandStep), nh = (int)Math.Ceiling(2 * h / IslandStep);
            var pts = new List<(double x, double z)>();
            for (int i = 0; i <= na; i++)
                for (int j = 0; j <= nh; j++)
                {
                    double u = -a + 2 * a * i / na, v = -h + 2 * h * j / nh;
                    pts.Add((s.X + ux * u + lx * v, s.Z + uz * u + lz * v));
                }
            foreach (var (what, subject, detail) in Footprint(ch, pts, s.X, s.Z, a + h))
                s.IslandHits.Add(("island-" + what, subject, "its median island " + detail));
        }

        /// <summary>
        /// P-5a: each straight part of the continuous median, IslandMargin + PierRadius each side of its line (as an island),
        /// sampled every IslandStep along and across, judged as an island's footprint (run-junction, run-crossing,
        /// run-bus-stop, run-building).
        /// </summary>
        static void RunParts(Chunk ch, Result res)
        {
            double h = ChunkRoads.PierRadius + IslandMargin;
            foreach (var (ax, az, bx, bz) in ch.R.Street.MedianRunParts ?? new List<(double, double, double, double)>())
            {
                double len = Math.Sqrt((bx - ax) * (bx - ax) + (bz - az) * (bz - az));
                if (len < 1e-9) continue;
                double ux = (bx - ax) / len, uz = (bz - az) / len, lx = -uz, lz = ux;
                int na = (int)Math.Ceiling(len / IslandStep), nh = (int)Math.Ceiling(2 * h / IslandStep);
                var pts = new List<(double x, double z)>();
                for (int i = 0; i <= na; i++)
                    for (int j = 0; j <= nh; j++)
                    {
                        double u = len * i / na, v = -h + 2 * h * j / nh;
                        pts.Add((ax + ux * u + lx * v, az + uz * u + lz * v));
                    }
                string id = $"MR-{F1(ch.E0 + ax)}-{F1(ch.N0 + az)}";
                foreach (var (what, subject, detail) in Footprint(ch, pts, (ax + bx) / 2, (az + bz) / 2, len / 2 + h))
                    res.Findings.Add(new Finding { Id = id, Check = "run-" + what, Subject = subject, Detail = $"the continuous median ({F1(len)} m part in {ch.C.Id}) {detail}" });
            }
        }

        /// <summary>P-5a: do the centre and both ends of a pier's island footprint lie on a drawn part of the continuous median, the pier's disc within its width?</summary>
        static bool OnRunParts(Chunk ch, Support s)
        {
            var parts = ch.R.Street.MedianRunParts;
            if (parts == null) return false;
            for (int i = -1; i <= 1; i++)
            {
                double px = s.X + s.Ux * IslandEnds(s) * i, pz = s.Z + s.Uz * IslandEnds(s) * i;
                bool held = false;
                foreach (var (ax, az, bx, bz) in parts)
                {
                    double len = Math.Sqrt((bx - ax) * (bx - ax) + (bz - az) * (bz - az));
                    if (len < 1e-9) continue;
                    double ux = (bx - ax) / len, uz = (bz - az) / len, ex = px - (ax + bx) / 2, ez = pz - (az + bz) / 2;
                    if (Math.Abs(ex * ux + ez * uz) <= len / 2 + 1e-6 && Math.Abs(-ex * uz + ez * ux) <= IslandMargin + 1e-6) { held = true; break; }
                }
                if (!held) return false;
            }
            return true;
        }

        /// <summary>What a footprint (points) lies in or on: a junction pad, a crossing's or stop line's keep-out, a bus stop pad, a building on the ground (within reach of (cx, cz)).</summary>
        static List<(string what, string subject, string detail)> Footprint(Chunk ch, List<(double x, double z)> pts, double cx, double cz, double reach)
        {
            var R = ch.R;
            var hits = new List<(string, string, string)>();
            bool pad = false, crossing = false, stop = false;
            for (int k = 0; k < R.KeepOuts.Count; k++)
            {
                var (ring, y) = R.KeepOuts[k];
                if (Math.Abs(y) > 0.8) continue;
                var poly = Flat(ring);
                if (!pts.Any(p => Clearance(poly, p.x, p.z) < 0)) continue;
                if (k < R.Pads) pad = true; else crossing = true;
            }
            var quad = new double[8];
            foreach (var w in R.Walks)
            {
                if (Math.Abs(w.RoadY) > 0.8 || w.Kind != ChunkRoads.WalkKind.Stop) continue;
                quad[0] = w.InAx; quad[1] = w.InAz; quad[2] = w.InBx; quad[3] = w.InBz;
                quad[4] = w.OutBx; quad[5] = w.OutBz; quad[6] = w.OutAx; quad[7] = w.OutAz;
                if (pts.Any(p => Clearance(quad, p.x, p.z) < 0)) stop = true;
            }
            if (pad) hits.Add(("junction", null, "reaches into a junction pad"));
            if (crossing) hits.Add(("crossing", null, "reaches into a crossing's or stop line's keep-out"));
            if (stop) hits.Add(("bus-stop", null, "reaches onto a bus stop pad"));
            var bs = ch.C.Buildings ?? new Building[0];
            for (int i = 0; i < bs.Length; i++)
            {
                var b = bs[i];
                // On the ground only: under a raised building (a station body) nothing is drawn at street level.
                if (b.Outer == null || b.Outer.Length < 6 || ch.B.VertexCounts[i] == 0 || ch.B.Placements[i].Base >= GroundBase) continue;
                if (BuildingClearance(b, cx, cz) > reach) continue;
                if (pts.Any(p => BuildingClearance(b, p.x, p.z) < 0))
                    hits.Add(("building", b.Id.ToString(CultureInfo.InvariantCulture), $"reaches into building {b.Id}"));
            }
            return hits;
        }

        public const double IslandStep = 0.5;

        /// <summary>Two median islands (of any structures) in one chunk whose footprints overlap (separating axes of the two rectangles).</summary>
        static void IslandOverlaps(Result res)
        {
            var isl = res.Supports.Where(s => s.Built && s.OnIsland).ToList();
            for (int i = 0; i < isl.Count; i++)
                for (int j = 0; j < isl.Count; j++)
                {
                    if (i == j) continue;
                    Support p = isl[i], q = isl[j];
                    if (p.Chunk != q.Chunk) continue;
                    double ex = q.X - p.X, ez = q.Z - p.Z;
                    bool Apart(double ax, double az)
                    {
                        double d = Math.Abs(ex * ax + ez * az);
                        double r1 = IslandEnds(p) * Math.Abs(p.Ux * ax + p.Uz * az) + IslandHalf(p) * Math.Abs(-p.Uz * ax + p.Ux * az);
                        double r2 = IslandEnds(q) * Math.Abs(q.Ux * ax + q.Uz * az) + IslandHalf(q) * Math.Abs(-q.Uz * ax + q.Ux * az);
                        return d >= r1 + r2 - 1e-6;
                    }
                    if (!Apart(p.Ux, p.Uz) && !Apart(-p.Uz, p.Ux) && !Apart(q.Ux, q.Uz) && !Apart(-q.Uz, q.Ux))
                        p.IslandHits.Add(("island-overlap", q.Id, $"its median island overlaps {q.Id}'s"));
                }
        }

        static void Neighbours(Result res)
        {
            var built = res.Supports.Where(s => s.Built && s.Structure != Structure.Stair).ToList();
            foreach (var s in built)
            {
                double next = double.PositiveInfinity, prev = double.PositiveInfinity;
                foreach (var o in built)
                {
                    if (o == s || o.Structure != s.Structure) continue;
                    double dx = o.East - s.East, dz = o.North - s.North, d = Math.Sqrt(dx * dx + dz * dz);
                    // A flyover support's nearest is judged by the gap between the columns' surfaces (piers and legs differ).
                    double key = s.Structure == Structure.Flyover ? d - s.Radius - o.Radius : d;
                    if (key < s.NearestOther) { s.NearestOther = key; s.NearestId = o.Id; }
                    if (s.Structure == Structure.Flyover) continue; // spans along the way, in FlySpans
                    // Viaduct spans: the nearest pier ahead and behind on the same deck (sharing a track), straight-line
                    // distance. A pier closer than DuplicatePier is a duplicate, reported as one and not as a span.
                    // Footbridge spans are measured along each deck, with its stairs (Decks).
                    if (s.Structure != Structure.Viaduct || d < DuplicatePier || !s.Tracks.Overlaps(o.Tracks)) continue;
                    // Only a pier ahead or behind counts, not one beside: where tracks fan out (the north terminus), piers of
                    // side-by-side decks that share a track stand in a row across, 12 m apart, and that row is not a span.
                    double along = dx * s.Ux + dz * s.Uz;
                    if (Math.Abs(along) < d * SpanAhead) continue;
                    if (along > 0 && d < next) { next = d; s.Next = o.Id; s.SpanNextAxis = Math.Max(Math.Abs(dx), Math.Abs(dz)); }
                    if (along < 0 && d < prev) prev = d;
                }
                if (!double.IsPositiveInfinity(next)) s.SpanNext = next;
                if (!double.IsPositiveInfinity(prev)) s.SpanPrev = prev;
            }
        }

        // ---- Decks ----

        static void Decks(List<Chunk> chunks, Result res)
        {
            var flyovers = new Dictionary<long, Deck>();
            var foot = new Dictionary<long, Deck>();
            foreach (var ch in chunks.Where(k => !k.Context))
            {
                var c = ch.C;
                foreach (var r in c.Roads ?? new Road[0])
                {
                    if (r.Pts == null || r.Pts.Length < 4 || !r.Bridge || r.YCm < RoadPalette.ElevatedY * 100) continue;
                    bool isFoot = RoadPalette.IsFoot(r.Cls);
                    if (isFoot && (r.Cls == "steps" || !RoadPalette.IsFootSpan(r))) continue;
                    var map = isFoot ? foot : flyovers;
                    if (!map.TryGetValue(r.Id, out var d))
                    {
                        d = map[r.Id] = new Deck
                        {
                            Id = (isFoot ? "FB-" : "FD-") + r.Id, Way = r.Id, Name = r.NameEn ?? r.Name, Class = r.Cls, Y = r.YCm / 100.0,
                            Structure = isFoot ? Structure.Footbridge : Structure.Flyover, Chunks = c.Id,
                        };
                        res.Decks.Add(d);
                    }
                    else d.Chunks += " " + c.Id;
                    d.Length += Length(r.Pts);
                    // The deck slab against drawn building volumes: a flyover's 1 m slab, a footbridge's 0.4 m.
                    double slab = isFoot ? ChunkRoads.FootDeckDepth : 1.0;
                    Clash(ch, res, d.Id, r.Pts, d.Y - slab, d.Y);
                }
                // The viaduct girder: from its underside to the parapet top, along each elevated track.
                foreach (var t in c.Rails ?? new Rail[0])
                {
                    if (t.YCm < ChunkRoads.ViaductMinY * 100 || t.Pts == null || t.Pts.Length < 4) continue;
                    double top = t.YCm / 100.0 - ChunkRoads.DeckDrop;
                    Clash(ch, res, "VT-" + t.Id, t.Pts, top - ChunkRoads.GirderDepth, top + 1.1);
                }
            }
            foreach (var d in foot.Values)
            {
                var cols = res.Supports.Where(s => s.Structure == Structure.Footbridge && s.Built && s.Way == d.Way).ToList();
                d.Supports = cols.Count;
            }
            foreach (var d in flyovers.Values)
            {
                d.Supports = res.Supports.Count(s => s.Structure == Structure.Flyover && s.Built && !s.Context && s.Way == d.Way);
                if (d.Supports == 0)
                    res.Findings.Add(new Finding { Id = d.Id, Check = "no-supports", Detail = $"{d.Class} '{d.Name ?? "-"}' at {F1(d.Y)} m, {F1(d.Length)} m in the surveyed chunks, has no pier or column under it" });
            }
            FootSpans(chunks, res);
            FlySpans(chunks, res);
        }

        /// <summary>
        /// Each footbridge deck piece in the surveyed chunks, with what holds it: its own columns, columns of other
        /// ways and attached stair tops on its line (within its half width + JoinReach), a stair top or column within
        /// EndReach of an end, or another deck it joins. A piece cut on a chunk border continues to the nearest
        /// support of the same way in the next chunk. Gaps over FootSpanMax are span-long; an end held by nothing
        /// is unsupported-end.
        /// </summary>
        static void FootSpans(List<Chunk> chunks, Result res)
        {
            var lines = chunks.SelectMany(ch => (ch.C.Roads ?? new Road[0]).Where(r => RoadPalette.IsFootSpan(r) && r.Cls != "steps")
                .Select(r => (ch, r, xz: World(ch, r.Pts), hw: Math.Max(0.9, r.WidthCm / 200.0)))).ToList();
            var holders = res.Supports.Where(s => s.Built && (s.Structure == Structure.Footbridge || s.Kind == "stair top" && s.BuilderWhy == null)).ToList();
            var byId = holders.GroupBy(s => s.Id).ToDictionary(g => g.Key, g => g.First());
            foreach (var (ch, r, xz, hw) in lines.Where(l => !l.ch.Context))
            {
                double total = 0;
                for (int i = 0; i + 3 < xz.Length; i += 2) total += Math.Sqrt(Math.Pow(xz[i + 2] - xz[i], 2) + Math.Pow(xz[i + 3] - xz[i + 1], 2));
                var on = new List<(double along, string id)>();
                foreach (var s in holders)
                {
                    var (d, along) = Project(xz, s.East, s.North);
                    if (d <= hw + JoinReach) on.Add((along, s.Id));
                }
                int n = xz.Length;
                foreach (var (ex, ez, at, cut) in new[] { (xz[0], xz[1], 0.0, AtBorder(r.Pts[0], r.Pts[1])), (xz[n - 2], xz[n - 1], total, AtBorder(r.Pts[n - 2], r.Pts[n - 1])) })
                {
                    double sign = at == 0 ? -1 : 1;
                    if (cut)
                    {
                        // The way goes on in the next chunk: its nearest column (or stair top) there.
                        var o = holders.Where(s => s.Chunk != ch.C.Id && (s.Way == r.Id || s.Structure == Structure.Stair))
                            .Select(s => (s, d: Math.Sqrt((s.East - ex) * (s.East - ex) + (s.North - ez) * (s.North - ez))))
                            .Where(p => p.d <= FootSpanMax * 2).OrderBy(p => p.d).FirstOrDefault();
                        if (o.s != null) on.Add((at + sign * o.d, o.s.Id));
                        continue;
                    }
                    if (on.Any(p => Math.Abs(p.along - at) <= EndReach)) continue;
                    var near = holders.FirstOrDefault(s => Math.Sqrt((s.East - ex) * (s.East - ex) + (s.North - ez) * (s.North - ez)) <= EndReach);
                    if (near != null) { on.Add((at, near.Id)); continue; }
                    var join = lines.FirstOrDefault(l => l.r.Id != r.Id && Project(l.xz, ex, ez).d <= l.hw + JoinReach);
                    if (join.r != null) { on.Add((at, "FB-" + join.r.Id.ToString(CultureInfo.InvariantCulture))); continue; }
                    res.Findings.Add(new Finding
                    {
                        Id = $"FB-{r.Id}", Check = "unsupported-end", Subject = $"{F1(ex)}-{F1(ez)}",
                        Detail = $"deck end at E {F1(ex)} N {F1(ez)} has no column, stair or other deck within {F1(EndReach)} m",
                    });
                }
                on = on.GroupBy(p => p.id).Select(g => g.First()).OrderBy(p => Math.Round(p.along, 2)).ThenBy(p => p.id, StringComparer.Ordinal).ToList();
                for (int k = 0; k + 1 < on.Count; k++)
                {
                    double gap = on[k + 1].along - on[k].along;
                    if (byId.TryGetValue(on[k].id, out var a) && a.Way == r.Id) { a.SpanNext = gap; a.Next = on[k + 1].id; }
                    if (byId.TryGetValue(on[k + 1].id, out var b) && b.Way == r.Id) b.SpanPrev = gap;
                    if (gap > FootSpanMax)
                        res.Findings.Add(new Finding
                        {
                            Id = $"FB-{r.Id}", Check = "span-long", Subject = $"{on[k].id}>{on[k + 1].id}",
                            Detail = $"{F1(gap)} m along the deck between {on[k].id} and {on[k + 1].id} with nothing under it (limit {F1(FootSpanMax)} m)",
                        });
                }
            }
        }

        /// <summary>
        /// Each flyover way, its pieces joined across the surveyed chunks (context included) into lines: the supports of
        /// that way in order along it (a portal's two legs count once), the gaps between them (span-long on the main
        /// axis over FlyAxisMax, span-short under FlySpanMin along), and each free end (not on a chunk border, not
        /// meeting another elevated deck) with no support within FlyEndReachMax (unsupported-end). Reported where a
        /// support or end of the gap is in a reported chunk.
        /// </summary>
        static void FlySpans(List<Chunk> chunks, Result res)
        {
            static bool IsFly(Road r) => r.Pts != null && r.Pts.Length >= 4 && r.Bridge && r.YCm >= RoadPalette.ElevatedY * 100 && !RoadPalette.IsFoot(r.Cls);
            var pieces = chunks.SelectMany(ch => (ch.C.Roads ?? new Road[0]).Where(IsFly).Select(r => (ch, r, xz: World(ch, r.Pts)))).ToList();
            var holders = res.Supports.Where(s => s.Structure == Structure.Flyover && s.Built).ToList();
            foreach (var g in pieces.GroupBy(p => p.r.Id).OrderBy(g => g.Key))
            {
                var list = g.ToList();
                var used = new bool[list.Count];
                for (int s0 = 0; s0 < list.Count; s0++)
                {
                    if (used[s0]) continue;
                    used[s0] = true;
                    var line = new List<double>(list[s0].xz);
                    var ends = new List<(double x, double z, bool free, bool reported)>();
                    var members = new List<int> { s0 };
                    for (bool grew = true; grew;)
                    {
                        grew = false;
                        for (int q = 0; q < list.Count; q++)
                        {
                            if (used[q]) continue;
                            var p = list[q].xz;
                            int m = p.Length;
                            bool Eq(double ax, double az, double bx, double bz) => Math.Abs(ax - bx) < 0.05 && Math.Abs(az - bz) < 0.05;
                            int n0 = line.Count;
                            if (Eq(p[0], p[1], line[n0 - 2], line[n0 - 1])) for (int i = 2; i < m; i++) line.Add(p[i]);
                            else if (Eq(p[m - 2], p[m - 1], line[n0 - 2], line[n0 - 1])) for (int i = m - 4; i >= 0; i -= 2) { line.Add(p[i]); line.Add(p[i + 1]); }
                            else if (Eq(p[m - 2], p[m - 1], line[0], line[1])) line.InsertRange(0, p.Take(m - 2));
                            else if (Eq(p[0], p[1], line[0], line[1]))
                            {
                                var rev = new List<double>();
                                for (int i = m - 2; i >= 2; i -= 2) { rev.Add(p[i]); rev.Add(p[i + 1]); }
                                line.InsertRange(0, rev);
                            }
                            else continue;
                            used[q] = true;
                            members.Add(q);
                            grew = true;
                        }
                    }
                    int nl = line.Count;
                    var lineArr = line.ToArray();
                    double total = 0;
                    for (int i = 0; i + 3 < nl; i += 2) total += Math.Sqrt(Math.Pow(line[i + 2] - line[i], 2) + Math.Pow(line[i + 3] - line[i + 1], 2));
                    // A portal stands where its beam's middle is (on a curve its legs project apart along the deck).
                    var on = holders.Where(s => s.Way == g.Key)
                        .Select(s => (s, p: s.Partner == null ? Project(lineArr, s.East, s.North) : Project(lineArr, (s.East + s.Partner.East) / 2, (s.North + s.Partner.North) / 2)))
                        .Where(q => q.p.d <= ChunkRoads.PortalReach + 1).OrderBy(q => q.p.along).ThenBy(q => q.s.Id, StringComparer.Ordinal).ToList();
                    // A portal's two legs (and nothing else) stand at one place along: count it once.
                    var stops = new List<(double along, Support s)>();
                    foreach (var q in on)
                        if (stops.Count == 0 || q.p.along - stops[stops.Count - 1].along > 1.0) stops.Add((q.p.along, q.s));
                    string deck = "FD-" + g.Key.ToString(CultureInfo.InvariantCulture);
                    for (int k = 0; k + 1 < stops.Count; k++)
                    {
                        var (a, sa) = stops[k];
                        var (b, sb) = stops[k + 1];
                        if (sa.Context && sb.Context) continue;
                        PointAt(lineArr, a, out double ax, out double az);
                        PointAt(lineArr, b, out double bx, out double bz);
                        double gap = b - a, axis = Math.Max(Math.Abs(bx - ax), Math.Abs(bz - az));
                        if (!sa.Context) { sa.SpanNext = gap; sa.Next = sb.Id; sa.SpanNextAxis = axis; }
                        if (!sb.Context) sb.SpanPrev = gap;
                        if (axis > FlyAxisMax)
                            res.Findings.Add(new Finding { Id = deck, Check = "span-long", Subject = $"{sa.Id}>{sb.Id}", Detail = $"{F1(gap)} m along the deck between {sa.Id} and {sb.Id}, {F1(axis)} m on the main axis (limit {F1(FlyAxisMax)} m)" });
                        if (gap < FlySpanMin)
                            res.Findings.Add(new Finding { Id = deck, Check = "span-short", Subject = $"{sa.Id}>{sb.Id}", Detail = $"{F1(gap)} m along the deck between {sa.Id} and {sb.Id} (limit {F1(FlySpanMin)} m)" });
                    }
                    // Free ends: an end of the joined line that is not on a chunk border and meets no other elevated deck.
                    foreach (var (ex, ez, at) in new[] { (line[0], line[1], 0.0), (line[nl - 2], line[nl - 1], total) })
                    {
                        var host = members.Select(i => list[i]).FirstOrDefault(p => (Math.Abs(p.xz[0] - ex) < 0.05 && Math.Abs(p.xz[1] - ez) < 0.05) ||
                                                                                     (Math.Abs(p.xz[p.xz.Length - 2] - ex) < 0.05 && Math.Abs(p.xz[p.xz.Length - 1] - ez) < 0.05));
                        if (host.ch == null || host.ch.Context) continue;
                        double lx = ex - host.ch.E0, lz = ez - host.ch.N0;
                        if (lx < 0.05 || lx > 499.95 || lz < 0.05 || lz > 499.95) continue; // cut on the border: the way goes on
                        bool meets = pieces.Any(p => p.r.Id != g.Key && Math.Abs(p.r.YCm - host.r.YCm) < 80 && Project(p.xz, ex, ez).d <= p.r.WidthCm / 200.0 + 1.0);
                        if (meets || stops.Any(q => Math.Abs(q.along - at) <= FlyEndReachMax)) continue;
                        res.Findings.Add(new Finding
                        {
                            Id = deck, Check = "unsupported-end", Subject = $"{F1(ex)}-{F1(ez)}",
                            Detail = $"free deck end at E {F1(ex)} N {F1(ez)} has no support within {F1(FlyEndReachMax)} m along it",
                        });
                    }
                }
            }
        }

        /// <summary>The point at distance s along a polyline (pairs of x, z).</summary>
        static void PointAt(double[] xz, double s, out double x, out double z)
        {
            double run = 0;
            for (int i = 0; i + 3 < xz.Length; i += 2)
            {
                double l = Math.Sqrt(Math.Pow(xz[i + 2] - xz[i], 2) + Math.Pow(xz[i + 3] - xz[i + 1], 2));
                if (run + l >= s || i + 4 >= xz.Length)
                {
                    double t = l > 1e-9 ? Math.Clamp((s - run) / l, 0, 1) : 0;
                    x = xz[i] + (xz[i + 2] - xz[i]) * t;
                    z = xz[i + 1] + (xz[i + 3] - xz[i + 1]) * t;
                    return;
                }
                run += l;
            }
            x = xz[0];
            z = xz[1];
        }

        static bool AtBorder(int xcm, int zcm) => xcm < 5 || xcm > 49995 || zcm < 5 || zcm > 49995;

        static double[] World(Chunk ch, int[] cm)
        {
            var o = new double[cm.Length];
            for (int i = 0; i + 1 < cm.Length; i += 2) { o[i] = ch.E0 + cm[i] / 100.0; o[i + 1] = ch.N0 + cm[i + 1] / 100.0; }
            return o;
        }

        /// <summary>Distance from (x, z) to a polyline (pairs of x, z), and how far along the line its nearest point is.</summary>
        public static (double d, double along) Project(double[] xz, double x, double z)
        {
            double best = double.MaxValue, at = 0, run = 0;
            for (int i = 0; i + 3 < xz.Length; i += 2)
            {
                double ax = xz[i], az = xz[i + 1], ex = xz[i + 2] - ax, ez = xz[i + 3] - az, ll = ex * ex + ez * ez, l = Math.Sqrt(ll);
                double t = ll > 1e-12 ? Math.Clamp(((x - ax) * ex + (z - az) * ez) / ll, 0, 1) : 0;
                double qx = ax + ex * t - x, qz = az + ez * t - z, d = Math.Sqrt(qx * qx + qz * qz);
                if (d < best) { best = d; at = run + t * l; }
                run += l;
            }
            return (best, at);
        }

        static void Clash(Chunk ch, Result res, string id, int[] pts, double lo, double hi)
        {
            var bs = ch.C.Buildings ?? new Building[0];
            var metres = new Dictionary<long, (int n, double e, double nn, double b, double t)>();
            for (int i = 0; i + 3 < pts.Length; i += 2)
            {
                double ax = pts[i] / 100.0, az = pts[i + 1] / 100.0, bx = pts[i + 2] / 100.0, bz = pts[i + 3] / 100.0;
                int n = Math.Max(1, (int)(Math.Sqrt((bx - ax) * (bx - ax) + (bz - az) * (bz - az)) / DeckSample));
                for (int k = 0; k < n; k++)
                {
                    double x = ax + (bx - ax) * k / n, z = az + (bz - az) * k / n;
                    for (int j = 0; j < bs.Length; j++)
                    {
                        var b = bs[j];
                        if (b.Outer == null || ch.B.VertexCounts[j] == 0) continue;
                        var pl = ch.B.Placements[j];
                        if (!(pl.Base < hi && pl.Top > lo) || !InBuilding(b, x, z)) continue;
                        metres[b.Id] = metres.TryGetValue(b.Id, out var e) ? (e.n + 1, e.e, e.nn, e.b, e.t) : (1, ch.E0 + x, ch.N0 + z, pl.Base, pl.Top);
                    }
                }
            }
            foreach (var kv in metres)
                res.Findings.Add(new Finding
                {
                    Id = id, Check = "deck-in-building", Subject = kv.Key.ToString(CultureInfo.InvariantCulture),
                    Detail = $"{kv.Value.n} sample(s) of {F1(DeckSample)} m in {ch.C.Id}, first at E {F1(kv.Value.e)} N {F1(kv.Value.nn)}; deck {F2(lo)}-{F2(hi)} m, building {kv.Key} {F2(kv.Value.b)}-{F2(kv.Value.t)} m",
                });
        }

        // ---- Judging one support ----

        static void Judge(Support s, Result res)
        {
            void Add(string check, string subject, string detail, bool note = false) =>
                res.Findings.Add(new Finding { Id = s.Id, Check = check, Subject = subject, Detail = detail, Note = note });
            if (!s.Built)
            {
                if (s.Structure == Structure.Viaduct) Add("left-out", null, $"not built: {s.BuilderWhy}", note: true);
                else if (s.Structure == Structure.Flyover) Add("left-out", null, $"not built: {s.BuilderWhy} (way {s.Way})", note: true);
                else if (s.Structure == Structure.Footbridge) Add("left-out", null, $"not built: {s.BuilderWhy} (way {s.Way})", note: true);
                else if (s.BuilderWhy == "unattached") Add("stair-unattached", null, $"{s.Kind} of steps way {s.Way}: the flight reaches no deck");
                if (s.Structure != Structure.Stair) return;
            }
            if (s.Structure == Structure.Stair)
            {
                if (s.Built && s.BuilderWhy == "unattached") Add("stair-unattached", null, $"{s.Kind} of steps way {s.Way}: the flight reaches no deck");
                return; // a stair's foot and top are not columns: only its attachment is checked
            }
            bool island = IsIsland(s);
            if (island)
            {
                // A pier on a median island stands on the carriageway by design: judged by the lanes the island leaves.
                if (!s.OnIsland) Add("median-missing", null, "a median pier with no island built under it");
                foreach (var (way, left, right, oneWay) in s.IslandParts)
                {
                    string w = way.ToString(CultureInfo.InvariantCulture);
                    bool narrow = oneWay
                        ? left >= 0.3 && left < LaneMinWidth || right >= 0.3 && right < LaneMinWidth || Math.Max(left, right) < LaneMinWidth
                        : Math.Min(left, right) < LaneMinWidth;
                    if (narrow) Add("median-narrow", w, oneWay
                        ? $"island on one-way {w} leaves {F2(Math.Max(0, left))} m and {F2(Math.Max(0, right))} m (a part over 0.3 m must be at least {F1(LaneMinWidth)} m, and one lane kept)"
                        : $"island on two-way {w} leaves {F2(Math.Max(0, left))} m and {F2(Math.Max(0, right))} m (a lane of at least {F1(LaneMinWidth)} m each way)");
                    else Add("median", w, $"on a median island on {w}: {F2(Math.Max(0, left))} m and {F2(Math.Max(0, right))} m of carriageway left beside it", note: true);
                }
                if (s.IslandParts.Count == 0) Add("median-missing", null, "a median pier with no carriageway under its island");
                foreach (var (check, subject, detail) in s.IslandHits) Add(check, subject, detail);
                // P-6, the owner's "traffic passing on both sides": a viaduct island needs a lane each side; a flyover's
                // (D-11a) may cover one side of a one-way road, so for it this is a note.
                double sideMin = IsRun(s) ? RunSideMin : LaneMinWidth;
                if (!double.IsNaN(s.IslandSideL) && Math.Min(s.IslandSideL, s.IslandSideR) < sideMin)
                    Add("median-one-side", null, $"traffic on one side of its {(IsRun(s) ? "median" : "island")} only: {F2(s.IslandSideL)} m of lane on the left of the deck, {F2(s.IslandSideR)} m on the right (a lane of at least {F1(sideMin)} m each side)",
                        note: s.Structure == Structure.Flyover);
                if (IsRun(s))
                    foreach (var (way, left, right, oneWay) in s.IslandParts)
                        if (!oneWay) Add("median-two-way", way.ToString(CultureInfo.InvariantCulture), $"the continuous median stands on two-way {way}");
                        else if (left >= 0.3 && right >= 0.3)
                            Add("median-split", way.ToString(CultureInfo.InvariantCulture), $"the continuous median splits one-way {way}: {F2(left)} m and {F2(right)} m either side (it must stand between twin carriageways)");
            }
            else
                foreach (var h in s.RoadHits) // one row per carriageway, so a side street under the deck is not hidden by the avenue
                    Add("carriageway", h.way.ToString(CultureInfo.InvariantCulture), $"{F2(h.depth)} m into {h.cls} {h.way} ({(h.mapped ? "mapped width" : "estimated width")})");
            if (s.RailClear < RailClearMin) Add("rail", s.RailWay.ToString(CultureInfo.InvariantCulture), $"{F2(s.RailClear)} m from at-grade railway {s.RailWay} (limit {F1(RailClearMin)} m)");
            if (s.Structure == Structure.Flyover && s.InWater) Add("water", null, "stands in water (a bridge pier; allowed)", note: true);
            if (s.InPad) Add("junction", null, "in a junction pad");
            if (s.InCrossing) Add("crossing", null, "in a zebra crossing's or stop line's keep-out");
            if (s.OnBusStop) Add("bus-stop", null, "on a bus stop pad");
            if (s.BuildingClear < 0)
            {
                if (s.BuildingBase < GroundBase) Add("building", s.BuildingId.ToString(CultureInfo.InvariantCulture), $"{F2(-s.BuildingClear)} m into building {s.BuildingId}");
                else if (s.BuildingBase < s.Top) Add("raised", s.BuildingId.ToString(CultureInfo.InvariantCulture), $"rises through raised building {s.BuildingId} (base {F2(s.BuildingBase)} m, support top {F2(s.Top)} m)");
            }
            if (!double.IsNaN(s.ExpectedTop) && Math.Abs(s.Top - s.ExpectedTop) > TopTolerance)
                Add("deck-top", null, $"top {F2(s.Top)} m, deck underside {F2(s.ExpectedTop)} m");
            else if (double.IsNaN(s.ExpectedTop)) Add("deck-top", null, "no deck found above it");
            double dup = s.Structure == Structure.Viaduct ? DuplicatePier : s.Structure == Structure.Flyover ? 2 * FlyCapRim : DuplicateColumn;
            if (s.NearestOther < dup) Add("duplicate", s.NearestId, s.Structure == Structure.Flyover
                ? $"another flyover support, {s.NearestId}, {F2(s.NearestOther)} m from its column (the pile caps need {F2(dup)} m)"
                : $"another {s.Kind}, {s.NearestId}, {F2(s.NearestOther)} m away");
            if (s.Structure == Structure.Viaduct && !double.IsNaN(s.SpanNext))
            {
                if (s.SpanNext < ViaductSpanMin) Add("span-short", s.Next, $"{F1(s.SpanNext)} m to {s.Next} (limit {F1(ViaductSpanMin)} m)");
                if (s.SpanNextAxis > ViaductAxisMax) Add("span-long", s.Next, $"{F1(s.SpanNext)} m to {s.Next}, {F1(s.SpanNextAxis)} m on the main axis (limit {F1(ViaductAxisMax)} m: one grid place spanned)");
            }
            if (s.ThroughDeck != 0) Add("through-deck", s.ThroughDeck.ToString(CultureInfo.InvariantCulture), $"rises through elevated carriageway {s.ThroughDeck}, drawn below its top");
        }

        // ---- Geometry ----

        static (double d, double ux, double uz) Near(int[] cm, double x, double z)
        {
            double best = double.MaxValue, bux = 1, buz = 0;
            for (int i = 0; i + 3 < cm.Length; i += 2)
            {
                double ax = cm[i] / 100.0, az = cm[i + 1] / 100.0, ex = cm[i + 2] / 100.0 - ax, ez = cm[i + 3] / 100.0 - az, ll = ex * ex + ez * ez;
                if (ll < 1e-12) continue;
                double t = Math.Clamp(((x - ax) * ex + (z - az) * ez) / ll, 0, 1), qx = ax + ex * t - x, qz = az + ez * t - z, d = Math.Sqrt(qx * qx + qz * qz);
                if (d < best) { best = d; double l = Math.Sqrt(ll); bux = ex / l; buz = ez / l; }
            }
            return (best, bux, buz);
        }

        static double Length(int[] cm)
        {
            double l = 0;
            for (int i = 0; i + 3 < cm.Length; i += 2) l += Math.Sqrt(Math.Pow((cm[i + 2] - cm[i]) / 100.0, 2) + Math.Pow((cm[i + 3] - cm[i + 1]) / 100.0, 2));
            return l;
        }

        static double[] Metres(int[] cm)
        {
            var o = new double[cm.Length];
            for (int i = 0; i < cm.Length; i++) o[i] = cm[i] / 100.0;
            return o;
        }

        static double[] Flat(List<(double x, double z)> ring)
        {
            var o = new double[ring.Count * 2];
            for (int i = 0; i < ring.Count; i++) { o[2 * i] = ring[i].x; o[2 * i + 1] = ring[i].z; }
            return o;
        }

        /// <summary>Distance from (x, z) to the polygon's edge (pairs of x, z): positive outside, negative inside.</summary>
        public static double Clearance(double[] poly, double x, double z)
        {
            int n = poly.Length / 2;
            bool inside = false;
            double best = double.MaxValue;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                double xi = poly[2 * i], zi = poly[2 * i + 1], xj = poly[2 * j], zj = poly[2 * j + 1];
                if ((zi > z) != (zj > z) && x < (xj - xi) * (z - zi) / (zj - zi) + xi) inside = !inside;
                best = Math.Min(best, SegDist(xj, zj, xi, zi, x, z));
            }
            return inside ? -best : best;
        }

        static double SegDist(double ax, double az, double bx, double bz, double x, double z)
        {
            double ex = bx - ax, ez = bz - az, ll = ex * ex + ez * ez;
            double t = ll > 1e-12 ? Math.Clamp(((x - ax) * ex + (z - az) * ez) / ll, 0, 1) : 0;
            double qx = ax + ex * t - x, qz = az + ez * t - z;
            return Math.Sqrt(qx * qx + qz * qz);
        }

        static bool InBuilding(Building b, double x, double z)
        {
            if (Clearance(Metres(b.Outer), x, z) > 0) return false;
            if (b.Holes != null)
                foreach (var h in b.Holes)
                    if (h != null && h.Length >= 6 && Clearance(Metres(h), x, z) < 0) return false;
            return true;
        }

        /// <summary>Clearance from a point to a building footprint: negative inside it (holes count as outside).</summary>
        static double BuildingClearance(Building b, double x, double z)
        {
            double c = Clearance(Metres(b.Outer), x, z);
            if (c < 0 && b.Holes != null)
                foreach (var h in b.Holes)
                {
                    if (h == null || h.Length < 6) continue;
                    double hc = Clearance(Metres(h), x, z);
                    if (hc < 0) return -hc;
                    c = Math.Max(c, -hc);
                }
            return c;
        }
    }
}
