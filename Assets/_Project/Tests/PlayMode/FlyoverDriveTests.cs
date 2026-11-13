// The brief's "every flyover must be physically drivable": a car on the real colliders of the real city, up one ramp, along the deck and down the other, each way, on the left. The routes come from
// Tools/worldgen/paths.py. Nothing here holds the car up: it climbs by its wheels or it fails.
using System.Collections;
using System.IO;
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
    public class FlyoverDriveTests
    {
        [System.Serializable] class Route { public string name; public float[] east, north, y; }
        [System.Serializable] class Routes { public Route[] routes; }

        static Routes Load() => JsonUtility.FromJson<Routes>(File.ReadAllText(Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Tests", "Fixtures", "flyovers.json"))));

        static string[] Names()
        {
            var r = Load().routes;
            var names = new string[r.Length];
            for (int i = 0; i < r.Length; i++) names[i] = r[i].name;
            return names;
        }

        [SetUp] public void Mark() => TestScenes.Mark();

        [UnityTearDown]
        public IEnumerator Clean()
        {
            Time.timeScale = 1;
            ChunkStreamer.BuildColliders = false;
            yield return TestScenes.Clean();
        }

        [Test]
        public void ThereAreFourFlyoversEachWay() => Assert.AreEqual(8, Names().Length);

        [UnityTest]
        public IEnumerator ItCanBeDriven([ValueSource(nameof(Names))] string name)
        {
            var route = System.Array.Find(Load().routes, x => x.name == name);
            SceneManager.SetActiveScene(SceneManager.CreateScene("Fly" + System.Guid.NewGuid().ToString("N")));
            var cam = new GameObject("Camera") { tag = "MainCamera" }.AddComponent<Camera>();
            var origin = new GameObject("FloatingOrigin").AddComponent<FloatingOrigin>();
            origin.Target = cam.transform;
            origin.Recenter(route.east[0], route.north[0]);
            origin.Offset.ToScene(route.east[0], route.north[0], out float x, out float z);
            cam.transform.position = new Vector3(x, 2, z);
            ChunkStreamer.BuildColliders = true;
            var go = new GameObject("ChunkStreamer");
            go.SetActive(false);
            var streamer = go.AddComponent<ChunkStreamer>();
            streamer.Focus = cam.transform;
            streamer.Origin = origin;
            streamer.LoadRadius = 2;
            streamer.UnloadRadius = 3;
            go.SetActive(true);
            float t0 = Time.realtimeSinceStartup;
            while ((!streamer.IndexReady || !streamer.Idle || streamer.Applied == 0 || streamer.Colliders.Pending > 0) && Time.realtimeSinceStartup - t0 < 60) yield return null;
            Assert.IsTrue(streamer.Idle, "the city round the ramp came in: " + streamer.State);

            Assert.IsTrue(Physics.Raycast(new Vector3(x, 5, z), Vector3.down, out var hit, 10, WorldLayers.Ground), "a road at the foot of the ramp");
            float heading = Mathf.Atan2(route.east[1] - route.east[0], route.north[1] - route.north[0]) * Mathf.Rad2Deg;
            var car = PlayerVehicle.Spawn(VehicleKind.Sedan, hit.point + Vector3.up * 0.05f, heading, null);
            origin.Target = car.transform;
            Time.timeScale = 4;

            int last = route.east.Length - 1, at = 0;
            float highest = 0, worstHeight = 0, worstSide = 0, start = Time.fixedTime;
            while (at < last && Time.fixedTime - start < 180)
            {
                yield return new WaitForFixedUpdate();
                cam.transform.position = car.transform.position + Vector3.up * 2;
                // The nearest point on the way, and one 14 m further to steer for.
                var p = car.transform.position;
                float best = 1e9f;
                int near = at;
                for (int i = at; i <= Mathf.Min(last, at + 6); i++)
                {
                    origin.Offset.ToScene(route.east[i], route.north[i], out float rx, out float rz);
                    float d = (rx - p.x) * (rx - p.x) + (rz - p.z) * (rz - p.z);
                    if (d < best) { best = d; near = i; }
                }
                at = near;
                int aim = Mathf.Min(last, at + 2);
                origin.Offset.ToScene(route.east[aim], route.north[aim], out float ax, out float az);
                var to = new Vector3(ax - p.x, 0, az - p.z);
                var ahead = Vector3.ProjectOnPlane(car.transform.forward, Vector3.up);
                float angle = Vector3.SignedAngle(ahead, to, Vector3.up);
                float speed = car.Body.linearVelocity.magnitude;
                car.Input = new DriveInput { Throttle = speed < 12 ? 0.7 : 0, Steer = Mathf.Clamp(angle / 20f, -1f, 1f) };
                highest = Mathf.Max(highest, p.y);
                worstHeight = Mathf.Max(worstHeight, Mathf.Abs(p.y - route.y[at]));
                worstSide = Mathf.Max(worstSide, Mathf.Sqrt(best));
            }
            Debug.Log($"[Dad] {name}: {Time.fixedTime - start:0} s, up to {highest:0.0} m, height off the way {worstHeight:0.00} m, side off the way {worstSide:0.0} m");
            if (at != last)
            {
                // What stopped it: what is in front of the car, and the ground under the way from here on.
                var c = car.transform;
                Debug.Log($"[Dad] {name} stopped: speed {car.Body.linearVelocity.magnitude:0.00}, facing {c.forward}, up {c.up}");
                foreach (var h in Physics.BoxCastAll(c.position + Vector3.up, new Vector3(1, 0.6f, 0.5f), c.forward, c.rotation, 6, ~0, QueryTriggerInteraction.Ignore))
                    if (!h.collider.transform.IsChildOf(c)) Debug.Log($"[Dad]   ahead: {h.collider.name} at {h.point} {h.distance:0.00} m, normal {h.normal}");
                for (int i = Mathf.Max(0, at - 2); i <= Mathf.Min(last, at + 6); i++)
                {
                    origin.Offset.ToScene(route.east[i], route.north[i], out float gx, out float gz);
                    var hits = Physics.RaycastAll(new Vector3(gx, 30, gz), Vector3.down, 40, WorldLayers.Ground);
                    System.Array.Sort(hits, (a, b) => b.point.y.CompareTo(a.point.y));
                    var line = "";
                    foreach (var h in hits) line += $" {h.point.y:0.00}/{h.normal.y:0.00}";
                    Debug.Log($"[Dad]   point {i} way {route.y[i]:0.00} ground{line}");
                }
            }
            Assert.AreEqual(last, at, $"reached the far ramp's foot; stopped at point {at} of {last}, at {car.transform.position}");
            Assert.Greater(highest, 7f, "it went over the deck");
            Assert.Less(worstHeight, 1.5f, "it never left the road's height");
            Assert.Greater(car.transform.up.y, 0.9f, "it is on its wheels");
        }
    }
}
