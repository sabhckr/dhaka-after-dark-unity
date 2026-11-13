// Where people with guns can walk (2026-11-12, Docs/Characters/SHOOTER_BAR_AUDIT.md section 11). The city is too big to bake a navigation mesh for ahead of time, and it is
// streamed, so one is built while the game runs: a square round where the fighting is, from the street's colliders (the ground, roads, kerbs, building walls, street
// solids) and the parked cars, on worker threads (NavMeshBuilder.UpdateNavMeshDataAsync). When the fighting moves near its edge, it is built again round the new place.
// Moving traffic is left out: it would leave holes where a car was. Until a mesh is ready a fighter goes straight, as before.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Dad.Core
{
    public static class Navigation
    {
        /// <summary>Metres across the square built, and how near its edge a place may be before it is built again round that place.</summary>
        public const float Size = 160, Margin = 35;
        /// <summary>A person: how wide, how tall, the step they take up a kerb, the slope they walk up.</summary>
        public const float Radius = 0.3f, Height = 1.8f, Climb = 0.4f, Slope = 40;
        /// <summary>The layers walked on and walked round: the street (World) and anything on the default layer, plus parked cars (Traffic, by name).</summary>
        public const int Layers = WorldLayers.Ground | 1 | 1 << WorldLayers.Traffic;

        static NavMeshData data;
        static NavMeshDataInstance instance;
        static AsyncOperation building;
        static Vector3 centre, next;
        static bool pending;
        static float startedAt;
        static readonly List<NavMeshBuildSource> sources = new List<NavMeshBuildSource>();
        static readonly List<NavMeshBuildMarkup> none = new List<NavMeshBuildMarkup>();
        static NavMeshPath path;

        /// <summary>Meshes built, the last one's time in milliseconds from asking to ready (worker time, not the frame), and the sources it was built from.</summary>
        public static int Builds { get; private set; }
        public static float LastBuildMs { get; private set; }
        public static int LastSources { get; private set; }
        /// <summary>A mesh is being built now.</summary>
        public static bool Building => building != null && !building.isDone;
        /// <summary>The middle of the square built (or being built).</summary>
        public static Vector3 Centre => centre;

        static NavMeshBuildSettings Settings()
        {
            var s = NavMesh.GetSettingsByIndex(0);
            s.agentRadius = Radius;
            s.agentHeight = Height;
            s.agentClimb = Climb;
            s.agentSlope = Slope;
            s.minRegionArea = 1;
            return s;
        }

        /// <summary>Somewhere people will fight: a mesh is built round it if the one there is does not reach it.</summary>
        public static void Need(Vector3 at)
        {
            if (Covers(at, Margin)) return;
            if (Building) { next = at; pending = true; return; } // once this one is done
            Build(at);
        }

        /// <summary>Whether the mesh built (or being built) reaches this far in from its edge round a place.</summary>
        public static bool Covers(Vector3 at, float inset = 0) => data != null && Mathf.Abs(at.x - centre.x) < Size / 2 - inset && Mathf.Abs(at.z - centre.z) < Size / 2 - inset;

        /// <summary>A mesh is ready round a place.</summary>
        public static bool Ready(Vector3 at) => !Building && Builds > 0 && Covers(at);

        static void Build(Vector3 at)
        {
            if (FloatingOrigin.Active != null && watched != FloatingOrigin.Active) { watched = FloatingOrigin.Active; watched.Shifted += Shifted; }
            var settings = Settings();
            var bounds = new Bounds(new Vector3(at.x, at.y, at.z), new Vector3(Size, 80, Size));
            sources.Clear();
            NavMeshBuilder.CollectSources(bounds, Layers, NavMeshCollectGeometry.PhysicsColliders, 0, none, sources);
            // parked cars stay; the moving traffic, the people and anything else on the traffic layer do not
            sources.RemoveAll(s => s.component != null && s.component.gameObject.layer == WorldLayers.Traffic && s.component.gameObject.name != "Parked body");
            LastSources = sources.Count;
            if (data == null)
            {
                data = new NavMeshData(settings.agentTypeID) { name = "Fighting ground" };
                instance = NavMesh.AddNavMeshData(data);
            }
            centre = at;
            startedAt = Time.realtimeSinceStartup;
            building = NavMeshBuilder.UpdateNavMeshDataAsync(data, settings, sources, bounds);
            building.completed += _ =>
            {
                Builds++;
                LastBuildMs = (Time.realtimeSinceStartup - startedAt) * 1000;
                if (pending) { pending = false; Need(next); }
            };
        }

        static FloatingOrigin watched;
        static void Shifted(double east, double north) => Forget();

        /// <summary>Drops the mesh: the scene moved under it (FloatingOrigin), or a test is done with it.</summary>
        public static void Forget()
        {
            if (instance.valid) instance.Remove();
            instance = default;
            if (data != null) Object.Destroy(data);
            data = null;
            building = null;
            pending = false;
            Builds = 0;
        }

        /// <summary>The way from one place to another round what is in the way: the corners to walk to, the last at the end. Without a mesh there, or with no way
        /// through, the end alone (straight there) and false.</summary>
        public static bool Route(Vector3 from, Vector3 to, List<Vector3> corners)
        {
            corners.Clear();
            Need(from);
            if (Ready(from) && NavMesh.SamplePosition(from, out var a, 2, NavMesh.AllAreas) && NavMesh.SamplePosition(to, out var b, 2, NavMesh.AllAreas))
            {
                path ??= new NavMeshPath();
                if (NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete)
                {
                    corners.AddRange(path.corners);
                    if (corners.Count > 0) corners.RemoveAt(0); // where it starts
                    if (corners.Count == 0) corners.Add(b.position);
                    return true;
                }
            }
            corners.Add(to);
            return false;
        }

        /// <summary>Metres walked along a way from a place through its corners.</summary>
        public static float Length(Vector3 from, List<Vector3> corners)
        {
            float d = 0;
            foreach (var c in corners) { d += Vector3.Distance(from, c); from = c; }
            return d;
        }
    }
}
