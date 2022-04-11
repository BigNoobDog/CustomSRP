#ifndef CUSTOM_BRDF_INCLUDED
#define CUSTOM_BRDF_INCLUDED


struct BRDF
{
    float3 diffuse;
    float3 specular;
    float roughness;
    float perceptualRoughness;
    float fresnel;
};

#define MIN_REFLECTIVITY 0.04

float Pow5(float a)
{
    return pow(a, 5);
}

float OneMinusReflectivity(float metallic)
{
    float range = 1.0 - MIN_REFLECTIVITY;
    return range - metallic * range;
}

// GGX / Trowbridge-Reitz
// [Walter et al. 2007, "Microfacet models for refraction through rough surfaces"]
float D_GGX_UE( float a2, float NoH )
{
    float t = 1.0 + (a2 - 1.0) * NoH * NoH;
    return a2 / (PI * t * t);
}

// Appoximation of joint Smith term for GGX
// [Heitz 2014, "Understanding the Masking-Shadowing Function in Microfacet-Based BRDFs"]
float Vis_SmithJointApprox_UE( float a2, float NoV, float NoL )
{
    float a = sqrt(a2);
    float Vis_SmithV = NoL * ( NoV * ( 1 - a ) + a );
    float Vis_SmithL = NoV * ( NoL * ( 1 - a ) + a );
    return 0.5 * rcp( Vis_SmithV + Vis_SmithL );
}

// [Schlick 1994, "An Inexpensive BRDF Model for Physically-Based Rendering"]
float3 F_Schlick_UE( float3 SpecularColor, float VoH )
{
    float Fc = Pow5( 1 - VoH );					// 1 sub, 3 mul
    //return Fc + (1 - Fc) * SpecularColor;		// 1 add, 3 mad
	
    // Anything less than 2% is physically impossible and is instead considered to be shadowing
    return saturate( 50.0 * SpecularColor.g ) * Fc + (1 - Fc) * SpecularColor;
	
}

void InitializeBRDFData(Surface surface, out BRDF brdf, bool applyAlphaToDiffuse = false)
{
    float oneMinusReflectivity = OneMinusReflectivity(surface.metallic);
    brdf.diffuse = surface.color * oneMinusReflectivity;
    if (applyAlphaToDiffuse)
    {
        brdf.diffuse *= surface.alpha;
    }
    brdf.specular = lerp(MIN_REFLECTIVITY, surface.color, surface.metallic);
    brdf.perceptualRoughness =
        PerceptualSmoothnessToPerceptualRoughness(surface.smoothness);
    brdf.roughness = PerceptualRoughnessToRoughness(brdf.perceptualRoughness);
    brdf.fresnel = saturate(surface.smoothness + 1.0 - oneMinusReflectivity);
}

float3 GetIndirectBRDF(
    Surface surface, BRDF brdf, float3 diffuse, float3 specular
)
{
    float fresnelStrength = surface.fresnelStrength *
        Pow4(1.0 - saturate(dot(surface.normalWS, surface.viewDirectionWS)));
    float3 reflection = specular * lerp(brdf.specular, brdf.fresnel, fresnelStrength);
    reflection /= brdf.roughness * brdf.roughness + 1.0;
    return diffuse * brdf.diffuse + reflection;
}

float3 GetDirectBRDF(Surface surface, BRDF brdf, float3 lightDirWS)
{
    float a2 = brdf.roughness * brdf.roughness;
    float NoV = saturate(dot(surface.normalWS, surface.viewDirectionWS));
    float NoL = saturate(dot(surface.normalWS, normalize(lightDirWS)));
    float VoL = saturate(dot(surface.viewDirectionWS, normalize(lightDirWS)));
    float3 halfVector = normalize(surface.viewDirectionWS + normalize(lightDirWS));
    float NoH = saturate(dot(surface.normalWS, halfVector));
    float VoH = saturate(dot(surface.viewDirectionWS, halfVector));
    
    float D = D_GGX_UE(a2, NoH);
    float Vis = Vis_SmithJointApprox_UE(a2, NoV, NoL);
    float3 F = F_Schlick_UE(brdf.specular, VoH);
    return (D * Vis) * F;
}

#endif
