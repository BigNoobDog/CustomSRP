Shader "Hidden/MSRP/DeferredLighting"
{
    SubShader
    {
        Cull Off
        ZTest Always
        ZWrite Off

        HLSLINCLUDE
        #include "DeferredLightPass.hlsl"
        ENDHLSL

        Pass
        {
            Name "Deferred Lighting"

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DefaultPassVertex
            #pragma fragment DeferredLightingPassFragment
            ENDHLSL
        }
    }
}