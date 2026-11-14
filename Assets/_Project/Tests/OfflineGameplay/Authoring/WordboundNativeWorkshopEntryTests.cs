using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Fusion;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Wreckabulary.Online;
using Wreckabulary.Rules.Wordbound;
using Wreckabulary.Workshop;
using Object = UnityEngine.Object;
using Assert = NUnit.Framework.Assert;

namespace Wreckabulary.OfflineGameplayTests
{
    public sealed class WordboundNativeWorkshopEntryTests
    {
        static readonly string[] PrefKeys = { MatchTally.CareerKey, LobbyMenu.OutfitKey, LobbyMenu.TurnHintKey };
        string[] prefs;
        float volume;
        int vsync;
        bool enteredPlay, sceneHooked;
        WordboundLocalMatchClient client;
        WordboundLaunchAssets sourceAssets, ownedAssets;
        WordboundCourseAsset ownedCourse;
        string sourceMemory, folder, recoveredJson;
        Dictionary<string, string> sourceFiles;
        readonly Dictionary<Object, string> authoredMemory = new();
        readonly Dictionary<Object, bool> authoredDirty = new();
        KeyboardBinding second;
        InputBinding[] seated;

        [SetUp]
        public void ResetCaseState()
        {
            Assert.IsFalse(enteredPlay || sceneHooked || client || ownedAssets || ownedCourse,
                "The preceding case must finish its owned lifecycle before the next case resets state.");
            authoredMemory.Clear(); authoredDirty.Clear();
            prefs = null; volume = 0; vsync = 0;
            enteredPlay = sceneHooked = false;
            client = null; sourceAssets = ownedAssets = null; ownedCourse = null;
            sourceMemory = folder = recoveredJson = null; sourceFiles = null;
            second = null; seated = null;
        }

        [UnityTest, Category("LocalWordboundWorkshopEntry"), Timeout(240000)]
        public IEnumerator DirectCreatorPlaysExactEditedCourseAndReturnsToSavedCreateChoice() => Run(0);

        [UnityTest, Category("LocalWordboundWorkshopEntry"), Timeout(180000)]
        public IEnumerator ExistingRecoveryRequiresChoiceAndBackPreservesItsBytes() => Run(1);

        [UnityTest, Category("LocalWordboundWorkshopEntry"), Timeout(180000)]
        public IEnumerator InvalidAssignedCourseShowsSafeCreatorReturnWithoutClientOrParty() => Run(2);

