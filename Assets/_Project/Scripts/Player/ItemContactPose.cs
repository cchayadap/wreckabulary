using UnityEngine;
using Wreckabulary.Art;

namespace Wreckabulary
{
    /// <summary>Cosmetic two-bone contact adjustment layered after imported animation. It never changes hit or use timing.</summary>
    sealed class ItemContactPose
    {
        readonly Transform shoulder, elbow, wrist, grip;
        readonly SkinnedMeshRenderer face;
        readonly Quaternion gripCorrection;
        Quaternion shoulderBase, elbowBase, shoulderWritten, elbowWritten;
        float guardWeight;

        public ItemContactPose(GameObject model, Transform rightGrip, SkinnedMeshRenderer head, Quaternion correction)
        {
            shoulder = ModelVisual.FindNamed(model, "upper_arm_R");
            elbow = ModelVisual.FindNamed(model, "forearm_R");
            wrist = ModelVisual.FindNamed(model, "hand_R");
            grip = rightGrip;
            face = head;
            gripCorrection = correction;
        }

        public bool Apply(PlayerController player, bool consuming, bool eating, float progress, out Quaternion itemRotation)
        {
            itemRotation = Quaternion.identity;
            if (!shoulder || !elbow || !wrist || !grip || !face) return false;
            if (shoulder.localRotation == shoulderWritten) shoulder.localRotation = shoulderBase;
            if (elbow.localRotation == elbowWritten) elbow.localRotation = elbowBase;
            shoulderBase = shoulder.localRotation;
            elbowBase = elbow.localRotation;
            var gear = player.Combat ? player.Combat.Weapon : null;
            bool guard = gear && gear.word == "SHIELD" && player.Combat.IsBlocking;
            guardWeight = Mathf.MoveTowards(guardWeight, guard ? 1f : 0f, Time.deltaTime * 10f);
            if (!gear || player.IsKnockedOut || (!consuming && !guard)) return false;
            var forward = player.visual.forward;
            var right = player.visual.right;
            var upright = Quaternion.LookRotation(forward, Vector3.up);
            var mouth = face.bounds.center + forward * (face.bounds.extents.z + .015f) - Vector3.up * face.bounds.extents.y * .40f;
            float weight = guard ? guardWeight : Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress / .25f));
            Vector3 target;
            if (guard)
            {
                target = mouth - Vector3.up * .20f + right * .16f + forward * .16f;
                itemRotation = upright;
            }
            else
            {
                itemRotation = eating ? upright : Quaternion.AngleAxis(55f * weight, forward) * upright;
                float aboveGrip = eating ? .07f : (gear.Definition.Size[1] - gear.Definition.Grip[1]) * gear.Definition.HeldScale;
                target = mouth + forward * .04f - (itemRotation * Vector3.up) * aboveGrip;
            }
            target = Vector3.Lerp(grip.position, target, weight) - (grip.position - wrist.position);
            Solve(target, right * .6f - Vector3.up);
            shoulderWritten = shoulder.localRotation;
            elbowWritten = elbow.localRotation;
            itemRotation = Quaternion.Slerp(grip.rotation * gripCorrection * gear.HoldRotation, itemRotation, weight);
            return true;
        }

        void Solve(Vector3 target, Vector3 bend)
        {
            var origin = shoulder.position;
            float upper = Vector3.Distance(origin, elbow.position), lower = Vector3.Distance(elbow.position, wrist.position);
            var delta = target - origin;
            if (upper < .001f || lower < .001f || delta.sqrMagnitude < .00001f) return;
            float distance = Mathf.Clamp(delta.magnitude, Mathf.Abs(upper - lower) + .001f, upper + lower - .001f);
            var direction = delta.normalized;
            var side = Vector3.ProjectOnPlane(bend, direction).normalized;
            float along = (distance * distance + upper * upper - lower * lower) / (2f * distance);
            float height = Mathf.Sqrt(Mathf.Max(0f, upper * upper - along * along));
            var elbowTarget = origin + direction * along + side * height;
            shoulder.rotation = Quaternion.FromToRotation(elbow.position - origin, elbowTarget - origin) * shoulder.rotation;
            elbow.rotation = Quaternion.FromToRotation(wrist.position - elbow.position,
                origin + direction * distance - elbow.position) * elbow.rotation;
        }
    }
}
