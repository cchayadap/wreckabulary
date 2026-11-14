using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Online.Tests
{
    public sealed class WordboundWorkshopPartyTests
    {
        const string Actor = "10000000-0000-4000-8000-000000000001", PartyId = "20000000-0000-4000-8000-000000000001";
        const string Seed = "0123456789abcdef", Code = "1234567890abcdef";
        string items, rules;
        WordboundClientContent authored;
        WordboundPartyCoordinator coordinator;
        Http http;
        RSA rsa;
        OnlineSettings settings;
        bool selected;
        string revision;
        sealed class Http : IOnlineHttpTransport {
            public Func<OnlineRequest, Task<OnlineResponse>> Handler;
            public readonly List<OnlineRequest> Requests = new List<OnlineRequest>();
            public Task<OnlineResponse> Send(OnlineRequest request, CancellationToken ct) { Requests.Add(request); return Handler(request); }
        }
        [OneTimeSetUp] public void Content()
        {
            string root = Environment.GetEnvironmentVariable("WORDBOUND_PROJECT_ROOT") ?? Directory.GetCurrentDirectory();
            items = File.ReadAllText(Path.Combine(root, "Assets/_Project/Data/Config/items.json"));
            rules = File.ReadAllText(Path.Combine(root, "Assets/_Project/Data/Config/rules.json"));
            var builtin = new WordboundClientContent(items, rules, "Freestyle", "Solo", Seed);
            var draft = builtin.Course.CopyDocument(); draft.Id = "private-authored-fixture"; draft.Title = "An exact user draft";
            var resolved = CourseResolver.Resolve(draft, builtin.Geometry,
                new CourseResolveRequest("fedcba9876543210", MatchStateValidation.Requirements(builtin.Mode)));
            Assert.IsTrue(resolved.Success);
            authored = WordboundClientContent.FromAuthored(CourseJson.ExportDocument(draft), CourseJson.ExportManifest(resolved.Manifest),
                builtin.Geometry, items, rules, "Freestyle", "Solo", resolved.Manifest.Seed);
        }
        [SetUp] public void Setup()
        {
            rsa = new RSACryptoServiceProvider(2048) { PersistKeyInCsp = false }; var key = rsa.ExportParameters(false);
            var verifier = new OnlineAdmissionVerifier("test", OnlineJson.Parse(OnlineJson.Encode(new Dictionary<string, object> {
                ["keys"] = new[] { new Dictionary<string, object> { ["kty"] = "RSA", ["kid"] = "test", ["use"] = "sig", ["alg"] = "RS256", ["n"] = OnlineJson.Base64Url(key.Modulus), ["e"] = OnlineJson.Base64Url(key.Exponent) } } })));
            http = new Http(); selected = false; revision = "1";
            coordinator = new WordboundPartyCoordinator(new OnlineCoordinatorClient(http), verifier, items, rules, "bundle-tests", clock: () => 1000);
            settings = coordinator.Settings("Freestyle", "Solo", false, "asia");
            http.Handler = request => Task.FromResult(Reply(request));
        }
        [TearDown] public void Cleanup() { coordinator.Dispose(); rsa.Dispose(); }
        static OnlineResponse Json(object value) => new OnlineResponse(200, OnlineJson.Encode(value));
        Dictionary<string, object> Party() => new Dictionary<string, object> {
            ["id"] = PartyId, ["revision"] = revision, ["leaderId"] = Actor, ["invite"] = Code, ["settings"] = settings.ToValue(),
            ["members"] = new[] { new Dictionary<string, object> { ["playerId"] = Actor, ["ready"] = false } }, ["state"] = "OPEN",
            ["workshop"] = selected ? authored.WorkshopBundle.Descriptor.ToValue() : null };
        OnlineResponse Reply(OnlineRequest request)
        {
            if (request.Path == "/v1/guests") return Json(new Dictionary<string, object> { ["playerId"] = Actor, ["accessToken"] = "test.token", ["refreshToken"] = Actor + "." + OnlineJson.Base64Url(new byte[32]), ["expiresIn"] = 900 });
            if (request.Path.EndsWith("content-info")) return selected ?
                Json(new Dictionary<string, object> { ["descriptor"] = authored.WorkshopBundle.Descriptor.ToValue() }) :
                new OnlineResponse(200, "{\"descriptor\":null}");
            if (request.Path.EndsWith("content-begin")) return Json(new Dictionary<string, object> { ["descriptor"] = authored.WorkshopBundle.Descriptor.ToValue(), ["committed"] = selected });
            if (request.Path.EndsWith("content-chunk")) {
                var body = OnlineJson.Parse(request.JsonBody);
                return Json(new Dictionary<string, object> { ["bundleId"] = authored.WorkshopBundle.Descriptor.BundleId, ["index"] = (int)body["index"].Number() });
            }
            if (request.Path.EndsWith("content-finalize")) { selected = true; revision = "2"; return Json(Party()); }
            if (request.Path.EndsWith("content-read")) {
                int index = (int)OnlineJson.Parse(request.JsonBody)["index"].Number();
                return Json(new Dictionary<string, object> { ["bundleId"] = authored.WorkshopBundle.Descriptor.BundleId, ["index"] = index, ["data"] = OnlineJson.Base64Url(authored.WorkshopBundle.Packet.Chunk(index)) });
            }
            return Json(Party());
        }
        [Test] public async Task AuthorSharesBeforeReadyAndNeverUsesBuiltinReconstruction()
        {
            await coordinator.Connect("Author"); await coordinator.CreateAuthoredParty(settings, authored);
            Assert.IsNull(coordinator.Error); Assert.AreEqual("2", coordinator.Party.Revision);
            Assert.AreEqual(authored.Course.Hash, coordinator.Content.Course.Hash); Assert.IsFalse(coordinator.PublicAvailable);
            Assert.IsFalse(http.Requests.Any(r => r.Path.EndsWith("/ready") || r.Path.EndsWith("/queue")));
            await coordinator.SetReady(true);
            Assert.IsTrue(http.Requests.Last().Path.EndsWith("/ready"));
        }
        [TestCase("TimeRun", "Solo"), TestCase("TeamRace", "Solo"), TestCase("TeamRace", "Duo"), TestCase("TeamRace", "Squad")]
        [TestCase("BrawlRace", "Solo"), TestCase("BrawlRace", "Duo"), TestCase("BrawlRace", "Squad")]
        [TestCase("FreeForAll", "Solo"), TestCase("Freestyle", "Solo")]
        public void EverySupportedModeAndFormatPreservesAnActualAuthoredPreview(string mode, string format)
        {
            var builtin = new WordboundClientContent(items, rules, mode, format, Seed);
            var draft = builtin.Course.CopyDocument(); draft.Id = "shared-" + mode.ToLowerInvariant() + "-" + format.ToLowerInvariant();
            var resolved = CourseResolver.Resolve(draft, builtin.Geometry,
                new CourseResolveRequest("fedcba9876543210", MatchStateValidation.Requirements(builtin.Mode)));
            Assert.IsTrue(resolved.Success);
            var source = WordboundClientContent.FromAuthored(CourseJson.ExportDocument(draft), CourseJson.ExportManifest(resolved.Manifest),
                builtin.Geometry, items, rules, mode, format, resolved.Manifest.Seed);
            var packet = source.WorkshopBundle;
            var restored = OnlineWorkshopBundle.Restore(packet.Descriptor,
                Enumerable.Range(0, packet.Packet.ChunkCount).Select(packet.Packet.Chunk).ToArray(), builtin.Geometry, items, rules);
            var remote = WordboundClientContent.FromDownloaded(restored, builtin.Geometry, items, rules);
            Assert.AreEqual(source.Course.Hash, remote.Course.Hash); Assert.AreEqual(source.Seed, remote.Seed);
            Assert.AreEqual(source.Course.SourceHash, remote.Course.SourceHash); Assert.IsTrue(remote.Matches(source.PrivateBinding));
            if (mode == "FreeForAll") Assert.IsEmpty(remote.PhysicalPuzzles.Plan.Sites);
            Assert.IsFalse(new WordboundPublicCourseRegistry().Allows(remote));
        }
        [Test] public async Task PartyInviteDownloadsExactAuthoredSeedAndMapBeforeAllowingReady()
        {
            await coordinator.Connect("Guest"); selected = true; revision = "2";
            await coordinator.JoinParty(WordboundPartyInvite.Encode(PartyId, Code, Seed));
            Assert.IsNull(coordinator.Error); Assert.AreEqual(authored.Seed, coordinator.Seed);
            Assert.AreEqual(authored.Course.Hash, coordinator.Content.Course.Hash);
            Assert.AreEqual(authored.Course.SourceHash, coordinator.Content.Course.SourceHash);
            Assert.IsTrue(http.Requests.Any(r => r.Path.EndsWith("content-read")));
            Assert.IsNull(coordinator.Content.WorkshopBundle.Packet);
            Assert.IsFalse(coordinator.PublicAvailable);
            await coordinator.SetReady(true); Assert.IsNull(coordinator.Error);
        }
        [Test] public async Task ChangingAuthoredModeRequiresANewValidatedPreviewBeforeAnyRequest()
        {
            await coordinator.Connect("Author"); await coordinator.CreateAuthoredParty(settings, authored);
            int requests = http.Requests.Count;
            Assert.Throws<InvalidOperationException>(() => coordinator.ChangeSettings(coordinator.Settings("TimeRun", "Solo", false, "asia")));
            Assert.AreEqual(requests, http.Requests.Count);
            Assert.AreEqual(authored.Course.Hash, coordinator.Content.Course.Hash);
        }
        [Test] public void UnknownCustomCatalogueIsRejectedBeforeCreatingASharingBundle()
        {
            var geometry = authored.Geometry;
            var custom = new CourseCatalogue(geometry.Modules.Values.Concat(new[] { new CourseModuleDefinition("UninstalledModule", 2000, 2000, 2000) }),
                geometry.Assets.Values, geometry.PuzzlePresets, geometry.Themes, geometry.NarrativeEvents);
            var draft = authored.Course.CopyDocument();
            var resolved = CourseResolver.Resolve(draft, custom, new CourseResolveRequest(authored.Seed, MatchStateValidation.Requirements(authored.Mode)));
            Assert.IsTrue(resolved.Success);
            var error = Assert.Throws<InvalidOperationException>(() => WordboundClientContent.FromAuthored(
                CourseJson.ExportDocument(draft), CourseJson.ExportManifest(resolved.Manifest), custom, items, rules, "Freestyle", "Solo", authored.Seed));
            StringAssert.Contains("custom assets are not transferred", error.Message);
            Assert.IsNull(coordinator.Party); Assert.IsEmpty(http.Requests);
        }
        [TestCase(true), TestCase(false)]
        public void CustomItemOrRuleSourcesAreRejectedBeforeCreatingAParty(bool changedItems)
        {
            var custom = WordboundClientContent.FromAuthored(authored.WorkshopBundle.DocumentJson, authored.WorkshopBundle.ManifestJson,
                authored.Geometry, changedItems ? items + "\n" : items, changedItems ? rules : rules + "\n", "Freestyle", "Solo", authored.Seed);
            var error = Assert.Throws<InvalidOperationException>(() => coordinator.CreateAuthoredParty(settings, custom));
            StringAssert.Contains("installed item and rule files", error.Message);
            Assert.IsNull(coordinator.Party); Assert.IsEmpty(http.Requests);
        }
        [Test] public async Task FailedUploadKeepsAuthoredIdentityAndCannotReadyBuiltinFallback()
        {
            await coordinator.Connect("Author");
            http.Handler = request => request.Path.EndsWith("content-chunk") ?
                Task.FromException<OnlineResponse>(new IOException("lost upload")) : Task.FromResult(Reply(request));
            await coordinator.CreateAuthoredParty(settings, authored);
            Assert.IsTrue(coordinator.RetryRequired); Assert.AreEqual(authored.Course.Hash, coordinator.Content.Course.Hash);
            Assert.Throws<InvalidOperationException>(() => coordinator.SetReady(true));
            Assert.IsFalse(http.Requests.Any(r => r.Path.EndsWith("/ready")));
            http.Handler = request => Task.FromResult(Reply(request)); await coordinator.Retry();
            Assert.IsNull(coordinator.Error); Assert.AreEqual("2", coordinator.Party.Revision);
        }
        [Test] public void ExactAuthoredSourceRejectsDifferentPreviewOrSeed()
        {
            Assert.Throws<ArgumentException>(() => WordboundClientContent.FromAuthored(authored.WorkshopBundle.DocumentJson,
                authored.WorkshopBundle.ManifestJson, authored.Geometry, items, rules, "Freestyle", "Solo", Seed));
            var changed = authored.Course.CopyDocument(); changed.Title += " unsaved";
            Assert.Throws<ArgumentException>(() => WordboundClientContent.FromAuthored(CourseJson.ExportDocument(changed),
                authored.WorkshopBundle.ManifestJson, authored.Geometry, items, rules, "Freestyle", "Solo", authored.Seed));
        }
    }
}
