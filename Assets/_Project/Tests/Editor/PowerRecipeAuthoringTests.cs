using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Wreckabulary.EditorTools;
using Wreckabulary.Rules;

namespace Wreckabulary.EditorTests
{
    public sealed class PowerRecipeAuthoringTests
    {
        [TestCase("pinwheel"), TestCase("courtyard"), TestCase("flat"), TestCase("terrace"), TestCase("walkup")]
        public void SavedWorldSuppliesEveryEnabledRecipe(string map)
        {
            var root = Resources.Load<GameObject>(AuthoredHouse.ResourcePath(map));
            var world = root.GetComponent<AuthoredHouse>();
            var words = world.FurnitureRoot.GetComponentsInChildren<Smashable>(true).Select(prop => prop.Word);
            var available = LetterBag.FromWord(string.Concat(words));
            foreach (var item in GameConfig.Current.Items.Enabled)
                Assert.IsTrue(available.Contains(item.Letters), map + " must supply " + item.Id + " in its saved furniture.");
        }

        [TestCase("pinwheel"), TestCase("courtyard"), TestCase("walkup")]
        public void RepeatSupplyMigrationPreservesArtistPoseAndDoesNotDuplicate(string map)
        {
            string path = "Assets/_Project/Resources/" + AuthoredHouse.ResourcePath(map) + ".prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var world = root.GetComponent<AuthoredHouse>();
                var supply = world.FurnitureRoot.Find(PowerRecipeAuthoring.SupplyName);
                Assert.IsNotNull(supply, "Run Add Power Recipe Supplies first.");
                int count = world.FurnitureRoot.childCount;
                var artistPosition = supply.localPosition + new Vector3(.1f, .02f, -.1f);
                supply.localPosition = artistPosition;
                Assert.IsFalse(PowerRecipeAuthoring.AddMissingSupply(world));
                Assert.AreEqual(count, world.FurnitureRoot.childCount);
                Assert.AreEqual(artistPosition, supply.localPosition);
                foreach (var renderer in supply.GetComponentsInChildren<Renderer>(true))
                    foreach (var material in renderer.sharedMaterials) Assert.IsTrue(EditorUtility.IsPersistent(material));
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
