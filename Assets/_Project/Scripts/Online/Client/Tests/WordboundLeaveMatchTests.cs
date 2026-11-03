using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Online.Tests
{
    public sealed class WordboundLeaveMatchTests
    {
        const string Player = "10000000-0000-4000-8000-000000000001";
        const string PartyId = "20000000-0000-4000-8000-000000000001";
        const string MatchId = "30000000-0000-4000-8000-000000000001";
        const string Seed = "0123456789abcdef";
        const string Code = "1234567890abcdef";
        static readonly string Secret = OnlineJson.Base64Url(Enumerable.Repeat((byte)42, 32).ToArray());
        FakeHttp http;
        WordboundPartyCoordinator party;
        RSA rsa;
        string items, rules;
        OnlineSettings settings;
        long now;

        [SetUp]
        public void SetUp()
        {
            string root = Environment.GetEnvironmentVariable("WORDBOUND_PROJECT_ROOT") ?? Directory.GetCurrentDirectory();
            items = File.ReadAllText(Path.Combine(root, "Assets/_Project/Data/Config/items.json"));
            rules = File.ReadAllText(Path.Combine(root, "Assets/_Project/Data/Config/rules.json"));
            rsa = new RSACryptoServiceProvider(2048) { PersistKeyInCsp = false };
            var key = rsa.ExportParameters(false);
            var verifier = new OnlineAdmissionVerifier("wreckabulary-wordbound", OnlineJson.Parse(OnlineJson.Encode(new Dictionary<string, object> {
                ["keys"] = new[] { new Dictionary<string, object> { ["kty"] = "RSA", ["kid"] = "test-key", ["use"] = "sig",
                    ["alg"] = "RS256", ["n"] = OnlineJson.Base64Url(key.Modulus), ["e"] = OnlineJson.Base64Url(key.Exponent) } } })));
            now = 1000000; http = new FakeHttp();
            party = new WordboundPartyCoordinator(new OnlineCoordinatorClient(http), verifier, items, rules, "leave-tests", clock: () => now);
            settings = party.Settings("Freestyle", "Solo", false, "asia");
            http.Handler = request => Task.FromResult(request.Path == "/v1/guests" ? Guest() : Response(PartyValue()));
        }
        [TearDown] public void TearDown() { party?.Dispose(); rsa?.Dispose(); }

        async Task<WordboundPartyLaunchContext> Launch()
        {
            await party.Connect("Guest"); await party.CreateParty(settings, Seed);
            var content = party.Content.PrivateBinding;
            var assignment = Assignment(content);
            http.Handler = request => {
                if (request.Path.EndsWith("/certificate"))
                {
                    var claims = Claims("wordbound-match-start");
                    claims["startedAt"] = now; claims["settings"] = settings.ToValue();
                    claims["content"] = content.ToValue(); claims["roster"] = new[] { Seat() };
                    return Task.FromResult(Response(new Dictionary<string, object> { ["certificate"] = Sign(claims) }));
                }
                return Task.FromResult(request.Path == "/v1/auth/refresh" ? Guest() : Response(PartyValue(assignment)));
            };
            await party.Poll();
            var launch = party.TakeLaunch();
            Assert.NotNull(launch, party.Error);
            Assert.IsTrue(party.MatchActive);
            return launch;
        }

        [Test]
        public async Task LeavingWhileTheServiceThrowsReturnsLocallyAndQueuesTheReturn()
        {
            var launch = await Launch();
            http.Handler = _ => Task.FromException<OnlineResponse>(new IOException("Coordinator unreachable"));
            try { await launch.Coordinator.MatchAction(launch.Verified.Match, "return"); Assert.Fail("The fake service must be unreachable."); }
            catch (IOException) { }
            party.LeaveMatchLocally(launch.Verified.Match);
            Assert.IsFalse(party.MatchActive);
            Assert.IsTrue(party.ReturnQueued);
            Assert.IsNull(party.TakeLaunch());
            int before = http.Requests.Count;
            await party.Poll();
            Assert.AreEqual(WordboundPartyCoordinator.QueuedReturnMessage, party.Error);
            Assert.IsTrue(party.ReturnQueued);
            Assert.AreEqual(1, http.Requests.Count - before);
            Assert.IsTrue(http.Requests.Last().Path.EndsWith("/matches/" + MatchId + "/return"));
            var assignment = Assignment(party.Content.PrivateBinding); assignment["state"] = "STARTED";
            http.Handler = request => Task.FromResult(Response(request.Path.EndsWith("/return") ? new Dictionary<string, object>() : PartyValue(assignment)));
            await party.Poll();
            Assert.IsFalse(party.ReturnQueued);
            Assert.IsNull(party.Error);
            Assert.AreEqual(1, http.Requests.Skip(before + 1).Count(r => r.Path.EndsWith("/return") && r.Method == "POST"));
            await party.Poll();
            Assert.AreEqual(1, http.Requests.Skip(before + 1).Count(r => r.Path.EndsWith("/return")), "A delivered return is sent once.");
            Assert.IsFalse(http.Requests.Skip(before).Any(r => r.Path.EndsWith("/certificate") || r.Path.EndsWith("/admission")),
                "A match left locally is never joined again.");
            Assert.IsNull(party.TakeLaunch());
        }

        [Test]
        public async Task TerminalServiceRefusalDropsTheQueuedReturn()
        {
            var launch = await Launch();
            party.LeaveMatchLocally(launch.Verified.Match);
            http.Handler = request => Task.FromResult(request.Path.EndsWith("/return") ?
                new OnlineResponse(409, "{\"error\":{\"code\":\"RECONNECT_EXPIRED\"}}") : Response(PartyValue()));
            await party.Poll();
            Assert.IsFalse(party.ReturnQueued);
            Assert.IsNull(party.Error);
        }

        [Test]
        public async Task PartyActionsDeliverTheQueuedReturnFirst()
        {
            var launch = await Launch();
            party.LeaveMatchLocally(launch.Verified.Match);
            http.Handler = _ => Task.FromException<OnlineResponse>(new IOException("Coordinator unreachable"));
            await party.LeaveParty();
            Assert.IsTrue(party.RetryRequired); Assert.IsTrue(party.ReturnQueued); Assert.NotNull(party.Party);
            var order = new List<string>();
            http.Handler = request => { order.Add(request.Path.Substring(request.Path.LastIndexOf('/') + 1)); return Task.FromResult(Response(new Dictionary<string, object>())); };
            await party.Retry();
            Assert.IsNull(party.Error); Assert.IsNull(party.Party); Assert.IsFalse(party.ReturnQueued);
            CollectionAssert.AreEqual(new[] { "return", "leave" }, order);
        }

        [Test]
        public async Task ConfirmedReturnClearsTheQueue()
        {
            var launch = await Launch();
            party.LeaveMatchLocally(launch.Verified.Match);
            party.LeaveMatchLocally(launch.Verified.Match);
            Assert.IsTrue(party.ReturnQueued);
            party.NotifyReturned(MatchId);
            Assert.IsFalse(party.ReturnQueued);
            Assert.Throws<ArgumentNullException>(() => party.LeaveMatchLocally(null));
        }

        Dictionary<string, object> PartyValue(object assignment = null)
        {
            var result = new Dictionary<string, object> {
                ["id"] = PartyId, ["revision"] = "1", ["leaderId"] = Player, ["invite"] = Code,
                ["members"] = new[] { new Dictionary<string, object> { ["playerId"] = Player, ["ready"] = true } },
                ["settings"] = settings.ToValue(), ["state"] = assignment == null ? "OPEN" : "QUEUED" };
            if (assignment != null)
            {
                result["assignment"] = assignment;
                result["activeAttempt"] = new Dictionary<string, object> { ["channel"] = "PrivateCreate" };
            }
            return result;
        }
        Dictionary<string, object> Assignment(OnlineContent content)
        {
            var roster = new[] { Seat() };
            return new Dictionary<string, object> { ["matchId"] = MatchId, ["state"] = "STARTED",
                ["reservationGeneration"] = "1", ["roster"] = roster, ["rosterHash"] = OnlineJson.Hash(roster),
                ["settings"] = settings.ToValue(), ["content"] = content.ToValue() };
        }
        static Dictionary<string, object> Seat() => new Dictionary<string, object> {
            ["playerId"] = Player, ["partyId"] = PartyId, ["partyRevision"] = "1",
            ["attemptId"] = "40000000-0000-4000-8000-000000000001",
            ["teamId"] = 0, ["seat"] = 0, ["connectionSecretHash"] = OnlineJson.Hash(Secret) };
        Dictionary<string, object> Claims(string audience) => new Dictionary<string, object> {
            ["version"] = 1, ["iss"] = "wreckabulary-wordbound", ["aud"] = audience, ["iat"] = now / 1000, ["exp"] = now / 1000 + 90,
            ["matchId"] = MatchId, ["reservationGeneration"] = "1", ["rosterHash"] = OnlineJson.Hash(new[] { Seat() }) };
        string Sign(Dictionary<string, object> claims)
        {
            string input = OnlineJson.Base64Url(Encoding.UTF8.GetBytes("{\"alg\":\"RS256\",\"typ\":\"JWT\",\"kid\":\"test-key\"}")) +
                "." + OnlineJson.Base64Url(Encoding.UTF8.GetBytes(OnlineJson.Encode(claims)));
            return input + "." + OnlineJson.Base64Url(rsa.SignData(Encoding.ASCII.GetBytes(input), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
        }
        static OnlineResponse Guest() => Response(new Dictionary<string, object> {
            ["playerId"] = Player, ["accessToken"] = "access.token", ["refreshToken"] = Player + "." + OnlineJson.Base64Url(new byte[32]),
            ["expiresIn"] = 900 });
        static OnlineResponse Response(object value) => new OnlineResponse(200, OnlineJson.Encode(value));
        sealed class FakeHttp : IOnlineHttpTransport
        {
            public readonly List<OnlineRequest> Requests = new List<OnlineRequest>();
            public Func<OnlineRequest, Task<OnlineResponse>> Handler;
            public Task<OnlineResponse> Send(OnlineRequest request, CancellationToken ct)
            { Requests.Add(request); return Handler(request); }
        }
    }
}
