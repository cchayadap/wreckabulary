using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using Wreckabulary.Art;
using Wreckabulary.Rules.Wordbound;
using Wreckabulary.WordboundRuntime;
using Object = UnityEngine.Object;

namespace Wreckabulary.Online.Editor
{
    public static class WordboundParcelServiceMaterialsSetup
    {
        public const string Folder = "Assets/_Project/Art/Wordbound/ParcelService";
        public const string VisualPath = Folder + "/Profiles/ParcelServiceVisuals.asset";
        public const string RoomPath = Folder + "/Profiles/ParcelServiceRooms.asset";
        public const string RedPath = Folder + "/Materials/ParcelRedWood.mat";
        public const string OakPath = Folder + "/Materials/HoneyOak.mat";
        public const string WalnutPath = Folder + "/Materials/Walnut.mat";
        public const string BackupRoot = "BuildNotes/visual-coherence-20261029/map-composition/material-authoring-setup/before-assets";
        const string LibraryFolder = "Assets/_Project/Materials/Library/";
        const string Seeds = "Assets/_Project/Resources/TactileVariantSeeds/";
        const string BaseRooms = "Assets/_Project/Resources/Wordbound/RoomArchitecture.asset";
        static readonly string[] ModelIds = { "DESK", "SHELF", "CHEST", "BOX", "CRATE" };
        static readonly string[] Targets = { VisualPath, RoomPath, RedPath, OakPath, WalnutPath };
        static readonly string[] BackupFiles = { WordboundParcelServiceSetup.LegacyPath, WordboundParcelServiceSetup.LegacyPath + ".meta" };

        [MenuItem("Tools/Wordbound/Courses/Inspect Parcel Service material roles")]
        public static void InspectOriginalRoles()
        {
            RequireEditMode();
            Debug.Log("[Parcel Service] Original renderer/material references:\n" + string.Join("\n", InspectRoles()));
        }

