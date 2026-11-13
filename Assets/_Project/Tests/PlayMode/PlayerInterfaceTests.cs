// The player interface (Docs/PRODUCTION/PLAYER_INTERFACE.md) against simulated devices: the settings file and its backup, the context switch under a menu, your guns
// hurting someone and making you wanted, going down and standing again, jumping and crouching, the HUD showing what is there, the pause menu and settings worked by keys,
// the map setting a waypoint and a route, and the weapon wheel slowing time and choosing.
using System.Collections;
using System.IO;
using System.Linq;
using Dad.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.TestTools;

namespace Dad.Tests
{
    public class PlayerInterfaceTests : InputTestFixture
    {
        const string AssetPath = "Assets/_Project/Input/DadInput.inputactions";

        InputActionAsset asset;
        GameInput input;
        Keyboard keyboard;
        Mouse mouse;
        string folder;
        readonly System.Collections.Generic.List<Object> made = new System.Collections.Generic.List<Object>();

        public override void Setup()
        {
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();
            InputSystem.AddDevice<Gamepad>();
            asset = InputActionAsset.FromJson(File.ReadAllText(AssetPath));
            input = new GameInput(asset);
            folder = Path.Combine(Path.GetTempPath(), "dad-settings-" + System.Guid.NewGuid().ToString("N"));
            GameSettings.Folder = folder;
            GameSettings.Use(new GameSettings(), input);
            Ui.UsePad(false);
            Heat.Clear();
            Waypoint.Clear();
            Menus.Title = false;
        }

        public override void TearDown()
        {
            foreach (var o in made) if (o != null) Object.Destroy(o);
            made.Clear();
            Time.timeScale = 1;
            AudioListener.pause = false;
            Heat.Clear();
            Waypoint.Clear();
            Cast.Read(null);
            Menus.Title = null;
            GameSettings.Use(new GameSettings(), null);
            GameSettings.Folder = null;
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
            input.Dispose();
            Object.DestroyImmediate(asset);
            base.TearDown();
        }

        // A key pressed for the next frame's input update, so WasPressedThisFrame sees it in Update, as in play.
        IEnumerator Tap(ButtonControl key)
        {
            Press(key, queueEventOnly: true);
            yield return null;
            Release(key, queueEventOnly: true);
            yield return null;
        }

        static IEnumerator Wait(float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until) yield return null;
        }

