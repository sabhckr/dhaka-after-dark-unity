// Per-frame timings and memory into preallocated arrays: no allocation while recording.
// Sources: Time.unscaledDeltaTime (always), FrameTimingManager (CPU, main thread, render
// thread, GPU; needs Player Settings > Frame Timing Stats), and ProfilerRecorder memory and
// render counters (batches, SetPass calls, triangles, shadow casters, graphics, mesh and managed-heap memory: these
// need a development player, D-01). On macOS, also the main thread's own CPU time from one sample to the next (the
// thread's CPU clock): a frame whose main thread took long but used little CPU was not running
// for part of it, which no capture had caught (C-07). A source that gives nothing is listed in the summary under "unavailable" and its
// stat has samples = 0 (JsonUtility cannot write null); it is never reported as 0 ms.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Unity.Profiling;
using UnityEngine;

namespace Dad.Core
{
    [Serializable]
    public sealed class BenchStat
    {
        public int samples;
        public float p50, p95, p99, max, mean;
    }

    [Serializable]
    public sealed class BenchSummary
    {
        public string format = "dad-bench/1";
        public string path, scene, build, unity, device, cpu, gpu, graphicsApi;
        public int systemMemoryMb, width, height, vSyncCount, targetFrameRate;
        public float warmupSeconds, seconds, pathSeconds;
        public bool complete;
        public int frames, framesOver20ms, framesOver33_3ms, originShifts;
        /// <summary>Frames whose FrameTimingManager CPU frame time is over 33.3 ms. Time.unscaledDeltaTime
        /// can read exactly one vsync interval through a long stall (seen: 16.667 ms for a 575 ms frame), so
        /// stalls are counted by this too.</summary>
        public int cpuFramesOver33_3ms;
        public BenchStat frameMs, cpuMs, mainThreadMs, renderThreadMs, gpuMs;
        /// <summary>The main thread's CPU time per frame, waits included (macOS only).</summary>
        public BenchStat mainThreadCpuMs;
        public long gcAllocTotalBytes, gcAllocMaxFrameBytes;
        public int gcAllocFrames;
        /// <summary>Garbage collections that finished during the run (GC.CollectionCount), and whether the GC is incremental.</summary>
        public int gcCollections;
        public bool gcIncremental;
        public long systemMemoryStartBytes, systemMemoryPeakBytes, systemMemoryEndBytes;
        /// <summary>Render counters per frame, every camera and pass included (shadow maps too), averaged
        /// over blocks of RenderBlock frames: the render thread finishes a frame out of step with the main
        /// thread's samples, so one sample holds 0, 1 or 2 frames' worth (seen: 0, 69, 138 batches).</summary>
        public BenchStat batches, setPassCalls, triangles, shadowCasters;
        /// <summary>Graphics memory in use (meshes, textures, render targets): peak and at the end.</summary>
        public long gfxMemoryPeakBytes, gfxMemoryEndBytes;
        /// <summary>Of the memory above (K-45): meshes (their CPU copies and GPU buffers, as Unity counts them), and the
        /// managed heap the GC holds (live objects, pooled arrays and free space it has not handed back), peak and at the end.</summary>
        public long meshMemoryPeakBytes, meshMemoryEndBytes, gcReservedPeakBytes, gcReservedEndBytes;
        /// <summary>Texture memory as the profiler counts it ("Texture Memory"), peak and at the end (K-63).</summary>
        public long textureMemoryPeakBytes, textureMemoryEndBytes;
        /// <summary>At the end of the path, every loaded texture by source, and the largest ones (TextureCensus).</summary>
        public int textureCount;
        public TextureGroup[] textureGroups;
        public TextureEntry[] textureLargest;
        public string[] unavailable;
        public string capture;
        /// <summary>Per-frame Dad.Streaming.Apply time, over the frames that applied anything.</summary>
        public BenchStat streamingApplyMs;
        public int streamingApplyFrames, streamingApplied, streamingUnloaded;
        public float streamingMaxApplyFrameMs, streamingMaxApplyStepMs;
        public float[] streamingMaxStepMsByKind;
        public float streamingWarmupMs;
        public int streamingPreparedMethods;
        public int streamingStepsWithGc;
        public float streamingMaxGcStepMs;
        /// <summary>Frames finished chunks waited for loads still building, and the longest wait.</summary>
        public int streamingHeldFrames, streamingMaxHeldFrames;
        /// <summary>Streamed meshes whose CPU copy was dropped once drawn, those still holding one at the end, and the
        /// slowest single drop and frame of drops (K-45).</summary>
        public int streamingReleased, streamingStillReadable;
        public float streamingMaxReleaseMs, streamingMaxReleaseFrameMs;
        /// <summary>The near rooftop kit (K-45, approach 4 prototype): per-frame Dad.Streaming.Kit time over the frames that
        /// uploaded one; kits uploaded, dropped and held at the end; frames in which a cell drawn near was still waiting for
        /// its kit (since the streamer started, and during the sampled path alone) and the most such cells in one frame;
        /// failed builds; the slowest upload and frame of uploads.</summary>
        public BenchStat streamingKitMs;
        public int streamingKitFrames, streamingKitsBuilt, streamingKitsDropped, streamingKitsHeld, streamingKitLateFrames, streamingKitLateFramesOnPath, streamingMaxKitLateCells, streamingKitFailures;
        public float streamingMaxKitUploadMs, streamingMaxKitFrameMs;
        public int captureFrames;
        public int captureFromFrame;
        /// <summary>The Phase C rule, read per frame: main-thread frames over 4 ms after the set-up frames,
        /// and how many of them had streaming work (Plan + Apply + Kit of 0.3 ms or more) or a finished garbage
        /// collection within StreamingWindow frames of them (the timing columns lag by a frame or two).</summary>
        public int mainFramesOver4ms, mainFramesOver4msNearStreaming;
        /// <summary>The first of those frames, as "row:ms/cpu": the most main-thread CPU time in the row or
        /// one either side ("*" marks one near streaming).</summary>
        public string[] mainFramesOver4msList;
    }

