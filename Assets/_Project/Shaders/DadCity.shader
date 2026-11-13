// The owner's building, airport and aircraft models, drawn instanced by CityView (Graphics.RenderMeshInstanced) and lit by the same URP path as Dad/World, so sun, sky, fog, shadows and ambient
// occlusion reach them as they reach the streets. Every instance has its own _Tint: rgb multiplies the model's own picture, so one model gives many buildings; a is the mirror sign, doubled when
// the instance has windows that light at night. A mirrored instance has a negative x scale. The GPU cannot turn that one instance's winding round, so nothing is culled, and the normal is turned
// to the viewer instead of trusted to the face: that also makes single sheets in the bought models light correctly from both sides.
Shader "Dad/City"
{
    Properties { _BaseMap ("Picture", 2D) = "white" {} }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        UNITY_INSTANCING_BUFFER_START(DadCity)
            UNITY_DEFINE_INSTANCED_PROP(float4, _Tint)
        UNITY_INSTANCING_BUFFER_END(DadCity)
        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            half4 color : COLOR;
            float2 uv : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
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
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma shader_feature_local_fragment _CUT
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                half4 color : COLOR;
                float2 map : TEXCOORD2;
                float4 local : TEXCOORD3; // model position, fog
                float3 wear : TEXCOORD4; // how much of each kind of wear this building has: streaks, grime, faded paint
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            float4 _DadLights; // windows, lamps, shops, signs: how much of the city is lit (NightLife)

            Varyings Vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.color = half4(SRGBToLinear(v.color.rgb), 1);
                o.map = v.uv;
                o.local = float4(v.positionOS.xyz, ComputeFogFactor(o.positionCS.z));
                // From where the building stands, so each has its own wear and every piece of it the same.
                float3 at = TransformObjectToWorld(float3(0, 0, 0));
                float3 h = frac(sin(float3(dot(at.xz, float2(12.9, 78.2)), dot(at.xz, float2(39.3, 11.1)), dot(at.xz, float2(73.1, 52.7)))) * 43758.5453);
                o.wear = h * h; // most buildings lightly worn, a few badly
                return o;
            }

            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }

            float Noise(float2 p)
            {
                float2 c = floor(p), f = frac(p);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(Hash(c), Hash(c + float2(1, 0)), f.x), lerp(Hash(c + float2(0, 1)), Hash(c + float2(1, 1)), f.x), f.y);
            }

            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float4 tint = UNITY_ACCESS_INSTANCED_PROP(DadCity, _Tint);
                half4 sampled = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.map);
            #if _CUT
                clip(sampled.a - 0.42); // The owner's grass cards.
            #endif
                half3 picture = sampled.rgb;
                half3 albedo = i.color.rgb * picture * tint.rgb;
                // Dust and damp gather at the foot of every wall, more on some buildings than others.
                albedo *= 1 - (0.24 + 0.22 * i.wear.y) * saturate(1 - i.local.y * 0.45);

                float3 v = GetWorldSpaceNormalizeViewDir(i.positionWS);
                float3 n = normalize(i.normalWS);
                n = dot(n, v) < 0 ? -n : n;

                bool windowed = abs(tint.a) > 1.5;
                half lum = dot(picture, half3(0.33, 0.34, 0.33));
                // The glass in a building's own picture: blue-grey panes. They are drawn pale, with the sky painted in; a real pane is mostly dark.
                // Judged on a blurred sample: the pictures are compressed in blocks, and a sharp test would print those blocks on the wall.
                half3 soft = SAMPLE_TEXTURE2D_BIAS(_BaseMap, sampler_BaseMap, i.map, 3).rgb;
                half glass = windowed && abs(n.y) < 0.4 ? saturate((soft.b - soft.r) * 9) * smoothstep(0.15, 0.35, dot(soft, half3(0.33, 0.34, 0.33))) : 0;
                albedo = lerp(albedo, albedo * half3(0.32, 0.35, 0.38), glass);

                // Weathering on the walls, by the building's own amounts (i.wear). The worst is on windowed buildings; other models (sheds, cranes, aircraft) get half.
                if (abs(n.y) < 0.6)
                {
                    float along = i.positionWS.x * abs(n.z) + i.positionWS.z * abs(n.x);
                    float up = i.positionWS.y;
                    half amount = (windowed ? 1 : 0.5) * (1 - glass);
                    half near = saturate(1.5 - 2 * fwidth(along)); // The fine noise fades before it is smaller than a pixel, or it would shimmer.
                    half streak = near * smoothstep(0.5, 0.85, Noise(float2(along * 1.3, up * 0.05 + i.wear.y * 9))) * (0.35 + 0.65 * i.wear.x);
                    half blotch = Noise(float2(along, up) * 0.22 + i.wear.z * 17) * 0.6 + lerp(0.5, Noise(float2(along, up) * 0.9), near) * 0.4;
                    half grime = smoothstep(0.42, 0.78, blotch) * (0.3 + 0.7 * i.wear.y);
                    half faded = smoothstep(0.35, 0.7, Noise(float2(along, up) * 0.07 + i.wear.x * 5)) * i.wear.z;
                    albedo = lerp(albedo, Luminance(albedo).xxx * 1.04, faded * 0.5 * amount);
                    albedo *= 1 - 0.38 * streak * amount;
                    albedo = lerp(albedo, albedo * half3(0.60, 0.54, 0.46), grime * 0.6 * amount);
                }

                // Windows: one cell of a storey-and-a-bay grid in the model, lit by its own hash, on the darker parts of a wall. Each instance has its own pattern from its tint.
                half glow = 0;
                if (windowed && abs(n.y) < 0.4)
                {
                    float3 g = i.local.xyz / float3(2.6, 3.3, 2.6);
                    float2 cell = float2(floor(g.x + g.z), floor(g.y));
                    float2 in_cell = frac(float2(g.x + g.z, g.y));
                    half pane = step(0.2, in_cell.x) * step(in_cell.x, 0.8) * step(0.3, in_cell.y) * step(in_cell.y, 0.78);
                    half rank = Hash(cell + tint.rg * 91.0);
                    // A window comes up over a few minutes of dusk, not in one frame: a near pane fills a lot of the screen.
                    glow = pane * smoothstep(rank - 0.012, rank, _DadLights.x * 0.2) * smoothstep(0.05, 0.3, _DadLights.x) * step(1.2, i.local.y);
                    glow *= max(glass, 1 - smoothstep(0.2, 0.42, lum)); // only where the picture has a window, not on bare wall
                }

                InputData d = (InputData)0;
                d.positionWS = i.positionWS;
                d.positionCS = i.positionCS;
                d.normalWS = n;
                d.viewDirectionWS = v;
                d.shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                d.fogCoord = i.local.w;
                d.bakedGI = SampleSH(n);
                d.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                d.shadowMask = half4(1, 1, 1, 1);

                SurfaceData s = (SurfaceData)0;
                s.albedo = albedo;
                s.smoothness = lerp(0.18, 0.62, glass);
                s.metallic = 0;
                s.occlusion = 1;
                s.alpha = 1;
                s.normalTS = half3(0, 0, 1);
                s.emission = glow * (0.2 + 0.32 * lum) * half3(1.0, 0.68, 0.36); // Light behind the facade's own glass, not a painted square.

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
            #pragma shader_feature_local_fragment _CUT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection;
            float3 _LightPosition;

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            struct Varyings { float4 positionCS : SV_POSITION; float2 map : TEXCOORD0; };

            Varyings Vert(Attributes v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                float3 p = TransformObjectToWorld(v.positionOS.xyz);
                float3 n = TransformObjectToWorldNormal(v.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 l = normalize(_LightPosition - p);
            #else
                float3 l = _LightDirection;
            #endif
                Varyings o;
                o.positionCS = ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(p, n, l)));
                o.map = v.uv;
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
            #if _CUT
                clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.map).a - 0.42);
            #endif
                return 0;
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
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling
            #pragma shader_feature_local_fragment _CUT
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            struct Varyings { float4 positionCS : SV_POSITION; float2 map : TEXCOORD0; };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                o.positionCS = TransformWorldToHClip(TransformObjectToWorld(v.positionOS.xyz));
                o.map = v.uv;
                return o;
            }

            half Frag(Varyings i) : SV_Target
            {
            #if _CUT
                clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.map).a - 0.42);
            #endif
                return 0;
            }
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
            #pragma shader_feature_local_fragment _CUT
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 positionWS : TEXCOORD1; float2 map : TEXCOORD2; };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.map = v.uv;
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
            #if _CUT
                clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.map).a - 0.42);
            #endif
                float3 n = normalize(i.normalWS);
                return half4(dot(n, GetWorldSpaceNormalizeViewDir(i.positionWS)) < 0 ? -n : n, 0);
            }
            ENDHLSL
        }
    }
}
