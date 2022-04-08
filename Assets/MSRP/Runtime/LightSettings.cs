using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;


namespace MSRP
{
    [Serializable]
    public enum AreaLightTpye
    {
        None,
        Rect,
        Disk
    }
    
    [Serializable]
    public class LightSettings
    {
        public AreaLightTpye areaLightTpye;

        public float RectWidth, RectHeight;

        public Texture2D SourceTexture;
    }
}