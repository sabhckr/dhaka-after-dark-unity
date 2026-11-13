// A category for every building (D-06a, master directive Part 8.1), and the far-tier facade family
// it gets. The data types 6.7 % of the city's buildings; the rest are inferred, and each answer says
// where it came from (Source), so inferred content can be told from mapped content.
//
// In order:
// 1. The building's own OSM type (building=*), by the table below. "yes" is no type.
// 2. The landuse polygon the piece's centre stands in (residential, commercial, retail, industrial,
//    education, religious, military, railway).
// 3. Rules, labelled:
//    - Tiny: a footprint under TinyArea m² is an informal structure (kiosk, shed, shack).
//    - Named large: a named footprint of LargeArea m² or more is a notable building (in the data: the
//      National Parliament, the Supreme Court, the conference centre) and stays plain (Special) until
//      it is modelled; its look is not guessed.
//    - Large: an unnamed footprint of LargeArea m² or more is a commercial hall (market, mall or office block).
//    - Main road: a corner within ChunkBuildings.ShopFacing (11 m) of the carriageway of a tertiary or
//      higher road makes a mixed-use shop-house (shops below, homes above), as Dhaka's main roads are lined.
//    - Otherwise a residential walk-up.
// A piece cut by the chunk grid (K-16) sees only its own part of the footprint, the landuse and the
// roads, so its neighbour could answer differently and the facade would change at the chunk border.
// Such pieces take step 1 only, else Residential by the rule "split"; every piece of one building
// agrees, because only the building's own tags are read. A building of several parts within one
// chunk (a multipolygon) is classified as one (ClassifyChunk): by its total area, as split if any
// part is on the border, else as its largest part says.
//
// Families are drawn by the Dad/World shader from the facade coordinates the builder writes
// (BuildingBuilder): bays along each wall and floors up it. Religious, special and informal buildings
// stay plain: no window grid is invented for a mosque, a station or a shack.
// No UnityEngine dependency: safe on worker threads.
using System;
using System.Collections.Generic;

namespace Dad.World
{
    public enum BuildingCategory : byte { Residential, MixedUse, Commercial, Industrial, Institutional, Religious, Special, Informal, Other }

    public enum ClassSource : byte { OsmType, Landuse, RuleTiny, RuleLarge, RuleMainRoad, RuleDefault, RuleSplit, RuleNamedLarge }

    /// <summary>The far-tier facade drawn in the shader; None is a plain wall.</summary>
    public enum FacadeFamily : byte { None, WalkUp, ShopHouse, Commercial, Industrial, Institutional }

    public static class BuildingClass
    {
        public const double TinyArea = 15, LargeArea = 3000;

