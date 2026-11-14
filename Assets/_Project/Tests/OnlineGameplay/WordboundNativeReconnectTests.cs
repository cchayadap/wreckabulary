using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wreckabulary.Rules.Wordbound;
using Assert = NUnit.Framework.Assert;

namespace Wreckabulary.OnlineGameplayTests
{
    public sealed partial class WordboundNativeGameplayTests
    {
        enum RecoveryEnding { None, ExplicitStop, ExpiredSeat, AuthenticationRejected }

        [UnityTest, Category("ExternalPhotonRecoveryGameplay"), Timeout(600000)]
        public IEnumerator TimeoutShutdownRejoinsOriginalSeatAndExplicitStopCancelsTheNextRetry()
            => Run(2, false, recoveryEnding: RecoveryEnding.ExplicitStop);

        [UnityTest, Category("ExternalPhotonRecoveryGameplay"), Timeout(600000)]
        public IEnumerator TimeoutShutdownStopsRetryingWhenTheCoordinatorRejectsTheExpiredSeat()
            => Run(2, false, recoveryEnding: RecoveryEnding.ExpiredSeat);

        [UnityTest, Category("ExternalPhotonRecoveryGameplay"), Timeout(600000)]
        public IEnumerator TimeoutShutdownStopsRetryingAfterDefinitiveAuthenticationRejection()
            => Run(2, false, recoveryEnding: RecoveryEnding.AuthenticationRejected);

        [UnityTest, Category("ExternalPhotonRecoveryGameplay"), Timeout(600000)]
        public IEnumerator LatePhotonElectionRetainsItsUnusedTokenAcrossATransientCredentialFailure()
            => Run(2, true, migrationPreflightFailure: true);

        IEnumerator DelayMigrationWithFailedPreflight(Peer[] survivors, Dictionary<Peer, NetworkRunner> originalRunners)
        {
            foreach (var peer in survivors)
            {
                bool first = true;
                peer.BeforeJoin = ct => {
                    if (first) { first = false; throw new OnlineServiceException(503, "UNAVAILABLE"); }
                    return Task.CompletedTask;
                };
                peer.Session.OnDisconnectedFromServer(peer.Runner, NetDisconnectReason.Timeout);
            }
            double deadline = Time.realtimeSinceStartupAsDouble + 1;
            while (Time.realtimeSinceStartupAsDouble < deadline) { Healthy(); yield return null; }
            foreach (var peer in survivors)
            {
                Assert.IsTrue(peer.Session.Recovering);
                Assert.AreSame(originalRunners[peer], peer.Runner,
                    "An early disconnect signal must not discard the Runner before Photon can elect its host.");
                Assert.IsTrue(peer.Runner.IsRunning);
                Assert.AreEqual(1, peer.CredentialRequests, "No ordinary retry may race the still-active Photon election source.");
                peer.Session.GameplayStarted += () => Assert.AreEqual(3, peer.CredentialRequests,
                    "The unused election token must survive its first credential failure and retry once.");
            }
        }

