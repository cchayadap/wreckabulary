using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Wreckabulary.Art;
using Wreckabulary.EditorTools;
using Wreckabulary.Rules;

namespace Wreckabulary.EditorTests
{
    [Explicit("Run after WordboundEquipmentImporter.BuildDetailed imports the verified detailed revision.")]
    public sealed class WordboundEquipmentDetailTests
    {
        [Test]
        public void DetailedMapsUseLinearDataAndUrpSmoothnessAlpha()
        {
            var manifest = WordboundEquipmentImporter.ReadVerifiedManifest();
            Assert.AreEqual("DetailV2", manifest.revision);
            Assert.IsNotNull(manifest.detail_maps);
            var paint = LoadMaterial(WordboundEquipmentImporter.PaintName);
            var brass = LoadMaterial(WordboundEquipmentImporter.BrassName);
            Assert.IsNotNull(paint);
            Assert.IsNotNull(brass);
            var color = Importer("WordboundPvPPalette.png");
            var normal = Importer("WordboundPvPNormal.png");
            var mask = Importer("WordboundPvPMask.png");
            Assert.IsTrue(color.sRGBTexture);
            Assert.AreEqual(TextureImporterType.NormalMap, normal.textureType);
            Assert.IsFalse(normal.sRGBTexture || normal.convertToNormalmap || normal.flipGreenChannel);
            Assert.AreEqual(TextureImporterType.Default, mask.textureType);
            Assert.IsFalse(mask.sRGBTexture || mask.alphaIsTransparency);
            Assert.AreEqual(TextureImporterAlphaSource.FromInput, mask.alphaSource);
            Assert.IsTrue(mask.DoesSourceTextureHaveAlpha());
            foreach (var importer in new[] { color, normal, mask })
            {
                Assert.AreEqual(512, importer.maxTextureSize);
                Assert.IsTrue(importer.mipmapEnabled);
                Assert.IsFalse(importer.isReadable);
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(importer.assetPath);
                Assert.AreEqual(512, texture.width);
                Assert.AreEqual(256, texture.height);
            }
            Assert.AreSame(AssetDatabase.LoadAssetAtPath<Texture2D>(color.assetPath), paint.GetTexture("_BaseMap"));
            Assert.AreSame(AssetDatabase.LoadAssetAtPath<Texture2D>(normal.assetPath), paint.GetTexture("_BumpMap"));
            Assert.AreSame(AssetDatabase.LoadAssetAtPath<Texture2D>(mask.assetPath), paint.GetTexture("_MetallicGlossMap"));
            Assert.IsTrue(paint.IsKeywordEnabled("_NORMALMAP"));
            Assert.IsTrue(paint.IsKeywordEnabled("_METALLICSPECGLOSSMAP"));
            Assert.AreEqual(1f, paint.GetFloat("_Smoothness"));
            Assert.AreEqual(0f, paint.GetFloat("_SmoothnessTextureChannel"));
            Assert.IsNull(brass.GetTexture("_BumpMap"));
            Assert.IsNull(brass.GetTexture("_MetallicGlossMap"));
            Assert.IsFalse(brass.IsKeywordEnabled("_NORMALMAP") || brass.IsKeywordEnabled("_METALLICSPECGLOSSMAP"));
        }

        [Test]
        public void DetailedNativeMeshesKeepTheOriginalDimensionsGripAndTwoMaterialBudget()
        {
            var manifest = WordboundEquipmentImporter.ReadVerifiedManifest();
            Assert.AreEqual("DetailV2", manifest.revision);
            var collection = AssetDatabase.LoadAssetAtPath<WordboundEquipmentCollection>(WordboundEquipmentImporter.CollectionPath);
            Assert.IsNotNull(collection);
            Assert.AreEqual(12, collection.Entries.Count);
            var paint = LoadMaterial(WordboundEquipmentImporter.PaintName);
            var brass = LoadMaterial(WordboundEquipmentImporter.BrassName);
            foreach (var entry in collection.Entries)
            {
                var source = manifest.assets.Single(record => record.id == entry.itemId);
                var measured = WordboundEquipmentImporter.ValidateImported(entry, source.triangles, paint, brass);
                Assert.Less(Vector3.Distance(measured.center, entry.boundsCenter), .001f,
                    entry.itemId + " presentation metadata must use the selected revision's actual native vertices");
            }
        }

