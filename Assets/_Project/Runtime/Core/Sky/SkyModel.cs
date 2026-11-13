// The light of a moment (D-03, ED-6): from where the sun and moon stand and the weather, every
// colour and level the scene needs: the two directional lights, the sky's zenith and horizon,
// ambient (trilight), fog, clouds, stars and a night factor. A pure function of its inputs, with
// no presets: each value is a curve over the sun's altitude, so the light changes as smoothly as
// the sun moves (DayCycleTests checks a whole day minute by minute).
//
// The sky colours are keyed for Dhaka's hazy dry season and its city glow at night. The sun's
// colour is the transmittance of an air mass (Kasten and Young) through Rayleigh scattering and a
// winter aerosol load, so the sun reddens and dims near the horizon without hand-tuned keys.
// Colours are linear. Intensities suit a scene with no exposure or tone mapping: the night is
// lighter than a real one, so streets stay readable before D-13 brings street lights.
using UnityEngine;

namespace Dad.Core
{
    public enum Weather { Clear, Cloudy, Overcast }

    public readonly struct WeatherSettings
    {
        public readonly float CloudCover, SunTransmit, AmbientGain, Grey, FogGain, ShadowStrength;

        public WeatherSettings(float cover, float sun, float ambient, float grey, float fog, float shadow)
        {
            CloudCover = cover; SunTransmit = sun; AmbientGain = ambient; Grey = grey; FogGain = fog; ShadowStrength = shadow;
        }

        public static WeatherSettings Of(Weather w) => w switch
        {
            Weather.Cloudy => new WeatherSettings(0.55f, 0.8f, 1.1f, 0.3f, 1.2f, 0.85f),
            Weather.Overcast => new WeatherSettings(0.95f, 0.22f, 1.35f, 0.75f, 1.6f, 0.3f),
            _ => new WeatherSettings(0.15f, 1f, 1f, 0f, 1f, 1f),
        };

        public static WeatherSettings Lerp(WeatherSettings a, WeatherSettings b, float t) => new WeatherSettings(
            Mathf.Lerp(a.CloudCover, b.CloudCover, t), Mathf.Lerp(a.SunTransmit, b.SunTransmit, t), Mathf.Lerp(a.AmbientGain, b.AmbientGain, t),
            Mathf.Lerp(a.Grey, b.Grey, t), Mathf.Lerp(a.FogGain, b.FogGain, t), Mathf.Lerp(a.ShadowStrength, b.ShadowStrength, t));
    }

    public struct SkyState
    {
        /// <summary>Unit vectors towards the sun and the moon (x east, y up, z north), and their altitudes in degrees.</summary>
        public Vector3 SunDirection, MoonDirection;
        public float SunAltitude, MoonAltitude, MoonLit;
        /// <summary>Colour times intensity of each directional light, and its shadow strength.</summary>
        public Color SunLight, MoonLight;
        public float SunShadow, MoonShadow;
        /// <summary>True while the moon is the main (shadowing) light.</summary>
        public bool MoonIsMain;
        public Color Zenith, Horizon, HorizonAway;
        public Color AmbientSky, AmbientEquator, AmbientGround;
        public Color FogColour;
        public float FogDensity;
        public float CloudCover;
        public Color CloudLit, CloudShade;
        public float Stars, Night;
    }

    public static class SkyModel
    {
        /// <summary>Exp-squared fog density in the clear day (D-01's value).</summary>
        public const float BaseFogDensity = 0.0011f;
        /// <summary>The sun hands the main light to the moon below this altitude (degrees), where its own light is already zero.</summary>
        public const float HandOverAltitude = -2f;

        // Clear-sky colours over the sun's altitude (degrees): zenith and horizon, sRGB.
        static readonly (float alt, uint zenith, uint horizon)[] SkyKeys =
        {
            (-90, 0x06080e, 0x3a2a20),
            (-14, 0x070a14, 0x3a2a20), // the city's glow at the horizon
            (-10, 0x0d1224, 0x3a2a28),
            (-6, 0x1d2540, 0x5c3c38),
            (-3, 0x33405f, 0xa96248),
            (0, 0x485878, 0xd98458),
            (2, 0x56688f, 0xe8a070),
            (6, 0x667891, 0xd8bb98),
            (15, 0x667d98, 0xccc8bd),
            (40, 0x5f7a9b, 0xc4c5c3),
            (90, 0x566f93, 0xbcbfc0),
        };

        // Optical depths at the zenith for red, green and blue: Rayleigh, and a dry-season aerosol
        // load (Angstrom exponent about 1.3).
        static readonly Vector3 Rayleigh = new Vector3(0.040f, 0.095f, 0.215f);
        static readonly Vector3 Aerosol = new Vector3(0.106f, 0.14f, 0.18f);

        const float SunNoon = 1.15f, MoonFull = 0.12f;
        static readonly Color MoonTint = new Color(0.62f, 0.72f, 1f);
        // A floor under the night's ambient (sky and city glow), so streets stay readable.
        static readonly Color NightSky = new Color(0.048f, 0.054f, 0.076f), NightEquator = new Color(0.054f, 0.047f, 0.045f);
        const float GroundAlbedo = 0.18f;

