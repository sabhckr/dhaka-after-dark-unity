// Streams chunks around a focus transform (the camera or player).
//
// Per frame, in Update:
//   1. StreamPlanner decides what to queue, cancel and unload for the focus cell and a lead
//      cell LeadSeconds ahead along the focus's velocity.
//   2. Up to MaxInFlight loads run on worker threads: read the .dadc file, decode it, build
//      the ground, road and building meshes, the debug overlays and the line arrays. Each has a CancellationToken; nothing on a worker
//      touches Unity objects.
//   3. Finished loads are collected (polled, so completion runs on the main thread).
//   4. Apply jobs run in small steps until ApplyBudgetMs is spent (at least one step a frame),
//      under the Dad.Streaming.Apply marker. They wait while loads are still building, for at
//      most MaxApplyHoldFrames: the workers' allocations start garbage collections, which stop
//      every thread, and one that landed in an apply step stretched it to 2.7-3.2 ms (C-07).
// Ground, roads and buildings are drawn lit with the world material (Dad/World, D-01). All three
// receive shadows; only buildings cast them (closed solids; elevated road decks do not, K-20).
// Street furniture and structures (Milestone 2, ChunkStreet) come with the roads: the structures
// (viaduct, footbridges, dividers) as one mesh, and poles, lamps, trees and cables as a near and a
// far mesh for each 125 m cell, of which each frame shows the one StreetLod picks for the cell's
// distance from the focus (near to 150 m, far to 450 m, neither beyond). All of them cast shadows.
// The rooftop kit's near meshes are not built with the chunk (K-45, approach 4): a cell's near kit is built
// on a worker once the cell is within KitBuildDistance, uploaded (at most MaxKitUploads a frame, under
// Dad.Streaming.Kit), shown with the cell's near tier and destroyed beyond KitDropDistance. Since D-06c the
// same build gives the cell's near facades (NearFacades.cs) after the kit, in a second submesh drawn with the
// WorldNear material, which dissolves them in from ChunkBuildings.FadeEnd to FadeStart around the focus (the
// globals _DadNearFocus and _DadNearFade, set each frame). A cell drawn near while its kit is still on the way
// is counted (KitLateFrames): that is pop-in. Kits start only in
// resident chunks and before the apply steps, and the apply waits for them as for loads (the first kit builds
// fill the array pools, and one collection that landed in an apply step made it 5.3 ms in a test walk).
// Each mesh keeps its CPU copy only until it has been drawn: once a camera (or a shadow pass) has seen it, it is
// already on the GPU and its copy is dropped (K-45; ReleaseScan meshes looked at a frame, at most ReleaseBudgetMs).
// A mesh never shown (the hidden tier of a cell, debug views) keeps its copy until it is, because dropping it would
// upload it: measured, that moved 52 MB to graphics memory on the street bench for no gain.
// Chunk roots are scene root objects placed at their cell corner through the floating origin,
// so origin shifts move them with everything else. Unloading destroys the root and its meshes.
// Only cells in world.json are requested; with SliceOnly, only cells inside Slice.
// Debug overlays (grid and ids, road graph) and the debug lines are built for every chunk but
// shown only while ShowGrid, ShowGraph and ShowLines say so; SetOverlays changes them everywhere.
// Once the index is read, one small made-up chunk goes the whole way a load goes (built on a worker
// task, through the completion checks, every apply step) and is destroyed: the first apply in a
// player otherwise costs about 3-4 ms more (first-call code compilation, first mesh and renderer),
// measured on the M1 player before this warm-up existed. The worker round trip came in C-07: the
// first finished load still compiled two methods (0.94 ms) in the frame that collected it, and the
// first loads started cost 0.76 ms while the thread pool made its threads. So the made-up chunk is
// built on MaxInFlight tasks at once; one result goes on through the apply steps. The warm-up
// also compiles the streamer's and planner's code and the collections they hold (PrepareCode),
// because a Mono player compiles each method on its first call: before that, the first finished
// load cost 3.5 ms and the first unload 2.1 ms of compilation in one frame (profiler captures,
// Phase C exit review). Since D-01 the applied made-up chunk is also drawn once, lit and shadowed,
// by a hidden camera (RenderWarmup) before it is destroyed, so the shader and URP's shadow code are
// ready before the first real chunk is drawn.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Dad.World;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

namespace Dad.Core
{
    [DefaultExecutionOrder(500)]
    public sealed class ChunkStreamer : MonoBehaviour
    {
        public Transform Focus;
        public FloatingOrigin Origin;
        public int LoadRadius = 2, UnloadRadius = 4, MaxInFlight = 3;
        public float ApplyBudgetMs = 2f;
        /// <summary>How many frames finished chunks may wait for the loads still building before they are applied anyway.</summary>
        public int MaxApplyHoldFrames = 30;
        public float LeadSeconds = 2f;
        /// <summary>Stream only a box of cells, for tests. Off in the game: every cell in world.json streams.</summary>
        public bool SliceOnly;
        public RectInt Slice;
        public string WorldDir;
        /// <summary>Debug views (C-05), off at start: chunk grid and ids, road graph, outlines and centrelines.</summary>
        public bool ShowGrid, ShowGraph, ShowLines;
        /// <summary>Tests only: each load waits this long on its worker first, so a cancel can catch it.</summary>
        public int TestLoadDelayMs;
        /// <summary>Tests only: each near rooftop kit build waits this long on its worker first, so its chunk can go meanwhile.</summary>
        public int TestKitDelayMs;
        /// <summary>Tests only: every mesh keeps its CPU copy, so a test can read it back.</summary>
        public bool KeepMeshesReadable;
        /// <summary>The near rooftop kit (K-45): built within KitBuildDistance of a cell, dropped beyond KitDropDistance (the
        /// distance StreetLod measures); at most MaxKitJobs building at once and MaxKitUploads uploaded a frame.</summary>
        public double KitBuildDistance = 250, KitDropDistance = 350;
        public int MaxKitJobs = 4, MaxKitUploads = 2;
        /// <summary>Meshes looked at per frame for release, and the time a frame may spend dropping copies (K-45).</summary>
        public const int ReleaseScan = 32;
        public const float ReleaseBudgetMs = 0.5f;

        public StreamPlanner Planner { get; private set; }
        public bool IndexReady => Planner != null;
        public string IndexError { get; private set; }

