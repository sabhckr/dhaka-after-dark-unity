// D-06c: near facades. Each building's near style follows its category and its district, the district that of
// the nearest named place; the near facade dresses exactly the windows the far tier paints (same bays, floors and
// bricked-up windows) and stands out of the wall, never further than the room in front of it, nothing at all where
// there is less than MinClear; the shader's window boxes, hash and fade rule are the ones the builder uses, and the
// near material is World.mat with its keyword and compiles in every pass; the slice's near facades stay with their
// cells, within their triangle targets, and are the same every build; Old Dhaka and the modern areas get theirs.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Dad.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace Dad.Tests
{
    public class NearFacadeTests
    {
        static string WorldDir => TestWorld.Root;
        static string ChunkDir => Path.Combine(WorldDir, "chunks");
        const string ShaderPath = "Assets/_Project/Shaders/DadWorld.shader";
        const int Cells = ChunkStreet.Cells * ChunkStreet.Cells;

        [Serializable] class PlaceFile { public Place[] items; }
        [Serializable] class Place { public string nameEn; public double x, z; }

        /// <summary>Sets the districts from places.json, as the streamer does.</summary>
        static void SetPlaces()
        {
            var path = Path.Combine(WorldDir, "places.json");
            Assume.That(File.Exists(path), "world data missing: run Tools/worldgen/build.sh");
            var items = JsonUtility.FromJson<PlaceFile>(File.ReadAllText(path)).items;
            Districts.Set(items.Select(p => p.x).ToArray(), items.Select(p => p.z).ToArray(), items.Select(p => p.nameEn).ToArray());
        }

        [TearDown]
        public void ForgetPlaces() => Districts.Clear();

        static int Cm(double m) => (int)Math.Round(m * 100);

        /// <summary>A rectangle x0..x0+w, z0..z0+d metres in the chunk, counter-clockwise, h metres tall.</summary>
        static Building Rect(long id, double x0, double z0, double w, double d, double h = 16, string type = "apartments") =>
            new Building
            {
                Id = id, HeightCm = Cm(h), HeightSource = "estimated", Type = type,
                Outer = new[] { Cm(x0), Cm(z0), Cm(x0 + w), Cm(z0), Cm(x0 + w), Cm(z0 + d), Cm(x0), Cm(z0 + d) },
            };

        // The chunk at the world origin: chunk metres are world metres.
        static DadcChunk Chunk(params Building[] buildings) =>
            new DadcChunk { Ix = 0, Iy = -1, Buildings = buildings, Roads = new Road[0], WidthsResolved = true };

        /// <summary>The near facade vertices of a cell (after its rooftop kit's), as positions.</summary>
        static List<Vector3> FacadeVertices(NearKit kit)
        {
            var l = new List<Vector3>();
            for (int v = kit.RoofVertexCount; v < kit.VertexCount; v++) l.Add(new Vector3(kit.Positions[3 * v], kit.Positions[3 * v + 1], kit.Positions[3 * v + 2]));
            return l;
        }

        static IEnumerable<ChunkBuildings.FacadeWall> Walls(ChunkBuildings b)
        {
            var seen = new HashSet<int>();
            for (int i = 0; i < b.NearWallStart[Cells]; i++)
                if (seen.Add(b.NearWalls[i].FirstBay)) yield return b.NearWalls[i];
        }

        [Test]
        public void StylesFollowTheBuildingAndItsDistrict()
        {
            var t = new (BuildingCategory, ClassSource, string, District, NearStyle)[]
            {
                (BuildingCategory.Residential, ClassSource.OsmType, "apartments", District.Other, NearStyle.WalkUp),
                (BuildingCategory.Residential, ClassSource.RuleDefault, null, District.OldDhaka, NearStyle.OldDhaka),
                (BuildingCategory.Other, ClassSource.RuleDefault, null, District.Modern, NearStyle.Modern),
                (BuildingCategory.MixedUse, ClassSource.RuleMainRoad, null, District.OldDhaka, NearStyle.ShopHouse),
                (BuildingCategory.Commercial, ClassSource.OsmType, "office", District.Other, NearStyle.Office),
                (BuildingCategory.Commercial, ClassSource.OsmType, "retail", District.Other, NearStyle.Market),
                (BuildingCategory.Commercial, ClassSource.OsmType, "supermarket", District.Modern, NearStyle.Market),
                (BuildingCategory.Commercial, ClassSource.RuleLarge, null, District.Other, NearStyle.Market),
                (BuildingCategory.Industrial, ClassSource.OsmType, "industrial", District.OldDhaka, NearStyle.Industrial),
                (BuildingCategory.Institutional, ClassSource.OsmType, "school", District.Modern, NearStyle.Institutional),
                (BuildingCategory.Religious, ClassSource.OsmType, "mosque", District.OldDhaka, NearStyle.None),
                (BuildingCategory.Informal, ClassSource.RuleTiny, null, District.Other, NearStyle.None),
                (BuildingCategory.Special, ClassSource.OsmType, "train_station", District.Other, NearStyle.None),
            };
            foreach (var (cat, src, type, d, want) in t) Assert.AreEqual(want, ChunkBuildings.StyleOf(cat, src, type, d), $"{cat} {src} {type} in {d}");

            // The nearest named place within Reach decides; its name is matched without case.
            Districts.Set(new[] { 100.0, 5000, 2000, 9000 }, new[] { 100.0, 0, 0, 0 }, new[] { "chawkbazar", "Gulshan 1", "Farmgate", null });
            Assert.AreEqual((4, 1, 1), (Districts.Count, Districts.CountOf(District.OldDhaka), Districts.CountOf(District.Modern)));
            Assert.AreEqual(District.OldDhaka, Districts.Of(150, 150));
            Assert.AreEqual(District.Modern, Districts.Of(4000, 0));
            Assert.AreEqual(District.Other, Districts.Of(2100, 0), "nearest is Farmgate");
            Assert.AreEqual(District.Other, Districts.Of(30000, 30000), "nothing within Reach");
            Assert.AreEqual(District.Other, Districts.Of(9000, 10), "a place without a name is in no district");

            // Built: homes take their district's style, shop-houses keep theirs, offices are not by district.
            var c = Chunk(Rect(1, 120, 120, 20, 12), Rect(2, 160, 120, 20, 12, type: "office"));
            var b = ChunkBuildings.Build(c);
            Assert.That(Walls(b).Where(w => w.Family == FacadeFamily.WalkUp).Select(w => w.Style), Is.Not.Empty.And.All.EqualTo(NearStyle.OldDhaka));
            Assert.That(Walls(b).Where(w => w.Family == FacadeFamily.Commercial).Select(w => w.Style), Is.Not.Empty.And.All.EqualTo(NearStyle.Office));
            Assert.AreEqual(b.NearWallCount, b.NearStyles.Sum());
            Districts.Clear();
            Assert.That(Walls(ChunkBuildings.Build(c)).Where(w => w.Family == FacadeFamily.WalkUp).Select(w => w.Style), Is.All.EqualTo(NearStyle.WalkUp), "no places, no district");
        }

        [Test]
        public void NearFacadesDressThePaintedWindows()
        {
            // A walk-up of five floors, alone in the middle of the chunk (a few, each with its own seed: all the walls of
            // one building share a pattern): every wall is kept with the far tier's bays and floors; each window the far
            // tier paints gets its sill and its sunshade at the painted box's edges, and each window it bricks up gets nothing.
            int dressed = 0, bricked = 0, vertices = 0;
            foreach (long id in new long[] { 7, 8, 9, 10, 11, 12 })
            {
            var c = Chunk(Rect(id, 190, 190, 22, 13));
            var b = ChunkBuildings.Build(c);
            var st = ChunkRoads.Build(c).Street;
            Assert.AreEqual(4, b.NearWallCount);
            int cell = ChunkStreet.CellOf(200, 196);
            Assert.IsTrue(b.HasNearFacade(cell));
            for (int other = 0; other < Cells; other++) if (other != cell) Assert.IsFalse(b.HasNearFacade(other), $"cell {other}");
            var kit = st.BuildNearKit(cell, b);
            Assert.Greater(kit.IndexCount, kit.RoofIndexCount);
            var verts = FacadeVertices(kit);
            vertices += verts.Count;
            foreach (var w in Walls(b))
            {
                // The far tier's wall: its facade coordinates end at the same bays and floors.
                int q = -1;
                for (int v = 0; v < b.VertexCount && q < 0; v += 4)
                    if (Mathf.Abs(b.Positions[3 * v] - w.Px) < 1e-3f && Mathf.Abs(b.Positions[3 * v + 2] - w.Pz) < 1e-3f && (b.Facade[4 * v + 2] % 8192) >= 4096
                        && Mathf.Abs(b.Positions[3 * v + 3] - (w.Px + w.Ux * w.Length)) < 1e-3f) q = v;
                Assert.GreaterOrEqual(q, 0, "the far tier's quad of the wall");
                Assert.AreEqual(w.Bays, b.Facade[4 * (q + 1)], "bays");
                Assert.AreEqual(w.Floors, (int)b.Facade[4 * q + 2] / ChunkBuildings.RoofFloorUnit, "floors below the parapet");
                Assert.AreEqual((int)FacadeFamily.WalkUp, (int)b.Facade[4 * q + 2] & 7);

                double bw = w.Length / w.Bays, fh = w.Floor;
                var win = ChunkBuildings.WindowOf(w.Family);
                for (int k = 0; k < w.Bays; k++)
                    for (int f = 0; f < w.Floors; f++)
                    {
                        ChunkBuildings.CellHash(w.Seed, k, f, out _, out double r1, out _);
                        double a = (k + win[0]) * bw, e = (k + win[2]) * bw, lo = w.Base + (f + win[1]) * fh, hi = w.Base + (f + win[3]) * fh;
                        bool On(Vector3 p, double s0, double s1, double y, double d) =>
                            Math.Abs(S(w, p) - s0) < 1e-3 || Math.Abs(S(w, p) - s1) < 1e-3 ? Math.Abs(p.y - y) < 1e-3 && Math.Abs(D(w, p) - d) < 1e-3 : false;
                        if (r1 > ChunkBuildings.Bricked)
                        {
                            bricked++;
                            Assert.IsFalse(verts.Any(p => S(w, p) > a + 1e-3 && S(w, p) < e - 1e-3 && p.y > lo - 0.1 && p.y < hi + 0.2 && D(w, p) > -1e-3 && D(w, p) < 1.5),
                                $"bay {k} floor {f}: bricked up, not dressed");
                            continue;
                        }
                        dressed++;
                        Assert.IsTrue(verts.Any(p => On(p, a - 0.06, e + 0.06, lo, 0.08)), $"bay {k} floor {f}: sill under the painted window");
                        Assert.IsTrue(verts.Any(p => On(p, a - 0.15, e + 0.15, hi + 0.16, 0.45)), $"bay {k} floor {f}: sunshade over it");
                    }
            }
            }
            Assert.Greater(dressed, 300);
            Assert.Greater(bricked, 0, "there are bricked-up windows to leave alone");
            Debug.Log($"[D-06c] six walk-ups: {dressed} windows dressed, {bricked} bricked up; {vertices} near facade vertices");
        }

        [Test]
        public void BuildingsKeepTheirColourAsTheirNearFacadesFadeIn()
        {
            // A lone building of each style the slice has (no road, so no shops): of the wall seen from outside, the near
            // pieces in a colour other than the wall's own cover little, so the building does not change colour between
            // FadeEnd and FadeStart. (Spandrels in a cladding colour once covered a third of an office's wall.)
            const double MostOtherColour = 0.08, OldDhakaShutters = 0.30;
            foreach (var (type, style, place) in new[] { ("apartments", NearStyle.WalkUp, null), ("office", NearStyle.Office, null),
                                                         ("school", NearStyle.Institutional, null), ("industrial", NearStyle.Industrial, null),
                                                         ("retail", NearStyle.Market, null), ("apartments", NearStyle.OldDhaka, "Lalbagh"),
                                                         ("apartments", NearStyle.Modern, "Gulshan") })
            {
                if (place != null) Districts.Set(new[] { 200.0 }, new[] { 200.0 }, new[] { place });
                else Districts.Clear();
                var c = Chunk(Rect(31, 190, 190, 24, 15, h: 19, type: type));
                var b = ChunkBuildings.Build(c);
                var walls = Walls(b).ToList();
                Assert.That(walls.Select(w => w.Style), Is.Not.Empty.And.All.EqualTo(style), type);
                uint wall = walls[0].Wall;
                double wallArea = walls.Sum(w => (double)w.Length * w.Floors * w.Floor), other = 0, own = 0;
                var st = ChunkRoads.Build(c).Street;
                for (int cell = 0; cell < Cells; cell++)
                {
                    var kit = st.BuildNearKit(cell, b);
                    for (int i = kit.RoofIndexCount; i < kit.IndexCount; i += 3)
                    {
                        int i0 = kit.Indices[i], i1 = kit.Indices[i + 1], i2 = kit.Indices[i + 2];
                        Vector3 P(int v) => new Vector3(kit.Positions[3 * v], kit.Positions[3 * v + 1], kit.Positions[3 * v + 2]);
                        var p0 = P(i0); var e1 = P(i1); var e2 = P(i2);
                        double ax = e1.x - p0.x, ay = e1.y - p0.y, az = e1.z - p0.z, bx = e2.x - p0.x, by = e2.y - p0.y, bz = e2.z - p0.z;
                        double cx = ay * bz - az * by, cy = az * bx - ax * bz, cz = ax * by - ay * bx;
                        double area = Math.Sqrt(cx * cx + cy * cy + cz * cz) / 2;
                        if (Math.Abs(cy) > 0.1 * area * 2) continue; // tops and undersides; fronts and ends are counted
                        if (kit.Colors[i0] == wall) own += area; else other += area;
                    }
                }
                Debug.Log($"[D-06c] lone {style}: fronts in the wall's colour {own:F1} m², in others {other:F1} m², of {wallArea:F0} m² of wall ({other / wallArea:P1})");
                Assert.Greater(own + other, 0, $"{style} is dressed");
                // Old Dhaka's open shutter leaves are painted wood beside every window, which the far tier does not paint:
                // its buildings do take on their shutters' colours as they fade in (K-48). Held to what they are now.
                Assert.LessOrEqual(other / wallArea, style == NearStyle.OldDhaka ? OldDhakaShutters : MostOtherColour, $"{style}: pieces in other colours");
            }
        }

        static double S(in ChunkBuildings.FacadeWall w, Vector3 p) => (p.x - w.Px) * w.Ux + (p.z - w.Pz) * w.Uz;
        static double D(in ChunkBuildings.FacadeWall w, Vector3 p) => (p.x - w.Px) * w.Nx + (p.z - w.Pz) * w.Nz;

        [Test]
        public void NothingReachesPastTheRoomInFrontOfIt()
        {
            // Two pairs of walk-ups side by side: 1.2 m apart, the facing walls are dressed but nothing stands out more than
            // the room less ClearMargin (the second building is lower, so above it every piece in the gap is the first's);
            // 0.6 m apart, they get nothing. Nothing stands inside a footprint.
            var c = Chunk(Rect(11, 190, 190, 10, 12), Rect(12, 201.2, 190, 10, 12, h: 6.4), Rect(13, 290, 290, 10, 12), Rect(14, 300.6, 290, 10, 12));
            var b = ChunkBuildings.Build(c);
            var st = ChunkRoads.Build(c).Street;
            var verts = new List<Vector3>();
            for (int cell = 0; cell < Cells; cell++) verts.AddRange(FacadeVertices(st.BuildNearKit(cell, b)));
            Assert.Greater(verts.Count, 0);
            var east = Walls(b).Single(w => w.Nx > 0.99f && Math.Abs(w.Px - 200) < 1e-3);
            for (int k = 0; k < east.Bays; k++) Assert.AreEqual(12, b.BayClear[east.FirstBay + k], "1.2 m to the next building, in decimetres");
            var between = verts.Where(p => p.x > 200 + 1e-3 && p.x < 201.2 - 1e-3 && p.z > 190 && p.z < 202).ToList();
            Assert.Greater(between.Count(p => p.y > 7), 0, "the 1.2 m gap is dressed above the lower building");
            Assert.Greater(between.Count(p => p.y < 6), 0, "and below");
            Assert.LessOrEqual(between.Where(p => p.y > 7).Max(p => p.x), 200 + 1.2 - ChunkBuildings.ClearMargin + 1e-3, "the taller one's pieces stop short");
            Assert.IsFalse(verts.Any(p => p.x > 300 + 1e-3 && p.x < 300.6 - 1e-3 && p.z > 290 && p.z < 302), "nothing in a 0.6 m gap");
            Assert.Greater(b.NearBaysClosed, 0);
            foreach (var bd in c.Buildings)
            {
                float x0 = bd.Outer[0] / 100f, z0 = bd.Outer[1] / 100f, x1 = bd.Outer[4] / 100f, z1 = bd.Outer[5] / 100f, top = bd.HeightCm / 100f;
                Assert.IsFalse(verts.Any(p => p.x > x0 + 1e-3 && p.x < x1 - 1e-3 && p.z > z0 + 1e-3 && p.z < z1 - 1e-3 && p.y < top), $"nothing inside building {bd.Id}");
            }

            // Old Dhaka homes 1.0 m apart: just room enough to dress (MinClear), but not for the iron balconies (0.7 m deep,
            // so they need 0.8 m less the margin), which the walls with room have.
            Districts.Set(new[] { 200.0 }, new[] { 200.0 }, new[] { "Lalbagh" });
            var old = Chunk(Rect(21, 190, 190, 10, 30, h: 22), Rect(22, 201, 190, 10, 30, h: 6.4));
            var ob = ChunkBuildings.Build(old);
            Assert.That(Walls(ob).Select(w => w.Style), Is.Not.Empty.And.All.EqualTo(NearStyle.OldDhaka));
            var ost = ChunkRoads.Build(old).Street;
            var ov = new List<Vector3>();
            for (int cell = 0; cell < Cells; cell++) ov.AddRange(FacadeVertices(ost.BuildNearKit(cell, ob)));
            var facing = Walls(ob).Single(w => w.Nx > 0.99f && Math.Abs(w.Px - 200) < 1e-3);
            for (int k = 0; k < facing.Bays; k++) Assert.AreEqual(10, ob.BayClear[facing.FirstBay + k], "1.0 m to the next building, in decimetres");
            var gap = ov.Where(p => p.x > 200 + 1e-3 && p.x < 201 - 1e-3 && p.z > 190 && p.z < 220 && p.y > 7).ToList();
            Assert.Greater(gap.Count, 0, "the 1.0 m gap is dressed above the lower building");
            Assert.LessOrEqual(gap.Max(p => p.x), 200 + 1.0 - ChunkBuildings.ClearMargin + 1e-3, "no balcony in it");
            Assert.Greater(ov.Count(p => p.x < 190 - 0.65 && p.y > 3), 0, "iron balconies where there is room");
        }

        [Test]
        public void TheShaderAgreesWithTheNearFacades()
        {
            string src = File.ReadAllText(ShaderPath);
            double[] Box(string pattern)
            {
                var m = Regex.Match(src, pattern);
                Assert.IsTrue(m.Success, pattern);
                return m.Groups[1].Value.Split(',').Select(x => double.Parse(x.Trim(), CultureInfo.InvariantCulture)).ToArray();
            }
            // A signboard lights up where the painted sign under it would.
            StringAssert.Contains("r3 = ToUnit(LowBias(h + 3u))", src);
            Assert.AreEqual(ChunkBuildings.LitSignShare, Box(@"o\.signLit = r3 < ([0-9.]+) \?")[0]);
            // The far tier's window boxes, shop opening, commercial glass and signboard.
            Assert.AreEqual(ChunkBuildings.WinCommercial, Box(@"family == FAMILY_COMMERCIAL \? float4\(([^)]*)\)"));
            Assert.AreEqual(ChunkBuildings.WinIndustrial, Box(@"family == FAMILY_INDUSTRIAL \? float4\(([^)]*)\)"));
            Assert.AreEqual(ChunkBuildings.WinInstitutional, Box(@"family == FAMILY_INSTITUTIONAL \? float4\(([^)]*)\)"));
            Assert.AreEqual(ChunkBuildings.WinWalkUp, Box(@"FAMILY_INSTITUTIONAL \? float4\([^)]*\) :\s*float4\(([^)]*)\);"));
            Assert.AreEqual(ChunkBuildings.ShopGlass, Box(@"o\.glass = Box\(p, float4\(([^)]*)\)"));
            Assert.AreEqual(ChunkBuildings.ShopOpening, Box(@"half opening = Box\(p, float4\(([^)]*)\)"));
            Assert.AreEqual(ChunkBuildings.ShopSign, Box(@"o\.sign = Box\(p, float4\(([^)]*)\)"));
            // Its bricked-up windows and its hash.
            StringAssert.Contains($"r1 > {ChunkBuildings.Bricked.ToString(CultureInfo.InvariantCulture)}) return o;", src);
            StringAssert.Contains("LowBias(seed * 0x9e3779b9u ^ (uint)cell.x * 0x85ebca6bu ^ (uint)cell.y * 0xc2b2ae35u)", src);
            StringAssert.Contains("float r0 = ToUnit(h), r1 = ToUnit(LowBias(h + 1u));", src);
            var low = Regex.Match(src, @"uint LowBias\(uint x\)\s*\{(.*?)\}", RegexOptions.Singleline);
            Assert.IsTrue(low.Success, "LowBias in the shader");
            Assert.AreEqual("x ^= x >> 16; x *= 0x7feb352du; x ^= x >> 15; x *= 0x846ca68bu; x ^= x >> 16; return x;",
                Regex.Replace(low.Groups[1].Value, @"\s+", " ").Replace("U", "u").Trim().Replace("0x7feb352d;", "0x7feb352du;").Replace("0x846ca68b;", "0x846ca68bu;"));
            var unit = Regex.Match(src, @"ToUnit\(uint h\)\s*\{\s*return \(h >> 8\) \* \(1\.0 / 16777216\.0\);");
            Assert.IsTrue(unit.Success, "ToUnit as the builder's");
            // The sign colours, in the same order.
            Assert.IsTrue(Regex.IsMatch(src, @"half3\(0xb0, 0x30, 0x2a\) / 255\.0, half3\(0x2f, 0x7a, 0x3a\) / 255\.0, half3\(0x2c, 0x4f, 0x8f\) / 255\.0,\s*half3\(0xd2, 0xb2, 0x3a\) / 255\.0, half3\(0xe6, 0xe2, 0xd6\) / 255\.0, half3\(0x7a, 0x2f, 0x6b\) / 255\.0"));
            // The fade: in all four passes, measured as StreetLod measures, and over before a cell can turn near or far.
            Assert.AreEqual(4, Regex.Matches(src, @"#pragma shader_feature_local _ _DAD_NEAR").Count, "_DAD_NEAR in every pass");
            Assert.AreEqual(4, Regex.Matches(src, @"NearDither\(i\.positionWS, i\.positionCS\);").Count, "every pass dithers");
            StringAssert.Contains("float up = max(0, _DadNearFocus.y - 10);", src);
            Assert.Less(ChunkBuildings.FadeStart, ChunkBuildings.FadeEnd);
            Assert.Less(ChunkBuildings.FadeEnd + ChunkBuildings.MaxReach, StreetLod.NearDistance, "a cell turning near or far draws none of its near facades");
        }

        [Test]
        public void NearMaterialIsTheWorldMaterialWithItsKeyword()
        {
            var world = Resources.Load<Material>("Materials/World");
            var near = Resources.Load<Material>("Materials/WorldNear");
            Assert.IsNotNull(near, "Resources/Materials/WorldNear.mat");
            Assert.AreSame(world.shader, near.shader);
            Assert.IsTrue(near.IsKeywordEnabled("_DAD_NEAR"));
            Assert.IsFalse(near.IsKeywordEnabled("_DAD_FACADE"));
            Assert.IsFalse(world.IsKeywordEnabled("_DAD_NEAR"));
            foreach (var t in new[] { "_WorldAlbedo", "_WorldNormal", "_WorldMask", "_WorldMacro" })
                Assert.AreSame(world.GetTexture(t), near.GetTexture(t), t);
            Assert.AreEqual(world.GetFloat("_ShadowCull"), near.GetFloat("_ShadowCull"));

            var shader = near.shader;
            var copy = new Material(near);
            try
            {
                ShaderUtil.ClearShaderMessages(shader);
                foreach (var k in new[] { "_MAIN_LIGHT_SHADOWS_CASCADE", "_CLUSTER_LIGHT_LOOP", "_SHADOWS_SOFT", "_SHADOWS_SOFT_HIGH", "_SCREEN_SPACE_OCCLUSION", "FOG_EXP2", "_ADDITIONAL_LIGHTS" })
                    copy.EnableKeyword(k);
                foreach (var name in new[] { "ForwardLit", "ShadowCaster", "DepthOnly", "DepthNormals" })
                {
                    int pass = copy.FindPass(name);
                    Assert.GreaterOrEqual(pass, 0, name);
                    ShaderUtil.CompilePass(copy, pass, true);
                    Assert.IsTrue(ShaderUtil.IsPassCompiled(copy, pass), name);
                }
                var errors = ShaderUtil.GetShaderMessages(shader).Where(m => m.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error)
                    .Select(m => $"{m.platform} line {m.line}: {m.message}").ToArray();
                Assert.IsEmpty(errors, string.Join("\n", errors));
            }
            finally { Object.DestroyImmediate(copy); }
        }

        [Test]
        public void TheSliceNearFacadesStayWithTheirCells()
        {
            // Every cell's near facades: within MaxReach of the cell's square, valid triangles, inside the triangle targets
            // (per cell at most PerCellTarget; half the cells at most HalfCellTarget), the same every build and on any thread;
            // every wall listed in exactly the cells its dressed bays fall in.
            const int PerCellTarget = 120000, HalfCellTarget = 50000;
            SetPlaces();
            var perCell = new List<int>();
            long vertices = 0, triangles = 0, walls = 0, bays = 0, closed = 0;
            int emptyCells = 0, roadHomes = 0, shopWalls = 0, spanning = 0;
            var styles = new long[9];
            int chunks = 0;
            for (int ix = 6; ix <= 9; ix++)
                for (int iy = -30; iy <= -28; iy++)
                {
                    var file = Path.Combine(ChunkDir, $"c_{ix}_{iy}.dadc");
                    Assume.That(File.Exists(file), "world data missing: run Tools/worldgen/build.sh");
                    var c = Dadc.Load(File.ReadAllBytes(file));
                    var b = ChunkBuildings.Build(c);
                    var again = ChunkBuildings.Build(Dadc.Load(File.ReadAllBytes(file)));
                    var st = ChunkRoads.Build(c).Street;
                    chunks++;
                    walls += b.NearWallCount; bays += b.NearBays; closed += b.NearBaysClosed;
                    for (int k = 0; k < 9; k++) styles[k] += b.NearStyles[k];
                    foreach (var w in Walls(b))
                    {
                        if (w.Style == NearStyle.WalkUp && w.Road) roadHomes++;
                        if (w.Shops) shopWalls++;
                        Assert.AreEqual(w.Shops, w.Road && (w.Family == FacadeFamily.ShopHouse || w.Family == FacadeFamily.Commercial), "shops on the road-facing walls of shop-houses and commercial buildings");
                        // Listed in the cell of each of its bays with room, and in no other: a wall across a cell line is
                        // drawn in each cell, its own bays in each.
                        var want = new SortedSet<int>();
                        for (int k = 0; k < w.Bays; k++)
                        {
                            if (b.BayClear[w.FirstBay + k] < ChunkBuildings.MinClear * 10) continue;
                            double s = (k + 0.5) * (w.Length / w.Bays);
                            want.Add(ChunkStreet.CellOf(w.Px + w.Ux * s, w.Pz + w.Uz * s));
                        }
                        var listed = new SortedSet<int>(Enumerable.Range(0, Cells).Where(cell =>
                            Enumerable.Range(b.NearWallStart[cell], b.NearWallStart[cell + 1] - b.NearWallStart[cell]).Any(r => b.NearWalls[r].FirstBay == w.FirstBay)));
                        Assert.AreEqual(string.Join(" ", want), string.Join(" ", listed), $"{c.Id}: the cells of wall {w.FirstBay}'s bays");
                        if (want.Count > 1) spanning++;
                    }
                    var kits = new NearKit[Cells];
                    System.Threading.Tasks.Parallel.For(0, Cells, cell => kits[cell] = st.BuildNearKit(cell, again, pooled: true));
                    for (int cell = 0; cell < Cells; cell++)
                    {
                        var kit = st.BuildNearKit(cell, b);
                        var p = kits[cell];
                        Assert.AreEqual((kit.VertexCount, kit.IndexCount, kit.RoofIndexCount), (p.VertexCount, p.IndexCount, p.RoofIndexCount), $"{c.Id} cell {cell}: the same every build");
                        for (int k = 0; k < 3 * kit.VertexCount; k++) if (kit.Positions[k] != p.Positions[k]) Assert.Fail($"{c.Id} cell {cell}: position {k} differs");
                        for (int k = 0; k < kit.IndexCount; k++) if (kit.Indices[k] != p.Indices[k]) Assert.Fail($"{c.Id} cell {cell}: index {k} differs");
                        p.Release();
                        if (!b.HasNearFacade(cell)) Assert.AreEqual(kit.RoofIndexCount, kit.IndexCount, $"{c.Id} cell {cell}: no walls, no near facades");
                        else if (kit.IndexCount == kit.RoofIndexCount) emptyCells++;
                        double x0 = cell % ChunkStreet.Cells * ChunkStreet.CellSize, z0 = cell / ChunkStreet.Cells * ChunkStreet.CellSize;
                        for (int v = kit.RoofVertexCount; v < kit.VertexCount; v++)
                        {
                            double x = kit.Positions[3 * v], z = kit.Positions[3 * v + 2];
                            double dx = Math.Max(0, Math.Max(x0 - x, x - x0 - ChunkStreet.CellSize)), dz = Math.Max(0, Math.Max(z0 - z, z - z0 - ChunkStreet.CellSize));
                            if (dx * dx + dz * dz > ChunkBuildings.MaxReach * ChunkBuildings.MaxReach) Assert.Fail($"{c.Id} cell {cell} vertex {v} is {Math.Sqrt(dx * dx + dz * dz):F2} m from its cell");
                        }
                        for (int i = kit.RoofIndexCount; i < kit.IndexCount; i++)
                            if (kit.Indices[i] < kit.RoofVertexCount || kit.Indices[i] >= kit.VertexCount) Assert.Fail($"{c.Id} cell {cell}: index {i} outside the facades' vertices");
                        int tris = (kit.IndexCount - kit.RoofIndexCount) / 3;
                        perCell.Add(tris);
                        vertices += kit.VertexCount - kit.RoofVertexCount;
                        triangles += tris;
                    }
                }
            perCell.Sort();
            int p50 = perCell[perCell.Count / 2], p90 = perCell[perCell.Count * 9 / 10], max = perCell[perCell.Count - 1];
            Debug.Log($"[D-06c] slice near facades: {chunks} chunks; walls {walls}, bays {bays} ({closed} with no room); " +
                      string.Join(", ", Enumerable.Range(1, 8).Select(k => $"{(NearStyle)k} {styles[k]}")) +
                      $"; {vertices} vertices, {triangles} triangles; per cell p50 {p50}, p90 {p90}, max {max}; cells with walls but nothing drawn {emptyCells}; " +
                      $"walk-up walls facing a road {roadHomes}, walls with shops {shopWalls}, walls across a cell line {spanning}");
            Assert.AreEqual(12, chunks);
            Assert.LessOrEqual(max, PerCellTarget);
            Assert.LessOrEqual(p50, HalfCellTarget);
            foreach (var s in new[] { NearStyle.WalkUp, NearStyle.ShopHouse, NearStyle.Office, NearStyle.Market, NearStyle.Industrial, NearStyle.Institutional })
                Assert.Greater(styles[(int)s], 0, $"the slice has {s} walls");
            Assert.Less(closed, bays / 10, "most bays have room");
            Assert.Greater(roadHomes, 0, "homes facing a road (balconies)");
            Assert.Greater(shopWalls, 0, "walls with shops");
            Assert.Greater(spanning, 0, "walls across a cell line");
        }

        [Test]
        public void OldDhakaAndTheModernAreasGetTheirStyles()
        {
            // Chawkbazar and Gulshan 1 (outside the slice, so drawn only in a city build): their homes take the district
            // styles, and the near kits carry the shutter leaves' paints and the glass railings.
            SetPlaces();
            foreach (var (id, style, colours) in new[]
                     {
                         ("c_8_-21", NearStyle.OldDhaka, new uint[] { 0x3f6b4a, 0x3d5f7a, 0x8a6a3a, 0x5e4330 }),
                         ("c_13_-35", NearStyle.Modern, new uint[] { ChunkBuildings.GlassRail }),
                     })
            {
                var file = Path.Combine(ChunkDir, id + ".dadc");
                Assume.That(File.Exists(file), "world data missing: run Tools/worldgen/build.sh");
                var c = Dadc.Load(File.ReadAllBytes(file));
                var b = ChunkBuildings.Build(c);
                var st = ChunkRoads.Build(c).Street;
                Assert.Greater(b.NearStyles[(int)style], b.NearStyles[(int)NearStyle.WalkUp], $"{id}: mostly {style} homes");
                var seen = new HashSet<uint>();
                for (int cell = 0; cell < Cells; cell++)
                {
                    var kit = st.BuildNearKit(cell, b);
                    for (int v = kit.RoofVertexCount; v < kit.VertexCount; v++) seen.Add(kit.Colors[v] & 0xffffff);
                }
                foreach (var rgb in colours)
                    Assert.IsTrue(seen.Contains(Linear(rgb)) || seen.Contains(rgb) || seen.Any(s => Close(s, rgb)), $"{id}: {style} colour {rgb:x6}");
                Debug.Log($"[D-06c] {id}: " + string.Join(", ", Enumerable.Range(1, 8).Select(k => $"{(NearStyle)k} {b.NearStyles[k]}")));
            }
        }

        // Packed colours keep the sRGB value in their low 24 bits (WorldMaterials.Pack); the two helpers only guard the
        // byte order.
        static uint Linear(uint rgb) => ((rgb & 0xff) << 16) | (rgb & 0xff00) | ((rgb >> 16) & 0xff);
        static bool Close(uint a, uint b) => Linear(a) == b;
    }
}
