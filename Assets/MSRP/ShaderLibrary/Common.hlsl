#ifndef CUSTOM_COMMON_INCLUDED
#define CUSTOM_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
#include "UnityInput.hlsl"

#define UNITY_MATRIX_M unity_ObjectToWorld
#define UNITY_MATRIX_I_M unity_WorldToObject
#define UNITY_MATRIX_V unity_MatrixV
#define UNITY_MATRIX_VP unity_MatrixVP
#define UNITY_MATRIX_P glstate_matrix_projection

#if defined(_SHADOW_MASK_ALWAYS) || defined(_SHADOW_MASK_DISTANCE)
	#define SHADOWS_SHADOWMASK
#endif

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"

SAMPLER(sampler_linear_clamp);
SAMPLER(sampler_point_clamp);

bool IsOrthographicCamera () {
	return unity_OrthoParams.w;
}

float OrthographicDepthBufferToLinear (float rawDepth) {
	#if UNITY_REVERSED_Z
	rawDepth = 1.0 - rawDepth;
	#endif
	return (_ProjectionParams.z - _ProjectionParams.y) * rawDepth + _ProjectionParams.y;
}

#include "Fragment.hlsl"

float Square (float x) {
	return x * x;
}

float DistanceSquared(float3 pA, float3 pB) {
	return dot(pA - pB, pA - pB);
}

void ClipLOD (Fragment fragment, float fade) {
	#if defined(LOD_FADE_CROSSFADE)
	float dither = InterleavedGradientNoise(fragment.positionSS, 0);
	clip(fade + (fade < 0.0 ? dither : -dither));
	#endif
}

float3 DecodeNormal (float4 sample, float scale) {
	#if defined(UNITY_NO_DXT5nm)
	return UnpackNormalRGB(sample, scale);
	#else
	return UnpackNormalmapRGorAG(sample, scale);
	#endif
}

float3 NormalTangentToWorld (float3 normalTS, float3 normalWS, float4 tangentWS) {
	float3x3 tangentToWorld =
		CreateTangentToWorld(normalWS, tangentWS.xyz, tangentWS.w);
	return TransformTangentToWorld(normalTS, tangentToWorld);
}

real ComputeFogIntensity(real fogFactor)
{
	real fogIntensity = 0.0h;
	#if defined(FOG_LINEAR) || defined(FOG_EXP) || defined(FOG_EXP2)
	#if defined(FOG_EXP)
	// factor = exp(-density*z)
	// fogFactor = density*z compute at vertex
	fogIntensity = saturate(exp2(-fogFactor));
	#elif defined(FOG_EXP2)
	// factor = exp(-(density*z)^2)
	// fogFactor = density*z compute at vertex
	fogIntensity = saturate(exp2(-fogFactor * fogFactor));
	#elif defined(FOG_LINEAR)
	fogIntensity = fogFactor;
	#endif
	#endif
	return fogIntensity;
}

real ComputeFogFactor(float z)
{
	float clipZ_01 = UNITY_Z_0_FAR_FROM_CLIPSPACE(z);

	#if defined(FOG_LINEAR)
	// factor = (end-z)/(end-start) = z * (-1/(end-start)) + (end/(end-start))
	float fogFactor = saturate(clipZ_01 * unity_FogParams.z + unity_FogParams.w);
	return real(fogFactor);
	#elif defined(FOG_EXP) || defined(FOG_EXP2)
	// factor = exp(-(density*z)^2)
	// -density * z computed at vertex
	return real(unity_FogParams.x * clipZ_01);
	#else
	return 0.0h;
	#endif
}

half3 MixFogColor(real3 fragColor, real3 fogColor, real fogFactor)
{
	#if defined(FOG_LINEAR) || defined(FOG_EXP) || defined(FOG_EXP2)
	real fogIntensity = ComputeFogIntensity(fogFactor);
	fragColor = lerp(fogColor, fragColor, fogIntensity);
	#endif
	return fragColor;
}

half3 MixFog(real3 fragColor, real fogFactor)
{
	return MixFogColor(fragColor, unity_FogColor.rgb, fogFactor);
}

#endif