// The twenty Blender people (any of whom can be the player) stand in the clips' reference pose (2026-11-12): the clips were made on marker_man, and a clip can only put
// our wrists, hands and fingers where it put his if their muscles are measured from the same pose (Dad.EditorTools.PeoplePose). His model was deleted on 2026-11-13;
// the pose was measured from it first and is kept in Editor/ReferencePose.json.
using System.Linq;
using Dad.Core;
using Dad.EditorTools;
using NUnit.Framework;
using UnityEngine;

namespace Dad.Tests
{
    public class PeoplePoseTests
    {
        [Test]
        public void ArmsHandsAndFingersStartFromTheIdolsPose()
        {
            var off = new System.Collections.Generic.List<string>();
            foreach (var who in PeoplePose.People())
                foreach (var (what, degrees, turned) in PeoplePose.Residuals(PeoplePose.Of(who)))
                    if (turned && degrees > 1) off.Add($"{who} {what} {degrees:F1} degrees off the reference pose");
            Assert.IsEmpty(off, string.Join("\n", off));
        }

        [Test]
        public void TheReferencePoseIsKept()
        {
            var people = PeoplePose.People().ToArray();
            Assert.AreEqual(20, people.Length, "the twenty, every one of whom can be the player");
            CollectionAssert.AreEquivalent(Cast.All, people, "the people you can play are the people posed");
            Assert.IsNotEmpty(PeoplePose.Residuals(PeoplePose.Of("npc_m08")), "the reference pose file is read");
        }

        // A rebuilt model whose bones moved (longer arms) needs its stored pose made again: StandThePeopleAsTheIdol, after each sync.
        [Test]
        public void TheStoredPoseIsOfTheModelAsItIs()
        {
            var stale = PeoplePose.People().Select(w => (w, n: PeoplePose.Stale(PeoplePose.Of(w)))).Where(x => x.n > 0).Select(x => $"{x.w}: {x.n} bones").ToArray();
            Assert.IsEmpty(stale, "run PeoplePoseTests.StandThePeopleAsTheIdol:\n" + string.Join("\n", stale));
        }

        // Turns the stored pose of everyone built in Blender and reimports them; run by name after a new build is synced. Logs every residual before and after.
        [Test, Explicit("changes the people's import settings: run by name")]
        public void StandThePeopleAsTheIdol()
        {
            foreach (var who in PeoplePose.People())
            {
                string Report() => string.Join(", ", PeoplePose.Residuals(PeoplePose.Of(who)).Where(r => r.degrees >= 0.5f).Select(r => $"{r.what} {r.degrees:F1}{(r.turned ? "" : " (kept)")}"));
                Debug.Log($"[Dad] POSE BEFORE {who}: {Report()}");
                Debug.Log("[Dad] POSED " + PeoplePose.Match(PeoplePose.Of(who)));
                Debug.Log($"[Dad] POSE AFTER {who}: {Report()}");
            }
        }
    }
}
