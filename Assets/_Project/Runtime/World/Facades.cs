// One facade family and its ground-floor shops (Milestone 2, Farmgate validation), on the buildings
// standing on the ground, two levels or more, whose footprint's centre is in ValidationArea. D-06
// takes facades city-wide once buildings have levels of detail.
//
// The footprint and height are kept: each outer wall is drawn in the wall's own plane as before,
// with openings cut into it, and everything else is set into the wall or onto its face.
// - Floors: the building's levels (BuildingRules), each the height divided evenly; a band 12 cm
//   high and 5 cm proud at each floor line, and a parapet on the roof edge: the roof is drawn
//   ParapetHeight below the building's top, so the top stays where the data puts it.
// - Bays: the wall divided into bays about BayWidth wide. Each bay of each floor above the ground
//   (and of the ground floor on a wall that does not face a road) has a window: an opening set
//   RevealDepth into the wall, glass at the back, a sill, and a steel security grille just inside
//   the face, as on most Dhaka walk-ups.
// - Shops: on a wall facing a vehicle road (a road's carriageway within 11 m straight out from the
//   wall), each ground-floor bay is a shop: an opening ShopDepth deep with a floor, a rolling
//   shutter closed, half open or open (by hash), its box, a signboard and, on one in two, an awning.
//   Signboards are blank: no lettering until real signage is designed (no pseudo-Bengali).
// Walls shorter than MinFacadeWall and walls cut along the chunk border stay plain.
using System;
using System.Collections.Generic;

namespace Dad.World
{
    public sealed partial class ChunkBuildings
    {
        /// <summary>The validation area, in world metres east and north: around the Farmgate footbridge.</summary>
        public const double AreaWest = 3940, AreaEast = 4140, AreaSouth = 14600, AreaNorth = 14850;
        public const double ParapetHeight = 0.9, BayWidth = 3.0, MinFacadeWall = 2.5, RevealDepth = 0.15, ShopDepth = 0.35, ShopFacing = 11;
        public const uint Glass = 0x2f3a40, GlassLit = 0x6b6a5e, Grille = 0x2c2c2b, Sill = 0x9c978d, Band = 0x8f8a80, ShopFloor = 0x77736b,
            ShopInside = 0x35302b, Shutter = 0x8a8d8f, ShutterBox = 0x6f7275;
        static readonly uint[] SignColours = { 0xb0302a, 0x2f7a3a, 0x2c4f8f, 0xd2b23a, 0xe6e2d6, 0x7a2f6b };

        public int Facades, Windows, Shops;
        KitWriter kit;

        /// <summary>Does this building get the facade family: on the ground, two levels or more, centred in the validation area?</summary>
        public static bool Facaded(DadcChunk c, Building b, BuildingRules.Placement pl)
        {
            if (pl.Base > 0 || pl.Top < 6 || b.Outer == null || b.Outer.Length < 6) return false;
            double x = 0, z = 0;
            int n = b.Outer.Length / 2;
            for (int i = 0; i < n; i++) { x += b.Outer[2 * i]; z += b.Outer[2 * i + 1]; }
            x = c.CornerEastCm / 100.0 + x / n / 100;
            z = c.CornerNorthCm / 100.0 + z / n / 100;
            return x >= AreaWest && x <= AreaEast && z >= AreaSouth && z <= AreaNorth;
        }

