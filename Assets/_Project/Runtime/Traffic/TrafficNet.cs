// The lanes of the chunks in memory, joined up (F-01). ChunkLanes are built per chunk; here each becomes a TLane in
// world metres, the open ends on chunk borders are joined through the shared graph node, and for each access
// group (VehicleSpec.Access) lanes that would trap that group are marked off, so a bus is never routed into a
// lane it could not leave. A chunk can come and go at any time: its lanes are removed and the seams reopened.
using System.Collections.Generic;
using Dad.World;

namespace Dad.Traffic
{
    public sealed class TLane
    {
        public Lane D;
        public TChunk Chunk;
        public TJunction Junction;
        /// <summary>World metres of the chunk corner: a lane's points are local to it.</summary>
        public double Ox, Oz;
        public TLane[] Next = System.Array.Empty<TLane>(), Prev = System.Array.Empty<TLane>(), Conflicts = System.Array.Empty<TLane>();
        public TLane Left, Right;
        /// <summary>A bit per access group: may use this lane and still get out.</summary>
        public byte Reach;
        /// <summary>Vehicles with their front on this lane, rearmost first.</summary>
        public readonly List<Agent> Cars = new List<Agent>(4);
        /// <summary>Vehicles whose front has gone on and whose body is still on this lane.</summary>
        public readonly List<Agent> Tails = new List<Agent>(2);
        /// <summary>Vehicles cleared to use this connector and not yet off it.</summary>
        public int Reserved;
        /// <summary>The road space those vehicles will take up beyond it.</summary>
        public double Booked;
        /// <summary>The vehicle that has waited longest for this connector, and since when.</summary>
        public Agent Waiter;
        public double WaiterSince;
        /// <summary>Vehicles on this lane at the Far level, a running estimate.</summary>
        public double Far;

        public double Length => D.Length;
        public bool Sink => Next.Length == 0;
        public bool CanUse(Access g) => (Reach & (1 << (int)g)) != 0;

        public void At(double s, out double x, out double z, out double dx, out double dz)
        {
            D.At(s, out x, out z, out dx, out dz);
            x += Ox;
            z += Oz;
        }
    }

    public sealed class TChunk
    {
        public ChunkLanes Lanes;
        public TLane[] All;
        public TJunction[] Junctions;
        public int X => Lanes.ChunkX;
        public int Y => Lanes.ChunkY;
    }

    public sealed class TJunction
    {
        public const double Clear = 3, Amber = 2.5;
        public LaneJunction D;
        public double X, Z;
        public int Phase;
        public double Clock;
        /// <summary>All red between phases, for traffic in the junction to get out.</summary>
        public bool Clearing;
        public bool Controlled => D.Kind >= JunctionKind.Officer;

        public void Update(double dt)
        {
            if (!Controlled) return;
            Clock += dt;
            if (!Clearing && Clock >= D.Green) { Clearing = true; Clock = 0; }
            else if (Clearing && Clock >= Clear) { Clearing = false; Clock = 0; Phase = (Phase + 1) % D.Phases; }
        }

        public bool Open(int phase) => !Controlled || phase < 0 || (!Clearing && Phase == phase && D.Green - Clock > 1);
        /// <summary>0 red, 1 amber, 2 green, for the lamps of this phase.</summary>
        public int Lamp(int phase) => Clearing || Phase != phase ? 0 : D.Green - Clock <= Amber ? 1 : 2;
    }

    public sealed class TrafficNet
    {
        public readonly List<TChunk> Chunks = new List<TChunk>();
        public readonly List<TJunction> Junctions = new List<TJunction>();
        /// <summary>Lane ends joined across a chunk border, and those left open for want of a neighbour.</summary>
        public int SeamLinks, Sinks;
        readonly Dictionary<(long, long), List<TLane>> starts = new Dictionary<(long, long), List<TLane>>(), ends = new Dictionary<(long, long), List<TLane>>();

        public TChunk Add(ChunkLanes g, bool update = true)
        {
            var ch = new TChunk { Lanes = g, All = new TLane[g.Lanes.Length], Junctions = new TJunction[g.Junctions.Length] };
            double ox = g.CornerEastCm / 100.0, oz = g.CornerNorthCm / 100.0;
            for (int i = 0; i < g.Junctions.Length; i++)
            {
                var d = g.Junctions[i];
                ch.Junctions[i] = new TJunction { D = d, X = d.X + ox, Z = d.Z + oz };
                Junctions.Add(ch.Junctions[i]);
            }
            for (int i = 0; i < g.Lanes.Length; i++)
                ch.All[i] = new TLane { D = g.Lanes[i], Chunk = ch, Ox = ox, Oz = oz, Junction = g.Lanes[i].Junction >= 0 ? ch.Junctions[g.Lanes[i].Junction] : null };
            for (int i = 0; i < g.Lanes.Length; i++)
            {
                var l = ch.All[i];
                l.Next = Map(ch, l.D.Next);
                l.Prev = Map(ch, l.D.Prev);
                l.Conflicts = Map(ch, l.D.Conflicts);
                l.Left = l.D.Left >= 0 ? ch.All[l.D.Left] : null;
                l.Right = l.D.Right >= 0 ? ch.All[l.D.Right] : null;
                if (l.D.OpenEnd) Keyed(ends, l).Add(l);
                if (l.D.OpenStart) Keyed(starts, l).Add(l);
            }
            Chunks.Add(ch);
            foreach (var l in ch.All) if (l.D.OpenEnd) JoinKey((l.D.NodeEast, l.D.NodeNorth));
            foreach (var l in ch.All) if (l.D.OpenStart) JoinKey((l.D.NodeEast, l.D.NodeNorth));
            if (update) Update();
            return ch;
        }

