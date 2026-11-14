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
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Wreckabulary.Online;
using Wreckabulary.Rules.Wordbound;
using Wreckabulary.Workshop;
using Object = UnityEngine.Object;
using Assert = NUnit.Framework.Assert;

namespace Wreckabulary.OfflineGameplayTests
{
    public sealed class WordboundNativeSoloEntryTests
    {
        static readonly string[] PrefKeys = { MatchTally.CareerKey, LobbyMenu.OutfitKey, LobbyMenu.TurnHintKey };
        string[] prefs;
        float volume;
        int vsync;
        bool enteredPlay, sceneHooked;
        WordboundLocalMatchClient client;
        WordboundLaunchAssets sourceAssets, ownedAssets;
        string sourceMemory;
        Dictionary<string, string> sourceFiles;

        [UnityTest, Category("LocalWordboundEntry"), Timeout(240000)]
        public IEnumerator TimeRunStartsInstalledFlagshipOfflineAndReturnsToLobby() => Run("TimeRun");

        [UnityTest, Category("LocalWordboundEntry"), Timeout(240000)]
        public IEnumerator FreestyleStartsInstalledFlagshipOfflineAndReturnsToLobby() => Run("Freestyle");

        [UnityTest, Category("LocalWordboundEntry"), Timeout(240000)]
        public IEnumerator ParcelHallStartsItsOwnFurnishedCourseAndReturnsToItsSelection() =>
            Run("Freestyle", soloMap: WordboundSoloEntry.ParcelHallMap);

        [TestCase("TimeRun")]
        [TestCase("Freestyle")]
        public void ParcelHallIsAnExplicitDeterministicVariantOfTheInstalledCourse(string mode)
        {
            var data = Resources.Load<GameData>(GameData.ResourcePath);
            const string seed = "0000000000000001";
            var original = WordboundLauncher.CreateSoloContent(data.items.text, data.rules.text, mode, seed);
            var parcel = WordboundLauncher.CreateSoloContent(data.items.text, data.rules.text, mode, seed, WordboundSoloEntry.ParcelHallMap);
            var again = WordboundLauncher.CreateSoloContent(data.items.text, data.rules.text, mode, seed, WordboundSoloEntry.ParcelHallMap);
            Assert.AreEqual(WordboundFlagshipContent.CourseId, original.Course.CopyDocument().Id);
            Assert.AreEqual(WordboundArrivalVariant.CourseId, parcel.Course.CopyDocument().Id);
            Assert.AreNotEqual(original.Course.SourceHash, parcel.Course.SourceHash);
            Assert.AreEqual(parcel.Course.Hash, again.Course.Hash);
            Assert.AreEqual(original.Mode.Hash, parcel.Mode.Hash);
            Assert.AreEqual(18, parcel.Course.CopyDocument().Zones.Count(zone => zone.Id.StartsWith("Arrival_", StringComparison.Ordinal)));
            Assert.Throws<ArgumentException>(() => WordboundLauncher.CreateSoloContent(data.items.text, data.rules.text, mode, seed, "unknown"));
        }

        [UnityTest, Category("LocalWordboundEntry"), Timeout(180000)]
        public IEnumerator MissingLocalBodyShowsSafeReturnWithoutLeakingAClient() => Run("TimeRun", true);

