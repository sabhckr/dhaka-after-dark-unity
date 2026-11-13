// The cinematic's stagecraft: people posed from clips on a fixed time, props in hands, lights, a graded camera, and frames written to disk. Editor only; a test runs it so the game's world can load.
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Dad.Core;

namespace Dad.Trailer
{
    /// <summary>Someone in a shot. The clip and the time in it are set for every frame, so a frame always comes out the same.</summary>
    public sealed partial class Actor
    {
        public GameObject Root;
        public Animator Body;
        PlayableGraph graph;
        AnimationMixerPlayable mix;
        readonly Dictionary<string, (int input, AnimationClipPlayable play, AnimationClip clip)> clips = new Dictionary<string, (int, AnimationClipPlayable, AnimationClip)>();
        const int Most = 24;

        public Actor(GameObject root, Animator body)
        {
            Root = root; Body = body;
            Body.applyRootMotion = false;
            Body.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var h = body.GetBoneTransform(HumanBodyBones.Head);
            if (h != null) { faceAxis = Quaternion.Inverse(h.rotation) * root.transform.forward; earAxis = Quaternion.Inverse(h.rotation) * root.transform.right; upAxis = Quaternion.Inverse(h.rotation) * root.transform.up; }
            var ch = body.GetBoneTransform(HumanBodyBones.Chest); if (ch != null) { chestAxis = Quaternion.Inverse(ch.rotation) * root.transform.forward; chestUp = Quaternion.Inverse(ch.rotation) * root.transform.up; }
            Transform lowerL = body.GetBoneTransform(HumanBodyBones.LeftLowerArm), lowerR = body.GetBoneTransform(HumanBodyBones.RightLowerArm), handL = body.GetBoneTransform(HumanBodyBones.LeftHand), handR = body.GetBoneTransform(HumanBodyBones.RightHand);
            if (lowerL != null && handL != null) restLeft = Quaternion.Inverse(lowerL.rotation) * handL.rotation;
            if (lowerR != null && handR != null) restRight = Quaternion.Inverse(lowerR.rotation) * handR.rotation;
            FindFace();
            LearnTips();
            graph = PlayableGraph.Create(root.name);
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            mix = AnimationMixerPlayable.Create(graph, Most);
            AnimationPlayableOutput.Create(graph, "Body", Body).SetSourcePlayable(mix);
            graph.Play();
        }

        public Transform Bone(HumanBodyBones b) => Body.GetBoneTransform(b);

        (int input, AnimationClipPlayable play, AnimationClip clip) Get(string name)
        {
            if (clips.TryGetValue(name, out var c)) return c;
            var clip = LoadClip(name);
            if (clip == null) throw new InvalidOperationException("no clip " + name);
            var p = AnimationClipPlayable.Create(graph, clip);
            p.SetApplyFootIK(true);
            int input = clips.Count;
            if (input >= Most) throw new InvalidOperationException("too many clips on " + Root.name);
            graph.Connect(p, 0, mix, input);
            clips[name] = c = (input, p, clip);
            return c;
        }

        public static AnimationClip LoadClip(string name)
        {
            var path = "Assets/Trailer/Clips/" + name + ".fbx";
            var c = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(x => !x.name.StartsWith("__preview", StringComparison.Ordinal));
            return c != null ? c : Resources.Load<AnimationClip>(Person.ClipFolder + name);
        }

        /// <summary>One clip at a time in seconds (repeated if it runs past the end, unless hold), optionally blended into a second one.</summary>
        public void Pose(string clip, double time, string to = null, double toTime = 0, float blend = 0, bool hold = false)
        {
            var a = Get(clip);
            for (int i = 0; i < clips.Count; i++) mix.SetInputWeight(i, 0);
            double len = a.clip.length;
            a.play.SetTime(hold ? Math.Min(time, len - 0.001) : time % len);
            mix.SetInputWeight(a.input, 1 - blend);
            if (to != null && blend > 0)
            {
                var b = Get(to);
                double lb = b.clip.length;
                b.play.SetTime(toTime % lb);
                mix.SetInputWeight(b.input, blend);
            }
            graph.Evaluate(0);
        }

        public double Length(string clip) => Get(clip).clip.length;

        /// <summary>Holds a prop in a hand, placed by its local offset.</summary>
        public GameObject Hold(GameObject prop, HumanBodyBones bone, Vector3 at, Vector3 euler)
        {
            // a bone can carry its model's unit scale; measure offsets and the prop's own size in metres
            var b = Bone(bone); float k = b.lossyScale.x; var size = prop.transform.lossyScale;
            prop.transform.SetParent(b, false);
            prop.transform.localPosition = at / k;
            prop.transform.localRotation = Quaternion.Euler(euler);
            prop.transform.localScale = size / k;
            return prop;
        }

        public void Place(Vector3 at, float heading) => Root.transform.SetPositionAndRotation(at, Quaternion.Euler(0, heading, 0));

        /// <summary>Height of the floor under this person, or NaN to leave them where placed. Kept after every pose: clips were made on another body, so the feet are put back on it.</summary>
        public float Floor = float.NaN;

        /// <summary>Moves the whole body so the hips are at a point (a car seat).</summary>
        public void HipsAt(Vector3 point) { Root.transform.position += point - Bone(HumanBodyBones.Hips).position; }

        public void Ground()
        {
            if (float.IsNaN(Floor)) return;
            float low = float.MaxValue;
            foreach (var b in new[] { HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot, HumanBodyBones.LeftToes, HumanBodyBones.RightToes })
            {
                var t = Bone(b);
                if (t != null) low = Mathf.Min(low, t.position.y);
            }
            var p = Root.transform.position;
            p.y += Floor + 0.045f - low;
            Root.transform.position = p;
        }

        Transform seen; readonly List<(bool left, Vector3 target, Vector3 pole, Quaternion? rot)> reach = new List<(bool, Vector3, Vector3, Quaternion?)>();
        Vector3? looking; float lookWeight; Vector3 faceAxis = Vector3.forward;

        /// <summary>Puts a hand on a point this frame (two-bone reach from the pose), bending the elbow towards the pole. Applied after the pose is written.</summary>
        public void Reach(bool left, Vector3 target, Vector3 pole, Quaternion? handRotation = null) => reach.Add((left, target, pole, handRotation));
        /// <summary>Turns the head towards a point this frame.</summary>
        public void Look(Vector3 at, float weight = 1) { looking = at; lookWeight = weight; }

        // The face: only what the model's own rig has. A model with no jaw bone keeps its mouth still; nothing is faked.
        Vector3 earAxis = Vector3.right, upAxis = Vector3.up, chestAxis = Vector3.forward, chestUp = Vector3.up; Quaternion restLeft = Quaternion.identity, restRight = Quaternion.identity;
        Transform jaw; readonly List<Transform> eyes = new List<Transform>(), lids = new List<Transform>(), brows = new List<Transform>(), corners = new List<Transform>();
        float open, brow, smile, kick; public float Clock;
        /// <summary>Degrees to close a jaw that the model was built with slightly open.</summary>
        public float JawRest;
        bool hasSite; Vector4 site; readonly List<Renderer> faceMeshes = new List<Renderer>(); MaterialPropertyBlock block;

