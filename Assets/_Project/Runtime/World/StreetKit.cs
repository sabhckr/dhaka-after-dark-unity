// Street kit parts (Milestone 2, Farmgate validation): utility poles, street lights, trees and
// overhead cables, generated in code like everything else in the world, at two levels of detail
// (lod 0 near, lod 1 far). KitWriter is the shared mesh writer: closed solids (hexahedra, tubes,
// blobs, lofted sections) whose every triangle is turned to face out, so the lit world shader's
// derivative normals and the front-face rules hold for any part.
//
// Sizes are this port's estimates for Dhaka street furniture, not measurements of a product: a
// prestressed concrete pole about 10 m tall with an 11 kV crossarm and a low-voltage crossarm, a
// galvanised steel lamp column about 8 m tall with an LED head, and three generic broadleaf tree
// forms (no species is mapped). Conductors and telecom bundles are drawn thicker than real (2 and
// 3.5 cm) so that they hold together at 1280x720 instead of breaking into single pixels.
//
// Vertices are x, y, z floats in metres from the chunk's south-west corner; colours are sRGB with
// the material in alpha, packed as WorldMaterials.Pack. Indices are relative to Base, the first
// vertex of the range being written. No UnityEngine dependency: safe on worker threads.
using System;
using System.Collections.Generic;

namespace Dad.World
{
    /// <summary>Appends closed solids to a vertex, colour and index list, every triangle facing out.</summary>
    public sealed class KitWriter
    {
        public List<float> P;
        public List<uint> C;
        public List<int> I;
        /// <summary>The first vertex of the range being written: indices are stored relative to it.</summary>
        public int Base;

        public KitWriter(List<float> p, List<uint> c, List<int> i) { P = p; C = c; I = i; }

        public int Count => C.Count;

        public int V(double x, double y, double z, uint colour)
        {
            P.Add((float)x);
            P.Add((float)y);
            P.Add((float)z);
            C.Add(colour);
            return C.Count - 1;
        }

        /// <summary>A triangle whose front face (Unity's: normal = cross(b - a, c - a)) turns towards (ox, oy, oz).</summary>
        public void Tri(int a, int b, int c, double ox, double oy, double oz)
        {
            double ax = P[3 * a], ay = P[3 * a + 1], az = P[3 * a + 2];
            double ux = P[3 * b] - ax, uy = P[3 * b + 1] - ay, uz = P[3 * b + 2] - az;
            double vx = P[3 * c] - ax, vy = P[3 * c + 1] - ay, vz = P[3 * c + 2] - az;
            double nx = uy * vz - uz * vy, ny = uz * vx - ux * vz, nz = ux * vy - uy * vx;
            if (nx * ox + ny * oy + nz * oz < 0) (b, c) = (c, b);
            I.Add(a - Base);
            I.Add(b - Base);
            I.Add(c - Base);
        }

        /// <summary>A quad from four corners in order around it, facing (ox, oy, oz).</summary>
        public void Quad(int a, int b, int c, int d, double ox, double oy, double oz)
        {
            Tri(a, b, c, ox, oy, oz);
            Tri(a, c, d, ox, oy, oz);
        }

        /// <summary>A quad given by its corners' positions, all four in one colour.</summary>
        public void Quad(double x0, double y0, double z0, double x1, double y1, double z1, double x2, double y2, double z2,
            double x3, double y3, double z3, uint colour, double ox, double oy, double oz)
        {
            int a = V(x0, y0, z0, colour), b = V(x1, y1, z1, colour), c = V(x2, y2, z2, colour), d = V(x3, y3, z3, colour);
            Quad(a, b, c, d, ox, oy, oz);
        }

        /// <summary>A convex hexahedron: k holds 8 corners (x, y, z), the bottom four then the top four in the same order around.</summary>
        public void Hex(ReadOnlySpan<double> k, uint colour)
        {
            double cx = 0, cy = 0, cz = 0;
            for (int i = 0; i < 8; i++) { cx += k[3 * i]; cy += k[3 * i + 1]; cz += k[3 * i + 2]; }
            cx /= 8; cy /= 8; cz /= 8;
            int f = V(k[0], k[1], k[2], colour);
            for (int i = 1; i < 8; i++) V(k[3 * i], k[3 * i + 1], k[3 * i + 2], colour);
            Face(f, 0, 1, 2, 3, cx, cy, cz);
            Face(f, 4, 5, 6, 7, cx, cy, cz);
            for (int i = 0; i < 4; i++)
            {
                int j = (i + 1) % 4;
                Face(f, i, j, j + 4, i + 4, cx, cy, cz);
            }
        }

        void Face(int f, int a, int b, int c, int d, double cx, double cy, double cz)
        {
            Mid(f + a, f + b, f + c, f + d, out double ox, out double oy, out double oz);
            Quad(f + a, f + b, f + c, f + d, ox - cx, oy - cy, oz - cz);
        }

