using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Wreckabulary.Rules.Wordbound;
using Wreckabulary.Workshop;

namespace Wreckabulary.EditorTests
{
    public sealed class WordboundWorkshopStepTests
    {
        const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic, Static = BindingFlags.Static | BindingFlags.NonPublic;
        string folder;
        GameObject owner, second;
        WordboundWorkshop workshop;

        [SetUp]
        public void SetUp()
        {
            folder = Path.Combine(Path.GetTempPath(), "wordbound-workshop-steps-" + Guid.NewGuid().ToString("N"));
            owner = new GameObject("Wordbound workshop step test");
            workshop = owner.AddComponent<WordboundWorkshop>();
        }

        [TearDown]
        public void TearDown()
        {
            if (second) UnityEngine.Object.DestroyImmediate(second);
            UnityEngine.Object.DestroyImmediate(owner);
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }

        [Test]
        public void OpeningAMapStartsOnBuildWithEveryAdvancedSectionClosed()
        {
            workshop.OpenTemplate(WorkshopTemplate.RaceCircuit, folder);
            Assert.AreEqual("Build", Field("step").GetValue(workshop).ToString());
            foreach (string name in new[] { "advanced", "fillAdvanced", "rulesAdvanced", "shareAdvanced", "inspectorAdvancedIds",
                "libraryOpen", "filesOpen", "savingAs", "sharing", "titleEditing" })
                Assert.IsFalse((bool)Field(name).GetValue(workshop), name);
            Assert.IsNull(Field("pendingImport").GetValue(workshop));
        }

        [Test]
        public void StepsAndListsUsePlainNames()
        {
            CollectionAssert.AreEqual(new[] { "Build", "Fill", "Rules", "Test & Share" }, StaticField<string[]>("StepTabs"));
            CollectionAssert.AreEqual(new[] { "Start points", "Checkpoints", "Paths", "Laps", "Safe-return points" }, StaticField<string[]>("RouteTabs"));
            CollectionAssert.AreEqual(new[] { "One object", "Scatter area", "Random mix" }, StaticField<string[]>("PlacementKinds"));
            var labels = StaticField<string[]>("StepTabs").Concat(StaticField<string[]>("StepHints")).Concat(StaticField<string[]>("RouteTabs"))
                .Concat(StaticField<string[]>("PlacementKinds")).Concat((string[])Field("controllerActions").GetValue(workshop));
            foreach (string label in labels)
                foreach (string jargon in new[] { "Module", "Spawn", "Recovery", "Zone", "Physical", "Inspector", "Preview" })
                    StringAssert.DoesNotContain(jargon, label);
        }

        [Test]
        public void FillTabReturnsToObjectsOrPuzzlesWhicheverWasUsedLast()
        {
            workshop.OpenTemplate(WorkshopTemplate.RaceCircuit, folder);
            Assert.AreEqual("Objects", Field("lastFill").GetValue(workshop).ToString());
            Show("Puzzles"); Show("Rules");
            Assert.AreEqual("Puzzles", Field("lastFill").GetValue(workshop).ToString());
            var tab = typeof(WordboundWorkshop).GetMethod("StepTab", Static);
            var steps = Field("step").FieldType;
            CollectionAssert.AreEqual(new[] { 0, 1, 1, 2, 3 }, new[] { "Build", "Objects", "Puzzles", "Rules", "Share" }
                .Select(name => (int)tab.Invoke(null, new[] { Enum.Parse(steps, name) })).ToArray());
        }

        [Test]
        public void EachStepShowsOneHintUntilARoomIsSelected()
        {
            workshop.OpenTemplate(WorkshopTemplate.RaceCircuit, folder);
            workshop.History.Select(Array.Empty<string>()); Call("Refresh");
            Assert.AreEqual("Pick a room to add, or click a room in the map.", Call("SelectionHint"));
            Show("Objects"); Assert.AreEqual("Select a room to add objects.", Call("SelectionHint"));
            Show("Puzzles"); Assert.AreEqual("Select a room to add puzzles.", Call("SelectionHint"));
            Show("Rules"); Assert.AreEqual("", Call("SelectionHint"));
            Show("Share"); Assert.AreEqual("", Call("SelectionHint"));
            workshop.History.Select(new[] { "Start" }); Call("Refresh");
            foreach (string step in new[] { "Build", "Objects", "Puzzles" }) { Show(step); Assert.AreEqual("", Call("SelectionHint"), step); }
        }

