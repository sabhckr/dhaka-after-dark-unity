// The shipped benchmark path loads, is smooth and deterministic, stays over the world data,
// and bad paths are refused. Paths fly above the roofs (over 20 m), except street paths (names
// ending "-street", D-03), which keep to eye height: 1.5 to 3 m.
using System;
using System.Collections.Generic;
using System.IO;
using Dad.Core;
using Dad.World;
using NUnit.Framework;
using UnityEngine;

namespace Dad.Tests
{
    public class CameraPathTests
    {
        [Serializable] class WorldJson { public WorldChunk[] chunks; }
        [Serializable] class WorldChunk { public string id; }

        [Test]
        public void ShippedPathIsSmoothAndPassesThroughItsPoints()
        {
            var path = CameraPath.Load(BenchmarkRunner.DefaultPath);
            Assert.AreEqual(40f, path.duration);
            foreach (var p in path.points)
            {
                var e = path.Evaluate(p.t);
                Assert.AreEqual(p.east, e.east, 1e-6);
                Assert.AreEqual(p.north, e.north, 1e-6);
                Assert.AreEqual(p.height, e.height, 1e-4);
                Assert.AreEqual(p.lookNorth, e.lookNorth, 1e-6);
            }
            Assert.AreEqual(path.points[0].east, path.Evaluate(-5f).east);
            Assert.AreEqual(path.points[path.points.Length - 1].north, path.Evaluate(99f).north);

            var prev = path.Evaluate(0f);
            double maxSpeed = 0;
            for (int i = 1; i <= 4000; i++)
            {
                float t = i * 0.01f;
                var p = path.Evaluate(t);
                Assert.IsFalse(double.IsNaN(p.east) || double.IsNaN(p.north) || float.IsNaN(p.height), $"NaN at {t}");
                Assert.Greater(p.height, 20f, $"camera stays above roofs at {t}");
                double step = Math.Sqrt(Math.Pow(p.east - prev.east, 2) + Math.Pow(p.north - prev.north, 2) + Math.Pow(p.height - prev.height, 2));
                maxSpeed = Math.Max(maxSpeed, step / 0.01);
                prev = p;
                var again = path.Evaluate(t);
                Assert.AreEqual((p.east, p.north, p.height), (again.east, again.north, again.height), "same time, same pose");
            }
            Debug.Log($"Camera path {path.name}: max speed {maxSpeed:F1} m/s");
            Assert.Less(maxSpeed, 120, "no jumps");
        }

        static string[] ShippedPaths()
        {
            var names = new List<string>();
            foreach (var a in Resources.LoadAll<TextAsset>("CameraPaths")) names.Add(a.name);
            return names.ToArray();
        }

        [Test]
        public void EveryShippedPathIsValidAndSmooth()
        {
            var names = ShippedPaths();
            CollectionAssert.IsSupersetOf(names, new[] { "hub-rich-air", "slums-airport-air", "central-avenue-street" });
            foreach (var name in names)
            {
                var path = CameraPath.Load(name);
                Assert.AreEqual(name, path.name, "file name and path name agree");
                bool street = name.EndsWith("-street");
                var prev = path.Evaluate(0f);
                for (float t = 0.01f; t <= path.duration; t += 0.01f)
                {
                    var p = path.Evaluate(t);
                    if (street) Assert.That(p.height, Is.InRange(1.5f, 3f), $"{name} at eye height at {t}");
                    else Assert.Greater(p.height, 20f, $"{name} above roofs at {t}");
                    double step = System.Math.Sqrt(System.Math.Pow(p.east - prev.east, 2) + System.Math.Pow(p.north - prev.north, 2));
                    Assert.Less(step / 0.01, 120, $"{name} jumps at {t}");
                    prev = p;
                }
            }
        }

        [Test]
        public void ShippedPathStaysOverWorldData()
        {
            var file = Path.Combine(TestWorld.City, "world.json");
            Assume.That(File.Exists(file), "world data missing: run Tools/worldgen/build.sh");
            var ids = new HashSet<string>();
            foreach (var c in JsonUtility.FromJson<WorldJson>(File.ReadAllText(file)).chunks) ids.Add(c.id);
            foreach (var name in ShippedPaths())
            {
                var path = CameraPath.Load(name);
                for (float t = 0; t <= path.duration; t += 0.5f)
                {
                    var p = path.Evaluate(t);
                    Assert.IsTrue(ids.Contains(ChunkCell.Of(p.east, p.north).Id), $"{name}: camera over a world chunk at {t} s");
                    Assert.IsTrue(ids.Contains(ChunkCell.Of(p.lookEast, p.lookNorth).Id), $"{name}: looking at a world chunk at {t} s");
                }
            }
        }

        [Test]
        public void BadPathsAreRefused()
        {
            const string pt = "{\"t\":T,\"east\":0,\"north\":0,\"height\":50,\"lookEast\":1,\"lookNorth\":1,\"lookHeight\":0}";
            string P(float t) => pt.Replace("T", t.ToString(System.Globalization.CultureInfo.InvariantCulture));
            string Path(float duration, params float[] ts) => $"{{\"duration\":{duration},\"points\":[{string.Join(",", Array.ConvertAll(ts, P))}]}}";
            Assert.DoesNotThrow(() => CameraPath.Parse(Path(5, 0, 5)));
            Assert.Throws<FormatException>(() => CameraPath.Parse(Path(5, 0)), "one point");
            Assert.Throws<FormatException>(() => CameraPath.Parse(Path(5, 1, 5)), "not starting at 0");
            Assert.Throws<FormatException>(() => CameraPath.Parse(Path(5, 0, 3, 3, 5)), "time standing still");
            Assert.Throws<FormatException>(() => CameraPath.Parse(Path(6, 0, 5)), "duration mismatch");
            Assert.Throws<FileNotFoundException>(() => CameraPath.Load("no-such-path"));
        }
    }
}
