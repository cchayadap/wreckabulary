using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.OnlineGameplayTests
{
    public class WordboundLiveCleanupTests
    {
        static readonly OnlineMatchExpectation Match = new OnlineMatchExpectation(
            "30000000-0000-4000-a000-000000000001", "1", new string('e', 64),
            new OnlineSettings("TimeRun", "Solo", false, "asia", "1", "1"),
            new OnlineContent("BuiltIn", "cleanup-test", new string('a', 64), new string('b', 64), new string('c', 64), new string('d', 64)));

        [Test]
        public async Task EveryReturnSettlesBeforeAnyPartyMutationAndFailureIsRetained()
        {
            var world = await World.Create(8, 1);
            var pending = Enumerable.Range(0, 8).Select(_ => Completion<OnlineResponse>()).ToArray();
            world.Handle = (call, _) => call.Kind == "return" ? pending[call.Peer].Task : null;
            var settled = Completion<bool>();
            int returns = 0;
            var cleanup = world.Plan(log: line => {
                world.Log(line);
                if (line.StartsWith("WORDBOUND_SERVICE_STAGE: stage=cleanup-return;", StringComparison.Ordinal) &&
                    Interlocked.Increment(ref returns) == 7) settled.TrySetResult(true);
            });
            var run = cleanup.Run(CancellationToken.None);
            Assert.AreEqual(8, world.Calls.Count(call => call.Kind == "return"));
            pending[0].SetException(new OnlineServiceException(503, "INTERNAL_ERROR"));
            for (int peer = 1; peer < 7; peer++) pending[peer].SetResult(world.Returned(peer));
            await Within(settled.Task);
            Assert.AreEqual(8, world.Calls.Count, "The final pending return must hold the global phase barrier.");
            pending[7].SetResult(world.Returned(7));
            Assert.AreEqual(1, await Within(run));
            Assert.AreEqual(1, cleanup.Failures("return"));
            Assert.AreEqual(0, cleanup.Failures("cancel"));
            Assert.AreEqual(0, cleanup.Failures("leave"));
            Assert.AreEqual(8, world.Calls.Count(call => call.Kind == "cancel"));
            Assert.AreEqual(8, world.Calls.Count(call => call.Kind == "leave"));
            var failure = cleanup.Receipts.Single(receipt => receipt.Request == "return" && receipt.Peer == 0);
            Assert.AreEqual(503, failure.HttpStatus);
            Assert.AreEqual("INTERNAL_ERROR", failure.Code);
            Assert.IsTrue(world.Membership.All(party => party == null));
            AssertSingleMutations(world);
        }

        [Test]
        public async Task IndependentPartyChainsOverlapButEachLeavesItsLeaderLast()
        {
            var world = await World.Create(8, 4);
            var cancels = new[] { Completion<OnlineResponse>(), Completion<OnlineResponse>() };
            world.Handle = (call, _) => call.Kind == "cancel" ? cancels[call.Peer / 4].Task : null;
            var firstPartyLeft = Completion<bool>();
            var cleanup = world.Plan(log: line => {
                world.Log(line);
                if (line == "WORDBOUND_SERVICE_STAGE: stage=cleanup-leave; peer=0; party=0; outcome=success")
                    firstPartyLeft.TrySetResult(true);
            });
            var run = cleanup.Run(CancellationToken.None);
            Assert.AreEqual(2, world.Calls.Count(call => call.Kind == "cancel"), "Each independent party must reach cancel without waiting for the other.");
            Assert.AreEqual(0, world.Calls.Count(call => call.Kind == "leave"));
            cancels[0].SetResult(world.Apply(world.Calls.Single(call => call.Kind == "cancel" && call.Peer == 0)));
            await Within(firstPartyLeft.Task);
            CollectionAssert.AreEqual(new[] { 3, 2, 1, 0 }, world.Calls.Where(call => call.Kind == "leave").Select(call => call.Peer));
            Assert.IsFalse(run.IsCompleted, "The second party is still pending.");
            cancels[1].SetResult(world.Apply(world.Calls.Single(call => call.Kind == "cancel" && call.Peer == 4)));
            Assert.AreEqual(0, await Within(run));
            CollectionAssert.AreEqual(new[] { 7, 6, 5, 4 }, world.Calls.Where(call => call.Kind == "leave" && call.Peer >= 4).Select(call => call.Peer));
            Assert.AreEqual(5, world.Revisions[0]);
            Assert.AreEqual(5, world.Revisions[1]);
            AssertSingleMutations(world);
        }

        [Test]
        public async Task EightSingletonPartyChainsCanAllAttemptCancelBeforeAnyCompletes()
        {
            var world = await World.Create(8, 1);
            var cancels = Enumerable.Range(0, 8).Select(_ => Completion<OnlineResponse>()).ToArray();
            world.Handle = (call, _) => call.Kind == "cancel" ? cancels[call.Peer].Task : null;
            var cleanup = world.Plan();
            var run = cleanup.Run(CancellationToken.None);
            Assert.AreEqual(8, world.Calls.Count(call => call.Kind == "cancel"));
            Assert.AreEqual(0, world.Calls.Count(call => call.Kind == "leave"));
            for (int peer = 0; peer < 8; peer++)
                cancels[peer].SetResult(world.Apply(world.Calls.Single(call => call.Kind == "cancel" && call.Peer == peer)));
            Assert.AreEqual(0, await Within(run));
            Assert.AreEqual(8, world.Calls.Count(call => call.Kind == "leave"));
            AssertSingleMutations(world);
        }

        [Test]
        public async Task OneExpiredRequestDoesNotCancelLaterPlayersOrPartyRequests()
        {
            var world = await World.Create(2, 1);
            CancellationTokenSource firstDeadline = null;
            int requests = 0;
            world.Handle = (call, token) => {
                if (call.Kind != "return" || call.Peer != 0) return null;
                var pending = Completion<OnlineResponse>();
                token.Register(() => pending.TrySetCanceled());
                return pending.Task;
            };
            var cleanup = world.Plan(scheduleDeadline: source => {
                if (Interlocked.Increment(ref requests) == 1) firstDeadline = source;
            });
            var run = cleanup.Run(CancellationToken.None);
            Assert.NotNull(firstDeadline);
            firstDeadline.Cancel();
            Assert.AreEqual(1, await Within(run));
            Assert.AreEqual(2, world.Calls.Count(call => call.Kind == "cancel"));
            Assert.AreEqual(2, world.Calls.Count(call => call.Kind == "leave"));
            Assert.AreEqual(world.Calls.Count, world.Calls.Select(call => call.Token).Distinct().Count(), "Every HTTP has its own cancellation token.");
            var failed = cleanup.Receipts.Single(receipt => receipt.Request == "return" && receipt.Peer == 0);
            Assert.IsTrue(failed.RequestCancelled);
            Assert.IsFalse(failed.OuterCancelled);
            Assert.IsTrue(cleanup.Receipts.Where(receipt => receipt.Peer != 0 || receipt.Request != "return")
                .All(receipt => receipt.Outcome == "success" && !receipt.RequestCancelled && !receipt.OuterCancelled));
            AssertSingleMutations(world);
        }

        [Test]
        public async Task FailedCancelAndFailedLeaveReadRemainFailuresWhileOtherMembersLeave()
        {
            var world = await World.Create(4, 4);
            world.Handle = (call, _) => call.Kind == "cancel" || call.Kind == "profile" && call.Peer == 3
                ? Task.FromResult(Error(503, "INTERNAL_ERROR")) : null;
            var cleanup = world.Plan();
            Assert.AreEqual(2, await Within(cleanup.Run(CancellationToken.None)));
            Assert.AreEqual(1, cleanup.Failures("cancel"));
            Assert.AreEqual(1, cleanup.Failures("leave"));
            Assert.AreEqual(1, world.Calls.Count(call => call.Kind == "cancel"));
            CollectionAssert.AreEqual(new[] { 2, 1, 0 }, world.Calls.Where(call => call.Kind == "leave").Select(call => call.Peer));
            Assert.NotNull(world.Membership[3]);
            Assert.IsTrue(world.Membership.Take(3).All(party => party == null));
            Assert.AreEqual(2, cleanup.Receipts.Count(receipt => receipt.HttpStatus == 503));
            AssertSingleMutations(world);
        }

        [Test]
        public async Task ChangedMembershipIsNeverDeletedAndAlreadyLeftMemberNeedsNoMutation()
        {
            var world = await World.Create(4, 4);
            world.Membership[3] = "20000000-0000-4000-a000-000000000099";
            world.Membership[2] = null;
            var cleanup = world.Plan();
            Assert.AreEqual(1, await Within(cleanup.Run(CancellationToken.None)));
            CollectionAssert.AreEqual(new[] { 1, 0 }, world.Calls.Where(call => call.Kind == "leave").Select(call => call.Peer));
            Assert.AreEqual("20000000-0000-4000-a000-000000000099", world.Membership[3]);
            Assert.IsTrue(world.Logs.Any(line => line.Contains("peer=2; party=0; outcome=already-left")));
            Assert.AreEqual(1, cleanup.Failures("leave"));
        }

        [Test]
        public async Task AbortRecordsUnsettledReturnAndBlocksPartyDeletionWithoutRetry()
        {
            var world = await World.Create(2, 1);
            var uncooperative = Completion<OnlineResponse>();
            world.Handle = (call, _) => call.Kind == "return" && call.Peer == 0 ? uncooperative.Task : null;
            var cleanup = world.Plan();
            using var watchdog = new CancellationTokenSource();
            var run = cleanup.Run(watchdog.Token);
            watchdog.Cancel();
            Assert.IsFalse(run.IsCompleted, "Cancellation alone cannot treat an uncooperative return as settled.");
            cleanup.Abort();
            Assert.AreEqual(5, await Within(run));
            Assert.AreEqual(2, world.Calls.Count);
            var incomplete = cleanup.Receipts.Single(receipt => receipt.Peer == 0);
            Assert.AreEqual("incomplete", incomplete.Outcome);
            Assert.IsTrue(incomplete.OuterCancelled);
            Assert.AreEqual(4, world.Logs.Count(line => line.Contains("outcome=blocked")));
            uncooperative.SetException(new InvalidOperationException("Synthetic late completion."));
            AssertSingleMutations(world);
        }

        [Test]
        public async Task SuccessfulLeaveResponseStillRequiresVerifiedEmptyMembership()
        {
            var world = await World.Create(1, 1);
            world.Handle = (call, _) => call.Kind == "leave" ? Task.FromResult(Response(new Dictionary<string, object>())) : null;
            var cleanup = world.Plan();
            Assert.AreEqual(1, await Within(cleanup.Run(CancellationToken.None)));
            Assert.AreEqual(1, cleanup.Failures("leave"));
            Assert.AreEqual(2, world.Calls.Count(call => call.Kind == "profile"));
            Assert.NotNull(world.Membership[0]);
            AssertSingleMutations(world);
        }

        [Test]
        public async Task WrongReturnActorIsAVisibleFailureAndDoesNotSuppressOtherCleanup()
        {
            var world = await World.Create(2, 1);
            world.Handle = (call, _) => call.Kind == "return" && call.Peer == 0 ? Task.FromResult(world.Returned(1)) : null;
            var cleanup = world.Plan();
            Assert.AreEqual(1, await Within(cleanup.Run(CancellationToken.None)));
            Assert.AreEqual(1, cleanup.Failures("return"));
            Assert.AreEqual(2, world.Calls.Count(call => call.Kind == "leave"));
            Assert.IsTrue(world.Logs.Any(line => line.StartsWith("WORDBOUND_SERVICE_FAILURE: stage=cleanup-return; peer=0; type=InvalidOperationException;", StringComparison.Ordinal)));
        }

        [TestCase(8, 1, 120)]
        [TestCase(2, 2, 180)]
        [TestCase(4, 4, 300)]
        [TestCase(8, 4, 300)]
        public async Task WatchdogCoversTheLongestPartyChain(int players, int partySize, int seconds)
        {
            var world = await World.Create(players, partySize);
            Assert.AreEqual(TimeSpan.FromSeconds(seconds), world.Plan().Watchdog);
            Assert.AreEqual(0, world.Calls.Count);
        }

        [Test]
        public async Task NoMatchOrPartyHasNoServiceWorkAndPreCancelledPlanCannotPass()
        {
            var world = await World.Create(1, 0);
            var empty = world.Plan(withMatch: false);
            Assert.AreEqual(0, await Within(empty.Run(CancellationToken.None)));
            Assert.AreEqual(0, world.Calls.Count);
            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();
            var owned = world.Plan();
            Assert.AreEqual(1, await Within(owned.Run(cancelled.Token)));
            Assert.AreEqual(0, world.Calls.Count);
        }

        [Test]
        public async Task DuplicateOrOversizeOwnershipIsRejectedBeforeSending()
        {
            var world = await World.Create(2, 1);
            Assert.Throws<ArgumentException>(() => new WordboundLiveMatch.CleanupExecution(
                world.Clients, new[] { new WordboundLiveMatch.CleanupParty(world.PartyIds[0], new[] { 0, 0 }) }, Match, world.Log));
            Assert.Throws<ArgumentException>(() => new WordboundLiveMatch.CleanupExecution(
                Enumerable.Repeat(world.Clients[0], 9), Array.Empty<WordboundLiveMatch.CleanupParty>(), Match, world.Log));
            Assert.AreEqual(0, world.Calls.Count);
        }

        static void AssertSingleMutations(World world)
        {
            var mutations = world.Calls.Where(call => call.Kind == "return" || call.Kind == "cancel" || call.Kind == "leave").ToArray();
            Assert.AreEqual(mutations.Length, mutations.Select(call => call.Peer + ":" + call.Request.Path).Distinct().Count(), "No mutation may be retried.");
            var keys = mutations.Where(call => call.Kind != "return").Select(call => call.Request.IdempotencyKey).ToArray();
            Assert.IsTrue(keys.All(key => !string.IsNullOrEmpty(key)));
            Assert.AreEqual(keys.Length, keys.Distinct().Count());
        }

        static TaskCompletionSource<T> Completion<T>() => new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        static async Task<T> Within<T>(Task<T> task)
        {
            Assert.AreSame(task, await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(5))), "Fake cleanup did not settle.");
            return await task;
        }
        static OnlineResponse Response(object value) => new OnlineResponse(200, OnlineJson.Encode(value));
        static OnlineResponse Error(int status, string code) => new OnlineResponse(status,
            OnlineJson.Encode(new Dictionary<string, object> { ["error"] = new Dictionary<string, object> { ["code"] = code } }));

        sealed class Call
        {
            public int Peer;
            public OnlineRequest Request;
            public CancellationToken Token;
            public string Kind => Request.Path == "/v1/me" ? "profile" : Request.Method == "GET" ? "party" : Request.Path.Substring(Request.Path.LastIndexOf('/') + 1);
        }

        sealed class World
        {
            public readonly ConcurrentQueue<Call> Calls = new ConcurrentQueue<Call>();
            public readonly ConcurrentQueue<string> Logs = new ConcurrentQueue<string>();
            public OnlineCoordinatorClient[] Clients;
            public WordboundLiveMatch.CleanupParty[] Groups;
            public string[] Membership, PartyIds;
            public int[] Revisions;
            public Func<Call, CancellationToken, Task<OnlineResponse>> Handle;
            readonly object gate = new object();

            public static async Task<World> Create(int players, int partySize)
            {
                var world = new World();
                int parties = partySize == 0 ? 0 : players / partySize;
                world.PartyIds = Enumerable.Range(0, parties).Select(index => "20000000-0000-4000-a000-" + (index + 1).ToString("D12")).ToArray();
                world.Revisions = new int[parties];
                world.Membership = Enumerable.Range(0, players).Select(peer => partySize == 0 ? null : world.PartyIds[peer / partySize]).ToArray();
                world.Groups = Enumerable.Range(0, parties).Select(party => new WordboundLiveMatch.CleanupParty(world.PartyIds[party], Enumerable.Range(party * partySize, partySize))).ToArray();
                world.Clients = Enumerable.Range(0, players).Select(peer => new OnlineCoordinatorClient(new Transport(world, peer))).ToArray();
                for (int peer = 0; peer < players; peer++) await world.Clients[peer].CreateGuest("Cleanup test " + peer);
                return world;
            }
            public WordboundLiveMatch.CleanupExecution Plan(bool withMatch = true, Action<string> log = null,
                Action<CancellationTokenSource> scheduleDeadline = null) =>
                new WordboundLiveMatch.CleanupExecution(Clients, Groups, withMatch ? Match : null, log ?? new Action<string>(Log), scheduleDeadline);
            public void Log(string line) => Logs.Enqueue(line);
            static string Player(int peer) => "10000000-0000-4000-a000-" + (peer + 1).ToString("D12");
            public OnlineResponse Returned(int peer) => Response(new Dictionary<string, object> { ["state"] = "RETURNED", ["playerId"] = Player(peer) });
            public OnlineResponse Apply(Call call)
            {
                lock (gate)
                {
                    if (call.Kind == "return") return Returned(call.Peer);
                    if (call.Kind == "profile") return Response(Membership[call.Peer] == null
                        ? new Dictionary<string, object>() : new Dictionary<string, object> { ["partyId"] = Membership[call.Peer] });
                    int party = Array.FindIndex(PartyIds, id => call.Request.Path.StartsWith("/v1/parties/" + id, StringComparison.Ordinal));
                    if (party < 0) throw new InvalidOperationException("Unexpected fake request.");
                    if (call.Kind == "party") return Response(new Dictionary<string, object> { ["revision"] = (Revisions[party] + 1).ToString() });
                    string revision = OnlineJson.Parse(call.Request.JsonBody)["expectedRevision"].String();
                    if (revision != (Revisions[party] + 1).ToString()) throw new InvalidOperationException("Stale fake party revision.");
                    if (call.Kind == "cancel")
                    {
                        if (call.Peer != Groups[party].Members[0]) throw new InvalidOperationException("Only the original leader may cancel.");
                    }
                    else if (call.Kind == "leave")
                    {
                        if (Membership[call.Peer] != PartyIds[party]) throw new InvalidOperationException("Unowned fake party leave.");
                        Membership[call.Peer] = null;
                    }
                    else throw new InvalidOperationException("Unexpected fake mutation.");
                    Revisions[party]++;
                    return Response(new Dictionary<string, object>());
                }
            }
            sealed class Transport : IOnlineHttpTransport
            {
                readonly World world;
                readonly int peer;
                public Transport(World world, int peer) { this.world = world; this.peer = peer; }
                public Task<OnlineResponse> Send(OnlineRequest request, CancellationToken cancellationToken)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (request.Path == "/v1/guests") return Task.FromResult(Response(new Dictionary<string, object> {
                        ["playerId"] = Player(peer), ["accessToken"] = "cleanup_probe", ["refreshToken"] = Player(peer) + "." + new string('A', 43), ["expiresIn"] = 900 }));
                    var call = new Call { Peer = peer, Request = request, Token = cancellationToken };
                    world.Calls.Enqueue(call);
                    return world.Handle?.Invoke(call, cancellationToken) ?? Task.FromResult(world.Apply(call));
                }
            }
        }
    }
}
