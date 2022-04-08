using UnityEngine;
using UnityEngine.Rendering;

namespace MSRP
{
    public partial class CustomRenderPipeline : RenderPipeline
    {
        CameraRenderer renderer;

        CameraBufferSettings cameraBufferSettings;

        bool useDynamicBatching, useGPUInstancing, useLightsPerObject;

        ShadowSettings shadowSettings;

        PostProcessingSetting postProcessingSettings;

        int colorLUTResolution;

        private PipelineLightSetting pipelineLightSetting;

        private PlanarReflectionSettings planarReflectionSettings;

        private PlanarReflections planarReflections;

        private RenderPipelineType renderPipelineType;

        public CustomRenderPipeline (
            RenderPipelineType renderPipelineType,
            CameraBufferSettings cameraBufferSettings,
            bool useDynamicBatching, bool useGPUInstancing, bool useSRPBatcher,
            bool useLightsPerObject, ShadowSettings shadowSettings,
            PostProcessingSetting postProcessingSettings, int colorLUTResolution, Shader cameraRendererShader,
            PipelineLightSetting pipelineLightSetting, PlanarReflectionSettings planarReflectionSettings
        )
        {
            this.renderPipelineType = renderPipelineType;
            this.colorLUTResolution = colorLUTResolution;
            this.cameraBufferSettings = cameraBufferSettings;
            this.postProcessingSettings = postProcessingSettings;
            this.shadowSettings = shadowSettings;
            this.useDynamicBatching = useDynamicBatching;
            this.useGPUInstancing = useGPUInstancing;
            this.useLightsPerObject = useLightsPerObject;
            this.pipelineLightSetting = pipelineLightSetting;
            this.planarReflectionSettings = planarReflectionSettings;
            GraphicsSettings.useScriptableRenderPipelineBatching = useSRPBatcher;
            GraphicsSettings.lightsUseLinearIntensity = true;
            InitializeForEditor();
            renderer = new CameraRenderer(cameraRendererShader);
        }

        protected override void Render (ScriptableRenderContext context, Camera[] cameras) {
            foreach (Camera camera in cameras) {
                if (planarReflectionSettings.m_Enable && camera.cameraType == CameraType.Game)
                {
                    if (planarReflections == null)
                    {
                        planarReflections = new PlanarReflections();
                    }
                    planarReflections.SetUp(planarReflectionSettings);
                    CameraBufferSettings planarReflectionCameraSetting = default;
                    planarReflectionCameraSetting.fxaa.enabled = false;
                    planarReflectionCameraSetting.allowHDR = cameraBufferSettings.allowHDR;
                    planarReflectionCameraSetting.bicubicRescaling = cameraBufferSettings.bicubicRescaling;
                    planarReflectionCameraSetting.renderScale = cameraBufferSettings.renderScale;
                    
                    planarReflections.ExecutePlanarReflections(context, camera, planarReflectionCameraSetting);
                    renderer.Render(
                        context, PlanarReflections._reflectionCamera, renderPipelineType, planarReflectionCameraSetting,
                        useDynamicBatching, useGPUInstancing, useLightsPerObject,
                        shadowSettings, null, colorLUTResolution,
                        pipelineLightSetting, false
                    ); 
                    planarReflections.SetData();
                }
                else
                {
                    if (planarReflections != null)
                    {
                        planarReflections.Cleanup();
                    }
                }
                renderer.Render(
                    context, camera, renderPipelineType, cameraBufferSettings,
                    useDynamicBatching, useGPUInstancing, useLightsPerObject,
                    shadowSettings, postProcessingSettings, colorLUTResolution,
                    pipelineLightSetting
                );
            }
        }

        protected override void Dispose (bool disposing) {
            base.Dispose(disposing);
            DisposeForEditor();
            renderer.Dispose();
        }
    }
}