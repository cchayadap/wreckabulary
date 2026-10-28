using System;
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
        public const string Path = "Assets/_Project/Resources/Wordbound/LocalCourses/ParcelServiceV1.asset";
        public const string ResourcePath = "Wordbound/LocalCourses/ParcelServiceV1";

        [MenuItem("Tools/Wordbound/Courses/Create or select Parcel Service")]
        public static void CreateOrSelect()
        {
            var source = AssetDatabase.LoadAssetAtPath<WordboundCourseAsset>(WordboundAuthoringSetup.FlagshipPath);
            if (!source) throw new InvalidOperationException("Create the existing Inspector authoring assets first.");
            var course = CreateOrSelectAt(Path, source.Items, source.RuleBook, source.CameraProfile, source.VisualProfile);
            Selection.activeObject = course; EditorGUIUtility.PingObject(course);
            Debug.Log("Parcel Service saved course selected: " + Path + ". Edit/apply, validate and preview, then Play exact preview locally. " +
                "Existing maps, launcher assignments and public catalogues were preserved.");
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
                throw new InvalidOperationException("Create Parcel Service using the original Wreckabulary artwork profile.");
            var content = WordboundParcelServiceContent.Create(ItemCatalogue.FromJson(items.text));
            var models = ModelLibrary.Load();
            foreach (var id in content.Geometry.Assets.Keys)
                if (!models || !models.Find("Items/" + id)) throw new InvalidOperationException("Original model is missing: Items/" + id);
            var course = ScriptableObject.CreateInstance<WordboundCourseAsset>();
            try
            {
                course.name = "Parcel Service V1";
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
                throw new InvalidOperationException("The existing asset must remain an explicit Parcel Service course with assigned gameplay data.");
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
