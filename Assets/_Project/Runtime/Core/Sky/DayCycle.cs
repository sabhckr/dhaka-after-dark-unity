// The clock and the light (D-03, ED-6). Holds a local date and time in Bangladesh and the weather.
// Each frame it places the sun and moon (Celestial), evaluates SkyModel, and sets the scene to
// match:
// - the two directional lights; the main one, which casts shadows, is RenderSettings.sun;
// - trilight ambient and fog;
// - the default reflection, a small cubemap of the sky (SkyReflection);
// - the sky shader's globals, _DadNight, and _DadLights, what the city has lit (NightLife, D-13).
//
// GameRoot creates it on the scene's directional light. The clock stands still unless
// MinutesPerSecond is set, as in plain play, so golden views and the bench see a fixed moment.
using System;
using Dad.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace Dad.Core
{
    public sealed class DayCycle : MonoBehaviour
    {
        /// <summary>Act 1 is the dry season; on this date a waxing gibbous moon stands high in the evening.</summary>
        public static readonly DateTime DefaultDate = new DateTime(2026, 1, 28);
        public const double DefaultHours = 12;
        /// <summary>Plain play: a 48-minute day.</summary>
        public const float PlayMinutesPerSecond = 0.5f;

        public DateTime Date = DefaultDate;
        /// <summary>Local time (Bangladesh Standard Time) in hours, 0 to 24.</summary>
        public double Hours = DefaultHours;
        /// <summary>Game minutes per real second; 0 stops the clock.</summary>
        public float MinutesPerSecond;
        public Weather Weather = Weather.Clear;

        public Light Sun, Moon;
        public SkyState State { get; private set; }
        /// <summary>What the city has lit now (D-13).</summary>
        public NightLights Lights { get; private set; }

        Material sky;
        Cubemap reflection;
        /// <summary>The sky the reflection shows, and the one being filled into it a face a frame (6: none).</summary>
        SkyState reflected, refilling;
        int nextFace = 6;
        bool reflectionDirty = true;

        static readonly int SunDirId = Shader.PropertyToID("_DadSunDir"), MoonDirId = Shader.PropertyToID("_DadMoonDir"),
            SunLightId = Shader.PropertyToID("_DadSunLight"), MoonLightId = Shader.PropertyToID("_DadMoonLight"),
            ZenithId = Shader.PropertyToID("_DadZenith"), HorizonId = Shader.PropertyToID("_DadHorizon"), HorizonAwayId = Shader.PropertyToID("_DadHorizonAway"),
            CloudsId = Shader.PropertyToID("_DadClouds"), CloudLitId = Shader.PropertyToID("_DadCloudLit"), CloudShadeId = Shader.PropertyToID("_DadCloudShade"),
            StarRotationId = Shader.PropertyToID("_DadStarRotation"), NightId = Shader.PropertyToID("_DadNight"), LightsId = Shader.PropertyToID("_DadLights");

        /// <summary>Adds the cycle to a scene: the given light becomes the sun, and a moon light is made beside it.</summary>
        public static DayCycle Create(Light sun)
        {
            var host = new GameObject("DayCycle");
            var c = host.AddComponent<DayCycle>();
            c.Sun = sun;
            var moon = new GameObject("Moon").AddComponent<Light>();
            moon.transform.SetParent(host.transform, false);
            moon.type = LightType.Directional;
            moon.shadows = sun != null ? sun.shadows : LightShadows.Soft;
            c.Moon = moon;
            c.reflection = SkyReflection.Create();
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = c.reflection;
            c.sky = Resources.Load<Material>("Materials/Sky");
            if (c.sky == null) Debug.LogError("[Dad] Resources/Materials/Sky missing");
            else RenderSettings.skybox = c.sky;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear; // Locked by the owner's brief of 2026-11-06.
            c.Apply();
            return c;
        }

        /// <summary>Sets the moment, as a local date and time.</summary>
        public void Set(DateTime date, double hours, Weather weather)
        {
            Date = date.Date;
            Hours = hours;
            Weather = weather;
            Normalise();
            reflectionDirty = true; // a jump in time: refill the reflection whole
            Apply();
        }

        void LateUpdate()
        {
            if (MinutesPerSecond != 0)
            {
                Hours += Time.deltaTime * MinutesPerSecond / 60.0;
                Normalise();
            }
            Apply();
        }

        void Normalise()
        {
            while (Hours >= 24) { Hours -= 24; Date = Date.AddDays(1); }
            while (Hours < 0) { Hours += 24; Date = Date.AddDays(-1); }
        }

        /// <summary>True when the sky reflections show the current sky.</summary>
        public bool ReflectionsReady => reflection == null || (!reflectionDirty && nextFace == 6 && !Changed(reflected, State));

        /// <summary>Julian day of the current moment.</summary>
        public double JulianDay => Celestial.JulianDayLocal(Date.Year, Date.Month, Date.Day, Hours);

        /// <summary>Places the sun and moon for the current moment and sets the scene's light.</summary>
        public void Apply()
        {
            using var _ = DadMarkers.SkyApply.Auto();
            double jd = JulianDay;
            var sunH = Celestial.SunFromDhaka(jd);
            var moonH = Celestial.MoonFromDhaka(jd);
            var s = SkyModel.Evaluate(ToVector(sunH), (float)sunH.Altitude, ToVector(moonH), (float)moonH.Altitude,
                (float)Celestial.MoonIllumination(jd), WeatherSettings.Of(Weather));
            State = s;

            SetLight(Sun, s.SunDirection, s.SunLight, s.SunShadow);
            SetLight(Moon, s.MoonDirection, s.MoonLight, s.MoonShadow);
            RenderSettings.sun = s.MoonIsMain ? Moon : Sun;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = s.AmbientSky.gamma;
            RenderSettings.ambientEquatorColor = s.AmbientEquator.gamma;
            RenderSettings.ambientGroundColor = s.AmbientGround.gamma;
            RenderSettings.fogColor = s.FogColour.gamma;
            RenderSettings.fogDensity = s.FogDensity;
            RenderSettings.fogStartDistance = 0.05f / s.FogDensity; // SkyModel gives the haze as a density; linear fog needs it as a start and an end.
            RenderSettings.fogEndDistance = 1.43f / s.FogDensity;

            Shader.SetGlobalVector(SunDirId, s.SunDirection);
            Shader.SetGlobalVector(MoonDirId, s.MoonDirection);
            Shader.SetGlobalVector(SunLightId, (Vector4)s.SunLight);
            Shader.SetGlobalVector(MoonLightId, (Vector4)s.MoonLight);
            Shader.SetGlobalVector(ZenithId, (Vector4)s.Zenith);
            Shader.SetGlobalVector(HorizonId, (Vector4)s.Horizon);
            Shader.SetGlobalVector(HorizonAwayId, (Vector4)s.HorizonAway);
            // Clouds drift 8 km a game hour to the east-north-east. The offset is a function of the
            // clock, so a fixed moment always shows the same clouds; each component wraps at 960 km,
            // a whole number of the sky shader's noise periods (12 and 3 km), so it never jumps.
            double drift = (jd - 2451545.0) * 24 * 8;
            Shader.SetGlobalVector(CloudsId, new Vector4(s.CloudCover, (float)(drift * 0.93 % 960), (float)(drift * 0.37 % 960), s.Stars));
            Shader.SetGlobalVector(CloudLitId, (Vector4)s.CloudLit);
            Shader.SetGlobalVector(CloudShadeId, (Vector4)s.CloudShade);
            Shader.SetGlobalMatrix(StarRotationId, StarRotation(jd));
            Shader.SetGlobalFloat(NightId, s.Night);
            var lit = NightLife.At(Hours, s.Night);
            Lights = lit;
            Shader.SetGlobalVector(LightsId, new Vector4(lit.Windows, lit.Lamps, lit.Shops, lit.Signs));

            UpdateReflections(s);
        }

        static Vector3 ToVector(Horizontal h)
        {
            var (x, y, z) = h.Direction();
            return new Vector3((float)x, (float)y, (float)z);
        }

        /// <summary>A directional light shining from a body: linear colour times intensity, split into Unity's colour and intensity.</summary>
        static void SetLight(Light light, Vector3 towards, Color linear, float shadowStrength)
        {
            if (light == null) return;
            float intensity = Mathf.Max(linear.r, Mathf.Max(linear.g, linear.b));
            light.enabled = intensity > 1e-5f;
            if (!light.enabled) return;
            light.intensity = intensity;
            light.color = (linear / intensity).gamma;
            light.shadowStrength = shadowStrength;
            light.transform.rotation = Quaternion.LookRotation(-towards, Mathf.Abs(towards.y) > 0.999f ? Vector3.forward : Vector3.up);
        }

        /// <summary>World to equatorial (x towards right ascension 0, z towards the celestial pole), for the stars.</summary>
        public static Matrix4x4 StarRotation(double jd)
        {
            double lst = (Celestial.SiderealTime(jd) + Celestial.DhakaLongitude) * Celestial.Rad;
            double phi = Celestial.DhakaLatitude * Celestial.Rad;
            var pole = new Vector3(0, (float)Math.Sin(phi), (float)Math.Cos(phi));
            var south = new Vector3(0, (float)Math.Cos(phi), -(float)Math.Sin(phi)); // the equator on the meridian
            var west = new Vector3(-1, 0, 0);
            float c = (float)Math.Cos(lst), sn = (float)Math.Sin(lst);
            Vector3 x = c * south + sn * west, y = sn * south - c * west;
            var m = Matrix4x4.identity;
            m.SetRow(0, x);
            m.SetRow(1, y);
            m.SetRow(2, pole);
            return m;
        }

        void UpdateReflections(SkyState s)
        {
            if (reflection == null) return;
            if (reflectionDirty)
            {
                // At the start and after a jump in time: the whole sky at once (about 1 ms).
                SkyReflection.Fill(reflection, s);
                reflected = s;
                reflectionDirty = false;
                nextFace = 6;
                return;
            }
            if (nextFace == 6)
            {
                // Refilled when the sky has moved on enough to see: about every game minute at dusk.
                if (!Changed(reflected, s)) return;
                refilling = s;
                nextFace = 0;
            }
            // As the clock runs: one face a frame, all six from one moment, uploaded together.
            SkyReflection.FillFace(reflection, (CubemapFace)nextFace, refilling);
            if (++nextFace < 6) return;
            SkyReflection.Upload(reflection);
            reflected = refilling;
        }

        void OnDestroy()
        {
            if (reflection != null) Destroy(reflection);
        }

        static bool Changed(SkyState a, SkyState b)
        {
            float d = Mathf.Abs(a.Zenith.r - b.Zenith.r) + Mathf.Abs(a.Zenith.g - b.Zenith.g) + Mathf.Abs(a.Zenith.b - b.Zenith.b)
                + Mathf.Abs(a.Horizon.r - b.Horizon.r) + Mathf.Abs(a.Horizon.g - b.Horizon.g) + Mathf.Abs(a.Horizon.b - b.Horizon.b)
                + Vector3.Distance(a.SunDirection, b.SunDirection) + Mathf.Abs(a.CloudCover - b.CloudCover);
            return d > 0.004f;
        }
    }
}
