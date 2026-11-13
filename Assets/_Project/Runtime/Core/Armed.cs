// A gun in someone's hands (2026-11-10). The models in Resources/Weapons are used as they are; each comes with its own axes and spare parts, which are found here and set
// aside. Every frame, after the animation, the gun's grip is put in the right palm: a rifle points from there towards the left hand, and a pistol points along the hand.
// The clip leaves the left hand floating under a rifle's fore-end, so it is then closed on it (2026-11-12, owner): under the fore-end against the angled grip of the scar,
// round the upright grip of the ump. Fire flashes at the muzzle and is heard by the street (Danger).
using UnityEngine;

namespace Dad.Core
{
    [DefaultExecutionOrder(110)] // after the animation, PersonLife and Person's turn of the head
    public sealed class Armed : MonoBehaviour
    {
        public sealed class Kind
        {
            public string Name, File;
            /// <summary>Metres from the back of the gun to the muzzle, as the real thing.</summary>
            public float Length;
            public bool Pistol;
            /// <summary>Parts of the file that are not the gun in hand: spare magazines laid beside it.</summary>
            public string[] Spare = new string[0];
            /// <summary>Parts fitted past the muzzle of the real gun (a suppressor): drawn, but not in its length.</summary>
            public string[] Added = new string[0];
            /// <summary>The grip, as fractions: of the length back from the muzzle, and of the height down from the top.</summary>
            public float GripBack, GripDown;
            public string Idle, Walk, Fire;
            /// <summary>The part whose middle is the barrel's line (a suppressor), where the widest parts' middle is not: a light fitted to one side widens the gun.
            /// Null: the bore is found from the gun's extent.</summary>
            public string Bore;
            /// <summary>The sights' line over the bore's, in metres, measured on the model (HandShots.ForeEnds): an optic's middle, or the tip of the front sight's post.
            /// The eye goes on it. 0: not measured, and the eye goes 1 cm over the top of the gun.</summary>
            public float SightUp;
            /// <summary>Where the left palm closes on a rifle, measured on its model (HandShots.ForeEnds): metres forward of the grip along the bore, and up from the
            /// bore's line (negative is under it). ForeGrip: an upright grip, held round like the right hand holds its grip; otherwise under the fore-end, palm up.</summary>
            public float ForeAhead, ForeUp;
            public bool ForeGrip;
            /// <summary>Kilograms, loaded, as the real one: what it weighs when it falls.</summary>
            public float Mass;
        }

        public static readonly Kind Scar = new Kind { Name = "scar", File = "scar", Length = 0.89f, Mass = 3.6f, GripBack = 0.66f, GripDown = 0.68f, Idle = "rifle-aim", Walk = "rifle-walk", Fire = "rifle-fire",
            SightUp = 0.097f, // the front post's tip; the rear aperture is 3 cm lower on this model, so the eye looks along the post, just over the rear sight (9.0)
            ForeAhead = 0.35f, ForeUp = -0.06f }; // the palm against the front of the angled grip, under the rail (the grip's ramp runs 24-39 cm, the rail's underside 6 cm under the bore)
        public static readonly Kind Ump = new Kind { Name = "ump", File = "ump", Length = 0.69f, Mass = 2.5f, Spare = new[] { "polySurface726", "polySurface727" }, Added = new[] { "polySurface723_suppressor" }, GripBack = 0.70f, GripDown = 0.58f, Idle = "rifle-aim", Walk = "rifle-walk", Fire = "rifle-fire",
            Bore = "polySurface723_suppressor2", SightUp = 0.089f, // the red dot's lens is centred 8.9 cm over the suppressor's middle, on the same line across
            ForeAhead = 0.29f, ForeUp = -0.085f, ForeGrip = true }; // round the upright grip, halfway down it (3.5 to 12.7 cm under the bore, 28-30 cm ahead)
        public static readonly Kind Glock = new Kind { Name = "glock", File = "glock/scene", Length = 0.213f, Mass = 0.9f, Pistol = true, Spare = new[] { "mag_full.001", "mag_empty" }, GripBack = 0.93f, GripDown = 0.5f, Idle = "pistol-idle", Walk = "pistol-walk", Fire = "pistol-fire" };
        public static Kind Of(string name) => name == "scar" ? Scar : name == "ump" ? Ump : name == "glock" ? Glock : null;

        public Kind Gun { get; private set; }
        public Transform Held { get; private set; }
        Animator body;
        Quaternion fromModel; // the model's own axes to the holder's (forward along the barrel, up)
        Vector3 grip, muzzle, sight, fore; // in the holder's frame, before scaling; sight is where the right eye goes on the shouldered gun, fore where the left palm closes
        Light glow;
        Material spark;
        MeshRenderer star;
        float flashUntil;
        bool? yours;
        HumanPoseHandler poser;
        HumanPose pose;
        int[] rightFingers, leftFingers, leftThumb, triggerFinger;
        Grasp grasp;

