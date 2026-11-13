// Your health and armour, and how wanted you are (2026-11-10, Docs/PRODUCTION/PLAYER_INTERFACE.md). Armour takes a shot before health does. Health comes back by itself to
// half, after a few seconds unhurt. At none you are down: Player puts you back where the game began, healed and no longer wanted.
using System;
using UnityEngine;

namespace Dad.Core
{
    /// <summary>Anything a shot can hurt: a fighter, someone on the street, or you. The damage comes already counted for the part struck (Hitbox.Strike); the wound says which part and where.</summary>
    public interface IHurt
    {
        bool Dead { get; }
        void Hurt(float damage, Vector3 from, Wound wound = default);
    }

    public sealed class Vitals : MonoBehaviour, IHurt
    {
        public const float Most = 100, ComesBackTo = 50, ComesBackPerSecond = 2.5f, ComesBackAfter = 5;

        /// <summary>You, while you are in the game.</summary>
        public static Vitals Player { get; private set; }

        public float Health = Most, Armour;
        public bool Dead => Health <= 0;
        /// <summary>Time.time you were last hurt; and where from, for the HUD.</summary>
        public float HurtAt { get; private set; } = -99;
        public Vector3 HurtFrom { get; private set; }
        /// <summary>The last shot's part and place, for going down the way it struck.</summary>
        public Wound LastWound { get; private set; }
        public event Action Downed;

        Person person;

        void Awake() => person = GetComponent<Person>();
        void OnEnable() { if (Player == null) Player = this; }
        void OnDestroy() { if (Player == this) Player = null; }

        public Vector3 Chest
        {
            get
            {
                var b = person != null ? person.Body.GetBoneTransform(HumanBodyBones.UpperChest) ?? person.Body.GetBoneTransform(HumanBodyBones.Chest) : null;
                return b != null ? b.position : transform.position + Vector3.up * 1.3f;
            }
        }

        public void Hurt(float damage, Vector3 from, Wound wound = default)
        {
            if (Dead || damage <= 0) return;
            HurtAt = Time.time;
            HurtFrom = from;
            LastWound = wound;
            float taken = Mathf.Min(Armour, damage);
            Armour -= taken;
            Health = Mathf.Max(0, Health - (damage - taken));
            if (!Dead && person != null) person.Flinch();
            if (GameSettings.Current.Vibration) Rumble.Pulse(0.5f, 0.8f, 0.18f);
            if (Dead) Downed?.Invoke();
        }

        /// <summary>Whole again: full health; armour as given.</summary>
        public void Heal(float armour = 0)
        {
            Health = Most;
            Armour = Mathf.Clamp(armour, 0, Most);
            HurtAt = -99;
        }

        void Update()
        {
            if (!Dead && Health < ComesBackTo && Time.time - HurtAt > ComesBackAfter) Health = Mathf.Min(ComesBackTo, Health + ComesBackPerSecond * Time.deltaTime);
            Heat.Tick();
        }
    }

    /// <summary>How much the police want you (0 to 5), and whether the gangs are after you. Raised by what you do (Arsenal), lost by staying out of the police's sight.</summary>
    public static class Heat
    {
        public const int Top = 5;
        public static int Level { get; private set; }
        public static bool GangAfter { get; private set; }
        /// <summary>Time.time an officer last saw you.</summary>
        public static float SeenAt { get; private set; }
        public static event Action<int> Changed;

        /// <summary>Seconds out of the police's sight it takes to lose them, at the level you are at.</summary>
        public static float LoseAfter => 12 + 6 * Level;
        /// <summary>The share of LoseAfter still to go, for the HUD: 1 just seen, 0 lost.</summary>
        public static float Hunting => Level == 0 ? 0 : Mathf.Clamp01(1 - (Time.time - SeenAt) / LoseAfter);

        public static void Raise(int atLeast)
        {
            atLeast = Mathf.Clamp(atLeast, 0, Top);
            SeenAt = Time.time;
            if (atLeast <= Level) return;
            Level = atLeast;
            Changed?.Invoke(Level);
        }

        /// <summary>One more than now, at least this.</summary>
        public static void Worse(int atLeast) => Raise(Mathf.Max(atLeast, Level + 1));

        public static void TurnGang() => GangAfter = true;

        public static void Seen() { if (Level > 0) SeenAt = Time.time; }

        public static void Clear()
        {
            GangAfter = false;
            if (Level == 0) return;
            Level = 0;
            Changed?.Invoke(0);
        }

        public static void Tick()
        {
            if (Level > 0 && Time.time - SeenAt > LoseAfter) { Level = 0; Changed?.Invoke(0); Notice.Post("You lost the police"); }
        }
    }
}
