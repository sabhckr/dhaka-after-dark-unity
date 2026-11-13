using System.Collections.Generic;
using Dad.Core;
using Dad.World;
using NUnit.Framework;
using UnityEngine;

namespace Dad.Tests
{
    public class NpcTests
    {
        static ChunkRoads.WalkPiece Piece(double x0, double x1, double width = 2, long way = 7, int side = 1, ChunkRoads.WalkKind kind = ChunkRoads.WalkKind.Paving) =>
            new ChunkRoads.WalkPiece { Way = way, Side = side, Kind = kind, InAx = x0, InAz = 0, InBx = x1, InBz = 0, OutAx = x0, OutAz = width, OutBx = x1, OutBz = width, Ya = 0.15, Yb = 0.15 };

        [Test]
        public void BandsJoinIntoALineAlongTheirMiddle()
        {
            var w = ChunkWalks.Build(0, 0, 100, 200, new List<ChunkRoads.WalkPiece> { Piece(0, 5), Piece(5, 10) });
            Assert.AreEqual(1, w.Lines.Count);
            var l = w.Lines[0];
            Assert.AreEqual(10, l.Length, 1e-9);
            l.At(5, out double e, out double n, out float y, out double de, out double dn);
            Assert.AreEqual(105, e, 1e-9);
            Assert.AreEqual(201, n, 1e-9, "the middle of a band 2 m wide");
            Assert.AreEqual(0.15f, y, 1e-5f);
            Assert.AreEqual(1, de, 1e-9);
            Assert.AreEqual(0, dn, 1e-9);
        }

        [Test]
        public void NobodyWalksInADrainOrOnTooNarrowABand()
        {
            var w = ChunkWalks.Build(0, 0, 0, 0, new List<ChunkRoads.WalkPiece> { Piece(0, 20, kind: ChunkRoads.WalkKind.Drain), Piece(20, 40, width: 0.7) });
            Assert.AreEqual(0, w.Lines.Count);
        }

        [Test]
        public void AGapOrAnotherSideStartsANewLine()
        {
            var w = ChunkWalks.Build(0, 0, 0, 0, new List<ChunkRoads.WalkPiece> { Piece(0, 10), Piece(12, 22), Piece(22, 32, side: -1) });
            Assert.AreEqual(3, w.Lines.Count);
        }

        [Test]
        public void NoMapOfAnNpcIsSmallerThanItsFile()
        {
            int seen = 0;
            const string root = "Assets/_Project/Resources/People/";
            var folders = new System.Collections.Generic.HashSet<string>();
            foreach (var p in NpcRoster.Load().people) // the Blender people keep their maps in the shared wardrobe, the others in a folder of their own
                folders.Add(UnityEditor.AssetDatabase.IsValidFolder(root + p.model) ? root + p.model : root + "Wardrobe");
            foreach (var folder in folders)
                foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
                {
                    var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                    var importer = (UnityEditor.TextureImporter)UnityEditor.AssetImporter.GetAtPath(path);
                    var file = System.IO.File.ReadAllBytes(path);
                    int width, height;
                    if (path.EndsWith(".tga", System.StringComparison.Ordinal)) // Unity's loader does not read these: the size is in the header
                    {
                        width = System.BitConverter.ToUInt16(file, 12);
                        height = System.BitConverter.ToUInt16(file, 14);
                    }
                    else
                    {
                        var size = new Texture2D(1, 1);
                        Assert.IsTrue(ImageConversion.LoadImage(size, file), path);
                        width = size.width;
                        height = size.height;
                        Object.DestroyImmediate(size);
                    }
                    var map = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    Assert.AreEqual(width, map.width, path);
                    Assert.AreEqual(height, map.height, path);
                    seen++;
                }
            Assert.Greater(seen, 0);
        }

        [Test]
        public void TheRosterIsOfDressedPeopleOnly()
        {
            var roster = NpcRoster.Load();
            var ids = new HashSet<string>();
            foreach (var p in roster.people)
            {
                Assert.IsTrue(ids.Add(p.id), p.id + " twice");
                StringAssert.StartsWith("NPC_", p.id);
                Assert.IsTrue(p.model.StartsWith("npc_"), p.id + " must be a dressed model: one of the Blender people");
                if (p.Walks) Assert.IsNotNull(Resources.Load<GameObject>(Person.Folder + p.model), p.model);
                Assert.That(p.height, Is.InRange(1.45f, 1.95f), p.id);
                Assert.That(p.skin, Is.InRange(0.4f, 1.2f), p.id);
            }
        }
    }
}
