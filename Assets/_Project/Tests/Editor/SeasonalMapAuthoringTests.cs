using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Wreckabulary.Art;
using Wreckabulary.EditorTools;
using Object = UnityEngine.Object;

namespace Wreckabulary.EditorTests
{
    public sealed class SeasonalMapAuthoringTests
    {
        GameObject contents;
        AuthoredHouse world;
        byte[] originalPrefab;
        string temporaryFolder;

        [SetUp]
        public void SetUp()
        {
            originalPrefab = File.ReadAllBytes(SeasonalMapAuthoring.WorldPath);
            contents = PrefabUtility.LoadPrefabContents(SeasonalMapAuthoring.WorldPath);
            world = contents.GetComponent<AuthoredHouse>();
            foreach (var marker in contents.GetComponentsInChildren<SeasonalRoomDressing>(true))
                if (marker.CollectionId == SeasonalMapAuthoring.CollectionId && marker.RoomId == SeasonalMapAuthoring.RoomId)
                    Object.DestroyImmediate(marker.gameObject);
        }

        [TearDown]
        public void TearDown()
        {
            if (contents)
            {
                var transient = contents.GetComponentsInChildren<MeshFilter>(true).Select(filter => filter.sharedMesh)
                    .Where(mesh => mesh && !EditorUtility.IsPersistent(mesh)).Distinct().ToArray();
                PrefabUtility.UnloadPrefabContents(contents);
                foreach (var mesh in transient) if (mesh) Object.DestroyImmediate(mesh);
            }
            if (!string.IsNullOrEmpty(temporaryFolder)) AssetDatabase.DeleteAsset(temporaryFolder);
            CollectionAssert.AreEqual(originalPrefab, File.ReadAllBytes(SeasonalMapAuthoring.WorldPath),
                "Seasonal tests must never write the production world prefab.");
        }

        [Test]
        public void AdditionPreservesAllOriginalTransformsMeshesMaterialsAndGameplayComponents()
        {
            var transforms = contents.GetComponentsInChildren<Transform>(true).ToDictionary(t => t, t => t.localToWorldMatrix);
            var meshes = contents.GetComponentsInChildren<MeshFilter>(true).ToDictionary(f => f, f => f.sharedMesh);
            var materials = contents.GetComponentsInChildren<Renderer>(true).ToDictionary(r => r, r => r.sharedMaterials);
            var colliders = contents.GetComponentsInChildren<Collider>(true);
            var furniture = contents.GetComponentsInChildren<Smashable>(true);
            var letters = contents.GetComponentsInChildren<LetterBuilt>(true);
            Assert.IsTrue(SeasonalMapAuthoring.ApplyMissing(world));
            foreach (var item in transforms) Assert.AreEqual(item.Value, item.Key.localToWorldMatrix, item.Key.name);
            foreach (var item in meshes) Assert.AreSame(item.Value, item.Key.sharedMesh, item.Key.name);
            foreach (var item in materials) CollectionAssert.AreEqual(item.Value, item.Key.sharedMaterials, item.Key.name);
            CollectionAssert.AreEquivalent(colliders, contents.GetComponentsInChildren<Collider>(true));
            CollectionAssert.AreEquivalent(furniture, contents.GetComponentsInChildren<Smashable>(true));
            CollectionAssert.AreEquivalent(letters, contents.GetComponentsInChildren<LetterBuilt>(true));
            var addition = SeasonalMapAuthoring.Find(world);
            Assert.NotNull(addition);
            Assert.AreEqual("Storey 0", addition.transform.parent.name);
            Assert.IsEmpty(addition.GetComponentsInChildren<Collider>(true));
            Assert.IsEmpty(addition.GetComponentsInChildren<Rigidbody>(true));
            Assert.IsEmpty(addition.GetComponentsInChildren<Light>(true));
            Assert.IsEmpty(addition.GetComponentsInChildren<SummonedThing>(true));
            var filters = addition.GetComponentsInChildren<MeshFilter>(true);
            long triangles = filters.Sum(filter => Enumerable.Range(0, filter.sharedMesh.subMeshCount)
                .Sum(index => (long)filter.sharedMesh.GetIndexCount(index) / 3));
            Assert.LessOrEqual(triangles, 6000);
            Assert.LessOrEqual(addition.GetComponentsInChildren<Renderer>(true).Sum(renderer => renderer.sharedMaterials.Length), 12);
            Assert.IsFalse(addition.GetComponentsInChildren<Transform>(true).Any(t => t.name.Contains("Stand") || t.name.Contains("mounting board")),
                "The gameplay room must not contain the lobby display stand.");
            Assert.AreEqual(5, filters.Count(filter => EditorUtility.IsPersistent(filter.sharedMesh)), "Tree, gifts and wreath reuse verified saved meshes.");
            Assert.IsTrue(addition.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).All(EditorUtility.IsPersistent));
        }

