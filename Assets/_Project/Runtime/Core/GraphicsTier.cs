// Low, Medium and High (owner's brief, section 13.7). Each is a quality level with a URP asset of its own: Assets/Settings/{Low,Medium,High}_RPAsset. This class picks
// one at start and sets what lives on the camera. Low puts a steady frame rate first (no ambient occlusion, no bloom, near shadows only); High is what the goldens and
// benches have always measured, and is never chosen for you.
//
// -dad-quality low|medium|high on the command line wins; a shot or bench without it runs High, so that their numbers stay comparable; otherwise what the player last
// chose, and failing that Low on integrated graphics and Medium elsewhere.
using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Dad.Core
{
    public enum Tier { Low, Medium, High }

    public static class GraphicsTier
    {
        public const string Argument = "-dad-quality", SavedKey = "dad.quality";

        public static Tier Current { get; private set; } = Tier.High;

        public static bool TryParse(string name, out Tier tier) => Enum.TryParse(name, true, out tier) && Enum.IsDefined(typeof(Tier), tier);

        /// <summary>What the hardware should start on, from the graphics device's name and memory in MB.</summary>
        public static Tier ForHardware(string device, int memoryMb)
        {
            bool integrated = (device ?? "").IndexOf("intel", StringComparison.OrdinalIgnoreCase) >= 0 || (device ?? "").IndexOf("uhd", StringComparison.OrdinalIgnoreCase) >= 0 || memoryMb < 2048;
            return integrated ? Tier.Low : Tier.Medium;
        }

        public static Tier Choose(string[] args)
        {
            bool measuring = false;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == Argument && i + 1 < args.Length && TryParse(args[i + 1], out var asked)) return asked;
                if (args[i] == "-dad-shot" || args[i] == "-dad-bench") measuring = true;
            }
            if (measuring) return Tier.High;
            if (PlayerPrefs.HasKey(SavedKey) && TryParse(PlayerPrefs.GetString(SavedKey), out var saved)) return saved;
            return ForHardware(SystemInfo.graphicsDeviceName, SystemInfo.graphicsMemorySize);
        }

        /// <summary>Switches level and sets the camera's post-processing. Keep is for the player's own choice.</summary>
        public static void Apply(Tier tier, Camera camera, bool keep = false)
        {
            Current = tier;
            QualitySettings.SetQualityLevel((int)tier, true);
            if (keep) PlayerPrefs.SetString(SavedKey, tier.ToString());
            if (camera == null) return;
            var data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.dithering = true;
            // The owner's brief of 2026-11-06 locks SMAA on every level; only its quality steps down.
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = tier == Tier.High ? AntialiasingQuality.High : tier == Tier.Medium ? AntialiasingQuality.Medium : AntialiasingQuality.Low;
        }
    }
}
