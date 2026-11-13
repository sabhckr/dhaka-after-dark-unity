// The gang on the streets (2026-11-12, Docs/Characters/SHOOTER_BAR_AUDIT.md section 11). Some stretches of sidewalk are a gang's turf, chosen by a hash of the chunk and
// the line, so the same streets always have them. When you come within reach of one, the gang (People/fighters.json, role "gang") are there: three standing about together, unarmed and calm,
// smoking and talking. Shots near them bring their guns out (Fighter): they fight the police when they come, and you if you have shot one of them (Heat.GangAfter). After a
// quiet half minute they put the guns away and stand about again. Far off, they go; a turf whose gang was shot down stays empty for a while.
using System.Collections.Generic;
using Dad.World;
using UnityEngine;

namespace Dad.Core
{
    public sealed class Turf : MonoBehaviour
    {
        /// <summary>Off in tests that load Boot for other things (NoAutomaticPlayer); a test about the gang turns it on.</summary>
        public static bool Automatic = true;
        /// <summary>One walking line in this many, of those long enough, has a turf.</summary>
        public const int OneIn = 8;
        public const double Longest = 20;
        /// <summary>Metres from you: a gang is put on a turf between Near and Far, and taken away beyond Leave (Fighting: still fighting).</summary>
        public const float Near = 40, Far = 120, Leave = 170, Fighting = 260;
        /// <summary>Seconds a turf stays empty after its gang was shot down.</summary>
        public const float Mourning = 300;
        static readonly string[] Loiters = { "smoke", "talk", "phone" };

        public static Turf Active { get; private set; }
        public ChunkStreamer Streamer;
        /// <summary>The gang out now, and where.</summary>
        public readonly List<Fighter> Gang = new List<Fighter>();
        public Vector3 Place { get; private set; }
        public (int, int) Chunk { get; private set; }
        /// <summary>Gangs put out, for tests and the record.</summary>
        public int Gangs { get; private set; }

        readonly Dictionary<(int, int), ChunkWalks> chunks = new Dictionary<(int, int), ChunkWalks>();
        readonly Dictionary<(int, int), float> emptyUntil = new Dictionary<(int, int), float>();
        float nextLook;

        public static Turf Attach(GameObject host, ChunkStreamer streamer)
        {
            ChunkStreamer.BuildWalks = true;
            var t = host.AddComponent<Turf>();
            t.Streamer = streamer;
            if (streamer != null)
            {
                streamer.WalksIn += t.OnWalks;
                streamer.ChunkOut += t.OnChunkOut;
            }
            return t;
        }

        void OnEnable() => Active = this;
        void OnDisable() { if (Active == this) Active = null; }

        void OnDestroy()
        {
            if (Streamer != null) { Streamer.WalksIn -= OnWalks; Streamer.ChunkOut -= OnChunkOut; }
            if (watched != null) watched.Shifted -= Shifted;
            TakeAway();
        }

        void OnWalks(ChunkCell cell, ChunkWalks w) => chunks[(cell.Ix, cell.Iy)] = w;
        void OnChunkOut(ChunkCell cell) => chunks.Remove((cell.Ix, cell.Iy));

        /// <summary>Walking lines given directly, in a test.</summary>
        public void Use(ChunkWalks w) => chunks[(w.ChunkX, w.ChunkY)] = w;

        static int Hash(int x, int y) { unchecked { int h = x * 73856093 ^ y * 19349663; h ^= h >> 13; h *= 0x5bd1e995; return h & 0x7fffffff; } }

        /// <summary>The turfs of a chunk: each line, long enough, whose hash says so, and how far along it.</summary>
        public static IEnumerable<(WalkLine line, double along, int key)> Turfs(ChunkWalks w)
        {
            for (int i = 0; i < w.Lines.Count; i++)
            {
                var line = w.Lines[i];
                int h = Hash(w.ChunkX * 131 + i, w.ChunkY);
                if (line.Length < Longest || h % OneIn != 0) continue;
                yield return (line, line.Length * (0.2 + 0.6 * ((h / OneIn) % 1000) / 1000.0), h);
            }
        }

