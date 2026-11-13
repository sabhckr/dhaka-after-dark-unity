// The paint shop (2026-11-06): exactly twenty base colours, each with its own finish, and which of them each kind of vehicle comes in. Grounded street colours, muted, not showroom ones. No engine
// here, so tests read the same table. The colours are the owner's list; the numbers are estimates of how each paint looks and behaves, not measurements of any maker's paint.
namespace Dad.Traffic
{
    public enum PaintFinish : byte { Solid, Metallic, Pearl, Matte }

    public sealed class VehiclePaint
    {
        public string Name;
        /// <summary>sRGB, 0 to 255.</summary>
        public byte R, G, B;
        public PaintFinish Finish;
        /// <summary>How smooth the clear coat is, how metallic the base is, how much flake shows, and how far the colour shifts at a glancing angle.</summary>
        public double Smoothness, Metallic, Flake, Pearl;

        VehiclePaint(string name, byte r, byte g, byte b, PaintFinish finish, double smoothness, double metallic, double flake, double pearl)
        { Name = name; R = r; G = g; B = b; Finish = finish; Smoothness = smoothness; Metallic = metallic; Flake = flake; Pearl = pearl; }

        static VehiclePaint Solid(string n, byte r, byte g, byte b, double s = 0.6) => new VehiclePaint(n, r, g, b, PaintFinish.Solid, s, 0.04, 0, 0);
        static VehiclePaint Metal(string n, byte r, byte g, byte b, double s = 0.64, double m = 0.42, double f = 0.3) => new VehiclePaint(n, r, g, b, PaintFinish.Metallic, s, m, f, 0);
        static VehiclePaint Pearly(string n, byte r, byte g, byte b, double s = 0.66) => new VehiclePaint(n, r, g, b, PaintFinish.Pearl, s, 0.12, 0.1, 0.5);
        static VehiclePaint Flat(string n, byte r, byte g, byte b) => new VehiclePaint(n, r, g, b, PaintFinish.Matte, 0.36, 0.02, 0, 0);

        /// <summary>The twenty, in the owner's order. Nothing else is a base colour.</summary>
        public static readonly VehiclePaint[] All =
        {
            Metal("Deep Obsidian Black", 15, 16, 19, 0.68, 0.3, 0.2),
            Solid("Charcoal Black", 38, 39, 42, 0.56),
            Metal("Midnight Navy", 21, 30, 52),
            Metal("Muted Steel Blue", 74, 92, 110, 0.6, 0.36, 0.3),
            Metal("Dark Burgundy", 68, 21, 31, 0.62, 0.34, 0.26),
            Pearly("Deep Crimson", 102, 24, 30, 0.62),
            Solid("Brick Red", 118, 54, 44, 0.48),
            Solid("Forest Green", 36, 58, 44, 0.54),
            Flat("Olive Green", 78, 79, 50),
            Metal("Muted British Racing Green", 27, 52, 41, 0.62, 0.32, 0.24),
            Pearly("Pearl Off-White", 212, 208, 197),
            Solid("Dirty Ivory", 188, 178, 156, 0.44),
            Metal("Arctic Silver", 166, 170, 174, 0.64, 0.56, 0.4),
            Metal("Metallic Aluminium", 138, 140, 143, 0.58, 0.62, 0.44),
            Metal("Graphite Grey", 72, 74, 78, 0.6, 0.44, 0.34),
            Metal("Gunmetal Grey", 53, 57, 61, 0.58, 0.46, 0.3),
            Metal("Champagne Beige", 172, 157, 129, 0.6, 0.4, 0.36),
            Metal("Bronze Brown", 92, 69, 47, 0.56, 0.42, 0.3),
            Metal("Muted Copper", 124, 82, 59, 0.56, 0.46, 0.32),
            Metal("Dark Plum", 52, 29, 48, 0.62, 0.34, 0.26),
        };
    }

    /// <summary>What a vehicle wears: one base colour, a shade of it, a second tone for a two-tone scheme, and how worn it is.</summary>
    public struct PaintJob
    {
        /// <summary>The base colour's number in VehiclePaint.All, and of the second tone, -1 for none.</summary>
        public int Base, Second;
        /// <summary>Lightness of this one against the base, about 1; the second tone starts at this share of the body's height; road dust, 0 to 1.</summary>
        public double Shade, Split, Dust;
    }

    public enum PaintGroup : byte { Car, Motorcycle, Bus, Truck, Boat, ThreeWheeler, Rickshaw }

