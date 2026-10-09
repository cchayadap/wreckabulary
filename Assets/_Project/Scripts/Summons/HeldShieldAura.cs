using UnityEngine;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    /// <summary>A raised-only outline; all protection and durability remain owned by the held shield.</summary>
    [DisallowMultipleComponent]
    public sealed class HeldShieldAura : MonoBehaviour
    {
        HeldWeapon gear;
        PlayerController owner;
        Transform aura;
        readonly LineRenderer[] arcs = new LineRenderer[8];
        Color color;
        float flash;
        public bool Visible => aura && arcs[0] && arcs[0].enabled;
        public Transform Aura => aura;

        void Awake() { gear = GetComponent<HeldWeapon>(); color = GameFeedback.SkillColor(gear.word); }
        void OnEnable() => Bind();
        void OnTransformParentChanged() { if (isActiveAndEnabled) Bind(); }
        void OnDisable() => Unbind();
        void OnDestroy() => Unbind();

        void Bind()
        {
            var next = GetComponentInParent<PlayerController>();
            if (owner == next) return;
            Unbind();
            owner = next;
            if (!owner) return;
            owner.Health.Damaged += OnHit;
            aura = new GameObject("Raised shield aura").transform;
            aura.SetParent(owner.transform, false);
            aura.localPosition = Vector3.up * .56f;
            for (int i = 0; i < arcs.Length; i++)
            {
                var segment = new GameObject("Guard arc").transform;
                segment.SetParent(aura, false);
                var points = new Vector3[7];
                for (int p = 0; p < points.Length; p++)
                {
                    float angle = (i * 45f + p * 5f) * Mathf.Deg2Rad;
                    points[p] = new Vector3(Mathf.Sin(angle) * .82f, 0f, Mathf.Cos(angle) * .82f);
                }
                arcs[i] = SummonEffects.Line(segment, color, points);
                arcs[i].enabled = false;
            }
        }

        void OnHit(PlayerHealth _, HitInfo hit, HitResult result)
        { if (result.Blocked && owner && owner.Combat.Weapon == gear) flash = 1f; }

        void LateUpdate()
        {
            if (!owner || !aura) return;
            bool raised = owner.Combat.Weapon == gear && owner.Health.RaisedShield == gear.Shield;
            flash = Mathf.MoveTowards(flash, 0f, Time.deltaTime * 5f);
            aura.localRotation = Quaternion.Euler(0f, Time.time * 18f, 0f);
            aura.localScale = Vector3.one * (1f + flash * .18f);
            foreach (var arc in arcs)
            {
                arc.enabled = raised;
                arc.startWidth = arc.endWidth = .035f + flash * .045f;
                arc.startColor = arc.endColor = Color.Lerp(Color.white, new Color(1f, 1f, .75f), flash);
            }
        }

        void Unbind()
        {
            if (owner) owner.Health.Damaged -= OnHit;
            owner = null;
            if (aura) { aura.gameObject.SetActive(false); Destroy(aura.gameObject); }
            aura = null;
            flash = 0f;
        }
    }
}
