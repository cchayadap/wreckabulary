using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Online
{
    public enum WordboundBaseArtwork
    {
        OriginalWreckabulary = 0,
        WordboundEquipment = 1
    }

    [Serializable]
    public sealed class WordboundVisualOverride
    {
        [Tooltip("Canonical item or prop visual key; player avatars and letter tiles keep their existing rig.")]
        public string VisualKey = "";
        public GameObject Model;
        [Tooltip("Optional replacement material per material slot, applied to every renderer. Null entries preserve that slot.")]
        public Material[] Materials = Array.Empty<Material>();
    }

    [CreateAssetMenu(menuName = "Wreckabulary/Wordbound/Visual profile")]
    public sealed class WordboundVisualProfile : ScriptableObject
    {
        [Tooltip("Choose the original Wreckabulary models or the optional Wordbound equipment collection. Per-item Overrides take priority.")]
        public WordboundBaseArtwork BaseArtwork = WordboundBaseArtwork.OriginalWreckabulary;
        public WordboundDefensePose DefensePose = new WordboundDefensePose();
        public WordboundHoldingPose HoldingPose = new WordboundHoldingPose();
        public List<WordboundVisualOverride> Overrides = new List<WordboundVisualOverride>();
        public void Validate()
        {
            if (!Enum.IsDefined(typeof(WordboundBaseArtwork), BaseArtwork)) throw new ArgumentException("Choose a supported base artwork source.");
            if (DefensePose == null) throw new ArgumentException("Assign a held-defense visual pose.");
            DefensePose.Validate();
            if (HoldingPose == null) throw new ArgumentException("Assign held-item pose settings.");
            HoldingPose.Validate();
            if (Overrides == null || Overrides.Count > 256) throw new ArgumentException("At most 256 visual overrides are supported.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in Overrides)
            {
                if (entry == null || !WordboundIdentity.IsId(entry.VisualKey) || !ids.Add(entry.VisualKey) ||
                    entry.VisualKey == "player" || entry.VisualKey.StartsWith("letter:", StringComparison.Ordinal))
                    throw new ArgumentException("Visual overrides require distinct non-avatar, non-letter keys.");
                ValidateModel(entry.Model);
                if (entry.Materials == null || entry.Materials.Length > 32)
                    throw new ArgumentException("At most 32 material slots are supported.");
            }
        }
        public static void ValidateModel(GameObject model)
        {
            if (!model) throw new ArgumentException("Assign a render-only model prefab.");
            if (model.GetComponentsInChildren<Collider>(true).Length != 0 || model.GetComponentsInChildren<Rigidbody>(true).Length != 0 ||
                model.GetComponentsInChildren<Collider2D>(true).Length != 0 || model.GetComponentsInChildren<Rigidbody2D>(true).Length != 0 ||
                model.GetComponentsInChildren<MonoBehaviour>(true).Length != 0 || model.GetComponentsInChildren<Camera>(true).Length != 0 ||
                model.GetComponentsInChildren<Light>(true).Length != 0 || model.GetComponentsInChildren<AudioSource>(true).Length != 0)
                throw new ArgumentException("Visual prefabs cannot contain physics, scripts, cameras, lights or audio sources.");
            var renderers = model.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0 || renderers.Length > 128 || renderers.Any(r => r.sharedMaterials.Any(m => !m)))
                throw new ArgumentException("Visual prefabs need 1–128 renderers with complete materials.");
            foreach (var transform in model.GetComponentsInChildren<Transform>(true))
                if (!Finite(transform.localPosition) || !Finite(transform.localScale) ||
                    transform.localScale.x <= 0 || transform.localScale.y <= 0 || transform.localScale.z <= 0)
                    throw new ArgumentException("Visual transforms must be finite with positive scale.");
        }
        public bool TryInstantiate(string visualKey, Transform parent, out GameObject model)
        {
            model = null;
            var entry = Overrides?.SingleOrDefault(e => e != null && e.VisualKey == visualKey);
            if (entry == null) return false;
            Validate();
            if (!parent) throw new ArgumentNullException(nameof(parent));
            model = Instantiate(entry.Model, parent, false);
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length && i < entry.Materials.Length; i++)
                    if (entry.Materials[i]) materials[i] = entry.Materials[i];
                renderer.sharedMaterials = materials;
            }
            return true;
        }
        static bool Finite(Vector3 v) => Good(v.x) && Good(v.y) && Good(v.z);
        static bool Good(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
    }
}