        [TestCase(1280, 720)]
        [TestCase(1920, 1080)]
        public void EveryStepFitsItsPanelAndLeavesMostOfTheScreenToTheMap(int width, int height)
        {
            float side = (float)typeof(WordboundWorkshop).GetMethod("StepPanelWidth", Static).Invoke(null, new object[] { (float)width });
            Assert.GreaterOrEqual(side, 300); Assert.LessOrEqual(side, 460);
            Assert.GreaterOrEqual(width - side - 24, width * .6f);
            var theme = ScriptableObject.CreateInstance<WordboundUiTheme>();
            StepWindow window = null;
            try
            {
                theme.BodyFont = Resources.Load<Font>("Fonts/Roboto/v3.016/Roboto-Regular");
                Assert.IsNotNull(theme.BodyFont);
                workshop.Theme = theme;
                workshop.OpenTemplate(WorkshopTemplate.RaceCircuit, folder);
                workshop.History.Select(new[] { "Start" }); Call("Refresh");
                Field("inspectorWidth").SetValue(workshop, side);
                string before = workshop.History.Current.RecoveryJson;
                int revision = workshop.History.Revision;
                var styles = (WordboundUiStyles)Field("ui").GetValue(workshop);
                Rect drawn = default;
                window = ScriptableObject.CreateInstance<StepWindow>();
                window.Draw = () => {
                    using var scope = styles.Apply(theme);
                    Call("Styles");
                    GUILayout.BeginArea(new Rect(0, 0, side, height - 160), styles.Panel);
                    Call("StepPanel");
                    GUILayout.EndArea();
                    if (Event.current.type == EventType.Repaint) drawn = (Rect)Field("inspectorScrollRect").GetValue(workshop);
                };
                window.position = new Rect(40, 40, side + 40, height - 120); window.ShowUtility();
                foreach (string step in new[] { "Build", "Objects", "Puzzles", "Rules", "Share" })
                {
                    Show(step); drawn = default;
                    for (int frame = 0; frame < 2; frame++)
                    {
                        int repaints = window.Repaints;
                        window.SendEvent(new Event { type = EventType.Layout });
                        window.SendEvent(new Event { type = EventType.Repaint });
                        Assert.IsNull(window.Failure, step); Assert.Greater(window.Repaints, repaints, step);
                    }
                    Assert.AreEqual(step, Field("step").GetValue(workshop).ToString());
                    Assert.Greater(drawn.width, 200, step);
                    Assert.LessOrEqual(drawn.xMax, side + 1, step);
                }
                Assert.AreEqual(before, workshop.History.Current.RecoveryJson);
                Assert.AreEqual(revision, workshop.History.Revision);
            }
            finally { if (window) window.Close(); workshop.Theme = null; UnityEngine.Object.DestroyImmediate(theme); }
        }

        [Test]
        public void MapNameRenamesOnCommitAsOneUndoableEditAndRejectsBlankNames()
        {
            workshop.OpenTemplate(WorkshopTemplate.FreestyleWorld, folder);
            string original = workshop.History.Current.Course().Title;
            int revision = workshop.History.Revision;
            Field("titleEditing").SetValue(workshop, true); Field("title").SetValue(workshop, "  Garden party  ");
            Call("CommitTitle");
            Assert.AreEqual("Garden party", workshop.History.Current.Course().Title);
            Assert.AreEqual(revision + 1, workshop.History.Revision);
            Assert.IsFalse((bool)Field("titleEditing").GetValue(workshop));
            Field("title").SetValue(workshop, "   "); Call("CommitTitle");
            Field("title").SetValue(workshop, "Garden party"); Call("CommitTitle");
            Assert.AreEqual("Garden party", workshop.History.Current.Course().Title);
            Assert.AreEqual("Garden party", Field("title").GetValue(workshop));
            Assert.AreEqual(revision + 1, workshop.History.Revision);
            Assert.IsTrue(workshop.History.Undo());
            Assert.AreEqual(original, workshop.History.Current.Course().Title);
        }

