// A muzzle flash (Dad.Core.Armed): a star of light that faces the camera, at the object's origin, added to what is behind it and writing no depth. _Bright is 0 between
// shots; _Spin turns the star so no two flashes look the same; _Size is its radius in metres.
Shader "Dad/Flash"
{
    Properties
    {
        _Bright("Brightness", Float) = 0
        _Size("Radius (m)", Float) = 0.15
        _Spin("Turn (radians)", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Flash"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Bright, _Size, _Spin;
            CBUFFER_END

            struct Attributes { float3 positionOS : POSITION; float2 corner : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 corner : TEXCOORD0; };

            Varyings Vert(Attributes i)
            {
                Varyings o = (Varyings)0;
                float3 vs = TransformWorldToView(TransformObjectToWorld(float3(0, 0, 0)));
                vs.xy += i.corner * _Size;
                vs.z += 0.04; // a little towards the camera, clear of the muzzle
                o.positionCS = TransformWViewToHClip(vs);
                float s, c;
                sincos(_Spin, s, c);
                o.corner = float2(c * i.corner.x - s * i.corner.y, s * i.corner.x + c * i.corner.y);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float r = length(i.corner);
                float2 a = abs(i.corner);
                half core = saturate(1 - r * 2.4);
                half glow = pow(saturate(1 - r), 3);
                half rays = saturate(1 - r) * (pow(saturate(1 - a.x * 7), 3) + pow(saturate(1 - a.y * 7), 3));
                half3 colour = half3(1, 0.9, 0.7) * (core * core * 4 + rays * 1.5) + half3(1, 0.5, 0.15) * glow * 0.8;
                return half4(colour * _Bright, 0);
            }
            ENDHLSL
        }
    }
}