        /// <summary>
        /// A box standing on (x, y0, z) up to y1: half lengths along (ax, az) and across it at the bottom
        /// (a0, w0) and the top (a1, w1), so it may taper.
        /// </summary>
        public void Post(double x, double z, double y0, double y1, double ax, double az, double a0, double w0, double a1, double w1, uint colour)
        {
            Span<double> k = stackalloc double[24];
            Corners(k, 0, x, y0, z, ax, az, a0, w0);
            Corners(k, 12, x, y1, z, ax, az, a1, w1);
            Hex(k, colour);
        }

        /// <summary>A box from (x0, z0) to (x1, z1) on the ground plan, half width w across, from y0a/y0b up to y1a/y1b at either end.</summary>
        public void Beam(double x0, double z0, double x1, double z1, double w, double y0a, double y1a, double y0b, double y1b, uint colour)
        {
            double dx = x1 - x0, dz = z1 - z0, len = Math.Sqrt(dx * dx + dz * dz);
            if (len < 1e-6) return;
            double nx = -dz / len * w, nz = dx / len * w;
            Span<double> k = stackalloc double[]
            {
                x0 + nx, y0a, z0 + nz, x1 + nx, y0b, z1 + nz, x1 - nx, y0b, z1 - nz, x0 - nx, y0a, z0 - nz,
                x0 + nx, y1a, z0 + nz, x1 + nx, y1b, z1 + nz, x1 - nx, y1b, z1 - nz, x0 - nx, y1a, z0 - nz,
            };
            Hex(k, colour);
        }

        static void Corners(Span<double> k, int at, double x, double y, double z, double ax, double az, double a, double w)
        {
            double bx = -az, bz = ax;
            k[at] = x + ax * a + bx * w; k[at + 1] = y; k[at + 2] = z + az * a + bz * w;
            k[at + 3] = x - ax * a + bx * w; k[at + 4] = y; k[at + 5] = z - az * a + bz * w;
            k[at + 6] = x - ax * a - bx * w; k[at + 7] = y; k[at + 8] = z - az * a - bz * w;
            k[at + 9] = x + ax * a - bx * w; k[at + 10] = y; k[at + 11] = z + az * a - bz * w;
        }

        /// <summary>A closed tube of n sides from a to b, radius ra at a and rb at b.</summary>
        public void Tube(double ax, double ay, double az, double bx, double by, double bz, double ra, double rb, int n, uint colour)
        {
            double dx = bx - ax, dy = by - ay, dz = bz - az, len = Math.Sqrt(dx * dx + dy * dy + dz * dz);
            if (len < 1e-6) return;
            dx /= len; dy /= len; dz /= len;
            Frame(dx, dy, dz, out double ux, out double uy, out double uz, out double vx, out double vy, out double vz);
            int f = Count;
            for (int k = 0; k < n; k++)
            {
                double t = 2 * Math.PI * k / n, c = Math.Cos(t), s = Math.Sin(t);
                double rx = ux * c + vx * s, ry = uy * c + vy * s, rz = uz * c + vz * s;
                V(ax + rx * ra, ay + ry * ra, az + rz * ra, colour);
                V(bx + rx * rb, by + ry * rb, bz + rz * rb, colour);
            }
            for (int k = 0; k < n; k++)
            {
                int j = (k + 1) % n;
                double t = 2 * Math.PI * (k + 0.5) / n, c = Math.Cos(t), s = Math.Sin(t);
                Quad(f + 2 * k, f + 2 * j, f + 2 * j + 1, f + 2 * k + 1, ux * c + vx * s, uy * c + vy * s, uz * c + vz * s);
            }
            for (int k = 1; k + 1 < n; k++)
            {
                Tri(f, f + 2 * k, f + 2 * k + 2, -dx, -dy, -dz);
                Tri(f + 1, f + 2 * k + 1, f + 2 * k + 3, dx, dy, dz);
            }
        }

        /// <summary>An octahedron about (x, y, z), radii rx, ry, rz, turned by yaw and tilt: a leaf cluster.</summary>
        public void Blob(double x, double y, double z, double rx, double ry, double rz, double yaw, double tilt, uint colour)
        {
            double cy = Math.Cos(yaw), sy = Math.Sin(yaw), ct = Math.Cos(tilt), st = Math.Sin(tilt);
            // Axes: e1 = yaw-turned east, e2 = up tilted towards e3, e3 = e1 x up.
            double e1x = cy, e1z = sy, e3x = -sy, e3z = cy;
            double e2x = e3x * st, e2y = ct, e2z = e3z * st;
            double f3x = e3x * ct, f3y = -st, f3z = e3z * ct;
            int f = Count;
            V(x + e1x * rx, y, z + e1z * rx, colour);
            V(x - e1x * rx, y, z - e1z * rx, colour);
            V(x + e2x * ry, y + e2y * ry, z + e2z * ry, colour);
            V(x - e2x * ry, y - e2y * ry, z - e2z * ry, colour);
            V(x + f3x * rz, y + f3y * rz, z + f3z * rz, colour);
            V(x - f3x * rz, y - f3y * rz, z - f3z * rz, colour);
            for (int a = 0; a < 2; a++)
                for (int b = 2; b < 4; b++)
                    for (int c = 4; c < 6; c++)
                    {
                        Mid(f + a, f + b, f + c, f + c, out double ox, out double oy, out double oz);
                        Tri(f + a, f + b, f + c, ox - x, oy - y, oz - z);
                    }
        }

