// Feet on the ground that is there (2026-11-10). The clips were made on a flat floor: on a kerb, a step or a slope one foot would hang in the air and the other sink in.
// After the clip has posed the body, each foot looks down for the ground under it; the hips come down by as much as the lower foot must go, each leg bends so its foot
// stands as high above the ground under it as it stood above the floor in the clip, and the foot turns to lie along the slope. Nothing changes on flat ground.
using UnityEngine;

namespace Dad.Core
{
    [DefaultExecutionOrder(105)] // after the animation and Person's turn of the head, before Armed puts the hands on the gun
    public sealed class Footing : MonoBehaviour
    {
        /// <summary>How far a foot may be lifted or lowered from where the clip put it, in metres.</summary>
        public float Reach = 0.4f;
        /// <summary>What the feet stand on.</summary>
        public int Ground = WorldLayers.Ground | 1;
        /// <summary>The last frame's offsets, for tests: how far each foot moved from the clip's, and the hips.</summary>
        public float LeftLift, RightLift, HipsDrop;

        Animator body;
        Transform hips;
        float drop; // eased, so the hips do not jump from one frame to the next

        public static Footing Give(Animator body)
        {
            var f = body.gameObject.AddComponent<Footing>();
            return f;
        }

        void Awake()
        {
            body = GetComponent<Animator>();
            hips = body.GetBoneTransform(HumanBodyBones.Hips);
        }

        void LateUpdate()
        {
            if (!body.enabled || hips == null) return;
            var root = transform.parent != null ? transform.parent : transform; // the person's root stands on the floor the clip was made on
            float floor = root.position.y;
            Want(HumanBodyBones.LeftFoot, floor, out var leftAt, out var leftNormal, out float left);
            Want(HumanBodyBones.RightFoot, floor, out var rightAt, out var rightNormal, out float right);
            float need = Mathf.Min(0, Mathf.Min(left, right));
            drop = Mathf.Lerp(drop, need, 1 - Mathf.Exp(-Time.deltaTime * 15));
            hips.position += Vector3.up * drop;
            HipsDrop = drop;
            LeftLift = Place(HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, leftAt, leftNormal, left);
            RightLift = Place(HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, rightAt, rightNormal, right);
        }

        // Where the ground under a foot is, against the floor of the clip: the change in height the foot needs (up is positive), and the slope.
        void Want(HumanBodyBones foot, float floor, out Vector3 at, out Vector3 normal, out float change)
        {
            var f = body.GetBoneTransform(foot);
            at = f.position; normal = Vector3.up; change = 0;
            int n = Physics.RaycastNonAlloc(new Vector3(at.x, floor + Reach + 0.5f, at.z), Vector3.down, hits, 2 * Reach + 0.5f, Ground, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                if (hits[i].collider.transform.IsChildOf(transform.root) || hits[i].distance >= best) continue; // not its own body
                best = hits[i].distance;
                change = Mathf.Clamp(hits[i].point.y - floor, -Reach, Reach);
                normal = hits[i].normal;
            }
        }

        readonly RaycastHit[] hits = new RaycastHit[8];

        float Place(HumanBodyBones upper, HumanBodyBones lower, HumanBodyBones foot, Vector3 clipAt, Vector3 normal, float change)
        {
            Transform a = body.GetBoneTransform(upper), b = body.GetBoneTransform(lower), c = body.GetBoneTransform(foot);
            if (a == null || b == null || c == null) return 0;
            var target = clipAt + Vector3.up * change;
            float moved = target.y - c.position.y;
            if (Mathf.Abs(moved) > 1e-4f) Armed.Bend(a, b, c, target, transform.right);
            if (normal != Vector3.up) c.rotation = Quaternion.FromToRotation(Vector3.up, normal) * c.rotation;
            return moved;
        }
    }
}
