// The traffic simulation (F-03): a pool of vehicles driven along the lane graph near the player, in fixed steps,
// with no engine dependency, so all of it runs and is tested headless.
//
// Car following is the Intelligent Driver Model, each vehicle's numbers from its VehicleSpec. A vehicle's S is the
// distance of its front along its lane; its body lies back along the lanes it has come by (Trail), so a bus swings
// round a corner. Lateral is its offset from the lane's centre: rickshaws keep to the left kerb, and those that
// filter slip past slower traffic where the lane has room. Between lanes of a road a vehicle changes when the lane
// ahead is slow and the gap beside it is safe.
//
// At a junction a vehicle stops at the lane's stop point unless it holds the connector it wants. A connector is
// granted only if no connector crossing it is held, the road beyond has room for the vehicle (no blocking the box),
// the phase is green where there is a controller, and nobody with a better claim is waiting: on an AllWay the one
// waiting longest goes first; at a Priority junction the lesser road waits for a gap, and nudges its way out after
// NudgeAfter seconds. A grant is kept until the vehicle's tail is off the connector, so two crossing connectors
// are never in use together. Where two lanes merge into one, the stream that has waited MergeTurn seconds gets its
// turn, and a route looks at how full the lanes ahead are, so a long queue on a one-lane road (K-57) does not starve
// the streets that feed it.
//
// Levels (FEATURE_TRANSLATION_MATRIX F-03), nearest first:
// - Near (under NearRadius): driven as below. The physics level of the design waits for the player's vehicle (E-04).
// - Mid (out to SpawnRadius): driven as below, on lanes.
// - Far: no vehicles, a count per lane, moved along once a second. Vehicles leaving the pool add to it, those
//   joining take from it, and the congestion on a road comes from it.
// - Outside: a total per chunk not in memory, kept for when it comes back.
using System;
using System.Collections.Generic;
using Dad.World;

namespace Dad.Traffic
{
    /// <summary>What a vehicle at a stop line is waiting for.</summary>
    public enum Hold : byte { None, Red, Crossing, LongerWaiter, Gap, Room }

    public sealed class Agent
    {
        public int Id;
        public VehicleSpec Spec;
        public TLane Lane;
        public double S, V, A, Lateral, LatGoal, LatBias;
        /// <summary>This driver's share of the road's speed.</summary>
        public double Eager;
        /// <summary>Lanes ahead, in order. Route[0] follows Lane.</summary>
        public readonly TLane[] Route = new TLane[TrafficSim.RouteLength];
        public int RouteCount;
        /// <summary>Lanes the body is still on, nearest first.</summary>
        public readonly TLane[] Trail = new TLane[TrafficSim.TrailLength];
        public int TrailCount;
        /// <summary>Connectors held, up to four.</summary>
        public readonly TLane[] Held = new TLane[4];
        public int HeldCount;
        public TLane WaitingFor;
        /// <summary>Why it was last refused, and for reports whose fault it was.</summary>
        public Hold Why;
        public Agent WhyAgent, Lead;
        public double WaitStart = -1, GrantTime;
        /// <summary>Seconds without making a metre of progress, and how far since the last metre counted.</summary>
        public double Stuck, Crept;
        /// <summary>Seconds at a dead stop.</summary>
        public double Still;
        /// <summary>Where this bus has stopped or will: on this lane, and until when.</summary>
        public double DwellUntil = -1, DoneStopS = -1;
        public TLane DoneStopLane;
        public double NextChange;
        public bool Leaving;
        // Pose, for drawing and checks: world metres, the body's centre on the road and its heading.
        public double X, Z, Y, Hx, Hz, PrevX, PrevZ, PrevHx, PrevHz, Steer, Spin, PrevSpin;
        public bool Braking;
        public sbyte Indicator;
        public bool Near;
    }

    /// <summary>The vehicle a player drives, as traffic sees it (E-04): world metres, heading, velocity and size.</summary>
    /// <summary>A level crossing: where the road meets the rail, how wide the road is, and whether a train has it.</summary>
    public sealed class CrossingHold
    {
        public double X, Z, Half;
        public bool Closed;
    }

    public sealed class DrivenVehicle
    {
        public double X, Z, Y, Hx, Hz, Vx, Vz, Length, Width;
    }

    public sealed class TrafficSim
    {
        public const int RouteLength = 6, TrailLength = 6;
        public const double Step = 0.1, NearRadius = 120, SpawnRadius = 320, DespawnRadius = 380, MinSpawn = 110, ViewSpawn = 230, ViewCos = 0.55;
        public const double MergeTurn = 8, NudgeAfter = 22, YieldGap = 4.5, RoomMargin = 1.5, WaitLimit = 25, GuardPatience = 20, ReleaseAfter = 50;

        /// <summary>Whoever a player is driving, or null. Traffic will not drive into it, whichever way it faces.</summary>
        public DrivenVehicle Driven;
        /// <summary>The level crossings. Nobody drives on to one a train has.</summary>
        public readonly List<CrossingHold> Crossings = new List<CrossingHold>();

        public readonly TrafficNet Net;
        public readonly List<Agent> Agents = new List<Agent>(512);
        public double Time;
        /// <summary>How many vehicles the pool may hold (200-400 in the design), and the hour of day.</summary>
        public int Budget = 300;
        public double Hour = 9;
        public double FocusX, FocusZ, ViewX, ViewZ = 1;
        /// <summary>Totals over the run.</summary>
        public long Spawned, Despawned, Exited, Released, Grants, LaneChanges;
        /// <summary>Longest wait so far without progress; and which vehicle, for reports.</summary>
        public double WorstStuck;
        public Agent WorstAgent;
        /// <summary>The Far level's total, and what the chunks not in memory hold.</summary>
        public readonly Dictionary<(int, int), double> Outside = new Dictionary<(int, int), double>();

        readonly List<TLane> pool = new List<TLane>(2048);
        readonly List<double> poolWeight = new List<double>(2048);
        double poolTotal, nextPool, nextSpawn, nextFar;
        TrafficRandom rng;
        int nextId;
        readonly Stack<Agent> spare = new Stack<Agent>(64);

        public TrafficSim(TrafficNet net, ulong seed = 1) { Net = net; rng = new TrafficRandom(seed); }

