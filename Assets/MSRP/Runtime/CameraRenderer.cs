using UnityEngine;
using UnityEngine.Rendering;

namespace MSRP
{
    public partial class CameraRenderer
    {
        public const float renderScaleMin = 0.1f, renderScaleMax = 2f;

        const string bufferName = "Render Camera";

        static ShaderTagId
            unlitShaderTagId = new ShaderTagId("SRPDefaultUnlit"),
            litForwardShaderTagId = new ShaderTagId("CustomLitForward"),
            litDeferredShaderTagId = new ShaderTagId("CustomLitDeferred"),
            litGBufferShaderTagId = new ShaderTagId("CustomLitGBuffer");

        static int
            bufferSizeId = Shader.PropertyToID("_CameraBufferSize"),
            colorAttachmentId = Shader.PropertyToID("_CameraColorAttachment"),
            depthAttachmentId = Shader.PropertyToID("_CameraDepthAttachment"),
            gBufferAAttachmetnId = Shader.PropertyToID("_GBufferA"),
            gBufferBAttachmetnId = Shader.PropertyToID("_GBufferB"),
            gBufferCAttachmetnId = Shader.PropertyToID("_GBufferC"),
            gBufferDAttachmetnId = Shader.PropertyToID("_GBufferD"),
            gBufferEAttachmetnId = Shader.PropertyToID("_GBufferE"),
            gBufferFAttachmetnId = Shader.PropertyToID("_GBufferF"),
            colorTextureId = Shader.PropertyToID("_CameraColorTexture"),
            depthTextureId = Shader.PropertyToID("_CameraDepthTexture"),
            sourceTextureId = Shader.PropertyToID("_SourceTexture"),
            srcBlendId = Shader.PropertyToID("_CameraSrcBlend"),
            dstBlendId = Shader.PropertyToID("_CameraDstBlend");

        static CameraSettings defaultCameraSettings = new CameraSettings();

        private PreIntegrateBRDF PreIntegrateBrdf = new PreIntegrateBRDF();

        static bool copyTextureSupported =
            SystemInfo.copyTextureSupport > CopyTextureSupport.None;

        CommandBuffer buffer = new CommandBuffer
        {
            name = bufferName
        };

        ScriptableRenderContext context;

        Camera camera;

        CullingResults cullingResults;

        Lighting lighting = new Lighting();

        PostProcessingStack postProcessingStack = new PostProcessingStack();

        bool useHDR, useScaledRendering, useLightsPerObject, useDynamicBatching, useGPUInstancing;

        bool useColorTexture, useDepthTexture, useIntermediateBuffer;

        private ShadowSettings shadowSettings;

        private PostProcessingSetting postProcessingSettings;

        private RenderPipelineType renderPipelineType;

        private int colorLUTResolution;
        
        PipelineLightSetting pipelineLightSetting;

        Vector2Int bufferSize;

        Material material;

        Texture2D missingTexture;
        
        private ComputeShader areaLightLut;
        
        private Camera pr_camera;

        private TAA taa = new TAA();

        private CameraBufferSettings bufferSettings;

        private VelocityBufferRenderer velocityBufferRenderer = new VelocityBufferRenderer();

        public CameraRenderer(Shader shader)
        {
            material = MSRPCoreUtils.CreateEngineMaterial(shader);
            missingTexture = new Texture2D(1, 1)
            {
                hideFlags = HideFlags.HideAndDontSave,
                name = "Missing"
            };
            missingTexture.SetPixel(0, 0, Color.white * 0.5f);
            missingTexture.Apply(true, true);
        }
        
        void SetupForward()
        {
            context.SetupCameraProperties(camera);
            CameraClearFlags flags = camera.clearFlags;

            useIntermediateBuffer = useScaledRendering ||
                                    useColorTexture || useDepthTexture || postProcessingStack.IsActive;
            if (useIntermediateBuffer)
            {
                if (flags > CameraClearFlags.Color)
                {
                    flags = CameraClearFlags.Color;
                }

                buffer.GetTemporaryRT(
                    colorAttachmentId, bufferSize.x, bufferSize.y,
                    0, FilterMode.Bilinear, useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default
                );
                buffer.GetTemporaryRT(
                    depthAttachmentId, bufferSize.x, bufferSize.y,
                    32, FilterMode.Point, RenderTextureFormat.Depth
                );
                buffer.SetRenderTarget(
                    colorAttachmentId,
                    RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store,
                    depthAttachmentId,
                    RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store
                );
            }

            buffer.ClearRenderTarget(
                flags <= CameraClearFlags.Depth,
                flags == CameraClearFlags.Color,
                flags == CameraClearFlags.Color ? camera.backgroundColor.linear : Color.clear
            );
            buffer.BeginSample(SampleName);
            buffer.SetGlobalTexture(colorTextureId, missingTexture);
            buffer.SetGlobalTexture(depthTextureId, missingTexture);
            // buffer.EndSample(SampleName);
            ExecuteBuffer();
        }
        
