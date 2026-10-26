using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Wreckabulary.Online;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Tests
{
    public sealed class WordboundStoryProfilePersistenceTests
    {
        [Test]
        public void AuthoredStoryCopySurvivesNativeAssetSaveAndReload()
        {
            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/WordboundStoryProfileTest.asset");
            var profile = ScriptableObject.CreateInstance<WordboundStoryProfile>();
            try
            {
                var items = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/_Project/Data/Config/items.json");
                Assert.IsNotNull(items);
                var content = WordboundFlagshipContent.Create(ItemCatalogue.FromJson(items.text));
                profile.PopulateDefaults(content);
                string id = profile.Notices[0].Id;
                string journal = profile.Notices[0].Clue.Fallback;
                profile.Notices[0].Cue.Fallback = "把正確的地址送回家。";
                profile.Notices[0].Cue.Key = "story.test.cue";
                AssetDatabase.CreateAsset(profile, path);
                EditorUtility.SetDirty(profile);
                AssetDatabase.SaveAssets();
                Resources.UnloadAsset(profile);
                profile = AssetDatabase.LoadAssetAtPath<WordboundStoryProfile>(path);
                Assert.IsNotNull(profile);
                profile.Validate(content);
                Assert.AreEqual(14, profile.Notices.Count);
                Assert.AreEqual(id, profile.Notices[0].Id);
                Assert.AreEqual("把正確的地址送回家。", profile.Notices[0].Cue.Fallback);
                Assert.AreEqual("story.test.cue", profile.Notices[0].Cue.Key);
                Assert.AreEqual(journal, profile.Notices[0].Clue.Fallback);
            }
            finally
            {
                if (AssetDatabase.LoadAssetAtPath<WordboundStoryProfile>(path)) AssetDatabase.DeleteAsset(path);
                else if (profile) Object.DestroyImmediate(profile);
            }
        }
    }
}
