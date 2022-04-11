#ifndef CUSTOM_VELOCITY_BUFFER_INCLUDED
#define CUSTOM_VELOCITY_BUFFER_INCLUDED

float4x4 _PreviousGPUViewProjection;
float2 _PreviousJitterOffset;
float2 _CurrentJutterOffset;


struct Attributes
{
    float4 positionOS : POSITION;
    float2 baseUV : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 positionCS_SS : SV_POSITION;
    float3 positionWS : TEXCOORD1;
    float4 positionOS : TEXCOORD2;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

Varyings vert_velocity(Attributes input)
{
    Varyings output;

    output.positionOS = input.positionOS;
    output.positionWS = TransformObjectToWorld(input.positionOS);
    output.positionCS_SS = TransformWorldToHClip(output.positionWS);
    return output;
}

float2 frag_velocity(Varyings input) : SV_TARGET
{
    Fragment fragment = GetFragment(input.positionCS_SS);

    //float depth = tex2D(_CameraDepthTex, i.uv.xy).r;
    float3 worldPos = mul(unity_MatrixPreviousM, float4(input.positionOS.xyz, 1));
    if (unity_MotionVectorsParams.y == 0)
        worldPos = input.positionWS;
    float4 pClip = mul(_PreviousGPUViewProjection, float4(worldPos.xyz, 1));
    pClip /= pClip.w;
    float2 currentScreenPos = fragment.screenUV;
    float2 previousScreenPos = pClip * .5 + .5;
    //float2 jitterOffset = (_CurrentJutterOffset - _PreviousJitterOffset) * (_ScreenParams.zw - 1);
    //_PreviousJitterOffset.y *= -1;
    //_CurrentJutterOffset.y *= -1;
    previousScreenPos += _PreviousJitterOffset * float2(1, 1) * (_ScreenParams.zw - 1);
    currentScreenPos += _CurrentJutterOffset * float2(1, 1) * (_ScreenParams.zw - 1);
    return (currentScreenPos - previousScreenPos);
}
#endif
