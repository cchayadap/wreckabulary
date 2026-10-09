using TMPro;
using UnityEngine;

namespace Wreckabulary
{
    /// <summary>Floating callout text, like "BLADE!" or "WRECKED!".</summary>
    public class Popup : MonoBehaviour
    {
        const float Life = 1.1f;

        TextMeshPro text;
        Vector3 start;
        float born;
        Color color;
        static Camera cachedCamera;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCamera() => cachedCamera = null;

        public static void Show(string message, Vector3 at, Color color, float size = 4f)
        {
            var go = new GameObject("Popup");
            go.transform.SetParent(World.Transient, false);
            go.transform.position = at;

            var t = go.AddComponent<TextMeshPro>();
            t.font = GameAssets.I.font;
            t.text = message;
            t.fontSize = size;
            t.fontStyle = FontStyles.Bold;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.color = color;
            t.outlineWidth = 0.25f;
            t.outlineColor = GameAssets.I.ink;
            t.rectTransform.sizeDelta = new Vector2(8f, 2f);

            var p = go.AddComponent<Popup>();
            p.text = t;
            p.start = at;
            p.born = Time.time;
            p.color = color;
            go.AddComponent<WorldSpaceBillboard>();
        }

        void LateUpdate()
        {
            float k = (Time.time - born) / Life;
            if (k >= 1f) { Destroy(gameObject); return; }

            transform.position = start + Vector3.up * (k * 1.2f);
            float pop = k < 0.15f ? Mathf.Lerp(0.4f, 1.15f, k / 0.15f) : Mathf.Lerp(1.15f, 1f, (k - 0.15f) * 4f);
            transform.localScale = Vector3.one * pop;
            text.color = new Color(color.r, color.g, color.b, 1f - k * k);
        }

        /// <summary>Turns a world-space label to face the main camera.</summary>
        public static void Billboard(Transform t)
        {
            var rig = CameraRig.Instance;
            var cam = rig ? rig.ViewCamera : cachedCamera;
            if (!cam) cam = cachedCamera = Camera.main;
            if (cam) t.rotation = cam.transform.rotation;
        }
    }
}
