// Street furniture and structures for one chunk (Milestone 2, Farmgate validation), built by
// ChunkRoads.Build after the road mesh, so placement can ask where the drawn carriageways, junction
// pads, sidewalk bands and buildings are. The result is ChunkStreet: one mesh's arrays in ranges,
// range 0 the structures (Structures.cs: viaduct, footbridges, dividers), then a near and a far
// range for each 125 m cell of the chunk, which the streamer draws by distance (StreetLod).
//
// What goes where:
// - Utility poles: the slice has no mapped power lines (DETAILING_STANDARDS section 9), so poles
//   follow a labelled rule. One side of each at-grade primary, secondary, tertiary, residential,
//   unclassified and living-street road (the side by a hash of the way), every PoleSpacing metres,
//   just behind the kerb where that side has a sidewalk, beyond the drain where it has a drain, else
//   PoleOffset from the edge. Spacing is counted from a road end on the chunk border, so the poles
//   of a road on both sides of the border keep step; the span over the border is drawn in halves,
//   each chunk to its border, where the cable is level. A pole that would stand on a carriageway,
//   a junction pad or in a building is moved up to PoleSlide metres along the road, else left out;
//   one closer than PoleGap to a pole already placed is left out. One pole in two (by hash) carries
//   a lamp arm; one in ten a transformer.
// - Cables run between neighbouring poles of a road and side, unless the chord passes over a
//   building footprint (then the span is left out and counted).
// - Street lights stand at every mapped lamp point, turned towards the nearest at-grade road. One
//   mapped inside a carriageway's (mostly estimated, K-31) width is moved straight out to just
//   behind its kerb and counted with the distance, LampMove metres at most; one that would go
//   further or still has no room, or stands in a building, is left out and counted.
// - Trees stand at every mapped tree point in one of three forms (by a hash of the position; no
//   species is mapped), at the mapped height when there is one; like lamps, never moved.
// - The rooftop kit (D-06b, Rooftops.cs) stands on the roofs BuildingSkin marks, in the same cells: far in
//   the far ranges; near it is not in the near ranges but kept as items (RoofKit) and built per cell when
//   the cell comes near (BuildNearKit, K-45).
// Everything is cut to its own chunk: an item belongs to the chunk its foot is in.
using System;
using System.Collections.Generic;

namespace Dad.World
{
    /// <summary>The street furniture and structures of a chunk, as mesh arrays in ranges.</summary>
    public sealed partial class ChunkStreet
    {
        public const int Cells = 4;
        public const double CellSize = 125;
        /// <summary>Range 0 is the structures; then for cell c, near is 1 + 2c and far 2 + 2c.</summary>
        public const int Ranges = 1 + 2 * Cells * Cells;

        /// <summary>The mesh arrays. Built pooled (MeshArrays), they are longer than the mesh: use the counts.</summary>
        public float[] Positions;
        public uint[] Colors;
        public int[] Indices;
        public int VertexCount, IndexCount;
        /// <summary>Per range: first vertex and count, first index and count. Indices are relative to the range's first vertex.</summary>
        public readonly int[] FirstVertex = new int[Ranges], Vertices = new int[Ranges], FirstIndex = new int[Ranges], IndexCounts = new int[Ranges];
        /// <summary>Lowest and highest corner of the structures range (x, y, z), for its bounds.</summary>
        public readonly float[] StructMin = { 0, 0, 0 }, StructMax = { 0, 0, 0 };
        /// <summary>Lowest and highest corner of each range (x, y, z at 3 * range), for its bounds: a cell's
        /// items stand in it, but a cable span from a pole near its edge reaches on into the next cell.</summary>
        public readonly float[] RangeMin = new float[3 * Ranges], RangeMax = new float[3 * Ranges];

        public int Poles, PolesSkipped, PolesCrowded, PoleLamps, Transformers, Spans, HalfSpans, SpansBlocked;
        public int Lamps, LampsMoved, LampsOnRoad, LampsInBuilding, Trees, TreesOnRoad, TreesInBuilding;
        /// <summary>Lamp columns placed by rule on the big roads (D-13); none of these is mapped.</summary>
        public int RoadLamps;
        public int ViaductRuns, ViaductLength, Piers, PiersOnRoad, PiersInJunction;
        /// <summary>Viaduct piers inside a building on the ground (a conflict), and those rising into a raised building such as a
        /// station body (counted apart, K-37 P-3). A pier is in at most one of the two.</summary>
        public int PiersInBuilding, PiersUnderRaised;
        /// <summary>P-6: viaduct piers on a carriageway given a median island; for those left without one (PiersOnRoad), how many for each reason (null when none).</summary>
        public int PiersOnIsland, DividerIslandGaps;
        /// <summary>P-5a: viaduct piers on a carriageway that stand on a continuous median under the deck; the median's pieces (one per
        /// unbroken stretch in the chunk) and their length in metres; with keepMarks, each piece's straight parts (local metres).</summary>
        public int PiersOnMedianRun, MedianRuns;
        public double MedianRunLength;
        public List<(double ax, double az, double bx, double bz)> MedianRunParts;
        public Dictionary<string, int> IslandRefusals;
        /// <summary>P-2: piers slid along the deck off a junction pad or crossing road, grid places over a crossing road left out (spanned), and grid places kept on a pad or crossing road because the span before was already left out.</summary>
        public int PiersSlid, PiersOverRoad, PiersKept;
        public int FootDecks, Stairs, StairsUnattached, Columns, ColumnsSkipped, DividerPieces;
        /// <summary>Flyovers (D-11a, Flyovers.cs): decks, girder segments and their length; supports (piers, offset piers, portals), of which slid (and of those, slid further than PierSlideMax off the grid), added to fill a long gap, on a median island, in the gap between twin carriageways, offset, portal and in water; grid places spanned.</summary>
        public int FlyDecks, FlySegments, FlySupports, FlyPiers, FlySlid, FlySlidFar, FlyFilled, FlyMedians, FlyMedianGaps, FlyOffsets, FlyPortals, FlyInWater, FlySpanned;
        public double FlyLength;
        /// <summary>The rooftop kit (D-06b, Rooftops.cs): roofs furnished, items placed, and items with no room.</summary>
        public int Roofs, StairRooms, LiftRooms, Tanks, Dishes, AcUnits, RoofNoRoom;
        /// <summary>Vertices and indices the rooftop kit adds, near (0) and far (1). Build counts far only; CountNearKit fills near.</summary>
        public readonly int[] RoofVertices = new int[2], RoofIndices = new int[2];

