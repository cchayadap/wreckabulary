using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Wreckabulary.Art;

namespace Wreckabulary.EditorTests
{
    public class ItemLookTests
    {
        GameObject anchor;

        [SetUp]
        public void Make() => anchor = new GameObject("Item look test") { hideFlags = HideFlags.HideAndDontSave };

        [TearDown]
        public void Clear()
        {
            TactileMaterials.Release(anchor);
            Object.DestroyImmediate(anchor);
        }

        static string[] Names(GameObject model) =>
            model.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Where(m => m).Select(m => m.name).Distinct().ToArray();

        [Test]
        public void TheItemLookIsVividInClassicAndGivesOtherFinishesBackTheirOwnParts()
        {
            var library = MaterialLibrary.Load();
            Assert.IsNotNull(library);
            Assert.IsNotNull(ItemLook.Palette, "Resources/Art/item_palette.json");
            Assert.IsNotNull(library.Find("toy_yellow_Classic"), "run FinishArt.Materials after finish_materials.py");
            var vivid = ModelVisual.SpawnItem("Items/LAMP", anchor.transform);
            Assert.IsNotNull(vivid.GetComponent<ItemLook>());
            CollectionAssert.IsSubsetOf(new[] { "toy_yellow_Classic", "toy_teal_Classic", "gold_Classic" }, Names(vivid));
            CollectionAssert.DoesNotContain(Names(vivid), "cream_Classic");
            library.ApplySkin(vivid, "Candy");
            CollectionAssert.IsSubsetOf(new[] { "cream_Candy", "ceramic_Candy" }, Names(vivid));
            Assert.IsFalse(Names(vivid).Any(n => n.StartsWith("toy_")), "Candy keeps today's finish exactly");
            library.ApplySkin(vivid, MaterialLibrary.StandardSkin);
            CollectionAssert.Contains(Names(vivid), "toy_yellow_Classic");
        }

        [Test]
        public void SharedSpawnsForFurnitureCoursesAndOnlineKeepTheSharedLook()
        {
            var shared = ModelVisual.Spawn("Items/TABLE", anchor.transform);
            Assert.IsNull(shared.GetComponent<ItemLook>());
            CollectionAssert.Contains(Names(shared), "red_Classic");
            Assert.IsFalse(Names(shared).Any(n => n.StartsWith("toy_")));
            var item = ModelVisual.SpawnItem("Items/TABLE", anchor.transform);
            CollectionAssert.Contains(Names(item), "toy_sky_Classic");
            CollectionAssert.Contains(Names(shared), "red_Classic", "a vivid item copy never repaints the shared material");
        }

        [Test]
        public void KeysOutsideItemsAreLeftAlone()
        {
            Assert.IsNull(ItemLook.ItemOf("Environment/Platform_2m"));
            Assert.AreEqual("LAMP", ItemLook.ItemOf("Items/LAMP"));
            Assert.IsFalse(ItemLook.Covers("NOT_AN_ITEM"));
            Assert.IsNull(ItemLook.Attach(anchor, "NOT_AN_ITEM"));
        }
    }
}
