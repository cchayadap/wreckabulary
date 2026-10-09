using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Wreckabulary.Art;
using Wreckabulary.EditorTools;

namespace Wreckabulary.EditorTests
{
    public class WinterPropsAssetTests
    {
        [TestCase("WinterTree", "Winter tree", 3000, 1.6f)]
        [TestCase("WrappedGifts", "Winter gifts", 1500, .46524f)]
        [TestCase("DoorWreath", "Wreath display/Mounted wreath", 1500, .65f)]
        public void VerifiedNativeMeshesRetainScaleMaterialsAndTriangleBudgets(string source, string child, int budget, float height)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WinterPropsBuilder.PrefabPath);
            Assert.IsNotNull(prefab);
            var prop = prefab.transform.Find(child);
            Assert.IsNotNull(prop);
            var bounds = ModelVisual.BoundsIn(prop, prop.gameObject);
            Assert.AreEqual(height, bounds.size.y, .002f, "FBX axis conversion and baked source scale");
            Assert.AreEqual(0f, bounds.min.y, .002f, "authored ground pivot");
            int triangles = prop.GetComponentsInChildren<MeshFilter>().Sum(filter => (int)filter.sharedMesh.GetIndexCount(0) / 3 +
                (filter.sharedMesh.subMeshCount > 1 ? (int)filter.sharedMesh.GetIndexCount(1) / 3 : 0));
            Assert.That(triangles, Is.InRange(100, budget));
            foreach (var renderer in prop.GetComponentsInChildren<MeshRenderer>())
            {
                Assert.That(renderer.sharedMaterials.Length, Is.InRange(1, 2));
                Assert.IsTrue(renderer.sharedMaterials.All(material => AssetDatabase.Contains(material)));
                Assert.IsTrue(renderer.sharedMaterials.All(material => material.shader.name == "Universal Render Pipeline/Lit"));
            }
            CollectionAssert.AreEqual(File.ReadAllBytes("ArtSource/Collections/Winter/Props/exports/" + source + ".fbx"),
                File.ReadAllBytes(WinterPropsBuilder.ArtRoot + "/" + source + ".fbx"), "native source bytes preserved");
        }

        [Test]
        public void DecorHasReusableParcelsAMountedWreathAndNoGameplayCost()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WinterPropsBuilder.PrefabPath);
            Assert.IsNotNull(prefab);
            foreach (string parcel in new[] { "GiftTall", "GiftWide", "GiftSmall" })
                Assert.IsNotNull(prefab.transform.Find("Winter gifts/" + parcel));
            Assert.IsNotNull(prefab.transform.Find("Wreath display/Wreath mounting board"));
            Assert.IsNotNull(prefab.transform.Find("Wreath display/Stand post"));
            Assert.IsNotNull(prefab.transform.Find("Wreath display/Stand foot"));
            Assert.IsEmpty(prefab.GetComponentsInChildren<Collider>(true));
            Assert.IsEmpty(prefab.GetComponentsInChildren<Light>(true));
            Assert.IsEmpty(prefab.GetComponentsInChildren<Camera>(true));
            Assert.IsEmpty(prefab.GetComponentsInChildren<Smashable>(true));
            Assert.LessOrEqual(prefab.GetComponentsInChildren<MeshRenderer>(true).Length, 8, "combined native display stays bounded");
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(WinterPropsBuilder.ArtRoot + "/WinterPalette.png");
            Assert.AreEqual(512, texture.width); Assert.AreEqual(64, texture.height);
            var importer = (TextureImporter)AssetImporter.GetAtPath(WinterPropsBuilder.ArtRoot + "/WinterPalette.png");
            Assert.IsTrue(importer.sRGBTexture); Assert.IsTrue(importer.mipmapEnabled); Assert.IsFalse(importer.isReadable);
            var paint = AssetDatabase.LoadAssetAtPath<Material>(WinterPropsBuilder.ArtRoot + "/Materials/WinterPaint.mat");
            Assert.AreSame(texture, paint.GetTexture("_BaseMap"));
        }

        [Test]
        public void RepeatedBuilderPreservesTheEditablePrefabAndMaterialValues()
        {
            string before = File.ReadAllText(WinterPropsBuilder.PrefabPath);
            string materialPath = WinterPropsBuilder.ArtRoot + "/Materials/WinterPaint.mat";
            string material = File.ReadAllText(materialPath);
            WinterPropsBuilder.Build();
            Assert.AreEqual(before, File.ReadAllText(WinterPropsBuilder.PrefabPath));
            Assert.AreEqual(material, File.ReadAllText(materialPath));
        }
    }
}
