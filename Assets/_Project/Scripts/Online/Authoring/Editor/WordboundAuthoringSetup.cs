using System;
using UnityEditor;
using UnityEngine;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Online.Editor
{
    public static class WordboundAuthoringSetup
    {
        public const string Folder = "Assets/_Project/Data/Wordbound/Authoring";
        public const string CameraPath = Folder + "/PlayerCamera.asset";
        public const string ThemePath = Folder + "/Interface.asset";
        public const string VisualPath = Folder + "/Presentation.asset";
        public const string FreestylePath = Folder + "/FreestyleCourse.asset";
        public const string FlagshipPath = Folder + "/FlagshipCourse.asset";

        [MenuItem("Tools/Wordbound/Create or verify Inspector authoring assets")]
        public static void CreateDefaults()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Create authoring assets in Edit Mode.");
            var launch = Resources.Load<WordboundLaunchAssets>(WordboundLaunchAssets.ResourcePath);
            CreateDefaultsAt(launch, Folder);
        }
        static void CreateDefaultsAt(WordboundLaunchAssets launch, string destination)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Create authoring assets in Edit Mode.");
            if (!launch) throw new InvalidOperationException("Create the existing Wordbound launch assets first.");
            var items = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/_Project/Data/Config/items.json");
            var rules = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/_Project/Data/Config/rules.json");
            if (!items || !rules) throw new InvalidOperationException("The project item catalogue and rule book are missing.");
            EnsureFolder(destination);
            string cameraPath = destination + "/PlayerCamera.asset", visualPath = destination + "/Presentation.asset";
            string themePath = destination + "/Interface.asset", freestylePath = destination + "/FreestyleCourse.asset";
            string flagshipPath = destination + "/FlagshipCourse.asset";
            var camera = Create<WordboundCameraProfile>(cameraPath, value => value.Validate());
            var visual = Create<WordboundVisualProfile>(visualPath, value => value.Validate());
            var theme = Create<WordboundUiTheme>(themePath, value => {
                value.BodyFont = Resources.Load<Font>("Fonts/Nunito-ExtraBold");
                value.HeadingFont = Resources.Load<Font>("Fonts/Nunito-Black");
                value.Validate();
            });
            var freestyle = Create<WordboundCourseAsset>(freestylePath, value => {
                value.Initialize(CourseTemplates.FreestyleWorld(), CourseTemplates.ProofCatalogue());
                Bind(value, items, rules, camera, visual);
            });
            var flagship = Create<WordboundCourseAsset>(flagshipPath, value => {
                var content = WordboundFlagshipContent.Create(ItemCatalogue.FromJson(items.text));
                value.Initialize(content.CopyDocument(), content.Geometry); value.ModeId = "TimeRun";
                Bind(value, items, rules, camera, visual);
            });
            camera.Validate(); visual.Validate(); theme.Validate();
            _ = freestyle.CreateContent(freestyle.Resolve());
            _ = flagship.CreateContent(flagship.Resolve());
            var serialized = new SerializedObject(launch);
            AssignIfEmpty(serialized, "CameraProfile", camera);
            AssignIfEmpty(serialized, "VisualProfile", visual);
            AssignIfEmpty(serialized, "UiTheme", theme);
            AssignIfEmpty(serialized, "WorkshopCourse", flagship);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(launch); AssetDatabase.SaveAssetIfDirty(launch);
            Debug.Log("Wordbound Inspector assets verified. Camera: " + cameraPath + "; visuals: " + visualPath +
                "; interface: " + themePath + "; Freestyle: " + freestylePath + "; flagship copy: " + flagshipPath +
                "; launcher: " + AssetDatabase.GetAssetPath(launch) + ". Existing assignments were preserved.");
        }
        static void Bind(WordboundCourseAsset course, TextAsset items, TextAsset rules,
            WordboundCameraProfile camera, WordboundVisualProfile visual)
        { course.Items = items; course.RuleBook = rules; course.CameraProfile = camera; course.VisualProfile = visual; }
        static T Create<T>(string path, Action<T> initialize) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadMainAssetAtPath(path);
            if (existing)
            {
                if (existing is T typed) return typed;
                throw new InvalidOperationException("An existing asset has a different type: " + path);
            }
            var value = ScriptableObject.CreateInstance<T>();
            try
            {
                initialize(value); AssetDatabase.CreateAsset(value, path);
                EditorUtility.SetDirty(value); AssetDatabase.SaveAssetIfDirty(value); return value;
            }
            catch { if (!AssetDatabase.Contains(value)) UnityEngine.Object.DestroyImmediate(value); throw; }
        }
        static void AssignIfEmpty(SerializedObject target, string name, UnityEngine.Object value)
        {
            var property = target.FindProperty(name);
            if (property == null || property.propertyType != SerializedPropertyType.ObjectReference)
                throw new InvalidOperationException("The launcher must expose the authoring hook: " + name);
            if (!property.objectReferenceValue) property.objectReferenceValue = value;
        }
        static void EnsureFolder(string path)
        {
            string current = "Assets";
            foreach (string part in path.Substring("Assets/".Length).Split('/'))
            {
                string next = current + "/" + part;
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, part);
                current = next;
            }
        }
    }
}
