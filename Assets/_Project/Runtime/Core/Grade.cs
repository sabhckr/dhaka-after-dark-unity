// The picture's look (owner's brief, sections 13.4 and 13.6): one global volume whose values follow the sky. GradeModel says what they are, so the
// look can be tested; every value is a blend, so nothing steps as the day turns. Day is a little desaturated with warm midtones, a low sun warmer still, and
// night cool in its ambient with the warm lights left warm. Bloom is for real lights only (its threshold is above anything the sun lights) and Low has none.
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Dad.Core
{
    public struct GradeValues
    {
        public float Exposure, Contrast, Saturation, Temperature, Bloom, Vignette;
        public Color Shadows, Midtones, Highlights;
    }

    public static class GradeModel
    {
        static float Smooth(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3 - 2 * t);
        }

        /// <summary>How far the sun is into its low warm hours: 0 at night and high by day.</summary>
        public static float GoldenHour(float sunAltitude) => Smooth(-4, 3, sunAltitude) * (1 - Smooth(6, 22, sunAltitude));

        public static GradeValues At(float night, float sunAltitude)
        {
            float gold = GoldenHour(sunAltitude), day = 1 - night;
            return new GradeValues
            {
                Exposure = -0.08f * day + 0.18f * night,
                Contrast = 11 * day + 7 * night,
                Saturation = Mathf.Lerp(-26, -15, gold) * day - 16 * night,
                Temperature = Mathf.Lerp(3, 7, gold) * day - 5 * night,
                Bloom = 0.05f * day + 0.20f * night,
                Vignette = 0.08f * day + 0.12f * night,
                Shadows = new Color(1 - 0.03f * night, 1, 1 + 0.05f * night, 0),
                Midtones = new Color(1 + 0.012f * day, 1, 1 - 0.012f * day, 0),
                Highlights = new Color(1 + 0.01f, 1, 1, 0),
            };
        }
    }

    public sealed class Grade : MonoBehaviour
    {
        public DayCycle Day;
        ColorAdjustments colour;
        WhiteBalance white;
        ShadowsMidtonesHighlights tones;
        Bloom bloom;
        Vignette vignette;
        VolumeProfile profile;
        Volume volume;
        static readonly int DebugId = Shader.PropertyToID("_DadWorldDebug");

        public static Grade Create(DayCycle day)
        {
            var go = new GameObject("Grade");
            go.transform.SetParent(day.transform, false);
            var g = go.AddComponent<Grade>();
            g.Day = day;
            g.profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var v = go.AddComponent<Volume>();
            v.isGlobal = true;
            v.priority = 10;
            v.sharedProfile = g.profile;
            g.volume = v;
            var tone = g.profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.Neutral);
            g.colour = g.profile.Add<ColorAdjustments>(true);
            g.white = g.profile.Add<WhiteBalance>(true);
            g.tones = g.profile.Add<ShadowsMidtonesHighlights>(true);
            g.bloom = g.profile.Add<Bloom>(true);
            g.bloom.threshold.Override(1.05f);
            g.bloom.scatter.Override(0.62f);
            g.bloom.highQualityFiltering.Override(false);
            g.vignette = g.profile.Add<Vignette>(true);
            g.vignette.smoothness.Override(0.5f);
            g.vignette.color.Override(new Color(0.02f, 0.02f, 0.03f));
            g.Apply();
            return g;
        }

        void LateUpdate() => Apply();

        public void Apply()
        {
            if (Day == null) return;
            // The debug views show exact colours.
            volume.weight = Shader.GetGlobalFloat(DebugId) > 0 ? 0 : 1;
            var s = Day.State;
            var v = GradeModel.At(s.Night, s.SunAltitude);
            colour.postExposure.value = v.Exposure;
            colour.contrast.value = v.Contrast;
            colour.saturation.value = v.Saturation;
            white.temperature.value = v.Temperature;
            tones.shadows.value = new Vector4(v.Shadows.r, v.Shadows.g, v.Shadows.b, 0);
            tones.midtones.value = new Vector4(v.Midtones.r, v.Midtones.g, v.Midtones.b, 0);
            tones.highlights.value = new Vector4(v.Highlights.r, v.Highlights.g, v.Highlights.b, 0);
            bloom.active = GraphicsTier.Current != Tier.Low;
            bloom.intensity.value = v.Bloom;
            vignette.intensity.value = v.Vignette;
        }

        void OnDestroy()
        {
            if (profile != null) Destroy(profile);
        }
    }
}
