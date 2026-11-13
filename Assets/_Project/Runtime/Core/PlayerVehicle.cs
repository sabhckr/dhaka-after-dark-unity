// The vehicle a player drives (E-04). A Rigidbody carries the chassis and collides with the world; VehicleDynamics, which knows no engine, works out the forces
// from what its wheels find. Everything shown comes from VehicleState: wheels turn, steer and ride on their springs by the numbers the physics used, with no offset
// of their own, and the body moves only because the Rigidbody does.
//
// The object tree is the same for every family:
//   root (Rigidbody, hull collider)
//     Chassis / Exterior, Windows, Interior, Doors, Lights, Mirrors, Damage, InteractionPoints
//     Wheels / <wheel> (Suspension: rides up and down) / Steer / Spin
// The car is the owner's model, baked by Tools/models/bake.py into Resources/Vehicles/Driven: body, cabin, steering wheel and doors, each a part on its own pivot, at the model's
// own size. Damage is still empty (Docs/PRODUCTION/E04_PLAYER_VEHICLE.md).
using Dad.Traffic;
using Dad.Vehicles;
using UnityEngine;

namespace Dad.Core
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class PlayerVehicle : MonoBehaviour, IGroundProbe, IForceSink
    {
        public const string ChassisName = "Chassis";
        const float BeamDip = 1.5f, BeamIntensity = 900;
        static readonly string[] Groups = { "Exterior", "Windows", "Interior", "Doors", "Lights", "Mirrors", "Damage", "InteractionPoints" };

        public VehicleDynamics Dynamics { get; private set; }
        /// <summary>Set instead of Dynamics on a boat, which has no wheels.</summary>
        public BoatDynamics Boat { get; private set; }
        public VehicleState State => Boat != null ? Boat.State : Dynamics.State;
        /// <summary>Balanced on two wheels: the rider stays in view, on the saddle.</summary>
        public bool TwoWheels => Dynamics != null && Dynamics.Spec.TwoWheels;
        /// <summary>The model opens a driver's door the clips can play against. Everything else is got into without one.</summary>
        public bool HasDoor { get; private set; }
        /// <summary>What it wears.</summary>
        public PaintJob Job { get; private set; }
        public Rigidbody Body { get; private set; }
        public VehicleKind Kind { get; private set; }
        /// <summary>What the driver is asking for; the input code or a test sets it.</summary>
        public DriveInput Input;
        /// <summary>The horn, for VehicleSound.</summary>
        public bool HornOn;
        /// <summary>Nobody at the wheel: the handbrake stays on.</summary>
        public bool Parked;
        /// <summary>The driver's eye and the driver's-side door, on the body.</summary>
        public Transform Eye { get; private set; }
        public Transform DoorPoint { get; private set; }
        public Transform Chassis { get; private set; }
        public Transform Interior { get; private set; }
        public Transform Driver { get; private set; }
        /// <summary>The car's size in metres, from its model.</summary>
        public double Length { get; private set; }
        public double Width { get; private set; }
        public double Height { get; private set; }

        Transform[] suspension, steer, spin;
        Transform steeringWheel, speedNeedle, revNeedle, fuelNeedle;
        Vector3 steeringAxis, dialAxis;
        readonly System.Collections.Generic.List<Renderer> paintRenderers = new System.Collections.Generic.List<Renderer>(), glassRenderers = new System.Collections.Generic.List<Renderer>(), cabinRenderers = new System.Collections.Generic.List<Renderer>();
        readonly System.Collections.Generic.List<Door> doors = new System.Collections.Generic.List<Door>();
        MaterialPropertyBlock props, cabinProps, glassProps;
        VehicleLook look;
        Light[] beams;
        DayCycle day;

        static readonly int TintId = Shader.PropertyToID("_Tint"), LampsId = Shader.PropertyToID("_Lamps"), ClearId = Shader.PropertyToID("_Clear"), FinishId = Shader.PropertyToID("_Finish"), SecondId = Shader.PropertyToID("_Second"), BaseMapId = Shader.PropertyToID("_BaseMap");

        sealed class Door { public string Name; public Transform Part; public Vector3 Axis; public float Swing; }

        /// <summary>Someone can take the wheel of this kind.</summary>
        public static bool Drivable(VehicleKind kind) => kind == VehicleKind.Boat || HandlingSpec.For(kind) != null;

        /// <summary>The body material of a family: the shared one, or its own where it has a picture.</summary>
        public static Material BodyMaterial(VehicleKind kind)
        {
            var body = Resources.Load<Material>("Materials/Vehicle");
            var picture = Resources.Load<Texture2D>("Vehicles/" + kind + "Map");
            if (picture == null) return body;
            var own = new Material(body) { name = kind + " body" };
            own.SetTexture(BaseMapId, picture);
            return own;
        }

        /// <summary>Makes a vehicle at this place, in this paint or the family's first. Returns null for a family nobody can drive.</summary>
        public static PlayerVehicle Spawn(VehicleKind kind, Vector3 at, float heading, DayCycle day, PaintJob? job = null)
        {
            var spec = HandlingSpec.For(kind);
            var art = Resources.Load<TextAsset>("Vehicles/Driven/" + kind) ?? Resources.Load<TextAsset>("Vehicles/Empty/" + kind);
            if ((spec == null && kind != VehicleKind.Boat) || art == null) return null;
            var go = new GameObject("Vehicle " + kind);
            go.layer = WorldLayers.Vehicle;
            go.transform.SetPositionAndRotation(at, Quaternion.Euler(0, heading, 0));
            go.SetActive(false);
            var v = go.AddComponent<PlayerVehicle>();
            v.Build(kind, spec, VehicleArt.Read(kind, art.bytes, 0), day, job ?? VehiclePaints.For(kind, 1));
            go.SetActive(true);
            return v;
        }

        /// <summary>Its engine note, from what kind of engine it would have.</summary>
        public VehicleSound AddSound()
        {
            var sound = gameObject.AddComponent<VehicleSound>();
            sound.Vehicle = this;
            sound.Cylinders = TwoWheels || Boat != null ? 2 : Kind == VehicleKind.Bus || Kind == VehicleKind.Coach || Kind == VehicleKind.Truck || Kind == VehicleKind.Tipper ? 6 : 4;
            return sound;
        }

        /// <summary>A new paint job, at once.</summary>
        public void Repaint(in PaintJob job)
        {
            Job = job;
            look = VehicleLook.Of(job, Height);
        }

        void Build(VehicleKind kind, HandlingSpec spec, VehicleModel model, DayCycle cycle, PaintJob job)
        {
            Kind = kind;
            day = cycle;
            Length = model.Length;
            Width = model.Width;
            Height = model.Height;
            Body = GetComponent<Rigidbody>();
            double pitch, yawInertia, roll;
            float floor;
            if (spec != null)
            {
                Dynamics = new VehicleDynamics(spec, model.Wheels, model.Length, model.Width, model.Height);
                Body.mass = (float)spec.Mass;
                centre = new Vector3(0, (float)spec.CentreHeight, (float)Dynamics.CentreZ);
                pitch = Dynamics.PitchInertia;
                yawInertia = Dynamics.YawInertia;
                roll = Dynamics.RollInertia;
                // The hull stands clear of the road: the wheels carry the car, and it touches a kerb only if the springs bottom.
                floor = Mathf.Max(0.34f, (float)model.Height * 0.16f);
            }
            else
            {
                Boat = new BoatDynamics(BoatSpec.Inflatable, model.Length, model.Width, model.Height);
                Body.mass = (float)Boat.Spec.Mass;
                centre = new Vector3(0, 0.1f, 0);
                pitch = Boat.PitchInertia;
                yawInertia = Boat.YawInertia;
                roll = Boat.RollInertia;
                floor = 0.25f; // The flat ground runs on under the water, just below the surface: the hull's box stays above it, and the floats carry the boat.
            }
            inertia = new Vector3((float)pitch, (float)yawInertia, (float)roll);
            Weigh();
            Body.linearDamping = 0;
            Body.angularDamping = 0.02f;
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            Body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            var hull = gameObject.AddComponent<BoxCollider>();
            hull.center = new Vector3(0, (floor + (float)model.Height) / 2, 0);
            // A bike is as wide as its handlebars only up high; low down, engine and pegs, it is narrow. A box the bars' width all the way down scraped the road at 44 degrees of lean and
            // carried the bike on its corner (2026-11-07), so a two-wheeler's box is 0.45 m wide, which clears the road to 56 degrees.
            float wide = spec != null && spec.TwoWheels ? Mathf.Min((float)model.Width, 0.45f) : (float)model.Width;
            hull.size = new Vector3(wide, (float)model.Height - floor, (float)model.Length);

            Chassis = new GameObject(ChassisName).transform;
            Chassis.SetParent(transform, false);
            foreach (var g in Groups) new GameObject(g).transform.SetParent(Chassis, false);

            props = new MaterialPropertyBlock();
            cabinProps = new MaterialPropertyBlock();
            glassProps = new MaterialPropertyBlock();
            Repaint(job);
            var body = BodyMaterial(kind);
            var glass = Resources.Load<Material>("Materials/VehicleGlass");
            paintRenderers.Add(Part(Chassis.Find("Exterior"), "Body", TrafficView.ToMesh(model.Near, kind + " body", keep: false), body, true));
            if (model.Glass.Tris.Count > 0) glassRenderers.Add(Part(Chassis.Find("Windows"), "Glass", TrafficView.ToMesh(model.Glass, kind + " glass", keep: false), glass, false));

            Interior = Chassis.Find("Interior");
            var doorGroup = Chassis.Find("Doors");
            foreach (var p in model.Parts ?? System.Array.Empty<KitPart>())
            {
                bool door = p.Name.StartsWith(VehicleFamilies.DoorPart);
                var r = Part(door ? doorGroup : Interior, p.Name, TrafficView.ToMesh(p.Mesh, kind + " " + p.Name, keep: false), p.Glass ? glass : body, door && !p.Glass);
                r.transform.localPosition = new Vector3((float)p.Pivot.X, (float)p.Pivot.Y, (float)p.Pivot.Z);
                var axis = new Vector3((float)p.Axis.X, (float)p.Axis.Y, (float)p.Axis.Z);
                if (p.Glass) glassRenderers.Add(r);
                else if (door) paintRenderers.Add(r);
                else cabinRenderers.Add(r);
                if (door) doors.Add(new Door { Name = p.Name, Part = r.transform, Axis = axis, Swing = (float)p.Swing });
                switch (p.Name)
                {
                    case VehicleFamilies.SteeringWheel: steeringWheel = r.transform; steeringAxis = axis; break;
                    case VehicleFamilies.SpeedNeedle: speedNeedle = r.transform; dialAxis = axis; break;
                    case VehicleFamilies.RevNeedle: revNeedle = r.transform; break;
                    case VehicleFamilies.FuelNeedle: fuelNeedle = r.transform; break;
                    case VehicleFamilies.DriverFigure: Driver = r.transform; break;
                }
            }

            HasDoor = doors.Exists(d => d.Name == VehicleFamilies.DriverDoor);
            var points = Chassis.Find("InteractionPoints");
            Eye = new GameObject("DriverEye").transform;
            Eye.SetParent(points, false);
            Eye.localPosition = new Vector3((float)model.Eye.X, (float)model.Eye.Y, (float)model.Eye.Z);
            DoorPoint = new GameObject("DriverDoor").transform;
            DoorPoint.SetParent(points, false);
            DoorPoint.localPosition = new Vector3((float)(model.Eye.X > 0 ? model.Width / 2 + 0.6 : -model.Width / 2 - 0.6), 0, (float)model.Eye.Z);

            // Wheels: each hangs from its spring, turns on its kingpin and spins on its axle, as three transforms.
            var wheels = new GameObject("Wheels").transform;
            wheels.SetParent(transform, false);
            int n = model.Wheels.Length;
            suspension = new Transform[n];
            steer = new Transform[n];
            spin = new Transform[n];
            var tyre = new Mesh[model.OwnWheels?.Length ?? 0];
            for (int i = 0; i < n; i++)
            {
                var w = model.Wheels[i];
                string side = w.X < 0 ? "Left" : w.X > 0 ? "Right" : "";
                suspension[i] = new GameObject((State.Wheels[i].Front ? "Front" : "Rear") + side).transform;
                suspension[i].SetParent(wheels, false);
                suspension[i].localPosition = new Vector3((float)w.X, (float)w.Y, (float)w.Z);
                steer[i] = new GameObject("Steer").transform;
                steer[i].SetParent(suspension[i], false);
                spin[i] = new GameObject("Spin").transform;
                spin[i].SetParent(steer[i], false);
                var r = Part(spin[i], "Wheel", tyre[w.Mesh] ??= TrafficView.ToMesh(model.OwnWheels[w.Mesh], kind + " wheel", keep: false), body, true);
                if (w.Flip) r.transform.localRotation = Quaternion.Euler(0, 180, 0);
                // What holds the brake turns with the wheel and does not spin.
                if (w.Hub >= 0) Part(steer[i], "Brake", TrafficView.ToMesh(model.Hubs[w.Hub], kind + " brake", keep: false), body, false);
            }

            var lights = Chassis.Find("Lights");
            beams = new Light[2];
            for (int i = 0; i < 2; i++)
            {
                var l = new GameObject("Headlamp").AddComponent<Light>();
                l.transform.SetParent(lights, false);
                l.transform.localPosition = new Vector3((i * 2 - 1) * (float)model.Width * 0.34f, 0.68f, (float)model.Length / 2);
                // Almost level, with a tight bright core: the road near the bumper falls outside the core, so the light goes down the road and not into a pool at the nose.
                l.transform.localRotation = Quaternion.Euler(BeamDip, 0, 0);
                l.type = LightType.Spot;
                l.range = 60;
                l.spotAngle = 46;
                l.innerSpotAngle = 14;
                l.intensity = 0;
                l.color = new Color(1f, 0.93f, 0.8f);
                beams[i] = l;
            }
        }

        Renderer Part(Transform parent, string name, Mesh mesh, Material material, bool shadows)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            return r;
        }

        Vector3 centre, inertia;

        // The vehicle is built while its object is inactive, and a body that is not yet in the physics scene reads back a centre of mass of zero: every vehicle had its centre of mass on the road, which tipped
        // the sedan 1.5 degrees nose-up at rest (2026-11-07). So the mass is set again whenever the body comes into the scene.
        void OnEnable() { if (Body != null) Weigh(); }

        void Weigh()
        {
            Body.automaticCenterOfMass = false;
            Body.automaticInertiaTensor = false;
            Body.centerOfMass = centre;
            Body.inertiaTensor = inertia;
            Body.inertiaTensorRotation = Quaternion.identity;
        }

        void FixedUpdate()
        {
            if (Parked) Input = new DriveInput { Handbrake = true };
            var t = transform;
            var pose = new ChassisPose
            {
                Position = D(Body.position), Centre = D(Body.worldCenterOfMass),
                Right = D(t.right), Up = D(t.up), Forward = D(t.forward),
                Velocity = D(Body.linearVelocity), Spin = D(Body.angularVelocity),
            };
            using (DadMarkers.VehicleStep.Auto())
            {
                if (Boat != null) Boat.Step(Time.fixedDeltaTime, pose, Input, Sea.Here, this);
                else Dynamics.Step(Time.fixedDeltaTime, pose, Input, this, this);
            }
        }

        void LateUpdate()
        {
            var s = State;
            for (int i = 0; i < suspension.Length; i++)
            {
                var w = s.Wheels[i];
                var at = suspension[i].localPosition;
                at.y = (float)w.HubY;
                suspension[i].localPosition = at;
                steer[i].localRotation = Quaternion.Euler(0, (float)(w.Steer * Mathf.Rad2Deg), 0);
                spin[i].localRotation = Quaternion.Euler((float)(w.Angle * Mathf.Rad2Deg), 0, 0);
            }
            if (steeringWheel != null && Dynamics != null) steeringWheel.localRotation = Quaternion.AngleAxis(-(float)(s.SteerAngle / Dynamics.Spec.MaxSteer) * 200f, steeringAxis);
            if (speedNeedle != null) speedNeedle.localRotation = Needle(System.Math.Abs(s.Speed) * 3.6 / 200);
            if (revNeedle != null) revNeedle.localRotation = Needle(s.Rpm / 8000);
            if (fuelNeedle != null) fuelNeedle.localRotation = Needle(s.Fuel);

            bool dark = day != null && TrafficView.HeadlampsOn(day.Hours) > 0.5f;
            bool head = s.HeadLamps || (dark && s.EngineOn);
            bool flash = Time.time * 1.5f % 1f < 0.5f;
            float left = s.Hazards || s.Indicator < 0 ? (flash ? 1 : 0) : 0, right = s.Hazards || s.Indicator > 0 ? (flash ? 1 : 0) : 0;
            props.SetVector(TintId, look.Tint);
            props.SetVector(FinishId, look.Finish);
            props.SetVector(SecondId, look.Second);
            props.SetVector(LampsId, new Vector4(head ? 1 : 0, s.BrakeLamps ? 1 : 0, left, right));
            foreach (var r in paintRenderers) r.SetPropertyBlock(props);
            // The cabin's parts are drawn apart from the body and need telling too: the dials light with the lamps.
            cabinProps.SetVector(LampsId, new Vector4(head ? 1 : 0, 0, 0, 0));
            foreach (var r in cabinRenderers) r.SetPropertyBlock(cabinProps);
            foreach (var b in beams) b.intensity = head ? BeamIntensity : 0;
        }

        /// <summary>How far the camera is inside the car, 0 to 1. The glass that hides a dark cabin from the street must not stand between the driver and the road.</summary>
        public void SeenFromInside(float share)
        {
            glassProps.SetFloat(ClearId, share);
            foreach (var r in glassRenderers) r.SetPropertyBlock(glassProps);
        }

        /// <summary>Opens a door, 0 shut to 1 as far as its hinge goes, by name (VehicleFamilies.DriverDoor). Its window goes with it.</summary>
        public void SwingDoor(string name, float open)
        {
            foreach (var d in doors)
                if (d.Name == name || d.Name == name + VehicleFamilies.DoorGlass) d.Part.localRotation = Quaternion.AngleAxis(d.Swing * Mathf.Clamp01(open), d.Axis);
        }

        Quaternion Needle(double share) => Quaternion.AngleAxis(130 - 260 * (float)System.Math.Clamp(share, 0, 1), dialAxis);

        public bool Cast(D3 from, D3 direction, double max, out ContactHit hit)
        {
            if (Physics.Raycast(V(from), V(direction), out var h, (float)max, WorldLayers.Ground, QueryTriggerInteraction.Ignore))
            {
                hit = new ContactHit { Distance = h.distance, Normal = D(h.normal), Grip = 1 };
                return true;
            }
            hit = default;
            return false;
        }

        public void AddForceAt(D3 force, D3 point) => Body.AddForceAtPosition(V(force), V(point), ForceMode.Force);

        // The water a boat is on, in the scene's frame: WaterView knows it in world metres.
        sealed class Sea : IWater
        {
            public static readonly Sea Here = new Sea();
            public double Surface(double x, double z) => WaterView.Height;
            public bool Shore(double x, double z, out double distance, out double outX, out double outZ)
            {
                distance = outX = outZ = 0;
                var view = WaterView.Active;
                var origin = FloatingOrigin.Active;
                if (view == null || origin == null) return false;
                origin.Offset.ToWorld((float)x, (float)z, out double east, out double north);
                return view.Shore(east, north, out distance, out outX, out outZ);
            }
        }

        static D3 D(Vector3 v) => new D3(v.x, v.y, v.z);
        static Vector3 V(D3 v) => new Vector3((float)v.X, (float)v.Y, (float)v.Z);
    }
}
