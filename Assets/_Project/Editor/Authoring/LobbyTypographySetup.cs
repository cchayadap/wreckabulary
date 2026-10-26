using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using Object = UnityEngine.Object;

namespace Wreckabulary.EditorTools
{
    public static class LobbyTypographySetup
    {
        public const string ProfilePath = "Assets/_Project/Resources/UI/LobbyTypography.asset";
        public const string FontFolder = "Assets/_Project/UI/Fonts";
        public const string MaterialFolder = "Assets/_Project/UI/Materials";
        static readonly string[] FontNames = { "LilitaOne-Regular", "Nunito-ExtraBold", "Nunito-Black" };
        static readonly string[] PresetNames = { "Lobby Display Stroke", "Lobby Display Drop", "Lobby Display Foreground", "Lobby Body Foreground", "Lobby Black Foreground" };

        [MenuItem("Wreckabulary/Authoring/Create or Verify Lobby Typography")]
        static void CreateFromMenu()
        {
            var profile = CreateOrVerify();
            if (profile) { Selection.activeObject = profile; EditorGUIUtility.PingObject(profile); }
        }

        public static LobbyTypographyProfile CreateOrVerify() => CreateOrVerifyAt(ProfilePath, FontFolder, MaterialFolder);

        static LobbyTypographyProfile CreateOrVerifyAt(string profilePath, string fontFolder, string materialFolder)
        {
            var generated = new List<Object>();
            try
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                    throw new InvalidOperationException("Create or verify saved lobby typography outside Play mode.");
                CheckAssetPath(profilePath); CheckAssetPath(fontFolder); CheckAssetPath(materialFolder);
                var existing = AssetDatabase.LoadMainAssetAtPath(profilePath);
                if (existing)
                {
                    if (existing is not LobbyTypographyProfile profile)
                        throw new InvalidOperationException("The typography profile path is occupied by another asset: " + profilePath);
                    if (!profile.TryValidate(out string reason)) throw new InvalidOperationException("Authored profile needs attention: " + reason);
                    if (!PersistentDependencies(profile, out reason)) throw new InvalidOperationException(reason);
                    return profile;
                }

                var fontPaths = FontNames.Select(name => fontFolder + "/" + name + " SDF.asset").ToArray();
                var presetPaths = PresetNames.Select(name => materialFolder + "/" + name + ".mat").ToArray();
                var paths = fontPaths.Concat(presetPaths).Append(profilePath).ToArray();
                if (paths.Distinct(StringComparer.OrdinalIgnoreCase).Count() != paths.Length)
                    throw new InvalidOperationException("Typography target paths must be distinct.");
                foreach (string path in paths) RequireVacant(path);
                foreach (string path in paths) CheckFolders(Path.GetDirectoryName(path).Replace('\\', '/'));
                var sources = FontNames.Select(name => AssetDatabase.LoadAssetAtPath<Font>("Assets/_Project/Resources/Fonts/" + name + ".ttf")).ToArray();
                for (int i = 0; i < sources.Length; i++)
                    if (!sources[i]) throw new InvalidOperationException("Missing original lobby source font: " + FontNames[i]);

                var created = ScriptableObject.CreateInstance<LobbyTypographyProfile>();
                created.name = "LobbyTypography"; generated.Add(created);
                var fonts = sources.Select((source, i) => MakeFont(source, FontNames[i], i == 0 ? 96 : 80, i == 0 ? 12 : 10, generated)).ToArray();
                created.Display = fonts[0]; created.Body = fonts[1]; created.Black = fonts[2];
                fonts[0].fontWeightTable[7].regularTypeface = fonts[0];
                fonts[1].fontWeightTable[7].regularTypeface = fonts[2];
                fonts[2].fontWeightTable[7].regularTypeface = fonts[2];
                var materials = new[] { MakePreset(fonts[0], false, false), MakePreset(fonts[0], true, false),
                    MakePreset(fonts[0], false, true), MakePreset(fonts[1], false, true), MakePreset(fonts[2], false, true) };
                generated.AddRange(materials);
                created.Stroke = materials[0]; created.Drop = materials[1]; created.DisplayForeground = materials[2];
                created.BodyForeground = materials[3]; created.BlackForeground = materials[4];
                if (!created.TryValidate(out string invalid)) throw new InvalidOperationException("Generated defaults failed validation: " + invalid);

                foreach (string path in paths) EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
                for (int i = 0; i < fonts.Length; i++)
                {
                    RequireVacant(fontPaths[i]);
                    AssetDatabase.CreateAsset(fonts[i], fontPaths[i]);
                    foreach (var atlas in fonts[i].atlasTextures)
                        if (atlas && !AssetDatabase.Contains(atlas)) AssetDatabase.AddObjectToAsset(atlas, fonts[i]);
                    if (!AssetDatabase.Contains(fonts[i].material)) AssetDatabase.AddObjectToAsset(fonts[i].material, fonts[i]);
                    EditorUtility.SetDirty(fonts[i]);
                }
                for (int i = 0; i < materials.Length; i++)
                {
                    RequireVacant(presetPaths[i]);
                    materials[i].name = PresetNames[i];
                    AssetDatabase.CreateAsset(materials[i], presetPaths[i]);
                }
                fonts[0].fontWeightTable[7].regularTypeface = fonts[0];
                fonts[1].fontWeightTable[7].regularTypeface = fonts[2];
                fonts[2].fontWeightTable[7].regularTypeface = fonts[2];
                foreach (var value in generated.Where(EditorUtility.IsPersistent))
                { EditorUtility.SetDirty(value); AssetDatabase.SaveAssetIfDirty(value); }
                if (!PersistentDependencies(created, out invalid)) throw new InvalidOperationException(invalid);
                RequireVacant(profilePath);
                AssetDatabase.CreateAsset(created, profilePath);
                AssetDatabase.SaveAssetIfDirty(created);
                foreach (string path in paths) AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                var saved = AssetDatabase.LoadAssetAtPath<LobbyTypographyProfile>(profilePath);
                if (!saved || !saved.TryValidate(out invalid)) throw new InvalidOperationException("Saved typography failed validation: " + invalid);
                if (!PersistentDependencies(saved, out invalid)) throw new InvalidOperationException(invalid);
                return saved;
            }
            catch (Exception error)
            {
                Debug.LogWarning("Lobby typography setup stopped without replacing authored assets. " + error.Message +
                    " Inspect the profile and target paths before retrying; any assets already created are retained.");
                return null;
            }
            finally
            {
                foreach (var value in generated)
                    if (value && !EditorUtility.IsPersistent(value)) Object.DestroyImmediate(value);
            }
        }