        IEnumerator Reconnect(Peer host, Peer client, RecoveryEnding ending)
        {
            var original = client.Runner;
            var originalHost = host.Runner;
            string room = original.SessionInfo.Name;
            long epoch = client.Controller.Frame.Epoch;
            long frontier = client.Controller.Frame.Commands.Frontiers[client.Actor];
            int requests = client.CredentialRequests, runners = client.RunnerCreations;
            int attempts = 0;
            bool admissionFailure = false;
            if (ending == RecoveryEnding.ExplicitStop)
            {
                int index = live.Clients.FindIndex(value => value.Credentials.PlayerId == client.Actor);
                live.ReconnectFault = (seat, request) => {
                    if (seat != index || admissionFailure || !client.Session.Recovering ||
                        client.Runner == null || !client.Runner.IsRunning ||
                        !request.Path.EndsWith("/checkpoint-head", StringComparison.Ordinal)) return null;
                    admissionFailure = true;
                    return new OnlineResponse(503, "{\"error\":{\"code\":\"UNAVAILABLE\"}}");
                };
            }
            var deadlines = new List<long>();
            client.BeforeJoin = ct => {
                deadlines.Add(client.Session.RecoveryDeadlineMilliseconds);
                if (++attempts == 1) throw new OnlineServiceException(503, "UNAVAILABLE");
                return Task.CompletedTask;
            };
            long firstDeadline = 0, noticedAt = 0;
            client.Session.RecoveryStatusChanged += message => {
                if (message == "" || firstDeadline != 0) return;
                firstDeadline = client.Session.RecoveryDeadlineMilliseconds;
                noticedAt = WordboundLiveMatch.Now();
            };
            using (var timing = new RecoveryTiming(new[] { client }, epoch,
                ending == RecoveryEnding.ExplicitStop ? "injected-client-timeout-shutdown-with-credential-and-admission-503" :
                "injected-client-timeout-shutdown-with-credential-503"))
            {
                yield return Wait(original.Shutdown(false, ShutdownReason.ConnectionTimeout), "Inject a timeout shutdown into the real client Runner", 25);
                yield return Until(() => client.Runner != original && Ready(client) && !client.Session.Recovering,
                    "Automatically reauthenticate the original seat in the existing room", 120);
                timing.MarkReadyObserved();
            }
            int expectedAttempts = ending == RecoveryEnding.ExplicitStop ? 3 : 2;
            Assert.AreEqual(expectedAttempts, attempts, "Each transient credential/admission failure must retry with fresh admission.");
            Assert.AreEqual(requests + expectedAttempts, client.CredentialRequests);
            Assert.AreEqual(runners + expectedAttempts - 1, client.RunnerCreations);
            if (ending == RecoveryEnding.ExplicitStop) Assert.IsTrue(admissionFailure,
                "The admission failure must happen on an actually connected replacement.");
            Assert.Greater(firstDeadline, noticedAt);
            Assert.LessOrEqual(firstDeadline, noticedAt + 120000);
            Assert.IsTrue(deadlines.All(value => value > noticedAt && value <= firstDeadline));
            Assert.IsTrue(deadlines.Zip(deadlines.Skip(1), (a, b) => b <= a).All(value => value),
                "Retries must never extend the original absolute deadline.");
            Assert.AreSame(originalHost, host.Runner);
            Assert.AreEqual(room, client.Runner.SessionInfo.Name);
            Assert.IsFalse(client.Runner.IsServer);
            Assert.AreEqual(epoch, client.Controller.Frame.Epoch, "A client rejoin must not elect another host epoch.");
            Assert.AreEqual(frontier, client.Controller.Frame.Commands.Frontiers[client.Actor]);
            AssertIdentities(host);
            yield return Until(() => peers.All(ExactBodies), "Restore one exact canonical body set after rejoin", 25);
            foreach (var peer in peers) AssertBodies(peer, peer == host);
            Assert.IsTrue(client.Controller.Frame.Progress.Players.Single(value => value.Id == client.Actor).Connected);
            Assert.IsFalse(client.Controller.Frame.Progress.Players.Single(value => value.Id == client.Actor).Returned);
            client.Session.OnShutdown(original, ShutdownReason.InvalidAuthentication);
            Assert.AreEqual("", client.Session.Failure, "A stale original-runner callback cannot terminate its replacement.");
            Assert.IsFalse(client.Session.Recovering);
            var command = client.Controller.Submit(WordboundAction.CancelCraft);
            Assert.Greater(command.Sequence, frontier);
            yield return Until(() => host.Controller.Frame.Commands.Receipts.Any(value => value.ActorId == client.Actor &&
                value.Sequence == command.Sequence && value.PayloadHash == command.PayloadHash) &&
                client.Controller.Frame.Commands.Frontiers[client.Actor] == command.Sequence &&
                client.Received.Values.Any(mark => mark.Tick >= host.Controller.Frame.Commands.Receipts.Single(value =>
                    value.ActorId == client.Actor && value.Sequence == command.Sequence).Tick &&
                    host.Acknowledged.TryGetValue(Key(client.Actor, mark.Sequence), out var ack) && ack.Same(mark)),
                "Commit and acknowledge a new command after rejoin", 25);

            int terminalRequests = client.CredentialRequests, terminalRunners = client.RunnerCreations;
            bool waiting = false, cancelled = false;
            client.BeforeJoin = async ct => {
                waiting = true;
                if (ending == RecoveryEnding.ExpiredSeat) throw new OnlineServiceException(409, "RECONNECT_EXPIRED");
                if (ending == RecoveryEnding.AuthenticationRejected) throw new OnlineServiceException(401, "UNAUTHORIZED");
                try { await Task.Delay(Timeout.Infinite, ct); }
                catch (OperationCanceledException) { cancelled = true; throw; }
            };
            var next = client.Runner;
            yield return Wait(next.Shutdown(false, ShutdownReason.ConnectionTimeout), "Inject the next timeout shutdown", 25);
            double deadline = Time.realtimeSinceStartupAsDouble + 25;
            while (!waiting && Time.realtimeSinceStartupAsDouble < deadline && !lifetime.IsCancellationRequested) yield return null;
            Assert.IsTrue(waiting, "The next recovery must reach its fresh credential request.");
            if (ending == RecoveryEnding.ExplicitStop)
            {
                yield return Wait(client.Session.Stop(), "Cancel a pending reconnect through explicit Stop", 25);
                Assert.IsTrue(cancelled, "Explicit Stop must cancel and await the pending credential request.");
            }
            else
            {
                while (client.Session.Failure == "" && Time.realtimeSinceStartupAsDouble < deadline &&
                    !lifetime.IsCancellationRequested) yield return null;
                Assert.AreNotEqual("", client.Session.Failure, "Definitive rejection must end recovery.");
            }
            double quiet = Time.realtimeSinceStartupAsDouble + 2;
            while (Time.realtimeSinceStartupAsDouble < quiet) yield return null;
            Assert.IsFalse(client.Session.Recovering);
            Assert.AreEqual(terminalRequests + 1, client.CredentialRequests);
            Assert.AreEqual(terminalRunners, client.RunnerCreations, "A terminal recovery must never create another Runner.");
            Assert.AreSame(originalHost, host.Runner);
            Assert.IsTrue(host.Runner.IsRunning && host.Runner.IsServer);
            Debug.Log("WORDBOUND_REJOIN_OBSERVED: real timeout-shutdown/rejoin, stable room/seat/epoch, fresh admission, exact bodies and acknowledged command; terminal=" + ending + ". This is not a packet-blackout detection measurement.");
        }
    }

    sealed partial class WordboundLiveMatch
    {
        public Func<int, OnlineRequest, OnlineResponse> ReconnectFault;
        sealed class ReconnectFaultTransport : IOnlineHttpTransport
        {
            readonly WordboundLiveMatch owner;
            readonly int seat;
            readonly IOnlineHttpTransport inner;
            public ReconnectFaultTransport(WordboundLiveMatch owner, int seat, IOnlineHttpTransport inner)
            { this.owner = owner; this.seat = seat; this.inner = inner; }
            public Task<OnlineResponse> Send(OnlineRequest request, CancellationToken ct)
            {
                ct.ThrowIfCancellationRequested();
                var response = owner.ReconnectFault?.Invoke(seat, request);
                return response == null ? inner.Send(request, ct) : Task.FromResult(response);
            }
        }
    }
}
