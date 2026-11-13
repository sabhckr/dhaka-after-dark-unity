// You, in the street (E-01, E-02, E-03, E-06; 2026-11-06). Plain play starts here. The world comes in, the owner's Jetta stands on the nearest lane and you stand by its driver's door.
// You walk, Interact at the door plays the real clip into the seat, and the exit key plays the real clip back out. The door is the model's own and the seat is the one the baked
// driver sits in: where the clip's hips end is read from the clip, so the body lands there and not at a number that fits one person. F1 is still the free camera.
// On foot you carry guns (Arsenal) and can be hurt (Vitals); at no health you go down, and a few seconds later stand again where the game began, healed and no longer
// wanted (2026-11-10). Prompt says what the use button does where you are, for the HUD.
using System;
using Dad.Traffic;
using UnityEngine;

namespace Dad.Core
{
    public sealed class Player : MonoBehaviour
    {
        public enum Phase { Waiting, Walking, GettingIn, Driving, GettingOut, Riding, Down }

        /// <summary>How near the door you must be to get in, and how slow the car must be to get out.</summary>
        public const float DoorReach = 2.2f, StopSpeed = 1.5f;
        public const string EnterClip = "enter-car", ExitClip = "exit-car", RideClip = "drive";
        /// <summary>A vehicle left this far behind goes back to being a parked one; how far a boat's crew can step ashore.</summary>
        public const float LeftBehind = 60f, StepAshore = 14f;
        /// <summary>Where the driver's hips are in the car, and in which share of a clip the door moves. From the Jetta's driver in Tools/models/bake.py and the clips as played.</summary>
        public const float SeatHeight = 0.56f, SeatBack = -0.11f, DoorOpens = 0.05f, DoorOpen = 0.22f, DoorShuts = 0.72f, DoorShut = 0.92f;

        public GameInput Input;
        public TrafficView Traffic;
        public DayCycle Day;
        public TestDrive Drive;
        /// <summary>Every vehicle you could take, and the train you could board.</summary>
        public ParkedView Yard;
        public TrainView Train;
        public double StartEast = FreeCamera.StartEast, StartNorth = FreeCamera.StartNorth;
        /// <summary>Who you are on foot (OnFoot.Spawn): the one of the twenty you chose (Cast).</summary>
        public string Who => Cast.You;
        /// <summary>Seconds down before you stand again.</summary>
        public const float DownFor = 6;

        /// <summary>Plain play stands you in the street by itself. Tests turn this off and stand someone where they want.</summary>
        public static bool Automatic = true;

        public Phase Now { get; private set; }
        public OnFoot Body { get; private set; }
        public PlayerVehicle Car { get; private set; }
        public Vitals Vitals { get; private set; }
        public Arsenal Arsenal { get; private set; }
        /// <summary>What the use button does here ("Get in", "Board", "Get out", "Get off"), or null for nothing.</summary>
        public string Prompt { get; private set; }
        /// <summary>Seconds since you went down; zero when you are not.</summary>
        public float DownTime => Now == Phase.Down ? Time.time - downAt : 0;
        /// <summary>Where the hips are in the car, for tests.</summary>
        public Vector3 Seat => new Vector3(Car.Eye.localPosition.x, SeatHeight, SeatBack);

        FootCamera view;
        float clock, length, fromY, toY, downAt, nextPrompt;
        Vector3 home;
        float homeHeading;

        void OnEnable()
        {
            if (Input != null) Input.ContextChanged += Changed;
        }

        void OnDisable()
        {
            if (Input != null) Input.ContextChanged -= Changed;
        }

        // F1 flies away from the body: it stands still with the world gone from under it, and the origin follows the camera until F1 comes back.
        void Changed(InputContext from, InputContext to)
        {
            var origin = FloatingOrigin.Active;
            if (Now != Phase.Walking || origin == null) return;
            bool free = to == InputContext.FreeCamera;
            if (free == (from == InputContext.FreeCamera)) return;
            origin.Target = free ? Camera.main.transform : Body.transform;
            Body.Held = free;
            view.enabled = !free;
            var fly = Camera.main.GetComponent<FreeCamera>();
            if (fly != null) fly.enabled = free;
        }

