// Is a junction locked? (F-03; FEATURE_TRANSLATION_MATRIX section 8.) More than Share of the vehicles within Radius of a junction at a dead stop for
// over StillFor seconds, with at least Fewest there to judge by. For tests and soak runs: called once a second, it counts what it finds.
using System;
using System.Collections.Generic;

namespace Dad.Traffic
{
    public sealed class GridlockWatch
    {
        public const double Radius = 45, StillFor = 90, Share = 0.15;
        public const int Fewest = 5;

        /// <summary>Junctions found locked, counted once each; the first of them for a report.</summary>
        public int Gridlocks;
        public string First;
        readonly HashSet<TJunction> found = new HashSet<TJunction>();

        public bool Sample(IEnumerable<Agent> agents, IEnumerable<TJunction> junctions)
        {
            bool any = false;
            foreach (var j in junctions)
            {
                int near = 0, still = 0;
                foreach (var a in agents)
                {
                    double dx = a.X - j.X, dz = a.Z - j.Z;
                    if (dx * dx + dz * dz > Radius * Radius) continue;
                    near++;
                    if (a.Still > StillFor) still++;
                }
                if (near < Fewest || still <= near * Share) continue;
                any = true;
                if (found.Add(j))
                {
                    Gridlocks++;
                    First ??= $"{j.D.Kind} junction at ({j.X:F0}, {j.Z:F0}): {still} of {near} still over {StillFor} s";
                }
            }
            return any;
        }
    }
}
