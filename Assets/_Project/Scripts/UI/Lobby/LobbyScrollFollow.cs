using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Wreckabulary
{
    [RequireComponent(typeof(ScrollRect))]
    public sealed class LobbyScrollFollow : MonoBehaviour
    {
        const float Margin = 14f;
        ScrollRect scroll;
        GameObject last;

        void Awake() => scroll = GetComponent<ScrollRect>();

        void LateUpdate()
        {
            var system = EventSystem.current;
            var selected = system ? system.currentSelectedGameObject : null;
            if (selected == last) return;
            last = selected;
            if (selected && scroll.content && selected.transform.IsChildOf(scroll.content))
                Show(scroll, (RectTransform)selected.transform);
        }

        public static void Show(ScrollRect scroll, RectTransform target)
        {
            Canvas.ForceUpdateCanvases();
            var view = scroll.viewport ? scroll.viewport : (RectTransform)scroll.transform;
            var content = scroll.content;
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(view, target);
            var shown = view.rect;
            float over = 0f;
            if (bounds.max.y + Margin > shown.yMax) over = bounds.max.y + Margin - shown.yMax;
            else if (bounds.min.y - Margin < shown.yMin) over = bounds.min.y - Margin - shown.yMin;
            if (over == 0f) return;
            var position = content.anchoredPosition;
            position.y = Mathf.Clamp(position.y - over, 0f, Mathf.Max(0f, content.rect.height - shown.height));
            content.anchoredPosition = position;
            scroll.velocity = Vector2.zero;
        }
    }
}
