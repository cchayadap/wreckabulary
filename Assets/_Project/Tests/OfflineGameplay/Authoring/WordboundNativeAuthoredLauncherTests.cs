using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Wreckabulary.Online;
using Wreckabulary.Rules.Wordbound;
using Wreckabulary.Workshop;
using Object = UnityEngine.Object;

namespace Wreckabulary.OfflineGameplayTests
{
    public sealed class WordboundNativeAuthoredLauncherTests
    {
        const string Key = "Wordbound.NativeAuthoredLauncher.";
        const string FolderPrefix = "Assets/__WordboundSavedLauncher_";
        GameObject root;
        WordboundLaunchAssets launch;
        WordboundLocalMatchClient client;

        [UnityTest, Timeout(600000), Category("LocalWordboundGameplay")]
        public IEnumerator SavedInspectorCourseRulesProfilesAndEditedGeometryReachLocalPlayAndReturn()
        {
            if (Environment.GetEnvironmentVariable("WORDBOUND_LOCAL_GAMEPLAY") != "1")
                Assert.Ignore("Set WORDBOUND_LOCAL_GAMEPLAY=1 for the saved Inspector course launcher proof.");
            Assert.IsFalse(Application.isPlaying);
            Assert.AreEqual("", SessionState.GetString(Key + "folder", ""), "Recover the preceding fixture's temporary assets first.");
            string suffix = Guid.NewGuid().ToString("N");
            string folder = FolderPrefix + suffix;
            string courseId = "native-saved-" + suffix;
            SessionState.SetString(Key + "folder", folder);
            SessionState.SetString(Key + "course", courseId);
            AssetDatabase.CreateFolder("Assets", folder.Substring("Assets/".Length));
            var course = CreateSavedCourse(folder, courseId);
            string savedJson = course.DocumentJson;
            string savedInput = course.InputHash;
            Resources.UnloadAsset(course);
            course = AssetDatabase.LoadAssetAtPath<WordboundCourseAsset>(folder + "/Course.asset");
            Assert.AreEqual(savedJson, course.DocumentJson);
            Assert.AreEqual(savedInput, course.InputHash);
            SessionState.SetBool(Key + "play", true);
            yield return new EnterPlayMode();

            folder = SessionState.GetString(Key + "folder", "");
            course = AssetDatabase.LoadAssetAtPath<WordboundCourseAsset>(folder + "/Course.asset");
            Assert.IsNotNull(course);
            savedJson = course.DocumentJson;
            savedInput = course.InputHash;
            var installed = Resources.Load<WordboundLaunchAssets>(WordboundLaunchAssets.ResourcePath);
            Assert.IsNotNull(installed);
            launch = Object.Instantiate(installed);
            launch.WorkshopCourse = course;
            root = new GameObject("Wordbound saved Inspector launcher proof");
            var cameraRoot = new GameObject("Authored launch lobby camera");
            cameraRoot.transform.SetParent(root.transform, false);
            var lobbyCamera = cameraRoot.AddComponent<Camera>();
            var launcher = root.AddComponent<WordboundLauncher>();
            launcher.Assets = launch;
            launcher.LobbyCamera = lobbyCamera;
            yield return null;
            var menu = root.GetComponent<WordboundPartyMenu>();
            Assert.IsNotNull(menu);
            launcher.SendMessage("OpenWorkshop", SendMessageOptions.RequireReceiver);
            var workshop = root.GetComponent<WordboundWorkshop>();
            Assert.IsNotNull(workshop);
            Assert.IsFalse(menu.Visible);
            Assert.AreEqual(savedJson, workshop.History.Current.CourseJsonText,
                "The actual launcher must open the persisted course instead of replacing it with a built-in map.");
            Assert.AreEqual(course.Seed, workshop.History.Current.Seed);
            Assert.AreEqual(course.Catalogue().Hash, workshop.Catalogue.Hash);
            Assert.AreEqual(course.ModeId, workshop.PlayRequirements.ModeId);
            var history = workshop.History;
            string initial = history.Current.RecoveryJson;
            history.Execute("Edit the saved source's runtime draft", draft => {
                draft.Course.Title = "Edited saved Inspector course";
                draft.Seed = "fedcba9876543210";
                draft.SeedLocked = true;
                draft.Course.Zones.Single(z => z.Id == "FixedStool").FixedPosition = new CoursePoint(-4500, 0, 3000);
            });
            string edited = history.Current.RecoveryJson;
            workshop.RequestPlaytest();
            var preview = workshop.LastPreview;
            Assert.IsNotNull(preview);
            Assert.IsTrue(preview.Success, string.Join("\n", preview.Issues));
            Assert.AreEqual(new CoursePoint(-4500, 0, 3000), preview.Manifest.Placements.Single(p => p.ZoneId == "FixedStool").Position);
            var expected = WordboundWorkshopPlaytest.Create(workshop.LastPlaytestDraft, preview.Manifest,
                workshop.Catalogue, workshop.PlayRequirements, course.Items.text, course.RuleBook.text);
            yield return Until(() => {
                client = root.GetComponentInChildren<WordboundLocalMatchClient>();
                return client && client.Gameplay && client.Gameplay.Frame != null &&
                    client.Gameplay.CourseWorld != null && !client.Gameplay.IsSimulationPaused && !lobbyCamera.enabled;
            }, "Launch the exact edited saved course through its native local client", 60);
            Assert.AreEqual(expected.Course.Hash, client.Gameplay.CourseWorld.ManifestHash);
            Assert.AreEqual(expected.CatalogueHash, client.Gameplay.Frame.Binding.CatalogueHash,
                "The copied, distinct item/rule source bytes must reach gameplay rather than global defaults.");
            var view = client.GetComponent<WordboundPlayerView>();
            Assert.AreSame(course.CameraProfile, view.Profile);
            Assert.AreEqual(67, view.Camera.fieldOfView);
            var presentation = client.GetComponent<WordboundGameplayPresentation>();
            var resolver = (WordboundVisualResolver)typeof(WordboundGameplayPresentation)
                .GetField("resolver", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(presentation);
            Assert.AreSame(course.VisualProfile, resolver.Profile);
            Assert.AreEqual(savedJson, course.DocumentJson);
            Assert.AreEqual(savedInput, course.InputHash);
            var localCamera = view.Camera;
            yield return Complete(client.ReturnToWorkshop(), "Return from the exact saved-source playtest", 30);
            yield return null;
            Assert.IsTrue(client == null);
            Assert.IsTrue(localCamera == null);
            Assert.IsTrue(workshop.Visible && lobbyCamera.enabled);
            Assert.IsFalse(menu.Visible);
            Assert.AreSame(history, workshop.History);
            Assert.AreEqual(edited, history.Current.RecoveryJson);
            Assert.IsTrue(history.Undo()); Assert.AreEqual(initial, history.Current.RecoveryJson);
            Assert.IsTrue(history.Redo()); Assert.AreEqual(edited, history.Current.RecoveryJson);
            Assert.AreEqual(savedJson, course.DocumentJson, "The separate runtime draft must not overwrite the persisted asset.");
            Debug.Log("WORDBOUND_AUTHORED_LAUNCH_OBSERVED: saved/reloaded CourseAsset, explicit seed, distinct rules/items, camera/visual profiles, edited prop geometry, exact local manifest and awaited return with same undo history; cleanup pending.");
        }

        static WordboundCourseAsset CreateSavedCourse(string folder, string courseId)
        {
            foreach (string name in new[] { "items", "rules" })
            {
                string path = folder + "/" + name + ".json";
                Assert.IsTrue(AssetDatabase.CopyAsset("Assets/_Project/Data/Config/" + name + ".json", path));
                File.AppendAllText(path, "\n ");
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            }
            var camera = ScriptableObject.CreateInstance<WordboundCameraProfile>();
            camera.FieldOfView = 67;
            AssetDatabase.CreateAsset(camera, folder + "/Camera.asset");
            var visual = ScriptableObject.CreateInstance<WordboundVisualProfile>();
            AssetDatabase.CreateAsset(visual, folder + "/Visual.asset");
            var asset = ScriptableObject.CreateInstance<WordboundCourseAsset>();
            var document = CourseTemplates.FreestyleWorld();
            document.Id = courseId; document.Title = "Saved Inspector launcher course";
            document.Zones.RemoveAll(z => z.Id != "FixedStool");
            asset.Initialize(document, CourseTemplates.ProofCatalogue());
            asset.Seed = "02468ace13579bdf";
            asset.Items = AssetDatabase.LoadAssetAtPath<TextAsset>(folder + "/items.json");
            asset.RuleBook = AssetDatabase.LoadAssetAtPath<TextAsset>(folder + "/rules.json");
            asset.CameraProfile = camera; asset.VisualProfile = visual;
            Assert.AreNotEqual(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/_Project/Data/Config/items.json").text, asset.Items.text);
            Assert.AreNotEqual(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/_Project/Data/Config/rules.json").text, asset.RuleBook.text);
            AssetDatabase.CreateAsset(asset, folder + "/Course.asset");
            foreach (var saved in new Object[] { camera, visual, asset }) { EditorUtility.SetDirty(saved); AssetDatabase.SaveAssetIfDirty(saved); }
            _ = asset.CreateContent(asset.Resolve());
            return asset;
        }

        IEnumerator Until(Func<bool> ready, string label, double seconds)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + seconds;
            while (!ready() && Time.realtimeSinceStartupAsDouble < deadline)
            {
                if (client && client.Gameplay) Assert.AreEqual("", client.Gameplay.Fault);
                yield return null;
            }
            Assert.IsTrue(ready(), label + " exceeded its deadline.");
        }
        static IEnumerator Complete(Task task, string label, double seconds)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + seconds;
            while (!task.IsCompleted && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.IsTrue(task.IsCompleted, label + " exceeded its deadline.");
            Assert.IsFalse(task.IsCanceled, label + " was cancelled.");
            Assert.IsFalse(task.IsFaulted, label + " failed: " + task.Exception?.GetBaseException().Message);
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            int failures = 0;
            Task stop = null;
            try { if (client) stop = client.ReturnToWorkshop(); } catch { failures++; }
            if (stop != null)
            {
                double deadline = Time.realtimeSinceStartupAsDouble + 30;
                while (!stop.IsCompleted && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                if (!stop.IsCompleted || stop.IsCanceled) failures++;
                if (stop.IsFaulted) { _ = stop.Exception; failures++; }
            }
            if (root) Object.Destroy(root);
            if (launch) Object.Destroy(launch);
            if (Application.isPlaying) yield return null;
            if (Application.isPlaying && SessionState.GetBool(Key + "play", false)) yield return new ExitPlayMode();
            SessionState.EraseBool(Key + "play");
            string folder = SessionState.GetString(Key + "folder", "");
            string courseId = SessionState.GetString(Key + "course", "");
            if (folder.StartsWith(FolderPrefix, StringComparison.Ordinal) && folder.Length == FolderPrefix.Length + 32)
            {
                if (AssetDatabase.IsValidFolder(folder) && !AssetDatabase.DeleteAsset(folder)) failures++;
                if (courseId == "native-saved-" + folder.Substring(FolderPrefix.Length))
                    foreach (string suffix in new[] { ".autosave.json", ".autosave.json.bak" })
                    {
                        string path = Path.Combine(Application.persistentDataPath, "wordbound-workshop", courseId + suffix);
                        if (File.Exists(path)) File.Delete(path);
                    }
            }
            SessionState.EraseString(Key + "folder"); SessionState.EraseString(Key + "course");
            root = null; launch = null; client = null;
            Assert.AreEqual(0, failures, "The fixture must await shutdown and remove only its own temporary assets.");
        }
    }
}
