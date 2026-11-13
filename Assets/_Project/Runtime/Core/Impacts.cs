// What a shot does where it strikes (2026-11-12, Docs/Characters/SHOOTER_BAR_AUDIT.md section 10). On a wall, the street or a car: a hole left on it, and dust and chips,
// sparks, splinters or shards thrown back off it; on a body: a puff of blood and drops going on the way the shot went, and blood on the wall or street behind and below.
// Each is heard (GunSound.Impact). What the surface is comes from what was struck: a body part (Hitbox), a car (its layer), or the names of the thing and its material.
// Nothing is made per shot: each kind of thing thrown is one particle system that every impact emits into, and the marks are a fixed number of flat quads, the oldest
// moved to the newest place when all are used. The pictures are drawn here, in code, once.
using UnityEngine;

namespace Dad.Core
{
    public static class Impacts
    {
        public enum Surface { Stone, Metal, Wood, Glass, Body }

        /// <summary>Marks kept at once: past this the oldest goes.</summary>
        public const int MostMarks = 160;

        /// <summary>For tests: impacts made, marks placed, and the last mark placed.</summary>
        public static int Made, Marked;
        public static Transform LastMark;

        static ParticleSystem dust, chips, sparks, splinters, shards, mist, drops;
        static Transform[] marks;
        static MeshRenderer[] markShown;
        static int nextMark;
        static Material holeStone, holeMetal, holeWood, holeGlass;
        static Material[] blood;
        static Mesh quad;
        static GameObject host;

        /// <summary>What was struck, from the collider.</summary>
        public static Surface Of(Collider c)
        {
            if (c == null) return Surface.Stone;
            if (c.GetComponent<Hitbox>() != null) return Surface.Body;
            int layer = c.gameObject.layer;
            if (layer == WorldLayers.Vehicle || layer == WorldLayers.Traffic) return Surface.Metal;
            string name = c.name.ToLowerInvariant();
            var r = c.GetComponent<Renderer>();
            if (r != null && r.sharedMaterial != null) name += " " + r.sharedMaterial.name.ToLowerInvariant();
            if (c.sharedMaterial != null) name += " " + c.sharedMaterial.name.ToLowerInvariant();
            if (name.Contains("glass") || name.Contains("window")) return Surface.Glass;
            if (name.Contains("metal") || name.Contains("steel") || name.Contains("iron") || name.Contains("car") || name.Contains("gun")) return Surface.Metal;
            if (name.Contains("wood") || name.Contains("plank") || name.Contains("tree") || name.Contains("crate")) return Surface.Wood;
            return Surface.Stone;
        }

        /// <summary>A shot from a place strikes where the hit says.</summary>
        public static void Hit(RaycastHit hit, Vector3 from)
        {
            if (host == null) Build();
            Made++;
            var surface = Of(hit.collider);
            var n = hit.normal.sqrMagnitude > 0.5f ? hit.normal : (from - hit.point).normalized;
            var way = (hit.point - from).normalized;
            var p = hit.point;
            var off = p + n * 0.02f; // what is thrown starts clear of the face it came off: from on it, collision turns it back into the wall
            switch (surface)
            {
                case Surface.Body:
                    Throw(mist, off, way * 0.5f, 0.8f, 9, 0.3f, 0.45f, 0.1f, 0.28f, new Color(0.5f, 0.04f, 0.04f, 0.9f));
                    Throw(drops, off, way * 2.2f, 0.55f, 12, 0.5f, 0.9f, 0.012f, 0.03f, new Color(0.35f, 0.02f, 0.02f, 1f));
                    Bleed(p, way, hit.collider.transform);
                    GunSound.Impact("body", p);
                    return;
                case Surface.Metal:
                    Throw(sparks, off, Vector3.Reflect(way, n) * 7f + n * 1.5f, 0.7f, 14, 0.08f, 0.25f, 0.004f, 0.008f, new Color(1f, 0.72f, 0.35f, 1f));
                    Throw(dust, off, n * 0.6f, 0.5f, 3, 0.4f, 0.8f, 0.05f, 0.12f, new Color(0.45f, 0.45f, 0.45f, 0.4f));
                    Mark(p, n, holeMetal, 0.1f, hit.collider.transform);
                    GunSound.Impact("metal", p);
                    return;
                case Surface.Wood:
                    Throw(splinters, off, n * 3f, 0.6f, 10, 0.5f, 0.9f, 0.01f, 0.022f, new Color(0.82f, 0.64f, 0.42f, 1f));
                    Throw(dust, off, n * 0.8f, 0.5f, 5, 0.5f, 1f, 0.06f, 0.16f, new Color(0.6f, 0.52f, 0.4f, 0.45f));
                    Mark(p, n, holeWood, 0.1f, hit.collider.transform);
                    GunSound.Impact("wood", p);
                    return;
                case Surface.Glass:
                    Throw(shards, off, (n * 1.5f + way * 1.5f), 0.7f, 12, 0.5f, 1f, 0.008f, 0.025f, new Color(0.85f, 0.92f, 1f, 0.9f));
                    Mark(p, n, holeGlass, 0.16f, hit.collider.transform);
                    GunSound.Impact("glass", p);
                    return;
                default:
                    Throw(dust, off, n * 1.2f, 0.5f, 8, 0.6f, 1.3f, 0.07f, 0.22f, new Color(0.58f, 0.55f, 0.5f, 0.5f));
                    Throw(chips, off, n * 3.5f, 0.6f, 7, 0.4f, 0.8f, 0.01f, 0.025f, new Color(0.35f, 0.33f, 0.3f, 1f));
                    Mark(p, n, holeStone, 0.12f, hit.collider.transform);
                    GunSound.Impact("stone", p);
                    return;
            }
        }

