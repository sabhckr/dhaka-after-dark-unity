// A paint job as the vehicle shader reads it, and a list of vehicles to draw instanced. TrafficView and ParkedView share these, so a car has the same paint moving and parked, and a
// colour costs no material of its own: it goes in with the instance.
using Dad.Traffic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Dad.Core
{
    public struct VehicleLook
    {
        /// <summary>rgb linear paint, a road dust; smoothness, metallic, flake, pearl; rgb linear second tone, a where it starts (metres up the body, 0 for none).</summary>
        public Vector4 Tint, Finish, Second;

        public static VehicleLook Of(in PaintJob job, double height)
        {
            var p = VehiclePaint.All[job.Base];
            var c = ((Color)new Color32(p.R, p.G, p.B, 255)).linear;
            float k = (float)job.Shade;
            var look = new VehicleLook
            {
                Tint = new Vector4(c.r * k, c.g * k, c.b * k, (float)job.Dust),
                Finish = new Vector4((float)p.Smoothness, (float)p.Metallic, (float)p.Flake, (float)p.Pearl),
            };
            if (job.Second >= 0)
            {
                var q = VehiclePaint.All[job.Second];
                var s = ((Color)new Color32(q.R, q.G, q.B, 255)).linear;
                look.Second = new Vector4(s.r, s.g, s.b, (float)(job.Split * height));
            }
            return look;
        }
    }

    public sealed class VehicleBatch
    {
        static readonly int TintId = Shader.PropertyToID("_Tint"), LampsId = Shader.PropertyToID("_Lamps"), FinishId = Shader.PropertyToID("_Finish"), SecondId = Shader.PropertyToID("_Second");

        public readonly Matrix4x4[] M;
        public readonly Vector4[] Tint, Finish, Second, Lamps;
        public readonly MaterialPropertyBlock Props = new MaterialPropertyBlock();
        public int Count;

        public VehicleBatch(int size) { M = new Matrix4x4[size]; Tint = new Vector4[size]; Finish = new Vector4[size]; Second = new Vector4[size]; Lamps = new Vector4[size]; }
        public bool Full => Count == M.Length;

        public void Add(in Matrix4x4 m, in VehicleLook look, Vector4 lamps)
        {
            if (Full) return;
            M[Count] = m;
            Tint[Count] = look.Tint;
            Finish[Count] = look.Finish;
            Second[Count] = look.Second;
            Lamps[Count++] = lamps;
        }

        public void Flush(Mesh mesh, Material mat, ShadowCastingMode shadows, in Bounds bounds, bool clear)
        {
            if (Count == 0) return;
            Props.SetVectorArray(TintId, Tint);
            Props.SetVectorArray(FinishId, Finish);
            Props.SetVectorArray(SecondId, Second);
            Props.SetVectorArray(LampsId, Lamps);
            var rp = new RenderParams(mat)
            {
                worldBounds = bounds,
                shadowCastingMode = shadows,
                receiveShadows = true,
                matProps = Props,
                lightProbeUsage = LightProbeUsage.BlendProbes,
            };
            Graphics.RenderMeshInstanced(rp, mesh, 0, M, Count);
            if (clear) Count = 0;
        }
    }
}