        public enum Kind : byte { Pole, Lamp, Tree, Pier, Column, StairTop, StairFoot, Divider, Deck, FlyPier, FlyLeg, Median, StairRoom, Tank, Dish, Ac }

        /// <summary>A placed (or, with Skipped, refused) item, kept for tests when Build keeps marks.</summary>
        public struct Item
        {
            public Kind Kind;
            public double X, Z, Y, Top;
            public long Way;
            public bool Skipped;
            public string Why;
        }

        /// <summary>A cable as drawn, for tests: its first and last point and its lowest height.</summary>
        public struct Wire
        {
            public double Ax, Ay, Az, Bx, By, Bz, Low;
            public bool Half;
        }

        public List<Item> Items;
        /// <summary>What stands on the ground where a road may run (F-01): a box centred at X, Z, Along metres each way along (Ux, Uz), Half across.</summary>
        public struct Block { public double X, Z, Ux, Uz, Along, Half; }
        /// <summary>Viaduct and flyover piers, median islands and the median of this chunk, local metres; the lane graph keeps clear of them.</summary>
        public Block[] Blocks = Array.Empty<Block>();
        /// <summary>Something a vehicle stops against (E-04): a box centred at X, Z, Along metres each way along (Ux, Uz), Half across, from Y up to Top.</summary>
        public struct Solid { public double X, Z, Ux, Uz, Along, Half, Y, Top; }
        /// <summary>Piers, median parts, columns, dividers, poles, lamp columns and tree trunks, local metres (ChunkCollision).</summary>
        public Solid[] Solids = Array.Empty<Solid>();
        /// <summary>A lamp's head, local metres (D-13), and which way its arm reaches.</summary>
        public struct LampHead { public double X, Y, Z, Fx, Fz; public long Seed; }
        /// <summary>Every street lamp's head, on columns and on utility poles.</summary>
        public LampHead[] LampHeads = Array.Empty<LampHead>();
        public List<Wire> Wires;
        bool pooled;

        public static int Range(int cell, int lod) => 1 + 2 * cell + lod;

        public static int CellOf(double x, double z)
        {
            int cx = Math.Clamp((int)Math.Floor(x / CellSize), 0, Cells - 1), cz = Math.Clamp((int)Math.Floor(z / CellSize), 0, Cells - 1);
            return cx + cz * Cells;
        }

        internal void Finish(List<float> p, List<uint> col, List<int> idx, bool pooled)
        {
            this.pooled = pooled;
            VertexCount = col.Count;
            IndexCount = idx.Count;
            Positions = MeshArrays.Copy(p, pooled);
            Colors = MeshArrays.Copy(col, pooled);
            Indices = MeshArrays.Copy(idx, pooled);
            for (int range = 0; range < Ranges; range++)
            {
                int first = FirstVertex[range], end = first + Vertices[range], o = 3 * range;
                for (int k = 0; k < 3; k++) { RangeMin[o + k] = float.MaxValue; RangeMax[o + k] = float.MinValue; }
                for (int v = first; v < end; v++)
                    for (int k = 0; k < 3; k++)
                    {
                        RangeMin[o + k] = Math.Min(RangeMin[o + k], Positions[3 * v + k]);
                        RangeMax[o + k] = Math.Max(RangeMax[o + k], Positions[3 * v + k]);
                    }
                if (Vertices[range] == 0) for (int k = 0; k < 3; k++) RangeMin[o + k] = RangeMax[o + k] = 0;
            }
            for (int k = 0; k < 3; k++) { StructMin[k] = RangeMin[k]; StructMax[k] = RangeMax[k]; }
        }

        /// <summary>Hands pooled mesh arrays back once the meshes are uploaded; the arrays are gone after.</summary>
        public void Release()
        {
            if (!pooled) return;
            pooled = false;
            MeshArrays.Return(Positions);
            MeshArrays.Return(Colors);
            MeshArrays.Return(Indices);
            Positions = null;
            Colors = null;
            Indices = null;
        }
    }

    /// <summary>Which level of detail a street cell is drawn at, by its distance from the camera.</summary>
    public static class StreetLod
    {
        public const int Near = 0, Far = 1, Hidden = 2;
        /// <summary>Near to NearDistance (the shadow distance), far to FarDistance; Hysteresis keeps a cell from flickering on the line.</summary>
        public const double NearDistance = 150, FarDistance = 450, Hysteresis = 10;

        public static int Tier(double distance, int current)
        {
            double near = NearDistance + (current == Near ? Hysteresis : 0);
            double far = FarDistance + (current != Hidden ? Hysteresis : 0);
            return distance < near ? Near : distance < far ? Far : Hidden;
        }

        /// <summary>Distance on the ground plan from (x, z) to a cell's square, from its south-west corner (cx, cz).</summary>
        public static double ToCell(double x, double z, double cx, double cz)
        {
            double dx = Math.Max(0, Math.Max(cx - x, x - (cx + ChunkStreet.CellSize)));
            double dz = Math.Max(0, Math.Max(cz - z, z - (cz + ChunkStreet.CellSize)));
            return Math.Sqrt(dx * dx + dz * dz);
        }
    }

