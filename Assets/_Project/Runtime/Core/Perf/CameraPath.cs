// A fixed camera path for benchmarks: timed points in world metres, Catmull-Rom between them.
// The same path and time always give the same pose, so captures from different builds compare.
// Paths live in Resources/CameraPaths/<name>.json.
using System;
using System.IO;
using UnityEngine;

namespace Dad.Core
{
    [Serializable]
    public sealed class CameraPath
    {
        [Serializable]
        public struct Point
        {
            public float t;
            public double east, north;
            public float height;
            public double lookEast, lookNorth;
            public float lookHeight;
        }

        public string name;
        public float duration;
        public Point[] points;

        public static CameraPath Load(string name)
        {
            var text = Resources.Load<TextAsset>("CameraPaths/" + name);
            if (text == null) throw new FileNotFoundException("no camera path Resources/CameraPaths/" + name);
            return Parse(text.text);
        }

        public static CameraPath Parse(string json)
        {
            var p = JsonUtility.FromJson<CameraPath>(json);
            if (p?.points == null || p.points.Length < 2) throw new FormatException("camera path needs at least 2 points");
            if (p.points[0].t != 0f) throw new FormatException("camera path must start at t = 0");
            for (int i = 1; i < p.points.Length; i++)
                if (!(p.points[i].t > p.points[i - 1].t)) throw new FormatException($"camera path times must increase (point {i})");
            if (p.points[p.points.Length - 1].t != p.duration) throw new FormatException("camera path must end at its duration");
            return p;
        }

        /// <summary>The pose at <paramref name="time"/> seconds, clamped to the path.</summary>
        public Point Evaluate(float time)
        {
            var pts = points;
            if (time <= 0f) return pts[0];
            if (time >= duration) return pts[pts.Length - 1];
            int i = 0;
            while (pts[i + 1].t < time) i++;
            Point p0 = pts[Math.Max(i - 1, 0)], p1 = pts[i], p2 = pts[i + 1], p3 = pts[Math.Min(i + 2, pts.Length - 1)];
            double u = (time - p1.t) / (p2.t - p1.t);
            return new Point
            {
                t = time,
                east = Spline(p0.east, p1.east, p2.east, p3.east, u),
                north = Spline(p0.north, p1.north, p2.north, p3.north, u),
                height = (float)Spline(p0.height, p1.height, p2.height, p3.height, u),
                lookEast = Spline(p0.lookEast, p1.lookEast, p2.lookEast, p3.lookEast, u),
                lookNorth = Spline(p0.lookNorth, p1.lookNorth, p2.lookNorth, p3.lookNorth, u),
                lookHeight = (float)Spline(p0.lookHeight, p1.lookHeight, p2.lookHeight, p3.lookHeight, u),
            };
        }

        // Uniform Catmull-Rom: passes through p1 at u = 0 and p2 at u = 1.
        static double Spline(double p0, double p1, double p2, double p3, double u)
        {
            double u2 = u * u, u3 = u2 * u;
            return 0.5 * (2 * p1 + (p2 - p0) * u + (2 * p0 - 5 * p1 + 4 * p2 - p3) * u2 + (3 * p1 - p0 - 3 * p2 + p3) * u3);
        }
    }
}
