// The vehicle families the traffic simulation drives, and what each one can do. Sizes and performance are
// estimates for this kind of vehicle on Dhaka's streets (DETAILING_DIRECTIVE part 15: a bus must not handle
// like a sedan, a CNG not like a motorcycle), not measurements of any model; no make is copied.
//
// Who may go where (Access), a labelled rule rather than mapped data:
// - everything must fit the lane (Lane.Width), to within what its mirrors overhang;
// - cycle rickshaws stay off trunk and primary roads, and off the motorway class;
// - heavy vehicles keep to tertiary roads and greater;
// - the CNG stays off the motorway class.
namespace Dad.Traffic
{
    public enum VehicleKind : byte { Sedan, Cng, Rickshaw, Motorcycle, Bus, Coach, Truck, Microbus, Hatch, Saloon, Roadster, Sports, Chopper, PoliceBike, Tipper, Boat }

    /// <summary>The road-access groups, which TrafficNet works out reachability for.</summary>
    public enum Access : byte { Motorcycle, Rickshaw, Cng, Car, Heavy, Count }

    public sealed class VehicleSpec
    {
        public VehicleKind Kind;
        public Access Group;
        /// <summary>Metres.</summary>
        public double Length, Width, Height, WheelBase, WheelRadius;
        /// <summary>IDM: top acceleration and comfortable braking (m/s2), time headway (s), standstill gap (m), top speed (m/s).</summary>
        public double Accel, Brake, Headway, MinGap, Top;
        /// <summary>Slips past slower traffic inside a lane where there is room.</summary>
        public bool Filters;