        /// <summary>A three-sided tube through the points (x, y, z triples), open at its ends: a cable.</summary>
        public void Cable(ReadOnlySpan<double> pts, double radius, uint colour)
        {
            int n = pts.Length / 3;
            if (n < 2) return;
            int f = Count;
            double px = 0, py = 1, pz = 0;
            for (int i = 0; i < n; i++)
            {
                int a = Math.Max(0, i - 1), b = Math.Min(n - 1, i + 1);
                double tx = pts[3 * b] - pts[3 * a], ty = pts[3 * b + 1] - pts[3 * a + 1], tz = pts[3 * b + 2] - pts[3 * a + 2];
                double tl = Math.Sqrt(tx * tx + ty * ty + tz * tz);
                if (tl < 1e-9) { tx = 1; ty = 0; tz = 0; tl = 1; }
                tx /= tl; ty /= tl; tz /= tl;
                Frame(tx, ty, tz, out double ux, out double uy, out double uz, out double vx, out double vy, out double vz);
                if (i == 0) { px = ux; py = uy; pz = uz; }
                for (int k = 0; k < 3; k++)
                {
                    double t = 2 * Math.PI * k / 3, c = Math.Cos(t), s = Math.Sin(t);
                    V(pts[3 * i] + (ux * c + vx * s) * radius, pts[3 * i + 1] + (uy * c + vy * s) * radius, pts[3 * i + 2] + (uz * c + vz * s) * radius, colour);
                }
            }
            for (int i = 0; i + 1 < n; i++)
                for (int k = 0; k < 3; k++)
                {
                    int j = (k + 1) % 3, a = f + 3 * i;
                    Mid(a + k, a + j, a + 3 + k, a + 3 + j, out double ox, out double oy, out double oz);
                    double mx = (pts[3 * i] + pts[3 * i + 3]) / 2, my = (pts[3 * i + 1] + pts[3 * i + 4]) / 2, mz = (pts[3 * i + 2] + pts[3 * i + 5]) / 2;
                    Quad(a + k, a + j, a + 3 + j, a + 3 + k, ox - mx, oy - my, oz - mz);
                }
        }

        /// <summary>
        /// Sweeps a closed cross-section along a path. The profile is (u across to the left, v up)
        /// pairs, either winding; colours holds one colour per profile edge. The path is (x, y, z)
        /// triples with a unit left normal (lx, lz) per point. Caps close the start and end when asked.
        /// </summary>
        public void Loft(ReadOnlySpan<double> profile, ReadOnlySpan<uint> colours, List<double> path, List<double> left,
            bool capStart, bool capEnd, uint capColour, List<double> flat, List<int> tris)
        {
            int m = profile.Length / 2, n = path.Count / 3;
            if (m < 3 || n < 2) return;
            double area = 0;
            for (int e = 0; e < m; e++)
            {
                int g = (e + 1) % m;
                area += profile[2 * e] * profile[2 * g + 1] - profile[2 * g] * profile[2 * e + 1];
            }
            // Outward in (u, v) is to the right of an edge on a counter-clockwise profile.
            double sign = area > 0 ? 1 : -1;
            for (int e = 0; e < m; e++)
            {
                int g = (e + 1) % m;
                double du = profile[2 * g] - profile[2 * e], dv = profile[2 * g + 1] - profile[2 * e + 1];
                double ou = dv * sign, ov = -du * sign;
                int f = Count;
                for (int i = 0; i < n; i++)
                {
                    double x = path[3 * i], y = path[3 * i + 1], z = path[3 * i + 2], lx = left[2 * i], lz = left[2 * i + 1];
                    V(x + lx * profile[2 * e], y + profile[2 * e + 1], z + lz * profile[2 * e], colours[e]);
                    V(x + lx * profile[2 * g], y + profile[2 * g + 1], z + lz * profile[2 * g], colours[e]);
                }
                for (int i = 0; i + 1 < n; i++)
                {
                    double lx = (left[2 * i] + left[2 * i + 2]) / 2, lz = (left[2 * i + 1] + left[2 * i + 3]) / 2;
                    Quad(f + 2 * i, f + 2 * i + 1, f + 2 * i + 3, f + 2 * i + 2, lx * ou, ov, lz * ou);
                }
            }
            if (!capStart && !capEnd) return;
            flat.Clear();
            for (int e = 0; e < m; e++) { flat.Add(profile[2 * e]); flat.Add(profile[2 * e + 1]); }
            tris.Clear();
            Earcut.Triangulate(flat, Array.Empty<int>(), tris);
            for (int end = 0; end < 2; end++)
            {
                if (end == 0 ? !capStart : !capEnd) continue;
                int i = end == 0 ? 0 : n - 1, j = end == 0 ? 1 : n - 2;
                double tx = path[3 * i] - path[3 * j], ty = path[3 * i + 1] - path[3 * j + 1], tz = path[3 * i + 2] - path[3 * j + 2];
                double x = path[3 * i], y = path[3 * i + 1], z = path[3 * i + 2], lx = left[2 * i], lz = left[2 * i + 1];
                int f = Count;
                for (int e = 0; e < m; e++) V(x + lx * profile[2 * e], y + profile[2 * e + 1], z + lz * profile[2 * e], capColour);
                for (int t = 0; t < tris.Count; t += 3) Tri(f + tris[t], f + tris[t + 1], f + tris[t + 2], tx, ty, tz);
            }
        }

