using System;
using System.Collections.Generic;
using UnityEngine;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Online
{
    [Serializable]
    public sealed class WordboundItemHold
    {
        public string VisualKey = "";
        [Tooltip("Align this item's authored Grip_R frame with the character's grip socket. Disable to retain its previous root orientation.")]
        public bool AlignGrip = true;
        [Tooltip("Rotation trim in the hand socket's axes, in degrees. Pivots around Grip_R without moving contact. Zero uses the authored socket frame.")]
        public Vector3 RotationTrim;
    }

    [Serializable]
    public sealed class WordboundHoldingPose
    {
        [Tooltip("Apply only the explicit item entries below. Other item families retain their previous placement.")]
        public bool Enabled = true;
        public List<WordboundItemHold> Items = new List<WordboundItemHold> {
            new WordboundItemHold { VisualKey = "BAT" },
            new WordboundItemHold { VisualKey = "HAMMER" } };

        public void Validate()
        {
            if (Items == null || Items.Count > 64) throw new ArgumentException("Use at most 64 explicit held-item poses.");
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in Items)
                if (item == null || !WordboundIdentity.IsId(item.VisualKey) || item.VisualKey == "player" ||
                    item.VisualKey.StartsWith("letter:", StringComparison.Ordinal) || !keys.Add(item.VisualKey) || !ValidTrim(item.RotationTrim))
                    throw new ArgumentException("Held poses need unique item keys and finite rotation trims within 180 degrees.");
        }

        public Quaternion RootRotationInHand(string key, bool hasAuthoredGrip, Quaternion authoredGripRotation)
        {
            if (!Enabled || !hasAuthoredGrip) return Quaternion.identity;
            if (Items == null) throw new ArgumentException("Held-item poses cannot be null.");
            foreach (var item in Items)
            {
                if (item == null || item.VisualKey != key) continue;
                if (!item.AlignGrip) return Quaternion.identity;
                if (!ValidTrim(item.RotationTrim)) throw new ArgumentException("Held rotation trim must be finite and within 180 degrees.");
                return Quaternion.Euler(item.RotationTrim) * Quaternion.Inverse(authoredGripRotation);
            }
            return Quaternion.identity;
        }

        static bool ValidTrim(Vector3 value) => Good(value.x) && Good(value.y) && Good(value.z);
        static bool Good(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && Mathf.Abs(value) <= 180;
    }
}