        void FindFace()
        {
            var seen = new List<string>();
            foreach (var t in Root.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name.ToLowerInvariant();
                bool end = n.Contains("end");
                if (n.Contains("jaw") && !end && jaw == null && t.GetComponent<Renderer>() == null) jaw = t;
                else if ((n == "l_eye" || n == "r_eye" || n.StartsWith("eye_") || n.StartsWith("eyeball") || n.EndsWith("_eye") || n == "l_ball" || n == "r_ball")) eyes.Add(t);
                else if (n.Contains("eyelid") && !end) lids.Add(t);
                else if (n.Contains("eyebrow") && !end) brows.Add(t);
                else if (n.StartsWith("mouth_")) corners.Add(t);
                if (n.Contains("jaw") || n.Contains("eye") || n.Contains("lip") || n.Contains("mouth") || n.Contains("brow") || n.Contains("teeth") || n.Contains("tongue")) seen.Add(t.name);
            }
            if (jaw == null && Bone(HumanBodyBones.Head) != null)
            {
                var head = Bone(HumanBodyBones.Head);
                var from = Matrix4x4.TRS(head.position, Quaternion.LookRotation(Root.transform.forward, Root.transform.up), Vector3.one).inverse;
                bool any = false; float line = 0, front = 0;
                foreach (var r in Root.GetComponentsInChildren<Renderer>(true))
                {
                    string n = r.name.ToLowerInvariant();
                    if (n.Contains("teeth_upper") || n.Contains("mouth")) { var b = r.bounds; var c = from.MultiplyPoint3x4(b.center); var size = b.size; float y = n.Contains("teeth") ? c.y - size.y / 2 : c.y; float z = c.z + Mathf.Max(size.z, size.x) * .5f; if (!any || z > front) { line = y; front = z; } any = true; }
                }
                if (any) { hasSite = true; site = new Vector4(line, front + .01f); faceMeshes.AddRange(Root.GetComponentsInChildren<Renderer>(true)); block = new MaterialPropertyBlock(); }
                Debug.Log($"[Trailer] mouth site {Root.name}: found={any} line={line:0.000} front={front:0.000}");
            }
            foreach (var t in eyes.Concat(lids).Concat(brows).Concat(corners).Concat(jaw != null ? new[] { jaw } : new Transform[0])) faceRest.Add((t, t.localPosition, t.localRotation));
            var shapes = new List<string>();
            foreach (var m in Root.GetComponentsInChildren<SkinnedMeshRenderer>(true)) if (m.sharedMesh != null) for (int i = 0; i < m.sharedMesh.blendShapeCount && shapes.Count < 70; i++) shapes.Add(m.sharedMesh.GetBlendShapeName(i));
            Debug.Log($"[Trailer] face {Root.name}: jaw={(jaw != null ? jaw.name : "none")} eyes={eyes.Count} lids={lids.Count} brows={brows.Count} corners={corners.Count} bones=[{string.Join(",", seen)}] shapes[{shapes.Count}]=[{string.Join(",", shapes)}]");
        }

        /// <summary>How far the mouth is open this frame (0 to 1).</summary>
        public void Talk(float amount) => open = amount;
        /// <summary>This frame's expression, with whatever the rig has: brows (+ raised, - knitted), mouth corners (+ up, - down).</summary>
        public void Feel(float browAmount, float smileAmount = 0) { brow = browAmount; smile = smileAmount; }
        /// <summary>Recoil this frame: 1 is the instant of the shot.</summary>
        public void Kick(float amount) => kick = amount;

        readonly List<(Transform t, Vector3 p, Quaternion r)> faceRest = new List<(Transform, Vector3, Quaternion)>();

        void Face(Vector3? gaze)
        {
            foreach (var f in faceRest) f.t.SetLocalPositionAndRotation(f.p, f.r);   // the clips do not write these bones: without this each frame's offset adds to the last
            var head = Bone(HumanBodyBones.Head); if (head == null) return;
            var ear = head.rotation * earAxis; var up = head.rotation * upAxis;
            if (jaw != null) jaw.rotation = Quaternion.AngleAxis(JawRest + open * 13, ear) * jaw.rotation;
            if (hasSite)
            {
                var to = Matrix4x4.TRS(head.position, Quaternion.LookRotation(head.rotation * faceAxis, up), Vector3.one);
                foreach (var r in faceMeshes)
                {
                    if (r == null) continue;
                    r.GetPropertyBlock(block);
                    block.SetMatrix("_HeadFrom", to.inverse); block.SetMatrix("_HeadTo", to); block.SetFloat("_JawOpen", open * .24f); block.SetVector("_JawSite", site);
                    r.SetPropertyBlock(block);
                }
            }
            float phase = Mathf.Repeat(Clock + Root.name.Length * .37f, 3.3f), shut = phase < .16f ? Mathf.Sin(phase / .16f * Mathf.PI) : 0;
            foreach (var l in lids) l.position -= up * (shut * .009f - brow * .0015f);
            foreach (var b in brows) b.position += up * brow * .005f;
            foreach (var c in corners) c.position += up * smile * .003f - (c.position - head.position).normalized * open * .002f;
            if (gaze.HasValue && eyes.Count > 0)
            {
                var ahead = head.rotation * faceAxis;
                var turn = Quaternion.RotateTowards(Quaternion.identity, Quaternion.FromToRotation(ahead, (gaze.Value - head.position).normalized), 16);
                foreach (var e in eyes) e.rotation = turn * e.rotation;
            }
            open = brow = smile = 0;
        }

        static readonly HumanBodyBones[] RightFingers = { HumanBodyBones.RightIndexProximal, HumanBodyBones.RightIndexIntermediate, HumanBodyBones.RightIndexDistal, HumanBodyBones.RightMiddleProximal, HumanBodyBones.RightMiddleIntermediate, HumanBodyBones.RightMiddleDistal, HumanBodyBones.RightRingProximal, HumanBodyBones.RightRingIntermediate, HumanBodyBones.RightRingDistal, HumanBodyBones.RightLittleProximal, HumanBodyBones.RightLittleIntermediate, HumanBodyBones.RightLittleDistal };
        static readonly HumanBodyBones[] LeftFingers = { HumanBodyBones.LeftIndexProximal, HumanBodyBones.LeftIndexIntermediate, HumanBodyBones.LeftIndexDistal, HumanBodyBones.LeftMiddleProximal, HumanBodyBones.LeftMiddleIntermediate, HumanBodyBones.LeftMiddleDistal, HumanBodyBones.LeftRingProximal, HumanBodyBones.LeftRingIntermediate, HumanBodyBones.LeftRingDistal, HumanBodyBones.LeftLittleProximal, HumanBodyBones.LeftLittleIntermediate, HumanBodyBones.LeftLittleDistal };
        readonly List<(bool left, float a, float b, float c, float index)> curls = new List<(bool, float, float, float, float)>();

        /// <summary>Bends this hand's fingers this frame, whatever pose the clip left them in. Degrees at the three joints; index, if not negative, is how far the first finger closes (a trigger finger).</summary>
        public void Curl(bool left, float a, float b, float c, float index = -1) => curls.Add((left, a, b, c, index));

