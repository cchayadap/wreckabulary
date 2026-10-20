using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;
using Wreckabulary.Workshop;

namespace Wreckabulary.Online
{
    [DisallowMultipleComponent]
    public sealed class WordboundLauncher : MonoBehaviour
    {
        public WordboundLaunchAssets Assets;
        public Camera LobbyCamera;
        WordboundPartyMenu menu;
        WordboundWorkshop workshop;
        WordboundMatchClient activeClient;
        WordboundLocalMatchClient localClient;
        WordboundPartyLaunchContext activeLaunch;
        string failure, itemJson, ruleJson;
        int localGeneration;
        bool destroying;

        void Start()
        {
            try
            {
                if (!Assets) Assets = Resources.Load<WordboundLaunchAssets>(WordboundLaunchAssets.ResourcePath);
                if (!Assets || !Assets.PublicTrust)
                    throw new InvalidOperationException("Wordbound launch data is missing.");
                var data = Resources.Load<GameData>(GameData.ResourcePath);
                if (!data || !data.items || !data.rules)
                    throw new InvalidOperationException("The item catalogue or rule book is missing.");
                itemJson = data.items.text;
                ruleJson = data.rules.text;
                Assets.BodyPrefabs();
                Assets.GameplayScene();
                if (!Assets.SnapshotChunk.IsValid)
                    throw new InvalidOperationException("The network snapshot prefab is missing.");
                menu = gameObject.AddComponent<WordboundPartyMenu>();
                menu.Initialize(Assets.PublicTrust.text, data.items.text, data.rules.text,
                    Assets.MapPoolVersion, Assets.PublicCoursesJson);
                menu.BeginMatchRequested = BeginMatch;
                menu.ReturnRequested = ReturnMatch;
                menu.WorkshopRequested += OpenWorkshop;
                menu.CloseRequested += BackToLobby;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            catch (Exception error) { failure = error.Message; }
        }

        async Task BeginMatch(WordboundPartyLaunchContext launch, CancellationToken cancellation)
        {
            WordboundMatchClient client = null;
            try
            {
                cancellation.ThrowIfCancellationRequested();
                if (destroying || activeClient || localClient || activeLaunch != null)
                    throw new InvalidOperationException("Return from the current match before opening another.");
                var prefabs = Assets.BodyPrefabs();
                var scene = Assets.GameplayScene();
                var content = launch.Content;
                var root = new GameObject("Wordbound match client");
                root.transform.SetParent(transform, false);
                client = root.AddComponent<WordboundMatchClient>();
                activeLaunch = launch;
                activeClient = client;
                client.ReturnedToParty += () => Returned(client, launch);
                client.Failed += reason => {
                    if (!destroying && activeClient == client) menu.ShowFailure(reason);
                };
                await client.StartMatch(launch.Coordinator, launch.Verifier, launch.Verified,
                    content.Mode, content.Course, content.Geometry, content.Items, content.Rules,
                    prefabs, Assets.SnapshotChunk, menu.PhotonAppId, scene, launch.FreshJoin,
                    content.PhysicalPuzzles);
                if (destroying || activeClient != client) return;
                if (LobbyCamera) LobbyCamera.enabled = false;
            }
            catch
            {
                if (!destroying && (activeLaunch == launch || activeLaunch == null && !localClient) && LobbyCamera)
                    LobbyCamera.enabled = true;
                throw;
            }
            finally
            {
                if (!ReferenceEquals(client, null) && client.HasMatchContext) launch.MarkClientInitialized();
                else
                {
                    launch.ConfirmNoClientInitialized();
                    if (client)
                    {
                        if (activeClient == client) { activeClient = null; activeLaunch = null; }
                        Destroy(client.gameObject);
                    }
                }
            }
        }

        Task ReturnMatch(CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            if (!activeClient)
                throw new InvalidOperationException("There is no initialized match client to return.");
            return activeClient.ReturnToParty();
        }

        void Returned(WordboundMatchClient client, WordboundPartyLaunchContext launch)
        {
            if (destroying || activeClient != client || activeLaunch != launch) return;
            menu.NotifyReturned(launch.Verified.Match.MatchId);
            activeClient = null;
            activeLaunch = null;
            Destroy(client.gameObject);
            if (LobbyCamera) LobbyCamera.enabled = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        void OpenWorkshop()
        {
            if (activeLaunch != null || activeClient || localClient) return;
            if (!workshop)
            {
                workshop = gameObject.AddComponent<WordboundWorkshop>();
                workshop.CloseRequested += CloseWorkshop;
                workshop.PlaytestRequested += PlaytestWorkshop;
                workshop.ConfigureLocalPlaytest(true);
                workshop.OpenFlagship(WordboundFlagshipContent.Create(ItemCatalogue.FromJson(itemJson)));
            }
            else workshop.ResumeEditing();
            menu.Visible = false;
        }

        async void PlaytestWorkshop(ResolvedCourse resolved)
        {
            if (destroying || localClient || activeClient || activeLaunch != null) return;
            int generation = ++localGeneration;
            WordboundLocalMatchClient client = null;
            try
            {
                var content = WordboundWorkshopPlaytest.Create(workshop.LastPlaytestDraft, resolved,
                    workshop.Catalogue, workshop.PlayRequirements, itemJson, ruleJson);
                var prefabs = Assets.BodyPrefabs();
                var scene = Assets.GameplayScene();
                var root = new GameObject("Wordbound workshop playtest");
                root.transform.SetParent(transform, false);
                client = root.AddComponent<WordboundLocalMatchClient>();
                localClient = client;
                client.ReturnedToWorkshop += () => ReturnedFromPlaytest(client);
                client.Failed += reason => {
                    if (!destroying && localClient == client) workshop.ShowStatus(reason);
                };
                workshop.Visible = false;
                await client.StartLocal(content, prefabs, scene);
                if (!destroying && generation == localGeneration && localClient == client && LobbyCamera)
                    LobbyCamera.enabled = false;
            }
            catch (Exception error)
            {
                if (destroying || generation != localGeneration) return;
                if (client && localClient == client && !client.ShutdownConfirmed)
                {
                    client.ShowStartupFailure();
                    return;
                }
                if (client && localClient == client)
                {
                    localClient = null;
                    Destroy(client.gameObject);
                }
                if (LobbyCamera) LobbyCamera.enabled = true;
                workshop.ResumeEditing();
                workshop.ShowStatus("Playtest could not start: " + error.Message);
            }
        }

        void ReturnedFromPlaytest(WordboundLocalMatchClient client)
        {
            if (destroying || localClient != client) return;
            localGeneration++;
            localClient = null;
            Destroy(client.gameObject);
            if (LobbyCamera) LobbyCamera.enabled = true;
            workshop.ResumeEditing();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        void CloseWorkshop()
        {
            if (localClient) return;
            if (workshop) workshop.Visible = false;
            if (menu) menu.Visible = true;
        }

        void BackToLobby()
        {
            if (activeLaunch != null || activeClient || localClient) return;
            if (!Session.GoHome()) failure = "The main lobby is unavailable in this build.";
        }

        void OnGUI()
        {
            if (string.IsNullOrEmpty(failure)) return;
            float width = Mathf.Min(580, Screen.width - 32);
            GUILayout.BeginArea(new Rect((Screen.width - width) * .5f, 40, width, 170), GUI.skin.box);
            GUILayout.Label("Wordbound could not open");
            GUILayout.Label(failure, new GUIStyle(GUI.skin.label) { wordWrap = true });
            if (GUILayout.Button("Back to main lobby")) BackToLobby();
            GUILayout.EndArea();
        }

        void OnDestroy()
        {
            destroying = true;
            localGeneration++;
            if (menu) { menu.WorkshopRequested -= OpenWorkshop; menu.CloseRequested -= BackToLobby; }
            if (workshop)
            {
                workshop.CloseRequested -= CloseWorkshop;
                workshop.PlaytestRequested -= PlaytestWorkshop;
            }
        }
    }
}
