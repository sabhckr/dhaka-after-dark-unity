// Street lamps at night (D-13): one small mesh a chunk for the light every lamp gives, whatever the quality level. Each lamp has a soft pool lying
// just above the footpath and a halo round its head, drawn additively by Dad/Glow, which also keeps them dark by day. Nothing is lit by them: walls and
// vehicles catch real lights only near the camera (StreetLamps, on the levels that have them).
//
// A pool lies flat at PoolHeight, over the road and the footpath alike, so that one height does for both; it is soft enough that 15 cm of parallax over
// the road does not show. Dhaka's lamps are a mix, so a hash of the lamp picks LED white or the older sodium.
using System;
using System.Collections.Generic;

namespace Dad.World
{
    public sealed class StreetGlow
    {
        /// <summary>Pool radius on the ground, and the halo's radius round the head, metres.</summary>
        public const double PoolRadius = 13, HaloRadius = 1.5;
        /// <summary>Just above the footpath (0.43 m) and below a median's top.</summary>
        public const double PoolHeight = 0.46;
        /// <summary>How far ahead of the head the pool's centre falls: a lamp throws towards the road.</summary>
        public const double PoolAhead = 1.0;
        /// <summary>The share of lamps still on sodium.</summary>
        public const double SodiumShare = 0.3;
        public const uint Led = 0xffe6c8, Sodium = 0xffa858;
        /// <summary>The alpha that marks a halo's vertices; a pool's is 0. The shader stands a halo up to face the camera.</summary>
        public const byte HaloMark = 255;

        public float[] Positions = Array.Empty<float>(), Corners = Array.Empty<float>();
        public uint[] Colors = Array.Empty<uint>();
        public int[] Indices = Array.Empty<int>();
        public int Lamps;
        public int VertexCount => Positions.Length / 3;

        /// <summary>A lamp's colour, as WorldMaterials packs it (red in the low byte), with no mark.</summary>
        public static uint ColourOf(long seed)
        {
            uint h = (uint)(seed ^ (seed >> 32));
            h = ChunkBuildings.LowBias(h);
            uint rgb = ChunkBuildings.ToUnit(h) < SodiumShare ? Sodium : Led;
            return ((rgb >> 16) & 0xff) | (rgb & 0xff00) | ((rgb & 0xff) << 16);
        }

        /// <summary>The mesh for these lamp heads, local metres.</summary>
        public static StreetGlow Build(IReadOnlyList<ChunkStreet.LampHead> lamps)
        {
            int n = lamps.Count;
            var g = new StreetGlow { Lamps = n, Positions = new float[n * 8 * 3], Corners = new float[n * 8 * 2], Colors = new uint[n * 8], Indices = new int[n * 12] };
            for (int i = 0; i < n; i++)
            {
                var l = lamps[i];
                uint colour = ColourOf(l.Seed);
                double px = l.X + l.Fx * PoolAhead, pz = l.Z + l.Fz * PoolAhead;
                for (int k = 0; k < 4; k++)
                {
                    double cx = k == 1 || k == 2 ? 1 : -1, cz = k >= 2 ? 1 : -1;
                    int v = i * 8 + k;
                    g.Set(v, px + cx * PoolRadius, PoolHeight, pz + cz * PoolRadius, cx, cz, colour);
                    // A halo keeps its head's position on all four corners: the shader spreads them on the screen.
                    g.Set(v + 4, l.X, l.Y, l.Z, cx, cz, colour | ((uint)HaloMark << 24));
                }
                int first = i * 8, at = i * 12;
                foreach (int k in new[] { 0, 3, 2, 0, 2, 1 }) g.Indices[at++] = first + k;
                foreach (int k in new[] { 4, 7, 6, 4, 6, 5 }) g.Indices[at++] = first + k;
            }
            return g;
        }

        void Set(int v, double x, double y, double z, double cx, double cz, uint colour)
        {
            Positions[3 * v] = (float)x;
            Positions[3 * v + 1] = (float)y;
            Positions[3 * v + 2] = (float)z;
            Corners[2 * v] = (float)cx;
            Corners[2 * v + 1] = (float)cz;
            Colors[v] = colour;
        }
    }
}
