using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Wreckabulary
{
    public sealed class LobbyShade : MonoBehaviour, IPointerClickHandler
    {
        public Action Clicked;

        public void OnPointerClick(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left || e.pointerPressRaycast.gameObject != gameObject) return;
            Clicked?.Invoke();
        }
    }
}
