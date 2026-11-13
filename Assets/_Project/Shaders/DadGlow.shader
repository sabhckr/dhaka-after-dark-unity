// Street lamps' pools and halos (D-13, Dad.World.StreetGlow): additive, no depth written, dark by day. _DadLights.y is how far the lamps are on (NightLife).
// Colour alpha marks a halo, which stands up to face the camera around its head. Both fade with the scene's fog, the halo less: a lamp is seen through haze.
Shader "Dad/Glow"
{
    Properties
    {
        _Pool("Pool brightness", Float) = 0.10
        _Halo("Halo brightness", Float) = 1.2
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent-50" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Glow"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Pool, _Halo;
            CBUFFER_END
            float4 _DadLights;  // NightLife: x windows, y lamps, z shops, w signs

            struct Attributes { float3 positionOS : POSITION; half4 color : COLOR; float2 corner : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 tint : COLOR; float2 corner : TEXCOORD0; half halo : TEXCOORD1; };

            Varyings Vert(Attributes i)
            {
                Varyings o = (Varyings)0;
                o.corner = i.corner;
                o.halo = i.color.a > 0.5 ? 1 : 0;
                if (_DadLights.y <= 0) { o.positionCS = float4(0, 0, -2, 1); return o; }
                float3 ws = TransformObjectToWorld(i.positionOS);
                float4 cs;
                half fog;
                if (o.halo > 0.5)
                {
                    float3 vs = TransformWorldToView(ws);
                    float d = length(vs);
                    // Keeps some size with distance, so a far lamp stays a point of light and not a dot.
                    float r = 0.8 * (1 + d * 0.012);
                    vs.xy += i.corner * r;
                    vs.z += 0.6; // towards the camera, clear of the lamp's own head
                    cs = TransformWViewToHClip(vs);
                    fog = saturate(ComputeFogIntensity(ComputeFogFactor(cs.z)) * 0.8 + 0.2);
                }
                else
                {
                    cs = TransformWorldToHClip(ws);
                    fog = ComputeFogIntensity(ComputeFogFactor(cs.z));
                }
                o.positionCS = cs;
                o.tint = half4(i.color.rgb * _DadLights.y * saturate(fog) * (o.halo > 0.5 ? _Halo : _Pool), 1);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float d2 = dot(i.corner, i.corner);
                half a;
                if (i.halo > 0.5)
                {
                    // A hot core in a wide soft glow.
                    half s = saturate(1 - d2);
                    a = s * s * 0.05 + pow(s, 24) * 1.0;
                }
                else
                {
                    half s = saturate(1 - d2);
                    a = s * s * s;
                }
                return half4(i.tint.rgb * a, 0);
            }
            ENDHLSL
        }
    }
}
