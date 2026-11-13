// Keeps the tracked object near the scene's (0, 0) so float positions stay precise.
// When the target passes Threshold metres from the origin on either axis, the origin moves to
// the corner of the cell under it and every root object in every loaded scene moves back by
// the same whole number of 500 m. Objects in DontDestroyOnLoad are not moved yet.
// Only one may be active: two would undo each other's shifts every frame, so a second one
// logs an error and disables itself.
// It also hands the world shader its offset, wrapped to 4,000 m (_DadWorldWrap), so textures in
// world metres stay put across shifts (D-01; WorldMaterials.Wrap).
using System;
using Dad.World;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Dad.Core
{
    [DefaultExecutionOrder(1000)]
    public sealed class FloatingOrigin : MonoBehaviour
    {
        public Transform Target;
        public float Threshold = 2000f;

        public OriginOffset Offset { get; } = new OriginOffset();
        public int Shifts { get; private set; }

        /// <summary>Raised after a shift with the shift in metres (east, north).</summary>
        public event Action<double, double> Shifted;

        public static FloatingOrigin Active { get; private set; }

        static readonly int WrapId = Shader.PropertyToID("_DadWorldWrap");

        void OnEnable()
        {
            if (Active != null && Active != this)
            {
                Debug.LogError($"[Dad] second FloatingOrigin on {name} disabled; {Active.name} is active");
                enabled = false;
                return;
            }
            Active = this;
            PublishWrap();
        }

        void OnDisable()
        {
            if (Active == this) Active = null;
        }

        void LateUpdate()
        {
            if (Target == null) return;
            var p = Target.position;
            if (Offset.TryShift(p.x, p.z, Threshold, out double east, out double north)) MoveScenes(east, north);
        }

        /// <summary>
        /// Puts the origin on the cell corner under a world position and moves the scene to match,
        /// e.g. at start or on a teleport. Counts as a shift.
        /// </summary>
        public void Recenter(double east, double north)
        {
            double e0 = Offset.East, n0 = Offset.North;
            Offset.Reset(east, north);
            if (Offset.East != e0 || Offset.North != n0) MoveScenes(Offset.East - e0, Offset.North - n0);
        }

        void MoveScenes(double east, double north)
        {
            using (DadMarkers.FloatingOriginShift.Auto())
            {
                var delta = new Vector3((float)east, 0f, (float)north);
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    var scene = SceneManager.GetSceneAt(i);
                    if (!scene.isLoaded) continue;
                    foreach (var root in scene.GetRootGameObjects()) root.transform.position -= delta;
                }
                Physics.SyncTransforms();
            }
            Shifts++;
            PublishWrap();
            Shifted?.Invoke(east, north);
        }

        void PublishWrap() =>
            Shader.SetGlobalVector(WrapId, new Vector4((float)WorldMaterials.Wrap(Offset.East), 0f, (float)WorldMaterials.Wrap(Offset.North), 0f));
    }
}
