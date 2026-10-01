using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Wreckabulary
{
    /// <summary>A hold-capable skill button, with one pointer owner and visible press feedback.</summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class TouchActionButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public TouchAction action;
        public System.Action pressed;
        public bool sendsInput = true;
        int pointer = int.MinValue;
        Image image;
        Color resting;
        CanvasGroup group;
        bool available = true;

        void Awake()
        {
            image = GetComponent<Image>();
            if (image) resting = image.color;
            group = GetComponent<CanvasGroup>();
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (!available || pointer != int.MinValue) return;
            pointer = e.pointerId;
            if (sendsInput) TouchBinding.Shared.SetHeld(action, true);
            pressed?.Invoke();
            if (image) image.color = Color.Lerp(resting, Color.white, 0.25f);
            transform.localScale = Vector3.one * 0.94f;
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (e.pointerId == pointer) Release();
        }

        void OnDisable() => Release();
        void OnApplicationFocus(bool focused) { if (!focused) Release(); }

        public void SetAvailable(bool value)
        {
            if (available == value) return;
            available = value;
            if (!value) Release();
            if (group) group.alpha = value ? 1f : 0.38f;
        }

        void Release()
        {
            pointer = int.MinValue;
            if (sendsInput) TouchBinding.Shared.SetHeld(action, false);
            if (image) image.color = resting;
            transform.localScale = Vector3.one;
        }
    }
}
