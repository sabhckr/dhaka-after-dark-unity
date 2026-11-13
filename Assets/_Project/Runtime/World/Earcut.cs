// Polygon triangulation with holes: a C# port of the ear-clipping algorithm of mapbox/earcut
// 2.2 (ISC licence, (c) Mapbox), the same algorithm three.js ShapeUtils uses for the browser
// build's flat surfaces. Coordinates are a flat [x, y, x, y, ...] array; holeStarts gives the
// vertex index where each hole ring begins. Output is vertex-index triples. Orientation of the
// triples is not fixed; callers orient them. No UnityEngine dependency: safe on worker threads.
// Nodes come from the current BuildScratch set and each call starts it over, since a chunk's
// thousands of polygons would otherwise each leave their nodes to the collector (C-07).
using System;
using System.Collections.Generic;

namespace Dad.World
{
    public static class Earcut
    {
        internal sealed class Node
        {
            public int I;
            public double X, Y;
            public Node Prev, Next, PrevZ, NextZ;
            public int Z;
            public bool Steiner;
        }

        // The node list of the BuildScratch set this call started with.
        [ThreadStatic] static List<Node> pool;
        [ThreadStatic] static int used;

        /// <summary>A node as new, from the pool; a call's nodes are reused by the next call.</summary>
        static Node NewNode(int i, double x, double y)
        {
            Node n;
            if (used < pool.Count)
            {
                n = pool[used];
                n.Prev = n.Next = n.PrevZ = n.NextZ = null;
                n.Z = 0;
                n.Steiner = false;
            }
            else
            {
                n = new Node();
                pool.Add(n);
            }
            used++;
            n.I = i;
            n.X = x;
            n.Y = y;
            return n;
        }

        /// <summary>Appends triangles to <paramref name="triangles"/>; returns how many were added.</summary>
        public static int Triangulate(IReadOnlyList<double> data, IReadOnlyList<int> holeStarts, List<int> triangles)
        {
            int before = triangles.Count;
            pool = BuildScratch.Current.EarcutNodes;
            used = 0;
            bool hasHoles = holeStarts != null && holeStarts.Count > 0;
            int outerLen = hasHoles ? holeStarts[0] * 2 : data.Count;
            var outer = LinkedList(data, 0, outerLen, true);
            if (outer == null || outer.Next == outer.Prev) return 0;
            if (hasHoles) outer = EliminateHoles(data, holeStarts, outer);

            double minX = 0, minY = 0, invSize = 0;
            if (data.Count > 80 * 2)
            {
                double maxX, maxY;
                minX = maxX = data[0];
                minY = maxY = data[1];
                for (int i = 2; i < outerLen; i += 2)
                {
                    double x = data[i], y = data[i + 1];
                    if (x < minX) minX = x;
                    if (y < minY) minY = y;
                    if (x > maxX) maxX = x;
                    if (y > maxY) maxY = y;
                }
                invSize = Math.Max(maxX - minX, maxY - minY);
                invSize = invSize != 0 ? 32767 / invSize : 0;
            }
            EarcutLinked(outer, triangles, minX, minY, invSize, 0);
            return (triangles.Count - before) / 3;
        }

        static Node LinkedList(IReadOnlyList<double> data, int start, int end, bool clockwise)
        {
            Node last = null;
            if (clockwise == (SignedArea(data, start, end) > 0))
                for (int i = start; i < end; i += 2) last = Insert(i / 2, data[i], data[i + 1], last);
            else
                for (int i = end - 2; i >= start; i -= 2) last = Insert(i / 2, data[i], data[i + 1], last);
            if (last != null && Equals(last, last.Next))
            {
                Remove(last);
                last = last.Next;
            }
            return last;
        }

        static Node FilterPoints(Node start, Node end = null)
        {
            if (start == null) return null;
            if (end == null) end = start;
            var p = start;
            bool again;
            do
            {
                again = false;
                if (!p.Steiner && (Equals(p, p.Next) || Area(p.Prev, p, p.Next) == 0))
                {
                    Remove(p);
                    p = end = p.Prev;
                    if (p == p.Next) break;
                    again = true;
                }
                else p = p.Next;
            } while (again || p != end);
            return end;
        }

