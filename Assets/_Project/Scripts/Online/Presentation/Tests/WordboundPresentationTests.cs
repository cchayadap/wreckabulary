using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using Wreckabulary.Art;
using Wreckabulary.Online;
using Wreckabulary.Rules;
using Object = UnityEngine.Object;

namespace Wreckabulary.Tests
{
    public sealed class WordboundPresentationTests
    {
        GameObject anchor;
        WordboundVisualResolver resolver;

        [SetUp]
        public void SetUp()
        {
            anchor = new GameObject("Wordbound presentation test");
            resolver = new WordboundVisualResolver(GameConfig.Current.Items);
        }
        [TearDown]
        public void TearDown() { if (anchor) Object.DestroyImmediate(anchor); }

        [TestCase("BAT"), TestCase("BROOM"), TestCase("HAMMER"), TestCase("SPEAR")]
        [TestCase("SHIELD"), TestCase("PLATE"), TestCase("TABLE"), TestCase("SOFA")]
        [TestCase("BED"), TestCase("STOOL"), TestCase("FAN"), TestCase("CLOCK")]
        public void DefaultPresentationUsesOriginalMeshesWithCanonicalBoundsAndGrip(string id)
        {
            var profile = AssetDatabase.LoadAssetAtPath<WordboundVisualProfile>("Assets/_Project/Data/Wordbound/Authoring/Presentation.asset");
            Assert.IsNotNull(profile, "The actual launch presentation profile must exist.");
            Assert.AreEqual(WordboundBaseArtwork.OriginalWreckabulary, profile.BaseArtwork);
            var item = GameConfig.Current.Items.Get(id);
            var source = ModelLibrary.Load().Find(item.Model);
            Assert.IsNotNull(source, "The original imported model must exist: " + item.Model);
            var target = Vector3.Scale(new Vector3(item.Size[0], item.Size[1], item.Size[2]), new Vector3(.8f, 1.1f, .9f));
            var originals = new WordboundVisualResolver(GameConfig.Current.Items, profile: profile);
            using var visual = originals.Create(id, target, anchor.transform);
            SameMeshes(source, visual.Model);
            var bounds = VertexBounds(visual.Model, anchor.transform);
            Near(target, bounds.size);
            Near(new Vector3(0, target.y / 2, 0), bounds.center);
            Assert.IsEmpty(visual.Root.GetComponentsInChildren<Collider>(true));
            Assert.IsEmpty(visual.Root.GetComponentsInChildren<Rigidbody>(true));
            Assert.IsEmpty(visual.Root.GetComponentsInChildren<Collider2D>(true));
            Assert.IsEmpty(visual.Root.GetComponentsInChildren<Rigidbody2D>(true));
            Assert.AreEqual(item.HeldScale, visual.HeldScale); Assert.AreEqual(item.IsTwoHanded, visual.TwoHanded);
            var grip = ModelVisual.FindNamed(visual.Model, "Grip_R");
            var itemGrip = new Vector3(item.Grip[0], item.Grip[1], item.Grip[2]);
            Vector3 contact = grip ? grip.position : visual.Model.transform.parent.TransformPoint(itemGrip);
            Near(visual.Root.transform.InverseTransformPoint(contact), visual.Grip);
            var hand = new GameObject("Original artwork hand").transform;
            hand.SetParent(anchor.transform, false);
            hand.SetPositionAndRotation(new Vector3(2, 3, 4), Quaternion.Euler(17, 43, 71));
            bool miniature = item.Category == ItemCategory.Combat;
            visual.PlaceAtHand(anchor.transform, hand, miniature);
            Near(hand.position, grip ? grip.position : visual.Model.transform.parent.TransformPoint(itemGrip));
            Near(Vector3.one * (miniature ? item.HeldScale : 1), visual.Root.transform.localScale);
        }