        IEnumerator Run(int variant)
        {
            if (Environment.GetEnvironmentVariable("WORDBOUND_LOCAL_WORKSHOP_ENTRY") != "1")
                Assert.Ignore("Set WORDBOUND_LOCAL_WORKSHOP_ENTRY=1 for actual offline creator launch/playtest/return.");
            Assert.IsFalse(Application.isPlaying);
            enteredPlay = true;
            yield return new EnterPlayMode();
            prefs = PrefKeys.Select(key => PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null).ToArray();
            volume = AudioListener.volume; vsync = QualitySettings.vSyncCount;
            Session.Clear();
            sourceAssets = Resources.Load<WordboundLaunchAssets>(WordboundLaunchAssets.ResourcePath);
            Assert.IsNotNull(sourceAssets); Assert.IsNotNull(sourceAssets.WorkshopCourse);
            sourceMemory = EditorJsonUtility.ToJson(sourceAssets);
            sourceFiles = SourceFiles(sourceAssets);
            foreach (var asset in new Object[] { sourceAssets.WorkshopCourse, sourceAssets.CameraProfile,
                sourceAssets.VisualProfile, sourceAssets.UiTheme, sourceAssets.StoryProfile,
                sourceAssets.WorkshopCourse.CameraProfile, sourceAssets.WorkshopCourse.VisualProfile })
                if (asset && !authoredMemory.ContainsKey(asset)) {
                    authoredMemory.Add(asset, EditorJsonUtility.ToJson(asset));
                    authoredDirty.Add(asset, EditorUtility.IsDirty(asset));
                }
            ownedAssets = Object.Instantiate(sourceAssets);
            ownedAssets.PublicTrust = null; ownedAssets.ApprovedPublicCourses = null;
            ownedAssets.MapPoolVersion = ""; ownedAssets.SnapshotChunk = default;
            if (variant == 2)
            {
                ownedCourse = Object.Instantiate(sourceAssets.WorkshopCourse);
                ownedCourse.Items = null; ownedAssets.WorkshopCourse = ownedCourse;
            }
            folder = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "wb-local-editor", Guid.NewGuid().ToString("N")));
            if (variant == 1)
            {
                var draft = new WorkshopDraft { Course = sourceAssets.WorkshopCourse.ReadDocument(),
                    Seed = "0314159265358979", SeedLocked = true,
                    ActiveLayer = sourceAssets.WorkshopCourse.ReadDocument().Layers[0].Id };
                draft.Course.Title = "Owned recovered creator draft";
                recoveredJson = new WorkshopSnapshot(draft).RecoveryJson;
            }
            SceneManager.sceneLoaded += InstallOwnedOfflineConfiguration; sceneHooked = true;
            yield return SceneManager.LoadSceneAsync(Session.HubScene);
            yield return Until(() => LobbyMenu.Instance && LobbyMenu.Instance.Current == LobbyMenu.Home &&
                GameHud.Active && !GameHud.Active.StateController.IsTransitioning, "Hub ready", 30);
            var lobby = LobbyMenu.Instance;
            lobby.Choose(LobbyMenu.Workshop, LobbyMenu.WorkshopMode, "courtyard");
            Assert.AreEqual("courtyard", lobby.Map, "Use a supported non-default home-builder map before entering Wordbound.");
            second = new KeyboardBinding(KeyboardBinding.Side.Right); lobby.Join(second);
            lobby.Open(LobbyMenu.Play);
            Submit(lobby, "Create Wordbound"); yield return null;
            Assert.AreEqual(2, lobby.PartySize);
            Submit(lobby, "Workshop OPEN");
            seated = Session.Bindings.ToArray();
            Assert.AreEqual(2, seated.Length); Assert.AreSame(second, seated[1]);
            WordboundLauncher launcher = null;
            yield return Until(() => {
                launcher = Object.FindAnyObjectByType<WordboundLauncher>();
                return launcher && (launcher.GetComponent<WordboundWorkshop>() || !string.IsNullOrEmpty(Read<string>(launcher, "failure")));
            }, "Local editor or safe error", 30);
            Assert.IsNull(launcher.GetComponent<WordboundPartyMenu>());
            Assert.IsNull(Object.FindAnyObjectByType<WordboundMatchClient>());
            Assert.IsFalse(WordboundWorkshopEntry.TakeRequest(), "The scene request must be consumed once.");
            if (variant == 2)
            {
                Assert.AreEqual("Workshop could not open. Return to the lobby and try again.", Read<string>(launcher, "failure"));
                Assert.IsNull(launcher.GetComponent<WordboundWorkshop>());
                Assert.IsNull(Object.FindAnyObjectByType<WordboundLocalMatchClient>());
                launcher.SendMessage("BackToLobby", SendMessageOptions.RequireReceiver);
                yield return CheckReturned();
                yield break;
            }
            Assert.IsTrue(string.IsNullOrEmpty(Read<string>(launcher, "failure")));
            var workshop = launcher.GetComponent<WordboundWorkshop>();
            Assert.IsTrue(workshop.Visible); Assert.IsTrue(workshop.UsesAssignedCourse);
            Assert.AreEqual("Back to lobby", workshop.CloseLabel);
            Assert.IsFalse(workshop.CanStartOnline());
            Assert.AreEqual(sourceAssets.WorkshopCourse.DocumentJson, workshop.History.Current.CourseJsonText);
            Assert.AreEqual(sourceAssets.WorkshopCourse.Seed, workshop.History.Current.Seed);
            Assert.AreEqual(sourceAssets.WorkshopCourse.Catalogue().Hash, workshop.Catalogue.Hash);
            string primary = Path.Combine(folder, workshop.History.Current.Course().Id + ".autosave.json");
            if (variant == 1)
            {
                Assert.IsTrue(workshop.RecoveryChoicePending);
                Assert.AreEqual(recoveredJson, File.ReadAllText(primary));
                workshop.SendMessage("QueueRecoveryChoice", 3, SendMessageOptions.RequireReceiver);
                Assert.AreEqual(3, Read<int>(workshop, "recoveryDecision"));
                workshop.SendMessage("ProcessRecoveryChoice", false, SendMessageOptions.RequireReceiver);
                Assert.AreEqual(0, Read<int>(workshop, "recoveryDecision"));
                Assert.IsTrue(workshop.Visible && workshop.RecoveryChoicePending);
                Assert.AreEqual(Session.WordboundScene, SceneManager.GetActiveScene().name);
                Assert.AreEqual(recoveredJson, File.ReadAllText(primary));
                workshop.SendMessage("QueueRecoveryChoice", 3, SendMessageOptions.RequireReceiver);
                workshop.SendMessage("ProcessRecoveryChoice", true, SendMessageOptions.RequireReceiver);
                yield return CheckReturned();
                Assert.AreEqual(recoveredJson, File.ReadAllText(primary), "Back must not overwrite the existing recovery choice.");
                Debug.Log("WORDBOUND_LOCAL_CREATOR_RECOVERY: scripted unfocused decision discarded; focused production decision saved no bytes and returned through actual Hub/Create callback; not physical focus or input acceptance.");
                yield break;
            }
            Assert.IsFalse(workshop.RecoveryChoicePending);
            var history = workshop.History;
            string initial = history.Current.RecoveryJson;
            history.Execute("Owned creator edit", draft => {
                draft.Course.Title = "Owned offline creator draft";
                draft.Seed = "0314159265358979"; draft.SeedLocked = true;
            });
            string edited = history.Current.RecoveryJson;
            workshop.RequestPlaytest();
            Assert.IsNotNull(workshop.LastPreview);
            Assert.IsTrue(workshop.LastPreview.Success, string.Join("\n", workshop.LastPreview.Issues.Select(issue => issue.Message)));
            string manifest = workshop.LastPreview.Manifest.Hash;
            var lobbyCamera = launcher.LobbyCamera;
            yield return Until(() => {
                client = launcher.GetComponentInChildren<WordboundLocalMatchClient>();
                return client && client.Gameplay && client.Gameplay.Frame != null && client.Gameplay.CourseWorld &&
                    !client.Gameplay.IsSimulationPaused && lobbyCamera && !lobbyCamera.enabled;
            }, "Exact edited local course running", 60);
            Assert.AreEqual(1, client.Gameplay.Frame.Progress.Players.Count);
            Assert.AreEqual(manifest, client.Gameplay.CourseWorld.ManifestHash);
            Assert.AreSame(history.Current, workshop.LastPlaytestDraft);
            Assert.AreEqual(edited, history.Current.RecoveryJson);
            var session = Read<WordboundLocalSession>(client, "session");
            Assert.IsNotNull(session); Assert.AreEqual(GameMode.Single, session.Runner.GameMode);
            var view = client.GetComponent<WordboundPlayerView>();
            Assert.IsNotNull(view); Assert.IsNotNull(view.Camera); Assert.IsNotNull(view.Player);
            Assert.IsNotNull(client.GetComponent<WordboundMatchHud>());
            var camera = view.Camera; var world = client.Gameplay.CourseWorld;
            long tick = client.Gameplay.Frame.Progress.Tick;
            yield return Until(() => client.Gameplay.Frame.Progress.Tick > tick + 2, "Real local ticks", 15);
            Assert.IsNull(client.Gameplay.Frame.Progress.Result);
            CollectionAssert.AreEqual(seated, Session.Bindings);
            yield return Await(client.ReturnToWorkshop(), "Return to same editor", 30);
            yield return null;
            Assert.IsFalse(client); Assert.IsFalse(camera); Assert.IsFalse(world);
            Assert.IsTrue(workshop.Visible); Assert.IsTrue(lobbyCamera.enabled);
            Assert.AreSame(history, workshop.History); Assert.AreEqual(edited, history.Current.RecoveryJson);
            Assert.IsTrue(history.Undo()); Assert.AreEqual(initial, history.Current.RecoveryJson);
            Assert.IsTrue(history.Redo()); Assert.AreEqual(edited, history.Current.RecoveryJson);
            workshop.RequestClose();
            yield return CheckReturned();
            Assert.AreEqual(edited, File.ReadAllText(primary), "Actual Close must save the exact edited recovery document.");
            Assert.IsFalse(workshop);
            Debug.Log("WORDBOUND_LOCAL_CREATOR: installed assigned course; online configuration absent; exact edited Single playtest; same Undo/Redo history; saved Close to Hub/Create; two original local seats retained; no full-finish or physical-input claim.");
        }

        IEnumerator CheckReturned()
        {
            yield return Until(() => SceneManager.GetActiveScene().name == Session.HubScene &&
                LobbyMenu.Instance && LobbyMenu.Instance.Current == LobbyMenu.Play, "Creator selection restored", 30);
            yield return null;
            var lobby = LobbyMenu.Instance;
            Assert.IsTrue(lobby.Page<PlayPage>().WordboundMaps);
            Assert.AreEqual(LobbyMenu.Workshop, lobby.Queue); Assert.AreEqual(LobbyMenu.WorkshopMode, lobby.Mode);
            Assert.AreEqual("courtyard", lobby.Map); Assert.AreEqual(2, lobby.PartySize);
            Assert.AreSame(second, lobby.Party.Single()); CollectionAssert.AreEqual(seated, Session.Bindings);
            Assert.IsNull(Object.FindAnyObjectByType<WordboundWorkshop>());
            Assert.IsNull(Object.FindAnyObjectByType<WordboundLocalMatchClient>());
            Assert.IsFalse(WordboundWorkshopEntry.TakeReturn());
        }

        void InstallOwnedOfflineConfiguration(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != Session.WordboundScene) return;
            var launcher = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<WordboundLauncher>(true)).Single();
            launcher.Assets = ownedAssets;
            launcher.ConfigureQaWorkshopStorage(folder);
            if (recoveredJson != null)
            {
                Directory.CreateDirectory(folder);
                File.WriteAllText(Path.Combine(folder, sourceAssets.WorkshopCourse.ReadDocument().Id + ".autosave.json"), recoveredJson);
            }
        }

        static void Submit(LobbyMenu lobby, string name)
        {
            var button = lobby.Page<PlayPage>().Root.GetComponentsInChildren<Button>().Single(value => value.name == name);
            Assert.IsTrue(button.IsInteractable());
            EventSystem.current.SetSelectedGameObject(button.gameObject);
            ExecuteEvents.Execute(button.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
        }
        static T Read<T>(object owner, string field) => (T)owner.GetType().GetField(field,
            BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
        static Dictionary<string, string> SourceFiles(WordboundLaunchAssets source) => AssetDatabase.GetDependencies(AssetDatabase.GetAssetPath(source), true)
            .Concat(new[] { WordboundLaunchAssets.LobbyPath, WordboundLaunchAssets.GameplayPath,
                "Assets/_Project/Data/Config/items.json", "Assets/_Project/Data/Config/rules.json" })
            .SelectMany(path => new[] { path, path + ".meta" }).Where(File.Exists).Distinct().ToDictionary(path => path, Hash, StringComparer.Ordinal);
        static string Hash(string path) { using var sha = SHA256.Create(); return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant(); }
        static IEnumerator Until(Func<bool> ready, string label, double seconds)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + seconds;
            while (!ready()) { Assert.Less(Time.realtimeSinceStartupAsDouble, deadline, label); yield return null; }
        }
        static IEnumerator Await(Task task, string label, double seconds)
        {
            yield return Until(() => task.IsCompleted, label, seconds);
            if (task.IsFaulted) throw task.Exception.GetBaseException();
            Assert.IsFalse(task.IsCanceled, label);
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (!enteredPlay) yield break;
            var errors = new List<string>();
            if (sceneHooked) SceneManager.sceneLoaded -= InstallOwnedOfflineConfiguration;
            sceneHooked = false;
            Task stop = null;
            try { if (client) stop = client.ReturnToWorkshop(); }
            catch (Exception error) { errors.Add("stop start: " + error.GetType().Name); }
            if (stop != null)
            {
                double deadline = Time.realtimeSinceStartupAsDouble + 30;
                while (!stop.IsCompleted && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                if (!stop.IsCompleted || stop.IsCanceled) {
                    errors.Add("local shutdown incomplete");
                    if (!stop.IsCompleted) _ = stop.ContinueWith(done => { _ = done.Exception; }, CancellationToken.None,
                        TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                }
                else if (stop.IsFaulted) errors.Add("local shutdown: " + stop.Exception.GetBaseException().GetType().Name);
            }
            if (ownedAssets) Object.Destroy(ownedAssets);
            if (ownedCourse) Object.Destroy(ownedCourse);
            if (Application.isPlaying) yield return null;
            if (ownedAssets || ownedCourse) errors.Add("owned configuration retained");
            if (sourceAssets && sourceMemory != EditorJsonUtility.ToJson(sourceAssets)) errors.Add("source launch memory changed");
            foreach (var pair in authoredMemory)
                if (!pair.Key || EditorJsonUtility.ToJson(pair.Key) != pair.Value || EditorUtility.IsDirty(pair.Key) != authoredDirty[pair.Key])
                    errors.Add("authored profile memory or dirty state changed");
            if (sourceFiles != null) foreach (var file in sourceFiles)
                if (!File.Exists(file.Key) || Hash(file.Key) != file.Value) errors.Add("source file changed: " + file.Key);
            if (prefs != null) {
                for (int index = 0; index < PrefKeys.Length; index++)
                    if (prefs[index] == null) PlayerPrefs.DeleteKey(PrefKeys[index]); else PlayerPrefs.SetString(PrefKeys[index], prefs[index]);
                PlayerPrefs.Save(); AudioListener.volume = volume; QualitySettings.vSyncCount = vsync;
            }
            Session.Clear();
            if (Application.isPlaying && enteredPlay) yield return new ExitPlayMode();
            enteredPlay = false;
            Debug.Log("WORDBOUND_LOCAL_CREATOR_CLEANUP: failures=" + errors.Count);
            Assert.IsEmpty(errors, string.Join("; ", errors));
        }
    }
}
