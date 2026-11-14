using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Wreckabulary.Online;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;
using Stage = Wreckabulary.OnlineGameplayTests.WordboundNativeCheckpoints.Stage;
using TickState = Wreckabulary.OnlineGameplayTests.WordboundNativeCheckpoints.TickState;

namespace Wreckabulary.OnlineGameplayTests
{
    public sealed class WordboundNativeCheckpointTests
    {
        const string MatchId = "11111111-1111-1111-1111-111111111111";
        static readonly string Hash = new string('a', 64);
        static OnlineSnapshotMark Mark(long sequence = 1, long tick = 0, long epoch = 0, string state = null) =>
            new OnlineSnapshotMark(MatchId, Hash, epoch, sequence, tick, state ?? Hash);

        [Test]
        public void AuthorityDecoratorForwardsEveryOperationWithoutExtraStateCalls()
        {
            var probe = new WordboundNativeCheckpoints.Probe(0);
            var source = new AuthorityStub(); var wrapped = probe.Wrap(source);
            var command = OnlineJson.Parse("{}"); var mark = Mark();
            Assert.AreEqual(Hash, wrapped.IdentityHash); Assert.AreEqual(0, wrapped.Epoch);
            wrapped.AcceptCommand("actor", command);
            Assert.AreSame(source.Mark, wrapped.Publish(out var json)); Assert.AreEqual("snapshot", json);
            wrapped.ValidateSnapshot(json, mark); Assert.IsFalse(wrapped.AcknowledgeDurable(mark));
            wrapped.PauseForMigration(); wrapped.RestoreCommitted(json, mark, 7, 1234);
            Assert.AreSame(source.Seats, wrapped.Presence()); wrapped.SetTransportReady(true, false);
            wrapped.Disconnect("actor", 1234); Assert.IsTrue(wrapped.Reconnect("actor", 1234)); wrapped.Return("actor", 1234);
            CollectionAssert.AreEqual(new[] { "IdentityHash", "Epoch", "AcceptCommand", "Publish", "ValidateSnapshot",
                "AcknowledgeDurable", "PauseForMigration", "RestoreCommitted", "Presence", "SetTransportReady", "Disconnect", "Reconnect", "Return" }, source.Calls);
            Assert.AreSame(command, source.Command); Assert.AreSame(mark, source.Restored);
            Assert.AreEqual(7, source.RestoredEpoch); Assert.AreEqual(1234, source.RestoredWall);
            Assert.AreEqual(0, probe.PublicationCounts().Accepted);
            foreach (var stage in new[] { Stage.Publish, Stage.Validate, Stage.Acknowledge })
            { Assert.AreEqual(1, probe.StageTotal(stage).Count); Assert.AreEqual(0, probe.StageTotal(stage).InFlight); }
        }

        [TestCase("Publish")]
        [TestCase("ValidateSnapshot")]
        [TestCase("AcknowledgeDurable")]
        public void AuthorityFailuresRetainOriginalExceptionAndCloseTheirMeasurement(string method)
        {
            var stage = method == "Publish" ? Stage.Publish : method == "ValidateSnapshot" ? Stage.Validate : Stage.Acknowledge;
            var source = new AuthorityStub { ThrowAt = method };
            var probe = new WordboundNativeCheckpoints.Probe(0); var wrapped = probe.Wrap(source);
            var error = Assert.Throws<InvalidOperationException>(() => {
                if (method == "Publish") wrapped.Publish(out _);
                else if (method == "ValidateSnapshot") wrapped.ValidateSnapshot("snapshot", Mark());
                else wrapped.AcknowledgeDurable(Mark());
            });
            Assert.AreSame(source.Failure, error); CollectionAssert.AreEqual(new[] { method }, source.Calls);
            var total = probe.StageTotal(stage);
            Assert.AreEqual(1, total.Count); Assert.AreEqual(1, total.Failed); Assert.AreEqual(0, total.InFlight);
            Assert.IsFalse(probe.PublicationCounts().Pending);
        }

        [Test]
        public void OnlyExactAcceptedAcknowledgementCompletesTheRetainedPublication()
        {
            var probe = new WordboundNativeCheckpoints.Probe(0);
            var source = new AuthorityStub(); var wrapped = probe.Wrap(source);
            wrapped.Publish(out _);
            Assert.IsFalse(wrapped.AcknowledgeDurable(source.Mark)); Assert.IsTrue(probe.PublicationCounts().Pending);
            source.Accept = true;
            foreach (var wrong in new[] { Mark(epoch: 1), Mark(sequence: 2), Mark(tick: 1), Mark(state: new string('b', 64)) })
            { Assert.IsTrue(wrapped.AcknowledgeDurable(wrong)); Assert.IsTrue(probe.PublicationCounts().Pending); }
            Assert.AreEqual(4, probe.PublicationCounts().Unmatched);
            Assert.IsTrue(wrapped.AcknowledgeDurable(Mark())); Assert.IsFalse(probe.PublicationCounts().Pending);
            wrapped.Publish(out _); source.Mark = Mark(2, 20); wrapped.Publish(out _);
            Assert.AreEqual(1, probe.PublicationCounts().Superseded);
            Assert.IsTrue(wrapped.AcknowledgeDurable(Mark())); Assert.IsTrue(probe.PublicationCounts().Pending);
            Assert.IsTrue(wrapped.AcknowledgeDurable(source.Mark)); Assert.IsFalse(probe.PublicationCounts().Pending);
            long accepted = probe.PublicationCounts().Accepted;
            long late = probe.Begin(Stage.Chunk); probe.Finish();
            wrapped.Publish(out _); wrapped.AcknowledgeDurable(source.Mark); probe.End(Stage.Chunk, late, true);
            Assert.AreEqual(accepted, probe.PublicationCounts().Accepted);
            Assert.AreEqual(1, probe.StageTotal(Stage.Chunk).InFlight, "A completion after stop must remain recorded as incomplete.");
            Assert.AreEqual(4, source.Calls.Count(value => value == "Publish"), "Stopping observation must not stop the real operation.");
        }

