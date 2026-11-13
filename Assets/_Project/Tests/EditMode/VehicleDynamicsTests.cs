// E-04: the sedan's handling, measured headless on made-up roads. The numbers are the owner's "weight and suspension" made checkable: a family's figures
// are estimates for that kind of vehicle, and a range here is what the spec is meant to give, so a change that moves them shows up.
using System;
using System.IO;
using Dad.Traffic;
using Dad.Vehicles;
using NUnit.Framework;
using UnityEngine;

namespace Dad.Tests
{
    public class VehicleDynamicsTests
    {
        const double Dt = 0.01;

        /// <summary>A road: flat at 0, with a rise of Step metres beyond z = StepAt (a kerb), on a grade.</summary>
        sealed class Road : IGroundProbe
        {
            public double Step, StepAt = double.MaxValue, Grade;
            /// <summary>When set, what the ground says its normal is, whatever its height.</summary>
            public D3? Face;
            double Height(double z) => z * Grade + (z >= StepAt ? Step : 0);

            public bool Cast(D3 from, D3 dir, double max, out ContactHit hit)
            {
                hit = default;
                // The direction is nearly straight down: walk it.
                double d = 0;
                for (int i = 0; i < 3; i++)
                {
                    var p = from + dir * d;
                    d += (p.Y - Height(p.Z)) / Math.Max(0.1, -dir.Y);
                }
                if (d < 0 || d > max) return false;
                hit = new ContactHit { Distance = d, Normal = Face ?? new D3(0, 1, -Grade).Normalized, Grip = 1 };
                return true;
            }
        }

        sealed class Rig
        {
            public readonly VehicleDynamics Car;
            public readonly TestBody Body;
            public readonly Road Road;
            public DriveInput In;
            public double Time;

            public Rig(HandlingSpec spec, Road road = null)
            {
                var model = VehicleArt.Read(spec.Kind, File.ReadAllBytes(Path.Combine(Application.dataPath, "_Project/Resources/Vehicles/Driven", spec.Kind + ".bytes")), 0);
                Car = new VehicleDynamics(spec, model.Wheels, model.Length, model.Width, model.Height);
                Body = new TestBody(spec.Mass, new D3(Car.PitchInertia, Car.YawInertia, Car.RollInertia), new D3(0, spec.CentreHeight, Car.CentreZ));
                Road = road ?? new Road();
                Run(4);
            }

            public void Step()
            {
                Car.Step(Dt, Body.Pose, In, Road, Body);
                Body.Integrate(Dt);
                Time += Dt;
            }

            public void Run(double seconds) { for (int i = 0, n = (int)(seconds / Dt); i < n; i++) Step(); }

            public double Speed => Body.Velocity.Dot(Body.Forward);

            /// <summary>Degrees: nose down is positive.</summary>
            public double Pitch => Math.Asin(-Body.Forward.Y) * 180 / Math.PI;

            /// <summary>Degrees: right side down is positive.</summary>
            public double Roll => Math.Asin(-Body.Right.Y) * 180 / Math.PI;

            public double RunUpTo(double speed)
            {
                In = new DriveInput { Throttle = 1 };
                double start = Time;
                while (Speed < speed && Time - start < 60) Step();
                In = default;
                return Time - start;
            }
        }

        static HandlingSpec Sedan => HandlingSpec.Sedan;

        [Test]
        public void ItSettlesAtItsDesignHeight()
        {
            var r = new Rig(Sedan);
            Assert.That(Math.Abs(r.Body.Position.Y), Is.LessThan(0.02), "body height");
            Assert.That(r.Body.Velocity.Length, Is.LessThan(0.02), "at rest");
            double total = 0;
            foreach (var w in r.Car.State.Wheels)
            {
                Assert.That(w.HubY, Is.EqualTo(w.RestY).Within(0.02), "hub at its rest height");
                total += w.Load;
            }
            Assert.That(total, Is.EqualTo(Sedan.Mass * 9.81).Within(Sedan.Mass * 9.81 * 0.01), "the wheels carry the car");
            var s = r.Car.State.Wheels;
            Assert.That(s[0].Load, Is.GreaterThan(s[2].Load), "the front is the heavier end");
        }

