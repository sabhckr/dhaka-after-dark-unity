// ACT II (end) and ACT III: the warehouse, 00:34.5 to 01:10. The model has no mezzanine, so Elara shoots from the top of a crate stack. Everything here is staged, shot by shot.
#if UNITY_EDITOR
using UnityEngine;
using Dad.Core;

namespace Dad.Trailer
{
    public sealed partial class Acts
    {
        static float Top(GameObject g) { var b = new Bounds(g.transform.position, Vector3.zero); bool first = true; foreach (var r in g.GetComponentsInChildren<Renderer>()) { if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds); } return b.max.y; }

        void Warehouse()
        {
            var s = Stage;
            const float F = -2.93f;
            Env(new Color(.13f, .15f, .19f), new Color(.1f, .115f, .14f));
            s.Cam.clearFlags = CameraClearFlags.SolidColor; s.Cam.backgroundColor = new Color(.012f, .018f, .03f);
            s.Model("warehouse/abandoned_warehouse_-_interior_scene.glb", Vector3.zero, 0);
            System.Action Base = () => s.Grade(.7f, 14, -20, new Color(.88f, .94f, 1f));
            Base();
            s.BloomAt(.7f, 1.0f); s.VignetteAt(.3f);
            s.Lamp(LightType.Directional, new Vector3(0, 30, 0), new Vector3(-2, 0, 3), new Color(.55f, .66f, .9f), .25f, 10, 60, true);
            var hang = s.Lamp(LightType.Spot, new Vector3(-1.2f, 2.8f, -6.2f), new Vector3(-1.2f, F, -6.2f), new Color(.86f, .94f, 1f), 150, 18, 96, true);
            var side = s.Lamp(LightType.Spot, new Vector3(9, 1.2f, -2), new Vector3(-8, F + 1, -7), new Color(.5f, .62f, .85f), 40, 30, 46, false);
            var sweep = s.Lamp(LightType.Spot, new Vector3(-12, 1.8f, -.4f), new Vector3(-12, F, -9), new Color(1, .94f, .8f), 0, 30, 70, true);
            var boom = s.Lamp(LightType.Point, new Vector3(-15, -1.2f, -3.2f), Vector3.zero, new Color(.95f, .97f, 1f), 0, 40, 60, true);
            var muz = s.Lamp(LightType.Point, Vector3.zero, Vector3.zero, new Color(1, .75f, .4f), 0, 12, 60, false);
            var key = s.Lamp(LightType.Spot, Vector3.zero, Vector3.forward, new Color(.74f, .84f, 1f), 0, 8, 46, false);
            void K(Vector3 from, Vector3 to, float i) { key.transform.position = from; key.transform.rotation = Quaternion.LookRotation(to - from); key.intensity = i; }
            fx.Add(s.Particles("dust", new Vector3(-1.5f, -.5f, -6), new Vector3(9, 5, 7), 26, .06f, 12, .02f, new Color(.7f, .78f, .95f, .22f)));
            var smoke = s.Particles("smoke", new Vector3(-11, F + 1, -4), new Vector3(10, 2, 8), 0, .25f, 7, 3.2f, new Color(.42f, .46f, .55f, .1f), false, new Vector3(.25f, .08f, 0)); fx.Add(smoke);
            var chips = s.Particles("chips", new Vector3(3, F + 1, -8.4f), new Vector3(.4f, .4f, .4f), 0, 7, .7f, .05f, new Color(.45f, .36f, .25f, .85f), false, null, 1.6f); fx.Add(chips);
            var muzzle = GameObject.CreatePrimitive(PrimitiveType.Sphere); Object.Destroy(muzzle.GetComponent<Collider>()); muzzle.transform.SetParent(s.Root, false); muzzle.transform.localScale = new Vector3(.1f, .1f, .1f); muzzle.GetComponent<Renderer>().sharedMaterial = Stage.Glow(new Color(1, .78f, .42f), 9); muzzle.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            muzzle.SetActive(false);
            GameObject Crate(Vector3 at, float yaw) => s.Model("crate/game-ready_wooden_crate.glb", at, yaw);
            float cr = Top(Crate(new Vector3(-.3f, F, -1.3f), 10)) - F;
            foreach (var (x, z, h, y) in new[] { (.95f, -1.3f, 0, -8f), (-.1f, -1.3f, 1, 25f), (-6.3f, -7.6f, 0, 40f), (-6.2f, -6.2f, 0, 20f), (-6f, -7f, 1, -5f), (-3.55f, -7.65f, 0, 12f) })
                Crate(new Vector3(x, F + h * cr, z), y);
            var belt = s.Model("conveyor/Conveyor_inner/Conveyor.FBX", new Vector3(2.4f, F, -8.4f), 0); s.Dress(belt, "conveyor/x");
            var suv = s.Model("suv/tayota_prado_2010_3d_model.glb", new Vector3(21, F, -5.5f), 270);
            s.Card(new Vector3(22, F - .02f, -5.5f), new Vector3(30, .04f, 40), new Color(.05f, .05f, .055f));
            var beamGlow = Stage.Glow(new Color(1, .95f, .82f), 40);
            s.Card(new Vector3(18.55f, F + .86f, -6.15f), new Vector3(.2f, .14f, .2f), Color.white, 0, beamGlow); s.Card(new Vector3(18.55f, F + .86f, -4.85f), new Vector3(.2f, .14f, .2f), Color.white, 0, beamGlow);
            var elara = s.CastModel("elara/olgapose1.fbx", ElaraBones, new Vector3(-3, F, -6.5f), 90);
            var leo = s.CastPerson("survival", new Vector3(6, F, -6.4f), 270);
            var g1 = s.CastPerson("contractor", new Vector3(-13, F, -3.5f), 90); var g2 = s.CastPerson("contractor", new Vector3(-13.6f, F, -6), 90);
            var vance = s.CastModel("eric/rp_eric_rigged_001_yup_t.fbx", Scan(), new Vector3(-12, F, -5), 90);
            foreach (var a in s.Actors) a.Floor = F;
            var pistolE = s.Model("glock/scene.gltf", Vector3.zero, 0, .055f); var pistolL = s.Model("glock/scene.gltf", Vector3.zero, 0, .055f);
            OneMag(pistolE); OneMag(pistolL);
            var rifle = s.Model("scar/scar.glb", new Vector3(-3.55f, F + cr + .05f, -7.65f), 0, .85f);
            rifle.transform.rotation = Quaternion.Euler(0, 15, 90);
            var ump = s.Model("ump/ump.glb", Vector3.zero, 0, .72f);
            var knife = s.Model("blade/scene.gltf", Vector3.zero, 0, .035f);
            knife.SetActive(false);
            Glock(elara, pistolE); Glock(leo, pistolL); Ump(vance, ump);
            var umpG1 = s.Model("ump/ump.glb", Vector3.zero, 0, .72f); var umpG2 = s.Model("ump/ump.glb", Vector3.zero, 0, .72f); Ump(g1, umpG1); Ump(g2, umpG2);
            foreach (var a in new[] { g1, g2 }) foreach (var r in a.Root.GetComponentsInChildren<Renderer>(true)) if (r.name == "Gun_Low") r.gameObject.SetActive(false);   // the model's own rifle: these two carry the UMP
            var front = new Vector3(3, F + 1.25f, -6.4f);
            s.Voice("ELARA", elara); s.Voice("LEO", leo); s.Voice("VANCE", vance);
            Vector3 Head(Actor a) => a.Bone(HumanBodyBones.Head).position;
            Vector3 Eye(Actor a) => Head(a) + a.Root.transform.forward * .08f + Vector3.up * .06f;
            void Off(params Actor[] xs) { foreach (var a in xs) a.Root.SetActive(false); }
            void On(params Actor[] xs) { foreach (var a in xs) a.Root.SetActive(true); }
            var facing = new Vector3(3, F + 1.55f, -6.35f);
            bool rifleHeld = false;
            void ElaraAims(float t) { elara.Place(new Vector3(-3, F, -6.5f), 90); elara.Pose("pistol-fire", .04f); elara.Aim(facing); }
            void LeoStood(float t) { leo.Place(new Vector3(1.0f, F, -6.3f), 270); leo.Pose("pistol-idle", 2.4f + t * .8f); leo.Look(Head(elara), .85f); }
            void Fire((Vector3 at, Vector3 dir) tip, bool on) { Flash(tip.at + tip.dir * .09f, on); if (on) { muzzle.transform.rotation = Quaternion.LookRotation(tip.dir, Vector3.up); muzzle.transform.localScale = new Vector3(.035f, .035f, .1f + Mathf.Repeat(tip.at.x * 97, .05f)); } }
            void Flash(Vector3 at, bool on) { muzzle.SetActive(on); muz.intensity = on ? 2.2f : 0; if (on) { muzzle.transform.position = at; muz.transform.position = at + Vector3.up * .05f; } }

            Off(g1, g2, vance);
            Add("W1-wide", 34.5f, 36.5f, (t, u) => { Off(leo, g1, g2, vance); ElaraAims(t); },
                (t, u) => { var at = new Vector3(11, F + .5f, -3.5f); var l = new Vector3(-5, F + 1.4f, -7); s.CamMove(at, l, at + new Vector3(-.5f, .05f, -.1f), l, u, 36, 36, 0, 0); s.Focus(0); });
            Add("W2-stay-back", 36.5f, 38.5f, (t, u) => { Off(g1, g2, vance); ElaraAims(t); },
                (t, u) => { var h = Head(elara) + Vector3.down * .25f; var at = h + new Vector3(1.5f, -.05f, -1.1f); s.CamMove(at, h, at + new Vector3(-.15f, 0, .1f), h, u, 34, 32, .0035f, t); K(at + new Vector3(0, .9f, .3f), h, 16); s.Focus(1.85f, 50, 4); });
            Add("W3-leo-enters", 38.5f, 41, (t, u) => { On(leo); float d = Mathf.Clamp01(t / 2.2f); leo.Place(new Vector3(Mathf.Lerp(5.4f, 1.0f, d), F, -6.3f), 270); leo.Pose(d < 1 ? "pistol-walk" : "pistol-idle", d < 1 ? t * .95f : 2.4f); ElaraAims(t); },
                (t, u) => { var at = new Vector3(-3.65f, F + 1.48f, -6.15f); var l = new Vector3(1.6f, F + 1.15f, -6.35f); s.CamMove(at, l, at + new Vector3(.05f, 0, 0), l, u, 36, 36, .002f, t); K(leo.Root.transform.position + new Vector3(-2, 2, 1.2f), leo.Root.transform.position + Vector3.up, 26); s.Focus(Vector3.Distance(at, leo.Root.transform.position), 50, 4); });
            Add("W4-ots", 41, 43.5f, (t, u) => { LeoStood(t); ElaraAims(t); },
                (t, u) => { var e = Head(elara) + new Vector3(0, -.15f, 0); var l = Head(leo); var at = l + new Vector3(.5f, .06f, .3f); s.CamMove(at, e, at + new Vector3(-.2f, 0, -.04f), e, u, 26, 24, .0015f, t); K(e + new Vector3(1.4f, 1, 1), e, 12); s.Focus(Vector3.Distance(at, e), 85, 9); });
            Add("W5-heart", 43.5f, 47, (t, u) => { Off(leo); elara.Place(new Vector3(-3, F, -6.5f), 90); elara.Pose("idle", 2 + t * .4f); elara.Look(facing, .8f); },
                (t, u) => { var h = Eye(elara); s.CamMove(h + new Vector3(.52f, -.17f, .2f), h, h + new Vector3(.52f, -.17f, .2f), h, u, 24, 24, 0, 0); K(h + new Vector3(.8f, .45f, .55f), h, .7f); s.Focus(.56f, 85, 9); });
            // ACT III
            Add("W6-headlights", 47, 49, (t, u) => { On(leo); LeoStood(t + 4); ElaraAims(t); leo.Look(new Vector3(0, F + 1.5f, 0), .9f); elara.Look(new Vector3(0, F + 1.5f, 0), .8f); sweep.intensity = 500; sweep.transform.position = new Vector3(Mathf.Lerp(-11, 9, u), 1.8f, -.4f); sweep.transform.rotation = Quaternion.LookRotation(new Vector3(-3, F - 1.8f, -8)); },
                (t, u) => { var at = new Vector3(-9.6f, F + 1.2f, -9.4f); var l = new Vector3(1, F + 1f, -3); s.CamMove(at, l, at + new Vector3(.3f, 0, .1f), l, u, 46, 46, 0, 0); key.intensity = 0; s.Focus(0); });
            Add("W7-suv", 49, 51, (t, u) => { sweep.intensity = 0; Off(leo, elara); },
                (t, u) => { var at = new Vector3(13.6f, F + .4f, -3.9f); var l = new Vector3(20, F + .9f, -5.5f); s.CamMove(at, l, at + new Vector3(.45f, 0, 0), l, u, 42, 42, 0, 0); hang.intensity = 60; side.intensity = 240; side.transform.position = new Vector3(14, 1, -3); side.transform.rotation = Quaternion.LookRotation(new Vector3(6, -.4f, -2.2f)); s.Focus(0); });
            Add("W8-not-my-ride", 51, 53, (t, u) => { On(leo, elara); hang.intensity = 150; side.intensity = 40; side.transform.position = new Vector3(9, 1.2f, -2); side.transform.rotation = Quaternion.LookRotation(new Vector3(-17, -.3f, -5)); LeoStood(t + 6); leo.Look(new Vector3(8, F + 3, -2), .9f); },
                (t, u) => { var h = Eye(leo); s.CamMove(h + new Vector3(-.58f, 0, .34f), h, h + new Vector3(-.5f, 0, .3f), h, u, 28, 26, .001f, t); K(h + new Vector3(-.5f, .6f, .9f), h, 2f); s.Focus(.65f, 85, 9); });
            Add("W9-rifle", 53, 54.5f, (t, u) => { Off(leo); pistolE.SetActive(false); if (t >= .65f && !rifleHeld) { rifleHeld = true; rifle.SetActive(true); elara.Arm(rifle, .96f, false, GlbMuzzle, Vector3.up, -.24f, -.067f, .163f, .03f, -.48f, 0, .086f).Map = GripMap.Scar; } elara.Place(new Vector3(-3.15f, F, -6.85f), 200); if (t < .65f) { elara.Pose("idle", t); elara.Reach(false, new Vector3(-3.5f, F + cr + .1f, -7.6f), new Vector3(-2.4f, F + .7f, -7.3f)); } else { elara.Pose("idle", t); elara.Aim(new Vector3(-12, F + 1.4f, -4.4f)); } elara.Look(t < .65f ? new Vector3(-3.5f, F + cr, -7.6f) : new Vector3(-12, F + 1.4f, -4.4f), .7f); },
                (t, u) => { var h = Head(elara) + Vector3.down * .35f; var at = new Vector3(-4.7f, F + 1.05f, -8f); s.CamMove(at, h, at + new Vector3(.1f, 0, .1f), h, u, 38, 36, .004f, t); K(at + new Vector3(.2f, 1, .2f), h, 18); s.Focus(Vector3.Distance(at, h), 50, 4); });
            // the fight: fast, clear, one action per shot; the back wall burns behind the smoke, so the gunmen read as silhouettes
            float Burn(float t) => 36 + Mathf.PerlinNoise(t * 7, 3) * 14;
            float Shot(float t, float every) => Mathf.Exp(-Mathf.Repeat(t, every) / .055f);
            Add("F2-flash", 54.5f, 55.7f, (t, u) => { Off(leo, elara); On(g1, g2); g1.Place(new Vector3(-12, F, -3.5f), 90); g2.Place(new Vector3(-12.7f, F, -6), 90); g1.Pose("rifle-walk", 0); g2.Pose("rifle-walk", .4f); g1.Aim(front); g2.Aim(front); float f = t > .5f ? Mathf.Clamp01(1 - (t - .5f) / .55f) : 0; boom.intensity = (t > .5f ? 60 : 4) + 3000 * f * f; s.Grade(.7f + f * f * 4, 14, -20, new Color(.9f, .95f, 1f)); if (t > .5f && t < .55f) smoke.Emit(60); },
                (t, u) => { key.intensity = 0; var at = new Vector3(-6, F + .6f, -3); var l = new Vector3(-16.5f, F + 1.5f, -3); s.CamMove(at, l, at, l, u, 38, 38, 0, 0); s.Focus(0); });
            Add("F3-gunmen", 55.7f, 56.9f, (t, u) => { Base(); boom.intensity = Burn(t); g1.Place(new Vector3(-12 + t * 1.3f, F, -3.5f), 90); g2.Place(new Vector3(-12.7f + t * 1.3f, F, -6), 90); g1.Pose("rifle-walk", t * 1.2f); g2.Pose("rifle-walk", .4f + t * 1.2f); g1.Aim(front); g2.Aim(front); },
                (t, u) => { var at = new Vector3(-5.5f, F + .35f, -4.5f); var l = new Vector3(-11, F + 1.5f, -4.6f); s.CamMove(at, l, at + new Vector3(.1f, 0, 0), l, u, 36, 36, .006f, t); s.Focus(0); });
            Add("F4-leo-fires", 56.9f, 58.1f, (t, u) => { Off(g1, g2); On(leo); leo.Place(new Vector3(3.3f, F, -8.4f), 270); leo.Pose("pistol-fire", .04f + t * .02f); leo.Aim(new Vector3(-10, F + 1.3f, -5)); leo.Kick(t < .75f ? Shot(t, .35f) : 0); leo.Feel(-1); },
                (t, u) => { var hand = leo.Bone(HumanBodyBones.RightHand).position; bool shot = t < .05f || (t > .35f && t < .4f); Fire(leo.Tip(pistolL), shot); var h = Head(leo); var at = new Vector3(2.55f, F + 1.42f, -5.95f); var aimed = h + new Vector3(-.3f, -.1f, 0); s.CamMove(at, aimed, at + new Vector3(.08f, 0, 0), aimed, u, 34, 34, .004f, t); K(new Vector3(1.6f, F + 1.9f, -7.3f), h, 18); s.Focus(Vector3.Distance(at, h), 50, 4); });
            Add("F5-falls", 58.1f, 59.2f, (t, u) => { Off(leo); On(g1); Flash(Vector3.zero, false); key.intensity = 0; boom.intensity = Burn(t); g1.Place(new Vector3(-9.8f, F, -3.5f), 90); umpG1.SetActive(false); g1.Pose("death-back", t * 1.4f, null, 0, 0, true); },
                (t, u) => { var at = new Vector3(-5.6f, F + .4f, -5); var l = new Vector3(-10, F + .9f, -3.5f); s.CamMove(at, l, at, l, u, 36, 36, .004f, t); s.Focus(0); });
            Add("F6-elara-high", 59.2f, 60.05f, (t, u) => { Off(g1); On(elara); elara.Floor = F + cr + .04f; elara.Place(new Vector3(-.1f, F + cr, -1.3f), 245); elara.Pose("crouch-aim", .4f + t * .1f); elara.Aim(new Vector3(-11, F + 1.1f, -4.2f)); elara.Look(new Vector3(-11, F + 1.1f, -4.2f), .8f); elara.Kick(t > .15f ? Shot(t - .15f, 2) : 0); },
                (t, u) => { pistolE.SetActive(false); bool shot = t > .15f && t < .2f; var hand = elara.Bone(HumanBodyBones.RightHand).position; Fire(elara.Tip(rifle), shot); var h = Head(elara) + new Vector3(-.25f, -.12f, -.1f); var at = h + new Vector3(.75f, -.1f, -2.05f); s.CamMove(at, h, at + new Vector3(.1f, 0, .1f), h, u, 34, 34, .004f, t); K(at + new Vector3(.2f, 1, -.2f), h, 14); s.Focus(2.2f, 50, 5.6f); });
            Add("F7-second", 60.05f, 61, (t, u) => { Off(elara); On(g2); elara.Floor = F; Flash(Vector3.zero, false); key.intensity = 0; boom.intensity = Burn(t); g2.Place(new Vector3(-9.3f, F, -6), 90); umpG2.SetActive(false); g2.Pose("death-chest", t * 1.4f, null, 0, 0, true); },
                (t, u) => { var at = new Vector3(-5.1f, F + 1.5f, -4.8f); var l = new Vector3(-9.6f, F + .85f, -6); s.CamMove(at, l, at, l, u, 30, 30, .006f, t); s.Focus(0); });
            Add("F8-vance", 61, 62, (t, u) => { Off(g2); On(vance); vance.Place(new Vector3(-12 + t * 1.2f, F, -5), 90); vance.Pose("rifle-walk", t * 1.15f); vance.Aim(front); boom.intensity = Burn(t); boom.transform.position = new Vector3(-15, -1, -5); },
                (t, u) => { var at = new Vector3(-5.8f, F + .3f, -5.2f); var l = new Vector3(-12, F + 1.3f, -5); s.CamMove(at, l, at, l, u, 34, 34, .002f, t); s.Focus(0); });
            Add("F9-smg", 62, 63, (t, u) => { boom.intensity = Burn(t); vance.Place(new Vector3(-10.5f, F, -5), 90); vance.Pose("rifle-fire", .05f + t * .03f); vance.Aim(front); vance.Kick((t < .85f ? Shot(t, .15f) : 0) * .55f); vance.Feel(-1, -.5f); },
                (t, u) => { bool on = Mathf.Repeat(t, .15f) < .075f && t < .85f; Fire(vance.Tip(ump), on); var at = new Vector3(-6.3f, F + 1.1f, -3.4f); var l = new Vector3(-10.5f, F + 1.2f, -5); s.CamMove(at, l, at, l, u, 32, 32, .012f, t); s.Focus(0); });
            Add("F10-return", 63, 64, (t, u) => { Off(vance); On(leo); Flash(Vector3.zero, false); boom.intensity = 0; if (t < .04f) chips.Emit(40); leo.Place(new Vector3(3.3f, F, -8.2f), 262); leo.Pose("pistol-fire", .04f + t * .02f); leo.Aim(new Vector3(-10, F + 1.3f, -5)); leo.Kick(Shot(t, .3f)); leo.Feel(-1); },
                (t, u) => { var h = Head(leo); var hand = leo.Bone(HumanBodyBones.RightHand).position; Fire(leo.Tip(pistolL), Mathf.Repeat(t, .3f) < .04f); var at = new Vector3(2.5f, F + 1.42f, -5.85f); var aimed = h + new Vector3(-.3f, -.1f, 0); s.CamMove(at, aimed, at + new Vector3(.08f, 0, 0), aimed, u, 34, 34, .01f, t); K(new Vector3(1.6f, F + 1.9f, -7.3f), h, 16); s.Focus(Vector3.Distance(at, h), 50, 4); });
            var wall = new Vector3(4.2f, F, -.5f);
            void Interrogate(float t) { Off(leo); On(elara, vance); Flash(Vector3.zero, false); boom.intensity = 0; pistolE.SetActive(false); ump.SetActive(false); rifle.SetActive(false); knife.SetActive(true); hang.transform.position = wall + new Vector3(.55f, 4.3f, -1); hang.transform.rotation = Quaternion.LookRotation(new Vector3(-.12f, -1, .2f)); hang.intensity = 34;
                elara.Place(wall + new Vector3(.03f, 0, -.74f), 0); elara.Pose("idle", 1 + t * .3f); vance.Floor = F; vance.Place(wall, 180); vance.Pose("idle", 2 + t * .3f); vance.Ground(); vance.Look(Head(elara) + Vector3.down * .1f, .9f); vance.Feel(1, -.6f); }
            var held = elara.TakeKnife(knife, .27f);
            void Blade(bool reverse)
            {
                var neck = Head(vance) + new Vector3(0, -.15f, -.1f);
                if (!reverse) { var d = new Vector3(.04f, .34f, .94f).normalized; elara.Stab(held, neck - d * .235f, d, Vector3.up, false, wall + new Vector3(.3f, -.2f, -1.3f)); }
                else { var d = new Vector3(-.45f, -.78f, .44f).normalized; var at = Head(vance) + new Vector3(.26f, -.22f, -.16f) - d * .225f; elara.Stab(held, at, d, new Vector3(-.2f, .7f, .7f), true, wall + new Vector3(1f, .9f, -.7f)); }
                elara.Reach(true, Head(vance) + new Vector3(-.12f, -.32f, -.13f), wall + new Vector3(-.7f, .9f, -.75f), elara.Palm(true, Vector3.up, Vector3.right)); elara.Look(Head(vance), .9f); elara.Curl(true, 10, 14, 8);
            }
            Add("V1-where", 64, 67, (t, u) => { Interrogate(t); Blade(false); },
                (t, u) => { var c = wall + new Vector3(0, 1.38f, -.3f); var at = c + new Vector3(1.75f, -.06f, -.42f); s.CamMove(at, c, at + new Vector3(-.28f, 0, .05f), c, u, 30, 28, .0015f, t); K(at + new Vector3(0, 1.0f, -.5f), Head(elara), 5); s.Focus(1.7f, 50, 5.6f); });
            Add("V2-heights", 67, 70, (t, u) => { Interrogate(t + 3); Blade(true); },
                (t, u) => { var c = Head(vance) + new Vector3(0, .02f, 0); var at = Head(elara) + new Vector3(.3f, .1f, -.26f); s.CamMove(at, c, at + new Vector3(-.03f, 0, .06f), c, u, 38, 34, .001f, t); hang.intensity = 12; K(c + new Vector3(1.0f, -.15f, -.7f), c, .55f); s.Focus(Vector3.Distance(at, c), 35, 8); });
        }