        // ---- The day ----

        /// <summary>How much of the budget is on the road at this hour: rush hours full, the small hours thin.</summary>
        public static double Density(double h)
        {
            h = ((h % 24) + 24) % 24;
            double morning = Math.Exp(-Math.Pow((h - 9) / 2.2, 2)), evening = Math.Exp(-Math.Pow((h - 18.5) / 2.6, 2)), day = h >= 7 && h <= 22 ? 0.62 : 0;
            return Math.Max(0.18, Math.Min(1, day + 0.4 * morning + 0.4 * evening));
        }

        /// <summary>How the road's speed falls in the rush, before what the Far counts add.</summary>
        public static double Congestion(double h)
        {
            h = ((h % 24) + 24) % 24;
            return 1 - 0.3 * Math.Exp(-Math.Pow((h - 9) / 1.8, 2)) - 0.34 * Math.Exp(-Math.Pow((h - 18.5) / 2.1, 2));
        }

        /// <summary>The share of each family on the road (labelled estimates): trucks are kept out of the city by day.</summary>
        public static double Mix(VehicleKind k, double h)
        {
            h = ((h % 24) + 24) % 24;
            bool night = h >= 22 || h < 6;
            switch (k)
            {
                case VehicleKind.Sedan: return night ? 0.09 : 0.1;
                case VehicleKind.Hatch: return night ? 0.05 : 0.05;
                case VehicleKind.Saloon: return night ? 0.03 : 0.03;
                case VehicleKind.Roadster: return 0.01;
                case VehicleKind.Sports: return 0.01;
                case VehicleKind.Chopper: return 0.02;
                case VehicleKind.PoliceBike: return 0.02;
                case VehicleKind.Tipper: case VehicleKind.Boat: return 0;
                case VehicleKind.Cng: return night ? 0.14 : 0.21;
                case VehicleKind.Rickshaw: return night ? 0.06 : 0.2;
                case VehicleKind.Motorcycle: return night ? 0.08 : 0.17;
                case VehicleKind.Bus: return night ? 0.05 : 0.08;
                case VehicleKind.Coach: return night ? 0.14 : 0.02;
                case VehicleKind.Truck: return night ? 0.26 : 0.01;
                default: return night ? 0.05 : 0.07;
            }
        }

        // ---- Stepping ----

        /// <summary>Puts vehicles on the road at once, anywhere near the focus, as if they had been there all along.</summary>
        public void Populate()
        {
            RebuildPool();
            int want = Target(), tries = want * 8;
            while (Agents.Count < want && tries-- > 0) TrySpawn(true);
        }

        int Target() => (int)Math.Round(Budget * Density(Hour));

        /// <summary>One fixed step of Step seconds.</summary>
        public void Advance()
        {
            Time += Step;
            foreach (var j in Net.Junctions) j.Update(Step);
            BuildCells();
            for (int i = 0; i < Agents.Count; i++) Decide(Agents[i]);
            for (int i = 0; i < Agents.Count; i++) Move(Agents[i]);
            for (int i = Agents.Count - 1; i >= 0; i--) if (Agents[i].Leaving) { Exited++; Remove(i); }
            if (Time >= nextFar) { nextFar = Time + 1; StepFar(); }
            if (Time >= nextPool) { nextPool = Time + 1; RebuildPool(); }
            if (Time >= nextSpawn) { nextSpawn = Time + 0.25; Churn(); }
        }

        void Churn()
        {
            for (int i = Agents.Count - 1; i >= 0; i--)
            {
                var a = Agents[i];
                double dx = a.X - FocusX, dz = a.Z - FocusZ, d2 = dx * dx + dz * dz;
                if (d2 > DespawnRadius * DespawnRadius) { Despawned++; a.Lane.Far += 1; Remove(i); }
                // The last resort, counted and out of sight: a vehicle that has not moved for this long is taken off so the queue behind it can go.
                else if (a.Stuck > ReleaseAfter && a.Why != Hold.Red && !InView(dx, dz, d2)) { Released++; Remove(i); }
            }
            int want = Target();
            for (int k = 0; k < 6 && Agents.Count < want; k++) TrySpawn(false);
        }

        bool InView(double dx, double dz, double d2) => d2 < NearRadius * NearRadius / 4 || (d2 < ViewSpawn * ViewSpawn && (dx * ViewX + dz * ViewZ) / Math.Sqrt(d2) > ViewCos);

        void RebuildPool()
        {
            pool.Clear();
            poolWeight.Clear();
            poolTotal = 0;
            foreach (var ch in Net.Chunks)
                foreach (var l in ch.All)
                {
                    if (l.D.Connector || l.Length < 12) continue;
                    double x = l.D.Pts[0] + l.Ox - FocusX, z = l.D.Pts[1] + l.Oz - FocusZ;
                    if (x * x + z * z > (SpawnRadius + 60) * (SpawnRadius + 60)) continue;
                    // Thicker close by: the pool is what the player sees, the Far counts are the rest.
                    double w = l.Length * RankWeight(l.D.Rank) / (1 + (x * x + z * z) / (190 * 190));
                    pool.Add(l);
                    poolTotal += w;
                    poolWeight.Add(poolTotal);
                }
        }

        static double RankWeight(byte rank) => rank <= 1 ? 4 : rank == 2 ? 3.2 : rank == 3 ? 2.6 : rank == 4 ? 1.8 : rank == 5 ? 1 : rank == 6 ? 0.55 : 0.2;

