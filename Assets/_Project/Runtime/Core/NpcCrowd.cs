// The people on the streets (NPC Phase 3, 2026-11-08). Everyone on the roster keeps one body, made when they are first needed and hidden when they are not. They walk the lines ChunkWalks found
// on the sidewalks near the camera, up and down, stepping round at the ends. They keep left, do not walk through anyone ahead of them or standing, and do not cross roads or avoid poles and other things on a sidewalk. A line is chosen by a hash, so the same
// chunks give the same crowd. Skinning and animation stop for anyone off screen (Animator culling); Person does the rest, one set of clips for everyone.
using System.Collections.Generic;
using Dad.World;
using UnityEngine;

namespace Dad.Core
{
    public sealed class NpcCrowd : MonoBehaviour
    {
        /// <summary>Metres from the camera: where someone may be put, and where they are taken away again.</summary>
        public const float PlaceRange = 50f, KeepRange = 80f;
        const float TurnPerSecond = 150f, TurnTime = 1.2f, KeepLeft = 0.28f, StandAside = 0.38f, LaneChange = 0.7f, Ahead = 1.8f, Close = 1.3f, Touching = 0.7f, Shoulders = 0.45f;

        public ChunkStreamer Streamer;
        public Transform View;
        /// <summary>True in a shot: time passes only when told to (Run).</summary>
        public bool Manual;

        sealed class Walker
        {
            public NpcRecord Who;
            public int Number;
            public Person Body;
            public WalkLine Line;
            public double S;
            public int Way = 1;
            public float Speed, Rest, Turn, Lane, Wait;
            public bool Stands => System.Array.IndexOf(StandingActs, Who.behaviour) >= 0;
            public bool Runs => Who.behaviour == "run" || Who.behaviour == "jog";
            /// <summary>Seconds of fright left, the part of it spent startled, where the danger is (scene) and whether this one cowers instead of running.</summary>
            public float Fear, Startled;
            public Vector3 From;
            public bool Cowers;
            /// <summary>The body's health and parts (null until the body is made).</summary>
            public Bystander Shot;
            public bool Dead => Shot != null && Shot.Dead;
        }

        /// <summary>What someone on the roster is doing about danger: calm, startled, cowering or fleeing (empty if they are not on the roster).</summary>
        public string Mood(string id)
        {
            var p = walkers.Find(w => w.Who.id == id);
            return p == null ? "" : p.Dead ? "dead" : p.Fear <= 0 ? "calm" : p.Startled > 0 ? "startled" : p.Cowers ? "cowering" : "fleeing";
        }

        /// <summary>Where someone on the roster stands now, in the scene.</summary>
        public Vector3 Where(string id)
        {
            var p = walkers.Find(w => w.Who.id == id);
            return p != null && p.Body != null ? p.Body.transform.position : Vector3.zero;
        }

        // Those near enough hear it. A startle first, facing it; then the nearest cower where they are and the rest run away along their sidewalk, sprinting while it is close.
        void OnDanger(Vector3 at, float reach, Danger.Kind kind)
        {
            foreach (var p in walkers)
            {
                if (p.Line == null || p.Body == null || !p.Body.gameObject.activeSelf || p.Dead) continue;
                float d = Vector3.Distance(p.Body.transform.position, at);
                if (d > reach) continue;
                bool already = p.Fear > 0;
                p.From = at;
                p.Fear = Mathf.Max(p.Fear, 9 + p.Number % 5);
                if (already) continue;
                p.Cowers = d < reach * 0.2f || kind == Danger.Kind.Gun && d < reach * 0.6f || p.Number % 4 == 3 && d < reach * 0.5f; // some freeze
                p.Startled = 0.7f + p.Number % 3 * 0.15f;
                p.Body.Play("startle");
                var life = p.Body.Body.GetComponent<PersonLife>();
                if (life != null) life.Talking = true; // a cry
            }
        }

