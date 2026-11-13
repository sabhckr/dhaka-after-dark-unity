// The city as a map (2026-11-10, Docs/PRODUCTION/PLAYER_INTERFACE.md): one picture, drawn once at start from the chunk files, for the radar and the map screen. Roads at their
// own widths (main roads lighter), buildings, parks and grass, canals, plazas, rails, and the sea and rivers from the harbour's shore distances. It also answers which district
// and which street a place is in (districts from places.json, by the nearest; streets from the roads' names), and finds a route along the road graph to a waypoint.
// The picture is drawn on a worker thread; only the upload is on the main thread, behind the start screen.
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Dad.World;
using UnityEngine;

namespace Dad.Core
{
    public sealed class CityMap
    {
        /// <summary>Pixels a metre: 0.8, so the 5 by 3.5 km city is 4000 by 2800.</summary>
        public const float PerMetre = 0.8f;
        const double CellSize = 50; // the street index's cells, metres

        public static CityMap Active { get; private set; }

        public Texture2D Picture { get; private set; }
        public double West { get; private set; }
        public double South { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }
        public double WidthMetres => Width / PerMetre;
        public double HeightMetres => Height / PerMetre;
        /// <summary>The named districts, where their names sit.</summary>
        public readonly List<(string name, double east, double north)> Areas = new List<(string, double, double)>();

        Color32[] pixels;
        readonly Dictionary<long, List<Piece>> streets = new Dictionary<long, List<Piece>>();
        // the road graph: a node per joined point, edges with their shape
        readonly Dictionary<long, int> nodeAt = new Dictionary<long, int>();
        readonly List<(double e, double n)> nodes = new List<(double, double)>();
        readonly List<List<(int to, double length, double[] shape)>> links = new List<List<(int, double, double[])>>();

        sealed class Piece { public string Name; public double Ax, Az, Bx, Bz, Reach; }

        // Colours: the art direction's near-black, steel and silver; water a deep blue, green kept dark.
        static readonly Color32 Land = new Color32(21, 24, 29, 255), Park = new Color32(28, 43, 33, 255), Water = new Color32(14, 34, 52, 255),
            Plaza = new Color32(34, 39, 47, 255), Building = new Color32(40, 44, 52, 255), Rail = new Color32(70, 76, 86, 255),
            Edge = new Color32(10, 12, 15, 255), Minor = new Color32(92, 100, 111, 255), Major = new Color32(150, 158, 168, 255), Footway = new Color32(58, 64, 73, 255);

        /// <summary>Draws the map from the world's files, on a worker, then uploads it; sets Active. Null if there are no chunks.</summary>
        public static async Task<CityMap> Build(string worldDir)
        {
            var m = await Task.Run(() => Draw(worldDir));
            if (m == null) return null;
            m.Upload();
            Active = m;
            return m;
        }

        /// <summary>The same, all on the calling thread (tests).</summary>
        public static CityMap BuildNow(string worldDir)
        {
            var m = Draw(worldDir);
            if (m == null) return null;
            m.Upload();
            Active = m;
            return m;
        }