        [Test]
        public void RepeatUsesMetadataAndPreservesArtistTransformsMaterialsAndRemovedChildren()
        {
            Assert.IsTrue(SeasonalMapAuthoring.ApplyMissing(world));
            var addition = SeasonalMapAuthoring.Find(world);
            addition.name = "My edited festive corner";
            var tree = addition.transform.Find("Floor decorations/Winter tree");
            tree.localPosition += new Vector3(.06f, .01f, -.05f);
            tree.localRotation = Quaternion.Euler(0f, 23f, 0f);
            var renderer = tree.GetComponent<MeshRenderer>();
            renderer.sharedMaterials = renderer.sharedMaterials.Reverse().ToArray();
            Object.DestroyImmediate(addition.transform.Find("Floor decorations/Small wrapped gift").gameObject);
            addition.gameObject.SetActive(false);
            var matrix = tree.localToWorldMatrix;
            var materials = renderer.sharedMaterials;
            int count = contents.GetComponentsInChildren<Transform>(true).Length;
            Assert.IsFalse(SeasonalMapAuthoring.ApplyMissing(world));
            Assert.AreSame(addition, SeasonalMapAuthoring.Find(world));
            Assert.AreEqual(matrix, tree.localToWorldMatrix);
            CollectionAssert.AreEqual(materials, renderer.sharedMaterials);
            Assert.AreEqual(count, contents.GetComponentsInChildren<Transform>(true).Length);
            Assert.IsFalse(addition.gameObject.activeSelf, "An artist-hidden corner remains hidden.");
        }

        [Test]
        public void WallAttachmentsHaveRoomCutawaysAndTheGroundedTreeAndGiftsDoNot()
        {
            Assert.IsTrue(SeasonalMapAuthoring.ApplyMissing(world));
            var addition = SeasonalMapAuthoring.Find(world);
            Assert.AreEqual(SeasonalRoomDressing.CurrentEdition, addition.Edition);
            Assert.AreEqual("LivingRoom", addition.RoomId);
            Assert.AreEqual(0, addition.OwnerStorey);
            var wall = addition.transform.Find("North wall decorations");
            var cutaway = wall.GetComponent<CutawaySurface>();
            Assert.NotNull(cutaway);
            Assert.AreEqual(CutawayKind.UpperWall, cutaway.Kind);
            Assert.AreEqual(addition.RoomId, cutaway.RoomId);
            Assert.AreEqual(addition.OwnerStorey, cutaway.OwnerStorey);
            CollectionAssert.AreEquivalent(wall.GetComponentsInChildren<Renderer>(true), cutaway.Renderers);
            var floor = addition.transform.Find("Floor decorations");
            Assert.IsEmpty(floor.GetComponentsInChildren<CutawaySurface>(true));
            foreach (Transform part in floor)
            {
                var bounds = ModelVisual.BoundsIn(world.GeometryRoot, part.gameObject);
                Assert.That(bounds.min.y, Is.EqualTo(0f).Within(.005f), part.name + " is grounded");
                Assert.Greater(bounds.min.x, -5.4f, "Keep the room's central and north-door routes open.");
                Assert.Less(bounds.max.x, -4.2f, "Keep the meshes inside the wall.");
            }
            Assert.Greater(ModelVisual.BoundsIn(world.GeometryRoot, wall.gameObject).min.y, 1.8f);
        }

