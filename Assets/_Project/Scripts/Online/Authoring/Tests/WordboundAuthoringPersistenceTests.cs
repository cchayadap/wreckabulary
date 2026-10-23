using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Wreckabulary.Online;
using Wreckabulary.Online.Editor;
using Wreckabulary.Rules.Wordbound;
using Object = UnityEngine.Object;

namespace Wreckabulary.Tests
{
    public sealed class WordboundAuthoringPersistenceTests
    {
        const string SessionPrefix = "Wordbound.Authoring.Playtest.";
        string folder;
        [SetUp] public void CreateFolder()
        {
            Assert.IsFalse(EditorApplication.isPlayingOrWillChangePlaymode);
            Assert.IsFalse(SessionState.GetBool(SessionPrefix + "active", false), "Close the authoring playtest before running persistence checks.");
            folder = "Assets/__WordboundAuthoring_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", folder.Substring("Assets/".Length));
        }
        [TearDown] public void DeleteFolder()
        {
            if (folder != null && folder.StartsWith("Assets/__WordboundAuthoring_", StringComparison.Ordinal))
            {
                foreach (string guid in AssetDatabase.FindAssets("t:WordboundCourseAsset", new[] { folder }))
                    WordboundCourseDraftStore.Clear(AssetDatabase.LoadAssetAtPath<WordboundCourseAsset>(AssetDatabase.GUIDToAssetPath(guid)));
                AssetDatabase.DeleteAsset(folder);
            }
        }

        [Test]
        public void RepeatedDraftRecoveryPreservesExactValidZoneCollections()
        {
            var asset = CreateDraftAsset();
            var document = asset.ReadDocument();
            document.Title = "Recovered without collection defaults";
            Assert.That(document.Zones.Count, Is.GreaterThan(0));
            Assert.IsTrue(document.Zones.Any(zone => zone.Placement == "Fixed"));
            string expected = CourseJson.ExportDocument(document);
            for (int attempt = 0; attempt < 3; attempt++)
            {
                WordboundCourseDraftStore.Save(asset, asset.SourceHash, document);
                Assert.IsFalse(WordboundCourseDraftStore.IsMemoryOnly(asset));
                document = WordboundCourseDraftStore.Load(asset).Document;
                Assert.AreEqual(expected, CourseJson.ExportDocument(document), "Recovery pass " + attempt);
            }
            asset.SaveDocument(document);
            Assert.AreEqual(expected, asset.DocumentJson);
        }

        [Test]
        public void InvalidPendingFormSurvivesClosingInspectorAndRejectedApplyWithoutChangingSavedCourse()
        {
            var asset = CreateDraftAsset();
            string original = asset.DocumentJson;
            var inspector = UnityEditor.Editor.CreateEditor(asset, typeof(WordboundCourseAssetEditor));
            try
            {
                var pending = EditorField<CourseDocument>(inspector, "draft");
                pending.Title = "Still editing";
                pending.Modules[0].Position = new CoursePoint(int.MaxValue, -100, 31);
                pending.Zones[0].Yaws.Clear();
                pending.Zones[0].Yaws.AddRange(new[] { 90, 90, 13 });
                pending.ReservedVolumes.Add(new CourseReservedVolume {
                    Id = "unfinished", Bounds = new CourseBox(new CoursePoint(900, 300, 100), new CoursePoint(-50, 0, -10))
                });
                InvokeEditor(inspector, "MarkDirty");
                Object.DestroyImmediate(inspector);
                inspector = UnityEditor.Editor.CreateEditor(asset, typeof(WordboundCourseAssetEditor));
                pending = EditorField<CourseDocument>(inspector, "draft");
                Assert.IsTrue(EditorField<bool>(inspector, "dirty"));
                Assert.AreEqual("Still editing", pending.Title);
                Assert.AreEqual(int.MaxValue, pending.Modules[0].Position.X);
                Assert.AreEqual(-50, pending.ReservedVolumes.Last().Bounds.Max.X);
                CollectionAssert.AreEqual(new[] { 90, 90, 13 }, pending.Zones[0].Yaws,
                    "Recovery must preserve unfinished values, including duplicates and invalid angles.");
                Assert.Throws<ArgumentException>(() => InvokeEditor(inspector, "Save"));
                Assert.AreEqual(original, asset.DocumentJson);
                Assert.AreEqual("Still editing", WordboundCourseDraftStore.Load(asset).Document.Title);
            }
            finally { if (inspector) Object.DestroyImmediate(inspector); }
        }

