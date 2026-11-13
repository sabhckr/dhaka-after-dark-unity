// Pictures of what shots leave (2026-11-12): holes in stone, metal, wood and glass, each a moment after the shot (what is thrown up) and once it has settled (the hole),
// and a body shot in front of a wall (blood in the air, then on the wall and the street). Explicit: it writes pictures to Evidence/local/impacts and checks nothing.
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
    public class ImpactLookShots
    {
        [UnityTest]
        public IEnumerator HolesDustAndBlood()
        {
            string output = "Evidence/local/impacts";
            Directory.CreateDirectory(output);
            var sun = new GameObject("Key").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.4f; sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(40, 160, 0);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.35f, 0.35f, 0.37f);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.transform.localScale = Vector3.one * 4;
            floor.GetComponent<Renderer>().material.color = new Color(0.45f, 0.44f, 0.42f);
            var cam = new GameObject("Cam").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.4f, 0.42f, 0.45f);
            cam.nearClipPlane = 0.02f;
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
            Time.captureFramerate = 60;
            var panels = new (string name, Color colour)[] { ("stone wall", new Color(0.62f, 0.6f, 0.56f)), ("metal door", new Color(0.35f, 0.4f, 0.45f)), ("wood crate", new Color(0.55f, 0.42f, 0.28f)), ("glass window", new Color(0.3f, 0.38f, 0.42f)) };
            for (int k = 0; k < panels.Length; k++)
            {
                var (name, colour) = panels[k];
                var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
                panel.name = name;
                panel.layer = WorldLayers.World;
                panel.transform.position = new Vector3(k * 4, 1.2f, 3);
                panel.transform.localScale = new Vector3(2.4f, 2.4f, 0.2f);
                panel.GetComponent<Renderer>().material.color = colour;
                yield return null;
                Physics.SyncTransforms();
                var target = panel.transform.position + new Vector3(0, 0, -0.1f);
                cam.fieldOfView = 30;
                cam.transform.position = target + new Vector3(-0.9f, 0.25f, -1.8f);
                cam.transform.LookAt(target);
                for (int i = 0; i < 4; i++)
                {
                    var from = target + new Vector3(-1, 0.3f, -4) + new Vector3((i % 2) * 0.5f, (i / 2) * 0.4f, 0);
                    var aim = target + new Vector3(((i % 2) - 0.5f) * 0.5f, ((i / 2) - 0.5f) * 0.5f, 0);
                    if (Physics.Raycast(from, (aim - from).normalized, out var hit, 10)) Impacts.Hit(hit, from);
                }
                yield return null; yield return null; yield return null;
                Debug.Log($"[Dad] {name}: thrown dust {Impacts.Thrown("dust")}, chips {Impacts.Thrown("chips")}, sparks {Impacts.Thrown("sparks")}, splinters {Impacts.Thrown("splinters")}, shards {Impacts.Thrown("shards")}");
                Shot(name.Split(' ')[0] + "_1_thrown");
                yield return new WaitForSeconds(2f);
                Shot(name.Split(' ')[0] + "_2_settled");
            }
            // a body shot in front of a wall
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "stone wall";
            wall.layer = WorldLayers.World;
            wall.transform.position = new Vector3(20, 1.5f, 4.2f);
            wall.transform.localScale = new Vector3(5, 3, 0.4f);
            wall.GetComponent<Renderer>().material.color = new Color(0.7f, 0.68f, 0.64f);
            var p = Person.Spawn(NpcRoster.Load().people.First(w => w.Walks).model, new Vector3(20, 0, 3), 180);
            Bystander.Give(p);
            yield return new WaitForSeconds(0.5f);
            Physics.SyncTransforms();
            var chest = (p.Body.GetBoneTransform(HumanBodyBones.Spine).position + p.Body.GetBoneTransform(HumanBodyBones.Neck).position) / 2;
            var gunAt = new Vector3(19.6f, 1.4f, -1);
            cam.fieldOfView = 34;
            cam.transform.position = new Vector3(17.6f, 1.5f, 0.6f);
            cam.transform.LookAt(new Vector3(20, 1.1f, 3.4f));
            if (Hitbox.Cast(gunAt, (chest - gunAt).normalized, 10, Fighter.Hits, null, out var shot))
            {
                Impacts.Hit(shot, gunAt);
                Hitbox.Strike(shot, 30, gunAt, out _);
            }
            yield return null; yield return null;
            Shot("body_1_struck");
            yield return new WaitForSeconds(0.25f);
            Shot("body_2_after");
            if (Hitbox.Cast(gunAt, (chest + Vector3.up * 0.1f - gunAt).normalized, 10, Fighter.Hits, null, out shot))
            {
                Impacts.Hit(shot, gunAt);
                Hitbox.Strike(shot, 300, gunAt, out _);
            }
            yield return new WaitForSeconds(3f);
            Shot("body_3_down");
            Time.captureFramerate = 0;
            cam.targetTexture = null;
            Object.Destroy(rt);
            Impacts.Clear();
        }
    }
}