        /// <summary>The mean of four vertices' positions (repeat one for three).</summary>
        void Mid(int a, int b, int c, int d, out double x, out double y, out double z)
        {
            x = (P[3 * a] + P[3 * b] + P[3 * c] + P[3 * d]) / 4;
            y = (P[3 * a + 1] + P[3 * b + 1] + P[3 * c + 1] + P[3 * d + 1]) / 4;
            z = (P[3 * a + 2] + P[3 * b + 2] + P[3 * c + 2] + P[3 * d + 2]) / 4;
        }

        /// <summary>Two unit vectors at right angles to the unit direction d and to each other.</summary>
        public static void Frame(double dx, double dy, double dz, out double ux, out double uy, out double uz, out double vx, out double vy, out double vz)
        {
            // Helper axis: up, unless d is nearly vertical.
            double hx = 0, hy = 1, hz = 0;
            if (Math.Abs(dy) > 0.9) { hx = 1; hy = 0; }
            ux = dy * hz - dz * hy; uy = dz * hx - dx * hz; uz = dx * hy - dy * hx;
            double l = Math.Sqrt(ux * ux + uy * uy + uz * uz);
            ux /= l; uy /= l; uz /= l;
            vx = dy * uz - dz * uy; vy = dz * ux - dx * uz; vz = dx * uy - dy * ux;
        }
    }

    /// <summary>A small deterministic random sequence (SplitMix64) for kit variation.</summary>
    public struct KitRandom
    {
        ulong s;
        public KitRandom(long seed) { s = unchecked((ulong)seed * 0x9E3779B97F4A7C15UL + 0x632BE59BD9B4E019UL); }

        /// <summary>A number in [0, 1).</summary>
        public double Next()
        {
            ulong z = s += 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            z ^= z >> 31;
            return (z >> 11) * (1.0 / (1UL << 53));
        }

        public double Range(double a, double b) => a + (b - a) * Next();
    }

    public static class StreetKit
    {
        // Albedo, sRGB, this port's choice.
        public const uint PoleConcrete = 0x8e8a80, Crossarm = 0x5b5955, Porcelain = 0x8f7f6c, Conductor = 0x3b3b39, Telecom = 0x202020,
            LampSteel = 0x8d9092, LampHead = 0x4a4c4f, Lens = 0xcfcbbd, Footing = 0x86827a, TransformerTank = 0x6f746f, Bark = 0x4f4236;
        public static readonly uint[] Leaves = { 0x3d5a2a, 0x48652e, 0x35502a, 0x506b34 };

        /// <summary>Utility pole: height above its foot, the crossarms' heights, and the telecom bundles' height.</summary>
        public const double PoleHeight = 10, HvArm = 9.35, LvArm = 8.0, TelecomHeight = 6.6, PoleFoot = -0.2;
        /// <summary>Cables a span carries: 3 conductors on the 11 kV arm, 2 on the low-voltage arm, 2 telecom bundles.</summary>
        public const int Anchors = 7;
        public const double LampColumnHeight = 7.6, LampOutreach = 1.5;

        internal static uint M(uint rgb, WorldMaterial m) => WorldMaterials.Pack(rgb, m);

        // ---- The rooftop kit (D-06b, Rooftops.cs) ----

        // Albedo, sRGB, this port's choice: plastic tanks in the common colours, with no maker's marks.
        public const uint TankBlack = 0x26282a, TankBlue = 0x2f5b8f, TankWhite = 0xcfcdc5, TankSteel = 0xa3a6a8,
            SteelDoor = 0x4a4f52, Condenser = 0xd3d2cb, FanGrille = 0x2b2c2d, DishWhite = 0xc8c8c2, Bracket = 0x55585a;
        /// <summary>A condenser's half length and half depth, and its height; the dish's mount height.</summary>
        public const double AcLength = 0.43, AcDepth = 0.17, AcHeight = 0.62, DishMount = 1.1;

