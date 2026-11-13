// Extruded buildings for one chunk, built off the main thread: walls, flat roofs and, under
// raised buildings, an underside, in one triangle list with a colour per vertex.
//
// Heights follow the browser build's rules (dhaka-after-dark src/geo/infer.ts and classify.ts);
// BuildingRules ports levels, elevation and base with their source (osm, rule, default).
// The browser build draws every building from the ground and never uses the base. This port
// does, and adds one rule of its own for the case infer.ts names, "a station or deck over a
// road": a train_station raised by layer alone (no min_height or min_level) stands on the
// generator's layer height, 6 + (layer - 1) x 5 m (classify.ts railElevation; road bridges use
// the same steps). In the city data that is exactly the 16 Dhaka Metro Line 6 stations (layer 3
// or 4). The rule looks only at the building's own tags, so every piece of a building that the
// chunk grid cut in two gets the same answer. Other layer-only buildings (879 in the city) stay
// on the ground and are counted: layer alone does not say how high.
// The top: a measured or levels-derived height is the top above ground (OSM's meaning). A
// class-default height is a body height, so a raised building keeps it above its base. A top at
// or below the base becomes base + one level, and is counted.
//
// Materials and colours (D-06b): each building's walls and roof take the material and tone its skin
// gives (BuildingSkin: building:material from the data, else a labelled rule by category), with a
// darker foot on every wall. They are albedo: the lit world shader (D-01) shades them by the real sun
// and sky, so no light direction is baked in. Buildings with a parapet (BuildingSkin) have their roof
// ParapetHeight below the top and a parapet ParapetThickness thick around each ring: its roof-side
// face and its coping, offset into the solid at the corners by mitre. The rooftop kit stands on the
// roof (Rooftops.cs, built with the street cells). Milestone 2 draws one facade family in geometry in
// the Farmgate validation area (Facades.cs).
//
// The far tier (D-06a): every building is classified (BuildingClass), and the walls of the plain
// ones carry facade coordinates for the Dad/World shader to draw their family's windows, shops and
// floor lines, with no geometry added. Per vertex, four floats (Facade):
// - u: bays along the wall, 0 at its start and a whole number of bays (BuildingClass.BayWidth) at its end;
// - v: floors up from the building's base, the height divided evenly into BuildingRules levels;
// - w: the family, plus 8 on a wall facing a vehicle road (shops on the ground floor), plus 16 x a
//   seed from the building's id (0-255), plus WallFlag (4096) on every wall, plus 8192 x the floors
//   below the roof on a building with a parapet (D-06b; the wall above them is the parapet): exact in
//   a float up to 2,047 floors;
// - s: metres along the ring from 8 x the seed at its first corner, so the shader's wall textures run
//   along each wall at its true length (K-21) and start differently on each building.
// Every wall drawn here carries s and WallFlag; only patterned walls carry u, v and a family. Walls
// shorter than ChunkBuildings.MinFacadeWall, walls cut along the chunk border and buildings with no
// family stay plain. Roofs, undersides and M2's geometry facades carry 0 (box-projected as before).
//
// Vertices are x, y, z floats in metres from the chunk's south-west corner; colours are sRGB with
// the material in alpha, packed as WorldMaterials.Pack. Every triangle faces out of its building
// (Unity's front face).
using System;
using System.Collections.Generic;

namespace Dad.World
{
    public enum Src { Osm, Rule, Default }

    public static class BuildingRules
    {
        public const double MetresPerLevel = 3.2;

        /// <summary>infer.ts buildingLevels: building:levels; else measured height / 3.2 (rule); else the estimate / 3.2 (default).</summary>
        public static (double value, Src src) Levels(Building b)
        {
            if (b.LevelsHalf is int lv && lv > 0) return (lv / 2.0, Src.Osm);
            double value = Math.Max(1, Math.Round(b.HeightCm / 100.0 / MetresPerLevel, MidpointRounding.AwayFromZero));
            return (value, b.HeightSource == "measured" ? Src.Rule : Src.Default);
        }

