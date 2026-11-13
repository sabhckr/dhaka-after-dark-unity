// Head tracking for the protagonists (2026-11-10), by Unity's Animation Rigging: a Rig with two Multi-Aim constraints, the neck taking part of the turn and the head the
// rest, both aiming at one point. This component moves that point: to whatever the person watches (Person.Watch) when it is near and in front, otherwise to a point
// straight ahead at eye height, and fades the rig in and out, so the head turns to a thing and back, never snapping. The constraints run inside the animation, after
// the clips (the Rig Builder's output comes after Person's), so the rest of the body, the feet on the ground and the hands on a gun, are posed after it.
using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace Dad.Core
{
    public sealed class Gaze : MonoBehaviour
    {
        /// <summary>How far, in degrees, the head turns from the body at most; how far away, in metres, something is still looked at.</summary>
        public float Limit = 70, Range = 8;
        public Rig Rig;
        public Transform Point;
        /// <summary>What is looked at now, if anything (for tests).</summary>
        public bool Looking { get; private set; }

        Person person;
        Transform head;

        /// <summary>Builds the rig on a humanoid: neck and head aimed at a point that is part of the model, so it travels in the prefab.</summary>
        public static Gaze Give(Animator a)
        {
            var g = a.gameObject.AddComponent<Gaze>();
            var rigGo = new GameObject("Rig");
            rigGo.transform.SetParent(a.transform, false);
            g.Rig = rigGo.AddComponent<Rig>();
            g.Rig.weight = 0;
            var point = new GameObject("LookAt").transform;
            point.SetParent(a.transform, false);
            var headBone = a.GetBoneTransform(HumanBodyBones.Head);
            point.position = headBone.position + a.transform.forward * 2;
            g.Point = point;
            Aim(rigGo.transform, "Neck", a.GetBoneTransform(HumanBodyBones.Neck), point, a.transform, 0.35f, g.Limit * 0.4f);
            Aim(rigGo.transform, "Head", headBone, point, a.transform, 1f, g.Limit * 0.6f);
            var builder = a.gameObject.GetComponent<RigBuilder>();
            if (builder == null) builder = a.gameObject.AddComponent<RigBuilder>();
            builder.layers.Add(new RigLayer(g.Rig, true));
            return g;
        }

        static void Aim(Transform rig, string name, Transform bone, Transform point, Transform body, float weight, float limit)
        {
            var go = new GameObject(name);
            go.transform.SetParent(rig, false);
            var c = go.AddComponent<MultiAimConstraint>();
            c.weight = weight;
            c.data.constrainedObject = bone;
            var sources = new WeightedTransformArray { new WeightedTransform(point, 1) };
            c.data.sourceObjects = sources;
            c.data.aimAxis = Nearest(bone.InverseTransformDirection(body.forward));
            c.data.upAxis = Nearest(bone.InverseTransformDirection(body.up));
            c.data.constrainedXAxis = c.data.constrainedYAxis = c.data.constrainedZAxis = true;
            c.data.limits = new Vector2(-limit, limit);
            c.data.maintainOffset = true; // the bone's own lean in the clip stays; only the turn towards the point is added
        }

        // The bone's own axis nearest a direction given in the bone's frame.
        static MultiAimConstraintData.Axis Nearest(Vector3 d)
        {
            var abs = new Vector3(Mathf.Abs(d.x), Mathf.Abs(d.y), Mathf.Abs(d.z));
            if (abs.x >= abs.y && abs.x >= abs.z) return d.x > 0 ? MultiAimConstraintData.Axis.X : MultiAimConstraintData.Axis.X_NEG;
            if (abs.y >= abs.z) return d.y > 0 ? MultiAimConstraintData.Axis.Y : MultiAimConstraintData.Axis.Y_NEG;
            return d.z > 0 ? MultiAimConstraintData.Axis.Z : MultiAimConstraintData.Axis.Z_NEG;
        }

        void Awake()
        {
            var a = GetComponent<Animator>();
            head = a.GetBoneTransform(HumanBodyBones.Head);
        }

        void Update()
        {
            if (person == null) person = GetComponentInParent<Person>();
            if (Rig == null || Point == null || head == null) return;
            var body = person != null ? person.transform : transform;
            var eyes = body.position + body.up * (head.position.y - body.position.y);
            var ahead = eyes + body.forward * 2;
            var watch = person != null ? person.Watch : null;
            Looking = false;
            var want = ahead;
            if (watch != null)
            {
                var to = watch.position - head.position;
                if (to.sqrMagnitude < Range * Range && Vector3.Angle(body.forward, Flat(to)) < Limit + 20) { want = watch.position; Looking = true; }
            }
            float k = 1 - Mathf.Exp(-Time.deltaTime * 6);
            Point.position = Vector3.Lerp(Point.position, want, k);
            Rig.weight = Mathf.MoveTowards(Rig.weight, Looking ? 1 : 0, Time.deltaTime * 2.5f);
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0, v.z);
    }
}
