#ifndef CUSTOM_LTCAREALIGHT_INCLUDED
#define CUSTOM_LTCAREALIGHT_INCLUDED

TEXTURE2D_ARRAY(_LtcData); // We pack all Ltc data inside one texture array to limit the number of resource used

#define LTC_GGX_MATRIX_INDEX 0 // RGBA
#define LTC_DISNEY_DIFFUSE_MATRIX_INDEX 1 // RGBA

#define LTC_LUT_SIZE   64
#define LTC_LUT_SCALE  ((LTC_LUT_SIZE - 1) * rcp(LTC_LUT_SIZE))
#define LTC_LUT_OFFSET (0.5 * rcp(LTC_LUT_SIZE))

struct FRect
{
    float3 Origin;
    float3x3 Axis;
    float2 Extent;
};

real3 ComputeEdgeFactor(real3 V1, real3 V2)
{
    real V1oV2 = dot(V1, V2);
    real3 V1xV2 = cross(V1, V2);
    #if 0
    return normalize(V1xV2) * acos(V1oV2));
    #else
    // Approximate: { y = rsqrt(1.0 - V1oV2 * V1oV2) * acos(V1oV2) } on [0, 1].
    // Fit: HornerForm[MiniMaxApproximation[ArcCos[x]/Sqrt[1 - x^2], {x, {0, 1 - $MachineEpsilon}, 6, 0}][[2, 1]]].
    // Maximum relative error: 2.6855360216340534 * 10^-6. Intensities up to 1000 are artifact-free.
    real x = abs(V1oV2);
    real y = 1.5707921083647782 + x * (-0.9995697178013095 + x * (0.778026455830408 + x * (-0.6173111361273548 + x * (
        0.4202724111150622 + x * (-0.19452783598217288 + x * 0.04232040013661036)))));

    if (V1oV2 < 0)
    {
        // Undo range reduction.
        const float epsilon = 1e-5f;
        y = PI * rsqrt(max(epsilon, saturate(1 - V1oV2 * V1oV2))) - y;
    }

    return V1xV2 * y;
    #endif
}

float IntegrateEdge(float3 v1, float3 v2)
{
    return ComputeEdgeFactor(v1, v2).z;
}

