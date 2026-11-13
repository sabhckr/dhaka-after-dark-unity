// What the vehicle dynamics (E-04) works in, with no engine in sight: vectors, the chassis as it stands this step, what a wheel's
// probe finds, and where forces go. In play these are filled from a Rigidbody and Physics.Raycast; in tests from TestBody. All of it is
// component for component, so it holds in Unity's left-handed frame as written: x right, y up, z forward, a positive turn about y to the right.
using System;

namespace Dad.Vehicles
{
    public readonly struct D3
    {
        public readonly double X, Y, Z;
        public D3(double x, double y, double z) { X = x; Y = y; Z = z; }
        public static readonly D3 Zero = new D3(0, 0, 0);
        public static D3 operator +(D3 a, D3 b) => new D3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static D3 operator -(D3 a, D3 b) => new D3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static D3 operator -(D3 a) => new D3(-a.X, -a.Y, -a.Z);
        public static D3 operator *(D3 a, double k) => new D3(a.X * k, a.Y * k, a.Z * k);
        public static D3 operator *(double k, D3 a) => a * k;
        public double Dot(D3 b) => X * b.X + Y * b.Y + Z * b.Z;
        public D3 Cross(D3 b) => new D3(Y * b.Z - Z * b.Y, Z * b.X - X * b.Z, X * b.Y - Y * b.X);
        public double Length => Math.Sqrt(Dot(this));
        public D3 Normalized { get { double l = Length; return l > 1e-12 ? this * (1 / l) : Zero; } }
    }

    /// <summary>The chassis at the start of a step. Position is the body's origin: on the road under its middle, at design ride height.</summary>
    public struct ChassisPose
    {
        public D3 Position, Centre, Right, Up, Forward, Velocity, Spin;
        public readonly D3 ToWorld(double x, double y, double z) => Position + Right * x + Up * y + Forward * z;
        public readonly D3 PointVelocity(D3 p) => Velocity + Spin.Cross(p - Centre);
    }

    /// <summary>What the driver asks for: throttle and brake 0 to 1, steer -1 (left) to 1 (right).</summary>
    public struct DriveInput
    {
        public double Throttle, Brake, Steer;
        public bool Handbrake;
    }

    public struct ContactHit
    {
        /// <summary>Metres along the probe.</summary>
        public double Distance;
        public D3 Normal;
        /// <summary>The surface's share of a tyre's grip: 1 for dry road.</summary>
        public double Grip;
    }

    /// <summary>Asks the world what is below a point. The world's collision, whatever it is, stays on the other side of this.</summary>
    public interface IGroundProbe
    {
        bool Cast(D3 from, D3 direction, double max, out ContactHit hit);
    }

    public interface IForceSink
    {
        void AddForceAt(D3 force, D3 point);
    }
}
