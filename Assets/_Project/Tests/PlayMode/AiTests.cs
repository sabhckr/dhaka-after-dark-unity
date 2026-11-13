// How armed people fight (2026-11-12, Docs/Characters/SHOOTER_BAR_AUDIT.md section 11): the way round a wall (Navigation), cover taken and fired from with reloads in it
// (Cover, Fighter), the police moving up from cover to cover, stepping side to side in the open, and the gang on their turf: calm and unarmed, guns out at a shot, away
// again after a quiet while (Turf). The last test goes through plain play in the real city.
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Dad.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Dad.Tests
{
    public class AiTests
    {
        static NpcRecord Record(string id) => NpcRoster.Load("People/fighters").people.First(p => p.id == id);
        readonly List<Object> made = new List<Object>();

        [SetUp] public void Begin() { TestScenes.Mark(); Heat.Clear(); }

        [UnityTearDown]
        public IEnumerator End()
        {
            Time.captureFramerate = 0;
            Fighter.Quiet = 30;
            Turf.Automatic = false;
            foreach (var f in Object.FindObjectsByType<Fighter>(FindObjectsSortMode.None)) Object.Destroy(f.gameObject);
            foreach (var r in Object.FindObjectsByType<Rigidbody>(FindObjectsSortMode.None)) Object.Destroy(r.gameObject); // dropped guns
            foreach (var o in made) if (o != null) Object.Destroy(o);
            made.Clear();
            Navigation.Forget();
            Cover.Clear();
            Heat.Clear();
            yield return null;
            yield return TestScenes.Clean();
        }

        GameObject Floor()
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.transform.localScale = new Vector3(12, 1, 12);
            made.Add(floor);
            return floor;
        }

        GameObject Wall(Vector3 at, Vector3 size)
        {
            var w = GameObject.CreatePrimitive(PrimitiveType.Cube);
            w.name = "wall";
            w.layer = WorldLayers.World;
            w.transform.position = at + Vector3.up * size.y / 2;
            w.transform.localScale = size;
            made.Add(w);
            return w;
        }

        // A picture of the gang on their turf in the city (Evidence/local/cover), from a camera beside them: to be looked at.
        static void CityPicture(string name, Turf turf)
        {
            var cam = new GameObject("Turf camera").AddComponent<Camera>();
            var main = Camera.main;
            if (main != null) { cam.CopyFrom(main); cam.tag = "Untagged"; }
            var middle = turf.Gang.Aggregate(Vector3.zero, (a, f) => a + f.transform.position) / turf.Gang.Count;
            var side = Vector3.Cross(Vector3.up, turf.Gang[0].transform.position - middle).normalized;
            cam.transform.position = middle + side * 5 + Vector3.up * 1.8f - (turf.Gang[0].transform.position - middle).normalized * 2;
            cam.transform.LookAt(middle + Vector3.up * 1.0f);
            cam.fieldOfView = 45;
            cam.nearClipPlane = 0.1f;
            var rt = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var read = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            cam.targetTexture = rt;
            CityView.Active?.Submit(); // the city is drawn instanced for the frame's cameras: a camera rendered by hand needs it again
            foreach (var p in Object.FindObjectsByType<ParkedView>(FindObjectsSortMode.None)) p.Submit();
            cam.Render();
            RenderTexture.active = rt;
            read.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
            RenderTexture.active = null;
            System.IO.Directory.CreateDirectory("Evidence/local/cover");
            System.IO.File.WriteAllBytes($"Evidence/local/cover/{name}.jpg", read.EncodeToJPG(92));
            cam.targetTexture = null;
            Object.Destroy(rt); Object.Destroy(read); Object.Destroy(cam.gameObject);
        }

        // The highest point of someone as posed now: the skinned meshes baked.
        static readonly Mesh baked = new Mesh();
        static Vector3 Crown(Fighter f)
        {
            var top = new Vector3(0, float.MinValue, 0);
            foreach (var r in f.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (!r.gameObject.activeInHierarchy) continue;
                r.BakeMesh(baked, true);
                foreach (var v in baked.vertices) { var p = r.transform.TransformPoint(v); if (p.y > top.y) top = p; }
            }
            return top;
        }

        static IEnumerator For(float seconds)
        {
            float until = Time.time + seconds;
            while (Time.time < until) yield return null;
        }

        static IEnumerator Built(Vector3 at)
        {
            Navigation.Need(at);
            float limit = Time.realtimeSinceStartup + 20;
            while (!Navigation.Ready(at) && Time.realtimeSinceStartup < limit) yield return null;
            Assert.IsTrue(Navigation.Ready(at), "no walking mesh within 20 s");
            Debug.Log($"[Dad] walking mesh: {Navigation.LastBuildMs:F0} ms from {Navigation.LastSources} sources");
        }

        // The way from one side of a wall to the other goes round its end, and no stretch of it passes through the wall.
        [UnityTest]
        public IEnumerator TheWayGoesRoundAWall()
        {
            Floor();
            Wall(new Vector3(-3, 0, 5), new Vector3(14, 3, 0.4f));
            yield return null;
            Physics.SyncTransforms();
            yield return Built(Vector3.zero);
            var corners = new List<Vector3>();
            var from = new Vector3(0, 0, 0);
            var to = new Vector3(0, 0, 10);
            Assert.IsTrue(Navigation.Route(from, to, corners), "no way found");
            float walk = Navigation.Length(from, corners);
            Debug.Log($"[Dad] round the wall: {corners.Count} corners, {walk:F1} m against 10 m straight: " + string.Join(" ", corners.Select(c => c.ToString("F1"))));
            Assert.That(corners.Count, Is.GreaterThanOrEqualTo(2), "went straight");
            Assert.That(walk, Is.GreaterThan(13), "too short to have gone round"); // round the end at x 4: two legs of about 6.4 m
            var a = from;
            foreach (var c in corners)
            {
                Assert.IsFalse(Physics.Linecast(a + Vector3.up, c + Vector3.up, WorldLayers.Solid), $"the way from {a} to {c} goes through the wall");
                a = c;
            }
            Assert.That(Vector3.Distance(corners[corners.Count - 1], to), Is.LessThan(0.5f), "it does not end where asked");
        }

        // A gang member in the open, shot at from beyond a low wall: runs to the wall, crouches behind it hidden, comes up over it to fire, and reloads behind it.
        [UnityTest]
        public IEnumerator CoverIsTakenAndFiredFrom()
        {
            Random.InitState(5);
            Time.captureFramerate = 30;
            Floor();
            Wall(new Vector3(0, 0, 9), new Vector3(4, 1.3f, 0.4f));
            yield return null;
            Physics.SyncTransforms();
            var gang = Fighter.Spawn(Record("GANG_002"), Fighter.Side.Gang, new Vector3(1, 0, 14), 180); // a rifle
            var police = Fighter.Spawn(Record("POLICE_001"), Fighter.Side.Police, new Vector3(0, 0, -10), 0);
            police.HoldFire = true;
            police.Stay = true; // stands in the open, a mark: the gang member is what is measured
            police.Health = 1e6f;
            float until = Time.time + 40;
            var shown = new List<string>();
            float hiddenHead = 0, peekHead = 0;
            int hiddenSeen = 0, hiddenBlocked = 0;
            var reloadsIn = new List<Fighter.Mode>();
            bool wasReloading = false;
            float downSince = -1;
            while (Time.time < until && (gang.Reloads < 1 || gang.Reloading || gang.Peeks < 2)) // a magazine is 20: a reload comes after a few turns, and is watched to its end
            {
                yield return null;
                var head = gang.Body.Body.GetBoneTransform(HumanBodyBones.Head).position;
                var eye = police.Body.Body.GetBoneTransform(HumanBodyBones.Head).position;
                if (gang.Now == Fighter.Mode.Hiding && downSince > 0 && Time.time - downSince > 0.5f) head = Crown(gang); // down: the very top of the head, from the mesh as posed
                if (gang.Now != Fighter.Mode.Hiding) downSince = -1;
                else if (downSince < 0) downSince = Time.time;
                if (gang.Now == Fighter.Mode.Hiding && gang.Body.Speed < 0.05f && Time.time - downSince > 0.5f) // once down: ducking from standing takes about half a second (the stance and the slowing walk blend out)
                {
                    hiddenSeen++;
                    if (Physics.Linecast(eye, head, WorldLayers.Solid)) hiddenBlocked++;
                    else if (shown.Count < 6) shown.Add($"crown {head:F2} body {gang.transform.position:F2} facing {gang.transform.eulerAngles.y:F0} reloading {gang.Reloading} upper {gang.Body.UpperWeight:F2}");
                    hiddenHead = head.y;
                }
                if (gang.Now == Fighter.Mode.Peeking) peekHead = Mathf.Max(peekHead, head.y);
                if (gang.Reloading && !wasReloading) reloadsIn.Add(gang.Now);
                wasReloading = gang.Reloading;
            }
            string tally = $"covers {gang.Covers}, peeks {gang.Peeks}, fired {gang.Fired}, reloads {gang.Reloads} (in {string.Join(", ", reloadsIn)}), spot {gang.Spot.At:F2} low {gang.Spot.Low}, top of the head down {hiddenHead:F2} m, peeking {peekHead:F2} m, hidden frames {hiddenBlocked}/{hiddenSeen} (shown: {string.Join("; ", shown)}), cover tried {Cover.Tried} good {Cover.Good}, a search at most {Cover.MostMs:F2} ms";
            Debug.Log("[Dad] cover: " + tally);
            Assert.That(gang.Covers, Is.GreaterThanOrEqualTo(1), "never took cover: " + tally);
            Assert.That(gang.Spot.At.z, Is.InRange(9.2f, 10.2f), "the cover taken is not behind the wall: " + tally);
            Assert.That(Mathf.Abs(gang.Spot.At.x), Is.LessThan(2.2f), "the cover taken is not behind the wall: " + tally);
            Assert.IsTrue(gang.Spot.Low, "a low wall is fired over: " + tally);
            Assert.That(hiddenSeen, Is.GreaterThan(10), "never seen down in cover: " + tally);
            Assert.AreEqual(hiddenSeen, hiddenBlocked, "the top of the head shows over the wall while down: " + tally);
            Assert.That(peekHead - hiddenHead, Is.GreaterThan(0.3f), "does not come up to fire: " + tally);
            Assert.That(gang.Peeks, Is.GreaterThanOrEqualTo(2), "never came up to fire: " + tally);
            Assert.That(gang.Fired, Is.GreaterThan(0), "never fired from cover: " + tally);
            Assert.That(gang.Reloads, Is.GreaterThanOrEqualTo(1), "never reloaded: " + tally);
            Assert.That(reloadsIn, Has.All.Matches<Fighter.Mode>(m => m == Fighter.Mode.Hiding || m == Fighter.Mode.Rushing), "reloaded out of cover: " + tally);
        }

        // An officer 42 m from a gang member, with low walls between every 10 m: takes cover, then moves up from wall to wall to within 25 m, firing on the way.
        [UnityTest]
        public IEnumerator PoliceMoveUpFromCoverToCover()
        {
            Random.InitState(9);
            Time.captureFramerate = 30;
            Floor();
            foreach (float z in new[] { -10f, -20f, -30f }) Wall(new Vector3(0, 0, z), new Vector3(5, 1.3f, 0.4f));
            yield return null;
            Physics.SyncTransforms();
            var gang = Fighter.Spawn(Record("GANG_002"), Fighter.Side.Gang, new Vector3(0, 0, 0), 180); // 1.85 m: seen over the walls between
            gang.HoldFire = true;
            gang.Stay = true; // stands in the open: the officer is what is measured
            gang.Health = 1e6f;
            var police = Fighter.Spawn(Record("POLICE_002"), Fighter.Side.Police, new Vector3(0.5f, 0, -42), 0);
            float until = Time.time + 60;
            var spots = new List<float>();
            while (Time.time < until && !(police.MovesUp >= 1 && police.Now == Fighter.Mode.Hiding && Mathf.Abs(police.transform.position.z) < Fighter.CloseIn))
            {
                yield return null;
                if (police.Now == Fighter.Mode.Hiding && (spots.Count == 0 || Mathf.Abs(spots[spots.Count - 1] - police.Spot.At.z) > 1)) spots.Add(police.Spot.At.z);
            }
            float away = Mathf.Abs(police.transform.position.z);
            string tally = $"covers {police.Covers}, moved up {police.MovesUp}, peeks {police.Peeks}, fired {police.Fired}, now {police.Now} at {away:F1} m, cover held at z {string.Join(", ", spots.Select(z => z.ToString("F1")))}";
            Debug.Log("[Dad] moving up: " + tally);
            Assert.That(police.Covers, Is.GreaterThanOrEqualTo(2), "did not go from cover to cover: " + tally);
            Assert.That(police.MovesUp, Is.GreaterThanOrEqualTo(1), "never moved up: " + tally);
            Assert.That(away, Is.LessThan(Fighter.CloseIn), "did not close in: " + tally);
            Assert.AreEqual(Fighter.Mode.Hiding, police.Now, "not in cover at the end: " + tally);
            Assert.That(police.Fired, Is.GreaterThan(0), "never fired: " + tally);
        }

        // With nothing to hide behind, a gang member fights in the open: steps from side to side with the gun on the mark, the body stepping sideways, and fires.
        [UnityTest]
        public IEnumerator InTheOpenTheyStepSideToSide()
        {
            Random.InitState(2);
            Time.captureFramerate = 30;
            Floor();
            yield return null;
            var gang = Fighter.Spawn(Record("GANG_001"), Fighter.Side.Gang, new Vector3(0, 0, 15), 180);
            var police = Fighter.Spawn(Record("POLICE_001"), Fighter.Side.Police, new Vector3(0, 0, 0), 0);
            police.HoldFire = true;
            police.Health = 1e6f;
            police.enabled = true;
            float lo = float.MaxValue, hi = float.MinValue, side = 0;
            int moving = 0, open = 0, frames = 0;
            float until = Time.time + 10;
            while (Time.time < until)
            {
                yield return null;
                frames++;
                if (gang.Now == Fighter.Mode.Open) open++;
                lo = Mathf.Min(lo, gang.transform.position.x);
                hi = Mathf.Max(hi, gang.transform.position.x);
                if (gang.Body.Speed > 0.5f) { moving++; side = Mathf.Max(side, Mathf.Abs(gang.Body.Going)); }
            }
            string tally = $"open {open}/{frames} frames, moved {hi - lo:F1} m across, moving {moving} frames, stepping at most {side:F0} deg off ahead, fired {gang.Fired}, covers {gang.Covers}, cover good {Cover.Good}";
            Debug.Log("[Dad] in the open: " + tally);
            Assert.AreEqual(0, gang.Covers, "found cover on a bare floor: " + tally);
            Assert.That(open, Is.GreaterThan(frames / 2), "not fighting in the open: " + tally);
            Assert.That(hi - lo, Is.GreaterThan(1.5f), "did not step about: " + tally);
            Assert.That(side, Is.GreaterThan(60), "did not step sideways (facing the mark): " + tally);
            Assert.That(gang.Fired, Is.GreaterThan(0), "never fired: " + tally);
        }

        // The gang on their turf: calm and unarmed, not shot at by the police, raising no alarm; a shot nearby and the guns are out and they fight the police; a quiet
        // while later the guns are put away and they stand about at home again.
        [UnityTest]
        public IEnumerator TheGangDrawAtShotsAndPutTheGunsAway()
        {
            Random.InitState(4);
            Time.captureFramerate = 30;
            Floor();
            var host = new GameObject("turf");
            made.Add(host);
            var turf = host.AddComponent<Turf>();
            yield return null;
            turf.Put(new Vector3(0, 0, 20), Vector3.right, (0, 0));
            var gang = turf.Gang.ToArray();
            Assert.AreEqual(3, gang.Length, "the gang were not all put out");
            var police = Fighter.Spawn(Record("POLICE_002"), Fighter.Side.Police, new Vector3(0, 0, 4), 0);
            police.Health = 1e6f;
            int alarms = 0;
            void Count(Vector3 at, float reach, Danger.Kind kind) { if (kind == Danger.Kind.Gun && Vector3.Distance(at, turf.Place) < 5) alarms++; }
            Danger.Heard += Count;
            try
            {
                yield return For(3);
                foreach (var f in gang)
                {
                    Assert.AreEqual(Fighter.Mode.Calm, f.Now, f.name + " is not calm");
                    Assert.IsNull(f.Gun, f.name + " has a gun out while calm");
                    Assert.IsTrue(f.Body.Acting, f.name + " is not standing about");
                }
                Assert.IsNull(police.Target, "the police went after the unarmed");
                Assert.AreEqual(0, police.Fired, "the police fired at the unarmed");
                Assert.AreEqual(0, alarms, "calm, they raised an alarm (a gun out)");
                Danger.Report(new Vector3(30, 1, 30), Danger.Kind.Shot); // a shot, 30 m off
                yield return null;
                foreach (var f in gang)
                {
                    Assert.IsNotNull(f.Gun, f.name + " did not draw at the shot");
                    Assert.AreNotEqual(Fighter.Mode.Calm, f.Now, f.name + " is still calm");
                }
                float until = Time.time + 25;
                while (Time.time < until && !(police.Fired > 0 && gang.Sum(g => g.Fired) > 0)) yield return null;
                Debug.Log($"[Dad] turf fight: police fired {police.Fired}, gang fired {gang.Sum(g => g.Fired)}, gang {string.Join(", ", gang.Select(g => $"{g.name} {g.Now} {g.Health:F0}"))}");
                Assert.That(police.Fired, Is.GreaterThan(0), "the police did not fight the armed gang");
                Assert.That(gang.Sum(g => g.Fired), Is.GreaterThan(0), "the gang did not fight the police");
                // the officer goes; after the quiet the living put their guns away at home
                Object.Destroy(police.gameObject);
                Fighter.Quiet = 4;
                until = Time.time + 40;
                while (Time.time < until && gang.Any(g => !g.Dead && g.Now != Fighter.Mode.Calm)) yield return null;
                foreach (var f in gang.Where(g => !g.Dead))
                {
                    Assert.AreEqual(Fighter.Mode.Calm, f.Now, f.name + " did not calm down");
                    Assert.IsNull(f.Gun, f.name + " kept the gun out");
                    Assert.That(Vector3.Distance(f.transform.position, f.Home.Value), Is.LessThan(1.5f), f.name + " is not back home");
                }
            }
            finally { Danger.Heard -= Count; }
        }

        // Plain play in the real city: the gang are put out on a turf in reach of you, standing on the street, calm; the walking mesh is built round them from the
        // street's colliders, and a way is found there.
        [UnityTest]
        public IEnumerator TheGangAreOnTheStreetsInPlainPlay()
        {
            Turf.Automatic = true;
            SceneManager.LoadScene("Boot");
            yield return null;
            var turf = Turf.Active;
            Assert.IsNotNull(turf, "plain play has no turf");
            float limit = Time.realtimeSinceStartup + 120;
            while (turf.Gang.Count == 0 && Time.realtimeSinceStartup < limit) yield return null;
            var places = turf.Places();
            var you = Camera.main.transform.position;
            Debug.Log($"[Dad] turfs on the chunks in: {places.Count}; within {Turf.Near}-{Turf.Far} m of you: {places.Count(p => { float d = new Vector2(p.x - you.x, p.z - you.z).magnitude; return d >= Turf.Near && d <= Turf.Far; })}; gangs out {turf.Gangs}");
            Assert.That(turf.Gang.Count, Is.EqualTo(3), "no gang came out in 120 s");
            float away = new Vector2(turf.Place.x - you.x, turf.Place.z - you.z).magnitude;
            Assert.That(away, Is.InRange(Turf.Near, Turf.Far), "the gang are out of reach");
            yield return null;
            Physics.SyncTransforms();
            foreach (var f in turf.Gang)
            {
                Assert.AreEqual(Fighter.Mode.Calm, f.Now, f.name + " is not calm");
                Assert.IsTrue(Physics.Raycast(f.transform.position + Vector3.up, Vector3.down, out var ground, 3, WorldLayers.Ground, QueryTriggerInteraction.Ignore), f.name + " is not over the street");
                Assert.That(ground.distance - 1, Is.LessThan(0.15f), f.name + " stands off the ground");
            }
            CityPicture("city_turf_1_standing_about", turf);
            Danger.Report(turf.Place + new Vector3(20, 1, 0), Danger.Kind.Shot);
            yield return For(1.5f);
            CityPicture("city_turf_2_guns_out", turf);
            Assert.That(turf.Gang.All(f => f.Gun != null), "the gang did not draw at a shot in the city");
            yield return Built(turf.Place);
            var corners = new List<Vector3>();
            var to = turf.Place + new Vector3(25, 0, 25);
            bool found = Navigation.Route(turf.Place, to, corners);
            var threat = turf.Place + new Vector3(20, 1.65f, 0);
            var times = new List<double>();
            for (int i = 0; i < 10; i++) { Cover.Find(turf.Gang[0], turf.Gang[0].transform.position, threat, 4, 40, 0, out _); times.Add(Cover.LastMs); }
            Debug.Log($"[Dad] city cover search: {Cover.Tried} places tried, {Cover.Good} would do; {times.Min():F2}-{times.Max():F2} ms, median {times.OrderBy(t => t).ElementAt(5):F2} ms");
            Debug.Log($"[Dad] city walking mesh: {Navigation.LastBuildMs:F0} ms, {Navigation.LastSources} sources; a way 35 m off: {found}, {corners.Count} corners, {Navigation.Length(turf.Place, corners):F1} m");
            Assert.IsTrue(Navigation.Route(turf.Place, turf.Place + new Vector3(3, 0, 0), corners) || Navigation.Route(turf.Place, turf.Place + new Vector3(0, 0, 3), corners), "no way a few metres along the street");
        }
    }
}
