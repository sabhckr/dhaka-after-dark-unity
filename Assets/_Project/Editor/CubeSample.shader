// Samples a cubemap in one direction at one mip, everywhere: SkyModelTests read the sky reflection
// through it, to check that its faces are laid out the way the GPU reads them.
Shader "Hidden/Dad/CubeSample"
{
    Properties
    {
        _Cube("Cube", Cube) = "" {}
        _Dir("Direction", Vector) = (0, 1, 0, 0)
        _Lod("Mip", Float) = 0
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
            TEXTURECUBE(_Cube);
            SAMPLER(sampler_Cube);
            float4 _Dir;
            float _Lod;

            float4 Vert(float4 p : POSITION) : SV_POSITION { return float4(p.xy * 2 - 1, 0.5, 1); }

            float4 Frag() : SV_Target { return SAMPLE_TEXTURECUBE_LOD(_Cube, sampler_Cube, normalize(_Dir.xyz), _Lod); }
            ENDHLSL
        }
    }
}
