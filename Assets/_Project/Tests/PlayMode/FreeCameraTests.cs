// C-05 free camera and debug toggles, against simulated devices: F1 goes in and out of the free
// camera; W flies along the view, E climbs, the mouse turns it; nothing moves it outside its
// context; F2-F4 switch the streamer's overlays.
using System.Collections;
using System.IO;
using Dad.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Dad.Tests
{
    public class FreeCameraTests : InputTestFixture
    {
        const string AssetPath = "Assets/_Project/Input/DadInput.inputactions";

        InputActionAsset asset;
        GameInput input;
        Keyboard keyboard;
        Mouse mouse;
        Scene scene;
        FreeCamera fly;
        DebugViews views;
        ChunkStreamer streamer;

        public override void Setup()
        {
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();
            asset = InputActionAsset.FromJson(File.ReadAllText(AssetPath));
            input = new GameInput(asset);
            scene = SceneManager.CreateScene("FreeCamera" + System.Guid.NewGuid().ToString("N"));
            SceneManager.SetActiveScene(scene);
            fly = new GameObject("Camera").AddComponent<FreeCamera>();
            fly.Input = input;
            fly.transform.SetPositionAndRotation(new Vector3(0f, 50f, 0f), Quaternion.identity); // looking north (+z)
            var host = new GameObject("Streamer");
            host.SetActive(false); // never starts: only its overlay switches are used
            streamer = host.AddComponent<ChunkStreamer>();
            views = new GameObject("DebugViews").AddComponent<DebugViews>();
            views.Input = input;
            views.Streamer = streamer;
        }

        public override void TearDown()
        {
            Object.DestroyImmediate(views.gameObject);
            Object.DestroyImmediate(fly.gameObject);
            Object.DestroyImmediate(streamer.gameObject);
            SceneManager.UnloadSceneAsync(scene);
            input.Dispose();
            Object.DestroyImmediate(asset);
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator F1EntersAndLeavesTheFreeCamera()
        {
            Assert.AreEqual(InputContext.OnFoot, input.Context);
            PressAndRelease(keyboard.f1Key);
            yield return null;
            Assert.AreEqual(InputContext.FreeCamera, input.Context);
            Assert.AreEqual((true, false, true), (input.Global.enabled, input.OnFoot.enabled, input.FreeCamera.enabled));
            PressAndRelease(keyboard.f1Key);
            yield return null;
            Assert.AreEqual(InputContext.OnFoot, input.Context, "back to what was underneath");
            Assert.IsFalse(input.FreeCamera.enabled);
        }

        [UnityTest]
        public IEnumerator FliesAlongTheViewClimbsAndTurns()
        {
            // On foot, W belongs to OnFoot/Move: the camera stays put.
            Press(keyboard.wKey);
            for (int i = 0; i < 5; i++) yield return null;
            Release(keyboard.wKey);
            yield return null;
            Assert.AreEqual(new Vector3(0f, 50f, 0f), fly.transform.position, "no flying outside the free-camera context");

            input.Switch(InputContext.FreeCamera);
            Press(keyboard.wKey);
            for (int i = 0; i < 10; i++) yield return null;
            Release(keyboard.wKey);
            yield return null;
            var p = fly.transform.position;
            Assert.Greater(p.z, 0.1f, "W flies north, the way it looks");
            Assert.AreEqual(0f, p.x, 1e-3f);
            Assert.AreEqual(50f, p.y, 1e-3f);

            Press(keyboard.eKey);
            for (int i = 0; i < 10; i++) yield return null;
            Release(keyboard.eKey);
            yield return null;
            Assert.Greater(fly.transform.position.y, 50.1f, "E climbs");

            Set(mouse.delta, new Vector2(100f, 0f));
            yield return null;
            Set(mouse.delta, Vector2.zero);
            yield return null;
            Assert.AreEqual(10f, fly.transform.eulerAngles.y, 0.01f, "100 px right turns 10 degrees right");
        }

        [UnityTest]
        public IEnumerator FunctionKeysSwitchTheOverlays()
        {
            Assert.AreEqual((false, false, false), (streamer.ShowGrid, streamer.ShowGraph, streamer.ShowLines), "all off at start");
            PressAndRelease(keyboard.f2Key);
            yield return null;
            Assert.AreEqual((true, false, false), (streamer.ShowGrid, streamer.ShowGraph, streamer.ShowLines));
            PressAndRelease(keyboard.f3Key);
            yield return null;
            PressAndRelease(keyboard.f4Key);
            yield return null;
            Assert.AreEqual((true, true, true), (streamer.ShowGrid, streamer.ShowGraph, streamer.ShowLines));
            PressAndRelease(keyboard.f2Key);
            yield return null;
            Assert.AreEqual((false, true, true), (streamer.ShowGrid, streamer.ShowGraph, streamer.ShowLines));
            input.Push(InputContext.Paused);
            PressAndRelease(keyboard.f3Key);
            yield return null;
            Assert.IsTrue(streamer.ShowGraph, "no toggles while paused");
        }
    }
}
