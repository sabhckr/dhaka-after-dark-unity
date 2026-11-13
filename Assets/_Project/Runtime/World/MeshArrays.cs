// The arrays a chunk's road, building and line meshes are uploaded from. When the streamer builds a
// chunk (pooled), they come from shared pools and are usually longer than the mesh: the builder's
// VertexCount and IndexCount give the part in use, and the streamer hands them back (Release) once
// the mesh is uploaded. Copied fresh, these arrays were 3.7 of the 6.9 MB the streaming workers
// allocated for the 12 slice chunks, and the collections they caused could stop the main thread in
// the middle of an apply step (C-07). Built anywhere else (tests, tools), they are exact copies and
// nothing needs handing back. Ground and debug overlays stay exact: theirs are small.
// No UnityEngine dependency: safe on worker threads. Rent and Return may run on different threads.
using System.Buffers;
using System.Collections.Generic;

namespace Dad.World
{
    public static class MeshArrays
    {
        // Arrays come in powers of two from 16 elements; up to 4M elements (16 MB) each, 32 of a
        // size. A dense chunk's buildings are about 30,000 floats, and the apply queue can hold a
        // dozen finished chunks at once. Larger arrays are made and dropped as before.
        const int MaxLength = 1 << 22, PerSize = 32;
        static readonly ArrayPool<float> floats = ArrayPool<float>.Create(MaxLength, PerSize);
        static readonly ArrayPool<uint> uints = ArrayPool<uint>.Create(MaxLength, PerSize);
        static readonly ArrayPool<int> ints = ArrayPool<int>.Create(MaxLength, PerSize);

        public static float[] Copy(List<float> from, bool pooled)
        {
            var a = pooled ? floats.Rent(from.Count) : new float[from.Count];
            from.CopyTo(a);
            return a;
        }

        public static uint[] Copy(List<uint> from, bool pooled)
        {
            var a = pooled ? uints.Rent(from.Count) : new uint[from.Count];
            from.CopyTo(a);
            return a;
        }

        public static int[] Copy(List<int> from, bool pooled)
        {
            var a = pooled ? ints.Rent(from.Count) : new int[from.Count];
            from.CopyTo(a);
            return a;
        }

        /// <summary>Hands back arrays from a pooled Copy. Nothing may use them afterwards.</summary>
        public static void Return(float[] a) { if (a != null && a.Length > 0) floats.Return(a); }
        public static void Return(uint[] a) { if (a != null && a.Length > 0) uints.Return(a); }
        public static void Return(int[] a) { if (a != null && a.Length > 0) ints.Return(a); }
    }
}
