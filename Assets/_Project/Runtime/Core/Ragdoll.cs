// Bodies that fall and get up again (2026-11-10). Eleven parts of a humanoid get a rigid body and a collider: pelvis, chest, head, each upper and lower arm and leg,
// weighed out of a whole body's mass, and joined where the body bends, each joint only as far as a person's does (an elbow and a knee one way only). While the clips
// move the body these parts follow it, kinematic, and are what a shot or a car meets. Fall lets them go: the animation stops and the body drops under gravity with the
// speed it had and the push it was given. Once it has lain still, it gets up: by the clip for getting up from the back or from the front, as it lies, the person
// turned and placed so the clip starts where the body is, and the fallen pose blending into the clip over a third of a second.
using System.Collections.Generic;
using UnityEngine;

namespace Dad.Core
{
    [DefaultExecutionOrder(120)] // after everything else that poses the body: the getting up blends from the fallen pose over all of it
    public sealed class Ragdoll : MonoBehaviour
    {
        /// <summary>Kilograms, the whole body.</summary>
        public float Mass = 70;
        public bool Down { get; private set; }
        public bool Rising { get; private set; }
        /// <summary>While down: how fast the trunk still moves, metres a second (its turn counted at 0.3 m a radian).</summary>
        public float Motion { get; private set; }
        /// <summary>Seconds it has to lie still before getting up; zero or less, it stays down.</summary>
        public float Wait = 1.2f;
        /// <summary>Seconds down after which it gets up even if the trunk has not been still for Wait, as long as it is only rocking where it lies.</summary>
        public float Longest = 5f;

        // (bone, the bone it reaches to, the part it hangs from, share of the mass, shape: c capsule, b box, s sphere, joint: k knee, e elbow, f free)
        static readonly (HumanBodyBones bone, HumanBodyBones to, HumanBodyBones parent, float share, char shape, char joint)[] Parts =
        {
            (HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.LastBone, 0.16f, 'b', 'f'),
            (HumanBodyBones.Spine, HumanBodyBones.Neck, HumanBodyBones.Hips, 0.28f, 'b', 'f'),
            (HumanBodyBones.Head, HumanBodyBones.Head, HumanBodyBones.Spine, 0.08f, 's', 'f'),
            (HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.Spine, 0.03f, 'c', 'f'),
            (HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, HumanBodyBones.LeftUpperArm, 0.025f, 'c', 'e'),
            (HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.Spine, 0.03f, 'c', 'f'),
            (HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, HumanBodyBones.RightUpperArm, 0.025f, 'c', 'e'),
            (HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.Hips, 0.11f, 'c', 'f'),
            (HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, HumanBodyBones.LeftUpperLeg, 0.06f, 'c', 'k'),
            (HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.Hips, 0.11f, 'c', 'f'),
            (HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, HumanBodyBones.RightUpperLeg, 0.06f, 'c', 'k'),
        };

        Animator body;
        readonly List<Rigidbody> bodies = new List<Rigidbody>();
        readonly List<Collider> colliders = new List<Collider>();
        Transform[] bones;
        Quaternion[] fallen;
        Vector3 fallenHips, lastRoot, rootSpeed;
        float downAt, stillSince, riseAt, riseLength;
        Vector3 riseEnd, riseEndFacing;

