using UnityEngine;

namespace Wreckabulary
{
    public sealed class LobbyPop : MonoBehaviour
    {
        public const float Seconds = .18f, VanishSeconds = .3f;
        CanvasGroup group;
        RectTransform rect;
        Vector2 rest, from;
        float start = -1f;
        bool vanishing, placed;

        public static LobbyPop On(Component target) =>
            target.TryGetComponent(out LobbyPop pop) ? pop : target.gameObject.AddComponent<LobbyPop>();

        public void Play(Vector2 offset)
        {
            Setup();
            vanishing = false;
            from = offset;
            start = Time.unscaledTime;
            Apply(0f);
        }

        public void Vanish()
        {
            Setup();
            if (!isActiveAndEnabled) { Destroy(gameObject); return; }
            vanishing = true;
            from = Vector2.zero;
            start = Time.unscaledTime;
        }

        void Setup()
        {
            if (!rect) rect = (RectTransform)transform;
            if (!group && !TryGetComponent(out group)) group = gameObject.AddComponent<CanvasGroup>();
            if (!placed) { rest = rect.anchoredPosition; placed = true; }
        }

        void Update()
        {
            if (start < 0f) return;
            float t = Mathf.Clamp01((Time.unscaledTime - start) / (vanishing ? VanishSeconds : Seconds));
            if (vanishing)
            {
                group.alpha = 1f - t;
                if (t >= 1f) { start = -1f; Destroy(gameObject); }
                return;
            }
            Apply(1f - (1f - t) * (1f - t) * (1f - t));
            if (t >= 1f) start = -1f;
        }

        void Apply(float k)
        {
            group.alpha = k;
            rect.anchoredPosition = rest + from * (1f - k);
        }

        void OnDisable()
        {
            if (start < 0f) return;
            start = -1f;
            if (vanishing) Destroy(gameObject);
            else Apply(1f);
        }
    }
}
