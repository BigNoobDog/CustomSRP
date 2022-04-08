#ifndef CUSTOM_LIT_INPUT_INCLUDED
#define CUSTOM_LIT_INPUT_INCLUDED

TEXTURE2D(_BaseMap);
TEXTURE2D(_MaskMap);
TEXTURE2D(_EmissionMap);
SAMPLER(sampler_BaseMap);

TEXTURE2D(_DisplacementMap);
TEXTURE2D(_NormalMap);
TEXTURE2D(_BubblesMap);
TEXTURE2D(_FormMap);
SAMPLER(sampler_DisplacementMap);
SAMPLER(sampler_NormalMap);
SAMPLER(sampler_BubblesMap);
SAMPLER(sampler_FormMap);

UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
	UNITY_DEFINE_INSTANCED_PROP(float4, _BaseMap_ST)
	UNITY_DEFINE_INSTANCED_PROP(float4, _DisplacementMap_ST)
	UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
	UNITY_DEFINE_INSTANCED_PROP(float4, _ShallowColor)
	UNITY_DEFINE_INSTANCED_PROP(float4, _DepthColor)
	UNITY_DEFINE_INSTANCED_PROP(float, _SpaceSize)
	UNITY_DEFINE_INSTANCED_PROP(float, _AMPScale)
	UNITY_DEFINE_INSTANCED_PROP(float, _DistanceSmoothIntensity)
	UNITY_DEFINE_INSTANCED_PROP(float, _DistanceSmoothStart)
	UNITY_DEFINE_INSTANCED_PROP(float, _Cutoff)
	UNITY_DEFINE_INSTANCED_PROP(float, _ZWrite)
	UNITY_DEFINE_INSTANCED_PROP(float, _Metallic)
	UNITY_DEFINE_INSTANCED_PROP(float, _Occlusion)
	UNITY_DEFINE_INSTANCED_PROP(float, _Smoothness)
	UNITY_DEFINE_INSTANCED_PROP(float, _Fresnel)
UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

#define INPUT_PROP(name) UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, name)

struct InputConfig
{
	Fragment fragment;
	float2 baseUV;
	float2 detailUV;
	bool useMask;
	bool useDetail;
};

InputConfig GetInputConfig (float4 positionSS, float3 positionWS, float2 detailUV = 0.0)
{
	InputConfig c;
	c.fragment = GetFragment(positionSS);
	c.baseUV = float2(positionWS.xz) / _SpaceSize;
	return c;
}

float2 TransformBaseUV (float2 baseUV)
{
	float4 baseST = INPUT_PROP(_BaseMap_ST);
	return baseUV * baseST.xy + baseST.zw;
}

float GetBubbles(InputConfig c)
{
	float3 bubbles = SAMPLE_TEXTURE2D(_BubblesMap, sampler_BubblesMap, c.baseUV);
	return bubbles;
}

float4 GetBase (InputConfig c)
{
	float4 color = INPUT_PROP(_BaseColor);
	float bubbles = GetBubbles(c);
	float4 forms =  SAMPLE_TEXTURE2D(_FormMap, sampler_FormMap, c.baseUV);
	color = lerp(color, 1, length(forms.xyz) * bubbles);
	color.rgb = bubbles.xxx;
	return color;
}

float3 GetDisplacement(InputConfig c)
{
	float3 displacement = SAMPLE_TEXTURE2D_LOD(_DisplacementMap, sampler_DisplacementMap, c.baseUV, 0);
	return displacement;
}

float GetFinalAlpha (float alpha)
{
	return INPUT_PROP(_ZWrite) ? 1.0 : alpha;
}

float3 GetNormalWS (InputConfig c, float distance)
{
	float3 normal = SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, c.baseUV);
	
	normal = lerp(float3(0, 1, 0), normal, pow(1 - saturate(distance / _DistanceSmoothStart), _DistanceSmoothIntensity));
	normal = normalize(normal);
	return normal;
}

float GetCutoff (InputConfig c)
{
	return INPUT_PROP(_Cutoff);
}

float GetMetallic (InputConfig c)
{
	float metallic = INPUT_PROP(_Metallic);
	return metallic;
}

float GetOcclusion (InputConfig c)
{
	float strength = INPUT_PROP(_Occlusion);
	return strength;
}

float GetSmoothness (InputConfig c)
{
	float smoothness = INPUT_PROP(_Smoothness);
	
	return smoothness;
}

float GetFresnel (InputConfig c)
{
	return INPUT_PROP(_Fresnel);
}

#endif