        [Test]
        public void NoWheelGoesIntoTheRoad()
        {
            var r = new Rig(Sedan);
            r.RunUpTo(25);
            r.In = new DriveInput { Brake = 1, Steer = 0.6 };
            for (int i = 0; i < 300; i++)
            {
                r.Step();
                foreach (var w in r.Car.State.Wheels)
                {
                    if (!w.Grounded) continue;
                    var bottom = r.Body.Pose.ToWorld(w.X, w.HubY - w.Radius, w.Z);
                    Assert.That(bottom.Y, Is.GreaterThan(-0.03), "tyre bottom");
                }
            }
        }

        [Test]
        public void ItPullsAwayAndChangesUp()
        {
            var r = new Rig(Sedan);
            double t = r.RunUpTo(100 / 3.6);
            Console.WriteLine($"  0-100 km/h {t:F1} s, gear {r.Car.State.Gear}");
            Assert.That(t, Is.InRange(9.0, 13.0));
            Assert.That(r.Car.State.Gear, Is.GreaterThanOrEqualTo(3));
        }

        [Test]
        public void ItHasATopSpeed()
        {
            var r = new Rig(Sedan);
            r.In = new DriveInput { Throttle = 1 };
            double most = 0, rpm = 0;
            for (int i = 0; i < 12000; i++) { r.Step(); most = Math.Max(most, r.Speed); rpm = Math.Max(rpm, r.Car.State.Rpm); }
            Console.WriteLine($"  top {most * 3.6:F0} km/h, gear {r.Car.State.Gear}, most revs {rpm:F0}");
            Assert.That(most * 3.6, Is.InRange(150, 200));
            Assert.That(rpm, Is.LessThanOrEqualTo(Sedan.RedlineRpm * 1.03));
        }

        [Test]
        public void ItBrakesAndTheNoseDips()
        {
            var r = new Rig(Sedan);
            r.RunUpTo(100 / 3.6);
            r.Run(0.5);
            var from = r.Body.Position;
            double start = r.Speed, dive = 0;
            r.In = new DriveInput { Brake = 1 };
            while (r.Speed > 0.5) { r.Step(); dive = Math.Max(dive, r.Pitch); }
            double metres = (r.Body.Position - from).Length;
            Console.WriteLine($"  from {start * 3.6:F0} km/h: {metres:F1} m, nose down {dive:F1} deg");
            Assert.That(metres, Is.InRange(36, 56));
            Assert.That(dive, Is.InRange(1.5, 6));
        }

        [Test]
        public void TheNoseRisesUnderPower()
        {
            var r = new Rig(Sedan);
            r.In = new DriveInput { Throttle = 1 };
            double most = 0;
            for (int i = 0; i < 200; i++) { r.Step(); most = Math.Min(most, r.Pitch); }
            Console.WriteLine($"  squat {most:F2} deg");
            Assert.That(most, Is.LessThan(-0.5));
        }

        [Test]
        public void SteeringRightTurnsRightAndTheBodyLeansOut()
        {
            var r = new Rig(Sedan);
            r.RunUpTo(60 / 3.6);
            r.In = new DriveInput { Throttle = 0.45, Steer = 1 };
            double roll = 0, lateral = 0;
            var last = r.Body.Velocity;
            for (int i = 0; i < 500; i++)
            {
                r.Step();
                roll = Math.Min(roll, r.Roll);
                // Measured from the velocity itself, over the last of the run, when the turn has settled.
                if (i > 200 && i % 10 == 0) { lateral = Math.Max(lateral, (r.Body.Velocity - last).Dot(r.Body.Right) / (10 * Dt)); }
                if (i % 10 == 0) last = r.Body.Velocity;
            }
            Console.WriteLine($"  at {r.Speed * 3.6:F0} km/h: {lateral:F1} m/s2, roll {roll:F1} deg, heading x {r.Body.Forward.X:F2}");
            Assert.That(r.Body.Spin.Dot(r.Body.Up), Is.GreaterThan(0), "turning right");
            Assert.That(roll, Is.InRange(-8.0, -3.0), "left side down in a right turn");
            Assert.That(lateral, Is.InRange(5.5, 10.0));
        }