        /// <summary>infer.ts buildingElevated: min_height or min_level above 0 (osm); layer above 0 alone (rule); else on the ground.</summary>
        public static (bool value, Src src) Elevated(Building b)
        {
            if ((b.MinHeightCm ?? 0) > 0 || (b.MinLevelHalf ?? 0) > 0) return (true, Src.Osm);
            if ((b.Layer ?? 0) > 0) return (true, Src.Rule);
            return (false, Src.Default);
        }

        /// <summary>infer.ts buildingBase: min_height; else min_level x 3.2 m (rule); else 0.</summary>
        public static (double value, Src src) Base(Building b)
        {
            if (b.MinHeightCm is int mh && mh != 0) return (mh / 100.0, Src.Osm);
            if (b.MinLevelHalf is int ml && ml != 0) return (ml / 2.0 * MetresPerLevel, Src.Rule);
            return (0, Src.Default);
        }

        /// <summary>The generator's height for a layer: 6 m for layer 1, then 5 m a layer.</summary>
        public static double LayerHeight(int layer) => 6 + (Math.Max(layer, 1) - 1) * 5;

        public struct Placement
        {
            public double Base, Top;
            public Src BaseSource;
            /// <summary>A layer-only station, raised to its layer height.</summary>
            public bool LayerRaised;
            /// <summary>Layer alone, nothing under it: left on the ground.</summary>
            public bool LayerIgnored;
            /// <summary>The height did not reach above the base; drawn one level tall.</summary>
            public bool TopFixed;
        }

        public static Placement Place(Building b)
        {
            var p = new Placement();
            (p.Base, p.BaseSource) = Base(b);
            var (elevated, how) = Elevated(b);
            if (elevated && how == Src.Rule)
            {
                if (b.Type == "train_station") { p.Base = LayerHeight(b.Layer.Value); p.BaseSource = Src.Rule; p.LayerRaised = true; }
                else p.LayerIgnored = true;
            }
            double h = b.HeightCm / 100.0;
            bool body = b.HeightSource != "measured" && !(b.LevelsHalf > 0);
            p.Top = body ? p.Base + h : h;
            if (p.Top <= p.Base + 0.01)
            {
                p.Top = p.Base + MetresPerLevel;
                p.TopFixed = true;
            }
            return p;
        }
    }

    public static class BuildingPalette
    {
        // Plaster albedo (this port's choice), about 0.3-0.45 linear: cream, grey, sand, cool grey,
        // pink-beige and dusty beige, picked per building by id. Roofs are dusty concrete slabs.
        public static readonly uint[] Walls = { 0xb5ad9c, 0xa8a397, 0xbba98a, 0x9c9d96, 0xb39f8a, 0xa39a88 };
        public const uint Roof = 0x8a857b, Underside = 0x5d5952;
        /// <summary>The foot of a wall is darker than its top (grime and splash).</summary>
        public const double Foot = 0.85;

        public static uint Tone(long id)
        {
            ulong z = (ulong)id + 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            z ^= z >> 31;
            return Walls[(int)(z % (ulong)Walls.Length)];
        }

        /// <summary>Packs an sRGB colour scaled by f (0..1) with its material, as WorldMaterials.Pack.</summary>
        public static uint Shade(uint rgb, double f, WorldMaterial m)
        {
            uint r = (uint)Math.Round(((rgb >> 16) & 0xff) * f), g = (uint)Math.Round(((rgb >> 8) & 0xff) * f), b = (uint)Math.Round((rgb & 0xff) * f);
            return WorldMaterials.Pack((r << 16) | (g << 8) | b, m);
        }
    }

