// Flyover supports and closed decks (D-11a: AXIOM v3 section J step 7, approved by the owner on 2026-11-04 with the
// world transformation brief; Docs/PRODUCTION/D11A_FLYOVER_SUPPORTS.md).
//
// Every elevated vehicle deck (a bridge way 2.5 m or higher that is not a foot way; RoadBuilder draws its surface,
// barriers and soffit) gets a closed box girder under it, which casts shadows (K-20), cut into segments at its
// supports with a narrow joint, and supports chosen from what is on the ground, never by moving the deck: the road
// and rail data are read, never changed. A support stands where the deck line crosses a PierGrid line of the world
// grid on its main axis, at least FlyMinSpan after the last, and near each free end. At each place, in order:
//   1. on clear ground (water allowed, as for any bridge; owner, 2026-11-04): a pier on the deck's centre line;
//   2. else slid along the deck by up to PierSlideMax to clear ground;
//   3. else, where only a road running along the deck is under it and a lane of at least LaneMin is left on both
//      sides (on a one-way road, one side may be covered instead), a pier on a raised median island drawn on that
//      road (owner: "median, else portal"), not overlapping another island (P-6: a viaduct's or another deck's);
//   4. else, where the railway is under it, an offset pier beside the tracks with a cap reaching out under the deck
//      (owner: "rail-side");
//   5. else a portal frame: two legs on clear ground either side, within PortalReach, and a cross-beam under the
//      girder;
//   6. else an offset pier for a building or a lower deck in the way;
//   7. else slid along the deck by up to FlyWideSlide to clear ground (a pier off the grid rather than a span of two
//      grid places);
//   8. else the place is spanned, and counted.
// No slide brings a support within FlyMinSpan of the last one. A grid line at a deck piece's start or end is a place
// too: on the south or west chunk border this chunk owns it, on the north or east border the neighbour does.
// A support never stands on a carriageway (except on its median island), in a junction pad, a crossing's keep-out or
// on a bus stop pad, in a building, within RailClear of a track, under a lower deck, on another support, or within
// BorderKeep of the chunk border near an elevated deck cut there (AtCutBorder). Where the grid leaves a gap too long,
// FillGaps adds supports. Deck heights are the exporter's layer rule (6, 11 or 16 m) until the elevation decision
// (K-05): ramps are not built (K-13), and under a 6 m deck the girder and caps leave less headroom than a real
// flyover would (K-52).
using System;
using System.Collections.Generic;

namespace Dad.World
{
    public sealed partial class ChunkRoads
    {
        public const double FlyPierRadius = 0.8, FlyLegRadius = 0.6, FlyMinSpan = 25, FlyEndInset = 2, FlyEndReach = 12, FlyEndGap = 8, FlyEndSlide = 6, FlyWideSlide = 12;
        /// <summary>Gap filling (FillGaps): the longest gap on the main axis between supports, from a border cut, and from a free end; where a border fill aims.</summary>
        public const double FlyFillAxis = 60, FlyBorderAxis = 30, FlyFreeReach = 20, FlyBorderTarget = 18, FlyFillStep = 20;
        public const double LaneMin = 3.0, MedianMargin = 0.6, MedianEnds = 3.0, MedianTop = 0.48, PortalReach = 20, OffsetReach = 2.0, RailClear = 2.5;
        public const double JointGap = 0.02, FlyBeamDepth = 0.9, FlyGirderInset = 0.05, RoadMargin = 0.05, MedianReach = 12;
        /// <summary>
        /// On a one-way road, a part of the carriageway beside an island narrower than this counts as covered by it (P-6:
        /// 5 cm inside the checker's 0.3 m, as RoadMargin, so that the two never disagree on an edge case).
        /// </summary>
        public const double IslandCovered = 0.25;
        public const uint FlyConcrete = 0x9b978e, FlySoffit = 0x78746b, PileCap = 0x86827a, DrainPipe = 0x3d3b38, MedianTopColour = 0x8e8a82;

        /// <summary>Depth of a flyover's box girder below its deck surface: shallower under the 6 m decks (K-05).</summary>
        public static double FlyDepth(double y) => y >= ViaductMinY ? 1.8 : 1.4;
        /// <summary>Depth of a flyover pier's cap below the girder.</summary>
        public static double FlyCapDepth(double y) => y >= ViaductMinY ? 1.1 : 0.6;
        /// <summary>A pile cap reaches CapRim past its column; two caps stay CapGap apart.</summary>
        public const double CapRim = 0.55, CapGap = 0.2;

        static readonly double[] Ring = MakeRing(16);

        static double[] MakeRing(int n)
        {
            var r = new double[2 * n];
            for (int k = 0; k < n; k++) { r[2 * k] = Math.Cos(2 * Math.PI * k / n); r[2 * k + 1] = Math.Sin(2 * Math.PI * k / n); }
            return r;
        }

        /// <summary>Half width of a median island across its road.</summary>
        public const double MedianHalf = FlyPierRadius + MedianMargin;

        enum Ground : byte { Clear, AlongRoad, CrossRoad, Pad, Building, Rail, LowerDeck, Support, Border, Crossing, BusStop }

        /// <summary>
        /// No flyover support stands within BorderKeep of its chunk's border near a place where the border cuts an elevated
        /// deck (within BorderCutReach of the deck's edge at the cut, along the border): each chunk places its own supports without seeing its
        /// neighbour's, so two supports either side of a cut deck stay at least twice BorderKeep apart (the shortest span
        /// the checker allows) and their pile caps never meet. Away from any cut the strip is open: a deck that only runs
        /// near the border, or ends there, has no neighbour piece to clash with.
        /// </summary>
        public const double BorderKeep = 4, BorderCutReach = 15;

        static bool IsFlyDeck(Drawn d) => d.Bridge && d.Y >= RoadPalette.ElevatedY && d.N >= 2 && d.Halves != null;

        void Flyovers(DadcChunk c, List<Drawn> drawn, ChunkStreet st, KitWriter w, StreetScratch ss, bool keep)
        {
            bool any = false;
            foreach (var d in drawn) if (IsFlyDeck(d)) { any = true; break; }
            if (!any) return;
            LoadGround(c, ss);
            LoadGroundBuildings(c);
            ss.FlyPlaced.Clear();
            ss.FlyBorderCuts.Clear();
            foreach (var d in drawn)
            {
                if (!d.Bridge || d.Y < RoadPalette.ElevatedY || d.N < 2) continue;
                if (d.StartOn) ss.FlyBorderCuts.Add((d.Pts[0], d.Pts[1], BorderCutReach + (d.Halves != null ? d.Halves[0] : 0)));
                if (d.EndOn) ss.FlyBorderCuts.Add((d.Pts[2 * d.N - 2], d.Pts[2 * d.N - 1], BorderCutReach + (d.Halves != null ? d.Halves[d.N - 1] : 0)));
            }
            foreach (var d in drawn)
            {
                if (!IsFlyDeck(d)) continue;
                ss.Cuts.Clear();
                FlyPlaces(c, d, drawn, st, w, ss, keep);
                FlyGirder(d, st, w, ss);
                st.FlyDecks++;
            }
        }

