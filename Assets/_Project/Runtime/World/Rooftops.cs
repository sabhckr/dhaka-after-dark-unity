// The rooftop kit (D-06b, ENVIRONMENT_ROADMAP): stair rooms and lift rooms, water tanks, satellite
// dishes and air-conditioner condensers on the slab roofs BuildingSkin marks (Skin.Kit: the roofs of
// buildings people live and work in, on the ground, 6 m or more). The kit is placed here, with the
// street furniture, so it is drawn by distance in the street cells (StreetLod). Far, it is in the far
// cell meshes. Near, it is kept as items by cell (ChunkStreet.RoofKit) and a cell's near kit is built
// on a worker only when the cell comes near (BuildNearKit; the streamer uploads it as one mesh more
// per cell and drops it again further off): built with the chunk, it held 744,660 of the slice's
// 927,756 kit vertices in every resident cell, most of them never drawn (K-45, approach 4, adopted
// 2026-11-03). The roadmap row says "instanced"; it is merged geometry instead, like every other kit in
// this port, because the streamer's cells already give it distance levels and a batch per cell, and
// GPU instancing would add a draw per item type per chunk.
//
// Rules (labelled, by building; no rooftop is mapped in the data):
// - A stair room (the stair's head house with its door) on walk-ups, shop-houses and institutions of
//   3 floors or more and 60 m² or more, nine in ten by hash; a lift room instead on commercial
//   buildings of 6 floors or more and 150 m² or more. Walls in the building's own wall material.
// - Water tanks: 1 + one per 150 m², at most 4; black, blue or white plastic or steel, no maker's
//   marks. Where there is a stair room, the first tank stands on its roof six times in ten.
// - Satellite dishes: on 35 % of walk-ups and shop-houses, facing south-south-east up 55 degrees,
//   the arc the region's satellites are on; drawn near only.
// - Condensers: commercial one per 80 m² (at most 12), institutions one per 120 m² (at most 6),
//   shop-houses 1 to 4, homes none to 2.
// Each item is placed at hashed points in the roof's frame (its longest outer edge), wholly inside
// the roof, at least RoofClearance from every ring edge (so clear of the parapet) and RoofGap from the
// items already placed; one that finds no room in RoofTries is left out and counted. Each piece of a
// building cut by the chunk grid (K-16) is a roof of its own.
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Threading;

namespace Dad.World
{
    public sealed partial class ChunkRoads
    {
        public const double RoofClearance = 0.5, RoofGap = 0.3;
        public const int RoofTries = 24;
        /// <summary>The dishes' azimuth from north (clockwise) and elevation, in degrees.</summary>
        public const double DishAzimuth = 160, DishElevation = 55;

        sealed class RoofScratch
        {
            public readonly List<double> Flat = new List<double>(64);
            public readonly List<int> Holes = new List<int>(4);
            public readonly List<(double u, double v, double hu, double hv)> Boxes = new List<(double, double, double, double)>(16);
            public readonly BuildingClass.Near Near = new BuildingClass.Near();
            public BuildingCategory[] Categories = new BuildingCategory[256];
            public ClassSource[] Sources = new ClassSource[256];
        }

        [ThreadStatic] static RoofScratch roofScratch;

        // The roof being furnished: its ring, frame and the boxes placed on it.
        struct RoofFrame
        {
            public double Ox, Oz, Ax, Az, U0, U1, V0, V1, Y;
        }

        void PlaceRoofs(DadcChunk c, ChunkStreet st, List<Prop> props, bool keep)
        {
            int n = c.Buildings?.Length ?? 0;
            if (n == 0) return;
            var rs = roofScratch ??= new RoofScratch();
            if (rs.Categories.Length < n) { rs.Categories = new BuildingCategory[n * 2]; rs.Sources = new ClassSource[n * 2]; }
            BuildingClass.ClassifyChunk(c, rs.Near, rs.Categories, rs.Sources);
            for (int i = 0; i < n; i++)
            {
                var b = c.Buildings[i];
                if (b.Outer == null || b.Outer.Length < 6) continue;
                var pl = BuildingRules.Place(b);
                var cat = rs.Categories[i];
                var skin = BuildingSkin.Of(b, cat, pl, ChunkBuildings.Facaded(c, b, pl));
                if (!skin.Kit) continue;
                ChunkSurfaces.Flatten(b.Outer, b.Holes, rs.Flat, rs.Holes);
                int outerEnd = rs.Holes.Count > 0 ? rs.Holes[0] : rs.Flat.Count / 2;
                if (outerEnd < 3) continue;
                double area = Math.Abs(RingArea(rs.Flat, 0, outerEnd));
                for (int h = 0; h < rs.Holes.Count; h++)
                    area -= Math.Abs(RingArea(rs.Flat, rs.Holes[h], h + 1 < rs.Holes.Count ? rs.Holes[h + 1] : rs.Flat.Count / 2));
                if (area < 20) continue;
                st.Roofs++;
                Furnish(b, cat, pl, skin, area, rs, Frame(rs.Flat, outerEnd, skin.RoofY), st, props, keep);
            }
        }

