// Traffic and the driven vehicle (E-04): nobody drives into it, whichever way it faces, and with nobody driving nothing changes.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dad.Traffic;
using Dad.World;
using NUnit.Framework;
using UnityEngine;

namespace Dad.Tests
{
    public class DrivenTrafficTests
    {
        static string ChunkDir => Path.Combine(TestWorld.Root, "chunks");
        static List<ChunkLanes> slice;

        static List<ChunkLanes> Slice()
        {
            if (slice != null) return slice;
            slice = new List<ChunkLanes>();
            for (int y = -30; y <= -28; y++)
                for (int x = 6; x <= 9; x++)
                {
                    var c = Dadc.Load(File.ReadAllBytes(Path.Combine(ChunkDir, $"c_{x}_{y}.dadc")));
                    NeighbourRails.Attach(c, ChunkDir);
                    slice.Add(ChunkLanes.Build(c, ChunkRoads.Build(c).Street.Blocks));
                }
            return slice;
        }

        static TrafficSim Run(ulong seed = 5, double hour = 11, int budget = 150)
        {
            var net = new TrafficNet();
            var sim = new TrafficSim(net, seed) { Hour = hour, Budget = budget, FocusX = 3750, FocusZ = 14250 };
            foreach (var l in Slice()) sim.AddChunk(l, false);
            net.Update();
            sim.Populate();
            for (int i = 0; i < 300; i++) sim.Advance();
            return sim;
        }

        // A vehicle on its way, on a lane with road ahead of it, and nothing close in front.
        static Agent Moving(TrafficSim s) => s.Agents.First(a => a.V > 6 && !a.Lane.D.Connector && a.Lane.Length - a.S > 45 && a.Lead == null);

        static DrivenVehicle Ahead(Agent a, double metres, bool across, double speed = 0)
        {
            double hx = across ? -a.Hz : a.Hx, hz = across ? a.Hx : a.Hz;
            return new DrivenVehicle { X = a.X + a.Hx * metres, Z = a.Z + a.Hz * metres, Y = a.Y, Hx = hx, Hz = hz, Vx = hx * speed, Vz = hz * speed, Length = 4.4, Width = 1.75 };
        }

        // How far the front of a is from the near side of d, along a's path.
        static double Gap(Agent a, DrivenVehicle d)
        {
            double dx = d.X - (a.X + a.Hx * a.Spec.Length / 2), dz = d.Z - (a.Z + a.Hz * a.Spec.Length / 2);
            double cos = Math.Abs(a.Hx * d.Hx + a.Hz * d.Hz), sin = Math.Sqrt(Math.Max(0, 1 - cos * cos));
            return dx * a.Hx + dz * a.Hz - (d.Length / 2 * cos + d.Width / 2 * sin);
        }

        [Test]
        public void ItStopsForACarInItsWay()
        {
            foreach (bool across in new[] { false, true })
            {
                var s = Run();
                var a = Moving(s);
                s.Driven = Ahead(a, 34, across);
                double least = double.MaxValue;
                for (int i = 0; i < 300; i++)
                {
                    s.Advance();
                    least = Math.Min(least, Gap(a, s.Driven));
                }
                Console.WriteLine($"{(across ? "across the lane" : "in line")}: stopped {least:0.00} m short, speed {a.V:0.00}");
                Assert.Greater(least, 0.3, "no contact");
                Assert.Less(a.V, 0.2);
            }
        }

        [Test]
        public void WithoutTheCarItWouldHaveGoneThrough()
        {
            var s = Run();
            var a = Moving(s);
            var d = Ahead(a, 34, true);
            for (int i = 0; i < 100; i++) s.Advance();
            Assert.Less(Gap(a, d), 0, "the spot is passed: the stop above is the car's doing, not the road's");
        }

        [Test]
        public void ItFollowsACarMovingOffAndCarriesOn()
        {
            var s = Run();
            var a = Moving(s);
            s.Driven = Ahead(a, 25, false);
            for (int i = 0; i < 40; i++) s.Advance();
            double slowed = a.V;
            var d = s.Driven;
            d.X += a.Hx * 60;
            d.Z += a.Hz * 60;
            for (int i = 0; i < 80; i++) s.Advance();
            Console.WriteLine($"speed behind it {slowed:0.0}, once it is gone {a.V:0.0}");
            Assert.Greater(a.V, slowed + 0.5);
        }

        [Test]
        public void ACarBesideOrBehindDoesNotStopIt()
        {
            var s = Run();
            var a = Moving(s);
            double v = a.V;
            s.Driven = Ahead(a, -12, false);
            for (int i = 0; i < 20; i++) s.Advance();
            Assert.Greater(a.V, v * 0.7, "behind");
            var t = Run();
            var b = Moving(t);
            double w = b.V;
            var beside = Ahead(b, 8, false);
            beside.X += -b.Hz * 6;
            beside.Z += b.Hx * 6;
            t.Driven = beside;
            for (int i = 0; i < 20; i++) t.Advance();
            Assert.Greater(b.V, w * 0.7, "a lane away");
        }

