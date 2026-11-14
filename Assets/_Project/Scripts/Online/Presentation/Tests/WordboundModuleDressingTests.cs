using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Wreckabulary.Online;
using Wreckabulary.Rules.Wordbound;
using Wreckabulary.WordboundRuntime;
using Wreckabulary.Workshop;
using Object = UnityEngine.Object;

namespace Wreckabulary.Tests
{
    public sealed class WordboundModuleDressingTests
    {
        GameObject source, owner;
        Material material;
        Mesh mesh;
        WordboundModuleDressingEntry row;
        CourseModuleDefinition room;

        [SetUp]
        public void SetUp()
        {
            source = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(source.GetComponent<Collider>());
            source.name = "Owned test wall fixture";
            source.transform.localScale = new Vector3(.6f, .3f, .15f);
            mesh = source.GetComponent<MeshFilter>().sharedMesh;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            source.GetComponent<MeshRenderer>().sharedMaterial = material;
            owner = new GameObject("Owned dressing test module");
            room = new CourseModuleDefinition("Room", 8000, 8000, 3600);
            row = new WordboundModuleDressingEntry { ModuleId = "Service", Prefab = source, LocalPosition = new Vector3(0, 2.7f, 3.8f) };
        }

        [TearDown]
        public void TearDown()
        {
            if (owner) Object.DestroyImmediate(owner);
            if (source) Object.DestroyImmediate(source);
            if (material) Object.DestroyImmediate(material);
        }

        [Test]
        public void LegacyProfileIsOptionalAndInvalidRowsOrComponentsAreRejected()
        {
            var profile = ScriptableObject.CreateInstance<WordboundVisualProfile>();
            try
            {
                EditorJsonUtility.FromJsonOverwrite("{\"BaseArtwork\":0,\"Overrides\":[]}", profile);
                Assert.IsEmpty(profile.ModuleDressing); profile.Validate();
                Assert.IsNull(WordboundModuleDressing.Apply(null, row.ModuleId, room, owner.transform));
                profile.ModuleDressing.Add(row); profile.Validate();
                profile.ModuleDressing.Add(row); Assert.Throws<ArgumentException>(profile.Validate);
                profile.ModuleDressing.RemoveAt(1);
                row.LocalScale = new Vector3(-1, 1, 1); Assert.Throws<ArgumentException>(profile.Validate);
                row.LocalScale = Vector3.one; row.LocalEulerAngles = new Vector3(float.NaN, 0, 0);
                Assert.Throws<ArgumentException>(profile.Validate); row.LocalEulerAngles = Vector3.zero;
                foreach (var type in new[] { typeof(BoxCollider), typeof(Rigidbody), typeof(Light), typeof(AudioSource), typeof(Animator), typeof(WordboundCourseNode) })
                {
                    var component = source.AddComponent(type);
                    Assert.Throws<ArgumentException>(profile.Validate, type.Name);
                    Object.DestroyImmediate(component);
                }
                profile.Validate();
            }
            finally { Object.DestroyImmediate(profile); }
        }

        [Test]
        public void ADeletedOrDifferentModuleDoesNotReceiveAnotherRoomsFixtures()
        {
            Assert.IsNull(WordboundModuleDressing.Apply(new[] { row }, "OtherRoom", room, owner.transform));
            Assert.AreEqual(0, owner.transform.childCount);
            Assert.AreEqual("Service", row.ModuleId); Assert.AreSame(source, row.Prefab);
        }

        [Test]
        public void ExactPoseMeshAndMaterialsAreBorrowedAndClonesFollowTheirModule()
        {
            var sourceScale = source.transform.localScale;
            var sourceJson = EditorJsonUtility.ToJson(material);
            owner.transform.SetPositionAndRotation(new Vector3(9, 4, -7), Quaternion.Euler(0, 90, 0));
            row.LocalEulerAngles = new Vector3(0, 180, 0);
            var clone = WordboundModuleDressing.Apply(new[] { row }, row.ModuleId, room, owner.transform, 31);
            Assert.AreEqual(row.LocalPosition, clone.transform.localPosition);
            Assert.Less(Quaternion.Angle(Quaternion.Euler(row.LocalEulerAngles), clone.transform.localRotation), .001f);
            Assert.AreSame(mesh, clone.GetComponentInChildren<MeshFilter>().sharedMesh);
            Assert.AreSame(material, clone.GetComponentInChildren<MeshRenderer>().sharedMaterial);
            Assert.IsTrue(clone.GetComponentsInChildren<Transform>().All(t => t.gameObject.layer == 31));
            Assert.IsEmpty(clone.GetComponentsInChildren<Collider>());
            Assert.IsEmpty(clone.GetComponentsInChildren<Light>());
            Object.DestroyImmediate(clone);
            Assert.IsTrue(mesh); Assert.IsTrue(material); Assert.IsTrue(source);
            Assert.AreEqual(sourceScale, source.transform.localScale);
            Assert.AreEqual(sourceJson, EditorJsonUtility.ToJson(material));
            Assert.AreEqual(0, owner.transform.childCount);
        }

