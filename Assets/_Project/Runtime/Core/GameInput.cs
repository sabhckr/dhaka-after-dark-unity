// Player input by context. Each context enables a fixed set of action maps, so no key is
// handled by two maps at once:
//   OnFoot   Global + OnFoot       Escape / P / Start (Global/Pause) pushes Paused
//   Driving  Global + Driving
//   FreeCamera Global + FreeCamera  the debug fly camera (C-05); Global/ToggleFreeCamera (F1) goes in and out
//   Menu     UI                    the menu closes itself with Pop() on UI/Cancel
//   Paused   UI                    UI/Cancel (Escape, East, Start) pops back
// Contexts form a stack, so resuming returns to whatever was underneath. Callbacks are
// subscribed once, here, never on enable, so switching maps cannot duplicate them.
// Losing a gamepad during gameplay pushes Paused. Nothing here allocates per frame.
// Global also holds the debug-view toggles (F1-F4); DebugViews reads them.
using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;

namespace Dad.Core
{
    public enum InputContext { OnFoot, Driving, Menu, Paused, FreeCamera }

    public sealed class GameInput : IDisposable
    {
        public readonly InputActionAsset Asset;
        public readonly InputActionMap Global, OnFoot, Driving, FreeCamera, UI;

        public readonly InputAction TestDrive, Pause, ToggleFreeCamera, ToggleGrid, ToggleGraph, ToggleLines, CycleShading, AdvanceHour, CycleWeather;
        public readonly InputAction FlyMove, FlyLook, FlyClimb, FlyFast;
        public readonly InputAction Move, Look, Sprint, Jump, Crouch, Interact, Attack, Aim, PreviousWeapon, NextWeapon, OnFootMap, Reload, WeaponWheel;
        public readonly InputAction Throttle, Brake, Steer, DriveLook, Handbrake, Horn, ExitVehicle, NextStation, DrivingMap, Repaint;
        public readonly InputAction CameraView, Ignition, Headlamps, Hazards, IndicateLeft, IndicateRight;
        public readonly InputAction Navigate, Submit, Cancel, PreviousTab, NextTab, Zoom, Pan, Point, Click, ScrollWheel;

        public bool PauseOnGamepadLoss = true;
        /// <summary>UI/Cancel in Paused pops it. The menus (Menus) turn this off and go back a screen at a time themselves.</summary>
        public bool CancelPops = true;

        /// <summary>Times the pause handler ran, ignored or not. Lets tests see duplicate callbacks.</summary>
        public int PauseRequests { get; private set; }

        /// <summary>Raised after every context change with (from, to).</summary>
        public event Action<InputContext, InputContext> ContextChanged;

        readonly List<InputContext> stack = new List<InputContext>(4);
        bool disposed;

