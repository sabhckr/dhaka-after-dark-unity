// Draws the traffic (E-05, F-03) and keeps the simulation fed: lanes built on the streaming workers join the TrafficNet as their chunks
// come in and leave with them; the simulation runs in fixed steps, and each frame every vehicle in view is drawn where it is between
// two steps. Nothing here touches a GameObject per vehicle: one instanced draw per family and level of detail.
//
// A vehicle under NearRange from the camera is drawn whole: body, cabin, glass and wheels turning on their own, with the small far
// mesh casting its shadow for it. Beyond that it is the far mesh, baked wheels and all; shadows stop at ShadowRange and drawing at
// DrawRange. In a shot (ViewShot) the clock is held: the view says how many seconds to run, and the picture is taken with no
// more steps. Traffic is off in bench runs and golden views unless they ask for it (-dad-traffic, ViewSpec.traffic).
using System;
using System.Collections.Generic;
using Dad.Traffic;
using Dad.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace Dad.Core
{
    public sealed class TrafficView : MonoBehaviour
    {
        public const float NearRange = 60f, ShadowRange = 160f, DrawRange = 460f;
        /// <summary>Spot lights, for the nearest vehicles with their lamps on, and how far from the camera they reach.</summary>
        public const int Beams = 8;
        public const float BeamRange = 90f;
        const int MaxStepsPerFrame = 3;

        public ChunkStreamer Streamer;
        public DayCycle Day;
        public ulong Seed = 1;
        public int Budget = 300;
        /// <summary>True in a shot: the simulation moves only when told to (Run).</summary>
        public bool Manual;
        /// <summary>The vehicle a player is driving, or null (E-04).</summary>
        public PlayerVehicle Player;

        public TrafficSim Sim { get; private set; }
        public TrafficNet Net { get; private set; }
        public bool Ready => populated;
        /// <summary>For a report: what it has heard from streaming.</summary>
        public string State => $"connected {connected}, lanes in {lanesIn}, waiting {waiting.Count}, streamer {(Streamer == null ? "none" : $"index {Streamer.IndexReady}, idle {Streamer.Idle}")}, build {ChunkStreamer.BuildLanes}";
        /// <summary>Vehicles drawn last frame, whole and far.</summary>
        public int DrawnNear { get; private set; }
        public int DrawnFar { get; private set; }

        sealed class Family
        {
            public VehicleModel Model;
            public Mesh Near, Glass, Far;
            /// <summary>The body material, or the family's own where it has a picture.</summary>
            public Material Body;
            public VehicleBatch Whole = new VehicleBatch(96), Shade = new VehicleBatch(96), Distant = new VehicleBatch(420), Faint = new VehicleBatch(420);
            public Matrix4x4[] WheelLocal;
            /// <summary>The paint jobs this family comes in; a vehicle wears the one its number picks.</summary>
            public VehicleLook[] Looks;
        }

        static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");

        const int Families = 16, Jobs = 64;
        readonly Family[] families = new Family[Families];
        // The shared generated wheels, then each family's own.
        readonly List<Mesh> wheelMesh = new List<Mesh>();
        readonly List<VehicleBatch> wheels = new List<VehicleBatch>();
        readonly List<Material> wheelLook = new List<Material>();
        readonly Dictionary<(int, int), TChunk> chunks = new Dictionary<(int, int), TChunk>();
        readonly List<ChunkLanes> waiting = new List<ChunkLanes>();
        readonly Queue<ChunkCell> leaving = new Queue<ChunkCell>();
        readonly Plane[] planes = new Plane[6];
        Material body, glass;
        const float BeamIntensity = 60f;
        readonly float[] beamRange = new float[Beams];
        readonly Vector3[] beamAt = new Vector3[Beams], beamDir = new Vector3[Beams];
        Light[] beams;
        int beamCount;
        List<Agent> parade;
        Vector4 paradeLamps;
        double clock;
        int lanesIn;
        bool populated, connected;

        /// <summary>Turns traffic on. Call before streaming starts: lanes are built with the chunks.</summary>
        public static TrafficView Attach(GameObject host, ChunkStreamer streamer, DayCycle day)
        {
            ChunkStreamer.BuildLanes = true;
            var v = host.AddComponent<TrafficView>();
            v.Streamer = streamer;
            v.Day = day;
            v.Connect();
            return v;
        }

        /// <summary>Turns a command line's -dad-traffic into a view.</summary>
        public static TrafficView FromCommandLine(GameObject host, ChunkStreamer streamer, DayCycle day)
        {
            var args = Environment.GetCommandLineArgs();
            return Array.IndexOf(args, "-dad-traffic") < 0 ? null : Attach(host, streamer, day);
        }

        void Awake()
        {
            body = Resources.Load<Material>("Materials/Vehicle");
            glass = Resources.Load<Material>("Materials/VehicleGlass");
            for (int k = 0; k < families.Length; k++)
            {
                if (TrafficSim.Mix((VehicleKind)k, 12) <= 0 && TrafficSim.Mix((VehicleKind)k, 1) <= 0) continue; // Only ever parked or afloat: ParkedView draws those.
                var model = Model((VehicleKind)k, wheelMesh.Count);
                var picture = Resources.Load<Texture2D>("Vehicles/" + model.Kind + "Map");
                var look = body;
                if (picture != null)
                {
                    look = new Material(body) { name = model.Kind + " body" };
                    look.SetTexture(BaseMapId, picture);
                }
                if (model.OwnWheels != null)
                    foreach (var own in model.OwnWheels)
                    {
                        wheelMesh.Add(ToMesh(own, model.Kind + " wheel"));
                        wheels.Add(new VehicleBatch(320));
                        wheelLook.Add(look);
                    }
                var f = new Family { Model = model, Body = look, Near = ToMesh(model.Near, model.Kind + " near"), Glass = model.Glass.Triangles > 0 ? ToMesh(model.Glass, model.Kind + " glass") : null, Far = ToMesh(model.Far, model.Kind + " far"), Looks = Looks((VehicleKind)k, model.Height) };
                f.WheelLocal = new Matrix4x4[model.Wheels.Length];
                families[k] = f;
            }
        }

        void OnEnable() => Connect();

        void OnDisable()
        {
            if (!connected) return;
            connected = false;
            Streamer.LanesIn -= OnLanes;
            Streamer.ChunkOut -= OnChunkOut;
        }

        void Connect()
        {
            if (connected || Streamer == null) return;
            connected = true;
            Streamer.LanesIn += OnLanes;
            Streamer.ChunkOut += OnChunkOut;
        }

        void OnDestroy()
        {
            ChunkStreamer.BuildLanes = false;
            foreach (var f in families)
            {
                if (f == null) continue;
                Destroy(f.Near);
                Destroy(f.Far);
                if (f.Glass != null) Destroy(f.Glass);
                if (f.Body != body) Destroy(f.Body);
            }
            foreach (var m in wheelMesh) if (m != null) Destroy(m);
        }

        void OnLanes(ChunkCell cell, ChunkLanes lanes)
        {
            lanesIn++;
            waiting.Add(lanes);
        }

        void OnChunkOut(ChunkCell cell) => leaving.Enqueue(cell);

        /// <summary>Takes in what streaming has brought and sent away, at most one chunk a frame once the road is running.</summary>
        void Feed()
        {
            using (DadMarkers.TrafficStream.Auto())
            {
                if (Sim == null)
                {
                    Net = new TrafficNet();
                    Sim = new TrafficSim(Net, Seed) { Budget = Budget };
                }
                while (leaving.Count > 0)
                {
                    var cell = leaving.Dequeue();
                    waiting.RemoveAll(l => l.ChunkX == cell.Ix && l.ChunkY == cell.Iy);
                    if (!chunks.TryGetValue((cell.Ix, cell.Iy), out var ch)) continue;
                    chunks.Remove((cell.Ix, cell.Iy));
                    Sim.RemoveChunk(ch);
                    if (populated) Net.Update();
                }
                if (!populated)
                {
                    if (Streamer == null || !Streamer.IndexReady || !Streamer.Idle || waiting.Count == 0) return;
                    // All at once, in a fixed order, so a seed gives the same traffic however the chunks arrived.
                    waiting.Sort((a, b) => a.ChunkY != b.ChunkY ? a.ChunkY.CompareTo(b.ChunkY) : a.ChunkX.CompareTo(b.ChunkX));
                    foreach (var l in waiting) chunks[(l.ChunkX, l.ChunkY)] = Sim.AddChunk(l, false);
                    waiting.Clear();
                    Net.Update();
                    Focus();
                    Sim.Populate();
                    populated = true;
                    return;
                }
                if (waiting.Count == 0) return;
                var next = waiting[waiting.Count - 1];
                waiting.RemoveAt(waiting.Count - 1);
                chunks[(next.ChunkX, next.ChunkY)] = Sim.AddChunk(next);
            }
        }

        void Focus()
        {
            var cam = Camera.main;
            var origin = FloatingOrigin.Active;
            if (cam == null || origin == null) return;
            var p = cam.transform.position;
            origin.Offset.ToWorld(p.x, p.z, out double east, out double north);
            Sim.FocusX = east;
            Sim.FocusZ = north;
            var f = cam.transform.forward;
            double l = Math.Sqrt(f.x * f.x + f.z * f.z);
            if (l > 1e-4) { Sim.ViewX = f.x / l; Sim.ViewZ = f.z / l; }
            if (Day != null) Sim.Hour = Day.Hours;
        }

        void Update()
        {
            Feed();
            if (!populated || Manual) return;
            Focus();
            clock += Time.deltaTime;
            int steps = 0;
            using (DadMarkers.TrafficStep.Auto())
                while (clock >= TrafficSim.Step)
                {
                    clock -= TrafficSim.Step;
                    if (steps++ < MaxStepsPerFrame)
                    {
                        Tell();
                        Sim.Advance();
                    }
                }
        }

        /// <summary>
        /// For looking at the models: one of each family standing in a line from (east, north) along a heading, Spacing apart, lamps as told,
        /// in place of the traffic. For review shots (ViewSpec.parade); the simulation does not run.
        /// </summary>
        public void Parade(double east, double north, double dirEast, double dirNorth, double spacing, Vector4 lamps)
        {
            double l = Math.Sqrt(dirEast * dirEast + dirNorth * dirNorth);
            dirEast /= l;
            dirNorth /= l;
            parade = new List<Agent>();
            paradeLamps = lamps;
            double road = RoadHeight(east, north);
            foreach (var sp in VehicleSpec.All)
            {
                if (families[(int)sp.Kind] == null) continue;
                double along = parade.Count * spacing;
                parade.Add(new Agent
                {
                    Id = parade.Count, Spec = sp, Y = road, Spin = 0.4, PrevSpin = 0.4, Steer = sp.Kind == VehicleKind.Sedan ? 0.25 : 0,
                    X = east + dirEast * along, Z = north + dirNorth * along, PrevX = east + dirEast * along, PrevZ = north + dirNorth * along,
                    Hx = dirEast, Hz = dirNorth, PrevHx = dirEast, PrevHz = dirNorth,
                });
            }
        }

        /// <summary>The height of the lane nearest this point.</summary>
        // Tells the simulation where the driven vehicle is. Null when nobody is driving, so traffic is as it always was.
        void Tell()
        {
            if (Player == null) { Sim.Driven = null; return; }
            var d = Sim.Driven ??= new DrivenVehicle();
            var t = Player.transform;
            var at = t.position;
            var v = Player.Body.linearVelocity;
            FloatingOrigin.Active.Offset.ToWorld(at.x, at.z, out d.X, out d.Z);
            d.Y = at.y;
            var f = t.forward;
            float flat = Mathf.Sqrt(f.x * f.x + f.z * f.z);
            d.Hx = flat > 1e-4f ? f.x / flat : 0;
            d.Hz = flat > 1e-4f ? f.z / flat : 1;
            d.Vx = v.x;
            d.Vz = v.z;
            d.Length = Player.Length;
            d.Width = Player.Width;
        }

        /// <summary>Where an agent is drawn, its road height and its heading, in the scene.</summary>
        public bool Place(Agent a, out Vector3 at, out Quaternion turn)
        {
            double t = Manual ? 1 : clock / TrafficSim.Step;
            double east = a.PrevX + (a.X - a.PrevX) * t, north = a.PrevZ + (a.Z - a.PrevZ) * t;
            FloatingOrigin.Active.Offset.ToScene(east, north, out float x, out float z);
            at = new Vector3(x, (float)a.Y, z);
            float hx = (float)(a.PrevHx + (a.Hx - a.PrevHx) * t), hz = (float)(a.PrevHz + (a.Hz - a.PrevHz) * t);
            if (hx * hx + hz * hz < 1e-8f) { turn = Quaternion.identity; return false; }
            turn = Quaternion.LookRotation(new Vector3(hx, 0, hz));
            return true;
        }

        /// <summary>The middle of the nearest road lane to a point: where it is, world metres, its heading and its height.</summary>
        public bool NearestLane(double east, double north, out double x, out double z, out double hx, out double hz, out double y)
        {
            x = z = y = 0;
            hx = 0;
            hz = 1;
            double best = double.MaxValue;
            if (Net == null) return false;
            foreach (var ch in Net.Chunks)
                foreach (var l in ch.All)
                {
                    if (l.D.Connector || l.D.Pts.Length < 4) continue;
                    for (int i = 0; i + 3 < l.D.Pts.Length; i += 2)
                    {
                        double ax = l.Ox + l.D.Pts[i], az = l.Oz + l.D.Pts[i + 1], bx = l.Ox + l.D.Pts[i + 2], bz = l.Oz + l.D.Pts[i + 3];
                        double mx = (ax + bx) / 2, mz = (az + bz) / 2, d = (mx - east) * (mx - east) + (mz - north) * (mz - north), n = Math.Sqrt((bx - ax) * (bx - ax) + (bz - az) * (bz - az));
                        if (d >= best || n < 1e-3) continue;
                        best = d;
                        x = mx;
                        z = mz;
                        hx = (bx - ax) / n;
                        hz = (bz - az) / n;
                        y = l.D.Y;
                    }
                }
            return best < double.MaxValue;
        }

        double RoadHeight(double east, double north)
        {
            double y = 0, best = double.MaxValue;
            if (Net == null) return y;
            foreach (var c in Net.Chunks)
                foreach (var l in c.All)
                {
                    if (l.D.Connector) continue;
                    l.At(l.D.Length / 2, out double x, out double z, out _, out _);
                    double d = (x - east) * (x - east) + (z - north) * (z - north);
                    if (d < best) { best = d; y = l.D.Y; }
                }
            return y;
        }

        /// <summary>Runs the simulation for this many seconds at once (a shot's warm-up).</summary>
        public void Run(double seconds)
        {
            Feed();
            if (!populated) return;
            Focus();
            for (int i = 0, n = (int)(seconds / TrafficSim.Step); i < n; i++) Sim.Advance();
        }

        void LateUpdate()
        {
            if (!Manual) Submit();
        }

        /// <summary>Hands this frame's vehicles to the renderer.</summary>
        public void Submit()
        {
            if (!populated && parade == null) return;
            var cam = Camera.main;
            var origin = FloatingOrigin.Active;
            if (cam == null || origin == null) return;
            using (DadMarkers.TrafficDraw.Auto())
            {
                var eye = cam.transform.position;
                GeometryUtility.CalculateFrustumPlanes(cam, planes);
                double t = Manual ? 1 : clock / TrafficSim.Step;
                float night = HeadlampsOn(Day != null ? Day.Hours : 12);
                bool blink = Sim != null && Sim.Time % 1.0 < 0.5;
                int near = 0, far = 0;
                foreach (var a in parade ?? Sim.Agents)
                {
                    double east = a.PrevX + (a.X - a.PrevX) * t, north = a.PrevZ + (a.Z - a.PrevZ) * t;
                    origin.Offset.ToScene(east, north, out float x, out float z);
                    var at = new Vector3(x, (float)a.Y, z);
                    var d = at - eye;
                    float range = d.magnitude;
                    if (range > DrawRange) continue;
                    float reach = (float)a.Spec.Length * 0.6f + 1f;
                    if (!InView(at, reach)) continue;
                    float hx = (float)(a.PrevHx + (a.Hx - a.PrevHx) * t), hz = (float)(a.PrevHz + (a.Hz - a.PrevHz) * t);
                    float hl = Mathf.Sqrt(hx * hx + hz * hz);
                    if (hl < 1e-4f) continue;
                    hx /= hl;
                    hz /= hl;
                    var f = families[(int)a.Spec.Kind];
                    if (f == null) continue;
                    var m = Pose(a, at, hx, hz);
                    ref var paint = ref f.Looks[(int)(((uint)a.Id * 2654435761u >> 8) % Jobs)];
                    var lamps = parade != null ? paradeLamps : new Vector4(night, a.Braking ? 1 : 0, blink && a.Indicator > 0 ? 1 : 0, blink && a.Indicator < 0 ? 1 : 0);
                    if (night > 0 && range < BeamRange) ConsiderBeam(a, at, range, hx, hz);
                    if (range < NearRange && !f.Whole.Full)
                    {
                        near++;
                        f.Whole.Add(m, paint, lamps);
                        f.Shade.Add(m, paint, lamps);
                        Wheels(f, a, m, t);
                    }
                    else
                    {
                        far++;
                        (range < ShadowRange ? f.Distant : f.Faint).Add(m, paint, lamps);
                    }
                }
                DrawnNear = near;
                DrawnFar = far;
                PlaceBeams(night);
                var bounds = new Bounds(eye, Vector3.one * (DrawRange * 2.2f));
                foreach (var f in families)
                {
                    if (f == null) continue;
                    f.Whole.Flush(f.Near, f.Body, ShadowCastingMode.Off, bounds, false);
                    if (f.Glass != null) f.Whole.Flush(f.Glass, glass, ShadowCastingMode.Off, bounds, false);
                    f.Shade.Flush(f.Far, f.Body, ShadowCastingMode.ShadowsOnly, bounds, true);
                    f.Distant.Flush(f.Far, f.Body, ShadowCastingMode.On, bounds, true);
                    f.Faint.Flush(f.Far, f.Body, ShadowCastingMode.Off, bounds, true);
                    f.Whole.Count = 0;
                }
                for (int w = 0; w < wheels.Count; w++) wheels[w].Flush(wheelMesh[w], wheelLook[w], ShadowCastingMode.Off, bounds, true);
            }
        }

        /// <summary>Keeps the nearest few vehicles for the lights, nearest first.</summary>
        void ConsiderBeam(Agent a, Vector3 at, float range, float hx, float hz)
        {
            if (a.Spec.Kind == VehicleKind.Rickshaw) return; // a cycle rickshaw has no headlamp
            int i = Math.Min(beamCount, Beams - 1);
            if (beamCount == Beams && range >= beamRange[i]) return;
            while (i > 0 && beamRange[i - 1] > range)
            {
                beamRange[i] = beamRange[i - 1];
                beamAt[i] = beamAt[i - 1];
                beamDir[i] = beamDir[i - 1];
                i--;
            }
            var dir = new Vector3(hx, 0, hz);
            beamRange[i] = range;
            beamAt[i] = at + dir * (float)(a.Spec.Length * 0.5 + 0.1) + Vector3.up * 0.75f;
            beamDir[i] = dir + Vector3.down * 0.1f;
            if (beamCount < Beams) beamCount++;
        }

        void PlaceBeams(float night)
        {
            if (beams == null && night > 0)
            {
                beams = new Light[Beams];
                for (int i = 0; i < Beams; i++)
                {
                    var l = new GameObject("Headlamp").AddComponent<Light>();
                    l.transform.SetParent(transform, false);
                    l.type = LightType.Spot;
                    l.range = 34;
                    l.spotAngle = 78;
                    l.innerSpotAngle = 34;
                    l.intensity = BeamIntensity;
                    l.color = new Color(1f, 0.93f, 0.8f);
                    l.shadows = LightShadows.None;
                    beams[i] = l;
                }
            }
            if (beams == null) return;
            for (int i = 0; i < Beams; i++)
            {
                bool on = i < beamCount;
                if (beams[i].enabled != on) beams[i].enabled = on;
                if (!on) continue;
                beams[i].transform.SetPositionAndRotation(beamAt[i], Quaternion.LookRotation(beamDir[i]));
                beams[i].intensity = BeamIntensity * night; // up with the lamps at dusk: eight lights must not switch on at once
            }
            beamCount = 0;
        }

        bool InView(Vector3 at, float reach)
        {
            var c = at + Vector3.up * 1.2f;
            for (int i = 0; i < 6; i++)
                if (planes[i].GetDistanceToPoint(c) < -reach) return false;
            return true;
        }

        /// <summary>The body's frame: on the road along its heading, dipping as it brakes, a motorcycle leaning into its bend.</summary>
        static Matrix4x4 Pose(Agent a, Vector3 at, float hx, float hz)
        {
            var sp = a.Spec;
            float pitch = Mathf.Clamp((float)a.A * -0.0035f, -0.02f, 0.02f);
            float roll = 0;
            if (sp.Group == Access.Motorcycle)
            {
                double curve = Math.Tan(a.Steer) / sp.WheelBase;
                roll = Mathf.Clamp((float)(a.V * a.V * curve / 9.8), -0.35f, 0.35f);
            }
            var f = new Vector3(hx, -pitch, hz).normalized;
            var r = new Vector3(hz, 0, -hx);
            var u = Vector3.Cross(f, r);
            if (roll != 0)
            {
                float c = Mathf.Cos(roll), s = Mathf.Sin(roll);
                var r2 = r * c + u * s;
                u = u * c - r * s;
                r = r2;
            }
            var m = new Matrix4x4();
            m.SetColumn(0, new Vector4(r.x, r.y, r.z, 0));
            m.SetColumn(1, new Vector4(u.x, u.y, u.z, 0));
            m.SetColumn(2, new Vector4(f.x, f.y, f.z, 0));
            m.SetColumn(3, new Vector4(at.x, at.y, at.z, 1));
            return m;
        }

        void Wheels(Family f, Agent a, in Matrix4x4 m, double t)
        {
            float spin = (float)((a.PrevSpin + (a.Spin - a.PrevSpin) * t) * Mathf.Rad2Deg), steer = (float)(-a.Steer * Mathf.Rad2Deg);
            var ws = f.Model.Wheels;
            for (int i = 0; i < ws.Length; i++)
            {
                var w = ws[i];
                var turn = Quaternion.Euler(0, w.Steers ? steer : 0, 0);
                if (w.Flip) turn *= Quaternion.Euler(0, 180, 0);
                turn *= Quaternion.Euler(w.Flip ? -spin : spin, 0, 0);
                wheels[w.Mesh].Add(m * Matrix4x4.TRS(new Vector3((float)w.X, (float)w.Y, (float)w.Z), turn, Vector3.one), default(VehicleLook), default);
            }
        }

        /// <summary>How far on the lamps are: from dusk to dawn, and a little ahead of it.</summary>
        public static float HeadlampsOn(double h) => h >= 18.0 || h < 5.8 ? 1 : h >= 17.4 ? (float)((h - 17.4) / 0.6) : h < 6.2 ? (float)((6.2 - h) / 0.4) : 0;

        static VehicleLook[] Looks(VehicleKind k, double height)
        {
            var v = new VehicleLook[Jobs];
            for (int i = 0; i < Jobs; i++) v[i] = VehicleLook.Of(VehiclePaints.For(k, (uint)i), height);
            return v;
        }

        /// <summary>The owner's model of this family, as baked into Resources/Vehicles.</summary>
        public static VehicleModel Model(VehicleKind kind, int wheelMesh)
        {
            var art = Resources.Load<TextAsset>("Vehicles/" + kind);
            if (art == null) throw new InvalidOperationException("no baked model for " + kind);
            return VehicleArt.Read(kind, art.bytes, wheelMesh);
        }

        /// <summary>A kit mesh as a Unity mesh; only `keep` leaves it readable.</summary>
        public static Mesh ToMesh(KitMesh k, string name, bool keep = false)
        {
            int n = k.Vertices;
            var pos = new Vector3[n];
            var nor = new Vector3[n];
            var col = new Color32[n];
            var uv = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                pos[i] = new Vector3(k.Pos[3 * i], k.Pos[3 * i + 1], k.Pos[3 * i + 2]);
                nor[i] = new Vector3(k.Nor[3 * i], k.Nor[3 * i + 1], k.Nor[3 * i + 2]);
                col[i] = new Color32(k.Rgba[4 * i], k.Rgba[4 * i + 1], k.Rgba[4 * i + 2], k.Rgba[4 * i + 3]);
                uv[i] = new Vector2(k.Lamp[i], 0);
            }
            var m = new Mesh { name = name, indexFormat = n > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            m.vertices = pos;
            m.normals = nor;
            m.colors32 = col;
            m.uv = uv;
            if (k.Map.Count > 0)
            {
                var map = new Vector2[n];
                for (int i = 0; i < n; i++) map[i] = new Vector2(k.Map[2 * i], k.Map[2 * i + 1]);
                m.uv2 = map;
            }
            m.SetTriangles(k.Tris, 0);
            m.RecalculateBounds();
            m.UploadMeshData(!keep);
            return m;
        }
    }
}
