// What a shot sounds like (2026-11-12, Docs/Characters/SHOOTER_BAR_AUDIT.md section 10). Real recordings: The Free Firearm Sound Library (CC0), an AR-15 for the scar, a Carl
// Gustav M45 for the ump and a Walther PPQ for the glock, each cut into single shots, close ("near": the crack) and from further off ("far": the crack and the street
// answering). A shot plays one of each, picked at random and a little off pitch, so no two are the same: your own gun loud and close, someone else's from where they stand,
// the crack fading within tens of metres and the far one carrying hundreds, arriving as late as sound travels. Impact plays what a shot strikes. A small pool of sources
// is shared by all, the oldest taken when every one is busy.
using System.Collections.Generic;
using UnityEngine;

namespace Dad.Core
{
    public static class GunSound
    {
        public const string Folder = "Audio/Guns/", Impacts = "Audio/Impacts/";
        /// <summary>Metres a second: a far shot is heard this much after it is fired.</summary>
        public const float SpeedOfSound = 343;
        /// <summary>Metres within which the crack is heard, and the far sound.</summary>
        public const float NearReach = 90, FarReach = 700;
        const int Voices = 32;

        static readonly Dictionary<string, AudioClip[]> clips = new Dictionary<string, AudioClip[]>();
        static AudioSource[] pool;
        static int next;

        /// <summary>The last sounds started, for tests: the clip, where, how loud, how long before it is heard, and whether it was your own gun.</summary>
        public struct Heard { public AudioClip Clip; public Vector3 At; public float Volume, Delay; public bool Yours; }
        public static readonly List<Heard> Log = new List<Heard>();
        const int LogSize = 64;

        /// <summary>The single takes of a set ("scar-near", "stone"), in their folder: name-1, name-2 and on.</summary>
        public static AudioClip[] Takes(string folder, string set)
        {
            if (clips.TryGetValue(folder + set, out var found)) return found;
            var list = new List<AudioClip>();
            for (int i = 1; ; i++)
            {
                var c = Resources.Load<AudioClip>(folder + set + "-" + i);
                if (c == null) break;
                list.Add(c);
            }
            if (list.Count == 0) Debug.LogWarning("[Dad] no sound " + folder + set);
            return clips[folder + set] = list.ToArray();
        }

        /// <summary>A shot fired at a place: yours (the camera's gun) or someone else's.</summary>
        public static void Shot(Armed.Kind gun, Vector3 at, bool yours)
        {
            if (gun == null) return;
            float world = GameSettings.Current != null ? GameSettings.Current.World : 1;
            float d = Distance(at);
            float pitch = Random.Range(0.96f, 1.04f);
            if (yours)
            {
                Play(Pick(gun.Name + "-near"), at, 0.85f * world, pitch, 0, 0.35f, 2, NearReach, true);
                Play(Pick(gun.Name + "-far"), at, 0.45f * world, pitch, 0, 0.6f, 4, FarReach, true);
                return;
            }
            if (d < NearReach) Play(Pick(gun.Name + "-near"), at, 0.8f * world, pitch, d / SpeedOfSound, 1, 3, NearReach, false);
            if (d < FarReach) Play(Pick(gun.Name + "-far"), at, 0.7f * world, pitch, d / SpeedOfSound, 1, 12, FarReach, false);
        }

        /// <summary>A shot striking stone, metal, wood, glass or a body, heard where it struck.</summary>
        public static void Impact(string surface, Vector3 at)
        {
            float world = GameSettings.Current != null ? GameSettings.Current.World : 1;
            float d = Distance(at);
            if (d > 60) return;
            Play(Pick(surface, Impacts), at, (surface == "body" ? 0.9f : 0.55f) * world, Random.Range(0.9f, 1.1f), d / SpeedOfSound, 1, 1.5f, 60, false);
        }

        static AudioClip Pick(string set, string folder = Folder)
        {
            var takes = Takes(folder, set);
            return takes.Length == 0 ? null : takes[Random.Range(0, takes.Length)];
        }

        static AudioListener ear;

        // The one listening, if anyone is.
        static AudioListener Ear()
        {
            if (ear == null || !ear.isActiveAndEnabled) ear = Object.FindAnyObjectByType<AudioListener>();
            return ear;
        }

        // How far the one listening is: the listener, else the main camera, else nobody (heard as if close).
        static float Distance(Vector3 at)
        {
            var e = Ear();
            var t = e != null ? e.transform : Camera.main != null ? Camera.main.transform : null;
            return t != null ? Vector3.Distance(t.position, at) : 0;
        }

        static void Play(AudioClip clip, Vector3 at, float volume, float pitch, float delay, float spatial, float near, float reach, bool yours)
        {
            if (clip == null) return;
            if (pool == null || pool[0] == null) Build();
            var s = pool[next];
            next = (next + 1) % pool.Length;
            s.Stop();
            s.transform.position = at;
            s.clip = clip;
            s.volume = volume;
            s.pitch = pitch;
            s.spatialBlend = spatial;
            s.minDistance = near;
            s.maxDistance = reach;
            if (Ear() != null) // with no one to hear it Unity complains every frame; it is still logged
            {
                if (delay > 0.005f) s.PlayDelayed(delay);
                else s.Play();
            }
            Log.Add(new Heard { Clip = clip, At = at, Volume = volume, Delay = delay, Yours = yours });
            if (Log.Count > LogSize) Log.RemoveAt(0);
        }

        static void Build()
        {
            var host = new GameObject("Gun sounds");
            Object.DontDestroyOnLoad(host);
            host.AddComponent<Hush>();
            pool = new AudioSource[Voices];
            for (int i = 0; i < Voices; i++)
            {
                var g = new GameObject("voice " + i);
                g.transform.SetParent(host.transform, false);
                var s = pool[i] = g.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.rolloffMode = AudioRolloffMode.Logarithmic;
                s.dopplerLevel = 0;
                s.spread = 30;
            }
        }

        /// <summary>Stops every shot and impact sounding or waiting to sound.</summary>
        public static void Silence()
        {
            if (pool == null) return;
            foreach (var s in pool) if (s != null) s.Stop();
        }

        // When the one listening goes (a scene or a test ends) the sounds still going stop with them.
        sealed class Hush : MonoBehaviour
        {
            void Update()
            {
                if (ear != null && ear.isActiveAndEnabled) return;
                if (Playing() > 0 && Ear() == null) Silence();
            }
        }

        /// <summary>The sources playing now, for tests.</summary>
        public static int Playing()
        {
            if (pool == null) return 0;
            int n = 0;
            foreach (var s in pool) if (s != null && s.isPlaying) n++;
            return n;
        }
    }
}
