using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Wreckabulary.Art;
using Wreckabulary.EditorTools;
using Wreckabulary.Rules;

namespace Wreckabulary.EditorTests
{
    [TestFixture]
    public class GameDataTests
    {
        GameConfig config;
        ModelLibrary models;
        MaterialLibrary materials;

        static readonly string[] CoreTwelve = { "BAT", "BLADE", "LAMP", "BALL", "PLATE", "TABLE", "BED", "MAT", "SOFA", "SOAP", "FOAM", "BOMB" };

        [OneTimeSetUp]
        public void Load()
        {
            GameConfig.Use(null);
            config = GameConfig.Current;
            models = AssetDatabase.LoadAssetAtPath<ModelLibrary>(ImportedArtSettings.ModelLibraryAssetPath);
            materials = AssetDatabase.LoadAssetAtPath<MaterialLibrary>(ImportedArtSettings.LibraryAssetPath);
            Assert.IsNotNull(models, "run Wreckabulary → Art → Set Up Imported Art");
            Assert.IsNotNull(materials, "run Wreckabulary → Art → Set Up Imported Art");
        }

        [OneTimeTearDown]
        public void Forget() => GameConfig.Use(null);

        [Test]
        public void TheGameDataAssetPointsAtTheConfigFiles()
        {
            var data = Resources.Load<GameData>(GameData.ResourcePath);
            Assert.IsNotNull(data);
            Assert.AreEqual(GameDataSetup.ConfigFolder + GameDataSetup.RulesFile, AssetDatabase.GetAssetPath(data.rules));
            Assert.AreEqual(GameDataSetup.ConfigFolder + GameDataSetup.ItemsFile, AssetDatabase.GetAssetPath(data.items));
            Assert.AreEqual(GameDataSetup.ConfigFolder + GameDataSetup.HouseFile, AssetDatabase.GetAssetPath(data.house));
            Assert.AreEqual(GameDataSetup.ConfigFolder + GameDataSetup.WardrobeFile, AssetDatabase.GetAssetPath(data.wardrobe));
        }

        [Test]
        public void TheShippedDataHasNoProblems()
        {
            Assert.IsEmpty(config.Validate());
            CollectionAssert.AreEquivalent(CoreTwelve.Concat(new[] { "APPLE", "WATER", "CAKE", "SODA", "SHIELD", "FAN", "CLOCK", "BROOM", "HAMMER", "SPEAR", "PIE", "STOOL" }), config.Items.Enabled.Select(i => i.Id), "only completed recipes are craftable");
            Assert.AreEqual(100f, config.Rules.Defaults.MaxHealth);
            Assert.AreEqual(10, config.Rules.Defaults.MaxLetters);
            Assert.AreEqual(2, config.Rules.Defaults.MaxCarried);
            Assert.AreEqual(2, config.Rules.Defaults.MaxDeployed);
            Assert.AreSame(config.Rules.Defaults, config.RulesFor("NoSuchMode"), "an unknown mode plays by the defaults");
        }

        [Test]
        public void ABadEditIsReportedWhenTheDataLoads()
        {
            var data = Resources.Load<GameData>(GameData.ResourcePath);
            string items = data.items.text.Replace("\"heldScale\": 0.9809", "\"heldScale\": 3");
            Assume.That(items, Is.Not.EqualTo(data.items.text), "BALL's held scale is 0.9809 in items.json");
            var e = Assert.Throws<System.InvalidOperationException>(() =>
                GameConfig.FromJson(data.rules.text, items, data.house.text, data.wardrobe.text));
            StringAssert.Contains("BALL: held scale 3 is outside (0, 1]", e.Message);
        }

        [Test]
        public void EveryModelledItemIsInTheModelLibrary()
        {
            var missing = config.Items.All.Where(i => !string.IsNullOrEmpty(i.Model) && models.Find(i.Model) == null).Select(i => $"{i.Id} ({i.Model})").ToList();
            Assert.IsEmpty(missing, string.Join(", ", missing));
            foreach (var item in config.Items.Enabled)
            {
                Assert.IsNotNull(models.Find(item.Model), item.Id);
                Assert.IsTrue(Wreckabulary.UI.ItemArt.TryGet(item.Id, out var picture, out var uv), item.Id + " has recipe art");
                Assert.IsNotNull(picture);
                Assert.Greater(uv.width * uv.height, 0f);
            }
        }

