using UnityEngine;

namespace Wreckabulary
{
    [DisallowMultipleComponent]
    public sealed class CameraCutawayRenderer : MonoBehaviour
    {
        Renderer owner;
        void Awake() => owner = GetComponent<Renderer>();
        void OnDestroy() => CameraCutaway.UnregisterRenderer(owner, this);

        void OnWillRenderObject() { }
    }
}