        [TestCase("BAT"), TestCase("BROOM"), TestCase("HAMMER"), TestCase("SPEAR")]
        [TestCase("SHIELD"), TestCase("PLATE"), TestCase("TABLE"), TestCase("SOFA")]
        [TestCase("BED"), TestCase("STOOL"), TestCase("FAN"), TestCase("CLOCK")]
        public void CollectionArtworkFitsCanonicalBoundsWithoutPhysicsAndKeepsItsMaterials(string id)
        {
            var collection = WordboundEquipmentCollection.Load();
            Assert.IsNotNull(collection, "Import Wordbound equipment before these native tests.");
            var entry = collection.Find(id);
            var target = Vector3.Scale(entry.size, new Vector3(.8f, 1.1f, .9f));
            var profile = ScriptableObject.CreateInstance<WordboundVisualProfile>();
            profile.BaseArtwork = WordboundBaseArtwork.WordboundEquipment;
            try
            {
                var selected = new WordboundVisualResolver(GameConfig.Current.Items, profile: profile);
                using var visual = selected.Create(id, target, anchor.transform);
                SameMeshes(entry.model, visual.Model);
                var bounds = VertexBounds(visual.Model, anchor.transform);
                Near(target, bounds.size);
                Assert.That(Mathf.Abs(bounds.min.y), Is.LessThan(.002f));
                Assert.That(Mathf.Abs(bounds.center.x) + Mathf.Abs(bounds.center.z), Is.LessThan(.002f));
                Assert.IsEmpty(visual.Root.GetComponentsInChildren<Collider>(true));
                Assert.IsEmpty(visual.Root.GetComponentsInChildren<Rigidbody>(true));
                var originals = new HashSet<Material>(entry.model.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials));
                Assert.IsTrue(visual.Model.GetComponentsInChildren<Renderer>(true).All(r => r.sharedMaterials.All(originals.Contains)));
            }
            finally { Object.DestroyImmediate(profile); }
        }

        [TestCase(false, false), TestCase(false, true)]
        [TestCase(true, false), TestCase(true, true)]
        public void CollectionOptInRespectsCallerFlagAndNullProfileKeepsItsLegacySelection(bool withProfile, bool allowCollection)
        {
            var profile = withProfile ? ScriptableObject.CreateInstance<WordboundVisualProfile>() : null;
            if (profile) profile.BaseArtwork = WordboundBaseArtwork.WordboundEquipment;
            try
            {
                var selected = new WordboundVisualResolver(GameConfig.Current.Items, allowCollection, profile: profile);
                var expected = allowCollection ? WordboundEquipmentCollection.Load().Find("BAT").model : ModelLibrary.Load().Find("Items/BAT");
                using var visual = selected.Create("BAT", new Vector3(.2f, 1f, .2f), anchor.transform);
                SameMeshes(expected, visual.Model);
            }
            finally { if (profile) Object.DestroyImmediate(profile); }
        }

        [Test]
        public void NewProfileDefaultsToOriginalArtworkAndRejectsUnknownChoices()
        {
            var profile = ScriptableObject.CreateInstance<WordboundVisualProfile>();
            try
            {
                Assert.AreEqual(WordboundBaseArtwork.OriginalWreckabulary, profile.BaseArtwork);
                var serialized = new SerializedObject(profile);
                var choice = serialized.FindProperty("BaseArtwork");
                Assert.AreEqual(SerializedPropertyType.Enum, choice.propertyType);
                Assert.AreEqual(0, choice.enumValueIndex);
                CollectionAssert.AreEqual(new[] { "Original Wreckabulary", "Wordbound Equipment" }, choice.enumDisplayNames);
                profile.BaseArtwork = (WordboundBaseArtwork)99;
                Assert.Throws<ArgumentException>(() => new WordboundVisualResolver(GameConfig.Current.Items, profile: profile));
            }
            finally { Object.DestroyImmediate(profile); }
        }