        IEnumerator Run(string mode, bool missingBody = false, string soloMap = WordboundSoloEntry.LostAddressMap)
        {
            if (Environment.GetEnvironmentVariable("WORDBOUND_LOCAL_ENTRY") != "1")
                Assert.Ignore("Set WORDBOUND_LOCAL_ENTRY=1 for actual offline flagship launch/return in both modes.");
            Assert.IsFalse(Application.isPlaying);
            enteredPlay = true;
            yield return new EnterPlayMode();
            prefs = PrefKeys.Select(key => PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null).ToArray();
            volume = AudioListener.volume; vsync = QualitySettings.vSyncCount;
            Session.Clear();
            sourceAssets = Resources.Load<WordboundLaunchAssets>(WordboundLaunchAssets.ResourcePath);
            Assert.IsNotNull(sourceAssets);
            sourceMemory = EditorJsonUtility.ToJson(sourceAssets);
            sourceFiles = SourceFiles(sourceAssets);
            ownedAssets = Object.Instantiate(sourceAssets);
            ownedAssets.name = "Owned offline-only launch configuration";
            ownedAssets.PublicTrust = null;
            ownedAssets.ApprovedPublicCourses = null;
            ownedAssets.MapPoolVersion = "";
            ownedAssets.SnapshotChunk = default;
            if (missingBody) ownedAssets.PlayerBody = null;
            SceneManager.sceneLoaded += InstallOwnedOfflineConfiguration;
            sceneHooked = true;
            yield return SceneManager.LoadSceneAsync(Session.HubScene);
            yield return Until(() => LobbyMenu.Instance && LobbyMenu.Instance.Current == LobbyMenu.Home, "Hub ready", 30);
            var lobby = LobbyMenu.Instance;
            lobby.Choose(LobbyMenu.Practice, "Duos", "flat");
            lobby.Open(LobbyMenu.Play);
            lobby.Page<PlayPage>().WordboundSolo = true;
            WordboundSoloEntry.Select(mode);
            WordboundSoloEntry.SelectMap(soloMap);
            lobby.Open(LobbyMenu.Play);
            Assert.AreEqual(1, lobby.PartySize);
            lobby.OpenWordboundSolo(mode);
            if (missingBody)
            {
                WordboundLauncher failed = null;
                yield return Until(() => {
                    failed = Object.FindAnyObjectByType<WordboundLauncher>();
                    return failed && !string.IsNullOrEmpty(Read<string>(failed, "failure"));
                }, "Safe local startup failure", 30);
                Assert.AreEqual("Solo could not start. Return to the lobby and try again.", Read<string>(failed, "failure"));
                Assert.IsNull(Object.FindAnyObjectByType<WordboundLocalMatchClient>());
                Assert.IsNull(Object.FindAnyObjectByType<WordboundPartyMenu>());
                Assert.IsNull(Object.FindAnyObjectByType<WordboundWorkshop>());
                Assert.IsNull(WordboundSoloEntry.TakeRequest());
                failed.SendMessage("BackToLobby", SendMessageOptions.RequireReceiver);
                yield return Until(() => SceneManager.GetActiveScene().name == Session.HubScene &&
                    LobbyMenu.Instance && LobbyMenu.Instance.Current == LobbyMenu.Home, "Failure-panel lobby return", 30);
                Assert.AreEqual("Duos", LobbyMenu.Instance.Mode); Assert.AreEqual("flat", LobbyMenu.Instance.Map);
                Assert.AreEqual(1, LobbyMenu.Instance.PartySize);
                Debug.Log("WORDBOUND_SOLO_ENTRY: missingBody=safeFailure; client=absent; return=Hub; onlineConfig=absent");
                yield break;
            }
            yield return Until(() => {
                client = Object.FindAnyObjectByType<WordboundLocalMatchClient>();
                return client && client.Gameplay && client.Gameplay.IsPresentationReady && !client.Gameplay.IsSimulationPaused;
            }, "Actual solo flagship startup", 90);
            var launcher = Object.FindAnyObjectByType<WordboundLauncher>();
            Assert.IsNotNull(launcher);
            Assert.AreSame(ownedAssets, launcher.Assets);
            Assert.IsNull(launcher.Assets.PublicTrust);
            Assert.IsFalse(launcher.Assets.SnapshotChunk.IsValid);
            Assert.IsNull(Object.FindAnyObjectByType<WordboundPartyMenu>(), "Offline entry must not create online configuration or a coordinator.");
            Assert.IsNull(Object.FindAnyObjectByType<WordboundWorkshop>(), "Normal play must not manufacture an editable draft.");
            Assert.IsNull(Object.FindAnyObjectByType<WordboundMatchClient>());
            Assert.IsNull(WordboundSoloEntry.TakeRequest());
            Assert.AreEqual("Duos", Session.LobbyMode); Assert.AreEqual(LobbyMenu.Practice, Session.LobbyQueue);
            Assert.AreEqual("flat", Session.MapId); Assert.AreEqual(1, Session.Bindings.Count);
            var controls = Session.Bindings[0];
            var gameplay = client.Gameplay;
            var hud = client.GetComponent<WordboundMatchHud>();
            var course = Read<ResolvedCourse>(hud, "course");
            Assert.AreEqual("Return to lobby", Read<string>(hud, "returnLabel"));
            Assert.AreSame(sourceAssets.UiTheme, hud.Theme);
            Assert.AreSame(sourceAssets.StoryProfile, hud.StoryProfile);
            Assert.AreEqual(soloMap == WordboundSoloEntry.ParcelHallMap ? WordboundArrivalVariant.CourseId : WordboundFlagshipContent.CourseId, course.CopyDocument().Id);
            var data = Resources.Load<GameData>(GameData.ResourcePath);
            var expected = WordboundLauncher.CreateSoloContent(data.items.text, data.rules.text, mode, course.Seed, soloMap);
            Assert.AreEqual(expected.Course.SourceHash, gameplay.Frame.Binding.CourseHash);
            Assert.AreEqual(expected.Course.Hash, gameplay.Frame.Binding.ManifestHash);
            Assert.AreEqual(expected.CatalogueHash, gameplay.Frame.Binding.CatalogueHash);
            Assert.AreEqual(expected.Mode.Hash, gameplay.Frame.Binding.ModeHash);
            Assert.AreEqual(expected.Course.Hash, gameplay.CourseWorld.ManifestHash);
            Assert.AreEqual(72, expected.Plan.Sites.Count);
            Assert.AreEqual(mode == "Freestyle" ? 0 : 3, course.Laps.Count);
            Assert.AreEqual(mode, gameplay.Authority.Match.Mode.Id);
            Assert.AreEqual(1, gameplay.Frame.Progress.Players.Count);
            Assert.IsTrue(gameplay.Frame.Binding.Seats.All(seat => seat.PlayerId == WordboundLocalMatchClient.LocalActor));
            Assert.IsNull(gameplay.Frame.Progress.Result);
            var session = Read<WordboundLocalSession>(client, "session");
            Assert.IsTrue(session.GameplayReady);
            Assert.AreEqual(GameMode.Single, session.Runner.GameMode);
            var view = client.GetComponent<WordboundPlayerView>();
            Assert.IsNotNull(view); Assert.IsNotNull(view.Player); Assert.IsNotNull(view.Camera);
            var camera = view.Camera;
            var world = gameplay.CourseWorld;
            Assert.IsNotNull(client.GetComponent<WordboundPuzzleGuide>());
            Assert.IsNotNull(client.GetComponent<WordboundGameplayPresentation>());
            long tick = gameplay.Frame.Progress.Tick;
            yield return Until(() => gameplay.Frame.Progress.Tick > tick + 5, "Real local simulation advances", 15);
            if (mode == "TimeRun")
            {
                var fail = typeof(WordboundGameplayController).GetMethod("Fail", BindingFlags.Instance | BindingFlags.NonPublic,
                    null, new[] { typeof(Exception) }, null);
                Assert.IsNotNull(fail);
                fail.Invoke(gameplay, new object[] { new InvalidOperationException("synthetic private fault detail") });
                Assert.IsNotEmpty(gameplay.Fault);
                Assert.IsTrue(view.MenuOpen);
                string notice = Read<string>(hud, "persistentNotice");
                Assert.AreEqual("The session stopped. Choose Return to lobby to continue.", notice);
                StringAssert.DoesNotContain("synthetic private fault detail", notice);
                Assert.AreEqual("Return to lobby", Read<string>(hud, "returnLabel"));
                yield return Await(gameplay.Stop(), "Fault shutdown before the ordinary lobby return", 30);
            }
            Task returning = client.ReturnToMenu();
            yield return Await(returning, "Confirmed local stop and Hub return", 30);
            yield return Until(() => SceneManager.GetActiveScene().name == Session.HubScene &&
                LobbyMenu.Instance && LobbyMenu.Instance.Current == LobbyMenu.Play, "Solo page restored in Hub", 30);
            yield return null;
            Assert.IsFalse(client); Assert.IsFalse(camera); Assert.IsFalse(world);
            Assert.IsNull(Object.FindAnyObjectByType<WordboundLocalMatchClient>());
            Assert.IsNull(Object.FindAnyObjectByType<WordboundWorkshop>());
            var returned = LobbyMenu.Instance;
            Assert.IsTrue(returned.Page<PlayPage>().WordboundSolo);
            Assert.AreEqual(mode, WordboundSoloEntry.SelectedMode);
            Assert.AreEqual(soloMap, WordboundSoloEntry.SelectedMap);
            Assert.AreEqual("Duos", returned.Mode); Assert.AreEqual("flat", returned.Map);
            Assert.AreEqual(LobbyMenu.Practice, returned.Queue); Assert.AreEqual(1, returned.PartySize);
            Assert.AreSame(controls, Session.Bindings.Single());
            Assert.IsNotNull(returned.Stage);
            Assert.IsTrue(Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Any(item => item.enabled && item.gameObject.activeInHierarchy));
            Assert.IsFalse(WordboundSoloEntry.TakeReturn());
            Assert.AreEqual(sourceMemory, EditorJsonUtility.ToJson(sourceAssets));
            foreach (var file in sourceFiles) Assert.AreEqual(file.Value, Hash(file.Key), file.Key);
            Debug.Log("WORDBOUND_SOLO_ENTRY: mode=" + mode + "; source=" + expected.Course.SourceHash +
                "; manifest=" + expected.Course.Hash + "; plan=" + expected.Plan.Hash +
                "; runner=Single; seats=1; onlineConfig=absent; return=Hub/solo; preservedFiles=" + sourceFiles.Count);
        }

