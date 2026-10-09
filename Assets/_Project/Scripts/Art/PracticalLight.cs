using System.Collections.Generic;
using UnityEngine;

namespace Wreckabulary.Art
{
    /// <summary>Visible bulb art remains independent of the small budget of real lights.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Light))]
    public sealed class PracticalLight : MonoBehaviour
    {
        static readonly List<PracticalLight> registered = new();
        public static IReadOnlyList<PracticalLight> Registered => registered;
        [SerializeField] Light lamp;
        public Light Lamp => lamp;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetRegistry() => registered.Clear();

        public void Configure(Color color, float intensity, float range)
        {
            lamp = GetComponent<Light>();
            lamp.type = LightType.Point;
            lamp.color = color;
            lamp.intensity = Mathf.Max(0f, intensity);
            lamp.range = Mathf.Max(.1f, range);
            lamp.shadows = LightShadows.None;
            lamp.renderMode = LightRenderMode.Auto;
            lamp.enabled = false;
        }

        void Awake() { if (!lamp) lamp = GetComponent<Light>(); }
        void OnEnable()
        {
            if (!lamp) lamp = GetComponent<Light>();
            lamp.enabled = false;
            if (!registered.Contains(this)) registered.Add(this);
        }
        void OnDisable()
        {
            registered.Remove(this);
            if (lamp) lamp.enabled = false;
        }

        public void SetSelected(bool selected)
        {
            if (lamp) lamp.enabled = selected && isActiveAndEnabled;
        }
    }
}
