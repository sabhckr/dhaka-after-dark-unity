// The train's sound, synthesised like the cars': a low rumble that rises with speed and the clack of rail joints, from where the train is. A source on the car nearest the listener.
using UnityEngine;

namespace Dad.Core
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class TrainSound : MonoBehaviour
    {
        const float Joint = 8.86f;  // A length of the owner's rail.

        public TrainView Train;
        volatile float speed;
        double beat;
        float low, hit, gain;
        uint noise = 91;
        int rate;

        void Awake()
        {
            rate = AudioSettings.outputSampleRate;
            var a = source = GetComponent<AudioSource>();
            a.spatialBlend = 1;
            a.minDistance = 12;
            a.maxDistance = 240;
            a.rolloffMode = AudioRolloffMode.Linear;
            a.loop = true;
            a.playOnAwake = true;
            a.Play();
        }

        AudioSource source;

        void Update()
        {
            source.volume = GameSettings.Current.World; // the player's setting (Settings > Audio)
            if (Train == null || Train.Run == null) return;
            speed = (float)Train.Run.V;
            var cam = Camera.main;
            if (cam == null) return;
            int best = 0;
            for (int k = 1; k < TrainView.Cars; k++)
                if ((Train.At[k] - cam.transform.position).sqrMagnitude < (Train.At[best] - cam.transform.position).sqrMagnitude) best = k;
            transform.position = Train.At[best] + Vector3.up;
        }

        float Noise()
        {
            noise = noise * 1664525u + 1013904223u;
            return (noise >> 8) / 8388608f - 1f;
        }

        void OnAudioFilterRead(float[] data, int channels)
        {
            float v = speed, want = Mathf.Clamp01(v / 12f) * 0.5f, step = v / Joint / rate;
            for (int i = 0; i < data.Length; i += channels)
            {
                gain += (want - gain) * 0.001f;
                beat += step;
                if (beat > 1) { beat -= 1; hit = 1; }
                hit *= 0.9994f;
                low += (Noise() - low) * 0.02f;
                float sample = gain * (low * 2.4f + hit * hit * Noise() * 0.5f);
                for (int c = 0; c < channels; c++) data[i + c] += sample;
            }
        }
    }
}