// 'sinSqSigma' is the sine^2 of the half-angle subtended by the sphere (aperture) as seen from the shaded point.
// 'cosOmega' is the cosine of the angle between the normal and the direction to the center of the light.
// N.b.: this function accounts for horizon clipping.
real DiffuseSphereLightIrradiance(real sinSqSigma, real cosOmega)
{
    #ifdef APPROXIMATE_SPHERE_LIGHT_NUMERICALLY
    real x = sinSqSigma;
    real y = cosOmega;

    // Use a numerical fit found in Mathematica. Mean absolute error: 0.00476944.
    // You can use the following Mathematica code to reproduce our results:
    // t = Flatten[Table[{x, y, f[x, y]}, {x, 0, 0.999999, 0.001}, {y, -0.999999, 0.999999, 0.002}], 1]
    // m = NonlinearModelFit[t, x * (y + e) * (0.5 + (y - e) * (a + b * x + c * x^2 + d * x^3)), {a, b, c, d, e}, {x, y}]
    return saturate(x * (0.9245867471551246 + y) * (0.5 + (-0.9245867471551246 + y) * (0.5359050373687144 + x * (-1.0054221851257754 + x * (1.8199061187417047 - x * 1.3172081704209504)))));
    #else
    #if 0 // Ref: Area Light Sources for Real-Time Graphics, page 4 (1996).
        real sinSqOmega = saturate(1 - cosOmega * cosOmega);
        real cosSqSigma = saturate(1 - sinSqSigma);
        real sinSqGamma = saturate(cosSqSigma / sinSqOmega);
        real cosSqGamma = saturate(1 - sinSqGamma);

        real sinSigma = sqrt(sinSqSigma);
        real sinGamma = sqrt(sinSqGamma);
        real cosGamma = sqrt(cosSqGamma);

        real sigma = asin(sinSigma);
        real omega = acos(cosOmega);
        real gamma = asin(sinGamma);

        if (omega >= HALF_PI + sigma)
        {
            // Full horizon occlusion (case #4).
            return 0;
        }

        real e = sinSqSigma * cosOmega;

        UNITY_BRANCH
        if (omega < HALF_PI - sigma)
        {
            // No horizon occlusion (case #1).
            return e;
        }
        else
        {
            real g = (-2 * sqrt(sinSqOmega * cosSqSigma) + sinGamma) * cosGamma + (HALF_PI - gamma);
            real h = cosOmega * (cosGamma * sqrt(saturate(sinSqSigma - cosSqGamma)) + sinSqSigma * asin(saturate(cosGamma / sinSigma)));

            if (omega < HALF_PI)
            {
                // Partial horizon occlusion (case #2).
                return saturate(e + INV_PI * (g - h));
            }
            else
            {
                // Partial horizon occlusion (case #3).
                return saturate(INV_PI * (g + h));
            }
        }
    #else // Ref: Moving Frostbite to Physically Based Rendering, page 47 (2015, optimized).
    real cosSqOmega = cosOmega * cosOmega; // y^2

    UNITY_BRANCH
    if (cosSqOmega > sinSqSigma) // (y^2)>x
    {
        return saturate(sinSqSigma * cosOmega); // Clip[x*y,{0,1}]
    }
    else
    {
        real cotSqSigma = rcp(sinSqSigma) - 1; // 1/x-1
        real tanSqSigma = rcp(cotSqSigma); // x/(1-x)
        real sinSqOmega = 1 - cosSqOmega; // 1-y^2

        real w = sinSqOmega * tanSqSigma; // (1-y^2)*(x/(1-x))
        real x = -cosOmega * rsqrt(w); // -y*Sqrt[(1/x-1)/(1-y^2)]
        real y = sqrt(sinSqOmega * tanSqSigma - cosSqOmega); // Sqrt[(1-y^2)*(x/(1-x))-y^2]
        real z = y * cotSqSigma; // Sqrt[(1-y^2)*(x/(1-x))-y^2]*(1/x-1)

        real a = cosOmega * acos(x) - z; // y*ArcCos[-y*Sqrt[(1/x-1)/(1-y^2)]]-Sqrt[(1-y^2)*(x/(1-x))-y^2]*(1/x-1)
        real b = atan(y); // ArcTan[Sqrt[(1-y^2)*(x/(1-x))-y^2]]

        return saturate(INV_PI * (a * sinSqSigma + b));
    }
    #endif
    #endif
}

real PolygonIrradiance(real4x3 L, out float3 F)
{
    UNITY_UNROLL
    for (uint i = 0; i < 4; i++)
    {
        L[i] = normalize(L[i]);
    }

    F = 0.0;

    UNITY_UNROLL
    for (uint edge = 0; edge < 4; edge++)
    {
        real3 V1 = L[edge];
        real3 V2 = L[(edge + 1) % 4];

        F += INV_TWO_PI * ComputeEdgeFactor(V1, V2);
    }

    // Clamp invalid values to avoid visual artifacts.
    real f2 = saturate(dot(F, F));
    real sinSqSigma = min(sqrt(f2), 0.999);
    real cosOmega = clamp(F.z * rsqrt(f2), -1, 1);

    return DiffuseSphereLightIrradiance(sinSqSigma, cosOmega);
}

