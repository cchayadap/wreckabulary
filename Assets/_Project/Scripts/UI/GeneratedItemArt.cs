using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wreckabulary.UI
{
    [CreateAssetMenu(menuName = "Wreckabulary/Generated Item Art")]
    public sealed class GeneratedItemArt : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public string itemId;
            public Sprite sprite;
        }

        [SerializeField] Entry[] entries = Array.Empty<Entry>();
        Dictionary<string, Sprite> index;
        static GeneratedItemArt library;
        static bool loaded;
        public IReadOnlyList<Entry> Entries => entries;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache() { library = null; loaded = false; }

        public static bool TryGet(string itemId, out Sprite sprite)
        {
            sprite = null;
            if (string.IsNullOrEmpty(itemId)) return false;
            if (!loaded) { library = Resources.Load<GeneratedItemArt>("UI/Generated/PowerItemArt"); loaded = true; }
            if (!library) return false;
            if (library.index == null)
            {
                library.index = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
                foreach (var entry in library.entries)
                    if (!string.IsNullOrEmpty(entry.itemId) && entry.sprite) library.index[entry.itemId] = entry.sprite;
            }
            return library.index.TryGetValue(itemId, out sprite) && sprite;
        }

        void OnEnable() => index = null;
        void OnValidate() => index = null;

#if UNITY_EDITOR
        public void SetEntries(Entry[] value) { entries = value ?? Array.Empty<Entry>(); index = null; }
#endif
    }
}