        bool TrySpawn(bool initial)
        {
            if (pool.Count == 0) return false;
            double pick = rng.Unit() * poolTotal;
            int lo = 0, hi = pool.Count - 1;
            while (lo < hi) { int m = (lo + hi) >> 1; if (poolWeight[m] < pick) lo = m + 1; else hi = m; }
            var lane = pool[lo];
            var spec = PickKind(lane);
            if (spec == null) return false;
            double s = spec.Length + 1 + rng.Unit() * Math.Max(0, lane.D.StopS - spec.Length - 8);
            if (s > lane.D.StopS - 3) return false;
            lane.At(s, out double x, out double z, out _, out _);
            double dx = x - FocusX, dz = z - FocusZ, d = Math.Sqrt(dx * dx + dz * dz);
            if (d > SpawnRadius) return false;
            if (!initial && (d < MinSpawn || InView(dx, dz, d * d))) return false;
            foreach (var c in lane.Cars) if (Math.Abs(c.S - s) < c.Spec.Length + spec.Length + 6) return false;
            foreach (var c in lane.Tails) if (s < spec.Length + 8) return false;
            var a = spare.Count > 0 ? spare.Pop() : new Agent();
            Reset(a);
            a.Id = nextId++;
            a.Spec = spec;
            a.Lane = lane;
            a.S = s;
            a.Eager = 0.85 + 0.3 * rng.Unit();
            a.V = Math.Min(spec.Top, lane.D.Speed) * (initial ? 0.5 + 0.4 * rng.Unit() : 0.7);
            a.LatBias = spec.Kind == VehicleKind.Rickshaw ? 1 : spec.Group == Access.Motorcycle ? 0.2 + 0.7 * rng.Unit() : (rng.Unit() - 0.5) * 0.3;
            a.NextChange = Time + rng.Unit() * 3;
            Insert(lane, a);
            a.LatGoal = a.Lateral = KerbLateral(a);
            Agents.Add(a);
            Spawned++;
            lane.Far = Math.Max(0, lane.Far - 1);
            Pose(a);
            a.PrevX = a.X; a.PrevZ = a.Z; a.PrevHx = a.Hx; a.PrevHz = a.Hz;
            return true;
        }

        static void Reset(Agent a)
        {
            a.RouteCount = a.TrailCount = a.HeldCount = 0;
            a.WaitingFor = null;
            a.WaitStart = -1;
            a.Stuck = a.Crept = a.Still = a.A = a.Steer = a.Spin = a.PrevSpin = 0;
            a.DwellUntil = a.DoneStopS = -1;
            a.DoneStopLane = null;
            a.Leaving = a.Braking = false;
            a.Indicator = 0;
        }

        VehicleSpec PickKind(TLane lane)
        {
            double total = 0;
            foreach (var sp in VehicleSpec.All) if (lane.CanUse(sp.Group)) total += Mix(sp.Kind, Hour);
            if (total <= 0) return null;
            double pick = rng.Unit() * total;
            foreach (var sp in VehicleSpec.All)
            {
                if (!lane.CanUse(sp.Group)) continue;
                pick -= Mix(sp.Kind, Hour);
                if (pick <= 0) return sp;
            }
            return null;
        }

        void Remove(int index)
        {
            var a = Agents[index];
            a.Lane.Cars.Remove(a);
            for (int i = 0; i < a.TrailCount; i++) a.Trail[i].Tails.Remove(a);
            ReleaseHeld(a);
            if (a.WaitingFor != null && a.WaitingFor.Waiter == a) a.WaitingFor.Waiter = null;
            int last = Agents.Count - 1;
            Agents[index] = Agents[last];
            Agents.RemoveAt(last);
            spare.Push(a);
        }

        /// <summary>Takes a chunk out of the simulation: its vehicles and any that are partly on it go, and what it held is kept as its total.</summary>
        public void RemoveChunk(TChunk ch)
        {
            double far = 0;
            foreach (var l in ch.All) far += l.Far;
            for (int i = Agents.Count - 1; i >= 0; i--)
            {
                var a = Agents[i];
                bool on = a.Lane.Chunk == ch;
                for (int k = 0; k < a.TrailCount && !on; k++) on = a.Trail[k].Chunk == ch;
                if (on) { far += 1; Despawned++; Remove(i); continue; }
                int keep = 0;
                while (keep < a.RouteCount && a.Route[keep].Chunk != ch) keep++;
                a.RouteCount = keep;
            }
            Outside[(ch.X, ch.Y)] = far;
            Net.Remove(ch);
        }

        /// <summary>Adds a chunk's lanes; what it held when it left is spread back over them.</summary>
        public TChunk AddChunk(ChunkLanes g, bool update = true)
        {
            var ch = Net.Add(g, update);
            double total = 0, held;
            if (Outside.TryGetValue((g.ChunkX, g.ChunkY), out held)) Outside.Remove((g.ChunkX, g.ChunkY));
            else held = -1;
            foreach (var l in ch.All) if (!l.D.Connector) total += l.Length * RankWeight(l.D.Rank);
            if (total <= 0) return ch;
            // A chunk seen for the first time starts at the road's usual density: about one vehicle per 120 m of a main road.
            double scale = held >= 0 ? held / total : 1.0 / 240;
            foreach (var l in ch.All) if (!l.D.Connector) l.Far = l.Length * RankWeight(l.D.Rank) * scale;
            return ch;
        }

        /// <summary>The Far level, once a second: each lane passes on what would have left it.</summary>
        void StepFar()
        {
            foreach (var ch in Net.Chunks)
                foreach (var l in ch.All)
                {
                    if (l.Far <= 0 || l.Sink) continue;
                    double moved = l.Far * Math.Min(1, l.D.Speed / Math.Max(l.Length, 1));
                    l.Far -= moved;
                    double share = moved / l.Next.Length;
                    foreach (var n in l.Next) n.Far += share;
                }
        }

        public double FarTotal()
        {
            double t = 0;
            foreach (var ch in Net.Chunks) foreach (var l in ch.All) t += l.Far;
            return t;
        }

        // ---- One vehicle's decision ----

        double DesiredSpeed(Agent a)
        {
            var l = a.Lane;
            // Far counts on the lane: past about one vehicle per 25 m the road slows, down to half its speed.
            double crowd = Math.Min(0.5, l.Far / Math.Max(l.Length, 10) * 12);
            return Math.Max(1.2, Math.Min(a.Spec.Top, l.D.Speed * a.Eager * Congestion(Hour) * (1 - crowd)));
        }

        static double Follow(VehicleSpec sp, double v, double v0, double gap, double dv, double minGap)
        {
            double want = minGap + Math.Max(0, v * sp.Headway + v * dv / (2 * Math.Sqrt(sp.Accel * sp.Brake)));
            double r = want / Math.Max(gap, 0.05), f = v / v0;
            return sp.Accel * (1 - f * f * f * f - r * r);
        }