        void Update()
        {
            switch (Now)
            {
                case Phase.Waiting: if (Automatic) TryStart(); break;
                case Phase.Walking:
                    if (Input != null && Input.Context == InputContext.OnFoot && Input.Interact.WasPressedThisFrame())
                    {
                        if (!TryGetIn()) TryBoard();
                    }
                    Leaving();
                    if (Time.time >= nextPrompt) { nextPrompt = Time.time + 0.1f; Prompt = CanGetIn() ? "Get in" : Train != null && Train.Board(Body.transform.position) != null ? "Board" : null; }
                    break;
                case Phase.Driving:
                    Prompt = Vector3.ProjectOnPlane(Car.Body.linearVelocity, Car.transform.up).magnitude <= StopSpeed ? "Get out" : null;
                    break;
                case Phase.Riding:
                    Prompt = Train.Run.DoorsOpen ? "Get off" : null;
                    if (Input != null && Input.Interact.WasPressedThisFrame()) TryAlight();
                    break;
                case Phase.Down:
                    Prompt = null;
                    if (DownTime >= DownFor) StandAgain();
                    break;
                case Phase.GettingIn: Going(true); break;
                case Phase.GettingOut: Going(false); break;
            }
        }

        /// <summary>Stands you by the car once there is a lane and something to stand on. Until then the free camera shows the world coming in.</summary>
        public bool TryStart()
        {
            var cam = Camera.main;
            var origin = FloatingOrigin.Active;
            if (cam == null || origin == null || Traffic == null || Traffic.Net == null) return false;
            // Your car stands in a space, off the road: on a lane, the traffic would run into it.
            var slot = Yard != null ? Yard.Nearest(StartEast, StartNorth, VehicleKind.Sedan, 700) : null;
            float heading;
            if (slot != null)
            {
                origin.Offset.ToScene(slot.East, slot.North, out float px, out float pz);
                if (!Physics.Raycast(new Vector3(px, slot.Y + 30, pz), Vector3.down, 60, WorldLayers.Ground)) return false;
                Car = Yard.Take(slot);
                heading = slot.Turn;
            }
            else
            {
                if (!Traffic.NearestLane(StartEast, StartNorth, out double x, out double z, out double hx, out double hz, out double y)) return false;
                origin.Offset.ToScene(x, z, out float sx, out float sz);
                if (!Physics.Raycast(new Vector3(sx, (float)y + 30, sz), Vector3.down, out var road, 60, WorldLayers.Ground)) return false;
                heading = Mathf.Atan2((float)hx, (float)hz) * Mathf.Rad2Deg;
                Car = PlayerVehicle.Spawn(VehicleKind.Sedan, road.point + Vector3.up * 0.2f, heading, Day);
            }
            if (Car == null) return false;
            Car.AddSound();
            var stand = Car.DoorPoint.position + Car.transform.right * 1.2f + Car.transform.forward * 0.3f;
            if (Physics.Raycast(stand + Vector3.up * 30, Vector3.down, out var ground, 60, WorldLayers.Ground)) stand = ground.point;
            home = stand;
            homeHeading = heading - 90;
            Body = OnFoot.Spawn(stand + Vector3.up * 0.05f, heading - 90, Input, cam.transform, Who);
            if (Body == null) { Destroy(Car.gameObject); Car = null; return false; }
            var fly = cam.GetComponent<FreeCamera>();
            if (fly != null) fly.enabled = false;
            origin.Target = Body.transform;
            Traffic.Player = Car;
            if (Traffic.GetComponent<TrafficColliders>() == null) Traffic.gameObject.AddComponent<TrafficColliders>().View = Traffic;
            Stand(Car, Body, cam.GetComponent<FootCamera>() ?? cam.gameObject.AddComponent<FootCamera>());
            Input?.Switch(InputContext.OnFoot);
            return true;
        }

        /// <summary>You, on foot, with this car parked and this camera behind you. Tests start here, on any ground.</summary>
        public void Stand(PlayerVehicle car, OnFoot body, FootCamera camera)
        {
            Car = car;
            Body = body;
            view = camera;
            view.Input = Input;
            view.Attach(body.transform, body.transform.eulerAngles.y);
            if (Drive != null) Drive.Leave = () => TryGetOut();
            if (Car != null)
            {
                if (Car.Driver != null) Car.Driver.gameObject.SetActive(false);
                Car.State.EngineOn = false;
                Car.Parked = true;
            }
            if (home == Vector3.zero) { home = body.transform.position; homeHeading = body.transform.eulerAngles.y; }
            Equip(null);
            Now = Phase.Walking;
        }

