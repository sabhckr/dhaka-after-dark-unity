// The owner's vehicle models as the game reads them: on the road, the right way round, the size the traffic simulation thinks they are, and with lamps that work.
using System;
using System.IO;
using System.Linq;
using Dad.Traffic;
using NUnit.Framework;
using UnityEngine;

namespace Dad.Tests
{
    public class VehicleArtTests
    {
        static string Dir => Path.Combine(Application.dataPath, "_Project/Resources/Vehicles");

        static VehicleKind[] Baked() => Enum.GetValues(typeof(VehicleKind)).Cast<VehicleKind>().Where(k => File.Exists(Path.Combine(Dir, k + ".bytes"))).ToArray();
        static VehicleModel Model(VehicleKind k) => VehicleArt.Read(k, File.ReadAllBytes(Path.Combine(Dir, k + ".bytes")), 7);

        // Where the triangles of a lamp are, as the mean of their vertices.
        static (double x, double z, int n) Lamp(KitMesh m, KitLamp lamp)
        {
            double x = 0, z = 0;
            int n = 0;
            for (int v = 0; v < m.Vertices; v++)
                if (m.Lamp[v] == (float)lamp) { x += m.Pos[v * 3]; z += m.Pos[v * 3 + 2]; n++; }
            return n == 0 ? (0, 0, 0) : (x / n, z / n, n);
        }

        static (double lo, double hi) Span(KitMesh m, int axis)
        {
            double lo = double.MaxValue, hi = double.MinValue;
            for (int i = axis; i < m.Pos.Count; i += 3) { lo = Math.Min(lo, m.Pos[i]); hi = Math.Max(hi, m.Pos[i]); }
            return (lo, hi);
        }

        [Test]
        public void ThereAreBakedVehicles() => Assert.Greater(Baked().Length, 0);

        [Test]
        public void AFileFromAnotherVersionIsRefused()
        {
            var data = File.ReadAllBytes(Path.Combine(Dir, "Sedan.bytes"));
            data[4] = (byte)(VehicleArt.Version - 1);
            Assert.Throws<InvalidDataException>(() => VehicleArt.Read(VehicleKind.Sedan, data, 0));
        }

        [Test]
        public void TheFilesAreSound()
        {
            foreach (var k in Baked())
            {
                var v = Model(k);
                foreach (var (m, name) in new[] { (v.Near, "near"), (v.Far, "far") }.Concat(v.OwnWheels.Select(w => (w, "wheel"))))
                {
                    Assert.Greater(m.Triangles, 0, $"{k} {name}");
                    Assert.IsTrue(m.Tris.All(t => t >= 0 && t < m.Vertices), $"{k} {name}: index out of range");
                    for (int i = 0; i < m.Nor.Count; i += 3)
                    {
                        double l = Math.Sqrt(m.Nor[i] * m.Nor[i] + m.Nor[i + 1] * m.Nor[i + 1] + m.Nor[i + 2] * m.Nor[i + 2]);
                        Assert.AreEqual(1, l, 0.02, $"{k} {name}: normal {i / 3}");
                    }
                }
            }
        }

        [Test]
        public void ItIsTheSizeTheTrafficThinksItIs()
        {
            foreach (var k in Baked())
            {
                var v = Model(k);
                var sp = VehicleSpec.Of(k);
                var (back, front) = Span(v.Near, 2);
                foreach (var w in v.Wheels)
                {
                    var (behind, ahead) = Span(v.OwnWheels[w.Mesh - 7], 2);
                    back = Math.Min(back, w.Z + behind);
                    front = Math.Max(front, w.Z + ahead);
                }
                var (_, top) = Span(v.Near, 1);
                Console.WriteLine($"{k}: {back:0.00} to {front:0.00} long, top {top:0.00}, spec {sp.Length} x {sp.Height}");
                Assert.AreEqual(sp.Length, front - back, 0.02, k + " length");
                Assert.AreEqual(0, back + front, 0.02, k + " is centred along its length");
                Assert.AreEqual(sp.Height, top, 0.12, k + " height");
                double axles = v.Wheels.Max(w => w.Z) - (v.Wheels.Where(w => !w.Steers).Max(w => w.Z) + v.Wheels.Where(w => !w.Steers).Min(w => w.Z)) / 2;
                Assert.AreEqual(sp.WheelBase, axles, 0.08, k + " wheelbase");
                Assert.AreEqual(sp.WheelRadius, v.Wheels.Average(w => w.Y), 0.02, k + " wheels");
            }
        }

