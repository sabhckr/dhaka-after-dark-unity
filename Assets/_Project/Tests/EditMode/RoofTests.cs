// D-06b: each building's skin follows its building:material tag, else the labelled rules by category; a
// parapet keeps the building's top where the data puts it and lowers the roof ParapetHeight, its face
// turned to the roof and its coping up, on courtyards too and never along the chunk border; the far
// tier marks where the parapet starts, and the shader reads it there; the rooftop kit stands wholly on
// its roofs, the same every build, simpler far and without dishes; the slice's roofs are furnished;
// the kit's parts are closed solids within their triangle targets.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Dad.World;
using NUnit.Framework;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Dad.Tests
{
    public class RoofTests
    {
        static string ChunkDir => Path.Combine(TestWorld.Root, "chunks");
        const string ShaderPath = "Assets/_Project/Shaders/DadWorld.shader";

        /// <summary>A rectangle x0..x0+w, z0..z0+d metres in the chunk, counter-clockwise, h metres tall (estimated).</summary>
        static Building Rect(long id, double x0, double z0, double w, double d, double h = 9.6, string type = null, string material = null) =>
            new Building
            {
                Id = id, HeightCm = Cm(h), HeightSource = "estimated", Type = type, Material = material,
                Outer = new[] { Cm(x0), Cm(z0), Cm(x0 + w), Cm(z0), Cm(x0 + w), Cm(z0 + d), Cm(x0), Cm(z0 + d) },
            };

        static int Cm(double m) => (int)Math.Round(m * 100);

        static DadcChunk Chunk(params Building[] buildings) =>
            new DadcChunk { Ix = 0, Iy = -1, Buildings = buildings, Roads = new Road[0], WidthsResolved = true };

        static BuildingRules.Placement Pl(double top) => new BuildingRules.Placement { Base = 0, Top = top };

        static Vector3 P(ChunkBuildings s, int v) => new Vector3(s.Positions[3 * v], s.Positions[3 * v + 1], s.Positions[3 * v + 2]);

        [Test]
        public void SkinsFollowTheTagThenTheRules()
        {
            var R = BuildingCategory.Residential;
            // The tag decides, whatever the category; unknown values fall to the rules.
            foreach (var (tag, m) in new[] { ("brick", WorldMaterial.Brick), ("exposed brick", WorldMaterial.Brick), ("plaster", WorldMaterial.Plaster),
                         ("tin", WorldMaterial.RoofSheet), ("tin shed", WorldMaterial.RoofSheet), ("concrete", WorldMaterial.Concrete), ("cement_block", WorldMaterial.Concrete), ("glass", WorldMaterial.Glass) })
            {
                var s = BuildingSkin.Of(Rect(1, 0, 0, 10, 10, material: tag), BuildingCategory.Commercial, Pl(9.6), false);
                Assert.AreEqual((m, SkinSource.Tag), (s.Wall, s.WallSource), tag);
            }
            Assert.AreEqual(SkinSource.Rule, BuildingSkin.Of(Rect(1, 0, 0, 10, 10, material: "bamboo"), R, Pl(9.6), false).WallSource);
            // M2's facades stay plaster even when tagged.
            Assert.AreEqual(WorldMaterial.Plaster, BuildingSkin.Of(Rect(1, 0, 0, 10, 10, material: "brick"), R, Pl(9.6), true).Wall);
            // A sheet-walled building has a sheet roof of the same tone and no parapet; so does an informal one.
            var tin = BuildingSkin.Of(Rect(2, 0, 0, 10, 10, material: "tin"), R, Pl(9.6), false);
            Assert.AreEqual((WorldMaterial.RoofSheet, tin.WallTone, false, 9.6, false), (tin.Roof, tin.RoofTone, tin.Parapet, tin.RoofY, tin.Kit));
            Assert.AreEqual(WorldMaterial.RoofSheet, BuildingSkin.Of(Rect(3, 0, 0, 3, 3), BuildingCategory.Informal, Pl(3), false).Roof);
            // The rule's shares among homes, over many ids: plaster 60 %, paint 25 %, brick 15 %.
            var count = new Dictionary<WorldMaterial, int>();
            const int n = 20000;
            for (int id = 0; id < n; id++)
            {
                var w = BuildingSkin.Of(Rect(id, 0, 0, 10, 10), R, Pl(9.6), false).Wall;
                count[w] = count.TryGetValue(w, out int k) ? k + 1 : 1;
            }
            CollectionAssert.AreEquivalent(new[] { WorldMaterial.Plaster, WorldMaterial.Paint, WorldMaterial.Brick }, count.Keys);
            Assert.AreEqual(0.60, count[WorldMaterial.Plaster] / (double)n, 0.02);
            Assert.AreEqual(0.25, count[WorldMaterial.Paint] / (double)n, 0.02);
            Assert.AreEqual(0.15, count[WorldMaterial.Brick] / (double)n, 0.02);
            // Tall commercial buildings may be glass; low ones never.
            Assert.IsTrue(Enumerable.Range(0, 200).Any(id => BuildingSkin.Of(Rect(id, 0, 0, 10, 10), BuildingCategory.Commercial, Pl(24), false).Wall == WorldMaterial.Glass));
            Assert.IsFalse(Enumerable.Range(0, 200).Any(id => BuildingSkin.Of(Rect(id, 0, 0, 10, 10), BuildingCategory.Commercial, Pl(12), false).Wall == WorldMaterial.Glass));
            // Every piece of a building gets the same skin: it depends on the id, category and placement only.
            var a = BuildingSkin.Of(Rect(99, 0, 0, 10, 10), R, Pl(9.6), false);
            var b = BuildingSkin.Of(Rect(99, 300, 300, 4, 40), R, Pl(9.6), false);
            Assert.AreEqual(a, b);
        }

        [Test]
        public void AParapetKeepsTheTopAndLowersTheRoof()
        {
            // 21 x 9 m, 9.6 m tall, a walk-up: a slab roof 0.9 m down, a parapet 0.15 m thick on four sides.
            var s = ChunkBuildings.Build(Chunk(Rect(70, 200, 200, 21, 9)));
            var skin = s.Skins[0];
            Assert.IsTrue(skin.Parapet);
            Assert.AreEqual(9.6 - ChunkBuildings.ParapetHeight, skin.RoofY, 1e-9);
            Assert.AreEqual((1, 4), (s.Parapets, s.ParapetPieces));
            float hi = float.MinValue;
            for (int v = 0; v < s.VertexCount; v++) hi = Math.Max(hi, s.Positions[3 * v + 1]);
            Assert.AreEqual(9.6f, hi, 1e-4f, "the top stays where the data puts it");
            double roof = 0, coping = 0;
            int inner = 0;
            var centre = new Vector3(210.5f, 0, 204.5f);
            for (int t = 0; t < s.IndexCount; t += 3)
            {
                Vector3 a = P(s, s.Indices[t]), b = P(s, s.Indices[t + 1]), c = P(s, s.Indices[t + 2]);
                var n = Vector3.Cross(b - a, c - a);
                double area = n.magnitude / 2;
                n.Normalize();
                if (n.y > 0.99 && Mathf.Abs(a.y - (float)skin.RoofY) < 1e-4f)
                {
                    roof += area;
                    Assert.AreEqual(skin.Roof, WorldMaterials.Of(s.Colors[s.Indices[t]]));
                }
                else if (n.y > 0.99 && Mathf.Abs(a.y - 9.6f) < 1e-4f) coping += area;
                else if (Mathf.Abs(n.y) < 1e-3f && Mathf.Min(a.y, Mathf.Min(b.y, c.y)) > (float)skin.RoofY - 1e-4f)
                {
                    // The parapet's face: 0.15 m inside the footprint, turned to the middle of the roof.
                    inner++;
                    var mid = (a + b + c) / 3;
                    var flatN = new Vector3(n.x, 0, n.z);
                    Assert.Greater(Vector3.Dot(flatN, new Vector3(centre.x - mid.x, 0, centre.z - mid.z)), 0, "the parapet faces the roof");
                    double edge = Math.Min(Math.Min(mid.x - 200, 221 - mid.x), Math.Min(mid.z - 200, 209 - mid.z));
                    Assert.AreEqual(BuildingSkin.ParapetThickness, edge, 1e-3);
                }
            }
            Assert.AreEqual(21 * 9, roof, 1e-3, "the roof covers the footprint");
            Assert.AreEqual(21 * 9 - 20.7 * 8.7, coping, 1e-3, "the coping is the ring between the wall and the parapet's face");
            Assert.AreEqual(8, inner, "four faces of two triangles");
            // The far tier's floors divide the wall up to the roof, and w marks where the parapet starts.
            int floors = (int)Math.Round(skin.RoofY / BuildingRules.MetresPerLevel);
            for (int v = 0; v < s.VertexCount; v++)
            {
                int w = (int)s.Facade[4 * v + 2];
                if (w == 0) continue;
                Assert.AreEqual(floors, w / ChunkBuildings.RoofFloorUnit);
                if (s.Positions[3 * v + 1] > 9) Assert.AreEqual(9.6 / (skin.RoofY / floors), s.Facade[4 * v + 1], 1e-4);
            }

            // Along the chunk border there is no parapet: the next chunk's piece continues the roof.
            Assert.AreEqual(3, ChunkBuildings.Build(Chunk(Rect(71, 0, 200, 10, 10))).ParapetPieces);
            // A courtyard has its own parapet, facing away from the courtyard.
            var yard = Rect(72, 200, 200, 30, 30);
            yard.Holes = new[] { new[] { Cm(210), Cm(210), Cm(210), Cm(220), Cm(220), Cm(220), Cm(220), Cm(210) } };
            var ys = ChunkBuildings.Build(Chunk(yard));
            Assert.AreEqual(8, ys.ParapetPieces);
            for (int t = 0; t < ys.IndexCount; t += 3)
            {
                Vector3 a = P(ys, ys.Indices[t]), b = P(ys, ys.Indices[t + 1]), c = P(ys, ys.Indices[t + 2]);
                var n = Vector3.Cross(b - a, c - a).normalized;
                var mid = (a + b + c) / 3;
                bool face = Mathf.Abs(n.y) < 1e-3f && Mathf.Min(a.y, Mathf.Min(b.y, c.y)) > (float)ys.Skins[0].RoofY - 1e-4f;
                if (!face || mid.x < 209 || mid.x > 221 || mid.z < 209 || mid.z > 221) continue;
                // Around the yard, the face stands 0.15 m out from the yard's wall and looks away from the yard.
                Assert.Greater(Vector3.Dot(new Vector3(n.x, 0, n.z), new Vector3(mid.x - 215, 0, mid.z - 215)), 0, "the yard's parapet faces the roof");
            }
            // No parapet on low buildings, on religious buildings or on the sheds.
            Assert.AreEqual(0, ChunkBuildings.Build(Chunk(Rect(73, 200, 200, 10, 10, 4))).Parapets);
            Assert.AreEqual(0, ChunkBuildings.Build(Chunk(Rect(74, 200, 200, 21, 9, type: "mosque"))).Parapets);
            Assert.AreEqual(0, ChunkBuildings.Build(Chunk(Rect(75, 200, 200, 3, 3))).Parapets);
        }

        [Test]
        public void TheShaderReadsTheParapetFloors()
        {
            var text = File.ReadAllText(ShaderPath);
            int shift = (int)Math.Round(Math.Log(ChunkBuildings.RoofFloorUnit, 2));
            Assert.AreEqual(ChunkBuildings.RoofFloorUnit, 1 << shift);
            Assert.AreEqual(2 * ChunkBuildings.WallFlag, ChunkBuildings.RoofFloorUnit, "the floors sit above the wall flag");
            StringAssert.Contains($"roofFloors = w >> {shift};", text);
            StringAssert.Contains("cell.y >= (float)roofFloors", text);
        }

        /// <summary>A walk-up, an office block, a shed, a low house and a block round a yard.</summary>
        static DadcChunk Roofs()
        {
            var yard = Rect(84, 300, 300, 30, 30, 16);
            yard.Holes = new[] { new[] { Cm(310), Cm(310), Cm(310), Cm(320), Cm(320), Cm(320), Cm(320), Cm(310) } };
            return Chunk(Rect(80, 100, 100, 12, 10), Rect(81, 200, 100, 20, 15, 24, "office"), Rect(82, 100, 200, 3, 3, 3),
                Rect(83, 150, 200, 10, 10, 4), yard);
        }

        [Test]
        public void TheRooftopKitStandsOnItsRoofs()
        {
            var c = Roofs();
            var b = ChunkBuildings.Build(c);
            var st = ChunkRoads.Build(c, keepMarks: true).Street;
            var items = st.Items.Where(i => i.Kind >= ChunkStreet.Kind.StairRoom).ToList();
            Debug.Log($"[D-06b] kit: roofs {st.Roofs}, stair rooms {st.StairRooms}, lift rooms {st.LiftRooms}, tanks {st.Tanks}, dishes {st.Dishes}, condensers {st.AcUnits}, no room {st.RoofNoRoom}");
            Assert.AreEqual(3, st.Roofs, "the walk-up, the office block and the yard block; not the shed or the low house");
            Assert.IsFalse(items.Any(i => i.Way == 82 || i.Way == 83));
            Assert.AreEqual(1, st.LiftRooms, "the office block of 7 floors and 300 m² has a lift room");
            Assert.AreEqual(3, items.Count(i => i.Way == 81 && i.Kind == ChunkStreet.Kind.Ac), "one condenser per 80 m²");
            Assert.AreEqual(Math.Min(4, 1 + 800 / 150), items.Count(i => i.Way == 84 && i.Kind == ChunkStreet.Kind.Tank && !i.Skipped));
            Assert.AreEqual(0, st.RoofNoRoom);
            // Each item stands on its building's roof (a tank may stand on the stair room's).
            for (int k = 0; k < c.Buildings.Length; k++)
            {
                var skin = b.Skins[k];
                foreach (var i in items.Where(i => i.Way == c.Buildings[k].Id))
                    Assert.That(i.Y, Is.EqualTo(skin.RoofY).Within(1e-6).Or.EqualTo(skin.RoofY + 2.7).Within(1e-6).Or.EqualTo(skin.RoofY + 3.2).Within(1e-6), $"{i.Kind} of {i.Way}");
            }
            // The near kit is not in the near ranges (no furniture here, so they are empty): it is built per cell (K-45).
            for (int cell = 0; cell < ChunkStreet.Cells * ChunkStreet.Cells; cell++)
                Assert.AreEqual(0, st.Vertices[ChunkStreet.Range(cell, 0)], $"cell {cell}: no near kit in the near range");
            // Every vertex of the kit, far and near, is on a roof: inside the footprint and clear of the parapet, above the roof,
            // below 5 m over it, not in the yard.
            void OnRoof(float x, float y, float z, string what)
            {
                int k = Array.FindIndex(c.Buildings, bb => x >= bb.Outer[0] / 100f + 0.15f && x <= bb.Outer[2] / 100f - 0.15f && z >= bb.Outer[1] / 100f + 0.15f && z <= bb.Outer[5] / 100f - 0.15f);
                Assert.GreaterOrEqual(k, 0, $"{what} at {x:F2}, {z:F2} is on no roof");
                Assert.That(y, Is.InRange((float)b.Skins[k].RoofY - 1e-4f, (float)b.Skins[k].RoofY + 5f));
                Assert.IsFalse(c.Buildings[k].Id == 84 && x > 309.85f && x < 320.15f && z > 309.85f && z < 320.15f, "nothing over the yard");
            }
            for (int r = 1; r < ChunkStreet.Ranges; r++)
                for (int v = st.FirstVertex[r]; v < st.FirstVertex[r] + st.Vertices[r]; v++)
                    OnRoof(st.Positions[3 * v], st.Positions[3 * v + 1], st.Positions[3 * v + 2], $"far kit vertex {v}");
            int nearVertices = 0;
            for (int cell = 0; cell < ChunkStreet.Cells * ChunkStreet.Cells; cell++)
            {
                var kit = st.BuildNearKit(cell);
                nearVertices += kit.VertexCount;
                for (int v = 0; v < kit.VertexCount; v++) OnRoof(kit.Positions[3 * v], kit.Positions[3 * v + 1], kit.Positions[3 * v + 2], $"cell {cell} near kit vertex {v}");
            }
            st.CountNearKit();
            Assert.AreEqual(nearVertices, st.RoofVertices[0]);
            // The same every build.
            var again = ChunkRoads.Build(c).Street;
            Assert.AreEqual(st.VertexCount, again.VertexCount);
            for (int k = 0; k < 3 * st.VertexCount; k++) Assert.AreEqual(st.Positions[k], again.Positions[k]);
            // Far is the near kit's boxes and tubes alone, without dishes: 8 vertices a room and a condenser, 12 a tank.
            Assert.AreEqual(8 * (st.StairRooms + st.LiftRooms + st.AcUnits) + 12 * st.Tanks, st.RoofVertices[1]);
            Assert.Less(st.RoofVertices[1], st.RoofVertices[0] / 2);
        }

        [Test]
        public void NearKitsAreBuiltPerCell()
        {
            // K-45 approach 4: the kit's items are kept by cell, every one in the cell its foot is in, and a cell's near kit
            // stays within a few metres of its square; built on several threads at once, it is the same as built on one.
            var file = Path.Combine(ChunkDir, "c_7_-30.dadc");
            Assume.That(File.Exists(file), "world data missing: run Tools/worldgen/build.sh");
            var st = ChunkRoads.Build(Dadc.Load(File.ReadAllBytes(file))).Street;
            Assert.AreEqual(st.StairRooms + st.LiftRooms + st.Tanks + st.Dishes + st.AcUnits, st.RoofKitStart[ChunkStreet.Cells * ChunkStreet.Cells], "every placed item is kept");
            Assert.AreEqual(st.RoofKit.Length, st.RoofKitStart[ChunkStreet.Cells * ChunkStreet.Cells], "built unpooled, exactly");
            Assert.AreEqual(0, st.RoofKitStart[0]);
            const int cells = ChunkStreet.Cells * ChunkStreet.Cells;
            var serial = new NearKit[cells];
            int withKit = 0;
            for (int cell = 0; cell < cells; cell++)
            {
                Assert.LessOrEqual(st.RoofKitStart[cell], st.RoofKitStart[cell + 1]);
                for (int k = st.RoofKitStart[cell]; k < st.RoofKitStart[cell + 1]; k++)
                    Assert.AreEqual(cell, ChunkStreet.CellOf(st.RoofKit[k].X, st.RoofKit[k].Z), $"item {k} is kept in its foot's cell");
                serial[cell] = st.BuildNearKit(cell);
                Assert.AreEqual(st.HasNearKit(cell), serial[cell].VertexCount > 0);
                if (st.HasNearKit(cell)) withKit++;
                double x0 = cell % ChunkStreet.Cells * ChunkStreet.CellSize, z0 = cell / ChunkStreet.Cells * ChunkStreet.CellSize;
                for (int v = 0; v < serial[cell].VertexCount; v++)
                {
                    Assert.That(serial[cell].Positions[3 * v], Is.InRange(x0 - 3, x0 + ChunkStreet.CellSize + 3), $"cell {cell} vertex {v} east");
                    Assert.That(serial[cell].Positions[3 * v + 2], Is.InRange(z0 - 3, z0 + ChunkStreet.CellSize + 3), $"cell {cell} vertex {v} north");
                    for (int k = 0; k < 3; k++) Assert.That(serial[cell].Positions[3 * v + k], Is.InRange(serial[cell].Min[k], serial[cell].Max[k]), "inside its extent");
                }
            }
            Assert.Greater(withKit, cells / 2, "most of Farmgate's cells have a rooftop kit");
            var parallel = new NearKit[4 * cells];
            System.Threading.Tasks.Parallel.For(0, 4 * cells, new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = 4 }, i => parallel[i] = st.BuildNearKit(i % cells, pooled: true));
            for (int i = 0; i < 4 * cells; i++)
            {
                var a = serial[i % cells];
                var p = parallel[i];
                Assert.AreEqual((a.VertexCount, a.IndexCount), (p.VertexCount, p.IndexCount), $"cell {i % cells}, build {i / cells}");
                for (int k = 0; k < 3 * a.VertexCount; k++) Assert.AreEqual(a.Positions[k], p.Positions[k]);
                for (int k = 0; k < a.VertexCount; k++) Assert.AreEqual(a.Colors[k], p.Colors[k]);
                for (int k = 0; k < a.IndexCount; k++) Assert.AreEqual(a.Indices[k], p.Indices[k]);
                p.Release();
                Assert.IsNull(p.Positions, "pooled arrays are handed back");
            }
        }

        [Test]
        public void NearKitBuildsGrowNoScratch()
        {
            // K-45: near-kit builds (since D-06c with their near facades) borrow scratch sets made ahead at the city's largest
            // cell from a shared pool: a build on a new thread makes no set and grows none, and one build at a time needs one
            // set. (On the standalone Mono a warm build allocated its 168-byte result alone; the editor's runtime does not
            // count per-thread allocations, so this test counts sets and growths instead.)
            var file = Path.Combine(ChunkDir, "c_7_-30.dadc");
            Assume.That(File.Exists(file), "world data missing: run Tools/worldgen/build.sh");
            var chunk = Dadc.Load(File.ReadAllBytes(file));
            var st = ChunkRoads.Build(chunk).Street;
            var facades = ChunkBuildings.Build(chunk);
            ChunkStreet.ResetNearKitScratch();
            ChunkStreet.PrepareNearKitScratch(1);
            Assert.AreEqual(1, ChunkStreet.NearKitScratchSets);
            int maxV = 0, maxI = 0, cells = 0;
            var thread = new System.Threading.Thread(() =>
            {
                for (int c = 0; c < ChunkStreet.Cells * ChunkStreet.Cells; c++)
                {
                    if (!st.HasNearKit(c) && !facades.HasNearFacade(c)) continue;
                    var kit = st.BuildNearKit(c, facades, pooled: true);
                    maxV = Math.Max(maxV, kit.VertexCount);
                    maxI = Math.Max(maxI, kit.IndexCount);
                    kit.Release();
                    cells++;
                }
            });
            thread.Start();
            thread.Join();
            Debug.Log($"[K-45] near kits of c_7_-30 on a new thread: {cells} cells; largest {maxV} vertices, {maxI} indices; scratch sets {ChunkStreet.NearKitScratchSets}, grown {ChunkStreet.NearKitScratchGrowths}");
            Assert.Greater(cells, 0);
            Assert.LessOrEqual(maxV, ChunkStreet.KitScratchVertices, "the sets hold Farmgate's largest cell");
            Assert.LessOrEqual(maxI, ChunkStreet.KitScratchIndices);
            Assert.AreEqual(0, ChunkStreet.NearKitScratchGrowths, "no build grew a set");
            Assert.AreEqual(1, ChunkStreet.NearKitScratchSets, "one build at a time used the one set made ahead");
        }

        [Test]
        public void DishesFaceSouthSouthEastAndUp()
        {
            var p = new List<float>();
            var w = new KitWriter(p, new List<uint>(), new List<int>());
            StreetKit.Dish(w, 0, 0, 0, ChunkRoads.DishAzimuth, ChunkRoads.DishElevation);
            // The receiver arm's far end is the vertex farthest from the post: south of it, a little east, and above the bowl.
            int best = 0;
            double far = 0;
            for (int v = 0; v < p.Count / 3; v++)
            {
                double d = p[3 * v] * p[3 * v] + p[3 * v + 2] * p[3 * v + 2];
                if (d > far) { far = d; best = v; }
            }
            Assert.Less(p[3 * best + 2], 0, "south");
            Assert.Greater(p[3 * best], 0, "east");
            Assert.Greater(-p[3 * best + 2], 2 * p[3 * best], "more south than east");
            Assert.Greater(p[3 * best + 1], StreetKit.DishMount, "up");
        }

        [Test]
        public void TheSliceRooftopsAreFurnished()
        {
            var sw = new Stopwatch();
            int roofs = 0, rooms = 0, tanks = 0, dishes = 0, acs = 0, none = 0, chunks = 0;
            long nearV = 0, farV = 0, nearT = 0, farT = 0, streetV = 0;
            for (int ix = 6; ix <= 9; ix++)
                for (int iy = -30; iy <= -28; iy++)
                {
                    var file = Path.Combine(ChunkDir, $"c_{ix}_{iy}.dadc");
                    Assume.That(File.Exists(file), "world data missing: run Tools/worldgen/build.sh");
                    var c = Dadc.Load(File.ReadAllBytes(file));
                    var b = ChunkBuildings.Build(c);
                    sw.Start();
                    var st = ChunkRoads.Build(c, keepMarks: true).Street;
                    sw.Stop();
                    st.CountNearKit();
                    chunks++;
                    var roofOf = new Dictionary<long, double>();
                    for (int k = 0; k < c.Buildings.Length; k++)
                        if (b.Skins[k].Kit) roofOf[c.Buildings[k].Id] = b.Skins[k].RoofY;
                    foreach (var i in st.Items.Where(i => i.Kind >= ChunkStreet.Kind.StairRoom && !i.Skipped))
                    {
                        Assert.IsTrue(roofOf.TryGetValue(i.Way, out double y), $"c_{ix}_{iy}: {i.Kind} on {i.Way}, which has no kit roof");
                        Assert.That(i.Y, Is.EqualTo(y).Within(1e-6).Or.EqualTo(y + 2.7).Within(1e-6).Or.EqualTo(y + 3.2).Within(1e-6));
                        Assert.That(i.X, Is.InRange(0, 500));
                        Assert.That(i.Z, Is.InRange(0, 500));
                    }
                    roofs += st.Roofs; rooms += st.StairRooms + st.LiftRooms; tanks += st.Tanks; dishes += st.Dishes; acs += st.AcUnits; none += st.RoofNoRoom;
                    nearV += st.RoofVertices[0]; farV += st.RoofVertices[1]; nearT += st.RoofIndices[0] / 3; farT += st.RoofIndices[1] / 3; streetV += st.VertexCount;
                }
            Debug.Log($"[D-06b] slice rooftop kit: {roofs} roofs, {rooms} stair and lift rooms, {tanks} tanks, {dishes} dishes, {acs} condensers, {none} with no room; " +
                      $"near {nearV} vertices {nearT} triangles, far {farV} vertices {farT} triangles, of {streetV} street vertices; roads and street of 12 chunks {sw.ElapsedMilliseconds} ms");
            Assert.AreEqual(12, chunks);
            Assert.Greater(roofs, 1000);
            Assert.Greater(rooms, 0);
            Assert.Greater(tanks, roofs);
            Assert.Greater(dishes, 0);
            Assert.Greater(acs, 0);
            Assert.Less(none, roofs / 20, "almost every item finds room");
        }

        static (double volume, int open) Solid(List<float> p, List<int> idx)
        {
            double vol = 0;
            var edges = new Dictionary<(int, int), int>();
            for (int t = 0; t < idx.Count; t += 3)
            {
                int a = idx[t], b = idx[t + 1], c = idx[t + 2];
                double ax = p[3 * a], ay = p[3 * a + 1], az = p[3 * a + 2], bx = p[3 * b], by = p[3 * b + 1], bz = p[3 * b + 2], cx = p[3 * c], cy = p[3 * c + 1], cz = p[3 * c + 2];
                vol += (ax * (by * cz - bz * cy) + ay * (bz * cx - bx * cz) + az * (bx * cy - by * cx)) / 6.0;
                foreach (var (x, y) in new[] { (a, b), (b, c), (c, a) })
                {
                    if (edges.TryGetValue((y, x), out int n) && n > 0) { if (--edges[(y, x)] == 0) edges.Remove((y, x)); }
                    else edges[(x, y)] = edges.TryGetValue((x, y), out int m) ? m + 1 : 1;
                }
            }
            return (vol, edges.Values.Sum());
        }

        [Test]
        public void KitPartsAreClosedAndWithinTheirTargets()
        {
            uint wall = BuildingPalette.Shade(0xd0c0a0, 1, WorldMaterial.Plaster), tank = BuildingPalette.Shade(StreetKit.TankBlack, 1, WorldMaterial.Paint);
            var parts = new (string name, Action<KitWriter, int> draw, int near)[]
            {
                ("stair room", (w, lod) => StreetKit.RoofRoom(w, 3, 9, 4, 0.6, 0.8, 1.7, 1.4, 2.7, wall, lod), 40),
                ("tank", (w, lod) => StreetKit.Tank(w, 3, 9, 4, 0.55, 1.25, tank, lod), 80),
                ("condenser", (w, lod) => StreetKit.Ac(w, 3, 9, 4, 0.6, 0.8, lod), 60),
                ("dish", (w, lod) => { if (lod == 0) StreetKit.Dish(w, 3, 9, 4, ChunkRoads.DishAzimuth, ChunkRoads.DishElevation); }, 120),
            };
            foreach (var (name, draw, target) in parts)
            {
                int[] tris = new int[2];
                for (int lod = 0; lod < 2; lod++)
                {
                    var p = new List<float>();
                    var i = new List<int>();
                    draw(new KitWriter(p, new List<uint>(), i), lod);
                    tris[lod] = i.Count / 3;
                    if (i.Count == 0) continue;
                    var (vol, open) = Solid(p, i);
                    Assert.Greater(vol, 0, $"{name} lod {lod} faces out");
                    Assert.AreEqual(0, open, $"{name} lod {lod} is closed");
                }
                Debug.Log($"{name}: {tris[0]} / {tris[1]} triangles");
                Assert.LessOrEqual(tris[0], target, name);
                Assert.Less(tris[1], tris[0], name);
            }
        }
    }
}