        /// <summary>The outer ring's walls as facades (see the file header).</summary>
        void FacadeWalls(DadcChunk c, Building b, List<double> flat, int start, int end, double roof, double top, uint tone)
        {
            kit ??= new KitWriter(p, col, idx);
            kit.P = p; kit.C = col; kit.I = idx; kit.Base = 0;
            int count = end - start;
            bool right = Area(flat, start, end, signed: true) > 0;
            int floors = Math.Max(2, (int)Math.Round(roof / BuildingRules.MetresPerLevel));
            double fh = roof / floors;
            uint wall = BuildingPalette.Shade(tone, 1, WorldMaterial.Plaster), foot = BuildingPalette.Shade(tone, BuildingPalette.Foot, WorldMaterial.Plaster);
            for (int k = 0; k < count; k++)
            {
                int a = start + k, q = start + (k + 1) % count;
                double px = flat[2 * a], pz = flat[2 * a + 1], qx = flat[2 * q], qz = flat[2 * q + 1];
                double dx = qx - px, dz = qz - pz, len = Math.Sqrt(dx * dx + dz * dz);
                if (len < 1e-4) continue;
                double ux = dx / len, uz = dz / len, nx = right ? uz : -uz, nz = right ? -ux : ux;
                var f = new Face { Px = px, Pz = pz, Ux = ux, Uz = uz, Nx = nx, Nz = nz, Wall = wall, Foot = foot };
                if (len < MinFacadeWall || OnChunkEdge(px, pz, qx, qz))
                {
                    f.Panel(kit, 0, len, 0, top);
                    continue;
                }
                bool shops = FacesRoad(c, px + dx / 2, pz + dz / 2, nx, nz);
                int bays = Math.Max(1, (int)Math.Floor(len / BayWidth));
                double bw = len / bays;
                for (int fl = 0; fl < floors; fl++)
                    for (int bay = 0; bay < bays; bay++)
                    {
                        double s0 = bay * bw, s1 = s0 + bw, h0 = fl * fh, h1 = h0 + fh;
                        long seed = b.Id * 977 + k * 131 + fl * 17 + bay;
                        if (fl == 0 && shops && fh >= 2.8 && bw >= 2) { Shop(f, s0, s1, h0, h1, seed); Shops++; }
                        else if (fh >= 2.4 && bw >= 1.4) { Window(f, s0, s1, h0, h1, seed); Windows++; }
                        else f.Panel(kit, s0, s1, h0, h1);
                    }
                uint band = BuildingPalette.Shade(Band, 1, WorldMaterial.Concrete);
                for (int fl = 1; fl < floors; fl++) f.Block(kit, 0, len, fl * fh - 0.06, fl * fh + 0.06, 0, 0.05, band);
                f.Block(kit, 0, len, roof, top, -0.15, 0, wall);
            }
            Facades++;
        }

        void Window(Face f, double s0, double s1, double h0, double h1, long seed)
        {
            var rnd = new KitRandom(seed);
            double mid = (s0 + s1) / 2, ww = Math.Min(1.5, s1 - s0 - 0.9), wh = Math.Min(1.5, h1 - h0 - 1.3);
            double a = mid - ww / 2, b = mid + ww / 2, lo = h0 + 0.9, hi = lo + wh;
            f.Opening(kit, s0, s1, h0, h1, a, b, lo, hi, RevealDepth, true);
            // Glass: most panes dark, some lighter (curtains, a lit room behind).
            double lit = rnd.Next();
            uint glass = WorldMaterials.Night(WorldMaterials.Pack(lit < 0.2 ? GlassLit : Glass, WorldMaterial.Glass), 1 + (uint)(lit * 5));
            f.Pane(kit, a, b, lo, hi, -RevealDepth, glass);
            f.Block(kit, a - 0.06, b + 0.06, lo - 0.07, lo, 0, 0.06, WorldMaterials.Pack(Sill, WorldMaterial.Concrete));
            uint grille = WorldMaterials.Pack(Grille, WorldMaterial.Metal);
            int bars = Math.Max(2, (int)Math.Round(ww / 0.3));
            for (int i = 1; i < bars; i++)
            {
                double s = a + ww * i / bars;
                f.Pane(kit, s - 0.012, s + 0.012, lo, hi, -0.03, grille);
            }
            f.Pane(kit, a, b, lo + wh * 0.5 - 0.012, lo + wh * 0.5 + 0.012, -0.025, grille);
        }