        /// <summary>The at-grade rails (metres) and the water areas and canals of the chunk, for the ground tests.</summary>
        static void LoadGround(DadcChunk c, StreetScratch ss)
        {
            ss.GradeRails.Clear();
            ss.WaterRings.Clear();
            ss.CanalLines.Clear();
            foreach (var r in c.Rails ?? Array.Empty<Rail>())
                if (r.Pts != null && r.Pts.Length >= 4 && r.YCm < RoadPalette.ElevatedY * 100) ss.GradeRails.Add(Metres(r.Pts));
            foreach (var p in c.Water ?? Array.Empty<Poly>())
                if (p.Outer != null && p.Outer.Length >= 6) ss.WaterRings.Add(Metres(p.Outer));
            foreach (var k in c.Canals ?? Array.Empty<Canal>())
                if (k.Pts != null && k.Pts.Length >= 4) ss.CanalLines.Add((Metres(k.Pts), k.WidthCm / 200.0));
        }

        static double[] Metres(int[] cm)
        {
            var o = new double[cm.Length];
            for (int i = 0; i < cm.Length; i++) o[i] = cm[i] / 100.0;
            return o;
        }

        // ---- Places ----

        void FlyPlaces(DadcChunk c, Drawn d, List<Drawn> drawn, ChunkStreet st, KitWriter w, StreetScratch ss, bool keep)
        {
            var p = d.Pts;
            int n = d.N;
            double total = d.Arc[n - 1], last = double.NegativeInfinity, visited = double.NegativeInfinity;
            for (int k = 0; k + 1 < n; k++)
            {
                double ax = p[2 * k], az = p[2 * k + 1], bx = p[2 * k + 2], bz = p[2 * k + 3];
                double seg = d.Arc[k + 1] - d.Arc[k];
                if (seg < 1e-9) continue;
                bool alongEast = Math.Abs(bx - ax) >= Math.Abs(bz - az);
                double corner = alongEast ? c.CornerEastCm / 100.0 : c.CornerNorthCm / 100.0;
                double a = corner + (alongEast ? ax : az), b = corner + (alongEast ? bx : bz);
                double lo = Math.Min(a, b), hi = Math.Max(a, b);
                // Grid lines met, [low, high] within 1 mm, visited in the segment's own direction so that along only grows;
                // a line at a vertex is met by both segments and visited once.
                double first = Math.Ceiling((lo - 1e-3) / PierGrid) * PierGrid, lastLine = Math.Floor((hi + 1e-3) / PierGrid) * PierGrid;
                int lines = lastLine < first ? 0 : (int)Math.Round((lastLine - first) / PierGrid) + 1;
                for (int gi = 0; gi < lines; gi++)
                {
                    double g = b >= a ? first + gi * PierGrid : lastLine - gi * PierGrid;
                    double t = Math.Clamp((g - a) / (b - a), 0, 1);
                    double along = d.Arc[k] + t * seg;
                    if (along - visited < 1e-3) continue;
                    visited = along;
                    if (along - last < FlyMinSpan) continue;
                    double px = ax + (bx - ax) * t, pz = az + (bz - az) * t;
                    if (!Owned(px, pz)) continue; // on the border, built by the neighbour
                    if (FlySupport(d, st, w, ss, ref along, 0, keep, null, last + FlyMinSpan, double.PositiveInfinity)) last = along;
                }
            }
            // A free end (not cut on the border, not meeting another deck) gets a support near it if none is close.
            for (int e = 0; e < 2; e++)
            {
                bool start = e == 0;
                if (start ? d.StartOn : d.EndOn) continue;
                double ex = start ? p[0] : p[2 * n - 2], ez = start ? p[1] : p[2 * n - 1];
                if (MeetsDeck(d, drawn, ex, ez)) continue;
                double end = start ? 0 : total, at = start ? Math.Min(FlyEndInset, total / 2) : Math.Max(total - FlyEndInset, total / 2);
                bool near = false;
                foreach (double cut in ss.Cuts) if (Math.Abs(cut - end) <= FlyEndReach) { near = true; break; }
                if (near) continue;
                FlySupport(d, st, w, ss, ref at, start ? 1 : -1, keep, "end", double.NegativeInfinity, double.PositiveInfinity);
            }
            FillGaps(c, d, drawn, st, w, ss, keep);
        }

        /// <summary>
        /// Fills what the grid left too long, on this piece of deck: two supports more than FlyFillAxis apart on the main
        /// axis (the checker's limit, less a metre); a border cut more than FlyBorderAxis from the nearest support on the
        /// axis (so that with the neighbour's piece, which keeps the same rule, the span over the border stays inside the
        /// limit); a free end more than FlyFreeReach from one. A fill support takes the same ground rules and slides, never
        /// within FlyMinSpan of another along the deck; one that finds no ground is not built (and not counted as spanned:
        /// the checker reports the gap).
        /// </summary>
        void FillGaps(DadcChunk c, Drawn d, List<Drawn> drawn, ChunkStreet st, KitWriter w, StreetScratch ss, bool keep)
        {
            var p = d.Pts;
            int n = d.N;
            double total = d.Arc[n - 1];
            bool startBorder = d.StartOn, endBorder = d.EndOn;
            bool startFree = !d.StartOn && !MeetsDeck(d, drawn, p[0], p[1]), endFree = !d.EndOn && !MeetsDeck(d, drawn, p[2 * n - 2], p[2 * n - 1]);
            var tried = ss.Tried;
            tried.Clear();
            for (int round = 0; round < 32; round++)
            {
                var cuts = ss.Sorted;
                cuts.Clear();
                cuts.AddRange(ss.Cuts);
                cuts.Sort();
                double target = double.NaN, lo = double.NegativeInfinity, hi = double.PositiveInfinity;
                double first = cuts.Count > 0 ? cuts[0] : double.NaN, last = cuts.Count > 0 ? cuts[cuts.Count - 1] : double.NaN;
                if (cuts.Count == 0)
                {
                    if (startBorder && endBorder ? Axis(d, 0, total) > FlyFillAxis : startBorder || endBorder ? Axis(d, 0, total) > FlyBorderAxis : total > 2 * FlyFreeReach)
                        target = total / 2;
                }
                else if (startBorder && Axis(d, 0, first) > FlyBorderAxis) { target = Math.Min(FlyBorderTarget, first - FlyMinSpan); hi = first - FlyMinSpan; }
                else if (endBorder && Axis(d, last, total) > FlyBorderAxis) { target = Math.Max(total - FlyBorderTarget, last + FlyMinSpan); lo = last + FlyMinSpan; }
                else if (startFree && first > FlyFreeReach) { target = Math.Min(FlyFreeReach / 2, first - FlyMinSpan); hi = first - FlyMinSpan; }
                else if (endFree && total - last > FlyFreeReach) { target = Math.Max(total - FlyFreeReach / 2, last + FlyMinSpan); lo = last + FlyMinSpan; }
                else
                    for (int k = 0; k + 1 < cuts.Count; k++)
                        if (Axis(d, cuts[k], cuts[k + 1]) > FlyFillAxis && !tried.Contains(Math.Round((cuts[k] + cuts[k + 1]) / 2, 2)))
                        {
                            target = (cuts[k] + cuts[k + 1]) / 2; lo = cuts[k] + FlyMinSpan; hi = cuts[k + 1] - FlyMinSpan;
                            break;
                        }
                double from = Math.Max(0, lo), to = Math.Min(total, hi);
                if (double.IsNaN(target) || to < from) return;
                target = Math.Clamp(target, from, to);
                double key = Math.Round(target, 2);
                if (tried.Contains(key)) return; // tried before, and nothing fits: the checker reports the gap
                tried.Add(key);
                // The target first, then every FlyFillStep either side of it within the range, nearest first: each try
                // slides up to FlyWideSlide, so together they cover the range.
                bool placed = false;
                for (int k = 0; !placed && k < 64; k++)
                {
                    double at = target + (k % 2 == 1 ? 1 : -1) * ((k + 1) / 2) * FlyFillStep;
                    if (at < from - 1e-9 || at > to + 1e-9)
                    {
                        if (target + ((k + 1) / 2) * FlyFillStep > to && target - ((k + 1) / 2) * FlyFillStep < from) break;
                        continue;
                    }
                    DrawnAt(d, at, out double px, out double pz, out _, out _, out _);
                    if (!Owned(px, pz)) continue;
                    if (FlySupport(d, st, w, ss, ref at, 0, keep, "fill", lo, hi)) { st.FlyFilled++; placed = true; }
                }
                if (!placed) return;
            }
        }

