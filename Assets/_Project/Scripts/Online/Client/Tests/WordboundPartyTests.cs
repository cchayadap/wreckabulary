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
    public sealed class WordboundPartyTests
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
            var parameters = rsa.ExportParameters(false);
            Assert.AreEqual(256, parameters.Modulus.Length);
            var verifier = new OnlineAdmissionVerifier("wreckabulary-wordbound", OnlineJson.Parse(OnlineJson.Encode(new Dictionary<string, object> {
                ["keys"] = new[] { new Dictionary<string, object> { ["kty"] = "RSA", ["kid"] = "test-key", ["use"] = "sig",
                    ["alg"] = "RS256", ["n"] = OnlineJson.Base64Url(parameters.Modulus), ["e"] = OnlineJson.Base64Url(parameters.Exponent) } } })));
            now = 1000000; http = new FakeHttp();
            party = new WordboundPartyCoordinator(new OnlineCoordinatorClient(http), verifier, items, rules,
                "party-tests", clock: () => now);
            settings = party.Settings("Freestyle", "Solo", false, "asia");
            http.Handler = request => Task.FromResult(request.Path == "/v1/guests" ? Guest() : Response(PartyValue()));
        }
        [TearDown] public void TearDown() { party?.Dispose(); rsa?.Dispose(); }

        [TestCase("TimeRun", "Solo")]
        [TestCase("TeamRace", "Squad")]
        [TestCase("BrawlRace", "Duo")]
        [TestCase("FreeForAll", "Solo")]
        [TestCase("Freestyle", "Solo")]
        public void RealContentBindsSeedModeAndRules(string mode, string format)
        {
            var content = new WordboundClientContent(items, rules, mode, format, Seed);
            var same = new WordboundClientContent(items, rules, mode, format, Seed);
            var other = new WordboundClientContent(items, rules, mode, format, "fedcba9876543210");
            Assert.IsTrue(content.Matches(same.PrivateBinding));
            Assert.IsFalse(content.Matches(other.PrivateBinding));
            var mutable = content.Rules; mutable.StarterLetters = "CHEAT";
            Assert.AreNotEqual("CHEAT", content.Rules.StarterLetters);
            if (content.Mode.IsRace) Assert.IsTrue(content.PhysicalPuzzles.BindsRules);
        }
        [Test]
        public void PublicRegistryStartsClosedAndRequiresEveryExactHash()
        {
            var content = new WordboundClientContent(items, rules, "TimeRun", "Solo", Seed);
            Assert.IsFalse(new WordboundPublicCourseRegistry().Allows(content));
            string registration = WordboundPublicCourseRegistry.RegistrationJson(new[] { content });
            var registry = new WordboundPublicCourseRegistry(registration);
            Assert.IsTrue(registry.Allows(content));
            Assert.IsFalse(registry.Allows(new WordboundClientContent(items, rules, "TimeRun", "Solo", "fedcba9876543210")));
            Assert.Throws<ArgumentException>(() => new WordboundPublicCourseRegistry(
                OnlineJson.Encode(new[] { content.PrivateBinding.ToValue() })));
        }
        [Test]
        public void PartyAndRoomInvitesPreserveSeedAndRejectIncompatibleSettings()
        {
            var invite = new WordboundPartyInvite(WordboundPartyInvite.Encode(PartyId, Code, Seed));
            Assert.AreEqual(Seed, invite.Seed); Assert.AreEqual(PartyId, invite.PartyId);
            Assert.Throws<ArgumentException>(() => new WordboundPartyInvite(Code));
            var room = new WordboundRoomInvite(WordboundRoomInvite.Encode(Code, settings, Seed));
            room.RequireCompatible(settings, Seed);
            Assert.Throws<InvalidOperationException>(() => room.RequireCompatible(settings, "fedcba9876543210"));
            Assert.Throws<InvalidOperationException>(() => room.RequireCompatible(party.Settings("Freestyle", "Solo", false, "eu"), Seed));
        }
        [Test]
        public async Task LostCreateResponseRetriesSameKeyAndBodyAndBlocksDifferentAction()
        {
            await party.Connect("Guest");
            var requests = new List<OnlineRequest>();
            http.Handler = request => {
                requests.Add(request);
                if (requests.Count == 1) return Task.FromException<OnlineResponse>(new IOException("Lost response"));
                return Task.FromResult(Response(PartyValue()));
            };
            await party.CreateParty(settings, Seed);
            Assert.IsTrue(party.RetryRequired); Assert.IsNull(party.Party);
            await party.JoinParty(WordboundPartyInvite.Encode(PartyId, Code, Seed));
            Assert.AreEqual(1, requests.Count); Assert.IsTrue(party.RetryRequired);
            await party.Retry();
            Assert.AreEqual(2, requests.Count);
            Assert.AreEqual(requests[0].IdempotencyKey, requests[1].IdempotencyKey);
            Assert.AreEqual(requests[0].JsonBody, requests[1].JsonBody);
            Assert.AreEqual(PartyId, party.Party.Id); Assert.IsFalse(party.RetryRequired);
        }
        [Test]
        public async Task RepeatedClickAndPollCoalesceWhileRealRequestIsPending()
        {
            await party.Connect("Guest"); await party.CreateParty(settings, Seed);
            var pending = new TaskCompletionSource<OnlineResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
            int requests = 0;
            http.Handler = _ => { Interlocked.Increment(ref requests); return pending.Task; };
            var first = party.SetReady(true);
            await party.SetReady(true);
            await party.Poll();
            Assert.AreEqual(1, requests);
            pending.SetResult(Response(PartyValue(ready: true))); await first;
            pending = new TaskCompletionSource<OnlineResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
            var poll = party.Poll(); var again = party.Poll();
            Assert.AreSame(poll, again); Assert.AreEqual(2, requests);
            pending.SetResult(Response(PartyValue(ready: true))); await poll;
            Assert.IsTrue(party.Party.Members.Single().Ready);
        }
        [Test]
        public async Task DisposedMenuCannotPublishLatePartyResponse()
        {
            await party.Connect("Guest");
            var pending = new TaskCompletionSource<OnlineResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
            http.Handler = _ => pending.Task;
            var creating = party.CreateParty(settings, Seed);
            party.Dispose(); pending.SetResult(Response(PartyValue())); await creating;
            Assert.IsNull(party.Party); Assert.IsFalse(party.Authenticated);
            Assert.IsFalse(party.Busy);
        }
        [Test]
        public async Task ContentMismatchNeverAcceptsAssignedMatch()
        {
            await party.Connect("Guest"); await party.CreateParty(settings, Seed);
            var wrong = new WordboundClientContent(items, rules, "Freestyle", "Solo", "fedcba9876543210");
            http.Handler = request => Task.FromResult(Response(PartyValue(assignment: Assignment(wrong.PrivateBinding))));
            int before = http.Requests.Count;
            await party.Poll();
            Assert.That(party.Error, Does.Contain("map differs"));
            Assert.AreEqual(1, http.Requests.Count - before);
            Assert.IsNull(party.TakeLaunch());
        }
        [Test]
        public async Task CustomFillRequiresExactContentAcceptanceBeforeReady()
        {
            settings = party.Settings("TimeRun", "Solo", true, "asia");
            await party.Connect("Guest"); await party.CreateParty(settings, Seed);
            var assignment = Assignment(party.Content.PrivateBinding);
            http.Handler = request => Task.FromResult(Response(request.Method == "GET"
                ? PartyValue(assignment: assignment, channel: "CustomFill") : new Dictionary<string, object>()));
            await party.Poll();
            Assert.IsFalse(http.Requests.Any(r => r.Path.EndsWith("/admission") || r.Path.EndsWith("/ready")));
            await party.AcceptCustomContent();
            await party.Poll();
            Assert.AreEqual(1, http.Requests.Count(r => r.Path.EndsWith("/admission")));
            Assert.AreEqual(1, http.Requests.Count(r => r.Path.EndsWith("/ready")));
            await party.Poll();
            Assert.AreEqual(1, http.Requests.Count(r => r.Path.EndsWith("/ready")));
        }
        [Test]
        public async Task ReturnedReservationNeverTriggersReadinessAgain()
        {
            await party.Connect("Guest"); await party.CreateParty(settings, Seed);
            var assignment = Assignment(party.Content.PrivateBinding);
            http.Handler = request => Task.FromResult(Response(PartyValue(assignment: assignment, channel: "PrivateCreate")));
            party.NotifyReturned(MatchId); await party.Poll();
            Assert.IsFalse(http.Requests.Any(r => r.Path.EndsWith("/admission") || r.Path.EndsWith("/ready")));
            Assert.IsNull(party.TakeLaunch());
        }
        [Test]
        public async Task RefreshOccursBeforeGuestAccessExpires()
        {
            await party.Connect("Guest"); await party.CreateParty(settings, Seed);
            now += 841000;
            http.Handler = request => Task.FromResult(request.Path == "/v1/auth/refresh" ? Guest() : Response(PartyValue()));
            await party.Poll(); await party.Poll();
            Assert.AreEqual(1, http.Requests.Count(r => r.Path == "/v1/auth/refresh"));
        }
        [TestCase(false)]
        [TestCase(true)]
        public async Task SignedMatchLaunchesOnceAndEachRunnerGetsFreshVerifiedCredentials(bool initialized)
        {
            await party.Connect("Guest"); await party.CreateParty(settings, Seed);
            var content = party.Content.PrivateBinding;
            var assignment = Assignment(content); assignment["state"] = "STARTED";
            int tickets = 0;
            http.Handler = request => {
                if (request.Path.EndsWith("/certificate"))
                {
                    var claims = Claims("wordbound-match-start");
                    claims["startedAt"] = now; claims["settings"] = settings.ToValue();
                    claims["content"] = content.ToValue(); claims["roster"] = new[] { Seat() };
                    return Task.FromResult(Response(new Dictionary<string, object> { ["certificate"] = Sign(claims) }));
                }
                if (request.Path.EndsWith("/admission"))
                {
                    var claims = Claims("wordbound-admission");
                    foreach (var entry in Seat()) claims[entry.Key] = entry.Value;
                    claims["sub"] = Player; claims["protocolVersion"] = settings.ProtocolVersion;
                    claims["modeHash"] = content.ModeHash; claims["courseHash"] = content.CourseHash;
                    claims["manifestHash"] = content.ManifestHash; claims["catalogHash"] = content.CatalogHash;
                    return Task.FromResult(Response(new Dictionary<string, object> { ["grant"] = Sign(claims), ["connectionSecret"] = Secret }));
                }
                if (request.Path == "/v1/photon/tickets")
                    return Task.FromResult(Response(new Dictionary<string, object> { ["ticket"] = "fresh.ticket." + ++tickets,
                        ["appId"] = WordboundClientTrust.ProductionAppId, ["expiresAt"] = now + 60000 }));
                return Task.FromResult(request.Path == "/v1/auth/refresh" ? Guest() : Response(PartyValue(assignment: assignment)));
            };
            await party.Poll();
            var launch = party.TakeLaunch();
            Assert.NotNull(launch, party.Error);
            Assert.IsNull(party.TakeLaunch());
            var first = await launch.FreshJoin(CancellationToken.None);
            var second = await launch.FreshJoin(CancellationToken.None);
            Assert.AreEqual(Player, first.PlayerId); Assert.AreEqual(Player, second.PlayerId);
            Assert.AreNotEqual(first.PhotonTicket.Ticket, second.PhotonTicket.Ticket);
            Assert.AreEqual(2, http.Requests.Count(r => r.Path == "/v1/auth/refresh"));
            Assert.Throws<InvalidOperationException>(() => launch.ReturnWithoutClient(CancellationToken.None));
            if (initialized)
            {
                launch.MarkClientInitialized();
                Assert.Throws<InvalidOperationException>(() => launch.ConfirmNoClientInitialized());
                Assert.Throws<InvalidOperationException>(() => launch.ReturnWithoutClient(CancellationToken.None));
            }
            else
            {
                launch.ConfirmNoClientInitialized();
                Assert.Throws<InvalidOperationException>(() => launch.MarkClientInitialized());
                http.Handler = _ => Task.FromResult(new OnlineResponse(503, "{\"error\":{\"code\":\"UNAVAILABLE\"}}"));
                try { await launch.ReturnWithoutClient(CancellationToken.None); Assert.Fail("A failed return must propagate."); }
                catch (OnlineServiceException error) { Assert.AreEqual(503, error.Status); }
                Assert.IsTrue(party.MatchActive);
                http.Handler = _ => Task.FromResult(Response(new Dictionary<string, object>()));
                await launch.ReturnWithoutClient(CancellationToken.None);
                Assert.IsTrue(party.MatchActive, "Only confirmed menu notification clears the active context.");
                http.Handler = _ => Task.FromResult(Response(PartyValue(assignment: assignment)));
            }
            party.NotifyReturned(MatchId); await party.Poll();
            Assert.IsNull(party.TakeLaunch());
        }
        [Test]
        public async Task PendingMutationSuppressesAutomaticAdmissionFromEarlierPoll()
        {
            await party.Connect("Guest"); await party.CreateParty(settings, Seed);
            var pending = new TaskCompletionSource<OnlineResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
            var assignment = Assignment(party.Content.PrivateBinding);
            http.Handler = request => request.Method == "GET" ? pending.Task : Task.FromResult(Response(PartyValue()));
            var polling = party.Poll();
            var leaving = party.LeaveParty();
            pending.SetResult(Response(PartyValue(assignment: assignment)));
            await Task.WhenAll(polling, leaving);
            Assert.IsNull(party.Party);
            Assert.IsFalse(http.Requests.Any(r => r.Path.EndsWith("/admission") || r.Path.EndsWith("/ready")));
        }
        [Test]
        public async Task RestoredGuestWithUnavailableReleaseCanStillLeaveItsExistingParty()
        {
            settings = new OnlineSettings("Freestyle", "Solo", false, "asia", "older-release", "party-tests");
            http.Handler = request => Task.FromResult(request.Path == "/v1/auth/refresh" ? Guest() :
                request.Path == "/v1/me" ? Response(new Dictionary<string, object> { ["partyId"] = PartyId }) : Response(PartyValue()));
            await party.ResumeGuest(Player + "." + OnlineJson.Base64Url(new byte[32]), Seed);
            Assert.AreEqual(PartyId, party.Party.Id);
            Assert.IsNull(party.Content);
            Assert.Throws<InvalidOperationException>(() => party.SetReady(true));
            await party.LeaveParty();
            Assert.IsNull(party.Party);
            Assert.IsTrue(http.Requests.Any(r => r.Path.EndsWith("/leave")));
        }
        Dictionary<string, object> PartyValue(bool ready = false, object assignment = null, string channel = null)
        {
            var result = new Dictionary<string, object> {
                ["id"] = PartyId, ["revision"] = "1", ["leaderId"] = Player, ["invite"] = Code,
                ["members"] = new[] { new Dictionary<string, object> { ["playerId"] = Player, ["ready"] = ready } },
                ["settings"] = settings.ToValue(), ["state"] = channel == null ? "OPEN" : "QUEUED" };
            if (assignment != null) result["assignment"] = assignment;
            if (channel != null) result["activeAttempt"] = new Dictionary<string, object> { ["channel"] = channel };
            return result;
        }
        Dictionary<string, object> Assignment(OnlineContent content)
        {
            var roster = new[] { Seat() };
            return new Dictionary<string, object> { ["matchId"] = MatchId, ["state"] = "ADMITTING",
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
