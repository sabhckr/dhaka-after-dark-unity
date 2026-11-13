// The owner's rigged people in Unity (E-01, E-02, E-03, E-06): that they import as humans, that every clip is real and moves bones, that a body walks, runs, falls and stops on real
// colliders, and that getting in and out ends where the seat and the street are. Needs Resources/People: the player's model (the one of the twenty chosen, Cast) and the clips.
using System.Collections;
using System.Linq;
using Dad.Core;
using Dad.Traffic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Dad.Tests
{
    public class PeoplePlayTests
    {
        static readonly string[] Who = { OnFoot.Who };
        static readonly string[] Clips = { "idle", "walk", "run", "turn-left", "turn-right", "talk", "sit", "drive", "enter-car", "exit-car", "horn", "passenger", "fight-idle", "jab", "cross-punch", "hook-punch", "kick", "block", "head-hit", "death-back", "death-forward", "death-chest", "wave", "text-stand", "text-walk", "bag-walk", "argue" };

        [SetUp] public void Mark() => TestScenes.Mark();

        [UnityTearDown]
        public IEnumerator Clean()
        {
            Time.timeScale = 1;
            yield return TestScenes.Clean();
        }

        static void Ground()
        {
            SceneManager.SetActiveScene(SceneManager.CreateScene("People" + System.Guid.NewGuid().ToString("N")));
            var g = new GameObject("Ground") { layer = WorldLayers.World };
            var box = g.AddComponent<BoxCollider>();
            box.size = new Vector3(400, 1, 400);
            g.transform.position = new Vector3(0, -0.5f, 0);
        }

        static IEnumerator Seconds(float s)
        {
            float until = Time.time + s;
            while (Time.time < until) yield return null;
        }

        static float Lowest(Person p) => Mathf.Min(p.Body.GetBoneTransform(HumanBodyBones.LeftFoot).position.y, p.Body.GetBoneTransform(HumanBodyBones.RightFoot).position.y);

        [Test]
        public void EveryoneComesInAsAHuman()
        {
            foreach (var w in Who)
            {
                var m = Resources.Load<GameObject>(Person.Folder + w);
                Assert.That(m, Is.Not.Null, w);
                var a = m.GetComponent<Animator>();
                Assert.That(a, Is.Not.Null, w);
                Assert.That(a.avatar, Is.Not.Null, w);
                Assert.That(a.avatar.isHuman, w);
                Assert.That(a.avatar.isValid, w);
            }
        }

        [Test]
        public void EveryClipIsThereAndLong()
        {
            foreach (var c in Clips)
            {
                var clip = Resources.Load<AnimationClip>(Person.ClipFolder + c);
                Assert.That(clip, Is.Not.Null, c);
                Assert.That(clip.length, Is.GreaterThan(0.5f), c);
                Assert.That(clip.humanMotion, c);
            }
        }

        [Test]
        public void TheClipsThatRepeatRepeatAndTheDoorsDoNot()
        {
            foreach (var c in new[] { "idle", "walk", "run" }) Assert.That(Resources.Load<AnimationClip>(Person.ClipFolder + c).isLooping, c);
            foreach (var c in new[] { Player.EnterClip, Player.ExitClip }) Assert.That(Resources.Load<AnimationClip>(Person.ClipFolder + c).isLooping, Is.False, c);
        }

        [UnityTest]
        public IEnumerator EveryoneStandsDressedAndOnTheirFeet()
        {
            Ground();
            foreach (var w in Who)
            {
                var p = Person.Spawn(w, Vector3.zero, 0);
                Assert.That(p, Is.Not.Null, w);
                yield return null;
                yield return null;
                foreach (var r in p.GetComponentsInChildren<SkinnedMeshRenderer>())
                    foreach (var m in r.sharedMaterials)
                    {
                        Assert.That(m, Is.Not.Null, w + " " + r.name);
                        Assert.That(m.shader.name, Does.StartWith("Universal Render Pipeline"), w + " " + m.name);
                    }
                Assert.That(Lowest(p), Is.InRange(-0.05f, 0.25f), w + ": feet at the ground");
                var head = p.Body.GetBoneTransform(HumanBodyBones.Head).position.y;
                Assert.That(head, Is.InRange(1.3f, 1.9f), w + ": head height");
                Object.Destroy(p.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator WalkingMovesTheLegsAtWalkingPace()
        {
            Ground();
            var f = OnFoot.Spawn(new Vector3(0, 0.1f, 0), 0, null, null);
            yield return Seconds(0.5f);
            f.Override = new Vector2(0, 1);
            var start = f.transform.position;
            var foot = f.Person.Body.GetBoneTransform(HumanBodyBones.LeftFoot);
            float low = float.MaxValue, high = float.MinValue;
            float until = Time.time + 2;
            while (Time.time < until)
            {
                var y = f.transform.InverseTransformPoint(foot.position).y;
                low = Mathf.Min(low, y);
                high = Mathf.Max(high, y);
                yield return null;
            }
            float gone = (f.transform.position - start).magnitude;
            Assert.That(gone, Is.InRange(2.3f, 3.2f), "metres in 2 s");
            Assert.That(high - low, Is.GreaterThan(0.08f), "the foot lifts: the clip is playing");
        }

        [UnityTest]
        public IEnumerator RunningIsFaster()
        {
            Ground();
            var f = OnFoot.Spawn(new Vector3(0, 0.1f, 0), 0, null, null);
            yield return Seconds(0.3f);
            f.Override = new Vector2(0, 1);
            f.Run = true;
            var start = f.transform.position;
            yield return Seconds(2);
            Assert.That((f.transform.position - start).magnitude, Is.InRange(7f, 9f));
        }

        [UnityTest]
        public IEnumerator AWallStopsYou()
        {
            Ground();
            var wall = new GameObject("Wall") { layer = WorldLayers.World };
            wall.AddComponent<BoxCollider>().size = new Vector3(20, 6, 1);
            wall.transform.position = new Vector3(0, 3, 3);
            var f = OnFoot.Spawn(new Vector3(0, 0.1f, 0), 0, null, null);
            f.Override = new Vector2(0, 1);
            f.Run = true;
            yield return Seconds(2);
            Assert.That(f.transform.position.z, Is.LessThan(2.5f));
            Assert.That(f.Person.Speed, Is.LessThan(0.6f), "the legs stop as the body stops");
        }

        [UnityTest]
        public IEnumerator YouFallWhereTheGroundEnds()
        {
            Ground();
            var f = OnFoot.Spawn(new Vector3(199, 0.1f, 0), 90, null, null);
            f.Override = new Vector2(0, 1); // Forward: with no camera the body's own heading is the way.
            f.Run = true;
            yield return Seconds(1.5f);
            Assert.That(f.transform.position.y, Is.LessThan(-1));
        }

        [UnityTest]
        public IEnumerator AKerbIsSteppedUp()
        {
            Ground();
            var kerb = new GameObject("Kerb") { layer = WorldLayers.World };
            kerb.AddComponent<BoxCollider>().size = new Vector3(20, 0.15f, 10);
            kerb.transform.position = new Vector3(0, 0.075f, 7);
            var f = OnFoot.Spawn(new Vector3(0, 0.1f, 0), 0, null, null);
            f.Override = new Vector2(0, 1);
            yield return Seconds(3);
            Assert.That(f.transform.position.z, Is.GreaterThan(3));
            Assert.That(f.transform.position.y, Is.InRange(0.1f, 0.25f));
        }

        Player Ready(out PlayerVehicle car)
        {
            Ground();
            var cam = new GameObject("Camera").AddComponent<Camera>();
            cam.tag = "MainCamera";
            car = PlayerVehicle.Spawn(VehicleKind.Sedan, new Vector3(0, 0.05f, 0), 0, null);
            var body = OnFoot.Spawn(car.DoorPoint.position + car.transform.right * 1.2f + Vector3.up * 0.1f, -90, null, cam.transform);
            var root = new GameObject("Player");
            var drive = root.AddComponent<TestDrive>();
            var p = root.AddComponent<Player>();
            p.Drive = drive;
            p.Stand(car, body, cam.gameObject.AddComponent<FootCamera>());
            return p;
        }

        [UnityTest]
        public IEnumerator ADoorOutOfReachDoesNothing()
        {
            var p = Ready(out var car);
            yield return Seconds(0.3f);
            p.Body.transform.position += Vector3.right * 5;
            Physics.SyncTransforms();
            Assert.That(p.TryGetIn(), Is.False);
            Assert.That(p.Now, Is.EqualTo(Player.Phase.Walking));
        }

        [UnityTest]
        public IEnumerator GettingInEndsInTheSeatWithTheDoorShut()
        {
            var p = Ready(out var car);
            yield return Seconds(0.3f);
            var doorPart = car.GetComponentsInChildren<Transform>(true).First(t => t.name == VehicleFamilies.DriverDoor);
            var shut = doorPart.localRotation;
            Assert.That(p.TryGetIn(), "at the door");
            float widest = 0;
            Vector3 hips = Vector3.zero;
            while (p.Now == Player.Phase.GettingIn)
            {
                widest = Mathf.Max(widest, Quaternion.Angle(shut, doorPart.localRotation));
                hips = car.Chassis.InverseTransformPoint(p.Body.Person.Hips.position);
                yield return null;
            }
            Assert.That(widest, Is.GreaterThan(30), "the door opened");
            Assert.That(Quaternion.Angle(shut, doorPart.localRotation), Is.LessThan(0.5f), "the door shut");
            Assert.That((hips - p.Seat).magnitude, Is.LessThan(0.2f), "hips in the seat, at " + hips);
            Assert.That(p.Now, Is.EqualTo(Player.Phase.Driving));
            Assert.That(p.Body.gameObject.activeSelf, Is.False, "the baked driver takes over");
            Assert.That(car.State.EngineOn, Is.True);
        }

        [UnityTest]
        public IEnumerator GettingOutEndsStandingBesideTheCar()
        {
            var p = Ready(out var car);
            yield return Seconds(0.3f);
            p.TryGetIn();
            while (p.Now == Player.Phase.GettingIn) yield return null;
            yield return Seconds(0.5f);
            Assert.That(p.TryGetOut(), "stopped, with room");
            while (p.Now == Player.Phase.GettingOut) yield return null;
            Assert.That(p.Now, Is.EqualTo(Player.Phase.Walking));
            var at = car.transform.InverseTransformPoint(p.Body.transform.position);
            Assert.That(at.x, Is.GreaterThan(car.Width / 2), "on the driver's side, clear of the car");
            Assert.That(at.x, Is.LessThan(car.Width / 2 + 2));
            yield return Seconds(0.5f);
            Assert.That(p.Body.transform.position.y, Is.InRange(-0.05f, 0.1f), "on the road");
            Assert.That(car.Parked, "left parked");
            p.Body.Override = new Vector2(0, 1);
            var from = p.Body.transform.position;
            yield return Seconds(1);
            Assert.That((p.Body.transform.position - from).magnitude, Is.GreaterThan(1), "and walks away");
        }

        [UnityTest]
        public IEnumerator NoGettingOutAtSpeedOrAgainstAWall()
        {
            var p = Ready(out var car);
            yield return Seconds(0.3f);
            p.TryGetIn();
            while (p.Now == Player.Phase.GettingIn) yield return null;
            car.Body.linearVelocity = car.transform.forward * 8;
            Assert.That(p.TryGetOut(), Is.False, "moving");
            car.Body.linearVelocity = Vector3.zero;
            yield return Seconds(1);
            var wall = new GameObject("Wall") { layer = WorldLayers.World };
            wall.AddComponent<BoxCollider>().size = new Vector3(1, 6, 10);
            wall.transform.position = car.DoorPoint.position + Vector3.up * 3;
            Physics.SyncTransforms();
            Assert.That(p.TryGetOut(), Is.False, "a wall at the door");
            Assert.That(p.Now, Is.EqualTo(Player.Phase.Driving));
        }
    }
}
