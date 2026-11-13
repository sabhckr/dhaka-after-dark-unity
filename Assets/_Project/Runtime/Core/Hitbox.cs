// Bodies a shot can hit part by part (2026-11-12, Docs/Characters/SHOOTER_BAR_AUDIT.md section 6 step 1).
//
// What a person is made of, for a shot:
// - Everyone on foot gets the eleven parts of a Ragdoll: pelvis, chest, head, and each upper and lower arm and leg. A shot meets those parts, not a capsule round the whole body.
// - Each part's collider is on the Body layer and carries the Hitbox, which says which part it is.
// - The damage a shot does is the gun's, times the part's share: a head takes three times as much, an arm or a leg three quarters.
//
// The Body layer:
// - It meets the ground and other bodies, so the dead fall onto the street and onto each other.
// - It does not meet cars, the player's capsule, or the capsule a fighter walks in. A car is not stopped dead by a pedestrian standing like a wall, and nobody is snagged on anyone's elbow.
//
// When someone dies, they go limp. The ragdoll takes the body from the clips, with the shot's push, and keeps it down; nothing on the body poses it any more.
using UnityEngine;

namespace Dad.Core
{
    /// <summary>What a shot did, beyond its damage: the part it struck and where. Unknown (Known false) when something hurts without a shot, as a test does.</summary>
    public struct Wound
    {
        public bool Known;
        public Hitbox.Zone Zone;
        /// <summary>Where it struck, and the way the shot was going.</summary>
        public Vector3 At, Way;
    }

    public sealed class Hitbox : MonoBehaviour
    {
        public enum Zone { Head, Body, Arm, Leg }

        public Zone Part;

        /// <summary>A gun's damage is times this for a part (as the shooter games tune it: the head far more, the limbs less).</summary>
        public static float Share(Zone z) => z == Zone.Head ? 3f : z == Zone.Body ? 1f : 0.75f;

        /// <summary>The push a fatal shot gives the body as it goes limp, kg m/s: far more than a bullet's own, as games give it, so that the fall reads.</summary>
        public const float Push = 18;

        static readonly (HumanBodyBones bone, Zone zone)[] Parts =
        {
            (HumanBodyBones.Hips, Zone.Body), (HumanBodyBones.Spine, Zone.Body), (HumanBodyBones.Head, Zone.Head),
            (HumanBodyBones.LeftUpperArm, Zone.Arm), (HumanBodyBones.LeftLowerArm, Zone.Arm), (HumanBodyBones.RightUpperArm, Zone.Arm), (HumanBodyBones.RightLowerArm, Zone.Arm),
            (HumanBodyBones.LeftUpperLeg, Zone.Leg), (HumanBodyBones.LeftLowerLeg, Zone.Leg), (HumanBodyBones.RightUpperLeg, Zone.Leg), (HumanBodyBones.RightLowerLeg, Zone.Leg),
        };