        // Health and guns on the body you are; those you carried come with you to a new body.
        void Equip(Arsenal carried)
        {
            Vitals = Body.GetComponent<Vitals>();
            if (Vitals == null) Vitals = Body.gameObject.AddComponent<Vitals>();
            Vitals.Downed -= Downed;
            Vitals.Downed += Downed;
            Arsenal = Body.GetComponent<Arsenal>();
            if (Arsenal == null) Arsenal = Core.Arsenal.Give(Body, Input, view);
            if (carried != null) { Arsenal.Slots.Clear(); Arsenal.Slots.AddRange(carried.Slots); }
            else if (Arsenal.Slots.Count == 1) foreach (var k in new[] { Armed.Glock, Armed.Ump, Armed.Scar }) Arsenal.Add(k); // no shops yet: you start with the game's three guns
        }

        // At no health: limp, falling with the shot (or, on a body without a ragdoll, by a death clip away from it). The gun is put away first, so nothing poses the arms.
        void Downed()
        {
            if (Now != Phase.Walking) return;
            Now = Phase.Down;
            downAt = Time.time;
            Body.Held = true;
            if (Arsenal != null) Arsenal.Select(0);
            if (!Hitbox.Die(Body.Person, Vitals.HurtFrom, Vitals.LastWound))
            {
                bool fromFront = Vector3.Dot(Body.transform.forward, Vitals.HurtFrom - Body.transform.position) > 0;
                Body.Person.Play(fromFront ? "death-back" : "death-forward");
            }
            Notice.Post("You are down");
        }

        /// <summary>Someone else now (Cast.Use, from the start screen or the pause menu): on foot, the new body stands where the old one stood, with your health and
        /// what you carried; before you stand, you start as them. In a car or down, false: nothing changes.</summary>
        public bool Become(string model)
        {
            if (!Cast.Has(model)) return false;
            if (Now == Phase.Waiting) { Cast.Use(model); return true; }
            if (Now != Phase.Walking || Body == null) return false;
            string was = Cast.You;
            Cast.Use(model);
            var old = Body;
            var carried = Arsenal;
            float health = Vitals != null ? Vitals.Health : Core.Vitals.Most, armour = Vitals != null ? Vitals.Armour : 0;
            int holding = carried != null ? carried.Current : 0;
            if (carried != null) { carried.Select(0); carried.enabled = false; }
            var at = old.transform.position;
            float heading = old.transform.eulerAngles.y;
            old.gameObject.SetActive(false); // out of the way of the new capsule
            Body = OnFoot.Spawn(at, heading, Input, view.transform, model);
            if (Body == null)
            {
                old.gameObject.SetActive(true);
                Body = old;
                if (carried != null) { carried.enabled = true; carried.Select(holding); }
                Cast.Use(was);
                return false;
            }
            Destroy(old.gameObject);
            Arsenal = null;
            if (FloatingOrigin.Active != null) FloatingOrigin.Active.Target = Body.transform;
            view.Attach(Body.transform, view.Yaw);
            Equip(carried);
            Vitals.Health = health;
            Vitals.Armour = armour;
            Arsenal.Select(holding);
            return true;
        }

        /// <summary>Back on your feet where the game began, healed, no longer wanted, with what you carried.</summary>
        public void StandAgain()
        {
            var carried = Arsenal;
            var old = Body;
            if (carried != null) { carried.Select(0); carried.enabled = false; }
            var at = home;
            if (Physics.Raycast(at + Vector3.up * 30, Vector3.down, out var ground, 60, WorldLayers.Ground)) at = ground.point;
            Body = OnFoot.Spawn(at + Vector3.up * 0.05f, homeHeading, Input, view.transform, Who);
            if (Body == null) { Body = old; return; }
            Destroy(old.gameObject);
            Arsenal = null;
            if (FloatingOrigin.Active != null) FloatingOrigin.Active.Target = Body.transform;
            view.Attach(Body.transform, homeHeading);
            Equip(carried);
            Heat.Clear();
            Now = Phase.Walking;
        }

        bool CanGetIn()
        {
            if (Car != null)
            {
                var gap = Body.transform.position - Car.DoorPoint.position;
                gap.y = 0;
                if (gap.magnitude <= DoorReach) return true;
            }
            return Yard != null && Yard.Within(Body.transform.position, DoorReach) != null;
        }

