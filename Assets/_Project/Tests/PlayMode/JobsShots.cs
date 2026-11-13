// Pictures of a delivery in the real city, to be looked at (Evidence/local/ui-jobs-*.png): the start screen with progress kept from before, a parcel offered in the street
// ahead, carrying it with its clock, its last seconds, the map, the light at night, and the pay. Nothing is asserted about the looks: these are for review.
using System.Collections;
using System.IO;
using Dad.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Dad.Tests
{
    public class JobsShots
    {
        string folder;

        [SetUp]
        public void Mark()
        {
            TestScenes.Mark();
            Player.Automatic = true;
            Jobs.Automatic = false;
            Menus.Title = true;
            Heat.Clear();
            Waypoint.Clear();
            folder = Path.Combine(Path.GetTempPath(), "dad-progress-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            Progress.Folder = folder;
            // what an earlier run left
            File.WriteAllText(Progress.FilePath, "{\"Version\":1,\"Cash\":1250,\"Earned\":1610,\"Deliveries\":4,\"Failed\":1}");
        }

        [UnityTearDown]
        public IEnumerator Clean()
        {
            Player.Automatic = false;
            Menus.Title = null;
            Time.timeScale = 1;
            AudioListener.pause = false;
            Heat.Clear();
            Waypoint.Clear();
            yield return TestScenes.Clean();
            Progress.Folder = null;
            Progress.Load();
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }

        static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }

        bool found;

        // A pickup on a road between near and far metres off whose foot you can see from where you stand, with the camera turned to it. found says whether one was.
        // (Building colliders do not stand as tall as the buildings, so this sees only near walls; the far view is DownTheRoad.)
        IEnumerator OfferAhead(Jobs jobs, Player you, int seed, double dropEast, double dropNorth, double near, double far)
        {
            Assert.IsTrue(jobs.Where(out double e, out double n, out _));
            var rnd = new System.Random(seed);
            var origin = FloatingOrigin.Active;
            var eye = you.Body.transform.position + Vector3.up * 1.7f;
            double pe = 0, pn = 0;
            found = false;
            for (int i = 0; i < 300 && !found; i++)
            {
                if (!CityMap.Active.RoadPoint(rnd, e, n, near, far, out pe, out pn)) break;
                origin.Offset.ToScene(pe, pn, out float x, out float z);
                if (!Physics.Raycast(new Vector3(x, 400, z), Vector3.down, out var ground, 800, WorldLayers.Ground)) continue;
                found = !Physics.Linecast(eye, ground.point + Vector3.up * 3, WorldLayers.Solid, QueryTriggerInteraction.Ignore);
            }
            if (!found) yield break;
            Assert.IsTrue(jobs.Offer(pe, pn, dropEast, dropNorth));
            var view = Camera.main.GetComponent<FootCamera>();
            view.Attach(view.Target, Mathf.Atan2((float)(pe - e), (float)(pn - n)) * Mathf.Rad2Deg);
            yield return Frames(40);
        }

        // The light as play offers it, 150 to 450 m off, seen from the road that leads to it: the camera is put on the route, back from the light along its last straight
        // stretch (up to 250 m), at a walker's eye. The HUD in the picture is still yours, where you stand.
        static IEnumerator DownTheRoad(Jobs jobs, string name)
        {
            Assert.IsTrue(jobs.Where(out double e, out double n, out _));
            var way = CityMap.Active.Route(e, n, jobs.PickEast, jobs.PickNorth);
            Assert.Greater(way.Count, 2);
            var end = way[way.Count - 1];
            Vector2 at = end, along = Vector2.zero;
            float back = 0;
            for (int i = way.Count - 2; i >= 0 && back < 250; i--)
            {
                var d = way[i + 1] - way[i];
                if (d.magnitude < 0.01f) continue;
                if (along != Vector2.zero && Vector2.Angle(along, d) > 12) break; // the stretch turns
                if (along == Vector2.zero) along = d;
                float take = Mathf.Min(d.magnitude, 250 - back);
                at = way[i + 1] - d.normalized * take;
                back += take;
            }
            var origin = FloatingOrigin.Active;
            origin.Offset.ToScene(at.x, at.y, out float cx, out float cz);
            origin.Offset.ToScene(end.x, end.y, out float lx, out float lz);
            float cy = Physics.Raycast(new Vector3(cx, 400, cz), Vector3.down, out var hit, 800, WorldLayers.Ground) ? hit.point.y : 0;
            var cam = Camera.main;
            var view = cam.GetComponent<FootCamera>();
            view.enabled = false;
            var was = (cam.transform.position, cam.transform.rotation);
            cam.transform.position = new Vector3(cx, cy + 1.7f, cz);
            cam.transform.LookAt(new Vector3(lx, cy + 18, lz));
            Debug.Log($"[Dad] JobsShots {name}: camera {back:F0} m back from the light along the road");
            yield return Frames(20);
            yield return PlayerInterfaceShots.Picture(name);
            cam.transform.SetPositionAndRotation(was.Item1, was.Item2);
            view.enabled = true;
            yield return Frames(5);
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator ADeliveryInTheCity()
        {
            SceneManager.LoadScene("Boot");
            yield return null;
            var root = Object.FindAnyObjectByType<GameRoot>();
            root.Day.MinutesPerSecond = 0;
            root.Day.Set(DayCycle.DefaultDate, 16.5, Weather.Clear);
            var menus = Object.FindAnyObjectByType<Menus>();
            var you = Object.FindAnyObjectByType<Player>();
            var jobs = Object.FindAnyObjectByType<Jobs>();
            Assert.AreEqual(1250, Progress.Current.Cash, "the earlier run's progress is read");
            float until = Time.realtimeSinceStartup + 120;
            while (you.Now == Player.Phase.Waiting || CityMap.Active == null || !GameRoot.MapBuilding.IsCompleted)
            {
                Assert.Less(Time.realtimeSinceStartup, until, "the city and its map within two minutes");
                yield return null;
            }
            yield return Frames(30);
            yield return PlayerInterfaceShots.Picture("jobs-1-start");
            menus.CloseAll();
            yield return Frames(10);

            Assert.IsTrue(jobs.Where(out double e, out double n, out _));
            Assert.IsTrue(CityMap.Active.RoadPoint(new System.Random(3), e, n, Jobs.TripNear, Jobs.TripFar, out double de, out double dn));
            yield return OfferAhead(jobs, you, 11, de, dn, 12, 160);
            Assert.IsTrue(found, "a road in sight near you");
            yield return PlayerInterfaceShots.Picture("jobs-2-offered");
            // as play offers them: 150 to 450 m off
            Assert.IsTrue(jobs.Where(out e, out n, out _));
            Assert.IsTrue(CityMap.Active.RoadPoint(new System.Random(2), e, n, Jobs.OfferNear, Jobs.OfferFar, out double fe, out double fn));
            Assert.IsTrue(jobs.Offer(fe, fn, de, dn));
            yield return Frames(10);
            yield return DownTheRoad(jobs, "jobs-2b-down-the-road");

            Assert.IsTrue(jobs.Where(out e, out n, out _));
            Assert.IsTrue(jobs.Offer(e, n, de, dn));
            while (jobs.Now != Jobs.Stage.Carrying) yield return null;
            yield return Frames(60);
            yield return PlayerInterfaceShots.Picture("jobs-3-carrying");
            jobs.Pass(jobs.Left - 14);
            yield return Frames(20);
            yield return PlayerInterfaceShots.Picture("jobs-4-last-seconds");
            menus.ShowMap();
            yield return Frames(10);
            yield return PlayerInterfaceShots.Picture("jobs-5-map");
            menus.CloseAll();
            yield return Frames(5);

            root.Day.Set(DayCycle.DefaultDate, 21, Weather.Clear);
            yield return OfferAhead(jobs, you, 11, de, dn, 12, 160);
            yield return PlayerInterfaceShots.Picture("jobs-6-night");
            Assert.IsTrue(jobs.Offer(fe, fn, de, dn));
            yield return Frames(10);
            yield return DownTheRoad(jobs, "jobs-6b-night-down-the-road");

            Assert.IsTrue(jobs.Where(out e, out n, out _));
            Assert.IsTrue(jobs.Offer(e, n, e + 3, n));
            while (jobs.Now != Jobs.Stage.Waiting) yield return null;
            yield return Frames(10);
            yield return PlayerInterfaceShots.Picture("jobs-7-delivered");
            Assert.Greater(Progress.Current.Cash, 1250);
        }
    }
}