        void SetupDeferred()
        {
            context.SetupCameraProperties(camera);
            CameraClearFlags flags = camera.clearFlags;

            useIntermediateBuffer = useScaledRendering ||
                                    useColorTexture || useDepthTexture || postProcessingStack.IsActive;
            if (useIntermediateBuffer)
            {
                if (flags > CameraClearFlags.Color)
                {
                    flags = CameraClearFlags.Color;
                }
                buffer.GetTemporaryRT(
                    colorAttachmentId, bufferSize.x, bufferSize.y,
                    0, FilterMode.Bilinear, useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default
                );
                buffer.GetTemporaryRT(
                    depthAttachmentId, bufferSize.x, bufferSize.y,
                    32, FilterMode.Point, RenderTextureFormat.Depth
                );

                buffer.GetTemporaryRT(
                    gBufferAAttachmetnId, bufferSize.x, bufferSize.y,
                    0, FilterMode.Bilinear, useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default
                );
                buffer.GetTemporaryRT(
                    gBufferBAttachmetnId, bufferSize.x, bufferSize.y,
                    32, FilterMode.Point, RenderTextureFormat.Depth
                );
                buffer.GetTemporaryRT(
                    gBufferCAttachmetnId, bufferSize.x, bufferSize.y,
                    0, FilterMode.Bilinear, useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default
                );
                buffer.GetTemporaryRT(
                    gBufferDAttachmetnId, bufferSize.x, bufferSize.y,
                    0, FilterMode.Bilinear, useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default
                );
                buffer.GetTemporaryRT(
                    gBufferEAttachmetnId, bufferSize.x, bufferSize.y,
                    0, FilterMode.Bilinear, useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default
                );
                buffer.GetTemporaryRT(
                    gBufferFAttachmetnId, bufferSize.x, bufferSize.y,
                    0, FilterMode.Bilinear, useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default
                );
                
                RenderTargetIdentifier[] GBufferAttachmentIds = new RenderTargetIdentifier[]
                {
                    gBufferAAttachmetnId, gBufferBAttachmetnId, gBufferCAttachmetnId,
                    gBufferDAttachmetnId, gBufferEAttachmetnId, gBufferFAttachmetnId
                };
                buffer.SetRenderTarget(
                    GBufferAttachmentIds, depthAttachmentId
                );
            }

            buffer.ClearRenderTarget(
                flags <= CameraClearFlags.Depth,
                flags == CameraClearFlags.Color,
                flags == CameraClearFlags.Color ? camera.backgroundColor.linear : Color.clear
            );
            buffer.BeginSample(SampleName);
            buffer.SetGlobalTexture(colorTextureId, missingTexture);
            buffer.SetGlobalTexture(depthTextureId, missingTexture);
            // buffer.EndSample(SampleName);
            ExecuteBuffer();
        }

        public void Dispose()
        {
            MSRPCoreUtils.Destroy(material);
            MSRPCoreUtils.Destroy(missingTexture);
        }