        [Test]
        public void ModeToggleAddsAndRemovesModesAsUndoableEditsButKeepsOne()
        {
            workshop.OpenTemplate(WorkshopTemplate.RaceCircuit, folder);
            Assert.AreEqual("TimeRun", workshop.PlayRequirements.ModeId);
            int revision = workshop.History.Revision;
            Call("SetModeSupported", "Freestyle", true);
            CollectionAssert.Contains(workshop.History.Current.Course().SupportedModes, "Freestyle");
            Assert.AreEqual(revision + 1, workshop.History.Revision);
            Assert.AreEqual("TimeRun", workshop.PlayRequirements.ModeId);
            Call("SetModeSupported", "TimeRun", false);
            CollectionAssert.DoesNotContain(workshop.History.Current.Course().SupportedModes, "TimeRun");
            Assert.AreNotEqual("TimeRun", workshop.PlayRequirements.ModeId);
            CollectionAssert.Contains(workshop.History.Current.Course().SupportedModes, workshop.PlayRequirements.ModeId);
            Assert.IsTrue(workshop.History.Undo());
            CollectionAssert.Contains(workshop.History.Current.Course().SupportedModes, "TimeRun");
            foreach (string mode in workshop.History.Current.Course().SupportedModes.Where(mode => mode != "TimeRun").ToArray())
                Call("SetModeSupported", mode, false);
            CollectionAssert.AreEqual(new[] { "TimeRun" }, workshop.History.Current.Course().SupportedModes);
            Assert.AreEqual("TimeRun", workshop.PlayRequirements.ModeId);
            int kept = workshop.History.Revision;
            var error = Assert.Throws<TargetInvocationException>(() => Call("SetModeSupported", "TimeRun", false));
            Assert.IsInstanceOf<InvalidOperationException>(error.InnerException);
            Assert.AreEqual(kept, workshop.History.Revision);
        }

        [Test]
        public void ChosenModeAndTeamSizeAreRememberedOutsideTheMapSave()
        {
            workshop.OpenTemplate(WorkshopTemplate.RaceCircuit, folder);
            string before = workshop.History.Current.RecoveryJson;
            workshop.SelectMode("TeamRace", RaceFormat.Duo);
            Assert.AreEqual(before, workshop.History.Current.RecoveryJson);
            var other = Second();
            other.OpenTemplate(WorkshopTemplate.RaceCircuit, folder);
            Assert.AreEqual("TeamRace", other.PlayRequirements.ModeId);
            Assert.AreEqual(RaceFormat.Duo, WorkshopModes.FormatOf(other.PlayRequirements));
            Assert.IsFalse(other.History.CanUndo);
            Assert.AreEqual(before, other.History.Current.RecoveryJson);
        }

        [Test]
        public void LauncherModeAlwaysWinsOverARememberedMode()
        {
            var race = WorkshopTemplates.Create(WorkshopTemplate.RaceCircuit);
            string id = race.Snapshot.Course().Id;
            new WorkshopStorage(folder).SaveSettings(id, "TeamRace", RaceFormat.Duo);
            new WorkshopStorage(Path.Combine(folder, "templates", "RaceCircuit")).SaveSettings(id, "BrawlRace", RaceFormat.Squad);
            var given = MatchStateValidation.Requirements(ModeDefinition.For("TimeRun"));
            workshop.OpenAuthored(race.Snapshot.Course(), race.Catalogue, given, race.Snapshot.Seed, folder);
            Assert.AreSame(given, workshop.PlayRequirements);
            workshop.SelectMode("TeamRace", RaceFormat.Squad);
            workshop.OpenTemplate(WorkshopTemplate.RaceCircuit);
            Assert.AreEqual("BrawlRace", workshop.PlayRequirements.ModeId);
            Assert.AreEqual(RaceFormat.Squad, WorkshopModes.FormatOf(workshop.PlayRequirements));

            var other = Second();
            other.OpenAuthored(race.Snapshot.Course(), race.Catalogue, given, race.Snapshot.Seed, folder);
            Assert.AreSame(given, other.PlayRequirements);
            other.Open(race.Snapshot.Course(), race.Catalogue, given, folder);
            Assert.AreSame(given, other.PlayRequirements);
            Assert.IsTrue(new WorkshopStorage(folder).TryLoadSettings(id, out string mode, out RaceFormat format));
            Assert.AreEqual("TeamRace", mode); Assert.AreEqual(RaceFormat.Duo, format);
        }