        /// <summary>The distance on the main axis between two points along deck d.</summary>
        static double Axis(Drawn d, double a, double b)
        {
            DrawnAt(d, a, out double ax, out double az, out _, out _, out _);
            DrawnAt(d, b, out double bx, out double bz, out _, out _, out _);
            return Math.Max(Math.Abs(bx - ax), Math.Abs(bz - az));
        }

        /// <summary>Does another elevated deck (or an elevated pad) at d's level meet (x, z)?</summary>
        bool MeetsDeck(Drawn d, List<Drawn> drawn, double x, double z)
        {
            if (InAnyPad(x, z, d.Y, 1.0)) return true;
            var mine = Mine();
            foreach (int q in SegmentsNear(x - 1, x + 1, z - 1, z + 1))
            {
                var s = mine.SegItems[q];
                if (s.Road == d || !s.Road.Bridge || !SameLevel(s.Road.Y, d.Y)) continue;
                int start = s.ExactCount >= 3 ? s.Exact : s.Start, m = s.ExactCount >= 3 ? s.ExactCount : s.Count;
                if (InRing(mine.SegRings, start, m, x, z, 1.0)) return true;
            }
            return false;
        }

        /// <summary>
        /// Puts up one support for deck d near along (moved there if it slides; slideDir 0 both ways, +1 or -1 one way only).
        /// False when the place is spanned.
        /// </summary>
        bool FlySupport(Drawn d, ChunkStreet st, KitWriter w, StreetScratch ss, ref double along, int slideDir, bool keep, string tag, double minAt, double maxAt)
        {
            double y = d.Y, total = d.Arc[d.N - 1];
            DrawnAt(d, along, out double px, out double pz, out double dx, out double dz, out int seg);
            long way = d.Attrs != null && seg < d.Attrs.Length ? d.Attrs[seg].Way : 0;
            var why = GroundAt(d, ss, px, pz, dx, dz, FlyPierRadius);
            if (why == Ground.Clear)
            {
                FlyPier(d, st, w, ss, px, pz, px, pz, dx, dz, along, way, keep, Tags(tag, InWater(ss, px, pz) ? "water" : null));
                return true;
            }
            // Slide along the deck.
            if (Slide(d, st, w, ss, ref along, slideDir, keep, tag, minAt, maxAt, tag == "end" ? FlyEndSlide : PierSlideMax, 0, way, "slid")) return true;
            double half = HalfAt(d, along);
            // On the road the deck runs along: the median, within the cap's reach across the deck.
            if (why == Ground.AlongRoad && MedianNear(d, st, w, ss, px, pz, dx, dz, half, along, way, keep, tag)) return true;
            // Beside the railway: an offset pier, else a portal over the tracks.
            if (why == Ground.Rail && Offset(d, st, w, ss, px, pz, dx, dz, half, along, way, keep, tag)) return true;
            if (Portal(d, st, w, ss, px, pz, dx, dz, half, along, way, keep, tag)) return true;
            if ((why == Ground.Building || why == Ground.LowerDeck || why == Ground.Support || why == Ground.Border || why == Ground.BusStop) && Offset(d, st, w, ss, px, pz, dx, dz, half, along, way, keep, tag)) return true;
            // Off the grid: further along the deck, rather than a span of two grid places.
            if (tag != "end" && Slide(d, st, w, ss, ref along, slideDir, keep, tag, minAt, maxAt, FlyWideSlide, PierSlideMax, way, "slid-far")) { st.FlySlidFar++; return true; }
            if (tag == "fill") return false;
            st.FlySpanned++;
            if (keep) st.Items.Add(new ChunkStreet.Item { Kind = ChunkStreet.Kind.FlyPier, X = px, Z = pz, Way = way, Skipped = true, Why = "spanned: " + Name(why) });
            return false;
        }

        /// <summary>A pier slid along the deck from along, by more than skip and at most reach, to clear ground, never before minAt.</summary>
        bool Slide(Drawn d, ChunkStreet st, KitWriter w, StreetScratch ss, ref double along, int slideDir, bool keep, string tag, double minAt, double maxAt, double reach, double skip, long way, string label)
        {
            double total = d.Arc[d.N - 1];
            for (double s = skip + PierSlideStep; s <= reach + 1e-9; s += PierSlideStep)
                for (int sign = 1; sign >= -1; sign -= 2)
                {
                    if (slideDir != 0 && sign != slideDir) continue;
                    double at = along + sign * s;
                    if (at < 0 || at > total || at < minAt || at > maxAt) continue;
                    DrawnAt(d, at, out double qx, out double qz, out double qdx, out double qdz, out int qs);
                    if (!Owned(qx, qz) || GroundAt(d, ss, qx, qz, qdx, qdz, FlyPierRadius) != Ground.Clear) continue;
                    if (tag == "end" && TooNearCut(ss, at)) continue;
                    along = at;
                    st.FlySlid++;
                    FlyPier(d, st, w, ss, qx, qz, qx, qz, qdx, qdz, at, d.Attrs != null && qs < d.Attrs.Length ? d.Attrs[qs].Way : way, keep,
                        Tags(tag, label, InWater(ss, qx, qz) ? "water" : null));
                    return true;
                }
            return false;
        }

