// The transport system's rules that need no engine (2026-11-06): the twenty base colours and who wears them, the train's day on its line, and a boat on made-up water.
using System;
using System.Collections.Generic;
using System.Linq;
using Dad.Traffic;
using Dad.Vehicles;
using NUnit.Framework;

namespace Dad.Tests
{
    public class TransportTests
    {
        static readonly VehicleKind[] Kinds = (VehicleKind[])Enum.GetValues(typeof(VehicleKind));

        [Test]
        public void ThereAreExactlyTwentyBaseColours()
        {
            Assert.AreEqual(20, VehiclePaint.All.Length);
            Assert.AreEqual(20, VehiclePaints.Count);
            Assert.AreEqual(20, VehiclePaint.All.Select(p => p.Name).Distinct().Count());
            Assert.AreEqual(20, VehiclePaint.All.Select(p => (p.R, p.G, p.B)).Distinct().Count());
        }

        [Test]
        public void NoColourIsLoudOrMirrorBright()
        {
            foreach (var p in VehiclePaint.All)
            {
                int most = Math.Max(p.R, Math.Max(p.G, p.B)), least = Math.Min(p.R, Math.Min(p.G, p.B));
                Assert.Less(most - least, 100, p.Name + " is too saturated for the grounded look");
                Assert.LessOrEqual(p.Smoothness, 0.7, p.Name + " is too glossy");
            }
        }

        [Test]
        public void EveryColourIsWornBySomeone()
        {
            var seen = new HashSet<int>();
            foreach (var k in Kinds)
                for (uint id = 1; id < 3000; id++) seen.Add(VehiclePaints.For(k, id).Base);
            Assert.AreEqual(20, seen.Count);
        }

        [Test]
        public void EachCategoryHasItsOwnPalette()
        {
            var groups = (PaintGroup[])Enum.GetValues(typeof(PaintGroup));
            var sets = groups.Select(g => string.Join(",", VehiclePaints.Colours(g).Distinct().OrderBy(x => x))).ToArray();
            Assert.AreEqual(sets.Length, sets.Distinct().Count());
        }

        [Test]
        public void AVehicleKeepsItsPaint()
        {
            var a = VehiclePaints.For(VehicleKind.Sedan, 41);
            var b = VehiclePaints.For(VehicleKind.Sedan, 41);
            Assert.AreEqual(a.Base, b.Base);
            Assert.AreEqual(a.Shade, b.Shade);
        }

        [Test]
        public void NotEveryVehicleIsDirty()
        {
            int clean = 0;
            for (uint id = 1; id <= 1000; id++) if (VehiclePaints.For(VehicleKind.Sedan, id).Dust < 0.3) clean++;
            Assert.Greater(clean, 600);
        }

        static TrainRun Line(out TrainLine line)
        {
            line = new TrainLine(new double[] { 0, 0, 1000, 0, 1000, 1500 });
            return new TrainRun(line, new double[] { 60, 1000, 2440 });
        }

        [Test]
        public void TheTrainStopsAtEveryStopAndComesBack()
        {
            var r = Line(out _);
            var order = new List<int>();
            int was = r.At;
            double fastest = 0;
            for (int i = 0; i < 20 * 60 * 40; i++)
            {
                r.Step(0.05);
                fastest = Math.Max(fastest, r.V);
                if (r.At >= 0 && r.At != was)
                {
                    order.Add(r.At);
                    Assert.AreEqual(r.Stops[r.At], r.S, 0.01, "stopped on the mark");
                }
                if (r.At >= 0 || r.Now == TrainRun.Doing.Running) was = r.At >= 0 ? r.At : -1;
            }
            CollectionAssert.AreEqual(new[] { 1, 2, 1, 0 }, order.Take(4).ToArray());
            Assert.LessOrEqual(fastest, TrainRun.Top + 1e-6);
            Assert.Greater(fastest, TrainRun.Top * 0.9);
        }

        [Test]
        public void ItPullsAwayAndSlowsSmoothly()
        {
            var r = Line(out _);
            double last = 0, worst = 0;
            for (int i = 0; i < 20 * 400; i++)
            {
                r.Step(0.05);
                worst = Math.Max(worst, Math.Abs(r.V - last) / 0.05);
                last = r.V;
            }
            Assert.LessOrEqual(worst, TrainRun.Brake * 1.5 + 0.01);
        }

        [Test]
        public void DoorsOpenOnlyWhileItStands()
        {
            var r = Line(out _);
            bool opened = false;
            for (int i = 0; i < 20 * 300; i++)
            {
                r.Step(0.05);
                if (r.DoorsOpen) { opened = true; Assert.AreEqual(0, r.V); Assert.GreaterOrEqual(r.At, 0); }
            }
            Assert.IsTrue(opened);
        }

