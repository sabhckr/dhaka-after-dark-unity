// Periodic noise for tileable textures (D-02, ENVIRONMENT_ROADMAP ED-2). Every function takes
// texture coordinates s, t (one tile is [0, 1)) and whole-number frequencies, and repeats exactly
// over the unit square, so a texture made from them tiles at any resolution. Deterministic:
// integer hashing only, no System.Random, no time.
using System;

namespace Dad.EditorTools.Materials
{
    public static class TileNoise
    {
        /// <summary>lowbias32 (Chris Wellons): a well-mixed 32-bit integer hash.</summary>
        public static uint Hash(uint x)
        {
            x ^= x >> 16; x *= 0x7feb352dU;
            x ^= x >> 15; x *= 0x846ca68bU;
            x ^= x >> 16;
            return x;
        }

        public static uint Hash(int x, int y, uint seed) => Hash((uint)x ^ Hash((uint)y ^ Hash(seed)));

        /// <summary>A hash as a float in [0, 1).</summary>
        public static float Unit(uint h) => (h >> 8) * (1f / 16777216f);

        public static int Wrap(int x, int period) { int r = x % period; return r < 0 ? r + period : r; }

        static float Fade(float t) => t * t * t * (t * (t * 6 - 15) + 10);

        static float Lerp(float a, float b, float t) => a + (b - a) * t;

        /// <summary>Gradient noise with fx by fy lattice cells per tile; roughly [-1, 1].</summary>
        public static float Perlin(float s, float t, int fx, int fy, uint seed)
        {
            float x = s * fx, y = t * fy;
            int x0 = (int)MathF.Floor(x), y0 = (int)MathF.Floor(y);
            float dx = x - x0, dy = y - y0;
            int ax = Wrap(x0, fx), bx = Wrap(x0 + 1, fx), ay = Wrap(y0, fy), by = Wrap(y0 + 1, fy);
            float n00 = Grad(ax, ay, seed, dx, dy), n10 = Grad(bx, ay, seed, dx - 1, dy);
            float n01 = Grad(ax, by, seed, dx, dy - 1), n11 = Grad(bx, by, seed, dx - 1, dy - 1);
            float u = Fade(dx), v = Fade(dy);
            return 1.4142f * Lerp(Lerp(n00, n10, u), Lerp(n01, n11, u), v);
        }

        public static float Perlin(float s, float t, int f, uint seed) => Perlin(s, t, f, f, seed);

        static float Grad(int ix, int iy, uint seed, float dx, float dy)
        {
            float a = Unit(Hash(ix, iy, seed)) * 6.2831853f;
            return MathF.Cos(a) * dx + MathF.Sin(a) * dy;
        }

        /// <summary>Fractal sum of Perlin octaves from f cells per tile, doubling; normalised to about [-1, 1].</summary>
        public static float Fbm(float s, float t, int f, int octaves, uint seed, float gain = 0.5f)
        {
            float sum = 0, amp = 1, norm = 0;
            for (int o = 0; o < octaves; o++)
            {
                sum += amp * Perlin(s, t, f << o, seed + (uint)o * 7919u);
                norm += amp;
                amp *= gain;
            }
            // Summed octaves cancel; rescale so the spread matches one octave's.
            return sum / norm * (1f + 0.35f * (octaves - 1) / octaves);
        }

        public struct Cell
        {
            /// <summary>Distances to the nearest and second-nearest feature point, in cells.</summary>
            public float F1, F2;
            /// <summary>A hash of the nearest feature point's cell.</summary>
            public uint Id;
        }

        /// <summary>Cellular (Worley) noise with f by f cells per tile, one jittered point per cell.</summary>
        public static Cell Worley(float s, float t, int f, uint seed, float jitter = 0.9f)
        {
            float x = s * f, y = t * f;
            int xi = (int)MathF.Floor(x), yi = (int)MathF.Floor(y);
            float f1 = 9f, f2 = 9f;
            uint id = 0;
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int cx = xi + dx, cy = yi + dy;
                uint h = Hash(Wrap(cx, f), Wrap(cy, f), seed);
                float px = cx + 0.5f + jitter * (Unit(h) - 0.5f);
                float py = cy + 0.5f + jitter * (Unit(Hash(h)) - 0.5f);
                float d = MathF.Sqrt((x - px) * (x - px) + (y - py) * (y - py));
                if (d < f1) { f2 = f1; f1 = d; id = Hash(h ^ 0x5bd1e995U); }
                else if (d < f2) f2 = d;
            }
            return new Cell { F1 = f1, F2 = f2, Id = id };
        }

        public struct Unit2
        {
            /// <summary>Distance to the nearest joint, in metres, across and along.</summary>
            public float Dx, Dy;
            /// <summary>Position inside the unit, [0, 1) each way.</summary>
            public float U, V;
            public uint Id;
        }

        /// <summary>
        /// A grid of units (bricks, tiles, slabs): cols by rows per tile of periodM metres, odd rows
        /// shifted by rowOffset of a unit (0.5 for running bond; rows must then be even to tile).
        /// </summary>
        public static Unit2 Units(float s, float t, int cols, int rows, float rowOffset, float periodM, uint seed)
        {
            float y = t * rows;
            int row = (int)MathF.Floor(y);
            float x = s * cols + ((row & 1) != 0 ? rowOffset : 0f);
            int col = (int)MathF.Floor(x);
            float u = x - col, v = y - row;
            return new Unit2
            {
                Dx = MathF.Min(u, 1 - u) * periodM / cols,
                Dy = MathF.Min(v, 1 - v) * periodM / rows,
                U = u, V = v,
                Id = Hash(Wrap(col, cols), Wrap(row, rows), seed),
            };
        }

        public static float Saturate(float x) => x < 0 ? 0 : x > 1 ? 1 : x;

        public static float Smoothstep(float a, float b, float x)
        {
            float t = Saturate((x - a) / (b - a));
            return t * t * (3 - 2 * t);
        }
    }
}