        /// <summary>Gives a humanoid its parts and joints, measured on the pose it has now: a standing pose, the model's own (the joints' limits are counted from it).</summary>
        public static Ragdoll Give(Animator a)
        {
            var forward = a.transform.forward;
            float total = 0;
            foreach (var p in Parts) total += p.share;
            float shoulders = Vector3.Distance(a.GetBoneTransform(HumanBodyBones.LeftUpperArm).position, a.GetBoneTransform(HumanBodyBones.RightUpperArm).position);
            float hipWidth = Vector3.Distance(a.GetBoneTransform(HumanBodyBones.LeftUpperLeg).position, a.GetBoneTransform(HumanBodyBones.RightUpperLeg).position);
            var r = a.gameObject.AddComponent<Ragdoll>();
            foreach (var p in Parts)
            {
                var bone = a.GetBoneTransform(p.bone);
                var to = a.GetBoneTransform(p.to);
                var rb = bone.gameObject.AddComponent<Rigidbody>();
                rb.mass = r.Mass * p.share / total;
                rb.isKinematic = true;
                rb.interpolation = RigidbodyInterpolation.None; // while the clips move the bones, nothing of the body's may write them back; it smooths only when falling
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                rb.solverIterations = 16; // a chain of eleven joined parts settles on the default six only slowly, trembling where parts lie against each other
                rb.solverVelocityIterations = 4;
                rb.linearDamping = 0.05f;
                rb.angularDamping = 0.6f;
                var reach = to.position - bone.position;
                float length = reach.magnitude;
                var local = bone.InverseTransformVector(reach); // in the bone's own units
                switch (p.shape)
                {
                    case 'b':
                    {
                        var box = bone.gameObject.AddComponent<BoxCollider>();
                        float width = p.bone == HumanBodyBones.Hips ? hipWidth * 1.7f : shoulders * 1.05f;
                        float depth = p.bone == HumanBodyBones.Hips ? 0.2f : 0.22f;
                        var up = local.normalized;
                        var side = bone.InverseTransformDirection(a.transform.right).normalized;
                        var front = Vector3.Cross(side, up).normalized;
                        // a box along the bone's own axes: take the axis each world direction is nearest to
                        var size = Vector3.zero;
                        Fit(ref size, up, length / bone.lossyScale.x);
                        Fit(ref size, side, width / bone.lossyScale.x);
                        Fit(ref size, front, depth / bone.lossyScale.x);
                        box.size = size;
                        box.center = local / 2;
                        r.colliders.Add(box);
                        break;
                    }
                    case 's':
                    {
                        var ball = bone.gameObject.AddComponent<SphereCollider>();
                        float height = Vector3.Distance(a.GetBoneTransform(HumanBodyBones.Hips).position, bone.position);
                        ball.radius = 0.15f * height / bone.lossyScale.x;
                        ball.center = bone.InverseTransformPoint(bone.position + a.transform.up * 0.11f * height) ;
                        r.colliders.Add(ball);
                        break;
                    }
                    default:
                    {
                        var cap = bone.gameObject.AddComponent<CapsuleCollider>();
                        var abs = new Vector3(Mathf.Abs(local.x), Mathf.Abs(local.y), Mathf.Abs(local.z));
                        cap.direction = abs.x > abs.y && abs.x > abs.z ? 0 : abs.y > abs.z ? 1 : 2;
                        bool leg = p.bone == HumanBodyBones.LeftUpperLeg || p.bone == HumanBodyBones.RightUpperLeg;
                        bool shin = p.joint == 'k';
                        cap.radius = (leg ? 0.085f : shin ? 0.06f : p.joint == 'e' ? 0.045f : 0.055f) * length / (leg ? 0.42f : shin ? 0.42f : 0.28f) / bone.lossyScale.x;
                        cap.height = local.magnitude + cap.radius;
                        cap.center = local / 2;
                        r.colliders.Add(cap);
                        break;
                    }
                }
                r.bodies.Add(rb);
                if (p.parent == HumanBodyBones.LastBone) continue;
                var joint = bone.gameObject.AddComponent<CharacterJoint>();
                joint.connectedBody = a.GetBoneTransform(p.parent).GetComponent<Rigidbody>();
                joint.enablePreprocessing = false;
                joint.enableProjection = true;
                var dir = reach.sqrMagnitude > 1e-6f ? reach.normalized : a.transform.up;
                if (p.joint == 'k' || p.joint == 'e')
                {
                    // the hinge: a knee folds the shin back, an elbow the forearm forward (the twist limit is the bend; no swing)
                    var hinge = p.joint == 'k' ? Vector3.Cross(forward, dir) : Vector3.Cross(dir, forward);
                    joint.axis = bone.InverseTransformDirection(hinge.normalized);
                    joint.swingAxis = bone.InverseTransformDirection(dir);
                    joint.lowTwistLimit = new SoftJointLimit { limit = -3 };
                    joint.highTwistLimit = new SoftJointLimit { limit = p.joint == 'k' ? 135 : 140 };
                    joint.swing1Limit = new SoftJointLimit { limit = 5 };
                    joint.swing2Limit = new SoftJointLimit { limit = 5 };
                }
                else
                {
                    bool arm = p.bone == HumanBodyBones.LeftUpperArm || p.bone == HumanBodyBones.RightUpperArm;
                    bool hip = p.bone == HumanBodyBones.LeftUpperLeg || p.bone == HumanBodyBones.RightUpperLeg;
                    joint.axis = bone.InverseTransformDirection(dir);
                    joint.swingAxis = bone.InverseTransformDirection(Vector3.Cross(dir, forward).normalized);
                    joint.lowTwistLimit = new SoftJointLimit { limit = arm ? -60 : -25 };
                    joint.highTwistLimit = new SoftJointLimit { limit = arm ? 60 : 25 };
                    joint.swing1Limit = new SoftJointLimit { limit = arm ? 85 : hip ? 70 : 35 };
                    joint.swing2Limit = new SoftJointLimit { limit = arm ? 60 : hip ? 35 : 25 };
                }
            }
            return r;
        }

