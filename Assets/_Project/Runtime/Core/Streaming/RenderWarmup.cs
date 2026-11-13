// Render warm-up (D-01): the streamer's made-up chunk is drawn once with the world material by two
// hidden cameras into small textures, and removed the next frame. Without it, the first real frame
// to draw each world-shader variant paid for it mid-game. On a cold Metal shader cache (the first
// run of a new build), the first frame with lit chunks took 300 ms while the driver compiled the
// shader. The first frame with a shadow caster in range took 6 ms of URP code compilation and
// 1.4 ms of pipeline creation (bench capture, D-01).
// The near camera is 8 m from the chunk's building, so it draws with main-light shadows. The far
// one is 560 m from it, past the 150 m shadow distance, so it draws the variant without them,
// which is what most frames over the city use (with the near camera only, a cold run still took a
// 102-108 ms frame at the first real chunk). Both copy the main camera's settings, so they run its
// passes (depth normals for SSAO, shadows, forward), into URP's 32-bit HDR colour format (the
// pipeline asset's HDR precision). They sit 5 km below the world, past every real camera's far clip.
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Dad.Core
{
    public sealed class RenderWarmup
    {
        public const float Depth = -5000f;

        readonly GameObject root;
        readonly Camera[] cameras;
        readonly RenderTexture[] targets;
        readonly int frame;
        bool done;

        /// <summary>Puts <paramref name="root"/> (a chunk root: a 500 m ground plate from its corner, a small
        /// building at the corner) under the world and points the cameras at it for this frame's rendering.</summary>
        public RenderWarmup(GameObject root)
        {
            this.root = root;
            root.transform.position = new Vector3(0f, Depth, 0f);
            cameras = new[]
            {
                Make("Render warm-up camera (shadows)", new Vector3(-6f, 4f, -6f), new Vector3(1f, 1f, 1f)),
                Make("Render warm-up camera (no shadows)", new Vector3(400f, 20f, 400f), new Vector3(300f, 0f, 300f)),
            };
            targets = new RenderTexture[cameras.Length];
            for (int i = 0; i < cameras.Length; i++)
            {
                targets[i] = new RenderTexture(64, 64, GraphicsFormat.B10G11R11_UFloatPack32, GraphicsFormat.D32_SFloat_S8_UInt) { name = cameras[i].name };
                cameras[i].targetTexture = targets[i];
            }
            frame = Time.frameCount;
        }

        static Camera Make(string name, Vector3 position, Vector3 lookAt)
        {
            var main = Camera.main;
            var camera = new GameObject(name).AddComponent<Camera>();
            if (main != null) camera.CopyFrom(main);
            camera.depth = (main != null ? main.depth : 0f) - 1f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 500f;
            camera.transform.position = position + new Vector3(0f, Depth, 0f);
            camera.transform.LookAt(lookAt + new Vector3(0f, Depth, 0f));
            return camera;
        }

        /// <summary>True, and the cameras removed and the chunk root hidden (the root is the caller's to
        /// destroy), once a frame has been drawn.</summary>
        public bool TryFinish()
        {
            if (Time.frameCount <= frame) return false;
            Finish();
            return true;
        }

        /// <summary>Removes the cameras and hides the chunk root now, drawn or not.</summary>
        public void Finish()
        {
            if (done) return;
            done = true;
            for (int i = 0; i < cameras.Length; i++)
            {
                cameras[i].targetTexture = null;
                Object.Destroy(cameras[i].gameObject);
                targets[i].Release();
                Object.Destroy(targets[i]);
            }
            if (root != null) root.SetActive(false);
        }
    }
}