        void Upload()
        {
            Picture = new Texture2D(Width, Height, TextureFormat.RGBA32, true) { name = "City map", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
            Picture.SetPixelData(pixels, 0);
            pixels = null;
            Picture.Apply(true);
            Picture.Compress(false);
            Picture.Apply(false, true); // the CPU copy goes: only the GPU's is needed
        }

        static CityMap Draw(string worldDir)
        {
            var files = Directory.Exists(Path.Combine(worldDir, "chunks")) ? Directory.GetFiles(Path.Combine(worldDir, "chunks"), "*.dadc") : Array.Empty<string>();
            if (files.Length == 0) return null;
            var chunks = new List<DadcChunk>(files.Length);
            foreach (var f in files) chunks.Add(Dadc.Load(File.ReadAllBytes(f)));
            int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue, maxY = int.MinValue;
            foreach (var c in chunks) { minX = Math.Min(minX, c.Ix); maxX = Math.Max(maxX, c.Ix); minY = Math.Min(minY, c.Iy); maxY = Math.Max(maxY, c.Iy); }
            var m = new CityMap { West = minX * 500.0, South = -(maxY + 1) * 500.0 };
            m.Width = (int)Math.Round((maxX - minX + 1) * 500 * PerMetre);
            m.Height = (int)Math.Round((maxY - minY + 1) * 500 * PerMetre);
            m.pixels = new Color32[m.Width * m.Height];
            Array.Fill(m.pixels, Land);
            m.Sea(worldDir, chunks);
            foreach (var c in chunks)
            {
                double ce = c.CornerEastCm / 100.0, cn = c.CornerNorthCm / 100.0;
                if (c.Landuse != null) foreach (var l in c.Landuse) if (Green(l.Type)) m.Fill(ce, cn, l.Outer, l.Holes, Park);
                if (c.Parks != null) foreach (var p in c.Parks) m.Fill(ce, cn, p.Outer, p.Holes, Park);
                if (c.Water != null) foreach (var w in c.Water) m.Fill(ce, cn, w.Outer, w.Holes, Water);
                if (c.Canals != null) foreach (var k in c.Canals) m.Line(ce, cn, k.Pts, Math.Max(3, k.WidthCm / 100.0), Water);
                if (c.Plazas != null) foreach (var p in c.Plazas) m.Fill(ce, cn, p.Outer, p.Holes, Plaza);
                foreach (var b in c.Buildings) if (b.Outer != null) m.Fill(ce, cn, b.Outer, b.Holes, Building);
                if (c.Rails != null) foreach (var r in c.Rails) m.Line(ce, cn, r.Pts, 2.5, Rail);
            }
            // roads: every road's dark edge first, then the roads over them, so crossings stay clean
            foreach (var pass in new[] { 0, 1 })
                foreach (var c in chunks)
                {
                    double ce = c.CornerEastCm / 100.0, cn = c.CornerNorthCm / 100.0;
                    foreach (var r in c.Roads)
                    {
                        bool foot = r.Cls == "footway" || r.Cls == "path" || r.Cls == "pedestrian" || r.Cls == "steps" || r.Cls == "cycleway";
                        double w = foot ? 2.5 : Math.Max(4, (r.WidthCm > 0 ? r.WidthCm : 600) / 100.0);
                        if (pass == 0) { if (!foot) m.Line(ce, cn, r.Pts, w + 2.5, Edge); }
                        else m.Line(ce, cn, r.Pts, w, foot ? Footway : IsMajor(r.Cls) ? Major : Minor);
                        if (pass == 1 && !foot && !string.IsNullOrEmpty(r.NameEn ?? r.Name)) m.Index(ce, cn, r.Pts, r.NameEn ?? r.Name, w / 2 + 6);
                    }
                }
            foreach (var c in chunks) m.Graph(c);
            m.Places(worldDir);
            return m;
        }

        static bool Green(string t) => t == "grass" || t == "park" || t == "forest" || t == "meadow" || t == "recreation_ground" || t == "cemetery" || t == "village_green" || t == "garden" || t == "wood";
        static bool IsMajor(string c) => c != null && (c.StartsWith("motorway") || c.StartsWith("trunk") || c.StartsWith("primary") || c.StartsWith("secondary"));

        // The sea and rivers: wherever the harbour's shore distance says water (World/water/<id>.bin, as WaterView reads it).
        void Sea(string worldDir, List<DadcChunk> chunks)
        {
            const int N = WaterView.Lattice;
            double step = 500.0 / (N - 1);
            foreach (var c in chunks)
            {
                var path = Path.Combine(worldDir, "water", c.Id + ".bin");
                if (!File.Exists(path)) continue;
                var b = File.ReadAllBytes(path);
                if (b.Length != N * N * 2) continue;
                double ce = c.CornerEastCm / 100.0, cn = c.CornerNorthCm / 100.0;
                int x0 = Px(ce), x1 = Px(ce + 500), y0 = Py(cn), y1 = Py(cn + 500);
                for (int y = Math.Max(0, y0); y < Math.Min(Height, y1); y++)
                    for (int x = Math.Max(0, x0); x < Math.Min(Width, x1); x++)
                    {
                        double u = ((x + 0.5) / PerMetre + West - ce) / step, w = ((y + 0.5) / PerMetre + South - cn) / step;
                        int i = Math.Clamp((int)u, 0, N - 2), j = Math.Clamp((int)w, 0, N - 2);
                        double fu = u - i, fw = w - j;
                        double At(int a, int d) => b[2 * (a + d * N)] / 4.0 - 32;
                        double d = (At(i, j) * (1 - fu) + At(i + 1, j) * fu) * (1 - fw) + (At(i, j + 1) * (1 - fu) + At(i + 1, j + 1) * fu) * fw;
                        if (d > 0) pixels[x + y * Width] = Water;
                    }
            }
        }

        int Px(double east) => (int)Math.Floor((east - West) * PerMetre);
        int Py(double north) => (int)Math.Floor((north - South) * PerMetre);

        /// <summary>Where a place is on the picture, in pixels from its south-west corner.</summary>
        public Vector2 ToPixel(double east, double north) => new Vector2((float)((east - West) * PerMetre), (float)((north - South) * PerMetre));

        /// <summary>The place at a point of the picture.</summary>
        public void ToWorld(Vector2 pixel, out double east, out double north) { east = West + pixel.x / PerMetre; north = South + pixel.y / PerMetre; }

        // A filled polygon, outer ring and holes, by scanlines (even-odd).
        void Fill(double ce, double cn, int[] outer, int[][] holes, Color32 colour)
        {
            if (outer == null || outer.Length < 6) return;
            var rings = new List<double[]> { Ring(ce, cn, outer) };
            if (holes != null) foreach (var h in holes) if (h != null && h.Length >= 6) rings.Add(Ring(ce, cn, h));
            double lo = double.MaxValue, hi = double.MinValue;
            foreach (var r in rings) for (int i = 1; i < r.Length; i += 2) { lo = Math.Min(lo, r[i]); hi = Math.Max(hi, r[i]); }
            var xs = new List<double>();
            for (int y = Math.Max(0, (int)Math.Floor(lo)); y <= Math.Min(Height - 1, (int)Math.Ceiling(hi)); y++)
            {
                double sy = y + 0.5;
                xs.Clear();
                foreach (var r in rings)
                    for (int i = 0, n = r.Length / 2; i < n; i++)
                    {
                        double ax = r[2 * i], ay = r[2 * i + 1], bx = r[2 * ((i + 1) % n)], by = r[2 * ((i + 1) % n) + 1];
                        if (ay <= sy && by > sy || by <= sy && ay > sy) xs.Add(ax + (sy - ay) / (by - ay) * (bx - ax));
                    }
                xs.Sort();
                for (int k = 0; k + 1 < xs.Count; k += 2)
                {
                    int a = Math.Max(0, (int)Math.Ceiling(xs[k] - 0.5)), b = Math.Min(Width - 1, (int)Math.Floor(xs[k + 1] - 0.5));
                    for (int x = a; x <= b; x++) pixels[x + y * Width] = colour;
                }
            }
        }

        double[] Ring(double ce, double cn, int[] pts)
        {
            var r = new double[pts.Length];
            for (int i = 0; i < pts.Length; i += 2) { r[i] = (ce + pts[i] / 100.0 - West) * PerMetre; r[i + 1] = (cn + pts[i + 1] / 100.0 - South) * PerMetre; }
            return r;
        }

        // A line of a width in metres: each piece a filled quad, each point a disc, so bends are round.
        void Line(double ce, double cn, int[] pts, double width, Color32 colour)
        {
            if (pts == null || pts.Length < 4) return;
            var r = Ring(ce, cn, pts);
            double half = width * PerMetre / 2;
            for (int i = 0; i + 3 < r.Length; i += 2)
            {
                double ax = r[i], ay = r[i + 1], bx = r[i + 2], by = r[i + 3], dx = bx - ax, dy = by - ay, l = Math.Sqrt(dx * dx + dy * dy);
                if (l < 1e-6) continue;
                double nx = -dy / l * half, ny = dx / l * half;
                Quad(new[] { ax + nx, ay + ny, bx + nx, by + ny, bx - nx, by - ny, ax - nx, ay - ny }, colour);
            }
            for (int i = 0; i < r.Length; i += 2) Disc(r[i], r[i + 1], half, colour);
        }

        void Quad(double[] q, Color32 colour)
        {
            double lo = Math.Min(Math.Min(q[1], q[3]), Math.Min(q[5], q[7])), hi = Math.Max(Math.Max(q[1], q[3]), Math.Max(q[5], q[7]));
            Span<double> xs = stackalloc double[4];
            for (int y = Math.Max(0, (int)Math.Floor(lo)); y <= Math.Min(Height - 1, (int)Math.Ceiling(hi)); y++)
            {
                double sy = y + 0.5;
                int n = 0;
                for (int i = 0; i < 4; i++)
                {
                    double ax = q[2 * i], ay = q[2 * i + 1], bx = q[2 * ((i + 1) % 4)], by = q[2 * ((i + 1) % 4) + 1];
                    if (ay <= sy && by > sy || by <= sy && ay > sy) xs[n++] = ax + (sy - ay) / (by - ay) * (bx - ax);
                }
                if (n < 2) continue;
                double a0 = Math.Min(xs[0], xs[1]), b0 = Math.Max(xs[0], xs[1]);
                for (int x = Math.Max(0, (int)Math.Ceiling(a0 - 0.5)); x <= Math.Min(Width - 1, (int)Math.Floor(b0 - 0.5)); x++) pixels[x + y * Width] = colour;
            }
        }

        void Disc(double cx, double cy, double r, Color32 colour)
        {
            for (int y = Math.Max(0, (int)(cy - r)); y <= Math.Min(Height - 1, (int)(cy + r)); y++)
            {
                double dy = y + 0.5 - cy, span = r * r - dy * dy;
                if (span < 0) continue;
                double s = Math.Sqrt(span);
                for (int x = Math.Max(0, (int)Math.Ceiling(cx - s - 0.5)); x <= Math.Min(Width - 1, (int)Math.Floor(cx + s - 0.5)); x++) pixels[x + y * Width] = colour;
            }
        }

        static long Key(int cx, int cz) => ((long)cx << 32) ^ (uint)cz;

        void Index(double ce, double cn, int[] pts, string name, double reach)
        {
            for (int i = 0; i + 3 < pts.Length; i += 2)
            {
                var s = new Piece { Name = name, Ax = ce + pts[i] / 100.0, Az = cn + pts[i + 1] / 100.0, Bx = ce + pts[i + 2] / 100.0, Bz = cn + pts[i + 3] / 100.0, Reach = reach };
                int x0 = (int)Math.Floor(Math.Min(s.Ax, s.Bx) / CellSize), x1 = (int)Math.Floor(Math.Max(s.Ax, s.Bx) / CellSize);
                int z0 = (int)Math.Floor(Math.Min(s.Az, s.Bz) / CellSize), z1 = (int)Math.Floor(Math.Max(s.Az, s.Bz) / CellSize);
                for (int x = x0; x <= x1; x++)
                    for (int z = z0; z <= z1; z++)
                    {
                        if (!streets.TryGetValue(Key(x, z), out var list)) streets[Key(x, z)] = list = new List<Piece>();
                        list.Add(s);
                    }
            }
        }

        /// <summary>The name of the street at a place, or null off any named street.</summary>
        public string Street(double east, double north)
        {
            string best = null;
            double most = double.MaxValue;
            int cx = (int)Math.Floor(east / CellSize), cz = (int)Math.Floor(north / CellSize);
            for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (!streets.TryGetValue(Key(cx + dx, cz + dz), out var list)) continue;
                    foreach (var s in list)
                    {
                        double d = Gap(east, north, s.Ax, s.Az, s.Bx, s.Bz);
                        if (d <= s.Reach && d < most) { most = d; best = s.Name; }
                    }
                }
            return best;
        }

