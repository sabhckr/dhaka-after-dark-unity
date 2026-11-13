// Flies the main camera along a fixed path and records every frame, then writes
// summary.json, frames.csv and (with Capture) a profiler capture, capture.raw.
//
// Player command line (GameRoot adds the runner when -dad-bench is present):
//   -dad-bench [path]        path name in Resources/CameraPaths (default hub-rich-air)
//   -dad-bench-out <dir>     output folder (default persistentDataPath/bench-<time>)
//   -dad-bench-capture       also write a profiler capture of CaptureMaxFrames frames
//                            (development builds only; about 50 KB a frame in the player). 2,000 is
//                            the most the editor's Profiler keeps of a loaded capture
//   -dad-bench-capture-from <n>  start the capture at sampled frame n instead of the first
//   -dad-bench-no-vsync      vsync off and frame rate capped at 500 (default: vsync on, as players run it)
// The player quits when done: exit code 0 after writing the files, 1 if the run could not start.
// "complete" is false when the frame buffer filled before the path ended.
using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;

namespace Dad.Core
{
    public sealed class BenchmarkRunner : MonoBehaviour
    {
        public const string DefaultPath = "hub-rich-air";
        const int UncappedFrameRate = 500;

        public string PathName = DefaultPath;
        public string OutDir;
        public bool Capture;
        public int CaptureMaxFrames = 2000;
        public int CaptureFromFrame;
        public bool VSync = true;
        public float WarmupSeconds = 2f;
        /// <summary>Stop after this many seconds of path; 0 plays the whole path.</summary>
        public float MaxSeconds;
        public bool QuitWhenDone;

        public bool Done { get; private set; }
        public BenchSummary Result { get; private set; }

        public static BenchmarkRunner FromCommandLine(GameObject host)
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-dad-bench");
            if (i < 0) return null;
            string Value(string flag)
            {
                int j = Array.IndexOf(args, flag);
                return j >= 0 && j + 1 < args.Length ? args[j + 1] : null;
            }
            var r = host.AddComponent<BenchmarkRunner>();
            if (i + 1 < args.Length && !args[i + 1].StartsWith("-", StringComparison.Ordinal)) r.PathName = args[i + 1];
            r.OutDir = Value("-dad-bench-out") ?? Path.Combine(Application.persistentDataPath, "bench-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            r.Capture = Array.IndexOf(args, "-dad-bench-capture") >= 0;
            if (int.TryParse(Value("-dad-bench-capture-from"), out int from)) r.CaptureFromFrame = Math.Max(0, from);
            r.VSync = Array.IndexOf(args, "-dad-bench-no-vsync") < 0;
            r.QuitWhenDone = true;
            return r;
        }

