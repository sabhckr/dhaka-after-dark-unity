using System.Collections;
using System.Linq;
using Dad.Core;
using Dad.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Dad.Tests
{
    public class NpcCrowdTests
    {
        // Everyone on the roster on one 100 m sidewalk, far busier than a street, for a minute: away from the two ends, where people gather to turn, nobody moving comes through anybody.
        [UnityTest]
        public IEnumerator NobodyWalksThroughAnyone()
        {
            bool built = ChunkStreamer.BuildWalks;
            var origin = new GameObject("origin").AddComponent<FloatingOrigin>();
            var eye = new GameObject("eye").transform;
            var host = new GameObject("crowd");
            var crowd = NpcCrowd.Attach(host, null, eye);
            crowd.Manual = true;
            var walks = new ChunkWalks();
            walks.Lines.Add(new WalkLine { East = new double[] { 0, 0 }, North = new double[] { -50, 50 }, Height = new[] { 0f, 0f }, Along = new double[] { 0, 100 } });
            crowd.Use(walks);
            yield return null;
            crowd.Run(0.1f);
            var people = Object.FindObjectsOfType<Person>();
            Assert.That(people.Length, Is.GreaterThan(1), "nobody came out");
            var last = people.Select(p => p.transform.position).ToArray();
            float closest = float.MaxValue;
            string who = "";
            for (int step = 0; step < 600; step++)
            {
                crowd.Run(0.1f);
                for (int a = 0; a < people.Length; a++)
                    for (int b = a + 1; b < people.Length; b++)
                    {
                        Vector3 pa = people[a].transform.position, pb = people[b].transform.position;
                        if (Mathf.Abs(pa.z) > 35 || Mathf.Abs(pb.z) > 35) continue;
                        if (pa == last[a] && pb == last[b]) continue; // two who chose to stand together
                        float d = Vector2.Distance(new Vector2(pa.x, pa.z), new Vector2(pb.x, pb.z));
                        if (d < closest) { closest = d; who = people[a].name + " and " + people[b].name + " at step " + step; }
                    }
                for (int a = 0; a < people.Length; a++) last[a] = people[a].transform.position;
                if (step % 60 == 0) yield return null;
            }
            Debug.Log($"[Dad] crowd closest pass {closest:F3} m: {who}");
            Assert.That(closest, Is.GreaterThan(0.4f), who);
            ChunkStreamer.BuildWalks = built;
            foreach (var p in people) Object.Destroy(p.gameObject);
            Object.Destroy(host); Object.Destroy(eye.gameObject); Object.Destroy(origin.gameObject);
            yield return null;
        }

        // A shot among people on a sidewalk: everyone startles, then each cowers where they are or runs away from it, getting further off, and in time all calm down.
        [UnityTest]
        public IEnumerator PeopleRunFromAShot()
        {
            bool built = ChunkStreamer.BuildWalks;
            var origin = new GameObject("origin").AddComponent<FloatingOrigin>();
            var eye = new GameObject("eye").transform;
            var host = new GameObject("crowd");
            var crowd = NpcCrowd.Attach(host, null, eye);
            crowd.Manual = true;
            var walks = new ChunkWalks();
            walks.Lines.Add(new WalkLine { East = new double[] { 0, 0 }, North = new double[] { -50, 50 }, Height = new[] { 0f, 0f }, Along = new double[] { 0, 100 } });
            crowd.Use(walks);
            yield return null;
            crowd.Run(3);
            // The walker in the body you play is not in the crowd (Cast), so is not counted.
            var ids = NpcRoster.Load().people.Where(w => w.Walks && w.model != Cast.You).Select(w => w.id).ToArray();
            Assert.That(ids.Length, Is.GreaterThan(0));
            var at = ids.Aggregate(Vector3.zero, (s, id) => s + crowd.Where(id)) / ids.Length;
            Danger.Report(at, Danger.Kind.Shot);
            crowd.Run(0.1f);
            foreach (var id in ids) Assert.That(crowd.Mood(id), Is.EqualTo("startled"), id + " did not hear a shot " + Vector3.Distance(crowd.Where(id), at).ToString("F1") + " m away");
            crowd.Run(1.2f);
            var before = ids.ToDictionary(id => id, id => Vector3.Distance(crowd.Where(id), at));
            Assert.That(ids.Count(id => crowd.Mood(id) == "fleeing"), Is.GreaterThan(0), "nobody ran");
            foreach (var id in ids) Assert.That(new[] { "fleeing", "cowering" }, Has.Member(crowd.Mood(id)), id);
            var fled = ids.Where(id => crowd.Mood(id) == "fleeing").ToArray();
            crowd.Run(3);
            foreach (var id in fled)
                if (crowd.Mood(id) == "fleeing") Assert.That(Vector3.Distance(crowd.Where(id), at), Is.GreaterThan(before[id] + 4), id + " is not getting away"); // in 3 s: sprinting near it, running and looking back further off
            yield return null;
            crowd.Run(15);
            foreach (var id in ids) Assert.That(crowd.Mood(id), Is.EqualTo("calm"), id + " never calmed down");
            ChunkStreamer.BuildWalks = built;
            foreach (var p in Object.FindObjectsOfType<Person>()) Object.Destroy(p.gameObject);
            Object.Destroy(host); Object.Destroy(eye.gameObject); Object.Destroy(origin.gameObject);
            yield return null;
        }
    }
}