        /// <summary>Puts a gun in this person's hands. Null if the model is not there.</summary>
        public static Armed Give(Person p, string gun)
        {
            var kind = Of(gun);
            var asset = kind != null ? Resources.Load<GameObject>("Weapons/" + kind.File) : null;
            if (asset == null) { Debug.LogError("[Dad] no gun " + gun); return null; }
            var a = p.gameObject.AddComponent<Armed>();
            a.Gun = kind;
            a.body = p.Body;
            a.Build(asset);
            a.poser = new HumanPoseHandler(p.Body.avatar, p.Body.transform);
            a.rightFingers = Muscles("Right", "Middle", "Ring", "Little", "Thumb");
            a.triggerFinger = Muscles("Right", "Index");
            a.leftFingers = Muscles("Left", "Index", "Middle", "Ring", "Little");
            a.leftThumb = Muscles("Left", "Thumb");
            EyesOf(p.Body); // the eye is read from the eye mesh now, while the gun is being made, and not on a frame of play
            a.grasp = p.Body.GetComponent<Grasp>(); // a protagonist's fingers close on the gun by touch, not by a set amount
            if (a.grasp != null) a.grasp.Hold(a.Held);
            return a;
        }

        void Build(GameObject asset)
        {
            var holder = new GameObject(Gun.Name).transform;
            var model = Instantiate(asset, holder);
            model.transform.localPosition = asset.transform.position;
            model.transform.localRotation = asset.transform.rotation;
            model.transform.localScale = asset.transform.localScale;
            foreach (var t in model.GetComponentsInChildren<Transform>(true))
                foreach (var s in Gun.Spare) if (t.name.StartsWith(s, System.StringComparison.Ordinal)) t.gameObject.SetActive(false);
            // the barrel's way, in the file's frame (the holder is at rest, so world and holder agree)
            Vector3 forward, up = Vector3.up;
            Bounds Part(string name) { foreach (var r in model.GetComponentsInChildren<Renderer>()) if (r.name.Contains(name)) return r.bounds; return default; }
            if (Gun == Ump) // the file has it tilted: its suppressor's long axis is the bore
            {
                var s = Part("suppressor2");
                float thick = Mathf.Min(s.size.x, Mathf.Min(s.size.y, s.size.z));
                forward = new Vector3(0, s.size.y - thick, s.size.z - thick).normalized;
            }
            else if (Gun == Glock) forward = Snap(Part("valkyrie_0").center - Part("slide").center);
            else forward = Vector3.back; // the rifle lies along z, its muzzle towards -z
            up = Vector3.ProjectOnPlane(Vector3.up, forward).normalized;
            fromModel = Quaternion.Inverse(Quaternion.LookRotation(forward, up));
            // the gun's extent along the bore and up from it, every part in hand; its length as the real one is measured without what is fitted past its muzzle
            var rot = fromModel;
            Extent(model.transform, Matrix4x4.Rotate(rot), null, out var lo, out var hi);
            Extent(model.transform, Matrix4x4.Rotate(rot), Gun.Added, out var bodyLo, out var bodyHi);
            float length = hi.z - lo.z, height = hi.y - lo.y, scale = Gun.Length / Mathf.Max(1e-4f, bodyHi.z - bodyLo.z);
            // the bore: its middle across and its height, from the barrel's own part where one is named, else from the gun's extent
            float middle = (lo.x + hi.x) / 2, bore = hi.y - height * (Gun.Pistol ? 0.2f : 0.3f);
            if (Gun.Bore != null)
            {
                Extent(model.transform, Matrix4x4.Rotate(rot), null, out var barrelLo, out var barrelHi, Gun.Bore);
                if (barrelLo.x <= barrelHi.x) { middle = (barrelLo.x + barrelHi.x) / 2; bore = (barrelLo.y + barrelHi.y) / 2; }
                else Debug.LogError("[Dad] no part " + Gun.Bore + " in " + Gun.Name);
            }
            // the model is turned into the holder's frame: z along the bore, y up
            var pivot = new GameObject("model").transform;
            pivot.SetParent(holder, false);
            model.transform.SetParent(pivot, true); // the pivot is at rest, so the model keeps its pose in the file's frame
            pivot.localRotation = rot; // then the file's frame is turned so the bore is the holder's z
            grip = new Vector3(middle, hi.y - height * Gun.GripDown, hi.z - length * Gun.GripBack);
            muzzle = new Vector3(middle, bore, hi.z);
            holder.localScale = Vector3.one * scale;
            holder.SetParent(transform, true);
            // the eye 17 cm forward of the butt, on the sights' line; where that was not measured, 1 cm over the top of the gun with the gun 1.5 cm to the right of the eye
            sight = Gun.SightUp > 0 ? new Vector3(middle, bore + Gun.SightUp / scale, lo.z + 0.17f / scale)
                : new Vector3(middle - 0.015f / scale, hi.y + 0.01f / scale, lo.z + 0.17f / scale);
            fore = new Vector3(middle, bore + Gun.ForeUp / scale, grip.z + Gun.ForeAhead / scale);
            Held = holder;
            var flash = new GameObject("flash");
            flash.transform.SetParent(holder, false);
            flash.transform.localPosition = muzzle + Vector3.forward * 0.02f / holder.localScale.x;
            glow = flash.AddComponent<Light>();
            glow.type = LightType.Point; glow.range = 4; glow.intensity = 0; glow.color = new Color(1, 0.72f, 0.4f); glow.shadows = LightShadows.None;
            // the star the flash is seen as: four corners at the muzzle, spread to face the camera by the shader
            var quad = new Mesh { name = "flash" };
            quad.vertices = new Vector3[4];
            quad.uv = new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(1, 1), new Vector2(-1, 1) };
            quad.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            quad.bounds = new Bounds(Vector3.zero, Vector3.one * 2 / holder.localScale.x);
            flash.AddComponent<MeshFilter>().sharedMesh = quad;
            star = flash.AddComponent<MeshRenderer>();
            star.sharedMaterial = spark = new Material(Resources.Load<Material>("Materials/Flash"));
            spark.SetFloat("_Size", Gun.Pistol ? 0.09f : 0.14f);
            star.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            star.enabled = false;
        }

