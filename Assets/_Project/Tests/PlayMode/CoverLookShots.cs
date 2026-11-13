// Pictures of the way they fight (2026-11-12, Docs/Characters/SHOOTER_BAR_AUDIT.md section 11): a gang member down behind a low wall (from the side, and as the officer
// sees it), up over it firing, reloading behind it; the gang on their turf standing about, then with their guns out at a shot. Explicit: it writes pictures to
// Evidence/local/cover and checks nothing. Run by name.
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
    public class CoverLookShots
    {
        static NpcRecord Record(string id) => NpcRoster.Load("People/fighters").people.First(p => p.id == id);

        [UnityTest]
        public IEnumerator CoverAndTurf()
        {
            string output = "Evidence/local/cover";
            Directory.CreateDirectory(output);
            Random.InitState(5);
            Time.captureFramerate = 30;
            var sun = new GameObject("Key").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.6f; sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(40, 120, 0);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.34f, 0.34f, 0.36f);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.transform.localScale = new Vector3(12, 1, 12);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.layer = WorldLayers.World;
            wall.transform.position = new Vector3(0, 0.65f, 9);
            wall.transform.localScale = new Vector3(4, 1.3f, 0.4f);
            wall.GetComponent<Renderer>().material.color = new Color(0.6f, 0.58f, 0.54f);
            var cam = new GameObject("Cam").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.42f, 0.43f, 0.45f);
            var rt = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var read = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            cam.targetTexture = rt;
            void Snap(string name, Vector3 from, Vector3 at, float fov)
            {
                cam.transform.position = from;
                cam.transform.LookAt(at);
                cam.fieldOfView = fov;
                cam.Render();
                RenderTexture.active = rt;
                read.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
                read.Apply(false);
                RenderTexture.active = null;
                File.WriteAllBytes(Path.Combine(output, name + ".jpg"), read.EncodeToJPG(92));
            }
            yield return null;
            Physics.SyncTransforms();
            var gang = Fighter.Spawn(Record("GANG_002"), Fighter.Side.Gang, new Vector3(1, 0, 14), 180);
            var police = Fighter.Spawn(Record("POLICE_001"), Fighter.Side.Police, new Vector3(0, 0, -10), 0);
            police.HoldFire = true;
            police.Health = 1e6f;
            police.Stay = true;
            bool down = false, up = false, reload = false;
            int fired = 0;
            float downSince = -1, until = Time.time + 40;
            while (Time.time < until && !(down && up && reload))
            {
                yield return null;
                if (gang.Now != Fighter.Mode.Hiding) downSince = -1; else if (downSince < 0) downSince = Time.time;
                var spot = gang.Spot.At;
                var side = spot + new Vector3(4.5f, 1.4f, 1.5f);
                if (!down && downSince > 0 && Time.time - downSince > 0.5f && !gang.Reloading)
                {
                    Snap("cover_1_down", side, spot + Vector3.up * 0.7f, 40);
                    var eye = police.Body.Body.GetBoneTransform(HumanBodyBones.Head).position;
                    Snap("cover_1_down_as_the_officer_sees_it", eye + (spot - eye).normalized * 0.3f, spot + Vector3.up * 0.9f, 18);
                    down = true;
                }
                if (!up && gang.Now == Fighter.Mode.Peeking && gang.Fired > fired && gang.Body.Speed < 0.05f) { Snap("cover_2_up_firing", side, spot + Vector3.up * 1.0f, 40); up = true; } // a shot this frame
                fired = gang.Fired;
                if (!reload && gang.Reloading && gang.Now == Fighter.Mode.Hiding && gang.Body.UpperWeight > 0.6f)
                {
                    Snap("cover_3_reloading", side, spot + Vector3.up * 0.7f, 40);
                    var eye = police.Body.Body.GetBoneTransform(HumanBodyBones.Head).position;
                    Snap("cover_3_reloading_as_the_officer_sees_it", eye + (spot - eye).normalized * 0.3f, spot + Vector3.up * 0.9f, 18);
                    reload = true;
                }
            }
            Debug.Log($"[Dad] cover pictures: down {down}, up {up}, reload {reload}");
            foreach (var f in Object.FindObjectsByType<Fighter>(FindObjectsSortMode.None)) Object.Destroy(f.gameObject);
            Object.Destroy(wall);
            yield return null;
            // the gang on their turf
            var host = new GameObject("turf");
            var turf = host.AddComponent<Turf>();
            turf.Put(new Vector3(30, 0, 0), Vector3.right, (0, 0));
            yield return new WaitForSeconds(1.5f);
            Snap("turf_1_standing_about", new Vector3(30 + 5, 2.0f, -5), new Vector3(30, 1.0f, 0), 40);
            Danger.Report(new Vector3(30, 1, 30), Danger.Kind.Shot);
            yield return new WaitForSeconds(1.5f);
            Snap("turf_2_guns_out_at_a_shot", new Vector3(30 + 5, 2.0f, -5), new Vector3(30, 1.0f, 0), 40);
            Time.captureFramerate = 0;
            cam.targetTexture = null;
            Object.Destroy(rt);
            foreach (var f in Object.FindObjectsByType<Fighter>(FindObjectsSortMode.None)) Object.Destroy(f.gameObject);
            foreach (var r in Object.FindObjectsByType<Rigidbody>(FindObjectsSortMode.None)) Object.Destroy(r.gameObject);
            Object.Destroy(host); Object.Destroy(floor); Object.Destroy(sun.gameObject); Object.Destroy(cam.gameObject);
            Navigation.Forget();
            Cover.Clear();
        }
    }
}
