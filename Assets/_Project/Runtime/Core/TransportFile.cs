// World/transport.json, written by Tools/worldgen/gen.py: what moves and is not a chunk's. The railway as one line, with its stops, its rail heights and its level crossings, and every parked vehicle.
using System;
using System.IO;
using UnityEngine;

namespace Dad.Core
{
    [Serializable]
    public sealed class TransportFile
    {
        [Serializable]
        public sealed class Railway
        {
            public string name;
            public double[] points, stops;
            public string[] stopNames;
            public float[] levels;
            public float levelStep;
            public double[] crossingX, crossingZ, crossingHalf;
        }

        [Serializable]
        public sealed class Parked
        {
            public string[] kinds;
            public int[] kind;
            public double[] east, north;
            public float[] height, turn;
        }

        public string owner;
        public Railway railway;
        public Parked parked;

        /// <summary>The file, or null in a world without one (the tests' old fixture).</summary>
        public static TransportFile Load()
        {
            var path = Path.Combine(GameRoot.WorldDir, "transport.json");
            return File.Exists(path) ? JsonUtility.FromJson<TransportFile>(File.ReadAllText(path)) : null;
        }
    }
}
