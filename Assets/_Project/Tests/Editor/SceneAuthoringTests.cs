using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Wreckabulary.Art;
using Wreckabulary.EditorTools;

namespace Wreckabulary.EditorTests
{
    public sealed class SceneAuthoringTests
    {
        [TestCase("Hub")]
        [TestCase("LivingRoom")]
        [TestCase("MovingDay")]
        [TestCase("Tutorial")]
        public void SavedScenesHaveCurrentPersistentVisualsAndThirdPersonPreview(string name)
        {
            string path = SceneWorkspace.SceneFolder + name + ".unity";
            var asset = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
            Assert.That(AssetDatabase.GetLabels(asset), Does.Contain(SceneWorkspace.MigrationLabel));
            var scene = EditorSceneManager.OpenPreviewScene(path);
            try
            {
                var roots = scene.GetRootGameObjects();
                var preview = roots.SelectMany(root => root.GetComponentsInChildren<EditorScenePreview>(true)).Single();
                Assert.AreEqual("EditorOnly", preview.tag);
                Assert.IsNull(preview.GetComponentInChildren<PlayerController>(true));
                Assert.IsNotNull(preview.GetComponentInChildren<SkinnedMeshRenderer>(true));
                var camera = roots.SelectMany(root => root.GetComponentsInChildren<Camera>()).Single();
                Assert.AreEqual(55f, camera.fieldOfView, .01f);
                Assert.AreEqual(.08f, camera.nearClipPlane, .001f);
                Assert.That(camera.transform.eulerAngles.x, Is.InRange(9f, 11f));
                var world = roots.SelectMany(root => root.GetComponentsInChildren<AuthoredHouse>(true)).FirstOrDefault();
                if (name is "LivingRoom" or "MovingDay")
                {
                    Assert.IsNotNull(world);
                    Assert.IsTrue(world.IsCurrent);
                    Assert.IsNotNull(PrefabUtility.GetCorrespondingObjectFromSource(world));
                    Assert.IsFalse(roots.Any(root => root.name is "Room" or "Furniture" or "Legacy scene content (preserved)"));
                    Assert.That(world.FurnitureRoot.GetComponentsInChildren<LetterBuilt>().Count(item => item.UsesImportedModel), Is.GreaterThan(5));
                }
                foreach (var presentation in roots.SelectMany(root => root.GetComponentsInChildren<HousePresentation>(true)))
                {
                    Assert.IsTrue(presentation.IsAuthored);
                    foreach (var renderer in presentation.GetComponentsInChildren<Renderer>())
                        foreach (var material in renderer.sharedMaterials)
                            Assert.IsTrue(EditorUtility.IsPersistent(material), renderer.name + " must survive editor reopen.");
                    foreach (var mesh in presentation.GetComponentsInChildren<MeshFilter>())
                        Assert.IsTrue(EditorUtility.IsPersistent(mesh.sharedMesh), mesh.name + " must survive editor reopen.");
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [TestCase("pinwheel")]
        [TestCase("courtyard")]
        [TestCase("flat")]
        [TestCase("terrace")]
        [TestCase("walkup")]
        public void AuthoredMapsPersistGeneratedSurfaceAndFurnitureTextureDependencies(string map)
        {
            string path = "Assets/_Project/Resources/" + AuthoredHouse.ResourcePath(map) + ".prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(prefab, "Run UpgradeWorldAssetsToCurrentVersion first: " + path);
            var world = prefab.GetComponent<AuthoredHouse>();
            Assert.IsTrue(world && world.IsCurrent, map);
            var layout = GameConfig.Current.HouseFor(map);
            for (int storey = 0; storey < layout.StoreyFloors().Count; storey++)
                Assert.IsNotNull(world.GeometryRoot.Find(RoomBuilder.StoreyName(storey)), map);
            Assert.IsNotEmpty(world.GeometryRoot.GetComponentsInChildren<TallWall>(true));
            var floor = world.GeometryRoot.GetComponentsInChildren<Renderer>(true).First(renderer => renderer.name.EndsWith(" floor"));
            Assert.IsNotNull(floor.sharedMaterial.mainTexture, "Surface albedo must survive prefab reload.");
            Assert.IsNotNull(floor.sharedMaterial.GetTexture("_BumpMap"), "Surface normal must survive prefab reload.");
            AssertPatternPixels(floor.sharedMaterial.mainTexture as Texture2D, map + " floor albedo");
            AssertPatternPixels(floor.sharedMaterial.GetTexture("_BumpMap") as Texture2D, map + " floor normal");
            foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(true))
                foreach (var material in renderer.sharedMaterials)
                {
                    Assert.IsTrue(material && EditorUtility.IsPersistent(material), renderer.name);
                    foreach (string property in material.GetTexturePropertyNames())
                    {
                        var texture = material.GetTexture(property);
                        if (texture) Assert.IsTrue(EditorUtility.IsPersistent(texture), map + ": " + material.name + " " + property);
                    }
                }
            foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
                Assert.IsTrue(filter.sharedMesh && EditorUtility.IsPersistent(filter.sharedMesh), filter.name);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void GpuOnlyTextureReadbackPreservesPixelsColourSpaceMipsAndSavedContent(bool linear)
        {
            const int size = 8;
            var expected = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    expected[y * size + x] = new Color32((byte)(x * 31 + 9), (byte)(y * 29 + 13), (byte)((x + y) * 15), 255);
            var source = new Texture2D(size, size, TextureFormat.RGBA32, true, linear)
            {
                name = "Non-readable persistence probe", filterMode = FilterMode.Trilinear,
                wrapModeU = TextureWrapMode.Clamp, wrapModeV = TextureWrapMode.Repeat,
                anisoLevel = 4, mipMapBias = .25f
            };
            Texture2D saved = null;
            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/_Project/Tests/Editor/TexturePersistenceProbe.asset");
            try
            {
                source.SetPixels32(expected);
                source.Apply(true, true);
                Assert.IsFalse(source.isReadable);
                saved = WorldAuthoring.ReadTextureForPersistence(source);
                Assert.AreEqual(source.graphicsFormat, saved.graphicsFormat);
                Assert.AreEqual(source.mipmapCount, saved.mipmapCount);
                Assert.AreEqual(source.filterMode, saved.filterMode);
                Assert.AreEqual(source.wrapModeU, saved.wrapModeU);
                Assert.AreEqual(source.wrapModeV, saved.wrapModeV);
                Assert.AreEqual(source.anisoLevel, saved.anisoLevel);
                Assert.AreEqual(source.mipMapBias, saved.mipMapBias);
                AssetDatabase.CreateAsset(saved, path);
                AssetDatabase.SaveAssets();
                Resources.UnloadAsset(saved);
                saved = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                Assert.IsTrue(saved && saved.isReadable, "The saved CPU pixel payload must reload.");
                Assert.AreEqual(source.graphicsFormat, saved.graphicsFormat);
                Assert.AreEqual(source.mipmapCount, saved.mipmapCount);
                var actual = saved.GetPixels32();
                Assert.AreEqual(expected.Length, actual.Length);
                for (int i = 0; i < actual.Length; i++)
                {
                    Assert.AreEqual(expected[i].r, actual[i].r, 1, "red at " + i);
                    Assert.AreEqual(expected[i].g, actual[i].g, 1, "green at " + i);
                    Assert.AreEqual(expected[i].b, actual[i].b, 1, "blue at " + i);
                    Assert.AreEqual(expected[i].a, actual[i].a, 1, "alpha at " + i);
                }
            }
            finally
            {
                Object.DestroyImmediate(source);
                if (saved && !EditorUtility.IsPersistent(saved)) Object.DestroyImmediate(saved);
                AssetDatabase.DeleteAsset(path);
            }
        }

        static void AssertPatternPixels(Texture2D texture, string description)
        {
            Assert.IsNotNull(texture, description);
            Assert.IsTrue(texture.isReadable, description + " must retain CPU data for serialization.");
            var pixels = texture.GetPixels32();
            Assert.That(pixels.Length, Is.GreaterThan(16), description);
            var first = pixels[0];
            Assert.IsTrue(pixels.Any(pixel => !pixel.Equals(first)), description + " must contain a pattern, not a blank fallback.");
            Assert.IsTrue(pixels.Any(pixel => pixel.a > 0), description + " must contain visible pixels.");
        }
    }
}
