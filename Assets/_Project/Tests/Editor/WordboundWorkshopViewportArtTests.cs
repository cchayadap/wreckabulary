using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using Wreckabulary.Art;
using Wreckabulary.Rules.Wordbound;
using Wreckabulary.Workshop;
using Object = UnityEngine.Object;

namespace Wreckabulary.EditorTests
{
    public sealed class WordboundWorkshopViewportArtTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void OriginalModelsFitCanonicalPlacementsWithoutChangingSeedPickingOrBorrowedAssets(bool resolved)
        {
            var preset = WorkshopTemplates.Create(WorkshopTemplate.FreestyleWorld);
            var owner = new GameObject("Workshop original-art test");
            var made = new List<(string id, GameObject model)>();
            var sources = new Dictionary<string, (GameObject model, Mesh[] meshes, Material[] materials)>();
            string before = preset.Snapshot.RecoveryJson;
            try
            {
                using (var view = new WordboundWorkshopViewport(owner.transform))
                {
                    view.SpawnAssetVisual = (id, parent) => {
                        var original = ModelLibrary.Load().Find("Items/" + id);
                        Assert.IsNotNull(original, id + " requires its shipped original model.");
                        if (!sources.ContainsKey(id))
                            sources[id] = (original, Meshes(original), Materials(original));
                        var visual = ModelVisual.Spawn("Items/" + id, parent, wordbound: false);
                        Assert.IsNotNull(visual); made.Add((id, visual)); return visual;
                    };
                    var exact = new WorkshopHistory(preset.Snapshot).Preview(preset.Catalogue, preset.Play);
                    Assert.IsTrue(exact.Success);
                    view.Sync(preset.Snapshot, preset.Catalogue, resolved ? exact.Manifest : null); view.Frame();
                    Assert.Greater(made.Count, 0);
                    foreach (var item in made)
                    {
                        var anchor = item.model.transform.parent.parent;
                        var bounds = ModelVisual.BoundsIn(anchor, item.model);
                        var expected = preset.Catalogue.Assets[item.id];
                        Assert.That(bounds.size.x, Is.EqualTo(expected.WidthMm / 1000f).Within(.001f));
                        Assert.That(bounds.size.y, Is.EqualTo(expected.HeightMm / 1000f).Within(.001f));
                        Assert.That(bounds.size.z, Is.EqualTo(expected.DepthMm / 1000f).Within(.001f));
                        Assert.That(bounds.min.y, Is.EqualTo(0).Within(.002f),
                            "The isolated preview is 20 km below the world, where float position precision is about 2 mm.");
                        Assert.That(bounds.center.x, Is.EqualTo(0).Within(.001f));
                        Assert.That(bounds.center.z, Is.EqualTo(0).Within(.001f));
                        Assert.IsTrue(item.model.GetComponentsInChildren<Transform>(true).All(t => t.gameObject.layer == 31));
                        Assert.IsTrue(item.model.GetComponentsInChildren<Collider>(true).All(c => !c.enabled));
                    }
                    var zone = preset.Snapshot.Course().Zones.Single(z => z.Placement == "Fixed");
                    var asset = preset.Catalogue.Assets[zone.Pool[0].AssetId];
                    var center = new CoursePoint(zone.FixedPosition.X, zone.FixedPosition.Y + asset.HeightMm / 2, zone.FixedPosition.Z);
                    var area = new Rect(0, 0, 800, 600);
                    Assert.AreEqual(zone.Id, view.PickFixed(view.Project(center, area), area));
                    Assert.AreEqual(before, preset.Snapshot.RecoveryJson);
                    Assert.AreEqual(exact.Manifest.Hash, new WorkshopHistory(preset.Snapshot).Preview(preset.Catalogue, preset.Play).Manifest.Hash);
                }
                foreach (var source in sources.Values)
                {
                    Assert.IsTrue(source.model);
                    CollectionAssert.AreEqual(source.meshes, Meshes(source.model));
                    CollectionAssert.AreEqual(source.materials, Materials(source.model));
                    Assert.IsTrue(source.meshes.All(mesh => mesh));
                    Assert.IsTrue(source.materials.All(material => material));
                }
            }
            finally { Object.DestroyImmediate(owner); }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void FittedFixedPropStillFollowsTransientDragAndCancelWithoutPublishingHistory(bool resolved)
        {
            var preset = WorkshopTemplates.Create(WorkshopTemplate.FreestyleWorld);
            var history = new WorkshopHistory(preset.Snapshot);
            var zone = history.Current.Course().Zones.Single(z => z.Placement == "Fixed");
            var owner = new GameObject("Workshop fitted drag test");
            try
            {
                using var view = new WordboundWorkshopViewport(owner.transform);
                view.SpawnAssetVisual = (id, parent) => ModelVisual.Spawn("Items/" + id, parent, wordbound: false);
                var exact = history.Preview(preset.Catalogue, preset.Play);
                Assert.IsTrue(exact.Success);
                view.Sync(history.Current, preset.Catalogue, resolved ? exact.Manifest : null);
                var anchor = owner.GetComponentsInChildren<Transform>().Single(t => t.name == zone.Id);
                var original = anchor.position; var rotation = anchor.rotation;
                string before = history.Current.RecoveryJson;
                var drag = new WorkshopPlacementDrag(history, zone.Id); drag.Move(700, 300); drag.Rotate(-90);
                view.PlacementPreview(drag);
                Assert.That(Vector3.Distance(anchor.position, original + new Vector3(.5f, 0, .5f)), Is.LessThan(.001f));
                Assert.AreEqual(before, history.Current.RecoveryJson);
                view.PlacementPreview(null);
                Assert.That(Vector3.Distance(anchor.position, original), Is.LessThan(.001f));
                Assert.That(Quaternion.Angle(anchor.rotation, rotation), Is.LessThan(.001f));
                Assert.IsFalse(history.CanUndo);
            }
            finally { Object.DestroyImmediate(owner); }
        }

