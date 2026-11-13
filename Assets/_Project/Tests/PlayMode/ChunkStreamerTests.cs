// C-01 acceptance: walk across the Farmgate slice; at every stop the resident set matches the
// rings, every chunk sits at its corner through the floating origin, no frame spends more than
// 4 ms applying, and loads cancelled in flight are never applied.
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Dad.Core;
using Dad.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Dad.Tests
{
    public class ChunkStreamerTests
    {
        Scene scene;
        Transform focus;
        FloatingOrigin origin;
        ChunkStreamer streamer;

        static bool InSlice(ChunkCell c) => c.X >= 6 && c.X <= 9 && c.Z >= 27 && c.Z <= 29;

        IEnumerator SetUp(int load, int unloadRadius, double east, double north, int delayMs = 0, bool readable = false)
        {
            OldWorld.Use();
            Assume.That(File.Exists(Path.Combine(GameRoot.WorldDir, "world.json")), "world data missing");
            scene = SceneManager.CreateScene("Streaming" + System.Guid.NewGuid().ToString("N"));
            SceneManager.SetActiveScene(scene);
            focus = new GameObject("Focus").transform;
            origin = new GameObject("FloatingOrigin").AddComponent<FloatingOrigin>();
            origin.Target = focus;
            origin.Recenter(east, north);
            MoveTo(east, north);
            var go = new GameObject("ChunkStreamer");
            go.SetActive(false);
            streamer = go.AddComponent<ChunkStreamer>();
            streamer.Focus = focus;
            streamer.Origin = origin;
            OldWorld.Limit(streamer);
            streamer.LoadRadius = load;
            streamer.UnloadRadius = unloadRadius;
            streamer.TestLoadDelayMs = delayMs;
            streamer.KeepMeshesReadable = readable;
            go.SetActive(true);
            float until = Time.realtimeSinceStartup + 10f;
            while (!streamer.IndexReady && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(streamer.IndexReady, "world index read: " + streamer.IndexError);
        }

        void MoveTo(double east, double north)
        {
            origin.Offset.ToScene(east, north, out float x, out float z);
            focus.position = new Vector3(x, 1.7f, z);
        }

        IEnumerator Settle()
        {
            float until = Time.realtimeSinceStartup + 10f;
            int quiet = 0;
            while (quiet < 3 && Time.realtimeSinceStartup < until)
            {
                yield return null;
                quiet = streamer.Idle ? quiet + 1 : 0;
            }
            Assert.IsTrue(streamer.Idle, "streaming settled within 10 s");
        }

        void CheckRings(ChunkCell at, string where)
        {
            int load = streamer.LoadRadius, unloadRadius = streamer.UnloadRadius;
            foreach (var c in streamer.Resident.Keys)
            {
                Assert.IsTrue(InSlice(c), $"{where}: {c.Id} outside the slice");
                Assert.Less(StreamPlanner.Chebyshev(at, c), unloadRadius, $"{where}: {c.Id} past the unload radius");
            }
            for (int x = at.X - load; x <= at.X + load; x++)
            for (int z = at.Z - load; z <= at.Z + load; z++)
            {
                var c = new ChunkCell(x, z);
                if (InSlice(c)) Assert.IsTrue(streamer.Resident.ContainsKey(c), $"{where}: {c.Id} in the load ring but not resident");
            }
            foreach (var v in streamer.Resident.Values)
            {
                origin.Offset.ToWorld(v.Root.transform.position.x, v.Root.transform.position.z, out double e, out double n);
                Assert.AreEqual(v.Cell.CornerEast, e, 0.001, $"{v.Cell.Id} corner east");
                Assert.AreEqual(v.Cell.CornerNorth, n, 0.001, $"{v.Cell.Id} corner north");
            }
        }

        [SetUp]
        public void MarkScenes() => TestScenes.Mark();

        // Runs after every test, failed ones too: the streamer stops its workers first, then the
        // scene goes (TestScenes).
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (streamer != null) Object.Destroy(streamer.gameObject);
            if (origin != null) Object.Destroy(origin.gameObject);
            streamer = null;
            origin = null;
            yield return null;
            yield return TestScenes.Clean();
        }

        [UnityTest]
        public IEnumerator WalkAcrossTheSliceKeepsRingsAndBudget()
        {
            // West of the slice (cell X 4) to east of it (X 11), along north 14250 (Z 28), 250 m steps.
            yield return SetUp(1, 2, 2000, 14250);
            Assert.Greater(streamer.PreparedMethods, 500, "the warm-up compiled the streaming code ahead");
            var seen = new HashSet<ChunkCell>();
            for (double east = 2000; east <= 5750; east += 250)
            {
                MoveTo(east, 14250);
                yield return Settle();
                CheckRings(ChunkCell.Of(east, 14250), $"east {east}");
                foreach (var c in streamer.Resident.Keys) seen.Add(c);
            }
            Assert.AreEqual(12, seen.Count, "every slice chunk was streamed in at some point");
            Assert.Greater(streamer.Unloaded, 0, "and chunks left again");

            // Teleport far away: everything goes. Back to the middle: exactly the ring comes back.
            MoveTo(20000, 30000);
            yield return Settle();
            Assert.AreEqual(0, streamer.Resident.Count, "nothing resident far from the slice");
            MoveTo(3750, 14250);
            yield return Settle();
            CheckRings(new ChunkCell(7, 28), "after teleports");
            Assert.AreEqual(9, streamer.Resident.Count, "3 x 3 ring around X 7, Z 28, all inside the slice");
            Assert.Greater(origin.Shifts, 1, "the teleports shifted the origin");

            Assert.AreEqual(0, streamer.Failures);
            Assert.AreEqual(0, streamer.AppliedOutsideRings);
            Debug.Log($"Streaming walk: applied {streamer.Applied}, unloaded {streamer.Unloaded}, cancelled {streamer.Cancelled}, discarded {streamer.Discarded}, max apply frame {streamer.MaxApplyFrameMs:F2} ms, max step {streamer.MaxApplyStepMs:F2} ms; " +
                      $"{streamer.StepsWithGc} steps with a garbage collection (slowest {streamer.MaxGcStepMs:F2} ms)");
            Assert.LessOrEqual(streamer.MaxApplyFrameMs, 4f, "no frame spends more than 4 ms applying chunks");
            Debug.Log($"Near rooftop kits on the walk: {streamer.KitsBuilt} built, {streamer.KitsDropped} dropped, slowest upload {streamer.MaxKitUploadMs:F2} ms, slowest frame {streamer.MaxKitFrameMs:F2} ms; " +
                      $"drawn near without their kit in {streamer.KitLateFrames} frames (at most {streamer.MaxKitLateCells} cells; each 250 m step is a jump)");
            Assert.AreEqual(0, streamer.KitFailures);
            Assert.LessOrEqual(streamer.MaxKitFrameMs, 4f, "no frame spends more than 4 ms uploading rooftop kits");
        }

        [UnityTest]
        public IEnumerator FinishedChunksWaitForLoadsStillBuilding()
        {
            // Start far from the slice, where nothing loads. Then one load at a time, each held 100 ms
            // on its worker, so every finished chunk has the next load building behind it.
            yield return SetUp(1, 2, 20000, 30000, delayMs: 100);
            streamer.MaxInFlight = 1;
            streamer.MaxApplyHoldFrames = int.MaxValue;
            MoveTo(3750, 14250);
            int steps = streamer.ApplySteps, building = 0;
            float until = Time.realtimeSinceStartup + 10f;
            while (!(streamer.Idle && streamer.Resident.Count == 9) && Time.realtimeSinceStartup < until)
            {
                yield return null;
                if (streamer.ApplySteps != steps && streamer.InFlight > 0) building++;
                steps = streamer.ApplySteps;
            }
            Assert.AreEqual(9, streamer.Resident.Count, "the 3 x 3 ring around X 7, Z 28 came in");
            Assert.AreEqual(0, building, "frames that ran apply steps while a load was building");
            Assert.Greater(streamer.HeldFrames, 0, "finished chunks waited");

            // With a 2-frame limit, waiting chunks are applied while later loads still build.
            MoveTo(20000, 30000);
            yield return Settle();
            streamer.MaxApplyHoldFrames = 2;
            MoveTo(3750, 14250);
            until = Time.realtimeSinceStartup + 10f;
            while (!(streamer.Idle && streamer.Resident.Count == 9) && Time.realtimeSinceStartup < until)
            {
                yield return null;
                if (streamer.ApplySteps != steps && streamer.InFlight > 0) building++;
                steps = streamer.ApplySteps;
            }
            Assert.AreEqual(9, streamer.Resident.Count);
            Assert.Greater(building, 0, "a wait that ran out let the queue apply");
            Debug.Log($"Apply hold: {streamer.HeldFrames} frames held in all, longest {streamer.MaxHeldFrames}; with the 2-frame limit, {building} frames applied while a load was building");
        }

        [UnityTest]
        public IEnumerator FarmgateChunkMatchesItsData()
        {
            // This test reads the meshes back, so they keep their CPU copies (DrawnMeshesDropTheirCopy checks that they do not otherwise).
            yield return SetUp(0, 1, 3750, 14750, readable: true);
            yield return Settle();
            var cell = ChunkCell.FromIxIy(7, -30);
            Assert.IsTrue(streamer.Resident.TryGetValue(cell, out var view), "c_7_-30 resident");
            var chunk = Dadc.Load(File.ReadAllBytes(Path.Combine(GameRoot.WorldDir, "chunks", "c_7_-30.dadc")));
            NeighbourRails.Attach(chunk, Path.Combine(GameRoot.WorldDir, "chunks")); // as the streamer does (K-37 P-2, K-50)
            var expected = ChunkOutlines.Build(chunk);
            Assert.AreEqual((725, 52), (view.Buildings, view.Roads));
            Assert.AreEqual(0, streamer.StillReadable, "with KeepMeshesReadable no mesh is listed for release");
            Assert.AreEqual(expected.VertexCount, view.Lines.vertexCount);
            Assert.AreEqual(expected.BuildingIndexCount, (int)view.Lines.GetIndexCount(0));
            Assert.AreEqual(expected.RoadIndexCount, (int)view.Lines.GetIndexCount(1));
            // Road 13848342, first point: 464.63 m east and 500 m north of the corner.
            var road = System.Array.Find(chunk.Roads, r => r.Id == 13848342);
            Assert.IsNotNull(road);
            var world = view.Root.transform.TransformPoint(new Vector3(road.Pts[0] / 100f, 0f, road.Pts[1] / 100f));
            origin.Offset.ToWorld(world.x, world.z, out double e, out double n);
            Assert.AreEqual(3500 + 464.63, e, 0.001);
            Assert.AreEqual(14500 + 500.0, n, 0.001);

            // C-02 ground: the uploaded mesh is the builder's, drawn with the lit world shader (D-01),
            // and Unity's own normals for it point up (the builder's winding is Unity's front face).
            // It receives shadows and casts none.
            var surfaces = ChunkSurfaces.Build(chunk);
            Assert.IsNotNull(view.Surfaces);
            Assert.AreEqual(surfaces.VertexCount, view.Surfaces.vertexCount);
            Assert.AreEqual(surfaces.Indices.Length, (int)view.Surfaces.GetIndexCount(0));
            CollectionAssert.AreEqual(surfaces.Features, view.SurfaceFeatures);
            Assert.AreEqual(0, view.SurfaceFailed);
            var renderer = view.Root.transform.Find("Ground").GetComponent<MeshRenderer>();
            Assert.AreEqual("Dad/World", renderer.sharedMaterial.shader.name);
            Assert.AreEqual((ShadowCastingMode.Off, true), (renderer.shadowCastingMode, renderer.receiveShadows));
            var copy = Object.Instantiate(view.Surfaces);
            copy.RecalculateNormals();
            int down = 0;
            foreach (var nrm in copy.normals) if (nrm.y < 0.99f) down++;
            Object.Destroy(copy);
            Assert.AreEqual(0, down, "every ground normal points up");
            var colours = view.Surfaces.colors32;
            Assert.AreEqual(new Color32(0x6a, 0x61, 0x53, (byte)WorldMaterial.Soil), colours[0], "base plate in the ground colour, soil");

            // C-03 roads: the uploaded mesh is the builder's, in the same shader, inside its bounds,
            // receiving shadows and casting none.
            var roads = ChunkRoads.Build(chunk);
            Assert.IsNotNull(view.RoadMesh);
            Assert.AreEqual(roads.VertexCount, view.RoadMesh.vertexCount);
            Assert.AreEqual(roads.Indices.Length, (int)view.RoadMesh.GetIndexCount(0));
            Assert.AreEqual((roads.Roads, roads.BorderCuts), (view.RoadsDrawn, view.BorderCuts));
            var roadRenderer = view.Root.transform.Find("Roads").GetComponent<MeshRenderer>();
            Assert.AreEqual("Dad/World", roadRenderer.sharedMaterial.shader.name);
            Assert.AreEqual((ShadowCastingMode.Off, true), (roadRenderer.shadowCastingMode, roadRenderer.receiveShadows));
            var box = view.RoadMesh.bounds;
            foreach (var v in view.RoadMesh.vertices) Assert.IsTrue(box.Contains(v), $"road vertex {v} inside the mesh bounds");

            // C-04 buildings: the uploaded mesh is the builder's. Unity's own normals point up on
            // every roof corner and down only under the raised station.
            var solids = ChunkBuildings.Build(chunk);
            Assert.IsNotNull(view.BuildingMesh);
            Assert.AreEqual(solids.VertexCount, view.BuildingMesh.vertexCount);
            Assert.AreEqual(solids.Indices.Length, (int)view.BuildingMesh.GetIndexCount(0));
            Assert.AreEqual((solids.Drawn, 1), (view.BuildingsDrawn, view.BuildingsRaised), "725 drawn, Farmgate station raised");
            var buildingRenderer = view.Root.transform.Find("Buildings").GetComponent<MeshRenderer>();
            Assert.AreEqual("Dad/World", buildingRenderer.sharedMaterial.shader.name);
            Assert.AreEqual((ShadowCastingMode.On, true), (buildingRenderer.shadowCastingMode, buildingRenderer.receiveShadows), "buildings cast and receive");
            int roofCorners = 0, undersideCorners = 0;
            var flat = new System.Collections.Generic.List<double>();
            var holes = new System.Collections.Generic.List<int>();
            // Facade buildings (M2) have sills, floors and awnings facing up and down too: StreetTests.FacadesKeepFootprintAndTop checks those.
            var facade = new bool[view.BuildingMesh.vertexCount];
            for (int i = 0; i < chunk.Buildings.Length; i++)
            {
                if (solids.VertexCounts[i] == 0) continue;
                if (ChunkBuildings.Facaded(chunk, chunk.Buildings[i], solids.Placements[i]))
                {
                    for (int v = solids.FirstVertex[i]; v < solids.FirstVertex[i] + solids.VertexCounts[i]; v++) facade[v] = true;
                    continue;
                }
                ChunkSurfaces.Flatten(chunk.Buildings[i].Outer, chunk.Buildings[i].Holes, flat, holes);
                roofCorners += flat.Count / 2;
                if (solids.Placements[i].Base > 0) undersideCorners += flat.Count / 2;
            }
            var solidCopy = Object.Instantiate(view.BuildingMesh);
            solidCopy.RecalculateNormals();
            int upward = 0, downward = 0;
            var normals = solidCopy.normals;
            for (int v = 0; v < normals.Length; v++)
            {
                var nrm = normals[v];
                if (facade[v]) continue;
                if (nrm.y > 0.99f) upward++;
                else if (nrm.y < -0.99f) downward++;
            }
            Object.Destroy(solidCopy);
            // Since D-06b each parapet piece's coping adds four corners facing up.
            Assert.AreEqual((roofCorners + 4 * solids.ParapetPieces, undersideCorners), (upward, downward), "roof and coping corners face up, the underside down");
            var solidBox = view.BuildingMesh.bounds;
            foreach (var v in view.BuildingMesh.vertices) Assert.IsTrue(solidBox.Contains(v), $"building vertex {v} inside the mesh bounds");

            // M2 street furniture and structures: the structure mesh (viaduct, footbridges, dividers) and a near and a far
            // mesh for each 125 m cell are the builder's ranges, in the same shader, casting shadows; a cell shows one tier at most.
            var street = roads.Street;
            Assert.IsNotNull(view.StructureMesh, "Farmgate has the viaduct and its footbridge");
            Assert.AreEqual((street.Vertices[0], street.IndexCounts[0]), (view.StructureMesh.vertexCount, (int)view.StructureMesh.GetIndexCount(0)));
            var structureRenderer = view.Root.transform.Find("Structures").GetComponent<MeshRenderer>();
            Assert.AreEqual("Dad/World", structureRenderer.sharedMaterial.shader.name);
            Assert.AreEqual(ShadowCastingMode.On, structureRenderer.shadowCastingMode);
            int cellMeshes = 0;
            for (int c = 0; c < ChunkStreet.Cells * ChunkStreet.Cells; c++)
            {
                for (int lod = 0; lod < 2; lod++)
                {
                    int range = ChunkStreet.Range(c, lod);
                    var mesh = view.CellMeshes[2 * c + lod];
                    if (street.IndexCounts[range] == 0) { Assert.IsNull(mesh, $"cell {c} lod {lod} is empty"); continue; }
                    cellMeshes++;
                    Assert.AreEqual((street.Vertices[range], street.IndexCounts[range]), (mesh.vertexCount, (int)mesh.GetIndexCount(0)), $"cell {c} lod {lod}");
                    foreach (var v in mesh.vertices) Assert.IsTrue(mesh.bounds.Contains(v), $"cell {c} vertex {v} inside its bounds");
                }
                var near = view.CellRenderers[2 * c];
                var far = view.CellRenderers[2 * c + 1];
                Assert.IsFalse(near != null && near.enabled && far != null && far.enabled, $"cell {c} shows one tier at most");
            }
            Assert.Greater(cellMeshes, ChunkStreet.Cells * ChunkStreet.Cells, "street cells have a near and a far mesh");
        }

        [UnityTest]
        public IEnumerator DrawnMeshesDropTheirCopy()
        {
            // K-45: a mesh drops its CPU copy once a camera has drawn it; one never drawn keeps it (dropping it would upload
            // it to the GPU for nothing); the meshes keep their counts and bounds; the list empties as chunks unload.
            yield return SetUp(0, 1, 3750, 14750);
            var eye = new GameObject("Eye", typeof(Camera)).GetComponent<Camera>();
            eye.farClipPlane = 2000f;
            eye.transform.SetPositionAndRotation(focus.position + Vector3.up * 40f, Quaternion.Euler(30f, 20f, 0f));
            yield return Settle();
            Assert.IsTrue(streamer.Resident.TryGetValue(ChunkCell.FromIxIy(7, -30), out var view), "c_7_-30 resident");
            // Batch-mode test runs draw no camera by themselves: the test draws its own each frame.
            for (int i = 0; i < 60; i++) { eye.Render(); yield return null; }
            Assert.Greater(streamer.Released, 0, "drawn meshes dropped their copies");
            Assert.IsFalse(view.BuildingMesh.isReadable, "the buildings in view were drawn: no copy");
            Assert.IsFalse(view.Surfaces.isReadable, "the ground in view was drawn: no copy");
            Assert.Greater(view.BuildingMesh.vertexCount, 0, "counts stay");
            Assert.Greater(view.BuildingMesh.bounds.size.sqrMagnitude, 0f, "bounds stay");
            Assert.IsTrue(view.GridMesh.isReadable && view.GraphMesh.isReadable && view.Lines.isReadable, "debug views never shown keep their copies");
            int hidden = 0, shownDropped = 0;
            for (int k = 0; k < view.CellMeshes.Length; k++)
            {
                var mesh = view.CellMeshes[k];
                if (mesh == null) continue;
                var r = view.CellRenderers[k];
                if (!r.enabled) { hidden++; Assert.IsTrue(mesh.isReadable, $"cell mesh {k} not shown keeps its copy"); }
                else if (!mesh.isReadable) shownDropped++;
            }
            Assert.Greater(hidden, 0, "some cell tiers are hidden");
            Assert.Greater(shownDropped, 0, "shown cell tiers dropped their copies");
            Assert.LessOrEqual(streamer.MaxReleaseFrameMs, 4f, "dropping copies stays inside the frame rule");

            // A debug view switched on is drawn, then drops its copy too.
            streamer.SetOverlays(true, false, false);
            for (int i = 0; i < 60 && view.GridMesh.isReadable; i++) { eye.Render(); yield return null; }
            Assert.IsFalse(view.GridMesh.isReadable, "the grid, once shown, dropped its copy");

            // Far away, everything unloads and the list empties.
            MoveTo(20000, 30000);
            yield return Settle();
            for (int i = 0; i < 10; i++) yield return null;
            Assert.AreEqual(0, streamer.Resident.Count);
            Assert.AreEqual(0, streamer.StillReadable, "unloaded meshes leave the list");
            Object.Destroy(eye.gameObject);
            Debug.Log($"Mesh release: {streamer.Released} copies dropped, slowest {streamer.MaxReleaseMs:F3} ms, slowest frame {streamer.MaxReleaseFrameMs:F3} ms");
        }

        /// <summary>Every resident cell with a rooftop kit holds its near kit within KitBuildDistance and none beyond
        /// KitDropDistance; a kit held is drawn exactly when its cell is drawn near. Returns the cells with a kit.</summary>
        int CheckKits(string where)
        {
            int withKit = 0;
            foreach (var v in streamer.Resident.Values)
                for (int c = 0; c < ChunkStreet.Cells * ChunkStreet.Cells; c++)
                {
                    if (!v.Street.HasNearKit(c) && !v.Facades.HasNearFacade(c)) { Assert.IsNull(v.KitMeshes[c], $"{where}: {v.Cell.Id} cell {c} has no kit to build"); continue; }
                    withKit++;
                    double d = streamer.CellDistance(v, c);
                    if (d < streamer.KitBuildDistance) Assert.IsNotNull(v.KitMeshes[c], $"{where}: {v.Cell.Id} cell {c} at {d:F0} m has its kit");
                    if (d > streamer.KitDropDistance) Assert.IsNull(v.KitMeshes[c], $"{where}: {v.Cell.Id} cell {c} at {d:F0} m dropped its kit");
                    if (v.KitMeshes[c] != null)
                        Assert.AreEqual(v.CellTier[c] == StreetLod.Near, v.KitRenderers[c].enabled, $"{where}: {v.Cell.Id} cell {c} kit drawn with the near tier only");
                }
            return withKit;
        }

        [UnityTest]
        public IEnumerator NearRooftopKitsComeAndGoWithDistance()
        {
            // K-45 approach 4: a cell's near rooftop kit is built once the cell comes within KitBuildDistance, drawn with the
            // cell's near tier, dropped beyond KitDropDistance, and is the builder's; far fewer cells hold one than have one.
            // The warm-up makes the builds' scratch sets ahead, one per build at once, and the builds never need more.
            // Since D-06c the kit carries the cell's near facades after it, a second submesh in the near material.
            ChunkStreet.ResetNearKitScratch();
            yield return SetUp(1, 2, 3750, 14750, readable: true);
            Assert.AreEqual(streamer.MaxKitJobs, ChunkStreet.NearKitScratchSets, "the warm-up made the kit scratch sets ahead");
            yield return Settle();
            Assert.AreEqual(streamer.MaxKitJobs, ChunkStreet.NearKitScratchSets, "no build needed a set more");
            Assert.AreEqual(0, ChunkStreet.NearKitScratchGrowths, "no build grew a set");
            int withKit = CheckKits("at Farmgate");
            Debug.Log($"Near rooftop kits at Farmgate: {streamer.KitsHeld} held of {withKit} cells with a kit; {streamer.NearCells} cells near");
            Assert.Greater(streamer.KitsHeld, 0);
            Assert.Less(streamer.KitsHeld, withKit / 2, "most cells hold no near kit");
            Assert.IsTrue(streamer.Resident.TryGetValue(ChunkCell.FromIxIy(7, -30), out var view), "c_7_-30 resident");
            int compared = 0, split = 0;
            var world = Resources.Load<Material>("Materials/World");
            var near = Resources.Load<Material>("Materials/WorldNear");
            for (int c = 0; c < ChunkStreet.Cells * ChunkStreet.Cells; c++)
            {
                var mesh = view.KitMeshes[c];
                if (mesh == null) continue;
                var kit = view.Street.BuildNearKit(c, view.Facades);
                int indices = 0;
                for (int sub = 0; sub < mesh.subMeshCount; sub++) indices += (int)mesh.GetIndexCount(sub);
                Assert.AreEqual((kit.VertexCount, kit.IndexCount), (mesh.vertexCount, indices), $"cell {c} kit is the builder's");
                var materials = view.KitRenderers[c].sharedMaterials;
                if (kit.RoofIndexCount > 0 && kit.IndexCount > kit.RoofIndexCount)
                {
                    split++;
                    Assert.AreEqual((2, (uint)kit.RoofIndexCount), (mesh.subMeshCount, mesh.GetIndexCount(0)), $"cell {c}: the rooftop kit, then the near facades");
                    CollectionAssert.AreEqual(new[] { world, near }, materials, $"cell {c} materials");
                }
                else CollectionAssert.AreEqual(new[] { kit.RoofIndexCount > 0 ? world : near }, materials, $"cell {c} material");
                var vertices = mesh.vertices;
                for (int v = 0; v < kit.VertexCount; v++)
                {
                    Assert.AreEqual(new Vector3(kit.Positions[3 * v], kit.Positions[3 * v + 1], kit.Positions[3 * v + 2]), vertices[v], $"cell {c} vertex {v}");
                    Assert.IsTrue(mesh.bounds.Contains(vertices[v]), $"cell {c} vertex {v} inside its bounds");
                }
                var r = view.KitRenderers[c];
                Assert.AreEqual(("Dad/World", ShadowCastingMode.On, true), (r.sharedMaterial.shader.name, r.shadowCastingMode, r.receiveShadows));
                compared++;
            }
            Assert.Greater(compared, 0, "c_7_-30 holds near kits");
            Assert.Greater(split, 0, "and near facades with them");

            // 500 m east: kits come and go.
            int built = streamer.KitsBuilt, dropped = streamer.KitsDropped;
            MoveTo(4250, 14750);
            yield return Settle();
            CheckKits("500 m east");
            Assert.Greater(streamer.KitsBuilt, built, "kits built for the cells come near");
            Assert.Greater(streamer.KitsDropped, dropped, "kits dropped for the cells left behind");
            Assert.AreEqual(0, streamer.KitFailures);

            // Far away, everything unloads; nothing is held or waiting.
            MoveTo(20000, 30000);
            yield return Settle();
            Assert.AreEqual((0, 0), (streamer.KitsHeld, streamer.KitJobs));
        }

        [UnityTest]
        public IEnumerator KitsFinishingForUnloadedChunksAreHandedBack()
        {
            // A near kit still building when its chunk unloads is handed back when it finishes, never uploaded, and so are the
            // chunk's kit items once nothing builds from them; a finished chunk waits for kits building as for loads.
            // One chunk, which goes as soon as the focus leaves it, the focus by its east edge; its kits build slowly.
            yield return SetUp(0, 1, 3990, 14750);
            streamer.TestKitDelayMs = 1000;
            float until = Time.realtimeSinceStartup + 10f;
            while (streamer.KitJobs == 0 && Time.realtimeSinceStartup < until) yield return null;
            Assert.Greater(streamer.KitJobs, 0, "c_7_-30's kits building");
            var streets = new List<ChunkStreet>();
            var facades = new List<ChunkBuildings>();
            foreach (var v in streamer.Resident.Values) { streets.Add(v.Street); facades.Add(v.Facades); }
            int held = streamer.HeldFrames;
            // Into the next chunk east: c_7_-30 unloads while its kits build, its east cells (near, so built first) 300 m off,
            // inside KitDropDistance: only the chunk being gone stops their upload.
            MoveTo(4300, 14750);
            yield return Settle();
            for (int i = 0; i < 5; i++) yield return null;
            Assert.AreEqual(1, streamer.Resident.Count);
            Assert.Greater(streamer.KitsDiscarded, 0, "the kits finished after their chunk went were handed back");
            Assert.Greater(streamer.HeldFrames, held, "the next chunk waited for the kits building");
            Assert.AreEqual(0, streamer.KitFailures, "nothing was handed back while a kit was building from it");
            foreach (var st in streets) Assert.IsNull(st.RoofKit, "the unloaded chunk handed its kit items back");
            foreach (var b in facades) Assert.IsNull(b.NearWalls, "and its near facade walls");
            streamer.TestKitDelayMs = 0;
            MoveTo(20000, 30000);
            yield return Settle();
            Assert.AreEqual((0, 0, 0), (streamer.KitsHeld, streamer.KitJobs, streamer.StillReadable));
        }

        [UnityTest]
        public IEnumerator CellsDrawnNearWithoutTheirKitAreCounted()
        {
            // The pop-in measure (K-45 approach 4): each frame in which a cell is drawn near without its rooftop kit counts.
            yield return SetUp(0, 1, 3750, 14750);
            yield return Settle();
            Assert.Greater(streamer.KitsHeld, 0);
            int late = streamer.KitLateFrames;
            // Nothing may be built or kept: the near cells lose their kits, and each frame they are drawn so counts.
            streamer.KitBuildDistance = 0;
            streamer.KitDropDistance = -1;
            for (int i = 0; i < 5; i++) yield return null;
            Assert.AreEqual(0, streamer.KitsHeld);
            Assert.That(streamer.KitLateFrames, Is.InRange(late + 4, late + 5), "each frame with a near cell waiting counts");
            Assert.Greater(streamer.MaxKitLateCells, 0);
            // Allowed again, the kits come back and the count stops.
            streamer.KitBuildDistance = 250;
            streamer.KitDropDistance = 350;
            yield return Settle();
            late = streamer.KitLateFrames;
            for (int i = 0; i < 5; i++) yield return null;
            Assert.AreEqual(late, streamer.KitLateFrames, "no cell drawn near is waiting");
            Assert.Greater(streamer.KitsHeld, 0);
        }

        [UnityTest]
        public IEnumerator NearFacadesComeInWithoutPopping()
        {
            // D-06c: the near facades dissolve in from ChunkBuildings.FadeEnd to FadeStart around the focus, so a cell that has
            // just turned near (149 m off) shows none of them: the frame is the same, pixel for pixel, with its near facades
            // as the streamer fades them and with them drawn but all dithered out, shadows and depth included. At 60 m they
            // are drawn. The globals the shader reads are the streamer's. (Against a frame without the near submesh at all,
            // a few pixels may differ by a level or so from the draw being there; the log counts them.)
            yield return SetUp(1, 2, 3750, 14750);
            yield return Settle();
            var fade = Shader.GetGlobalVector("_DadNearFade");
            Assert.AreEqual(new Vector2((float)ChunkBuildings.FadeStart, (float)ChunkBuildings.FadeEnd), new Vector2(fade.x, fade.y));
            // The resident cell whose near kit has the most near facades.
            ChunkStreamer.ChunkView view = null;
            int cell = -1;
            uint most = 0;
            foreach (var v in streamer.Resident.Values)
                for (int c = 0; c < ChunkStreet.Cells * ChunkStreet.Cells; c++)
                {
                    var m = v.KitMeshes[c];
                    if (m == null || v.KitRenderers[c].sharedMaterials.Length != 2 || m.GetIndexCount(1) <= most) continue;
                    most = m.GetIndexCount(1);
                    view = v;
                    cell = c;
                }
            Assert.IsNotNull(view, "a near kit with near facades");
            double x0 = view.Cell.CornerEast + cell % ChunkStreet.Cells * ChunkStreet.CellSize, z0 = view.Cell.CornerNorth + cell / ChunkStreet.Cells * ChunkStreet.CellSize;
            double cx = x0 + ChunkStreet.CellSize / 2, cz = z0 + ChunkStreet.CellSize / 2;
            var eye = new GameObject("Eye", typeof(Camera)).GetComponent<Camera>();
            eye.farClipPlane = 2000f;
            var rt = new RenderTexture(640, 360, 24);
            eye.targetTexture = rt;
            var renderer = view.KitRenderers[cell];
            var both = renderer.sharedMaterials;
            var roofOnly = new[] { both[0] };
            try
            {
                foreach (var (off, same) in new[] { (149.0, true), (60.0, false) })
                {
                    // The focus south of the cell; the eye 35 m over it, looking at the cell's middle over the roofs between.
                    MoveTo(cx, z0 - off);
                    for (int i = 0; i < 3; i++) yield return null;
                    Assert.AreEqual(off, streamer.CellDistance(view, cell), 0.01);
                    Assert.AreEqual(StreetLod.Near, view.CellTier[cell], $"{off} m: drawn near");
                    Assert.IsTrue(renderer != null && renderer.enabled, $"{off} m: the kit is drawn");
                    var at = focus.position + Vector3.up * 35f;
                    origin.Offset.ToScene(cx, cz, out float tx, out float tz);
                    eye.transform.SetPositionAndRotation(at, Quaternion.LookRotation(new Vector3(tx, 0f, tz) - at));
                    renderer.sharedMaterials = both;
                    var with = Shot(eye, rt);
                    renderer.sharedMaterials = roofOnly;
                    var without = Shot(eye, rt);
                    renderer.sharedMaterials = both;
                    // The near facades drawn with a fade that no pixel anywhere passes (none beyond 1 mm): what this frame
                    // still differs in from the one without them comes from their draw being there, not from a piece.
                    var fade0 = Shader.GetGlobalVector("_DadNearFade");
                    Shader.SetGlobalVector("_DadNearFade", new Vector4(0f, 0.001f, 0f, 0f));
                    var hidden = Shot(eye, rt);
                    Shader.SetGlobalVector("_DadNearFade", fade0);
                    int drawn = Differ(with, hidden, out string drawnAt), changed = Differ(with, without, out string changedAt), side = Differ(hidden, without, out _);
                    Debug.Log($"[D-06c] {view.Cell.Id} cell {cell}, {most / 3} near facade triangles, seen from {off} m: of {with.Length} pixels, {drawn} show a piece" +
                              $" (with them against them drawn but all dithered out){(same ? drawnAt : "")}; {changed} differ with and without them{(same ? changedAt : "")}," +
                              $" {side} of those from the draw alone");
                    if (same) Assert.AreEqual(0, drawn, "a cell just turned near shows none of its near facades");
                    else Assert.Greater(drawn, 500, "at 60 m they are drawn");
                }
            }
            finally
            {
                renderer.sharedMaterials = both;
                eye.targetTexture = null;
                Object.Destroy(eye.gameObject);
                rt.Release();
                Object.Destroy(rt);
            }
        }

        /// <summary>The pixels in which two shots differ, and where the first few are, with their largest channel difference.</summary>
        static int Differ(Color32[] a, Color32[] b, out string where)
        {
            int n = 0;
            var at = new List<string>();
            for (int i = 0; i < a.Length; i++)
            {
                int d = Mathf.Max(Mathf.Abs(a[i].r - b[i].r), Mathf.Max(Mathf.Abs(a[i].g - b[i].g), Mathf.Abs(a[i].b - b[i].b)));
                if (d == 0) continue;
                n++;
                if (at.Count < 6) at.Add($"({i % 640},{i / 640}) by {d}");
            }
            where = at.Count > 0 ? " [" + string.Join(", ", at) + "]" : "";
            return n;
        }

        static Color32[] Shot(Camera eye, RenderTexture rt)
        {
            eye.Render();
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            var was = RenderTexture.active;
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            RenderTexture.active = was;
            var px = tex.GetPixels32();
            Object.Destroy(tex);
            return px;
        }

        [UnityTest]
        public IEnumerator OverlaysFollowTheSwitchesOnLaterChunks()
        {
            yield return SetUp(1, 2, 3942, 14893);
            Assert.AreEqual((false, false, false), (streamer.ShowGrid, streamer.ShowGraph, streamer.ShowLines), "debug views off at start");
            streamer.SetOverlays(true, false, true); // before any chunk is applied
            yield return Settle();
            Assert.Greater(streamer.Resident.Count, 1);
            foreach (var v in streamer.Resident.Values)
                Assert.AreEqual((true, false, true), (v.Grid.activeSelf, v.Graph.activeSelf, v.LineObject.activeSelf), v.Cell.Id);

            Assert.IsTrue(streamer.Resident.TryGetValue(ChunkCell.FromIxIy(7, -30), out var farmgate));
            var chunk = Dadc.Load(File.ReadAllBytes(Path.Combine(GameRoot.WorldDir, "chunks", "c_7_-30.dadc")));
            var d = ChunkDebug.Build(chunk);
            Assert.AreEqual(d.GridIndices.Length, (int)farmgate.GridMesh.GetIndexCount(0));
            Assert.AreEqual((d.EdgeIndexCount, d.NodeIndexCount, d.SignalIndexCount),
                ((int)farmgate.GraphMesh.GetIndexCount(0), (int)farmgate.GraphMesh.GetIndexCount(1), (int)farmgate.GraphMesh.GetIndexCount(2)));
            Assert.AreEqual(MeshTopology.Lines, farmgate.GraphMesh.GetTopology(0));
            var grid = farmgate.Grid.GetComponent<MeshRenderer>();
            Assert.AreEqual("Dad/Debug Lines", grid.sharedMaterial.shader.name);
            Assert.AreEqual(3, farmgate.Graph.GetComponent<MeshRenderer>().sharedMaterials.Length);

            streamer.SetOverlays(false, true, false);
            foreach (var v in streamer.Resident.Values)
                Assert.AreEqual((false, true, false), (v.Grid.activeSelf, v.Graph.activeSelf, v.LineObject.activeSelf), v.Cell.Id);
        }

        [UnityTest]
        public IEnumerator LoadsCancelledInFlightAreNeverApplied()
        {
            yield return SetUp(1, 2, 3750, 14250, delayMs: 400);
            yield return null;
            yield return null;
            Assert.Greater(streamer.InFlight, 0, "loads in flight");
            int applied = streamer.Applied;
            MoveTo(20000, 30000);
            yield return Settle();
            Assert.AreEqual(applied, streamer.Applied, "nothing applied after leaving");
            Assert.Greater(streamer.Cancelled, 0, "loads were cancelled");
            Assert.AreEqual(0, streamer.Resident.Count);
            Assert.AreEqual(0, streamer.Failures, "a cancel is not a failure");
            LogAssert.NoUnexpectedReceived();
        }
    }
}
