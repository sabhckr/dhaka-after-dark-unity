// City-wide building census for the master directive audit (2026-11-03), read-only: reads every chunk with
// Dadc.Load and writes one row per building id (pieces cut by the chunk grid are merged) with what the data
// says about it and what the builders draw for it today. Nothing in the game is changed.
//   census.exe <chunks dir> <out csv>
// Columns are described in Tools/census/README.md. The audit's classification (category, category_source) uses
// the building's own OSM type where it has one, else the landuse polygon its centre stands in (labelled
// "landuse"), else "unclassified". The d06a_* columns are what the game draws since D-06a (BuildingClass), per
// piece; pieces of one building that disagree are counted. Every chunk is also built (ChunkBuildings.Build) for
// the far tier's wall counts and the time classification takes. The d06b_* columns are the building's skin
// (BuildingSkin: wall material and its source, roof material, parapet, rooftop kit) from its largest piece; every
// chunk's roads are built too (ChunkRoads.Build) for the rooftop kit's counts.
using System; using System.IO; using System.Linq; using System.Collections.Generic; using System.Globalization; using Dad.World;

static class Census {
  // OSM building=* values to the directive's categories (Part 8.1). Anything not listed is "other: <value>".
  static readonly Dictionary<string,string> Cat = new Dictionary<string,string> {
    ["house"]="residential",["detached"]="residential",["residential"]="residential",["apartments"]="residential",["terrace"]="residential",
    ["semidetached_house"]="residential",["dormitory"]="residential",["bungalow"]="residential",["semi"]="residential",["flats"]="residential",
    ["hut"]="informal",["shed"]="informal",["cabin"]="informal",["slum"]="informal",["shack"]="informal",
    ["commercial"]="commercial",["retail"]="commercial",["office"]="commercial",["supermarket"]="commercial",["kiosk"]="commercial",
    ["hotel"]="commercial",["shop"]="commercial",["mall"]="commercial",["market"]="commercial",["restaurant"]="commercial",["bank"]="commercial",
    ["industrial"]="industrial",["warehouse"]="industrial",["factory"]="industrial",["manufacture"]="industrial",["garage"]="industrial",
    ["garages"]="industrial",["storage_tank"]="industrial",["workshop"]="industrial",["service"]="industrial",["hangar"]="industrial",
    ["school"]="institutional",["college"]="institutional",["university"]="institutional",["hospital"]="institutional",["clinic"]="institutional",
    ["government"]="institutional",["public"]="institutional",["civic"]="institutional",["kindergarten"]="institutional",
    ["fire_station"]="institutional",["police"]="institutional",["courthouse"]="institutional",["military"]="institutional",["barracks"]="institutional",
    ["mosque"]="religious",["temple"]="religious",["church"]="religious",["religious"]="religious",["chapel"]="religious",["shrine"]="religious",
    ["cathedral"]="religious",["pagoda"]="religious",["monastery"]="religious",
    ["train_station"]="special",["transportation"]="special",["stadium"]="special",["cinema"]="special",["sports_hall"]="special",
    ["grandstand"]="special",["parking"]="special",["roof"]="special",["bridge"]="special",["construction"]="special",["ruins"]="special",
    ["toilets"]="special",["gatehouse"]="special",["sports_centre"]="special",["pavilion"]="special",["tower"]="special",
  };
  // landuse=* to a category, used only for buildings with no type of their own.
  static readonly Dictionary<string,string> LandCat = new Dictionary<string,string> {
    ["residential"]="residential",["commercial"]="commercial",["retail"]="commercial",["industrial"]="industrial",
    ["education"]="institutional",["religious"]="religious",["military"]="institutional",["railway"]="special",
  };
  static readonly HashSet<string> NotForVehicles = new HashSet<string> { "footway","path","pedestrian","steps","cycleway","bridleway","corridor","platform" };

  sealed class B {
    public long Id; public string Chunks = ""; public double Area, Cx, Cz; public string Type, Name, Material, Roof, HeightSource, Land; public double Top, Base;
    public bool HasLevels, Street, Landmark, Facaded, Shops, Raised, LayerIgnored, Pieces2; public double RoadGap = double.MaxValue; public string LandmarkCat;
    public BuildingCategory D6Cat; public ClassSource D6Src; public bool D6Set, D6Disagree;
    public Skin Skin; public double SkinArea = -1;
  }