        [MenuItem("Tools/Wordbound/Courses/Create or verify Parcel Service V1 finishes")]
        public static void CreateOrVerify()
        {
            RequireEditMode();
            var course = RequireSaved<WordboundCourseAsset>(WordboundParcelServiceSetup.LegacyPath);
            if (course.ReadDocument().Id != WordboundParcelServiceContent.LegacyCourseId)
                throw new InvalidOperationException("The target must remain the saved Parcel Service course.");
            var existing = AssetDatabase.LoadMainAssetAtPath(VisualPath);
            if (existing)
            {
                if (!(existing is WordboundVisualProfile)) throw new InvalidOperationException("Unexpected visual-profile target type.");
                if (!File.Exists(BackupRoot + "/COMPLETE.txt")) throw new InvalidOperationException("Incomplete finishes setup. Inspect the preserved backup before recovery.");
                VerifyBackups(); VerifySaved(course);
                Select(course.VisualProfile);
                Debug.Log("[Parcel Service] Saved finishes verified. Existing material, palette and course edits were retained; no assets rewritten.");
                return;
            }
            foreach (string path in Targets.Append(Folder).Append(BackupRoot)) RequireVacant(path);
            var original = RequireSaved<WordboundVisualProfile>(WordboundAuthoringSetup.VisualPath);
            if (course.VisualProfile != original || original.BaseArtwork != WordboundBaseArtwork.OriginalWreckabulary)
                throw new InvalidOperationException("Parcel Service must still reference the expected original Inspector presentation profile. A custom assignment is never replaced.");
            original.Validate();
            if (original.Overrides.Any(e => ModelIds.Contains(e.VisualKey)) || original.MaterialRemaps.Any(e => ModelIds.Contains(e.VisualKey)))
                throw new InvalidOperationException("An original fixture already has a custom visual or material mapping. Review it without replacing the edit.");
            var rooms = original.RoomStyle ? original.RoomStyle : RequireSaved<WordboundRoomStyleProfile>(BaseRooms);
            RequirePersistentClean(rooms); rooms.Validate();
            var packing = rooms.Palettes.SingleOrDefault(p => p.LayerId == "Packing");
            if (packing == null || new[] { packing.FloorMaterial, packing.WallMaterial, packing.CeilingMaterial,
                packing.FrameMaterial, packing.InsetMaterial, packing.AccentMaterial, rooms.WoodworkMaterial }.Any(m => m))
                throw new InvalidOperationException("The starting Packing room must use its original colour-driven materials. Existing custom material assignments are preserved.");
            var wood = RequireSaved<Material>(LibraryFolder + "wood_Classic.mat");
            var light = RequireSaved<Material>(LibraryFolder + "wood_light_Classic.mat");
            var woodSeed = RequireSeed("wood_Classic");
            var lightSeed = RequireSeed("wood_light_Classic");
            var roles = InspectRoles();
            foreach (string id in new[] { "DESK", "SHELF", "CHEST" })
                foreach (var source in new[] { wood, light }) RequireModelMaterial(id, source);
            var protectedPaths = AssetDatabase.GetDependencies(new[] {
                WordboundAuthoringSetup.VisualPath, AssetDatabase.GetAssetPath(rooms),
                AssetDatabase.GetAssetPath(woodSeed), AssetDatabase.GetAssetPath(lightSeed),
                "Assets/_Project/Resources/ModelLibrary.asset", "Assets/_Project/Resources/MaterialLibrary.asset"
            }, true).Where(File.Exists).SelectMany(p => File.Exists(p + ".meta") ? new[] { p, p + ".meta" } : new[] { p })
                .Distinct(StringComparer.Ordinal).OrderBy(p => p, StringComparer.Ordinal).ToArray();
            foreach (string path in protectedPaths.Where(p => !p.EndsWith(".meta", StringComparison.Ordinal)))
            {
                var value = AssetDatabase.LoadMainAssetAtPath(path);
                bool compiledPackageShader = value is Shader &&
                    path == "Packages/com.unity.render-pipelines.universal/Shaders/Lit.shader";
                if (value && !compiledPackageShader) RequirePersistentClean(value);
                var importer = AssetImporter.GetAtPath(path);
                if (importer && EditorUtility.IsDirty(importer))
                    throw new InvalidOperationException("Save pending importer edits before setup: " + path);
            }
            var protectedHashes = protectedPaths.ToDictionary(p => p, Hash, StringComparer.Ordinal);
            string gameplay = GameplayIdentity(course);
            Directory.CreateDirectory(BackupRoot);
            foreach (string path in BackupFiles)
            {
                string target = BackupRoot + "/" + path;
                Directory.CreateDirectory(Path.GetDirectoryName(target)); File.Copy(path, target, false);
            }
            File.WriteAllLines(BackupRoot + "/SHA256SUMS.txt", BackupFiles.Select(p => Hash(p) + "  " + p));
            VerifyBackups();
            File.WriteAllLines(BackupRoot + "/ORIGINAL-SOURCES.sha256", protectedHashes.Select(p => p.Value + "  " + p.Key));
            File.WriteAllLines(BackupRoot + "/ORIGINAL-MATERIAL-ROLES.txt", roles);
            EnsureFolder(Folder + "/Materials"); EnsureFolder(Folder + "/Profiles");
            var red = CloneMaterial(woodSeed, RedPath, "Parcel Red Wood", Hex(0xa94735));
            var oak = CloneMaterial(lightSeed, OakPath, "Honey Oak", Hex(0xb47a43));
            var walnut = CloneMaterial(woodSeed, WalnutPath, "Walnut", Hex(0x754d35));
            var styledRooms = Object.Instantiate(rooms);
            try
            {
                styledRooms.name = "Parcel Service Rooms";
                var palette = styledRooms.Palettes.Single(p => p.LayerId == "Packing");
                palette.Wall = Hex(0x30465d); palette.Ceiling = Hex(0xe9ddc6); palette.Floor = Hex(0x754d35);
                palette.Frame = Hex(0xb47a43); palette.Inset = Hex(0x30465d); palette.Accent = Hex(0xa94735);
                styledRooms.Woodwork = Hex(0xb47a43); styledRooms.Validate();
                AssetDatabase.CreateAsset(styledRooms, RoomPath); AssetDatabase.SaveAssetIfDirty(styledRooms);
            }
            catch { if (!AssetDatabase.Contains(styledRooms)) Object.DestroyImmediate(styledRooms); throw; }
            var visual = Object.Instantiate(original);
            try
            {
                visual.name = "Parcel Service Visuals"; visual.RoomStyle = styledRooms;
                visual.MaterialRemaps.AddRange(new[] {
                    Remap("DESK", wood, red), Remap("DESK", light, oak),
                    Remap("SHELF", wood, oak), Remap("SHELF", light, oak),
                    Remap("CHEST", wood, walnut), Remap("CHEST", light, oak)
                });
                visual.Validate(); AssetDatabase.CreateAsset(visual, VisualPath); AssetDatabase.SaveAssetIfDirty(visual);
            }
            catch { if (!AssetDatabase.Contains(visual)) Object.DestroyImmediate(visual); throw; }
            VerifyOriginals(protectedHashes);
            if (GameplayIdentity(course) != gameplay || course.VisualProfile != original)
                throw new InvalidOperationException("The course changed during setup. Saved finishes were retained, but the course assignment was not changed.");
            Undo.RecordObject(course, "Assign Parcel Service finishes");
            course.VisualProfile = visual; EditorUtility.SetDirty(course); AssetDatabase.SaveAssetIfDirty(course);
            VerifySaved(course); VerifyOriginals(protectedHashes);
            if (Hash(WordboundParcelServiceSetup.LegacyPath + ".meta") != Hash(BackupRoot + "/" + WordboundParcelServiceSetup.LegacyPath + ".meta"))
                throw new IOException("The existing Parcel Service asset identity changed unexpectedly.");
            if (GameplayIdentity(course) != gameplay) throw new InvalidOperationException("Course gameplay identity changed unexpectedly.");
            File.WriteAllText(BackupRoot + "/COMPLETE.txt", "Parcel Service finishes created. Exact original course backup retained. Shared sources unchanged.\n");
            Select(visual);
            Debug.Log("[Parcel Service] Course-local finishes saved: " + VisualPath + ". Edit room colours and material assets in the Inspector. Exact backup: " + BackupRoot);
        }

