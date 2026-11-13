// How the 90-second trailer's models and clips come in. Everything lives under Assets/Trailer so the game's own assets and art budget are untouched. Models arrive whole: no mesh
// compression, no texture smaller than its file. Clips are Mixamo moves downloaded without a skin, so each borrows the Humanoid avatar of a clip the game already uses.
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Dad.Trailer.EditorTools
{
    public sealed class TrailerImport : AssetPostprocessor
    {
        const string Root = "Assets/Trailer/", Models = Root + "Models/", Clips = Root + "Clips/";
        const string SourceAvatar = "Assets/_Project/Resources/People/Clips/walk.fbx";
        static readonly string[] Data = { "metallic", "metalness", "roughness", "smoothness", "_ao", "ambient", "opacity", "gloss", "height", "specular", "mixed_ao" };

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(Root, StringComparison.Ordinal)) return;
            var m = (ModelImporter)assetImporter;
            m.importCameras = false;
            m.importLights = false;
            m.importVisibility = false;
            m.importBlendShapes = false;
            if (assetPath.StartsWith(Clips, StringComparison.Ordinal))
            {
                m.animationType = ModelImporterAnimationType.Human;
                m.importAnimation = true;
                m.materialImportMode = ModelImporterMaterialImportMode.None;
                var source = AssetDatabase.LoadAssetAtPath<Avatar>(SourceAvatar);
                if (source != null)
                {
                    m.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                    m.sourceAvatar = source;
                }
                return;
            }
            m.meshCompression = ModelImporterMeshCompression.Off;
            m.isReadable = false;
            m.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            m.materialLocation = ModelImporterMaterialLocation.InPrefab;
            m.materialSearch = ModelImporterMaterialSearch.RecursiveUp;
            m.materialName = ModelImporterMaterialName.BasedOnMaterialName;
            m.importAnimation = assetPath.Contains("/fastback/");
            m.animationType = ModelImporterAnimationType.Generic;
            m.skinWeights = ModelImporterSkinWeights.Custom;
            m.maxBonesPerVertex = 8;
        }

        void OnPreprocessAnimation()
        {
            if (!assetPath.StartsWith(Clips, StringComparison.Ordinal)) return;
            var m = (ModelImporter)assetImporter;
            var clips = m.defaultClipAnimations;
            foreach (var c in clips)
            {
                c.name = Path.GetFileNameWithoutExtension(assetPath);
                c.lockRootRotation = true;
                c.keepOriginalOrientation = true;
                c.lockRootHeightY = true;
                c.keepOriginalPositionY = true;
                c.lockRootPositionXZ = true;
                c.keepOriginalPositionXZ = true;
            }
            m.clipAnimations = clips;
        }

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Models, StringComparison.Ordinal)) return;
            var t = (TextureImporter)assetImporter;
            string stem = Path.GetFileNameWithoutExtension(assetPath).ToLowerInvariant();
            t.maxTextureSize = 8192;
            t.ignoreMipmapLimit = true;
            t.npotScale = TextureImporterNPOTScale.None;
            t.mipmapEnabled = true;
            t.textureCompression = TextureImporterCompression.Compressed;
            t.sRGBTexture = !Array.Exists(Data, stem.Contains);
            if (stem.Contains("normal") || stem.EndsWith("_nrm", StringComparison.Ordinal) || stem.EndsWith("_n", StringComparison.Ordinal))
            {
                t.textureType = TextureImporterType.NormalMap;
                t.sRGBTexture = false;
            }
        }
    }
}