        /// <summary>The body's half-width interval on a lane overlaps another's, allowing for where each is heading.</summary>
        static bool Beside(Agent a, Agent b, double clearance)
        {
            double aLo = Math.Min(a.Lateral, a.LatGoal) - a.Spec.Width / 2, aHi = Math.Max(a.Lateral, a.LatGoal) + a.Spec.Width / 2;
            double bLo = Math.Min(b.Lateral, b.LatGoal) - b.Spec.Width / 2, bHi = Math.Max(b.Lateral, b.LatGoal) + b.Spec.Width / 2;
            return aHi + clearance <= bLo || bHi + clearance <= aLo;
        }

        void ExtendRoute(Agent a)
        {
            var last = a.RouteCount > 0 ? a.Route[a.RouteCount - 1] : a.Lane;
            while (a.RouteCount < RouteLength)
            {
                var n = PickNext(a, last);
                if (n == null) break;
                a.Route[a.RouteCount++] = n;
                last = n;
            }
        }

        TLane PickNext(Agent a, TLane from)
        {
            var next = from.Next;
            if (next.Length == 0) return null;
            if (next.Length == 1) return next[0].CanUse(a.Spec.Group) ? next[0] : null;
            double total = 0;
            foreach (var n in next) if (n.CanUse(a.Spec.Group)) total += Weight(n);
            if (total <= 0) return null;
            double pick = rng.Unit() * total;
            TLane found = null;
            foreach (var n in next)
            {
                if (!n.CanUse(a.Spec.Group)) continue;
                found = n;
                pick -= Weight(n);
                if (pick <= 0) break;
            }
            return found;
        }

        /// <summary>Straight on and onto the greater road are likelier, and a driver who can see a queue down one road takes another if there is one.</summary>
        public static double Weight(TLane c)
        {
            double w = (c.D.Turn == 0 ? 3 : 1) * RankWeight(c.D.Rank);
            foreach (var n in c.Next)
            {
                double full = n.Cars.Count * 7 / Math.Max(n.Length, 7);
                if (full > 0.5) w /= 1 + 6 * (full - 0.5);
            }
            return w;
        }

        void Decide(Agent a)
        {
            var sp = a.Spec;
            ExtendRoute(a);
            double v = a.V, v0 = DesiredSpeed(a);
            double f = v / v0, acc = sp.Accel * (1 - f * f * f * f);
            double reach = 14 + v * 3 + v * v / (2 * sp.Brake);
            // Ahead on this lane.
            var lane = a.Lane;
            int mine = lane.Cars.IndexOf(a);
            double leadGap = double.MaxValue;
            Agent lead = null;
            for (int i = mine + 1; i < lane.Cars.Count; i++)
            {
                var o = lane.Cars[i];
                double gap = o.S - o.Spec.Length - a.S;
                if (gap > reach) break;
                if (Beside(a, o, 0.25)) continue;
                lead = o;
                leadGap = gap;
                break;
            }
            foreach (var o in lane.Tails)
            {
                if (Beside(a, o, 0.25)) continue;
                double gap = RearOn(o, lane) - a.S;
                if (gap >= -a.Spec.Length && gap < leadGap) { lead = o; leadGap = Math.Max(gap, 0.01); }
            }
            bool stopped = false;
            double start = lane.Length - a.S; // distance from the front to the end of `lane`
            var at = lane;
            for (int r = 0; ; r++)
            {
                var next = r < a.RouteCount ? a.Route[r] : null;
                // Buses pull in at their stop.
                if (at == a.Lane && sp.Kind == VehicleKind.Bus) acc = Math.Min(acc, BusStop(a, sp, v, v0));
                if (next != null && next.D.Connector && at.Junction != null && !Holds(a, next))
                {
                    // Past the stop line (a lane change, a queue that crept on) the lane's end is the stop.
                    double stop = at == a.Lane && a.S > at.D.StopS ? start : start - (at.Length - at.D.StopS);
                    double decide = v * v / (2 * sp.Brake) + v + 4;
                    bool queued = lead != null && leadGap < stop && !HoldsLane(lead, next);
                    if (stop > decide || queued || !TryGrant(a, at, next))
                    {
                        acc = Math.Min(acc, Follow(sp, v, v0, Math.Max(stop - 0.2, 0.02) + 0.5, v, 0.5));
                        stopped = true;
                    }
                }
                if (next == null)
                {
                    // Nothing further this vehicle can use, though the lane goes on: it stops short of the end, or leaves.
                    if (!at.Sink && !at.D.OpenEnd) acc = Math.Min(acc, Follow(sp, v, v0, Math.Max(start, 0.02), v, 0.5));
                    break;
                }
                if (stopped || start > reach) break;
                // The way ahead: the next lane's limit, and whoever is on it.
                double limit = next.D.Speed;
                if (v > limit)
                {
                    double need = (limit * limit - v * v) / (2 * Math.Max(start, 1));
                    acc = Math.Min(acc, Math.Max(need, -sp.Brake));
                }
                FindLead(a, next, start, ref lead, ref leadGap);
                if (at.Next.Length > 1)
                    foreach (var sib in at.Next)
                    {
                        if (sib == next) continue;
                        FindLead(a, sib, start, ref lead, ref leadGap, 7);
                    }
                start += next.Length;
                at = next;
            }
            Guard(a, ref lead, ref leadGap);
            a.Lead = lead;
            if (a.HeldCount > 0 && v < 0.3 && Time - a.GrantTime > 2.5) ReleaseUnreached(a);
            if (lead != null) acc = Math.Min(acc, Follow(sp, v, v0, leadGap, v - lead.V, sp.MinGap));
            if (Driven != null) acc = Math.Min(acc, AvoidDriven(a, v, v0));
            if (Crossings.Count > 0) acc = Math.Min(acc, AvoidCrossing(a, v, v0));
            a.A = Math.Max(acc, -2.4 * sp.Brake);
            if (Time >= a.NextChange) { a.NextChange = Time + 1 + rng.Unit(); ConsiderChange(a, lead, leadGap, v0); }
            SteerLateral(a, lead, leadGap);
        }