        IEnumerator Start()
        {
            CameraPath path;
            var cam = Camera.main;
            try
            {
                if (cam == null) throw new InvalidOperationException("no main camera");
                if (string.IsNullOrEmpty(OutDir)) throw new InvalidOperationException("no output folder");
                path = CameraPath.Load(PathName);
                Directory.CreateDirectory(OutDir);
            }
            catch (Exception e)
            {
                Debug.LogError("[Dad] bench cannot start: " + e.Message);
                Done = true;
                if (QuitWhenDone) Application.Quit(1);
                yield break;
            }

            var origin = FindAnyObjectByType<FloatingOrigin>();
            var player = cam.GetComponent<CameraPathPlayer>();
            if (player == null) player = cam.gameObject.AddComponent<CameraPathPlayer>();
            player.Origin = origin;
            if (origin != null)
            {
                var start = path.Evaluate(0f);
                origin.Target = cam.transform;
                origin.Recenter(start.east, start.north);
            }
            player.Load(path);
            QualitySettings.vSyncCount = VSync ? 1 : 0;
            Application.targetFrameRate = VSync ? -1 : UncappedFrameRate;
            // A player started from a terminal is often not the front window; by default Unity
            // pauses it then, and the run would never finish.
            Application.runInBackground = true;
            Debug.Log($"[Dad] bench {PathName} started: {path.duration} s path, warmup {WarmupSeconds} s, capture {(Capture && Debug.isDebugBuild ? CaptureMaxFrames + " frames from frame " + CaptureFromFrame : "off")}, out {OutDir}");

            float until = Time.realtimeSinceStartup + WarmupSeconds;
            while (Time.realtimeSinceStartup < until) yield return null;

            // Set-up costs (recorder buffers, starting the profiler) land in these frames, which
            // are not sampled: the first sampled frame is an ordinary one.
            float limit = MaxSeconds > 0f ? Mathf.Min(MaxSeconds, path.duration) : path.duration;
            var recorder = new FrameRecorder((int)(limit * (VSync ? 130 : UncappedFrameRate + 20)) + 600);
            string raw = Path.Combine(OutDir, "capture");
            bool wantCapture = Capture && Debug.isDebugBuild, capturing = false;
            int captureStart = 0;
            if (wantCapture && CaptureFromFrame <= 0) StartCapture(ref capturing);
            yield return null;
            yield return null;
            int shifts = origin != null ? origin.Shifts : 0, captured = 0;
            // Kit pop-in during the path alone: the warm-up's first loads are left out.
            var kitStreamer = FindAnyObjectByType<ChunkStreamer>();
            int kitLateAtStart = kitStreamer != null ? kitStreamer.KitLateFrames : 0;
            float t0 = Time.realtimeSinceStartup;
            player.Playing = true;
            while (player.Time < limit && recorder.Count < recorder.Capacity)
            {
                yield return null;
                recorder.Sample();
                // A capture started mid-path begins at the frame after sampled frame CaptureFromFrame - 1.
                if (wantCapture && !capturing && captured == 0 && CaptureFromFrame > 0 && recorder.Count == CaptureFromFrame)
                {
                    StartCapture(ref capturing);
                    captureStart = recorder.Count;
                }
                if (capturing && recorder.Count - captureStart >= CaptureMaxFrames) StopCapture(ref capturing, ref captured);
            }
            player.Playing = false;
            float seconds = Time.realtimeSinceStartup - t0;
            StopCapture(ref capturing, ref captured);

            var s = new BenchSummary
            {
                path = PathName,
                scene = SceneManager.GetActiveScene().name,
                build = Application.isEditor ? "editor" : Debug.isDebugBuild ? "development player" : "release player",
                unity = Application.unityVersion,
                device = SystemInfo.deviceModel,
                cpu = SystemInfo.processorType,
                gpu = SystemInfo.graphicsDeviceName,
                graphicsApi = SystemInfo.graphicsDeviceType.ToString(),
                systemMemoryMb = SystemInfo.systemMemorySize,
                width = Screen.width,
                height = Screen.height,
                vSyncCount = QualitySettings.vSyncCount,
                targetFrameRate = Application.targetFrameRate,
                warmupSeconds = WarmupSeconds,
                seconds = seconds,
                originShifts = origin != null ? origin.Shifts - shifts : 0,
            };
            recorder.Fill(s);
            TextureCensus.Fill(s);
            var streamer = FindAnyObjectByType<ChunkStreamer>();
            if (streamer != null)
            {
                s.streamingApplied = streamer.Applied;
                s.streamingUnloaded = streamer.Unloaded;
                s.streamingMaxApplyFrameMs = streamer.MaxApplyFrameMs;
                s.streamingMaxApplyStepMs = streamer.MaxApplyStepMs;
                s.streamingMaxStepMsByKind = (float[])streamer.MaxStepMsByKind.Clone();
                s.streamingWarmupMs = streamer.WarmupMs;
                s.streamingPreparedMethods = streamer.PreparedMethods;
                s.streamingStepsWithGc = streamer.StepsWithGc;
                s.streamingMaxGcStepMs = streamer.MaxGcStepMs;
                s.streamingHeldFrames = streamer.HeldFrames;
                s.streamingMaxHeldFrames = streamer.MaxHeldFrames;
                s.streamingReleased = streamer.Released;
                s.streamingStillReadable = streamer.StillReadable;
                s.streamingMaxReleaseMs = streamer.MaxReleaseMs;
                s.streamingMaxReleaseFrameMs = streamer.MaxReleaseFrameMs;
                s.streamingKitsBuilt = streamer.KitsBuilt;
                s.streamingKitsDropped = streamer.KitsDropped;
                s.streamingKitsHeld = streamer.KitsHeld;
                s.streamingKitLateFrames = streamer.KitLateFrames;
                s.streamingKitLateFramesOnPath = streamer.KitLateFrames - kitLateAtStart;
                s.streamingMaxKitLateCells = streamer.MaxKitLateCells;
                s.streamingKitFailures = streamer.KitFailures;
                s.streamingMaxKitUploadMs = streamer.MaxKitUploadMs;
                s.streamingMaxKitFrameMs = streamer.MaxKitFrameMs;
            }
            s.pathSeconds = player.Time;
            s.complete = player.Time >= limit;
            s.captureFrames = captured;
            s.captureFromFrame = CaptureFromFrame;
            s.capture = captured > 0 && File.Exists(raw + ".raw") ? "capture.raw" : captured > 0 && File.Exists(raw) ? "capture" : "";
            File.WriteAllText(Path.Combine(OutDir, "summary.json"), JsonUtility.ToJson(s, true));
            recorder.WriteCsv(Path.Combine(OutDir, "frames.csv"));
            recorder.Dispose();
            Result = s;
            Debug.Log($"[Dad] bench {PathName}: {s.frames} frames in {s.seconds:F1} s, frame p50 {s.frameMs?.p50:F2} ms, p95 {s.frameMs?.p95:F2} ms; out {OutDir}");
            Done = true;
            if (QuitWhenDone) Application.Quit(0);

            void StartCapture(ref bool on)
            {
                Profiler.logFile = raw;
                Profiler.enableBinaryLog = true;
                Profiler.enabled = true;
                on = true;
            }

            void StopCapture(ref bool on, ref int frames)
            {
                if (!on) return;
                Profiler.enabled = false;
                Profiler.enableBinaryLog = false;
                Profiler.logFile = "";
                frames = recorder.Count - captureStart;
                on = false;
            }
        }
    }
}
