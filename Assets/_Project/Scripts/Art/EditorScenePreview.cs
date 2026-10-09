using UnityEngine;

namespace Wreckabulary.Art
{
    /// <summary>Hides an authored scene preview when entering editor Play; EditorOnly-tagged roots are stripped from builds.</summary>
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
