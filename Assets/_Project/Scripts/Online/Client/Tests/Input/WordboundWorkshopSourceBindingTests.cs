using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Wreckabulary.Rules.Wordbound;
using Wreckabulary.Workshop;
using Object = UnityEngine.Object;

namespace Wreckabulary.Online.Tests
{
    [NonParallelizable]
    public sealed class WordboundWorkshopSourceBindingTests
    {
        readonly List<Object> assets = new List<Object>();
        GameObject owner;
        WordboundWorkshop workshop;
        WordboundLauncher launcher;
        WordboundLaunchAssets launch;
        WordboundCourseAsset assigned;
        WorkshopTemplatePreset template;
        string folder, globalItems, globalRules, assignedItems, assignedRules;
        bool oldTyping;

        [SetUp]
        public void SetUp()
        {
            oldTyping = DesktopBinding.Typing;
            try
            {
                DesktopBinding.Typing = false;
                folder = Path.Combine(Path.GetTempPath(), "wordbound-source-" + Guid.NewGuid().ToString("N"));
                owner = new GameObject("Workshop source binding test");
                workshop = owner.AddComponent<WordboundWorkshop>(); workshop.enabled = false;
                launcher = owner.AddComponent<WordboundLauncher>(); launcher.enabled = false;
                launch = Make<WordboundLaunchAssets>(); launcher.Assets = launch;
                launch.CameraProfile = Make<WordboundCameraProfile>(); launch.VisualProfile = Make<WordboundVisualProfile>();
                template = WorkshopTemplates.Create(WorkshopTemplate.FreestyleWorld);
                assigned = Make<WordboundCourseAsset>(); assigned.Initialize(template.Snapshot.Course(), template.Catalogue);
                assigned.Seed = "fedcba9876543210";
                globalItems = File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Data/Config/items.json"));
                globalRules = File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Data/Config/rules.json"));
                assignedItems = globalItems + "\n"; assignedRules = globalRules + "\n";
                assigned.Items = Text(assignedItems); assigned.RuleBook = Text(assignedRules);
                assigned.CameraProfile = Make<WordboundCameraProfile>(); assigned.VisualProfile = Make<WordboundVisualProfile>();
                Set(launcher, "workshop", workshop); Set(launcher, "itemJson", globalItems); Set(launcher, "ruleJson", globalRules);
                Invoke(launcher, "ConfigureWorkshopSources", assigned);
            }
            catch (Exception setupError)
            {
                try { TearDown(); }
                catch (Exception cleanupError) { throw new AggregateException(setupError, cleanupError); }
                throw;
            }
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                if (owner) Object.DestroyImmediate(owner);
                foreach (var asset in assets) if (asset) Object.DestroyImmediate(asset);
                assets.Clear();
                string prefix = Path.Combine(Path.GetTempPath(), "wordbound-source-");
                if (folder != null && Path.GetFullPath(folder).StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && Directory.Exists(folder))
                    Directory.Delete(folder, true);
            }
            finally { DesktopBinding.Typing = oldTyping; }
        }

        [Test]
        public void AssignedAndTemplateRetainIndependentHistorySelectionSeedAndRedo()
        {
            OpenAssigned();
            var authoredHistory = workshop.History;
            authoredHistory.Execute("Assigned edit", draft => { draft.Course.Title = "Assigned edit"; draft.Seed = "1234567890abcdef"; });
            authoredHistory.Select(new[] { authoredHistory.Current.Course().Modules.First().Id });
            string expected = authoredHistory.Current.RecoveryJson;
            workshop.OpenTemplate(WorkshopTemplate.FreestyleWorld);
            var templateHistory = workshop.History;
            Assert.IsFalse(workshop.UsesAssignedCourse); Assert.AreNotSame(authoredHistory, templateHistory);
            templateHistory.Execute("Template edit", draft => draft.Course.Title = "Template edit");
            Assert.IsTrue(templateHistory.Undo()); Assert.IsTrue(templateHistory.CanRedo);
            workshop.OpenAssignedCourse();
            Assert.IsTrue(workshop.UsesAssignedCourse); Assert.AreSame(authoredHistory, workshop.History);
            Assert.AreEqual(expected, workshop.History.Current.RecoveryJson);
            Assert.AreEqual(assigned.Catalogue().Hash, workshop.Catalogue.Hash);
            Assert.IsTrue(workshop.History.Undo()); Assert.IsTrue(workshop.History.Redo());
            Assert.AreEqual(expected, workshop.History.Current.RecoveryJson);
            workshop.OpenTemplate(WorkshopTemplate.FreestyleWorld);
            Assert.AreSame(templateHistory, workshop.History); Assert.IsTrue(workshop.History.Redo());
            Assert.AreEqual("Template edit", workshop.History.Current.Course().Title);
        }