        static string GameplayIdentity(WordboundCourseAsset course)
        {
            var copy = Object.Instantiate(course);
            try { copy.VisualProfile = null; return EditorJsonUtility.ToJson(copy); }
            finally { Object.DestroyImmediate(copy); }
        }

        static void VerifySaved(WordboundCourseAsset course)
        {
            var visual = RequireSaved<WordboundVisualProfile>(VisualPath);
            var room = RequireSaved<WordboundRoomStyleProfile>(RoomPath);
            if (course.VisualProfile != visual || visual.RoomStyle != room)
                throw new InvalidOperationException("A course or room-profile assignment was edited. It has been preserved; review the saved configuration.");
            visual.Validate(); room.Validate();
            foreach (string path in new[] { RedPath, OakPath, WalnutPath })
            {
                var material = RequireSaved<Material>(path);
                if (!material.shader || !material.HasProperty("_BaseColor")) throw new InvalidOperationException("Invalid saved material: " + path);
            }
        }

        static WordboundMaterialRemap Remap(string key, Material source, Material replacement) =>
            new WordboundMaterialRemap { VisualKey = key, Source = source, Replacement = replacement };

        static Material CloneMaterial(Material source, string path, string label, Color colour)
        {
            var value = new Material(source) { name = label };
            try
            {
                value.SetColor("_BaseColor", colour);
                if (value.HasProperty("_Color")) value.SetColor("_Color", colour);
                AssetDatabase.CreateAsset(value, path); AssetDatabase.SaveAssetIfDirty(value); return value;
            }
            catch { if (!AssetDatabase.Contains(value)) Object.DestroyImmediate(value); throw; }
        }

