using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Wreckabulary.Rules.Wordbound;
using Wreckabulary.Workshop;

namespace Wreckabulary.EditorTests
{
    public sealed class WordboundWorkshopRoutesTests
    {
        [Test]
        public void OpeningAnotherHistoryWithSameIdsAndRevisionDiscardsOldRouteInputs()
        {
            var owner = new GameObject("Workshop route history test");
            string folder = Path.Combine(Path.GetTempPath(), "wordbound-routes-" + Guid.NewGuid().ToString("N"));
            try
            {
                var workshop = owner.AddComponent<WordboundWorkshop>();
                var first = CourseTemplates.RaceCircuit();
                var play = MatchStateValidation.Requirements(ModeDefinition.For("TimeRun"));
                workshop.Open(first, CourseTemplates.ProofCatalogue(), play, folder);
                var oldHistory = workshop.History;
                Load(workshop, first.RecoveryAnchors.Single(r => r.Id == "StartSafe"));
                Field("routeX").SetValue(workshop, "987");
                ((List<string>)Field("routeAfter").GetValue(workshop)).Add("BedBridge");
                Field("routeBranchLoadRevision").SetValue(workshop, 0);
                Field("routeBranchLoadId").SetValue(workshop, "North");
                var second = CourseTemplates.RaceCircuit(); second.Id = "second-course";
                var anchor = second.RecoveryAnchors.Single(r => r.Id == "StartSafe");
                anchor.Position = new CoursePoint(-2000, 0, -7000); anchor.RadiusMm = 1500;
                workshop.Open(second, CourseTemplates.ProofCatalogue(), play, folder);
                Assert.AreNotSame(oldHistory, workshop.History);
                Assert.AreEqual(oldHistory.Revision, workshop.History.Revision);
                Load(workshop, anchor);
                Assert.AreEqual("-2", Field("routeX").GetValue(workshop));
                Assert.AreEqual("1.5", Field("routeRadius").GetValue(workshop));
                Assert.IsEmpty((List<string>)Field("routeAfter").GetValue(workshop));
                Assert.AreEqual(-1, Field("routeBranchLoadRevision").GetValue(workshop));
                Assert.AreEqual("", Field("routeBranchLoadId").GetValue(workshop));
                Assert.AreEqual(second.Id, workshop.History.Current.Course().Id);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
                string resolved = Path.GetFullPath(folder);
                string temporary = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (!resolved.StartsWith(temporary, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Unexpected test folder.");
                if (Directory.Exists(resolved)) Directory.Delete(resolved, true);
            }
        }

        static FieldInfo Field(string name) => typeof(WordboundWorkshop).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
        static void Load(WordboundWorkshop workshop, CourseRecovery value) =>
            typeof(WordboundWorkshop).GetMethod("LoadRecoveryRoute", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(workshop, new object[] { value });
    }
}
