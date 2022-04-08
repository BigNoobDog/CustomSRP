using UnityEngine;

namespace MSRP
{
    [DisallowMultipleComponent, RequireComponent(typeof(Light))]
    public class CustomRenderPipelineLight : MonoBehaviour
    {
        [SerializeField]
        LightSettings settings = default;

        public LightSettings Settings => settings ?? (settings = new LightSettings());
    }
}