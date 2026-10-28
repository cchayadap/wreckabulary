using System;
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
        const string CoursePath = "Assets/_Project/Resources/Wordbound/LocalCourses/ParcelServiceV1.asset";

        [MenuItem("Tools/Wordbound/Parcel Service/Bind saved course to local launch")]
        public static void BindInstalledCourse()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Leave Play mode before binding a saved course.");
            var launch = AssetDatabase.LoadAssetAtPath<WordboundLaunchAssets>(LaunchPath);
            Require(launch, "Create the existing Wordbound launch assets before binding Parcel Service.");
            var source = launch.ParcelServiceCourse ? launch.ParcelServiceCourse :
                AssetDatabase.LoadAssetAtPath<WordboundCourseAsset>(CoursePath);
            Require(source, "Create the saved Parcel Service course first.");
            foreach (var input in new UnityEngine.Object[] { launch, source, source.Items, source.RuleBook,
                source.CameraProfile, source.VisualProfile })
                Require(!input || !EditorUtility.IsDirty(input), "Save pending launch/course input edits before binding Parcel Service.");
            foreach (string mode in new[] { "TimeRun", "Freestyle" })
                WordboundLauncher.CreateSoloContent(null, null, mode, source.Seed, WordboundSoloEntry.ParcelServiceMap, source);
            if (launch.ParcelServiceCourse)
            {
                Selection.activeObject = launch;
                Debug.Log("Parcel Service launch assignment is already valid; the existing saved assignment was preserved.");
                return;
            }

            string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string launchFile = Path.Combine(project, LaunchPath), launchMeta = launchFile + ".meta";
            string sourcePath = AssetDatabase.GetAssetPath(source);
            Require(sourcePath.StartsWith("Assets/", StringComparison.Ordinal), "Parcel Service must be a saved project asset.");
            string sourceFile = Path.Combine(project, sourcePath);
            byte[] before = File.ReadAllBytes(launchFile), meta = File.ReadAllBytes(launchMeta);
            byte[] sourceBytes = File.ReadAllBytes(sourceFile), sourceMeta = File.ReadAllBytes(sourceFile + ".meta");
            string launchGuid = AssetDatabase.AssetPathToGUID(LaunchPath), sourceGuid = AssetDatabase.AssetPathToGUID(sourcePath);
            Require(launchGuid.Length > 0 && sourceGuid.Length > 0, "Both saved assets need existing GUIDs.");
            string backup = Path.Combine(project, "BuildNotes", "visual-coherence-20261029", "map-composition", "launch-binding",
                DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Require(!Directory.Exists(backup), "The launch backup path already exists.");
            Directory.CreateDirectory(backup);
            WriteNew(Path.Combine(backup, "LaunchAssets.asset.before"), before);
            WriteNew(Path.Combine(backup, "LaunchAssets.asset.meta.before"), meta);
            WriteNew(Path.Combine(backup, "before.json"), System.Text.Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new {
                launchPath = LaunchPath, launchGuid, launchSha256 = Hash(before), launchMetaSha256 = Hash(meta),
                sourcePath, sourceGuid, sourceSha256 = Hash(sourceBytes), sourceMetaSha256 = Hash(sourceMeta)
            }, Formatting.Indented)));
            try
            {
                launch.ParcelServiceCourse = source;
                EditorUtility.SetDirty(launch); AssetDatabase.SaveAssetIfDirty(launch);
                Require(launch.ParcelServiceCourse == source, "The saved launch did not retain its explicit course reference.");
                Require(AssetDatabase.AssetPathToGUID(LaunchPath) == launchGuid && AssetDatabase.AssetPathToGUID(sourcePath) == sourceGuid,
                    "A saved asset GUID changed while binding Parcel Service.");
                Require(File.ReadAllBytes(launchMeta).SequenceEqual(meta) && File.ReadAllBytes(sourceFile).SequenceEqual(sourceBytes) &&
                    File.ReadAllBytes(sourceFile + ".meta").SequenceEqual(sourceMeta), "Course bytes or asset metadata changed unexpectedly.");
                WriteNew(Path.Combine(backup, "result.json"), System.Text.Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new {
                    status = "assigned", launchPath = LaunchPath, launchGuid, sourcePath, sourceGuid,
                    launchSha256 = Hash(File.ReadAllBytes(launchFile)), backup
                }, Formatting.Indented)));
            }
            catch (Exception error)
            {
                throw new InvalidOperationException("Parcel Service binding needs review. Exact pre-write launch files are retained at " + backup, error);
            }
            Selection.activeObject = launch;
            Debug.Log("Parcel Service was assigned to local launch. Exact prior launch files: " + backup);
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
