// The first playable's numbers and its saved progress (Docs/PRODUCTION/FIRST_PLAYABLE.md): what a delivery pays and how long it allows, how money and the clock are written,
// and the progress file kept like the settings file, with a backup taken when it is written and used when it cannot be read.
using System.IO;
using Dad.Core;
using NUnit.Framework;
using UnityEngine;

namespace Dad.Tests
{
    public class JobsTests
    {
        string folder;

        [SetUp]
        public void Point()
        {
            folder = Path.Combine(Path.GetTempPath(), "dad-progress-" + System.Guid.NewGuid().ToString("N"));
            Progress.Folder = folder;
        }

        [TearDown]
        public void Back()
        {
            Progress.Folder = null;
            Progress.Load();
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }

        [Test]
        public void PayGrowsWithTheRoadAndTheTimeToSpare()
        {
            // 1,000 m of road: 150 + 400 = 550 at the last second; half again with all the time left
            Assert.AreEqual(550, Jobs.Pay(1000, 0, 168));
            Assert.AreEqual(830, Jobs.Pay(1000, 168, 168));
            Assert.AreEqual(690, Jobs.Pay(1000, 84, 168));
            Assert.Greater(Jobs.Pay(1400, 0, 200), Jobs.Pay(600, 0, 200));
            Assert.AreEqual(0, Jobs.Pay(1000, 50, 168) % 10, "rounded to ten taka");
            Assert.AreEqual(Jobs.Pay(1000, 0, 168), Jobs.Pay(1000, -5, 168), "late is no less than the last second");
        }

        [Test]
        public void TimeAllowedIsTheRoadAtAPaceWithAMargin()
        {
            Assert.AreEqual(1000 / Jobs.Pace + Jobs.Margin, Jobs.TimeFor(1000), 1e-4);
            Assert.AreEqual(Jobs.Least, Jobs.TimeFor(10), "never less than a minute");
            // the shortest trip offered, 600 m, is a minute and a half or so; the longest road a few minutes
            Assert.That(Jobs.TimeFor((float)Jobs.TripNear), Is.InRange(90f, 120f));
            Assert.That(Jobs.TimeFor(2500), Is.InRange(300f, 420f));
        }

        [Test]
        public void MoneyAndTheClockAreWrittenPlainly()
        {
            Assert.AreEqual("Tk 0", Jobs.Money(0));
            Assert.AreEqual("Tk 1,250", Jobs.Money(1250));
            Assert.AreEqual("2:05", Jobs.Clock(124.2f));
            Assert.AreEqual("0:00", Jobs.Clock(-3));
        }

        [Test]
        public void ProgressIsKeptAndItsBackupUsed()
        {
            var p = Progress.Load();
            Assert.AreEqual(0, p.Cash);
            Assert.AreEqual("", Progress.Note);
            Progress.Earn(300);
            Progress.Current.Deliveries = 1;
            Progress.Save();
            Assert.IsTrue(File.Exists(Progress.FilePath));
            Assert.IsFalse(File.Exists(Progress.BackupPath), "no backup before a second write");
            Progress.Earn(200);
            Progress.Current.Deliveries = 2;
            Progress.Save();
            Assert.IsTrue(File.Exists(Progress.BackupPath));
            Assert.IsFalse(File.Exists(Progress.FilePath + ".new"), "nothing half-written left");

            Progress.Load();
            Assert.AreEqual(500, Progress.Current.Cash);
            Assert.AreEqual(500, Progress.Current.Earned);
            Assert.AreEqual(2, Progress.Current.Deliveries);

            // a broken file: the backup, one write behind
            File.WriteAllText(Progress.FilePath, "{ not json");
            Progress.Load();
            Assert.AreEqual(300, Progress.Current.Cash);
            StringAssert.Contains("backup", Progress.Note);

            // both broken: a fresh start, said so
            File.WriteAllText(Progress.BackupPath, "{ also broken");
            Progress.Load();
            Assert.AreEqual(0, Progress.Current.Cash);
            StringAssert.Contains("fresh", Progress.Note);
        }

        [Test]
        public void ProgressFromALaterGameOrWithBadNumbersIsNotTakenAsItIs()
        {
            Directory.CreateDirectory(folder);
            File.WriteAllText(Progress.FilePath, "{\"Version\": 99, \"Cash\": 5000}");
            Progress.Load();
            Assert.AreEqual(0, Progress.Current.Cash, "a version this game does not know is not read");
            File.WriteAllText(Progress.FilePath, "{\"Version\": 1, \"Cash\": -40, \"Deliveries\": 3}");
            Progress.Load();
            Assert.AreEqual(0, Progress.Current.Cash, "never below nothing");
            Assert.AreEqual(3, Progress.Current.Deliveries);
        }

        [Test]
        public void SpendingTakesNoMoreThanThereIs()
        {
            Progress.Load();
            int changes = 0;
            void Count(int _) => changes++;
            Progress.Changed += Count;
            try
            {
                Progress.Earn(120);
                Assert.AreEqual(50, Progress.Spend(50));
                Assert.AreEqual(70, Progress.Spend(500));
                Assert.AreEqual(0, Progress.Current.Cash);
                Assert.AreEqual(0, Progress.Spend(10));
                Progress.Earn(-5);
                Assert.AreEqual(0, Progress.Current.Cash);
                Assert.AreEqual(3, changes, "earned, spent, spent: nothing for the empty ones");
            }
            finally { Progress.Changed -= Count; }
        }

        [Test]
        public void ABatchRunWithNoFolderWritesNothing()
        {
            Progress.Folder = null;
            Assume.That(Application.isBatchMode, "only a batch run keeps no file");
            Progress.Load();
            Progress.Earn(100);
            Progress.Save();
            Progress.Folder = folder;
            Assert.IsFalse(File.Exists(Progress.FilePath));
        }
    }
}