    public sealed partial class ChunkRoads
    {
        public const double PoleSpacing = 35, PoleOffset = 0.5, PoleBehindKerb = 0.45, PoleSlide = 4, PoleGap = 6, PoleMargin = 0.3;
        /// <summary>How far a lamp mapped on a carriageway may be moved out to the kerb; further and the lamp is left out.</summary>
        public const double LampMove = 6;
        public const double TreeMargin = 0.2, MinTree = 4, MaxTree = 22;

        /// <summary>The chunk's street furniture and structures (Milestone 2), built with the roads.</summary>
        public ChunkStreet Street;

        struct Prop
        {
            public ChunkStreet.Kind Kind;
            public double X, Z, Y, Ax, Az, H, L, W;
            public int Side, Form;
            public bool Lamp, Transformer;
            public long Seed;
            public uint Colour;
        }

        struct CableSpan
        {
            public double Ax, Az, Aax, Aaz, Bx, Bz, Bax, Baz, Y;
            public bool Half;
        }

        sealed class StreetScratch
        {
            public readonly List<Prop> Props = new List<Prop>(512);
            /// <summary>Columns and divider pieces met while drawing them (Structures.cs), for ChunkStreet.Solids.</summary>
            public readonly List<ChunkStreet.Solid> Solids = new List<ChunkStreet.Solid>(128);
            public readonly List<CableSpan> CableSpans = new List<CableSpan>(256);
            public readonly List<int> Order = new List<int>(512);
            public readonly List<double> Flat = new List<double>(64), Path = new List<double>(512), Left = new List<double>(512), Run = new List<double>(1536);
            public readonly List<int> Tris = new List<int>(64);
            public readonly List<(double s, int pole)> Placed = new List<(double s, int pole)>(32);
            /// <summary>Piers placed in this chunk's viaduct pass, and its neighbours' as seen from here (Structures.cs).</summary>
            public readonly List<(double x, double z)> PiersPlaced = new List<(double x, double z)>(64);
            /// <summary>Median islands built in this chunk (P-6): centre, direction along, half width, reach past the centre.</summary>
            public readonly List<(double x, double z, double ux, double uz, double half, double a)> Islands = new List<(double, double, double, double, double, double)>(16);
            /// <summary>P-5a: the continuous median's straight parts built in this chunk (also in Islands), and which deck points pass its test.</summary>
            public readonly List<(double x, double z, double ux, double uz, double half, double a)> Runs = new List<(double, double, double, double, double, double)>(64);
            public readonly List<bool> RunPass = new List<bool>(512);
            public readonly List<(double x, double z, double r)> FlyPlaced = new List<(double x, double z, double r)>(64);
            /// <summary>Gap filling: the cuts in order, and the targets tried (FillGaps).</summary>
            public readonly List<double> Sorted = new List<double>(64);
            public readonly HashSet<double> Tried = new HashSet<double>();
            /// <summary>Where the chunk border cuts an elevated deck of this chunk (local metres), and how far along the border its keep-out reaches.</summary>
            public readonly List<(double x, double z, double reach)> FlyBorderCuts = new List<(double x, double z, double reach)>(16);
            public readonly List<double> Cuts = new List<double>(32);
            public readonly List<(double at, double gap)> Marks = new List<(double at, double gap)>(64);
            public readonly List<double[]> GradeRails = new List<double[]>(8), WaterRings = new List<double[]>(8);
            public readonly List<(double[] line, double half)> CanalLines = new List<(double[] line, double half)>(8);
            public readonly List<ChunkStreet.RoofItem> Kit = new List<ChunkStreet.RoofItem>(256);
        }

        [ThreadStatic] static StreetScratch streetScratch;

        static readonly HashSet<string> PoleClasses = new HashSet<string> { "primary", "secondary", "tertiary", "residential", "unclassified", "living_street" };
        static readonly HashSet<string> RoadLampClasses = new HashSet<string> { "trunk", "trunk_link", "motorway", "motorway_link" };
        public const double RoadLampSpacing = 40, RoadLampGap = 12, RoadLampBorder = 6;
        /// <summary>Why a lamp stands where no lamp is mapped.</summary>
        public const string RuleLamp = "road lamp, by rule";

