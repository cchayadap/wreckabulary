using System;
using System.Collections.Generic;
using System.IO;
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

        [Test]
        public void MenuSuggestsNextActualPuzzleWithoutCraftingOrOverridingManualChoice()
        {
            using var f = new WordboundRecipeContextFixture("TimeRun");
            string letters = f.Grid.Inventory.Letters.ToString();
            f.Grid.Call("MenuRequested");
            Assert.AreEqual("BED", f.Grid.Get("selectedRecipe"), "The first checkpoint only needs a keepsake; show the next authored recipe.");
            Assert.AreEqual(letters, f.Grid.Inventory.Letters.ToString());
            Assert.IsNull(f.Grid.Inventory.Craft);
            f.Grid.Call("SelectRecipe", "FAN");
            f.Grid.Call("MenuRequested");
            f.Grid.Call("RefreshRecipes");
            Assert.AreEqual("FAN", f.Grid.Get("selectedRecipe"), "Reopening the same objective keeps an explicit choice.");
            f.Grid.View.SetMenuOpen(false);
            f.Grid.Call("ReadCommands", new PlayerCommands { spellDown = true });
            Assert.IsTrue(f.Grid.View.MenuOpen);
            Assert.AreEqual("recipe:FAN", ((WordboundMenuFocus)f.Grid.Get("menuFocus")).Selected);
            Assert.IsNull(f.Grid.Inventory.Craft, "Opening Craft must never submit a craft.");
        }

        [Test]
        public void NextObjectiveChangesSuggestedRecipeOnlyWhenTheMenuIsOpened()
        {
            using var f = new WordboundRecipeContextFixture("TimeRun");
            f.Grid.Call("PrepareRecipeMenu");
            Assert.AreEqual("BED", f.Grid.Get("selectedRecipe"));
            f.AdvanceCheckpoint();
            f.AdvanceCheckpoint();
            Assert.AreEqual("BED", f.Grid.Get("selectedRecipe"), "Read-model changes do not move an open menu selection.");
            f.Grid.Call("PrepareRecipeMenu");
            Assert.AreEqual("TABLE", f.Grid.Get("selectedRecipe"));
            Assert.AreEqual(2, f.Progress.Team(0).NextCheckpoint);
        }

        [Test]
        public void SearchAndUnknownOrStaleContextDoNotResetAChosenRecipe()
        {
            using var f = new WordboundRecipeContextFixture("TimeRun");
            f.Grid.Set("search", "BAT");
            f.Grid.Call("PrepareRecipeMenu");
            Assert.AreEqual("BAT", f.Grid.Get("selectedRecipe"));
            Assert.IsNull(f.Grid.Get("recipeContext"));
            f.Grid.Set("search", ""); f.Grid.Call("SelectRecipe", "FAN");
            f.SetFrame(stale: true); f.Grid.Call("PrepareRecipeMenu");
            Assert.AreEqual("FAN", f.Grid.Get("selectedRecipe"));
            f.SetFrame(); f.Grid.Set("actorId", "outsider"); f.Grid.Call("PrepareRecipeMenu");
            Assert.AreEqual("FAN", f.Grid.Get("selectedRecipe"));
            f.Grid.Set("actorId", WordboundRecipeContextFixture.Player);
            f.Grid.Set("mode", ModeDefinition.For("FreeForAll")); f.Grid.Call("PrepareRecipeMenu");
            Assert.AreEqual("FAN", f.Grid.Get("selectedRecipe"));
        }

        [Test]
        public void DisconnectedOrMigratingPlayersReceiveNoNewRecipeSuggestion()
        {
            using var f = new WordboundRecipeContextFixture("TimeRun");
            f.Grid.Call("SelectRecipe", "FAN");
            f.Progress.Disconnect(WordboundRecipeContextFixture.Player, 1); f.SetFrame();
            f.Grid.Call("PrepareRecipeMenu"); Assert.AreEqual("FAN", f.Grid.Get("selectedRecipe"));
            Assert.IsTrue(f.Progress.Reconnect(WordboundRecipeContextFixture.Player, 2));
            f.Progress.BeginMigration(); f.SetFrame();
            f.Grid.Call("PrepareRecipeMenu"); Assert.AreEqual("FAN", f.Grid.Get("selectedRecipe"));
        }

        [Test]
        public void FreestyleUsesActiveStationThenNearbyUnfinishedStationWithoutRaceProgress()
        {
            using var f = new WordboundRecipeContextFixture("Freestyle");
            var sorting = f.Course.PhysicalSites.First(s => s.PuzzleId == "SortingBench");
            var clock = f.Course.PhysicalSites.Single(s => s.PuzzleId == "ReturnClock");
            f.SetFrame(selected: clock, position: sorting.Position);
            f.Grid.Call("PrepareRecipeMenu"); Assert.AreEqual("CLOCK", f.Grid.Get("selectedRecipe"));
            f.SetFrame(selected: clock, position: sorting.Position, completed: clock);
            f.Grid.Call("PrepareRecipeMenu"); Assert.AreEqual("TABLE", f.Grid.Get("selectedRecipe"));
            Assert.AreEqual(0, f.Progress.Team(0).NextCheckpoint);
            Assert.IsNull(f.Progress.Result);
        }

        [Test]
        public void FlagshipSuggestsTheSmashingToolBeforeTheSocketRecipeAndDoesNotSuggestDuplicateTools()
        {
            using var f = new WordboundRecipeContextFixture("TimeRun", flagship: true);
            f.SetFrame(sources: true);
            f.Grid.Call("PrepareRecipeMenu"); Assert.AreEqual("HAMMER", f.Grid.Get("selectedRecipe"));
            f.Grid.Call("SelectRecipe", "FAN"); f.Grid.Call("PrepareRecipeMenu");
            Assert.AreEqual("FAN", f.Grid.Get("selectedRecipe"));
            f.SetFrame(sources: true, heldTool: true);
            f.Grid.Call("PrepareRecipeMenu"); Assert.AreEqual("TABLE", f.Grid.Get("selectedRecipe"));
            f.Grid.Set("recipeContext", null); f.Grid.Call("SelectRecipe", "FAN");
            f.SetFrame(sources: false);
            f.Grid.Call("PrepareRecipeMenu"); Assert.AreEqual("TABLE", f.Grid.Get("selectedRecipe"));
            f.Grid.Set("recipeContext", null); f.Grid.Call("SelectRecipe", "FAN");
            f.Grid.Inventory.Letters.Add(LetterBag.FromWord("TABLE")); f.SetFrame(sources: true);
            f.Grid.Call("RefreshRecipes"); f.Grid.Call("PrepareRecipeMenu");
            Assert.AreEqual("HAMMER", f.Grid.Get("selectedRecipe"), "Recovery letters do not remove the marked-source smashing prerequisite.");
        }

        [Test]
        public void RivalTeamProgressCannotChangeTheLocalTeamsSuggestedRecipe()
        {
            using var f = new WordboundRecipeContextFixture("TeamRace", flagship: true);
            f.SetFrame(sources: true); f.Grid.Call("PrepareRecipeMenu");
            Assert.AreEqual("HAMMER", f.Grid.Get("selectedRecipe"));
            f.AdvanceCheckpoint("recipe-rival"); f.AdvanceCheckpoint("recipe-rival");
            f.SetFrame(sources: true); f.Grid.Call("PrepareRecipeMenu");
            Assert.AreEqual("HAMMER", f.Grid.Get("selectedRecipe"));
            Assert.AreEqual(0, f.Progress.Team(0).NextCheckpoint);
            f.AdvanceCheckpoint(); f.AdvanceCheckpoint(); f.SetFrame(sources: true);
            f.Grid.Call("PrepareRecipeMenu"); Assert.AreEqual("TABLE", f.Grid.Get("selectedRecipe"));
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

    internal sealed class WordboundRecipeContextFixture : IDisposable
    {
        internal const string Player = "recipe-player";
        internal readonly WordboundRecipeGridFixture Grid = new WordboundRecipeGridFixture();
        internal readonly ResolvedCourse Course;
        internal readonly MatchProgress Progress;
        readonly ItemCatalogue items;
        readonly ModeDefinition mode;
        readonly WordboundSeat[] seats;

        internal WordboundRecipeContextFixture(string modeId, bool flagship = false)
        {
            items = ItemCatalogue.FromJson(File.ReadAllText("Assets/_Project/Data/Config/items.json"));
            mode = ModeDefinition.For(modeId);
            seats = modeId == "TeamRace" ? new[] { new WordboundSeat(Player, 0, 0), new WordboundSeat("recipe-rival", 1, 0) } :
                new[] { new WordboundSeat(Player, 0, 0) };
            var result = flagship ? WordboundFlagshipContent.Create(items).Resolve("0123456789abcdef", MatchStateValidation.Requirements(mode)) :
                WordboundParcelServiceContent.Create(items).Resolve("0123456789abcdef", MatchStateValidation.Requirements(mode));
            Assert.IsTrue(result.Success, string.Join("\n", result.Issues)); Course = result.Manifest;
            Progress = new MatchProgress(mode, seats, MatchStateValidation.Route(Course));
            Grid.Set("items", items); Grid.Set("course", Course); Grid.Set("mode", mode);
            Grid.Set("actorId", Player); Grid.Set("recipeLetters", null); Grid.Call("RefreshRecipes");
            SetFrame();
        }

        internal void SetFrame(bool stale = false, ResolvedCoursePhysicalSite selected = null,
            CoursePoint? position = null, ResolvedCoursePhysicalSite completed = null, bool sources = false, bool heldTool = false)
        {
            var binding = new MatchBinding("recipe-context", MatchSnapshotCodec.ProtocolVersion, mode.Hash,
                stale ? new string('c', 64) : Course.SourceHash, Course.Hash, new string('a', 64), new string('b', 64), seats);
            var at = position ?? Course.Spawns[0].Position;
            var bodies = new List<WorldBodyState> { new WorldBodyState("player:0", WorldBodyKind.Player,
                new WorldVector(at.X / 1000f, at.Y / 1000f, at.Z / 1000f), sourceId: Player) };
            if (sources)
                bodies.AddRange(Course.Placements.Select((placement, index) => new WorldBodyState("source:" + index, WorldBodyKind.Item,
                    new WorldVector(placement.Position.X / 1000f, placement.Position.Y / 1000f, placement.Position.Z / 1000f), sourceId: placement.Id)));
            var economy = new Economy(items, new GameRules());
            if (heldTool)
            {
                economy.AddPlayer(0, 0);
                foreach (var tile in economy.MintTiles("HAMMER").Tiles) Assert.IsTrue(economy.CollectTile(0, tile.TileId).Ok);
                Assert.IsTrue(economy.BeginCraft(0, "HAMMER", 0).Ok); Assert.IsTrue(economy.CompleteCraft(0, 100).Ok);
            }
            Grid.Set("frame", new MatchSnapshot(binding, 0, 1, 1, Progress.Capture(),
                economy.Capture(), new CommandJournal(binding.Hash, seats.Select(s => s.PlayerId)).Capture(),
                Array.Empty<MatchActorState>(), bodies,
                Array.Empty<MatchResourceState>(), completed == null ? Array.Empty<MatchMechanismState>() :
                    new[] { new MatchMechanismState(completed.PuzzleId, 0, completed.Lap - 1, true) },
                Array.Empty<MatchRandomState>(), Array.Empty<string>(), practice: selected == null ? null : new MatchPracticeState(selected.Hash, 1)));
        }

        internal void AdvanceCheckpoint(string player = Player)
        {
            var team = Progress.Team(Progress.Player(player).Team);
            var checkpoint = Course.Laps[team.Lap].Checkpoints[team.NextCheckpoint];
            var events = checkpoint.PuzzleIds.Select(id => new ProgressEvent(ProgressEventKind.PuzzleSatisfied, player, id, team.Lap)).ToList();
            events.Add(new ProgressEvent(ProgressEventKind.CheckpointReached, player, checkpoint.Id, team.Lap));
            Progress.Advance(Progress.Tick + 1, (Progress.Tick + 1) * 17, events);
            SetFrame();
        }

        public void Dispose() => Grid.Dispose();
    }
}
