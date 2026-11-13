// D-03 exit evidence: a whole day on the Boot scene has no step changes. The camera stays put, over
// Farmgate at the free camera's start (95 m up) or in the street at Kawran Bazar (1.7 m up, where
// the shadows are: they reach 150 m from the camera), while the clock steps one game minute at a
// time from midnight to midnight; each minute is rendered small and compared with the one before. The change per game minute is bounded, so
// at the play speed (half a game minute a second) the change per frame is 1/120 of that. And no
// minute changes more than both minutes either side of it by more than a small margin: the rate of
// change itself moves smoothly (by at most 0.05 levels from one minute to the next on 2026-11-02),
// so a light switching or shadows snapping in shows as one minute standing out. The clock is
// applied six times a minute, as frames would, and each frame's DayCycle.Apply is timed. The series
// go to Evidence/local/day-cycle-<view>.csv.
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Dad.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Dad.Tests
{
    public class DayCycleTests
    {
        const int Width = 128, Height = 72;
        /// <summary>Mean absolute channel change (0-255) allowed from one game minute to the next.</summary>
        const double MaxPerMinute = 4;
        /// <summary>How much a minute may change beyond the larger of the minutes either side of it (0-255).</summary>
        const double MaxStep = 0.25;
        const int FramesPerMinute = 6;
        /// <summary>Editor milliseconds the day cycle may take in 99 frames out of 100.</summary>
        const double MaxApplyMs = 1;

        [SetUp]
        public void MarkScenes() => TestScenes.Mark();

        [UnityTearDown]
        public IEnumerator CleanUp() => TestScenes.Clean();

        [UnityTest]
        public IEnumerator AWholeDayHasNoStepChanges([Values("air", "street")] string view)
        {
            SceneManager.LoadScene("Boot");
            yield return null;
            var root = Object.FindAnyObjectByType<GameRoot>();
            Assert.IsNotNull(root, "GameRoot in Boot scene");
            var day = root.Day;
            Assert.IsNotNull(day, "GameRoot made the day cycle");
            day.MinutesPerSecond = 0;
            day.Set(DayCycle.DefaultDate, 0, Weather.Clear);
            // This measures the city's light. Traffic moves, and its headlamp beams with it, each time the test yields a frame.
            var traffic = Object.FindAnyObjectByType<TrafficView>();
            if (traffic != null) traffic.enabled = false;
            // So do the people on the sidewalks, and this street is in the hub.
            var crowd = Object.FindAnyObjectByType<NpcCrowd>();
            if (crowd != null) crowd.enabled = false;
            // The real lamp lights fade by frames, and this test renders between frames with only one every two hours, so they would jump where a frame falls. Glow and windows follow the clock.
            var lamps = Object.FindAnyObjectByType<StreetLamps>();
            if (lamps != null) lamps.enabled = false;
            var cam = Camera.main;
            var grade = Object.FindAnyObjectByType<Grade>();
            Assert.IsNotNull(grade, "GameRoot made the grade");
            if (view == "street")
            {
                // In the hub, under the metro.
                var origin = Object.FindAnyObjectByType<FloatingOrigin>();
                origin.Recenter(2640, 2118);
                cam.GetComponent<FreeCamera>().Place(origin.Offset, 2640, 2118, 1.7f, 2720, 2160, 1.2f);
            }

            // Let the city round the camera stream in.
            var streamer = Object.FindAnyObjectByType<ChunkStreamer>();
            float until = Time.realtimeSinceStartup + 60f;
            int quiet = 0;
            while (quiet < 10)
            {
                Assert.Less(Time.realtimeSinceStartup, until, "streaming settled within 60 s");
                yield return null;
                quiet = streamer == null || (streamer.IndexReady && streamer.Idle && streamer.Applied > 0) ? quiet + 1 : 0;
            }

            var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var read = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            var change = new List<double>();
            var luminance = new List<double>();
            var applyMs = new List<double>();
            var clock = new System.Diagnostics.Stopwatch();
            Color32[] prev = null;
            try
            {
                for (int minute = 0; minute <= 24 * 60; minute++)
                {
                    // Six frames a minute, as the running clock applies them: enough for a reflection
                    // refill (a face a frame) to finish within the minute. Each frame is timed.
                    for (int frame = 1; minute > 0 && frame <= FramesPerMinute; frame++)
                    {
                        day.Hours = (minute - 1 + frame / (double)FramesPerMinute) / 60.0;
                        clock.Restart();
                        day.Apply();
                        applyMs.Add(clock.Elapsed.TotalMilliseconds);
                        grade.Apply(); // what LateUpdate does in a frame
                    }
                    if (minute == 12 * 60) Assert.AreSame(day.Sun, RenderSettings.sun, "the sun is the main light at noon");
                    if (minute == 21 * 60) Assert.AreSame(day.Moon, RenderSettings.sun, "the moon is the main light in the evening");
                    var before = cam.targetTexture;
                    cam.targetTexture = rt;
                    cam.Render();
                    cam.targetTexture = before;
                    RenderTexture.active = rt;
                    read.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                    RenderTexture.active = null;
                    var px = read.GetPixels32();
                    double lum = 0, diff = 0;
                    for (int i = 0; i < px.Length; i++)
                    {
                        lum += 0.2126 * px[i].r + 0.7152 * px[i].g + 0.0722 * px[i].b;
                        if (prev != null) diff += System.Math.Abs(px[i].r - prev[i].r) + System.Math.Abs(px[i].g - prev[i].g) + System.Math.Abs(px[i].b - prev[i].b);
                    }
                    luminance.Add(lum / px.Length);
                    if (prev != null) change.Add(diff / (px.Length * 3));
                    prev = px;
                    if (minute % 120 == 0) yield return null; // keep the editor responsive
                }
            }
            finally
            {
                RenderTexture.active = null;
                Object.Destroy(rt);
                Object.Destroy(read);
            }

            var csv = new StringBuilder("minute,luminance,change\n");
            for (int m = 0; m < luminance.Count; m++)
                csv.Append($"{m},{luminance[m]:F3},{(m == 0 ? 0 : change[m - 1]):F4}\n");
            Directory.CreateDirectory("Evidence/local");
            File.WriteAllText($"Evidence/local/day-cycle-{view}.csv", csv.ToString());

            // change[i] is the change from minute i to minute i + 1.
            int worst = 0, worstStep = 1;
            double Step(int i) => change[i] - System.Math.Max(change[i - 1], change[i + 1]);
            for (int i = 0; i < change.Count; i++)
            {
                if (change[i] > change[worst]) worst = i;
                if (i > 0 && i < change.Count - 1 && Step(i) > Step(worstStep)) worstStep = i;
            }
            string Clock(int i) => $"{i / 60:00}:{i % 60:00}";
            Debug.Log($"Day cycle, {view}: luminance {luminance.Min():F1} to {luminance.Max():F1}; largest change per minute {change[worst]:F3} at {Clock(worst)}; "
                + $"largest step over the minutes either side {Step(worstStep):F3} at {Clock(worstStep)}");
            applyMs.Sort();
            Debug.Log($"Day cycle, {view}: DayCycle.Apply a frame (place, light, a reflection face when due) median {applyMs[applyMs.Count / 2]:F3} ms, "
                + $"99th percentile {applyMs[applyMs.Count * 99 / 100]:F3} ms, max {applyMs[applyMs.Count - 1]:F3} ms (editor)");
            Assert.Greater(luminance.Max() - luminance.Min(), 30, "day and night differ");
            // A guard on the main thread's share: refilling the reflection whole every minute, as the
            // first cut did, took about 5 ms here.
            Assert.Less(applyMs[applyMs.Count * 99 / 100], MaxApplyMs, "DayCycle.Apply, 99th percentile of frames");
            Assert.LessOrEqual(change[worst], MaxPerMinute, $"change from {Clock(worst)} to the next minute");
            Assert.LessOrEqual(Step(worstStep), MaxStep, $"a step at {Clock(worstStep)}: {change[worstStep]:F3}, the minutes either side {change[worstStep - 1]:F3} and {change[worstStep + 1]:F3}");
        }
    }
}