        /// <summary>The box, in a frame, round every drawn part under a root, from the parts' points, less the parts whose names start with one of skip.</summary>
        public static void Extent(Transform root, Matrix4x4 frame, string[] skip, out Vector3 lo, out Vector3 hi, string only = null)
        {
            lo = Vector3.one * 1e9f; hi = -lo;
            foreach (var f in root.GetComponentsInChildren<MeshFilter>())
            {
                var mesh = f.sharedMesh;
                var shown = f.GetComponent<Renderer>();
                if (mesh == null || shown == null || !shown.enabled) continue;
                if (skip != null && System.Array.Exists(skip, k => f.name.StartsWith(k, System.StringComparison.Ordinal))) continue;
                if (only != null && !f.name.StartsWith(only, System.StringComparison.Ordinal)) continue;
                var m = frame * f.transform.localToWorldMatrix;
                if (mesh.isReadable)
                    foreach (var v in mesh.vertices) { var c = m.MultiplyPoint3x4(v); lo = Vector3.Min(lo, c); hi = Vector3.Max(hi, c); }
                else
                {
                    var b = mesh.bounds;
                    for (int i = 0; i < 8; i++)
                    {
                        var c = m.MultiplyPoint3x4(new Vector3(i % 2 == 0 ? b.min.x : b.max.x, i / 2 % 2 == 0 ? b.min.y : b.max.y, i / 4 == 0 ? b.min.z : b.max.z));
                        lo = Vector3.Min(lo, c); hi = Vector3.Max(hi, c);
                    }
                }
            }
        }

        static Vector3 Snap(Vector3 v)
        {
            var a = new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
            return a.x >= a.y && a.x >= a.z ? new Vector3(Mathf.Sign(v.x), 0, 0) : a.y >= a.z ? new Vector3(0, Mathf.Sign(v.y), 0) : new Vector3(0, 0, Mathf.Sign(v.z));
        }

        /// <summary>Where the muzzle is, in the scene.</summary>
        public Vector3 Muzzle => Held.TransformPoint(muzzle);
        /// <summary>Where the right palm should be, on the grip, in the scene.</summary>
        public Vector3 Grip => Held.TransformPoint(grip);
        /// <summary>Where the left palm closes on a rifle's fore-end, in the scene.</summary>
        public Vector3 Fore => Held.TransformPoint(fore);
        /// <summary>The palm of a hand, in the scene: halfway along it, between the wrist and the knuckle of the middle finger.</summary>
        public Vector3 PalmOf(bool right) => Palm(body, right);
        /// <summary>Where the right eye should be on a shouldered rifle, in the scene: on an optic's line, or just over iron sights, along the gun's top.</summary>
        public Vector3 SightLine => Held.TransformPoint(sight);

