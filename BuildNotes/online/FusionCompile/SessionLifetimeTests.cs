using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Fusion;
using Fusion.Photon.Realtime;
using NUnit.Framework;
using Wreckabulary.Online;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;
using Wreckabulary.Rules.Tests;

namespace Wreckabulary.LifecycleTests
{
    public sealed class SessionLifetimeTests
    {
        const string Player = "11111111-1111-4111-a111-111111111111";
        const string MatchId = "33333333-3333-4333-a333-333333333333";
        const string AppId = "66666666-6666-4666-a666-666666666666";
        const long Now = 1700000000;
        static readonly string Secret = OnlineJson.Base64Url(Enumerable.Repeat((byte)42, 32).ToArray());
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        static void Field(object target, string name, object value) =>
            target.GetType().GetField(name, Private | BindingFlags.Public).SetValue(target, value);
        static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Private | BindingFlags.Public).GetValue(target);
        static T Call<T>(object target, string name, params object[] values) =>
            (T)target.GetType().GetMethod(name, Private).Invoke(target, values);
        static T FixtureCall<T>(string name, params object[] values) =>
            (T)typeof(WordboundOnlineTests).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, values);
        sealed class Transport : IOnlineHttpTransport
        {
            public Func<OnlineRequest, Task<OnlineResponse>> Reply;
            public Task<OnlineResponse> Send(OnlineRequest request, CancellationToken ct) => Reply(request);
        }
        sealed class Authority : IWordboundFusionAuthority
        {
            long sequence;
            public string IdentityHash => new string('a', 64);
            public long Epoch => 0;
            public int Acknowledgements, Commands, Returns, Validations, Reconnects;
            public bool ReconnectAllowed = true;
            public bool TransportReady;
            public void AcceptCommand(string actor, JsonNode body) { Commands++; }
            public OnlineSnapshotMark Publish(out string json) { json = "{}"; return new OnlineSnapshotMark(MatchId, IdentityHash, 0, ++sequence, 0, new string('b', 64)); }
            public void ValidateSnapshot(string json, OnlineSnapshotMark mark) { Validations++; }
            public bool AcknowledgeDurable(OnlineSnapshotMark mark) { Acknowledgements++; return true; }
            public void PauseForMigration() { }
            public void RestoreCommitted(string json, OnlineSnapshotMark mark, long epoch, long wallClock) { }
            public IReadOnlyList<OnlineSeatPresence> Presence() => Array.Empty<OnlineSeatPresence>();
            public void SetTransportReady(bool ready, bool host) { TransportReady = ready; }
            public void Disconnect(string actor, long wallClock) { }
            public bool Reconnect(string actor, long wallClock) { Reconnects++; return ReconnectAllowed; }
            public void Return(string actor, long wallClock) { Returns++; }
        }
        sealed class Fixture : IDisposable
        {
            public readonly RSA Key = FixtureCall<RSA>("Key");
            public readonly Transport Http = new Transport();
            public readonly Authority Authority = new Authority();
            public readonly OnlineAdmissionVerifier Verifier;
            public readonly VerifiedOnlineMatch Match;
            public readonly WordboundFusionJoin Join;
            public readonly WordboundFusionSession Session;
            public int RunnerCreations;
            public long ClockMs = Now * 1000;
            public Fixture(Func<Task<WordboundFusionJoin>> credentialReply = null)
            {
                Verifier = new OnlineAdmissionVerifier("wordbound-test", FixtureCall<string>("Jwks", Key));
                var expected = FixtureCall<OnlineMatchExpectation>("Expected", new object[] { null });
                string certificate = Sign(FixtureCall<Dictionary<string, object>>("Start"));
                Match = Verifier.VerifyStart(certificate, expected, Now);
                string admission = Sign(FixtureCall<Dictionary<string, object>>("Admission"));
                var ticketNode = OnlineJson.Parse(OnlineJson.Encode(new Dictionary<string, object> {
                    ["ticket"] = "test.photon.ticket", ["appId"] = AppId, ["expiresAt"] = Now * 1000 + 60000 }));
                var ticket = (OnlinePhotonTicket)Activator.CreateInstance(typeof(OnlinePhotonTicket),
                    BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { ticketNode }, null);
                Join = new WordboundFusionJoin(Player, certificate, admission, Secret, ticket);
                Http.Reply = _ => Task.FromResult(new OnlineResponse(200, OnlineJson.Encode(new Dictionary<string, object> {
                    ["playerId"] = Player, ["accessToken"] = "test.access.token", ["refreshToken"] = Player + "." + Secret, ["expiresIn"] = 900 })));
                var client = new OnlineCoordinatorClient(Http); client.CreateGuest("Fixture").GetAwaiter().GetResult();
                var service = new OnlineConnectionService(client, Verifier, expected, () => ClockMs);
                Session = new WordboundFusionSession(() => { RunnerCreations++; return Runner(); },
                    new FusionAppSettings { AppIdFusion = AppId }, new NetworkProjectConfig(), default, Verifier, expected,
                    _ => credentialReply == null ? Task.FromResult(Join) : credentialReply(), Authority, service, () => ClockMs);
                Field(Session, "gate", new OnlineSessionGate(Match));
            }
            public string Sign(Dictionary<string, object> claims) => FixtureCall<string>("Sign", Key, claims, null);
            public NetworkRunner Runner()
            {
                var runner = new NetworkRunner(); runner.Users[new PlayerRef(1)] = Player; runner.Users[new PlayerRef(2)] = Player;
                runner.Tokens[new PlayerRef(2)] = Encoding.ASCII.GetBytes(Secret); return runner;
            }
            public object Replace(NetworkRunner runner)
            {
                object run = Call<object>(Session, "BeginRun");
                Field(run, "Runner", runner); Field(run, "Join", Join);
                Field(Session, "<Runner>k__BackingField", runner);
                return run;
            }
            public OnlineResponse Authorization(OnlineRequest request)
            {
                var body = OnlineJson.Parse(request.JsonBody);
                var claims = FixtureCall<Dictionary<string, object>>("Base", "wordbound-connection");
                claims["exp"] = Now + 15; claims["playerId"] = Player; claims["hostId"] = Player;
                claims["connectionId"] = body["connectionId"].String(); claims["connectionEpoch"] = "1";
                claims["connectionRevision"] = "1"; claims["leaseExpiresAt"] = Now * 1000 + 15000;
                return new OnlineResponse(200, OnlineJson.Encode(new Dictionary<string, object> { ["authorization"] = Sign(claims), ["claims"] = claims }));
            }
            public string HostProof(string nonce = AppId, string host = Player)
            {
                var claims = FixtureCall<Dictionary<string, object>>("Base", "wordbound-connection");
                claims["exp"] = Now + 15; claims["playerId"] = Player; claims["hostId"] = host;
                claims["connectionId"] = nonce; claims["connectionEpoch"] = "1"; claims["connectionRevision"] = "1";
                claims["leaseExpiresAt"] = Now * 1000 + 15000;
                return Sign(claims);
            }
            public NetworkRunner Client(string nonce = AppId)
            {
                var runner = Runner(); runner.IsServer = false;
                var run = Replace(runner); Field(run, "ConnectionId", nonce);
                Field(Session, "localReady", true);
                return runner;
            }
            public Dictionary<string, object> ProofBody(string proof) => new Dictionary<string, object> {
                ["roster"] = Match.Match.RosterHash, ["authorization"] = proof };
            public Dictionary<string, object> SnapshotBody()
            {
                var packet = OnlineSnapshotPacket.Compress("{}");
                var mark = new OnlineSnapshotMark(MatchId, Authority.IdentityHash, 0, 1, 0, new string('b', 64));
                return new Dictionary<string, object> {
                    ["mark"] = mark.ToValue(), ["index"] = 0, ["count"] = packet.ChunkCount, ["length"] = packet.Length,
                    ["wireHash"] = packet.WireHash, ["data"] = OnlineJson.Base64Url(packet.Chunk(0)) };
            }
            public void Deliver(NetworkRunner runner, OnlineWireKind kind, object body, PlayerRef? sender = null)
            {
                byte[] json = Encoding.UTF8.GetBytes(OnlineJson.Encode(body)), bytes = new byte[json.Length + 1];
                bytes[0] = (byte)kind; Array.Copy(json, 0, bytes, 1, json.Length);
                Session.OnReliableDataReceived(runner, sender ?? new PlayerRef(9), default, bytes);
            }
            public void Dispose() { Key.Dispose(); }
        }
        [TestCase(false), TestCase(true)]
        public void OldAdmissionCompletionCannotFailOrUnlockAReplacementRunner(bool failure)
        {
            using var f = new Fixture(); var old = f.Replace(f.Runner());
            var reply = new TaskCompletionSource<OnlineResponse>(); OnlineRequest sent = null;
            f.Http.Reply = request => { sent = request; return reply.Task; };
            var pending = Call<Task>(f.Session, "Admit", old, new PlayerRef(2), f.Join.Certificate, f.Join.Grant, Secret, AppId);
            var replacement = f.Replace(f.Runner());
            var guard = Field<HashSet<PlayerRef>>(replacement, "Authorizing"); guard.Add(new PlayerRef(2));
            if (failure) reply.SetException(new InvalidOperationException("Old request failed."));
            else reply.SetResult(f.Authorization(sent));
            pending.GetAwaiter().GetResult();
            Assert.AreEqual("", f.Session.Failure);
            Assert.IsTrue(guard.Contains(new PlayerRef(2)));
            Assert.AreEqual(0, Field<OnlineSessionGate>(f.Session, "gate").ConnectedCount);
        }
        [TestCase(false), TestCase(true)]
        public void OldCloudPushCannotAcknowledgeOrClearTheReplacementPublication(bool failure)
        {
            using var f = new Fixture(); var oldRunner = f.Runner(); f.Replace(oldRunner);
            oldRunner.ActivePlayers.Clear();
            Field(f.Session, "<GameplayReady>k__BackingField", true);
            var oldReply = new TaskCompletionSource<bool>(); oldRunner.Push = () => oldReply.Task;
            var pendingOld = f.Session.PublishCheckpoint();
            var nextRunner = f.Runner(); nextRunner.ActivePlayers.Clear();
            var next = f.Replace(nextRunner); var nextReply = new TaskCompletionSource<bool>(); nextRunner.Push = () => nextReply.Task;
            var pendingNext = f.Session.PublishCheckpoint();
            if (failure) oldReply.SetException(new InvalidOperationException("Old push failed."));
            else oldReply.SetResult(true);
            Assert.IsFalse(pendingOld.GetAwaiter().GetResult());
            Assert.AreEqual("", f.Session.Failure);
            Assert.IsTrue(Field<bool>(next, "Publishing"));
            Assert.AreEqual(0, f.Authority.Acknowledgements);
            nextReply.SetResult(true); Assert.IsTrue(pendingNext.GetAwaiter().GetResult());
            Assert.AreEqual(1, f.Authority.Acknowledgements);
            Assert.AreEqual(2L, f.Session.Committed.Sequence);
        }
        [Test] public void OldTicketCannotSendAdmissionIntoTheReplacementRunner()
        {
            using var f = new Fixture(); var old = f.Replace(f.Runner());
            var reply = new TaskCompletionSource<OnlineResponse>(); f.Http.Reply = _ => reply.Task;
            var pending = Call<Task>(f.Session, "SendAdmission", old);
            var replacement = f.Runner(); f.Replace(replacement);
            reply.SetResult(new OnlineResponse(200, OnlineJson.Encode(new Dictionary<string, object> { ["ticket"] = Secret, ["expiresAt"] = Now * 1000 + 15000 })));
            Assert.Throws<OperationCanceledException>(() => pending.GetAwaiter().GetResult());
            Assert.AreEqual(0, replacement.Sends);
            Assert.AreEqual("", f.Session.Failure);
        }
        [Test] public void StopFencesCredentialsThatIgnoreCancellation()
        {
            var reply = new TaskCompletionSource<WordboundFusionJoin>();
            using var f = new Fixture(() => reply.Task);
            var pending = f.Session.Start(); f.Session.Stop().GetAwaiter().GetResult(); reply.SetResult(f.Join);
            Assert.Throws<OperationCanceledException>(() => pending.GetAwaiter().GetResult());
            Assert.AreEqual(0, f.RunnerCreations);
            Assert.AreEqual("", f.Session.Failure);
        }
        [Test] public void StopFencesPendingMigrationShutdownBeforeNewRunnerCreation()
        {
            using var f = new Fixture(); var runner = f.Runner(); f.Replace(runner);
            var reply = new TaskCompletionSource<bool>(); runner.ShutdownCompletion = () => reply.Task;
            f.Session.OnHostMigration(runner, new HostMigrationToken { GameMode = GameMode.Host });
            runner.ShutdownCompletion = () => Task.CompletedTask;
            f.Session.Stop().GetAwaiter().GetResult(); reply.SetResult(true);
            Assert.AreEqual(0, f.RunnerCreations);
            Assert.AreEqual("", f.Session.Failure);
        }
        [Test] public void TerminalFailureCannotBeReversedByAnOutstandingBootstrapPush()
        {
            using var f = new Fixture(); var runner = f.Runner(); runner.ActivePlayers.Clear(); var run = f.Replace(runner);
            var reply = new TaskCompletionSource<bool>(); runner.Push = () => reply.Task;
            f.Http.Reply = _ => Task.FromResult(new OnlineResponse(200, OnlineJson.Encode(new Dictionary<string, object> {
                ["matchId"] = f.Match.Match.MatchId, ["reservationGeneration"] = f.Match.Match.Generation,
                ["rosterHash"] = f.Match.Match.RosterHash, ["revision"] = "0", ["serverTime"] = Now * 1000,
                ["seats"] = new[] { new Dictionary<string, object> {
                    ["playerId"] = Player, ["returned"] = false, ["connectionEpoch"] = "0", ["connectionId"] = null,
                    ["hostId"] = null, ["leaseExpiresAt"] = 0, ["reconnectDeadline"] = Now * 1000 + 120000 } } })));
            Call<Task>(f.Session, "RefreshConnectionState", run).GetAwaiter().GetResult();
            var pending = Call<Task>(f.Session, "BeginAfterCheckpoint", run, false);
            Assert.IsTrue(Field<bool>(run, "Publishing"), "Bootstrap failure=" + f.Session.Failure + " task=" + pending.Status);
            Call<object>(f.Session, "Fail", new TimeoutException("Current host lease expired."));
            reply.SetResult(true); pending.GetAwaiter().GetResult();
            Assert.AreEqual("TimeoutException", f.Session.Failure);
            Assert.IsFalse(f.Session.GameplayReady);
            Assert.IsFalse(f.Authority.TransportReady);
            Assert.AreEqual(0, f.Authority.Acknowledgements);
        }
        [Test] public void LiveActorLookupRejectsExpiredLeaseWithoutDependingOnPollOrder()
        {
            using var f = new Fixture(); var runner = f.Runner(); f.Replace(runner);
            var gate = Field<OnlineSessionGate>(f.Session, "gate");
            gate.Admit(f.Verifier.VerifyAdmission(f.Join.Grant, f.Match, Player, Secret, Now), "2", Now * 1000);
            gate.Ready("2"); Assert.IsTrue(gate.TryStart());
            var claims = FixtureCall<Dictionary<string, object>>("Base", "wordbound-connection");
            claims["exp"] = Now + 15; claims["playerId"] = Player; claims["hostId"] = Player;
            claims["connectionId"] = AppId; claims["connectionEpoch"] = "1"; claims["connectionRevision"] = "1";
            claims["leaseExpiresAt"] = Now * 1000 + 15000;
            var lease = f.Verifier.VerifyConnection(f.Sign(claims), f.Match, Player, Player, AppId, Now * 1000);
            Field<Dictionary<PlayerRef, OnlineConnectionLease>>(f.Session, "leases").Add(new PlayerRef(2), lease);
            Field(f.Session, "<GameplayReady>k__BackingField", true);
            Assert.IsTrue(f.Session.TryGetAdmittedActor(new PlayerRef(2), out string actor));
            Assert.AreEqual(Player, actor);
            f.ClockMs += 15000;
            Assert.IsFalse(f.Session.TryGetAdmittedActor(new PlayerRef(2), out actor)); Assert.IsNull(actor);
        }
        [TestCase(OnlineWireKind.Admitted), TestCase(OnlineWireKind.Begin), TestCase(OnlineWireKind.Snapshot)]
        public void ClientRejectsUnprovenHostBeforeReadyOrReplicaAcceptance(OnlineWireKind kind)
        {
            using var f = new Fixture(); var runner = f.Client();
            object body = kind == OnlineWireKind.Snapshot ? f.SnapshotBody() :
                new Dictionary<string, object> { ["roster"] = f.Match.Match.RosterHash };
            f.Deliver(runner, kind, body);
            Assert.AreEqual("FormatException", f.Session.Failure);
            Assert.IsFalse(f.Session.GameplayReady); Assert.AreEqual(0, runner.Sends);
            Assert.AreEqual(0, f.Authority.Validations);
        }
        [Test] public void ClientAcceptsExactNonceProofThenReadyAndVerifiedReplica()
        {
            using var f = new Fixture(); var runner = f.Client();
            f.Deliver(runner, OnlineWireKind.Admitted, f.ProofBody(f.HostProof()));
            Assert.AreEqual("", f.Session.Failure); Assert.AreEqual(1, runner.Sends);
            f.Deliver(runner, OnlineWireKind.Begin, new Dictionary<string, object> { ["roster"] = f.Match.Match.RosterHash });
            Assert.IsFalse(f.Session.GameplayReady);
            f.Deliver(runner, OnlineWireKind.Snapshot, f.SnapshotBody());
            Assert.AreEqual("", f.Session.Failure); Assert.IsTrue(f.Session.GameplayReady);
            Assert.IsTrue(f.Authority.TransportReady); Assert.AreEqual(1, f.Authority.Validations);
            Assert.AreEqual(2, runner.Sends);
        }
        [TestCase(false), TestCase(true)]
        public void ClientRejectsOldRunnerNonceAndSignedOutsideRosterHost(bool outsideHost)
        {
            using var f = new Fixture(); var runner = f.Client(outsideHost ? AppId : MatchId);
            string proof = f.HostProof(host: outsideHost ? "22222222-2222-4222-a222-222222222222" : Player);
            f.Deliver(runner, OnlineWireKind.Admitted, f.ProofBody(proof));
            Assert.AreEqual("FormatException", f.Session.Failure); Assert.AreEqual(0, runner.Sends);
            Assert.IsFalse(f.Session.GameplayReady);
        }
        [Test] public void ReplacementClientCannotAdvertiseReadinessFromItsPreviousScene()
        {
            using var f = new Fixture();
            f.Client();
            var replacement = f.Runner(); replacement.IsServer = false;
            var run = f.Replace(replacement); Field(run, "ConnectionId", MatchId);
            f.Deliver(replacement, OnlineWireKind.Admitted, f.ProofBody(f.HostProof(MatchId)));
            Assert.AreEqual("", f.Session.Failure);
            Assert.AreEqual(0, replacement.Sends);
            Assert.IsFalse(Field<bool>(f.Session, "localReady"));
            f.Session.MarkLocalReady();
            Assert.AreEqual(1, replacement.Sends);
        }
        [TestCase(false), TestCase(true)]
        public void ReconnectActivatesDomainOnlyAtAuthenticatedReadinessAndOnlyOnce(bool local)
        {
            using var f = new Fixture(); var runner = f.Runner(); var run = f.Replace(runner);
            var gate = Field<OnlineSessionGate>(f.Session, "gate");
            var admitted = f.Verifier.VerifyAdmission(f.Join.Grant, f.Match, Player, Secret, Now);
            gate.Admit(admitted, "old", Now * 1000); gate.Ready("old"); Assert.IsTrue(gate.TryStart());
            gate.Disconnect("old", Now * 1000);
            f.Http.Reply = request => Task.FromResult(f.Authorization(request));
            var peer = local ? runner.LocalPlayer : new PlayerRef(2);
            Call<Task>(f.Session, "Admit", run, peer, f.Join.Certificate, f.Join.Grant, Secret, AppId).GetAwaiter().GetResult();
            Field(f.Session, "<GameplayReady>k__BackingField", true);
            Assert.AreEqual(0, f.Authority.Reconnects);
            Assert.IsFalse(f.Session.TryGetAdmittedActor(peer, out _));
            for (int i = 0; i < 2; i++)
            {
                if (local) f.Session.MarkLocalReady();
                else f.Deliver(runner, OnlineWireKind.Ready, new Dictionary<string, object> {
                    ["roster"] = f.Match.Match.RosterHash }, peer);
            }
            Assert.AreEqual(1, f.Authority.Reconnects);
            Assert.IsTrue(f.Session.TryGetAdmittedActor(peer, out _));
            Assert.AreEqual(0, runner.Disconnects);
            Assert.AreEqual("", f.Session.Failure);
        }
        [TestCase(false), TestCase(true)]
        public void ReadinessCannotReviveAnExpiredLeaseOrDomainReconnectDeadline(bool expiredLease)
        {
            using var f = new Fixture(); var runner = f.Runner(); var run = f.Replace(runner);
            var gate = Field<OnlineSessionGate>(f.Session, "gate");
            gate.Admit(f.Verifier.VerifyAdmission(f.Join.Grant, f.Match, Player, Secret, Now), "old", Now * 1000);
            gate.Ready("old"); Assert.IsTrue(gate.TryStart()); gate.Disconnect("old", Now * 1000);
            f.Http.Reply = request => Task.FromResult(f.Authorization(request));
            var peer = new PlayerRef(2);
            Call<Task>(f.Session, "Admit", run, peer, f.Join.Certificate, f.Join.Grant, Secret, AppId).GetAwaiter().GetResult();
            Field(f.Session, "<GameplayReady>k__BackingField", true);
            if (expiredLease) f.ClockMs += 15000;
            else f.Authority.ReconnectAllowed = false;
            f.Deliver(runner, OnlineWireKind.Ready, new Dictionary<string, object> {
                ["roster"] = f.Match.Match.RosterHash }, peer);
            Assert.IsFalse(f.Session.TryGetAdmittedActor(peer, out _));
            Assert.AreEqual(expiredLease ? 0 : 1, f.Authority.Reconnects);
            Assert.AreEqual(1, runner.Disconnects);
            Assert.AreEqual("", f.Session.Failure);
        }
    }
}
