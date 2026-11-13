// The shooter's clip library (2026-11-12): every clip in People/Clips/Library comes in humanoid, the walking, running and crouching ones loop, and the hands move:
// the character's avatar maps all fifteen finger bones a hand, and the clips carry finger curves, so a grip on a rifle is a grip, not a flat hand.
using System.IO;
using System.Linq;
using Dad.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Dad.Tests
{
    public class ClipLibraryTests
    {
        const string Library = "Assets/_Project/Resources/People/Clips/Library";

        static string[] Files() => Directory.GetFiles(Library, "*.fbx").Select(f => f.Replace('\\', '/')).OrderBy(f => f).ToArray();

        [Test]
        public void TheLibraryHoldsAroundAHundredClips()
        {
            int n = Files().Length;
            Assert.That(n, Is.InRange(80, 110), "the owner asked for about a hundred, not the whole catalogue");
        }

        [Test]
        public void TheCharacterMapsEveryFingerBone()
        {
            var model = (ModelImporter)AssetImporter.GetAtPath("Assets/_Project/Resources/" + Person.Folder + OnFoot.Who + ".fbx");
            Assert.IsNotNull(model, OnFoot.Who);
            Assert.AreEqual(ModelImporterAnimationType.Human, model.animationType);
            var mapped = model.humanDescription.human.Select(h => h.humanName).ToHashSet();
            string[] fingers = { "Thumb", "Index", "Middle", "Ring", "Little" };
            string[] joints = { "Proximal", "Intermediate", "Distal" };
            var missing = (from side in new[] { "Left", "Right" } from f in fingers from j in joints let name = $"{side} {f} {j}" where !mapped.Contains(name) select name).ToList();
            Assert.IsEmpty(missing, "finger bones the avatar does not map");
        }

        [Test]
        public void EveryClipIsHumanoidAndTheGaitsLoop()
        {
            var flat = new System.Collections.Generic.List<string>();
            var notHuman = new System.Collections.Generic.List<string>();
            foreach (var f in Files())
            {
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(f);
                string name = Path.GetFileNameWithoutExtension(f);
                Assert.IsNotNull(clip, name);
                Assert.AreEqual(name, clip.name);
                if (!clip.humanMotion) notHuman.Add(name);
                var curves = AnimationUtility.GetCurveBindings(clip).Select(b => b.propertyName);
                if (!curves.Any(p => p.Contains("Thumb") || p.Contains("Index"))) flat.Add(name);
                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                bool gait = name.Contains("walk") || name.Contains("run-left") || name.Contains("run-right") || name.Contains("sprint") || name.EndsWith("-idle") || name.Contains("sneak");
                if (gait && !name.Contains("stop") && !name.Contains("turn") && !name.Contains("start")) Assert.IsTrue(settings.loopTime, name + " should loop");
            }
            Assert.IsEmpty(notHuman, "clips that did not come in humanoid");
            Assert.IsEmpty(flat, "clips with no finger motion");
        }

        [Test]
        public void ALibraryClipLoadsByItsName()
        {
            Assert.IsNotNull(Resources.Load<AnimationClip>(Person.ClipFolder + "Library/rifle-idle"));
        }
    }
}
