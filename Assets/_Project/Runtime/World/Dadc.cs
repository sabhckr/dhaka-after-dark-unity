// Reader for DADC chunk files (format 1). The specification is the field table in
// dhaka-after-dark/scripts/unity/dadc.ts; this file mirrors it in the same order.
//
// Positions stay as integer centimetres east and north of the chunk's south-west
// corner (Unity frame: x = east, z = north). Point lists are flat [east, north, ...].
// "cm" fields are value * 100 and "half" fields value * 2, exactly as stored.
// No UnityEngine dependency: safe on worker threads and testable on its own.
using System;
using System.Text;

namespace Dad.World
{
    public sealed class DadcFormatException : Exception
    {
        public DadcFormatException(string message) : base("DADC: " + message) { }
    }

    public sealed class Road
    {
        public long Id; public string Cls; public int WidthCm; public bool WidthEstimated; public int YCm; public bool Bridge; public int[] Pts;
        /// <summary>Format 2: a height at each point, for a ramp; null on a level road.</summary>
        public int[] YsCm;
        public int? Lanes; public string Sidewalk; public string Surface; public int? Lit; public int? MaxSpeedCm;
        public string Name; public string NameEn; public int? Layer; public int? Tunnel;
        /// <summary>Not in the file: the exporter's width before RoadWidths.Resolve (what a road's kind is still judged by:
        /// sidewalks, markings), and whether the lanes rule replaced it.</summary>
        public int ClassWidthCm; public bool WidthFromLanes;
    }

    public sealed class Building
    {
        public long Id; public int HeightCm; public string HeightSource; public int[] Outer; public int[][] Holes;
        public string Type; public int? LevelsHalf; public int? MinLevelHalf; public int? MinHeightCm; public string Material;
        public string Roof; public int? Layer; public string Name;
        /// <summary>Format 2: which model stands here, and how: east, north, base height in cm, turn in centidegrees, size in thousandths, mirrored (0, 1), tint 0xRRGGBB. Null on a building made from its footprint.</summary>
        public string Model; public int[] Pose;
    }

    public sealed class Poly { public long Id; public int[] Outer; public int[][] Holes; }
    public sealed class Canal { public long Id; public int WidthCm; public int[] Pts; }
    public sealed class Rail { public long Id; public int YCm; public int[] Pts; }
    public sealed class Tree { public int East; public int North; public int HeightCm; public string Kind; }
    public sealed class Landmark { public long Id; public string Name; public string Category; public int East; public int North; }
    public sealed class GraphNode { public int East; public int North; public int? Signal; public int? Crossing; }
    public sealed class GraphEdge { public long Seq; public int A; public int B; public long Way; public string Cls; public int Oneway; public int[] Pts; }
    public sealed class PointFeature { public long Id; public string Kind; public int East; public int North; public string CrossingType; public string Name; }
    public sealed class Landuse { public long Id; public string Type; public int[] Outer; public int[][] Holes; }
    public sealed class LineFeature { public long Id; public string Type; public int[] Pts; }
    public sealed class PowerLine { public long Id; public string Type; public int[] Pts; public int? KvCm; }

    /// <summary>Totals any reader can recompute; world.json stores the exporter's values per chunk.</summary>
    public struct DadcChecks
    {
        public long Positions; public long SumEastCm; public long SumNorthCm; public long SumInts;
    }

    public sealed class DadcChunk
    {
        public const int ChunkCm = 50000;
        public int Format; public int SourceSchema; public int Ix; public int Iy;
        public string[] Strings;
        public Road[] Roads; public Building[] Buildings; public Building[] Placed = Array.Empty<Building>(); public Poly[] Water; public Canal[] Canals; public Poly[] Parks;
        public Rail[] Rails; public Poly[] Plazas; public Poly[] Bridges; public Tree[] Trees; public Landmark[] Landmarks;
        public GraphNode[] Nodes; public GraphEdge[] Edges; public PointFeature[] Points; public Landuse[] Landuse;
        public LineFeature[] Barriers; public PowerLine[] Power;
        public DadcChecks Checks;
        /// <summary>Set by RoadWidths.Resolve.</summary>
        public bool WidthsResolved;
        /// <summary>The elevated rails of the eight neighbouring chunks, in this chunk's centimetres (NeighbourRails.Attach); null if not attached, and then the viaduct is built from this chunk's tracks alone.</summary>
        public Rail[] NearRails;

