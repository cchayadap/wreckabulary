using UnityEngine;
using UnityEngine.EventSystems;

namespace Wreckabulary
{
    /// <summary>Captures one pointer so a second finger on another skill cannot steal this stick.</summary>
    public sealed class TouchStick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public RectTransform knob;
        public bool aims;
        public float radius = 60f;
        RectTransform rect;
        Camera worldCamera;
        int pointer = int.MinValue;

        public void Initialise(Camera camera) => worldCamera = camera;

        void Awake() => rect = (RectTransform)transform;

        public void OnPointerDown(PointerEventData e)
        {
            if (pointer != int.MinValue) return;
            pointer = e.pointerId;
            Move(e);
        }

        public void OnDrag(PointerEventData e)
        {
            if (e.pointerId == pointer) Move(e);
        }

        void Move(PointerEventData e)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, e.position, e.pressEventCamera, out var point)) return;
            var value = Vector2.ClampMagnitude(point / radius, 1f);
            if (knob) knob.anchoredPosition = value * radius;
            if (value.magnitude < 0.12f) value = Vector2.zero;
            // Sticks are screen-relative. Their world direction follows the actual arena camera.
            if (worldCamera)
            {
                var right = World.Flat(worldCamera.transform.right).normalized;
                var forward = World.Flat(worldCamera.transform.forward).normalized;
                var world = right * value.x + forward * value.y;
                value = new Vector2(world.x, world.z);
            }
            if (aims) TouchBinding.Shared.SetLook(value);
            else TouchBinding.Shared.SetMove(value);
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (e.pointerId == pointer) Release();
        }

        void OnDisable() => Release();

        public void Release()
        {
            pointer = int.MinValue;
            if (knob) knob.anchoredPosition = Vector2.zero;
            if (aims) TouchBinding.Shared.SetLook(Vector2.zero);
            else TouchBinding.Shared.SetMove(Vector2.zero);
        }
    }
}