    public static class VehiclePaints
    {
        public const int Count = 20;
        const int Black = 0, Charcoal = 1, Navy = 2, SteelBlue = 3, Burgundy = 4, Crimson = 5, Brick = 6, Forest = 7, Olive = 8, RacingGreen = 9, OffWhite = 10, Ivory = 11, Silver = 12, Aluminium = 13, Graphite = 14, Gunmetal = 15, Champagne = 16, Bronze = 17, Copper = 18, Plum = 19;

        // Each number is a share of that group's vehicles, so a repeated colour is a common one. Every group keeps its own look; between them they use all twenty.
        static readonly int[] Cars = { Silver, Silver, Silver, OffWhite, OffWhite, OffWhite, Black, Black, Charcoal, Graphite, Graphite, Gunmetal, Aluminium, Navy, Navy, Burgundy, RacingGreen, Forest, Champagne, SteelBlue, Plum, Bronze };
        static readonly int[] Bikes = { Black, Black, Charcoal, Charcoal, Crimson, Brick, SteelBlue, SteelBlue, Silver, Aluminium, RacingGreen, Forest, Plum };
        static readonly int[] Buses = { Navy, OffWhite, Burgundy, Forest, Silver, Graphite, Champagne, Ivory, Brick };
        static readonly int[] Trucks = { Gunmetal, Gunmetal, Aluminium, OffWhite, Ivory, Forest, RacingGreen, Navy, Champagne, Bronze, Copper, Brick, Olive };
        static readonly int[] Boats = { Navy, Navy, SteelBlue, OffWhite, Charcoal, Brick, Forest, Graphite };
        static readonly int[] ThreeWheelers = { Forest, Forest, Forest, RacingGreen, Olive };
        static readonly int[] Rickshaws = { SteelBlue, Brick, Forest, Crimson, Olive, Copper, Navy, Ivory };
        // Restrained schemes for a fleet: a pale body over a darker skirt, or the reverse.
        static readonly int[] FleetSkirt = { Navy, Graphite, Burgundy, Forest, Charcoal };

        public static PaintGroup GroupOf(VehicleKind k)
        {
            switch (k)
            {
                case VehicleKind.Motorcycle: case VehicleKind.Chopper: case VehicleKind.PoliceBike: return PaintGroup.Motorcycle;
                case VehicleKind.Bus: case VehicleKind.Coach: return PaintGroup.Bus;
                case VehicleKind.Truck: case VehicleKind.Tipper: return PaintGroup.Truck;
                case VehicleKind.Boat: return PaintGroup.Boat;
                case VehicleKind.Cng: return PaintGroup.ThreeWheeler;
                case VehicleKind.Rickshaw: return PaintGroup.Rickshaw;
                default: return PaintGroup.Car;
            }
        }

        public static int[] Colours(PaintGroup g)
        {
            switch (g)
            {
                case PaintGroup.Motorcycle: return Bikes;
                case PaintGroup.Bus: return Buses;
                case PaintGroup.Truck: return Trucks;
                case PaintGroup.Boat: return Boats;
                case PaintGroup.ThreeWheeler: return ThreeWheelers;
                case PaintGroup.Rickshaw: return Rickshaws;
                default: return Cars;
            }
        }

        /// <summary>The paint job this vehicle always gets, from its number: the same vehicle looks the same every time it is drawn.</summary>
        public static PaintJob For(VehicleKind kind, uint id)
        {
            uint h = id * 2654435761u;
            h ^= h >> 15;
            var group = GroupOf(kind);
            var list = Colours(group);
            var job = new PaintJob { Base = list[(int)(h % (uint)list.Length)], Second = -1 };
            uint h2 = h * 2246822519u;
            job.Shade = 0.92 + 0.16 * ((h2 >> 8 & 255) / 255.0);
            double wear = (h2 >> 16 & 255) / 255.0;
            // Most are kept up; a few, and more of the working ones, have seen better days.
            bool working = group == PaintGroup.Bus || group == PaintGroup.Truck || group == PaintGroup.ThreeWheeler || group == PaintGroup.Rickshaw;
            job.Dust = working ? 0.3 + 0.5 * wear : wear < 0.75 ? 0.08 + 0.2 * wear : 0.3 + 0.8 * (wear - 0.75);
            if (group == PaintGroup.Bus && (h2 >> 24 & 3) != 0)
            {
                int skirt = FleetSkirt[(int)((h2 >> 26) % (uint)FleetSkirt.Length)];
                if (skirt != job.Base) { job.Second = skirt; job.Split = 0.34; }
            }
            return job;
        }

        /// <summary>A job in one chosen colour, for the player's choice.</summary>
        public static PaintJob Plain(int colour, double dust = 0.08) => new PaintJob { Base = colour, Second = -1, Shade = 1, Dust = dust };
    }
}