        /// <summary>Builds the street furniture and structures into the (empty) lists, after the road mesh is copied out.</summary>
        ChunkStreet BuildStreet(DadcChunk c, List<Drawn> drawn, List<Pad> pads, List<float> p, List<uint> col, List<int> idx, bool pooled, bool keep)
        {
            var st = new ChunkStreet();
            if (keep) { st.Items = new List<ChunkStreet.Item>(); st.Wires = new List<ChunkStreet.Wire>(); st.MedianRunParts = new List<(double, double, double, double)>(); }
            var ss = streetScratch ??= new StreetScratch();
            ss.Props.Clear();
            ss.Solids.Clear();
            ss.CableSpans.Clear();
            p.Clear();
            col.Clear();
            idx.Clear();
            var w = new KitWriter(p, col, idx);
            this.pads = pads;

            // Range 0: structures.
            w.Base = 0;
            Viaduct(c, st, w, ss, keep);
            Flyovers(c, drawn, st, w, ss, keep);
            Footbridges(c, st, w, ss, keep);
            Dividers(st, w, ss, keep);
            st.Vertices[0] = col.Count;
            st.IndexCounts[0] = idx.Count;

            PlacePoles(drawn, st, ss, keep);
            PlaceLamps(c, drawn, st, ss, keep);
            PlaceRoadLamps(drawn, st, ss, keep);
            PlaceTrees(c, st, ss, keep);
            PlaceRoofs(c, st, ss.Props, keep);

            // Cells, near then far. The rooftop kit is drawn far here; near, its items are kept by cell (RoofKit).
            var order = ss.Order;
            var kit = ss.Kit;
            kit.Clear();
            Span<double> pts = stackalloc double[3 * 13];
            for (int cell = 0; cell < ChunkStreet.Cells * ChunkStreet.Cells; cell++)
            {
                order.Clear();
                for (int i = 0; i < ss.Props.Count; i++) if (ChunkStreet.CellOf(ss.Props[i].X, ss.Props[i].Z) == cell) order.Add(i);
                st.RoofKitStart[cell] = kit.Count;
                for (int lod = 0; lod < 2; lod++)
                {
                    int range = ChunkStreet.Range(cell, lod);
                    st.FirstVertex[range] = col.Count;
                    st.FirstIndex[range] = idx.Count;
                    w.Base = col.Count;
                    foreach (int i in order)
                    {
                        var q = ss.Props[i];
                        if (q.Kind >= ChunkStreet.Kind.StairRoom)
                        {
                            var item = new ChunkStreet.RoofItem { Kind = q.Kind, X = q.X, Y = q.Y, Z = q.Z, Ax = q.Ax, Az = q.Az, L = q.L, W = q.W, H = q.H, Colour = q.Colour };
                            if (lod == 0) { kit.Add(item); continue; }
                            int v0 = col.Count, i0 = idx.Count;
                            DrawRoofItem(w, item, lod);
                            st.RoofVertices[lod] += col.Count - v0;
                            st.RoofIndices[lod] += idx.Count - i0;
                            continue;
                        }
                        switch (q.Kind)
                        {
                            case ChunkStreet.Kind.Pole: StreetKit.UtilityPole(w, q.X, q.Y, q.Z, q.Ax, q.Az, q.Side, q.Lamp, q.Transformer, lod); break;
                            case ChunkStreet.Kind.Lamp: StreetKit.StreetLight(w, q.X, q.Y, q.Z, q.Ax, q.Az, lod); break;
                            case ChunkStreet.Kind.Tree: StreetKit.Tree(w, q.X, q.Y, q.Z, q.H, q.Form, q.Seed, lod); break;
                        }
                    }
                    if (lod == 0)
                        foreach (var sp in ss.CableSpans)
                        {
                            if (ChunkStreet.CellOf(sp.Ax, sp.Az) != cell) continue;
                            for (int k = 0; k < StreetKit.Anchors; k++)
                            {
                                StreetKit.Anchor(k, sp.Ax, sp.Y, sp.Az, sp.Aax, sp.Aaz, out double ax, out double ay, out double az, out double r, out double sag, out uint colour);
                                StreetKit.Anchor(k, sp.Bx, sp.Y, sp.Bz, sp.Bax, sp.Baz, out double bx, out double by, out double bz, out _, out _, out _);
                                double len = Math.Sqrt((bx - ax) * (bx - ax) + (bz - az) * (bz - az));
                                int n = StreetKit.Catenary(pts, ax, ay, az, bx, by, bz, 0.25 + sag * len, sp.Half ? 6 : 12, sp.Half);
                                w.Cable(pts.Slice(0, 3 * n), r, colour);
                                if (keep && k == 0)
                                    st.Wires.Add(new ChunkStreet.Wire { Ax = pts[0], Ay = pts[1], Az = pts[2], Bx = pts[3 * n - 3], By = pts[3 * n - 2], Bz = pts[3 * n - 1], Low = Low(pts, n), Half = sp.Half });
                            }
                        }
                    st.Vertices[range] = col.Count - st.FirstVertex[range];
                    st.IndexCounts[range] = idx.Count - st.FirstIndex[range];
                }
            }
            st.RoofKitStart[ChunkStreet.Cells * ChunkStreet.Cells] = kit.Count;
            st.KeepRoofKit(kit, pooled);
            CollectBlocks(st, ss);
            CollectSolids(st, ss);
            CollectLamps(st, ss);
            st.Finish(p, col, idx, pooled);
            this.pads = null;
            Mine().SegItems.Clear();
            all = null;
            return st;
        }

        /// <summary>Piers, flyover supports, islands and median parts as blocks (F-01).</summary>
        static void CollectBlocks(ChunkStreet st, StreetScratch ss)
        {
            var b = new ChunkStreet.Block[ss.PiersPlaced.Count + ss.FlyPlaced.Count + ss.Islands.Count + ss.Runs.Count];
            int n = 0;
            foreach (var (x, z) in ss.PiersPlaced) b[n++] = new ChunkStreet.Block { X = x, Z = z, Ux = 1, Along = PierRadius, Half = PierRadius };
            foreach (var (x, z, r) in ss.FlyPlaced) b[n++] = new ChunkStreet.Block { X = x, Z = z, Ux = 1, Along = r, Half = r };
            foreach (var o in ss.Islands) b[n++] = new ChunkStreet.Block { X = o.x, Z = o.z, Ux = o.ux, Uz = o.uz, Along = o.a, Half = o.half };
            foreach (var o in ss.Runs) b[n++] = new ChunkStreet.Block { X = o.x, Z = o.z, Ux = o.ux, Uz = o.uz, Along = o.a, Half = o.half };
            st.Blocks = b;
        }

        // How much of an item a vehicle meets: the foot of it, not what stands above the roof.
        const double SolidHeight = 4, PoleSolid = 0.13, LampSolid = 0.1, TrunkSolid = 0.2;

