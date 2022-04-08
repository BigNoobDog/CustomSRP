using UnityEngine;

namespace MSRP
{
    [DisallowMultipleComponent, RequireComponent(typeof(Camera))]
    public class CustomRenderPipelineCamera : MonoBehaviour
    {
        [SerializeField]
        CameraSettings settings = default;

        public CameraSettings Settings => settings ?? (settings = new CameraSettings());
    }
}