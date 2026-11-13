// Every vehicle family a person can take, in the real physics engine (2026-11-06): that it stands on its wheels, goes, steers, stops and reverses, that the heavy ones behave heavier, that a bike
// stays up, and that you get in and out through the same Player as the Jetta. Each drive writes what it measured to the log, for Docs/PRODUCTION/TEST_REPORT.md. Needs Resources/Vehicles/Empty.
using System.Collections;
using System.Collections.Generic;
using Dad.Core;
using Dad.Traffic;
using Dad.Vehicles;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Dad.Tests
{
    public class TransportPlayTests
    {
        static readonly VehicleKind[] Land = { VehicleKind.Sedan, VehicleKind.Hatch, VehicleKind.Microbus, VehicleKind.Saloon, VehicleKind.Roadster, VehicleKind.Sports, VehicleKind.Motorcycle, VehicleKind.Chopper, VehicleKind.PoliceBike, VehicleKind.Bus, VehicleKind.Coach, VehicleKind.Truck, VehicleKind.Tipper };

        [SetUp] public void Mark() => TestScenes.Mark();

        [UnityTearDown]
        public IEnumerator Clean()
        {
            Time.timeScale = 1;
            yield return TestScenes.Clean();
        }

        static void Ground()
        {
            SceneManager.SetActiveScene(SceneManager.CreateScene("Transport" + System.Guid.NewGuid().ToString("N")));
            var g = new GameObject("Ground") { layer = WorldLayers.World };
            var box = g.AddComponent<BoxCollider>();
            box.size = new Vector3(3000, 1, 3000);
            g.transform.position = new Vector3(0, -0.5f, 0);
        }

        static IEnumerator Seconds(float s)
        {
            float until = Time.time + s;
            while (Time.time < until) yield return null;
        }

        struct Drive { public float Rest, Top8, StopFrom, Stop, Back, Turn, Roll; }
        static readonly Dictionary<VehicleKind, Drive> Done = new Dictionary<VehicleKind, Drive>();

        static IEnumerator Run(VehicleKind k)
        {
            Ground();
            Time.timeScale = 4;
            var v = PlayerVehicle.Spawn(k, new Vector3(0, 0.15f, -1000), 0, null);
            Assert.IsNotNull(v, k + " spawns");
            v.State.EngineOn = true;
            yield return Seconds(4);
            var d = new Drive { Rest = v.transform.position.y };
            foreach (var w in v.State.Wheels) Assert.IsTrue(w.Grounded, k + " stands on every wheel");
            Assert.Less(Vector3.Angle(v.transform.up, Vector3.up), 3, k + " stands upright");
            v.Input = new DriveInput { Throttle = 1 };
            yield return Seconds(8);
            d.Top8 = (float)v.State.Speed;
            Assert.Greater(d.Top8, 3, k + " pulls away");
            Assert.Less(Vector3.Angle(v.transform.up, Vector3.up), 6, k + " stays up under power");
            v.Input = new DriveInput { Throttle = 0.5, Steer = 0.6 };
            float yaw = v.transform.eulerAngles.y;
            yield return Seconds(4);
            d.Turn = Mathf.Abs(Mathf.DeltaAngle(yaw, v.transform.eulerAngles.y));
            d.Roll = Vector3.Angle(v.transform.up, Vector3.up);
            Assert.Greater(d.Turn, 10, k + " steers");
            Assert.Less(d.Roll, 55, k + " does not fall over in a turn");
            v.Input = new DriveInput { Throttle = 1 };
            yield return Seconds(4);
            d.StopFrom = (float)v.State.Speed;
            var from = v.transform.position;
            v.Input = new DriveInput { Brake = 1 };
            float until = Time.time + 25;
            while (Time.time < until && v.Body.linearVelocity.magnitude > 0.3f) yield return null;
            Assert.Less(v.Body.linearVelocity.magnitude, 0.4f, k + " stops");
            d.Stop = (v.transform.position - from).magnitude;
            v.Input = default;
            yield return Seconds(1);
            var at = v.transform.position;
            v.Input = new DriveInput { Brake = 1 };
            yield return Seconds(4);
            d.Back = Vector3.Dot(v.transform.position - at, -v.transform.forward);
            Done[k] = d;
            Debug.Log($"[Drive] {k}: rest y {d.Rest:F3}, {d.Top8 * 3.6f:F0} km/h after 8 s, turned {d.Turn:F0} deg in 4 s, rolled {d.Roll:F1} deg, stopped from {d.StopFrom * 3.6f:F0} km/h in {d.Stop:F1} m, reversed {d.Back:F1} m in 4 s");
            Assert.Greater(d.Back, 0.5f, k + " reverses");
        }

        [UnityTest] public IEnumerator EveryFamilyDrives([ValueSource(nameof(Land))] VehicleKind kind) { yield return Run(kind); }

        [UnityTest]
        public IEnumerator HeavyVehiclesAreSlowerAndTakeLongerToStop()
        {
            if (!Done.ContainsKey(VehicleKind.Sedan)) yield return Run(VehicleKind.Sedan);
            yield return TestScenes.Clean();
            TestScenes.Mark();
            if (!Done.ContainsKey(VehicleKind.Truck)) yield return Run(VehicleKind.Truck);
            yield return TestScenes.Clean();
            TestScenes.Mark();
            if (!Done.ContainsKey(VehicleKind.Bus)) yield return Run(VehicleKind.Bus);
            var car = Done[VehicleKind.Sedan];
            foreach (var k in new[] { VehicleKind.Truck, VehicleKind.Bus })
            {
                var h = Done[k];
                Assert.Less(h.Top8, car.Top8, k + " pulls away slower");
                Assert.Greater(h.Stop / (h.StopFrom * h.StopFrom), car.Stop / (car.StopFrom * car.StopFrom), k + " stops longer from the same speed");
            }
        }

        [UnityTest]
        public IEnumerator ABikeStaysUpAtWalkingPaceAndLeansInto()
        {
            Ground();
            Time.timeScale = 2;
            var v = PlayerVehicle.Spawn(VehicleKind.Motorcycle, new Vector3(0, 0.15f, 0), 0, null);
            v.State.EngineOn = true;
            yield return Seconds(3);
            Assert.Less(Vector3.Angle(v.transform.up, Vector3.up), 5, "stands at rest");
            v.Input = new DriveInput { Throttle = 0.2 };
            yield return Seconds(5);
            Assert.Less(Vector3.Angle(v.transform.up, Vector3.up), 6, "steady at low speed");
            v.Input = new DriveInput { Throttle = 0.7 };
            yield return Seconds(4);
            v.Input = new DriveInput { Throttle = 0.5, Steer = 0.5 };
            yield return Seconds(2);
            float lean = Vector3.Angle(v.transform.up, Vector3.up);
            Assert.Greater(lean, 4, "leans into a turn");
            Assert.Less(lean, 50, "and does not fall");
        }

        [UnityTest]
        public IEnumerator ABoatFloatsAndGoes()
        {
            Ground();
            var v = PlayerVehicle.Spawn(VehicleKind.Boat, new Vector3(0, 0.2f, 0), 0, null);
            Assert.IsNotNull(v, "the boat spawns");
            Assert.IsNotNull(v.Boat);
            v.State.EngineOn = true;
            Time.timeScale = 2;
            yield return Seconds(5);
            Assert.AreEqual(WaterView.Height - v.Boat.Spec.Draft, v.transform.position.y, 0.05, "floats at its draught");
            v.Input = new DriveInput { Throttle = 1 };
            yield return Seconds(6);
            Assert.Greater(v.State.Speed, 3, "under way");
            float yaw = v.transform.eulerAngles.y;
            v.Input = new DriveInput { Throttle = 1, Steer = 1 };
            yield return Seconds(3);
            Debug.Log($"[Drive] Boat: {v.State.Speed * 3.6:F0} km/h, turned {Mathf.DeltaAngle(yaw, v.transform.eulerAngles.y):F0} deg in 3 s, y {v.transform.position.y:F3}");
            Assert.Greater(Mathf.Abs(Mathf.DeltaAngle(yaw, v.transform.eulerAngles.y)), 15, "answers the helm");
        }

        [UnityTest]
        public IEnumerator YouGetOnAndOffEveryFamily([ValueSource(nameof(Land))] VehicleKind kind)
        {
            Ground();
            var cam = new GameObject("Cam").AddComponent<Camera>();
            cam.tag = "MainCamera";
            var host = new GameObject("You");
            var drive = host.AddComponent<TestDrive>();
            var you = host.AddComponent<Player>();
            you.Drive = drive;
            Player.Automatic = false;
            var car = PlayerVehicle.Spawn(kind, new Vector3(0, 0.15f, 0), 0, null);
            yield return Seconds(1.5f);
            var body = OnFoot.Spawn(car.DoorPoint.position + car.transform.right * 0.3f + Vector3.up * 0.1f, -90, null, cam.transform);
            you.Stand(car, body, cam.gameObject.AddComponent<FootCamera>());
            yield return null;
            Assert.IsTrue(you.TryGetIn(), kind + ": gets in");
            float until = Time.time + 8;
            while (you.Now != Player.Phase.Driving && Time.time < until) yield return null;
            Assert.AreEqual(Player.Phase.Driving, you.Now, kind + ": is at the controls");
            Assert.AreEqual(car, drive.Vehicle);
            Assert.AreEqual(car.TwoWheels, body.gameObject.activeSelf, kind + ": a rider stays in view, a driver does not");
            yield return Seconds(0.5f);
            Assert.IsTrue(you.TryGetOut(), kind + ": gets out");
            until = Time.time + 8;
            while (you.Now != Player.Phase.Walking && Time.time < until) yield return null;
            Assert.AreEqual(Player.Phase.Walking, you.Now, kind + ": is on foot");
            Assert.IsTrue(body.gameObject.activeSelf);
            Assert.IsFalse(body.Held);
            Assert.IsNull(drive.Vehicle);
            Assert.IsTrue(car.Parked);
            Player.Automatic = true;
        }

        [UnityTest]
        public IEnumerator PaintChangesWithoutANewMaterial()
        {
            Ground();
            var v = PlayerVehicle.Spawn(VehicleKind.Sedan, new Vector3(0, 0.15f, 0), 0, null);
            var r = v.GetComponentsInChildren<MeshRenderer>();
            var before = r[0].sharedMaterial;
            for (int i = 0; i < VehiclePaints.Count; i++)
            {
                v.Repaint(VehiclePaints.Plain(i));
                yield return null;
                Assert.AreEqual(i, v.Job.Base);
                Assert.AreSame(before, r[0].sharedMaterial);
            }
        }
    }
}