        /// <summary>
        /// Whatever the lanes say, nobody drives into the back of what is in front of them. Lanes do not know of each other where two ways are drawn
        /// over each other (K-31), and a vehicle changing lanes is still partly in the old one: here each looks down its own path at the
        /// vehicles near it going the same way.
        /// </summary>
        void Guard(Agent a, ref Agent lead, ref double leadGap)
        {
            var sp = a.Spec;
            // Twenty seconds behind something that will not move: whatever it is, it is not traffic ahead.
            if (a.Stuck > GuardPatience) return;
            double look = sp.Length / 2 + 6 + a.V * 2;
            double frontX = a.X + a.Hx * sp.Length / 2, frontZ = a.Z + a.Hz * sp.Length / 2;
            int cx = (int)Math.Floor(a.X / Cell), cz = (int)Math.Floor(a.Z / Cell), reach = 1 + (int)(look / Cell);
            for (int ix = cx - reach; ix <= cx + reach; ix++)
                for (int iz = cz - reach; iz <= cz + reach; iz++)
                    for (int i = cellHead[Bucket(ix, iz)]; i >= 0; i = cellNext[i])
                    {
                        var o = Agents[i];
                        if (o == a || Math.Abs(o.Y - a.Y) > 2) continue;
                        // One waiting at a stop line with its nose out is waiting for us to clear: it is not in the way.
                        if (o.WaitingFor != null && a.Lane.D.Connector) continue;
                        double cos = a.Hx * o.Hx + a.Hz * o.Hz;
                        if (cos < 0.4) continue;
                        double dx = o.X - frontX, dz = o.Z - frontZ, ahead = dx * a.Hx + dz * a.Hz, side = Math.Abs(-dx * a.Hz + dz * a.Hx);
                        double reachBack = o.Spec.Length / 2 * cos + o.Spec.Width / 2 * Math.Sqrt(Math.Max(0, 1 - cos * cos));
                        double gap = ahead - reachBack;
                        // Well past our nose it is ahead of us; level with us it is beside us, and lanes and laterals deal with that.
                        if (ahead < 0.5 || gap > look || gap >= leadGap) continue;
                        double wide = o.Spec.Length / 2 * Math.Sqrt(Math.Max(0, 1 - cos * cos)) + o.Spec.Width / 2 * cos;
                        if (side > sp.Width / 2 + wide + 0.15) continue;
                        lead = o;
                        leadGap = Math.Max(gap, 0.01);
                    }
        }

        /// <summary>
        /// What the driven vehicle asks of this one. It is not on a lane and can face any way, so it is taken as a box in the path: this
        /// vehicle follows it at the speed it is making along that path, which across the path is none. Patience does not apply.
        /// </summary>
        double AvoidDriven(Agent a, double v, double v0)
        {
            var d = Driven;
            var sp = a.Spec;
            if (Math.Abs(d.Y - a.Y) > 2) return double.MaxValue;
            double look = sp.Length / 2 + 8 + v * 2.5;
            double dx = d.X - (a.X + a.Hx * sp.Length / 2), dz = d.Z - (a.Z + a.Hz * sp.Length / 2);
            double ahead = dx * a.Hx + dz * a.Hz, side = Math.Abs(-dx * a.Hz + dz * a.Hx);
            double cos = Math.Abs(a.Hx * d.Hx + a.Hz * d.Hz), sin = Math.Sqrt(Math.Max(0, 1 - cos * cos));
            double reach = d.Length / 2 * cos + d.Width / 2 * sin, wide = d.Length / 2 * sin + d.Width / 2 * cos;
            double gap = ahead - reach;
            if (ahead + reach < 0 || gap > look || side > sp.Width / 2 + wide + 0.3) return double.MaxValue;
            double along = Math.Max(0, d.Vx * a.Hx + d.Vz * a.Hz);
            return Follow(sp, v, v0, Math.Max(gap, 0.01), v - along, sp.MinGap);
        }

        /// <summary>Stop short of a shut crossing; anyone already on it carries on off it.</summary>
        double AvoidCrossing(Agent a, double v, double v0)
        {
            const double StopLine = 5;
            double best = double.MaxValue;
            var sp = a.Spec;
            foreach (var h in Crossings)
            {
                if (!h.Closed) continue;
                double dx = h.X - a.X, dz = h.Z - a.Z;
                double ahead = dx * a.Hx + dz * a.Hz, side = Math.Abs(-dx * a.Hz + dz * a.Hx);
                if (side > h.Half + 3) continue;
                double gap = ahead - StopLine - sp.Length / 2;
                if (gap < -1.5 || gap > 12 + v * 4) continue;
                best = Math.Min(best, Follow(sp, v, v0, Math.Max(gap, 0.01), v, sp.MinGap));
            }
            return best;
        }

        const double Cell = 16;
        const int Buckets = 1024;
        readonly int[] cellHead = new int[Buckets];
        int[] cellNext = new int[512];

        static int Bucket(int x, int z) => (int)(((uint)(x * 73856093) ^ (uint)(z * 19349663)) & (Buckets - 1));

        void BuildCells()
        {
            if (cellNext.Length < Agents.Count) cellNext = new int[Agents.Count * 2];
            for (int i = 0; i < Buckets; i++) cellHead[i] = -1;
            for (int i = 0; i < Agents.Count; i++)
            {
                var a = Agents[i];
                int b = Bucket((int)Math.Floor(a.X / Cell), (int)Math.Floor(a.Z / Cell));
                cellNext[i] = cellHead[b];
                cellHead[b] = i;
            }
        }

        double BusStop(Agent a, VehicleSpec sp, double v, double v0)
        {
            var stops = a.Lane.D.BusStops;
            if (a.Lane.D.Index != 0 || stops.Length == 0) return double.MaxValue;
            if (a.DwellUntil >= 0)
            {
                if (Time < a.DwellUntil) return -sp.Brake;
                a.DwellUntil = -1;
                a.DoneStopLane = a.Lane;
                a.DoneStopS = a.S;
                return double.MaxValue;
            }
            foreach (double s in stops)
            {
                if (a.DoneStopLane == a.Lane && a.DoneStopS >= s - 1) continue;
                double gap = s - a.S;
                if (gap < -1 || gap > 60) continue;
                if (gap < 1 && v < 0.4) { a.DwellUntil = Time + 8 + 8 * rng.Unit(); return -sp.Brake; }
                return Follow(sp, v, v0, Math.Max(gap, 0.02) + 0.5, v, 0.5);
            }
            return double.MaxValue;
        }

