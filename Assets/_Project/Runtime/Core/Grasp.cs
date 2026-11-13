// Fingers that close on what the hand holds (2026-11-10), for the protagonists. Armed puts the gun in the palm and opens these fingers; then, joint by joint from the
// knuckle out, each finger turns towards the palm until it, or any part of it further out, touches the gun's surface, and stops there: the fingers wrap the grip that is
// there, a thick one or a thin one, rather than closing by a set amount. A finger the clip left inside the gun first opens until it is clear. The gun's surface is its
// parts' own meshes, exactly (a convex hull would fill the hollow the fingers close into), held still in a physics scene of their own where nothing else is, so they
// touch nothing in the game; each finger, a capsule a finger thick along each bone, is carried into the gun's frame there and tested against them.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Dad.Core
{
    public sealed class Grasp : MonoBehaviour
    {
        /// <summary>Half a finger's thickness, metres.</summary>
        public float Radius = 0.0085f;
        /// <summary>For tests: of the fingers wrapped last frame, how many ended touching, and how many were inside the gun at the start (left as they were).</summary>
        public int Touching { get; private set; }
        public int Inside { get; private set; }
        /// <summary>For tests: how many of the gun's parts are its surface; how many could not be (their points unreadable).</summary>
        public int Parts { get; private set; }
        public int Unread { get; private set; }
        /// <summary>For tests, when set: each finger's last result, joint by joint (t touched, - no contact), or "in" (could not get out of the gun).</summary>
        public System.Text.StringBuilder Note;

        static readonly string[] Fingers = { "Thumb", "Index", "Middle", "Ring", "Little" };

        Animator body;
        Scene surfaceScene;
        PhysicsScene surface;
        readonly Collider[] overlap = new Collider[4];
        Transform holding;
        // per hand (0 right, 1 left), per finger, the three joints and the tip in the end joint's frame
        Transform[,,] joints;
        Vector3[,] tips;

        public static Grasp Give(Animator a) => a.gameObject.AddComponent<Grasp>();

        void Awake()
        {
            body = GetComponent<Animator>();
            joints = new Transform[2, 5, 3];
            tips = new Vector3[2, 5];
            for (int h = 0; h < 2; h++)
                for (int f = 0; f < 5; f++)
                {
                    for (int j = 0; j < 3; j++) joints[h, f, j] = body.GetBoneTransform(Bone(h == 0, f, j));
                    Transform mid = joints[h, f, 1], end = joints[h, f, 2];
                    if (mid == null || end == null) continue;
                    // the fingertip lies on past the end joint, about as far as the end joint is long (the skeleton has no bone there)
                    var tip = end.position + (end.position - mid.position) * 0.8f;
                    tips[h, f] = end.InverseTransformPoint(tip);
                }
        }

        static HumanBodyBones Bone(bool right, int finger, int joint)
        {
            // HumanBodyBones runs Left Thumb Proximal .. Left Little Distal, then the same on the right, three a finger
            var first = right ? HumanBodyBones.RightThumbProximal : HumanBodyBones.LeftThumbProximal;
            return first + finger * 3 + joint;
        }

        /// <summary>Takes hold of a gun: its parts, as they are in its frame, become the surface the fingers close on.</summary>
        public void Hold(Transform gun)
        {
            Release();
            holding = gun;
            surfaceScene = SceneManager.CreateScene("grip " + GetInstanceID(), new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            surface = surfaceScene.GetPhysicsScene();
            foreach (var f in gun.GetComponentsInChildren<MeshFilter>())
            {
                var shown = f.GetComponent<Renderer>();
                if (f.sharedMesh == null || shown == null || !shown.enabled || !f.gameObject.activeInHierarchy) continue;
                if (!f.sharedMesh.isReadable) { Unread++; continue; }
                var part = new GameObject(f.name);
                SceneManager.MoveGameObjectToScene(part, surfaceScene);
                // the gun's own frame, in metres: its origin at the scene's, unturned
                part.transform.SetPositionAndRotation(Quaternion.Inverse(gun.rotation) * (f.transform.position - gun.position), Quaternion.Inverse(gun.rotation) * f.transform.rotation);
                part.transform.localScale = f.transform.lossyScale;
                part.AddComponent<MeshCollider>().sharedMesh = f.sharedMesh; // not convex: the surface itself
                Parts++;
            }
        }

        /// <summary>Lets go: the gun's surface is put away.</summary>
        public void Release()
        {
            if (surfaceScene.IsValid()) SceneManager.UnloadSceneAsync(surfaceScene);
            surfaceScene = default;
            holding = null;
            Parts = Unread = 0;
        }

        /// <summary>Closes the fingers of each hand that holds on the gun; the right index finger is left on the trigger as the gun's holder put it.</summary>
        public void Wrap(bool right, bool left)
        {
            Touching = Inside = RightFingers = LeftFingers = 0;
            Note?.Clear();
            if (holding == null || Parts == 0) return;
            // some guns' parts are mirrored in their files, their faces turned inwards: the surface is found from either side while the hands are put on it
            bool backs = Physics.queriesHitBackfaces;
            Physics.queriesHitBackfaces = true;
            try
            {
                if (right) { Place(0, !left); Hand(0, true); }
                if (left) { Place(1, false); Hand(1, false); }
            }
            finally { Physics.queriesHitBackfaces = backs; }
        }

        /// <summary>The grip, in the scene, where the right palm goes (the gun's holder sets it each frame).</summary>
        public Vector3 GripAt;
        /// <summary>For tests: whether each hand found the gun's surface last frame, and how far its palm is from it (metres).</summary>
        public bool RightOn { get; private set; }
        public bool LeftOn { get; private set; }

        Vector3 PalmFacing(int h)
        {
            var hand = body.GetBoneTransform(h == 0 ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
            return Vector3.Cross(joints[h, 2, 0].position - hand.position, joints[h, 1, 0].position - joints[h, 4, 0].position).normalized * (h == 0 ? 1 : -1);
        }

        Vector3 PalmAt(int h)
        {
            var hand = body.GetBoneTransform(h == 0 ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
            return Vector3.Lerp(hand.position, joints[h, 2, 0].position, 0.55f);
        }

        // A hand put on the gun by its own hold, not the clip's: the right palm against the side of the grip, the index finger's knuckle at the top of it, the fingers
        // pointing forward round its front; the left palm up under the fore-end, the index finger towards the muzzle, the fingers round the far side. The hand is turned
        // to that hold, brought in along the way its palm faces until the back of the hand would go into the gun, and the arm is bent to put the wrist there. A gun held
        // in one hand (a pistol) whose grip the arm cannot reach is brought to the hand instead.
        void Place(int h, bool oneHanded)
        {
            bool right = h == 0;
            if (right) RightOn = false; else LeftOn = false;
            var hand = body.GetBoneTransform(right ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
            var upper = body.GetBoneTransform(right ? HumanBodyBones.RightUpperArm : HumanBodyBones.LeftUpperArm);
            var lower = body.GetBoneTransform(right ? HumanBodyBones.RightLowerArm : HumanBodyBones.LeftLowerArm);
            if (hand == null || upper == null || lower == null || joints[h, 1, 0] == null || joints[h, 2, 0] == null || joints[h, 4, 0] == null) return;
            var gunTurn = holding.rotation;
            var back = Quaternion.Inverse(gunTurn);
            // the hold, in the gun's frame (x right, y up, z towards the muzzle): the palm's way, the index-to-little-finger line, the wrist-to-knuckles line
            Vector3 palm, across;
            float arm = 0;
            Vector3 from, dir; // a ray, in the gun's frame, to find the surface the palm goes on
            var g = back * (GripAt - holding.position); // the grip lies on the gun's middle line
            if (right)
            {
                palm = Vector3.left; across = new Vector3(0, 1, 0.3f).normalized; // a grip raked back: the index finger's knuckle above and forward of the little finger's
                from = g + Vector3.right * 0.15f; dir = Vector3.left;
            }
            else
            {
                palm = Vector3.up; across = Vector3.forward;
                var l = back * (PalmAt(1) - holding.position);
                // where the clip has the hand along the gun, or nearer the body if a shorter arm cannot reach there
                arm = Vector3.Distance(upper.position, lower.position) + Vector3.Distance(lower.position, hand.position) + Vector3.Distance(hand.position, PalmAt(1));
                for (int i = 0; i < 3; i++)
                {
                    var under = holding.position + gunTurn * new Vector3(g.x, l.y, l.z);
                    float over = Vector3.Distance(upper.position, under) - arm * 0.95f;
                    if (over <= 0) break;
                    l.z -= over;
                }
                from = new Vector3(g.x, l.y - 0.2f, l.z); dir = Vector3.up;
            }
            string who = right ? " Rhand:" : " Lhand:";
            RaycastHit hit = default;
            bool found = false;
            if (right)
            {
                // the grip's side at the grip, or the nearest to it: a little forward or lower, where the set point falls just behind the grip or under the frame
                foreach (var dz in Offsets)
                {
                    foreach (var dy in Offsets)
                        if (dy <= 0 && surface.Raycast(from + new Vector3(0, dy, dz), dir, out hit, 0.4f, ~0, QueryTriggerInteraction.Collide)) { found = true; break; }
                    if (found) break;
                }
            }
            else
            {
                // the fore-end's underside near where the clip put the hand: of the undersides within 8 cm back and 12 cm forward, the highest, so not the bottom of a
                // magazine or a fore-grip hanging below it, and of those the nearest to the clip's place
                float top = float.MinValue;
                var hits = new List<RaycastHit>();
                for (float dz = -0.08f; dz <= 0.12f; dz += 0.01f)
                {
                    // across the gun's width, the lowest: a fore-end can be open underneath down its middle, its sides lower than what is above the gap
                    bool any = false;
                    RaycastHit low = default;
                    for (float dx = -0.03f; dx <= 0.031f; dx += 0.01f)
                        if (surface.Raycast(from + new Vector3(dx, 0, dz), dir, out var h3, 0.4f, ~0, QueryTriggerInteraction.Collide) && (!any || h3.point.y < low.point.y)) { low = h3; any = true; }
                    if (!any) continue;
                    low.point = new Vector3(from.x, low.point.y, low.point.z); // the palm goes under the middle, at the height of the lowest side
                    hits.Add(low); top = Mathf.Max(top, low.point.y);
                }
                float best = float.MaxValue;
                foreach (var h2 in hits)
                {
                    if (Mathf.Abs(h2.point.z - from.z) >= best) continue;
                    // the hand is a palm wide along the gun: nothing within that may hang lower (a magazine, a fore-grip)
                    bool open = true;
                    foreach (var o in hits) if (Mathf.Abs(o.point.z - h2.point.z) <= 0.045f && o.point.y < h2.point.y - 0.02f) { open = false; break; }
                    // and the arm must reach the palm there, a few centimetres under it
                    bool reach = Vector3.Distance(upper.position, holding.position + gunTurn * (h2.point - Vector3.up * 0.03f)) <= arm * 0.97f;
                    if (open && reach) { best = Mathf.Abs(h2.point.z - from.z); hit = h2; found = true; }
                }
                if (Note != null)
                {
                    Note.Append($" Lprofile(from z {from.z * 100:F0} y {from.y * 100 + 20:F0} x {from.x * 100:F1}, cm):");
                    foreach (var o in hits) Note.Append($" {o.point.z * 100:F0}/{o.point.y * 100:F0}");
                    if (found) Note.Append($" chose {hit.point.z * 100:F0}/{hit.point.y * 100:F0}");
                }
            }
            if (!found) { Note?.Append(who).Append("no surface"); return; }
            var along = right ? Vector3.Cross(across, palm) : Vector3.Cross(palm, across); // so that the palm comes out the way PalmFacing measures it
            Quaternion wasUpper = upper.rotation, wasLower = lower.rotation, wasHand = hand.rotation;
            void Undo() { upper.rotation = wasUpper; lower.rotation = wasLower; hand.rotation = wasHand; }
            // turn the hand to the hold
            var nowAlong = joints[h, 2, 0].position - hand.position;
            var nowAcross = joints[h, 1, 0].position - joints[h, 4, 0].position;
            var want = Quaternion.LookRotation(gunTurn * along, gunTurn * across) * Quaternion.Inverse(Quaternion.LookRotation(nowAlong, nowAcross));
            hand.rotation = want * hand.rotation;
            // bring it in: from 6 cm off the surface towards it, until the back of the hand (wrist to each knuckle, a palm thick) would go in
            if (!right)
            {
                // the fore-end lies across the palm, the knuckles at its far side so the fingers close round it: the hand moves across until they are there
                var knuckles = (joints[h, 1, 0].position + joints[h, 4, 0].position) / 2;
                float kx = (back * (knuckles - PalmAt(h))).x;
                if (surface.Raycast(new Vector3(g.x + 0.15f, hit.point.y + 0.012f, hit.point.z), Vector3.left, out var side, 0.3f, ~0, QueryTriggerInteraction.Collide))
                {
                    float x = Mathf.Clamp(side.point.x + Radius - kx, hit.point.x - 0.04f, hit.point.x + 0.04f);
                    Note?.Append($" Lside {side.point.x * 100:F1} knuckles {kx * 100:F1} palm x {hit.point.x * 100:F1}->{x * 100:F1}");
                    hit.point = new Vector3(x, hit.point.y, hit.point.z);
                }
            }
            var surfaceAt = holding.position + gunTurn * hit.point;
            var n = gunTurn * palm; // the way the palm faces, into the gun
            var keep = hand.rotation;
            bool Clash() { for (int f = 1; f < 5; f++) if (Segment(hand.position, joints[h, f, 0].position, PalmThick)) return true; return false; }
            void Put(float off)
            {
                var wrist = hand.position + (surfaceAt - n * off) - PalmAt(h);
                Armed.Bend(upper, lower, hand, wrist, -transform.up);
                hand.rotation = keep;
            }
            const float far = 0.06f;
            Put(far);
            float miss = Vector3.Distance(PalmAt(h), surfaceAt - n * far);
            if (miss > 0.015f && !oneHanded) { Note?.Append(who).Append($"out of reach by {miss * 100:F1} cm"); Undo(); return; }
            if (Clash()) { Note?.Append(who).Append("in the gun 6 cm off (").Append(overlap[0] != null ? overlap[0].name : "?").Append(")"); Undo(); return; } // even off the surface the hand is in the gun (another part in the way): left as the clip put it
            float lo = 0, hi = far; // lo would be in, hi is clear
            for (int i = 0; i < 6; i++)
            {
                float mid = (lo + hi) / 2;
                Put(mid);
                if (Clash()) lo = mid; else hi = mid;
            }
            Put(hi);
            // on the gun only if the arm reached: the palm within a centimetre and a half of where it was put
            bool on = Vector3.Distance(PalmAt(h), surfaceAt - n * hi) < 0.015f;
            if (!on && oneHanded)
            {
                // the arm as the clip had it, the hand in its hold, and the gun brought to the hand
                upper.rotation = wasUpper; lower.rotation = wasLower; hand.rotation = keep;
                holding.position += PalmAt(h) + n * hi - surfaceAt;
                on = true;
            }
            if (!on) { Note?.Append(who).Append($"out of reach by {Vector3.Distance(PalmAt(h), surfaceAt - n * hi) * 100:F1} cm"); Undo(); return; }
            if (right) RightOn = true; else LeftOn = true;
        }

        static readonly float[] Offsets = { 0, 0.01f, -0.01f, 0.02f, -0.02f, 0.03f, 0.04f };

        /// <summary>Half the palm's thickness over the bones of the back of the hand, metres.</summary>
        public float PalmThick = 0.016f;

        // How far each joint bends in a full grip, in degrees: the knuckle, the middle joint, the end joint. The end joint follows the middle one at about seven tenths, as a
        // finger's tendons make it, so a finger never closes at the knuckle with its tip left stiff. The thumb swings in at its root (by the wrist) as it bends, so it
        // comes to lie along the side of what the hand holds.
        static readonly float[] Bend = { 85, 100, 70 }, ThumbBend = { 35, 45, 60 };
        /// <summary>How far a finger with nothing to touch closes, as a share of a full grip: a hand at rest is a little closed, never straight or bent back.</summary>
        public float Rest = 0.3f, ThumbRest = 0.6f;
        /// <summary>For tests: of the four fingers (not the thumb), how many of each hand touched the gun last frame.</summary>
        public int RightFingers { get; private set; }
        public int LeftFingers { get; private set; }

        readonly Quaternion[] given = new Quaternion[3];
        readonly Quaternion[,] straight = new Quaternion[5, 3];
        readonly Vector3[] axes = new Vector3[5];
        readonly Vector3[] curl = new Vector3[5]; // the share of a full grip each finger closed to, knuckle, middle, end; x = -1 where it rests on nothing, -2 where it was opened, left or skipped

        void Hand(int h, bool skipIndex)
        {
            var hand = body.GetBoneTransform(h == 0 ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
            Transform middle = joints[h, 2, 0], index = joints[h, 1, 0], little = joints[h, 4, 0];
            if (hand == null || middle == null || index == null || little == null) return;
            var palm = Vector3.Cross(middle.position - hand.position, index.position - little.position).normalized * (h == 0 ? 1 : -1);
            for (int f = 0; f < 5; f++) { curl[f] = new Vector3(-2, 0, 0); axes[f] = Vector3.zero; }
            for (int f = 0; f < 5; f++)
            {
                if (skipIndex && f == 1) continue;
                if (joints[h, f, 0] == null || joints[h, f, 1] == null || joints[h, f, 2] == null) continue;
                string name = (h == 0 ? " R" : " L") + Fingers[f] + ":";
                for (int j = 0; j < 3; j++) given[j] = joints[h, f, j].localRotation;
                // the finger made straight first, each bone in line with the one before it, and the four fingers side by side along the hand (wrist to middle
                // knuckle), not fanned out: what the clip or the gun's holder left, bent back or spread, does not carry into the grip
                for (int j = f == 0 ? 1 : 0; j < 3; j++)
                {
                    var joint = joints[h, f, j];
                    var before = j == 0 ? middle.position - hand.position : joint.position - joints[h, f, j - 1].position;
                    joint.rotation = Quaternion.FromToRotation(Next(h, f, j) - joint.position, before) * joint.rotation;
                }
                for (int j = 0; j < 3; j++) straight[f, j] = joints[h, f, j].localRotation;
                // it bends in one plane, about one axis: across the finger, towards the palm
                int first = f == 0 ? 1 : 0;
                var axis = Vector3.Cross(Next(h, f, first) - joints[h, f, first].position, palm);
                if (axis.sqrMagnitude < 1e-10f) { Restore(h, f); continue; }
                axis.Normalize();
                axes[f] = axis;
                var bend = f == 0 ? ThumbBend : Bend;
                bool In(float k, float m, float e) { Shape(h, f, axis, bend[0] * k, bend[1] * m, bend[2] * e); return Hits(h, f, 0); }
                if (In(0, 0, 0))
                {
                    // straight, it is in the gun: it opens at the knuckle, as little as it must; if that does not free it, the finger stays as it was given
                    // (the thumb opens at its middle joint, the first that bends it, and also swings out at its root)
                    bool Out(float x) { Shape(h, f, axis, -40 * x, f == 0 ? -40 * x : 0, 0); return !Hits(h, f, 0); }
                    if (!Out(1)) { Restore(h, f); Inside++; Note?.Append(name).Append("in"); continue; }
                    float open = Last(1, 0, x => Out(x)); // the least opening that frees it
                    Out(open);
                    Note?.Append(name).Append($"opened {open * 40:F0}");
                    continue;
                }
                if (!In(1, 1, 1)) { float r = f == 0 ? ThumbRest : Rest; In(r, r, r); curl[f].x = -1; Note?.Append(name).Append("rest"); continue; } // nothing within reach: a hand at rest
                // the whole finger closes until it touches; then the joints beyond the one that touched go on closing until they touch too; then the tip
                float t = Last(0, 1, x => !In(x, x, x));
                float u = Last(t, 1, x => !In(t, x, x));
                float v = Last(u, 1, x => !In(t, u, x));
                In(t, u, v);
                Touching++;
                curl[f] = new Vector3(t, u, v);
                if (f > 0) { if (h == 0) RightFingers++; else LeftFingers++; }
                Note?.Append(name).Append($"{t * bend[0]:F0}/{u * bend[1]:F0}/{v * bend[2]:F0}");
            }
            // a finger with nothing to touch still goes with the fingers that hold (one tendon moves them together): it closes as far as they did on average, as
            // far as it is free to, not left straighter than them like a claw
            Vector3 held = Vector3.zero;
            int closed = 0;
            for (int f = 1; f < 5; f++) if (curl[f].x >= 0) { held += curl[f]; closed++; }
            if (closed == 0) return;
            held /= closed;
            for (int f = 1; f < 5; f++)
            {
                if (curl[f].x != -1) continue;
                var from = Vector3.one * Rest;
                if (held.x <= Rest) continue;
                int g = f;
                bool Free(float s) { var c = Vector3.Lerp(from, held, s); Shape(h, g, axes[g], Bend[0] * c.x, Bend[1] * c.y, Bend[2] * c.z); return !Hits(h, g, 0); }
                float most = Last(0, 1, Free);
                Free(most);
                Note?.Append(" ").Append(h == 0 ? "R" : "L").Append(Fingers[f]).Append($" follows {most * 100:F0}%");
            }
        }

        // The largest value from lo towards hi for which free holds (free(lo) is taken to hold), to within a sixty-fourth of the range; hi itself if free there.
        static float Last(float lo, float hi, System.Func<float, bool> free)
        {
            if (free(hi)) return hi;
            for (int i = 0; i < 6; i++)
            {
                float mid = (lo + hi) / 2;
                if (free(mid)) lo = mid; else hi = mid;
            }
            return lo;
        }

        // Poses one finger from straight: each joint bent this many degrees about the finger's own axis (one plane, so a finger closes without fanning out).
        void Shape(int h, int f, Vector3 axis, float knuckle, float mid, float end)
        {
            for (int j = 0; j < 3; j++) joints[h, f, j].localRotation = straight[f, j];
            if (knuckle != 0) joints[h, f, 0].rotation = Quaternion.AngleAxis(knuckle, axis) * joints[h, f, 0].rotation;
            if (mid != 0) joints[h, f, 1].rotation = Quaternion.AngleAxis(mid, axis) * joints[h, f, 1].rotation;
            if (end != 0) joints[h, f, 2].rotation = Quaternion.AngleAxis(end, axis) * joints[h, f, 2].rotation;
        }

        void Restore(int h, int f) { for (int j = 0; j < 3; j++) joints[h, f, j].localRotation = given[j]; }

        // Where a finger's bone ends: the next joint, or past the end joint, the tip.
        Vector3 Next(int h, int f, int j) => j < 2 ? joints[h, f, j + 1].position : joints[h, f, 2].TransformPoint(tips[h, f]);

        // Does the finger, from this joint to its tip, go into the gun?
        bool Hits(int h, int f, int from)
        {
            for (int j = from; j < 3; j++)
            {
                var a = joints[h, f, j].position;
                var b = j < 2 ? joints[h, f, j + 1].position : joints[h, f, 2].TransformPoint(tips[h, f]);
                if (Segment(a, b)) return true;
            }
            return false;
        }

        // Does a capsule a finger thick from a to b, in the scene, go into the gun's surface? It is carried into the gun's frame, where the surface lies.
        bool Segment(Vector3 a, Vector3 b) => Segment(a, b, Radius);

        bool Segment(Vector3 a, Vector3 b, float radius)
        {
            var back = Quaternion.Inverse(holding.rotation);
            var p = back * (a - holding.position);
            var q = back * (b - holding.position);
            return surface.OverlapCapsule(p, q, radius, overlap, ~0, QueryTriggerInteraction.Collide) > 0;
        }


        void OnDestroy() => Release();
    }
}