        void Shop(Face f, double s0, double s1, double h0, double h1, long seed)
        {
            var rnd = new KitRandom(seed);
            double a = s0 + 0.25, b = s1 - 0.25, hs = Math.Min(2.7, h1 - h0 - 0.5);
            f.Opening(kit, s0, s1, h0, h1, a, b, h0, h0 + hs, ShopDepth, false);
            f.Floor(kit, a, b, h0 + 0.02, ShopDepth, WorldMaterials.Pack(ShopFloor, WorldMaterial.Concrete));
            f.Pane(kit, a, b, h0, h0 + hs, -ShopDepth, WorldMaterials.Night(WorldMaterials.Pack(ShopInside, WorldMaterial.Default), WorldMaterials.NightShop));
            // Shutter: closed, half or rolled up.
            double r = rnd.Next(), down = r < 0.35 ? h0 : r < 0.6 ? h0 + 1.2 : h0 + hs - 0.3;
            uint metal = WorldMaterials.Pack(Shutter, WorldMaterial.Metal);
            f.Pane(kit, a, b, down, h0 + hs, -0.1, metal);
            f.Block(kit, a - 0.05, b + 0.05, h0 + hs - 0.05, h0 + hs + 0.25, 0, 0.12, WorldMaterials.Pack(ShutterBox, WorldMaterial.Metal));
            double sb = h0 + hs + 0.35, st = Math.Min(h1 - 0.1, sb + 0.65);
            double pick = rnd.Next() * SignColours.Length;
            uint sign = WorldMaterials.Pack(SignColours[(int)pick], WorldMaterial.Paint);
            if (pick - Math.Floor(pick) < ChunkBuildings.LitSignShare) sign = WorldMaterials.Night(sign, WorldMaterials.NightSign);
            if (st - sb > 0.3) f.Block(kit, a - 0.1, b + 0.1, sb, st, 0, 0.08, sign);
            if (rnd.Next() < 0.5)
            {
                uint cloth = WorldMaterials.Pack(SignColours[(int)(rnd.Next() * SignColours.Length)], WorldMaterial.Paint);
                f.Awning(kit, a - 0.1, b + 0.1, h0 + hs + 0.3, h0 + hs - 0.05, 0.9, cloth);
            }
        }

        static bool OnChunkEdge(double px, double pz, double qx, double qz)
        {
            const double e = 0.02, size = DadcChunk.ChunkCm / 100.0;
            return Math.Abs(px - qx) < e && (px < e || px > size - e) || Math.Abs(pz - qz) < e && (pz < e || pz > size - e);
        }

        /// <summary>Is a vehicle road's carriageway at grade within ShopFacing metres straight out from the wall at (x, z)?</summary>
        static bool FacesRoad(DadcChunk c, double x, double z, double nx, double nz)
        {
            if (c.Roads == null) return false;
            foreach (var r in c.Roads)
            {
                if (r.Bridge || r.YCm >= 100 || r.Pts == null || RoadPalette.IsFoot(r.Cls) || r.Cls == "service" || r.Cls == "construction") continue;
                double half = r.WidthCm / 200.0;
                for (double d = 1; d <= ShopFacing; d += 2)
                {
                    double tx = x + nx * d, tz = z + nz * d;
                    for (int i = 0; i + 3 < r.Pts.Length; i += 2)
                    {
                        double ax = r.Pts[i] / 100.0, az = r.Pts[i + 1] / 100.0, ux = r.Pts[i + 2] / 100.0 - ax, uz = r.Pts[i + 3] / 100.0 - az, ll = ux * ux + uz * uz;
                        if (ll < 1e-9) continue;
                        double t = Math.Clamp(((tx - ax) * ux + (tz - az) * uz) / ll, 0, 1);
                        double ex = ax + ux * t - tx, ez = az + uz * t - tz;
                        if (ex * ex + ez * ez <= half * half) return true;
                    }
                }
            }
            return false;
        }

        /// <summary>A wall's frame: origin, unit direction along it and its outward normal, and its plaster colours.</summary>
        struct Face
        {
            public double Px, Pz, Ux, Uz, Nx, Nz;
            public uint Wall, Foot;

            double X(double s, double d) => Px + Ux * s + Nx * d;
            double Z(double s, double d) => Pz + Uz * s + Nz * d;
            uint Tone(double h) => h < 0.01 ? Foot : Wall;

            /// <summary>A flat piece of wall in its plane, from s0 to s1 along and h0 to h1 up.</summary>
            public void Panel(KitWriter w, double s0, double s1, double h0, double h1)
            {
                if (s1 - s0 < 1e-4 || h1 - h0 < 1e-4) return;
                int a = w.V(X(s0, 0), h0, Z(s0, 0), Tone(h0)), b = w.V(X(s1, 0), h0, Z(s1, 0), Tone(h0));
                int c = w.V(X(s1, 0), h1, Z(s1, 0), Tone(h1)), d = w.V(X(s0, 0), h1, Z(s0, 0), Tone(h1));
                w.Quad(a, b, c, d, Nx, 0, Nz);
            }