        // Counters for tests and benchmarks.
        public int Applied { get; private set; }
        public int Unloaded { get; private set; }
        public int Cancelled { get; private set; }
        public int Discarded { get; private set; }
        public int Failures { get; private set; }
        public int AppliedOutsideRings { get; private set; }
        public float MaxApplyFrameMs { get; private set; }
        public float MaxApplyStepMs { get; private set; }
        /// <summary>Apply steps during which a garbage collection finished, and the slowest such step.</summary>
        public int StepsWithGc { get; private set; }
        public float MaxGcStepMs { get; private set; }
        /// <summary>Apply steps run so far, warm-up left out.</summary>
        public int ApplySteps { get; private set; }
        /// <summary>Frames in which finished chunks waited for loads still building, and the longest wait.</summary>
        public int HeldFrames { get; private set; }
        public int MaxHeldFrames { get; private set; }
        public float WarmupMs { get; private set; }
        /// <summary>Meshes whose CPU copy was dropped, those still holding one, and the slowest drop and frame of drops (K-45).</summary>
        public int Released { get; private set; }
        public int StillReadable => readable.Count;
        public float MaxReleaseMs { get; private set; }
        public float MaxReleaseFrameMs { get; private set; }
        /// <summary>Near rooftop kits uploaded, dropped and held now; frames in which a cell drawn near was still waiting for
        /// its kit and the most such cells in one frame; builds that failed; the slowest upload and frame of uploads (K-45).</summary>
        public int KitsBuilt { get; private set; }
        public int KitsDropped { get; private set; }
        /// <summary>Near kits finished for a chunk gone or a cell gone past KitDropDistance, handed back unused.</summary>
        public int KitsDiscarded { get; private set; }
        public int KitsHeld { get; private set; }
        public int KitLateFrames { get; private set; }
        public int MaxKitLateCells { get; private set; }
        public int KitFailures { get; private set; }
        public float MaxKitUploadMs { get; private set; }
        public float MaxKitFrameMs { get; private set; }
        public int KitJobs => kitJobs.Count;
        /// <summary>Methods compiled ahead in the warm-up (PrepareCode).</summary>
        public int PreparedMethods { get; private set; }
        /// <summary>Slowest time per apply step kind: [0] root, [1] ground, [2] roads, [3] buildings, [4] overlays, [5] structures, [6] street cells, [7] line mesh.</summary>
        public readonly float[] MaxStepMsByKind = new float[8];
        /// <summary>Street cells uploaded per apply step (two meshes each at most).</summary>
        public const int CellsPerStep = 2;
        const int FirstCellStep = 6, LastCellStep = FirstCellStep + ChunkStreet.Cells * ChunkStreet.Cells / CellsPerStep - 1;
        /// <summary>Street cells shown near and far now, over every resident chunk; for tests and benches.</summary>
        public int NearCells { get; private set; }
        public int FarCells { get; private set; }
        public int InFlight => inFlight.Count;
        public int ApplyQueue => applyQueue.Count;
        public bool Idle => IndexReady && inFlight.Count == 0 && applyQueue.Count == 0 && kitJobs.Count == 0 && Planner.Count(ChunkState.Queued) == 0;

        /// <summary>What it is still doing, for a shot that gives up waiting.</summary>
        public string State => $"index {IndexReady}, in flight {inFlight.Count}, to apply {applyQueue.Count}, kit jobs {kitJobs.Count}, queued {Planner?.Count(ChunkState.Queued)}, applied {Applied}";

        public event Action<ChunkCell> ChunkIn, ChunkOut;

        /// <summary>A chunk's lanes, as it comes in, when BuildLanes is on.</summary>
        public event Action<ChunkCell, ChunkLanes> LanesIn;
        /// <summary>A chunk's walking lines, as it comes in, when BuildWalks is on.</summary>
        public event Action<ChunkCell, ChunkWalks> WalksIn;

        /// <summary>Build each chunk's lanes on the workers (TrafficView sets it before streaming starts).</summary>
        public static bool BuildLanes;
        public static bool BuildWalks;
        /// <summary>Gives chunks colliders (E-04), for a view with a vehicle in it. Off, chunks cost what they always did.</summary>
        public static bool BuildColliders;
        readonly ChunkColliders colliders = new ChunkColliders();
        public ChunkColliders Colliders => colliders;

        public IReadOnlyDictionary<ChunkCell, ChunkView> Resident => resident;

        public sealed class ChunkView
        {
            public ChunkCell Cell;
            public GameObject Root;
            public Mesh Surfaces, RoadMesh, BuildingMesh, GridMesh, GraphMesh, Lines, StructureMesh, CollisionMesh, GlowMesh;
            /// <summary>The chunk's street lamps, local metres (D-13).</summary>
            public ChunkStreet.LampHead[] Lamps = System.Array.Empty<ChunkStreet.LampHead>();
            /// <summary>The chunk's models (CityView draws them).</summary>
            public Building[] Placed = System.Array.Empty<Building>();
            /// <summary>Street cells (Milestone 2): near and far meshes and renderers per cell (null when empty), and the tier shown.</summary>
            public readonly Mesh[] CellMeshes = new Mesh[2 * ChunkStreet.Cells * ChunkStreet.Cells];
            public readonly MeshRenderer[] CellRenderers = new MeshRenderer[2 * ChunkStreet.Cells * ChunkStreet.Cells];
            public readonly int[] CellTier = new int[ChunkStreet.Cells * ChunkStreet.Cells];
            /// <summary>The street build's counts and rooftop kit items (its arrays are handed back after upload).</summary>
            public ChunkStreet Street;
            /// <summary>The building build's counts and near facade walls (D-06c; its mesh arrays are handed back after upload).</summary>
            public ChunkBuildings Facades;
            /// <summary>Street cells made so far (all of them once resident).</summary>
            public int CellsMade;
            /// <summary>The near rooftop kit per cell (K-45): mesh and renderer while held, a build under way, a build that failed.</summary>
            public readonly Mesh[] KitMeshes = new Mesh[ChunkStreet.Cells * ChunkStreet.Cells];
            public readonly MeshRenderer[] KitRenderers = new MeshRenderer[ChunkStreet.Cells * ChunkStreet.Cells];
            public readonly bool[] KitPending = new bool[ChunkStreet.Cells * ChunkStreet.Cells], KitFailed = new bool[ChunkStreet.Cells * ChunkStreet.Cells];
            /// <summary>Destroyed: kits finishing for it are handed back, not uploaded.</summary>
            public bool Gone;
            public GameObject Grid, Graph, LineObject;
            public int Buildings, Roads, Vertices;
            /// <summary>Ground features drawn by GroundLayer, and polygons that gave no triangles.</summary>
            public int[] SurfaceFeatures;
            public int SurfaceFailed;
            /// <summary>Carriageways drawn, and road ends cut along the chunk border.</summary>
            public int RoadsDrawn, BorderCuts;
            /// <summary>Buildings extruded, and how many of them stand off the ground.</summary>
            public int BuildingsDrawn, BuildingsRaised;
        }

        sealed class Payload
        {
            /// <summary>The view has the street's rooftop kit items (ChunkView.Street), and the buildings' near facade walls
            /// (ChunkView.Facades), and hands them back itself.</summary>
            public bool KitTaken, FacadesTaken;
            public DadcChunk Chunk;
            public ChunkSurfaces Surfaces;
            public ChunkRoads Roads;
            public ChunkBuildings Solids;
            public ChunkDebug Overlays;
            public ChunkOutlines Outlines;
            public ChunkLanes Lanes;
            public ChunkWalks Walks;
            public ChunkCollision Collision;

            /// <summary>Hands back the pooled arrays not uploaded yet, for a chunk that will not be applied.</summary>
            public void Release()
            {
                Roads?.Street?.Release();
                if (!KitTaken) Roads?.Street?.ReleaseRoofKit();
                Roads?.Release();
                Solids?.Release();
                if (!FacadesTaken) Solids?.ReleaseNearWalls();
                Outlines?.Release();
                Collision?.Release();
            }
        }

