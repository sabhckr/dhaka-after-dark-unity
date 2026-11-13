// The player's settings (2026-11-10, Docs/PRODUCTION/PLAYER_INTERFACE.md). One file in the player's own folder, JSON with a version number; the one before it is kept as a
// backup. A file that cannot be read falls back to the backup and then to the defaults, and Note says what happened. Quality is not kept here: GraphicsTier keeps it.
// What a setting changes is read where it acts (cameras read the field of view and look speeds, sounds their volume), so a change shows at once.
using System;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Dad.Core
{
    [Serializable]
    public sealed class GameSettings
    {
        public const int Format = 1;
        public int Version = Format;

        // Display. A width of zero keeps the screen as it is.
        public int Width, Height;
        public FullScreenMode Mode = FullScreenMode.FullScreenWindow;
        public bool VSync = true;
        /// <summary>Frames a second at most; zero for no limit.</summary>
        public int FrameLimit;

        // Graphics
        public float FieldOfView = 60;

        // Audio, 0 to 1
        public float Master = 1, Vehicles = 0.8f, World = 0.8f, Voices = 1;

        // Controls
        public float MouseLook = 1, PadLook = 1, AimLook = 0.6f;
        public bool InvertLook, Vibration = true, AimToggle, SprintToggle;
        /// <summary>The player's own bindings, as the Input System writes them.</summary>
        public string Bindings = "";

        // Gameplay
        public bool Miles, AimAssist = true;

        // Interface
        public bool Hud = true, Radar = true, NorthUp, Crosshair = true, Prompts = true, Notices = true;
        public float HudSize = 1;

        public static GameSettings Current { get; private set; } = new GameSettings();
        /// <summary>Raised after a change is applied.</summary>
        public static event Action Changed;
        /// <summary>What happened when the file was read: empty when it was read as it was.</summary>
        public static string Note { get; private set; } = "";
        /// <summary>Where the file is kept. Tests point it elsewhere.</summary>
        public static string Folder;

        public static string FilePath => Path.Combine(Folder ?? Application.persistentDataPath, "settings.json");
        public static string BackupPath => FilePath + ".bak";

        public GameSettings Copy() => JsonUtility.FromJson<GameSettings>(JsonUtility.ToJson(this));

        /// <summary>Reads the file, or the backup, or starts from the defaults.</summary>
        public static GameSettings Load()
        {
            Note = "";
            var s = Read(FilePath);
            if (s == null && File.Exists(FilePath)) { s = Read(BackupPath); Note = s != null ? "settings file unreadable: the backup was used" : "settings file unreadable: defaults used"; }
            Current = s ?? new GameSettings();
            Current.Clamp();
            return Current;
        }

        static GameSettings Read(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                var s = JsonUtility.FromJson<GameSettings>(File.ReadAllText(path));
                return s != null && s.Version >= 1 && s.Version <= Format ? s : null;
            }
            catch (Exception e) when (e is IOException || e is ArgumentException || e is UnauthorizedAccessException) { return null; }
        }

        /// <summary>Writes the file whole, the old one becoming the backup.</summary>
        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                string fresh = FilePath + ".new";
                File.WriteAllText(fresh, JsonUtility.ToJson(this, true));
                if (File.Exists(FilePath)) File.Copy(FilePath, BackupPath, true);
                if (File.Exists(FilePath)) File.Delete(FilePath);
                File.Move(fresh, FilePath);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { Debug.LogWarning("[Dad] settings not saved: " + e.Message); }
        }

        void Clamp()
        {
            Version = Format;
            FieldOfView = Mathf.Clamp(FieldOfView, 45, 90);
            Master = Mathf.Clamp01(Master); Vehicles = Mathf.Clamp01(Vehicles); World = Mathf.Clamp01(World); Voices = Mathf.Clamp01(Voices);
            MouseLook = Mathf.Clamp(MouseLook, 0.1f, 3); PadLook = Mathf.Clamp(PadLook, 0.1f, 3); AimLook = Mathf.Clamp(AimLook, 0.1f, 2);
            HudSize = Mathf.Clamp(HudSize, 0.75f, 1.25f);
            FrameLimit = FrameLimit <= 0 ? 0 : Mathf.Clamp(FrameLimit, 30, 240);
            Bindings ??= "";
        }

        /// <summary>Makes these the settings in use, applies what is not read where it acts, and tells everyone listening.</summary>
        public static void Use(GameSettings s, GameInput input)
        {
            s.Clamp();
            Current = s;
            if (s.Width > 0 && s.Height > 0 && !Application.isBatchMode && (Screen.width != s.Width || Screen.height != s.Height || Screen.fullScreenMode != s.Mode))
                Screen.SetResolution(s.Width, s.Height, s.Mode);
            else if (!Application.isBatchMode && Screen.fullScreenMode != s.Mode) Screen.fullScreenMode = s.Mode;
            QualitySettings.vSyncCount = s.VSync ? 1 : 0;
            Application.targetFrameRate = s.FrameLimit > 0 ? s.FrameLimit : -1;
            AudioListener.volume = s.Master;
            if (input != null)
            {
                input.Asset.RemoveAllBindingOverrides();
                if (!string.IsNullOrEmpty(s.Bindings)) input.LoadBindingOverrides(s.Bindings);
            }
            Changed?.Invoke();
        }
    }
}