        // Blood on what is behind (the way the shot went, within 2.5 m) and below (the street under the one struck).
        static void Bleed(Vector3 at, Vector3 way, Transform body)
        {
            var paint = blood[Random.Range(0, blood.Length)];
            if (Physics.Raycast(at + way * 0.15f, way, out var behind, 2.5f, WorldLayers.Solid | 1, QueryTriggerInteraction.Ignore) && !behind.transform.IsChildOf(body.root))
                Mark(behind.point, behind.normal, paint, Random.Range(0.35f, 0.6f), behind.collider.transform);
            if (Physics.Raycast(at + way * 0.3f, Vector3.down, out var below, 3f, WorldLayers.Solid | 1, QueryTriggerInteraction.Ignore) && !below.transform.IsChildOf(body.root))
                Mark(below.point, below.normal, blood[Random.Range(0, blood.Length)], Random.Range(0.25f, 0.45f), below.collider.transform);
        }

        // Particles thrown from a place: along a way, spread by a share of their speed, for a time, at a size, in a colour.
        static void Throw(ParticleSystem into, Vector3 at, Vector3 way, float spread, int count, float lifeLo, float lifeHi, float sizeLo, float sizeHi, Color colour)
        {
            float speed = way.magnitude;
            var e = new ParticleSystem.EmitParams { applyShapeToPosition = false };
            for (int i = 0; i < count; i++)
            {
                e.position = at;
                e.velocity = way + Random.insideUnitSphere * speed * spread;
                e.startLifetime = Random.Range(lifeLo, lifeHi);
                e.startSize = Random.Range(sizeLo, sizeHi);
                e.startColor = colour;
                e.rotation = Random.Range(0f, 360f);
                into.Emit(e, 1);
            }
        }

        // A mark lying on a surface, facing out of it, turned at random; it goes with what it is on.
        static void Mark(Vector3 at, Vector3 normal, Material paint, float size, Transform on)
        {
            if (marks[nextMark] == null) NewMark(nextMark); // it went with the car it was on
            var m = marks[nextMark];
            var r = markShown[nextMark];
            nextMark = (nextMark + 1) % marks.Length;
            m.SetParent(host.transform, false);
            m.SetPositionAndRotation(at + normal * 0.003f, Quaternion.LookRotation(-normal) * Quaternion.Euler(0, 0, Random.Range(0f, 360f)));
            m.localScale = Vector3.one * size;
            r.sharedMaterial = paint;
            r.enabled = true;
            // a car drives off with its holes (a stretched parent would stretch the mark); the street stays put, and its chunks come and go, so marks on it hang from nothing
            if (on != null && (on.gameObject.layer == WorldLayers.Vehicle || on.gameObject.layer == WorldLayers.Traffic || on.GetComponentInParent<Rigidbody>() != null) && IsUniform(on))
                m.SetParent(on, true);
            Marked++;
            LastMark = m;
        }

        static bool IsUniform(Transform t)
        {
            var s = t.lossyScale;
            return Mathf.Abs(s.x - s.y) < 1e-3f && Mathf.Abs(s.y - s.z) < 1e-3f;
        }

