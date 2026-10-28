using System;
using UnityEditor;
using UnityEngine;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Online.Editor
{
    public static class WordboundArrivalVariantSetup
    {
        public const string Path = "Assets/_Project/Data/Wordbound/Authoring/Variants/ParcelHallArrivalV2.asset";

        [MenuItem("Tools/Wordbound/Courses/Create or select furnished Parcel Hall")]
        public static void CreateOrSelect()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Create a course variant in Edit Mode.");
            var existing = AssetDatabase.LoadMainAssetAtPath(Path);
            if (existing && !(existing is WordboundCourseAsset))
                throw new InvalidOperationException("A different asset already occupies " + Path);
            var course = existing as WordboundCourseAsset;
            if (!course)
            {
                var source = AssetDatabase.LoadAssetAtPath<WordboundCourseAsset>(WordboundAuthoringSetup.FlagshipPath);
                if (!source) throw new InvalidOperationException("Create the existing Inspector authoring assets first.");
                string folder = WordboundAuthoringSetup.Folder + "/Variants";
                if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(WordboundAuthoringSetup.Folder, "Variants");
                course = UnityEngine.Object.Instantiate(source); course.name = "Parcel Hall Arrival";
                try
                {
                    course.SaveDocument(WordboundArrivalVariant.Create(source.ReadDocument(), source.Catalogue()));
                    _ = course.CreateContent(course.Resolve());
                    AssetDatabase.CreateAsset(course, Path); AssetDatabase.SaveAssetIfDirty(course);
                }
                catch { if (!AssetDatabase.Contains(course)) UnityEngine.Object.DestroyImmediate(course); throw; }
            }
            _ = course.CreateContent(course.Resolve());
            Selection.activeObject = course; EditorGUIUtility.PingObject(course);
            Debug.Log("Local Parcel Hall variant selected. Use its existing Inspector Preview and Play controls. " +
                "Published map catalogues and launcher assignments were preserved.");
        }
    }
}