        void Afraid(Walker p, float dt)
        {
            p.Fear -= dt;
            if (p.Startled > 0)
            {
                p.Startled -= dt;
                p.Body.Speed = 0;
                if (p.Startled > 0) return;
                if (p.Cowers) p.Body.Play("cower");
                else p.Body.Stop();
            }
            if (p.Fear <= 0) { Calm(p); return; }
            if (p.Cowers) { p.Body.Speed = 0; return; }
            p.Line.At(p.S, out double e, out double n, out _, out double de, out double dn);
            FloatingOrigin.Active.Offset.ToWorld(p.From.x, p.From.z, out double fe, out double fn);
            p.Way = de * (e - fe) + dn * (n - fn) >= 0 ? 1 : -1; // along the line, away from it
            float away = (float)System.Math.Sqrt(Sq(e - fe) + Sq(n - fn));
            string gait = away < 40 ? "sprint" : "flee"; // flat out while it is close, then running and looking back
            p.Body.RunWith(gait);
            float speed = p.Body.Stride * Person.RunPace(gait);
            p.S += p.Way * speed * dt;
            if (p.S <= 0 || p.S >= p.Line.Length) // the sidewalk ends: nowhere further to go, so down where they are, short of those already down there
            {
                double end = p.S <= 0 ? 0 : p.Line.Length;
                int there = 0;
                foreach (var o in walkers) if (o != p && o.Cowers && o.Line == p.Line && System.Math.Abs(o.S - end) < 8) there++;
                p.S = end - p.Way * (0.4 + there * 0.9);
                p.Cowers = true;
                p.Body.Play("cower");
                speed = 0;
            }
            p.Body.Speed = speed;
        }

        void Calm(Walker p)
        {
            p.Fear = p.Startled = 0;
            p.Cowers = false;
            p.Rest = p.Turn = p.Wait = 0;
            p.Body.Stop();
            p.Body.RunWith(p.Who.behaviour == "jog" ? "jog" : "run");
            p.Body.Speed = 0;
            var life = p.Body.Body.GetComponent<PersonLife>();
            if (life != null) life.Talking = p.Who.behaviour == "talk" || p.Who.behaviour == "argue";
            if (p.Stands && p.Who.behaviour != "stand") p.Body.Play(p.Who.behaviour);
        }

        /// <summary>What someone may do standing in one place on the sidewalk; each but "stand" is the clip of that name, played over and over.</summary>
        public static readonly string[] StandingActs = { "stand", "talk", "text-stand", "argue", "phone", "smoke", "drink" };

        readonly List<Walker> walkers = new List<Walker>();
        readonly SortedDictionary<(int, int), ChunkWalks> chunks = new SortedDictionary<(int, int), ChunkWalks>();
        readonly List<WalkLine> lines = new List<WalkLine>();
        static readonly Dictionary<(int, string), Material> Looks = new Dictionary<(int, string), Material>();
        static readonly string[] Untouched = { "Eye", "Teeth", "Beard" };
        static readonly int OpacityPickId = Shader.PropertyToID("_OpacityPick"), BaseMap = Shader.PropertyToID("_BaseMap"), KeyId = Shader.PropertyToID("_Key"), WearsId = Shader.PropertyToID("_Wears"), ReachId = Shader.PropertyToID("_Reach"), KeyedId = Shader.PropertyToID("_Keyed"), SkinId = Shader.PropertyToID("_Skin"), DyeId = Shader.PropertyToID("_Dye"), CutId = Shader.PropertyToID("_Cut"), BumpId = Shader.PropertyToID("_BumpMap"), OpacityId = Shader.PropertyToID("_OpacityMap"), SmoothId = Shader.PropertyToID("_Smoothness"), AlphaId = Shader.PropertyToID("_Alpha"), SrcId = Shader.PropertyToID("_SrcBlend"), DstId = Shader.PropertyToID("_DstBlend"), ZWriteId = Shader.PropertyToID("_ZWrite"), GlossId = Shader.PropertyToID("_GlossMap"), GlossPickId = Shader.PropertyToID("_GlossPick"), GlossInvertId = Shader.PropertyToID("_GlossInvert"), HasGlossId = Shader.PropertyToID("_HasGloss"), SpecularId = Shader.PropertyToID("_SpecularMap"), HairId = Shader.PropertyToID("_Hair"), OrmId = Shader.PropertyToID("_OrmMap"), HasOccId = Shader.PropertyToID("_HasOcc"), MaskId = Shader.PropertyToID("_MaskMap");
        static readonly int[] TintIds = { Shader.PropertyToID("_TintR"), Shader.PropertyToID("_TintG"), Shader.PropertyToID("_TintB"), Shader.PropertyToID("_TintA") };
        bool stale;

