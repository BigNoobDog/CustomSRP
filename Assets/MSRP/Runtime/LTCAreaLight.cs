using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace MSRP
{
    public partial class LTCAreaLight
    {
        static LTCAreaLight s_Instance;

        public static LTCAreaLight instance
        {
            get
            {
                if (s_Instance == null)
                    s_Instance = new LTCAreaLight();

                return s_Instance;
            }
        }
        
        public const int k_LtcLUTMatrixDim = 3; // size of the matrix (3x3)
        public const int k_LtcLUTResolution = 64;
        
        public Texture2DArray m_LtcData;
        
        public static void LoadLUT(Texture2DArray tex, int arrayElement, TextureFormat format, double[,] LUTTransformInv)
        {
            const int count = k_LtcLUTResolution * k_LtcLUTResolution;
            Color[] pixels = new Color[count];

            float clampValue = (format == TextureFormat.RGBAHalf) ? 65504.0f : float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                // Both GGX and Disney Diffuse BRDFs have zero values in columns 1, 3, 5, 7.
                // Column 8 contains only ones.
                pixels[i] = new Color(Mathf.Min(clampValue, (float)LUTTransformInv[i, 0]),
                    Mathf.Min(clampValue, (float)LUTTransformInv[i, 2]),
                    Mathf.Min(clampValue, (float)LUTTransformInv[i, 4]),
                    Mathf.Min(clampValue, (float)LUTTransformInv[i, 6]));
            }

            tex.SetPixels(pixels, arrayElement);
        }
        
        // Load LUT with one scalar in alpha of a tex2D
        public static void LoadLUT(Texture2DArray tex, int arrayElement, TextureFormat format, float[] LUTScalar)
        {
            const int count = k_LtcLUTResolution * k_LtcLUTResolution;
            Color[] pixels = new Color[count];

            for (int i = 0; i < count; i++)
            {
                pixels[i] = new Color(0, 0, 0, LUTScalar[i]);
            }

            tex.SetPixels(pixels, arrayElement);
        }

        public void BuildLUT()
        {
            m_LtcData = new Texture2DArray(k_LtcLUTResolution, k_LtcLUTResolution, 3, TextureFormat.RGBAHalf, false /*mipmap*/, true /* linear */)
            {
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = MSRPCoreUtils.GetTextureAutoName(k_LtcLUTResolution, k_LtcLUTResolution, TextureFormat.RGBAHalf, depth: 2, dim: TextureDimension.Tex2DArray, name: "LTC_LUT")
            };
            
            LoadLUT(m_LtcData, 0, TextureFormat.RGBAHalf, s_LtcGGXMatrixData);
            LoadLUT(m_LtcData, 1, TextureFormat.RGBAHalf, s_LtcDisneyDiffuseMatrixData);

            
            m_LtcData.Apply();
        }

        public void CleanUp()
        {
            MSRPCoreUtils.Destroy(m_LtcData);
        }
        
        public void Bind(CommandBuffer cmd)
        {
            cmd.SetGlobalTexture("_LtcData", m_LtcData);
        }
    }
}