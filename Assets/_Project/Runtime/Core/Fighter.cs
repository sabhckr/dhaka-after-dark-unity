// Armed people who fight (2026-11-10; the way they fight, 2026-11-12, Docs/Characters/SHOOTER_BAR_AUDIT.md section 11): gang members and police (People/fighters.json).
// Each looks for the nearest living one of the other side it can see (or you, when that side is after you), and fights them as a soldier would:
//   - into cover first: the nearest place beside something solid where, crouching, they cannot be seen from where the shots come (Cover), run to by the way round
//     what is in the way (Navigation);
//   - from cover, in turns: down behind it a moment (reloading there when the magazine runs low), then up over it or out round its side to fire, and down again;
//     hurt while up, they drop back at once;
//   - the police move up: after a few turns, to cover nearer, until close; with none nearer they walk in firing;
//   - with no cover to be had, they fight in the open, stepping from side to side as they fire, and look again for cover now and then;
//   - a gang member badly hurt falls back to cover further off.
// One they can no longer see is remembered for a while where last seen, and not fired at until seen again. A shot is a ray from the muzzle, thrown off the line by the
// gun's spread and by moving; what it meets first stops it, a wall or a body. The gang on the streets (Turf) stand about unarmed and calm until shots are heard near
// them or one is hurt: then the guns come out, and after a quiet while they go away again. The dead fall (Hitbox: ragdoll) and the gun drops from their hands.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Dad.Core
{
    [DefaultExecutionOrder(50)] // decides before Armed puts the gun where the hands are
    public sealed class Fighter : MonoBehaviour, IHurt
    {
        public enum Side { Gang, Police }

        /// <summary>What they are doing: standing about unarmed (Calm), armed with nobody to fight (Ready), walking somewhere (Going), running to cover (Rushing), down
        /// in cover (Hiding), up or out of it firing (Peeking), fighting in the open (Open), or walking in on someone, firing (Advancing).</summary>
        public enum Mode { Calm, Ready, Going, Rushing, Hiding, Peeking, Open, Advancing }

        public Side Team { get; private set; }
        public NpcRecord Who { get; private set; }
        public Person Body { get; private set; }
        public Armed Gun { get; private set; }
        public float Health = 100;
        public bool Dead => Health <= 0;
        /// <summary>Time.time this one fell, for clearing the body away (Remains).</summary>
        public float DiedAt { get; private set; }
        /// <summary>Where this one is going when no enemy is in sight: police, to where shots were heard.</summary>
        public Vector3? Goal;
        public Fighter Target { get; private set; }
        /// <summary>Fighting you rather than Target: police while you are wanted, gang members once you have shot at them (Heat).</summary>
        public bool OnPlayer { get; private set; }
        /// <summary>The one fought is in sight now (not only remembered).</summary>
        public bool InSight { get; private set; }
        /// <summary>Aims but does not fire.</summary>
        public bool HoldFire;
        /// <summary>Holds their ground: fights from where they stand, taking no cover and not moving (a guard at a door).</summary>
        public bool Stay;
        /// <summary>Shots fired and shots that struck someone, for tests and the record.</summary>
        public int Fired, Struck;
        public Mode Now { get; private set; } = Mode.Ready;
        /// <summary>The cover held or being run to.</summary>
        public Cover.Spot Spot { get; private set; }
        /// <summary>Rounds in the gun; whether a reload is going on.</summary>
        public int Rounds { get; private set; }
        public bool Reloading => reloadedAt > 0;
        /// <summary>Times this one has taken cover, peeked out of it, reloaded, and moved up to cover nearer, for tests and the record.</summary>
        public int Covers, Peeks, Reloads, MovesUp;
        /// <summary>Where they stand about on the street when calm (Turf), and the clip they stand about with.</summary>
        public Vector3? Home;
        public string Loiter = "smoke";

        public static readonly List<Fighter> All = new List<Fighter>();
        /// <summary>What a shot can meet: the street's solid things, the default layer, and people's parts (Hitbox), yours included.</summary>
        public const int Hits = WorldLayers.Solid | 1 | WorldLayers.Bodies;
        public const float Sight = 70, CloseIn = 25; // police move up to this, then hold
        /// <summary>Metres within which the calm hear a shot; seconds one out of sight is remembered.</summary>
        public const float Hearing = 60, Memory = 8;
        /// <summary>Seconds of quiet before the gang put their guns away (a test may shorten it).</summary>
        public static float Quiet = 30;
        /// <summary>Metres a second: walking, running, moving crouched.</summary>
        public const float WalkPace = 1.4f, RunPace = 3.8f, CrouchPace = 1.0f;

        CapsuleCollider hull;
        string playing;
        float nextShot, nextLook, nextWarning, nextSearch, modeUntil, reloadedAt, lastSeen, quietSince = -99, nextRoute, strafeWait, peekAt;
        int burst, turns;
        bool moving, running, fresh, crouched;
        Vector3 lastKnown, heardAt, strafeTo;
        readonly List<Vector3> route = new List<Vector3>();

        /// <summary>Spawns a fighter from a record of People/fighters.json, standing at a place and facing a heading in degrees: armed, or calm (no gun out yet).</summary>
        public static Fighter Spawn(NpcRecord who, Side side, Vector3 at, float heading, bool calm = false)
        {
            if (All.Count == 0) { Navigation.Forget(); Cover.Clear(); } // a new fight: the ground is measured afresh, for the street may have changed since the last
            if (Cast.Other(who.model) != who.model) who = who.As(Cast.Other(who.model)); // nobody fights you in your own body
            var p = Person.Spawn(who.model, at, heading);
            if (p == null) return null;
            p.name = who.id;
            var head = p.Body.GetBoneTransform(HumanBodyBones.Head);
            float natural = (head.position.y - at.y) / 0.885f; // as the crowd sizes people
            if (natural > 0.1f) p.transform.localScale = Vector3.one * (who.height / natural);
            p.Stride = 0.885f * who.height;
            p.Directional = true; // stepping every way: to the side and back while the gun stays on the mark
            NpcCrowd.Dress(p, who);
            var f = p.gameObject.AddComponent<Fighter>();
            f.Team = side;
            f.Who = who;
            f.Body = p;
            Hitbox.Give(p.Body); // after the person is sized, before the clips first move it
            // the capsule it walks in: solid to anyone walking into it, but a shot passes it and meets the parts inside (Ignore Raycast)
            p.gameObject.layer = 2;
            f.hull = p.gameObject.AddComponent<CapsuleCollider>();
            f.hull.radius = 0.25f / p.transform.localScale.x;
            f.hull.height = who.height / p.transform.localScale.x;
            f.hull.center = Vector3.up * f.hull.height / 2;
            if (calm) f.StandAbout();
            else f.Draw();
            return f;
        }

        void OnEnable() { All.Add(this); Danger.Heard += Heard; }
        void OnDisable() { All.Remove(this); Danger.Heard -= Heard; Cover.Leave(this); }

        public Vector3 Chest
        {
            get
            {
                var c = Body.Body.GetBoneTransform(HumanBodyBones.UpperChest) ?? Body.Body.GetBoneTransform(HumanBodyBones.Chest);
                return c != null ? c.position : transform.position + Vector3.up * Who.height * 0.72f;
            }
        }

        Vector3 Eye => Body.Body.GetBoneTransform(HumanBodyBones.Head).position;

        /// <summary>The gun out: the stance of holding it, a full magazine.</summary>
        public void Draw()
        {
            if (Gun != null || Dead) return;
            Gun = Armed.Give(Body, Who.gun); // after the person is sized: the gun keeps its own size
            if (Gun == null) return;
            Rounds = Magazine;
            if (playing != null) { playing = null; Body.Stop(0.2f); }
            Now = Mode.Ready;
            Stance(false);
            quietSince = Time.time;
        }

        /// <summary>Unarmed, standing about where they belong with the Loiter clip; the gun, if out, put away.</summary>
        public void StandAbout()
        {
            if (Gun != null) { Gun.Stow(); Gun = null; }
            Cover.Leave(this);
            Target = null;
            OnPlayer = false;
            reloadedAt = 0;
            Body.Speed = 0;
            Body.Use(0, null); Body.Use(1, null); Body.Use(2, null);
            Act(Loiter);
            Now = Mode.Calm;
        }

        void Act(string clip)
        {
            if (playing == clip) return;
            playing = clip;
            Body.Play(clip);
        }

        // Standing, walking and running with the gun, or crouched behind cover.
        void Stance(bool down)
        {
            if (Gun == null) return;
            crouched = down;
            var g = Gun.Gun;
            if (down) { Body.Use(0, "crouch-idle"); Body.Use(1, "crouch-walk"); Body.Use(2, "crouch-walk"); } // right down, the gun low: the crouched aim keeps the head 10 cm higher
            else { Body.Use(0, g.Pistol && (Target != null || OnPlayer) ? g.Fire : g.Idle); Body.Use(1, g.Walk); Body.Use(2, g.Pistol ? g.Walk : "rifle-run"); } // a pistol comes up from low ready to the arms' length
        }

        /// <summary>Rounds a magazine holds, as yours (Arsenal).</summary>
        public int Magazine => Gun == null ? 0 : Gun.Gun.Pistol ? 17 : Gun.Gun == Armed.Ump ? 25 : 20;
        float ReloadTime => Gun.Gun.Pistol ? 1.4f : Gun.Gun == Armed.Ump ? 2.0f : 2.2f;

        // A shot heard: the calm draw; anyone not yet fighting turns to it.
        void Heard(Vector3 at, float reach, Danger.Kind kind)
        {
            if (Dead || kind != Danger.Kind.Shot || Vector3.Distance(at, transform.position) > Mathf.Min(reach, Hearing)) return;
            heardAt = at;
            quietSince = Time.time;
            if (Now == Mode.Calm) Draw();
        }

        bool Sees(Fighter other) => !Physics.Linecast(Eye, other.Chest, WorldLayers.Solid);

        Fighter Nearest()
        {
            Fighter best = null;
            float bestDistance = Sight;
            foreach (var f in All)
            {
                if (f.Team == Team || f.Dead || f.Now == Mode.Calm) continue; // nobody shoots the unarmed
                float d = Vector3.Distance(transform.position, f.transform.position);
                if (d < bestDistance && Sees(f)) { best = f; bestDistance = d; }
            }
            return best;
        }

        /// <summary>Where the shots go: the chest of the one fought, or where they were last seen.</summary>
        Vector3 Mark => !InSight ? lastKnown + Vector3.up * 1.3f : OnPlayer ? Vitals.Player.Chest : Target.Chest;
        /// <summary>Where the shots come from: the eyes of the one fought, a little above the chest.</summary>
        Vector3 Threat => Mark + Vector3.up * 0.35f;
        Vector3 There => !InSight ? lastKnown : OnPlayer ? Vitals.Player.transform.position : Target.transform.position;

        // You, if this side is after you, in sight (or seen a moment ago), and nearer than any enemy of their own.
        bool AfterYou(out bool seen)
        {
            seen = false;
            var you = Vitals.Player;
            if (you == null || you.Dead || !you.isActiveAndEnabled || !(Team == Side.Police ? Heat.Level > 0 : Heat.GangAfter)) return false;
            float d = Vector3.Distance(transform.position, you.transform.position);
            if (d > Sight || Target != null && InSight && Vector3.Distance(transform.position, Target.transform.position) < d) return false;
            seen = !Physics.Linecast(Eye, you.Chest, WorldLayers.Solid);
            if (!seen) return OnPlayer && Time.time - lastSeen < Memory;
            if (Team == Side.Police) Heat.Seen();
            return true;
        }

        void Update()
        {
            if (Dead) return;
            if (Now == Mode.Calm) { Body.Speed = 0; return; }
            if (Gun == null) return;
            if (Time.time >= nextLook || Target != null && Target.Dead)
            {
                Look();
                nextLook = Time.time + 0.5f;
            }
            if (OnPlayer && (Vitals.Player == null || Vitals.Player.Dead || !Vitals.Player.isActiveAndEnabled)) OnPlayer = InSight = false;
            if (reloadedAt > 0 && Time.time >= reloadedAt) { reloadedAt = 0; Rounds = Magazine; Gun.Loose = false; }
            moving = running = false;
            if (Target != null || OnPlayer) { quietSince = Time.time; Fight(); }
            else Wander();
            if (!moving) { Body.Speed = Mathf.MoveTowards(Body.Speed, 0, 8 * Time.deltaTime); Body.Going = 0; }
            if (Time.time >= nextWarning) { Danger.Report(transform.position, Danger.Kind.Gun); nextWarning = Time.time + 2; }
        }

        // Who to fight: one seen now, else the one fought before while they are remembered; you, if after you.
        void Look()
        {
            var was = Target;
            bool wasOn = OnPlayer;
            var seen = Nearest();
            InSight = false;
            if (seen != null) { Target = seen; InSight = true; lastSeen = Time.time; lastKnown = seen.transform.position; }
            else if (Target != null && (Target.Dead || Time.time - lastSeen > Memory))
            {
                if (Team == Side.Police && !Target.Dead) Goal = lastKnown; // lost: to where they were last seen, to look
                Target = null;
            }
            OnPlayer = AfterYou(out bool youSeen);
            if (OnPlayer)
            {
                InSight = youSeen;
                if (youSeen) { lastSeen = Time.time; lastKnown = Vitals.Player.transform.position; }
            }
            if ((Target != null || OnPlayer) && (Target != was || OnPlayer != wasOn)) nextShot = Mathf.Max(nextShot, Time.time + Random.Range(0.6f, 1.3f)); // a moment to bring the gun on a new mark
        }

        // Nobody to fight: to where shots were heard, home, or standing ready; the gang put their guns away after a quiet while.
        void Wander()
        {
            Gun.Target = null;
            Cover.Leave(this);
            if (Now != Mode.Ready && Now != Mode.Going) { Now = Mode.Ready; Stance(false); }
            if (Goal.HasValue && Flat(Goal.Value - transform.position).magnitude > 2)
            {
                Now = Mode.Going;
                Walk(Goal.Value, false, null);
                return;
            }
            if (Home.HasValue && Team == Side.Gang && Time.time - quietSince > Quiet)
            {
                if (Flat(Home.Value - transform.position).magnitude > 1) { Now = Mode.Going; Walk(Home.Value, false, null); return; }
                StandAbout();
                return;
            }
            Now = Mode.Ready;
            if (Time.time - quietSince < 6 && heardAt != Vector3.zero) Turn(heardAt, 180); // looking where the shots came from
        }

        void Fight()
        {
            var there = There;
            float distance = Flat(there - transform.position).magnitude;
            Navigation.Need(transform.position);
            if (Rounds <= 0) Reload();
            if (Stay) { Now = Mode.Open; Stance(false); Aim(there); return; }
            switch (Now)
            {
                case Mode.Rushing: // to cover, or back down behind it
                    Gun.Target = Mark;
                    if (!Cover.Hidden(Spot.At, Threat) && Time.time >= nextSearch) { if (!Search(distance)) Now = Mode.Open; return; } // no cover from here any more
                    if (!crouched && Flat(Spot.At - transform.position).magnitude < 1.6f) Stance(true); // the last steps crouched: down as they get there, not after
                    if (!Walk(Spot.At, fresh, fresh ? (Vector3?)null : Mark)) return;
                    if (fresh) { Covers++; fresh = false; }
                    Now = Mode.Hiding;
                    Stance(true);
                    modeUntil = Time.time + Random.Range(0.8f, 2.0f);
                    return;
                case Mode.Hiding:
                    Gun.Target = null; // the gun down with them, not aimed through the wall
                    Turn(transform.position - Spot.Out, 300); // facing what covers them
                    if (!Cover.Hidden(Spot.At, Threat) && Time.time >= nextSearch) { if (!Search(distance)) { Now = Mode.Open; Stance(false); } return; }
                    if (Time.time < modeUntil || Reloading) return;
                    if (Rounds < Magazine / 3) { Reload(); return; } // low: fill up while down
                    if (Team == Side.Police && turns >= 1 && !InSight && Time.time - lastSeen > 3) // out of sight a while: move up on where they were seen
                    {
                        if (!MoveUp(distance)) { Now = Mode.Advancing; Stance(false); }
                        return;
                    }
                    if (Team == Side.Police && distance > CloseIn && turns >= 2 && MoveUp(distance)) return;
                    if (Team == Side.Gang && Health < 35 && turns >= 1 && FallBack(distance)) return;
                    Now = Mode.Peeking;
                    peekAt = Time.time;
                    Peeks++;
                    turns++;
                    Stance(false);
                    modeUntil = Time.time + Random.Range(1.4f, 2.8f);
                    return;
                case Mode.Peeking:
                    if (Walk(Spot.Peek, false, Mark)) { if (Time.time - peekAt > 0.3f) Aim(there); else { Gun.Target = Mark; Turn(there, 300); } } // up (a moment to stand) or a step out, then firing
                    if (Time.time >= modeUntil || Rounds <= 0 || Reloading) Now = Mode.Rushing; // back down
                    return;
                case Mode.Open:
                case Mode.Advancing:
                    if (crouched) Stance(false);
                    if (Time.time >= nextSearch && Search(distance)) return;
                    if (Team == Side.Police && (distance > CloseIn || !InSight && distance > 3)) { Now = Mode.Advancing; Walk(there, false, Mark); Aim(there); return; }
                    Now = Mode.Open;
                    Strafe(there);
                    Aim(there);
                    return;
                default:
                    Stance(false);
                    if (Search(distance)) return;
                    Now = Team == Side.Police && distance > CloseIn ? Mode.Advancing : Mode.Open;
                    return;
            }
        }

        // Cover from where the shots come, at least 4 m from them; for the police no further than they are now. Running to it if found.
        bool Search(float distance)
        {
            nextSearch = Time.time + Random.Range(1.5f, 2.5f);
            if (!Cover.Find(this, transform.position, Threat, 4, Mathf.Max(12, distance + (Team == Side.Police ? 2 : 8)), 0, out var s)) { Cover.Leave(this); return false; }
            Take(s);
            return true;
        }

        bool MoveUp(float distance)
        {
            if (!Cover.Find(this, transform.position, Threat, Mathf.Min(CloseIn - 8, distance - 6), distance - 4, 4, out var s)) return false;
            MovesUp++;
            Take(s);
            return true;
        }

        bool FallBack(float distance)
        {
            if (!Cover.Find(this, transform.position, Threat, distance + 4, distance + 25, 0, out var s)) return false;
            Take(s);
            return true;
        }

        void Take(Cover.Spot s)
        {
            Spot = s;
            Cover.Take(this, s.At);
            Now = Mode.Rushing;
            fresh = true;
            turns = 0;
            route.Clear();
            nextRoute = 0;
            Stance(false);
        }

        // In the open: a few steps to one side, a pause, then the other way, the gun on the mark.
        void Strafe(Vector3 there)
        {
            if (strafeTo == Vector3.zero || Flat(strafeTo - transform.position).magnitude < 0.3f)
            {
                if (strafeWait <= 0) strafeWait = Random.Range(0.3f, 0.9f);
                strafeWait -= Time.deltaTime;
                Turn(there, 300);
                if (strafeWait > 0) return;
                var side = Vector3.Cross(Vector3.up, Flat(there - transform.position).normalized) * (Random.value < 0.5f ? -1 : 1);
                if (Physics.Raycast(transform.position + Vector3.up, side, 4, WorldLayers.Solid, QueryTriggerInteraction.Ignore)) side = -side; // a wall that way
                strafeTo = transform.position + side * Random.Range(1.5f, 3.5f);
                if (Navigation.Ready(transform.position) && NavMesh.SamplePosition(strafeTo, out var on, 1, NavMesh.AllAreas)) strafeTo = on.position;
                route.Clear();
                nextRoute = 0;
                strafeWait = 0;
            }
            if (Walk(strafeTo, false, Mark)) strafeTo = transform.position;
        }

        // Aiming at the mark and, seen, faced, with a clear line from the muzzle, firing: bursts from a rifle, single shots from a pistol.
        void Aim(Vector3 there)
        {
            Gun.Target = Mark;
            Turn(there, 300);
            bool facing = Vector3.Angle(Flat(transform.forward), Flat(there - transform.position)) < 12;
            if (!facing || !InSight || HoldFire || Reloading || Rounds <= 0 || Time.time < nextShot) return;
            if (Physics.Linecast(Gun.Muzzle, Mark, WorldLayers.Solid, QueryTriggerInteraction.Ignore)) return; // the cover they are behind, or a wall between
            Shoot();
            if (burst <= 0) burst = Gun.Gun.Pistol ? 1 : Random.Range(3, 6);
            burst--;
            nextShot = Time.time + (burst > 0 ? 0.11f : Gun.Gun.Pistol ? Random.Range(0.45f, 0.9f) : Random.Range(0.7f, 1.6f));
        }

        void Reload()
        {
            if (Reloading || Gun == null || Rounds >= Magazine) return;
            reloadedAt = Time.time + ReloadTime;
            Reloads++;
            burst = 0;
            var clip = Resources.Load<AnimationClip>(Person.ClipFolder + "reload");
            if (clip != null) { Body.Upper("reload", 0.15f, 0.2f, clip.length / ReloadTime, 1, arms: true); Gun.Loose = true; } // the arms alone: a standing reload's back and head would rise over the cover
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0, v.z);

        void Turn(Vector3 toward, float degreesPerSecond)
        {
            var way = Flat(toward - transform.position);
            if (way.sqrMagnitude < 1e-4f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(way), degreesPerSecond * Time.deltaTime);
        }

        // Towards a place by the way round what is in the way, walking or running, facing where they go or, given a mark, facing it and stepping sideways or back.
        // Kept off others' shoulders and on the ground. True once there.
        bool Walk(Vector3 to, bool run, Vector3? face)
        {
            if (Time.time >= nextRoute || route.Count == 0 || Flat(route[route.Count - 1] - to).magnitude > 0.5f)
            {
                Navigation.Route(transform.position, to, route);
                nextRoute = Time.time + 1;
            }
            while (route.Count > 1 && Flat(route[0] - transform.position).magnitude < 0.35f) route.RemoveAt(0);
            var goal = route.Count > 0 ? route[0] : to;
            var way = Flat(goal - transform.position);
            if (route.Count <= 1 && way.magnitude < 0.25f) { route.Clear(); return true; }
            way.Normalize();
            float speed = crouched ? CrouchPace : run ? RunPace : WalkPace;
            var step = way * Mathf.Min(speed * Time.deltaTime, Flat(goal - transform.position).magnitude);
            foreach (var f in All) // not into anyone's shoulder
            {
                if (f == this || f.Dead) continue;
                var apart = Flat(transform.position - f.transform.position);
                if (apart.magnitude < 0.7f && apart.sqrMagnitude > 1e-4f) step += apart.normalized * (0.7f - apart.magnitude) * 2 * Time.deltaTime;
            }
            var at = transform.position + step;
            if (Physics.Raycast(at + Vector3.up * 1.5f, Vector3.down, out var ground, 4, WorldLayers.Ground | 1, QueryTriggerInteraction.Ignore) && ground.collider != hull) at.y = ground.point.y;
            transform.position = at;
            if (face.HasValue) Turn(face.Value, 300);
            else Turn(transform.position + way, run ? 540 : 300);
            Body.Speed = speed;
            Body.Going = Vector3.SignedAngle(Flat(transform.forward), way, Vector3.up);
            moving = true;
            running = run;
            return false;
        }

        /// <summary>Degrees off the line a shot may go: the gun's own spread, and more on the move.</summary>
        public float Spread => (Gun.Gun.Pistol ? 3.5f : Gun.Gun == Armed.Ump ? 3f : 2.5f) * (Team == Side.Police ? 0.8f : 1.25f) + (running ? 6f : moving ? 1.5f : 0); // police are trained shots; the gang spray
        public float Damage => Gun.Gun.Pistol ? 25 : Gun.Gun == Armed.Ump ? 22 : 30;

        void Shoot()
        {
            Gun.Fire();
            Fired++;
            Rounds--;
            var from = Gun.Muzzle;
            var line = (Mark - from).normalized;
            var off = Quaternion.AngleAxis(Random.Range(0f, 360f), line) * Quaternion.AngleAxis(Random.Range(0f, Spread), Vector3.Cross(line, Vector3.up).normalized);
            if (!Hitbox.Cast(from, off * line, 150, Hits, transform, out var hit)) return; // never its own arm or head
            Impacts.Hit(hit, from);
            if (Hitbox.Strike(hit, Damage, from, out _) != null) Struck++;
        }

        /// <summary>A shot strikes this one, fired from a place. At no health left they go limp and fall with the shot, and the gun drops from their hands; still standing,
        /// they flinch, draw if calm, and if up out of cover drop back behind it.</summary>
        public void Hurt(float damage, Vector3 from, Wound wound = default)
        {
            if (Dead) return;
            Health -= damage;
            heardAt = from;
            quietSince = Time.time;
            if (!Dead)
            {
                Body.Flinch();
                if (Now == Mode.Calm) Draw();
                if (Now == Mode.Peeking) Now = Mode.Rushing; // back down at once
                return;
            }
            DiedAt = Time.time;
            gameObject.AddComponent<Remains>();
            Cover.Leave(this);
            if (Gun != null) { Gun.Target = null; Gun.Drop(); }
            hull.enabled = false;
            Target = null;
            Body.Speed = 0;
            if (Hitbox.Die(Body, from, wound)) return;
            // no ragdoll: a death clip, falling away from the shot (hit in front, back; from behind, forward)
            bool fromFront = Vector3.Dot(transform.forward, from - transform.position) > 0;
            Act(fromFront ? (Random.value < 0.5f ? "death-back" : "death-chest") : "death-forward");
        }
    }
}
