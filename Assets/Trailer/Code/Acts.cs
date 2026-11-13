// The cinematic, set by set. Each act builds its stage once and then lists its shots.
#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Dad.Core;

namespace Dad.Trailer
{
    public sealed partial class Acts
    {
        public Stage Stage;
        public readonly List<Shot> Shots = new List<Shot>();
        readonly List<ParticleSystem> fx = new List<ParticleSystem>();
        readonly string act;
        int lastFrame = -1;

        public Acts(string act) { this.act = act; }

        public void Particles(int frame)
        {
            int n = lastFrame < 0 ? 90 : Mathf.Max(1, frame - lastFrame);
            lastFrame = frame;
            foreach (var p in fx) p.Simulate(n / 30f, true, false, false);
        }

        public IEnumerator Build()
        {
            Stage = new Stage("Act " + act);
            Stage.BuildCamera();
            switch (act)
            {
                case "scout": Scout(); break;
                case "1": Diner(); break;
                case "s": yield return Street(); break;
                case "w": Warehouse(); break;
                case "p": Penthouse(); break;
                case "g": Garage(); break;
                default: throw new ArgumentException(act);
            }
            yield return null;
        }

        static readonly Dictionary<HumanBodyBones, string> ElaraBones = new Dictionary<HumanBodyBones, string> {
            {HumanBodyBones.Hips,"hip"},{HumanBodyBones.LeftUpperLeg,"L_leg"},{HumanBodyBones.LeftLowerLeg,"L_knee"},{HumanBodyBones.LeftFoot,"L_ankle"},{HumanBodyBones.LeftToes,"L_foot"},
            {HumanBodyBones.RightUpperLeg,"R_leg"},{HumanBodyBones.RightLowerLeg,"R_knee"},{HumanBodyBones.RightFoot,"R_ankle"},{HumanBodyBones.RightToes,"R_foot"},
            {HumanBodyBones.Spine,"spine"},{HumanBodyBones.Chest,"chest"},{HumanBodyBones.Neck,"neck"},{HumanBodyBones.Head,"head"},{HumanBodyBones.Jaw,"jaw"},{HumanBodyBones.LeftEye,"L_eye"},{HumanBodyBones.RightEye,"R_eye"},
            {HumanBodyBones.LeftShoulder,"L_shoulder"},{HumanBodyBones.LeftUpperArm,"L_arm"},{HumanBodyBones.LeftLowerArm,"L_elbow"},{HumanBodyBones.LeftHand,"L_wrist"},
            {HumanBodyBones.RightShoulder,"R_shoulder"},{HumanBodyBones.RightUpperArm,"R_arm"},{HumanBodyBones.RightLowerArm,"R_elbow"},{HumanBodyBones.RightHand,"R_wrist"},
            {HumanBodyBones.LeftThumbProximal,"L_thumb1"},{HumanBodyBones.LeftThumbIntermediate,"L_thumb2"},{HumanBodyBones.LeftThumbDistal,"L_thumb3"},
            {HumanBodyBones.LeftIndexProximal,"L_point1"},{HumanBodyBones.LeftIndexIntermediate,"L_point2"},{HumanBodyBones.LeftIndexDistal,"L_point3"},
            {HumanBodyBones.LeftMiddleProximal,"L_middle1"},{HumanBodyBones.LeftMiddleIntermediate,"L_middle2"},{HumanBodyBones.LeftMiddleDistal,"L_middle3"},
            {HumanBodyBones.LeftRingProximal,"L_ring1"},{HumanBodyBones.LeftRingIntermediate,"L_ring2"},{HumanBodyBones.LeftRingDistal,"L_ring3"},
            {HumanBodyBones.LeftLittleProximal,"L_pink1"},{HumanBodyBones.LeftLittleIntermediate,"L_pink2"},{HumanBodyBones.LeftLittleDistal,"L_pink3"},
            {HumanBodyBones.RightThumbProximal,"R_thumb1"},{HumanBodyBones.RightThumbIntermediate,"R_thumb2"},{HumanBodyBones.RightThumbDistal,"R_thumb3"},
            {HumanBodyBones.RightIndexProximal,"R_point1"},{HumanBodyBones.RightIndexIntermediate,"R_point2"},{HumanBodyBones.RightIndexDistal,"R_point3"},
            {HumanBodyBones.RightMiddleProximal,"R_middle1"},{HumanBodyBones.RightMiddleIntermediate,"R_middle2"},{HumanBodyBones.RightMiddleDistal,"R_middle3"},
            {HumanBodyBones.RightRingProximal,"R_ring1"},{HumanBodyBones.RightRingIntermediate,"R_ring2"},{HumanBodyBones.RightRingDistal,"R_ring3"},
            {HumanBodyBones.RightLittleProximal,"R_pink1"},{HumanBodyBones.RightLittleIntermediate,"R_pink2"},{HumanBodyBones.RightLittleDistal,"R_pink3"},
        };

