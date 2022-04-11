using UnityEngine;
using UnityEngine.Rendering;

namespace MSRP
{
    public partial class CustomRenderPipeline : RenderPipeline
    {
        CameraRenderer renderer;

        private RenderPiplineData data;

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
            RenderPiplineData data
        )
        {
            this.data = data;
            renderPipelineType = data.renderPipelineType;
            colorLUTResolution = (int)data.colorLUTResolution;
            cameraBufferSettings = data.cameraBuffer;
            postProcessingSettings = data.postProcessingSettings;
            shadowSettings = data.shadows;
            useDynamicBatching = data.useDynamicBatching;
            useGPUInstancing = data.useGPUInstancing;
            useLightsPerObject = data.useLightsPerObject;
            pipelineLightSetting = data.pipelineLightSetting;
            planarReflectionSettings = data.planarReflectionSettings;
            GraphicsSettings.useScriptableRenderPipelineBatching = data.useSRPBatcher;
            GraphicsSettings.lightsUseLinearIntensity = true;
            InitializeForEditor();
            renderer = new CameraRenderer(data.cameraRendererShader);
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
                    planarReflectionCameraSetting.fxaaSetting.enabled = false;
                    planarReflectionCameraSetting.allowHDR = cameraBufferSettings.allowHDR;
                    planarReflectionCameraSetting.bicubicRescaling = cameraBufferSettings.bicubicRescaling;
                    planarReflectionCameraSetting.renderScale = cameraBufferSettings.renderScale;
                    
                    planarReflections.ExecutePlanarReflections(context, camera, planarReflectionCameraSetting);
                    renderer.Render(
                        context, PlanarReflections._reflectionCamera, data, false
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
                    context, camera, data
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