        /// <summary>
        /// A stair or lift room at (x, z) on roof y, half lengths l along (ax, az) and wd across, h tall, in the
        /// building's wall colour: a slab on top overhanging a little, and a steel door on its left long side. Far, a box.
        /// </summary>
        public static void RoofRoom(KitWriter w, double x, double y, double z, double ax, double az, double l, double wd, double h, uint wall, int lod)
        {
            if (lod > 0) { w.Post(x, z, y, y + h, ax, az, l, wd, l, wd, wall); return; }
            w.Post(x, z, y, y + h - 0.12, ax, az, l, wd, l, wd, wall);
            w.Post(x, z, y + h - 0.12, y + h, ax, az, l + 0.12, wd + 0.12, l + 0.12, wd + 0.12, M(BuildingSkin.Coping, WorldMaterial.Concrete));
            // The door: on the left side (-az, ax), a little proud of the wall, 0.9 x 2.0 m.
            double dx = x - az * (wd + 0.02), dz = z + ax * (wd + 0.02);
            w.Post(dx, dz, y + 0.05, y + 2.05, ax, az, 0.45, 0.03, 0.45, 0.03, M(SteelDoor, WorldMaterial.Metal));
        }

        /// <summary>A water tank of radius r and height h standing on y at (x, z): near, on a low steel stand with its lid.</summary>
        public static void Tank(KitWriter w, double x, double y, double z, double r, double h, uint colour, int lod)
        {
            if (lod > 0) { w.Tube(x, y, z, x, y + h, z, r, r * 0.92, 6, colour); return; }
            uint steel = M(Bracket, WorldMaterial.Metal);
            w.Post(x, z, y, y + 0.18, 1, 0, r * 0.8, r * 0.8, r * 0.8, r * 0.8, steel);
            w.Tube(x, y + 0.18, z, x, y + 0.18 + h, z, r, r * 0.92, 10, colour);
            // The lid.
            w.Tube(x, y + 0.18 + h, z, x, y + 0.26 + h, z, r * 0.4, r * 0.36, 6, colour);
        }

        /// <summary>A condenser at (x, z) on roof y, its long side along (ax, az), the fan facing to its left; on a low frame.</summary>
        public static void Ac(KitWriter w, double x, double y, double z, double ax, double az, int lod)
        {
            uint case_ = M(Condenser, WorldMaterial.Paint);
            if (lod > 0) { w.Post(x, z, y, y + AcHeight, ax, az, AcLength, AcDepth, AcLength, AcDepth, case_); return; }
            w.Post(x, z, y, y + 0.1, ax, az, AcLength - 0.03, AcDepth - 0.02, AcLength - 0.03, AcDepth - 0.02, M(Bracket, WorldMaterial.Metal));
            w.Post(x, z, y + 0.1, y + AcHeight, ax, az, AcLength, AcDepth, AcLength, AcDepth, case_);
            // The fan grille, off centre towards one end.
            double fx = x + ax * 0.1 - az * AcDepth, fz = z + az * 0.1 + ax * AcDepth, fy = y + 0.1 + (AcHeight - 0.1) / 2;
            w.Tube(fx, fy, fz, fx - az * 0.015, fy, fz + ax * 0.015, 0.2, 0.2, 8, M(FanGrille, WorldMaterial.Metal));
        }

        /// <summary>A satellite dish on a post at (x, z) on roof y, facing the azimuth and elevation (degrees). Near only.</summary>
        public static void Dish(KitWriter w, double x, double y, double z, double azimuth, double elevation)
        {
            double a = azimuth * Math.PI / 180, e = elevation * Math.PI / 180;
            double dx = Math.Sin(a) * Math.Cos(e), dy = Math.Sin(e), dz = Math.Cos(a) * Math.Cos(e);
            uint steel = M(Bracket, WorldMaterial.Metal), dish = M(DishWhite, WorldMaterial.Paint);
            w.Post(x, z, y, y + 0.05, 1, 0, 0.18, 0.18, 0.18, 0.18, steel);
            w.Tube(x, y, z, x, y + DishMount, z, 0.03, 0.03, 6, steel);
            double cx = x + dx * 0.12, cy = y + DishMount + dy * 0.12, cz = z + dz * 0.12;
            // A shallow bowl: a cone from its back to its rim, then the feed arm to the receiver.
            w.Tube(cx - dx * 0.14, cy - dy * 0.14, cz - dz * 0.14, cx, cy, cz, 0.06, 0.36, 10, dish);
            w.Tube(cx, cy, cz, cx + dx * 0.42, cy + dy * 0.42, cz + dz * 0.42, 0.012, 0.012, 4, steel);
            w.Tube(cx + dx * 0.42, cy + dy * 0.42, cz + dz * 0.42, cx + dx * 0.52, cy + dy * 0.52, cz + dz * 0.52, 0.035, 0.03, 6, steel);
        }

        /// <summary>
        /// Where cable k leaves a pole standing at (x, y, z), its street running along (ax, az): the 11 kV
        /// conductors on the top crossarm, the low-voltage pair below, the telecom bundles on the pole's
        /// faces. The radius and sag per metre of span are also given.
        /// </summary>
        public static void Anchor(int k, double x, double y, double z, double ax, double az, out double px, out double py, out double pz, out double radius, out double sag, out uint colour)
        {
            double bx = -az, bz = ax, across, up;
            switch (k)
            {
                case 0: case 1: case 2: across = (k - 1) * 0.8; up = HvArm + 0.29; radius = 0.02; sag = 0.012; colour = M(Conductor, WorldMaterial.Metal); break;
                case 3: case 4: across = (k == 3 ? -0.5 : 0.5); up = LvArm + 0.17; radius = 0.02; sag = 0.016; colour = M(Conductor, WorldMaterial.Metal); break;
                default: across = (k == 5 ? 0.17 : -0.17); up = TelecomHeight - (k - 5) * 0.35; radius = 0.035; sag = 0.03; colour = M(Telecom, WorldMaterial.Default); break;
            }
            px = x + bx * across;
            py = y + up;
            pz = z + bz * across;
        }

