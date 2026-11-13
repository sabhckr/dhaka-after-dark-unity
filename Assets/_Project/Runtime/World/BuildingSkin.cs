// Wall and roof materials, parapets and the rooftop kit's choices for a building (D-06b, ENVIRONMENT_ROADMAP).
// Everything here is decided from the building's own tags and its category (BuildingClass), so every
// piece of a building that the chunk grid cut in two gets the same answer, and the road builder (which
// places the rooftop kit, Rooftops.cs) and the building builder agree on where the roof is.
//
// Walls, in order:
// 1. building:material from the data (3.8 % of the city's buildings): brick, plaster, tin (corrugated
//    sheet), concrete or cement block, glass. Source Tag.
// 2. A labelled rule by category, picked by a hash of the id (Source Rule): homes mostly plastered,
//    some painted and some in bare brick (unfinished upper floors and walls left unrendered); shops and
//    offices concrete or painted, with glass on some of 20 m and more; industry concrete, sheet or brick;
//    institutions brick or plaster; informal structures corrugated sheet, some brick.
// Roofs are all flat (roof:shape is on 169 of 494,978 buildings and is not drawn). A sheet-walled
// building has a sheet roof, and so do most industrial ones; the other flat roofs are a concrete slab,
// white waterproofing, tar or mosaic tiles, by rule.
// Parapets: walk-ups, shop-houses, commercial and institutional buildings on the ground, with a slab
// roof and 5 m or more tall, have a ParapetHeight parapet ParapetThickness thick; the roof is drawn
// ParapetHeight below the top, so the top stays where the data puts it (as M2's facades).
// Colours are this port's choices (sRGB, albedo), like BuildingPalette's.
// No UnityEngine dependency: safe on worker threads.
using System;

namespace Dad.World
{
    public enum SkinSource : byte { Tag, Rule }

    public struct Skin
    {
        public WorldMaterial Wall, Roof;
        public uint WallTone, RoofTone;
        public SkinSource WallSource;
        public bool Parapet;
        /// <summary>Height of the roof surface: the top, or ParapetHeight below it.</summary>
        public double RoofY;
        /// <summary>Does the rooftop kit stand on this roof (Rooftops.cs)?</summary>
        public bool Kit;
    }

    public static class BuildingSkin
    {
        public const double ParapetHeight = ChunkBuildings.ParapetHeight, ParapetThickness = 0.15, MinParapetBuilding = 5;

        static readonly uint[] Paint = { 0xd2bb78, 0xc8917c, 0x8fabb3, 0xa6b58e, 0xd6cdbb, 0xb39fb0 };
        static readonly uint[] Brick = { 0x8a4a35, 0x96573d, 0x7b4536 };
        static readonly uint[] Concrete = { 0x9a9890, 0x8c8a83, 0xa6a399 };
        static readonly uint[] Sheet = { 0x808a8b, 0x86503c, 0x50695a, 0x5d6e84 };
        static readonly uint[] Glass = { 0x45606b, 0x34505a };
        static readonly uint[] Slab = { 0x8a857b, 0x7f7a70, 0x958f84 };
        const uint WhiteCoat = 0xc9c7bf, Tar = 0x3c3a37;
        /// <summary>The parapet's coping: cement, weathered.</summary>
        public const uint Coping = 0x8e8a82;
        static readonly uint[] Mosaic = { 0xa08b78, 0x9c9a91 };

        /// <summary>A hash of the id and a salt in [0, 1), the same on every piece of the building.</summary>
        public static double Hash(long id, ulong salt)
        {
            ulong z = (ulong)id * 0x9E3779B97F4A7C15UL + salt * 0xD1B54A32D192ED03UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            z ^= z >> 31;
            return (z >> 11) * (1.0 / 9007199254740992.0);
        }

        static uint Pick(uint[] tones, long id, ulong salt) => tones[Math.Min(tones.Length - 1, (int)(Hash(id, salt) * tones.Length))];

        /// <summary>The wall material named by building:material, if it names one this port draws.</summary>
        public static bool FromTag(string material, out WorldMaterial m)
        {
            switch (material)
            {
                case "brick": case "masonry": case "exposed brick": case "brick;tin": m = WorldMaterial.Brick; return true;
                case "plaster": m = WorldMaterial.Plaster; return true;
                case "tin": case "tinshed": case "tin shed": case "metal": m = WorldMaterial.RoofSheet; return true;
                case "concrete": case "cement_block": case "stone": m = WorldMaterial.Concrete; return true;
                case "glass": m = WorldMaterial.Glass; return true;
                default: m = default; return false;
            }
        }

