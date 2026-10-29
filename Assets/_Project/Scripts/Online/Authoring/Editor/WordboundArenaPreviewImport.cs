using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Wreckabulary.Online.Editor
{
    public static class WordboundArenaPreviewImport
    {
        const string Output = "Assets/_Project/UI/Previews/Wordbound";
        const string Field = "CombatArenaPreview";

        [MenuItem("Tools/Wordbound/Presentation/Import reviewed actual Arena preview")]
        public static void ImportReviewed()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode && !WordboundSoloPreviewWindow.IsOpen,
                "Close Play and the framing window before importing the reviewed arena image.");
            string path = Environment.GetEnvironmentVariable("WORDBOUND_ARENA_PREVIEW_RECEIPT");
            if (string.IsNullOrWhiteSpace(path) && !Application.isBatchMode)
                path = EditorUtility.OpenFilePanel("Choose the arena receipt whose image you reviewed", WordboundArenaPreviewCapture.CaptureRoot, "json");
            Require(!string.IsNullOrWhiteSpace(path), "Provide the reviewed receipt path; batch mode requires WORDBOUND_ARENA_PREVIEW_RECEIPT.");
            path = Path.GetFullPath(path); string folder = Path.GetDirectoryName(path);
            Require(Path.GetFileName(path) == "receipt.json" && string.Equals(Path.GetDirectoryName(folder), Path.GetFullPath(WordboundArenaPreviewCapture.CaptureRoot), StringComparison.OrdinalIgnoreCase),
                "Select a direct actual arena capture receipt from this project.");
            WordboundSoloPreviewCapture.RejectLinks(folder); WordboundSoloPreviewCapture.RejectLinks(Path.GetFullPath(Output));
            Require(File.Exists(path) && new FileInfo(path).Length <= 8 * 1024 * 1024, "Arena receipt is missing or oversized.");
            var receipt = JObject.Parse(File.ReadAllText(path));
            Require((int?)receipt["schema"] == 1 && (string)receipt["kind"] == "wreckabulary.arena-preview" &&
                (string)receipt["status"] == "CAPTURED_NOT_VISUALLY_APPROVED" && receipt["errors"] is JArray errors && errors.Count == 0 &&
                (int?)receipt["width"] == WordboundSoloPreviewCapture.Width && (int?)receipt["height"] == WordboundSoloPreviewCapture.Height &&
                (string)receipt["frame"]?["filename"] == WordboundArenaPreviewCapture.ImageName,
                "Only a complete unchanged actual arena image receipt can be imported.");
            string png = Path.Combine(folder, WordboundArenaPreviewCapture.ImageName);
            Require(File.Exists(png) && new FileInfo(png).Length <= 16 * 1024 * 1024, "Arena image is missing or oversized.");
            byte[] image = File.ReadAllBytes(png); string imageHash = WordboundSoloPreviewCapture.Hash(image);
            Require(imageHash == (string)receipt["frame"]?["sha256"], "Arena image changed after capture.");
            string target = Output + "/ToyboxArena-" + imageHash.Substring(0, 16) + ".png";
            var layout = AssetDatabase.LoadAssetAtPath<LobbyPlayLayout>(WordboundArenaPreviewCapture.LayoutPath);
            Require(layout && !EditorUtility.IsDirty(layout), "Save the current Play layout before importing."); layout.Validate();
            using var serialized = new SerializedObject(layout);
            var property = serialized.FindProperty(Field);
            Require(property != null && property.propertyType == SerializedPropertyType.ObjectReference,
                "Integrate the optional CombatArenaPreview layout field before importing.");
            var importer = AssetImporter.GetAtPath(WordboundArenaPreviewCapture.LayoutPath);
            Require(!importer || !EditorUtility.IsDirty(importer), "Save pending layout import changes first.");
            string complete = folder + "/import-receipt.json";
            if (File.Exists(complete))
            {
                var done = JObject.Parse(File.ReadAllText(complete));
                Require((string)done["status"] == "IMPORTED_REQUIRES_MENU_REVIEW" && (string)done["captureReceiptSha256"] == WordboundSoloPreviewCapture.FileHash(path) &&
                    (string)done["imageSha256"] == imageHash && (string)done["imagePath"] == target && File.Exists(target) &&
                    WordboundSoloPreviewCapture.FileHash(target) == imageHash && AssetDatabase.AssetPathToGUID(target) == (string)done["imageGuid"],
                    "Existing import evidence/image differs; existing files were preserved.");
                Debug.Log("[Arena preview] Completed import verified without writes. Current owner layout assignments/edits are preserved: " + target);
                return;
            }
            string backup = folder + "/before-import";
            Require(!Directory.Exists(backup) && !File.Exists(backup), "A previous attempt retained its backup; inspect partial output before retrying.");
            Require(!File.Exists(target) && !File.Exists(target + ".meta") && !AssetDatabase.LoadMainAssetAtPath(target), "Immutable arena image target is already occupied.");
            var data = Resources.Load<GameData>(GameData.ResourcePath);
            var launch = Resources.Load<WordboundLaunchAssets>(WordboundLaunchAssets.ResourcePath);
            Require(data && data.items && data.rules && launch && launch.VisualProfile, "Installed arena inputs are missing.");
            var content = WordboundArenaPreviewCapture.Content(data, launch);
            Require((string)receipt["seed"] == WordboundArenaPreviewCapture.Seed && (string)receipt["mode"] == "FreeForAll" &&
                (string)receipt["format"] == "Solo" && (string)receipt["courseId"] == "arena-proof" &&
                (string)receipt["sourceHash"] == content.Course.SourceHash && (string)receipt["manifestHash"] == content.Course.Hash &&
                (string)receipt["geometryHash"] == content.Geometry.Hash && (string)receipt["contentHash"] == content.PublicBinding.CatalogHash &&
                (string)receipt["sourceFingerprint"] == WordboundSoloPreviewCapture.SourceFingerprint(), "Arena content/source differs from capture; recapture before import.");
            var inputs = receipt["inputs"]?.ToObject<Dictionary<string, string>>();
            Require(inputs != null, "Capture input roster is missing.");
            var current = WordboundArenaPreviewCapture.Inputs(data, launch);
            Require(inputs.Count == current.Count && inputs.All(pair => current.TryGetValue(pair.Key, out string hash) && hash == pair.Value),
                "The complete arena input roster changed; recapture before import.");
            WordboundArenaPreviewCapture.Verify(inputs);
            string scene = WordboundArenaPreviewCapture.SceneState(), other = OtherFields(layout), metaHash = WordboundSoloPreviewCapture.FileHash(WordboundArenaPreviewCapture.LayoutPath + ".meta");
            Directory.CreateDirectory(backup);
            foreach (string source in new[] { WordboundArenaPreviewCapture.LayoutPath, WordboundArenaPreviewCapture.LayoutPath + ".meta" })
            {
                string destination = backup + "/" + Path.GetFileName(source);
                File.Copy(source, destination, false);
                Require(WordboundSoloPreviewCapture.FileHash(source) == WordboundSoloPreviewCapture.FileHash(destination), "Exact pre-import layout backup failed.");
            }
            WordboundArenaPreviewCapture.Write(backup + "/manifest.json", JsonConvert.SerializeObject(new {
                layoutPath = WordboundArenaPreviewCapture.LayoutPath, layoutSha256 = WordboundSoloPreviewCapture.FileHash(WordboundArenaPreviewCapture.LayoutPath),
                layoutMetaSha256 = metaHash, otherFields = other, captureSha256 = WordboundSoloPreviewCapture.FileHash(path)
            }, Formatting.Indented));
            try
            {
                WordboundSoloPreviewCapture.EnsureFolder(Output);
                WordboundSoloPreviewCapture.WriteNew(target, image);
                AssetDatabase.ImportAsset(target, ImportAssetOptions.ForceSynchronousImport);
                var texture = AssetImporter.GetAtPath(target) as TextureImporter;
                Require(texture, "Expected a PNG texture importer.");
                texture.textureType = TextureImporterType.Sprite; texture.spriteImportMode = SpriteImportMode.Single;
                texture.sRGBTexture = true; texture.alphaSource = TextureImporterAlphaSource.None; texture.alphaIsTransparency = false;
                texture.mipmapEnabled = false; texture.isReadable = false; texture.wrapMode = TextureWrapMode.Clamp;
                texture.filterMode = FilterMode.Bilinear; texture.textureCompression = TextureImporterCompression.Uncompressed;
                texture.maxTextureSize = 1024; texture.npotScale = TextureImporterNPOTScale.None;
                var settings = new TextureImporterSettings(); texture.ReadTextureSettings(settings); settings.spriteMeshType = SpriteMeshType.FullRect; texture.SetTextureSettings(settings);
                texture.SaveAndReimport();
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(target);
                Require(sprite && sprite.texture.width == WordboundSoloPreviewCapture.Width && sprite.texture.height == WordboundSoloPreviewCapture.Height &&
                    WordboundSoloPreviewCapture.FileHash(target) == imageHash, "Imported arena image size/bytes changed.");
                Undo.RecordObject(layout, "Assign reviewed actual Arena preview");
                serialized.Update(); serialized.FindProperty(Field).objectReferenceValue = sprite; serialized.ApplyModifiedProperties();
                EditorUtility.SetDirty(layout); AssetDatabase.SaveAssetIfDirty(layout);
                Require(OtherFields(layout) == other && WordboundSoloPreviewCapture.FileHash(WordboundArenaPreviewCapture.LayoutPath + ".meta") == metaHash &&
                    WordboundArenaPreviewCapture.SceneState() == scene, "Unrelated layout fields, GUID or scene state changed. Exact backup retained.");
                WordboundArenaPreviewCapture.Verify(inputs, true);
                WordboundArenaPreviewCapture.Write(complete, JsonConvert.SerializeObject(new {
                    status = "IMPORTED_REQUIRES_MENU_REVIEW", utc = DateTime.UtcNow.ToString("O"), captureReceiptSha256 = WordboundSoloPreviewCapture.FileHash(path),
                    imagePath = target, imageSha256 = imageHash, imageGuid = AssetDatabase.AssetPathToGUID(target),
                    layoutPath = WordboundArenaPreviewCapture.LayoutPath, layoutSha256 = WordboundSoloPreviewCapture.FileHash(WordboundArenaPreviewCapture.LayoutPath),
                    layoutMetaSha256 = metaHash, otherFieldsPreserved = true, backup
                }, Formatting.Indented));
                Selection.activeObject = layout; EditorGUIUtility.PingObject(layout);
                Debug.Log("[Arena preview] Reviewed immutable arena image assigned; only CombatArenaPreview changed. Backup: " + backup);
            }
            catch (Exception error) { throw new InvalidOperationException("Arena import stopped. Preserve partial outputs and exact backup: " + backup, error); }
        }
        static string OtherFields(LobbyPlayLayout layout)
        {
            var data = JObject.Parse(EditorJsonUtility.ToJson(layout));
            var body = data["MonoBehaviour"] as JObject;
            Require(data.Count == 1 && body != null && body.Remove(Field), "Expected the serialized Play layout wrapper and arena field.");
            return data.ToString(Formatting.None);
        }
        static void Require(bool valid, string message) => WordboundArenaPreviewCapture.Require(valid, message);
    }
}