        static double Gap(double px, double pz, double ax, double az, double bx, double bz)
        {
            double dx = bx - ax, dz = bz - az, l = dx * dx + dz * dz;
            double t = l > 0 ? Math.Clamp(((px - ax) * dx + (pz - az) * dz) / l, 0, 1) : 0;
            double x = ax + t * dx - px, z = az + t * dz - pz;
            return Math.Sqrt(x * x + z * z);
        }

        [Serializable] sealed class PlaceFile { public PlaceItem[] items; }
        [Serializable] sealed class PlaceItem { public string kind, name, nameEn; public double x, z; }

        void Places(string worldDir)
        {
            var path = Path.Combine(worldDir, "places.json");
            if (!File.Exists(path)) return;
            var f = JsonUtility.FromJson<PlaceFile>(File.ReadAllText(path));
            if (f?.items == null) return;
            foreach (var p in f.items) Areas.Add((string.IsNullOrEmpty(p.nameEn) ? p.name : p.nameEn, p.x, p.z));
        }

        /// <summary>The district a place is in: the nearest named one.</summary>
        public string Area(double east, double north)
        {
            string best = null;
            double most = double.MaxValue;
            foreach (var a in Areas)
            {
                double d = (a.east - east) * (a.east - east) + (a.north - north) * (a.north - north);
                if (d < most) { most = d; best = a.name; }
            }
            return best;
        }

