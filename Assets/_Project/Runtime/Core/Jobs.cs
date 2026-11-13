// Deliveries, the first job (first playable, 2026-11-13; the design's "Job" loop, H-03 side jobs, Docs/PRODUCTION/FIRST_PLAYABLE.md). A parcel waits on a street near you,
// marked by a column of light in the street, on the radar and on the map, with the route to it. Stop in the light to take it, then take it to another street across town
// before the clock runs out; stop in that light to deliver it and be paid, more the sooner you are. You cannot hand it over while the police are after you. Running out of
// time or going down loses it. Then the next parcel is offered. Pay goes into Progress, which is kept between runs.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Dad.Core
{
    public sealed class Jobs : MonoBehaviour
    {
        public enum Stage { Waiting, Offered, Carrying }

        /// <summary>How near (metres, from the middle of the light) and how slow (metres a second) counts as stopped in a marker.</summary>
        public const float Reach = 7f, Still = 4f;
        /// <summary>Where a pickup is offered (metres from you) and how far a parcel goes (metres from the pickup, in a straight line).</summary>
        public const double OfferNear = 150, OfferFar = 450, TripNear = 600, TripFar = 1400;
        /// <summary>Seconds between one job and the next offer.</summary>
        public const float Rest = 6f;
        /// <summary>Pay in taka: a fixed part, a part a metre of road, and up to half again for time to spare. Time allowed: the road at this pace, plus a margin.</summary>
        public const int Fixed = 150;
        public const float PerMetre = 0.4f, Pace = 7f, Margin = 25f, Least = 60f;

        /// <summary>Plain play offers jobs by itself. Tests turn this off and Offer what they want.</summary>
        public static bool Automatic = true;
        public static Jobs Active { get; private set; }

        public Player Player;
        public Stage Now { get; private set; }
        /// <summary>The pickup and the drop, as world points (east, north), and the streets they are on.</summary>
        public double PickEast { get; private set; }
        public double PickNorth { get; private set; }
        public double DropEast { get; private set; }
        public double DropNorth { get; private set; }
        public string PickStreet { get; private set; } = "";
        public string DropStreet { get; private set; } = "";
        /// <summary>The road from the pickup to the drop, metres; the time allowed for it, seconds; what is left of it.</summary>
        public float Trip { get; private set; }
        public float Allowed { get; private set; }
        public float Left { get; private set; }
        /// <summary>What the job pays if delivered now.</summary>
        public int PayNow => Pay(Trip, Left, Allowed);
        /// <summary>Where the marker is shown now: the pickup while offered, the drop while carrying.</summary>
        public bool Marked => Now != Stage.Waiting;
        public double MarkEast => Now == Stage.Carrying ? DropEast : PickEast;
        public double MarkNorth => Now == Stage.Carrying ? DropNorth : PickNorth;
        /// <summary>What the HUD says the job is now, or empty.</summary>
        public string Objective => Now switch
        {
            Stage.Offered => "Pick up the parcel" + (PickStreet.Length > 0 ? " on " + PickStreet : ""),
            Stage.Carrying => "Deliver the parcel" + (DropStreet.Length > 0 ? " to " + DropStreet : ""),
            _ => "",
        };

        Task<Plan> finding;
        float nextOffer, nextWarn;
        Player.Phase was;
        GameObject beacon;
        readonly System.Random dice = new System.Random();

        sealed class Plan { public double Pe, Pn, De, Dn; public float Trip; public string PickStreet, DropStreet; }

        public static Jobs Attach(GameObject host, Player player)
        {
            var j = host.AddComponent<Jobs>();
            j.Player = player;
            return j;
        }

        void OnEnable() => Active = this;
        void OnDisable() { if (Active == this) Active = null; }
        void OnDestroy() { if (beacon != null) Destroy(beacon); }

        /// <summary>Pay for a trip of so many metres, delivered with so many of the allowed seconds left; rounded to ten taka.</summary>
        public static int Pay(float trip, float left, float allowed)
        {
            float spare = allowed > 0 ? Mathf.Clamp01(left / allowed) : 0;
            float pay = (Fixed + PerMetre * trip) * (1 + 0.5f * spare);
            return Mathf.FloorToInt(pay / 10 + 0.5f) * 10; // half up, not to even
        }

        /// <summary>For tests: runs the job's clock on by so many seconds, as if they had passed.</summary>
        public void Pass(float seconds) => Left -= seconds;

        /// <summary>Seconds allowed for a trip of so many metres of road.</summary>
        public static float TimeFor(float trip) => Mathf.Max(Least, trip / Pace + Margin);

        /// <summary>Where you are (east, north) and how fast you go, metres a second; false before you stand in the street.</summary>
        public bool Where(out double east, out double north, out float speed)
        {
            east = north = 0; speed = 0;
            var origin = FloatingOrigin.Active;
            if (Player == null || origin == null) return false;
            Transform at;
            if (Player.Now == Player.Phase.Driving && Player.Car != null) { at = Player.Car.transform; speed = Player.Car.Body.linearVelocity.magnitude; }
            else if (Player.Body != null && (Player.Now == Player.Phase.Walking || Player.Now == Player.Phase.Riding)) { at = Player.Body.transform; speed = Player.Body.Person.Speed; }
            else return false;
            origin.Offset.ToWorld(at.position.x, at.position.z, out east, out north);
            return true;
        }

        /// <summary>Offers a job now: the pickup and drop given (tests), or found near you on the roads. True if one is offered.</summary>
        public bool Offer(double pickEast, double pickNorth, double dropEast, double dropNorth)
        {
            var map = CityMap.Active;
            if (map == null) return false;
            var plan = new Plan { Pe = pickEast, Pn = pickNorth, De = dropEast, Dn = dropNorth };
            plan.Trip = Length(map.Route(pickEast, pickNorth, dropEast, dropNorth));
            if (plan.Trip <= 0) return false;
            plan.PickStreet = map.Street(pickEast, pickNorth) ?? "";
            plan.DropStreet = map.Street(dropEast, dropNorth) ?? "";
            Take(plan);
            return true;
        }

        void Take(Plan p)
        {
            PickEast = p.Pe; PickNorth = p.Pn; DropEast = p.De; DropNorth = p.Dn;
            PickStreet = p.PickStreet; DropStreet = p.DropStreet;
            Trip = p.Trip;
            Allowed = TimeFor(Trip);
            Left = Allowed;
            Now = Stage.Offered;
            Mark();
            Notice.Post("A parcel is waiting" + (PickStreet.Length > 0 ? " on " + PickStreet : ""));
        }

        static float Length(List<Vector2> way)
        {
            if (way == null || way.Count < 2) return 0;
            float sum = 0;
            for (int i = 1; i < way.Count; i++) sum += Vector2.Distance(way[i - 1], way[i]);
            return sum;
        }

        void Update()
        {
            if (Player == null) return;
            bool down = Player.Now == Player.Phase.Down;
            if (down && was != Player.Phase.Down) WentDown();
            was = Player.Now;
            if (!Where(out double east, out double north, out float speed)) { Show(); return; }
            switch (Now)
            {
                case Stage.Waiting:
                    if (Automatic) Look(east, north);
                    break;
                case Stage.Offered:
                    if (Near(east, north, PickEast, PickNorth) && speed < Still) PickUp();
                    break;
                case Stage.Carrying:
                    Left -= Time.deltaTime;
                    if (Left <= 0) { Fail("Too late: the parcel was not delivered"); break; }
                    if (Near(east, north, DropEast, DropNorth) && speed < Still)
                    {
                        if (Heat.Level > 0) { if (Time.time >= nextWarn) { nextWarn = Time.time + 5; Notice.Post("Lose the police before you deliver"); } }
                        else Deliver();
                    }
                    break;
            }
            Route();
            Show();
        }

        static bool Near(double e, double n, double te, double tn) => (e - te) * (e - te) + (n - tn) * (n - tn) < Reach * Reach;

        // A new offer is looked for on a worker once the rest is over; a failed look tries again a second later.
        void Look(double east, double north)
        {
            if (finding != null)
            {
                if (!finding.IsCompleted) return;
                var plan = finding.Status == TaskStatus.RanToCompletion ? finding.Result : null;
                finding = null;
                if (plan != null) Take(plan);
                else nextOffer = Time.time + 1;
                return;
            }
            var map = CityMap.Active;
            if (map == null || Time.time < nextOffer) return;
            int seed = dice.Next();
            finding = Task.Run(() => Find(map, east, north, seed));
        }

        static Plan Find(CityMap map, double east, double north, int seed)
        {
            var rnd = new System.Random(seed);
            for (int tries = 0; tries < 6; tries++)
            {
                if (!map.RoadPoint(rnd, east, north, OfferNear, OfferFar, out double pe, out double pn)) return null;
                if (!map.RoadPoint(rnd, pe, pn, TripNear, TripFar, out double de, out double dn)) continue;
                float trip = Length(map.Route(pe, pn, de, dn));
                if (trip < TripNear) continue; // a drop that is far in a line but near by road is no trip
                return new Plan { Pe = pe, Pn = pn, De = de, Dn = dn, Trip = trip, PickStreet = map.Street(pe, pn) ?? "", DropStreet = map.Street(de, dn) ?? "" };
            }
            return null;
        }

        void PickUp()
        {
            Now = Stage.Carrying;
            Left = Allowed;
            Mark();
            Notice.Post($"Parcel taken: {Clock(Allowed)} to deliver it");
            Rumble.Pulse(0.2f, 0.3f, 0.15f);
        }

        void Deliver()
        {
            int pay = PayNow;
            Progress.Earn(pay);
            Progress.Current.Deliveries++;
            Progress.Save();
            Notice.Post($"Delivered: {Money(pay)}");
            Rumble.Pulse(0.3f, 0.5f, 0.25f);
            End();
        }

        void Fail(string why)
        {
            Notice.Post(why);
            Progress.Current.Failed++;
            Progress.Save();
            End();
        }

        // Going down loses the parcel, and the treatment costs a tenth of your cash.
        void WentDown()
        {
            if (Now == Stage.Carrying) Fail("The parcel was lost when you went down");
            int bill = Mathf.FloorToInt(Progress.Current.Cash * 0.01f + 0.5f) * 10;
            if (bill > 0)
            {
                Progress.Spend(bill);
                Progress.Save();
                Notice.Post($"Treatment: {Money(bill)}");
            }
        }

        void End()
        {
            Now = Stage.Waiting;
            nextOffer = Time.time + Rest;
            if (Waypoint.ForJob) Waypoint.Clear();
            if (beacon != null) beacon.SetActive(false);
        }

        // The route goes to the marker unless you have marked a place of your own on the map.
        void Mark()
        {
            if (!Waypoint.Active || Waypoint.ForJob) Waypoint.Place(MarkEast, MarkNorth, job: true);
        }

        void Route()
        {
            if (Marked && !Waypoint.Active) Mark(); // your own waypoint reached or cleared: back to the job's
        }

        // The column of light where the marker is, on the ground under it once the ground is there.
        void Show()
        {
            var origin = FloatingOrigin.Active;
            if (!Marked || origin == null) { if (beacon != null) beacon.SetActive(false); return; }
            if (beacon == null) beacon = Beacon.Make();
            beacon.SetActive(true);
            origin.Offset.ToScene(MarkEast, MarkNorth, out float x, out float z);
            float y = beacon.transform.position.y;
            if (Physics.Raycast(new Vector3(x, 400, z), Vector3.down, out var hit, 800, WorldLayers.Ground)) y = hit.point.y;
            beacon.transform.position = new Vector3(x, y, z);
        }

        public static string Clock(float seconds)
        {
            int s = Mathf.Max(0, Mathf.CeilToInt(seconds));
            return $"{s / 60}:{s % 60:00}";
        }

        public static string Money(int taka) => "Tk " + taka.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>The job marker: a ring on the street and a column of light over it, drawn by Dad/Beacon (glowing at its edges, fading upwards).</summary>
    public static class Beacon
    {
        public const float Radius = 3.5f, Height = 70f;

        public static GameObject Make()
        {
            var go = new GameObject("Job marker");
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = Mesh();
            var mr = go.AddComponent<MeshRenderer>();
            var mat = Resources.Load<Material>("Materials/Beacon");
            if (mat == null) Debug.LogError("[Dad] no Materials/Beacon");
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return go;
        }

        static Mesh made;

        // A wall round the marker whose light fades going up (vertex alpha) and glows at its edges (vertex red 1: the shader's edge glow, from the normals), and a ring
        // on the ground, brighter at its rim (vertex red 0: no edge glow).
        static Mesh Mesh()
        {
            if (made != null) return made;
            const int sides = 48;
            var v = new List<Vector3>(); var c = new List<Color>(); var t = new List<int>(); var nm = new List<Vector3>();
            for (int i = 0; i <= sides; i++)
            {
                float a = i * Mathf.PI * 2 / sides, x = Mathf.Sin(a) * Radius, z = Mathf.Cos(a) * Radius;
                var outward = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a));
                v.Add(new Vector3(x, 0, z)); c.Add(new Color(1, 1, 1, 0.9f)); nm.Add(outward);
                v.Add(new Vector3(x, Height * 0.12f, z)); c.Add(new Color(1, 1, 1, 0.45f)); nm.Add(outward);
                v.Add(new Vector3(x, Height, z)); c.Add(new Color(1, 1, 1, 0)); nm.Add(outward);
            }
            for (int i = 0; i < sides; i++)
            {
                int a = i * 3, b = a + 3;
                t.AddRange(new[] { a, a + 1, b, b, a + 1, b + 1, a + 1, a + 2, b + 1, b + 1, a + 2, b + 2 });
            }
            int ring = v.Count;
            for (int i = 0; i <= sides; i++)
            {
                float a = i * Mathf.PI * 2 / sides, s = Mathf.Sin(a), k = Mathf.Cos(a);
                v.Add(new Vector3(s * Radius * 0.55f, 0.06f, k * Radius * 0.55f)); c.Add(new Color(0, 1, 1, 0.08f)); nm.Add(Vector3.up);
                v.Add(new Vector3(s * Radius, 0.06f, k * Radius)); c.Add(new Color(0, 1, 1, 0.9f)); nm.Add(Vector3.up);
            }
            for (int i = 0; i < sides; i++)
            {
                int a = ring + i * 2, b = a + 2;
                t.AddRange(new[] { a, a + 1, b, b, a + 1, b + 1 });
            }
            made = new Mesh { name = "Job marker" };
            made.SetVertices(v);
            made.SetColors(c);
            made.SetNormals(nm);
            made.SetTriangles(t, 0);
            made.bounds = new Bounds(new Vector3(0, Height / 2, 0), new Vector3(Radius * 2, Height, Radius * 2));
            return made;
        }
    }
}
