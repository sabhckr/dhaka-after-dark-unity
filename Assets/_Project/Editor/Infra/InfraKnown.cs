// The known infrastructure violations (Tests/Infra/known-violations.csv): every violation the survey
// (InfraSurvey) finds today, each with the issue or decision it belongs to. The EditMode test fails on a
// violation that is not listed, and on a listed one that is gone, so the list always matches the world:
// a fix removes its rows, and nothing new can come in unseen. Rows: key, reference, note; '#' starts a comment.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Dad.EditorTools
{
    public static class InfraKnown
    {
        public sealed class Entry
        {
            public string Key, Reference, Note;
        }

        public static Dictionary<string, Entry> Read(string path)
        {
            var o = new Dictionary<string, Entry>(StringComparer.Ordinal);
            bool header = true;
            foreach (var line in File.ReadAllLines(path))
            {
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal)) continue;
                var f = Split(line);
                if (header) { header = false; if (f[0] == "key") continue; }
                if (f.Count < 2 || f[1].Trim().Length == 0) throw new FormatException($"{path}: '{line}' needs a key and a reference");
                if (o.ContainsKey(f[0])) throw new FormatException($"{path}: {f[0]} is listed twice");
                o[f[0]] = new Entry { Key = f[0], Reference = f[1], Note = f.Count > 2 ? f[2] : "" };
            }
            return o;
        }

        /// <summary>The survey against the list: violations not listed (new) and listed keys the survey no longer finds (gone).</summary>
        public static (List<InfraSurvey.Finding> fresh, List<string> gone) Compare(InfraSurvey.Result res, Dictionary<string, Entry> list)
        {
            var v = res.Findings.Where(f => !f.Note).ToList();
            var keys = new HashSet<string>(v.Select(f => f.Key), StringComparer.Ordinal);
            return (v.Where(f => !list.ContainsKey(f.Key)).ToList(), list.Keys.Where(k => !keys.Contains(k)).OrderBy(k => k, StringComparer.Ordinal).ToList());
        }

        /// <summary>One CSV line: commas, with double quotes around fields that hold them.</summary>
        static List<string> Split(string line)
        {
            var o = new List<string>();
            var sb = new StringBuilder();
            bool quoted = false;
            for (int i = 0; i < line.Length; i++)
            {
                char ch = line[i];
                if (quoted)
                {
                    if (ch == '"' && i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                    else if (ch == '"') quoted = false;
                    else sb.Append(ch);
                }
                else if (ch == '"') quoted = true;
                else if (ch == ',') { o.Add(sb.ToString()); sb.Clear(); }
                else sb.Append(ch);
            }
            o.Add(sb.ToString());
            return o;
        }
    }
}
