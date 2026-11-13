// Draws one slice of a texture array at one mip, everywhere: MaterialLibraryTests read each
// layer's last mip (its mean) through it, to check the imported slice order.
Shader "Hidden/Dad/ArraySlice"
{
    Properties
    {
        _Array("Array", 2DArray) = "" {}
        _Slice("Slice", Float) = 0
        _Lod("Mip", Float) = 12
    }
    SubShader
    {
        Pass
        {
            ZTest Always ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D_ARRAY(_Array);
            SAMPLER(sampler_Array);
            float _Slice, _Lod;

            // Graphics.Blit's quad spans 0..1; cover the target whatever the projection.
            float4 Vert(float4 p : POSITION) : SV_POSITION { return float4(p.xy * 2 - 1, 0.5, 1); }

            float4 Frag() : SV_Target { return SAMPLE_TEXTURE2D_ARRAY_LOD(_Array, sampler_Array, float2(0.5, 0.5), _Slice, _Lod); }
            ENDHLSL
        }
    }
}
