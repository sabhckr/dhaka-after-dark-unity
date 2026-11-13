// Who you can play (Cast, owner's word of 2026-11-13): the twenty people, ten men and ten women, all in Resources/People; choosing goes round the list; the choice is kept
// with your progress; and nobody else in the street is given your body.
using System.IO;
using System.Linq;
using Dad.Core;
using NUnit.Framework;

namespace Dad.Tests
{
    public class CastTests
    {
        string folder;

        [SetUp]
        public void Point()
        {
            folder = Path.Combine(Path.GetTempPath(), "dad-cast-" + System.Guid.NewGuid().ToString("N"));
            Progress.Folder = folder;
            Progress.Load();
            Cast.Read(null);
        }

        [TearDown]
        public void Back()
        {
            Cast.Read(null);
            Progress.Folder = null;
            Progress.Load();
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }

        [Test]
        public void TheTwentyAreAllThere()
        {
            Assert.AreEqual(10, Cast.Men.Length);
            Assert.AreEqual(10, Cast.Women.Length);
            Assert.AreEqual(20, Cast.All.Distinct().Count());
            foreach (var who in Cast.All)
            {
                Assert.IsTrue(File.Exists($"Assets/_Project/Resources/People/{who}.fbx"), who);
                Assert.IsNotEmpty(Cast.Clothes(who), who);
            }
            Assert.IsFalse(File.Exists("Assets/_Project/Resources/People/MalePlayer.fbx"), "Isaac was removed (owner, 2026-11-13)");
            Assert.AreEqual("Man 1", Cast.Name("npc_m01"));
            Assert.AreEqual("Woman 10", Cast.Name("npc_f10"));
            Assert.AreEqual(Cast.First, Cast.You, "before any choice, the first");
            Assert.IsTrue(Cast.Has(Cast.First));
        }

        [Test]
        public void ChoosingGoesRoundMenThenWomen()
        {
            Cast.Use("npc_m10");
            Assert.AreEqual("npc_f01", Cast.Step(1));
            Cast.Use("npc_f10");
            Assert.AreEqual("npc_m01", Cast.Step(1), "round to the first");
            Cast.Use("npc_m01");
            Assert.AreEqual("npc_f10", Cast.Step(-1));
            Cast.Use("nobody");
            Assert.AreEqual("npc_m01", Cast.You, "an unknown name changes nothing");
        }

        [Test]
        public void TheChoiceIsKeptWithYourProgress()
        {
            int changes = 0;
            void Count() => changes++;
            Cast.Changed += Count;
            try
            {
                Cast.Use("npc_f07");
                Assert.AreEqual(1, changes);
                Assert.AreEqual("npc_f07", Progress.Current.Character);
                Progress.Save();
                Cast.Read(null);
                Assert.AreEqual(Cast.First, Cast.You);
                Cast.Read(Progress.Load());
                Assert.AreEqual("npc_f07", Cast.You, "read back");
                File.WriteAllText(Progress.FilePath, "{\"Version\":1,\"Character\":\"MalePlayer\"}");
                Cast.Read(Progress.Load());
                Assert.AreEqual("", Progress.Current.Character, "someone not of the twenty is not kept");
                Assert.AreEqual(Cast.First, Cast.You);
            }
            finally { Cast.Changed -= Count; }
        }

        [Test]
        public void NobodyElseWearsYourBody()
        {
            Cast.Use("npc_m03"); // an officer's body
            Assert.AreEqual("npc_m04", Cast.Other("npc_m03"), "the next man");
            Assert.AreEqual("npc_m01", Cast.Other("npc_m01"), "anyone else keeps theirs");
            Cast.Use("npc_m10");
            Assert.AreEqual("npc_m01", Cast.Other("npc_m10"), "round the men");
            Cast.Use("npc_f03");
            Assert.AreEqual("npc_f04", Cast.Other("npc_f03"), "a woman for a woman");
            var record = new NpcRecord { id = "GANG_003", model = "npc_f03", height = 1.66f, gun = "glock" };
            var other = record.As(Cast.Other(record.model));
            Assert.AreEqual("npc_f04", other.model);
            Assert.AreEqual("GANG_003", other.id);
            Assert.AreEqual("glock", other.gun);
            Assert.AreEqual("npc_f03", record.model, "the roster's record is not changed");
            // the street's people and the fighters are all of the twenty, so these rules cover everyone
            foreach (var r in NpcRoster.Load().people.Concat(NpcRoster.Load("People/fighters").people))
                Assert.IsTrue(Cast.Has(r.model), r.id + " " + r.model);
        }
    }
}