        public void Render(
            ScriptableRenderContext context, 
            Camera camera,
            RenderPiplineData data,
            bool drawAddtion = true
        )
        {
            this.context = context;
            this.camera = camera;
            bufferSettings = data.cameraBuffer;
            shadowSettings = data.shadows;
            useLightsPerObject = data.useLightsPerObject;
            useDynamicBatching = data.useDynamicBatching;
            useGPUInstancing = data.useGPUInstancing;
            postProcessingSettings = data.postProcessingSettings;
            colorLUTResolution = (int)data.colorLUTResolution;
            pipelineLightSetting = data.pipelineLightSetting;
            renderPipelineType = data.renderPipelineType;

            var crpCamera = camera.GetComponent<CustomRenderPipelineCamera>();
            CameraSettings cameraSettings =
                crpCamera ? crpCamera.Settings : defaultCameraSettings;

            if (camera.cameraType == CameraType.Reflection)
            {
                useColorTexture = bufferSettings.copyColorReflection;
                useDepthTexture = bufferSettings.copyDepthReflection;
            }
            else
            {
                useColorTexture = bufferSettings.copyColor && cameraSettings.copyColor;
                useDepthTexture = bufferSettings.copyDepth && cameraSettings.copyDepth;
            }

            if (cameraSettings.overridePostProcessing)
            {
                postProcessingSettings = cameraSettings.postFXSettings;
            }

            float renderScale = cameraSettings.GetRenderScale(bufferSettings.renderScale);
            useScaledRendering = renderScale < 0.99f || renderScale > 1.01f;
            PrepareBuffer();
            PrepareForSceneWindow();
            if (!Cull(shadowSettings.maxDistance))
            {
                return;
            }

            useHDR = bufferSettings.allowHDR && camera.allowHDR;
            if (useScaledRendering)
            {
                renderScale = Mathf.Clamp(renderScale, renderScaleMin, renderScaleMax);
                bufferSize.x = (int) (camera.pixelWidth * renderScale);
                bufferSize.y = (int) (camera.pixelHeight * renderScale);
            }
            else
            {
                bufferSize.x = camera.pixelWidth;
                bufferSize.y = camera.pixelHeight;
            }

            buffer.BeginSample(SampleName);
            buffer.SetGlobalVector(bufferSizeId, new Vector4(
                1f / bufferSize.x, 1f / bufferSize.y,
                bufferSize.x, bufferSize.y
            ));
            ExecuteBuffer();
            
            lighting.Setup(
                context, cullingResults, shadowSettings, useLightsPerObject,
                cameraSettings.maskLights ? cameraSettings.renderingLayerMask : -1,
                pipelineLightSetting
            );

            bufferSettings.fxaaSetting.enabled = 
                (cameraSettings.allowFXAA && bufferSettings.antiAliasingType == CameraBufferSettings.AntiAliasingType.FXAA);
            postProcessingStack.Setup(
                context, camera, bufferSize, postProcessingSettings, cameraSettings.keepAlpha, useHDR,
                colorLUTResolution, cameraSettings.finalBlendMode,
                bufferSettings.bicubicRescaling, bufferSettings.fxaaSetting
            );
            PreIntegrateBrdf.SetUp(context, colorAttachmentId);
            buffer.EndSample(SampleName);

            switch (renderPipelineType)
            {
                case RenderPipelineType.Forward: 
                    SetupForward();

                    if (bufferSettings.antiAliasingType == CameraBufferSettings.AntiAliasingType.TAA)
                    {
                        taa.SetUp(context, camera, bufferSettings.taaSetting);
                    }
            
                    if (drawAddtion && (camera.cameraType == CameraType.Game || camera.cameraType == CameraType.SceneView))
                    {
                        lighting.DrawAreaLight();
                    }
            
                    DrawForwardVisibleGeometry(
                        useDynamicBatching, useGPUInstancing, useLightsPerObject,
                        cameraSettings.renderingLayerMask
                    );
                    break;
                case RenderPipelineType.Deferred:
                    SetupDeferred();

                    if (bufferSettings.antiAliasingType == CameraBufferSettings.AntiAliasingType.TAA)
                    {
                        taa.SetUp(context, camera, bufferSettings.taaSetting);
                    }
            
                    if (drawAddtion && (camera.cameraType == CameraType.Game || camera.cameraType == CameraType.SceneView))
                    {
                        lighting.DrawAreaLight();
                    }
            
                    DrawDeferredVisibleGeometry(
                        useDynamicBatching, useGPUInstancing, useLightsPerObject,
                        cameraSettings.renderingLayerMask
                    );
                    
                    break;
                default:
                    break;;
            }
            
            DrawUnsupportedShaders();
            DrawGizmosBeforeFX();
            
            if (bufferSettings.antiAliasingType == CameraBufferSettings.AntiAliasingType.TAA)
            {
                taa.Render(colorAttachmentId, bufferSize, useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default);
            }
            
            if (postProcessingStack.IsActive)
            {
                postProcessingStack.Render(colorAttachmentId);
            }
            else if (useIntermediateBuffer)
            {
                DrawFinal(cameraSettings.finalBlendMode);
                ExecuteBuffer();
            }

            DrawGizmosAfterFX();
            Cleanup();
            Submit();
        }
        
