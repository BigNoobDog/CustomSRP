using UnityEngine;
using UnityEngine.Rendering;
using System;
using UnityEngine.Serialization;

namespace MSRP
{
    public enum MotionBlurQuality
    {
        Low,
        Medium,
        High
    }
    
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
        
        public enum AntiAliasingType
        {
            None,
            FXAA,
            TAA
        }

        public BicubicRescalingMode bicubicRescaling;

        [Serializable]
        public struct FXAASetting
        {
            [HideInInspector]
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
        
        [Serializable]
        public struct TAASetting
        {
            public MotionBlurQuality quality;
            
            [Range(0.0f, 1.0f)] public float feedback;

            [Range(0.0f, 1.0f)] public float spread;
            
            [HideInInspector]
            public bool IsActive() => feedback > 0.0f;
        }

        public AntiAliasingType antiAliasingType;

        public FXAASetting fxaaSetting;

        public TAASetting taaSetting;
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

    public enum ColorLUTResolution
    {
        _16 = 16,
        _32 = 32,
        _64 = 64
    }

    [CreateAssetMenu(menuName = "Rendering/Custom Render Pipeline")]
    public partial class CustomRenderPipelineAsset : RenderPipelineAsset
    {
        [SerializeField]
        public RenderPiplineData data;

        protected override RenderPipeline CreatePipeline()
        {
            return new CustomRenderPipeline(data);
        }
    }


    [Serializable]
    public class RenderPiplineData
    {
        [SerializeField] public RenderPipelineType renderPipelineType;

        [SerializeField] public CameraBufferSettings cameraBuffer = new CameraBufferSettings
        {
            allowHDR = true,
            renderScale = 1f,
            fxaaSetting = new CameraBufferSettings.FXAASetting
            {
                fixedThreshold = 0.0833f,
                relativeThreshold = 0.166f,
                subpixelBlending = 0.75f
            }
        };

        [SerializeField] public bool
            useDynamicBatching = true,
            useGPUInstancing = true,
            useSRPBatcher = true,
            useLightsPerObject = true;

        [SerializeField] public ShadowSettings shadows = default;

        [SerializeField] public PostProcessingSetting postProcessingSettings = default;

        [SerializeField] public PlanarReflectionSettings planarReflectionSettings = default;

        [SerializeField] public ColorLUTResolution colorLUTResolution = ColorLUTResolution._32;

        [SerializeField] public Shader cameraRendererShader = default;

        [SerializeField] public PipelineLightSetting pipelineLightSetting;
    }
}