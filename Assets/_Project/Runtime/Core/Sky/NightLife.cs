// What the city has lit at an hour (D-13). Pure, so it can be tested. Each share follows a day's curve in the local clock, scaled by how dark it is, so lights
// come on through dusk and thin out through the night with no step anywhere. The shaders turn the shares into individual windows, shops and signs by hash:
// a window has a rank of its own and is on while the share is above its rank.
namespace Dad.Core
{
    public struct NightLights
    {
        /// <summary>The share of windows lit, how far the street lamps are on, the share of shops open, the share of lit signs on: each 0 to 1.</summary>
        public float Windows, Lamps, Shops, Signs;
    }

    public static class NightLife
    {
        // Hour, share; the last hour is the first plus 24. Offices and most homes in the evening; a few rooms all night; shops until about eleven.
        static readonly float[] WindowCurve = { 0, 0.20f, 2, 0.10f, 4.5f, 0.09f, 6, 0.22f, 9, 0.30f, 17, 0.42f, 19.5f, 0.62f, 21.5f, 0.60f, 23, 0.38f, 24, 0.20f };
        static readonly float[] ShopCurve = { 0, 0.14f, 5, 0.12f, 7, 0.40f, 10, 1, 21, 1, 22.5f, 0.70f, 23.5f, 0.20f, 24, 0.14f };
        static readonly float[] SignCurve = { 0, 0.40f, 1.5f, 0.22f, 5, 0.22f, 6.5f, 0.60f, 10, 1, 23, 1, 24, 0.40f };

        /// <summary>The share a curve gives at an hour, 0 to 24.</summary>
        public static float Track(float[] curve, double hours)
        {
            float h = (float)hours;
            for (int i = 2; i < curve.Length; i += 2)
                if (h <= curve[i])
                {
                    float t = (h - curve[i - 2]) / (curve[i] - curve[i - 2]);
                    return curve[i - 1] + (curve[i + 1] - curve[i - 1]) * t;
                }
            return curve[curve.Length - 1];
        }

        static float Smooth(float a, float b, float x)
        {
            float t = (x - a) / (b - a);
            t = t < 0 ? 0 : t > 1 ? 1 : t;
            return t * t * (3 - 2 * t);
        }

        /// <summary>The lights at a local hour with the sky this dark (SkyState.Night).</summary>
        public static NightLights At(double hours, float night)
        {
            float dark = Smooth(0.12f, 0.7f, night);
            return new NightLights
            {
                Windows = Track(WindowCurve, hours) * dark,
                Lamps = Smooth(0.28f, 0.5f, night),
                Shops = Track(ShopCurve, hours) * dark,
                Signs = Track(SignCurve, hours) * dark,
            };
        }
    }
}