        /// <summary>OSM building=* values; anything else that is not "yes" is Other.</summary>
        public static readonly Dictionary<string, BuildingCategory> ByType = new Dictionary<string, BuildingCategory>
        {
            ["house"] = BuildingCategory.Residential, ["detached"] = BuildingCategory.Residential, ["residential"] = BuildingCategory.Residential,
            ["apartments"] = BuildingCategory.Residential, ["terrace"] = BuildingCategory.Residential, ["semidetached_house"] = BuildingCategory.Residential,
            ["dormitory"] = BuildingCategory.Residential, ["bungalow"] = BuildingCategory.Residential, ["stilt_house"] = BuildingCategory.Residential,
            ["semi"] = BuildingCategory.Residential, ["flats"] = BuildingCategory.Residential,
            ["mixed"] = BuildingCategory.MixedUse, ["commercial;residential"] = BuildingCategory.MixedUse, ["residential;commercial"] = BuildingCategory.MixedUse,
            ["hut"] = BuildingCategory.Informal, ["shed"] = BuildingCategory.Informal, ["cabin"] = BuildingCategory.Informal, ["slum"] = BuildingCategory.Informal,
            ["shack"] = BuildingCategory.Informal,
            ["commercial"] = BuildingCategory.Commercial, ["retail"] = BuildingCategory.Commercial, ["office"] = BuildingCategory.Commercial,
            ["supermarket"] = BuildingCategory.Commercial, ["kiosk"] = BuildingCategory.Commercial, ["hotel"] = BuildingCategory.Commercial,
            ["shop"] = BuildingCategory.Commercial, ["mall"] = BuildingCategory.Commercial, ["market"] = BuildingCategory.Commercial,
            ["restaurant"] = BuildingCategory.Commercial, ["bank"] = BuildingCategory.Commercial,
            ["industrial"] = BuildingCategory.Industrial, ["warehouse"] = BuildingCategory.Industrial, ["factory"] = BuildingCategory.Industrial,
            ["manufacture"] = BuildingCategory.Industrial, ["garage"] = BuildingCategory.Industrial, ["garages"] = BuildingCategory.Industrial,
            ["storage_tank"] = BuildingCategory.Industrial, ["workshop"] = BuildingCategory.Industrial, ["service"] = BuildingCategory.Industrial,
            ["hangar"] = BuildingCategory.Industrial,
            ["school"] = BuildingCategory.Institutional, ["college"] = BuildingCategory.Institutional, ["university"] = BuildingCategory.Institutional,
            ["hospital"] = BuildingCategory.Institutional, ["clinic"] = BuildingCategory.Institutional, ["government"] = BuildingCategory.Institutional,
            ["public"] = BuildingCategory.Institutional, ["civic"] = BuildingCategory.Institutional, ["kindergarten"] = BuildingCategory.Institutional,
            ["fire_station"] = BuildingCategory.Institutional, ["police"] = BuildingCategory.Institutional, ["courthouse"] = BuildingCategory.Institutional,
            ["military"] = BuildingCategory.Institutional, ["barracks"] = BuildingCategory.Institutional, ["academic"] = BuildingCategory.Institutional,
            ["museum"] = BuildingCategory.Institutional,
            ["mosque"] = BuildingCategory.Religious, ["temple"] = BuildingCategory.Religious, ["church"] = BuildingCategory.Religious,
            ["religious"] = BuildingCategory.Religious, ["chapel"] = BuildingCategory.Religious, ["shrine"] = BuildingCategory.Religious,
            ["cathedral"] = BuildingCategory.Religious, ["pagoda"] = BuildingCategory.Religious, ["monastery"] = BuildingCategory.Religious,
            ["train_station"] = BuildingCategory.Special, ["transportation"] = BuildingCategory.Special, ["stadium"] = BuildingCategory.Special,
            ["cinema"] = BuildingCategory.Special, ["sports_hall"] = BuildingCategory.Special, ["grandstand"] = BuildingCategory.Special,
            ["parking"] = BuildingCategory.Special, ["roof"] = BuildingCategory.Special, ["bridge"] = BuildingCategory.Special,
            ["construction"] = BuildingCategory.Special, ["ruins"] = BuildingCategory.Special, ["toilets"] = BuildingCategory.Special,
            ["gatehouse"] = BuildingCategory.Special, ["sports_centre"] = BuildingCategory.Special, ["pavilion"] = BuildingCategory.Special,
            ["tower"] = BuildingCategory.Special, ["water_tower"] = BuildingCategory.Special,
        };

        static readonly Dictionary<string, BuildingCategory> ByLanduse = new Dictionary<string, BuildingCategory>
        {
            ["residential"] = BuildingCategory.Residential, ["commercial"] = BuildingCategory.Commercial, ["retail"] = BuildingCategory.Commercial,
            ["industrial"] = BuildingCategory.Industrial, ["education"] = BuildingCategory.Institutional, ["religious"] = BuildingCategory.Religious,
            ["military"] = BuildingCategory.Institutional, ["railway"] = BuildingCategory.Special,
        };

        /// <summary>Road classes whose frontage makes an untyped building a shop-house.</summary>
        static readonly HashSet<string> MainRoads = new HashSet<string>
        {
            "motorway", "trunk", "primary", "secondary", "tertiary", "motorway_link", "trunk_link", "primary_link", "secondary_link", "tertiary_link",
        };

