Shader "Hidden/MSRP/PreIntegrateBrdfLUT"
{
    Properties
    {

    }

    HLSLINCLUDE
    #pragma exclude_renderers gles
    #pragma target 3.5
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/Shaders/PostProcessing/Common.hlsl"
    

    struct AttributesPreIntegrateBrdfLUT
    {
        float4 positionOS : POSITION;
        float2 texcoord : TEXCOORD0;
        UNITY_VERTEX_INPUT_INSTANCE_ID
    };

    struct VaryingsPreIntegrateBrdfLUT
    {
        half4 positionCS : SV_POSITION;
        half4 uv : TEXCOORD0;
        UNITY_VERTEX_OUTPUT_STEREO
    };

    VaryingsPreIntegrateBrdfLUT VertexTAA(AttributesPreIntegrateBrdfLUT input)
    {
        VaryingsPreIntegrateBrdfLUT output;
        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

        output.positionCS = TransformObjectToHClip(input.positionOS.xyz);

        float4 projPos = output.positionCS * 0.5;
        projPos.xy = projPos.xy + projPos.w;

        output.uv.xy = UnityStereoTransformScreenSpaceTex(input.texcoord);
        output.uv.zw = projPos.xy;

        return output;
    }

    float3 ImportanceSampleGGX(float2 Xi, float3 N, float roughness)
    {
        float a = roughness * roughness;

        float phi = 2.0 * PI * Xi.x;
        float cosTheta = sqrt((1.0 - Xi.y) / (1.0 + (a * a - 1.0) * Xi.y));
        float sinTheta = sqrt(1.0 - cosTheta * cosTheta);

        // from spherical coordinates to cartesian coordinates
        float3 H;
        H.x = cos(phi) * sinTheta;
        H.y = sin(phi) * sinTheta;
        H.z = cosTheta;

        // from tangent-space floattor to world-space sample floattor
        float3 up = abs(N.z) < 0.999 ? float3(0.0, 0.0, 1.0) : float3(1.0, 0.0, 0.0);
        float3 tangent = normalize(cross(up, N));
        float3 bitangent = cross(N, tangent);

        float3 samplefloat = tangent * H.x + bitangent * H.y + N * H.z;
        return normalize(samplefloat);
    }

    /** Reverses all the 32 bits. */
    uint ReverseBits32(uint bits)
    {
        #if SM5_PROFILE || COMPILER_METAL
	return reversebits( bits );
        #else
        bits = (bits << 16) | (bits >> 16);
        bits = ((bits & 0x00ff00ff) << 8) | ((bits & 0xff00ff00) >> 8);
        bits = ((bits & 0x0f0f0f0f) << 4) | ((bits & 0xf0f0f0f0) >> 4);
        bits = ((bits & 0x33333333) << 2) | ((bits & 0xcccccccc) >> 2);
        bits = ((bits & 0x55555555) << 1) | ((bits & 0xaaaaaaaa) >> 1);
        return bits;
        #endif
    }

    float2 Hammersley(uint Index, uint NumSamples, uint2 Random)
    {
        float E1 = frac((float)Index / NumSamples + float(Random.x & 0xffff) / (1 << 16));
        float E2 = float(ReverseBits32(Index) ^ Random.y) * 2.3283064365386963e-10;
        return float2(E1, E2);
    }

    float GeometrySchlickGGX(float NdotV, float roughness)
    {
        float a = roughness;
        float k = (a * a) / 2.0;

        float nom = NdotV;
        float denom = NdotV * (1.0 - k) + k;

        return nom / denom;
    }

    // ----------------------------------------------------------------------------
    float GeometrySmith(float3 N, float3 V, float3 L, float roughness)
    {
        float NdotV = max(dot(N, V), 0.0);
        float NdotL = max(dot(N, L), 0.0);
        float ggx2 = GeometrySchlickGGX(NdotV, roughness);
        float ggx1 = GeometrySchlickGGX(NdotL, roughness);

        return ggx1 * ggx2;
    }

    float2 IntegrateBRDF(float NdotV, float roughness)
    {
        float3 V;
        V.x = sqrt(1.0 - NdotV * NdotV);
        V.y = 0.0;
        V.z = NdotV;

        float A = 0.0;
        float B = 0.0;

        float3 N = float3(0.0, 0.0, 1.0);

        const uint SAMPLE_COUNT = 1024u;
        [loop]
        for (uint i = 0u; i < SAMPLE_COUNT; ++i)
        {
            float2 Xi = Hammersley(i, SAMPLE_COUNT, 0);
            float3 H = ImportanceSampleGGX(Xi, N, roughness);
            float3 L = normalize(2.0 * dot(V, H) * H - V);

            float NdotL = max(L.z, 0.0);
            float NdotH = max(H.z, 0.0);
            float VdotH = max(dot(V, H), 0.0);

            if (NdotL > 0.0)
            {
                float G = GeometrySmith(N, V, L, roughness);
                float G_Vis = (G * VdotH) / (NdotH * NdotV);
                float Fc = pow(1.0 - VdotH, 5.0);

                A += (1.0 - Fc) * G_Vis;
                B += Fc * G_Vis;
            }
        }
        A /= float(SAMPLE_COUNT);
        B /= float(SAMPLE_COUNT);
        return float2(A, B);
    }

    float4 Frag(VaryingsPreIntegrateBrdfLUT input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        float2 uv = UnityStereoTransformScreenSpaceTex(input.uv);
        float4 color = 0.0;
        color.xy = IntegrateBRDF(uv.x, uv.y);
        return color;
    }
    ENDHLSL

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline"
        }

        Cull Off
        ZTest Always
        ZWrite Off

        Pass
        {
            Name "TAA"

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex VertexTAA
            #pragma fragment Frag
            ENDHLSL
        }
    }
}