        static TMP_FontAsset MakeFont(Font source, string name, int size, int padding, List<Object> generated)
        {
            var font = TMP_FontAsset.CreateFontAsset(source, size, padding, GlyphRenderMode.SDFAA, 2048, 2048);
            if (!font) throw new InvalidOperationException("Unable to create " + name + "; check Include Font Data in its importer.");
            generated.Add(font); font.name = name;
            font.isMultiAtlasTexturesEnabled = true;
            font.TryAddCharacters(LobbyTypographyProfile.InitialCharacters);
            foreach (var atlas in font.atlasTextures) if (atlas) atlas.filterMode = FilterMode.Bilinear;
            if (GameAssets.I && GameAssets.I.font) font.fallbackFontAssetTable = new List<TMP_FontAsset> { GameAssets.I.font };
            font.creationSettings = new FontAssetCreationSettings {
                sourceFontFileName = source.name, sourceFontFileGUID = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(source)),
                pointSize = size, padding = padding, paddingMode = 2, atlasWidth = 2048, atlasHeight = 2048,
                characterSetSelectionMode = 7, characterSequence = LobbyTypographyProfile.InitialCharacters, renderMode = (int)GlyphRenderMode.SDFAA };
            var serialized = new SerializedObject(font);
            var clearOnBuild = serialized.FindProperty("m_ClearDynamicDataOnBuild");
            if (clearOnBuild == null) throw new InvalidOperationException("The installed TMP font build settings are unavailable.");
            clearOnBuild.boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return font;
        }

        static Material MakePreset(TMP_FontAsset font, bool drop, bool foreground)
        {
            var material = new Material(font.material);
            material.EnableKeyword(ShaderUtilities.Keyword_Outline);
            material.SetColor("_OutlineColor", foreground ? new Color(1f, .976f, .914f, .96f) : LobbyKit.Navy);
            material.SetFloat("_OutlineWidth", foreground ? .18f : .28f);
            material.SetFloat("_FaceDilate", foreground ? .02f : .28f);
            if (drop)
            {
                material.EnableKeyword(ShaderUtilities.Keyword_Underlay);
                material.SetColor("_UnderlayColor", LobbyKit.Navy);
                material.SetFloat("_UnderlayOffsetX", 0f);
                material.SetFloat("_UnderlayOffsetY", -.55f);
                material.SetFloat("_UnderlayDilate", .28f);
                material.SetFloat("_UnderlaySoftness", 0f);
            }
            ShaderUtilities.UpdateShaderRatios(material);
            return material;
        }

        static bool PersistentDependencies(LobbyTypographyProfile profile, out string reason)
        {
            foreach (var font in new[] { profile.Display, profile.Body, profile.Black })
            {
                if (!EditorUtility.IsPersistent(font) || !EditorUtility.IsPersistent(font.material) ||
                    font.atlasTextures.Any(atlas => atlas && !EditorUtility.IsPersistent(atlas)) ||
                    font.sourceFontFile && !EditorUtility.IsPersistent(font.sourceFontFile) ||
                    font.fallbackFontAssetTable != null && font.fallbackFontAssetTable.Any(fallback => fallback && !EditorUtility.IsPersistent(fallback)))
                { reason = "A font, atlas, base material, source or fallback has an unsaved reference; save it explicitly before verifying."; return false; }
            }
            foreach (var material in new[] { profile.Stroke, profile.Drop, profile.DisplayForeground, profile.BodyForeground, profile.BlackForeground })
                if (!EditorUtility.IsPersistent(material))
                { reason = "A typography preset is unsaved; save it explicitly before verifying."; return false; }
            reason = null;
            return true;
        }

        static void CheckAssetPath(string path)
        {
            if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/", StringComparison.Ordinal) ||
                path.Contains('\\') || path.Split('/').Any(part => part == ".." || part == "." || part.Length == 0))
                throw new InvalidOperationException("Typography targets must be explicit project asset paths.");
        }

        static void RequireVacant(string path)
        {
            if (AssetDatabase.LoadMainAssetAtPath(path) || !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path)) ||
                File.Exists(path) || File.Exists(path + ".meta") || Directory.Exists(path))
                throw new InvalidOperationException("Typography target is already occupied; it will not be overwritten: " + path);
        }

        static void CheckFolders(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            RequireVacant(path);
            int split = path.LastIndexOf('/');
            if (split < 0) throw new InvalidOperationException("The project Assets folder is unavailable.");
            CheckFolders(path.Substring(0, split));
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int split = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, split));
            RequireVacant(path);
            if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(path.Substring(0, split), path.Substring(split + 1))))
                throw new InvalidOperationException("Unable to create typography folder: " + path);
        }
    }
}
