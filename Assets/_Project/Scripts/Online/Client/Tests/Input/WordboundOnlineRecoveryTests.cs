using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using Wreckabulary.Rules.Wordbound;
using Object = UnityEngine.Object;

namespace Wreckabulary.Online.Tests
{
    public sealed class WordboundOnlineRecoveryTests
    {
        const string Player = "11111111-1111-4111-a111-111111111111";
        const long ServerSeconds = 1700000000;

        sealed class DatedHandler : HttpMessageHandler
        {
            public readonly List<string> Paths = new List<string>();
            public DateTimeOffset? Date;
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Paths.Add(request.RequestUri.AbsolutePath);
                var response = new HttpResponseMessage(HttpStatusCode.OK) {
                    Content = new StringContent(OnlineJson.Encode(new Dictionary<string, object> {
                        ["playerId"] = Player, ["accessToken"] = "access.token",
                        ["refreshToken"] = Player + "." + OnlineJson.Base64Url(new byte[32]), ["expiresIn"] = 900 })) };
                if (Date.HasValue) response.Headers.Date = Date;
                return Task.FromResult(response);
            }
        }

        [Test]
        public async Task HttpTransportTeachesTheServerClockFromTheDateHeader()
        {
            long local = 1000;
            var clock = new OnlineServerClock(() => 0, () => local);
            var handler = new DatedHandler { Date = DateTimeOffset.FromUnixTimeSeconds(ServerSeconds) };
            using (var transport = new WordboundHttpTransport(new Uri("https://coordinator.invalid/"), handler, coordinatorClock: clock))
                await new OnlineCoordinatorClient(transport).CreateGuest("Tester");
            CollectionAssert.AreEqual(new[] { "/v1/guests" }, handler.Paths);
            Assert.IsTrue(clock.Synchronized, "Every coordinator response with a Date header must sample the server clock.");
            Assert.AreEqual(500, clock.UncertaintyMilliseconds);
            Assert.AreEqual(ServerSeconds * 1000 + 500, clock.Now());
        }

        [Test]
        public async Task HttpTransportWithoutADateHeaderLeavesTheClockUnsynchronized()
        {
            long local = 1000;
            var clock = new OnlineServerClock(() => 42000, () => local);
            var handler = new DatedHandler();
            using (var transport = new WordboundHttpTransport(new Uri("https://coordinator.invalid/"), handler, coordinatorClock: clock))
                await new OnlineCoordinatorClient(transport).CreateGuest("Tester");
            Assert.IsFalse(clock.Synchronized);
            Assert.AreEqual(42000, clock.Now());
            Assert.Throws<ArgumentNullException>(() => new WordboundHttpTransport(new Uri("https://coordinator.invalid/"), null));
        }

        static FieldInfo Field(string name) => typeof(WordboundMatchClient).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic) ??
            throw new MissingFieldException(nameof(WordboundMatchClient), name);
        static OnlineMatchExpectation Expected() => new OnlineMatchExpectation("33333333-3333-4333-a333-333333333333", "1", new string('e', 64),
            new OnlineSettings("TimeRun", "Solo", false, "asia", "1", "1"),
            new OnlineContent("BuiltIn", "test", new string('a', 64), new string('b', 64), new string('c', 64), new string('d', 64)));

        [TestCase("stopped"), TestCase("returnFailed")]
        public async Task LeaveLocallyReleasesAStoppedOrUnconfirmedMatchExactlyOnce(string reason)
        {
            var root = new GameObject("Wordbound leave locally test");
            try
            {
                var client = root.AddComponent<WordboundMatchClient>();
                Assert.IsFalse(client.CanLeaveLocally, "Without a match there is nothing to leave.");
                await client.LeaveLocally();
                Field("expected").SetValue(client, Expected());
                Assert.IsTrue(client.HasMatchContext);
                Assert.IsFalse(client.CanLeaveLocally, "A running match is left through Return to party.");
                Field(reason).SetValue(client, true);
                Assert.IsTrue(client.CanLeaveLocally);
                Field("leaving").SetValue(client, true);
                Assert.IsFalse(client.CanLeaveLocally);
                try { await client.LeaveLocally(); Assert.Fail("A second concurrent leave must be refused."); }
                catch (InvalidOperationException) { }
                Field("leaving").SetValue(client, false);
                var leaving = client.LeaveLocally();
                Assert.AreSame(leaving, await Task.WhenAny(leaving, Task.Delay(10000)), "Leaving must not wait on the service.");
                await leaving;
                Assert.IsTrue((bool)Field("stopped").GetValue(client));
                Assert.IsTrue((bool)Field("returned").GetValue(client));
                Assert.IsFalse(client.Leaving);
                Assert.IsFalse(client.CanLeaveLocally, "A left match cannot be left again.");
                await client.LeaveLocally();
                Assert.IsFalse(client.CanLeaveLocally);
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
