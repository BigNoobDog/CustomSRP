using UnityEngine;
using UnityEngine.Rendering;
using System;
using UnityEngine.Serialization;

namespace MSRP
{
    [Serializable]
    public struct CameraBufferSettings
    {
        public bool allowHDR;

        public bool copyColor, copyColorReflection, copyDepth, copyDepthReflection;

        [Range(CameraRenderer.renderScaleMin, CameraRenderer.renderScaleMax)]
        public float renderScale;

        public enum BicubicRescalingMode
        {
            Off,
            UpOnly,
            UpAndDown
        }

        public BicubicRescalingMode bicubicRescaling;

        [Serializable]
        public struct FXAA
        {
            public bool enabled;

            [Range(0.0312f, 0.0833f)] public float fixedThreshold;

            [Range(0.063f, 0.333f)] public float relativeThreshold;

            [Range(0f, 1f)] public float subpixelBlending;

            public enum Quality
            {
                Low,
                Medium,
                High
            }

            public Quality quality;
        }

        public FXAA fxaa;
    }
    
    
    [Serializable]
    public struct PipelineLightSetting
    {
        [FormerlySerializedAs("areaLightLut")] public ComputeShader areaLightLutCS;
        
        public Texture2D matLUT;

        public Texture2D ampLUT;
        
        public Material AreaLightMat;
    }
    
    
    [Serializable]
    public enum ResolutionMulltiplier
    {
        Full,
        Half,
        Third,
        Quarter
    }
    
    [Serializable]
    public enum RenderPipelineType
    {
        Forward,
        Deferred
    }
    
    [Serializable]
    public class PlanarReflectionSettings
    {
        public bool m_Enable = false;
        public ResolutionMulltiplier m_ResolutionMultiplier = ResolutionMulltiplier.Third;
        public float m_ClipPlaneOffset = 0.07f;
        public LayerMask m_ReflectLayers = -1;
        public bool m_Shadows;
        public bool followZeroPlan = true;
    }
    
    
    [Serializable]
    public class FFTOceanSettings
    {
        public bool enable;
        public float SpaceSize;
        public int N;
        public float AMPScale;
        [Range(5, 5000)]
        public float Depth;
        public float WindDir;
        public float WindSpeed;
        public float WindBias;
        public float WindMove;
        public float WindMink;
        public float WaveChop;
        public bool Loop;
        public float LoopPeriod;
        public Texture2D GaussianTexture;
        public ComputeShader OceanSimulationCS;
        public Shader OceanRenderShader;
        public GameObject OceanMesh;
    }

    [CreateAssetMenu(menuName = "Rendering/Custom Render Pipeline")]
    public partial class CustomRenderPipelineAsset : RenderPipelineAsset
    {
        [SerializeField] private RenderPipelineType renderPipelineType;
        
        [SerializeField] CameraBufferSettings cameraBuffer = new CameraBufferSettings
        {
            allowHDR = true,
            renderScale = 1f,
            fxaa = new CameraBufferSettings.FXAA
            {
                fixedThreshold = 0.0833f,
                relativeThreshold = 0.166f,
                subpixelBlending = 0.75f
            }
        };

        [SerializeField] bool
            useDynamicBatching = true,
            useGPUInstancing = true,
            useSRPBatcher = true,
            useLightsPerObject = true;

        [SerializeField] ShadowSettings shadows = default;

        [FormerlySerializedAs("postFXSettings")] [SerializeField] PostProcessingSetting postProcessingSettings = default;

        [SerializeField] PlanarReflectionSettings planarReflectionSettings = default;

        public enum ColorLUTResolution
        {
            _16 = 16,
            _32 = 32,
            _64 = 64
        }

        [SerializeField] ColorLUTResolution colorLUTResolution = ColorLUTResolution._32;

        [SerializeField] Shader cameraRendererShader = default;

        [FormerlySerializedAs("lightSetting")] [SerializeField] PipelineLightSetting pipelineLightSetting;

        protected override RenderPipeline CreatePipeline()
        {
            return new CustomRenderPipeline(renderPipelineType, 
                cameraBuffer, useDynamicBatching, useGPUInstancing, useSRPBatcher,
                useLightsPerObject, shadows, postProcessingSettings, (int) colorLUTResolution,
                cameraRendererShader, pipelineLightSetting, planarReflectionSettings
            );
        }
    }
}