        bool Cull(float maxShadowDistance)
        {
            if (camera.TryGetCullingParameters(out ScriptableCullingParameters p))
            {
                p.shadowDistance = Mathf.Min(maxShadowDistance, camera.farClipPlane);
                cullingResults = context.Cull(ref p);
                return true;
            }

            return false;
        }

        void Cleanup()
        {
            lighting.Cleanup();
            velocityBufferRenderer.Cleanup();
            if (useIntermediateBuffer)
            {
                
                switch (renderPipelineType)
                {
                    case RenderPipelineType.Forward: 
                        buffer.ReleaseTemporaryRT(colorAttachmentId);
                        buffer.ReleaseTemporaryRT(depthAttachmentId);
                        if (useColorTexture)
                        {
                            buffer.ReleaseTemporaryRT(colorTextureId);
                        }

                        if (useDepthTexture)
                        {
                            buffer.ReleaseTemporaryRT(depthTextureId);
                        }
                        break;
                    case RenderPipelineType.Deferred:
                        buffer.ReleaseTemporaryRT(gBufferAAttachmetnId);
                        buffer.ReleaseTemporaryRT(gBufferBAttachmetnId);
                        buffer.ReleaseTemporaryRT(gBufferCAttachmetnId);
                        buffer.ReleaseTemporaryRT(gBufferDAttachmetnId);
                        buffer.ReleaseTemporaryRT(gBufferEAttachmetnId);
                        buffer.ReleaseTemporaryRT(gBufferFAttachmetnId);
                        buffer.ReleaseTemporaryRT(depthAttachmentId);
                        if (useColorTexture)
                        {
                            buffer.ReleaseTemporaryRT(colorTextureId);
                        }
                        if (useDepthTexture)
                        {
                            buffer.ReleaseTemporaryRT(depthTextureId);
                        }
                        break;
                    default:
                        break;;
                }
            }
            if (bufferSettings.antiAliasingType != CameraBufferSettings.AntiAliasingType.TAA)
            {
                taa.ClearUp();
            }
            PreIntegrateBrdf.ClearUp();
        }

        void Submit()
        {
            buffer.EndSample(SampleName);
            ExecuteBuffer();
            context.Submit();
        }

        void ExecuteBuffer()
        {
            context.ExecuteCommandBuffer(buffer);
            buffer.Clear();
        }

        void DrawForwardVisibleGeometry(
            bool useDynamicBatching, bool useGPUInstancing, bool useLightsPerObject,
            int renderingLayerMask
        )
        {
            PerObjectData lightsPerObjectFlags = useLightsPerObject
                ? PerObjectData.LightData | PerObjectData.LightIndices
                : PerObjectData.None;
            var sortingSettings = new SortingSettings(camera)
            {
                criteria = SortingCriteria.CommonOpaque
            };
            var drawingSettings = new DrawingSettings(
                unlitShaderTagId, sortingSettings
            )
            {
                enableDynamicBatching = useDynamicBatching,
                enableInstancing = useGPUInstancing,
                perObjectData =
                    PerObjectData.ReflectionProbes |
                    PerObjectData.Lightmaps | PerObjectData.ShadowMask |
                    PerObjectData.LightProbe | PerObjectData.OcclusionProbe |
                    PerObjectData.LightProbeProxyVolume |
                    PerObjectData.OcclusionProbeProxyVolume |
                    lightsPerObjectFlags
            };
            drawingSettings.SetShaderPassName(1, litForwardShaderTagId);

            var filteringSettings = new FilteringSettings(
                RenderQueueRange.opaque, renderingLayerMask: (uint) renderingLayerMask
            );

            context.DrawRenderers(
                cullingResults, ref drawingSettings, ref filteringSettings
            );

            context.DrawSkybox(camera);
            if (useColorTexture || useDepthTexture)
            {
                CopyAttachments();
            }

            sortingSettings.criteria = SortingCriteria.CommonTransparent;
            drawingSettings.sortingSettings = sortingSettings;
            filteringSettings.renderQueueRange = RenderQueueRange.transparent;

            context.DrawRenderers(
                cullingResults, ref drawingSettings, ref filteringSettings
            );
        }
        
