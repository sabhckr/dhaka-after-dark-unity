// The twenty Blender people as the game draws them (2026-11-12): men in one row, women in the next, each at their height, the tactical ones holding a rifle as
// the game's fighters do (its aim, Armed), everyone else standing. Any of them can be the player (Cast). Explicit: it writes pictures to Evidence/local/rebuild/lineup and checks nothing. Run by name.
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
    public class LineupShots
    {
        static readonly string[] Tactical = { "npc_m01", "npc_m02", "npc_m03", "npc_m04", "npc_f01", "npc_f02", "npc_f03" };

        // Dressed, at the height the street gives them (NpcCrowd, Fighter.Spawn), and doing what the lineup shows them doing: a tactical one holds the scar up.
        static void Stand(Person p, string who)
        {
            var record = NpcRoster.Load("People/fighters").people.Concat(NpcRoster.Load().people).FirstOrDefault(r => r.model == who);
            float height = record != null ? record.height : who.StartsWith("npc_f") ? 1.65f : 1.78f;
            p.transform.localScale = Vector3.one * (height / (p.Body.GetBoneTransform(HumanBodyBones.Head).position.y - p.transform.position.y) * 0.885f);
            NpcCrowd.Dress(p, new NpcRecord { id = who, model = who, skin = 1 });
            if (System.Array.IndexOf(Tactical, who) < 0) { p.Play("idle"); return; }
            p.Play(Armed.Scar.Idle);
            Armed.Give(p, "scar");
        }

        [UnityTest]
        public IEnumerator Lineup()
        {
            const string output = "Evidence/local/rebuild/lineup";
            Directory.CreateDirectory(output);
            var key = new GameObject("Key").AddComponent<Light>();
            key.type = LightType.Directional; key.intensity = 1.3f; key.shadows = LightShadows.Soft;
            key.transform.rotation = Quaternion.Euler(35, 155, 0);
            var fill = new GameObject("Fill").AddComponent<Light>();
            fill.type = LightType.Directional; fill.intensity = 0.5f; fill.transform.rotation = Quaternion.Euler(15, 230, 0);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.42f, 0.42f, 0.44f);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.transform.localScale = Vector3.one * 4;
            floor.GetComponent<Renderer>().material.color = new Color(0.5f, 0.5f, 0.52f);
            var cam = new GameObject("Cam").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.62f, 0.63f, 0.65f);
            cam.fieldOfView = 20;
            const int W = 3200, H = 1400;
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
            var read = new Texture2D(W, H, TextureFormat.RGB24, false);
            cam.targetTexture = rt;
            foreach (var row in new[] { "m", "f" })
            {
                var spawned = new System.Collections.Generic.List<Person>();
                for (int i = 0; i < 10; i++)
                {
                    string who = $"npc_{row}{i + 1:00}";
                    var p = Person.Spawn(who, new Vector3((i - 4.5f) * 0.95f, 0, 0), 180);
                    Assert.IsNotNull(p, who);
                    Stand(p, who);
                    spawned.Add(p);
                }
                yield return new WaitForSeconds(0.8f);
                foreach (var (name, at, look) in new[] { ("row", new Vector3(0, 1.0f, -14f), new Vector3(0, 0.95f, 0)), ("left", new Vector3(-2.4f, 1.45f, -6.5f), new Vector3(-2.4f, 1.15f, 0)), ("right", new Vector3(2.4f, 1.45f, -6.5f), new Vector3(2.4f, 1.15f, 0)) })
                {
                    cam.transform.position = at; cam.transform.LookAt(look);
                    yield return null;
                    cam.Render();
                    RenderTexture.active = rt; read.ReadPixels(new Rect(0, 0, W, H), 0, 0); read.Apply(false); RenderTexture.active = null;
                    File.WriteAllBytes(Path.Combine(output, $"{(row == "m" ? "men" : "women")}_{name}.jpg"), read.EncodeToJPG(93));
                }
                foreach (var p in spawned) Object.Destroy(p.gameObject);
                yield return null;
            }
            // close to each of the twenty, upright frames: collar to thigh from the front and three-quarter (weave, sheen, how the cloth sits,
            // where layers meet), and the face (skin, eyes, brows and lashes, hair line)
            const int CW = 1600, CH = 2000;
            var close = new RenderTexture(CW, CH, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
            var closeRead = new Texture2D(CW, CH, TextureFormat.RGB24, false);
            cam.targetTexture = close;
            for (int i = 0; i < 20; i++)
            {
                string who = i < 10 ? $"npc_m{i + 1:00}" : $"npc_f{i - 9:00}";
                var p = Person.Spawn(who, Vector3.zero, 180);
                Stand(p, who);
                yield return new WaitForSeconds(0.8f);
                var chest = p.Body.GetBoneTransform(HumanBodyBones.Chest).position;
                var head = p.Body.GetBoneTransform(HumanBodyBones.Head).position;
                foreach (var (name, yaw, fov, from, at) in new[] {
                    ("close", 0f, 30f, chest + Vector3.up * 0.05f, chest + Vector3.down * 0.12f),
                    ("close3q", 35f, 30f, chest + Vector3.up * 0.05f, chest + Vector3.down * 0.12f),
                    ("face", 20f, 18f, head + Vector3.up * 0.08f, head + Vector3.up * 0.07f) })
                {
                    var dir = Quaternion.Euler(0, yaw, 0) * Vector3.back;
                    cam.fieldOfView = fov;
                    cam.transform.position = from + dir * (name == "face" ? 0.95f : 1.5f);
                    cam.transform.LookAt(at);
                    yield return null;
                    cam.Render();
                    RenderTexture.active = close; closeRead.ReadPixels(new Rect(0, 0, CW, CH), 0, 0); closeRead.Apply(false); RenderTexture.active = null;
                    File.WriteAllBytes(Path.Combine(output, $"{who}_{name}.jpg"), closeRead.EncodeToJPG(93));
                }
                Object.Destroy(p.gameObject);
                yield return null;
            }
            cam.targetTexture = null; Object.Destroy(close);
            cam.targetTexture = null; Object.Destroy(rt);
        }
    }
}
