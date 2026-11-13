// The 16 texture layers of the world material library (D-02, ENVIRONMENT_ROADMAP ED-2), one
// recipe each. A recipe gives, for a point of the tile, its linear albedo, height (metres),
// smoothness, metallic and crack amount. MaterialLibrary turns heights into normals, ambient
// occlusion and a cavity mask. All recipes are original and procedural, built only from TileNoise.
//
// Physical scale: a tile is Period metres square (it must divide 4,000 m, WorldMaterial.cs), so
// sizes below are in metres: brick courses, slab joints, crack widths, corrugation pitch.
// Colours are sRGB hex converted to linear. The shader rescales each layer so its mean is the
// mesh's vertex colour; a recipe's own colours set the contrast inside the layer, and the look in
// the material sheet.
// Layers with a Scan take their pixels from that Poly Haven set instead (MaterialLibrary.Bake); their
// recipe is then only kept for the tiling test.
// Wear that varies across the city (dirt, fading, repairs) is the shader's job, from the macro
// texture and the cavity mask, so a layer here is the material in middling condition.
using System;
using Dad.World;
using static Dad.EditorTools.Materials.TileNoise;

namespace Dad.EditorTools.Materials
{
    public struct Texel
    {
        public float R, G, B;     // linear albedo
        public float H;           // height, metres
        public float Smooth, Metal;
        public float Crack;       // 0..1, cracks and open joints for the cavity mask
    }

    public delegate void Recipe(float s, float t, ref Texel o);

    public sealed class LayerRecipe
    {
        public MaterialLayer Layer;
        public float Period;      // metres per tile
        public float Relief;      // typical relief depth, metres: scales occlusion and cavities
        public Recipe Pixel;
        // Poly Haven id (CC0) of a scanned set that replaces Pixel, owner 2026-11-11 (BUILDING_ASSETS_AUDIT.md).
        // Period is then the scan's real size rounded to a divisor of 4,000 m.
        public string Scan;
    }

    public static class LayerRecipes
    {
        public static readonly LayerRecipe[] All =
        {
            new LayerRecipe { Layer = MaterialLayer.Asphalt, Period = 2, Scan = "road_damaged", Relief = 0.002f, Pixel = Asphalt },
            new LayerRecipe { Layer = MaterialLayer.Concrete, Period = 2.5f, Scan = "concrete_wall_008", Relief = 0.0012f, Pixel = Concrete },
            new LayerRecipe { Layer = MaterialLayer.Brick, Period = 1.25f, Scan = "red_brick_plaster_patch_02", Relief = 0.006f, Pixel = Brick },
            new LayerRecipe { Layer = MaterialLayer.Plaster, Period = 2, Scan = "worn_plaster_wall", Relief = 0.0015f, Pixel = Plaster },
            new LayerRecipe { Layer = MaterialLayer.Paint, Period = 2, Scan = "painted_concrete", Relief = 0.0008f, Pixel = Paint },
            new LayerRecipe { Layer = MaterialLayer.Metal, Period = 0.5f, Scan = "metal_plate", Relief = 0.0005f, Pixel = Metal },
            new LayerRecipe { Layer = MaterialLayer.Rust, Period = 1, Scan = "rusty_metal_02", Relief = 0.0012f, Pixel = Rust },
            new LayerRecipe { Layer = MaterialLayer.Glass, Period = 2, Relief = 0.0003f, Pixel = Glass },
            new LayerRecipe { Layer = MaterialLayer.RoofSheet, Period = 2, Scan = "rusty_corrugated_iron", Relief = 0.009f, Pixel = RoofSheet },
            new LayerRecipe { Layer = MaterialLayer.Tiles, Period = 2, Scan = "terrazzo_tiles", Relief = 0.002f, Pixel = Tiles },
            new LayerRecipe { Layer = MaterialLayer.Paving, Period = 2, Scan = "concrete_pavers_03", Relief = 0.005f, Pixel = Paving },
            new LayerRecipe { Layer = MaterialLayer.Soil, Period = 2, Scan = "dirt", Relief = 0.004f, Pixel = Soil },
            new LayerRecipe { Layer = MaterialLayer.Grass, Period = 2, Relief = 0.008f, Pixel = Grass },
            new LayerRecipe { Layer = MaterialLayer.Sand, Period = 4, Relief = 0.003f, Pixel = Sand },
            new LayerRecipe { Layer = MaterialLayer.Water, Period = 8, Relief = 0.002f, Pixel = Water },
            new LayerRecipe { Layer = MaterialLayer.Marking, Period = 2, Relief = 0.001f, Pixel = Marking },
        };