        public string Id => $"c_{Ix}_{Iy}";
        /// <summary>South-west corner in centimetres from the world origin (east, north).</summary>
        public long CornerEastCm => (long)Ix * ChunkCm;
        public long CornerNorthCm => -(long)(Iy + 1) * ChunkCm;

        /// <summary>The three.js graph node id ("n&lt;x cm&gt;_&lt;z cm&gt;"), shared by nodes in neighbouring chunks.</summary>
        public string NodeId(GraphNode n) => $"n{CornerEastCm + n.East}_{-(CornerNorthCm + n.North)}";
        public string EdgeId(GraphEdge e) => $"w{e.Way}:{e.Seq}";
    }

    public static class Dadc
    {
        public const int Format = 2;

        /// <summary>Decode, then the read-time rules every builder expects (RoadWidths). The world and tests load chunks this way; Decode alone is the file format.</summary>
        public static DadcChunk Load(byte[] data)
        {
            var c = Decode(data);
            RoadWidths.Resolve(c);
            return c;
        }

        public static DadcChunk Decode(byte[] data)
        {
            var r = new Reader(data);
            if (r.U8() != 'D' || r.U8() != 'A' || r.U8() != 'D' || r.U8() != 'C') throw new DadcFormatException("bad magic");
            var c = new DadcChunk { Format = r.U8() };
            if (c.Format < 1 || c.Format > Format) throw new DadcFormatException($"format {c.Format} not supported");
            c.SourceSchema = r.U8();
            c.Ix = r.Scaled();
            c.Iy = r.Scaled();
            var strings = new string[r.Count()];
            for (int i = 0; i < strings.Length; i++) strings[i] = r.Utf8(r.Count());
            c.Strings = strings;
            r.Strings = strings;

            c.Roads = r.List(() =>
            {
                var x = new Road { Id = r.Long(), Cls = r.Str(), WidthCm = r.Scaled(), WidthEstimated = r.Bool(), YCm = r.Scaled(), Bridge = r.Bool(), Pts = r.Pts() };
                var m = r.Mask(c.Format >= 2 ? 10 : 9);
                if (Has(m, 0)) x.Lanes = r.Int32();
                if (Has(m, 1)) x.Sidewalk = r.Str();
                if (Has(m, 2)) x.Surface = r.Str();
                if (Has(m, 3)) x.Lit = r.Int32();
                if (Has(m, 4)) x.MaxSpeedCm = r.Scaled();
                if (Has(m, 5)) x.Name = r.Str();
                if (Has(m, 6)) x.NameEn = r.Str();
                if (Has(m, 7)) x.Layer = r.Int32();
                if (Has(m, 8)) x.Tunnel = r.Int32();
                if (Has(m, 9)) x.YsCm = r.Scaleds();
                return x;
            });
            var buildings = r.List(() =>
            {
                var x = new Building { Id = r.Long(), HeightCm = r.Scaled(), HeightSource = r.Str(), Outer = r.Pts() };
                var m = r.Mask(c.Format >= 2 ? 11 : 9);
                if (Has(m, 0)) x.Holes = r.Rings();
                if (Has(m, 1)) x.Type = r.Str();
                if (Has(m, 2)) x.LevelsHalf = r.Scaled();
                if (Has(m, 3)) x.MinLevelHalf = r.Scaled();
                if (Has(m, 4)) x.MinHeightCm = r.Scaled();
                if (Has(m, 5)) x.Material = r.Str();
                if (Has(m, 6)) x.Roof = r.Str();
                if (Has(m, 7)) x.Layer = r.Int32();
                if (Has(m, 8)) x.Name = r.Str();
                if (Has(m, 9)) x.Model = r.Str();
                if (Has(m, 10)) x.Pose = r.Scaleds();
                return x;
            });
            // A building that is a model is drawn by CityView, not extruded: everything that makes walls from footprints reads Buildings, and only Placed keeps the models.
            int placed = 0;
            foreach (var b in buildings) if (b.Model != null) placed++;
            if (placed == 0) c.Buildings = buildings;
            else
            {
                c.Placed = new Building[placed];
                c.Buildings = new Building[buildings.Length - placed];
                int i0 = 0, i1 = 0;
                foreach (var b in buildings)
                    if (b.Model != null) c.Placed[i0++] = b;
                    else c.Buildings[i1++] = b;
            }
            c.Water = r.List(r.ReadPoly);
            c.Canals = r.List(() => { var x = new Canal { Id = r.Long(), WidthCm = r.Scaled(), Pts = r.Pts() }; r.Mask(0); return x; });
            c.Parks = r.List(r.ReadPoly);
            c.Rails = r.List(() => { var x = new Rail { Id = r.Long(), YCm = r.Scaled(), Pts = r.Pts() }; r.Mask(0); return x; });
            c.Plazas = r.List(r.ReadPoly);
            c.Bridges = r.List(r.ReadPoly);
            c.Trees = r.List(() =>
            {
                var x = new Tree();
                (x.East, x.North) = r.Xy();
                x.HeightCm = r.Scaled();
                x.Kind = r.Str();
                r.Mask(0);
                return x;
            });
            c.Landmarks = r.List(() =>
            {
                var x = new Landmark { Id = r.Long(), Name = r.Str(), Category = r.Str() };
                (x.East, x.North) = r.Xy();
                r.Mask(0);
                return x;
            });
            c.Nodes = r.List(() =>
            {
                var x = new GraphNode();
                (x.East, x.North) = r.Xy();
                var m = r.Mask(2);
                if (Has(m, 0)) x.Signal = r.Int32();
                if (Has(m, 1)) x.Crossing = r.Int32();
                return x;
            });
            int nodeCount = c.Nodes.Length;
            c.Edges = r.List(() =>
            {
                var x = new GraphEdge { Seq = r.Long(), A = r.Index(nodeCount), B = r.Index(nodeCount), Way = r.Long(), Cls = r.Str(), Oneway = r.Int32(), Pts = r.Pts() };
                r.Mask(0);
                return x;
            });
            c.Points = r.List(() =>
            {
                var x = new PointFeature { Id = r.Long(), Kind = r.Str() };
                (x.East, x.North) = r.Xy();
                var m = r.Mask(2);
                if (Has(m, 0)) x.CrossingType = r.Str();
                if (Has(m, 1)) x.Name = r.Str();
                return x;
            });
            c.Landuse = r.List(() =>
            {
                var x = new Landuse { Id = r.Long(), Type = r.Str(), Outer = r.Pts() };
                if (Has(r.Mask(1), 0)) x.Holes = r.Rings();
                return x;
            });
            c.Barriers = r.List(() => { var x = new LineFeature { Id = r.Long(), Type = r.Str(), Pts = r.Pts() }; r.Mask(0); return x; });
            c.Power = r.List(() =>
            {
                var x = new PowerLine { Id = r.Long(), Type = r.Str(), Pts = r.Pts() };
                if (Has(r.Mask(1), 0)) x.KvCm = r.Scaled();
                return x;
            });
            if (!r.Done) throw new DadcFormatException("trailing bytes");
            c.Checks = r.Checks;
            return c;
        }

