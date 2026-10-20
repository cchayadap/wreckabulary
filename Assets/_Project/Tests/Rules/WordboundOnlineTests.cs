using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Rules.Tests
{
    public class WordboundOnlineTests
    {
        const string Player = "11111111-1111-4111-a111-111111111111";
        const string Other = "22222222-2222-4222-a222-222222222222";
        const string Match = "33333333-3333-4333-a333-333333333333";
        const string Party = "44444444-4444-4444-a444-444444444444";
        const string Attempt = "55555555-5555-4555-a555-555555555555";
        const long Now = 1700000000;
        static readonly string Secret = OnlineJson.Base64Url(Enumerable.Repeat((byte)42, 32).ToArray());
        static readonly OnlineSettings Settings = new OnlineSettings("TimeRun", "Solo", false, "asia", "1", "1");
        static readonly OnlineContent Content = new OnlineContent("BuiltIn", "test", new string('a', 64), new string('b', 64), new string('c', 64), new string('d', 64));
        static Dictionary<string, object> Seat() => new Dictionary<string, object> {
            ["playerId"] = Player, ["partyId"] = Party, ["partyRevision"] = "1", ["attemptId"] = Attempt,
            ["teamId"] = 0, ["seat"] = 0, ["connectionSecretHash"] = OnlineJson.Hash(Secret) };
        static Dictionary<string, object> Base(string audience) => new Dictionary<string, object> {
            ["version"] = 1, ["iss"] = "wordbound-test", ["aud"] = audience, ["iat"] = Now, ["exp"] = Now + 90,
            ["matchId"] = Match, ["reservationGeneration"] = "1", ["rosterHash"] = OnlineJson.Hash(new[] { Seat() }) };
        static Dictionary<string, object> Start()
        {
            var claims = Base("wordbound-match-start");
            claims["startedAt"] = Now * 1000; claims["settings"] = Settings.ToValue(); claims["content"] = Content.ToValue();
            claims["roster"] = new[] { Seat() }; return claims;
        }
        static Dictionary<string, object> Admission()
        {
            var claims = Base("wordbound-admission");
            foreach (var entry in Seat()) claims[entry.Key] = entry.Value;
            claims["sub"] = Player; claims["protocolVersion"] = "1"; claims["modeHash"] = Content.ModeHash;
            claims["courseHash"] = Content.CourseHash; claims["manifestHash"] = Content.ManifestHash; claims["catalogHash"] = Content.CatalogHash;
            return claims;
        }
        static OnlineMatchExpectation Expected(string rosterHash = null) => new OnlineMatchExpectation(Match, "1",
            rosterHash ?? OnlineJson.Hash(new[] { Seat() }), Settings, Content);
        static string Jwks(RSA rsa)
        {
            var key = rsa.ExportParameters(false);
            return OnlineJson.Encode(new Dictionary<string, object> { ["keys"] = new[] { new Dictionary<string, object> {
                ["kty"] = "RSA", ["kid"] = "test-key", ["alg"] = "RS256", ["use"] = "sig",
                ["n"] = OnlineJson.Base64Url(key.Modulus), ["e"] = OnlineJson.Base64Url(key.Exponent) } } });
        }
        static string Sign(RSA rsa, Dictionary<string, object> claims, string header = null)
        {
            string input = OnlineJson.Base64Url(Encoding.UTF8.GetBytes(header ?? "{\"alg\":\"RS256\",\"typ\":\"JWT\",\"kid\":\"test-key\"}")) +
                "." + OnlineJson.Base64Url(Encoding.UTF8.GetBytes(OnlineJson.Encode(claims)));
            return input + "." + OnlineJson.Base64Url(rsa.SignData(Encoding.ASCII.GetBytes(input), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
        }
        static RSA Key() { var rsa = RSA.Create(); rsa.KeySize = 2048; return rsa; }

        [Test] public void ConnectionAuthorizationPinsHostTransportAndExclusiveLease()
        {
            using var rsa = Key(); var verifier = new OnlineAdmissionVerifier("wordbound-test", Jwks(rsa));
            var match = verifier.VerifyStart(Sign(rsa, Start()), Expected(), Now);
            var claims = Base("wordbound-connection");
            claims["exp"] = Now + 15; claims["playerId"] = Player; claims["hostId"] = Player;
            claims["connectionId"] = Attempt; claims["connectionEpoch"] = "1"; claims["connectionRevision"] = "2";
            claims["leaseExpiresAt"] = Now * 1000 + 15000;
            string signed = Sign(rsa, claims);
            Assert.AreEqual("1", verifier.VerifyConnection(signed, match, Player, Player, Attempt, Now * 1000).Epoch);
            Assert.Throws<FormatException>(() => verifier.VerifyConnection(signed, match, Player, Other, Attempt, Now * 1000));
            Assert.Throws<FormatException>(() => verifier.VerifyConnection(signed, match, Player, Player, Other, Now * 1000));
            Assert.Throws<FormatException>(() => verifier.VerifyConnection(signed, match, Player, Player, Attempt, Now * 1000 + 15000));
            claims["aud"] = "wordbound-admission";
            Assert.Throws<FormatException>(() => verifier.VerifyConnection(Sign(rsa, claims), match, Player, Player, Attempt, Now * 1000));
        }

        [Test] public void SessionGateRequiresEveryOriginalSeatReadyAndRejectsIdentityReplacement()
        {
            using var rsa = Key(); var verifier = new OnlineAdmissionVerifier("wordbound-test", Jwks(rsa));
            var otherSeat = Seat(); otherSeat["playerId"] = Other; otherSeat["seat"] = 1;
            var start = Start(); start["roster"] = new[] { Seat(), otherSeat };
            string rosterHash = OnlineJson.Hash(start["roster"]); start["rosterHash"] = rosterHash;
            var match = verifier.VerifyStart(Sign(rsa, start), Expected(rosterHash), Now);
            var first = Admission(); first["rosterHash"] = rosterHash;
            var second = Admission(); second["rosterHash"] = rosterHash; second["playerId"] = Other; second["sub"] = Other; second["seat"] = 1;
            var a = verifier.VerifyAdmission(Sign(rsa, first), match, Player, Secret, Now);
            var b = verifier.VerifyAdmission(Sign(rsa, second), match, Other, Secret, Now);
            var gate = new OnlineSessionGate(match);
            gate.Admit(a, "peer-a", Now * 1000); gate.Ready("peer-a");
            Assert.IsFalse(gate.TryStart()); Assert.IsFalse(gate.CanCommand("peer-a"));
            Assert.Throws<InvalidOperationException>(() => gate.Admit(b, "peer-a", Now * 1000));
            Assert.Throws<InvalidOperationException>(() => gate.Admit(a, "peer-x", Now * 1000));
            gate.Admit(b, "peer-b", Now * 1000); gate.Ready("peer-b");
            Assert.IsTrue(gate.TryStart()); Assert.IsTrue(gate.CanCommand("peer-a"));
            gate.Freeze(); Assert.IsFalse(gate.CanCommand("peer-a"));
        }

        [Test] public void SessionGateKeepsExclusiveReconnectDeadlineAndTerminalReturnAcrossMigration()
        {
            using var rsa = Key(); var verifier = new OnlineAdmissionVerifier("wordbound-test", Jwks(rsa));
            var match = verifier.VerifyStart(Sign(rsa, Start()), Expected(), Now);
            var admission = verifier.VerifyAdmission(Sign(rsa, Admission()), match, Player, Secret, Now);
            var gate = new OnlineSessionGate(match);
            gate.Admit(admission, "peer-a", Now * 1000); gate.Ready("peer-a"); gate.TryStart();
            gate.Disconnect("peer-a", Now * 1000 + 1000);
            gate.Disconnect("peer-a", Now * 1000 + 2000);
            long deadline = gate.Presence()[0].ReconnectDeadline;
            Assert.AreEqual(Now * 1000 + 121000, deadline);
            gate.RestoreForMigration(gate.Presence(), Now * 1000 + 3000);
            Assert.AreEqual(deadline, gate.Presence()[0].ReconnectDeadline);
            var freshClaims = Admission(); freshClaims["iat"] = Now + 100; freshClaims["exp"] = Now + 190;
            var freshStart = Start(); freshStart["iat"] = Now + 100; freshStart["exp"] = Now + 190;
            var freshMatch = verifier.VerifyStart(Sign(rsa, freshStart), Expected(), Now + 100);
            var fresh = verifier.VerifyAdmission(Sign(rsa, freshClaims), freshMatch, Player, Secret, Now + 100);
            Assert.Throws<InvalidOperationException>(() => gate.Admit(fresh, "peer-b", deadline));
            gate.Admit(fresh, "peer-b", deadline - 1); gate.Ready("peer-b"); gate.ResumeAfterRestore();
            Assert.IsTrue(gate.CanCommand("peer-b"));
            gate.Return(Player); Assert.IsFalse(gate.CanCommand("peer-b"));
            gate.RestoreForMigration(gate.Presence(), deadline);
            Assert.Throws<InvalidOperationException>(() => gate.Admit(fresh, "peer-c", deadline + 1));
        }

        [Test] public void SignedRosterAndAdmissionBindAuthenticatedIdentityAndSecret()
        {
            using var rsa = Key(); var verifier = new OnlineAdmissionVerifier("wordbound-test", Jwks(rsa));
            var match = verifier.VerifyStart(Sign(rsa, Start()), Expected(), Now);
            var grant = Sign(rsa, Admission());
            Assert.AreEqual(Player, verifier.VerifyAdmission(grant, match, Player, Secret, Now).PlayerId);
            Assert.Throws<FormatException>(() => verifier.VerifyAdmission(grant, match, Other, Secret, Now));
            Assert.Throws<FormatException>(() => verifier.VerifyAdmission(grant, match, Player, OnlineJson.Base64Url(new byte[32]), Now));
        }
        [Test] public void RejectsSignatureTamperingAndUntrustedHeaders()
        {
            using var rsa = Key(); var verifier = new OnlineAdmissionVerifier("wordbound-test", Jwks(rsa));
            string signed = Sign(rsa, Start());
            Assert.Throws<FormatException>(() => verifier.VerifyStart(signed + "x", Expected(), Now));
            foreach (string header in new[] {
                "{\"alg\":\"HS256\",\"typ\":\"JWT\",\"kid\":\"test-key\"}", "{\"alg\":\"RS256\",\"typ\":\"JWT\",\"kid\":\"other\"}",
                "{\"alg\":\"RS256\",\"typ\":\"JWT\",\"kid\":\"test-key\",\"jku\":\"https://attacker.invalid\"}",
                "{\"alg\":\"RS256\",\"alg\":\"RS256\",\"typ\":\"JWT\",\"kid\":\"test-key\"}" })
                Assert.Throws<FormatException>(() => verifier.VerifyStart(Sign(rsa, Start(), header), Expected(), Now));
        }
        [Test] public void RejectsAudienceIssuerFutureAndExclusiveExpiry()
        {
            using var rsa = Key(); var verifier = new OnlineAdmissionVerifier("wordbound-test", Jwks(rsa));
            foreach (var change in new[] { ("aud", (object)"wordbound-admission"), ("iss", (object)"other"),
                ("iat", (object)(Now + 1)), ("exp", (object)Now), ("exp", (object)(Now + 91)) })
            {
                var claims = Start(); claims[change.Item1] = change.Item2;
                Assert.Throws<FormatException>(() => verifier.VerifyStart(Sign(rsa, claims), Expected(), Now));
            }
            Assert.DoesNotThrow(() => verifier.VerifyStart(Sign(rsa, Start()), Expected(), Now + 89));
            Assert.Throws<FormatException>(() => verifier.VerifyStart(Sign(rsa, Start()), Expected(), Now + 90));
        }
        [Test] public void RejectsResignedContentGenerationAndRosterAlterations()
        {
            using var rsa = Key(); var verifier = new OnlineAdmissionVerifier("wordbound-test", Jwks(rsa));
            foreach (var change in new[] { ("reservationGeneration", (object)"2"), ("matchId", (object)Other),
                ("rosterHash", (object)new string('f', 64)), ("unexpected", (object)true) })
            {
                var claims = Start(); claims[change.Item1] = change.Item2;
                Assert.Throws<FormatException>(() => verifier.VerifyStart(Sign(rsa, claims), Expected(), Now));
            }
            var wrongContent = Content.ToValue(); wrongContent["manifestHash"] = new string('f', 64);
            var changed = Start(); changed["content"] = wrongContent;
            Assert.Throws<FormatException>(() => verifier.VerifyStart(Sign(rsa, changed), Expected(), Now));
        }
        [Test] public void RejectsCorrectlyHashedButInvalidSeatDomains()
        {
            using var rsa = Key(); var verifier = new OnlineAdmissionVerifier("wordbound-test", Jwks(rsa));
            foreach (string field in new[] { "teamId", "seat" })
            {
                var seat = Seat(); seat[field] = 7; var roster = new[] { seat };
                string hash = OnlineJson.Hash(roster); var claims = Start(); claims["roster"] = roster; claims["rosterHash"] = hash;
                Assert.Throws<FormatException>(() => verifier.VerifyStart(Sign(rsa, claims), Expected(hash), Now));
            }
            var duplicates = new[] { Seat(), Seat() }; var duplicateClaims = Start();
            duplicateClaims["roster"] = duplicates; duplicateClaims["rosterHash"] = OnlineJson.Hash(duplicates);
            Assert.Throws<FormatException>(() => verifier.VerifyStart(Sign(rsa, duplicateClaims), Expected(OnlineJson.Hash(duplicates)), Now));
        }
        [Test] public void RejectsAdmissionRebindingWithinAValidMatch()
        {
            using var rsa = Key(); var verifier = new OnlineAdmissionVerifier("wordbound-test", Jwks(rsa));
            var match = verifier.VerifyStart(Sign(rsa, Start()), Expected(), Now);
            foreach (var change in new[] { ("sub", (object)Other), ("partyId", (object)Other), ("seat", (object)1),
                ("catalogHash", (object)new string('f', 64)), ("connectionSecretHash", (object)new string('f', 64)) })
            {
                var claims = Admission(); claims[change.Item1] = change.Item2;
                Assert.Throws<FormatException>(() => verifier.VerifyAdmission(Sign(rsa, claims), match, Player, Secret, Now));
            }
        }
        [Test] public void JsonRejectsDuplicateDeepOversizedAndAmbiguousNumericInput()
        {
            foreach (string json in new[] { "{\"a\":1,\"a\":2}", "{\"a\":01}", "{\"a\":1e3}", "{\"a\":9007199254740992}",
                "{\"a\":-9223372036854775808}", "[[[[[[[[[[[[[0]]]]]]]]]]]]]", "{\"a\":\"unescaped\nline\"}" })
                Assert.Throws<FormatException>(() => OnlineJson.Parse(json));
            Assert.Throws<FormatException>(() => OnlineJson.Parse(new string(' ', OnlineJson.MaximumBytes + 1)));
            Assert.AreEqual("{\"a\":1,\"b\":\"line\\nvalue\"}", OnlineJson.Encode(new Dictionary<string, object> { ["b"] = "line\nvalue", ["a"] = 1 }));
            Assert.Throws<FormatException>(() => new OnlineMatchExpectation(Match + "\n", "1", new string('a', 64), Settings, Content));
        }
        [Test] public void KeepsCanonicalDictionaryOrderAndOrderedRosterSemantics()
        {
            var a = new Dictionary<string, object> { ["b"] = 2, ["a"] = 1 };
            var b = new Dictionary<string, object> { ["a"] = 1, ["b"] = 2 };
            Assert.AreEqual(OnlineJson.Hash(a), OnlineJson.Hash(b));
            Assert.AreNotEqual(OnlineJson.Hash(new[] { "a", "b" }), OnlineJson.Hash(new[] { "b", "a" }));
            Assert.AreEqual("43258cff783fe7036d8a43033f830adfc60ec037382473548ac742b888292777", OnlineJson.Hash(a));
        }
        sealed class Transport : IOnlineHttpTransport
        {
            public readonly List<OnlineRequest> Requests = new List<OnlineRequest>();
            public OnlineResponse Next = new OnlineResponse(200, "{}");
            public Task<OnlineResponse> Send(OnlineRequest request, CancellationToken ct)
            { ct.ThrowIfCancellationRequested(); Requests.Add(request); return Task.FromResult(Next); }
        }
        static OnlineCoordinatorClient Client(Transport transport)
        {
            transport.Next = new OnlineResponse(200, OnlineJson.Encode(new Dictionary<string, object> {
                ["playerId"] = Player, ["accessToken"] = "test.access.token", ["refreshToken"] = Player + "." + Secret, ["expiresIn"] = 900 }));
            var client = new OnlineCoordinatorClient(transport);
            client.CreateGuest("Tester").GetAwaiter().GetResult(); transport.Next = new OnlineResponse(200, "{}");
            return client;
        }
        [Test] public void ClientPreservesOperationKeyAndDoesNotSendIdentityClaims()
        {
            var transport = new Transport(); var client = Client(transport);
            client.Queue(Party, "1", "QuickPlay", "same-key", Content).GetAwaiter().GetResult();
            client.Queue(Party, "1", "QuickPlay", "same-key", Content).GetAwaiter().GetResult();
            Assert.AreEqual(transport.Requests[1].JsonBody, transport.Requests[2].JsonBody);
            Assert.AreEqual("same-key", transport.Requests[1].IdempotencyKey);
            Assert.AreEqual("test.access.token", transport.Requests[1].AccessToken);
            Assert.IsFalse(OnlineJson.Parse(transport.Requests[1].JsonBody).Has("playerId"));
            Assert.Throws<ArgumentException>(() => client.Queue(Party, "1", "CustomFill", "same-key", Content));
            Assert.Throws<ArgumentException>(() => client.Queue(Party, "1", "PrivateJoin", "same-key"));
        }
        [Test] public void ClientUsesExactAcceptanceAndRejectsAuthenticationOrHttpFailures()
        {
            var transport = new Transport(); var client = new OnlineCoordinatorClient(transport);
            Assert.Throws<InvalidOperationException>(() => client.Profile().GetAwaiter().GetResult());
            client = Client(transport);
            client.AcceptMatch(Expected()).GetAwaiter().GetResult();
            var sent = transport.Requests.Last();
            Assert.IsTrue(sent.Path.Contains("asia%3A1%3A1%3ATimeRun%3ASolo"));
            Assert.AreEqual(Content.ManifestHash, OnlineJson.Parse(sent.JsonBody)["manifestHash"].String());
            transport.Next = new OnlineResponse(409, "{\"error\":{\"code\":\"REVISION_CONFLICT\"}}");
            var error = Assert.Throws<OnlineServiceException>(() => client.Party(Party).GetAwaiter().GetResult());
            Assert.AreEqual("REVISION_CONFLICT", error.Code); Assert.AreEqual(409, error.Status);
            client.ClearCredentials();
            Assert.Throws<InvalidOperationException>(() => client.PhotonTicket().GetAwaiter().GetResult());
        }
        [Test] public void ConnectionStateAcceptsExplicitUnattachedIdsButRejectsMissingOrNullRequiredFields()
        {
            using var rsa = Key(); var verifier = new OnlineAdmissionVerifier("wordbound-test", Jwks(rsa));
            var match = verifier.VerifyStart(Sign(rsa, Start()), Expected(), Now);
            var transport = new Transport(); var client = Client(transport);
            var service = new OnlineConnectionService(client, verifier, Expected(), () => Now * 1000);
            var seat = new Dictionary<string, object> {
                ["playerId"] = Player, ["returned"] = false, ["connectionEpoch"] = "0",
                ["connectionId"] = null, ["hostId"] = null, ["leaseExpiresAt"] = 0, ["reconnectDeadline"] = Now * 1000 + 120000 };
            var response = new Dictionary<string, object> {
                ["matchId"] = Match, ["reservationGeneration"] = "1", ["rosterHash"] = Expected().RosterHash,
                ["revision"] = "0", ["serverTime"] = Now * 1000, ["seats"] = new[] { seat } };
            transport.Next = new OnlineResponse(200, OnlineJson.Encode(response));
            var accepted = service.Renew(match, Array.Empty<OnlineConnectionLease>(), default).GetAwaiter().GetResult().Single();
            Assert.IsNull(accepted.ConnectionId); Assert.IsNull(accepted.HostId);
            seat.Remove("connectionId"); transport.Next = new OnlineResponse(200, OnlineJson.Encode(response));
            Assert.Throws<FormatException>(() => service.Renew(match, Array.Empty<OnlineConnectionLease>(), default).GetAwaiter().GetResult());
            seat["connectionId"] = null; seat["returned"] = null; transport.Next = new OnlineResponse(200, OnlineJson.Encode(response));
            Assert.Throws<FormatException>(() => service.Renew(match, Array.Empty<OnlineConnectionLease>(), default).GetAwaiter().GetResult());
        }
        sealed class DelayedTransport : IOnlineHttpTransport
        {
            public readonly TaskCompletionSource<OnlineResponse> Completion = new TaskCompletionSource<OnlineResponse>();
            public int RequestCount;
            public Task<OnlineResponse> Send(OnlineRequest request, CancellationToken ct) { RequestCount++; return Completion.Task; }
        }
        [Test] public void ConcurrentRefreshOfOneCredentialUsesOneRotationAndKeepsTheWinningResult()
        {
            var transport = new DelayedTransport(); var client = new OnlineCoordinatorClient(transport);
            var first = client.Refresh(Player + "." + Secret); var second = client.Refresh(Player + "." + Secret);
            Assert.AreSame(first, second); Assert.AreEqual(1, transport.RequestCount);
            Assert.Throws<InvalidOperationException>(() => client.Refresh(Player + "." + OnlineJson.Base64Url(new byte[32])));
            string rotated = Player + "." + OnlineJson.Base64Url(new byte[32]);
            transport.Completion.SetResult(new OnlineResponse(200, OnlineJson.Encode(new Dictionary<string, object> {
                ["playerId"] = Player, ["accessToken"] = "new.access.token", ["refreshToken"] = rotated, ["expiresIn"] = 900 })));
            Assert.AreEqual(rotated, first.GetAwaiter().GetResult().RefreshToken);
            Assert.AreEqual(rotated, second.GetAwaiter().GetResult().RefreshToken);
            Assert.AreEqual("new.access.token", client.Credentials.AccessToken);
        }
        [Test] public void SignoutStillFencesACoalescedRefreshReply()
        {
            var transport = new DelayedTransport(); var client = new OnlineCoordinatorClient(transport);
            var pending = client.Refresh(Player + "." + Secret); client.ClearCredentials();
            transport.Completion.SetResult(new OnlineResponse(200, OnlineJson.Encode(new Dictionary<string, object> {
                ["playerId"] = Player, ["accessToken"] = "new.access.token", ["refreshToken"] = Player + "." + Secret, ["expiresIn"] = 900 })));
            Assert.Throws<TaskCanceledException>(() => pending.GetAwaiter().GetResult());
            Assert.IsNull(client.Credentials);
        }
        [Test] public void ClearingCredentialsFencesAnOutstandingAuthenticationReply()
        {
            var transport = new DelayedTransport(); var client = new OnlineCoordinatorClient(transport);
            var pending = client.CreateGuest("Late reply"); client.ClearCredentials();
            transport.Completion.SetResult(new OnlineResponse(200, OnlineJson.Encode(new Dictionary<string, object> {
                ["playerId"] = Player, ["accessToken"] = "test.access.token", ["refreshToken"] = Player + "." + Secret, ["expiresIn"] = 900 })));
            Assert.Throws<OperationCanceledException>(() => pending.GetAwaiter().GetResult());
            Assert.IsNull(client.Credentials);
        }
    }
}
