// The face and hair of a person whose model carries them (2026-11-10). Character Creator people bring the face's blend shapes (the Blender people's ARKit and viseme shapes are taken under the same names, 12 Nov): here they blink, the eyes glance about and,
// while talking, the mouth makes the shapes of speech. Their hair cards are weighted to the head alone, so each gets short chains of bones of its own, weighted down from the
// scalp, that swing on springs as the head moves. A model without these shapes or hair keeps what it has: nothing here is added to it.
using System.Collections.Generic;
using UnityEngine;

namespace Dad.Core
{
    [DefaultExecutionOrder(100)] // after the animation and Person's turn of the head
    public sealed class PersonLife : MonoBehaviour
    {
        /// <summary>While true the mouth shapes speech.</summary>
        public bool Talking;

        readonly Dictionary<string, List<(SkinnedMeshRenderer r, int i)>> shapes = new Dictionary<string, List<(SkinnedMeshRenderer, int)>>();
        Transform head, eyeL, eyeR;
        Quaternion restL, restR, glance = Quaternion.identity, glanceTo = Quaternion.identity;
        float nextBlink, blinkAt = -1, nextGlance, syllableAt, syllableLength, open, wanted;
        int vowel;
        SkinnedMeshRenderer[] seen;
        static readonly string[] Speech = { "V_Open", "V_Wide", "V_Tight_O", "V_Explosive", "V_Dental_Lip", "V_Lip_Open" };
        static readonly (string theirs, string ours)[] Aliases = {
            ("eyeBlinkLeft", "Eye_Blink_L"), ("eyeBlinkRight", "Eye_Blink_R"), ("jawOpen", "Jaw_Open"), ("viseme_aa", "V_Open"), ("viseme_E", "V_Wide"),
            ("viseme_O", "V_Tight_O"), ("viseme_PP", "V_Explosive"), ("viseme_FF", "V_Dental_Lip"), ("viseme_CH", "V_Lip_Open") };

        public static PersonLife Give(Animator body)
        {
            var life = body.gameObject.AddComponent<PersonLife>();
            life.Build(body);
            return life;
        }

        void Build(Animator body)
        {
            head = body.GetBoneTransform(HumanBodyBones.Head);
            eyeL = body.GetBoneTransform(HumanBodyBones.LeftEye);
            eyeR = body.GetBoneTransform(HumanBodyBones.RightEye);
            if (eyeL != null) restL = eyeL.localRotation;
            if (eyeR != null) restR = eyeR.localRotation;
            foreach (var r in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mesh = r.sharedMesh;
                if (mesh == null) continue;
                for (int i = 0; i < mesh.blendShapeCount; i++)
                {
                    string name = mesh.GetBlendShapeName(i);
                    name = name.Substring(name.LastIndexOf('.') + 1);
                    if (!shapes.TryGetValue(name, out var list)) shapes[name] = list = new List<(SkinnedMeshRenderer, int)>();
                    list.Add((r, i));
                }
                if (head != null) Hair(r);
            }
            foreach (var (theirs, ours) in Aliases) // the Blender people's face is named the ARKit and viseme way: the same shapes under the names used here
                if (!shapes.ContainsKey(ours) && shapes.TryGetValue(theirs, out var same)) shapes[ours] = same;
            seen = GetComponentsInChildren<SkinnedMeshRenderer>();
            nextBlink = Time.time + Random.Range(0.5f, 4f);
        }

        void Set(string name, float weight)
        {
            if (shapes.TryGetValue(name, out var list)) foreach (var (r, i) in list) r.SetBlendShapeWeight(i, weight);
        }

        void LateUpdate()
        {
            if (!System.Array.Exists(seen, r => r.isVisible)) return; // any part on screen: a face in close-up shows the head alone
            Face();
            Swing();
        }