        static void EarcutLinked(Node ear, List<int> triangles, double minX, double minY, double invSize, int pass)
        {
            if (ear == null) return;
            if (pass == 0 && invSize != 0) IndexCurve(ear, minX, minY, invSize);
            var stop = ear;
            while (ear.Prev != ear.Next)
            {
                var prev = ear.Prev;
                var next = ear.Next;
                if (invSize != 0 ? IsEarHashed(ear, minX, minY, invSize) : IsEar(ear))
                {
                    triangles.Add(prev.I);
                    triangles.Add(ear.I);
                    triangles.Add(next.I);
                    Remove(ear);
                    ear = next.Next;
                    stop = next.Next;
                    continue;
                }
                ear = next;
                if (ear == stop)
                {
                    if (pass == 0) EarcutLinked(FilterPoints(ear), triangles, minX, minY, invSize, 1);
                    else if (pass == 1)
                    {
                        ear = CureLocalIntersections(FilterPoints(ear), triangles);
                        EarcutLinked(ear, triangles, minX, minY, invSize, 2);
                    }
                    else SplitEarcut(ear, triangles, minX, minY, invSize);
                    break;
                }
            }
        }

        static bool IsEar(Node ear)
        {
            Node a = ear.Prev, b = ear, c = ear.Next;
            if (Area(a, b, c) >= 0) return false;
            double x0 = Math.Min(a.X, Math.Min(b.X, c.X)), y0 = Math.Min(a.Y, Math.Min(b.Y, c.Y));
            double x1 = Math.Max(a.X, Math.Max(b.X, c.X)), y1 = Math.Max(a.Y, Math.Max(b.Y, c.Y));
            for (var p = c.Next; p != a; p = p.Next)
                if (p.X >= x0 && p.X <= x1 && p.Y >= y0 && p.Y <= y1 &&
                    PointInTriangle(a.X, a.Y, b.X, b.Y, c.X, c.Y, p.X, p.Y) && Area(p.Prev, p, p.Next) >= 0)
                    return false;
            return true;
        }

        static bool Blocks(Node p, Node a, Node c, double x0, double y0, double x1, double y1, Node b) =>
            p.X >= x0 && p.X <= x1 && p.Y >= y0 && p.Y <= y1 && p != a && p != c &&
            PointInTriangle(a.X, a.Y, b.X, b.Y, c.X, c.Y, p.X, p.Y) && Area(p.Prev, p, p.Next) >= 0;

        static bool IsEarHashed(Node ear, double minX, double minY, double invSize)
        {
            Node a = ear.Prev, b = ear, c = ear.Next;
            if (Area(a, b, c) >= 0) return false;
            double x0 = Math.Min(a.X, Math.Min(b.X, c.X)), y0 = Math.Min(a.Y, Math.Min(b.Y, c.Y));
            double x1 = Math.Max(a.X, Math.Max(b.X, c.X)), y1 = Math.Max(a.Y, Math.Max(b.Y, c.Y));
            int minZ = ZOrder(x0, y0, minX, minY, invSize), maxZ = ZOrder(x1, y1, minX, minY, invSize);
            Node p = ear.PrevZ, n = ear.NextZ;
            while (p != null && p.Z >= minZ && n != null && n.Z <= maxZ)
            {
                if (Blocks(p, a, c, x0, y0, x1, y1, b)) return false;
                p = p.PrevZ;
                if (Blocks(n, a, c, x0, y0, x1, y1, b)) return false;
                n = n.NextZ;
            }
            while (p != null && p.Z >= minZ)
            {
                if (Blocks(p, a, c, x0, y0, x1, y1, b)) return false;
                p = p.PrevZ;
            }
            while (n != null && n.Z <= maxZ)
            {
                if (Blocks(n, a, c, x0, y0, x1, y1, b)) return false;
                n = n.NextZ;
            }
            return true;
        }

        static Node CureLocalIntersections(Node start, List<int> triangles)
        {
            var p = start;
            do
            {
                Node a = p.Prev, b = p.Next.Next;
                if (!Equals(a, b) && Intersects(a, p, p.Next, b) && LocallyInside(a, b) && LocallyInside(b, a))
                {
                    triangles.Add(a.I);
                    triangles.Add(p.I);
                    triangles.Add(b.I);
                    Remove(p);
                    Remove(p.Next);
                    p = start = b;
                }
                p = p.Next;
            } while (p != start);
            return FilterPoints(p);
        }

        static void SplitEarcut(Node start, List<int> triangles, double minX, double minY, double invSize)
        {
            var a = start;
            do
            {
                var b = a.Next.Next;
                while (b != a.Prev)
                {
                    if (a.I != b.I && IsValidDiagonal(a, b))
                    {
                        var c = SplitPolygon(a, b);
                        a = FilterPoints(a, a.Next);
                        c = FilterPoints(c, c.Next);
                        EarcutLinked(a, triangles, minX, minY, invSize, 0);
                        EarcutLinked(c, triangles, minX, minY, invSize, 0);
                        return;
                    }
                    b = b.Next;
                }
                a = a.Next;
            } while (a != start);
        }