        /// <summary>One shot: a flash at the muzzle, heard by the street, and the gun kicks: up and back into the hands, then settling (Recoil).</summary>
        public void Fire()
        {
            kick += Gun.Pistol ? 1f : 0.55f;
            flashUntil = Time.time + 0.05f;
            spark.SetFloat("_Spin", Random.Range(0f, Mathf.PI));
            spark.SetFloat("_Bright", Random.Range(0.8f, 1.2f));
            yours ??= GetComponent<OnFoot>() != null;
            GunSound.Shot(Gun, Muzzle, yours.Value); // heard: close and loud in your own hands, from where they stand in anyone else's
            Danger.Report(Muzzle, Danger.Kind.Shot);
        }

        static Vector3 Palm(Animator a, bool right)
        {
            var hand = a.GetBoneTransform(right ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
            var middle = a.GetBoneTransform(right ? HumanBodyBones.RightMiddleProximal : HumanBodyBones.LeftMiddleProximal);
            if (middle == null) return hand.position;
            // the bones run through the back of the hand; what it holds is in front of the palm, inside the closed fingers
            var index = a.GetBoneTransform(right ? HumanBodyBones.RightIndexProximal : HumanBodyBones.LeftIndexProximal);
            var little = a.GetBoneTransform(right ? HumanBodyBones.RightLittleProximal : HumanBodyBones.LeftLittleProximal);
            var along = middle.position - hand.position;
            var palm = index != null && little != null ? Vector3.Cross(along, index.position - little.position).normalized * (right ? 1 : -1) : Vector3.zero;
            return Vector3.Lerp(hand.position, middle.position, 0.55f) + palm * InFront;
        }

        void OnDestroy() => poser?.Dispose();

        /// <summary>Lets go of the gun: it falls as a body of its own, in a box round its parts, with the real one's weight. Nothing is held or fired after.</summary>
        public void Drop()
        {
            if (Held == null) return;
            var gun = Held;
            Held = null;
            if (grasp != null) grasp.Release();
            gun.SetParent(null, true);
            Extent(gun, gun.worldToLocalMatrix, null, out var lo, out var hi);
            var box = gun.gameObject.AddComponent<BoxCollider>();
            box.center = (lo + hi) / 2;
            box.size = hi - lo;
            if (body != null) foreach (var part in body.GetComponentsInChildren<Collider>()) Physics.IgnoreCollision(box, part); // it falls from the hands, not off the chest it was held across
            var fall = gun.gameObject.AddComponent<Rigidbody>();
            fall.mass = Gun.Mass;
            fall.interpolation = RigidbodyInterpolation.Interpolate;
            fall.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; // thin and quick: it must not fall through the street
            glow.intensity = 0;
            star.enabled = false;
            enabled = false;
        }

        /// <summary>Puts the gun away: it and this go, and the hands are the person's own again.</summary>
        public void Stow()
        {
            if (grasp != null) grasp.Release();
            if (Held != null) Destroy(Held.gameObject);
            Held = null;
            Destroy(this);
        }

        /// <summary>The gun once dropped, lying where it fell; null while held.</summary>
        public Transform Dropped => Held == null && star != null ? star.transform.parent : null;

        static int[] Muscles(string side, params string[] fingers)
        {
            var found = new System.Collections.Generic.List<int>();
            foreach (var f in fingers)
                for (int joint = 1; joint <= 3; joint++)
                {
                    int i = System.Array.IndexOf(HumanTrait.MuscleName, $"{side} {f} {joint} Stretched");
                    if (i >= 0) found.Add(i);
                }
            return found.ToArray();
        }

        /// <summary>How far each finger closes round the gun, as a muscle (-1 is a fist): the right hand round the grip, its index finger on the trigger, the left round
        /// the fore-end of a rifle or over the right hand on a pistol. Under a rifle's fore-end the fingers close up its far side (Around) and the thumb lies along
        /// its near side (Along).</summary>
        public static float Closed = -0.75f, OnTrigger = -0.2f, Under = -0.55f, Around = -0.8f, Along = -0.1f;
        /// <summary>The fingers opened before Grasp closes them on the gun.</summary>
        public static float Open = 0.6f;
        /// <summary>Metres from the hand's bones to the middle of what it grips, towards the palm.</summary>
        public static float InFront = 0.02f;

        // The clip's fingers are the clip's: open, or round a gun of another size. They are closed round this one, in the muscles, before the arms are bent to it.
        void Close()
        {
            poser.GetHumanPose(ref pose);
            bool wrapLeft = grasp != null && !Gun.Pistol; // on a pistol the left hand cups the right, not the gun: it keeps the set curl
            foreach (var i in rightFingers) pose.muscles[i] = grasp != null ? Open : Closed;
            foreach (var i in triggerFinger) pose.muscles[i] = OnTrigger;
            float fingers = Gun.Pistol ? Under : Gun.ForeGrip ? Closed : Around, thumb = Gun.Pistol ? Under : Gun.ForeGrip ? Closed : Along;
            foreach (var i in leftFingers) pose.muscles[i] = wrapLeft ? Open : fingers;
            foreach (var i in leftThumb) pose.muscles[i] = wrapLeft ? Open : thumb;
            poser.SetHumanPose(ref pose);
        }

        /// <summary>A rifle at the shoulder, its sights under the right eye (true unless the clip carries it low, running).</summary>
        public bool Shouldered = true;
        /// <summary>Where the gun is aimed, in the scene; with none it points the way the person faces.</summary>
        public Vector3? Target;

        /// <summary>How far the gun is thrown by the shots just fired: 1 is a pistol shot's kick (some 6 degrees up, 3 cm back). It comes on fast and dies away.</summary>
        public float Recoil { get; private set; }
        float kick;

        /// <summary>Degrees the spine is bent towards the target now (up or down, and round): the chest follows the aim, the arms and the gun do the rest.</summary>
        public float LeanPitch { get; private set; }
        public float LeanYaw { get; private set; }
        float lean;
        /// <summary>The most of the aim the spine takes, and the most it bends.</summary>
        public const float LeanShare = 0.7f, LeanMost = 45;

        /// <summary>While true the arms are the clip's, as in a reload: the aim lets go and the gun stays in the right hand as it was held when it began.</summary>
        public bool Loose
        {
            get => loose;
            set
            {
                if (value && !loose && Held != null && body != null)
                {
                    var hand = body.GetBoneTransform(HumanBodyBones.RightHand);
                    looseAt = hand.InverseTransformPoint(Held.position);
                    looseTurn = Quaternion.Inverse(hand.rotation) * Held.rotation;
                }
                loose = value;
            }
        }
        bool loose;
        Vector3 looseAt;
        Quaternion looseTurn;

        void LateUpdate()
        {
            if (Held == null || body == null) return;
            float dt = Time.deltaTime;
            Recoil = Mathf.Lerp(Recoil, kick, 1 - Mathf.Exp(-dt * 70)); // up within a frame or two
            kick *= Mathf.Exp(-dt * 10); // and down over a third of a second
            bool lit = Time.time < flashUntil;
            glow.intensity = lit ? 2.5f : 0;
            star.enabled = lit;
            if (loose)
            {
                var hand = body.GetBoneTransform(HumanBodyBones.RightHand);
                Held.SetPositionAndRotation(hand.TransformPoint(looseAt), hand.rotation * looseTurn);
                lean = Mathf.MoveTowards(lean, 0, dt * 4);
                return;
            }
            Close(); // the fingers, through the muscles: the whole pose is written back, so it comes before anything else is bent
            Lean(dt);
            // where the clip's hands put the gun: the grip in the right palm, the barrel towards the left hand (a rifle) or along the right hand (a pistol)
            var right = Palm(body, true);
            Vector3 bore;
            if (Gun.Pistol)
            {
                var hand = body.GetBoneTransform(HumanBodyBones.RightHand);
                var index = body.GetBoneTransform(HumanBodyBones.RightIndexProximal);
                bore = index != null ? index.position - hand.position : hand.forward;
            }
            else bore = Palm(body, false) - right;
            if (bore.sqrMagnitude < 1e-6f) return;
            var rot = Quaternion.LookRotation(bore.normalized, Vector3.ProjectOnPlane(transform.up, bore.normalized));
            var pos = right - rot * Vector3.Scale(grip, Held.lossyScale);
            if (!Gun.Pistol && Shouldered) Shoulder(ref pos, ref rot);
            else if (Gun.Pistol) Point(ref pos, ref rot);
            Held.SetPositionAndRotation(pos, rot);
            if (!Gun.Pistol) Support();
            if (grasp != null) { grasp.GripAt = Grip; grasp.Wrap(true, !Gun.Pistol); }
        }

        // The chest turns towards the target, up or down and round, before the arms are bent to the gun: part of the aim (LeanShare), as far as a spine goes (LeanMost),
        // shared over the spine's bones. It comes on as a target is taken and goes when there is none.
        void Lean(float dt)
        {
            lean = Mathf.MoveTowards(lean, Target.HasValue ? 1 : 0, dt * 4);
            var chest = body.GetBoneTransform(HumanBodyBones.UpperChest) ?? body.GetBoneTransform(HumanBodyBones.Chest) ?? body.GetBoneTransform(HumanBodyBones.Spine);
            if (Target.HasValue && chest != null)
            {
                var to = Target.Value - chest.position;
                var flat = new Vector3(to.x, 0, to.z);
                var facing = new Vector3(transform.forward.x, 0, transform.forward.z);
                if (flat.sqrMagnitude > 1e-4f)
                {
                    LeanPitch = Mathf.Clamp(Mathf.Atan2(to.y, flat.magnitude) * Mathf.Rad2Deg * LeanShare, -LeanMost, LeanMost);
                    LeanYaw = Mathf.Clamp(Vector3.SignedAngle(facing, flat, Vector3.up) * LeanShare, -LeanMost, LeanMost);
                }
            }
            if (lean <= 0) { LeanPitch = LeanYaw = 0; return; }
            var spine = new[] { body.GetBoneTransform(HumanBodyBones.Spine), body.GetBoneTransform(HumanBodyBones.Chest), body.GetBoneTransform(HumanBodyBones.UpperChest) };
            int n = 0;
            foreach (var b in spine) if (b != null) n++;
            var side = Vector3.Cross(Vector3.up, new Vector3(transform.forward.x, 0, transform.forward.z)).normalized;
            float pitch = LeanPitch * lean / n, yaw = LeanYaw * lean / n;
            foreach (var b in spine)
            {
                if (b == null) continue;
                b.rotation = Quaternion.AngleAxis(yaw, Vector3.up) * Quaternion.AngleAxis(-pitch, side) * b.rotation;
                side = Quaternion.AngleAxis(yaw, Vector3.up) * side;
            }
        }

        // A pistol at arm's length is brought onto the target: turned about the right shoulder, as far as an arm swings from the clip's hold (70 degrees), the hands bent
        // to keep it as the clip held it. A shot throws the muzzle up and the gun back (Recoil).
        void Point(ref Vector3 pos, ref Quaternion rot)
        {
            var shoulder = body.GetBoneTransform(HumanBodyBones.RightUpperArm);
            if (shoulder == null) return;
            var from = pos; var was = rot;
            if (Target.HasValue)
            {
                var tip = pos + rot * Vector3.Scale(muzzle, Held.lossyScale);
                var want = Target.Value - tip;
                if (want.sqrMagnitude > 1e-4f)
                {
                    var turn = Quaternion.RotateTowards(Quaternion.identity, Quaternion.FromToRotation(rot * Vector3.forward, want.normalized), 70);
                    pos = shoulder.position + turn * (pos - shoulder.position);
                    rot = turn * rot;
                }
            }
            if (Recoil > 1e-3f)
            {
                rot = rot * Quaternion.Euler(-6f * Recoil, 0, 0);
                pos -= rot * Vector3.forward * 0.03f * Recoil;
            }
            if (pos == from && rot == was) return;
            Reach(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, from, was, pos, rot);
            Reach(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, from, was, pos, rot);
        }

        // The clip was made for another body, so its hands do not bring a rifle to this one's eye: the stock would go through the face. The rifle is put where it belongs,
        // the sights under the right eye and the stock at the cheek, pointing at the target, and each arm is bent to reach it, the hand held to the gun as the clip held it.
        void Shoulder(ref Vector3 pos, ref Quaternion rot)
        {
            var eye = EyeOf(body, transform);
            Vector3 aim = Target.HasValue ? Target.Value - eye : transform.forward;
            if (aim.sqrMagnitude < 1e-4f) aim = transform.forward;
            var raised = Quaternion.LookRotation(aim.normalized, Vector3.ProjectOnPlane(transform.up, aim.normalized));
            raised = raised * Quaternion.Euler(-3.5f * Recoil, 0, 0); // a shot lifts the muzzle about the cheek
            var s = Held.lossyScale.x;
            var raisedAt = eye - raised * (sight * s) - raised * Vector3.forward * 0.02f * Recoil; // and pushes it back into the shoulder
            Reach(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, pos, rot, raisedAt, raised);
            Reach(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, pos, rot, raisedAt, raised);
            pos = raisedAt; rot = raised;
        }

        // The left hand closed on the fore-end: turned to the hold (under the fore-end, palm up against the gun and a little back and in, the fingers across it and
        // forward; or round an upright grip, palm in, the fingers forward round its front) and the arm bent so the palm is at the hold. The clip's elbow plane is kept.
        void Support()
        {
            var hand = body.GetBoneTransform(HumanBodyBones.LeftHand);
            if (hand == null || body.GetBoneTransform(HumanBodyBones.LeftMiddleProximal) == null) return;
            Vector3 up = Held.up, right = Held.right, ahead = Held.forward, along, palm;
            if (Gun.ForeGrip) { palm = right; along = ahead; }
            else
            {
                palm = (up * 0.85f - ahead * 0.3f + right * 0.3f).normalized;
                along = Vector3.ProjectOnPlane(right * 0.85f + ahead * 0.5f, palm).normalized;
            }
            Turn(body, false, along, palm);
            var target = hand.position + (Fore - Palm(body, false));
            var upper = body.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            Bend(upper, body.GetBoneTransform(HumanBodyBones.LeftLowerArm), hand, target, -transform.up);
            // on an upright grip the elbow comes down under the gun, part of the way from where the clip has it out to the side, so the forearm meets the grip
            if (Gun.ForeGrip) Swing(upper, body.GetBoneTransform(HumanBodyBones.LeftLowerArm), hand, -transform.up, ElbowDown);
        }

        /// <summary>How far an elbow is swung down on an upright fore-grip: 0 leaves the clip's elbow, 1 puts it straight under the line from shoulder to hand.</summary>
        public static float ElbowDown = 0.5f;

        // Turns the upper arm about the line from the shoulder to the hand, so the elbow moves a share of the way towards a direction and the hand stays where it is.
        static void Swing(Transform upper, Transform lower, Transform hand, Vector3 towards, float share)
        {
            Vector3 shoulder = upper.position, axis = (hand.position - shoulder).normalized;
            var now = Vector3.ProjectOnPlane(lower.position - shoulder, axis);
            var want = Vector3.ProjectOnPlane(towards, axis);
            if (now.sqrMagnitude < 1e-8f || want.sqrMagnitude < 1e-8f) return;
            var keep = hand.rotation;
            upper.rotation = Quaternion.AngleAxis(Vector3.SignedAngle(now, want, axis) * share, axis) * upper.rotation;
            hand.rotation = keep;
        }

        /// <summary>Turns a hand so it points along a way (wrist to the middle knuckle) with its palm facing another, whatever the rig's own axes.</summary>
        static void Turn(Animator a, bool right, Vector3 along, Vector3 palm)
        {
            var hand = a.GetBoneTransform(right ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
            var middle = a.GetBoneTransform(right ? HumanBodyBones.RightMiddleProximal : HumanBodyBones.LeftMiddleProximal);
            var index = a.GetBoneTransform(right ? HumanBodyBones.RightIndexProximal : HumanBodyBones.LeftIndexProximal);
            var little = a.GetBoneTransform(right ? HumanBodyBones.RightLittleProximal : HumanBodyBones.LeftLittleProximal);
            if (middle == null || index == null || little == null) return;
            var nowAlong = middle.position - hand.position;
            var nowPalm = Vector3.Cross(nowAlong, index.position - little.position) * (right ? 1 : -1);
            hand.rotation = Quaternion.LookRotation(along, palm) * Quaternion.Inverse(Quaternion.LookRotation(nowAlong, nowPalm)) * hand.rotation;
        }

        /// <summary>The right eye: its bone; or, for a rig with none (the Mixamo rig the Blender people have), the middle of the right eyeball in the model's
        /// eye mesh, carried by the bone it is skinned to, so it follows the head as it tilts; or, with no eye mesh either, a guess on the head: 8 cm forward, 3 up and 3.2
        /// to the right. (Until 2026-11-12 the guess was used for everyone. In the rifle's stance, with the head turned along the gun, it sat 7 to 11 cm left of the
        /// real right eye, so the sights were raised in front of the nose.)</summary>
        public static Vector3 EyeOf(Animator body, Transform facing)
        {
            var eyeBone = body.GetBoneTransform(HumanBodyBones.RightEye);
            if (eyeBone != null) return eyeBone.position;
            var eyes = EyesOf(body);
            if (eyes.on != null)
            {
                Vector3 a = eyes.on.TransformPoint(eyes.a), b = eyes.on.TransformPoint(eyes.b);
                return Vector3.Dot(a - b, facing.right) > 0 ? a : b;
            }
            return body.GetBoneTransform(HumanBodyBones.Head).position + facing.forward * 0.08f + facing.up * 0.03f + facing.right * 0.032f;
        }

        // Each body's eyeballs, found once: the bone they are skinned to and the middle of each, in its frame (on is null where the model has no eye mesh to read).
        static readonly System.Collections.Generic.Dictionary<Animator, (Transform on, Vector3 a, Vector3 b)> eyeballs = new System.Collections.Generic.Dictionary<Animator, (Transform, Vector3, Vector3)>();

        static (Transform on, Vector3 a, Vector3 b) EyesOf(Animator body)
        {
            if (eyeballs.TryGetValue(body, out var known)) return known;
            if (eyeballs.Count > 512) // bodies come and go with the street
            {
                var gone = new System.Collections.Generic.List<Animator>();
                foreach (var k in eyeballs.Keys) if (k == null) gone.Add(k);
                foreach (var k in gone) eyeballs.Remove(k);
            }
            (Transform, Vector3, Vector3) found = (null, default, default);
            foreach (var r in body.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                // the eyes' own mesh: the Blender people's is MakeHuman's eye proxy, which comes out named high-poly; other models name theirs with "eye"
                string n = r.name.ToLowerInvariant();
                if (!(n == "high-poly" || n.Contains("eye") && !n.Contains("brow") && !n.Contains("lash"))) continue;
                var mesh = r.sharedMesh;
                if (mesh == null || !mesh.isReadable || r.bones.Length == 0) continue;
                var v = mesh.vertices;
                var weights = mesh.GetAllBoneWeights();
                var per = mesh.GetBonesPerVertex();
                var most = new int[r.bones.Length];
                for (int k = 0, at = 0; k < per.Length; at += per[k], k++)
                {
                    int best = -1; float w = 0;
                    for (int j = 0; j < per[k]; j++) if (weights[at + j].weight > w) { w = weights[at + j].weight; best = weights[at + j].boneIndex; }
                    if (best >= 0) most[best]++;
                }
                int bone = System.Array.IndexOf(most, Mathf.Max(most));
                if (r.bones[bone] == null) continue;
                // the two eyeballs lie apart across the face: split the mesh by its widest way, and take the middle of each half
                var bind = mesh.bindposes[bone];
                Vector3 lo = Vector3.one * 1e9f, hi = -lo;
                var local = new Vector3[v.Length];
                for (int k = 0; k < v.Length; k++) { local[k] = bind.MultiplyPoint3x4(v[k]); lo = Vector3.Min(lo, local[k]); hi = Vector3.Max(hi, local[k]); }
                var size = hi - lo;
                int axis = size.x >= size.y && size.x >= size.z ? 0 : size.y >= size.z ? 1 : 2;
                float cut = (lo[axis] + hi[axis]) / 2;
                Vector3 one = default, two = default; int ones = 0, twos = 0;
                foreach (var c in local) if (c[axis] < cut) { one += c; ones++; } else { two += c; twos++; }
                if (ones == 0 || twos == 0) continue;
                found = (r.bones[bone], one / ones, two / twos);
                break;
            }
            eyeballs[body] = found;
            return found;
        }

        // Moves a hand from where it held the gun at one pose to the same hold at another, bending the arm to get there (two bones, the elbow kept in the plane it was in).
        void Reach(HumanBodyBones upper, HumanBodyBones lower, HumanBodyBones end, Vector3 fromAt, Quaternion from, Vector3 toAt, Quaternion to)
        {
            Transform a = body.GetBoneTransform(upper), b = body.GetBoneTransform(lower), c = body.GetBoneTransform(end);
            if (a == null || b == null || c == null) return;
            var back = Quaternion.Inverse(from);
            var target = toAt + to * (back * (c.position - fromAt));
            var turn = to * back * c.rotation;
            Bend(a, b, c, target, -transform.up);
            c.rotation = turn;
        }

        /// <summary>Two bones (a, b) bent so that the end of the second (c) is at target, as near as they reach: the middle joint stays in the plane it bends in, or
        /// bends about straight, a joint straight as it is, about this axis. The end keeps its rotation in the world.</summary>
        public static void Bend(Transform a, Transform b, Transform c, Vector3 target, Vector3 straight)
        {
            var keep = c.rotation;
            Vector3 A = a.position, B = b.position, C = c.position;
            float ab = (B - A).magnitude, bc = (C - B).magnitude;
            float d = Mathf.Clamp((target - A).magnitude, Mathf.Abs(ab - bc) + 1e-3f, ab + bc - 1e-3f);
            float now = Vector3.Angle(A - B, C - B);
            float want = Mathf.Acos(Mathf.Clamp((ab * ab + bc * bc - d * d) / (2 * ab * bc), -1, 1)) * Mathf.Rad2Deg;
            var axis = Vector3.Cross(A - B, C - B);
            if (axis.sqrMagnitude < 1e-8f) axis = straight;
            b.rotation = Quaternion.AngleAxis(want - now, axis.normalized) * b.rotation;
            a.rotation = Quaternion.FromToRotation(c.position - A, target - A) * a.rotation;
            c.rotation = keep;
        }
    }
}
