using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Tests
{
    public class CourseCoreTests
    {
        internal static CoursePlayRequirements Play(string mode, int capacity = 4, int occupants = 8)
        {
            if (mode == "TimeRun") return new CoursePlayRequirements(mode, CourseCapability.Circuit, 1, 4, 4, CourseOwnershipPolicy.TeamIsolated);
            if (mode == "FreeForAll") return new CoursePlayRequirements(mode, CourseCapability.Arena, 0, 1, occupants, CourseOwnershipPolicy.Individual);
            if (mode == "Freestyle") return new CoursePlayRequirements(mode, CourseCapability.Explore, 0, 0, occupants, CourseOwnershipPolicy.Shared);
            return new CoursePlayRequirements(mode, CourseCapability.Circuit, 2, capacity, capacity * 2,
                mode == "TeamRace" ? CourseOwnershipPolicy.TeamIsolated : CourseOwnershipPolicy.TeamProtected);
        }
        internal static CourseDocument Template(string mode) => mode == "FreeForAll" ? CourseTemplates.CombatArena() :
            mode == "Freestyle" ? CourseTemplates.FreestyleWorld() : CourseTemplates.RaceCircuit();
        internal static string Errors(IEnumerable<CourseIssue> issues) => string.Join("\n", issues.Select(i => i.ToString()));

        [TestCase("TimeRun"), TestCase("TeamRace"), TestCase("BrawlRace"), TestCase("FreeForAll"), TestCase("Freestyle")]
        public void ProofTemplatesRoundTripAndResolveForEachMode(string mode)
        {
            var d = Template(mode); var cat = CourseTemplates.ProofCatalogue(); var play = Play(mode);
            var valid = CourseValidator.Validate(d, cat, play);
            Assert.IsTrue(valid.Ok, Errors(valid.Issues));
            string json = CourseJson.ExportDocument(d);
            var imported = CourseJson.ImportDocument(json);
            Assert.IsTrue(imported.Success, Errors(imported.Issues));
            Assert.AreEqual(json, CourseJson.ExportDocument(imported.Document));
            var result = CourseResolver.Resolve(d, cat, new CourseResolveRequest("0123456789abcdef", play));
            Assert.IsTrue(result.Success, Errors(result.Issues));
            string manifest = CourseJson.ExportManifest(result.Manifest);
            var restored = CourseJson.ImportManifest(manifest, cat);
            Assert.IsTrue(restored.Success, Errors(restored.Issues));
            Assert.AreEqual(manifest, CourseJson.ExportManifest(restored.Manifest));
            var check = CourseValidator.ValidateResolved(restored.Manifest, cat, play);
            Assert.IsTrue(check.Ok, Errors(check.Issues));
        }

        [Test]
        public void ThreeLapsRetainStaticUnionAndAlternateBranches()
        {
            var d = CourseTemplates.RaceCircuit();
            var result = CourseResolver.Resolve(d, CourseTemplates.ProofCatalogue(), new CourseResolveRequest("0000000000000001", Play("TeamRace")));
            Assert.IsTrue(result.Success, Errors(result.Issues));
            var m = result.Manifest;
            Assert.AreEqual(3, m.Laps.Count);
            Assert.AreEqual(d.Modules.Count, m.Geometry.Count);
            Assert.AreEqual(d.Connections.Count, m.Connections.Count);
            Assert.AreNotEqual(m.Laps[0].AlternativeIds[0], m.Laps[1].AlternativeIds[0]);
            Assert.AreNotEqual(m.Laps[1].AlternativeIds[0], m.Laps[2].AlternativeIds[0]);
            string encoded = CourseJson.ExportManifest(m);
            d.Modules.Clear();
            m.CopyDocument().Modules.Clear();
            Assert.AreEqual(encoded, CourseJson.ExportManifest(m));
            Assert.Throws<NotSupportedException>(() => ((IList<ResolvedCourseModule>)m.Geometry).Clear());
        }
    }
}