        /// <summary>How many are on the street now.</summary>
        public int Placed { get; private set; }

        /// <summary>Turns the crowd on. Call before streaming starts: the walking lines are built with the chunks.</summary>
        public static NpcCrowd Attach(GameObject host, ChunkStreamer streamer, Transform view)
        {
            ChunkStreamer.BuildWalks = true;
            var c = host.AddComponent<NpcCrowd>();
            c.Streamer = streamer;
            c.View = view;
            var roster = NpcRoster.Load();
            for (int i = 0; i < roster.people.Length; i++)
                if (roster.people[i].Walks) c.walkers.Add(new Walker { Who = roster.people[i], Number = i });
            if (streamer != null)
            {
                streamer.WalksIn += c.OnWalks;
                streamer.ChunkOut += c.OnChunkOut;
            }
            Danger.Heard += c.OnDanger;
            return c;
        }

        void OnDestroy()
        {
            Danger.Heard -= OnDanger;
            if (Streamer == null) return;
            Streamer.WalksIn -= OnWalks;
            Streamer.ChunkOut -= OnChunkOut;
        }

        void OnWalks(ChunkCell cell, ChunkWalks w)
        {
            chunks[(cell.Ix, cell.Iy)] = w;
            stale = true;
        }

        void OnChunkOut(ChunkCell cell)
        {
            if (!chunks.TryGetValue((cell.Ix, cell.Iy), out var w)) return;
            chunks.Remove((cell.Ix, cell.Iy));
            stale = true;
            foreach (var p in walkers)
                if (p.Line != null && w.Lines.Contains(p.Line)) TakeAway(p);
        }

        /// <summary>Walking lines given directly, in a test.</summary>
        public void Use(ChunkWalks w)
        {
            chunks[(w.ChunkX, w.ChunkY)] = w;
            stale = true;
        }

        void Update()
        {
            if (!Manual) Step(Time.deltaTime);
        }

        /// <summary>Lets time pass, in a shot, and holds everyone in a frame of their walk.</summary>
        public void Run(float seconds)
        {
            for (float t = 0; t < seconds; t += 0.1f) Step(0.1f);
            foreach (var p in walkers)
                if (p.Body != null && p.Body.gameObject.activeSelf && !p.Dead)
                {
                    p.Body.Pose(p.Rest > 0 || p.Stands ? 0 : p.Speed, p.Number * 0.37);
                    Place(p, true);
                }
        }

        /// <summary>Everyone side by side, in a shot: at a place, facing a heading, a spacing apart along the line at right angles to it, held a time into their walk.</summary>
        public void Lineup(float east, float north, float height, float heading, float spacing, float held, string act = "", float facing = 0, string only = "")
        {
            float a = heading * Mathf.Deg2Rad;
            var line = walkers.FindAll(w => string.IsNullOrEmpty(only) || System.Array.IndexOf(only.Split(','), w.Who.id) >= 0);
            for (int i = 0; i < line.Count; i++)
            {
                var p = line[i];
                float along = (i - (line.Count - 1) / 2f) * spacing;
                FloatingOrigin.Active.Offset.ToScene(east + along * Mathf.Cos(a), north - along * Mathf.Sin(a), out float x, out float z);
                if (!Body(p)) continue;
                p.Body.transform.SetPositionAndRotation(new Vector3(x, height, z), Quaternion.Euler(0, heading + facing, 0));
                if (!string.IsNullOrEmpty(act)) p.Body.Play(act);
                p.Body.Pose(p.Stands ? 0 : p.Runs ? Person.RunSpeed : Person.WalkSpeed, held);
            }
            Placed = line.Count;
        }

