// Flat ground surfaces for one chunk, built off the main thread: the base plate, landuse,
// parks, water, canals and plazas, in one triangle list with a colour per vertex.
//
// Layers sit at fixed heights so they never fight in the depth buffer. Park, water, canal and
// plaza heights are the browser build's (dhaka-after-dark src/viewer/meshes.ts: y 0.05, 0.12,
// 0.14, 0.22 m). The browser build draws no landuse; the two landuse heights below are this
// port's own design choice. Bridge decks belong to the road builder (C-03).
// Colours are albedo for the lit world shader (D-01), this port's choice: dusty packed earth
// for the base and most landuse, dry grass, murky water, concrete paving. Each comes with its
// WorldMaterial.
//
// Vertices are x, y, z floats in metres from the chunk's south-west corner (x = east,
// z = north). Colours are sRGB with the material in alpha, packed as WorldMaterials.Pack.
// Triangles are clockwise seen from above, Unity's front face, so normals point up.
using System;
using System.Collections.Generic;

namespace Dad.World
{
    public enum GroundLayer { Base, LanduseBroad, LanduseDetail, Park, Water, Canal, Plaza }

    public static class GroundPalette
    {
        public static readonly float[] Height = { 0f, 0.02f, 0.035f, 0.05f, 0.12f, 0.14f, 0.22f };

        // Albedo, sRGB (this port's choice, D-01): soil about 0.11-0.15 linear, grass 0.08-0.12,
        // paving about 0.25. Landuse tones differ a little so districts read apart.
        public const uint Ground = 0x6a6153, Park = 0x4b5e37, Water = 0x34463f, Canal = 0x3a4d45, Plaza = 0x8b867b;
        public const uint Residential = 0x6b6254, Commercial = 0x706a5f, Industrial = 0x655e53, Institutional = 0x686558,
            Grass = 0x56663e, Sport = 0x5a6a44, LanduseOther = 0x686154;

        /// <summary>Layer, sRGB colour (0xRRGGBB) and material for a DADC landuse type ("leisure:" prefix for leisure areas).</summary>
        public static (GroundLayer layer, uint rgb, WorldMaterial mat) Landuse(string type)
        {
            switch (type)
            {
                case "city-green": return (GroundLayer.LanduseBroad, Grass, WorldMaterial.Grass);
                case "city-soil": return (GroundLayer.LanduseDetail, Residential, WorldMaterial.Soil);
                case "residential": case "slum": case "slam":
                    return (GroundLayer.LanduseBroad, Residential, WorldMaterial.Soil);
                case "commercial": case "retail":
                    return (GroundLayer.LanduseBroad, Commercial, WorldMaterial.Soil);
                case "industrial": case "garages": case "depot": case "railway": case "landfill":
                case "brownfield": case "construction":
                    return (GroundLayer.LanduseBroad, Industrial, WorldMaterial.Soil);
                case "military":
                    return (GroundLayer.LanduseBroad, Institutional, WorldMaterial.Soil);
                case "education": case "religious": case "cemetery": case "churchyard":
                    return (GroundLayer.LanduseDetail, Institutional, WorldMaterial.Soil);
                case "grass": case "meadow": case "greenfield": case "flowerbed": case "farmland": case "orchard":
                case "plant_nursery": case "allotments": case "playground": case "leisure:common": case "leisure:golf_course":
                    return (GroundLayer.LanduseDetail, Grass, WorldMaterial.Grass);
                case "leisure:swimming_pool": case "aquaculture":
                    return (GroundLayer.Water, Water, WorldMaterial.Water);
                default:
                    if (type != null && type.StartsWith("leisure:", StringComparison.Ordinal)) return (GroundLayer.LanduseDetail, Sport, WorldMaterial.Grass);
                    return (GroundLayer.LanduseBroad, LanduseOther, WorldMaterial.Soil);
            }
        }

        public static uint Pack(uint rgb, WorldMaterial m) => WorldMaterials.Pack(rgb, m);
    }