        // ---- colour helpers ----

        public struct Rgb
        {
            public float R, G, B;
            public Rgb(float r, float g, float b) { R = r; G = g; B = b; }
            public static Rgb operator *(Rgb a, float k) => new Rgb(a.R * k, a.G * k, a.B * k);
            public static Rgb Lerp(Rgb a, Rgb b, float t) => new Rgb(a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t);
        }

        public static float SrgbToLinear(float c) => c <= 0.04045f ? c / 12.92f : MathF.Pow((c + 0.055f) / 1.055f, 2.4f);

        public static Rgb Hex(uint rgb) => new Rgb(
            SrgbToLinear(((rgb >> 16) & 0xff) / 255f), SrgbToLinear(((rgb >> 8) & 0xff) / 255f), SrgbToLinear((rgb & 0xff) / 255f));

        static void Set(ref Texel o, Rgb c) { o.R = c.R; o.G = c.G; o.B = c.B; }

        /// <summary>A crack line from Worley cell edges: 1 on the line, 0 away from it. Half-width and
        /// texel in metres; the line is never thinner than about a texel, so it stays antialiased.</summary>
        static float CrackLine(Cell w, float cellMetres, float halfWidth, float texel)
        {
            float edge = (w.F2 - w.F1) * 0.5f * cellMetres; // ~ distance to the cell edge, metres
            float hw = MathF.Max(halfWidth, 0.6f * texel);
            return Smoothstep(hw * 1.6f, hw * 0.4f, edge) * MathF.Min(1f, halfWidth / hw + 0.25f);
        }

        // Texel size of a 4 m layer at 512 pixels, for crack widths (the recipes do not know the
        // resolution; at lower resolutions lines just get relatively thinner).
        const float Texel4 = 4f / 512;

        // ---- recipes ----

        // Asphalt, 4 m. Dark binder with lighter aggregate showing as speckle, broad stains, and a
        // network of thin cracks in some areas.
        static void Asphalt(float s, float t, ref Texel o)
        {
            var stones = Worley(s, t, 200, 11);
            float stone = Smoothstep(0.42f, 0.28f, stones.F1) * (Unit(stones.Id) < 0.7f ? 1 : 0);
            float tone = 0.08f + 0.14f * Unit(Hash(stones.Id));
            float grain = Perlin(s, t, 256, 12);
            float stain = Fbm(s, t, 4, 4, 13);
            float binder = 0.030f * (1 + 0.25f * grain);
            float lum = binder + (tone - binder) * stone;
            lum *= 1 + 0.16f * stain;
            Set(ref o, new Rgb(lum, lum * 0.99f, lum * 0.96f));
            o.H = 0.0015f * stone * (1 - stones.F1 / 0.42f) + 0.0003f * grain;
            o.Smooth = 0.32f - 0.14f * stone + 0.05f * Saturate(-stain);
            // Cracks only where the crack mask is up; the lattice is warped so lines wander.
            float region = Smoothstep(0.1f, 0.35f, Fbm(s, t, 2, 3, 14));
            float ws = s + 0.025f * Perlin(s, t, 8, 15), wt = t + 0.025f * Perlin(s, t, 8, 16);
            float crack = CrackLine(Worley(ws, wt, 3, 17), 4f / 3, 0.003f, Texel4) * region;
            o.R *= 1 - 0.65f * crack; o.G *= 1 - 0.65f * crack; o.B *= 1 - 0.65f * crack;
            o.H -= 0.003f * crack;
            o.Crack = crack;
        }

