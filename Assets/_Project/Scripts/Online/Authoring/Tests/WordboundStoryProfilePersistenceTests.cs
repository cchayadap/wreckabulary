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
                profile.Epilogues[0].Title.Fallback = "Our home";
                profile.Epilogues[0].Cue.Fallback = "一起回家。";
                profile.Epilogues[0].Cue.Key = "story.test.ending";
                profile.Epilogues.RemoveRange(1, 5);
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
                Assert.AreEqual(5, profile.AddMissingEpilogues());
                Assert.AreEqual(0, profile.AddMissingEpilogues());
                EditorUtility.SetDirty(profile);
                AssetDatabase.SaveAssets();
                Resources.UnloadAsset(profile);
                profile = AssetDatabase.LoadAssetAtPath<WordboundStoryProfile>(path);
                profile.Validate(content);
                Assert.AreEqual(6, profile.Epilogues.Count);
                Assert.AreEqual("Our home", profile.Epilogues[0].Title.Fallback);
                Assert.AreEqual("一起回家。", profile.Epilogues[0].Cue.Fallback);
                Assert.AreEqual("story.test.ending", profile.Epilogues[0].Cue.Key);
                Assert.AreEqual("把正確的地址送回家。", profile.Notices[0].Cue.Fallback);
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