    public sealed partial class ChunkBuildings
    {
        /// <summary>The mesh arrays. Built pooled (MeshArrays), they are longer than the mesh: use the counts.</summary>
        public float[] Positions;
        public uint[] Colors;
        public int[] Indices;
        /// <summary>Facade coordinates, four a vertex (u bays, v floors, w family and flags, s metres along the wall; see the file header).</summary>
        public float[] Facade;
        public int VertexCount, IndexCount;
        public int Drawn, Slivers, RaisedOsm, RaisedRule, RaisedByLayer, LayerIgnored, TopsFixed, RoofFailed;
        /// <summary>Lowest and highest corner of the mesh (x, y, z), for its bounds.</summary>
        public readonly float[] Min = { 0, 0, 0 }, Max = { 0, 0, 0 };
        /// <summary>Per building, in chunk order: placement and its vertex range (count 0 when not drawn).</summary>
        public BuildingRules.Placement[] Placements;
        public int[] FirstVertex, VertexCounts;
        /// <summary>Per building: its category and the source of it (BuildingClass), whether drawn or not.</summary>
        public BuildingCategory[] Categories;
        public ClassSource[] Sources;
        /// <summary>Walls drawn with a far-tier pattern, and those of them with shops on the ground floor.</summary>
        public int PatternWalls, ShopWalls;
        /// <summary>Per building: its skin (BuildingSkin), whether drawn or not.</summary>
        public Skin[] Skins;
        /// <summary>Buildings drawn with a parapet, its pieces (one per ring edge), and pieces left out because the
        /// edge is too short for the parapet's thickness (its inner edge would fold back).</summary>
        public int Parapets, ParapetPieces, ParapetFolded;

        // The thread's BuildScratch lists, held only while Build runs.
        List<float> p;
        List<uint> col;
        List<int> idx;
        List<float> fac;
        bool pooled;