        /// <summary>
        /// A prestressed concrete pole at (x, z) on ground y, its street along (ax, az). The low-voltage
        /// arm and lamp arm reach towards roadSide (+1 left of (ax, az), -1 right). Optional: a lamp arm and
        /// head, and a pole-mounted transformer.
        /// </summary>
        public static void UtilityPole(KitWriter w, double x, double y, double z, double ax, double az, int roadSide, bool lamp, bool transformer, int lod)
        {
            uint concrete = M(PoleConcrete, WorldMaterial.Concrete), steel = M(Crossarm, WorldMaterial.Metal);
            double bx = -az, bz = ax;
            // Tapered rectangular shaft, the wider face across the street.
            w.Post(x, z, y + PoleFoot, y + PoleHeight, ax, az, 0.11, 0.16, 0.07, 0.09, concrete);
            if (lod > 0)
            {
                w.Beam(x - bx * 0.95, z - bz * 0.95, x + bx * 0.95, z + bz * 0.95, 0.05, y + HvArm, y + HvArm + 0.1, y + HvArm, y + HvArm + 0.1, steel);
                return;
            }
            w.Post(x, z, y + PoleFoot, y + 0.35, ax, az, 0.2, 0.25, 0.2, 0.25, concrete); // collar where it enters the ground
            uint porcelain = M(Porcelain, WorldMaterial.Tiles);
            // 11 kV arm across the street with three pin insulators.
            w.Beam(x - bx * 1.0, z - bz * 1.0, x + bx * 1.0, z + bz * 1.0, 0.045, y + HvArm, y + HvArm + 0.09, y + HvArm, y + HvArm + 0.09, steel);
            for (int k = -1; k <= 1; k++)
                w.Tube(x + bx * k * 0.8, y + HvArm + 0.09, z + bz * k * 0.8, x + bx * k * 0.8, y + HvArm + 0.27, z + bz * k * 0.8, 0.055, 0.04, 6, porcelain);
            // Braces from the shaft to the arm.
            for (int s = -1; s <= 1; s += 2)
                w.Beam(x, z, x + bx * s * 0.55, z + bz * s * 0.55, 0.015, y + HvArm - 0.55, y + HvArm - 0.5, y + HvArm, y + HvArm + 0.04, steel);
            // Low-voltage arm with two shackle insulators.
            w.Beam(x - bx * 0.65, z - bz * 0.65, x + bx * 0.65, z + bz * 0.65, 0.04, y + LvArm, y + LvArm + 0.08, y + LvArm, y + LvArm + 0.08, steel);
            for (int s = -1; s <= 1; s += 2)
                w.Tube(x + bx * s * 0.5, y + LvArm + 0.08, z + bz * s * 0.5, x + bx * s * 0.5, y + LvArm + 0.2, z + bz * s * 0.5, 0.045, 0.045, 6, porcelain);
            // Telecom clamp bands where the bundles are tied on.
            uint black = M(Telecom, WorldMaterial.Default);
            for (int k = 0; k < 2; k++)
                w.Post(x, z, y + TelecomHeight - k * 0.35 - 0.06, y + TelecomHeight - k * 0.35 + 0.06, ax, az, 0.1, 0.12, 0.1, 0.12, black);
            if (lamp) LampArm(w, x + bx * roadSide * 0.1, y + LvArm + 0.6, z + bz * roadSide * 0.1, bx * roadSide, bz * roadSide, 1.3, 0);
            if (transformer)
            {
                // A pole-mounted distribution transformer on the side away from the road, with bushings and fins.
                double tx = x - bx * roadSide * 0.55, tz = z - bz * roadSide * 0.55;
                uint tank = M(TransformerTank, WorldMaterial.Paint);
                w.Post(tx, tz, y + 4.4, y + 5.5, ax, az, 0.42, 0.3, 0.42, 0.3, tank);
                w.Post(tx, tz, y + 5.5, y + 5.6, ax, az, 0.46, 0.34, 0.46, 0.34, tank);
                for (int k = -1; k <= 1; k++)
                    w.Tube(tx + ax * k * 0.25, y + 5.6, tz + az * k * 0.25, tx + ax * k * 0.25, y + 5.9, tz + az * k * 0.25, 0.04, 0.03, 6, porcelain);
                for (int s = -1; s <= 1; s += 2)
                    w.Post(tx + ax * s * 0.46, tz + az * s * 0.46, y + 4.5, y + 5.4, ax, az, 0.03, 0.22, 0.03, 0.22, tank);
                w.Beam(x, z, tx, tz, 0.06, y + 4.3, y + 4.4, y + 4.3, y + 4.4, steel);
            }
        }

