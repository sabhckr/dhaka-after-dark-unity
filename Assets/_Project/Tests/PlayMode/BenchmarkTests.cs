// The Dad.* profiler markers reach ProfilerRecorder, and a short benchmark on the Boot scene
// writes its summary, per-frame CSV and profiler capture with the camera on the path.
using System.Collections;
using System.IO;
using System.Text.RegularExpressions;
using Dad.Core;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Dad.Tests
{
    public class BenchmarkTests
    {
        [SetUp]
        public void MarkScenes() => TestScenes.Mark();

        [UnityTearDown]
        public IEnumerator CleanUp() => TestScenes.Clean();

        [UnityTest]
        public IEnumerator MarkersReachTheProfiler()
        {
            var names = new[] { "Dad.FloatingOrigin.Shift", "Dad.Input.Context", "Dad.CameraPath.Apply" };
            var recorders = new ProfilerRecorder[names.Length];
            for (int i = 0; i < names.Length; i++) recorders[i] = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, names[i]);
            var seen = new bool[names.Length];
            var scene = SceneManager.CreateScene("MarkerTest");
            SceneManager.SetActiveScene(scene);
            var asset = InputActionAsset.FromJson(File.ReadAllText("Assets/_Project/Input/DadInput.inputactions"));
            GameInput input = null;
            try
            {
                yield return null;
                var cam = new GameObject("Camera");
                var origin = new GameObject("FloatingOrigin").AddComponent<FloatingOrigin>();
                var player = cam.AddComponent<CameraPathPlayer>();
                player.Origin = origin;
                origin.Recenter(2600, 2000);                         // Dad.FloatingOrigin.Shift
                input = new GameInput(asset);                         // Dad.Input.Context
                player.Load(CameraPath.Load(BenchmarkRunner.DefaultPath)); // Dad.CameraPath.Apply
                for (int f = 0; f < 4; f++)
                {
                    yield return null;
                    for (int i = 0; i < names.Length; i++) seen[i] |= recorders[i].Valid && recorders[i].LastValue > 0;
                }
                for (int i = 0; i < names.Length; i++) Assert.IsTrue(seen[i], names[i] + " recorded");
            }
            finally
            {
                foreach (var r in recorders) r.Dispose();
                input?.Dispose();
                Object.Destroy(asset);
            }
            yield return SceneManager.UnloadSceneAsync(scene);
        }

        /// <summary>Waits for this run's city map, so its line is in before the logs are checked and none comes into the next test.</summary>
        public static IEnumerator MapDrawn()
        {
            float until = Time.realtimeSinceStartup + 30f;
            while ((GameRoot.MapBuilding == null || !GameRoot.MapBuilding.IsCompleted) && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(GameRoot.MapBuilding != null && GameRoot.MapBuilding.IsCompleted, "city map drawn within 30 s");
        }

        [UnityTest]
        public IEnumerator ShortBenchOnBootSceneWritesResults()
        {
            // Four lines, in whatever order the boot check, the bench and the city map (a worker each) finish in.
            var line = new Regex(@"^\[Dad\] (ok c_5_-5|bench hub-rich-air started|bench hub-rich-air: \d+ frames|city map \d+x\d+)");
            for (int i = 0; i < 4; i++) LogAssert.Expect(LogType.Log, line);
            SceneManager.LoadScene("Boot");
            yield return null;
            var dir = Path.Combine(Directory.GetCurrentDirectory(), "Evidence", "local", "bench-playmode-" + System.DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            var bench = new GameObject("Bench").AddComponent<BenchmarkRunner>();
            bench.OutDir = dir;
            bench.Capture = true;
            bench.CaptureMaxFrames = 120; // batch mode runs thousands of frames a second
            bench.WarmupSeconds = 0.2f;
            bench.MaxSeconds = 2f;
            bench.VSync = false; // batch mode has no display to sync to
            float until = Time.realtimeSinceStartup + 30f;
            while (!bench.Done && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(bench.Done, "bench finished within 30 s");

            var s = bench.Result;
            Assert.IsNotNull(s);
            Assert.Greater(s.frames, 10);
            Assert.IsTrue(s.complete, "path played to MaxSeconds");
            Assert.GreaterOrEqual(s.pathSeconds, 2f);
            Assert.AreEqual(s.frames, s.frameMs.samples);
            Assert.Greater(s.frameMs.p50, 0f);
            Assert.AreEqual("editor", s.build);
            Assert.AreEqual(s.frames + 1, File.ReadAllLines(Path.Combine(dir, "frames.csv")).Length);
            var json = JsonUtility.FromJson<BenchSummary>(File.ReadAllText(Path.Combine(dir, "summary.json")));
            Assert.AreEqual(s.frames, json.frames);
            Assert.IsNotEmpty(s.capture, "profiler capture written");
            Assert.AreEqual(120, s.captureFrames);
            Assert.Greater(new FileInfo(Path.Combine(dir, s.capture)).Length, 0);
            CollectionAssert.DoesNotContain(s.unavailable, "frameMs", "frame time is always available");

            // The camera is where the path says, through the floating origin.
            var player = Camera.main.GetComponent<CameraPathPlayer>();
            var origin = Object.FindAnyObjectByType<FloatingOrigin>();
            var p = player.Path.Evaluate(player.Time);
            origin.Offset.ToWorld(player.transform.position.x, player.transform.position.z, out double e, out double n);
            Assert.AreEqual(p.east, e, 0.01);
            Assert.AreEqual(p.north, n, 0.01);
            Assert.GreaterOrEqual(player.Time, 2f);
            yield return MapDrawn();
            LogAssert.NoUnexpectedReceived();
            Debug.Log($"Bench test: {s.frames} frames, unavailable [{string.Join(", ", s.unavailable)}], capture {s.capture}, dir {dir}");

            // Leave no Boot scene behind: its FloatingOrigin would block the next test's.
            SceneManager.SetActiveScene(SceneManager.CreateScene("AfterBoot" + System.Guid.NewGuid().ToString("N")));
            yield return SceneManager.UnloadSceneAsync("Boot");
        }
    }
}
