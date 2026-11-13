// How each family handles (E-05). Estimates for this kind of vehicle, not measurements of any model, and no make is copied. Handling comes from
// these numbers acting through one model, so a difference between two vehicles is a difference in mass, springs, tyres and engine, not a special case.
using Dad.Traffic;

namespace Dad.Vehicles
{
    public sealed class HandlingSpec
    {
        public VehicleKind Kind;
        /// <summary>Kilograms; the centre of mass's height above the road; the share of the weight on the front axle.</summary>
        public double Mass, CentreHeight, FrontShare;
        /// <summary>Spring rate as a natural frequency (Hz), and damping as a share of critical, in bump and rebound.</summary>
        public double FrontHz, RearHz, BumpDamping, ReboundDamping;
        /// <summary>Metres the hub may go below and above where it sits at rest.</summary>
        public double Droop, Bump;
        /// <summary>Anti-roll bars, newtons per metre of difference across an axle.</summary>
        public double FrontBar, RearBar;
        /// <summary>The height side forces act at (roll centre), and the share of braking pitch the geometry takes out (anti-dive).</summary>
        public double RollCentre, AntiDive;
        /// <summary>Peak grip per axle, the slip at that peak (ratio, radians), and how much grip falls off with load.</summary>
        public double FrontGrip, RearGrip, PeakSlipRatio, PeakSlipAngle, LoadFade;
        /// <summary>Radians at the road wheels at rest; radians over the turn a road can hold at speed; how fast the wheels turn and return.</summary>
        public double MaxSteer, SteerSlack, SteerRate, SteerReturn;
        /// <summary>The lateral acceleration the steering limit is worked out for (m/s2).</summary>
        public double SteerGrip;
        /// <summary>Newton metres at the peak, at PeakRpm; the idle, the redline, and the revs the box changes up at under full throttle.</summary>
        public double PeakTorque, PeakRpm, IdleRpm, RedlineRpm, ShiftRpm;
        /// <summary>Gear ratios, and the final drive.</summary>
        public double[] Gears;
        public double ReverseRatio, FinalDrive, ShiftTime;
        /// <summary>The share of drive on the front axle.</summary>
        public double FrontDrive;
        /// <summary>Most brake torque per wheel, front and rear, and for the handbrake on each rear wheel.</summary>
        public double FrontBrake, RearBrake, Handbrake;
        public bool Abs;
        /// <summary>Traction control: drive torque is cut while a driven wheel spins past its peak slip, as ABS cuts the brake.</summary>
        public bool Tcs;
        /// <summary>Drag area (m2), rolling resistance, the rotating inertia of a wheel (kg m2).</summary>
        public double DragArea, Rolling, WheelInertia;
        /// <summary>Seconds a full tank lasts at full throttle and full revs.</summary>
        public double TankSeconds;
        /// <summary>Two wheels in line: the rider keeps it up. How far it may lean (radians), and how quickly the lean follows (Hz).</summary>
        public bool TwoWheels;
        public double LeanMost = 0.75, LeanHz = 1.6;

        public static readonly HandlingSpec Sedan = new HandlingSpec
        {
            Kind = VehicleKind.Sedan, Mass = 1250, CentreHeight = 0.58, FrontShare = 0.56,
            FrontHz = 1.15, RearHz = 1.25, BumpDamping = 0.24, ReboundDamping = 0.38, Droop = 0.13, Bump = 0.1,
            FrontBar = 6000, RearBar = 2000, RollCentre = 0.12, AntiDive = 0.15,
            FrontGrip = 0.98, RearGrip = 1.0, PeakSlipRatio = 0.12, PeakSlipAngle = 0.16, LoadFade = 0.12,
            MaxSteer = 0.58, SteerSlack = 0.1, SteerRate = 1.5, SteerReturn = 3, SteerGrip = 8.5,
            PeakTorque = 145, PeakRpm = 4400, IdleRpm = 800, RedlineRpm = 6400, ShiftRpm = 5800,
            Gears = new[] { 3.4, 2.0, 1.35, 1.0, 0.8 }, ReverseRatio = 3.2, FinalDrive = 3.9, ShiftTime = 0.28,
            FrontDrive = 1, FrontBrake = 1300, RearBrake = 650, Handbrake = 1500, Abs = true,
            DragArea = 0.8, Rolling = 0.014, WheelInertia = 1.1, TankSeconds = 5400,
        };

        HandlingSpec As(VehicleKind kind, System.Action<HandlingSpec> change)
        {
            var s = (HandlingSpec)MemberwiseClone();
            s.Kind = kind;
            change(s);
            return s;
        }

