// Something on the street that frightens people (2026-11-10): a shot, a gun drawn, a car driven at people or a crash. Whatever causes it reports where it is and how far it
// carries; the crowd hears it and those near enough react (NpcCrowd). Nothing is remembered: a report is heard once, when it is made.
using System;
using UnityEngine;

namespace Dad.Core
{
    public static class Danger
    {
        public enum Kind { Shot, Gun, Car, Crash }

        /// <summary>Metres a kind of danger is noticed at, unless the report says otherwise.</summary>
        public static float Reach(Kind kind) => kind == Kind.Shot ? 60 : kind == Kind.Crash ? 35 : kind == Kind.Gun ? 14 : 10;

        public static event Action<Vector3, float, Kind> Heard;

        /// <summary>Tells everyone listening that there is danger here, noticed this many metres away (zero: the kind's own reach).</summary>
        public static void Report(Vector3 at, Kind kind, float reach = 0) => Heard?.Invoke(at, reach > 0 ? reach : Reach(kind), kind);
    }
}
