// Near facades (D-06c, ENVIRONMENT_ROADMAP): within StreetLod.NearDistance, the walls the far tier paints (D-06a)
// get their relief in geometry: sills, sunshades, box grilles, balconies, pilasters, shutter boxes, signboards,
// awnings, bands, columns, fins and shutter leaves, in one of eight near styles. The wall itself is not touched:
// the painted glass, shutters and signs stay where the far tier puts them, and everything here stands proud of the
// wall, placed on the same bays and floors and skipping the same bricked-up windows (the shader's hash, ported).
// Nothing is cut into a wall and no footprint changes.
//
// Styles, by the far-tier family, the building and its district (each a labelled inference; no facade is mapped):
// - WalkUp: the residential default, the dense walk-up apartments of most of the city: a sill and a cast sunshade
//   (chhajja) at every window, a steel box grille where the window has room, and on walls facing a road a balcony
//   with a solid parapet on about a third of the upper bays.
// - ShopHouse (mixed use): shops on the ground floor of a road-facing wall (pilasters between them, the rolling
//   shutter's box, a signboard over the painted one, an awning on about half), the district's homes above.
// - Office (commercial): mullions at the bay lines, a spandrel between floors, a signboard and a canopy over the
//   glazed shopfronts of a road-facing wall.
// - Market (a commercial building typed retail, shop, supermarket, mall or market, or classed commercial for its
//   size): shopfronts as shop-houses have, under the painted glass's rolled-up shutters, and a continuous sunshade
//   and sill band on each upper floor.
// - Industrial: pilasters at the bay lines, a hood over each strip window, and on a road-facing wall a roller
//   shutter, closed, in about half the ground-floor bays.
// - Institutional: a frame of columns and floor beams, a sunshade band at the window heads, a sill at each window.
// - OldDhaka (homes whose nearest named place is in Districts.OldDhaka): open shutter leaves in painted wood beside
//   each window, a hood moulding over it, a cornice at each floor line, and small balconies with iron railings.
// - Modern (homes whose nearest named place is in Districts.Modern: Gulshan, Banani, Baridhara, Dhanmondi and the
//   DOHS areas): slab edges at each floor line, balconies with glass railings on about half the upper bays of a
//   road-facing wall, and fins at every other bay line.
// Pieces that cover much of a wall (spandrels, mullions, pilasters, columns, beams, fins, bands, cornices, sunshades) are
// in the wall's own material (D-06b's skin), so a building keeps its colour as they fade in; only small pieces (sills,
// rails, grilles, shutters, signs, slab edges, glass) have colours of their own.
//
// Room: each bay looks straight out from its centre for the nearest footprint edge of the chunk (any building,
// its own included) up to ClearReach, and to the chunk border plus BorderSlack (what stands beyond is not known).
// A bay with less than MinClear gets nothing (party walls and close neighbours); elsewhere nothing reaches further
// than the room less ClearMargin, and a balcony needs its whole depth.
//
// Levels of detail without popping: each cell's near facades are built on a worker with its rooftop kit (K-45,
// ChunkStreet.BuildNearKit) and shown with the cell's near tier, in their own material (WorldNear: Dad/World with
// _DAD_NEAR). The shader dissolves each pixel of them in by a screen-door dither as its distance from the focus,
// measured as StreetLod measures (on the ground plan, and the focus's height above 10 m), goes from FadeEnd down to
// FadeStart: nothing of them is drawn beyond FadeEnd, all of them nearer than FadeStart. Every piece belongs to the
// cell of its bay's centre and lies within MaxReach of it, so none is drawn when its cell turns near or far.
// Sliding pieces out of the wall instead would pop in colour (a glass railing or a shutter leaf crossing the wall
// face shows its whole front at once).
// Only buildings on the ground, and only their outer ring; walls cut along the chunk border and walls shorter than
// MinFacadeWall stay plain, as in the far tier. Milestone 2's validation-area buildings keep their own facades.
// No UnityEngine dependency: safe on worker threads.
using System;
using System.Buffers;
using System.Collections.Generic;

namespace Dad.World
{
    /// <summary>The near facade drawn in geometry (D-06c; see NearFacades.cs).</summary>
    public enum NearStyle : byte { None, WalkUp, ShopHouse, Office, Market, Industrial, Institutional, OldDhaka, Modern }

    /// <summary>What a near facade needs to know of a building's district: from the nearest named place (Districts).</summary>
    public enum District : byte { Other, OldDhaka, Modern }

    /// <summary>The named places (places.json), for the district rule: a building takes the district of the nearest named
    /// place within Reach. A labelled inference: the data has no district boundaries.</summary>
    public static class Districts
    {
        public const double Reach = 1500;

        /// <summary>Old Dhaka: the quarters of the old city between the Buriganga and Fulbaria, by their English names in places.json.</summary>
        public static readonly HashSet<string> OldDhaka = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Babu bazar", "Badamtoli", "Islampur", "Shakhari Bazar", "Patuatuli", "Tanti bazar", "Armanitola", "Chawkbazar",
            "Becharam Dewri", "Muqim Katra", "Moulovibazar", "Begum Bazar", "Churihatta", "Bongshal", "Nazira Bazar", "Sat Rawza",
            "Alu Bazar", "Khaze Dewan", "Kayettuli", "Siddik Bazar", "Bokshibazar", "Chankharpul", "Nimtali", "Anandabazar",
            "Koshaituli", "Mahuttuli", "Shikkatuli", "Noyabazar", "Malitola", "Zindabahar", "Ray Shaheb Bazar", "Nolgola",
            "Rahmatganj", "Islambag", "Lakshmibazar", "Bangla Bazar", "Sutrapur", "Kulutola", "Shingtola", "Dholaikhal",
            "Koltabazar", "Narinda", "Farashganj", "Shayam Bazar", "Kagojitola", "Dailpotti", "Wari", "Nawabpur", "Gendaria",
            "Rishipara", "Bania Nagar", "Lalbagh",
        };

