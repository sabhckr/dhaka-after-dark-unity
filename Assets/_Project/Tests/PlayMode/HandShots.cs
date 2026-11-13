// Hands and wrists (2026-11-12): the player (OnFoot.Who, whichever of the twenty you chose) beside four of the Blender people at their heights (marker_man, on whom the clips were made, was deleted 2026-11-13): holding a rifle and a
// pistol as the game has them held (the gun's own idle, Armed), and unarmed in the plain idle, at the same moment of each clip. Each hand is filmed from the front and from its side, and its angles are written down: how far the wrist
// bends off the forearm, how the palm is turned against the elbow's plane, and how much each finger curls. Explicit: it writes to
// Evidence/local/rebuild/hands and checks nothing. Run by name.
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Dad.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Dad.Tests
{
    [Explicit("pictures and angles to look at: run by name")]
    public class HandShots
    {
        static readonly string[] Who = { OnFoot.Who, "npc_m01", "npc_m09", "npc_f01", "npc_f09" };
        static readonly (string tag, string clip, string gun)[] Clips = { ("scar", "rifle-aim", "scar"), ("ump", "rifle-aim", "ump"), ("glock", "pistol-idle", "glock"), ("idle", "idle", null), ("aim-clip", "rifle-aim", null) };

        static float Angle(Vector3 a, Vector3 b) => Vector3.Angle(a, b);

        static string Measure(Animator a, bool left)
        {
            Transform B(HumanBodyBones b) => a.GetBoneTransform(b);
            var upper = B(left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm).position;
            var lower = B(left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm).position;
            var hand = B(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand).position;
            var middle = B(left ? HumanBodyBones.LeftMiddleProximal : HumanBodyBones.RightMiddleProximal).position;
            var index = B(left ? HumanBodyBones.LeftIndexProximal : HumanBodyBones.RightIndexProximal).position;
            var little = B(left ? HumanBodyBones.LeftLittleProximal : HumanBodyBones.RightLittleProximal).position;
            Vector3 fore = hand - lower, palmDir = middle - hand;
            var elbowPlane = Vector3.Cross(lower - upper, fore);
            var palm = Vector3.Cross(index - hand, little - hand) * (left ? 1 : -1);
            var sb = new StringBuilder();
            sb.Append($"{(left ? "L" : "R")} elbow {Angle(lower - upper, fore):F0} wrist {Angle(fore, palmDir):F0} palm-vs-elbow-plane {Angle(palm, elbowPlane):F0} curl");
            foreach (var (p, i, d) in new[] {
                (left ? HumanBodyBones.LeftIndexProximal : HumanBodyBones.RightIndexProximal, left ? HumanBodyBones.LeftIndexIntermediate : HumanBodyBones.RightIndexIntermediate, left ? HumanBodyBones.LeftIndexDistal : HumanBodyBones.RightIndexDistal),
                (left ? HumanBodyBones.LeftMiddleProximal : HumanBodyBones.RightMiddleProximal, left ? HumanBodyBones.LeftMiddleIntermediate : HumanBodyBones.RightMiddleIntermediate, left ? HumanBodyBones.LeftMiddleDistal : HumanBodyBones.RightMiddleDistal),
                (left ? HumanBodyBones.LeftLittleProximal : HumanBodyBones.RightLittleProximal, left ? HumanBodyBones.LeftLittleIntermediate : HumanBodyBones.RightLittleIntermediate, left ? HumanBodyBones.LeftLittleDistal : HumanBodyBones.RightLittleDistal),
                (left ? HumanBodyBones.LeftThumbProximal : HumanBodyBones.RightThumbProximal, left ? HumanBodyBones.LeftThumbIntermediate : HumanBodyBones.RightThumbIntermediate, left ? HumanBodyBones.LeftThumbDistal : HumanBodyBones.RightThumbDistal) })
            {
                Transform tp = B(p), ti = B(i), td = B(d);
                if (tp == null || ti == null || td == null) { sb.Append(" -"); continue; }
                Vector3 s1 = ti.position - tp.position, s2 = td.position - ti.position;
                sb.Append($" {Angle(palmDir, s1) + Angle(s1, s2):F0}");
            }
            return sb.ToString();
        }

        // Each rifle's fore-end, measured from its meshes in the gun's own frame (x right, y up from the bore's line, z forward from the grip): the underside along
        // its centre line and the lowest point of any part, in 2 cm steps, and each part's box. Where the support hand can close: written to fore-ends.txt.
        [UnityTest]
        public IEnumerator ForeEnds()
        {
            const string output = "Evidence/local/rebuild/hands";
            Directory.CreateDirectory(output);
            var log = new StringBuilder();
            foreach (var kind in new[] { Armed.Scar, Armed.Ump })
            {
                var p = Person.Spawn(OnFoot.Who, Vector3.zero, 180);
                p.Play(kind.Idle);
                var armed = Armed.Give(p, kind.Name);
                yield return new WaitForSeconds(0.5f);
                var g = armed.Held;
                Vector3 o = armed.Muzzle;
                var inv = Quaternion.Inverse(g.rotation);
                Vector3 F(Vector3 w) => inv * (w - o);
                float gripZ = F(armed.Grip).z;
                var bins = new SortedDictionary<int, Vector3>(); // x: underside on the centre line, y: top on it, z: lowest anywhere
                foreach (var f in g.GetComponentsInChildren<MeshFilter>())
                {
                    var r = f.GetComponent<Renderer>();
                    if (r == null || !r.enabled || f.sharedMesh == null) continue;
                    Vector3 lo = Vector3.one * 1e9f, hi = -lo;
                    foreach (var v in f.sharedMesh.vertices)
                    {
                        var c = F(f.transform.TransformPoint(v));
                        lo = Vector3.Min(lo, c); hi = Vector3.Max(hi, c);
                        int bin = Mathf.FloorToInt((c.z - gripZ) * 50);
                        var b = bins.TryGetValue(bin, out var x) ? x : new Vector3(9, -9, 9);
                        if (Mathf.Abs(c.x) < 0.012f) { b.x = Mathf.Min(b.x, c.y); b.y = Mathf.Max(b.y, c.y); }
                        b.z = Mathf.Min(b.z, c.y);
                        bins[bin] = b;
                    }
                    log.AppendLine($"  part {f.name}: z {(lo.z - gripZ) * 100:F1}..{(hi.z - gripZ) * 100:F1}, y {lo.y * 100:F1}..{hi.y * 100:F1}, x {lo.x * 100:F1}..{hi.x * 100:F1}");
                    if (kind == Armed.Scar && (f.name == "Object_78" || f.name == "Object_84" || f.name == "Object_86"))
                    {
                        // an iron sight seen down the bore: up its middle, where there is no metal (the rear's aperture; over the front's post, under its hood)
                        float mid = (lo.x + hi.x) / 2;
                        var ys = new List<float>();
                        foreach (var v in f.sharedMesh.vertices) { var c = F(f.transform.TransformPoint(v)); if (Mathf.Abs(c.x - mid) < 0.003f) ys.Add(c.y); }
                        ys.Sort();
                        var gaps = new List<(float from, float to)>();
                        for (int k = 1; k < ys.Count; k++) gaps.Add((ys[k - 1], ys[k]));
                        gaps.Sort((a, b) => (b.to - b.from).CompareTo(a.to - a.from));
                        log.AppendLine($"    up its middle (x {mid * 100:F2}), {ys.Count} points from {ys[0] * 100:F2} to {ys[ys.Count - 1] * 100:F2}; widest gaps: " + string.Join(", ", gaps.Take(3).Select(g => $"{g.from * 100:F2}..{g.to * 100:F2}")));
                    }
                    if (!f.name.Contains("scope") && !f.name.Contains("suppressor")) continue;
                    // a sight's or a barrel's cross-section, cm by cm: where its axis runs
                    var slices = new SortedDictionary<int, (Vector2 lo, Vector2 hi)>();
                    foreach (var v in f.sharedMesh.vertices)
                    {
                        var c = F(f.transform.TransformPoint(v));
                        int k = Mathf.FloorToInt((c.z - gripZ) * 100);
                        var e = slices.TryGetValue(k, out var had) ? had : (new Vector2(9, 9), new Vector2(-9, -9));
                        slices[k] = (Vector2.Min(e.Item1, new Vector2(c.x, c.y)), Vector2.Max(e.Item2, new Vector2(c.x, c.y)));
                    }
                    foreach (var kv in slices)
                        log.AppendLine($"    {kv.Key,4} cm: x {kv.Value.lo.x * 100:F1}..{kv.Value.hi.x * 100:F1} (middle {(kv.Value.lo.x + kv.Value.hi.x) * 50:F2}), y {kv.Value.lo.y * 100:F1}..{kv.Value.hi.y * 100:F1} (middle {(kv.Value.lo.y + kv.Value.hi.y) * 50:F2})");
                }
                var palm = F(armed.PalmOf(false));
                log.AppendLine($"{kind.Name}: grip {-gripZ * 100:F1} cm behind the muzzle and {F(armed.Grip).y * 100:F1} from the bore; the clip's left palm {(palm.z - gripZ) * 100:F1} forward of the grip, {palm.y * 100:F1} up, {palm.x * 100:F1} right");
                foreach (var kv in bins) log.AppendLine($"  {kv.Key * 2,4} cm: centre underside {kv.Value.x * 100:F1}, top {kv.Value.y * 100:F1}, lowest {kv.Value.z * 100:F1}");
                Object.Destroy(p.gameObject);
                yield return null;
            }
            File.WriteAllText(Path.Combine(output, "fore-ends.txt"), log.ToString());
        }

        [UnityTest]
        public IEnumerator Hands()
        {
            const string output = "Evidence/local/rebuild/hands";
            Directory.CreateDirectory(output);
            var key = new GameObject("Key").AddComponent<Light>();
            key.type = LightType.Directional; key.intensity = 1.3f; key.transform.rotation = Quaternion.Euler(35, 155, 0);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.45f, 0.45f, 0.47f);
            var cam = new GameObject("Cam").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.62f, 0.63f, 0.65f);
            cam.fieldOfView = 24;
            const int W = 900, H = 900;
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
            var read = new Texture2D(W, H, TextureFormat.RGB24, false);
            cam.targetTexture = rt;
            byte[] Shot() { RenderTexture.active = rt; read.ReadPixels(new Rect(0, 0, W, H), 0, 0); read.Apply(false); RenderTexture.active = null; return read.EncodeToJPG(92); }
            var log = new StringBuilder();
            foreach (var (tag, clip, gun) in Clips)
                foreach (var who in Who)
                {
                    var p = Person.Spawn(who, Vector3.zero, 180);
                    Assert.IsNotNull(p, who);
                    if (who != OnFoot.Who)
                    {
                        // sized to their height as the street and the fights size them (NpcCrowd, Fighter.Spawn), before the gun, which keeps its own size
                        var record = NpcRoster.Load("People/fighters").people.Concat(NpcRoster.Load().people).FirstOrDefault(r => r.model == who);
                        float height = record != null ? record.height : who.StartsWith("npc_f") ? 1.65f : 1.78f;
                        float natural = p.Body.GetBoneTransform(HumanBodyBones.Head).position.y / 0.885f;
                        p.transform.localScale = Vector3.one * (height / natural);
                        NpcCrowd.Dress(p, new NpcRecord { id = who, model = who, skin = 1 });
                    }
                    p.Play(clip);
                    var armed = gun != null ? Armed.Give(p, gun) : null;
                    yield return new WaitForSeconds(0.8f);
                    string held = "";
                    if (armed != null)
                    {
                        // the right palm on the grip; the left palm on the fore-end of a rifle (its distance from the bore's line), or over the right hand on a pistol
                        var bore = armed.Muzzle - armed.Grip;
                        var left = armed.PalmOf(false);
                        float offLine = Vector3.Cross(bore.normalized, left - armed.Grip).magnitude;
                        held = $" | right palm to grip {Vector3.Distance(armed.PalmOf(true), armed.Grip) * 100:F1} cm, left palm off the bore {offLine * 100:F1} cm, {Vector3.Dot(left - armed.Grip, bore.normalized) * 100:F0} cm along it"
                            + (armed.Gun.Pistol ? "" : $", {Vector3.Distance(left, armed.Fore) * 100:F1} cm from the fore-end hold");
                    }
                    if (armed != null && !armed.Gun.Pistol)
                    {
                        // where the rifle is raised to: the eye Armed uses, against the head bone, in the person's own frame (forward, up, right), in cm
                        var head = p.Body.GetBoneTransform(HumanBodyBones.Head).position;
                        var e = p.transform.InverseTransformDirection(Armed.EyeOf(p.Body, p.transform) - head) * 100;
                        var sh = p.transform.InverseTransformDirection(p.Body.GetBoneTransform(HumanBodyBones.RightUpperArm).position - head) * 100;
                        var guess = head + p.transform.forward * 0.08f + p.transform.up * 0.03f + p.transform.right * 0.032f; // the eye Armed used before 12 Nov
                        held += $" | eye to the sight line {Vector3.Distance(Armed.EyeOf(p.Body, p.transform), armed.SightLine) * 100:F1} cm, eye from the old guess {Vector3.Distance(Armed.EyeOf(p.Body, p.transform), guess) * 100:F1} cm";
                        held += $" | eye from head: fwd {e.z:F1} up {e.y:F1} right {e.x:F1}; right shoulder from head: fwd {sh.z:F1} up {sh.y:F1} right {sh.x:F1}; head bone {p.Body.GetBoneTransform(HumanBodyBones.Head).position.y * 100:F0} cm up, scale {p.transform.lossyScale.y:F3} / {p.Body.transform.lossyScale.y:F3}; arm {(p.Body.GetBoneTransform(HumanBodyBones.LeftLowerArm).position - p.Body.GetBoneTransform(HumanBodyBones.LeftUpperArm).position).magnitude * 100:F1}+{(p.Body.GetBoneTransform(HumanBodyBones.LeftHand).position - p.Body.GetBoneTransform(HumanBodyBones.LeftLowerArm).position).magnitude * 100:F1}";
                    }
                    log.AppendLine($"{who} {tag} {Measure(p.Body, true)} | {Measure(p.Body, false)}{held}");
                    foreach (var left in new[] { true, false })
                    {
                        var hand = p.Body.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
                        var mid = p.Body.GetBoneTransform(left ? HumanBodyBones.LeftMiddleProximal : HumanBodyBones.RightMiddleProximal);
                        var centre = (hand.position + mid.position) * 0.5f;
                        foreach (var (view, dir) in new[] { ("front", Vector3.back), ("side", left ? Vector3.left : Vector3.right) })
                        {
                            // the figure faces -z (yaw 180), so "front" looks at it from -z; "side" from the hand's own side
                            cam.transform.position = centre + (view == "front" ? Vector3.back : (left ? Vector3.right : Vector3.left)) * 0.75f + Vector3.up * 0.05f;
                            cam.transform.LookAt(centre);
                            yield return null;
                            cam.Render();
                            RenderTexture.active = rt; read.ReadPixels(new Rect(0, 0, W, H), 0, 0); read.Apply(false); RenderTexture.active = null;
                            File.WriteAllBytes(Path.Combine(output, $"{tag}_{who}_{(left ? "L" : "R")}_{view}.jpg"), read.EncodeToJPG(92));
                        }
                    }
                    if (armed != null && !armed.Gun.Pistol)
                    {
                        // the head on the shouldered rifle, from the right, at the eye's height: the eye on the sights' line
                        var at = armed.SightLine + armed.Held.forward * 0.12f;
                        cam.transform.position = at + Vector3.left * 0.7f;
                        cam.transform.LookAt(at);
                        // a red bead where Armed takes the eye to be, drawn over everything so it shows even inside the eyeball
                        var bead = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                        Object.Destroy(bead.GetComponent<Collider>());
                        bead.transform.position = Armed.EyeOf(p.Body, p.transform);
                        bead.transform.localScale = Vector3.one * 0.006f;
                        var red = new Material(Shader.Find("Dad/Debug Lines"));
                        red.SetColor("_BaseColor", Color.red);
                        bead.GetComponent<Renderer>().sharedMaterial = red;
                        yield return null;
                        cam.Render();
                        Object.Destroy(bead); Object.Destroy(red);
                        File.WriteAllBytes(Path.Combine(output, $"{tag}_{who}_sight.jpg"), Shot());
                        // and what that eye sees down the gun: the sight picture, with the person hidden so the camera can sit in the eye
                        var hidden = new List<Renderer>();
                        foreach (var r in p.GetComponentsInChildren<Renderer>()) if (r.enabled && !r.transform.IsChildOf(armed.Held)) { r.enabled = false; hidden.Add(r); }
                        cam.transform.SetPositionAndRotation(armed.SightLine, Quaternion.LookRotation(armed.Held.forward, armed.Held.up));
                        cam.nearClipPlane = 0.01f; cam.fieldOfView = 18;
                        yield return null;
                        cam.Render();
                        File.WriteAllBytes(Path.Combine(output, $"{tag}_{who}_picture.jpg"), Shot());
                        foreach (var r in hidden) r.enabled = true;
                        cam.nearClipPlane = 0.3f; cam.fieldOfView = 24;
                    }
                    Object.Destroy(p.gameObject);
                    yield return null;
                }
            File.WriteAllText(Path.Combine(output, "angles.txt"), log.ToString());
            Debug.Log("HANDS\n" + log);
            cam.targetTexture = null; Object.Destroy(rt);
        }
    }
}
