using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Wreckabulary.EditorTools
{
    public static class LobbyMinimalDesignSetup
    {
        const string Archive = "Assets/_Project/UI/DesignArchive/BeforeMinimal20261028";
        const string ThemePath = "Assets/_Project/Data/Wordbound/Authoring/Interface.asset";
        static readonly string[] Paths = {
            LobbyPlaySettingsLayoutSetup.PlayPrefab,
            LobbyPlaySettingsLayoutSetup.SettingsPrefab,
            "Assets/_Project/Prefabs/UI/Loadout/LoadoutPage.prefab",
            "Assets/_Project/Prefabs/UI/Loadout/RecipeTile.prefab",
            "Assets/_Project/Prefabs/UI/Shop/ShopPage.prefab"
        };

        [MenuItem("Wreckabulary/Authoring/Apply Minimal Menu Redesign")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Save menu designs outside Play mode.");
            var typography = AssetDatabase.LoadAssetAtPath<LobbyTypographyProfile>(LobbyTypographySetup.ProfilePath);
            if (!typography || !typography.TryValidate(out _) || EditorUtility.IsDirty(typography))
                throw new InvalidOperationException("Save a valid typography profile first.");
            var theme = AssetDatabase.LoadAssetAtPath<WordboundUiTheme>(ThemePath);
            if (!theme || EditorUtility.IsDirty(theme)) throw new InvalidOperationException("Save the interface theme first.");
            theme.Validate();
            foreach (string path in Paths)
            {
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (!source || EditorUtility.IsDirty(source)) throw new IOException("Save the existing menu before redesign: " + path);
            }
            if (AssetDatabase.IsValidFolder(Archive))
            {
                var current = AssetDatabase.LoadAssetAtPath<GameObject>(Paths[2]).GetComponent<LobbyLoadoutPageView>();
                var title = current.Panel.GetComponentsInChildren<TMP_Text>(true).Single(t => t.name == "Title");
                if (Mathf.Approximately(current.Panel.offsetMin.x, -LoadoutPage.Width) && title.text == "Customize" && theme.MenuWidth == 780)
                {
                    ValidateAll();
                    Debug.Log("[Wreckabulary] Minimal menu redesign is already saved; no assets changed.");
                    return;
                }
                throw new IOException("The preserved design archive already exists. Inspect the saved menus before applying further edits.");
            }
            WorkspaceAssetCopies.EnsureFolder(Archive);
            foreach (string path in Paths)
                if (!AssetDatabase.CopyAsset(path, Archive + "/" + Path.GetFileName(path)))
                    throw new IOException("Could not preserve the existing design: " + path);
            if (!AssetDatabase.CopyAsset(ThemePath, Archive + "/Interface.asset"))
                throw new IOException("Could not preserve the original interface theme.");
            foreach (string path in Paths)
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    Restyle(root, typography);
                    if (root.TryGetComponent(out LobbyLoadoutPageView loadout))
                    {
                        loadout.Panel.offsetMin = new Vector2(-LoadoutPage.Width, loadout.Panel.offsetMin.y);
                        loadout.LockerTab.Label.text = "Character";
                        loadout.RecipesTab.Label.text = "Equipment";
                        loadout.RecipeGrid.cellSize = new Vector2(180, 164);
                        loadout.RecipeGrid.spacing = new Vector2(12, 12);
                    }
                    if (root.TryGetComponent(out LobbyRecipeTileView tile))
                    {
                        tile.SelectedInk = LobbyKit.Cyan;
                        tile.Icon.rectTransform.sizeDelta = new Vector2(96, 96);
                        tile.MaximumLetterSize = 22;
                        tile.LetterWidthBudget = 154;
                    }
                    if (!PrefabUtility.SaveAsPrefabAsset(root, path)) throw new IOException("Could not save the redesigned menu: " + path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            theme.MenuWidth = 780;
            theme.Panel = LobbyKit.Hex(0xfaf9f5);
            theme.Text = LobbyKit.Navy;
            theme.Muted = LobbyKit.Muted;
            theme.Button = LobbyKit.Hex(0xf2f4f1);
            theme.Hover = LobbyKit.Hex(0xe5efeb);
            theme.Primary = LobbyKit.Cyan;
            theme.PrimaryText = Color.white;
            theme.Disabled = LobbyKit.Hex(0xe4e6e2);
            theme.DisabledText = LobbyKit.Muted;
            theme.Focus = LobbyKit.Cyan;
            theme.Validate();
            EditorUtility.SetDirty(theme);
            AssetDatabase.SaveAssetIfDirty(theme);
            ValidateAll();
            Debug.Log("[Wreckabulary] Saved minimal Play, Settings, Customize, equipment and Shop frames. Previous prefabs are preserved in " + Archive);
        }

        static void Restyle(GameObject root, LobbyTypographyProfile typography)
        {
            foreach (var title in root.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                title.font = typography.Body;
                title.fontSharedMaterial = typography.Body.material;
                title.characterSpacing = 0;
                if (title.name != "Title") continue;
                title.text = root.GetComponent<LobbyLoadoutPageView>() ? "Customize" :
                    root.GetComponent<LobbyPlayPageView>() ? "Play" :
                    root.GetComponent<LobbySettingsPageView>() ? "Settings" : "Shop";
                title.fontSize = 34;
                title.color = LobbyKit.Navy;
            }
            foreach (var gradient in root.GetComponentsInChildren<LobbyGradient>(true))
                gradient.Set(LobbyKit.PanelTop, LobbyKit.PanelTop);
            foreach (var press in root.GetComponentsInChildren<LobbyPress>(true))
            {
                press.Lift = 0; press.Tilt = 0; press.Lean = 0;
                press.HoverScale = 1; press.Sink = 1;
                if (press.Drop) press.Drop.enabled = false;
            }
            foreach (var action in root.GetComponentsInChildren<LobbyShopActionView>(true))
            {
                if (action.Label.text.Length > 0)
                    action.Label.text = char.ToUpperInvariant(action.Label.text[0]) + action.Label.text.Substring(1).ToLowerInvariant();
            }
            var panel = root.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(t => t.name == "Panel");
            if (panel && panel.TryGetComponent(out Image face) && !panel.GetComponent<LobbyGradient>()) face.color = LobbyKit.PanelTop;
        }

        static void ValidateAll()
        {
            AssetDatabase.LoadAssetAtPath<WordboundUiTheme>(ThemePath).Validate();
            Resources.Load<LobbyPlayLayout>(LobbyPlayLayout.ResourcePath).Validate();
            Resources.Load<LobbySettingsLayout>(LobbySettingsLayout.ResourcePath).Validate();
            Resources.Load<LobbyLoadoutLayout>(LobbyLoadoutLayout.ResourcePath).Validate();
            Resources.Load<LobbyShopLayout>(LobbyShopLayout.ResourcePath).Validate();
            foreach (string path in Paths)
                foreach (var text in AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentsInChildren<TMP_Text>(true))
                    if (!text.font || !EditorUtility.IsPersistent(text.font) || !EditorUtility.IsPersistent(text.fontSharedMaterial))
                        throw new InvalidOperationException("Menu typography must remain saved and editable.");
        }
    }
}
