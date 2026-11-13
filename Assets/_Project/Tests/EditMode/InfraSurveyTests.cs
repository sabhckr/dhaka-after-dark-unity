// Infrastructure placement checker (AXIOM v3 audit, section G; section J step 5): the read-only survey of
// every metro pier, footbridge column, stair and elevated deck in the slice (InfraSurvey), gated against the
// known list (Tests/Infra/known-violations.csv). A violation that is not listed fails, and so does a listed
// one that is gone, so nothing new comes in unseen and a fix must take its rows out. These are spatial checks
// only: navigation, collision and driving checks need Phases E and F (section G.4), and lane checks need lane
// polygons (G.5).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dad.EditorTools;
using Dad.World;
using NUnit.Framework;
using UnityEngine;

namespace Dad.Tests
{
    public class InfraSurveyTests
    {
        static string ChunkDir => Path.Combine(TestWorld.Root, "chunks");
        static string KnownList => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Tests", "Infra", "known-violations.csv"));

        static InfraSurvey.Result survey;

        static InfraSurvey.Result Survey()
        {
            if (survey != null) return survey;
            foreach (var id in InfraSurvey.Slice)
                Assume.That(File.Exists(Path.Combine(ChunkDir, id + ".dadc")), "world data missing: run Tools/worldgen/build.sh");
            var t0 = DateTime.UtcNow;
            survey = InfraSurvey.Run(ChunkDir, InfraSurvey.Slice, InfraSurvey.Ring(InfraSurvey.Slice));
            Debug.Log($"Infrastructure survey: {survey.Supports.Count} supports, {survey.Decks.Count} decks, {survey.Findings.Count(f => !f.Note)} violations, " +
                      $"{survey.Findings.Count(f => f.Note)} notes in {(DateTime.UtcNow - t0).TotalSeconds:F1} s");
            return survey;
        }

        [Test]
        public void SliceHasNoUnlistedInfrastructureViolation()
        {
            var res = Survey();
            var list = InfraKnown.Read(KnownList);
            var (fresh, gone) = InfraKnown.Compare(res, list);
            Debug.Log($"Against the known list ({list.Count} rows): {fresh.Count} new, {gone.Count} gone; spatial checks only");
            Assert.That(fresh, Is.Empty, "violations not in Tests/Infra/known-violations.csv (fix them, or list them with a reference):\n" +
                                         string.Join("\n", fresh.Select(f => $"{f.Key}: {f.Detail}")));
            Assert.That(gone, Is.Empty, "listed violations the survey no longer finds (take their rows out):\n" + string.Join("\n", gone));
        }

        [Test]
        public void SurveyAgreesWithTheBuilderOnCarriagewayPiers()
        {
            var res = Survey();
            Assert.That(res.BuilderOnRoad, Is.GreaterThan(0), "the builder marks no pier on a carriageway: the survey is reading something else");
            Assert.That(res.BuilderOnRoadAgreed, Is.EqualTo(res.BuilderOnRoad), "of the piers the builder marks 'on a carriageway', some are not on one in the survey");
        }