        /// <summary>Takes a chunk's lanes out. Its vehicles must be gone first (TrafficSim.RemoveChunk).</summary>
        public void Remove(TChunk ch)
        {
            Chunks.Remove(ch);
            foreach (var j in ch.Junctions) Junctions.Remove(j);
            foreach (var l in ch.All)
            {
                if (l.D.OpenEnd) { var k = (l.D.NodeEast, l.D.NodeNorth); ends[k].Remove(l); SeamLinks -= l.Next.Length; foreach (var n in l.Next) n.Prev = Without(n.Prev, l); l.Next = System.Array.Empty<TLane>(); }
                if (l.D.OpenStart) { var k = (l.D.NodeEast, l.D.NodeNorth); starts[k].Remove(l); SeamLinks -= l.Prev.Length; foreach (var p in l.Prev) p.Next = Without(p.Next, l); l.Prev = System.Array.Empty<TLane>(); }
            }
            Update();
        }

        static TLane[] Map(TChunk ch, int[] ids)
        {
            if (ids.Length == 0) return System.Array.Empty<TLane>();
            var r = new TLane[ids.Length];
            for (int i = 0; i < ids.Length; i++) r[i] = ch.All[ids[i]];
            return r;
        }

        static List<TLane> Keyed(Dictionary<(long, long), List<TLane>> map, TLane l)
        {
            var k = (l.D.NodeEast, l.D.NodeNorth);
            if (!map.TryGetValue(k, out var list)) map[k] = list = new List<TLane>(4);
            return list;
        }

        /// <summary>Joins the lanes ending at a border node to those starting there in the other chunk, lane for lane from the left kerb.</summary>
        void JoinKey((long, long) k)
        {
            if (!ends.TryGetValue(k, out var e) || !starts.TryGetValue(k, out var s)) return;
            foreach (var a in e)
            {
                if (a.Next.Length > 0) continue;
                TLane best = null;
                foreach (var b in s)
                {
                    if (b.Chunk == a.Chunk) continue;
                    int want = System.Math.Min(a.D.Index, b.D.Count - 1);
                    if (b.D.Index == want) { best = b; break; }
                }
                if (best == null) continue;
                a.Next = With(a.Next, best);
                best.Prev = With(best.Prev, a);
                SeamLinks++;
            }
        }

        static TLane[] With(TLane[] a, TLane v)
        {
            var r = new TLane[a.Length + 1];
            System.Array.Copy(a, r, a.Length);
            r[a.Length] = v;
            return r;
        }

        static TLane[] Without(TLane[] a, TLane v)
        {
            int n = 0;
            foreach (var x in a) if (x != v) n++;
            var r = new TLane[n];
            n = 0;
            foreach (var x in a) if (x != v) r[n++] = x;
            return r;
        }

        /// <summary>Works out Reach from scratch: each group may use a lane that fits it and leads to one that does, or off the map.</summary>
        public void Update()
        {
            var work = new Stack<TLane>();
            Sinks = 0;
            foreach (var ch in Chunks) foreach (var l in ch.All) { l.Reach = 0; if (l.Sink) Sinks++; }
            for (int g = 0; g < (int)Access.Count; g++)
            {
                byte bit = (byte)(1 << g);
                foreach (var ch in Chunks)
                    foreach (var l in ch.All)
                        if (VehicleSpec.Allowed((Access)g, l.D.Width, l.D.Rank)) l.Reach |= bit;
                foreach (var ch in Chunks)
                    foreach (var l in ch.All)
                        if ((l.Reach & bit) != 0 && !Leads(l, bit)) work.Push(l);
                while (work.Count > 0)
                {
                    var l = work.Pop();
                    if ((l.Reach & bit) == 0) continue;
                    l.Reach &= (byte)~bit;
                    foreach (var p in l.Prev)
                        if ((p.Reach & bit) != 0 && !Leads(p, bit)) work.Push(p);
                }
            }
        }

        static bool Leads(TLane l, byte bit)
        {
            if (l.Sink) return true;
            foreach (var n in l.Next) if ((n.Reach & bit) != 0) return true;
            return false;
        }
    }
}