        public static ChunkBuildings Build(DadcChunk c, bool pooled = false)
        {
            RoadWidths.Resolve(c); // facades face the roads at their resolved widths
            var scratch = BuildScratch.Take();
            var s = new ChunkBuildings { p = scratch.Positions, col = scratch.Colors, idx = scratch.Indices, fac = scratch.Facade, pooled = pooled };
            int n = c.Buildings?.Length ?? 0;
            s.Placements = new BuildingRules.Placement[n];
            s.FirstVertex = new int[n];
            s.VertexCounts = new int[n];
            s.Categories = new BuildingCategory[n];
            s.Sources = new ClassSource[n];
            s.Skins = new Skin[n];
            var near = scratch.Near ??= new BuildingClass.Near();
            BuildingClass.ClassifyChunk(c, near, s.Categories, s.Sources);
            s.ns = scratch.NearFacades ??= new NearScratch();
            s.ns.Reset(c);
            var flat = scratch.Flat;
            var holes = scratch.Holes;
            var tris = scratch.Tris;
            for (int i = 0; i < n; i++)
            {
                var b = c.Buildings[i];
                s.FirstVertex[i] = s.col.Count;
                if (b.Outer == null || b.Outer.Length < 6) continue;
                var pl = BuildingRules.Place(b);
                s.Placements[i] = pl;
                ChunkSurfaces.Flatten(b.Outer, b.Holes, flat, holes);
                // Clipping to the chunk edge leaves a few zero-area slivers; they have nothing to draw.
                if (flat.Count < 6 || Area(flat, 0, holes.Count > 0 ? holes[0] : flat.Count / 2) <= 0.01) { s.Slivers++; continue; }

                uint tone = BuildingPalette.Tone(b.Id);
                // The validation area's buildings get the facade family (Facades.cs), their roof below a parapet.
                bool facade = Facaded(c, b, pl);
                var skin = s.Skins[i] = BuildingSkin.Of(b, s.Categories[i], pl, facade);
                double roof = skin.RoofY;
                uint wallTop = BuildingPalette.Shade(skin.WallTone, 1, skin.Wall), wallFoot = BuildingPalette.Shade(skin.WallTone, BuildingPalette.Foot, skin.Wall);
                // The far tier, on buildings without M2 geometry: floors divide the wall up to the roof.
                var far = new FarTier { Family = facade ? FacadeFamily.None : BuildingClass.FamilyOf(s.Categories[i]), Seed = Seed(b.Id) };
                if (far.Family != FacadeFamily.None)
                {
                    int floors = Math.Max(1, (int)Math.Round((roof - pl.Base) / BuildingRules.MetresPerLevel));
                    far.Floor = (roof - pl.Base) / floors;
                    far.Bay = BuildingClass.BayWidth(far.Family);
                    far.Shops = far.Family == FacadeFamily.ShopHouse || far.Family == FacadeFamily.Commercial;
                    far.RoofFloors = skin.Parapet ? floors : 0;
                    // Near facades (NearFacades.cs) on buildings on the ground.
                    if (pl.Base <= 0) far.Style = s.NearStyleOf(c, b, s.Categories[i], s.Sources[i], flat, holes.Count > 0 ? holes[0] : flat.Count / 2, out far.District);
                }
                for (int r = 0; r <= holes.Count; r++)
                {
                    int start = r == 0 ? 0 : holes[r - 1], end = r < holes.Count ? holes[r] : flat.Count / 2;
                    if (facade && r == 0) { s.FacadeWalls(c, b, flat, start, end, roof, pl.Top, tone); s.Pad(); }
                    else s.Walls(flat, start, end, r > 0, pl.Base, facade ? roof : pl.Top, wallTop, wallFoot, far, near);
                    if (skin.Parapet)
                        s.Parapet(flat, start, end, r > 0, roof, pl.Top, BuildingPalette.Shade(skin.WallTone, BuildingPalette.Foot, skin.Wall),
                            BuildingPalette.Shade(BuildingSkin.Coping, 1, WorldMaterial.Concrete));
                }
                if (skin.Parapet) s.Parapets++;
                tris.Clear();
                if (Earcut.Triangulate(flat, holes, tris) == 0) s.RoofFailed++;
                else
                {
                    s.Cap(flat, tris, roof, BuildingPalette.Shade(skin.RoofTone, 1, skin.Roof), false);
                    if (pl.Base > 0) s.Cap(flat, tris, pl.Base, BuildingPalette.Shade(BuildingPalette.Underside, 1, WorldMaterial.Concrete), true);
                }
                s.VertexCounts[i] = s.col.Count - s.FirstVertex[i];
                s.Drawn++;
                if (pl.LayerRaised) s.RaisedByLayer++;
                else if (pl.Base > 0 && pl.BaseSource == Src.Osm) s.RaisedOsm++;
                else if (pl.Base > 0) s.RaisedRule++;
                if (pl.LayerIgnored) s.LayerIgnored++;
                if (pl.TopFixed) s.TopsFixed++;
            }
            s.KeepNearWalls(pooled);
            s.ns = null;
            s.VertexCount = s.col.Count;
            s.IndexCount = s.idx.Count;
            s.Positions = MeshArrays.Copy(s.p, pooled);
            s.Colors = MeshArrays.Copy(s.col, pooled);
            s.Indices = MeshArrays.Copy(s.idx, pooled);
            s.Facade = MeshArrays.Copy(s.fac, pooled);
            s.p = null;
            s.col = null;
            s.idx = null;
            s.fac = null;
            for (int k = 0; k < 3; k++) { s.Min[k] = float.MaxValue; s.Max[k] = float.MinValue; }
            for (int v = 0; v < 3 * s.VertexCount; v += 3)
                for (int k = 0; k < 3; k++)
                {
                    s.Min[k] = Math.Min(s.Min[k], s.Positions[v + k]);
                    s.Max[k] = Math.Max(s.Max[k], s.Positions[v + k]);
                }
            if (s.VertexCount == 0) for (int k = 0; k < 3; k++) s.Min[k] = s.Max[k] = 0;
            return s;
        }

        /// <summary>Hands pooled mesh arrays back once the mesh is uploaded; the arrays are gone after.</summary>
        public void Release()
        {
            if (!pooled) return;
            pooled = false;
            MeshArrays.Return(Positions);
            MeshArrays.Return(Colors);
            MeshArrays.Return(Indices);
            MeshArrays.Return(Facade);
            Positions = null;
            Colors = null;
            Indices = null;
            Facade = null;
        }

