using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace MSRP
{
    [ExecuteInEditMode]
    public class FFTOceanSimulation : MonoBehaviour
    {
        int Size;
        public float SpaceSize = 50;
        public int N = 7;
        public float AMPScale = 10;
        [Range(5, 5000)] public float Depth = 3000;
        public float WindDir = 0.68f;
        public float WindSpeed = 6;
        public float WindBias = 2;
        public float WindMove = 0.6f;
        public float WindMink = 0;
        public float WaveChop = 0.9f;
        public bool Loop = false;
        public float LoopPeriod = 6.283f;

        private RenderTexture HZeroRT;
        private RenderTexture ButterflyRT;
        private RenderTexture SpectrumX;
        private RenderTexture SpectrumY;
        private RenderTexture SpectrumZ;
        private RenderTexture PingTexture;
        private RenderTexture PongTexture;
        private RenderTexture Displacement;
        private RenderTexture NormalX;
        private RenderTexture NormalZ;
        private RenderTexture NormalRT;
        private RenderTexture BubblesRT;
        public Texture2D GaussianTexture;

        public ComputeShader OceanSimulationCS;

        [FormerlySerializedAs("OceanRenderering")] public Material OceanMat;

        public GameObject OceanMesh;
        
        
        GameObject OceanMeshInstance;

        private void OnEnable()
        {
            Init();
        }

        private void OnDisable()
        {
            CleanUp();
        }

        void CleanUp()
        {
            CoreUtils.SafeDestroy(HZeroRT);
            CoreUtils.SafeDestroy(SpectrumX);
            CoreUtils.SafeDestroy(SpectrumY);
            CoreUtils.SafeDestroy(SpectrumZ);
            CoreUtils.SafeDestroy(ButterflyRT);
            CoreUtils.SafeDestroy(PingTexture);
            CoreUtils.SafeDestroy(PongTexture);
            CoreUtils.SafeDestroy(Displacement);
            CoreUtils.SafeDestroy(NormalX);
            CoreUtils.SafeDestroy(NormalZ);
            CoreUtils.SafeDestroy(NormalRT);
            CoreUtils.SafeDestroy(BubblesRT);
            CoreUtils.SafeDestroy(OceanMeshInstance);
        }

        private void Update()
        {
            if (Size - Mathf.Pow(2, N) > 0.00001f)
            {
                Init();
            }

            if (OceanMeshInstance)
            {
                OceanMeshInstance.transform.position =
                    new Vector3(Camera.main.transform.position.x, 0, Camera.main.transform.position.z);

                var mrs = OceanMeshInstance.GetComponentsInChildren<MeshRenderer>();

                foreach (var mr in mrs)
                {
                    mr.sharedMaterial = OceanMat;
                }
            }

            SetCSParam();
            CaculateButterfly();
            CaculateHZero();
            CaculateSpectrum();
            CaculateButterfly();
            CaculateDisplacement();
            CaculateNormal();
            CaculateBubbles();

            SetMatParam();
        }

        public void Init()
        {
            Size = (int) Mathf.Pow(2, N);
            
            HZeroRT = CreateRT();
            SpectrumX = CreateRT();
            SpectrumY = CreateRT();
            SpectrumZ = CreateRT();
            ButterflyRT = CreateRT();
            PingTexture = CreateRT();
            PongTexture = CreateRT();
            Displacement = CreateRT();
            NormalX = CreateRT();
            NormalZ = CreateRT();
            NormalRT = CreateRT();
            BubblesRT = CreateRT();

            OceanMeshInstance = MonoBehaviour.Instantiate(OceanMesh, transform);

            OceanMat.SetTexture("_DisplacementMap", Displacement);
            OceanMat.SetTexture("_NormalMap", NormalRT);
            OceanMat.SetTexture("_BubblesMap", BubblesRT);
        }

        private RenderTexture CreateRT()
        {
            RenderTexture rt = new RenderTexture(Size, Size, 0, RenderTextureFormat.ARGBFloat);
            rt.enableRandomWrite = true;
            rt.useMipMap = false;
            rt.wrapMode = TextureWrapMode.Repeat;
            rt.Create();
            return rt;
        }

        void SetCSParam()
        {
            OceanSimulationCS.SetFloat("Size", (float) Size);
            OceanSimulationCS.SetFloat("SpaceSize", (float) SpaceSize);
            OceanSimulationCS.SetFloat("AMPScale", (float) AMPScale);
            OceanSimulationCS.SetFloat("WindDir", (float) WindDir);
            OceanSimulationCS.SetFloat("WindSpeed", (float) WindSpeed);
            OceanSimulationCS.SetFloat("WindBias", (float) WindBias);
            OceanSimulationCS.SetFloat("WindMove", (float) WindMove);
            OceanSimulationCS.SetFloat("WindMink", (float) WindMink);
            OceanSimulationCS.SetFloat("WaveChop", (float) WaveChop);
            OceanSimulationCS.SetFloat("Depth", (float) Depth);
            OceanSimulationCS.SetFloat("Time", (float) Time.time + 100f);
            if (Loop)
            {
                OceanSimulationCS.SetFloat("Loop", 1.0f);
            }
            else
            {
                OceanSimulationCS.SetFloat("Loop", 0.0f);
            }

            OceanSimulationCS.SetFloat("LoopPeriod", LoopPeriod);
            OceanSimulationCS.SetFloat("_RandomSeed1", Random.value * 10f);
            OceanSimulationCS.SetFloat("_RandomSeed2", Random.value * 10f);
        }

        void SetMatParam()
        {
            OceanMat.SetFloat("_AMPScale", AMPScale);
            OceanMat.SetFloat("_SpaceSize", SpaceSize);
        }

        void CaculateHZero()
        {
            // Debug.Log(HZeroTexture);
            int H0_kernel = OceanSimulationCS.FindKernel("H0CSMain");
            OceanSimulationCS.SetTexture(H0_kernel, "GaussianTexture", GaussianTexture);
            OceanSimulationCS.SetTexture(H0_kernel, "H0Result", HZeroRT);
            OceanSimulationCS.Dispatch(H0_kernel, Size / 8, Size / 8, 1);
        }

        void CaculateSpectrum()
        {
            // Debug.Log(HZeroTexture);
            int Frequency_kernel = OceanSimulationCS.FindKernel("FrequencyCSMain");
            OceanSimulationCS.SetTexture(Frequency_kernel, "H0Result", HZeroRT);
            OceanSimulationCS.SetTexture(Frequency_kernel, "SpectrumX", SpectrumX);
            OceanSimulationCS.SetTexture(Frequency_kernel, "SpectrumY", SpectrumY);
            OceanSimulationCS.SetTexture(Frequency_kernel, "SpectrumZ", SpectrumZ);
            OceanSimulationCS.Dispatch(Frequency_kernel, Size / 8, Size / 8, 1);
        }

        void CaculateButterfly()
        {
            // Debug.Log(HZeroTexture);
            int Butterfly_kernel = OceanSimulationCS.FindKernel("CreateButterflyCSMain");
            OceanSimulationCS.SetTexture(Butterfly_kernel, "ButterflyRT", ButterflyRT);
            OceanSimulationCS.Dispatch(Butterfly_kernel, Size / 8, Size / 8, 1);
        }

        void CaculateDisplacement()
        {
            int Displace_kernel = OceanSimulationCS.FindKernel("GenerationDisplaceCSMain");
            var resultY = CaculateFFTNative(SpectrumY, Displacement);
            OceanSimulationCS.SetInt("DisplaceChannel", 1);
            OceanSimulationCS.SetTexture(Displace_kernel, "FFTInputRT", resultY);
            OceanSimulationCS.SetTexture(Displace_kernel, "DisplacementOutputRT", Displacement);
            OceanSimulationCS.Dispatch(Displace_kernel, Size / 8, Size / 8, 1);
            var resultX = CaculateFFTNative(SpectrumX, Displacement);
            OceanSimulationCS.SetInt("DisplaceChannel", 0);
            OceanSimulationCS.SetTexture(Displace_kernel, "FFTInputRT", resultX);
            OceanSimulationCS.SetTexture(Displace_kernel, "DisplacementOutputRT", Displacement);
            OceanSimulationCS.Dispatch(Displace_kernel, Size / 8, Size / 8, 1);
            var resultZ = CaculateFFTNative(SpectrumZ, Displacement);
            OceanSimulationCS.SetInt("DisplaceChannel", 2);
            OceanSimulationCS.SetTexture(Displace_kernel, "FFTInputRT", resultZ);
            OceanSimulationCS.SetTexture(Displace_kernel, "DisplacementOutputRT", Displacement);
            OceanSimulationCS.Dispatch(Displace_kernel, Size / 8, Size / 8, 1);
        }

        void CaculateFFT(RenderTexture source, RenderTexture dest)
        {
            int kernelFFTHorizontal = OceanSimulationCS.FindKernel("FFTHorizontalCSMain");
            int kernelFFTVertical = OceanSimulationCS.FindKernel("FFTVerticalCSMain");
            Graphics.Blit(source, PingTexture);
            OceanSimulationCS.SetTexture(kernelFFTHorizontal, "ButterflyRT", ButterflyRT);
            OceanSimulationCS.SetTexture(kernelFFTVertical, "ButterflyRT", ButterflyRT);
            int index = 0;

            //Horizontal IFFT
            for (int stageIndex = 0; stageIndex < N; ++stageIndex)
            {
                OceanSimulationCS.SetInt("Stage", stageIndex);
                OceanSimulationCS.SetTexture(kernelFFTHorizontal, "FFTInputRT",
                    index > 0.5f ? PongTexture : PingTexture);
                OceanSimulationCS.SetTexture(kernelFFTHorizontal, "FFTOutputRT",
                    index > 0.5f ? PingTexture : PongTexture);
                OceanSimulationCS.Dispatch(kernelFFTHorizontal, Size / 8, Size / 8, 1);

                index = index > 0.5 ? 0 : 1;
            }

            //Vertical IFFT
            for (int stageIndex = 0; stageIndex < N; ++stageIndex)
            {
                OceanSimulationCS.SetInt("Stage", stageIndex);
                OceanSimulationCS.SetTexture(kernelFFTVertical, "FFTInputRT", index > 0.5f ? PongTexture : PingTexture);
                OceanSimulationCS.SetTexture(kernelFFTVertical, "FFTOutputRT",
                    index > 0.5f ? PingTexture : PongTexture);
                OceanSimulationCS.Dispatch(kernelFFTVertical, Size / 8, Size / 8, 1);

                index = index > 0.5 ? 0 : 1;
            }

            Graphics.Blit(index > 0.5f ? PongTexture : PingTexture, dest);
        }

        RenderTexture CaculateFFTNative(RenderTexture source, RenderTexture dest)
        {
            int FFTHorizontal_kernel = OceanSimulationCS.FindKernel("FFTHorizontalCSMain");
            int FFTVertical_kernel = OceanSimulationCS.FindKernel("FFTVerticalCSMain");
            Graphics.Blit(source, PingTexture);
            OceanSimulationCS.SetTexture(FFTHorizontal_kernel, "ButterflyRT", ButterflyRT);
            OceanSimulationCS.SetTexture(FFTVertical_kernel, "ButterflyRT", ButterflyRT);

            //Horizontal IFFT
            for (int stageIndex = 0; stageIndex < N; ++stageIndex)
            {
                OceanSimulationCS.SetInt("Stage", stageIndex);
                OceanSimulationCS.SetTexture(FFTHorizontal_kernel, "FFTInputRT", PingTexture);
                OceanSimulationCS.SetTexture(FFTHorizontal_kernel, "FFTOutputRT", PongTexture);
                OceanSimulationCS.Dispatch(FFTHorizontal_kernel, Size / 8, Size / 8, 1);
                Graphics.Blit(PongTexture, PingTexture);
            }

            //Vertical IFFT
            for (int stageIndex = 0; stageIndex < N; ++stageIndex)
            {
                OceanSimulationCS.SetInt("Stage", stageIndex);
                OceanSimulationCS.SetTexture(FFTVertical_kernel, "FFTInputRT", PingTexture);
                OceanSimulationCS.SetTexture(FFTVertical_kernel, "FFTOutputRT", PongTexture);
                OceanSimulationCS.Dispatch(FFTVertical_kernel, Size / 8, Size / 8, 1);
                Graphics.Blit(PongTexture, PingTexture);
            }

            return PingTexture;
        }

        void CaculateNormal()
        {
            //Normal Spectrum
            int normalSpectrum_kernel = OceanSimulationCS.FindKernel("NormalSpectrumCSMain");
            int normal_kernel = OceanSimulationCS.FindKernel("GenerationNormalCSMain");
            OceanSimulationCS.SetTexture(normalSpectrum_kernel, "SpectrumY", SpectrumY);
            OceanSimulationCS.SetTexture(normalSpectrum_kernel, "NormalX", NormalX);
            OceanSimulationCS.SetTexture(normalSpectrum_kernel, "NormalZ", NormalZ);
            OceanSimulationCS.Dispatch(normalSpectrum_kernel, Size / 8, Size / 8, 1);
            var resultX = CaculateFFTNative(NormalX, NormalRT);
            OceanSimulationCS.SetInt("NormalChannel", 0);
            OceanSimulationCS.SetTexture(normal_kernel, "FFTInputRT", resultX);
            OceanSimulationCS.SetTexture(normal_kernel, "NormalRT", NormalRT);
            OceanSimulationCS.Dispatch(normal_kernel, Size / 8, Size / 8, 1);
            var resultZ = CaculateFFTNative(NormalZ, NormalRT);
            OceanSimulationCS.SetInt("NormalChannel", 1);
            OceanSimulationCS.SetTexture(normal_kernel, "FFTInputRT", resultZ);
            OceanSimulationCS.SetTexture(normal_kernel, "NormalRT", NormalRT);
            OceanSimulationCS.Dispatch(normal_kernel, Size / 8, Size / 8, 1);
        }

        void CaculateBubbles()
        {
            // Debug.Log(HZeroTexture);
            int Bubbles_kernel = OceanSimulationCS.FindKernel("GenerationBubblesCSMain");
            OceanSimulationCS.SetTexture(Bubbles_kernel, "BubblesRT", BubblesRT);
            OceanSimulationCS.SetTexture(Bubbles_kernel, "NormalRT", NormalRT);
            OceanSimulationCS.SetTexture(Bubbles_kernel, "DisplacementOutputRT", Displacement);
            OceanSimulationCS.Dispatch(Bubbles_kernel, Size / 8, Size / 8, 1);
        }
    }
}
