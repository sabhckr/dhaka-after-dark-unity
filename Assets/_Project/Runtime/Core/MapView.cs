// A window on the city map (2026-11-10, Docs/PRODUCTION/PLAYER_INTERFACE.md): the radar and the map screen are both this. The picture (CityMap) is moved and turned so a
// world point sits at the window's centre; the route is drawn over it; marks (blips) are placed by world point and, if asked, kept inside the edge.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Dad.Core
{
    public sealed class MapView : VisualElement
    {
        public CityMap Map;
        /// <summary>Window pixels a metre.</summary>
        public float PerMetre = 0.8f;
        /// <summary>The world point at the window's centre.</summary>
        public double East, North;
        /// <summary>The heading (degrees clockwise from north) that is up in the window: 0 for north up.</summary>
        public float Turn;
        public float RouteWidth = 5;

        readonly VisualElement layer, lines;
        readonly List<(Label label, double east, double north)> names = new List<(Label, double, double)>();
        Texture2D shown;
        float sized = -1;
        int routeShown = -1;

        public MapView()
        {
            pickingMode = PickingMode.Ignore;
            style.overflow = Overflow.Hidden;
            style.backgroundColor = new Color(21 / 255f, 24 / 255f, 29 / 255f);
            layer = new VisualElement { pickingMode = PickingMode.Ignore };
            layer.style.position = Position.Absolute;
            layer.style.left = layer.style.top = 0;
            Add(layer);
            lines = new VisualElement { pickingMode = PickingMode.Ignore };
            lines.style.position = Position.Absolute;
            lines.style.left = lines.style.top = lines.style.right = lines.style.bottom = 0;
            lines.generateVisualContent += DrawRoute;
            layer.Add(lines);
        }

        public float ViewWidth => float.IsNaN(resolvedStyle.width) ? 0 : resolvedStyle.width;
        public float ViewHeight => float.IsNaN(resolvedStyle.height) ? 0 : resolvedStyle.height;

        /// <summary>Puts the district names on the map, turning with it.</summary>
        public void ShowNames(float size)
        {
            if (Map == null || names.Count > 0) return;
            foreach (var a in Map.Areas)
            {
                var l = Ui.Text(a.name.ToUpperInvariant(), size, Ui.Fade(Ui.Silver, 0.85f), true, TextAnchor.MiddleCenter);
                l.style.position = Position.Absolute;
                l.style.width = 400;
                l.style.marginLeft = -200;
                l.style.letterSpacing = 3;
                layer.Add(l);
                names.Add((l, a.east, a.north));
            }
            sized = -1;
        }

        /// <summary>Moves the picture to East, North and Turn; redraws the route when its version changes. Called every frame the window is seen.</summary>
        public void Refresh(int routeVersion)
        {
            if (Map == null || Map.Picture == null) { Ui.Show(layer, false); return; }
            Ui.Show(layer, true);
            if (shown != Map.Picture) { shown = Map.Picture; layer.style.backgroundImage = new StyleBackground(shown); }
            if (!Mathf.Approximately(sized, PerMetre))
            {
                sized = PerMetre;
                layer.style.width = (float)(Map.WidthMetres * PerMetre);
                layer.style.height = (float)(Map.HeightMetres * PerMetre);
                foreach (var (label, e, n) in names)
                {
                    var p = Layer(e, n);
                    label.style.left = p.x;
                    label.style.top = p.y - 14;
                }
                lines.MarkDirtyRepaint();
            }
            var at = Layer(East, North);
            layer.style.transformOrigin = new TransformOrigin(at.x, at.y, 0);
            layer.style.translate = new Translate(ViewWidth / 2 - at.x, ViewHeight / 2 - at.y, 0);
            layer.style.rotate = new Rotate(new Angle(-Turn, AngleUnit.Degree));
            if (routeVersion != routeShown) { routeShown = routeVersion; lines.MarkDirtyRepaint(); }
        }

        // Where a world point is on the picture, scaled.
        Vector2 Layer(double east, double north) => new Vector2((float)((east - Map.West) * PerMetre), (float)((Map.HeightMetres - (north - Map.South)) * PerMetre));

        /// <summary>Where a world point is in the window.</summary>
        public Vector2 ToView(double east, double north)
        {
            float x = (float)((east - East) * PerMetre), y = (float)(-(north - North) * PerMetre);
            float a = -Turn * Mathf.Deg2Rad, c = Mathf.Cos(a), s = Mathf.Sin(a);
            return new Vector2(ViewWidth / 2 + x * c - y * s, ViewHeight / 2 + x * s + y * c);
        }

        /// <summary>The world point under a point of the window.</summary>
        public void FromView(Vector2 v, out double east, out double north)
        {
            float x = v.x - ViewWidth / 2, y = v.y - ViewHeight / 2;
            float a = Turn * Mathf.Deg2Rad, c = Mathf.Cos(a), s = Mathf.Sin(a);
            float rx = x * c - y * s, ry = x * s + y * c;
            east = East + rx / PerMetre;
            north = North - ry / PerMetre;
        }

        /// <summary>Places a mark (whose own size is its width and height) at a world point; inside the edge by inset if clamp, else hidden off the window. True if it is on the window.</summary>
        public bool Place(VisualElement mark, double east, double north, bool clamp, float inset = 10)
        {
            var p = ToView(east, north);
            float w = ViewWidth, h = ViewHeight;
            bool inside = p.x >= inset && p.y >= inset && p.x <= w - inset && p.y <= h - inset;
            if (!inside && clamp)
            {
                // along the line from the centre to the point, to where it meets the edge
                var c = new Vector2(w / 2, h / 2);
                var d = p - c;
                float sx = Mathf.Abs(d.x) > 0.001f ? (w / 2 - inset) / Mathf.Abs(d.x) : float.MaxValue, sy = Mathf.Abs(d.y) > 0.001f ? (h / 2 - inset) / Mathf.Abs(d.y) : float.MaxValue;
                p = c + d * Mathf.Min(1, Mathf.Min(sx, sy));
            }
            Ui.Show(mark, inside || clamp);
            float mw = float.IsNaN(mark.resolvedStyle.width) ? 0 : mark.resolvedStyle.width, mh = float.IsNaN(mark.resolvedStyle.height) ? 0 : mark.resolvedStyle.height;
            mark.style.translate = new Translate(p.x - mw / 2, p.y - mh / 2, 0);
            return inside;
        }

        void DrawRoute(MeshGenerationContext ctx)
        {
            var way = Waypoint.Route;
            if (Map == null || way == null || way.Count < 2) return;
            var p = ctx.painter2D;
            p.lineWidth = RouteWidth;
            p.lineJoin = LineJoin.Round;
            p.lineCap = LineCap.Round;
            p.strokeColor = Waypoint.ForJob ? Ui.Job : Ui.Route;
            p.BeginPath();
            p.MoveTo(Layer(way[0].x, way[0].y));
            for (int i = 1; i < way.Count; i++) p.LineTo(Layer(way[i].x, way[i].y));
            p.Stroke();
        }
    }

    /// <summary>The place you have marked on the map, and the way to it along the roads, found again when you leave it.</summary>
    public static class Waypoint
    {
        /// <summary>How near counts as arrived, and how far off the route before a new one is found.</summary>
        public const float Arrive = 20, Stray = 40;

        public static bool Active { get; private set; }
        /// <summary>The waypoint is a job's (Jobs): placed without a notice, and kept when you reach it, as the job decides when it is done.</summary>
        public static bool ForJob { get; private set; }
        public static double East { get; private set; }
        public static double North { get; private set; }
        /// <summary>World points (east, north) from you to the waypoint; changes Version when it changes.</summary>
        public static List<Vector2> Route { get; private set; } = new List<Vector2>();
        public static int Version { get; private set; }

        static System.Threading.Tasks.Task<List<Vector2>> finding;
        static float nextCheck;

        public static void Place(double east, double north, bool job = false)
        {
            Active = true;
            ForJob = job;
            East = east;
            North = north;
            Route = new List<Vector2>();
            finding = null;
            nextCheck = 0;
            Version++;
            if (!job) Notice.Post("Waypoint set");
        }

        public static void Clear()
        {
            if (!Active) return;
            Active = false;
            ForJob = false;
            Route = new List<Vector2>();
            finding = null;
            Version++;
        }

        public static double Distance(double east, double north) => Math.Sqrt((East - east) * (East - east) + (North - north) * (North - north));

        /// <summary>Keeps the route up with you: called every frame with where you are. A route is found on a worker; a second is the most it waits to look again.</summary>
        public static void Follow(double east, double north, bool now = false)
        {
            if (!Active) return;
            if (finding != null)
            {
                if (!finding.IsCompleted) return;
                if (finding.Status == System.Threading.Tasks.TaskStatus.RanToCompletion)
                {
                    var found = finding.Result;
                    // no way by the roads (off the graph): a straight line, which is at least the direction
                    Route = found.Count >= 2 ? found : new List<Vector2> { new Vector2((float)east, (float)north), new Vector2((float)East, (float)North) };
                    Version++;
                }
                finding = null;
            }
            if (!ForJob && Distance(east, north) < Arrive) { Clear(); Notice.Post("You have arrived"); return; }
            if (!now && Time.unscaledTime < nextCheck) return;
            nextCheck = Time.unscaledTime + 1;
            var map = CityMap.Active;
            if (map == null) return;
            int near = Nearest(east, north, out double off);
            if (Route.Count < 2 || off > Stray)
            {
                double te = East, tn = North;
                if (now) { Route = map.Route(east, north, te, tn); if (Route.Count < 2) Route = new List<Vector2> { new Vector2((float)east, (float)north), new Vector2((float)te, (float)tn) }; Version++; }
                else finding = System.Threading.Tasks.Task.Run(() => map.Route(east, north, te, tn));
                return;
            }
            // the way behind you is dropped; the line starts where you are
            if (near > 0) Route.RemoveRange(0, near);
            Route[0] = new Vector2((float)east, (float)north);
            Version++;
        }

        // The route's segment nearest you (its first point's index) and how far off it you are.
        static int Nearest(double east, double north, out double off)
        {
            off = double.MaxValue;
            int best = 0;
            for (int i = 0; i + 1 < Route.Count; i++)
            {
                double ax = Route[i].x, az = Route[i].y, dx = Route[i + 1].x - ax, dz = Route[i + 1].y - az, l = dx * dx + dz * dz;
                double t = l > 0 ? Math.Clamp(((east - ax) * dx + (north - az) * dz) / l, 0, 1) : 0;
                double x = ax + t * dx - east, z = az + t * dz - north, d = Math.Sqrt(x * x + z * z);
                if (d < off) { off = d; best = i; }
            }
            return best;
        }
    }
}
