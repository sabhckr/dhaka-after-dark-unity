// A train on a track (2026-11-06). The track is a line the world already has, read from World/transport.json; the train is a place along it and a speed. It pulls away, runs, brakes to stand at
// the next stop, waits with its doors open and goes on, and at the end of the line it goes back, cabs being at both ends. No engine here, so tests run a whole day of it in a moment.
using System;

namespace Dad.Traffic
{
    /// <summary>A line through points: where a distance along it is.</summary>
    public sealed class TrainLine
    {
        readonly double[] x, z, at;
        public double Length => at[at.Length - 1];

        public TrainLine(double[] xz)
        {
            int n = xz.Length / 2;
            x = new double[n];
            z = new double[n];
            at = new double[n];
            for (int i = 0; i < n; i++)
            {
                x[i] = xz[2 * i];
                z[i] = xz[2 * i + 1];
                if (i > 0) at[i] = at[i - 1] + Math.Sqrt((x[i] - x[i - 1]) * (x[i] - x[i - 1]) + (z[i] - z[i - 1]) * (z[i] - z[i - 1]));
            }
        }

        /// <summary>Where this distance along is; past either end the line goes on straight.</summary>
        public void Point(double s, out double px, out double pz)
        {
            int lo = 0, hi = at.Length - 1;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) / 2;
                if (at[mid] <= s) lo = mid;
                else hi = mid;
            }
            double t = (s - at[lo]) / Math.Max(at[hi] - at[lo], 1e-9);
            px = x[lo] + (x[hi] - x[lo]) * t;
            pz = z[lo] + (z[hi] - z[lo]) * t;
        }

        /// <summary>Where a car stands with its middle here: on its two bogies, so it cuts a curve as a real car does. Heading is the unit way from the back bogie to the front one.</summary>
        public void Car(double s, double bogies, out double px, out double pz, out double hx, out double hz)
        {
            Point(s + bogies, out double fx, out double fz);
            Point(s - bogies, out double bx, out double bz);
            px = (fx + bx) / 2;
            pz = (fz + bz) / 2;
            hx = fx - bx;
            hz = fz - bz;
            double l = Math.Sqrt(hx * hx + hz * hz);
            if (l > 1e-9) { hx /= l; hz /= l; }
        }
    }

    public sealed class TrainRun
    {
        public const double Accel = 0.55, Brake = 0.7, Top = 16, Dwell = 28, DoorTime = 3;
        public enum Doing { Standing, Running }

        public readonly TrainLine Line;
        /// <summary>Where the middle of the train stands at each stop, in order along the line.</summary>
        public readonly double[] Stops;
        public double S { get; private set; }
        public double V { get; private set; }
        /// <summary>Which way the line is being run: 1 towards its end, -1 back.</summary>
        public int Way { get; private set; } = 1;
        public int Next { get; private set; }
        public Doing Now { get; private set; } = Doing.Standing;
        /// <summary>The stop it stands at, or -1.</summary>
        public int At { get; private set; }
        public double Waited { get; private set; }
        /// <summary>Open: the train has stood long enough for them to open and not yet begun to go.</summary>
        public bool DoorsOpen => Now == Doing.Standing && Waited > DoorTime && Waited < Dwell - DoorTime;
        public long Departures { get; private set; }
        public long Arrivals { get; private set; }

        public TrainRun(TrainLine line, double[] stops, int start = 0, double waited = 0)
        {
            if (stops.Length < 2) throw new ArgumentException("a train needs two stops");
            Line = line;
            Stops = stops;
            At = start;
            S = stops[start];
            Waited = waited;
            if (start == stops.Length - 1) Way = -1;
            Next = start + Way;
        }

        public void Step(double dt)
        {
            if (Now == Doing.Standing)
            {
                Waited += dt;
                if (Waited < Dwell) return;
                Now = Doing.Running;
                At = -1;
                Departures++;
            }
            double left = (Stops[Next] - S) * Way;
            // As fast as lets it stop on the mark in whole steps of braking, and no faster than it can reach by pulling away. The last centimetres at a crawl,
            // so it never waits short of the mark and comes to rest from walking pace, not from a jolt.
            double half = Brake * dt / 2;
            double most = Math.Min(Top, Math.Max(Brake * dt, Math.Sqrt(2 * Brake * Math.Max(left, 0) + half * half) - half));
            V = V < most ? Math.Min(most, V + Accel * dt) : Math.Max(most, V - Brake * dt * 1.5);
            double move = V * dt;
            if (move >= left)
            {
                S = Stops[Next];
                V = 0;
                Now = Doing.Standing;
                At = Next;
                Waited = 0;
                Arrivals++;
                if (At == Stops.Length - 1) Way = -1;
                else if (At == 0) Way = 1;
                Next = At + Way;
                return;
            }
            S += move * Way;
        }
    }
}
