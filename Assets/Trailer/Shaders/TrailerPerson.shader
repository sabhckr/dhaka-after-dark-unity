// The trailer's characters: the game's person shader plus what a close-up needs and the game does not pay for. Skin lets light bleed past the terminator, warm and soft;
// hair gets two strand highlights instead of a plastic sheen. Trailer only: the game keeps Dad/Person.
Shader "Dad/TrailerPerson"
{
    Properties
    {
        [MainTexture] _BaseMap ("Picture", 2D) = "white" {}
        _Key ("Garment in the picture", Color) = (1,0,0,1)
        _Wears ("Worn instead", Color) = (1,0,0,1)
        _Reach ("How near to the garment's colour", Float) = 0.09
        _Keyed ("Change the garment", Float) = 0
        _Skin ("Skin, against the picture", Float) = 1
        _Dye ("All one colour, the picture only shading it", Float) = 0
        _Cut ("Nothing where the opacity map is darker than this", Float) = 0
        [Normal] _BumpMap ("The model's relief", 2D) = "bump" {}
        _OpacityMap ("The model's opacity", 2D) = "white" {}
        _OpacityPick ("Which channel of it", Vector) = (1,0,0,0)
        _Smoothness ("Smoothness, without a map", Float) = 0.18
        _GlossMap ("The model's smoothness or roughness", 2D) = "white" {}
        _GlossPick ("Which channel of it", Vector) = (1,0,0,0)
        _GlossInvert ("It is roughness", Float) = 0
        _HasGloss ("There is one", Float) = 0
        _SpecularMap ("The model's specular colour", 2D) = "black" {}
        _OrmMap ("Occlusion in red, metallic in blue", 2D) = "red" {}
        _HasOcc ("Its red is occlusion", Float) = 0
        _MaskMap ("The model's colour mask", 2D) = "black" {}
        _TintR ("Colour for the mask's red", Color) = (1,1,1,1)
        _TintG ("Colour for the mask's green", Color) = (1,1,1,1)
        _TintB ("Colour for the mask's blue", Color) = (1,1,1,1)
        _TintA ("Colour for the mask's alpha", Color) = (1,1,1,1)
        _Alpha ("How solid a see-through part is", Float) = 1
        _OccMap ("Ambient occlusion file", 2D) = "white" {}
        _MetalMap ("Metallic file", 2D) = "black" {}
        _HasMaps ("Occlusion and metallic come from their own files", Float) = 0
        _Scatter ("Light through skin", Float) = 1
        _Hair ("All of this part is hair", Float) = 0
        _HairA ("Hair in the map: u min, v min, u max, v max", Vector) = (0,0,0,0)
        _HairB ("Hair in the map, second area", Vector) = (0,0,0,0)
        _Inside ("Inside the mouth in the map, kept dull and dark", Vector) = (0,0,0,0)
        _Face ("Face in the map: no wet shine on lids or lips", Vector) = (0,0,0,0)
        _GlossScale ("Everything else, less polished", Float) = 1
        _SkinGloss ("The most polish skin can have", Float) = 0.46
        [HideInInspector] _SrcBlend ("Source blend", Float) = 1
        [HideInInspector] _DstBlend ("Destination blend", Float) = 0
        [HideInInspector] _ZWrite ("Write depth", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _Key, _Wears;
            half _Reach, _Keyed, _Skin, _Dye, _Cut, _Smoothness, _Alpha, _GlossInvert, _HasGloss, _HasOcc;
            half4 _OpacityPick, _GlossPick, _TintR, _TintG, _TintB, _TintA, _HairA, _HairB;
            half _HasMaps, _Scatter, _Hair, _GlossScale, _SkinGloss;
            half4 _Inside, _Face;
        CBUFFER_END

        // A mouth for bodies with no jaw bone: below the line of the lips, the front of the lower face turns about the jaw hinge.
        float4x4 _HeadFrom, _HeadTo; float _JawOpen; float4 _JawSite;
        float3 Jaw(float3 wp)
        {
            if (_JawOpen <= 0.0001) return wp;
            float3 h = mul(_HeadFrom, float4(wp, 1)).xyz;
            float y0 = _JawSite.x, z0 = _JawSite.y;
            float m = (1 - smoothstep(y0 - 0.003, y0 + 0.003, h.y)) * smoothstep(y0 - 0.08, y0 - 0.048, h.y) * smoothstep(z0 - 0.08, z0 - 0.03, h.z) * (1 - smoothstep(0.035, 0.065, abs(h.x)));
            if (m <= 0) return wp;
            float a = _JawOpen * m, s = sin(a), c = cos(a);
            float2 pivot = float2(y0 + 0.015, z0 - 0.085), q = h.yz - pivot;
            h.yz = pivot + float2(q.x * c - q.y * s, q.x * s + q.y * c);
            return mul(_HeadTo, float4(h, 1)).xyz;
        }
        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);
        TEXTURE2D(_BumpMap);
        SAMPLER(sampler_BumpMap);
        TEXTURE2D(_OpacityMap);
        TEXTURE2D(_GlossMap);
        TEXTURE2D(_SpecularMap);
        TEXTURE2D(_OrmMap);
        TEXTURE2D(_MaskMap);
        TEXTURE2D(_OccMap);
        TEXTURE2D(_MetalMap);
        SAMPLER(sampler_OpacityMap);
        void Cut(float2 map) { if (_Cut > 0) clip(dot(SAMPLE_TEXTURE2D(_OpacityMap, sampler_OpacityMap, map), _OpacityPick) - _Cut); }
        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float4 tangentOS : TANGENT;
            float2 uv : TEXCOORD0;
        };
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off // a hem or a sleeve is open: its inside can be seen
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_local_fragment _ _SPECULAR_SETUP
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
                float3 normalWS : TEXCOORD1;
                float2 map : TEXCOORD2;
                half fog : TEXCOORD3;
                float4 tangentWS : TEXCOORD4;
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionWS = Jaw(TransformObjectToWorld(v.positionOS.xyz));
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.tangentWS = float4(TransformObjectToWorldDir(v.tangentOS.xyz), v.tangentOS.w * GetOddNegativeScale());
                o.map = TRANSFORM_TEX(v.uv, _BaseMap);
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            static const half3 Luma = half3(0.299, 0.587, 0.114);

            half3 Change(half3 c, half y, half2 chroma, half3 key, half3 wears)
            {
                half ky = dot(key, Luma);
                half d = length(chroma - half2(key.b - ky, key.r - ky) / max(ky, 0.04));
                return lerp(c, wears * y / max(ky, 0.04), 1 - smoothstep(_Reach, _Reach * 1.6, d));
            }

            half3 Person(half3 c)
            {
                half y = dot(c, Luma);
                if (_Dye > 0.5) return _Wears.rgb * (0.55 + 0.6 * y); // hair and beard: painted light and dark in the picture, but one colour on a head
                half2 chroma = half2(c.b - y, c.r - y) / max(y, 0.04); // by brightness: a fold in shade is still the same colour
                // Skin is warm: red above and blue below the grey line, by a moderate amount.
                half skin = smoothstep(0, 0.4, saturate(1 - length((chroma - half2(-0.33, 0.3)) / 0.3)));
                c *= lerp(1, _Skin, skin);
                if (_Keyed > 0.5) c = Change(c, y, chroma, _Key.rgb, _Wears.rgb);
                return c;
            }

            static const half3 Through = half3(1, 0.62, 0.5); // what is left of light after it has crossed flesh

            // How much light a surface passes on beyond the line where it would go dark.
            half3 Bleed(half3 n, half3 l, half3 colour, half shade)
            {
                half nl = dot(n, l);
                half wrapped = saturate((nl + 0.55) / 1.55);
                return colour * saturate(wrapped * wrapped - saturate(nl)) * shade * Through * 0.35;
            }

            // Strands: light runs along a hair, so the highlight is a band across it. T is taken as falling down the head; the map's own strand detail breaks it up.
            half3 Strand(half3 t, half3 n, half3 l, half3 v, half3 colour, half shade, half3 albedo, half grain)
            {
                half3 h = normalize(l + v);
                half3 t1 = normalize(t + n * (grain * 0.28 - 0.06));
                half3 t2 = normalize(t + n * (grain * 0.28 + 0.12));
                half a = dot(t1, h), b = dot(t2, h);
                half s1 = pow(saturate(1 - a * a), 70) * smoothstep(-1, 0, a);
                half s2 = pow(saturate(1 - b * b), 18) * smoothstep(-1, 0, b);
                return colour * shade * saturate(dot(n, l) * 0.6 + 0.4) * (s1 * 0.035 + s2 * 0.03 * (albedo * 4 + 0.12));
            }

            bool In(float2 map, half4 r) { return map.x >= r.x && map.y >= r.y && map.x <= r.z && map.y <= r.w; }

            half4 Frag(Varyings i, half facing : VFACE) : SV_Target
            {
                Cut(i.map);
                half3 albedo = Person(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.map).rgb);
                half4 mask = SAMPLE_TEXTURE2D(_MaskMap, sampler_BaseMap, i.map);
                albedo *= lerp(1, _TintR.rgb, mask.r) * lerp(1, _TintG.rgb, mask.g) * lerp(1, _TintB.rgb, mask.b) * lerp(1, _TintA.rgb, mask.a);
                half gloss = dot(SAMPLE_TEXTURE2D(_GlossMap, sampler_BaseMap, i.map), _GlossPick);
                half4 orm = SAMPLE_TEXTURE2D(_OrmMap, sampler_BaseMap, i.map);
                half3 picture = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.map).rgb;
                half luma = dot(picture, Luma);
                half2 hue = half2(picture.b - luma, picture.r - luma) / max(luma, 0.04);
                half isSkin = (1 - _Hair) * smoothstep(0, 0.4, saturate(1 - length((hue - half2(-0.33, 0.3)) / 0.34))) * smoothstep(0.03, 0.09, luma);
                half isHair = max(_Hair, (In(i.map, _HairA) || In(i.map, _HairB)) ? 1 - smoothstep(0.1, 0.18, luma) : 0);
                InputData inputData = (InputData)0;
                #define d inputData
                d.positionWS = i.positionWS;
                float3 n = normalize(i.normalWS) * (facing > 0 ? 1 : -1); // the inside of a sleeve is lit like the cloth it is
                half3 relief = UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, i.map));
                d.normalWS = normalize(relief.x * i.tangentWS.xyz + relief.y * cross(n, i.tangentWS.xyz) * i.tangentWS.w + relief.z * n);
                d.viewDirectionWS = GetWorldSpaceNormalizeViewDir(i.positionWS);
                d.shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                d.fogCoord = i.fog;
                d.bakedGI = SampleSH(d.normalWS);
                d.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                d.positionCS = i.positionCS;
                SurfaceData s = (SurfaceData)0;
                s.albedo = albedo;
                s.alpha = dot(SAMPLE_TEXTURE2D(_OpacityMap, sampler_OpacityMap, i.map), _OpacityPick) * _Alpha;
                s.specular = SAMPLE_TEXTURE2D(_SpecularMap, sampler_BaseMap, i.map).rgb;
                s.metallic = orm.b;
                s.occlusion = _HasOcc > 0.5 ? orm.r : 1;
                s.smoothness = _HasGloss > 0.5 ? (_GlossInvert > 0.5 ? 1 - gloss : gloss) : _Smoothness;
                if (_HasMaps > 0.5)
                {
                    s.occlusion = SAMPLE_TEXTURE2D(_OccMap, sampler_BaseMap, i.map).r;
                    s.metallic = SAMPLE_TEXTURE2D(_MetalMap, sampler_BaseMap, i.map).r;
                }
                s.smoothness *= _GlossScale;
                if (In(i.map, _Face)) s.smoothness = min(s.smoothness, 0.4);
                if (In(i.map, _Inside)) { s.albedo *= 0.22; s.smoothness = min(s.smoothness, 0.2); s.occlusion *= 0.4; isSkin = 0; }
                s.smoothness = lerp(s.smoothness, min(s.smoothness, _SkinGloss), isSkin);
                s.smoothness = lerp(s.smoothness, 0.16, isHair);
                s.occlusion = lerp(s.occlusion, s.occlusion * s.occlusion, isHair);
                half4 c = UniversalFragmentPBR(d, s);
                if (isSkin > 0.01 && _Scatter > 0 || isHair > 0.01)
                {
                    half3 fall = normalize(float3(0, -1, 0) - d.normalWS * dot(float3(0, -1, 0), d.normalWS) + float3(0.0001, 0, 0));
                    half grain = gloss;
                    half3 extra = 0;
                    Light main = GetMainLight(d.shadowCoord, d.positionWS, half4(1, 1, 1, 1));
                    half shade = main.distanceAttenuation * lerp(main.shadowAttenuation, 1, 0.35);
                    extra += albedo * Bleed(d.normalWS, main.direction, main.color, shade) * isSkin * _Scatter;
                    extra += Strand(fall, d.normalWS, main.direction, d.viewDirectionWS, main.color, shade, albedo, grain) * isHair;
                    uint pixelLightCount = GetAdditionalLightsCount();
                    LIGHT_LOOP_BEGIN(pixelLightCount)
                        Light l = GetAdditionalLight(lightIndex, d.positionWS, half4(1, 1, 1, 1));
                        half a = l.distanceAttenuation * lerp(l.shadowAttenuation, 1, 0.35);
                        extra += albedo * Bleed(d.normalWS, l.direction, l.color, a) * isSkin * _Scatter;
                        extra += Strand(fall, d.normalWS, l.direction, d.viewDirectionWS, l.color, a, albedo, grain) * isHair;
                    LIGHT_LOOP_END
                    c.rgb += extra;
                }
                #undef d
                c.rgb = MixFog(c.rgb, i.fog);
                return c;
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
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection;
            float3 _LightPosition;

            struct Varyings { float4 positionCS : SV_POSITION; float2 map : TEXCOORD0; };

            Varyings Vert(Attributes v)
            {
                float3 p = Jaw(TransformObjectToWorld(v.positionOS.xyz));
                float3 n = TransformObjectToWorldNormal(v.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 l = normalize(_LightPosition - p);
            #else
                float3 l = _LightDirection;
            #endif
                Varyings o;
                o.positionCS = ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(p, n, l)));
                o.map = TRANSFORM_TEX(v.uv, _BaseMap);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target { Cut(i.map); return 0; }
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

            struct Varyings { float4 positionCS : SV_POSITION; float2 map : TEXCOORD0; };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformWorldToHClip(Jaw(TransformObjectToWorld(v.positionOS.xyz)));
                o.map = TRANSFORM_TEX(v.uv, _BaseMap);
                return o;
            }

            half Frag(Varyings i) : SV_Target { Cut(i.map); return 0; }
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
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float2 map : TEXCOORD1; };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformWorldToHClip(Jaw(TransformObjectToWorld(v.positionOS.xyz)));
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.map = TRANSFORM_TEX(v.uv, _BaseMap);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target { Cut(i.map); return half4(normalize(i.normalWS), 0); }
            ENDHLSL
        }
    }
}
