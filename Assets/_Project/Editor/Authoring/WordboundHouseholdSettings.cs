using System;
using System.Linq;
using UnityEngine;

namespace Wreckabulary.EditorTools
{
    public sealed class WordboundHouseholdSettings : ScriptableObject
    {
        [Serializable]
        public sealed class Item
        {
            public string id;
            [Tooltip("Initial height in metres for a missing prefab. Existing prefab edits are preserved; edit the prefab directly to resize it later.")]
            [Range(.1f, 4f)] public float targetHeight = 1f;
        }

        [Tooltip("Initial dimensions only. Repeat setup never resizes or repaints saved prefabs.")]
        public Item[] items = Array.Empty<Item>();

        public void Validate(string[] expected)
        {
            if (items == null || items.Length != expected.Length || items.Any(item => item == null ||
                !expected.Contains(item.id) || !float.IsFinite(item.targetHeight) || item.targetHeight < .1f || item.targetHeight > 4f) ||
                items.Select(item => item.id).Distinct(StringComparer.Ordinal).Count() != expected.Length)
                throw new InvalidOperationException("Preserve and inspect Household settings: each source needs one finite height between 0.1 and 4 metres.");
        }
    }
}
