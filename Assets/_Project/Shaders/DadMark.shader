// What a shot leaves (Dad.Core.Impacts): bullet holes and blood on walls and the street, and the dust, chips, splinters, shards and blood thrown up. A picture times the vertex
// colour, lit by the sun and the sky as the street is (so a mark is as dark at night as the wall it is on), or with _Glow above zero lit by itself and added (sparks). It writes
// no depth and is drawn a little towards the camera, so a mark lies on its wall without flicker.
Shader "Dad/Mark"
{
    Properties
    {
        _MainTex("Picture", 2D) = "white" {}
        _Color("Tint", Color) = (1, 1, 1, 1)
        _Glow("Glow (0 = lit by the scene)", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent-10" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Mark"
            Tags { "LightMode" = "UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            Offset -1, -1
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                half _Glow;
            CBUFFER_END

            struct Attributes { float3 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; float3 normalWS : TEXCOORD1; float3 positionWS : TEXCOORD2; float fog : TEXCOORD3; };

            Varyings Vert(Attributes i)
            {
                Varyings o = (Varyings)0;
                o.positionWS = TransformObjectToWorld(i.positionOS);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.uv = TRANSFORM_TEX(i.uv, _MainTex);
                o.color = i.color;
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * i.color * _Color;
                if (_Glow > 0)
                    return half4(c.rgb * _Glow * c.a, 0); // added: a spark is its own light
                float3 n = normalize(i.normalWS);
                float3 toEye = GetWorldSpaceViewDir(i.positionWS);
                if (dot(n, toEye) < 0) n = -n; // a billboard or a mark seen from behind: the side towards the eye
                Light sun = GetMainLight();
                half3 light = sun.color * sun.distanceAttenuation * (0.25 + 0.75 * saturate(dot(n, sun.direction))) + SampleSH(n);
                half3 rgb = MixFog(c.rgb * light, i.fog);
                return half4(rgb * c.a, c.a);
            }
            ENDHLSL
        }
    }
}