        static Material RequireSeed(string name)
        {
            var seed = RequireSaved<Material>(Seeds + name + ".mat");
            if (!seed.shader || seed.shader.name != "Universal Render Pipeline/Lit" || !seed.HasProperty("_BaseColor") ||
                !seed.GetTexture("_BaseMap") || !seed.GetTexture("_DetailAlbedoMap") || !seed.GetTexture("_DetailNormalMap"))
                throw new InvalidOperationException("Saved original grain/detail maps are required: " + name);
            return seed;
        }

        static string[] InspectRoles()
        {
            var library = RequireSaved<ModelLibrary>("Assets/_Project/Resources/ModelLibrary.asset");
            var lines = new List<string>();
            foreach (string id in ModelIds)
            {
                var model = library.Find("Items/" + id);
                if (!model) throw new InvalidOperationException("Missing original model: " + id);
                RequirePersistentClean(model);
                foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
                    for (int i = 0; i < renderer.sharedMaterials.Length; i++)
                    {
                        var material = renderer.sharedMaterials[i];
                        if (!material) throw new InvalidOperationException("Missing original material: " + id);
                        RequirePersistentClean(material);
                        lines.Add(id + " | " + AnimationUtility.CalculateTransformPath(renderer.transform, model.transform) +
                            " | slot " + i + " | " + AssetDatabase.GetAssetPath(material) + " | " + material.name);
                    }
            }
            return lines.ToArray();
        }

        static void RequireModelMaterial(string id, Material source)
        {
            var model = RequireSaved<ModelLibrary>("Assets/_Project/Resources/ModelLibrary.asset").Find("Items/" + id);
            if (!model || !model.GetComponentsInChildren<Renderer>(true).Any(r => r.sharedMaterials.Contains(source)))
                throw new InvalidOperationException("The loaded model does not use the expected original material: " + id + "/" + source.name);
        }

        static T RequireSaved<T>(string path) where T : Object
        {
            var value = AssetDatabase.LoadAssetAtPath<T>(path);
            if (!value || !File.Exists(path) || !File.Exists(path + ".meta")) throw new IOException("Required saved asset missing: " + path);
            RequirePersistentClean(value); return value;
        }

        static void RequirePersistentClean(Object value)
        {
            if (!value || !AssetDatabase.Contains(value) || EditorUtility.IsDirty(value))
                throw new InvalidOperationException("Save pending asset edits before setup or verification: " + (value ? value.name : "missing asset"));
        }

        static void VerifyOriginals(Dictionary<string, string> originals)
        {
            foreach (var original in originals)
                if (Hash(original.Key) != original.Value) throw new IOException("A protected source changed during setup: " + original.Key);
        }

        static void VerifyBackups()
        {
            var lines = File.ReadAllLines(BackupRoot + "/SHA256SUMS.txt");
            if (lines.Length != BackupFiles.Length) throw new IOException("Incomplete original course backup.");
            for (int i = 0; i < BackupFiles.Length; i++)
                if (lines[i] != Hash(BackupRoot + "/" + BackupFiles[i]) + "  " + BackupFiles[i])
                    throw new IOException("Original course backup checksum/roster mismatch.");
        }

        static void RequireVacant(string path)
        {
            if (AssetDatabase.LoadMainAssetAtPath(path) || File.Exists(path) || Directory.Exists(path) || File.Exists(path + ".meta"))
                throw new IOException("Existing or partial target preserved. Inspect before recovery: " + path);
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            RequireVacant(path);
            string parent = Path.GetDirectoryName(path).Replace('\\', '/'); EnsureFolder(parent);
            if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent, Path.GetFileName(path)))) throw new IOException("Cannot create folder: " + path);
        }

        static void RequireEditMode()
        { if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Inspect or create finishes in Edit Mode."); }
        static void Select(Object value) { Selection.activeObject = value; EditorGUIUtility.PingObject(value); }
        static Color Hex(uint rgb) => new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);
        static string Hash(string path)
        {
            using var sha = SHA256.Create(); using var input = File.OpenRead(path);
            return BitConverter.ToString(sha.ComputeHash(input)).Replace("-", "").ToLowerInvariant();
        }
    }
}