        public GameInput(InputActionAsset asset, InputContext start = InputContext.OnFoot)
        {
            Asset = asset ?? throw new ArgumentNullException(nameof(asset));
            Global = asset.FindActionMap("Global", true);
            OnFoot = asset.FindActionMap("OnFoot", true);
            Driving = asset.FindActionMap("Driving", true);
            FreeCamera = asset.FindActionMap("FreeCamera", true);
            UI = asset.FindActionMap("UI", true);

            TestDrive = Global.FindAction("TestDrive", true);
            Pause = Global.FindAction("Pause", true);
            ToggleFreeCamera = Global.FindAction("ToggleFreeCamera", true);
            ToggleGrid = Global.FindAction("ToggleGrid", true);
            ToggleGraph = Global.FindAction("ToggleGraph", true);
            ToggleLines = Global.FindAction("ToggleLines", true);
            CycleShading = Global.FindAction("CycleShading", true);
            AdvanceHour = Global.FindAction("AdvanceHour", true);
            CycleWeather = Global.FindAction("CycleWeather", true);
            FlyMove = FreeCamera.FindAction("Move", true);
            FlyLook = FreeCamera.FindAction("Look", true);
            FlyClimb = FreeCamera.FindAction("Climb", true);
            FlyFast = FreeCamera.FindAction("Fast", true);
            Move = OnFoot.FindAction("Move", true);
            Look = OnFoot.FindAction("Look", true);
            Sprint = OnFoot.FindAction("Sprint", true);
            Jump = OnFoot.FindAction("Jump", true);
            Crouch = OnFoot.FindAction("Crouch", true);
            Interact = OnFoot.FindAction("Interact", true);
            Attack = OnFoot.FindAction("Attack", true);
            Aim = OnFoot.FindAction("Aim", true);
            PreviousWeapon = OnFoot.FindAction("PreviousWeapon", true);
            NextWeapon = OnFoot.FindAction("NextWeapon", true);
            OnFootMap = OnFoot.FindAction("Map", true);
            Reload = OnFoot.FindAction("Reload", true);
            WeaponWheel = OnFoot.FindAction("WeaponWheel", true);
            Throttle = Driving.FindAction("Throttle", true);
            Brake = Driving.FindAction("Brake", true);
            Steer = Driving.FindAction("Steer", true);
            DriveLook = Driving.FindAction("Look", true);
            Handbrake = Driving.FindAction("Handbrake", true);
            Horn = Driving.FindAction("Horn", true);
            ExitVehicle = Driving.FindAction("ExitVehicle", true);
            Repaint = Driving.FindAction("Repaint", true);
            NextStation = Driving.FindAction("NextStation", true);
            DrivingMap = Driving.FindAction("Map", true);
            CameraView = Driving.FindAction("CameraView", true);
            Ignition = Driving.FindAction("Ignition", true);
            Headlamps = Driving.FindAction("Headlamps", true);
            Hazards = Driving.FindAction("Hazards", true);
            IndicateLeft = Driving.FindAction("IndicateLeft", true);
            IndicateRight = Driving.FindAction("IndicateRight", true);
            Navigate = UI.FindAction("Navigate", true);
            Submit = UI.FindAction("Submit", true);
            Cancel = UI.FindAction("Cancel", true);
            PreviousTab = UI.FindAction("PreviousTab", true);
            NextTab = UI.FindAction("NextTab", true);
            Zoom = UI.FindAction("Zoom", true);
            Pan = UI.FindAction("Pan", true);
            Point = UI.FindAction("Point", true);
            Click = UI.FindAction("Click", true);
            ScrollWheel = UI.FindAction("ScrollWheel", true);

            Pause.performed += OnPause;
            Cancel.performed += OnCancel;
            InputSystem.onDeviceChange += OnDeviceChange;
            InputSystem.onActionChange += OnActionChange;

            stack.Add(start);
            Apply();
        }

        /// <summary>The project-wide actions (Project Settings → Input System Package).</summary>
        public static GameInput FromProject() => new GameInput(InputSystem.actions);

        public InputContext Context => stack[stack.Count - 1];
        public int Depth => stack.Count;
        public bool IsGameplay => Gameplay(Context);

        public void Push(InputContext context)
        {
            var from = Context;
            stack.Add(context);
            Apply();
            ContextChanged?.Invoke(from, context);
        }

        /// <summary>Returns to the context underneath. The bottom context is never popped.</summary>
        public bool Pop()
        {
            if (stack.Count == 1) return false;
            var from = Context;
            stack.RemoveAt(stack.Count - 1);
            Apply();
            ContextChanged?.Invoke(from, Context);
            return true;
        }

        /// <summary>Replaces the gameplay context, e.g. entering or leaving a vehicle. Under a menu (the start screen while the world loads) the one under it is replaced,
        /// and shows when the menu is popped.</summary>
        public void Switch(InputContext context)
        {
            int i = stack.Count - 1;
            while (i > 0 && !Gameplay(stack[i])) i--;
            var from = stack[i];
            if (from == context) return;
            stack[i] = context;
            if (i < stack.Count - 1) return;
            Apply();
            ContextChanged?.Invoke(from, context);
        }