        /// <summary>Particles of a kind in the air now ("dust", "chips", "sparks", "splinters", "shards", "blood mist", "blood drops"), for tests.</summary>
        public static int Thrown(string kind)
        {
            if (host == null) return 0;
            var t = host.transform.Find(kind);
            return t != null ? t.GetComponent<ParticleSystem>().particleCount : 0;
        }

        /// <summary>Marks showing now, for tests.</summary>
        public static int Showing()
        {
            int n = 0;
            if (markShown != null) foreach (var r in markShown) if (r != null && r.enabled) n++;
            return n;
        }

        /// <summary>Takes every mark away (a new game, a test).</summary>
        public static void Clear()
        {
            if (marks == null) return;
            for (int i = 0; i < marks.Length; i++)
            {
                if (marks[i] == null) continue;
                marks[i].SetParent(host != null ? host.transform : null, false);
                markShown[i].enabled = false;
            }
            foreach (var ps in new[] { dust, chips, sparks, splinters, shards, mist, drops }) if (ps != null) ps.Clear();
        }

        static void Build()
        {
            host = new GameObject("Impacts");
            Object.DontDestroyOnLoad(host);
            var baseMat = Resources.Load<Material>("Materials/Mark");
            if (baseMat == null) { Debug.LogError("[Dad] no Materials/Mark"); baseMat = new Material(Shader.Find("Sprites/Default")); }
            Material With(Texture2D t, float glow = 0)
            {
                var m = new Material(baseMat) { mainTexture = t };
                m.SetFloat("_Glow", glow);
                return m;
            }
            var soft = Pictures.Soft();
            var lit = With(soft);
            var glowing = With(soft, 3);
            dust = System("dust", lit, 0.05f, 2.5f, ParticleSystemRenderMode.Billboard, 1.8f);
            chips = System("chips", lit, 1f, 0.5f, ParticleSystemRenderMode.Billboard, 1f);
            sparks = System("sparks", glowing, 0.8f, 0.6f, ParticleSystemRenderMode.Stretch, 0.6f, 0.015f, 1f); // thin streaks, about 10 cm long at speed
            splinters = System("splinters", lit, 1f, 0.4f, ParticleSystemRenderMode.Stretch, 1f);
            shards = System("shards", lit, 1f, 0.3f, ParticleSystemRenderMode.Billboard, 1f);
            mist = System("blood mist", lit, 0.1f, 4f, ParticleSystemRenderMode.Billboard, 2.2f);
            drops = System("blood drops", lit, 1f, 0.3f, ParticleSystemRenderMode.Stretch, 1f);
            // the fresh stone, wood or metal a shot lays bare is lighter than the weathered face round it, so a hole reads on a dark wall too
            holeStone = With(Pictures.Hole(new Color(0.06f, 0.055f, 0.05f), new Color(0.8f, 0.77f, 0.72f), 0.3f, 0.36f, 1f, 6, 11));
            holeMetal = With(Pictures.Hole(new Color(0.04f, 0.04f, 0.04f), new Color(0.85f, 0.85f, 0.83f), 0.35f, 0.24f, 0.15f, 0, 23));
            holeWood = With(Pictures.Hole(new Color(0.1f, 0.06f, 0.03f), new Color(0.86f, 0.7f, 0.48f), 0.25f, 0.32f, 0.8f, 5, 37));
            holeGlass = With(Pictures.Hole(new Color(0.1f, 0.1f, 0.1f), new Color(0.92f, 0.95f, 1f), 0.08f, 0, 0, 14, 41, true));
            blood = new[] { With(Pictures.Blood(1)), With(Pictures.Blood(2)), With(Pictures.Blood(3)) };
            quad = new Mesh { name = "mark" };
            quad.SetVertices(new[] { new Vector3(-0.5f, -0.5f, 0), new Vector3(0.5f, -0.5f, 0), new Vector3(0.5f, 0.5f, 0), new Vector3(-0.5f, 0.5f, 0) });
            quad.SetNormals(new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back });
            quad.SetUVs(0, new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) });
            quad.SetColors(new[] { Color.white, Color.white, Color.white, Color.white });
            quad.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
            marks = new Transform[MostMarks];
            markShown = new MeshRenderer[MostMarks];
            for (int i = 0; i < MostMarks; i++) NewMark(i);
        }

        static void NewMark(int i)
        {
            var g = new GameObject("mark");
            g.transform.SetParent(host.transform, false);
            g.AddComponent<MeshFilter>().sharedMesh = quad;
            var r = g.AddComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.enabled = false;
            marks[i] = g.transform;
            markShown[i] = r;
        }

        // One kind of thing thrown: world space, falling with gravity times this, slowed by the air, fading out (and growing, for a puff).
        static ParticleSystem System(string name, Material m, float gravity, float drag, ParticleSystemRenderMode mode, float grow, float velocityScale = 0.04f, float lengthScale = 1.5f)
        {
            var g = new GameObject(name);
            g.transform.SetParent(host.transform, false);
            var ps = g.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = gravity;
            main.maxParticles = 3000;
            main.startSpeed = 0;
            var emission = ps.emission;
            emission.enabled = false;
            var shape = ps.shape;
            shape.enabled = false;
            var slow = ps.limitVelocityOverLifetime;
            slow.enabled = true;
            slow.drag = drag;
            slow.limit = 100;
            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 0.6f), new GradientAlphaKey(0, 1) });
            fade.color = gradient;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, 1, 1, grow));
            var collide = ps.collision; // drops and chips land rather than fall through the street
            collide.enabled = gravity > 0.5f;
            collide.type = ParticleSystemCollisionType.World;
            collide.mode = ParticleSystemCollisionMode.Collision3D;
            collide.collidesWith = WorldLayers.Solid | 1;
            collide.dampen = 0.6f;
            collide.bounce = 0.15f;
            collide.quality = ParticleSystemCollisionQuality.Low;
            var r = g.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = m;
            r.renderMode = mode;
            if (mode == ParticleSystemRenderMode.Stretch) { r.velocityScale = velocityScale; r.lengthScale = lengthScale; }
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            ps.Play();
            return ps;
        }

        /// <summary>The pictures of marks and particles, drawn once.</summary>
        static class Pictures
        {
            const int Size = 128;

            // A soft round blot: every particle.
            public static Texture2D Soft()
            {
                var t = New("soft", 64);
                var px = new Color[64 * 64];
                for (int y = 0; y < 64; y++)
                    for (int x = 0; x < 64; x++)
                    {
                        float r = new Vector2(x - 31.5f, y - 31.5f).magnitude / 31.5f;
                        px[y * 64 + x] = new Color(1, 1, 1, Mathf.Pow(Mathf.Clamp01(1 - r), 1.6f));
                    }
                t.SetPixels(px);
                t.Apply(true, true);
                return t;
            }

            // A bullet hole: the dark hole, a crater of the surface laid bare round it (lighter, darker towards the hole, its edge as ragged as rough), flecks
            // chipped off just outside, soot or scuffing fading out, and cracks (or, on glass, a star of them).
            public static Texture2D Hole(Color hole, Color bare, float soot, float crater, float rough, int cracks, int seed, bool glass = false)
            {
                var t = New("hole", Size);
                var rnd = new System.Random(seed);
                var angles = new float[cracks];
                var lengths = new float[cracks];
                for (int i = 0; i < cracks; i++) { angles[i] = (float)(rnd.NextDouble() * Mathf.PI * 2); lengths[i] = 0.45f + (float)rnd.NextDouble() * (glass ? 0.55f : 0.45f); }
                var px = new Color[Size * Size];
                for (int y = 0; y < Size; y++)
                    for (int x = 0; x < Size; x++)
                    {
                        var v = new Vector2(x - Size / 2f + 0.5f, y - Size / 2f + 0.5f) / (Size / 2f);
                        float r = v.magnitude, a = Mathf.Atan2(v.y, v.x);
                        float w = glass ? 1 : 0.25f + 0.75f * rough; // punched metal is round, broken stone ragged
                        float wobble = 1 + w * (0.18f * Mathf.Sin(a * 5 + seed) + 0.1f * Mathf.Sin(a * 11 + seed * 2));
                        float rr = r / wobble;
                        Color c = new Color(0, 0, 0, 0);
                        if (glass)
                        {
                            float ring = Mathf.Exp(-Mathf.Pow((r - 0.22f) / 0.03f, 2)) * 0.8f + Mathf.Exp(-Mathf.Pow((r - 0.5f) / 0.025f, 2)) * 0.45f;
                            c = new Color(bare.r, bare.g, bare.b, Mathf.Clamp01(ring));
                        }
                        else
                        {
                            float big = Mathf.PerlinNoise(x * 0.09f + seed, y * 0.09f + seed * 0.5f), fine = Mathf.PerlinNoise(x * 0.5f + seed * 1.7f, y * 0.5f);
                            if (soot > 0) c = new Color(hole.r, hole.g, hole.b, Mathf.Clamp01(1 - rr / 0.95f) * soot * (0.6f + 0.4f * fine));
                            float edge = crater * (1 + rough * (big - 0.5f) * 0.9f + rough * (fine - 0.5f) * 0.25f);
                            if (rr < edge + 0.12f * rough && fine > 0.55f - 0.15f * (1 - rough)) // flecks chipped off round the crater
                                c = Over(c, new Color(bare.r, bare.g, bare.b, 0.75f * Mathf.Clamp01(1 - (rr - edge) / (0.12f * rough + 0.01f))));
                            if (rr < edge)
                            {
                                float deep = Mathf.Clamp01((rr - 0.1f) / Mathf.Max(0.01f, edge - 0.1f));
                                var floor = Color.Lerp(bare * 0.45f, bare, deep * (0.65f + 0.35f * fine));
                                c = Over(c, new Color(floor.r, floor.g, floor.b, Mathf.Clamp01((edge - rr) / 0.02f)));
                            }
                        }
                        if (rr < (glass ? 0.15f : 0.11f)) c = Over(c, new Color(hole.r, hole.g, hole.b, Mathf.Clamp01(((glass ? 0.15f : 0.11f) - rr) / 0.015f)));
                        for (int i = 0; i < cracks; i++)
                        {
                            float d = Mathf.Abs(Mathf.DeltaAngle(a * Mathf.Rad2Deg, angles[i] * Mathf.Rad2Deg)) * Mathf.Deg2Rad * r;
                            if (r > 0.12f && r < lengths[i] && d < 0.012f + 0.01f * (1 - r))
                                c = Over(c, glass ? new Color(bare.r, bare.g, bare.b, 0.8f * (1 - r / lengths[i])) : new Color(hole.r, hole.g, hole.b, 0.9f * (1 - r / lengths[i])));
                        }
                        px[y * Size + x] = c;
                    }
                t.SetPixels(px);
                t.Apply(true, true);
                return t;
            }

            // A splash of blood: a blot of joined drops, darker where thicker, with drops flung round it.
            public static Texture2D Blood(int seed)
            {
                var t = New("blood", Size);
                var rnd = new System.Random(seed * 97);
                int blobs = 9;
                var at = new Vector2[blobs + 14];
                var rad = new float[blobs + 14];
                for (int i = 0; i < blobs; i++) { at[i] = new Vector2((float)rnd.NextDouble() - 0.5f, (float)rnd.NextDouble() - 0.5f) * 0.45f; rad[i] = 0.12f + (float)rnd.NextDouble() * 0.16f; }
                for (int i = blobs; i < at.Length; i++)
                {
                    float a = (float)(rnd.NextDouble() * Mathf.PI * 2), d = 0.42f + (float)rnd.NextDouble() * 0.5f;
                    at[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * d * 0.95f;
                    rad[i] = 0.015f + (float)rnd.NextDouble() * 0.035f;
                }
                var px = new Color[Size * Size];
                for (int y = 0; y < Size; y++)
                    for (int x = 0; x < Size; x++)
                    {
                        var v = new Vector2(x - Size / 2f + 0.5f, y - Size / 2f + 0.5f) / (Size / 2f);
                        float field = 0;
                        for (int i = 0; i < at.Length; i++) field += rad[i] * rad[i] / Mathf.Max(1e-4f, (v - at[i]).sqrMagnitude);
                        float edge = Mathf.Clamp01((field - 1f) * 3f);
                        float thick = Mathf.Clamp01((field - 1f) * 0.25f);
                        float grain = 0.85f + 0.15f * Mathf.PerlinNoise(x * 0.2f + seed * 3, y * 0.2f);
                        var c = Color.Lerp(new Color(0.45f, 0.03f, 0.03f), new Color(0.2f, 0.01f, 0.01f), thick) * grain;
                        c.a = edge * 0.95f;
                        px[y * Size + x] = c;
                    }
                t.SetPixels(px);
                t.Apply(true, true);
                return t;
            }

            static Texture2D New(string name, int size) => new Texture2D(size, size, TextureFormat.RGBA32, true) { name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 4 };

            static Color Over(Color under, Color over)
            {
                float a = over.a + under.a * (1 - over.a);
                if (a <= 0) return new Color(0, 0, 0, 0);
                var c = (over * over.a + under * under.a * (1 - over.a)) / a;
                c.a = a;
                return c;
            }
        }
    }
}