        // Concrete, 4 m. Light grey with soft mottling, small pores, stains and rare hairline cracks.
        static void Concrete(float s, float t, ref Texel o)
        {
            float mottle = 0.08f * Fbm(s, t, 6, 5, 21) + 0.04f * Perlin(s, t, 64, 22) + 0.03f * Perlin(s, t, 200, 23);
            float stain = Smoothstep(0.2f, 0.6f, Fbm(s, t, 2, 4, 24));
            var pores = Worley(s, t, 96, 25);
            float pore = Smoothstep(0.12f, 0.05f, pores.F1) * (Unit(pores.Id) < 0.3f ? 1 : 0);
            var c = Hex(0x8f8b84) * ((1 + mottle) * (1 - 0.15f * stain) * (1 - 0.45f * pore));
            Set(ref o, c);
            o.H = 0.0004f * Fbm(s, t, 16, 3, 26) + 0.00015f * Perlin(s, t, 128, 27) - 0.0006f * pore;
            o.Smooth = 0.24f + 0.05f * Perlin(s, t, 8, 28) - 0.06f * stain;
            float region = Smoothstep(0.3f, 0.5f, Fbm(s, t, 2, 3, 29));
            float ws = s + 0.02f * Perlin(s, t, 6, 30), wt = t + 0.02f * Perlin(s, t, 6, 31);
            float crack = CrackLine(Worley(ws, wt, 2, 32), 2f, 0.001f, Texel4) * region;
            o.R *= 1 - 0.4f * crack; o.G *= 1 - 0.4f * crack; o.B *= 1 - 0.4f * crack;
            o.H -= 0.0015f * crack;
            o.Crack = crack;
        }

        // Brick, 2 m: running bond of 8 bricks by 24 courses (250 x 83 mm units, 10 mm joints), the
        // orange-red of Bangladeshi kiln bricks, some over-burnt, with recessed grey mortar.
        static void Brick(float s, float t, ref Texel o)
        {
            var u = Units(s, t, 8, 24, 0.5f, 2f, 41);
            float d = MathF.Min(u.Dx, u.Dy) + 0.0025f * Perlin(s, t, 64, 96, 42);
            float mortar = 1 - Smoothstep(0.0035f, 0.0065f, d);
            float h1 = Unit(u.Id), h2 = Unit(Hash(u.Id));
            var brick = Hex(0xa65a3e) * (0.8f + 0.4f * h1);
            if (h2 < 0.12f) brick = Rgb.Lerp(brick, Hex(0x6e3a2a), 0.7f);
            else if (h2 > 0.9f) brick = Rgb.Lerp(brick, Hex(0xb8784e), 0.6f);
            brick *= 1 + 0.08f * Perlin(s, t, 48, 43) + 0.05f * Perlin(s, t, 160, 44);
            var mix = Hex(0x9d978b) * (1 + 0.1f * Perlin(s, t, 64, 45));
            Set(ref o, Rgb.Lerp(brick, mix, mortar));
            float bevel = 1 - Smoothstep(0.005f, 0.012f, d);
            o.H = (1 - mortar) * (0.0004f * Perlin(s, t, 40, 46) - 0.001f * bevel) - 0.006f * mortar;
            o.Smooth = 0.18f - 0.1f * mortar;
            o.Crack = 0;
        }

