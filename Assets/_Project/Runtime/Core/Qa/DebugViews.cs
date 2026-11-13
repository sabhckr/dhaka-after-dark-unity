// Debug-view toggles (C-05), read from the Global map while playing: F1 the free camera, F2 the
// chunk grid and ids, F3 the road graph, F4 the building outlines and road centrelines, F5 the
// world shading (lit, normals, material ids, metre grid, albedo, facade families; WorldShading, D-01), F6 the clock
// one hour on and F7 the next weather (DayCycle, D-03). The
// overlay switches live on ChunkStreamer, which applies them to resident chunks and to chunks that
// stream in later. All are off at start. Polled in Update, so no callbacks are added to the actions.
using UnityEngine;

namespace Dad.Core
{
    public sealed class DebugViews : MonoBehaviour
    {
        public GameInput Input;
        public ChunkStreamer Streamer;
        public DayCycle Day;

        /// <summary>The context F1 returns to when leaving the free camera.</summary>
        public InputContext Underneath = InputContext.OnFoot;

        void Update()
        {
            if (Input == null || !Input.IsGameplay) return;
            if (Input.ToggleFreeCamera.WasPressedThisFrame())
            {
                if (Input.Context == InputContext.FreeCamera) Input.Switch(Underneath);
                else
                {
                    Underneath = Input.Context;
                    Input.Switch(InputContext.FreeCamera);
                }
            }
            if (Input.CycleShading.WasPressedThisFrame()) WorldShading.Next();
            if (Day != null && Input.AdvanceHour.WasPressedThisFrame()) Day.Set(Day.Date, Day.Hours + 1, Day.Weather);
            if (Day != null && Input.CycleWeather.WasPressedThisFrame()) Day.Set(Day.Date, Day.Hours, (Weather)(((int)Day.Weather + 1) % 3));
            if (Streamer == null) return;
            bool grid = Streamer.ShowGrid, graph = Streamer.ShowGraph, lines = Streamer.ShowLines;
            if (Input.ToggleGrid.WasPressedThisFrame()) grid = !grid;
            if (Input.ToggleGraph.WasPressedThisFrame()) graph = !graph;
            if (Input.ToggleLines.WasPressedThisFrame()) lines = !lines;
            if (grid != Streamer.ShowGrid || graph != Streamer.ShowGraph || lines != Streamer.ShowLines) Streamer.SetOverlays(grid, graph, lines);
        }
    }
}
