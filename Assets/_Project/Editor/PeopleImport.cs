// How the owner's rigged people and clips come into Unity (E-01, 2026-11-06). Tools/models/people.py copies them to Resources/People; this sets them up as they import: every
// person and every clip is Humanoid, so one clip fits everyone, and the clips that repeat are marked to loop. Nothing here touches the world or the vehicles.
using System;
using System.IO;
using UnityEditor;

namespace Dad.EditorTools
{
    public sealed class PeopleImport : AssetPostprocessor
    {
        const string People = "Assets/_Project/Resources/People/", Clips = People + "Clips/";
        static readonly string[] Repeat = { "idle", "walk", "run", "turn-left", "turn-right", "drive", "sit", "passenger", "talk", "fight-idle", "text-stand", "text-walk", "bag-walk", "argue",
            "cower", "flee", "sprint", "phone", "smoke", "jog", "lean", "drink", "yell", "cover-idle", "pistol-kneel", "pistol-idle", "pistol-walk", "rifle-aim", "rifle-walk", "rifle-run", "crouch-idle", "crouch-walk", "crouch-aim", "fall",
            "walk-left", "walk-right", "walk-back", "run-left", "run-right", "run-back", "rifle-walk-left", "rifle-walk-right", "rifle-walk-back", "rifle-run-back", "pistol-walk-left",
            "pistol-walk-right", "pistol-walk-back" }; // walking and running every way (2026-11-12)
        static readonly string[] Door = { "enter-car", "exit-car" };
        // The shooter's library (Clips/Library, 2026-11-12): 87 Mixamo clips on the one character, rifle, pistol, cover, hits, deaths, fists and signs. These repeat; the rest play once.
        static readonly string[] LibraryRepeat = { "rifle-idle", "rifle-walk-fl", "rifle-walk-fr", "rifle-walk-bl", "rifle-walk-br", "rifle-run-left", "rifle-run-right", "rifle-sprint",
            "rifle-crouch-idle", "rifle-crouch-fwd", "rifle-crouch-back", "rifle-crouch-left", "rifle-crouch-right", "rifle-fire-walk", "rifle-fire-burst", "pistol-kneel-aim", "pistol-run",
            "pistol-ready", "cover-sneak-left", "cover-sneak-right", "injured-walk-back", "injured-run", "injured-idle", "crawl-back", "crouch-left", "crouch-right", "crouch-back", "look-around" };

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(People, StringComparison.Ordinal)) return;
            var m = (ModelImporter)assetImporter;
            bool clip = assetPath.StartsWith(Clips, StringComparison.Ordinal);
            m.animationType = ModelImporterAnimationType.Human;
            m.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            m.importAnimation = clip;
            m.importBlendShapes = !clip; // the face's shapes, where a model has them: blinks, the mouth's shapes for speech (PersonLife)
            m.importVisibility = false;
            m.importCameras = false;
            m.importLights = false;
            m.materialImportMode = clip ? ModelImporterMaterialImportMode.None : ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            if (clip) return;
            m.meshCompression = ModelImporterMeshCompression.Off; // owner, 2026-11-09: a model comes in with all it has
            m.isReadable = true; // PersonLife reads the hair's vertices to give it bones
            m.skinWeights = ModelImporterSkinWeights.Custom;
            m.maxBonesPerVertex = 8;
        }

        static readonly string[] Data = { "_opacity", "_cut", "_cuta", "_smoothness", "_roughness", "_metallic", "_orm", "_mask" }; // numbers, not colours

        static readonly string[] Travelling = { "text-walk", "bag-walk", "rifle-crouch-fwd" }; // Mixamo has no in-place copy of the crouched rifle walk

        // Each map is its own file beside the model (Person.Map), so the copies packed in the FBX must not be pulled into memory as well.
        void OnPostprocessMaterial(UnityEngine.Material material)
        {
            if (!assetPath.StartsWith(People, StringComparison.Ordinal)) return;
            foreach (var map in new[] { "_BaseMap", "_MainTex", "_BumpMap" }) if (material.HasProperty(map)) material.SetTexture(map, null);
        }

        void OnPreprocessAnimation()
        {
            if (!assetPath.StartsWith(Clips, StringComparison.Ordinal)) return;
            var m = (ModelImporter)assetImporter;
            string name = Path.GetFileNameWithoutExtension(assetPath);
            var clips = m.defaultClipAnimations;
            foreach (var c in clips)
            {
                c.name = name;
                bool library = assetPath.StartsWith(Clips + "Library/", StringComparison.Ordinal);
                c.loopTime = Array.IndexOf(library ? LibraryRepeat : Repeat, name) >= 0;
                // Root motion is baked into the pose on every clip, so the body's transform moves only when the controller moves it. Getting in and out keep their hips' travel from
                // the kerb to the seat inside the pose, which is what Player plays on a still root; they were made for a left-hand-drive car, so they are mirrored for the driver's door on the right.
                c.mirror = Array.IndexOf(Door, name) >= 0;
                c.lockRootRotation = true;
                c.keepOriginalOrientation = true;
                c.lockRootHeightY = true;
                c.keepOriginalPositionY = true;
                c.lockRootPositionXZ = Array.IndexOf(Travelling, name) < 0; // these two were downloaded walking forward: left unbaked, their travel goes to the root, and root motion is off
                c.keepOriginalPositionXZ = true;
            }
            m.clipAnimations = clips;
        }

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(People, StringComparison.Ordinal)) return;
            var t = (TextureImporter)assetImporter;
            t.textureCompression = TextureImporterCompression.CompressedHQ;
            t.mipmapEnabled = true;
            t.sRGBTexture = !Array.Exists(Data, kind => Path.GetFileNameWithoutExtension(assetPath).EndsWith(kind, StringComparison.Ordinal));
            t.maxTextureSize = 8192; // never smaller than the file
            t.ignoreMipmapLimit = true; // and no quality preset takes the top level away
            t.npotScale = TextureImporterNPOTScale.None; // nor is a picture stretched to a power of two
            string stem = Path.GetFileNameWithoutExtension(assetPath);
            bool directX = stem.EndsWith("_normaldx", StringComparison.Ordinal); // a map baked for DirectX has its green the other way up
            if (directX || stem.EndsWith("_normal", StringComparison.Ordinal))
            {
                t.textureType = TextureImporterType.NormalMap;
                t.sRGBTexture = false;
                t.flipGreenChannel = directX;
            }
        }
    }
}
