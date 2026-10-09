using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Wreckabulary.EditorTools
{
    /// <summary>Stable entry points to the current scenes, character and presentation sources.</summary>
    public sealed class WorkspaceWindow : EditorWindow
    {
        public const string HubGuid = "4829ca58046a07b4db4cd309dd5093a8";
        public const string EmptyGuid = "58d0348edda8ffa4583aba09a2f26a9d";
        Vector2 scroll;
        readonly List<GameObject> hiddenCeilings = new();

        void OnEnable()
        {
            EditorSceneManager.sceneClosing += OnSceneClosing;
            PrefabStage.prefabStageClosing += OnPrefabStageClosing;
        }

        void OnSceneClosing(UnityEngine.SceneManagement.Scene scene, bool removingScene) => RestoreCeilings();
        void OnPrefabStageClosing(PrefabStage stage) => RestoreCeilings();

        [MenuItem("Wreckabulary/Open Current Workspace", priority = -100)]
        public static void ShowWindow()
        {
            var window = GetWindow<WorkspaceWindow>("Wreckabulary");
            window.minSize = new Vector2(360f, 520f);
            window.Show();
        }

        public static string HubPath => AssetDatabase.GUIDToAssetPath(HubGuid);

        void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            GUILayout.Space(14);
            GUILayout.Label("CURRENT WORKSPACE", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Edit the saved scenes and assets used by the game.", EditorStyles.wordWrappedLabel);
            GUILayout.Space(12);

            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                SceneButton("Edit Hub / Main Menu", HubGuid);
                SceneButton("Edit Arena / Dibs", "82b92a76ebd641c49b5e9ed8aa025a1a");
                SceneButton("Edit Moving Day", "ac5fed3922febf84ca59faadd8da4e64");
                SceneButton("Edit Tutorial", "db2c4e6815ecee14ba4faa77ac3f3945");
                GUILayout.Space(8);
                bool openedScene = EditorGUILayout.ToggleLeft("Play the opened scene", WorkspaceBootstrap.PlayOpenedScene);
                if (openedScene != WorkspaceBootstrap.PlayOpenedScene) WorkspaceBootstrap.PlayOpenedScene = openedScene;
                EditorGUILayout.HelpBox(openedScene
                    ? "Play starts in your currently opened scene."
                    : "Play starts at the Hub. Your open authoring scene is restored after Play.", MessageType.Info);
            }

            Section("CHARACTER & ART");
            AssetButton("Edit Player Prefab", "Assets/_Project/Prefabs/Player.prefab", true);
            AssetButton("Character Models", "Assets/_Project/Art/Imported/Avatar");
            AssetButton("Model Library", "Assets/_Project/Resources/ModelLibrary.asset");
            AssetButton("Generated UI Art", "Assets/_Project/Resources/UI/Generated");
            AssetButton("Power Item Illustrations", "Assets/_Project/Art/Generated/Items");
            AssetButton("Power Item Sprite Library", "Assets/_Project/Resources/UI/Generated/PowerItemArt.asset");

            Section("WORLDS");
            AssetButton("Edit Pinwheel House", "Assets/_Project/Resources/Worlds/PinwheelHouse.prefab", true);
            AssetButton("Edit Garden Courtyard", "Assets/_Project/Resources/Worlds/GardenCourtyard.prefab", true);
            AssetButton("Edit City Flat", "Assets/_Project/Resources/Worlds/CityFlat.prefab", true);
            AssetButton("Edit Terrace House", "Assets/_Project/Resources/Worlds/TerraceHouse.prefab", true);
            AssetButton("Edit Walk-up Apartments", "Assets/_Project/Resources/Worlds/WalkupApartments.prefab", true);
            EditorGUILayout.LabelField("Shared house prefabs are used by every mode. Choose a house in the Play menu to try it.", EditorStyles.wordWrappedMiniLabel);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button(hiddenCeilings.Count > 0 ? "Show Ceilings in Scene View" : "Hide Ceilings in Scene View", GUILayout.Height(28)))
                {
                    if (hiddenCeilings.Count > 0) RestoreCeilings();
                    else HideCeilings();
                }
            }
            EditorGUILayout.LabelField("Temporarily opens the roof for editing. Saved assets and the Game view are unchanged.", EditorStyles.wordWrappedMiniLabel);

            Section("LIGHTING & MATERIALS");
            AssetButton("Sunlit House / Lighting Profile", "Assets/_Project/Resources/Environment/SunlitHouse.asset");
            AssetButton("Daylight Sky / Material", "Assets/_Project/Materials/Environment/DaylightSky.mat");
            AssetButton("Current World Materials", "Assets/_Project/Worlds/Generated/Materials");
            AssetButton("Current World Textures", "Assets/_Project/Worlds/Generated/Textures");
            EditorGUILayout.LabelField("Select a saved asset to edit its Inspector. Scene and prefab changes are saved normally; opening a world does not rebuild it.", EditorStyles.wordWrappedMiniLabel);

            Section("UI & RULE SOURCES");
            AssetButton("Inventory Layout / UXML", "Assets/_Project/Resources/UI/Inventory/Inventory.uxml", true);
            AssetButton("Inventory Styles / USS", "Assets/_Project/Resources/UI/Inventory/Inventory.uss", true);
            AssetButton("Gameplay HUD Layout", "Assets/_Project/Scripts/Game/GameHud.cs", true);
            AssetButton("Pause & Result Cards", "Assets/_Project/Scripts/Game/GameHudCards.cs", true);
            AssetButton("Main Menu Layout", "Assets/_Project/Scripts/UI/Lobby/LobbyMenu.cs", true);
            AssetButton("Arena Maps & Game Rules", "Assets/_Project/Data/Config");
            AssetButton("Recipe Balance & Powers", "Assets/_Project/Data/Config/items.json", true);
            EditorGUILayout.LabelField("HUD and menu layouts are authored in C#. The inventory uses UI Builder assets.", EditorStyles.wordWrappedMiniLabel);

            Section("WORKSPACE TOOLS");
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button("Upgrade Missing Authoring Assets", GUILayout.Height(30)))
                    SceneWorkspace.UpgradeAll();
            }
            EditorGUILayout.LabelField("Adds missing authoring assets while preserving upgraded scene edits. Legacy prototype generation is under Wreckabulary / Legacy.", EditorStyles.wordWrappedMiniLabel);
            GUILayout.Space(12);
            EditorGUILayout.EndScrollView();
        }

        static void Section(string title)
        {
            GUILayout.Space(16);
            GUILayout.Label(title, EditorStyles.boldLabel);
        }

        void HideCeilings()
        {
            foreach (var surface in Resources.FindObjectsOfTypeAll<CutawaySurface>())
            {
                if (!surface || EditorUtility.IsPersistent(surface) || !surface.gameObject.scene.IsValid() ||
                    surface.Kind == CutawayKind.UpperWall || SceneVisibilityManager.instance.IsHidden(surface.gameObject)) continue;
                SceneVisibilityManager.instance.Hide(surface.gameObject, false);
                hiddenCeilings.Add(surface.gameObject);
            }
            SceneView.RepaintAll();
        }

        void RestoreCeilings()
        {
            foreach (var ceiling in hiddenCeilings)
                if (ceiling) SceneVisibilityManager.instance.Show(ceiling, false);
            hiddenCeilings.Clear();
            SceneView.RepaintAll();
        }

        void OnDisable()
        {
            EditorSceneManager.sceneClosing -= OnSceneClosing;
            PrefabStage.prefabStageClosing -= OnPrefabStageClosing;
            RestoreCeilings();
        }

        static void SceneButton(string label, string guid)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(path)))
                if (GUILayout.Button(label, GUILayout.Height(32))) OpenScene(path);
        }

        public static void OpenScene(string path)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || string.IsNullOrEmpty(path)) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            SceneView.RepaintAll();
        }

        static void AssetButton(string label, string path, bool open = false)
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(path);
            using (new EditorGUI.DisabledScope(!asset))
            {
                if (!GUILayout.Button(label, GUILayout.Height(25))) return;
                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
                if (open) AssetDatabase.OpenAsset(asset);
            }
        }
    }
}
