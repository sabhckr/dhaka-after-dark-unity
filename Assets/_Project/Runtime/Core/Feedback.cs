// Small things the game tells you (2026-11-10): a short notice for the HUD, and the gamepad shaking.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Dad.Core
{
    /// <summary>Short messages for the HUD's notice feed (waypoint set, settings saved). Kept until the HUD takes them.</summary>
    public static class Notice
    {
        public static readonly List<string> Waiting = new List<string>();
        /// <summary>The last one posted, for tests.</summary>
        public static string Last { get; private set; }

        public static void Post(string text)
        {
            Last = text;
            if (Waiting.Count < 8) Waiting.Add(text);
        }
    }

    /// <summary>The gamepad's motors: a pulse of low and high, for a time. Nothing without a gamepad, or with vibration off.</summary>
    public static class Rumble
    {
        static float until;
        static bool on;

        public static void Pulse(float low, float high, float seconds)
        {
            var pad = Gamepad.current;
            if (pad == null || !GameSettings.Current.Vibration) return;
            pad.SetMotorSpeeds(low, high);
            until = Time.unscaledTime + seconds;
            on = true;
        }

        /// <summary>Called every frame by the HUD: stops a pulse whose time is up.</summary>
        public static void Tick()
        {
            if (!on || Time.unscaledTime < until) return;
            on = false;
            Gamepad.current?.SetMotorSpeeds(0, 0);
        }
    }
}
