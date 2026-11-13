// The credits screen over the real city's start screen (owner, 2026-11-13). Pictures for review: Evidence/local/ui-credits-*.png, the top of the list and the
// weapons with the rifle chosen (its source in the help line), and the city with the map data chosen.
using System.Collections;
using Dad.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Dad.Tests
{
    public class CreditsInCityTests
    {
        [SetUp]
        public void Mark()
        {
            TestScenes.Mark();
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

        [UnityTest, Timeout(600000)]
        public IEnumerator TheCreditsOverTheStartScreen()
        {
            SceneManager.LoadScene("Boot");
            yield return null;
            var root = Object.FindAnyObjectByType<GameRoot>();
            root.Day.MinutesPerSecond = 0;
            root.Day.Set(DayCycle.DefaultDate, 16.5, Weather.Clear);
            var you = Object.FindAnyObjectByType<Player>();
            var menus = Object.FindAnyObjectByType<Menus>();
            yield return Until(() => you.Now == Player.Phase.Walking && CityMap.Active != null, 120, "standing in the street within two minutes");
            yield return Frames(30);
            Assert.AreEqual("Title", menus.Top);

            menus.ShowCredits();
            yield return Frames(10);
            Assert.AreEqual("Credits", menus.Top);
            Assert.AreEqual("DHAKA: AFTER DARK", menus.RowNames[menus.Chosen]);
            Assert.AreEqual("A CITY THAT NEVER SLEEPS.", menus.HelpShown);
            yield return PlayerInterfaceShots.Picture("credits-1-top");

            menus.ShowCredits("FN Herstal Scar Light");
            yield return Frames(10);
            Assert.AreEqual("FN Herstal Scar Light", menus.RowNames[menus.Chosen]);
            StringAssert.Contains("sketchfab.com/3d-models/fn-herstal-scar-light", menus.HelpShown);
            yield return PlayerInterfaceShots.Picture("credits-2-weapons");

            menus.ShowCredits("Map data");
            yield return Frames(10);
            Assert.AreEqual("Map data", menus.RowNames[menus.Chosen]);
            StringAssert.Contains("openstreetmap.org/copyright", menus.HelpShown);
            yield return PlayerInterfaceShots.Picture("credits-3-city");

            menus.Back();
            yield return null;
            Assert.AreEqual("Title", menus.Top);
        }
    }
}