        // You, standing on a floor with a camera behind you; an arsenal of the three guns.
        Player Stand(Vector3 at = default)
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.transform.localScale = new Vector3(12, 1, 12);
            made.Add(floor);
            var camera = new GameObject("Main Camera") { tag = "MainCamera" };
            camera.AddComponent<Camera>();
            made.Add(camera);
            var view = camera.AddComponent<FootCamera>();
            var body = OnFoot.Spawn(at, 0, input, camera.transform);
            Assert.IsNotNull(body, "the player did not spawn");
            made.Add(body.gameObject);
            var host = new GameObject("you");
            made.Add(host);
            var you = host.AddComponent<Player>();
            you.Input = input;
            you.Stand(null, body, view);
            return you;
        }

        static NpcRecord Gang => NpcRoster.Load("People/fighters").people.First(p => p.id == "GANG_001");

        [Test]
        public void SettingsSurviveASaveAndABrokenFile()
        {
            var s = new GameSettings { FieldOfView = 75, Miles = true };
            s.Save();
            Assert.AreEqual(75, GameSettings.Load().FieldOfView);
            Assert.AreEqual("", GameSettings.Note);
            s.FieldOfView = 80;
            s.Save();
            Assert.IsTrue(File.Exists(GameSettings.BackupPath), "the one before is kept");
            File.WriteAllText(GameSettings.FilePath, "{ not json");
            var back = GameSettings.Load();
            Assert.AreEqual(75, back.FieldOfView, "the backup was not used");
            Assert.IsTrue(back.Miles);
            StringAssert.Contains("backup", GameSettings.Note);
            File.WriteAllText(GameSettings.BackupPath, "garbage");
            Assert.AreEqual(60, GameSettings.Load().FieldOfView, "defaults when both are broken");
            StringAssert.Contains("defaults", GameSettings.Note);
            File.WriteAllText(GameSettings.FilePath, "{\"Version\":1,\"FieldOfView\":500,\"Master\":7}");
            var clamped = GameSettings.Load();
            Assert.AreEqual((90f, 1f), (clamped.FieldOfView, clamped.Master), "values out of range are brought in");
        }

        [Test]
        public void BindingsAreKeptInTheSettings()
        {
            int jump = input.Jump.GetBindingIndex(InputBinding.MaskByGroup("Keyboard&Mouse"));
            input.Jump.ApplyBindingOverride(jump, "<Keyboard>/k");
            var s = GameSettings.Current;
            s.Bindings = input.SaveBindingOverrides();
            s.Save();
            input.Jump.RemoveAllBindingOverrides();
            GameSettings.Use(GameSettings.Load(), input);
            Assert.AreEqual("<Keyboard>/k", input.Jump.bindings[jump].effectivePath);
            Assert.AreEqual("K", Ui.Key(input.Jump, false));
            Assert.AreEqual("A", Ui.Key(input.Jump, true));
            Assert.AreEqual("RMB", Ui.Key(input.Aim, false));
            Assert.AreEqual("LT", Ui.Key(input.Aim, true));
        }

        [Test]
        public void SwitchingUnderAMenuChangesWhatComesBack()
        {
            input.Switch(InputContext.FreeCamera);
            input.Push(InputContext.Menu);
            input.Switch(InputContext.OnFoot);
            Assert.AreEqual(InputContext.Menu, input.Context);
            Assert.AreEqual(InputContext.OnFoot, input.Underneath);
            Assert.IsFalse(input.OnFoot.enabled, "nothing under a menu is live");
            input.Pop();
            Assert.AreEqual(InputContext.OnFoot, input.Context);
            Assert.IsTrue(input.OnFoot.enabled);
            Assert.IsFalse(input.FreeCamera.enabled);
        }

        // Aim and fire at a gang member: rounds go, the shot strikes, they are hurt, the police want you and the gang turns on you; R reloads from the spare rounds.
        [UnityTest]
        public IEnumerator YourShotsHurtAndMakeYouWanted()
        {
            var you = Stand();
            var foe = Fighter.Spawn(Gang, Fighter.Side.Gang, new Vector3(0, 0, 7), 180);
            Assert.IsNotNull(foe);
            made.Add(foe.gameObject);
            yield return null;
            var guns = you.Arsenal;
            Assert.AreEqual(4, guns.Slots.Count, "hands and the three guns");
            guns.Select(1);
            Assert.AreEqual("Pistol", guns.Now.Name);
            yield return Wait(0.5f);
            Press(mouse.rightButton, queueEventOnly: true);
            yield return Wait(0.4f);
            Assert.IsTrue(guns.Aiming, "the right button aims");
            float before = foe.Health;
            for (int i = 0; i < 8 && foe.Health >= before; i++)
            {
                yield return Tap(mouse.leftButton);
                yield return Wait(0.25f);
            }
            Release(mouse.rightButton, queueEventOnly: true);
            yield return null;
            Assert.Greater(guns.Shots, 0, "no shot was fired");
            Assert.Less(guns.Now.InGun, 17);
            Assert.Less(foe.Health, before, $"no shot struck in {guns.Shots}");
            Assert.Greater(guns.Struck, 0);
            Assert.GreaterOrEqual(Heat.Level, 1, "shooting makes you wanted");
            Assert.IsTrue(Heat.GangAfter, "hurting a gang member turns the gang on you");
            Assert.AreEqual(Player.Phase.Walking, you.Now, $"you went down under the gang's fire (health {you.Vitals.Health:F0})");
            foe.HoldFire = true; // the gang's answer is checked above; from here it is the reload alone, which a head shot during it (three times the damage) would cut short
            int spare = guns.Now.Spare;
            yield return Tap(keyboard.rKey);
            Assert.AreEqual(Player.Phase.Walking, you.Now, $"you went down under the gang's fire as the reload began (health {you.Vitals.Health:F0})");
            Assert.IsTrue(guns.Reloading, "R reloads");
            // the reload runs on game time, which falls behind the clock on the wall when frames are slow
            float limit = Time.realtimeSinceStartup + 10;
            while (guns.Reloading && Time.realtimeSinceStartup < limit) yield return null;
            yield return null;
            Assert.AreEqual(Player.Phase.Walking, you.Now, $"you went down during the reload (health {you.Vitals.Health:F0})");
            Assert.AreEqual(17, guns.Now.InGun, "the magazine is full");
            Assert.Less(guns.Now.Spare, spare);
        }

        // At no health you go down; a few seconds later you stand again where you began, healed, no longer wanted, still with your guns.
        [UnityTest]
        public IEnumerator DownAndUpAgain()
        {
            var you = Stand();
            var hud = Hud.Attach(new GameObject("hud"), input, you, null, null, null);
            made.Add(hud.gameObject);
            yield return null;
            you.Body.transform.position = new Vector3(5, 0, 5);
            Heat.Raise(2);
            you.Vitals.Hurt(150, new Vector3(5, 1, 10));
            Assert.AreEqual(Player.Phase.Down, you.Now);
            Assert.AreEqual("You are down", Notice.Last);
            yield return null;
            yield return null;
            Assert.IsTrue(hud.Showing("down"));
            StringAssert.StartsWith("Back on your feet in", hud.Shown("down"));
            yield return Wait(Player.DownFor + 0.5f);
            Assert.AreEqual(Player.Phase.Walking, you.Now);
            Assert.AreEqual(Vitals.Most, you.Vitals.Health);
            Assert.AreEqual(0, Heat.Level);
            Assert.AreEqual(4, you.Arsenal.Slots.Count, "your guns came with you");
            Assert.Less(new Vector2(you.Body.transform.position.x, you.Body.transform.position.z).magnitude, 1, "back where you began");
            yield return null;
            Assert.IsFalse(hud.Showing("down"));
        }

        [UnityTest]
        public IEnumerator JumpAndCrouch()
        {
            var you = Stand();
            yield return Wait(0.5f);
            var body = you.Body;
            float ground = body.transform.position.y;
            Assert.IsTrue(body.Grounded);
            Press(keyboard.spaceKey, queueEventOnly: true);
            float top = ground;
            float until = Time.realtimeSinceStartup + 0.8f;
            yield return null;
            Release(keyboard.spaceKey, queueEventOnly: true);
            while (Time.realtimeSinceStartup < until) { top = Mathf.Max(top, body.transform.position.y); yield return null; }
            Assert.Greater(top - ground, 0.3f, "space jumps");
            yield return Wait(0.5f);
            Assert.AreEqual(ground, body.transform.position.y, 0.05f, "and comes down");
            yield return Tap(keyboard.cKey);
            Assert.IsTrue(body.Crouched, "C crouches");
            Assert.AreEqual(OnFoot.CrouchHeight, body.GetComponent<CharacterController>().height, 1e-3f);
            yield return Tap(keyboard.cKey);
            Assert.IsFalse(body.Crouched, "and stands");
        }

        // The HUD shows what is there: no gun box with bare hands; the pistol and its rounds when held; the wanted marks; health after a hurt.
        [UnityTest]
        public IEnumerator TheHudShowsWhatIsThere()
        {
            var you = Stand();
            var hud = Hud.Attach(new GameObject("hud"), input, you, null, null, null);
            made.Add(hud.gameObject);
            yield return null;
            yield return null;
            Assert.IsTrue(hud.Showing("hud"));
            Assert.IsFalse(hud.Showing("weapon"), "bare hands show no gun");
            Assert.IsFalse(hud.Showing("prompt"), "nothing to use here");
            Assert.IsFalse(hud.Showing("vehicle"));
            you.Arsenal.Select(1);
            yield return null;
            Assert.IsTrue(hud.Showing("weapon"));
            Assert.AreEqual("PISTOL", hud.Shown("weapon"));
            Assert.AreEqual("17", hud.Shown("ammo"));
            Assert.IsTrue(hud.Showing("crosshair"));
            Heat.Raise(3);
            you.Vitals.Hurt(30, Vector3.forward * 5);
            yield return null;
            Assert.AreEqual(3, hud.HeatShown);
            Assert.AreEqual(0.7f, hud.HealthShown, 0.01f);
            GameSettings.Current.Crosshair = false;
            yield return null;
            Assert.IsFalse(hud.Showing("crosshair"), "the setting hides it");
            input.Push(InputContext.Paused);
            yield return null;
            Assert.IsFalse(hud.Showing("hud"), "hidden under the menus");
        }

        // The Character row (owner, 2026-11-13): right and left go through the twenty, men then women, and the choice is who you are.
        [UnityTest]
        public IEnumerator TheCharacterRowChoosesWhoYouAre()
        {
            var menus = Menus.Attach(new GameObject("menus"), input, null, null);
            made.Add(menus.gameObject);
            yield return null;
            yield return Tap(keyboard.escapeKey);
            yield return null;
            Assert.AreEqual(Cast.First, Cast.You);
            yield return Tap(keyboard.downArrowKey);
            yield return Tap(keyboard.downArrowKey);
            Assert.AreEqual("Character", menus.RowNames[menus.Chosen]);
            yield return Tap(keyboard.rightArrowKey);
            Assert.AreEqual("npc_m09", Cast.You, "right: the next man");
            yield return Tap(keyboard.rightArrowKey);
            yield return Tap(keyboard.rightArrowKey);
            Assert.AreEqual("npc_f01", Cast.You, "after the ten men, the women");
            Assert.AreEqual("npc_f01", Progress.Current.Character, "kept with your progress");
            yield return Tap(keyboard.leftArrowKey);
            Assert.AreEqual("npc_m10", Cast.You, "left goes back");
            Assert.AreEqual("Pause", menus.Top, "choosing stays on the menu");
        }

        // Credits on the start screen (owner, 2026-11-13): down to it, Enter opens the list in sections with each work's maker and licence; moving down skips the
        // headings and the help line gives the source; Enter does nothing there; Escape goes back to the start screen.
        [UnityTest]
        public IEnumerator TheCreditsSayWhoMadeWhat()
        {
            var menus = Menus.Attach(new GameObject("menus"), input, null, null);
            made.Add(menus.gameObject);
            yield return null;
            menus.ShowTitle();
            yield return null;
            CollectionAssert.AreEqual(new[] { "Play", "Character", "Settings", "Credits", "Quit to desktop" }, menus.RowNames);
            for (int i = 0; i < 3; i++) yield return Tap(keyboard.downArrowKey);
            Assert.AreEqual("Credits", menus.RowNames[menus.Chosen]);
            yield return Tap(keyboard.enterKey);
            Assert.AreEqual("Credits", menus.Top);
            var rows = menus.RowNames;
            foreach (var work in new[] { "DHAKA: AFTER DARK", "THE PEOPLE'S CLOTHES AND GEAR", "Patrol cap", "Bandana mask", "WEAPONS", "FN Herstal Scar Light", "Old Residential Building [4k]", "Map data", "LICENCES", "CC BY 4.0", "ODbL 1.0" })
                CollectionAssert.Contains(rows, work);
            Assert.AreEqual("DHAKA: AFTER DARK", rows[menus.Chosen]);
            yield return Tap(keyboard.downArrowKey);
            Assert.AreEqual("Patrol cap", rows[menus.Chosen], "the heading is passed over");
            StringAssert.Contains("makehumancommunity.org/node/841", menus.HelpShown);
            yield return Tap(keyboard.enterKey);
            Assert.AreEqual("Credits", menus.Top, "nothing to press");
            yield return Tap(keyboard.escapeKey);
            Assert.AreEqual("Title", menus.Top);
            Assert.AreEqual("Credits", menus.RowNames[menus.Chosen], "back where you were");
        }

        // Escape stops the game and opens the pause menu; keys go down to Settings, into it, across to Graphics, and the field of view up a step; back saves and resumes.
        [UnityTest]
        public IEnumerator PauseAndChangeASetting()
        {
            var menus = Menus.Attach(new GameObject("menus"), input, null, null);
            made.Add(menus.gameObject);
            yield return null;
            Assert.IsFalse(menus.Open);
            yield return Tap(keyboard.escapeKey);
            yield return null;
            Assert.AreEqual("Pause", menus.Top);
            Assert.AreEqual(0, Time.timeScale, "the game stops");
            Assert.AreEqual(InputContext.Paused, input.Context);
            CollectionAssert.AreEqual(new[] { "Resume", "Map", "Character", "Settings", "Quit to desktop" }, menus.RowNames);
            yield return Tap(keyboard.downArrowKey);
            yield return Tap(keyboard.downArrowKey);
            yield return Tap(keyboard.downArrowKey);
            Assert.AreEqual(3, menus.Chosen);
            yield return Tap(keyboard.enterKey);
            Assert.AreEqual("Settings", menus.Top);
            Assert.AreEqual("Display", menus.Tab);
            yield return Tap(keyboard.eKey);
            Assert.AreEqual("Graphics", menus.Tab);
            Assert.AreEqual("Field of view", menus.RowNames[1]);
            yield return Tap(keyboard.downArrowKey);
            yield return Tap(keyboard.rightArrowKey);
            Assert.AreEqual(65, GameSettings.Current.FieldOfView, "right raises it a step");
            yield return Tap(keyboard.escapeKey);
            Assert.AreEqual("Pause", menus.Top);
            Assert.IsTrue(File.Exists(GameSettings.FilePath), "leaving the settings saves them");
            StringAssert.Contains("\"FieldOfView\": 65", File.ReadAllText(GameSettings.FilePath));
            yield return Tap(keyboard.escapeKey);
            Assert.IsFalse(menus.Open);
            Assert.AreEqual(1, Time.timeScale);
            Assert.AreEqual(InputContext.OnFoot, input.Context);
        }

        // The Controls tab lists the keys; choosing one and pressing a new key rebinds it and keeps it in the settings.
        [UnityTest]
        public IEnumerator RebindAKeyInTheMenu()
        {
            var menus = Menus.Attach(new GameObject("menus"), input, null, null);
            made.Add(menus.gameObject);
            yield return null;
            menus.Pause();
            yield return null;
            for (int i = 0; i < 3; i++) yield return Tap(keyboard.downArrowKey); // Resume, Map, Character, Settings
            yield return Tap(keyboard.enterKey);
            for (int i = 0; i < 3; i++) yield return Tap(keyboard.eKey);
            Assert.AreEqual("Controls", menus.Tab);
            int jump = menus.RowNames.ToList().IndexOf("Jump");
            Assert.Greater(jump, 0, "Jump is listed: " + string.Join(", ", menus.RowNames));
            while (menus.Chosen != jump) yield return Tap(keyboard.downArrowKey);
            yield return Tap(keyboard.enterKey);
            Assert.IsTrue(menus.Rebinding);
            Press(keyboard.kKey, queueEventOnly: true);
            yield return null;
            currentTime += 0.2; // the rebind waits a moment for a better match; the fixture's input clock moves only when told
            yield return null;
            Release(keyboard.kKey, queueEventOnly: true);
            yield return null;
            yield return null;
            Assert.IsFalse(menus.Rebinding);
            int index = input.Jump.GetBindingIndex(InputBinding.MaskByGroup("Keyboard&Mouse"));
            Assert.AreEqual("<Keyboard>/k", input.Jump.bindings[index].effectivePath);
            StringAssert.Contains("<Keyboard>/k", GameSettings.Current.Bindings);
        }

        // The map: it opens over the game, a press sets a waypoint where the cursor is and a route is found to it along the roads; the radar then shows the mark.
        [UnityTest]
        public IEnumerator TheMapSetsAWaypoint()
        {
            GameRoot.WorldDirForTests = null;
            var map = CityMap.BuildNow(GameRoot.WorldDir);
            Assert.IsNotNull(map, "no city map");
            Assert.Greater(map.Nodes, 100);
            Assert.AreEqual(5, map.Areas.Count);
            // the street and district at the start point, and a route across town
            Assert.IsNotNull(map.Area(FreeCamera.StartEast, FreeCamera.StartNorth));
            var way = map.Route(FreeCamera.StartEast, FreeCamera.StartNorth, FreeCamera.StartEast + 900, FreeCamera.StartNorth - 600);
            Assert.Greater(way.Count, 3, "no route across town");
            double length = 0;
            for (int i = 1; i < way.Count; i++) length += (way[i] - way[i - 1]).magnitude;
            Assert.That(length, Is.InRange(1000, 3500), "a way by the roads is longer than the straight line, not wildly");
            var menus = Menus.Attach(new GameObject("menus"), input, null, null);
            made.Add(menus.gameObject);
            yield return null;
            menus.ShowMap();
            yield return null;
            yield return null;
            Assert.AreEqual("Map", menus.Top);
            Assert.AreEqual(0, Time.timeScale);
            var view = menus.MapShown;
            menus.MarkAt(new Vector2(view.ViewWidth / 2 + 200, view.ViewHeight / 2 - 100));
            Assert.IsTrue(Waypoint.Active);
            Assert.AreEqual("Waypoint set", Notice.Last);
            // with no floating origin here, you are taken to be at the map's centre, where it opened
            Waypoint.Follow(view.East, view.North, true);
            Assert.Greater(Waypoint.Route.Count, 2, "no route to the waypoint");
            menus.MarkAt(view.ToView(Waypoint.East, Waypoint.North));
            Assert.IsFalse(Waypoint.Active, "pressing on the waypoint clears it");
            yield return Tap(keyboard.escapeKey);
            Assert.IsFalse(menus.Open);
            Assert.AreEqual(1, Time.timeScale);
        }

        // Tab held: the wheel opens, time slows to a quarter, the mouse points at the rifle (slot 3, to the left) and letting go takes it.
        [UnityTest]
        public IEnumerator TheWheelSlowsTimeAndChooses()
        {
            Assert.AreEqual(0, WeaponWheel.Toward(Vector2.up, 4));
            Assert.AreEqual(1, WeaponWheel.Toward(Vector2.right, 4));
            Assert.AreEqual(2, WeaponWheel.Toward(Vector2.down, 4));
            Assert.AreEqual(3, WeaponWheel.Toward(Vector2.left, 4));
            var you = Stand();
            var wheel = WeaponWheel.Attach(new GameObject("wheel"), input, you);
            made.Add(wheel.gameObject);
            yield return null;
            Press(keyboard.tabKey, queueEventOnly: true);
            yield return null;
            yield return null;
            Assert.IsTrue(wheel.Open);
            Assert.AreEqual(WeaponWheel.Slow, Time.timeScale);
            Set(mouse.delta, new Vector2(-80, 0), queueEventOnly: true);
            yield return null;
            Set(mouse.delta, Vector2.zero, queueEventOnly: true);
            yield return null;
            Assert.AreEqual(3, wheel.Pointed);
            Release(keyboard.tabKey, queueEventOnly: true);
            yield return null;
            yield return null;
            Assert.IsFalse(wheel.Open);
            Assert.AreEqual(1, Time.timeScale);
            Assert.AreEqual("Rifle", you.Arsenal.Now?.Name);
        }
    }
}
