using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Wreckabulary.EditorTools
{
    [InitializeOnLoad]
    public static class WorkspacePlayPreview
    {
        const string Prefix = "Wreckabulary.Workspace.Preview.";
        const int Pending = 1, Playing = 2, Restored = 3;
        static int Phase => SessionState.GetInt(Prefix + "phase", 0);
        public static bool IsActive => Phase == Pending || Phase == Playing;
        public static string Message => SessionState.GetString(Prefix + "message", "");

        static WorkspacePlayPreview() => EditorApplication.update += Update;

        public static bool Start(string page)
        {
            ValidatePage(page);
            if (WorkspaceBootstrap.IsAutomatedSession || WorkspaceBootstrap.IsTestRun ||
                EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling ||
                EditorApplication.isUpdating || IsActive) return false;
            var hub = AssetDatabase.LoadAssetAtPath<SceneAsset>(WorkspaceWindow.HubPath);
            if (!hub) throw new InvalidOperationException("The saved Hub scene is unavailable.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
            Arm(page, EditorSceneManager.playModeStartScene);
            try
            {
                EditorSceneManager.playModeStartScene = hub;
                EditorApplication.isPlaying = true;
                return true;
            }
            catch
            {
                RestoreEntry();
                throw;
            }
        }

        static void ValidatePage(string page)
        {
            if (page != LobbyMenu.Home && page != LobbyMenu.Shop && page != LobbyMenu.Loadout && page != LobbyMenu.Play && page != LobbyMenu.Settings)
                throw new ArgumentException("Choose Lobby, Play, Settings, Shop or Loadout.", nameof(page));
        }

        static void Arm(string page, SceneAsset previousEntry)
        {
            ValidatePage(page);
            SessionState.SetString(Prefix + "previous", previousEntry
                ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(previousEntry)) : "");
            SessionState.SetString(Prefix + "page", page);
            SessionState.SetString(Prefix + "message", "Opening the saved Hub for a one-shot " + Label(page) + " preview.");
            SessionState.SetFloat(Prefix + "started", (float)EditorApplication.timeSinceStartup);
            SessionState.SetInt(Prefix + "phase", Pending);
        }

        internal static bool ApplyPlayEntry()
        {
            if (Phase == Pending)
            {
                EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(WorkspaceWindow.HubPath);
                return true;
            }
            if (Phase != Restored) return false;
            SetPreviousEntry();
            return true;
        }

        internal static bool HandlePlayModeChanged(PlayModeStateChange state)
        {
            if (Phase == Restored && state == PlayModeStateChange.ExitingEditMode)
            {
                ForgetRestoredEntry();
                return false;
            }
            if (!IsActive) return false;
            if (state == PlayModeStateChange.ExitingEditMode) ApplyPlayEntry();
            else if (state == PlayModeStateChange.EnteredPlayMode)
            {
                SessionState.SetInt(Prefix + "phase", Playing);
                SessionState.SetFloat(Prefix + "started", (float)EditorApplication.timeSinceStartup);
            }
            else if (state == PlayModeStateChange.EnteredEditMode) RestoreEntry();
            return true;
        }

        internal static void ForgetRestoredEntry()
        {
            if (Phase != Restored) return;
            SessionState.EraseInt(Prefix + "phase");
            SessionState.EraseString(Prefix + "previous");
            SessionState.EraseString(Prefix + "message");
        }

        static void SetPreviousEntry()
        {
            string guid = SessionState.GetString(Prefix + "previous", "");
            EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(guid) ? null :
                AssetDatabase.LoadAssetAtPath<SceneAsset>(AssetDatabase.GUIDToAssetPath(guid));
        }

        static void RestoreEntry()
        {
            SetPreviousEntry();
            SessionState.EraseString(Prefix + "page");
            SessionState.EraseFloat(Prefix + "started");
            SessionState.SetInt(Prefix + "phase", Restored);
            SessionState.SetString(Prefix + "message", "Preview stopped. Your previous Play entry and open authoring scene are restored.");
        }

        static void Update()
        {
            if (WorkspaceBootstrap.IsAutomatedSession || WorkspaceBootstrap.IsTestRun) return;
            if (RecoverStoppedPreview(EditorApplication.isPlayingOrWillChangePlaymode,
                EditorApplication.isCompiling, EditorApplication.isUpdating)) return;
            if (Phase != Playing || !EditorApplication.isPlaying) return;
            string page = SessionState.GetString(Prefix + "page", "");
            if (string.IsNullOrEmpty(page)) return;
            try
            {
                var menu = LobbyMenu.Instance;
                var hud = GameHud.Active;
                if (!menu || menu.Current == null || hud && hud.StateController && hud.StateController.IsTransitioning)
                {
                    if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Prefix + "started", 0) > 30)
                        throw new InvalidOperationException("The lobby preview did not become ready within 30 seconds.");
                    return;
                }
                menu.Open(page);
                SessionState.EraseString(Prefix + "page");
                SessionState.SetString(Prefix + "message", Label(page) + " is running. Stop Play to return to your authoring scene.");
                Selection.activeGameObject = menu.gameObject;
                EditorGUIUtility.PingObject(menu.gameObject);
            }
            catch (Exception error)
            {
                SessionState.EraseString(Prefix + "page");
                Debug.LogException(error);
                EditorApplication.isPlaying = false;
            }
        }

        internal static bool RecoverStoppedPreview(bool playingOrWillChangePlaymode, bool compiling, bool updating)
        {
            if (!IsActive || playingOrWillChangePlaymode || compiling || updating) return false;
            RestoreEntry();
            return true;
        }

        static string Label(string page) => page switch { LobbyMenu.Home => "Lobby", LobbyMenu.Shop => "Shop",
            LobbyMenu.Loadout => "Loadout", LobbyMenu.Play => "Play choices", LobbyMenu.Settings => "Settings", _ => throw new ArgumentException("Unknown preview page.") };
    }
}
