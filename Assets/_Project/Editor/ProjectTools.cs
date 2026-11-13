// Batch-mode entry points, run with -executeMethod:
//   Dad.EditorTools.ProjectTools.CreateBootScene   writes Assets/_Project/Scenes/Boot.unity (and the streaming materials)
//   Dad.EditorTools.ProjectTools.BuildWindows      Windows x64 Mono player into Builds/Windows
//   Dad.EditorTools.ProjectTools.BuildMacBench     macOS arm64 development player into Builds/MacBench (benchmarks)
//   Dad.EditorTools.ProjectTools.BuildMacPlay      macOS arm64 release player into Builds/MacPlay (the first playable, to play)
// Each writes a short JSON result to Evidence/local and exits non-zero on failure.
using System.IO;
using Dad.Core;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Dad.EditorTools
{
    public static class ProjectTools
    {
        public const string BootScene = "Assets/_Project/Scenes/Boot.unity";

        static string Evidence(string name)
        {
            var dir = Path.Combine(Directory.GetCurrentDirectory(), "Evidence", "local");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, name);
        }

        const string MaterialDir = "Assets/_Project/Resources/Materials";

        // Materials ChunkStreamer loads from Resources: ground surfaces (vertex colour), the unlit
        // debug lines, and the overlays drawn over everything (C-05: grid and ids, graph edges,
        // nodes, signals). ChunkGround (the C-01 plain plate) is no longer used.
        static void EnsureStreamingMaterials()
        {
            var vertexColour = Shader.Find("Dad/Vertex Color Unlit");
            if (vertexColour == null) throw new System.InvalidOperationException("Dad/Vertex Color Unlit shader not found");
            {
                var path = $"{MaterialDir}/ChunkSurfaces.mat";
                Directory.CreateDirectory(MaterialDir);
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) { mat = new Material(vertexColour); AssetDatabase.CreateAsset(mat, path); }
                mat.shader = vertexColour;
                mat.enableInstancing = false;
                EditorUtility.SetDirty(mat);
            }
            Directory.CreateDirectory(MaterialDir);
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) throw new System.InvalidOperationException("URP Unlit shader not found");
            foreach (var (name, colour) in new[]
            {
                ("ChunkGround", new Color(0.13f, 0.14f, 0.16f)),
                ("ChunkBuildings", new Color(0.96f, 0.72f, 0.38f)),
                ("ChunkRoads", new Color(0.62f, 0.78f, 0.95f)),
            })
            {
                var path = $"{MaterialDir}/{name}.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
                mat.shader = shader;
                mat.SetColor("_BaseColor", colour);
                EditorUtility.SetDirty(mat);
            }
            var overlay = Shader.Find("Dad/Debug Lines");
            if (overlay == null) throw new System.InvalidOperationException("Dad/Debug Lines shader not found");
            foreach (var (name, colour) in new[]
            {
                ("DebugGrid", new Color(1f, 0.86f, 0.2f)),
                ("DebugGraph", new Color(0.3f, 0.95f, 0.45f)),
                ("DebugNodes", new Color(0.95f, 0.95f, 0.95f)),
                ("DebugSignals", new Color(1f, 0.25f, 0.25f)),
            })
            {
                var path = $"{MaterialDir}/{name}.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) { mat = new Material(overlay); AssetDatabase.CreateAsset(mat, path); }
                mat.shader = overlay;
                mat.SetColor("_BaseColor", colour);
                EditorUtility.SetDirty(mat);
            }
            AssetDatabase.SaveAssets();
        }

        /// <summary>Creates or updates the streaming materials only (batch mode: -executeMethod).</summary>
        public static void CreateMaterials()
        {
            EnsureStreamingMaterials();
            EditorApplication.Exit(0);
        }

        public static void CreateBootScene()
        {
            EnsureStreamingMaterials();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cam = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)) { tag = "MainCamera" };
            cam.transform.SetPositionAndRotation(new Vector3(0, 30, -60), Quaternion.Euler(25, 0, 0));
            cam.GetComponent<Camera>().farClipPlane = 3000f; // golden views and bench paths see a whole slice
            var sun = new GameObject("Sun", typeof(Light));
            var light = sun.GetComponent<Light>();
            light.type = LightType.Directional;
            light.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(50, -30, 0);
            new GameObject("GameRoot", typeof(GameRoot));
            var origin = new GameObject("FloatingOrigin", typeof(FloatingOrigin)).GetComponent<FloatingOrigin>();
            origin.Target = cam.transform;
            var streamer = new GameObject("ChunkStreamer", typeof(ChunkStreamer)).GetComponent<ChunkStreamer>();
            streamer.Focus = cam.transform;
            streamer.Origin = origin;
            Directory.CreateDirectory(Path.GetDirectoryName(BootScene));
            bool saved = EditorSceneManager.SaveScene(scene, BootScene);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(BootScene, true) };
            File.WriteAllText(Evidence("create-boot-scene.json"), $"{{\"saved\":{saved.ToString().ToLowerInvariant()},\"scene\":\"{BootScene}\"}}\n");
            EditorApplication.Exit(saved ? 0 : 1);
        }

        public static void BuildWindows() =>
            Build("Builds/Windows/DhakaAfterDark.exe", BuildTarget.StandaloneWindows64, BuildOptions.None, "build-windows.json");

        // Development build: the profiler capture (-dad-bench-capture) needs it. Numbers from it run
        // slightly slower than a release build; the summary records which build it was.
        public static void BuildMacBench()
        {
            UnityEditor.OSXStandalone.UserBuildSettings.architecture = UnityEditor.Build.OSArchitecture.ARM64;
            Build("Builds/MacBench/DhakaAfterDark.app", BuildTarget.StandaloneOSX, BuildOptions.Development, "build-macbench.json");
        }

        // Release build of plain play: what you start to play the first playable on this Mac. The game's target stays Windows (K-02).
        public static void BuildMacPlay()
        {
            UnityEditor.OSXStandalone.UserBuildSettings.architecture = UnityEditor.Build.OSArchitecture.ARM64;
            Build("Builds/MacPlay/DhakaAfterDark.app", BuildTarget.StandaloneOSX, BuildOptions.None, "build-macplay.json");
        }

        static void Build(string location, BuildTarget target, BuildOptions options, string evidence)
        {
            EnsureStreamingMaterials(); // a build never ships without the materials the streamer loads
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { BootScene },
                locationPathName = location,
                target = target,
                options = options,
            });
            var s = report.summary;
            File.WriteAllText(Evidence(evidence),
                $"{{\"result\":\"{s.result}\",\"target\":\"{target}\",\"development\":{((options & BuildOptions.Development) != 0).ToString().ToLowerInvariant()},\"bytes\":{s.totalSize},\"seconds\":{s.totalTime.TotalSeconds:F1},\"errors\":{s.totalErrors},\"warnings\":{s.totalWarnings},\"backend\":\"Mono\"}}\n");
            EditorApplication.Exit(s.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
