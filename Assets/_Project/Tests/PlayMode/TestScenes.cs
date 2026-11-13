// After-test clean-up shared by the PlayMode fixtures. Every scene a test loaded or created is
// unloaded even when the test failed half-way, so no FloatingOrigin or Boot scene is left behind
// for the next test. (A failed streaming test once failed four later tests that way.) Scenes that
// were loaded before the test, such as the test runner's own, are never unloaded: unloading the
// runner's scene from a tear-down hangs the run.
using System.Collections;
using System.Collections.Generic;
using Dad.Core;
using UnityEngine.SceneManagement;

namespace Dad.Tests
{
    /// <summary>The frozen fixture: the streaming and builder tests' expected counts were measured on it, and it holds the buildings near kits and facades are built from. Clean() puts the city back.</summary>
    static class OldWorld
    {
        public static void Use() => GameRoot.WorldDirForTests = System.IO.Path.GetFullPath(System.IO.Path.Combine(UnityEngine.Application.dataPath, "..", "Tests", "Fixtures", "OldWorld"));
        public static void Limit(ChunkStreamer s)
        {
            s.SliceOnly = true;
            s.Slice = new UnityEngine.RectInt(6, 27, 4, 3);
        }
    }

    /// <summary>Tests that load Boot are about the world and its traffic: nobody walks in on them. PeoplePlayTests stands someone where it needs them.</summary>
    [NUnit.Framework.SetUpFixture]
    public class NoAutomaticPlayer
    {
        [NUnit.Framework.OneTimeSetUp] public void Off() { Player.Automatic = false; Turf.Automatic = false; Jobs.Automatic = false; }
        [NUnit.Framework.OneTimeTearDown] public void On() { Player.Automatic = true; Turf.Automatic = true; Jobs.Automatic = true; }
    }

    static class TestScenes
    {
        static readonly HashSet<int> before = new HashSet<int>();

        /// <summary>Remembers the scenes loaded before a test ([SetUp]).</summary>
        public static void Mark()
        {
            before.Clear();
            for (int i = 0; i < SceneManager.sceneCount; i++) before.Add(SceneManager.GetSceneAt(i).handle);
        }

        /// <summary>Unloads every scene loaded since Mark ([UnityTearDown]). If no earlier scene is
        /// left (a test loaded Boot on its own), a new empty scene stays active.</summary>
        public static IEnumerator Clean()
        {
            GameRoot.WorldDirForTests = null;
            var added = new List<Scene>();
            Scene keep = default;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (!s.isLoaded) continue;
                if (before.Contains(s.handle)) keep = s;
                else added.Add(s);
            }
            if (added.Count == 0) yield break;
            if (!keep.IsValid()) keep = SceneManager.CreateScene("AfterTest" + System.Guid.NewGuid().ToString("N"));
            SceneManager.SetActiveScene(keep);
            foreach (var s in added) yield return SceneManager.UnloadSceneAsync(s);
        }
    }
}
