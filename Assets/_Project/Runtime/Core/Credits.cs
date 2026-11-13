// Who made what the game is built with (owner, 2026-11-13: an in-game credits screen for the CC-BY parts). The list is Resources/Credits.txt, one line a work:
// title | by | licence | source | what it is in the game and what was changed | id. "## " starts a heading, "#" a note that is not shown. The start screen's Credits
// page shows each work's title with its maker and licence under it; choosing one shows its source and what was changed. CreditsTests checks the list against the
// project's credit records (Docs/ASSET_INVENTORY), the licensed models in Resources and the downloaded buildings.
using System.Collections.Generic;
using UnityEngine;

namespace Dad.Core
{
    public static class Credits
    {
        public sealed class Entry
        {
            /// <summary>The heading it is listed under; empty before the first.</summary>
            public string Section = "";
            public string Title = "", By = "", Licence = "", Source = "", Note = "", Id = "";

            /// <summary>The line under the title: the maker, and the licence when there is one.</summary>
            public string Under => string.IsNullOrEmpty(Licence) ? By : By + "   ·   " + Licence;

            /// <summary>What the help line says when the work is chosen: the source on a line of its own (nothing after it, so it copies whole), then the note.</summary>
            public string About
            {
                get
                {
                    if (string.IsNullOrEmpty(Source)) return Note;
                    return string.IsNullOrEmpty(Note) ? Source : Source + "\n" + Note;
                }
            }
        }

        /// <summary>The list as the game ships it.</summary>
        public static List<Entry> Load()
        {
            var text = Resources.Load<TextAsset>("Credits");
            return text != null ? Read(text.text) : new List<Entry>();
        }

        public static List<Entry> Read(string text)
        {
            var list = new List<Entry>();
            string section = "";
            foreach (var raw in text.Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0) continue;
                if (line.StartsWith("## ")) { section = line.Substring(3).Trim(); continue; }
                if (line.StartsWith("#")) continue;
                var f = line.Split('|');
                string F(int i) => i < f.Length ? f[i].Trim() : "";
                list.Add(new Entry { Section = section, Title = F(0), By = F(1), Licence = F(2), Source = F(3), Note = F(4), Id = F(5) });
            }
            return list;
        }

        /// <summary>The headings in the order they come.</summary>
        public static List<string> Sections(List<Entry> list)
        {
            var s = new List<string>();
            foreach (var e in list) if (e.Section.Length > 0 && !s.Contains(e.Section)) s.Add(e.Section);
            return s;
        }
    }
}
