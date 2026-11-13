// The FloatingOrigin component moves the scene back when its target passes the threshold,
// and nothing's world position or spacing changes by more than 1 mm.
using System.Collections;
using Dad.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Dad.Tests
{
    public class FloatingOriginTests
    {
        [SetUp]
        public void MarkScenes() => TestScenes.Mark();

        [UnityTearDown]
        public IEnumerator CleanUp() => TestScenes.Clean();

        [UnityTest]
        public IEnumerator ShiftsSceneWhenTargetPassesThreshold()
        {
            var scene = SceneManager.CreateScene("FloatingOriginTest");
            SceneManager.SetActiveScene(scene);
            var player = new GameObject("Player").transform;
            var a = new GameObject("A").transform;
            var b = new GameObject("B").transform;
            var origin = new GameObject("FloatingOrigin").AddComponent<FloatingOrigin>();
            origin.Target = player;
            a.position = new Vector3(2610.125f, 4f, 30.5f);
            b.position = new Vector3(2617.375f, 9f, 41.25f);
            var spacing = b.position - a.position;
            int events = 0;
            origin.Shifted += (e, n) => events++;

            player.position = new Vector3(1999f, 0f, 30f);
            yield return null;
            Assert.AreEqual(0, origin.Shifts, "no shift inside 2 km");

            player.position = new Vector3(2600.25f, 1.5f, 30f);
            yield return null;
            Assert.AreEqual(1, origin.Shifts);
            Assert.AreEqual(1, events);
            Assert.AreEqual((2500.0, 0.0), (origin.Offset.East, origin.Offset.North));
            Assert.AreEqual(new Vector3(100.25f, 1.5f, 30f), player.position, "player moved by exactly 2500 m");
            origin.Offset.ToWorld(a.position.x, a.position.z, out double ae, out double an);
            Assert.AreEqual(2610.125, ae, 0.001);
            Assert.AreEqual(30.5, an, 0.001);
            Assert.AreEqual(4f, a.position.y, "height is never shifted");
            Assert.Less((b.position - a.position - spacing).magnitude, 0.001f);

            yield return null;
            Assert.AreEqual(1, origin.Shifts, "no repeat shift after moving back");
            LogAssert.NoUnexpectedReceived();
            yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator SecondOriginDisablesItself()
        {
            var scene = SceneManager.CreateScene("TwoOrigins");
            SceneManager.SetActiveScene(scene);
            var first = new GameObject("First").AddComponent<FloatingOrigin>();
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(@"^\[Dad\] second FloatingOrigin on Second disabled"));
            var second = new GameObject("Second").AddComponent<FloatingOrigin>();
            Assert.AreSame(first, FloatingOrigin.Active);
            Assert.IsFalse(second.enabled);
            Object.Destroy(first.gameObject);
            yield return null;
            Assert.IsNull(FloatingOrigin.Active);
            yield return SceneManager.UnloadSceneAsync(scene);
        }
    }
}