        [Test]
        public void RegisterHasAStableRowForEverySupport()
        {
            var res = Survey();
            Assert.That(res.Chunks, Is.EquivalentTo(InfraSurvey.Slice));
            var dupes = res.Supports.GroupBy(s => s.Id).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            Assert.That(dupes, Is.Empty, "support ids must be unique");
            Assert.That(res.Supports.Count(s => s.Structure == InfraSurvey.Structure.Viaduct && s.Built), Is.GreaterThanOrEqualTo(60), "built piers");
            Assert.That(res.Supports.Count(s => s.Structure == InfraSurvey.Structure.Footbridge), Is.GreaterThan(0), "footbridge columns");
            Assert.That(res.Supports.Count(s => s.Structure == InfraSurvey.Structure.Stair), Is.GreaterThan(0), "stairs");
            foreach (var s in res.Supports)
            {
                Assert.That(s.Source, Is.Not.Empty, s.Id + " source");
                Assert.That(s.Confidence, Is.Not.Empty, s.Id + " confidence");
                Assert.That(res.Chunks, Does.Contain(s.Chunk), s.Id + ": context supports must not be reported");
                if (s.Structure == InfraSurvey.Structure.Viaduct && s.Built)
                {
                    Assert.That(s.Tracks, Is.Not.Empty, s.Id + " carries no track");
                    Assert.That(double.IsNaN(s.ExpectedTop), Is.False, s.Id + " has no deck above it");
                }
            }
            Assert.That(res.Decks.Count(d => d.Structure == InfraSurvey.Structure.Flyover), Is.GreaterThan(0), "flyover decks");
            foreach (var f in res.Findings)
                Assert.That(f.Detail, Is.Not.Empty, f.Key);
        }

        [Test]
        public void SurveyIsDeterministic()
        {
            var a = Survey();
            var b = InfraSurvey.Run(ChunkDir, InfraSurvey.Slice, InfraSurvey.Ring(InfraSurvey.Slice));
            Assert.That(b.Supports.Select(s => s.Id), Is.EqualTo(a.Supports.Select(s => s.Id)));
            Assert.That(b.Findings.Select(f => f.Key + " " + f.Detail), Is.EqualTo(a.Findings.Select(f => f.Key + " " + f.Detail)));
        }

        [Test]
        public void KnownListComparisonCatchesNewAndGone()
        {
            // The list here is the survey's own violations, so this checks the comparison alone, whatever the world holds.
            var res = Survey();
            var list = res.Findings.Where(f => !f.Note).GroupBy(f => f.Key).ToDictionary(g => g.Key, g => new InfraKnown.Entry { Key = g.Key, Reference = "self" });
            Assert.That(InfraKnown.Compare(res, list).fresh, Is.Empty);
            Assert.That(InfraKnown.Compare(res, list).gone, Is.Empty);
            var violation = res.Findings.First(f => !f.Note);
            var less = new Dictionary<string, InfraKnown.Entry>(list);
            less.Remove(violation.Key);
            var (fresh, gone) = InfraKnown.Compare(res, less);
            Assert.That(fresh.Select(f => f.Key), Is.EqualTo(new[] { violation.Key }), "a violation taken off the list must show as new");
            Assert.That(gone, Is.Empty);
            var more = new Dictionary<string, InfraKnown.Entry>(list) { ["VP-0.0-0.0|carriageway|1"] = new InfraKnown.Entry { Key = "VP-0.0-0.0|carriageway|1", Reference = "test" } };
            (fresh, gone) = InfraKnown.Compare(res, more);
            Assert.That(gone, Is.EqualTo(new[] { "VP-0.0-0.0|carriageway|1" }), "a listed violation that is not found must show as gone");
            var note = res.Findings.FirstOrDefault(f => f.Note);
            if (note != null) Assert.That(InfraKnown.Compare(res, list).fresh.Any(f => f.Key == note.Key), Is.False, "notes are never violations");
        }

        /// <summary>The 64 chunks with an elevated metro track (Line 6, Uttara North to Motijheel), as of the 2026-11-04 data.</summary>
        static readonly string[] Line6 =
        {
            "c_10_-24", "c_11_-24", "c_11_-51", "c_11_-52", "c_11_-53", "c_11_-54", "c_11_-55", "c_11_-56", "c_11_-57", "c_11_-58",
            "c_12_-24", "c_12_-51", "c_13_-23", "c_13_-24", "c_14_-23", "c_14_-24", "c_15_-24", "c_2_-45", "c_2_-46", "c_2_-47",
            "c_2_-48", "c_2_-49", "c_2_-50", "c_2_-51", "c_3_-41", "c_3_-42", "c_3_-43", "c_3_-44", "c_3_-45", "c_3_-48",
            "c_3_-49", "c_3_-51", "c_3_-52", "c_3_-53", "c_3_-54", "c_3_-55", "c_3_-56", "c_3_-57", "c_4_-38", "c_4_-39",
            "c_4_-40", "c_4_-41", "c_4_-56", "c_5_-35", "c_5_-36", "c_5_-37", "c_5_-38", "c_6_-30", "c_6_-31", "c_6_-32",
            "c_6_-33", "c_6_-34", "c_6_-35", "c_7_-30", "c_7_-31", "c_8_-25", "c_8_-27", "c_8_-28", "c_8_-29", "c_8_-30",
            "c_9_-24", "c_9_-25", "c_9_-26", "c_9_-27",
        };