        static WorldMaterial WallRule(BuildingCategory c, double r, double height) => c switch
        {
            BuildingCategory.Residential or BuildingCategory.MixedUse or BuildingCategory.Other =>
                r < 0.60 ? WorldMaterial.Plaster : r < 0.85 ? WorldMaterial.Paint : WorldMaterial.Brick,
            BuildingCategory.Commercial => height >= 20
                ? (r < 0.35 ? WorldMaterial.Glass : r < 0.75 ? WorldMaterial.Concrete : WorldMaterial.Paint)
                : (r < 0.6 ? WorldMaterial.Concrete : WorldMaterial.Paint),
            BuildingCategory.Industrial => r < 0.5 ? WorldMaterial.Concrete : r < 0.8 ? WorldMaterial.RoofSheet : WorldMaterial.Brick,
            BuildingCategory.Institutional => r < 0.4 ? WorldMaterial.Brick : r < 0.8 ? WorldMaterial.Plaster : WorldMaterial.Paint,
            BuildingCategory.Informal => r < 0.8 ? WorldMaterial.RoofSheet : WorldMaterial.Brick,
            BuildingCategory.Special => WorldMaterial.Concrete,
            _ => WorldMaterial.Plaster, // religious
        };

        public static uint ToneOf(WorldMaterial m, long id) => m switch
        {
            WorldMaterial.Paint => Pick(Paint, id, 3),
            WorldMaterial.Brick => Pick(Brick, id, 3),
            WorldMaterial.Concrete => Pick(Concrete, id, 3),
            WorldMaterial.RoofSheet => Pick(Sheet, id, 3),
            WorldMaterial.Glass => Pick(Glass, id, 3),
            _ => BuildingPalette.Tone(id),
        };

        /// <summary>The building's skin (see the file header). pl is its placement; facaded: it has M2's geometry facade.</summary>
        public static Skin Of(Building b, BuildingCategory c, BuildingRules.Placement pl, bool facaded)
        {
            var s = new Skin();
            if (FromTag(b.Material, out var tagged)) { s.Wall = tagged; s.WallSource = SkinSource.Tag; }
            else { s.Wall = WallRule(c, Hash(b.Id, 1), pl.Top); s.WallSource = SkinSource.Rule; }
            // M2's facades are plaster, as built.
            if (facaded) { s.Wall = WorldMaterial.Plaster; s.WallSource = SkinSource.Rule; }
            s.WallTone = ToneOf(s.Wall, b.Id);

            double r = Hash(b.Id, 2);
            if (s.Wall == WorldMaterial.RoofSheet || c == BuildingCategory.Informal || c == BuildingCategory.Industrial && r < 0.6)
            {
                s.Roof = WorldMaterial.RoofSheet;
                s.RoofTone = s.Wall == WorldMaterial.RoofSheet ? s.WallTone : Pick(Sheet, b.Id, 4);
            }
            else if (r < 0.60) { s.Roof = WorldMaterial.Roof; s.RoofTone = Pick(Slab, b.Id, 4); }
            else if (r < 0.72) { s.Roof = WorldMaterial.Paint; s.RoofTone = WhiteCoat; }
            else if (r < 0.85) { s.Roof = WorldMaterial.Roof; s.RoofTone = Tar; }
            else { s.Roof = WorldMaterial.Tiles; s.RoofTone = Pick(Mosaic, b.Id, 4); }

            var family = BuildingClass.FamilyOf(c);
            bool slab = s.Roof != WorldMaterial.RoofSheet;
            s.Parapet = !facaded && slab && pl.Base <= 0 && pl.Top - pl.Base >= MinParapetBuilding &&
                        (family == FacadeFamily.WalkUp || family == FacadeFamily.ShopHouse || family == FacadeFamily.Commercial || family == FacadeFamily.Institutional);
            s.RoofY = s.Parapet || facaded ? pl.Top - ParapetHeight : pl.Top;
            // The kit stands on the slab roofs of buildings people live and work in, two floors or more.
            s.Kit = slab && pl.Base <= 0 && pl.Top >= 6 && (s.Parapet || facaded);
            return s;
        }
    }
}
