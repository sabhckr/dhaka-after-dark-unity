// You, in the real city, as plain play starts it (E-01, E-02, E-03, E-06): one person, the first of the twenty (Cast; batch runs keep no choice), beside the Jetta on the nearest lane, who gets in, gets out and walks on the chunk colliders.
// PeoplePlayTests has the same moves on a plain floor. This one goes through GameRoot, and writes a picture at each step to Evidence/local/player-*.png to be looked at.
using System.Collections;
using System.IO;
using Dad.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Dad.Tests
{
    public class PlayerInCityTests
    {
        const int Width = 1280, Height = 720;

        [SetUp] public void Mark() { TestScenes.Mark(); Player.Automatic = true; }

        [UnityTearDown]
        public IEnumerator Clean()
        {
            Player.Automatic = false;
            yield return TestScenes.Clean();
        }

        static IEnumerator Seconds(float s)
        {
            float until = Time.time + s;
            while (Time.time < until) yield return null;
        }

        // The first of right, ahead, left and back (as the controls say them, from the camera) with room for his capsule for 4 m.
        static Vector2 Open(Vector3 at)
        {
            var view = Camera.main.transform;
            foreach (var c in new[] { new Vector2(1, 0), new Vector2(0, 1), new Vector2(-1, 0), new Vector2(0, -1) })
            {
                var way = view.right * c.x + view.forward * c.y;
                way.y = 0;
                way.Normalize();
                if (!Physics.CapsuleCast(at + Vector3.up * 0.6f, at + Vector3.up * 1.4f, OnFoot.Radius, way, 4, WorldLayers.Solid | 1 << WorldLayers.Vehicle, QueryTriggerInteraction.Ignore)) return c;
            }
            Assert.Fail("no way open on foot from " + at);
            return default;
        }

        static void Picture(string name)
        {
            var cam = Camera.main;
            var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var read = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            cam.targetTexture = rt;
            // The city, the parked vehicles and the trains are drawn instanced in LateUpdate, for the frame's cameras; a camera rendered by hand needs them again.
            CityView.Active?.Submit();
            foreach (var p in Object.FindObjectsByType<ParkedView>(FindObjectsSortMode.None)) p.Submit();
            foreach (var t in Object.FindObjectsByType<TrainView>(FindObjectsSortMode.None)) t.Submit();
            cam.Render();
            cam.targetTexture = null;
            RenderTexture.active = rt;
            read.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            RenderTexture.active = null;
            Directory.CreateDirectory("Evidence/local");
            File.WriteAllBytes($"Evidence/local/player-{name}.png", read.EncodeToPNG());
            Object.Destroy(rt);
            Object.Destroy(read);
        }

        [UnityTest]
        public IEnumerator PlainPlayIsOneManWhoWalksDrivesAndGetsOut()
        {
            SceneManager.LoadScene("Boot");
            yield return null;
            var root = Object.FindAnyObjectByType<GameRoot>();
            root.Day.MinutesPerSecond = 0;
            root.Day.Set(DayCycle.DefaultDate, 11, Weather.Clear);
            var you = Object.FindAnyObjectByType<Player>();
            Assert.That(you, Is.Not.Null, "plain play makes the player");
            float until = Time.realtimeSinceStartup + 120;
            while (you.Now == Player.Phase.Waiting)
            {
                Assert.Less(Time.realtimeSinceStartup, until, "standing in the street within two minutes");
                yield return null;
            }
            yield return Seconds(4);
            Assert.AreEqual(1, Object.FindObjectsByType<OnFoot>(FindObjectsSortMode.None).Length, "one body");
            Assert.AreEqual(1, Object.FindObjectsByType<Player>(FindObjectsSortMode.None).Length, "one player");
            Assert.AreEqual(OnFoot.Who, you.Body.Person.Who);
            Assert.AreEqual(Cast.First, OnFoot.Who, "batch runs keep no progress: the first of the twenty");
            var view = Camera.main.GetComponent<FootCamera>();
            Assert.That(view != null && view.enabled, "the camera is behind him");
            var feet = you.Body.Person.Body.GetBoneTransform(HumanBodyBones.LeftFoot).position.y - you.Body.transform.position.y;
            Assert.That(feet, Is.InRange(-0.05f, 0.25f), "feet at the ground");
            Picture("1-standing");

            Assert.IsTrue(you.TryGetIn(), $"he starts beside the door: body {you.Body.transform.position}, door {you.Car.DoorPoint.position}, car {you.Car.transform.position}");
            yield return Seconds(1.5f);
            Picture("2-getting-in");
            until = Time.realtimeSinceStartup + 30;
            while (you.Now != Player.Phase.Driving)
            {
                Assert.Less(Time.realtimeSinceStartup, until, "seated within 30 s");
                yield return null;
            }
            var drive = root.GetComponent<TestDrive>();
            Assert.AreSame(you.Car, drive.Vehicle, "the car's controls are his");
            Assert.AreEqual(InputContext.Driving, root.Controls.Context);
            Assert.IsTrue(you.Car.State.EngineOn, "the engine runs");
            you.Car.Input = new Dad.Vehicles.DriveInput { Throttle = 0.4f };
            drive.enabled = false; // Its Update would put the keys' nothing back into Input.
            var from = you.Car.transform.position;
            yield return Seconds(2.5f);
            Assert.Greater(Vector3.Distance(from, you.Car.transform.position), 0.5f, "the car goes");
            Picture("3-driving");
            you.Car.Input = new Dad.Vehicles.DriveInput { Brake = 1, Handbrake = true };
            until = Time.realtimeSinceStartup + 30;
            while (you.Car.Body.linearVelocity.magnitude > 0.5f)
            {
                Assert.Less(Time.realtimeSinceStartup, until, "stopped within 30 s");
                yield return null;
            }
            drive.enabled = true;

            Assert.IsTrue(you.TryGetOut(), "he can get out of a stopped car");
            yield return Seconds(1.5f);
            Picture("4-getting-out");
            until = Time.realtimeSinceStartup + 30;
            while (you.Now != Player.Phase.Walking)
            {
                Assert.Less(Time.realtimeSinceStartup, until, "standing within 30 s");
                yield return null;
            }
            Assert.AreEqual(InputContext.OnFoot, root.Controls.Context);
            Assert.That(you.Body.gameObject.activeInHierarchy, "he is back");
            yield return Seconds(1);

            // Away from the car, whichever way is open. On the city of 2026-11-07 he gets out 0.3 m from the corner of an apartment block, to his right: walking into it,
            // he stopped against its wall, as he should.
            var start = you.Body.transform.position;
            you.Body.Override = Open(you.Body.transform.position);
            yield return Seconds(3);
            Picture("5-walking");
            var walked = you.Body.transform.position - start;
            walked.y = 0;
            Assert.Greater(walked.magnitude, 2.5f, "walking moves him");
            you.Body.Run = true;
            yield return Seconds(2);
            Picture("6-running");
            var ran = you.Body.transform.position - start;
            ran.y = 0;
            Assert.Greater(ran.magnitude, walked.magnitude + 5, "running is faster");
            you.Body.Override = Vector2.zero;
            you.Body.Run = false;
            yield return Seconds(1);
            Assert.That(Physics.Raycast(you.Body.transform.position + Vector3.up, Vector3.down, out var ground, 3, WorldLayers.Ground), "ground under him");
            Assert.That(you.Body.transform.position.y - ground.point.y, Is.InRange(-0.1f, 0.2f), "on the ground");
        }
    }
}
