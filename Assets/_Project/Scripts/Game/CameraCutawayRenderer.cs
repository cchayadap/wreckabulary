using UnityEngine;

namespace Wreckabulary
{
    /// <summary>Per-camera visibility requires the regular renderer path, not a frame-cached GPU batch.</summary>
    [DisallowMultipleComponent]
    public sealed class CameraCutawayRenderer : MonoBehaviour
    {
        Renderer owner;
        void Awake() => owner = GetComponent<Renderer>();
        void OnDestroy() => CameraCutaway.UnregisterRenderer(owner, this);

        // Unity excludes renderers with instance callbacks from GPU Resident Drawer. The callback's
        // presence keeps camera-local shadow/visibility changes synchronous with native culling.
        void OnWillRenderObject() { }
    }
}
