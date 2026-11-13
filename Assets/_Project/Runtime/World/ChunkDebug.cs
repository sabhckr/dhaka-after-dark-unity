// Debug overlays for one chunk, built off the main thread as line lists: the chunk's cell square
// with its id written in the south-west corner, and the road graph (edges, a cross on every node,
// a square on every signalised node). The overlay material draws them over everything else.
//
// Ids are drawn with a seven-segment line font (digits, '-', '_', 'c'), so no font asset is needed
// and the labels show up in golden-view shots like any other mesh. They lie flat on the ground,
// reading west to east with their tops to the north.
//
// Vertices are x, y, z floats in metres from the chunk's south-west corner.
using System;
using System.Collections.Generic;

namespace Dad.World
{
    public static class SegmentFont
    {
        // Segments of a 1 x 2 cell, as (x0, y0, x1, y1): a top, b upper right, c lower right,
        // d bottom, e lower left, f upper left, g middle.
        static readonly float[][] Segments =
        {
            new[] { 0f, 2f, 1f, 2f }, new[] { 1f, 2f, 1f, 1f }, new[] { 1f, 1f, 1f, 0f }, new[] { 0f, 0f, 1f, 0f },
            new[] { 0f, 0f, 0f, 1f }, new[] { 0f, 1f, 0f, 2f }, new[] { 0f, 1f, 1f, 1f },
        };

        static readonly Dictionary<char, string> Glyphs = new Dictionary<char, string>
        {
            ['0'] = "abcdef", ['1'] = "bc", ['2'] = "abged", ['3'] = "abgcd", ['4'] = "fgbc",
            ['5'] = "afgcd", ['6'] = "afgedc", ['7'] = "abc", ['8'] = "abcdefg", ['9'] = "abcdfg",
            ['-'] = "g", ['_'] = "d", ['c'] = "ged",
        };

        /// <summary>Distance from one glyph's left edge to the next, in cell widths.</summary>
        public const float Advance = 1.5f;

        public static bool Supports(char ch) => Glyphs.ContainsKey(ch);

        /// <summary>The segments of <paramref name="ch"/> (letters a..g), or null.</summary>
        public static string Of(char ch) => Glyphs.TryGetValue(ch, out var s) ? s : null;

        /// <summary>Adds the line segments of <paramref name="text"/>: cell width <paramref name="unit"/>
        /// metres, glyphs two units tall, starting at (x, z). Returns the glyphs drawn.</summary>
        public static int Write(string text, float x, float z, float y, float unit, List<float> positions, List<int> indices)
        {
            int drawn = 0;
            for (int i = 0; i < text.Length; i++)
            {
                var segs = Of(text[i]);
                if (segs == null) continue;
                float left = x + i * Advance * unit;
                foreach (char s in segs)
                {
                    var g = Segments[s - 'a'];
                    Line(left + g[0] * unit, z + g[1] * unit, left + g[2] * unit, z + g[3] * unit, y, positions, indices);
                }
                drawn++;
            }
            return drawn;
        }

        public static void Line(float ax, float az, float bx, float bz, float y, List<float> positions, List<int> indices)
        {
            int v = positions.Count / 3;
            positions.Add(ax); positions.Add(y); positions.Add(az);
            positions.Add(bx); positions.Add(y); positions.Add(bz);
            indices.Add(v);
            indices.Add(v + 1);
        }
    }

    public sealed class ChunkDebug
    {
        public const float GridY = 0.5f, GraphY = 0.6f, LabelUnit = 12f, LabelInset = 15f, NodeArm = 1.2f, SignalHalf = 2.5f;

        public float[] GridPositions, GraphPositions;
        public int[] GridIndices, GraphIndices;
        /// <summary>Index counts of the graph mesh's three line lists, in order: edges, nodes, signals.</summary>
        public int EdgeIndexCount, NodeIndexCount, SignalIndexCount;
        public int Glyphs, EdgeSegments, Nodes, Signals;

        public static ChunkDebug Build(DadcChunk c)
        {
            var d = new ChunkDebug();
            var scratch = BuildScratch.Take();
            var p = scratch.Positions;
            var idx = scratch.Indices;
            float size = (float)ChunkCell.Size;
            SegmentFont.Line(0, 0, size, 0, GridY, p, idx);
            SegmentFont.Line(size, 0, size, size, GridY, p, idx);
            SegmentFont.Line(size, size, 0, size, GridY, p, idx);
            SegmentFont.Line(0, size, 0, 0, GridY, p, idx);
            d.Glyphs = SegmentFont.Write(c.Id, LabelInset, LabelInset, GridY, LabelUnit, p, idx);
            d.GridPositions = p.ToArray();
            d.GridIndices = idx.ToArray();

            p.Clear();
            idx.Clear();
            if (c.Edges != null)
                foreach (var e in c.Edges)
                {
                    if (e.Pts == null) continue;
                    for (int k = 0; k + 3 < e.Pts.Length; k += 2)
                    {
                        SegmentFont.Line(e.Pts[k] / 100f, e.Pts[k + 1] / 100f, e.Pts[k + 2] / 100f, e.Pts[k + 3] / 100f, GraphY, p, idx);
                        d.EdgeSegments++;
                    }
                }
            d.EdgeIndexCount = idx.Count;
            if (c.Nodes != null)
                foreach (var n in c.Nodes)
                {
                    float x = n.East / 100f, z = n.North / 100f;
                    SegmentFont.Line(x - NodeArm, z, x + NodeArm, z, GraphY, p, idx);
                    SegmentFont.Line(x, z - NodeArm, x, z + NodeArm, GraphY, p, idx);
                    d.Nodes++;
                }
            d.NodeIndexCount = idx.Count - d.EdgeIndexCount;
            if (c.Nodes != null)
                foreach (var n in c.Nodes)
                {
                    if ((n.Signal ?? 0) <= 0) continue;
                    float x = n.East / 100f, z = n.North / 100f, h = SignalHalf;
                    SegmentFont.Line(x - h, z - h, x + h, z - h, GraphY, p, idx);
                    SegmentFont.Line(x + h, z - h, x + h, z + h, GraphY, p, idx);
                    SegmentFont.Line(x + h, z + h, x - h, z + h, GraphY, p, idx);
                    SegmentFont.Line(x - h, z + h, x - h, z - h, GraphY, p, idx);
                    d.Signals++;
                }
            d.SignalIndexCount = idx.Count - d.EdgeIndexCount - d.NodeIndexCount;
            d.GraphPositions = p.ToArray();
            d.GraphIndices = idx.ToArray();
            return d;
        }
    }
}