// Expects non-normalized vertex positions.
real PolygonIrradiance(real4x3 L)
{
    // 1. ClipQuadToHorizon

    // detect clipping config
    uint config = 0;
    if (L[0].z > 0) config += 1;
    if (L[1].z > 0) config += 2;
    if (L[2].z > 0) config += 4;
    if (L[3].z > 0) config += 8;

    // The fifth vertex for cases when clipping cuts off one corner.
    // Due to a compiler bug, copying L into a vector array with 5 rows
    // messes something up, so we need to stick with the matrix + the L4 vertex.
    real3 L4 = L[3];

    // This switch is surprisingly fast. Tried replacing it with a lookup array of vertices.
    // Even though that replaced the switch with just some indexing and no branches, it became
    // way, way slower - mem fetch stalls?

    // clip
    uint n = 0;
    switch (config)
    {
    case 0: // clip all
        break;

    case 1: // V1 clip V2 V3 V4
        n = 3;
        L[1] = -L[1].z * L[0] + L[0].z * L[1];
        L[2] = -L[3].z * L[0] + L[0].z * L[3];
        break;

    case 2: // V2 clip V1 V3 V4
        n = 3;
        L[0] = -L[0].z * L[1] + L[1].z * L[0];
        L[2] = -L[2].z * L[1] + L[1].z * L[2];
        break;

    case 3: // V1 V2 clip V3 V4
        n = 4;
        L[2] = -L[2].z * L[1] + L[1].z * L[2];
        L[3] = -L[3].z * L[0] + L[0].z * L[3];
        break;

    case 4: // V3 clip V1 V2 V4
        n = 3;
        L[0] = -L[3].z * L[2] + L[2].z * L[3];
        L[1] = -L[1].z * L[2] + L[2].z * L[1];
        break;

    case 5: // V1 V3 clip V2 V4: impossible
        break;

    case 6: // V2 V3 clip V1 V4
        n = 4;
        L[0] = -L[0].z * L[1] + L[1].z * L[0];
        L[3] = -L[3].z * L[2] + L[2].z * L[3];
        break;

    case 7: // V1 V2 V3 clip V4
        n = 5;
        L4 = -L[3].z * L[0] + L[0].z * L[3];
        L[3] = -L[3].z * L[2] + L[2].z * L[3];
        break;

    case 8: // V4 clip V1 V2 V3
        n = 3;
        L[0] = -L[0].z * L[3] + L[3].z * L[0];
        L[1] = -L[2].z * L[3] + L[3].z * L[2];
        L[2] = L[3];
        break;

    case 9: // V1 V4 clip V2 V3
        n = 4;
        L[1] = -L[1].z * L[0] + L[0].z * L[1];
        L[2] = -L[2].z * L[3] + L[3].z * L[2];
        break;

    case 10: // V2 V4 clip V1 V3: impossible
        break;

    case 11: // V1 V2 V4 clip V3
        n = 5;
        L[3] = -L[2].z * L[3] + L[3].z * L[2];
        L[2] = -L[2].z * L[1] + L[1].z * L[2];
        break;

    case 12: // V3 V4 clip V1 V2
        n = 4;
        L[1] = -L[1].z * L[2] + L[2].z * L[1];
        L[0] = -L[0].z * L[3] + L[3].z * L[0];
        break;

    case 13: // V1 V3 V4 clip V2
        n = 5;
        L[3] = L[2];
        L[2] = -L[1].z * L[2] + L[2].z * L[1];
        L[1] = -L[1].z * L[0] + L[0].z * L[1];
        break;

    case 14: // V2 V3 V4 clip V1
        n = 5;
        L4 = -L[0].z * L[3] + L[3].z * L[0];
        L[0] = -L[0].z * L[1] + L[1].z * L[0];
        break;

    case 15: // V1 V2 V3 V4
        n = 4;
        break;
    }

    if (n == 0) return 0;

    // 2. Project onto sphere
    L[0] = normalize(L[0]);
    L[1] = normalize(L[1]);
    L[2] = normalize(L[2]);

    switch (n)
    {
    case 3:
        L[3] = L[0];
        break;
    case 4:
        L[3] = normalize(L[3]);
        L4 = L[0];
        break;
    case 5:
        L[3] = normalize(L[3]);
        L4 = normalize(L4);
        break;
    }

    // 3. Integrate
    real sum = 0;
    sum += IntegrateEdge(L[0], L[1]);
    sum += IntegrateEdge(L[1], L[2]);
    sum += IntegrateEdge(L[2], L[3]);
    if (n >= 4)
        sum += IntegrateEdge(L[3], L4);
    if (n == 5)
        sum += IntegrateEdge(L4, L[0]);

    sum *= INV_TWO_PI; // Normalization

    sum = max(sum, 0.0);

    return isfinite(sum) ? sum : 0.0;
}

