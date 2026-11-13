// The Blender people's reference pose, made the clips' own (2026-11-12). Unity measures every muscle of a humanoid from the pose stored with its avatar (the T-pose in
// the importer's human description) and plays a clip as muscles. The clips were made on marker_man, the Mixamo character, who stood in an exact T-pose. Ours were stored
// with the arms a few degrees low, elbows and wrists bent, palms turned and every finger joint curled, so every clip put their wrists and fingers that much off his. Here
// each bone of the shoulders, arms, hands and fingers in our stored pose is turned to point exactly as his did, the hand also turned about its length so the palm faces
// as his. Nothing else changes: not the mesh, the bind pose or the clips. marker_man was deleted at the owner's word (2026-11-13); the way each of his bones pointed
// was measured from his model first and is kept in ReferencePose.json beside this file, which is all this needs of him.
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Dad.EditorTools
{
    public static class PeoplePose
    {
        /// <summary>The clips' reference pose: the way each bone of marker_man pointed in his stored pose, measured from his model on 2026-11-13 before it was deleted.</summary>
        public const string ReferenceFile = "Assets/_Project/Editor/ReferencePose.json";
        public static string Of(string who) => "Assets/_Project/Resources/People/" + who + ".fbx";

        [System.Serializable] sealed class Way { public string bone, to; public Vector3 way; }
        [System.Serializable] sealed class ReferenceData { public string source; public Way[] ways; public Vector3 acrossLeft, acrossRight; }

        /// <summary>The reference pose as kept: the way each bone points and the palms' turn, in the model's own frame, as Posed gives them for a model.</summary>
        sealed class Reference
        {
            readonly Dictionary<(HumanBodyBones, HumanBodyBones), Vector3> ways = new Dictionary<(HumanBodyBones, HumanBodyBones), Vector3>();
            readonly Vector3 left, right;

            public Reference()
            {
                var text = System.IO.File.Exists(ReferenceFile) ? System.IO.File.ReadAllText(ReferenceFile) : null;
                var data = text != null ? JsonUtility.FromJson<ReferenceData>(text) : null;
                if (data?.ways == null) throw new System.IO.FileNotFoundException("the reference pose is missing", ReferenceFile);
                foreach (var w in data.ways)
                    ways[((HumanBodyBones)System.Enum.Parse(typeof(HumanBodyBones), w.bone), (HumanBodyBones)System.Enum.Parse(typeof(HumanBodyBones), w.to))] = w.way;
                left = data.acrossLeft;
                right = data.acrossRight;
            }

            public Vector3? Way(HumanBodyBones b, HumanBodyBones to) => ways.TryGetValue((b, to), out var w) ? w : (Vector3?)null;
            public Vector3? Across(bool isLeft) => isLeft ? left : right;
        }

        // Each bone turned, with what it points to; a finger's last bone points to its tip, found in the skin (ours have no end bones).
        const HumanBodyBones Tip = HumanBodyBones.LastBone;
        public static readonly (HumanBodyBones bone, HumanBodyBones to)[] Arms = Chain();
        // reported, not turned
        public static readonly (HumanBodyBones bone, HumanBodyBones to)[] Rest =
        {
            (HumanBodyBones.Hips, HumanBodyBones.Spine), (HumanBodyBones.Spine, HumanBodyBones.Chest), (HumanBodyBones.Neck, HumanBodyBones.Head),
            (HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg), (HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot), (HumanBodyBones.LeftFoot, HumanBodyBones.LeftToes),
            (HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg), (HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot), (HumanBodyBones.RightFoot, HumanBodyBones.RightToes),
        };

        static (HumanBodyBones, HumanBodyBones)[] Chain()
        {
            var c = new List<(HumanBodyBones, HumanBodyBones)>();
            HumanBodyBones B(string n) => (HumanBodyBones)System.Enum.Parse(typeof(HumanBodyBones), n);
            foreach (var side in new[] { "Left", "Right" })
            {
                c.Add((B(side + "Shoulder"), B(side + "UpperArm")));
                c.Add((B(side + "UpperArm"), B(side + "LowerArm")));
                c.Add((B(side + "LowerArm"), B(side + "Hand")));
                c.Add((B(side + "Hand"), B(side + "MiddleProximal")));
                foreach (var f in new[] { "Thumb", "Index", "Middle", "Ring", "Little" })
                {
                    c.Add((B(side + f + "Proximal"), B(side + f + "Intermediate")));
                    c.Add((B(side + f + "Intermediate"), B(side + f + "Distal")));
                    c.Add((B(side + f + "Distal"), Tip));
                }
            }
            return c.ToArray();
        }

        /// <summary>A model laid out in the pose stored with its avatar, to measure and turn; nothing in it is saved unless asked.</summary>
        sealed class Posed : System.IDisposable
        {
            public readonly ModelImporter Importer;
            public readonly HumanDescription Description;
            public readonly GameObject Root;
            readonly Dictionary<HumanBodyBones, Transform> bone = new Dictionary<HumanBodyBones, Transform>();
            readonly Dictionary<Transform, Vector3> tip = new Dictionary<Transform, Vector3>(); // in the bone's own frame

            public Posed(string fbx)
            {
                Importer = (ModelImporter)AssetImporter.GetAtPath(fbx);
                Description = Importer.humanDescription;
                Root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(fbx));
                Root.hideFlags = HideFlags.HideAndDontSave;
                var animator = Root.GetComponent<Animator>();
                if (animator != null) Object.DestroyImmediate(animator); // the stored pose, not whatever an animator would put
                var byName = new Dictionary<string, Transform>();
                foreach (var t in Root.GetComponentsInChildren<Transform>(true)) if (!byName.ContainsKey(t.name)) byName[t.name] = t;
                // the stored turn of each bone; where it sits is the model's own, so a rebuilt model with longer bones is measured as it is now
                foreach (var s in Description.skeleton)
                    if (byName.TryGetValue(s.name, out var t))
                    {
                        if ((t.localPosition - s.position).sqrMagnitude > 1e-6f) Moved++;
                        t.localRotation = s.rotation; t.localScale = s.scale;
                    }
                foreach (var h in Description.human)
                {
                    int i = System.Array.IndexOf(HumanTrait.BoneName, h.humanName);
                    if (i >= 0 && byName.TryGetValue(h.boneName, out var t)) bone[(HumanBodyBones)i] = t;
                }
            }

            /// <summary>Bones the stored pose has more than a millimetre from where the model has them: the model was rebuilt since the pose was stored.</summary>
            public readonly int Moved;

            public Transform this[HumanBodyBones b] => bone.TryGetValue(b, out var t) ? t : null;

            // Where a finger's last bone ends: the middle of the skin it carries most, taken from the bind pose into the bone's own frame, so it moves with the bone.
            Vector3? TipOf(Transform b)
            {
                if (tip.TryGetValue(b, out var local)) return b.TransformPoint(local);
                Vector3 best = default; int most = 0;
                foreach (var r in Root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    int i = System.Array.IndexOf(r.bones, b);
                    var mesh = r.sharedMesh;
                    if (i < 0 || mesh == null || !mesh.isReadable) continue;
                    var weights = mesh.GetAllBoneWeights();
                    var per = mesh.GetBonesPerVertex();
                    var v = mesh.vertices;
                    Vector3 sum = default; int n = 0, at = 0;
                    for (int k = 0; k < per.Length; k++)
                    {
                        for (int j = 0; j < per[k]; j++)
                        {
                            var w = weights[at + j];
                            if (w.boneIndex == i && w.weight > 0.6f) { sum += v[k]; n++; }
                        }
                        at += per[k];
                    }
                    if (n > most) { most = n; best = mesh.bindposes[i].MultiplyPoint3x4(sum / n); }
                }
                if (most == 0) return null;
                tip[b] = best;
                return b.TransformPoint(best);
            }

            /// <summary>The way a bone points, in the model's own frame.</summary>
            public Vector3? Way(HumanBodyBones b, HumanBodyBones to)
            {
                var from = this[b];
                if (from == null) return null;
                Vector3? end = to == Tip ? TipOf(from) : this[to]?.position;
                if (end == null) return null;
                var d = end.Value - from.position;
                return d.sqrMagnitude < 1e-10f ? (Vector3?)null : Root.transform.InverseTransformDirection(d.normalized);
            }

            /// <summary>Across the knuckles, little finger to index, in the model's own frame: how the palm is turned about the hand's length.</summary>
            public Vector3? Across(bool left)
            {
                var index = this[left ? HumanBodyBones.LeftIndexProximal : HumanBodyBones.RightIndexProximal];
                var little = this[left ? HumanBodyBones.LeftLittleProximal : HumanBodyBones.RightLittleProximal];
                return index == null || little == null ? (Vector3?)null : Root.transform.InverseTransformDirection(index.position - little.position);
            }

            public void Dispose() => Object.DestroyImmediate(Root);
        }

        /// <summary>Degrees between this person's stored pose and the reference: the way each bone points, and the palm's turn about the hand's length. Turned is true for
        /// what Match turns (shoulders, arms, hands, fingers); the spine and legs are only reported.</summary>
        public static List<(string what, float degrees, bool turned)> Residuals(string fbx)
        {
            var found = new List<(string, float, bool)>();
            using var us = new Posed(fbx);
            var idol = new Reference();
            foreach (var (b, to) in Arms.Concat(Rest))
            {
                var mine = us.Way(b, to); var his = idol.Way(b, to);
                if (mine != null && his != null) found.Add((b.ToString(), Vector3.Angle(mine.Value, his.Value), Arms.Contains((b, to))));
            }
            foreach (var left in new[] { true, false })
            {
                var length = us.Way(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand, left ? HumanBodyBones.LeftMiddleProximal : HumanBodyBones.RightMiddleProximal);
                var a = us.Across(left); var b = idol.Across(left);
                if (length == null || a == null || b == null) continue;
                found.Add(((left ? "Left" : "Right") + " palm turn", Vector3.Angle(Vector3.ProjectOnPlane(a.Value, length.Value), Vector3.ProjectOnPlane(b.Value, length.Value)), true));
            }
            return found;
        }

        /// <summary>Turns this person's stored shoulders, arms, hands and fingers to point as the reference's, and reimports the model. Returns what was done.</summary>
        public static string Match(string fbx)
        {
            var log = new StringBuilder(fbx + ":");
            var idol = new Reference();
            using (var us = new Posed(fbx))
            {
                var turned = new HashSet<Transform>();
                foreach (var (b, to) in Arms)
                {
                    var t = us[b];
                    var mine = us.Way(b, to); var his = idol.Way(b, to);
                    if (t == null || mine == null || his == null) { log.Append($" {b} skipped;"); continue; }
                    var root = us.Root.transform;
                    Vector3 now = root.TransformDirection(mine.Value), want = root.TransformDirection(his.Value);
                    float off = Vector3.Angle(now, want);
                    t.rotation = Quaternion.FromToRotation(now, want) * t.rotation;
                    if (b == HumanBodyBones.LeftHand || b == HumanBodyBones.RightHand)
                    {
                        bool left = b == HumanBodyBones.LeftHand;
                        Vector3 a = root.TransformDirection(us.Across(left).Value), w = root.TransformDirection(idol.Across(left).Value);
                        float roll = Vector3.SignedAngle(Vector3.ProjectOnPlane(a, want), Vector3.ProjectOnPlane(w, want), want);
                        t.rotation = Quaternion.AngleAxis(roll, want) * t.rotation;
                        log.Append($" {b} {off:F1}+roll {roll:F1};");
                    }
                    else if (off >= 0.5f) log.Append($" {b} {off:F1};");
                    turned.Add(t);
                }
                var description = us.Description;
                var skeleton = description.skeleton;
                var byName = new Dictionary<string, Transform>();
                foreach (var t in us.Root.GetComponentsInChildren<Transform>(true)) if (!byName.ContainsKey(t.name)) byName[t.name] = t;
                for (int i = 0; i < skeleton.Length; i++)
                {
                    if (!byName.TryGetValue(skeleton[i].name, out var t)) continue;
                    skeleton[i].position = t.localPosition; // where the model has the bone now
                    if (turned.Contains(t)) skeleton[i].rotation = t.localRotation;
                }
                if (us.Moved > 0) log.Append($" {us.Moved} bones moved to where the rebuilt model has them;");
                description.skeleton = skeleton;
                us.Importer.humanDescription = description;
                us.Importer.SaveAndReimport();
            }
            return log.ToString();
        }

        /// <summary>How many bones of this person's stored pose are not where the model has them (0 once Match has run on the model as it is).</summary>
        public static int Stale(string fbx)
        {
            using var us = new Posed(fbx);
            return us.Moved;
        }

        /// <summary>Everyone built in Blender: the twenty, any of whom can be the player (Cast).</summary>
        public static IEnumerable<string> People() => Enumerable.Range(1, 10).Select(i => $"npc_m{i:00}").Concat(Enumerable.Range(1, 10).Select(i => $"npc_f{i:00}"))
            .Where(w => System.IO.File.Exists(Of(w)));

        // Once, on 2026-11-13, before marker_man was deleted: his stored pose, measured as Posed measures ours, written to ReferenceFile.
        public static void BakeReference()
        {
            const string idolFbx = "Assets/_Project/Resources/People/marker_man.fbx";
            var data = new ReferenceData { source = "marker_man.fbx (Mixamo), the stored pose of his avatar, measured 2026-11-13 before the model was deleted at the owner's word" };
            var list = new List<Way>();
            using (var idol = new Posed(idolFbx))
            {
                foreach (var (b, to) in Arms.Concat(Rest))
                {
                    var w = idol.Way(b, to);
                    if (w != null) list.Add(new Way { bone = b.ToString(), to = to.ToString(), way = w.Value });
                }
                data.acrossLeft = idol.Across(true).Value;
                data.acrossRight = idol.Across(false).Value;
            }
            data.ways = list.ToArray();
            System.IO.File.WriteAllText(ReferenceFile, JsonUtility.ToJson(data, true));
            Debug.Log($"[Dad] reference pose: {list.Count} ways written to {ReferenceFile}");
            EditorApplication.Exit(list.Count == Arms.Length + Rest.Length ? 0 : 1);
        }

        [MenuItem("Dad/People/Stand the Blender people in the clips' pose")]
        public static void MatchAll()
        {
            foreach (var who in People()) Debug.Log("[Dad] POSED " + Match(Of(who)));
        }
    }
}
