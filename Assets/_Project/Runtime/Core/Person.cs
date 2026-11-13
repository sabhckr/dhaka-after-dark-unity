// Someone from the owner's rigged models, on their feet (E-01, E-02, 2026-11-06). The model and every clip are Humanoid, so one set of clips moves anyone. A small playable graph
// blends the clips by how fast the body is going: there is no animator controller to keep in step with the code. Only clips that exist in the files are played.
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Dad.Core
{
    public sealed class Person : MonoBehaviour
    {
        /// <summary>Metres a second the clips were made for, roughly: above these the feet would slide.</summary>
        public const float WalkSpeed = 1.5f, RunSpeed = 4.2f;
        /// <summary>Metres a second a foot on the ground travels under the body, per metre of head height (measured on five rigs, 2026-11-09): what a clip covers at its own pace.</summary>
        public const float RunPerHead = 2.56f;
        public static float PerHead(string gait) => gait == "text-walk" ? 0.50f : gait == "bag-walk" ? 0.59f : 0.97f;
        /// <summary>The same for the clips that run: the plain run, running away looking back, sprinting and jogging (measured as the walks, 2026-11-10).</summary>
        public static float RunPace(string clip) => clip == "flee" ? 1.05f : clip == "sprint" ? 3.19f : clip == "jog" ? 1f : RunPerHead;
        /// <summary>Which clip the body runs with.</summary>
        public string Running { get; private set; } = "run";
        /// <summary>Above zero (an NPC's head height in metres), the clip's pace follows the speed so the feet do not slide. Zero keeps the blend the player is built on.</summary>
        public float Stride;
        /// <summary>Someone to turn the head towards when close and in front.</summary>
        public Transform Watch;
        public string Gait = "walk";
        public float WalkAtOwnPace => Stride * PerHead(Gait);
        public const string Folder = "People/", ClipFolder = "People/Clips/";
        static readonly string[] Moves = { "idle", "walk", "run" };

        public Animator Body { get; private set; }
        public string Who { get; private set; }

        /// <summary>Metres a second: sets the blend of standing, walking and running.</summary>
        public float Speed;

        /// <summary>The hips, whichever bone the model calls them.</summary>
        public Transform Hips => Body.GetBoneTransform(HumanBodyBones.Hips);

        /// <summary>A clip played whole in place of standing, walking and running.</summary>
        public bool Acting { get; private set; }

        // The graph, from the clips up:
        //   three slots (standing, walking, running), each a pair of clips crossfading when Use swaps one (a gun drawn, a crouch);
        //   those blended by Speed into the moving body;
        //   two act slots crossfading into each other, and the acts as a whole crossfading over the moving body (Play, Stop);
        //   on top of it all, the upper body alone: a clip played once over the waist up (Upper: a reload, a flinch).
        PlayableGraph graph;
        AnimationMixerPlayable moving, acts, whole;
        AnimationLayerMixerPlayable layers;
        readonly AnimationMixerPlayable[] slots = new AnimationMixerPlayable[3];
        readonly int[] slotNow = new int[3];
        readonly float[] weight = { 1, 0, 0 };
        int actNow;
        float actWeight, actRate, slotRate = 1 / 0.25f;
        AnimationClipPlayable acted, upper;
        float upperAt = -1, upperLength, upperIn, upperOut, upperPeak;

        /// <summary>Seconds a clip takes to come in or go (Play, Stop) unless the caller says otherwise; a swapped stance (Use) takes a quarter of a second.</summary>
        public const float Fade = 0.2f;

        /// <summary>Degrees a second the body is turning on the spot: the feet step round with it.</summary>
        public float Turning;
        /// <summary>Walking or running backwards: the clips play in reverse (where there is no clip of stepping back).</summary>
        public bool Backward;
        /// <summary>Degrees from where the body faces to where it goes: 0 ahead, 90 to the right, 180 back, -90 to the left. With Directional, walking and running blend
        /// the clips of stepping that way.</summary>
        public float Going;

        // Walking and running every way (2026-11-12): a slot's clip is then four, ahead, to the right, back and to the left, blended by Going and kept in step. Ways names
        // the clips beside the one ahead ("walk-left", "rifle-walk-back"); where one is missing, stepping back is the clip ahead in reverse, and a side is the clip ahead.
        static readonly string[] Ways = { "", "-right", "-back", "-left" };
        sealed class Way
        {
            public AnimationMixerPlayable Mix;
            public readonly AnimationClipPlayable[] Clip = new AnimationClipPlayable[4];
            public readonly float[] Length = new float[4];
            public readonly bool[] Reverse = new bool[4];
            /// <summary>How far through a step cycle, 0 to 1, the same for all four.</summary>
            public double Phase;
        }
        readonly Way[,] ways = new Way[3, 2];
        readonly float[] wayWeight = { 1, 0, 0, 0 };
        float goingNow;
        bool directional;

        /// <summary>Walks and runs with the clips of stepping sideways and back where there are some (the player); otherwise one clip, turned to face the way it goes.</summary>
        public bool Directional
        {
            get => directional;
            set
            {
                if (value == directional) return;
                directional = value;
                for (int m = 1; m < 3; m++) { var c = inUse[m]; inUse[m] = null; Use(m, c); }
            }
        }

        /// <summary>Loads a person from Resources/People, standing at the position and facing the heading in degrees. Null if the file is not there (Tools/models/people.py brings them in).</summary>
        public static Person Spawn(string who, Vector3 at, float heading, string worn = null, string gait = null)
        {
            var model = Resources.Load<GameObject>(Folder + who);
            if (model == null) { Debug.LogError("[Dad] no person " + who + ": run Tools/models/people.py"); return null; }
            var root = new GameObject(who);
            root.transform.SetPositionAndRotation(at, Quaternion.Euler(0, heading, 0));
            var figure = Instantiate(model, root.transform);
            figure.name = "Figure";
            figure.transform.localPosition = Vector3.zero;
            figure.transform.localRotation = Quaternion.identity;
            var p = root.AddComponent<Person>();
            p.Who = who;
            if (!string.IsNullOrEmpty(gait)) p.Gait = gait;
            p.Body = figure.GetComponent<Animator>();
            if (p.Body == null || !p.Body.isHuman) { Debug.LogError("[Dad] " + who + " is not Humanoid"); Destroy(root); return null; }
            p.Body.applyRootMotion = false;
            p.Body.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            if (worn != null) p.Strap(figure, worn);
            p.Dress(figure);
            Details(figure);
            p.Build();
            p.gaze = p.Body.GetComponent<Gaze>() != null;
            if (p.Body.GetComponent<Face>() == null) PersonLife.Give(p.Body); // blinks, speech and swinging hair, where the model has a face's shapes and hair (the player too: he blinks, and talks only if told)
            return p;
        }

        // Brows, lashes and teeth: modelled hair by hair (the brows and lashes alone are about 50,000 triangles) and too small to see beyond a few metres.
        // They are left out once the person is shorter than a seventh of the screen, about 9 m away at the street camera; nothing else is touched.
        static void Details(GameObject figure)
        {
            var small = new System.Collections.Generic.List<Renderer>();
            foreach (var r in figure.GetComponentsInChildren<Renderer>(true))
                if (r.name.Contains("eyebrows") || r.name.Contains("eyelashes") || r.name.StartsWith("teeth")) small.Add(r);
            if (small.Count == 0 || figure.GetComponent<LODGroup>() != null) return;
            var lod = figure.AddComponent<LODGroup>();
            lod.SetLODs(new[] { new LOD(DetailHeight, small.ToArray()) });
            lod.RecalculateBounds();
        }

        public const float DetailHeight = 0.14f;

        // Parts no bone holds: those worn go with the chest, from where they are at rest; the others were left in the air by the model's render.
        void Strap(GameObject figure, string worn)
        {
            var chest = Body.GetBoneTransform(HumanBodyBones.UpperChest);
            if (chest == null) chest = Body.GetBoneTransform(HumanBodyBones.Chest);
            var names = worn.Split(',');
            foreach (var r in figure.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (chest != null && System.Array.Exists(names, n => n.Trim() == r.name)) r.transform.SetParent(chest, true);
                else r.gameObject.SetActive(false);
            }
            foreach (var r in figure.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (System.Array.Exists(names, n => n.Trim() == "-" + r.name)) r.gameObject.SetActive(false); // "-part": a part the roster takes off
        }

        // Mixamo's files do not always carry their pictures, so each material takes the picture named after it, where there is one.
        void Dress(GameObject figure)
        {
            foreach (var r in figure.GetComponentsInChildren<Renderer>(true))
            {
                if (!r.gameObject.activeInHierarchy) continue;
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;
                    var picture = Map(Who, r.name, mats[i].name);
                    if (picture == null) picture = Map(Who, r.name, mats[i].name, "_color");
                    if (picture == null) continue;
                    var m = new Material(mats[i]);
                    if (m.HasProperty(BaseMap)) m.SetTexture(BaseMap, picture);
                    if (m.HasProperty(BaseColor)) m.SetColor(BaseColor, Color.white);
                    var relief = Map(Who, r.name, mats[i].name, "_normal");
                    if (relief == null) relief = Map(Who, r.name, mats[i].name, "_normaldx");
                    if (relief != null && m.HasProperty(BumpMap))
                    {
                        m.SetTexture(BumpMap, relief);
                        m.EnableKeyword("_NORMALMAP");
                    }
                    mats[i] = m;
                }
                r.sharedMaterials = mats;
            }
        }

        static readonly int BaseMap = Shader.PropertyToID("_BaseMap"), BaseColor = Shader.PropertyToID("_BaseColor"), BumpMap = Shader.PropertyToID("_BumpMap");

        /// <summary>A map of the model's, by what it is for ("", "_normal", "_opacity"). Materials can share a name across the body, so the part's own comes first, then the
        /// model's, then the wardrobe's: the maps of the people made in Blender (2026-11-12), kept once for everyone who wears the same skin, shirt or hair.</summary>
        public static Texture2D Map(string who, string part, string material, string kind = "")
        {
            var own = Resources.Load<Texture2D>(Folder + who + "/" + Safe(part) + "_" + Safe(material) + kind);
            if (own == null) own = Resources.Load<Texture2D>(Folder + who + "/" + Safe(material) + kind);
            return own != null ? own : Resources.Load<Texture2D>(Folder + Wardrobe + Safe(material) + kind);
        }

        /// <summary>The shared maps' folder under People.</summary>
        public const string Wardrobe = "Wardrobe/";

        static string Safe(string name)
        {
            var c = name.ToCharArray();
            for (int i = 0; i < c.Length; i++) if (!char.IsLetterOrDigit(c[i]) && c[i] != '_' && c[i] != '-') c[i] = '_';
            return new string(c);
        }

        static AvatarMask waistUp;
        /// <summary>The spine, head, arms and fingers: what Upper moves. Not the hips, legs or feet, so the legs go on walking under it.</summary>
        static AvatarMask WaistUp
        {
            get
            {
                if (waistUp != null) return waistUp;
                waistUp = new AvatarMask { name = "Waist up" };
                for (var part = AvatarMaskBodyPart.Root; part < AvatarMaskBodyPart.LastBodyPart; part++)
                    waistUp.SetHumanoidBodyPartActive(part, part == AvatarMaskBodyPart.Body || part == AvatarMaskBodyPart.Head || part == AvatarMaskBodyPart.LeftArm || part == AvatarMaskBodyPart.RightArm
                        || part == AvatarMaskBodyPart.LeftFingers || part == AvatarMaskBodyPart.RightFingers);
                return waistUp;
            }
        }

        static AvatarMask armsOnly;
        /// <summary>The arms and fingers alone: an Upper clip that must not lift the back or head (a reload crouched behind cover).</summary>
        static AvatarMask ArmsOnly
        {
            get
            {
                if (armsOnly != null) return armsOnly;
                armsOnly = new AvatarMask { name = "Arms" };
                for (var part = AvatarMaskBodyPart.Root; part < AvatarMaskBodyPart.LastBodyPart; part++)
                    armsOnly.SetHumanoidBodyPartActive(part, part == AvatarMaskBodyPart.LeftArm || part == AvatarMaskBodyPart.RightArm || part == AvatarMaskBodyPart.LeftFingers || part == AvatarMaskBodyPart.RightFingers);
                return armsOnly;
            }
        }

        void Build()
        {
            graph = PlayableGraph.Create(Who);
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            moving = AnimationMixerPlayable.Create(graph, Moves.Length);
            for (int i = 0; i < Moves.Length; i++)
            {
                slots[i] = AnimationMixerPlayable.Create(graph, 2);
                graph.Connect(slots[i], 0, moving, i);
                var clip = Resources.Load<AnimationClip>(ClipFolder + (i == 1 ? Gait : Moves[i]));
                if (clip == null) { Debug.LogError("[Dad] no clip " + Moves[i]); continue; }
                graph.Connect(AnimationClipPlayable.Create(graph, clip), 0, slots[i], 0);
                slots[i].SetInputWeight(0, 1);
                inUse[i] = i == 1 ? Gait : Moves[i];
            }
            moving.SetInputWeight(0, 1);
            acts = AnimationMixerPlayable.Create(graph, 2);
            whole = AnimationMixerPlayable.Create(graph, 2);
            graph.Connect(moving, 0, whole, 0);
            graph.Connect(acts, 0, whole, 1);
            whole.SetInputWeight(0, 1);
            layers = AnimationLayerMixerPlayable.Create(graph, 2);
            graph.Connect(whole, 0, layers, 0);
            layers.SetInputWeight(0, 1);
            layers.SetLayerMaskFromAvatarMask(1u, WaistUp);
            AnimationPlayableOutput.Create(graph, "Body", Body).SetSourcePlayable(layers);
            graph.Play();
        }

        // Puts a clip in an input of a mixer, in place of whatever was there.
        void Put(AnimationMixerPlayable into, int input, Playable clip)
        {
            var old = into.GetInput(input);
            if (old.IsValid()) { graph.Disconnect(into, input); graph.DestroySubgraph(old); }
            graph.Connect(clip, 0, into, input);
        }

        /// <summary>Plays a clip whole, from its start or this far in and at a speed, in place of moving by Speed, coming in over fade seconds (from what was moving the body, or
        /// from the clip played before). Says how long the clip is. Stop goes back to moving by Speed.</summary>
        public float Play(string clip, float fade = Fade, float from = 0, float speed = 1)
        {
            var c = Resources.Load<AnimationClip>(ClipFolder + clip);
            if (c == null) { Debug.LogError("[Dad] no clip " + clip); return 0; }
            bool showing = Acting || actWeight > 0;
            int into = showing && fade > 0 ? 1 - actNow : actNow; // a new act crossfades from the one showing; with no fade it takes its place
            acted = AnimationClipPlayable.Create(graph, c);
            acted.SetTime(from);
            acted.SetSpeed(speed);
            Put(acts, into, acted);
            actNow = into;
            if (fade <= 0)
            {
                var other = acts.GetInput(1 - into);
                if (other.IsValid()) { graph.Disconnect(acts, 1 - into); other.Destroy(); }
                acts.SetInputWeight(into, 1);
                acts.SetInputWeight(1 - into, 0);
                actWeight = 1;
                ApplyAct();
            }
            else if (!showing) { acts.SetInputWeight(into, 1); acts.SetInputWeight(1 - into, 0); }
            actRate = fade > 0 ? 1 / fade : float.PositiveInfinity;
            Acting = true;
            return c.length;
        }

        /// <summary>Back to moving by Speed, the clip played going over fade seconds.</summary>
        public void Stop(float fade = 0.25f)
        {
            if (!Acting) return;
            Acting = false;
            actRate = fade > 0 ? 1 / fade : float.PositiveInfinity;
            if (fade <= 0) { actWeight = 0; ApplyAct(); }
        }

        void ApplyAct()
        {
            whole.SetInputWeight(0, 1 - actWeight);
            whole.SetInputWeight(1, actWeight);
        }

        /// <summary>Plays a clip once over the waist up only (or the arms only), the legs going on as they were: in over fadeIn seconds, at most this much of the pose (peak),
        /// out over fadeOut at its end, at a speed. Says how long it takes. A second one replaces the first.</summary>
        public float Upper(string clip, float fadeIn = 0.12f, float fadeOut = 0.25f, float speed = 1, float peak = 1, bool arms = false)
        {
            layers.SetLayerMaskFromAvatarMask(1u, arms ? ArmsOnly : WaistUp);
            var c = Resources.Load<AnimationClip>(ClipFolder + clip);
            if (c == null || speed <= 0) { Debug.LogError("[Dad] no clip " + clip); return 0; }
            var old = layers.GetInput(1);
            if (old.IsValid()) { graph.Disconnect(layers, 1); old.Destroy(); }
            upper = AnimationClipPlayable.Create(graph, c);
            upper.SetSpeed(speed);
            graph.Connect(upper, 0, layers, 1);
            layers.SetInputWeight(1, 0);
            upperAt = Time.time;
            upperLength = c.length / speed;
            upperIn = Mathf.Max(1e-3f, fadeIn);
            upperOut = Mathf.Max(1e-3f, fadeOut);
            upperPeak = Mathf.Clamp01(peak);
            return upperLength;
        }

        /// <summary>How much of the body the acts hold now (Play in, Stop out), 0 to 1.</summary>
        public float ActWeight => actWeight;

        /// <summary>How much of the body an Upper clip holds now, 0 to its peak.</summary>
        public float UpperWeight => upperAt >= 0 && layers.IsValid() ? layers.GetInputWeight(1) : 0;

        /// <summary>A shot struck: the upper body jerks from the blow and comes back (the head-hit clip, part way, quickly).</summary>
        public void Flinch() => Upper("head-hit", 0.05f, 0.3f, 1.6f, 0.65f);

        /// <summary>Runs with another clip from now on: fleeing, sprinting, jogging, or the plain run again.</summary>
        public void RunWith(string clip)
        {
            if (clip == Running) return;
            if (Use(2, clip)) Running = clip;
        }

        readonly string[] inUse = new string[3];

        /// <summary>Stands (0), walks (1) or runs (2) with another clip from now on, crossfading over a quarter second: a gun's stance, crouching, or null for the person's own again.</summary>
        public bool Use(int move, string clip)
        {
            clip ??= move == 0 ? Moves[0] : move == 1 ? Gait : Running;
            if (inUse[move] == clip) return true;
            var c = Resources.Load<AnimationClip>(ClipFolder + clip);
            if (c == null) { Debug.LogError("[Dad] no clip " + clip); return false; }
            var slot = slots[move];
            int into = 1 - slotNow[move];
            var now = slot.GetInput(slotNow[move]);
            var was = ways[move, slotNow[move]];
            // in step with the one going, so the feet do not jump
            double phase = was != null ? was.Phase : 0;
            if (was == null && now.IsValid() && now.GetPlayableType() == typeof(AnimationClipPlayable))
            {
                var going = ((AnimationClipPlayable)now).GetAnimationClip();
                if (going != null && going.length > 0) phase = Frac(now.GetTime() / going.length);
            }
            var way = move > 0 && directional ? Make(clip, c) : null;
            Playable fresh;
            if (way != null) { way.Phase = phase; Step(way, 0, 0); fresh = way.Mix; }
            else
            {
                var one = AnimationClipPlayable.Create(graph, c);
                if (now.IsValid()) { one.SetTime(phase * c.length); one.SetSpeed(was != null ? 1 : now.GetSpeed()); }
                fresh = one;
            }
            Put(slot, into, fresh);
            ways[move, into] = way;
            slotNow[move] = into;
            if (!now.IsValid()) { slot.SetInputWeight(into, 1); slot.SetInputWeight(1 - into, 0); }
            inUse[move] = clip;
            return true;
        }

        static double Frac(double x) => x - System.Math.Floor(x);

        static AnimationClip Quiet(string clip) => Resources.Load<AnimationClip>(ClipFolder + clip);

        // The four clips of a slot that walks or runs every way, or null where there is none but the clip ahead.
        Way Make(string name, AnimationClip ahead)
        {
            var found = new AnimationClip[4];
            found[0] = ahead;
            int own = 0;
            for (int d = 1; d < 4; d++)
            {
                found[d] = Quiet(name + Ways[d]);
                if (found[d] == null && name == "rifle-run" && d != 2) found[d] = Quiet("rifle-walk" + Ways[d]); // no running sideways with a rifle: its walk, stepped quicker
                if (found[d] != null) own++;
            }
            if (own == 0) return null;
            var w = new Way { Mix = AnimationMixerPlayable.Create(graph, 4) };
            for (int d = 0; d < 4; d++)
            {
                var c = found[d] ?? ahead;
                w.Reverse[d] = found[d] == null && d == 2;
                w.Length[d] = Mathf.Max(0.05f, c.length);
                w.Clip[d] = AnimationClipPlayable.Create(graph, c);
                w.Clip[d].SetSpeed(0); // their time is set by Step, all four in step
                graph.Connect(w.Clip[d], 0, w.Mix, d);
            }
            return w;
        }

        // The way's blend by Going, and its clips moved on together: one cycle's length is the blend of theirs, so the feet stay in step whichever way the body goes.
        void Step(Way w, float dt, float pace)
        {
            float length = 0;
            for (int d = 0; d < 4; d++) { w.Mix.SetInputWeight(d, wayWeight[d]); length += wayWeight[d] * w.Length[d]; }
            w.Phase = Frac(w.Phase + dt * pace / Mathf.Max(0.05f, length));
            for (int d = 0; d < 4; d++) w.Clip[d].SetTime((w.Reverse[d] ? 1 - w.Phase : w.Phase) * w.Length[d]);
        }

        // Going as weights of ahead, right, back and left: the two ways either side of it, by how near each is.
        void Weigh(float going)
        {
            float a = Mathf.Repeat(going, 360) / 90;
            int from = Mathf.FloorToInt(a) % 4;
            float t = a - Mathf.Floor(a);
            for (int d = 0; d < 4; d++) wayWeight[d] = 0;
            wayWeight[from] = 1 - t;
            wayWeight[(from + 1) % 4] += t;
        }

        /// <summary>How much each way of stepping shows now: ahead, right, back, left.</summary>
        public float WayWeight(int way) => wayWeight[way];

        /// <summary>Whether the walk (1) or run (2) is blended from clips of stepping every way.</summary>
        public bool Ways4(int move) => ways[move, slotNow[move]] != null;

        /// <summary>Where the hips are at a time in the clip being played, in the frame of the person's root. The clip goes back to where it was.</summary>
        public Vector3 HipsAt(float time) => At(HumanBodyBones.Hips, time);

        /// <summary>Where a bone is at a time in the clip being played (that clip alone, whatever is fading), in the frame of the person's root. The clip and the blend go back to where they were.</summary>
        public Vector3 At(HumanBodyBones bone, float time)
        {
            double was = acted.GetTime();
            float w0 = whole.GetInputWeight(0), w1 = whole.GetInputWeight(1), a0 = acts.GetInputWeight(0), a1 = acts.GetInputWeight(1), up = layers.GetInputWeight(1);
            whole.SetInputWeight(0, 0); whole.SetInputWeight(1, 1);
            acts.SetInputWeight(actNow, 1); acts.SetInputWeight(1 - actNow, 0);
            layers.SetInputWeight(1, 0);
            acted.SetTime(time);
            graph.Evaluate(0);
            var at = transform.InverseTransformPoint(Body.GetBoneTransform(bone).position);
            acted.SetTime(was);
            whole.SetInputWeight(0, w0); whole.SetInputWeight(1, w1);
            acts.SetInputWeight(0, a0); acts.SetInputWeight(1, a1);
            layers.SetInputWeight(1, up);
            graph.Evaluate(0);
            return at;
        }

        // The clip each slot shows now.
        Playable Showing(int move) => slots[move].GetInput(slotNow[move]);

        /// <summary>Starts the clips this far in, so that a crowd does not step in time.</summary>
        public void Phase(double seconds)
        {
            for (int i = 0; i < Moves.Length; i++)
                for (int k = 0; k < 2; k++)
                {
                    var c = slots[i].GetInput(k);
                    if (!c.IsValid()) continue;
                    if (ways[i, k] != null) { ways[i, k].Phase = Frac(seconds / ways[i, k].Length[0]); Step(ways[i, k], 0, 0); }
                    else c.SetTime(seconds);
                }
        }

        // Every fade done at once: for a held picture.
        void Settle()
        {
            for (int i = 0; i < Moves.Length; i++)
            {
                slots[i].SetInputWeight(slotNow[i], 1);
                slots[i].SetInputWeight(1 - slotNow[i], 0);
            }
            acts.SetInputWeight(actNow, 1);
            acts.SetInputWeight(1 - actNow, 0);
            actWeight = Acting ? 1 : 0;
            ApplyAct();
            layers.SetInputWeight(1, 0);
        }

        /// <summary>Holds one frame for a picture: moving at speed, this far into the clips. Nothing moves afterwards.</summary>
        public void Pose(float speed, double seconds)
        {
            enabled = false;
            // a rig's own graph (Gaze) goes on evaluating every frame on top of a held one, and with nothing new under it puts the body back in its model pose
            var rig = Body.GetComponent<UnityEngine.Animations.Rigging.RigBuilder>();
            if (rig != null) rig.enabled = false;
            Settle();
            if (Acting)
            {
                acted.SetTime(seconds);
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                graph.Evaluate(0);
                return;
            }
            Blend(speed, out float walk, out float run);
            moving.SetInputWeight(0, 1 - walk);
            moving.SetInputWeight(1, walk * (1 - run));
            moving.SetInputWeight(2, walk * run);
            Phase(seconds);
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            graph.Evaluate(0);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            // stances swapped by Use: the new one in, the old one out, then gone
            for (int i = 0; i < Moves.Length; i++)
            {
                int now = slotNow[i];
                float w = Mathf.MoveTowards(slots[i].GetInputWeight(now), 1, slotRate * dt);
                slots[i].SetInputWeight(now, w);
                slots[i].SetInputWeight(1 - now, 1 - w);
                var gone = slots[i].GetInput(1 - now);
                if (w >= 1 && gone.IsValid()) { graph.Disconnect(slots[i], 1 - now); graph.DestroySubgraph(gone); ways[i, 1 - now] = null; }
            }
            // acts: the one playing in, the one before out; the acts as a whole in while acting, out after Stop
            float a = Mathf.MoveTowards(acts.GetInputWeight(actNow), 1, actRate * dt);
            acts.SetInputWeight(actNow, a);
            acts.SetInputWeight(1 - actNow, 1 - a);
            var before = acts.GetInput(1 - actNow);
            if (a >= 1 && before.IsValid()) { graph.Disconnect(acts, 1 - actNow); before.Destroy(); }
            actWeight = Mathf.MoveTowards(actWeight, Acting ? 1 : 0, actRate * dt);
            ApplyAct();
            if (!Acting && actWeight <= 0)
            {
                var last = acts.GetInput(actNow);
                if (last.IsValid()) { graph.Disconnect(acts, actNow); last.Destroy(); }
            }
            // the upper body's clip: in, held, out at its end
            if (upperAt >= 0)
            {
                float t = Time.time - upperAt;
                float u = t >= upperLength ? 0 : upperPeak * Mathf.Clamp01(Mathf.Min(t / upperIn, (upperLength - t) / upperOut));
                layers.SetInputWeight(1, u);
                if (t >= upperLength)
                {
                    upperAt = -1;
                    var old = layers.GetInput(1);
                    if (old.IsValid()) { graph.Disconnect(layers, 1); old.Destroy(); }
                }
            }
            // moving: standing, walking and running blended by speed; turning on the spot steps the feet round
            float s = Mathf.Max(0, Speed);
            if (Stride <= 0 && s < 0.3f && Turning > 45) s = Mathf.Max(s, Mathf.Min(0.9f, Turning / 360 * 1.2f));
            Blend(s, out float walk, out float run);
            float back = Backward ? -1 : 1;
            if (Stride > 0 && s > 0) Showing(run > 0 ? 2 : 1).SetSpeed(Mathf.Clamp(s / (run > 0 ? Stride * RunPace(Running) : WalkAtOwnPace), 0.4f, 1.5f));
            else for (int i = 1; i < 3; i++) { var c = Showing(i); if (c.IsValid() && ways[i, slotNow[i]] == null) c.SetSpeed(back); }
            // the ways of stepping: Going eased in, and each way's four clips moved on in step
            goingNow += Mathf.DeltaAngle(goingNow, Going) * (1 - Mathf.Exp(-dt * 10));
            Weigh(goingNow);
            for (int i = 1; i < 3; i++)
                for (int n = 0; n < 2; n++)
                    if (ways[i, n] != null) Step(ways[i, n], dt, Stride > 0 && s > 0 ? Mathf.Clamp(s / (i == 2 ? Stride * RunPace(Running) : WalkAtOwnPace), 0.4f, 1.5f) : 1);
            float k = 1 - Mathf.Exp(-dt * 12);
            weight[0] = Mathf.Lerp(weight[0], 1 - walk, k);
            weight[1] = Mathf.Lerp(weight[1], walk * (1 - run), k);
            weight[2] = Mathf.Lerp(weight[2], walk * run, k);
            for (int i = 0; i < weight.Length; i++) moving.SetInputWeight(i, weight[i]);
        }

        void Blend(float speed, out float walk, out float run)
        {
            if (Stride > 0)
            {
                walk = Mathf.Clamp01(speed / 0.3f); // only starting and stopping mix with standing: a blended stride is what slides
                run = Running != "run" ? walk > 0 ? 1 : 0 // jogging or fleeing: their clips go about as fast as a walk, so moving at all is that clip
                    : speed > (WalkAtOwnPace + Stride * RunPace(Running)) / 2 ? 1 : 0;
                return;
            }
            walk = Mathf.Clamp01(speed / WalkSpeed);
            run = Mathf.Clamp01((speed - WalkSpeed) / (RunSpeed - WalkSpeed));
        }

        Transform neck, head;
        Renderer seen;
        float look;
        bool gaze;

        // The head turns after the animation has posed it; bones of anyone off screen are not posed, so nothing may build up on them.
        void LateUpdate()
        {
            if (Watch == null || gaze) return; // a body with its own rig (Gaze) turns its head inside the animation
            if (seen == null) { seen = GetComponentInChildren<Renderer>(); head = Body.GetBoneTransform(HumanBodyBones.Head); neck = Body.GetBoneTransform(HumanBodyBones.Neck); }
            if (head == null || seen == null || !seen.isVisible) { look = 0; return; }
            var to = Watch.position - head.position;
            bool wants = to.sqrMagnitude < 36 && Vector3.Angle(transform.forward, to) < 100;
            look = Mathf.MoveTowards(look, wants ? 1 : 0, Time.deltaTime * 3);
            if (look <= 0) return;
            var turn = Quaternion.RotateTowards(Quaternion.identity, Quaternion.FromToRotation(transform.forward, to.normalized), 60);
            if (neck != null) neck.rotation = Quaternion.Slerp(Quaternion.identity, turn, 0.35f * look) * neck.rotation;
            head.rotation = Quaternion.Slerp(Quaternion.identity, turn, (neck != null ? 0.65f : 1) * look) * head.rotation;
        }

        void OnDestroy()
        {
            if (graph.IsValid()) graph.Destroy();
        }
    }
}