        /// <summary>A building's far-tier facade: family, floor height and bay width in metres, whether its
        /// road-facing walls get shops, and its seed; and its near facade's style and district (NearFacades.cs).</summary>
        struct FarTier
        {
            public FacadeFamily Family;
            public double Floor, Bay;
            public bool Shops;
            public int Seed, RoofFloors;
            public NearStyle Style;
            public District District;
        }

        /// <summary>Set in w on every wall Walls draws: s is valid (see the file header).</summary>
        public const int WallFlag = 4096;
        /// <summary>w's unit for the floors below a parapet (see the file header).</summary>
        public const int RoofFloorUnit = 8192;

        /// <summary>A parapet on one ring (see the file header): the face towards the roof from roofY to topY, set
        /// ParapetThickness into the solid, and the coping between it and the wall's top. Edges cut along the chunk
        /// border have none: the neighbouring piece's roof continues there. Nor do edges whose inner edge would be
        /// reversed or under 5 cm (slivers and short jogs left by clipping), counted in ParapetFolded.</summary>
        void Parapet(List<double> flat, int start, int end, bool hole, double roofY, double topY, uint face, uint coping)
        {
            int count = end - start;
            if (count < 3) return;
            bool right = (Area(flat, start, end, signed: true) > 0) != hole;
            const double t = BuildingSkin.ParapetThickness;
            var inner = ring;
            inner.Clear();
            for (int k = 0; k < count; k++)
            {
                int a = start + (k + count - 1) % count, b = start + k, c = start + (k + 1) % count;
                Outward(flat, a, b, right, out double n1x, out double n1z);
                Outward(flat, b, c, right, out double n2x, out double n2z);
                double mx = n1x + n2x, mz = n1z + n2z, ml = Math.Sqrt(mx * mx + mz * mz);
                if (ml < 1e-9) { mx = n1x; mz = n1z; } else { mx /= ml; mz /= ml; }
                // Mitre: the offset point is t from both edges; at sharp corners at most 3 t.
                double d = Math.Max(mx * n1x + mz * n1z, 1.0 / 3);
                inner.Add(flat[2 * b] - mx * t / d);
                inner.Add(flat[2 * b + 1] - mz * t / d);
            }
            for (int k = 0; k < count; k++)
            {
                int a = start + k, b = start + (k + 1) % count;
                double px = flat[2 * a], pz = flat[2 * a + 1], qx = flat[2 * b], qz = flat[2 * b + 1];
                if ((qx - px) * (qx - px) + (qz - pz) * (qz - pz) < 1e-8 || OnChunkEdge(px, pz, qx, qz)) continue;
                int j = (k + 1) % count;
                double ipx = inner[2 * k], ipz = inner[2 * k + 1], iqx = inner[2 * j], iqz = inner[2 * j + 1];
                double along = (iqx - ipx) * (qx - px) + (iqz - ipz) * (qz - pz);
                if (along <= 0 || (iqx - ipx) * (iqx - ipx) + (iqz - ipz) * (iqz - ipz) < 0.05 * 0.05) { ParapetFolded++; continue; }
                // The face towards the roof: as a wall's, the other way round.
                int v = col.Count;
                Vertex(ipx, roofY, ipz, face);
                Vertex(iqx, roofY, iqz, face);
                Vertex(iqx, topY, iqz, face);
                Vertex(ipx, topY, ipz, face);
                if (right) Quad(v, v + 1, v + 2, v + 3);
                else Quad(v, v + 3, v + 2, v + 1);
                // The coping, facing up: p, q, then the inner points, turn into the solid.
                v = col.Count;
                Vertex(px, topY, pz, coping);
                Vertex(qx, topY, qz, coping);
                Vertex(iqx, topY, iqz, coping);
                Vertex(ipx, topY, ipz, coping);
                if (right) Quad(v, v + 3, v + 2, v + 1);
                else Quad(v, v + 1, v + 2, v + 3);
                ParapetPieces++;
            }
        }

        readonly List<double> ring = new List<double>(64);