        [TestCase(0, 1, 3.8f)]
        [TestCase(0, 2.7f, 0)]
        [TestCase(0, 2.7f, 4.2f)]
        [TestCase(0, 3.6f, 3.8f)]
        public void OutsideHeadClearanceWallBandOrRoomIsRejectedAtomically(float x, float y, float z)
        {
            row.LocalPosition = new Vector3(x, y, z);
            Assert.Throws<ArgumentException>(() => WordboundModuleDressing.Apply(new[] { row }, row.ModuleId, room, owner.transform));
            Assert.AreEqual(0, owner.transform.childCount);
            Assert.IsTrue(source); Assert.IsTrue(mesh); Assert.IsTrue(material);
        }

        [Test]
        public void RuntimeWorldPreservesCanonicalDataCollidersAndOwnedCleanup()
        {
            var preset = WorkshopTemplates.Create(WorkshopTemplate.FreestyleWorld);
            var exact = new WorkshopHistory(preset.Snapshot).Preview(preset.Catalogue, preset.Play);
            Assert.IsTrue(exact.Success);
            var module = exact.Manifest.Geometry.First();
            var definition = preset.Catalogue.Modules[module.DefinitionId];
            row.ModuleId = module.Id;
            row.LocalPosition = new Vector3(0, 2.7f, definition.DepthMm / 2000f - .2f);
            string sourceCourse = preset.Snapshot.RecoveryJson;
            WordboundCourseWorld first = null, second = null;
            GameObject clone = null;
            try
            {
                first = WordboundCourseWorld.Build(exact.Manifest, preset.Catalogue, options: new WordboundCourseWorldOptions { EnableRoomArchitecture = false });
                second = WordboundCourseWorld.Build(exact.Manifest, preset.Catalogue, options: new WordboundCourseWorldOptions { EnableRoomArchitecture = false, ModuleDressing = new[] { row } });
                Assert.AreEqual(first.ManifestHash, second.ManifestHash);
                CollectionAssert.AreEqual(first.Surfaces.Select(Shape), second.Surfaces.Select(Shape));
                Assert.AreEqual(first.GetComponentsInChildren<WordboundCourseNode>().Length, second.GetComponentsInChildren<WordboundCourseNode>().Length);
                Assert.AreEqual(first.GetComponentsInChildren<Collider>().Length, second.GetComponentsInChildren<Collider>().Length);
                Assert.IsTrue(second.TryGetAnchor(WordboundCourseNodeKind.Module, module.Id, out var anchor));
                clone = anchor.Find(WordboundModuleDressing.RootName).gameObject;
                Assert.AreSame(mesh, clone.GetComponentInChildren<MeshFilter>().sharedMesh);
                Assert.AreEqual(sourceCourse, preset.Snapshot.RecoveryJson);
            }
            finally { first?.Dispose(); second?.Dispose(); second?.Dispose(); }
            Assert.IsTrue(clone == null); Assert.IsTrue(source); Assert.IsTrue(material); Assert.IsTrue(mesh);
        }

        [Test]
        public void WorkshopCutawayOmitsUnsupportedWallFixturesWithoutChangingTheirAuthoredData()
        {
            var preset = WorkshopTemplates.Create(WorkshopTemplate.FreestyleWorld);
            var history = new WorkshopHistory(preset.Snapshot);
            var module = history.Current.Course().Modules.First(m => m.LayerId == history.Current.ActiveLayer);
            var definition = preset.Catalogue.Modules[module.DefinitionId];
            row.ModuleId = module.Id;
            row.LocalPosition = new Vector3(0, 2.7f, definition.DepthMm / 2000f - .2f);
            IReadOnlyList<WordboundModuleDressingEntry> selected = new[] { row };
            string before = history.Current.RecoveryJson;
            var sourceScale = source.transform.localScale;
            var position = row.LocalPosition;
            var materialBefore = EditorJsonUtility.ToJson(material);
            using (var view = new WordboundWorkshopViewport(owner.transform))
            {
                view.ModuleDressingProvider = () => selected;
                view.Sync(history.Current, preset.Catalogue);
                Assert.IsEmpty(Fixtures(), "Wall fixtures need full walls; the editing viewport is a cutaway.");
                Assert.IsTrue(owner.GetComponentsInChildren<MeshRenderer>().Any(r => r.name == "Floor"), "The cutaway itself must still be rendered.");
                Assert.AreEqual(before, history.Current.RecoveryJson);
                selected = null; view.Sync(history.Current, preset.Catalogue);
                Assert.IsEmpty(Fixtures());
                selected = new[] { row };
                history.Execute("Hide fixture floor", draft => draft.HiddenLayers.Add(module.LayerId));
                view.Sync(history.Current, preset.Catalogue); Assert.IsEmpty(Fixtures());
                Assert.IsTrue(material); Assert.IsTrue(mesh); Assert.IsTrue(source);
            }
            Assert.IsEmpty(Fixtures()); Assert.IsTrue(material); Assert.IsTrue(mesh);
            Assert.AreEqual(position, row.LocalPosition); Assert.AreSame(source, row.Prefab);
            Assert.AreEqual(sourceScale, source.transform.localScale);
            Assert.AreEqual(materialBefore, EditorJsonUtility.ToJson(material));
        }

        Transform[] Fixtures() => owner.GetComponentsInChildren<Transform>(true).Where(t => t.name == WordboundModuleDressing.RootName).ToArray();
        static string Shape(Collider c) => c.GetComponent<WordboundCourseNode>().Kind + "/" + c.transform.position + "/" +
            c.transform.rotation + "/" + c.transform.localScale + "/" + ((BoxCollider)c).center + "/" + ((BoxCollider)c).size;
    }
}