        sealed class Load
        {
            public ChunkCell Cell;
            public Task<Payload> Task;
            public CancellationTokenSource Cts;
        }

        sealed class KitJob
        {
            public ChunkView View;
            public int Cell;
            public Task<NearKit> Task;
        }

        sealed class ApplyJob
        {
            public ChunkCell Cell;
            public Payload Data;
            public ChunkView View;
            public int Step;
        }

        Material worldMat, facadeMat, nearMat, buildingMat, roadMat, gridMat, graphMat, nodeMat, signalMat, glowMat;
        static readonly int NearFocusId = Shader.PropertyToID("_DadNearFocus"), NearFadeId = Shader.PropertyToID("_DadNearFade");
        HashSet<ChunkCell> index;
        Task<HashSet<ChunkCell>> indexTask;
        readonly List<Load> inFlight = new List<Load>();
        readonly List<KitJob> kitJobs = new List<KitJob>();
        readonly LinkedList<ApplyJob> applyQueue = new LinkedList<ApplyJob>();
        readonly Dictionary<ChunkCell, ChunkView> resident = new Dictionary<ChunkCell, ChunkView>();
        readonly List<ChunkCell> unload = new List<ChunkCell>(), cancel = new List<ChunkCell>(), take = new List<ChunkCell>();
        readonly Stopwatch clock = new Stopwatch();
        /// <summary>Meshes still holding their CPU copy, with the renderer that draws them.</summary>
        readonly List<(Mesh mesh, Renderer renderer)> readable = new List<(Mesh, Renderer)>();
        int releaseCursor;
        bool hasLast, hasFocus;
        double lastEast, lastNorth, focusEast, focusNorth, focusHeight;
        int held;
        RenderWarmup renderWarmup;
        ChunkView warmView;
        Mesh warmNear;

        [Serializable] class WorldIndex { public Entry[] chunks; }
        [Serializable] class Entry { public string id; }
        [Serializable] class PlaceFile { public Place[] items; }
        [Serializable] class Place { public string nameEn; public double x, z; }

        void Start()
        {
            if (string.IsNullOrEmpty(WorldDir)) WorldDir = GameRoot.WorldDir;
            worldMat = Resources.Load<Material>("Materials/World");
            glowMat = Resources.Load<Material>("Materials/Glow");
            facadeMat = Resources.Load<Material>("Materials/WorldFacades");
            nearMat = Resources.Load<Material>("Materials/WorldNear");
            buildingMat = Resources.Load<Material>("Materials/ChunkBuildings");
            roadMat = Resources.Load<Material>("Materials/ChunkRoads");
            gridMat = Resources.Load<Material>("Materials/DebugGrid");
            graphMat = Resources.Load<Material>("Materials/DebugGraph");
            nodeMat = Resources.Load<Material>("Materials/DebugNodes");
            signalMat = Resources.Load<Material>("Materials/DebugSignals");
            var file = Path.Combine(WorldDir, "world.json");
            indexTask = Task.Run(() =>
            {
                var set = new HashSet<ChunkCell>();
                foreach (var e in JsonUtility.FromJson<WorldIndex>(File.ReadAllText(file)).chunks)
                    if (ChunkCell.TryParse(e.id, out var c)) set.Add(c);
                ReadPlaces(Path.Combine(Path.GetDirectoryName(file), "places.json"));
                return set;
            });
        }

        bool Available(ChunkCell c) =>
            index.Contains(c) && (!SliceOnly || (c.X >= Slice.xMin && c.X < Slice.xMax && c.Z >= Slice.yMin && c.Z < Slice.yMax));

        void Update()
        {
            if (renderWarmup != null && renderWarmup.TryFinish()) EndRenderWarmup();
            if (Planner == null && !TryFinishIndex()) return;
            if (Focus == null) return;

            using (DadMarkers.StreamingPlan.Auto())
            {
                var offset = Origin != null ? Origin.Offset : null;
                var p = Focus.position;
                double east = p.x, north = p.z;
                if (offset != null) offset.ToWorld(p.x, p.z, out east, out north);
                focusEast = east;
                focusNorth = north;
                focusHeight = p.y;
                hasFocus = true;
                Shader.SetGlobalVector(NearFocusId, new Vector4(p.x, p.y, p.z, 0));
                Shader.SetGlobalVector(NearFadeId, new Vector4((float)ChunkBuildings.FadeStart, (float)ChunkBuildings.FadeEnd, 0, 0));
                var focus = ChunkCell.Of(east, north);
                ChunkCell? lead = null;
                float dt = Time.unscaledDeltaTime;
                if (hasLast && dt > 0f)
                {
                    double vx = (east - lastEast) / dt, vz = (north - lastNorth) / dt;
                    // A jump of more than 200 m in one frame is a teleport, not travel.
                    if (Math.Abs(east - lastEast) < 200 && Math.Abs(north - lastNorth) < 200)
                        lead = ChunkCell.Of(east + vx * LeadSeconds, north + vz * LeadSeconds);
                }
                hasLast = true;
                lastEast = east;
                lastNorth = north;

                unload.Clear();
                cancel.Clear();
                Planner.Update(focus, lead, unload, cancel);
                foreach (var c in unload) Drop(c);
                foreach (var c in cancel) CancelLoad(c);
                using (DadMarkers.StreamingCollect.Auto()) Collect();
                take.Clear();
                Planner.TakeNext(MaxInFlight - inFlight.Count, take);
                using (DadMarkers.StreamingStart.Auto())
                    foreach (var c in take) StartLoad(c);
            }
            UpdateStreetLod();
            CollectKits();
            colliders.Collect();
            ReleaseDrawnMeshes();
            // Once a wait runs out, the queue is applied without waiting until it is empty.
            if (applyQueue.Count > 0 && (inFlight.Count > 0 || kitJobs.Count > 0) && held < MaxApplyHoldFrames)
            {
                held++;
                HeldFrames++;
                MaxHeldFrames = Math.Max(MaxHeldFrames, held);
                return;
            }
            RunApply();
            if (applyQueue.Count == 0) held = 0;
        }

        bool TryFinishIndex()
        {
            if (indexTask == null || !indexTask.IsCompleted) return false;
            if (indexTask.IsFaulted)
            {
                if (IndexError == null)
                {
                    IndexError = indexTask.Exception?.GetBaseException().Message ?? "unknown";
                    Debug.LogError("[Dad] streaming: cannot read world.json: " + IndexError);
                }
                return false;
            }
            index = indexTask.Result;
            Planner = new StreamPlanner(LoadRadius, UnloadRadius, Available);
            WarmUp();
            return true;
        }

        /// <summary>Gives the near facades' district rule its named places (Districts), before any chunk is built; without the
        /// file every building is in no district.</summary>
        static void ReadPlaces(string path)
        {
            if (!File.Exists(path))
            {
                Debug.LogWarning("[Dad] streaming: no places.json; near facades use no districts");
                return;
            }
            var items = JsonUtility.FromJson<PlaceFile>(File.ReadAllText(path)).items ?? Array.Empty<Place>();
            var x = new double[items.Length];
            var z = new double[items.Length];
            var names = new string[items.Length];
            for (int i = 0; i < items.Length; i++) { x[i] = items[i].x; z[i] = items[i].z; names[i] = items[i].nameEn; }
            Districts.Set(x, z, names);
        }