        [TestCase("BAT", true), TestCase("TABLE", false)]
        [TestCase("SHIELD", true), TestCase("SOFA", false), TestCase("CLOCK", true)]
        public void HeldArtworkAlignsAuthoredGripAndOnlyWeaponsUseMiniatureScale(string id, bool miniature)
        {
            var item = GameConfig.Current.Items.Get(id);
            var size = new Vector3(item.Size[0], item.Size[1], item.Size[2]);
            var hand = new GameObject("Authored hand test").transform;
            hand.SetParent(anchor.transform, false);
            hand.SetPositionAndRotation(new Vector3(2, 3, 4), Quaternion.Euler(17, 43, 71));
            using var visual = resolver.Create(id, size, anchor.transform);
            visual.PlaceAtHand(anchor.transform, hand, miniature);
            var grip = ModelVisual.FindNamed(visual.Model, "Grip_R");
            Assert.IsNotNull(grip);
            Near(hand.position, grip.position);
            Near(Vector3.one * (miniature ? item.HeldScale : 1), visual.Root.transform.localScale);
            visual.Show(false);
            Assert.IsFalse(visual.Root.activeSelf);
            visual.Show(true);
            Assert.IsTrue(visual.Root.activeSelf);
        }

        [TestCase("letter:A"), TestCase("letter:Z"), TestCase("platform"), TestCase("keepsake")]
        public void NonItemBodiesResolveExistingArtworkAndGroundTheirBounds(string key)
        {
            var size = key.StartsWith("letter:", StringComparison.Ordinal) ? new Vector3(.3f, .12f, .3f) :
                key == "platform" ? new Vector3(2, .24f, 2) : Vector3.one * .45f;
            using var visual = resolver.Create(key, size, anchor.transform);
            var bounds = ModelVisual.BoundsIn(anchor.transform, visual.Model);
            Near(size, bounds.size);
            Assert.That(Mathf.Abs(bounds.min.y), Is.LessThan(.002f));
        }

        [Test]
        public void WardrobeVisibilitySurvivesBodyRendererRefreshWithoutChangingSavedOutfits()
        {
            string saved = PlayerPrefs.GetString("wv.outfit.0", "");
            using var visual = resolver.Create("player", new Vector3(.6f, 1.8f, .6f), anchor.transform);
            visual.Dress(PlayerAppearance.DefaultPresentationOutfit().Serialize());
            var renderers = visual.Model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var hidden = renderers.Where(r => r.forceRenderingOff).ToArray();
            Assert.IsNotEmpty(hidden, "The authored avatar must contain unselected wardrobe pieces.");
            foreach (var renderer in renderers) renderer.enabled = true;
            visual.Show(false); visual.Show(true);
            Assert.IsTrue(hidden.All(r => r.forceRenderingOff));
            Assert.IsTrue(renderers.Any(r => !r.forceRenderingOff));
            Assert.AreEqual(saved, PlayerPrefs.GetString("wv.outfit.0", ""));
            using var motion = new WordboundAvatarMotion(visual);
            motion.Step(1f / 60, true, true, false);
            Assert.IsNotNull(visual.RightHand);
            Assert.IsNotNull(visual.LeftHand);
        }

        [Test]
        public void MissingMappingsNeverBecomePrimitiveSubstitutesAndCourseAliasesAreExplicit()
        {
            Assert.Throws<ArgumentException>(() => resolver.Create("unmapped", Vector3.one, anchor.transform));
            Assert.AreEqual(0, anchor.transform.childCount);
            Assert.Throws<ArgumentException>(() => resolver.ModelKey("letter:a"));
            var aliases = new WordboundVisualResolver(GameConfig.Current.Items, courseModels:
                new Dictionary<string, string> { ["authored-table"] = "Items/TABLE" });
            Assert.AreEqual("Items/TABLE", aliases.ModelKey("authored-table"));
            Assert.Throws<ArgumentException>(() => new WordboundVisualResolver(GameConfig.Current.Items,
                courseModels: new Dictionary<string, string> { ["player"] = "Items/TABLE" }));
        }

