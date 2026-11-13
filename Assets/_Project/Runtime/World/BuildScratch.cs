// What the chunk builders reuse from one chunk to the next: the lists they write meshes into,
// Earcut's nodes, and the road builder's outlines and point lists. A builder that starts from fresh
// lists grows them by doubling, and each step allocates again; on the streaming workers that was
// most of the 22.4 MB a slice run allocated, and the collections it caused bunched up just before
// each batch of chunks was applied (Phase C exit review, K-18).
//
// The streaming worker rents a set for each chunk (Rent, then Dispose on the same thread), so there
// are only as many sets as loads in flight, whichever pool threads run them. Anything else (tests,
// the main thread) gets a set of its own thread. Builders call Take at their start, copy their
// result out (ToArray) and keep no reference, so one builder at a time on a thread uses a set.
// No UnityEngine dependency: safe on worker threads.
using System;
using System.Collections.Generic;

namespace Dad.World
{
    public sealed class BuildScratch
    {
        [ThreadStatic] static BuildScratch own, rented;
        static readonly Stack<BuildScratch> spare = new Stack<BuildScratch>();
        static int created;

        public readonly List<float> Positions = new List<float>(16384);
        public readonly List<uint> Colors = new List<uint>(5600);
        public readonly List<int> Indices = new List<int>(16384);
        /// <summary>Facade coordinates, four floats a vertex (ChunkBuildings.Facade); only the building builder writes them.</summary>
        public readonly List<float> Facade = new List<float>(16384);
        /// <summary>One polygon at a time: its flattened rings, where each hole starts, its triangles.</summary>
        public readonly List<double> Flat = new List<double>(256);
        public readonly List<int> Holes = new List<int>(4);
        public readonly List<int> Tris = new List<int>(512);

        internal readonly List<Earcut.Node> EarcutNodes = new List<Earcut.Node>(1024);
        /// <summary>The road builder's own reusable state (ChunkRoads.Scratch), made on first use.</summary>
        internal object Roads;
        /// <summary>The building classifier's view of the chunk (BuildingClass.Near), made on first use.</summary>
        internal BuildingClass.Near Near;
        /// <summary>The building builder's near-facade state (ChunkBuildings.NearScratch), made on first use.</summary>
        internal ChunkBuildings.NearScratch NearFacades;

        /// <summary>Sets made so far, rented or not; the streamer's figure for how many it keeps.</summary>
        public static int Created => created;

        /// <summary>The set this thread is using: the rented one, else the thread's own.</summary>
        public static BuildScratch Current
        {
            get
            {
                if (rented != null) return rented;
                if (own == null)
                {
                    own = new BuildScratch();
                    System.Threading.Interlocked.Increment(ref created);
                }
                return own;
            }
        }

        /// <summary>The current set with its mesh and polygon lists emptied, for a builder's start.</summary>
        public static BuildScratch Take()
        {
            var s = Current;
            s.Positions.Clear();
            s.Colors.Clear();
            s.Indices.Clear();
            s.Facade.Clear();
            s.Flat.Clear();
            s.Holes.Clear();
            s.Tris.Clear();
            return s;
        }

        /// <summary>Lends this thread a set from the shared ones until the lease is disposed, on this thread.</summary>
        public static Lease Rent()
        {
            BuildScratch s = null;
            lock (spare) if (spare.Count > 0) s = spare.Pop();
            if (s == null)
            {
                s = new BuildScratch();
                System.Threading.Interlocked.Increment(ref created);
            }
            rented = s;
            return new Lease(s);
        }

        public readonly struct Lease : IDisposable
        {
            readonly BuildScratch set;
            internal Lease(BuildScratch s) { set = s; }

            public void Dispose()
            {
                if (set == null) return;
                if (rented == set) rented = null;
                lock (spare) spare.Push(set);
            }
        }
    }
}