        void Furnish(Building b, BuildingCategory cat, BuildingRules.Placement pl, Skin skin, double area, RoofScratch rs, RoofFrame f,
            ChunkStreet st, List<Prop> props, bool keep)
        {
            rs.Boxes.Clear();
            var rng = new KitRandom(b.Id ^ 0x5eed_0f_d06bL);
            var family = BuildingClass.FamilyOf(cat);
            int floors = (int)Math.Round((skin.RoofY - pl.Base) / BuildingRules.MetresPerLevel);
            WorldMaterial wallM = skin.Wall == WorldMaterial.Glass ? WorldMaterial.Concrete : skin.Wall;
            uint wall = BuildingPalette.Shade(skin.Wall == WorldMaterial.Glass ? BuildingSkin.Coping : skin.WallTone, 1, wallM);

            // The stair room or lift room.
            bool stair = (family == FacadeFamily.WalkUp || family == FacadeFamily.ShopHouse || family == FacadeFamily.Institutional)
                         && floors >= 3 && area >= 60 && BuildingSkin.Hash(b.Id, 10) < 0.9;
            bool lift = family == FacadeFamily.Commercial && floors >= 6 && area >= 150;
            Prop room = default;
            bool hasRoom = false;
            if (stair || lift)
            {
                double hu = lift ? 2.1 : 1.7, hv = lift ? 1.6 : 1.4, h = lift ? 3.2 : 2.7;
                if (Place(rs, f, hu, hv, ref rng, out double x, out double z))
                {
                    room = new Prop { Kind = ChunkStreet.Kind.StairRoom, X = x, Z = z, Y = f.Y, Ax = f.Ax, Az = f.Az, L = hu, W = hv, H = h, Colour = wall, Seed = b.Id };
                    props.Add(room);
                    hasRoom = true;
                    if (lift) st.LiftRooms++; else st.StairRooms++;
                    Keep(st, keep, room, b.Id, null);
                }
                else Refuse(st, keep, ChunkStreet.Kind.StairRoom, f, b.Id);
            }

            // Water tanks.
            int tanks = Math.Min(4, 1 + (int)(area / 150));
            for (int t = 0; t < tanks; t++)
            {
                double r = rng.Next() < 0.6 ? 0.55 : 0.68, h = r < 0.6 ? 1.25 : 1.5;
                double pick = rng.Next();
                uint colour = pick < 0.45 ? StreetKit.M(StreetKit.TankBlack, WorldMaterial.Paint)
                    : pick < 0.7 ? StreetKit.M(StreetKit.TankBlue, WorldMaterial.Paint)
                    : pick < 0.9 ? StreetKit.M(StreetKit.TankWhite, WorldMaterial.Paint)
                    : StreetKit.M(StreetKit.TankSteel, WorldMaterial.Metal);
                Prop tank;
                if (t == 0 && hasRoom && rng.Next() < 0.6)
                    tank = new Prop { Kind = ChunkStreet.Kind.Tank, X = room.X, Z = room.Z, Y = room.Y + room.H, H = h, L = r, Ax = f.Ax, Az = f.Az, Colour = colour, Seed = b.Id + t };
                else if (Place(rs, f, r, r, ref rng, out double x, out double z))
                    tank = new Prop { Kind = ChunkStreet.Kind.Tank, X = x, Z = z, Y = f.Y, H = h, L = r, Ax = f.Ax, Az = f.Az, Colour = colour, Seed = b.Id + t };
                else { Refuse(st, keep, ChunkStreet.Kind.Tank, f, b.Id); continue; }
                props.Add(tank);
                st.Tanks++;
                Keep(st, keep, tank, b.Id, null);
            }

            // Condensers.
            double ra = BuildingSkin.Hash(b.Id, 11);
            int acs = family switch
            {
                FacadeFamily.Commercial => Math.Min(12, (int)(area / 80)),
                FacadeFamily.Institutional => Math.Min(6, (int)(area / 120)),
                FacadeFamily.ShopHouse => 1 + (int)(ra * 4),
                FacadeFamily.WalkUp => ra < 0.5 ? 0 : ra < 0.85 ? 1 : 2,
                _ => 0,
            };
            for (int k = 0; k < acs; k++)
            {
                // Condensers stand across the frame or along it, the fan facing out of the long side.
                bool across = rng.Next() < 0.5;
                double hu = across ? StreetKit.AcDepth : StreetKit.AcLength, hv = across ? StreetKit.AcLength : StreetKit.AcDepth;
                if (!Place(rs, f, hu, hv, ref rng, out double x, out double z)) { Refuse(st, keep, ChunkStreet.Kind.Ac, f, b.Id); continue; }
                double ax = across ? -f.Az : f.Ax, az = across ? f.Ax : f.Az;
                var ac = new Prop { Kind = ChunkStreet.Kind.Ac, X = x, Z = z, Y = f.Y, Ax = ax, Az = az, H = StreetKit.AcHeight, Seed = b.Id + 100 + k };
                props.Add(ac);
                st.AcUnits++;
                Keep(st, keep, ac, b.Id, null);
            }

            // A dish.
            if ((family == FacadeFamily.WalkUp || family == FacadeFamily.ShopHouse) && BuildingSkin.Hash(b.Id, 12) < 0.35)
            {
                if (Place(rs, f, 0.45, 0.45, ref rng, out double x, out double z))
                {
                    var dish = new Prop { Kind = ChunkStreet.Kind.Dish, X = x, Z = z, Y = f.Y, H = StreetKit.DishMount, Seed = b.Id };
                    props.Add(dish);
                    st.Dishes++;
                    Keep(st, keep, dish, b.Id, null);
                }
                else Refuse(st, keep, ChunkStreet.Kind.Dish, f, b.Id);
            }
        }

