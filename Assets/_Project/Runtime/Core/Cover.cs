// Cover (2026-11-12, Docs/Characters/SHOOTER_BAR_AUDIT.md section 11): a place beside something solid, a wall, a car, a barrier, where someone crouching cannot be seen
// from where the shots come, and from where they can fire back, standing up over it (low cover) or stepping out round its end (a wall). Found on the walking mesh
// (Navigation): its edges are where it meets what is in the way. Places near and reachable are tried, the nearest that will do is taken, and one taken is kept from others.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Dad.Core
{
    public static class Cover
    {
        /// <summary>A place in cover: where to crouch, the way out from what covers it, and where to fire from (the same place standing, or a step to the side).</summary>
        public struct Spot
        {
            public Vector3 At, Out, Peek;
            /// <summary>Low cover: fired over standing up. Otherwise fired round, from the side.</summary>
            public bool Low;
            /// <summary>Metres walked to get there.</summary>
            public float Walk;
        }

        /// <summary>The top of the head crouched down (crouch-idle; measured on three of the fighters, 1.07 to 1.13 m) and the eyes standing, metres.</summary>
        public const float CrouchTop = 1.15f, StandEye = 1.55f;
        /// <summary>How near two people may take cover.</summary>
        public const float Apart = 1.4f;
        static readonly float[] Rings = { 2.5f, 5, 8, 11, 15, 19 };
        const int Ways = 16;

        static readonly Dictionary<Object, Vector3> taken = new Dictionary<Object, Vector3>();
        static readonly List<Spot> found = new List<Spot>();
        static readonly List<Vector3> corners = new List<Vector3>();

        /// <summary>Places tried in the last search, and those that would do, for tests and the record.</summary>
        public static int Tried { get; private set; }
        public static int Good { get; private set; }
        /// <summary>Milliseconds the last search took on the main thread, and the longest so far.</summary>
        public static double LastMs { get; private set; }
        public static double MostMs { get; private set; }
        static readonly System.Diagnostics.Stopwatch clock = new System.Diagnostics.Stopwatch();

        public static void Take(Object who, Vector3 at) => taken[who] = at;
        public static void Leave(Object who) => taken.Remove(who);
        public static void Clear() => taken.Clear();

        /// <summary>Whether someone crouched down at a place is hidden from an eye, to the top of the head.</summary>
        public static bool Hidden(Vector3 at, Vector3 eye) => Physics.Linecast(eye, at + Vector3.up * CrouchTop, WorldLayers.Solid, QueryTriggerInteraction.Ignore);

        /// <summary>Whether someone standing at a place can see a mark.</summary>
        public static bool Sees(Vector3 at, Vector3 mark) => !Physics.Linecast(at + Vector3.up * StandEye, mark, WorldLayers.Solid, QueryTriggerInteraction.Ignore);

        /// <summary>The nearest cover, by walking, for someone at a place from shots coming from an eye: between near and far metres from it, and if closer is given, at
        /// least that much nearer it than they are now (moving up). False if there is none or no walking mesh yet.</summary>
        public static bool Find(Object who, Vector3 from, Vector3 eye, float near, float far, float closer, out Spot best)
        {
            clock.Restart();
            try { return Search(who, from, eye, near, far, closer, out best); }
            finally { LastMs = clock.Elapsed.TotalMilliseconds; if (LastMs > MostMs) MostMs = LastMs; }
        }

        static bool Search(Object who, Vector3 from, Vector3 eye, float near, float far, float closer, out Spot best)
        {
            best = default;
            Tried = Good = 0;
            if (!Navigation.Ready(from)) return false;
            found.Clear();
            float now = Flat(eye - from).magnitude;
            foreach (float r in Rings)
                for (int k = 0; k < Ways; k++)
                {
                    float a = (k + (r % 2) * 0.5f) * Mathf.PI * 2 / Ways;
                    var p = from + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * r;
                    Tried++;
                    if (!NavMesh.SamplePosition(p, out var on, 1.5f, NavMesh.AllAreas)) continue;
                    if (!NavMesh.FindClosestEdge(on.position, out var edge, NavMesh.AllAreas) || edge.distance > 1.2f) continue;
                    if (Judge(who, edge.position, edge.normal, eye, near, far, closer > 0 ? now - closer : float.MaxValue, out var s)) found.Add(s);
                }
            Good = found.Count;
            if (found.Count == 0) return false;
            // the nearest few as the crow flies, then walked: the shortest walk that gets there
            found.Sort((x, y) => Flat(x.At - from).sqrMagnitude.CompareTo(Flat(y.At - from).sqrMagnitude));
            float shortest = float.MaxValue;
            for (int i = 0; i < Mathf.Min(5, found.Count); i++)
            {
                if (!Navigation.Route(from, found[i].At, corners)) continue;
                float walk = Navigation.Length(from, corners);
                if (walk > Flat(found[i].At - from).magnitude * 2.2f + 3) continue; // the long way round: through the line of fire, likely
                if (walk >= shortest) continue;
                shortest = walk;
                best = found[i];
                best.Walk = walk;
            }
            return shortest < float.MaxValue;
        }

        // A place by the mesh's edge: something solid behind it towards the eye, hidden crouching, and a place to fire from.
        static bool Judge(Object who, Vector3 at, Vector3 normal, Vector3 eye, float near, float far, float within, out Spot s)
        {
            s = default;
            if (Physics.Raycast(at + Vector3.up, Vector3.down, out var ground, 2, WorldLayers.Ground | 1, QueryTriggerInteraction.Ignore)) at.y = ground.point.y; // the mesh lies a little above the street
            normal.y = 0;
            if (normal.sqrMagnitude < 0.01f) return false;
            normal.Normalize();
            var toEye = Flat(eye - at);
            float d = toEye.magnitude;
            if (d < near || d > far || d > within) return false;
            if (Vector3.Dot(-normal, toEye / d) < 0.35f) return false; // what covers it is not between it and the shots
            if (!Physics.Raycast(at + Vector3.up * 0.5f, -normal, 1.0f, WorldLayers.Solid, QueryTriggerInteraction.Ignore)) return false; // the mesh ends at a drop, not a wall
            var along = Vector3.Cross(Vector3.up, normal);
            if (!Hidden(at, eye) || !Hidden(at + along * 0.4f, eye) || !Hidden(at - along * 0.4f, eye)) return false; // the shoulders too: not the very end of a wall
            foreach (var t in taken) if (t.Key != who && t.Key != null && Flat(t.Value - at).magnitude < Apart) return false;
            var mark = eye;
            if (Sees(at, mark)) { s = new Spot { At = at, Out = normal, Peek = at, Low = true }; return true; }
            var side = along;
            foreach (float way in new[] { 1f, -1f })
                for (float step = 0.8f; step <= 1.6f; step += 0.4f)
                {
                    var q = at + side * way * step;
                    if (!NavMesh.SamplePosition(q, out var on, 0.4f, NavMesh.AllAreas)) break;
                    if (!Sees(on.position, mark)) continue;
                    s = new Spot { At = at, Out = normal, Peek = on.position, Low = false };
                    return true;
                }
            return false;
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0, v.z);
    }
}
