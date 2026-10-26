using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;
using Wreckabulary.UI;
using Object = UnityEngine.Object;

namespace Wreckabulary.Online.Tests
{
    public sealed class WordboundRecipeGridTests
    {
        [Test]
        public void EveryModeRecipeUsesTheExactOriginalResourceAndMissingArtStaysMissing()
        {
            var ids = new[] { "TimeRun", "TeamRace", "BrawlRace", "FreeForAll", "Freestyle" }
                .SelectMany(mode => ModeDefinition.For(mode).Recipes).Distinct(StringComparer.Ordinal);
            foreach (string id in ids)
            {
                var expected = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Resources/UI/Items/" + id + ".png");
                Assert.IsNotNull(expected, id);
                Assert.AreSame(expected, OriginalItemArt.Find(id), id + " must not use a generated collection override.");
                Assert.AreSame(expected, OriginalItemArt.Find(id));
            }
            Assert.IsNull(OriginalItemArt.Find("MISSINGRECIPE"));
            Assert.IsNull(OriginalItemArt.Find("../FAN"));
            Assert.IsNull(OriginalItemArt.Find(null));
        }

        [Test]
        public void GridNavigationCrossesRowsClampsThePartialRowAndReturnsToOrdinaryControls()
        {
            var focus = new WordboundMenuFocus(); focus.Reset("recipe:5"); focus.BeginLayout();
            focus.Register("resume");
            for (int i = 0; i < 13; i++) focus.Register("recipe:" + i);
            focus.Register("craft-selected"); focus.Register("return"); focus.EndLayout();
            Assert.IsTrue(focus.MoveGrid(0, 1, 4)); Assert.AreEqual("recipe:9", focus.Selected);
            focus.MoveGrid(0, 1, 4); Assert.AreEqual("recipe:12", focus.Selected);
            focus.MoveGrid(0, 1, 4); Assert.AreEqual("craft-selected", focus.Selected);
            Assert.IsFalse(focus.MoveGrid(0, 1, 4)); focus.Move(1); Assert.AreEqual("return", focus.Selected);
            focus.Move(1); Assert.AreEqual("resume", focus.Selected);
            focus.Move(1); Assert.AreEqual("recipe:0", focus.Selected);
            focus.MoveGrid(1, 0, 4); Assert.AreEqual("recipe:1", focus.Selected);
            focus.MoveGrid(0, -1, 4); Assert.AreEqual("resume", focus.Selected);
            focus.Confirm(); Assert.IsTrue(focus.Consume("resume")); Assert.IsFalse(focus.Consume("resume"));
            Assert.IsFalse(focus.MoveGrid(0, 0, 4));
        }

