using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

namespace MSRP
{
    [Serializable]
    public class CameraSettings {

        public bool copyColor = true, copyDepth = true;

        [RenderingLayerMaskField]
        public int renderingLayerMask = -1;

        public bool maskLights = false;

        public enum RenderScaleMode { Inherit, Multiply, Override }

        public RenderScaleMode renderScaleMode = RenderScaleMode.Inherit;

        [Range(CameraRenderer.renderScaleMin, CameraRenderer.renderScaleMax)]
        public float renderScale = 1f;

        [FormerlySerializedAs("overridePostFX")] public bool overridePostProcessing = false;

        public PostProcessingSetting postFXSettings = default;

        public bool allowFXAA = false;

        public bool keepAlpha = false;

        [Serializable]
        public struct FinalBlendMode {

            public BlendMode source, destination;
        }

        public FinalBlendMode finalBlendMode = new FinalBlendMode {
            source = BlendMode.One,
            destination = BlendMode.Zero
        };

        public float GetRenderScale (float scale) {
            return
                renderScaleMode == RenderScaleMode.Inherit ? scale :
                renderScaleMode == RenderScaleMode.Override ? renderScale :
                scale * renderScale;
        }
    }
}