        void WarmUp()
        {
            var sw = Stopwatch.StartNew();
            var chunk = new DadcChunk
            {
                Ix = 0, Iy = -1,
                Roads = new[] { new Road { WidthCm = 800, YCm = 28, Pts = new[] { 0, 0, 1000, 1000 } } },
                Buildings = new[] { new Building { Outer = new[] { 0, 0, 100, 0, 100, 100 } } },
                Parks = new[] { new Poly { Outer = new[] { 0, 0, 100, 0, 100, 100 } } },
                Trees = new[] { new Dad.World.Tree { East = 2000, North = 300, HeightCm = 800 } },
                Points = new[] { new PointFeature { Kind = "lamp", East = 300, North = 2000 } },
            };
            var loads = new Load[Math.Max(1, MaxInFlight)];
            for (int i = 0; i < loads.Length; i++)
            {
                var cts = new CancellationTokenSource();
                loads[i] = new Load { Cell = new ChunkCell(0, 0), Task = Task.Run(() => Build(chunk), cts.Token), Cts = cts };
            }
            Payload data = null;
            foreach (var l in loads)
            {
                l.Task.Wait();
                var built = Finish(l, out _);
                if (data == null) data = built;
                else built?.Release();
            }
            var job = new ApplyJob { Cell = loads[0].Cell, Data = data };
            while (!Step(job)) { }
            warmView = job.View;
            // The made-up chunk has no wall long enough for a near facade: one triangle in the near material, so its shader
            // variant is drawn too.
            warmNear = Coloured(warmView.Root, "Near warm-up", new[] { 0f, 0f, 0f, 1f, 0f, 0f, 0f, 1f, 0f }, new[] { 0xff808080u, 0xff808080u, 0xff808080u }, new[] { 0, 1, 2 },
                3, 3, true, new Bounds(new Vector3(0.5f, 0.5f, 0f), Vector3.one), 0, 0, out _, nearFrom: 0);
            renderWarmup = new RenderWarmup(warmView.Root);
            PreparedMethods = PrepareCode();
            ChunkStreet.PrepareNearKitScratch(MaxKitJobs);
            WarmupMs = (float)sw.Elapsed.TotalMilliseconds;
        }

        void EndRenderWarmup()
        {
            renderWarmup.Finish();
            DestroyView(warmView);
            Destroy(warmNear);
            warmNear = null;
            renderWarmup = null;
            warmView = null;
        }

        /// <summary>Compiles, without running them, every method of the streamer, the planner, their
        /// nested types and the generic and threading types of their fields (such as
        /// Dictionary&lt;ChunkCell, ChunkView&gt;, Task&lt;Payload&gt;, CancellationTokenSource); Unity types are
        /// left out, and so are arrays, delegates and methods the runtime implements itself: asking Mono
        /// for those prints "runtime and class libraries are out of sync" and once ended a player run.
        /// Mono compiles a method on GetFunctionPointer; RuntimeHelpers.PrepareMethod does nothing
        /// there. Returns the number of methods compiled.</summary>
        public static int PrepareCode()
        {
            const BindingFlags all = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            var types = new HashSet<Type>();
            var pending = new Stack<Type>(new[] { typeof(ChunkStreamer), typeof(StreamPlanner) });
            while (pending.Count > 0)
            {
                var t = pending.Pop();
                if (t.IsArray) { pending.Push(t.GetElementType()); continue; }
                bool ours = t.Assembly == typeof(ChunkStreamer).Assembly || t.Assembly == typeof(StreamPlanner).Assembly;
                bool wanted = ours || t.IsGenericType || t.Namespace == "System.Threading" || t.Namespace == "System.Threading.Tasks";
                if (!wanted || t.IsArray || t.IsPointer || t.IsByRef || typeof(Delegate).IsAssignableFrom(t) || t.ContainsGenericParameters || !types.Add(t)) continue;
                if (ours)
                {
                    foreach (var n in t.GetNestedTypes(all)) pending.Push(n);
                    foreach (var field in t.GetFields(all)) pending.Push(field.FieldType);
                }
            }
            int compiled = 0;
            foreach (var t in types)
            {
                if (t.IsInterface || t.IsPrimitive || t.IsEnum) continue;
                foreach (var m in t.GetMethods(all))
                {
                    if (m.IsAbstract || m.ContainsGenericParameters) continue;
                    var impl = m.GetMethodImplementationFlags();
                    if ((impl & (MethodImplAttributes.InternalCall | MethodImplAttributes.Runtime)) != 0 || (m.Attributes & MethodAttributes.PinvokeImpl) != 0) continue;
                    try { m.MethodHandle.GetFunctionPointer(); compiled++; }
                    catch (Exception) { } // a method the runtime cannot compile ahead stays as it was
                }
            }
            return compiled;
        }

        void StartLoad(ChunkCell cell)
        {
            var cts = new CancellationTokenSource();
            var token = cts.Token;
            var file = Path.Combine(WorldDir, "chunks", cell.Id + ".dadc");
            int delay = TestLoadDelayMs;
            var task = Task.Run(() =>
            {
                if (delay > 0) token.WaitHandle.WaitOne(delay);
                token.ThrowIfCancellationRequested();
                var bytes = File.ReadAllBytes(file);
                token.ThrowIfCancellationRequested();
                var chunk = Dadc.Load(bytes);
                // Elevated rails across the borders, so the viaduct pairs twins and places piers as its neighbours do (P-2, K-50).
                NeighbourRails.Attach(chunk, Path.GetDirectoryName(file));
                token.ThrowIfCancellationRequested();
                return Build(chunk);
            }, token);
            inFlight.Add(new Load { Cell = cell, Task = task, Cts = cts });
        }

        /// <summary>All of a chunk's meshes, built with a BuildScratch set lent for the purpose. Road, building
        /// and line arrays are pooled (MeshArrays); their apply steps hand them back after the upload.</summary>
        static Payload Build(DadcChunk chunk)
        {
            using (BuildScratch.Rent())
            {
                var p = new Payload { Chunk = chunk, Surfaces = ChunkSurfaces.Build(chunk), Roads = ChunkRoads.Build(chunk, pooled: true, keepWalks: BuildWalks),
                    Solids = ChunkBuildings.Build(chunk, pooled: true), Overlays = ChunkDebug.Build(chunk), Outlines = ChunkOutlines.Build(chunk, pooled: true) };
                if (BuildLanes) p.Lanes = ChunkLanes.Build(chunk, p.Roads.Street?.Blocks);
                if (BuildWalks) p.Walks = ChunkWalks.Build(chunk.Ix, chunk.Iy, chunk.CornerEastCm / 100.0, chunk.CornerNorthCm / 100.0, p.Roads.Walks);
                if (BuildColliders) p.Collision = ChunkCollision.Build(chunk, p.Roads, p.Solids, pooled: true);
                return p;
            }
        }

        /// <summary>A completed load's payload, or null if it was cancelled or failed (then with its error).</summary>
        static Payload Finish(Load l, out Exception error)
        {
            l.Cts.Dispose();
            error = null;
            if (l.Task.IsCanceled) return null;
            if (l.Task.IsFaulted)
            {
                error = l.Task.Exception?.GetBaseException();
                return null;
            }
            return l.Task.Result;
        }

