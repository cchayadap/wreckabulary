using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Online.Tests
{
    public sealed class WordboundRecipeCacheTests
    {
        [Test]
        public void EqualLetterSnapshotsReuseCardsAndLetterChangesRefreshCraftability()
        {
            var root = new GameObject("Wordbound recipe cache native test");
            try
            {
                var hud = root.AddComponent<WordboundMatchHud>();
                var catalogue = new ItemCatalogue();
                catalogue.Add(new ItemDefinition { Id = "BAT", Enabled = true });
                catalogue.Add(new ItemDefinition { Id = "STOOL", Enabled = true });
                var inventory = new PlayerInventory(); inventory.Letters.Add(LetterBag.FromWord("BT"));
                Field("items").SetValue(hud, catalogue);
                Field("mode").SetValue(hud, ModeDefinition.For("Freestyle"));
                Field("inventory").SetValue(hud, inventory);
                Refresh(hud);
                var before = (List<RecipeCard>)Field("recipeCards").GetValue(hud);
                Assert.AreEqual("A", before.Single(c => c.Item.Id == "BAT").Missing.ToString());
                var equal = new PlayerInventory(); equal.Letters.Add(inventory.Letters);
                Field("inventory").SetValue(hud, equal);
                for (int i = 0; i < 10; i++) Refresh(hud);
                Assert.AreSame(before, Field("recipeCards").GetValue(hud), "Unchanged letters must not rebuild recipe sorting and missing-letter bags.");
                equal.Letters.Add('A'); Refresh(hud);
                var after = (List<RecipeCard>)Field("recipeCards").GetValue(hud);
                Assert.AreNotSame(before, after);
                Assert.IsTrue(after.Single(c => c.Item.Id == "BAT").Craftable);
                Assert.IsFalse(after.Single(c => c.Item.Id == "STOOL").Craftable);
            }
            finally { Object.DestroyImmediate(root); }
        }

        static FieldInfo Field(string name) => typeof(WordboundMatchHud).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        static void Refresh(WordboundMatchHud hud) => typeof(WordboundMatchHud)
            .GetMethod("RefreshRecipes", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(hud, null);
    }
}
