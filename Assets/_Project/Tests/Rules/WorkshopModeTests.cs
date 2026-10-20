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
