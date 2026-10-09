using UnityEngine;

namespace Wreckabulary
{
    public sealed class LobbyFit : MonoBehaviour
    {
        public RectTransform Target;

        void OnEnable() => Fit();
        void OnRectTransformDimensionsChange() => Fit();

        public void Fit()
        {
            if (!Target || Target.sizeDelta.x <= 0f) return;
            float width = ((RectTransform)transform).rect.width;
            float scale = width > 0f ? Mathf.Min(1f, width / Target.sizeDelta.x) : 1f;
            Target.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
