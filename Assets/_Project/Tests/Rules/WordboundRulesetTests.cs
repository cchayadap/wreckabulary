using System;
using System.Linq;
using NUnit.Framework;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Tests
{
    public sealed class WordboundRulesetTests
    {
        static readonly string[] modes = { "TimeRun", "TeamRace", "BrawlRace", "FreeForAll", "Freestyle" };

        [TestCase("TimeRun", RaceFormat.Solo, "9e114ec92835d4b5ff0ecda80aaf7aaa0fe2a28920f65fe3b4e5c7c8f037e1a2")]
        [TestCase("TeamRace", RaceFormat.Solo, "0e2a1e9d8d99efac3329dc03894c40cce1e6d05a80a0457e1be941e0c7e2992f")]
        [TestCase("TeamRace", RaceFormat.Duo, "a2ada8f2b53ab6ad9091e018b13b799bb161079b7238faa56954075e484dbbab")]
        [TestCase("TeamRace", RaceFormat.Squad, "cdc9164d67bcd3e67fb1b59610e7e532fd41b3fa661038033cea17c432f9f98e")]
        [TestCase("BrawlRace", RaceFormat.Solo, "54295861cb10d06b4da56f1c031d1bb561018cbaa0a86d1d0002c40fff4bb776")]
        [TestCase("BrawlRace", RaceFormat.Duo, "6bdd6bc2b2429d24b14f79f92f71e62ce0ff27a735cc033e5f8cc7147575ab75")]
        [TestCase("BrawlRace", RaceFormat.Squad, "d4f898616508111318b8685e04fb8ffba7f0c23073cd50aadfb2890855827f84")]
        [TestCase("FreeForAll", RaceFormat.Solo, "63b019fc66cdb05c6c84a11a424a58194f731070b054baeb5d2707d44d138cfa")]
        [TestCase("Freestyle", RaceFormat.Solo, "bffc34b6defb237d51a36866998a00b3d0921e91568a787c78cfbf13c2d98c71")]
        public void ClassicKeepsThePublishedModeHash(string id, RaceFormat format, string published)
        {
            Assert.AreEqual(published, ModeDefinition.For(id, format).Hash);
            Assert.AreEqual(published, ModeDefinition.For(id, format, "Classic").Hash);
            Assert.AreEqual(published, ModeDefinition.For(id, format, WordboundRuleset.Classic).Hash);
            Assert.AreSame(WordboundRuleset.Classic, ModeDefinition.For(id, format).Ruleset);
        }

        [TestCase("TeamRace", RaceFormat.Duo, "DeadLetter.Rush")]
        [TestCase("BrawlRace", RaceFormat.Squad, "Unspeller.LightsOut.Steady")]
        [TestCase("TimeRun", RaceFormat.Solo, "House.Storm")]
        [TestCase("TimeRun", RaceFormat.Solo, "Unspeller.Rush.G2")]
        [TestCase("Freestyle", RaceFormat.Solo, "DeadLetter.Free")]
        public void StoryRulesetsFoldTheirHashAfterThePracticeSuffix(string id, RaceFormat format, string rulesetId)
        {
            var ruleset = WordboundRuleset.Parse(rulesetId);
            var mode = ModeDefinition.For(id, format, rulesetId);
            Assert.AreEqual(WordboundIdentity.Sha256(ModeDefinition.For(id, format).Hash + "|" + ruleset.Hash), mode.Hash);
            Assert.AreEqual(rulesetId, mode.Ruleset.Id);
            Assert.AreEqual(mode.Hash, ModeDefinition.For(id, format, ruleset).Hash);
            Assert.AreEqual(2, ModeDefinition.Version);
        }

        [Test]
        public void StandardRulesetsHaveDistinctCanonicalIdsAndHashes()
        {
            var all = WordboundRuleset.Standard;
            Assert.AreEqual(22, all.Count);
            Assert.AreEqual(all.Count, all.Select(r => r.Id).Distinct(StringComparer.Ordinal).Count());
            Assert.AreEqual(all.Count, all.Select(r => r.Hash).Distinct(StringComparer.Ordinal).Count());
            foreach (var ruleset in all)
            {
                Assert.IsTrue(WordboundIdentity.IsHash(ruleset.Hash), ruleset.Id);
                Assert.IsTrue(WordboundIdentity.IsId(ruleset.Id), ruleset.Id);
                Assert.IsFalse(ruleset.Id.Contains(":"), ruleset.Id);
                Assert.AreEqual(ruleset.Hash, WordboundRuleset.Parse(ruleset.Id).Hash);
                Assert.IsFalse(ruleset.LocalOnly, ruleset.Id);
            }
            Assert.AreEqual(1, all.Count(r => r.IsClassic));
            Assert.AreEqual(9, all.Count(r => r.Director));
        }

        [Test]
        public void EveryApplicableRulesetGivesTheModeADistinctHash()
        {
            foreach (var id in modes)
            {
                var hashes = WordboundRuleset.Standard.Where(r => r.AppliesTo(id))
                    .Select(r => ModeDefinition.For(id, RaceFormat.Solo, r).Hash).ToArray();
                Assert.AreEqual(hashes.Length, hashes.Distinct(StringComparer.Ordinal).Count(), id);
            }
            Assert.AreNotEqual(ModeDefinition.For("TimeRun", RaceFormat.Solo, "Unspeller.Rush").Hash,
                ModeDefinition.For("TimeRun", RaceFormat.Solo, "Unspeller.Rush.G1").Hash);
            Assert.AreNotEqual(ModeDefinition.For("TeamRace", RaceFormat.Duo, "DeadLetter.Rush").Hash,
                ModeDefinition.For("TeamRace", RaceFormat.Squad, "DeadLetter.Rush").Hash);
        }

        [TestCase("Classic", WordboundStoryId.Classic, false, 0)]
        [TestCase("DeadLetter.Rush", WordboundStoryId.DeadLetter, true, 0)]
        [TestCase("House.Storm.Steady", WordboundStoryId.ForgettingHouse, false, 0)]
        [TestCase("Unspeller.Rush.G2", WordboundStoryId.Unspeller, true, 2)]
        [TestCase("Unspeller.LightsOut.Steady.G3", WordboundStoryId.Unspeller, false, 3)]
        [TestCase("DeadLetter.Free", WordboundStoryId.DeadLetter, false, 0)]
        [TestCase("Unspeller.Free.G1", WordboundStoryId.Unspeller, false, 1)]
        public void ParseReadsStoryDirectorAndGrudge(string id, WordboundStoryId story, bool director, int grudge)
        {
            var ruleset = WordboundRuleset.Parse(id);
            Assert.AreEqual(id, ruleset.Id);
            Assert.AreEqual(story, ruleset.Story);
            Assert.AreEqual(director, ruleset.Director);
            Assert.AreEqual(grudge, ruleset.Grudge);
            Assert.AreEqual(grudge > 0, ruleset.LocalOnly);
            Assert.AreEqual(story == WordboundStoryId.Classic, ruleset.IsClassic);
            Assert.IsTrue(WordboundRuleset.TryParse(id, out var again));
            Assert.AreEqual(ruleset.Hash, again.Hash);
        }

        [TestCase("DeadLetter.Rush", WordboundShift.Rush)]
        [TestCase("House.Storm", WordboundShift.Storm)]
        [TestCase("Unspeller.LightsOut", WordboundShift.LightsOut)]
        [TestCase("House.Free", WordboundShift.Free)]
        public void ParseReadsTheShift(string id, WordboundShift shift)
        {
            Assert.AreEqual(shift, WordboundRuleset.Parse(id).Shift);
        }

        [TestCase("")]
        [TestCase("classic")]
        [TestCase("DeadLetter")]
        [TestCase("DeadLetter.")]
        [TestCase("DeadLetter.Rush.Rush")]
        [TestCase("DeadLetter.Free.Steady")]
        [TestCase("DeadLetter.Rush.G1")]
        [TestCase("House.Storm.G2")]
        [TestCase("Unspeller.Rush.G0")]
        [TestCase("Unspeller.Rush.G4")]
        [TestCase("Unspeller.Rush.G2.Steady")]
        [TestCase("Unspeller.Rush.Steady.Steady")]
        [TestCase("ForgettingHouse.Rush")]
        [TestCase("Classic.Rush")]
        [TestCase("House.Sprint")]
        [TestCase("Unspeller..Rush")]
        [TestCase("deadletter.rush")]
        [TestCase("DeadLetter:Rush")]
        public void ParseRejectsNonCanonicalIds(string id)
        {
            Assert.IsFalse(WordboundRuleset.TryParse(id, out var ruleset));
            Assert.IsNull(ruleset);
            Assert.Throws<ArgumentException>(() => WordboundRuleset.Parse(id));
        }

        [Test]
        public void ParseRejectsNull()
        {
            Assert.IsFalse(WordboundRuleset.TryParse(null, out _));
            Assert.Throws<ArgumentException>(() => WordboundRuleset.Parse(null));
        }

        [TestCase("Classic", "TimeRun,TeamRace,BrawlRace,FreeForAll,Freestyle")]
        [TestCase("DeadLetter.Rush", "TimeRun,TeamRace,BrawlRace")]
        [TestCase("House.Storm.Steady", "TimeRun,TeamRace,BrawlRace")]
        [TestCase("Unspeller.LightsOut.G2", "TimeRun,TeamRace,BrawlRace")]
        [TestCase("DeadLetter.Free", "Freestyle")]
        [TestCase("Unspeller.Free.G3", "Freestyle")]
        public void ApplicabilityFollowsTheShift(string id, string expected)
        {
            var ruleset = WordboundRuleset.Parse(id);
            CollectionAssert.AreEqual(expected.Split(','), modes.Where(ruleset.AppliesTo).ToArray());
            Assert.IsFalse(ruleset.AppliesTo("Unknown"));
            Assert.IsFalse(ruleset.AppliesTo(null));
        }

        [Test]
        public void ModesRefuseRulesetsThatDoNotApply()
        {
            Assert.Throws<ArgumentException>(() => ModeDefinition.For("FreeForAll", RaceFormat.Solo, "DeadLetter.Rush"));
            Assert.Throws<ArgumentException>(() => ModeDefinition.For("FreeForAll", RaceFormat.Solo, "DeadLetter.Free"));
            Assert.Throws<ArgumentException>(() => ModeDefinition.For("Freestyle", RaceFormat.Solo, "House.Rush"));
            Assert.Throws<ArgumentException>(() => ModeDefinition.For("TimeRun", RaceFormat.Solo, "House.Free"));
            Assert.Throws<ArgumentException>(() => ModeDefinition.For("TeamRace", RaceFormat.Solo, "Story.Rush"));
            Assert.Throws<ArgumentException>(() => ModeDefinition.For("Unknown", RaceFormat.Solo, "Classic"));
            Assert.Throws<ArgumentNullException>(() => ModeDefinition.For("TeamRace", RaceFormat.Solo, (WordboundRuleset)null));
            Assert.Throws<ArgumentException>(() => ModeDefinition.For("TimeRun", RaceFormat.Duo, "DeadLetter.Rush"));
        }

        [TestCase("TeamRace", "DeadLetter.Rush", true)]
        [TestCase("BrawlRace", "House.LightsOut", true)]
        [TestCase("BrawlRace", "Unspeller.Storm.Steady", false)]
        [TestCase("TimeRun", "DeadLetter.Rush", false)]
        [TestCase("Freestyle", "DeadLetter.Free", false)]
        [TestCase("FreeForAll", "Classic", false)]
        [TestCase("TeamRace", "Classic", false)]
        public void DirectorRunsOnlyInTwoTeamRacesWithoutSteady(string modeId, string rulesetId, bool director)
        {
            Assert.AreEqual(director, WordboundRuleset.Parse(rulesetId).DirectorFor(modeId));
        }

        [Test]
        public void ClassicHasNoDeckAndStoriesFollowTheShiftTable()
        {
            Assert.IsFalse(WordboundRuleset.Classic.Deck.Enabled);
            Assert.AreEqual(WordboundRuleset.Classic.Hash, WordboundRuleset.Parse("Classic").Hash);
            foreach (var ruleset in WordboundRuleset.Standard.Where(r => !r.IsClassic))
            {
                Assert.IsTrue(ruleset.Deck.Enabled, ruleset.Id);
                Assert.AreEqual(ruleset.Shift == WordboundShift.Free, ruleset.Deck.JobBoard, ruleset.Id);
                Assert.AreEqual(ruleset.Director, ruleset.Deck.Allows(WordboundCard.ExpressChute), ruleset.Id);
                Assert.AreEqual(ruleset.Director, ruleset.Deck.Allows(WordboundCard.InspectionDelay), ruleset.Id);
            }
            Assert.AreNotEqual(WordboundRuleset.Parse("DeadLetter.Rush").Hash, WordboundRuleset.Parse("House.Rush").Hash);
            Assert.AreNotEqual(WordboundRuleset.Parse("DeadLetter.Rush").Hash, WordboundRuleset.Parse("DeadLetter.Rush.Steady").Hash);
        }
    }
}