            /// <summary>The wall of one bay around an opening (a to b, lo to hi), and the opening's reveals, depth deep; with a sill reveal when sill.</summary>
            public void Opening(KitWriter w, double s0, double s1, double h0, double h1, double a, double b, double lo, double hi, double depth, bool sill)
            {
                Panel(w, s0, a, h0, h1);
                Panel(w, b, s1, h0, h1);
                Panel(w, a, b, h0, lo);
                Panel(w, a, b, hi, h1);
                uint r = Wall;
                // Left and right reveals face into the opening; the head faces down, the sill up.
                w.Quad(X(a, 0), lo, Z(a, 0), X(a, -depth), lo, Z(a, -depth), X(a, -depth), hi, Z(a, -depth), X(a, 0), hi, Z(a, 0), r, Ux, 0, Uz);
                w.Quad(X(b, 0), lo, Z(b, 0), X(b, -depth), lo, Z(b, -depth), X(b, -depth), hi, Z(b, -depth), X(b, 0), hi, Z(b, 0), r, -Ux, 0, -Uz);
                w.Quad(X(a, 0), hi, Z(a, 0), X(b, 0), hi, Z(b, 0), X(b, -depth), hi, Z(b, -depth), X(a, -depth), hi, Z(a, -depth), r, 0, -1, 0);
                if (sill) w.Quad(X(a, 0), lo, Z(a, 0), X(b, 0), lo, Z(b, 0), X(b, -depth), lo, Z(b, -depth), X(a, -depth), lo, Z(a, -depth), r, 0, 1, 0);
            }

            /// <summary>A flat piece facing out at depth d (negative: inside the wall).</summary>
            public void Pane(KitWriter w, double s0, double s1, double h0, double h1, double d, uint colour)
            {
                if (s1 - s0 < 1e-4 || h1 - h0 < 1e-4) return;
                w.Quad(X(s0, d), h0, Z(s0, d), X(s1, d), h0, Z(s1, d), X(s1, d), h1, Z(s1, d), X(s0, d), h1, Z(s0, d), colour, Nx, 0, Nz);
            }

            /// <summary>A floor inside an opening, from the face to depth, at height h.</summary>
            public void Floor(KitWriter w, double s0, double s1, double h, double depth, uint colour)
            {
                w.Quad(X(s0, 0), h, Z(s0, 0), X(s1, 0), h, Z(s1, 0), X(s1, -depth), h, Z(s1, -depth), X(s0, -depth), h, Z(s0, -depth), colour, 0, 1, 0);
            }

            /// <summary>A box on the wall from s0 to s1, h0 to h1, between depths d0 and d1 (positive: out of the wall).</summary>
            public void Block(KitWriter w, double s0, double s1, double h0, double h1, double d0, double d1, uint colour)
            {
                Span<double> k = stackalloc double[]
                {
                    X(s0, d0), h0, Z(s0, d0), X(s1, d0), h0, Z(s1, d0), X(s1, d1), h0, Z(s1, d1), X(s0, d1), h0, Z(s0, d1),
                    X(s0, d0), h1, Z(s0, d0), X(s1, d0), h1, Z(s1, d0), X(s1, d1), h1, Z(s1, d1), X(s0, d1), h1, Z(s0, d1),
                };
                w.Hex(k, colour);
            }

            /// <summary>A sloping awning from the wall at height top out to reach at height low, both faces drawn.</summary>
            public void Awning(KitWriter w, double s0, double s1, double top, double low, double reach, uint colour)
            {
                for (int side = 0; side < 2; side++)
                    w.Quad(X(s0, 0.02), top, Z(s0, 0.02), X(s1, 0.02), top, Z(s1, 0.02), X(s1, reach), low, Z(s1, reach), X(s0, reach), low, Z(s0, reach), colour,
                        side == 0 ? Nx * 0.3 : -Nx * 0.3, side == 0 ? 1 : -1, side == 0 ? Nz * 0.3 : -Nz * 0.3);
            }
        }
    }
}