        [Test]
        public void ChangedSavedCourseRequiresExplicitRecoveryChoiceAndSuccessfulApplyClearsDraft()
        {
            var asset = CreateDraftAsset();
            var pending = asset.ReadDocument(); pending.Title = "Pending title";
            WordboundCourseDraftStore.Save(asset, asset.SourceHash, pending);
            var saved = asset.ReadDocument(); saved.Title = "Changed elsewhere";
            asset.SaveDocument(saved); EditorUtility.SetDirty(asset); AssetDatabase.SaveAssetIfDirty(asset);
            var inspector = UnityEditor.Editor.CreateEditor(asset, typeof(WordboundCourseAssetEditor));
            try
            {
                Assert.AreEqual("Changed elsewhere", EditorField<CourseDocument>(inspector, "draft").Title);
                Assert.AreEqual("Pending title", EditorField<WordboundCourseDraftStore.PendingDraft>(inspector, "conflictingDraft").Document.Title);
                Assert.DoesNotThrow(() => InvokeEditor(inspector, "SceneGUI", new object[] { null }));
                Assert.Throws<InvalidOperationException>(() => InvokeEditor(inspector, "Save"));
                Assert.AreEqual("Changed elsewhere", asset.ReadDocument().Title);
                InvokeEditor(inspector, "RestorePending");
                InvokeEditor(inspector, "Save");
                Assert.AreEqual("Pending title", asset.ReadDocument().Title);
                Assert.IsNull(WordboundCourseDraftStore.Load(asset));
                Assert.IsFalse(EditorField<bool>(inspector, "dirty"));
            }
            finally { Object.DestroyImmediate(inspector); }
        }

