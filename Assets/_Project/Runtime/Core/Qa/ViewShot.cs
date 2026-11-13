// Golden views: puts the main camera at a fixed pose, waits until streaming has settled, renders
// one frame at a fixed size and writes shot.png and shot.json. With a golden image it also
// compares the two and records the difference.
//
// Player command line (GameRoot adds the component when -dad-shot is present):
//   -dad-shot <view>          view name in Resources/Views (JSON, see ViewSpec), or a path to such a .json file
//   -dad-shot-out <dir>       output folder (default persistentDataPath/shot-<time>)
//   -dad-shot-golden <png>    golden image to compare with (optional)
// Exit code: 0 written (and matching, if a golden was given), 2 different from the golden,
// 1 could not run. Limits: mean absolute difference <= MaxMeanDiff (0-255 per channel) and at
// most MaxChangedFraction of pixels with a channel more than ChangedThreshold apart.
using System;
using System.Collections;
using System.IO;
using Dad.Traffic;
using Dad.Vehicles;
using Dad.World;
using UnityEngine;

namespace Dad.Core
{
    [Serializable]
    public sealed class ViewSpec
    {
        public string name;
        public double east, north;
        public float height;
        public double lookEast, lookNorth;
        public float lookHeight;
        public float fov = 60f;
        public int width = 1280, height_px = 720;
        /// <summary>Debug views to show: any of "grid", "graph", "lines". None by default.</summary>
        public string[] overlays = new string[0];
        /// <summary>World shading (WorldShading.Parse): "lit" by default, or "normals", "materials", "grid", "albedo", "families".</summary>
        public string shading = "lit";
        /// <summary>Local date (yyyy-MM-dd; DayCycle.DefaultDate if empty), time in hours, and weather ("clear", "cloudy", "overcast").</summary>
        public string date = "";
        public float time = (float)DayCycle.DefaultHours;
        public string weather = "clear";
        /// <summary>Seconds of traffic to run before the picture (F-03); 0 has no traffic, as the golden views before it were taken. With its seed and pool size.</summary>
        public float traffic = 0;
        public int trafficSeed = 1, trafficBudget = 300;
        /// <summary>A line of one of each vehicle family in place of traffic (TrafficView.Parade): east, north, heading east, heading north, spacing, then the four lamps.</summary>
        public float[] parade = new float[0];
        /// <summary>Seconds the street crowd walks before the picture (NpcCrowd); 0 has none.</summary>
        public float npcs = 0;
        /// <summary>Everyone on the roster side by side in place of the crowd: east, north, height, heading, spacing, and how far into the walk they are held.</summary>
        public float[] lineup = new float[0];
        /// <summary>With lineup: the clip everyone in it plays, instead of what they are doing on the street.</summary>
        public string act = "";
        /// <summary>With lineup: degrees everyone is turned from facing the way the line faces, to see them from the side.</summary>
        public float facing = 0;
        /// <summary>With lineup: the roster ids of those in it, comma between, empty for everyone.</summary>
        public string only = "";
        /// <summary>Where the Port Line's train stands in the picture: a stop's number, and seconds of its timetable run on from there. -1 leaves it at the first stop.</summary>
        public int trainStop = -1;
        public float trainRun = 0;
        /// <summary>A driven vehicle on the lane nearest the view's place (E-04): "chase" or "driver", the camera it is seen from, or "side", beside it at road level to see the wheels on the road. Needs traffic, for the lanes.</summary>
        public string drive = "";
        /// <summary>Seconds at full throttle before the picture, so that the needles and the body have something to show.</summary>
        public float driveThrottle = 0;
        /// <summary>The quality level to shoot at (GraphicsTier): "low", "medium" or "high"; empty leaves it as started.</summary>
        public string quality = "";

        public DateTime Date => string.IsNullOrEmpty(date) ? DayCycle.DefaultDate
            : DateTime.ParseExact(date, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

        public Weather Sky => (Weather)Enum.Parse(typeof(Weather), weather, true);

        public bool Shows(string overlay) => overlays != null && Array.IndexOf(overlays, overlay) >= 0;

        public static ViewSpec Load(string name)
        {
            // A path to a .json file on disk also works, to try a view without building the player again (trial buildings, 2026-11-11).
            bool onDisk = name.EndsWith(".json", StringComparison.Ordinal) && File.Exists(name);
            var asset = onDisk ? null : Resources.Load<TextAsset>("Views/" + name);
            if (asset == null && !onDisk) throw new FileNotFoundException("no view Resources/Views/" + name);
            var v = JsonUtility.FromJson<ViewSpec>(onDisk ? File.ReadAllText(name) : asset.text);
            if (v.name != (onDisk ? Path.GetFileNameWithoutExtension(name) : name)) throw new FormatException($"view file {name} names itself {v.name}");
            WorldShading.Parse(v.shading); // throws on an unknown shading view
            _ = v.Date; // throws on a bad date
            _ = v.Sky; // or weather
            if (!string.IsNullOrEmpty(v.quality) && !GraphicsTier.TryParse(v.quality, out _)) throw new FormatException($"view {name}: quality {v.quality}");
            if (v.time < 0 || v.time > 24) throw new FormatException($"view {name}: time {v.time} is not 0-24 hours");
            return v;
        }
    }