        // Cars: the same model with the numbers of each kind of car.
        public static readonly HandlingSpec Hatch = Sedan.As(VehicleKind.Hatch, s => { s.Mass = 1320; s.CentreHeight = 0.64; s.PeakTorque = 160; s.FrontBar = 7000; });
        public static readonly HandlingSpec Microbus = Sedan.As(VehicleKind.Microbus, s => { s.Mass = 1450; s.CentreHeight = 0.66; s.FrontHz = 1.25; s.RearHz = 1.4; s.PeakTorque = 150; });
        public static readonly HandlingSpec Saloon = Sedan.As(VehicleKind.Saloon, s => { s.Mass = 2100; s.CentreHeight = 0.54; s.FrontShare = 0.5; s.FrontHz = 1.05; s.RearHz = 1.15; s.PeakTorque = 420; s.PeakRpm = 3200; s.FrontDrive = 0.5; s.FrontBrake = 2300; s.RearBrake = 1300; s.DragArea = 0.62; s.MaxSteer = 0.54; });
        public static readonly HandlingSpec Roadster = Sedan.As(VehicleKind.Roadster, s => { s.Mass = 1560; s.CentreHeight = 0.48; s.FrontShare = 0.52; s.FrontHz = 1.5; s.RearHz = 1.6; s.FrontGrip = 1.08; s.RearGrip = 1.1; s.PeakTorque = 400; s.PeakRpm = 4500; s.FrontDrive = 0; s.FrontBrake = 2000; s.RearBrake = 1100; s.SteerRate = 1.9; s.FrontBar = 9000; s.RearBar = 5000; s.Tcs = true; });
        public static readonly HandlingSpec Sports = Sedan.As(VehicleKind.Sports, s => { s.Mass = 1780; s.CentreHeight = 0.46; s.FrontShare = 0.51; s.FrontHz = 1.6; s.RearHz = 1.7; s.FrontGrip = 1.12; s.RearGrip = 1.16; s.PeakTorque = 560; s.PeakRpm = 4800; s.FrontDrive = 0; s.FrontBrake = 2500; s.RearBrake = 1400; s.SteerRate = 2.1; s.FrontBar = 12000; s.RearBar = 7000; s.DragArea = 0.68; s.Tcs = true; });

        /// <summary>A motorcycle: two wheels in line, one steering, a light engine with a long rev range. It stands by Balance, not by its tyres.</summary>
        public static readonly HandlingSpec Motorcycle = new HandlingSpec
        {
            Kind = VehicleKind.Motorcycle, TwoWheels = true, Mass = 265, CentreHeight = 0.6, FrontShare = 0.5,
            FrontHz = 2.0, RearHz = 2.2, BumpDamping = 0.3, ReboundDamping = 0.45, Droop = 0.1, Bump = 0.09,
            FrontBar = 0, RearBar = 0, RollCentre = 0.28, AntiDive = 0.3,
            FrontGrip = 1.05, RearGrip = 1.08, PeakSlipRatio = 0.11, PeakSlipAngle = 0.14, LoadFade = 0.08,
            MaxSteer = 0.42, SteerSlack = 0.01, SteerRate = 1.7, SteerReturn = 4, SteerGrip = 7.5,
            PeakTorque = 52, PeakRpm = 7000, IdleRpm = 1200, RedlineRpm = 10500, ShiftRpm = 9500,
            Gears = new[] { 2.6, 1.9, 1.5, 1.25, 1.08 }, ReverseRatio = 2.6, FinalDrive = 5.2, ShiftTime = 0.14,
            FrontDrive = 0, FrontBrake = 620, RearBrake = 300, Handbrake = 260, Abs = true, Tcs = true,
            DragArea = 0.55, Rolling = 0.016, WheelInertia = 0.4, TankSeconds = 4000,
        };
        public static readonly HandlingSpec Chopper = Motorcycle.As(VehicleKind.Chopper, s => { s.Mass = 340; s.CentreHeight = 0.54; s.FrontShare = 0.42; s.MaxSteer = 0.34; s.SteerRate = 1.2; s.PeakTorque = 110; s.PeakRpm = 3200; s.IdleRpm = 900; s.RedlineRpm = 5600; s.ShiftRpm = 5000; s.Gears = new[] { 2.8, 1.95, 1.45, 1.15 }; s.FinalDrive = 3.4; s.ShiftTime = 0.25; s.LeanMost = 0.5; });
        public static readonly HandlingSpec PoliceBike = Motorcycle.As(VehicleKind.PoliceBike, s => { s.Mass = 290; s.PeakTorque = 48; s.PeakRpm = 6500; s.FrontHz = 1.8; s.RearHz = 2.0; });

