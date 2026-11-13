// Runs the Unity DADC reader (Assets/_Project/Runtime/World/Dadc.cs) outside the editor,
// on Unity's bundled Mono, against every chunk listed in an expectations TSV that
// Tools/dadc-check.sh extracts from world.json. Same checks as DadcReaderTests.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Dad.World;

static class Program
{
    static int Main(string[] args)
    {
        string chunksDir = args[0], tsv = args[1];
        var failures = new List<string>();
        var ms = new List<double>();
        int n = 0;
        using (var sha = SHA256.Create())
        {
            foreach (var line in File.ReadLines(tsv).Skip(1))
            {
                // id ix iy cellE cellN bytes sha256 buildings roads nodes edges positions sumE sumN sumInts
                var f = line.Split('\t');
                var data = File.ReadAllBytes(Path.Combine(chunksDir, f[0] + ".dadc"));
                var hash = BitConverter.ToString(sha.ComputeHash(data)).Replace("-", "").ToLowerInvariant();
                if (data.Length != int.Parse(f[5]) || hash != f[6]) failures.Add(f[0] + ": file differs from world.json");
                var sw = Stopwatch.StartNew();
                DadcChunk d;
                try { d = Dadc.Decode(data); }
                catch (DadcFormatException e) { failures.Add(f[0] + ": " + e.Message); continue; }
                ms.Add(sw.Elapsed.TotalMilliseconds);
                n++;
                if (d.Id != f[0] || d.Ix != int.Parse(f[1]) || d.Iy != int.Parse(f[2])) failures.Add(f[0] + ": ix/iy");
                if (d.CornerEastCm != long.Parse(f[3]) * DadcChunk.ChunkCm || d.CornerNorthCm != long.Parse(f[4]) * DadcChunk.ChunkCm) failures.Add(f[0] + ": cell");
                if (d.Buildings.Length != int.Parse(f[7]) || d.Roads.Length != int.Parse(f[8]) || d.Nodes.Length != int.Parse(f[9]) || d.Edges.Length != int.Parse(f[10]))
                    failures.Add(f[0] + ": counts");
                var k = d.Checks;
                if (k.Positions != long.Parse(f[11]) || k.SumEastCm != long.Parse(f[12]) || k.SumNorthCm != long.Parse(f[13]) || k.SumInts != long.Parse(f[14]))
                    failures.Add($"{f[0]}: checks {k.Positions}/{k.SumEastCm}/{k.SumNorthCm}/{k.SumInts}");
            }
        }
        // Frame: road 13848342 starts east 464.63 m, north 500 m from chunk 7,-30's corner.
        var c = Dadc.Decode(File.ReadAllBytes(Path.Combine(chunksDir, "c_7_-30.dadc")));
        var road = c.Roads.Single(r => r.Id == 13848342);
        if (road.Pts[0] != 46463 || road.Pts[1] != 50000 || road.NameEn != "Airport Road") failures.Add("frame: road 13848342");
        // Corrupt input must throw DadcFormatException, never return or throw anything else.
        var good = File.ReadAllBytes(Path.Combine(chunksDir, "c_7_-30.dadc"));
        var bad = new List<byte[]> { good.Take(good.Length - 3).ToArray(), good.Concat(new byte[] { 0 }).ToArray() };
        var magic = (byte[])good.Clone(); magic[0] = 0; bad.Add(magic);
        var fmt = (byte[])good.Clone(); fmt[4] = 9; bad.Add(fmt);
        for (int cut = 5; cut < good.Length; cut += 997) bad.Add(good.Take(cut).ToArray());
        int rejected = 0;
        foreach (var b in bad)
        {
            try { Dadc.Decode(b); failures.Add($"corrupt input of {b.Length} bytes decoded"); }
            catch (DadcFormatException) { rejected++; }
            catch (Exception e) { failures.Add($"corrupt input of {b.Length} bytes threw {e.GetType().Name}"); }
        }

        ms.Sort();
        Func<double, double> p = q => ms[Math.Min(ms.Count - 1, (int)(q / 100 * ms.Count))];
        Console.WriteLine($"runtime {Type.GetType("Mono.Runtime") != null} mono; chunks {n}; corrupt inputs rejected {rejected}/{bad.Count}");
        Console.WriteLine($"decode ms p50 {p(50):F3} p95 {p(95):F3} max {ms[ms.Count - 1]:F3} total {ms.Sum():F0}");
        foreach (var x in failures.Take(20)) Console.WriteLine("FAIL " + x);
        Console.WriteLine(failures.Count == 0 ? "PASS" : $"FAIL ({failures.Count})");
        return failures.Count == 0 ? 0 : 1;
    }
}
