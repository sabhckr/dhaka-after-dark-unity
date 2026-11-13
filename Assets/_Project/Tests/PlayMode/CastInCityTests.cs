// Choosing who you play, in the real city (Cast, owner's word of 2026-11-13): the start screen offers the twenty; a choice puts the new body where the old one stood,
// with your health and guns, and keeps it with your progress; the crowd then has nobody in your body, and a fighter who would have it is given the next person instead.
// You cannot change in a car. Pictures for review: Evidence/local/ui-cast-*.png.
using System.Collections;
using System.IO;
using System.Linq;
using Dad.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Dad.Tests
{
    public class CastInCityTests
    {
        string folder;

        [SetUp]
        public void Mark()
        {
            TestScenes.Mark();
            folder = Path.Combine(Path.GetTempPath(), "dad-cast-" + System.Guid.NewGuid().ToString("N"));
            Progress.Folder = folder;
            Player.Automatic = true;
            Jobs.Automatic = false;
            Menus.Title = true;
            Heat.Clear();
            Waypoint.Clear();
        }

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
            Cast.Read(null);
            Progress.Folder = null;
            Progress.Load();
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }

        static IEnumerator Until(System.Func<bool> done, float seconds, string what)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (!done())
            {
                Assert.Less(Time.realtimeSinceStartup, until, what);
                yield return null;
            }
        }

        static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }

        static int OthersIn(string model, Player you) =>
            Object.FindObjectsByType<Person>(FindObjectsSortMode.None).Count(p => p.Who == model && p.gameObject.activeInHierarchy && p != you.Body.Person);

        [UnityTest, Timeout(600000)]
        public IEnumerator YouChooseWhoYouPlay()
        {
            SceneManager.LoadScene("Boot");
            yield return null;
            var root = Object.FindAnyObjectByType<GameRoot>();
            root.Day.MinutesPerSecond = 0;
            root.Day.Set(DayCycle.DefaultDate, 16.5, Weather.Clear);
            var you = Object.FindAnyObjectByType<Player>();
            var menus = Object.FindAnyObjectByType<Menus>();
            Assert.AreEqual(Cast.First, Cast.You, "a fresh start is the first of the twenty");
            yield return Until(() => you.Now == Player.Phase.Walking && CityMap.Active != null, 120, "standing in the street within two minutes");
            Assert.AreEqual(Cast.First, you.Body.Person.Who);
            yield return Frames(30);
            Assert.AreEqual("Title", menus.Top);

            // ---- a woman, from the start screen's own row: the body changes behind the menu
            you.Vitals.Hurt(30, you.Body.transform.position + you.Body.transform.forward * 5);
            float health = you.Vitals.Health;
            int guns = you.Arsenal.Slots.Count;
            var at = you.Body.transform.position;
            Assert.IsTrue(you.Become("npc_f07"));
            yield return Frames(20);
            Assert.AreEqual("npc_f07", you.Body.Person.Who);
            Assert.AreEqual("npc_f07", OnFoot.Who);
            Assert.AreEqual(1, Object.FindObjectsByType<OnFoot>(FindObjectsSortMode.None).Length, "one body");
            Assert.Less(Vector3.Distance(at, you.Body.transform.position), 0.5f, "where the old one stood");
            Assert.AreEqual(health, you.Vitals.Health, 0.5f, "with your health");
            Assert.AreEqual(guns, you.Arsenal.Slots.Count, "and your guns");
            Assert.AreEqual(you.Body.transform, FloatingOrigin.Active.Target, "the world follows the new body");
            Assert.AreEqual("npc_f07", Progress.Current.Character);
            yield return PlayerInterfaceShots.Picture("cast-1-start-woman-7");

            // ---- in the street: nobody else in your body
            menus.CloseAll();
            yield return Frames(120);
            Assert.AreEqual(0, OthersIn("npc_f07", you), "the walker in this body is left out of the crowd");
            var record = NpcRoster.Load("People/fighters").people.First(r => r.model == "npc_f03");
            Assert.IsTrue(you.Become("npc_f03"));
            yield return Frames(5);
            var gang = Fighter.Spawn(record, Fighter.Side.Gang, you.Body.transform.position + you.Body.transform.forward * 6, 180, calm: true);
            Assert.IsNotNull(gang);
            Assert.AreEqual("npc_f04", gang.Body.Who, "a gang member in your body is given the next woman's");
            Assert.AreEqual("npc_f03", record.model, "the roster is not changed");
            Object.Destroy(gang.gameObject);
            Assert.IsTrue(you.Become("npc_m10"));
            you.Arsenal.Select(1);
            yield return Frames(30);
            yield return PlayerInterfaceShots.Picture("cast-2-street-man-10");

            // ---- not in a car
            Assert.IsTrue(you.TryGetIn(), "beside the car");
            yield return Until(() => you.Now == Player.Phase.Driving, 30, "seated");
            Assert.IsFalse(you.Become("npc_m01"), "not while driving");
            Assert.AreEqual("npc_m10", Cast.You);
            Assert.IsTrue(you.TryGetOut());
            yield return Until(() => you.Now == Player.Phase.Walking, 30, "out");
            Assert.AreEqual("npc_m10", you.Body.Person.Who, "the same person gets out");

            // ---- kept for the next run
            var kept = JsonUtility.FromJson<Progress>(File.ReadAllText(Progress.FilePath));
            Assert.AreEqual("npc_m10", kept.Character);
        }
    }
}