    public sealed class FrameRecorder : IDisposable
    {
        public readonly int Capacity;
        public int Count { get; private set; }

        /// <summary>Rows 0-4 are the bench's own set-up: the first samples, before any chunk is asked for.</summary>
        public const int SetupFrames = 5;
        /// <summary>Frames either side of a slow frame searched for streaming work or a collection.</summary>
        public const int StreamingWindow = 3;
        const float StreamingWorkMs = 0.3f;
        public const int RenderBlock = 10;

        readonly float[] frame, cpu, main, render, gpu, apply, plan, kit, mainCpu, batches, setPass, triangles, casters;
        double lastCpuMs = -1;
        readonly long[] gc;
        readonly int[] collections;
        int lastCollections = -1;
        readonly FrameTiming[] timing = new FrameTiming[1];
        ProfilerRecorder gcAlloc, systemMemory, streamingApply, streamingPlan, streamingKit, batchCount, setPassCount, triangleCount, casterCount, gfxMemory, meshMemory, gcReserved, textureMemory;
        long memStart = -1, memPeak = -1, memEnd = -1, gfxPeak = -1, gfxEnd = -1, meshPeak = -1, meshEnd = -1, heapPeak = -1, heapEnd = -1, texPeak = -1, texEnd = -1;

        public FrameRecorder(int capacity)
        {
            Capacity = capacity;
            frame = new float[capacity];
            cpu = new float[capacity];
            main = new float[capacity];
            render = new float[capacity];
            gpu = new float[capacity];
            gc = new long[capacity];
            collections = new int[capacity];
            apply = new float[capacity];
            plan = new float[capacity];
            kit = new float[capacity];
            mainCpu = new float[capacity];
            batches = new float[capacity];
            setPass = new float[capacity];
            triangles = new float[capacity];
            casters = new float[capacity];
            gcAlloc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            systemMemory = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "System Used Memory");
            streamingApply = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Dad.Streaming.Apply");
            streamingPlan = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Dad.Streaming.Plan");
            streamingKit = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Dad.Streaming.Kit");
            batchCount = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
            setPassCount = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
            triangleCount = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
            casterCount = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Shadow Casters Count");
            gfxMemory = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Gfx Used Memory");
            meshMemory = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Mesh Memory");
            gcReserved = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Reserved Memory");
            textureMemory = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Texture Memory");
        }