        void Face()
        {
            float now = Time.time;
            if (now >= nextBlink) { blinkAt = now; nextBlink = now + (Random.value < 0.15f ? 0.3f : Random.Range(2f, 6f)); }
            float b = blinkAt < 0 ? 0 : 1 - Mathf.Abs((now - blinkAt) / 0.075f - 1); // 75 ms closing, 75 ms opening
            Set("Eye_Blink_L", Mathf.Clamp01(b) * 100);
            Set("Eye_Blink_R", Mathf.Clamp01(b) * 100);
            if (eyeL != null && eyeR != null)
            {
                if (now >= nextGlance) { nextGlance = now + Random.Range(0.6f, 3f); glanceTo = Quaternion.Euler(Random.Range(-4f, 4f), Random.Range(-9f, 9f), 0); }
                glance = Quaternion.Slerp(glance, glanceTo, 1 - Mathf.Exp(-Time.deltaTime * 30)); // saccades are fast
                eyeL.localRotation = restL * glance;
                eyeR.localRotation = restR * glance;
            }
            if (Talking && now >= syllableAt + syllableLength)
            {
                syllableAt = now;
                bool pause = Random.value < 0.12f;
                syllableLength = pause ? Random.Range(0.25f, 0.7f) : Random.Range(0.11f, 0.24f);
                Set(Speech[vowel], 0);
                vowel = Random.Range(0, Speech.Length);
                wanted = pause ? 0 : Random.Range(35f, 85f);
            }
            if (!Talking) wanted = 0;
            open = Mathf.Lerp(open, wanted * Mathf.Sin(Mathf.Clamp01((now - syllableAt) / Mathf.Max(0.01f, syllableLength)) * Mathf.PI), 1 - Mathf.Exp(-Time.deltaTime * 25));
            Set(Speech[vowel], open);
            Set("Jaw_Open", open * 0.25f);
        }

        // ---- hair ----
        sealed class Chain { public Transform[] bones; public Vector3[] at, before, rest; public float[] length; }
        readonly List<Chain> chains = new List<Chain>();
        Vector3 skull; float skullRadius; Vector3 lastHead;
        const int Segments = 3;
        static readonly Dictionary<Mesh, (Mesh mesh, Vector3[][] restLocal)> Rigged = new Dictionary<Mesh, (Mesh, Vector3[][])>();