        public static FacadeFamily FamilyOf(BuildingCategory c) => c switch
        {
            BuildingCategory.Residential => FacadeFamily.WalkUp,
            BuildingCategory.MixedUse => FacadeFamily.ShopHouse,
            BuildingCategory.Commercial => FacadeFamily.Commercial,
            BuildingCategory.Industrial => FacadeFamily.Industrial,
            BuildingCategory.Institutional => FacadeFamily.Institutional,
            BuildingCategory.Other => FacadeFamily.WalkUp,
            _ => FacadeFamily.None,
        };

        /// <summary>Bay width in metres: each wall is divided into whole bays about this wide.</summary>
        public static double BayWidth(FacadeFamily f) => f switch
        {
            FacadeFamily.Commercial => 3.6,
            FacadeFamily.Industrial => 6.0,
            FacadeFamily.Institutional => 3.6,
            _ => 3.0,
        };

        /// <summary>Does the piece have a corner on its chunk's border (cut by the grid, K-16)?</summary>
        public static bool OnChunkBorder(Building b)
        {
            var o = b.Outer;
            for (int i = 0; i < o.Length; i += 2)
                if (o[i] <= 0 || o[i] >= DadcChunk.ChunkCm || o[i + 1] <= 0 || o[i + 1] >= DadcChunk.ChunkCm) return true;
            return false;
        }

        /// <summary>Every building of the chunk, into cats and srcs (by index; buildings without an outline are left as they are),
        /// with the parts of one building agreeing (see the file header). near is Reset for c here.</summary>
        public static void ClassifyChunk(DadcChunk c, Near near, BuildingCategory[] cats, ClassSource[] srcs)
        {
            near.Reset(c);
            int n = c.Buildings?.Length ?? 0;
            var first = near.Ids;
            var areas = near.Areas;
            first.Clear();
            areas.Clear();
            bool several = false;
            for (int i = 0; i < n; i++)
            {
                var b = c.Buildings[i];
                double a = b.Outer == null || b.Outer.Length < 6 ? 0 : Math.Abs(SignedArea(b.Outer)) / 1e4;
                areas.Add(a);
                if (b.Outer == null || b.Outer.Length < 6) continue;
                if (first.TryGetValue(b.Id, out int f)) { several = true; near.Totals[f] += a; }
                else { first[b.Id] = i; near.Totals[i] = a; }
            }
            for (int i = 0; i < n; i++)
            {
                var b = c.Buildings[i];
                if (b.Outer == null || b.Outer.Length < 6) continue;
                (cats[i], srcs[i]) = Classify(c, b, near, near.Totals[first[b.Id]]);
            }
            if (!several) return;
            // Parts of one building: split if any is, else the largest part's answer.
            for (int i = 0; i < n; i++)
            {
                var b = c.Buildings[i];
                if (b.Outer == null || b.Outer.Length < 6 || first[b.Id] != i) continue;
                int best = i;
                bool split = false;
                for (int j = i; j < n; j++)
                {
                    var o = c.Buildings[j];
                    if (o.Id != b.Id || o.Outer == null || o.Outer.Length < 6) continue;
                    split |= srcs[j] == ClassSource.RuleSplit;
                    if (areas[j] > areas[best]) best = j;
                }
                for (int j = i; j < n; j++)
                {
                    var o = c.Buildings[j];
                    if (o.Id != b.Id || o.Outer == null || o.Outer.Length < 6) continue;
                    if (split) (cats[j], srcs[j]) = (BuildingCategory.Residential, ClassSource.RuleSplit);
                    else (cats[j], srcs[j]) = (cats[best], srcs[best]);
                }
            }
        }

