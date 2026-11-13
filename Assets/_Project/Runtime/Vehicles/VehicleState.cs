// A vehicle as it is this moment (E-04), the one place everything reads from: gauges, lamps, wheels, sound, the camera. Nothing a player sees
// of a vehicle is worked out anywhere else.
namespace Dad.Vehicles
{
    public sealed class WheelState
    {
        /// <summary>Where it is on the body, metres; its radius; and where the hub sits at rest.</summary>
        public double X, RestY, Z, Radius;
        public bool Front, Steers, Driven;
        /// <summary>The hub's height on the body now, the spring's load (N), turn of the wheel about its axle (radians) and its rate.</summary>
        public double HubY, Load, Angle, Spin;
        /// <summary>The wheel's steering angle (radians, positive right); slip along and across, as ratio and radians.</summary>
        public double Steer, SlipRatio, SlipAngle;
        /// <summary>How far past its grip the tyre is, 0 gripping to 1 sliding.</summary>
        public double Skid;
        public bool Grounded;
        public D3 Contact, Normal = new D3(0, 1, 0);
        internal double LastHub;
        internal bool WasGrounded;
    }

    public sealed class VehicleState
    {
        public readonly WheelState[] Wheels;
        /// <summary>Metres a second along the body, negative going backwards.</summary>
        public double Speed;
        public double Rpm, Throttle, Brake, SteerAngle;
        /// <summary>1 upwards, 0 neutral, -1 reverse.</summary>
        public int Gear = 1;
        public double ShiftLeft;
        public bool EngineOn = true, Handbrake;
        /// <summary>Lamps that follow from the driving.</summary>
        public bool BrakeLamps, ReverseLamps;
        /// <summary>The driver's switches. Indicator: -1 left, 0 off, 1 right; it cancels itself as the wheel comes back from a turn.</summary>
        public bool HeadLamps, Hazards;
        public int Indicator;
        /// <summary>Share of a full tank.</summary>
        public double Fuel = 0.8;

        public VehicleState(WheelState[] wheels) { Wheels = wheels; }

        public bool Reversing => Gear < 0;
    }
}