        [Test]
        public void SaveAsNewMapKeepsTheOriginalAndBothReopenFromMyMaps()
        {
            workshop.OpenTemplate(WorkshopTemplate.FreestyleWorld, folder);
            string originalId = workshop.History.Current.Course().Id;
            workshop.History.Execute("Rename", draft => draft.Course.Title = "First map");
            Field("saveAsName").SetValue(workshop, "Second map");
            Call("SaveAsNewMap");
            Assert.AreEqual("Second map", workshop.History.Current.Course().Title);
            Assert.AreEqual("second-map", workshop.History.Current.Course().Id);
            Assert.IsFalse(workshop.History.CanUndo);
            Assert.AreEqual("Saved ✓", Property<string>(workshop, "SaveState"));
            var maps = WorkshopStorage.Library(folder);
            var first = maps.Single(entry => entry.CourseId == originalId);
            Assert.AreEqual("First map", first.Title);
            Assert.AreEqual("templates/FreestyleWorld", first.Source);
            Assert.IsTrue(maps.Any(entry => entry.CourseId == "second-map" && entry.Title == "Second map"));

            var other = Second();
            other.OpenTemplate(WorkshopTemplate.FreestyleWorld, folder);
            Assert.AreEqual("Second map", other.History.Current.Course().Title, "A template reopens the map last worked on.");
            typeof(WordboundWorkshop).GetMethod("OpenSavedMap", Instance).Invoke(other, new object[] { first });
            Assert.AreEqual(originalId, other.History.Current.Course().Id);
            Assert.AreEqual("First map", other.History.Current.Course().Title);
            Assert.IsFalse(other.History.CanUndo);
        }

        [Test]
        public void ImportWaitsForConfirmationAndKeepsTheCurrentMapInMyMaps()
        {
            workshop.OpenTemplate(WorkshopTemplate.FreestyleWorld, folder);
            workshop.History.Execute("Rename", draft => draft.Course.Title = "Mine");
            string mine = workshop.History.Current.Course().Id;
            var friend = workshop.History.Current.Edit();
            friend.Course.Id = "friend-map"; friend.Course.Title = "Friend's map";
            string code = WorkshopShare.Export(new WorkshopSnapshot(friend), workshop.Catalogue, workshop.PlayRequirements);
            var before = workshop.History.Current;
            var parsed = Call("ParseImport", code);
            Assert.IsNotNull(parsed);
            Assert.AreSame(before, workshop.History.Current);
            Assert.IsFalse(WorkshopStorage.Library(folder).Any(entry => entry.CourseId == mine && entry.Title == "Mine"));
            Call("ApplyImport", parsed);
            Assert.AreEqual("Friend's map", workshop.History.Current.Course().Title);
            Assert.AreEqual("friend-map", workshop.History.Current.Course().Id);
            Assert.IsTrue(WorkshopStorage.Library(folder).Any(entry => entry.CourseId == mine && entry.Title == "Mine"));
            Assert.IsTrue(workshop.History.Undo());
            Assert.AreEqual("Mine", workshop.History.Current.Course().Title);
        }