        /// <summary>Planned residential areas of modern apartment blocks.</summary>
        public static readonly HashSet<string> Modern = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Gulshan", "Gulshan 1", "Gulshan 2", "Banani", "Baridhara", "Baridhara DOHS", "Dhanmondi", "Mirpur DOHS",
        };

        internal sealed class Places
        {
            public double[] X = Array.Empty<double>(), Z = Array.Empty<double>();
            public District[] Kind = Array.Empty<District>();
        }

        static volatile Places places = new Places();

        internal static Places Current => places;

        /// <summary>Named places set, and of them in each district.</summary>
        public static int Count => places.X.Length;
        public static int CountOf(District d)
        {
            int n = 0;
            foreach (var k in places.Kind) if (k == d) n++;
            return n;
        }

        /// <summary>Sets the named places: world metres east and north, and English names (null for none). Call before chunks
        /// are built; builds already running keep the places they started with.</summary>
        public static void Set(double[] x, double[] z, string[] names)
        {
            var p = new Places { X = (double[])x.Clone(), Z = (double[])z.Clone(), Kind = new District[x.Length] };
            for (int i = 0; i < x.Length; i++)
                p.Kind[i] = names[i] == null ? District.Other : OldDhaka.Contains(names[i]) ? District.OldDhaka : Modern.Contains(names[i]) ? District.Modern : District.Other;
            places = p;
        }

        /// <summary>Forgets the named places (tests).</summary>
        public static void Clear() => places = new Places();

        /// <summary>The places that may be nearest within Reach of a point in the box (x0, z0)-(x1, z1), world metres.</summary>
        internal static void Near(Places p, double x0, double z0, double x1, double z1, List<int> into)
        {
            into.Clear();
            for (int i = 0; i < p.X.Length; i++)
            {
                double dx = Math.Max(0, Math.Max(x0 - p.X[i], p.X[i] - x1)), dz = Math.Max(0, Math.Max(z0 - p.Z[i], p.Z[i] - z1));
                if (dx * dx + dz * dz < Reach * Reach) into.Add(i);
            }
        }

        /// <summary>The district of the nearest named place within Reach of (x, z), world metres, among those listed; Other if none.</summary>
        internal static District Of(Places p, List<int> near, double x, double z)
        {
            double best = Reach * Reach;
            var kind = District.Other;
            foreach (int i in near)
            {
                double dx = p.X[i] - x, dz = p.Z[i] - z, d = dx * dx + dz * dz;
                if (d < best) { best = d; kind = p.Kind[i]; }
            }
            return kind;
        }

        /// <summary>The district of the nearest named place within Reach of (x, z), world metres; Other if none.</summary>
        public static District Of(double x, double z)
        {
            var p = places;
            var all = new List<int>(p.X.Length);
            for (int i = 0; i < p.X.Length; i++) all.Add(i);
            return Of(p, all, x, z);
        }
    }

    public sealed partial class ChunkBuildings
    {
        /// <summary>Where the near facades dissolve in: from none at FadeEnd to all at FadeStart (see the header).</summary>
        public const double FadeStart = StreetLod.NearDistance - 40, FadeEnd = StreetLod.NearDistance - 10;
        /// <summary>How far a piece reaches from its bay's centre at most, on the ground plan: half the widest bay (a wall
        /// shorter than two bays is one bay) and a pilaster, and a balcony's depth.</summary>
        public const double MaxReach = 6.3 + 1.4;
        public const double MinClear = 1.0, ClearMargin = 0.4, ClearReach = 12, BorderSlack = 1.0;
        public const uint Trim = 0x9c978d, IronRail = 0x2a2826, Steel = 0x8d9194, GlassRail = 0x5a7480, Slab = 0xc4c1b8;
        static readonly uint[] Leaves = { 0x3f6b4a, 0x3d5f7a, 0x8a6a3a, 0x5e4330 };

        // The far tier's boxes in a bay-and-floor cell (x0, y0, x1, y1), as Dad/World's FacadeAt draws them.
        public static readonly double[] WinWalkUp = { 0.22, 0.30, 0.78, 0.78 }, WinCommercial = { 0.08, 0.22, 0.92, 0.86 },
            WinIndustrial = { 0.12, 0.62, 0.88, 0.86 }, WinInstitutional = { 0.18, 0.26, 0.82, 0.80 },
            ShopOpening = { 0.08, 0, 0.92, 0.70 }, ShopSign = { 0.03, 0.76, 0.97, 0.93 }, ShopGlass = { 0.05, 0, 0.95, 0.74 };
        /// <summary>The far tier bricks up a window of a home when the cell's second hash is over this.</summary>
        public const double Bricked = 0.93;

        /// <summary>One patterned outer wall, kept for its cells' near facades: its start, direction and outward normal
        /// (chunk metres), length, base, floor height, bays and floors below the roof, and the building's facade.</summary>
        public struct FacadeWall
        {
            public float Px, Pz, Ux, Uz, Nx, Nz, Length, Base, Floor;
            public ushort Bays;
            public byte Floors, Seed;
            public NearStyle Style;
            public District District;
            public FacadeFamily Family;
            /// <summary>Faces a vehicle road (BuildingClass.Near.Faces); Shops: and the far tier paints shops on it.</summary>
            public bool Road, Shops;
            /// <summary>The wall's colour and material, packed (WorldMaterials.Pack).</summary>
            public uint Wall;
            /// <summary>The wall's first bay in BayClear.</summary>
            public int FirstBay;
        }

        /// <summary>Near facade walls by cell: cell c's run from NearWallStart[c] to NearWallStart[c + 1] (a wall whose bays
        /// fall in several cells is listed in each). Built pooled, the arrays are longer and live until ReleaseNearWalls.</summary>
        public FacadeWall[] NearWalls;
        public readonly int[] NearWallStart = new int[ChunkStreet.Cells * ChunkStreet.Cells + 1];
        /// <summary>Per bay of the near facade walls: the room straight out, in decimetres (ClearReach at most).</summary>
        public byte[] BayClear;
        /// <summary>Walls kept for near facades; bays of the patterned outer walls, and of them with too little room for any.</summary>
        public int NearWallCount, NearBays, NearBaysClosed;
        /// <summary>Kept walls by style (NearStyle as index).</summary>
        public readonly int[] NearStyles = new int[9];
        /// <summary>Near facade vertices and indices of every cell (CountNearFacades; tests and tools).</summary>
        public int NearVertices, NearIndices;
        bool nearPooled;
        // The thread's near-facade scratch, held only while Build runs.
        NearScratch ns;

        // Up to 64K walls a chunk, 32 of a size, as the rooftop kit's items (Rooftops.cs).
        static readonly ArrayPool<FacadeWall> wallPool = ArrayPool<FacadeWall>.Create(1 << 16, 32);
        static readonly ArrayPool<byte> clearPool = ArrayPool<byte>.Create(1 << 18, 32);

        public bool HasNearFacade(int cell) => NearWallStart[cell + 1] > NearWallStart[cell];

        static readonly HashSet<string> Markets = new HashSet<string> { "retail", "shop", "supermarket", "mall", "market" };

        /// <summary>A building's near style (see the header). type: its building=* value.</summary>
        public static NearStyle StyleOf(BuildingCategory category, ClassSource source, string type, District district)
        {
            switch (category)
            {
                case BuildingCategory.Residential:
                case BuildingCategory.Other:
                    return Homes(district);
                case BuildingCategory.MixedUse: return NearStyle.ShopHouse;
                case BuildingCategory.Commercial: return source == ClassSource.RuleLarge || type != null && Markets.Contains(type) ? NearStyle.Market : NearStyle.Office;
                case BuildingCategory.Industrial: return NearStyle.Industrial;
                case BuildingCategory.Institutional: return NearStyle.Institutional;
                default: return NearStyle.None;
            }
        }

        /// <summary>Does a building's style depend on its district?</summary>
        public static bool ByDistrict(BuildingCategory category) =>
            category == BuildingCategory.Residential || category == BuildingCategory.Other || category == BuildingCategory.MixedUse;

        static NearStyle Homes(District d) => d == District.OldDhaka ? NearStyle.OldDhaka : d == District.Modern ? NearStyle.Modern : NearStyle.WalkUp;

        /// <summary>The builder's state for near facades: the chunk's footprint edges in a grid, its candidate named places,
        /// and the walls and bays kept.</summary>
        internal sealed class NearScratch
        {
            const double Cell = 10;
            readonly List<double> edges = new List<double>(32768);
            readonly Dictionary<long, List<int>> grid = new Dictionary<long, List<int>>(4096);
            readonly Stack<List<int>> spare = new Stack<List<int>>();
            internal readonly List<FacadeWall> Walls = new List<FacadeWall>(4096);
            internal readonly List<ushort> Cells = new List<ushort>(4096);
            internal readonly List<byte> Clear = new List<byte>(32768);
            internal readonly List<int> Places = new List<int>(64);
            internal Districts.Places Named;

            internal void Reset(DadcChunk c)
            {
                foreach (var l in grid.Values) { l.Clear(); spare.Push(l); }
                grid.Clear();
                edges.Clear();
                Walls.Clear();
                Cells.Clear();
                Clear.Clear();
                double x0 = c.CornerEastCm / 100.0, z0 = c.CornerNorthCm / 100.0, size = DadcChunk.ChunkCm / 100.0;
                Named = Districts.Current;
                Districts.Near(Named, x0, z0, x0 + size, z0 + size, Places);
                if (c.Buildings == null) return;
                foreach (var b in c.Buildings)
                {
                    var o = b.Outer;
                    if (o == null || o.Length < 6) continue;
                    int n = o.Length / 2;
                    for (int i = 0, j = n - 1; i < n; j = i, i++)
                    {
                        double ax = o[2 * j] / 100.0, az = o[2 * j + 1] / 100.0, bx = o[2 * i] / 100.0, bz = o[2 * i + 1] / 100.0;
                        int e = edges.Count / 4;
                        edges.Add(ax); edges.Add(az); edges.Add(bx); edges.Add(bz);
                        int gx0 = (int)Math.Floor(Math.Min(ax, bx) / Cell), gx1 = (int)Math.Floor(Math.Max(ax, bx) / Cell);
                        int gz0 = (int)Math.Floor(Math.Min(az, bz) / Cell), gz1 = (int)Math.Floor(Math.Max(az, bz) / Cell);
                        for (int gx = gx0; gx <= gx1; gx++)
                            for (int gz = gz0; gz <= gz1; gz++)
                            {
                                long key = ((long)gx << 32) ^ (uint)gz;
                                if (!grid.TryGetValue(key, out var l)) grid[key] = l = spare.Count > 0 ? spare.Pop() : new List<int>(8);
                                l.Add(e);
                            }
                    }
                }
            }

            /// <summary>The room straight out from (x, z) along (nx, nz), chunk metres: to the nearest footprint edge, or the
            /// chunk border plus BorderSlack, ClearReach at most.</summary>
            internal double Room(double x, double z, double nx, double nz)
            {
                double size = DadcChunk.ChunkCm / 100.0, best = ClearReach;
                // The chunk border: what stands beyond it is not known.
                if (nx > 1e-6) best = Math.Min(best, (size - x) / nx + BorderSlack);
                if (nx < -1e-6) best = Math.Min(best, -x / nx + BorderSlack);
                if (nz > 1e-6) best = Math.Min(best, (size - z) / nz + BorderSlack);
                if (nz < -1e-6) best = Math.Min(best, -z / nz + BorderSlack);
                // From 5 cm out, so the wall itself is not met.
                double ox = x + nx * 0.05, oz = z + nz * 0.05, ex = ox + nx * best, ez = oz + nz * best;
                int gx0 = (int)Math.Floor(Math.Min(ox, ex) / Cell), gx1 = (int)Math.Floor(Math.Max(ox, ex) / Cell);
                int gz0 = (int)Math.Floor(Math.Min(oz, ez) / Cell), gz1 = (int)Math.Floor(Math.Max(oz, ez) / Cell);
                for (int gx = gx0; gx <= gx1; gx++)
                    for (int gz = gz0; gz <= gz1; gz++)
                    {
                        if (!grid.TryGetValue(((long)gx << 32) ^ (uint)gz, out var l)) continue;
                        foreach (int e in l)
                        {
                            double ax = edges[4 * e], az = edges[4 * e + 1], sx = edges[4 * e + 2] - ax, sz = edges[4 * e + 3] - az;
                            double den = nx * sz - nz * sx;
                            if (Math.Abs(den) < 1e-9) continue;
                            double t = ((ax - ox) * sz - (az - oz) * sx) / den, u = ((ax - ox) * nz - (az - oz) * nx) / den;
                            if (t >= 0 && u >= 0 && u <= 1) best = Math.Min(best, t + 0.05);
                        }
                    }
                return Math.Max(0, best);
            }
        }

        /// <summary>A building's near style and district, from the centre of its outer ring (flat's first n points, chunk metres).</summary>
        NearStyle NearStyleOf(DadcChunk c, Building b, BuildingCategory category, ClassSource source, List<double> flat, int n, out District district)
        {
            district = District.Other;
            if (ByDistrict(category) && ns.Places.Count > 0)
            {
                double x = 0, z = 0;
                for (int i = 0; i < n; i++) { x += flat[2 * i]; z += flat[2 * i + 1]; }
                district = Districts.Of(ns.Named, ns.Places, c.CornerEastCm / 100.0 + x / n, c.CornerNorthCm / 100.0 + z / n);
            }
            return StyleOf(category, source, b.Type, district);
        }

        /// <summary>Keeps a patterned outer wall from p to q (chunk metres, outward normal n) for near facades, with its bays' room,
        /// listed in the cells of its bays with room (none: not kept).</summary>
        void KeepNearWall(double px, double pz, double qx, double qz, double nx, double nz, double baseY, double topY, uint wall, in FarTier far, bool road)
        {
            if (far.Style == NearStyle.None) return;
            double dx = qx - px, dz = qz - pz, len = Math.Sqrt(dx * dx + dz * dz);
            int bays = Math.Max(1, (int)Math.Floor(len / far.Bay));
            int floors = far.RoofFloors > 0 ? far.RoofFloors : (int)Math.Round((topY - baseY) / far.Floor);
            if (floors < 1 || bays > ushort.MaxValue) return;
            var fw = new FacadeWall
            {
                Px = (float)px, Pz = (float)pz, Ux = (float)(dx / len), Uz = (float)(dz / len), Nx = (float)nx, Nz = (float)nz, Length = (float)len,
                Base = (float)baseY, Floor = (float)far.Floor, Bays = (ushort)bays, Floors = (byte)Math.Min(floors, 255), Seed = (byte)far.Seed,
                Style = far.Style, District = far.District, Family = far.Family, Road = road, Shops = far.Shops && road, Wall = wall,
                FirstBay = ns.Clear.Count,
            };
            ushort cells = 0;
            for (int k = 0; k < bays; k++)
            {
                // From the wall's stored frame, as the drawing finds its bays (Mine).
                BayCentre(fw, k, out double x, out double z);
                // Whole decimetres, rounded down (a hair under: 1.2 m measured is 1.1999...).
                int room = Math.Min(255, (int)Math.Floor(ns.Room(x, z, nx, nz) * 10 + 1e-6));
                ns.Clear.Add((byte)room);
                if (room < MinClear * 10) NearBaysClosed++;
                else cells |= (ushort)(1 << ChunkStreet.CellOf(x, z));
            }
            NearBays += bays;
            if (cells == 0)
            {
                ns.Clear.RemoveRange(fw.FirstBay, bays);
                return;
            }
            NearStyles[(int)far.Style]++;
            ns.Walls.Add(fw);
            ns.Cells.Add(cells);
        }

        /// <summary>Copies the kept walls out by cell (pooled when the chunk is).</summary>
        void KeepNearWalls(bool pooled)
        {
            int total = 0;
            for (int i = 0; i < ns.Cells.Count; i++) total += PopCount(ns.Cells[i]);
            nearPooled = pooled;
            NearWalls = pooled ? wallPool.Rent(total) : new FacadeWall[total];
            BayClear = pooled ? clearPool.Rent(ns.Clear.Count) : new byte[ns.Clear.Count];
            ns.Clear.CopyTo(BayClear);
            int at = 0;
            for (int cell = 0; cell < ChunkStreet.Cells * ChunkStreet.Cells; cell++)
            {
                NearWallStart[cell] = at;
                for (int i = 0; i < ns.Walls.Count; i++)
                    if ((ns.Cells[i] & (1 << cell)) != 0) NearWalls[at++] = ns.Walls[i];
            }
            NearWallStart[ChunkStreet.Cells * ChunkStreet.Cells] = at;
            NearWallCount = ns.Walls.Count;
        }

        static int PopCount(ushort v)
        {
            int n = 0;
            for (; v != 0; v &= (ushort)(v - 1)) n++;
            return n;
        }

        /// <summary>Hands the pooled near walls back; no near facade may be built after, and none may be building.</summary>
        public void ReleaseNearWalls()
        {
            if (!nearPooled) return;
            nearPooled = false;
            wallPool.Return(NearWalls);
            clearPool.Return(BayClear);
            NearWalls = null;
            BayClear = null;
        }

        /// <summary>Fills NearVertices and NearIndices by building every cell's near facades (tests and tools); returns the
        /// largest cell's vertices and indices.</summary>
        public (int vertices, int indices) CountNearFacades()
        {
            NearVertices = NearIndices = 0;
            int maxV = 0, maxI = 0;
            var w = new KitWriter(new List<float>(), new List<uint>(), new List<int>());
            for (int cell = 0; cell < ChunkStreet.Cells * ChunkStreet.Cells; cell++)
            {
                w.P.Clear(); w.C.Clear(); w.I.Clear();
                DrawNearFacades(cell, w);
                NearVertices += w.Count;
                NearIndices += w.I.Count;
                maxV = Math.Max(maxV, w.Count);
                maxI = Math.Max(maxI, w.I.Count);
            }
            return (maxV, maxI);
        }

        // ---- The shader's cell hash (Dad/World FacadeAt), so the near facade dresses the windows the far tier paints ----

        public static uint LowBias(uint x)
        {
            unchecked
            {
                x ^= x >> 16; x *= 0x7feb352du;
                x ^= x >> 15; x *= 0x846ca68bu;
                x ^= x >> 16;
                return x;
            }
        }

        public static double ToUnit(uint h) => (h >> 8) * (1.0 / 16777216.0);

        /// <summary>The far tier's hash for bay k and floor f of a wall with this seed: r0 and r1 as the shader's, r2 one more.</summary>
        public static void CellHash(int seed, int k, int f, out double r0, out double r1, out double r2)
        {
            unchecked
            {
                uint h = LowBias((uint)seed * 0x9e3779b9u ^ (uint)k * 0x85ebca6bu ^ (uint)f * 0xc2b2ae35u);
                r0 = ToUnit(h);
                r1 = ToUnit(LowBias(h + 1u));
                r2 = ToUnit(LowBias(h + 2u));
            }
        }

        /// <summary>How many shops' signs light up: the far tier's r3 below this. A signboard covers its painted sign, so it lights up with it.</summary>
        public const double LitSignShare = 0.45;

        /// <summary>Whether the far tier lights this bay's sign.</summary>
        public static bool SignLit(int seed, int k, int f)
        {
            unchecked
            {
                uint h = LowBias((uint)seed * 0x9e3779b9u ^ (uint)k * 0x85ebca6bu ^ (uint)f * 0xc2b2ae35u);
                return ToUnit(LowBias(h + 3u)) < LitSignShare;
            }
        }

        /// <summary>The window box the far tier paints for a family (on any floor without shops).</summary>
        public static double[] WindowOf(FacadeFamily f) => f switch
        {
            FacadeFamily.Commercial => WinCommercial,
            FacadeFamily.Industrial => WinIndustrial,
            FacadeFamily.Institutional => WinInstitutional,
            _ => WinWalkUp,
        };

        // ---- Drawing ----

        /// <summary>Draws cell's near facades with w (indices relative to w.Base). Reads only NearWalls and BayClear, so it
        /// may run on any thread, for several cells at once.</summary>
        internal void DrawNearFacades(int cell, KitWriter w)
        {
            if (NearWalls == null) return;
            for (int r = NearWallStart[cell]; r < NearWallStart[cell + 1]; r++) DrawNearWall(cell, NearWalls[r], w);
        }

        void DrawNearWall(int cell, in FacadeWall fw, KitWriter w)
        {
            var f = new Face { Px = fw.Px, Pz = fw.Pz, Ux = fw.Ux, Uz = fw.Uz, Nx = fw.Nx, Nz = fw.Nz, Wall = fw.Wall, Foot = fw.Wall };
            double bw = fw.Length / fw.Bays;
            uint trim = WorldMaterials.Pack(Trim, WorldMaterial.Concrete);
            // Mixed use: the district's homes above the shops.
            NearStyle upper = fw.Style == NearStyle.ShopHouse ? Homes(fw.District) : fw.Style;
            // Runs of consecutive bays of this cell with room, for the pieces that run along the wall.
            int k = 0;
            while (k < fw.Bays)
            {
                if (!Mine(cell, fw, k) || Room(fw, k) < MinClear) { k++; continue; }
                int k0 = k;
                double cap = double.MaxValue;
                for (; k < fw.Bays && Mine(cell, fw, k) && Room(fw, k) >= MinClear; k++) cap = Math.Min(cap, Room(fw, k) - ClearMargin);
                Run(f, fw, upper, k0 * bw, k * bw, cap, trim, w);
            }
            for (k = 0; k < fw.Bays; k++)
            {
                if (!Mine(cell, fw, k) || Room(fw, k) < MinClear) continue;
                Bay(f, fw, upper, k, bw, Room(fw, k) - ClearMargin, trim, w);
            }
        }

        static bool Mine(int cell, in FacadeWall fw, int k)
        {
            BayCentre(fw, k, out double x, out double z);
            return ChunkStreet.CellOf(x, z) == cell;
        }

        /// <summary>The centre of bay k on the wall's face. The listing and the drawing both take it from here, so a bay near a
        /// cell line falls in the same cell for both.</summary>
        static void BayCentre(in FacadeWall fw, int k, out double x, out double z)
        {
            double s = (k + 0.5) * (fw.Length / fw.Bays);
            x = fw.Px + fw.Ux * s;
            z = fw.Pz + fw.Uz * s;
        }

        double Room(in FacadeWall fw, int k) => BayClear[fw.FirstBay + k] / 10.0;

        // Faces of a Shelf besides its front.
        const int Top = 1, Bottom = 2, Ends = 4, Closed = Top | Bottom | Ends;

        /// <summary>A box on the wall from s0 to s1 along and h0 to h1 up, depth out, on 8 vertices: its front and the faces
        /// asked for (Top, Bottom, Ends); never the face against the wall, which is never seen.</summary>
        static void Shelf(in Face f, KitWriter w, double s0, double s1, double h0, double h1, double depth, uint colour, int faces)
        {
            if (s1 - s0 < 1e-4 || h1 - h0 < 1e-4 || depth < 1e-4) return;
            double ax = f.Px + f.Ux * s0, az = f.Pz + f.Uz * s0, bx = f.Px + f.Ux * s1, bz = f.Pz + f.Uz * s1, ox = f.Nx * depth, oz = f.Nz * depth;
            int a0 = w.V(ax, h0, az, colour), b0 = w.V(bx, h0, bz, colour), a1 = w.V(ax, h1, az, colour), b1 = w.V(bx, h1, bz, colour);
            int c0 = w.V(ax + ox, h0, az + oz, colour), d0 = w.V(bx + ox, h0, bz + oz, colour);
            int c1 = w.V(ax + ox, h1, az + oz, colour), d1 = w.V(bx + ox, h1, bz + oz, colour);
            w.Quad(c0, d0, d1, c1, f.Nx, 0, f.Nz);
            if ((faces & Top) != 0) w.Quad(a1, b1, d1, c1, 0, 1, 0);
            if ((faces & Bottom) != 0) w.Quad(a0, b0, d0, c0, 0, -1, 0);
            if ((faces & Ends) != 0)
            {
                w.Quad(a0, c0, c1, a1, -f.Ux, 0, -f.Uz);
                w.Quad(b0, d0, d1, b1, f.Ux, 0, f.Uz);
            }
        }

        /// <summary>Pieces along a run of bays from a to b (metres along the wall), one per floor: bands, beams, cornices, slab edges.</summary>
        static void Run(in Face f, in FacadeWall fw, NearStyle upper, double a, double b, double cap, uint trim, KitWriter w)
        {
            double fh = fw.Floor, y0 = fw.Base;
            int floors = fw.Floors;
            switch (fw.Style)
            {
                case NearStyle.Office:
                    // Spandrels from one window head to the next floor's sill, in the wall's own material: they cover a third
                    // of the wall, so a colour of their own would change the building's colour as it fades in.
                    for (int fl = 0; fl + 1 < floors; fl++)
                        Shelf(f, w, a, b, y0 + (fl + WinCommercial[3]) * fh, y0 + (fl + 1 + WinCommercial[1]) * fh, Math.Min(0.06, cap), fw.Wall, Closed);
                    // A canopy over the shopfronts.
                    if (fw.Shops && cap >= 0.6)
                        Shelf(f, w, a, b, y0 + ShopSign[3] * fh + 0.02, y0 + ShopSign[3] * fh + 0.14, Math.Min(1.2, cap), trim, Closed);
                    break;
                case NearStyle.Market:
                    for (int fl = 1; fl < floors; fl++)
                    {
                        double head = y0 + (fl + WinCommercial[3]) * fh, sill = y0 + (fl + WinCommercial[1]) * fh;
                        if (cap >= 0.2) Shelf(f, w, a, b, head + 0.04, head + 0.12, Math.Min(0.6, cap), fw.Wall, Closed);
                        Shelf(f, w, a, b, sill - 0.08, sill, Math.Min(0.08, cap), trim, Top | Bottom);
                    }
                    break;
                case NearStyle.Institutional:
                    for (int fl = 0; fl < floors; fl++)
                    {
                        double head = y0 + (fl + WinInstitutional[3]) * fh;
                        if (fl > 0) Shelf(f, w, a, b, y0 + fl * fh - 0.18, y0 + fl * fh + 0.18, Math.Min(0.25, cap), fw.Wall, Closed);
                        if (cap >= 0.2) Shelf(f, w, a, b, head + 0.04, head + 0.12, Math.Min(0.6, cap), fw.Wall, Closed);
                    }
                    break;
            }
            switch (upper)
            {
                case NearStyle.OldDhaka:
                    // A two-step cornice at each floor line.
                    for (int fl = 1; fl < floors; fl++)
                    {
                        double y = y0 + fl * fh;
                        Shelf(f, w, a, b, y - 0.12, y + 0.06, Math.Min(0.2, cap), fw.Wall, Closed);
                        Shelf(f, w, a, b, y + 0.06, y + 0.12, Math.Min(0.1, cap), trim, Top | Ends);
                    }
                    break;
                case NearStyle.Modern:
                    for (int fl = 1; fl < floors; fl++)
                        Shelf(f, w, a, b, y0 + fl * fh - 0.15, y0 + fl * fh + 0.05, Math.Min(0.2, cap), WorldMaterials.Pack(Slab, WorldMaterial.Concrete), Closed);
                    break;
            }
        }

        /// <summary>The pieces of bay k on every floor (see the header for each style).</summary>
        static void Bay(in Face f, in FacadeWall fw, NearStyle upper, int k, double bw, double cap, uint trim, KitWriter w)
        {
            double fh = fw.Floor, y0 = fw.Base, s0 = k * bw, top = y0 + fw.Floors * fh;
            double end = k == fw.Bays - 1 ? s0 + bw : -1;
            var win = WindowOf(fw.Family);
            switch (fw.Style)
            {
                // Members running the wall's height, in its own material (see the spandrels in Run).
                case NearStyle.Office: Line(f, s0, y0, top, 0.04, Math.Min(0.12, cap), fw.Wall, w, end); break;
                case NearStyle.Industrial: Line(f, s0, y0, top, 0.2, Math.Min(0.25, cap), fw.Wall, w, end); break;
                case NearStyle.Institutional: Line(f, s0, y0, top, 0.18, Math.Min(0.3, cap), fw.Wall, w, end); break;
            }
            if (upper == NearStyle.Modern && k % 2 == 0 && cap >= 0.3)
                Line(f, s0, y0 + (fw.Shops ? fh : 0), top, 0.07, Math.Min(0.5, cap), fw.Wall, w, -1);
            for (int fl = 0; fl < fw.Floors; fl++)
            {
                CellHash(fw.Seed, k, fl, out double r0, out double r1, out double r2);
                double h0 = y0 + fl * fh;
                if (fl == 0 && fw.Shops)
                {
                    Shop(f, fw, s0, bw, h0, r0, r1, r2, cap, w, end >= 0, SignLit(fw.Seed, k, fl));
                    continue;
                }
                // The far tier bricks up a few windows of homes: nothing to dress there.
                if (fw.Family != FacadeFamily.Commercial && r1 > Bricked) continue;
                double a = s0 + win[0] * bw, b = s0 + win[2] * bw, lo = h0 + win[1] * fh, hi = h0 + win[3] * fh;
                switch (fw.Style == NearStyle.ShopHouse ? upper : fw.Style)
                {
                    case NearStyle.WalkUp:
                        SillBox(f, a, b, lo, cap, trim, w);
                        Sunshade(f, a, b, hi, cap, fw.Wall, w);
                        if (fl > 0 && fw.Road && r2 < 0.33 && cap >= 1.0 && bw >= 2.6) Balcony(f, s0, bw, h0, 1.0, fw.Wall, w);
                        else if (cap >= 0.25) BoxGrille(f, a, b, lo, hi, w);
                        break;
                    case NearStyle.OldDhaka:
                        SillBox(f, a, b, lo, cap, trim, w);
                        // A hood moulding, and the shutter leaves folded back against the wall either side.
                        if (cap >= 0.12) Shelf(f, w, a - 0.12, b + 0.12, hi + 0.05, hi + 0.2, 0.12, trim, Closed);
                        if (cap >= 0.04)
                        {
                            uint wood = WorldMaterials.Pack(Leaves[(int)(r0 * Leaves.Length) % Leaves.Length], WorldMaterial.Paint);
                            double leaf = Math.Min((b - a) / 2, Math.Max(0, a - s0 - 0.04));
                            if (leaf > 0.15)
                            {
                                Shelf(f, w, a - leaf - 0.02, a - 0.02, lo, hi, 0.04, wood, Ends);
                                Shelf(f, w, b + 0.02, b + leaf + 0.02, lo, hi, 0.04, wood, Ends);
                            }
                        }
                        if (fl > 0 && r2 < 0.2 && cap >= 0.8) IronBalcony(f, a - 0.15, b + 0.15, h0, 0.7, fw.Wall, w);
                        break;
                    case NearStyle.Modern:
                        SillBox(f, a, b, lo, cap, trim, w);
                        if (fl > 0 && fw.Road && r2 < 0.5 && cap >= 1.3 && bw >= 2.6) GlassBalcony(f, s0 + 0.15, s0 + bw - 0.15, h0, 1.3, w);
                        break;
                    case NearStyle.Industrial:
                        if (fl == 0 && fw.Road && r2 < 0.5) { RollerShutter(f, s0, bw, h0, fh, cap, trim, w); break; }
                        // A hood over the strip window.
                        if (cap >= 0.1) Shelf(f, w, a - 0.1, b + 0.1, hi + 0.05, hi + 0.12, Math.Min(0.3, cap), fw.Wall, Closed);
                        SillBox(f, a, b, lo, cap, trim, w);
                        break;
                    case NearStyle.Institutional:
                        SillBox(f, a, b, lo, cap, trim, w);
                        break;
                }
            }
        }

        /// <summary>A vertical member centred at s on the wall from y0 to y1, half wide, depth out; another at end when end >= 0.</summary>
        static void Line(in Face f, double s, double y0, double y1, double half, double depth, uint colour, KitWriter w, double end)
        {
            if (depth < 0.02) return;
            Shelf(f, w, s - half, s + half, y0, y1, depth, colour, Top | Ends);
            if (end >= 0) Shelf(f, w, end - half, end + half, y0, y1, depth, colour, Top | Ends);
        }

        /// <summary>A sill under a window: its top, front and underside.</summary>
        static void SillBox(in Face f, double a, double b, double lo, double cap, uint colour, KitWriter w)
        {
            if (cap < 0.05) return;
            Shelf(f, w, a - 0.06, b + 0.06, lo - 0.07, lo, Math.Min(0.08, cap), colour, Top | Bottom);
        }

        /// <summary>A cast sunshade (chhajja) over a window.</summary>
        static void Sunshade(in Face f, double a, double b, double hi, double cap, uint colour, KitWriter w)
        {
            if (cap < 0.1) return;
            Shelf(f, w, a - 0.15, b + 0.15, hi + 0.08, hi + 0.16, Math.Min(0.45, cap), colour, Closed);
        }

        /// <summary>A steel box grille standing 14 cm out over a window: a plate on top, posts at its front corners and a rail
        /// along its foot. Its bars are the ones the far tier paints on the glass behind: bars 2 cm thick in geometry
        /// would break into sparkling single pixels beyond about 20 m.</summary>
        static void BoxGrille(in Face f, double a, double b, double lo, double hi, KitWriter w)
        {
            const double d = 0.14, t = 0.015;
            uint g = WorldMaterials.Pack(Grille, WorldMaterial.Metal);
            Shelf(f, w, a, b, hi, hi + 0.02, d + 0.01, g, Top | Bottom);
            f.Pane(w, a, a + 2 * t, lo, hi, d, g);
            f.Pane(w, b - 2 * t, b, lo, hi, d, g);
            f.Pane(w, a, b, lo, lo + 0.03, d, g);
        }

        /// <summary>A balcony on the floor at y: a slab, a solid parapet in front and short side walls.</summary>
        static void Balcony(in Face f, double s0, double bw, double y, double depth, uint colour, KitWriter w)
        {
            double a = s0 + 0.1, b = s0 + bw - 0.1;
            Shelf(f, w, a, b, y - 0.12, y, depth, colour, Closed);
            f.Block(w, a, b, y, y + 1.0, depth - 0.1, depth, colour);
            Shelf(f, w, a, a + 0.1, y, y + 1.0, depth - 0.1, colour, Top | Ends);
            Shelf(f, w, b - 0.1, b, y, y + 1.0, depth - 0.1, colour, Top | Ends);
        }

        /// <summary>A small cantilevered balcony with an iron railing (Old Dhaka): the slab, a top rail, and the railing's
        /// uprights at its ends and every 50 cm.</summary>
        static void IronBalcony(in Face f, double a, double b, double y, double depth, uint colour, KitWriter w)
        {
            uint iron = WorldMaterials.Pack(IronRail, WorldMaterial.Metal);
            Shelf(f, w, a, b, y - 0.1, y, depth, colour, Closed);
            f.Block(w, a, b, y + 0.9, y + 0.95, depth - 0.05, depth, iron);
            int bars = Math.Max(2, (int)Math.Round((b - a) / 0.5));
            for (int i = 0; i <= bars; i++)
            {
                double s = a + (b - a) * i / bars;
                f.Pane(w, Math.Max(a, s - 0.02), Math.Min(b, s + 0.02), y, y + 0.9, depth - 0.02, iron);
            }
            Shelf(f, w, a, a + 0.04, y + 0.9, y + 0.95, depth, iron, Top | Ends);
            Shelf(f, w, b - 0.04, b, y + 0.9, y + 0.95, depth, iron, Top | Ends);
        }

        /// <summary>A balcony with a glass railing and a steel top rail (modern apartments).</summary>
        static void GlassBalcony(in Face f, double a, double b, double y, double depth, KitWriter w)
        {
            Shelf(f, w, a, b, y - 0.15, y, depth, WorldMaterials.Pack(Slab, WorldMaterial.Concrete), Closed);
            f.Block(w, a, b, y, y + 1.05, depth - 0.03, depth, WorldMaterials.Pack(GlassRail, WorldMaterial.Glass));
            f.Block(w, a, b, y + 1.05, y + 1.09, depth - 0.05, depth + 0.01, WorldMaterials.Pack(Steel, WorldMaterial.Metal));
        }

        /// <summary>A ground-floor shop in the bay from s0 over the painted one: a signboard over the painted sign; on a
        /// shop-house or market also pilasters, the shutter's box and, on about half, an awning; on an office nothing more
        /// (its canopy runs along the wall).</summary>
        static void Shop(in Face f, in FacadeWall fw, double s0, double bw, double h0, double r0, double r1, double r2, double cap, KitWriter w, bool last, bool lit)
        {
            if (cap < 0.05) return;
            double fh = fw.Floor;
            uint sign = WorldMaterials.Pack(SignColours[(uint)(r1 * 6) % 6u], WorldMaterial.Paint);
            if (lit) sign = WorldMaterials.Night(sign, WorldMaterials.NightSign);
            Shelf(f, w, s0 + ShopSign[0] * bw, s0 + ShopSign[2] * bw, h0 + ShopSign[1] * fh, h0 + ShopSign[3] * fh, Math.Min(0.1, cap), sign, Closed);
            if (fw.Style == NearStyle.Office) return;
            var open = fw.Family == FacadeFamily.Commercial ? ShopGlass : ShopOpening;
            Line(f, s0, h0, h0 + ShopSign[1] * fh, 0.12, Math.Min(0.15, cap), fw.Wall, w, last ? s0 + bw : -1);
            double a = s0 + open[0] * bw, b = s0 + open[2] * bw, head = h0 + open[3] * fh;
            Shelf(f, w, a, b, head, head + 0.2, Math.Min(0.2, cap), WorldMaterials.Pack(ShutterBox, WorldMaterial.Metal), Closed);
            if (r2 < 0.5 && cap >= 0.5)
                f.Awning(w, a, b, head + 0.2, head - 0.15, Math.Min(0.9, cap), WorldMaterials.Pack(SignColours[(uint)(r0 * 6) % 6u], WorldMaterial.Paint));
        }

        /// <summary>A closed roller shutter in a ground-floor bay, with its frame and box (industrial).</summary>
        static void RollerShutter(in Face f, double s0, double bw, double h0, double fh, double cap, uint trim, KitWriter w)
        {
            if (cap < 0.3) return;
            double a = s0 + 0.15 * bw, b = s0 + 0.85 * bw, head = h0 + Math.Min(0.75 * fh, 4.0);
            Shelf(f, w, a - 0.12, a, h0, head, 0.15, trim, Ends);
            Shelf(f, w, b, b + 0.12, h0, head, 0.15, trim, Ends);
            f.Pane(w, a, b, h0, head, 0.03, WorldMaterials.Pack(Shutter, WorldMaterial.Metal));
            Shelf(f, w, a - 0.12, b + 0.12, head, head + 0.3, 0.3, WorldMaterials.Pack(ShutterBox, WorldMaterial.Metal), Closed);
        }
    }
}