        [Test]
        public void ItemGripsAndSizesMatchTheImportedModels()
        {
            var wrong = new List<string>();
            foreach (var item in config.Items.All.Where(i => !string.IsNullOrEmpty(i.Model)))
            {
                var parent = new GameObject("test parent");
                try
                {
                    var copy = models.Spawn(item.Model, parent.transform);
                    var grip = FindChild(copy.transform, "Grip_R");
                    var expectedGrip = new Vector3(item.Grip[0], item.Grip[1], item.Grip[2]);
                    if (grip == null) wrong.Add($"{item.Id}: no Grip_R");
                    else if (Vector3.Distance(grip.position, expectedGrip) > 0.01f)
                        wrong.Add($"{item.Id}: Grip_R at {grip.position.ToString("F3")}, items.json says {expectedGrip.ToString("F3")}");

                    var renderers = copy.GetComponentsInChildren<Renderer>(true);
                    var bounds = renderers[0].bounds;
                    foreach (var r in renderers.Skip(1)) bounds.Encapsulate(r.bounds);
                    var expectedSize = new Vector3(item.Size[0], item.Size[1], item.Size[2]);
                    var diff = bounds.size - expectedSize;
                    if (Mathf.Max(Mathf.Abs(diff.x), Mathf.Abs(diff.y), Mathf.Abs(diff.z)) > 0.02f)
                        wrong.Add($"{item.Id}: size {bounds.size.ToString("F3")}, items.json says {expectedSize.ToString("F3")}");
                }
                finally
                {
                    Object.DestroyImmediate(parent);
                }
            }
            Assert.IsEmpty(wrong, string.Join("\n", wrong));
        }

        [Test]
        public void EveryListedSkinHasItsMaterials()
        {
            var wrong = new List<string>();
            foreach (var item in config.Items.All.Where(i => !string.IsNullOrEmpty(i.Model)))
            {
                var model = models.Find(item.Model);
                var used = model.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Distinct().ToList();
                var skinnable = used.Where(m => MaterialLibrary.FamilyOf(m.name) != null).ToList();
                if (item.Skins.Any(s => s != Skin.Standard) && skinnable.Count == 0)
                    wrong.Add($"{item.Id}: lists skins but none of its materials has any");
                foreach (string skin in item.Skins)
                    foreach (var m in skinnable)
                    {
                        string expected = MaterialLibrary.FamilyOf(m.name) + "_" + skin;
                        if (materials.ForSkin(m, skin).name != expected) wrong.Add($"{item.Id}: {skin} needs material {expected}");
                    }
            }
            Assert.IsEmpty(wrong, string.Join("\n", wrong.Take(20)));
        }

        [Test]
        public void EveryPieceOfHouseFurnitureHasAModel()
        {
            foreach (var house in config.Houses)
                foreach (var f in house.Value.Furniture)
                {
                    var item = config.Items.Get(f.Word);
                    Assert.IsNotNull(models.Find(item.Model), $"{f.Word} in {house.Key} {f.Room}");
                }
        }

        [Test]
        public void EveryHouseFileIsAPlayableMap()
        {
            var files = System.IO.Directory.GetFiles(GameDataSetup.ConfigFolder, "house_*.json")
                .Select(path => System.IO.Path.GetFileNameWithoutExtension(path).Substring("house_".Length));
            CollectionAssert.AreEquivalent(files, config.Houses.Keys);
        }

        [Test]
        public void WardrobePiecesAreOnTheAvatar()
        {
            var avatar = models.Find("Avatar/Avatar");
            Assert.IsNotNull(avatar);
            var renderers = avatar.GetComponentsInChildren<Renderer>(true);
            foreach (var piece in config.Wardrobe.Pieces)
            {
                var r = renderers.FirstOrDefault(x => x.name == piece.Mesh);
                Assert.IsNotNull(r, $"{piece.Id}: Avatar.fbx has no mesh {piece.Mesh}");
                if (piece.TintMaterial == null) continue;
                Assert.IsNotNull(materials.Find(piece.TintMaterial), $"{piece.Id}: no library material {piece.TintMaterial}");
                Assert.IsTrue(r.sharedMaterials.Any(m => m != null && m.name == piece.TintMaterial), $"{piece.Id}: {piece.Mesh} doesn't use {piece.TintMaterial}");
            }
        }

        [Test]
        public void SpawnedModelsKeepTheirImportRotation()
        {
            var parent = new GameObject("test parent");
            try
            {
                parent.transform.SetPositionAndRotation(new Vector3(3, 0, 2), Quaternion.Euler(0, 90, 0));
                var bat = models.Spawn("Items/BAT", parent.transform);
                var bounds = bat.GetComponentInChildren<Renderer>().bounds;
                Assert.Greater(bounds.size.y, 0.9f, "the bat stands upright when its parent is turned");
                Assert.AreEqual(0f, bounds.min.y, 0.01f, "and rests on the parent's floor");
            }
            finally
            {
                Object.DestroyImmediate(parent);
            }
        }

        static Transform FindChild(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform child in root)
            {
                var found = FindChild(child, name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