        [Test]
        public void UnreadableTemplateSaveOffersBackOrAFreshStartAndKeepsTheDamagedFile()
        {
            workshop.OpenTemplate(WorkshopTemplate.FreestyleWorld, folder);
            workshop.History.Execute("Rename", draft => draft.Course.Title = "Keep me");
            var previous = workshop.History;
            var preset = WorkshopTemplates.Create(WorkshopTemplate.RaceCircuit).Snapshot.Course();
            string templateFolder = Path.Combine(folder, "templates", "RaceCircuit");
            Directory.CreateDirectory(templateFolder);
            string damaged = Path.Combine(templateFolder, WorkshopStorage.FileStem(preset.Id) + WorkshopStorage.AutosaveExtension);
            File.WriteAllText(damaged, "interrupted write");

            workshop.OpenTemplate(WorkshopTemplate.RaceCircuit, folder);
            Assert.IsTrue(workshop.RecoveryChoicePending);
            Call("QueueRecoveryChoice", 3); Call("ProcessRecoveryChoice", true);
            Assert.IsFalse(workshop.RecoveryChoicePending);
            Assert.AreSame(previous, workshop.History);
            Assert.AreEqual("Keep me", workshop.History.Current.Course().Title);
            Assert.AreEqual("interrupted write", File.ReadAllText(damaged));

            workshop.OpenTemplate(WorkshopTemplate.RaceCircuit, folder);
            Assert.IsTrue(workshop.RecoveryChoicePending);
            Call("QueueRecoveryChoice", 2); Call("ProcessRecoveryChoice", true);
            Assert.IsFalse(workshop.RecoveryChoicePending);
            Assert.AreEqual(preset.Title, workshop.History.Current.Course().Title);
            Assert.IsFalse(File.Exists(damaged));
            var kept = Directory.GetFiles(templateFolder).Single(path => Path.GetFileName(path).StartsWith(Path.GetFileName(damaged) + ".unreadable-", StringComparison.Ordinal));
            Assert.AreEqual("interrupted write", File.ReadAllText(kept));
            workshop.History.Execute("Rename", draft => draft.Course.Title = "Fresh start");
            Assert.IsTrue((bool)Call("SaveNow"));
            Assert.AreEqual("Fresh start", new WorkshopStorage(templateFolder).Recover(preset.Id, out _).Course().Title);
            Assert.AreEqual("interrupted write", File.ReadAllText(kept));
        }

        [Test]
        public void UntouchedMapsAreSavedBeforeSaveAsOrOpeningAnotherMap()
        {
            var preset = WorkshopTemplates.Create(WorkshopTemplate.FreestyleWorld).Snapshot;
            string original = preset.Course().Id;
            var scoped = new WorkshopStorage(Path.Combine(folder, "templates", "FreestyleWorld"));
            var otherDraft = preset.Edit(); otherDraft.Course.Id = "other-map"; otherDraft.Course.Title = "Other map";
            scoped.Save(new WorkshopSnapshot(otherDraft));
            workshop.OpenTemplate(WorkshopTemplate.FreestyleWorld, folder);
            Assert.AreEqual(original, workshop.History.Current.Course().Id);
            Assert.IsFalse(scoped.HasRecovery(original));
            Call("OpenSavedMap", WorkshopStorage.Library(folder).Single(entry => entry.CourseId == "other-map"));
            Assert.AreEqual("Other map", workshop.History.Current.Course().Title);
            Assert.IsTrue(scoped.HasRecovery(original));

            var arena = WorkshopTemplates.Create(WorkshopTemplate.CombatArena).Snapshot.Course();
            var arenaStorage = new WorkshopStorage(Path.Combine(folder, "templates", "CombatArena"));
            workshop.OpenTemplate(WorkshopTemplate.CombatArena, folder);
            Assert.IsFalse(arenaStorage.HasRecovery(arena.Id));
            Field("saveAsName").SetValue(workshop, "Arena copy");
            Call("SaveAsNewMap");
            Assert.AreEqual("Arena copy", workshop.History.Current.Course().Title);
            Assert.IsTrue(arenaStorage.HasRecovery(arena.Id));
            var other = Second();
            other.OpenTemplate(WorkshopTemplate.CombatArena, folder);
            Assert.AreEqual("Arena copy", other.History.Current.Course().Title);
            typeof(WordboundWorkshop).GetMethod("OpenSavedMap", Instance).Invoke(other,
                new object[] { WorkshopStorage.Library(folder).Single(entry => entry.CourseId == arena.Id) });
            Assert.AreEqual(arena.Title, other.History.Current.Course().Title);
        }