    [Serializable]
    public sealed class ShotResult
    {
        public string format = "dad-shot/1";
        public string view, build, unity, gpu, graphicsApi;
        public int width, height;
        public string golden;
        public bool compared, pass;
        public double meanAbsDiff, changedFraction;
        public float maxMeanDiff, maxChangedFraction;
        public int changedThreshold;
        public int resident, applied, failedPolygons;
        public float settleSeconds;
        /// <summary>Traffic in the picture (F-03): vehicles in the pool, and drawn whole and far.</summary>
        public int trafficAgents, trafficNear, trafficFar, trafficLanes;
        public string trafficState;
        /// <summary>The driven vehicle at the picture: metres a second and revs.</summary>
        public double driveSpeed, driveRpm;
        public string error;
    }

    public sealed class ViewShot : MonoBehaviour
    {
        public const float MaxMeanDiff = 1.0f, MaxChangedFraction = 0.005f;
        public const int ChangedThreshold = 24;

        public string ViewName;
        public string OutDir;
        public string GoldenPath;
        public float TimeoutSeconds = 30f;
        public bool QuitWhenDone;

        public bool Done { get; private set; }
        public ShotResult Result { get; private set; }
        public Vector2Int Size;

        public static ViewShot FromCommandLine(GameObject host)
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-dad-shot");
            if (i < 0 || i + 1 >= args.Length) return null;
            string Value(string flag)
            {
                int j = Array.IndexOf(args, flag);
                return j >= 0 && j + 1 < args.Length ? args[j + 1] : null;
            }
            var s = host.AddComponent<ViewShot>();
            s.ViewName = args[i + 1];
            s.OutDir = Value("-dad-shot-out") ?? Path.Combine(Application.persistentDataPath, "shot-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            s.GoldenPath = Value("-dad-shot-golden");
            // -dad-shot-size 2560x1440: render at another size than the view's (before-and-after pictures; no golden then).
            var size = Value("-dad-shot-size")?.Split('x');
            if (size?.Length == 2) s.Size = new Vector2Int(int.Parse(size[0]), int.Parse(size[1]));
            s.QuitWhenDone = true;
            return s;
        }

