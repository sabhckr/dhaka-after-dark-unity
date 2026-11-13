// Vehicles nobody is driving (2026-11-06): the spaces in World/transport.json, kept by chunk and drawn instanced, so hundreds cost a few draw calls and no GameObject. Near by they are whole,
// with wheels and glass; further off, one light mesh. The nearest borrow a kinematic box from a small pool, as traffic does. Get into one and it becomes a PlayerVehicle wearing the same paint;
// walk away from it and it goes back to being a space, where it was left. Boats are the same thing on the water.
using System;
using System.Collections.Generic;
using Dad.Traffic;
using Dad.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace Dad.Core
{
    public sealed class ParkedView : MonoBehaviour
    {
        public const float NearRange = 70, DrawRange = 320, BoxRange = 40;
        public const int Boxes = 20;
        const int ChooseEvery = 10;

        public sealed class Slot
        {
            public uint Id;
            public VehicleKind Kind;
            public double East, North;
            public float Y, Turn;
            public PaintJob Job;
            public VehicleLook Look;
            /// <summary>Someone has this one out as a PlayerVehicle.</summary>
            public bool Out;
        }

        sealed class Family
        {
            public VehicleModel Model;
            public Mesh Near, Glass, Far;
            public Material Body;
            public Mesh[] Wheels;
            public Matrix4x4[] WheelLocal;
            public VehicleBatch Whole = new VehicleBatch(128), Distant = new VehicleBatch(420);
            public VehicleBatch[] Tyres;
        }

        public DayCycle Day;
        /// <summary>Spaces in the file, and how many were drawn last frame.</summary>
        public int Count { get; private set; }
        public int DrawnNear { get; private set; }
        public int DrawnFar { get; private set; }
        public IEnumerable<Slot> All { get { foreach (var l in cells.Values) foreach (var s in l) yield return s; } }

        readonly Dictionary<ChunkCell, List<Slot>> cells = new Dictionary<ChunkCell, List<Slot>>();
        readonly Dictionary<VehicleKind, Family> families = new Dictionary<VehicleKind, Family>();
        readonly Dictionary<PlayerVehicle, Slot> taken = new Dictionary<PlayerVehicle, Slot>();
        readonly Plane[] planes = new Plane[6];
        readonly Rigidbody[] bodies = new Rigidbody[Boxes];
        readonly BoxCollider[] shapes = new BoxCollider[Boxes];
        readonly Slot[] held = new Slot[Boxes];
        readonly float[] range = new float[Boxes];
        Material glass;
        int tick;

        public static ParkedView Attach(GameObject host, TransportFile file, DayCycle day)
        {
            var v = host.AddComponent<ParkedView>();
            v.Day = day;
            if (file?.parked?.kind != null) v.Read(file.parked);
            return v;
        }

        void Read(TransportFile.Parked p)
        {
            glass = Resources.Load<Material>("Materials/VehicleGlass");
            var kinds = new VehicleKind[p.kinds.Length];
            for (int i = 0; i < kinds.Length; i++)
            {
                kinds[i] = (VehicleKind)Enum.Parse(typeof(VehicleKind), p.kinds[i]);
                Prepare(kinds[i]);
            }
            int last = -1;
            for (int i = 0; i < p.kind.Length; i++)
            {
                var kind = kinds[p.kind[i]];
                if (!families.ContainsKey(kind)) continue;
                var s = new Slot { Id = (uint)i + 1, Kind = kind, East = p.east[i], North = p.north[i], Y = p.height[i], Turn = p.turn[i] };
                // No two neighbours in one colour where another can be had.
                s.Job = VehiclePaints.For(kind, s.Id);
                for (uint n = 1; n < 6 && s.Job.Base == last; n++) s.Job = VehiclePaints.For(kind, s.Id + 7919 * n);
                last = s.Job.Base;
                s.Look = VehicleLook.Of(s.Job, families[kind].Model.Height);
                Cell(s.East, s.North).Add(s);
                Count++;
            }
            for (int i = 0; i < Boxes; i++)
            {
                var go = new GameObject("Parked body") { layer = WorldLayers.Traffic };
                go.transform.SetParent(transform, false);
                bodies[i] = go.AddComponent<Rigidbody>();
                bodies[i].isKinematic = true;
                shapes[i] = go.AddComponent<BoxCollider>();
                go.SetActive(false);
            }
        }

        List<Slot> Cell(double east, double north)
        {
            var c = ChunkCell.Of(east, north);
            if (!cells.TryGetValue(c, out var l)) cells[c] = l = new List<Slot>();
            return l;
        }

        void Prepare(VehicleKind kind)
        {
            var art = Resources.Load<TextAsset>("Vehicles/Empty/" + kind);
            if (art == null) { Debug.LogError("[Dad] no empty model for " + kind + ": run Tools/models/bake.py"); return; }
            var m = VehicleArt.Read(kind, art.bytes, 0);
            var f = new Family { Model = m, Body = PlayerVehicle.BodyMaterial(kind) };
            f.Near = TrafficView.ToMesh(m.Near, kind + " parked");
            f.Far = TrafficView.ToMesh(m.Far, kind + " parked far");
            if (m.Glass.Triangles > 0) f.Glass = TrafficView.ToMesh(m.Glass, kind + " parked glass");
            int n = m.OwnWheels?.Length ?? 0;
            f.Wheels = new Mesh[n];
            f.Tyres = new VehicleBatch[n];
            for (int i = 0; i < n; i++)
            {
                f.Wheels[i] = TrafficView.ToMesh(m.OwnWheels[i], kind + " parked wheel");
                f.Tyres[i] = new VehicleBatch(320);
            }
            f.WheelLocal = new Matrix4x4[m.Wheels.Length];
            for (int i = 0; i < m.Wheels.Length; i++)
            {
                var w = m.Wheels[i];
                f.WheelLocal[i] = Matrix4x4.TRS(new Vector3((float)w.X, (float)w.Y, (float)w.Z), w.Flip ? Quaternion.Euler(0, 180, 0) : Quaternion.identity, Vector3.one);
            }
            families[kind] = f;
        }

        Vector3 Scene(Slot s)
        {
            FloatingOrigin.Active.Offset.ToScene(s.East, s.North, out float x, out float z);
            return new Vector3(x, s.Y, z);
        }

        void LateUpdate() => Submit();

        /// <summary>Places and draws this frame's cars. ViewShot calls it again just before its own render, which otherwise misses what was drawn in LateUpdate.</summary>
        public void Submit()
        {
            var cam = Camera.main;
            if (cam == null || FloatingOrigin.Active == null || Count == 0) return;
            var eye = cam.transform.position;
            FloatingOrigin.Active.Offset.ToWorld(eye.x, eye.z, out double east, out double north);
            GeometryUtility.CalculateFrustumPlanes(cam, planes);
            var at = ChunkCell.Of(east, north);
            int near = 0, far = 0;
            for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (!cells.TryGetValue(new ChunkCell(at.X + dx, at.Z + dz), out var list)) continue;
                    foreach (var s in list)
                    {
                        if (s.Out) continue;
                        double ex = s.East - east, ez = s.North - north, d = ex * ex + ez * ez;
                        if (d > DrawRange * DrawRange) continue;
                        var f = families[s.Kind];
                        var p = Scene(s);
                        if (!GeometryUtility.TestPlanesAABB(planes, new Bounds(p + Vector3.up * 1.5f, new Vector3(1, 0.3f, 1) * (float)f.Model.Length + Vector3.up * 3))) continue;
                        var m = Matrix4x4.TRS(p, Quaternion.Euler(0, s.Turn, 0), Vector3.one);
                        if (d < NearRange * NearRange)
                        {
                            f.Whole.Add(m, s.Look, Vector4.zero);
                            for (int i = 0; i < f.WheelLocal.Length; i++) f.Tyres[f.Model.Wheels[i].Mesh].Add(m * f.WheelLocal[i], s.Look, Vector4.zero);
                            near++;
                        }
                        else
                        {
                            f.Distant.Add(m, s.Look, Vector4.zero);
                            far++;
                        }
                    }
                }
            DrawnNear = near;
            DrawnFar = far;
            var bounds = new Bounds(eye, Vector3.one * DrawRange * 2.2f);
            foreach (var f in families.Values)
            {
                f.Whole.Flush(f.Near, f.Body, ShadowCastingMode.On, bounds, false);
                if (f.Glass != null) f.Whole.Flush(f.Glass, glass, ShadowCastingMode.Off, bounds, false);
                f.Whole.Count = 0;
                f.Distant.Flush(f.Far, f.Body, ShadowCastingMode.Off, bounds, true);
                for (int i = 0; i < f.Tyres.Length; i++) f.Tyres[i].Flush(f.Wheels[i], f.Body, ShadowCastingMode.Off, bounds, true);
            }
        }

        void FixedUpdate()
        {
            if (Count == 0 || tick++ % ChooseEvery != 0) return;
            var cam = Camera.main;
            if (cam == null || FloatingOrigin.Active == null) return;
            var eye = cam.transform.position;
            FloatingOrigin.Active.Offset.ToWorld(eye.x, eye.z, out double east, out double north);
            var at = ChunkCell.Of(east, north);
            int n = 0;
            for (int i = 0; i < Boxes; i++) { held[i] = null; range[i] = float.MaxValue; }
            for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (!cells.TryGetValue(new ChunkCell(at.X + dx, at.Z + dz), out var list)) continue;
                    foreach (var s in list)
                    {
                        if (s.Out || s.Kind == VehicleKind.Boat) continue;
                        float d = (float)((s.East - east) * (s.East - east) + (s.North - north) * (s.North - north));
                        if (d > BoxRange * BoxRange) continue;
                        int slot = n < Boxes ? n++ : Boxes - 1;
                        if (slot == Boxes - 1 && d >= range[slot]) continue;
                        while (slot > 0 && range[slot - 1] > d)
                        {
                            range[slot] = range[slot - 1];
                            held[slot] = held[slot - 1];
                            slot--;
                        }
                        range[slot] = d;
                        held[slot] = s;
                    }
                }
            for (int i = 0; i < Boxes; i++)
            {
                var s = held[i];
                if (s == null) { if (bodies[i].gameObject.activeSelf) bodies[i].gameObject.SetActive(false); continue; }
                var m = families[s.Kind].Model;
                var size = new Vector3((float)m.Width, (float)m.Height, (float)m.Length);
                if (shapes[i].size != size) { shapes[i].size = size; shapes[i].center = new Vector3(0, size.y / 2, 0); }
                if (!bodies[i].gameObject.activeSelf) bodies[i].gameObject.SetActive(true);
                bodies[i].position = Scene(s);
                bodies[i].rotation = Quaternion.Euler(0, s.Turn, 0);
            }
        }

        /// <summary>The nearest space of this kind within reach of a point, world metres.</summary>
        public Slot Nearest(double east, double north, VehicleKind kind, double reach)
        {
            Slot best = null;
            double most = reach * reach;
            foreach (var s in All)
            {
                if (s.Out || s.Kind != kind) continue;
                double d = (s.East - east) * (s.East - east) + (s.North - north) * (s.North - north);
                if (d < most) { most = d; best = s; }
            }
            return best;
        }

        /// <summary>The vehicle someone standing here could get into: the one whose side is nearest, within reach. Null if none, or if it is one nobody can drive.</summary>
        public Slot Within(Vector3 from, float reach)
        {
            if (Count == 0 || FloatingOrigin.Active == null) return null;
            FloatingOrigin.Active.Offset.ToWorld(from.x, from.z, out double east, out double north);
            var at = ChunkCell.Of(east, north);
            Slot best = null;
            float most = reach;
            for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (!cells.TryGetValue(new ChunkCell(at.X + dx, at.Z + dz), out var list)) continue;
                    foreach (var s in list)
                    {
                        if (s.Out || !PlayerVehicle.Drivable(s.Kind)) continue;
                        double ex = s.East - east, ez = s.North - north;
                        if (ex * ex + ez * ez > 900) continue;
                        var m = families[s.Kind].Model;
                        float t = s.Turn * Mathf.Deg2Rad, sn = Mathf.Sin(t), cs = Mathf.Cos(t);
                        float x = Mathf.Abs((float)(-ex * cs + ez * sn)) - (float)m.Width / 2, z = Mathf.Abs((float)(ex * sn + ez * cs)) - (float)m.Length / 2;
                        float gap = new Vector2(Mathf.Max(x, 0), Mathf.Max(z, 0)).magnitude;
                        if (gap < most) { most = gap; best = s; }
                    }
                }
            return best;
        }

        /// <summary>This space's vehicle, made real where it stands, in its paint.</summary>
        public PlayerVehicle Take(Slot s)
        {
            var at = Scene(s);
            if (s.Kind != VehicleKind.Boat && Physics.Raycast(at + Vector3.up * 4, Vector3.down, out var ground, 12, WorldLayers.Ground)) at.y = ground.point.y;
            var v = PlayerVehicle.Spawn(s.Kind, at + Vector3.up * (s.Kind == VehicleKind.Boat ? 0 : 0.2f), s.Turn, Day, s.Job);
            if (v == null) return null;
            s.Out = true;
            taken[v] = s;
            return v;
        }

        /// <summary>A vehicle left behind goes back to being a space, where it now stands and in the paint it now wears. One that never was a space is simply gone.</summary>
        public void Give(PlayerVehicle v)
        {
            if (taken.TryGetValue(v, out var s))
            {
                taken.Remove(v);
                Cell(s.East, s.North).Remove(s);
                var p = v.transform.position;
                FloatingOrigin.Active.Offset.ToWorld(p.x, p.z, out s.East, out s.North);
                s.Turn = v.transform.eulerAngles.y;
                if (s.Kind != VehicleKind.Boat)
                {
                    s.Y = p.y;
                    if (Physics.Raycast(p + Vector3.up * 3, Vector3.down, out var ground, 8, WorldLayers.Ground)) s.Y = ground.point.y;
                }
                s.Job = v.Job;
                s.Look = VehicleLook.Of(s.Job, families[s.Kind].Model.Height);
                s.Out = false;
                Cell(s.East, s.North).Add(s);
            }
            Destroy(v.gameObject);
        }
    }
}
