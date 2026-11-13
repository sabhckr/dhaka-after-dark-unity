// F-03 in play: the lanes come in with the chunks, traffic appears on them, moves, and is drawn; a chunk that goes takes its lanes
// with it. The driving itself is TrafficTests' (EditMode); this is the join to streaming and the camera.
using System.Collections;
using System.IO;
using System.Linq;
using Dad.Core;
using Dad.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Dad.Tests
{
    public class TrafficViewTests
    {
        const double East = 4224, North = 14198;
        Transform focus;
        FloatingOrigin origin;
        ChunkStreamer streamer;
        TrafficView view;

        [SetUp] public void Mark() => TestScenes.Mark();

        [UnityTearDown]
        public IEnumerator Clean()
        {
            ChunkStreamer.BuildLanes = false;
            yield return TestScenes.Clean();
        }

        IEnumerator Start(int load, int unload)
        {
            OldWorld.Use();
            Assume.That(File.Exists(Path.Combine(GameRoot.WorldDir, "world.json")), "world data missing");
            SceneManager.SetActiveScene(SceneManager.CreateScene("Traffic" + System.Guid.NewGuid().ToString("N")));
            var cam = new GameObject("Camera") { tag = "MainCamera" }.AddComponent<Camera>();
            focus = cam.transform;
            origin = new GameObject("FloatingOrigin").AddComponent<FloatingOrigin>();
            origin.Target = focus;
            origin.Recenter(East, North);
            MoveTo(East, North);
            var go = new GameObject("ChunkStreamer");
            go.SetActive(false);
            streamer = go.AddComponent<ChunkStreamer>();
            streamer.Focus = focus;
            streamer.Origin = origin;
            OldWorld.Limit(streamer);
            streamer.LoadRadius = load;
            streamer.UnloadRadius = unload;
            view = TrafficView.Attach(go, streamer, null);
            view.Budget = 120;
            go.SetActive(true);
            float until = Time.realtimeSinceStartup + 60f;
            while (!view.Ready && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(view.Ready, view.State);
        }

        [Test]
        public void LampsComeUpOverTheDuskAndGoOverTheDawn()
        {
            Assert.AreEqual(0, TrafficView.HeadlampsOn(12));
            Assert.AreEqual(1, TrafficView.HeadlampsOn(21));
            Assert.AreEqual(1, TrafficView.HeadlampsOn(2));
            const double minute = 1 / 60.0;
            for (double h = minute; h < 24; h += minute)
                Assert.Less(System.Math.Abs(TrafficView.HeadlampsOn(h) - TrafficView.HeadlampsOn(h - minute)), 0.05, $"lamps step at {h:F2} h");
        }

        void MoveTo(double east, double north)
        {
            origin.Offset.ToScene(east, north, out float x, out float z);
            focus.position = new Vector3(x, 2f, z);
        }

        [UnityTest]
        public IEnumerator TrafficComesInWithTheChunksAndMoves()
        {
            yield return Start(1, 2);
            Assert.That(view.Net.Chunks.Count, Is.EqualTo(streamer.Resident.Count));
            Assert.That(view.Sim.Agents.Count, Is.GreaterThan(30));
            var a = view.Sim.Agents.First(x => x.V > 1);
            double x0 = a.X, z0 = a.Z, t0 = view.Sim.Time;
            float until = Time.realtimeSinceStartup + 10f;
            while (view.Sim.Time < t0 + 1 && Time.realtimeSinceStartup < until) yield return null;
            Assert.That(view.Sim.Time, Is.GreaterThan(t0 + 0.9), "the simulation runs in play");
            Assert.That(System.Math.Abs(a.X - x0) + System.Math.Abs(a.Z - z0), Is.GreaterThan(0.2), "a moving vehicle moves");
            Assert.That(view.DrawnNear + view.DrawnFar, Is.GreaterThan(0), "what is in front of the camera is submitted");
        }

        [UnityTest]
        public IEnumerator AChunkThatGoesTakesItsLanes()
        {
            yield return Start(1, 2);
            int before = view.Net.Chunks.Count;
            MoveTo(East + 1500, North);
            float until = Time.realtimeSinceStartup + 30f;
            while (Time.realtimeSinceStartup < until && !(streamer.Idle && view.Net.Chunks.All(c => streamer.Resident.Keys.Any(k => k.Ix == c.X && k.Iy == c.Y)))) yield return null;
            Assert.That(view.Net.Chunks.All(c => streamer.Resident.Keys.Any(k => k.Ix == c.X && k.Iy == c.Y)), "no lanes left of chunks that have gone");
            Assert.That(view.Sim.Agents.All(a => view.Net.Chunks.Contains(a.Lane.Chunk)), "no vehicle left on them");
            Assert.That(before, Is.GreaterThan(0));
        }
    }
}
