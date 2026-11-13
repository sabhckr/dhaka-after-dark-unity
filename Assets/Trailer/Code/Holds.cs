// Exact holds: where each hand goes on each weapon, read from the reference photographs and placed on the weapon's own measured shape; fingers close until they touch it.
// The map and audit at the end report what the hands really did, joint by joint, in the weapon's own space.
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Dad.Trailer
{
    /// <summary>A solid part of a weapon, in the weapon's space (x right, y up, z towards the muzzle). Up can lean with the grip's rake.</summary>
    public struct Solid
    {
        public Vector3 Centre, Up, Half;
        public Solid(float x, float y, float z, float hx, float hy, float hz, float rake = 0) { Centre = new Vector3(x, y, z); Half = new Vector3(hx, hy, hz); Up = new Vector3(0, Mathf.Cos(rake * Mathf.Deg2Rad), Mathf.Sin(rake * Mathf.Deg2Rad)); }
        public float Distance(Vector3 p)
        {
            const float round = .005f;
            var d = p - Centre; var z = Vector3.Cross(Vector3.right, Up);
            var q = new Vector3(Mathf.Abs(d.x) - Half.x + round, Mathf.Abs(Vector3.Dot(d, Up)) - Half.y + round, Mathf.Abs(Vector3.Dot(d, z)) - Half.z + round);
            return new Vector3(Mathf.Max(q.x, 0), Mathf.Max(q.y, 0), Mathf.Max(q.z, 0)).magnitude + Mathf.Min(Mathf.Max(q.x, Mathf.Max(q.y, q.z)), 0) - round;
        }
    }

    /// <summary>Where the hands go on one weapon, in metres from the middle of the model: x right, y up, z towards the muzzle.</summary>
    public sealed class GripMap
    {
        public string Name;
        public Vector3 FireKnuckle, FireAlong, FireIndex, FireWrist, SupKnuckle, SupAlong, SupIndex, SupWrist, Trigger, IndexRest, FireThumbVia, FireThumbTip, SupThumbVia, SupThumbTip;
        public Solid[] Body;
        public float Distance(Vector3 p) { float best = float.MaxValue; foreach (var b in Body) best = Mathf.Min(best, b.Distance(p)); return best; }

        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

        // Thumbs forward, high on the backstrap, firing fingers under the guard, support fingers wrapped over them, both thumbs on the left of the frame.
        public static readonly GripMap Glock = new GripMap
        {
            Name = "glock",
            FireKnuckle = V(.026f, .007f, -.056f), FireAlong = V(.25f, .09f, .96f), FireIndex = V(0, 1, .15f), FireWrist = V(.006f, -.043f, -.14f),
            SupKnuckle = V(-.044f, -.02f, -.016f), SupAlong = V(.05f, .05f, .99f), SupIndex = V(0, 1, .22f), SupWrist = V(-.07f, -.043f, -.111f),
            Trigger = V(0, .017f, -.011f), IndexRest = V(.024f, .024f, .044f),
            FireThumbVia = V(-.014f, .03f, -.098f), FireThumbTip = V(-.026f, .047f, -.024f), SupThumbVia = V(-.044f, .03f, -.05f), SupThumbTip = V(-.026f, .041f, .057f),
            Body = new[] { new Solid(0, -.034f, -.073f, .017f, .05f, .028f, 20), new Solid(0, .036f, -.003f, .015f, .03f, .096f), new Solid(0, .011f, .004f, .012f, .012f, .036f) },
        };

        // Firing wrist below the knuckles, finger on the trigger, support hand clamped round the magazine well with its forearm under the gun.
        public static readonly GripMap Scar = new GripMap
        {
            Name = "scar",
            FireKnuckle = V(.026f, -.013f, -.165f), FireAlong = V(.2f, .35f, .92f), FireIndex = V(0, 1, .1f), FireWrist = V(.01f, -.076f, -.215f),
            SupKnuckle = V(-.03f, -.034f, -.022f), SupAlong = V(.2f, .35f, .92f), SupIndex = V(0, .92f, -.35f), SupWrist = V(-.045f, -.1f, -.1f),
            Trigger = V(0, -.025f, -.128f), IndexRest = V(.024f, -.008f, -.09f),
            FireThumbVia = V(-.014f, -.02f, -.235f), FireThumbTip = V(-.027f, -.02f, -.168f), SupThumbVia = V(-.036f, -.02f, -.07f), SupThumbTip = V(-.024f, -.012f, -.012f),
            Body = new[] { new Solid(0, -.082f, -.222f, .016f, .072f, .032f, 35), new Solid(0, .075f, -.1f, .016f, .07f, .125f), new Solid(0, .0f, -.15f, .016f, .02f, .06f), new Solid(0, -.03f, -.045f, .016f, .04f, .046f), new Solid(0, -.03f, -.125f, .01f, .012f, .03f), new Solid(0, -.125f, -.03f, .015f, .06f, .042f) },
        };

        public static readonly GripMap Ump = new GripMap
        {
            Name = "ump",
            FireKnuckle = V(.023f, .017f, -.002f), FireAlong = V(.2f, .3f, .93f), FireIndex = V(0, 1, .1f), FireWrist = V(.008f, -.046f, -.052f),
            SupKnuckle = V(-.028f, -.03f, .155f), SupAlong = V(.2f, .35f, .92f), SupIndex = V(0, .92f, -.35f), SupWrist = V(-.043f, -.096f, .077f),
            Trigger = V(0, .006f, .037f), IndexRest = V(.021f, .024f, .09f),
            FireThumbVia = V(-.012f, .014f, -.062f), FireThumbTip = V(-.024f, .022f, -.002f), SupThumbVia = V(-.034f, -.014f, .1f), SupThumbTip = V(-.022f, -.006f, .17f),
            Body = new[] { new Solid(0, -.035f, -.035f, .011f, .05f, .03f, 27), new Solid(0, .06f, .03f, .02f, .05f, .16f), new Solid(0, -.012f, .145f, .016f, .036f, .036f), new Solid(0, -.1f, .165f, .013f, .06f, .032f) },
        };
    }

    /// <summary>A knife: which way its tip and cutting edge point in the model, and how far the handle's middle is behind the model's centre.</summary>
    public sealed class Knife { public GameObject Prop; public Quaternion Fix; public Vector3 Centre; public float Back, Half; }

    public sealed partial class Actor
    {
        Bones3 FingerBones(bool left, int f) { var b = left ? LeftFingers : RightFingers; return new Bones3(Bone(b[f * 3]), Bone(b[f * 3 + 1]), Bone(b[f * 3 + 2])); }
        readonly struct Bones3 { public readonly Transform A, B, C; public Bones3(Transform a, Transform b, Transform c) { A = a; B = b; C = c; } }

        readonly Dictionary<Transform, Vector3> distalRest = new Dictionary<Transform, Vector3>();
        static bool HasTip(Transform d) => d.childCount > 0 && (d.GetChild(0).position - d.position).sqrMagnitude > 1e-8f;
        Vector3 Tip(Transform distal, Transform before)
        {
            if (HasTip(distal)) return distal.GetChild(0).position;
            if (!distalRest.TryGetValue(distal, out var l)) distalRest[distal] = l = Quaternion.Inverse(distal.rotation) * (distal.position - before.position) * .8f;
            return distal.position + distal.rotation * l;
        }
        /// <summary>Learns each fingertip's rest direction while the hand is still in its bind pose.</summary>
        void LearnTips() { foreach (bool left in new[] { false, true }) { for (int i = 0; i < 4; i++) { var f = FingerBones(left, i); if (f.C != null && f.B != null) Tip(f.C, f.B); } var t = ThumbBones(left); if (t[2] != null && t[1] != null) Tip(t[2], t[1]); } }
        float FingerRadius(bool left) { var b = left ? LeftFingers : RightFingers; return Vector3.Distance(Bone(b[0]).position, Bone(b[9]).position) / 8; }
        Vector3 HandAlong(bool left) { var b = left ? LeftFingers : RightFingers; return (Bone(b[3]).position - Bone(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand).position).normalized; }
        Vector3 HandSide(bool left) { var b = left ? LeftFingers : RightFingers; return Vector3.ProjectOnPlane(Bone(b[0]).position - Bone(b[9]).position, HandAlong(left)).normalized; }

        /// <summary>Points the last finger bone so its tip goes one way, whether or not the rig has an end bone.</summary>
        void AimTip(Transform distal, Transform before, Vector3 dir) { var now = Tip(distal, before) - distal.position; if (now.sqrMagnitude > 1e-8f) distal.rotation = Quaternion.FromToRotation(now, dir) * distal.rotation; }

        /// <summary>Puts this hand's first knuckle exactly on a point, the hand turned exactly so; the arm follows. If a knuckle would be inside the weapon the whole hand rests on its surface instead. Returns the knuckle point used.</summary>
        Vector3 PlaceHand(bool left, Vector3 knuckle, Vector3 along, Vector3 indexSide, Vector3 pole, Func<Vector3, float> solid, float fr)
        {
            var hand = Bone(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            var bones = left ? LeftFingers : RightFingers;
            var k = Quaternion.Inverse(hand.rotation) * (Bone(bones[0]).position - hand.position);
            var rot = Palm(left, along, indexSide);
            Solve(left, knuckle - rot * k, pole, rot);
            var a = along.normalized; var s = Vector3.ProjectOnPlane(indexSide, a).normalized;
            var outward = (left ? Vector3.Cross(a, s) : -Vector3.Cross(a, s)).normalized;   // away from the palm
            float deficit = 0;
            for (int i = 0; i < 4; i++) { var b = Bone(bones[i * 3]); if (b != null) deficit = Mathf.Max(deficit, fr + .001f - solid(b.position)); }
            if (deficit <= 0) return knuckle;
            knuckle += outward * deficit;
            Solve(left, knuckle - rot * k, pole, rot);
            return knuckle;
        }

        Vector3 WristFor(bool left, Vector3 knuckle, Vector3 along, Vector3 indexSide)
        {
            var hand = Bone(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            var k = Quaternion.Inverse(hand.rotation) * (Bone((left ? LeftFingers : RightFingers)[0]).position - hand.position);
            return knuckle - Palm(left, along, indexSide) * k;
        }
        float ArmLength(bool left) { var u = Bone(left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm); var l = Bone(left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm); var h = Bone(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand); return Vector3.Distance(u.position, l.position) + Vector3.Distance(l.position, h.position); }

        static void Aim(Transform bone, Transform next, Vector3 dir) { if ((next.position - bone.position).sqrMagnitude > 1e-8f) bone.rotation = Quaternion.FromToRotation(next.position - bone.position, dir) * bone.rotation; }

        /// <summary>Closes one finger joint by joint until it touches the solid. A joint that starts inside the solid opens until it is clear.</summary>
        void Wrap(bool left, int finger, Func<Vector3, float> solid, float radius, Func<Vector3, float> real = null)
        {
            var along = HandAlong(left); var axis = left ? HandSide(left) : -HandSide(left);
            var f = FingerBones(left, finger); if (f.A == null || f.B == null || f.C == null) return;
            var dir = along; float[] limit = { 92, 110, 88 };
            var chain = new[] { f.A, f.B, f.C };
            for (int j = 0; j < 3; j++)
            {
                var bone = chain[j]; float len = j < 2 ? Vector3.Distance(bone.position, chain[j + 1].position) : Vector3.Distance(f.C.position, Tip(f.C, f.B));
                bool Hit(float deg) { var d = Quaternion.AngleAxis(deg, axis) * dir; for (float k = .34f; k < 1.05f; k += .33f) if (solid(bone.position + d * len * k) < radius) return true; return false; }
                float best = 0;
                if (Hit(0)) { for (float deg = -1.5f; deg >= -32; deg -= 1.5f) { best = deg; if (!Hit(deg)) break; } }
                else for (float deg = 1.5f; deg <= limit[j]; deg += 1.5f) { if (Hit(deg)) break; best = deg; }
                dir = Quaternion.AngleAxis(best, axis) * dir;
                if (j < 2) Aim(bone, chain[j + 1], dir);
                else AimTip(f.C, f.B, dir);
            }
            // the last few millimetres: press the finger in until the pad touches
            for (int i = 0; i < 40 && solid(Tip(f.C, f.B)) - radius > .002f; i++)
            {
                var keep = f.A.rotation; f.A.rotation = Quaternion.AngleAxis(1.5f, axis) * f.A.rotation;
                var r = real ?? solid; if (r(f.B.position) - radius < -.003f || r(f.C.position) - radius < -.003f) { f.A.rotation = keep; break; }
            }
        }

        /// <summary>Lays the first finger straight towards a point: along the frame, off the trigger.</summary>
        void Straight(bool left, Vector3 point)
        {
            var f = FingerBones(left, 0); if (f.A == null || f.B == null || f.C == null) return;
            Aim(f.A, f.B, point - f.A.position); Aim(f.B, f.C, point - f.B.position);
            AimTip(f.C, f.B, point - f.C.position);
        }

        /// <summary>Curls the first finger so its pad rests on a point: the trigger.</summary>
        void OnTrigger(bool left, Vector3 point)
        {
            var along = HandAlong(left); var axis = left ? HandSide(left) : -HandSide(left);
            var f = FingerBones(left, 0); if (f.A == null || f.B == null || f.C == null) return;
            axis = Vector3.ProjectOnPlane(axis, point - f.A.position).normalized; along = Vector3.ProjectOnPlane(along, axis).normalized;
            float l1 = Vector3.Distance(f.A.position, f.B.position), l2 = Vector3.Distance(f.B.position, f.C.position), l3 = Vector3.Distance(f.C.position, Tip(f.C, f.B));
            float a0 = 0, b0 = 0, c0 = 0, best = float.MaxValue;
            for (float a = 0; a <= 80; a += 2) for (float b = 0; b <= 112; b += 2)
            {
                foreach (float kc in new[] { .55f, .7f, .85f, 1f }) { float c = b * kc;
                var d1 = Quaternion.AngleAxis(a, axis) * along; var d2 = Quaternion.AngleAxis(a + b, axis) * along; var d3 = Quaternion.AngleAxis(a + b + c, axis) * along;
                var e = (f.A.position + d1 * l1 + d2 * l2 + d3 * l3 - point).sqrMagnitude;
                if (e < best) { best = e; a0 = a; b0 = b; c0 = c; } }
            }
            var at = along;
            at = Quaternion.AngleAxis(a0, axis) * at; Aim(f.A, f.B, at);
            at = Quaternion.AngleAxis(b0, axis) * at; Aim(f.B, f.C, at);
            at = Quaternion.AngleAxis(c0, axis) * at; AimTip(f.C, f.B, at);
        }

        Transform[] ThumbBones(bool left) => left ? new[] { Bone(HumanBodyBones.LeftThumbProximal), Bone(HumanBodyBones.LeftThumbIntermediate), Bone(HumanBodyBones.LeftThumbDistal) } : new[] { Bone(HumanBodyBones.RightThumbProximal), Bone(HumanBodyBones.RightThumbIntermediate), Bone(HumanBodyBones.RightThumbDistal) };

        /// <summary>The thumb's base aims at via; its last two bones bend so the tip lands on the point (a long thumb bends, it does not overrun).</summary>
        void ThumbTo(bool left, Vector3 via, Vector3 tip)
        {
            var t = ThumbBones(left); if (t[0] == null || t[1] == null || t[2] == null) return;
            Aim(t[0], t[1], via - t[0].position);
            float l1 = Vector3.Distance(t[1].position, t[2].position), l2 = Vector3.Distance(t[2].position, Tip(t[2], t[1]));
            var to = tip - t[1].position; float d = Mathf.Clamp(to.magnitude, Mathf.Abs(l1 - l2) + .001f, l1 + l2 - .0005f);
            float cos = Mathf.Clamp((l1 * l1 + d * d - l2 * l2) / (2 * l1 * d), -1, 1), sin = Mathf.Sqrt(1 - cos * cos);
            var dir = to.normalized; var side = Vector3.ProjectOnPlane(left ? HandSide(left) : -HandSide(left), dir).normalized;
            var joint = t[1].position + dir * (l1 * cos) + side * (l1 * sin);
            Aim(t[1], t[2], joint - t[1].position);
            AimTip(t[2], t[1], t[1].position + dir * d - t[2].position);
        }

        float FiringFingers(Vector3 w, float radius)
        {
            float best = float.MaxValue;
            for (int i = 1; i < 4; i++)
            {
                var f = FingerBones(false, i); if (f.A == null || f.B == null || f.C == null) continue;
                var p = new[] { f.A.position, f.B.position, f.C.position, Tip(f.C, f.B) };
                for (int s = 0; s < 3; s++)
                {
                    var ab = p[s + 1] - p[s]; float t = Mathf.Clamp01(Vector3.Dot(w - p[s], ab) / Mathf.Max(ab.sqrMagnitude, 1e-8f));
                    best = Mathf.Min(best, Vector3.Distance(w, p[s] + ab * t) - radius);
                }
            }
            return best;
        }

        // ----- knife -----
        readonly List<Knife> knives = new List<Knife>();
        (Knife k, Vector3 fist, Vector3 tip, Vector3 refDir, bool reverse, Vector3 pole)? stab;

        /// <summary>Gives this person a knife. modelTip and modelEdge are the directions of the point and the cutting edge in the model.</summary>
        public Knife TakeKnife(GameObject prop, float length)
        {
            bool was = prop.activeSelf; prop.SetActive(true);
            var keep = prop.transform.rotation; prop.transform.rotation = Quaternion.identity;
            var pts = new List<Vector3>();
            foreach (var f in prop.GetComponentsInChildren<MeshFilter>(true)) { var m = f.sharedMesh; if (m == null || !m.isReadable) throw new InvalidOperationException("knife mesh not readable"); var w = f.transform.localToWorldMatrix; foreach (var v in m.vertices) pts.Add(w.MultiplyPoint3x4(v) - prop.transform.position); }
            var mean = Vector3.zero; foreach (var p in pts) mean += p; mean /= pts.Count;
            Vector3 Principal(Vector3 start, Vector3? without)
            {
                var d = start.normalized;
                for (int it = 0; it < 30; it++)
                {
                    var n = Vector3.zero;
                    foreach (var p in pts) { var q = p - mean; if (without.HasValue) q -= without.Value * Vector3.Dot(q, without.Value); n += q * Vector3.Dot(q, d); }
                    d = n.normalized;
                }
                return d;
            }
            var longAxis = Principal(new Vector3(.3f, .5f, .8f), null);
            var width = Principal(new Vector3(.8f, .1f, .3f), longAxis);
            var thick = Vector3.Cross(longAxis, width);
            float lo = float.MaxValue, hi = float.MinValue; foreach (var p in pts) { float t = Vector3.Dot(p - mean, longAxis); lo = Mathf.Min(lo, t); hi = Mathf.Max(hi, t); }
            // the handle is the thick end
            float thickLo = 0, thickHi = 0, wTip = 0; int nLo = 0, nHi = 0;
            foreach (var p in pts) { var q = p - mean; float t = Vector3.Dot(q, longAxis), th = Mathf.Abs(Vector3.Dot(q, thick)); if (t < lo + (hi - lo) * .25f) { thickLo = Mathf.Max(thickLo, th); nLo++; } if (t > hi - (hi - lo) * .25f) { thickHi = Mathf.Max(thickHi, th); nHi++; } }
            var tip = thickHi > thickLo ? -longAxis : longAxis;
            foreach (var p in pts) { var q = p - mean; float t = Vector3.Dot(q, tip); if (t > (hi - lo) * .3f) wTip += Vector3.Dot(q, width); }
            var edge = wTip >= 0 ? width : -width;
            float k = length / (hi - lo);
            var mid = mean + longAxis * (lo + hi) * .5f;
            var centre = mid * k;
            prop.transform.localScale *= k;
            prop.transform.rotation = keep; prop.SetActive(was);
            Debug.Log($"[Trailer] knife tip {tip} edge {edge} scale {k:0.0000} thick ends {thickLo * k:0.000} {thickHi * k:0.000}");
            var kn = new Knife { Prop = prop, Fix = Quaternion.Inverse(Quaternion.LookRotation(tip, edge)), Centre = centre, Back = .09f, Half = length / 2 };
            knives.Add(kn);
            return kn;
        }

        /// <summary>This frame's knife hold: where the handle's middle is, which way the point goes. refDir is up for the forward grip, the forearm's way for the reverse grip.</summary>
        public void Stab(Knife k, Vector3 handle, Vector3 tipDir, Vector3 refDir, bool reverse, Vector3 pole) => stab = (k, handle, tipDir.normalized, refDir, reverse, pole);

        void StabNow()
        {
            if (!stab.HasValue) return;
            var (k, handle, d, refDir, reverse, pole) = stab.Value; stab = null;
            Vector3 a, s, edge;
            if (!reverse)
            {
                var p = Vector3.ProjectOnPlane(refDir, d).normalized;
                const float cos = .85f, sin = .53f;   // the handle lies diagonally across the palm: the point rises 35 degrees above the knuckles' line, thumb on top
                a = d * cos - p * sin; s = d * sin + p * cos; edge = -p;
            }
            else
            {
                a = Vector3.ProjectOnPlane(handle - Bone(HumanBodyBones.RightUpperArm).position, d).normalized;
                s = (-d * .985f + a * .17f).normalized;   // the handle goes straight across the palm, the blade comes out of the little-finger side
                edge = -a;
            }
            var n = Vector3.Cross(a, s).normalized;   // out of the right palm
            var hand = Bone(HumanBodyBones.RightHand);
            Vector3 mcp = (Bone(RightFingers[3]).position + Bone(RightFingers[6]).position) * .5f;
            var local = Quaternion.Inverse(hand.rotation) * (mcp - hand.position);
            var rot = Palm(false, a, s);
            Solve(false, handle - (rot * local - a * .02f + n * .027f), pole, rot);
            var knifeRot = Quaternion.LookRotation(d, edge) * k.Fix;
            var centre = handle + d * k.Back;
            k.Prop.transform.SetPositionAndRotation(centre - knifeRot * k.Centre, knifeRot);
            tips[k.Prop] = (centre + d * k.Half, d);
            Vector3 p0 = handle - d * .05f, p1 = handle + d * .045f;
            Func<Vector3, float> solid = w => { var ab = p1 - p0; float t = Mathf.Clamp01(Vector3.Dot(w - p0, ab) / ab.sqrMagnitude); return Vector3.Distance(w, p0 + ab * t) - .013f; };
            float fr = FingerRadius(false);
            for (int i = 0; i < 4; i++) Wrap(false, i, solid, fr);
            var to = Bone(RightFingers[1]).position + s * .004f;
            ThumbTo(false, handle + s * .02f - n * .014f, to);
            ShareTwist(false);
            heldKnife = k; knifeHandle = handle; knifeTip = d; knifeRev = reverse; knifeA = a; knifeS = s; knifeSolid = solid;
        }

        // ----- map and audit -----
        Gun mappedGun; Quaternion mapQ; Vector3 mapC, mapFire, mapSup; bool mapHot, mapSupport;
        Knife heldKnife; Vector3 knifeHandle, knifeTip, knifeA, knifeS; bool knifeRev; Func<Vector3, float> knifeSolid;
        readonly Dictionary<string, Vector3> driftFrom = new Dictionary<string, Vector3>();

        public bool HasHold => mappedGun != null || heldKnife != null;
        public void NewShot() => driftFrom.Clear();
        public Vector3 HoldCentre => mappedGun != null ? mapC + mapQ * mappedGun.Map.FireKnuckle : knifeHandle;
        public Vector3 HoldRight => mappedGun != null ? mapQ * Vector3.right : Vector3.Cross(Vector3.up, knifeTip).normalized;
        public Vector3 HoldForward => mappedGun != null ? mapQ * Vector3.forward : knifeTip;

        Vector3 Local(Vector3 w) => Quaternion.Inverse(mapQ) * (w - mapC);

        /// <summary>The joints of one hand, for drawing: wrist, then for each finger knuckle, joints and tip.</summary>
        public IEnumerable<(Vector3 a, Vector3 b)> HandLines(bool left)
        {
            var w = Bone(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            for (int i = 0; i < 4; i++)
            {
                var f = FingerBones(left, i); if (f.A == null || f.B == null || f.C == null) continue;
                yield return (w.position, f.A.position); yield return (f.A.position, f.B.position); yield return (f.B.position, f.C.position); yield return (f.C.position, Tip(f.C, f.B));
            }
            var t = ThumbBones(left); if (t[0] == null || t[1] == null || t[2] == null) yield break;
            yield return (w.position, t[0].position); yield return (t[0].position, t[1].position); yield return (t[1].position, t[2].position); yield return (t[2].position, Tip(t[2], t[1]));
        }

        /// <summary>Measures the hold this frame. Each line: name, measured, lowest allowed, highest allowed.</summary>
        public List<(string name, float value, float low, float high)> Measure()
        {
            var o = new List<(string, float, float, float)>();
            void Ck(string n, float v, float lo, float hi) => o.Add((n, v, lo, hi));
            if (mappedGun != null)
            {
                var m = mappedGun.Map;
                Func<Vector3, float> body = w => m.Distance(Local(w));
                float frF = FingerRadius(false), frS = FingerRadius(true);
                void Hand(bool left, string tag, Vector3 knuckle, Vector3 along, Vector3 index, Vector3 wrist, Func<Vector3, float> solid, float fr, int from)
                {
                    var bones = left ? LeftFingers : RightFingers; var h = Bone(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
                    Ck(tag + " knuckle error mm", Vector3.Distance(Local(Bone(bones[0]).position), knuckle) * 1000, 0, 3);
                    var a = Quaternion.Inverse(mapQ) * HandAlong(left); Ck(tag + " wrist-to-knuckle angle error deg", Vector3.Angle(a, along), 0, 3);
                    Ck(tag + " knuckle-line angle error deg", Vector3.Angle(Quaternion.Inverse(mapQ) * HandSide(left), Vector3.ProjectOnPlane(index, along)), 0, 3);
                    var lw = Local(h.position); Ck(tag + " wrist height below knuckles mm", (Local(Bone(bones[3]).position).y - lw.y) * 1000, 1, 125);
                    float scale = Vector3.Distance(h.position, Bone(bones[3]).position) / .095f;
                    Ck(tag + " wrist to photo position (scaled to this hand) mm", Vector3.Distance(lw, knuckle + (wrist - knuckle) * scale) * 1000, 0, 28);
                    string[] names = { "index", "middle", "ring", "little" };
                    for (int i = from; i < 4; i++)
                    {
                        var f = FingerBones(left, i); if (f.A == null || f.B == null || f.C == null) continue;
                        Ck($"{tag} {names[i]} tip gap mm", (solid(Tip(f.C, f.B)) - fr) * 1000, -2, 9);
                        float deep = Mathf.Min(solid(f.A.position), Mathf.Min(solid(f.B.position), solid(f.C.position)));
                        Ck($"{tag} {names[i]} joint clearance mm", (deep - fr) * 1000, -5, 40);
                    }
                    Ck(tag + " forearm-to-hand bend deg", Vector3.Angle(h.position - Bone(left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm).position, HandAlong(left)), 0, 62);
                }
                Hand(false, "firing", mapFire, m.FireAlong, m.FireIndex, m.FireWrist, body, frF, 1);
                var idx = FingerBones(false, 0); var idxTip = Tip(idx.C, idx.B);
                if (mapHot) Ck("firing index pad to trigger mm", Vector3.Distance(Local(idxTip), m.Trigger) * 1000, 0, 12);
                else Ck("firing index along bore deg", Vector3.Angle(Quaternion.Inverse(mapQ) * (idxTip - idx.A.position), Vector3.forward), 0, 12);
                var ft = ThumbBones(false); if (ft[2] != null) Ck("firing thumb tip error mm", Vector3.Distance(Local(Tip(ft[2], ft[1])), m.FireThumbTip) * 1000, 0, 26);
                if (mapSupport)
                {
                    Func<Vector3, float> both = w => body(w) - (mappedGun.Pistol ? frF * 2 : 0);
                    Hand(true, "support", mapSup, m.SupAlong, m.SupIndex, m.SupWrist, both, frS, 0);
                    float through = float.MaxValue; for (int i = 0; i < 4; i++) { var f = FingerBones(true, i); if (f.A == null) continue; through = Mathf.Min(through, FiringFingers(Tip(f.C, f.B), frF) - frS); through = Mathf.Min(through, FiringFingers(f.B.position, frF) - frS); }
                    Ck("support fingers through firing fingers mm", through * 1000, -7, 200);
                    var st = ThumbBones(true); if (st[2] != null) Ck(m.Name == "glock" ? "support thumb along bore deg" : "support thumb tip error mm", m.Name == "glock" ? Vector3.Angle(Quaternion.Inverse(mapQ) * (Tip(st[2], st[1]) - st[1].position), Vector3.forward) : Vector3.Distance(Local(Tip(st[2], st[1])), m.SupThumbTip) * 1000, 0, m.Name == "glock" ? 14 : 26);
                }
                string key = m.Name; var now = Local(Bone(HumanBodyBones.RightHand).position);
                if (driftFrom.TryGetValue(key, out var was)) Ck("hand slip on weapon since first frame mm", Vector3.Distance(now, was) * 1000, 0, 2); else driftFrom[key] = now;
            }
            if (heldKnife != null)
            {
                var bones = RightFingers; float fr = FingerRadius(false);
                var d = HandSide(false); float ang = Vector3.Angle(knifeTip, knifeRev ? -d : (knifeA * .85f + knifeS * .53f));
                Ck(knifeRev ? "reverse grip: blade out of little-finger side deg" : "forward grip: blade out of thumb side deg", knifeRev ? Vector3.Angle(knifeTip, -d) : Vector3.Angle(knifeTip, knifeA * .85f + knifeS * .53f), 0, 12);
                string[] names = { "index", "middle", "ring", "little" };
                for (int i = 0; i < 4; i++) { var f = FingerBones(false, i); Ck($"knife {names[i]} tip gap mm", (knifeSolid(Tip(f.C, f.B)) - fr) * 1000, -2, 12); }
                Ck("knife hand wrist bend deg", Vector3.Angle(Bone(HumanBodyBones.RightHand).position - Bone(HumanBodyBones.RightLowerArm).position, HandAlong(false)), 0, 62);
            }
            return o;
        }

        /// <summary>Finger length against the middle finger, for the open-hand reference photo. Reported, not adjusted: the models keep their own hands.</summary>
        public string Proportions(bool left)
        {
            float Len(int i) { var f = FingerBones(left, i); return Vector3.Distance(f.A.position, f.B.position) + Vector3.Distance(f.B.position, f.C.position) + Vector3.Distance(f.C.position, Tip(f.C, f.B)); }
            float m = Len(1);
            return $"index {Len(0) / m:0.00} ring {Len(2) / m:0.00} little {Len(3) / m:0.00} (photo .93 .91 .76)";
        }
    }

    /// <summary>Renders each hold close up from both sides, draws the measured joints on top, and writes every measurement.</summary>
    public static class GripMapper
    {
        static void Line(Texture2D t, Vector2 a, Vector2 b, Color c)
        {
            int n = Mathf.CeilToInt((b - a).magnitude) + 1;
            for (int i = 0; i <= n; i++) { var p = Vector2.Lerp(a, b, i / (float)n); for (int x = -1; x <= 1; x++) for (int y = -1; y <= 1; y++) t.SetPixel((int)p.x + x, (int)p.y + y, c); }
        }

        static int done, passed;
        public static void Reset() { done = passed = 0; }

        public static void Audit(Actor a, Stage st, Camera cam, RenderTexture rt, Texture2D read, string dir, string shot, int frame, StreamWriter log)
        {
            if (!a.Root.activeInHierarchy || !a.HasHold) return;
            foreach (var sm in a.Root.GetComponentsInChildren<SkinnedMeshRenderer>(true)) sm.updateWhenOffscreen = true;
            var vp = cam.WorldToViewportPoint(a.HoldCentre); if (vp.z < 0 || vp.x < -.2f || vp.x > 1.2f || vp.y < -.2f || vp.y > 1.2f) return;   // only people this shot can see
            foreach (var m in a.Measure())
            {
                bool ok = m.value >= m.low && m.value <= m.high; done++; if (ok) passed++;
                log.WriteLine($"{shot},{frame},{a.Root.name},{m.name},{m.value:0.0},{m.low:0.0},{m.high:0.0},{(ok ? "pass" : "FAIL")}");
            }
            log.WriteLine($"{shot},{frame},{a.Root.name},finger proportions right,\"{a.Proportions(false)}\",,,info");
            var lamp = new GameObject("map lamp").AddComponent<Light>(); lamp.type = LightType.Point; lamp.range = 4; lamp.intensity = 2.5f;
            foreach (var view in new[] { ("R", a.HoldRight), ("L", -a.HoldRight) })
            {
                var c = a.HoldCentre; var at = c + view.Item2 * .62f + Vector3.up * .05f;
                cam.transform.SetPositionAndRotation(at, Quaternion.LookRotation(c - at, Vector3.up)); cam.fieldOfView = 28; st.Focus(0);
                lamp.transform.position = at + Vector3.up * .25f;
                cam.Render();
                RenderTexture.active = rt; read.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); read.Apply(false); RenderTexture.active = null;
                string b = $"{shot}_{frame:00000}_{a.Root.name}_{view.Item1}";
                File.WriteAllBytes(Path.Combine(dir, b + ".jpg"), read.EncodeToJPG(95));
                foreach (bool left in new[] { false, true })
                    foreach (var l in a.HandLines(left))
                    {
                        Vector3 p = cam.WorldToScreenPoint(l.a), q = cam.WorldToScreenPoint(l.b);
                        if (p.z > 0 && q.z > 0) Line(read, p, q, left ? new Color(.2f, 1, 1) : new Color(1, .9f, .1f));
                    }
                read.Apply(false);
                File.WriteAllBytes(Path.Combine(dir, b + "_map.jpg"), read.EncodeToJPG(95));
            }
            UnityEngine.Object.Destroy(lamp.gameObject);
        }

        public static void Finish(StreamWriter log)
        {
            string s = $"[GripAudit] measured {done}, in tolerance {passed}, out {done - passed}";
            log.WriteLine(s); Debug.Log(s);
        }
    }
}
#endif
