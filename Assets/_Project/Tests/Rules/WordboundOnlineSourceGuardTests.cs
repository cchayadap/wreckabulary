using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Wreckabulary.Rules.Tests
{
    public sealed class WordboundOnlineSourceGuardTests
    {
        static string Source(string relative) => Regex.Replace(File.ReadAllText(Path.Combine(TestData.Root, "Assets", "_Project", "Scripts",
            relative.Replace('/', Path.DirectorySeparatorChar))), @"\s+", " ");

        static string Body(string source, string signature)
        {
            int start = source.IndexOf(signature, System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(start, 0, signature + " is missing.");
            int open = source.IndexOf('{', start), depth = 0;
            for (int index = open; index < source.Length; index++)
            {
                if (source[index] == '{') depth++;
                else if (source[index] == '}' && --depth == 0) return source.Substring(open, index - open + 1);
            }
            Assert.Fail(signature + " has no complete body.");
            return "";
        }

        [Test]
        public void FusionPollEnforcesTheClientStartDeadlineBeforeAnythingElse()
        {
            string session = Source("Online/Fusion/WordboundFusionSession.cs");
            StringAssert.StartsWith("{ if (StartDeadlinePassed()) { Fail(new TimeoutException(\"The match did not start before its deadline.\")); return; }",
                Body(session, "public void Poll()"));
            StringAssert.Contains("initialStarted = true; sessionStartedAt = clock();", Body(session, "public Task Start(CancellationToken"));
            StringAssert.Contains("WordboundStartDeadline.ClientExpired(gate.Match.StartedAtMilliseconds, sessionStartedAt, clock());", session);
            StringAssert.Contains("!gameplayObserved", session.Substring(session.IndexOf("bool StartDeadlinePassed()", System.StringComparison.Ordinal)));
            StringAssert.Contains("\"The match did not start before its deadline.\" => \"client-start-deadline\"", session);
        }

        [Test]
        public void CoordinatorResponsesAndConnectionStateFeedTheSharedServerClock()
        {
            string transport = Body(Source("Online/WordboundHttpTransport.cs"), "public async Task<OnlineResponse> Send(");
            int sent = transport.IndexOf("long sent = serverClock?.Local() ?? 0;", System.StringComparison.Ordinal);
            int request = transport.IndexOf("client.SendAsync(", System.StringComparison.Ordinal);
            int sample = transport.IndexOf("serverClock.Observe(date.ToUnixTimeMilliseconds(), sent, serverClock.Local(), OnlineServerClock.MaximumResolutionMilliseconds);", System.StringComparison.Ordinal);
            Assert.IsTrue(sent >= 0 && request > sent && sample > request, "The transport must sample the Date header around each request.");
            StringAssert.Contains("new WordboundHttpTransport(trust.Origin, coordinatorClock: serverClock)", Source("Online/Client/WordboundPartyMenu.cs"));
            string renew = Body(Source("Rules/Wordbound/OnlineConnectionService.cs"), "public async Task<IReadOnlyList<OnlineConnectionSeat>> Renew(");
            int estimate = renew.IndexOf("long estimated = clock();", System.StringComparison.Ordinal);
            int observe = renew.IndexOf("serverClock?.Observe(serverTime, sent, received);", System.StringComparison.Ordinal);
            int tolerance = renew.IndexOf("Math.Abs(serverTime - estimated) > 5000", System.StringComparison.Ordinal);
            Assert.IsTrue(estimate >= 0 && observe > estimate && tolerance > observe, "Tolerance must be checked against the estimate held before the sample.");
        }

        [Test]
        public void LeaveMatchStopsTheClientLocallyBeforeQueueingTheReturn()
        {
            string leave = Body(Source("Online/Client/WordboundMatchClient.cs"), "public async Task LeaveLocally()");
            StringAssert.Contains("Task.WhenAny(stopping, Task.Delay(5000))", leave);
            StringAssert.Contains("returned = true;", leave);
            Assert.IsFalse(leave.Contains("coordinator."), "Leaving locally must never wait on the coordinator.");
            string menu = Body(Source("Online/Client/WordboundPartyMenu.cs"), "async Task Leave()");
            int local = menu.IndexOf("await LeaveRequested(lifetime.Token);", System.StringComparison.Ordinal);
            int queue = menu.IndexOf("coordinator.LeaveMatchLocally(context.Verified.Match);", System.StringComparison.Ordinal);
            Assert.IsTrue(local >= 0 && queue > local, "The match client must stop before the return is queued.");
        }
    }
}