        static string Name(Ground g) => g switch
        {
            Ground.AlongRoad => "on the road below, too narrow for a median",
            Ground.CrossRoad => "on a crossing road",
            Ground.Pad => "in a junction",
            Ground.Building => "in a building",
            Ground.Rail => "on the railway",
            Ground.LowerDeck => "under a lower deck",
            Ground.Support => "on another support",
            Ground.Border => "at the chunk border",
            Ground.Crossing => "in a crossing's keep-out",
            Ground.BusStop => "on a bus stop",
            _ => "clear",
        };

        static string Tags(params string[] t)
        {
            string o = null;
            foreach (var s in t) if (s != null) o = o == null ? s : o + " " + s;
            return o;
        }

        static bool TooNearCut(StreetScratch ss, double at)
        {
            foreach (double c in ss.Cuts) if (Math.Abs(c - at) < FlyEndGap) return true;
            return false;
        }

        /// <summary>Within BorderKeep of a chunk border side, and within BorderCutReach (along it) of the edge of an elevated deck cut there?</summary>
        /// The support's own deck keeps it out within 2 BorderCutReach (straight-line) of its own cut, however wide the deck
        /// or shallow the crossing.
        static bool AtCutBorder(Drawn d, StreetScratch ss, double x, double z)
        {
            bool w = x < BorderKeep, e = x > Size - BorderKeep, s = z < BorderKeep, n = z > Size - BorderKeep;
            if (!(w || e || s || n)) return false;
            bool Side(double cx, double cz) => w && cx < OwnSnap || e && cx > Size - OwnSnap || s && cz < OwnSnap || n && cz > Size - OwnSnap;
            for (int k = 0; k < 2; k++)
            {
                if (!(k == 0 ? d.StartOn : d.EndOn)) continue;
                double cx = k == 0 ? d.Pts[0] : d.Pts[2 * d.N - 2], cz = k == 0 ? d.Pts[1] : d.Pts[2 * d.N - 1];
                if (Side(cx, cz) && (cx - x) * (cx - x) + (cz - z) * (cz - z) <= 4 * BorderCutReach * BorderCutReach) return true;
            }
            foreach (var (cx, cz, reach) in ss.FlyBorderCuts)
            {
                if (w && cx < OwnSnap && Math.Abs(cz - z) <= reach) return true;
                if (e && cx > Size - OwnSnap && Math.Abs(cz - z) <= reach) return true;
                if (s && cz < OwnSnap && Math.Abs(cx - x) <= reach) return true;
                if (n && cz > Size - OwnSnap && Math.Abs(cx - x) <= reach) return true;
            }
            return false;
        }

        /// <summary>What is on the ground under a support's disc of radius r at (x, z), for a deck at level d.Y running (dx, dz).</summary>
        Ground GroundAt(Drawn d, StreetScratch ss, double x, double z, double dx, double dz, double r)
        {
            if (AtCutBorder(d, ss, x, z)) return Ground.Border;
            if (InAnyPad(x, z, 0.28, r + 0.5)) return Ground.Pad;
            foreach (var ring in Mine().GradeKeepOuts) if (InOrNear(ring, x, z, r)) return Ground.Crossing;
            if (OnStopPad(x, z, r)) return Ground.BusStop;
            if (InBuilding(x, z)) return Ground.Building;
            // The pile cap's footprint (CapRim past the column), on a ring of 16 points.
            for (int k = 0; k < 16; k++)
                if (InBuilding(x + (r + CapRim) * Ring[2 * k], z + (r + CapRim) * Ring[2 * k + 1])) return Ground.Building;
            if (NearRail(ss, x, z, r)) return Ground.Rail;
            if (UnderLowerDeck(d, x, z, r)) return Ground.LowerDeck;
            foreach (var q in ss.FlyPlaced)
            {
                // Pile caps apart by CapGap.
                double m = q.r + r + 2 * CapRim + CapGap;
                if ((q.x - x) * (q.x - x) + (q.z - z) * (q.z - z) < m * m) return Ground.Support;
            }
            foreach (var q in ss.PiersPlaced)
            {
                double m = PierRadius + r + 2 * CapRim + CapGap;
                if ((q.x - x) * (q.x - x) + (q.z - z) * (q.z - z) < m * m) return Ground.Support;
            }
            // A 5 cm margin past the disc: the drawn outline is a little inside its exact ring (ExactInset).
            if (OnCarriageway(x, z, 0.28, r + RoadMargin)) return OnCrossingRoad(x, z, dx, dz, r + RoadMargin) ? Ground.CrossRoad : Ground.AlongRoad;
            return Ground.Clear;
        }

        static bool NearRail(StreetScratch ss, double x, double z, double r)
        {
            foreach (var l in ss.GradeRails)
                if (LineDistance(l, x, z) < RailClear + r) return true;
            return false;
        }

        static bool InWater(StreetScratch ss, double x, double z)
        {
            foreach (var ring in ss.WaterRings) if (InPolygon(ring, x, z)) return true;
            foreach (var (l, half) in ss.CanalLines) if (LineDistance(l, x, z) < half) return true;
            return false;
        }

        /// <summary>Is the disc under an elevated carriageway lower than deck d (the support would rise through it)?</summary>
        bool UnderLowerDeck(Drawn d, double x, double z, double r)
        {
            var mine = Mine();
            foreach (int q in SegmentsNear(x - r - LowerDeckClear, x + r + LowerDeckClear, z - r - LowerDeckClear, z + r + LowerDeckClear))
            {
                var s = mine.SegItems[q];
                // Any bridge above ground (low ones too) and below this deck's level.
                if (s.Road == d || !s.Road.Bridge || s.Road.Y <= 0.8 || s.Road.Y >= d.Y - 0.5) continue;
                int start = s.ExactCount >= 3 ? s.Exact : s.Start, m = s.ExactCount >= 3 ? s.ExactCount : s.Count;
                if (InRing(mine.SegRings, start, m, x, z, r + LowerDeckClear)) return true;
            }
            return false;
        }

        /// <summary>A support keeps this far clear of a lower deck's edge.</summary>
        public const double LowerDeckClear = 0.3;

