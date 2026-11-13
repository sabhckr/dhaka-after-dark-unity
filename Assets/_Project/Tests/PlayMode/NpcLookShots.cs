// Pictures of each person on the roster in a plain lit room, to look at by eye (2026-11-10): whole body, the face while talking and blinking, walking, and the hair as the
// head turns. Explicit: it writes pictures to Evidence/local/npclook and checks nothing. Run by name.
using System.Collections;
using System.IO;
using Dad.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Dad.Tests
{
    [Explicit("pictures to look at: run by name")]
    public class NpcLookShots
    {
        [UnityTest]
        public IEnumerator EachPersonFromAround()
        {
            string output = "Evidence/local/npclook";
            Directory.CreateDirectory(output);
            var sun = new GameObject("Key").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.6f; sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(35, 150, 0);
            var fill = new GameObject("Fill").AddComponent<Light>();
            fill.type = LightType.Directional; fill.intensity = 0.5f; fill.color = new Color(0.75f, 0.82f, 1f);
            fill.transform.rotation = Quaternion.Euler(20, -60, 0);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.32f, 0.32f, 0.34f);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.transform.localScale = Vector3.one * 3;
            var cam = new GameObject("Cam").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.42f, 0.43f, 0.45f);
            cam.nearClipPlane = 0.02f;
            var rt = new RenderTexture(1280, 1280, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var read = new Texture2D(1280, 1280, TextureFormat.RGB24, false);
            cam.targetTexture = rt;
            Time.captureFramerate = 60; // game time steps 1/60 s a frame however slow the render, so a 150 ms blink is caught
            void Shot(string name)
            {
                cam.Render();
                RenderTexture.active = rt;
                read.ReadPixels(new Rect(0, 0, 1280, 1280), 0, 0);
                read.Apply(false);
                RenderTexture.active = null;
                File.WriteAllBytes(Path.Combine(output, name + ".jpg"), read.EncodeToJPG(94));
            }
            foreach (var who in NpcRoster.Load().people)
            {
                var p = Person.Spawn(who.model, Vector3.zero, 180);
                Assert.IsNotNull(p, who.model);
                NpcCrowd.Dress(p, who); // as the street shows them
                var life = p.Body.GetComponent<PersonLife>();
                yield return new WaitForSeconds(0.6f);
                var head = p.Body.GetBoneTransform(HumanBodyBones.Head);
                float tall = head.position.y + 0.12f;
                cam.fieldOfView = 30;
                cam.transform.position = new Vector3(0.9f, tall * 0.55f, -3.6f);
                cam.transform.LookAt(new Vector3(0, tall * 0.5f, 0));
                yield return null; Shot(who.model + "_1_body");
                var face = head.position + Vector3.up * 0.07f;
                cam.fieldOfView = 22;
                cam.transform.position = face + new Vector3(0.18f, 0.02f, -0.62f);
                cam.transform.LookAt(face);
                if (life != null) life.Talking = true;
                for (int i = 0; i < 4; i++) { yield return new WaitForSeconds(0.13f); Shot(who.model + "_2_talk" + i); }
                if (life != null) life.Talking = false;
                // a blink: wait for one to be under way
                float until = Time.time + 7;
                var renderers = p.GetComponentsInChildren<SkinnedMeshRenderer>();
                while (Time.time < until)
                {
                    yield return null;
                    bool shut = false;
                    foreach (var r in renderers) { var m = r.sharedMesh; if (m == null) continue; int b = m.GetBlendShapeIndex("Eye_Blink_L"); if (b < 0) for (int k = 0; k < m.blendShapeCount; k++) if (m.GetBlendShapeName(k).EndsWith("Eye_Blink_L")) b = k; if (b >= 0 && r.GetBlendShapeWeight(b) > 70) shut = true; }
                    if (shut) { Shot(who.model + "_3_blink"); break; }
                }
                cam.fieldOfView = 30;
                cam.transform.position = new Vector3(3.4f, tall * 0.55f, 0);
                cam.transform.LookAt(new Vector3(0, tall * 0.5f, 0));
                p.Speed = 1.4f;
                yield return new WaitForSeconds(1.3f); Shot(who.model + "_4_walk");
                p.Speed = 0;
                yield return new WaitForSeconds(0.8f);
                cam.fieldOfView = 26;
                cam.transform.position = head.position + new Vector3(-0.35f, 0.05f, 1.3f);
                cam.transform.LookAt(head.position + Vector3.down * 0.1f);
                for (int i = 0; i <= 12; i++) { p.transform.rotation = Quaternion.Euler(0, 180 + 55 * Mathf.Sin(i / 12f * Mathf.PI), 0); yield return null; }
                Shot(who.model + "_5_hair_swing");
                yield return new WaitForSeconds(1.2f); Shot(who.model + "_6_hair_rest");
                Object.Destroy(p.gameObject);
                yield return null;
            }
            Time.captureFramerate = 0;
            cam.targetTexture = null;
            Object.Destroy(rt);
        }
    }
}
