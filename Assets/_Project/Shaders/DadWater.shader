// The water's surface, drawn by WaterView over the flat ground layer. The owner's sea photograph, sampled twice at different sizes and drifting apart, is the ripple; its slopes turn the normal, which picks up the sky
// (the same reflection probe as everything else) and the sun. The owner's noise picture breaks up the colour over distance. Vertex red is the signed distance from the shore: along a natural shore the water thins out over the sand with a line of wash,
// and vertex green marks paved quays, where it simply stops. No extra pass, no depth or opaque texture and no displaced vertices, so it costs one blended plane with four samples.
Shader "Dad/Water"
{
    Properties
    {
        _Sea ("Sea photograph", 2D) = "gray" {}
        _Noise ("Noise", 2D) = "gray" {}
        _Deep ("Deep colour", Color) = (0.055, 0.135, 0.15, 1)
        _Shallow ("Shallow colour", Color) = (0.2, 0.3, 0.27, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent-10" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_Sea);
            SAMPLER(sampler_Sea);
            TEXTURE2D(_Noise);
            SAMPLER(sampler_Noise);
            // Set by the shot tool (ViewShot) so golden pictures of water repeat: 1 holds the ripples at _DadWaterClock.
            float _DadWaterFrozen;
            float _DadWaterClock;

            CBUFFER_START(UnityPerMaterial)
                half4 _Deep;
                half4 _Shallow;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0; // World metres.
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half2 shore : TEXCOORD1;
                float3 map : TEXCOORD2; // metres, fog
            };

            Varyings Vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.shore = half2(v.color.r * 63.75 - 32.0, v.color.g);
                o.map = float3(v.uv, ComputeFogFactor(o.positionCS.z));
                return o;
            }

            half Ripple(float2 m, float t)
            {
                // The photograph is four times wider than high.
                half a = SAMPLE_TEXTURE2D(_Sea, sampler_Sea, float2(m.x / 96.0 + t * 0.0021, m.y / 24.0 + t * 0.0034)).g;
                half b = SAMPLE_TEXTURE2D(_Sea, sampler_Sea, float2(m.y / 58.0 - t * 0.0029, m.x / 14.5 + t * 0.0043)).g;
                return a + b;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float2 m = i.map.xy;
                float t = _DadWaterFrozen > 0.5 ? _DadWaterClock : _Time.y;
                half far = saturate(length(i.positionWS - GetCameraPositionWS()) / 400.0);
                const float e = 0.45;
                half h = Ripple(m, t);
                half2 slope = half2(Ripple(m + float2(e, 0), t) - h, Ripple(m + float2(0, e), t) - h) * lerp(1.1, 0.35, far);
                half3 n = normalize(half3(-slope.x, 1, -slope.y));

                half patch = SAMPLE_TEXTURE2D(_Noise, sampler_Noise, m / 310.0 + t * 0.0004).g;
                half d = i.shore.x + (patch - 0.5) * 3.0 + sin(t * 0.6 + m.x * 0.05 + m.y * 0.04) * 0.9;
                half3 view = GetWorldSpaceNormalizeViewDir(i.positionWS);
                half fresnel = 0.03 + 0.97 * pow(1.0 - saturate(dot(n, view)), 5.0);
                half depth = saturate((i.shore.x + 2.0) / 22.0);
                half3 body = lerp(_Shallow.rgb, _Deep.rgb, depth) * (0.75 + 0.5 * patch);

                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                Light sun = GetMainLight(shadowCoord);
                half lit = sun.shadowAttenuation;
                half3 ambient = SampleSH(half3(0, 1, 0));
                half3 colour = body * (ambient + sun.color * (0.35 * saturate(sun.direction.y) * lit));
                half3 sky = GlossyEnvironmentReflection(reflect(-view, n), i.positionWS, 0.25, 1.0, GetNormalizedScreenSpaceUV(i.positionCS));
                colour = lerp(colour, sky, fresnel * 0.75);
                half3 hv = normalize(sun.direction + view);
                colour += sun.color * (pow(saturate(dot(n, hv)), 320.0) * 1.6 * lit);

                // Wash on a natural shore: a soft pale line that comes and goes. Quays have none.
                half natural = 1.0 - i.shore.y;
                half wash = natural * smoothstep(-5.0, -2.0, d) * (1.0 - smoothstep(-0.5, 2.5, d)) * (0.45 + 0.55 * h);
                colour = lerp(colour, ambient + sun.color * 0.3 * lit, wash * 0.55);

                half alpha = lerp(smoothstep(-7.0, -0.5, d) * 0.97, 1.0, step(0.0, i.shore.x));
                alpha = max(alpha, i.shore.y * step(-3.0, i.shore.x));
                return half4(MixFog(colour, i.map.z), alpha);
            }
            ENDHLSL
        }
    }
}
