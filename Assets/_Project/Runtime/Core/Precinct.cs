// The police answer gunfire (2026-11-10). A shot heard anywhere brings officers from People/fighters.json after a while, unless some are already on their way there or
// standing near it. They come on foot from a little way off, from the first side with ground under it that has a clear line to the place, and walk to it (Fighter).
using System.Collections.Generic;
using UnityEngine;

namespace Dad.Core
{
    public sealed class Precinct : MonoBehaviour
    {
        /// <summary>Seconds from the first shot to the officers arriving at the edge of it.</summary>
        public float Delay = 25;
        /// <summary>How far off they come from, and how near a call must be to one already answered to be the same.</summary>
        public float From = 35, Same = 80;
        public readonly List<Fighter> Officers = new List<Fighter>();
        readonly List<(Vector3 at, float when)> calls = new List<(Vector3, float)>();

        public static Precinct Attach(GameObject host)
        {
            var p = host.AddComponent<Precinct>();
            Danger.Heard += p.Heard;
            return p;
        }

        void OnDestroy() => Danger.Heard -= Heard;

        void Heard(Vector3 at, float reach, Danger.Kind kind)
        {
            if (kind != Danger.Kind.Shot) return;
            foreach (var c in calls) if (Vector3.Distance(c.at, at) < Same) return;
            foreach (var o in Officers) if (o != null && !o.Dead && Vector3.Distance(o.transform.position, at) < Same) return;
            calls.Add((at, Time.time + Delay));
        }

        float nextHunt;

        void Update()
        {
            for (int i = calls.Count - 1; i >= 0; i--)
            {
                if (Time.time < calls[i].when) continue;
                Send(calls[i].at);
                calls.RemoveAt(i);
            }
            Hunt();
        }

        // While you are wanted (Heat): officers go to where you are, and more come, two for each level, the first a few seconds after you become wanted.
        void Hunt()
        {
            var you = Vitals.Player;
            if (Heat.Level == 0 || you == null || you.Dead || !you.isActiveAndEnabled) { nextHunt = Time.time + FirstHunt; return; }
            if (Time.time < nextHunt) return;
            nextHunt = Time.time + Mathf.Max(8, 30 - 4 * Heat.Level);
            int alive = 0;
            Officers.RemoveAll(o => o == null); // bodies cleared away (Remains)
            foreach (var o in Officers)
            {
                if (o == null || o.Dead) continue;
                alive++;
                if (!o.OnPlayer) o.Goal = you.transform.position;
            }
            if (alive < Heat.Level * 2) Send(you.transform.position);
        }

        /// <summary>Seconds from becoming wanted to the first officers being sent.</summary>
        public float FirstHunt = 8;

        void Send(Vector3 to)
        {
            var roster = NpcRoster.Load("People/fighters");
            var start = Origin(to);
            int n = 0;
            foreach (var who in roster.people)
            {
                if (who.role != "police") continue;
                var side = Vector3.Cross(Vector3.up, (to - start).normalized);
                var at = start + side * (n * 1.4f - 0.7f);
                if (Physics.Raycast(at + Vector3.up * 3, Vector3.down, out var ground, 8, WorldLayers.Ground | 1, QueryTriggerInteraction.Ignore)) at.y = ground.point.y;
                var f = Fighter.Spawn(who, Fighter.Side.Police, at, Quaternion.LookRotation(new Vector3(to.x - at.x, 0, to.z - at.z)).eulerAngles.y);
                if (f == null) continue;
                f.Goal = to;
                Officers.Add(f);
                n++;
            }
            Debug.Log($"[Dad] police: {n} officers sent to {to} from {start}");
        }

        // The first of eight ways round the place with ground there and nothing solid between it and the place; straight back along x if none has.
        Vector3 Origin(Vector3 to)
        {
            for (int k = 0; k < 8; k++)
            {
                var way = Quaternion.Euler(0, k * 45, 0) * Vector3.right;
                var at = to + way * From;
                if (!Physics.Raycast(at + Vector3.up * 20, Vector3.down, out var ground, 40, WorldLayers.Ground | 1, QueryTriggerInteraction.Ignore)) continue;
                at.y = ground.point.y;
                if (!Physics.Linecast(at + Vector3.up * 1.5f, to + Vector3.up * 1.5f, WorldLayers.Solid)) return at;
            }
            return to + Vector3.right * From;
        }
    }
}
