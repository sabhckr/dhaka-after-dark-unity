// The job marker (Dad.Core.Beacon): a ring on the street and a column of light over it. Premultiplied blending, no depth written, both faces drawn, so the red stays red
// over a bright street rather than washing it pink. The vertex alpha says how bright each part is (the column fades going up); the vertex red says how much of the
// column's edge glow applies (the column is a tube of light, brightest where you see its edge, faint face-on; the ring has none). _Color is the light, and it breathes
// slowly so it reads as a marker, not a lamp. It fades with the scene's fog, but not all the way: a marker is meant to be seen across town.
Shader "Dad/Beacon"
{
    Properties
    {
        _Color("Light", Color) = (0.95, 0.18, 0.22, 1)
        _Cover("How much of what is behind it the light covers", Range(0, 1)) = 0.45
        _Breath("Breathing (0 = steady)", Range(0, 1)) = 0.25
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent-40" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Beacon"
            Tags { "LightMode" = "UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Cover, _Breath;
            CBUFFER_END

            struct Attributes { float3 positionOS : POSITION; float3 normalOS : NORMAL; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 tint : COLOR; };

            Varyings Vert(Attributes i)
            {
                Varyings o;
                float3 ws = TransformObjectToWorld(i.positionOS);
                o.positionCS = TransformWorldToHClip(ws);
                float3 n = TransformObjectToWorldNormal(i.normalOS);
                float3 v = normalize(GetWorldSpaceViewDir(ws));
                half side = 1 - abs(dot(n, v));
                half rim = lerp(1, 0.18 + 0.82 * side * side, i.color.r);
                half fog = saturate(ComputeFogIntensity(ComputeFogFactor(o.positionCS.z)) * 0.6 + 0.4);
                half breath = 1 - _Breath * (0.5 + 0.5 * sin(_Time.y * 3.0));
                half k = saturate(i.color.a * rim * breath * fog);
                o.tint = half4(_Color.rgb * k, k * _Cover);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target { return i.tint; }
            ENDHLSL
        }
    }
}
