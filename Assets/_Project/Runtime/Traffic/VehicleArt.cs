// A vehicle from the owner's own models (2026-11-06). Tools/models/bake.py turns a model into a .dadv file in Resources/Vehicles: the same meshes VehicleModel always held, in the
// format the vehicle shader reads, so traffic draws them as it drew everything. No engine here, so the tests read the same files outside Unity.
using System;
using System.IO;
using System.Text;

namespace Dad.Traffic
{
    public static class VehicleArt
    {
        public const int Version = 4;

        /// <summary>The file's vehicle. Its wheels point at `firstWheel` and on, the numbers the caller keeps the file's wheel meshes under.</summary>
        public static VehicleModel Read(VehicleKind kind, byte[] data, int firstWheel)
        {
            using var r = new BinaryReader(new MemoryStream(data));
            if (Encoding.ASCII.GetString(r.ReadBytes(4)) != "DADV") throw new InvalidDataException(kind + ": not a vehicle file");
            int version = r.ReadInt32();
            if (version != Version) throw new InvalidDataException(kind + ": version " + version);
            var eye = new V3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
            double headTop = r.ReadSingle();
            double length = r.ReadSingle(), width = r.ReadSingle(), height = r.ReadSingle();
            var wheels = new WheelSpot[r.ReadInt32()];
            for (int i = 0; i < wheels.Length; i++)
            {
                wheels[i] = new WheelSpot { X = r.ReadSingle(), Y = r.ReadSingle(), Z = r.ReadSingle(), Steers = r.ReadByte() != 0, Flip = r.ReadByte() != 0, Mesh = firstWheel + r.ReadByte() };
                int hub = r.ReadByte();
                wheels[i].Hub = hub == 255 ? -1 : hub;
            }
            var near = Mesh(r);
            var glass = Mesh(r);
            var far = Mesh(r);
            var own = new KitMesh[r.ReadInt32()];
            for (int i = 0; i < own.Length; i++) own[i] = Mesh(r);
            var hubs = new KitMesh[r.ReadInt32()];
            for (int i = 0; i < hubs.Length; i++) hubs[i] = Mesh(r);
            var parts = new KitPart[r.ReadInt32()];
            for (int i = 0; i < parts.Length; i++)
            {
                var p = parts[i] = new KitPart { Name = Encoding.ASCII.GetString(r.ReadBytes(r.ReadByte())) };
                p.Pivot = new V3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                p.Axis = new V3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                p.Swing = r.ReadSingle();
                p.Glass = r.ReadByte() != 0;
                p.Mesh = Mesh(r);
            }
            return new VehicleModel { Kind = kind, Near = near, Glass = glass, Far = far, OwnWheels = own, Hubs = hubs, Wheels = wheels, Parts = parts, Eye = eye, HeadTop = headTop, Length = length, Width = width, Height = height };
        }

        static KitMesh Mesh(BinaryReader r)
        {
            int vertices = r.ReadInt32(), indices = r.ReadInt32();
            var m = new KitMesh();
            for (int i = 0; i < vertices * 3; i++) m.Pos.Add(r.ReadSingle());
            for (int i = 0; i < vertices * 3; i++) m.Nor.Add(r.ReadSingle());
            m.Rgba.AddRange(r.ReadBytes(vertices * 4));
            for (int i = 0; i < vertices; i++) m.Lamp.Add(r.ReadSingle());
            for (int i = 0; i < indices; i++) m.Tris.Add(r.ReadInt32());
            int mapped = r.ReadInt32();
            for (int i = 0; i < mapped * 2; i++) m.Map.Add(r.ReadSingle());
            return m;
        }
    }
}
