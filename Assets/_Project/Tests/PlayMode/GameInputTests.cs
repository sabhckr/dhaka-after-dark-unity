// GameInput against simulated devices: contexts do not leak into each other, pausing and
// resuming restore the right maps, losing a gamepad is safe, callbacks never duplicate, a
// floating-origin shift leaves input alone, and rebinding survives a save and load.
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
    public class GameInputTests : InputTestFixture
    {
        const string AssetPath = "Assets/_Project/Input/DadInput.inputactions";

        InputActionAsset asset;
        GameInput input;
        Keyboard keyboard;
        Mouse mouse;
        Gamepad gamepad;

        static InputActionAsset LoadAsset() => InputActionAsset.FromJson(File.ReadAllText(AssetPath));

        public override void Setup()
        {
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();
            gamepad = InputSystem.AddDevice<Gamepad>();
            asset = LoadAsset();
            input = new GameInput(asset);
        }

        public override void TearDown()
        {
            input.Dispose();
            Object.DestroyImmediate(asset);
            base.TearDown();
        }

        void AssertNoGameplayAction(string when)
        {
            foreach (var a in input.OnFoot.actions) Assert.IsFalse(a.enabled, $"{when}: OnFoot/{a.name} enabled");
            foreach (var a in input.Driving.actions) Assert.IsFalse(a.enabled, $"{when}: Driving/{a.name} enabled");
            foreach (var a in input.FreeCamera.actions) Assert.IsFalse(a.enabled, $"{when}: FreeCamera/{a.name} enabled");
            Assert.IsFalse(input.Global.enabled, $"{when}: Global enabled");
        }

        [Test]
        public void KeyboardMouseAndGamepadDriveOnFoot()
        {
            Press(keyboard.wKey);
            Press(keyboard.dKey);
            var move = input.Move.ReadValue<Vector2>();
            Assert.AreEqual(0.7071f, move.x, 0.001f);
            Assert.AreEqual(0.7071f, move.y, 0.001f);
            Release(keyboard.wKey);
            Release(keyboard.dKey);
            Assert.AreEqual(Vector2.zero, input.Move.ReadValue<Vector2>());

            Set(gamepad.leftStick, new Vector2(0f, 0.9f));
            Assert.Greater(input.Move.ReadValue<Vector2>().y, 0.5f);
            Set(gamepad.leftStick, Vector2.zero);

            Set(mouse.delta, new Vector2(3f, -2f));
            Assert.AreEqual(new Vector2(3f, -2f), input.Look.ReadValue<Vector2>());

            Press(keyboard.spaceKey);
            Assert.IsTrue(input.Jump.WasPressedThisFrame());
        }

        [Test]
        public void DrivingReadsAxesAndOnFootIsOff()
        {
            input.Switch(InputContext.Driving);
            Assert.IsFalse(input.Move.enabled);
            Press(keyboard.wKey);
            Press(keyboard.aKey);
            Assert.AreEqual(1f, input.Throttle.ReadValue<float>());
            Assert.AreEqual(-1f, input.Steer.ReadValue<float>());
            Assert.AreEqual(Vector2.zero, input.Move.ReadValue<Vector2>(), "W does not walk while driving");
            Release(keyboard.wKey);
            Release(keyboard.aKey);
            Set(gamepad.rightTrigger, 0.75f);
            Assert.AreEqual(0.75f, input.Throttle.ReadValue<float>(), 0.01f);
        }

        [Test]
        public void OpeningAMenuDoesNotMoveThePlayer()
        {
            Press(keyboard.wKey);
            Assert.AreEqual(Vector2.up, input.Move.ReadValue<Vector2>());
            input.Push(InputContext.Menu);
            Assert.AreEqual(Vector2.zero, input.Move.ReadValue<Vector2>(), "held W stops counting once the menu opens");
            AssertNoGameplayAction("menu");
            Release(keyboard.wKey);
            Press(keyboard.sKey);
            Set(gamepad.leftStick, new Vector2(0f, 1f));
            Assert.AreEqual(Vector2.zero, input.Move.ReadValue<Vector2>());
            Assert.Greater(input.Navigate.ReadValue<Vector2>().magnitude, 0.5f, "the stick drives the menu instead");
            Release(keyboard.sKey);
            Set(gamepad.leftStick, Vector2.zero);
            Assert.IsTrue(input.Pop());
            Assert.AreEqual(InputContext.OnFoot, input.Context);
            Assert.AreEqual(Vector2.zero, input.Move.ReadValue<Vector2>(), "no stale movement after closing");
        }

        [Test]
        public void PauseDisablesGameplayAndResumeRestoresIt()
        {
            input.Switch(InputContext.Driving);
            Press(keyboard.escapeKey);
            Assert.AreEqual(InputContext.Paused, input.Context);
            Release(keyboard.escapeKey);
            Assert.AreEqual(InputContext.Paused, input.Context, "one press pauses once, it does not also resume");
            AssertNoGameplayAction("paused");
            Set(gamepad.rightTrigger, 1f);
            Assert.AreEqual(0f, input.Throttle.ReadValue<float>());
            Set(gamepad.rightTrigger, 0f);

            Press(keyboard.escapeKey);
            Release(keyboard.escapeKey);
            Assert.AreEqual(InputContext.Driving, input.Context, "resume returns to driving, not on foot");
            Assert.AreEqual((true, false, true, false), (input.Global.enabled, input.OnFoot.enabled, input.Driving.enabled, input.UI.enabled));

            PressAndRelease(gamepad.startButton);
            Assert.AreEqual(InputContext.Paused, input.Context, "Start pauses");
            PressAndRelease(gamepad.startButton);
            Assert.AreEqual(InputContext.Driving, input.Context, "Start resumes");
            Assert.AreEqual(1, input.Depth);
        }

        [Test]
        public void ButtonHeldWhileResumingDoesNotFire()
        {
            int jumps = 0;
            input.Jump.performed += _ => jumps++;
            PressAndRelease(gamepad.startButton);
            Assert.AreEqual(InputContext.Paused, input.Context);
            Press(gamepad.buttonSouth); // Submit in the pause menu
            PressAndRelease(gamepad.startButton);
            Assert.AreEqual(InputContext.OnFoot, input.Context);
            Assert.AreEqual(0, jumps, "the menu's Submit press does not become a jump");
            Release(gamepad.buttonSouth);
            PressAndRelease(gamepad.buttonSouth);
            Assert.AreEqual(1, jumps);
        }

        [Test]
        public void LosingTheGamepadPausesSafely()
        {
            input.Switch(InputContext.Driving);
            Set(gamepad.rightTrigger, 1f);
            Assert.AreEqual(1f, input.Throttle.ReadValue<float>());
            Assert.DoesNotThrow(() => InputSystem.RemoveDevice(gamepad));
            Assert.AreEqual(InputContext.Paused, input.Context);
            Assert.AreEqual(0f, input.Throttle.ReadValue<float>());

            var pad = InputSystem.AddDevice<Gamepad>();
            PressAndRelease(pad.startButton); // UI/Cancel on the new pad resumes
            Assert.AreEqual(InputContext.Driving, input.Context);
            Set(pad.rightTrigger, 0.5f);
            Assert.AreEqual(0.5f, input.Throttle.ReadValue<float>(), 0.01f);

            input.Push(InputContext.Menu);
            InputSystem.RemoveDevice(pad);
            Assert.AreEqual(InputContext.Menu, input.Context, "already in a menu: no extra pause");
            Assert.AreEqual(2, input.Depth);
            input.Pop();
            input.PauseOnGamepadLoss = false;
            InputSystem.RemoveDevice(InputSystem.AddDevice<Gamepad>());
            Assert.AreEqual(InputContext.Driving, input.Context);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void SwitchingContextsNeverDuplicatesCallbacks()
        {
            int jumps = 0, changes = 0;
            input.Jump.performed += _ => jumps++;
            for (int i = 0; i < 20; i++)
            {
                input.Switch(InputContext.Driving);
                input.Switch(InputContext.OnFoot);
                input.Push(InputContext.Menu);
                input.Pop();
                input.OnFoot.Disable();
                input.OnFoot.Enable();
            }
            input.ContextChanged += (_, __) => changes++;
            PressAndRelease(keyboard.spaceKey);
            Assert.AreEqual(1, jumps);
            PressAndRelease(keyboard.escapeKey);
            Assert.AreEqual(2, input.Depth, "one Escape, one pause");
            Assert.AreEqual(1, input.PauseRequests, "the pause handler ran once");
            Assert.AreEqual(1, changes);
        }

        [UnityTest]
        public IEnumerator FloatingOriginShiftDoesNotInterruptInput()
        {
            var scene = SceneManager.CreateScene("InputShiftTest");
            SceneManager.SetActiveScene(scene);
            var player = new GameObject("Player").transform;
            var origin = new GameObject("FloatingOrigin").AddComponent<FloatingOrigin>();
            origin.Target = player;
            int jumps = 0;
            input.Jump.performed += _ => jumps++;

            // In a [UnityTest] the fixture only queues events; the next frame processes them.
            Press(keyboard.wKey);
            player.position = new Vector3(2600f, 0f, 0f);
            yield return null; // W is processed and the origin shifts in the same frame
            Assert.AreEqual(1, origin.Shifts);
            Assert.AreEqual(InputContext.OnFoot, input.Context);
            Assert.IsTrue(input.Move.enabled);
            Assert.AreEqual(Vector2.up, input.Move.ReadValue<Vector2>(), "held W survives the shift");
            PressAndRelease(keyboard.spaceKey);
            yield return null;
            Assert.AreEqual(1, jumps);
            Assert.AreEqual(Vector2.up, input.Move.ReadValue<Vector2>());
            Release(keyboard.wKey);
            yield return null;
            Assert.AreEqual(Vector2.zero, input.Move.ReadValue<Vector2>());
            yield return SceneManager.UnloadSceneAsync(scene);
        }

        [Test]
        public void RebindingIsInteractiveAndPersists()
        {
            int index = input.Jump.GetBindingIndex(InputBinding.MaskByGroup("Keyboard&Mouse"));
            Assert.GreaterOrEqual(index, 0);
            using (var op = input.StartRebind(input.Jump, index))
            {
                Press(keyboard.kKey);
                currentTime += 0.1; // the rebind waits 0.05 s for a better match before completing
                InputSystem.Update();
                Assert.IsTrue(op.completed, "rebind finished on K");
            }
            Release(keyboard.kKey);
            Assert.AreEqual("<Keyboard>/k", input.Jump.bindings[index].effectivePath);
            Assert.AreEqual(InputContext.OnFoot, input.Context);
            Assert.IsTrue(input.OnFoot.enabled, "map back on after rebinding");

            int jumps = 0;
            input.Jump.performed += _ => jumps++;
            PressAndRelease(keyboard.spaceKey);
            PressAndRelease(keyboard.kKey);
            Assert.AreEqual(1, jumps, "only K jumps now");

            var json = input.SaveBindingOverrides();
            var fresh = LoadAsset();
            try
            {
                using var other = new GameInput(fresh);
                other.LoadBindingOverrides(json);
                Assert.AreEqual("<Keyboard>/k", other.Jump.bindings[index].effectivePath);
            }
            finally { Object.DestroyImmediate(fresh); }
        }
    }
}