        void Collect()
        {
            for (int i = inFlight.Count - 1; i >= 0; i--)
            {
                var l = inFlight[i];
                if (!l.Task.IsCompleted) continue;
                inFlight.RemoveAt(i);
                var data = Finish(l, out var error);
                if (l.Task.IsFaulted)
                {
                    Failures++;
                    Debug.LogWarning($"[Dad] streaming: {l.Cell.Id} failed: {error?.Message}");
                    Planner.Failed(l.Cell);
                    continue;
                }
                if (data == null) continue; // cancelled, already counted in CancelLoad
                if (Planner.Loaded(l.Cell)) applyQueue.AddLast(new ApplyJob { Cell = l.Cell, Data = data });
                else
                {
                    data.Release();
                    Discarded++;
                }
            }
        }

        void CancelLoad(ChunkCell cell)
        {
            foreach (var l in inFlight)
                if (l.Cell.Equals(cell)) { l.Cts.Cancel(); Cancelled++; return; }
        }

        void Drop(ChunkCell cell)
        {
            for (var n = applyQueue.First; n != null; n = n.Next)
            {
                if (!n.Value.Cell.Equals(cell)) continue;
                DestroyView(n.Value.View); // a half-built chunk
                n.Value.Data?.Release();
                applyQueue.Remove(n);
                return;
            }
            if (!resident.TryGetValue(cell, out var view)) return;
            using (DadMarkers.StreamingUnload.Auto())
            {
                resident.Remove(cell);
                DestroyView(view);
            }
            Unloaded++;
            ChunkOut?.Invoke(cell);
        }

        static void DestroyView(ChunkView view)
        {
            if (view == null) return;
            view.Gone = true;
            foreach (var m in view.KitMeshes) if (m != null) Destroy(m);
            if (Array.IndexOf(view.KitPending, true) < 0) ReleaseKitData(view); // else once the last build finishes
            if (view.Surfaces != null) Destroy(view.Surfaces);
            if (view.RoadMesh != null) Destroy(view.RoadMesh);
            if (view.BuildingMesh != null) Destroy(view.BuildingMesh);
            if (view.GridMesh != null) Destroy(view.GridMesh);
            if (view.GraphMesh != null) Destroy(view.GraphMesh);
            if (view.Lines != null) Destroy(view.Lines);
            if (view.StructureMesh != null) Destroy(view.StructureMesh);
            if (view.GlowMesh != null) Destroy(view.GlowMesh);
            if (view.CollisionMesh != null) Destroy(view.CollisionMesh);
            foreach (var m in view.CellMeshes) if (m != null) Destroy(m);
            if (view.Root != null) Destroy(view.Root);
        }

        void RunApply()
        {
            if (applyQueue.Count == 0) return;
            using (DadMarkers.StreamingApply.Auto())
            {
                clock.Restart();
                double budget = ApplyBudgetMs;
                int steps = 0;
                while (applyQueue.Count > 0 && (steps == 0 || clock.Elapsed.TotalMilliseconds < budget))
                {
                    double before = clock.Elapsed.TotalMilliseconds;
                    var job = applyQueue.First.Value;
                    int kind = job.Step <= 5 ? job.Step : job.Step <= LastCellStep ? 6 : 7;
                    int collections = GC.CollectionCount(0);
                    var lanes = job.Data?.Lanes; // the last step lets go of the data
                    var walks = job.Data?.Walks;
                    bool done = Step(job);
                    steps++;
                    ApplySteps++;
                    float ms = (float)(clock.Elapsed.TotalMilliseconds - before);
                    if (GC.CollectionCount(0) != collections)
                    {
                        StepsWithGc++;
                        MaxGcStepMs = Mathf.Max(MaxGcStepMs, ms);
                    }
                    MaxApplyStepMs = Mathf.Max(MaxApplyStepMs, ms);
                    MaxStepMsByKind[kind] = Mathf.Max(MaxStepMsByKind[kind], ms);
                    if (!done) continue;
                    applyQueue.RemoveFirst();
                    if (StreamPlanner.Chebyshev(Planner.Focus, job.Cell) >= UnloadRadius) AppliedOutsideRings++;
                    Planner.Applied(job.Cell);
                    resident[job.Cell] = job.View;
                    Applied++;
                    ChunkIn?.Invoke(job.Cell);
                    if (lanes != null) LanesIn?.Invoke(job.Cell, lanes);
                    if (walks != null) WalksIn?.Invoke(job.Cell, walks);
                }
                MaxApplyFrameMs = Mathf.Max(MaxApplyFrameMs, (float)clock.Elapsed.TotalMilliseconds);
            }
        }