        static bool Holds(Agent a, TLane c)
        {
            for (int i = 0; i < a.HeldCount; i++) if (a.Held[i] == c) return true;
            return false;
        }

        static bool HoldsLane(Agent a, TLane c) => Holds(a, c) && a.Lane != c;

        /// <summary>The rearmost body on a lane ahead (at distance `start` from a's front), if nearer than what is known. With within, only bodies that have not got that far along it.</summary>
        void FindLead(Agent a, TLane l, double start, ref Agent lead, ref double leadGap, double within = double.MaxValue)
        {
            for (int i = 0; i < l.Cars.Count; i++)
            {
                var o = l.Cars[i];
                if (o == a) continue;
                double rear = o.S - o.Spec.Length;
                if (rear > within) break;
                double gap = start + rear;
                if (gap < leadGap) { lead = o; leadGap = Math.Max(gap, 0.01); }
                break;
            }
            foreach (var o in l.Tails)
            {
                if (o == a) continue;
                double rear = RearOn(o, l);
                if (rear > within) continue;
                double gap = start + rear;
                if (gap < leadGap) { lead = o; leadGap = Math.Max(gap, 0.01); }
            }
        }

        /// <summary>Where on lane l (which is on its Trail) this vehicle's rear is: negative if it has not reached it.</summary>
        static double RearOn(Agent o, TLane l)
        {
            double r = o.S - o.Spec.Length;
            for (int i = 0; i < o.TrailCount; i++)
            {
                r += o.Trail[i].Length;
                if (o.Trail[i] == l) return r;
            }
            return r;
        }

        /// <summary>
        /// Asks for the connector c from lane `from`. A lane too short to hold this vehicle cannot be waited on: the connector after it is
        /// asked for at the same time, so a bus does not stop with its tail across the junction it has just crossed.
        /// </summary>
        bool TryGrant(Agent a, TLane from, TLane c)
        {
            if (a.WaitStart < 0) a.WaitStart = Time;
            int chain = 1, at = 0;
            while (at < a.RouteCount && a.Route[at] != c) at++;
            bool ok = Clear(a, from, c, at);
            int r = at;
            double need = a.Spec.Length + RoomMargin;
            while (ok && chain < 3 && r + 2 < a.RouteCount)
            {
                var exit = a.Route[r + 1];
                var after = a.Route[r + 2];
                if (exit.Length >= need || !after.D.Connector || exit.Junction == null) break;
                if (!Holds(a, after)) ok = Clear(a, exit, after, r + 2);
                if (ok) { chain++; r += 2; }
            }
            if (ok && a.HeldCount + chain > a.Held.Length) ok = false;
            if (!ok)
            {
                if (a.WaitingFor != c)
                {
                    if (a.WaitingFor != null && a.WaitingFor.Waiter == a) a.WaitingFor.Waiter = null;
                    a.WaitingFor = c;
                }
                if (c.Waiter == null || c.Waiter.WaitingFor != c || a.WaitStart < c.WaiterSince) { c.Waiter = a; c.WaiterSince = a.WaitStart; }
                return false;
            }
            for (int i = 0, k = at; i < chain; i++, k += 2)
            {
                var h = a.Route[k];
                if (Holds(a, h)) continue;
                a.Held[a.HeldCount++] = h;
                h.Reserved++;
                h.Booked += a.Spec.Length + a.Spec.MinGap;
            }
            a.GrantTime = Time;
            if (a.WaitingFor != null && a.WaitingFor.Waiter == a) a.WaitingFor.Waiter = null;
            a.WaitingFor = null;
            a.WaitStart = -1;
            Grants++;
            return true;
        }

        /// <summary>May this vehicle take connector c (Route[at]) from lane `from` now?</summary>
        bool Clear(Agent a, TLane from, TLane c, int at)
        {
            var j = from.Junction;
            a.WhyAgent = null;
            a.Why = Hold.Red;
            if (j.Controlled && !j.Open(from.D.Phase)) return false;
            a.Why = Hold.Crossing;
            bool yields = from.D.Yield;
            double waited = Time - a.WaitStart;
            foreach (var k in c.Conflicts)
            {
                if (k.Reserved > 0) return false;
                var w = k.Waiter;
                if (w == null || w.WaitingFor != k) continue;
                a.Why = Hold.LongerWaiter;
                a.WhyAgent = w;
                double theirs = Time - k.WaiterSince;
                if (j.Controlled && w.Lane.D.Phase >= 0 && !j.Open(w.Lane.D.Phase)) continue;
                // Two queues into one road go in turn: whoever waits for the same room and was there first has the next gap, or
                // the stream that never stops takes every gap and the other queue starves. No ring can come of it: both want one thing.
                if (w.Why == Hold.Room && w.Lane.D.StopS - w.S <= 4 && k.Next.Length > 0 && c.Next.Length > 0 && k.Next[0] == c.Next[0] && theirs > MergeTurn && theirs > waited) return false;
                if (w.Why == Hold.Room || w.Lane.D.StopS - w.S > 4) continue; // it could not go anyway
                if (waited > WaitLimit) continue; // courtesy has its limits: a ring of drivers each waving the next on would never end
                // Whoever has waited longest; a lesser road's vehicle has first to wait out its nudge.
                bool wYields = w.Lane.D.Yield;
                if (wYields && !yields) { if (theirs > NudgeAfter) return false; continue; }
                if (yields && !wYields) return false;
                if (k.WaiterSince < a.WaitStart) return false;
            }
            a.Why = Hold.Gap;
            if (yields && waited < NudgeAfter)
                foreach (var k in c.Conflicts)
                    foreach (var p in k.Prev)
                        if (!p.D.Yield && Coming(p, k)) return false;
            a.Why = Hold.Room;
            // Room beyond: no stopping in the junction.
            double need = a.Spec.Length + RoomMargin, room = 0;
            for (int r = at + 1; ; r++)
            {
                // The route's end says nothing against going.
                if (r >= a.RouteCount) { room = double.MaxValue; break; }
                var l = a.Route[r];
                double free = Room(l, ref a.WhyAgent);
                if (free < l.Length - 0.01) { room += free; break; }
                room += l.Length;
                if (room >= need + c.Booked) break;
            }
            if (room - c.Booked < need) return false;
            a.Why = Hold.None;
            return true;
        }

