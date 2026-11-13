// Pictures of each gun in Resources/Weapons as the file has it (2026-11-10), from the side and from above, with the model's axes drawn (x red, y green, z blue), to read which
// way each one points. Explicit: it writes pictures to Evidence/local/weapons and checks nothing. Run by name.
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Dad.Tests
{
    [Explicit("pictures to look at: run by name")]
    public class WeaponLookShots
    {
        [UnityTest]
        public IEnumerator EachGunAsTheFileHasIt()
        {
            string output = "Evidence/local/weapons";
            Directory.CreateDirectory(output);
            var sun = new GameObject("Key").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.5f;
            sun.transform.rotation = Quaternion.Euler(50, 30, 0);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.45f, 0.45f, 0.47f);
            var cam = new GameObject("Cam").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.8f, 0.8f, 0.82f);
            cam.orthographic = true;
            var rt = new RenderTexture(1200, 800, 24);
            var read = new Texture2D(1200, 800, TextureFormat.RGB24, false);
            cam.targetTexture = rt;
            foreach (var name in new[] { "scar", "ump", "glock/scene" })
            {
                var asset = Resources.Load<GameObject>("Weapons/" + name);
                Assert.IsNotNull(asset, name);
                var gun = Object.Instantiate(asset);
                yield return null;
                var b = new Bounds(gun.transform.position, Vector3.zero);
                foreach (var r in gun.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
                float size = b.extents.magnitude;
                for (int a = 0; a < 3; a++)
                {
                    var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    var axis = a == 0 ? Vector3.right : a == 1 ? Vector3.up : Vector3.forward;
                    bar.transform.position = b.center + axis * size * 0.6f;
                    bar.transform.localScale = Vector3.one * size * 0.05f + axis * size * 0.5f;
                    bar.GetComponent<Renderer>().material.color = a == 0 ? Color.red : a == 1 ? Color.green : Color.blue;
                    bar.name = "axis";
                }
                cam.orthographicSize = size * 0.75f;
                foreach (var (view, from) in new[] { ("side", Vector3.right), ("top", Vector3.up), ("front", Vector3.forward) })
                {
                    cam.transform.position = b.center + from * size * 4;
                    cam.transform.LookAt(b.center, from == Vector3.up ? Vector3.forward : Vector3.up);
                    cam.Render();
                    RenderTexture.active = rt;
                    read.ReadPixels(new Rect(0, 0, 1200, 800), 0, 0);
                    read.Apply(false);
                    RenderTexture.active = null;
                    File.WriteAllBytes(Path.Combine(output, name.Replace('/', '_') + "_" + view + ".jpg"), read.EncodeToJPG(90));
                }
                File.AppendAllText(Path.Combine(output, "bounds.txt"), $"{name}: centre {b.center.ToString("F4")} size {b.size.ToString("F4")} root rotation {gun.transform.rotation.eulerAngles} scale {gun.transform.localScale}\n");
                Object.Destroy(gun);
                foreach (var g in Object.FindObjectsOfType<GameObject>()) if (g.name == "axis") Object.Destroy(g);
                yield return null;
            }
            cam.targetTexture = null;
            Object.Destroy(rt);
        }
    }
}
