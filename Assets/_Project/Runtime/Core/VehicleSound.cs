// A vehicle's sound (E-04), synthesised, with no recordings: an engine from its firing rate, harmonics and a little noise, louder and rougher under load; tyres from how far past
// their grip they are; a two-tone horn. The audio thread reads a few numbers the main thread sets from VehicleState, and nothing else.
using UnityEngine;

namespace Dad.Core
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class VehicleSound : MonoBehaviour
    {
        public PlayerVehicle Vehicle;
        /// <summary>Cylinders firing per two turns of the crank.</summary>
        public int Cylinders = 4;

        volatile float rpm, load, skid;
        volatile bool horn, on;
        double phase, hornA, hornB;
        float rough, tyre, gain;
        uint noise = 22222;
        int rate;

        void Awake()
        {
            rate = AudioSettings.outputSampleRate;
            var a = source = GetComponent<AudioSource>();
            a.spatialBlend = 0.6f;
            a.loop = true;
            a.playOnAwake = true;
            a.Play();
        }

        AudioSource source;

        void Update()
        {
            source.volume = GameSettings.Current.Vehicles; // the player's setting (Settings > Audio)
            if (Vehicle == null) return;
            var s = Vehicle.State;
            rpm = (float)s.Rpm;
            load = (float)s.Throttle;
            on = s.EngineOn;
            horn = Vehicle.HornOn;
            double most = 0;
            foreach (var w in s.Wheels) most = System.Math.Max(most, w.Grounded ? w.Skid : 0);
            skid = (float)most;
        }

        float Noise()
        {
            noise = noise * 1664525u + 1013904223u;
            return (noise >> 8) / 8388608f - 1f;
        }

        void OnAudioFilterRead(float[] data, int channels)
        {
            float f = rpm / 60f * Cylinders / 2f, step = f / rate;
            float l = load, wantGain = on ? 0.16f + 0.3f * l : 0f, wantTyre = Mathf.Clamp01(skid) * 0.28f;
            bool h = horn;
            for (int i = 0; i < data.Length; i += channels)
            {
                phase += step;
                if (phase > 1) phase -= 1;
                gain += (wantGain - gain) * 0.002f;
                tyre += (wantTyre - tyre) * 0.002f;
                float a = (float)phase * 2 * Mathf.PI;
                rough += (Noise() - rough) * 0.08f;
                float engine = 0.55f * Mathf.Sin(a) + 0.3f * Mathf.Sin(2 * a + 0.6f) + (0.1f + 0.2f * l) * Mathf.Sin(3 * a + 1.1f) + (0.04f + 0.14f * l) * Mathf.Sin(5 * a) + rough * (0.15f + 0.45f * l);
                float sample = engine * gain + Noise() * tyre * 0.6f;
                if (h)
                {
                    hornA += 415.0 / rate;
                    hornB += 498.0 / rate;
                    sample += 0.12f * (Mathf.Sign(Mathf.Sin((float)(hornA % 1) * 2 * Mathf.PI)) + Mathf.Sign(Mathf.Sin((float)(hornB % 1) * 2 * Mathf.PI)));
                }
                for (int c = 0; c < channels; c++) data[i + c] += sample;
            }
        }
    }
}