        /// <summary>Call once per frame, after the frame's Update.</summary>
        public void Sample()
        {
            if (Count >= Capacity) return;
            using (DadMarkers.BenchSample.Auto())
            {
                int i = Count++;
                frame[i] = Time.unscaledDeltaTime * 1000f;
                FrameTimingManager.CaptureFrameTimings();
                if (FrameTimingManager.GetLatestTimings(1, timing) > 0)
                {
                    cpu[i] = (float)timing[0].cpuFrameTime;
                    main[i] = (float)timing[0].cpuMainThreadFrameTime;
                    render[i] = (float)timing[0].cpuRenderThreadFrameTime;
                    gpu[i] = (float)timing[0].gpuFrameTime;
                }
                gc[i] = gcAlloc.Valid ? gcAlloc.LastValue : -1;
                int n = GC.CollectionCount(0);
                collections[i] = lastCollections < 0 ? 0 : n - lastCollections;
                lastCollections = n;
                apply[i] = streamingApply.Valid ? streamingApply.LastValue / 1e6f : 0f;
                plan[i] = streamingPlan.Valid ? streamingPlan.LastValue / 1e6f : 0f;
                kit[i] = streamingKit.Valid ? streamingKit.LastValue / 1e6f : 0f;
                double now = ThreadCpuMs();
                mainCpu[i] = lastCpuMs >= 0 && now >= 0 ? (float)(now - lastCpuMs) : 0f;
                lastCpuMs = now;
                batches[i] = batchCount.Valid ? batchCount.LastValue : 0;
                setPass[i] = setPassCount.Valid ? setPassCount.LastValue : 0;
                triangles[i] = triangleCount.Valid ? triangleCount.LastValue : 0;
                casters[i] = casterCount.Valid ? casterCount.LastValue : 0;
                if (gfxMemory.Valid)
                {
                    gfxPeak = Math.Max(gfxPeak, gfxMemory.LastValue);
                    gfxEnd = gfxMemory.LastValue;
                }
                if (meshMemory.Valid)
                {
                    meshPeak = Math.Max(meshPeak, meshMemory.LastValue);
                    meshEnd = meshMemory.LastValue;
                }
                if (gcReserved.Valid)
                {
                    heapPeak = Math.Max(heapPeak, gcReserved.LastValue);
                    heapEnd = gcReserved.LastValue;
                }
                if (textureMemory.Valid)
                {
                    texPeak = Math.Max(texPeak, textureMemory.LastValue);
                    texEnd = textureMemory.LastValue;
                }
                if (systemMemory.Valid)
                {
                    long m = systemMemory.LastValue;
                    if (memStart < 0) memStart = m;
                    memPeak = Math.Max(memPeak, m);
                    memEnd = m;
                }
            }
        }

#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
        [StructLayout(LayoutKind.Sequential)]
        struct Timespec { public long Seconds, Nanoseconds; }

        [DllImport("/usr/lib/libSystem.dylib")]
        static extern int clock_gettime(int clock, out Timespec time);

        const int ThreadCpuClock = 16; // CLOCK_THREAD_CPUTIME_ID in <time.h>