        [Test]
        public void InterPublicationGapRejectsBootstrapAndChangedOrUnmatchedOwnership()
        {
            var probe = new WordboundNativeCheckpoints.Probe(0);
            var source = new AuthorityStub { Accept = true }; var wrapped = probe.Wrap(source);
            void Publish(long sequence, long tick, long epoch = 0)
            { source.Mark = Mark(sequence, tick, epoch); wrapped.Publish(out _); }
            void Ack() => Assert.IsTrue(wrapped.AcknowledgeDurable(source.Mark));
            Publish(1, 0); Ack(); Publish(2, 50); Ack();
            Assert.AreEqual(1, probe.GapCounts().BootstrapSkipped); Assert.AreEqual(0, probe.GapCounts().Count);
            Publish(3, 100); Ack(); Assert.AreEqual(1, probe.GapCounts().Count);
            Publish(4, 110, 1); Ack(); Assert.AreEqual(1, probe.GapCounts().BoundarySkipped);
            probe.NewRunner(1); Publish(5, 120, 1); Ack(); Assert.AreEqual(1, probe.GapCounts().Count);
            Assert.IsTrue(wrapped.AcknowledgeDurable(Mark(99, 130, 1)));
            Publish(6, 130, 1); Ack(); Assert.AreEqual(1, probe.GapCounts().Count);
            Publish(7, 140, 1); Publish(8, 150, 1); Ack();
            Assert.AreEqual(2, probe.GapCounts().Count); Assert.AreEqual(1, probe.PublicationCounts().Superseded);
            Publish(9, 160, 1); probe.NewRunner(2); Ack(); Publish(10, 170, 1);
            Assert.AreEqual(3, probe.GapCounts().Count); Assert.AreEqual(1, probe.GapCounts().Abandoned);
            Assert.AreEqual(2, probe.PublicationCounts().Unmatched);
        }

        [Test]
        public void HostSamplesSeparateReadinessMigrationResultAndExactDurableGap()
        {
            var probe = new WordboundNativeCheckpoints.Probe(0); var mark = Mark(tick: 40);
            probe.Sample(1, 159, 0, mark, false, false, false, true, true);
            probe.Sample(2, 160, 0, mark, true, true, false, true, true);
            probe.Sample(3, 160, 0, mark, true, false, true, true, true);
            probe.Sample(4, 160, 0, mark, true, false, false, true, true);
            probe.Sample(5, 160, 1, mark, true, false, false, true, true);
            probe.Sample(6, 159, 0, mark, true, false, false, true, true);
            probe.Sample(7, 160, 0, null, true, false, false, true, true);
            probe.Sample(8, 159, 0, mark, true, false, false, false, false);
            Assert.AreEqual(1, probe.Samples(TickState.NotReady)); Assert.AreEqual(1, probe.Samples(TickState.Migration));
            Assert.AreEqual(1, probe.Samples(TickState.Result)); Assert.AreEqual(1, probe.Samples(TickState.DurableGapObserved));
            Assert.AreEqual(3, probe.Samples(TickState.OtherPaused)); Assert.AreEqual(1, probe.Samples(TickState.Eligible));
        }

