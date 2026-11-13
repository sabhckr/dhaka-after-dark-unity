// The sky in reflections (D-03): a small HDR cubemap filled on the CPU from the sky model and set as
// the scene's default reflection, so glass, water and metal reflect the sky of the hour. It holds
// the sky shader's gradient and the haze glow round the sun; clouds only grey it, and the sun and
// moon discs and the stars are left out. The sky is a smooth gradient, so 16-texel faces (5.6
// degrees a texel) are enough. The work is the radiance of each texel, about 0.7 microseconds in the
// editor: a whole fill of 6 x 16 x 16 texels takes about 1.1 ms, one face 0.2 ms, the upload with
// mips 0.08 ms (a 32-texel cube took 4.7 ms). So DayCycle fills it whole only at the start and
// after a jump in time; as the clock runs it refills one face a frame (FillFace) and uploads the six
// together (Upload).
using System;
using UnityEngine;

namespace Dad.Core
{
    public static class SkyReflection
    {
        public const int Size = 16;

        public static Cubemap Create() => new Cubemap(Size, TextureFormat.RGBAHalf, true)
        {
            name = "Dad sky reflection",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Trilinear,
        };

        static Color[] pixels;
        /// <summary>Each face's texel directions, for the cube size they were made for.</summary>
        static Vector3[][] directions;

        static Vector3[] Directions(CubemapFace face, int n)
        {
            if (directions == null || directions[0].Length != n * n)
            {
                directions = new Vector3[6][];
                for (int f = 0; f < 6; f++)
                {
                    directions[f] = new Vector3[n * n];
                    for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                        directions[f][y * n + x] = Direction((CubemapFace)f, (x + 0.5f) / n * 2 - 1, (y + 0.5f) / n * 2 - 1);
                }
            }
            return directions[(int)face];
        }

        /// <summary>Fills the whole cubemap with the sky of a moment and uploads it.</summary>
        public static void Fill(Cubemap cube, in SkyState s)
        {
            for (int f = 0; f < 6; f++) FillFace(cube, (CubemapFace)f, s);
            Upload(cube);
        }

        /// <summary>Fills one face with the sky of a moment; Upload sends the faces to the GPU.</summary>
        public static void FillFace(Cubemap cube, CubemapFace face, in SkyState s)
        {
            using var _ = DadMarkers.SkyReflection.Auto();
            int n = cube.width;
            if (pixels == null || pixels.Length != n * n) pixels = new Color[n * n];
            var dirs = Directions(face, n);
            for (int i = 0; i < dirs.Length; i++) pixels[i] = Radiance(dirs[i], s);
            cube.SetPixels(pixels, face);
        }

        /// <summary>Sends the filled faces to the GPU and makes the mips.</summary>
        public static void Upload(Cubemap cube)
        {
            using var _ = DadMarkers.SkyReflection.Auto();
            cube.Apply(true);
        }

        /// <summary>Fills the whole cubemap with any radiance over directions and uploads it (SkyModelTests checks the face layout with one).</summary>
        public static void Fill(Cubemap cube, Func<Vector3, Color> radiance)
        {
            int n = cube.width;
            if (pixels == null || pixels.Length != n * n) pixels = new Color[n * n];
            for (int f = 0; f < 6; f++)
            {
                var dirs = Directions((CubemapFace)f, n);
                for (int i = 0; i < dirs.Length; i++) pixels[i] = radiance(dirs[i]);
                cube.SetPixels(pixels, (CubemapFace)f);
            }
            Upload(cube);
        }

        /// <summary>World direction of a cubemap texel: u to the right, v down the face, as Unity lays out cubemap faces.</summary>
        public static Vector3 Direction(CubemapFace face, float u, float v) => (face switch
        {
            CubemapFace.PositiveX => new Vector3(1, -v, -u),
            CubemapFace.NegativeX => new Vector3(-1, -v, u),
            CubemapFace.PositiveY => new Vector3(u, 1, v),
            CubemapFace.NegativeY => new Vector3(u, -1, -v),
            CubemapFace.PositiveZ => new Vector3(u, -v, 1),
            _ => new Vector3(-u, -v, -1),
        }).normalized;

        /// <summary>The sky shader's colour in a direction, without discs, stars or cloud shapes.</summary>
        public static Color Radiance(Vector3 d, in SkyState s)
        {
            Vector2 flatD = new Vector2(d.x, d.z), flatS = new Vector2(s.SunDirection.x, s.SunDirection.z);
            float towards = flatD.sqrMagnitude > 1e-8f && flatS.sqrMagnitude > 1e-8f ? Mathf.Clamp01(Vector2.Dot(flatD.normalized, flatS.normalized) * 0.5f + 0.5f) : 0.5f;
            Color horizon = Color.Lerp(s.HorizonAway, s.Horizon, towards * towards);
            Color sky = Color.Lerp(horizon, s.Zenith, Mathf.Pow(Mathf.Clamp01(d.y), 0.45f));
            sky = Color.Lerp(sky, horizon * 0.55f, Mathf.Clamp01(-d.y * 6));
            float mu = Vector3.Dot(d, s.SunDirection);
            float aboveSun = Mathf.Clamp01(s.SunDirection.y * 8 + 0.5f);
            sky += s.SunLight * ((HenyeyGreenstein(mu, 0.78f) * 0.09f + HenyeyGreenstein(mu, 0.3f) * 0.05f) * aboveSun);
            if (d.y > 0)
            {
                Color cloud = Color.Lerp(s.CloudShade, s.CloudLit, Mathf.Clamp01(0.55f + 0.45f * mu));
                sky = Color.Lerp(sky, cloud, s.CloudCover * 0.6f * Mathf.Clamp01(d.y / 0.12f));
            }
            sky.a = 1;
            return sky;
        }

        static float HenyeyGreenstein(float mu, float g)
        {
            float g2 = g * g;
            return (1 - g2) / (4 * Mathf.PI * Mathf.Pow(Mathf.Abs(1 + g2 - 2 * g * mu), 1.5f));
        }
    }
}