        /// <summary>
        /// The median under deck d near the place (px, pz): across the deck, out to the cap's reach (half + OffsetReach),
        /// the nearest point that is either clear ground between two carriageways running along the deck (the median
        /// itself, as between the two one-way halves of a dual carriageway), or a point on the along-running road(s) where
        /// a raised island leaves at least LaneMin of each carriageway clear. A pier stands there, its cap reaching under
        /// the deck; the island is drawn when needed.
        /// </summary>
        bool MedianNear(Drawn d, ChunkStreet st, KitWriter w, StreetScratch ss, double px, double pz, double dx, double dz, double half, double along, long way, bool keep, string tag)
        {
            double lx = -dz, lz = dx;
            for (double s = 0; s <= half + OffsetReach + 1e-9; s += PierSlideStep)
                for (int sign = 1; sign >= -1; sign -= 2)
                {
                    if (s == 0 && sign < 0) continue;
                    double x = px + lx * s * sign, z = pz + lz * s * sign;
                    if (!Owned(x, z) || (s > 0 && BuildingBetween(px, pz, x, z))) continue;
                    var g = GroundAt(d, ss, x, z, dx, dz, FlyPierRadius);
                    if (g == Ground.Clear && BetweenRoads(x, z, lx, lz, dx, dz))
                    {
                        st.FlyMedianGaps++;
                        FlyPier(d, st, w, ss, x, z, px, pz, dx, dz, along, way, keep, Tags(tag, "median-gap", InWater(ss, x, z) ? "water" : null));
                        return true;
                    }
                    if (g == Ground.AlongRoad && !IslandOverlaps(ss, x, z, dx, dz, MedianHalf, FlyPierRadius + MedianEnds)
                        && !IslandInGroundBuilding(x, z, dx, dz, MedianHalf, FlyPierRadius + MedianEnds) && MedianFits(x, z, dx, dz, out double lane))
                    {
                        Median(st, w, x, z, dx, dz, lane, keep);
                        ss.Islands.Add((x, z, dx, dz, MedianHalf, FlyPierRadius + MedianEnds));
                        st.FlyMedians++;
                        FlyPier(d, st, w, ss, x, z, px, pz, dx, dz, along, way, keep, Tags(tag, "median"));
                        return true;
                    }
                }
            return false;
        }

        /// <summary>Is there an at-grade carriageway running along the deck within 8 m on both sides of (x, z), across it (lx, lz)?</summary>
        bool BetweenRoads(double x, double z, double lx, double lz, double dx, double dz)
        {
            bool Side(int sign)
            {
                for (double t = FlyPierRadius + 0.25; t <= 8; t += 0.5)
                {
                    double qx = x + lx * t * sign, qz = z + lz * t * sign;
                    if (OnCarriageway(qx, qz, 0.28, 0) && !InAnyPad(qx, qz, 0.28, 0)) return !OnCrossingRoad(qx, qz, dx, dz, 0);
                }
                return false;
            }
            return Side(1) && Side(-1);
        }

        /// <summary>
        /// Can a median island (MedianHalf across, MedianEnds past the pier each way, along the deck) stand at (x, z) on
        /// the at-grade carriageways under it, all running along the deck (dx, dz), with every part of each carriageway
        /// left beside it at least LaneMin wide? On a one-way road one side may be covered instead (under IslandCovered left),
        /// as long as one LaneMin lane is kept; a two-way road keeps a lane each way. Gives the narrowest lane kept.
        /// </summary>
        bool MedianFits(double x, double z, double dx, double dz, out double lane) => MedianFits(x, z, dx, dz, MedianHalf, FlyPierRadius + MedianEnds, 0, out lane, out _);

        /// <summary>
        /// MedianFits for an island half wide across and reaching a past its centre along (dx, dz) each way (P-6: the
        /// viaduct's wider pier). With beside above 0 (P-6, the owner's "traffic passing on both sides"), every
        /// cross-section must also have a lane of at least LaneMin on each side of the island: the part of a carriageway
        /// under it, or a carriageway running along the deck whose edge is within beside of the island's. why says what
        /// stops it: "junction", "crossing road", "road end", "no road", "narrow" or "one side".
        /// </summary>
        bool MedianFits(double x, double z, double dx, double dz, double half, double a, double beside, out double lane, out string why, bool inChunk = false)
        {
            double sideL = double.MaxValue, sideR = double.MaxValue;
            lane = double.MaxValue;
            why = null;
            // The island lies along the deck. At its centre and its two ends, every carriageway under it: each must keep
            // its lane on the same side all along (the narrowest part either side over the three), none may cross the
            // deck or end under the island.
            var acc = medianParts ??= new Dictionary<Drawn, (double left, double right, bool oneWay)>();
            acc.Clear();
            for (int k = -1; k <= 1; k++)
            {
                // inChunk (P-5a, the continuous median): a cross-section past the chunk border is the neighbour's to judge.
                if (inChunk && k != 0 && !Inside(x + dx * a * k, z + dz * a * k)) continue;
                why = IslandAt(x + dx * a * k, z + dz * a * k, dx, dz, half, beside, inChunk);
                if (why != null) return false;
                sideL = Math.Min(sideL, islandSideL);
                sideR = Math.Min(sideR, islandSideR);
                foreach (var kv in medianRoads)
                    acc[kv.Key] = acc.TryGetValue(kv.Key, out var o)
                        ? (Math.Min(o.left, kv.Value.left), Math.Min(o.right, kv.Value.right), o.oneWay && kv.Value.oneWay)
                        : (kv.Value.left, kv.Value.right, kv.Value.oneWay);
            }
            why = "no road";
            if (acc.Count == 0) return false;
            why = "narrow";
            foreach (var e in acc.Values)
            {
                bool kept = false;
                for (int k = 0; k < 2; k++)
                {
                    double part = k == 0 ? e.left : e.right;
                    if (part < IslandCovered && e.oneWay) continue;
                    if (part < LaneMin) return false;
                    kept = true;
                    lane = Math.Min(lane, part);
                }
                if (!kept) return false;
            }
            why = "one side";
            if (beside > 0 && Math.Min(sideL, sideR) < LaneMin) return false;
            fitSides = Math.Min(sideL, sideR);
            fitOneWay = fitTwins = true;
            foreach (var e in acc.Values)
            {
                fitOneWay &= e.oneWay;
                fitTwins &= e.left < IslandCovered || e.right < IslandCovered;
            }
            why = null;
            return true;
        }

        /// <summary>After MedianFits succeeds: the narrower of the widest lanes either side (P-6, P-5a), whether every carriageway under the
        /// island is one-way, and whether each keeps its lanes on one side of it only, so that the island stands between twin carriageways
        /// and splits none (P-5a).</summary>
        double fitSides;
        bool fitOneWay, fitTwins;

        Dictionary<Drawn, (double left, double right, bool oneWay)> medianParts;

