// The chunks' collision meshes (E-04). A chunk's triangles (ChunkCollision) come from the worker with the rest of it; here
// they go into a mesh, PhysX cooks that on a worker (Physics.BakeMesh, which Unity allows off the main thread once the mesh
// exists) and the finished collider is attached with nothing left to cook, so the main thread only copies arrays.
// Off unless ChunkStreamer.BuildColliders is on, so a view without a vehicle pays nothing.
using System.Collections.Generic;
using System.Threading.Tasks;
using Dad.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace Dad.Core
{
    public sealed class ChunkColliders
    {
        // The cooking options must match the collider's, or assigning the mesh cooks it again on the main thread.
        const MeshColliderCookingOptions Cooking = MeshColliderCookingOptions.CookForFasterSimulation | MeshColliderCookingOptions.EnableMeshCleaning
            | MeshColliderCookingOptions.WeldColocatedVertices | MeshColliderCookingOptions.UseFastMidphase;

        struct Job
        {
            public ChunkStreamer.ChunkView View;
            public Mesh Mesh;
            public Task Bake;
        }

        readonly List<Job> jobs = new List<Job>();

        public int Attached { get; private set; }
        public int Failed { get; private set; }
        public int Pending => jobs.Count;

        /// <summary>Starts cooking a chunk's mesh and hands its arrays back.</summary>
        public void Start(ChunkStreamer.ChunkView v, ChunkCollision c)
        {
            using var _ = DadMarkers.StreamingColliders.Auto();
            var flags = MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontNotifyMeshUsers;
            var mesh = new Mesh { name = v.Cell.Id + " collision" };
            mesh.SetVertexBufferParams(c.VertexCount, new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, 0));
            mesh.SetVertexBufferData(c.Positions, 0, 0, 3 * c.VertexCount, 0, flags);
            mesh.SetIndexBufferParams(c.IndexCount, IndexFormat.UInt32);
            mesh.SetIndexBufferData(c.Indices, 0, 0, c.IndexCount, flags);
            mesh.subMeshCount = 1;
            mesh.SetSubMesh(0, new SubMeshDescriptor(0, c.IndexCount), flags);
            mesh.RecalculateBounds(flags);
            c.Release();
            int id = mesh.GetInstanceID();
            jobs.Add(new Job { View = v, Mesh = mesh, Bake = Task.Run(() => Physics.BakeMesh(id, false, Cooking)) });
        }

        /// <summary>Attaches a mesh finished cooking, one a frame. A mesh whose chunk has gone is destroyed here, not under the worker.</summary>
        public void Collect()
        {
            if (jobs.Count == 0) return;
            using var _ = DadMarkers.StreamingColliders.Auto();
            for (int i = 0; i < jobs.Count;)
            {
                var j = jobs[i];
                if (!j.Bake.IsCompleted) { i++; continue; }
                jobs.RemoveAt(i);
                if (j.View.Gone) { Object.Destroy(j.Mesh); continue; }
                if (j.Bake.IsFaulted)
                {
                    Failed++;
                    Debug.LogWarning($"[Dad] streaming: {j.View.Cell.Id} collision failed: {j.Bake.Exception?.GetBaseException().Message}");
                    Object.Destroy(j.Mesh);
                    continue;
                }
                var go = new GameObject("Collision") { layer = WorldLayers.World };
                go.transform.SetParent(j.View.Root.transform, false);
                var mc = go.AddComponent<MeshCollider>();
                mc.cookingOptions = Cooking;
                mc.sharedMesh = j.Mesh;
                j.View.CollisionMesh = j.Mesh;
                Attached++;
                return; // one a frame
            }
        }

        /// <summary>The streamer is going: nothing is left to attach to. A bake still running finishes against a live mesh.</summary>
        public void Abandon()
        {
            foreach (var j in jobs)
            {
                try { j.Bake.Wait(); } catch (System.AggregateException) { }
                Object.Destroy(j.Mesh);
            }
            jobs.Clear();
        }
    }
}