        /// <summary>An arm from (x, y, z) reaching out along (fx, fz) and rising a little, with an LED head at its end.</summary>
        static void LampArm(KitWriter w, double x, double y, double z, double fx, double fz, double reach, int lod)
        {
            uint steel = M(LampSteel, WorldMaterial.Metal), head = M(LampHead, WorldMaterial.Metal), lens = M(Lens, WorldMaterial.Glass);
            double ex = x + fx * reach, ey = y + 0.3, ez = z + fz * reach;
            if (lod == 0) w.Tube(x, y, z, ex, ey, ez, 0.035, 0.03, 6, steel);
            else w.Beam(x, z, ex, ez, 0.03, y, y + 0.06, ey, ey + 0.06, steel);
            // Head: 0.62 m long, 0.24 m wide, tilted 5 degrees up at its far end.
            double hx = ex + fx * 0.3, hz = ez + fz * 0.3;
            w.Beam(ex - fx * 0.02, ez - fz * 0.02, ex + fx * 0.6, ez + fz * 0.6, 0.12, ey - 0.06, ey + 0.04, ey - 0.01, ey + 0.08, head);
            if (lod > 0) return;
            double nx = -fz * 0.09, nz = fx * 0.09;
            double y0 = ey - 0.07, y1 = ey - 0.025;
            w.Quad(hx - fx * 0.25 + nx, y0, hz - fz * 0.25 + nz, hx + fx * 0.25 + nx, y1, hz + fz * 0.25 + nz,
                hx + fx * 0.25 - nx, y1, hz + fz * 0.25 - nz, hx - fx * 0.25 - nx, y0, hz - fz * 0.25 - nz, lens, 0, -1, 0);
        }

        /// <summary>Where a lamp's head ends up on the arm LampArm draws from (x, y, z): the middle of its lens.</summary>
        static ChunkStreet.LampHead Head(double x, double y, double z, double fx, double fz, double reach) =>
            new ChunkStreet.LampHead { X = x + fx * (reach + 0.3), Y = y + 0.25, Z = z + fz * (reach + 0.3), Fx = fx, Fz = fz };

        /// <summary>The head of the lamp StreetLight draws at (x, z) on ground y.</summary>
        public static ChunkStreet.LampHead ColumnHead(double x, double y, double z, double fx, double fz) => Head(x, y + LampColumnHeight - 0.2, z, fx, fz, LampOutreach);

        /// <summary>The head of the lamp UtilityPole draws at (x, z) on ground y.</summary>
        public static ChunkStreet.LampHead PoleHead(double x, double y, double z, double ax, double az, int roadSide)
        {
            double fx = -az * roadSide, fz = ax * roadSide;
            return Head(x + fx * 0.1, y + LvArm + 0.6, z + fz * 0.1, fx, fz, 1.3);
        }

        /// <summary>A galvanised steel lamp column at (x, z) on ground y, with its arm and head over (fx, fz).</summary>
        public static void StreetLight(KitWriter w, double x, double y, double z, double fx, double fz, int lod)
        {
            uint steel = M(LampSteel, WorldMaterial.Metal);
            double top = y + LampColumnHeight;
            if (lod > 0)
            {
                w.Post(x, z, y + PoleFoot, top, fx, fz, 0.07, 0.07, 0.04, 0.04, steel);
                LampArm(w, x, top - 0.2, z, fx, fz, LampOutreach, 1);
                return;
            }
            w.Post(x, z, y + PoleFoot, y + 0.12, fx, fz, 0.28, 0.28, 0.25, 0.25, M(Footing, WorldMaterial.Concrete));
            w.Post(x, z, y + 0.12, y + 0.15, fx, fz, 0.17, 0.17, 0.17, 0.17, steel);
            w.Tube(x, y + 0.12, z, x, top, z, 0.085, 0.048, 8, steel);
            w.Tube(x, y + 0.15, z, x, y + 0.9, z, 0.1, 0.095, 8, steel); // door section at the foot
            LampArm(w, x, top - 0.2, z, fx, fz, LampOutreach, 0);
        }

        /// <summary>The three tree forms: wide umbrella crown, upright oval, round.</summary>
        public const int TreeForms = 3;

