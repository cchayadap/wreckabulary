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

        [TestCase(false), TestCase(true)]
        public void LegacyEpilogueListsUseClosedDefaultsWithoutMutatingTheAsset(bool nullList)
        {
            if (nullList) profile.Epilogues = null;
            Assert.DoesNotThrow(() => profile.Validate(content));
            foreach (string id in WordboundStoryProfile.EpilogueIds)
            {
                var expected = WordboundStoryProfile.DefaultEpilogue(id);
                Assert.AreEqual(expected.Title.Fallback, profile.EpilogueTitle(id));
                Assert.AreEqual(expected.Cue.Fallback, profile.EpilogueCue(id));
                Assert.AreEqual("Localized", profile.EpilogueCue(id, _ => "Localized"));
                Assert.AreEqual(expected.Cue.Fallback, profile.EpilogueCue(id, key => key));
            }
            if (nullList) Assert.IsNull(profile.Epilogues);
            else Assert.IsEmpty(profile.Epilogues);
            Assert.Throws<ArgumentException>(() => profile.EpilogueTitle("Restoration"));
        }

        [Test]
        public void AddingMissingEpiloguesPreservesAuthoredRowsAndIsIdempotent()
        {
            profile.PopulateDefaults(content);
            profile.Title.Fallback = "Authored title";
            profile.Notices[0].Cue.Fallback = "Authored cue.";
            profile.Chapters[0].Title.Fallback = "Authored chapter";
            var row = profile.Epilogues[0];
            row.Title.Fallback = "Our home";
            row.Cue.Key = "authored.home";
            profile.Epilogues.RemoveRange(1, 5);
            Assert.AreEqual(5, profile.AddMissingEpilogues());
            Assert.AreEqual(0, profile.AddMissingEpilogues());
            Assert.AreSame(row, profile.Epilogues[0]);
            Assert.AreEqual("Our home", row.Title.Fallback);
            Assert.AreEqual("authored.home", row.Cue.Key);
            Assert.AreEqual("Authored title", profile.Title.Fallback);
            Assert.AreEqual("Authored cue.", profile.Notices[0].Cue.Fallback);
            Assert.AreEqual("Authored chapter", profile.Chapters[0].Title.Fallback);
            CollectionAssert.AreEquivalent(WordboundStoryProfile.EpilogueIds, profile.Epilogues.Select(e => e.Id));
            Assert.DoesNotThrow(() => profile.Validate(content));
        }

        [TestCase("duplicate"), TestCase("unknown"), TestCase("null-row"), TestCase("null-title")]
        [TestCase("blank"), TestCase("long-title"), TestCase("long-cue"), TestCase("bad-key"), TestCase("too-many")]
        public void InvalidEpilogueCopyCannotChangeTheClosedOutcomeContract(string problem)
        {
            profile.AddMissingEpilogues();
            switch (problem)
            {
                case "duplicate": profile.Epilogues[1].Id = profile.Epilogues[0].Id; break;
                case "unknown": profile.Epilogues[0].Id = "Restoration"; break;
                case "null-row": profile.Epilogues[0] = null; break;
                case "null-title": profile.Epilogues[0].Title = null; break;
                case "blank": profile.Epilogues[0].Cue.Fallback = " "; break;
                case "long-title": profile.Epilogues[0].Title.Fallback = new string('a', 49); break;
                case "long-cue": profile.Epilogues[0].Cue.Fallback = new string('a', 97); break;
                case "bad-key": profile.Epilogues[0].Cue.Key = "invalid key"; break;
                case "too-many": profile.Epilogues.Add(WordboundStoryProfile.DefaultEpilogue("TimeRunSuccess")); break;
            }
            string before = JsonUtility.ToJson(profile);
            Assert.Throws<ArgumentException>(() => profile.Validate(content));
            Assert.Throws<ArgumentException>(() => profile.AddMissingEpilogues());
            Assert.AreEqual(before, JsonUtility.ToJson(profile));
        }

        [Test]
        public void LegacyJsonWithNoEpilogueFieldCanBeExtendedWithoutReplacingStoryCopy()
        {
            JsonUtility.FromJsonOverwrite("{\"Title\":{\"Key\":\"old.title\",\"Fallback\":\"Old story title\"},\"Chapters\":[],\"Notices\":[]}", profile);
            Assert.AreEqual("The keepsake is home.", profile.EpilogueCue("TimeRunSuccess"));
            profile.AddMissingEpilogues();
            profile.Epilogues[0].Cue.Fallback = "一起回家。";
            string json = JsonUtility.ToJson(profile);
            JsonUtility.FromJsonOverwrite(json, profile);
            profile.Validate(content);
            Assert.AreEqual("Old story title", profile.Title.Fallback);
            Assert.AreEqual("old.title", profile.Title.Key);
            Assert.AreEqual("一起回家。", profile.EpilogueCue("TimeRunSuccess"));
        }
    }
}
