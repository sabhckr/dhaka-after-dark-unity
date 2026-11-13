// Which chunks to load, keep and drop, in what order. No UnityEngine dependency, no I/O:
// the driver (Dad.Core ChunkStreamer) does the loading and reports back.
//
// Rings use Chebyshev distance in cells, with hysteresis (ported from the three.js
// ChunkScheduler): a cell is wanted within LoadRadius of the focus, and dropped only at
// UnloadRadius or more, so a camera on a chunk edge does not flicker loads. With a lead
// cell (where travel is heading), cells within LoadRadius of the lead are wanted too while
// they stay inside the unload radius. Requests are taken focus ring first, then nearest.
// A load still in flight when its cell reaches the unload radius is cancelled. A failed
// load is retried once on a later update, then the cell is dead until ResetFailures.
//
//   None -> Queued -> Loading -> Ready -> Resident
//   (any) -> None on unload or cancel; Loading -> None or Dead on failure
using System;
using System.Collections.Generic;

namespace Dad.World
{
    public enum ChunkState { None, Queued, Loading, Ready, Resident, Dead }

    public sealed class StreamPlanner
    {
        public readonly int LoadRadius, UnloadRadius;
        public const int MaxAttempts = 2;

        public ChunkCell Focus { get; private set; }
        public ChunkCell? Lead { get; private set; }

        readonly Func<ChunkCell, bool> available;
        readonly Dictionary<ChunkCell, ChunkState> states = new Dictionary<ChunkCell, ChunkState>();
        readonly Dictionary<ChunkCell, int> attempts = new Dictionary<ChunkCell, int>();
        readonly List<ChunkCell> queued = new List<ChunkCell>();
        readonly List<ChunkCell> scratch = new List<ChunkCell>();

        public StreamPlanner(int loadRadius, int unloadRadius, Func<ChunkCell, bool> available)
        {
            if (loadRadius < 0 || unloadRadius <= loadRadius) throw new ArgumentException("need 0 <= loadRadius < unloadRadius");
            LoadRadius = loadRadius;
            UnloadRadius = unloadRadius;
            this.available = available ?? (_ => true);
        }

        public static int Chebyshev(ChunkCell a, ChunkCell b) => Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Z - b.Z));

        public ChunkState StateOf(ChunkCell c) => states.TryGetValue(c, out var s) ? s : ChunkState.None;

        public int Count(ChunkState s)
        {
            int n = 0;
            foreach (var kv in states) if (kv.Value == s) n++;
            return n;
        }

        public void CellsIn(ChunkState s, List<ChunkCell> into)
        {
            foreach (var kv in states) if (kv.Value == s) into.Add(kv.Key);
        }

        public bool Wanted(ChunkCell c)
        {
            if (!available(c)) return false;
            if (Chebyshev(Focus, c) <= LoadRadius) return true;
            return Lead.HasValue && Chebyshev(Lead.Value, c) <= LoadRadius && Chebyshev(Focus, c) < UnloadRadius;
        }

        /// <summary>
        /// Moves the focus (and lead) and queues newly wanted cells. Fills <paramref name="unload"/> with
        /// Ready or Resident cells at the unload radius or beyond, and <paramref name="cancel"/> with
        /// loads in flight there; all of them are back to None afterwards.
        /// </summary>
        public void Update(ChunkCell focus, ChunkCell? lead, List<ChunkCell> unload, List<ChunkCell> cancel)
        {
            Focus = focus;
            Lead = lead.HasValue && !lead.Value.Equals(focus) ? lead : null;
            RequestAround(focus);
            if (Lead.HasValue) RequestAround(Lead.Value);

            scratch.Clear();
            foreach (var kv in states)
                if (Chebyshev(focus, kv.Key) >= UnloadRadius && kv.Value != ChunkState.None && kv.Value != ChunkState.Dead)
                    scratch.Add(kv.Key);
            foreach (var c in scratch)
            {
                var s = states[c];
                if (s == ChunkState.Ready || s == ChunkState.Resident) unload.Add(c);
                else if (s == ChunkState.Loading) cancel.Add(c);
                if (s == ChunkState.Queued) queued.Remove(c);
                states.Remove(c);
            }
        }

        void RequestAround(ChunkCell centre)
        {
            int r = LoadRadius;
            for (int z = centre.Z - r; z <= centre.Z + r; z++)
            for (int x = centre.X - r; x <= centre.X + r; x++)
            {
                var c = new ChunkCell(x, z);
                if (StateOf(c) != ChunkState.None || !Wanted(c)) continue;
                states[c] = ChunkState.Queued;
                queued.Add(c);
            }
        }

        /// <summary>Takes up to <paramref name="max"/> queued cells, focus ring first, nearest first; they become Loading.</summary>
        public int TakeNext(int max, List<ChunkCell> into)
        {
            int taken = 0;
            while (taken < max && queued.Count > 0)
            {
                int best = -1;
                long bestKey = long.MaxValue;
                for (int i = 0; i < queued.Count; i++)
                {
                    long k = Priority(queued[i]);
                    if (k < bestKey) { bestKey = k; best = i; }
                }
                var c = queued[best];
                queued.RemoveAt(best);
                if (!Wanted(c)) { states.Remove(c); continue; }
                states[c] = ChunkState.Loading;
                into.Add(c);
                taken++;
            }
            return taken;
        }

        long Priority(ChunkCell c)
        {
            long ring = Chebyshev(Focus, c) <= LoadRadius ? 0 : 1;
            long dx = c.X - Focus.X, dz = c.Z - Focus.Z;
            return (ring << 40) | ((long)Chebyshev(Focus, c) << 20) | Math.Min(dx * dx + dz * dz, (1 << 20) - 1);
        }

        /// <summary>A load finished. True if it should be applied (the cell is Ready); false if it is no longer wanted.</summary>
        public bool Loaded(ChunkCell c)
        {
            if (StateOf(c) != ChunkState.Loading) return false;
            if (Chebyshev(Focus, c) >= UnloadRadius) { states.Remove(c); return false; }
            states[c] = ChunkState.Ready;
            return true;
        }

        public void Applied(ChunkCell c)
        {
            if (StateOf(c) == ChunkState.Ready) states[c] = ChunkState.Resident;
        }

        /// <summary>A load threw. Retried on a later update, until MaxAttempts; then Dead.</summary>
        public void Failed(ChunkCell c)
        {
            if (StateOf(c) != ChunkState.Loading) return;
            int n = (attempts.TryGetValue(c, out var a) ? a : 0) + 1;
            attempts[c] = n;
            if (n >= MaxAttempts) states[c] = ChunkState.Dead;
            else states.Remove(c);
        }

        public void ResetFailures()
        {
            attempts.Clear();
            scratch.Clear();
            foreach (var kv in states) if (kv.Value == ChunkState.Dead) scratch.Add(kv.Key);
            foreach (var c in scratch) states.Remove(c);
        }
    }
}
