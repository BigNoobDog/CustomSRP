#ifndef MSRP_GBUFFER_PASS
#define MSRP_GBUFFER_PASS

struct FragmentOutput
{
    half4 GBuffer0 : SV_Target0;
    half4 GBuffer1 : SV_Target1;
    half4 GBuffer2 : SV_Target2;
    half4 GBuffer3 : SV_Target3;
    half4 GBuffer4 : SV_Target4;
    half4 GBuffer5 : SV_Target5;
};

FragmentOutput EncodeGBuffer(Surface surface, BRDF brdf, float3 gi, float3 emission)
{

    //World Space Normal, Per Object GBuffer Data
    //Metallic, Specular, Smoothness, Shading Model ID
    //BaseColor, AO
    //CustomData
    //PercomputedShadow
    //WorldTangent, /
    
    FragmentOutput output;
    uint shadingModelID;
    output.GBuffer0 = float4(surface.normalWS, 0);
    output.GBuffer1 = float4(surface.metallic, 0, surface.smoothness, shadingModelID);
    output.GBuffer2 = float4(surface.color, surface.occlusion);
    output.GBuffer3 = 0;
    output.GBuffer4 = float4(gi, 0);
    output.GBuffer5 = 0;

    return output;
}

void DecodeGBuffer()
{
    
}

#endif