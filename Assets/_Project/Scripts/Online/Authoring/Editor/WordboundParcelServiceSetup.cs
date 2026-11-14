using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Wreckabulary.Art;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Online.Editor
{
    public static class WordboundParcelServiceSetup
    {
        public const string Path = "Assets/_Project/Resources/Wordbound/LocalCourses/ParcelServiceV2.asset";
        public const string ResourcePath = "Wordbound/LocalCourses/ParcelServiceV2";
        public const string LegacyPath = "Assets/_Project/Resources/Wordbound/LocalCourses/ParcelServiceV1.asset";
        static readonly string[] EarlierGenerations = { "c87ecf24958661efcd9c386a813e97f9f1408ea649e55ed74fc6880c22c7994c" };

        [MenuItem("Tools/Wordbound/Courses/Create or select Parcel Depot")]
        public static void CreateOrSelect()
        {
            var source = LegacyInputs() ?? AssetDatabase.LoadAssetAtPath<WordboundCourseAsset>(WordboundAuthoringSetup.FlagshipPath);
            if (!source) throw new InvalidOperationException("Create the existing Inspector authoring assets first.");
            string backup = "BuildNotes/visual-coherence-20261029/map-composition/parcel-depot-furnishing/" +
                DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            bool refreshed = RefreshEarlierGenerationAt(Path, backup);
            var course = CreateOrSelectAt(Path, source.Items, source.RuleBook, source.CameraProfile, source.VisualProfile);
            Selection.activeObject = course; EditorGUIUtility.PingObject(course);
            Debug.Log((refreshed ? "The unedited earlier Parcel Depot generation now has the current furnishings; exact prior course files: " + backup + ". " : "") +
                "Parcel Depot saved course selected: " + Path + " (inputs from " + AssetDatabase.GetAssetPath(source) + "). " +
                "Next: Tools/Wordbound/Parcel Depot/Bind saved course to local launch. The Parcel Service V1 asset, other maps and public catalogues were preserved.");
        }

        public static bool RefreshEarlierGenerationAt(string path, string backupDirectory) =>
            RefreshEarlierGenerationAt(path, backupDirectory, EarlierGenerations);

        public static bool RefreshEarlierGenerationAt(string path, string backupDirectory, IReadOnlyCollection<string> earlierSourceHashes)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Update courses in Edit Mode.");
            var course = AssetDatabase.LoadAssetAtPath<WordboundCourseAsset>(path);
            if (!course || !course.HasDocument || earlierSourceHashes == null || !earlierSourceHashes.Contains(course.SourceHash)) return false;
            if (!course.Items || !File.Exists(path + ".meta") || new UnityEngine.Object[] { course, course.Items, course.RuleBook, course.CameraProfile, course.VisualProfile }
                .Any(value => value && (!AssetDatabase.Contains(value) || EditorUtility.IsDirty(value))))
                throw new InvalidOperationException("Save pending course/input/profile edits before updating Parcel Depot furnishings.");
            var content = WordboundParcelServiceContent.Create(ItemCatalogue.FromJson(course.Items.text));
            var prepared = UnityEngine.Object.Instantiate(course);
            try { prepared.Initialize(content.CopyDocument(), content.Geometry); Validate(prepared); }
            finally { UnityEngine.Object.DestroyImmediate(prepared); }
            string backup = System.IO.Path.GetFullPath(backupDirectory), assets = System.IO.Path.GetFullPath("Assets");
            if (backup.StartsWith(assets + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || backup == assets ||
                Directory.Exists(backup) || File.Exists(backup))
                throw new InvalidOperationException("Choose a new backup directory outside Assets.");
            byte[] before = File.ReadAllBytes(path), meta = File.ReadAllBytes(path + ".meta");
            string guid = AssetDatabase.AssetPathToGUID(path), name = System.IO.Path.GetFileName(path);
            Directory.CreateDirectory(backup);
            WriteNew(System.IO.Path.Combine(backup, name), before);
            WriteNew(System.IO.Path.Combine(backup, name + ".meta"), meta);
            try
            {
                course.Initialize(content.CopyDocument(), content.Geometry);
                EditorUtility.SetDirty(course); AssetDatabase.SaveAssetIfDirty(course);
                if (AssetDatabase.AssetPathToGUID(path) != guid || !File.ReadAllBytes(path + ".meta").SequenceEqual(meta) || EditorUtility.IsDirty(course))
                    throw new IOException("The saved Parcel Depot identity changed unexpectedly.");
                Validate(course);
                return true;
            }
            catch (Exception error)
            { throw new InvalidOperationException("Parcel Depot furnishing update needs review. Exact prior course files remain at " + backup, error); }
        }

        static void WriteNew(string path, byte[] bytes)
        { using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None); stream.Write(bytes, 0, bytes.Length); }

        public static WordboundCourseAsset LegacyInputs()
        {
            var legacy = AssetDatabase.LoadAssetAtPath<WordboundCourseAsset>(LegacyPath);
            if (!legacy || !legacy.Items || !legacy.RuleBook || !legacy.CameraProfile || !legacy.VisualProfile) return null;
            try { return legacy.ReadDocument().Id == WordboundParcelServiceContent.LegacyCourseId ? legacy : null; }
            catch (ArgumentException) { return null; }
        }

        public static WordboundCourseAsset CreateOrSelectAt(string path, TextAsset items, TextAsset rules,
            WordboundCameraProfile camera, WordboundVisualProfile visual)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Create courses in Edit Mode.");
            if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/", StringComparison.Ordinal) ||
                !path.EndsWith(".asset", StringComparison.Ordinal) || path.Contains('\\') || path.Contains(':') ||
                path.Split('/').Any(p => p == "" || p == "." || p == ".."))
                throw new ArgumentException("Choose a new project-relative .asset path.");
            var existing = AssetDatabase.LoadMainAssetAtPath(path);
            if (existing)
            {
                if (!(existing is WordboundCourseAsset saved)) throw new InvalidOperationException("A different asset already occupies " + path);
                if (EditorUtility.IsDirty(saved)) throw new InvalidOperationException("Save pending course Inspector edits before verifying it.");
                Validate(saved); return saved;
            }
            if (File.Exists(path) || File.Exists(path + ".meta") || Directory.Exists(path))
                throw new InvalidOperationException("Unimported or partial content already occupies " + path + "; preserve and resolve it first.");
            if (!items || !rules || !camera || !visual) throw new InvalidOperationException("Assign saved items, rules, camera and original artwork profiles.");
            if (new UnityEngine.Object[] { items, rules, camera, visual }.Any(value =>
                !AssetDatabase.Contains(value) || EditorUtility.IsDirty(value)))
                throw new InvalidOperationException("Save the referenced gameplay assets before creating the course.");
            camera.Validate(); visual.Validate();
            if (visual.BaseArtwork != WordboundBaseArtwork.OriginalWreckabulary)
                throw new InvalidOperationException("Create Parcel Depot using the original Wreckabulary artwork profile.");
            var content = WordboundParcelServiceContent.Create(ItemCatalogue.FromJson(items.text));
            var models = ModelLibrary.Load();
            foreach (var id in content.Geometry.Assets.Keys)
                if (!models || !models.Find("Items/" + id)) throw new InvalidOperationException("Original model is missing: Items/" + id);
            var course = ScriptableObject.CreateInstance<WordboundCourseAsset>();
            try
            {
                course.name = "Parcel Depot V2";
                course.Initialize(content.CopyDocument(), content.Geometry);
                course.Items = items; course.RuleBook = rules; course.CameraProfile = camera; course.VisualProfile = visual;
                course.Seed = "0123456789abcdef"; course.ModeId = "TimeRun"; course.Format = RaceFormat.Solo;
                Validate(course);
                string folder = path.Substring(0, path.LastIndexOf('/'));
                EnsureFolder(folder);
                AssetDatabase.CreateAsset(course, path); AssetDatabase.SaveAssetIfDirty(course);
                return course;
            }
            catch { if (!AssetDatabase.Contains(course)) UnityEngine.Object.DestroyImmediate(course); throw; }
        }

        static void Validate(WordboundCourseAsset course)
        {
            if (!course.Items || !course.RuleBook || course.ReadDocument().Id != WordboundParcelServiceContent.CourseId)
                throw new InvalidOperationException("The existing asset must remain an explicit Parcel Depot course with assigned gameplay data.");
            course.CameraProfile?.Validate(); course.VisualProfile?.Validate();
            _ = course.CreateContent(course.Resolve());
            foreach (string id in new[] { "TimeRun", "Freestyle" })
            {
                var mode = ModeDefinition.For(id, RaceFormat.Solo);
                var geometry = course.Catalogue();
                var result = CourseResolver.Resolve(course.ReadDocument(), geometry,
                    new CourseResolveRequest(course.Seed, MatchStateValidation.Requirements(mode)));
                if (!result.Success) throw new InvalidOperationException(id + ": " + string.Join("; ", result.Issues.Select(i => i.EntityId + ": " + i.Message)));
                _ = new WordboundAuthoredPuzzleContent(mode, geometry, result.Manifest, course.Items.text, course.RuleBook.text);
            }
        }

        static void EnsureFolder(string path)
        {
            if (path == "Assets") return;
            string current = "Assets";
            foreach (string part in path.Substring("Assets/".Length).Split('/'))
            {
                string next = current + "/" + part;
                if (!AssetDatabase.IsValidFolder(next))
                {
                    if (File.Exists(next) || File.Exists(next + ".meta") || Directory.Exists(next))
                        throw new InvalidOperationException("Unimported content occupies required folder " + next);
                    if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(current, part)))
                        throw new IOException("Could not create asset folder " + next);
                }
                current = next;
            }
        }
    }
}