        /// <summary>Draws one item of the kit at a level of detail; a dish only near.</summary>
        internal static void DrawRoofItem(KitWriter w, in ChunkStreet.RoofItem q, int lod)
        {
            switch (q.Kind)
            {
                case ChunkStreet.Kind.StairRoom: StreetKit.RoofRoom(w, q.X, q.Y, q.Z, q.Ax, q.Az, q.L, q.W, q.H, q.Colour, lod); break;
                case ChunkStreet.Kind.Tank: StreetKit.Tank(w, q.X, q.Y, q.Z, q.L, q.H, q.Colour, lod); break;
                case ChunkStreet.Kind.Ac: StreetKit.Ac(w, q.X, q.Y, q.Z, q.Ax, q.Az, lod); break;
                case ChunkStreet.Kind.Dish: if (lod == 0) StreetKit.Dish(w, q.X, q.Y, q.Z, DishAzimuth, DishElevation); break;
            }
        }

        static void Keep(ChunkStreet st, bool keep, Prop q, long id, string why)
        {
            if (keep) st.Items.Add(new ChunkStreet.Item { Kind = q.Kind, X = q.X, Z = q.Z, Y = q.Y, Top = q.Y + q.H, Way = id, Skipped = why != null, Why = why });
        }

        static void Refuse(ChunkStreet st, bool keep, ChunkStreet.Kind kind, RoofFrame f, long id)
        {
            st.RoofNoRoom++;
            if (keep) st.Items.Add(new ChunkStreet.Item { Kind = kind, X = f.Ox, Z = f.Oz, Y = f.Y, Top = f.Y, Way = id, Skipped = true, Why = "no room on the roof" });
        }

        static double RingArea(List<double> flat, int start, int end)
        {
            double a = 0;
            for (int k = start; k < end; k++)
            {
                int j = k + 1 < end ? k + 1 : start;
                a += flat[2 * k] * flat[2 * j + 1] - flat[2 * j] * flat[2 * k + 1];
            }
            return a / 2;
        }

