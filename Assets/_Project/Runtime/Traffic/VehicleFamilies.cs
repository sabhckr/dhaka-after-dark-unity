// What a vehicle is to the game: the meshes of one family, baked from the owner's models (Tools/models/bake.py, read by VehicleArt), and the names of the parts that move.
// Nothing is generated in code any more: the generated families and figures were removed on the owner's order of 2026-11-06 and stay in git history.
namespace Dad.Traffic
{
    public struct WheelSpot { public double X, Y, Z; public bool Steers, Flip; public int Mesh; /// <summary>The driven car: what turns with this wheel and does not spin, in VehicleModel.Hubs; -1 for none.</summary>
        public int Hub; }

    /// <summary>One family's meshes.</summary>
    public sealed class VehicleModel
    {
        public VehicleKind Kind;
        public KitMesh Near, Glass, Far;
        /// <summary>The wheel meshes in the file. WheelSpot.Mesh counts from where the reader was told to start.</summary>
        public KitMesh[] OwnWheels;
        /// <summary>The driven car's brakes, each at its own wheel.</summary>
        public KitMesh[] Hubs;
        /// <summary>Metres, from the model.</summary>
        public double Length, Width, Height;
        public WheelSpot[] Wheels;
        /// <summary>The driven vehicle's cabin and its moving pieces; empty on a traffic vehicle.</summary>
        public KitPart[] Parts;
        /// <summary>Where the driver's eyes are (the driven vehicle).</summary>
        public V3 Eye;
        /// <summary>The top of the driver's head, metres above the road.</summary>
        public double HeadTop;
    }

    public static class VehicleFamilies
    {
        public const string CabinPart = "Cabin", SteeringWheel = "SteeringWheel", SpeedNeedle = "SpeedNeedle", RevNeedle = "RevNeedle", FuelNeedle = "FuelNeedle", DriverFigure = "Driver";
        public const string DoorPart = "Door", DoorGlass = "Glass", DriverDoor = "DoorFrontRight";
    }
}
