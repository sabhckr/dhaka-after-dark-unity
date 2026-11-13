// Texture memory by source at the end of a bench (K-63): every loaded texture, what the profiler says it holds, grouped
// by where it came from. In a player, Profiler.GetRuntimeMemorySizeLong is the texture's GPU size, plus its CPU copy
// when it is readable (listed, so a readable texture is not mistaken for a large one). Needs a development player.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;

namespace Dad.Core
{
    [Serializable]
    public sealed class TextureGroup
    {
        public string group;
        public int count;
        public long bytes;
    }

    [Serializable]
    public sealed class TextureEntry
    {
        public string name, group, format;
        public int width, height, mips;
        public bool readable;
        public long bytes;
    }

    public static class TextureCensus
    {
        public const int Largest = 40;

        /// <summary>World library (names World*), the owner's city model pictures (CityModels.Pictures), render targets
        /// (shadow maps, camera targets, post effects) and the rest.</summary>
        public static string GroupOf(Texture t)
        {
            if (t is RenderTexture) return "render targets";
            if (t.name.StartsWith("World", StringComparison.Ordinal)) return "world library";
            if (CityModels.Pictures.Contains(t.name)) return "city models";
            return "other";
        }

        public static void Fill(BenchSummary s)
        {
            var groups = new Dictionary<string, TextureGroup>();
            var all = new List<TextureEntry>();
            foreach (var t in Resources.FindObjectsOfTypeAll<Texture>())
            {
                long b = Profiler.GetRuntimeMemorySizeLong(t);
                string g = GroupOf(t);
                if (!groups.TryGetValue(g, out var tg)) groups[g] = tg = new TextureGroup { group = g };
                tg.count++;
                tg.bytes += b;
                all.Add(new TextureEntry
                {
                    name = t.name, group = g, width = t.width, height = t.height, mips = t.mipmapCount, readable = t.isReadable, bytes = b,
                    format = t is Texture2D t2 ? t2.format.ToString() : t.graphicsFormat.ToString(),
                });
            }
            var list = new List<TextureGroup>(groups.Values);
            list.Sort((a, b) => b.bytes.CompareTo(a.bytes));
            s.textureGroups = list.ToArray();
            all.Sort((a, b) => b.bytes.CompareTo(a.bytes));
            s.textureLargest = all.GetRange(0, Math.Min(Largest, all.Count)).ToArray();
            s.textureCount = all.Count;
        }
    }
}
