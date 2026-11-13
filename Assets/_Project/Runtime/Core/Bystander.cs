// People on the street can be shot (2026-11-12, Docs/Characters/SHOOTER_BAR_AUDIT.md section 6 step 1). Each walker of the crowd has a body with parts (Hitbox) and some health.
// A shot that wounds them makes them run from where it came; one that kills them drops them limp where they stand (Hitbox.Die). The crowd keeps the body there until it is
// out of reach or unseen for a while (NpcCrowd), then makes the person again, whole, the next time they are needed.
using System;
using UnityEngine;

namespace Dad.Core
{
    public sealed class Bystander : MonoBehaviour, IHurt
    {
        public const float Most = 100;
        public float Health = Most;
        public bool Dead => Health <= 0;
        /// <summary>Time.time they fell; where the last shot came from.</summary>
        public float DiedAt { get; private set; }
        public Vector3 HurtFrom { get; private set; }
        /// <summary>Struck, alive or not: the crowd answers it.</summary>
        public event Action<Bystander> Struck;

        Person person;

        public static Bystander Give(Person p)
        {
            Hitbox.Give(p.Body);
            return p.gameObject.AddComponent<Bystander>();
        }

        void Awake() => person = GetComponent<Person>();

        public void Hurt(float damage, Vector3 from, Wound wound = default)
        {
            if (Dead || damage <= 0) return;
            Health -= damage;
            HurtFrom = from;
            if (!Dead) person.Flinch();
            if (Dead)
            {
                DiedAt = Time.time;
                if (!Hitbox.Die(person, from, wound)) // no ragdoll: a death clip, away from the shot
                    person.Play(Vector3.Dot(transform.forward, from - transform.position) > 0 ? "death-back" : "death-forward");
            }
            Struck?.Invoke(this);
        }
    }

    /// <summary>A body left where it fell, cleared away once nobody is looking: after Linger seconds, when no camera draws it. With no camera at all nothing is cleared.</summary>
    public sealed class Remains : MonoBehaviour
    {
        public static float Linger = 60;
        float since;
        Renderer[] seen;

        void Start()
        {
            since = Time.time;
            seen = GetComponentsInChildren<Renderer>();
        }

        void Update()
        {
            if (Time.time - since < Linger || Camera.main == null) return;
            foreach (var r in seen) if (r != null && r.isVisible) return;
            Destroy(gameObject);
        }
    }
}
