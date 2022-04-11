#ifndef CUSTOM_SURFACE_INCLUDED
#define CUSTOM_SURFACE_INCLUDED

struct Surface {
	float3 position;
	float3 normalWS;
	float3 interpolatedNormal;
	float3 viewDirectionWS;
	float depth;
	float3 color;
	float alpha;
	float metallic;
	float occlusion;
	float smoothness;
	float fresnelStrength;
	float dither;
	uint renderingLayerMask;
	float2 screenUV;
};
#endif