        public static SkyState Evaluate(Vector3 sunDirection, float sunAltitude, Vector3 moonDirection, float moonAltitude, float moonLit, WeatherSettings w)
        {
            var s = new SkyState
            {
                SunDirection = sunDirection, SunAltitude = sunAltitude,
                MoonDirection = moonDirection, MoonAltitude = moonAltitude, MoonLit = moonLit,
            };
            s.Night = 1 - Smooth(-10, 3, sunAltitude);

            // Direct light: transmittance through the air mass, faded as the disc sets.
            s.SunLight = Transmit(sunAltitude) * (SunNoon * w.SunTransmit * Smooth(-1.2f, 0.6f, sunAltitude));
            // Moonlight counts only once the sun is down; by day the moon light is off.
            s.MoonLight = Transmit(moonAltitude) * MoonTint * (MoonFull * Mathf.Pow(Mathf.Clamp01(moonLit), 1.5f) * Mathf.Lerp(1, w.SunTransmit, 0.8f)
                * Smooth(-1.2f, 0.6f, moonAltitude) * (1 - Smooth(HandOverAltitude, 4, sunAltitude)));
            s.MoonIsMain = sunAltitude < HandOverAltitude;
            // The moon's shadows come in after the hand-over, so the switch itself changes nothing.
            s.SunShadow = w.ShadowStrength;
            s.MoonShadow = 0.6f * w.ShadowStrength * Smooth(HandOverAltitude, HandOverAltitude - 6, sunAltitude);

            // Sky colours, greyed by cloud, and lit by the moon at night.
            SkyColours(sunAltitude, out var zenith, out var horizon);
            Color moonSky = s.MoonLight * 0.25f;
            zenith += moonSky;
            horizon += moonSky;
            zenith = Grey(zenith, w.Grey);
            horizon = Grey(horizon, w.Grey);
            s.Zenith = zenith;
            s.Horizon = horizon;
            // Away from a low sun the horizon takes the zenith's cooler colour.
            float low = 1 - Smooth(0, 15, sunAltitude);
            s.HorizonAway = Color.Lerp(horizon, Color.Lerp(zenith, horizon, 0.35f), low * 0.85f * (1 - w.Grey));

            // Ambient trilight: the sky above, the horizon around, light bounced from the ground below.
            float sunUp = Mathf.Max(0, sunDirection.y);
            s.AmbientSky = (Color.Lerp(zenith, s.HorizonAway, 0.3f) * 0.85f + s.MoonLight * 0.12f) * w.AmbientGain + NightSky * s.Night;
            s.AmbientEquator = Color.Lerp(s.HorizonAway, horizon, 0.3f) * 0.75f * w.AmbientGain + NightEquator * s.Night;
            s.AmbientGround = (s.SunLight * sunUp + s.AmbientSky) * GroundAlbedo;

            // Haze: the horizon's colour, mostly the side away from the sun; thicker looking with a low sun.
            s.FogColour = Color.Lerp(s.HorizonAway, horizon, 0.35f);
            s.FogDensity = BaseFogDensity * w.FogGain * (1 + 0.25f * (1 - Smooth(0, 20, sunAltitude)));

            s.CloudCover = w.CloudCover;
            s.CloudLit = s.SunLight * 0.55f + s.AmbientSky * 0.9f + s.MoonLight * 0.4f;
            s.CloudShade = s.AmbientSky * 0.8f + horizon * 0.15f + SkyKey(-90).horizon * (0.8f * s.Night); // lit from below by the city at night
            // Dhaka's sky glow leaves only the brightest stars.
            s.Stars = Smooth(-7, -14, sunAltitude) * (1 - w.CloudCover) * 0.22f;
            return s;
        }

        /// <summary>Clear-sky zenith and horizon colours (linear) at a sun altitude.</summary>
        public static void SkyColours(float sunAltitude, out Color zenith, out Color horizon)
        {
            int i = 1;
            while (i < SkyKeys.Length - 1 && SkyKeys[i].alt < sunAltitude) i++;
            var a = SkyKeys[i - 1];
            var b = SkyKeys[i];
            float t = Smooth(a.alt, b.alt, sunAltitude);
            zenith = Color.Lerp(Linear(a.zenith), Linear(b.zenith), t);
            horizon = Color.Lerp(Linear(a.horizon), Linear(b.horizon), t);
        }

        static (Color zenith, Color horizon) SkyKey(float alt)
        {
            SkyColours(alt, out var z, out var h);
            return (z, h);
        }

        /// <summary>Kasten and Young's relative air mass at an altitude in degrees (held at -1 below that).</summary>
        public static float AirMass(float altitude)
        {
            float h = Mathf.Max(altitude, -1f);
            return 1f / (Mathf.Sin(h * Mathf.Deg2Rad) + 0.50572f * Mathf.Pow(h + 6.07995f, -1.6364f));
        }

        /// <summary>Transmittance of the light from a body at this altitude, relative to the zenith.</summary>
        public static Color Transmit(float altitude)
        {
            float m = AirMass(altitude) - 1;
            Vector3 tau = Rayleigh + Aerosol;
            return new Color(Mathf.Exp(-tau.x * m), Mathf.Exp(-tau.y * m), Mathf.Exp(-tau.z * m));
        }

        static Color Grey(Color c, float amount)
        {
            float l = 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
            return Color.Lerp(c, new Color(l, l, l) * 1.05f, amount);
        }

        static Color Linear(uint hex) => new Color(((hex >> 16) & 255) / 255f, ((hex >> 8) & 255) / 255f, (hex & 255) / 255f).linear;

        /// <summary>Smoothstep from edge a to edge b (either order).</summary>
        static float Smooth(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3 - 2 * t);
        }
    }
}
