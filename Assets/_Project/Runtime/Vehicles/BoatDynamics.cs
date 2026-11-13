// A boat on the water (2026-11-06): floats on four points, is pushed by a motor at the stern that swings with the helm, and is slowed far more sideways than along its keel. Knows no engine, as
// VehicleDynamics knows none: it asks IWater where the surface and the shore are and hands forces to IForceSink, so tests drive it on made-up water. The numbers are estimates for an inflatable
// with an outboard. A boat that reaches the shore grounds: the shore pushes back and takes its way off, which is what stands in for a sloped sea bed, as the ground under the water is flat.
using System;
using Dad.Traffic;

namespace Dad.Vehicles
{
    /// <summary>What a boat asks of the water it is on.</summary>
    public interface IWater
    {
        /// <summary>Where the surface is at this point, at this moment.</summary>
        double Surface(double x, double z);
        /// <summary>Metres to the nearest land, negative on land, and the way out from it to open water. False where nothing is known about the place.</summary>
        bool Shore(double x, double z, out double distance, out double outX, out double outZ);
    }

    public sealed class BoatSpec
    {
        public double Mass = 520, Draft = 0.16, Thrust = 2300, ReverseThrust = 900, Rudder = 0.5, RudderRate = 1.8, Along = 26, Across = 620, Turn = 1500, Roll = 1400;
        /// <summary>The share of critical damping on the floats, and how far off the shore the keel touches.</summary>
        public double Heave = 0.55, Grounds = 1.2;
        public static readonly BoatSpec Inflatable = new BoatSpec();
    }

    public sealed class BoatDynamics
    {
        const double Gravity = 9.81;
        public readonly BoatSpec Spec;
        public readonly VehicleState State;
        public readonly double Length, Width, RollInertia, PitchInertia, YawInertia;
        /// <summary>The shore has the boat this moment.</summary>
        public bool Aground { get; private set; }
        /// <summary>How much of it is under water, 0 to 1: tests and the wake.</summary>
        public double Afloat { get; private set; }

        readonly (double x, double z)[] floats;
        readonly double spring, damper;

        public BoatDynamics(BoatSpec spec, double length, double width, double height)
        {
            Spec = spec;
            Length = length;
            Width = width;
            State = new VehicleState(Array.Empty<WheelState>());
            floats = new[] { (-width * 0.36, length * 0.32), (width * 0.36, length * 0.32), (-width * 0.36, -length * 0.36), (width * 0.36, -length * 0.36) };
            spring = spec.Mass * Gravity / (floats.Length * spec.Draft);
            damper = 2 * spec.Heave * Math.Sqrt(spring * spec.Mass / floats.Length);
            RollInertia = spec.Mass * (width * width + height * height) / 12;
            PitchInertia = spec.Mass * (height * height + length * length) / 12;
            YawInertia = spec.Mass * (width * width + length * length) / 12;
        }

        public void Step(double dt, in ChassisPose pose, in DriveInput input, IWater water, IForceSink sink)
        {
            var s = State;
            s.Speed = pose.Velocity.Dot(pose.Forward);
            s.Throttle = Approach(s.Throttle, s.EngineOn ? input.Throttle : 0, 2.5 * dt);
            s.Brake = Approach(s.Brake, s.EngineOn ? input.Brake : 0, 2.5 * dt);
            s.Gear = s.Brake > s.Throttle ? -1 : 1;
            s.SteerAngle = Approach(s.SteerAngle, Math.Clamp(input.Steer, -1, 1) * Spec.Rudder, Spec.RudderRate * dt);
            // No gearbox: the revs follow the lever, and the sound follows the revs.
            s.Rpm = Approach(s.Rpm, s.EngineOn ? 900 + 4600 * Math.Max(s.Throttle, s.Brake) : 0, 5000 * dt);

            int wet = 0;
            foreach (var (x, z) in floats)
            {
                var at = pose.ToWorld(x, 0, z);
                double depth = water.Surface(at.X, at.Z) - at.Y;
                if (depth <= -0.2) continue;
                wet++;
                var v = pose.PointVelocity(at);
                double force = spring * Math.Max(0, Math.Min(depth, Spec.Draft * 4)) - damper * v.Y;
                if (depth > 0 && force > 0) sink.AddForceAt(new D3(0, force, 0), at);
            }
            Afloat = wet / (double)floats.Length;
            if (wet == 0) return;

            // The motor pushes along where it points, from the stern, and only while its screw is wet.
            double push = (s.Throttle * Spec.Thrust - s.Brake * Spec.ReverseThrust) * Afloat;
            double sn = Math.Sin(s.SteerAngle), cs = Math.Cos(s.SteerAngle);
            var flatForward = new D3(pose.Forward.X, 0, pose.Forward.Z).Normalized;
            var flatRight = new D3(pose.Right.X, 0, pose.Right.Z).Normalized;
            var motor = pose.ToWorld(0, 0, -Length * 0.46);
            sink.AddForceAt((flatForward * cs - flatRight * sn) * push, motor);

            // The water's hold: quadratic and linear, little along the keel, much across it.
            double along = pose.Velocity.Dot(flatForward), across = pose.Velocity.Dot(flatRight);
            sink.AddForceAt(flatForward * (-Spec.Along * along * Math.Abs(along) - 60 * along) + flatRight * (-Spec.Across * across * Math.Abs(across) - 300 * across), pose.Centre);

            // The hull resists turning and rolling; a couple, so it moves nothing along.
            double yaw = pose.Spin.Y;
            Couple(pose.Forward, pose.Right, Spec.Turn * yaw, pose, sink);
            double roll = pose.Spin.Dot(pose.Forward);
            Couple(pose.Right, pose.Up, Spec.Roll * roll, pose, sink);

            Aground = false;
            if (water.Shore(pose.Position.X, pose.Position.Z, out double dist, out double ox, out double oz) && dist < Spec.Grounds + Length / 2)
            {
                Aground = true;
                double into = -(pose.Velocity.X * ox + pose.Velocity.Z * oz);
                double press = (Spec.Grounds + Length / 2 - dist) * Spec.Mass * 30;
                if (into > 0) press += into * Spec.Mass * 4;
                sink.AddForceAt(new D3(ox, 0, oz) * press + new D3(pose.Velocity.X, 0, pose.Velocity.Z) * (-Spec.Mass * 0.8), pose.Centre);
            }
        }

        // Forces at arm's length either side of the centre: a turn about the third axis, of this many newton metres.
        static void Couple(D3 forceAxis, D3 armAxis, double torque, in ChassisPose pose, IForceSink sink)
        {
            var f = forceAxis * (torque / 2);
            sink.AddForceAt(f, pose.Centre + armAxis);
            sink.AddForceAt(-f, pose.Centre - armAxis);
        }

        static double Approach(double from, double to, double by) => from < to ? Math.Min(to, from + by) : Math.Max(to, from - by);
    }
}