        [Test]
        public void ItStandsOnItsWheelsOnTheRoad()
        {
            foreach (var k in Baked())
            {
                var v = Model(k);
                foreach (var w in v.Wheels)
                {
                    var (bottom, _) = Span(v.OwnWheels[w.Mesh - 7], 1);
                    Assert.AreEqual(0, w.Y + bottom, 0.012, $"{k}: the tyre meets the road");
                    if (Math.Abs(w.X) > 0.03) Assert.AreEqual(w.X < 0, w.Flip, $"{k}: left wheels are turned round");
                    Assert.AreEqual(w.Z > 0, w.Steers, $"{k}: the front ones steer");
                }
                Assert.Greater(Span(v.Near, 1).lo, 0.02, k + ": the body clears the road");
            }
        }

        [Test]
        public void ItFacesForwardWithItsLampsWhereTheyBelong()
        {
            foreach (var k in Baked())
            {
                var v = Model(k);
                if (k == VehicleKind.Rickshaw) continue; // a cycle rickshaw has none, and the model none
                var head = Lamp(v.Near, KitLamp.Head);
                var brake = Lamp(v.Near, KitLamp.Brake);
                Assert.Greater(head.n, 0, k + " has headlamps");
                Assert.Greater(brake.n, 0, k + " has brake lamps");
                Assert.Greater(head.z, 0.4, k + ": headlamps at the front");
                Assert.Less(brake.z, -0.4, k + ": brake lamps at the back");
                Assert.AreEqual(0, head.x, 0.1, k + ": a pair, not one side");
                Assert.AreEqual(0, brake.x, 0.1, k + ": a pair, not one side");
                var left = Lamp(v.Near, KitLamp.Left);
                var right = Lamp(v.Near, KitLamp.Right);
                Assert.Greater(left.n, 0, k + " indicates");
                Assert.Less(left.x, -0.15, k + ": left is left");
                Assert.Greater(right.x, 0.15, k + ": right is right");
            }
        }

        [Test]
        public void ItCanBePainted()
        {
            foreach (var k in Baked())
            {
                var m = Model(k).Near;
                if (m.Map.Count > 0) continue; // its picture is its livery
                int paint = 0;
                for (int v = 0; v < m.Vertices; v++) if (m.Rgba[v * 4 + 3] >> 5 == (int)KitMat.Paint) paint++;
                Assert.Greater(paint, 200, k + ": traffic gives each vehicle its own colour through the paint");
            }
        }

        [Test]
        public void FarAwayItIsCheap()
        {
            foreach (var k in Baked())
            {
                var v = Model(k);
                Console.WriteLine($"{k}: near {v.Near.Triangles}, far {v.Far.Triangles}");
                Assert.LessOrEqual(v.Far.Triangles, 300, k.ToString());
                Assert.Less(v.Far.Triangles * 3, v.Near.Triangles + v.OwnWheels.Sum(w => w.Triangles) * v.Wheels.Length / v.OwnWheels.Length, k + ": a good deal lighter than near");
                if (k != VehicleKind.Rickshaw) Assert.Greater(Lamp(v.Far, KitLamp.Brake).n, 0, k + ": the far mesh keeps its brake lamps");
            }
        }

        [Test]
        public void ItComesFromTheModelAndNotFromCode()
        {
            // A wrong file must not pass for a vehicle.
            Assert.Throws<InvalidDataException>(() => VehicleArt.Read(VehicleKind.Sedan, new byte[64], 0));
        }
    }
}