        /// <summary>One small piece of building a chunk. Returns true when the chunk is complete.</summary>
        bool Step(ApplyJob job)
        {
            switch (job.Step++)
            {
                case 0:
                {
                    var root = new GameObject(job.Cell.Id);
                    var offset = Origin != null ? Origin.Offset : null;
                    float x = (float)job.Cell.CornerEast, z = (float)job.Cell.CornerNorth;
                    if (offset != null) offset.ToScene(job.Cell.CornerEast, job.Cell.CornerNorth, out x, out z);
                    root.transform.position = new Vector3(x, 0f, z);
                    job.View = new ChunkView
                    {
                        Cell = job.Cell,
                        Root = root,
                        Placed = job.Data.Chunk.Placed,
                        Buildings = job.Data.Chunk.Buildings.Length,
                        Roads = job.Data.Chunk.Roads.Length,
                        Vertices = job.Data.Outlines.VertexCount,
                        SurfaceFeatures = job.Data.Surfaces.Features,
                        SurfaceFailed = job.Data.Surfaces.Failed,
                        RoadsDrawn = job.Data.Roads.Roads,
                        BorderCuts = job.Data.Roads.BorderCuts,
                        BuildingsDrawn = job.Data.Solids.Drawn,
                        BuildingsRaised = job.Data.Solids.RaisedOsm + job.Data.Solids.RaisedRule + job.Data.Solids.RaisedByLayer,
                    };
                    if (job.Data.Collision != null) colliders.Start(job.View, job.Data.Collision);
                    return false;
                }
                case 1:
                {
                    var s = job.Data.Surfaces;
                    job.View.Surfaces = Coloured(job.View.Root, "Ground", s.Positions, s.Colors, s.Indices, s.VertexCount, s.Indices.Length, false,
                        new Bounds(new Vector3(250f, 0.1f, 250f), new Vector3(520f, 0.5f, 520f)));
                    return false;
                }
                case 2:
                {
                    var r = job.Data.Roads;
                    job.View.RoadMesh = Coloured(job.View.Root, "Roads", r.Positions, r.Colors, r.Indices, r.VertexCount, r.IndexCount, false,
                        new Bounds(new Vector3(250f, 9f, 250f), new Vector3(560f, 20f, 560f)));
                    r.Release();
                    return false;
                }
                case 3:
                {
                    var b = job.Data.Solids;
                    var min = new Vector3(b.Min[0], b.Min[1], b.Min[2]);
                    var max = new Vector3(b.Max[0], b.Max[1], b.Max[2]);
                    var bounds = new Bounds((min + max) * 0.5f, max - min + Vector3.one);
                    job.View.BuildingMesh = Coloured(job.View.Root, "Buildings", b.Positions, b.Colors, b.Indices, b.VertexCount, b.IndexCount, true, bounds, 0, 0, out _, b.Facade);
                    b.Release();
                    job.View.Facades = b;
                    job.Data.FacadesTaken = true;
                    return false;
                }
                case 4:
                {
                    var d = job.Data.Overlays;
                    var lineBounds = new Bounds(new Vector3(250f, 0.5f, 250f), new Vector3(520f, 2f, 520f));
                    job.View.GridMesh = LineMesh(job.View.Root, "Grid", d.GridPositions, d.GridIndices, new[] { d.GridIndices.Length }, new[] { gridMat }, lineBounds, out job.View.Grid);
                    job.View.GraphMesh = LineMesh(job.View.Root, "Graph", d.GraphPositions, d.GraphIndices,
                        new[] { d.EdgeIndexCount, d.NodeIndexCount, d.SignalIndexCount }, new[] { graphMat, nodeMat, signalMat }, lineBounds, out job.View.Graph);
                    ApplyOverlays(job.View);
                    return false;
                }
                case 5:
                {
                    var st = job.Data.Roads.Street;
                    job.View.Street = st;
                    job.Data.KitTaken = true;
                    if (st.Vertices[0] > 0)
                    {
                        var min = new Vector3(st.StructMin[0], st.StructMin[1], st.StructMin[2]);
                        var max = new Vector3(st.StructMax[0], st.StructMax[1], st.StructMax[2]);
                        job.View.StructureMesh = Coloured(job.View.Root, "Structures", st.Positions, st.Colors, st.Indices, st.Vertices[0], st.IndexCounts[0], true,
                            new Bounds((min + max) * 0.5f, max - min + Vector3.one), st.FirstVertex[0], st.FirstIndex[0], out _);
                    }
                    return false;
                }
                case int step when step <= LastCellStep:
                {
                    var st = job.Data.Roads.Street;
                    for (int c = (step - FirstCellStep) * CellsPerStep, last = c + CellsPerStep; c < last; c++)
                    {
                        for (int lod = 0; lod < 2; lod++)
                        {
                            int range = ChunkStreet.Range(c, lod);
                            if (st.IndexCounts[range] == 0) continue;
                            // The range's own extent, a metre larger: a cable span reaches past the cell's square.
                            var min = new Vector3(st.RangeMin[3 * range], st.RangeMin[3 * range + 1], st.RangeMin[3 * range + 2]);
                            var max = new Vector3(st.RangeMax[3 * range], st.RangeMax[3 * range + 1], st.RangeMax[3 * range + 2]);
                            var bounds = new Bounds((min + max) * 0.5f, max - min + Vector3.one);
                            job.View.CellMeshes[2 * c + lod] = Coloured(job.View.Root, lod == 0 ? "Street near" : "Street far", st.Positions, st.Colors, st.Indices,
                                st.Vertices[range], st.IndexCounts[range], true, bounds, st.FirstVertex[range], st.FirstIndex[range], out var renderer);
                            job.View.CellRenderers[2 * c + lod] = renderer;
                        }
                        job.View.CellTier[c] = -1;
                        ShowCell(job.View, c);
                        job.View.CellsMade = c + 1;
                    }
                    if (step == LastCellStep)
                    {
                        st.Release();
                        AddGlow(job.View, st.LampHeads);
                    }
                    return false;
                }
                default:
                {
                    var o = job.Data.Outlines;
                    var mesh = new Mesh { name = job.Cell.Id + " lines" };
                    mesh.SetVertexBufferParams(o.VertexCount, new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3));
                    mesh.SetVertexBufferData(o.Vertices, 0, 0, 3 * o.VertexCount, 0, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
                    mesh.SetIndexBufferParams(o.IndexCount, IndexFormat.UInt32);
                    mesh.SetIndexBufferData(o.Indices, 0, 0, o.IndexCount, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
                    o.Release();
                    mesh.subMeshCount = 2;
                    var flags = MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices;
                    mesh.SetSubMesh(0, new SubMeshDescriptor(0, o.BuildingIndexCount, MeshTopology.Lines), flags);
                    mesh.SetSubMesh(1, new SubMeshDescriptor(o.BuildingIndexCount, o.RoadIndexCount, MeshTopology.Lines), flags);
                    mesh.bounds = new Bounds(new Vector3(250f, 0f, 250f), new Vector3(520f, 2f, 520f));
                    var lines = new GameObject("Lines", typeof(MeshFilter), typeof(MeshRenderer));
                    lines.transform.SetParent(job.View.Root.transform, false);
                    lines.GetComponent<MeshFilter>().sharedMesh = mesh;
                    var r = lines.GetComponent<MeshRenderer>();
                    r.sharedMaterials = new[] { buildingMat, roadMat };
                    r.shadowCastingMode = ShadowCastingMode.Off;
                    KeepUntilDrawn(mesh, r);
                    job.View.Lines = mesh;
                    job.View.LineObject = lines;
                    ApplyOverlays(job.View);
                    job.Data = null;
                    return true;
                }
            }
        }

        /// <summary>Shows or hides the debug views on every chunk, resident or still being applied, and on later ones.</summary>
        public void SetOverlays(bool grid, bool graph, bool lines)
        {
            ShowGrid = grid;
            ShowGraph = graph;
            ShowLines = lines;
            foreach (var v in resident.Values) ApplyOverlays(v);
            foreach (var j in applyQueue) ApplyOverlays(j.View);
        }

        void ApplyOverlays(ChunkView v)
        {
            if (v == null) return;
            if (v.Grid != null) v.Grid.SetActive(ShowGrid);
            if (v.Graph != null) v.Graph.SetActive(ShowGraph);
            if (v.LineObject != null) v.LineObject.SetActive(ShowLines);
        }

        /// <summary>A child with a line mesh (Float32 x3 positions), one submesh and material per index range.</summary>
        Mesh LineMesh(GameObject root, string name, float[] positions, int[] indices, int[] counts, Material[] materials, Bounds bounds, out GameObject go)
        {
            var flags = MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices;
            var mesh = new Mesh { name = root.name + " " + name.ToLowerInvariant() };
            mesh.SetVertexBufferParams(positions.Length / 3, new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3));
            mesh.SetVertexBufferData(positions, 0, 0, positions.Length, 0, flags);
            mesh.SetIndexBufferParams(indices.Length, IndexFormat.UInt32);
            mesh.SetIndexBufferData(indices, 0, 0, indices.Length, flags);
            mesh.subMeshCount = counts.Length;
            int start = 0;
            for (int i = 0; i < counts.Length; i++)
            {
                mesh.SetSubMesh(i, new SubMeshDescriptor(start, counts[i], MeshTopology.Lines), flags);
                start += counts[i];
            }
            mesh.bounds = bounds;
            go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(root.transform, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterials = materials;
            r.shadowCastingMode = ShadowCastingMode.Off;
            KeepUntilDrawn(mesh, r);
            return mesh;
        }

        /// <summary>The pools and halos of a chunk's street lamps (D-13), and the lamps for StreetLamps.</summary>
        void AddGlow(ChunkView v, ChunkStreet.LampHead[] lamps)
        {
            v.Lamps = lamps;
            if (lamps.Length == 0 || glowMat == null) return;
            var g = StreetGlow.Build(lamps);
            var flags = MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices;
            var mesh = new Mesh { name = v.Cell.Id + " glow" };
            mesh.SetVertexBufferParams(g.VertexCount,
                new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, 0),
                new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4, 1),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2, 2));
            mesh.SetVertexBufferData(g.Positions, 0, 0, g.Positions.Length, 0, flags);
            mesh.SetVertexBufferData(g.Colors, 0, 0, g.Colors.Length, 1, flags);
            mesh.SetVertexBufferData(g.Corners, 0, 0, g.Corners.Length, 2, flags);
            mesh.SetIndexBufferParams(g.Indices.Length, IndexFormat.UInt32);
            mesh.SetIndexBufferData(g.Indices, 0, 0, g.Indices.Length, flags);
            mesh.SetSubMesh(0, new SubMeshDescriptor(0, g.Indices.Length, MeshTopology.Triangles), flags);
            mesh.bounds = new Bounds(new Vector3(250f, 6f, 250f), new Vector3(540f, 20f, 540f));
            var go = new GameObject("Glow", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(v.Root.transform, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = glowMat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            v.GlowMesh = mesh;
        }

        /// <summary>A child with a vertex-coloured mesh (Float32 x3 positions, RGBA8 colours with the material in alpha)
        /// in the world material, from the first vertexCount vertices and indexCount indices of the arrays. It receives
        /// shadows, and casts them if castShadows.</summary>
        Mesh Coloured(GameObject root, string name, float[] positions, uint[] colors, int[] indices, int vertexCount, int indexCount, bool castShadows, Bounds bounds) =>
            Coloured(root, name, positions, colors, indices, vertexCount, indexCount, castShadows, bounds, 0, 0, out _);

        /// <summary>As above, from firstVertex and firstIndex on (the indices relative to firstVertex); also gives the renderer.
        /// With facade (four floats a vertex, ChunkBuildings.Facade), a third stream in TEXCOORD0 and the facade material.
        /// With nearFrom below indexCount, the indices from nearFrom on are near facades (D-06c): a submesh of their own
        /// in the near material.</summary>
        Mesh Coloured(GameObject root, string name, float[] positions, uint[] colors, int[] indices, int vertexCount, int indexCount, bool castShadows, Bounds bounds,
            int firstVertex, int firstIndex, out MeshRenderer renderer, float[] facade = null, int nearFrom = int.MaxValue)
        {
            var flags = MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices;
            var mesh = new Mesh { name = root.name + " " + name.ToLowerInvariant() };
            if (facade == null)
                mesh.SetVertexBufferParams(vertexCount,
                    new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, 0),
                    new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4, 1));
            else
                mesh.SetVertexBufferParams(vertexCount,
                    new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, 0),
                    new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4, 1),
                    new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 4, 2));
            mesh.SetVertexBufferData(positions, 3 * firstVertex, 0, 3 * vertexCount, 0, flags);
            mesh.SetVertexBufferData(colors, firstVertex, 0, vertexCount, 1, flags);
            if (facade != null) mesh.SetVertexBufferData(facade, 4 * firstVertex, 0, 4 * vertexCount, 2, flags);
            mesh.SetIndexBufferParams(indexCount, IndexFormat.UInt32);
            mesh.SetIndexBufferData(indices, firstIndex, 0, indexCount, flags);
            bool split = nearFrom > 0 && nearFrom < indexCount;
            mesh.subMeshCount = split ? 2 : 1;
            if (split)
            {
                mesh.SetSubMesh(0, new SubMeshDescriptor(0, nearFrom), flags);
                mesh.SetSubMesh(1, new SubMeshDescriptor(nearFrom, indexCount - nearFrom), flags);
            }
            else mesh.SetSubMesh(0, new SubMeshDescriptor(0, indexCount), flags);
            mesh.bounds = bounds;
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(root.transform, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.GetComponent<MeshRenderer>();
            if (split) r.sharedMaterials = new[] { worldMat, nearMat };
            else r.sharedMaterial = facade != null ? facadeMat : nearFrom < indexCount ? nearMat : worldMat;
            r.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            r.receiveShadows = true;
            renderer = r;
            KeepUntilDrawn(mesh, r);
            return mesh;
        }

        /// <summary>Lists a new mesh to have its CPU copy dropped once r has drawn it, unless KeepMeshesReadable.</summary>
        void KeepUntilDrawn(Mesh mesh, Renderer r)
        {
            if (!KeepMeshesReadable) readable.Add((mesh, r));
        }

        /// <summary>Looks at up to ReleaseScan listed meshes, going round the list, and drops the CPU copy of each one that was
        /// drawn (isVisible: a camera or shadow pass saw it last frame, so it is on the GPU), until ReleaseBudgetMs is spent.
        /// Meshes destroyed with their chunk leave the list.</summary>
        void ReleaseDrawnMeshes()
        {
            if (readable.Count == 0) return;
            using (DadMarkers.StreamingRelease.Auto())
            {
                long start = Stopwatch.GetTimestamp();
                for (int n = 0; n < ReleaseScan && readable.Count > 0; n++)
                {
                    if (releaseCursor >= readable.Count) releaseCursor = 0;
                    var (mesh, r) = readable[releaseCursor];
                    if (mesh == null || r == null) { RemoveReadable(releaseCursor); continue; }
                    if (!r.isVisible) { releaseCursor++; continue; }
                    long before = Stopwatch.GetTimestamp();
                    mesh.UploadMeshData(true);
                    long now = Stopwatch.GetTimestamp();
                    MaxReleaseMs = Mathf.Max(MaxReleaseMs, (float)((now - before) * 1000.0 / Stopwatch.Frequency));
                    RemoveReadable(releaseCursor);
                    Released++;
                    if ((now - start) * 1000.0 / Stopwatch.Frequency >= ReleaseBudgetMs) break;
                }
                MaxReleaseFrameMs = Mathf.Max(MaxReleaseFrameMs, (float)((Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency));
            }
        }

        void RemoveReadable(int i)
        {
            int last = readable.Count - 1;
            readable[i] = readable[last];
            readable.RemoveAt(last);
        }

        /// <summary>Picks each resident chunk's street cells' level of detail from the focus's distance (StreetLod), drops near
        /// rooftop kits beyond KitDropDistance and starts those within KitBuildDistance, cells drawn near first; counts
        /// cells drawn near (in resident and half-applied chunks) whose kit is not there yet.</summary>
        void UpdateStreetLod()
        {
            if (!hasFocus) return;
            int near = 0, far = 0, late = 0, kits = 0;
            foreach (var v in resident.Values)
            {
                for (int c = 0; c < v.CellTier.Length; c++)
                {
                    ShowCell(v, c);
                    if (v.CellTier[c] == StreetLod.Near) near++;
                    else if (v.CellTier[c] == StreetLod.Far) far++;
                    if (v.KitMeshes[c] != null && CellDistance(v, c) > KitDropDistance) DropKit(v, c);
                    if (v.KitMeshes[c] != null) kits++;
                    if (Late(v, c)) late++;
                }
            }
            foreach (var j in applyQueue)
                if (j.View != null)
                    for (int c = 0; c < j.View.CellsMade; c++)
                        if (Late(j.View, c)) late++;
            NearCells = near;
            FarCells = far;
            KitsHeld = kits;
            if (late > 0)
            {
                KitLateFrames++;
                MaxKitLateCells = Math.Max(MaxKitLateCells, late);
            }
            for (int pass = 0; pass < 2 && kitJobs.Count < MaxKitJobs; pass++)
                foreach (var v in resident.Values)
                    for (int c = 0; c < v.CellTier.Length && kitJobs.Count < MaxKitJobs; c++)
                        if (WantsKit(v, c) && (pass == 1 || v.CellTier[c] == StreetLod.Near) && CellDistance(v, c) < KitBuildDistance) StartKit(v, c);
        }

        bool WantsKit(ChunkView v, int c) => HasNear(v, c) && v.KitMeshes[c] == null && !v.KitPending[c] && !v.KitFailed[c];

        /// <summary>Does cell c have a near kit to build: rooftop kit items or near facade walls?</summary>
        static bool HasNear(ChunkView v, int c) => v.Street != null && (v.Street.HasNearKit(c) || v.Facades != null && v.Facades.HasNearFacade(c));

        /// <summary>Is cell c drawn near without its near kit?</summary>
        static bool Late(ChunkView v, int c) => c < v.CellsMade && v.CellTier[c] == StreetLod.Near && HasNear(v, c) && v.KitMeshes[c] == null;

        /// <summary>Hands back what a view's near kits are built from; none may be building.</summary>
        static void ReleaseKitData(ChunkView v)
        {
            v.Street?.ReleaseRoofKit();
            v.Facades?.ReleaseNearWalls();
        }

        /// <summary>The distance StreetLod measures from the focus to cell c of a chunk: on the ground plan to the cell's
        /// square, and the focus's height above 10 m.</summary>
        public double CellDistance(ChunkView v, int c)
        {
            double cx = v.Cell.CornerEast + c % ChunkStreet.Cells * ChunkStreet.CellSize, cz = v.Cell.CornerNorth + c / ChunkStreet.Cells * ChunkStreet.CellSize;
            double flat = StreetLod.ToCell(focusEast, focusNorth, cx, cz), up = Math.Max(0, focusHeight - 10);
            return Math.Sqrt(flat * flat + up * up);
        }

        /// <summary>Shows cell c of a chunk at the tier for the focus's distance (CellDistance), its near rooftop kit with its near tier.</summary>
        void ShowCell(ChunkView v, int c)
        {
            if (!hasFocus || v.CellRenderers[2 * c] == null && v.CellRenderers[2 * c + 1] == null && !HasNear(v, c)) return;
            int tier = StreetLod.Tier(CellDistance(v, c), v.CellTier[c]);
            if (tier == v.CellTier[c]) return;
            v.CellTier[c] = tier;
            if (v.CellRenderers[2 * c] != null) v.CellRenderers[2 * c].enabled = tier == StreetLod.Near;
            if (v.CellRenderers[2 * c + 1] != null) v.CellRenderers[2 * c + 1].enabled = tier == StreetLod.Far;
            if (v.KitRenderers[c] != null) v.KitRenderers[c].enabled = tier == StreetLod.Near;
        }

        /// <summary>Builds cell c's near kit (rooftop kit and near facades) on a worker.</summary>
        void StartKit(ChunkView v, int c)
        {
            var st = v.Street;
            var fa = v.Facades;
            int delay = TestKitDelayMs;
            v.KitPending[c] = true;
            kitJobs.Add(new KitJob { View = v, Cell = c, Task = Task.Run(() =>
            {
                if (delay > 0) Thread.Sleep(delay);
                return st.BuildNearKit(c, fa, pooled: true);
            }) });
        }

        /// <summary>Uploads up to MaxKitUploads finished near kits, oldest first; a kit for a chunk gone or a cell gone past
        /// KitDropDistance is handed back instead. A failed build is counted and not tried again.</summary>
        void CollectKits()
        {
            if (kitJobs.Count == 0) return;
            using (DadMarkers.StreamingKit.Auto())
            {
                long start = Stopwatch.GetTimestamp();
                int uploads = 0;
                for (int i = 0; i < kitJobs.Count && uploads < MaxKitUploads;)
                {
                    var j = kitJobs[i];
                    if (!j.Task.IsCompleted) { i++; continue; }
                    kitJobs.RemoveAt(i);
                    var v = j.View;
                    v.KitPending[j.Cell] = false;
                    if (j.Task.IsFaulted)
                    {
                        KitFailures++;
                        v.KitFailed[j.Cell] = true;
                        Debug.LogWarning($"[Dad] streaming: {v.Cell.Id} cell {j.Cell} rooftop kit failed: {j.Task.Exception?.GetBaseException().Message}");
                        if (v.Gone && Array.IndexOf(v.KitPending, true) < 0) ReleaseKitData(v);
                        continue;
                    }
                    var kit = j.Task.Result;
                    if (v.Gone || CellDistance(v, j.Cell) > KitDropDistance)
                    {
                        kit.Release();
                        KitsDiscarded++;
                        if (v.Gone && Array.IndexOf(v.KitPending, true) < 0) ReleaseKitData(v);
                        continue;
                    }
                    long before = Stopwatch.GetTimestamp();
                    var min = new Vector3(kit.Min[0], kit.Min[1], kit.Min[2]);
                    var max = new Vector3(kit.Max[0], kit.Max[1], kit.Max[2]);
                    v.KitMeshes[j.Cell] = Coloured(v.Root, "Roof kit near", kit.Positions, kit.Colors, kit.Indices, kit.VertexCount, kit.IndexCount, true,
                        new Bounds((min + max) * 0.5f, max - min + Vector3.one), 0, 0, out var renderer, nearFrom: kit.RoofIndexCount);
                    v.KitRenderers[j.Cell] = renderer;
                    renderer.enabled = v.CellTier[j.Cell] == StreetLod.Near;
                    kit.Release();
                    MaxKitUploadMs = Mathf.Max(MaxKitUploadMs, (float)((Stopwatch.GetTimestamp() - before) * 1000.0 / Stopwatch.Frequency));
                    KitsBuilt++;
                    uploads++;
                }
                if (uploads > 0) MaxKitFrameMs = Mathf.Max(MaxKitFrameMs, (float)((Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency));
            }
        }

        void DropKit(ChunkView v, int c)
        {
            Destroy(v.KitRenderers[c].gameObject);
            Destroy(v.KitMeshes[c]);
            v.KitMeshes[c] = null;
            v.KitRenderers[c] = null;
            KitsDropped++;
        }

        void OnDestroy()
        {
            if (renderWarmup != null) EndRenderWarmup();
            foreach (var l in inFlight) l.Cts.Cancel();
            foreach (var j in applyQueue) DestroyView(j.View);
            foreach (var v in resident.Values) DestroyView(v);
            colliders.Abandon();
            applyQueue.Clear();
            resident.Clear();
            Shader.SetGlobalVector(NearFadeId, Vector4.zero);
        }
    }
}
