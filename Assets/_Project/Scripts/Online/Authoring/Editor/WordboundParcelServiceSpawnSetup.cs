using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Online.Editor
{
    public static class WordboundParcelServiceSpawnSetup
    {
        static readonly CoursePoint OldPosition = WordboundParcelServiceContent.RearStartSeat;
        static readonly CoursePoint ClearPosition = WordboundParcelServiceContent.StartSeat;

        [MenuItem("Tools/Wordbound/Courses/Repair Parcel Depot initial camera clearance")]
        public static void Apply()
        {
            string backup = "BuildNotes/visual-coherence-20261029/map-composition/spawn-camera/before-assets/" +
                DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            bool changed = ApplyAt(WordboundParcelServiceSetup.Path, backup);
            Debug.Log(changed ? "Parcel Depot Start0 moved into the clear yard aisle. Exact prior course files: " + backup :
                "Parcel Depot Start0 already has the corrected position; saved course preserved.");
        }

        public static bool ApplyAt(string path, string backupDirectory)
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Repair a saved course in Edit Mode.");
            Require(!string.IsNullOrEmpty(path) && path.StartsWith("Assets/", StringComparison.Ordinal) &&
                path.EndsWith(".asset", StringComparison.Ordinal) && !path.Contains('\\') && !path.Contains(':') &&
                path.Split('/').All(part => part != "" && part != "." && part != ".."), "Use a project-relative saved course path.");
            var course = AssetDatabase.LoadAssetAtPath<WordboundCourseAsset>(path);
            Require(course && AssetDatabase.Contains(course) && File.Exists(path) && File.Exists(path + ".meta"), "The saved course and metadata are required.");
            Require(new UnityEngine.Object[] { course, course.Items, course.RuleBook, course.CameraProfile, course.VisualProfile }
                .Where(value => value).All(value => AssetDatabase.Contains(value) && !EditorUtility.IsDirty(value)), "Save pending course/input/profile edits first.");
            var document = course.ReadDocument();
            Require(document.Id == WordboundParcelServiceContent.CourseId, "Only the saved Parcel Depot course is supported.");
            var start = document.Spawns.SingleOrDefault(spawn => spawn.Id == "Start0");
            Require(start != null && start.ModuleId == "Yard" && start.Yaw == 0, "Start0 module/orientation was edited; preserve and review it manually.");
            if (start.Position.Equals(ClearPosition)) { Validate(course); return false; }
            Require(start.Position.Equals(OldPosition), "Start0 position was edited; preserve and review it manually.");
            Validate(course);
            var before = File.ReadAllBytes(path); var beforeMeta = File.ReadAllBytes(path + ".meta");
            string guid = AssetDatabase.AssetPathToGUID(path), memory = JsonUtility.ToJson(course);
            var dependency = AssetDatabase.GetAssetDependencyHash(path);
            start.Position = ClearPosition;
            var prepared = UnityEngine.Object.Instantiate(course);
            string expected;
            try { prepared.name = course.name; prepared.SaveDocument(document); Validate(prepared); expected = JsonUtility.ToJson(prepared); }
            finally { UnityEngine.Object.DestroyImmediate(prepared); }
            string backup = Path.GetFullPath(backupDirectory), assetRoot = Path.GetFullPath("Assets") + Path.DirectorySeparatorChar;
            Require(!backup.StartsWith(assetRoot, StringComparison.OrdinalIgnoreCase) && backup != Path.GetFullPath("Assets") &&
                !Directory.Exists(backup) && !File.Exists(backup), "Choose a new backup directory outside Assets.");
            Require(JsonUtility.ToJson(course) == memory && AssetDatabase.GetAssetDependencyHash(path) == dependency &&
                File.ReadAllBytes(path).SequenceEqual(before) && File.ReadAllBytes(path + ".meta").SequenceEqual(beforeMeta), "Course/dependencies changed during preparation.");
            Directory.CreateDirectory(backup);
            WriteNew(Path.Combine(backup, Path.GetFileName(path)), before);
            WriteNew(Path.Combine(backup, Path.GetFileName(path) + ".meta"), beforeMeta);
            WriteNew(Path.Combine(backup, "before.json"), Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new {
                path, guid, sourceHash = course.SourceHash, inputHash = course.InputHash,
                assetSha256 = Hash(before), metaSha256 = Hash(beforeMeta), from = OldPosition, to = ClearPosition
            }, Formatting.Indented)));
            try
            {
                course.SaveDocument(document);
                Require(JsonUtility.ToJson(course) == expected, "Only the prepared Start0 document mutation is permitted.");
                EditorUtility.SetDirty(course); AssetDatabase.SaveAssetIfDirty(course);
                Require(AssetDatabase.AssetPathToGUID(path) == guid && File.ReadAllBytes(path + ".meta").SequenceEqual(beforeMeta), "Course GUID/metadata changed unexpectedly.");
                Require(JsonUtility.ToJson(course) == expected && !EditorUtility.IsDirty(course), "The prepared course did not save exactly.");
                Validate(course);
                WriteNew(Path.Combine(backup, "result.json"), Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new {
                    status = "Start0 updated", path, guid, sourceHash = course.SourceHash,
                    inputHash = course.InputHash, assetSha256 = Hash(File.ReadAllBytes(path))
                }, Formatting.Indented)));
                return true;
            }
            catch (Exception error)
            { throw new InvalidOperationException("Parcel Depot update needs review. Exact before files remain at " + backup, error); }
        }

        static void Validate(WordboundCourseAsset course)
        {
            Require(course.Items && course.RuleBook, "Keep the explicit saved item/rule inputs.");
            course.CameraProfile?.Validate(); course.VisualProfile?.Validate();
            _ = course.CreateContent(course.Resolve());
            var geometry = course.Catalogue();
            foreach (string id in new[] { "TimeRun", "Freestyle" })
            {
                var mode = ModeDefinition.For(id, RaceFormat.Solo);
                var resolved = CourseResolver.Resolve(course.ReadDocument(), geometry,
                    new CourseResolveRequest(course.Seed, MatchStateValidation.Requirements(mode)));
                Require(resolved.Success, id + ": " + string.Join("; ", resolved.Issues.Select(issue => issue.Message)));
                _ = new WordboundAuthoredPuzzleContent(mode, geometry, resolved.Manifest, course.Items.text, course.RuleBook.text);
            }
        }
        static void Require(bool valid, string message) { if (!valid) throw new InvalidOperationException(message); }
        static string Hash(byte[] value)
        { using var hash = SHA256.Create(); return BitConverter.ToString(hash.ComputeHash(value)).Replace("-", "").ToLowerInvariant(); }
        static void WriteNew(string path, byte[] bytes)
        { using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None); stream.Write(bytes, 0, bytes.Length); }
    }
}
