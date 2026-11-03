using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Rules.Tests
{
    public sealed class WordboundPhysicalRecipeTests
    {
        const string Seed = "0123456789abcdef";
        static readonly string[] Seeds = { Seed, "0000000000000001", "fedcba9876543210" };
        static CoursePlayRequirements Play(string mode, RaceFormat format = RaceFormat.Solo) =>
            MatchStateValidation.Requirements(ModeDefinition.For(mode, format));
        static ItemCatalogue KitchenItems() => ItemCatalogue.FromJson(File.ReadAllText(
            Path.Combine(TestData.Root, "Assets/_Project/Data/Wordbound/Authoring/FlagshipKitchenV1/Items.json")));

        static (CourseDocument Document, CourseCatalogue Geometry) Course(string name)
        {
            switch (name)
            {
                case "RaceCircuit": return (CourseTemplates.RaceCircuit(), CourseTemplates.ProofCatalogue());
                case "CombatArena": return (CourseTemplates.CombatArena(), CourseTemplates.ProofCatalogue());
                case "FreestyleWorld": return (CourseTemplates.FreestyleWorld(), CourseTemplates.ProofCatalogue());
                case "Flagship": { var c = WordboundFlagshipContent.Create(TestData.Catalogue()); return (c.CopyDocument(), c.Geometry); }
                case "KitchenPilot": { var c = WordboundKitchenPilotContent.Create(KitchenItems()); return (c.CopyDocument(), c.Geometry); }
                case "ParcelService": { var c = WordboundParcelServiceContent.Create(TestData.Catalogue()); return (c.CopyDocument(), c.Geometry); }
                default:
                    var template = WorkshopTemplates.Create((WorkshopTemplate)Enum.Parse(typeof(WorkshopTemplate), name.Substring("Workshop".Length)));
                    return (template.Snapshot.Course(), template.Catalogue);
            }
        }
        static IEnumerable<RaceFormat> Formats(string mode) =>
            mode == "TeamRace" || mode == "BrawlRace" ? new[] { RaceFormat.Solo, RaceFormat.Duo, RaceFormat.Squad } : new[] { RaceFormat.Solo };
        static CourseDocument Copy(CourseDocument document) => CourseJson.ImportDocument(CourseJson.ExportDocument(document)).Document;

        [Test]
        public void SiteRecipesAreTheModeRecipeList()
        {
            foreach (string recipe in ModeDefinition.For("TimeRun").Recipes) Assert.IsTrue(CourseValidator.SiteRecipeAllowed(recipe), recipe);
            foreach (string recipe in new[] { "BELT", "TOASTER", "BLADE", "hammer", "HAMMERS", "", null })
                Assert.IsFalse(CourseValidator.SiteRecipeAllowed(recipe), recipe ?? "null");
        }

        [TestCase("RaceCircuit"), TestCase("CombatArena"), TestCase("FreestyleWorld"), TestCase("Flagship"), TestCase("KitchenPilot")]
        [TestCase("ParcelService"), TestCase("WorkshopRaceCircuit"), TestCase("WorkshopCombatArena"), TestCase("WorkshopFreestyleWorld")]
        public void EveryBuiltInCourseStaysValid(string name)
        {
            var (document, geometry) = Course(name);
            var structure = CourseValidator.ValidateStructure(document);
            Assert.IsTrue(structure.Ok, string.Join("\n", structure.Issues));
            foreach (var site in document.PhysicalSites)
            {
                if (site.ToolRecipeId != "") Assert.IsTrue(CourseValidator.SiteRecipeAllowed(site.ToolRecipeId), site.PuzzleId);
                if (site.WindReceiverRecipeId != "") Assert.IsTrue(CourseValidator.SiteRecipeAllowed(site.WindReceiverRecipeId), site.PuzzleId);
            }
            int resolved = 0;
            foreach (string mode in document.SupportedModes)
                foreach (var format in Formats(mode))
                    foreach (string seed in Seeds)
                    {
                        var result = CourseResolver.Resolve(Copy(document), geometry, new CourseResolveRequest(seed, Play(mode, format)));
                        Assert.IsFalse(result.Issues.Any(issue => issue.Code == "physical.recipe"), name + " " + mode + " " + format);
                        if (!result.Success) continue;
                        resolved++;
                        var imported = CourseJson.ImportManifest(CourseJson.ExportManifest(result.Manifest), geometry);
                        Assert.IsTrue(imported.Success, string.Join("\n", imported.Issues));
                        break;
                    }
            Assert.Greater(resolved, 0, name + " must resolve for at least one supported mode.");
        }

        [Test]
        public void FlagshipExercisesBothSiteRecipeKinds()
        {
            var document = Course("Flagship").Document;
            Assert.IsTrue(document.PhysicalSites.Any(site => site.ToolRecipeId == "HAMMER"));
            Assert.IsTrue(document.PhysicalSites.Any(site => site.WindReceiverRecipeId == "STOOL"));
        }

        [Test]
        public void AuthoredCourseAssetsOnlyNameCraftableSiteRecipes()
        {
            var pattern = new Regex(@"(toolRecipeId|windReceiverRecipeId)\\?"":\\?""([A-Za-z0-9_-]*)");
            int named = 0, files = 0;
            foreach (string folder in new[] { "Assets/_Project/Data/Wordbound", "Assets/_Project/Resources/Wordbound" })
                foreach (string file in Directory.GetFiles(Path.Combine(TestData.Root, folder), "*.asset", SearchOption.AllDirectories))
                {
                    var matches = pattern.Matches(File.ReadAllText(file));
                    if (matches.Count > 0) files++;
                    foreach (Match match in matches)
                    {
                        string recipe = match.Groups[2].Value;
                        if (recipe.Length == 0) continue;
                        named++;
                        Assert.IsTrue(CourseValidator.SiteRecipeAllowed(recipe), Path.GetFileName(file) + ": " + recipe);
                    }
                }
            Assert.Greater(files, 0); Assert.Greater(named, 0);
        }

        static string Tampered(string json, bool tool, string recipe)
        {
            string field = tool ? "toolRecipeId" : "windReceiverRecipeId", original = tool ? "HAMMER" : "STOOL";
            string from = "\"" + field + "\":\"" + original + "\"";
            StringAssert.Contains(from, json);
            return json.Replace(from, "\"" + field + "\":\"" + recipe + "\"");
        }

        [TestCase("BELT", true), TestCase("TOASTER", true), TestCase("BLADE", true), TestCase("LAMP", true)]
        [TestCase("BELT", false), TestCase("TOASTER", false), TestCase("table", false), TestCase("stool", false)]
        public void DraftsWithUncraftableSiteRecipesOpenButCannotBePlayedHostedOrShared(string recipe, bool tool)
        {
            var (document, geometry) = Course("Flagship");
            string source = Tampered(CourseJson.ExportDocument(document), tool, recipe);
            var imported = CourseJson.ImportDocument(source);
            Assert.IsTrue(imported.Success, "Drafts saved by earlier builds must still open: " + string.Join("\n", imported.Issues));
            Assert.IsTrue(CourseValidator.ValidateStructure(imported.Document).Ok);
            var opened = WorkshopShare.Import(source, null);
            Assert.IsFalse(opened.HasPlaySettings);
            Assert.IsTrue(opened.Snapshot.Course().PhysicalSites.Any(s => (tool ? s.ToolRecipeId : s.WindReceiverRecipeId) == recipe));
            var checks = CourseValidator.Validate(imported.Document, geometry, Play("TimeRun"));
            Assert.IsFalse(checks.Ok);
            Assert.IsTrue(checks.Issues.Any(issue => issue.Code == "physical.recipe" && issue.Message.Length > 0), string.Join("\n", checks.Issues));
            var resolved = CourseResolver.Resolve(imported.Document, geometry, new CourseResolveRequest(Seed, Play("TimeRun")));
            Assert.IsFalse(resolved.Success);
            Assert.IsTrue(resolved.Issues.Any(issue => issue.Code == "physical.recipe"), string.Join("\n", resolved.Issues));
            var direct = Course("Flagship").Document;
            var edited = direct.PhysicalSites.First(s => tool ? s.ToolRecipeId == "HAMMER" : s.WindReceiverRecipeId == "STOOL");
            if (tool) edited.ToolRecipeId = recipe; else edited.WindReceiverRecipeId = recipe;
            Assert.IsTrue(CourseValidator.Validate(direct, geometry, Play("TimeRun")).Issues.Any(issue => issue.Code == "physical.recipe"));
            string shared = OnlineJson.Encode(new Dictionary<string, object> {
                ["format"] = WorkshopShare.Format, ["schema"] = WorkshopShare.Schema, ["course"] = source, ["seed"] = Seed,
                ["mode"] = "TimeRun", ["raceFormat"] = "Solo", ["catalogueHash"] = geometry.Hash, ["modeHash"] = ModeDefinition.For("TimeRun").Hash });
            Assert.Throws<FormatException>(() => WorkshopShare.Import(shared, geometry));
        }

        [TestCase("BELT", true), TestCase("LAMP", true), TestCase("TOASTER", false), TestCase("stool", false)]
        public void SharedBundleRestoreRejectsTamperedSiteRecipes(string recipe, bool tool)
        {
            var (document, geometry) = Course("Flagship");
            var mode = ModeDefinition.For("TimeRun");
            var resolved = CourseResolver.Resolve(document, geometry, new CourseResolveRequest(Seed, Play("TimeRun")));
            Assert.IsTrue(resolved.Success, string.Join("\n", resolved.Issues));
            var course = resolved.Manifest;
            var bundle = OnlineWorkshopBundle.Create(document, course, geometry, mode,
                new OnlineContent("Workshop", document.Id, course.SourceHash, course.Hash, mode.Hash, new string('a', 64)), "actual-items", "actual-rules");
            var chunks = Enumerable.Range(0, bundle.Packet.ChunkCount).Select(bundle.Packet.Chunk).ToArray();
            Assert.DoesNotThrow(() => OnlineWorkshopBundle.Restore(bundle.Descriptor, chunks, geometry, "actual-items", "actual-rules"));
            string json = Tampered(OnlineSnapshotPacket.Restore(chunks, bundle.Packet.Length, bundle.Packet.WireHash), tool, recipe);
            var packet = OnlineSnapshotPacket.Compress(json);
            var value = bundle.Descriptor.ToValue(); value.Remove("bundleId");
            value["wireHash"] = packet.WireHash; value["rawLength"] = packet.RawLength;
            value["totalBytes"] = packet.Length; value["chunkCount"] = packet.ChunkCount;
            value["bundleId"] = OnlineJson.Hash(value);
            var descriptor = new OnlineWorkshopDescriptor(OnlineJson.Parse(OnlineJson.Encode(value)));
            var tampered = Enumerable.Range(0, packet.ChunkCount).Select(packet.Chunk).ToArray();
            Assert.Throws<FormatException>(() => OnlineWorkshopBundle.Restore(descriptor, tampered, geometry, "actual-items", "actual-rules"));
            var manifest = CourseJson.ImportManifest(Tampered(CourseJson.ExportManifest(course), tool, recipe), geometry);
            Assert.IsFalse(manifest.Success);
            Assert.IsTrue(manifest.Issues.Any(issue => issue.Code == "physical.recipe"), string.Join("\n", manifest.Issues));
        }
    }
}
