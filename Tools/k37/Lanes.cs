// City-wide count for K-37 proposal P-1 (narrowing only): the roads RoadWidths gives a lanes-based
// width (estimated, one-way, lanes-tagged, and narrower that way) against the class width the
// exporter gave them, and the ones it leaves at the class width because their lanes would widen them.
using System; using System.IO; using System.Linq; using System.Collections.Generic; using Dad.World;
static class Lanes { static void Main(string[] a) {
  var hit = new Dictionary<long,(string cls,int cw,int w,int lanes)>(); var kept = new Dictionary<long,(string cls,int cw,int lanes)>();
  int est = 0, files = 0;
  var all = new HashSet<long>();
  foreach (var f in Directory.GetFiles(a[0], "c_*.dadc")) { files++;
    var c = Dadc.Load(File.ReadAllBytes(f));
    var oneway = new HashSet<long>((c.Edges ?? new GraphEdge[0]).Where(e => e.Oneway != 0).Select(e => e.Way));
    foreach (var r in c.Roads ?? new Road[0]) { if (!all.Add(r.Id)) continue;
      if (r.WidthEstimated) est++;
      if (r.WidthFromLanes) hit[r.Id] = (r.Cls, r.ClassWidthCm, r.WidthCm, r.Lanes.Value);
      else if (r.WidthEstimated && r.Lanes is int l && l > 0 && oneway.Contains(r.Id)) kept[r.Id] = (r.Cls, r.ClassWidthCm, l);
    } }
  Console.WriteLine($"chunks {files}; roads {all.Count}; width estimated {est}; lanes-based width (estimated, one-way, lanes tagged, narrower) {hit.Count}; " +
                    $"kept at the class width: {kept.Count} (lanes would widen {kept.Values.Count(v => v.lanes * RoadWidths.LaneWidthCm > v.cw)}, equal {kept.Values.Count(v => v.lanes * RoadWidths.LaneWidthCm == v.cw)})");
  foreach (var g in hit.Values.GroupBy(v => v.cls).OrderByDescending(g => g.Count()))
    Console.WriteLine($"  narrower {g.Key}: {g.Count()} ways; class width {g.First().cw/100.0} m; lanes {string.Join(",", g.GroupBy(v=>v.lanes).OrderBy(x=>x.Key).Select(x=>$"{x.Key}:{x.Count()}"))}");
  foreach (var g in kept.Values.Where(v => v.lanes * RoadWidths.LaneWidthCm > v.cw).GroupBy(v => v.cls).OrderByDescending(g => g.Count()))
    Console.WriteLine($"  not widened {g.Key}: {g.Count()} ways; class width {g.First().cw/100.0} m; lanes {string.Join(",", g.GroupBy(v=>v.lanes).OrderBy(x=>x.Key).Select(x=>$"{x.Key}:{x.Count()}"))}");
} }
