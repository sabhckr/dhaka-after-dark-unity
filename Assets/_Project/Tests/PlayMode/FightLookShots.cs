// Pictures of a fight on a sidewalk (2026-11-10): the gang among the crowd, the crowd running at the first shots, the police walking in, the exchange, the fallen and their
// guns on the ground. Explicit: it writes pictures to Evidence/local/fight and checks nothing. Run by name.
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
    public class FightLookShots
    {
        [UnityTest]
        public IEnumerator AFightOnTheStreet()
        {
            string output = "Evidence/local/fight";
            Directory.CreateDirectory(output);
            Random.InitState(11);
            Time.captureFramerate = 30;
            bool built = ChunkStreamer.BuildWalks;
            var origin = new GameObject("origin").AddComponent<FloatingOrigin>();
            var sun = new GameObject("Key").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.6f; sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(40, 120, 0);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.34f, 0.34f, 0.36f);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.transform.localScale = new Vector3(12, 1, 12);
            var cam = new GameObject("Cam").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.42f, 0.43f, 0.45f);
            cam.fieldOfView = 50;
            cam.transform.position = new Vector3(15, 9, -21);
            cam.transform.LookAt(new Vector3(15, 0.5f, 0));
            var rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var read = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            cam.targetTexture = rt;
            void Shot(string name)
            {
                Snap(name);
                var (at, look, fov) = (cam.transform.position, cam.transform.rotation, cam.fieldOfView);
                cam.transform.position = new Vector3(4.5f, 2.2f, -5.5f);
                cam.transform.LookAt(new Vector3(0, 0.9f, 0));
                cam.fieldOfView = 40;
                Snap(name + "_close");
                cam.transform.SetPositionAndRotation(at, look);
                cam.fieldOfView = fov;
            }
            void Snap(string name)
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
            var walks = new ChunkWalks();
            walks.Lines.Add(new WalkLine { East = new double[] { 3, 3 }, North = new double[] { -25, 25 }, Height = new[] { 0f, 0f }, Along = new double[] { 0, 50 } });
            crowd.Use(walks);
            var precinct = Precinct.Attach(host);
            precinct.Delay = 3;
            precinct.From = 32;
            var roster = NpcRoster.Load("People/fighters");
            var gang = roster.people.Where(p => p.role == "gang").Select((p, i) => Fighter.Spawn(p, Fighter.Side.Gang, new Vector3(0, 0, i * 1.6f - 1.6f), 90)).ToArray();
            float t = 0;
            IEnumerator For(float seconds) { float until = Time.time + seconds; while (Time.time < until) yield return null; t += seconds; }
            yield return For(2); Shot("1_gang_on_the_street");
            Danger.Report(gang[0].transform.position + Vector3.up, Danger.Kind.Shot);
            yield return For(1.5f); Shot("2_crowd_runs");
            yield return For(3); Shot("3_police_arrive");
            var fighters = gang.Concat(precinct.Officers).ToArray();
            int frame = 0;
            float end = Time.time + 60;
            int fallen = 0;
            while (Time.time < end && !(gang.All(g => g.Dead) || precinct.Officers.All(p => p.Dead)))
            {
                yield return null;
                if (++frame % 30 == 0 && frame <= 300) Shot($"4_exchange_{frame / 30:D2}");
                int now = fighters.Count(f => f.Dead);
                if (now > fallen) { fallen = now; Shot($"5_falls_{fallen}"); }
            }
            yield return For(4); Shot("6_after");
            File.WriteAllText(Path.Combine(output, "tally.txt"), string.Join("\n", fighters.Select(f => $"{f.name} {f.Team} fired {f.Fired} struck {f.Struck} health {f.Health:F0}")));
            Time.captureFramerate = 0;
            ChunkStreamer.BuildWalks = built;
            cam.targetTexture = null;
            Object.Destroy(rt);
            foreach (var p in Object.FindObjectsOfType<Person>()) Object.Destroy(p.gameObject);
            foreach (var b in Object.FindObjectsOfType<Rigidbody>()) Object.Destroy(b.gameObject);
            Object.Destroy(host); Object.Destroy(origin.gameObject); Object.Destroy(floor);
            yield return null;
        }
    }
}