        /// <summary>Into the car at whose door you stand, or whatever parked vehicle is in reach. Where the model has a door the clip plays into the seat; where it has none, you are simply aboard.</summary>
        public bool TryGetIn()
        {
            if (Now != Phase.Walking) return false;
            bool atCar = false;
            if (Car != null)
            {
                var gap = Body.transform.position - Car.DoorPoint.position;
                gap.y = 0;
                atCar = gap.magnitude <= DoorReach;
            }
            if (!atCar)
            {
                var slot = Yard != null ? Yard.Within(Body.transform.position, DoorReach) : null;
                if (slot == null) return false;
                var next = Yard.Take(slot);
                if (next == null) return false;
                if (Car != null) Yard.Give(Car);
                Car = next;
                Car.AddSound();
                Car.State.EngineOn = false;
                Car.Parked = true;
                if (Traffic != null) Traffic.Player = Car;
            }
            Body.Held = true;
            if (!Car.HasDoor)
            {
                Seated();
                return true;
            }
            var root = Body.transform;
            root.SetParent(Car.Chassis, false);
            root.localRotation = Quaternion.Euler(0, -90, 0);
            length = Body.Person.Play(EnterClip, 0); // at once: where it starts and ends is measured on the clip
            // The clip walks the hips to a seat of its own, in the root's frame. The root goes where that seat is this car's seat.
            var end = root.localRotation * Body.Person.HipsAt(length);
            root.localPosition = new Vector3(Seat.x - end.x, 0, Seat.z - end.z);
            fromY = 0;
            toY = Seat.y - end.y;
            clock = 0;
            Now = Phase.GettingIn;
            view.Attach(root, Car.transform.eulerAngles.y + 90);
            return true;
        }

        /// <summary>Starts the clip out of the seat, if the car has stopped and there is room to stand at the door.</summary>
        public bool TryGetOut()
        {
            // Stopped means not travelling: the bob of the springs as a rider settles is not travel.
            if (Now != Phase.Driving || Vector3.ProjectOnPlane(Car.Body.linearVelocity, Car.transform.up).magnitude > StopSpeed) return false;
            if (!Car.HasDoor) return StepOff();
            var at = Car.DoorPoint.position;
            if (Physics.CheckCapsule(at + Vector3.up * 0.6f, at + Vector3.up * 1.5f, OnFoot.Radius, WorldLayers.Solid, QueryTriggerInteraction.Ignore)) return false;
            Drive.Release();
            Car.State.EngineOn = false;
            if (Car.Driver != null) Car.Driver.gameObject.SetActive(false);
            var root = Body.transform;
            root.gameObject.SetActive(true);
            root.SetParent(Car.Chassis, false);
            root.localRotation = Quaternion.identity;
            length = Body.Person.Play(ExitClip, 0);
            var start = Body.Person.HipsAt(0);
            root.localPosition = new Vector3(Seat.x - start.x, Seat.y - start.y, Seat.z - start.z);
            fromY = root.localPosition.y;
            toY = 0;
            clock = 0;
            Now = Phase.GettingOut;
            view.enabled = true;
            view.Attach(root, Car.transform.eulerAngles.y);
            return true;
        }

        // Off a vehicle with no door to play against: beside it on land, or to the nearest dry ground from a boat.
        bool StepOff()
        {
            var at = Car.DoorPoint.position;
            if (Car.Boat != null)
            {
                var origin = FloatingOrigin.Active;
                var water = WaterView.Active;
                if (origin == null || water == null) return false;
                var p = Car.transform.position;
                origin.Offset.ToWorld(p.x, p.z, out double east, out double north);
                if (!water.Landing(east, north, StepAshore, out double x, out double z)) return false;
                origin.Offset.ToScene(x, z, out float sx, out float sz);
                at = new Vector3(sx, 2, sz);
            }
            else if (Physics.CheckCapsule(at + Vector3.up * 0.6f, at + Vector3.up * 1.5f, OnFoot.Radius, WorldLayers.Solid, QueryTriggerInteraction.Ignore)) return false;
            if (Physics.Raycast(at + Vector3.up * 4, Vector3.down, out var ground, 12, WorldLayers.Ground)) at = ground.point;
            Drive.Release();
            Car.State.EngineOn = false;
            Car.Parked = true;
            var root = Body.transform;
            Body.Person.Stop();
            root.SetParent(null, true);
            root.gameObject.SetActive(true);
            float heading = Car.transform.eulerAngles.y;
            root.SetPositionAndRotation(at + Vector3.up * 0.05f, Quaternion.Euler(0, heading, 0));
            Body.Held = false;
            var follow = FloatingOrigin.Active;
            if (follow != null) follow.Target = root;
            view.enabled = true;
            view.Attach(root, heading);
            Input?.Switch(InputContext.OnFoot);
            Now = Phase.Walking;
            return true;
        }

