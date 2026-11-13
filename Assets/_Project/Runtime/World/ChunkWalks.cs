// Where people can walk in a chunk: the middle of every sidewalk band ChunkRoads built, joined into lines (NPC Phase 3, 2026-11-08). Read from the bands as drawn, so a line never leaves
// paving the road builder laid. A band narrower than MinWidth has no line, and a drain none at all. Lines are cut at the chunk border and stop there.
using System;
using System.Collections.Generic;

namespace Dad.World
{
    public sealed class WalkLine
    {
        /// <summary>Metres east and north from the world's origin, and height, at each point; and the distance along the line to it.</summary>
        public double[] East, North, Along;
        public float[] Height;
        public double Length => Along[Along.Length - 1];

        /// <summary>The place and the direction of travel at a distance along the line.</summary>
        public void At(double s, out double east, out double north, out float height, out double de, out double dn)
        {
            int i = Array.BinarySearch(Along, s);
            if (i < 0) i = ~i - 1;
            i = Math.Clamp(i, 0, Along.Length - 2);
            double len = Along[i + 1] - Along[i], t = len > 1e-9 ? Math.Clamp((s - Along[i]) / len, 0, 1) : 0;
            east = East[i] + (East[i + 1] - East[i]) * t;
            north = North[i] + (North[i + 1] - North[i]) * t;
            height = Height[i] + (Height[i + 1] - Height[i]) * (float)t;
            de = len > 1e-9 ? (East[i + 1] - East[i]) / len : 1;
            dn = len > 1e-9 ? (North[i + 1] - North[i]) / len : 0;
        }
    }

    public sealed class ChunkWalks
    {
        public const double MinWidth = 1.0, MinLength = 8, Join = 0.6;

        public int ChunkX, ChunkY;
        public readonly List<WalkLine> Lines = new List<WalkLine>();

        public static ChunkWalks Build(int ix, int iy, double cornerEast, double cornerNorth, List<ChunkRoads.WalkPiece> pieces)
        {
            var w = new ChunkWalks { ChunkX = ix, ChunkY = iy };
            if (pieces == null) return w;
            var points = new List<(double x, double z, float y)>();
            long way = long.MinValue;
            int side = 0;
            foreach (var p in pieces)
            {
                double width = Math.Sqrt(Sq(p.OutAx - p.InAx) + Sq(p.OutAz - p.InAz));
                bool usable = p.Kind != ChunkRoads.WalkKind.Drain && p.Kind != ChunkRoads.WalkKind.Covered && width >= MinWidth;
                double ax = (p.InAx + p.OutAx) / 2, az = (p.InAz + p.OutAz) / 2, bx = (p.InBx + p.OutBx) / 2, bz = (p.InBz + p.OutBz) / 2;
                bool joins = usable && points.Count > 0 && p.Way == way && p.Side == side && Sq(points[points.Count - 1].x - ax) + Sq(points[points.Count - 1].z - az) <= Join * Join;
                if (!joins)
                {
                    Finish(w, points, cornerEast, cornerNorth);
                    points.Clear();
                    if (!usable) continue;
                    points.Add((ax, az, (float)p.Ya));
                }
                points.Add((bx, bz, (float)p.Yb));
                way = p.Way;
                side = p.Side;
            }
            Finish(w, points, cornerEast, cornerNorth);
            return w;
        }

        static double Sq(double v) => v * v;

        static void Finish(ChunkWalks w, List<(double x, double z, float y)> points, double cornerEast, double cornerNorth)
        {
            if (points.Count < 2) return;
            int n = points.Count;
            var line = new WalkLine { East = new double[n], North = new double[n], Along = new double[n], Height = new float[n] };
            for (int i = 0; i < n; i++)
            {
                line.East[i] = cornerEast + points[i].x;
                line.North[i] = cornerNorth + points[i].z;
                line.Height[i] = points[i].y;
                if (i > 0) line.Along[i] = line.Along[i - 1] + Math.Sqrt(Sq(points[i].x - points[i - 1].x) + Sq(points[i].z - points[i - 1].z));
            }
            if (line.Length >= MinLength) w.Lines.Add(line);
        }
    }
}