        // The size along whichever of the bone's own axes this direction lies nearest.
        static void Fit(ref Vector3 size, Vector3 dir, float length)
        {
            var abs = new Vector3(Mathf.Abs(dir.x), Mathf.Abs(dir.y), Mathf.Abs(dir.z));
            int i = abs.x > abs.y && abs.x > abs.z ? 0 : abs.y > abs.z ? 1 : 2;
            if (size[i] < length) size[i] = length;
        }

        void Awake()
        {
            body = GetComponent<Animator>();
            if (bodies.Count == 0) // loaded from a prefab: find what Give made, on the body's own bones only (not a gun in the hand)
                foreach (var p in Parts)
                {
                    var rb = body.GetBoneTransform(p.bone).GetComponent<Rigidbody>();
                    if (rb == null) continue;
                    bodies.Add(rb);
                    colliders.AddRange(rb.GetComponents<Collider>());
                }
            var all = new List<Transform>();
            foreach (var t in GetComponentsInChildren<Transform>(true)) if (t != transform && t.GetComponent<Renderer>() == null) all.Add(t);
            bones = all.ToArray();
            fallen = new Quaternion[bones.Length];
        }

        void Start()
        {
            // the person's own capsule walks with these parts inside it: they must not push each other
            var capsule = GetComponentInParent<CharacterController>();
            var hull = transform.parent != null ? transform.parent.GetComponent<Collider>() : null;
            foreach (var c in colliders)
            {
                if (capsule != null) Physics.IgnoreCollision(capsule, c);
                if (hull != null) Physics.IgnoreCollision(hull, c);
            }
            lastRoot = Root.position;
        }

        Transform Root => transform.parent != null ? transform.parent : transform;

        /// <summary>The body goes limp and falls, keeping the speed it had, with a push (an impulse, kg m/s) at a point: a shot, a car, a blow.</summary>
        public void Fall(Vector3 push = default, Vector3 at = default)
        {
            if (Down) return;
            Down = true; Rising = false;
            downAt = Time.time; stillSince = -1;
            var walker = GetComponentInParent<OnFoot>();
            if (walker != null) walker.Held = true;
            body.enabled = false;
            Rigidbody nearest = null;
            float best = float.MaxValue;
            foreach (var rb in bodies)
            {
                rb.isKinematic = false;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.linearVelocity = rootSpeed;
                rb.angularVelocity = Vector3.zero;
                float d = (rb.worldCenterOfMass - at).sqrMagnitude;
                if (d < best) { best = d; nearest = rb; }
            }
            if (push != Vector3.zero && nearest != null) nearest.AddForceAtPosition(push, at == default ? nearest.worldCenterOfMass : at, ForceMode.Impulse);
        }

        void Update()
        {
            if (!Down)
            {
                if (Time.deltaTime > 0) rootSpeed = (Root.position - lastRoot) / Time.deltaTime;
                lastRoot = Root.position;
                return;
            }
            if (Rising)
            {
                if (Time.time - riseAt >= riseLength) Stand();
                return;
            }
            if (Wait <= 0) return;
            // lying still: the trunk (pelvis and chest, the first two parts) no longer moving; a hand settling or a foot rocking does not keep a person down
            Motion = 0;
            for (int i = 0; i < 2 && i < bodies.Count; i++) Motion = Mathf.Max(Motion, bodies[i].linearVelocity.magnitude, bodies[i].angularVelocity.magnitude * 0.3f);
            bool still = Motion <= 0.15f;
            if (!still) stillSince = -1;
            else if (stillSince < 0) stillSince = Time.time;
            float down = Time.time - downAt;
            // or, after Longest, when it is only rocking, not sliding or rolling: a body that comes to rest at an angle can tremble there without ever settling
            if (down >= 1.5f && (still && Time.time - stillSince >= Wait || down >= Longest && Motion < 0.6f)) Rise();
        }

