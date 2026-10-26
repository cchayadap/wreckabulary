using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Tests
{
    public sealed class WordboundWriterLeaseTests
    {
        const string Player = "11111111-1111-4111-a111-111111111111";
        const string Other = "22222222-2222-4222-a222-222222222222";
        const string Match = "33333333-3333-4333-a333-333333333333";
        const long Start = 1700000000000;

        sealed class Http : IOnlineHttpTransport
        {
            public Func<OnlineRequest, Task<OnlineResponse>> Reply;
            public Task<OnlineResponse> Send(OnlineRequest request, CancellationToken token) => Reply(request);
        }
        sealed class BlockedContext : SynchronizationContext
        {
            public int Posts;
            public override void Post(SendOrPostCallback callback, object state) => Interlocked.Increment(ref Posts);
        }
        sealed class Fixture
        {
            readonly Http http = new Http();
            public readonly OnlineCheckpointService Service;
            public readonly OnlineCheckpointWriter Initial;
            public Func<OnlineRequest, Task<OnlineResponse>> Reply;
            public long Now = Start;
            public int Requests;
            public Fixture()
            {
                http.Reply = request => {
                    if (request.Path == "/v1/guests") return Task.FromResult(Json(new Dictionary<string, object> {
                        ["playerId"] = Player, ["accessToken"] = "test.access.token",
                        ["refreshToken"] = Player + "." + OnlineJson.Base64Url(new byte[32]), ["expiresIn"] = 900 }));
                    Interlocked.Increment(ref Requests);
                    return Reply == null ? Task.FromResult(Response(Start + 15000)) : Reply(request);
                };
                var client = new OnlineCoordinatorClient(http);
                client.CreateGuest("Lease test").GetAwaiter().GetResult();
                var settings = new OnlineSettings("Freestyle", "Solo", false, "asia", MatchSnapshotCodec.ProtocolVersion, "test");
                var content = new OnlineContent("Workshop", "proof", new string('b', 64), new string('c', 64), new string('d', 64), new string('e', 64));
                Service = new OnlineCheckpointService(client, new OnlineMatchExpectation(Match, "1", new string('f', 64), settings, content), new string('a', 64));
                Initial = Service.Claim(Other).GetAwaiter().GetResult().Writer;
            }
            public OnlineWriterLease Lease(CancellationToken token = default) => new OnlineWriterLease(Initial, () => Interlocked.Read(ref Now), token);
            public OnlineCheckpointState State(long expiry, bool open = false, string field = null, object value = null)
            {
                Reply = _ => Task.FromResult(Response(expiry, open, field, value));
                return Service.Head().GetAwaiter().GetResult();
            }
            public static OnlineResponse Response(long expiry, bool open = false, string field = null, object value = null)
            {
                var writer = new Dictionary<string, object> { ["playerId"] = Player, ["claimId"] = Other, ["term"] = "1", ["epoch"] = "0",
                    ["expiresAt"] = expiry, ["baseHeadId"] = null, ["admissionOpen"] = open };
                if (field != null) writer[field] = value;
                return Json(new Dictionary<string, object> { ["writer"] = writer, ["head"] = null, ["previous"] = null });
            }
            static OnlineResponse Json(object value) => new OnlineResponse(200, OnlineJson.Encode(value));
        }
        static TaskCompletionSource<OnlineResponse> Pending() => new TaskCompletionSource<OnlineResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        static bool Completed(Task<bool> task)
        {
            Assert.IsTrue(task.Wait(5000), "Renewal completion must not wait for the blocked caller context.");
            return task.GetAwaiter().GetResult();
        }

        [Test]
        public void RealServiceRenewalCompletesWhileCallerSynchronizationContextIsStalled()
        {
            var f = new Fixture(); var lease = f.Lease(); var pending = Pending();
            f.Reply = _ => pending.Task; f.Now = Start + 5000;
            var prior = SynchronizationContext.Current; var blocked = new BlockedContext();
            try
            {
                SynchronizationContext.SetSynchronizationContext(blocked);
                Assert.IsTrue(lease.RenewalDue);
                var renewing = lease.Renew(f.Service.Renew);
                Assert.IsFalse(renewing.IsCompleted);
                pending.SetResult(Fixture.Response(Start + 20000));
                Assert.IsTrue(Completed(renewing));
                Assert.AreEqual(0, blocked.Posts);
                Assert.AreEqual(Start + 20000, lease.Current.ExpiresAtMilliseconds);
                Assert.AreEqual(1, lease.Observe().Attempts);
                Assert.IsFalse(lease.Observe().InFlight);
            }
            finally { SynchronizationContext.SetSynchronizationContext(prior); }
        }

        [TestCase(false), TestCase(true)]
        public void RetiredOrCancelledRunCannotAcceptAnAlreadyDispatchedReply(bool cancel)
        {
            var f = new Fixture(); using var lifetime = new CancellationTokenSource();
            var old = f.Lease(lifetime.Token); var replacement = f.Lease(); var pending = Pending();
            f.Reply = _ => pending.Task;
            var renewing = old.Renew(f.Service.Renew);
            if (cancel) lifetime.Cancel(); else old.Retire();
            pending.SetResult(Fixture.Response(Start + 30000, true));
            Assert.IsFalse(Completed(renewing));
            Assert.AreSame(f.Initial, old.Current);
            Assert.AreSame(f.Initial, replacement.Current);
            Assert.IsFalse(old.IsLiveFor(Player)); Assert.IsTrue(replacement.IsLiveFor(Player));
            Assert.IsTrue(old.Observe().Error is OperationCanceledException);
        }

        [Test]
        public void ResponseAfterPriorLocalExpiryCannotResurrectWriter()
        {
            var f = new Fixture(); var lease = f.Lease(); var pending = Pending();
            f.Reply = _ => pending.Task;
            var renewing = lease.Renew(f.Service.Renew);
            Interlocked.Exchange(ref f.Now, f.Initial.ExpiresAtMilliseconds);
            pending.SetResult(Fixture.Response(Start + 30000));
            Assert.IsFalse(Completed(renewing)); Assert.AreSame(f.Initial, lease.Current);
            Assert.IsFalse(lease.IsLiveFor(Player)); Assert.IsTrue(lease.Observe().Error is TimeoutException);
        }

        [TestCase("expired"), TestCase("cancelled"), TestCase("retired")]
        public void ExpiryOrCancellationBetweenPollAndDispatchReturnsWithoutCallingTransport(string boundary)
        {
            var f = new Fixture(); using var lifetime = new CancellationTokenSource(); var lease = f.Lease(lifetime.Token);
            f.Now = Start + 5000; Assert.IsTrue(lease.RenewalDue); int before = f.Requests;
            if (boundary == "expired") f.Now = f.Initial.ExpiresAtMilliseconds;
            else if (boundary == "cancelled") lifetime.Cancel(); else lease.Retire();
            var result = lease.Renew(f.Service.Renew);
            Assert.IsTrue(result.IsCompleted); Assert.IsFalse(result.IsFaulted); Assert.IsFalse(Completed(result));
            Assert.AreEqual(before, f.Requests); Assert.AreEqual(0, lease.Observe().Attempts);
            Assert.IsTrue(lease.Observe().Completed); Assert.IsFalse(lease.Observe().InFlight);
            Assert.IsFalse(lease.RenewalDue); Assert.IsFalse(lease.IsLiveFor(Player));
            Assert.AreSame(f.Initial, lease.Current);
        }

        [TestCase(false), TestCase(true)]
        public void OpenAndRenewResponsesCanArriveInEitherOrderWithoutRegressingExpiryOrAdmission(bool renewFirst)
        {
            var f = new Fixture(); var opened = f.State(Start + 15000, true).Writer;
            var lease = f.Lease(); var pending = Pending(); f.Reply = _ => pending.Task;
            var renewing = lease.Renew(f.Service.Renew);
            if (!renewFirst) lease.OpenAdmission(f.Initial, opened);
            pending.SetResult(Fixture.Response(Start + 25000, false));
            Assert.IsTrue(Completed(renewing));
            if (renewFirst) lease.OpenAdmission(f.Initial, opened);
            Assert.AreEqual(Start + 25000, lease.Current.ExpiresAtMilliseconds);
            Assert.IsTrue(lease.Current.AdmissionOpen); Assert.AreEqual(f.Initial.Term, lease.Current.Term);
            Assert.AreEqual(f.Initial.ClaimId, lease.Current.ClaimId);
        }

        [TestCase("playerId"), TestCase("claimId"), TestCase("term"), TestCase("epoch"), TestCase("baseHeadId"), TestCase("expiresAt")]
        public void LeaseCellRejectsChangedAuthorityEvenWhenItsDelegateReturnsParsedState(string field)
        {
            var f = new Fixture(); object value = field == "playerId" ? Other : field == "claimId" ? Match :
                field == "baseHeadId" ? new string('b', 64) : field == "expiresAt" ? (object)(Start + 14000) : "2";
            var wrong = f.State(Start + 25000, false, field, value); var lease = f.Lease();
            Assert.IsFalse(Completed(lease.Renew((_, __) => Task.FromResult(wrong))));
            Assert.AreSame(f.Initial, lease.Current); Assert.IsTrue(lease.Observe().Error is FormatException);
        }

        [Test]
        public void AtomicLivenessUsesCompletedRenewalAtOldExpiryAndRejectsWrongOwner()
        {
            var f = new Fixture(); var lease = f.Lease(); var stale = lease.Current; var pending = Pending();
            f.Reply = _ => pending.Task;
            var renewing = lease.Renew(f.Service.Renew);
            f.Now = Start + 14999;
            pending.SetResult(Fixture.Response(Start + 25000));
            Assert.IsTrue(Completed(renewing));
            f.Now = stale.ExpiresAtMilliseconds;
            Assert.IsTrue(stale.ExpiresAtMilliseconds <= f.Now);
            Assert.IsTrue(lease.IsLiveFor(Player)); Assert.IsFalse(lease.IsLiveFor(Other));
            f.Now = Start + 25000; Assert.IsFalse(lease.IsLiveFor(Player));
        }

        [Test]
        public void OnlyOneExplicitRenewalRunsAndElapsedTimeCannotStartAnother()
        {
            var f = new Fixture(); var lease = f.Lease(); var pending = Pending(); f.Reply = _ => pending.Task;
            f.Now = Start + 5000; var first = lease.Renew(f.Service.Renew); int requested = f.Requests;
            Assert.IsFalse(Completed(lease.Renew(f.Service.Renew))); Assert.AreEqual(requested, f.Requests);
            Assert.IsTrue(lease.Observe().InFlight); Assert.IsFalse(lease.RenewalDue);
            pending.SetResult(Fixture.Response(Start + 30000)); Assert.IsTrue(Completed(first));
            f.Now = Start + 12000; Assert.IsTrue(lease.RenewalDue);
            Assert.AreEqual(requested, f.Requests); Assert.AreEqual(1, lease.Observe().Attempts);
        }

        [Test]
        public void OpenAdmissionAlsoRejectsExpiredRetiredOrWrongFenceReplies()
        {
            var f = new Fixture(); var opened = f.State(Start + 25000, true).Writer;
            var wrong = f.State(Start + 25000, true, "term", "2").Writer; var lease = f.Lease();
            Assert.Throws<FormatException>(() => lease.OpenAdmission(f.Initial, wrong));
            Assert.AreSame(f.Initial, lease.Current);
            f.Now = Start + 15000;
            Assert.Throws<TimeoutException>(() => lease.OpenAdmission(f.Initial, opened));
            f.Now = Start; lease.Retire();
            Assert.Throws<OperationCanceledException>(() => lease.OpenAdmission(f.Initial, opened));
            Assert.AreSame(f.Initial, lease.Current);
        }
    }
}