        [Test]
        public void WallMountsFollowTheSavedWallFaceInsteadOfFloatingAtAFixedPlane()
        {
            Assert.IsTrue(SeasonalMapAuthoring.ApplyMissing(world));
            var first = SeasonalMapAuthoring.Find(world);
            var original = first.transform.Find("North wall decorations").GetComponentsInChildren<MeshRenderer>()
                .ToDictionary(renderer => renderer.name, renderer => ModelVisual.BoundsIn(world.GeometryRoot, renderer.gameObject).max.z);
            var temporary = first.GetComponentsInChildren<MeshFilter>().Select(filter => filter.sharedMesh)
                .Where(mesh => mesh && !EditorUtility.IsPersistent(mesh)).ToArray();
            Object.DestroyImmediate(first.gameObject);
            foreach (var mesh in temporary) Object.DestroyImmediate(mesh);
            int moved = 0;
            foreach (var renderer in world.GeometryRoot.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (renderer.name != "Upper wall infill" &&
                    !(renderer.name.StartsWith("Wall", StringComparison.Ordinal) && renderer.GetComponent<BoxCollider>())) continue;
                var bounds = ModelVisual.BoundsIn(world.GeometryRoot, renderer.gameObject);
                if (Mathf.Abs(bounds.center.z - 4f) > .3f || bounds.max.x > -3.9f || bounds.min.x < -6.3f) continue;
                renderer.transform.position += world.GeometryRoot.TransformVector(Vector3.back * .06f);
                moved++;
            }
            Assert.Greater(moved, 0, "The fixture must shift actual saved wall geometry.");
            Assert.IsTrue(SeasonalMapAuthoring.ApplyMissing(world));
            var second = SeasonalMapAuthoring.Find(world);
            foreach (var renderer in second.transform.Find("North wall decorations").GetComponentsInChildren<MeshRenderer>())
                Assert.That(ModelVisual.BoundsIn(world.GeometryRoot, renderer.gameObject).max.z,
                    Is.EqualTo(original[renderer.name] - .06f).Within(.001f), renderer.name + " follows the authored face");
        }

        [Test]
        public void OccupiedCornerRejectsTheAdditionWithoutLeavingPartialObjects()
        {
            var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blocker.name = "Artist's existing corner cabinet";
            blocker.transform.SetParent(world.FurnitureRoot, false);
            blocker.transform.SetPositionAndRotation(world.GeometryRoot.TransformPoint(new Vector3(-4.85f, .8f, 3f)), world.GeometryRoot.rotation);
            blocker.transform.localScale = new Vector3(.9f, 1.6f, .95f);
            int objects = contents.GetComponentsInChildren<Transform>(true).Length;
            var position = blocker.transform.localToWorldMatrix;
            var error = Assert.Throws<InvalidOperationException>(() => SeasonalMapAuthoring.ApplyMissing(world));
            StringAssert.Contains("authored furniture", error.Message);
            Assert.IsNull(SeasonalMapAuthoring.Find(world));
            Assert.AreEqual(objects, contents.GetComponentsInChildren<Transform>(true).Length);
            Assert.AreEqual(position, blocker.transform.localToWorldMatrix);
        }

        [Test]
        public void IsolatedPrefabRoundTripKeepsSeasonalMeshReferencesAndArtistEdits()
        {
            Assert.IsTrue(SeasonalMapAuthoring.ApplyMissing(world));
            var addition = SeasonalMapAuthoring.Find(world);
            var garland = addition.transform.Find("North wall decorations/Brass winter garland").GetComponent<MeshFilter>();
            var transient = garland.sharedMesh;
            temporaryFolder = "Assets/_Project/Tests/SeasonalFixture_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets/_Project/Tests", Path.GetFileName(temporaryFolder));
            var persistent = Object.Instantiate(transient);
            AssetDatabase.CreateAsset(persistent, temporaryFolder + "/Garland.asset");
            garland.sharedMesh = persistent;
            Object.DestroyImmediate(transient);
            addition.transform.localPosition += new Vector3(.01f, 0f, .015f);
            Vector3 edited = addition.transform.localPosition;
            string path = temporaryFolder + "/House.prefab";
            Assert.NotNull(PrefabUtility.SaveAsPrefabAsset(contents, path));
            var reopened = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var savedWorld = reopened.GetComponent<AuthoredHouse>();
                var saved = SeasonalMapAuthoring.Find(savedWorld);
                Assert.NotNull(saved);
                Assert.AreEqual(edited, saved.transform.localPosition);
                Assert.IsTrue(saved.GetComponentsInChildren<MeshFilter>(true).All(filter => filter.sharedMesh && EditorUtility.IsPersistent(filter.sharedMesh)));
                Assert.IsFalse(SeasonalMapAuthoring.ApplyMissing(savedWorld));
            }
            finally { PrefabUtility.UnloadPrefabContents(reopened); }
        }
    }
}