        /// <summary>The heads of the lamps placed (D-13): where they sit is StreetKit's, from what was drawn.</summary>
        static void CollectLamps(ChunkStreet st, StreetScratch ss)
        {
            int n = 0;
            foreach (var q in ss.Props) if (q.Kind == ChunkStreet.Kind.Lamp || (q.Kind == ChunkStreet.Kind.Pole && q.Lamp)) n++;
            var lamps = new ChunkStreet.LampHead[n];
            n = 0;
            foreach (var q in ss.Props)
            {
                if (q.Kind == ChunkStreet.Kind.Lamp) lamps[n++] = StreetKit.ColumnHead(q.X, q.Y, q.Z, q.Ax, q.Az);
                else if (q.Kind == ChunkStreet.Kind.Pole && q.Lamp) lamps[n++] = StreetKit.PoleHead(q.X, q.Y, q.Z, q.Ax, q.Az, q.Side);
                else continue;
                lamps[n - 1].Seed = (long)(q.X * 16) * 73856093L ^ (long)(q.Z * 16) * 19349663L;
            }
            st.LampHeads = lamps;
        }

        /// <summary>Everything on the street a vehicle stops against, as boxes (E-04). Runs after the items are placed.</summary>
        static void CollectSolids(ChunkStreet st, StreetScratch ss)
        {
            var s = ss.Solids;
            foreach (var (x, z) in ss.PiersPlaced) s.Add(new ChunkStreet.Solid { X = x, Z = z, Ux = 1, Along = PierRadius, Half = PierRadius, Top = SolidHeight });
            foreach (var (x, z, r) in ss.FlyPlaced) s.Add(new ChunkStreet.Solid { X = x, Z = z, Ux = 1, Along = r, Half = r, Top = SolidHeight });
            foreach (var o in ss.Islands) s.Add(new ChunkStreet.Solid { X = o.x, Z = o.z, Ux = o.ux, Uz = o.uz, Along = o.a, Half = o.half, Top = MedianTop });
            foreach (var q in ss.Props)
            {
                double r = q.Kind == ChunkStreet.Kind.Pole ? PoleSolid : q.Kind == ChunkStreet.Kind.Lamp ? LampSolid : q.Kind == ChunkStreet.Kind.Tree ? TrunkSolid : 0;
                if (r > 0) s.Add(new ChunkStreet.Solid { X = q.X, Z = q.Z, Ux = 1, Along = r, Half = r, Y = q.Y, Top = q.Y + SolidHeight });
            }
            st.Solids = s.ToArray();
        }

        List<Pad> pads;

        static double Low(Span<double> pts, int n)
        {
            double low = double.MaxValue;
            for (int i = 0; i < n; i++) low = Math.Min(low, pts[3 * i + 1]);
            return low;
        }

        // ---- Where things may stand ----

        /// <summary>Is (x, z) within margin of a carriageway or junction pad at level y?</summary>
        bool OnCarriageway(double x, double z, double y, double margin)
        {
            var mine = Mine();
            foreach (int q in SegmentsNear(x - margin, x + margin, z - margin, z + margin))
            {
                var s = mine.SegItems[q];
                if (!SameLevel(s.Road.Y, y)) continue;
                int start = s.ExactCount >= 3 ? s.Exact : s.Start, n = s.ExactCount >= 3 ? s.ExactCount : s.Count;
                if (InRing(mine.SegRings, start, n, x, z, margin)) return true;
            }
            if (pads != null)
                foreach (var pad in pads)
                    if (SameLevel(pad.Y, y) && InPad(pad.Ring, x, z, margin)) return true;
            return false;
        }

        /// <summary>Is (x, z) inside, or within margin of, the convex ring of n points from start (counter-clockwise)?</summary>
        static bool InRing(List<double> rings, int start, int n, double x, double z, double margin)
        {
            for (int k = 0; k < n; k++)
            {
                int a = start + 2 * k, b = start + 2 * ((k + 1) % n);
                double ex = rings[b] - rings[a], ez = rings[b + 1] - rings[a + 1], len = Math.Sqrt(ex * ex + ez * ez);
                if (len < 1e-9) continue;
                if ((ex * (z - rings[a + 1]) - ez * (x - rings[a])) / len < -margin) return false;
            }
            return true;
        }

        static bool InPad(List<(double x, double z)> ring, double x, double z, double margin)
        {
            int n = ring.Count;
            if (n < 3) return false;
            double area = 0;
            for (int i = 0; i < n; i++) area += ring[i].x * ring[(i + 1) % n].z - ring[(i + 1) % n].x * ring[i].z;
            double sign = area > 0 ? 1 : -1;
            for (int i = 0; i < n; i++)
            {
                var a = ring[i];
                var b = ring[(i + 1) % n];
                double ex = b.x - a.x, ez = b.z - a.z, len = Math.Sqrt(ex * ex + ez * ez);
                if (len < 1e-9) continue;
                if (sign * (ex * (z - a.z) - ez * (x - a.x)) / len < -margin) return false;
            }
            return true;
        }

        /// <summary>The top of a sidewalk band at (x, z) on level y, or -1 where there is none.</summary>
        double BandTop(double x, double z, double y)
        {
            var mine = Mine();
            var list = mine.WalkCellLists[Cell(x) + Cell(z) * WallCells];
            if (list == null) return -1;
            foreach (int k in list)
            {
                var q = mine.WalkQuads[k];
                if (!SameLevel(q.Y, y) || x < q.MinX || x > q.MaxX || z < q.MinZ || z > q.MaxZ) continue;
                if (Left(q.X0, q.Z0, q.X1, q.Z1, x, z) && Left(q.X1, q.Z1, q.X2, q.Z2, x, z) && Left(q.X2, q.Z2, q.X3, q.Z3, x, z) && Left(q.X3, q.Z3, q.X0, q.Z0, x, z))
                    return q.Y + WalkHeight(x, z, q.Y);
            }
            return -1;
        }

