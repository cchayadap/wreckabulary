using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using Wreckabulary.Online;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Tests
{
    public sealed class WordboundFusionAuthorityTests
    {
        const string Player = "11111111-1111-4111-a111-111111111111";
        const string Other = "22222222-2222-4222-a222-222222222222";
        const string MatchId = "33333333-3333-4333-a333-333333333333";
        sealed class World : IWordboundWorldQuery
        {
            public bool HasLineOfSight(string actor, WorldVector from, WorldVector to) => true;
            public bool CanDeploy(string actor, string recipe, WorldVector position) => true;
            public bool PuzzleSatisfied(string actor, ResolvedCoursePuzzle puzzle, WorldBodyState item) => true;
        }
        sealed class Fixture
        {
            public WordboundFusionAuthority Bridge;
            public MatchSnapshot State => Bridge.Match.Capture(1);
        }
        static string Config(string file)
        {
            foreach (string start in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
                for (var directory = new DirectoryInfo(start); directory != null; directory = directory.Parent)
                {
                    string path = Path.Combine(directory.FullName, "Assets", "_Project", "Data", "Config", file);
                    if (File.Exists(path)) return File.ReadAllText(path);
                }
            throw new DirectoryNotFoundException("The test requires the repository gameplay configuration.");
        }
        static Fixture Create()
        {
            var mode = ModeDefinition.For("TimeRun");
            var catalogue = ItemCatalogue.FromJson(Config("items.json"), "items.json");
            var rules = mode.CreateGameplayRules(RuleBook.FromJson(Config("rules.json"), "rules.json").For("Dibs"));
            var resolved = CourseResolver.Resolve(CourseTemplates.RaceCircuit(), CourseTemplates.ProofCatalogue(),
                new CourseResolveRequest("0123456789abcdef", MatchStateValidation.Requirements(mode)));
            Assert.IsTrue(resolved.Success);
            var course = resolved.Manifest;
            var settings = new OnlineSettings(mode.Id, "Solo", false, "asia", "wordbound-v1", "1");
            var content = new OnlineContent("BuiltIn", "test", course.SourceHash, course.Hash, mode.Hash, new string('b', 64));
            var roster = new[] { Player, Other }.Select((id, slot) => new Dictionary<string, object> {
                ["playerId"] = id, ["partyId"] = "44444444-4444-4444-a444-444444444444",
                ["partyRevision"] = "1", ["attemptId"] = "55555555-5555-4555-a555-555555555555",
                ["teamId"] = 0, ["seat"] = slot, ["connectionSecretHash"] = new string('c', 64) }).ToArray();
            string rosterHash = OnlineJson.Hash(roster);
            var expected = new OnlineMatchExpectation(MatchId, "1", rosterHash, settings, content);
            var claims = new Dictionary<string, object> {
                ["version"] = 1, ["iss"] = "bridge-test", ["aud"] = "wordbound-match-start", ["iat"] = 1000, ["exp"] = 1090,
                ["matchId"] = MatchId, ["reservationGeneration"] = "1", ["rosterHash"] = rosterHash,
                ["startedAt"] = 1000000, ["settings"] = settings.ToValue(), ["content"] = content.ToValue(), ["roster"] = roster };
            using var rsa = new RSACryptoServiceProvider(2048) { PersistKeyInCsp = false };
            var key = rsa.ExportParameters(false);
            Assert.AreEqual(256, key.Modulus.Length, "The fixture must actually generate a 2048-bit RSA key.");
            string jwks = OnlineJson.Encode(new Dictionary<string, object> { ["keys"] = new[] { new Dictionary<string, object> {
                ["kty"] = "RSA", ["kid"] = "test", ["alg"] = "RS256", ["use"] = "sig",
                ["n"] = OnlineJson.Base64Url(key.Modulus), ["e"] = OnlineJson.Base64Url(key.Exponent) } } });
            string input = OnlineJson.Base64Url(Encoding.UTF8.GetBytes("{\"alg\":\"RS256\",\"typ\":\"JWT\",\"kid\":\"test\"}")) +
                "." + OnlineJson.Base64Url(Encoding.UTF8.GetBytes(OnlineJson.Encode(claims)));
            string signed = input + "." + OnlineJson.Base64Url(rsa.SignData(Encoding.ASCII.GetBytes(input), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
            var online = new OnlineAdmissionVerifier("bridge-test", jwks).VerifyStart(signed, expected, 1000);
            var binding = new MatchBinding(MatchId, settings.ProtocolVersion, mode.Hash, course.SourceHash, course.Hash,
                rosterHash, content.CatalogHash, new[] { new WordboundSeat(Player, 0, 0), new WordboundSeat(Other, 0, 1) });
            var authority = new AuthoritativeMatch(binding, mode, course, CourseTemplates.ProofCatalogue(), catalogue, rules, new World());
            return new Fixture { Bridge = new WordboundFusionAuthority(authority, online, course, catalogue, rules) };
        }
        static JsonNode Command(long sequence = 1, long epoch = 0) => OnlineFusionWire.Decode(OnlineFusionWire.Encode(
            OnlineWireKind.Command, new Dictionary<string, object> {
                ["sequence"] = sequence.ToString(), ["epoch"] = epoch.ToString(), ["action"] = "CancelCraft",
                ["target"] = "", ["revision"] = 0, ["slot"] = -1, ["value"] = "" })).Body;

        [Test] public void OnlyReadyHostAcceptsCommandsAndAdvances()
        {
            var f = Create();
            Assert.Throws<InvalidOperationException>(() => f.Bridge.AcceptCommand(Player, Command()));
            Assert.Throws<InvalidOperationException>(() => f.Bridge.Advance(1));
            f.Bridge.SetTransportReady(true, false);
            Assert.Throws<InvalidOperationException>(() => f.Bridge.AcceptCommand(Player, Command()));
            Assert.Throws<InvalidOperationException>(() => f.Bridge.Advance(1));
            f.Bridge.SetTransportReady(true, true);
            Assert.Throws<InvalidOperationException>(() => f.Bridge.AcceptCommand("outsider", Command()));
            f.Bridge.AcceptCommand(Player, Command());
            Assert.AreEqual(Player, f.Bridge.Advance(1).Receipts.Single().ActorId);
        }
        [Test] public void DurabilityPauseRetainsCommandsUntilTheNextSuccessfulTick()
        {
            var f = Create(); f.Bridge.SetTransportReady(true, true);
            for (int tick = 1; tick <= 120; tick++) Assert.IsTrue(f.Bridge.Advance(tick * 17).Advanced);
            f.Bridge.AcceptCommand(Player, Command());
            Assert.IsFalse(f.Bridge.Advance(121 * 17).Advanced);
            var mark = f.Bridge.Publish(out string json);
            f.Bridge.ValidateSnapshot(json, mark);
            Assert.IsTrue(f.Bridge.AcknowledgeDurable(mark));
            Assert.AreEqual(1, f.Bridge.Advance(121 * 17).Receipts.Count);
            Assert.AreEqual(0, f.Bridge.Advance(122 * 17).Receipts.Count);
        }
        [Test] public void FailedHostFrameRetainsTheExactCommandForRetry()
        {
            var f = Create(); f.Bridge.SetTransportReady(true, true);
            var body = f.State.Bodies.First(); f.Bridge.AcceptCommand(Player, Command());
            Assert.Throws<ArgumentException>(() => f.Bridge.Advance(17, new[] { body, body }));
            var receipt = f.Bridge.Advance(17).Receipts.Single();
            Assert.AreEqual(Player, receipt.ActorId);
            Assert.AreEqual(1L, receipt.Sequence);
            Assert.AreEqual(0, f.Bridge.Advance(34).Receipts.Count);
        }
        [Test] public void RestoreUsesExactCommittedStateAndPreservesDisconnectedDeadline()
        {
            var f = Create(); f.Bridge.SetTransportReady(true, true);
            f.Bridge.Disconnect(Other, 100);
            var mark = f.Bridge.Publish(out string json); Assert.IsTrue(f.Bridge.AcknowledgeDurable(mark));
            f.Bridge.AcceptCommand(Player, Command()); f.Bridge.Advance(117);
            f.Bridge.PauseForMigration();
            f.Bridge.RestoreCommitted(json, mark, 1, 1000);
            Assert.AreEqual(1L, f.Bridge.Epoch);
            Assert.AreEqual(mark.Tick, f.Bridge.Match.Tick);
            Assert.AreEqual(120100L, f.Bridge.Presence().Single(p => p.PlayerId == Other).ReconnectDeadline);
            Assert.AreEqual(121000L, f.Bridge.Presence().Single(p => p.PlayerId == Player).ReconnectDeadline);
            Assert.IsTrue(f.Bridge.Reconnect(Player, 1000));
            f.Bridge.SetTransportReady(true, true);
            Assert.AreEqual(0, f.Bridge.Advance(1017).Receipts.Count);
            Assert.Throws<ArgumentException>(() => f.Bridge.RestoreCommitted(json, mark, 1, 1100));
        }
        [Test] public void ReturnedSeatRemainsTerminalThroughCommittedMigration()
        {
            var f = Create(); f.Bridge.Return(Other, 100);
            var mark = f.Bridge.Publish(out string json); Assert.IsTrue(f.Bridge.AcknowledgeDurable(mark));
            f.Bridge.PauseForMigration(); f.Bridge.RestoreCommitted(json, mark, 1, 1000);
            Assert.IsTrue(f.Bridge.Presence().Single(p => p.PlayerId == Other).Returned);
            Assert.IsFalse(f.Bridge.Reconnect(Other, 1001));
        }
        [Test] public void SnapshotCannotBeReboundToAnotherHashOrTick()
        {
            var f = Create(); var mark = f.Bridge.Publish(out string json);
            var wrongHash = new OnlineSnapshotMark(mark.MatchId, mark.IdentityHash, mark.Epoch, mark.Sequence, mark.Tick, new string('f', 64));
            var wrongTick = new OnlineSnapshotMark(mark.MatchId, mark.IdentityHash, mark.Epoch, mark.Sequence, mark.Tick + 1, mark.StateHash);
            Assert.Throws<FormatException>(() => f.Bridge.ValidateSnapshot(json, wrongHash));
            Assert.Throws<FormatException>(() => f.Bridge.ValidateSnapshot(json, wrongTick));
            Assert.IsFalse(f.Bridge.AcknowledgeDurable(wrongHash));
            Assert.IsTrue(f.Bridge.AcknowledgeDurable(mark));
        }
    }
}