        [Test]
        public void ADeckAboveIsNotInTheWay()
        {
            var s = Run();
            var a = Moving(s);
            double v = a.V;
            var d = Ahead(a, 30, true);
            d.Y += 6;
            s.Driven = d;
            for (int i = 0; i < 100; i++) s.Advance();
            Assert.Less(Gap(a, d), 0, "it has driven on under the deck");
        }

        // A car going across is not getting out of the way along this lane, however fast it goes.
        [Test]
        public void ACarGoingAcrossIsNotTakenForOneDrivingOff()
        {
            // On a free road, so that only the car ahead slows it: the first vehicle under way that keeps its speed with nobody in front.
            // Which one that is depends on the traffic's mix of kinds, which the owner's models changed.
            double Speed(int pick, bool? across)
            {
                var s = Run();
                var a = s.Agents.Where(x => x.V > 6 && !x.Lane.D.Connector && x.Lane.Length - x.S > 45 && x.Lead == null).ElementAt(pick);
                double was = a.V;
                // Close, within the follower's look, and for half a second, while neither has moved far: what is measured is how it reads the way the car is going. Over 3 s
                // (as before 2026-11-07) a car standing 30 m ahead that only says it is leaving gets caught up with and slows the follower as much as one across the road.
                if (across != null) s.Driven = Ahead(a, 15, across.Value, 12);
                for (int i = 0; i < 5; i++) s.Advance();
                return across == null ? a.V - was : a.V;
            }
            int free = Enumerable.Range(0, 12).First(i => Speed(i, null) > -0.3);
            double crossing = Speed(free, true), leaving = Speed(free, false);
            Console.WriteLine($"speed behind one crossing {crossing:0.0}, behind one driving off {leaving:0.0}");
            Assert.Less(crossing, leaving - 0.5);
        }

        // Standing across the lane beside this one, its nose in the way: it is measured as it stands.
        [Test]
        public void ACarAcrossTheNextLaneWithItsNoseOutIsSeen()
        {
            var s = Run();
            var a = Moving(s);
            var d = Ahead(a, 34, true);
            d.X += -a.Hz * 2.4;
            d.Z += a.Hx * 2.4;
            s.Driven = d;
            for (int i = 0; i < 300; i++)
            {
                s.Advance();
                Assert.IsFalse(Overlap(a, d), $"at step {i}");
            }
        }

        [Test]
        public void NobodyEverDrivesIntoAParkedCar()
        {
            var s = Run(budget: 250);
            // A place on a busy lane.
            var a = Moving(s);
            s.Driven = Ahead(a, 40, true);
            for (int i = 0; i < 900; i++)
            {
                s.Advance();
                foreach (var o in s.Agents)
                {
                    double dx = o.X - s.Driven.X, dz = o.Z - s.Driven.Z;
                    if (dx * dx + dz * dz > 400 || Math.Abs(o.Y - s.Driven.Y) > 2) continue;
                    // The two boxes, by separating axes, on the headings of each.
                    Assert.IsFalse(Overlap(o, s.Driven), $"vehicle {o.Id} at step {i}");
                }
            }
        }

        static bool Overlap(Agent a, DrivenVehicle d)
        {
            var axes = new[] { (a.Hx, a.Hz), (-a.Hz, a.Hx), (d.Hx, d.Hz), (-d.Hz, d.Hx) };
            foreach (var (ux, uz) in axes)
            {
                double ra = a.Spec.Length / 2 * Math.Abs(a.Hx * ux + a.Hz * uz) + a.Spec.Width / 2 * Math.Abs(-a.Hz * ux + a.Hx * uz);
                double rd = d.Length / 2 * Math.Abs(d.Hx * ux + d.Hz * uz) + d.Width / 2 * Math.Abs(-d.Hz * ux + d.Hx * uz);
                if (Math.Abs((a.X - d.X) * ux + (a.Z - d.Z) * uz) > ra + rd) return false;
            }
            return true;
        }

        [Test]
        public void WithNobodyDrivingTheRunIsTheSame()
        {
            var a = Run(9, 8, 200);
            var b = Run(9, 8, 200);
            b.Driven = new DrivenVehicle { X = 1e7, Z = 1e7, Y = 0.28, Hx = 1, Length = 4, Width = 2 };
            for (int i = 0; i < 300; i++) { a.Advance(); b.Advance(); }
            Assert.AreEqual(a.Agents.Count, b.Agents.Count);
            for (int i = 0; i < a.Agents.Count; i++)
            {
                Assert.AreEqual(a.Agents[i].X, b.Agents[i].X);
                Assert.AreEqual(a.Agents[i].Z, b.Agents[i].Z);
            }
        }
    }
}