    public sealed class ChunkSurfaces
    {
        public float[] Positions;
        public uint[] Colors;
        public int[] Indices;
        public int VertexCount => Colors.Length;
        /// <summary>Polygons and ribbons drawn, by GroundLayer.</summary>
        public readonly int[] Features = new int[7];
        /// <summary>Polygons with area that produced no triangles (zero-area clipping slivers are not counted).</summary>
        public int Failed;

        public static ChunkSurfaces Build(DadcChunk c)
        {
            var s = new ChunkSurfaces();
            var b = BuildScratch.Take();
            float size = (float)ChunkCell.Size;
            uint g = GroundPalette.Pack(GroundPalette.Ground, WorldMaterial.Soil);
            Vertex(b, 0, 0, 0, g);
            Vertex(b, 0, 0, size, g);
            Vertex(b, size, 0, size, g);
            Vertex(b, size, 0, 0, g);
            b.Indices.AddRange(new[] { 0, 1, 2, 0, 2, 3 });
            s.Features[(int)GroundLayer.Base] = 1;

            if (c.Landuse != null)
                foreach (var l in c.Landuse)
                {
                    var (layer, rgb, mat) = GroundPalette.Landuse(l.Type);
                    s.Polygon(b, l.Outer, l.Holes, layer, rgb, mat);
                }
            if (c.Parks != null) foreach (var p in c.Parks) s.Polygon(b, p.Outer, p.Holes, GroundLayer.Park, GroundPalette.Park, WorldMaterial.Grass);
            if (c.Water != null) foreach (var p in c.Water) s.Polygon(b, p.Outer, p.Holes, GroundLayer.Water, GroundPalette.Water, WorldMaterial.Water);
            if (c.Plazas != null) foreach (var p in c.Plazas) s.Polygon(b, p.Outer, p.Holes, GroundLayer.Plaza, GroundPalette.Plaza, WorldMaterial.Paving);
            if (c.Canals != null)
                foreach (var k in c.Canals)
                    if (Ribbon(b, k.Pts, k.WidthCm / 200f, GroundPalette.Height[(int)GroundLayer.Canal], GroundPalette.Pack(GroundPalette.Canal, WorldMaterial.Water)))
                        s.Features[(int)GroundLayer.Canal]++;

            s.Positions = b.Positions.ToArray();
            s.Colors = b.Colors.ToArray();
            s.Indices = b.Indices.ToArray();
            return s;
        }

        /// <summary>Flattens a DADC ring set (centimetres) into Earcut input (metres).</summary>
        public static void Flatten(int[] outer, int[][] holes, List<double> flat, List<int> holeStarts)
        {
            flat.Clear();
            holeStarts.Clear();
            Ring(outer, flat);
            if (holes == null) return;
            foreach (var h in holes)
            {
                if (h == null || h.Length < 6) continue;
                holeStarts.Add(flat.Count / 2);
                Ring(h, flat);
            }
        }

        static void Ring(int[] pts, List<double> flat)
        {
            int n = pts.Length / 2;
            // Drop a closing point equal to the first.
            if (n > 1 && pts[0] == pts[2 * n - 2] && pts[1] == pts[2 * n - 1]) n--;
            for (int i = 0; i < n; i++)
            {
                flat.Add(pts[2 * i] / 100.0);
                flat.Add(pts[2 * i + 1] / 100.0);
            }
        }

        void Polygon(BuildScratch b, int[] outer, int[][] holes, GroundLayer layer, uint rgb, WorldMaterial mat)
        {
            if (outer == null || outer.Length < 6) return;
            Flatten(outer, holes, b.Flat, b.Holes);
            b.Tris.Clear();
            if (Earcut.Triangulate(b.Flat, b.Holes, b.Tris) == 0)
            {
                // Slivers left by clipping to the chunk edge have no area and nothing to draw.
                if (OuterArea(b.Flat, b.Holes) > 0.01) Failed++;
                return;
            }
            float y = GroundPalette.Height[(int)layer];
            uint col = GroundPalette.Pack(rgb, mat);
            int first = b.Colors.Count;
            for (int i = 0; i < b.Flat.Count; i += 2) Vertex(b, (float)b.Flat[i], y, (float)b.Flat[i + 1], col);
            for (int t = 0; t < b.Tris.Count; t += 3)
            {
                int i0 = b.Tris[t], i1 = b.Tris[t + 1], i2 = b.Tris[t + 2];
                double cross = (b.Flat[2 * i1] - b.Flat[2 * i0]) * (b.Flat[2 * i2 + 1] - b.Flat[2 * i0 + 1])
                             - (b.Flat[2 * i1 + 1] - b.Flat[2 * i0 + 1]) * (b.Flat[2 * i2] - b.Flat[2 * i0]);
                // Counter-clockwise in (east, north) faces down in Unity: swap two corners.
                if (cross > 0) (i1, i2) = (i2, i1);
                b.Indices.Add(first + i0);
                b.Indices.Add(first + i1);
                b.Indices.Add(first + i2);
            }
            Features[(int)layer]++;
        }

