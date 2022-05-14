#ifndef MSRP_LIT_GBUFFER_PASS
#define MSRP_LIT_GBUFFER_PASS

#include "../ShaderLibrary/Common.hlsl"
#include "LitInput.hlsl"
#include "../ShaderLibrary/Surface.hlsl"
#include "../ShaderLibrary/Shadows.hlsl"
#include "../ShaderLibrary/Light.hlsl"
#include "../ShaderLibrary/BRDF.hlsl"
#include "../ShaderLibrary/GlobalIllumination.hlsl"
#include "MSRPGBuffer.hlsl"

struct Attributes {
    float3 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float4 tangentOS : TANGENT;
    float2 baseUV : TEXCOORD0;
    GI_ATTRIBUTE_DATA
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings {
    float4 positionCS_SS : SV_POSITION;
    float3 positionWS : VAR_POSITION;
    float3 normalWS : VAR_NORMAL;
    #if defined(_NORMAL_MAP)
    float4 tangentWS : VAR_TANGENT;
    #endif
    float2 baseUV : VAR_BASE_UV;
    #if defined(_DETAIL_MAP)
    float2 detailUV : VAR_DETAIL_UV;
    #endif
    GI_VARYINGS_DATA
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

Varyings LitGBufferPassVertex (Attributes input) {
    Varyings output;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    TRANSFER_GI_DATA(input, output);
    output.positionWS = TransformObjectToWorld(input.positionOS);
    output.positionCS_SS = TransformWorldToHClip(output.positionWS);
    output.normalWS = TransformObjectToWorldNormal(input.normalOS);
    #if defined(_NORMAL_MAP)
    output.tangentWS = float4(
        TransformObjectToWorldDir(input.tangentOS.xyz), input.tangentOS.w
    );
    #endif
    output.baseUV = TransformBaseUV(input.baseUV);
    #if defined(_DETAIL_MAP)
    output.detailUV = TransformDetailUV(input.baseUV);
    #endif
    return output;
}

void InitializeSurfaceData(Varyings input, InputConfig config, out Surface surface)
{
    float4 base = GetBase(config);
    #if defined(_CLIPPING)
    clip(base.a - GetCutoff(config));
    #endif
    surface.position = input.positionWS;
    #if defined(_NORMAL_MAP)
    surface.normalWS = NormalTangentToWorld(
        GetNormalTS(config), input.normalWS, input.tangentWS
    );
    surface.interpolatedNormal = input.normalWS;
    #else
    surface.normalWS = normalize(input.normalWS);
    surface.interpolatedNormal = surface.normalWS;
    #endif
    surface.viewDirectionWS = normalize(_WorldSpaceCameraPos - input.positionWS);
    surface.depth = -TransformWorldToView(input.positionWS).z;
    surface.color = base.rgb;
    surface.alpha = base.a;
    surface.metallic = GetMetallic(config);
    surface.occlusion = GetOcclusion(config);
    surface.smoothness = GetSmoothness(config);
    surface.fresnelStrength = GetFresnel(config);
    surface.dither = InterleavedGradientNoise(config.fragment.positionSS, 0);
    surface.renderingLayerMask = asuint(unity_RenderingLayer.x);
    surface.screenUV = config.fragment.screenUV;
}

bool RenderingLayersOverlap (Surface surface, Light light) {
    return (surface.renderingLayerMask & light.renderingLayerMask) != 0;
}

FragmentOutput LitGBufferPassFragment(Varyings input)
{
    UNITY_SETUP_INSTANCE_ID(input);

    InputConfig config = GetInputConfig(input.positionCS_SS, input.baseUV);
    ClipLOD(config.fragment, unity_LODFade.x);
    
    Surface surface;
    InitializeSurfaceData(input, config, surface);

    BRDF brdf;
    #if defined(_PREMULTIPLY_ALPHA)
    InitializeBRDFData(surface, brdf, true);
    #else
    InitializeBRDFData(surface, brdf);
    #endif
	
    GI gi = GetGI(GI_FRAGMENT_DATA(input), surface, brdf);
	
    ShadowData shadowData = GetShadowData(surface);
    shadowData.shadowMask = gi.shadowMask;
	
    float3 giColor = GetIndirectBRDF(surface, brdf, gi.diffuse, gi.specular);

    
    
    float emission = GetEmission(config);
    float finalAlpha = GetFinalAlpha(surface.alpha);
    return EncodeGBuffer(surface, brdf, giColor, emission);
}

#endif