        /// <summary>CPU time the calling thread has used, in ms; -1 where there is no such clock.</summary>
        static double ThreadCpuMs() => clock_gettime(ThreadCpuClock, out var t) == 0 ? t.Seconds * 1000.0 + t.Nanoseconds / 1e6 : -1;
#else
        static double ThreadCpuMs() => -1;
#endif

        public void Fill(BenchSummary s)
        {
            var missing = new List<string>();
            s.frames = Count;
            s.frameMs = Stat(frame, "frameMs", missing);
            s.cpuMs = Stat(cpu, "cpuMs", missing);
            s.mainThreadMs = Stat(main, "mainThreadMs", missing);
            s.renderThreadMs = Stat(render, "renderThreadMs", missing);
            s.gpuMs = Stat(gpu, "gpuMs", missing);
            s.mainThreadCpuMs = Stat(mainCpu, "mainThreadCpuMs", missing);
            s.batches = BlockStat(batches, "batches", missing);
            s.setPassCalls = BlockStat(setPass, "setPassCalls", missing);
            s.triangles = BlockStat(triangles, "triangles", missing);
            s.shadowCasters = BlockStat(casters, "shadowCasters", missing);
            if (gfxPeak >= 0) { s.gfxMemoryPeakBytes = gfxPeak; s.gfxMemoryEndBytes = gfxEnd; }
            else missing.Add("gfxMemory");
            if (meshPeak >= 0) { s.meshMemoryPeakBytes = meshPeak; s.meshMemoryEndBytes = meshEnd; }
            else missing.Add("meshMemory");
            if (heapPeak >= 0) { s.gcReservedPeakBytes = heapPeak; s.gcReservedEndBytes = heapEnd; }
            else missing.Add("gcReserved");
            if (texPeak >= 0) { s.textureMemoryPeakBytes = texPeak; s.textureMemoryEndBytes = texEnd; }
            else missing.Add("textureMemory");
            for (int i = 0; i < Count; i++)
            {
                // 20 ms: clearly past one 60 Hz interval (with vsync, half the frames sit just over 16.7).
                if (frame[i] > 20f) s.framesOver20ms++;
                if (frame[i] > 1000f / 30f) s.framesOver33_3ms++;
                if (cpu[i] > 1000f / 30f) s.cpuFramesOver33_3ms++;
            }
            if (gcAlloc.Valid && Count > 0 && gc[0] >= 0)
            {
                for (int i = 0; i < Count; i++)
                {
                    s.gcAllocTotalBytes += gc[i];
                    s.gcAllocMaxFrameBytes = Math.Max(s.gcAllocMaxFrameBytes, gc[i]);
                    if (gc[i] > 0) s.gcAllocFrames++;
                }
            }
            else missing.Add("gcAlloc");
            for (int i = 0; i < Count; i++) s.gcCollections += collections[i];
            s.gcIncremental = UnityEngine.Scripting.GarbageCollector.isIncremental;
            if (memStart >= 0) { s.systemMemoryStartBytes = memStart; s.systemMemoryPeakBytes = memPeak; s.systemMemoryEndBytes = memEnd; }
            else missing.Add("systemMemory");
            var applied = new List<float>();
            for (int i = 0; i < Count; i++) if (apply[i] > 0f) applied.Add(apply[i]);
            s.streamingApplyFrames = applied.Count;
            s.streamingApplyMs = applied.Count > 0 ? Stat(applied.ToArray(), applied.Count, "streamingApplyMs", missing) : new BenchStat();
            var kits = new List<float>();
            for (int i = 0; i < Count; i++) if (kit[i] > 0f) kits.Add(kit[i]);
            s.streamingKitFrames = kits.Count;
            s.streamingKitMs = kits.Count > 0 ? Stat(kits.ToArray(), kits.Count, "streamingKitMs", missing) : new BenchStat();
            var slow = new List<string>();
            for (int i = SetupFrames; i < Count; i++)
            {
                if (main[i] <= 4f) continue;
                bool near = false;
                for (int j = Math.Max(0, i - StreamingWindow); j <= Math.Min(Count - 1, i + StreamingWindow); j++)
                    if (plan[j] + apply[j] + kit[j] >= StreamingWorkMs || collections[j] > 0) near = true;
                s.mainFramesOver4ms++;
                if (near) s.mainFramesOver4msNearStreaming++;
                float most = 0f;
                for (int j = Math.Max(0, i - 1); j <= Math.Min(Count - 1, i + 1); j++) most = Math.Max(most, mainCpu[j]);
                if (slow.Count < 20)
                    slow.Add(i + ":" + main[i].ToString("F2", CultureInfo.InvariantCulture) + "/" + most.ToString("F2", CultureInfo.InvariantCulture) + (near ? "*" : ""));
            }
            s.mainFramesOver4msList = slow.ToArray();
            s.unavailable = missing.ToArray();
        }

