using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace Wreckabulary.Online.Editor
{
    public static class WordboundParcelServiceLaunchSetup
    {
        const string LaunchPath = "Assets/_Project/Resources/Wordbound/LaunchAssets.asset";

        [MenuItem("Tools/Wordbound/Parcel Depot/Bind saved course to local launch")]
        public static void BindInstalledCourse()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Leave Play mode before binding a saved course.");
            var launch = AssetDatabase.LoadAssetAtPath<WordboundLaunchAssets>(LaunchPath);
            Require(launch, "Create the existing Wordbound launch assets before binding Parcel Depot.");
            var source = AssetDatabase.LoadAssetAtPath<WordboundCourseAsset>(WordboundParcelServiceSetup.Path);
            Require(source, "Create the saved Parcel Depot course first: Tools/Wordbound/Courses/Create or select Parcel Depot.");
            var legacy = AssetDatabase.LoadAssetAtPath<WordboundCourseAsset>(WordboundParcelServiceSetup.LegacyPath);
            var targets = new[] { LaunchPath }.Concat(AssetDatabase.FindAssets("t:" + nameof(WordboundLaunchAssets))
                    .Select(AssetDatabase.GUIDToAssetPath).Where(path => path != LaunchPath).OrderBy(path => path, StringComparer.Ordinal))
                .Distinct().Select(path => (path, launch: AssetDatabase.LoadAssetAtPath<WordboundLaunchAssets>(path)))
                .Where(pair => pair.launch).ToArray();
            foreach (var input in targets.Select(pair => (UnityEngine.Object)pair.launch).Concat(new UnityEngine.Object[] {
                source, source.Items, source.RuleBook, source.CameraProfile, source.VisualProfile }))
                Require(!input || !EditorUtility.IsDirty(input), "Save pending launch/course input edits before binding Parcel Depot.");
            Validate(source);
            if (launch.ParcelServiceCourse && launch.ParcelServiceCourse != source && launch.ParcelServiceCourse != legacy)
                Validate(launch.ParcelServiceCourse);

            var bound = new List<string>();
            string backup = null;
            foreach (var (path, target) in targets)
            {
                bool replaceable = path == LaunchPath ? !target.ParcelServiceCourse || target.ParcelServiceCourse == legacy :
                    legacy && target.ParcelServiceCourse == legacy;
                if (!replaceable) continue;
                backup ??= NewBackup();
                Bind(path, target, source, Path.Combine(backup, bound.Count.ToString("00") + "-" + Path.GetFileName(Path.GetDirectoryName(path))));
                bound.Add(path);
            }
            Selection.activeObject = launch;
            Debug.Log(bound.Count == 0 ? "Parcel Depot launch assignments are already valid; existing saved assignments were preserved." :
                "Parcel Depot was assigned to " + string.Join(", ", bound) + ". The Parcel Service V1 asset is unchanged. Exact prior launch files: " + backup);
        }

        static void Validate(WordboundCourseAsset course)
        {
            foreach (string mode in new[] { "TimeRun", "Freestyle" })
                WordboundLauncher.CreateSoloContent(null, null, mode, course.Seed, WordboundSoloEntry.ParcelServiceMap, course);
        }

        static string NewBackup()
        {
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string backup = Path.Combine(project, "BuildNotes", "visual-coherence-20261029", "map-composition", "launch-binding",
                DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Require(!Directory.Exists(backup), "The launch backup path already exists.");
            return backup;
        }

        static void Bind(string launchPath, WordboundLaunchAssets launch, WordboundCourseAsset source, string backup)
        {
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string launchFile = Path.Combine(project, launchPath), launchMeta = launchFile + ".meta";
            string sourcePath = AssetDatabase.GetAssetPath(source);
            Require(sourcePath.StartsWith("Assets/", StringComparison.Ordinal), "Parcel Depot must be a saved project asset.");
            string sourceFile = Path.Combine(project, sourcePath);
            byte[] before = File.ReadAllBytes(launchFile), meta = File.ReadAllBytes(launchMeta);
            byte[] sourceBytes = File.ReadAllBytes(sourceFile), sourceMeta = File.ReadAllBytes(sourceFile + ".meta");
            string launchGuid = AssetDatabase.AssetPathToGUID(launchPath), sourceGuid = AssetDatabase.AssetPathToGUID(sourcePath);
            Require(launchGuid.Length > 0 && sourceGuid.Length > 0, "Both saved assets need existing GUIDs.");
            string previousPath = launch.ParcelServiceCourse ? AssetDatabase.GetAssetPath(launch.ParcelServiceCourse) : "";
            Require(!Directory.Exists(backup), "The launch backup path already exists.");
            Directory.CreateDirectory(backup);
            WriteNew(Path.Combine(backup, "LaunchAssets.asset.before"), before);
            WriteNew(Path.Combine(backup, "LaunchAssets.asset.meta.before"), meta);
            WriteNew(Path.Combine(backup, "before.json"), System.Text.Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new {
                launchPath, launchGuid, launchSha256 = Hash(before), launchMetaSha256 = Hash(meta), previousPath,
                sourcePath, sourceGuid, sourceSha256 = Hash(sourceBytes), sourceMetaSha256 = Hash(sourceMeta)
            }, Formatting.Indented)));
            try
            {
                launch.ParcelServiceCourse = source;
                EditorUtility.SetDirty(launch); AssetDatabase.SaveAssetIfDirty(launch);
                Require(launch.ParcelServiceCourse == source, "The saved launch did not retain its explicit course reference.");
                Require(AssetDatabase.AssetPathToGUID(launchPath) == launchGuid && AssetDatabase.AssetPathToGUID(sourcePath) == sourceGuid,
                    "A saved asset GUID changed while binding Parcel Depot.");
                Require(File.ReadAllBytes(launchMeta).SequenceEqual(meta) && File.ReadAllBytes(sourceFile).SequenceEqual(sourceBytes) &&
                    File.ReadAllBytes(sourceFile + ".meta").SequenceEqual(sourceMeta), "Course bytes or asset metadata changed unexpectedly.");
                WriteNew(Path.Combine(backup, "result.json"), System.Text.Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new {
                    status = "assigned", launchPath, launchGuid, sourcePath, sourceGuid,
                    launchSha256 = Hash(File.ReadAllBytes(launchFile)), backup
                }, Formatting.Indented)));
            }
            catch (Exception error)
            {
                throw new InvalidOperationException("Parcel Depot binding needs review. Exact pre-write launch files are retained at " + backup, error);
            }
        }

        static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        static string Hash(byte[] bytes)
        {
            using var hash = SHA256.Create();
            return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }
        static void WriteNew(string path, byte[] bytes)
        {
            using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            stream.Write(bytes, 0, bytes.Length);
        }
    }
}