        [Test]
        public void ForeignFactoryResultIsRejectedWithoutMovingOrDestroyingTheBorrowedObject()
        {
            var preset = WorkshopTemplates.Create(WorkshopTemplate.FreestyleWorld);
            var owner = new GameObject("Workshop ownership test");
            var borrowed = GameObject.CreatePrimitive(PrimitiveType.Cube);
            borrowed.transform.position = new Vector3(12, 4, 9);
            var position = borrowed.transform.position; var scale = borrowed.transform.localScale;
            try
            {
                using (var view = new WordboundWorkshopViewport(owner.transform))
                {
                    view.SpawnAssetVisual = (id, parent) => borrowed;
                    Assert.Throws<InvalidOperationException>(() => view.Sync(preset.Snapshot, preset.Catalogue));
                    Assert.IsTrue(borrowed); Assert.AreEqual(position, borrowed.transform.position); Assert.AreEqual(scale, borrowed.transform.localScale);
                    Assert.IsTrue(borrowed.GetComponent<Collider>().enabled);
                }
                Assert.IsTrue(borrowed); Assert.AreEqual(position, borrowed.transform.position);
            }
            finally { Object.DestroyImmediate(owner); Object.DestroyImmediate(borrowed); }
        }

        [Test]
        public void PreviewLightsStayOnThePreviewLayerAndDisposeWithoutChangingOtherLights()
        {
            var owner = new GameObject("Workshop light test");
            var other = new GameObject("Unrelated light").AddComponent<Light>();
            var otherCamera = new GameObject("Unrelated camera").AddComponent<Camera>();
            other.cullingMask = 1 << 8; other.intensity = .37f; other.color = Color.magenta;
            try
            {
                using (var view = new WordboundWorkshopViewport(owner.transform))
                {
                    var lights = owner.GetComponentsInChildren<Light>(true);
                    Assert.AreEqual(2, lights.Length);
                    Assert.IsTrue(lights.All(l => !l.enabled), "Preview lights must be disabled between renders.");
                    foreach (var light in lights)
                    {
                        Assert.AreEqual(1 << 31, light.cullingMask);
                        Assert.AreEqual(LightShadows.None, light.shadows);
                        Assert.AreEqual(Color.white, light.color);
                    }
                    var lens = owner.GetComponentInChildren<Camera>();
                    CameraEvent(view, "BeginPreviewCamera", lens);
                    Assert.IsTrue(lights.All(l => l.enabled));
                    CameraEvent(view, "EndPreviewCamera", lens);
                    Assert.IsTrue(lights.All(l => !l.enabled));
                    CameraEvent(view, "BeginPreviewCamera", lens);
                    CameraEvent(view, "BeginPreviewCamera", otherCamera);
                    Assert.IsTrue(lights.All(l => !l.enabled), "An unrelated camera must disable preview lights before culling.");
                    view.Active = false; Assert.IsTrue(lights.All(l => !l.gameObject.activeInHierarchy));
                    Assert.AreEqual(1 << 8, other.cullingMask); Assert.AreEqual(.37f, other.intensity); Assert.AreEqual(Color.magenta, other.color);
                }
                Assert.IsEmpty(owner.GetComponentsInChildren<Light>(true));
                Assert.IsTrue(other); Assert.AreEqual(.37f, other.intensity);
            }
            finally { Object.DestroyImmediate(owner); Object.DestroyImmediate(other.gameObject); Object.DestroyImmediate(otherCamera.gameObject); }
        }

        [Test]
        public void UnavailableArtworkKeepsAnExplicitNonphysicalCanonicalPlaceholder()
        {
            var preset = WorkshopTemplates.Create(WorkshopTemplate.FreestyleWorld);
            var zone = preset.Snapshot.Course().Zones.Single(z => z.Placement == "Fixed");
            var owner = new GameObject("Workshop missing-art test");
            try
            {
                using var view = new WordboundWorkshopViewport(owner.transform);
                view.SpawnAssetVisual = (id, parent) => null;
                view.Sync(preset.Snapshot, preset.Catalogue);
                var anchor = owner.GetComponentsInChildren<Transform>().Single(t => t.name == zone.Id);
                Assert.IsNotNull(anchor.GetComponentInChildren<MeshRenderer>());
                Assert.IsEmpty(anchor.GetComponentsInChildren<Collider>(true));
                Assert.IsEmpty(anchor.GetComponentsInChildren<Rigidbody>(true));
                var expected = preset.Catalogue.Assets[zone.Pool[0].AssetId];
                var bounds = ModelVisual.BoundsIn(anchor, anchor.gameObject);
                Assert.That(bounds.size.y, Is.EqualTo(expected.HeightMm / 1000f).Within(.001f));
            }
            finally { Object.DestroyImmediate(owner); }
        }

        static Mesh[] Meshes(GameObject root) => root.GetComponentsInChildren<MeshFilter>(true).Select(f => f.sharedMesh)
            .Concat(root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(r => r.sharedMesh)).ToArray();
        static Material[] Materials(GameObject root) => root.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).ToArray();
        static void CameraEvent(WordboundWorkshopViewport view, string method, Camera camera) => typeof(WordboundWorkshopViewport)
            .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(view, new object[] { default(ScriptableRenderContext), camera });
    }
}
