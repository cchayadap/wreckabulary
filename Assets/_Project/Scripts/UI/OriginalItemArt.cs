using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wreckabulary.UI
{
    public static class OriginalItemArt
    {
        static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>(StringComparer.Ordinal);

        public static Texture2D Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < id.Length; i++) if (id[i] < 'A' || id[i] > 'Z') return null;
            if (!cache.TryGetValue(id, out var image)) cache[id] = image = Resources.Load<Texture2D>("UI/Items/" + id);
            return image;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache() => cache.Clear();
    }
}
