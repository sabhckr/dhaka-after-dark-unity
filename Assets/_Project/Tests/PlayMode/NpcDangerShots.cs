// Pictures of the roster on a sidewalk before and after a shot (2026-11-10): going about their business, startled, scattering, running. Explicit: it writes pictures to
// Evidence/local/npcdanger and checks nothing. Run by name.
using System.Collections;
using System.IO;
using System.Linq;
using Dad.Core;
using Dad.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Dad.Tests
{
    [Explicit("pictures to look at: run by name")]
    public class NpcDangerShots
    {
        [UnityTest]
        public IEnumerator BeforeAndAfterAShot()
        {
            string output = "Evidence/local/npcdanger";
            Directory.CreateDirectory(output);
            bool built = ChunkStreamer.BuildWalks;
            var origin = new GameObject("origin").AddComponent<FloatingOrigin>();
            var sun = new GameObject("Key").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.6f; sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(40, 120, 0);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.34f, 0.34f, 0.36f);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.transform.localScale = new Vector3(3, 1, 12);
            var cam = new GameObject("Cam").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.42f, 0.43f, 0.45f);
            cam.fieldOfView = 40;
            cam.transform.position = new Vector3(14, 6, -6);
            cam.transform.LookAt(new Vector3(0, 0.9f, 2));
            var rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var read = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            cam.targetTexture = rt;
            void Shot(string name)
            {
                cam.Render();
                RenderTexture.active = rt;
                read.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
                read.Apply(false);
                RenderTexture.active = null;
                File.WriteAllBytes(Path.Combine(output, name + ".jpg"), read.EncodeToJPG(92));
            }
            var host = new GameObject("crowd");
            var crowd = NpcCrowd.Attach(host, null, cam.transform);
            crowd.Manual = true;
            var walks = new ChunkWalks();
            walks.Lines.Add(new WalkLine { East = new double[] { 0, 0 }, North = new double[] { -25, 25 }, Height = new[] { 0f, 0f }, Along = new double[] { 0, 50 } });
            crowd.Use(walks);
            yield return null;
            crowd.Run(4);
            yield return null; Shot("1_street");
            var ids = NpcRoster.Load().people.Where(w => w.Walks).Select(w => w.id).ToArray();
            var at = new Vector3(0, 1, 2);
            Danger.Report(at, Danger.Kind.Shot);
            crowd.Run(0.35f); yield return null; Shot("2_startled");
            crowd.Run(1.2f); yield return null; Shot("3_scatter");
            crowd.Run(2.5f); yield return null; Shot("4_running");
            File.WriteAllText(Path.Combine(output, "moods.txt"), string.Join("\n", ids.Select(id => id + " " + crowd.Mood(id) + " " + Vector3.Distance(crowd.Where(id), at).ToString("F1") + " m")));
            ChunkStreamer.BuildWalks = built;
            cam.targetTexture = null;
            Object.Destroy(rt);
            foreach (var p in Object.FindObjectsOfType<Person>()) Object.Destroy(p.gameObject);
            Object.Destroy(host); Object.Destroy(origin.gameObject);
            yield return null;
        }
    }
}
