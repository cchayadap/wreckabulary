using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Wreckabulary.EditorTools
{
    public static class LobbyShopCompactFooterSetup
    {
        public const string PagePath = "Assets/_Project/Prefabs/UI/Shop/ShopPage.prefab";
        public const string BackupRoot = "BuildNotes/creator-clarity-20261029/shop/compact-correction/native-migration";
        const string ProfilePath = "Assets/_Project/Resources/UI/LobbyShopLayout.asset";
        const string OfferPath = "Assets/_Project/Prefabs/UI/Shop/ShopOfferCard.prefab";
        const string ExpectedPageHash = "62feffcfbd4c3c30d93c97fa47d0b0773047b9293cd29da8c439694d1a3aa8b6";
        const string ExpectedMetaHash = "0a2875d600bce48936dc9e0d16bd36c524fb380abf52db2f5ee9482731ea4651";
        const string PageGuid = "681abda322e4458419409f04c40be1c4";
        static readonly string[] Paths = { PagePath, PagePath + ".meta", ProfilePath, ProfilePath + ".meta", OfferPath, OfferPath + ".meta" };

        [Serializable] sealed class Proof { public string path, sha256; }
        [Serializable] sealed class Receipt
        {
            public string status, pageGuid, utc, beforeSha256, afterSha256;
            public float beforeHeight, afterHeight;
            public int preservedProperties;
            public Proof[] originals;
        }

        [MenuItem("Wreckabulary/Authoring/Apply Compact Shop Footer Once")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Apply the saved Shop footer outside Play Mode.");
            var profile = AssetDatabase.LoadAssetAtPath<LobbyShopLayout>(ProfilePath);
            if (!profile) throw new InvalidOperationException("The saved Shop layout is missing.");
            profile.Validate();
            if (AssetDatabase.GetAssetPath(profile.Page) != PagePath || AssetDatabase.GetAssetPath(profile.Offer) != OfferPath ||
                AssetDatabase.AssetPathToGUID(PagePath) != PageGuid)
                throw new InvalidOperationException("The active Shop must reference the exact original page and offer prefabs.");

            string completePath = BackupRoot + "/COMPLETE.json";
            if (File.Exists(completePath))
            {
                var complete = JsonUtility.FromJson<Receipt>(File.ReadAllText(completePath));
                if (complete == null || complete.status != "COMPLETE" || complete.pageGuid != PageGuid ||
                    complete.beforeSha256 != ExpectedPageHash || complete.afterHeight != 96 || complete.preservedProperties <= 0)
                    throw new InvalidOperationException("The prior compact-Shop migration receipt is invalid.");
                VerifyBackups(complete.originals);
                Debug.Log("[Wreckabulary] Compact Shop footer migration already completed. Current saved height " +
                    profile.Page.SelectionHeight + " and all subsequent author edits are preserved; no writes performed.");
                return;
            }
            if (Directory.Exists(BackupRoot) || File.Exists(BackupRoot))
                throw new IOException("A partial compact-Shop migration already exists. Inspect its exact backups before recovery.");
            if (Hash(PagePath) != ExpectedPageHash || Hash(PagePath + ".meta") != ExpectedMetaHash)
                throw new InvalidOperationException("The Shop page differs from the reviewed baseline. Preserve and review those edits before this one-time migration.");
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && (stage.assetPath == PagePath || stage.assetPath == OfferPath))
                throw new InvalidOperationException("Save and close Shop Prefab Mode before migration.");
            foreach (var asset in new Object[] { profile, profile.Page.gameObject, profile.Offer.gameObject })
            {
                if (EditorUtility.IsDirty(asset)) throw new InvalidOperationException("Save current Shop edits first: " + asset.name);
                if (asset is GameObject go && go.GetComponentsInChildren<Component>(true).Any(c => c && EditorUtility.IsDirty(c)))
                    throw new InvalidOperationException("Save current Shop component edits first: " + go.name);
            }

            var inputs = Paths.Select(path => new Proof { path = path, sha256 = Hash(path) }).ToArray();
            GameObject original = null, edited = null, persisted = null;
            var receipt = new Receipt { status = "IN PROGRESS", pageGuid = PageGuid, utc = DateTime.UtcNow.ToString("O"),
                beforeSha256 = ExpectedPageHash, originals = inputs };
            try
            {
                original = PrefabUtility.LoadPrefabContents(PagePath);
                edited = PrefabUtility.LoadPrefabContents(PagePath);
                var page = edited.GetComponent<LobbyShopPageView>();
                if (!page) throw new InvalidOperationException("The saved page needs its root LobbyShopPageView.");
                page.Validate();
                receipt.beforeHeight = page.SelectionHeight;
                if (page.SelectionHeight != 132 && page.SelectionHeight != 96)
                    throw new InvalidOperationException("Preserve an unexpected authored footer height: " + page.SelectionHeight);
                Compare(original, edited, false);
                Directory.CreateDirectory(BackupRoot);
                foreach (var input in inputs)
                {
                    string copy = BackupRoot + "/before/" + input.path;
                    Directory.CreateDirectory(Path.GetDirectoryName(copy));
                    File.Copy(input.path, copy, false);
                }
                VerifyBackups(inputs);
                WriteNew(BackupRoot + "/STARTED.json", JsonUtility.ToJson(receipt, true));
                VerifyUnchanged(inputs, false);
                page.SelectionHeight = 96;
                page.Validate();
                receipt.preservedProperties = Compare(original, edited, true);
                if (!PrefabUtility.SaveAsPrefabAsset(edited, PagePath)) throw new IOException("Could not save the explicit Shop footer height.");
                PrefabUtility.UnloadPrefabContents(edited); edited = null;
                AssetDatabase.ImportAsset(PagePath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                persisted = PrefabUtility.LoadPrefabContents(PagePath);
                var saved = persisted.GetComponent<LobbyShopPageView>();
                saved.Validate();
                if (saved.SelectionHeight != 96) throw new InvalidOperationException("The saved Shop footer did not persist 96.");
                if (Compare(original, persisted, true) != receipt.preservedProperties)
                    throw new InvalidOperationException("The saved Shop property roster changed.");
                VerifyUnchanged(inputs, true);
                receipt.afterHeight = saved.SelectionHeight;
                receipt.afterSha256 = Hash(PagePath);
            }
            finally
            {
                var cleanupErrors = new List<Exception>();
                foreach (var owned in new[] { persisted, edited, original })
                    if (owned)
                        try { PrefabUtility.UnloadPrefabContents(owned); }
                        catch (Exception error) { cleanupErrors.Add(error); }
                if (cleanupErrors.Count != 0) throw new AggregateException("Shop prefab cleanup did not complete; no success receipt was written.", cleanupErrors);
            }
            VerifyBackups(inputs);
            VerifyUnchanged(inputs, true);
            receipt.status = "COMPLETE";
            WriteNew(completePath, JsonUtility.ToJson(receipt, true));
            Debug.Log("[Wreckabulary] Saved explicit96-unit Shop footer. All other serialized authored properties, prefab GUID, profile and offer preserved. Receipt: " + completePath);
        }

        static int Compare(GameObject before, GameObject after, bool allowHeight)
        {
            var a = Objects(before); var b = Objects(after);
            if (a.Count != b.Count) throw new InvalidOperationException("Shop hierarchy/component count changed.");
            int checkedProperties = 0;
            for (int i = 0; i < a.Count; i++)
            {
                if (a[i].GetType() != b[i].GetType() || Key(a[i], before) != Key(b[i], after))
                    throw new InvalidOperationException("Shop hierarchy/component identity changed.");
                using var left = new SerializedObject(a[i]); using var right = new SerializedObject(b[i]);
                var p = left.GetIterator(); var q = right.GetIterator();
                bool descend = true;
                while (p.Next(descend))
                {
                    if (!q.Next(descend) || p.propertyPath != q.propertyPath || p.propertyType != q.propertyType)
                        throw new InvalidOperationException("Shop serialized property roster changed: " + p.propertyPath);
                    descend = p.propertyType != SerializedPropertyType.ObjectReference;
                    if (p.propertyType == SerializedPropertyType.Generic) continue;
                    if (allowHeight && a[i] is LobbyShopPageView && p.propertyPath == nameof(LobbyShopPageView.SelectionHeight)) continue;
                    if (p.propertyType == SerializedPropertyType.ObjectReference &&
                        (!p.objectReferenceValue && p.objectReferenceEntityIdValue != default || !q.objectReferenceValue && q.objectReferenceEntityIdValue != default))
                        throw new InvalidOperationException("Resolve the missing Shop reference before migration: " + p.propertyPath);
                    bool same = p.propertyType == SerializedPropertyType.ObjectReference
                        ? Key(p.objectReferenceValue, before) == Key(q.objectReferenceValue, after)
                        : SerializedProperty.DataEquals(p, q);
                    if (!same) throw new InvalidOperationException("Unexpected Shop property change: " + Key(a[i], before) + "/" + p.propertyPath);
                    checkedProperties++;
                }
                if (q.Next(descend)) throw new InvalidOperationException("An extra Shop serialized property appeared.");
            }
            return checkedProperties;
        }

        static List<Object> Objects(GameObject root)
        {
            var objects = new List<Object>();
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                objects.Add(transform.gameObject);
                foreach (var component in transform.GetComponents<Component>())
                {
                    if (!component) throw new InvalidOperationException("Shop prefab contains a missing script.");
                    objects.Add(component);
                }
            }
            return objects;
        }

        static string Key(Object value, GameObject root)
        {
            if (!value) return "null";
            var transform = value is GameObject go ? go.transform : value is Component component ? component.transform : null;
            if (transform && (transform == root.transform || transform.IsChildOf(root.transform)))
            {
                var indices = new List<int>();
                for (var cursor = transform; cursor != root.transform; cursor = cursor.parent) indices.Add(cursor.GetSiblingIndex());
                indices.Reverse();
                string path = string.Join("/", indices);
                return path + (value is Component c ? ":component:" + Array.IndexOf(transform.GetComponents<Component>(), c) : ":object");
            }
            if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value, out string guid, out long id)) return guid + ":" + id;
            throw new InvalidOperationException("Shop serialized reference is not local or a saved asset: " + value.name);
        }

        static void VerifyBackups(Proof[] proofs)
        {
            if (proofs == null || proofs.Length != Paths.Length || !proofs.Select(p => p.path).SequenceEqual(Paths))
                throw new IOException("The exact six-file Shop backup roster is invalid.");
            foreach (var proof in proofs)
                if (Hash(BackupRoot + "/before/" + proof.path) != proof.sha256) throw new IOException("Shop backup changed: " + proof.path);
            if (proofs[0].sha256 != ExpectedPageHash || proofs[1].sha256 != ExpectedMetaHash)
                throw new IOException("The Shop backup does not match the reviewed original page.");
        }

        static void VerifyUnchanged(Proof[] proofs, bool saved)
        {
            foreach (var proof in proofs)
                if (!(saved && proof.path == PagePath) && Hash(proof.path) != proof.sha256)
                    throw new IOException("An unrelated Shop input changed: " + proof.path);
        }

        static string Hash(string path)
        {
            using var stream = File.OpenRead(path); using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }

        static void WriteNew(string path, string contents)
        {
            using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            using var writer = new StreamWriter(stream); writer.Write(contents);
        }
    }
}
