// The elevated rails of a chunk's eight neighbours (K-37 P-2 and K-50, approved by the owner on 2026-11-04).
// A chunk's file holds only its own piece of each track, cut on its border. Where Line 6's two tracks run on
// either side of a border (Farmgate: 6.3–7.0 m apart, one each side), a chunk alone cannot see that they are a
// twin, so each side built a single-track deck with its own piers (K-50 a, V-5). With its neighbours' elevated
// rails, the viaduct builder pairs the tracks and places piers along the whole deck the same way on both sides,
// and each chunk builds only the part of the deck and the piers that fall inside it.
//
// Read-only: the rails are read from the neighbours' chunk files as they are, translated into this chunk's
// centimetres, and never written back. Each file is decoded at most once per directory (cached), and only for
// chunks that have an elevated track of their own: 64 of the city's 1,303.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace Dad.World
{
    public static class NeighbourRails
    {
        static readonly ConcurrentDictionary<string, Lazy<Rail[]>> cache = new ConcurrentDictionary<string, Lazy<Rail[]>>(StringComparer.Ordinal);

        static bool Elevated(Rail r) => r.YCm >= ChunkRoads.ViaductMinY * 100 && r.Pts != null && r.Pts.Length >= 4;

        /// <summary>The elevated rails in a chunk file, in that chunk's own centimetres; none if there is no file.</summary>
        static Rail[] Read(string file) => cache.GetOrAdd(file, f => new Lazy<Rail[]>(() =>
        {
            if (!File.Exists(f)) return Array.Empty<Rail>();
            var c = Dadc.Decode(File.ReadAllBytes(f));
            var o = new List<Rail>();
            foreach (var r in c.Rails ?? Array.Empty<Rail>())
                if (Elevated(r)) o.Add(r);
            return o.ToArray();
        }, LazyThreadSafetyMode.ExecutionAndPublication)).Value;

        /// <summary>
        /// Sets c.NearRails to the elevated rails of the eight chunks around it (files in chunkDir), in c's
        /// centimetres. A chunk with no elevated track of its own builds no deck and gets none.
        /// </summary>
        public static void Attach(DadcChunk c, string chunkDir)
        {
            bool any = false;
            foreach (var r in c.Rails ?? Array.Empty<Rail>())
                if (Elevated(r)) { any = true; break; }
            if (!any) { c.NearRails = Array.Empty<Rail>(); return; }
            var o = new List<Rail>();
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    // Corner east grows with Ix; corner north is -(Iy + 1) chunks, so it falls as Iy grows.
                    int offE = dx * DadcChunk.ChunkCm, offN = -dy * DadcChunk.ChunkCm;
                    foreach (var r in Read(Path.Combine(chunkDir, $"c_{c.Ix + dx}_{c.Iy + dy}.dadc")))
                    {
                        var pts = new int[r.Pts.Length];
                        for (int i = 0; i + 1 < pts.Length; i += 2) { pts[i] = r.Pts[i] + offE; pts[i + 1] = r.Pts[i + 1] + offN; }
                        o.Add(new Rail { Id = r.Id, YCm = r.YCm, Pts = pts });
                    }
                }
            c.NearRails = o.ToArray();
        }
    }
}
