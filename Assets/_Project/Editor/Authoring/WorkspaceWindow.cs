using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Wreckabulary.EditorTools
{
    public sealed class WorkspaceWindow : EditorWindow
    {
        public const string HubGuid = "4829ca58046a07b4db4cd309dd5093a8";
        public const string EmptyGuid = "58d0348edda8ffa4583aba09a2f26a9d";
        public const string WordboundLobbyGuid = "8fb103840aae32947b0a9b13c7af0fba";
        public const string WordboundGameplayGuid = "38e8ae58953ea874c9550132f992c100";
        [SerializeField] int tab;
        static readonly string[] Tabs = { "Scenes", "Design", "Assets" };
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
            GUILayout.Space(14);
            GUILayout.Label("CURRENT WORKSPACE", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Open a saved scene, edit its design assets, or try the real game.", EditorStyles.wordWrappedLabel);
            GUILayout.Space(10);
            tab = GUILayout.Toolbar(tab, Tabs);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            if (tab == 0) DrawScenes();
            else if (tab == 1) DrawDesign();
            else WorkspaceAssetBrowser.Draw();
            GUILayout.Space(12);
            EditorGUILayout.EndScrollView();
        }

        void DrawScenes()
        {
            Section("TRY THE LOBBY");
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode ||
                EditorApplication.isCompiling || EditorApplication.isUpdating || WorkspacePlayPreview.IsActive ||
                WorkspaceBootstrap.IsAutomatedSession || WorkspaceBootstrap.IsTestRun))
            {
                EditorGUILayout.BeginHorizontal();
                PreviewButton("Play Lobby", LobbyMenu.Home);
                PreviewButton("Play Shop", LobbyMenu.Shop);
                PreviewButton("Play Loadout", LobbyMenu.Loadout);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
                PreviewButton("Play choices", LobbyMenu.Play);
                PreviewButton("Play Settings", LobbyMenu.Settings);
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.HelpBox("These shortcuts enter the real Hub once. Stop Play to return to your open scene and previous Play entry. Edit saved page frames from Design. Changes to the Play hierarchy are temporary.", MessageType.Info);
            if (!string.IsNullOrEmpty(WorkspacePlayPreview.Message))
                EditorGUILayout.LabelField(WorkspacePlayPreview.Message, EditorStyles.wordWrappedMiniLabel);

            Section("SAVED SCENES");
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                SceneButton("Open Hub / Lobby environment", HubGuid);
                SceneButton("Open Wordbound Lobby", WordboundLobbyGuid);
                SceneButton("Open Wordbound Gameplay shell", WordboundGameplayGuid);
                EditorGUILayout.LabelField("Wordbound Lobby contains its launcher and camera. Gameplay contains lighting; courses and players are created by the existing launcher or local course playtest.", EditorStyles.wordWrappedMiniLabel);
                SceneButton("Edit Arena / Dibs", "82b92a76ebd641c49b5e9ed8aa025a1a");
                SceneButton("Edit Moving Day", "ac5fed3922febf84ca59faadd8da4e64");
                SceneButton("Edit Tutorial", "db2c4e6815ecee14ba4faa77ac3f3945");
                ScenePathButton("Open Equipment Studio", WorkspaceStudioSetup.EquipmentScene);
                ScenePathButton("Open Character Studio", WorkspaceStudioSetup.CharacterScene);
                ScenePathButton("Open Household Studio", WordboundHouseholdSetup.ScenePath);
                EditorGUILayout.LabelField("Studios save a visual review layout. Equipment uses editable prefab copies; assign a copy in a Wordbound visual profile to use it in gameplay.", EditorStyles.wordWrappedMiniLabel);
                GUILayout.Space(8);
                bool openedScene = EditorGUILayout.ToggleLeft("Play the opened scene", WorkspaceBootstrap.PlayOpenedScene);
                if (openedScene != WorkspaceBootstrap.PlayOpenedScene) WorkspaceBootstrap.PlayOpenedScene = openedScene;
                EditorGUILayout.HelpBox(openedScene
                    ? "Play starts in your currently opened scene."
                    : "Play starts at the Hub. Your open authoring scene is restored after Play.", MessageType.Info);
            }

            Section("COURSE & WORKSHOP");
            AssetButton("Edit Flagship course", "Assets/_Project/Data/Wordbound/Authoring/FlagshipCourse.asset");
            AssetButton("Edit Freestyle course", "Assets/_Project/Data/Wordbound/Authoring/FreestyleCourse.asset");
            EditorGUILayout.LabelField("Use the course Inspector to validate, preview and play its exact seed locally. For direct dragging, play Wordbound Lobby and choose Workshop. Workshop drafts do not automatically overwrite the source course asset.", EditorStyles.wordWrappedMiniLabel);
            string guide = System.IO.Path.GetFullPath("docs/wordbound/UNITY-EDITING-GUIDE.md");
            using (new EditorGUI.DisabledScope(!System.IO.File.Exists(guide)))
                if (GUILayout.Button("Open Wordbound editing guide")) EditorUtility.OpenWithDefaultApp(guide);
        }

        void DrawDesign()
        {
            Section("HOME LAYOUT");
            AssetButton("Edit Home dock & turn hint", "Assets/_Project/Prefabs/UI/Home/HomePage.prefab", true);
            AssetButton("Home layout assignments", "Assets/_Project/Resources/UI/LobbyHomeLayout.asset");
            Section("SHOP LAYOUT");
            AssetButton("Edit Shop panel", "Assets/_Project/Prefabs/UI/Shop/ShopPage.prefab", true);
            AssetButton("Edit offer card", "Assets/_Project/Prefabs/UI/Shop/ShopOfferCard.prefab", true);
            AssetButton("Shop layout assignments", "Assets/_Project/Resources/UI/LobbyShopLayout.asset");
            Section("LOADOUT LAYOUT");
            AssetButton("Edit Loadout panel & recipe shelf", "Assets/_Project/Prefabs/UI/Loadout/LoadoutPage.prefab", true);
            AssetButton("Edit recipe card", "Assets/_Project/Prefabs/UI/Loadout/RecipeTile.prefab", true);
            AssetButton("Loadout layout assignments", "Assets/_Project/Resources/UI/LobbyLoadoutLayout.asset");
            EditorGUILayout.LabelField("Wearable pickers and recipe detail contents are created inside the saved body slots during Play.", EditorStyles.wordWrappedMiniLabel);
            Section("PLAY & SETTINGS FRAMES");
            AssetButton("Edit Play panel", LobbyPlaySettingsLayoutSetup.PlayPrefab, true);
            AssetButton("Play layout assignments", LobbyPlaySettingsLayoutSetup.PlayProfile);
            AssetButton("Edit Settings panel", LobbyPlaySettingsLayoutSetup.SettingsPrefab, true);
            AssetButton("Settings layout assignments", LobbyPlaySettingsLayoutSetup.SettingsProfile);
            EditorGUILayout.LabelField("Edit saved frame regions outside Play. Cards, settings controls and binding rows remain generated inside them.", EditorStyles.wordWrappedMiniLabel);
            Section("LOBBY CAMERA");
            AssetButton("Framing, height & field of view", "Assets/_Project/Resources/UI/LobbyCamera.asset");
            Section("LOBBY TYPOGRAPHY");
            AssetButton("Lobby fonts & material presets", "Assets/_Project/Resources/UI/LobbyTypography.asset");
            AssetButton("Saved fonts", "Assets/_Project/UI/Fonts");
            AssetButton("Saved text materials", "Assets/_Project/UI/Materials");
            Section("WORDBOUND INSPECTOR ASSETS");
            AssetButton("Launcher / assigned profiles", "Assets/_Project/Resources/Wordbound/LaunchAssets.asset");
            AssetButton("Flagship course", "Assets/_Project/Data/Wordbound/Authoring/FlagshipCourse.asset");
            AssetButton("Freestyle course", "Assets/_Project/Data/Wordbound/Authoring/FreestyleCourse.asset");
            AssetButton("Player camera", "Assets/_Project/Data/Wordbound/Authoring/PlayerCamera.asset");
            AssetButton("Story & optional ending copy", "Assets/_Project/Data/Wordbound/Authoring/LostAddressStory.asset");
            AssetButton("Interface fonts, colours & spacing", "Assets/_Project/Data/Wordbound/Authoring/Interface.asset");
            AssetButton("Gameplay artwork & held pose", "Assets/_Project/Data/Wordbound/Authoring/Presentation.asset");
            EditorGUILayout.LabelField("These are saved defaults. Check the launcher and course assignments for custom copies. Interface styles Wordbound menus and workshop; use the Home, Shop and Loadout prefabs above for those lobby pages.", EditorStyles.wordWrappedMiniLabel);

            Section("CHARACTER & ART");
            AssetButton("Facial reactions & blending", "Assets/_Project/Resources/Art/AvatarExpressions.asset");
            AssetButton("Edit Player Prefab", "Assets/_Project/Prefabs/Player.prefab", true);
            AssetButton("Character Models", "Assets/_Project/Art/Imported/Avatar");
            AssetButton("Household furniture prefabs", WordboundHouseholdSetup.PrefabRoot);
            AssetButton("Household initial sizing", WordboundHouseholdSetup.SettingsPath);
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
            AssetButton("Loadout & lobby page layouts", "Assets/_Project/Scripts/UI/Lobby/LobbyPages.cs", true);
            AssetButton("Shop page layout", "Assets/_Project/Scripts/UI/Lobby/ShopPage.cs", true);
            AssetButton("Arena Maps & Game Rules", "Assets/_Project/Data/Config");
            AssetButton("Recipe Balance & Powers", "Assets/_Project/Data/Config/items.json", true);
            EditorGUILayout.LabelField("Edit saved Home, Shop and Loadout layouts above. Wearable pickers, recipe details and the gameplay HUD still use code layouts; the separate inventory study uses UI Builder assets.", EditorStyles.wordWrappedMiniLabel);

            Section("WORKSPACE TOOLS");
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button("Create missing developer assets", GUILayout.Height(30)))
                    WorkspaceStudioSetup.CreateOrVerify();
                if (GUILayout.Button("Create missing Household Studio", GUILayout.Height(30)))
                    WordboundHouseholdSetup.CreateOrVerify();
                if (GUILayout.Button("Upgrade Missing Authoring Assets", GUILayout.Height(30)))
                    SceneWorkspace.UpgradeAll();
            }
            EditorGUILayout.LabelField("Adds missing authoring assets while preserving upgraded scene edits. Legacy prototype generation is under Wreckabulary / Legacy.", EditorStyles.wordWrappedMiniLabel);
        }

        static void PreviewButton(string label, string page)
        {
            if (!GUILayout.Button(label, GUILayout.Height(32))) return;
            try { WorkspacePlayPreview.Start(page); }
            catch (System.Exception error) { EditorUtility.DisplayDialog("Workspace preview", error.Message, "Close"); }
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

        static void ScenePathButton(string label, string path)
        {
            using (new EditorGUI.DisabledScope(!AssetDatabase.LoadAssetAtPath<SceneAsset>(path)))
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