        static double OuterArea(List<double> flat, List<int> holes)
        {
            int end = holes.Count > 0 ? holes[0] * 2 : flat.Count;
            double sum = 0;
            for (int i = 0, j = end - 2; i < end; j = i, i += 2) sum += (flat[j] - flat[i]) * (flat[i + 1] + flat[j + 1]);
            return Math.Abs(sum) / 2;
        }

        static void Vertex(BuildScratch b, float x, float y, float z, uint col)
        {
            b.Positions.Add(x);
            b.Positions.Add(y);
            b.Positions.Add(z);
            b.Colors.Add(col);
        }

        /// <summary>A flat strip of half-width <paramref name="half"/> metres along a centreline (cm), mitred at bends.</summary>
        static bool Ribbon(BuildScratch b, int[] pts, float half, float y, uint col)
        {
            if (pts == null || half <= 0) return false;
            var xs = new List<double>(pts.Length / 2);
            var zs = new List<double>(pts.Length / 2);
            for (int i = 0; i + 1 < pts.Length; i += 2)
            {
                double x = pts[i] / 100.0, z = pts[i + 1] / 100.0;
                if (xs.Count > 0 && Math.Abs(x - xs[xs.Count - 1]) < 1e-3 && Math.Abs(z - zs[zs.Count - 1]) < 1e-3) continue;
                xs.Add(x);
                zs.Add(z);
            }
            int n = xs.Count;
            if (n < 2) return false;
            int first = b.Colors.Count;
            for (int i = 0; i < n; i++)
            {
                // Unit normals of the segments before and after this point.
                Normal(xs, zs, Math.Max(i - 1, 0), out double nx0, out double nz0);
                Normal(xs, zs, Math.Min(i, n - 2), out double nx1, out double nz1);
                double mx = nx0 + nx1, mz = nz0 + nz1, len = Math.Sqrt(mx * mx + mz * mz);
                if (len < 1e-9) { mx = nx1; mz = nz1; len = 1; }
                mx /= len;
                mz /= len;
                // Mitre length, capped at 3x the half width on sharp turns.
                double scale = half / Math.Max(mx * nx1 + mz * nz1, 1.0 / 3.0);
                Vertex(b, (float)(xs[i] + mx * scale), y, (float)(zs[i] + mz * scale), col);
                Vertex(b, (float)(xs[i] - mx * scale), y, (float)(zs[i] - mz * scale), col);
            }
            for (int i = 0; i < n - 1; i++)
            {
                int l0 = first + 2 * i, r0 = l0 + 1, l1 = l0 + 2, r1 = l0 + 3;
                // Left is +normal, the normal is the direction turned counter-clockwise, so
                // l0 -> l1 -> r0 runs clockwise seen from above.
                b.Indices.Add(l0); b.Indices.Add(l1); b.Indices.Add(r0);
                b.Indices.Add(r0); b.Indices.Add(l1); b.Indices.Add(r1);
            }
            return true;
        }

        static void Normal(List<double> xs, List<double> zs, int i, out double nx, out double nz)
        {
            double dx = xs[i + 1] - xs[i], dz = zs[i + 1] - zs[i], len = Math.Sqrt(dx * dx + dz * dz);
            nx = -dz / len;
            nz = dx / len;
        }
    }
}
