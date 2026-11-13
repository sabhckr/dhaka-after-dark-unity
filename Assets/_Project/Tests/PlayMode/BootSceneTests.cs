// The Boot scene starts, and its GameRoot reads the world data on a worker thread.
using System.Collections;
using Dad.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Dad.Tests
{
    public class BootSceneTests
    {
        [SetUp]
        public void MarkScenes() => TestScenes.Mark();

        [UnityTearDown]
        public IEnumerator CleanUp() => TestScenes.Clean();

        [UnityTest]
        public IEnumerator BootSceneDecodesItsSmokeChunk()
        {
            // c_5_-5 per world.json: its buildings are placed models, so none are outlines. Any other log fails the test.
            const string expected = "ok c_5_-5: 0 buildings, 47 roads, 90 graph nodes";
            // That line and the city map's, in whichever order their workers finish.
            var line = new System.Text.RegularExpressions.Regex(@"^\[Dad\] (ok c_5_-5: 0 buildings, 47 roads, 90 graph nodes|city map \d+x\d+)");
            LogAssert.Expect(LogType.Log, line);
            LogAssert.Expect(LogType.Log, line);
            SceneManager.LoadScene("Boot");
            yield return null;
            var root = Object.FindAnyObjectByType<GameRoot>();
            Assert.IsNotNull(root, "GameRoot in Boot scene");
            float until = Time.realtimeSinceStartup + 10f;
            while (root.BootStatus == null && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsNotNull(root.BootStatus, "boot check finished within 10 s");
            Assert.AreEqual(expected, root.BootStatus);

            // Plain play starts in the free camera (no protagonist until Phase E): only Global and
            // FreeCamera are on, nothing the Input System enabled at startup is left on, and the
            // camera is over the hub.
            var input = root.Controls;
            Assert.IsNotNull(input, "GameRoot created GameInput");
            Assert.AreEqual(InputContext.FreeCamera, input.Context);
            Assert.AreEqual((true, false, false, true, false), (input.Global.enabled, input.OnFoot.enabled, input.Driving.enabled, input.FreeCamera.enabled, input.UI.enabled));
            var cam = Camera.main;
            Assert.IsNotNull(cam.GetComponent<FreeCamera>(), "free camera on the main camera");
            Assert.IsNotNull(root.GetComponent<DebugViews>(), "debug views on GameRoot");
            Object.FindAnyObjectByType<FloatingOrigin>().Offset.ToWorld(cam.transform.position.x, cam.transform.position.z, out double east, out double north);
            Assert.AreEqual((FreeCamera.StartEast, FreeCamera.StartNorth), (System.Math.Round(east, 2), System.Math.Round(north, 2)), "starts over Farmgate");
            yield return BenchmarkTests.MapDrawn();
            LogAssert.NoUnexpectedReceived();
            // Leave no Boot scene behind: its FloatingOrigin would block the next test's.
            SceneManager.SetActiveScene(SceneManager.CreateScene("AfterBoot" + System.Guid.NewGuid().ToString("N")));
            yield return SceneManager.UnloadSceneAsync("Boot");
        }
    }
}
