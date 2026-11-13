// The train on the Port Line (2026-11-06). The owner's metro car on the ground line the world already has: TrainRun says where along it the train is, the rail heads in World/transport.json say
// how high, and each car stands on its two bogies. It is drawn instanced like any other city model, so it costs what the parked ones at the metro stations cost; four kinematic boxes go with it
// when anyone is near, and it shuts the level crossings as it comes. The model has no separate doors or bogies, so nothing on it opens or turns: DoorsOpen is when boarding is allowed.
using Dad.Traffic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Dad.Core
{
    public sealed class TrainView : MonoBehaviour
    {
        public const int Cars = 4;
        public const double Pitch = 23, Bogies = 7.5;
        public const float BoardReach = 3.2f, NearRange = 160, DrawRange = 1300, BoxRange = 220, ShutRange = 230;

        /// <summary>True in a shot: it stays where it was put.</summary>
        public bool Manual;
        public TrainRun Run { get; private set; }
        public TrafficView Traffic;
        /// <summary>Where each car stands now, in the scene: tests and boarding.</summary>
        public readonly Vector3[] At = new Vector3[Cars];
        public readonly Quaternion[] Turn = new Quaternion[Cars];

        readonly Matrix4x4[] near = new Matrix4x4[Cars], far = new Matrix4x4[Cars];
        readonly Rigidbody[] bodies = new Rigidbody[Cars];
        readonly Transform[] seats = new Transform[Cars];
        MaterialPropertyBlock props;
        TransportFile.Railway rail;
        CityModel model;
        CrossingHold[] holds;
        bool holdsGiven;

        public static TrainView Attach(GameObject host, TransportFile file, TrafficView traffic)
        {
            if (file?.railway?.points == null || file.railway.points.Length < 4) return null;
            var model = CityModels.Get("metro_car");
            if (model == null) return null;
            var v = host.AddComponent<TrainView>();
            v.rail = file.railway;
            v.model = model;
            v.Traffic = traffic;
            v.Run = new TrainRun(new TrainLine(file.railway.points), file.railway.stops);
            v.Build();
            return v;
        }

        void Build()
        {
            props = new MaterialPropertyBlock();
            var tints = new Vector4[Cars];
            for (int i = 0; i < Cars; i++) tints[i] = new Vector4(1, 1, 1, 1);
            props.SetVectorArray("_Tint", tints);
            int n = rail.crossingX?.Length ?? 0;
            holds = new CrossingHold[n];
            for (int i = 0; i < n; i++) holds[i] = new CrossingHold { X = rail.crossingX[i], Z = rail.crossingZ[i], Half = rail.crossingHalf[i] };
            for (int k = 0; k < Cars; k++)
            {
                var go = new GameObject("Train car") { layer = WorldLayers.Traffic };
                go.transform.SetParent(transform, false);
                bodies[k] = go.AddComponent<Rigidbody>();
                bodies[k].isKinematic = true;
                var box = go.AddComponent<BoxCollider>();
                box.size = model.Size;
                box.center = new Vector3(0, model.Size.y / 2, 0);
                seats[k] = new GameObject("Seat").transform;
                seats[k].SetParent(go.transform, false);
                seats[k].localPosition = new Vector3(0, model.Size.y - 1.4f, -4);
                go.SetActive(false);
            }
        }

        float Level(double s)
        {
            float k = (float)(s / rail.levelStep);
            int i = Mathf.Clamp((int)k, 0, rail.levels.Length - 2);
            return Mathf.Lerp(rail.levels[i], rail.levels[i + 1], Mathf.Clamp01(k - i));
        }

        void Update()
        {
            if (FloatingOrigin.Active == null) return;
            if (!Manual) Run.Step(Time.deltaTime);
            Shut();
        }

        void Shut()
        {
            if (!holdsGiven && Traffic != null && Traffic.Sim != null)
            {
                Traffic.Sim.Crossings.AddRange(holds);
                holdsGiven = true;
            }
            Run.Line.Point(Run.S, out double x, out double z);
            foreach (var h in holds)
            {
                double d = System.Math.Sqrt((h.X - x) * (h.X - x) + (h.Z - z) * (h.Z - z));
                h.Closed = d < Pitch * Cars / 2 + 8 || (Run.Now == TrainRun.Doing.Running && d < ShutRange);
            }
        }

        void LateUpdate() => Submit();

        /// <summary>Places and draws this frame's cars. ViewShot calls it again just before its own render, which otherwise misses what was drawn in LateUpdate.</summary>
        public void Submit()
        {
            var origin = FloatingOrigin.Active;
            var cam = Camera.main;
            if (origin == null || cam == null) return;
            int a = 0, b = 0;
            var eye = cam.transform.position;
            for (int k = 0; k < Cars; k++)
            {
                double s = Run.S + (k - (Cars - 1) / 2.0) * Pitch;
                Run.Line.Car(s, Bogies, out double px, out double pz, out double hx, out double hz);
                origin.Offset.ToScene(px, pz, out float x, out float z);
                At[k] = new Vector3(x, Level(s), z);
                // Two cars each way, so that the cabs are the train's two ends.
                float sign = k < Cars / 2 ? -1 : 1;
                Turn[k] = Quaternion.LookRotation(new Vector3((float)hx * sign, 0, (float)hz * sign));
                float d = (At[k] - eye).sqrMagnitude;
                if (d < NearRange * NearRange) near[a++] = Matrix4x4.TRS(At[k], Turn[k], Vector3.one);
                else if (d < DrawRange * DrawRange) far[b++] = Matrix4x4.TRS(At[k], Turn[k], Vector3.one);
                bool boxed = d < BoxRange * BoxRange;
                if (bodies[k].gameObject.activeSelf != boxed) bodies[k].gameObject.SetActive(boxed);
                if (boxed)
                {
                    bodies[k].transform.SetPositionAndRotation(At[k], Turn[k]);
                    bodies[k].position = At[k];
                    bodies[k].rotation = Turn[k];
                }
            }
            var bounds = new Bounds(eye, Vector3.one * DrawRange * 2.2f);
            Draw(0, near, a, bounds);
            Draw(1, far, b, bounds);
        }

        void Draw(int lod, Matrix4x4[] m, int count, in Bounds bounds)
        {
            if (count == 0) return;
            var mats = model.Materials[lod];
            for (int s = 0; s < mats.Length; s++)
            {
                var rp = new RenderParams(mats[s])
                {
                    worldBounds = bounds,
                    shadowCastingMode = lod == 0 ? ShadowCastingMode.On : ShadowCastingMode.Off,
                    receiveShadows = true,
                    matProps = props,
                    layer = WorldLayers.World,
                    lightProbeUsage = LightProbeUsage.BlendProbes,
                };
                Graphics.RenderMeshInstanced(rp, model.Lods[lod], s, m, count);
            }
        }

        /// <summary>The seat of the car someone standing here could board, while the train stands with its doors open. Null otherwise.</summary>
        public Transform Board(Vector3 from)
        {
            if (!Run.DoorsOpen) return null;
            for (int k = 0; k < Cars; k++)
            {
                var local = Quaternion.Inverse(Turn[k]) * (from - At[k]);
                float x = Mathf.Max(Mathf.Abs(local.x) - model.Size.x / 2, 0), z = Mathf.Max(Mathf.Abs(local.z) - model.Size.z / 2, 0);
                if (Mathf.Sqrt(x * x + z * z) < BoardReach && bodies[k].gameObject.activeSelf) return seats[k];
            }
            return null;
        }

        /// <summary>For a shot: stands the train with its middle at this stop.</summary>
        public void StandAt(int stop) => Run = new TrainRun(Run.Line, Run.Stops, stop);

        /// <summary>For a shot or a test: the train moved on by this many seconds of its timetable.</summary>
        public void RunFor(double seconds)
        {
            for (double t = 0; t < seconds; t += 0.05) Run.Step(0.05);
        }
    }
}
