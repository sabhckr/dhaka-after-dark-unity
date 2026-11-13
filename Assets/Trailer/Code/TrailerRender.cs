// Renders the 90-second cinematic to numbered JPEG frames. It is a test so that the game's world can load, marked Explicit so ordinary test runs never start it.
//   DAD_TRAILER_ACT   scout | all | 1 | 2 | 3 | 4 | 5     DAD_TRAILER_STEP  every Nth frame (default 1)     DAD_TRAILER_ONLY  shot name filter
#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Dad.Trailer
{
    [Explicit("renders the trailer: run by name")]
    public class TrailerRender
    {
        static string Env(string k, string d) { var v = Environment.GetEnvironmentVariable(k); return string.IsNullOrEmpty(v) ? d : v; }

        [UnityTest, Timeout(86400000)]
        public IEnumerator Frames()
        {
            string act = Env("DAD_TRAILER_ACT", "scout"), only = Env("DAD_TRAILER_ONLY", ""), output = Env("DAD_TRAILER_OUT", "Evidence/local/frames");
            bool key = Env("DAD_TRAILER_KEY", "") == "1";
            int step = int.Parse(Env("DAD_TRAILER_STEP", "1")), w = int.Parse(Env("DAD_TRAILER_W", "1920")), h = int.Parse(Env("DAD_TRAILER_H", "1080"));
            Application.runInBackground = true;
            Directory.CreateDirectory(output);
            bool map = Env("DAD_TRAILER_MAP", "") == "1";
            StreamWriter log = null;
            if (map) { log = new StreamWriter(Path.Combine(output, "grip_audit.csv")); log.WriteLine("shot,frame,actor,measure,value,low,high,result"); GripMapper.Reset(); }
            foreach (string a in act == "all" ? new[] { "1", "s", "w", "p", "g" } : new[] { act })
            {
                var acts = new Acts(a);
                yield return acts.Build();
                var read = new Texture2D(w, h, TextureFormat.RGB24, false);
                var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                var cam = acts.Stage.Cam;
                cam.targetTexture = rt;
                foreach (var s in acts.Shots)
                {
                    if (only.Length > 0 && !Array.Exists(only.Split(','), o => s.Name.Contains(o))) continue;
                    var frames = new System.Collections.Generic.List<int>();
                    if (key) { frames.Add(s.First); frames.Add((s.First + s.Last) / 2); frames.Add(s.Last); }
                    else for (int f0 = s.First; f0 <= s.Last; f0 += step) frames.Add(f0);
                    foreach (var a2 in acts.Stage.Actors) a2.NewShot();
                    foreach (int f in frames)
                    {
                        float t = (f - s.First) / 30f, u = s.Last > s.First ? (f - s.First) / (float)(s.Last - s.First) : 0;
                        if (f == s.First) { s.Do(t, u); yield return null; } // someone switched on by this shot needs a frame before their pose takes
                        s.Do(t, u);
                        yield return null; // the animation system writes the pose during the frame
                        acts.Stage.Speak(f / 30f);
                        foreach (var a2 in acts.Stage.Actors) { a2.Ground(); a2.Finish(); }
                        s.After?.Invoke(t, u);
                        yield return null; // skinned meshes take their bones once a frame, after this point: without this they would miss the reach, the look and the mouth
                        acts.Particles(f);
                        cam.Render();
                        RenderTexture.active = rt;
                        read.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                        read.Apply(false);
                        RenderTexture.active = null;
                        File.WriteAllBytes(Path.Combine(output, $"f_{f:00000}.jpg"), read.EncodeToJPG(96));
                        if (map) foreach (var a2 in acts.Stage.Actors) GripMapper.Audit(a2, acts.Stage, cam, rt, read, output, s.Name, f, log);
                    }
                }
                cam.targetTexture = null;
                UnityEngine.Object.Destroy(rt); UnityEngine.Object.Destroy(read);
                acts.Stage.Clear();
                yield return Resources.UnloadUnusedAssets();
                GC.Collect();
            }
            if (log != null) { GripMapper.Finish(log); log.Dispose(); }
        }
    }
}
#endif