        // Plaster, 4 m: cement-sand render with trowel marks, rain streaks running down, patches
        // where the top coat has peeled to the grey layer under it, and rare hairline cracks.
        static void Plaster(float s, float t, ref Texel o)
        {
            float trowel = 0.03f * Perlin(s, t, 16, 6, 51) + 0.04f * Fbm(s, t, 6, 4, 52) + 0.02f * Perlin(s, t, 128, 53);
            float streak = Smoothstep(0.25f, 0.7f, Perlin(s, t, 64, 2, 54)) * (0.5f + 0.5f * Fbm(s, t, 4, 3, 55));
            float pm = Fbm(s, t, 5, 4, 56) + 0.15f * Perlin(s, t, 40, 57);
            // Damage clusters: about half of the wall is sound, the rest has its patches.
            float zone = Smoothstep(-0.1f, 0.2f, Fbm(s, t, 2, 2, 64));
            float peeled = Smoothstep(0.42f, 0.46f, pm) * zone;
            float rim = Smoothstep(0.38f, 0.42f, pm) * zone * (1 - peeled);
            var top = Hex(0xb5ad9c) * ((1 + trowel) * (1 - 0.18f * streak) * (1 + 0.08f * rim));
            var under = Hex(0x8e877a) * (1 + 0.5f * trowel);
            Set(ref o, Rgb.Lerp(top, under, peeled));
            o.H = 0.0003f * Perlin(s, t, 24, 58) - 0.0015f * peeled;
            o.Smooth = 0.2f + 0.04f * Perlin(s, t, 12, 59) - 0.05f * peeled;
            float region = Smoothstep(0.35f, 0.55f, Fbm(s, t, 2, 3, 60));
            float ws = s + 0.02f * Perlin(s, t, 6, 61), wt = t + 0.02f * Perlin(s, t, 6, 62);
            float crack = CrackLine(Worley(ws, wt, 3, 63), 4f / 3, 0.001f, Texel4) * region;
            o.R *= 1 - 0.35f * crack; o.G *= 1 - 0.35f * crack; o.B *= 1 - 0.35f * crack;
            o.H -= 0.001f * crack;
            o.Crack = crack;
        }

        // Paint, 4 m: an off-white painted wall, roller texture, and chips showing the render under it.
        static void Paint(float s, float t, ref Texel o)
        {
            float roll = 0.02f * Perlin(s, t, 8, 64, 71) + 0.015f * Perlin(s, t, 200, 72);
            // Flakes: ragged islands of a fine noise, only where a broad mask says the wall is old.
            float flake = Fbm(s, t, 10, 4, 73) + 0.35f * Perlin(s, t, 80, 76);
            float chip = Smoothstep(0.5f, 0.56f, flake) * Smoothstep(0.05f, 0.35f, Fbm(s, t, 3, 3, 74));
            Set(ref o, Rgb.Lerp(Hex(0xd6d0c2) * (1 + roll), Hex(0x8e877a), chip));
            o.H = 0.0001f * Perlin(s, t, 64, 75) - 0.0004f * chip;
            o.Smooth = 0.45f - 0.27f * chip;
            o.Crack = 0;
        }

        // Metal, 2 m: brushed steel with grime blotches.
        static void Metal(float s, float t, ref Texel o)
        {
            float brushed = 0.05f * Perlin(s, t, 2, 256, 81) + 0.03f * Perlin(s, t, 3, 200, 82);
            float grime = Smoothstep(0.1f, 0.5f, Fbm(s, t, 4, 4, 83));
            Set(ref o, Hex(0xb8b9b8) * ((1 + brushed) * (1 - 0.2f * grime)));
            o.H = 0.0002f * Perlin(s, t, 16, 84);
            o.Smooth = 0.6f + 0.06f * brushed / 0.08f - 0.25f * grime;
            o.Metal = 1 - 0.3f * grime;
            o.Crack = 0;
        }

        // Rust, 2 m: corroded steel in browns and orange, pitted, with patches of metal left.
        static void Rust(float s, float t, ref Texel o)
        {
            float n1 = Fbm(s, t, 6, 5, 91), n2 = Fbm(s, t, 24, 3, 92);
            var c = Rgb.Lerp(Hex(0x4e2a1c), Hex(0x86452a), Smoothstep(-0.5f, 0.1f, n1));
            c = Rgb.Lerp(c, Hex(0xa8642e), Smoothstep(0.1f, 0.5f, n1 + 0.3f * n2));
            c = Rgb.Lerp(c, Hex(0xb38345), Smoothstep(0.45f, 0.75f, n2));
            var pits = Worley(s, t, 80, 93);
            float pit = Smoothstep(0.25f, 0.1f, pits.F1) * (Unit(pits.Id) < 0.4f ? 1 : 0);
            float bare = Smoothstep(-0.35f, -0.55f, n1);
            c = Rgb.Lerp(c * (1 - 0.4f * pit), Hex(0x6d6b68), bare);
            Set(ref o, c);
            o.H = 0.0006f * n2 - 0.0005f * pit;
            o.Smooth = 0.1f + 0.25f * bare;
            o.Metal = 0.8f * bare;
            o.Crack = 0;
        }

