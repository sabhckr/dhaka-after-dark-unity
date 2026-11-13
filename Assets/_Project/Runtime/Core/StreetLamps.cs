// Real lights from the nearest street lamps (D-13), on the levels that can pay for them: walls, vehicles and people near the camera catch the lamps' light, which the
// glow mesh (StreetGlow) cannot give. A small pool of point lights follows the camera: every few frames it takes the nearest lamps of the chunks loaded, and each light
// eases to full or to nothing, so a lamp never pops as it joins or leaves. Low has none.
using System;
using System.Collections.Generic;
using Dad.World;
using UnityEngine;

namespace Dad.Core
{
    public sealed class StreetLamps : MonoBehaviour
    {
        public const float Range = 28, Brightness = 13, Reach = 70, EaseSeconds = 0.5f, ScanSeconds = 0.25f;
        public static int PoolFor(Tier tier) => tier == Tier.High ? 12 : tier == Tier.Medium ? 6 : 0;

        public DayCycle Day;
        public ChunkStreamer Streamer;
        public Transform Focus;
        /// <summary>No easing and a scan every frame, for a still picture.</summary>
        public bool Instant;

        sealed class Slot { public Light Light; public float Level; public bool Wanted; public long Seed; }
        Slot[] slots = Array.Empty<Slot>();
        readonly List<(float d, Vector3 at, long seed)> near = new List<(float, Vector3, long)>(64);
        float nextScan;

        public static StreetLamps Create(DayCycle day, ChunkStreamer streamer, Transform focus)
        {
            var go = new GameObject("StreetLamps");
            var s = go.AddComponent<StreetLamps>();
            s.Day = day;
            s.Streamer = streamer;
            s.Focus = focus;
            s.Resize(PoolFor(GraphicsTier.Current));
            return s;
        }

        void Resize(int n)
        {
            slots = new Slot[n];
            for (int i = 0; i < n; i++)
            {
                var l = new GameObject("Lamp").AddComponent<Light>();
                l.transform.SetParent(transform, false);
                l.type = LightType.Point;
                l.range = Range;
                l.shadows = LightShadows.None;
                l.enabled = false;
                slots[i] = new Slot { Light = l };
            }
        }

        void LateUpdate()
        {
            if (slots.Length == 0 || Day == null) return;
            float on = Day.Lights.Lamps;
            if (on <= 0.01f)
            {
                foreach (var s in slots) { s.Level = 0; s.Wanted = false; s.Seed = 0; s.Light.enabled = false; }
                return;
            }
            if (Instant || Time.unscaledTime >= nextScan)
            {
                nextScan = Time.unscaledTime + ScanSeconds;
                Scan();
            }
            float step = Instant ? 1 : Time.unscaledDeltaTime / EaseSeconds;
            foreach (var s in slots)
            {
                s.Level = Mathf.MoveTowards(s.Level, s.Wanted ? 1 : 0, step);
                s.Light.enabled = s.Level > 0.003f;
                s.Light.intensity = Brightness * s.Level * on;
            }
        }

        /// <summary>Which lamps the slots should show: those nearest the focus. A slot already on a wanted lamp stays on it.</summary>
        void Scan()
        {
            if (Streamer == null || Focus == null) return;
            near.Clear();
            var f = Focus.position;
            foreach (var v in Streamer.Resident.Values)
            {
                var root = v.Root.transform;
                foreach (var l in v.Lamps)
                {
                    var at = root.TransformPoint((float)l.X, (float)l.Y, (float)l.Z);
                    float d = (at - f).sqrMagnitude;
                    if (d < Reach * Reach) near.Add((d, at, l.Seed));
                }
            }
            near.Sort((a, b) => a.d.CompareTo(b.d));
            int keep = Math.Min(near.Count, slots.Length);
            foreach (var s in slots) s.Wanted = false;
            for (int i = 0; i < keep; i++)
            {
                var want = near[i];
                Slot match = null;
                foreach (var s in slots) if (s.Seed == want.seed && s.Level > 0) { match = s; break; }
                if (match == null) continue;
                match.Wanted = true;
                match.Light.transform.position = want.at;
            }
            for (int i = 0; i < keep; i++)
            {
                var want = near[i];
                bool shown = false;
                foreach (var s in slots) if (s.Wanted && s.Seed == want.seed) { shown = true; break; }
                if (shown) continue;
                foreach (var s in slots)
                {
                    if (s.Wanted || s.Level > 0) continue;
                    s.Wanted = true;
                    s.Seed = want.seed;
                    s.Light.transform.position = want.at;
                    uint c = StreetGlow.ColourOf(want.seed);
                    s.Light.color = new Color32((byte)c, (byte)(c >> 8), (byte)(c >> 16), 255);
                    break;
                }
            }
        }
    }
}
