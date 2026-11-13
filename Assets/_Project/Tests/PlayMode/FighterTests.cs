using System.Collections;
using System.Linq;
using Dad.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Dad.Tests
{
    public class FighterTests
    {
        static NpcRecord Record(string id) => NpcRoster.Load("People/fighters").people.First(p => p.id == id);

        static GameObject Floor()
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane); // the default layer: shots and dropped guns meet it, as they meet the street
            floor.transform.localScale = new Vector3(12, 1, 12);
            return floor;
        }

        static void Clear()
        {
            foreach (var f in Object.FindObjectsOfType<Fighter>()) Object.Destroy(f.gameObject);
            foreach (var a in Object.FindObjectsOfType<Rigidbody>()) Object.Destroy(a.gameObject);
        }

        // Gang members fire; the shots bring the police, who walk in from 30 m and close in; both sides fire, shots strike, people fall, and each fallen one's gun drops
        // to the ground and comes to rest there.
        [UnityTest]
        public IEnumerator ShotsBringThePoliceAndTheyFight()
        {
            Random.InitState(11);
            Time.captureFramerate = 30;
            var floor = Floor();
            var host = new GameObject("precinct");
            var precinct = Precinct.Attach(host);
            precinct.Delay = 1;
            precinct.From = 30;
            var gang = new[] { Fighter.Spawn(Record("GANG_001"), Fighter.Side.Gang, new Vector3(0, 0, 0), 90), Fighter.Spawn(Record("GANG_003"), Fighter.Side.Gang, new Vector3(0, 0, 2), 90) };
            Assert.That(gang, Has.All.Not.Null);
            Danger.Report(gang[0].transform.position + Vector3.up, Danger.Kind.Shot); // the first shot, fired at nobody
            float until = Time.time + 2;
            while (Time.time < until) yield return null;
            Assert.AreEqual(2, precinct.Officers.Count, "the police were not sent");
            var police = precinct.Officers.ToArray();
            float startAway = police.Min(p => Vector3.Distance(p.transform.position, gang[0].transform.position));
            Assert.That(startAway, Is.GreaterThan(25), "the police came too near");
            until = Time.time + 90;
            while (Time.time < until && !(gang.All(g => g.Dead) || police.All(p => p.Dead))) yield return null;
            string tally = string.Join("; ", gang.Concat(police).Select(f => $"{f.name} {f.Team} fired {f.Fired} struck {f.Struck} health {f.Health:F0}"));
            Debug.Log("[Dad] fight: " + tally);
            Assert.That(gang.Sum(g => g.Fired), Is.GreaterThan(0), "the gang never fired: " + tally);
            Assert.That(police.Sum(p => p.Fired), Is.GreaterThan(0), "the police never fired: " + tally);
            Assert.That(gang.Concat(police).Sum(f => f.Struck), Is.GreaterThan(0), "no shot struck anyone: " + tally);
            Assert.That(gang.All(g => g.Dead) || police.All(p => p.Dead), "neither side fell in 90 s: " + tally);
            // the guns of the fallen: dropped, at rest on the ground
            until = Time.time + 4;
            while (Time.time < until) yield return null;
            foreach (var f in gang.Concat(police).Where(f => f.Dead))
            {
                var gun = f.Gun.Dropped;
                Assert.IsNotNull(gun, f.name + " still holds the gun");
                var body = gun.GetComponent<Rigidbody>();
                Assert.That(gun.position.y, Is.InRange(-0.05f, 0.4f), f.name + "'s gun is not on the ground");
                Assert.That(body.linearVelocity.magnitude, Is.LessThan(0.05f), f.name + "'s gun has not come to rest");
            }
            Time.captureFramerate = 0;
            Clear();
            Object.Destroy(host); Object.Destroy(floor);
            yield return null;
        }

        // Nobody fires at someone they cannot see: a wall between them stops it. Take the wall away and the shots come.
        [UnityTest]
        public IEnumerator NobodyShootsThroughAWall()
        {
            Random.InitState(3);
            Time.captureFramerate = 30;
            var floor = Floor();
            var a = Fighter.Spawn(Record("GANG_002"), Fighter.Side.Gang, new Vector3(0, 0, 0), 0);
            var b = Fighter.Spawn(Record("POLICE_001"), Fighter.Side.Police, new Vector3(0, 0, 12), 180);
            b.HoldFire = true; // stands and aims back, and does not fire
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.layer = WorldLayers.World;
            wall.transform.position = new Vector3(0, 2, 6);
            wall.transform.localScale = new Vector3(8, 4, 0.3f);
            float until = Time.time + 3;
            while (Time.time < until) yield return null;
            Assert.AreEqual(0, a.Fired, "fired through a wall at someone out of sight");
            Assert.IsNull(a.Target);
            Object.Destroy(wall);
            until = Time.time + 3;
            while (Time.time < until) yield return null;
            Assert.That(a.Fired, Is.GreaterThan(0), "never fired with the wall gone");
            Time.captureFramerate = 0;
            Clear();
            Object.Destroy(floor);
            yield return null;
        }
    }
}