        static void Outward(List<double> flat, int a, int b, bool right, out double nx, out double nz)
        {
            double dx = flat[2 * b] - flat[2 * a], dz = flat[2 * b + 1] - flat[2 * a + 1], len = Math.Sqrt(dx * dx + dz * dz);
            if (len < 1e-9) { nx = nz = 0; return; }
            nx = right ? dz / len : -dz / len;
            nz = right ? -dx / len : dx / len;
        }

        /// <summary>A building's pattern seed, 0-255, from its id: the same for every piece of it.</summary>
        static int Seed(long id) => (int)((((ulong)id * 0x9E3779B97F4A7C15UL) >> 56) & 0xff);

        /// <summary>One quad per ring edge, facing away from the solid (outwards, or into a courtyard),
        /// with far-tier facade coordinates where the building has a family (see the file header).</summary>
        void Walls(List<double> flat, int start, int end, bool hole, double baseY, double topY, uint top, uint foot, FarTier far, BuildingClass.Near near)
        {
            int count = end - start;
            if (count < 3) return;
            // Counter-clockwise rings (seen from above, x east, z north) have the solid on the left
            // of each edge, so the outward normal is the right-hand one; holes are the other way.
            bool right = (Area(flat, start, end, signed: true) > 0) != hole;
            double along = 8.0 * far.Seed;
            for (int k = 0; k < count; k++)
            {
                int a = start + k, b = start + (k + 1) % count;
                double px = flat[2 * a], pz = flat[2 * a + 1], qx = flat[2 * b], qz = flat[2 * b + 1];
                double dx = qx - px, dz = qz - pz, len = Math.Sqrt(dx * dx + dz * dz);
                if (len < 1e-4) continue;
                int v = col.Count;
                Vertex(px, baseY, pz, foot);
                Vertex(qx, baseY, qz, foot);
                Vertex(qx, topY, qz, top);
                Vertex(px, topY, pz, top);
                // The four vertices just written: foot p, foot q, top q, top p.
                int f = 4 * v;
                float w = WallFlag + 16 * far.Seed;
                fac[f + 3] = fac[f + 15] = (float)along;
                fac[f + 7] = fac[f + 11] = (float)(along + len);
                along += len;
                if (far.Family != FacadeFamily.None && len >= MinFacadeWall && !OnChunkEdge(px, pz, qx, qz))
                {
                    float bays = Math.Max(1, (int)Math.Floor(len / far.Bay)), floors = (float)((topY - baseY) / far.Floor);
                    double nx = right ? dz / len : -dz / len, nz = right ? -dx / len : dx / len;
                    bool road = !hole && (far.Shops || far.Style != NearStyle.None) && near.Faces(px + dx / 2, pz + dz / 2, nx, nz), shops = far.Shops && road;
                    w += (float)far.Family + (shops ? 8 : 0) + RoofFloorUnit * far.RoofFloors;
                    fac[f + 4] = bays;
                    fac[f + 8] = bays; fac[f + 9] = floors;
                    fac[f + 13] = floors;
                    PatternWalls++;
                    if (shops) ShopWalls++;
                    if (!hole) KeepNearWall(px, pz, qx, qz, nx, nz, baseY, topY, top, far, road);
                }
                fac[f + 2] = fac[f + 6] = fac[f + 10] = fac[f + 14] = w;
                // With the right-hand normal, p0 -> p1 -> q1 runs clockwise seen from outside.
                if (right) Quad(v, v + 3, v + 2, v + 1);
                else Quad(v, v + 1, v + 2, v + 3);
            }
        }

        void Quad(int a, int b, int c, int d)
        {
            idx.Add(a); idx.Add(b); idx.Add(c);
            idx.Add(a); idx.Add(c); idx.Add(d);
        }

        /// <summary>A flat roof (facing up) or underside (facing down) from the footprint's triangles.</summary>
        void Cap(List<double> flat, List<int> tris, double y, uint colour, bool down)
        {
            int first = col.Count;
            for (int i = 0; i < flat.Count; i += 2) Vertex(flat[i], y, flat[i + 1], colour);
            for (int t = 0; t < tris.Count; t += 3)
            {
                int i0 = tris[t], i1 = tris[t + 1], i2 = tris[t + 2];
                double cross = (flat[2 * i1] - flat[2 * i0]) * (flat[2 * i2 + 1] - flat[2 * i0 + 1])
                             - (flat[2 * i1 + 1] - flat[2 * i0 + 1]) * (flat[2 * i2] - flat[2 * i0]);
                // Counter-clockwise in (east, north) faces down in Unity.
                if (cross > 0 != down) (i1, i2) = (i2, i1);
                idx.Add(first + i0);
                idx.Add(first + i1);
                idx.Add(first + i2);
            }
        }