        IEnumerator Start()
        {
            // The water moves with time; a golden must repeat, so the ripples stand still for the shot.
            Shader.SetGlobalFloat("_DadWaterFrozen", 1);
            Shader.SetGlobalFloat("_DadWaterClock", 100);
            var r = new ShotResult
            {
                view = ViewName,
                build = Application.isEditor ? "editor" : Debug.isDebugBuild ? "development player" : "release player",
                unity = Application.unityVersion,
                gpu = SystemInfo.graphicsDeviceName,
                graphicsApi = SystemInfo.graphicsDeviceType.ToString(),
                maxMeanDiff = MaxMeanDiff,
                maxChangedFraction = MaxChangedFraction,
                changedThreshold = ChangedThreshold,
            };
            Result = r;
            ViewSpec view = null;
            string startError = null;
            var cam = Camera.main;
            try
            {
                if (cam == null) throw new InvalidOperationException("no main camera");
                view = ViewSpec.Load(ViewName);
                Directory.CreateDirectory(OutDir);
            }
            catch (Exception e)
            {
                startError = e.Message;
            }
            if (startError != null)
            {
                yield return Finish(r, 1, startError);
                yield break;
            }
            Application.runInBackground = true;
            var origin = FindAnyObjectByType<FloatingOrigin>();
            var offset = new OriginOffset();
            if (origin != null)
            {
                origin.Target = cam.transform;
                origin.Recenter(view.east, view.north);
                offset = origin.Offset;
            }
            else offset.Reset(view.east, view.north);
            // Keep the camera put: a bench path or other mover must not touch it.
            var mover = cam.GetComponent<CameraPathPlayer>();
            if (mover != null) mover.Playing = false;
            Place(cam, view, offset);

            var streamer = FindAnyObjectByType<ChunkStreamer>();
            TrafficView traffic = null;
            bool driving = !string.IsNullOrEmpty(view.drive);
            if (driving) ChunkStreamer.BuildColliders = true;
            var train = FindAnyObjectByType<TrainView>();
            if (train != null && view.trainStop >= 0)
            {
                train.StandAt(view.trainStop);
                train.RunFor(view.trainRun);
            }
            bool parade = view.parade != null && view.parade.Length >= 9;
            if (view.traffic > 0 || parade)
            {
                traffic = TrafficView.Attach(gameObject, streamer, FindAnyObjectByType<DayCycle>());
                traffic.Manual = true;
                traffic.Seed = (ulong)view.trafficSeed;
                traffic.Budget = view.trafficBudget;
            }
            NpcCrowd crowd = null;
            bool lineup = view.lineup != null && view.lineup.Length >= 6;
            if (view.npcs > 0 || lineup)
            {
                crowd = NpcCrowd.Attach(gameObject, streamer, cam.transform);
                crowd.Manual = true;
            }
            if (streamer != null) streamer.SetOverlays(view.Shows("grid"), view.Shows("graph"), view.Shows("lines"));
            WorldShading.Set(WorldShading.Parse(view.shading));
            if (GraphicsTier.TryParse(view.quality, out var level)) GraphicsTier.Apply(level, cam);
            var day = FindAnyObjectByType<DayCycle>();
            if (day != null)
            {
                day.MinutesPerSecond = 0;
                day.Set(view.Date, view.time, view.Sky);
            }
            float t0 = Time.realtimeSinceStartup;
            int quiet = 0;
            while (quiet < 10)
            {
                yield return null;
                Place(cam, view, offset);
                bool idle = (streamer == null || (streamer.IndexReady && streamer.Idle && streamer.Applied > 0)) && (day == null || day.ReflectionsReady);
                quiet = idle ? quiet + 1 : 0;
                if (Time.realtimeSinceStartup - t0 > TimeoutSeconds)
                {
                    yield return Finish(r, 1, $"streaming did not settle in {TimeoutSeconds} s ({streamer?.State}, reflections {day == null || day.ReflectionsReady})");
                    yield break;
                }
            }
            r.settleSeconds = Time.realtimeSinceStartup - t0;
            if (crowd != null)
            {
                if (lineup) crowd.Lineup(view.lineup[0], view.lineup[1], view.lineup[2], view.lineup[3], view.lineup[4], view.lineup[5], view.act, view.facing, view.only);
                else crowd.Run(view.npcs);
            }
            if (streamer != null)
            {
                r.resident = streamer.Resident.Count;
                r.applied = streamer.Applied;
                foreach (var v in streamer.Resident.Values) r.failedPolygons += v.SurfaceFailed;
            }

            // The lanes go in on the next frame's Update; the simulation then runs at once.
            while (traffic != null && !traffic.Ready)
            {
                yield return null;
                Place(cam, view, offset);
                if (Time.realtimeSinceStartup - t0 > TimeoutSeconds) break;
            }
            if (parade)
            {
                var p = view.parade;
                traffic.Parade(p[0], p[1], p[2], p[3], p[4], new Vector4(p[5], p[6], p[7], p[8]));
                traffic.Submit();
            }
            else if (traffic != null)
            {
                traffic.Run(view.traffic);
                traffic.Submit();
                r.trafficState = traffic.State;
                r.trafficAgents = traffic.Sim?.Agents.Count ?? 0;
                r.trafficNear = traffic.DrawnNear;
                r.trafficFar = traffic.DrawnFar;
                if (traffic.Net != null) foreach (var c in traffic.Net.Chunks) r.trafficLanes += c.All.Length;
            }
            if (driving)
            {
                while (streamer.Colliders.Pending > 0 && Time.realtimeSinceStartup - t0 < TimeoutSeconds) yield return null;
                if (traffic == null || !traffic.NearestLane(view.east, view.north, out double lx, out double lz, out double hx, out double hz, out double ly))
                {
                    yield return Finish(r, 1, "no lane to drive on");
                    yield break;
                }
                offset.ToScene(lx, lz, out float sx, out float sz);
                var car = PlayerVehicle.Spawn(VehicleKind.Sedan, new Vector3(sx, (float)ly + 0.05f, sz), Mathf.Atan2((float)hx, (float)hz) * Mathf.Rad2Deg, day);
                traffic.Player = car;
                if (origin != null) origin.Target = car.transform;
                var chase = cam.gameObject.AddComponent<VehicleCamera>();
                chase.Attach(car);
                if (view.drive == "driver") chase.SetMode(VehicleCamera.View.Driver, true);
                car.Input = new DriveInput { Throttle = view.driveThrottle > 0 ? 1 : 0 };
                float until = Time.time + 1.5f + view.driveThrottle;
                while (Time.time < until) yield return null;
                if (view.drive == "door") car.SwingDoor(VehicleFamilies.DriverDoor, 1);
                if (view.drive == "side" || view.drive == "door" || view.drive == "front" || view.drive == "rear")
                {
                    chase.enabled = false;
                    cam.transform.position = car.transform.TransformPoint(view.drive == "door" ? new Vector3(5.4f, 0.75f, 0.3f) : view.drive == "front" ? new Vector3(1.7f, 1.05f, 5.6f) : view.drive == "rear" ? new Vector3(-1.5f, 1.2f, -5.6f) : new Vector3(-5.4f, 0.75f, 0.3f));
                    cam.transform.LookAt(car.transform.TransformPoint(0, 0.6f, 0));
                }
                r.driveSpeed = car.State.Speed;
                r.driveRpm = car.State.Rpm;
                traffic.Submit();
            }
            CityView.Active?.Submit();
            foreach (var t in FindObjectsByType<TrainView>(FindObjectsSortMode.None)) t.Submit();
            foreach (var p in FindObjectsByType<ParkedView>(FindObjectsSortMode.None)) p.Submit();
            var shot = Size.x > 0 ? Render(cam, Size.x, Size.y) : Render(cam, view.width, view.height_px);
            r.width = shot.width;
            r.height = shot.height;
            File.WriteAllBytes(Path.Combine(OutDir, "shot.png"), shot.EncodeToPNG());

            int code = 0;
            if (!string.IsNullOrEmpty(GoldenPath))
            {
                r.golden = GoldenPath;
                if (!File.Exists(GoldenPath)) r.error = "golden image missing";
                else
                {
                    var golden = new Texture2D(2, 2, TextureFormat.RGB24, false);
                    golden.LoadImage(File.ReadAllBytes(GoldenPath));
                    if (Compare(shot, golden, out r.meanAbsDiff, out r.changedFraction))
                    {
                        r.compared = true;
                        r.pass = r.meanAbsDiff <= MaxMeanDiff && r.changedFraction <= MaxChangedFraction;
                    }
                    else r.error = $"golden is {golden.width}x{golden.height}, shot is {shot.width}x{shot.height}";
                    Destroy(golden);
                }
                code = r.pass ? 0 : 2;
            }
            Destroy(shot);
            yield return Finish(r, code, r.error);
        }