        void InstallOwnedOfflineConfiguration(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != Session.WordboundScene) return;
            var launcher = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<WordboundLauncher>(true)).Single();
            launcher.Assets = ownedAssets;
        }

        static T Read<T>(object owner, string field)
        {
            var member = owner.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(member, field);
            return (T)member.GetValue(owner);
        }

        static Dictionary<string, string> SourceFiles(WordboundLaunchAssets source)
        {
            var paths = AssetDatabase.GetDependencies(AssetDatabase.GetAssetPath(source), true)
                .Concat(new[] { WordboundLaunchAssets.LobbyPath, WordboundLaunchAssets.GameplayPath,
                    "Assets/_Project/Data/Config/items.json", "Assets/_Project/Data/Config/rules.json" })
                .SelectMany(path => new[] { path, path + ".meta" }).Where(File.Exists).Distinct();
            return paths.ToDictionary(path => path, Hash, StringComparer.Ordinal);
        }

        static string Hash(string path)
        {
            using var algorithm = SHA256.Create();
            return BitConverter.ToString(algorithm.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant();
        }

        static IEnumerator Until(Func<bool> ready, string label, double seconds)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + seconds;
            while (!ready())
            {
                Assert.Less(Time.realtimeSinceStartupAsDouble, deadline, label);
                yield return null;
            }
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
            try { if (client) stop = client.ReturnToMenu(); }
            catch (Exception error) { errors.Add("stop start: " + error.GetType().Name); }
            if (stop != null)
            {
                double deadline = Time.realtimeSinceStartupAsDouble + 30;
                while (!stop.IsCompleted && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                if (!stop.IsCompleted || stop.IsCanceled)
                {
                    errors.Add("local shutdown incomplete");
                    if (!stop.IsCompleted) _ = stop.ContinueWith(done => { _ = done.Exception; }, CancellationToken.None,
                        TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                }
                else if (stop.IsFaulted) errors.Add("local shutdown: " + stop.Exception.GetBaseException().GetType().Name);
            }
            if (ownedAssets) Object.Destroy(ownedAssets);
            if (Application.isPlaying) yield return null;
            if (ownedAssets) errors.Add("owned launch configuration retained");
            if (sourceAssets && sourceMemory != EditorJsonUtility.ToJson(sourceAssets)) errors.Add("source launch memory changed");
            if (sourceFiles != null)
                foreach (var file in sourceFiles)
                    if (!File.Exists(file.Key) || Hash(file.Key) != file.Value) errors.Add("source file changed: " + file.Key);
            if (prefs != null)
            {
                for (int index = 0; index < PrefKeys.Length; index++)
                    if (prefs[index] == null) PlayerPrefs.DeleteKey(PrefKeys[index]); else PlayerPrefs.SetString(PrefKeys[index], prefs[index]);
                PlayerPrefs.Save();
                AudioListener.volume = volume; QualitySettings.vSyncCount = vsync;
            }
            Session.Clear();
            if (Application.isPlaying && enteredPlay) yield return new ExitPlayMode();
            enteredPlay = false;
            Debug.Log("WORDBOUND_SOLO_ENTRY_CLEANUP: failures=" + errors.Count);
            Assert.IsEmpty(errors, string.Join("; ", errors));
        }
    }
}