        static Node EliminateHoles(IReadOnlyList<double> data, IReadOnlyList<int> holeStarts, Node outer)
        {
            var queue = new List<Node>(holeStarts.Count);
            for (int i = 0; i < holeStarts.Count; i++)
            {
                int start = holeStarts[i] * 2;
                int end = i < holeStarts.Count - 1 ? holeStarts[i + 1] * 2 : data.Count;
                var list = LinkedList(data, start, end, false);
                if (list == null) continue;
                if (list == list.Next) list.Steiner = true;
                queue.Add(Leftmost(list));
            }
            queue.Sort((a, b) => a.X.CompareTo(b.X));
            foreach (var h in queue) outer = EliminateHole(h, outer);
            return outer;
        }

        static Node EliminateHole(Node hole, Node outer)
        {
            var bridge = FindHoleBridge(hole, outer);
            if (bridge == null) return outer;
            var reverse = SplitPolygon(bridge, hole);
            FilterPoints(reverse, reverse.Next);
            return FilterPoints(bridge, bridge.Next);
        }

        static Node FindHoleBridge(Node hole, Node outer)
        {
            var p = outer;
            double hx = hole.X, hy = hole.Y, qx = double.NegativeInfinity;
            Node m = null;
            do
            {
                if (hy <= p.Y && hy >= p.Next.Y && p.Next.Y != p.Y)
                {
                    double x = p.X + (hy - p.Y) * (p.Next.X - p.X) / (p.Next.Y - p.Y);
                    if (x <= hx && x > qx)
                    {
                        qx = x;
                        m = p.X < p.Next.X ? p : p.Next;
                        if (x == hx) return m;
                    }
                }
                p = p.Next;
            } while (p != outer);
            if (m == null) return null;

            var stop = m;
            double mx = m.X, my = m.Y, tanMin = double.PositiveInfinity;
            p = m;
            do
            {
                if (hx >= p.X && p.X >= mx && hx != p.X &&
                    PointInTriangle(hy < my ? hx : qx, hy, mx, my, hy < my ? qx : hx, hy, p.X, p.Y))
                {
                    double tan = Math.Abs(hy - p.Y) / (hx - p.X);
                    if (LocallyInside(p, hole) &&
                        (tan < tanMin || (tan == tanMin && (p.X > m.X || (p.X == m.X && SectorContainsSector(m, p))))))
                    {
                        m = p;
                        tanMin = tan;
                    }
                }
                p = p.Next;
            } while (p != stop);
            return m;
        }

        static bool SectorContainsSector(Node m, Node p) => Area(m.Prev, m, p.Prev) < 0 && Area(p.Next, m, m.Next) < 0;

        static void IndexCurve(Node start, double minX, double minY, double invSize)
        {
            var p = start;
            do
            {
                if (p.Z == 0) p.Z = ZOrder(p.X, p.Y, minX, minY, invSize);
                p.PrevZ = p.Prev;
                p.NextZ = p.Next;
                p = p.Next;
            } while (p != start);
            p.PrevZ.NextZ = null;
            p.PrevZ = null;
            SortLinked(p);
        }

        // Simon Tatham's linked-list merge sort, on the z links.
        static void SortLinked(Node list)
        {
            int inSize = 1, merges;
            do
            {
                var p = list;
                list = null;
                Node tail = null;
                merges = 0;
                while (p != null)
                {
                    merges++;
                    var q = p;
                    int pSize = 0;
                    for (int i = 0; i < inSize; i++)
                    {
                        pSize++;
                        q = q.NextZ;
                        if (q == null) break;
                    }
                    int qSize = inSize;
                    while (pSize > 0 || (qSize > 0 && q != null))
                    {
                        Node e;
                        if (pSize != 0 && (qSize == 0 || q == null || p.Z <= q.Z)) { e = p; p = p.NextZ; pSize--; }
                        else { e = q; q = q.NextZ; qSize--; }
                        if (tail != null) tail.NextZ = e;
                        else list = e;
                        e.PrevZ = tail;
                        tail = e;
                    }
                    p = q;
                }
                tail.NextZ = null;
                inSize *= 2;
            } while (merges > 1);
        }

        static int ZOrder(double fx, double fy, double minX, double minY, double invSize)
        {
            int x = (int)((fx - minX) * invSize), y = (int)((fy - minY) * invSize);
            x = (x | (x << 8)) & 0x00FF00FF;
            x = (x | (x << 4)) & 0x0F0F0F0F;
            x = (x | (x << 2)) & 0x33333333;
            x = (x | (x << 1)) & 0x55555555;
            y = (y | (y << 8)) & 0x00FF00FF;
            y = (y | (y << 4)) & 0x0F0F0F0F;
            y = (y | (y << 2)) & 0x33333333;
            y = (y | (y << 1)) & 0x55555555;
            return x | (y << 1);
        }

        static Node Leftmost(Node start)
        {
            Node p = start, left = start;
            do
            {
                if (p.X < left.X || (p.X == left.X && p.Y < left.Y)) left = p;
                p = p.Next;
            } while (p != start);
            return left;
        }