        [Test]
        public async Task ConcurrentHttpObservationsPreserveRequestsResponsesCancellationAndFailures()
        {
            var probe = new WordboundNativeCheckpoints.Probe(0);
            var transport = new TransportStub(); var client = new OnlineCoordinatorClient(probe.Wrap(transport));
            await client.CreateGuest("CheckpointProbe");
            var expected = new OnlineMatchExpectation(MatchId, "1", Hash,
                new OnlineSettings("TimeRun", "Solo", false, "asia", MatchSnapshotCodec.ProtocolVersion, "probe"),
                new OnlineContent("BuiltIn", "probe", Hash, Hash, Hash, Hash));
            var checkpoints = new OnlineCheckpointService(client, expected, Hash);
            using var cancellation = new CancellationTokenSource();
            var first = checkpoints.Head(cancellation.Token); var second = checkpoints.Head(cancellation.Token);
            Assert.AreEqual(2, transport.Pending.Count); Assert.AreEqual(2, probe.StageTotal(Stage.Head).InFlight);
            Assert.AreEqual(2, probe.StageTotal(Stage.Head).MaximumInFlight);
            Assert.IsTrue(transport.Tokens.All(token => token == cancellation.Token));
            Assert.IsTrue(transport.Requests.All(request => request.Method == "POST" && request.JsonBody == "{}" && request.AccessToken == "probe"));
            transport.Pending[1].SetResult(new OnlineResponse(200, "{\"writer\":null,\"head\":null,\"previous\":null}"));
            transport.Pending[0].SetResult(new OnlineResponse(200, "{\"writer\":null,\"head\":null,\"previous\":null}"));
            var states = await Task.WhenAll(first, second); Assert.IsTrue(states.All(state => state.Head == null));
            var failed = checkpoints.Head(cancellation.Token); var failure = new InvalidOperationException("test-only");
            transport.Pending[2].SetException(failure);
            try { await failed; Assert.Fail("The original transport failure must propagate."); }
            catch (InvalidOperationException error) { Assert.AreSame(failure, error); }
            var cancelled = checkpoints.Head(cancellation.Token); cancellation.Cancel();
            var cancellationError = new OperationCanceledException(cancellation.Token);
            transport.Pending[3].SetException(cancellationError);
            try { await cancelled; Assert.Fail("Cancellation must propagate without retry."); }
            catch (OperationCanceledException error) { Assert.AreEqual(cancellation.Token, error.CancellationToken); }
            var totals = probe.StageTotal(Stage.Head);
            Assert.AreEqual(4, totals.Count); Assert.AreEqual(2, totals.Failed); Assert.AreEqual(0, totals.InFlight);
            Assert.AreEqual(4, transport.Requests.Count); Assert.AreEqual(1, transport.Guests);
            Assert.AreEqual(0, probe.StageTotal(Stage.Begin).Count);
        }

        sealed class TransportStub : IOnlineHttpTransport
        {
            internal int Guests;
            internal readonly List<TaskCompletionSource<OnlineResponse>> Pending = new List<TaskCompletionSource<OnlineResponse>>();
            internal readonly List<OnlineRequest> Requests = new List<OnlineRequest>();
            internal readonly List<CancellationToken> Tokens = new List<CancellationToken>();
            public Task<OnlineResponse> Send(OnlineRequest request, CancellationToken ct)
            {
                if (request.Path == "/v1/guests")
                {
                    Guests++;
                    return Task.FromResult(new OnlineResponse(200, OnlineJson.Encode(new Dictionary<string, object> {
                        ["playerId"] = MatchId, ["accessToken"] = "probe", ["refreshToken"] = MatchId + "." + new string('A', 43), ["expiresIn"] = 900 })));
                }
                Requests.Add(request); Tokens.Add(ct);
                var pending = new TaskCompletionSource<OnlineResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
                Pending.Add(pending); return pending.Task;
            }
        }
        sealed class AuthorityStub : IWordboundFusionAuthority
        {
            internal readonly List<string> Calls = new List<string>();
            internal readonly InvalidOperationException Failure = new InvalidOperationException("test-only");
            internal readonly OnlineSeatPresence[] Seats = { new OnlineSeatPresence("actor", 0, false) };
            internal OnlineSnapshotMark Mark = WordboundNativeCheckpointTests.Mark(), Restored;
            internal long RestoredEpoch, RestoredWall;
            internal JsonNode Command;
            internal bool Accept;
            internal string ThrowAt;
            void Call(string name) { Calls.Add(name); if (ThrowAt == name) throw Failure; }
            public string IdentityHash { get { Call(nameof(IdentityHash)); return Hash; } }
            public long Epoch { get { Call(nameof(Epoch)); return 0; } }
            public void AcceptCommand(string actor, JsonNode command) { Call(nameof(AcceptCommand)); Command = command; }
            public OnlineSnapshotMark Publish(out string json) { Call(nameof(Publish)); json = "snapshot"; return Mark; }
            public void ValidateSnapshot(string json, OnlineSnapshotMark mark) => Call(nameof(ValidateSnapshot));
            public bool AcknowledgeDurable(OnlineSnapshotMark mark) { Call(nameof(AcknowledgeDurable)); return Accept; }
            public void PauseForMigration() => Call(nameof(PauseForMigration));
            public void RestoreCommitted(string json, OnlineSnapshotMark mark, long epoch, long wallClock)
            { Call(nameof(RestoreCommitted)); Restored = mark; RestoredEpoch = epoch; RestoredWall = wallClock; }
            public IReadOnlyList<OnlineSeatPresence> Presence() { Call(nameof(Presence)); return Seats; }
            public void SetTransportReady(bool ready, bool host) => Call(nameof(SetTransportReady));
            public void Disconnect(string actor, long wallClock) => Call(nameof(Disconnect));
            public bool Reconnect(string actor, long wallClock) { Call(nameof(Reconnect)); return true; }
            public void Return(string actor, long wallClock) => Call(nameof(Return));
        }
    }
}
