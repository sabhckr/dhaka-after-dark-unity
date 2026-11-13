// Pictures for the character rebuild (2026-11-12): the player (OnFoot.Who), as the street dresses them, front, side and back, in the model's own rest pose and
// standing, walking, running and crouching. Also a clean front picture of the rest pose for the image-to-3D model. Explicit: it writes pictures to Evidence/local/rebuild
// and checks nothing. Run by name.
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dad.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Dad.Tests
{
    [Explicit("pictures to look at: run by name")]
    public class RebuildShots
    {
        public static string Model = "", Set = "before";
        static readonly (string model, string id)[] People = { (OnFoot.Who, "PLAYER") };

        [UnityTest]
        public IEnumerator FrontSideBack()
        {
            string output = "Evidence/local/rebuild/" + Set;
            Directory.CreateDirectory(output);
            var key = new GameObject("Key").AddComponent<Light>();
            key.type = LightType.Directional; key.intensity = 1.3f; key.shadows = LightShadows.Soft;
            var fill = new GameObject("Fill").AddComponent<Light>();
            fill.type = LightType.Directional; fill.intensity = 0.55f; fill.color = new Color(0.85f, 0.9f, 1f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.42f, 0.42f, 0.44f);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.transform.localScale = Vector3.one * 3;
            floor.GetComponent<Renderer>().material.color = new Color(0.55f, 0.55f, 0.56f);
            var cam = new GameObject("Cam").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.nearClipPlane = 0.05f;
            const int W = 1536, H = 1536;
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var read = new Texture2D(W, H, TextureFormat.RGB24, false);
            cam.targetTexture = rt;
            void Shot(string name, bool png = false)
            {
                cam.Render();
                RenderTexture.active = rt;
                read.ReadPixels(new Rect(0, 0, W, H), 0, 0);
                read.Apply(false);
                RenderTexture.active = null;
                File.WriteAllBytes(Path.Combine(output, name + (png ? ".png" : ".jpg")), png ? read.EncodeToPNG() : read.EncodeToJPG(94));
            }
            var roster = NpcRoster.Load("People/fighters");
            foreach (var (model, id) in People)
            {
                if (Model != "" && Model != model) continue;
                var who = roster.people.FirstOrDefault(p => p.id == id); // none for the player: he is dressed as he is
                var p = Person.Spawn(model, Vector3.zero, 0);
                Assert.IsNotNull(p, model);
                if (who != null) NpcCrowd.Dress(p, who);
                var head = p.Body.GetBoneTransform(HumanBodyBones.Head);
                p.Stride = head.position.y; // as NpcCrowd sizes a stride: 0.885 of the height, the head bone standing at 0.885 of it
                yield return null;

                // the model's own rest pose: every bone as the file has it
                p.enabled = false;
                p.Body.enabled = false;
                var file = Resources.Load<GameObject>(Person.Folder + model).transform;
                var rest = new Dictionary<string, Transform>();
                foreach (var t in file.GetComponentsInChildren<Transform>(true)) rest[Named(t, file)] = t;
                var figure = p.Body.transform;
                foreach (var t in figure.GetComponentsInChildren<Transform>(true))
                    if (t != figure && rest.TryGetValue(Named(t, figure), out var r)) { t.localPosition = r.localPosition; t.localRotation = r.localRotation; t.localScale = r.localScale; }
                yield return null;
                float tall = head.position.y + 0.14f;

                IEnumerator Views(string pose)
                {
                    foreach (var (view, yaw) in new[] { ("front", 0f), ("side", 90f), ("back", 180f) })
                    {
                        var dir = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
                        cam.fieldOfView = 24;
                        float back = tall * 0.56f / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
                        cam.transform.position = new Vector3(0, tall * 0.5f, 0) + dir * back;
                        cam.transform.LookAt(new Vector3(0, tall * 0.5f, 0));
                        key.transform.rotation = Quaternion.LookRotation(-dir) * Quaternion.Euler(30, -35, 0);
                        fill.transform.rotation = Quaternion.LookRotation(-dir) * Quaternion.Euler(10, 50, 0);
                        cam.backgroundColor = new Color(0.62f, 0.63f, 0.65f);
                        yield return null;
                        Shot($"{model}_{pose}_{view}");
                    }
                }

                // the clean front picture for the image-to-3D model: plain white behind, no floor, even light
                floor.SetActive(false);
                cam.fieldOfView = 24;
                cam.transform.position = new Vector3(0, tall * 0.5f, tall * 0.56f / Mathf.Tan(12 * Mathf.Deg2Rad));
                cam.transform.LookAt(new Vector3(0, tall * 0.5f, 0));
                cam.backgroundColor = Color.white;
                key.transform.rotation = Quaternion.LookRotation(Vector3.back) * Quaternion.Euler(25, -25, 0);
                fill.transform.rotation = Quaternion.LookRotation(Vector3.back) * Quaternion.Euler(10, 40, 0);
                yield return null;
                Shot($"{model}_input_front", true);
                floor.SetActive(true);

                yield return Views("0_rest");
                p.Body.enabled = true;
                p.enabled = true;
                yield return null;
                p.Pose(0, 0.5);
                yield return Views("1_standing");
                p.Pose(p.WalkAtOwnPace, 0.35);
                yield return Views("2_walking");
                p.Pose(p.Stride * Person.RunPace("run"), 0.2);
                yield return Views("3_running");
                p.enabled = true;
                p.Play("crouch-idle", 0);
                yield return null;
                p.Pose(0, 0.5);
                yield return Views("4_crouching");
                Object.Destroy(p.gameObject);
                yield return null;
            }
            cam.targetTexture = null;
            Object.Destroy(rt);
            Object.Destroy(cam.gameObject); Object.Destroy(key.gameObject); Object.Destroy(fill.gameObject); Object.Destroy(floor);
        }

        static string Named(Transform t, Transform root)
        {
            var s = t.name;
            for (var u = t.parent; u != null && u != root; u = u.parent) s = u.name + "/" + s;
            return s;
        }
    }
}
