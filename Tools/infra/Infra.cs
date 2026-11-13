// Infrastructure survey, outside Unity (Tools/infra/run.sh): runs Dad.EditorTools.InfraSurvey on the slice (or
// the chunks given) with the ring around it as context, and writes the register:
//   supports.csv  one row per support (piers, footbridge columns, stair feet and tops), audit section G fields
//   decks.csv     one row per elevated deck (flyover and footbridge ways)
//   findings.csv  every finding, violation or note, with the known list's reference where it has one
//   summary.txt   counts by structure and check, and what is new or gone against the known list
// Read-only: it changes nothing in the project.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Dad.EditorTools;

static class Infra
{
    static string Q(object v)
    {
        string s = v switch
        {
            null => "",
            double d when double.IsNaN(d) || double.IsInfinity(d) => "",
            double d => d.ToString("0.00", CultureInfo.InvariantCulture),
            bool b => b ? "yes" : "no",
            _ => Convert.ToString(v, CultureInfo.InvariantCulture),
        };
        return s.IndexOfAny(new[] { ',', '"', '\n' }) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
    }

    static string Row(params object[] v) => string.Join(",", v.Select(Q));

    static int Main(string[] a)
    {
        string dir = a[0], outDir = a[1], known = a.Length > 2 ? a[2] : null;
        var ids = a.Length > 3 ? a[3].Split(',') : InfraSurvey.Slice;
        Directory.CreateDirectory(outDir);
        var t0 = DateTime.UtcNow;
        var res = InfraSurvey.Run(dir, ids, InfraSurvey.Ring(ids));
        double secs = (DateTime.UtcNow - t0).TotalSeconds;
        var list = known != null && File.Exists(known) ? InfraKnown.Read(known) : new Dictionary<string, InfraKnown.Entry>();

        var sb = new StringBuilder();
        sb.AppendLine(Row("id", "structure", "kind", "chunk", "east_m", "north_m", "chunk_x_m", "chunk_z_m", "segment_way", "built", "builder_note",
            "foot_m", "top_m", "deck_underside_m", "nearest_carriageway_way", "carriageway_class", "carriageway_width", "carriageway_clearance_m",
            "in_junction_pad", "in_crossing_keepout", "on_bus_stop", "on_sidewalk", "crossing_distance_m", "nearest_building", "building_base_m",
            "building_clearance_m", "through_elevated_way", "span_prev_m", "span_next_m", "next_support", "nearest_same_structure_m",
            "status", "findings", "source", "confidence"));
        var byId = res.Findings.GroupBy(f => f.Id).ToDictionary(g => g.Key, g => g.ToList());
        foreach (var s in res.Supports)
        {
            byId.TryGetValue(s.Id, out var fs);
            var viol = (fs ?? new List<InfraSurvey.Finding>()).Where(f => !f.Note).ToList();
            string status = viol.Count == 0 ? (s.Built ? "clear" : "not built") : viol.All(f => list.ContainsKey(f.Key)) ? "known violation" : "NEW violation";
            sb.AppendLine(Row(s.Id, s.Structure, s.Kind, s.Chunk, s.East, s.North, s.X, s.Z, s.Way, s.Built, s.BuilderWhy,
                s.Built ? s.Foot : double.NaN, s.Built ? s.Top : double.NaN, s.ExpectedTop, s.RoadWay == 0 ? null : (object)s.RoadWay, s.RoadClass,
                s.RoadWay == 0 ? null : s.RoadMapped ? "mapped" : "estimated", s.RoadClear, s.InPad, s.InCrossing, s.OnBusStop, s.OnSidewalk,
                s.CrossingDistance, s.BuildingId == 0 ? null : (object)s.BuildingId, s.BuildingId == 0 ? double.NaN : s.BuildingBase, s.BuildingClear,
                s.ThroughDeck == 0 ? null : (object)s.ThroughDeck, s.SpanPrev, s.SpanNext, s.Next, s.NearestOther, status,
                string.Join(" | ", (fs ?? new List<InfraSurvey.Finding>()).Select(f => (f.Note ? "note " : "") + f.Check + ": " + f.Detail)), s.Source, s.Confidence));
        }
        File.WriteAllText(Path.Combine(outDir, "supports.csv"), sb.ToString());

        sb.Clear();
        sb.AppendLine(Row("id", "structure", "way", "class", "name", "deck_m", "length_in_surveyed_chunks_m", "chunks", "supports_built", "findings"));
        foreach (var d in res.Decks.OrderBy(d => d.Id, StringComparer.Ordinal))
        {
            byId.TryGetValue(d.Id, out var fs);
            sb.AppendLine(Row(d.Id, d.Structure, d.Way, d.Class, d.Name, d.Y, d.Length, d.Chunks, d.Supports,
                string.Join(" | ", (fs ?? new List<InfraSurvey.Finding>()).Select(f => f.Check + ": " + f.Detail))));
        }
        File.WriteAllText(Path.Combine(outDir, "decks.csv"), sb.ToString());

        sb.Clear();
        sb.AppendLine(Row("key", "id", "check", "subject", "kind", "known", "reference", "detail"));
        foreach (var f in res.Findings)
        {
            list.TryGetValue(f.Key, out var k);
            sb.AppendLine(Row(f.Key, f.Id, f.Check, f.Subject, f.Note ? "note" : "violation", f.Note ? "" : k != null ? "yes" : "NEW", k?.Reference, f.Detail));
        }
        File.WriteAllText(Path.Combine(outDir, "findings.csv"), sb.ToString());

        sb.Clear();
        var v = res.Findings.Where(f => !f.Note).ToList();
        sb.AppendLine($"Infrastructure survey of {res.Chunks.Count} chunks ({string.Join(" ", res.Chunks)}), with the ring around them as context; {secs:0.0} s.");
        sb.AppendLine($"Supports: {res.Supports.Count}. By structure and state:");
        foreach (var g in res.Supports.GroupBy(s => (s.Structure, s.Kind, s.Built)).OrderBy(g => g.Key.ToString()))
            sb.AppendLine($"  {g.Key.Structure} {g.Key.Kind} {(g.Key.Built ? "built" : "not built")}: {g.Count()}, clear {g.Count(s => !byId.ContainsKey(s.Id) || byId[s.Id].All(f => f.Note))}");
        sb.AppendLine($"Decks: {res.Decks.Count} ({res.Decks.Count(d => d.Structure == InfraSurvey.Structure.Flyover)} flyover ways, {res.Decks.Count(d => d.Structure == InfraSurvey.Structure.Footbridge)} footbridge ways).");
        sb.AppendLine($"Findings: {v.Count} violations, {res.Findings.Count - v.Count} notes. Violations by check:");
        foreach (var g in v.GroupBy(f => f.Check).OrderBy(g => g.Key, StringComparer.Ordinal))
            sb.AppendLine($"  {g.Key}: {g.Count()} ({g.Select(f => f.Id).Distinct().Count()} items)");
        sb.AppendLine($"Builder agreement: of {res.BuilderOnRoad} piers the builder marks 'on a carriageway', the survey finds {res.BuilderOnRoadAgreed} on a carriageway or junction pad.");
        // K-37 P-3: piers in buildings, by the builder's centre test and by the survey's disc (radius 0.9 m) and the building's base.
        var inB = res.Supports.Where(s => s.Structure == InfraSurvey.Structure.Viaduct && s.Built && !s.Context && s.BuildingClear < 0).ToList();
        sb.AppendLine($"Viaduct piers in buildings: the builder counts {res.BuilderInBuilding} in a building on the ground and {res.BuilderUnderRaised} under a raised building (a station body); "
            + $"the survey finds {inB.Count(s => s.BuildingBase < InfraSurvey.GroundBase)} reaching into a building on the ground, {inB.Count(s => s.BuildingBase >= InfraSurvey.GroundBase && s.BuildingBase < s.Top)} rising through a raised building "
            + $"and {inB.Count(s => s.BuildingBase >= InfraSurvey.GroundBase && s.BuildingBase >= s.Top)} standing clear under one.");
        // K-37 P-5a: the continuous median under the viaduct, as the builder counts it.
        sb.AppendLine($"Continuous median (P-5a): {res.RunPieces} pieces, {res.RunLength:F0} m, with {res.BuilderOnRun} viaduct piers on it.");
        var (fresh, gone) = InfraKnown.Compare(res, list);
        sb.AppendLine($"Against the known list ({list.Count} entries): {fresh.Count} new, {gone.Count} gone.");
        foreach (var f in fresh) sb.AppendLine($"  NEW {f.Key}: {f.Detail}");
        foreach (var k in gone) sb.AppendLine($"  GONE {k}");
        File.WriteAllText(Path.Combine(outDir, "summary.txt"), sb.ToString());
        Console.Write(sb.ToString());
        return 0;
    }
}
