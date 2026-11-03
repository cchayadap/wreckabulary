using System;
using System.Linq;
using NUnit.Framework;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Rules.Tests
{
    public sealed class WorkshopModeTests
    {
        [Test]
        public void RaceTemplateOffersExplicitTimeRunWithoutChangingTheCourse()
        {
            var preset = WorkshopTemplates.Create(WorkshopTemplate.RaceCircuit);
            var course = preset.Snapshot.Course();
            string before = CourseJson.ExportDocument(course);
            var choice = WorkshopModes.Select(course, "TimeRun", RaceFormat.Solo);
            Assert.IsTrue(choice.AllowsOneHumanLocal);
            Assert.AreEqual(CourseCapability.Circuit, choice.Requirements.Capability);
            Assert.AreEqual(1, choice.Requirements.TeamCount);
            Assert.AreEqual(4, choice.Requirements.TeamCapacity);
            Assert.AreEqual(CourseOwnershipPolicy.TeamIsolated, choice.Requirements.Ownership);
            Assert.AreEqual(before, CourseJson.ExportDocument(course));
            Assert.IsTrue(CourseResolver.Resolve(course, preset.Catalogue,
                new CourseResolveRequest(preset.Snapshot.Seed, choice.Requirements)).Success);
        }

        [Test]
        public void ArenaSandboxEnablementIsExplicitAndUndoable()
        {
            var preset = WorkshopTemplates.Create(WorkshopTemplate.CombatArena);
            var history = new WorkshopHistory(preset.Snapshot);
            string original = history.Current.CourseJsonText;
            Assert.Throws<ArgumentException>(() => WorkshopModes.Select(history.Current.Course(), "Freestyle", RaceFormat.Solo));
            history.Execute("Enable Freestyle sandbox", WorkshopModes.EnableFreestyleSandbox);
            var choice = WorkshopModes.Select(history.Current.Course(), "Freestyle", RaceFormat.Solo);
            Assert.AreEqual(CourseCapability.Explore, choice.Requirements.Capability);
            Assert.IsTrue(history.Preview(preset.Catalogue, choice.Requirements).Success);
            Assert.IsTrue(history.Undo()); Assert.AreEqual(original, history.Current.CourseJsonText);
            Assert.IsTrue(history.Redo()); Assert.Contains("Freestyle", history.Current.Course().SupportedModes);
            Assert.IsFalse(history.Execute("Enable again", WorkshopModes.EnableFreestyleSandbox));
        }

        [TestCase("TimeRun", true)]
        [TestCase("Freestyle", true)]
        [TestCase("TeamRace", false)]
        [TestCase("BrawlRace", false)]
        [TestCase("FreeForAll", false)]
        public void LocalPlaytestUsesOnlyTheExactPermittedRequirements(string mode, bool local)
        {
            var play = MatchStateValidation.Requirements(ModeDefinition.For(mode));
            Assert.AreEqual(local, WorkshopModes.AllowsOneHumanLocal(play));
        }

        [Test]
        public void ForgedCapacityOrOwnershipCannotMasqueradeAsLocalTimeRun()
        {
            Assert.IsFalse(WorkshopModes.AllowsOneHumanLocal(new CoursePlayRequirements("TimeRun",
                CourseCapability.Circuit, 2, 1, 2, CourseOwnershipPolicy.TeamIsolated)));
            Assert.IsFalse(WorkshopModes.AllowsOneHumanLocal(new CoursePlayRequirements("TimeRun",
                CourseCapability.Circuit, 1, 4, 4, CourseOwnershipPolicy.Shared)));
        }

        [TestCase("TimeRun", RaceFormat.Solo, "Carry the keepsake through 3 laps · 25 min", "1–4 players · all together")]
        [TestCase("TeamRace", RaceFormat.Duo, "Carry the keepsake through 3 laps · 25 min", "2–4 players · 2 vs 2")]
        [TestCase("BrawlRace", RaceFormat.Squad, "Carry the keepsake through 3 laps · 25 min", "2–8 players · 4 vs 4 · knockdowns on")]
        [TestCase("FreeForAll", RaceFormat.Solo, "First to 10 knockouts · 8 min", "2–8 players · everyone for themselves · knockdowns on")]
        [TestCase("Freestyle", RaceFormat.Solo, "Explore and build freely · no time limit", "1–8 players · all together")]
        public void ModeCardsDescribeTheFixedModeRules(string mode, RaceFormat format, string win, string players)
        {
            var definition = ModeDefinition.For(mode, format);
            var summary = WorkshopModes.Summary(mode, format);
            Assert.AreEqual(win, summary.HowToWin); Assert.AreEqual(players, summary.Players);
            Assert.AreEqual(definition.IsRace, summary.IsRace); Assert.AreEqual(definition.HasRaceFormat, summary.HasTeamFormat);
            Assert.AreEqual(definition.Name, summary.Name);
            Assert.AreSame(WorkshopModes.Definition(mode, format), WorkshopModes.Definition(mode, format));
            Assert.AreEqual(definition.Hash, WorkshopModes.Definition(mode, format).Hash);
            Assert.AreSame(summary, WorkshopModes.Summary(mode, format));
            var formats = definition.HasRaceFormat ? new[] { RaceFormat.Solo, RaceFormat.Duo, RaceFormat.Squad } : new[] { RaceFormat.Solo };
            foreach (var each in formats)
                Assert.AreEqual(each, WorkshopModes.FormatOf(MatchStateValidation.Requirements(ModeDefinition.For(mode, each))), mode + " " + each);
            if (!definition.HasRaceFormat)
                Assert.Throws<ArgumentException>(() => WorkshopModes.Definition(mode, RaceFormat.Duo));
        }

        [Test]
        public void SupportedModesToggleAsUndoableEditsAndKeepAtLeastOne()
        {
            var preset = WorkshopTemplates.Create(WorkshopTemplate.RaceCircuit);
            var history = new WorkshopHistory(preset.Snapshot);
            string original = history.Current.CourseJsonText;
            Assert.IsTrue(history.Execute("Add Free-for-All", draft => WorkshopModes.SetSupported(draft, "FreeForAll", true)));
            Assert.Contains("FreeForAll", history.Current.Course().SupportedModes);
            Assert.IsFalse(history.Execute("Add again", draft => WorkshopModes.SetSupported(draft, "FreeForAll", true)));
            Assert.IsTrue(history.Execute("Remove Team Race", draft => WorkshopModes.SetSupported(draft, "TeamRace", false)));
            CollectionAssert.DoesNotContain(history.Current.Course().SupportedModes, "TeamRace");
            Assert.IsFalse(history.Execute("Remove missing", draft => WorkshopModes.SetSupported(draft, "Freestyle", false)));
            Assert.Throws<ArgumentException>(() => history.Execute("Unknown", draft => WorkshopModes.SetSupported(draft, "Golf", true)));
            Assert.Throws<ArgumentNullException>(() => WorkshopModes.SetSupported(null, "TimeRun", true));
            var single = history.Current.Edit(); single.Course.SupportedModes.Clear(); single.Course.SupportedModes.Add("TimeRun");
            Assert.Throws<InvalidOperationException>(() => WorkshopModes.SetSupported(single, "TimeRun", false));
            Assert.IsTrue(history.Undo()); Assert.IsTrue(history.Undo());
            Assert.AreEqual(original, history.Current.CourseJsonText);
        }

        [Test]
        public void ModeNeedsReportWhatAnUnsupportedOrIncompleteModeStillRequires()
        {
            var race = WorkshopTemplates.Create(WorkshopTemplate.RaceCircuit);
            var course = race.Snapshot.Course();
            string before = CourseJson.ExportDocument(course);
            CollectionAssert.IsEmpty(WorkshopModes.Needs(course, race.Catalogue, "TimeRun"));
            var arena = WorkshopModes.Needs(course, race.Catalogue, "FreeForAll");
            CollectionAssert.IsNotEmpty(arena); Assert.LessOrEqual(arena.Count, 3);
            Assert.AreEqual(before, CourseJson.ExportDocument(course));
            var broken = race.Snapshot.Edit(); broken.Course.Laps.RemoveAll(lap => lap.Number == 3);
            CollectionAssert.IsNotEmpty(WorkshopModes.Needs(broken.Course, race.Catalogue, "TimeRun"));
        }

        [Test]
        public void CompetitiveFormatsAreVisibleAndUnsupportedChoicesFail()
        {
            var course = WorkshopTemplates.Create(WorkshopTemplate.RaceCircuit).Snapshot.Course();
            var choices = WorkshopModes.Choices(course).Where(c => c.ModeId == "TeamRace").ToArray();
            CollectionAssert.AreEqual(new[] { RaceFormat.Solo, RaceFormat.Duo, RaceFormat.Squad }, choices.Select(c => c.Format));
            CollectionAssert.AreEqual(new[] { 2, 4, 8 }, choices.Select(c => c.Requirements.MaxOccupants));
            Assert.Throws<ArgumentException>(() => WorkshopModes.Select(course, "FreeForAll", RaceFormat.Solo));
            Assert.Throws<ArgumentException>(() => WorkshopModes.Select(course, "TimeRun", RaceFormat.Squad));
        }
    }
}
