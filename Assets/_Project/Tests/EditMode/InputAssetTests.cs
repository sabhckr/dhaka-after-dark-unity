// The project-wide input actions are DadInput, every action GameInput needs exists, and no
// control is bound to two actions that can be live at the same time.
using System.Collections.Generic;
using System.Linq;
using Dad.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Dad.Tests
{
    public class InputAssetTests
    {
        const string Path = "Assets/_Project/Input/DadInput.inputactions";

        static InputActionAsset ProjectWide()
        {
            Assert.IsTrue(EditorBuildSettings.TryGetConfigObject("com.unity.input.settings.actions", out InputActionAsset asset), "project-wide actions set");
            Assert.AreEqual(Path, AssetDatabase.GetAssetPath(asset));
            return asset;
        }

        [Test]
        public void ProjectWideActionsHaveEveryGameAction()
        {
            var copy = Object.Instantiate(ProjectWide());
            try
            {
                using var input = new GameInput(copy); // throws if a map or action is missing
                Assert.AreEqual(InputContext.OnFoot, input.Context);
            }
            finally { Object.DestroyImmediate(copy); }
        }

        [Test]
        public void NoControlDrivesTwoLiveActions()
        {
            var asset = ProjectWide();
            var contexts = new Dictionary<string, string[]>
            {
                ["OnFoot"] = new[] { "Global", "OnFoot" },
                ["Driving"] = new[] { "Global", "Driving" },
                ["FreeCamera"] = new[] { "Global", "FreeCamera" },
            };
            var clashes = new List<string>();
            foreach (var kv in contexts)
            {
                var byPath = new Dictionary<string, string>();
                foreach (var map in kv.Value.Select(m => asset.FindActionMap(m, true)))
                foreach (var b in map.bindings)
                {
                    if (b.isComposite) continue;
                    var who = $"{map.name}/{b.action}";
                    if (byPath.TryGetValue(b.path, out var other) && other != who) clashes.Add($"{kv.Key}: {b.path} -> {other} and {who}");
                    else byPath[b.path] = who;
                }
            }
            Assert.IsEmpty(clashes, string.Join("\n", clashes));
        }
    }
}