        [Test]
        public void ControllerIsIgnoredBehindEveryDialog()
        {
            workshop.OpenTemplate(WorkshopTemplate.RaceCircuit, folder);
            var allowed = typeof(WordboundWorkshop).GetMethod("ControllerAllowed", Instance);
            Assert.IsTrue((bool)allowed.Invoke(workshop, new object[] { true }));
            Assert.IsFalse((bool)allowed.Invoke(workshop, new object[] { false }));
            foreach (string dialog in new[] { "sharing", "libraryOpen", "filesOpen" })
            {
                Field(dialog).SetValue(workshop, true);
                Assert.IsFalse((bool)allowed.Invoke(workshop, new object[] { true }), dialog);
                Field("controllerTools").SetValue(workshop, true);
                Call("ControllerInput", true);
                Assert.IsFalse((bool)Field("controllerTools").GetValue(workshop), dialog);
                Field(dialog).SetValue(workshop, false);
            }
            string code = WorkshopShare.Export(workshop.History.Current, workshop.Catalogue, workshop.PlayRequirements);
            var parsed = Call("ParseImport", code);
            Call("SetPendingImport", parsed);
            Assert.AreEqual(workshop.History.Current.Course().Title, Field("pendingImportTitle").GetValue(workshop));
            Assert.IsFalse((bool)allowed.Invoke(workshop, new object[] { true }), "pending import");
            Call("SetPendingImport", new object[] { null });
            Assert.IsTrue((bool)allowed.Invoke(workshop, new object[] { true }));
        }

        [Test]
        public void TypedNameIsKeptWhenShareOrExitIsPressedWhileTyping()
        {
            workshop.OpenTemplate(WorkshopTemplate.RaceCircuit, folder);
            string id = workshop.History.Current.Course().Id;
            Field("titleEditing").SetValue(workshop, true); Field("title").SetValue(workshop, "Typed name");
            Call("OpenShare");
            Assert.AreEqual("Typed name", workshop.History.Current.Course().Title);
            StringAssert.Contains("Typed name", (string)Field("shareText").GetValue(workshop));
            Field("sharing").SetValue(workshop, false);
            Field("titleEditing").SetValue(workshop, true); Field("title").SetValue(workshop, "Closing name");
            workshop.RequestClose();
            Assert.AreEqual("Closing name", new WorkshopStorage(Path.Combine(folder, "templates", "RaceCircuit")).Recover(id, out _).Course().Title);
        }

        [Test]
        public void DamagedLastMapAsksFirstAndABackupReopenIsAnnounced()
        {
            var preset = WorkshopTemplates.Create(WorkshopTemplate.FreestyleWorld).Snapshot;
            string templateFolder = Path.Combine(folder, "templates", "FreestyleWorld");
            var scoped = new WorkshopStorage(templateFolder);
            var garden = preset.Edit(); garden.Course.Id = "garden"; garden.Course.Title = "Garden";
            scoped.Save(new WorkshopSnapshot(garden));
            garden.Course.Title = "Garden two";
            scoped.Save(new WorkshopSnapshot(garden));
            scoped.RememberLastMap("garden");
            string primary = Path.Combine(templateFolder, "garden" + WorkshopStorage.AutosaveExtension);
            File.WriteAllText(primary, "interrupted write");

            workshop.OpenTemplate(WorkshopTemplate.FreestyleWorld, folder);
            Assert.IsFalse(workshop.RecoveryChoicePending);
            Assert.AreEqual("Garden", workshop.History.Current.Course().Title);
            StringAssert.Contains("previous save", (string)Field("status").GetValue(workshop));

            File.Delete(primary + ".bak");
            var other = Second();
            other.OpenTemplate(WorkshopTemplate.FreestyleWorld, folder);
            Assert.IsTrue(other.RecoveryChoicePending);
            Assert.AreEqual("garden", typeof(WordboundWorkshop).GetField("authoredLabel", Instance).GetValue(other));
            Assert.AreEqual("interrupted write", File.ReadAllText(primary));
            typeof(WordboundWorkshop).GetMethod("QueueRecoveryChoice", Instance).Invoke(other, new object[] { 2 });
            typeof(WordboundWorkshop).GetMethod("ProcessRecoveryChoice", Instance).Invoke(other, new object[] { true });
            Assert.IsFalse(other.RecoveryChoicePending);
            Assert.AreEqual(preset.Course().Id, other.History.Current.Course().Id);
            Assert.IsFalse(File.Exists(primary));
            Assert.AreEqual("interrupted write", File.ReadAllText(Directory.GetFiles(templateFolder)
                .Single(path => Path.GetFileName(path).StartsWith("garden" + WorkshopStorage.AutosaveExtension + ".unreadable-", StringComparison.Ordinal))));
        }

