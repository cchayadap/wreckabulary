using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Wreckabulary
{
    public sealed class LobbyDrag : MonoBehaviour, IBeginDragHandler, IDragHandler
    {
        public const float DegreesPerPixel = .4f;

        public Action<float> Turned;
        public Action Started;

        public void OnBeginDrag(PointerEventData data) => Started?.Invoke();

        public void OnDrag(PointerEventData data) =>
            Turned?.Invoke(-data.delta.x * DegreesPerPixel * 1080f / Mathf.Max(1, Screen.height));
    }
}