        BenchStat Stat(float[] values, string name, List<string> missing) => Stat(values, Count, name, missing);

        /// <summary>Stats of the means of whole blocks of RenderBlock frames.</summary>
        BenchStat BlockStat(float[] values, string name, List<string> missing)
        {
            int blocks = Count / RenderBlock;
            var means = new float[blocks];
            for (int b = 0; b < blocks; b++)
            {
                double sum = 0;
                for (int i = b * RenderBlock; i < (b + 1) * RenderBlock; i++) sum += values[i];
                means[b] = (float)(sum / RenderBlock);
            }
            return Stat(means, blocks, name, missing);
        }

        static BenchStat Stat(float[] values, int count, string name, List<string> missing)
        {
            var v = new float[count];
            Array.Copy(values, v, count);
            bool any = false;
            foreach (var x in v) if (x > 0f) { any = true; break; }
            if (!any) { missing.Add(name); return new BenchStat(); }
            Array.Sort(v);
            double sum = 0;
            foreach (var x in v) sum += x;
            float P(float p) => v[Math.Min(v.Length - 1, (int)(p / 100f * v.Length))];
            return new BenchStat { samples = v.Length, p50 = P(50), p95 = P(95), p99 = P(99), max = v[v.Length - 1], mean = (float)(sum / v.Length) };
        }

        public void WriteCsv(string file)
        {
            var sb = new StringBuilder("frame,frameMs,cpuMs,mainThreadMs,renderThreadMs,gpuMs,gcAllocBytes,gcCollections,streamingApplyMs,streamingPlanMs,mainCpuMs,batches,setPassCalls,triangles,shadowCasters\n");
            var c = CultureInfo.InvariantCulture;
            for (int i = 0; i < Count; i++)
                sb.Append(i).Append(',').Append(frame[i].ToString("F3", c)).Append(',').Append(cpu[i].ToString("F3", c)).Append(',')
                  .Append(main[i].ToString("F3", c)).Append(',').Append(render[i].ToString("F3", c)).Append(',')
                  .Append(gpu[i].ToString("F3", c)).Append(',').Append(gc[i]).Append(',').Append(collections[i]).Append(',').Append(apply[i].ToString("F3", c))
                  .Append(',').Append(plan[i].ToString("F3", c)).Append(',').Append(mainCpu[i].ToString("F3", c))
                  .Append(',').Append((long)batches[i]).Append(',').Append((long)setPass[i]).Append(',').Append((long)triangles[i]).Append(',').Append((long)casters[i]).Append('\n');
            File.WriteAllText(file, sb.ToString());
        }

        public void Dispose()
        {
            gcAlloc.Dispose();
            systemMemory.Dispose();
            streamingApply.Dispose();
            streamingPlan.Dispose();
            streamingKit.Dispose();
            batchCount.Dispose();
            setPassCount.Dispose();
            triangleCount.Dispose();
            casterCount.Dispose();
            gfxMemory.Dispose();
            meshMemory.Dispose();
            gcReserved.Dispose();
            textureMemory.Dispose();
        }
    }
}