        /// <summary>The roof's frame: along its longest outer edge, with the ring's extent in it.</summary>
        static RoofFrame Frame(List<double> flat, int outerEnd, double y)
        {
            var f = new RoofFrame { Ox = flat[0], Oz = flat[1], Ax = 1, Az = 0, Y = y };
            double best = -1;
            for (int k = 0; k < outerEnd; k++)
            {
                int j = (k + 1) % outerEnd;
                double dx = flat[2 * j] - flat[2 * k], dz = flat[2 * j + 1] - flat[2 * k + 1], l = dx * dx + dz * dz;
                if (l > best) { best = l; l = Math.Sqrt(l); f.Ax = dx / l; f.Az = dz / l; }
            }
            f.U0 = f.V0 = double.MaxValue;
            f.U1 = f.V1 = double.MinValue;
            for (int k = 0; k < outerEnd; k++)
            {
                ToFrame(f, flat[2 * k], flat[2 * k + 1], out double u, out double v);
                f.U0 = Math.Min(f.U0, u); f.U1 = Math.Max(f.U1, u);
                f.V0 = Math.Min(f.V0, v); f.V1 = Math.Max(f.V1, v);
            }
            return f;
        }

        // u along (Ax, Az), v to its left (-Az, Ax).
        static void ToFrame(in RoofFrame f, double x, double z, out double u, out double v)
        {
            double dx = x - f.Ox, dz = z - f.Oz;
            u = dx * f.Ax + dz * f.Az;
            v = -dx * f.Az + dz * f.Ax;
        }

        /// <summary>Finds room for a box of half sizes (hu, hv) in the roof's frame (see the file header).</summary>
        static bool Place(RoofScratch rs, in RoofFrame f, double hu, double hv, ref KitRandom rng, out double x, out double z)
        {
            x = z = 0;
            double c = RoofClearance;
            if (f.U1 - f.U0 < 2 * (hu + c) || f.V1 - f.V0 < 2 * (hv + c)) return false;
            for (int t = 0; t < RoofTries; t++)
            {
                double u = rng.Range(f.U0 + hu + c, f.U1 - hu - c), v = rng.Range(f.V0 + hv + c, f.V1 - hv - c);
                if (!Clear(rs, u, v, hu, hv)) continue;
                if (!Inside(rs.Flat, rs.Holes, f, u, v, hu + c, hv + c)) continue;
                rs.Boxes.Add((u, v, hu, hv));
                x = f.Ox + u * f.Ax - v * f.Az;
                z = f.Oz + u * f.Az + v * f.Ax;
                return true;
            }
            return false;
        }

        static bool Clear(RoofScratch rs, double u, double v, double hu, double hv)
        {
            foreach (var o in rs.Boxes)
                if (Math.Abs(u - o.u) < hu + o.hu + RoofGap && Math.Abs(v - o.v) < hv + o.hv + RoofGap) return false;
            return true;
        }

        /// <summary>Is the box (u ± hu, v ± hv) wholly inside the roof: its centre inside, no ring edge crossing it?</summary>
        static bool Inside(List<double> flat, List<int> holes, in RoofFrame f, double u, double v, double hu, double hv)
        {
            int count = flat.Count / 2;
            bool inside = false;
            for (int r = 0; r <= holes.Count; r++)
            {
                int start = r == 0 ? 0 : holes[r - 1], end = r < holes.Count ? holes[r] : count;
                for (int k = start; k < end; k++)
                {
                    int j = k + 1 < end ? k + 1 : start;
                    ToFrame(f, flat[2 * k], flat[2 * k + 1], out double pu, out double pv);
                    ToFrame(f, flat[2 * j], flat[2 * j + 1], out double qu, out double qv);
                    if (SegmentHitsBox(pu - u, pv - v, qu - u, qv - v, hu, hv)) return false;
                    if ((pv > v) != (qv > v) && u < pu + (v - pv) * (qu - pu) / (qv - pv)) inside = !inside;
                }
            }
            return inside;
        }

        /// <summary>Does the segment p-q meet the box |u| ≤ hu, |v| ≤ hv (Liang-Barsky)?</summary>
        static bool SegmentHitsBox(double pu, double pv, double qu, double qv, double hu, double hv)
        {
            double t0 = 0, t1 = 1, du = qu - pu, dv = qv - pv;
            return Clip(-du, pu + hu, ref t0, ref t1) && Clip(du, hu - pu, ref t0, ref t1)
                && Clip(-dv, pv + hv, ref t0, ref t1) && Clip(dv, hv - pv, ref t0, ref t1);
        }

