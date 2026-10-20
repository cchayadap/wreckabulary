using System;
using UnityEditor;
using UnityEngine;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Online.Editor
{
    public static class WordboundAuthoringMenus
    {
        [MenuItem("Assets/Create/Wreckabulary/Wordbound/Freestyle course with defaults")]
        static void CreateFreestyle() => Create(false);
        [MenuItem("Assets/Create/Wreckabulary/Wordbound/Flagship course copy")]
        static void CreateFlagship() => Create(true);
        static void Create(bool flagship)
        {
            var asset = ScriptableObject.CreateInstance<WordboundCourseAsset>();
            try
            {
                asset.Items = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/_Project/Data/Config/items.json");
                asset.RuleBook = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/_Project/Data/Config/rules.json");
                if (!asset.Items || !asset.RuleBook) throw new ArgumentException("The project item and rule TextAssets are unavailable.");
                if (flagship)
                {
                    var content = WordboundFlagshipContent.Create(ItemCatalogue.FromJson(asset.Items.text));
                    asset.Initialize(content.CopyDocument(), content.Geometry); asset.ModeId = "TimeRun";
                }
                else asset.Initialize(CourseTemplates.FreestyleWorld(), CourseTemplates.ProofCatalogue());
                ProjectWindowUtil.CreateAsset(asset, flagship ? "FlagshipCourse.asset" : "FreestyleCourse.asset");
            }
            catch { UnityEngine.Object.DestroyImmediate(asset); throw; }
        }
    }
}
