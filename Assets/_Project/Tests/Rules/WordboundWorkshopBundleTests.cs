using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Tests
{
    public sealed class WordboundWorkshopBundleTests
    {
        static readonly string Hash = new string('a', 64);
        static OnlineWorkshopBundle Create(out CourseCatalogue geometry)
        {
            geometry = CourseTemplates.ProofCatalogue();
            var mode = ModeDefinition.For("Freestyle"); var doc = CourseTemplates.FreestyleWorld();
            doc.Id = "authored-not-a-built-in";
            var result = CourseResolver.Resolve(doc, geometry, new CourseResolveRequest("1234567890abcdef", MatchStateValidation.Requirements(mode)));
            Assert.IsTrue(result.Success, string.Join(";", result.Issues));
            var course = result.Manifest;
            return OnlineWorkshopBundle.Create(doc, course, geometry, mode,
                new OnlineContent("Workshop", doc.Id, course.SourceHash, course.Hash, mode.Hash, Hash), "actual-items", "actual-rules");
        }
        [Test] public void ExactAuthoredDocumentAndResolvedManifestRoundTripWithoutResolution()
        {
            var original = Create(out var geometry);
            var copy = OnlineWorkshopBundle.Restore(original.Descriptor,
                Enumerable.Range(0, original.Packet.ChunkCount).Select(original.Packet.Chunk).ToArray(), geometry, "actual-items", "actual-rules");
            Assert.AreEqual(original.Descriptor.BundleId, copy.Descriptor.BundleId);
            Assert.AreEqual(original.DocumentJson, copy.DocumentJson);
            Assert.AreEqual(original.ManifestJson, copy.ManifestJson);
            Assert.IsNull(copy.Packet, "Downloaded bytes must not be silently recompressed into a new upload identity.");
        }
        [Test] public void DescriptorCommitsEveryBoundAndMetadataField()
        {
            var original = Create(out _);
            foreach (string field in new[] { "seed", "geometryHash", "itemHash", "rulesHash", "wireHash", "mode" }) {
                var value = original.Descriptor.ToValue(); value[field] = field == "seed" ? "abcdef0123456789" : field == "mode" ? "TimeRun" : new string('b', 64);
                Assert.Throws<FormatException>(() => new OnlineWorkshopDescriptor(OnlineJson.Parse(OnlineJson.Encode(value))), field);
            }
        }
        [TestCase("changed-items", "actual-rules"), TestCase("actual-items", "changed-rules")]
        public void InstalledContentPinsAreCheckedBeforeInterpretingMap(string items, string rules)
        {
            var bundle = Create(out var geometry);
            Assert.Throws<FormatException>(() => OnlineWorkshopBundle.Restore(bundle.Descriptor, null, geometry, items, rules));
        }
        [Test] public void MissingMixedOrCorruptChunksFailClosed()
        {
            var bundle = Create(out var geometry);
            var chunks = Enumerable.Range(0, bundle.Packet.ChunkCount).Select(bundle.Packet.Chunk).ToArray();
            Assert.Throws<FormatException>(() => OnlineWorkshopBundle.Restore(bundle.Descriptor, new byte[0][], geometry, "actual-items", "actual-rules"));
            chunks[0][chunks[0].Length - 1] ^= 1;
            Assert.Throws<FormatException>(() => OnlineWorkshopBundle.Restore(bundle.Descriptor, chunks, geometry, "actual-items", "actual-rules"));
        }
        [Test] public void PartySettingsCannotSubstituteAnotherModeOrProtocol()
        {
            var bundle = Create(out _);
            Assert.DoesNotThrow(() => bundle.Descriptor.Bind(new OnlineSettings("Freestyle", "Solo", true, "asia", MatchSnapshotCodec.ProtocolVersion, "test")));
            Assert.Throws<FormatException>(() => bundle.Descriptor.Bind(new OnlineSettings("TimeRun", "Solo", true, "asia", MatchSnapshotCodec.ProtocolVersion, "test")));
        }
        sealed class Http : IOnlineHttpTransport
        {
            public Func<OnlineRequest, Task<OnlineResponse>> Reply;
            public Task<OnlineResponse> Send(OnlineRequest request, CancellationToken ct) => Reply(request);
        }
        [Test] public async Task LostFinalizeResponseResolvesSelectedIdentityWithoutReupload()
        {
            var bundle = Create(out _); const string id = "11111111-1111-4111-a111-111111111111";
            int begin = 0, chunks = 0; bool committed = false;
            var http = new Http();
            http.Reply = request => {
                object value;
                if (request.Path == "/v1/guests") value = new Dictionary<string, object> { ["playerId"] = id, ["accessToken"] = "test.token", ["refreshToken"] = id + "." + OnlineJson.Base64Url(new byte[32]), ["expiresIn"] = 900 };
                else if (request.Path.EndsWith("content-info")) {
                    if (!committed) return Task.FromResult(new OnlineResponse(200, "{\"descriptor\":null}"));
                    value = new Dictionary<string, object> { ["descriptor"] = bundle.Descriptor.ToValue() };
                }
                else if (request.Path.EndsWith("content-begin")) { begin++; value = new Dictionary<string, object> { ["descriptor"] = bundle.Descriptor.ToValue(), ["committed"] = false }; }
                else if (request.Path.EndsWith("content-chunk")) { chunks++; var part = OnlineJson.Parse(request.JsonBody); value = new Dictionary<string, object> { ["bundleId"] = bundle.Descriptor.BundleId, ["index"] = (int)part["index"].Number() }; }
                else if (request.Path.EndsWith("content-finalize")) { committed = true; return Task.FromException<OnlineResponse>(new System.IO.IOException("lost response")); }
                else value = new Dictionary<string, object> { ["id"] = id, ["revision"] = "2" };
                return Task.FromResult(new OnlineResponse(200, OnlineJson.Encode(value)));
            };
            var client = new OnlineCoordinatorClient(http); await client.CreateGuest("Test").ConfigureAwait(false);
            var service = new OnlineWorkshopService(client);
            Assert.ThrowsAsync<System.IO.IOException>(async () => await service.Upload(id, "1", bundle).ConfigureAwait(false));
            var result = await service.Upload(id, "1", bundle).ConfigureAwait(false);
            Assert.AreEqual("2", result["revision"].String()); Assert.AreEqual(1, begin); Assert.AreEqual(bundle.Packet.ChunkCount, chunks);
        }
    }
}