        [Test]
        public void AssignedAndTemplateKeepTheirSelectedModeAndTeamFormat()
        {
            var race = WorkshopTemplates.Create(WorkshopTemplate.RaceCircuit);
            workshop.OpenAuthored(race.Snapshot.Course(), race.Catalogue, race.Play, assigned.Seed, folder);
            workshop.SelectMode("TeamRace", RaceFormat.Squad);
            var authoredPlay = workshop.PlayRequirements;
            workshop.OpenTemplate(WorkshopTemplate.RaceCircuit);
            workshop.SelectMode("TeamRace", RaceFormat.Duo);
            var templatePlay = workshop.PlayRequirements;
            workshop.OpenAssignedCourse(); Assert.AreSame(authoredPlay, workshop.PlayRequirements);
            Assert.AreEqual(4, workshop.PlayRequirements.TeamCapacity);
            workshop.OpenTemplate(WorkshopTemplate.RaceCircuit); Assert.AreSame(templatePlay, workshop.PlayRequirements);
            Assert.AreEqual(2, workshop.PlayRequirements.TeamCapacity);
        }

        [Test]
        public void SameIdTemplateDoesNotRecoverOrOverwriteTheAssignedDraft()
        {
            OpenAssigned();
            workshop.History.Execute("Assigned edit", draft => draft.Course.Title = "Keep assigned");
            workshop.OpenTemplate(WorkshopTemplate.FreestyleWorld);
            string id = assigned.ReadDocument().Id;
            string primary = Path.Combine(folder, id + ".autosave.json");
            byte[] original = File.ReadAllBytes(primary);
            Assert.AreEqual(id, workshop.History.Current.Course().Id);
            Assert.AreEqual(template.Snapshot.Course().Title, workshop.History.Current.Course().Title);
            workshop.History.Execute("Template edit", draft => draft.Course.Title = "Keep template");
            workshop.OpenAssignedCourse();
            CollectionAssert.AreEqual(original, File.ReadAllBytes(primary));
            var templateStorage = new WorkshopStorage(Path.Combine(folder, "templates", WorkshopTemplate.FreestyleWorld.ToString()));
            Assert.AreEqual("Keep template", templateStorage.Recover(id, out _).Course().Title);
            Assert.AreEqual("Keep assigned", workshop.History.Current.Course().Title);
        }