        void Step(float dt)
        {
            if (View == null) return;
            if (stale)
            {
                lines.Clear();
                foreach (var c in chunks.Values) lines.AddRange(c.Lines);
                stale = false;
            }
            FloatingOrigin.Active.Offset.ToWorld(View.position.x, View.position.z, out double east, out double north);
            int placed = 0;
            foreach (var p in walkers)
            {
                if (p.Who.model == Cast.You) { if (p.Line != null) TakeAway(p); continue; } // the person you chose to be is not in the crowd as well
                if (p.Line == null && !Choose(p, east, north)) continue;
                p.Line.At(p.S, out double e, out double n, out _, out _, out _);
                if (Sq(e - east) + Sq(n - north) > KeepRange * KeepRange || p.Dead && Unseen(p))
                {
                    TakeAway(p);
                    continue;
                }
                placed++;
                if (p.Dead) continue; // where they fell
                Walk(p, dt);
                Place(p, false);
            }
            Placed = placed;
        }

        static double Sq(double v) => v * v;

        bool Choose(Walker p, double east, double north)
        {
            WalkLine best = null;
            uint bestHash = uint.MaxValue;
            double bestS = 0;
            for (int i = 0; i < lines.Count; i++)
            {
                uint h = ((uint)p.Number + 1) * 2654435761u ^ ((uint)i + 1) * 40503u;
                h ^= h >> 13;
                h *= 2246822519u;
                if (h >= bestHash) continue;
                double s = (h >> 8) % 65536 / 65536.0 * lines[i].Length;
                lines[i].At(s, out double e, out double n, out _, out _, out _);
                if (Sq(e - east) + Sq(n - north) > PlaceRange * PlaceRange) continue;
                best = lines[i];
                bestHash = h;
                bestS = s;
            }
            if (best == null || !Body(p)) return false;
            p.Line = best;
            p.S = bestS;
            p.Way = (bestHash & 1) == 0 ? 1 : -1;
            p.Speed = p.Who.behaviour == "jog" ? p.Body.Stride * Person.RunPace("jog") * (0.92f + (bestHash >> 16) % 16 / 100f)
                : p.Runs ? 3.4f + (bestHash >> 16) % 60 / 100f : p.Body.WalkAtOwnPace * (0.8f + (bestHash >> 16) % 25 / 100f);
            p.Rest = p.Turn = 0;
            p.Lane = p.Stands ? ((bestHash & 2) == 0 ? StandAside : -StandAside) : p.Way * KeepLeft;
            p.Body.gameObject.SetActive(true);
            p.Body.Phase(p.Number * 0.37);
            Place(p, true);
            return true;
        }

        void TakeAway(Walker p)
        {
            p.Line = null;
            if (p.Body == null) return;
            if (p.Dead)
            {
                // the body goes; the person is made again, whole, when next needed
                Destroy(p.Body.gameObject);
                p.Body = null;
                p.Shot = null;
                p.Fear = p.Startled = 0;
                p.Cowers = false;
                return;
            }
            p.Shot.Health = Bystander.Most; // wounds heal out of sight
            p.Body.gameObject.SetActive(false);
        }

        // Dead for longer than a body lingers (Remains.Linger), and not drawn by any camera.
        static bool Unseen(Walker p)
        {
            if (Time.time - p.Shot.DiedAt < Remains.Linger) return false;
            foreach (var r in p.Body.GetComponentsInChildren<Renderer>()) if (r.isVisible) return false;
            return true;
        }

        // A shot struck someone: the dead lie where they fell; the wounded run from where it came, at once, startled or cowering or not.
        void OnStruck(Walker p, Bystander b)
        {
            if (b.Dead)
            {
                p.Fear = p.Startled = 0;
                p.Body.Speed = 0;
                return;
            }
            bool was = p.Fear > 0;
            p.From = b.HurtFrom;
            p.Fear = Mathf.Max(p.Fear, 12 + p.Number % 5);
            p.Startled = 0;
            p.Cowers = false;
            p.Body.Stop();
            if (!was) { var life = p.Body.Body.GetComponent<PersonLife>(); if (life != null) life.Talking = true; } // a cry
        }

