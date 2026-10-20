using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wreckabulary.Online;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;
using Wreckabulary.Workshop;

namespace Wreckabulary.OfflineGameplayTests
{
    public sealed class WordboundNativeLauncherTests
    {
        GameObject root;
        WordboundLocalMatchClient client;
        bool enteredPlay;

        [UnityTest, Category("LocalWordboundGameplay")]
        public IEnumerator LauncherReturnsFromExactFlagshipPlaytestWithUndoHistory()
        {
            if (Environment.GetEnvironmentVariable("WORDBOUND_LOCAL_GAMEPLAY") != "1")
                Assert.Ignore("Set WORDBOUND_LOCAL_GAMEPLAY=1 for the native launcher composition proof.");
            Assert.IsFalse(Application.isPlaying);
            enteredPlay = true;
            yield return new EnterPlayMode();
            root = new GameObject("WordboundNativeLauncher");
            var cameraRoot = new GameObject("Local proof lobby camera");
            cameraRoot.transform.SetParent(root.transform, false);
            var lobbyCamera = cameraRoot.AddComponent<Camera>();
            var launcher = root.AddComponent<WordboundLauncher>();
            launcher.LobbyCamera = lobbyCamera;
            yield return null;
            var menu = root.GetComponent<WordboundPartyMenu>();
            Assert.IsNotNull(menu);
            Assert.IsNotNull(menu.Coordinator);
            launcher.SendMessage("OpenWorkshop", SendMessageOptions.RequireReceiver);
            var workshop = root.GetComponent<WordboundWorkshop>();
            Assert.IsNotNull(workshop);
            Assert.IsFalse(menu.Visible);
            var data = Resources.Load<GameData>(GameData.ResourcePath);
            string folder = Path.GetFullPath(Path.Combine("Library", "WordboundLauncherProof", Guid.NewGuid().ToString("N")));
            workshop.OpenFlagship(WordboundFlagshipContent.Create(ItemCatalogue.FromJson(data.items.text)), folder);
            var history = workshop.History;
            string initial = history.Current.RecoveryJson;
            history.Execute("Native launch edit", draft => {
                draft.Course.Title = "Native edited flagship";
                draft.Seed = "0314159265358979";
                draft.SeedLocked = true;
            });
            string edited = history.Current.RecoveryJson;
            Assert.IsTrue(history.CanUndo);
            workshop.RequestPlaytest();
            var preview = workshop.LastPreview;
            Assert.IsNotNull(preview);
            Assert.IsTrue(preview.Success, string.Join("\n", preview.Issues.Select(issue => issue.Message)));
            yield return Until(() => {
                client = root.GetComponentInChildren<WordboundLocalMatchClient>();
                return client && client.Gameplay && client.Gameplay.Frame != null &&
                    client.Gameplay.CourseWorld != null && !client.Gameplay.IsSimulationPaused &&
                    !lobbyCamera.enabled;
            }, "Start the flagship through the actual launcher", 60);
            Assert.IsFalse(workshop.Visible);
            Assert.IsFalse(lobbyCamera.enabled);
            Assert.AreSame(history.Current, workshop.LastPlaytestDraft);
            Assert.AreEqual(preview.Manifest.Hash, client.Gameplay.CourseWorld.ManifestHash);
            Assert.AreEqual(edited, history.Current.RecoveryJson);
            var view = client.GetComponent<WordboundPlayerView>();
            Assert.IsNotNull(view);
            Assert.IsNotNull(view.Camera);
            Assert.IsNotNull(view.Player);
            Assert.IsNotNull(client.GetComponent<WordboundMatchHud>());
            var guide = client.GetComponent<WordboundPuzzleGuide>();
            Assert.IsNotNull(guide);
            yield return Until(() => guide.MarkerCount > 0, "Show the current puzzle markers", 10);
            Assert.AreEqual("", guide.Fault);
            var ownedCamera = view.Camera;
            yield return Complete(client.ReturnToWorkshop(), "Return through the real local client", 30);
            yield return null;
            Assert.IsTrue(client == null);
            Assert.IsTrue(ownedCamera == null);
            Assert.IsTrue(guide == null);
            Assert.IsTrue(workshop.Visible);
            Assert.IsFalse(menu.Visible);
            Assert.IsTrue(lobbyCamera.enabled);
            Assert.AreSame(history, workshop.History);
            Assert.AreEqual(edited, history.Current.RecoveryJson);
            Assert.IsTrue(history.Undo());
            Assert.AreEqual(initial, history.Current.RecoveryJson);
            Assert.IsTrue(history.Redo());
            Assert.AreEqual(edited, history.Current.RecoveryJson);
            Debug.Log("WORDBOUND_LAUNCHER_OBSERVED: exact edited flagship, real local world/view/HUD, awaited return, restored same undo/redo history; cleanup pending.");
        }

        IEnumerator Until(Func<bool> complete, string operation, double seconds)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + seconds;
            while (!complete() && Time.realtimeSinceStartupAsDouble < deadline)
            {
                if (client && client.Gameplay) Assert.AreEqual("", client.Gameplay.Fault);
                yield return null;
            }
            Assert.IsTrue(complete(), operation + " exceeded its deadline.");
        }

        static IEnumerator Complete(Task task, string operation, double seconds)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + seconds;
            while (!task.IsCompleted && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.IsTrue(task.IsCompleted, operation + " exceeded its deadline.");
            Assert.IsFalse(task.IsCanceled, operation + " was cancelled.");
            if (task.IsFaulted) Assert.Fail(operation + " failed: " + task.Exception.GetBaseException());
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Task stop = null;
            if (client) stop = client.ReturnToWorkshop();
            if (stop != null) yield return Complete(stop, "Clean up the native launcher", 30);
            if (root) UnityEngine.Object.Destroy(root);
            if (Application.isPlaying) yield return null;
            Assert.IsTrue(root == null);
            client = null;
            if (Application.isPlaying && enteredPlay) yield return new ExitPlayMode();
            enteredPlay = false;
        }
    }
}