        /// <summary>
        /// One cross-section of an island half wide at (x, z), across the deck (dx, dz): every at-grade carriageway under
        /// it, in medianRoads with the parts of it left either side. Null if it can stand; else why not: in a junction pad,
        /// a road crossing the deck, or a road ending under the island. With beside above 0, also the widest lane on each
        /// side of the island (islandSideL, islandSideR, left and right of the deck's direction): a carriageway's part
        /// beyond the island, or a carriageway running along the deck whose edge is within beside of the island's. cutOn (P-5a):
        /// a road end cut on the chunk border is taken on straight past it.
        /// </summary>
        string IslandAt(double x, double z, double dx, double dz, double half, double beside = 0, bool cutOn = false)
        {
            islandSideL = islandSideR = 0;
            if (InAnyPad(x, z, 0.28, 0.3)) return "junction";
            var mine = Mine();
            double cos = Math.Cos(CrossAngle * Math.PI / 180);
            var near = medianRoads ??= new Dictionary<Drawn, (double d, double left, double right, double ux, double uz, bool past, bool oneWay)>();
            near.Clear();
            // Every at-grade carriageway whose drawn centre line comes within its half width plus MedianHalf of the point,
            // judged on its nearest segment that the point falls within; a segment the point is past is left to the next
            // segment of its road, unless it is the road's own end (not cut on the border): then the island would stand
            // over that road's mouth.
            foreach (int q in SegmentsNear(x - MedianReach, x + MedianReach, z - MedianReach, z + MedianReach))
            {
                var s = mine.SegItems[q];
                if (!SameLevel(s.Road.Y, 0.28) || s.Wedge) continue;
                var p = s.Road.Pts;
                int j = Math.Max(0, Math.Min(s.Seg, s.Road.N - 2));
                double ex = p[2 * j + 2] - p[2 * j], ez = p[2 * j + 3] - p[2 * j + 1], l = Math.Sqrt(ex * ex + ez * ez);
                if (l < 1e-9) continue;
                double raw = ((x - p[2 * j]) * ex + (z - p[2 * j + 1]) * ez) / (l * l);
                // cutOn (P-5a, the continuous median near the chunk border): past a road's end cut on the border the road
                // goes on in the neighbour; it is taken on straight.
                bool cut = cutOn && (raw < 0 && j == 0 && s.Road.StartOn || raw > 1 && j == s.Road.N - 2 && s.Road.EndOn);
                double t = cut ? raw : Math.Clamp(raw, 0, 1);
                double cx = p[2 * j] + ex * t - x, cz = p[2 * j + 1] + ez * t - z, d = Math.Sqrt(cx * cx + cz * cz);
                double h = s.Road.Halves[j] + (s.Road.Halves[j + 1] - s.Road.Halves[j]) * Math.Clamp(raw, 0, 1);
                if (d > h + half)
                {
                    // Beside the island, not under it: a carriageway along the deck counts for the lane on its side. Past a
                    // segment's end counts too where the road goes on (outside a bend no segment holds the point), not past its own end.
                    bool within = raw >= -1e-6 && raw <= 1 + 1e-6 || raw < 0 && j > 0 || raw > 1 && j < s.Road.N - 2 || cut;
                    if (beside > 0 && d <= h + half + beside && within && Math.Abs(ex * dx + ez * dz) / l > cos)
                    {
                        double o = ((x - p[2 * j]) * -ez + (z - p[2 * j + 1]) * ex) / l;
                        bool with = ex * dx + ez * dz > 0;
                        double pl = h - (o + half), pr = o - half + h; // the road beyond the island, on the road's left and right
                        double onL = with ? pl : pr, onR = with ? pr : pl;
                        if (onL > 0 && onL - 2 * h <= beside) islandSideL = Math.Max(islandSideL, Math.Min(onL, 2 * h));
                        if (onR > 0 && onR - 2 * h <= beside) islandSideR = Math.Max(islandSideR, Math.Min(onR, 2 * h));
                    }
                    continue;
                }
                bool past = raw < -1e-6 && j == 0 && !s.Road.StartOn || raw > 1 + 1e-6 && j == s.Road.N - 2 && !s.Road.EndOn;
                if ((raw < -1e-6 || raw > 1 + 1e-6) && !past && !cut) continue;
                if (Math.Abs(ex * dx + ez * dz) / l <= cos) return "crossing road"; // a crossing road under the island
                double off = ((x - p[2 * j]) * -ez + (z - p[2 * j + 1]) * ex) / l;
                bool oneWay = s.Road.Attrs != null && j < s.Road.Attrs.Length && s.Road.Attrs[j].Dir != 0;
                if (!near.TryGetValue(s.Road, out var e) || d < e.d) near[s.Road] = (d, h - (off + half), off - half + h, ex / l, ez / l, past, oneWay);
            }
            foreach (var e in near.Values)
            {
                if (e.past) return "road end";
                bool with = e.ux * dx + e.uz * dz > 0;
                islandSideL = Math.Max(islandSideL, with ? e.left : e.right);
                islandSideR = Math.Max(islandSideR, with ? e.right : e.left);
            }
            return null;
        }

        /// <summary>The widest lane either side of the last cross-section IslandAt judged (P-6).</summary>
        double islandSideL, islandSideR;

        /// <summary>P-6: a carriageway whose edge is this close to a viaduct island's counts as passing beside it (an open strip between twin carriageways).</summary>
        public const double IslandBeside = 1.0;

        Dictionary<Drawn, (double d, double left, double right, double ux, double uz, bool past, bool oneWay)> medianRoads; // made on first use: only chunks with a flyover need it

        /// <summary>A pier beside the deck line (rail-side or past an obstacle), up to the girder's half width plus OffsetReach out, with a cap reaching under the deck.</summary>
        bool Offset(Drawn d, ChunkStreet st, KitWriter w, StreetScratch ss, double px, double pz, double dx, double dz, double half, double along, long way, bool keep, string tag)
        {
            double lx = -dz, lz = dx;
            for (double s = FlyPierRadius + 0.5; s <= half + OffsetReach + 1e-9; s += PierSlideStep)
                for (int sign = 1; sign >= -1; sign -= 2)
                {
                    double x = px + lx * s * sign, z = pz + lz * s * sign;
                    if (!Owned(x, z) || GroundAt(d, ss, x, z, dx, dz, FlyPierRadius) != Ground.Clear || BuildingBetween(px, pz, x, z)) continue;
                    st.FlyOffsets++;
                    FlyPier(d, st, w, ss, x, z, px, pz, dx, dz, along, way, keep, Tags(tag, "offset", InWater(ss, x, z) ? "water" : null));
                    return true;
                }
            return false;
        }

