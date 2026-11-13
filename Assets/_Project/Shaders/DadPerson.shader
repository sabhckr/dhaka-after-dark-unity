// The people of the street (NPC Phase 3, 2026-11-08): the owner's picture, with one garment's colour changed and the skin made lighter or darker, so that one model can be many people
// without another texture. A colour is changed by how near its chroma, taken against its brightness, is to _Key; its light and shade come from the picture, so the folds stay.
Shader "Dad/Person"
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
        [Normal] _DetailNormal ("The fabric's weave or grain, tiled", 2D) = "bump" {}
        _DetailTiling ("How many times the weave repeats across the map (0: none)", Float) = 0
        _DetailStrength ("How deep the weave is", Float) = 1
        _Mute ("How far the colour is greyed, as cloth worn and washed (0: as painted)", Float) = 0
        _Sheen ("Cloth: light caught by the fibres at a glancing view (0: none)", Float) = 0
        _Scatter ("Skin: light carried under the skin, red furthest (0: none, 1: skin)", Float) = 0
        _Hair ("Hair cards: two highlights along the strands (0: none, 1: hair)", Float) = 0
        _Eye ("Eyeball: the lids' shadow over its edges (0: none, 1: eye)", Float) = 0
        _Alpha ("How solid a see-through part is", Float) = 1
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
            half _Reach, _Keyed, _Skin, _Dye, _Cut, _Smoothness, _Alpha, _GlossInvert, _HasGloss, _HasOcc, _DetailTiling, _DetailStrength, _Sheen, _Mute, _Scatter, _Hair, _Eye;
            half4 _OpacityPick, _GlossPick, _TintR, _TintG, _TintB, _TintA;
        CBUFFER_END
        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);
        TEXTURE2D(_BumpMap);
        SAMPLER(sampler_BumpMap);
        TEXTURE2D(_DetailNormal);
        SAMPLER(sampler_DetailNormal);
        TEXTURE2D(_OpacityMap);
        TEXTURE2D(_GlossMap);
        TEXTURE2D(_SpecularMap);
        TEXTURE2D(_OrmMap);
        TEXTURE2D(_MaskMap);
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
            // No _SCREEN_SPACE_OCCLUSION (2026-11-12): people are kept out of the depth-normals pass the street's occlusion is worked out from (NpcCrowd.Dress), so what it
            // holds at a person's pixel is the shading of whatever stands behind them; read here, it printed a gun's grooves through the arm in front of it.
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
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.tangentWS = float4(TransformObjectToWorldDir(v.tangentOS.xyz), v.tangentOS.w * GetOddNegativeScale());
                o.map = v.uv;
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

            // One strand's highlight (Kajiya-Kay): light mirrored by a thin cylinder lies on a cone round it, so it is brightest where the half vector is square to the strand.
            half Strand(half3 t, half3 h, half sharp)
            {
                half th = dot(t, h);
                return smoothstep(-1, 0, th) * pow(saturate(1 - th * th), sharp * 0.5); // sin^sharp, from sin^2
            }

            // What a light adds that the plain surface does not (2026-11-12, after how large studios shade skin and hair):
            // skin: light carried under the skin. A cheek turned from a lamp still glows past the line where the light ends, red furthest, and a shadow's soft
            //   edge is red, not grey. Diffuse wrapped per colour (the curve pre-integrated skin shading fits); red sees the surface without its fine relief.
            //   How far it wraps follows how sharply the surface curves (curve, 0 to 1): a nose, a lip, an ear's rim glow; a flat cheek barely changes.
            //   What the plain surface already gave is taken off, so only the difference is added.
            // hair cards: two highlights along the strands, one near white and sharp, one in the hair's colour, broader and shifted, broken up strand by strand.
            //   The strands run down the head, as hair falls and is combed; on the crown, where down goes straight into the scalp, there is none.
            half3 More(Light l, half3 nFine, half3 nBody, half3 v, half3 albedo, half grain, half curve)
            {
                half3 radiance = l.color * l.distanceAttenuation;
                half3 add = 0;
                if (_Scatter > 0)
                {
                    half nl = dot(nFine, l.direction), nlBody = dot(nBody, l.direction);
                    half3 w = _Scatter * (half3(0.06, 0.02, 0.01) + curve * half3(0.6, 0.2, 0.1));
                    half3 wrapped = saturate((half3(nlBody, nl, nl) + w) / (1 + w));
                    half3 shade = pow(max(l.shadowAttenuation, 0.0001h), 1 / (1 + _Scatter * half3(0.6, 0.2, 0.05)));
                    add += albedo * 0.96 * radiance * (wrapped * shade - saturate(nl) * l.shadowAttenuation);
                }
                if (_Hair > 0)
                {
                    half3 t = half3(0, -1, 0) - nBody * -nBody.y; // down, laid along the surface
                    half along = length(t);
                    t /= max(along, 0.001h);
                    half3 h = normalize(l.direction + v);
                    half first = Strand(normalize(t + nBody * (0.1 + grain)), h, 80);
                    half second = Strand(normalize(t + nBody * (-0.1 + grain)), h, 18) * saturate(0.4 + 3 * grain);
                    half lit = saturate(dot(nBody, l.direction) * 0.75 + 0.25) * l.shadowAttenuation * smoothstep(0.25, 0.6, along);
                    add += _Hair * radiance * lit * (0.1 * first + 0.45 * second * albedo);
                }
                return add;
            }

            half4 Frag(Varyings i, half facing : VFACE) : SV_Target
            {
                Cut(i.map);
                half3 picture = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.map).rgb;
                half3 albedo = Person(picture);
                half4 mask = SAMPLE_TEXTURE2D(_MaskMap, sampler_BaseMap, i.map);
                albedo *= lerp(1, _TintR.rgb, mask.r) * lerp(1, _TintG.rgb, mask.g) * lerp(1, _TintB.rgb, mask.b) * lerp(1, _TintA.rgb, mask.a);
                albedo = lerp(albedo, dot(albedo, Luma).xxx, _Mute);
                half gloss = dot(SAMPLE_TEXTURE2D(_GlossMap, sampler_BaseMap, i.map), _GlossPick);
                half4 orm = SAMPLE_TEXTURE2D(_OrmMap, sampler_BaseMap, i.map);
                InputData d = (InputData)0;
                d.positionWS = i.positionWS;
                float3 n = normalize(i.normalWS) * (facing > 0 ? 1 : -1); // the inside of a sleeve is lit like the cloth it is
                half3 relief = UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, i.map));
                if (_DetailTiling > 0) // the weave laid over the garment's own relief (whiteout blend)
                {
                    half3 weave = UnpackNormalScale(SAMPLE_TEXTURE2D(_DetailNormal, sampler_DetailNormal, i.map * _DetailTiling), _DetailStrength);
                    relief = normalize(half3(relief.xy + weave.xy, relief.z * weave.z));
                }
                d.normalWS = normalize(relief.x * i.tangentWS.xyz + relief.y * cross(n, i.tangentWS.xyz) * i.tangentWS.w + relief.z * n);
                d.viewDirectionWS = GetWorldSpaceNormalizeViewDir(i.positionWS);
                half lid = 1; // the lids shade the eyeball's edges, as an occlusion shell over the eye does, light and reflection both; the iris facing out stays as painted
                if (_Eye > 0) lid = lerp(1, saturate(1 - 1.6 * (1 - saturate(dot(n, d.viewDirectionWS)))), _Eye);
                albedo *= lid;
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
                s.occlusion = (_HasOcc > 0.5 ? orm.r : 1) * lid * (_Hair > 0 ? 0.6 : 1); // hair: a mass of strands shades itself, so the sky shows less in it
                s.smoothness = _HasGloss > 0.5 ? (_GlossInvert > 0.5 ? 1 - gloss : gloss) : _Smoothness;
                half4 c = UniversalFragmentPBR(d, s);
                if (_Sheen > 0) // cloth, not plastic: fibres standing off the weave catch light at a glancing view, in the cloth's own colour
                {
                    Light sun = GetMainLight(d.shadowCoord);
                    half edge = pow(1 - saturate(dot(d.normalWS, d.viewDirectionWS)), 4);
                    half lit = saturate(dot(d.normalWS, sun.direction) * 0.5 + 0.5) * sun.shadowAttenuation;
                    c.rgb += _Sheen * edge * (sun.color * lit + d.bakedGI) * sqrt(max(albedo, 0.0001)) * 0.6;
                }
                if (_Scatter > 0 || _Hair > 0)
                {
                    half4 shadowMask = CalculateShadowMask(d);
                    half grain = (dot(picture, Luma) - 0.5) * 0.3; // the picture's own light and dark strands move the highlights along them
                    half curve = saturate(length(fwidth(n)) / max(length(fwidth(i.positionWS)), 0.00001) * 0.01); // 1 at a radius of 1 cm, 0.1 at 10 cm
                    c.rgb += More(GetMainLight(d.shadowCoord, d.positionWS, shadowMask), d.normalWS, n, d.viewDirectionWS, albedo, grain, curve);
                #if defined(_ADDITIONAL_LIGHTS)
                    InputData inputData = d; // the name the light loop reads
                    uint pixelLightCount = GetAdditionalLightsCount();
                #if USE_CLUSTER_LIGHT_LOOP
                    [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
                    {
                        CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
                        c.rgb += More(GetAdditionalLight(lightIndex, d.positionWS, shadowMask), d.normalWS, n, d.viewDirectionWS, albedo, grain, curve);
                    }
                #endif
                    LIGHT_LOOP_BEGIN(pixelLightCount)
                        c.rgb += More(GetAdditionalLight(lightIndex, d.positionWS, shadowMask), d.normalWS, n, d.viewDirectionWS, albedo, grain, curve);
                    LIGHT_LOOP_END
                #endif
                }
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
                o.positionCS = TransformWorldToHClip(TransformObjectToWorld(v.positionOS.xyz));
                o.map = v.uv;
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
                o.positionCS = TransformWorldToHClip(TransformObjectToWorld(v.positionOS.xyz));
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.map = v.uv;
                return o;
            }

            half4 Frag(Varyings i) : SV_Target { Cut(i.map); return half4(normalize(i.normalWS), 0); }
            ENDHLSL
        }
    }
}