        [Test]
        public void UndoThatRemovesTheChosenModeSwitchesToASupportedMode()
        {
            workshop.OpenTemplate(WorkshopTemplate.RaceCircuit, folder);
            Call("SetModeSupported", "Freestyle", true);
            workshop.SelectMode("Freestyle");
            Call("StepHistory", false);
            CollectionAssert.DoesNotContain(workshop.History.Current.Course().SupportedModes, "Freestyle");
            Assert.AreEqual("TimeRun", workshop.PlayRequirements.ModeId);
            Call("StepHistory", true);
            CollectionAssert.Contains(workshop.History.Current.Course().SupportedModes, "Freestyle");
            Assert.AreEqual("TimeRun", workshop.PlayRequirements.ModeId);
        }

        [Test]
        public void PuzzlePartRecipePickersOfferOnlyRecipesTheCheckAccepts()
        {
            var proof = CourseTemplates.ProofCatalogue();
            var catalogue = new CourseCatalogue(proof.Modules.Values,
                proof.Assets.Values.Concat(new[] { new CourseAssetDefinition("GOLF", "GOLF", 400, 400, 400) }),
                proof.PuzzlePresets, new[] { "Cozy" }, new[] { "FindAddress", "WrongDelivery", "RestoreHome" });
            Assert.IsFalse(CourseValidator.SiteRecipeAllowed("GOLF"));
            workshop.Open(CourseTemplates.RaceCircuit(), catalogue, MatchStateValidation.Requirements(ModeDefinition.For("TimeRun")), folder);
            var choices = ((System.Collections.Generic.IEnumerable<string>)Call("SiteRecipeChoices")).ToArray();
            CollectionAssert.IsNotEmpty(choices);
            CollectionAssert.DoesNotContain(choices, "GOLF");
            CollectionAssert.AreEquivalent(catalogue.Assets.Keys.Where(CourseValidator.SiteRecipeAllowed), choices);
        }

        WordboundWorkshop Second()
        {
            second = new GameObject("Second workshop");
            return second.AddComponent<WordboundWorkshop>();
        }

        static FieldInfo Field(string name) => typeof(WordboundWorkshop).GetField(name, Instance) ?? throw new MissingFieldException(name);
        static T StaticField<T>(string name) => (T)typeof(WordboundWorkshop).GetField(name, Static).GetValue(null);
        static T Property<T>(object target, string name) => (T)typeof(WordboundWorkshop).GetProperty(name, Instance).GetValue(target);
        object Call(string name, params object[] args) => typeof(WordboundWorkshop).GetMethod(name, Instance).Invoke(workshop, args);
        void Show(string step) => Call("ShowStep", Enum.Parse(Field("step").FieldType, step));

        sealed class StepWindow : EditorWindow
        {
            internal Action Draw;
            internal Exception Failure;
            internal int Repaints;
            void OnGUI()
            {
                if (Event.current.type != EventType.Layout && Event.current.type != EventType.Repaint) return;
                var skin = GUI.skin; var colour = GUI.color; var content = GUI.contentColor;
                var background = GUI.backgroundColor; var matrix = GUI.matrix; bool enabled = GUI.enabled;
                try { if (Event.current.type == EventType.Repaint) Repaints++; Draw?.Invoke(); }
                catch (Exception error) { Failure = error; }
                finally { GUI.skin = skin; GUI.color = colour; GUI.contentColor = content;
                    GUI.backgroundColor = background; GUI.matrix = matrix; GUI.enabled = enabled; }
            }
        }
    }
}
