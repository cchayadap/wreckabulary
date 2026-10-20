using System;
using System.Collections.Generic;
using UnityEngine;
using Wreckabulary.Art;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;
using Object = UnityEngine.Object;

namespace Wreckabulary.Online
{
    public sealed class WordboundVisualResolver
    {
        public const string AvatarKey = "Avatar/Avatar";
        readonly ItemCatalogue items;
        readonly Dictionary<string, string> mappings;
        public bool UseWordboundCollection { get; }

        public WordboundVisualResolver(ItemCatalogue items, bool useWordboundCollection = true,
            IReadOnlyDictionary<string, string> courseModels = null)
        {
            this.items = items ?? throw new ArgumentNullException(nameof(items));
            UseWordboundCollection = useWordboundCollection;
            mappings = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["keepsake"] = "Items/CHEST",
                ["platform"] = "Environment/Platform_2m"
            };
            if (courseModels != null)
                foreach (var pair in courseModels)
                {
                    if (!WordboundIdentity.IsId(pair.Key) || string.IsNullOrEmpty(pair.Value) ||
                        pair.Key == "player" || pair.Key.StartsWith("letter:", StringComparison.Ordinal))
                        throw new ArgumentException("Course artwork requires a non-reserved ID and an explicit model key.");
                    mappings[pair.Key] = pair.Value;
                }
        }

        public string ModelKey(string visualKey)
        {
            if (visualKey == "player") return AvatarKey;
            if (visualKey != null && visualKey.Length == 8 && visualKey.StartsWith("letter:", StringComparison.Ordinal) &&
                visualKey[7] >= 'A' && visualKey[7] <= 'Z') return "Letters/Tile_" + visualKey[7];
            if (visualKey != null && mappings.TryGetValue(visualKey, out var mapped)) return mapped;
            if (items.TryGet(visualKey, out var item) && !string.IsNullOrEmpty(item.Model)) return item.Model;
            throw new ArgumentException("No authored artwork mapping for canonical visual: " + visualKey);
        }

        public string SkinFor(string visualKey, Outfit outfit)
        {
            return items.TryGet(visualKey, out var item) ? Appearance.Pick(item, outfit?.SkinFor(visualKey)) : Skin.Standard;
        }

