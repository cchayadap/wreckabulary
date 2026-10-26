using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Wreckabulary.EditorTools
{
    public static class LobbyHomeNavigationSetup
    {
        static readonly string[] Before = { "START", "WORDBOUND", "Online parties · Map workshop" };
        static readonly string[] After = { "LOCAL PLAY", "ONLINE PLAY", "Human players · Public / private" };

        [Serializable] public sealed class Receipt
        {
            public string status = "IN PROGRESS", error = "", profile, prefab, prefabGuid;
            public string beforeHash, afterHash, otherFieldsBefore, otherFieldsAfter;
            public bool saveAttempted, alreadyApplied, reloaded, dependenciesPreserved, importedFontsPreserved, scenePreserved, contentsReleased;
            public string[] beforeLabels, afterLabels;
            public List<FileProof> preservedFiles = new();
            public List<ImportedFontProof> importedFonts = new();
        }

        [Serializable] public sealed class FileProof
        {
            public string path, before, after;
        }

        [Serializable] public sealed class ImportedFontProof
        {
            public string path, beforeState, afterState, importerBeforeState, importerAfterState;
            public string objectType, objectName;
            public bool mainAsset, subAsset;
            public bool dirtyBefore, dirtyAfter, importerDirtyAfter;
        }

        [MenuItem("Wreckabulary/Authoring/Apply Reviewed Home Navigation Labels")]
        public static void Run()
        {
            string folder = "BuildNotes/lobby-experience-20261026/design/home-labels/runs/" +
                DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            if (Directory.Exists(folder)) throw new IOException("Preserve the existing migration evidence.");
            Directory.CreateDirectory(folder);
            var receipt = new Receipt();
            var profile = AssetDatabase.LoadAssetAtPath<LobbyHomeLayout>(LobbyHomeLayoutSetup.ProfilePath);
            if (profile && profile.Page)
            {
                string source = AssetDatabase.GetAssetPath(profile.Page);
                if (File.Exists(source)) File.WriteAllBytes(folder + "/before.prefab.txt", File.ReadAllBytes(source));
            }
            try { ApplyAt(LobbyHomeLayoutSetup.ProfilePath, receipt); }
            finally
            {
                File.WriteAllText(folder + "/receipt.json", JsonUtility.ToJson(receipt, true));
                Debug.Log("[Wreckabulary] Home navigation labels: " + receipt.status + "; " + folder);
            }
        }

        public static void ApplyAt(string profilePath, Receipt receipt)
        {
            if (receipt == null) throw new ArgumentNullException(nameof(receipt));
            receipt.profile = profilePath;
            string sceneBefore = Scenes();
            GameObject contents = null;
            Exception failure = null;
            var preserved = new Dictionary<string, string>(StringComparer.Ordinal);
            var importedFonts = new List<(UnityEngine.Object asset, AssetImporter importer, ImportedFontProof proof)>();
            try
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                    throw new InvalidOperationException("Apply saved Home labels outside Play, compilation and import.");
                if (string.IsNullOrEmpty(profilePath) || !profilePath.StartsWith("Assets/_Project/", StringComparison.Ordinal) ||
                    profilePath.Contains('\\') || profilePath.Split('/').Any(p => p.Length == 0 || p == "." || p == "..") ||
                    !profilePath.EndsWith(".asset", StringComparison.Ordinal)) throw new ArgumentException("Choose a saved project Home profile.");
                var profile = AssetDatabase.LoadAssetAtPath<LobbyHomeLayout>(profilePath);
                if (!profile || EditorUtility.IsDirty(profile)) throw new InvalidOperationException("Save or resolve the existing Home profile first.");
                profile.Validate();
                receipt.prefab = AssetDatabase.GetAssetPath(profile.Page);
                if (!receipt.prefab.EndsWith(".prefab", StringComparison.Ordinal) || !receipt.prefab.StartsWith("Assets/_Project/", StringComparison.Ordinal))
                    throw new InvalidOperationException("Home must reference a saved project prefab.");
                foreach (var item in profile.Page.GetComponentsInChildren<Transform>(true))
                {
                    if (EditorUtility.IsDirty(item.gameObject) || item.GetComponents<Component>().Any(component => component && EditorUtility.IsDirty(component)))
                        throw new InvalidOperationException("Preserve unsaved Home prefab edits before migration.");
                }
                var stage = PrefabStageUtility.GetCurrentPrefabStage();
                if (stage != null && stage.assetPath == receipt.prefab)
                    throw new InvalidOperationException("Close Home Prefab Mode before this explicit migration; saved custom layout is preserved.");
                foreach (string path in AssetDatabase.GetDependencies(profilePath, true))
                {
                    var importer = AssetImporter.GetAtPath(path);
                    if (importer && EditorUtility.IsDirty(importer)) throw new InvalidOperationException("Preserve unsaved importer edits: " + path);
                    var sourceFont = importer is TrueTypeFontImporter &&
                        (path.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".otf", StringComparison.OrdinalIgnoreCase))
                        ? AssetDatabase.LoadAssetAtPath<Font>(path) : null;
                    bool confirmedFontSource = sourceFont && AssetDatabase.IsMainAsset(sourceFont) && AssetDatabase.GetAssetPath(sourceFont) == path;
                    foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                    {
                        if (!asset) continue;
                        bool binaryFont = confirmedFontSource && AssetDatabase.GetAssetPath(asset) == path &&
                            (asset == sourceFont || AssetDatabase.IsSubAsset(asset) && (asset is Texture2D || asset is Material));
                        if (binaryFont)
                        {
                            var proof = new ImportedFontProof { path = path, dirtyBefore = EditorUtility.IsDirty(asset),
                                objectType = asset.GetType().FullName, objectName = asset.name,
                                mainAsset = AssetDatabase.IsMainAsset(asset), subAsset = AssetDatabase.IsSubAsset(asset),
                                beforeState = Hash(Encoding.UTF8.GetBytes(EditorJsonUtility.ToJson(asset))),
                                importerBeforeState = Hash(Encoding.UTF8.GetBytes(EditorJsonUtility.ToJson(importer))) };
                            receipt.importedFonts.Add(proof); importedFonts.Add((asset, importer, proof));
                        }
                        else if (EditorUtility.IsDirty(asset)) throw new InvalidOperationException("Preserve unsaved dependency edits: " + path +
                            " (type=" + asset.GetType().FullName + ", name=" + asset.name + ", main=" + AssetDatabase.IsMainAsset(asset) +
                            ", importer=" + (importer ? importer.GetType().FullName : "none") + ")");
                    }
                    if (path != receipt.prefab && File.Exists(path)) preserved[path] = Hash(File.ReadAllBytes(path));
                    if (File.Exists(path + ".meta")) preserved[path + ".meta"] = Hash(File.ReadAllBytes(path + ".meta"));
                }
                receipt.prefabGuid = AssetDatabase.AssetPathToGUID(receipt.prefab);
                receipt.beforeHash = Hash(File.ReadAllBytes(receipt.prefab));
                var labels = Labels(profile.Page);
                var ids = labels.Select(label =>
                {
                    if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(label, out string guid, out long id) || guid != receipt.prefabGuid)
                        throw new InvalidOperationException("Home text must be owned by its referenced prefab.");
                    return id;
                }).ToArray();
                if (ids.Distinct().Count() != 3) throw new InvalidOperationException("Home labels must be distinct.");
                receipt.beforeLabels = labels.Select(label => label.text).ToArray();
                bool old = receipt.beforeLabels.SequenceEqual(Before);
                receipt.alreadyApplied = receipt.beforeLabels.SequenceEqual(After);
                if (!old && !receipt.alreadyApplied)
                    throw new InvalidOperationException("Home labels contain custom or partially migrated text. Preserve it; edit the three labels in Prefab Mode explicitly.");
                receipt.otherFieldsBefore = MaskedTextHash(receipt.prefab, ids);
                if (old)
                {
                    contents = PrefabUtility.LoadPrefabContents(receipt.prefab);
                    var page = contents.GetComponent<LobbyHomePageView>();
                    page.Validate();
                    var edit = Labels(page);
                    if (!edit.Select(label => label.text).SequenceEqual(Before)) throw new InvalidOperationException("Home contents changed since preflight.");
                    for (int i = 0; i < edit.Length; i++) edit[i].text = After[i];
                    receipt.saveAttempted = true;
                    if (!PrefabUtility.SaveAsPrefabAsset(contents, receipt.prefab)) throw new IOException("Unity could not save Home labels.");
                    PrefabUtility.UnloadPrefabContents(contents); contents = null;
                    AssetDatabase.ImportAsset(receipt.prefab, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                }
                var saved = AssetDatabase.LoadAssetAtPath<GameObject>(receipt.prefab).GetComponent<LobbyHomePageView>();
                saved.Validate();
                receipt.afterLabels = Labels(saved).Select(label => label.text).ToArray();
                receipt.afterHash = Hash(File.ReadAllBytes(receipt.prefab));
                receipt.otherFieldsAfter = MaskedTextHash(receipt.prefab, ids);
                receipt.reloaded = receipt.afterLabels.SequenceEqual(After) && !EditorUtility.IsDirty(saved) &&
                    AssetDatabase.AssetPathToGUID(receipt.prefab) == receipt.prefabGuid;
                if (!receipt.reloaded || receipt.otherFieldsBefore != receipt.otherFieldsAfter ||
                    receipt.alreadyApplied && receipt.beforeHash != receipt.afterHash)
                    throw new InvalidOperationException("Saved Home changed beyond the three reviewed text fields; inspect the receipt and preserved original.");
                receipt.status = old ? "APPLIED; VERIFIED" : "ALREADY APPLIED; NO SAVE";
            }
            catch (Exception error) { failure = error; receipt.status = "FAIL"; receipt.error = error.ToString(); throw; }
            finally
            {
                bool cleanupFailed = false;
                try { if (contents) PrefabUtility.UnloadPrefabContents(contents); }
                catch (Exception cleanup) { cleanupFailed = true; receipt.error += "\nHome contents cleanup: " + cleanup; }
                receipt.contentsReleased = !contents;
                receipt.scenePreserved = Scenes() == sceneBefore;
                receipt.dependenciesPreserved = true;
                receipt.importedFontsPreserved = true;
                foreach (var entry in importedFonts)
                {
                    var proof = entry.proof;
                    proof.afterState = entry.asset ? Hash(Encoding.UTF8.GetBytes(EditorJsonUtility.ToJson(entry.asset))) : "MISSING";
                    proof.importerAfterState = entry.importer ? Hash(Encoding.UTF8.GetBytes(EditorJsonUtility.ToJson(entry.importer))) : "MISSING";
                    proof.dirtyAfter = entry.asset && EditorUtility.IsDirty(entry.asset);
                    proof.importerDirtyAfter = entry.importer && EditorUtility.IsDirty(entry.importer);
                    receipt.importedFontsPreserved &= proof.beforeState == proof.afterState && proof.importerBeforeState == proof.importerAfterState &&
                        proof.dirtyBefore == proof.dirtyAfter && !proof.importerDirtyAfter;
                }
                foreach (var pair in preserved)
                {
                    string after = File.Exists(pair.Key) ? Hash(File.ReadAllBytes(pair.Key)) : "MISSING";
                    receipt.preservedFiles.Add(new FileProof { path = pair.Key, before = pair.Value, after = after });
                    receipt.dependenciesPreserved &= pair.Value == after;
                }
                if (!string.IsNullOrEmpty(receipt.prefab) && File.Exists(receipt.prefab)) receipt.afterHash = Hash(File.ReadAllBytes(receipt.prefab));
                if (cleanupFailed || !receipt.contentsReleased || !receipt.scenePreserved || !receipt.dependenciesPreserved || !receipt.importedFontsPreserved)
                {
                    receipt.status = "FAIL"; receipt.error += "\nHome migration preservation check failed.";
                    if (failure == null) throw new InvalidOperationException(receipt.error);
                }
            }
        }

        static TextMeshProUGUI[] Labels(LobbyHomePageView page) => new[] { page.Start.Label, page.Wordbound.Label, page.WordboundNote };

        static string MaskedTextHash(string path, long[] ids)
        {
            long current = 0;
            var found = new HashSet<long>();
            var lines = File.ReadAllText(path).Replace("\r\n", "\n").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                var header = Regex.Match(lines[i], @"^--- !u!\d+ &(-?\d+)$");
                if (header.Success) current = long.Parse(header.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                if (!ids.Contains(current) || !lines[i].StartsWith("  m_text:", StringComparison.Ordinal)) continue;
                if (!found.Add(current)) throw new InvalidOperationException("Unexpected duplicate Home text serialization.");
                lines[i] = "  m_text: <reviewed-home-navigation-label>";
            }
            if (found.Count != ids.Length) throw new InvalidOperationException("Expected text-serialized Home label fields were not found.");
            return Hash(Encoding.UTF8.GetBytes(string.Join("\n", lines)));
        }

        static string Scenes()
        {
            var parts = new List<string> { SceneManager.GetActiveScene().handle.ToString() };
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                parts.Add(scene.handle + ":" + scene.path + ":" + scene.isDirty + ":" +
                    string.Join(",", scene.GetRootGameObjects().Select(root => root.GetEntityId().ToString())));
            }
            return string.Join("|", parts);
        }

        static string Hash(byte[] bytes)
        {
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }
    }
}