        void Walk(Walker p, float dt)
        {
            if (p.Fear > 0) { Afraid(p, dt); return; }
            if (p.Stands) { p.Body.Speed = 0; return; }
            if (p.Rest > 0)
            {
                p.Rest -= dt;
                p.Body.Speed = 0;
                if (p.Rest <= 0)
                {
                    p.Way = -p.Way;
                    p.Turn = TurnTime;
                }
                return;
            }
            float side = p.Way * KeepLeft, speed = p.Speed;
            bool round = false, headOn = false, blocked = false;
            foreach (var q in walkers)
            {
                if (q == p || q.Line != p.Line || q.Dead) continue;
                double gap = (q.S - p.S) * p.Way;
                if (gap <= 0) continue;
                blocked |= gap < Touching && Mathf.Abs(q.Lane - p.Lane) < Shoulders;
                if (q.Stands || q.Rest > 0) round |= gap < Ahead && Mathf.Sign(q.Lane) == Mathf.Sign(side); // someone standing in my lane
                else if (q.Way == p.Way) { if (gap < Close) speed = Mathf.Min(speed, q.Speed); } // behind someone slower
                else headOn |= gap < Ahead * 1.5f && Mathf.Abs(q.Lane - side) < 0.3f; // someone coming who stepped round into my lane
            }
            if (round || headOn) side = -side;
            float next = Mathf.MoveTowards(p.Lane, side, LaneChange * dt);
            if (walkers.TrueForAll(q => q == p || q.Line != p.Line || System.Math.Abs(q.S - p.S) > 0.5 || Mathf.Abs(q.Lane - next) >= Shoulders)) p.Lane = next; // not sideways into anyone
            if (p.Turn > 0)
            {
                p.Turn -= dt;
                p.Body.Speed = p.Speed * 0.7f; // stepping round on the spot
                return;
            }
            if (blocked)
            {
                speed = 0;
                p.Wait += dt;
                if (p.Wait > 2) // the way is not clearing: go back
                {
                    p.Wait = 0;
                    p.Rest = 0.5f;
                    p.Body.Speed = 0;
                    return;
                }
            }
            else p.Wait = 0;
            p.S += p.Way * speed * dt;
            if (p.S <= 0 || p.S >= p.Line.Length)
            {
                p.S = Mathf.Clamp((float)p.S, 0, (float)p.Line.Length);
                p.Rest = 1.5f + p.Number % 3;
            }
            p.Body.Speed = speed;
        }

        void Place(Walker p, bool snap)
        {
            p.Line.At(p.S, out double e, out double n, out float y, out double de, out double dn);
            FloatingOrigin.Active.Offset.ToScene(e - dn * p.Lane, n + de * p.Lane, out float x, out float z);
            var want = Quaternion.Euler(0, Mathf.Atan2((float)de * p.Way, (float)dn * p.Way) * Mathf.Rad2Deg, 0);
            var t = p.Body.transform;
            if (p.Fear > 0 && (p.Startled > 0 || p.Cowers)) // startled or cowering: facing it
            {
                var to = p.From - t.position;
                to.y = 0;
                if (to.sqrMagnitude > 0.01f) want = Quaternion.LookRotation(to);
            }
            t.SetPositionAndRotation(new Vector3(x, y, z), snap ? want : Quaternion.RotateTowards(t.rotation, want, TurnPerSecond * Time.deltaTime));
        }

        // The one body this person ever has.
        bool Body(Walker p)
        {
            if (p.Body != null) return true;
            p.Body = Person.Spawn(p.Who.model, Vector3.zero, 0, p.Who.worn, p.Who.gait);
            if (p.Body == null) return false;
            p.Body.name = p.Who.id;
            p.Body.Body.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            var head = p.Body.Body.GetBoneTransform(HumanBodyBones.Head);
            float natural = head.position.y / 0.885f; // the head bone of a figure at rest stands this far up it
            if (natural > 0.1f) p.Body.transform.localScale = Vector3.one * (p.Who.height / natural);
            p.Body.Stride = 0.885f * p.Who.height;
            p.Shot = Bystander.Give(p.Body); // sized, and before the clips first move it
            var walker = p;
            p.Shot.Struck += b => OnStruck(walker, b);
            p.Body.Watch = View;
            Dress(p.Body, p.Who);
            if (p.Who.behaviour == "jog") p.Body.RunWith("jog");
            if (p.Stands && p.Who.behaviour != "stand") p.Body.Play(p.Who.behaviour); // talk, text-stand and argue are clips of those names
            var life = p.Body.Body.GetComponent<PersonLife>();
            if (life != null) life.Talking = p.Who.behaviour == "talk" || p.Who.behaviour == "argue";
            return true;
        }