        /// <summary>One piece's category and where it came from (see the file header), with area its building's footprint
        /// in m² (its own when NaN). near: the chunk's surroundings, Reset for c. ClassifyChunk makes the parts agree.</summary>
        public static (BuildingCategory category, ClassSource source) Classify(DadcChunk c, Building b, Near near, double area = double.NaN)
        {
            string t = b.Type;
            if (!string.IsNullOrEmpty(t) && t != "yes")
                return (ByType.TryGetValue(t, out var k) ? k : BuildingCategory.Other, ClassSource.OsmType);
            if (OnChunkBorder(b)) return (BuildingCategory.Residential, ClassSource.RuleSplit);
            (double cx, double cz) = Centre(b.Outer);
            if (near.LanduseAt(cx, cz, out var l)) return (l, ClassSource.Landuse);
            if (double.IsNaN(area)) area = Math.Abs(SignedArea(b.Outer)) / 1e4;
            if (area < TinyArea) return (BuildingCategory.Informal, ClassSource.RuleTiny);
            if (area >= LargeArea)
                return string.IsNullOrEmpty(b.Name) ? (BuildingCategory.Commercial, ClassSource.RuleLarge) : (BuildingCategory.Special, ClassSource.RuleNamedLarge);
            for (int i = 0; i < b.Outer.Length; i += 2)
                if (near.Gap(b.Outer[i] / 100.0, b.Outer[i + 1] / 100.0, Near.Kind.Main) <= ChunkBuildings.ShopFacing) return (BuildingCategory.MixedUse, ClassSource.RuleMainRoad);
            return (BuildingCategory.Residential, ClassSource.RuleDefault);
        }

        static (double, double) Centre(int[] o)
        {
            double x = 0, z = 0;
            int n = o.Length / 2;
            for (int i = 0; i < n; i++) { x += o[2 * i]; z += o[2 * i + 1]; }
            return (x / n, z / n);
        }

        static double SignedArea(int[] o)
        {
            double s = 0;
            int n = o.Length / 2;
            for (int i = 0, j = n - 1; i < n; j = i, i++) s += (double)o[2 * j] * o[2 * i + 1] - (double)o[2 * i] * o[2 * j + 1];
            return s / 2;
        }

        internal static bool Inside(int[] o, double x, double z)
        {
            bool inside = false;
            int n = o.Length / 2;
            for (int i = 0, j = n - 1; i < n; j = i, i++)
            {
                double xi = o[2 * i], zi = o[2 * i + 1], xj = o[2 * j], zj = o[2 * j + 1];
                if ((zi > z) != (zj > z) && x < (xj - xi) * (z - zi) / (zj - zi) + xi) inside = !inside;
            }
            return inside;
        }

        /// <summary>A chunk's at-grade vehicle carriageways, bucketed on a 25 m grid, and its landuse polygons' boxes:
        /// what the rules ask about a building's surroundings. Reset once per chunk; reused from chunk to chunk.</summary>
        public sealed class Near
        {
            const double Cell = 25, Reach = 12;
            // Per segment: ax, az, bx, bz, half width, kind (Kind).
            readonly List<double> segs = new List<double>(4096);
            readonly Dictionary<long, List<int>> grid = new Dictionary<long, List<int>>();
            readonly Stack<List<int>> spare = new Stack<List<int>>();
            // Per landuse polygon with a known type: min x, min z, max x, max z in cm.
            readonly List<int> boxes = new List<int>(64);
            readonly List<Landuse> landuse = new List<Landuse>(16);
            // ClassifyChunk's per-chunk state: each id's first piece, piece areas, and per first piece the building's total.
            internal readonly Dictionary<long, int> Ids = new Dictionary<long, int>(1024);
            internal readonly List<double> Areas = new List<double>(1024);
            internal double[] Totals = new double[1024];

            /// <summary>Service roads, other vehicle roads, main roads (tertiary and up).</summary>
            public enum Kind { Service, Road, Main }

