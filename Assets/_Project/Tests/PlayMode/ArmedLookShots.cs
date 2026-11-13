// Pictures of each person holding each gun (2026-11-10): the rifles in the aiming clip, the pistol in the pistol stance, from the side, from the front and close on the hands,
// and one shot with the flash. Explicit: it writes pictures to Evidence/local/armed and checks nothing. Run by name.
using System.Collections;
using System.IO;
using System.Linq;
using Dad.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Dad.Tests
{
    [Explicit("pictures to look at: run by name")]
    public class ArmedLookShots
    {
        [UnityTest]
        public IEnumerator EachPersonWithEachGun()
        {
            string output = "Evidence/local/armed";
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
            void Shot(string name)
            {
                cam.Render();
                RenderTexture.active = rt;
                read.ReadPixels(new Rect(0, 0, 1280, 1280), 0, 0);
                read.Apply(false);
                RenderTexture.active = null;
                File.WriteAllBytes(Path.Combine(output, name + ".jpg"), read.EncodeToJPG(92));
            }
            Time.captureFramerate = 60; // a flash lasts 50 ms of game time, however slow the render
            var people = NpcRoster.Load().people.GroupBy(w => w.model).Select(g => g.First()).ToArray();
            foreach (var who in people)
                foreach (var gun in new[] { Armed.Scar, Armed.Ump, Armed.Glock })
                {
                    var p = Person.Spawn(who.model, Vector3.zero, 180);
                    Assert.IsNotNull(p, who.model);
                    NpcCrowd.Dress(p, who);
                    p.Play(gun.Idle);
                    var armed = Armed.Give(p, gun.Name);
                    Assert.IsNotNull(armed, gun.Name);
                    yield return new WaitForSeconds(0.8f);
                    var head = p.Body.GetBoneTransform(HumanBodyBones.Head);
                    float tall = head.position.y + 0.12f;
                    string name = who.model + "_" + gun.Name;
                    cam.fieldOfView = 30;
                    cam.transform.position = new Vector3(-3.4f, tall * 0.6f, 0); // his right side: he faces -z
                    cam.transform.LookAt(new Vector3(0, tall * 0.6f, 0));
                    yield return null; Shot(name + "_1_side");
                    cam.transform.position = new Vector3(-1.4f, tall * 0.7f, -3.2f);
                    cam.transform.LookAt(new Vector3(0, tall * 0.6f, 0));
                    yield return null; Shot(name + "_2_front");
                    var hand = p.Body.GetBoneTransform(HumanBodyBones.RightHand).position;
                    cam.fieldOfView = 24;
                    cam.transform.position = hand + new Vector3(-0.9f, 0.25f, 0.2f);
                    cam.transform.LookAt(hand);
                    yield return null; Shot(name + "_3_hands");
                    armed.Fire();
                    cam.fieldOfView = 30;
                    cam.transform.position = new Vector3(-2.4f, tall * 0.7f, -2.4f);
                    cam.transform.LookAt(new Vector3(0, tall * 0.6f, -0.3f));
                    yield return null; Shot(name + "_4_fire");
                    Object.Destroy(p.gameObject);
                    yield return null;
                }
            Time.captureFramerate = 0;
            cam.targetTexture = null;
            Object.Destroy(rt);
        }
    }
}
