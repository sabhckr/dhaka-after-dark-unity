// The camera behind someone on foot (E-03): the mouse or stick turns it round them, and it comes in rather than go through a wall. It runs before FloatingOrigin, as VehicleCamera does.
// Aiming (2026-11-10) brings it in over the right shoulder and narrows the view; the look speeds, inverted look and field of view are the player's settings.
using UnityEngine;
using UnityEngine.InputSystem;

namespace Dad.Core
{
    [DefaultExecutionOrder(900)]
    public sealed class FootCamera : MonoBehaviour
    {
        const float Pivot = 1.5f, Distance = 3.4f, MinDistance = 0.8f, Skin = 0.2f, Side = 0.35f;
        const float AimDistance = 1.5f, AimSide = 0.6f, AimPivot = 1.55f, AimNarrows = 0.72f, AimTime = 0.18f;
        /// <summary>Degrees a second a stick held right over turns the view, at a gamepad look speed of 1.</summary>
        public const float StickSpeed = 160;

        public Transform Target;
        public GameInput Input;
        /// <summary>Degrees a frame the mouse's movement is worth, before the player's mouse look speed.</summary>
        public float Sensitivity = 0.1f;
        /// <summary>Over the shoulder (Arsenal sets it while you aim).</summary>
        public bool Aiming;
        /// <summary>Degrees the view is kicked up by a shot, taken once.</summary>
        public float Kick;

        public float Yaw => yaw;
        public float AimBlend { get; private set; }

        float yaw, pitch = 8, shown = Distance;
        Camera cam;

        void Awake() => cam = GetComponent<Camera>();

        public void Attach(Transform target, float heading)
        {
            Target = target;
            yaw = heading;
            pitch = 8;
            shown = Distance;
        }

        /// <summary>What a look input is worth this frame, in degrees, by the player's settings: the mouse by how far it moved, a stick by how long it is held.</summary>
        public static Vector2 Degrees(InputAction look, bool aiming, float mouse = 0.1f)
        {
            var v = look.ReadValue<Vector2>();
            var s = GameSettings.Current;
            bool pad = look.activeControl != null && look.activeControl.device is Gamepad;
            v *= pad ? StickSpeed * s.PadLook * Time.unscaledDeltaTime : mouse * s.MouseLook;
            if (aiming) v *= s.AimLook;
            if (s.InvertLook) v.y = -v.y;
            return v;
        }

        void LateUpdate()
        {
            if (Target == null) return;
            if (Input != null && Time.timeScale > 0.5f)
            {
                var look = Degrees(Input.Look, Aiming, Sensitivity);
                yaw += look.x;
                pitch = Mathf.Clamp(pitch - look.y, -30, 60);
            }
            pitch = Mathf.Clamp(pitch - Kick, -30, 60);
            Kick = 0;
            AimBlend = Mathf.MoveTowards(AimBlend, Aiming ? 1 : 0, Time.deltaTime / AimTime);
            float a = Mathf.SmoothStep(0, 1, AimBlend);
            var turn = Quaternion.Euler(pitch, yaw, 0);
            var pivot = Target.position + Vector3.up * Mathf.Lerp(Pivot, AimPivot, a) + turn * Vector3.right * Mathf.Lerp(Side, AimSide, a);
            float far = Mathf.Lerp(Distance, AimDistance, a);
            float want = far;
            if (Physics.SphereCast(pivot, Skin, turn * Vector3.back, out var hit, far, WorldLayers.Ground)) want = Mathf.Max(MinDistance, hit.distance);
            shown = want < shown ? want : Mathf.MoveTowards(shown, want, 4 * Time.deltaTime);
            transform.SetPositionAndRotation(pivot + turn * Vector3.back * shown, turn);
            if (cam != null) cam.fieldOfView = GameSettings.Current.FieldOfView * Mathf.Lerp(1, AimNarrows, a);
        }
    }
}