        IEnumerator Finish(ShotResult r, int code, string error)
        {
            r.error = error;
            if (!string.IsNullOrEmpty(OutDir) && Directory.Exists(OutDir))
                File.WriteAllText(Path.Combine(OutDir, "shot.json"), JsonUtility.ToJson(r, true));
            if (error != null) Debug.LogError($"[Dad] shot {ViewName}: {error}");
            else Debug.Log($"[Dad] shot {ViewName}: {r.width}x{r.height}, {r.resident} chunks" +
                           (r.compared ? $", mean diff {r.meanAbsDiff:F3}, changed {r.changedFraction:P3}, {(r.pass ? "PASS" : "FAIL")}" : ", no golden"));
            Done = true;
            yield return null;
            if (QuitWhenDone) Application.Quit(code);
        }

        static void Place(Camera cam, ViewSpec v, OriginOffset offset)
        {
            offset.ToScene(v.east, v.north, out float x, out float z);
            offset.ToScene(v.lookEast, v.lookNorth, out float lx, out float lz);
            cam.fieldOfView = v.fov;
            cam.transform.position = new Vector3(x, v.height, z);
            cam.transform.LookAt(new Vector3(lx, v.lookHeight, lz));
        }

        static Texture2D Render(Camera cam, int w, int h)
        {
            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var before = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = before;
            var active = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = active;
            RenderTexture.ReleaseTemporary(rt);
            return tex;
        }

        /// <summary>Mean absolute channel difference (0-255) and the fraction of pixels with any channel over ChangedThreshold apart.</summary>
        public static bool Compare(Texture2D a, Texture2D b, out double meanAbs, out double changedFraction)
        {
            meanAbs = changedFraction = 0;
            if (a.width != b.width || a.height != b.height) return false;
            var pa = a.GetPixels32();
            var pb = b.GetPixels32();
            long sum = 0, changed = 0;
            for (int i = 0; i < pa.Length; i++)
            {
                int dr = Math.Abs(pa[i].r - pb[i].r), dg = Math.Abs(pa[i].g - pb[i].g), db = Math.Abs(pa[i].b - pb[i].b);
                sum += dr + dg + db;
                if (dr > ChangedThreshold || dg > ChangedThreshold || db > ChangedThreshold) changed++;
            }
            meanAbs = sum / (3.0 * pa.Length);
            changedFraction = changed / (double)pa.Length;
            return true;
        }
    }
}
