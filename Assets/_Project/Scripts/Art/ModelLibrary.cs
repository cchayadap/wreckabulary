using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wreckabulary.Art
{
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
