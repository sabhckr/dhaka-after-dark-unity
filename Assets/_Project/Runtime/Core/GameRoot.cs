// Boot-scene root. For now it proves the world data is reachable in a player build:
// it reads world.json and decodes one chunk on a worker thread, then logs the result.
// It owns the player input (GameInput), the debug views (DebugViews, F1-F7) and the time of day
// (DayCycle, D-03, made on the scene's directional light). In plain play, with no bench or shot on
// the command line, it starts the free camera over Farmgate, because there is no protagonist until
// Phase E, and sets the clock running. Later it creates the clock, event bus and module order
// (Docs/ARCHITECTURE/UNITY_ARCHITECTURE.md).
using System.IO;
using System.Threading.Tasks;
using Dad.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Dad.Core
{
    public sealed class GameRoot : MonoBehaviour
    {
        public const string SmokeChunk = "c_5_-5"; // Central Commercial Hub

        public static string WorldDir => WorldDirForTests ?? Path.Combine(Application.streamingAssetsPath, "World");

        /// <summary>Set by the streaming tests, whose counts were measured on the frozen fixture. Never set in the game.</summary>
        public static string WorldDirForTests;

        /// <summary>Set when the boot check finishes: "ok ..." or "error ...".</summary>
        public string BootStatus { get; private set; }

        public GameInput Controls { get; private set; }

        public DayCycle Day { get; private set; }

        // Held from start-up (2026-11-07, PlayMode tests): in the editor a single-scene load unloads assets nothing refers to, and an InputTestFixture (FreeCameraTests,
        // GameInputTests) leaves InputSystem.actions unset when it tears down, so a later GameRoot found none. Awake falls back on the held ones (the property cannot be set in play).
        // A player has neither problem.
        static InputActionAsset projectActions;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void HoldActions() => projectActions = InputSystem.actions;

        void Awake()
        {
            var actions = InputSystem.actions != null ? InputSystem.actions : projectActions;
            if (actions == null) Debug.LogError("[Dad] no project-wide input actions: Project Settings > Input System Package");
            else Controls = new GameInput(actions);
            Light sun = null;
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.InstanceID))
                if (l.type == LightType.Directional) { sun = l; break; }
            Day = DayCycle.Create(sun);
            GraphicsTier.Apply(GraphicsTier.Choose(System.Environment.GetCommandLineArgs()), Camera.main);
            Grade.Create(Day);
            var bench = BenchmarkRunner.FromCommandLine(gameObject); // only with -dad-bench
            var shot = ViewShot.FromCommandLine(gameObject); // only with -dad-shot
            var streamer = FindAnyObjectByType<ChunkStreamer>();
            StreetLamps.Create(Day, streamer, Camera.main != null ? Camera.main.transform : null).Instant = shot != null;
            TrialBuilding.FromCommandLine(gameObject); // only with -dad-building, before the city draws
            CityView.Attach(gameObject, streamer, Camera.main != null ? Camera.main.transform : null);
            WaterView.Attach(gameObject, streamer);
            TrafficView traffic = null;
            if (bench == null && shot == null) traffic = TrafficView.Attach(gameObject, streamer, Day);
            else if (bench != null) traffic = TrafficView.FromCommandLine(gameObject, streamer, Day); // bench runs ask for it: -dad-traffic
            if (bench == null && shot == null || bench != null && System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-dad-npcs") >= 0) NpcCrowd.Attach(gameObject, streamer, Camera.main != null ? Camera.main.transform : null);
            if (bench == null && shot == null && Turf.Automatic) Turf.Attach(gameObject, streamer); // the gang on their turf (2026-11-12)
            var transport = TransportFile.Load();
            var parked = ParkedView.Attach(gameObject, transport, Day);
            var train = TrainView.Attach(gameObject, transport, traffic);
            if (train != null)
            {
                train.Manual = shot != null;
                var noise = new GameObject("Train sound").AddComponent<TrainSound>();
                noise.transform.SetParent(transform, false);
                noise.Train = train;
            }
            // Colliders and the test drive come with plain play; a bench asks for colliders with -dad-colliders, a view with its spec.
            if (bench == null && shot == null) ChunkStreamer.BuildColliders = true;
            else if (bench != null && System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-dad-colliders") >= 0) ChunkStreamer.BuildColliders = true;
            if (bench != null && traffic != null && System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-dad-drive") >= 0)
            {
                var drive = gameObject.AddComponent<BenchDrive>();
                drive.Traffic = traffic;
                drive.Day = Day;
            }
            if (Controls == null) return;
            var views = gameObject.AddComponent<DebugViews>();
            views.Input = Controls;
            views.Streamer = streamer;
            views.Day = Day;
            if (bench == null && shot == null)
            {
                StartFreeCamera();
                Day.MinutesPerSecond = DayCycle.PlayMinutesPerSecond;
                var test = gameObject.AddComponent<TestDrive>();
                test.Input = Controls;
                test.Traffic = traffic;
                test.Day = Day;
                var you = gameObject.AddComponent<Player>();
                you.Input = Controls;
                you.Traffic = traffic;
                you.Day = Day;
                you.Drive = test;
                you.Yard = parked;
                you.Train = train;
                // The player interface (2026-11-10): your settings, the police, the city map, the HUD, the menus and the weapon wheel.
                // Batch runs (tests) keep the defaults rather than read whatever settings file this machine has, unless a test points Folder at its own.
                if (!Application.isBatchMode || GameSettings.Folder != null) GameSettings.Use(GameSettings.Load(), Controls);
                // What you have done so far (first playable, 2026-11-13): cash and deliveries, read the same way (Progress).
                Progress.Load();
                if (Progress.Note.Length > 0) Debug.LogWarning("[Dad] " + Progress.Note);
                Cast.Read(Progress.Current); // who you chose to be last time
                var police = Precinct.Attach(gameObject);
                MapBuilding = BuildMap();
                Hud.Attach(gameObject, Controls, you, test, Day, police);
                Menus.Attach(gameObject, Controls, you, police);
                WeaponWheel.Attach(gameObject, Controls, you);
                Jobs.Attach(gameObject, you);
            }
        }

        /// <summary>The city map being drawn for this run, null before plain play starts (tests wait on it).</summary>
        public static System.Threading.Tasks.Task MapBuilding { get; private set; }

        // The map is drawn on a worker while the start screen is up; the radar is empty until it is done.
        static async System.Threading.Tasks.Task BuildMap()
        {
            try
            {
                var map = await CityMap.Build(WorldDir);
                Debug.Log(map != null ? $"[Dad] city map {map.Width}x{map.Height}, {map.Nodes} road nodes, {map.Areas.Count} districts" : "[Dad] city map: no chunks");
            }
            catch (System.Exception e) { Debug.LogError("[Dad] city map not built: " + e); }
        }

        void StartFreeCamera()
        {
            var cam = Camera.main;
            if (cam == null) return;
            var fly = cam.gameObject.AddComponent<FreeCamera>();
            fly.Input = Controls;
            var origin = FindAnyObjectByType<FloatingOrigin>();
            var offset = new OriginOffset();
            if (origin != null)
            {
                origin.Recenter(FreeCamera.StartEast, FreeCamera.StartNorth);
                offset = origin.Offset;
            }
            fly.Place(offset, FreeCamera.StartEast, FreeCamera.StartNorth, FreeCamera.StartHeight, FreeCamera.LookEast, FreeCamera.LookNorth, 0f);
            Controls.Switch(InputContext.FreeCamera);
        }

        void OnDestroy()
        {
            Controls?.Dispose();
            ChunkStreamer.BuildColliders = false;
        }

        async void Start()
        {
            BootStatus = await Task.Run(BootCheck);
            if (BootStatus.StartsWith("ok")) Debug.Log("[Dad] " + BootStatus);
            else Debug.LogError("[Dad] " + BootStatus);
        }

        static string BootCheck()
        {
            var world = Path.Combine(WorldDir, "world.json");
            if (!File.Exists(world)) return "error world.json missing: run Tools/worldgen/build.sh";
            try
            {
                var chunk = Dadc.Load(File.ReadAllBytes(Path.Combine(WorldDir, "chunks", SmokeChunk + ".dadc")));
                return $"ok {chunk.Id}: {chunk.Buildings.Length} buildings, {chunk.Roads.Length} roads, {chunk.Nodes.Length} graph nodes";
            }
            catch (System.Exception e) when (e is IOException || e is DadcFormatException)
            {
                return "error " + e.Message;
            }
        }
    }
}