        static Dictionary<HumanBodyBones, string> Mixamo(string prefix = "")
        {
            var d = new Dictionary<HumanBodyBones, string>();
            void A(HumanBodyBones b, string n) => d[b] = prefix + n;
            A(HumanBodyBones.Hips, "Hips"); A(HumanBodyBones.Spine, "Spine"); A(HumanBodyBones.Chest, "Spine1"); A(HumanBodyBones.UpperChest, "Spine2"); A(HumanBodyBones.Neck, "Neck"); A(HumanBodyBones.Head, "Head");
            foreach (var (s, left) in new[] { ("Left", true), ("Right", false) })
            {
                HumanBodyBones B(string n) => (HumanBodyBones)Enum.Parse(typeof(HumanBodyBones), s + n);
                A(B("Shoulder"), s + "Shoulder"); A(B("UpperArm"), s + "Arm"); A(B("LowerArm"), s + "ForeArm"); A(B("Hand"), s + "Hand");
                A(B("UpperLeg"), s + "UpLeg"); A(B("LowerLeg"), s + "Leg"); A(B("Foot"), s + "Foot"); A(B("Toes"), s + "ToeBase");
                foreach (var (hb, m) in new[] { ("Thumb", "Thumb"), ("Index", "Index"), ("Middle", "Middle"), ("Ring", "Ring"), ("Little", "Pinky") })
                {
                    A(B(hb + "Proximal"), s + "Hand" + m + "1"); A(B(hb + "Intermediate"), s + "Hand" + m + "2"); A(B(hb + "Distal"), s + "Hand" + m + "3");
                }
            }
            return d;
        }

        /// <summary>Bones of RenderPeople's rigged scans.</summary>
        static Dictionary<HumanBodyBones, string> Scan()
        {
            var d = new Dictionary<HumanBodyBones, string> { {HumanBodyBones.Hips,"hip"},{HumanBodyBones.Spine,"spine_01"},{HumanBodyBones.Chest,"spine_02"},{HumanBodyBones.UpperChest,"spine_03"},{HumanBodyBones.Neck,"neck"},{HumanBodyBones.Head,"head"} };
            foreach (var (s, x) in new[] { ("Left", "_l"), ("Right", "_r") })
            {
                HumanBodyBones B(string n) => (HumanBodyBones)Enum.Parse(typeof(HumanBodyBones), s + n);
                d[B("Shoulder")] = "shoulder" + x; d[B("UpperArm")] = "upperarm" + x; d[B("LowerArm")] = "lowerarm" + x; d[B("Hand")] = "hand" + x;
                d[B("UpperLeg")] = "upperleg" + x; d[B("LowerLeg")] = "lowerleg" + x; d[B("Foot")] = "foot" + x; d[B("Toes")] = "ball" + x;
                foreach (var (f, n) in new[] { ("Thumb", "thumb"), ("Index", "index"), ("Middle", "middle"), ("Ring", "ring"), ("Little", "pinky") })
                { d[B(f + "Proximal")] = n + "_01" + x; d[B(f + "Intermediate")] = n + "_02" + x; d[B(f + "Distal")] = n + "_03" + x; }
            }
            return d;
        }