        [Test]
        public void DetailedNativeRendererCountsMatchGroupedExports()
        {
            var source = ReadDetailedSource();
            var collection = AssetDatabase.LoadAssetAtPath<WordboundEquipmentCollection>(WordboundEquipmentImporter.CollectionPath);
            Assert.IsNotNull(collection);
            Assert.AreEqual(source["assets"].Items.Count, collection.Entries.Count);
            int totalMeshes = 0, totalDraws = 0;
            foreach (var entry in collection.Entries)
            {
                var asset = source["assets"].Items.Single(item => item["id"].String() == entry.itemId);
                Assert.IsNotNull(entry.model, entry.itemId);
                int expectedMeshes = asset["export_mesh_count"].Int();
                int expectedDraws = asset["material_draw_count"].Int();
                var filters = entry.model.GetComponentsInChildren<MeshFilter>(true);
                var renderers = entry.model.GetComponentsInChildren<Renderer>(true);
                Assert.Greater(expectedMeshes, 0, entry.itemId);
                Assert.AreEqual(expectedMeshes, filters.Length, entry.itemId + " mesh count");
                Assert.AreEqual(expectedMeshes, renderers.Length, entry.itemId + " renderer count");
                int draws = renderers.Sum(renderer => renderer.sharedMaterials.Length);
                Assert.AreEqual(expectedDraws, draws, entry.itemId + " material-slot count");
                foreach (var renderer in renderers)
                {
                    Assert.IsInstanceOf<MeshRenderer>(renderer, entry.itemId);
                    var filter = renderer.GetComponent<MeshFilter>();
                    Assert.IsNotNull(filter, entry.itemId);
                    Assert.IsNotNull(filter.sharedMesh, entry.itemId);
                    Assert.AreEqual(1, filter.sharedMesh.subMeshCount, entry.itemId + " grouped submesh count");
                    Assert.AreEqual(1, renderer.sharedMaterials.Length, entry.itemId + " grouped material slots");
                }
                totalMeshes += filters.Length;
                totalDraws += draws;
                TestContext.WriteLine(entry.itemId + ": native meshes=" + filters.Length + ", material slots=" + draws);
            }
            Assert.AreEqual(source["assets"].Items.Sum(item => item["export_mesh_count"].Int()), totalMeshes);
            Assert.AreEqual(source["assets"].Items.Sum(item => item["material_draw_count"].Int()), totalDraws);
            TestContext.WriteLine("Detailed collection: native meshes=" + totalMeshes + ", material slots=" + totalDraws);
        }

        [TestCase("FAN", "FAN_Rotor")]
        [TestCase("CLOCK", "CLOCK_HourHand")]
        [TestCase("CLOCK", "CLOCK_MinuteHand")]
        public void DetailedNativeArticulatedPivotsKeepAuthoredTransformsAndMeshOwnership(string id, string pivotName)
        {
            var source = ReadDetailedSource()["assets"].Items.Single(item => item["id"].String() == id);
            var pivotRecord = source["export_pivots"].Items.Single(item => item["name"].String() == pivotName);
            var expected = BlenderToUnity(pivotRecord["matrix_world"].Floats(16));
            var collection = AssetDatabase.LoadAssetAtPath<WordboundEquipmentCollection>(WordboundEquipmentImporter.CollectionPath);
            Assert.IsNotNull(collection);
            var entry = collection.Find(id);
            Assert.IsNotNull(entry);
            Assert.IsNotNull(entry.model, id);
            var anchor = new GameObject("Detailed pivot verification");
            try
            {
                anchor.transform.SetPositionAndRotation(new Vector3(3, 2, -5), Quaternion.Euler(17, 31, 11));
                anchor.transform.localScale = new Vector3(1.2f, .8f, 1.1f);
                var model = Object.Instantiate(entry.model, anchor.transform, false);
                var pivots = model.GetComponentsInChildren<Transform>(true).Where(transform => transform.name == pivotName).ToArray();
                Assert.AreEqual(1, pivots.Length, pivotName + " must remain a distinct authored pivot");
                var pivot = pivots[0];
                var actual = anchor.transform.worldToLocalMatrix * pivot.localToWorldMatrix;
                TestContext.WriteLine(pivotName + " expected converted world matrix:\n" + expected.ToString("F7") +
                    "native world matrix:\n" + actual.ToString("F7") + "native model matrix:\n" +
                    (anchor.transform.worldToLocalMatrix * model.transform.localToWorldMatrix).ToString("F7"));
                Assert.LessOrEqual(Vector3.Distance(expected.MultiplyPoint3x4(Vector3.zero), actual.MultiplyPoint3x4(Vector3.zero)),
                    .001f, pivotName + " origin in metres");
                for (int axis = 0; axis < 3; axis++)
                {
                    Vector3 expectedAxis = expected.GetColumn(axis), actualAxis = actual.GetColumn(axis);
                    Assert.LessOrEqual(Vector3.Distance(expectedAxis, actualAxis), .001f,
                        pivotName + " basis " + axis + " must preserve orientation, scale and shear");
                }
                Assert.AreEqual(1, pivot.GetComponentsInChildren<MeshFilter>(true).Length,
                    pivotName + " must retain exactly its own grouped mesh");
                Assert.AreEqual(1, pivot.GetComponentsInChildren<MeshRenderer>(true).Length,
                    pivotName + " must not flatten into the static model or absorb another articulated part");
            }
            finally { Object.DestroyImmediate(anchor); }
        }

        static JsonNode ReadDetailedSource()
        {
            Assert.AreEqual("DetailV2", WordboundEquipmentImporter.ReadVerifiedManifest().revision);
            string path = WordboundEquipmentImporter.SourceRoot + "/DetailV2/manifest.json";
            var source = Json.Parse(File.ReadAllText(path), path);
            Assert.AreEqual("DetailV2", source["revision"].String());
            return source;
        }

        static Matrix4x4 BlenderToUnity(float[] rowMajor)
        {
            var blender = new Matrix4x4();
            for (int row = 0; row < 4; row++)
                for (int column = 0; column < 4; column++)
                    blender[row, column] = rowMajor[row * 4 + column];
            var axes = new Matrix4x4(new Vector4(1, 0, 0, 0), new Vector4(0, 0, 1, 0),
                new Vector4(0, 1, 0, 0), new Vector4(0, 0, 0, 1));
            return axes * blender * axes;
        }

        static TextureImporter Importer(string file) =>
            (TextureImporter)AssetImporter.GetAtPath(WordboundEquipmentImporter.ArtRoot + "/" + file);

        static Material LoadMaterial(string name) =>
            AssetDatabase.LoadAssetAtPath<Material>(WordboundEquipmentImporter.ArtRoot + "/Materials/" + name + ".mat");
    }
}
