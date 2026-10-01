using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wreckabulary.Art
{
    /// <summary>
    /// Every imported model by key, the path under <c>Art/Imported</c> without the extension:
    /// "Items/BALL", "Letters/Tile_A", "Environment/Door_Frame", "Avatar/Avatar". The
    /// <c>model</c> field in items.json uses the same keys. Built by Wreckabulary → Art →
    /// Set Up Imported Art.
    /// </summary>
    [CreateAssetMenu(menuName = "Wreckabulary/Model Library", fileName = "ModelLibrary")]
    public sealed class ModelLibrary : ScriptableObject
    {
        public const string ResourcePath = "ModelLibrary";

        [Serializable]
        public struct Entry
        {
            public string key;
            public GameObject model;
            public AnimationClip[] clips;
        }

        [SerializeField] List<Entry> models = new List<Entry>();

        Dictionary<string, GameObject> byKey;

        public IReadOnlyList<Entry> Entries => models;

        static ModelLibrary loaded;

        /// <summary>The library in <c>Resources/ModelLibrary.asset</c>, or null if it hasn't been built.</summary>
        public static ModelLibrary Load()
        {
            if (loaded == null) loaded = Resources.Load<ModelLibrary>(ResourcePath);
            return loaded;
        }

        public void Set(IEnumerable<Entry> all)
        {
            models = new List<Entry>(all);
            byKey = null;
        }

        public GameObject Find(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            if (byKey == null)
            {
                byKey = new Dictionary<string, GameObject>(StringComparer.Ordinal);
                foreach (var e in models)
                    if (e.model != null) byKey[e.key] = e.model;
            }
            return byKey.TryGetValue(key, out var found) ? found : null;
        }

        /// <summary>Returns a canonical skeletal clip bundled with the imported model.</summary>
        public AnimationClip FindClip(string key, string name)
        {
            foreach (var entry in models)
            {
                if (entry.key != key || entry.clips == null) continue;
                foreach (var clip in entry.clips)
                {
                    if (!clip) continue;
                    string canonical = clip.name;
                    int bar = canonical.LastIndexOf('|');
                    if (bar >= 0) canonical = canonical.Substring(bar + 1);
                    if (canonical == name) return clip;
                }
            }
            return null;
        }

        /// <summary>
        /// Places a copy of a model under <paramref name="parent"/>. Move and turn the parent, not
        /// the copy: a single-mesh model keeps its import rotation on its own root, and resetting
        /// that rotation lays it on its side.
        /// </summary>
        public GameObject Spawn(string key, Transform parent)
        {
            var model = Find(key);
            if (model == null) throw new KeyNotFoundException($"No imported model '{key}'. Run Wreckabulary > Art > Set Up Imported Art.");
            var copy = Instantiate(model, parent, false);
            copy.name = model.name;
            return copy;
        }
    }
}