        static bool Left(double ax, double az, double bx, double bz, double x, double z) => (bx - ax) * (z - az) - (bz - az) * (x - ax) >= -1e-9;

        static bool InChunk(double x, double z) => x >= 0 && x < Size && z >= 0 && z < Size;

        /// <summary>The point at arc s along a drawn road, its unit direction and half width there.</summary>
        static void AlongRoad(Drawn r, double s, out double x, out double z, out double dx, out double dz, out double half)
        {
            int n = r.N, i = 0;
            s = Math.Clamp(s, 0, r.Arc[n - 1]);
            while (i + 2 < n && r.Arc[i + 1] < s) i++;
            double len = r.Arc[i + 1] - r.Arc[i], t = len > 1e-9 ? (s - r.Arc[i]) / len : 0;
            double ax = r.Pts[2 * i], az = r.Pts[2 * i + 1], bx = r.Pts[2 * i + 2], bz = r.Pts[2 * i + 3];
            x = ax + (bx - ax) * t;
            z = az + (bz - az) * t;
            dx = len > 1e-9 ? (bx - ax) / len : 1;
            dz = len > 1e-9 ? (bz - az) / len : 0;
            half = r.Halves[i] + (r.Halves[i + 1] - r.Halves[i]) * t;
        }

        // ---- Utility poles and cables ----

        void PlacePoles(List<Drawn> drawn, ChunkStreet st, StreetScratch ss, bool keep)
        {
            var placed = ss.Placed;
            foreach (var r in drawn)
            {
                if (!r.Grade || r.Bridge || r.N < 2) continue;
                var attr = r.Attrs[0];
                if (!PoleClasses.Contains(attr.Cls)) continue;
                double total = r.Arc[r.N - 1];
                if (total < 1) continue;
                int side = Hash(attr.Way, 900) < 0.5 ? 1 : -1;
                // Counted from an end on the chunk border, so the neighbour's poles keep step.
                bool fromEnd = !r.StartOn && r.EndOn;
                placed.Clear();
                for (int k = 0; ; k++)
                {
                    double s0 = PoleSpacing / 2 + k * PoleSpacing;
                    if (s0 > total) break;
                    double s = fromEnd ? total - s0 : s0;
                    bool done = false;
                    for (int t = 0; t <= 2 * (int)PoleSlide && !done; t++)
                    {
                        // 0, +1, -1, +2, -2 ... metres along.
                        double sl = s + (t % 2 == 1 ? 1 : -1) * ((t + 1) / 2);
                        if (sl < 0 || sl > total) continue;
                        if (!PoleSpot(r, sl, side, out double x, out double z, out double ax, out double az)) continue;
                        done = true;
                        if (Crowded(ss.Props, x, z))
                        {
                            st.PolesCrowded++;
                            if (keep) st.Items.Add(new ChunkStreet.Item { Kind = ChunkStreet.Kind.Pole, X = x, Z = z, Way = attr.Way, Skipped = true, Why = "crowded" });
                            break;
                        }
                        bool lamp = Hash(attr.Way, 910 + k) < 0.5, transformer = Hash(attr.Way, 950 + k) < 0.1;
                        placed.Add((sl, ss.Props.Count));
                        ss.Props.Add(new Prop { Kind = ChunkStreet.Kind.Pole, X = x, Z = z, Y = 0, Ax = ax, Az = az, Side = -side, Lamp = lamp, Transformer = transformer, Seed = attr.Way * 31 + k });
                        st.Poles++;
                        if (lamp) st.PoleLamps++;
                        if (transformer) st.Transformers++;
                        if (keep) st.Items.Add(new ChunkStreet.Item { Kind = ChunkStreet.Kind.Pole, X = x, Z = z, Y = 0, Top = StreetKit.PoleHeight, Way = attr.Way });
                    }
                    if (!done)
                    {
                        st.PolesSkipped++;
                        if (keep)
                        {
                            AlongRoad(r, s, out double x, out double z, out _, out _, out _);
                            st.Items.Add(new ChunkStreet.Item { Kind = ChunkStreet.Kind.Pole, X = x, Z = z, Way = attr.Way, Skipped = true, Why = "no room" });
                        }
                    }
                }
                if (fromEnd) placed.Reverse();
                // Spans between neighbours, and halves to the border at either end.
                for (int i = 0; i + 1 < placed.Count; i++)
                {
                    var a = ss.Props[placed[i].pole];
                    var b = ss.Props[placed[i + 1].pole];
                    if (placed[i + 1].s - placed[i].s > 1.6 * PoleSpacing) continue;
                    AddSpan(st, ss, a.X, a.Z, a.Ax, a.Az, b.X, b.Z, b.Ax, b.Az, false);
                }
                if (placed.Count > 0)
                {
                    if (r.StartOn) BorderSpan(r, 0, side, ss.Props[placed[0].pole], st, ss);
                    if (r.EndOn) BorderSpan(r, total, side, ss.Props[placed[placed.Count - 1].pole], st, ss);
                }
            }
        }

        /// <summary>The spot for a pole at arc s on a side of a road, and the road's direction there; false where it may not stand.</summary>
        bool PoleSpot(Drawn r, double s, int side, out double x, out double z, out double ax, out double az)
        {
            AlongRoad(r, s, out double cx, out double cz, out ax, out az, out double half);
            int seg = 0;
            while (seg + 2 < r.N && r.Arc[seg + 1] < s) seg++;
            var attr = r.Attrs[seg];
            bool walk = side > 0 ? attr.WalkLeft : attr.WalkRight;
            double off = half + (walk ? PoleBehindKerb : DrainClass(attr.Cls) ? DrainWidth + 0.35 : PoleOffset);
            x = cx - az * side * off;
            z = cz + ax * side * off;
            return InChunk(x, z) && !OnCarriageway(x, z, r.Y, PoleMargin) && !InBuilding(x, z)
                && !InBuilding(x + PoleMargin, z) && !InBuilding(x - PoleMargin, z) && !InBuilding(x, z + PoleMargin) && !InBuilding(x, z - PoleMargin);
        }

