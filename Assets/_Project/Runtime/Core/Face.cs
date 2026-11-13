// The protagonists' faces (2026-11-10). They blink, the eyes glance about, and while a line is spoken the mouth takes the shapes Rhubarb Lip Sync cut the line's sound
// into (Tools/characters/lipsync.sh writes them beside the line). Rhubarb names nine mouths, A to H and X (at rest); each is made here from the ten speech shapes the
// models carry (AA EE IH OH OU FV MBP L WQ TH, Tools/characters/protagonist.py), on the body and on every part that has the shape: teeth, tongue, brows. The mouth
// moves to each new shape over a few hundredths of a second, as a mouth does, not in a cut.
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Dad.Core
{
    [DefaultExecutionOrder(100)] // after the animation
    public sealed class Face : MonoBehaviour
    {
        public struct Cue { public float At; public char Mouth; }

        /// <summary>Rhubarb's mouths as the models' shapes, weights out of 100. A: closed lips (M B P). B: teeth together (K S T EE). C: open (EH AE). D: wide open (AA).
        /// E: rounded (AO ER). F: puckered (UW OW W). G: lip on teeth (F V). H: tongue up (L). X: at rest.</summary>
        public static readonly Dictionary<char, (string shape, float weight)[]> Mouths = new Dictionary<char, (string, float)[]>
        {
            { 'A', new[] { ("MBP", 100f) } }, { 'B', new[] { ("EE", 70f) } }, { 'C', new[] { ("IH", 100f) } }, { 'D', new[] { ("AA", 100f) } },
            { 'E', new[] { ("OH", 100f) } }, { 'F', new[] { ("OU", 80f), ("WQ", 40f) } }, { 'G', new[] { ("FV", 100f) } }, { 'H', new[] { ("L", 100f) } },
            { 'X', new (string, float)[0] },
        };
        static readonly string[] Speech = { "AA", "EE", "IH", "OH", "OU", "FV", "MBP", "L", "WQ", "TH" };

        /// <summary>Where the line's sound comes from; its time is the mouth's clock.</summary>
        public AudioSource Voice;
        public bool Blinks = true;

        readonly Dictionary<string, List<(SkinnedMeshRenderer r, int i)>> shapes = new Dictionary<string, List<(SkinnedMeshRenderer, int)>>();
        readonly Dictionary<string, float> now = new Dictionary<string, float>(), want = new Dictionary<string, float>();
        List<Cue> line;
        float nextBlink, blinkAt = -1, nextGlance;
        Transform eyeL, eyeR;
        Quaternion glance = Quaternion.identity, glanceTo = Quaternion.identity; // the eyes are posed afresh by the animation every frame, so the glance is added to that

        public static Face Give(Animator body)
        {
            var f = body.gameObject.AddComponent<Face>();
            f.Voice = body.gameObject.AddComponent<AudioSource>();
            f.Voice.playOnAwake = false;
            f.Voice.spatialBlend = 1;
            f.Voice.minDistance = 1.5f;
            return f;
        }

        void Awake()
        {
            foreach (var r in GetComponentsInChildren<SkinnedMeshRenderer>(true))
                for (int i = 0; i < r.sharedMesh.blendShapeCount; i++)
                {
                    string name = r.sharedMesh.GetBlendShapeName(i);
                    if (!shapes.TryGetValue(name, out var list)) shapes[name] = list = new List<(SkinnedMeshRenderer, int)>();
                    list.Add((r, i));
                }
            foreach (var s in Speech) { now[s] = 0; want[s] = 0; }
            var a = GetComponent<Animator>();
            if (a != null && a.isHuman) { eyeL = a.GetBoneTransform(HumanBodyBones.LeftEye); eyeR = a.GetBoneTransform(HumanBodyBones.RightEye); }
            nextBlink = Time.time + Random.Range(1f, 4f);
        }

        /// <summary>Has the model this shape anywhere?</summary>
        public bool Has(string shape) => shapes.ContainsKey(shape);

        /// <summary>The weight the face has now for a speech shape.</summary>
        public float Weight(string shape) => now.TryGetValue(shape, out var w) ? w : 0;

        /// <summary>Reads Rhubarb's cue file: one line per change, the time in seconds and the mouth, by a tab (its tsv output; the JSON output's "mouthCues" also reads).</summary>
        public static List<Cue> Read(string text)
        {
            var cues = new List<Cue>();
            if (string.IsNullOrEmpty(text)) return cues;
            if (text.TrimStart().StartsWith("{"))
            {
                foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(text, "\"start\"\\s*:\\s*([0-9.]+)[^}]*?\"value\"\\s*:\\s*\"([A-HX])\""))
                    cues.Add(new Cue { At = float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture), Mouth = m.Groups[2].Value[0] });
                return cues;
            }
            foreach (var raw in text.Split('\n'))
            {
                var parts = raw.Trim().Split('\t');
                if (parts.Length < 2 || parts[1].Length != 1 || !Mouths.ContainsKey(parts[1][0])) continue;
                if (!float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float at)) continue;
                cues.Add(new Cue { At = at, Mouth = parts[1][0] });
            }
            return cues;
        }

        /// <summary>Speaks a line: plays its sound and moves the mouth by its cues.</summary>
        public void Say(AudioClip clip, string cues)
        {
            line = Read(cues);
            Voice.clip = clip;
            Voice.volume = GameSettings.Current.Voices;
            Voice.Play();
        }

        /// <summary>The mouth Rhubarb gave for this moment of the line ('X' before, between and after).</summary>
        public static char MouthAt(List<Cue> cues, float time)
        {
            char mouth = 'X';
            if (cues == null) return mouth;
            foreach (var c in cues) { if (c.At > time) break; mouth = c.Mouth; }
            return mouth;
        }

        /// <summary>Speaks by the cues alone, from now, with no sound: for a line whose audio is played elsewhere, and for tests.</summary>
        public void Mime(List<Cue> cues) { line = cues; mimeStart = Time.time; Voice.Stop(); }
        float mimeStart = -1;

        void LateUpdate()
        {
            float t = Voice != null && Voice.isPlaying ? Voice.time : mimeStart >= 0 ? Time.time - mimeStart : -1;
            char mouth = t >= 0 ? MouthAt(line, t) : 'X';
            foreach (var s in Speech) want[s] = 0;
            foreach (var (shape, weight) in Mouths[mouth]) want[shape] = weight;
            float k = 1 - Mathf.Exp(-Time.deltaTime * 28); // about 35 ms to most of the way
            foreach (var s in Speech)
            {
                now[s] = Mathf.Lerp(now[s], want[s], k);
                Set(s, now[s]);
            }
            if (Blinks)
            {
                if (Time.time >= nextBlink) { blinkAt = Time.time; nextBlink = Time.time + Random.Range(2f, 6f); }
                float b = blinkAt < 0 ? 0 : 1 - Mathf.Abs((Time.time - blinkAt) / 0.075f - 1); // 75 ms down, 75 ms up
                Set("eyeBlinkLeft", Mathf.Clamp01(b) * 100);
                Set("eyeBlinkRight", Mathf.Clamp01(b) * 100);
            }
            if (eyeL != null && eyeR != null)
            {
                if (Time.time >= nextGlance) { glanceTo = Quaternion.Euler(Random.Range(-4f, 4f), Random.Range(-9f, 9f), 0); nextGlance = Time.time + Random.Range(0.8f, 3f); }
                glance = Quaternion.Slerp(glance, glanceTo, 1 - Mathf.Exp(-Time.deltaTime * 20));
                eyeL.localRotation = eyeL.localRotation * glance;
                eyeR.localRotation = eyeR.localRotation * glance;
            }
        }

        void Set(string shape, float weight)
        {
            if (!shapes.TryGetValue(shape, out var list)) return;
            foreach (var (r, i) in list) r.SetBlendShapeWeight(i, weight);
        }
    }
}
