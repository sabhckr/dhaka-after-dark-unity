// K-37 pier survey: every viaduct pier in the 12 slice chunks, with its track, the roads and buildings
// around it, measured from the DADC data. Read-only: builds with keepMarks, changes nothing.
using System; using System.IO; using System.Linq; using System.Text; using System.Collections.Generic; using System.Globalization; using Dad.World;
static class K37 {
  const double R = ChunkRoads.PierRadius;
  static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
  static string Q(string s) => s == null ? "null" : "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
  // Distance from (x,z) to polyline pts (metres): distance, segment, t, signed side (+ left of travel), direction.
  static (double d, int seg, double t, double side, double ux, double uz) Near(double[] p, double x, double z) {
    double best = double.MaxValue; int bs = 0; double bt = 0, side = 0, bux = 0, buz = 0;
    for (int i = 0; i + 3 < p.Length; i += 2) {
      double ax = p[i], az = p[i+1], bx = p[i+2], bz = p[i+3], ex = bx-ax, ez = bz-az, ll = ex*ex+ez*ez; if (ll < 1e-12) continue;
      double t = Math.Clamp(((x-ax)*ex+(z-az)*ez)/ll, 0, 1), qx = ax+ex*t-x, qz = az+ez*t-z, d = Math.Sqrt(qx*qx+qz*qz);
      if (d < best) { best = d; bs = i/2; bt = t; double l = Math.Sqrt(ll); bux = ex/l; buz = ez/l; side = Math.Sign(ex*(z-az)-ez*(x-ax)); }
    }
    return (best, bs, bt, side, bux, buz);
  }
  static bool InPoly(int[] ring, double x, double z) {
    bool ins = false; int n = ring.Length/2;
    for (int i = 0, j = n-1; i < n; j = i++) { double xi = ring[2*i]/100.0, zi = ring[2*i+1]/100.0, xj = ring[2*j]/100.0, zj = ring[2*j+1]/100.0;
      if ((zi > z) != (zj > z) && x < (xj-xi)*(z-zi)/(zj-zi)+xi) ins = !ins; }
    return ins;
  }
  static double EdgeDist(int[] ring, double x, double z) {
    var p = new double[ring.Length + 2]; for (int i = 0; i < ring.Length; i++) p[i] = ring[i]/100.0; p[ring.Length] = p[0]; p[ring.Length+1] = p[1];
    return Near(p, x, z).d;
  }
  static double[] M(int[] cm) => cm.Select(v => v/100.0).ToArray();
  static void Main(string[] a) {
    var sb = new StringBuilder(); sb.Append("[\n"); bool first = true;
    var ids = a.Length > 2 ? a[2].Split(',') : new[]{6,7,8,9}.SelectMany(ix => new[]{-30,-29,-28}.Select(iy => $"c_{ix}_{iy}")).ToArray();
    foreach (var id in ids) {
      var c = Dadc.Load(File.ReadAllBytes(Path.Combine(a[0], id + ".dadc")));
      double ce = c.CornerEastCm/100.0, cn = c.CornerNorthCm/100.0;
      var r = ChunkRoads.Build(c, keepMarks: true);
      var oneway = new Dictionary<long,int>(); if (c.Edges != null) foreach (var e in c.Edges) oneway[e.Way] = e.Oneway;
      var rails = (c.Rails ?? new Rail[0]).Where(t => t.YCm >= ChunkRoads.ViaductMinY*100 && t.Pts != null && t.Pts.Length >= 4).ToList();
      foreach (var it in r.Street.Items.Where(i => i.Kind == ChunkStreet.Kind.Pier)) {
        double x = it.X, z = it.Z;
        var o = new StringBuilder();
        o.Append($"{{\"chunk\":\"{c.Id}\",\"cx\":{F(ce)},\"cz\":{F(cn)},\"x\":{F(x)},\"z\":{F(z)},\"e\":{F(ce+x)},\"n\":{F(cn+z)},\"skipped\":{(it.Skipped?"true":"false")},\"why\":{Q(it.Why)},\"bottom\":{F(it.Top)},");
        o.Append("\"tracks\":[");
        o.Append(string.Join(",", rails.Select(t => { var n = Near(M(t.Pts), x, z); return $"{{\"id\":{t.Id},\"y\":{F(t.YCm/100.0)},\"d\":{F(n.d)},\"seg\":{n.seg},\"side\":{F(n.side)},\"ux\":{F(n.ux)},\"uz\":{F(n.uz)},\"pts\":[{string.Join(",", M(t.Pts).Select(F))}]}}"; })));
        o.Append("],\"roads\":[");
        var rs = new List<string>();
        foreach (var rd in c.Roads ?? new Road[0]) {
          if (rd.Pts == null || rd.Pts.Length < 4) continue;
          var p = M(rd.Pts); var n = Near(p, x, z); double half = Math.Max(0.4, rd.WidthCm/200.0);
          if (n.d > half + 40) continue;
          oneway.TryGetValue(rd.Id, out int ow);
          rs.Add($"{{\"id\":{rd.Id},\"cls\":{Q(rd.Cls)},\"name\":{Q(rd.Name)},\"nameEn\":{Q(rd.NameEn)},\"width\":{F(rd.WidthCm/100.0)},\"est\":{(rd.WidthEstimated?"true":"false")},\"lanes\":{(rd.Lanes?.ToString() ?? "null")},\"oneway\":{ow},\"y\":{F(rd.YCm/100.0)},\"bridge\":{(rd.Bridge?"true":"false")},\"layer\":{(rd.Layer?.ToString() ?? "null")},\"foot\":{(RoadPalette.IsFoot(rd.Cls)?"true":"false")},\"d\":{F(n.d)},\"seg\":{n.seg},\"side\":{F(n.side)},\"ux\":{F(n.ux)},\"uz\":{F(n.uz)},\"pts\":[{string.Join(",", p.Select(F))}]}}");
        }
        o.Append(string.Join(",", rs));
        o.Append("],\"buildings\":[");
        var bs = new List<string>();
        foreach (var b in c.Buildings ?? new Building[0]) {
          if (b.Outer == null || b.Outer.Length < 6) continue;
          bool inside = InPoly(b.Outer, x, z) && !(b.Holes ?? new int[0][]).Any(h => InPoly(h, x, z));
          double ed = EdgeDist(b.Outer, x, z);
          if (!inside && ed > 40) continue;
          var pl = BuildingRules.Place(b);
          bs.Add($"{{\"id\":{b.Id},\"type\":{Q(b.Type)},\"name\":{Q(b.Name)},\"layer\":{(b.Layer?.ToString() ?? "null")},\"h\":{F(b.HeightCm/100.0)},\"hs\":{Q(b.HeightSource)},\"minh\":{(b.MinHeightCm.HasValue?F(b.MinHeightCm.Value/100.0):"null")},\"base\":{F(pl.Base)},\"top\":{F(pl.Top)},\"inside\":{(inside?"true":"false")},\"edge\":{F(ed)},\"outer\":[{string.Join(",", M(b.Outer).Select(F))}]}}");
        }
        o.Append(string.Join(",", bs));
        o.Append("]}");
        sb.Append(first ? "" : ",\n").Append(o); first = false;
      }
    }
    sb.Append("\n]\n");
    File.WriteAllText(a[1], sb.ToString());
    Console.WriteLine("ok");
  }
}
