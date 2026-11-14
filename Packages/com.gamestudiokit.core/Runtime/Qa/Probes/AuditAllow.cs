// Game Studio Kit (MIT). Marks an object (and its children) as an intended exception to specific world audits, e.g.
// an invisible wall that is meant to be there, a hanging lamp that floats on purpose, a cave below the terrain.
// Every exemption is recorded: the audits list each exempted object with its flag and reason in "allowed", and count
// them in the ROUTE metric audit_exemptions, so exceptions stay visible to reviewers and the gate.
using System;
using UnityEngine;

namespace GameStudioKit.Qa.Probes
{
    [Flags]
    public enum AuditException
    {
        None = 0,
        OrphanCollider = 1 << 0,
        Floating = 1 << 1,
        Sunk = 1 << 2,
        BelowTerrain = 1 << 3,
        ColliderRenderDivergence = 1 << 4,
        Placeholder = 1 << 5,
        AnimatorWithoutController = 1 << 6,
        ExtraCamera = 1 << 7,
        IntendedWall = 1 << 8,
        LodDoubleDraw = 1 << 9,
        TextureDownsample = 1 << 10,
        All = ~0,
    }

    [AddComponentMenu("Game Studio Kit/Audit Allow")]
    [DisallowMultipleComponent]
    public sealed class AuditAllow : MonoBehaviour
    {
        public AuditException allow = AuditException.None;

        [Tooltip("Why this exception is intended (shown in the audit details).")]
        public string reason;

        /// <summary>True when <paramref name="go"/> or a parent carries an AuditAllow with <paramref name="flag"/>, or its
        /// tag is "intended_wall" (IntendedWall and OrphanCollider) or "audit_allow" (any flag).</summary>
        public static bool Allows(GameObject go, AuditException flag) => Allows(go, flag, out _);

        /// <summary>As <see cref="Allows(GameObject, AuditException)"/>, and says why (for the audit's "allowed" list).</summary>
        public static bool Allows(GameObject go, AuditException flag, out string why)
        {
            why = null;
            if (go == null) return false;
            string tag = SafeTag(go);
            if (tag == "audit_allow")
            {
                why = "tag audit_allow";
                return true;
            }
            if (tag == "intended_wall" && (flag == AuditException.IntendedWall || flag == AuditException.OrphanCollider))
            {
                why = "tag intended_wall";
                return true;
            }
            var a = go.GetComponentInParent<AuditAllow>(true);
            if (a == null || (a.allow & flag) == 0) return false;
            why = "AuditAllow on " + WorldAudit.PathOf(a.transform) + (string.IsNullOrWhiteSpace(a.reason) ? " (no reason given)" : ": " + a.reason.Trim());
            return true;
        }

        static string SafeTag(GameObject go)
        {
            try { return go.tag; }
            catch (UnityException) { return null; }
        }
    }

    /// <summary>One recorded exemption: which object skipped which audit, and why.</summary>
    public sealed class AuditExemption
    {
        public string Path;
        public string Flag;
        public string Why;

        public System.Collections.Generic.Dictionary<string, object> ToJsonObject() =>
            new System.Collections.Generic.Dictionary<string, object> { { "path", Path }, { "flag", Flag }, { "why", Why } };
    }
}