        // The road graph of a chunk joined to the others by where its nodes are, to the centimetre.
        void Graph(DadcChunk c)
        {
            if (c.Nodes == null || c.Edges == null) return;
            long ce = c.CornerEastCm, cn = c.CornerNorthCm;
            var ids = new int[c.Nodes.Length];
            for (int i = 0; i < c.Nodes.Length; i++)
            {
                long e = ce + c.Nodes[i].East, n = cn + c.Nodes[i].North, k = e * 100_000_000L + n;
                if (!nodeAt.TryGetValue(k, out int id)) { id = nodes.Count; nodeAt[k] = id; nodes.Add((e / 100.0, n / 100.0)); links.Add(new List<(int, double, double[])>()); }
                ids[i] = id;
            }
            foreach (var g in c.Edges)
            {
                if (g.A < 0 || g.B < 0 || g.A >= ids.Length || g.B >= ids.Length) continue;
                if (g.Cls == "footway" || g.Cls == "path" || g.Cls == "steps" || g.Cls == "pedestrian" || g.Cls == "cycleway") continue;
                var pts = g.Pts != null && g.Pts.Length >= 4 ? g.Pts : new[] { c.Nodes[g.A].East, c.Nodes[g.A].North, c.Nodes[g.B].East, c.Nodes[g.B].North };
                var shape = new double[pts.Length];
                double length = 0;
                for (int i = 0; i < pts.Length; i += 2)
                {
                    shape[i] = ce / 100.0 + pts[i] / 100.0;
                    shape[i + 1] = cn / 100.0 + pts[i + 1] / 100.0;
                    if (i >= 2) length += Math.Sqrt((shape[i] - shape[i - 2]) * (shape[i] - shape[i - 2]) + (shape[i + 1] - shape[i - 1]) * (shape[i + 1] - shape[i - 1]));
                }
                int a = ids[g.A], b = ids[g.B];
                var back = new double[shape.Length];
                for (int i = 0; i < shape.Length; i += 2) { back[i] = shape[shape.Length - 2 - i]; back[i + 1] = shape[shape.Length - 1 - i]; }
                links[a].Add((b, length, shape));
                links[b].Add((a, length, back)); // both ways: a route for someone finding their way, not a lane plan
            }
        }

