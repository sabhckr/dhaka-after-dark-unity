// D-03 light of the day: SkyModel has no steps (every value moves a bounded amount from one game
// minute to the next, and no more than the minutes around it, through a whole day in each
// weather); the sun hands the main light to the moon only when its own light is gone and the
// moon's shadows are not yet in; noon is day and midnight is night; the sun reddens towards the
// horizon; and the sky reflection cubemap's faces are laid out the way the GPU reads them, and hold
// the sky.
using System;
using Dad.Core;
using Dad.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Dad.Tests
{
    public class SkyModelTests
    {
        static SkyState At(DateTime date, double hours, Weather weather)
        {
            double jd = Celestial.JulianDayLocal(date.Year, date.Month, date.Day, hours);
            var sun = Celestial.SunFromDhaka(jd);
            var moon = Celestial.MoonFromDhaka(jd);
            return SkyModel.Evaluate(V(sun), (float)sun.Altitude, V(moon), (float)moon.Altitude, (float)Celestial.MoonIllumination(jd), WeatherSettings.Of(weather));
        }

        static Vector3 V(Horizontal h)
        {
            var (x, y, z) = h.Direction();
            return new Vector3((float)x, (float)y, (float)z);
        }

        static float Delta(Color a, Color b) => Mathf.Max(Mathf.Abs(a.r - b.r), Mathf.Max(Mathf.Abs(a.g - b.g), Mathf.Abs(a.b - b.b)));

        [Test]
        public void ADayHasNoSteps()
        {
            // Two checks on every value, minute by minute through a day in each weather. The change
            // in one game minute is bounded: at most 1/20 of the full range for a colour, so at the
            // play speed (half a game minute a second, a frame is 1/120 of a minute) under 0.0005 a
            // frame. The bound sits above the dawn horizon, the fastest real change (it brightens
            // about 0.03 a minute between the -3 and 0 degree keys). And no minute changes much more
            // than the ten minutes around it, which is what a step would look like.
            var limits = new (string name, Func<SkyState, SkyState, float> delta, float max, float floor)[]
            {
                ("sun light", (a, b) => Delta(a.SunLight, b.SunLight), 0.05f, 0.004f),
                ("moon light", (a, b) => Delta(a.MoonLight, b.MoonLight), 0.05f, 0.004f),
                ("zenith", (a, b) => Delta(a.Zenith, b.Zenith), 0.05f, 0.004f),
                ("horizon", (a, b) => Delta(a.Horizon, b.Horizon), 0.05f, 0.004f),
                ("horizon away", (a, b) => Delta(a.HorizonAway, b.HorizonAway), 0.05f, 0.004f),
                ("ambient sky", (a, b) => Delta(a.AmbientSky, b.AmbientSky), 0.05f, 0.004f),
                ("ambient equator", (a, b) => Delta(a.AmbientEquator, b.AmbientEquator), 0.05f, 0.004f),
                ("ambient ground", (a, b) => Delta(a.AmbientGround, b.AmbientGround), 0.05f, 0.004f),
                ("fog colour", (a, b) => Delta(a.FogColour, b.FogColour), 0.05f, 0.004f),
                ("fog density", (a, b) => Mathf.Abs(a.FogDensity - b.FogDensity) / SkyModel.BaseFogDensity, 0.03f, 0.003f),
                ("night", (a, b) => Mathf.Abs(a.Night - b.Night), 0.03f, 0.003f),
                ("stars", (a, b) => Mathf.Abs(a.Stars - b.Stars), 0.03f, 0.003f),
                ("cloud lit", (a, b) => Delta(a.CloudLit, b.CloudLit), 0.05f, 0.004f),
                ("cloud shade", (a, b) => Delta(a.CloudShade, b.CloudShade), 0.05f, 0.004f),
                ("sun shadow", (a, b) => Mathf.Abs(a.SunShadow - b.SunShadow), 0.05f, 0.005f),
                ("moon shadow", (a, b) => Mathf.Abs(a.MoonShadow - b.MoonShadow), 0.05f, 0.005f),
            };
            const int Minutes = 24 * 60;
            foreach (Weather weather in Enum.GetValues(typeof(Weather)))
            {
                var states = new SkyState[Minutes + 1];
                for (int m = 0; m <= Minutes; m++) states[m] = At(DayCycle.DefaultDate, m / 60.0, weather);
                float worstRate = 0, worstSpike = 0;
                string rateAt = null, spikeAt = null;
                var deltas = new float[Minutes];
                var around = new System.Collections.Generic.List<float>();
                foreach (var (name, delta, max, floor) in limits)
                {
                    for (int m = 0; m < Minutes; m++) deltas[m] = delta(states[m], states[m + 1]);
                    for (int m = 0; m < Minutes; m++)
                    {
                        string Where(float d) => $"{name} from {m / 60:00}:{m % 60:00} ({d:F4})";
                        if (deltas[m] / max > worstRate) { worstRate = deltas[m] / max; rateAt = Where(deltas[m]); }
                        around.Clear();
                        for (int j = m - 5; j <= m + 5; j++)
                            if (j != m && j >= 0 && j < Minutes) around.Add(deltas[j]);
                        around.Sort();
                        float spike = deltas[m] / (3 * around[around.Count / 2] + floor);
                        if (spike > worstSpike) { worstSpike = spike; spikeAt = Where(deltas[m]); }
                    }
                }
                Debug.Log($"Day without steps, {weather}: fastest {worstRate:P0} of its bound, {rateAt}; largest against its neighbours {worstSpike:P0} of the limit, {spikeAt}");
                Assert.LessOrEqual(worstRate, 1f, $"{weather}: {rateAt}");
                Assert.LessOrEqual(worstSpike, 1f, $"{weather}: a step, {spikeAt}");
            }
        }

        [Test]
        public void TheMoonTakesOverOnlyWhenTheSunIsGone()
        {
            var d = DayCycle.DefaultDate;
            int switches = 0;
            var prev = At(d, 12, Weather.Clear);
            for (int minute = 12 * 60 + 1; minute <= 36 * 60; minute++)
            {
                var s = At(d, minute / 60.0, Weather.Clear);
                if (s.MoonIsMain != prev.MoonIsMain)
                {
                    switches++;
                    // Either side of the switch, the sun gives no light and the moon casts no shadow.
                    foreach (var x in new[] { prev, s })
                    {
                        Assert.Less(Mathf.Max(x.SunLight.r, Mathf.Max(x.SunLight.g, x.SunLight.b)), 1e-3f, $"sun light at the switch, {minute / 60.0:F2} h");
                        Assert.Less(x.MoonShadow, 0.02f, $"moon shadow at the switch, {minute / 60.0:F2} h");
                    }
                }
                prev = s;
            }
            Assert.AreEqual(2, switches, "one hand-over at dusk, one back at dawn");
        }

        [Test]
        public void NoonIsDayAndMidnightIsNight()
        {
            var d = DayCycle.DefaultDate;
            var noon = At(d, 12, Weather.Clear);
            var midnight = At(d, 0, Weather.Clear);
            var evening = At(d, 21, Weather.Clear); // the moon is high
            Assert.AreEqual(0f, noon.Night, 1e-4f);
            Assert.AreEqual(1f, midnight.Night, 1e-4f);
            Assert.Greater(noon.SunLight.g, 1f);
            Assert.AreEqual(0f, midnight.SunLight.g);
            Assert.IsFalse(noon.MoonIsMain);
            Assert.IsTrue(evening.MoonIsMain);
            Assert.Greater(evening.MoonLight.b, 0.03f, "moonlight in the evening");
            Assert.AreEqual(0f, noon.MoonLight.b, "no moon light by day");
            Assert.Greater(At(d, 15, Weather.Clear).SunLight.g, At(d, 15, Weather.Overcast).SunLight.g * 3, "overcast dims the sun");
        }

        [Test]
        public void TheSunReddensTowardsTheHorizon()
        {
            float prevRatio = float.MaxValue;
            foreach (float alt in new[] { 60f, 30f, 15f, 8f, 4f, 2f, 0.5f })
            {
                var t = SkyModel.Transmit(alt);
                float ratio = t.b / t.r;
                Assert.Less(ratio, prevRatio, $"bluer than at the altitude above, at {alt}°");
                prevRatio = ratio;
            }
            Assert.AreEqual(1f, SkyModel.AirMass(90), 0.01f);
            Assert.AreEqual(2f, SkyModel.AirMass(30), 0.02f);
        }

        /// <summary>Reads a cubemap on the GPU in one direction, through Hidden/Dad/CubeSample.</summary>
        sealed class CubeReader : IDisposable
        {
            readonly Material mat = new Material(AssetDatabase.LoadAssetAtPath<Shader>("Assets/_Project/Editor/CubeSample.shader"));
            readonly RenderTexture rt = RenderTexture.GetTemporary(1, 1, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            readonly Texture2D read = new Texture2D(1, 1, TextureFormat.RGBAFloat, false, true);

            public CubeReader(Cubemap cube) => mat.SetTexture("_Cube", cube);

            public Color Read(Vector3 dir)
            {
                mat.SetVector("_Dir", dir);
                Graphics.Blit(null, rt, mat);
                RenderTexture.active = rt;
                read.ReadPixels(new Rect(0, 0, 1, 1), 0, 0);
                RenderTexture.active = null;
                return read.GetPixel(0, 0);
            }

            public void Dispose()
            {
                RenderTexture.ReleaseTemporary(rt);
                Object.DestroyImmediate(read);
                Object.DestroyImmediate(mat);
            }
        }

        [Test]
        public void SkyReflectionFacesAreLaidOutAsTheGpuReadsThem()
        {
            // Fill the cubemap with the direction itself as the colour, and read it back on the GPU
            // at each face's centre and four points off it: each reading is its own direction, and
            // not the direction mirrored across either of the face's axes, so a face that is
            // swapped, upside down or the wrong way round fails.
            static Color Field(Vector3 d) => new Color(d.x * 0.5f + 0.5f, d.y * 0.5f + 0.5f, d.z * 0.5f + 0.5f);
            var cube = SkyReflection.Create();
            var reader = new CubeReader(cube);
            try
            {
                SkyReflection.Fill(cube, Field);
                for (int major = 0; major < 3; major++)
                foreach (float sign in new[] { 1f, -1f })
                foreach (var (a, b) in new[] { (0f, 0f), (0.6f, 0.3f), (-0.5f, 0.6f), (0.4f, -0.7f), (-0.6f, -0.4f) })
                {
                    var raw = Vector3.zero;
                    raw[major] = sign;
                    raw[(major + 1) % 3] = a;
                    raw[(major + 2) % 3] = b;
                    var dir = raw.normalized;
                    var got = reader.Read(dir);
                    float error = Delta(got, Field(dir));
                    Assert.Less(error, 0.01f, $"direction {raw}: GPU {got}, expected {Field(dir)}");
                    for (int axis = 0; axis < 3; axis++)
                    {
                        if (axis == major || dir[axis] == 0) continue;
                        var mirror = dir;
                        mirror[axis] = -mirror[axis];
                        Assert.Greater(Delta(got, Field(mirror)), 0.1f, $"direction {raw} reads like its mirror across axis {axis}");
                    }
                }
            }
            finally
            {
                reader.Dispose();
                Object.DestroyImmediate(cube);
            }
        }

        [Test]
        public void SkyReflectionHoldsTheSky()
        {
            // The sky of a low sun in the morning (east-south-east) and in the evening (west-south-
            // west), read back on the GPU in two directions on each face. The directions keep clear
            // of the horizon, where the gradient has a kink that bilinear filtering of 3-degree
            // texels cannot follow.
            var cube = SkyReflection.Create();
            var reader = new CubeReader(cube);
            var dirs = new[]
            {
                new Vector3(1, 0.45f, 0.6f), new Vector3(1, -0.5f, -0.55f),
                new Vector3(-1, 0.45f, -0.6f), new Vector3(-1, -0.5f, 0.55f),
                new Vector3(0.8f, 1, 0.6f), new Vector3(-0.7f, 1, -0.8f),
                new Vector3(0.8f, -1, 0.6f), new Vector3(-0.7f, -1, -0.8f),
                new Vector3(0.6f, 0.45f, 1), new Vector3(-0.55f, -0.5f, 1),
                new Vector3(-0.6f, 0.45f, -1), new Vector3(0.55f, -0.5f, -1),
            };
            try
            {
                foreach (double hours in new[] { 7.0, 17.25 })
                {
                    var s = At(DayCycle.DefaultDate, hours, Weather.Clear);
                    Assert.That(s.SunAltitude, Is.InRange(2f, 10f), "a low sun");
                    SkyReflection.Fill(cube, s);
                    foreach (var raw in dirs)
                    {
                        var dir = raw.normalized;
                        var got = reader.Read(dir);
                        var want = SkyReflection.Radiance(dir, s);
                        float tolerance = 0.01f + 0.04f * Mathf.Max(want.r, Mathf.Max(want.g, want.b)); // 32-texel faces, half floats
                        Assert.Less(Delta(got, want), tolerance, $"{hours} h, direction {raw}: GPU {got}, CPU {want}");
                    }
                    // The sky is brighter than the haze below the horizon.
                    Assert.Greater(SkyReflection.Radiance(Vector3.up, s).b, SkyReflection.Radiance(Vector3.down, s).b);
                }
            }
            finally
            {
                reader.Dispose();
                Object.DestroyImmediate(cube);
            }
        }
    }
}
