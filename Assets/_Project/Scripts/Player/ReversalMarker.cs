using UnityEngine;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    [DisallowMultipleComponent]
    public sealed class ReversalMarker : MonoBehaviour
    {
        const int Arrows = 2;
        const float Lift = .3f, ArcRadius = .36f, ArrowSpan = 140f, SpinDegrees = 220f, Width = .06f, BobHeight = .05f, BobSpeed = 6f;

        PlayerController player;
        Transform root;
        ReverseMode shown;

        public Transform Root => root;

        public void Initialize(PlayerController owner) => player = owner;

        void LateUpdate()
        {
            var mode = player ? player.Reversed : ReverseMode.None;
            if (mode != shown) Show(mode);
            if (!root) return;
            root.SetPositionAndRotation(player.OverheadPosition + Vector3.up * (Lift + Mathf.Sin(Time.time * BobSpeed) * BobHeight), Turn(mode, Time.time * SpinDegrees));
        }

        static Quaternion Turn(ReverseMode mode, float spin) => mode switch
        {
            ReverseMode.Mirror => Quaternion.AngleAxis(spin, Vector3.forward) * Quaternion.Euler(90f, 0f, 0f),
            ReverseMode.Flip => Quaternion.AngleAxis(spin, Vector3.right) * Quaternion.Euler(0f, 0f, 90f),
            _ => Quaternion.AngleAxis(spin, Vector3.up),
        };

        void Show(ReverseMode mode)
        {
            shown = mode;
            if (root) Destroy(root.gameObject);
            root = null;
            if (mode == ReverseMode.None || !player) return;
            root = new GameObject("Reversed arrows").transform;
            root.SetParent(player.transform, false);
            var colour = ReverseZone.Tint(mode);
            for (int i = 0; i < Arrows; i++)
            {
                var arrow = ReverseZone.Arrow(root, colour, ArcRadius, i * 360f / Arrows, ArrowSpan);
                arrow.startWidth = arrow.endWidth = Width;
            }
        }

        void OnDisable() => Show(ReverseMode.None);
    }
}
