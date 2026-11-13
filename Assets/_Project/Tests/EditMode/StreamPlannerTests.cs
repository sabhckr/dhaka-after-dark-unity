// StreamPlanner decisions, exactly: rings, order, hysteresis, lead, cancellation, retries
// and the Farmgate slice.
using System.Collections.Generic;
using System.Linq;
using Dad.World;
using NUnit.Framework;

namespace Dad.Tests
{
    public class StreamPlannerTests
    {
        readonly List<ChunkCell> unload = new List<ChunkCell>(), cancel = new List<ChunkCell>(), take = new List<ChunkCell>();

        static ChunkCell C(int x, int z) => new ChunkCell(x, z);

        void Step(StreamPlanner p, ChunkCell focus, ChunkCell? lead = null)
        {
            unload.Clear();
            cancel.Clear();
            p.Update(focus, lead, unload, cancel);
        }

        void LoadAll(StreamPlanner p)
        {
            take.Clear();
            p.TakeNext(1000, take);
            foreach (var c in take)
            {
                Assert.IsTrue(p.Loaded(c), c.ToString());
                p.Applied(c);
            }
        }

        static HashSet<ChunkCell> Ring(ChunkCell f, int r)
        {
            var s = new HashSet<ChunkCell>();
            for (int x = -r; x <= r; x++) for (int z = -r; z <= r; z++) s.Add(C(f.X + x, f.Z + z));
            return s;
        }

        static HashSet<ChunkCell> In(StreamPlanner p, ChunkState s)
        {
            var l = new List<ChunkCell>();
            p.CellsIn(s, l);
            return new HashSet<ChunkCell>(l);
        }

        [Test]
        public void RingIsTakenNearestFirstWithoutDuplicates()
        {
            var p = new StreamPlanner(2, 4, null);
            Step(p, C(10, 10));
            Step(p, C(10, 10));
            take.Clear();
            Assert.AreEqual(25, p.TakeNext(1000, take));
            Assert.AreEqual(C(10, 10), take[0], "focus cell first");
            CollectionAssert.AreEquivalent(Ring(C(10, 10), 2), take);
            for (int i = 1; i < take.Count; i++)
                Assert.LessOrEqual(StreamPlanner.Chebyshev(C(10, 10), take[i - 1]), StreamPlanner.Chebyshev(C(10, 10), take[i]), "nearest first");
            Assert.AreEqual(0, p.TakeNext(1000, take), "nothing queued twice");
        }

        [Test]
        public void HysteresisKeepsCellsUntilTheUnloadRadius()
        {
            var p = new StreamPlanner(2, 4, null);
            Step(p, C(10, 10));
            LoadAll(p);
            Assert.AreEqual(25, p.Count(ChunkState.Resident));
            Step(p, C(11, 10));
            Assert.IsEmpty(unload, "one cell east: the west column is 3 away and stays");
            LoadAll(p);
            Assert.AreEqual(30, p.Count(ChunkState.Resident));
            Step(p, C(12, 10));
            CollectionAssert.AreEquivalent(Enumerable.Range(8, 5).Select(z => C(8, z)), unload, "column x = 8 is 4 away");
            Assert.AreEqual(ChunkState.None, p.StateOf(C(8, 10)));
            Step(p, C(11, 10));
            take.Clear();
            p.TakeNext(1000, take);
            Assert.IsEmpty(take, "back one cell: x = 8 is 3 away, outside the load radius, not reloaded");
        }

        [Test]
        public void LeadCellsComeAfterTheFocusRingAndInsideTheUnloadRadius()
        {
            var p = new StreamPlanner(2, 4, null);
            Step(p, C(10, 10), C(13, 10));
            take.Clear();
            Assert.AreEqual(30, p.TakeNext(1000, take));
            CollectionAssert.AreEquivalent(Ring(C(10, 10), 2), take.Take(25), "focus ring first");
            CollectionAssert.AreEquivalent(Enumerable.Range(8, 5).Select(z => C(13, z)), take.Skip(25), "then x = 13; 14 and 15 are at the unload radius");
        }

        [Test]
        public void LoadsAreCancelledAtTheUnloadRadiusAndKeptInsideIt()
        {
            var p = new StreamPlanner(2, 4, null);
            Step(p, C(10, 10));
            take.Clear();
            p.TakeNext(1000, take);
            Step(p, C(13, 10));
            Assert.IsEmpty(unload);
            CollectionAssert.AreEquivalent(Enumerable.Range(8, 5).Select(z => C(9, z)).Concat(Enumerable.Range(8, 5).Select(z => C(8, z))), cancel, "x = 8 and 9 are 4 and 5 away");
            Assert.IsFalse(p.Loaded(C(9, 10)), "a cancelled load is not applied");
            Assert.IsTrue(p.Loaded(C(10, 10)), "3 away: still kept");
            Assert.AreEqual(ChunkState.Ready, p.StateOf(C(10, 10)));
            Step(p, C(20, 10));
            CollectionAssert.Contains(unload, C(10, 10), "a Ready cell past the radius is unloaded too");
        }

        [Test]
        public void QueuedCellsNoLongerWantedAreSkipped()
        {
            var p = new StreamPlanner(2, 4, null);
            Step(p, C(10, 10));
            Step(p, C(11, 10));
            take.Clear();
            p.TakeNext(1000, take);
            Assert.IsFalse(take.Contains(C(8, 10)), "queued at 10, unwanted at 11 (3 away)");
            Assert.AreEqual(25, take.Count);
        }

        [Test]
        public void FailedLoadsRetryOnceThenStop()
        {
            var p = new StreamPlanner(0, 1, null);
            Step(p, C(0, 0));
            take.Clear();
            p.TakeNext(10, take);
            p.Failed(C(0, 0));
            Assert.AreEqual(ChunkState.None, p.StateOf(C(0, 0)));
            Step(p, C(0, 0));
            take.Clear();
            Assert.AreEqual(1, p.TakeNext(10, take), "retried");
            p.Failed(C(0, 0));
            Assert.AreEqual(ChunkState.Dead, p.StateOf(C(0, 0)));
            Step(p, C(0, 0));
            take.Clear();
            Assert.AreEqual(0, p.TakeNext(10, take), "dead cells are not requested");
            p.ResetFailures();
            Step(p, C(0, 0));
            Assert.AreEqual(1, p.TakeNext(10, take));
        }

        [Test]
        public void OnlyAvailableCellsAreRequested()
        {
            // The Farmgate slice: cells X 6..9, Z 27..29.
            var p = new StreamPlanner(2, 4, c => c.X >= 6 && c.X <= 9 && c.Z >= 27 && c.Z <= 29);
            Step(p, C(7, 28));
            take.Clear();
            Assert.AreEqual(12, p.TakeNext(100, take), "x 5..9 and z 26..30 cut to the slice: all 4 x 3 cells");
            Assert.IsTrue(take.All(c => c.X >= 6 && c.X <= 9 && c.Z >= 27 && c.Z <= 29));
        }

        [Test]
        public void RadiiAreValidated()
        {
            Assert.Throws<System.ArgumentException>(() => new StreamPlanner(2, 2, null));
            Assert.Throws<System.ArgumentException>(() => new StreamPlanner(-1, 2, null));
        }
    }
}
