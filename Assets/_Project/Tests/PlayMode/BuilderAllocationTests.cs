// C-07: what the streaming worker allocates for each of the 12 slice chunks, stage by stage, read
// from Unity's "GC Allocated In Frame" counter (the one the bench reads). Each stage runs for all 12
// chunks inside one frame; idle frames before it give the noise floor of the editor and test runner.
// Unity's Mono reads 0 from GC.GetAllocatedBytesForCurrentThread, and heap-size differences miss
// small objects, so neither can be used here.
//
// BuildScratch keeps each builder's working lists from chunk to chunk, and the road, building and
// line mesh arrays come from MeshArrays' pools and go back after use, as in the streamer. So ground
// and overlays may allocate only a little more than the exact arrays they hand on, and buildings and
// outlines only a small part of theirs. Roads keep their per-road point arrays. Before C-07, for
// these 12 chunks: ground 676 KB, roads 5,077 KB, buildings 9,360 KB, overlays 752 KB, outlines
// 1,980 KB, against 3,882 KB of mesh arrays. The limits sit above the C-07 figures by a margin.
// D-04 put the markings in the road mesh, which made the road arrays 5 times larger (388 to
// 2,040 KB) for 541 KB more allocated (the per-road marking arrays): the roads limit fell from 4x
// to 1x, so that outlines not reused (+1.4 MB at C-07) still fail it. M2 builds the street furniture and
// structures inside the road build, into arrays of their own from the same pools, which the streamer hands
// back after its last street step: the stage releases them too, and counts them as handed on. That made
// the arrays handed on 2.4 times larger (12,725 to 30,758 KB) for 2,691 KB allocated, so the roads limit
// fell from 1x to 0.15x (4.6 MB): leaving the street arrays unreturned allocates 21.5 MB and fails it.
using System;
using System.Collections;
using System.IO;
using Dad.Core;
using Dad.World;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.TestTools;

namespace Dad.Tests
{
    public class BuilderAllocationTests
    {
        [UnityTest]
        public IEnumerator SliceBuildAllocations()
        {
            OldWorld.Use();
            string dir = Path.Combine(GameRoot.WorldDir, "chunks");
            GameRoot.WorldDirForTests = null;
            Assume.That(File.Exists(Path.Combine(dir, "c_7_-30.dadc")), "fixture missing");
            var files = new string[12];
            for (int i = 0, ix = 6; ix <= 9; ix++)
                for (int iy = -30; iy <= -28; iy++) files[i++] = Path.Combine(dir, $"c_{ix}_{iy}.dadc");
            var bytes = new byte[12][];
            var chunks = new DadcChunk[12];
            var stages = new (string name, Action<int> run)[]
            {
                ("idle", i => { }),
                ("read", i => bytes[i] = File.ReadAllBytes(files[i])),
                ("decode", i => chunks[i] = Dadc.Load(bytes[i])),
                ("ground", i => ChunkSurfaces.Build(chunks[i])),
                // As the streamer builds them: pooled, handed back once uploaded.
                ("roads", i => { var r = ChunkRoads.Build(chunks[i], pooled: true); r.Street.Release(); r.Release(); }),
                ("buildings", i => ChunkBuildings.Build(chunks[i], pooled: true).Release()),
                ("overlays", i => ChunkDebug.Build(chunks[i])),
                ("outlines", i => ChunkOutlines.Build(chunks[i], pooled: true).Release()),
            };
            using var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            Assume.That(recorder.Valid, "no GC Allocated In Frame counter in this editor");
            var bytesBy = new long[stages.Length];
            // Twice: the first pass also grows the per-thread lists and pools to the slice's size.
            for (int pass = 0; pass < 2; pass++)
                for (int s = 0; s < stages.Length; s++)
                {
                    yield return null;
                    for (int i = 0; i < 12; i++) stages[s].run(i);
                    yield return null;
                    bytesBy[s] = recorder.LastValue;
                }
            // What each builder hands on: the arrays its meshes are made from.
            var output = new long[stages.Length];
            foreach (var c in chunks)
            {
                var g = ChunkSurfaces.Build(c);
                var r = ChunkRoads.Build(c);
                var b = ChunkBuildings.Build(c);
                var d = ChunkDebug.Build(c);
                var o = ChunkOutlines.Build(c);
                output[3] += 4L * (g.Positions.Length + g.Colors.Length + g.Indices.Length);
                output[4] += 4L * (4 * r.VertexCount + r.IndexCount + 4 * r.Street.VertexCount + r.Street.IndexCount);
                output[5] += 4L * (4 * b.VertexCount + b.IndexCount);
                output[6] += 4L * (d.GridPositions.Length + d.GridIndices.Length + d.GraphPositions.Length + d.GraphIndices.Length);
                output[7] += 4L * (3 * o.VertexCount + o.IndexCount);
            }
            long builders = 0, handed = 0;
            var line = "Slice build allocations, 12 chunks, second pass (KB allocated / KB of mesh arrays):";
            for (int s = 0; s < stages.Length; s++)
            {
                long got = bytesBy[s] - (s > 0 ? bytesBy[0] : 0);
                line += $" {stages[s].name} {got / 1024}" + (s >= 3 ? $" / {output[s] / 1024};" : ";");
                if (s >= 3) { builders += got; handed += output[s]; }
            }
            Debug.Log(line + $" builders {builders / 1024} / {handed / 1024}");
            Assert.Greater(handed, 0);
            // Limits as a multiple of the mesh arrays handed on, plus 64 KB of counter noise.
            var limit = new[] { 0, 0, 0, 1.5, 0.15, 0.2, 1.2, 0.2 };
            for (int s = 3; s < stages.Length; s++)
            {
                long got = bytesBy[s] - bytesBy[0];
                Assert.LessOrEqual(got, (long)(limit[s] * output[s]) + 64 * 1024,
                    $"{stages[s].name} allocated {got / 1024} KB for {output[s] / 1024} KB of mesh arrays (limit {limit[s]}x + 64 KB)");
            }
        }
    }
}