        /// <summary>The double-decker: heavy, tall and slow to everything, turning on a wide circle.</summary>
        public static readonly HandlingSpec Bus = new HandlingSpec
        {
            Kind = VehicleKind.Bus, Mass = 11500, CentreHeight = 1.25, FrontShare = 0.38,
            FrontHz = 1.0, RearHz = 1.1, BumpDamping = 0.28, ReboundDamping = 0.42, Droop = 0.14, Bump = 0.12,
            FrontBar = 120000, RearBar = 160000, RollCentre = 0.55, AntiDive = 0.1,
            FrontGrip = 0.85, RearGrip = 0.88, PeakSlipRatio = 0.12, PeakSlipAngle = 0.14, LoadFade = 0.1,
            MaxSteer = 0.6, SteerSlack = 0.05, SteerRate = 0.8, SteerReturn = 1.6, SteerGrip = 3.0,
            PeakTorque = 700, PeakRpm = 1400, IdleRpm = 600, RedlineRpm = 2400, ShiftRpm = 2100,
            Gears = new[] { 5.0, 3.0, 1.9, 1.3, 1.0 }, ReverseRatio = 5.0, FinalDrive = 4.6, ShiftTime = 0.7,
            FrontDrive = 0, FrontBrake = 5500, RearBrake = 7500, Handbrake = 11000, Abs = true,
            DragArea = 6.0, Rolling = 0.009, WheelInertia = 14, TankSeconds = 7200,
        };
        public static readonly HandlingSpec Coach = Bus.As(VehicleKind.Coach, s => { s.Mass = 10500; s.CentreHeight = 1.0; s.PeakTorque = 760; s.DragArea = 4.8; });

        /// <summary>The tipper, loaded: heavier than the bus and longer to stop, with stiff rear springs and a low first gear.</summary>
        public static readonly HandlingSpec Truck = new HandlingSpec
        {
            Kind = VehicleKind.Truck, Mass = 15000, CentreHeight = 1.15, FrontShare = 0.33,
            FrontHz = 1.2, RearHz = 1.5, BumpDamping = 0.3, ReboundDamping = 0.42, Droop = 0.13, Bump = 0.1,
            FrontBar = 140000, RearBar = 220000, RollCentre = 0.6, AntiDive = 0.08,
            FrontGrip = 0.82, RearGrip = 0.86, PeakSlipRatio = 0.12, PeakSlipAngle = 0.13, LoadFade = 0.1,
            MaxSteer = 0.56, SteerSlack = 0.04, SteerRate = 0.7, SteerReturn = 1.4, SteerGrip = 2.6,
            PeakTorque = 1000, PeakRpm = 1300, IdleRpm = 600, RedlineRpm = 2300, ShiftRpm = 2000,
            Gears = new[] { 5.2, 3.6, 2.4, 1.6, 1.15, 0.9 }, ReverseRatio = 5.2, FinalDrive = 4.3, ShiftTime = 0.8,
            FrontDrive = 0, FrontBrake = 5000, RearBrake = 6500, Handbrake = 12000, Abs = true,
            DragArea = 6.6, Rolling = 0.01, WheelInertia = 16, TankSeconds = 9000,
        };

        /// <summary>The tipper is the truck with a heavier back end and its own turning circle.</summary>
        public static readonly HandlingSpec Tipper = Truck.As(VehicleKind.Tipper, s => { s.Mass = 17000; s.FrontShare = 0.3; s.MaxSteer = 0.52; s.DragArea = 7.0; });

        public static HandlingSpec For(VehicleKind kind)
        {
            switch (kind)
            {
                case VehicleKind.Sedan: return Sedan;
                case VehicleKind.Hatch: return Hatch;
                case VehicleKind.Microbus: return Microbus;
                case VehicleKind.Saloon: return Saloon;
                case VehicleKind.Roadster: return Roadster;
                case VehicleKind.Sports: return Sports;
                case VehicleKind.Motorcycle: return Motorcycle;
                case VehicleKind.Chopper: return Chopper;
                case VehicleKind.PoliceBike: return PoliceBike;
                case VehicleKind.Bus: return Bus;
                case VehicleKind.Coach: return Coach;
                case VehicleKind.Truck: return Truck;
                case VehicleKind.Tipper: return Tipper;
                default: return null;
            }
        }
    }
}
