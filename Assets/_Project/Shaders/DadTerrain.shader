// Open ground (owner's sand, rocky soil and grass, brief addition of 2026-11-06). One flat mesh per chunk at 10 m spacing, drawn by CityView: its vertex colour is how much sand, rock and grass stand at
// each point, and alpha whether this ground shows at all. The lattice is shared across chunk borders, so edges meet. Weights are sharpened by each picture's own brightness, so one ground eats into the
// next along its grains and stones and not along a straight line. A second, much larger sample breaks up the tiling seen from the air.
Shader "Dad/Terrain"
{
    Properties
    {
        _Sand ("Sand", 2D) = "white" {}
        _Rock ("Rocky soil", 2D) = "white" {}
        _Grass ("Grass", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        struct Attributes
        {
            float4 positionOS : POSITION;
            half4 color : COLOR;
            float2 uv : TEXCOORD0; // World metres.
        };
        TEXTURE2D(_Grass);
        SAMPLER(sampler_Grass);
        half Keep(float2 m, half a)
        {
            half n = SAMPLE_TEXTURE2D(_Grass, sampler_Grass, m * 0.043).g;
            return a + (n - 0.3) * 0.3 - 0.5;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half4 color : COLOR;
                float3 map : TEXCOORD1; // metres, fog
            };

            TEXTURE2D(_Sand);
            SAMPLER(sampler_Sand);
            TEXTURE2D(_Rock);
            SAMPLER(sampler_Rock);

            Varyings Vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.color = v.color;
                o.map = float3(v.uv, ComputeFogFactor(o.positionCS.z));
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float2 m = i.map.xy;
                clip(Keep(m, i.color.a));
                half3 sand = SAMPLE_TEXTURE2D(_Sand, sampler_Sand, m / 7.0).rgb;
                // Each ground is two samples, turned and scaled apart, so no tile edge lines up from the air.
                float2 turned = float2(m.x * 0.83 - m.y * 0.56, m.x * 0.56 + m.y * 0.83);
                half3 rock = (SAMPLE_TEXTURE2D(_Rock, sampler_Rock, m / 5.5).rgb + SAMPLE_TEXTURE2D(_Rock, sampler_Rock, turned / 8.3).rgb) * 0.5;
                half3 grass = (SAMPLE_TEXTURE2D(_Grass, sampler_Grass, m / 4.5).rgb + SAMPLE_TEXTURE2D(_Grass, sampler_Grass, turned / 11.7).rgb) * 0.5;
                half3 w = i.color.rgb * half3(0.5 + dot(sand, 0.33), 0.5 + dot(rock, 0.33), 0.5 + dot(grass, 0.5));
                w = w * w * w;
                w /= max(dot(w, 1), 1e-4);
                half far = SAMPLE_TEXTURE2D(_Rock, sampler_Rock, m / 61.0).g;
                half3 albedo = (sand * w.x + rock * w.y + grass * w.z) * (0.72 + 0.62 * far);

                float3 n = float3(0, 1, 0);
                InputData d = (InputData)0;
                d.positionWS = i.positionWS;
                d.positionCS = i.positionCS;
                d.normalWS = n;
                d.viewDirectionWS = GetWorldSpaceNormalizeViewDir(i.positionWS);
                d.shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                d.fogCoord = i.map.z;
                d.bakedGI = SampleSH(n);
                d.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                d.shadowMask = half4(1, 1, 1, 1);

                SurfaceData s = (SurfaceData)0;
                s.albedo = albedo;
                s.smoothness = 0.08;
                s.occlusion = 1;
                s.alpha = 1;
                s.normalTS = half3(0, 0, 1);

                half4 c = UniversalFragmentPBR(d, s);
                return half4(MixFog(c.rgb, d.fogCoord), 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            struct Varyings { float4 positionCS : SV_POSITION; half a : TEXCOORD0; float2 m : TEXCOORD1; };
            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformWorldToHClip(TransformObjectToWorld(v.positionOS.xyz));
                o.a = v.color.a;
                o.m = v.uv;
                return o;
            }
            half Frag(Varyings i) : SV_Target { clip(Keep(i.m, i.a)); return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            struct Varyings { float4 positionCS : SV_POSITION; half a : TEXCOORD0; float2 m : TEXCOORD1; };
            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformWorldToHClip(TransformObjectToWorld(v.positionOS.xyz));
                o.a = v.color.a;
                o.m = v.uv;
                return o;
            }
            half4 Frag(Varyings i) : SV_Target { clip(Keep(i.m, i.a)); return half4(0, 1, 0, 0); }
            ENDHLSL
        }
    }
}
