using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace MSRP
{
    public sealed class TAAData
    {
        #region Fields

        internal Vector2 sampleOffset;
        internal Matrix4x4 projOverride;
        internal Matrix4x4 porjPreview;
        internal Matrix4x4 viewPreview;

        #endregion

        #region Constructors

        internal TAAData()
        {
            projOverride = Matrix4x4.identity;
            porjPreview = Matrix4x4.identity;
            viewPreview = Matrix4x4.identity;
        }

        #endregion
    }

    public class TAA : RenderPassBase
    {
        const string bufferName = "TAA";

        Dictionary<Camera, TAAData> m_TAADatas;

        private ScriptableRenderContext context;

        private CameraBufferSettings.TAASetting setting;

        private Camera camera;

        Matrix4x4 previewView;
        Matrix4x4 previewProj;

        
        const string TaaShader = "Hidden/MSRP/TAA";

        private Material mat;

        RenderTexture[] historyBuffer;

        static int indexWrite = 0;

        CommandBuffer buffer = new CommandBuffer
        {
            name = bufferName
        };
        
        TAAData TaaData;
        
        public static readonly int SourceTextureID = Shader.PropertyToID("_SourceTex");
        public static readonly int TAAParamsID = Shader.PropertyToID("_TAA_Params");
        public static readonly int TAAHistoryTextureID = Shader.PropertyToID("_TAA_Pretexture");
        public static readonly int _TAA_pre_vp = Shader.PropertyToID("_TAA_Pretexture");
        public static readonly int _TAA_PrevViewProjM = Shader.PropertyToID("_PrevViewProjM_TAA");
        public static readonly int _TAA_CurInvView = Shader.PropertyToID("_I_V_Current_jittered");
        public static readonly int _TAA_CurInvProj = Shader.PropertyToID("_I_P_Current_jittered");
        
        internal static readonly string HighTAAQuality = "_HIGH_TAA";
        internal static readonly string MiddleTAAQuality = "_MIDDLE_TAA";
        internal static readonly string LOWTAAQuality = "_LOW_TAA";

        public TAA()
        {
            m_TAADatas = new Dictionary<Camera, TAAData>();
            mat = new Material(Shader.Find(TaaShader));
        }

        public void SetUp(ScriptableRenderContext context, Camera camera, CameraBufferSettings.TAASetting setting)
        {
            this.context = context;
            this.camera = camera;
            this.setting = setting;
            if (!m_TAADatas.TryGetValue(camera, out TaaData))
            {
                TaaData = new TAAData();
                m_TAADatas.Add(camera, TaaData);
            }

            buffer.BeginSample(bufferName);
            if (setting.IsActive() && camera.cameraType == CameraType.Game)
            {
                UpdateTAAData(camera, TaaData);

                buffer.SetViewProjectionMatrices(camera.worldToCameraMatrix, TaaData.projOverride);
            }
            buffer.EndSample(bufferName);
            context.ExecuteCommandBuffer(buffer);
            buffer.Clear();
        }

        public void Render(int sourceId, Vector2Int bufferSize, RenderTextureFormat format)
        {
            if (camera.cameraType != CameraType.Game) return;
            buffer.BeginSample(bufferName);

            EnsureArray(ref historyBuffer, 2);
            EnsureRenderTarget(ref historyBuffer[0], bufferSize.x, bufferSize.y, format,
                FilterMode.Bilinear);
            EnsureRenderTarget(ref historyBuffer[1], bufferSize.x, bufferSize.y, format,
                FilterMode.Bilinear);
            
            int indexRead = indexWrite;
            indexWrite = (++indexWrite) % 2;
            
            Matrix4x4 inv_p_jitterd = Matrix4x4.Inverse(TaaData.projOverride);
            Matrix4x4 inv_v_jitterd = Matrix4x4.Inverse(camera.worldToCameraMatrix);
            Matrix4x4 previous_vp = TaaData.porjPreview * TaaData.viewPreview;
            mat.SetMatrix(_TAA_CurInvView, inv_v_jitterd);
            mat.SetMatrix(_TAA_CurInvProj, inv_p_jitterd);
            mat.SetMatrix(_TAA_PrevViewProjM, previous_vp);
            mat.SetVector(TAAParamsID, new Vector3(TaaData.sampleOffset.x, 
                TaaData.sampleOffset.y, setting.feedback));
            mat.SetTexture(TAAHistoryTextureID, historyBuffer[indexRead]);
            CoreUtils.SetKeyword(buffer, HighTAAQuality, setting.quality == MotionBlurQuality.High);
            CoreUtils.SetKeyword(buffer, MiddleTAAQuality, setting.quality == MotionBlurQuality.Medium);
            CoreUtils.SetKeyword(buffer, LOWTAAQuality, setting.quality == MotionBlurQuality.Low);
            
            // var colorTextureIdentifier = new RenderTargetIdentifier(sourceId);
            
            buffer.Blit(sourceId, historyBuffer[indexWrite], mat, 0);
            buffer.Blit(historyBuffer[indexWrite], sourceId);
            
            
            // Draw(sourceId, historyBuffer[indexWrite], 0);
            // Draw(historyBuffer[indexWrite], sourceId, 1);

            buffer.EndSample(bufferName);
            context.ExecuteCommandBuffer(buffer);
            buffer.Clear();
        }
        
        void Draw(RenderTargetIdentifier from, RenderTargetIdentifier to, int pass)
        {
            buffer.SetGlobalTexture(SourceTextureID, from);
            buffer.SetRenderTarget(
                to, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store
            );
            buffer.DrawProcedural(
                Matrix4x4.identity, mat, (int) pass, MeshTopology.Triangles, 3
            );
        }

        public override void ClearUp()
        {
            Debug.Log("new TAA");
            if(historyBuffer!=null)
            {
                ClearRT(ref historyBuffer[0]);
                ClearRT(ref historyBuffer[1]);
                historyBuffer = null;
            }
        }

        void EnsureArray<T>(ref T[] array, int size, T initialValue = default(T))
        {
            if (array == null || array.Length != size)
            {
                array = new T[size];
                for (int i = 0; i != size; i++)
                    array[i] = initialValue;
            }
        }

        void ClearRT(ref RenderTexture rt)
        {
            if (rt != null)
            {
                RenderTexture.ReleaseTemporary(rt);
                rt = null;
            }
        }

        bool EnsureRenderTarget(ref RenderTexture rt, int width, int height, RenderTextureFormat format,
            FilterMode filterMode, int depthBits = 0, int antiAliasing = 1)
        {
            if (rt != null && (rt.width != width || rt.height != height || rt.format != format ||
                               rt.filterMode != filterMode || rt.antiAliasing != antiAliasing))
            {
                RenderTexture.ReleaseTemporary(rt);
                rt = null;
            }

            if (rt == null)
            {
                rt = RenderTexture.GetTemporary(width, height, depthBits, format, RenderTextureReadWrite.Default,
                    antiAliasing);
                rt.filterMode = filterMode;
                rt.wrapMode = TextureWrapMode.Clamp;
                return true; // new target
            }

            return false; // same target
        }

        void UpdateTAAData(Camera camera, TAAData TaaData)
        {
            Vector2 additionalSample = TAAUtils.GenerateRandomOffset() * setting.spread;
            TaaData.sampleOffset = additionalSample;
            TaaData.porjPreview = previewProj;
            TaaData.viewPreview = previewView;
            TaaData.projOverride = camera.orthographic
                ? TAAUtils.GetJitteredOrthographicProjectionMatrix(camera, TaaData.sampleOffset)
                : TAAUtils.GetJitteredPerspectiveProjectionMatrix(camera, TaaData.sampleOffset);
            TaaData.sampleOffset = new Vector2(TaaData.sampleOffset.x / camera.scaledPixelWidth,
                TaaData.sampleOffset.y / camera.scaledPixelHeight);
            previewView = camera.worldToCameraMatrix;
            previewProj = camera.projectionMatrix;
        }
    }
}