        /// <summary>How far along lane l is free of vehicles, as they will be a moment from now.</summary>
        static double Room(TLane l, ref Agent who)
        {
            double room = l.Length;
            if (l.Cars.Count > 0)
            {
                var o = l.Cars[0];
                double rear = o.S - o.Spec.Length + o.V * 1.5;
                if (rear < room) { room = rear; who = o; }
            }
            foreach (var o in l.Tails)
            {
                double rear = RearOn(o, l) + o.V * 1.5;
                if (rear < room) { room = rear; who = o; }
            }
            return room;
        }

        /// <summary>Gives up what it has been cleared for and has not reached: stopped behind someone, it should not keep the junction from others.</summary>
        void ReleaseUnreached(Agent a)
        {
            // Inside the junction it is too late: it must be let through.
            if (a.Lane.D.Connector || a.Lane.Length < a.Spec.Length + RoomMargin) return;
            for (int i = a.HeldCount - 1; i >= 0; i--)
            {
                var h = a.Held[i];
                if (h == a.Lane) continue;
                bool passed = false;
                for (int t = 0; t < a.TrailCount && !passed; t++) passed = a.Trail[t] == h;
                if (passed) continue;
                h.Reserved--;
                h.Booked -= a.Spec.Length + a.Spec.MinGap;
                a.Held[i] = a.Held[--a.HeldCount];
            }
        }

        /// <summary>Is someone on lane p about to take connector k, within the gap a yielding driver wants?</summary>
        static bool Coming(TLane p, TLane k)
        {
            for (int i = p.Cars.Count - 1; i >= 0; i--)
            {
                var o = p.Cars[i];
                if (o.RouteCount == 0 || o.Route[0] != k) continue;
                double d = p.Length - o.S;
                return d < Math.Max(o.V, 2) * YieldGap;
            }
            return false;
        }

        // ---- Lanes and lateral ----

        double KerbLateral(Agent a)
        {
            double room = Math.Max(0, (a.Lane.D.Width - a.Spec.Width) / 2 - 0.15);
            return Math.Max(-room, Math.Min(room, a.LatBias * room));
        }

        void ConsiderChange(Agent a, Agent lead, double leadGap, double v0)
        {
            var l = a.Lane;
            if (l.D.Connector || a.TrailCount > 0 || a.DwellUntil >= 0) return;
            if (a.S > l.D.StopS - 25 || Math.Abs(a.Lateral - a.LatGoal) > 0.3) return;
            bool keepLeft = a.Spec.Kind == VehicleKind.Rickshaw || a.Spec.Kind == VehicleKind.Bus || a.Spec.Kind == VehicleKind.Truck;
            if (keepLeft && l.Left != null && Safe(a, l.Left)) { Change(a, l.Left); return; }
            if (lead == null || lead.V > 0.75 * v0 || leadGap > 25) return;
            var first = a.Id % 2 == 0 ? l.Right : l.Left;
            var second = a.Id % 2 == 0 ? l.Left : l.Right;
            if (first != null && Safe(a, first)) Change(a, first);
            else if (second != null && Safe(a, second)) Change(a, second);
        }

        bool Safe(Agent a, TLane t)
        {
            if (!t.CanUse(a.Spec.Group)) return false;
            double s = a.S * t.Length / a.Lane.Length;
            if (s < a.Spec.Length + 2 || s > t.D.StopS - 20) return false;
            foreach (var o in t.Cars)
            {
                double ahead = o.S - o.Spec.Length - s, behind = s - a.Spec.Length - o.S;
                if (ahead > 0 ? ahead < a.Spec.MinGap + 0.6 * a.V + Math.Max(0, a.V - o.V) * 1.5 : behind < o.Spec.MinGap + 0.8 * o.V + Math.Max(0, o.V - a.V) * 2.5) return false;
            }
            foreach (var o in t.Tails) if (RearOn(o, t) > s - a.Spec.Length - 3) return false;
            return true;
        }

        void Change(Agent a, TLane t)
        {
            // The body stays where it is and eases across: its lateral is measured from the new lane now.
            a.Lane.At(a.S, out double x0, out double z0, out double dx, out double dz);
            double s = a.S * t.Length / a.Lane.Length;
            t.At(s, out double x1, out double z1, out _, out _);
            double offset = -dz * (x0 - x1) + dx * (z0 - z1);
            a.Lane.Cars.Remove(a);
            a.Lane = t;
            a.S = s;
            Insert(t, a);
            a.Lateral += offset;
            a.RouteCount = 0;
            ReleaseHeld(a);
            a.LatGoal = KerbLateral(a);
            LaneChanges++;
        }

        static void ReleaseHeld(Agent a)
        {
            for (int i = 0; i < a.HeldCount; i++) { a.Held[i].Reserved--; a.Held[i].Booked -= a.Spec.Length + a.Spec.MinGap; }
            a.HeldCount = 0;
        }

        void SteerLateral(Agent a, Agent lead, double leadGap)
        {
            double goal = KerbLateral(a);
            var sp = a.Spec;
            double room = Math.Max(0, (a.Lane.D.Width - sp.Width) / 2 - 0.1);
            // Filtering: where the slower one ahead leaves room on its right, go by.
            if (sp.Filters && lead != null && lead.Lane == a.Lane && leadGap < 14 && lead.V < a.V + 1.5 && lead.V < DesiredSpeed(a) * 0.8)
            {
                double need = (sp.Width + lead.Spec.Width) / 2 + 0.3, past = lead.Lateral - need;
                if (past >= -room) goal = past;
            }
            // Never slide into someone alongside.
            foreach (var o in a.Lane.Cars)
            {
                if (o == a) continue;
                double along = Math.Abs(o.S - o.Spec.Length / 2 - (a.S - sp.Length / 2));
                if (along > (o.Spec.Length + sp.Length) / 2 + 0.5) continue;
                double reachLo = Math.Min(goal, a.Lateral) - sp.Width / 2 - 0.2, reachHi = Math.Max(goal, a.Lateral) + sp.Width / 2 + 0.2;
                if (reachHi > o.Lateral - o.Spec.Width / 2 && reachLo < o.Lateral + o.Spec.Width / 2) { goal = a.Lateral; break; }
            }
            a.LatGoal = goal;
        }