// float3 SampleSourceTexture( float3 L, FRect Rect, FRectTexture RectTexture)
// {
//     #if USE_SOURCE_TEXTURE
//     // Force to point at plane
//     L += Rect.Axis[2] * saturate( 0.001 - dot( Rect.Axis[2], L ) );
//
//     // Intersect ray with plane
//     float DistToPlane = dot( Rect.Axis[2], Rect.Origin ) / dot( Rect.Axis[2], L );
//     float3 PointOnPlane = L * DistToPlane;
//
//     float2 PointInRect;
//     PointInRect.x = dot( Rect.Axis[0], PointOnPlane - Rect.Origin );
//     PointInRect.y = dot( Rect.Axis[1], PointOnPlane - Rect.Origin );
//
//     // Compute UV on the original rect (i.e. unoccluded rect)
//     float2 RectUV = (PointInRect + Rect.Offset) / Rect.FullExtent * float2(0.5, -0.5) + 0.5;
// 	
//     float Level = log2( DistToPlane * rsqrt( Rect.FullExtent.x * Rect.FullExtent.y ) );
//
//     return SampleRectTexture(RectTexture, RectUV, Level);
//     #else
//     return 1;
//     #endif
// }

float3 GetLTCDirectBRDF(Surface surface, float3 specularColor, float3 diffuseColor, float perceptualRoughness, Light light)
{
    FRect Rect;
    Rect.Origin = light.position;
    Rect.Axis[0] = -light.xAxis;
    Rect.Axis[1] = light.yAxis;
    Rect.Axis[2] = cross(light.xAxis, light.yAxis);
    Rect.Extent = float2(light.areaData.zw);

    float3 N = normalize(surface.normal);
    float3 V = surface.viewDirection;
    float3 pos = surface.position;
    float clampedNdotV = ClampNdotV(dot(N, V));

    float theta = FastACosPos(clampedNdotV); // For Area light - UVs for sampling the LUTs
    float2 UV = Remap01ToHalfTexelCoord(float2(perceptualRoughness, theta * INV_HALF_PI), LTC_LUT_SIZE);

    float4 LTCGGX = SAMPLE_TEXTURE2D_ARRAY_LOD(_LtcData, sampler_linear_clamp, UV, LTC_GGX_MATRIX_INDEX, 0);
    float4 LTCDIFFUSE = SAMPLE_TEXTURE2D_ARRAY_LOD(_LtcData, sampler_linear_clamp, UV, LTC_DISNEY_DIFFUSE_MATRIX_INDEX, 0);

    float3x3 ltcTransformSpecular = 0;
    ltcTransformSpecular[2][2] = 1.0;
    ltcTransformSpecular[0][0] = LTCGGX.x;
    ltcTransformSpecular[0][2] = LTCGGX.y;
    ltcTransformSpecular[1][1] = LTCGGX.z;
    ltcTransformSpecular[2][0] = LTCGGX.w;
    

    float4x3 lightVertexs;
    lightVertexs[0] = Rect.Origin - Rect.Axis[0] * Rect.Extent.x - Rect.Axis[1] * Rect.Extent.y - pos;
    lightVertexs[1] = Rect.Origin + Rect.Axis[0] * Rect.Extent.x - Rect.Axis[1] * Rect.Extent.y - pos;
    lightVertexs[2] = Rect.Origin + Rect.Axis[0] * Rect.Extent.x + Rect.Axis[1] * Rect.Extent.y - pos;
    lightVertexs[3] = Rect.Origin - Rect.Axis[0] * Rect.Extent.x + Rect.Axis[1] * Rect.Extent.y - pos;

    // construct orthonormal basis around N
    float3 T1, T2;
    T1 = normalize(V - N * saturate(dot(V, N)));
    T2 = cross(N, T1);

    float3x3 orthoBasisViewNormal = float3x3(T1, T2, N);

    lightVertexs = mul(lightVertexs, transpose(orthoBasisViewNormal));

    float3 ltcValue;

    float3 formFactorD;

    float4x3 LD = mul(lightVertexs, k_identity3x3);

    ltcValue = PolygonIrradiance(LD, formFactorD);

    float3 diffuse = ltcValue;

    float4x3 LS = mul(lightVertexs, ltcTransformSpecular);
    
    float3 formFactorS;
    
    ltcValue = PolygonIrradiance(LS);

    float3 specular = ltcValue;
    
    return specular * specularColor + diffuseColor * diffuse;
}

#endif