        static bool Gameplay(InputContext c) => c == InputContext.OnFoot || c == InputContext.Driving || c == InputContext.FreeCamera;

        /// <summary>The gameplay context under any menus.</summary>
        public InputContext Underneath
        {
            get
            {
                for (int i = stack.Count - 1; i > 0; i--) if (Gameplay(stack[i])) return stack[i];
                return stack[0];
            }
        }

        void Apply()
        {
            using var _ = DadMarkers.InputContext.Auto();
            var c = Context;
            // Disable first, so a key shared by two maps is never live in both for a moment.
            if (c != InputContext.OnFoot) OnFoot.Disable();
            if (c != InputContext.Driving) Driving.Disable();
            if (c != InputContext.FreeCamera) FreeCamera.Disable();
            if (c != InputContext.Menu && c != InputContext.Paused) UI.Disable();
            if (!IsGameplay) Global.Disable();
            switch (c)
            {
                case InputContext.OnFoot: Global.Enable(); OnFoot.Enable(); break;
                case InputContext.Driving: Global.Enable(); Driving.Enable(); break;
                case InputContext.FreeCamera: Global.Enable(); FreeCamera.Enable(); break;
                default: UI.Enable(); break;
            }
        }

        void OnPause(InputAction.CallbackContext _)
        {
            PauseRequests++;
            if (IsGameplay) Push(InputContext.Paused);
        }

        void OnCancel(InputAction.CallbackContext _)
        {
            if (CancelPops && Context == InputContext.Paused) Pop();
        }

        // UI Toolkit's input (the Input System's InputSystemProvider) turns the project's UI map on when the first panel registers, whatever the
        // context (since the HUD and menus, 2026-11-10). The context decides: outside the menus it goes off again.
        void OnActionChange(object o, InputActionChange change)
        {
            if (change == InputActionChange.ActionMapEnabled && ReferenceEquals(o, UI) && Context != InputContext.Menu && Context != InputContext.Paused) UI.Disable();
        }

        void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (!(device is Gamepad) || !PauseOnGamepadLoss || !IsGameplay) return;
            if (change == InputDeviceChange.Removed || change == InputDeviceChange.Disconnected) Push(InputContext.Paused);
        }

        // Rebinding: the Controls settings (Menus) call these.

        public string SaveBindingOverrides() => Asset.SaveBindingOverridesAsJson();

        public void LoadBindingOverrides(string json) => Asset.LoadBindingOverridesFromJson(json);

        /// <summary>
        /// Listens for the next control and binds it to <paramref name="bindingIndex"/> of
        /// <paramref name="action"/>. Escape cancels. The action's map is disabled while listening and
        /// the current context is restored afterwards. The caller disposes the returned operation.
        /// With a device ("Gamepad", or "Keyboard" for keyboard and mouse) only its controls are taken; pointer movement never is.
        /// </summary>
        public InputActionRebindingExtensions.RebindingOperation StartRebind(InputAction action, int bindingIndex, string device = null)
        {
            action.actionMap.Disable();
            var op = action.PerformInteractiveRebinding(bindingIndex)
                .WithCancelingThrough("<Keyboard>/escape")
                .WithControlsExcluding("<Pointer>/position")
                .WithControlsExcluding("<Pointer>/delta")
                .WithControlsExcluding("<Mouse>/scroll")
                .OnComplete(_ => Apply())
                .OnCancel(_ => Apply());
            if (device == "Gamepad") op.WithControlsHavingToMatchPath("<Gamepad>");
            else if (device != null) op.WithControlsHavingToMatchPath("<Keyboard>").WithControlsHavingToMatchPath("<Mouse>");
            return op.Start();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            Pause.performed -= OnPause;
            Cancel.performed -= OnCancel;
            InputSystem.onDeviceChange -= OnDeviceChange;
            InputSystem.onActionChange -= OnActionChange;
            Asset.Disable();
        }
    }
}
