// How the recorded sounds come in (2026-11-12): the gunshots (The Free Firearm Sound Library, CC0) and the impacts (Kenney's Impact Sounds, CC0) in Resources/Audio. Each is
// short and played at once, many at a time, so it is held in memory decoded, at the rate it was recorded: nothing is thinned or squeezed.
using System;
using UnityEditor;
using UnityEngine;

namespace Dad.EditorTools
{
    public sealed class AudioImport : AssetPostprocessor
    {
        const string Audio = "Assets/_Project/Resources/Audio/";

        void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(Audio, StringComparison.Ordinal)) return;
            var a = (AudioImporter)assetImporter;
            a.forceToMono = false;
            a.loadInBackground = false;
            var s = a.defaultSampleSettings;
            s.loadType = AudioClipLoadType.DecompressOnLoad;
            s.compressionFormat = AudioCompressionFormat.PCM;
            s.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            s.preloadAudioData = true;
            a.defaultSampleSettings = s;
        }
    }
}