        void Vertex(double x, double y, double z, uint c)
        {
            p.Add((float)x);
            p.Add((float)y);
            p.Add((float)z);
            col.Add(c);
            fac.Add(0); fac.Add(0); fac.Add(0); fac.Add(0);
        }

        /// <summary>Zero facade coordinates for vertices written without them (the M2 kit, KitWriter).</summary>
        void Pad()
        {
            while (fac.Count < 4 * col.Count) fac.Add(0);
        }

        static double Area(List<double> flat, int start, int end, bool signed = false)
        {
            double sum = 0;
            for (int i = start, j = end - 1; i < end; j = i, i++)
                sum += flat[2 * j] * flat[2 * i + 1] - flat[2 * i] * flat[2 * j + 1];
            return signed ? sum / 2 : Math.Abs(sum) / 2;
        }

        /// <summary>Does an at-grade road (not a bridge, under 1 m) cross or lie inside the footprint? For checks; no rule uses it.</summary>
        public static bool CoversRoad(Building b, Road[] roads)
        {
            if (roads == null || b.Outer == null || b.Outer.Length < 6) return false;
            var o = b.Outer;
            int n = o.Length / 2;
            int minx = int.MaxValue, minz = int.MaxValue, maxx = int.MinValue, maxz = int.MinValue;
            for (int i = 0; i < n; i++)
            {
                minx = Math.Min(minx, o[2 * i]); maxx = Math.Max(maxx, o[2 * i]);
                minz = Math.Min(minz, o[2 * i + 1]); maxz = Math.Max(maxz, o[2 * i + 1]);
            }
            foreach (var r in roads)
            {
                if (r.Bridge || r.YCm >= 100 || r.Pts == null) continue;
                for (int k = 0; k + 3 < r.Pts.Length; k += 2)
                {
                    long ax = r.Pts[k], az = r.Pts[k + 1], bx = r.Pts[k + 2], bz = r.Pts[k + 3];
                    if (Math.Max(ax, bx) < minx || Math.Min(ax, bx) > maxx || Math.Max(az, bz) < minz || Math.Min(az, bz) > maxz) continue;
                    if (Inside(o, ax, az) || Inside(o, bx, bz)) return true;
                    for (int i = 0, j = n - 1; i < n; j = i, i++)
                        if (Cross(ax, az, bx, bz, o[2 * j], o[2 * j + 1], o[2 * i], o[2 * i + 1])) return true;
                }
            }
            return false;
        }

        static bool Inside(int[] ring, long x, long z)
        {
            bool inside = false;
            int n = ring.Length / 2;
            for (int i = 0, j = n - 1; i < n; j = i, i++)
            {
                long xi = ring[2 * i], zi = ring[2 * i + 1], xj = ring[2 * j], zj = ring[2 * j + 1];
                if ((zi > z) != (zj > z) && x < (double)(xj - xi) * (z - zi) / (zj - zi) + xi) inside = !inside;
            }
            return inside;
        }

        static bool Cross(long ax, long az, long bx, long bz, long cx, long cz, long dx, long dz)
        {
            long d1 = Orient(cx, cz, dx, dz, ax, az), d2 = Orient(cx, cz, dx, dz, bx, bz);
            long d3 = Orient(ax, az, bx, bz, cx, cz), d4 = Orient(ax, az, bx, bz, dx, dz);
            return ((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) && ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0));
        }

        static long Orient(long ax, long az, long bx, long bz, long cx, long cz) => Math.Sign((bx - ax) * (cz - az) - (bz - az) * (cx - ax));
    }
}
