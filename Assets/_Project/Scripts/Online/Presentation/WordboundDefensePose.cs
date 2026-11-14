using System;
using UnityEngine;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Online
{
    [Serializable]
    public sealed class WordboundDefensePose
    {
        [Tooltip("Hand-placement correction in the character's local axes. Zero preserves the authored grip contact.")]
        public Vector3 ShieldOffset;
        [Tooltip("Raised shield rotation in the character's local axes, pivoted around its authored grip.")]
        public Vector3 ShieldEuler;
        [Tooltip("Hand-placement correction in the character's local axes. Zero preserves the authored grip contact.")]
        public Vector3 PlateOffset;
        [Tooltip("The plate starts horizontal; 90 degrees around X presents its face forward.")]
        public Vector3 PlateEuler = new Vector3(90, 0, 0);
        [Range(0, .5f)] public float BlendSeconds = .12f;

        public void Validate()
        {
            if (!Bounded(ShieldOffset, .25f) || !Bounded(PlateOffset, .25f) ||
                !Bounded(ShieldEuler, 180) || !Bounded(PlateEuler, 180) ||
                !Finite(BlendSeconds) || BlendSeconds < 0 || BlendSeconds > .5f)
                throw new ArgumentException("Defense pose needs finite offsets within 0.25m, angles within 180 degrees and blend time within 0.5s.");
        }

        public bool TryGet(string key, out Vector3 offset, out Vector3 euler)
        {
            Validate();
            if (key == "SHIELD") { offset = ShieldOffset; euler = ShieldEuler; return true; }
            if (key == "PLATE") { offset = PlateOffset; euler = PlateEuler; return true; }
            offset = euler = default;
            return false;
        }

        public static bool IsActive(MatchDefenseState defense, string itemId, long tick) =>
            defense != null && defense.ItemEntityId == itemId && defense.StartedTick <= tick && tick < defense.UntilTick;

        public static void PlaceAroundGrip(Transform visual, Vector3 grip, Transform hand,
            Transform character, Vector3 offset, Vector3 euler, float amount)
        {
            if (!visual || !hand || !character || !Bounded(grip, 100) || !Bounded(offset, .25f) ||
                !Bounded(euler, 180) || !Finite(amount) || amount < 0 || amount > 1)
                throw new ArgumentException("A defensive pose requires valid authored transforms and bounded blend.");
            visual.rotation = Quaternion.Slerp(hand.rotation, character.rotation * Quaternion.Euler(euler), amount);
            Vector3 pivot = hand.position + character.TransformDirection(offset) * amount;
            visual.position = pivot - visual.TransformVector(grip);
        }

        static bool Bounded(Vector3 value, float limit) => Finite(value.x) && Finite(value.y) && Finite(value.z) &&
            Mathf.Abs(value.x) <= limit && Mathf.Abs(value.y) <= limit && Mathf.Abs(value.z) <= limit;
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
