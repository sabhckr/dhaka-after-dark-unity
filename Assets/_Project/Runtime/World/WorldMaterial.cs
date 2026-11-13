// Surface materials of the chunk meshes (D-01, ENVIRONMENT_ROADMAP ED-1). Each vertex colour
// carries its material in the alpha byte: rgb is the sRGB albedo the surface has on average, a is
// the WorldMaterial id. The Dad/World shader reads it, so the mesh format and the apply step stay
// as they were. The ids index the shader's material tables (Shaders/DadWorld.shader,
// "WorldMaterial table"), which pick a texture layer (MaterialLayer) and its wear: keep the order,
// and add new ids at the end.
//
// Texture layers (D-02, ED-2): the slices of the generated texture arrays (Editor/Materials).
// A layer is a tileable PBR set; several materials can share one (Default and Roof use Concrete).
// The shader scales a layer's albedo so its mean is the vertex colour, so builders keep choosing
// colours and the textures add the detail.
//
// World UVs: the shader textures in metres from world position. The floating origin always sits
// on a 500 m cell corner, so it hands the shader its offset wrapped to WrapPeriod (4,000 m) rather
// than whole, which keeps the sum small and precise. Every texture and noise period in world UVs
// must divide 4,000 m (1, 2, 4, 8, 16, 32, 125, 250, 500, 1,000 m...) to stay seamless across
// origin shifts, and so must the shader's sampling lattice (WorldUvLattice).
// No UnityEngine dependency: safe on worker threads.
using System;

namespace Dad.World
{
    public enum WorldMaterial : byte
    {
        Default = 0,
        Soil = 1,
        Grass = 2,
        Water = 3,
        Paving = 4,
        Asphalt = 5,
        Concrete = 6,
        Plaster = 7,
        Roof = 8,
        Brick = 9,
        Paint = 10,
        Metal = 11,
        Rust = 12,
        Glass = 13,
        RoofSheet = 14,
        Tiles = 15,
        Sand = 16,
        Marking = 17,
    }

    /// <summary>Slices of the world texture arrays, in order.</summary>
    public enum MaterialLayer : byte
    {
        Asphalt = 0,
        Concrete = 1,
        Brick = 2,
        Plaster = 3,
        Paint = 4,
        Metal = 5,
        Rust = 6,
        Glass = 7,
        RoofSheet = 8,
        Tiles = 9,
        Paving = 10,
        Soil = 11,
        Grass = 12,
        Sand = 13,
        Water = 14,
        Marking = 15,
    }

    public static class WorldMaterials
    {
        public const int LayerCount = 16;

        /// <summary>Packs an sRGB colour (0xRRGGBB) and a material as RGBA8, r in the low byte, the material in a.</summary>
        public static uint Pack(uint rgb, WorldMaterial m) =>
            ((rgb >> 16) & 0xff) | (rgb & 0xff00) | ((rgb & 0xff) << 16) | ((uint)m << 24);

        /// <summary>The material in a packed vertex colour (its low five bits: the top three are the night mark).</summary>
        public static WorldMaterial Of(uint packed) => (WorldMaterial)((packed >> 24) & 31);

        /// <summary>Night marks, for pieces the far tier's painted facade does not cover (D-13): a room's 1 to 5, the order its lights go off in (1 last), a shop's inside, a sign that lights up.</summary>
        public const uint NightShop = 6, NightSign = 7;

        /// <summary>Marks a packed colour with how it lights up at night. The shader reads it (DadWorld, Frag); the material is unchanged.</summary>
        public static uint Night(uint packed, uint mark) => packed | (mark << 29);

        /// <summary>The night mark in a packed vertex colour.</summary>
        public static uint NightOf(uint packed) => packed >> 29;

        /// <summary>The texture layer a material draws with (the shader's table holds the same).</summary>
        public static MaterialLayer LayerOf(WorldMaterial m) => m switch
        {
            WorldMaterial.Default => MaterialLayer.Concrete,
            WorldMaterial.Soil => MaterialLayer.Soil,
            WorldMaterial.Grass => MaterialLayer.Grass,
            WorldMaterial.Water => MaterialLayer.Water,
            WorldMaterial.Paving => MaterialLayer.Paving,
            WorldMaterial.Asphalt => MaterialLayer.Asphalt,
            WorldMaterial.Concrete => MaterialLayer.Concrete,
            WorldMaterial.Plaster => MaterialLayer.Plaster,
            WorldMaterial.Roof => MaterialLayer.Concrete,
            WorldMaterial.Brick => MaterialLayer.Brick,
            WorldMaterial.Paint => MaterialLayer.Paint,
            WorldMaterial.Metal => MaterialLayer.Metal,
            WorldMaterial.Rust => MaterialLayer.Rust,
            WorldMaterial.Glass => MaterialLayer.Glass,
            WorldMaterial.RoofSheet => MaterialLayer.RoofSheet,
            WorldMaterial.Tiles => MaterialLayer.Tiles,
            WorldMaterial.Sand => MaterialLayer.Sand,
            WorldMaterial.Marking => MaterialLayer.Marking,
            _ => throw new ArgumentOutOfRangeException(nameof(m), m, null),
        };

        public const double WrapPeriod = 4000;

        /// <summary>An origin offset (metres) wrapped into [0, WrapPeriod), for the shader's world UVs.</summary>
        public static double Wrap(double offset)
        {
            double w = Math.IEEERemainder(offset, WrapPeriod);
            return w < 0 ? w + WrapPeriod : w;
        }
    }

    /// <summary>
    /// The shader's sampling lattice for stochastic texturing (D-02), mirrored here so tests can
    /// check it. Triangles of edge vectors e1 = (Size, 0) and e2 = (Size / 2, Size) in world-UV metres:
    /// not equilateral, but the lattice repeats exactly every 4,000 m along both axes (2,000 e1, and
    /// 2,000 e2 - 1,000 e1), so a vertex keeps its random offset across origin shifts. An equilateral
    /// lattice cannot: its rows are sqrt(3)/2 apart, which never divides 4,000 m.
    /// Keep in step with LatticeVertexId in DadWorld.shader.
    /// </summary>
    public static class WorldUvLattice
    {
        public const double Size = 2;
        public const int Period = 2000; // lattice steps per 4,000 m along e1, and along e2

        /// <summary>The lattice cell (i along e1, j along e2) holding a world-UV point.</summary>
        public static (long i, long j) Cell(double u, double v)
        {
            double b = v / Size, a = u / Size - 0.5 * b;
            return ((long)Math.Floor(a), (long)Math.Floor(b));
        }

        /// <summary>The vertex's canonical index in [0, Period²): the same for every copy of it 4,000 m away.</summary>
        public static uint Canonical(long i, long j)
        {
            long jj = Mod(j, Period), k = (j - jj) / Period;
            long ii = Mod(i + k * (Period / 2), Period);
            return (uint)(ii + Period * jj);
        }

        static long Mod(long x, long m) { long r = x % m; return r < 0 ? r + m : r; }
    }
}
