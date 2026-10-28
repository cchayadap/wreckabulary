using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Wreckabulary.EditorTools
{
    public static class LobbySharedThemeSetup
    {
        public const string ThemePath = "Assets/_Project/Resources/UI/LobbyInterfaceTheme.asset";
        public const string OnlinePath = "Assets/_Project/Data/Wordbound/Authoring/Interface.asset";
        public const string BackupRoot = "BuildNotes/visual-coherence-20261029/shared-theme/before-assets";
        public const string ArchiveRoot = "Assets/_Project/UI/DesignArchive/BeforeNeutral20261029";
        const string Fonts = "Assets/_Project/Resources/Fonts/Roboto/v3.016/";
        static readonly string[] Frames = {
            "Assets/_Project/Prefabs/UI/Home/HomePage.prefab",
            "Assets/_Project/Prefabs/UI/Play/PlayPage.prefab",
            "Assets/_Project/Prefabs/UI/Settings/SettingsPage.prefab",
            "Assets/_Project/Prefabs/UI/Loadout/LoadoutPage.prefab",
            "Assets/_Project/Prefabs/UI/Loadout/RecipeTile.prefab",
            "Assets/_Project/Prefabs/UI/Shop/ShopPage.prefab",
            "Assets/_Project/Prefabs/UI/Shop/ShopOfferCard.prefab"
        };

        [MenuItem("Wreckabulary/Authoring/Apply Shared Neutral Interface")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Apply the interface outside Play mode.");
            EnsureClean();
            var existing = AssetDatabase.LoadAssetAtPath<LobbyInterfaceTheme>(ThemePath);
            if (existing)
            {
                if (!File.Exists(BackupRoot + "/COMPLETE.txt"))
                    throw new InvalidOperationException("An incomplete theme migration exists. Inspect the exact backups before recovery.");
                VerifyBackups();
                VerifySaved(existing);
                Debug.Log("[Wreckabulary] Shared interface verified. Existing edits retained; no migration repeated.");
                return;
            }
            RequireVacant(ThemePath); RequireVacant(ArchiveRoot); RequireVacant(BackupRoot);
            var body = AssetDatabase.LoadAssetAtPath<Font>(Fonts + "Roboto-Regular.ttf");
            var heading = AssetDatabase.LoadAssetAtPath<Font>(Fonts + "Roboto-Medium.ttf");
            var typography = AssetDatabase.LoadAssetAtPath<LobbyTypographyProfile>(LobbyTypographySetup.ProfilePath);
            var online = AssetDatabase.LoadAssetAtPath<WordboundUiTheme>(OnlinePath);
            if (!body || !heading || !typography || !typography.TryValidate(out _) || !online)
                throw new InvalidOperationException("Saved Roboto fonts, valid lobby typography and the online theme are required.");
            online.Validate();
            foreach (string path in Frames)
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try { PlanBindings(root); }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }

            var originals = Frames.Append(OnlinePath).ToArray();
            Directory.CreateDirectory(BackupRoot);
            foreach (string path in originals.SelectMany(path => new[] { path, path + ".meta" })) BackupExact(path);
            File.WriteAllLines(BackupRoot + "/SHA256SUMS.txt", originals.SelectMany(path => new[] { path, path + ".meta" })
                .Select(path => Hash(path) + "  " + path));
            VerifyBackups();
            EnsureFolder(ArchiveRoot);
            foreach (string path in originals)
                if (!AssetDatabase.CopyAsset(path, ArchiveRoot + "/" + Path.GetFileName(path)))
                    throw new IOException("Cannot preserve editable asset " + path);

            var theme = ScriptableObject.CreateInstance<LobbyInterfaceTheme>();
            theme.name = "LobbyInterfaceTheme"; theme.BodyFont = body; theme.HeadingFont = heading;
            AssetDatabase.CreateAsset(theme, ThemePath);
            AssetDatabase.SaveAssetIfDirty(theme);
            foreach (string path in Frames)
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var bindings = PlanBindings(root);
                    var adapter = root.AddComponent<LobbyInterfaceFrame>();
                    adapter.Theme = theme; adapter.Bindings = bindings;
                    adapter.Apply();
                    EditorUtility.SetDirty(adapter);
                    if (!PrefabUtility.SaveAsPrefabAsset(root, path))
                        throw new IOException("Cannot save shared interface frame " + path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            theme.ApplyOnline(online);
            online.SharedTheme = theme;
            EditorUtility.SetDirty(online); AssetDatabase.SaveAssetIfDirty(online);
            VerifySaved(theme);
            File.WriteAllText(BackupRoot + "/COMPLETE.txt", "Shared neutral interface applied and verified. Exact original bytes and editable archives retained.\n");
            Debug.Log("[Wreckabulary] Shared interface saved. Edit " + ThemePath + ". Exact backups: " + BackupRoot);
        }

        internal static LobbyInterfaceFrame.Binding[] PlanBindings(GameObject root)
        {
            if (!root || root.GetComponent<LobbyInterfaceFrame>())
                throw new InvalidOperationException("A fresh frame without an existing palette adapter is required.");
            var bindings = new List<LobbyInterfaceFrame.Binding>();
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                var role = Near(text.color, 0x4c5b5c) || Near(text.color, 0x66706d) || Near(text.color, 0x536064)
                    ? LobbyColourRole.Muted : Near(text.color, 0x136b68) ? LobbyColourRole.Selection : LobbyColourRole.Text;
                bindings.Add(new LobbyInterfaceFrame.Binding { Target = text, Role = role });
            }
            foreach (var image in root.GetComponentsInChildren<Image>(true))
            {
                if (image.color.a == 0) continue;
                LobbyColourRole role;
                if (image.name == "Panel" || image.name == "Next match") role = LobbyColourRole.Panel;
                else if (image.name == "Body" || image.name == "Art") role = image.name == "Art" ? LobbyColourRole.Raised : LobbyColourRole.Card;
                else if (image.name == "Edge") role = LobbyColourRole.Border;
                else if (image.name == "Focus") role = LobbyColourRole.Focus;
                else if (image.name == "Ring" || image.name == "Underline") role = LobbyColourRole.Selection;
                else if (image.name.StartsWith("Icon ", StringComparison.Ordinal)) role = LobbyColourRole.Text;
                else throw new InvalidOperationException("Unclassified saved graphic; review before tinting: " + root.name + "/" + image.name);
                bindings.Add(new LobbyInterfaceFrame.Binding { Target = image, Role = role, Gradient = image.GetComponent<LobbyGradient>() });
            }
            if (bindings.Count == 0) throw new InvalidOperationException("The frame has no supported colour roles: " + root.name);
            return bindings.ToArray();
        }

        static bool Near(Color c, uint rgb) =>
            Mathf.Abs(c.r - (rgb >> 16 & 255) / 255f) < .002f &&
            Mathf.Abs(c.g - (rgb >> 8 & 255) / 255f) < .002f &&
            Mathf.Abs(c.b - (rgb & 255) / 255f) < .002f;

        static void EnsureClean()
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            foreach (string path in Frames.Append(OnlinePath).Append(ThemePath))
            {
                var asset = AssetDatabase.LoadMainAssetAtPath(path);
                if (!asset && path != ThemePath) throw new IOException("Required saved asset missing: " + path);
                if (asset && EditorUtility.IsDirty(asset) || stage != null && stage.assetPath == path)
                    throw new InvalidOperationException("Save and close the edited asset before migration: " + path);
                if (asset is GameObject root && root.GetComponentsInChildren<Component>(true).Any(c => c && EditorUtility.IsDirty(c)))
                    throw new InvalidOperationException("Unsaved prefab component changes: " + path);
            }
        }

        static void VerifySaved(LobbyInterfaceTheme theme)
        {
            if (!theme || !theme.TryValidate(out _) || !theme.BodyFont || !theme.HeadingFont)
                throw new InvalidOperationException("The saved shared palette/fonts are invalid.");
            var online = AssetDatabase.LoadAssetAtPath<WordboundUiTheme>(OnlinePath);
            if (!online || online.SharedTheme != theme) throw new InvalidOperationException("Online must reference the saved shared theme.");
            online.Validate();
            foreach (string path in Frames)
            {
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var adapter = root ? root.GetComponent<LobbyInterfaceFrame>() : null;
                if (!adapter || adapter.Theme != theme) throw new InvalidOperationException("Missing shared frame adapter: " + path);
                adapter.Validate();
            }
        }

        static void BackupExact(string path)
        {
            string target = BackupRoot + "/" + path;
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            File.Copy(path, target, false);
            if (Hash(path) != Hash(target)) throw new IOException("Exact backup verification failed: " + path);
        }

        static void VerifyBackups()
        {
            string sums = BackupRoot + "/SHA256SUMS.txt";
            if (!File.Exists(sums)) throw new IOException("Backup checksum record missing.");
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string line in File.ReadAllLines(sums))
            {
                if (line.Length < 67 || line.Substring(64, 2) != "  ") throw new IOException("Invalid backup checksum record.");
                string path = line.Substring(66);
                if (!seen.Add(path)) throw new IOException("Duplicate backup checksum path.");
                if (!Frames.Append(OnlinePath).SelectMany(p => new[] { p, p + ".meta" }).Contains(path))
                    throw new IOException("Unexpected backup path.");
                if (Hash(BackupRoot + "/" + path) != line.Substring(0, 64))
                    throw new IOException("Backup bytes changed: " + path);
            }
            if (File.ReadAllLines(sums).Length != (Frames.Length + 1) * 2) throw new IOException("Incomplete exact backup roster.");
        }

        static void RequireVacant(string path)
        {
            if (File.Exists(path) || Directory.Exists(path) || File.Exists(path + ".meta"))
                throw new IOException("Migration target already exists; inspect before retrying: " + path);
        }

        static string Hash(string path)
        {
            using var sha = SHA256.Create();
            using var stream = File.OpenRead(path);
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent, Path.GetFileName(path))))
                throw new IOException("Cannot create archive folder " + path);
        }
    }
}
