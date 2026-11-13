// Traffic a driven vehicle can hit (E-04). Traffic has no bodies of its own: it runs on lanes at 10 Hz. So the few vehicles
// near the driver borrow a kinematic box from this pool for as long as they are near, moved each physics step to where the
// vehicle is drawn. A hit stops the driven car and does not move the other: traffic stays on its lanes (a compromise, see
// Docs/PRODUCTION/E04_PLAYER_VEHICLE.md). Traffic keeps clear of the driver by itself, through TrafficSim.Driven.
using Dad.Traffic;
using UnityEngine;

namespace Dad.Core
{
    public sealed class TrafficColliders : MonoBehaviour
    {
        public const int Count = 24;
        public const float Range = 45f;
        const int ChooseEvery = 10;

        public TrafficView View;

        readonly Rigidbody[] bodies = new Rigidbody[Count];
        readonly BoxCollider[] shapes = new BoxCollider[Count];
        readonly Agent[] held = new Agent[Count];
        readonly float[] range = new float[Count];
        int tick;

        /// <summary>Boxes in use, for tests and reports.</summary>
        public int InUse { get; private set; }

        public Agent Holder(int i) => held[i];

        void Awake()
        {
            for (int i = 0; i < Count; i++)
            {
                var go = new GameObject("Traffic body") { layer = WorldLayers.Traffic };
                go.transform.SetParent(transform, false);
                bodies[i] = go.AddComponent<Rigidbody>();
                bodies[i].isKinematic = true;
                shapes[i] = go.AddComponent<BoxCollider>();
                go.SetActive(false);
            }
        }

        void FixedUpdate()
        {
            var player = View != null ? View.Player : null;
            if (player == null || View.Sim == null) { if (InUse > 0) Clear(); return; }
            if (tick++ % ChooseEvery == 0) Choose(player.transform.position);
            for (int i = 0; i < Count; i++)
            {
                var a = held[i];
                if (a == null) continue;
                if (!View.Place(a, out var at, out var turn)) { Drop(i); continue; }
                bodies[i].MovePosition(at);
                bodies[i].MoveRotation(turn);
            }
        }

        // The nearest vehicles get the boxes, nearest first. A box may change hands, so it goes straight to its new holder: there is
        // a box wherever one of them is, whichever box it is.
        void Choose(Vector3 from)
        {
            int n = 0;
            for (int i = 0; i < Count; i++) { held[i] = null; range[i] = float.MaxValue; }
            var agents = View.Sim.Agents;
            for (int k = 0; k < agents.Count; k++)
            {
                var a = agents[k];
                if (!View.Place(a, out var at, out _)) continue;
                float d = (at - from).sqrMagnitude;
                if (d > Range * Range) continue;
                // Insert by distance into the short list.
                int slot = n < Count ? n++ : Count - 1;
                if (slot == Count - 1 && d >= range[slot]) continue;
                while (slot > 0 && range[slot - 1] > d)
                {
                    range[slot] = range[slot - 1];
                    held[slot] = held[slot - 1];
                    slot--;
                }
                range[slot] = d;
                held[slot] = a;
            }
            InUse = n;
            for (int i = 0; i < n; i++)
            {
                var a = held[i];
                View.Place(a, out var at, out var turn);
                var size = new Vector3((float)a.Spec.Width, (float)a.Spec.Height, (float)a.Spec.Length);
                if (shapes[i].size != size)
                {
                    shapes[i].size = size;
                    shapes[i].center = new Vector3(0, size.y / 2, 0);
                }
                if (!bodies[i].gameObject.activeSelf) bodies[i].gameObject.SetActive(true);
                bodies[i].position = at;
                bodies[i].rotation = turn;
            }
            for (int i = n; i < Count; i++) if (bodies[i].gameObject.activeSelf) bodies[i].gameObject.SetActive(false);
        }

        void Drop(int i)
        {
            held[i] = null;
            bodies[i].gameObject.SetActive(false);
        }

        void Clear()
        {
            for (int i = 0; i < Count; i++) Drop(i);
            InUse = 0;
        }
    }
}