        // Measured from the models. The two glb guns have the muzzle along -X in the file; import mirrors X.
        static readonly Vector3 GlbMuzzle = Vector3.right;
        static void Ump(Actor a, GameObject gun) => a.Arm(gun, .69f, false, GlbMuzzle, Vector3.up, -.052f, -.041f, .276f, .041f, -.345f, .028f, .09f).Map = GripMap.Ump;
        static void Glock(Actor a, GameObject gun)
        {
            Bounds Of(string part) { foreach (var r in gun.GetComponentsInChildren<Renderer>(true)) if (r.name.Contains(part)) return r.bounds; throw new System.InvalidOperationException("no " + part); }
            var keep = gun.transform.rotation; gun.transform.rotation = Quaternion.identity;
            var forward = Snap(Of("valkyrie_0").center - Of("slide").center);
            var up = Snap(Vector3.ProjectOnPlane(Of("slide").center - Of("mag_full_").center, forward));
            gun.transform.rotation = keep;
            a.Arm(gun, .213f, true, forward, up, -.034f, .017f, 0, 0, 0, 0, .05f).Map = GripMap.Glock;
        }
        static Vector3 Snap(Vector3 v) { var x = new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z)); return x.x >= x.y && x.x >= x.z ? new Vector3(Mathf.Sign(v.x), 0, 0) : x.y >= x.z ? new Vector3(0, Mathf.Sign(v.y), 0) : new Vector3(0, 0, Mathf.Sign(v.z)); }

        /// <summary>The pistol model comes with two spare magazines laid out beside it.</summary>
        static void OneMag(GameObject gun) { foreach (var t in gun.GetComponentsInChildren<Transform>(true)) if (t.name == "mag_full.001" || t.name == "mag_empty") t.gameObject.SetActive(false); }

        void Penthouse()
        {
            var s = Stage;
            const float F = .17f;
            Env(new Color(.11f, .095f, .085f), new Color(.14f, .11f, .09f));
            s.Cam.clearFlags = CameraClearFlags.SolidColor; s.Cam.backgroundColor = new Color(.005f, .007f, .012f);
            var set = s.Model("penthouse/Rectangle House.fbx", Vector3.zero, 0); s.Dress(set, "penthouse/x");
            s.CityLights(new Vector3(-4, 0, 1.5f), 60, 520, 460, 5);
            System.Action Base = () => s.Grade(.05f, 14, -8, new Color(1f, .955f, .9f));
            Base();
            s.BloomAt(.7f, 1.0f); s.VignetteAt(.3f);
            Bounds tb = default; bool found = false;
            foreach (var r in set.GetComponentsInChildren<Renderer>()) if (r.name.StartsWith("DiningTable", System.StringComparison.Ordinal)) { if (!found) { tb = r.bounds; found = true; } else tb.Encapsulate(r.bounds); }
            float top = .945f, end = tb.max.x, midZ = tb.center.z;
            foreach (var r in set.GetComponentsInChildren<Renderer>()) if (r.name.StartsWith("DiningTable", System.StringComparison.Ordinal)) Debug.Log($"[Trailer] table part {r.name} {r.bounds.min} {r.bounds.max}");
            // the lift stands apart: no part of it has to line up with the flat
            var liftAt = new Vector3(60, 0, 60);
            var lift = s.Model("elevator/FirstPersonExampleMap.fbx", liftAt, 0); s.Dress(lift, "elevator/x");
            Transform leaf = null; foreach (var t in lift.GetComponentsInChildren<Transform>()) if (t.name.StartsWith("SM_Lift_door", System.StringComparison.Ordinal)) leaf = t;
            Vector3 leafShut = leaf.position, leafOpen = leafShut + new Vector3(1.05f, 0, 0);
            foreach (var r in lift.GetComponentsInChildren<Renderer>()) if (r.name.StartsWith("SM_LIft_Outer_Wall", System.StringComparison.Ordinal)) r.enabled = false; // the set's outer shell: the door shot looks in from outside it
            var cabinFloor = liftAt + new Vector3(4.7f, 0, 1.3f);
            s.Lamp(LightType.Point, cabinFloor + Vector3.up * 2.1f, Vector3.zero, new Color(1, .94f, .84f), 9, 5, 60, false);
            var a1 = s.Lamp(LightType.Spot, new Vector3(end - .6f, 3.0f, midZ), new Vector3(end - .3f, F, midZ), new Color(1, .86f, .7f), 70, 12, 110, true);
            s.Lamp(LightType.Point, new Vector3(3, 2.4f, 2.5f), Vector3.zero, new Color(1, .82f, .65f), 8, 9, 60, false);
            s.Lamp(LightType.Directional, new Vector3(0, 20, 0), new Vector3(.1f, -.3f, .7f), new Color(.55f, .65f, .95f), .3f, 10, 60, false);
            var key = s.Lamp(LightType.Spot, Vector3.zero, Vector3.forward, new Color(1, .9f, .8f), 0, 7, 50, false);
            void K(Vector3 from, Vector3 to, float i) { key.transform.position = from; key.transform.rotation = Quaternion.LookRotation(to - from); key.intensity = i; }
            var mPos = new Vector3(end + .9f, F, midZ);
            var ePin = new Vector3(end + .3f, F, midZ - .1f);
            var marcus = s.CastModel("marcus/Businessman.glb", Mixamo(), mPos, 270);
            var elara = s.CastModel("elara/olgapose1.fbx", ElaraBones, cabinFloor + new Vector3(-.4f, 0, .15f), 180);
            var leo = s.CastPerson("survival", cabinFloor + new Vector3(.4f, 0, .15f), 180);
            var pistol = s.Model("glock/scene.gltf", Vector3.zero, 0, .055f);
            OneMag(pistol);
            Transform magIn = null; foreach (var x in pistol.GetComponentsInChildren<Transform>(true)) if (x.name == "mag_full") magIn = x;
            var fresh = Object.Instantiate(magIn.gameObject, s.Root); fresh.transform.localScale = magIn.lossyScale; fresh.SetActive(false);
            var spent = Object.Instantiate(magIn.gameObject, s.Root); spent.transform.localScale = magIn.lossyScale; spent.SetActive(false);
            Glock(leo, pistol);
            var ledger = s.Model("ledger/Notebook.fbx", Vector3.zero, 0, .07f); s.Dress(ledger, "ledger/x");
            marcus.Hold(ledger, HumanBodyBones.LeftHand, new Vector3(0, .05f, .12f), new Vector3(90, 0, 0));
            var decanter = s.Model("decanter/sketchWhiskey.fbx", new Vector3(end - .55f, top, midZ - .3f), 0); s.Dress(decanter, "decanter/x");
            var shards = s.Particles("glass", new Vector3(end - .3f, top + .1f, midZ - .2f), new Vector3(.15f, .05f, .15f), 0, 2.3f, .9f, .03f, new Color(.85f, .9f, 1f, .8f), false, null, 1.4f, 11); fx.Add(shards);
            var drink = s.Particles("scotch", new Vector3(end - .3f, top + .1f, midZ - .2f), new Vector3(.1f, .05f, .1f), 0, 1.5f, .9f, .05f, new Color(.72f, .4f, .1f, .8f), false, null, 1.1f, 12); fx.Add(drink);
            var pageAt = new Vector3(.0f, top + .16f, midZ);
            var page = s.Card(pageAt, new Vector3(.21f, .002f, .29f), new Color(.88f, .85f, .76f)); page.SetActive(false);
            var rules = new GameObject[9];
            for (int i = 0; i < rules.Length; i++) { rules[i] = s.Card(pageAt + new Vector3(0, .0015f, .09f - i * .024f), new Vector3(.18f, .0006f, .0012f), new Color(.34f, .36f, .42f)); rules[i].SetActive(false); }
            var num = new GameObject("42").AddComponent<TextMesh>(); num.text = "42"; num.characterSize = .006f; num.fontSize = 120; num.color = new Color(.1f, .08f, .08f); num.anchor = TextAnchor.MiddleCenter; num.transform.SetParent(s.Root); num.gameObject.SetActive(false);
            num.transform.SetPositionAndRotation(pageAt + new Vector3(.07f, .004f, .115f), Quaternion.Euler(90, 0, 0));
            Vector3 Head(Actor a) => a.Bone(HumanBodyBones.Head).position;
            void Off(params Actor[] xs) { foreach (var a in xs) a.Root.SetActive(false); }
            void On(params Actor[] xs) { foreach (var a in xs) a.Root.SetActive(true); }
            void Marcus(float t, float look = .7f) { marcus.Floor = F; marcus.Place(mPos, 270); marcus.Pose("idle", t); marcus.Look(new Vector3(-2, F + 1.5f, midZ), look); }
            void Lift(Actor a, Vector3 at, string clip, float time) { a.Floor = 0; a.Place(at, 0); a.Pose(clip, time); }
            elara.Floor = leo.Floor = 0;
            s.Voice("ELARA", elara); s.Voice("LEO", leo); s.Voice("MARCUS", marcus);
            bool taken = false;
            // The reload is built from the pistol's own magazines: drop the spent one, take a fresh one from the belt, seat it, rack the slide.
            void Reload(float t)
            {
                leo.Root.transform.rotation = Quaternion.Euler(0, -58, 0);
                var f = leo.Root.transform.forward; var r = leo.Root.transform.right;
                leo.Aim(leo.Root.transform.position + f * 2 + Vector3.up * 3.1f, .55f);
                leo.Look(leo.Root.transform.position + f * .5f + Vector3.up * 1.15f, .6f); leo.Feel(-.4f);
                float s0 = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.3f, .7f, t)), s1 = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.75f, 1.05f, t)), s2 = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(1.15f, 1.3f, t)), s3 = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(1.3f, 1.42f, t)), s4 = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(1.45f, 1.8f, t));
                leo.LeftAt = (grip, gf, gu, gr) =>
                {
                    var belt = grip + Vector3.down * .36f - gr * .2f - gf * .22f; var butt = grip - gu * .07f - gf * .07f; var slide = grip + gu * .03f + gf * .0f;
                    var at = Vector3.Lerp(butt - gu * .05f, belt, s0); at = Vector3.Lerp(at, butt, s1); at = Vector3.Lerp(at, slide, s2); at = Vector3.Lerp(at, slide - gf * .06f, s3); at = Vector3.Lerp(at, belt - gu * .05f, s4);
                    float d = t - .25f;
                    if (d >= 0 && d < .65f) spent.transform.SetPositionAndRotation(grip - gu * (.12f + 4.9f * d * d), Quaternion.Euler(d * 240, 20, d * 90));
                    return at;
                };
                magIn.gameObject.SetActive(t < .25f || t > 1.05f);
                fresh.SetActive(t >= .7f && t <= 1.05f);
                spent.SetActive(t >= .25f && t < .9f);
            }
            leo.Grip(fresh, .13f, Actor.Hold2.OneHand, true, default, .08f, .01f);
            Add("P1-lift", 70, 71.9f, (t, u) => { Off(marcus); On(leo, elara); leaf.position = leafShut; Lift(elara, cabinFloor + new Vector3(-.42f, 0, -.05f), "idle", t); Lift(leo, cabinFloor + new Vector3(.42f, 0, -.05f), "pistol-idle", 1); Reload(t); },
                (t, u) => { var at = cabinFloor + new Vector3(0, 1.38f, .92f); var c = cabinFloor + new Vector3(0, 1.42f, 0); s.CamMove(at, c, at + new Vector3(0, -.02f, -.06f), c, u, 58, 56, .0007f, t); K(at + new Vector3(-.15f, .15f, 0), c, 4.5f); s.Focus(.95f, 35, 5.6f); });
            Add("P1b-doors", 71.9f, 72.7f, (t, u) => { Lift(elara, cabinFloor + new Vector3(-.42f, 0, -.05f), "idle", t + 2); Lift(leo, cabinFloor + new Vector3(.42f, 0, -.05f), "pistol-idle", 1); leo.Aim(cabinFloor + new Vector3(.42f, 1.05f, 3), .55f); leaf.position = Vector3.Lerp(leafShut, leafOpen, Mathf.SmoothStep(0, 1, t / .7f)); },
                (t, u) => { var at = cabinFloor + new Vector3(0, 1.32f, 2.4f); var c = cabinFloor + new Vector3(0, 1.28f, 0); s.CamMove(at, c, at + new Vector3(0, 0, -.12f), c, u, 38, 36, .0005f, t); K(cabinFloor + new Vector3(0, 2.05f, .75f), c, 3.5f); s.Focus(3.5f, 35, 5.6f); });
            Add("P2-marcus-waits", 72.7f, 75, (t, u) => { On(marcus); Marcus(t); elara.Root.SetActive(false); leo.Root.SetActive(false); },
                (t, u) => { var at = new Vector3(end - 3.9f, 1.42f, midZ + 1.05f); var c = mPos + Vector3.up * 1.1f; s.CamMove(at, c, at + new Vector3(.22f, 0, -.03f), c, u, 40, 40, .0005f, t); s.Focus(Vector3.Distance(at, c), 50, 5.6f); });
            Add("P3-leo-first", 75, 77, (t, u) => { Marcus(t + 3, .85f); On(leo); leo.Floor = F; float d = Mathf.Clamp01(t / 1.75f); leo.Place(Vector3.Lerp(new Vector3(end - 3.4f, F, midZ + 1.05f), new Vector3(end - 1.5f, F, midZ + .95f), d), 95); leo.Pose(d < 1 ? "pistol-walk" : "pistol-idle", t * .95f); leo.Aim(Head(marcus)); },
                (t, u) => { var at = mPos + new Vector3(1.05f, 1.42f, -.95f); var c = Head(leo) + new Vector3(0, -.2f, 0); s.CamMove(at, c, at + new Vector3(0, 0, .05f), c, u, 36, 36, .001f, t); K(Head(leo) + new Vector3(1.2f, .5f, -.7f), Head(leo), 5); s.Focus(Vector3.Distance(at, c), 50, 4); });
            Add("P4-lunge", 77, 77.4f, (t, u) => { Off(leo); On(elara, marcus); elara.Floor = F; float d = t / .4f; elara.Place(Vector3.Lerp(ePin + new Vector3(-1.5f, 0, .05f), ePin, d), 90); elara.Pose("run", t); Marcus(t + 5, .9f); },
                (t, u) => { var at = new Vector3(end + .15f, 1.32f, midZ - 2.05f); var c = new Vector3(end + .15f, 1.12f, midZ); s.CamMove(at, c, at + new Vector3(.12f, 0, 0), c, u, 46, 46, .008f, t); K(at + new Vector3(0, .7f, .4f), c, 8); s.Focus(0); });
            Add("P5-glass", 77.4f, 78.2f, (t, u) => { elara.Place(ePin, 90); elara.Pose("cross-punch", .3f + t * .8f); marcus.Floor = F; marcus.Place(mPos, 270); marcus.Pose("head-hit", t * .9f, null, 0, 0, true); if (t < .05f) { shards.Emit(120); drink.Emit(70); } },
                (t, u) => { var at = new Vector3(end + .1f, 1.26f, midZ - 1.55f); var c = new Vector3(end + .15f, 1.1f, midZ); s.CamMove(at, c, at, c, u, 44, 44, .012f, t); s.Focus(0); });
            Add("P6-page-forty-two", 78.2f, 79.4f, (t, u) => { elara.Place(ePin, 90); elara.Pose("idle", t); marcus.Place(mPos + new Vector3(-.1f, 0, 0), 270); marcus.Pose("head-hit", .9f, null, 0, 0, true); elara.Reach(true, Head(marcus) + new Vector3(-.12f, -.18f, -.05f), new Vector3(end, 1, midZ - .9f)); },
                (t, u) => { var c = Head(marcus) + new Vector3(0, .02f, 0); var at = c + new Vector3(-.28f, -.03f, -.72f); s.CamMove(at, c, at + new Vector3(.03f, 0, .07f), c, u, 30, 28, .001f, t); K(at + new Vector3(0, .35f, -.3f), c, 2.2f); s.Focus(.78f, 85, 9); });
            Add("P7-ledger", 79.4f, 80.4f, (t, u) => { Off(elara, marcus); ledger.SetActive(false); page.SetActive(true); foreach (var r in rules) r.SetActive(true); num.gameObject.SetActive(true); },
                (t, u) => { var c = pageAt + new Vector3(.03f, 0, .07f); s.CamMove(c + new Vector3(.07f, .3f, -.28f), c, c + new Vector3(.05f, .24f, -.22f), c, u, 32, 30, .0007f, t); K(c + new Vector3(.25f, .55f, -.1f), c, 1.6f); s.Focus(.34f, 50, 5.6f); });
            Add("P8-parasite", 80.4f, 83.2f, (t, u) => { num.gameObject.SetActive(false); page.SetActive(false); foreach (var r in rules) r.SetActive(false); On(elara, marcus); elara.Place(ePin, 90); elara.Pose("idle", t + 2); elara.Look(Head(marcus) + Vector3.up * .3f, .5f); marcus.Pose("head-hit", .9f, null, 0, 0, true); },
                (t, u) => { var h = Head(elara) + elara.Root.transform.forward * .07f + Vector3.down * .03f; s.CamMove(h + new Vector3(.42f, -.16f, -.26f), h, h + new Vector3(.35f, -.13f, -.21f), h, u, 30, 26, .0006f, t); K(h + new Vector3(.7f, -.05f, -.6f), h, 1.8f); s.Focus(.5f, 85, 9); });
            Add("P9-decanter", 83.2f, 83.5f, (t, u) => { elara.Place(ePin, 90); elara.Pose("idle", t + 4); float d = t / .3f; var from = ePin + new Vector3(-.1f, 1.95f, -.25f); var to = mPos + new Vector3(-.2f, 1.6f, -.05f); decanter.transform.position = Vector3.Lerp(from, to, d * d); decanter.transform.rotation = Quaternion.Euler(0, 0, -160 * d); elara.Reach(false, decanter.transform.position + new Vector3(0, .05f, 0), ePin + new Vector3(0, 1, -.8f)); },
                (t, u) => { var at = new Vector3(end + .15f, 1.4f, midZ - 1.75f); var c = new Vector3(end + .4f, 1.45f, midZ); s.CamMove(at, c, at, c, u, 44, 44, .006f, t); s.Focus(0); });
            Add("P10-black", 83.5f, 84, (t, u) => { s.Grade(-16, 0, 0, Color.white); Off(elara, marcus); decanter.SetActive(false); }, (t, u) => { });
            Add("P11-after", 84, 85, (t, u) => { Base(); On(elara, leo); elara.Place(ePin + new Vector3(.05f, 0, 0), 100); elara.Pose("idle", t + 1); elara.Look(new Vector3(ePin.x + 2, 1.35f, midZ - .4f), .7f); leo.Floor = F; leo.Place(new Vector3(end - .9f, F, midZ + 1.0f), 110); leo.Pose("pistol-idle", 1); leo.Aim(leo.Root.transform.position + leo.Root.transform.forward * 3 + Vector3.up * 1.05f, .55f); ledger.SetActive(true); if (!taken) { taken = true; ledger.transform.SetParent(s.Root, true); elara.Grip(ledger, .26f, Actor.Hold2.Flat, true, default, .1f, 0); } },
                (t, u) => { var at = mPos + new Vector3(1.55f, 1.25f, -.75f); var c = new Vector3(ePin.x - .2f, 1.42f, midZ + .3f); s.CamMove(at, c, at, c, u, 42, 42, 0, 0); K(at + new Vector3(0, .9f, 0), c, 3); s.Focus(Vector3.Distance(at, c), 50, 5.6f); });
        }

        void Garage()
        {
            var s = Stage;
            const float F = -.63f;
            Env(new Color(.05f, .055f, .07f), new Color(.08f, .09f, .11f));
            s.Cam.clearFlags = CameraClearFlags.SolidColor; s.Cam.backgroundColor = new Color(.005f, .006f, .01f);
            var set = s.Model("garage/parking garage_inner/parking garage.obj", Vector3.zero, 0); s.Dress(set, "garage/x");
            var garageTex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Trailer/Models/garage/textures/1.jpg");
            foreach (var r in set.GetComponentsInChildren<Renderer>()) { var m = new Material(r.sharedMaterial); m.SetTexture("_BaseMap", garageTex); m.SetColor("_BaseColor", Color.white); m.SetFloat("_Smoothness", .22f); r.sharedMaterial = m; }
            s.Grade(.25f, 14, -16, new Color(.92f, .95f, 1f));
            s.BloomAt(.95f, 1.0f); s.VignetteAt(.4f);
            var carAt = new Vector3(-1.3f, F + .08f, 2.6f);
            var car = s.Model("fastback/MuscleCSO_low.fbx", carAt, 30); s.Dress(car, "fastback/x");
            s.PaintMidnight(car, "Body_mid");
            foreach (var r in car.GetComponentsInChildren<Renderer>()) { var ms = r.sharedMaterials; for (int i = 0; i < ms.Length; i++) if (ms[i] != null && ms[i].name.StartsWith("Lit", System.StringComparison.Ordinal)) { var g = new Material(ms[i]); g.SetColor("_BaseColor", new Color(.012f, .014f, .02f, .35f)); g.SetFloat("_Surface", 1); g.SetFloat("_Blend", 0); g.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha); g.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha); g.SetInt("_ZWrite", 0); g.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); g.renderQueue = 3000; g.SetFloat("_Smoothness", .96f); ms[i] = g; } r.sharedMaterials = ms; }
            var fwd = car.transform.forward; var right = car.transform.right;
            var lampAt = carAt - fwd * .9f + right * 1.1f + Vector3.up * 3.3f;
            s.Lamp(LightType.Spot, lampAt, carAt - fwd * .3f, new Color(1, .93f, .8f), 42, 12, 96, true);
            s.Card(lampAt + Vector3.up * .05f, new Vector3(1.0f, .03f, .1f), Color.white, 30, Stage.Glow(new Color(1, .9f, .75f), 8));
            var key = s.Lamp(LightType.Spot, Vector3.zero, Vector3.forward, new Color(1, .92f, .82f), 0, 6, 46, false);
            void K(Vector3 from, Vector3 to, float i) { key.transform.position = from; key.transform.rotation = Quaternion.LookRotation(to - from); key.intensity = i; }
            var door = carAt + right * 1.25f - fwd * .1f;
            var elara = s.CastModel("elara/olgapose1.fbx", ElaraBones, door - fwd * 3, 30);
            var leo = s.CastPerson("survival", door - fwd * 3 + right * 1, 30);
            s.Voice("ELARA", elara); s.Voice("LEO", leo);
            foreach (var a in s.Actors) a.Floor = F;
            Vector3 Head(Actor a) => a.Bone(HumanBodyBones.Head).position;
            float heading = 30;
            Add("G1-car", 85, 86.4f, (t, u) => { elara.Place(door - fwd * (2.6f - t * 1.25f) + right * .25f, heading); elara.Pose("walk", t * 1.1f); leo.Place(door - fwd * (3.4f - t * 1.25f) + right * 1.05f, heading); leo.Pose("walk", .4f + t * 1.1f); },
                (t, u) => { var at = carAt + fwd * 4.3f + right * 3.2f + Vector3.up * .55f; var c = carAt + Vector3.up * .55f - fwd * .2f; s.CamMove(at, c, at - right * .3f, c, u, 44, 44, 0, 0); s.Focus(0); });
            Add("G2-door", 86.4f, 87.2f, (t, u) => { leo.Root.SetActive(false); elara.Place(door - fwd * (.75f - t * .7f) + right * .22f, heading); elara.Pose("walk", 1 + t * 1.1f); },
                (t, u) => { var c = Head(elara) + Vector3.down * .16f; var at = c + fwd * 1.5f + right * .85f + Vector3.up * .02f; s.CamMove(at, c, at - fwd * .08f, c, u, 32, 32, .001f, t); K(at + Vector3.up * .7f, c, 5); s.Focus(Vector3.Distance(at, c), 85, 9); });
            var beams = new GameObject[2]; var glow = Stage.Glow(new Color(1, .93f, .78f), 30);
            for (int i = 0; i < 2; i++) { beams[i] = GameObject.CreatePrimitive(PrimitiveType.Sphere); Object.Destroy(beams[i].GetComponent<Collider>()); beams[i].transform.SetParent(s.Root, false); beams[i].transform.localScale = new Vector3(.17f, .17f, .05f); beams[i].transform.SetPositionAndRotation(car.transform.TransformPoint(new Vector3(i == 0 ? -.68f : .68f, .62f, 2.1f)), car.transform.rotation); beams[i].GetComponent<Renderer>().sharedMaterial = glow; beams[i].SetActive(false); }
            var headlamp = s.Lamp(LightType.Spot, car.transform.TransformPoint(new Vector3(0, .65f, 2.2f)), car.transform.TransformPoint(new Vector3(0, -.1f, 12)), new Color(1, .94f, .82f), 0, 22, 62, false);
            Add("G3-start", 87.2f, 88, (t, u) => { elara.Root.SetActive(false); leo.Root.SetActive(false); bool on = t > .22f; foreach (var b in beams) b.SetActive(on); headlamp.intensity = on ? 70 : 0; },
                (t, u) => { float j = t > .22f && t < .7f ? Mathf.Sin(t * 95) * .005f : 0; var at = carAt + fwd * 4.0f + right * 1.25f + Vector3.up * (.38f + j); var c = carAt + fwd * .9f + Vector3.up * .55f; s.CamMove(at, c, at, c, u, 38, 38, 0, 0); s.Focus(0); });
        }
    }
}
#endif
