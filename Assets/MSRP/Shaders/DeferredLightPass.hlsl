#ifndef DEFERRED_LIGHTING_PASS_INCLUED
#define DEFERRED_LIGHTING_PASS_INCLUED

#include "../ShaderLibrary/Common.hlsl"
#include "MSRPGBuffer.hlsl"

// #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

TEXTURE2D(_GBufferA);
TEXTURE2D(_GBufferB);
TEXTURE2D(_GBufferC);
TEXTURE2D(_GBufferD);
TEXTURE2D(_GBufferE);
TEXTURE2D(_GBufferF);

float4 _GBufferA_TexelSize;

float4 GetSourceTexelSize () {
    return _GBufferA_TexelSize;
}

// float4 GetSource(float2 screenUV) {
//     return SAMPLE_TEXTURE2D_LOD(_PostFXSource, sampler_linear_clamp, screenUV, 0);
// }

struct Varyings {
    float4 positionCS : SV_POSITION;
    float2 screenUV : VAR_SCREEN_UV;
};

Varyings DefaultPassVertex (uint vertexID : SV_VertexID) {
    Varyings output;
    output.positionCS = float4(
        vertexID <= 1 ? -1.0 : 3.0,
        vertexID == 1 ? 3.0 : -1.0,
        0.0, 1.0
    );
    output.screenUV = float2(
        vertexID <= 1 ? 0.0 : 2.0,
        vertexID == 1 ? 2.0 : 0.0
    );
    if (_ProjectionParams.x < 0.0) {
        output.screenUV.y = 1.0 - output.screenUV.y;
    }
    return output;
}

float4 DeferredLightingPassFragment (Varyings input) : SV_TARGET {
    float4 GBuffer[6];
    GBuffer[0] = SAMPLE_TEXTURE2D_LOD(_GBufferA, sampler_linear_clamp, input.screenUV, 0);
    GBuffer[1] = SAMPLE_TEXTURE2D_LOD(_GBufferB, sampler_linear_clamp, input.screenUV, 0);
    GBuffer[2] = SAMPLE_TEXTURE2D_LOD(_GBufferC, sampler_linear_clamp, input.screenUV, 0);
    GBuffer[3] = SAMPLE_TEXTURE2D_LOD(_GBufferD, sampler_linear_clamp, input.screenUV, 0);
    GBuffer[4] = SAMPLE_TEXTURE2D_LOD(_GBufferE, sampler_linear_clamp, input.screenUV, 0);
    GBuffer[5] = SAMPLE_TEXTURE2D_LOD(_GBufferF, sampler_linear_clamp, input.screenUV, 0);

    Surface surface;
    BRDF brdf;
    // DecodeGBuffer(surface, brdf);
    
    return float4(SAMPLE_TEXTURE2D_LOD(_GBufferA, sampler_linear_clamp, input.screenUV, 0).rgb, 1);
}
#endif