using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Wreckabulary
{
    public sealed class LobbyPress : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
    {
        public RectTransform Body;
        public float Lift = 2f, Sink = 2f, Tilt, Scale = 1f, HoverScale = 1f;
        public float Lean;
        public Shadow Drop;
        public float DropRest, DropPressed = 1f;
        public Graphic Ring;
        public bool FadeOff = true;
        public Action<bool> Hot;
        public Action<bool> Pressed;

        Selectable selectable;
        CanvasGroup fade;
        bool over, down, selected, ringed;
        bool? wasHot, wasOff, wasPressed;

        static int checkedFrame;
        static bool navigating;

        public static bool Navigating
        {
            get
            {
                if (checkedFrame == Time.frameCount) return navigating;
                checkedFrame = Time.frameCount;
                var mouse = Mouse.current;
                if (mouse != null && (mouse.delta.ReadValue().sqrMagnitude > 4f || mouse.leftButton.wasPressedThisFrame)) navigating = false;
                else if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) navigating = true;
                else
                    foreach (var pad in Gamepad.all)
                        if (pad.leftStick.ReadValue().sqrMagnitude > .25f || pad.dpad.ReadValue().sqrMagnitude > .25f ||
                            pad.buttonSouth.wasPressedThisFrame || pad.buttonEast.wasPressedThisFrame)
                        { navigating = true; break; }
                return navigating;
            }
        }

        public bool IsHot => wasHot == true;

        void Awake() => selectable = GetComponent<Selectable>();

        public void OnPointerEnter(PointerEventData data) => over = true;
        public void OnPointerExit(PointerEventData data) { over = false; down = false; }
        public void OnPointerDown(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Left) return;
            down = true;
            ringed = false;
        }
        public void OnPointerUp(PointerEventData data) => down = false;
        public void OnSelect(BaseEventData data) { selected = true; ringed = true; }
        public void OnDeselect(BaseEventData data) { selected = false; ringed = false; }

        void OnDisable()
        {
            over = down = false;
            wasHot = null;
            if (wasPressed == true) Pressed?.Invoke(false);
            wasPressed = null;
            if (!Body) return;
            Body.anchoredPosition = Vector2.zero;
            Body.localRotation = Quaternion.Euler(0, 0, Lean);
            Body.localScale = Vector3.one * Scale;
            SetDrop(DropRest);
        }

        void Update()
        {
            if (!Body) return;
            bool off = selectable && !selectable.IsInteractable();
            bool focus = selected && Navigating;
            bool hot = !off && (over || focus);
            if (wasOff != off)
            {
                wasOff = off;
                if (off && FadeOff && !fade) fade = Body.gameObject.AddComponent<CanvasGroup>();
                if (fade) fade.alpha = off && FadeOff ? .45f : 1f;
            }
            if (wasHot != hot) { wasHot = hot; Hot?.Invoke(hot); }
            bool pressing = !off && down && over;
            if (wasPressed != pressing) { wasPressed = pressing; Pressed?.Invoke(pressing); }
            bool showRing = focus && ringed && !off;
            if (Ring && Ring.enabled != showRing) Ring.enabled = showRing;

            float y = off ? 0f : down && over ? -Sink : hot ? Lift : 0f;
            float k = 1f - Mathf.Exp(-25f * Time.unscaledDeltaTime);
            var position = Vector2.Lerp(Body.anchoredPosition, new Vector2(0f, y), k);
            if ((position - new Vector2(0f, y)).sqrMagnitude < .0004f) position = new Vector2(0f, y);
            if (Body.anchoredPosition != position) Body.anchoredPosition = position;
            float tilt = (hot && !down ? Tilt : 0f) + Lean;
            var targetRotation = Quaternion.Euler(0, 0, tilt);
            if (Quaternion.Angle(Body.localRotation, targetRotation) > .01f)
                Body.localRotation = Quaternion.Slerp(Body.localRotation, targetRotation, k);
            float scale = Scale * (hot && !down ? HoverScale : 1f);
            var targetScale = Vector3.one * scale;
            if ((Body.localScale - targetScale).sqrMagnitude > .000001f)
                Body.localScale = Vector3.Lerp(Body.localScale, targetScale, k);
            SetDrop(Mathf.Max(DropPressed, DropRest + position.y));
        }

        void SetDrop(float depth)
        {
            if (!Drop || DropRest <= 0f) return;
            var distance = new Vector2(0f, -Mathf.Round(depth * 2f) / 2f);
            if (Drop.effectDistance != distance) Drop.effectDistance = distance;
        }
    }
}
