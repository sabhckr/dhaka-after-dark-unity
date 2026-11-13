// ACT I, THE TRAP (00:00-00:25): the diner.
#if UNITY_EDITOR
using System;
using UnityEngine;
using Dad.Core;

namespace Dad.Trailer
{
    public sealed partial class Acts
    {
        void Add(string n, double a, double b, Action<float, float> pose, Action<float, float> cam = null) => Shots.Add(new Shot(n, a, b, pose, cam));

        void Diner()
        {
            var s = Stage;
            Env(new Color(.11f, .13f, .13f), new Color(.1f, .11f, .12f));
            s.Cam.clearFlags = CameraClearFlags.SolidColor; s.Cam.backgroundColor = new Color(.01f, .012f, .018f);
            var set = s.Model("diner/bench_inner/booth02.dae", Vector3.zero, 0, .0254f); s.Dress(set, "diner/x");
            var tube = s.Card(new Vector3(-.3f, 2.92f, .05f), new Vector3(1.3f, .03f, .14f), Color.white, 0, Stage.Glow(new Color(.86f, 1f, .9f), 3.2f));
            var key = s.Lamp(LightType.Spot, new Vector3(-.3f, 2.85f, .05f), new Vector3(-.2f, .7f, -.1f), new Color(.9f, 1f, .92f), 22, 8, 125, true);
            var key2 = s.Lamp(LightType.Point, new Vector3(1.5f, 2.3f, 3f), new Vector3(0, 0, 0), new Color(.7f, .9f, .85f), 2.2f, 8, 60, false);
            s.Grade(.0f, 14, -14, new Color(.97f, 1f, .97f));
            s.BloomAt(.5f, 1.05f);
            foreach (var r in set.GetComponentsInChildren<Renderer>()) if (r.sharedMaterial != null && r.sharedMaterial.name.StartsWith("glass")) { var gm = new Material(r.sharedMaterial); gm.SetColor("_BaseColor", new Color(.015f, .02f, .03f)); gm.SetFloat("_Smoothness", .95f); r.sharedMaterial = gm; }
            var papers = s.Model("papers/Paper & Envelope.fbx", new Vector3(-.28f, .795f, -.88f), 90, .42f); s.Dress(papers, "papers/x");
            var usb = s.Model("usb/USB Flash Drive.fbx", new Vector3(.75f, .797f, -.05f), 0); s.Dress(usb, "usb/x");
            var photo = s.Card(new Vector3(.45f, .797f, .05f), new Vector3(.15f, .0015f, .10f), new Color(.82f, .8f, .74f), 25);
            var elara = s.CastModel("elara/olgapose1.fbx", ElaraBones, new Vector3(-.3f, 0, -1.47f), 0);
            var marcus = s.CastModel("marcus/Businessman.glb", Mixamo(), new Vector3(1.34f, 0, .2f), 270);
            s.Voice("ELARA", elara); s.Voice("MARCUS", marcus);
            elara.Floor = marcus.Floor = 0;
            float Flick(float t) => t < .9f ? ((Mathf.PerlinNoise(t * 21, 3) > .45f) ? 1 : .15f) : 1 + (Mathf.PerlinNoise(t * 9, 7) - .5f) * .12f * (Mathf.PerlinNoise(t * 1.3f, 2) > .68f ? 3 : 1);
            void Light(float absolute) { float f = Flick(absolute); key.intensity = 22 * f; key2.intensity = 2.2f * f; tube.GetComponent<Renderer>().sharedMaterial.SetColor("_BaseColor", new Color(.86f, 1f, .9f) * (3.2f * f)); }
            Vector3 Head(Actor a) => a.Bone(HumanBodyBones.Head).position;
            Vector3 Hand(Actor a, bool left) => a.Bone(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand).position;
            var table = new Vector3(-.28f, .80f, -.88f);
            void Hands(float t, float shake, float grip = 0)
            {
                float n1 = (Mathf.PerlinNoise(t * 19, 1) - .5f) * shake, n2 = (Mathf.PerlinNoise(t * 17, 9) - .5f) * shake;
                elara.Reach(true, new Vector3(-.5f + n1, .835f, -1.0f + n2), new Vector3(-1.2f, .5f, -1.5f));
                elara.Reach(false, new Vector3(-.1f - n2, .835f, -1.0f + n1), new Vector3(.6f, .5f, -1.5f));
            }
            void E(float t, string clip = "sit-idle", float over = 0) { elara.Pose(clip, over + t); elara.Look(table + new Vector3(.2f, 0, .2f), .85f); }
            void MarcusSit(float t) { marcus.Pose("sit-idle", 2 + t * .7f); marcus.Look(Head(elara), .7f); }

            Add("A0-black", 0, 2, (t, u) => { Light(t); s.Grade(-16, 0, 0, Color.white); elara.Root.SetActive(false); marcus.Root.SetActive(false); }, (t, u) => s.Cam.transform.position = new Vector3(0, 1, 5));
            Add("A1-eyes", 2, 4.5f, (t, u) => { elara.Root.SetActive(true); marcus.Root.SetActive(false); s.Grade(.0f, 14, -14, new Color(.97f, 1f, .97f)); Light(2 + t); E(t, "sit-idle", .5f); elara.Look(new Vector3(.55f, 1.28f, .1f), 1); Hands(t, .006f); },
                (t, u) => { var h = Head(elara) + new Vector3(.03f, .01f, .1f); s.CamMove(h + new Vector3(.2f, -.03f, .48f), h, h + new Vector3(.16f, -.03f, .38f), h, u, 15, 13, .0008f, t); s.Focus(Vector3.Distance(s.Cam.transform.position, h), 85, 9); });
            Add("A2-envelope", 4.5f, 6.5f, (t, u) => { Light(4.5f + t); E(t + 2); Hands(t, .006f); },
                (t, u) => { s.CamMove(table + new Vector3(.5f, .26f, .42f), table + new Vector3(.0f, -.02f, -.05f), table + new Vector3(.38f, .22f, .32f), table + new Vector3(0, -.02f, -.05f), u, 30, 30, .0007f, t); s.Focus(.6f, 50, 4f); });
            Add("A3-hands", 6.5f, 8.5f, (t, u) => { Light(6.5f + t); E(t + 3); Hands(t, .018f); },
                (t, u) => { var c = new Vector3(-.3f, .84f, -1.0f); s.CamMove(c + new Vector3(.34f, .17f, .4f), c, c + new Vector3(.27f, .14f, .3f), c, u, 30, 28, .0009f, t); s.Focus(.43f, 85, 9); });
            Add("A4-slides-in", 8.5f, 11, (t, u) => { marcus.Root.SetActive(true); Light(8.5f + t); E(t + 1); Hands(t, .007f);
                    MarcusSit(t * .4f + 1); marcus.Look(papers.transform.position, .7f); },
                (t, u) => { var c = new Vector3(1.05f, 1.05f, .05f); s.CamMove(new Vector3(.32f, 1.38f, -2.0f), c, new Vector3(.34f, 1.36f, -1.85f), c, u, 34, 32, .001f, t); s.Focus(Vector3.Distance(s.Cam.transform.position, marcus.Root.transform.position), 50, 4f); });
            Add("A5-marcus", 11, 13.4f, (t, u) => { Light(11 + t); MarcusSit(t); E(t + 4); Hands(t, .007f); },
                (t, u) => { var h = Head(marcus) + new Vector3(0, .13f, 0); s.CamMove(h + new Vector3(-.75f, -.03f, -.32f), h, h + new Vector3(-.62f, -.03f, -.27f), h + new Vector3(0, .01f, 0), u, 30, 28, .0008f, t); s.Focus(.74f, 85, 9); });
            Add("A6-photo", 13.4f, 15.4f, (t, u) => { Light(13.4f + t); MarcusSit(t + 3); float d = Mathf.SmoothStep(0, 1, Mathf.Clamp01(t / .9f)); var pos = Vector3.Lerp(new Vector3(1.0f, .797f, .12f), new Vector3(.6f, .797f, -.08f), d); photo.transform.position = pos; marcus.Reach(false, pos + new Vector3(.09f, .05f, .03f), new Vector3(1.9f, .6f, -.4f)); },
                (t, u) => { var c = new Vector3(.65f, .84f, -.1f); s.CamMove(c + new Vector3(-.62f, .38f, -.52f), c, c + new Vector3(-.5f, .32f, -.42f), c, u, 28, 26, .0006f, t); s.Focus(.82f, 50, 4f); });
            Add("A7-reaction", 15.4f, 19, (t, u) => { Light(15.4f + t); elara.Pose("sit-idle", 1.4 + t * .25f); Hands(t, .016f); elara.Look(photo.transform.position + Vector3.up * .55f, .55f); },
                (t, u) => { var h = Head(elara) + new Vector3(0, .02f, .08f); s.CamMove(h + new Vector3(.24f, -.06f, .46f), h, h + new Vector3(.19f, -.05f, .36f), h, u, 24, 21, .0008f, t); s.Focus(Vector3.Distance(s.Cam.transform.position, h), 85, 9); });
            Add("A8-drive", 19, 21.4f, (t, u) => { photo.SetActive(false); Light(19 + t); MarcusSit(t + 5); float d = Mathf.SmoothStep(0, 1, Mathf.Clamp01((t - .4f) / .85f)); usb.transform.position = Vector3.Lerp(new Vector3(.95f, .797f, .05f), new Vector3(-.12f, .797f, -.68f), d); usb.transform.rotation = Quaternion.Euler(0, 20 + d * 25, 0);
                    if (t < .75f) marcus.Reach(false, usb.transform.position + new Vector3(.06f, .05f, .02f), new Vector3(1.9f, .6f, -.4f)); },
                (t, u) => { var c = new Vector3(.25f, .8f, -.42f); s.CamMove(c + new Vector3(-.62f, .13f, -.66f), c, c + new Vector3(-.56f, .12f, -.6f), c, u, 30, 30, .0005f, t); s.Focus(.9f, 50, 2.8f); });
            Add("A9-stands", 21.4f, 23.5f, (t, u) => { Light(21.4f + t); elara.Pose("sit-idle", 1 + t); Hands(t, .007f); float d = Mathf.Clamp01(t / 1.6f); marcus.Pose("sit-stand", d * 2.26f, null, 0, 0, true); marcus.Look(Head(elara), .3f); },
                (t, u) => { var c = new Vector3(1.34f, 1.15f, .2f); s.CamMove(new Vector3(-.75f, 1.2f, 1.0f), c, new Vector3(-.72f, 1.18f, .96f), c + Vector3.up * .45f, u, 40, 40, .001f, t); s.Focus(1.55f, 50, 4f); });
            Add("A10-takes-it", 23.5f, 25, (t, u) => { marcus.Root.SetActive(false); Light(23.5f + t); elara.Pose("sit-idle", 2 + t); elara.Look(usb.transform.position, .9f); float d = Mathf.SmoothStep(0, 1, Mathf.Clamp01(t / .7f)); var r = Vector3.Lerp(new Vector3(-.1f, .835f, -1.0f), new Vector3(-.13f, .822f, -.7f), d); elara.Reach(false, r, new Vector3(.6f, .5f, -1.5f)); elara.Reach(true, new Vector3(-.5f, .835f, -1.0f), new Vector3(-1.2f, .5f, -1.5f)); },
                (t, u) => { if (t > .78f && usb.transform.parent != elara.Bone(HumanBodyBones.RightHand)) usb.transform.SetParent(elara.Bone(HumanBodyBones.RightHand), true);
                    var c = new Vector3(-.13f, .84f, -.72f); s.CamMove(c + new Vector3(.36f, .15f, .36f), c, c + new Vector3(.3f, .13f, .3f), c + new Vector3(0, .02f, -.02f), u, 30, 28, .0009f, t); s.Focus(.5f, 85, 9); });
        }
    }
}
#endif