        static double LeanInATurn()
        {
            var r = new Rig(Sedan);
            r.RunUpTo(60 / 3.6);
            r.In = new DriveInput { Throttle = 0.45, Steer = 1 };
            double roll = 0;
            for (int i = 0; i < 500; i++) { r.Step(); roll = Math.Min(roll, r.Roll); }
            return -roll;
        }

        // The range above does not show the bars: the Jetta's own track and spring rates keep it inside the range without them (mutation D1, 2026-11-06). So the same turn, with and without.
        [Test]
        public void TheBarsHoldTheBodyUpInATurn()
        {
            double with = LeanInATurn(), without, front = Sedan.FrontBar, rear = Sedan.RearBar;
            Sedan.FrontBar = Sedan.RearBar = 0;
            try { without = LeanInATurn(); }
            finally { Sedan.FrontBar = front; Sedan.RearBar = rear; }
            Console.WriteLine($"  lean {with:F2} deg with bars, {without:F2} without");
            Assert.That(with, Is.LessThan(without * 0.9));
        }

        [Test]
        public void TheHandbrakeLetsTheTailGo()
        {
            var r = new Rig(Sedan);
            r.RunUpTo(60 / 3.6);
            r.In = new DriveInput { Steer = 0.7, Handbrake = true };
            double skid = 0, most = 0;
            for (int i = 0; i < 120; i++)
            {
                r.Step();
                var ws = r.Car.State.Wheels;
                foreach (var w in ws) if (!w.Front) skid = Math.Max(skid, w.Skid);
                double side = Math.Abs(r.Body.Velocity.Dot(r.Body.Right)) / Math.Max(1, r.Body.Velocity.Length);
                most = Math.Max(most, side);
            }
            Console.WriteLine($"  rear skid {skid:F2}, slide {Math.Asin(Math.Min(1, most)) * 180 / Math.PI:F0} deg");
            Assert.That(skid, Is.GreaterThan(0.5));
            Assert.That(Math.Asin(Math.Min(1, most)) * 180 / Math.PI, Is.GreaterThan(10));
        }

        [Test]
        public void ItDoesNotRollOverOnFullLock()
        {
            var r = new Rig(Sedan);
            r.RunUpTo(90 / 3.6);
            r.In = new DriveInput { Steer = 1, Throttle = 1 };
            double least = 1;
            for (int i = 0; i < 600; i++) { r.Step(); least = Math.Min(least, r.Body.Up.Y); }
            Console.WriteLine($"  least up {least:F3}");
            Assert.That(least, Is.GreaterThan(0.9));
        }

        [Test]
        public void TheBrakePedalAtAStopGoesBackwards()
        {
            var r = new Rig(Sedan);
            r.In = new DriveInput { Brake = 1 };
            r.Run(3);
            Assert.That(r.Speed, Is.LessThan(-1), "rolling back");
            Assert.That(r.Car.State.ReverseLamps);
            r.In = new DriveInput { Throttle = 1 };
            r.Run(1);
            Assert.That(r.Car.State.BrakeLamps, "the throttle pedal brakes a car going backwards");
            r.Run(3);
            Assert.That(r.Speed, Is.GreaterThan(1));
            Assert.That(r.Car.State.Gear, Is.GreaterThan(0));
        }