        static InfraSurvey.Result line6;

        static InfraSurvey.Result Line6Survey()
        {
            if (line6 != null) return line6;
            foreach (var id in Line6)
                Assume.That(File.Exists(Path.Combine(ChunkDir, id + ".dadc")), "world data missing: run Tools/worldgen/build.sh");
            return line6 = InfraSurvey.Run(ChunkDir, Line6, InfraSurvey.Ring(Line6));
        }

        [Test]
        public void Line6PiersOnACarriagewayStandOnIslandsOrAreListedWithTheReason()
        {
            // K-37 P-6 (owner, 2026-11-04): along the whole line, every pier the builder puts on a median island has one
            // built under it, every carriageway beside it keeps a lane (median-narrow), traffic passes on both sides of it
            // (median-one-side), and no island reaches into a junction, a crossing, a bus stop, a building on the ground
            // or another island; every pier left on a carriageway says why. Before P-6: 354 piers on a carriageway;
            // after: 23 on islands, 331 left. P-5a (owner, 2026-11-05): the piers on the continuous median are judged as
            // island piers with two lanes each side (median-one-side at 6 m), one-way twins under it (median-two-way,
            // median-split), and every part of the median clear of junction pads, crossings, bus stops and ground buildings
            // (run-*): 23 on it, 20 left on P-6 islands, 311 on a carriageway.
            var res = Line6Survey();
            var viaduct = res.Supports.Where(s => s.Structure == InfraSurvey.Structure.Viaduct && s.Built).ToList();
            var islands = viaduct.Where(s => s.BuilderWhy == "median").ToList();
            var runs = viaduct.Where(s => s.BuilderWhy == "median run").ToList();
            var left = viaduct.Where(s => s.BuilderWhy != null && s.BuilderWhy.StartsWith("on a carriageway", StringComparison.Ordinal)).ToList();
            var bad = res.Findings.Where(f => !f.Note && (f.Id.StartsWith("VP-", StringComparison.Ordinal) && (f.Check.StartsWith("median", StringComparison.Ordinal) || f.Check.StartsWith("island", StringComparison.Ordinal))
                                                          || f.Id.StartsWith("MR-", StringComparison.Ordinal) || f.Check.StartsWith("run-", StringComparison.Ordinal))).ToList();
            Debug.Log($"Line 6: {viaduct.Count} piers built, {islands.Count} on a median island, {runs.Count} on the continuous median ({res.RunPieces} pieces, {res.RunLength:F0} m), {left.Count} on a carriageway without either: " +
                      string.Join(", ", left.GroupBy(s => s.BuilderWhy.Substring(s.BuilderWhy.LastIndexOf(':') + 2)).OrderBy(g => g.Key).Select(g => $"{g.Key} {g.Count()}")));
            Assert.That(bad, Is.Empty, string.Join("\n", bad.Select(f => $"{f.Key}: {f.Detail}")));
            Assert.That(islands.All(s => s.OnIsland), Is.True, "an island under every median pier");
            Assert.That(runs.All(s => s.OnIsland), Is.True, "the median under every pier the builder puts on it");
            Assert.That(runs, Has.Count.EqualTo(23));
            Assert.That(res.BuilderOnRun, Is.EqualTo(23));
            Assert.That(res.RunPieces, Is.EqualTo(37));
            Assert.That(left.All(s => s.BuilderWhy.StartsWith("on a carriageway; no island: ", StringComparison.Ordinal)), Is.True, "a reason for each");
            Assert.That(islands, Has.Count.EqualTo(20));
            Assert.That(left, Has.Count.EqualTo(311));
            Assert.That(res.BuilderOnRoadAgreed, Is.EqualTo(res.BuilderOnRoad));
        }