        /// <summary>A tree at (x, z) on ground y, height h, form 0..2, varied by seed.</summary>
        public static void Tree(KitWriter w, double x, double y, double z, double h, int form, long seed, int lod)
        {
            var rnd = new KitRandom(seed);
            double trunkTop, crownY, rh, rv;
            int blobs;
            switch (form)
            {
                case 0: trunkTop = 0.4; crownY = 0.68; rh = 0.5; rv = 0.24; blobs = 70; break;
                case 1: trunkTop = 0.3; crownY = 0.62; rh = 0.22; rv = 0.36; blobs = 55; break;
                default: trunkTop = 0.35; crownY = 0.6; rh = 0.36; rv = 0.3; blobs = 60; break;
            }
            trunkTop *= h; crownY = y + crownY * h; rh *= h; rv *= h;
            // The crown leans a little off the trunk.
            double lean = rnd.Range(0, 2 * Math.PI), off = rnd.Range(0, 0.06) * h;
            double cx = x + Math.Cos(lean) * off, cz = z + Math.Sin(lean) * off;
            uint bark = M(Bark, WorldMaterial.Default);
            double r0 = 0.1 + 0.014 * h;
            if (lod > 0)
            {
                w.Tube(x, y + PoleFoot, z, cx, y + trunkTop, cz, r0, r0 * 0.6, 4, bark);
                for (int i = 0; i < 7; i++)
                {
                    double a = 2 * Math.PI * i / 6 + rnd.Range(0, 0.5), d = i == 6 ? 0 : 0.55;
                    double bx = cx + Math.Cos(a) * rh * d, bz = cz + Math.Sin(a) * rh * d, by = crownY + (i == 6 ? rv * 0.35 : rnd.Range(-0.25, 0.15) * rv);
                    w.Blob(bx, by, bz, rh * 0.62, rv * 0.75, rh * 0.62, rnd.Range(0, Math.PI), rnd.Range(-0.3, 0.3), M(Leaves[i % Leaves.Length], WorldMaterial.Grass));
                }
                return;
            }
            w.Tube(x, y + PoleFoot, z, cx, y + trunkTop, cz, r0, r0 * 0.62, 7, bark);
            // Root flare.
            w.Tube(x, y + PoleFoot, z, x, y + 0.35, z, r0 * 1.45, r0 * 1.02, 7, bark);
            int branches = 3 + (int)(rnd.Next() * 3);
            for (int b = 0; b < branches; b++)
            {
                double a = 2 * Math.PI * (b + rnd.Range(0, 0.6)) / branches;
                double ex = cx + Math.Cos(a) * rh * 0.55, ez = cz + Math.Sin(a) * rh * 0.55, ey = crownY + rnd.Range(-0.1, 0.35) * rv;
                w.Tube(cx, y + trunkTop - 0.2, cz, ex, ey, ez, r0 * 0.55, r0 * 0.2, 5, bark);
            }
            // A few large dark clusters fill the crown's core, so the sky does not show through it.
            for (int i = 0; i < 5; i++)
            {
                double a = 2 * Math.PI * i / 5 + rnd.Range(0, 0.6);
                double px = cx + Math.Cos(a) * rh * 0.3, pz = cz + Math.Sin(a) * rh * 0.3, py = crownY + rnd.Range(-0.2, 0.2) * rv;
                w.Blob(px, py, pz, rh * 0.5, rv * 0.6, rh * 0.5, rnd.Range(0, Math.PI), rnd.Range(-0.3, 0.3), BuildingPalette.Shade(Leaves[i % Leaves.Length], 0.68, WorldMaterial.Grass));
            }
            // Then many small leaf clumps, three times the form's count, mostly near the skin.
            blobs *= 3;
            double size = Math.Clamp(rh * 0.11, 0.3, 0.85);
            for (int i = 0; i < blobs; i++)
            {
                // A point in the crown's ellipsoid, denser towards its skin.
                double u = rnd.Next(), v = rnd.Next(), t = Math.Pow(rnd.Next(), 0.35);
                double th = 2 * Math.PI * u, ph = Math.Acos(2 * v - 1);
                double px = cx + Math.Sin(ph) * Math.Cos(th) * rh * t, pz = cz + Math.Sin(ph) * Math.Sin(th) * rh * t, py = crownY + Math.Cos(ph) * rv * t;
                double s = size * rnd.Range(0.75, 1.25);
                uint leaf = Leaves[(int)(rnd.Next() * Leaves.Length)];
                // Leaves lower in the crown are a little darker.
                double f = 0.82 + 0.18 * (py - (crownY - rv)) / (2 * rv);
                w.Blob(px, py, pz, s, s * 0.7, s, rnd.Range(0, Math.PI), rnd.Range(-0.6, 0.6), BuildingPalette.Shade(leaf, Math.Clamp(f, 0.7, 1), WorldMaterial.Grass));
            }
        }

        /// <summary>
        /// Writes a sagging cable from a to b into pts (x, y, z triples, segments + 1 points): a
        /// parabola, sag metres below the chord at its middle. With half true, only the first half,
        /// from a down to the lowest point, where it is level (a span cut by the chunk border).
        /// </summary>
        public static int Catenary(Span<double> pts, double ax, double ay, double az, double bx, double by, double bz, double sag, int segments, bool half)
        {
            for (int i = 0; i <= segments; i++)
            {
                double t = (double)i / segments;
                if (half) t *= 0.5;
                pts[3 * i] = ax + (bx - ax) * t;
                pts[3 * i + 1] = ay + (by - ay) * t - 4 * sag * t * (1 - t);
                pts[3 * i + 2] = az + (bz - az) * t;
            }
            return segments + 1;
        }
    }
}
