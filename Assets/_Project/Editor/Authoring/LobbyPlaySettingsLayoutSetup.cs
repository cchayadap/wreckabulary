using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Wreckabulary.EditorTools
{
    public static class LobbyPlaySettingsLayoutSetup
    {
        public const string PlayProfile = "Assets/_Project/Resources/UI/LobbyPlayLayout.asset";
        public const string SettingsProfile = "Assets/_Project/Resources/UI/LobbySettingsLayout.asset";
        public const string PlayPrefab = "Assets/_Project/Prefabs/UI/Play/PlayPage.prefab";
        public const string SettingsPrefab = "Assets/_Project/Prefabs/UI/Settings/SettingsPage.prefab";
        public const string Graphics = "Assets/_Project/UI/PlaySettings/Graphics";

        [MenuItem("Wreckabulary/Authoring/Create or Verify Saved Play and Settings Frames")]
        public static void CreateOrVerify()
        {
            var typography = AssetDatabase.LoadAssetAtPath<LobbyTypographyProfile>(LobbyTypographySetup.ProfilePath);
            CreatePlayAt(PlayProfile, PlayPrefab, Graphics, typography);
            Selection.activeObject = CreateSettingsAt(SettingsProfile, SettingsPrefab, Graphics, typography);
        }

        public static LobbyPlayLayout CreatePlayAt(string profilePath, string prefabPath, string graphics, LobbyTypographyProfile typography)
        {
            Check(profilePath, ".asset"); Check(prefabPath, ".prefab"); Check(graphics, null);
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Create saved frames outside Play Mode.");
            var current = AssetDatabase.LoadMainAssetAtPath(profilePath);
            if (current)
            {
                if (current is not LobbyPlayLayout existing) throw new IOException("Frame profile destination is occupied.");
                existing.Validate(); LobbyShopLayoutSetup.ValidatePersistent(existing.Page); return existing;
            }
            RequireVacant(profilePath);
            var saved = AssetDatabase.LoadMainAssetAtPath(prefabPath);
            LobbyPlayPageView page = null;
            if (saved)
            {
                if (saved is not GameObject prefab || !prefab.TryGetComponent(out page))
                    throw new IOException("Existing frame needs its typed Play view.");
                page.Validate(); LobbyShopLayoutSetup.ValidatePersistent(page);
            }
            else
            {
                RequireVacant(prefabPath);
                if (!typography || !EditorUtility.IsPersistent(typography) || EditorUtility.IsDirty(typography) || !typography.TryValidate(out _))
                    throw new InvalidOperationException("A saved valid lobby typography profile is required.");
            }
            WorkspaceAssetCopies.EnsureFolder(Path.GetDirectoryName(profilePath).Replace('\\', '/'));
            WorkspaceAssetCopies.EnsureFolder(Path.GetDirectoryName(prefabPath).Replace('\\', '/'));
            WorkspaceAssetCopies.EnsureFolder(graphics);
            if (!page) page = LobbyShopLayoutSetup.SaveTemplate(prefabPath, () => BuildPlay(typography), graphics, new Dictionary<Sprite, Sprite>());
            var profile = ScriptableObject.CreateInstance<LobbyPlayLayout>();
            try
            {
                profile.Page = page; profile.Validate(); LobbyShopLayoutSetup.ValidatePersistent(page);
                RequireVacant(profilePath); AssetDatabase.CreateAsset(profile, profilePath); AssetDatabase.SaveAssetIfDirty(profile);
                return profile;
            }
            catch { if (!AssetDatabase.Contains(profile)) Object.DestroyImmediate(profile); throw; }
        }

        public static LobbySettingsLayout CreateSettingsAt(string profilePath, string prefabPath, string graphics, LobbyTypographyProfile typography)
        {
            Check(profilePath, ".asset"); Check(prefabPath, ".prefab"); Check(graphics, null);
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Create saved frames outside Play Mode.");
            var current = AssetDatabase.LoadMainAssetAtPath(profilePath);
            if (current)
            {
                if (current is not LobbySettingsLayout existing) throw new IOException("Frame profile destination is occupied.");
                existing.Validate(); LobbyShopLayoutSetup.ValidatePersistent(existing.Page); return existing;
            }
            RequireVacant(profilePath);
            var saved = AssetDatabase.LoadMainAssetAtPath(prefabPath);
            LobbySettingsPageView page = null;
            if (saved)
            {
                if (saved is not GameObject prefab || !prefab.TryGetComponent(out page))
                    throw new IOException("Existing frame needs its typed Settings view.");
                page.Validate(); LobbyShopLayoutSetup.ValidatePersistent(page);
            }
            else
            {
                RequireVacant(prefabPath);
                if (!typography || !EditorUtility.IsPersistent(typography) || EditorUtility.IsDirty(typography) || !typography.TryValidate(out _))
                    throw new InvalidOperationException("A saved valid lobby typography profile is required.");
            }
            WorkspaceAssetCopies.EnsureFolder(Path.GetDirectoryName(profilePath).Replace('\\', '/'));
            WorkspaceAssetCopies.EnsureFolder(Path.GetDirectoryName(prefabPath).Replace('\\', '/'));
            WorkspaceAssetCopies.EnsureFolder(graphics);
            if (!page) page = LobbyShopLayoutSetup.SaveTemplate(prefabPath, () => BuildSettings(typography), graphics, new Dictionary<Sprite, Sprite>());
            var profile = ScriptableObject.CreateInstance<LobbySettingsLayout>();
            try
            {
                profile.Page = page; profile.Validate(); LobbyShopLayoutSetup.ValidatePersistent(page);
                RequireVacant(profilePath); AssetDatabase.CreateAsset(profile, profilePath); AssetDatabase.SaveAssetIfDirty(profile);
                return profile;
            }
            catch { if (!AssetDatabase.Contains(profile)) Object.DestroyImmediate(profile); throw; }
        }

        static LobbyPlayPageView BuildPlay(LobbyTypographyProfile typography)
        {
            var root = Shell("Play", Vector2.zero, Vector2.one, typography, out var panel, out var title, out var close);
            var view = root.gameObject.AddComponent<LobbyPlayPageView>();
            view.Panel = panel; view.Title = title; view.Close = close;
            view.Subtitle = Text(panel, "Subtitle", "Play on this PC.", typography.Body, 17, LobbyKit.Muted, TextAlignmentOptions.TopLeft);
            view.Subtitle.rectTransform.Place(new Vector2(0, 1), Vector2.one, new Vector2(34, -120), new Vector2(-88, -90));
            view.Body = LobbyKit.Rect(panel, "Body").Place(Vector2.zero, Vector2.one, new Vector2(32, 28), new Vector2(-32, -130));
            view.Queues = LobbyKit.Rect(panel, "Queue strip").Place(Vector2.one, Vector2.one, new Vector2(-688, -78), new Vector2(-88, -26));
            view.Choices = LobbyKit.Column(view.Body, "Choices", 8).Place(Vector2.zero, Vector2.one, new Vector2(0, 104), Vector2.zero);
            view.Summary = LobbyKit.Rect(view.Body, "Summary region").Place(Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 88));
            view.Validate(); return view;
        }

        static LobbySettingsPageView BuildSettings(LobbyTypographyProfile typography)
        {
            var root = Shell("Settings", new Vector2(.12f, 0), new Vector2(.88f, 1), typography, out var panel, out var title, out var close);
            var view = root.gameObject.AddComponent<LobbySettingsPageView>();
            view.Panel = panel; view.Title = title; view.Close = close;
            view.Body = LobbyKit.Rect(panel, "Body").Place(Vector2.zero, Vector2.one, new Vector2(32, 28), new Vector2(-32, -100));
            view.Tabs = LobbyKit.Column(view.Body, "Tabs", 8).Place(Vector2.zero, new Vector2(0, 1), Vector2.zero, new Vector2(230, 0));
            var stack = LobbyKit.Column(view.Body, "Content and display confirmation", 12).Place(Vector2.zero, Vector2.one, new Vector2(254, 0), Vector2.zero);
            var layout = stack.GetComponent<VerticalLayoutGroup>();
            layout.childControlHeight = true; layout.childForceExpandHeight = false;
            view.Content = LobbyKit.Rect(stack, "Content");
            var content = view.Content.gameObject.AddComponent<LayoutElement>(); content.flexibleHeight = 1; content.minHeight = 0;
            view.Confirmation = LobbyKit.Rect(stack, "Display confirmation");
            var reservation = view.Confirmation.gameObject.AddComponent<LayoutElement>(); reservation.preferredHeight = 80; reservation.minHeight = 80;
            view.Confirmation.gameObject.SetActive(false);
            view.Validate(); return view;
        }

        static RectTransform Shell(string name, Vector2 min, Vector2 max, LobbyTypographyProfile typography,
            out RectTransform panel, out TextMeshProUGUI title, out Button close)
        {
            var root = LobbyKit.Rect(null, name + " frame").Fill();
            panel = LobbyKit.Rect(root, "Panel").Place(min, max);
            LobbyKit.PanelFace(panel, 28, 5, 8).raycastTarget = true;
            title = Text(panel, "Title", LobbyKit.Upper(name), typography.Display, 43, LobbyKit.Sun, TextAlignmentOptions.BottomLeft);
            title.fontSharedMaterial = typography.Drop; title.characterSpacing = 2;
            title.rectTransform.Place(new Vector2(0, 1), Vector2.one, new Vector2(34, -84), new Vector2(-88, -20));
            close = LobbyKit.IconButton(panel, LobbyIcons.Close, "Close page", null);
            ((RectTransform)close.transform).Pin(Vector2.one, new Vector2(-20, -20), new Vector2(48, 48));
            return root;
        }

        static TextMeshProUGUI Text(Transform parent, string name, string value, TMP_FontAsset font, float size, Color color, TextAlignmentOptions alignment)
        {
            var rect = LobbyKit.Rect(parent, name); rect.gameObject.SetActive(false);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>(); text.font = font; text.fontSharedMaterial = font.material;
            text.text = value; text.fontSize = size; text.color = color; text.alignment = alignment; text.raycastTarget = false;
            text.extraPadding = true; text.textWrappingMode = TextWrappingModes.NoWrap; text.overflowMode = TextOverflowModes.Ellipsis;
            rect.gameObject.SetActive(true); return text;
        }

        static void Check(string path, string extension)
        {
            if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/_Project/", StringComparison.Ordinal) || path.Contains('\\') ||
                path.Split('/').Any(part => string.IsNullOrEmpty(part) || part == "." || part == ".." || part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) ||
                extension != null && !string.Equals(Path.GetExtension(path), extension, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Frame authoring paths must stay inside Assets/_Project with the expected extension.");
        }

        static void RequireVacant(string path)
        {
            if (File.Exists(path) || Directory.Exists(path) || File.Exists(path + ".meta") || AssetDatabase.LoadMainAssetAtPath(path))
                throw new IOException("Existing authoring content is preserved: " + path);
        }
    }
}