        [Test]
        public void LockedRecoveryFileKeepsNewestEditsThroughSavedChangesAndInspectorRecreation()
        {
            var asset = CreateDraftAsset();
            WordboundCourseDraftStore.Save(asset, asset.SourceHash, asset.ReadDocument());
            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset));
            string path = Path.Combine("Library", "Wordbound", "InspectorDrafts", guid + ".json");
            var inspector = UnityEditor.Editor.CreateEditor(asset, typeof(WordboundCourseAssetEditor));
            try
            {
                EditorField<CourseDocument>(inspector, "draft").Title = "Newest edits";
                InvokeEditor(inspector, "MarkDirty");
                var external = asset.ReadDocument(); external.Title = "Saved elsewhere"; asset.SaveDocument(external);
                using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("^Wordbound pending form could not be saved:"));
                    InvokeEditor(inspector, "ReloadAfterUndo");
                    Assert.AreEqual("Newest edits", EditorField<WordboundCourseDraftStore.PendingDraft>(inspector, "conflictingDraft").Document.Title);
                    Assert.AreEqual("Saved elsewhere", asset.ReadDocument().Title);
                    InvokeEditor(inspector, "RestorePending");
                    LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("^Wordbound pending form could not be saved:"));
                    Object.DestroyImmediate(inspector);
                    inspector = UnityEditor.Editor.CreateEditor(asset, typeof(WordboundCourseAssetEditor));
                    Assert.AreEqual("Newest edits", EditorField<CourseDocument>(inspector, "draft").Title);
                    Assert.IsTrue(EditorField<bool>(inspector, "dirty"));
                    Assert.IsNotEmpty(EditorField<string>(inspector, "recoveryWarning"));
                    Assert.IsFalse(double.IsPositiveInfinity(EditorField<double>(inspector, "saveDraftAt")));
                }
                InvokeEditor(inspector, "SavePendingDraft");
                Assert.AreEqual("", EditorField<string>(inspector, "recoveryWarning"));
                Object.DestroyImmediate(inspector);
                inspector = UnityEditor.Editor.CreateEditor(asset, typeof(WordboundCourseAssetEditor));
                Assert.AreEqual("Newest edits", EditorField<CourseDocument>(inspector, "draft").Title);
                InvokeEditor(inspector, "Save");
                Assert.AreEqual("Newest edits", asset.ReadDocument().Title);
                Assert.IsNull(WordboundCourseDraftStore.Load(asset));
            }
            finally { if (inspector) Object.DestroyImmediate(inspector); }
        }

        [Test]
        public void DamagedPendingFileIsRetainedWhenInspectorCloses()
        {
            var asset = CreateDraftAsset();
            WordboundCourseDraftStore.Save(asset, asset.SourceHash, asset.ReadDocument());
            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset));
            string path = Path.Combine("Library", "Wordbound", "InspectorDrafts", guid + ".json");
            File.WriteAllText(path, "interrupted local draft");
            var inspector = UnityEditor.Editor.CreateEditor(asset, typeof(WordboundCourseAssetEditor));
            try
            {
                Assert.IsTrue(EditorField<bool>(inspector, "recoveryFailed"));
                Assert.Throws<InvalidOperationException>(() => InvokeEditor(inspector, "Save"));
            }
            finally { Object.DestroyImmediate(inspector); }
            Assert.AreEqual("interrupted local draft", File.ReadAllText(path));
        }

        WordboundCourseAsset CreateDraftAsset()
        {
            var asset = ScriptableObject.CreateInstance<WordboundCourseAsset>();
            asset.Initialize(CourseTemplates.FreestyleWorld(), CourseTemplates.ProofCatalogue());
            AssetDatabase.CreateAsset(asset, folder + "/Draft.asset");
            return asset;
        }
        static T EditorField<T>(UnityEditor.Editor editor, string name) =>
            (T)typeof(WordboundCourseAssetEditor).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(editor);
        static void InvokeEditor(UnityEditor.Editor editor, string name, params object[] arguments)
        {
            try { typeof(WordboundCourseAssetEditor).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(editor, arguments); }
            catch (TargetInvocationException error) { throw error.InnerException ?? error; }
        }

        [Test]
        public void SavedCourseProfilesPrefabAndMaterialsReloadIntoTheExactPreviewAndPlayContent()
        {
            var camera = ScriptableObject.CreateInstance<WordboundCameraProfile>();
            camera.FieldOfView = 67; camera.Distance = 4.5f; camera.Validate();
            AssetDatabase.CreateAsset(camera, folder + "/Camera.asset");
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            Assert.IsNotNull(shader, "The project URP shader must be imported before this native fixture.");
            var material = new Material(shader) { color = new Color(.12f, .47f, .81f) };
            AssetDatabase.CreateAsset(material, folder + "/Blue.mat");
            var model = GameObject.CreatePrimitive(PrimitiveType.Cube);
            GameObject prefab;
            try
            {
                Object.DestroyImmediate(model.GetComponent<Collider>());
                model.GetComponent<Renderer>().sharedMaterial = material;
                var grip = new GameObject("Grip_R"); grip.transform.SetParent(model.transform, false);
                grip.transform.localPosition = new Vector3(.1f, 0, .1f);
                prefab = PrefabUtility.SaveAsPrefabAsset(model, folder + "/Model.prefab");
            }
            finally { Object.DestroyImmediate(model); }
            var visual = ScriptableObject.CreateInstance<WordboundVisualProfile>();
            visual.Overrides.Add(new WordboundVisualOverride { VisualKey = "STOOL", Model = prefab, Materials = new[] { material } });
            visual.Validate(); AssetDatabase.CreateAsset(visual, folder + "/Visual.asset");
            var course = ScriptableObject.CreateInstance<WordboundCourseAsset>();
            course.Initialize(CourseTemplates.FreestyleWorld(), CourseTemplates.ProofCatalogue());
            course.Items = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/_Project/Data/Config/items.json");
            course.RuleBook = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/_Project/Data/Config/rules.json");
            course.CameraProfile = camera; course.VisualProfile = visual; course.Seed = "0123456789abcdef";
            var document = course.ReadDocument(); document.Title = "Saved Inspector course"; course.SaveDocument(document);
            AssetDatabase.CreateAsset(course, folder + "/Course.asset");
            foreach (var saved in new Object[] { camera, material, visual, course }) { EditorUtility.SetDirty(saved); AssetDatabase.SaveAssetIfDirty(saved); }
            string inputHash = course.InputHash, sourceHash = course.SourceHash;
            var resolved = course.Resolve();
            string manifestJson = CourseJson.ExportManifest(resolved), manifestHash = resolved.Hash;
            string contentHash = course.CreateContent(resolved).CatalogueHash;
            Assert.That(new FileInfo(folder + "/Course.asset").Length, Is.GreaterThan(0));

            document.Title = "UNSAVED runtime mutation"; course.SaveDocument(document); camera.FieldOfView = 39;
            Resources.UnloadAsset(course); Resources.UnloadAsset(visual); Resources.UnloadAsset(camera);
            course = AssetDatabase.LoadAssetAtPath<WordboundCourseAsset>(folder + "/Course.asset");
            Assert.AreEqual("Saved Inspector course", course.ReadDocument().Title);
            Assert.AreEqual(sourceHash, course.SourceHash); Assert.AreEqual(inputHash, course.InputHash);
            Assert.AreEqual(67, course.CameraProfile.FieldOfView); Assert.AreEqual(4.5f, course.CameraProfile.Distance);
            Assert.AreEqual(folder + "/Visual.asset", AssetDatabase.GetAssetPath(course.VisualProfile));
            Assert.AreEqual(folder + "/Model.prefab", AssetDatabase.GetAssetPath(course.VisualProfile.Overrides.Single().Model));
            Assert.AreEqual(folder + "/Blue.mat", AssetDatabase.GetAssetPath(course.VisualProfile.Overrides.Single().Materials.Single()));
            var imported = CourseJson.ImportManifest(manifestJson, course.Catalogue());
            Assert.IsTrue(imported.Success, string.Join("\n", imported.Issues));
            var playContent = course.CreateContent(imported.Manifest);
            Assert.AreEqual(manifestHash, playContent.Course.Hash); Assert.AreEqual(contentHash, playContent.CatalogueHash);
            Assert.AreEqual(manifestHash, course.Resolve().Hash, "Reload must resolve the same saved seed and inputs.");

            var preview = ScriptableObject.CreateInstance<WordboundCoursePreview>();
            Component previewWorld = null;
            try
            {
                typeof(WordboundCoursePreview).GetMethod("Initialize", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(preview, new object[] { course, imported.Manifest });
                Assert.AreEqual("", typeof(WordboundCoursePreview).GetField("error", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(preview));
                previewWorld = (Component)typeof(WordboundCoursePreview).GetField("world", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(preview);
                Assert.IsNotNull(previewWorld, "The actual preview must construct the saved manifest's geometry.");
                Assert.AreEqual(manifestHash, previewWorld.GetType().GetProperty("ManifestHash").GetValue(previewWorld));
                Assert.That(previewWorld.GetComponentsInChildren<Collider>(true).Length, Is.GreaterThan(0));
            }
            finally { Object.DestroyImmediate(preview); }
            Assert.IsTrue(previewWorld == null, "Closing the preview must dispose its generated world.");

            var parent = new GameObject("Reloaded visual check");
            try
            {
                Assert.IsTrue(course.VisualProfile.TryInstantiate("STOOL", parent.transform, out var created));
                Assert.AreEqual(course.VisualProfile.Overrides[0].Materials[0], created.GetComponent<Renderer>().sharedMaterial);
                Assert.AreEqual(0, created.GetComponentsInChildren<Collider>(true).Length);
                Assert.IsNotNull(created.transform.Find("Grip_R"));
            }
            finally { Object.DestroyImmediate(parent); }
            var before = EditorSceneManager.GetSceneManagerSetup();
            Assert.Throws<ArgumentException>(() => WordboundCoursePlaytest.Start(course, imported.Manifest, new string('0', 64)));
            CollectionAssert.AreEqual(before.Select(s => s.path).ToArray(), EditorSceneManager.GetSceneManagerSetup().Select(s => s.path).ToArray());
            Assert.IsFalse(EditorApplication.isPlayingOrWillChangePlaymode);
            Assert.IsFalse(SessionState.GetBool(SessionPrefix + "active", false), "Rejected play setup must not claim the editor session.");
        }

        [Test]
        public void RestorationRetainsSavedSceneBesideUntitledAdditiveSceneAndOriginalActiveSelection()
        {
            var previous = EditorSceneManager.GetSceneManagerSetup();
            var originalScenes = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).ToArray();
            var originalRoots = originalScenes.Select(scene => scene.isLoaded
                ? scene.GetRootGameObjects().OrderBy(root => root.transform.GetSiblingIndex()).ToArray()
                : Array.Empty<GameObject>()).ToArray();
            var clearDirtiness = typeof(EditorSceneManager).GetMethod("ClearSceneDirtiness", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(clearDirtiness, "The native fixture must be able to restore clean ambient scene flags.");
            foreach (var scene in originalScenes)
                Assert.IsFalse(scene.isDirty, "Run scene restoration checks from a clean editor setup.");
            string oldScenes = SessionState.GetString(SessionPrefix + "scenes", "");
            var parking = EditorSceneManager.NewPreviewScene();
            bool replacedSetup = false;
            try
            {
                foreach (var roots in originalRoots)
                    foreach (var root in roots) SceneManager.MoveGameObjectToScene(root, parking);
                replacedSetup = true;
                var saved = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                string path = folder + "/Saved.unity";
                Assert.IsTrue(EditorSceneManager.SaveScene(saved, path));
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                SceneManager.SetActiveScene(saved);
                string records = "{\"scenes\":[{\"path\":\"" + path + "\",\"loaded\":true,\"active\":true}," +
                    "{\"path\":\"\",\"loaded\":true,\"active\":false}]}";
                SessionState.SetString(SessionPrefix + "scenes", records);
                SessionState.SetBool(SessionPrefix + "active", true);
                InvokeRestore();
                Assert.AreEqual(2, SceneManager.sceneCount);
                Assert.AreEqual(path, SceneManager.GetSceneAt(0).path);
                Assert.AreEqual("", SceneManager.GetSceneAt(1).path);
                Assert.AreEqual(path, SceneManager.GetActiveScene().path);
                Assert.IsFalse(SessionState.GetBool(SessionPrefix + "active", false));
            }
            finally
            {
                try
                {
                    if (replacedSetup)
                    {
                        SessionState.SetString(SessionPrefix + "scenes", JsonUtility.ToJson(new SavedSetup {
                            scenes = previous.Select(s => new SavedScene { path = s.path, loaded = s.isLoaded, active = s.isActive }).ToArray() }));
                        SessionState.SetBool(SessionPrefix + "active", true);
                        InvokeRestore();
                    }
                    for (int i = 0; i < originalScenes.Length; i++)
                    {
                        var scene = replacedSetup ? SceneManager.GetSceneAt(i) : originalScenes[i];
                        if (!scene.isLoaded) continue;
                        if (replacedSetup)
                            foreach (var generated in scene.GetRootGameObjects()) Object.DestroyImmediate(generated);
                        for (int index = 0; index < originalRoots[i].Length; index++)
                        {
                            var root = originalRoots[i][index];
                            SceneManager.MoveGameObjectToScene(root, scene);
                            root.transform.SetSiblingIndex(index);
                        }
                    }
                    for (int i = 0; i < originalScenes.Length; i++)
                    {
                        var scene = replacedSetup ? SceneManager.GetSceneAt(i) : originalScenes[i];
                        if (!scene.isLoaded) continue;
                        clearDirtiness.Invoke(null, new object[] { scene });
                        CollectionAssert.AreEqual(originalRoots[i], scene.GetRootGameObjects().OrderBy(root => root.transform.GetSiblingIndex()).ToArray());
                        Assert.IsFalse(scene.isDirty);
                    }
                    CollectionAssert.AreEqual(previous.Select(s => s.path), EditorSceneManager.GetSceneManagerSetup().Select(s => s.path));
                    CollectionAssert.AreEqual(previous.Select(s => s.isLoaded), EditorSceneManager.GetSceneManagerSetup().Select(s => s.isLoaded));
                    CollectionAssert.AreEqual(previous.Select(s => s.isActive), EditorSceneManager.GetSceneManagerSetup().Select(s => s.isActive));
                }
                finally
                {
                    SessionState.SetString(SessionPrefix + "scenes", oldScenes);
                    SessionState.EraseBool(SessionPrefix + "active");
                    if (parking.IsValid() && parking.rootCount == 0) EditorSceneManager.ClosePreviewScene(parking);
                }
            }
        }

        [Test]
        public void MissingSavedSceneKeepsCurrentSetupAndRecoveryRecord()
        {
            var previous = EditorSceneManager.GetSceneManagerSetup();
            string oldScenes = SessionState.GetString(SessionPrefix + "scenes", "");
            string missing = "{\"scenes\":[{\"path\":\"" + folder + "/Missing.unity\",\"loaded\":true,\"active\":true}]}";
            try
            {
                SessionState.SetString(SessionPrefix + "scenes", missing);
                SessionState.SetBool(SessionPrefix + "active", true);
                Assert.Throws<InvalidOperationException>(InvokeRestore);
                CollectionAssert.AreEqual(previous.Select(s => s.path).ToArray(), EditorSceneManager.GetSceneManagerSetup().Select(s => s.path).ToArray());
                CollectionAssert.AreEqual(previous.Select(s => s.isActive).ToArray(), EditorSceneManager.GetSceneManagerSetup().Select(s => s.isActive).ToArray());
                Assert.AreEqual(missing, SessionState.GetString(SessionPrefix + "scenes", ""));
                Assert.IsTrue(SessionState.GetBool(SessionPrefix + "active", false));
            }
            finally
            {
                SessionState.SetString(SessionPrefix + "scenes", oldScenes);
                SessionState.EraseBool(SessionPrefix + "active");
            }
        }
        [Serializable] sealed class SavedSetup { public SavedScene[] scenes; }
        [Serializable] sealed class SavedScene { public string path; public bool loaded, active; }
        static void InvokeRestore()
        {
            try { typeof(WordboundCoursePlaytest).GetMethod("RestoreScenes", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null); }
            catch (TargetInvocationException error) { throw error.InnerException ?? error; }
        }
    }
}
