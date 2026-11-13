// The driven vehicle (E-04), from the owner's model: what the driver sees is there, and it opens and turns about the right places.
using System;
using System.IO;
using System.Linq;
using Dad.Traffic;
using NUnit.Framework;
using UnityEngine;

namespace Dad.Tests
{
    public class PlayerCabinTests
    {
        static string Dir => Path.Combine(Application.dataPath, "_Project/Resources/Vehicles");
        static VehicleModel Car() => VehicleArt.Read(VehicleKind.Sedan, File.ReadAllBytes(Path.Combine(Dir, "Driven", "Sedan.bytes")), 0);
        static KitPart Part(VehicleModel m, string name) => m.Parts.Single(p => p.Name == name);
        static V3 Unit(V3 v) => v * (1 / Math.Sqrt(Dot(v, v)));
        static double Dot(V3 a, V3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        static V3 Cross(V3 a, V3 b) => new V3(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);

        // How far a mesh reaches from the line through its pivot along this axis.
        static double Reach(KitPart part)
        {
            var a = Unit(part.Axis);
            double r = 0;
            var m = part.Mesh;
            for (int i = 0; i < m.Pos.Count; i += 3)
            {
                var p = new V3(m.Pos[i], m.Pos[i + 1], m.Pos[i + 2]);
                var off = p - a * Dot(p, a);
                r = Math.Max(r, Math.Sqrt(Dot(off, off)));
            }
            return r;
        }

        // A point turned about an axis, as Quaternion.AngleAxis turns it.
        static V3 Turned(V3 v, V3 axis, double degrees)
        {
            double t = degrees * Math.PI / 180;
            var k = Unit(axis);
            return v * Math.Cos(t) + Cross(k, v) * Math.Sin(t) + k * (Dot(k, v) * (1 - Math.Cos(t)));
        }

        [Test]
        public void ATrafficVehicleStaysOneMesh()
        {
            foreach (VehicleKind k in Enum.GetValues(typeof(VehicleKind)))
                if (k != VehicleKind.Boat) Assert.AreEqual(0, VehicleArt.Read(k, File.ReadAllBytes(Path.Combine(Dir, k + ".bytes")), 0).Parts.Length, k.ToString()); // The boat is never traffic.
        }

        [Test]
        public void TheCarIsWithinItsBudget()
        {
            var car = Car();
            int parts = car.Parts.Sum(p => p.Mesh.Triangles), wheels = car.Wheels.Sum(w => car.OwnWheels[w.Mesh].Triangles + (w.Hub >= 0 ? car.Hubs[w.Hub].Triangles : 0));
            int all = car.Near.Triangles + car.Glass.Triangles + parts + wheels;
            Console.WriteLine($"driven body {car.Near.Triangles}, glass {car.Glass.Triangles}, parts {parts}, wheels {wheels}: {all}");
            Assert.Less(all, 120000, "VEHICLE_MODEL_SPEC.md: a player car");
            Assert.Greater(Part(car, VehicleFamilies.CabinPart).Mesh.Triangles, 10000, "a real cabin");
        }

        [Test]
        public void TheCarIsTheSizeOfItsModel()
        {
            var car = Car();
            Assert.AreEqual(4.76, car.Length, 0.03);
            Assert.AreEqual(1.8, car.Width, 0.03);
            Assert.AreEqual(1.47, car.Height, 0.03);
            Assert.Less(car.Wheels.Max(w => Math.Abs(w.Z)), car.Length / 2, "the wheels are under the car");
            Assert.Less(car.Wheels.Max(w => Math.Abs(w.X)), car.Width / 2);
        }

        [Test]
        public void EveryMovingPartIsThere()
        {
            var car = Car();
            foreach (var n in new[] { VehicleFamilies.CabinPart, VehicleFamilies.SteeringWheel, VehicleFamilies.SpeedNeedle, VehicleFamilies.RevNeedle, VehicleFamilies.FuelNeedle, VehicleFamilies.DriverFigure })
                Assert.Greater(Part(car, n).Mesh.Triangles, 0, n);
            Assert.AreEqual(4, car.Parts.Count(p => p.Name.StartsWith(VehicleFamilies.DoorPart) && !p.Glass), "four doors");
            Assert.AreEqual(4, car.Parts.Count(p => p.Name.StartsWith(VehicleFamilies.DoorPart) && p.Glass), "each with its window");
            foreach (var p in car.Parts.Where(p => p.Glass)) Assert.IsTrue(p.Name.EndsWith(VehicleFamilies.DoorGlass), p.Name);
            Assert.AreEqual(4, car.Wheels.Count(w => w.Hub >= 0), "a brake at each wheel");
        }

        [Test]
        public void PartsTurnAboutTheirOwnCentres()
        {
            var car = Car();
            Assert.Less(Reach(Part(car, VehicleFamilies.SteeringWheel)), 0.2, "the rim, round its column");
            Assert.Greater(Reach(Part(car, VehicleFamilies.SteeringWheel)), 0.15);
            Assert.Less(Reach(Part(car, VehicleFamilies.SpeedNeedle)), 0.07, "a needle, inside its dial");
            Assert.Less(Reach(Part(car, VehicleFamilies.FuelNeedle)), 0.03);
            Assert.Greater(Part(car, VehicleFamilies.SteeringWheel).Axis.Z, 0.5, "the column runs forward, away from the driver");
            Assert.Less(Part(car, VehicleFamilies.SteeringWheel).Axis.Y, 0, "and down");
        }

        [Test]
        public void TheDoorsHangOnTheirHingesAndOpenOutwards()
        {
            var car = Car();
            foreach (var d in car.Parts.Where(p => p.Name.StartsWith(VehicleFamilies.DoorPart)))
            {
                var m = d.Mesh;
                bool right = d.Name.Contains("Right");
                Assert.AreEqual(right, d.Pivot.X > 0, d.Name + ": on its own side");
                Assert.Greater(Math.Abs(d.Axis.Y), 0.9, d.Name + ": the hinge stands nearly upright");
                Assert.That(d.Swing, Is.InRange(45.0, 75.0), d.Name);
                if (d.Glass) continue;
                // The hinge is at the front edge, and the back of the door swings out.
                double back = double.MaxValue;
                int rear = 0;
                for (int i = 0; i < m.Pos.Count; i += 3) if (m.Pos[i + 2] < back) { back = m.Pos[i + 2]; rear = i; }
                Assert.Less(back, -0.7, d.Name + ": it reaches back from the hinge");
                var corner = new V3(m.Pos[rear], m.Pos[rear + 1], m.Pos[rear + 2]);
                var open = Turned(corner, d.Axis, d.Swing);
                Assert.Greater(Math.Abs(d.Pivot.X + open.X), Math.Abs(d.Pivot.X + corner.X) + 0.4, d.Name + ": opens out from the car");
                Assert.Greater(open.Z, corner.Z, d.Name + ": and towards the hinge");
            }
            Assert.Greater(Part(car, VehicleFamilies.DriverDoor).Pivot.X * car.Eye.X, 0, "the driver's door is on the driver's side");
            Assert.Greater(Part(car, VehicleFamilies.DriverDoor).Pivot.Z, car.Eye.Z, "and in front of the seat");
        }

        [Test]
        public void TheEyesAreInTheDriversSeatUnderTheRoof()
        {
            var car = Car();
            Assert.Greater(car.Eye.X, 0.2, "right-hand drive");
            Assert.Less(car.Eye.Y, car.Height - 0.12, "clear of the lining");
            Assert.Greater(car.Eye.Y, 0.95, "above the dash");
            Assert.Less(car.HeadTop, car.Height);
            Assert.AreEqual(Part(car, VehicleFamilies.SteeringWheel).Pivot.X, car.Eye.X, 0.02, "the wheel is in front of the driver");
        }

        // The nearest triangle of the car on the line from one point to another, short of the end; the driver is left out, for the view hides them.
        static string InTheWay(VehicleModel car, V3 from, V3 to, string own)
        {
            var d = to - from;
            double length = Math.Sqrt(d.X * d.X + d.Y * d.Y + d.Z * d.Z), best = length - 0.012;
            string found = null;
            void Look(KitMesh m, V3 at, string name)
            {
                for (int i = 0; i < m.Tris.Count; i += 3)
                {
                    V3 P(int v) => new V3(m.Pos[v * 3] + at.X, m.Pos[v * 3 + 1] + at.Y, m.Pos[v * 3 + 2] + at.Z);
                    V3 a = P(m.Tris[i]), e1 = P(m.Tris[i + 1]) - a, e2 = P(m.Tris[i + 2]) - a, u = d * (1 / length);
                    var h = new V3(u.Y * e2.Z - u.Z * e2.Y, u.Z * e2.X - u.X * e2.Z, u.X * e2.Y - u.Y * e2.X);
                    double det = e1.X * h.X + e1.Y * h.Y + e1.Z * h.Z;
                    if (Math.Abs(det) < 1e-12) continue;
                    var s = from - a;
                    double p = (s.X * h.X + s.Y * h.Y + s.Z * h.Z) / det;
                    if (p < 0 || p > 1) continue;
                    var q = new V3(s.Y * e1.Z - s.Z * e1.Y, s.Z * e1.X - s.X * e1.Z, s.X * e1.Y - s.Y * e1.X);
                    double r = (u.X * q.X + u.Y * q.Y + u.Z * q.Z) / det;
                    if (r < 0 || p + r > 1) continue;
                    double t = (e2.X * q.X + e2.Y * q.Y + e2.Z * q.Z) / det;
                    if (t > 0.02 && t < best) { best = t; found = $"{name}, {t:0.000} m from the eye"; }
                }
            }
            Look(car.Near, new V3(), "the body");
            foreach (var p in car.Parts) if (p.Name != own && p.Name != VehicleFamilies.DriverFigure) Look(p.Mesh, p.Pivot, p.Name);
            return found;
        }

        [Test]
        public void NothingStandsBetweenTheEyeAndWhatTheDriverLooksAt()
        {
            var car = Car();
            var hub = Part(car, VehicleFamilies.SteeringWheel).Pivot;
            foreach (var n in new[] { VehicleFamilies.SpeedNeedle, VehicleFamilies.RevNeedle })
                Assert.IsNull(InTheWay(car, car.Eye, Part(car, n).Pivot, n), n);
            // The rim all the way round, in the wheel's own plane.
            var column = Part(car, VehicleFamilies.SteeringWheel).Axis;
            var up = Unit(Cross(Cross(column, new V3(0, 1, 0)), column));
            for (int i = 0; i < 16; i++)
                Assert.IsNull(InTheWay(car, car.Eye, hub + Turned(up, column, i * 22.5) * 0.17, VehicleFamilies.SteeringWheel), $"the rim at {i * 22.5} degrees");
        }

        // Looks round the cabin from the eye at everything below the windows: bare paint there is the back of a body panel showing through a gap in the trim.
        [Test]
        public void NoPaintShowsInsideTheCabin()
        {
            var car = Car();
            int looked = 0, bare = 0;
            string first = null;
            for (double yaw = -80; yaw <= 80; yaw += 2.5)
                for (double down = 15; down <= 75; down += 2.5)
                {
                    double y = yaw * Math.PI / 180, p = down * Math.PI / 180;
                    var to = car.Eye + new V3(Math.Sin(y) * Math.Cos(p), -Math.Sin(p), Math.Cos(y) * Math.Cos(p)) * 3;
                    var (at, paint) = FirstSurface(car, car.Eye, to);
                    if (!at.HasValue || at.Value.Y > 0.88 || at.Value.Z > car.Eye.Z + 0.95) continue;
                    looked++;
                    if (!paint) continue;
                    bare++;
                    first ??= $"yaw {yaw}, down {down}: at {at.Value.X:0.00}, {at.Value.Y:0.00}, {at.Value.Z:0.00}";
                }
            Console.WriteLine($"{looked} surfaces looked at, {bare} bare paint; {first}");
            Assert.Greater(looked, 500);
            Assert.AreEqual(0, bare, first);
        }

        // What the eye reaches first along a line, and whether it is the paint.
        static (V3?, bool) FirstSurface(VehicleModel car, V3 from, V3 to)
        {
            var d = to - from;
            double length = Math.Sqrt(d.X * d.X + d.Y * d.Y + d.Z * d.Z), best = length;
            var u = d * (1 / length);
            bool paint = false;
            void Look(KitMesh m, V3 at)
            {
                for (int i = 0; i < m.Tris.Count; i += 3)
                {
                    V3 P(int v) => new V3(m.Pos[v * 3] + at.X, m.Pos[v * 3 + 1] + at.Y, m.Pos[v * 3 + 2] + at.Z);
                    V3 a = P(m.Tris[i]), e1 = P(m.Tris[i + 1]) - a, e2 = P(m.Tris[i + 2]) - a;
                    var h = new V3(u.Y * e2.Z - u.Z * e2.Y, u.Z * e2.X - u.X * e2.Z, u.X * e2.Y - u.Y * e2.X);
                    double det = e1.X * h.X + e1.Y * h.Y + e1.Z * h.Z;
                    if (Math.Abs(det) < 1e-12) continue;
                    var s = from - a;
                    double b = (s.X * h.X + s.Y * h.Y + s.Z * h.Z) / det;
                    if (b < 0 || b > 1) continue;
                    var q = new V3(s.Y * e1.Z - s.Z * e1.Y, s.Z * e1.X - s.X * e1.Z, s.X * e1.Y - s.Y * e1.X);
                    double c = (u.X * q.X + u.Y * q.Y + u.Z * q.Z) / det;
                    if (c < 0 || b + c > 1) continue;
                    double t = (e2.X * q.X + e2.Y * q.Y + e2.Z * q.Z) / det;
                    if (t <= 0.02 || t >= best) continue;
                    best = t;
                    paint = m.Rgba[m.Tris[i] * 4 + 3] >> 5 == (int)KitMat.Paint;
                }
            }
            Look(car.Near, new V3());
            foreach (var p in car.Parts) if (p.Name != VehicleFamilies.DriverFigure) Look(p.Mesh, p.Pivot);
            return (best < length ? from + u * best : (V3?)null, paint);
        }

        [Test]
        public void TheDialsLightWithTheLamps()
        {
            var car = Car();
            Assert.IsTrue(Part(car, VehicleFamilies.CabinPart).Mesh.Lamp.Contains((float)KitLamp.Dash), "the ticks");
            foreach (var n in new[] { VehicleFamilies.SpeedNeedle, VehicleFamilies.RevNeedle, VehicleFamilies.FuelNeedle })
                Assert.IsTrue(Part(car, n).Mesh.Lamp.Contains((float)KitLamp.Dash), n);
        }

        [Test]
        public void TheDialsCanBeSeenThroughTheWheel()
        {
            var car = Car();
            var wheel = Part(car, VehicleFamilies.SteeringWheel).Pivot;
            foreach (var n in new[] { VehicleFamilies.SpeedNeedle, VehicleFamilies.RevNeedle })
            {
                var dial = Part(car, n).Pivot;
                double t = (wheel.Z - car.Eye.Z) / (dial.Z - car.Eye.Z);
                double x = car.Eye.X + (dial.X - car.Eye.X) * t - wheel.X, y = car.Eye.Y + (dial.Y - car.Eye.Y) * t - wheel.Y;
                double r = Math.Sqrt(x * x + y * y);
                Console.WriteLine($"{n}: crosses the wheel plane {r:0.000} m from the hub, at {x:0.000}, {y:0.000}");
                Assert.Less(r, 0.16, "inside the rim");
                Assert.Greater(r, 0.06, "clear of the boss");
                Assert.Greater(y, 0.02, "above the spokes");
            }
        }
    }
}
