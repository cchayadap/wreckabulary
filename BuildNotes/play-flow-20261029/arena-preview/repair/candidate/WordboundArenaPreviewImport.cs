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
        [MenuItem("Tools/Wordbound/Presentation/Verify retained Arena import")]
        public static void FinalizeRetainedImport()
        {
            const string repairRoot = "BuildNotes/play-flow-20261029/arena-preview/repair";
            const string importerPath = "Assets/_Project/Scripts/Online/Authoring/Editor/WordboundArenaPreviewImport.cs";
            const string folder = "BuildNotes/play-flow-20261029/arena-preview/native/20261029T033724Z-be0375ba";
            string path = folder + "/receipt.json", backup = folder + "/before-import";
            Require(!EditorApplication.isPlayingOrWillChangePlaymode && !WordboundSoloPreviewWindow.IsOpen,
                "Close Play and the framing window before verifying retained import output.");
            string sceneBeforeVerification = WordboundArenaPreviewCapture.SceneState();
            WordboundSoloPreviewCapture.RejectLinks(Path.GetFullPath(folder));
            WordboundSoloPreviewCapture.RejectLinks(Path.GetFullPath(repairRoot));
            WordboundSoloPreviewCapture.RejectLinks(Path.GetFullPath(Output));
            var repair = JObject.Parse(File.ReadAllText(repairRoot + "/manifest.json"));
            Require((int?)repair["schema"] == 1 && (string)repair["kind"] == "wreckabulary.arena-preview-import-repair" &&
                (string)repair["captureReceiptPath"] == path && (string)repair["importerPath"] == importerPath,
                "Expected the exact reviewed bounded repair manifest.");
            Require(WordboundSoloPreviewCapture.FileHash(path) == (string)repair["captureReceiptSha256"], "Original capture receipt changed.");
            var receipt = JObject.Parse(File.ReadAllText(path));
            Require((int?)receipt["schema"] == 1 && (string)receipt["kind"] == "wreckabulary.arena-preview" &&
                (string)receipt["status"] == "CAPTURED_NOT_VISUALLY_APPROVED" && receipt["errors"] is JArray errors && errors.Count == 0 &&
                (int?)receipt["width"] == WordboundSoloPreviewCapture.Width && (int?)receipt["height"] == WordboundSoloPreviewCapture.Height &&
                (string)receipt["frame"]?["filename"] == WordboundArenaPreviewCapture.ImageName, "Incomplete original capture.");
            string imageHash = (string)receipt["frame"]?["sha256"];
            Require(imageHash != null && imageHash.Length == 64, "Invalid captured image hash.");
            string target = Output + "/ToyboxArena-" + imageHash.Substring(0, 16) + ".png";
            Require(target == (string)repair["imagePath"] && WordboundSoloPreviewCapture.FileHash(folder + "/" + WordboundArenaPreviewCapture.ImageName) == imageHash &&
                WordboundSoloPreviewCapture.FileHash(target) == imageHash && WordboundSoloPreviewCapture.FileHash(target + ".meta") == (string)repair["imageMetaSha256"],
                "Reviewed image or retained image metadata changed; existing outputs were preserved.");
            string complete = folder + "/import-receipt.json", imageGuid = AssetDatabase.AssetPathToGUID(target);
            if (File.Exists(complete))
            {
                var done = JObject.Parse(File.ReadAllText(complete));
                Require((string)done["status"] == "IMPORTED_REQUIRES_MENU_REVIEW" && (string)done["captureReceiptSha256"] == (string)repair["captureReceiptSha256"] &&
                    (string)done["imageSha256"] == imageHash && (string)done["imagePath"] == target && (string)done["imageGuid"] == imageGuid &&
                    (bool?)done["recoveredRetainedImport"] == true, "Existing completion evidence differs; nothing was overwritten.");
                Debug.Log("[Arena preview] Retained import already verified; no writes and current owner layout changes preserved.");
                return;
            }
            string baseline = repairRoot + "/baseline/WordboundArenaPreviewImport.cs";
            string candidate = repairRoot + "/candidate/WordboundArenaPreviewImport.cs";
            Require(WordboundSoloPreviewCapture.FileHash(baseline) == (string)repair["originalImporterSha256"] &&
                WordboundSoloPreviewCapture.FileHash(candidate) == (string)repair["reviewedImporterSha256"] &&
                WordboundSoloPreviewCapture.FileHash(importerPath) == (string)repair["reviewedImporterSha256"],
                "Live helper is not the exact reviewed repair or retained original bytes changed.");
            Require(WordboundSoloPreviewCapture.FileHash(backup + "/manifest.json") == (string)repair["beforeImportManifestSha256"], "Pre-import manifest changed.");
            var before = JObject.Parse(File.ReadAllText(backup + "/manifest.json"));
            var inputs = receipt["inputs"]?.ToObject<Dictionary<string, string>>();
            Require(inputs != null && inputs.ContainsKey(importerPath) && inputs[importerPath] == (string)repair["originalImporterSha256"] &&
                (string)before["captureSha256"] == (string)repair["captureReceiptSha256"] &&
                (string)before["layoutPath"] == WordboundArenaPreviewCapture.LayoutPath &&
                inputs[WordboundArenaPreviewCapture.LayoutPath] == (string)before["layoutSha256"] &&
                WordboundSoloPreviewCapture.FileHash(backup + "/LobbyPlayLayout.asset") == (string)before["layoutSha256"] &&
                WordboundSoloPreviewCapture.FileHash(backup + "/LobbyPlayLayout.asset.meta") == (string)before["layoutMetaSha256"] &&
                WordboundSoloPreviewCapture.FileHash(WordboundArenaPreviewCapture.LayoutPath + ".meta") == (string)before["layoutMetaSha256"],
                "Exact pre-import asset, capture binding or layout GUID evidence changed.");
            Require(WordboundSoloPreviewCapture.FileHash(WordboundArenaPreviewCapture.LayoutPath) == (string)repair["retainedLayoutSha256"],
                "Owner layout changed after the audited failed attempt; preserve it and inspect before any recovery.");
            string restoredFingerprint = FingerprintWithOriginalImporter(importerPath, inputs[importerPath]);
            Require(restoredFingerprint == (string)receipt["sourceFingerprint"], "Source differs beyond the exact reviewed importer repair; recapture rather than bypassing freshness.");
            foreach (var input in inputs)
                if (input.Key != importerPath && input.Key != WordboundArenaPreviewCapture.LayoutPath)
                    Require(File.Exists(input.Key) && WordboundSoloPreviewCapture.FileHash(input.Key) == input.Value, "Captured input changed: " + input.Key);
            var layout = AssetDatabase.LoadAssetAtPath<LobbyPlayLayout>(WordboundArenaPreviewCapture.LayoutPath);
            Require(layout && !EditorUtility.IsDirty(layout), "Save pending owner layout changes before inspection; this repair never saves them.");
            layout.Validate();
            var layoutImporter = AssetImporter.GetAtPath(WordboundArenaPreviewCapture.LayoutPath);
            Require(!layoutImporter || !EditorUtility.IsDirty(layoutImporter), "Pending layout importer changes must be resolved separately.");
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(target);
            string spriteGuid = null; long spriteId = 0;
            Require(sprite && sprite.texture.width == WordboundSoloPreviewCapture.Width && sprite.texture.height == WordboundSoloPreviewCapture.Height &&
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite, out spriteGuid, out spriteId) && spriteGuid == imageGuid,
                "Expected the unchanged persistent arena Sprite.");
            using (var serialized = new SerializedObject(layout))
                Require(serialized.FindProperty(Field)?.objectReferenceValue == sprite, "Current arena assignment differs; owner edits were preserved.");
            RequireOnlyReviewedLayoutAdditions(backup + "/LobbyPlayLayout.asset", WordboundArenaPreviewCapture.LayoutPath, imageGuid, spriteId, before);
            var texture = AssetImporter.GetAtPath(target) as TextureImporter;
            Require(texture && !EditorUtility.IsDirty(texture) && texture.textureType == TextureImporterType.Sprite && texture.spriteImportMode == SpriteImportMode.Single &&
                !texture.mipmapEnabled && !texture.isReadable && texture.sRGBTexture && texture.alphaSource == TextureImporterAlphaSource.None &&
                !texture.alphaIsTransparency && texture.wrapMode == TextureWrapMode.Clamp && texture.filterMode == FilterMode.Bilinear &&
                texture.textureCompression == TextureImporterCompression.Uncompressed && texture.maxTextureSize == 1024 && texture.npotScale == TextureImporterNPOTScale.None,
                "Retained image import settings differ from the reviewed importer.");
            var data = Resources.Load<GameData>(GameData.ResourcePath);
            var launch = Resources.Load<WordboundLaunchAssets>(WordboundLaunchAssets.ResourcePath);
            Require(data && data.items && data.rules && launch && launch.VisualProfile, "Installed arena inputs are missing.");
            var content = WordboundArenaPreviewCapture.Content(data, launch);
            Require((string)receipt["seed"] == WordboundArenaPreviewCapture.Seed && (string)receipt["mode"] == "FreeForAll" &&
                (string)receipt["format"] == "Solo" && (string)receipt["courseId"] == "arena-proof" &&
                (string)receipt["sourceHash"] == content.Course.SourceHash && (string)receipt["manifestHash"] == content.Course.Hash &&
                (string)receipt["geometryHash"] == content.Geometry.Hash && (string)receipt["contentHash"] == content.PublicBinding.CatalogHash,
                "Installed arena no longer matches the reviewed capture.");
            var current = WordboundArenaPreviewCapture.Inputs(data, launch);
            string[] additions = current.Keys.Except(inputs.Keys, StringComparer.Ordinal).OrderBy(p => p, StringComparer.Ordinal).ToArray();
            Require(inputs.Keys.All(current.ContainsKey) && additions.SequenceEqual(new[] { target, target + ".meta" }.OrderBy(p => p, StringComparer.Ordinal)),
                "Dependency roster changed beyond the new reviewed image and its metadata.");
            WordboundSoloPreviewCapture.RequireCleanDependencies(current.Keys);
            string memory = EditorJsonUtility.ToJson(layout), layoutHash = WordboundSoloPreviewCapture.FileHash(WordboundArenaPreviewCapture.LayoutPath);
            foreach (var input in current)
                Require(WordboundSoloPreviewCapture.FileHash(input.Key) == input.Value, "Input changed during verification: " + input.Key);
            Require(WordboundArenaPreviewCapture.SceneState() == sceneBeforeVerification && EditorJsonUtility.ToJson(layout) == memory &&
                !EditorUtility.IsDirty(layout) && WordboundSoloPreviewCapture.FileHash(WordboundArenaPreviewCapture.LayoutPath) == layoutHash,
                "Verification changed scene, owner layout state or saved bytes.");
            WordboundArenaPreviewCapture.Write(complete, JsonConvert.SerializeObject(new {
                status = "IMPORTED_REQUIRES_MENU_REVIEW", utc = DateTime.UtcNow.ToString("O"), recoveredRetainedImport = true,
                captureReceiptSha256 = WordboundSoloPreviewCapture.FileHash(path), imagePath = target, imageSha256 = imageHash, imageGuid,
                layoutPath = WordboundArenaPreviewCapture.LayoutPath, layoutSha256 = layoutHash,
                layoutMetaSha256 = (string)before["layoutMetaSha256"], otherFieldsPreserved = true, backup,
                repairManifestSha256 = WordboundSoloPreviewCapture.FileHash(repairRoot + "/manifest.json"),
                restoredOriginalSourceFingerprint = restoredFingerprint, currentSourceFingerprint = WordboundSoloPreviewCapture.SourceFingerprint(),
                exactOriginalLayoutLinesPreserved = true, schemaDefaultsMaterialized = new[] { "ModeMinimumWidth", "ModeMaximumWidth", "ModeHeight", "MapMinimumWidth", "MapMinimumHeight", "MapColumns" },
                sceneVerification = "This verification is read-only and preserves its observed scene state. The failed import did not record its before-scene state, so that earlier scene state is not retroactively claimed."
            }, Formatting.Indented));
            Debug.Log("[Arena preview] Retained assignment verified without asset writes; exact original values/GUIDs plus six already-loaded schema defaults preserved. Menu review still required.");
        }

        static string FingerprintWithOriginalImporter(string importerPath, string originalHash)
        {
            var roots = new[] { "Assets/_Project/Scripts/Art", "Assets/_Project/Scripts/Core", "Assets/_Project/Scripts/Rules",
                "Assets/_Project/Scripts/WordboundRuntime", "Assets/_Project/Scripts/Online/Presentation", "Assets/_Project/Scripts/Online/Puzzles",
                "Assets/_Project/Scripts/Online/Client", "Assets/_Project/Scripts/Online/Authoring/Editor" };
            var paths = roots.SelectMany(root => Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
                .Select(p => p.Replace('\\', '/')).Distinct(StringComparer.Ordinal).OrderBy(p => p, StringComparer.Ordinal).ToArray();
            Require(paths.Length > 0 && paths.Length <= 512 && paths.Contains(importerPath), "Unexpected bounded source roster.");
            return WordboundSoloPreviewCapture.Hash(System.Text.Encoding.UTF8.GetBytes(string.Join("\n", paths.Select(p => p + ":" +
                (p == importerPath ? originalHash : WordboundSoloPreviewCapture.FileHash(p))))));
        }

        static void RequireOnlyReviewedLayoutAdditions(string original, string current, string guid, long spriteId, JObject before)
        {
            var originalBody = JObject.Parse((string)before["otherFields"])["MonoBehaviour"] as JObject;
            Require(originalBody != null && (int?)originalBody["ModeMinimumWidth"] == 104 && (int?)originalBody["ModeMaximumWidth"] == 1160 &&
                (int?)originalBody["ModeHeight"] == 52 && (int?)originalBody["MapMinimumWidth"] == 264 &&
                (int?)originalBody["MapMinimumHeight"] == 340 && (int?)originalBody["MapColumns"] == 3,
                "Before-import memory does not prove the six serialized schema defaults.");
            var lines = File.ReadAllLines(current).ToList();
            var additions = new[] { "  ModeMinimumWidth: 104", "  ModeMaximumWidth: 1160", "  ModeHeight: 52",
                "  MapMinimumWidth: 264", "  MapMinimumHeight: 340", "  MapColumns: 3",
                "  CombatArenaPreview: {fileID: " + spriteId + ", guid: " + guid + ", type: 3}" };
            foreach (string line in additions)
            { Require(lines.Count(value => value == line) == 1, "Missing or duplicated intended layout addition: " + line); lines.Remove(line); }
            Require(lines.SequenceEqual(File.ReadAllLines(original)), "Original saved values, GUIDs or structure changed beyond the seven reviewed line additions.");
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