        /// <summary>A portal frame: a leg either side on clear ground within PortalReach, and a cross-beam under the girder.</summary>
        bool Portal(Drawn d, ChunkStreet st, KitWriter w, StreetScratch ss, double px, double pz, double dx, double dz, double half, double along, long way, bool keep, string tag)
        {
            double lx = -dz, lz = dx;
            double FindLeg(int sign)
            {
                for (double s = Math.Max(1.0, FlyLegRadius + 0.5); s <= PortalReach + 1e-9; s += PierSlideStep)
                {
                    double x = px + lx * s * sign, z = pz + lz * s * sign;
                    if (Owned(x, z) && GroundAt(d, ss, x, z, dx, dz, FlyLegRadius) == Ground.Clear) return s;
                }
                return -1;
            }
            double sl = FindLeg(1), sr = FindLeg(-1);
            if (sl < 0 || sr < 0) return false;
            double ax = px + lx * sl, az = pz + lz * sl, bx = px - lx * sr, bz = pz - lz * sr;
            if (BuildingBetween(ax, az, bx, bz)) return false; // the beam would pass through a building
            double top = d.Y - FlyDepth(d.Y), beam = top - FlyBeamDepth;
            Column(w, ax, az, beam, FlyLegRadius, dx, dz, d.Y, false);
            Column(w, bx, bz, beam, FlyLegRadius, dx, dz, d.Y, false);
            // The beam runs from leg to leg, past each by its radius.
            double ex = (bx - ax) / (sl + sr), ez = (bz - az) / (sl + sr);
            w.Beam(ax - ex * FlyLegRadius, az - ez * FlyLegRadius, bx + ex * FlyLegRadius, bz + ez * FlyLegRadius, 0.75, beam, top, beam, top, Pack(FlyConcrete));
            st.FlyPortals++;
            st.FlySupports++;
            Placed(ss, ax, az, FlyLegRadius, along);
            ss.FlyPlaced.Add((bx, bz, FlyLegRadius));
            if (keep)
            {
                string why = Tags(tag, "portal");
                st.Items.Add(new ChunkStreet.Item { Kind = ChunkStreet.Kind.FlyLeg, X = ax, Z = az, Y = 0, Top = beam, Way = way, Why = Tags(why, InWater(ss, ax, az) ? "water" : null) });
                st.Items.Add(new ChunkStreet.Item { Kind = ChunkStreet.Kind.FlyLeg, X = bx, Z = bz, Y = 0, Top = beam, Way = way, Why = Tags(why, InWater(ss, bx, bz) ? "water" : null) });
            }
            return true;
        }

        /// <summary>Is a building's footprint on the line from a to b (sampled every metre)?</summary>
        bool BuildingBetween(double ax, double az, double bx, double bz)
        {
            double l = Dist(ax, az, bx, bz);
            int n = Math.Max(1, (int)Math.Ceiling(l));
            for (int i = 1; i < n; i++)
                if (InBuilding(ax + (bx - ax) * i / n, az + (bz - az) * i / n)) return true;
            return false;
        }

        static void Placed(StreetScratch ss, double x, double z, double r, double along)
        {
            ss.FlyPlaced.Add((x, z, r));
            ss.Cuts.Add(along);
        }

        static uint Pack(uint rgb) => WorldMaterials.Pack(rgb, WorldMaterial.Concrete);

        // ---- Pieces ----

        /// <summary>
        /// A pier at (x, z) under deck d, whose centre line is at (cx, cz): pile cap, column, drain pipe, and a cap. Near the
        /// centre line the cap widens to under the girder (a hammerhead); further out it is an outrigger reaching under the
        /// girder's far web.
        /// </summary>
        void FlyPier(Drawn d, ChunkStreet st, KitWriter w, StreetScratch ss, double x, double z, double cx, double cz, double dx, double dz, double along, long way, bool keep, string why)
        {
            double half = HalfAt(d, along), top = d.Y - FlyDepth(d.Y), capBase = top - FlyCapDepth(d.Y);
            double lx = -dz, lz = dx, off = (x - cx) * lx + (z - cz) * lz;
            Column(w, x, z, capBase, FlyPierRadius, dx, dz, d.Y, true);
            if (Math.Abs(off) <= 0.3 * half)
            {
                double r = FlyPierRadius * 0.95, wide = Math.Max(r, 0.55 * half + 0.25);
                double ox = cx - x, oz = cz - z; // the cap's top is centred under the deck
                Span<double> k = stackalloc double[]
                {
                    x + dx * r + lx * r, capBase, z + dz * r + lz * r, x - dx * r + lx * r, capBase, z - dz * r + lz * r,
                    x - dx * r - lx * r, capBase, z - dz * r - lz * r, x + dx * r - lx * r, capBase, z + dz * r - lz * r,
                    cx + dx * 1.0 + lx * wide, top, cz + dz * 1.0 + lz * wide, cx - dx * 1.0 + lx * wide, top, cz - dz * 1.0 + lz * wide,
                    cx - dx * 1.0 - lx * wide, top, cz - dz * 1.0 - lz * wide, cx + dx * 1.0 - lx * wide, top, cz + dz * 1.0 - lz * wide,
                };
                w.Hex(k, Pack(FlyConcrete));
            }
            else
            {
                double sign = Math.Sign(off), fx = cx - lx * sign * 0.55 * half, fz = cz - lz * sign * 0.55 * half;
                w.Beam(x + lx * sign * FlyPierRadius, z + lz * sign * FlyPierRadius, fx, fz, 1.0, capBase, top, capBase + 0.35, top, Pack(FlyConcrete));
            }
            st.FlyPiers++;
            st.FlySupports++;
            if (why != null && why.Contains("water")) st.FlyInWater++;
            Placed(ss, x, z, FlyPierRadius, along);
            if (keep) st.Items.Add(new ChunkStreet.Item { Kind = ChunkStreet.Kind.FlyPier, X = x, Z = z, Y = 0, Top = top, Way = way, Why = why });
        }

        /// <summary>A round column from a pile cap at the ground up to y, with a drain pipe down its side when drain (piers, not portal legs).</summary>
        static void Column(KitWriter w, double x, double z, double y, double r, double dx, double dz, double deckY, bool drain)
        {
            w.Post(x, z, -0.3, 0.32, dx, dz, r + CapRim, r + CapRim, r + CapRim - 0.1, r + CapRim - 0.1, Pack(PileCap));
            w.Tube(x, 0.32, z, x, y, z, r, r * 0.95, 12, Pack(FlyConcrete));
            if (!drain) return;
            double lx = -dz, lz = dx, ox = x + lx * (r + 0.12), oz = z + lz * (r + 0.12);
            w.Tube(ox, 0.32, oz, ox, deckY - FlyDepth(deckY) + 0.2, oz, 0.07, 0.07, 6, WorldMaterials.Pack(DrainPipe, WorldMaterial.Metal));
        }

        /// <summary>A raised median island under a pier, along the deck (ux, uz): a kerb painted yellow, a concrete top.</summary>
        static void Median(ChunkStreet st, KitWriter w, double x, double z, double ux, double uz, double lane, bool keep) =>
            Median(st, w, x, z, ux, uz, MedianHalf, FlyPierRadius + MedianEnds, lane, keep);

        /// <summary>An island half wide across and reaching a past (x, z) each way along (ux, uz).</summary>
        static void Median(ChunkStreet st, KitWriter w, double x, double z, double ux, double uz, double half, double a, double lane, bool keep)
        {
            w.Post(x, z, 0.27, MedianTop - 0.04, ux, uz, a, half, a, half, WorldMaterials.Pack(DividerYellow, WorldMaterial.Paint));
            w.Post(x, z, MedianTop - 0.04, MedianTop, ux, uz, a - 0.12, half - 0.12, a - 0.15, half - 0.15, Pack(MedianTopColour));
            if (keep) st.Items.Add(new ChunkStreet.Item { Kind = ChunkStreet.Kind.Median, X = x, Z = z, Y = 0.28, Top = MedianTop, Why = "lanes " + lane.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) });
        }

        // ---- The girder ----

