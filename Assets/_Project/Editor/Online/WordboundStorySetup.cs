using System;
using UnityEditor;
using UnityEngine;
using Wreckabulary.Online;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.EditorTools
{
    public static class WordboundStorySetup
    {
        public const string Folder = "Assets/_Project/Data/Wordbound/Authoring";
        public const string ProfilePath = Folder + "/LostAddressStory.asset";
        const string LaunchPath = "Assets/_Project/Resources/Wordbound/LaunchAssets.asset";

        [MenuItem("Tools/Wordbound/Create or verify story profile")]
        public static void Configure()
            => ConfigureAt(AssetDatabase.LoadAssetAtPath<WordboundLaunchAssets>(LaunchPath), Folder);

        static void ConfigureAt(WordboundLaunchAssets launch, string destination)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Create story assets in Edit Mode.");
            if (!launch) throw new InvalidOperationException("Create the existing Wordbound launch assets first.");
            var items = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/_Project/Data/Config/items.json");
            if (!items) throw new InvalidOperationException("The project item catalogue is missing.");
            var content = WordboundFlagshipContent.Create(ItemCatalogue.FromJson(items.text));
            if (launch.StoryProfile) launch.StoryProfile.Validate(content);
            EnsureFolder(destination);
            string path = destination + "/LostAddressStory.asset";
            var existing = AssetDatabase.LoadMainAssetAtPath(path);
            if (existing && !(existing is WordboundStoryProfile))
                throw new InvalidOperationException("An existing asset has a different type: " + path);
            var profile = existing as WordboundStoryProfile;
            bool created = !profile;
            if (created) profile = ScriptableObject.CreateInstance<WordboundStoryProfile>();
            try
            {
                bool empty = profile.Chapters != null && profile.Notices != null &&
                    profile.Chapters.Count == 0 && profile.Notices.Count == 0;
                if (empty) profile.PopulateDefaults(content);
                profile.Validate(content);
                if (created) AssetDatabase.CreateAsset(profile, path);
                if (created || empty)
                {
                    EditorUtility.SetDirty(profile);
                    AssetDatabase.SaveAssetIfDirty(profile);
                }
                if (!launch.StoryProfile)
                {
                    launch.StoryProfile = profile;
                    EditorUtility.SetDirty(launch);
                    AssetDatabase.SaveAssetIfDirty(launch);
                }
                Debug.Log("[Wreckabulary] Story profile verified: " + path +
                    "; launcher profile: " + AssetDatabase.GetAssetPath(launch.StoryProfile));
            }
            catch
            {
                if (created && !AssetDatabase.Contains(profile)) UnityEngine.Object.DestroyImmediate(profile);
                throw;
            }
        }

        static void EnsureFolder(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !path.StartsWith("Assets/", StringComparison.Ordinal) ||
                path.Contains("..") || path.Contains("\\"))
                throw new ArgumentException("Choose a story asset folder inside Assets.");
            string current = "Assets";
            foreach (string part in path.Substring("Assets/".Length).Split('/'))
            {
                if (part.Length == 0) throw new ArgumentException("Story asset folders cannot contain empty segments.");
                string next = current + "/" + part;
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, part);
                current = next;
            }
        }
    }
}