        static bool Clip(double p, double q, ref double t0, ref double t1)
        {
            if (Math.Abs(p) < 1e-12) return q >= 0;
            double t = q / p;
            if (p < 0) { if (t > t1) return false; if (t > t0) t0 = t; }
            else { if (t < t0) return false; if (t < t1) t1 = t; }
            return true;
        }
    }

    public sealed partial class ChunkStreet
    {
        /// <summary>An item of the rooftop kit as placed, kept to build its cell's near kit later.</summary>
        public struct RoofItem
        {
            public Kind Kind;
            public double X, Y, Z, Ax, Az, L, W, H;
            public uint Colour;
        }

        /// <summary>The rooftop kit's items by cell, in placement order: cell c's run from RoofKitStart[c] to RoofKitStart[c + 1].
        /// Built pooled, the array is longer than that and lives until ReleaseRoofKit (the streamer's chunk unloads).</summary>
        public RoofItem[] RoofKit;
        public readonly int[] RoofKitStart = new int[Cells * Cells + 1];
        bool kitPooled;

        // Up to 64K items a chunk (a dense one has a few thousand), 32 of a size: a new array each load was 1.5 MiB of
        // garbage for the 12 slice chunks, measured.
        static readonly ArrayPool<RoofItem> kitPool = ArrayPool<RoofItem>.Create(1 << 16, 32);

        public bool HasNearKit(int cell) => RoofKitStart[cell + 1] > RoofKitStart[cell];

        internal void KeepRoofKit(List<RoofItem> items, bool pooled)
        {
            kitPooled = pooled;
            RoofKit = pooled ? kitPool.Rent(items.Count) : new RoofItem[items.Count];
            items.CopyTo(RoofKit);
        }

        /// <summary>Hands a pooled RoofKit back; no near kit may be built after, and none may be building (one would fail
        /// on the missing items rather than read an array lent out again).</summary>
        public void ReleaseRoofKit()
        {
            if (!kitPooled) return;
            kitPooled = false;
            kitPool.Return(RoofKit);
            RoofKit = null;
        }

        /// <summary>The size a near-kit scratch set is made at: the largest cell's near kit with its near facades (D-06c)
        /// city-wide (198,280 vertices, 573,954 indices, c_10_-22 cell 3 in Old Dhaka, measured 2026-11-03), rounded up, so no
        /// build grows one. The rooftop kit alone was 19,840 and 99,216 at most.</summary>
        public const int KitScratchVertices = 200704, KitScratchIndices = 589824;

        sealed class KitScratch
        {
            public readonly List<float> P = new List<float>(3 * KitScratchVertices);
            public readonly List<uint> C = new List<uint>(KitScratchVertices);
            public readonly List<int> I = new List<int>(KitScratchIndices);
            public readonly KitWriter W;
            public KitScratch() { W = new KitWriter(P, C, I); }
        }

        // Builds borrow a scratch set from this pool rather than keeping one per thread: the thread pool's threads come and
        // go, and each new one grew its own lists again (0.87 MiB a thread over the slice's cells, most of it garbage, measured).
        // The streamer makes its sets ahead (PrepareNearKitScratch); a build that finds none free makes one more.
        static readonly Stack<KitScratch> kitScratch = new Stack<KitScratch>();
        static int kitScratchMade, kitScratchGrown;

        /// <summary>Makes scratch sets until sets of them exist, for that many near-kit builds at once.</summary>
        public static void PrepareNearKitScratch(int sets)
        {
            lock (kitScratch)
                while (kitScratchMade < sets)
                {
                    kitScratch.Push(new KitScratch());
                    kitScratchMade++;
                }
        }

        /// <summary>Scratch sets made so far, and builds that had to grow one. Tests only: ResetNearKitScratch forgets them all.</summary>
        public static int NearKitScratchSets { get { lock (kitScratch) return kitScratchMade; } }
        public static int NearKitScratchGrowths => Volatile.Read(ref kitScratchGrown);

        public static void ResetNearKitScratch()
        {
            lock (kitScratch)
            {
                kitScratch.Clear();
                kitScratchMade = 0;
                kitScratchGrown = 0;
            }
        }

