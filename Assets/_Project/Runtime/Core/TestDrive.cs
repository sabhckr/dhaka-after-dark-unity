// Driving (E-04, E-06). Player hands a car over with Take, and the exit key then asks Player to get out. Without a Player (tests, benches) entry is the old one: from the free camera, F8 puts a
// vehicle on the nearest lane and hands the controls to it, and the exit key hands them back. Real entry and exit (E-06) replaces this.
using Dad.Traffic;
using Dad.World;
using UnityEngine;

namespace Dad.Core
{
    public sealed class TestDrive : MonoBehaviour
    {
        public GameInput Input;
        public TrafficView Traffic;
        public DayCycle Day;
        public PlayerVehicle Vehicle { get; private set; }
        /// <summary>Someone is at the wheel: the exit key asks them to get out, and there is no spawning a car with F8.</summary>
        public System.Action Leave;

        VehicleCamera view;
        FreeCamera free;
        float painted;

        void Update()
        {
            if (Input == null) return;
            if (Vehicle == null)
            {
                if (Leave == null && Input.TestDrive.WasPressedThisFrame()) Begin(VehicleKind.Sedan);
                return;
            }
            if (Input.ExitVehicle.WasPressedThisFrame())
            {
                if (Leave != null) Leave();
                else End();
                return;
            }
            if (Input.CameraView.WasPressedThisFrame()) view.Toggle();
            var s = Vehicle.State;
            if (Input.Ignition.WasPressedThisFrame()) s.EngineOn = !s.EngineOn;
            if (Input.Headlamps.WasPressedThisFrame()) s.HeadLamps = !s.HeadLamps;
            if (Input.Hazards.WasPressedThisFrame()) s.Hazards = !s.Hazards;
            if (Input.IndicateLeft.WasPressedThisFrame()) s.Indicator = s.Indicator < 0 ? 0 : -1;
            if (Input.IndicateRight.WasPressedThisFrame()) s.Indicator = s.Indicator > 0 ? 0 : 1;
            if (Input.Repaint.WasPressedThisFrame()) NextPaint();
            Vehicle.Input = new Dad.Vehicles.DriveInput
            {
                Throttle = Input.Throttle.ReadValue<float>(), Brake = Input.Brake.ReadValue<float>(), Steer = Input.Steer.ReadValue<float>(),
                Handbrake = Input.Handbrake.IsPressed(),
            };
            Vehicle.HornOn = Input.Horn.IsPressed();
            view.Look = FootCamera.Degrees(Input.DriveLook, false);
        }

        /// <summary>The next of the twenty base colours on the vehicle you are in, in the same state of wear.</summary>
        public void NextPaint()
        {
            var job = Vehicle.Job;
            job.Base = (job.Base + 1) % VehiclePaints.Count;
            job.Second = -1;
            Vehicle.Repaint(job);
            painted = Time.unscaledTime;
        }

        void OnGUI()
        {
            if (Vehicle == null || Time.unscaledTime - painted > 2.5f || painted == 0) return;
            GUI.Label(new Rect(24, Screen.height - 48, 400, 28), VehiclePaint.All[Vehicle.Job.Base].Name);
        }

        /// <summary>Puts a vehicle on the nearest lane, facing along it, and takes the controls.</summary>
        public bool Begin(VehicleKind kind)
        {
            var cam = Camera.main;
            var origin = FloatingOrigin.Active;
            if (cam == null || origin == null || Traffic == null || Traffic.Net == null) return false;
            var p = cam.transform.position;
            origin.Offset.ToWorld(p.x, p.z, out double east, out double north);
            if (!Traffic.NearestLane(east, north, out double x, out double z, out double hx, out double hz, out double y)) return false;
            origin.Offset.ToScene(x, z, out float sx, out float sz);
            float heading = Mathf.Atan2((float)hx, (float)hz) * Mathf.Rad2Deg;
            Vehicle = PlayerVehicle.Spawn(kind, new Vector3(sx, (float)y + 0.2f, sz), heading, Day);
            if (Vehicle == null) return false;
            Vehicle.AddSound();
            free = cam.GetComponent<FreeCamera>();
            if (free != null) free.enabled = false;
            view = cam.GetComponent<VehicleCamera>() ?? cam.gameObject.AddComponent<VehicleCamera>();
            view.enabled = true;
            view.Attach(Vehicle);
            origin.Target = Vehicle.transform;
            Traffic.Player = Vehicle;
            if (Traffic.GetComponent<TrafficColliders>() == null) Traffic.gameObject.AddComponent<TrafficColliders>().View = Traffic;
            Input.Switch(InputContext.Driving);
            return true;
        }

        /// <summary>Hands the controls of a car that is already there to whoever just sat in it.</summary>
        public void Take(PlayerVehicle car)
        {
            var cam = Camera.main;
            Vehicle = car;
            view = cam.GetComponent<VehicleCamera>() ?? cam.gameObject.AddComponent<VehicleCamera>();
            view.enabled = true;
            view.Attach(car);
            if (FloatingOrigin.Active != null) FloatingOrigin.Active.Target = car.transform;
            Input?.Switch(InputContext.Driving);
        }

        /// <summary>Gives the controls back, leaving the car where it stands.</summary>
        public void Release()
        {
            if (view != null) view.enabled = false;
            var cam = Camera.main;
            if (cam != null) cam.nearClipPlane = VehicleCamera.ChaseNear;
            Vehicle.Input = default;
            Vehicle.HornOn = false;
            Vehicle = null;
        }

        public void End()
        {
            var origin = FloatingOrigin.Active;
            var cam = Camera.main;
            if (origin != null && cam != null) origin.Target = cam.transform;
            if (view != null) view.enabled = false;
            if (free != null) free.enabled = true;
            if (cam != null) cam.nearClipPlane = VehicleCamera.ChaseNear;
            Traffic.Player = null;
            Destroy(Vehicle.gameObject);
            Vehicle = null;
            Input.Switch(InputContext.FreeCamera);
        }
    }
}