        static Dictionary<HumanBodyBones, string> Vance()
        {
            var d = new Dictionary<HumanBodyBones, string> {
                {HumanBodyBones.Hips,"root"},{HumanBodyBones.Spine,"spine05"},{HumanBodyBones.Chest,"spine03"},{HumanBodyBones.UpperChest,"spine01"},{HumanBodyBones.Neck,"neck01"},{HumanBodyBones.Head,"head"},{HumanBodyBones.Jaw,"jaw"} };
            foreach (var (s, x) in new[] { ("Left", "L"), ("Right", "R") })
            {
                HumanBodyBones B(string n) => (HumanBodyBones)Enum.Parse(typeof(HumanBodyBones), s + n);
                d[B("Shoulder")] = "shoulder01_" + x; d[B("UpperArm")] = "upperarm01_" + x; d[B("LowerArm")] = "lowerarm01_" + x; d[B("Hand")] = "wrist_" + x;
                d[B("UpperLeg")] = "upperleg01_" + x; d[B("LowerLeg")] = "lowerleg01_" + x; d[B("Foot")] = "foot_" + x; d[B("Toes")] = "toe3-1_" + x;
                foreach (var (hb, n) in new[] { ("Thumb", 1), ("Index", 2), ("Middle", 3), ("Ring", 4), ("Little", 5) })
                {
                    d[B(hb + "Proximal")] = $"finger{n}-1_{x}"; d[B(hb + "Intermediate")] = $"finger{n}-2_{x}"; d[B(hb + "Distal")] = $"finger{n}-3_{x}";
                }
            }
            return d;
        }

        /// <summary>Raises straight arms sideways: Humanoid retargeting expects a T-pose, and this rig was modelled with arms down.</summary>
        static void TPose(Transform model)
        {
            var named = new Dictionary<string, Transform>();
            foreach (var t in model.GetComponentsInChildren<Transform>(true)) if (!named.ContainsKey(t.name)) named[t.name] = t;
            void Aim(string bone, string child, Vector3 way) { var b = named[bone]; var dir = (named[child].position - b.position).normalized; b.rotation = Quaternion.FromToRotation(dir, way) * b.rotation; }
            foreach (var (x, sign) in new[] { ("L", 1f), ("R", -1f) })
            {
                var w = new Vector3(sign, 0, 0);
                Aim("upperarm01_" + x, "lowerarm01_" + x, w); Aim("lowerarm01_" + x, "wrist_" + x, w);
            }
        }

        void Env(Color ambient, Color reflect)
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = ambient;
            RenderSettings.fog = false;
            var cube = new Cubemap(16, TextureFormat.RGBA32, true);
            for (int f = 0; f < 6; f++) for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++) cube.SetPixel((CubemapFace)f, x, y, reflect);
            cube.Apply(true);
            RenderSettings.customReflectionTexture = cube;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.reflectionIntensity = 1;
        }

        void Scout()
        {
            Env(new Color(.55f, .55f, .6f), new Color(.5f, .5f, .55f));
            var s = Stage;
            s.Lamp(LightType.Directional, Vector3.up * 50, new Vector3(.3f, 0, .5f), Color.white, .8f, 50, 60, false);
            var tr = new GameObject("t").transform; tr.SetParent(s.Root);
            int frame = 0;
            var log = new System.Text.StringBuilder();
            void View(string name, Vector3 at, Vector3 look, float fov) { Shots.Add(new Shot(name, frame / 30.0, (frame + 1) / 30.0, (t, u) => { s.Cam.transform.position = at; s.Cam.transform.LookAt(look); s.Cam.fieldOfView = fov; })); frame++; }
            void Set(string path, float sc) { Shots.Add(new Shot("load", frame / 30.0, (frame + 1) / 30.0, (t, u) => { foreach (Transform k in tr) UnityEngine.Object.Destroy(k.gameObject); var g = s.Model(path, Vector3.zero, 0, sc, tr); s.Dress(g, path); log.AppendLine("== " + path); foreach (var r in g.GetComponentsInChildren<Renderer>(true)) log.AppendLine($"{r.name} c={r.bounds.center:F2} s={r.bounds.size:F2} m={r.sharedMaterial?.name}"); System.IO.File.WriteAllText("Evidence/local/scout-parts.txt", log.ToString()); s.Cam.transform.position = Vector3.up * 40; s.Cam.transform.LookAt(Vector3.zero); s.Cam.fieldOfView = 60; })); frame++; }
            Set("diner/bench_inner/booth02.dae", .0254f);
            View("d4", new Vector3(0, 2.7f, 0), new Vector3(0, 0, .01f), 120);
            Set("penthouse/Rectangle House.fbx", 1);
            View("p5", new Vector3(-10, 3.3f, 1), new Vector3(-10, 0, 1.01f), 110);
            View("p6", new Vector3(0, 3.3f, 1), new Vector3(0, 0, 1.01f), 110);
            Set("fastback/MuscleCSO_low.fbx", 1);
            View("c3", new Vector3(-2.2f, 1.0f, -.2f), new Vector3(0, .6f, -.2f), 50);
        }
    }
}
#endif
