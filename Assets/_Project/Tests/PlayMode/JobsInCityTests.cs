// Deliveries in the real city, as plain play starts them (Docs/PRODUCTION/FIRST_PLAYABLE.md): a parcel is offered on a street near you, marked in the street, on the radar and
// with a route; stopping in the light takes it and starts its clock; the clock running out loses it; the police being after you keep you from handing it over until they are
// not, and then you are paid and it is saved; going down loses the parcel and costs a tenth of your cash. The progress file goes to a folder of the test's own.
using System.Collections;
using System.IO;
using Dad.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Dad.Tests
{
    public class JobsInCityTests
    {
        string folder;

        [SetUp]
        public void Mark()
        {
            TestScenes.Mark();
            folder = Path.Combine(Path.GetTempPath(), "dad-progress-" + System.Guid.NewGuid().ToString("N"));
            Progress.Folder = folder;
            Player.Automatic = true;
            Jobs.Automatic = true;
            Heat.Clear();
            Waypoint.Clear();
        }

        [UnityTearDown]
        public IEnumerator Clean()
        {
            Player.Automatic = false;
            Jobs.Automatic = false;
            Heat.Clear();
            Waypoint.Clear();
            yield return TestScenes.Clean();
            Progress.Folder = null;
            Progress.Load();
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }

        static IEnumerator Seconds(float s)
        {
            float until = Time.time + s;
            while (Time.time < until) yield return null;
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

        static double Distance(double e1, double n1, double e2, double n2) => System.Math.Sqrt((e1 - e2) * (e1 - e2) + (n1 - n2) * (n1 - n2));

        [UnityTest]
        public IEnumerator ADeliveryIsOfferedTakenLostAndPaid()
        {
            SceneManager.LoadScene("Boot");
            yield return null;
            var root = Object.FindAnyObjectByType<GameRoot>();
            root.Day.MinutesPerSecond = 0;
            root.Day.Set(DayCycle.DefaultDate, 11, Weather.Clear);
            var you = Object.FindAnyObjectByType<Player>();
            var jobs = Object.FindAnyObjectByType<Jobs>();
            var hud = Object.FindAnyObjectByType<Hud>();
            Assert.IsNotNull(jobs, "plain play gives jobs");
            Assert.AreSame(jobs, Jobs.Active);
            Assert.AreEqual(0, Progress.Current.Cash, "a fresh start in an empty folder");
            yield return Until(() => you.Now == Player.Phase.Walking, 120, "standing in the street within two minutes");
            yield return Until(() => GameRoot.MapBuilding != null && GameRoot.MapBuilding.IsCompleted, 60, "the city map within a minute");
            Assert.IsNotNull(CityMap.Active);

            // ---- an offer of its own, near you, on a road
            yield return Until(() => jobs.Now == Jobs.Stage.Offered, 30, "a parcel offered within 30 s of standing");
            Assert.IsTrue(jobs.Where(out double e, out double n, out _));
            double near = Distance(e, n, jobs.PickEast, jobs.PickNorth);
            Assert.That(near, Is.InRange(Jobs.OfferNear - 30, Jobs.OfferFar + 30), "the pickup is a short way off (you may have moved a little)");
            Assert.That(Distance(jobs.PickEast, jobs.PickNorth, jobs.DropEast, jobs.DropNorth), Is.InRange(Jobs.TripNear, Jobs.TripFar));
            Assert.GreaterOrEqual(jobs.Trip, (float)Jobs.TripNear, "the road there is no shorter than the line");
            Assert.AreEqual(Jobs.TimeFor(jobs.Trip), jobs.Allowed, 1e-3);
            Assert.IsTrue(Waypoint.Active && Waypoint.ForJob, "the route goes to the pickup");
            Assert.AreEqual(jobs.PickEast, Waypoint.East, 1e-6);
            yield return null;
            StringAssert.StartsWith("Pick up the parcel", hud.Shown("objective"));
            Assert.IsTrue(hud.Showing("job"));
            Assert.IsFalse(hud.Showing("timer"), "no clock before you have it");
            Assert.IsTrue(hud.Showing("jobmark"), "on the radar, at its edge if far");
            Assert.IsFalse(hud.Showing("waypoint"), "not a second mark for the same place");
            Assert.AreEqual("Tk 0", hud.Shown("cash"));
            var light = GameObject.Find("Job marker");
            Assert.IsNotNull(light, "a column of light in the street");
            Assert.IsTrue(light.activeInHierarchy);
            Assert.IsNotNull(light.GetComponent<MeshRenderer>().sharedMaterial, "drawn with Materials/Beacon");
            Assert.AreEqual("Dad/Beacon", light.GetComponent<MeshRenderer>().sharedMaterial.shader.name);
            Assert.Less(Mathf.Abs(light.transform.position.y - you.Body.transform.position.y), 25, "on the ground, not in the sky or under it");

            // ---- the rest are placed where the test wants them
            Jobs.Automatic = false;
            Assert.IsTrue(jobs.Where(out e, out n, out _));
            var rnd = new System.Random(7);
            Assert.IsTrue(CityMap.Active.RoadPoint(rnd, e, n, Jobs.TripNear, Jobs.TripFar, out double de, out double dn), "a road to deliver to");
            Assert.IsTrue(jobs.Offer(e, n, de, dn));
            yield return Until(() => jobs.Now == Jobs.Stage.Carrying, 3, "standing in the light takes the parcel");
            StringAssert.StartsWith("Parcel taken", Notice.Last);
            Assert.AreEqual(de, Waypoint.East, 1e-6, "the route now goes to the drop");
            yield return Seconds(1.2f);
            Assert.Less(jobs.Left, jobs.Allowed - 1, "its clock runs");
            StringAssert.StartsWith("Deliver the parcel", hud.Shown("objective"));
            Assert.IsTrue(hud.Showing("timer"));
            StringAssert.IsMatch(@"^\d+:\d\d$", hud.Shown("timer"));
            StringAssert.StartsWith("pays Tk ", hud.Shown("pays"));

            // ---- too late
            jobs.Pass(jobs.Left + 1);
            yield return null;
            yield return null;
            Assert.AreEqual(Jobs.Stage.Waiting, jobs.Now);
            StringAssert.StartsWith("Too late", Notice.Last);
            Assert.AreEqual(1, Progress.Current.Failed);
            Assert.AreEqual(0, Progress.Current.Cash);
            Assert.IsFalse(Waypoint.Active, "the job's route goes with it");
            Assert.IsFalse(hud.Showing("job"));

            // ---- wanted: held until the police are lost, then paid and saved
            Heat.Raise(1);
            Assert.IsTrue(jobs.Where(out e, out n, out _));
            Assert.IsTrue(jobs.Offer(e, n, e + 3, n));
            float trip = jobs.Trip, allowed = jobs.Allowed;
            bool told = false;
            float until = Time.time + 1.5f;
            while (Time.time < until)
            {
                told |= Notice.Last == "Lose the police before you deliver";
                yield return null;
            }
            Assert.AreEqual(Jobs.Stage.Carrying, jobs.Now, "not handed over while wanted");
            Assert.IsTrue(told, "told why");
            Heat.Clear();
            yield return Until(() => jobs.Now == Jobs.Stage.Waiting, 2, "delivered once the police are lost");
            StringAssert.StartsWith("Delivered: Tk ", Notice.Last);
            int paid = Progress.Current.Cash;
            Assert.That(paid, Is.InRange(Jobs.Pay(trip, 0, allowed), Jobs.Pay(trip, allowed, allowed)), "the fixed part, the road, and some of the time to spare");
            Assert.GreaterOrEqual(paid, Jobs.Fixed);
            Assert.AreEqual(paid, Progress.Current.Earned);
            Assert.AreEqual(1, Progress.Current.Deliveries);
            yield return null;
            Assert.AreEqual(Jobs.Money(paid), hud.Shown("cash"));
            var kept = JsonUtility.FromJson<Progress>(File.ReadAllText(Progress.FilePath));
            Assert.AreEqual(paid, kept.Cash, "saved as it is earned");
            Assert.AreEqual(1, kept.Deliveries);
            Assert.AreEqual(1, kept.Failed);

            // ---- going down: the parcel is lost and the treatment costs a tenth of the cash
            Progress.Earn(1000 - paid);
            Assert.IsTrue(jobs.Where(out e, out n, out _));
            Assert.IsTrue(jobs.Offer(e, n, de, dn));
            yield return Until(() => jobs.Now == Jobs.Stage.Carrying, 3, "taken");
            you.Vitals.Hurt(10000, you.Body.transform.position + Vector3.forward * 5);
            yield return Until(() => you.Now == Player.Phase.Down, 3, "down");
            yield return null;
            Assert.AreEqual(Jobs.Stage.Waiting, jobs.Now);
            Assert.AreEqual(2, Progress.Current.Failed);
            Assert.AreEqual(900, Progress.Current.Cash);
            kept = JsonUtility.FromJson<Progress>(File.ReadAllText(Progress.FilePath));
            Assert.AreEqual(900, kept.Cash);
            Assert.AreEqual(2, kept.Failed);
        }
    }
}
