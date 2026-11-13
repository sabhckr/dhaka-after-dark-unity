// Your guns (2026-11-10, Docs/PRODUCTION/PLAYER_INTERFACE.md): what you carry, the one in hand, aiming, firing, reloading. The guns are the game's three (Armed), held by the same
// hands and fingers as anyone's. A shot goes from the muzzle to where the crosshair points, thrown off by the gun's spread, by moving and by firing fast, and hurts what it
// meets first (IHurt). Every shot is heard (Danger) and makes you wanted (Heat); hurting the police or a gang member makes it worse, and turns the gang on you.
using System.Collections.Generic;
using UnityEngine;

namespace Dad.Core
{
    public sealed class Arsenal : MonoBehaviour
    {
        public sealed class Slot
        {
            public Armed.Kind Gun;
            public int Magazine, InGun, Spare;
            /// <summary>Seconds between shots; damage of one; degrees off the line a shot may go when aimed.</summary>
            public float Interval, Damage, Spread, Reload;
            /// <summary>Fires while the trigger is held.</summary>
            public bool Automatic;
            public string Name => Gun == Armed.Glock ? "Pistol" : Gun == Armed.Ump ? "SMG" : "Rifle";
        }

        /// <summary>A gun of a kind, loaded, with spare rounds. Damage is as the fighters' (Fighter.Damage).</summary>
        public static Slot Make(Armed.Kind k)
        {
            if (k == Armed.Glock) return new Slot { Gun = k, Magazine = 17, InGun = 17, Spare = 51, Interval = 0.2f, Damage = 25, Spread = 1.2f, Reload = 1.4f };
            if (k == Armed.Ump) return new Slot { Gun = k, Magazine = 25, InGun = 25, Spare = 100, Interval = 0.09f, Damage = 22, Spread = 1.6f, Reload = 2.0f, Automatic = true };
            return new Slot { Gun = k, Magazine = 20, InGun = 20, Spare = 80, Interval = 0.11f, Damage = 30, Spread = 1.0f, Reload = 2.2f, Automatic = true };
        }

        /// <summary>How far a shot carries, and what the crosshair and a shot can meet (never your own body: Hitbox.Cast leaves it out).</summary>
        public const float Range = 150;
        public const int Hits = Fighter.Hits & ~(1 << WorldLayers.Player);

        /// <summary>What you carry; the first is your bare hands.</summary>
        public readonly List<Slot> Slots = new List<Slot> { null };
        public int Current { get; private set; }
        public Slot Now => Slots[Current];
        public Armed Held { get; private set; }
        public bool Aiming { get; private set; }
        public bool Reloading => Time.time < reloaded;
        /// <summary>0 to 1 through a reload.</summary>
        public float ReloadDone => Now == null || !Reloading ? 1 : 1 - (reloaded - Time.time) / Now.Reload;
        /// <summary>Degrees added to the spread by moving and firing fast; the crosshair opens with it.</summary>
        public float Bloom { get; private set; }
        /// <summary>Time.time a shot of yours last struck someone, for the HUD's mark.</summary>
        public float StruckAt { get; private set; } = -99;
        /// <summary>Where the crosshair points.</summary>
        public Vector3 Point { get; private set; }
        public int Shots { get; private set; }
        public int Struck { get; private set; }

        public GameInput Input;
        public OnFoot Body;
        public FootCamera View;

        float reloaded, nextShot, facingUntil;
        bool aimOn, reloadDue;

        public static Arsenal Give(OnFoot body, GameInput input, FootCamera view)
        {
            var a = body.gameObject.AddComponent<Arsenal>();
            a.Body = body;
            a.Input = input;
            a.View = view;
            return a;
        }

        public void Add(Armed.Kind kind)
        {
            foreach (var s in Slots) if (s != null && s.Gun == kind) { s.Spare += s.Magazine * 2; return; }
            Slots.Add(Make(kind));
        }

        /// <summary>Takes the gun in this slot in hand (0: none), putting away the one held.</summary>
        public void Select(int slot)
        {
            slot = Mathf.Clamp(slot, 0, Slots.Count - 1);
            if (slot == Current && (Now == null) == (Held == null)) return;
            if (Held != null) Held.Stow();
            Held = null;
            Current = slot;
            reloaded = 0;
            reloadDue = false;
            if (Now != null) Held = Armed.Give(Body.Person, Now.Gun.Name);
            Body.Gun = Now?.Gun;
        }

        /// <summary>The next (1) or previous (-1) of what you carry, hands included.</summary>
        public void Cycle(int way) => Select((Current + way + Slots.Count) % Slots.Count);