        /// <summary>
        /// The closed box girder under deck d, inside the chunk: from just below its surface down the fascias to a narrower
        /// soffit, cut at every support into segments with a JointGap joint, and at any jump in the deck's width.
        /// </summary>
        void FlyGirder(Drawn d, ChunkStreet st, KitWriter w, StreetScratch ss)
        {
            var cuts = ss.Cuts;
            cuts.Sort();
            int n = d.N;
            double total = d.Arc[n - 1];
            var marks = ss.Marks;
            marks.Clear();
            marks.Add((0, 0));
            foreach (double c in cuts) if (c > JointGap && c < total - JointGap) marks.Add((c, JointGap));
            for (int i = 1; i + 1 < n; i++)
                if (Math.Abs(d.Halves[i] - d.Halves[i - 1]) > 0.05) marks.Add((d.Arc[i], 0));
            marks.Add((total, 0));
            marks.Sort((p, q) => p.at.CompareTo(q.at));
            uint concrete = Pack(FlyConcrete), soffit = Pack(FlySoffit);
            double depth = FlyDepth(d.Y);
            for (int i = 0; i + 1 < marks.Count; i++)
            {
                double a = marks[i].at + marks[i].gap, b = marks[i + 1].at - marks[i + 1].gap;
                if (b - a < 0.1) continue;
                SubPath(d, a, b, ss.Path);
                double W = Math.Max(0.5, HalfAt(d, (a + b) / 2) - FlyGirderInset);
                Span<double> profile = stackalloc double[] { W, -0.05, -W, -0.05, -W, -0.5, -0.55 * W, -depth, 0.55 * W, -depth, W, -0.5 };
                Span<uint> colours = stackalloc uint[] { concrete, concrete, soffit, soffit, soffit, concrete };
                bool capA = !(a <= 1e-9 && d.StartOn), capB = !(b >= total - 1e-9 && d.EndOn);
                LeftNormals(ss.Path, ss.Left);
                w.Loft(profile, colours, ss.Path, ss.Left, capA, capB, concrete, ss.Flat, ss.Tris);
                st.FlySegments++;
                st.FlyLength += b - a;
            }
        }

        /// <summary>Points of d from a to b along it (metres), as (x, deck top, z) triples.</summary>
        static void SubPath(Drawn d, double a, double b, List<double> path)
        {
            path.Clear();
            DrawnAt(d, a, out double x, out double z, out _, out _, out _);
            path.Add(x); path.Add(d.Y); path.Add(z);
            for (int i = 1; i + 1 < d.N; i++)
                if (d.Arc[i] > a + 1e-6 && d.Arc[i] < b - 1e-6) { path.Add(d.Pts[2 * i]); path.Add(d.Y); path.Add(d.Pts[2 * i + 1]); }
            DrawnAt(d, b, out x, out z, out _, out _, out _);
            path.Add(x); path.Add(d.Y); path.Add(z);
        }

        /// <summary>The point at distance s along d, its unit direction, and its segment.</summary>
        static void DrawnAt(Drawn d, double s, out double x, out double z, out double dx, out double dz, out int seg)
        {
            int n = d.N;
            seg = n - 2;
            for (int i = 0; i + 1 < n; i++) if (d.Arc[i + 1] >= s) { seg = i; break; }
            double ax = d.Pts[2 * seg], az = d.Pts[2 * seg + 1], bx = d.Pts[2 * seg + 2], bz = d.Pts[2 * seg + 3];
            double l = d.Arc[seg + 1] - d.Arc[seg], t = l > 1e-9 ? Math.Clamp((s - d.Arc[seg]) / l, 0, 1) : 0;
            x = ax + (bx - ax) * t;
            z = az + (bz - az) * t;
            double len = Math.Sqrt((bx - ax) * (bx - ax) + (bz - az) * (bz - az));
            dx = len > 1e-9 ? (bx - ax) / len : 1;
            dz = len > 1e-9 ? (bz - az) / len : 0;
        }

        static double HalfAt(Drawn d, double s)
        {
            DrawnAt(d, s, out _, out _, out _, out _, out int seg);
            double l = d.Arc[seg + 1] - d.Arc[seg], t = l > 1e-9 ? Math.Clamp((s - d.Arc[seg]) / l, 0, 1) : 0;
            return d.Halves[seg] + (d.Halves[seg + 1] - d.Halves[seg]) * t;
        }

        static double LineDistance(double[] l, double x, double z)
        {
            double best = double.MaxValue;
            for (int i = 0; i + 3 < l.Length; i += 2)
            {
                double ax = l[i], az = l[i + 1], ex = l[i + 2] - ax, ez = l[i + 3] - az, ll = ex * ex + ez * ez;
                double t = ll > 1e-12 ? Math.Clamp(((x - ax) * ex + (z - az) * ez) / ll, 0, 1) : 0;
                double qx = ax + ex * t - x, qz = az + ez * t - z;
                best = Math.Min(best, Math.Sqrt(qx * qx + qz * qz));
            }
            return best;
        }

        /// <summary>Is the disc (x, z, r) inside a ring or within r of its edge?</summary>
        static bool InOrNear(List<(double x, double z)> ring, double x, double z, double r)
        {
            bool inside = false;
            for (int i = 0, j = ring.Count - 1; i < ring.Count; j = i++)
            {
                var (ax, az) = ring[i];
                var (bx, bz) = ring[j];
                if ((az > z) != (bz > z) && x < (bx - ax) * (z - az) / (bz - az) + ax) inside = !inside;
                double ex = bx - ax, ez = bz - az, ll = ex * ex + ez * ez;
                double t = ll < 1e-12 ? 0 : Math.Clamp(((x - ax) * ex + (z - az) * ez) / ll, 0, 1);
                double qx = ax + ex * t - x, qz = az + ez * t - z;
                if (qx * qx + qz * qz < r * r) return true;
            }
            return inside;
        }

        /// <summary>Is the disc (x, z, r) on a bus stop's pad (the quads the sidewalks drew, with 0.2 m to spare)?</summary>
        bool OnStopPad(double x, double z, double r)
        {
            var ring = stopRing ??= new List<(double x, double z)>(4) { (0, 0), (0, 0), (0, 0), (0, 0) };
            foreach (var q in Mine().StopQuads)
            {
                ring[0] = (q.ax, q.az); ring[1] = (q.bx, q.bz); ring[2] = (q.cx, q.cz); ring[3] = (q.dx, q.dz);
                if (InOrNear(ring, x, z, r + 0.2)) return true;
            }
            return false;
        }

        List<(double x, double z)> stopRing;

        static bool InPolygon(double[] ring, double x, double z)
        {
            bool inside = false;
            int n = ring.Length / 2;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                double xi = ring[2 * i], zi = ring[2 * i + 1], xj = ring[2 * j], zj = ring[2 * j + 1];
                if ((zi > z) != (zj > z) && x < (xj - xi) * (z - zi) / (zj - zi) + xi) inside = !inside;
            }
            return inside;
        }
    }
}
