using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Wreckabulary.Art;
using Wreckabulary.EditorTools;

namespace Wreckabulary.EditorTests
{
    public sealed class EnvironmentAuthoringTests
    {
        [TestCase("pinwheel")]
        [TestCase("courtyard")]
        [TestCase("flat")]
        [TestCase("terrace")]
        [TestCase("walkup")]
        public void SavedDressingIsPersistentNonSolidAndClearsOutgoingStairs(string map)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Resources/" + AuthoredHouse.ResourcePath(map) + ".prefab");
            var world = prefab.GetComponent<AuthoredHouse>();
            var layout = GameConfig.Current.HouseFor(map);
            var dressing = world.GetComponentsInChildren<RoomDressing>(true);
            CollectionAssert.AreEquivalent(layout.Rooms.Where(r => r.Name != "Garden").Select(r => r.Name), dressing.Select(d => d.RoomId));
            AssertPresentation(world.gameObject);
            foreach (var room in dressing)
            {
                Assert.IsEmpty(room.GetComponentsInChildren<Collider>(true), room.RoomId + " must not change collision routes");
                Assert.IsEmpty(room.GetComponentsInChildren<Smashable>(true), room.RoomId + " must not add loot or round ownership");
                Assert.AreEqual(layout.StoreyOf(layout.Room(room.RoomId)), room.OwnerStorey);
                Assert.IsTrue(room.HasDoorLintels);
                var headers = room.GetComponentsInChildren<Renderer>(true).Where(r => r.name == "Door lintel").ToArray();
                Assert.AreEqual(layout.Doors.Count(d => d.A == room.RoomId || d.B == room.RoomId), headers.Length);
                foreach (var header in headers)
                    Assert.GreaterOrEqual(ModelVisual.BoundsIn(world.GeometryRoot, header.gameObject).min.y,
                        layout.Room(room.RoomId).FloorY + 2.399f, "Door headers retain the full 2.4 m opening.");
                foreach (var ceiling in room.GetComponentsInChildren<Renderer>(true).Where(r => r.name == "Ceiling panel"))
                {
                    var b = ModelVisual.BoundsIn(world.GeometryRoot, ceiling.gameObject);
                    foreach (var stair in layout.Stairs.Where(s => s.Lower == room.RoomId))
                        Assert.IsFalse(b.min.x < stair.MaxX && b.max.x > stair.MinX && b.min.z < stair.MaxZ && b.max.z > stair.MinZ,
                            map + ": " + room.RoomId + " ceiling must leave the stair flight open");
                }
                foreach (var cutaway in room.GetComponentsInChildren<CutawaySurface>(true))
                {
                    Assert.AreEqual(room.OwnerStorey, cutaway.OwnerStorey);
                    Assert.AreEqual(room.RoomId, cutaway.RoomId);
                    Assert.IsNotEmpty(cutaway.Renderers);
                }
            }
        }