        /// <summary>Puts a person's maps on as the street shows them: cut-out hair and lashes, see-through eye shells, the roster's colours.</summary>
        public static void Dress(Person body, NpcRecord who)
        {
            string look = who.garment + who.wears + who.reach.ToString("F3") + who.skin.ToString("F2") + who.hairColour + who.tints;
            foreach (var r in body.GetComponentsInChildren<Renderer>(false))
            {
                if (r is SkinnedMeshRenderer skin)
                {
                    skin.skinnedMotionVectors = false;
                    skin.quality = QualitySettings.skinWeights == SkinWeights.Unlimited ? SkinQuality.Auto : SkinQuality.Bone4; // every bone the model weights a vertex to; a lower preset is held to four, never fewer
                }
                bool hair = !string.IsNullOrEmpty(who.hair) && who.hair.Contains(r.name);
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null || !mats[i].HasProperty(BaseMap)) continue;
                    var shape = Person.Map(who.model, r.name, mats[i].name, "_cut"); // a model's hair cards or lashes, cut to this map
                    var carded = Person.Map(who.model, r.name, mats[i].name, "_cuta"); // or to this map's alpha
                    float cut = shape != null || carded != null ? 0.35f : Cut(mats[i].name), shell = Shell(mats[i].name);
                    if (!hair && cut == 0 && shell == 0 && Leave(mats[i].name)) continue;
                    var picture = mats[i].GetTexture(BaseMap);
                    var shapeOnly = picture == null ? Person.Map(who.model, r.name, mats[i].name, "_opacity") : null; // a part the file gives only a shape, as a scalp under hair cards
                    bool strands = picture == null && (r.name.Contains("eyebrows") || r.name.Contains("eyelashes")); // brows and lashes modelled hair by hair, with no picture
                    bool dyed = hair || shapeOnly != null || strands; // which then takes the hair's colour
                    if (picture == null && shell > 0) { mats[i] = Clear(); continue; } // an eye shell the file gives no picture: the eye's own picture shows through it
                    if (picture == null && !dyed) continue;
                    int pictureId = picture != null ? picture.GetInstanceID() : shapeOnly != null ? shapeOnly.GetInstanceID() : 0;
                    if (!Looks.TryGetValue((pictureId, look), out var m))
                    {
                        m = new Material(Resources.Load<Material>("Materials/Person"));
                        if (picture != null) m.SetTexture(BaseMap, picture);
                        if (mats[i].HasProperty(BumpId) && mats[i].GetTexture(BumpId) != null) m.SetTexture(BumpId, mats[i].GetTexture(BumpId));
                        var opacity = Person.Map(who.model, r.name, mats[i].name, "_opacity");
                        if (opacity != null) m.SetTexture(OpacityId, opacity);
                        if (shape != null) m.SetTexture(OpacityId, shape);
                        if (carded != null || Carded(mats[i].name) && picture != null)
                        {
                            m.SetTexture(OpacityId, carded != null ? carded : picture);
                            m.SetVector(OpacityPickId, new Vector4(0, 0, 0, 1));
                        }
                        if (mats[i].HasProperty(SmoothId)) m.SetFloat(SmoothId, Mathf.Min(mats[i].GetFloat(SmoothId), 0.45f));
                        m.SetShaderPassEnabled("DepthNormals", false); // the street's ambient occlusion, made for buildings, speckles a face
                        m.SetFloat(CutId, cut);
                        Surface(m, who.model, r.name, mats[i].name, who.tints);
                        Fabrics.Apply(m, mats[i].name); // what the garment is made of, where the wardrobe says
                        if (shell > 0) SeeThrough(m, shell);
                        if (hair && picture != null) // hair cards: the highlights run along the strands, so the surface's own round highlight is kept faint
                        {
                            m.SetFloat(HairId, 1);
                            m.SetFloat(SmoothId, 0.2f);
                        }
                        if (dyed)
                        {
                            m.SetFloat(DyeId, 1);
                            m.SetColor(WearsId, NpcRoster.Hex(who.hairColour, new Color(0.09f, 0.07f, 0.06f)));
                        }
                        else
                        {
                            m.SetFloat(SkinId, who.skin);
                            bool keyed = !string.IsNullOrEmpty(who.garment) && !string.IsNullOrEmpty(who.wears) && m.GetFloat(DyeId) < 0.5f; // hair the wardrobe dyes keeps its colour
                            m.SetFloat(KeyedId, keyed ? 1 : 0);
                            if (keyed)
                            {
                                m.SetColor(KeyId, NpcRoster.Hex(who.garment, Color.white));
                                m.SetColor(WearsId, NpcRoster.Hex(who.wears, Color.white));
                                m.SetFloat(ReachId, who.reach);
                            }
                        }
                        Looks[(pictureId, look)] = m;
                    }
                    mats[i] = m;
                }
                r.sharedMaterials = mats;
            }
        }

        // The rest of a model's maps, by the end of the file's name: _specular (colour), _specsmooth (colour, smoothness in alpha), _smoothness, _roughness, _metallic,
        // _orm (occlusion, roughness, metallic in red, green, blue) and _mask, whose four channels take the colours in the roster's "tints". Without a map nothing changes.
        static void Surface(Material m, string model, string part, string material, string tints)
        {
            Texture2D Find(string kind) => Person.Map(model, part, material, kind);
            void Gloss(Texture2D map, Vector4 pick, float invert)
            {
                m.SetTexture(GlossId, map);
                m.SetVector(GlossPickId, pick);
                m.SetFloat(GlossInvertId, invert);
                m.SetFloat(HasGlossId, 1);
            }
            var joined = Find("_specsmooth");
            var specular = joined != null ? joined : Find("_specular");
            if (specular != null)
            {
                m.SetTexture(SpecularId, specular);
                m.EnableKeyword("_SPECULAR_SETUP");
            }
            var orm = Find("_orm");
            var metal = orm != null ? orm : Find("_metallic");
            if (metal != null) m.SetTexture(OrmId, metal);
            m.SetFloat(HasOccId, orm != null ? 1 : 0);
            Texture2D smooth = Find("_smoothness"), rough = Find("_roughness");
            if (smooth != null) Gloss(smooth, new Vector4(1, 0, 0, 0), 0);
            else if (rough != null) Gloss(rough, new Vector4(1, 0, 0, 0), 1);
            else if (joined != null) Gloss(joined, new Vector4(0, 0, 0, 1), 0);
            else if (orm != null) Gloss(orm, new Vector4(0, 1, 0, 0), 1);
            var mask = Find("_mask");
            if (mask == null || string.IsNullOrEmpty(tints)) return;
            m.SetTexture(MaskId, mask);
            var each = tints.Split(',');
            for (int i = 0; i < each.Length && i < TintIds.Length; i++) m.SetColor(TintIds[i], NpcRoster.Hex(each[i].Trim(), Color.white));
        }

        // Parts the model draws through its own opacity map. Lashes and hair cards are cut to that shape, so they sort and shadow as solid parts.
        static float Cut(string material) => material.Contains("lash") || material.Contains("Transparency") || Carded(material) ? 0.35f : 0;

        // A material whose own picture carries its cut-out in alpha: hair and lashes on the Rocketbox people.
        static bool Carded(string material) => material.EndsWith("_opacity", System.StringComparison.Ordinal);

        // Shells the model lays over the eye, blended as the file says: how solid each is, or 0 if the material is not one.
        static float Shell(string material) => material.Contains("Cornea") ? 0.8f : material.Contains("Tearline") || material.Contains("Occlusion") ? 1 : 0;

        static void SeeThrough(Material m, float solid)
        {
            m.SetFloat(AlphaId, solid);
            m.SetFloat(SrcId, (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat(DstId, (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat(ZWriteId, 0);
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            foreach (var pass in new[] { "ShadowCaster", "DepthOnly", "DepthNormals" }) m.SetShaderPassEnabled(pass, false);
        }

        static Material clear;
        static Material Clear()
        {
            if (clear != null) return clear;
            clear = new Material(Resources.Load<Material>("Materials/Person")) { name = "Clear shell" };
            SeeThrough(clear, 0);
            return clear;
        }

        static bool Leave(string material)
        {
            foreach (var part in Untouched) if (material.Contains(part)) return true;
            return false;
        }
    }
}