        [Test]
        public void DirectionMetadataPreservesExistingStepRepeatAndLegacyCommands()
        {
            var previous = Gamepad.current; Gamepad pad = null; WordboundInputBinding binding = null;
            bool typing = DesktopBinding.Typing;
            using var input = new WordboundNativeInputScope();
            try
            {
                DesktopBinding.Typing = false;
                pad = InputSystem.AddDevice<Gamepad>(); binding = new WordboundInputBinding();
                typeof(WordboundInputBinding).GetField("selected", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(binding, pad);
                WordboundNativeInputScope.Sample(pad, new GamepadState().WithButton(GamepadButton.DpadDown));
                var first = binding.ReadMenu(1); Assert.AreEqual(1, first.Step); Assert.AreEqual(new Vector2Int(0, 1), first.Direction);
                var held = binding.ReadMenu(1.1f); Assert.AreEqual(0, held.Step); Assert.AreEqual(Vector2Int.zero, held.Direction);
                Assert.AreEqual(1, binding.ReadMenu(1.36f).Step);
                WordboundNativeInputScope.Sample(pad, new GamepadState().WithButton(GamepadButton.DpadRight));
                var right = binding.ReadMenu(1.37f); Assert.AreEqual(1, right.Step); Assert.AreEqual(new Vector2Int(1, 0), right.Direction);
                Assert.AreEqual(0, binding.ReadMenu(1.4f).Step);
                WordboundNativeInputScope.Sample(pad, new GamepadState().WithButton(GamepadButton.DpadUp));
                var up = binding.ReadMenu(1.5f); Assert.AreEqual(-1, up.Step); Assert.AreEqual(new Vector2Int(0, -1), up.Direction);
                WordboundNativeInputScope.Sample(pad, new GamepadState().WithButton(GamepadButton.DpadLeft));
                var left = binding.ReadMenu(1.51f); Assert.AreEqual(-1, left.Step); Assert.AreEqual(new Vector2Int(-1, 0), left.Direction);
                var legacy = new WordboundMenuCommands(-1, true, false, .5f);
                Assert.AreEqual(-1, legacy.Step); Assert.IsTrue(legacy.Confirm); Assert.AreEqual(.5f, legacy.Scroll);
                Assert.AreEqual(Vector2Int.zero, legacy.Direction);
            }
            finally
            {
                binding?.Dispose();
                if (pad != null && pad.added) InputSystem.RemoveDevice(pad);
                if (previous != null && previous.added) previous.MakeCurrent();
                DesktopBinding.Typing = typing;
            }
        }

        [Test]
        public void SelectionFilterAndLetterChangesKeepTheExistingModeRecipeCache()
        {
            using var f = new WordboundRecipeGridFixture();
            var before = (List<RecipeCard>)f.Get("recipeCards");
            CollectionAssert.AreEquivalent(ModeDefinition.For("FreeForAll").Recipes, before.Select(c => c.Item.Id));
            f.Call("SelectRecipe", "STOOL"); Assert.AreEqual("STOOL", f.Get("selectedRecipe"));
            f.Call("RefreshRecipes"); Assert.AreSame(before, f.Get("recipeCards"));
            f.Set("search", "bat"); f.Call("NormalizeRecipeSelection"); Assert.AreEqual("BAT", f.Get("selectedRecipe"));
            Assert.AreEqual("BAT · Need A", f.Call("RecipeDetail", f.Call("SelectedRecipe")));
            f.Inventory.Letters.Add('A'); f.Call("RefreshRecipes");
            Assert.AreNotSame(before, f.Get("recipeCards")); Assert.AreEqual("BAT", f.Call("RecipeDetail", f.Call("SelectedRecipe")));
            f.Set("search", "NO MATCH"); f.Call("NormalizeRecipeSelection"); Assert.IsNull(f.Get("selectedRecipe"));
            Assert.AreEqual("Choose a recipe", f.Call("RecipeDetail", f.Call("SelectedRecipe")));
            f.Set("search", ""); f.Call("NormalizeRecipeSelection"); Assert.AreEqual("BAT", f.Get("selectedRecipe"));
        }

        [Test]
        public void SelectionIsDeferredAndCannotCraftOrResumeAnUnavailableSession()
        {
            using var f = new WordboundRecipeGridFixture();
            string before = (string)f.Get("selectedRecipe");
            Action select = () => f.Call("SelectRecipe", "STOOL");
            f.Call("QueueMenuAction", select); Assert.AreEqual(before, f.Get("selectedRecipe"));
            f.Call("ExecuteMenuAction", false); f.Call("ExecuteMenuAction", true); Assert.AreEqual(before, f.Get("selectedRecipe"));
            f.Call("QueueMenuAction", select); f.Call("ExecuteMenuAction", true); Assert.AreEqual("STOOL", f.Get("selectedRecipe"));
            Assert.IsTrue(f.View.MenuOpen); Assert.AreEqual("", f.Get("notice"));
            f.Inventory.Letters.Add(LetterBag.FromWord("STOOL")); f.Call("RefreshRecipes");
            Assert.IsFalse((bool)f.Call("CanCraftRecipe", f.Call("SelectedRecipe")));
            f.Call("QueueMenuAction", new Action(() => f.Call("CraftSelectedRecipe")));
            f.Call("ExecuteMenuAction", true); f.Call("ExecuteMenuAction", true);
            Assert.IsTrue(f.View.MenuOpen); Assert.AreEqual("", f.Get("notice"));
        }
    }

    internal sealed class WordboundRecipeGridFixture : IDisposable
    {
        internal readonly GameObject Root;
        internal readonly WordboundMatchHud Hud;
        internal readonly WordboundPlayerView View;
        internal readonly PlayerInventory Inventory = new PlayerInventory();
        internal readonly WordboundUiTheme Theme;

        internal WordboundRecipeGridFixture(int bodySize = 14)
        {
            Root = new GameObject("Original recipe grid native fixture");
            var controller = Root.AddComponent<WordboundGameplayController>(); controller.enabled = false;
            View = Root.AddComponent<WordboundPlayerView>(); View.Initialize(controller, Root.AddComponent<Camera>()); View.enabled = false; View.SetMenuOpen(true);
            Hud = Root.AddComponent<WordboundMatchHud>(); Hud.enabled = false;
            Theme = ScriptableObject.CreateInstance<WordboundUiTheme>(); Theme.BodySize = bodySize; Theme.RowHeight = Math.Max(34, bodySize + 12); Theme.ModuleHeight = Math.Max(56, bodySize * 2 + 16); Theme.Validate(); Hud.Theme = Theme;
            var catalogue = new ItemCatalogue();
            foreach (string id in ModeDefinition.For("FreeForAll").Recipes) catalogue.Add(new ItemDefinition { Id = id, Enabled = true });
            catalogue.Add(new ItemDefinition { Id = "APPLE", Enabled = true });
            Inventory.Letters.Add(LetterBag.FromWord("BT"));
            typeof(PlayerInventory).GetProperty(nameof(PlayerInventory.Slots)).SetValue(Inventory, new[] { -1, -1 });
            Set("controller", controller); Set("view", View); Set("inventory", Inventory); Set("items", catalogue);
            Set("mode", ModeDefinition.For("FreeForAll")); Set("actor", new MatchActorState("grid-player", 100, 100));
            Call("RefreshRecipes");
        }

        internal static FieldInfo Field(string name) => typeof(WordboundMatchHud).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        internal object Get(string name) => Field(name).GetValue(Hud);
        internal void Set(string name, object value) => Field(name).SetValue(Hud, value);
        internal object Call(string name, params object[] args)
        {
            try { return typeof(WordboundMatchHud).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(Hud, args); }
            catch (TargetInvocationException error) { throw error.InnerException ?? error; }
        }
        public void Dispose()
        {
            ((WordboundUiStyles)Get("ui")).Dispose();
            Object.DestroyImmediate(Root); Object.DestroyImmediate(Theme);
        }
    }
}
