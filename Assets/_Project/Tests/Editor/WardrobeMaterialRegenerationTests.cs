using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Wreckabulary.EditorTools;

namespace Wreckabulary.EditorTests
{
    public sealed class WardrobeMaterialRegenerationTests
    {
        const string NeutralTexture = "Assets/_Project/Art/Imported/Textures/ivory_vinyl_BaseColor.png";

        [TestCase("boots_color")]
        [TestCase("glasses_frame")]
        [TestCase("satchel_straps")]
        public void RegeneratingTintableAccessoriesKeepsNeutralAlbedoAndOriginalSurface(string name)
        {
            var spec = ImportedArtSettings.ReadMaterials().Single(item => item.Name == name);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            Assert.IsNotNull(shader);
            var neutral = AssetDatabase.LoadAssetAtPath<Texture2D>(NeutralTexture);
            var original = AssetDatabase.LoadAssetAtPath<Texture2D>(spec.BaseMap);
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(spec.NormalMap);
            Assert.IsNotNull(neutral);
            Assert.IsNotNull(original);
            Assert.IsNotNull(normal);
            Assert.AreNotSame(original, neutral);
            var material = new Material(shader);
            try
            {
                for (int pass = 0; pass < 2; pass++)
                {
                    material.SetTexture("_BaseMap", original);
                    material.SetColor("_BaseColor", Color.black);
                    MaterialLibraryBuilder.ApplySpecification(material, spec);
                    Assert.AreSame(neutral, material.GetTexture("_BaseMap"));
                    Assert.AreEqual(Color.white, material.GetColor("_BaseColor"));
                    Assert.AreSame(normal, material.GetTexture("_BumpMap"));
                    Assert.AreEqual(spec.Metallic, material.GetFloat("_Metallic"), .0001f);
                    Assert.AreEqual(1f - spec.Roughness, material.GetFloat("_Smoothness"), .0001f);
                    if (material.HasProperty("_MainTex")) Assert.AreSame(neutral, material.GetTexture("_MainTex"));
                }
            }
            finally { Object.DestroyImmediate(material); }
        }

        [Test]
        public void NeutralAlbedoDoesNotReplaceOtherMaterialsUsingDarkTextures()
        {
            var specs = ImportedArtSettings.ReadMaterials();
            var accessory = specs.Single(item => item.Name == "boots_color");
            var original = AssetDatabase.LoadAssetAtPath<Texture2D>(accessory.BaseMap);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            Assert.IsNotNull(shader);
            Assert.IsNotNull(original);
            var material = new Material(shader);
            try
            {
                accessory.Name = "boots_color_trim";
                MaterialLibraryBuilder.ApplySpecification(material, accessory);
                Assert.AreSame(original, material.GetTexture("_BaseMap"), "Only the three exact wardrobe material names receive a neutral base.");
            }
            finally { Object.DestroyImmediate(material); }
        }
    }
}
