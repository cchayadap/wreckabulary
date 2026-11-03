using UnityEngine;

namespace Wreckabulary.Art
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-1000)]
    public sealed class EditorScenePreview : MonoBehaviour
    {
        void Awake()
        {
            if (Application.isPlaying) gameObject.SetActive(false);
        }
    }
}