        // A vehicle you have walked away from stops being one: it goes back among the parked, where you left it.
        void Leaving()
        {
            if (Car == null || Yard == null) return;
            if ((Car.transform.position - Body.transform.position).sqrMagnitude < LeftBehind * LeftBehind) return;
            if (Traffic != null) Traffic.Player = null;
            Yard.Give(Car);
            Car = null;
        }

        /// <summary>Aboard the train, if it stands at a stop with its doors open and you are beside it.</summary>
        public bool TryBoard()
        {
            if (Now != Phase.Walking || Train == null) return false;
            var seat = Train.Board(Body.transform.position);
            if (seat == null) return false;
            Body.Held = true;
            Body.gameObject.SetActive(false);
            view.Attach(seat, seat.eulerAngles.y);
            if (FloatingOrigin.Active != null) FloatingOrigin.Active.Target = seat;
            Now = Phase.Riding;
            return true;
        }

        /// <summary>Off the train, on the side you are facing, once it has stopped and opened up.</summary>
        public bool TryAlight()
        {
            if (Now != Phase.Riding || !Train.Run.DoorsOpen) return false;
            var seat = view.Target;
            var at = seat.position + seat.right * 3.2f;
            if (Physics.Raycast(at + Vector3.up * 10, Vector3.down, out var ground, 30, WorldLayers.Ground)) at = ground.point;
            var root = Body.transform;
            root.gameObject.SetActive(true);
            root.SetPositionAndRotation(at + Vector3.up * 0.05f, Quaternion.Euler(0, seat.eulerAngles.y, 0));
            Body.Held = false;
            if (FloatingOrigin.Active != null) FloatingOrigin.Active.Target = root;
            view.Attach(root, seat.eulerAngles.y);
            Now = Phase.Walking;
            return true;
        }

        void Going(bool into)
        {
            clock += Time.deltaTime;
            float t = Mathf.Clamp01(clock / length);
            float open = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(DoorOpens, DoorOpen, t)) * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(DoorShuts, DoorShut, t)));
            Car.SwingDoor(VehicleFamilies.DriverDoor, open);
            var root = Body.transform;
            var p = root.localPosition;
            // Into the seat the body drops late; out of it, the body rises early.
            p.y = into ? Mathf.Lerp(fromY, toY, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.45f, 0.85f, t))) : Mathf.Lerp(fromY, toY, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.15f, 0.6f, t)));
            root.localPosition = p;
            if (t < 1) return;
            Car.SwingDoor(VehicleFamilies.DriverDoor, 0);
            if (into) Seated();
            else Stood();
        }

        void Seated()
        {
            var root = Body.transform;
            Body.Person.Stop();
            if (Car.TwoWheels)
            {
                // Astride, in view, going with the bike. There is no riding clip: this is the driver's, sat on the saddle.
                root.SetParent(Car.Chassis, false);
                root.localRotation = Quaternion.identity;
                Body.Person.Play(RideClip, 0);
                var hips = Body.Person.HipsAt(0);
                var saddle = Car.Eye.localPosition + Vector3.down * 0.62f;
                root.localPosition = saddle - hips;
            }
            else
            {
                root.SetParent(null, true);
                root.gameObject.SetActive(false);
            }
            view.enabled = false;
            Car.Parked = false;
            Car.State.EngineOn = true;
            Drive.Take(Car);
            Now = Phase.Driving;
        }

        void Stood()
        {
            var root = Body.transform;
            var hips = Body.Person.Hips.position;
            var facing = Body.Person.Body.bodyRotation * Vector3.forward;
            root.SetParent(null, true);
            float heading = Mathf.Atan2(facing.x, facing.z) * Mathf.Rad2Deg;
            float y = root.position.y;
            if (Physics.Raycast(hips + Vector3.up, Vector3.down, out var ground, 4, WorldLayers.Ground)) y = ground.point.y;
            root.SetPositionAndRotation(new Vector3(hips.x, y + 0.03f, hips.z), Quaternion.Euler(0, heading, 0));
            Body.Person.Stop();
            Body.Held = false;
            Car.Parked = true;
            var origin = FloatingOrigin.Active;
            if (origin != null) origin.Target = root;
            view.Attach(root, heading);
            Input?.Switch(InputContext.OnFoot);
            Now = Phase.Walking;
        }
    }
}