        public int Nodes => nodes.Count;

        /// <summary>A point in the middle of a stretch of road (40 m or longer, so not at a junction) whose middle lies between near and far metres from a place, chosen by
        /// rnd; false if the city has none there. Safe on a worker: the map does not change once built. For the delivery jobs (first playable, 2026-11-13).</summary>
        public bool RoadPoint(System.Random rnd, double east, double north, double near, double far, out double pointEast, out double pointNorth)
        {
            pointEast = pointNorth = 0;
            var found = new List<(double e, double n)>();
            for (int a = 0; a < links.Count; a++)
                foreach (var (to, length, shape) in links[a])
                {
                    if (to < a || length < 40 || shape.Length < 4) continue; // each road once, long enough to stop on
                    double half = length / 2, walked = 0, me = shape[0], mn = shape[1];
                    for (int i = 2; i < shape.Length; i += 2)
                    {
                        double dx = shape[i] - shape[i - 2], dz = shape[i + 1] - shape[i - 1], d = Math.Sqrt(dx * dx + dz * dz);
                        if (walked + d >= half && d > 0) { double t = (half - walked) / d; me = shape[i - 2] + dx * t; mn = shape[i - 1] + dz * t; break; }
                        walked += d;
                    }
                    double r = Math.Sqrt((me - east) * (me - east) + (mn - north) * (mn - north));
                    if (r >= near && r <= far) found.Add((me, mn));
                }
            if (found.Count == 0) return false;
            var pick = found[rnd.Next(found.Count)];
            pointEast = pick.e;
            pointNorth = pick.n;
            return true;
        }