        // Bones the ragdoll measures from as well as its parts'.
        static readonly HumanBodyBones[] Also = { HumanBodyBones.Neck, HumanBodyBones.LeftHand, HumanBodyBones.RightHand, HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Layers()
        {
            for (int i = 0; i < 32; i++) Physics.IgnoreLayerCollision(WorldLayers.Body, i, !(i == 0 || i == WorldLayers.World || i == WorldLayers.Body));
        }

        /// <summary>Gives a person's body its parts to be hit by (made at the pose it has now: call it before the clips first move it, and after it is sized). A body that already has a ragdoll (the protagonists) keeps it. Returns the ragdoll; null if the rig lacks a bone it needs, and then the whole figure is one part, the body.</summary>
        public static Ragdoll Give(Animator a)
        {
            var doll = a.GetComponent<Ragdoll>();
            if (doll == null)
            {
                foreach (var p in Parts) if (a.GetBoneTransform(p.bone) == null) return Whole(a, p.bone);
                foreach (var b in Also) if (a.GetBoneTransform(b) == null) return Whole(a, b);
                doll = Ragdoll.Give(a);
            }
            foreach (var p in Parts)
            {
                var bone = a.GetBoneTransform(p.bone);
                if (bone.GetComponent<Collider>() == null) continue;
                bone.gameObject.layer = WorldLayers.Body;
                var h = bone.GetComponent<Hitbox>();
                if (h == null) h = bone.gameObject.AddComponent<Hitbox>();
                h.Part = p.zone;
            }
            return doll;
        }

        // A rig that cannot take a ragdoll: one capsule, the height of the figure, is all of it.
        static Ragdoll Whole(Animator a, HumanBodyBones missing)
        {
            Debug.LogWarning($"[Dad] {a.name} has no {missing} bone: the whole body is one part to hit");
            var bounds = new Bounds(a.transform.position, Vector3.zero);
            foreach (var r in a.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(r.bounds);
            var part = new GameObject("Hit") { layer = WorldLayers.Body };
            part.transform.SetParent(a.transform, false);
            var c = part.AddComponent<CapsuleCollider>();
            float scale = a.transform.lossyScale.y;
            c.height = Mathf.Max(0.5f, bounds.size.y) / scale;
            c.radius = 0.25f / scale;
            c.center = Vector3.up * c.height / 2;
            part.AddComponent<Hitbox>().Part = Zone.Body;
            return null;
        }

        /// <summary>The first thing a ray meets, leaving out one body (the one firing). False if it meets nothing.</summary>
        public static bool Cast(Vector3 from, Vector3 way, float range, int mask, Transform leaveOut, out RaycastHit first)
        {
            int n = Physics.RaycastNonAlloc(from, way, hits, range, mask, QueryTriggerInteraction.Ignore);
            first = default;
            float best = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                if (leaveOut != null && hits[i].collider.transform.IsChildOf(leaveOut)) continue;
                if (hits[i].distance < best) { best = hits[i].distance; first = hits[i]; }
            }
            return best < float.MaxValue;
        }

        static readonly RaycastHit[] hits = new RaycastHit[16];

        /// <summary>A shot of this damage, fired from a place, meets what the ray hit. If that is a body still alive, it is hurt by the part struck, and it is returned. The damage done comes out.</summary>
        public static IHurt Strike(RaycastHit hit, float damage, Vector3 from, out float done)
        {
            done = 0;
            var struck = hit.collider.GetComponentInParent<IHurt>();
            if (struck == null || struck.Dead) return null;
            var part = hit.collider.GetComponent<Hitbox>();
            var wound = new Wound { Known = true, Zone = part != null ? part.Part : Zone.Body, At = hit.point, Way = (hit.point - from).normalized };
            done = damage * Share(wound.Zone);
            struck.Hurt(done, from, wound);
            return struck;
        }

        /// <summary>A person goes limp and stays down: the ragdoll takes the body from the clips with the shot's push. Whatever posed the body (a turned head, a face, the feet on the ground)
        /// stops. False if the body has no ragdoll: then the caller plays a death clip instead.</summary>
        public static bool Die(Person p, Vector3 from, Wound wound)
        {
            if (p == null) return false;
            var doll = p.Body.GetComponent<Ragdoll>();
            if (doll == null) return false;
            p.Watch = null;
            foreach (var b in p.GetComponentsInChildren<Behaviour>())
                if (b is PersonLife || b is Face || b is Footing || b is Grasp) b.enabled = false;
            var rig = p.Body.GetComponent<UnityEngine.Animations.Rigging.RigBuilder>();
            if (rig != null) rig.enabled = false;
            doll.Wait = 0; // the dead do not get up
            var chest = p.Body.GetBoneTransform(HumanBodyBones.Spine).position;
            var at = wound.Known ? wound.At : chest;
            var way = wound.Known ? wound.Way : chest - from;
            way.y = Mathf.Max(way.y, 0) * 0.3f; // a shot pushes the body back, not into the ground or up off it
            doll.Fall(way.sqrMagnitude > 1e-6f ? way.normalized * Push : Vector3.zero, at);
            return true;
        }
    }
}