        [Test]
        public void ItClimbsAKerbWithoutAJolt()
        {
            var road = new Road { Step = 0.15, StepAt = 30 };
            var r = new Rig(Sedan, road);
            r.In = new DriveInput { Throttle = 0.3 };
            while (r.Speed < 5) r.Step();
            r.In = new DriveInput { Throttle = 0.15 };
            double most = 0, last = r.Body.Velocity.Y, jolt = 0;
            for (int i = 0; i < 900 && r.Body.Position.Z < 40; i++)
            {
                r.Step();
                foreach (var w in r.Car.State.Wheels) most = Math.Max(most, w.HubY - w.RestY);
                jolt = Math.Max(jolt, Math.Abs(r.Body.Velocity.Y - last) / Dt);
                last = r.Body.Velocity.Y;
            }
            Console.WriteLine($"  most compression {most:F3} m, jolt {jolt:F1} m/s2, at z {r.Body.Position.Z:F1}");
            Assert.That(r.Body.Position.Z, Is.GreaterThan(35), "over it");
            Assert.That(r.Body.Position.Y, Is.EqualTo(0.15).Within(0.05), "on top");
            Assert.That(most, Is.GreaterThan(0.03), "the spring took it");
            Assert.That(jolt, Is.LessThan(30));
        }

        // A tyre is round: it meets a kerb ahead of the point under its axle.
        [Test]
        public void AWheelFeelsAKerbBeforeItsAxleIsOverIt()
        {
            var front = new Rig(Sedan).Car.State.Wheels[0];
            Assert.IsTrue(front.Front);
            var r = new Rig(Sedan, new Road { Step = 0.12, StepAt = front.Z + 0.12 });
            foreach (var w in r.Car.State.Wheels)
                if (w.Front) Assert.That(r.Road.Cast(new D3(0, 2, w.Z), new D3(0, -1, 0), 3, out var under) ? under.Distance : 0, Is.EqualTo(2).Within(0.05), "the axle is still short of it");
            Console.WriteLine($"the nose stands {-r.Pitch:0.00} degrees up");
            Assert.That(r.Pitch, Is.LessThan(-0.2), "the springs carry the same load, so it is the body that rises");
        }

        [Test]
        public void TheWheelsTurnLessTheFasterItGoes()
        {
            var r = new Rig(Sedan);
            r.RunUpTo(32);
            r.In = new DriveInput { Throttle = 0.4, Steer = 1 };
            r.Run(0.6);
            Console.WriteLine($"{r.Speed:0.0} m/s: {r.Car.State.SteerAngle * 180 / Math.PI:0.0} degrees of {Sedan.MaxSteer * 180 / Math.PI:0.0}");
            Assert.That(Math.Abs(r.Car.State.SteerAngle), Is.LessThan(Sedan.MaxSteer * 0.5));
        }

        [Test]
        public void TheWheelsKeepTurningUnderFullBrakes()
        {
            var r = new Rig(Sedan);
            r.RunUpTo(25);
            r.In = new DriveInput { Brake = 1 };
            int steps = 0, locked = 0;
            while (r.Speed > 6 && steps < 2000)
            {
                r.Step();
                steps++;
                foreach (var w in r.Car.State.Wheels) if (Math.Abs(w.Spin) * w.Radius < r.Speed * 0.1) { locked++; break; }
            }
            Console.WriteLine($"{locked} of {steps} steps with a wheel locked");
            Assert.That(steps, Is.LessThan(2000));
            Assert.That(locked, Is.LessThan(steps * 0.1));
        }

        [Test]
        public void TheEngineIdlesAtAStopAndRevsOnTheThrottle()
        {
            var r = new Rig(Sedan);
            Assert.That(r.Car.State.Rpm, Is.EqualTo(Sedan.IdleRpm).Within(60), "idling at a stop");
            r.In = new DriveInput { Throttle = 1 };
            r.Run(0.35);
            Assert.That(r.Car.State.Rpm, Is.GreaterThan(Sedan.IdleRpm + 1000), "the clutch lets the revs rise before the road does");
        }