        int Nearest(double east, double north)
        {
            int best = -1;
            double most = double.MaxValue;
            for (int i = 0; i < nodes.Count; i++)
            {
                if (links[i].Count == 0) continue;
                double d = (nodes[i].e - east) * (nodes[i].e - east) + (nodes[i].n - north) * (nodes[i].n - north);
                if (d < most) { most = d; best = i; }
            }
            return best;
        }

        /// <summary>The way along the roads from one place to another, as world points (east, north), the two ends included; empty if none is found.</summary>
        public List<Vector2> Route(double fromEast, double fromNorth, double toEast, double toNorth)
        {
            var way = new List<Vector2>();
            int start = Nearest(fromEast, fromNorth), goal = Nearest(toEast, toNorth);
            if (start < 0 || goal < 0) return way;
            var cost = new Dictionary<int, double> { [start] = 0 };
            var came = new Dictionary<int, (int from, double[] shape)>();
            var open = new SortedSet<(double f, int id)> { (0, start) };
            double H(int i) => Math.Sqrt((nodes[i].e - nodes[goal].e) * (nodes[i].e - nodes[goal].e) + (nodes[i].n - nodes[goal].n) * (nodes[i].n - nodes[goal].n));
            while (open.Count > 0)
            {
                var top = open.Min;
                open.Remove(top);
                if (top.id == goal) break;
                foreach (var (to, length, shape) in links[top.id])
                {
                    double g = cost[top.id] + length;
                    if (cost.TryGetValue(to, out double had) && had <= g) continue;
                    if (cost.ContainsKey(to)) open.Remove((had + H(to), to));
                    cost[to] = g;
                    came[to] = (top.id, shape);
                    open.Add((g + H(to), to));
                }
            }
            if (!cost.ContainsKey(goal)) return way;
            var pieces = new List<double[]>();
            for (int at = goal; at != start; at = came[at].from) pieces.Add(came[at].shape);
            way.Add(new Vector2((float)fromEast, (float)fromNorth));
            for (int p = pieces.Count - 1; p >= 0; p--)
                for (int i = 0; i < pieces[p].Length; i += 2) way.Add(new Vector2((float)pieces[p][i], (float)pieces[p][i + 1]));
            way.Add(new Vector2((float)toEast, (float)toNorth));
            return way;
        }
    }
}
