using System;
using System.IO;
using System.Reflection;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Wreckabulary.Rules.Wordbound;
using Wreckabulary.Workshop;

namespace Wreckabulary.EditorTests
{
    public sealed class WordboundWorkshopTests
    {
        string folder;
        GameObject owner;
        WordboundWorkshop workshop;

        [SetUp]
        public void SetUp()
        {
            folder = Path.Combine(Path.GetTempPath(), "wordbound-workshop-" + Guid.NewGuid().ToString("N"));
            owner = new GameObject("Wordbound workshop test");
            workshop = owner.AddComponent<WordboundWorkshop>();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(owner);
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }

        [Test]
        public void ObjectPickerUsesOriginalArtWithoutAnInjectedProviderAndPreservesOverrides()
        {
            var resolve = typeof(WordboundWorkshop).GetMethod("ObjectIcon", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(resolve);
            var original = Resources.Load<Texture2D>("UI/Items/STOOL");
            Assert.IsNotNull(original, "The original object artwork must be present in the real project.");
            Assert.AreSame(original, resolve.Invoke(workshop, new object[] { "STOOL" }));
            Assert.IsNull(resolve.Invoke(workshop, new object[] { "UNKNOWNOBJECT" }));
            var custom = new Texture2D(8, 8);
            try
            {
                workshop.AssetIcon = id => id == "STOOL" ? custom : null;
                Assert.AreSame(custom, resolve.Invoke(workshop, new object[] { "STOOL" }));
                workshop.AssetIcon = id => null;
                Assert.AreSame(original, resolve.Invoke(workshop, new object[] { "STOOL" }));
                Assert.IsTrue(custom, "Borrowed provider art must not be disposed by the picker.");
            }
            finally { UnityEngine.Object.DestroyImmediate(custom); }
        }

        [Test]
        public void InspectorSectionsKeepPendingValuesDraftSeedAndUndoHistory()
        {
            workshop.OpenTemplate(WorkshopTemplate.FreestyleWorld, folder);
            workshop.History.Execute("Keep redo", draft => draft.Course.Title = "Pending title");
            workshop.History.Undo();
            string before = workshop.History.Current.RecoveryJson;
            int revision = workshop.History.Revision;
            InspectorField("pool").SetValue(workshop, "STOOL:3, BED:1");
            InspectorField("rangeMinY").SetValue(workshop, "1.75");
            InspectorField("prerequisite").SetValue(workshop, "AuthoredPuzzle");
            InspectorField("routeWeight").SetValue(workshop, "7");
            foreach (string section in new[] { "Puzzles", "Room", "Objects" })
            {
                InspectorField("inspectorScroll").SetValue(workshop, new Vector2(0, 900));
                InspectorField("workshopTyping").SetValue(workshop, true);
                ShowSection(section);
                Assert.AreEqual(section, InspectorField("inspectorSection").GetValue(workshop).ToString());
                Assert.AreEqual(Vector2.zero, InspectorField("inspectorScroll").GetValue(workshop));
                Assert.IsFalse((bool)InspectorField("workshopTyping").GetValue(workshop));
            }
            Assert.AreEqual(before, workshop.History.Current.RecoveryJson);
            Assert.AreEqual(revision, workshop.History.Revision); Assert.IsTrue(workshop.History.CanRedo);
            Assert.AreEqual("STOOL:3, BED:1", InspectorField("pool").GetValue(workshop));
            Assert.AreEqual("1.75", InspectorField("rangeMinY").GetValue(workshop));
            Assert.AreEqual("AuthoredPuzzle", InspectorField("prerequisite").GetValue(workshop));
            Assert.AreEqual("7", InspectorField("routeWeight").GetValue(workshop));
            workshop.History.Redo(); Assert.AreEqual("Pending title", workshop.History.Current.Course().Title);
        }

        [Test]
        public void ObjectPhysicalPuzzleAndControllerRoomSelectionRevealTheirInspector()
        {
            var course = CourseTemplates.RaceCircuit();
            var catalogue = CourseTemplates.ProofCatalogue();
            var draft = new WorkshopSnapshot(course).Edit();
            WorkshopPhysicalSites.Create(draft, catalogue, 1, "BedBridge");
            workshop.Open(draft.Course, catalogue, MatchStateValidation.Requirements(ModeDefinition.For("TimeRun")), folder);
            var zone = workshop.History.Current.Course().Zones.First();
            workshop.History.Select(new[] { zone.ModuleId });
            InspectorCall("Refresh");
            string before = workshop.History.Current.CourseJsonText;
            ShowSection("Room"); InspectorCall("SelectZone", zone);
            Assert.AreEqual("Objects", InspectorField("inspectorSection").GetValue(workshop).ToString());
            Assert.AreEqual(zone.Id, InspectorField("zoneId").GetValue(workshop));
            workshop.SelectPhysicalSite(1, "BedBridge");
            Assert.AreEqual("Puzzles", InspectorField("inspectorSection").GetValue(workshop).ToString());
            Assert.AreEqual("BedBridge", InspectorField("sitePuzzle").GetValue(workshop));
            Assert.AreEqual("BedBridge", InspectorField("puzzleId").GetValue(workshop));
            InspectorCall("StepSelection", 1);
            Assert.AreEqual("Room", InspectorField("inspectorSection").GetValue(workshop).ToString());
            Assert.AreEqual("", InspectorField("sitePuzzle").GetValue(workshop));
            Assert.AreEqual(before, workshop.History.Current.CourseJsonText);
        }

        [Test]
        public void FriendlyReferencesDisambiguateConnectionsAndRetainExactAdvancedIds()
        {
            var course = CourseTemplates.RaceCircuit();
            var duplicate = course.Clone().Connections.First();
            string first = course.Connections.First().Id;
            duplicate.Id = "internal_second_connection"; course.Connections.Add(duplicate);
            workshop.Open(course, CourseTemplates.ProofCatalogue(), MatchStateValidation.Requirements(ModeDefinition.For("TimeRun")), folder);
            string before = workshop.History.Current.RecoveryJson;
            string firstCaption = (string)InspectorCall("ReferenceCaption", first);
            string secondCaption = (string)InspectorCall("ReferenceCaption", duplicate.Id);
            Assert.AreNotEqual(firstCaption, secondCaption);
            StringAssert.Contains("→", secondCaption);
            StringAssert.DoesNotContain(duplicate.Id, secondCaption);
            string puzzle = (string)InspectorCall("PuzzleCaption", "BedBridge");
            StringAssert.Contains("Bed", puzzle); StringAssert.DoesNotContain("BedBridge", puzzle);
            InspectorField("inspectorAdvancedIds").SetValue(workshop, true);
            StringAssert.Contains(duplicate.Id, (string)InspectorCall("ReferenceCaption", duplicate.Id));
            StringAssert.Contains("BedBridge", (string)InspectorCall("PuzzleCaption", "BedBridge"));
            Assert.AreEqual(before, workshop.History.Current.RecoveryJson);
        }

        [Test]
        public void PuzzleRecipeTilesExcludePropsBeforeLimitingAndKeepAllCraftRecipesSearchable()
        {
            var proof = CourseTemplates.ProofCatalogue();
            var props = new[] { "AAONE", "AATWO", "AATHREE", "AAFOUR", "AAFIVE", "AASIX", "AASEVEN",
                "BOX", "CHEST", "CRATE", "DESK", "SHELF" }.Select(id => new CourseAssetDefinition(id, id, 500, 500, 500, false));
            var recipes = proof.Assets.Values.Concat(new[] {
                new CourseAssetDefinition("CLOCK", "CLOCK", 500, 500, 500),
                new CourseAssetDefinition("KEY", "KEY", 500, 500, 500),
                new CourseAssetDefinition("TABLE", "TABLE", 1000, 1000, 500) }).ToArray();
            var catalogue = new CourseCatalogue(proof.Modules.Values, props.Concat(recipes),
                proof.PuzzlePresets, proof.Themes, proof.NarrativeEvents);
            workshop.Open(CourseTemplates.RaceCircuit(), catalogue,
                MatchStateValidation.Requirements(ModeDefinition.For("TimeRun")), folder);
            string before = workshop.History.Current.RecoveryJson;
            ShowSection("Puzzles"); InspectorField("newPuzzle").SetValue(workshop, true);
            var visible = (string[])InspectorCall("PuzzleRecipeChoices");
            CollectionAssert.AreEqual(new[] { "BED", "CLOCK", "FAN", "KEY", "PLATE", "STOOL" }, visible);
            foreach (var recipe in recipes)
            {
                InspectorField("recipeSearch").SetValue(workshop, recipe.Id.ToLowerInvariant());
                CollectionAssert.Contains((string[])InspectorCall("PuzzleRecipeChoices"), recipe.Id);
                var draft = workshop.History.Current.Edit();
                WorkshopCommands.SetPuzzle(draft, new CoursePuzzle {
                    Id = "TestRecipe", ModuleId = "Start", PresetId = "CollectLetters",
                    RecipeId = recipe.Id, RequiredLetters = recipe.Letters }, catalogue);
                Assert.AreEqual(recipe.Id, draft.Course.Puzzles.Single(puzzle => puzzle.Id == "TestRecipe").RecipeId);
            }
            InspectorField("recipeSearch").SetValue(workshop, "box");
            CollectionAssert.IsEmpty((string[])InspectorCall("PuzzleRecipeChoices"));
            Assert.AreEqual(before, workshop.History.Current.RecoveryJson);
        }

        static FieldInfo InspectorField(string name) => typeof(WordboundWorkshop).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        object InspectorCall(string name, params object[] args) => typeof(WordboundWorkshop)
            .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(workshop, args);
        void ShowSection(string name) => InspectorCall("ShowInspectorSection",
            Enum.Parse(InspectorField("inspectorSection").FieldType, name));

        [Test]
        public void ReopeningTemplateRestoresItsAutosaveBeforeNewEdits()
        {
            var template = WorkshopTemplates.Create(WorkshopTemplate.FreestyleWorld);
            var draft = template.Snapshot.Edit();
            draft.Course.Title = "Recovered authoring";
            draft.Seed = "1234567890abcdef";
            new WorkshopStorage(folder).Save(new WorkshopSnapshot(draft));

            workshop.OpenTemplate(WorkshopTemplate.FreestyleWorld, folder);
            Assert.AreEqual("Recovered authoring", workshop.History.Current.Course().Title);
            Assert.AreEqual("1234567890abcdef", workshop.History.Current.Seed);
            Assert.IsFalse(workshop.History.CanUndo);
        }

        [Test]
        public void SuppliedDocumentDoesNotSilentlyLoadOrOverwriteAnEarlierSave()
        {
            var template = WorkshopTemplates.Create(WorkshopTemplate.FreestyleWorld);
            var store = new WorkshopStorage(folder);
            store.Save(template.Snapshot);
            var course = template.Snapshot.Course();
            course.Title = "Explicit import";
            workshop.Open(course, template.Catalogue, template.Play, folder);
            Assert.AreEqual("Explicit import", workshop.History.Current.Course().Title);
            Assert.AreEqual(template.Snapshot.Course().Title, store.Recover(course.Id, out _).Course().Title);
        }

        [Test]
        public void PlaytestAndTemplateRoundTripPreserveTheEditingHistory()
        {
            workshop.OpenTemplate(WorkshopTemplate.FreestyleWorld, folder);
            workshop.History.Execute("Rename", draft => draft.Course.Title = "My world");
            string before = workshop.History.Current.RecoveryJson;
            ResolvedCourse dispatched = null;
            workshop.PlaytestRequested += manifest => dispatched = manifest;
            workshop.RequestPlaytest();
            Assert.IsNotNull(dispatched);
            Assert.AreSame(workshop.LastPreview.Manifest, dispatched);
            Assert.AreEqual(before, workshop.LastPlaytestDraft.RecoveryJson);
            workshop.Visible = false;
            workshop.ResumeEditing();
            Assert.AreEqual(before, workshop.History.Current.RecoveryJson);

            workshop.OpenTemplate(WorkshopTemplate.CombatArena, folder);
            workshop.OpenTemplate(WorkshopTemplate.FreestyleWorld, folder);
            Assert.AreEqual(before, workshop.History.Current.RecoveryJson);
            Assert.IsTrue(workshop.History.Undo());
            Assert.AreNotEqual("My world", workshop.History.Current.Course().Title);
        }
        [Test]
        public void PlaytestCapturesExactDraftBeforeLauncherChangesEditingState()
        {
            workshop.OpenTemplate(WorkshopTemplate.FreestyleWorld, folder);
            workshop.History.Execute("Seed", draft => draft.Seed = "1234567890abcdef");
            var expected = workshop.History.Current;
            string expectedManifest = CourseResolver.Resolve(expected.Course(), workshop.Catalogue,
                new CourseResolveRequest(expected.Seed, workshop.PlayRequirements)).Manifest.Hash;
            ResolvedCourse dispatched = null;
            workshop.PlaytestRequested += manifest =>
            {
                dispatched = manifest;
                workshop.History.Execute("Later edit", draft => draft.Course.Title = "After dispatch");
            };
            workshop.RequestPlaytest();
            Assert.AreSame(expected, workshop.LastPlaytestDraft);
            Assert.AreEqual(expectedManifest, dispatched.Hash);
            Assert.AreEqual("1234567890abcdef", workshop.LastPlaytestDraft.Seed);
            Assert.AreNotEqual(workshop.History.Current.CourseJsonText, workshop.LastPlaytestDraft.CourseJsonText);
        }
        [Test]
        public void InvalidDraftCannotDispatchAnEarlierSuccessfulPlaytest()
        {
            workshop.OpenTemplate(WorkshopTemplate.FreestyleWorld, folder);
            int dispatches = 0;
            workshop.PlaytestRequested += _ => dispatches++;
            workshop.RequestPlaytest();
            Assert.AreEqual(1, dispatches);
            Assert.IsNotNull(workshop.LastPlaytestDraft);
            workshop.History.Execute("Incomplete draft", draft => draft.Course.Spawns.Clear());
            workshop.RequestPlaytest();
            Assert.AreEqual(1, dispatches);
            Assert.IsNull(workshop.LastPlaytestDraft);
            Assert.IsFalse(workshop.LastPreview.Success);
        }
        [TestCase(800, 600, 0, 0)]
        [TestCase(1280, 720, 55, -30)]
        [TestCase(640, 960, -80, 35)]
        public void ViewportProjectsAndPicksTheActiveFloorWithoutChangingCourseData(int width, int height, int orbitX, int orbitY)
        {
            var preset = WorkshopTemplates.Create(WorkshopTemplate.FreestyleWorld);
            using (var view = new WordboundWorkshopViewport(owner.transform))
            {
                view.Sync(preset.Snapshot, preset.Catalogue); view.Frame(); view.Resize(width, height);
                view.Orbit(new Vector2(orbitX, orbitY));
                var module = preset.Snapshot.Course().Modules.Find(item => item.Id == "Start");
                var rect = new Rect(0, 0, width, height);
                var screen = view.Project(module.Position, rect);
                Assert.IsTrue(view.Surface(screen, rect, module.Position.Y, out var point));
                Assert.LessOrEqual(Math.Abs(point.X - module.Position.X), 5);
                Assert.LessOrEqual(Math.Abs(point.Z - module.Position.Z), 5);
                Assert.AreEqual("Start", view.Pick(screen, rect));
                var fixedZone = preset.Snapshot.Course().Zones.Find(item => item.Placement == "Fixed");
                var asset = preset.Catalogue.Assets[fixedZone.Pool[0].AssetId];
                var centre = new CoursePoint(fixedZone.FixedPosition.X, fixedZone.FixedPosition.Y + asset.HeightMm / 2, fixedZone.FixedPosition.Z);
                Assert.AreEqual(fixedZone.Id, view.PickFixed(view.Project(centre, rect), rect));
                var history = new WorkshopHistory(preset.Snapshot); history.Select(new[] { "Start" });
                var drag = new WorkshopTransformDrag(history, WorkshopHandle.Plane, module.Position);
                drag.Move(1500, 500);
                string before = history.Current.RecoveryJson;
                view.TransformPreview(drag, module.Position);
                Assert.AreEqual(before, history.Current.RecoveryJson);
            }
        }
        [Test]
        public void ViewportDisposalReleasesOnlyItsOwnedCameraAndRenderTarget()
        {
            var other = new GameObject("Unrelated camera").AddComponent<Camera>();
            try
            {
                var view = new WordboundWorkshopViewport(owner.transform);
                var image = view.Texture;
                view.Dispose();
                Assert.IsTrue(other);
                Assert.IsTrue(image == null);
                Assert.AreEqual(0, owner.GetComponentsInChildren<Camera>(true).Length);
            }
            finally { UnityEngine.Object.DestroyImmediate(other.gameObject); }
        }
    }
}
