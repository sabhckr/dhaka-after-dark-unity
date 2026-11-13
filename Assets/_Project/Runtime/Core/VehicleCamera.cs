// The cameras for driving (E-03): a chase view and the driver's own (E-04). The chase view trails the car's heading a little behind its turns, draws out and widens with
// speed, and comes in rather than pass through a wall; the mouse or stick looks round, and it swings back after a moment. The driver's view sits at the eye on the body,
// so it moves as the body does, and leans with what the car is doing by the numbers from VehicleState. It runs before FloatingOrigin (LateUpdate order 1000).
using Dad.Vehicles;
using UnityEngine;

namespace Dad.Core
{
    [DefaultExecutionOrder(900)]
    public sealed class VehicleCamera : MonoBehaviour
    {
        public enum View { Chase, Driver }

        public const float ChaseNear = 0.3f, DriverNear = 0.06f, BlendTime = 0.35f;
        const float Pivot = 1.25f, MinDistance = 1.4f, Skin = 0.25f, LookReturn = 1.5f;

        public PlayerVehicle Vehicle;
        public View Mode { get; private set; } = View.Chase;
        /// <summary>Set by the input code: degrees a frame.</summary>
        public Vector2 Look;

        Camera cam;
        float yaw, lookYaw, lookPitch, sinceLook = 99, shown, blend = 1;
        Vector3 fromPos;
        Quaternion fromRot;
        float fromFov;
        Vector3 lastVelocity, lean;

        void Awake() => cam = GetComponent<Camera>();

        public void Attach(PlayerVehicle v)
        {
            Vehicle = v;
            yaw = v.transform.eulerAngles.y;
            shown = 6.7f;
            SetMode(View.Chase, true);
        }

        public void Toggle() => SetMode(Mode == View.Chase ? View.Driver : View.Chase, false);

        public void SetMode(View mode, bool now)
        {
            fromPos = transform.position;
            fromRot = transform.rotation;
            fromFov = cam.fieldOfView;
            blend = now ? 1 : 0;
            Mode = mode;
            lookYaw = lookPitch = 0;
            if (Vehicle.Driver != null) Vehicle.Driver.gameObject.SetActive(mode == View.Chase);
            if (now) Vehicle.SeenFromInside(mode == View.Driver ? 1 : 0);
        }

        void LateUpdate()
        {
            if (Vehicle == null) return;
            float dt = Time.deltaTime;
            if (Look.sqrMagnitude > 0.01f) { lookYaw += Look.x; lookPitch = Mathf.Clamp(lookPitch - Look.y, -35, 35); sinceLook = 0; }
            else sinceLook += dt;
            if (sinceLook > LookReturn && Mode == View.Chase)
            {
                float k = 1 - Mathf.Exp(-3 * dt);
                lookYaw = Mathf.LerpAngle(lookYaw, 0, k);
                lookPitch = Mathf.Lerp(lookPitch, 0, k);
            }
            Vector3 pos;
            Quaternion rot;
            float fov;
            if (Mode == View.Chase) Chase(dt, out pos, out rot, out fov);
            else Driver(dt, out pos, out rot, out fov);
            fov += GameSettings.Current.FieldOfView - 60; // the player's setting widens or narrows both views
            cam.nearClipPlane = Mode == View.Driver ? DriverNear : ChaseNear;
            if (blend < 1)
            {
                blend = Mathf.Min(1, blend + dt / BlendTime);
                float s = Mathf.SmoothStep(0, 1, blend);
                pos = Vector3.Lerp(fromPos, pos, s);
                rot = Quaternion.Slerp(fromRot, rot, s);
                fov = Mathf.Lerp(fromFov, fov, s);
                Vehicle.SeenFromInside(Mode == View.Driver ? s : 1 - s);
            }
            transform.SetPositionAndRotation(pos, rot);
            cam.fieldOfView = fov;
        }

        void Chase(float dt, out Vector3 pos, out Quaternion rot, out float fov)
        {
            var car = Vehicle.transform;
            float speed = Mathf.Abs((float)Vehicle.State.Speed), fast = Mathf.Clamp01(speed / 40f);
            // Follow where the car points, faster the faster it goes; backwards, keep looking the way it faces.
            float follow = Mathf.Lerp(2.2f, 5f, fast);
            yaw = Mathf.LerpAngle(yaw, car.eulerAngles.y, 1 - Mathf.Exp(-follow * dt));
            // A bus needs more room than a car, and a bike less.
            float more = (float)Vehicle.Length - 4.5f;
            float distance = Mathf.Lerp(6.6f, 8.2f, fast) + (more > 0 ? more * 0.85f : more * 0.5f), height = Mathf.Lerp(0.45f, 0.35f, fast);
            var pivot = car.position + Vector3.up * Mathf.Max(Pivot, (float)Vehicle.Height * 0.86f);
            var look = Quaternion.Euler(6 + lookPitch, yaw + lookYaw, 0);
            var back = look * Vector3.back;
            var want = pivot + back * distance + Vector3.up * height;
            var ray = want - pivot;
            float reach = ray.magnitude;
            if (Physics.SphereCast(pivot, Skin, ray / reach, out var hit, reach, WorldLayers.Solid, QueryTriggerInteraction.Ignore)) reach = Mathf.Max(MinDistance, hit.distance);
            // In at once, out slowly: a wall must not be passed through, and open road should not jump.
            shown = reach < shown ? reach : Mathf.Lerp(shown, reach, 1 - Mathf.Exp(-3 * dt));
            pos = pivot + ray.normalized * shown;
            rot = look;
            fov = Mathf.Lerp(58, 70, fast);
        }

        void Driver(float dt, out Vector3 pos, out Quaternion rot, out float fov)
        {
            // The head goes with the body and lags a little under acceleration, by the acceleration itself.
            var v = Vehicle.Body.linearVelocity;
            var a = dt > 0 ? (v - lastVelocity) / dt : Vector3.zero;
            lastVelocity = v;
            var local = Vehicle.transform.InverseTransformDirection(a);
            var target = new Vector3(-local.x, 0, -local.z) * 0.006f;
            lean = Vector3.Lerp(lean, Vector3.ClampMagnitude(target, 0.06f), 1 - Mathf.Exp(-6 * dt));
            pos = Vehicle.Eye.TransformPoint(lean);
            rot = Vehicle.transform.rotation * Quaternion.Euler(7 + lookPitch, lookYaw, 0);
            fov = 72;
        }
    }
}