        public static readonly VehicleSpec[] All =
        {
            new VehicleSpec { Kind = VehicleKind.Sedan, Group = Access.Car, Length = 4.4, Width = 1.72, Height = 1.45, WheelBase = 2.6, WheelRadius = 0.31, Accel = 2.0, Brake = 3.0, Headway = 1.2, MinGap = 1.6, Top = 22 },
            new VehicleSpec { Kind = VehicleKind.Cng, Group = Access.Cng, Length = 2.7, Width = 1.38, Height = 1.73, WheelBase = 2.02, WheelRadius = 0.22, Accel = 1.3, Brake = 2.5, Headway = 1.0, MinGap = 1.0, Top = 13, Filters = true },
            new VehicleSpec { Kind = VehicleKind.Rickshaw, Group = Access.Rickshaw, Length = 2.25, Width = 0.92, Height = 1.74, WheelBase = 1.57, WheelRadius = 0.33, Accel = 0.6, Brake = 1.8, Headway = 1.0, MinGap = 0.8, Top = 3.6 },
            new VehicleSpec { Kind = VehicleKind.Motorcycle, Group = Access.Motorcycle, Length = 1.98, Width = 0.8, Height = 1.72, WheelBase = 1.38, WheelRadius = 0.3, Accel = 2.6, Brake = 3.6, Headway = 0.9, MinGap = 0.9, Top = 20, Filters = true },
            new VehicleSpec { Kind = VehicleKind.Bus, Group = Access.Heavy, Length = 10.2, Width = 2.48, Height = 4.39, WheelBase = 5.27, WheelRadius = 0.52, Accel = 1.0, Brake = 2.2, Headway = 1.6, MinGap = 2.0, Top = 14 },
            new VehicleSpec { Kind = VehicleKind.Coach, Group = Access.Heavy, Length = 10.9, Width = 2.5, Height = 3.12, WheelBase = 5.38, WheelRadius = 0.49, Accel = 1.0, Brake = 2.2, Headway = 1.7, MinGap = 2.0, Top = 20 },
            new VehicleSpec { Kind = VehicleKind.Truck, Group = Access.Heavy, Length = 7.0, Width = 2.5, Height = 2.9, WheelBase = 3.75, WheelRadius = 0.49, Accel = 0.8, Brake = 2.0, Headway = 1.8, MinGap = 2.0, Top = 14 },
            new VehicleSpec { Kind = VehicleKind.Microbus, Group = Access.Car, Length = 4.5, Width = 1.74, Height = 1.5, WheelBase = 2.57, WheelRadius = 0.31, Accel = 1.6, Brake = 2.8, Headway = 1.3, MinGap = 1.6, Top = 18 },
            new VehicleSpec { Kind = VehicleKind.Hatch, Group = Access.Car, Length = 4.15, Width = 1.8, Height = 1.6, WheelBase = 2.57, WheelRadius = 0.32, Accel = 2.0, Brake = 3.0, Headway = 1.2, MinGap = 1.6, Top = 22 },
            new VehicleSpec { Kind = VehicleKind.Saloon, Group = Access.Car, Length = 5.2, Width = 2.07, Height = 1.5, WheelBase = 3.42, WheelRadius = 0.43, Accel = 2.2, Brake = 3.2, Headway = 1.3, MinGap = 1.8, Top = 24 },
            new VehicleSpec { Kind = VehicleKind.Roadster, Group = Access.Car, Length = 4.1, Width = 1.82, Height = 1.3, WheelBase = 2.43, WheelRadius = 0.33, Accel = 2.6, Brake = 3.4, Headway = 1.1, MinGap = 1.6, Top = 25 },
            new VehicleSpec { Kind = VehicleKind.Sports, Group = Access.Car, Length = 4.75, Width = 1.95, Height = 1.29, WheelBase = 2.8, WheelRadius = 0.35, Accel = 2.8, Brake = 3.6, Headway = 1.1, MinGap = 1.6, Top = 25 },
            new VehicleSpec { Kind = VehicleKind.Chopper, Group = Access.Motorcycle, Length = 2.45, Width = 0.9, Height = 1.5, WheelBase = 1.86, WheelRadius = 0.3, Accel = 2.2, Brake = 3.0, Headway = 1.0, MinGap = 1.0, Top = 19, Filters = true },
            new VehicleSpec { Kind = VehicleKind.PoliceBike, Group = Access.Motorcycle, Length = 2.15, Width = 0.85, Height = 1.87, WheelBase = 1.5, WheelRadius = 0.33, Accel = 2.4, Brake = 3.4, Headway = 0.9, MinGap = 0.9, Top = 20, Filters = true },
            new VehicleSpec { Kind = VehicleKind.Tipper, Group = Access.Heavy, Length = 7.2, Width = 2.6, Height = 2.9, WheelBase = 3.8, WheelRadius = 0.52, Accel = 0.8, Brake = 2.0, Headway = 1.8, MinGap = 2.0, Top = 14 },
            new VehicleSpec { Kind = VehicleKind.Boat, Group = Access.Heavy, Length = 4.13, Width = 1.55, Height = 1.41, WheelBase = 1, WheelRadius = 0.1, Accel = 1, Brake = 1, Headway = 1, MinGap = 1, Top = 12 },
        };

        public static VehicleSpec Of(VehicleKind kind) => All[(int)kind];

        /// <summary>Can a vehicle of this group use a lane this wide on a road of this rank?</summary>
        public static bool Allowed(Access group, double laneWidth, byte rank)
        {
            switch (group)
            {
                case Access.Motorcycle: return laneWidth >= 0.8;
                case Access.Rickshaw: return laneWidth >= 1.0 && rank >= 3;
                case Access.Cng: return laneWidth >= 1.25 && rank >= 1;
                case Access.Car: return laneWidth >= 1.6;
                default: return laneWidth >= 2.2 && rank <= 4;
            }
        }
    }

    /// <summary>A small repeatable random source: runs with one seed drive the same.</summary>
    public struct TrafficRandom
    {
        ulong s;
        public TrafficRandom(ulong seed) { s = seed * 2685821657736338717UL + 88172645463325252UL; if (s == 0) s = 88172645463325252UL; }
        public ulong Next() { s ^= s << 13; s ^= s >> 7; s ^= s << 17; return s; }
        /// <summary>In [0, 1).</summary>
        public double Unit() => (Next() >> 11) * (1.0 / 9007199254740992.0);
        public int Below(int n) => (int)(Unit() * n);
    }
}
