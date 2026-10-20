using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Wreckabulary.Online;
using Wreckabulary.Rules.Wordbound;
using Object = UnityEngine.Object;

namespace Wreckabulary.Tests
{
    public sealed class WordboundAuthoringTests
    {
        WordboundCourseAsset asset;
        [SetUp] public void SetUp()
        {
            asset = ScriptableObject.CreateInstance<WordboundCourseAsset>();
            asset.Initialize(CourseTemplates.FreestyleWorld(), CourseTemplates.ProofCatalogue());
            asset.Items = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/_Project/Data/Config/items.json");
            asset.RuleBook = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/_Project/Data/Config/rules.json");
        }
        [TearDown] public void TearDown() { if (asset) Object.DestroyImmediate(asset); }

        [Test] public void InspectorDocumentCloneAndInvalidImportPreserveSavedSource()
        {
            string saved = asset.DocumentJson;
            var copy = asset.ReadDocument(); copy.Modules[0].Position = new CoursePoint(4000000, 0, 0);
            Assert.AreEqual(saved, asset.DocumentJson);
            Assert.Throws<ArgumentException>(() => asset.SaveDocument(copy));
            Assert.Throws<ArgumentException>(() => asset.ImportDocument("{\"schema\":2}"));
            Assert.AreEqual(saved, asset.DocumentJson);
        }
        [Test] public void PreviewBindsExactSavedCourseSeedCatalogueAndGameplayAssets()
        {
            var resolved = asset.Resolve();
            var content = asset.CreateContent(resolved);
            Assert.AreEqual(asset.SourceHash, content.Course.SourceHash);
            Assert.AreEqual(resolved.Hash, content.Course.Hash);
            string input = asset.InputHash;
            asset.Seed = "0000000000000002";
            Assert.AreNotEqual(input, asset.InputHash);
            Assert.Throws<ArgumentException>(() => asset.CreateContent(resolved));
            asset.Seed = "0000000000000001";
            asset.Modules[0].SizeMm += new Vector3Int(1000, 0, 0);
            Assert.Throws<ArgumentException>(() => asset.CreateContent(resolved));
        }
        [Test] public void CourseAndRuleEditsInvalidateTheExactAuthoringInputIdentity()
        {
            string original = asset.InputHash;
            var draft = asset.ReadDocument(); draft.Title += " edited"; asset.SaveDocument(draft);
            Assert.AreNotEqual(original, asset.InputHash);
            string edited = asset.InputHash;
            var replacement = new TextAsset(asset.RuleBook.text + "\n");
            try { asset.RuleBook = replacement; Assert.AreNotEqual(edited, asset.InputHash); }
            finally { Object.DestroyImmediate(replacement); }
        }
        [Test] public void ModuleSceneMoveCarriesAttachedSpawnSupplyZonesAndEndpointsWithoutChangingOriginal()
        {
            var source = asset.ReadDocument();
            var module = source.Modules.Single(m => m.Id == "Start");
            var destination = new CoursePoint(module.Position.X + 1000, module.Position.Y, module.Position.Z - 2000);
            var result = WordboundCourseEditing.MoveModule(source, module.Id, destination);
            Assert.AreEqual(module.Position, source.Modules.Single(m => m.Id == "Start").Position);
            Assert.AreEqual(destination, result.Modules.Single(m => m.Id == "Start").Position);
            Assert.AreEqual(source.Spawns[0].Position.X + 1000, result.Spawns[0].Position.X);
            Assert.AreEqual(source.Spawns[0].Position.Z - 2000, result.Spawns[0].Position.Z);
            Assert.AreEqual(source.Zones.Single(z => z.Id == "FixedStool").FixedPosition.X + 1000,
                result.Zones.Single(z => z.Id == "FixedStool").FixedPosition.X);
            Assert.AreEqual(source.Connections[0].From.X + 1000, result.Connections[0].From.X);
            Assert.AreEqual(source.Connections[0].To, result.Connections[0].To);
            Assert.Throws<ArgumentException>(() => WordboundCourseEditing.MoveModule(source, module.Id, new CoursePoint(0, 1000, 0)));
        }
        [Test] public void UnitySerializationRoundTripRetainsCourseAndInspectorCatalogue()
        {
            var clone = ScriptableObject.CreateInstance<WordboundCourseAsset>();
            try
            {
                EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(asset), clone);
                Assert.AreEqual(asset.DocumentJson, clone.DocumentJson);
                Assert.AreEqual(asset.Catalogue().Hash, clone.Catalogue().Hash);
                Assert.AreEqual(asset.Resolve().Hash, clone.Resolve().Hash);
            }
            finally { Object.DestroyImmediate(clone); }
        }
        [Test] public void CameraDefaultsMatchExistingViewAndInvalidClearanceIsRejected()
        {
            var profile = ScriptableObject.CreateInstance<WordboundCameraProfile>();
            try
            {
                Assert.DoesNotThrow(profile.Validate);
                Assert.AreEqual(55, profile.FieldOfView); Assert.AreEqual(3.3f, profile.Distance);
                profile.ProbeRadius = .4f;
                Assert.Throws<ArgumentException>(profile.Validate);
                profile.ProbeRadius = .2f; profile.DefaultPitch = float.NaN;
                Assert.Throws<ArgumentException>(profile.Validate);
            }
            finally { Object.DestroyImmediate(profile); }
        }
        [Test] public void VisualOverridesRejectPhysicsBeforeInstantiationAndKeepOriginalMaterialsUntouched()
        {
            var profile = ScriptableObject.CreateInstance<WordboundVisualProfile>();
            var model = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var parent = new GameObject("Authoring test parent");
            try
            {
                profile.Overrides.Add(new WordboundVisualOverride { VisualKey = "STOOL", Model = model });
                Assert.Throws<ArgumentException>(profile.Validate);
                Object.DestroyImmediate(model.GetComponent<Collider>());
                var material = model.GetComponent<Renderer>().sharedMaterial;
                Assert.DoesNotThrow(profile.Validate);
                Assert.IsTrue(profile.TryInstantiate("STOOL", parent.transform, out var copy));
                Assert.AreEqual(material, copy.GetComponent<Renderer>().sharedMaterial);
                Assert.AreEqual(material, model.GetComponent<Renderer>().sharedMaterial);
                Assert.IsFalse(profile.TryInstantiate("FAN", parent.transform, out _));
                profile.Overrides[0].VisualKey = "player";
                Assert.Throws<ArgumentException>(profile.Validate);
            }
            finally { Object.DestroyImmediate(profile); Object.DestroyImmediate(parent); Object.DestroyImmediate(model); }
        }
    }
}