        void Update()
        {
            bool free = Input != null && Body != null && View != null && Input.Context == InputContext.OnFoot && Body.enabled && Time.timeScale > 0.5f;
            if (!free)
            {
                if (Body != null && Input != null && Input.Context != InputContext.OnFoot) { Body.Facing = null; Body.Slow = false; }
                if (View != null && (Input == null || Input.Context != InputContext.OnFoot)) View.Aiming = false;
                Aiming = aimOn = false;
                return;
            }
            if (Input.NextWeapon.WasPressedThisFrame()) Cycle(1);
            if (Input.PreviousWeapon.WasPressedThisFrame()) Cycle(-1);
            bool armed = Now != null && Held != null;
            if (GameSettings.Current.AimToggle) { if (Input.Aim.WasPressedThisFrame()) aimOn = !aimOn; }
            else aimOn = Input.Aim.IsPressed();
            if (!armed) aimOn = false;
            Aiming = aimOn;
            bool trigger = armed && (Now.Automatic ? Input.Attack.IsPressed() : Input.Attack.WasPressedThisFrame());
            if (trigger) facingUntil = Time.time + 1; // fired from the hip, you turn to where you shoot for a moment
            bool facing = Aiming || Time.time < facingUntil;
            Body.Facing = facing ? View.Yaw : (float?)null;
            Body.Slow = Aiming;
            View.Aiming = Aiming;
            var eye = View.transform;
            Point = Hitbox.Cast(eye.position, eye.forward, Range, Hits, Body.transform, out var seen) ? seen.point : eye.position + eye.forward * Range;
            if (armed) Held.Target = facing ? Point : (Vector3?)null;
            float moving = Body.Person != null ? Mathf.Clamp01(Body.Person.Speed / Person.WalkSpeed) : 0;
            Bloom = Mathf.MoveTowards(Bloom, moving * 1.5f, 6 * Time.deltaTime);
            if (!armed) return;
            if (reloadDue && !Reloading) Reloaded();
            if (Input.Reload.WasPressedThisFrame()) StartReload();
            if (!trigger || Reloading || Time.time < nextShot) return;
            if (Now.InGun > 0) Shoot();
            else StartReload();
        }

        public bool StartReload()
        {
            if (Now == null || Reloading || Now.InGun >= Now.Magazine || Now.Spare <= 0) return false;
            reloaded = Time.time + Now.Reload;
            reloadDue = true;
            // the arms do it: the reload clip over the waist up, timed to the gun's reload, the aim let go meanwhile
            var clip = Resources.Load<AnimationClip>(Person.ClipFolder + "reload");
            if (clip != null && Body.Person != null && Held != null)
            {
                Body.Person.Upper("reload", 0.15f, 0.2f, clip.length / Now.Reload);
                Held.Loose = true;
            }
            return true;
        }

        void Reloaded()
        {
            reloadDue = false;
            if (Held != null) Held.Loose = false;
            int take = Mathf.Min(Now.Magazine - Now.InGun, Now.Spare);
            Now.InGun += take;
            Now.Spare -= take;
        }

        /// <summary>One shot at where the crosshair points.</summary>
        public void Shoot()
        {
            var s = Now;
            nextShot = Time.time + s.Interval;
            s.InGun--;
            Shots++;
            Held.Fire();
            Heat.Raise(1); // someone always calls it in
            var from = Held.Muzzle;
            var line = (Point - from).normalized;
            float spread = s.Spread + Bloom + (Aiming ? 0 : 2.5f);
            var off = Quaternion.AngleAxis(Random.Range(0f, 360f), line) * Quaternion.AngleAxis(Random.Range(0f, spread), Vector3.Cross(line, Vector3.up).normalized);
            Bloom = Mathf.Min(6, Bloom + (s.Gun.Pistol ? 1.2f : 0.5f));
            View.Kick += s.Gun.Pistol ? 1.1f : 0.45f;
            Rumble.Pulse(0.15f, 0.45f, 0.06f);
            if (!Hitbox.Cast(from, off * line, Range, Hits, Body.transform, out var hit)) return;
            Impacts.Hit(hit, from); // a hole and dust, or blood, where it struck
            var struck = Hitbox.Strike(hit, s.Damage, from, out float done);
            if (struck == null) return;
            StruckAt = Time.time;
            Struck++;
            LastDamage = done;
            if (struck is Fighter f)
            {
                if (f.Team == Fighter.Side.Police) { if (f.Dead) Heat.Worse(3); else Heat.Raise(2); }
                else { Heat.TurnGang(); Heat.Raise(f.Dead ? 2 : 1); }
            }
            else if (struck is Bystander b) Heat.Raise(b.Dead ? 2 : 1); // shooting people on the street brings the police harder
        }

        /// <summary>The damage the last shot that struck did, counted for the part it struck.</summary>
        public float LastDamage { get; private set; }
    }
}