        // Glass, 2 m: window glass as seen from the street, dark and glossy, with smudges and dust
        // and a slight waviness that bends reflections. Opaque: the world shader has no transparency.
        static void Glass(float s, float t, ref Texel o)
        {
            float smudge = Smoothstep(0.0f, 0.5f, Fbm(s, t, 6, 4, 101));
            var dust = Worley(s, t, 150, 102);
            float speck = Smoothstep(0.1f, 0.04f, dust.F1) * (Unit(dust.Id) < 0.3f ? 1 : 0);
            Set(ref o, Hex(0x2f3a42) * ((1 + 0.15f * smudge) * (1 + 0.6f * speck)));
            o.H = 0.00015f * Perlin(s, t, 3, 103);
            o.Smooth = 0.92f - 0.25f * smudge - 0.3f * speck;
            o.Crack = 0;
        }

        // Corrugated roof sheet, 2 m: galvanised iron at a 76.9 mm pitch (26 waves a tile) in three
        // sheets of different age, rusting along vertical streaks and in the troughs.
        static void RoofSheet(float s, float t, ref Texel o)
        {
            float wave = MathF.Sin(6.2831853f * 26 * s);
            int sheet = (int)MathF.Floor(s * 3);
            float age = Unit(Hash(Wrap(sheet, 3), 0, 111));
            var galv = Hex(0x9b9e9c) * (1 + 0.06f * Perlin(s, t, 60, 112) + 0.04f * Fbm(s, t, 4, 3, 113));
            float streak = Smoothstep(0.2f, 0.7f, Perlin(s, t, 48, 2, 114));
            float trough = Smoothstep(-0.5f, -0.9f, wave);
            float rust = Saturate(age * 0.8f + 0.5f * Fbm(s, t, 4, 4, 115) + 0.35f * streak + 0.15f * trough - 0.35f);
            var rustCol = Rgb.Lerp(Hex(0x5e3222), Hex(0x96512c), 0.5f + 0.5f * Perlin(s, t, 32, 116));
            float seam = Smoothstep(0.004f, 0.0f, MathF.Abs(s * 3 - MathF.Round(s * 3)) * 2f / 3);
            Set(ref o, Rgb.Lerp(galv, rustCol, rust) * (1 - 0.25f * seam));
            o.H = 0.0045f * wave + 0.002f * seam;
            o.Smooth = 0.42f - 0.3f * rust;
            o.Metal = 0.75f * (1 - rust);
            o.Crack = 0;
        }

        // Ceramic floor tiles, 2 m: 8 by 8 tiles of 250 mm, 4 mm grout, the odd cracked tile.
        static void Tiles(float s, float t, ref Texel o)
        {
            var u = Units(s, t, 8, 8, 0f, 2f, 121);
            float d = MathF.Min(u.Dx, u.Dy);
            float grout = 1 - Smoothstep(0.0015f, 0.0035f, d);
            float h1 = Unit(u.Id), h2 = Unit(Hash(u.Id));
            var tile = Hex(0xcfc6b4) * (0.94f + 0.12f * h1 + 0.03f * Perlin(s, t, 96, 122) + 0.04f * Perlin(s, t, 16, 123));
            float crack = 0;
            if (h2 < 0.06f)
            {
                float a = Unit(Hash(u.Id + 7)) * 3.14159f;
                float line = MathF.Abs((u.U - 0.5f) * MathF.Cos(a) + (u.V - 0.5f) * MathF.Sin(a)) * 0.25f;
                crack = Smoothstep(0.0015f, 0.0003f, line + 0.0008f * Perlin(s, t, 128, 124)) * (1 - grout);
            }
            Set(ref o, Rgb.Lerp(tile * (1 - 0.5f * crack), Hex(0x8a857b) * (1 + 0.1f * Perlin(s, t, 64, 125)), grout));
            float bevel = 1 - Smoothstep(0.002f, 0.006f, d);
            o.H = -0.0006f * bevel * (1 - grout) - 0.002f * grout - 0.0005f * crack;
            o.Smooth = 0.72f - 0.6f * grout - 0.3f * crack;
            o.Crack = crack;
        }