        /// <summary>Every turf on the chunks in now, in the scene, for tests and the record.</summary>
        public List<Vector3> Places()
        {
            var all = new List<Vector3>();
            if (FloatingOrigin.Active == null) return all;
            foreach (var c in chunks)
                foreach (var t in Turfs(c.Value))
                {
                    t.line.At(t.along, out double e, out double n, out float y, out _, out _);
                    FloatingOrigin.Active.Offset.ToScene(e, n, out float x, out float z);
                    all.Add(new Vector3(x, y, z));
                }
            return all;
        }

        FloatingOrigin watched;
        void Shifted(double east, double north) => TakeAway(); // home and goal were in the old frame: out of reach anyway

        void Update()
        {
            if (Time.time < nextLook) return;
            nextLook = Time.time + 1;
            if (FloatingOrigin.Active != null && watched != FloatingOrigin.Active) { watched = FloatingOrigin.Active; watched.Shifted += Shifted; }
            var you = Where();
            if (you == null) return;
            Gang.RemoveAll(f => f == null); // bodies cleared away (Remains)
            if (Gang.Count > 0)
            {
                bool fighting = Gang.Exists(f => !f.Dead && f.Now != Fighter.Mode.Calm);
                float away = Flat(Place - you.Value).magnitude;
                if (Gang.TrueForAll(f => f.Dead)) { emptyUntil[Chunk] = Time.time + Mourning; Gang.Clear(); }
                else if (away > (fighting ? Fighting : Leave)) TakeAway();
                return;
            }
            Put(you.Value);
        }

        // You: the player, else the camera.
        static Vector3? Where()
        {
            var p = Vitals.Player;
            if (p != null && p.isActiveAndEnabled && !p.Dead) return p.transform.position;
            return Camera.main != null ? Camera.main.transform.position : (Vector3?)null;
        }

        // The nearest turf in reach, its gang put out there.
        void Put(Vector3 you)
        {
            if (FloatingOrigin.Active == null) return;
            float best = float.MaxValue;
            Vector3 at = default, along = default;
            (int, int) which = default;
            foreach (var c in chunks)
                foreach (var t in Turfs(c.Value))
                {
                    var key = (c.Key.Item1 * 4096 + c.Key.Item2, t.key);
                    if (emptyUntil.TryGetValue(key, out float until) && Time.time < until) continue;
                    t.line.At(t.along, out double e, out double n, out float y, out double de, out double dn);
                    FloatingOrigin.Active.Offset.ToScene(e, n, out float x, out float z);
                    var p = new Vector3(x, y, z);
                    float d = Flat(p - you).magnitude;
                    if (d < Near || d > Far || d >= best) continue;
                    best = d;
                    at = p;
                    along = new Vector3((float)de, 0, (float)dn);
                    which = key;
                }
            if (best == float.MaxValue) return;
            Put(at, along, which);
        }

        /// <summary>The gang out at a place, standing round it, the sidewalk going along this way.</summary>
        public void Put(Vector3 at, Vector3 along, (int, int) chunk)
        {
            var roster = NpcRoster.Load("People/fighters");
            int i = 0;
            int count = 0;
            foreach (var who in roster.people) if (who.role == "gang") count++;
            foreach (var who in roster.people)
            {
                if (who.role != "gang") continue;
                float a = (i + 0.5f) * Mathf.PI * 2 / count + Hash(chunk.Item1, chunk.Item2) % 360 * Mathf.Deg2Rad;
                var spot = at + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 1.0f;
                if (Physics.Raycast(spot + Vector3.up * 3, Vector3.down, out var ground, 8, WorldLayers.Ground | 1, QueryTriggerInteraction.Ignore)) spot.y = ground.point.y;
                var face = at - spot;
                var f = Fighter.Spawn(who, Fighter.Side.Gang, spot, Mathf.Atan2(face.x, face.z) * Mathf.Rad2Deg, calm: true);
                i++;
                if (f == null) continue;
                f.Home = spot;
                f.Loiter = Loiters[(i + Hash(chunk.Item1, chunk.Item2)) % Loiters.Length];
                f.StandAbout(); // with the clip they stand about with
                Gang.Add(f);
            }
            Place = at;
            Chunk = chunk;
            Gangs++;
            Debug.Log($"[Dad] gang: {Gang.Count} on their turf at {at} (chunk {chunk.Item1},{chunk.Item2})");
        }

        /// <summary>Takes the gang away, living and dead.</summary>
        public void TakeAway()
        {
            foreach (var f in Gang) if (f != null) Destroy(f.gameObject);
            Gang.Clear();
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0, v.z);
    }
}