        /// <summary>Cell c's near rooftop kit (lod 0) as mesh arrays, pooled if asked (MeshArrays: hand them back with
        /// Release once uploaded). Reads only RoofKit, so it may run on any thread, for several cells at once.</summary>
        public NearKit BuildNearKit(int cell, bool pooled = false) => BuildNearKit(cell, null, pooled);

        /// <summary>Cell c's near rooftop kit and, after it, the near facades of the chunk's buildings (D-06c, NearFacades.cs;
        /// none when facades is null): the kit's indices first, RoofIndexCount of them. Reads only RoofKit and the
        /// facades' near walls, so it may run on any thread, for several cells at once.</summary>
        public NearKit BuildNearKit(int cell, ChunkBuildings facades, bool pooled = false)
        {
            KitScratch s;
            lock (kitScratch)
            {
                if (kitScratch.Count > 0) s = kitScratch.Pop();
                else
                {
                    s = new KitScratch();
                    kitScratchMade++;
                }
            }
            try { return BuildNearKit(cell, facades, pooled, s); }
            finally { lock (kitScratch) kitScratch.Push(s); }
        }

        NearKit BuildNearKit(int cell, ChunkBuildings facades, bool pooled, KitScratch s)
        {
            s.P.Clear();
            s.C.Clear();
            s.I.Clear();
            s.W.Base = 0;
            int capacity = s.P.Capacity + s.C.Capacity + s.I.Capacity;
            for (int k = RoofKitStart[cell]; k < RoofKitStart[cell + 1]; k++) ChunkRoads.DrawRoofItem(s.W, RoofKit[k], 0);
            int roofVertices = s.C.Count, roofIndices = s.I.Count;
            facades?.DrawNearFacades(cell, s.W);
            if (s.P.Capacity + s.C.Capacity + s.I.Capacity != capacity) Interlocked.Increment(ref kitScratchGrown);
            var kit = new NearKit
            {
                VertexCount = s.C.Count, IndexCount = s.I.Count, RoofVertexCount = roofVertices, RoofIndexCount = roofIndices, Pooled = pooled,
                Positions = MeshArrays.Copy(s.P, pooled), Colors = MeshArrays.Copy(s.C, pooled), Indices = MeshArrays.Copy(s.I, pooled),
            };
            for (int k = 0; k < 3; k++) { kit.Min[k] = float.MaxValue; kit.Max[k] = float.MinValue; }
            for (int v = 0; v < kit.VertexCount; v++)
                for (int k = 0; k < 3; k++)
                {
                    kit.Min[k] = Math.Min(kit.Min[k], kit.Positions[3 * v + k]);
                    kit.Max[k] = Math.Max(kit.Max[k], kit.Positions[3 * v + k]);
                }
            if (kit.VertexCount == 0) for (int k = 0; k < 3; k++) kit.Min[k] = kit.Max[k] = 0;
            return kit;
        }

        /// <summary>Fills RoofVertices[0] and RoofIndices[0] by building every cell's near kit (tests and tools).</summary>
        public void CountNearKit()
        {
            RoofVertices[0] = RoofIndices[0] = 0;
            for (int c = 0; c < Cells * Cells; c++)
            {
                var kit = BuildNearKit(c);
                RoofVertices[0] += kit.VertexCount;
                RoofIndices[0] += kit.IndexCount;
            }
        }
    }

    /// <summary>One cell's near rooftop kit and near facades as mesh arrays (ChunkStreet.BuildNearKit), with its extent.
    /// Pooled arrays are longer than the mesh: use the counts.</summary>
    public sealed class NearKit
    {
        public float[] Positions;
        public uint[] Colors;
        public int[] Indices;
        /// <summary>All of it, and the rooftop kit's vertices and indices, first: the near facades' are the rest.</summary>
        public int VertexCount, IndexCount, RoofVertexCount, RoofIndexCount;
        public readonly float[] Min = new float[3], Max = new float[3];
        internal bool Pooled;

        /// <summary>Hands pooled arrays back once the mesh is uploaded; the arrays are gone after.</summary>
        public void Release()
        {
            if (!Pooled) return;
            Pooled = false;
            MeshArrays.Return(Positions);
            MeshArrays.Return(Colors);
            MeshArrays.Return(Indices);
            Positions = null;
            Colors = null;
            Indices = null;
        }
    }
}
