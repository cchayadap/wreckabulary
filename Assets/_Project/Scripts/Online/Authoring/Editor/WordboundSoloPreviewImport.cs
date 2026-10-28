using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Wreckabulary.Online.Editor
{
    public static class WordboundSoloPreviewImport
    {
        const string Output = "Assets/_Project/UI/Previews/Wordbound";
        const string LayoutPath = "Assets/_Project/Resources/UI/LobbyPlayLayout.asset";
        static readonly string[] InstalledMaps = { WordboundSoloEntry.LostAddressMap, WordboundSoloEntry.ParcelHallMap, WordboundSoloEntry.ParcelServiceMap };

        [MenuItem("Tools/Wordbound/Presentation/Import reviewed Solo preview receipt")]
        public static void ImportReviewed()
        {
            WordboundSoloPreviewCapture.Require(!EditorApplication.isPlayingOrWillChangePlaymode && !WordboundSoloPreviewWindow.IsOpen,
                "Close the framing window and leave Play mode before importing previews.");
            string path = Environment.GetEnvironmentVariable("WORDBOUND_SOLO_PREVIEW_RECEIPT");
            if (string.IsNullOrWhiteSpace(path) && !Application.isBatchMode)
                path = EditorUtility.OpenFilePanel("Select the reviewed Solo preview receipt", WordboundSoloPreviewCapture.CaptureRoot, "json");
            WordboundSoloPreviewCapture.Require(!string.IsNullOrWhiteSpace(path), "Select a reviewed capture receipt; batch mode requires WORDBOUND_SOLO_PREVIEW_RECEIPT.");
            path = Path.GetFullPath(path);
            string folder = Path.GetDirectoryName(path), parent = Path.GetFullPath(WordboundSoloPreviewCapture.CaptureRoot);
            WordboundSoloPreviewCapture.Require(Path.GetFileName(path) == "receipt.json" &&
                string.Equals(Path.GetDirectoryName(folder), parent, StringComparison.OrdinalIgnoreCase), "Choose a direct capture-folder receipt from this project's Solo preview output.");
            WordboundSoloPreviewCapture.RejectLinks(folder);
            WordboundSoloPreviewCapture.RejectLinks(Path.GetFullPath(Output));
            WordboundSoloPreviewCapture.Require(File.Exists(path) && new FileInfo(path).Length <= 8 * 1024 * 1024, "Preview receipt is missing or exceeds its size bound.");
            var receipt = JObject.Parse(File.ReadAllText(path));
            WordboundSoloPreviewCapture.Require((int?)receipt["schema"] == 1 && (string)receipt["status"] == "CAPTURED_NOT_VISUALLY_APPROVED" &&
                receipt["errors"] is JArray errors && errors.Count == 0 && receipt["frames"] is JArray frames && frames.Count >= 1 && frames.Count <= InstalledMaps.Length &&
                (int?)receipt["width"] == WordboundSoloPreviewCapture.Width && (int?)receipt["height"] == WordboundSoloPreviewCapture.Height,
                "Only a complete installed-map capture receipt can be imported.");
            WordboundSoloPreviewCapture.Require(receipt["sets"] is JArray setsRoster && setsRoster.Count == ((JArray)receipt["frames"]).Count, "Capture map/frame roster differs.");
            var Maps = ((JArray)receipt["sets"]).Select(s => (string)s["map"]).ToArray();
            WordboundSoloPreviewCapture.Require(Maps.Distinct(StringComparer.Ordinal).Count() == Maps.Length &&
                Maps.All(m => InstalledMaps.Contains(m)) &&
                ((JArray)receipt["frames"]).Select(f => (string)f["filename"]).OrderBy(n => n, StringComparer.Ordinal)
                    .SequenceEqual(Maps.Select(m => m + ".png").OrderBy(n => n, StringComparer.Ordinal)), "Capture contains unknown, duplicated or mismatched map frames.");
            foreach (string field in new[] { "filesPreserved", "memoryPreserved", "scenesPreserved", "globalsPreserved", "tactileReleased", "sourcePreserved" })
                WordboundSoloPreviewCapture.Require((bool?)receipt[field] == true, "Capture preservation did not pass: " + field);
            WordboundSoloPreviewCapture.Require((string)receipt["sourceFingerprint"] == WordboundSoloPreviewCapture.SourceFingerprint(),
                "Relevant rendering/generation source changed after capture; regenerate the previews.");
            WordboundSoloPreviewCapture.Require(receipt["inputFacts"] is JArray inputs && inputs.Count >= 10 && inputs.Count <= 128, "Invalid input roster.");
            foreach (var input in (JArray)receipt["inputFacts"])
            {
                string inputPath = (string)input["path"];
                WordboundSoloPreviewCapture.Require(!string.IsNullOrEmpty(inputPath) && !inputPath.Contains('\\') &&
                    !inputPath.Split('/').Any(p => p == ".." || p == "." || p.Length == 0) &&
                    (inputPath.StartsWith("Assets/", StringComparison.Ordinal) || inputPath.StartsWith("ProjectSettings/", StringComparison.Ordinal)), "Invalid recorded project input path.");
                WordboundSoloPreviewCapture.Require(File.Exists(inputPath) && WordboundSoloPreviewCapture.FileHash(inputPath) == (string)input["before"], "Source input changed after capture: " + inputPath);
                string dependency = (string)input["dependencyBefore"];
                bool dependencyRequired = inputPath.StartsWith("Assets/", StringComparison.Ordinal) && !inputPath.EndsWith(".meta", StringComparison.Ordinal);
                WordboundSoloPreviewCapture.Require(dependencyRequired
                    ? !string.IsNullOrEmpty(dependency) && AssetDatabase.GetAssetDependencyHash(inputPath).ToString() == dependency
                    : dependency == "", "Source dependency is missing or changed after capture: " + inputPath);
                var current = AssetDatabase.LoadMainAssetAtPath(inputPath);
                WordboundSoloPreviewCapture.Require(!current || !EditorUtility.IsDirty(current), "Save source edits and recapture before import: " + inputPath);
            }
            WordboundSoloPreviewCapture.RequireCleanDependencies(((JArray)receipt["inputFacts"]).Select(i => (string)i["path"]));
            var settings = AssetDatabase.LoadAssetAtPath<WordboundSoloPreviewSettings>(WordboundSoloPreviewSettings.Path);
            var data = Resources.Load<GameData>(GameData.ResourcePath);
            var launch = Resources.Load<WordboundLaunchAssets>(WordboundLaunchAssets.ResourcePath);
            WordboundSoloPreviewCapture.Require(settings && launch && data && data.items && data.rules &&
                settings.Seed == (string)receipt["seed"] && settings.Mode == (string)receipt["mode"] &&
                receipt["sets"] is JArray sets && sets.Count == Maps.Length, "Capture settings or installed game inputs no longer match.");
            settings.Validate();
            var recordedPaths = ((JArray)receipt["inputFacts"]).Select(i => (string)i["path"]).ToArray();
            WordboundSoloPreviewCapture.Require(recordedPaths.Distinct(StringComparer.Ordinal).Count() == recordedPaths.Length &&
                recordedPaths.OrderBy(p => p, StringComparer.Ordinal).SequenceEqual(WordboundSoloPreviewCapture.CurrentInputPaths(settings)),
                "The capture input roster differs from the current complete source roster; recapture before import.");
            foreach (string map in Maps)
            {
                var recorded = ((JArray)receipt["sets"]).SingleOrDefault(s => (string)s["map"] == map);
                var frame = ((JArray)receipt["frames"]).SingleOrDefault(f => (string)f["filename"] == map + ".png");
                var current = WordboundLauncher.CreateSoloContent(data.items.text, data.rules.text, settings.Mode, settings.Seed, map, launch.ParcelServiceCourse);
                WordboundSoloPreviewCapture.Require(recorded != null && frame != null &&
                    (string)recorded["seed"] == settings.Seed && (string)recorded["mode"] == settings.Mode &&
                    (string)recorded["manifestHash"] == current.Course.Hash && (string)frame["manifestHash"] == current.Course.Hash &&
                    (string)recorded["sourceHash"] == current.Course.SourceHash && (string)recorded["geometryHash"] == current.Geometry.Hash &&
                    (string)recorded["catalogueHash"] == current.CatalogueHash,
                    "Installed map generation changed after capture; regenerate the preview: " + map);
            }
            var layout = AssetDatabase.LoadAssetAtPath<LobbyPlayLayout>(LayoutPath);
            WordboundSoloPreviewCapture.Require(layout && !EditorUtility.IsDirty(layout), "Save the existing Play layout first."); layout.Validate();
            var images = new Dictionary<string, byte[]>();
            foreach (string map in Maps)
            {
                var frame = ((JArray)receipt["frames"]).SingleOrDefault(f => (string)f["filename"] == map + ".png");
                WordboundSoloPreviewCapture.Require(frame != null, "Capture lacks the installed map image: " + map);
                string png = Path.Combine(folder, map + ".png");
                WordboundSoloPreviewCapture.Require(File.Exists(png) && new FileInfo(png).Length <= 16 * 1024 * 1024, "Preview image is missing or oversized.");
                var bytes = File.ReadAllBytes(png);
                WordboundSoloPreviewCapture.Require(WordboundSoloPreviewCapture.Hash(bytes) == (string)frame["sha256"], "Preview image changed after capture: " + map);
                images.Add(map, bytes);
                var existing = AssetDatabase.LoadMainAssetAtPath(Output + "/" + map + ".png");
                WordboundSoloPreviewCapture.Require(!existing || existing is Texture2D && !EditorUtility.IsDirty(existing), "Existing preview has unsaved edits or is not a texture.");
                var existingImporter = AssetImporter.GetAtPath(Output + "/" + map + ".png");
                WordboundSoloPreviewCapture.Require(!existingImporter || !EditorUtility.IsDirty(existingImporter), "Apply or discard the existing preview's pending import settings first.");
            }
            string backup = Path.Combine(folder, "before-import");
            WordboundSoloPreviewCapture.Require(!Directory.Exists(backup) && !File.Exists(backup), "This capture has already started an import; inspect its retained backup instead of overwriting it.");
            Directory.CreateDirectory(backup);
            var saved = new List<object>();
            foreach (string source in Maps.Select(m => Output + "/" + m + ".png").Append(LayoutPath).SelectMany(p => new[] { p, p + ".meta" }))
                if (File.Exists(source))
                {
                    string destination = Path.Combine(backup, source); Directory.CreateDirectory(Path.GetDirectoryName(destination));
                    File.Copy(source, destination, false);
                    WordboundSoloPreviewCapture.Require(WordboundSoloPreviewCapture.FileHash(source) == WordboundSoloPreviewCapture.FileHash(destination), "Exact preview backup failed.");
                    saved.Add(new { path = source, sha256 = WordboundSoloPreviewCapture.FileHash(source) });
                }
            WordboundSoloPreviewCapture.WriteNew(Path.Combine(backup, "manifest.json"), Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(saved, Formatting.Indented)));
            var imported = new Dictionary<string, Sprite>();
            try
            {
                WordboundSoloPreviewCapture.EnsureFolder(Output);
                foreach (string map in Maps)
                {
                    string target = Output + "/" + map + ".png";
                    File.WriteAllBytes(target, images[map]);
                    AssetDatabase.ImportAsset(target, ImportAssetOptions.ForceSynchronousImport);
                    var importer = AssetImporter.GetAtPath(target) as TextureImporter;
                    WordboundSoloPreviewCapture.Require(importer, "Expected a PNG texture importer.");
                    importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
                    importer.sRGBTexture = true; importer.alphaSource = TextureImporterAlphaSource.None; importer.alphaIsTransparency = false;
                    importer.mipmapEnabled = false; importer.isReadable = false; importer.wrapMode = TextureWrapMode.Clamp;
                    importer.filterMode = FilterMode.Bilinear; importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.maxTextureSize = 1024; importer.npotScale = TextureImporterNPOTScale.None;
                    var textureSettings = new TextureImporterSettings(); importer.ReadTextureSettings(textureSettings);
                    textureSettings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(textureSettings);
                    importer.SaveAndReimport();
                    var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(target);
                    WordboundSoloPreviewCapture.Require(sprite && sprite.texture.width == WordboundSoloPreviewCapture.Width &&
                        sprite.texture.height == WordboundSoloPreviewCapture.Height && WordboundSoloPreviewCapture.FileHash(target) == WordboundSoloPreviewCapture.Hash(images[map]), "Imported preview dimensions or bytes changed.");
                    imported.Add(map, sprite);
                }
                Undo.RecordObject(layout, "Assign actual Solo map previews");
                if (imported.TryGetValue(WordboundSoloEntry.LostAddressMap, out var lost)) layout.LostAddressPreview = lost;
                if (imported.TryGetValue(WordboundSoloEntry.ParcelHallMap, out var hall)) layout.ParcelHallPreview = hall;
                if (imported.TryGetValue(WordboundSoloEntry.ParcelServiceMap, out var service)) layout.ParcelServicePreview = service;
                EditorUtility.SetDirty(layout); AssetDatabase.SaveAssetIfDirty(layout);
                WordboundSoloPreviewCapture.WriteNew(Path.Combine(folder, "import-receipt.json"), Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new {
                    status = "IMPORTED_REQUIRES_MENU_REVIEW", captureReceipt = WordboundSoloPreviewCapture.FileHash(path),
                    layoutGuid = AssetDatabase.AssetPathToGUID(LayoutPath), layoutSha256 = WordboundSoloPreviewCapture.FileHash(LayoutPath),
                    images = imported.Select(p => new { map = p.Key, path = AssetDatabase.GetAssetPath(p.Value), guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(p.Value)), sha256 = WordboundSoloPreviewCapture.FileHash(AssetDatabase.GetAssetPath(p.Value)) })
                }, Formatting.Indented)));
                Selection.activeObject = layout; EditorGUIUtility.PingObject(layout);
            }
            catch (Exception e) { throw new InvalidOperationException("Import stopped. Exact prior assets are retained at " + backup + "; inspect any partial import before retrying.", e); }
        }
    }
}