        static bool Crowded(List<Prop> props, double x, double z)
        {
            foreach (var q in props)
                if (q.Kind == ChunkStreet.Kind.Pole && (q.X - x) * (q.X - x) + (q.Z - z) * (q.Z - z) < PoleGap * PoleGap) return true;
            return false;
        }

        void AddSpan(ChunkStreet st, StreetScratch ss, double ax, double az, double aax, double aaz, double bx, double bz, double bax, double baz, bool half)
        {
            // Turn the far pole's axes to agree with the near one's, so cable k meets cable k.
            if (aax * bax + aaz * baz < 0) { bax = -bax; baz = -baz; }
            double ex = half ? ax + (bx - ax) / 2 : bx, ez = half ? az + (bz - az) / 2 : bz;
            for (int k = 1; k < 8; k++)
            {
                double t = k / 8.0, x = ax + (ex - ax) * t, z = az + (ez - az) * t;
                if (InChunk(x, z) && InBuilding(x, z))
                {
                    st.SpansBlocked++;
                    return;
                }
            }
            ss.CableSpans.Add(new CableSpan { Ax = ax, Az = az, Aax = aax, Aaz = aaz, Bx = bx, Bz = bz, Bax = bax, Baz = baz, Y = 0, Half = half });
            if (half) st.HalfSpans++;
            else st.Spans++;
        }

        /// <summary>Half a span from the end pole to the chunk border, as if the next pole stood as far beyond it.</summary>
        void BorderSpan(Drawn r, double s, int side, Prop pole, ChunkStreet st, StreetScratch ss)
        {
            AlongRoad(r, s, out double cx, out double cz, out double ax, out double az, out double half);
            int seg = s <= 0 ? 0 : r.N - 2;
            var attr = r.Attrs[seg];
            bool walk = side > 0 ? attr.WalkLeft : attr.WalkRight;
            double off = half + (walk ? PoleBehindKerb : DrainClass(attr.Cls) ? DrainWidth + 0.35 : PoleOffset);
            double ex = cx - az * side * off, ez = cz + ax * side * off;
            double d2 = (ex - pole.X) * (ex - pole.X) + (ez - pole.Z) * (ez - pole.Z);
            if (d2 > PoleSpacing * PoleSpacing) return;
            AddSpan(st, ss, pole.X, pole.Z, pole.Ax, pole.Az, 2 * ex - pole.X, 2 * ez - pole.Z, pole.Ax, pole.Az, true);
        }

        // ---- Street lights and trees ----

        void PlaceLamps(DadcChunk c, List<Drawn> drawn, ChunkStreet st, StreetScratch ss, bool keep)
        {
            if (c.Points == null) return;
            foreach (var pt in c.Points)
            {
                if (pt.Kind != "lamp") continue;
                double x = pt.East / 100.0, z = pt.North / 100.0;
                // Facing the nearest at-grade road's centre line, within 25 m; else east.
                double fx = 1, fz = 0, best = 25, half = 0;
                foreach (var r in drawn)
                {
                    if (!r.Grade || r.Bridge || x < r.MinX - 25 || x > r.MaxX + 25 || z < r.MinZ - 25 || z > r.MaxZ + 25) continue;
                    for (int i = 0; i + 1 < r.N; i++)
                    {
                        double axp = r.Pts[2 * i], azp = r.Pts[2 * i + 1], ux = r.Pts[2 * i + 2] - axp, uz = r.Pts[2 * i + 3] - azp;
                        double ll = ux * ux + uz * uz;
                        double t = ll > 0 ? Math.Clamp(((x - axp) * ux + (z - azp) * uz) / ll, 0, 1) : 0;
                        double qx = axp + ux * t - x, qz = azp + uz * t - z, d = Math.Sqrt(qx * qx + qz * qz);
                        if (d < best && d > 0.1) { best = d; fx = qx / d; fz = qz / d; half = r.Halves[i] + (r.Halves[i + 1] - r.Halves[i]) * t; }
                    }
                }
                // A lamp mapped inside a carriageway's estimated width (K-31) goes out to just behind its kerb, and is counted.
                string why = null, note = null;
                if (OnCarriageway(x, z, 0.28, 0.1))
                {
                    double mx = x - fx * (half + PoleBehindKerb - best), mz = z - fz * (half + PoleBehindKerb - best);
                    if (best < 25 && half + PoleBehindKerb - best <= LampMove && InChunk(mx, mz) && !OnCarriageway(mx, mz, 0.28, 0.1) && !InBuilding(mx, mz))
                    {
                        note = $"moved {half + PoleBehindKerb - best:F1} m to the kerb";
                        x = mx;
                        z = mz;
                        st.LampsMoved++;
                    }
                    else why = "on a carriageway";
                }
                else if (InBuilding(x, z)) why = "in a building";
                if (why != null)
                {
                    if (why == "in a building") st.LampsInBuilding++;
                    else st.LampsOnRoad++;
                    if (keep) st.Items.Add(new ChunkStreet.Item { Kind = ChunkStreet.Kind.Lamp, X = x, Z = z, Way = pt.Id, Skipped = true, Why = why });
                    continue;
                }
                double y = BandTop(x, z, 0.28);
                y = y < 0 ? 0 : y;
                ss.Props.Add(new Prop { Kind = ChunkStreet.Kind.Lamp, X = x, Z = z, Y = y, Ax = fx, Az = fz });
                st.Lamps++;
                if (keep) st.Items.Add(new ChunkStreet.Item { Kind = ChunkStreet.Kind.Lamp, X = x, Z = z, Y = y, Top = y + StreetKit.LampColumnHeight, Way = pt.Id, Why = note });
            }
        }