        void DrawDeferredVisibleGeometry(
            bool useDynamicBatching, bool useGPUInstancing, bool useLightsPerObject,
            int renderingLayerMask
        )
        {
            PerObjectData lightsPerObjectFlags = useLightsPerObject
                ? PerObjectData.LightData | PerObjectData.LightIndices
                : PerObjectData.None;
            var sortingSettings = new SortingSettings(camera)
            {
                criteria = SortingCriteria.CommonOpaque
            };
            var drawingSettings = new DrawingSettings(
                unlitShaderTagId, sortingSettings
            )
            {
                enableDynamicBatching = useDynamicBatching,
                enableInstancing = useGPUInstancing,
                perObjectData =
                    PerObjectData.ReflectionProbes |
                    PerObjectData.Lightmaps | PerObjectData.ShadowMask |
                    PerObjectData.LightProbe | PerObjectData.OcclusionProbe |
                    PerObjectData.LightProbeProxyVolume |
                    PerObjectData.OcclusionProbeProxyVolume |
                    lightsPerObjectFlags
            };
            drawingSettings.SetShaderPassName(1, litGBufferShaderTagId);

            var filteringSettings = new FilteringSettings(
                RenderQueueRange.opaque, renderingLayerMask: (uint) renderingLayerMask
            );

            context.DrawRenderers(
                cullingResults, ref drawingSettings, ref filteringSettings
            );

            context.DrawSkybox(camera);
            if (useColorTexture || useDepthTexture)
            {
                CopyAttachments();
            }

            sortingSettings.criteria = SortingCriteria.CommonTransparent;
            drawingSettings.sortingSettings = sortingSettings;
            filteringSettings.renderQueueRange = RenderQueueRange.transparent;

            context.DrawRenderers(
                cullingResults, ref drawingSettings, ref filteringSettings
            );
        }

        void CopyAttachments()
        {
            if (useColorTexture)
            {
                buffer.GetTemporaryRT(
                    colorTextureId, bufferSize.x, bufferSize.y,
                    0, FilterMode.Bilinear, useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default
                );
                if (copyTextureSupported)
                {
                    buffer.CopyTexture(colorAttachmentId, colorTextureId);
                }
                else
                {
                    Draw(colorAttachmentId, colorTextureId);
                }
            }

            if (useDepthTexture)
            {
                buffer.GetTemporaryRT(
                    depthTextureId, bufferSize.x, bufferSize.y,
                    32, FilterMode.Point, RenderTextureFormat.Depth
                );
                if (copyTextureSupported)
                {
                    buffer.CopyTexture(depthAttachmentId, depthTextureId);
                }
                else
                {
                    Draw(depthAttachmentId, depthTextureId, true);
                }
            }

            if (!copyTextureSupported)
            {
                buffer.SetRenderTarget(
                    colorAttachmentId,
                    RenderBufferLoadAction.Load, RenderBufferStoreAction.Store,
                    depthAttachmentId,
                    RenderBufferLoadAction.Load, RenderBufferStoreAction.Store
                );
            }

            ExecuteBuffer();
        }

        void Draw(
            RenderTargetIdentifier from, RenderTargetIdentifier to, bool isDepth = false
        )
        {
            buffer.SetGlobalTexture(sourceTextureId, from);
            buffer.SetRenderTarget(
                to, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store
            );
            buffer.SetViewport(camera.pixelRect);
            buffer.DrawProcedural(
                Matrix4x4.identity, material, isDepth ? 1 : 0, MeshTopology.Triangles, 3
            );
        }

        void DrawFinal(CameraSettings.FinalBlendMode finalBlendMode)
        {
            buffer.SetGlobalFloat(srcBlendId, (float) finalBlendMode.source);
            buffer.SetGlobalFloat(dstBlendId, (float) finalBlendMode.destination);
            buffer.SetGlobalTexture(sourceTextureId, colorAttachmentId);
            buffer.SetRenderTarget(
                BuiltinRenderTextureType.CameraTarget,
                finalBlendMode.destination == BlendMode.Zero
                    ? RenderBufferLoadAction.DontCare
                    : RenderBufferLoadAction.Load,
                RenderBufferStoreAction.Store
            );
            buffer.SetViewport(camera.pixelRect);
            buffer.DrawProcedural(
                Matrix4x4.identity, material, 0, MeshTopology.Triangles, 3
            );
            buffer.SetGlobalFloat(srcBlendId, 1f);
            buffer.SetGlobalFloat(dstBlendId, 0f);
        }
    }
}