        static bool Has(long mask, int bit) => ((mask >> bit) & 1) == 1;

        sealed class Reader
        {
            readonly byte[] _buf;
            int _pos;
            public string[] Strings;
            public DadcChecks Checks;

            public Reader(byte[] buf) { _buf = buf ?? throw new ArgumentNullException(nameof(buf)); }
            public int[] Scaleds()
            {
                var v = new int[Count()];
                for (int i = 0; i < v.Length; i++) v[i] = Scaled();
                return v;
            }
            public bool Done => _pos == _buf.Length;

            public int U8()
            {
                if (_pos >= _buf.Length) throw new DadcFormatException("unexpected end of data");
                return _buf[_pos++];
            }

            ulong UVar()
            {
                ulong n = 0;
                for (int shift = 0; ; shift += 7)
                {
                    if (shift > 56) throw new DadcFormatException("varint too long");
                    int b = U8();
                    n |= (ulong)(b & 127) << shift;
                    if (b < 128) return n;
                }
            }

            long SVar()
            {
                ulong n = UVar();
                return (long)(n >> 1) ^ -(long)(n & 1);
            }

            /// <summary>An "int" field: counted in the checks, like the exporter's onInt hook.</summary>
            public long Long()
            {
                long v = SVar();
                Checks.SumInts += v;
                return v;
            }

