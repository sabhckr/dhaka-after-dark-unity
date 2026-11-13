// Free-flying debug camera (C-05). WASD, the arrows or the left stick fly along the view; the mouse
// or the right stick looks; E / Q or the shoulders climb and sink; Shift or a left-stick press is
// fast. It moves only in the FreeCamera input context (Global/ToggleFreeCamera, F1, goes in and out).
// Until Phase E brings a protagonist, plain play (no bench or shot) starts in this context over
// Farmgate. It moves the transform in Update, before FloatingOrigin's LateUpdate shifts the scene.
using Dad.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Dad.Core
{
    public sealed class FreeCamera : MonoBehaviour
    {
        public GameInput Input;
        public float Speed = 30f, FastSpeed = 150f, ClimbSpeed = 20f;
        public float MouseDegreesPerPixel = 0.1f, StickDegreesPerSecond = 120f;
        public float MinHeight = 2f, MaxHeight = 1500f;

        /// <summary>Where plain play starts: south-west of the Farmgate junction, looking at it.</summary>
        public const double StartEast = 2660, StartNorth = 2132, LookEast = 2800, LookNorth = 2200;
        public const float StartHeight = 95f;

        float yaw, pitch;

        /// <summary>Puts the camera at a world position looking at a world point.</summary>
        public void Place(OriginOffset offset, double east, double north, float height, double lookEast, double lookNorth, float lookHeight)
        {
            offset.ToScene(east, north, out float x, out float z);
            offset.ToScene(lookEast, lookNorth, out float lx, out float lz);
            transform.position = new Vector3(x, height, z);
            transform.LookAt(new Vector3(lx, lookHeight, lz));
            ReadAngles();
        }

        void OnEnable() => ReadAngles();

        void ReadAngles()
        {
            var e = transform.eulerAngles;
            yaw = e.y;
            pitch = e.x > 180f ? e.x - 360f : e.x;
        }

        void Update()
        {
            if (Input == null || Input.Context != InputContext.FreeCamera) return;
            float dt = Time.unscaledDeltaTime;
            var look = Input.FlyLook.ReadValue<Vector2>();
            // Mouse delta is a distance this frame; a stick is a rate.
            float scale = Input.FlyLook.activeControl?.device is Pointer ? MouseDegreesPerPixel : StickDegreesPerSecond * dt;
            yaw += look.x * scale;
            pitch = Mathf.Clamp(pitch - look.y * scale, -89f, 89f);
            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);

            var move = Input.FlyMove.ReadValue<Vector2>();
            float climb = Input.FlyClimb.ReadValue<float>();
            float speed = Input.FlyFast.IsPressed() ? FastSpeed : Speed;
            var step = (transform.forward * move.y + transform.right * move.x) * (speed * dt) + Vector3.up * (climb * ClimbSpeed * dt);
            var p = transform.position + step;
            p.y = Mathf.Clamp(p.y, MinHeight, MaxHeight);
            transform.position = p;
        }
    }
}
