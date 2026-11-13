// Where the builder tests read their chunks. Their fixtures are the old map, frozen outside Assets in Tests/Fixtures/OldWorld and never shipped: the builders are the same code, and those streets
// are what their expected counts were measured on. The new city is checked by CityMapTests and Tools/worldgen/check.py.
using System.IO;
using UnityEngine;

namespace Dad.Tests
{
    internal static class TestWorld
    {
        /// <summary>The city the game ships, for tests that walk every chunk.</summary>
        public static string City => Path.Combine(Application.streamingAssetsPath, "World");
        public const int CityChunks = 62;

        public static string Root => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Tests", "Fixtures", "OldWorld"));
    }
}
