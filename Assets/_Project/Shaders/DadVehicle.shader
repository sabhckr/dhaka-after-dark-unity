// Vehicles (E-05): the opaque parts of the kit, drawn instanced by TrafficView (Graphics.RenderMeshInstanced), lit by the same URP PBR
// path as Dad/World, so sun, sky, fog and the night reach them as they reach the city. Each surface takes its finish from its
// material number: paint is the vehicle's own colour, with road dust gathering low on the body; glass is Dad/VehicleGlass.
// Lamps light by what TrafficView says each vehicle is doing. Sheets are single sided, so nothing is culled and back faces turn their normals.
Shader "Dad/Vehicle"
{
    Properties { _BaseMap ("Picture", 2D) = "white" {} }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "DadVehicleCommon.hlsl"
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #define _CLEARCOAT 1
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                half4 color : COLOR;
                float3 extra : TEXCOORD2; // height above the road, lamp, fog
                float2 map : TEXCOORD3;
                float3 local : TEXCOORD4; // the body's own metres, so wear and flake stay on it as it moves
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            // (smoothness, metallic) by material.
            static const half2 Finish[8] =
            {
                half2(0.66, 0.0),  // Paint
                half2(0.34, 0.0),  // Plastic
                half2(0.85, 0.0),  // Glass
                half2(0.58, 0.8),  // Metal
                half2(0.78, 0.0),  // Lens
                half2(0.14, 0.0),  // Rough
                half2(0.24, 0.0),  // Rubber
                half2(0.44, 0.0),  // Trim
            };

            Varyings Vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.color = half4(SRGBToLinear(v.color.rgb), v.color.a);
                o.extra = float3(v.positionOS.y, v.uv.x, ComputeFogFactor(o.positionCS.z));
                o.map = v.map;
                o.local = v.positionOS.xyz;
                return o;
            }

            half4 Frag(Varyings i, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float4 tint = UNITY_ACCESS_INSTANCED_PROP(DadVehicle, _Tint);
                float4 lamps = UNITY_ACCESS_INSTANCED_PROP(DadVehicle, _Lamps);
                float4 fin = UNITY_ACCESS_INSTANCED_PROP(DadVehicle, _Finish);
                float4 second = UNITY_ACCESS_INSTANCED_PROP(DadVehicle, _Second);
                uint id = KitMaterial(i.color.a);
                half3 albedo = i.color.rgb * SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.map).rgb;
                half2 finish = Finish[id];
                float3 n = normalize(i.normalWS) * IS_FRONT_VFACE(face, 1, -1);
                float3 view = GetWorldSpaceNormalizeViewDir(i.positionWS);
                half coat = 0;
                // Slow blotches, the same on a vehicle wherever it goes: dust gathers and paint dulls unevenly.
                half blotch = 0.5 + 0.5 * sin(i.local.x * 3.1 + sin(i.local.z * 1.7) * 2.0) * sin(i.local.y * 5.3 + i.local.z * 2.3);
                if (id == KIT_PAINT)
                {
                    half3 paint = i.extra.x < second.a ? second.rgb : tint.rgb;
                    if (fin.x > 0)
                    {
                        finish = fin.xy;
                        coat = 0.45 * saturate(fin.x * 1.4);
                        half facing = saturate(dot(n, view));
                        // Pearl: warmer and paler at a glancing angle.
                        paint *= lerp(half3(1, 1, 1), lerp(half3(1.22, 1.14, 1.02), half3(1, 1, 1), facing), fin.w);
                        // Flake: fine grains of the base catch the light, seen only up close.
                        float3 cell = floor(i.local * 420.0);
                        half grain = frac(sin(dot(cell, float3(12.9898, 78.233, 37.719))) * 43758.5453);
                        half near = saturate(1.3 - length(i.positionWS - _WorldSpaceCameraPos) / 14.0);
                        paint *= 1 + fin.z * near * (grain - 0.5) * 0.5;
                    }
                    albedo *= paint;
                }
                if (id != KIT_LENS && id != KIT_RUBBER)
                {
                    half dust = tint.a * saturate(1.15 - i.extra.x * 0.8) * (0.55 + 0.9 * blotch);
                    albedo = lerp(albedo, half3(0.2, 0.17, 0.13), saturate(dust) * 0.45);
                    finish.x *= 1 - 0.6 * saturate(dust) - 0.12 * tint.a * blotch;
                    coat *= 1 - saturate(dust);
                }

                half glow = 0;
                uint lamp = (uint)(i.extra.y + 0.5);
                if (lamp == 1) glow = lamps.x * 16;
                else if (lamp == 2) glow = lamps.x * 3;
                else if (lamp == 3) glow = max(lamps.x * 2, lamps.y * 9);
                else if (lamp == 4) glow = lamps.z * 7;
                else if (lamp == 5) glow = lamps.w * 7;
                else if (lamp == 7) glow = lamps.x * 3;
                else if (lamp == 8) glow = lamps.x * 1.5;

                InputData d = (InputData)0;
                d.positionWS = i.positionWS;
                d.positionCS = i.positionCS;
                d.normalWS = n;
                d.viewDirectionWS = view;
                d.shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                d.fogCoord = i.extra.z;
                d.bakedGI = SampleSH(n);
                d.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                d.shadowMask = half4(1, 1, 1, 1);

                SurfaceData s = (SurfaceData)0;
                s.albedo = albedo;
                s.smoothness = finish.x;
                s.metallic = finish.y;
                s.occlusion = 1;
                s.alpha = 1;
                s.normalTS = half3(0, 0, 1);
                s.emission = albedo * glow;
                s.clearCoatMask = coat;
                s.clearCoatSmoothness = 0.82;

                half4 c = UniversalFragmentPBR(d, s);
                return half4(MixFog(c.rgb, d.fogCoord), 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection;
            float3 _LightPosition;

            float4 Vert(Attributes v) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(v);
                float3 p = TransformObjectToWorld(v.positionOS.xyz);
                float3 n = TransformObjectToWorldNormal(v.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 l = normalize(_LightPosition - p);
            #else
                float3 l = _LightDirection;
            #endif
                return ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(p, n, l)));
            }

            half4 Frag() : SV_Target { return 0; }
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
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling

            float4 Vert(Attributes v) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(v);
                return TransformWorldToHClip(TransformObjectToWorld(v.positionOS.xyz));
            }

            half Frag() : SV_Target { return 0; }
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
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                o.positionCS = TransformWorldToHClip(TransformObjectToWorld(v.positionOS.xyz));
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }

            half4 Frag(Varyings i, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                return half4(normalize(i.normalWS) * IS_FRONT_VFACE(face, 1, -1), 0);
            }
            ENDHLSL
        }
    }
}
