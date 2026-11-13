// A rigid body with no engine (E-04): the chassis the dynamics drives in tests and tools, so handling can be measured headless. In play a Rigidbody does this job.
using System;

namespace Dad.Vehicles
{
    public sealed class TestBody : IForceSink
    {
        public readonly double Mass;
        readonly D3 inertia, centre;
        public D3 Position, Velocity, Spin, Right = new D3(1, 0, 0), Up = new D3(0, 1, 0), Forward = new D3(0, 0, 1);
        D3 force, torque;

        /// <summary>inertia is about the body's right, up and forward; centre is where the mass is, on the body.</summary>
        public TestBody(double mass, D3 inertia, D3 centre) { Mass = mass; this.inertia = inertia; this.centre = centre; }

        public D3 Centre => Position + Right * centre.X + Up * centre.Y + Forward * centre.Z;

        public ChassisPose Pose => new ChassisPose { Position = Position, Centre = Centre, Right = Right, Up = Up, Forward = Forward, Velocity = Velocity, Spin = Spin };

        public void AddForceAt(D3 f, D3 point)
        {
            force += f;
            torque += (point - Centre).Cross(f);
        }

        /// <summary>Moves on by dt under what it has been given, and gravity.</summary>
        public void Integrate(double dt, double gravity = 9.81)
        {
            var c0 = Centre;
            Velocity += (force * (1 / Mass) + new D3(0, -gravity, 0)) * dt;
            // Spin in the body's axes, Euler's equations, then back.
            var w = new D3(Spin.Dot(Right), Spin.Dot(Up), Spin.Dot(Forward));
            var t = new D3(torque.Dot(Right), torque.Dot(Up), torque.Dot(Forward));
            var iw = new D3(inertia.X * w.X, inertia.Y * w.Y, inertia.Z * w.Z);
            var gyro = w.Cross(iw);
            w += new D3((t.X - gyro.X) / inertia.X, (t.Y - gyro.Y) / inertia.Y, (t.Z - gyro.Z) / inertia.Z) * dt;
            Spin = Right * w.X + Up * w.Y + Forward * w.Z;
            var turn = Spin * dt;
            Right = Rotate(Right, turn);
            Up = Rotate(Up, turn);
            Forward = Rotate(Forward, turn);
            // Keep the axes square.
            Forward = Forward.Normalized;
            Right = Up.Cross(Forward).Normalized;
            Up = Forward.Cross(Right);
            var c1 = c0 + Velocity * dt;
            Position = c1 - Right * centre.X - Up * centre.Y - Forward * centre.Z;
            force = torque = D3.Zero;
        }

        static D3 Rotate(D3 v, D3 turn)
        {
            double a = turn.Length;
            if (a < 1e-12) return v;
            var k = turn * (1 / a);
            return v * Math.Cos(a) + k.Cross(v) * Math.Sin(a) + k * (k.Dot(v) * (1 - Math.Cos(a)));
        }
    }
}