        void CurlNow(bool left, float a, float b, float c, float index)
        {
            var bones = left ? LeftFingers : RightFingers;
            var hand = Bone(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            Transform first = Bone(bones[0]), mid = Bone(bones[3]), last = Bone(bones[9]);
            if (hand == null || first == null || mid == null || last == null) return;
            var along = (mid.position - hand.position).normalized;
            var side = Vector3.ProjectOnPlane(first.position - last.position, along).normalized;
            var axis = left ? side : -side;
            for (int f = 0; f < 4; f++)
            {
                float k = f == 0 && index >= 0 ? index / a : 1;
                var dir = along;
                float[] bend = { a * k, b * k, c * k };
                for (int j = 0; j < 3; j++)
                {
                    var bone = Bone(bones[f * 3 + j]); if (bone == null) break;
                    dir = Quaternion.AngleAxis(bend[j], axis) * dir;
                    Transform next = j < 2 ? Bone(bones[f * 3 + j + 1]) : bone.childCount > 0 ? bone.GetChild(0) : null;
                    if (next != null && (next.position - bone.position).sqrMagnitude > 1e-8f) bone.rotation = Quaternion.FromToRotation(next.position - bone.position, dir) * bone.rotation;
                }
            }
        }

        Vector3? aimAt; float raised = 1;
        /// <summary>Brings the gun up at a point this frame. raise 1 is on target, 0 is low ready.</summary>
        public void Aim(Vector3 target, float raise = 1) { aimAt = target; raised = raise; }

        /// <summary>A gun's geometry: all in metres from the middle of the model, along its barrel and up through its top.</summary>
        public sealed class Gun { public GripMap Map; public GameObject Prop; public Quaternion Fix; public Vector3 Centre; public bool Pistol; public float GripF, GripU, SupF, SupU, ButtF, ButtU, BoreU, Half; }
        readonly List<Gun> guns = new List<Gun>();
        /// <summary>This frame only: where the support hand goes instead of the gun (grip, forward, up, right).</summary>
        public Func<Vector3, Vector3, Vector3, Vector3, Vector3> LeftAt;

        /// <summary>Gives this person a gun. modelForward and modelUp are the model's muzzle and top directions.</summary>
        public Gun Arm(GameObject prop, float length, bool pistol, Vector3 modelForward, Vector3 modelUp, float gripF, float gripU, float supF, float supU, float buttF, float buttU, float boreU)
        {
            bool was = prop.activeSelf; prop.SetActive(true);
            var keep = prop.transform.rotation; prop.transform.rotation = Quaternion.identity;
            bool first = true; var b = new Bounds();
            foreach (var r in prop.GetComponentsInChildren<Renderer>(false)) { if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds); }
            float k = length / Mathf.Max(b.size.x, b.size.y, b.size.z);
            var centre = (b.center - prop.transform.position) * k;
            prop.transform.localScale *= k;
            prop.transform.rotation = keep; prop.SetActive(was);
            var g = new Gun { Prop = prop, Pistol = pistol, Centre = centre, Fix = Quaternion.Inverse(Quaternion.LookRotation(modelForward, modelUp)), GripF = gripF, GripU = gripU, SupF = supF, SupU = supU, ButtF = buttF, ButtU = buttU, BoreU = boreU, Half = length / 2 };
            guns.Add(g);
            return g;
        }