        static bool PointInTriangle(double ax, double ay, double bx, double by, double cx, double cy, double px, double py) =>
            (cx - px) * (ay - py) >= (ax - px) * (cy - py) &&
            (ax - px) * (by - py) >= (bx - px) * (ay - py) &&
            (bx - px) * (cy - py) >= (cx - px) * (by - py);

        static bool IsValidDiagonal(Node a, Node b) =>
            a.Next.I != b.I && a.Prev.I != b.I && !IntersectsPolygon(a, b) &&
            ((LocallyInside(a, b) && LocallyInside(b, a) && MiddleInside(a, b) &&
              (Area(a.Prev, a, b.Prev) != 0 || Area(a, b.Prev, b) != 0)) ||
             (Equals(a, b) && Area(a.Prev, a, a.Next) > 0 && Area(b.Prev, b, b.Next) > 0));

        static double Area(Node p, Node q, Node r) => (q.Y - p.Y) * (r.X - q.X) - (q.X - p.X) * (r.Y - q.Y);

        static bool Equals(Node a, Node b) => a.X == b.X && a.Y == b.Y;

        static bool Intersects(Node p1, Node q1, Node p2, Node q2)
        {
            int o1 = Math.Sign(Area(p1, q1, p2)), o2 = Math.Sign(Area(p1, q1, q2));
            int o3 = Math.Sign(Area(p2, q2, p1)), o4 = Math.Sign(Area(p2, q2, q1));
            if (o1 != o2 && o3 != o4) return true;
            if (o1 == 0 && OnSegment(p1, p2, q1)) return true;
            if (o2 == 0 && OnSegment(p1, q2, q1)) return true;
            if (o3 == 0 && OnSegment(p2, p1, q2)) return true;
            if (o4 == 0 && OnSegment(p2, q1, q2)) return true;
            return false;
        }

        static bool OnSegment(Node p, Node q, Node r) =>
            q.X <= Math.Max(p.X, r.X) && q.X >= Math.Min(p.X, r.X) && q.Y <= Math.Max(p.Y, r.Y) && q.Y >= Math.Min(p.Y, r.Y);

        static bool IntersectsPolygon(Node a, Node b)
        {
            var p = a;
            do
            {
                if (p.I != a.I && p.Next.I != a.I && p.I != b.I && p.Next.I != b.I && Intersects(p, p.Next, a, b)) return true;
                p = p.Next;
            } while (p != a);
            return false;
        }

        static bool LocallyInside(Node a, Node b) =>
            Area(a.Prev, a, a.Next) < 0
                ? Area(a, b, a.Next) >= 0 && Area(a, a.Prev, b) >= 0
                : Area(a, b, a.Prev) < 0 || Area(a, a.Next, b) < 0;

        static bool MiddleInside(Node a, Node b)
        {
            var p = a;
            bool inside = false;
            double px = (a.X + b.X) / 2, py = (a.Y + b.Y) / 2;
            do
            {
                if ((p.Y > py) != (p.Next.Y > py) && p.Next.Y != p.Y &&
                    px < (p.Next.X - p.X) * (py - p.Y) / (p.Next.Y - p.Y) + p.X)
                    inside = !inside;
                p = p.Next;
            } while (p != a);
            return inside;
        }

        static Node SplitPolygon(Node a, Node b)
        {
            var a2 = NewNode(a.I, a.X, a.Y);
            var b2 = NewNode(b.I, b.X, b.Y);
            Node an = a.Next, bp = b.Prev;
            a.Next = b;
            b.Prev = a;
            a2.Next = an;
            an.Prev = a2;
            b2.Next = a2;
            a2.Prev = b2;
            bp.Next = b2;
            b2.Prev = bp;
            return b2;
        }

        static Node Insert(int i, double x, double y, Node last)
        {
            var p = NewNode(i, x, y);
            if (last == null)
            {
                p.Prev = p;
                p.Next = p;
            }
            else
            {
                p.Next = last.Next;
                p.Prev = last;
                last.Next.Prev = p;
                last.Next = p;
            }
            return p;
        }

        static void Remove(Node p)
        {
            p.Next.Prev = p.Prev;
            p.Prev.Next = p.Next;
            if (p.PrevZ != null) p.PrevZ.NextZ = p.NextZ;
            if (p.NextZ != null) p.NextZ.PrevZ = p.PrevZ;
        }

        static double SignedArea(IReadOnlyList<double> data, int start, int end)
        {
            double sum = 0;
            for (int i = start, j = end - 2; i < end; i += 2)
            {
                sum += (data[j] - data[i]) * (data[i + 1] + data[j + 1]);
                j = i;
            }
            return sum;
        }
    }
}
