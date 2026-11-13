// What a player has done, kept between runs (first playable, 2026-11-13): cash and deliveries, and who they chose to be (Cast). Kept like the settings (GameSettings): in the player's own folder
// (progress.json in the persistent data path), with a version number, written whole through a new file, the old one kept as a backup (progress.json.bak). A file that
// cannot be read falls back to the backup and then to a fresh start, and Note says which. Batch runs (tests, benches) start fresh and write nothing unless a test points
// Folder at its own folder.
using System;
using System.IO;
using UnityEngine;

namespace Dad.Core
{
    [Serializable]
    public sealed class Progress
    {
        public const int Format = 1;

        public int Version = Format;
        /// <summary>Taka in hand; never below zero.</summary>
        public int Cash;
        /// <summary>Taka earned in all, and deliveries made and lost.</summary>
        public int Earned, Deliveries, Failed;
        /// <summary>Which of the twenty you play (Cast); empty or unknown is the first.</summary>
        public string Character = "";

        public static Progress Current { get; private set; } = new Progress();
        /// <summary>What happened when the file was read: empty when it was read as it was, or there was none.</summary>
        public static string Note { get; private set; } = "";
        /// <summary>Where the file is kept. Tests point it elsewhere; null in a batch run means nothing is read or written.</summary>
        public static string Folder;
        /// <summary>Raised when the cash changes, with the change.</summary>
        public static event Action<int> Changed;

        static bool Kept => Folder != null || !Application.isBatchMode;
        public static string FilePath => Path.Combine(Folder ?? Application.persistentDataPath, "progress.json");
        public static string BackupPath => FilePath + ".bak";

        /// <summary>Reads the file, or the backup, or starts fresh.</summary>
        public static Progress Load()
        {
            Note = "";
            Progress p = null;
            if (Kept)
            {
                p = Read(FilePath);
                if (p == null && File.Exists(FilePath)) { p = Read(BackupPath); Note = p != null ? "progress file unreadable: the backup was used" : "progress file unreadable: a fresh start"; }
            }
            Current = p ?? new Progress();
            Current.Clamp();
            return Current;
        }

        /// <summary>A fresh start, kept as such (the file is written).</summary>
        public static void Reset()
        {
            Current = new Progress();
            Save();
            Changed?.Invoke(0);
        }

        static Progress Read(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                var p = JsonUtility.FromJson<Progress>(File.ReadAllText(path));
                return p != null && p.Version >= 1 && p.Version <= Format ? p : null;
            }
            catch (Exception e) when (e is IOException || e is ArgumentException || e is UnauthorizedAccessException) { return null; }
        }

        /// <summary>Writes the file whole, the old one becoming the backup.</summary>
        public static void Save()
        {
            if (!Kept) return;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                string fresh = FilePath + ".new";
                File.WriteAllText(fresh, JsonUtility.ToJson(Current, true));
                if (File.Exists(FilePath)) File.Copy(FilePath, BackupPath, true);
                if (File.Exists(FilePath)) File.Delete(FilePath);
                File.Move(fresh, FilePath);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { Debug.LogWarning("[Dad] progress not saved: " + e.Message); }
        }

        public static void Earn(int taka)
        {
            if (taka <= 0) return;
            Current.Cash += taka;
            Current.Earned += taka;
            Changed?.Invoke(taka);
        }

        /// <summary>Takes what there is, up to taka; returns what was taken.</summary>
        public static int Spend(int taka)
        {
            int took = Mathf.Clamp(taka, 0, Current.Cash);
            Current.Cash -= took;
            if (took > 0) Changed?.Invoke(-took);
            return took;
        }

        void Clamp()
        {
            Version = Format;
            Cash = Mathf.Max(0, Cash);
            Earned = Mathf.Max(0, Earned);
            Deliveries = Mathf.Max(0, Deliveries);
            Failed = Mathf.Max(0, Failed);
            if (!Cast.Has(Character)) Character = "";
        }
    }
}
