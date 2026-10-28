using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Wreckabulary.EditorTools
{
    public static class LobbyRobotoTypographySetup
    {
        public const string SourceRoot = "ArtSource/Fonts/Roboto/v3.016";
        public const string FontRoot = "Assets/_Project/UI/Fonts/Roboto/v3.016";
        public const string SourceAssetRoot = "Assets/_Project/Resources/Fonts/Roboto/v3.016";
        public const string PresetRoot = "Assets/_Project/UI/Materials/Roboto/v3.016";
        public const string DistributedNotice = "Assets/StreamingAssets/ThirdParty/Roboto/OFL.txt";
        public const string RobotoProfilePath = FontRoot + "/RobotoTypography.asset";
        public const string ArchiveRoot = "Assets/_Project/UI/DesignArchive/BeforeRoboto20261029";
        public const string BackupRoot = "BuildNotes/play-organization-20261029/typography/before-roboto";
        static readonly string[] Names = { "Roboto-Medium", "Roboto-Regular", "Roboto-Bold" };
        static readonly string[] SourceHashes = {
            "80ce163ec5ecd91a883aa20e80d31dc74971b55cf658dd7c5e8d21f4e9fcb417",
            "9f300202f482ad59f8b13bc3131a295744d14fd530fa3765a0a11ec87264d203",
            "44f13e570a8b3fc42240abfcb3d72fe298f3dd6a25db0c78d0c2495dbd0e9121"
        };
        const string LicenseHash = "061402327a96aadb0bfb694a960ed289ecd38d383e396243831ab81feb109c41";
        static readonly string[] Frames = {
            "Assets/_Project/Prefabs/UI/Home/HomePage.prefab",
            "Assets/_Project/Prefabs/UI/Play/PlayPage.prefab",
            "Assets/_Project/Prefabs/UI/Settings/SettingsPage.prefab",
            "Assets/_Project/Prefabs/UI/Loadout/LoadoutPage.prefab",
            "Assets/_Project/Prefabs/UI/Loadout/RecipeTile.prefab",
            "Assets/_Project/Prefabs/UI/Shop/ShopPage.prefab",
            "Assets/_Project/Prefabs/UI/Shop/ShopOfferCard.prefab"
        };

        [MenuItem("Wreckabulary/Authoring/Apply Roboto Typography")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Save typography outside Play mode.");
            var active = AssetDatabase.LoadAssetAtPath<LobbyTypographyProfile>(LobbyTypographySetup.ProfilePath);
            if (!active || !active.TryValidate(out _) || EditorUtility.IsDirty(active))
                throw new InvalidOperationException("Save a valid current typography profile before migration.");
            var ready = AssetDatabase.LoadAssetAtPath<LobbyTypographyProfile>(RobotoProfilePath);
            if (ready && active.Display == ready.Display && active.Body == ready.Body && active.Black == ready.Black)
            {
                ValidateSaved(active);
                ValidateFrames(active);
                Debug.Log("[Wreckabulary] Saved Roboto typography verified; existing edits retained.");
                return;
            }
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            foreach (string path in Frames)
            {
                var frame = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (!frame || EditorUtility.IsDirty(frame) || frame.GetComponentsInChildren<Component>(true).Any(c => c && EditorUtility.IsDirty(c)) ||
                    stage != null && stage.assetPath == path)
                    throw new InvalidOperationException("Save and close Prefab Mode before migration: " + path);
            }
            for (int i = 0; i < Names.Length; i++) RequireHash(SourceRoot + "/" + Names[i] + ".ttf", SourceHashes[i]);
            RequireHash(SourceRoot + "/OFL.txt", LicenseHash);
            foreach (string path in new[] { ArchiveRoot, BackupRoot, FontRoot, PresetRoot, SourceAssetRoot })
                RequireVacant(path);
            if (File.Exists(DistributedNotice)) RequireHash(DistributedNotice, LicenseHash);
            else RequireVacant(DistributedNotice);

            Directory.CreateDirectory(BackupRoot);
            foreach (string path in Frames.Append(LobbyTypographySetup.ProfilePath))
                BackupExact(path);
            File.WriteAllLines(BackupRoot + "/SHA256SUMS.txt", Frames.Append(LobbyTypographySetup.ProfilePath)
                .SelectMany(path => new[] { path, path + ".meta" }).Select(path => Hash(path) + "  " + path));
            EnsureFolder(ArchiveRoot);
            foreach (string path in Frames.Append(LobbyTypographySetup.ProfilePath))
                if (!AssetDatabase.CopyAsset(path, ArchiveRoot + "/" + Path.GetFileName(path)))
                    throw new IOException("Could not preserve editable asset: " + path);

            var profile = CreateRoboto();
            foreach (string path in Frames)
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    Restyle(root, profile);
                    if (root.TryGetComponent(out LobbyPlayPageView play))
                    {
                        play.ArrangeOptions();
                        play.Validate();
                        EditorUtility.SetDirty(play);
                    }
                    if (!PrefabUtility.SaveAsPrefabAsset(root, path)) throw new IOException("Unable to save typography: " + path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            ValidateFrames(profile);
            EditorUtility.CopySerialized(profile, active);
            active.name = "LobbyTypography";
            EditorUtility.SetDirty(active);
            AssetDatabase.SaveAssetIfDirty(active);
            ValidateSaved(active);
            Debug.Log("[Wreckabulary] Saved Roboto: Regular body, Medium headings, Bold emphasis. Previous editable frames/profile: " + ArchiveRoot + "; exact original bytes: " + BackupRoot);
        }

        static LobbyTypographyProfile CreateRoboto()
        {
            EnsureFolder(SourceAssetRoot); EnsureFolder(FontRoot); EnsureFolder(PresetRoot);
            for (int i = 0; i < Names.Length; i++)
            {
                string target = SourceAssetRoot + "/" + Names[i] + ".ttf";
                File.Copy(SourceRoot + "/" + Names[i] + ".ttf", target, false);
                RequireHash(target, SourceHashes[i]);
                AssetDatabase.ImportAsset(target, ImportAssetOptions.ForceSynchronousImport);
            }
            File.Copy(SourceRoot + "/OFL.txt", SourceAssetRoot + "/OFL.txt", false);
            AssetDatabase.ImportAsset(SourceAssetRoot + "/OFL.txt", ImportAssetOptions.ForceSynchronousImport);
            if (!File.Exists(DistributedNotice))
            {
                EnsureFolder("Assets/StreamingAssets/ThirdParty/Roboto");
                File.Copy(SourceRoot + "/OFL.txt", DistributedNotice, false);
                AssetDatabase.ImportAsset(DistributedNotice, ImportAssetOptions.ForceSynchronousImport);
            }
            RequireHash(DistributedNotice, LicenseHash);
            var owned = new List<Object>();
            try
            {
                var profile = ScriptableObject.CreateInstance<LobbyTypographyProfile>(); owned.Add(profile);
                profile.name = "RobotoTypography";
                var fonts = Names.Select(name => MakeFont(name, owned)).ToArray();
                profile.Display = fonts[0]; profile.Body = fonts[1]; profile.Black = fonts[2];
                fonts[0].fontWeightTable[7].regularTypeface = fonts[0];
                fonts[1].fontWeightTable[7].regularTypeface = fonts[2];
                fonts[2].fontWeightTable[7].regularTypeface = fonts[2];
                foreach (var font in fonts)
                {
                    AssetDatabase.CreateAsset(font, FontRoot + "/" + font.name + ".asset");
                    foreach (var atlas in font.atlasTextures) if (atlas && !AssetDatabase.Contains(atlas)) AssetDatabase.AddObjectToAsset(atlas, font);
                    if (!AssetDatabase.Contains(font.material)) AssetDatabase.AddObjectToAsset(font.material, font);
                }
                fonts[0].fontWeightTable[7].regularTypeface = fonts[0];
                fonts[1].fontWeightTable[7].regularTypeface = fonts[2];
                fonts[2].fontWeightTable[7].regularTypeface = fonts[2];
                foreach (var font in fonts) { EditorUtility.SetDirty(font); AssetDatabase.SaveAssetIfDirty(font); }
                profile.Stroke = Preset(fonts[0], "Roboto Display Stroke", .055f, false, owned);
                profile.Drop = Preset(fonts[0], "Roboto Display Drop", 0f, true, owned);
                profile.DisplayForeground = Preset(fonts[0], "Roboto Display Foreground", .035f, false, owned);
                profile.BodyForeground = Preset(fonts[1], "Roboto Body Foreground", .035f, false, owned);
                profile.BlackForeground = Preset(fonts[2], "Roboto Bold Foreground", .035f, false, owned);
                if (!profile.TryValidate(out string reason)) throw new InvalidOperationException(reason);
                AssetDatabase.CreateAsset(profile, RobotoProfilePath);
                AssetDatabase.SaveAssetIfDirty(profile);
                ValidateSaved(profile);
                return profile;
            }
            finally
            {
                foreach (var item in owned) if (item && !EditorUtility.IsPersistent(item)) Object.DestroyImmediate(item);
            }
        }

        static TMP_FontAsset MakeFont(string name, List<Object> owned)
        {
            var source = AssetDatabase.LoadAssetAtPath<Font>(SourceAssetRoot + "/" + name + ".ttf");
            if (!source) throw new InvalidOperationException("Unable to import font: " + name);
            var font = TMP_FontAsset.CreateFontAsset(source, 80, 10, GlyphRenderMode.SDFAA, 2048, 2048);
            if (!font) throw new InvalidOperationException("TMP font creation failed: " + name);
            owned.Add(font); owned.Add(font.material); font.name = name + " SDF";
            font.isMultiAtlasTexturesEnabled = true;
            if (!font.TryAddCharacters(LobbyTypographyProfile.InitialCharacters, out string missing))
                throw new InvalidOperationException("Font is missing required initial characters: " + name + ": " + missing);
            foreach (var atlas in font.atlasTextures) if (atlas) { owned.Add(atlas); atlas.filterMode = FilterMode.Bilinear; }
            if (GameAssets.I && GameAssets.I.font) font.fallbackFontAssetTable = new List<TMP_FontAsset> { GameAssets.I.font };
            font.creationSettings = new FontAssetCreationSettings {
                sourceFontFileName = source.name, sourceFontFileGUID = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(source)),
                pointSize = 80, padding = 10, paddingMode = 2, atlasWidth = 2048, atlasHeight = 2048,
                characterSetSelectionMode = 7, characterSequence = LobbyTypographyProfile.InitialCharacters, renderMode = (int)GlyphRenderMode.SDFAA
            };
            var data = new SerializedObject(font);
            var clear = data.FindProperty("m_ClearDynamicDataOnBuild");
            if (clear == null) throw new InvalidOperationException("TMP build preservation setting unavailable.");
            clear.boolValue = false; data.ApplyModifiedPropertiesWithoutUndo();
            ShaderUtilities.UpdateShaderRatios(font.material);
            return font;
        }

        static Material Preset(TMP_FontAsset font, string name, float outline, bool shadow, List<Object> owned)
        {
            var material = new Material(font.material) { name = name }; owned.Add(material);
            material.SetFloat("_FaceDilate", 0);
            material.SetFloat("_OutlineWidth", outline);
            material.SetColor("_OutlineColor", name.EndsWith("Foreground", StringComparison.Ordinal) ? LobbyKit.PanelTop : LobbyKit.Navy);
            if (outline > 0) material.EnableKeyword(ShaderUtilities.Keyword_Outline);
            else material.DisableKeyword(ShaderUtilities.Keyword_Outline);
            if (shadow)
            {
                material.EnableKeyword(ShaderUtilities.Keyword_Underlay);
                material.SetColor("_UnderlayColor", new Color(.08f, .13f, .18f, .4f));
                material.SetFloat("_UnderlayOffsetY", -.18f); material.SetFloat("_UnderlaySoftness", .08f);
                material.SetFloat("_UnderlayDilate", 0);
            }
            else material.DisableKeyword(ShaderUtilities.Keyword_Underlay);
            ShaderUtilities.UpdateShaderRatios(material);
            AssetDatabase.CreateAsset(material, PresetRoot + "/" + name + ".mat");
            AssetDatabase.SaveAssetIfDirty(material);
            return material;
        }

        static void Restyle(GameObject root, LobbyTypographyProfile profile)
        {
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                bool heading = text.name == "Title" || text.name == "Header";
                bool action = text.GetComponentInParent<LobbyShopActionView>() || (text.fontStyle & FontStyles.Bold) != 0;
                text.font = heading ? profile.Display : action ? profile.Black : profile.Body;
                text.fontSharedMaterial = text.font.material;
                text.fontStyle &= ~FontStyles.Bold;
                text.characterSpacing = 0;
                text.UpdateMeshPadding();
            }
            foreach (var panel in root.GetComponentsInChildren<RectTransform>(true).Where(t => t.name == "Panel"))
            {
                if (panel.TryGetComponent(out LobbyGradient gradient)) gradient.Set(LobbyKit.PanelTop, LobbyKit.PanelBottom);
                else if (panel.TryGetComponent(out Image face)) face.color = LobbyKit.PanelTop;
            }
        }

        static void ValidateSaved(LobbyTypographyProfile profile)
        {
            RequireHash(DistributedNotice, LicenseHash);
            if (!profile.TryValidate(out string reason)) throw new InvalidOperationException(reason);
            foreach (var font in new[] { profile.Display, profile.Body, profile.Black })
                if (!EditorUtility.IsPersistent(font) || !EditorUtility.IsPersistent(font.material) || !EditorUtility.IsPersistent(font.sourceFontFile) ||
                    font.atlasTextures.Any(a => a && !EditorUtility.IsPersistent(a)) ||
                    font.fallbackFontAssetTable != null && font.fallbackFontAssetTable.Any(f => f && !EditorUtility.IsPersistent(f)))
                    throw new InvalidOperationException("Typography dependencies must remain saved and editable.");
            foreach (var material in new[] { profile.Stroke, profile.Drop, profile.DisplayForeground, profile.BodyForeground, profile.BlackForeground })
                if (!EditorUtility.IsPersistent(material)) throw new InvalidOperationException("Typography presets must remain saved.");
        }

        static void ValidateFrames(LobbyTypographyProfile profile)
        {
            var fonts = new[] { profile.Display, profile.Body, profile.Black };
            foreach (string path in Frames)
                foreach (var text in AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentsInChildren<TMP_Text>(true))
                    if (!fonts.Contains(text.font) || !EditorUtility.IsPersistent(text.fontSharedMaterial) || text.fontSharedMaterial.mainTexture != text.font.atlasTexture)
                        throw new InvalidOperationException("Saved frame has unresolved typography: " + path + ": " + text.name);
        }

        static void BackupExact(string path)
        {
            foreach (string source in new[] { path, path + ".meta" })
            {
                string target = BackupRoot + "/" + source;
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(source, target, false);
                RequireHash(target, Hash(source));
            }
        }

        static string Hash(string path)
        {
            using var stream = File.OpenRead(path);
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }

        static void RequireHash(string path, string expected)
        {
            if (!File.Exists(path) || Hash(path) != expected) throw new IOException("Official font or exact backup hash mismatch: " + path);
        }

        static void RequireVacant(string path)
        {
            if (File.Exists(path) || File.Exists(path + ".meta") || Directory.Exists(path) || !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path)))
                throw new IOException("Migration target already exists; inspect preserved work before retrying: " + path);
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int split = path.LastIndexOf('/');
            if (split < 0) throw new IOException("Expected an Assets folder path.");
            EnsureFolder(path.Substring(0, split)); RequireVacant(path);
            if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(path.Substring(0, split), path.Substring(split + 1))))
                throw new IOException("Unable to create asset folder: " + path);
        }
    }
}
