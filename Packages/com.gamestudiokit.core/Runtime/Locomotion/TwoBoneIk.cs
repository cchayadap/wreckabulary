// Game Studio Kit (MIT). Analytic two-bone IK (law of cosines) for legs and arms on any rig. Runs in LateUpdate after
// the Animator, so it also works on Generic rigs, where Unity's OnAnimatorIK is unavailable.
using UnityEngine;

namespace GameStudioKit.Locomotion
{
    public static class TwoBoneIk
    {
        /// <summary>
        /// Rotates <paramref name="upper"/> and <paramref name="lower"/> so <paramref name="end"/> reaches
        /// <paramref name="target"/> (clamped to the chain's reach), bending toward <paramref name="hint"/> when given.
        /// <paramref name="weight"/> blends from the incoming pose (0) to the solved pose (1). The end bone's own
        /// rotation is left to the caller.
        /// </summary>
        public static void Solve(Transform upper, Transform lower, Transform end, Vector3 target, Vector3? hint, float weight,
            Vector3 fallbackBendNormal = default)
        {
            if (upper == null || lower == null || end == null || weight <= 0f) return;
            Quaternion upperStart = upper.rotation;
            Quaternion lowerStart = lower.rotation;

            Vector3 a = upper.position;
            Vector3 b = lower.position;
            Vector3 c = end.position;
            float lab = (b - a).magnitude;
            float lbc = (c - b).magnitude;
            if (lab < 1e-5f || lbc < 1e-5f) return;
            float lat = Mathf.Clamp((target - a).magnitude, Mathf.Abs(lab - lbc) + 1e-4f, lab + lbc - 1e-4f);

            // Bend plane normal: the current bend, else the hint, else the fallback.
            Vector3 ab = b - a;
            Vector3 ac = c - a;
            Vector3 n = Vector3.Cross(ac, ab);
            if (n.sqrMagnitude < 1e-10f && hint.HasValue) n = Vector3.Cross(target - a, hint.Value - a);
            if (n.sqrMagnitude < 1e-10f) n = Vector3.Cross(ac, fallbackBendNormal == Vector3.zero ? Vector3.right : fallbackBendNormal);
            if (n.sqrMagnitude < 1e-10f) return;
            n.Normalize();

            // Set the middle joint's interior angle so the upper→end distance equals the target distance. A positive
            // rotation about m = (a−b)×(c−b) moves c−b away from a−b, opening the joint.
            float curB = Vector3.Angle(a - b, c - b);
            float wantB = Mathf.Acos(Mathf.Clamp((lab * lab + lbc * lbc - lat * lat) / (2f * lab * lbc), -1f, 1f)) * Mathf.Rad2Deg;
            Vector3 m = Vector3.Cross(a - b, c - b);
            if (m.sqrMagnitude < 1e-10f) m = -n;
            m.Normalize();
            lower.rotation = Quaternion.AngleAxis(wantB - curB, m) * lower.rotation;

            // Swing the whole chain so the end points at the target.
            Vector3 cNow = end.position - upper.position;
            Vector3 toTarget = target - upper.position;
            if (cNow.sqrMagnitude > 1e-10f && toTarget.sqrMagnitude > 1e-10f)
                upper.rotation = Quaternion.FromToRotation(cNow, toTarget) * upper.rotation;

            // Twist around the upper→target axis so the middle joint faces the hint.
            if (hint.HasValue)
            {
                Vector3 axis = toTarget.normalized;
                Vector3 mid = Vector3.ProjectOnPlane(lower.position - upper.position, axis);
                Vector3 want = Vector3.ProjectOnPlane(hint.Value - upper.position, axis);
                if (mid.sqrMagnitude > 1e-8f && want.sqrMagnitude > 1e-8f)
                    upper.rotation = Quaternion.AngleAxis(Vector3.SignedAngle(mid, want, axis), axis) * upper.rotation;
            }

            if (weight < 1f)
            {
                upper.rotation = Quaternion.Slerp(upperStart, upper.rotation, weight);
                lower.rotation = Quaternion.Slerp(lowerStart, lower.rotation, weight);
            }
        }
    }
}
