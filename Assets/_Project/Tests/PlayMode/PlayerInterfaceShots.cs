// Pictures of the player interface in the real city, to be looked at (Evidence/local/ui-*.png): the start screen, on foot with a gun, wanted and hurt with a waypoint,
// driving, the pause menu, the settings, the map and the weapon wheel. The game's camera and the UI panels are drawn into textures and laid one over the other here;
// in play the panels draw straight onto the screen. Nothing is asserted about the looks: these are for review.
using System.Collections;
using System.IO;
using System.Linq;
using Dad.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Dad.Tests
{
    public class PlayerInterfaceShots
    {
        const int Width = 1600, Height = 900;

        [SetUp] public void Mark() { TestScenes.Mark(); Player.Automatic = true; Menus.Title = true; }

        [UnityTearDown]
        public IEnumerator Clean()
        {
            Player.Automatic = false;
            Menus.Title = null;
            Time.timeScale = 1;
            AudioListener.pause = false;
            Heat.Clear();
            Waypoint.Clear();
            yield return TestScenes.Clean();
        }

        internal static IEnumerator Picture(string name)
        {
            var docs = Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None).Where(d => d.panelSettings != null).OrderBy(d => d.panelSettings.sortingOrder).ToArray();
            var ui = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            for (int i = 0; i < docs.Length; i++)
            {
                docs[i].panelSettings.targetTexture = ui;
                docs[i].panelSettings.clearColor = i == 0;
                docs[i].panelSettings.colorClearValue = Color.clear;
            }
            for (int i = 0; i < 3; i++) yield return null;
            var cam = Camera.main;
            var world = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            cam.targetTexture = world;
            CityView.Active?.Submit();
            foreach (var p in Object.FindObjectsByType<ParkedView>(FindObjectsSortMode.None)) p.Submit();
            foreach (var t in Object.FindObjectsByType<TrainView>(FindObjectsSortMode.None)) t.Submit();
            cam.Render();
            cam.targetTexture = null;
            var a = Read(world);
            var b = Read(ui);
            var px = a.GetPixels32();
            var over = b.GetPixels32();
            for (int i = 0; i < px.Length; i++)
            {
                float k = over[i].a / 255f;
                px[i] = new Color32((byte)Mathf.Min(255, over[i].r + px[i].r * (1 - k)), (byte)Mathf.Min(255, over[i].g + px[i].g * (1 - k)), (byte)Mathf.Min(255, over[i].b + px[i].b * (1 - k)), 255);
            }
            a.SetPixels32(px);
            Directory.CreateDirectory("Evidence/local");
            File.WriteAllBytes($"Evidence/local/ui-{name}.png", a.EncodeToPNG());
            foreach (var d in docs) { d.panelSettings.targetTexture = null; d.panelSettings.clearColor = false; }
            Object.Destroy(a); Object.Destroy(b); Object.Destroy(world); Object.Destroy(ui);
            yield return null;
        }

        static Texture2D Read(RenderTexture rt)
        {
            var t = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            RenderTexture.active = rt;
            t.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            RenderTexture.active = null;
            return t;
        }

        static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }

        [UnityTest, Timeout(600000)]
        public IEnumerator TheInterfaceInTheCity()
        {
            SceneManager.LoadScene("Boot");
            yield return null;
            var root = Object.FindAnyObjectByType<GameRoot>();
            root.Day.MinutesPerSecond = 0;
            root.Day.Set(DayCycle.DefaultDate, 19.6, Weather.Clear);
            var menus = Object.FindAnyObjectByType<Menus>();
            var you = Object.FindAnyObjectByType<Player>();
            Assert.IsNotNull(menus, "plain play makes the menus");
            Assert.IsNotNull(Object.FindAnyObjectByType<Hud>(), "plain play makes the HUD");
            yield return Frames(3);
            Assert.AreEqual("Title", menus.Top);
            float until = Time.realtimeSinceStartup + 120;
            while (you.Now == Player.Phase.Waiting || CityMap.Active == null)
            {
                Assert.Less(Time.realtimeSinceStartup, until, "the city and its map within two minutes");
                yield return null;
            }
            yield return Frames(30);
            yield return Picture("1-start");
            menus.CloseAll();
            Assert.AreEqual(InputContext.OnFoot, root.Controls.Context, "Play goes to you on foot");
            yield return Frames(30);
            yield return Picture("2-on-foot");

            you.Arsenal.Select(2);
            Waypoint.Place(FreeCamera.StartEast + 700, FreeCamera.StartNorth - 400);
            yield return Frames(90);
            Heat.Raise(3);
            you.Vitals.Hurt(55, you.Body.transform.position + you.Body.transform.forward * 10);
            yield return Frames(4);
            yield return Picture("3-armed-wanted-hurt");

            you.Arsenal.Select(0);
            Heat.Clear();
            you.Vitals.Heal();
            yield return Frames(30);
            Assert.IsTrue(you.TryGetIn(), "beside the car");
            until = Time.realtimeSinceStartup + 30;
            while (you.Now != Player.Phase.Driving && Time.realtimeSinceStartup < until) yield return null;
            Assert.AreEqual(Player.Phase.Driving, you.Now);
            you.Car.State.HeadLamps = true;
            you.Car.State.Indicator = 1;
            yield return Frames(20);
            yield return Picture("4-driving");

            menus.Pause();
            yield return Frames(5);
            yield return Picture("5-pause");
            menus.ShowSettings(3);
            yield return Frames(5);
            yield return Picture("6-settings-controls");
            menus.CloseAll();
            menus.ShowSettings(5);
            yield return Frames(5);
            yield return Picture("7-settings-interface");
            menus.CloseAll();
            menus.ShowMap();
            yield return Frames(10);
            yield return Picture("8-map");
            menus.CloseAll();
            yield return Frames(5);

            Assert.IsTrue(you.TryGetOut() || you.Now != Player.Phase.Driving, "out of the car");
            until = Time.realtimeSinceStartup + 30;
            while (you.Now != Player.Phase.Walking && Time.realtimeSinceStartup < until) yield return null;
            var wheel = Object.FindAnyObjectByType<WeaponWheel>();
            wheel.Show(you.Arsenal);
            wheel.Point(1);
            yield return Frames(5);
            yield return Picture("9-weapon-wheel");
            wheel.Hide(false);
        }
    }
}