        // Footpath paving, 2 m: 400 mm concrete slabs, 8 mm joints, uneven and chipped, some broken.
        static void Paving(float s, float t, ref Texel o)
        {
            var u = Units(s, t, 5, 5, 0f, 2f, 131);
            float d = MathF.Min(u.Dx, u.Dy) + 0.003f * Perlin(s, t, 80, 132);
            float joint = 1 - Smoothstep(0.003f, 0.006f, d);
            float h1 = Unit(u.Id), h2 = Unit(Hash(u.Id)), h3 = Unit(Hash(u.Id + 3));
            var slab = Hex(0x8a8378) * ((0.85f + 0.3f * h1) * (1 + 0.06f * Fbm(s, t, 16, 3, 133) + 0.04f * Perlin(s, t, 150, 134)));
            float crack = 0, side = 0;
            if (h3 < 0.1f)
            {
                float a = Unit(Hash(u.Id + 11)) * 3.14159f;
                float sd = (u.U - 0.5f) * MathF.Cos(a) + (u.V - 0.5f) * MathF.Sin(a) + 0.05f * Perlin(s, t, 40, 135);
                crack = Smoothstep(0.012f, 0.002f, MathF.Abs(sd)) * (1 - joint);
                side = sd > 0 ? 1 : 0;
            }
            slab *= (1 - 0.08f * side) * (1 - 0.5f * crack);
            Set(ref o, Rgb.Lerp(slab, Hex(0x5a554c), joint));
            o.H = (1 - joint) * ((h2 - 0.5f) * 0.004f - 0.0015f * side) - 0.006f * joint - 0.002f * crack;
            o.Smooth = 0.2f - 0.08f * joint;
            o.Crack = Math.Max(crack, 0.5f * joint);
        }

        // Soil, 4 m: bare earth, damp and dry, pebbles, and dried mud cracks in the dry parts.
        static void Soil(float s, float t, ref Texel o)
        {
            float n = Fbm(s, t, 4, 5, 141);
            var c = Hex(0x6a6153);
            c = Rgb.Lerp(c, Hex(0x4d4539), Smoothstep(-0.15f, -0.45f, n));
            c = Rgb.Lerp(c, Hex(0x8a7d68), Smoothstep(0.2f, 0.5f, n));
            c *= 1 + 0.08f * Perlin(s, t, 160, 142);
            var peb = Worley(s, t, 48, 143);
            float pebble = Smoothstep(0.3f, 0.18f, peb.F1) * (Unit(peb.Id) < 0.12f ? 1 : 0);
            c = Rgb.Lerp(c, Hex(0x7d776d) * (0.85f + 0.3f * Unit(Hash(peb.Id))), 0.8f * pebble);
            float dry = Smoothstep(0.1f, 0.35f, n);
            float ws = s + 0.02f * Perlin(s, t, 12, 144), wt = t + 0.02f * Perlin(s, t, 12, 145);
            float crack = CrackLine(Worley(ws, wt, 10, 146), 0.4f, 0.003f, Texel4) * dry;
            Set(ref o, c * (1 - 0.45f * crack));
            o.H = 0.002f * Fbm(s, t, 8, 4, 147) + 0.003f * pebble * (1 - peb.F1 / 0.3f) - 0.004f * crack;
            o.Smooth = 0.08f + 0.1f * Smoothstep(-0.15f, -0.45f, n);
            o.Crack = crack;
        }