        public WordboundVisualInstance Create(string visualKey, Vector3 size, Transform parent,
            string skin = Skin.Standard)
        {
            if (!parent) throw new ArgumentNullException(nameof(parent));
            _ = new WordboundBodyPresentation(new WorldVector(size.x, size.y, size.z), visualKey);
            string key = ModelKey(visualKey);
            items.TryGet(visualKey, out var definition);
            skin = definition == null ? Skin.Standard : Appearance.Pick(definition, skin);
            var collection = UseWordboundCollection && skin == Skin.Standard ? WordboundEquipmentCollection.Load() : null;
            var entry = collection ? collection.FindByModelKey(key) : null;
            var root = new GameObject("Wordbound artwork " + visualKey);
            root.SetActive(false);
            root.transform.SetParent(parent, false);
            try
            {
                var fit = new GameObject("Shape fit").transform;
                fit.SetParent(root.transform, false);
                var orientation = new GameObject("Authored orientation").transform;
                orientation.SetParent(fit, false);
                if (visualKey.StartsWith("letter:", StringComparison.Ordinal))
                    orientation.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                var model = ModelVisual.Spawn(key, orientation, skin, UseWordboundCollection);
                if (!model) throw new InvalidOperationException("Required authored model is not imported: " + key);
                if (model.GetComponentsInChildren<Collider>(true).Length != 0 ||
                    model.GetComponentsInChildren<Rigidbody>(true).Length != 0 ||
                    model.GetComponentsInChildren<Collider2D>(true).Length != 0 ||
                    model.GetComponentsInChildren<Rigidbody2D>(true).Length != 0)
                    throw new InvalidOperationException("Presentation models must contain no physics components: " + key);
                var renderers = model.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0) throw new InvalidOperationException("Required artwork has no renderers: " + key);
                foreach (var renderer in renderers)
                    foreach (var material in renderer.sharedMaterials)
                        if (!material) throw new InvalidOperationException("Required artwork has a missing material: " + key);
                if (entry != null && Mathf.Abs(entry.boundsCenter.y - entry.size.y * .5f) > .001f)
                    throw new InvalidOperationException("Reimport Wordbound equipment to verify authored geometry bounds: " + key);
                Bounds bounds = entry != null
                    ? new Bounds(entry.boundsCenter, entry.size)
                    : ModelVisual.BoundsIn(fit, model);
                bool player = visualKey == "player";
                Fit(fit, bounds, size, player);
                var authoredGrip = ModelVisual.FindNamed(model, "Grip_R");
                Vector3 grip = authoredGrip ? root.transform.InverseTransformPoint(authoredGrip.position) :
                    fit.localPosition + Vector3.Scale(fit.localScale,
                        entry != null ? entry.grip :
                        definition != null && definition.Grip?.Length == 3 ?
                            new Vector3(definition.Grip[0], definition.Grip[1], definition.Grip[2]) : bounds.center);
                var instance = new WordboundVisualInstance(root, model, grip,
                    entry?.heldScale ?? definition?.HeldScale ?? 1f, definition?.IsTwoHanded ?? true);
                root.SetActive(true);
                return instance;
            }
            catch { Release(root); throw; }
        }

        public static void Fit(Transform fit, Bounds bounds, Vector3 size, bool preserveProportions)
        {
            if (!fit || !Positive(bounds.size.x) || !Positive(bounds.size.y) || !Positive(bounds.size.z) ||
                !Positive(size.x) || !Positive(size.y) || !Positive(size.z))
                throw new ArgumentException("Artwork and canonical dimensions must be finite and non-degenerate.");
            var scale = preserveProportions ? Vector3.one * (size.y / bounds.size.y) :
                new Vector3(size.x / bounds.size.x, size.y / bounds.size.y, size.z / bounds.size.z);
            fit.localScale = scale;
            fit.localPosition = -Vector3.Scale(new Vector3(bounds.center.x, bounds.min.y, bounds.center.z), scale);
        }

        static bool Positive(float value) => value > .000001f && !float.IsInfinity(value) && !float.IsNaN(value);
        internal static void Release(GameObject root)
        {
            if (!root) return;
            root.SetActive(false);
            if (Application.isPlaying) Object.Destroy(root); else Object.DestroyImmediate(root);
        }
    }

    public sealed class WordboundVisualInstance : IDisposable
    {
        readonly Renderer[] renderers;
        public GameObject Root { get; }
        public GameObject Model { get; }
        public Vector3 Grip { get; }
        public float HeldScale { get; }
        public bool TwoHanded { get; }
        public Outfit Outfit { get; private set; }
        public Transform RightHand { get; }
        public Transform LeftHand { get; }
        public WordboundVisualInstance(GameObject root, GameObject model, Vector3 grip, float heldScale, bool twoHanded)
        {
            Root = root; Model = model; Grip = grip;
            HeldScale = heldScale; TwoHanded = twoHanded;
            renderers = model.GetComponentsInChildren<Renderer>(true);
            RightHand = ModelVisual.FindNamed(model, "grip_R");
            LeftHand = ModelVisual.FindNamed(model, "grip_L");
        }
        public void Dress(string serialized)
        {
            var look = string.IsNullOrEmpty(serialized) ? PlayerAppearance.DefaultPresentationOutfit() : Rules.Outfit.Deserialize(serialized);
            Outfit = PlayerAppearance.Dress(Model.GetComponentsInChildren<SkinnedMeshRenderer>(true), look);
            foreach (var renderer in renderers) renderer.forceRenderingOff = !renderer.enabled;
        }
        public void Show(bool visible)
        {
            if (!Root) return;
            if (Root.activeSelf != visible) Root.SetActive(visible);
            if (visible)
                foreach (var renderer in renderers) if (renderer) renderer.enabled = true;
        }
        public void PlaceAtHand(Transform visualParent, Transform hand, bool miniature)
        {
            if (!Root || !hand || !visualParent) throw new ArgumentException("A held visual requires an authored hand.");
            var target = Root.transform;
            if (target.parent != visualParent) target.SetParent(visualParent, false);
            target.localScale = Vector3.one * (miniature ? HeldScale : 1f);
            target.rotation = hand.rotation;
            target.position = hand.position - target.TransformVector(Grip);
        }
        public void Dispose() => WordboundVisualResolver.Release(Root);
    }
}
