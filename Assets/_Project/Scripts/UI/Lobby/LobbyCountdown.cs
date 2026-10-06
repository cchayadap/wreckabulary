using System;
using TMPro;
using UnityEngine;

namespace Wreckabulary
{
    public sealed class LobbyCountdown : MonoBehaviour
    {
        public float Ends;
        public Action Done;
        TextMeshProUGUI label;
        Func<int, string> words;
        int shown = -1;

        public void Show(TextMeshProUGUI target, Func<int, string> say)
        {
            label = target;
            words = say;
            shown = -1;
            Update();
        }

        void Update()
        {
            float left = Ends - Time.unscaledTime;
            if (left <= 0f)
            {
                var done = Done;
                Done = null;
                done?.Invoke();
                return;
            }
            int seconds = Mathf.CeilToInt(left);
            if (seconds == shown || !label || words == null) return;
            shown = seconds;
            label.text = words(seconds);
        }
    }
}