        [Test]
        public void ImportedRootRepairRetainsArtistPoseAndRestoresFbxAxesOnce()
        {
            string path = "Assets/_Project/Resources/" + AuthoredHouse.ResourcePath("pinwheel") + ".prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var marker = root.GetComponentsInChildren<RoomDressing>(true).First(d => d.GetComponentsInChildren<Transform>(true)
                    .Any(t => t.name == "Imported model anchor - Window_Frame"));
                var anchor = marker.GetComponentsInChildren<Transform>(true).First(t => t.name == "Imported model anchor - Window_Frame");
                var model = anchor.GetChild(0);
                var at = anchor.localPosition;
                var rotation = anchor.localRotation;
                var scale = anchor.localScale;
                model.SetParent(anchor.parent, false);
                model.SetLocalPositionAndRotation(at + new Vector3(.07f, .05f, .02f), rotation * Quaternion.Euler(0f, 8f, 0f));
                model.localScale = scale * 1.03f;
                Object.DestroyImmediate(anchor.gameObject);
                var artistPose = model.localToWorldMatrix;
                var serialized = new SerializedObject(marker);
                serialized.FindProperty("importedModelAnchorsVersion").intValue = 0;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.IsTrue(EnvironmentAuthoring.RepairImportedModelAnchors(root));
                Assert.AreEqual(artistPose, model.parent.localToWorldMatrix, "The old intended pose, including edits, belongs on the new anchor.");
                AssertImport(model, "Environment/Window_Frame");
                Assert.IsFalse(EnvironmentAuthoring.RepairImportedModelAnchors(root), "A second pass must preserve the repaired hierarchy.");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        [Test]
        public void RepeatedDressingPreservesAuthoredTransformsAndGameplayObjects()
        {
            string path = "Assets/_Project/Resources/" + AuthoredHouse.ResourcePath("terrace") + ".prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var world = root.GetComponent<AuthoredHouse>();
                foreach (var dressing in root.GetComponentsInChildren<RoomDressing>(true)) Object.DestroyImmediate(dressing.gameObject);
                var original = root.GetComponentsInChildren<Transform>(true).ToDictionary(t => t, t => t.localToWorldMatrix);
                int colliders = root.GetComponentsInChildren<Collider>(true).Length;
                Assert.IsTrue(EnvironmentAuthoring.ApplyMissing(world));
                foreach (var entry in original) Assert.AreEqual(entry.Value, entry.Key.localToWorldMatrix, entry.Key.name);
                Assert.AreEqual(colliders, root.GetComponentsInChildren<Collider>(true).Length);
                var panel = root.GetComponentsInChildren<Renderer>(true).First(r => r.name == "Painted wall panel");
                panel.transform.localPosition += new Vector3(.03f, .04f, .05f);
                var edited = panel.transform.localToWorldMatrix;
                int objects = root.GetComponentsInChildren<Transform>(true).Length;
                Assert.IsFalse(EnvironmentAuthoring.ApplyMissing(world));
                Assert.AreEqual(edited, panel.transform.localToWorldMatrix);
                Assert.AreEqual(objects, root.GetComponentsInChildren<Transform>(true).Length);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        [TestCase("Hub")]
        [TestCase("Tutorial")]
        public void SavedSceneDressingKeepsFrontOpenAndIsIdempotent(string name)
        {
            var scene = EditorSceneManager.OpenPreviewScene(SceneWorkspace.SceneFolder + name + ".unity");
            try
            {
                var root = scene.GetRootGameObjects().Single(r => r.name == "Room");
                var room = root.GetComponentInChildren<RoomDressing>(true);
                Assert.IsNotNull(room);
                var floor = root.GetComponentsInChildren<Renderer>(true).First(r => r.name == "Floor");
                var floorBounds = ModelVisual.BoundsIn(root.transform, floor.gameObject);
                foreach (var panel in room.GetComponentsInChildren<Renderer>(true).Where(r => r.name == "Ceiling panel"))
                    Assert.Greater(ModelVisual.BoundsIn(root.transform, panel.gameObject).min.z, floorBounds.center.z);
                var original = root.GetComponentsInChildren<Transform>(true).ToDictionary(t => t, t => t.localToWorldMatrix);
                Assert.IsFalse(EnvironmentAuthoring.ApplyMissingScene(root, name));
                foreach (var entry in original) Assert.AreEqual(entry.Value, entry.Key.localToWorldMatrix);
                AssertPresentation(root);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        static void AssertPresentation(GameObject root)
        {
            var dressing = root.GetComponentsInChildren<RoomDressing>(true);
            var renderers = dressing.SelectMany(d => d.GetComponentsInChildren<Renderer>(true)).ToArray();
            foreach (var room in dressing)
            {
                Assert.IsTrue(room.HasImportedModelAnchors, room.RoomId);
                foreach (var anchor in room.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("Imported model anchor - ")))
                {
                    Assert.AreEqual(1, anchor.childCount);
                    string model = anchor.name.Substring("Imported model anchor - ".Length);
                    AssertImport(anchor.GetChild(0), "Environment/" + model);
                    var bounds = ModelVisual.BoundsIn(anchor, anchor.GetChild(0).gameObject);
                    if (model == "Window_Frame")
                    {
                        Assert.Greater(bounds.size.y, .9f, "The window frame must be upright.");
                        Assert.Less(bounds.size.z, .25f, "The window frame must not lie flat outside the wall.");
                    }
                    if (model == "Wall_Sconce") Assert.Greater(bounds.size.y, .25f, "The complete fixture must stand above its wall mounting.");
                }
            }
            Assert.IsTrue(renderers.Any(r => r.name == "Window glass"), root.name + " window vignette");
            var prints = renderers.Where(r => r.name == "Print surface").ToArray();
            Assert.IsNotEmpty(prints, root.name + " generated artwork integration");
            foreach (var print in prints)
            {
                Assert.AreEqual(print.transform.localScale.x, print.transform.localScale.y, .001f, "square print must not stretch");
                Assert.AreEqual(EnvironmentAuthoring.PrintPath, AssetDatabase.GetAssetPath(print.sharedMaterial.GetTexture("_BaseMap")));
            }
            Assert.IsNotEmpty(root.GetComponentsInChildren<PracticalLight>(true));
            foreach (var lamp in root.GetComponentsInChildren<PracticalLight>(true))
                Assert.AreEqual(LightShadows.None, lamp.Lamp.shadows);
            foreach (var renderer in renderers)
                foreach (var material in renderer.sharedMaterials)
                {
                    Assert.IsTrue(material && EditorUtility.IsPersistent(material), renderer.name);
                    foreach (string property in material.GetTexturePropertyNames())
                    {
                        var texture = material.GetTexture(property);
                        if (texture) Assert.IsTrue(EditorUtility.IsPersistent(texture), material.name + property);
                    }
                }
            foreach (var filter in dressing.SelectMany(d => d.GetComponentsInChildren<MeshFilter>(true)))
                Assert.IsTrue(filter.sharedMesh && EditorUtility.IsPersistent(filter.sharedMesh), filter.name);
        }

        static void AssertImport(Transform model, string key)
        {
            var original = ModelLibrary.Load().Find(key).transform;
            Assert.AreEqual(original.localPosition, model.localPosition, key + " imported origin");
            Assert.AreEqual(original.localScale, model.localScale, key + " imported scale");
            Assert.Less(Quaternion.Angle(original.localRotation, model.localRotation), .01f, key + " imported axis conversion");
        }
    }
}