        /// <summary>Which way the chest faces: up, lying on the back.</summary>
        public bool FaceUp
        {
            get
            {
                var side = body.GetBoneTransform(HumanBodyBones.RightUpperLeg).position - body.GetBoneTransform(HumanBodyBones.LeftUpperLeg).position;
                var along = body.GetBoneTransform(HumanBodyBones.Head).position - body.GetBoneTransform(HumanBodyBones.Hips).position;
                return Vector3.Cross(side, along).y > 0;
            }
        }

        /// <summary>Gets up now, by the clip for the way the body lies.</summary>
        public void Rise()
        {
            var person = GetComponentInParent<Person>();
            if (!Down || Rising || person == null) return;
            string clip = FaceUp ? "getup-back" : "getup-front";
            Transform hips = body.GetBoneTransform(HumanBodyBones.Hips), head = body.GetBoneTransform(HumanBodyBones.Head);
            for (int i = 0; i < bones.Length; i++) fallen[i] = bones[i].localRotation;
            fallenHips = hips.position;
            fallenHipsTurn = hips.rotation; // in the world: the person's root is about to turn under it
            var lying = Flat(head.position - hips.position);
            foreach (var rb in bodies) { rb.isKinematic = true; rb.interpolation = RigidbodyInterpolation.None; }
            body.enabled = true;
            riseLength = person.Play(clip, 0); // at once: the fallen pose blends into it here (LateUpdate)
            if (riseLength <= 0) { body.enabled = false; foreach (var rb in bodies) rb.isKinematic = false; return; }
            var root = Root;
            // turn the person so the clip's first frame lies the way the body lies, and place it so the clip's hips are where the body's are
            var startHips = person.At(HumanBodyBones.Hips, 0);
            var startAlong = Flat(person.At(HumanBodyBones.Head, 0) - startHips);
            float turn = Vector3.SignedAngle(root.rotation * startAlong, lying, Vector3.up);
            root.rotation = Quaternion.AngleAxis(turn, Vector3.up) * root.rotation;
            var at = fallenHips - root.rotation * new Vector3(startHips.x, 0, startHips.z);
            at.y = fallenHips.y - startHips.y; // no ground found: as high as the hips lie
            float nearest = float.MaxValue;
            int n = Physics.RaycastNonAlloc(at + Vector3.up * 1.5f, Vector3.down, hits, 4, WorldLayers.Ground | 1, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
                if (!hits[i].collider.transform.IsChildOf(root) && hits[i].distance < nearest) { nearest = hits[i].distance; at.y = hits[i].point.y; } // the ground, not its own body
            root.position = at;
            // where the clip ends, so the person can be put there when it is over
            riseEnd = person.At(HumanBodyBones.Hips, riseLength - 0.02f);
            var endSide = person.At(HumanBodyBones.RightUpperLeg, riseLength - 0.02f) - person.At(HumanBodyBones.LeftUpperLeg, riseLength - 0.02f);
            riseEndFacing = Flat(Vector3.Cross(endSide, Vector3.up));
            riseAt = Time.time;
            Rising = true;
        }

        void Stand()
        {
            var person = GetComponentInParent<Person>();
            var root = Root;
            var moveBy = root.rotation * new Vector3(riseEnd.x, 0, riseEnd.z);
            if (riseEndFacing.sqrMagnitude > 1e-4f) root.rotation = root.rotation * Quaternion.FromToRotation(Vector3.forward, riseEndFacing.normalized);
            root.position += moveBy;
            person.Stop();
            Rising = false; Down = false;
            lastRoot = root.position; rootSpeed = Vector3.zero;
            var walker = GetComponentInParent<OnFoot>();
            if (walker != null) walker.Held = false;
        }

        void LateUpdate()
        {
            if (!Rising) return;
            float w = 1 - (Time.time - riseAt) / 0.35f;
            if (w <= 0) return;
            var hips = body.GetBoneTransform(HumanBodyBones.Hips);
            for (int i = 0; i < bones.Length; i++) if (bones[i] != hips) bones[i].localRotation = Quaternion.Slerp(bones[i].localRotation, fallen[i], w);
            hips.SetPositionAndRotation(Vector3.Lerp(hips.position, fallenHips, w), Quaternion.Slerp(hips.rotation, fallenHipsTurn, w));
        }

        readonly RaycastHit[] hits = new RaycastHit[8];
        Quaternion fallenHipsTurn;

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0, v.z);
    }
}
