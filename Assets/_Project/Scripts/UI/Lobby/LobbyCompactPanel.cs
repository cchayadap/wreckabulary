using UnityEngine;

namespace Wreckabulary
{
    public sealed class LobbyCompactPanel : MonoBehaviour
    {
        public Vector2 MaximumSize = new Vector2(1100, 600);
        bool fitting;
        RectTransform sizedParent;
        Vector2 parentSize, appliedMaximum;

        void OnEnable() => Fit();
        void OnRectTransformDimensionsChange() => Fit();
        void OnTransformParentChanged() => Fit();

        void LateUpdate()
        {
            if (sizedParent && (sizedParent.rect.size != parentSize || MaximumSize != appliedMaximum)) Fit();
        }

        public void Fit()
        {
            if (fitting || !(transform is RectTransform panel) || !(panel.parent is RectTransform parent)) return;
            sizedParent = parent;
            var available = parent.rect.size;
            if (available.x <= 0f || available.y <= 0f) return;
            fitting = true;
            try
            {
                parentSize = available;
                appliedMaximum = MaximumSize;
                panel.anchorMin = panel.anchorMax = panel.pivot = Vector2.one;
                panel.anchoredPosition = Vector2.zero;
                panel.sizeDelta = new Vector2(Mathf.Min(MaximumSize.x, available.x), Mathf.Min(MaximumSize.y, available.y));
            }
            finally { fitting = false; }
        }
    }
}
