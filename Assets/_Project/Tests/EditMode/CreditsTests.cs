// The credits screen against the records (owner, 2026-11-13: an in-game credits screen for the CC-BY parts): every part of the twenty people whose licence asks for
// credit, every licensed model in Resources, every downloaded building and every author the project's credit list names is on the screen, with its licence explained.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Dad.Core;
using NUnit.Framework;
using UnityEngine;

namespace Dad.Tests
{
    public class CreditsTests
    {
        static string Project => Path.GetDirectoryName(Application.dataPath);
        static List<Credits.Entry> List => Credits.Load();

        // The table rows of a Markdown file, as cells.
        static IEnumerable<string[]> Rows(string path, string after = null)
        {
            bool on = after == null;
            foreach (var line in File.ReadAllLines(path))
            {
                if (after != null && line.StartsWith("## ")) on = line.Trim() == after;
                if (!on || !line.StartsWith("|") || line.StartsWith("|---")) continue;
                yield return line.Trim().Trim('|').Split('|').Select(c => c.Trim()).ToArray();
            }
        }

        [Test]
        public void TheListReads()
        {
            var list = List;
            Assert.Greater(list.Count, 30);
            Assert.AreEqual("DHAKA: AFTER DARK", list[0].Title);
            Assert.AreEqual("MADE BY ISHAK HASAN SABBiR", list[0].By);
            foreach (var e in list)
            {
                Assert.IsNotEmpty(e.Title, "a line without a title");
                Assert.IsNotEmpty(e.By, e.Title + " has nobody named");
                Assert.IsNotEmpty(e.Id, e.Title + " has no id");
            }
            Assert.AreEqual(list.Count, list.Select(e => e.Id).Distinct().Count(), "ids are used once");
            CollectionAssert.AreEqual(new[] { "THE PEOPLE'S CLOTHES AND GEAR", "WEAPONS", "THE CITY", "WITH THANKS", "LICENCES" }, Credits.Sections(list));
            var map = list.FirstOrDefault(e => e.Id == "osm");
            Assert.IsNotNull(map, "the city is drawn from OpenStreetMap data (ODbL): its credit must be shown");
            Assert.AreEqual("© OpenStreetMap contributors", map.By);
            Assert.AreEqual("openstreetmap.org/copyright", map.Source);
        }

        [Test]
        public void EveryPartOfThePeopleThatAsksForCreditIsListed()
        {
            var ids = new HashSet<string>(List.Select(e => e.Id));
            int asked = 0;
            foreach (var c in Rows(Path.Combine(Project, "Docs/ASSET_INVENTORY/PEOPLE_CREDITS.md")))
            {
                if (c.Length != 6 || c[4] != "CC-BY") continue;
                asked++;
                Assert.IsTrue(ids.Contains(c[1]), c[1] + " by " + c[3] + " is CC-BY and not on the credits screen");
            }
            Assert.AreEqual(21, asked, "the CC-BY parts the record lists");
            Assert.IsTrue(ids.Contains("plate_carrier"), "the plate carrier (Sketchfab, credit asked for)");
        }

        // The title and licence a Sketchfab model carries inside it (glTF asset extras).
        static (string title, string licence) Stamp(string path)
        {
            string json;
            if (path.EndsWith(".gltf")) json = File.ReadAllText(path);
            else
            {
                var b = File.ReadAllBytes(path);
                int length = System.BitConverter.ToInt32(b, 12);
                json = Encoding.UTF8.GetString(b, 20, length);
            }
            var title = Regex.Match(json, "\"title\"\\s*:\\s*\"([^\"]*)\"");
            var licence = Regex.Match(json, "\"license\"\\s*:\\s*\"([^\" ]*)");
            return (title.Success ? title.Groups[1].Value : null, licence.Success ? licence.Groups[1].Value : null);
        }

        [Test]
        public void EveryLicensedModelInTheGameIsListed()
        {
            var list = List;
            var models = Directory.GetFiles(Path.Combine(Application.dataPath, "_Project/Resources"), "*.gl*", SearchOption.AllDirectories)
                .Where(f => f.EndsWith(".glb") || f.EndsWith(".gltf")).ToArray();
            Assert.GreaterOrEqual(models.Length, 3);
            foreach (var m in models)
            {
                var (title, licence) = Stamp(m);
                if (licence == null) continue;
                var e = list.FirstOrDefault(x => x.Title == title);
                Assert.IsNotNull(e, $"{Path.GetFileName(m)}: \"{title}\" ({licence}) is not on the credits screen");
                string shown = licence.Replace("CC-BY-NC-", "CC BY-NC ").Replace("CC-BY-", "CC BY ");
                Assert.AreEqual(shown, e.Licence, title);
            }
        }

        [Test]
        public void EveryDownloadedBuildingIsListed()
        {
            var ids = new HashSet<string>(List.Select(e => e.Id));
            var folder = Path.Combine(Application.dataPath, "_Project/Resources/Buildings");
            foreach (var b in Directory.GetDirectories(folder)) Assert.IsTrue(ids.Contains(Path.GetFileName(b)), Path.GetFileName(b) + " is not credited");
        }

        // The project's credit list (Docs/ASSET_INVENTORY/CREDITS.md): every author whose work is in the game.
        [Test]
        public void EveryAuthorTheProjectCreditsIsListed()
        {
            var by = List.Select(e => e.Title + " " + e.By).ToArray();
            int n = 0;
            foreach (var c in Rows(Path.Combine(Project, "Docs/ASSET_INVENTORY/CREDITS.md"), "## Credit asked for by the author"))
            {
                if (c.Length < 4 || c[0] == "Asset" || c[1].StartsWith("many") || c[3].Contains("no longer")) continue;
                n++;
                string author = c[1].Split(' ')[0];
                Assert.IsTrue(by.Any(x => x.Contains(author)), author + " (" + c[0] + ") is not on the credits screen");
            }
            Assert.GreaterOrEqual(n, 6);
        }

        [Test]
        public void EveryLicenceIsExplained()
        {
            var list = List;
            var explained = new HashSet<string>(list.Where(e => e.Section == "LICENCES").Select(e => e.Title));
            foreach (var e in list)
            {
                if (e.Section == "LICENCES" || e.Licence.Length == 0 || e.Licence.EndsWith(" terms")) continue;
                Assert.IsTrue(explained.Contains(e.Licence), e.Title + ": " + e.Licence + " has no line under LICENCES");
            }
            foreach (var e in list.Where(e => e.Section == "LICENCES")) Assert.IsNotEmpty(e.Source, e.Title + " has no link");
        }
    }
}
