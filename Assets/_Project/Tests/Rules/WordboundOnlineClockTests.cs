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
    public sealed class WordboundOnlineClockTests
    {
        const string Player = "11111111-1111-4111-a111-111111111111";
        const string Match = "33333333-3333-4333-a333-333333333333";
        const string Party = "44444444-4444-4444-a444-444444444444";
        const string Attempt = "55555555-5555-4555-a555-555555555555";
        const long Server = 1700000000;
        static readonly string Secret = OnlineJson.Base64Url(Enumerable.Repeat((byte)42, 32).ToArray());
        static readonly OnlineSettings Settings = new OnlineSettings("TimeRun", "Solo", false, "asia", "1", "1");
        static readonly OnlineContent Content = new OnlineContent("BuiltIn", "test", new string('a', 64), new string('b', 64), new string('c', 64), new string('d', 64));
        RSA rsa;
        OnlineAdmissionVerifier verifier;

        [SetUp] public void SetUp()
        {
            rsa = new RSACryptoServiceProvider(2048) { PersistKeyInCsp = false };
            var key = rsa.ExportParameters(false);
            verifier = new OnlineAdmissionVerifier("wordbound-test", OnlineJson.Encode(new Dictionary<string, object> { ["keys"] = new[] {
                new Dictionary<string, object> { ["kty"] = "RSA", ["kid"] = "test-key", ["alg"] = "RS256", ["use"] = "sig",
                    ["n"] = OnlineJson.Base64Url(key.Modulus), ["e"] = OnlineJson.Base64Url(key.Exponent) } } }));
        }
        [TearDown] public void TearDown() => rsa?.Dispose();

        static Dictionary<string, object> Seat() => new Dictionary<string, object> {
            ["playerId"] = Player, ["partyId"] = Party, ["partyRevision"] = "1", ["attemptId"] = Attempt,
            ["teamId"] = 0, ["seat"] = 0, ["connectionSecretHash"] = OnlineJson.Hash(Secret) };
        static Dictionary<string, object> Claims(string audience, long issued, long lifetime = 90) => new Dictionary<string, object> {
            ["version"] = 1, ["iss"] = "wordbound-test", ["aud"] = audience, ["iat"] = issued, ["exp"] = issued + lifetime,
            ["matchId"] = Match, ["reservationGeneration"] = "1", ["rosterHash"] = OnlineJson.Hash(new[] { Seat() }) };
        static Dictionary<string, object> Start(long issued, long? startedSeconds = null)
        {
            var claims = Claims("wordbound-match-start", issued);
            claims["startedAt"] = (startedSeconds ?? issued) * 1000; claims["settings"] = Settings.ToValue();
            claims["content"] = Content.ToValue(); claims["roster"] = new[] { Seat() };
            return claims;
        }
        static Dictionary<string, object> Admission(long issued)
        {
            var claims = Claims("wordbound-admission", issued);
            foreach (var entry in Seat()) claims[entry.Key] = entry.Value;
            claims["sub"] = Player; claims["protocolVersion"] = "1"; claims["modeHash"] = Content.ModeHash;
            claims["courseHash"] = Content.CourseHash; claims["manifestHash"] = Content.ManifestHash; claims["catalogHash"] = Content.CatalogHash;
            return claims;
        }
        static Dictionary<string, object> Connection(long issued)
        {
            var claims = Claims("wordbound-connection", issued, 15);
            claims["playerId"] = Player; claims["hostId"] = Player; claims["connectionId"] = Attempt;
            claims["connectionEpoch"] = "1"; claims["connectionRevision"] = "2"; claims["leaseExpiresAt"] = (issued + 15) * 1000;
            return claims;
        }
        static OnlineMatchExpectation Expected() => new OnlineMatchExpectation(Match, "1", OnlineJson.Hash(new[] { Seat() }), Settings, Content);
        string Sign(Dictionary<string, object> claims)
        {
            string input = OnlineJson.Base64Url(Encoding.UTF8.GetBytes("{\"alg\":\"RS256\",\"typ\":\"JWT\",\"kid\":\"test-key\"}")) +
                "." + OnlineJson.Base64Url(Encoding.UTF8.GetBytes(OnlineJson.Encode(claims)));
            return input + "." + OnlineJson.Base64Url(rsa.SignData(Encoding.ASCII.GetBytes(input), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
        }

        [Test] public void TokensIssuedOneSecondAheadOfTheLocalClockAreAccepted()
        {
            var match = verifier.VerifyStart(Sign(Start(Server + 1)), Expected(), Server);
            Assert.AreEqual(Server + 91, match.ExpiresAtSeconds);
            Assert.AreEqual(Player, verifier.VerifyAdmission(Sign(Admission(Server + 1)), match, Player, Secret, Server).PlayerId);
            Assert.AreEqual(Player, verifier.VerifyConnection(Sign(Connection(Server + 1)), match, Player, Player, Attempt, Server * 1000).PlayerId);
            Assert.DoesNotThrow(() => verifier.VerifyStart(Sign(Start(Server, Server + 1)), Expected(), Server));
            Assert.DoesNotThrow(() => verifier.VerifyStart(Sign(Start(Server + OnlineAdmissionVerifier.ClockLeewaySeconds)), Expected(), Server));
        }

        [Test] public void TokensIssuedSixtySecondsAheadAreRejected()
        {
            Assert.Throws<FormatException>(() => verifier.VerifyStart(Sign(Start(Server + 60)), Expected(), Server));
            Assert.Throws<FormatException>(() => verifier.VerifyStart(Sign(Start(Server + OnlineAdmissionVerifier.ClockLeewaySeconds + 1)), Expected(), Server));
            var start = Assert.Throws<FormatException>(() => verifier.VerifyStart(Sign(Start(Server, Server + 60)), Expected(), Server));
            Assert.AreEqual("Match start is in the future.", start.Message);
            var match = verifier.VerifyStart(Sign(Start(Server)), Expected(), Server);
            Assert.Throws<FormatException>(() => verifier.VerifyAdmission(Sign(Admission(Server + 60)), match, Player, Secret, Server));
            Assert.Throws<FormatException>(() => verifier.VerifyConnection(Sign(Connection(Server + 60)), match, Player, Player, Attempt, Server * 1000));
        }

        [Test] public void ExpiryIsStillEnforcedExactly()
        {
            Assert.DoesNotThrow(() => verifier.VerifyStart(Sign(Start(Server)), Expected(), Server + 89));
            Assert.Throws<FormatException>(() => verifier.VerifyStart(Sign(Start(Server)), Expected(), Server + 90));
            Assert.Throws<FormatException>(() => verifier.VerifyStart(Sign(Start(Server - 120)), Expected(), Server));
            var match = verifier.VerifyStart(Sign(Start(Server)), Expected(), Server);
            Assert.Throws<FormatException>(() => verifier.VerifyAdmission(Sign(Admission(Server)), match, Player, Secret, Server + 90));
            Assert.Throws<FormatException>(() => verifier.VerifyConnection(Sign(Connection(Server)), match, Player, Player, Attempt, (Server + 15) * 1000));
            Assert.Throws<FormatException>(() => verifier.VerifyConnection(Sign(Connection(Server + 1)), match, Player, Player, Attempt, (Server + 16) * 1000));
        }

        [Test] public void ServerClockOffsetIsAppliedToVerification()
        {
            long local = 5000, wall = (Server - 120) * 1000;
            var clock = new OnlineServerClock(() => wall, () => local);
            Assert.IsFalse(clock.Synchronized);
            Assert.AreEqual(wall, clock.Now());
            var certificate = Sign(Start(Server));
            Assert.Throws<FormatException>(() => verifier.VerifyStart(certificate, Expected(), clock.Now() / 1000));
            Assert.IsTrue(clock.Observe(Server * 1000 + 50, 5000, 5100));
            Assert.IsTrue(clock.Synchronized);
            Assert.AreEqual(50, clock.UncertaintyMilliseconds);
            Assert.AreEqual(Server * 1000, clock.Now());
            Assert.DoesNotThrow(() => verifier.VerifyStart(certificate, Expected(), clock.Now() / 1000));
            local += 91000;
            Assert.Throws<FormatException>(() => verifier.VerifyStart(certificate, Expected(), clock.Now() / 1000));
        }

        [Test] public void ServerClockPrefersPreciseSamplesAndNeverRunsBackward()
        {
            long local = 1000;
            var clock = new OnlineServerClock(() => 0, () => local);
            Assert.IsTrue(clock.Observe(Server * 1000, 900, 1100));
            long first = clock.Now();
            Assert.AreEqual(Server * 1000, first);
            Assert.IsFalse(clock.Observe(Server * 1000 - 1000, 0, 1000, 1000), "A less precise consistent sample must not replace a precise one.");
            Assert.IsFalse(clock.Observe(Server * 1000 - 5000, 950, 1000), "One disagreeing sample is not enough to move the clock.");
            Assert.AreEqual(first, clock.Now());
            Assert.IsTrue(clock.Observe(Server * 1000 - 5000, 950, 1000), "A second agreeing sample means the server clock changed.");
            Assert.AreEqual(first, clock.Now(), "The estimate must stay monotonic after a backward correction.");
            local += 6000;
            Assert.Greater(clock.Now(), first);
            Assert.IsFalse(clock.Observe(Server * 1000, 2000, 2000 + OnlineServerClock.MaximumRoundTripMilliseconds + 1));
            Assert.IsFalse(clock.Observe(0, 1000, 1001));
            Assert.IsFalse(clock.Observe(Server * 1000, 1001, 1000));
        }

        [Test] public void ServerClockAcceptsLargeJumpsOnlyWhenTwoSamplesAgree()
        {
            long local = 1000;
            var clock = new OnlineServerClock(() => 0, () => local);
            Assert.IsTrue(clock.Observe(Server * 1000, 900, 1100));
            Assert.IsFalse(clock.Observe(Server * 1000 + 10000, 1950, 2000));
            Assert.IsFalse(clock.Observe(Server * 1000 + 20000, 2950, 3000), "Two disagreeing jumps must not move the clock.");
            local = 3000;
            Assert.AreEqual(Server * 1000 + 2000, clock.Now());
            Assert.IsTrue(clock.Observe(Server * 1000 + 21000, 3950, 4000));
            local = 4000;
            Assert.AreEqual(Server * 1000 + 21025, clock.Now());
            Assert.IsTrue(clock.Observe(Server * 1000 + 22000 + 1500, 4975, 5000), "Small precise corrections still apply at once.");
            Assert.AreEqual(Server * 1000 + 22513, clock.Now());
        }

        [Test] public void ConnectionStateChecksToleranceAgainstThePriorEstimateAndStillLearnsTheClock()
        {
            long local = 20000, wall = (Server - 600) * 1000;
            var clock = new OnlineServerClock(() => wall, () => local);
            var transport = new Transport { Advance = elapsed => local += elapsed };
            var client = new OnlineCoordinatorClient(transport);
            transport.Next = Json(new Dictionary<string, object> { ["playerId"] = Player, ["accessToken"] = "test.access.token",
                ["refreshToken"] = Player + "." + Secret, ["expiresIn"] = 900 });
            client.CreateGuest("Tester").GetAwaiter().GetResult();
            var match = verifier.VerifyStart(Sign(Start(Server)), Expected(), Server);
            var seat = new Dictionary<string, object> { ["playerId"] = Player, ["returned"] = false, ["connectionEpoch"] = "0",
                ["connectionId"] = null, ["hostId"] = null, ["leaseExpiresAt"] = 0, ["reconnectDeadline"] = Server * 1000 + 120000 };
            transport.Next = Json(new Dictionary<string, object> { ["matchId"] = Match, ["reservationGeneration"] = "1",
                ["rosterHash"] = Expected().RosterHash, ["revision"] = "0", ["serverTime"] = Server * 1000, ["seats"] = new[] { seat } });
            var raw = new OnlineConnectionService(client, verifier, Expected(), () => wall);
            Assert.Throws<FormatException>(() => raw.Renew(match, Array.Empty<OnlineConnectionLease>(), default).GetAwaiter().GetResult());
            var service = new OnlineConnectionService(client, verifier, Expected(), clock);
            Assert.Throws<FormatException>(() => service.Renew(match, Array.Empty<OnlineConnectionLease>(), default).GetAwaiter().GetResult(),
                "The tolerance check must use the estimate held before this sample.");
            Assert.IsTrue(clock.Synchronized, "The rejected renewal still teaches the clock.");
            Assert.AreEqual(Player, service.Renew(match, Array.Empty<OnlineConnectionLease>(), default).GetAwaiter().GetResult().Single().PlayerId);
            Assert.AreEqual(40, clock.UncertaintyMilliseconds);
            Assert.That(Math.Abs(clock.Now() - Server * 1000), Is.LessThan(1000));
        }

        static OnlineResponse Json(object value) => new OnlineResponse(200, OnlineJson.Encode(value));
        sealed class Transport : IOnlineHttpTransport
        {
            public OnlineResponse Next = new OnlineResponse(200, "{}");
            public Action<long> Advance;
            public Task<OnlineResponse> Send(OnlineRequest request, CancellationToken ct)
            { ct.ThrowIfCancellationRequested(); Advance?.Invoke(80); return Task.FromResult(Next); }
        }
    }
}
