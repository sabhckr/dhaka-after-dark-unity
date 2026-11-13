// Road widths from the lanes tag (K-37, proposal P-1, approved by the owner 2026-11-03, narrowing only).
//
// The exporter gives a road with no width tag the width of its class (classify.ts HIGHWAY_WIDTH_M:
// trunk 14 m, primary 11, secondary 9, ...), and gives it to each one-way carriageway of a dual road
// too, whatever its lanes tag says. Here, after decoding and before anything is built, a road whose
// width is estimated, that is one-way in the routing graph and that carries a lanes tag takes
// lanes x LaneWidth instead (3.5 m, the owner's choice) when that is narrower. Mapped widths,
// two-way roads, roads without a lanes tag, and roads the lanes tag would widen keep the exporter's
// width: widening put Manik Mia Avenue's 7-lane carriageways at 24.5 m, over its mapped median trees,
// so the owner chose narrowing only (2026-11-03). The DADC files and the reader are unchanged: the
// class width is kept in ClassWidthCm, and WidthFromLanes marks the roads this rule changed. Only the
// carriageway takes the new width: whether a road has sidewalks and is marked as a major road is still
// judged by its class width, so the rule moves kerbs and adds or removes no sidewalks or lines.
using System;
using System.Collections.Generic;

namespace Dad.World
{
    public static class RoadWidths
    {
        public const double LaneWidth = 3.5;
        public const int LaneWidthCm = 350;

        [ThreadStatic] static HashSet<long> oneWay;

        /// <summary>Applies the lanes rule to c's roads, once; later calls do nothing.</summary>
        public static void Resolve(DadcChunk c)
        {
            if (c == null || c.WidthsResolved) return;
            c.WidthsResolved = true;
            if (c.Roads == null) return;
            foreach (var r in c.Roads) r.ClassWidthCm = r.WidthCm;
            if (c.Edges == null) return;
            var ways = oneWay ??= new HashSet<long>();
            ways.Clear();
            foreach (var e in c.Edges)
                if (e.Oneway != 0) ways.Add(e.Way);
            foreach (var r in c.Roads)
            {
                if (!r.WidthEstimated || r.Lanes is not int lanes || lanes <= 0 || !ways.Contains(r.Id) || lanes * LaneWidthCm >= r.WidthCm) continue;
                r.WidthCm = lanes * LaneWidthCm;
                r.WidthFromLanes = true;
            }
            ways.Clear();
        }
    }
}
