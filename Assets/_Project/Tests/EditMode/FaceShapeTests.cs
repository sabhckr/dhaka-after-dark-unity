// The twenty Blender people's faces (2026-11-12): the body carries the 52 ARKit expression shapes and the 15 speech shapes, carried onto the brows and lashes,
// and none of the body-building shapes MakeHuman leaves behind ("$md-..."), which only weigh the file. PersonLife blinks and speaks with them under its own names.
using System.Linq;
using Dad.Core;
using NUnit.Framework;
using UnityEngine;

namespace Dad.Tests
{
    public class FaceShapeTests
    {
        static readonly string[] Needed = { "eyeBlinkLeft", "eyeBlinkRight", "jawOpen", "mouthSmileLeft", "browInnerUp", "viseme_aa", "viseme_O", "viseme_PP", "viseme_FF" };

        static string[] Shapes(SkinnedMeshRenderer r) => Enumerable.Range(0, r.sharedMesh.blendShapeCount).Select(i => r.sharedMesh.GetBlendShapeName(i)).Select(n => n.Substring(n.LastIndexOf('.') + 1)).ToArray();

        [Test]
        public void EveryFaceCanBlinkSmileAndSpeak()
        {
            foreach (var who in Enumerable.Range(1, 10).Select(i => $"npc_m{i:00}").Concat(Enumerable.Range(1, 10).Select(i => $"npc_f{i:00}")))
            {
                var model = Resources.Load<GameObject>(Person.Folder + who);
                Assert.IsNotNull(model, who);
                var renderers = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                var body = renderers.FirstOrDefault(r => r.name == "body");
                Assert.IsNotNull(body, who + " has no body mesh");
                var shapes = Shapes(body);
                foreach (var n in Needed) Assert.Contains(n, shapes, $"{who}'s face has no {n}");
                Assert.GreaterOrEqual(shapes.Length, 60, who);
                Assert.IsFalse(shapes.Any(n => n.StartsWith("$md")), $"{who} still carries MakeHuman's body-building shapes");
                var brows = renderers.FirstOrDefault(r => r.name.Contains("eyebrows"));
                Assert.IsNotNull(brows, who + " has no brows");
                Assert.Contains("browInnerUp", Shapes(brows), $"{who}'s brows do not follow the face");
                var lashes = renderers.FirstOrDefault(r => r.name.Contains("eyelashes"));
                Assert.IsNotNull(lashes, who + " has no lashes");
                Assert.Contains("eyeBlinkLeft", Shapes(lashes), $"{who}'s lashes do not close with the eye");
            }
        }
    }
}