        static bool IsHair(Material m) => m != null && m.name.IndexOf("hair", System.StringComparison.OrdinalIgnoreCase) >= 0
            && !System.Text.RegularExpressions.Regex.IsMatch(m.name, "scalp|lash|brow|beard|skullcap", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        void Hair(SkinnedMeshRenderer r)
        {
            var mats = r.sharedMaterials;
            var mesh = r.sharedMesh;
            var bones = r.bones;
            int hi = System.Array.IndexOf(bones, head);
            if (hi < 0 || !System.Array.Exists(mats, IsHair)) return;
            var bind = mesh.bindposes;
            var down = head.InverseTransformDirection(Vector3.down).normalized; // the figure stands at rest when it is spawned
            var front = Vector3.ProjectOnPlane(head.InverseTransformDirection(transform.forward), down).normalized;
            var side = Vector3.Cross(down, front);
            if (!Rigged.TryGetValue(mesh, out var rig) || (rig.mesh == null && rig.restLocal != null)) Rigged[mesh] = rig = Rig(mesh, mats, bind[hi], down, front, side); // again if the copy was unloaded
            if (rig.mesh == null) return;
            var all = new List<Transform>(bones);
            foreach (var restLocal in rig.restLocal)
            {
                var c = new Chain { bones = new Transform[Segments], at = new Vector3[Segments + 1], before = new Vector3[Segments + 1], rest = restLocal, length = new float[Segments] };
                Transform parent = head;
                for (int k = 0; k < Segments; k++)
                {
                    var b = new GameObject("HairBone").transform;
                    b.SetParent(parent, false);
                    b.localPosition = k == 0 ? restLocal[0] : parent.InverseTransformPoint(head.TransformPoint(restLocal[k]));
                    c.bones[k] = b;
                    all.Add(b);
                    parent = b;
                }
                for (int k = 0; k < Segments; k++) c.length[k] = Vector3.Distance(head.TransformPoint(restLocal[k]), head.TransformPoint(restLocal[k + 1]));
                for (int k = 0; k <= Segments; k++) c.at[k] = c.before[k] = head.TransformPoint(restLocal[k]);
                chains.Add(c);
            }
            r.bones = all.ToArray();
            r.sharedMesh = rig.mesh;
            skullRadius = 0.085f; // metres, at the figure's own size; the skull's middle is about 8 cm above the head bone
            skull = head.InverseTransformPoint(head.position + transform.up * 0.08f * transform.lossyScale.y);
            lastHead = head.position;
        }

        // The hair's mesh with bones added: up to four chains (behind, left, right, in front) hang from the scalp to the ends of the hair on that side. Each vertex keeps its own
        // weights near the scalp and goes over to the two nearest chains towards the ends. Shared by everyone of this model, as the bind poses are the same.
        static (Mesh, Vector3[][]) Rig(Mesh source, Material[] mats, Matrix4x4 headBind, Vector3 down, Vector3 front, Vector3 side)
        {
            var verts = source.vertices;
            var inHair = new bool[verts.Length];
            for (int s = 0; s < source.subMeshCount && s < mats.Length; s++)
                if (IsHair(mats[s])) foreach (int t in source.GetTriangles(s)) inHair[t] = true;
            var local = new Vector3[verts.Length];
            float top = float.MaxValue, bottom = float.MinValue; Vector3 centre = Vector3.zero; int n = 0;
            for (int v = 0; v < verts.Length; v++)
            {
                if (!inHair[v]) continue;
                local[v] = headBind.MultiplyPoint3x4(verts[v]);
                float d = Vector3.Dot(local[v], down);
                top = Mathf.Min(top, d); bottom = Mathf.Max(bottom, d); centre += local[v]; n++;
            }
            if (n == 0) return (null, null);
            centre /= n;
            float span = bottom - top;
            var dirs = new[] { -front, -side, side, front };
            var ends = new float[4];
            for (int v = 0; v < verts.Length; v++)
            {
                if (!inHair[v]) continue;
                var flat = Vector3.ProjectOnPlane(local[v] - centre, down);
                for (int c = 0; c < 4; c++) if (Vector3.Angle(flat, dirs[c]) < 50) ends[c] = Mathf.Max(ends[c], Vector3.Dot(local[v], down));
            }
            float start = top + span * 0.3f; // chains start a third of the way down the hair: the crown does not swing
            var used = new List<int>();
            for (int c = 0; c < 4; c++) if (ends[c] - start > span * 0.25f) used.Add(c);
            if (used.Count == 0) return (null, null);
            float radius = 0;
            for (int v = 0; v < verts.Length; v++) if (inHair[v]) radius = Mathf.Max(radius, Vector3.ProjectOnPlane(local[v] - centre, down).magnitude);
            var restLocal = new Vector3[used.Count][];
            for (int u = 0; u < used.Count; u++)
            {
                int c = used[u];
                var anchor = centre + dirs[c] * radius * 0.55f + down * (start - Vector3.Dot(centre, down));
                restLocal[u] = new Vector3[Segments + 1];
                for (int k = 0; k <= Segments; k++) restLocal[u][k] = anchor + down * ((ends[c] - start) * k / Segments);
            }
            var mesh = Copy(source);
            var oldBind = source.bindposes;
            var bind = new List<Matrix4x4>(oldBind);
            int first = bind.Count;
            for (int u = 0; u < used.Count; u++)
            {
                for (int k = 0; k < Segments; k++) bind.Add(Matrix4x4.Translate(-restLocal[u][k]) * headBind); // each bone sits at its rest point with the head's axes
            }
            mesh.bindposes = bind.ToArray();
            var counts = source.GetBonesPerVertex();
            var weights = source.GetAllBoneWeights();
            var newCounts = new byte[verts.Length];
            var newWeights = new List<BoneWeight1>(weights.Length + verts.Length * 2);
            int w = 0;
            var add = new List<BoneWeight1>(8);
            for (int v = 0; v < verts.Length; v++)
            {
                add.Clear();
                float a = 0; int ca = -1, cb = -1; float blend = 0; int seg = 0;
                if (inHair[v])
                {
                    float along = (Vector3.Dot(local[v], down) - start);
                    var flat = Vector3.ProjectOnPlane(local[v] - centre, down);
                    float best = 999, next = 999;
                    for (int u = 0; u < used.Count; u++)
                    {
                        float ang = Vector3.Angle(flat, dirs[used[u]]);
                        if (ang < best) { next = best; cb = ca; best = ang; ca = u; } else if (ang < next) { next = ang; cb = u; }
                    }
                    float len = ends[used[ca]] - start;
                    float s = Mathf.Clamp01(along / Mathf.Max(1e-5f, len));
                    a = s * s * (3 - 2 * s) * 0.95f;
                    seg = Mathf.Min(Segments - 1, (int)(s * Segments));
                    blend = cb < 0 ? 0 : Mathf.Clamp01(best / Mathf.Max(1e-3f, best + next));
                }
                for (int j = 0; j < counts[v]; j++) { var bw = weights[w + j]; bw.weight *= 1 - a; if (bw.weight > 0) add.Add(bw); }
                w += counts[v];
                if (a > 0)
                {
                    add.Add(new BoneWeight1 { boneIndex = first + ca * Segments + seg, weight = a * (1 - blend) });
                    if (cb >= 0 && blend > 0) add.Add(new BoneWeight1 { boneIndex = first + cb * Segments + seg, weight = a * blend });
                }
                add.Sort((x, y) => y.weight.CompareTo(x.weight));
                newCounts[v] = (byte)add.Count;
                newWeights.AddRange(add);
            }
            var nc = new Unity.Collections.NativeArray<byte>(newCounts, Unity.Collections.Allocator.Temp);
            var nw = new Unity.Collections.NativeArray<BoneWeight1>(newWeights.ToArray(), Unity.Collections.Allocator.Temp);
            mesh.SetBoneWeights(nc, nw);
            nc.Dispose(); nw.Dispose();
            return (mesh, restLocal);
        }

        // The same mesh, channel for channel, in Unity's plain layout: the import packs skin weights as 16-bit numbers, which new weights cannot be written into.
        static Mesh Copy(Mesh source)
        {
            var mesh = new Mesh { name = source.name + " (hair bones)", indexFormat = source.indexFormat };
            mesh.vertices = source.vertices;
            if (source.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.Normal)) mesh.normals = source.normals;
            if (source.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.Tangent)) mesh.tangents = source.tangents;
            if (source.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.Color)) mesh.colors = source.colors;
            var uv = new List<Vector4>();
            for (int c = 0; c < 8; c++)
            {
                if (!source.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.TexCoord0 + c)) continue;
                source.GetUVs(c, uv);
                mesh.SetUVs(c, uv);
            }
            mesh.subMeshCount = source.subMeshCount;
            for (int s = 0; s < source.subMeshCount; s++) mesh.SetIndices(source.GetIndices(s), source.GetTopology(s), s, false, (int)source.GetBaseVertex(s));
            int n = source.vertexCount;
            Vector3[] dv = new Vector3[n], dn = new Vector3[n], dt = new Vector3[n];
            for (int b = 0; b < source.blendShapeCount; b++)
                for (int f = 0; f < source.GetBlendShapeFrameCount(b); f++)
                {
                    source.GetBlendShapeFrameVertices(b, f, dv, dn, dt);
                    mesh.AddBlendShapeFrame(source.GetBlendShapeName(b), source.GetBlendShapeFrameWeight(b, f), dv, dn, dt);
                }
            mesh.bounds = source.bounds;
            return mesh;
        }

        // Each chain's points fall with gravity, lag behind the head and spring back towards where they hang at rest, held at their lengths and outside the skull.
        void Swing()
        {
            if (chains.Count == 0) return;
            float dt = Mathf.Clamp(Time.deltaTime, 1e-4f, 1 / 20f);
            bool jumped = (head.position - lastHead).sqrMagnitude > 1; // put somewhere else: start again from rest
            lastHead = head.position;
            var skullAt = head.TransformPoint(skull);
            float scale = transform.lossyScale.y;
            foreach (var c in chains)
            {
                for (int k = 0; k < Segments; k++) c.bones[k].localRotation = Quaternion.identity;
                c.at[0] = c.bones[0].position;
                for (int k = 1; k <= Segments; k++)
                {
                    var home = head.TransformPoint(c.rest[k]);
                    if (jumped) { c.at[k] = c.before[k] = home; continue; }
                    var p = c.at[k];
                    var v = (p - c.before[k]) * 0.9f;
                    c.before[k] = p;
                    p += v + Physics.gravity * dt * dt * 0.6f;
                    p = Vector3.Lerp(p, home, 1 - Mathf.Exp(-dt * 9)); // stiffness: hair holds its shape
                    var from = c.at[k - 1];
                    p = from + (p - from).normalized * c.length[k - 1];
                    var out_ = p - skullAt;
                    float r = Mathf.Min(skullRadius * scale, Vector3.Distance(home, skullAt)); // never pushed further out than it hangs at rest
                    if (out_.sqrMagnitude < r * r) p = skullAt + out_.normalized * r;
                    c.at[k] = p;
                }
                for (int k = 0; k < Segments; k++) // each bone turns its child towards its point; the bones' own axes are the head's, so the rest offset is in those units
                {
                    var b = c.bones[k];
                    b.rotation = Quaternion.FromToRotation(b.TransformDirection(c.rest[k + 1] - c.rest[k]), c.at[k + 1] - b.position) * b.rotation;
                }
            }
        }
    }
}
