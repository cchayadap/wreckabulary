using System;
using System.IO;
using System.Reflection;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
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
            foreach (string section in new[] { "Puzzles", "Rules", "Objects", "Share", "Build" })
            {
                InspectorField("inspectorScroll").SetValue(workshop, new Vector2(0, 900));
                InspectorField("workshopTyping").SetValue(workshop, true);
                ShowSection(section);
                Assert.AreEqual(section, InspectorField("step").GetValue(workshop).ToString());
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
            ShowSection("Rules"); InspectorCall("SelectZone", zone);
            Assert.AreEqual("Objects", InspectorField("step").GetValue(workshop).ToString());
            Assert.AreEqual(zone.Id, InspectorField("zoneId").GetValue(workshop));
            workshop.SelectPhysicalSite(1, "BedBridge");
            Assert.AreEqual("Puzzles", InspectorField("step").GetValue(workshop).ToString());
            Assert.AreEqual("BedBridge", InspectorField("sitePuzzle").GetValue(workshop));
            Assert.AreEqual("BedBridge", InspectorField("puzzleId").GetValue(workshop));
            InspectorCall("StepSelection", 1);
            Assert.AreEqual("Puzzles", InspectorField("step").GetValue(workshop).ToString(), "Selecting the next room keeps the chosen step.");
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

        [TestCase(300, 15, 34, 56)]
        [TestCase(300, 22, 40, 64)]
        [TestCase(239, 15, 34, 56)]
        [TestCase(239, 22, 40, 64)]
        public void AllRoutePanelsStayInsideTheActualInspectorWidth(int panelWidth, int font, int row, int module)
        {
            var theme = ScriptableObject.CreateInstance<WordboundUiTheme>();
            RouteInspectorWindow window = null;
            try
            {
                theme.BodyFont = Resources.Load<Font>("Fonts/Roboto/v3.016/Roboto-Regular");
                Assert.IsNotNull(theme.BodyFont);
                theme.BodySize = font; theme.RowHeight = row; theme.ModuleHeight = module;
                workshop.Theme = theme;
                InspectorField("inspectorWidth").SetValue(workshop, (float)panelWidth);
                var course = CourseTemplates.RaceCircuit();
                workshop.Open(course, CourseTemplates.ProofCatalogue(),
                    MatchStateValidation.Requirements(ModeDefinition.For("TimeRun")), folder);
                workshop.History.Select(new[] { "Start" }); InspectorCall("Refresh"); ShowSection("Rules");
                InspectorField("routesExpanded").SetValue(workshop, true);
                InspectorField("routeModule").SetValue(workshop, "Start");
                InspectorField("routeRecovery").SetValue(workshop, "StartSafe");
                InspectorField("routeCheckpoint").SetValue(workshop, "Begin");
                InspectorField("routeSection").SetValue(workshop, "Circuit");
                InspectorField("routeBranch").SetValue(workshop, "North");
                InspectorField("routeSpawn").SetValue(workshop, course.Spawns.First().Id);
                string before = workshop.History.Current.RecoveryJson;
                int revision = workshop.History.Revision;
                bool undo = workshop.History.CanUndo, redo = workshop.History.CanRedo;
                var styles = (WordboundUiStyles)InspectorField("ui").GetValue(workshop);
                window = ScriptableObject.CreateInstance<RouteInspectorWindow>();
                window.Draw = () => {
                    using var scope = styles.Apply(theme);
                    InspectorCall("Styles");
                    GUILayout.BeginArea(new Rect(0, 0, panelWidth, 520), styles.Panel);
                    InspectorCall("StepPanel");
                    GUILayout.EndArea();
                    if (Event.current.type != EventType.Repaint) return;
                    float width = panelWidth - styles.Panel.padding.horizontal - GUI.skin.verticalScrollbar.fixedWidth - GUI.skin.verticalScrollbar.margin.left;
                    foreach (string field in new[] { "routeContentRect", "routeTabsRect" })
                    {
                        var rect = (Rect)InspectorField(field).GetValue(workshop);
                        Assert.Greater(rect.width, 80, field);
                        Assert.LessOrEqual(rect.xMax, width + 1, "Route tab " + InspectorField("routeTab").GetValue(workshop) + ": " + field);
                        Assert.GreaterOrEqual(rect.xMin, -1, field);
                    }
                    Assert.GreaterOrEqual(((Rect)InspectorField("routeTabsRect").GetValue(workshop)).height, styles.RowHeight * 3);
                };
                window.position = new Rect(100, 100, 340, 560); window.ShowUtility();
                for (int tab = 0; tab < 5; tab++)
                {
                    InspectorField("routeTab").SetValue(workshop, tab);
                    InspectorField("routeLoadKey").SetValue(workshop, "");
                    for (int frame = 0; frame < 2; frame++)
                    {
                        int repaints = window.Repaints;
                        window.SendEvent(new Event { type = EventType.Layout });
                        window.SendEvent(new Event { type = EventType.Repaint });
                        Assert.IsNull(window.Failure); Assert.Greater(window.Repaints, repaints);
                    }
                    Assert.AreEqual(tab, InspectorField("routeTab").GetValue(workshop));
                }
                Assert.AreEqual(before, workshop.History.Current.RecoveryJson);
                Assert.AreEqual(revision, workshop.History.Revision);
                Assert.AreEqual(undo, workshop.History.CanUndo); Assert.AreEqual(redo, workshop.History.CanRedo);
            }
            finally { if (window) window.Close(); workshop.Theme = null; UnityEngine.Object.DestroyImmediate(theme); }
        }

        sealed class RouteInspectorWindow : EditorWindow
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

        [Test]
        public void FloorRowsDeriveFootprintAndStageHeightsUntilOneUndoableSet()
        {
            workshop.Open(CourseTemplates.RaceCircuit(), CourseTemplates.ProofCatalogue(),
                MatchStateValidation.Requirements(ModeDefinition.For("TimeRun")), folder);
            string before = workshop.History.Current.RecoveryJson;
            int revision = workshop.History.Revision;
            Assert.AreEqual("20 × 20 m", InspectorCall("FloorSize", "Ground"));
            Assert.AreEqual("40 × 20 m", InspectorCall("FloorSize", "Loft"));
            Assert.AreEqual("10", InspectorField("layerHeight").GetValue(workshop), "+ Floor defaults to just above the top floor's rooms.");
            Assert.AreEqual("0", InspectorField("floorHeight").GetValue(workshop));
            Assert.AreEqual(before, workshop.History.Current.RecoveryJson);
            workshop.History.Execute("Choose loft", draft => draft.ActiveLayer = "Loft"); InspectorCall("Refresh");
            var chosen = workshop.History.Current; revision = workshop.History.Revision;
            Assert.AreEqual("6", InspectorField("floorHeight").GetValue(workshop));
            InspectorField("floorHeight").SetValue(workshop, "-1");
            InspectorCall("SetFloorHeight", "Loft");
            Assert.AreSame(chosen, workshop.History.Current); Assert.AreEqual(revision, workshop.History.Revision);
            Assert.IsNotEmpty((string)InspectorField("status").GetValue(workshop));
            InspectorField("floorHeight").SetValue(workshop, "6.5");
            InspectorCall("SetFloorHeight", "Loft");
            Assert.AreEqual(revision + 1, workshop.History.Revision);
            Assert.AreEqual(6500, workshop.History.Current.Course().Layers.Single(layer => layer.Id == "Loft").HeightMm);
            Assert.AreEqual("6.5", InspectorField("floorHeight").GetValue(workshop));
            Assert.IsTrue(workshop.History.Undo()); Assert.AreSame(chosen, workshop.History.Current);
        }

        [Test]
        public void SectionStatePersistsAsAViewPreferenceAndNeverEntersTheDraft()
        {
            string[] names = { "routes", "view", "floors" };
            var keys = names.Select(name => "wv.workshop.section." + name).ToArray();
            var previous = keys.Select(key => PlayerPrefs.HasKey(key) ? (int?)PlayerPrefs.GetInt(key) : null).ToArray();
            GameObject second = null;
            try
            {
                workshop.OpenTemplate(WorkshopTemplate.FreestyleWorld, folder);
                string before = workshop.History.Current.RecoveryJson, course = workshop.History.Current.CourseJsonText;
                int revision = workshop.History.Revision;
                InspectorCall("RememberSection", "routes", true);
                InspectorCall("RememberSection", "view", false);
                InspectorCall("RememberSection", "floors", false);
                Assert.AreEqual(1, PlayerPrefs.GetInt(keys[0])); Assert.AreEqual(0, PlayerPrefs.GetInt(keys[1])); Assert.AreEqual(0, PlayerPrefs.GetInt(keys[2]));
                second = new GameObject("Second workshop");
                var other = second.AddComponent<WordboundWorkshop>();
                InspectorField("routesExpanded").SetValue(other, false); InspectorField("showWorkshopHelp").SetValue(other, true);
                typeof(WordboundWorkshop).GetMethod("LoadSections", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(other, null);
                Assert.IsTrue((bool)InspectorField("routesExpanded").GetValue(other));
                Assert.IsFalse((bool)InspectorField("showWorkshopHelp").GetValue(other));
                var saved = typeof(WordboundWorkshop).GetMethod("SavedSection", BindingFlags.Static | BindingFlags.NonPublic);
                Assert.IsFalse((bool)saved.Invoke(null, new object[] { "floors", true }), "A remembered closed section overrides its open default.");
                PlayerPrefs.DeleteKey(keys[2]);
                Assert.IsTrue((bool)saved.Invoke(null, new object[] { "floors", true }), "A section without a preference uses its default.");
                Assert.AreEqual(before, workshop.History.Current.RecoveryJson); Assert.AreEqual(course, workshop.History.Current.CourseJsonText);
                Assert.AreEqual(revision, workshop.History.Revision);
                StringAssert.DoesNotContain("wv.workshop", before);
            }
            finally
            {
                if (second) UnityEngine.Object.DestroyImmediate(second);
                for (int i = 0; i < keys.Length; i++)
                    if (previous[i].HasValue) PlayerPrefs.SetInt(keys[i], previous[i].Value); else PlayerPrefs.DeleteKey(keys[i]);
            }
        }

        static FieldInfo InspectorField(string name) => typeof(WordboundWorkshop).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        object InspectorCall(string name, params object[] args) => typeof(WordboundWorkshop)
            .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(workshop, args);
        void ShowSection(string name) => InspectorCall("ShowStep",
            Enum.Parse(InspectorField("step").FieldType, name));

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
