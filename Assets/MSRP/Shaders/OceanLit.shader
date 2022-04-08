﻿Shader "MSRP/OceanLit"
{
    Properties {
		_BaseColor("BaseColor", Color) = (1.0, 1.0, 1.0, 1.0)
		_ShallowColor("ShallowColor", Color) = (1.0, 1.0, 1.0, 1.0)
		_DepthColor("DepthColor", Color) = (1.0, 1.0, 1.0, 1.0)
    	
    	_DistanceSmoothIntensity("DistanceSmoothIntensity", Float) = 0
    	_DistanceSmoothStart("DistanceSmoothStart", Float) = 0
    	
		_DisplacementMap("DisplacementMap", 2D) = "white" {}
		_NormalMap("NormalMap", 2D) = "green" {}
		_BubblesMap("BubblesMap", 2D) = "white" {}
		_FormMap("FormMap", 2D) = "white" {}
    	
		[Toggle(_CLIPPING)] _Clipping ("Alpha Clipping", Float) = 0
		[Toggle(_RECEIVE_SHADOWS)] _ReceiveShadows ("Receive Shadows", Float) = 1
    	
		_Metallic ("Metallic", Range(0, 1)) = 0
		_Smoothness ("Smoothness", Range(0, 1)) = 0.5
		_Fresnel ("Fresnel", Range(0, 1)) = 1
		
		[Toggle(_PREMULTIPLY_ALPHA)] _PremulAlpha ("Premultiply Alpha", Float) = 0
    	
		[Toggle(_ENABLE_PLANARREFLECTION)] _EnablePlanarReflection ("Planar Reflection", Float) = 0

		[Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 1
		[Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 0
		[Enum(Off, 0, On, 1)] _ZWrite ("Z Write", Float) = 1

		[HideInInspector] _MainTex("Texture for Lightmap", 2D) = "white" {}
		[HideInInspector] _Color("Color for Lightmap", Color) = (0.5, 0.5, 0.5, 1.0)
	}
	
	SubShader {
		HLSLINCLUDE
		ENDHLSL

		Pass {
			Tags {
				"LightMode" = "CustomLit"
			}

			Blend [_SrcBlend] [_DstBlend], One OneMinusSrcAlpha
			ZWrite [_ZWrite]

			HLSLPROGRAM
			#pragma target 3.5
			#pragma shader_feature _CLIPPING
			#pragma shader_feature _RECEIVE_SHADOWS
			#pragma shader_feature _PREMULTIPLY_ALPHA
			#pragma shader_feature _MASK_MAP
			#pragma shader_feature _NORMAL_MAP
			#pragma shader_feature _DETAIL_MAP
			#pragma shader_feature _ENABLE_PLANARREFLECTION
			#pragma multi_compile _ _REFLECTION_PLANARREFLECTION
			#pragma multi_compile _ _DIRECTIONAL_PCF3 _DIRECTIONAL_PCF5 _DIRECTIONAL_PCF7
			#pragma multi_compile _ _OTHER_PCF3 _OTHER_PCF5 _OTHER_PCF7
			#pragma multi_compile _ _CASCADE_BLEND_SOFT _CASCADE_BLEND_DITHER
			#pragma multi_compile _ _SHADOW_MASK_ALWAYS _SHADOW_MASK_DISTANCE
			#pragma multi_compile _ _LIGHTS_PER_OBJECT
			#pragma multi_compile _ LIGHTMAP_ON
			#pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_fog
			#pragma multi_compile_instancing
			#pragma vertex LitPassVertex
			#pragma fragment LitPassFragment
			#include "OceanLitPass.hlsl"
			ENDHLSL
		}

//		Pass {
//			Tags {
//				"LightMode" = "ShadowCaster"
//			}
//
//			ColorMask 0
//			
//			HLSLPROGRAM
//			#pragma target 3.5
//			#pragma shader_feature _ _SHADOWS_CLIP _SHADOWS_DITHER
//			#pragma multi_compile _ LOD_FADE_CROSSFADE
//			#pragma multi_compile_instancing
//			#pragma vertex ShadowCasterPassVertex
//			#pragma fragment ShadowCasterPassFragment
//			
//		#include "../ShaderLibrary/Common.hlsl"
//		#include "LitInput.hlsl"
//			#include "ShadowCasterPass.hlsl"
//			ENDHLSL
//		}
//
//		Pass {
//			Tags {
//				"LightMode" = "Meta"
//			}
//
//			Cull Off
//
//			HLSLPROGRAM
//			#pragma target 3.5
//			#pragma vertex MetaPassVertex
//			#pragma fragment MetaPassFragment
//			
//		#include "../ShaderLibrary/Common.hlsl"
//		#include "LitInput.hlsl"
//			#include "MetaPass.hlsl"
//			ENDHLSL
//		}
	}
	CustomEditor "MSRP.Editor.CustomShaderGUI"
}
