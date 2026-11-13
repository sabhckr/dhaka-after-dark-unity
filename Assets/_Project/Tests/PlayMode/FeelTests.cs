// What a shot sounds like and leaves (GunSound, Impacts, 2026-11-12, Docs/Characters/SHOOTER_BAR_AUDIT.md section 10), and the jump's height at any frame rate. Sounds
// cannot be listened to here: what is checked is that each recording is one whole shot, that the right one starts, where, how loud and how late.
using System.Collections;
using System.Linq;
using Dad.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Dad.Tests
{
    public class FeelTests
    {
        static string Model => NpcRoster.Load().people.First(w => w.Walks).model;

        static IEnumerator For(float seconds)
        {
            float until = Time.time + seconds;
            while (Time.time < until) yield return null;
        }

        static void Clear(params GameObject[] also)
        {
            foreach (var p in Object.FindObjectsOfType<Person>()) Object.Destroy(p.gameObject);
            foreach (var rb in Object.FindObjectsOfType<Rigidbody>()) if (rb.GetComponentInParent<Person>() == null) Object.Destroy(rb.gameObject);
            foreach (var g in also) if (g != null) Object.Destroy(g);
            Impacts.Clear();
            Time.captureFramerate = 0;
        }

        static GameObject Ear(Vector3 at)
        {
            var g = new GameObject("ear");
            g.transform.position = at;
            g.AddComponent<AudioListener>();
            return g;
        }

        // Each gun has its close and far takes; each is one shot that starts at once (its loudest within the first 30 ms) and dies away, recorded at 96 kHz in stereo.
        [Test]
        public void EachGunHasItsShots()
        {
            foreach (var (gun, near, far) in new[] { ("scar", 2, 2), ("ump", 3, 3), ("glock", 3, 2) })
            {
                Assert.AreEqual(near, GunSound.Takes(GunSound.Folder, gun + "-near").Length, gun + " close");
                Assert.AreEqual(far, GunSound.Takes(GunSound.Folder, gun + "-far").Length, gun + " far");
                foreach (var c in GunSound.Takes(GunSound.Folder, gun + "-near").Concat(GunSound.Takes(GunSound.Folder, gun + "-far")))
                {
                    Assert.AreEqual(96000, c.frequency, c.name);
                    Assert.AreEqual(2, c.channels, c.name);
                    var data = new float[c.samples * c.channels];
                    Assert.IsTrue(c.GetData(data, 0), c.name + " cannot be read");
                    int start = (int)(0.03f * c.frequency) * c.channels;
                    float first = data.Take(start).Max(Mathf.Abs), all = data.Max(Mathf.Abs);
                    int lastQuarter = data.Length * 3 / 4;
                    float end = data.Skip(lastQuarter).Max(Mathf.Abs);
                    Debug.Log($"[Dad] {c.name}: {c.length:F2} s, loudest {all:F2}, in the first 30 ms {first:F2}, in the last quarter {end:F3}");
                    Assert.That(first, Is.GreaterThan(all * 0.9f), c.name + ": the shot is not at the start");
                    Assert.That(end, Is.LessThan(all * 0.15f), c.name + ": it does not die away (a second shot?)");
                }
            }
            foreach (var surface in new[] { "stone", "metal", "wood", "glass", "body" }) Assert.AreEqual(5, GunSound.Takes(GunSound.Impacts, surface).Length, surface);
        }

        // Someone else's shot is heard from where it is fired, late by the way sound travels; close to it, the crack and the far sound; far off, only the far one. Your own
        // gun is heard at once, close.
        [UnityTest]
        public IEnumerator ShotsAreHeardFromWhereTheyAre()
        {
            var ear = Ear(Vector3.zero);
            var gang = NpcRoster.Load("People/fighters").people.First(p => p.role != "police");
            var near = Fighter.Spawn(gang, Fighter.Side.Gang, new Vector3(0, 0, 50), 180);
            var far = Fighter.Spawn(gang, Fighter.Side.Gang, new Vector3(0, 0, 200), 180);
            near.HoldFire = far.HoldFire = true;
            yield return null;
            GunSound.Log.Clear();
            near.Gun.Fire();
            var heard = GunSound.Log.ToArray();
            Assert.AreEqual(2, heard.Length, "a shot at 50 m: the crack and the far sound");
            Assert.That(heard.All(h => !h.Yours));
            Assert.That(heard[0].Clip.name, Does.StartWith(near.Gun.Gun.Name + "-near"));
            Assert.That(heard[1].Clip.name, Does.StartWith(near.Gun.Gun.Name + "-far"));
            float d = Vector3.Distance(near.Gun.Muzzle, Vector3.zero);
            Assert.AreEqual(d / GunSound.SpeedOfSound, heard[0].Delay, 1e-3f, "heard as late as sound takes");
            Assert.AreEqual(near.Gun.Muzzle, heard[0].At);
            Debug.Log($"[Dad] a shot {d:F0} m away is heard {heard[0].Delay * 1000:F0} ms late: {heard[0].Clip.name} at {heard[0].Volume:F2}, {heard[1].Clip.name} at {heard[1].Volume:F2}");
            GunSound.Log.Clear();
            far.Gun.Fire();
            Assert.AreEqual(1, GunSound.Log.Count, "a shot at 200 m: only the far sound");
            Assert.That(GunSound.Log[0].Clip.name, Does.Contain("-far"));
            yield return For(0.2f);
            Assert.That(GunSound.Playing(), Is.GreaterThan(0), "nothing is sounding");
            // your own gun
            var you = OnFoot.Spawn(new Vector3(3, 0.05f, 0), 0, null, null);
            var gun = Armed.Give(you.Person, "glock");
            yield return null;
            GunSound.Log.Clear();
            gun.Fire();
            Assert.AreEqual(2, GunSound.Log.Count);
            Assert.That(GunSound.Log.All(h => h.Yours && h.Delay == 0), "your own shot is late or not yours");
            Assert.That(GunSound.Log[0].Clip.name, Does.StartWith("glock-near"));
            Clear(ear);
        }

        // A shot into a wall leaves a hole where it struck, lying on the wall and facing out of it, throws dust and chips, and is heard; into a car, a hole and sparks.
        [UnityTest]
        public IEnumerator ShotsLeaveHolesAndDust()
        {
            var ear = Ear(Vector3.zero);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.layer = WorldLayers.World;
            wall.transform.position = new Vector3(0, 1.5f, 6);
            wall.transform.localScale = new Vector3(6, 3, 0.4f);
            var car = GameObject.CreatePrimitive(PrimitiveType.Cube);
            car.layer = WorldLayers.Traffic;
            car.transform.position = new Vector3(5, 0.8f, 3);
            yield return null;
            Physics.SyncTransforms();
            var from = new Vector3(0.3f, 1.4f, 0);
            Assert.IsTrue(Physics.Raycast(from, Vector3.forward, out var hit, 20));
            int made = Impacts.Made;
            GunSound.Log.Clear();
            Impacts.Hit(hit, from);
            Assert.AreEqual(made + 1, Impacts.Made);
            var mark = Impacts.LastMark;
            Assert.That(Vector3.Distance(mark.position, hit.point), Is.LessThan(0.01f), "the hole is not where the shot struck");
            Assert.That(Vector3.Angle(mark.forward, -hit.normal), Is.LessThan(1), "the hole does not lie on the wall");
            Assert.IsTrue(mark.GetComponent<MeshRenderer>().enabled);
            Assert.That(Impacts.Thrown("dust"), Is.GreaterThan(0), "no dust");
            Assert.That(Impacts.Thrown("chips"), Is.GreaterThan(0), "no chips");
            Assert.That(GunSound.Log.Single().Clip.name, Does.StartWith("stone"));
            Assert.AreEqual(Impacts.Surface.Metal, Impacts.Of(car.GetComponent<Collider>()));
            Assert.IsTrue(Physics.Raycast(new Vector3(5, 0.8f, 0), Vector3.forward, out var onCar, 10));
            Impacts.Hit(onCar, new Vector3(5, 0.8f, 0));
            Assert.That(Impacts.Thrown("sparks"), Is.GreaterThan(0), "a car gives no sparks");
            Assert.AreEqual(car.transform, Impacts.LastMark.parent, "the hole does not go with the car");
            car.transform.position += Vector3.right * 2;
            Assert.That(Vector3.Distance(Impacts.LastMark.position, onCar.point + Vector3.right * 2), Is.LessThan(0.01f), "the hole stayed when the car moved");
            Object.Destroy(car); // and when the car is gone, its marks are made again
            yield return null;
            for (int i = 0; i < Impacts.MostMarks + 5; i++) Impacts.Hit(hit, from);
            Assert.That(Impacts.Showing(), Is.LessThanOrEqualTo(Impacts.MostMarks), "too many marks");
            yield return For(2.5f);
            Assert.AreEqual(0, Impacts.Thrown("dust"), "the dust does not settle");
            Clear(ear, wall);
        }

        // A shot into someone: blood in the air, drops going on the way the shot went, blood on the wall behind and on the street below, and the sound of it.
        [UnityTest]
        public IEnumerator ABodyShotBleeds()
        {
            var ear = Ear(Vector3.zero);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.layer = WorldLayers.World;
            wall.transform.position = new Vector3(0, 1.5f, 3.2f);
            wall.transform.localScale = new Vector3(6, 3, 0.4f);
            var p = Person.Spawn(Model, new Vector3(0, 0, 2), 180);
            var b = Bystander.Give(p);
            for (int i = 0; i < 3; i++) yield return new WaitForFixedUpdate();
            Physics.SyncTransforms();
            var chest = (p.Body.GetBoneTransform(HumanBodyBones.Spine).position + p.Body.GetBoneTransform(HumanBodyBones.Neck).position) / 2;
            var from = new Vector3(chest.x, chest.y, -2);
            Assert.IsTrue(Hitbox.Cast(from, (chest - from).normalized, 10, Fighter.Hits, null, out var hit) && hit.collider.GetComponent<Hitbox>() != null, "the shot met no one");
            int marked = Impacts.Marked;
            GunSound.Log.Clear();
            Impacts.Hit(hit, from);
            Assert.That(Impacts.Thrown("blood mist"), Is.GreaterThan(0));
            Assert.That(Impacts.Thrown("blood drops"), Is.GreaterThan(0));
            Assert.AreEqual(marked + 2, Impacts.Marked, "blood behind and below");
            var marks = GameObject.Find("Impacts").GetComponentsInChildren<MeshRenderer>().Where(r => r.enabled).Select(r => r.transform.position).ToArray();
            Assert.That(marks.Any(m => Mathf.Abs(m.z - 3.0f) < 0.02f), "no blood on the wall behind: marks at " + string.Join(", ", marks.Select(m => m.ToString("F3"))) + "; struck " + hit.point.ToString("F3") + " " + hit.collider.name);
            Assert.That(marks.Any(m => Mathf.Abs(m.y) < 0.02f), "no blood on the street");
            Assert.That(GunSound.Log.Single().Clip.name, Does.StartWith("body"));
            Clear(ear, floor, wall);
        }

        // The jump goes as high as its speed says (v squared over 2g, 0.59 m), at 30 frames a second and at 60.
        [UnityTest]
        public IEnumerator TheJumpIsAsHighAtAnyFrameRate()
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            float want = OnFoot.JumpSpeed * OnFoot.JumpSpeed / (2 * OnFoot.Gravity);
            foreach (int fps in new[] { 30, 60 })
            {
                Time.captureFramerate = fps;
                var you = OnFoot.Spawn(new Vector3(0, 0.05f, 0), 0, null, null);
                yield return For(0.5f);
                float start = you.transform.position.y, top = 0;
                you.Jump();
                for (int i = 0; i < fps; i++) { yield return null; top = Mathf.Max(top, you.transform.position.y - start); }
                Debug.Log($"[Dad] at {fps} fps the jump rose {top * 100:F1} cm (v^2/2g = {want * 100:F1})");
                Assert.AreEqual(want, top, 0.03f, fps + " fps");
                Object.Destroy(you.gameObject);
                yield return null;
            }
            Clear(floor);
        }
    }
}
