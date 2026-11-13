// World shader debug views (D-01): F5 cycles lit, normals, material ids, metre grid, albedo and
// far-tier facade families (D-06a).
// The view is one global shader value (_DadWorldDebug), so it reaches every chunk at once,
// including chunks that stream in later. A golden view can ask for one ("shading" in its file).
// It starts lit in every play session.
using System;
using UnityEngine;

namespace Dad.Core
{
    public enum WorldShadingView { Lit, Normals, Materials, MetreGrid, Albedo, Families }

    public static class WorldShading
    {
        static readonly int DebugId = Shader.PropertyToID("_DadWorldDebug");

        public static WorldShadingView View { get; private set; }

        public static void Set(WorldShadingView view)
        {
            View = view;
            Shader.SetGlobalFloat(DebugId, (float)view);
        }

        public static void Next() => Set((WorldShadingView)(((int)View + 1) % Enum.GetValues(typeof(WorldShadingView)).Length));

        /// <summary>"lit", "normals", "materials", "grid" or "albedo", case ignored; null or empty is lit.</summary>
        public static WorldShadingView Parse(string name)
        {
            switch (name?.ToLowerInvariant())
            {
                case null: case "": case "lit": return WorldShadingView.Lit;
                case "normals": return WorldShadingView.Normals;
                case "materials": return WorldShadingView.Materials;
                case "grid": return WorldShadingView.MetreGrid;
                case "albedo": return WorldShadingView.Albedo;
                case "families": return WorldShadingView.Families;
                default: throw new FormatException($"unknown shading view {name}: lit, normals, materials, grid, albedo or families");
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() => Set(WorldShadingView.Lit);
    }
}