        [TestCase("BAT", false, WordboundBaseArtwork.OriginalWreckabulary), TestCase("BAT", true, WordboundBaseArtwork.OriginalWreckabulary)]
        [TestCase("custom-inspector-prop", false, WordboundBaseArtwork.OriginalWreckabulary), TestCase("custom-inspector-prop", true, WordboundBaseArtwork.OriginalWreckabulary)]
        [TestCase("BAT", false, WordboundBaseArtwork.WordboundEquipment), TestCase("BAT", true, WordboundBaseArtwork.WordboundEquipment)]
        [TestCase("custom-inspector-prop", false, WordboundBaseArtwork.WordboundEquipment), TestCase("custom-inspector-prop", true, WordboundBaseArtwork.WordboundEquipment)]
        public void InspectorModelOverrideUsesItsOwnBoundsAndGripInsteadOfOriginalMetadata(string visualKey, bool authoredGrip, WordboundBaseArtwork baseArtwork)
        {
            var profile = ScriptableObject.CreateInstance<WordboundVisualProfile>();
            profile.BaseArtwork = baseArtwork;
            var model = GameObject.CreatePrimitive(PrimitiveType.Cube);
            model.transform.SetParent(anchor.transform, false);
            Object.DestroyImmediate(model.GetComponent<Collider>());
            model.transform.localPosition = new Vector3(4, 2, -3);
            model.transform.localScale = new Vector3(2, 3, 4);
            if (authoredGrip)
            {
                var grip = new GameObject("Grip_R").transform;
                grip.SetParent(model.transform, false);
                grip.localPosition = new Vector3(.2f, -.3f, .1f);
            }
            profile.Overrides.Add(new WordboundVisualOverride { VisualKey = visualKey, Model = model });
            try
            {
                var custom = new WordboundVisualResolver(GameConfig.Current.Items, profile: profile);
                var size = new Vector3(.4f, 1.4f, .3f);
                using var visual = custom.Create(visualKey, size, anchor.transform);
                var bounds = VertexBounds(visual.Model, anchor.transform);
                Near(size, bounds.size);
                Near(new Vector3(0, size.y / 2, 0), bounds.center);
                var hand = new GameObject("Override hand").transform;
                hand.SetParent(anchor.transform, false);
                hand.SetPositionAndRotation(new Vector3(-2, 3, 4), Quaternion.Euler(23, 48, -11));
                visual.PlaceAtHand(anchor.transform, hand, true);
                Vector3 actual = authoredGrip ? ModelVisual.FindNamed(visual.Model, "Grip_R").position :
                    visual.Model.GetComponent<MeshRenderer>().bounds.center;
                Near(hand.position, actual);
                Assert.AreSame(model.GetComponent<MeshRenderer>().sharedMaterial,
                    visual.Model.GetComponent<MeshRenderer>().sharedMaterial);
            }
            finally { Object.DestroyImmediate(profile); }
        }

        static void Near(Vector3 expected, Vector3 actual) =>
            Assert.That(Vector3.Distance(expected, actual), Is.LessThan(.002f), $"Expected {expected}, got {actual}");

        static void SameMeshes(GameObject expected, GameObject actual)
        {
            Mesh[] Meshes(GameObject model) => model.GetComponentsInChildren<MeshFilter>(true).Select(f => f.sharedMesh)
                .Concat(model.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(r => r.sharedMesh)).ToArray();
            var source = Meshes(expected);
            Assert.IsNotEmpty(source);
            CollectionAssert.AreEquivalent(source, Meshes(actual), "Spawn the selected imported mesh assets, not merely a matching object name.");
        }

        static Bounds VertexBounds(GameObject model, Transform space)
        {
            var bounds = new Bounds();
            bool initialized = false;
            foreach (var filter in model.GetComponentsInChildren<MeshFilter>(true))
            {
                using var data = MeshUtility.AcquireReadOnlyMeshData(filter.sharedMesh);
                using var vertices = new NativeArray<Vector3>(data[0].vertexCount, Allocator.Temp);
                data[0].GetVertices(vertices);
                var matrix = space.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                foreach (var vertex in vertices)
                {
                    var point = matrix.MultiplyPoint3x4(vertex);
                    if (initialized) bounds.Encapsulate(point);
                    else { initialized = true; bounds = new Bounds(point, Vector3.zero); }
                }
            }
            Assert.IsTrue(initialized);
            return bounds;
        }
    }
}
