using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wreckabulary.Art
{
    [CreateAssetMenu(menuName = "Wreckabulary/Wordbound Equipment Collection")]
    public sealed class WordboundEquipmentCollection : ScriptableObject
    {
        public const string ResourcePath = "Collections/WordboundPvP";

        [Serializable]
        public sealed class Entry
        {
            public string itemId;
            public string modelKey;
            public GameObject model;
            public Sprite icon;
            public Vector3 size;
            public Vector3 boundsCenter;
            public Vector3 grip;
            public float heldScale;
        }

        [SerializeField] List<Entry> entries = new List<Entry>();
        static WordboundEquipmentCollection loaded;
        public IReadOnlyList<Entry> Entries => entries;

        public static WordboundEquipmentCollection Load() =>
            loaded ? loaded : loaded = Resources.Load<WordboundEquipmentCollection>(ResourcePath);

        public Entry Find(string itemId) =>
            entries.Find(entry => entry != null && string.Equals(entry.itemId, itemId, StringComparison.Ordinal));

        public Entry FindByModelKey(string modelKey) =>
            entries.Find(entry => entry != null && string.Equals(entry.modelKey, modelKey, StringComparison.Ordinal));

        public void SetEntries(IEnumerable<Entry> values)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            var next = new List<Entry>(values);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in next)
            {
                if (entry == null || string.IsNullOrEmpty(entry.itemId) || string.IsNullOrEmpty(entry.modelKey) ||
                    !entry.model || !entry.icon || !ids.Add(entry.itemId) || !keys.Add(entry.modelKey))
                    throw new ArgumentException("Equipment entries need unique item/model keys, a model and an icon.");
                if (!Positive(entry.size.x) || !Positive(entry.size.y) || !Positive(entry.size.z) ||
                    !Finite(entry.boundsCenter.x) || !Finite(entry.boundsCenter.y) || !Finite(entry.boundsCenter.z) ||
                    Mathf.Abs(entry.boundsCenter.y - entry.size.y * .5f) > .001f ||
                    !Finite(entry.grip.x) || !Finite(entry.grip.y) || !Finite(entry.grip.z) ||
                    !Positive(entry.heldScale) || entry.heldScale > 1f)
                    throw new ArgumentException("Equipment dimensions, grip and held scale must be finite and valid.");
            }
            entries = next;
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static bool Positive(float value) => Finite(value) && value > 0f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetLoaded() => loaded = null;
    }
}
