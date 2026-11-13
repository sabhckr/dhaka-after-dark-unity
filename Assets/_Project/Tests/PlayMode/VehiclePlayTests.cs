// The driven vehicle in Unity (E-04): the same dynamics as VehicleDynamicsTests, now on a Rigidbody and real colliders. What is
// proved here is the join: forces reach the body, wheels find the ground, nothing passes through a wall, the world's chunks can
// be driven on, and the numbers shown are the numbers the physics used.
using System.Collections;
using System.IO;
using System.Linq;
using Dad.Core;
using Dad.Traffic;
using Dad.Vehicles;
using Dad.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Dad.Tests
{
    public class VehiclePlayTests
    {
        const double East = 4224, North = 14198;
        const float Fast = 4;
        PlayerVehicle car;

        [SetUp] public void Mark() => TestScenes.Mark();

        [UnityTearDown]
        public IEnumerator Clean()
        {
            Time.timeScale = 1;
            ChunkStreamer.BuildLanes = false;
            ChunkStreamer.BuildColliders = false;
            yield return TestScenes.Clean();
        }

        static GameObject Plane()
        {
            SceneManager.SetActiveScene(SceneManager.CreateScene("Vehicle" + System.Guid.NewGuid().ToString("N")));
            var g = new GameObject("Ground") { layer = WorldLayers.World };
            var box = g.AddComponent<BoxCollider>();
            box.size = new Vector3(6000, 1, 6000);
            g.transform.position = new Vector3(0, -0.5f, 0);
            return g;
        }

        IEnumerator OnAPlane(float heading = 0)
        {
            Plane();
            car = PlayerVehicle.Spawn(VehicleKind.Sedan, new Vector3(0, 0.05f, 0), heading, null);
            Time.timeScale = Fast;
            yield return Seconds(2);
        }

        static IEnumerator Seconds(float s)
        {
            float until = Time.fixedTime + s;
            while (Time.fixedTime < until) yield return new WaitForFixedUpdate();
        }

        [Test]
        public void TheStepIsTheOneTheCarIsTunedFor()
        {
            Assert.AreEqual(0.01f, Time.fixedDeltaTime, 1e-6f);
        }

        [UnityTest]
        public IEnumerator ItStandsOnItsWheels()
        {
            yield return OnAPlane();
            Assert.Less(car.Body.linearVelocity.magnitude, 0.05f, "at rest");
            var s = car.State;
            foreach (var w in s.Wheels)
            {
                Assert.IsTrue(w.Grounded);
                float bottom = car.transform.TransformPoint(0, (float)(w.HubY - w.Radius), 0).y;
                Assert.Less(Mathf.Abs(bottom), 0.02f, "the tyre is on the road, not in it");
            }
            float total = s.Wheels.Sum(w => (float)w.Load);
            Assert.AreEqual(car.Body.mass * 9.81f, total, car.Body.mass * 9.81f * 0.02f, "the wheels carry the car");
            Assert.Less(Vector3.Angle(car.transform.up, Vector3.up), 0.5f);
        }

        [UnityTest]
        public IEnumerator ItPerformsAsTheModelSays()
        {
            // The model alone, on a plane, against the same spec on a Rigidbody.
            var spec = HandlingSpec.Sedan;
            var model = VehicleArt.Read(VehicleKind.Sedan, Resources.Load<TextAsset>("Vehicles/Driven/Sedan").bytes, 0);
            var dyn = new VehicleDynamics(spec, model.Wheels, model.Length, model.Width, model.Height);
            var body = new TestBody(spec.Mass, new D3(dyn.PitchInertia, dyn.YawInertia, dyn.RollInertia), new D3(0, spec.CentreHeight, dyn.CentreZ));
            var ground = new Flat();
            double t = 0;
            for (int i = 0; i < 200; i++) { dyn.Step(0.01, body.Pose, default, ground, body); body.Integrate(0.01, 9.81); }
            while (dyn.State.Speed < 60 / 3.6 && t < 20) { dyn.Step(0.01, body.Pose, new DriveInput { Throttle = 1 }, ground, body); body.Integrate(0.01, 9.81); t += 0.01; }

            yield return OnAPlane();
            float start = Time.fixedTime;
            car.Input = new DriveInput { Throttle = 1 };
            while (car.State.Speed < 60 / 3.6 && Time.fixedTime - start < 20) yield return new WaitForFixedUpdate();
            float played = Time.fixedTime - start;
            Debug.Log($"[Dad] 0-60 km/h: model {t:0.00} s, on the Rigidbody {played:0.00} s");
            Assert.AreEqual(t, played, t * 0.08);
        }

        [UnityTest]
        public IEnumerator ItStopsAndSteersOnTheBody()
        {
            yield return OnAPlane();
            car.Input = new DriveInput { Throttle = 1 };
            yield return Seconds(6);
            Assert.Greater(car.Body.linearVelocity.z, 10f);
            car.Input = new DriveInput { Throttle = 0.5, Steer = 0.5 };
            yield return Seconds(1.5f);
            Assert.Greater(car.transform.eulerAngles.y, 3f, "right turns right");
            Assert.Less(car.transform.eulerAngles.y, 180f);
            // Held on at a stop the brake pedal selects reverse, so this watches for the stop.
            car.Input = new DriveInput { Brake = 1 };
            float start = Time.fixedTime;
            while (car.State.Speed > 0.3 && Time.fixedTime - start < 5) yield return new WaitForFixedUpdate();
            Debug.Log($"[Dad] stopped in {Time.fixedTime - start:0.00} s");
            Assert.Less(Time.fixedTime - start, 4f);
            Assert.IsTrue(car.State.BrakeLamps);
        }

        [UnityTest]
        public IEnumerator AWallStopsItAtSpeed()
        {
            yield return OnAPlane();
            var wall = new GameObject("Wall") { layer = WorldLayers.World };
            var box = wall.AddComponent<BoxCollider>();
            box.size = new Vector3(40, 6, 0.3f);
            wall.transform.position = new Vector3(0, 3, 60);
            car.Input = new DriveInput { Throttle = 1 };
            float most = 0, top = 0;
            for (int i = 0; i < 1200 && car.transform.position.z < 70; i++)
            {
                yield return new WaitForFixedUpdate();
                most = Mathf.Max(most, car.transform.position.z);
                top = Mathf.Max(top, car.Body.linearVelocity.z);
            }
            Debug.Log($"[Dad] into a wall at {top:0.0} m/s: nose got to {most + VehicleSpec.Of(VehicleKind.Sedan).Length / 2:0.00}, wall at 59.85");
            Assert.Greater(top, 13f, "it had speed");
            Assert.Less(most + (float)VehicleSpec.Of(VehicleKind.Sedan).Length / 2, 60f, "it did not go through");
        }

        [UnityTest]
        public IEnumerator AnOriginShiftLeavesItDriving()
        {
            yield return OnAPlane();
            var origin = new GameObject("FloatingOrigin").AddComponent<FloatingOrigin>();
            origin.Target = car.transform;
            origin.Recenter(East, North);
            car.Input = new DriveInput { Throttle = 1 };
            yield return Seconds(4);
            var v = car.Body.linearVelocity;
            var p = car.Body.position;
            origin.Recenter(East + 500, North + 500);
            yield return new WaitForFixedUpdate();
            Assert.Less((car.Body.linearVelocity - v).magnitude, 0.6f, "speed is kept");
            Assert.Less(car.Body.position.z, p.z - 490f, "it went with the world");
            foreach (var w in car.State.Wheels) Assert.IsTrue(w.Grounded, "the ground moved with it");
        }

        [UnityTest]
        public IEnumerator TheObjectTreeIsTheOneAnAuthoredModelWillFill()
        {
            yield return OnAPlane();
            var c = car.Chassis;
            foreach (var n in new[] { "Exterior", "Windows", "Interior", "Doors", "Lights", "Mirrors", "Damage", "InteractionPoints" })
                Assert.IsNotNull(c.Find(n), n);
            Assert.IsNotNull(car.Eye);
            Assert.IsNotNull(car.transform.Find("Wheels/FrontLeft/Steer/Spin/Wheel"));
            Assert.IsNotNull(car.transform.Find("Wheels/RearRight/Steer/Spin/Wheel"));
            Assert.IsNotNull(c.Find("Interior/SteeringWheel"));
        }

        [UnityTest]
        public IEnumerator WhatIsShownIsWhatTheStateSays()
        {
            yield return OnAPlane();
            var front = car.transform.Find("Wheels/FrontLeft");
            var spin = front.Find("Steer/Spin");
            car.Input = new DriveInput { Throttle = 1, Steer = 0.3 };
            yield return Seconds(2);
            yield return null;
            var s = car.State;
            Assert.AreEqual(s.Wheels[0].HubY, front.localPosition.y, 1e-3, "the hub rides on the spring");
            Assert.Greater(Mathf.Abs(Mathf.DeltaAngle(0, front.Find("Steer").localEulerAngles.y)), 2f, "the wheel steers");
            float wheelTurn = Mathf.DeltaAngle(0, car.Interior.Find("SteeringWheel").localEulerAngles.z);
            Assert.Less(wheelTurn, -5f, "steering right turns the wheel clockwise, from the driver");
            float rest = 130f, needle = Mathf.DeltaAngle(0, car.Interior.Find("SpeedNeedle").localEulerAngles.z);
            Assert.Less(needle, rest - 5, "the needle has moved off zero");
        }

        [UnityTest]
        public IEnumerator TheCameraDoesNotGoThroughAWall()
        {
            yield return OnAPlane();
            var cam = new GameObject("Camera") { tag = "MainCamera" }.AddComponent<Camera>();
            var view = cam.gameObject.AddComponent<VehicleCamera>();
            view.Attach(car);
            var wall = new GameObject("Wall") { layer = WorldLayers.World };
            wall.AddComponent<BoxCollider>().size = new Vector3(40, 8, 0.4f);
            wall.transform.position = new Vector3(0, 4, -3.4f);
            yield return Seconds(1);
            yield return null;
            Assert.Greater(cam.transform.position.z, -3.2f, "the camera comes in, in front of the wall");
            view.SetMode(VehicleCamera.View.Driver, true);
            yield return null;
            yield return null;
            Assert.Less((cam.transform.position - car.Eye.position).magnitude, 0.08f, "driver's view is at the eyes");
            Assert.IsFalse(car.Driver.gameObject.activeSelf, "the driver does not stand in their own way");
            Assert.AreEqual(VehicleCamera.DriverNear, cam.nearClipPlane, 1e-4f);
        }

        [UnityTest]
        public IEnumerator TheChaseViewHasTheWholeCarInIt()
        {
            yield return OnAPlane();
            var cam = new GameObject("Camera") { tag = "MainCamera" }.AddComponent<Camera>();
            var view = cam.gameObject.AddComponent<VehicleCamera>();
            view.Attach(car);
            yield return Seconds(2);
            yield return null;
            var size = VehicleSpec.Of(car.Kind);
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3((i & 1) == 0 ? -1 : 1, 0, (i & 2) == 0 ? -1 : 1);
                corner = new Vector3(corner.x * (float)size.Width / 2, (i & 4) == 0 ? 0.1f : (float)size.Height, corner.z * (float)size.Length / 2);
                var p = cam.WorldToViewportPoint(car.transform.TransformPoint(corner));
                Assert.Greater(p.z, 0, "in front of the camera");
                Assert.IsTrue(p.x > 0.02f && p.x < 0.98f && p.y > 0.02f && p.y < 0.98f, $"corner {corner} is at {p}, outside the picture");
            }
        }

        [UnityTest]
        public IEnumerator TheCabinIsToldWhatTheLampsAreDoing()
        {
            yield return OnAPlane();
            var block = new MaterialPropertyBlock();
            var needle = car.Interior.Find("SpeedNeedle").GetComponent<Renderer>();
            yield return null;
            needle.GetPropertyBlock(block);
            Assert.AreEqual(0f, block.GetVector("_Lamps").x, "lamps off");
            car.State.HeadLamps = true;
            yield return null;
            yield return null;
            needle.GetPropertyBlock(block);
            Assert.AreEqual(1f, block.GetVector("_Lamps").x, "the dials light with the headlamps");
            foreach (var beam in car.Chassis.Find("Lights").GetComponentsInChildren<Light>()) Assert.Greater(beam.intensity, 0, "and the beams come on");
            car.State.HeadLamps = false;
        }

        [UnityTest]
        public IEnumerator TheGlassClearsFromTheDriversSeat()
        {
            yield return OnAPlane();
            var cam = new GameObject("Camera") { tag = "MainCamera" }.AddComponent<Camera>();
            var view = cam.gameObject.AddComponent<VehicleCamera>();
            view.Attach(car);
            var glass = car.Chassis.Find("Windows/Glass").GetComponent<Renderer>();
            var block = new MaterialPropertyBlock();
            yield return null;
            glass.GetPropertyBlock(block);
            Assert.AreEqual(0f, block.GetFloat("_Clear"), "from outside");
            view.Toggle();
            yield return Seconds(VehicleCamera.BlendTime + 0.1f);
            yield return null;
            glass.GetPropertyBlock(block);
            Assert.AreEqual(1f, block.GetFloat("_Clear"), 1e-3f, "from the seat");
            view.Toggle();
            yield return Seconds(VehicleCamera.BlendTime + 0.1f);
            yield return null;
            glass.GetPropertyBlock(block);
            Assert.AreEqual(0f, block.GetFloat("_Clear"), 1e-3f, "and back");
        }

        IEnumerator Chunks(bool lanes)
        {
            OldWorld.Use();
            Assume.That(File.Exists(Path.Combine(GameRoot.WorldDir, "world.json")), "world data missing");
            SceneManager.SetActiveScene(SceneManager.CreateScene("Drive" + System.Guid.NewGuid().ToString("N")));
            var cam = new GameObject("Camera") { tag = "MainCamera" }.AddComponent<Camera>();
            var origin = new GameObject("FloatingOrigin").AddComponent<FloatingOrigin>();
            origin.Target = cam.transform;
            origin.Recenter(East, North);
            // Recentring moves everything with it, the camera too: it is still at the old place until it is put here.
            origin.Offset.ToScene(East, North, out float cx, out float cz);
            cam.transform.position = new Vector3(cx, 2, cz);
            ChunkStreamer.BuildColliders = true;
            ChunkStreamer.BuildLanes = lanes;
            var go = new GameObject("ChunkStreamer");
            go.SetActive(false);
            streamer = go.AddComponent<ChunkStreamer>();
            streamer.Focus = cam.transform;
            streamer.Origin = origin;
            OldWorld.Limit(streamer);
            streamer.LoadRadius = 1;
            streamer.UnloadRadius = 2;
            go.SetActive(true);
            float t0 = Time.realtimeSinceStartup;
            // Counted in the scene: the streamer's warm-up chunk gets a collider too, and goes.
            while ((streamer.Resident.Count < 9 || streamer.Colliders.Pending > 0 || Object.FindObjectsByType<MeshCollider>(FindObjectsSortMode.None).Length < 9) && Time.realtimeSinceStartup - t0 < 120) yield return null;
            Assert.AreEqual(9, Object.FindObjectsByType<MeshCollider>(FindObjectsSortMode.None).Length);
            Assert.AreEqual(0, streamer.Colliders.Failed);
        }

        ChunkStreamer streamer;

        [UnityTest]
        public IEnumerator AChunkCanBeDrivenOn()
        {
            yield return Chunks(true);
            // A place on a lane: the ray finds the road as drawn, and that is what the wheels will use.
            var origin = FloatingOrigin.Active;
            var chunk = ChunkLanes.Build(Dadc.Load(File.ReadAllBytes(Path.Combine(GameRoot.WorldDir, "chunks", origin.Offset.Cell.Id + ".dadc"))));
            var lane = chunk.Lanes.First(l => !l.Connector && l.Length > 60);
            int m = System.Math.Max(0, lane.Pts.Length / 4 - 1);
            double ox = chunk.CornerEastCm / 100.0, oz = chunk.CornerNorthCm / 100.0;
            origin.Offset.ToScene(ox + lane.Pts[2 * m], oz + lane.Pts[2 * m + 1], out float x, out float z);
            Assert.IsTrue(Physics.Raycast(new Vector3(x, 3, z), Vector3.down, out var hit, 6, WorldLayers.Ground));
            Assert.AreEqual(lane.Y, hit.point.y, 0.08f, "the road, where the lane says");
            float heading = Mathf.Atan2((float)(lane.Pts[2 * m + 2] - lane.Pts[2 * m]), (float)(lane.Pts[2 * m + 3] - lane.Pts[2 * m + 1])) * Mathf.Rad2Deg;
            car = PlayerVehicle.Spawn(VehicleKind.Sedan, hit.point + Vector3.up * 0.05f, heading, null);
            Time.timeScale = Fast;
            yield return Seconds(2);
            foreach (var w in car.State.Wheels) Assert.IsTrue(w.Grounded);
            Assert.Less(car.Body.linearVelocity.magnitude, 0.1f);
            Assert.AreEqual(hit.point.y, car.transform.position.y, 0.1f);
            // Held still long enough for the CPU copies of the drawn meshes to go, and the collider is still there.
            yield return Seconds(1);
            Assert.IsTrue(Physics.Raycast(new Vector3(x, 3, z), Vector3.down, out hit, 6, WorldLayers.Ground));
        }

        // Open ground lies 0.28 m under a road (the lanes' height), so that step is what a driver comes back over.
        IEnumerator AtAStep(float height, float speed)
        {
            yield return OnAPlane();
            var step = new GameObject("Step") { layer = WorldLayers.World };
            step.AddComponent<BoxCollider>().size = new Vector3(40, height, 40);
            step.transform.position = new Vector3(0, height / 2, 32);
            car.gameObject.AddComponent<Hits>();
            yield return Seconds(0.2f);
            car.Body.linearVelocity = new Vector3(0, 0, speed);
            car.Input = new DriveInput { Throttle = 0.5 };
            yield return Seconds(4);
            Debug.Log($"[Dad] a {height:0.00} m step at {speed} m/s: z {car.transform.position.z:0.0}, height {car.transform.position.y:0.00}, up {car.transform.up.y:0.00}, first hit {car.GetComponent<Hits>().First}");
        }

        [UnityTest]
        public IEnumerator ItComesBackOntoTheRoadFromTheGround()
        {
            yield return AtAStep(0.28f, 6);
            Assert.Greater(car.transform.position.z, 14f, "over the step");
            Assert.AreEqual(0.28f, car.transform.position.y, 0.1f, "up on the road");
            Assert.Greater(car.transform.up.y, 0.97f);
        }

        [UnityTest]
        public IEnumerator ASidewalkEdgeIsTooHighToDriveUp()
        {
            // From open ground a sidewalk is 0.43 m up: higher than the floor of the car, so it stops the car as it would.
            yield return AtAStep(0.43f, 6);
            Assert.Less(car.transform.position.z + (float)VehicleSpec.Of(VehicleKind.Sedan).Length / 2, 12.5f);
            Assert.Greater(car.transform.up.y, 0.9f, "it is still on its wheels");
        }

        // Off the road and back: what a driver meets at the edge of a road, on real geometry, with what stops the body logged.
        [UnityTest]
        public IEnumerator TheEdgeOfTheRoad()
        {
            yield return Chunks(true);
            var origin = FloatingOrigin.Active;
            var chunk = ChunkLanes.Build(Dadc.Load(File.ReadAllBytes(Path.Combine(GameRoot.WorldDir, "chunks", origin.Offset.Cell.Id + ".dadc"))));
            double ox = chunk.CornerEastCm / 100.0, oz = chunk.CornerNorthCm / 100.0;
            int tried = 0, stuck = 0;
            foreach (var lane in chunk.Lanes.Where(l => !l.Connector && l.Length > 40).Take(6))
            {
                int m = System.Math.Max(0, lane.Pts.Length / 4 - 1);
                origin.Offset.ToScene(ox + lane.Pts[2 * m], oz + lane.Pts[2 * m + 1], out float x, out float z);
                var along = new Vector3((float)(lane.Pts[2 * m + 2] - lane.Pts[2 * m]), 0, (float)(lane.Pts[2 * m + 3] - lane.Pts[2 * m + 1])).normalized;
                var left = new Vector3(-along.z, 0, along.x);
                string line = "";
                for (float d = 0; d <= 16; d += 2)
                {
                    var p = new Vector3(x, 3, z) + left * d;
                    line += Physics.Raycast(p, Vector3.down, out var h, 6, WorldLayers.Ground) ? $" {h.point.y:0.00}" : " -";
                }
                Debug.Log($"[Dad] road edge, lane {lane.Id}, surface every 2 m to the left:{line}");
                var at = new Vector3(x, lane.Y + 0.05f, z);
                var heading = (along + left).normalized;
                car = PlayerVehicle.Spawn(VehicleKind.Sedan, at, Quaternion.LookRotation(heading).eulerAngles.y, null);
                var hits = car.gameObject.AddComponent<Hits>();
                Time.timeScale = Fast;
                yield return Seconds(0.4f);
                car.Input = new DriveInput { Throttle = 0.6 };
                yield return Seconds(5);
                float gone = (car.transform.position - at).magnitude;
                tried++;
                if (car.State.Speed < 1) stuck++;
                Debug.Log($"[Dad] road edge, lane {lane.Id}: went {gone:0.0} m, speed {car.State.Speed:0.0}, height {car.transform.position.y:0.00}, first hit {hits.First}");
                // A solid thing a few metres off the lane stops a car that drives straight at it, and that is right: lane 6 has a pier there. Anything else must let it go.
                if (!hits.Wall) Assert.Greater(gone, 5, "it left the lane");
                Assert.Greater(car.transform.position.y, -0.2f, "it is on the ground, not under it");
                Assert.Greater(Vector3.Dot(car.transform.up, Vector3.up), 0.8f, "it is on its wheels");
                Object.Destroy(car.gameObject);
                yield return null;
            }
            Debug.Log($"[Dad] road edge: {tried} tries, {stuck} stopped");
            Assert.Greater(tried, 0);
        }

        [UnityTest]
        public IEnumerator ABuildingStopsItAtSpeed()
        {
            yield return Chunks(false);
            var origin = FloatingOrigin.Active;
            var c = Dadc.Load(File.ReadAllBytes(Path.Combine(GameRoot.WorldDir, "chunks", origin.Offset.Cell.Id + ".dadc")));
            double ox = origin.Offset.Cell.CornerEast, oz = origin.Offset.Cell.CornerNorth;
            // The middle of a wall with a clear run up to it for the body of the car, whatever the wheels cross on the way.
            const float Run = 16;
            bool found = false;
            Vector3 wall = default, outward = default, start = default;
            foreach (var b in c.Buildings)
            {
                if (b.Outer == null || b.Outer.Length < 8 || found) continue;
                double cx = 0, cz = 0;
                int n = b.Outer.Length / 2;
                for (int k = 0; k < n; k++) { cx += b.Outer[2 * k] / 100.0; cz += b.Outer[2 * k + 1] / 100.0; }
                cx /= n;
                cz /= n;
                for (int k = 0; k + 3 < b.Outer.Length && !found; k += 2)
                {
                    double mx = (b.Outer[k] + b.Outer[k + 2]) / 200.0, mz = (b.Outer[k + 1] + b.Outer[k + 3]) / 200.0;
                    origin.Offset.ToScene(ox + mx, oz + mz, out float x, out float z);
                    var at = new Vector3(x, 1.2f, z);
                    var away = new Vector3((float)(mx - cx), 0, (float)(mz - cz)).normalized;
                    if (!Physics.Raycast(at + away * 1.5f, -away, out var face, 2.5f, WorldLayers.Ground)) continue;
                    var from = at + face.normal * Run;
                    if (!Physics.Raycast(from + Vector3.up * 3, Vector3.down, out var under, 6, WorldLayers.Ground) || under.normal.y < 0.9f) continue;
                    var body = new Vector3(from.x, under.point.y + 1, from.z);
                    if (!Physics.BoxCast(body, new Vector3(0.95f, 0.45f, 0.1f), -face.normal, out var first, Quaternion.LookRotation(-face.normal), Run + 2, WorldLayers.Ground)) continue;
                    if (Mathf.Abs(first.distance - Run) > 0.5f) continue;
                    wall = at;
                    outward = face.normal;
                    start = under.point;
                    found = true;
                }
            }
            Assert.IsTrue(found, "no wall in this chunk with a clear run up");
            car = PlayerVehicle.Spawn(VehicleKind.Sedan, start + Vector3.up * 0.05f, Quaternion.LookRotation(-outward).eulerAngles.y, null);
            var hits = car.gameObject.AddComponent<Hits>();
            Time.timeScale = Fast;
            yield return Seconds(0.5f);
            car.Body.linearVelocity = -outward * 25;
            car.Input = new DriveInput { Throttle = 1 };
            float least = float.MaxValue;
            for (int i = 0; i < 300; i++)
            {
                yield return new WaitForFixedUpdate();
                least = Mathf.Min(least, Vector3.Dot(car.transform.position - wall, outward));
            }
            float half = (float)VehicleSpec.Of(VehicleKind.Sedan).Length / 2;
            Debug.Log($"[Dad] into a building at 25 m/s from {Run} m: centre got to {least:0.00} m from the wall, half a car is {half:0.00}; first hit {hits.First}");
            Assert.Less(least, half + 1, "it reached the wall");
            Assert.Greater(least, half - 0.5f, "it did not go through");
        }

        // What the car first touched, for the log.
        sealed class Hits : MonoBehaviour
        {
            public string First = "nothing";
            public bool Wall;
            void OnCollisionEnter(Collision c)
            {
                if (First != "nothing") return;
                var p = c.GetContact(0);
                Wall = Mathf.Abs(p.normal.y) < 0.3f;
                First = $"{c.collider.name} at height {p.point.y:0.00}, normal {p.normal.x:0.00} {p.normal.y:0.00} {p.normal.z:0.00}, impact {c.relativeVelocity.magnitude:0.0} m/s";
            }
        }

        [UnityTest]
        public IEnumerator AChunkGoingTakesItsColliderWithIt()
        {
            yield return Chunks(false);
            var colliders = Object.FindObjectsByType<MeshCollider>(FindObjectsSortMode.None);
            Assert.GreaterOrEqual(colliders.Length, 9);
            Object.Destroy(streamer.gameObject);
            yield return null;
            yield return null;
            Assert.AreEqual(0, Object.FindObjectsByType<MeshCollider>(FindObjectsSortMode.None).Length);
        }

        sealed class Flat : IGroundProbe
        {
            public bool Cast(D3 from, D3 direction, double max, out ContactHit hit)
            {
                hit = default;
                if (direction.Y >= -1e-6) return false;
                double d = -from.Y / direction.Y;
                if (d < 0 || d > max) return false;
                hit = new ContactHit { Distance = d, Normal = new D3(0, 1, 0), Grip = 1 };
                return true;
            }
        }
    }
}
