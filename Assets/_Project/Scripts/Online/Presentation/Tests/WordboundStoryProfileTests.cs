using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Wreckabulary.Online;
using Wreckabulary.Rules.Wordbound;
using Object = UnityEngine.Object;

namespace Wreckabulary.Tests
{
    public sealed class WordboundStoryProfileTests
    {
        WordboundFlagshipContent content;
        WordboundStoryProfile profile;

        [SetUp]
        public void Create()
        {
            content = WordboundFlagshipContent.Create(WordboundPuzzleGuidePlanTests.Items());
            profile = ScriptableObject.CreateInstance<WordboundStoryProfile>();
        }

        [TearDown]
        public void Cleanup() => Object.DestroyImmediate(profile);

        [Test]
        public void DefaultsPreserveEveryCanonicalClueAndHaveDistinctConciseCues()
        {
            profile.PopulateDefaults(content);
            Assert.AreEqual(3, profile.Chapters.Count);
            Assert.AreEqual(14, profile.Notices.Count);
            Assert.AreEqual(14, profile.Notices.Select(n => n.Id).Distinct().Count());
            foreach (var beat in content.Story)
            {
                string id = beat.Lap + ":" + beat.CheckpointId;
                var row = profile.Notices.Single(n => n.Id == id);
                Assert.AreEqual(beat.Clue, row.Clue.Fallback);
                Assert.AreEqual(WordboundStoryProfile.DefaultCue(id), row.Cue.Fallback);
                Assert.AreNotEqual("A clue for the journal.", row.Cue.Fallback, id);
                Assert.LessOrEqual(row.Cue.Fallback.Split(' ').Length, 7, id);
                Assert.AreNotEqual(row.Cue.Key, row.Clue.Key);
            }
            Assert.DoesNotThrow(() => profile.Validate(content));
        }

        [Test]
        public void LocalizedCopyDoesNotChangeRouteIdentityOrPositions()
        {
            profile.PopulateDefaults(content);
            var course = content.Resolve("0123456789abcdef", MatchStateValidation.Requirements(ModeDefinition.For("TeamRace"))).Manifest;
            var original = new WordboundStoryPlan(course, WordboundPuzzleGuidePlanTests.Items());
            var translated = new WordboundStoryPlan(course, WordboundPuzzleGuidePlanTests.Items(), profile, key => "Translated " + key);
            Assert.AreEqual(original.Notices.Count, translated.Notices.Count);
            for (int i = 0; i < original.Notices.Count; i++)
            {
                var before = original.Notices[i]; var after = translated.Notices[i];
                Assert.AreEqual(before.Id, after.Id);
                Assert.AreEqual(before.Lap, after.Lap);
                Assert.AreEqual(before.Checkpoint, after.Checkpoint);
                Assert.AreEqual(before.Position, after.Position);
                Assert.AreEqual(before.Place, after.Place);
                Assert.That(after.Cue, Does.StartWith("Translated "));
                Assert.That(after.Clue, Does.StartWith("Translated "));
                Assert.That(after.Chapter, Does.StartWith("Translated "));
            }
            Assert.AreEqual(course.Hash, content.Resolve("0123456789abcdef",
                MatchStateValidation.Requirements(ModeDefinition.For("TeamRace"))).Manifest.Hash);
        }

        [TestCase(null), TestCase(""), TestCase("  "), TestCase("cue.key")]
        public void MissingTranslationsUseFallback(string translation)
        {
            var text = new WordboundStoryText { Key = "cue.key", Fallback = "Look upstairs." };
            Assert.AreEqual("Look upstairs.", text.Resolve(_ => translation));
            Assert.AreEqual("Look upstairs.", text.Resolve());
        }

        [Test]
        public void PartialProfilesFallBackWithoutLosingUneditedClues()
        {
            var beat = content.Story.First();
            string id = beat.Lap + ":" + beat.CheckpointId;
            Assert.DoesNotThrow(() => profile.Validate(content));
            Assert.AreEqual(beat.Clue, profile.Clue(id, beat.Clue));
            Assert.AreEqual(WordboundStoryProfile.DefaultCue(id), profile.Cue(id));
            profile.PopulateDefaults(content);
            profile.Notices[0].Cue.Fallback = "Edited cue.";
            Assert.Throws<InvalidOperationException>(() => profile.PopulateDefaults(content));
            Assert.AreEqual("Edited cue.", profile.Notices[0].Cue.Fallback);
        }

        [TestCase("duplicate"), TestCase("unknown"), TestCase("blank"), TestCase("long"), TestCase("chapter")]
        public void InvalidPresentationRowsAreRejected(string problem)
        {
            profile.PopulateDefaults(content);
            switch (problem)
            {
                case "duplicate": profile.Notices[1].Id = profile.Notices[0].Id; break;
                case "unknown": profile.Notices[0].Id = "1:NotInTheCourse"; break;
                case "blank": profile.Notices[0].Clue.Fallback = " "; break;
                case "long": profile.Notices[0].Cue.Fallback = new string('a', 97); break;
                case "chapter": profile.Chapters[0].Id = "UnwrittenEnding"; break;
            }
            Assert.Throws<ArgumentException>(() => profile.Validate(content));
        }

        [Test]
        public void ProfileSerializationPreservesUnicodeKeysAndFullJournalCopy()
        {
            profile.PopulateDefaults(content);
            profile.Notices[0].Cue.Fallback = "把正確的地址送回家。";
            string json = JsonUtility.ToJson(profile);
            var restored = ScriptableObject.CreateInstance<WordboundStoryProfile>();
            try
            {
                JsonUtility.FromJsonOverwrite(json, restored);
                restored.Validate(content);
                Assert.AreEqual(profile.Notices[0].Cue.Fallback, restored.Notices[0].Cue.Fallback);
                CollectionAssert.AreEqual(profile.Notices.Select(n => n.Id), restored.Notices.Select(n => n.Id));
                CollectionAssert.AreEqual(profile.Notices.Select(n => n.Clue.Key), restored.Notices.Select(n => n.Clue.Key));
                CollectionAssert.AreEqual(profile.Notices.Select(n => n.Clue.Fallback), restored.Notices.Select(n => n.Clue.Fallback));
            }
            finally { Object.DestroyImmediate(restored); }
        }
    }
}