        [Test]
        public void TheSpringsPushAlongTheGroundsOwnNormal()
        {
            var r = new Rig(Sedan, new Road { Face = new D3(0.34, 0.94, 0).Normalized });
            r.Step();
            foreach (var w in r.Car.State.Wheels) Assert.That(w.Normal.Dot(r.Body.Up), Is.LessThan(0.97), "a slope the body has not come round to");
            // Not a wall, though: a hit on something nearly upright is not ground to push along.
            var s = new Rig(Sedan);
            s.Road.Face = new D3(1, 0.1, 0).Normalized;
            s.Step();
            foreach (var w in s.Car.State.Wheels) Assert.That(w.Normal.Dot(s.Body.Up), Is.GreaterThan(0.99));
        }

        [Test]
        public void TheBrakeHoldsItOnAHill()
        {
            var road = new Road { Grade = 0.06 };
            var r = new Rig(Sedan, road);
            r.In = new DriveInput { Handbrake = true };
            r.Run(5);
            Console.WriteLine($"  on 6 %: {r.Body.Velocity.Length:F4} m/s");
            Assert.That(r.Body.Velocity.Length, Is.LessThan(0.05));
        }

        [Test]
        public void CoastingOnlySlowsIt()
        {
            var r = new Rig(Sedan);
            r.RunUpTo(20);
            r.In = default;
            r.Run(0.5); // the foot takes a moment to come up
            double last = r.Speed;
            for (int i = 0; i < 1500; i++)
            {
                r.Step();
                Assert.That(r.Speed, Is.LessThanOrEqualTo(last + 1e-6), "no speed from nowhere");
                last = r.Speed;
            }
            Assert.That(r.Speed, Is.LessThan(20));
        }

        [Test]
        public void TheLampsFollowTheDriving()
        {
            var r = new Rig(Sedan);
            Assert.That(r.Car.State.BrakeLamps, Is.False);
            r.In = new DriveInput { Throttle = 1 };
            r.Run(3);
            Assert.That(r.Car.State.BrakeLamps, Is.False, "not while pulling");
            r.In = new DriveInput { Brake = 0.6 };
            r.Run(0.3);
            Assert.That(r.Car.State.BrakeLamps, "on the brake");
            Assert.That(r.Car.State.ReverseLamps, Is.False);
        }

        [Test]
        public void AnIndicatorCancelsAsTheWheelComesBack()
        {
            var r = new Rig(Sedan);
            r.RunUpTo(6);
            r.Car.State.Indicator = 1;
            r.In = new DriveInput { Throttle = 0.2 };
            r.Run(1);
            Assert.That(r.Car.State.Indicator, Is.EqualTo(1), "stays on before the turn");
            r.In = new DriveInput { Throttle = 0.2, Steer = 1 };
            r.Run(1.5);
            Assert.That(r.Car.State.Indicator, Is.EqualTo(1), "and through it");
            r.In = new DriveInput { Throttle = 0.2 };
            r.Run(1);
            Assert.That(r.Car.State.Indicator, Is.EqualTo(0), "off when straight again");
        }

        [Test]
        public void TheEngineStopsWithoutFuelOrIgnition()
        {
            var r = new Rig(Sedan);
            r.RunUpTo(10);
            double before = r.Car.State.Fuel;
            r.In = new DriveInput { Throttle = 1 };
            r.Run(2);
            Assert.That(r.Car.State.Fuel, Is.LessThan(before), "it burns fuel");
            r.Car.State.Fuel = 1e-6;
            r.Run(1);
            Assert.That(r.Car.State.EngineOn, Is.False);
            Assert.That(r.Car.State.Rpm, Is.LessThan(100), "and winds down");
            double was = r.Speed;
            r.Run(2);
            Assert.That(r.Speed, Is.LessThan(was), "with no drive left");
        }

        [Test]
        public void ARunGivesOneResult()
        {
            double Once()
            {
                var r = new Rig(Sedan);
                r.RunUpTo(20);
                r.In = new DriveInput { Steer = 0.5, Throttle = 0.5 };
                r.Run(4);
                return r.Body.Position.X * 1000 + r.Body.Position.Z;
            }
            Assert.That(Once(), Is.EqualTo(Once()));
        }
    }
}