        /// <summary>The rotation that puts this hand's fingers along one direction with its index side towards another, whatever way this model's bone is built.</summary>
        public Quaternion Palm(bool left, Vector3 along, Vector3 indexSide)
        {
            var hand = Bone(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            var bones = left ? LeftFingers : RightFingers;
            Transform first = Bone(bones[0]), mid = Bone(bones[3]), last = Bone(bones[9]);
            var f = (mid.position - hand.position).normalized;
            var localF = Quaternion.Inverse(hand.rotation) * f;
            var localS = Quaternion.Inverse(hand.rotation) * Vector3.ProjectOnPlane(first.position - last.position, f).normalized;
            return Quaternion.LookRotation(along.normalized, Vector3.ProjectOnPlane(indexSide, along).normalized) * Quaternion.Inverse(Quaternion.LookRotation(localF, localS));
        }

        void Thumb(bool left, Vector3 point)
        {
            var chain = left ? new[] { HumanBodyBones.LeftThumbProximal, HumanBodyBones.LeftThumbIntermediate, HumanBodyBones.LeftThumbDistal } : new[] { HumanBodyBones.RightThumbProximal, HumanBodyBones.RightThumbIntermediate, HumanBodyBones.RightThumbDistal };
            for (int i = 0; i < 2; i++)
            {
                Transform b = Bone(chain[i]), n = Bone(chain[i + 1]);
                if (b != null && n != null) b.rotation = Quaternion.FromToRotation(n.position - b.position, point - b.position) * b.rotation;
            }
        }

        /// <summary>Shares the wrist's turn with the forearm, as a real arm does, so the wrist does not twist like a wrung cloth.</summary>
        void ShareTwist(bool left)
        {
            var lower = Bone(left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm);
            var hand = Bone(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            var rest = left ? restLeft : restRight;
            var keep = hand.rotation;
            var error = keep * Quaternion.Inverse(lower.rotation * rest);
            var axis = (hand.position - lower.position).normalized;
            var v = new Vector3(error.x, error.y, error.z);
            var along = axis * Vector3.Dot(v, axis);
            var twist = new Quaternion(along.x, along.y, along.z, error.w);
            float m = Mathf.Sqrt(twist.x * twist.x + twist.y * twist.y + twist.z * twist.z + twist.w * twist.w);
            if (m < 1e-5f) return;
            twist = new Quaternion(twist.x / m, twist.y / m, twist.z / m, twist.w / m);
            lower.rotation = Quaternion.Slerp(Quaternion.identity, twist, .55f) * lower.rotation;
            hand.rotation = keep;
        }

        float cheek; Vector3 cheekAxis;

        /// <summary>The gun is placed first, as a shooter would hold it; then the body, wrists, palms, fingers and thumbs are put on it.</summary>
        void Stance(Gun g, Vector3 target, float raise)
        {
            var chest = Bone(HumanBodyBones.Chest); var spine = Bone(HumanBodyBones.Spine); var head = Bone(HumanBodyBones.Head);
            var flat = Vector3.ProjectOnPlane(target - chest.position, Vector3.up).normalized;
            var want = Quaternion.AngleAxis(g.Pistol ? 0 : 26 * raise, Vector3.up) * flat;
            var now = Vector3.ProjectOnPlane(chest.rotation * chestAxis, Vector3.up).normalized;
            var yaw = Quaternion.FromToRotation(now, want);
            spine.rotation = Quaternion.Slerp(Quaternion.identity, yaw, .45f) * spine.rotation;
            chest.rotation = Quaternion.Slerp(Quaternion.identity, yaw, .55f) * chest.rotation;
            var side = Vector3.Cross(Vector3.up, want).normalized;
            // a shooter's back: a little forward of upright, whatever the clip was doing
            float tilt = Vector3.SignedAngle(Vector3.up, Vector3.ProjectOnPlane(chest.rotation * chestUp, side), side);
            float lean = Mathf.Clamp(tilt, 4, g.Pistol ? 10 : 13) - tilt - kick * 1.8f;
            spine.rotation = Quaternion.AngleAxis(lean * .5f, side) * spine.rotation;
            chest.rotation = Quaternion.AngleAxis(lean * .5f, side) * chest.rotation;

            var rs = Bone(HumanBodyBones.RightUpperArm); var ls = Bone(HumanBodyBones.LeftUpperArm);
            var eye = head.position + head.rotation * upAxis * .09f + head.rotation * faceAxis * .08f;
            var aim = (target - eye).normalized;
            var right = Vector3.Cross(Vector3.up, aim).normalized;
            var f = Vector3.Slerp(Quaternion.AngleAxis(40, right) * aim, aim, raise);
            f = Quaternion.AngleAxis(-kick * (g.Pistol ? 7 : 2.8f), right) * f;
            var r = Vector3.Cross(Vector3.up, f).normalized; var u = Vector3.Cross(f, r).normalized;

            var m = g.Map;
            var rl = Bone(HumanBodyBones.RightLowerArm); var rh = Bone(HumanBodyBones.RightHand);
            float reach = (Vector3.Distance(rs.position, rl.position) + Vector3.Distance(rl.position, rh.position)) * .95f;
            var mid = Bone(RightFingers[3]); float palm = Vector3.Distance(rh.position, mid.position);
            var fireAlong = (r * m.FireAlong.x + u * m.FireAlong.y + f * m.FireAlong.z).normalized;
            Vector3 grip;
            if (g.Pistol)
            {
                var up = eye + f * .5f - u * (.035f + g.BoreU - g.GripU);
                // both arms must truly reach: bring the gun in until they do
                var qq = Quaternion.LookRotation(f, u);
                for (int i = 0; i < 3; i++)
                {
                    var c0 = up - f * g.GripF - u * g.GripU;
                    float o = Mathf.Max(Vector3.Distance(WristFor(false, c0 + qq * m.FireKnuckle, qq * m.FireAlong, qq * m.FireIndex), rs.position) - ArmLength(false) * .97f, Vector3.Distance(WristFor(true, c0 + qq * m.SupKnuckle, qq * m.SupAlong, qq * m.SupIndex), ls.position) - ArmLength(true) * .97f);
                    if (o > 0) up -= f * (o + .002f); else break;
                }
                grip = Vector3.Lerp(chest.position + want * .28f + Vector3.down * .16f + side * .03f, up, raise) - f * kick * .035f;
            }
            else
            {
                var pocket = rs.position + want * .05f + u * .07f - r * .01f;
                grip = pocket + f * (g.GripF - g.ButtF) + u * (g.GripU - g.ButtU) - f * kick * .02f;
            }
            var centre = grip - f * g.GripF - u * g.GripU;
            var rot = Quaternion.LookRotation(f, u) * g.Fix;
            g.Prop.transform.SetPositionAndRotation(centre - rot * g.Centre, rot);
            tips[g.Prop] = (centre + f * g.Half + u * g.BoreU, f);

            // Exact positions from the grip map, with no blend from whatever the clip was doing.
            var q = Quaternion.LookRotation(f, u);
            Vector3 G(Vector3 l) => centre + q * l;
            Func<Vector3, float> body = w => m.Distance(Quaternion.Inverse(q) * (w - centre));
            bool hot = raise > .99f;
            var pole = g.Pistol ? rs.position + Vector3.down * .6f + r * .25f - f * .05f : rs.position + Vector3.down * .7f + r * .3f - f * .25f;
            float frF = FingerRadius(false);
            var fireK = m.FireKnuckle; fireK.x = Mathf.Max(fireK.x, m.Body[0].Half.x + frF + .002f);
            mapFire = Quaternion.Inverse(q) * (PlaceHand(false, G(fireK), q * m.FireAlong, q * m.FireIndex, pole, body, frF) - centre);
            for (int i = 1; i < 4; i++) Wrap(false, i, body, frF);
            if (hot) OnTrigger(false, G(m.Trigger)); else Straight(false, G(m.IndexRest));
            ThumbTo(false, G(m.FireThumbVia), G(m.FireThumbTip));
            ShareTwist(false);

            mappedGun = g; mapQ = q; mapC = centre; mapHot = hot; mapSupport = LeftAt == null;
            if (LeftAt != null)
            {
                Solve(true, LeftAt(grip, f, u, r), ls.position - r * .75f - f * .35f + Vector3.down * .15f, null);
                if (!guns.Exists(x => x.Prop != g.Prop && x.Prop.activeInHierarchy)) CurlNow(true, 24, 30, 18, -1);
            }
            else
            {
                float frS = FingerRadius(true);
                Func<Vector3, float> both = w => body(w) - (g.Pistol ? frF * 2 : 0);   // on a pistol the firing fingers lie on the grip: the support fingers close over that layer
                var supK = m.SupKnuckle; if (g.Pistol) supK.x = Mathf.Min(supK.x, -(m.Body[0].Half.x + frF * 2 + frS + .002f));
                mapSup = Quaternion.Inverse(q) * (PlaceHand(true, G(supK), q * m.SupAlong, q * m.SupIndex, g.Pistol ? ls.position + Vector3.down * .6f - r * .3f : ls.position + Vector3.down * .9f + r * .12f + f * .1f, both, frS) - centre);
                for (int i = 0; i < 4; i++) Wrap(true, i, both, frS, body);
                ThumbTo(true, G(m.SupThumbVia), G(m.SupThumbTip));
                ShareTwist(true);
            }
            cheek = g.Pistol ? 0 : raise; cheekAxis = f;
            if (!looking.HasValue) { looking = target; lookWeight = 1; }
            kick = 0; LeftAt = null;
        }

        readonly Dictionary<GameObject, (Vector3 at, Vector3 dir)> tips = new Dictionary<GameObject, (Vector3, Vector3)>();
        /// <summary>Where the far end of a carried prop is now, and which way it points (read after this frame's Finish).</summary>
        public (Vector3 at, Vector3 dir) Tip(GameObject prop) => tips.TryGetValue(prop, out var t) ? t : (prop.transform.position, Root.transform.forward);

        public void Finish()
        {
            Gun carried = Root.activeInHierarchy ? guns.Find(x => x.Prop.activeInHierarchy) : null;
            foreach (var g in guns) foreach (var pr in g.Prop.GetComponentsInChildren<Renderer>(true)) pr.enabled = Root.activeInHierarchy;
            foreach (var k in knives) foreach (var pr in k.Prop.GetComponentsInChildren<Renderer>(true)) pr.enabled = Root.activeInHierarchy;
            if (carried != null) Stance(carried, aimAt ?? (Root.transform.position + Root.transform.forward * 8 + Vector3.up * 1.4f), aimAt.HasValue ? raised : 0);
            aimAt = null; kick = 0; LeftAt = null;
            if (carried == null) mappedGun = null;
            heldKnife = null;
            StabNow();
            foreach (var r in reach) Solve(r.left, r.target, r.pole, r.rot);
            reach.Clear();
            foreach (var c in curls) CurlNow(c.left, c.a, c.b, c.c, c.index);
            curls.Clear();
            var gaze = looking;
            if (looking.HasValue)
            {
                var head = Bone(HumanBodyBones.Head); var neck = Bone(HumanBodyBones.Neck);
                var turn = Quaternion.RotateTowards(Quaternion.identity, Quaternion.FromToRotation(head.rotation * faceAxis, (looking.Value - head.position).normalized), 55 * lookWeight);
                if (neck != null) neck.rotation = Quaternion.Slerp(Quaternion.identity, turn, 0.3f) * neck.rotation;
                head.rotation = Quaternion.Slerp(Quaternion.identity, turn, neck != null ? 0.7f : 1) * head.rotation;
                looking = null;
            }
            if (cheek > .01f)
            {
                var hd = Bone(HumanBodyBones.Head);
                hd.rotation = Quaternion.AngleAxis(-9 * cheek, cheekAxis) * hd.rotation;
                cheek = 0;
            }
            Face(gaze);
            foreach (var g in grips)
            {
                foreach (var pr in g.prop.GetComponentsInChildren<Renderer>(true)) pr.enabled = Root.activeInHierarchy;
                if (!g.prop.activeInHierarchy || !Root.activeInHierarchy) continue;
                var hand = Bone(g.left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
                var other = Bone(g.left ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
                var fore = Bone(g.left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm);
                var along = (hand.position - fore.position).normalized;
                if (g.how == Hold2.TwoHands && Vector3.Distance(hand.position, other.position) > .14f) { along = (other.position - hand.position).normalized; CurlNow(!g.left, 42, 55, 35, -1); }
                if (g.how == Hold2.Across)
                {
                    var i = Bone(g.left ? HumanBodyBones.LeftIndexProximal : HumanBodyBones.RightIndexProximal); var p = Bone(g.left ? HumanBodyBones.LeftLittleProximal : HumanBodyBones.RightLittleProximal);
                    if (i != null && p != null) along = (i.position - p.position).normalized;
                }
                if (g.how == Hold2.Flat) CurlNow(g.left, 30, 35, 20, -1); else CurlNow(g.left, 62, 78, 52, g.how == Hold2.Across ? -1 : 28);
                var up = Vector3.ProjectOnPlane(Vector3.up, along).normalized;
                var rot = Quaternion.LookRotation(along, up) * g.fix;
                var middle = hand.position + along * g.forward + up * g.lift;
                g.prop.transform.SetPositionAndRotation(middle - rot * g.centre, rot);
                tips[g.prop] = (middle + along * g.half, along);
            }
        }

        public enum Hold2 { OneHand, TwoHands, Across, Flat }
        readonly List<(GameObject prop, bool left, Quaternion fix, Vector3 centre, float forward, float lift, Hold2 how, float half)> grips = new List<(GameObject, bool, Quaternion, Vector3, float, float, Hold2, float)>();

        /// <summary>A prop carried in a hand. A pistol runs along the forearm, a long gun from one hand to the other, a blade across the fist; the fingers close on it and it keeps its own size.</summary>
        public void Grip(GameObject prop, float length, Hold2 how = Hold2.OneHand, bool left = false, Vector3 turn = default, float forward = .08f, float lift = .02f)
        {
            bool was = prop.activeSelf; prop.SetActive(true);
            var keep = prop.transform.rotation; prop.transform.rotation = Quaternion.identity;
            bool first = true; var b = new Bounds();
            foreach (var r in prop.GetComponentsInChildren<Renderer>(true)) { if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds); }
            var size = b.size; var axis = size.z >= size.x && size.z >= size.y ? Vector3.forward : size.x >= size.y ? Vector3.right : Vector3.up;   // the long axis
            var fix = Quaternion.Euler(turn) * Quaternion.FromToRotation(axis, Vector3.forward);
            float longest = Mathf.Max(size.x, size.y, size.z), k = length / longest;
            var centre = (b.center - prop.transform.position) * k;
            prop.transform.localScale *= k;   // real-world length in metres
            prop.transform.rotation = keep; prop.SetActive(was);
            grips.Add((prop, left, fix, centre, forward, lift, how, length / 2));
        }

        void Solve(bool left, Vector3 target, Vector3 pole, Quaternion? rot)
        {
            var a = Bone(left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm);
            var b = Bone(left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm);
            var c = Bone(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            float ab = Vector3.Distance(a.position, b.position), bc = Vector3.Distance(b.position, c.position);
            var to = target - a.position; float d = Mathf.Clamp(to.magnitude, 0.05f, (ab + bc) * 0.999f);
            float cosA = Mathf.Clamp((ab * ab + d * d - bc * bc) / (2 * ab * d), -1, 1), sinA = Mathf.Sqrt(1 - cosA * cosA);
            var dir = to.normalized;
            var side = Vector3.ProjectOnPlane(pole - a.position, dir).normalized;
            if (side.sqrMagnitude < 1e-5f) side = Vector3.ProjectOnPlane(Vector3.down, dir).normalized;
            var elbow = a.position + dir * (ab * cosA) + side * (ab * sinA);
            a.rotation = Quaternion.FromToRotation(b.position - a.position, elbow - a.position) * a.rotation;
            b.rotation = Quaternion.FromToRotation(c.position - b.position, a.position + dir * d - b.position) * b.rotation;
            if (rot.HasValue) c.rotation = rot.Value;
        }

        public void Destroy() { if (graph.IsValid()) graph.Destroy(); UnityEngine.Object.Destroy(Root); }
    }

    /// <summary>One shot: the frames it covers and what to do on each (seconds since the start of the shot, and the fraction through it).</summary>
    public sealed class Shot
    {
        public string Name; public int First, Last; public Action<float, float> Do, After;
        public Shot(string name, double from, double to, Action<float, float> pose, Action<float, float> camera = null) { Name = name; First = Mathf.RoundToInt((float)from * 30); Last = Mathf.RoundToInt((float)to * 30) - 1; Do = pose; After = camera; }
    }

    public sealed class Stage
    {
        public Camera Cam;
        public Volume Post;
        public VolumeProfile Profile;
        public Transform Root;
        public readonly List<Actor> Actors = new List<Actor>();

        [Serializable] class Line { public float start, end; public string who, text; }
        [Serializable] class Lines { public Line[] items; }
        Lines lines; readonly Dictionary<string, Actor> voices = new Dictionary<string, Actor>();
        public void Voice(string who, Actor a) => voices[who] = a;

        static float Shape(char c)
        {
            switch (char.ToLowerInvariant(c))
            {
                case 'a': return 1; case 'o': return .85f; case 'e': return .62f; case 'u': return .5f; case 'i': case 'y': return .42f;
                case 'm': case 'b': case 'p': return 0; case 'f': case 'v': return .14f;
                case ' ': return .05f; case '.': case ',': case '?': case '!': case '\u2026': return 0;
                default: return .28f;
            }
        }

        /// <summary>Moves the jaw of whoever has a line now, from the written line. There are no recorded voices yet, so this follows the words' vowels, not a performance.</summary>
        public void Speak(float time)
        {
            if (lines == null) lines = JsonUtility.FromJson<Lines>("{\"items\":" + System.IO.File.ReadAllText("Production/Trailer_2026/Editing/cinematic_v1_subtitles.json") + "}");
            foreach (var a in Actors) a.Clock = time;
            foreach (var l in lines.items)
            {
                if (time < l.start + .05f || time > l.end - .08f || !voices.TryGetValue(l.who, out var who)) continue;
                float u = Mathf.InverseLerp(l.start + .05f, l.end - .08f, time) * (l.text.Length - 1), sum = 0, weights = 0;
                for (int i = Mathf.Max(0, Mathf.FloorToInt(u) - 2); i <= Mathf.Min(l.text.Length - 1, Mathf.CeilToInt(u) + 2); i++) { float w = Mathf.Max(0, 1 - Mathf.Abs(i - u) / 2.6f); sum += Shape(l.text[i]) * w; weights += w; }
                who.Talk(sum / weights);
            }
        }
        readonly List<UnityEngine.Object> made = new List<UnityEngine.Object>();

        ColorAdjustments color; Vignette vignette; Bloom bloom; DepthOfField dof; Tonemapping tone; LiftGammaGain lgg; FilmGrain grain;

        public Stage(string name)
        {
            Root = new GameObject(name).transform;
        }

        public void BuildCamera(Camera use = null)
        {
            Cam = use != null ? use : new GameObject("Cam").AddComponent<Camera>();
            if (use == null) Cam.transform.SetParent(Root);
            var data = Cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            data.dithering = true;
            data.renderShadows = true;
            Cam.allowHDR = true;
            Cam.nearClipPlane = 0.02f;
            Cam.farClipPlane = 3000;
            var go = new GameObject("Post"); go.transform.SetParent(Root);
            Post = go.AddComponent<Volume>();
            Post.isGlobal = true; Post.priority = 50;
            Profile = ScriptableObject.CreateInstance<VolumeProfile>();
            Post.sharedProfile = Profile;
            tone = Add<Tonemapping>(); tone.mode.Override(TonemappingMode.ACES);
            color = Add<ColorAdjustments>();
            vignette = Add<Vignette>(); vignette.intensity.Override(0.28f); vignette.smoothness.Override(0.42f);
            bloom = Add<Bloom>(); bloom.intensity.Override(0.5f); bloom.threshold.Override(1.1f); bloom.scatter.Override(0.6f);
            dof = Add<DepthOfField>(); dof.mode.Override(DepthOfFieldMode.Bokeh);
            lgg = Add<LiftGammaGain>();
            grain = Add<FilmGrain>(); grain.type.Override(FilmGrainLookup.Medium1); grain.intensity.Override(0.16f); grain.response.Override(0.8f);
            Grade(0, 0, 1, Color.white);
        }

        T Add<T>() where T : VolumeComponent { var c = Profile.Add<T>(true); return c; }

        /// <summary>Exposure in stops, contrast (-100..100), saturation (-100..100), and a white-balance tint.</summary>
        public void Grade(float exposure, float contrast, float saturation, Color filter)
        {
            color.postExposure.Override(exposure);
            color.contrast.Override(contrast);
            color.saturation.Override(saturation);
            color.colorFilter.Override(filter);
        }
        public void Gain(float r, float g, float b, float lift = 0)
        {
            lgg.gain.Override(new Vector4(r, g, b, 0));
            lgg.lift.Override(new Vector4(0, 0, 0, lift));
        }
        public void Focus(float metres, float mm = 50, float stop = 2.8f)
        {
            if (metres <= 0) { dof.active = false; return; }
            dof.active = true;
            dof.focusDistance.Override(metres); dof.focalLength.Override(mm); dof.aperture.Override(stop);
        }
        public void BloomAt(float intensity, float threshold) { bloom.intensity.Override(intensity); bloom.threshold.Override(threshold); }
        public void VignetteAt(float i) => vignette.intensity.Override(i);

        /// <summary>A camera move between two places, each pointing somewhere. Handheld adds a small slow drift in metres.</summary>
        public void CamMove(Vector3 from, Vector3 lookFrom, Vector3 to, Vector3 lookTo, float u, float fov0 = 35, float fov1 = -1, float handheld = 0, float time = 0, bool easy = true)
        {
            float k = easy ? Mathf.SmoothStep(0, 1, u) : u;
            var p = Vector3.Lerp(from, to, k);
            var l = Vector3.Lerp(lookFrom, lookTo, k);
            if (handheld > 0)
            {
                p += new Vector3(Noise(time, 11.3f), Noise(time, 47.1f), Noise(time, 83.7f)) * handheld;
                l += new Vector3(Noise(time, 7.9f), Noise(time, 31.5f), Noise(time, 59.2f)) * handheld * 0.7f;
            }
            Cam.transform.position = p;
            Cam.transform.rotation = Quaternion.LookRotation(l - p, Vector3.up);
            Cam.fieldOfView = Mathf.Lerp(fov0, fov1 < 0 ? fov0 : fov1, k);
        }

        static float Noise(float t, float seed) => (Mathf.PerlinNoise(t * 1.7f + seed, seed * 0.5f) - 0.5f) * 2 + (Mathf.PerlinNoise(t * 3.9f + seed * 2, seed) - 0.5f) * 0.6f;

        public GameObject Model(string path, Vector3 at, float heading, float scale = 1, Transform under = null)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Trailer/Models/" + path);
            if (asset == null) throw new InvalidOperationException("no model " + path);
            var g = UnityEngine.Object.Instantiate(asset, under != null ? under : Root);
            g.transform.position = at + asset.transform.position; // a glTF root carries its own offset: keep it
            g.transform.rotation = Quaternion.Euler(0, heading, 0) * asset.transform.rotation;
            g.transform.localScale = asset.transform.localScale * scale;
            return g;
        }


        /// <summary>The model files often lose the link to their pictures: each material takes the maps named after it, and every material ends up on URP Lit.</summary>
        public void Dress(GameObject root, string path)
        {
            string folder = "Assets/Trailer/Models/" + path.Split('/')[0] + "/textures";
            var files = AssetDatabase.IsValidFolder(folder) ? AssetDatabase.FindAssets("t:Texture2D", new[] { folder }).Select(AssetDatabase.GUIDToAssetPath).ToArray() : new string[0];
            Texture2D Find(string key, params string[] kinds)
            {
                foreach (var f in files)
                {
                    string n = Path.GetFileNameWithoutExtension(f).ToLowerInvariant();
                    if (n.Contains(key) && kinds.Any(n.Contains)) return AssetDatabase.LoadAssetAtPath<Texture2D>(f);
                }
                return null;
            }
            var done = new Dictionary<Material, Material>();
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    var o = mats[i];
                    if (o == null) continue;
                    if (!done.TryGetValue(o, out var m))
                    {
                        bool glTF = o.shader.name.StartsWith("Shader Graphs/glTF", StringComparison.Ordinal);
                        string key = o.name.ToLowerInvariant();
                        int dot = key.IndexOf('.'); if (dot > 0) key = key.Substring(0, dot);
                        key = key.Replace(" ", "_");
                        var baseMap = Find(key, "basecolor", "base_color", "albedo", "color", "diffuse", "-base");
                        if (glTF) { done[o] = m = o; }
                        else
                        {
                            m = new Material(o);
                            if (m.shader != lit) { m.shader = lit; m.SetColor("_BaseColor", o.HasProperty("_Color") ? o.GetColor("_Color") : Color.white); m.SetFloat("_Smoothness", 0.3f); }
                            if (m.GetTexture("_BaseMap") == null && baseMap != null) { m.SetTexture("_BaseMap", baseMap); m.SetColor("_BaseColor", Color.white); }
                            var normal = Find(key, "normal");
                            if (normal != null && m.GetTexture("_BumpMap") == null) { m.SetTexture("_BumpMap", normal); m.EnableKeyword("_NORMALMAP"); }
                            var metal = Find(key, "metallic");
                            if (metal != null && m.GetTexture("_MetallicGlossMap") == null) { m.SetTexture("_MetallicGlossMap", metal); m.EnableKeyword("_METALLICSPECGLOSSMAP"); m.SetFloat("_Smoothness", 0.45f); }
                            var glow = Find(key, "emissive");
                            if (glow != null) { m.SetTexture("_EmissionMap", glow); m.SetColor("_EmissionColor", Color.white); m.EnableKeyword("_EMISSION"); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack; }
                            done[o] = m;
                            made.Add(m);
                        }
                    }
                    mats[i] = m;
                }
                r.sharedMaterials = mats;
            }
        }


        /// <summary>The owner's trailer-only change: the body's paint becomes midnight blue on a copy of its map, kept in memory; the model and the file are not touched.</summary>
        public void PaintMidnight(GameObject car, string material)
        {
            foreach (var r in car.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null || !mats[i].name.StartsWith(material, StringComparison.Ordinal)) continue;
                    if (mats[i].GetTexture("_BaseMap") is Texture2D source)
                    {
                        if (!painted.TryGetValue(source, out var copy))
                        {
                            var rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                            Graphics.Blit(source, rt);
                            var was = RenderTexture.active; RenderTexture.active = rt;
                            copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, true, false);
                            copy.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
                            RenderTexture.active = was; RenderTexture.ReleaseTemporary(rt);
                            var px = copy.GetPixels32();
                            for (int p = 0; p < px.Length; p++)
                            {
                                var c = px[p];
                                if (c.g > c.r + 12 && c.g > c.b + 12) { float v = c.g / 90f; px[p] = new Color32((byte)Mathf.Min(255, 11 * v), (byte)Mathf.Min(255, 18 * v), (byte)Mathf.Min(255, 44 * v), c.a); }
                            }
                            copy.SetPixels32(px); copy.Apply(true);
                            painted[source] = copy; made.Add(copy);
                        }
                        var m = new Material(mats[i]); m.SetTexture("_BaseMap", copy); m.SetFloat("_Smoothness", .82f); made.Add(m); mats[i] = m;
                    }
                }
                r.sharedMaterials = mats;
            }
        }
        readonly Dictionary<Texture2D, Texture2D> painted = new Dictionary<Texture2D, Texture2D>();

        static Texture2D File(string role, string file) { var t = AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/Trailer/Models/{role}/textures/{file}"); if (t == null) throw new InvalidOperationException("missing map " + role + "/" + file); return t; }

        Material Flesh(Texture2D baseMap, Texture2D normal = null, float smooth = 0.3f, float scatter = 1)
        {
            var m = new Material(Shader.Find("Dad/TrailerPerson"));
            m.SetTexture("_BaseMap", baseMap);
            if (normal != null) m.SetTexture("_BumpMap", normal);
            m.SetFloat("_Smoothness", smooth); m.SetFloat("_Scatter", scatter);
            made.Add(m);
            return m;
        }

        static void CutToAlpha(Material m, Texture2D map, float at = 0.35f) { m.SetTexture("_OpacityMap", map); m.SetVector("_OpacityPick", new Vector4(0, 0, 0, 1)); m.SetFloat("_Cut", at); }

        /// <summary>A character's materials for close-ups: every map the model ships with actually connected, skin that passes light, hair that reads as strands. Geometry and pictures untouched.</summary>
        void Flesh(Actor a, string role)
        {
            if (role == "elara") a.JawRest = -5;
            var seen = new Dictionary<Material, Material>();
            foreach (var r in a.Root.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    var o = mats[i];
                    if (o == null) continue;
                    if (!seen.TryGetValue(o, out var m)) seen[o] = m = Dressed(o, r, role);
                    mats[i] = m;
                }
                r.sharedMaterials = mats;
            }
        }

        Material Dressed(Material o, Renderer r, string role)
        {
            string name = o.name;
            if (role == null)   // the game's own people: same maps and settings, the close-up shader
            {
                if (o.shader.name != "Dad/Person") return o;
                var m = new Material(o) { shader = Shader.Find("Dad/TrailerPerson") };
                bool hair = o.GetFloat("_Dye") > 0.5f || name.IndexOf("hair", StringComparison.OrdinalIgnoreCase) >= 0 || r.name.IndexOf("hair", StringComparison.OrdinalIgnoreCase) >= 0;
                m.SetFloat("_Hair", hair ? 1 : 0); m.SetFloat("_Scatter", 1);
                made.Add(m);
                return m;
            }
            if (role == "elara" && name.StartsWith("olga", StringComparison.Ordinal))
            {
                var m = Flesh(File(role, "olgaColor.png"), File(role, "olgaNormal.png"));
                m.SetTexture("_GlossMap", File(role, "olgaSmoothness.png")); m.SetFloat("_HasGloss", 1);
                m.SetTexture("_OccMap", File(role, "olgaAO.png")); m.SetTexture("_MetalMap", File(role, "Olgametallic.png")); m.SetFloat("_HasMaps", 1);
                m.SetVector("_HairA", new Vector4(.44f, .67f, .64f, 1f));       // the head's hair in her map
                m.SetVector("_HairB", new Vector4(.45f, .33f, .74f, .43f));     // the plait
                m.SetVector("_Face", new Vector4(.44f, .67f, .64f, 1f)); m.SetVector("_Inside", new Vector4(.2f, .86f, .455f, 1f)); m.SetFloat("_GlossScale", .62f);
                return m;
            }
            if (role == "eric")   // a RenderPeople photo scan, 8K maps as shipped
            {
                var m = Flesh(File(role, "eric_base.jpg"), File(role, "eric_normal.jpg"));
                m.SetTexture("_GlossMap", File(role, "eric_gloss.jpg")); m.SetFloat("_HasGloss", 1);
                m.SetVector("_HairA", new Vector4(.016f, .317f, .245f, .505f));
                m.SetVector("_HairB", new Vector4(.58f, .015f, .99f, .36f)); m.SetFloat("_SkinGloss", .3f); m.SetFloat("_GlossScale", .85f);
                return m;
            }
            if (role == "vance")
            {
                if (name.StartsWith("DefaultSkin", StringComparison.Ordinal))  // the model ships without its skin; this is a MakeHuman skin from the owner's pack, on MakeHuman's own layout
                {
                    var orm = File(role, "dave_roughness_orm.png");
                    var m = Flesh(File(role, "dave_base.png"), File(role, "dave_normal.png"));
                    m.SetTexture("_GlossMap", orm); m.SetVector("_GlossPick", new Vector4(0, 1, 0, 0)); m.SetFloat("_GlossInvert", 1); m.SetFloat("_HasGloss", 1);
                    m.SetTexture("_OrmMap", orm); m.SetFloat("_HasOcc", 1);
                    return m;
                }
                if (name.StartsWith("short03", StringComparison.Ordinal)) { var t = File(role, "short03_diffuse.png"); var m = Flesh(t, null, .16f, 0); CutToAlpha(m, t); m.SetFloat("_Hair", 1); return m; }
                if (name.StartsWith("eyebrow", StringComparison.Ordinal)) { var t = File(role, "eyebrow001.png"); var m = Flesh(t, null, .16f, 0); CutToAlpha(m, t, .2f); m.SetFloat("_Hair", 1); return m; }
                if (name.StartsWith("Eyeblue", StringComparison.Ordinal)) return Flesh(File(role, "blue_eye.png"), null, .88f, 0);
                if (name.StartsWith("male_elegantsuit", StringComparison.Ordinal)) return Flesh(File(role, "male_elegantsuit01_diffuse.png"), null, .2f, 0);
                if (name.StartsWith("teeth", StringComparison.Ordinal)) return Flesh(File(role, "teeth.png"), null, .5f, 0);
                return o;
            }
            if (role == "marcus" && o.HasProperty("baseColorTexture"))
            {
                var baseTex = o.GetTexture("baseColorTexture") as Texture2D;
                var m = new Material(Shader.Find("Dad/TrailerPerson"));
                if (baseTex != null) { m.SetTexture("_BaseMap", baseTex); m.SetTextureScale("_BaseMap", o.GetTextureScale("baseColorTexture")); m.SetTextureOffset("_BaseMap", o.GetTextureOffset("baseColorTexture")); }
                m.SetTexture("_MaskMap", Texture2D.whiteTexture); if (o.HasProperty("baseColorFactor")) m.SetColor("_TintR", o.GetColor("baseColorFactor"));
                if (o.HasProperty("normalTexture") && o.GetTexture("normalTexture") != null) m.SetTexture("_BumpMap", o.GetTexture("normalTexture"));
                var mr = o.HasProperty("metallicRoughnessTexture") ? o.GetTexture("metallicRoughnessTexture") : null;
                if (mr != null) { m.SetTexture("_OrmMap", mr); m.SetTexture("_GlossMap", mr); m.SetVector("_GlossPick", new Vector4(0, 1, 0, 0)); m.SetFloat("_GlossInvert", 1); m.SetFloat("_HasGloss", 1); }
                else m.SetFloat("_Smoothness", o.HasProperty("roughnessFactor") ? 1 - o.GetFloat("roughnessFactor") : .3f);
                bool hair = name.StartsWith("Hair", StringComparison.OrdinalIgnoreCase);
                m.SetFloat("_Hair", hair ? 1 : 0); m.SetFloat("_Scatter", 1);
                if (hair && name.StartsWith("Hair_01", StringComparison.OrdinalIgnoreCase) && baseTex != null) CutToAlpha(m, baseTex);
                made.Add(m);
                return m;
            }
            return o;
        }

        public Actor CastPerson(string who, Vector3 at, float heading)
        {
            var p = Person.Spawn(who, at, heading);
            if (p == null) throw new InvalidOperationException("no person " + who);
            var root = p.gameObject; var body = p.Body;
            UnityEngine.Object.DestroyImmediate(p); // its own small graph would fight this one
            root.transform.SetParent(Root, true);
            var a = new Actor(root, body);
            Actors.Add(a);
            Flesh(a, null);
            return a;
        }

        /// <summary>A model not in the game's roster: rigged in its own way, given a Humanoid avatar here from a table of its bones.</summary>
        public Actor CastModel(string path, Dictionary<HumanBodyBones, string> bones, Vector3 at, float heading, float scale = 1, Action<Transform> tpose = null, float yaw = 0)
        {
            var g = Model(path, Vector3.zero, 0, 1);
            var root = new GameObject(Path.GetFileNameWithoutExtension(path));
            root.transform.SetParent(Root);
            root.transform.SetPositionAndRotation(at, Quaternion.Euler(0, heading, 0));
            root.transform.localScale = Vector3.one * scale;
            g.transform.SetParent(root.transform, false);
            g.transform.localPosition = Vector3.zero;
            g.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            var map = g.GetComponentsInChildren<Transform>(true).GroupBy(t => t.name).ToDictionary(x => x.Key, x => x.First());
            tpose?.Invoke(g.transform);
            var old = g.GetComponent<Animator>();
            if (old != null) UnityEngine.Object.DestroyImmediate(old);
            var humans = new List<HumanBone>();
            string[] names = HumanTrait.BoneName;
            foreach (var kv in bones)
            {
                if (!map.ContainsKey(kv.Value)) { Debug.LogWarning("[Trailer] no bone " + kv.Value + " on " + path); continue; }
                string human = names[(int)kv.Key];
                var h = new HumanBone { boneName = kv.Value, humanName = human };
                h.limit.useDefaultValues = true;
                humans.Add(h);
            }
            var skel = g.GetComponentsInChildren<Transform>(true).Select(t => new SkeletonBone { name = t.name, position = t.localPosition, rotation = t.localRotation, scale = t.localScale }).ToArray();
            var desc = new HumanDescription { human = humans.ToArray(), skeleton = skel, upperArmTwist = 0.5f, lowerArmTwist = 0.5f, upperLegTwist = 0.5f, lowerLegTwist = 0.5f, armStretch = 0.05f, legStretch = 0.05f, feetSpacing = 0, hasTranslationDoF = false };
            var avatar = AvatarBuilder.BuildHumanAvatar(g, desc);
            if (avatar == null || !avatar.isValid || !avatar.isHuman) throw new InvalidOperationException("avatar failed for " + path);
            made.Add(avatar);
            var anim = g.AddComponent<Animator>();
            anim.avatar = avatar;
            var a = new Actor(root, anim);
            Actors.Add(a);
            Flesh(a, path.Split('/')[0]);
            return a;
        }

        public Light Lamp(LightType kind, Vector3 at, Vector3 look, Color c, float intensity, float range = 20, float cone = 60, bool shadows = true)
        {
            var go = new GameObject("Lamp"); go.transform.SetParent(Root);
            go.transform.SetPositionAndRotation(at, Quaternion.LookRotation((look - at).sqrMagnitude < 1e-4f ? Vector3.forward : look - at));
            var l = go.AddComponent<Light>();
            l.type = kind; l.color = c; l.intensity = intensity; l.range = range; l.spotAngle = cone;
            l.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            return l;
        }

        public GameObject Card(Vector3 at, Vector3 size, Color c, float heading = 0, Material material = null)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.transform.SetParent(Root);
            g.transform.position = at; g.transform.localScale = size; g.transform.rotation = Quaternion.Euler(0, heading, 0);
            UnityEngine.Object.Destroy(g.GetComponent<Collider>());
            var r = g.GetComponent<Renderer>();
            var m = material != null ? material : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (material == null) { m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", 0.25f); }
            r.sharedMaterial = m;
            return g;
        }

        public void CityLights(Vector3 centre, float inner, float outer, int count, int seed)
        {
            var rng = new System.Random(seed);
            var mesh = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            var shared = mesh.GetComponent<MeshFilter>().sharedMesh; UnityEngine.Object.Destroy(mesh);
            for (int i = 0; i < count; i++)
            {
                double a = rng.NextDouble() * Math.PI * 2, d = inner + rng.NextDouble() * (outer - inner);
                var p = centre + new Vector3((float)(Math.Cos(a) * d), -6 - (float)rng.NextDouble() * 55, (float)(Math.Sin(a) * d));
                var g = new GameObject("c"); g.transform.SetParent(Root); g.transform.position = p; g.transform.localScale = Vector3.one * (float)(1.2 + rng.NextDouble() * 3.2);
                g.AddComponent<MeshFilter>().sharedMesh = shared;
                var r = g.AddComponent<MeshRenderer>(); r.shadowCastingMode = ShadowCastingMode.Off;
                bool warm = rng.NextDouble() < .72;
                r.sharedMaterial = Glow(warm ? new Color(1f, .68f, .33f) : new Color(.72f, .82f, 1f), (float)(1.6 + rng.NextDouble() * 4));
            }
        }

        public static Material Glow(Color c, float power)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            m.SetColor("_BaseColor", c * power);
            return m;
        }

        /// <summary>A particle system seen in a few shots: falling rain, dust in a beam, smoke, sparks.</summary>
        public ParticleSystem Particles(string name, Vector3 at, Vector3 box, float rate, float speed, float life, float size, Color c, bool stretch = false, Vector3? vel = null, float gravity = 0, int seed = 7)
        {
            var go = new GameObject(name); go.transform.SetParent(Root); go.transform.position = at;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.randomSeed = (uint)seed;
            ps.useAutoRandomSeed = false;
            var main = ps.main;
            main.startLifetime = life; main.startSpeed = speed; main.startSize = size; main.startColor = c; main.maxParticles = 20000;
            main.simulationSpace = ParticleSystemSimulationSpace.World; main.gravityModifier = gravity;
            main.playOnAwake = false;
            var emit = ps.emission; emit.rateOverTime = rate;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = box;
            if (vel.HasValue) { var v = ps.velocityOverLifetime; v.enabled = true; v.space = ParticleSystemSimulationSpace.World; v.x = vel.Value.x; v.y = vel.Value.y; v.z = vel.Value.z; }
            var r = go.GetComponent<ParticleSystemRenderer>();
            var mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            mat.SetFloat("_Surface", 1); mat.SetFloat("_Blend", 0); mat.SetColor("_BaseColor", c);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha); mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha); mat.SetInt("_ZWrite", 0);
            mat.renderQueue = 3000;
            mat.SetTexture("_BaseMap", SoftDot());
            r.sharedMaterial = mat;
            if (stretch) { r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = 0.03f; r.lengthScale = 2f; }
            return ps;
        }

        static Texture2D dot;
        public static Texture2D SoftDot()
        {
            if (dot != null) return dot;
            dot = new Texture2D(64, 64, TextureFormat.RGBA32, true);
            for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++) { float d = Vector2.Distance(new Vector2(x, y), new Vector2(31.5f, 31.5f)) / 31.5f; float a = Mathf.Clamp01(1 - d); dot.SetPixel(x, y, new Color(1, 1, 1, a * a * (3 - 2 * a))); }
            dot.Apply();
            return dot;
        }

        public void Clear()
        {
            foreach (var a in Actors) a.Destroy();
            Actors.Clear();
            if (Root != null) UnityEngine.Object.Destroy(Root.gameObject);
            foreach (var m in made) if (m != null) UnityEngine.Object.Destroy(m);
            made.Clear();
            if (Profile != null) UnityEngine.Object.Destroy(Profile);
        }
    }
}
#endif
