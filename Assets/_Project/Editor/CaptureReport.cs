// Batch-mode reader for a profiler capture (capture.raw from Tools/bench.sh --capture), so a
// capture can be read without opening the Profiler window. Writes one TSV row per marker per frame
// for the main thread and the render thread:
//   frame  thread  frameMs  marker  selfMs  totalMs  calls
// Markers whose self and total time are both under 0.05 ms are left out. Run with
//   Unity -batchmode -quit -projectPath . -executeMethod Dad.EditorTools.CaptureReport.Run
//         -dad-capture <capture.raw> [-dad-capture-out <markers.tsv>]
// The editor keeps only its own frame history of a loaded capture; the first line of the TSV says
// which frames were loaded. Every first-call compilation (Mono.JIT) also goes to jit.tsv beside it,
// with where it was called from and the sample's metadata:
//   frame  thread  ms  path  metadata
// Exits 1 if the capture cannot be loaded.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine;

namespace Dad.EditorTools
{
    public static class CaptureReport
    {
        const float MinMs = 0.05f;

        public static void Run()
        {
            var args = Environment.GetCommandLineArgs();
            string Value(string flag)
            {
                int j = Array.IndexOf(args, flag);
                return j >= 0 && j + 1 < args.Length ? args[j + 1] : null;
            }
            string file = Value("-dad-capture");
            if (string.IsNullOrEmpty(file) || !File.Exists(file))
            {
                Debug.LogError("[Dad] capture report: no capture file (-dad-capture)");
                EditorApplication.Exit(1);
                return;
            }
            string output = Value("-dad-capture-out") ?? Path.Combine(Path.GetDirectoryName(file), "markers.tsv");
            if (!ProfilerDriver.LoadProfile(file, false))
            {
                Debug.LogError("[Dad] capture report: could not load " + file);
                EditorApplication.Exit(1);
                return;
            }
            int first = ProfilerDriver.firstFrameIndex, last = ProfilerDriver.lastFrameIndex;
            var inv = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            var jit = new StringBuilder("frame\tthread\tms\tpath\tmetadata\n");
            sb.Append("# ").Append(file).Append(": frames ").Append(first).Append("..").Append(last).Append('\n');
            sb.Append("frame\tthread\tframeMs\tmarker\tselfMs\ttotalMs\tcalls\n");
            var children = new List<int>();
            var stack = new Stack<int>();
            int rows = 0;
            for (int f = first; f <= last; f++)
            {
                for (int t = 0; ; t++)
                {
                    using var view = ProfilerDriver.GetHierarchyFrameDataView(f, t, HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName, HierarchyFrameDataView.columnSelfTime, false);
                    if (view == null || !view.valid) break;
                    string thread = view.threadName;
                    if (t != 0 && thread != "Render Thread") continue;
                    string frameMs = view.frameTimeMs.ToString("F3", inv);
                    stack.Clear();
                    stack.Push(view.GetRootItemID());
                    while (stack.Count > 0)
                    {
                        int id = stack.Pop();
                        children.Clear();
                        view.GetItemChildren(id, children);
                        foreach (var c in children) stack.Push(c);
                        if (id == view.GetRootItemID()) continue;
                        float self = view.GetItemColumnDataAsFloat(id, HierarchyFrameDataView.columnSelfTime);
                        float total = view.GetItemColumnDataAsFloat(id, HierarchyFrameDataView.columnTotalTime);
                        if (view.GetItemName(id) == "Mono.JIT")
                        {
                            jit.Append(f).Append('\t').Append(thread).Append('\t').Append(total.ToString("F3", inv)).Append('\t').Append(view.GetItemPath(id)).Append('\t');
                            int samples = view.GetItemMergedSamplesCount(id);
                            for (int m = 0; m < samples; m++)
                            {
                                int count = view.GetItemMergedSamplesMetadataCount(id, m);
                                for (int k = 0; k < count; k++) jit.Append(view.GetItemMergedSamplesMetadata(id, m, k)).Append(" | ");
                            }
                            jit.Append('\n');
                        }
                        if (self < MinMs && total < MinMs) continue;
                        float calls = view.GetItemColumnDataAsFloat(id, HierarchyFrameDataView.columnCalls);
                        sb.Append(f).Append('\t').Append(thread).Append('\t').Append(frameMs).Append('\t')
                          .Append(view.GetItemName(id)).Append('\t').Append(self.ToString("F3", inv)).Append('\t')
                          .Append(total.ToString("F3", inv)).Append('\t').Append(calls.ToString("F0", inv)).Append('\n');
                        rows++;
                    }
                    if (t != 0) break;
                }
            }
            File.WriteAllText(output, sb.ToString());
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(output), "jit.tsv"), jit.ToString());
            Debug.Log($"[Dad] capture report: frames {first}..{last}, {rows} rows, {output}");
        }
    }
}
