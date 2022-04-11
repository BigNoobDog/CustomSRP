#ifndef CUSTOM_GI_INCLUDED
#define CUSTOM_GI_INCLUDED
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/EntityLighting.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/ImageBasedLighting.hlsl"

struct GI
{
    float3 diffuse;
	float3 specular;
	ShadowMask shadowMask;
};

float3 SampleLightMap(float2 lightMapUV)
{
    #if defined(LIGHTMAP_ON)
	return SampleSingleLightmap(
	TEXTURE2D_ARGS(unity_Lightmap, samplerunity_Lightmap),
	lightMapUV,
	float4(1.0, 1.0, 0.0, 0.0),
    #if defined(UNITY_LIGHTMAP_FULL_HDR)
				false,
    #else
				true,
    #endif
			float4(LIGHTMAP_HDR_MULTIPLIER, LIGHTMAP_HDR_EXPONENT, 0.0, 0.0)
	);
    #else
    return 0.0;
    #endif
}

float4 SampleLightProbeOcclusion (Surface surfaceWS) {
	return unity_ProbesOcclusion;
}

float4 SampleBakedShadows (float2 lightMapUV, Surface surface) {
	#if defined(LIGHTMAP_ON)
	return SAMPLE_TEXTURE2D(
		unity_ShadowMask, samplerunity_ShadowMask, lightMapUV
	);
	#else
	if (unity_ProbeVolumeParams.x) {
		return SampleProbeOcclusion(
			TEXTURE3D_ARGS(unity_ProbeVolumeSH, samplerunity_ProbeVolumeSH),
			surface.position, unity_ProbeVolumeWorldToObject,
			unity_ProbeVolumeParams.y, unity_ProbeVolumeParams.z,
			unity_ProbeVolumeMin.xyz, unity_ProbeVolumeSizeInv.xyz
		);
	}
	else {
		return unity_ProbesOcclusion;
	}
	#endif
}

float3 SampleLightProbe (Surface surface) {
	#if defined(LIGHTMAP_ON)
	return 0.0;
	#else
	if (unity_ProbeVolumeParams.x) {
		return SampleProbeVolumeSH4(
			TEXTURE3D_ARGS(unity_ProbeVolumeSH, samplerunity_ProbeVolumeSH),
			surface.position, surface.normalWS,
			unity_ProbeVolumeWorldToObject,
			unity_ProbeVolumeParams.y, unity_ProbeVolumeParams.z,
			unity_ProbeVolumeMin.xyz, unity_ProbeVolumeSizeInv.xyz
		);
	}
	else
	{
		float4 coefficients[7];
		coefficients[0] = unity_SHAr;
		coefficients[1] = unity_SHAg;
		coefficients[2] = unity_SHAb;
		coefficients[3] = unity_SHBr;
		coefficients[4] = unity_SHBg;
		coefficients[5] = unity_SHBb;
		coefficients[6] = unity_SHC;
		return max(0.0, SampleSH9(coefficients, surface.normalWS));
	}
	#endif
}

float3 SampleEnvironment (Surface surfaceWS, BRDF brdf) {

	#if defined(_REFLECTION_PLANARREFLECTION) && defined(_ENABLE_PLANARREFLECTION)
	float2 p11_22 = float2(unity_CameraInvProjection._11, unity_CameraInvProjection._22) * 10;
	// conver the uvs into view space by "undoing" projection
	float3 viewDir = -(float3((surfaceWS.screenUV * 2 - 1) / p11_22, -1));

	half3 viewNormal = mul(surfaceWS.normal, (float3x3)GetWorldToViewMatrix()).xyz;
	half3 reflectVector = reflect(-viewDir, viewNormal);

	half2 reflectionUV = surfaceWS.screenUV + surfaceWS.normal.zx * half2(0.02, 0.1);

	return SAMPLE_TEXTURE2D_LOD(_PlanarReflectionTexture, sampler_linear_clamp, reflectionUV, 6 * brdf.roughness).rgb;//planar reflection
	#else

	
	float3 reflectVector = reflect(-surfaceWS.viewDirectionWS, surfaceWS.normalWS);
	float mip = PerceptualRoughnessToMipmapLevel(brdf.perceptualRoughness);
	float4 environment = SAMPLE_TEXTURECUBE_LOD(unity_SpecCube0, samplerunity_SpecCube0, reflectVector, mip);
	return DecodeHDREnvironment(environment, unity_SpecCube0_HDR) * surfaceWS.occlusion;

	#endif

}

GI GetGI(float2 lightMapUV, Surface surface, BRDF brdf)
{
    GI gi;
    gi.diffuse = SampleLightMap(lightMapUV) + SampleLightProbe(surface);
	gi.specular = SampleEnvironment(surface, brdf);
	gi.shadowMask.always = false;
	gi.shadowMask.distance = false;
	gi.shadowMask.shadows = 1.0;
	#if defined(_SHADOW_MASK_ALWAYS)
	gi.shadowMask.always = true;
	gi.shadowMask.shadows = SampleBakedShadows(lightMapUV, surface);
	#elif defined(_SHADOW_MASK_DISTANCE)
	gi.shadowMask.distance = true;
	gi.shadowMask.shadows = SampleBakedShadows(lightMapUV, surface);
	#endif
    return gi;
}

#endif
