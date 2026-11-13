// World coordinates, chunk cells and the floating origin offset. No UnityEngine dependency.
//
// World frame: metres from the world origin (UTM 46N E 230000, N 2615000, EPSG:32646),
// x = east, z = north, kept as double. Chunks are 500 m cells. A cell (X, Z) covers
// east [X*500, X*500+500) and north [Z*500, Z*500+500); chunk id "c_<ix>_<iy>" uses the
// Three.js grid, where ix = X and iy = -(Z + 1).
//
// Scene positions are float, relative to the floating origin. The origin always sits on a
// cell corner, so every shift is a whole number of 500 m and is exact in float. Height (y)
// is never shifted: the city spans tens of metres vertically.
using System;
using System.Globalization;

namespace Dad.World
{
    public readonly struct ChunkCell : IEquatable<ChunkCell>
    {
        public const double Size = DadcChunk.ChunkCm / 100.0;

        public readonly int X, Z;

        public ChunkCell(int x, int z) { X = x; Z = z; }

        public int Ix => X;
        public int Iy => -(Z + 1);
        public string Id => $"c_{Ix.ToString(CultureInfo.InvariantCulture)}_{Iy.ToString(CultureInfo.InvariantCulture)}";
        public double CornerEast => X * Size;
        public double CornerNorth => Z * Size;

        /// <summary>The cell containing a world position in metres.</summary>
        public static ChunkCell Of(double east, double north) =>
            new ChunkCell((int)Math.Floor(east / Size), (int)Math.Floor(north / Size));

        public static ChunkCell FromIxIy(int ix, int iy) => new ChunkCell(ix, -(iy + 1));

        public static bool TryParse(string id, out ChunkCell cell)
        {
            cell = default;
            if (id == null || !id.StartsWith("c_", StringComparison.Ordinal)) return false;
            int sep = id.IndexOf('_', 3);
            if (sep < 0) return false;
            if (!int.TryParse(id.Substring(2, sep - 2), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int ix)) return false;
            if (!int.TryParse(id.Substring(sep + 1), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int iy)) return false;
            cell = FromIxIy(ix, iy);
            return cell.Id == id;
        }

        public bool Equals(ChunkCell o) => X == o.X && Z == o.Z;
        public override bool Equals(object o) => o is ChunkCell c && Equals(c);
        public override int GetHashCode() => X * 73856093 ^ Z * 19349663;
        public override string ToString() => $"{Id} ({X}, {Z})";
    }

    public static class WorldFrame
    {
        public const double OriginEasting = 230000, OriginNorthing = 2615000;

        public static void ToUtm(double east, double north, out double easting, out double northing)
        {
            easting = east + OriginEasting;
            northing = north + OriginNorthing;
        }

        public static void FromUtm(double easting, double northing, out double east, out double north)
        {
            east = easting - OriginEasting;
            north = northing - OriginNorthing;
        }
    }

    /// <summary>Where the scene's (0, 0) sits in the world. Converts world doubles to scene floats.</summary>
    public sealed class OriginOffset
    {
        public double East { get; private set; }
        public double North { get; private set; }
        public ChunkCell Cell => ChunkCell.Of(East, North);

        public void ToScene(double east, double north, out float x, out float z)
        {
            x = (float)(east - East);
            z = (float)(north - North);
        }

        public void ToWorld(float x, float z, out double east, out double north)
        {
            east = East + x;
            north = North + z;
        }

        /// <summary>
        /// If a scene position is further than <paramref name="threshold"/> metres from the origin on
        /// either axis, moves the origin to the corner of the cell under it and returns the shift.
        /// Scene objects must then move by minus the shift.
        /// </summary>
        public bool TryShift(float x, float z, float threshold, out double shiftEast, out double shiftNorth)
        {
            shiftEast = shiftNorth = 0;
            if (Math.Abs(x) <= threshold && Math.Abs(z) <= threshold) return false;
            ToWorld(x, z, out double east, out double north);
            var cell = ChunkCell.Of(east, north);
            shiftEast = cell.CornerEast - East;
            shiftNorth = cell.CornerNorth - North;
            East = cell.CornerEast;
            North = cell.CornerNorth;
            return true;
        }

        /// <summary>Puts the origin on the corner of the cell containing a world position (start or teleport).</summary>
        public void Reset(double east, double north)
        {
            var cell = ChunkCell.Of(east, north);
            East = cell.CornerEast;
            North = cell.CornerNorth;
        }
    }
}
