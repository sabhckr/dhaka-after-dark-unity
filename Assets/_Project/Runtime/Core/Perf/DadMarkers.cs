// Profiler markers for the game's own work. Every name starts with "Dad." so a capture or a
// ProfilerRecorder can pick them out. Add a marker here when a module gets a budget.
using Unity.Profiling;

namespace Dad.Core
{
    public static class DadMarkers
    {
        public static readonly ProfilerMarker FloatingOriginShift = new ProfilerMarker("Dad.FloatingOrigin.Shift");
        public static readonly ProfilerMarker InputContext = new ProfilerMarker("Dad.Input.Context");
        public static readonly ProfilerMarker CameraPathApply = new ProfilerMarker("Dad.CameraPath.Apply");
        public static readonly ProfilerMarker BenchSample = new ProfilerMarker("Dad.Bench.Sample");
        public static readonly ProfilerMarker StreamingPlan = new ProfilerMarker("Dad.Streaming.Plan");
        public static readonly ProfilerMarker StreamingApply = new ProfilerMarker("Dad.Streaming.Apply");
        public static readonly ProfilerMarker StreamingUnload = new ProfilerMarker("Dad.Streaming.Unload");
        /// <summary>Dropping the CPU copies of streamed meshes once they have been drawn (K-45).</summary>
        public static readonly ProfilerMarker StreamingRelease = new ProfilerMarker("Dad.Streaming.Release");
        public static readonly ProfilerMarker StreamingKit = new ProfilerMarker("Dad.Streaming.Kit");
        public static readonly ProfilerMarker StreamingColliders = new ProfilerMarker("Dad.Streaming.Colliders");
        /// <summary>Inside Plan: taking finished loads off the workers, and starting new loads.</summary>
        public static readonly ProfilerMarker StreamingCollect = new ProfilerMarker("Dad.Streaming.Collect");
        public static readonly ProfilerMarker StreamingStart = new ProfilerMarker("Dad.Streaming.Start");
        /// <summary>DayCycle placing the sun and moon and setting the light, every frame.</summary>
        public static readonly ProfilerMarker SkyApply = new ProfilerMarker("Dad.Sky.Apply");
        /// <summary>Inside Apply: refilling the sky reflection cubemap, when the sky has changed enough to see.</summary>
        public static readonly ProfilerMarker SkyReflection = new ProfilerMarker("Dad.Sky.Reflection");

        /// <summary>Stepping the traffic, handing it to the renderer, and taking lanes in and out as chunks stream.</summary>
        public static readonly ProfilerMarker TrafficStep = new ProfilerMarker("Dad.Traffic.Step");
        public static readonly ProfilerMarker VehicleStep = new ProfilerMarker("Dad.Vehicle.Step");
        public static readonly ProfilerMarker TrafficDraw = new ProfilerMarker("Dad.Traffic.Draw");
        public static readonly ProfilerMarker TrafficStream = new ProfilerMarker("Dad.Traffic.Stream");

        public static readonly string[] All =
        {
            "Dad.FloatingOrigin.Shift", "Dad.Input.Context", "Dad.CameraPath.Apply", "Dad.Bench.Sample",
            "Dad.Streaming.Plan", "Dad.Streaming.Apply", "Dad.Streaming.Unload", "Dad.Streaming.Collect", "Dad.Streaming.Start", "Dad.Streaming.Release", "Dad.Streaming.Kit",
            "Dad.Sky.Apply", "Dad.Sky.Reflection", "Dad.Traffic.Step", "Dad.Traffic.Draw", "Dad.Traffic.Stream",
        };
    }
}