        static void Insert(TLane l, Agent a)
        {
            int i = l.Cars.Count;
            while (i > 0 && l.Cars[i - 1].S > a.S) i--;
            l.Cars.Insert(i, a);
        }

        // ---- Moving ----

        void Move(Agent a)
        {
            a.PrevX = a.X; a.PrevZ = a.Z; a.PrevHx = a.Hx; a.PrevHz = a.Hz; a.PrevSpin = a.Spin;
            double v = Math.Max(0, a.V + a.A * Step), ds = (a.V + v) / 2 * Step;
            if (a.V + a.A * Step < 0) ds = a.V * a.V / (2 * Math.Max(-a.A, 1e-6));
            a.V = v;
            a.S += ds;
            a.Spin += ds / a.Spec.WheelRadius;
            a.Braking = a.A < -0.7 || v < 0.3;
            double lat = a.Spec.Filters ? 1.4 : 1.1;
            double dl = a.LatGoal - a.Lateral, most = lat * Step * (0.3 + Math.Min(1, v / 3));
            a.Lateral += Math.Abs(dl) < most ? dl : Math.Sign(dl) * most;
            while (a.S >= a.Lane.Length)
            {
                ExtendRoute(a);
                if (a.RouteCount == 0)
                {
                    a.Leaving = true;
                    a.S = a.Lane.Length - 0.01;
                    break;
                }
                // Nobody crosses into a junction uncleared, however slowly.
                if (a.Route[0].D.Connector && a.Lane.Junction != null && !Holds(a, a.Route[0]))
                {
                    a.S = a.Lane.Length - 0.01;
                    a.V = 0;
                    break;
                }
                Advance(a);
            }
            // Keep the lane's list in order.
            var cars = a.Lane.Cars;
            int i = cars.IndexOf(a);
            while (i + 1 < cars.Count && cars[i + 1].S < a.S) { cars[i] = cars[i + 1]; cars[i + 1] = a; i++; }
            DropTrail(a);
            a.Crept += ds;
            a.Still = a.V < 0.05 ? a.Still + Step : 0;
            if (a.Crept >= 1) { a.Crept = 0; a.Stuck = 0; }
            else if (a.DwellUntil < 0)
            {
                a.Stuck += Step;
                if (a.Stuck > WorstStuck) { WorstStuck = a.Stuck; WorstAgent = a; }
            }
            Pose(a);
            var next = a.RouteCount > 0 ? a.Route[0] : null;
            a.Indicator = next != null && next.D.Connector && a.Lane.Length - a.S < 40 ? next.D.Turn : a.Lane.D.Connector ? a.Lane.D.Turn : (sbyte)0;
        }

        void Advance(Agent a)
        {
            var from = a.Lane;
            var to = a.Route[0];
            Array.Copy(a.Route, 1, a.Route, 0, a.RouteCount - 1);
            a.RouteCount--;
            // A join that does not line up is taken out by easing across.
            from.At(from.Length, out double x0, out double z0, out double dx, out double dz);
            to.At(0, out double x1, out double z1, out _, out _);
            a.Lateral += -dz * (x0 - x1) + dx * (z0 - z1);
            a.S -= from.Length;
            from.Cars.Remove(a);
            a.Lane = to;
            Insert(to, a);
            if (a.TrailCount == TrailLength)
            {
                var old = a.Trail[--a.TrailCount];
                Dropped(a, old);
            }
            Array.Copy(a.Trail, 0, a.Trail, 1, a.TrailCount);
            a.Trail[0] = from;
            a.TrailCount++;
            from.Tails.Add(a);
            a.LatGoal = KerbLateral(a);
        }

        /// <summary>Lets go of lanes the body has got clear of.</summary>
        static void DropTrail(Agent a)
        {
            double r = a.S - a.Spec.Length;
            int keep = 0;
            while (keep < a.TrailCount && r < 0) { r += a.Trail[keep].Length; keep++; }
            while (a.TrailCount > keep) Dropped(a, a.Trail[--a.TrailCount]);
        }

        static void Dropped(Agent a, TLane l)
        {
            l.Tails.Remove(a);
            for (int i = 0; i < a.HeldCount; i++)
                if (a.Held[i] == l)
                {
                    l.Reserved--;
                    l.Booked -= a.Spec.Length + a.Spec.MinGap;
                    a.Held[i] = a.Held[--a.HeldCount];
                    break;
                }
        }

        /// <summary>The point of the road the given distance behind the front.</summary>
        static void Behind(Agent a, double back, out double x, out double z, out double dx, out double dz)
        {
            var l = a.Lane;
            double s = a.S - back;
            for (int i = 0; s < 0 && i < a.TrailCount; i++)
            {
                l = a.Trail[i];
                s += l.Length;
            }
            l.At(s, out x, out z, out dx, out dz);
        }

        void Pose(Agent a)
        {
            var sp = a.Spec;
            double overhang = (sp.Length - sp.WheelBase) / 2;
            Behind(a, overhang, out double fx, out double fz, out double fdx, out double fdz);
            Behind(a, overhang + sp.WheelBase, out double rx, out double rz, out double rdx, out double rdz);
            // Each axle on the road, set off by the lateral.
            fx += -fdz * a.Lateral;
            fz += fdx * a.Lateral;
            rx += -rdz * a.Lateral;
            rz += rdx * a.Lateral;
            double hx = fx - rx, hz = fz - rz, d = Math.Sqrt(hx * hx + hz * hz);
            if (d > 1e-6) { a.Hx = hx / d; a.Hz = hz / d; }
            a.X = (fx + rx) / 2;
            a.Z = (fz + rz) / 2;
            a.Y = a.Lane.D.YAt(a.S);
            a.Steer = Math.Atan2(a.Hx * fdz - a.Hz * fdx, a.Hx * fdx + a.Hz * fdz);
            double cx = a.X - FocusX, cz = a.Z - FocusZ;
            a.Near = cx * cx + cz * cz < NearRadius * NearRadius;
        }
    }
}
