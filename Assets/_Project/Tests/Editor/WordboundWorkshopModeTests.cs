using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;
using Wreckabulary.Workshop;

namespace Wreckabulary.EditorTests
{
    public sealed class WordboundWorkshopModeTests
    {
        GameObject owner;
        WordboundWorkshop workshop;
        string folder;

        [SetUp]
        public void SetUp()
        {
            owner = new GameObject("Workshop mode test");
            workshop = owner.AddComponent<WordboundWorkshop>();
            folder = Path.Combine(Path.GetTempPath(), "wordbound-modes-" + Guid.NewGuid().ToString("N"));
            workshop.ConfigureLocalPlaytest(true);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(owner);
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }

        [Test]
        public void LocalRacePracticeDispatchesOnlyTheExplicitTimeRunManifestAndDraft()
        {
            workshop.OpenTemplate(WorkshopTemplate.RaceCircuit, folder);
            ResolvedCourse received = null;
            CoursePlayRequirements capturedPlay = null;
            WorkshopSnapshot capturedDraft = null;
            workshop.PlaytestRequested += manifest =>
            {
                received = manifest; capturedPlay = workshop.PlayRequirements; capturedDraft = workshop.LastPlaytestDraft;
                workshop.Visible = false;
            };
            workshop.RequestPlaytest();
            Assert.IsNull(received); Assert.IsFalse(workshop.CanPlaytestLocally);
            workshop.SelectMode("TimeRun");
            Assert.IsTrue(workshop.Preview().Success);
            workshop.RequestPlaytest();
            Assert.IsNull(received, "Geometry-only race templates cannot dispatch missing physical controls.");
            Assert.IsNull(workshop.LastPlaytestDraft);
            var items = ItemCatalogue.FromJson(File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Data/Config/items.json")));
            workshop.OpenFlagship(WordboundFlagshipContent.Create(items), folder);
            string draft = workshop.History.Current.RecoveryJson;
            var preview = workshop.Preview();
            Assert.IsTrue(preview.Success, string.Join("\n", preview.Issues));
            workshop.RequestPlaytest();
            Assert.IsNotNull(received); Assert.AreEqual(preview.Manifest.Hash, received.Hash);
            Assert.AreSame(workshop.PlayRequirements, capturedPlay);
            Assert.AreEqual("TimeRun", capturedPlay.ModeId);
            Assert.AreEqual(draft, capturedDraft.RecoveryJson);
            workshop.ResumeEditing();
            Assert.IsTrue(workshop.Visible); Assert.AreEqual(draft, workshop.History.Current.RecoveryJson);
            Assert.AreSame(capturedPlay, workshop.PlayRequirements);
        }

        [Test]
        public void ArenaFreestyleMustBeEnabledBeforeLocalPlaytestAndUndoRevokesIt()
        {
            workshop.OpenTemplate(WorkshopTemplate.CombatArena, folder);
            int launches = 0;
            workshop.PlaytestRequested += _ => launches++;
            workshop.RequestPlaytest(); Assert.AreEqual(0, launches);
            Assert.Throws<ArgumentException>(() => workshop.SelectMode("Freestyle"));
            workshop.EnableFreestyleSandbox();
            Assert.IsTrue(workshop.CanPlaytestLocally);
            workshop.RequestPlaytest(); Assert.AreEqual(1, launches);
            Assert.AreEqual("Freestyle", workshop.PlayRequirements.ModeId);
            Assert.IsTrue(workshop.History.Undo());
            workshop.RequestPlaytest(); Assert.AreEqual(1, launches);
            Assert.IsNull(workshop.LastPlaytestDraft);
            Assert.IsTrue(workshop.History.Redo());
            workshop.RequestPlaytest(); Assert.AreEqual(2, launches);
        }

        [Test]
        public void InvalidModeSelectionPreservesTheCurrentPreviewAndRequirements()
        {
            workshop.OpenTemplate(WorkshopTemplate.RaceCircuit, folder);
            workshop.SelectMode("TimeRun");
            var requirements = workshop.PlayRequirements;
            var preview = workshop.Preview();
            Assert.Throws<ArgumentException>(() => workshop.SelectMode("FreeForAll"));
            Assert.AreSame(requirements, workshop.PlayRequirements);
            Assert.AreSame(preview, workshop.LastPreview);
        }

        [Test]
        public void OpeningAnotherHistoryWithMatchingSiteIdsCannotReuseUnsavedPhysicalFields()
        {
            var original = CourseTemplates.RaceCircuit(); original.Schema = 2;
            original.Puzzles.Single(p => p.Id == "BedBridge").PresetId = "PlaceObject";
            original.PhysicalSites.Add(new CoursePhysicalSite
            {
                Lap = 1, PuzzleId = "BedBridge", CheckpointId = "Begin", RecoverySupplyId = "BedSupply"
            });
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var load = typeof(WordboundWorkshop).GetMethod("LoadSiteEditor", flags);
            var edit = typeof(WordboundWorkshop).GetField("siteEdit", flags);
            var fields = typeof(WordboundWorkshop).GetField("siteFields", flags);
            var play = MatchStateValidation.Requirements(ModeDefinition.For("TimeRun"));
            workshop.Open(original, CourseTemplates.ProofCatalogue(), play, folder);
            var first = workshop.History;
            load.Invoke(workshop, new object[] { original.PhysicalSites[0] });
            ((CoursePhysicalSite)edit.GetValue(workshop)).Position = new CoursePoint(4000, 0, 0);
            var unsaved = (Dictionary<string, string>)fields.GetValue(workshop);
            unsaved["Socket X"] = "4";
            var replacement = original.Clone();
            replacement.PhysicalSites[0].Position = new CoursePoint(1000, 0, 0);
            workshop.Open(replacement, CourseTemplates.ProofCatalogue(), play, folder);
            Assert.AreNotSame(first, workshop.History);
            Assert.AreEqual(first.Revision, workshop.History.Revision);
            load.Invoke(workshop, new object[] { replacement.PhysicalSites[0] });
            Assert.AreEqual(new CoursePoint(1000, 0, 0), ((CoursePhysicalSite)edit.GetValue(workshop)).Position);
            Assert.IsEmpty(unsaved);
            Assert.AreEqual(replacement.PhysicalSites[0].Position, workshop.History.Current.Course().PhysicalSites[0].Position);
        }
    }
}
