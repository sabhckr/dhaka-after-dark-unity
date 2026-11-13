// Pictures of the player walking every way and jumping (2026-11-12): aiming ahead and going right, back, left and on a diagonal, bare-handed, with a rifle and with a pistol,
// four moments of a step each; then a standing and a running jump, frame by frame. Explicit: it writes pictures to Evidence/local/strafe and checks nothing. Run by name.
using System.Collections;
using System.IO;
using Dad.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Dad.Tests
{
    [Explicit("pictures to look at: run by name")]
    public class StrafeLookShots
    {
        [UnityTest]
        public IEnumerator EveryWayAndJumps()
        {
            string output = "Evidence/local/strafe";
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
            floor.transform.localScale = Vector3.one * 20;
            var cam = new GameObject("Cam").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.42f, 0.43f, 0.45f);
            cam.nearClipPlane = 0.02f;
            cam.fieldOfView = 34;
            var rt = new RenderTexture(900, 900, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var read = new Texture2D(900, 900, TextureFormat.RGB24, false);
            cam.targetTexture = rt;
            void Shot(string name)
            {
                cam.Render();
                RenderTexture.active = rt;
                read.ReadPixels(new Rect(0, 0, 900, 900), 0, 0);
                read.Apply(false);
                RenderTexture.active = null;
                File.WriteAllBytes(Path.Combine(output, name + ".jpg"), read.EncodeToJPG(90));
            }
            Time.captureFramerate = 30;
            var you = OnFoot.Spawn(new Vector3(0, 0.05f, 0), 0, null, null);
            void Look(float fromAngle, float height = 1.0f)
            {
                var at = you.transform.position + Vector3.up * height;
                cam.transform.position = at + Quaternion.Euler(0, fromAngle, 0) * new Vector3(0, 0.3f, -4.2f);
                cam.transform.LookAt(at);
            }
            var guns = new (string name, Armed.Kind kind)[] { ("bare", null), ("rifle", Armed.Scar), ("pistol", Armed.Glock) };
            var ways = new (string name, Vector2 move)[] { ("right", new Vector2(1, 0)), ("back", new Vector2(0, -1)), ("left", new Vector2(-1, 0)), ("diagonal", new Vector2(0.7f, 0.7f)) };
            foreach (var (gunName, kind) in guns)
            {
                Armed armed = null;
                if (kind != null) { you.Gun = kind; armed = Armed.Give(you.Person, kind.Name); }
                you.Facing = 0;
                foreach (var (wayName, move) in ways)
                {
                    you.transform.position = new Vector3(0, 0.05f, 0);
                    you.Override = move;
                    yield return new WaitForSeconds(1f);
                    for (int i = 0; i < 4; i++)
                    {
                        Look(-50);
                        yield return null;
                        Shot($"{gunName}_{wayName}_{i}");
                        for (int k = 0; k < 4; k++) yield return null;
                    }
                }
                you.Override = null;
                yield return new WaitForSeconds(0.5f);
                if (armed != null) { if (armed.Held != null) Object.Destroy(armed.Held.gameObject); Object.Destroy(armed); }
                you.Gun = null;
                yield return null;
            }
            // jumps: standing, then running, a frame every 0.1 s
            foreach (bool running in new[] { false, true })
            {
                you.transform.position = new Vector3(0, 0.05f, 0);
                you.Facing = null;
                you.Override = running ? new Vector2(0, 1) : (Vector2?)null;
                you.Run = running;
                yield return new WaitForSeconds(running ? 1f : 0.6f);
                float ground = you.transform.position.y;
                you.Jump();
                for (int i = 0; i < 9; i++)
                {
                    var at = new Vector3(you.transform.position.x, ground + 1.0f, you.transform.position.z);
                    cam.transform.position = at + new Vector3(-4.4f, 0.2f, 0);
                    cam.transform.LookAt(at);
                    yield return null;
                    Shot($"jump_{(running ? "running" : "standing")}_{i}");
                    yield return null; yield return null;
                }
                you.Override = null;
                you.Run = false;
                yield return new WaitForSeconds(0.8f);
            }
            Time.captureFramerate = 0;
            cam.targetTexture = null;
            Object.Destroy(rt);
            Object.Destroy(you.gameObject);
        }
    }
}
