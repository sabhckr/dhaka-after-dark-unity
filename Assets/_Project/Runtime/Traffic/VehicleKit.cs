// The form a vehicle mesh takes in the game, whichever model it came from. A mesh is positions, normals, an sRGB colour with the material in alpha, and a lamp number in a float.
// x is right, y up, z forward, in metres, the origin on the road midway along the vehicle. Glass goes in a mesh of its own, drawn after the rest, so that the cabin shows through it.
// No engine dependency.
using System;
using System.Collections.Generic;
using Dad.World;

namespace Dad.Traffic
{
    /// <summary>What the Dad/Vehicle shader makes of a surface.</summary>
    public enum KitMat : byte { Paint, Plastic, Glass, Metal, Lens, Rough, Rubber, Trim }

    /// <summary>What lights a lens.</summary>
    public enum KitLamp : byte { None, Head, Tail, Brake, Left, Right, Cabin, Marker, Dash }

    public struct V3
    {
        public double X, Y, Z;
        public V3(double x, double y, double z) { X = x; Y = y; Z = z; }
        public static V3 operator +(V3 a, V3 b) => new V3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static V3 operator -(V3 a, V3 b) => new V3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static V3 operator *(V3 a, double k) => new V3(a.X * k, a.Y * k, a.Z * k);
        public static double Dot(V3 a, V3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        public static V3 Cross(V3 a, V3 b) => new V3(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
        public double Length => Math.Sqrt(X * X + Y * Y + Z * Z);
        public V3 Unit { get { double l = Length; return l > 1e-12 ? this * (1 / l) : new V3(0, 1, 0); } }
    }

    public sealed class KitMesh
    {
        public readonly List<float> Pos = new List<float>(4096), Nor = new List<float>(4096), Lamp = new List<float>(1024);
        public readonly List<byte> Rgba = new List<byte>(4096);
        public readonly List<int> Tris = new List<int>(8192);
        /// <summary>Places in the family's picture, two a vertex, where it has one.</summary>
        public readonly List<float> Map = new List<float>();
        public int Vertices => Lamp.Count;
        public int Triangles => Tris.Count / 3;
    }

    /// <summary>A piece of the driven vehicle that moves or hides by itself (E-04): its mesh is relative to Pivot.</summary>
    public sealed class KitPart
    {
        public string Name;
        public V3 Pivot;
        /// <summary>What it turns about (a wheel's column, a needle's spindle, a door's hinge); how far a door opens, in degrees.</summary>
        public V3 Axis;
        public double Swing;
        /// <summary>Drawn with the glass, not the body.</summary>
        public bool Glass;
        public KitMesh Mesh = new KitMesh();
    }
}
