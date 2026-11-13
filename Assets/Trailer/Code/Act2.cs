// ACT II, street: the game's own Dhaka streets at night, with rain falling through them.
#if UNITY_EDITOR
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Dad.Core;
using Dad.Traffic;
using Dad.Vehicles;
using Dad.World;

namespace Dad.Trailer
{
    public sealed partial class Acts
    {
        IEnumerator Street()
        {
            Player.Automatic = false;
            SceneManager.LoadScene("Boot");
            yield return null; yield return null;
            var s = Stage;
            var origin = UnityEngine.Object.FindAnyObjectByType<FloatingOrigin>();
            var streamer = UnityEngine.Object.FindAnyObjectByType<ChunkStreamer>();
            var day = UnityEngine.Object.FindAnyObjectByType<DayCycle>();
            Camera main = Camera.main;
            if (main != null) { main.enabled = false; }
            s.Root = new GameObject("Act street").transform;
            s.BuildCamera();
            const double east = 2640, north = 2118;
            origin.Target = s.Cam.transform;
            origin.Recenter(east, north);
            var offset = origin.Offset;
            offset.ToScene(east, north, out float cx, out float cz);
            s.Cam.transform.position = new Vector3(cx, 1.6f, cz);
            GraphicsTier.Apply(Tier.High, s.Cam);
            day.MinutesPerSecond = 0;
            day.Set(DayCycle.DefaultDate, 23.2, Weather.Overcast);
            float t0 = Time.realtimeSinceStartup; int quiet = 0;
            while (quiet < 10 && Time.realtimeSinceStartup - t0 < 180) { yield return null; quiet = streamer.IndexReady && streamer.Idle && streamer.Applied > 0 && day.ReflectionsReady ? quiet + 1 : 0; }
            float ground = 0;
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared; RenderSettings.fogColor = new Color(.05f, .06f, .08f); RenderSettings.fogDensity = .02f;
            var c = new Vector3(cx, ground, cz);
            var dirA = new Vector3(0.82f, 0, 0.57f).normalized;       // along the street
            var side = Vector3.Cross(Vector3.up, dirA);
            float heading = Mathf.Atan2(dirA.x, dirA.z) * Mathf.Rad2Deg;
            var carAt = c + side * -2.4f;
            var car = PlayerVehicle.Spawn(VehicleKind.Sedan, carAt + Vector3.up * .05f, heading, day, new PaintJob { Base = 0, Second = -1, Shade = 1, Split = 1, Dust = 0 });
            car.Body.isKinematic = true;
            var elara = s.CastModel("elara/olgapose1.fbx", ElaraBones, c, heading);
            var leo = s.CastPerson("survival", c, heading);
            s.Voice("LEO", leo); s.Voice("ELARA", elara);
            elara.Floor = leo.Floor = ground;
            // light: headlamps and a shop's sign wash; the sky is the game's overcast night
            var lampL = s.Lamp(LightType.Spot, carAt + dirA * 2.5f + side * .6f + Vector3.up * .75f, carAt + dirA * 22 + Vector3.up * .2f, new Color(1, .94f, .82f), 55, 40, 48, true);
            var lampR = s.Lamp(LightType.Spot, carAt + dirA * 2.5f - side * .6f + Vector3.up * .75f, carAt + dirA * 22 + Vector3.up * .2f, new Color(1, .94f, .82f), 55, 40, 48, false);
            var sign = s.Lamp(LightType.Point, c + side * 7 + dirA * 6 + Vector3.up * 3.5f, c, new Color(.8f, .45f, .25f), 16, 22, 60, false);
            var cool = s.Lamp(LightType.Directional, c + Vector3.up * 40, c + dirA * 8 + side * 4, new Color(.55f, .65f, .85f), .25f, 10, 60, false);
            var wet = s.Card(c + Vector3.up * .012f, new Vector3(36, .002f, 36), Color.white, heading, null);
            var wm = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            wm.SetFloat("_Surface", 1); wm.SetFloat("_Blend", 0); wm.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha); wm.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha); wm.SetInt("_ZWrite", 0); wm.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); wm.renderQueue = 3000;
            wm.SetColor("_BaseColor", new Color(.01f, .012f, .015f, .28f)); wm.SetFloat("_Smoothness", .97f); wm.SetFloat("_Metallic", .1f);
            wet.GetComponent<Renderer>().sharedMaterial = wm; wet.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            fx.Add(s.Particles("rain", c + Vector3.up * 11, new Vector3(34, 1, 34), 5200, 0, 1.1f, .022f, new Color(.76f, .82f, .92f, .55f), true, new Vector3(1.2f, -18, .4f)));
            s.Grade(.55f, 14, -20, new Color(.88f, .93f, 1f));
            var dash = s.Lamp(LightType.Point, carAt, carAt, new Color(.6f, .72f, .95f), 0, 3, 60, false);
            var rim = s.Lamp(LightType.Spot, c, c + Vector3.forward, new Color(.72f, .82f, 1f), 0, 16, 75, false);
            void Rim(Vector3 at, float i) { rim.transform.position = at + side * -2.6f + dirA * 2.4f + Vector3.up * 3.2f; rim.transform.rotation = Quaternion.LookRotation(at + Vector3.up - rim.transform.position); rim.intensity = i; }
            s.BloomAt(.8f, 1.0f);
            Vector3 Head(Actor a) => a.Bone(HumanBodyBones.Head).position;
            var walkFrom = c + side * 3.2f - dirA * 7;
            Add("B1-street", 25, 27.5f, (t, u) => { elara.Root.SetActive(false); leo.Root.SetActive(false); },
                (t, u) => { var at = c + side * 6.5f - dirA * 7 + Vector3.up * .55f; var look = carAt + dirA * 1 + Vector3.up * .9f; s.CamMove(at, look, at + dirA * .5f, look, u, 40, 40, .004f, 25 + t); s.Focus(10, 35, 8); });
            Add("B2-watching", 27.5f, 30, (t, u) => { leo.Root.SetActive(true); leo.Floor = float.NaN; leo.Pose("drive", 1 + t); },
                (t, u) => { var seat = car.transform.TransformPoint(new Vector3(car.Eye.localPosition.x, Player.SeatHeight, Player.SeatBack)); leo.HipsAt(seat); leo.Root.transform.rotation = car.transform.rotation; leo.Look(seat + dirA * 12 + side * 6, .8f); leo.Finish();
                    var h = Head(leo); var at = h + car.transform.right * 1.15f * (car.Eye.localPosition.x > 0 ? 1 : -1) + dirA * .35f + Vector3.up * .04f; s.CamMove(at, h, at - dirA * .05f, h, u, 34, 32, .001f, 27.5f + t); dash.transform.position = h + dirA * .55f + Vector3.down * .25f; dash.intensity = 1.1f; s.Focus(Vector3.Distance(at, h), 50, 4); });
            Add("B3-into-rain", 30, 32.5f, (t, u) => { leo.Root.SetActive(false); elara.Root.SetActive(true); elara.Place(walkFrom + dirA * (t * 1.35f), heading); elara.Pose("walk", 1 + t * 1.05f); dash.intensity = 0; Rim(walkFrom + dirA * 1.7f, 42); },
                (t, u) => { var e = elara.Root.transform.position; var at = walkFrom - dirA * 4.2f + side * -1.1f + Vector3.up * 1.15f; s.CamMove(at, e + Vector3.up * 1.05f, at + dirA * 1.2f, e + Vector3.up * 1.05f, u, 34, 34, .003f, 30 + t); s.Focus(Vector3.Distance(s.Cam.transform.position, e), 50, 4); });
            Add("B4-follows", 32.5f, 34.5f, (t, u) => { leo.Root.SetActive(true); leo.Floor = ground; leo.Root.transform.rotation = Quaternion.Euler(0, heading, 0); elara.Place(walkFrom + dirA * (3.4f + t * 1.35f + 2.5f), heading); elara.Pose("walk", 3 + t * 1.05f); leo.Place(walkFrom - dirA * 4 + dirA * (t * 1.35f) + side * -3.4f, heading + 12); leo.Pose("walk", .3 + t * 1.05f); },
                (t, u) => { var e = leo.Root.transform.position; var at = e - dirA * 3.2f + side * -1.3f + Vector3.up * 1.2f; s.CamMove(at, e + Vector3.up * 1.3f + dirA * 3, at + dirA * .3f, e + Vector3.up * 1.3f + dirA * 3, u, 40, 40, .003f, 32.5f + t); Rim(walkFrom - dirA * 2.6f + side * -3.4f, 42); s.Focus(4f, 50, 4); });
        }
    }
}
#endif
