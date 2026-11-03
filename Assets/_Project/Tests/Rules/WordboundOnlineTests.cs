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
        static RSA Key()
        {
            var rsa = new RSACryptoServiceProvider(2048) { PersistKeyInCsp = false };
            Assert.AreEqual(256, rsa.ExportParameters(false).Modulus.Length, "The fixture must actually generate a 2048-bit RSA key.");
            return rsa;
        }

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

        [Test] public void HostAuthorizationRequiresThisClientFreshNonceAndOriginalRosterHost()
        {
            using var rsa = Key(); var verifier = new OnlineAdmissionVerifier("wordbound-test", Jwks(rsa));
            var match = verifier.VerifyStart(Sign(rsa, Start()), Expected(), Now);
            var claims = Base("wordbound-connection");
            claims["exp"] = Now + 15; claims["playerId"] = Player; claims["hostId"] = Player;
            claims["connectionId"] = Attempt; claims["connectionEpoch"] = "1"; claims["connectionRevision"] = "2";
            claims["leaseExpiresAt"] = Now * 1000 + 15000;
            string signed = Sign(rsa, claims);
            Assert.AreEqual(Player, verifier.VerifyHostAuthorization(signed, match, Player, Attempt, Now * 1000).HostId);
            Assert.Throws<FormatException>(() => verifier.VerifyHostAuthorization(signed, match, Other, Attempt, Now * 1000));
            Assert.Throws<FormatException>(() => verifier.VerifyHostAuthorization(signed, match, Player, Other, Now * 1000));
            Assert.Throws<FormatException>(() => verifier.VerifyHostAuthorization(signed, match, Player, Attempt, Now * 1000 + 15000));
            string[] pieces = signed.Split('.'); byte[] signature = OnlineJson.DecodeBase64Url(pieces[2], 512); signature[0] ^= 1;
            Assert.Throws<FormatException>(() => verifier.VerifyHostAuthorization(pieces[0] + "." + pieces[1] + "." + OnlineJson.Base64Url(signature),
                match, Player, Attempt, Now * 1000));
            foreach (string field in new[] { "hostId", "matchId" })
            {
                var altered = new Dictionary<string, object>(claims); altered[field] = Other;
                Assert.Throws<FormatException>(() => verifier.VerifyHostAuthorization(Sign(rsa, altered), match, Player, Attempt, Now * 1000));
            }
        }

        static Dictionary<string, object> ConnectionClaims(long leaseExpiresAt)
        {
            var claims = Base("wordbound-connection");
            claims["exp"] = Now + 15; claims["playerId"] = Player; claims["hostId"] = Player;
            claims["connectionId"] = Attempt; claims["connectionEpoch"] = "1"; claims["connectionRevision"] = "2";
            claims["leaseExpiresAt"] = leaseExpiresAt;
            return claims;
        }

        [TestCase(0, 0)]
        [TestCase(504, 500)]
        [TestCase(999, 0)]
        [TestCase(999, 999)]
        public void ConnectionLeaseRetainsTheSignedExpirationSecondsFinalFraction(int issuanceFraction, int verificationFraction)
        {
            using var rsa = Key(); var verifier = new OnlineAdmissionVerifier("wordbound-test", Jwks(rsa));
            var match = verifier.VerifyStart(Sign(rsa, Start()), Expected(), Now);
            long leaseExpiresAt = (Now + 15) * 1000 + issuanceFraction;
            long verificationTime = Now * 1000 + verificationFraction;
            string signed = Sign(rsa, ConnectionClaims(leaseExpiresAt));
            var lease = verifier.VerifyConnection(signed, match, Player, Player, Attempt, verificationTime);
            Assert.AreEqual(leaseExpiresAt, lease.ExpiresAtMilliseconds);
            Assert.AreEqual(leaseExpiresAt, verifier.VerifyHostAuthorization(signed, match, Player, Attempt,
                verificationTime).ExpiresAtMilliseconds);
            if (issuanceFraction == 504)
                Assert.AreEqual(15004, lease.ExpiresAtMilliseconds - verificationTime);
        }

        [TestCase(1000L, 0)]
        [TestCase(1001L, 0)]
        [TestCase(1000L, 14000)]
        [TestCase(1001L, 14000)]
        [TestCase(5000L, 14000)]
        public void ConnectionLeaseRejectsTheNextSecondBoundaryAndBeyond(long beyondExpirationSecondStart, int verificationOffset)
        {
            using var rsa = Key(); var verifier = new OnlineAdmissionVerifier("wordbound-test", Jwks(rsa));
            var match = verifier.VerifyStart(Sign(rsa, Start()), Expected(), Now);
            string signed = Sign(rsa, ConnectionClaims((Now + 15) * 1000 + beyondExpirationSecondStart));
            long verificationTime = Now * 1000 + verificationOffset;
            Assert.Throws<FormatException>(() => verifier.VerifyConnection(signed, match, Player, Player, Attempt, verificationTime));
            Assert.Throws<FormatException>(() => verifier.VerifyHostAuthorization(signed, match, Player, Attempt, verificationTime));
        }

        [TestCase("expired-jwt")]
        [TestCase("future-jwt")]
        [TestCase("overlong-jwt")]
        [TestCase("expired-lease")]
        [TestCase("lease-at-now")]
        public void FractionalConnectionLeaseDoesNotRelaxJwtOrExclusiveLeaseTime(string invalid)
        {
            using var rsa = Key(); var verifier = new OnlineAdmissionVerifier("wordbound-test", Jwks(rsa));
            var match = verifier.VerifyStart(Sign(rsa, Start()), Expected(), Now);
            long verificationTime = Now * 1000 + 500;
            var claims = ConnectionClaims((Now + 15) * 1000 + 999);
            if (invalid == "expired-jwt") verificationTime = (Now + 15) * 1000;
            if (invalid == "future-jwt")
            {
                claims["iat"] = Now + 60; claims["exp"] = Now + 75;
                claims["leaseExpiresAt"] = (Now + 75) * 1000 + 999;
            }
            if (invalid == "overlong-jwt")
            {
                claims["exp"] = Now + 16;
                claims["leaseExpiresAt"] = (Now + 16) * 1000 + 999;
            }
            if (invalid == "expired-lease") claims["leaseExpiresAt"] = verificationTime - 1;
            if (invalid == "lease-at-now") claims["leaseExpiresAt"] = verificationTime;
            string signed = Sign(rsa, claims);
            Assert.Throws<FormatException>(() => verifier.VerifyConnection(signed, match, Player, Player, Attempt, verificationTime));
            Assert.Throws<FormatException>(() => verifier.VerifyHostAuthorization(signed, match, Player, Attempt, verificationTime));
        }

        [TestCase("playerId")]
        [TestCase("hostId")]
        [TestCase("connectionId")]
        [TestCase("matchId")]
        [TestCase("reservationGeneration")]
        [TestCase("rosterHash")]
        public void FractionalConnectionLeaseStillPinsEveryTransportAndMatchBinding(string field)
        {
            using var rsa = Key(); var verifier = new OnlineAdmissionVerifier("wordbound-test", Jwks(rsa));
            var match = verifier.VerifyStart(Sign(rsa, Start()), Expected(), Now);
            var claims = ConnectionClaims((Now + 15) * 1000 + 999);
            claims[field] = field == "reservationGeneration" ? "2" : field == "rosterHash" ? new string('e', 64) : Other;
            Assert.Throws<FormatException>(() => verifier.VerifyConnection(Sign(rsa, claims), match, Player, Player, Attempt, Now * 1000));
        }

        [Test] public void FractionalConnectionLeaseRejectsOutsidersAndAlteredSignature()
        {
            using var rsa = Key(); var verifier = new OnlineAdmissionVerifier("wordbound-test", Jwks(rsa));
            var match = verifier.VerifyStart(Sign(rsa, Start()), Expected(), Now);
            var player = ConnectionClaims((Now + 15) * 1000 + 999); player["playerId"] = Other;
            Assert.Throws<FormatException>(() => verifier.VerifyConnection(Sign(rsa, player), match, Other, Player, Attempt, Now * 1000));
            var host = ConnectionClaims((Now + 15) * 1000 + 999); host["hostId"] = Other;
            Assert.Throws<FormatException>(() => verifier.VerifyConnection(Sign(rsa, host), match, Player, Other, Attempt, Now * 1000));
            string[] pieces = Sign(rsa, ConnectionClaims((Now + 15) * 1000 + 999)).Split('.');
            byte[] signature = OnlineJson.DecodeBase64Url(pieces[2], 512); signature[0] ^= 1;
            string tampered = pieces[0] + "." + pieces[1] + "." + OnlineJson.Base64Url(signature);
            Assert.Throws<FormatException>(() => verifier.VerifyConnection(tampered, match, Player, Player, Attempt, Now * 1000));
        }

        [Test] public void ResignedExactRetryDoesNotExtendTheOriginalMillisecondLease()
        {
            using var rsa = Key(); var verifier = new OnlineAdmissionVerifier("wordbound-test", Jwks(rsa));
            var match = verifier.VerifyStart(Sign(rsa, Start()), Expected(), Now);
            long originalExpiry = (Now + 15) * 1000 + 504;
            var claims = ConnectionClaims(originalExpiry);
            var original = verifier.VerifyConnection(Sign(rsa, claims), match, Player, Player, Attempt, Now * 1000 + 500);
            claims["iat"] = Now + 4; claims["exp"] = Now + 19;
            string signedRetry = Sign(rsa, claims);
            var retry = verifier.VerifyConnection(signedRetry, match, Player, Player, Attempt, Now * 1000 + 4500);
            Assert.AreEqual(original.ExpiresAtMilliseconds, retry.ExpiresAtMilliseconds);
            Assert.AreEqual(original.Epoch, retry.Epoch);
            Assert.Throws<FormatException>(() => verifier.VerifyConnection(signedRetry, match, Player, Player, Attempt, originalExpiry));
        }

        static OnlineSnapshotMark BootstrapMark() => new OnlineSnapshotMark(Match, new string('a', 64), 0, 1, 0, new string('b', 64));

        [TestCase(2), TestCase(4), TestCase(8)]
        public void BootstrapRequiresEveryExactRemoteReceiptBeforeInitialStart(int players)
        {
            var owner = new object(); var mark = BootstrapMark();
            string[] peers = Enumerable.Range(1, players - 1).Select(value => "peer-" + value).ToArray();
            var barrier = new OnlineBootstrapBarrier(owner, mark, peers, Now * 1000);
            Assert.IsFalse(barrier.IsComplete(owner, Now * 1000 + 1));
            foreach (string peer in peers.Take(peers.Length - 1))
            {
                Assert.IsTrue(barrier.Acknowledge(owner, peer, mark, Now * 1000 + 2));
                Assert.IsFalse(barrier.Acknowledge(owner, peer, mark, Now * 1000 + 3));
                Assert.IsFalse(barrier.IsComplete(owner, Now * 1000 + 3));
            }
            Assert.IsTrue(barrier.Acknowledge(owner, peers.Last(), OnlineSnapshotMark.Read(OnlineJson.Parse(OnlineJson.Encode(mark.ToValue()))),
                Now * 1000 + 89999));
            Assert.IsTrue(barrier.IsComplete(owner, Now * 1000 + 89999));
            Assert.AreEqual(Now * 1000 + 90000, barrier.DeadlineMilliseconds);
            Assert.Throws<TimeoutException>(() => barrier.IsComplete(owner, barrier.DeadlineMilliseconds));
        }

        [TestCase("match"), TestCase("identity"), TestCase("epoch"), TestCase("sequence"), TestCase("tick"), TestCase("hash")]
        public void BootstrapRejectsEachWrongMarkFieldWithoutCountingAReceipt(string changed)
        {
            var owner = new object(); var mark = BootstrapMark();
            var barrier = new OnlineBootstrapBarrier(owner, mark, new[] { "peer" }, Now * 1000);
            var wrong = new OnlineSnapshotMark(changed == "match" ? Other : mark.MatchId,
                changed == "identity" ? new string('c', 64) : mark.IdentityHash, changed == "epoch" ? 1 : 0,
                changed == "sequence" ? 2 : 1, changed == "tick" ? 1 : 0, changed == "hash" ? new string('c', 64) : mark.StateHash);
            Assert.Throws<FormatException>(() => barrier.Acknowledge(owner, "peer", wrong, Now * 1000 + 1));
            Assert.IsFalse(barrier.IsComplete(owner, Now * 1000 + 1));
            Assert.IsTrue(barrier.Acknowledge(owner, "peer", mark, Now * 1000 + 2));
            Assert.IsTrue(barrier.IsComplete(owner, Now * 1000 + 2));
        }

        [Test] public void BootstrapPinsPeersAndRejectsUnknownPeerOrSupersededRun()
        {
            var owner = new object(); var replacement = new object(); var mark = BootstrapMark();
            var peers = new[] { "peer" };
            var barrier = new OnlineBootstrapBarrier(owner, mark, peers, Now * 1000);
            peers[0] = "replacement";
            Assert.Throws<FormatException>(() => barrier.Acknowledge(owner, "replacement", mark, Now * 1000));
            Assert.Throws<OperationCanceledException>(() => barrier.Acknowledge(replacement, "peer", mark, Now * 1000));
            Assert.Throws<OperationCanceledException>(() => barrier.IsComplete(replacement, Now * 1000));
            Assert.IsFalse(barrier.IsComplete(owner, Now * 1000));
            Assert.IsTrue(barrier.Acknowledge(owner, "peer", mark, Now * 1000));
            Assert.IsTrue(barrier.IsComplete(owner, Now * 1000));
        }

        [TestCase(0), TestCase(1), TestCase(2)]
        public void BootstrapCancellationCannotBeUndoneByLateOrAlreadyCompleteReceipts(int accepted)
        {
            var owner = new object(); var mark = BootstrapMark(); var peers = new[] { "first", "second" };
            var barrier = new OnlineBootstrapBarrier(owner, mark, peers, Now * 1000);
            foreach (string peer in peers.Take(accepted)) barrier.Acknowledge(owner, peer, mark, Now * 1000);
            barrier.Cancel(); barrier.Cancel();
            Assert.Throws<OperationCanceledException>(() => barrier.Acknowledge(owner, "second", mark, Now * 1000 + 1));
            Assert.Throws<OperationCanceledException>(() => barrier.IsComplete(owner, Now * 1000 + 1));
            var nextOwner = new object(); var next = new OnlineBootstrapBarrier(nextOwner, mark, peers, Now * 1000);
            Assert.IsFalse(next.IsComplete(nextOwner, Now * 1000 + 1));
            Assert.Throws<OperationCanceledException>(() => barrier.Acknowledge(nextOwner, "first", mark, Now * 1000 + 1));
        }

        [TestCase(0), TestCase(1)]
        public void MissingBootstrapReceiptsExpireAtTheOriginalAbsoluteDeadline(int accepted)
        {
            var owner = new object(); var mark = BootstrapMark();
            var barrier = new OnlineBootstrapBarrier(owner, mark, new[] { "first", "second" }, Now * 1000);
            if (accepted == 1) barrier.Acknowledge(owner, "first", mark, Now * 1000 + 89998);
            Assert.IsFalse(barrier.IsComplete(owner, Now * 1000 + 89999));
            Assert.Throws<TimeoutException>(() => barrier.Acknowledge(owner, "second", mark, Now * 1000 + 90000));
            Assert.Throws<TimeoutException>(() => barrier.IsComplete(owner, Now * 1000 + 90001));
            Assert.AreEqual(Now * 1000 + 90000, barrier.DeadlineMilliseconds);
        }

        [Test] public void BootstrapCannotTreatADisconnectedRequiredPeerAsAnEmptyCompletedSendQueue()
        {
            var owner = new object(); var mark = BootstrapMark();
            var sendQueue = new HashSet<string> { "first", "second" };
            var barrier = new OnlineBootstrapBarrier(owner, mark, sendQueue, Now * 1000);
            barrier.Acknowledge(owner, "first", mark, Now * 1000); sendQueue.Remove("first");
            sendQueue.Remove("second");
            Assert.IsEmpty(sendQueue); Assert.IsFalse(barrier.IsComplete(owner, Now * 1000 + 1));
            barrier.Cancel();
            Assert.Throws<OperationCanceledException>(() => barrier.Acknowledge(owner, "second", mark, Now * 1000 + 2));
        }

        [Test] public void BootstrapRejectsMalformedPeerSetsAndAllowsNoRemoteSeatsWithoutInventingAnAck()
        {
            var owner = new object(); var mark = BootstrapMark();
            foreach (var peers in new[] { new[] { "peer", "peer" }, new[] { "" }, new string[] { null },
                Enumerable.Range(0, 8).Select(value => value.ToString()).ToArray() })
                Assert.Throws<ArgumentException>(() => new OnlineBootstrapBarrier(owner, mark, peers, Now * 1000));
            var barrier = new OnlineBootstrapBarrier(owner, mark, Array.Empty<string>(), Now * 1000);
            Assert.IsTrue(barrier.IsComplete(owner, Now * 1000));
            Assert.Throws<FormatException>(() => barrier.Acknowledge(owner, "host", mark, Now * 1000));
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
                ("iat", (object)(Now + 60)), ("exp", (object)Now), ("exp", (object)(Now + 91)) })
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
        sealed class RotatingTransport : IOnlineHttpTransport
        {
            public OnlineCoordinatorClient Client;
            public bool RotateOnStaleRequest = true;
            public readonly List<OnlineRequest> Requests = new List<OnlineRequest>();
            static OnlineResponse Issued(string access) => new OnlineResponse(200, OnlineJson.Encode(new Dictionary<string, object> {
                ["playerId"] = Player, ["accessToken"] = access, ["refreshToken"] = Player + "." + Secret, ["expiresIn"] = 900 }));
            public Task<OnlineResponse> Send(OnlineRequest request, CancellationToken ct)
            {
                Requests.Add(request);
                if (request.Path == "/v1/guests") return Task.FromResult(Issued("old.access.token"));
                if (request.Path == "/v1/auth/refresh") return Task.FromResult(Issued("new.access.token"));
                if (request.AccessToken == "old.access.token")
                {
                    if (RotateOnStaleRequest) Client.Refresh(Player + "." + Secret).GetAwaiter().GetResult();
                    return Task.FromResult(new OnlineResponse(401, "{\"error\":{\"code\":\"INVALID_TOKEN\"}}"));
                }
                return Task.FromResult(new OnlineResponse(200, "{}"));
            }
        }
        [Test] public void ARequestCaughtByATokenRotationRetriesOnceWithTheNewToken()
        {
            var transport = new RotatingTransport(); var client = new OnlineCoordinatorClient(transport); transport.Client = client;
            client.CreateGuest("Tester").GetAwaiter().GetResult();
            client.Profile().GetAwaiter().GetResult();
            var profile = transport.Requests.Where(r => r.Path == "/v1/me").Select(r => r.AccessToken).ToArray();
            CollectionAssert.AreEqual(new[] { "old.access.token", "new.access.token" }, profile);
            Assert.AreEqual("new.access.token", client.Credentials.AccessToken);
        }
        [Test] public void ARejectedTokenThatWasNotRotatedStillFailsWithoutRetrying()
        {
            var transport = new RotatingTransport { RotateOnStaleRequest = false }; var client = new OnlineCoordinatorClient(transport);
            transport.Client = client; client.CreateGuest("Tester").GetAwaiter().GetResult();
            var error = Assert.Throws<OnlineServiceException>(() => client.Profile().GetAwaiter().GetResult());
            Assert.AreEqual(401, error.Status);
            Assert.AreEqual(1, transport.Requests.Count(r => r.Path == "/v1/me"));
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
