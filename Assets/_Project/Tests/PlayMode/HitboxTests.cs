// Bodies a shot hits part by part (Hitbox, Bystander, 2026-11-12). These run the parts plain play uses: the street crowd's people, the fighters, and the player (one of the twenty).
using System.Collections;
using System.Linq;
using Dad.Core;
using Dad.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Dad.Tests
{
    public class HitboxTests
    {
        static string Model => NpcRoster.Load().people.First(w => w.Walks).model;

        static GameObject Floor()
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane); // the default layer, as in FighterTests
            floor.transform.localScale = new Vector3(12, 1, 12);
            return floor;
        }

        static Bystander Someone(Vector3 at)
        {
            var p = Person.Spawn(Model, at, 0);
            Assert.IsNotNull(p, "no person " + Model);
            return Bystander.Give(p);
        }

        static Vector3 Bone(Bystander b, HumanBodyBones bone) => b.GetComponent<Person>().Body.GetBoneTransform(bone).position;
        static Vector3 Between(Bystander b, HumanBodyBones a, HumanBodyBones c) => (Bone(b, a) + Bone(b, c)) / 2;

        static IEnumerator Settle()
        {
            for (int i = 0; i < 3; i++) yield return new WaitForFixedUpdate();
            Physics.SyncTransforms();
        }

        static void Clear(params GameObject[] also)
        {
            foreach (var p in Object.FindObjectsOfType<Person>()) Object.Destroy(p.gameObject);
            foreach (var rb in Object.FindObjectsOfType<Rigidbody>()) if (rb.GetComponentInParent<Person>() == null) Object.Destroy(rb.gameObject); // dropped guns
            foreach (var g in also) if (g != null) Object.Destroy(g);
        }

        // Everyone gets the eleven parts of a ragdoll on the Body layer, each saying which part it is: the street's people, and the player (one of the twenty).
        [UnityTest]
        public IEnumerator EveryoneHasElevenParts()
        {
            var someone = Someone(Vector3.zero);
            var you = Person.Spawn(OnFoot.Who, new Vector3(3, 0, 0), 0);
            Assert.IsNotNull(Hitbox.Give(you.Body), "the player could not take a ragdoll");
            yield return null;
            foreach (var body in new[] { someone.GetComponent<Person>(), you })
            {
                var parts = body.GetComponentsInChildren<Hitbox>();
                Assert.AreEqual(11, parts.Length, body.Who);
                Assert.That(parts.All(h => h.gameObject.layer == WorldLayers.Body && h.GetComponent<Collider>() != null), body.Who + ": a part is off the Body layer or has no collider");
                Assert.AreEqual(1, parts.Count(h => h.Part == Hitbox.Zone.Head), body.Who);
                Assert.AreEqual(4, parts.Count(h => h.Part == Hitbox.Zone.Arm), body.Who);
                Assert.AreEqual(4, parts.Count(h => h.Part == Hitbox.Zone.Leg), body.Who);
            }
            Assert.IsFalse(Physics.GetIgnoreLayerCollision(WorldLayers.Body, WorldLayers.World), "bodies must lie on the street");
            Assert.IsTrue(Physics.GetIgnoreLayerCollision(WorldLayers.Body, WorldLayers.Vehicle), "a car must not stop dead on someone's elbow");
            Assert.IsTrue(Physics.GetIgnoreLayerCollision(WorldLayers.Body, WorldLayers.Player), "you must not snag on anyone");
            Clear();
            yield return null;
        }

        // A shot does the gun's damage times the part's share: three times at the head, once at the chest, three quarters at an arm or a leg.
        [UnityTest]
        public IEnumerator EachPartTakesItsShare()
        {
            var floor = Floor();
            var b = Someone(Vector3.zero);
            yield return Settle();
            void Shoot(Vector3 target, Vector3 from, Hitbox.Zone part, float share)
            {
                Assert.IsTrue(Hitbox.Cast(from, (target - from).normalized, 10, Fighter.Hits, null, out var hit), "the shot met nothing aimed at the " + part);
                var h = hit.collider.GetComponent<Hitbox>();
                Assert.IsNotNull(h, "the shot aimed at the " + part + " met " + hit.collider.name);
                Assert.AreEqual(part, h.Part, "aimed at the " + part);
                float health = b.Health;
                Assert.AreSame(b, Hitbox.Strike(hit, 10, from, out float done));
                Assert.AreEqual(10 * share, done, 1e-4f, part.ToString());
                Assert.AreEqual(health - done, b.Health, 1e-4f, part.ToString());
            }
            var front = b.transform.forward * 3;
            Shoot(Bone(b, HumanBodyBones.Head) + Vector3.up * 0.06f, Bone(b, HumanBodyBones.Head) + Vector3.up * 0.06f + front, Hitbox.Zone.Head, 3);
            Shoot(Between(b, HumanBodyBones.Spine, HumanBodyBones.Neck), Between(b, HumanBodyBones.Spine, HumanBodyBones.Neck) + front, Hitbox.Zone.Body, 1);
            Shoot(Between(b, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot), Between(b, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot) + front, Hitbox.Zone.Leg, 0.75f);
            var arm = Between(b, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm);
            Shoot(arm, arm + (arm - Bone(b, HumanBodyBones.Spine)).normalized * 3, Hitbox.Zone.Arm, 0.75f); // from the side the arm is on
            Assert.IsFalse(b.Dead);
            Clear(floor);
            yield return null;
        }

        // The one who fires is left out of their own shot: through the body from behind, a ray that leaves it out meets none of its parts.
        [UnityTest]
        public IEnumerator YourOwnBodyIsLeftOut()
        {
            var b = Someone(Vector3.zero);
            yield return Settle();
            var chest = Between(b, HumanBodyBones.Spine, HumanBodyBones.Neck);
            var from = chest - b.transform.forward * 2;
            Assert.IsTrue(Hitbox.Cast(from, b.transform.forward, 5, Fighter.Hits, null, out var hit) && hit.collider.GetComponent<Hitbox>() != null, "the shot did not meet the body at all");
            Assert.IsFalse(Hitbox.Cast(from, b.transform.forward, 5, Fighter.Hits, b.transform, out hit) && hit.collider.GetComponent<Hitbox>() != null, "the shot met its own body: " + hit.collider?.name);
            Clear();
            yield return null;
        }

        // At no health someone goes limp: the clips stop, the body drops to the street pushed the way the shot went, and stays down.
        [UnityTest]
        public IEnumerator TheDeadFallLimpAndStayDown()
        {
            var floor = Floor();
            var b = Someone(Vector3.zero);
            var body = b.GetComponent<Person>().Body;
            yield return Settle();
            var chest = Between(b, HumanBodyBones.Spine, HumanBodyBones.Neck);
            var from = chest + b.transform.forward * 3;
            Assert.IsTrue(Hitbox.Cast(from, -b.transform.forward, 5, Fighter.Hits, null, out var hit));
            Hitbox.Strike(hit, 500, from, out _);
            Assert.IsTrue(b.Dead);
            var doll = body.GetComponent<Ragdoll>();
            Assert.IsTrue(doll.Down, "not limp");
            Assert.IsFalse(body.enabled, "the clips still pose the body");
            float hipsZ = Bone(b, HumanBodyBones.Hips).z;
            float until = Time.time + 3;
            while (Time.time < until) yield return null;
            var hips = Bone(b, HumanBodyBones.Hips);
            Debug.Log($"[Dad] fallen: hips at {hips}, moved {hips.z - hipsZ:F2} m along the shot");
            Assert.That(hips.y, Is.LessThan(0.45f), "the body is not down on the street");
            Assert.That(hips.z, Is.LessThan(hipsZ - 0.05f), "the body did not go the way the shot went");
            until = Time.time + 4;
            while (Time.time < until) yield return null;
            Assert.IsTrue(doll.Down && !doll.Rising, "the dead got up");
            Clear(floor);
            yield return null;
        }

        // A fighter shot dead goes limp, drops the gun, and is left as remains to be cleared away.
        [UnityTest]
        public IEnumerator AFighterShotDeadGoesLimp()
        {
            var floor = Floor();
            var who = NpcRoster.Load("People/fighters").people.First(p => p.role != "police");
            var f = Fighter.Spawn(who, Fighter.Side.Gang, Vector3.zero, 0);
            f.HoldFire = true;
            yield return Settle();
            Assert.IsTrue(f.GetComponentsInChildren<Hitbox>().Length == 11, "the fighter has no parts");
            var head = f.Body.Body.GetBoneTransform(HumanBodyBones.Head).position + Vector3.up * 0.06f;
            var from = head + f.transform.forward * 4;
            Assert.IsTrue(Hitbox.Cast(from, (head - from).normalized, 6, Fighter.Hits, null, out var hit), "nothing met");
            Assert.AreEqual(Hitbox.Zone.Head, hit.collider.GetComponent<Hitbox>()?.Part, "the head shot met " + hit.collider.name);
            Hitbox.Strike(hit, 34, from, out float done); // a rifle shot is 30: three at the head is more than a life
            Assert.AreEqual(102, done, 1e-3f);
            Assert.IsTrue(f.Dead, "one head shot of 34 did not kill");
            Assert.IsTrue(f.Body.Body.GetComponent<Ragdoll>().Down, "not limp");
            Assert.IsNotNull(f.Gun.Dropped, "still holds the gun");
            Assert.IsNotNull(f.GetComponent<Remains>(), "not left as remains");
            Clear(floor);
            yield return null;
        }

        // On the street: someone wounded runs from where the shot came; someone killed lies where they fell, and the crowd no longer moves them.
        [UnityTest]
        public IEnumerator TheStreetAnswersAShot()
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
            var people = Object.FindObjectsOfType<Bystander>().Where(b => b.gameObject.activeInHierarchy).OrderBy(b => b.name).ToArray();
            Assert.That(people.Length, Is.GreaterThanOrEqualTo(2), "too few on the street");
            var wounded = people[0];
            var killed = people[1];
            wounded.Hurt(10, wounded.transform.position + wounded.transform.forward * 8);
            killed.Hurt(500, killed.transform.position + killed.transform.forward * 8);
            crowd.Run(0.2f);
            Assert.AreEqual("fleeing", crowd.Mood(wounded.name), "the wounded did not run");
            Assert.AreEqual("dead", crowd.Mood(killed.name));
            var lies = crowd.Where(killed.name);
            var away = Vector3.Distance(crowd.Where(wounded.name), wounded.HurtFrom);
            crowd.Run(3);
            Assert.That(Vector3.Distance(crowd.Where(wounded.name), wounded.HurtFrom), Is.GreaterThan(away + 3), "the wounded is not getting away");
            Assert.AreEqual(lies, crowd.Where(killed.name), "the crowd moved the dead");
            ChunkStreamer.BuildWalks = built;
            Clear(host, eye.gameObject, origin.gameObject);
            yield return null;
        }
    }
}
