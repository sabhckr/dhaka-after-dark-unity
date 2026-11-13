// Moves this object (normally the main camera) along a CameraPath in real time.
// Runs in Update, before FloatingOrigin's LateUpdate, so a shift sees the new position.
// Without a FloatingOrigin it keeps its own offset, set at the start of the path.
using Dad.World;
using UnityEngine;

namespace Dad.Core
{
    public sealed class CameraPathPlayer : MonoBehaviour
    {
        public CameraPath Path;
        public FloatingOrigin Origin;
        public bool Playing;

        public float Time { get; private set; }
        public bool Finished => Path != null && Time >= Path.duration;

        readonly OriginOffset ownOffset = new OriginOffset();

        OriginOffset Offset => Origin != null ? Origin.Offset : ownOffset;

        /// <summary>Puts the camera at the start of <paramref name="path"/>; it moves once Playing is set.</summary>
        public void Load(CameraPath path)
        {
            Path = path;
            var start = path.Evaluate(0f);
            ownOffset.Reset(start.east, start.north);
            Seek(0f);
        }

        public void Seek(float time)
        {
            Time = Mathf.Clamp(time, 0f, Path.duration);
            using (DadMarkers.CameraPathApply.Auto())
            {
                var p = Path.Evaluate(Time);
                var offset = Offset;
                offset.ToScene(p.east, p.north, out float x, out float z);
                offset.ToScene(p.lookEast, p.lookNorth, out float lx, out float lz);
                transform.position = new Vector3(x, p.height, z);
                transform.LookAt(new Vector3(lx, p.lookHeight, lz));
            }
        }

        void Update()
        {
            if (Playing && Path != null && !Finished) Seek(Time + UnityEngine.Time.unscaledDeltaTime);
        }
    }
}
