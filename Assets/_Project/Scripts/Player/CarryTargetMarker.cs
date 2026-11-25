using System.Collections.Generic;
using UnityEngine;

namespace Wreckabulary
{
    public sealed class CarryTargetMarker : MonoBehaviour
    {
        const float RefreshSeconds = .1f;
        const float Lift = .04f;
        const float Margin = .12f;
        const float MinRadius = .3f, MaxRadius = 1.4f;
        const float Width = .06f;
        const float PulseRate = 6f, PulseShare = .06f;

        PlayerController player;
        LineRenderer ring;
        Rigidbody target;
        Color colour;
        float nextRefresh;
        readonly List<Collider> shapes = new();

        public Rigidbody Target => target;
        public bool Showing => ring && ring.enabled;

        public void Initialize(PlayerController owner) => player = owner;

        void Awake()
        {
            if (!player) player = GetComponent<PlayerController>();
        }

        bool Wanted()
        {
            if (!player || !player.isActiveAndEnabled || player.Binding == null || player.Binding is BotBinding) return false;
            if (!player.CanAct || player.IsDodging) return false;
            var combat = player.Combat;
            return combat && !combat.IsChanneling && !combat.IsReviving && !(combat.IsHolding && !combat.Weapon);
        }

        void LateUpdate()
        {
            if (Time.timeScale <= 0f) return;
            if (Time.time >= nextRefresh)
            {
                nextRefresh = Time.time + RefreshSeconds;
                Choose(Wanted() ? player.Combat.GrabTarget() : null);
            }
            if (target && !target.gameObject.activeInHierarchy) Choose(null);
            if (!target) { Hide(); return; }
            Show();
        }

        void Choose(Rigidbody next)
        {
            if (next == target) return;
            target = next;
            shapes.Clear();
            if (!target) return;
            target.GetComponentsInChildren(shapes);
            colour = GameHud.SeatColour(player);
            if (ring) ring.sharedMaterial = PlayerHud.Mark(colour);
        }

        void Show()
        {
            if (!ring) ring = MakeRing();
            if (!TargetBounds(out var bounds)) { Hide(); return; }
            float radius = Mathf.Clamp(Mathf.Max(bounds.extents.x, bounds.extents.z) + Margin, MinRadius, MaxRadius);
            float pulse = 1f + Mathf.Sin(Time.time * PulseRate) * PulseShare;
            ring.transform.position = new Vector3(bounds.center.x, bounds.min.y + Lift, bounds.center.z);
            ring.transform.localScale = Vector3.one * (radius * pulse);
            if (!ring.enabled) ring.enabled = true;
        }

        bool TargetBounds(out Bounds bounds)
        {
            bounds = default;
            bool any = false;
            for (int i = 0; i < shapes.Count; i++)
            {
                var shape = shapes[i];
                if (!shape || !shape.enabled || shape.isTrigger) continue;
                if (any) bounds.Encapsulate(shape.bounds);
                else { bounds = shape.bounds; any = true; }
            }
            return any;
        }

        LineRenderer MakeRing()
        {
            var root = new GameObject("Grab target marker").transform;
            var line = SummonEffects.Ring(root, "Ring", colour, 1f, Quaternion.Euler(90f, 0f, 0f));
            line.startWidth = line.endWidth = Width;
            line.sharedMaterial = PlayerHud.Mark(colour);
            line.enabled = false;
            return line;
        }

        void Hide()
        {
            if (ring && ring.enabled) ring.enabled = false;
        }

        void OnDisable()
        {
            target = null;
            shapes.Clear();
            Hide();
        }

        void OnDestroy()
        {
            if (ring) Destroy(ring.transform.parent ? ring.transform.parent.gameObject : ring.gameObject);
        }
    }
}
