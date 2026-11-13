// For a bench (E-04): what a driven vehicle costs on a camera path. A bench's camera follows its own path, so the car does not drive:
// it stands on the lane nearest the camera, engine running, and is moved up every MoveEvery seconds. So the wheels probe, the physics
// steps and the car is drawn all the way, and what is not measured is contact with the world at speed. Only with -dad-drive.
using Dad.Traffic;
using UnityEngine;

namespace Dad.Core
{
    public sealed class BenchDrive : MonoBehaviour
    {
        const float MoveEvery = 8;

        public TrafficView Traffic;
        public DayCycle Day;

        PlayerVehicle car;
        float next;

        void Update()
        {
            var cam = Camera.main;
            var origin = FloatingOrigin.Active;
            if (Traffic == null || !Traffic.Ready || cam == null || origin == null || Time.time < next) return;
            next = Time.time + MoveEvery;
            var p = cam.transform.position;
            origin.Offset.ToWorld(p.x, p.z, out double east, out double north);
            if (!Traffic.NearestLane(east, north, out double x, out double z, out double hx, out double hz, out double y)) return;
            origin.Offset.ToScene(x, z, out float sx, out float sz);
            var at = new Vector3(sx, (float)y + 0.05f, sz);
            float heading = Mathf.Atan2((float)hx, (float)hz) * Mathf.Rad2Deg;
            if (car == null)
            {
                car = PlayerVehicle.Spawn(VehicleKind.Sedan, at, heading, Day);
                car.gameObject.AddComponent<VehicleSound>().Vehicle = car;
                Traffic.Player = car;
                Traffic.gameObject.AddComponent<TrafficColliders>().View = Traffic;
                return;
            }
            car.Body.position = at;
            car.Body.rotation = Quaternion.Euler(0, heading, 0);
            car.Body.linearVelocity = Vector3.zero;
            car.Body.angularVelocity = Vector3.zero;
        }
    }
}
