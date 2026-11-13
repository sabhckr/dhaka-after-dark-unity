// The physics layers (E-04). Project Settings has the same names on these numbers (TagManager.asset).
namespace Dad.Core
{
    public static class WorldLayers
    {
        public const int World = 6, Vehicle = 7, Traffic = 8, Player = 9, Body = 10;
        /// <summary>People's parts that a shot meets (Hitbox).</summary>
        public const int Bodies = 1 << Body;
        /// <summary>What a wheel stands on, and what a camera must not pass through.</summary>
        public const int Ground = 1 << World, Solid = 1 << World | 1 << Traffic;
    }
}
