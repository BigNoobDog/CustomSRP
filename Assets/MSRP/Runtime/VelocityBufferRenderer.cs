using UnityEngine;
using UnityEngine.Rendering;

namespace MSRP
{
    public class VelocityBufferRenderer : RenderPassBase
    {
        const string bufferName = "VelocityBuffer";

        CommandBuffer buffer = new CommandBuffer
        {
            name = bufferName
        };

        private static int
            vetorBufferAttachmentId = Shader.PropertyToID("_VetorBufferAttachment");

        Vector2Int bufferSize;

        private ScriptableRenderContext context;

        private Camera camera;

        CullingResults cullingResults;

        public void Setup(
            ScriptableRenderContext context, Camera camera, CullingResults cullingResults, Vector2Int bufferSize)
        {
            this.cullingResults = cullingResults;
            this.context = context;
            this.camera = camera;
            buffer.BeginSample(bufferName);
            
            buffer.GetTemporaryRT(
                vetorBufferAttachmentId, bufferSize.x, bufferSize.y,
                0, FilterMode.Bilinear, RenderTextureFormat.DefaultHDR
            );
            buffer.SetRenderTarget(
                vetorBufferAttachmentId,
                RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store
            );
            DrawBuffer();
            buffer.EndSample(bufferName);
            context.ExecuteCommandBuffer(buffer);
            buffer.Clear();
        }

        public void DrawBuffer()
        {
            var filteringSettings = new FilteringSettings(RenderQueueRange.opaque);
            var sortingSettings = new SortingSettings(camera)
            {
                criteria = SortingCriteria.CommonOpaque
            };
            var drawingSettings = new DrawingSettings(new ShaderTagId("MotionVectors"), sortingSettings)
            {
                perObjectData = PerObjectData.MotionVectors,
            };
            RenderStateBlock stateBlock = new RenderStateBlock(RenderStateMask.Nothing);
            context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings, ref stateBlock);
        }
        
        public void Cleanup()
        {
            buffer.ReleaseTemporaryRT(vetorBufferAttachmentId);
        }
    }
}