        // Grass, 2 m: short urban grass in clumps, greener and drier patches, worn bare spots.
        static void Grass(float s, float t, ref Texel o)
        {
            float patch = Fbm(s, t, 3, 4, 151);
            float dryness = Smoothstep(0.15f, 0.45f, patch);
            float bare = Smoothstep(-0.38f, -0.55f, patch);
            // Blades as crossing strokes (noise stretched one way, then the other), in small clumps.
            float blade = 0.5f + 0.5f * (0.6f * Perlin(s, t, 256, 96, 152) + 0.4f * Perlin(s, t, 96, 256, 157));
            float tip = 0.78f + 0.35f * blade + 0.08f * Perlin(s, t, 200, 153);
            var clumps = Worley(s, t, 48, 154);
            float gap = Smoothstep(0.3f, 0.8f, clumps.F1);
            var c = Rgb.Lerp(Hex(0x4c6a2c), Hex(0x6b8a3a), 0.5f + 0.5f * Perlin(s, t, 12, 155));
            c = Rgb.Lerp(c, Hex(0x9a8f52), dryness);
            c = c * (tip * (1 - 0.18f * gap));
            c = Rgb.Lerp(c, Hex(0x6a5d48) * (1 + 0.1f * Perlin(s, t, 96, 156)), bare);
            Set(ref o, c);
            o.H = (1 - bare) * (0.004f * (1 - Smoothstep(0f, 0.9f, clumps.F1)) + 0.004f * (blade - 0.5f));
            o.Smooth = 0.18f - 0.1f * bare;
            o.Crack = 0;
        }

        // Sand, 4 m: fine pale grain with wind ripples and darker grains.
        static void Sand(float s, float t, ref Texel o)
        {
            float phase = 6.2831853f * (10 * s + 2 * t) + 1.5f * Fbm(s, t, 3, 3, 161);
            float ripple = MathF.Sin(phase);
            var grains = Worley(s, t, 180, 162);
            float dark = Smoothstep(0.3f, 0.15f, grains.F1) * (Unit(grains.Id) < 0.1f ? 1 : 0);
            Set(ref o, Hex(0xb9a888) * ((1 + 0.07f * Perlin(s, t, 256, 163)) * (1 - 0.3f * dark) * (1 - 0.06f * Saturate(-ripple))));
            o.H = 0.003f * ripple + 0.0003f * Perlin(s, t, 128, 164);
            o.Smooth = 0.1f;
            o.Crack = 0;
        }

        // Water, 8 m: murky canal water, nearly uniform, with ripples in several directions.
        static void Water(float s, float t, ref Texel o)
        {
            const float Tau = 6.2831853f;
            float h = 0.0015f * MathF.Sin(Tau * (3 * s + t))
                    + 0.0010f * MathF.Sin(Tau * (-2 * s + 3 * t) + 1.3f)
                    + 0.0006f * MathF.Sin(Tau * (5 * s - 2 * t) + 2.1f)
                    + 0.0004f * MathF.Sin(Tau * (s + 6 * t) + 0.4f)
                    + 0.0006f * Fbm(s, t, 16, 3, 171);
            float scum = Smoothstep(0.45f, 0.65f, Fbm(s, t, 6, 4, 172));
            Set(ref o, Rgb.Lerp(Hex(0x34463f) * (1 + 0.04f * Fbm(s, t, 4, 3, 173)), Hex(0x4f5a49), scum * 0.6f));
            o.H = h;
            o.Smooth = 0.9f - 0.3f * scum;
            o.Crack = 0;
        }

        // Road marking paint, 2 m: white thermoplastic, worn through to asphalt in places.
        static void Marking(float s, float t, ref Texel o)
        {
            float wv = Fbm(s, t, 6, 4, 181) + 0.6f * Perlin(s, t, 128, 182) + 0.3f * Perlin(s, t, 48, 186);
            float worn = Smoothstep(0.5f, 0.65f, wv);
            float dirt = Smoothstep(0f, 0.5f, Fbm(s, t, 3, 3, 183));
            var paint = Hex(0xe6e3da) * ((1 + 0.03f * Perlin(s, t, 128, 184)) * (1 - 0.12f * dirt));
            var asphalt = Hex(0x3a3a38) * (1 + 0.25f * Perlin(s, t, 256, 185));
            Set(ref o, Rgb.Lerp(paint, asphalt, worn));
            o.H = 0.0008f * (1 - worn);
            o.Smooth = 0.4f - 0.15f * worn;
            o.Crack = 0;
        }
    }
}