        [Test]
        public void RecoveredAssignedDraftReturnsWithItsNewEditsAndUndo()
        {
            var draft = template.Snapshot.Edit(); draft.Course.Title = "Recovered assigned"; draft.Seed = "1234567890abcdef";
            new WorkshopStorage(folder).Save(new WorkshopSnapshot(draft));
            OpenAssigned();
            Assert.IsTrue(workshop.RecoveryChoicePending);
            Assert.Throws<InvalidOperationException>(() => workshop.OpenAssignedCourse());
            Invoke(workshop, "QueueRecoveryChoice", 1); Invoke(workshop, "ProcessRecoveryChoice", true);
            var recovered = workshop.History;
            recovered.Execute("After recovery", edit => edit.Course.Title = "Edited recovery");
            workshop.OpenTemplate(WorkshopTemplate.FreestyleWorld); workshop.OpenAssignedCourse();
            Assert.AreSame(recovered, workshop.History); Assert.IsTrue(workshop.UsesAssignedCourse);
            Assert.AreEqual(assignedItems, Field<string>(Inputs(), "Items"));
            Assert.IsTrue(workshop.History.Undo()); Assert.AreEqual("Recovered assigned", workshop.History.Current.Course().Title);
            Assert.AreEqual("1234567890abcdef", workshop.History.Current.Seed);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void FailedOutgoingSaveKeepsTheCurrentHistoryAndSource(bool returning)
        {
            OpenAssigned();
            if (returning) workshop.OpenTemplate(WorkshopTemplate.FreestyleWorld);
            var before = workshop.History;
            before.Execute("Unsaved edit", draft => draft.Course.Title = "Must stay in memory");
            var saveFolder = returning ? Path.Combine(folder, "templates", WorkshopTemplate.FreestyleWorld.ToString()) : folder;
            new WorkshopStorage(saveFolder).Save(before.Current);
            string primary = Path.Combine(saveFolder, before.Current.Course().Id + ".autosave.json");
            using (var locked = new FileStream(primary, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                if (returning) Assert.Throws<IOException>(() => workshop.OpenAssignedCourse());
                else Assert.Throws<IOException>(() => workshop.OpenTemplate(WorkshopTemplate.FreestyleWorld));
                Assert.AreSame(before, workshop.History); Assert.AreEqual(!returning, workshop.UsesAssignedCourse);
                Assert.AreEqual("Must stay in memory", workshop.History.Current.Course().Title);
            }
        }

        [TestCase("focus")]
        [TestCase("disable")]
        [TestCase("typing")]
        [TestCase("gameplay-block")]
        public void DeferredSourceChoiceIsCancelledBeforeItCanReplaceTheDraft(string interruption)
        {
            OpenAssigned(); var before = workshop.History;
            Invoke(workshop, "QueueSourceChoice", 2 + (int)WorkshopTemplate.FreestyleWorld);
            Assert.AreSame(before, workshop.History);
            IDisposable lease = null;
            try
            {
                if (interruption == "focus") Invoke(workshop, "OnApplicationFocus", false);
                if (interruption == "disable") Invoke(workshop, "OnDisable");
                if (interruption == "typing") DesktopBinding.Typing = true;
                if (interruption == "gameplay-block") lease = KeyBindings.BlockGameplay();
                Invoke(workshop, "ProcessSourceChoice", true);
            }
            finally { lease?.Dispose(); DesktopBinding.Typing = false; }
            Invoke(workshop, "ProcessSourceChoice", true);
            Assert.AreSame(before, workshop.History); Assert.IsTrue(workshop.UsesAssignedCourse);
            Invoke(workshop, "QueueSourceChoice", 2 + (int)WorkshopTemplate.FreestyleWorld);
            Invoke(workshop, "ProcessSourceChoice", true); Assert.IsFalse(workshop.UsesAssignedCourse);
            Invoke(workshop, "QueueSourceChoice", 1); Assert.IsFalse(workshop.UsesAssignedCourse);
            Invoke(workshop, "ProcessSourceChoice", true); Assert.AreSame(before, workshop.History);
        }

        [Test]
        public void ActualLauncherUsesCapturedAssignedBytesAndProfilesOnlyForTheAssignedSession()
        {
            OpenAssigned();
            var camera = assigned.CameraProfile; var visual = assigned.VisualProfile;
            AssertInputs(assignedItems, assignedRules, camera, visual);
            assigned.Items = Text("changed after opening"); assigned.RuleBook = Text("changed after opening");
            assigned.CameraProfile = Make<WordboundCameraProfile>(); assigned.VisualProfile = Make<WordboundVisualProfile>();
            workshop.OpenTemplate(WorkshopTemplate.FreestyleWorld);
            AssertInputs(globalItems, globalRules, launch.CameraProfile, launch.VisualProfile);
            workshop.OpenAssignedCourse(); AssertInputs(assignedItems, assignedRules, camera, visual);
        }

        [Test]
        public void PendingControllerReturnCannotFireAfterWorkshopTextTakesFocus()
        {
            OpenAssigned(); workshop.OpenTemplate(WorkshopTemplate.FreestyleWorld); var before = workshop.History;
            Invoke(workshop, "QueueControllerSourceChoice"); Set(workshop, "workshopTyping", true);
            Invoke(workshop, "ProcessSourceChoice", true);
            Set(workshop, "workshopTyping", false); Invoke(workshop, "ProcessSourceChoice", true);
            Assert.AreSame(before, workshop.History); Assert.IsFalse(workshop.UsesAssignedCourse);
            Invoke(workshop, "QueueControllerSourceChoice"); Invoke(workshop, "ProcessSourceChoice", true);
            Assert.IsTrue(workshop.UsesAssignedCourse);
        }

        [Test]
        public void LocalAndPrivateOnlineCompositionUseTheExactPreviewForEachSource()
        {
            OpenAssigned(); AssertComposition();
            workshop.OpenTemplate(WorkshopTemplate.FreestyleWorld); AssertComposition();
            workshop.OpenAssignedCourse(); AssertComposition();
        }

        [Test]
        public void AssignedOpeningAlsoOffersTheGlobalFlagshipAndCanReturnWithoutResetting()
        {
            OpenAssigned();
            var history = workshop.History;
            history.Execute("Keep me", draft => draft.Course.Title = "Assigned work");
            var flagship = Field<WordboundFlagshipContent>(workshop, "flagshipContent");
            Assert.IsNotNull(flagship);
            workshop.OpenFlagship(flagship);
            Assert.IsFalse(workshop.UsesAssignedCourse);
            AssertInputs(globalItems, globalRules, launch.CameraProfile, launch.VisualProfile);
            Assert.AreEqual(flagship.Geometry.Hash, workshop.Catalogue.Hash);
            workshop.OpenAssignedCourse();
            Assert.AreSame(history, workshop.History); Assert.AreEqual("Assigned work", workshop.History.Current.Course().Title);
        }

        [Test]
        public void SameIdFlagshipHasSeparateSavesAndRetainsItsHistoryModeAndRedo()
        {
            var flagship = Field<WordboundFlagshipContent>(workshop, "flagshipContent");
            assigned.Initialize(flagship.CopyDocument(), flagship.Geometry); assigned.ModeId = "TimeRun";
            Invoke(launcher, "BindAssignedWorkshopSource", assigned); OpenAssigned();
            var authored = workshop.History;
            authored.Execute("Assigned edit", draft => draft.Course.Title = "Keep assigned flagship");
            Invoke(workshop, "QueueSourceChoice", 5); Assert.AreSame(authored, workshop.History);
            Invoke(workshop, "ProcessSourceChoice", true);
            var global = workshop.History; Assert.AreNotSame(authored, global);
            Assert.AreEqual(assigned.ReadDocument().Id, global.Current.Course().Id);
            string primary = Path.Combine(folder, assigned.ReadDocument().Id + ".autosave.json");
            byte[] original = File.ReadAllBytes(primary);
            global.Execute("Global edit", draft => draft.Course.Title = "Keep global flagship");
            workshop.SelectMode("TeamRace", RaceFormat.Duo); var play = workshop.PlayRequirements;
            Assert.IsTrue(global.Undo());
            workshop.OpenAssignedCourse();
            Assert.AreSame(authored, workshop.History); CollectionAssert.AreEqual(original, File.ReadAllBytes(primary));
            Assert.AreEqual(assignedItems, Field<string>(Inputs(), "Items"));
            workshop.OpenFlagship(flagship);
            Assert.AreSame(global, workshop.History); Assert.AreSame(play, workshop.PlayRequirements);
            Assert.IsTrue(global.Redo()); Assert.AreEqual("Keep global flagship", global.Current.Course().Title);
            AssertInputs(globalItems, globalRules, launch.CameraProfile, launch.VisualProfile);
            workshop.OpenAssignedCourse();
            var scoped = new WorkshopStorage(Path.Combine(folder, "templates", "Flagship"));
            Assert.AreEqual("Keep global flagship", scoped.Recover(assigned.ReadDocument().Id, out _).Course().Title);
            CollectionAssert.AreEqual(original, File.ReadAllBytes(primary));
        }

        [Test]
        public void FlagshipDeferredChoiceIsCancelledOnFocusLoss()
        {
            OpenAssigned(); var before = workshop.History;
            Invoke(workshop, "QueueSourceChoice", 5); Invoke(workshop, "OnApplicationFocus", false);
            Invoke(workshop, "ProcessSourceChoice", true);
            Assert.AreSame(before, workshop.History); Assert.IsTrue(workshop.UsesAssignedCourse);
        }

        [Test]
        public void FailedFlagshipSavePreventsAssignedReturn()
        {
            OpenAssigned(); workshop.OpenFlagship(Field<WordboundFlagshipContent>(workshop, "flagshipContent"));
            var before = workshop.History; before.Execute("Keep unsaved", draft => draft.Course.Title = "Unsaved flagship");
            string path = Path.Combine(folder, "templates", "Flagship");
            new WorkshopStorage(path).Save(before.Current);
            using (var locked = new FileStream(Path.Combine(path, before.Current.Course().Id + ".autosave.json"), FileMode.Open, FileAccess.Read, FileShare.None))
            {
                Assert.Throws<IOException>(() => workshop.OpenAssignedCourse());
                Assert.AreSame(before, workshop.History); Assert.IsFalse(workshop.UsesAssignedCourse);
            }
        }

        [TestCase(1)]
        [TestCase(4)]
        [TestCase(5)]
        public void OnlinePreparationBlocksDirectAndPreviouslyQueuedSourceChanges(int choice)
        {
            OpenAssigned();
            if (choice == 1) workshop.OpenTemplate(WorkshopTemplate.FreestyleWorld);
            var before = workshop.History; bool assignedBefore = workshop.UsesAssignedCourse;
            Invoke(workshop, "QueueSourceChoice", choice);
            typeof(WordboundWorkshop).GetProperty("OnlineStarting").GetSetMethod(true).Invoke(workshop, new object[] { true });
            try
            {
                Assert.Throws<InvalidOperationException>(() => workshop.OpenAssignedCourse());
                Assert.Throws<InvalidOperationException>(() => workshop.OpenTemplate(WorkshopTemplate.FreestyleWorld));
                Assert.Throws<InvalidOperationException>(() => workshop.OpenFlagship(Field<WordboundFlagshipContent>(workshop, "flagshipContent")));
                Invoke(workshop, "ProcessSourceChoice", true);
                Assert.AreSame(before, workshop.History); Assert.AreEqual(assignedBefore, workshop.UsesAssignedCourse);
            }
            finally { workshop.FinishOnlinePreparation(); }
            Invoke(workshop, "ProcessSourceChoice", true); Assert.AreSame(before, workshop.History);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AssignedSourceRejectsMissingCaptureOrMismatchedGeometry(bool differentGeometry)
        {
            OpenAssigned();
            if (!differentGeometry) Set(launcher, "assignedWorkshopInputs", null);
            else
            {
                var original = workshop.Catalogue;
                var changed = new CourseCatalogue(original.Modules.Values.Concat(new[] { new CourseModuleDefinition("Additional", 1000, 1000, 1000) }),
                    original.Assets.Values, original.PuzzlePresets, original.Themes, original.NarrativeEvents);
                typeof(WordboundWorkshop).GetProperty("Catalogue").GetSetMethod(true).Invoke(workshop, new object[] { changed });
            }
            Assert.Throws<InvalidOperationException>(() => Inputs());
        }

        [Test]
        public void MissingRequiredAssignedBytesDoNotFallBackToGlobals()
        {
            assigned.Items = null;
            Assert.Throws<ArgumentException>(() => Invoke(launcher, "BindAssignedWorkshopSource", assigned));
            assigned.Items = Text(assignedItems); assigned.RuleBook = null;
            Assert.Throws<ArgumentException>(() => Invoke(launcher, "BindAssignedWorkshopSource", assigned));
        }

        [Test]
        public void OptionalAssignedProfilesUseTheDefaultProfiles()
        {
            assigned.CameraProfile = null; assigned.VisualProfile = null;
            Invoke(launcher, "BindAssignedWorkshopSource", assigned); OpenAssigned();
            AssertInputs(assignedItems, assignedRules, launch.CameraProfile, launch.VisualProfile);
        }

        [Test]
        public void UnassignedLegacyTemplateRecoveryIsPreservedWhileNewEditsUseItsScopedStorage()
        {
            var legacy = template.Snapshot.Edit(); legacy.Course.Title = "Legacy template";
            var storage = new WorkshopStorage(folder); storage.Save(new WorkshopSnapshot(legacy));
            string path = Path.Combine(folder, legacy.Course.Id + ".autosave.json"); byte[] before = File.ReadAllBytes(path);
            workshop.OpenTemplate(WorkshopTemplate.FreestyleWorld, folder);
            Assert.IsFalse(workshop.HasAssignedCourse); Assert.AreEqual("Legacy template", workshop.History.Current.Course().Title);
            workshop.History.Execute("Scoped edit", draft => draft.Course.Title = "Scoped template");
            workshop.OpenTemplate(WorkshopTemplate.CombatArena);
            CollectionAssert.AreEqual(before, File.ReadAllBytes(path));
            var scoped = new WorkshopStorage(Path.Combine(folder, "templates", WorkshopTemplate.FreestyleWorld.ToString()));
            Assert.AreEqual("Scoped template", scoped.Recover(legacy.Course.Id, out _).Course().Title);
        }

        void OpenAssigned() => workshop.OpenAuthored(assigned.ReadDocument(), assigned.Catalogue(),
            MatchStateValidation.Requirements(ModeDefinition.For(assigned.ModeId, assigned.Format)), assigned.Seed, folder);
        object Inputs() => Invoke(launcher, "InputsForWorkshop");
        void AssertInputs(string items, string rules, WordboundCameraProfile camera, WordboundVisualProfile visual)
        {
            object inputs = Inputs();
            Assert.AreEqual(items, Field<string>(inputs, "Items")); Assert.AreEqual(rules, Field<string>(inputs, "Rules"));
            Assert.AreSame(camera, Field<WordboundCameraProfile>(inputs, "Camera"));
            Assert.AreSame(visual, Field<WordboundVisualProfile>(inputs, "Visual"));
        }
        void AssertComposition()
        {
            var preview = workshop.Preview(); Assert.IsTrue(preview.Success, string.Join("\n", preview.Issues));
            object inputs = Inputs();
            string items = Field<string>(inputs, "Items"), rules = Field<string>(inputs, "Rules");
            var local = WordboundWorkshopPlaytest.Create(workshop.History.Current, preview.Manifest, workshop.Catalogue, workshop.PlayRequirements, items, rules);
            var online = WordboundWorkshopOnlineContent.Create(workshop.History.Current, preview.Manifest, workshop.Catalogue, workshop.PlayRequirements, items, rules);
            Assert.AreEqual(preview.Manifest.Hash, local.Course.Hash); Assert.AreEqual(preview.Manifest.Hash, online.Course.Hash);
            Assert.AreEqual(CourseJson.Sha256(workshop.History.Current.CourseJsonText), online.Course.SourceHash);
            Assert.AreEqual(local.CatalogueHash, online.PhysicalPuzzles.CatalogueHash);
            Assert.AreEqual(WordboundIdentity.Sha256(items), ((WordboundAuthoredPuzzleContent)local).ItemSourceHash);
            Assert.AreEqual(WordboundIdentity.Sha256(rules), ((WordboundAuthoredPuzzleContent)online.PhysicalPuzzles).RuleSourceHash);
            Assert.IsNotNull(online.WorkshopBundle);
        }
        T Make<T>() where T : ScriptableObject { var value = ScriptableObject.CreateInstance<T>(); assets.Add(value); return value; }
        TextAsset Text(string value) { var text = new TextAsset(value); assets.Add(text); return text; }
        static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(target);
        static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        static object Invoke(object target, string name, params object[] args)
        {
            try { return target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args); }
            catch (TargetInvocationException error) { throw error.InnerException ?? error; }
        }
    }
}