        [Test]
        public void ACarStandsOnItsTwoBogiesRoundACurve()
        {
            Line(out var line);
            line.Car(1000, 7.5, out double x, out double z, out double hx, out double hz);
            Assert.AreEqual(1, hx * hx + hz * hz, 1e-9);
            Assert.Less(x, 1000, "a car cuts the corner");
            Assert.Greater(hz, 0.5);
            Assert.Greater(hx, 0.5);
        }

        // A sea with a straight shore at z = 200, and a rigid body to feel what the boat asks for.
        sealed class Sea : IWater
        {
            public double Surface(double x, double z) => 0.14;
            public bool Shore(double x, double z, out double distance, out double outX, out double outZ)
            {
                distance = 200 - z;
                outX = 0;
                outZ = -1;
                return true;
            }
        }

        sealed class Hull : IForceSink
        {
            public D3 Force, Torque, Centre;
            public void AddForceAt(D3 force, D3 point)
            {
                Force += force;
                var r = point - Centre;
                Torque += new D3(r.Y * force.Z - r.Z * force.Y, r.Z * force.X - r.X * force.Z, r.X * force.Y - r.Y * force.X);
            }
        }

        // Upright, turning only about the vertical: enough to read the boat's height, speed and heading from.
        static void Sail(BoatDynamics b, DriveInput input, double seconds, ref double x, ref double y, ref double z, ref double vx, ref double vy, ref double vz, ref double yaw, ref double spin)
        {
            var sea = new Sea();
            const double dt = 0.01;
            for (double t = 0; t < seconds; t += dt)
            {
                var f = new D3(Math.Sin(yaw), 0, Math.Cos(yaw));
                var right = new D3(Math.Cos(yaw), 0, -Math.Sin(yaw));
                var pose = new ChassisPose { Position = new D3(x, y, z), Centre = new D3(x, y, z), Right = right, Up = new D3(0, 1, 0), Forward = f, Velocity = new D3(vx, vy, vz), Spin = new D3(0, spin, 0) };
                var h = new Hull { Centre = pose.Centre };
                b.Step(dt, pose, input, sea, h);
                vx += h.Force.X / b.Spec.Mass * dt;
                vy += (h.Force.Y / b.Spec.Mass - 9.81) * dt;
                vz += h.Force.Z / b.Spec.Mass * dt;
                spin += h.Torque.Y / b.YawInertia * dt;
                x += vx * dt;
                y += vy * dt;
                z += vz * dt;
                yaw += spin * dt;
            }
        }

        [Test]
        public void ABoatFloatsAtItsDraught()
        {
            var b = new BoatDynamics(BoatSpec.Inflatable, 4.13, 1.55, 1.41);
            double x = 0, y = 0.6, z = 0, vx = 0, vy = 0, vz = 0, yaw = 0, spin = 0;
            Sail(b, default, 20, ref x, ref y, ref z, ref vx, ref vy, ref vz, ref yaw, ref spin);
            Assert.AreEqual(0.14 - b.Spec.Draft, y, 0.02);
            Assert.AreEqual(0, vy, 0.01);
        }

        [Test]
        public void ItGoesWhereTheHelmSays()
        {
            var b = new BoatDynamics(BoatSpec.Inflatable, 4.13, 1.55, 1.41);
            b.State.EngineOn = true;
            double x = 0, y = -0.02, z = -400, vx = 0, vy = 0, vz = 0, yaw = 0, spin = 0;
            Sail(b, new DriveInput { Throttle = 1 }, 12, ref x, ref y, ref z, ref vx, ref vy, ref vz, ref yaw, ref spin);
            Assert.Greater(vz, 4, "under way");
            Assert.AreEqual(0, yaw, 0.01, "straight with the helm straight");
            Sail(b, new DriveInput { Throttle = 1, Steer = 1 }, 5, ref x, ref y, ref z, ref vx, ref vy, ref vz, ref yaw, ref spin);
            Assert.Greater(Math.Abs(yaw), 0.3, "turned");
            var before = Math.Sqrt(vx * vx + vz * vz);
            Sail(b, default, 10, ref x, ref y, ref z, ref vx, ref vy, ref vz, ref yaw, ref spin);
            Assert.Less(Math.Sqrt(vx * vx + vz * vz), before * 0.5, "the water takes its way off");
        }

        [Test]
        public void TheShoreStopsIt()
        {
            var b = new BoatDynamics(BoatSpec.Inflatable, 4.13, 1.55, 1.41);
            b.State.EngineOn = true;
            double x = 0, y = -0.02, z = 120, vx = 0, vy = 0, vz = 0, yaw = 0, spin = 0;
            Sail(b, new DriveInput { Throttle = 1 }, 40, ref x, ref y, ref z, ref vx, ref vy, ref vz, ref yaw, ref spin);
            Assert.Less(z, 200, "still afloat");
            Assert.IsTrue(b.Aground);
        }
    }
}
