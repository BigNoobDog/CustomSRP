using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

namespace MSRP
{
    public class PreIntegrateBRDF
    {
        const string bufferName = "PreIntegrateBRDF";

        private static int PreIntegrateBRDFLUTId = Shader.PropertyToID("_PreIntegrateBRDF");

        private const string shaderName = "Hidden/MSRP/PreIntegrateBrdfLUT";

        private const int size = 512;
        
        CommandBuffer buffer = new CommandBuffer
        {
            name = bufferName
        };

        private RenderTexture PreIntegrateBRDFLUT;

        private Material mat;

        void CreateRT()
        {
            if (PreIntegrateBRDFLUT != null && (PreIntegrateBRDFLUT.width != size 
                                                || PreIntegrateBRDFLUT.height != size 
                                                || PreIntegrateBRDFLUT.format != RenderTextureFormat.DefaultHDR 
                                                || PreIntegrateBRDFLUT.filterMode != FilterMode.Point 
                                                || PreIntegrateBRDFLUT.antiAliasing != 1))
            {
                RenderTexture.ReleaseTemporary(PreIntegrateBRDFLUT);
                PreIntegrateBRDFLUT = null;
            }
            
            if (PreIntegrateBRDFLUT == null)
            {
                PreIntegrateBRDFLUT = RenderTexture.GetTemporary(size, size,  0, 
                    RenderTextureFormat.DefaultHDR, RenderTextureReadWrite.Linear,
                    1);
                PreIntegrateBRDFLUT.filterMode = FilterMode.Point;
                PreIntegrateBRDFLUT.wrapMode = TextureWrapMode.Clamp;
            }

            if (mat == null)
            {
                mat = new Material(Shader.Find(shaderName));
            }
        }

        public void SetUp(ScriptableRenderContext context, int sourceID)
        {
            buffer.BeginSample(bufferName);
            CreateRT();
            buffer.Blit(sourceID, PreIntegrateBRDFLUT, mat);
            buffer.SetGlobalTexture(PreIntegrateBRDFLUTId, PreIntegrateBRDFLUT);
            buffer.EndSample(bufferName);
            context.ExecuteCommandBuffer(buffer);
            buffer.Clear();
        }

        public void ClearUp()
        {
            if (PreIntegrateBRDFLUT != null)
            {
                RenderTexture.ReleaseTemporary(PreIntegrateBRDFLUT);
                PreIntegrateBRDFLUT = null;
            }
        }
    }
}