            public int Int32()
            {
                long v = Long();
                if (v < int.MinValue || v > int.MaxValue) throw new DadcFormatException("value out of int range");
                return (int)v;
            }

            /// <summary>A stored integer that is not an "int" field (cm, half): same encoding, not counted.</summary>
            public int Scaled()
            {
                long v = SVar();
                if (v < int.MinValue || v > int.MaxValue) throw new DadcFormatException("value out of int range");
                return (int)v;
            }

            public int Count()
            {
                ulong n = UVar();
                if (n > (ulong)(_buf.Length - _pos)) throw new DadcFormatException("count larger than the data"); // every element takes at least one byte
                return (int)n;
            }

            public bool Bool()
            {
                int b = U8();
                if (b > 1) throw new DadcFormatException("bad boolean");
                return b == 1;
            }

            public string Utf8(int len)
            {
                if (_pos + len > _buf.Length) throw new DadcFormatException("unexpected end of data");
                string s;
                try { s = new UTF8Encoding(false, true).GetString(_buf, _pos, len); }
                catch (ArgumentException) { throw new DadcFormatException("invalid UTF-8"); }
                _pos += len;
                return s;
            }

            public string Str()
            {
                ulong i = UVar();
                if (i >= (ulong)Strings.Length) throw new DadcFormatException("string index out of range");
                return Strings[i];
            }

            public int Index(int count)
            {
                ulong i = UVar();
                if (i >= (ulong)count) throw new DadcFormatException("node index out of range");
                return (int)i;
            }

            public long Mask(int optionalFields)
            {
                ulong m = UVar();
                if (m >> optionalFields != 0) throw new DadcFormatException("bad presence mask");
                return (long)m;
            }

            void Position(int east, int north)
            {
                Checks.Positions++;
                Checks.SumEastCm += east;
                Checks.SumNorthCm += north;
            }

            public (int, int) Xy()
            {
                int e = Scaled(), n = Scaled();
                Position(e, n);
                return (e, n);
            }

            public int[] Pts()
            {
                int count = Count();
                var pts = new int[count * 2];
                long e = 0, n = 0;
                for (int i = 0; i < count; i++)
                {
                    e += SVar();
                    n += SVar();
                    if (e < int.MinValue || e > int.MaxValue || n < int.MinValue || n > int.MaxValue) throw new DadcFormatException("position out of range");
                    pts[i * 2] = (int)e;
                    pts[i * 2 + 1] = (int)n;
                    Position((int)e, (int)n);
                }
                return pts;
            }

            public int[][] Rings()
            {
                var rings = new int[Count()][];
                for (int i = 0; i < rings.Length; i++) rings[i] = Pts();
                return rings;
            }

            public Poly ReadPoly()
            {
                var x = new Poly { Id = Long(), Outer = Pts() };
                if (Has(Mask(1), 0)) x.Holes = Rings();
                return x;
            }

            public T[] List<T>(Func<T> read)
            {
                var list = new T[Count()];
                for (int i = 0; i < list.Length; i++) list[i] = read();
                return list;
            }
        }
    }
}