        [Test]
        public void Line6HasNoDuplicatePierAndNoSpanOutOfRange()
        {
            // K-37 P-2 and K-50 a, b (2026-11-04), along the whole line, not just the slice: no pier built twice across
            // a chunk border, no span under 25 m, none longer than one grid place spanned. Before P-2: 51 duplicate,
            // 15 long and 5 short span findings on viaduct piers here.
            var res = Line6Survey();
            var bad = res.Findings.Where(f => !f.Note && f.Key.StartsWith("VP-", StringComparison.Ordinal) &&
                                              (f.Check == "duplicate" || f.Check == "span-short" || f.Check == "span-long")).ToList();
            int piers = res.Supports.Count(s => s.Structure == InfraSurvey.Structure.Viaduct && s.Built);
            Debug.Log($"Line 6: {res.Chunks.Count} chunks, {piers} piers built, {bad.Count} duplicate or span findings on piers");
            Assert.That(piers, Is.GreaterThan(500));
            Assert.That(bad, Is.Empty, string.Join("\n", bad.Select(f => $"{f.Key}: {f.Detail}")));
        }

        [Test]
        public void KnownListRowsNeedAReference()
        {
            string dir = Path.Combine(Path.GetTempPath(), "dad-infra-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                string ok = Path.Combine(dir, "ok.csv"), bare = Path.Combine(dir, "bare.csv"), twice = Path.Combine(dir, "twice.csv");
                File.WriteAllText(ok, "# comment\nkey,reference,note\nA|carriageway|1,K-37,\"group E, insufficient evidence\"\n");
                File.WriteAllText(bare, "key,reference,note\nA|carriageway|1,,no reference\n");
                File.WriteAllText(twice, "A|x,K-1,\nA|x,K-2,\n");
                var e = InfraKnown.Read(ok);
                Assert.That(e["A|carriageway|1"].Note, Is.EqualTo("group E, insufficient evidence"));
                Assert.Throws<FormatException>(() => InfraKnown.Read(bare));
                Assert.Throws<FormatException>(() => InfraKnown.Read(twice));
            }
            finally
            {
                foreach (var f in Directory.GetFiles(dir)) File.Delete(f);
                Directory.Delete(dir);
            }
        }

        [Test]
        public void GeometryHelpersMeasureClearanceAndProgress()
        {
            var square = new double[] { 0, 0, 10, 0, 10, 10, 0, 10 };
            Assert.That(InfraSurvey.Clearance(square, 5, 5), Is.EqualTo(-5).Within(1e-9), "inside: negative, to the nearest edge");
            Assert.That(InfraSurvey.Clearance(square, 13, 5), Is.EqualTo(3).Within(1e-9), "outside: positive");
            Assert.That(InfraSurvey.Clearance(square, 9.5, 2), Is.EqualTo(-0.5).Within(1e-9));
            var bend = new double[] { 0, 0, 10, 0, 10, 10 };
            var (d, along) = InfraSurvey.Project(bend, 12, 4);
            Assert.That(d, Is.EqualTo(2).Within(1e-9));
            Assert.That(along, Is.EqualTo(14).Within(1e-9), "progress runs round the bend");
            (d, along) = InfraSurvey.Project(bend, -3, 0);
            Assert.That(d, Is.EqualTo(3).Within(1e-9));
            Assert.That(along, Is.EqualTo(0).Within(1e-9), "clamped at the start");
        }
    }
}