            public void Reset(DadcChunk c)
            {
                foreach (var l in grid.Values) { l.Clear(); spare.Push(l); }
                grid.Clear();
                segs.Clear();
                boxes.Clear();
                landuse.Clear();
                int n = c.Buildings?.Length ?? 0;
                if (Totals.Length < n) Totals = new double[Math.Max(n, 2 * Totals.Length)];
                if (c.Landuse != null)
                    foreach (var lu in c.Landuse)
                    {
                        if (lu.Outer == null || lu.Outer.Length < 6 || lu.Type == null || !ByLanduse.ContainsKey(lu.Type)) continue;
                        int x0 = int.MaxValue, z0 = int.MaxValue, x1 = int.MinValue, z1 = int.MinValue;
                        for (int i = 0; i < lu.Outer.Length; i += 2)
                        {
                            x0 = Math.Min(x0, lu.Outer[i]); x1 = Math.Max(x1, lu.Outer[i]);
                            z0 = Math.Min(z0, lu.Outer[i + 1]); z1 = Math.Max(z1, lu.Outer[i + 1]);
                        }
                        landuse.Add(lu);
                        boxes.Add(x0); boxes.Add(z0); boxes.Add(x1); boxes.Add(z1);
                    }
                if (c.Roads == null) return;
                foreach (var r in c.Roads)
                {
                    if (r.Pts == null || r.Bridge || r.YCm >= 100 || r.Cls == null || RoadPalette.IsFoot(r.Cls) || r.Cls == "construction") continue;
                    double half = r.WidthCm / 200.0, kind = (double)(MainRoads.Contains(r.Cls) ? Kind.Main : r.Cls == "service" ? Kind.Service : Kind.Road);
                    for (int i = 0; i + 3 < r.Pts.Length; i += 2)
                    {
                        double ax = r.Pts[i] / 100.0, az = r.Pts[i + 1] / 100.0, bx = r.Pts[i + 2] / 100.0, bz = r.Pts[i + 3] / 100.0;
                        int s = segs.Count / 6;
                        segs.Add(ax); segs.Add(az); segs.Add(bx); segs.Add(bz); segs.Add(half); segs.Add(kind);
                        double reach = half + Reach;
                        int x0 = (int)Math.Floor((Math.Min(ax, bx) - reach) / Cell), x1 = (int)Math.Floor((Math.Max(ax, bx) + reach) / Cell);
                        int z0 = (int)Math.Floor((Math.Min(az, bz) - reach) / Cell), z1 = (int)Math.Floor((Math.Max(az, bz) + reach) / Cell);
                        for (int gx = x0; gx <= x1; gx++)
                            for (int gz = z0; gz <= z1; gz++)
                            {
                                long key = ((long)gx << 32) ^ (uint)gz;
                                if (!grid.TryGetValue(key, out var l)) grid[key] = l = spare.Count > 0 ? spare.Pop() : new List<int>(8);
                                l.Add(s);
                            }
                    }
                }
            }

            /// <summary>The landuse category at (x, z), cm in the chunk, if a typed landuse polygon holds it.</summary>
            public bool LanduseAt(double x, double z, out BuildingCategory category)
            {
                for (int i = 0; i < landuse.Count; i++)
                {
                    int k = 4 * i;
                    if (x < boxes[k] || z < boxes[k + 1] || x > boxes[k + 2] || z > boxes[k + 3] || !Inside(landuse[i].Outer, x, z)) continue;
                    category = ByLanduse[landuse[i].Type];
                    return true;
                }
                category = default;
                return false;
            }

            /// <summary>Distance from (x, z), metres in the chunk, to the nearest edge of a carriageway of at least kind min
            /// (0 inside one); MaxValue past Reach.</summary>
            public double Gap(double x, double z, Kind min)
            {
                if (!grid.TryGetValue(((long)(int)Math.Floor(x / Cell) << 32) ^ (uint)(int)Math.Floor(z / Cell), out var l)) return double.MaxValue;
                double best = double.MaxValue;
                foreach (int s in l)
                {
                    int k = 6 * s;
                    if (segs[k + 5] < (double)min) continue;
                    double ax = segs[k], az = segs[k + 1], dx = segs[k + 2] - ax, dz = segs[k + 3] - az, len = dx * dx + dz * dz;
                    double t = len > 0 ? Math.Max(0, Math.Min(1, ((x - ax) * dx + (z - az) * dz) / len)) : 0;
                    double px = ax + t * dx - x, pz = az + t * dz - z;
                    best = Math.Min(best, Math.Max(0, Math.Sqrt(px * px + pz * pz) - segs[k + 4]));
                }
                return best;
            }

            /// <summary>Is a carriageway (not a service road) straight out from the wall at (x, z), within ChunkBuildings.ShopFacing,
            /// along the outward normal (nx, nz)? Facades.FacesRoad's question, on the grid.</summary>
            public bool Faces(double x, double z, double nx, double nz)
            {
                for (double d = 1; d <= ChunkBuildings.ShopFacing; d += 2)
                    if (Gap(x + nx * d, z + nz * d, Kind.Road) <= 0) return true;
                return false;
            }
        }
    }
}