  static void Main(string[] a) {
    var all = new Dictionary<long,B>(); var cls = new Dictionary<string,int>();
    int files = 0, pieces = 0; var bad = new HashSet<long>();
    var near = new BuildingClass.Near(); var classify = new System.Diagnostics.Stopwatch(); var build = new System.Diagnostics.Stopwatch();
    long patternWalls = 0, shopWalls = 0, drawn = 0, sliceWalls = 0, sliceShops = 0;
    var roads = new System.Diagnostics.Stopwatch(); var kit = new long[2, 8]; long parapets = 0, parapetPieces = 0, parapetFolded = 0;
    long sliceStreetV = 0, sliceStreetI = 0; var roofV = new long[2]; var roofI = new long[2];
    foreach (var f in Directory.GetFiles(a[0], "c_*.dadc").OrderBy(x => x, StringComparer.Ordinal)) { files++;
      var c = Dadc.Load(File.ReadAllBytes(f));
      double ex = c.CornerEastCm / 100.0, nz = c.CornerNorthCm / 100.0;
      var segs = Segments(c, cls);
      bool inSlice = c.Ix >= 6 && c.Ix <= 9 && c.Iy >= -30 && c.Iy <= -28;
      build.Start(); var cb = ChunkBuildings.Build(c); build.Stop();
      patternWalls += cb.PatternWalls; shopWalls += cb.ShopWalls; drawn += cb.Drawn;
      if (inSlice) { sliceWalls += cb.PatternWalls; sliceShops += cb.ShopWalls; }
      parapets += cb.Parapets; parapetPieces += cb.ParapetPieces; parapetFolded += cb.ParapetFolded;
      roads.Start(); var st = ChunkRoads.Build(c).Street; roads.Stop();
      if (inSlice) st.CountNearKit(); // the near kit is built per cell when near (K-45), not by Build
      if (inSlice) { sliceStreetV += st.VertexCount; sliceStreetI += st.IndexCount; for (int k = 0; k < 2; k++) { roofV[k] += st.RoofVertices[k]; roofI[k] += st.RoofIndices[k]; } }
      for (int k = 0; k < (inSlice ? 2 : 1); k++) {
        kit[k, 0] += st.Roofs; kit[k, 1] += st.StairRooms; kit[k, 2] += st.LiftRooms; kit[k, 3] += st.Tanks;
        kit[k, 4] += st.Dishes; kit[k, 5] += st.AcUnits; kit[k, 6] += st.RoofNoRoom; kit[k, 7] += cb.Parapets; }
      int nb = c.Buildings?.Length ?? 0; var cats = new BuildingCategory[nb]; var srcs = new ClassSource[nb];
      classify.Start(); BuildingClass.ClassifyChunk(c, near, cats, srcs); classify.Stop();
      Count(c, "city"); if (c.Ix >= 6 && c.Ix <= 9 && c.Iy >= -30 && c.Iy <= -28) Count(c, "slice");
      for (int bi = 0; bi < nb; bi++) { var b = c.Buildings[bi];
        if (b.Outer == null || b.Outer.Length < 6) { bad.Add(b.Id); continue; } pieces++;
        if (!all.TryGetValue(b.Id, out var r)) all[b.Id] = r = new B { Id = b.Id, Type = b.Type, Name = b.Name, Material = b.Material, Roof = b.Roof, HeightSource = b.HeightSource, HasLevels = b.LevelsHalf is int lv && lv > 0 };
        else r.Pieces2 = true;
        var (d6c, d6s) = (cats[bi], srcs[bi]);
        if (!r.D6Set) { r.D6Cat = d6c; r.D6Src = d6s; r.D6Set = true; } else if (r.D6Cat != d6c) r.D6Disagree = true;
        r.Chunks += (r.Chunks.Length > 0 ? ";" : "") + c.Id;
        var pl = BuildingRules.Place(b); r.Top = Math.Max(r.Top, pl.Top); r.Base = pl.Base; r.Raised |= pl.Base > 0; r.LayerIgnored |= pl.LayerIgnored;
        double area = Math.Abs(Signed(b.Outer)) / 1e4; (double x, double z) = Centroid(b.Outer);
        if (area > r.SkinArea) { r.SkinArea = area; r.Skin = cb.Skins[bi]; }
        if (area > 0) { r.Cx = (r.Cx * r.Area + (ex + x) * area) / (r.Area + area); r.Cz = (r.Cz * r.Area + (nz + z) * area) / (r.Area + area); r.Area += area; }
        else if (r.Area == 0) { r.Cx = ex + x; r.Cz = nz + z; }
        // Street-facing: any outline corner within ChunkBuildings.ShopFacing of a vehicle carriageway's edge.
        for (int i = 0; i < b.Outer.Length; i += 2) r.RoadGap = Math.Min(r.RoadGap, Gap(segs, b.Outer[i] / 100.0, b.Outer[i + 1] / 100.0));
        r.Street = r.RoadGap <= ChunkBuildings.ShopFacing;
        if (r.Land == null && c.Landuse != null) foreach (var lu in c.Landuse) if (lu.Outer != null && Inside(lu.Outer, x * 100, z * 100)) { r.Land = lu.Type; break; }
        if (c.Landmarks != null) foreach (var lm in c.Landmarks) if (Inside(b.Outer, lm.East, lm.North)) { r.Landmark = true; r.LandmarkCat ??= lm.Category; }
        if (ChunkBuildings.Facaded(c, b, pl)) { r.Facaded = true; if (!r.Shops) r.Shops = ShopsOn(c, b); }
      }
    }
    var inv = CultureInfo.InvariantCulture;
    using (var w = new StreamWriter(a[1])) {
      w.WriteLine("id,chunks,east_m,north_m,osm_type,category,category_source,name,material_tag,roof_tag,height_m,height_source,levels_tagged,raised,footprint_m2,road_gap_m,street_facing,landmark,landmark_cat,facade_family,ground_floor_shops,pieces,d06a_category,d06a_source,d06a_family,d06b_wall,d06b_wall_source,d06b_roof,d06b_parapet,d06b_kit");
      foreach (var r in all.Values.OrderBy(v => v.Id)) {
        string cat, src;
        if (!string.IsNullOrEmpty(r.Type) && r.Type != "yes") { cat = Cat.TryGetValue(r.Type, out var k) ? k : "other"; src = "osm type"; }
        else if (r.Land != null && LandCat.TryGetValue(r.Land, out var k2)) { cat = k2; src = "landuse"; }
        else { cat = "unclassified"; src = "none"; }
        w.WriteLine(string.Join(",", r.Id, r.Chunks, r.Cx.ToString("F1", inv), r.Cz.ToString("F1", inv), Q(r.Type), cat, src, Q(r.Name), Q(r.Material), Q(r.Roof),
          r.Top.ToString("F1", inv), Q(r.HeightSource), r.HasLevels ? 1 : 0, r.Raised ? 1 : 0, r.Area.ToString("F1", inv),
          r.RoadGap == double.MaxValue ? "" : r.RoadGap.ToString("F1", inv), r.Street ? 1 : 0, r.Landmark ? 1 : 0, Q(r.LandmarkCat), r.Facaded ? 1 : 0, r.Shops ? 1 : 0, r.Chunks.Count(ch => ch == ';') + 1,
          r.D6Cat, r.D6Src, r.Facaded ? "M2" : BuildingClass.FamilyOf(r.D6Cat).ToString(),
          r.Skin.Wall, r.Skin.WallSource, r.Skin.Roof, r.Skin.Parapet ? 1 : 0, r.Skin.Kit ? 1 : 0));
      }
    }
    Console.WriteLine($"chunks {files}; building pieces {pieces}; building ids {all.Count}; ids with no usable outline in any chunk {bad.Count(id => !all.ContainsKey(id))}");
    var inv2 = CultureInfo.InvariantCulture;
    Console.WriteLine($"D-06a: building ids whose pieces disagree on the category {all.Values.Count(r => r.D6Disagree)} ({string.Join(" ", all.Values.Where(r => r.D6Disagree).Take(10).Select(r => r.Id))}); classification {classify.Elapsed.TotalMilliseconds.ToString("F0", inv2)} ms for {pieces} pieces ({(classify.Elapsed.TotalMilliseconds * 1000 / Math.Max(1, pieces)).ToString("F1", inv2)} us each, one thread)");
    Console.WriteLine($"D-06a: ChunkBuildings.Build, all chunks: {build.Elapsed.TotalSeconds.ToString("F1", inv2)} s, pieces drawn {drawn}; far-tier walls city {patternWalls}, with shops {shopWalls}; slice {sliceWalls}, with shops {sliceShops}");
    for (int k = 0; k < 2; k++)
      Console.WriteLine($"D-06b {(k == 0 ? "city" : "slice")}: parapet buildings {kit[k, 7]}; roofs furnished {kit[k, 0]}; stair rooms {kit[k, 1]}, lift rooms {kit[k, 2]}, tanks {kit[k, 3]}, dishes {kit[k, 4]}, condensers {kit[k, 5]}; items with no room {kit[k, 6]}");
    Console.WriteLine($"D-06b slice street meshes: {sliceStreetV} vertices, {sliceStreetI / 3} triangles; the rooftop kit far (in them) {roofV[1]} vertices, {roofI[1] / 3} triangles; near (built per cell when near, K-45) {roofV[0]} vertices, {roofI[0] / 3} triangles");
    Console.WriteLine($"D-06b: parapet pieces city {parapetPieces}, left out as folded {parapetFolded}; ChunkRoads.Build, all chunks: {roads.Elapsed.TotalSeconds.ToString("F1", inv2)} s");
    Console.WriteLine("road pieces by class: " + string.Join(" ", cls.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key}:{kv.Value}")));
    foreach (var scope in new[] { "city", "slice" })
      Console.WriteLine($"{scope}: " + string.Join("; ", Tally.Where(kv => kv.Key.StartsWith(scope + ":")).OrderBy(kv => kv.Key, StringComparer.Ordinal)
        .Select(kv => $"{kv.Key.Substring(scope.Length + 1)} {(kv.Key.EndsWith(" km") ? (kv.Value / 1000).ToString("F1", inv) : kv.Value.ToString("F0", inv))}")));
  }

  // Other things the coverage report needs denominators for, per scope. Ways cut by the chunk grid are counted once
  // (by id); lengths add every piece.
  static readonly Dictionary<string,double> Tally = new Dictionary<string,double>();
  static readonly HashSet<string> Seen = new HashSet<string>();
  static void Add(string k, double v = 1) => Tally[k] = (Tally.TryGetValue(k, out var x) ? x : 0) + v;
  static void Once(string scope, string what, long id) { if (Seen.Add(scope + what + id)) Add(scope + ":" + what); }
  static double Len(int[] p) { double s = 0; for (int i = 0; i + 3 < p.Length; i += 2) s += Math.Sqrt(Math.Pow(p[i + 2] - p[i], 2) + Math.Pow(p[i + 3] - p[i + 1], 2)) / 100; return s; }
  static void Count(DadcChunk c, string sc) {
    foreach (var r in c.Roads ?? new Road[0]) { if (r.Pts == null) continue;
      bool foot = NotForVehicles.Contains(r.Cls ?? "");
      string kind = r.Bridge ? (foot ? "bridge ways, foot" : "bridge ways, vehicle") : foot ? "ways, foot" : "ways, vehicle";
      Once(sc, kind, r.Id); Add(sc + ":" + kind + " km", Len(r.Pts));
      if (r.Bridge && !foot && (r.Layer ?? 0) >= 1) Once(sc, "bridge ways, vehicle, layer 1+", r.Id);
      if ((r.Tunnel ?? 0) > 0) Once(sc, "tunnel ways", r.Id);
      if (r.Lit is int l && l > 0) Once(sc, "ways tagged lit", r.Id);
      if (!string.IsNullOrEmpty(r.Sidewalk)) Once(sc, "ways with a sidewalk tag", r.Id); }
    foreach (var r in c.Rails ?? new Rail[0]) { string k = r.YCm >= 1000 ? "rails, elevated" : "rails, ground"; Once(sc, k, r.Id); Add(sc + ":" + k + " km", Len(r.Pts)); }
    Add(sc + ":bridge polygons", c.Bridges?.Length ?? 0);
    Add(sc + ":water polygons", c.Water?.Length ?? 0); Add(sc + ":park polygons", c.Parks?.Length ?? 0); Add(sc + ":canals", c.Canals?.Length ?? 0);
    foreach (var t in c.Trees ?? new Tree[0]) Add(sc + ":trees, " + (t.Kind ?? "?"));
    foreach (var pt in c.Points ?? new PointFeature[0]) Add(sc + ":points, " + pt.Kind);
    foreach (var pl in c.Power ?? new PowerLine[0]) Once(sc, "power lines, " + pl.Type, pl.Id);
    foreach (var lu in c.Landuse ?? new Landuse[0]) Add(sc + ":landuse, " + lu.Type);
    foreach (var lm in c.Landmarks ?? new Landmark[0]) Add(sc + ":landmarks");
  }

  static string Q(string s) => string.IsNullOrEmpty(s) ? "" : "\"" + s.Replace("\"", "\"\"") + "\"";

  // Does a facaded building get any ground-floor shop? Build it alone against the chunk's roads.
  static bool ShopsOn(DadcChunk c, Building b) {
    var one = new DadcChunk { Format = c.Format, SourceSchema = c.SourceSchema, Ix = c.Ix, Iy = c.Iy, Strings = c.Strings, Roads = c.Roads, Edges = c.Edges, Buildings = new[] { b }, WidthsResolved = true };
    return ChunkBuildings.Build(one).Shops > 0;
  }

  // Vehicle carriageway segments in metres, bucketed in 25 m cells: (ax, az, bx, bz, half width).
  const double Cell = 25;
  static Dictionary<long, List<double[]>> Segments(DadcChunk c, Dictionary<string,int> cls) {
    var grid = new Dictionary<long, List<double[]>>();
    foreach (var r in c.Roads ?? new Road[0]) {
      cls[r.Cls ?? ""] = cls.TryGetValue(r.Cls ?? "", out var n) ? n + 1 : 1;
      if (r.Pts == null || NotForVehicles.Contains(r.Cls ?? "") || r.Bridge || (r.Layer ?? 0) > 0 || (r.Tunnel ?? 0) > 0) continue;
      double half = r.WidthCm / 200.0, reach = half + ChunkBuildings.ShopFacing;
      for (int i = 0; i + 3 < r.Pts.Length; i += 2) {
        var s = new[] { r.Pts[i] / 100.0, r.Pts[i + 1] / 100.0, r.Pts[i + 2] / 100.0, r.Pts[i + 3] / 100.0, half };
        int x0 = (int)Math.Floor((Math.Min(s[0], s[2]) - reach) / Cell), x1 = (int)Math.Floor((Math.Max(s[0], s[2]) + reach) / Cell);
        int z0 = (int)Math.Floor((Math.Min(s[1], s[3]) - reach) / Cell), z1 = (int)Math.Floor((Math.Max(s[1], s[3]) + reach) / Cell);
        for (int gx = x0; gx <= x1; gx++) for (int gz = z0; gz <= z1; gz++) {
          long key = ((long)gx << 32) ^ (uint)gz; if (!grid.TryGetValue(key, out var l)) grid[key] = l = new List<double[]>(); l.Add(s); }
      }
    }
    return grid;
  }

  static double Gap(Dictionary<long, List<double[]>> grid, double x, double z) {
    long key = ((long)(int)Math.Floor(x / Cell) << 32) ^ (uint)(int)Math.Floor(z / Cell);
    if (!grid.TryGetValue(key, out var l)) return double.MaxValue;
    double best = double.MaxValue;
    foreach (var s in l) {
      double dx = s[2] - s[0], dz = s[3] - s[1], len = dx * dx + dz * dz, t = len > 0 ? ((x - s[0]) * dx + (z - s[1]) * dz) / len : 0;
      t = Math.Max(0, Math.Min(1, t)); double px = s[0] + t * dx - x, pz = s[1] + t * dz - z;
      best = Math.Min(best, Math.Max(0, Math.Sqrt(px * px + pz * pz) - s[4]));
    }
    return best;
  }

  static double Signed(int[] o) { double s = 0; int n = o.Length / 2; for (int i = 0; i < n; i++) { int j = (i + 1) % n; s += (double)o[2 * i] * o[2 * j + 1] - (double)o[2 * j] * o[2 * i + 1]; } return s / 2; }
  static (double, double) Centroid(int[] o) { double x = 0, z = 0; int n = o.Length / 2; for (int i = 0; i < n; i++) { x += o[2 * i]; z += o[2 * i + 1]; } return (x / n / 100, z / n / 100); }
  static bool Inside(int[] o, double x, double z) {
    bool inside = false; int n = o.Length / 2;
    for (int i = 0, j = n - 1; i < n; j = i++) {
      double xi = o[2 * i], zi = o[2 * i + 1], xj = o[2 * j], zj = o[2 * j + 1];
      if ((zi > z) != (zj > z) && x < (xj - xi) * (z - zi) / (zj - zi) + xi) inside = !inside; }
    return inside;
  }
}
