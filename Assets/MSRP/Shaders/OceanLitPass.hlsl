#ifndef CUSTOM_LIT_PASS_INCLUDED
#define CUSTOM_LIT_PASS_INCLUDED

#include "../ShaderLibrary/Common.hlsl"
#include "OceanLitInput.hlsl"
#include "../ShaderLibrary/Surface.hlsl"
#include "../ShaderLibrary/Shadows.hlsl"
#include "../ShaderLibrary/Light.hlsl"
#include "../ShaderLibrary/BRDF.hlsl"
#include "../ShaderLibrary/GlobalIllumination.hlsl"
#include "../ShaderLibrary/LTCAreaLight.hlsl"

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
	float3 positionWS : TEXCOORD0;
	float3 normalWS : VAR_NORMAL;
	float2 baseUV : TEXCOORD1;
	float4 fogFactor : TEXCOORD2;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

Varyings LitForwardPassVertex (Attributes input) {
	Varyings output;
	UNITY_SETUP_INSTANCE_ID(input);
	UNITY_TRANSFER_INSTANCE_ID(input, output);
	output.positionWS = TransformObjectToWorld(input.positionOS);
	output.positionCS_SS = TransformWorldToHClip(output.positionWS);
	InputConfig config = GetInputConfig(output.positionCS_SS, output.positionWS);
	float3 displacement = GetDisplacement(config);
	float displaceScale = pow(1 - saturate(length(_WorldSpaceCameraPos - output.positionWS) / _DistanceSmoothStart), _DistanceSmoothIntensity);
	output.positionWS += displacement * displaceScale;
	output.positionCS_SS = TransformWorldToHClip(output.positionWS);

	output.fogFactor = ComputeFogFactor(output.positionCS_SS.z);
	output.normalWS = TransformObjectToWorldNormal(input.normalOS);
	output.baseUV = TransformBaseUV(input.baseUV);
	return output;
}

void InitializeSurfaceData(Varyings input, InputConfig config, out Surface surface)
{
	float4 base = GetBase(config);
	surface.position = input.positionWS;
	surface.normalWS = GetNormalWS(config, length(_WorldSpaceCameraPos - input.positionWS));
	surface.interpolatedNormal = surface.normalWS;
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

float3 GetDirectLightBRDF(Light light, Surface surface, BRDF brdf)
{
	if(light.areaData.w > 1e-5)
	{
		float3 ltc_brdf = GetLTCDirectBRDF(surface, brdf.specular, brdf.diffuse, brdf.perceptualRoughness, light);
		ltc_brdf *= (light.color * light.attenuation);
		return ltc_brdf;
	}
	else
	{
		float3 directSpecular = GetDirectBRDF(surface, brdf, light.direction) * brdf.specular + brdf.diffuse;
		float3 directDiffuse = saturate(dot(surface.normalWS, light.direction)) * light.color * light.attenuation;
		
		return directSpecular * directDiffuse;
	}
}

bool RenderingLayersOverlap (Surface surface, Light light) {
	return (surface.renderingLayerMask & light.renderingLayerMask) != 0;
}

float4 LitPassFragment(Varyings input ) : SV_TARGET
{
    UNITY_SETUP_INSTANCE_ID(input);

	InputConfig config = GetInputConfig(input.positionCS_SS, input.positionWS);
    
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
	
	float3 color = 0;//GetIndirectBRDF(surface, brdf, gi.diffuse, gi.specular);
	return float4(surface.color, 1.0);
	for (int i = 0; i < GetDirectionalLightCount(); i++) {
		Light light = GetDirectionalLight(i, surface, shadowData);
		if (RenderingLayersOverlap(surface, light))
		{
			color += GetDirectLightBRDF(light, surface, brdf);
		}
	}
	#if defined(_LIGHTS_PER_OBJECT)
	for (int j = 0; j < min(unity_LightData.y, 8); j++) {
		int lightIndex = unity_LightIndices[(uint)j / 4][(uint)j % 4];
		Light light = GetOtherLight(lightIndex, surface, shadowData);
		if (RenderingLayersOverlap(surface, light))
		{
			color += GetDirectLightBRDF(light, surface, brdf);
		}
	}
	#else
	for (int j = 0; j < GetOtherLightCount(); j++) {
		Light light = GetOtherLight(j, surface, shadowData);
		if (RenderingLayersOverlap(surface, light))
		{
			color += GetDirectLightBRDF(light, surface, brdf);
		}
	}
	#endif
	color = MixFog(color, input.fogFactor);
	return float4(color, GetFinalAlpha(surface.alpha));
}
#endif