        /// <summary>The big roads carry no utility poles, so at night nothing would light them. A labelled rule (D-13; none of these is mapped): lamp columns behind the kerb on
        /// both sides, RoadLampSpacing apart and staggered, wherever PoleSpot finds room, and clear of any lamp or pole already there.</summary>
        void PlaceRoadLamps(List<Drawn> drawn, ChunkStreet st, StreetScratch ss, bool keep)
        {
            foreach (var r in drawn)
            {
                if (!r.Grade || r.Bridge || r.N < 2 || !RoadLampClasses.Contains(r.Attrs[0].Cls)) continue;
                double total = r.Arc[r.N - 1];
                if (total < 1) continue;
                bool fromEnd = !r.StartOn && r.EndOn;
                for (int side = -1; side <= 1; side += 2)
                    for (double s0 = RoadLampSpacing * (side > 0 ? 0.25 : 0.75); s0 < total; s0 += RoadLampSpacing)
                    {
                        double s = fromEnd ? total - s0 : s0;
                        for (int t = 0; t <= 2 * (int)PoleSlide; t++)
                        {
                            double sl = s + (t % 2 == 1 ? 1 : -1) * ((t + 1) / 2);
                            if (sl < 0 || sl > total || !PoleSpot(r, sl, side, out double x, out double z, out double ax, out double az)) continue;
                            // The roads of the chunk next door are not in this one's list.
                            if (!InChunk(x - RoadLampBorder, z - RoadLampBorder) || !InChunk(x + RoadLampBorder, z + RoadLampBorder) || !ClearOfRoads(drawn, x, z)) continue;
                            if (LampCrowded(ss.Props, x, z)) break;
                            double y = BandTop(x, z, 0.28);
                            y = y < 0 ? 0 : y;
                            ss.Props.Add(new Prop { Kind = ChunkStreet.Kind.Lamp, X = x, Z = z, Y = y, Ax = az * side, Az = -ax * side });
                            st.RoadLamps++;
                            if (keep) st.Items.Add(new ChunkStreet.Item { Kind = ChunkStreet.Kind.Lamp, X = x, Z = z, Y = y, Top = y + StreetKit.LampColumnHeight, Way = r.Attrs[0].Way, Why = RuleLamp });
                            break;
                        }
                    }
            }
        }

        /// <summary>No road drawn at grade reaches the spot: a lamp keeps off every road, not only the one it lights (a dual carriageway's two halves stand close).</summary>
        static bool ClearOfRoads(List<Drawn> drawn, double x, double z)
        {
            foreach (var r in drawn)
            {
                if (!r.Grade || x < r.MinX - 1 || x > r.MaxX + 1 || z < r.MinZ - 1 || z > r.MaxZ + 1) continue;
                for (int i = 0; i + 1 < r.N; i++)
                {
                    double ax = r.Pts[2 * i], az = r.Pts[2 * i + 1], ux = r.Pts[2 * i + 2] - ax, uz = r.Pts[2 * i + 3] - az, ll = ux * ux + uz * uz;
                    double t = ll > 1e-9 ? Math.Clamp(((x - ax) * ux + (z - az) * uz) / ll, 0, 1) : 0;
                    double dx = ax + ux * t - x, dz = az + uz * t - z, reach = r.Halves[i] + (r.Halves[i + 1] - r.Halves[i]) * t + PoleMargin;
                    if (dx * dx + dz * dz < reach * reach) return false;
                }
            }
            return true;
        }

        static bool LampCrowded(List<Prop> props, double x, double z)
        {
            foreach (var q in props)
                if ((q.Kind == ChunkStreet.Kind.Pole || q.Kind == ChunkStreet.Kind.Lamp) && (q.X - x) * (q.X - x) + (q.Z - z) * (q.Z - z) < RoadLampGap * RoadLampGap) return true;
            return false;
        }

        void PlaceTrees(DadcChunk c, ChunkStreet st, StreetScratch ss, bool keep)
        {
            if (c.Trees == null) return;
            foreach (var t in c.Trees)
            {
                double x = t.East / 100.0, z = t.North / 100.0;
                long seed = (c.CornerEastCm + t.East) * 73856093L ^ (c.CornerNorthCm + t.North) * 19349663L;
                string why = OnCarriageway(x, z, 0.28, TreeMargin) ? "on a carriageway" : InBuilding(x, z) ? "in a building" : null;
                if (why != null)
                {
                    if (why == "in a building") st.TreesInBuilding++;
                    else st.TreesOnRoad++;
                    if (keep) st.Items.Add(new ChunkStreet.Item { Kind = ChunkStreet.Kind.Tree, X = x, Z = z, Skipped = true, Why = why });
                    continue;
                }
                double h = t.HeightCm >= 200 ? t.HeightCm / 100.0 : 7 + 6 * Hash(seed, 3);
                h = Math.Clamp(h, MinTree, MaxTree);
                int form = (int)(Hash(seed, 5) * StreetKit.TreeForms);
                ss.Props.Add(new Prop { Kind = ChunkStreet.Kind.Tree, X = x, Z = z, Y = 0, H = h, Form = form, Seed = seed });
                st.Trees++;
                if (keep) st.Items.Add(new ChunkStreet.Item { Kind = ChunkStreet.Kind.Tree, X = x, Z = z, Y = 0, Top = h });
            }
        }
    }
}
