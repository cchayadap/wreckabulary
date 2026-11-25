using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Wreckabulary.Rules.Tests
{
    public class SocialRailTests
    {
        const float Bar = 76f, CompactBar = 140f;

        [TestCase(1280, 720, 1920, Bar, false, SocialRail.Width)]
        [TestCase(1920, 1080, 1920, Bar, false, SocialRail.Width)]
        [TestCase(1680, 1050, 1728, CompactBar, false, SocialRail.Width)]
        [TestCase(1440, 1080, 1440, CompactBar, false, SocialRail.Width)]
        [TestCase(1248, 576, 2340, Bar, false, SocialRail.TouchWidth)]
        [TestCase(1920, 1080, 1920, Bar, true, SocialRail.TouchWidth)]
        public void TheRailDocksAtEveryTargetSizeAndFitsAFullParty(int pixelsWide, int pixelsHigh, float canvasWidth, float bar, bool touch, float width)
        {
            var layout = SocialRail.Layout(canvasWidth, 1080f, bar, pixelsHigh, touch);
            Assert.AreEqual(width, layout.Width, $"{pixelsWide}x{pixelsHigh}");
            Assert.IsTrue(layout.Docked, $"{pixelsWide}x{pixelsHigh} docks, so the page is inset and nothing is covered");
            Assert.AreEqual(SocialRail.Margin + width + SocialRail.Gap, layout.Inset, .01f);
            Assert.AreEqual(width - 2f * SocialRail.Pad, layout.Portrait, .01f);
            Assert.AreEqual(bar + SocialRail.TopGap, layout.Top, .01f);
            for (int seats = 1; seats <= PartyPlates.Places; seats++)
            {
                bool invite = SocialRail.InviteSlot(seats);
                Assert.IsTrue(SocialRail.Fits(layout, seats, invite, SocialRail.Buttons(SocialServices.None).Count), $"{seats} seats at {pixelsWide}x{pixelsHigh}");
                Assert.IsTrue(SocialRail.Fits(layout, seats, invite, SocialRail.Buttons(SocialServices.Nearby | SocialServices.Requests).Count),
                    $"room for the later buttons too at {pixelsWide}x{pixelsHigh}");
            }
            Assert.Less(layout.PanelLeft(canvasWidth), layout.Left(canvasWidth));
            Assert.GreaterOrEqual(layout.PanelLeft(canvasWidth), 0f, "the open panel stays on screen");
        }

        [Test]
        public void ANarrowWindowFloatsTheRailAboveTheFooterInsteadOfDocking()
        {
            var layout = SocialRail.Layout(1350f, 1080f, CompactBar, 1024f, false);
            Assert.IsFalse(layout.Docked);
            Assert.AreEqual(0f, layout.Inset, "the page keeps its width");
            Assert.AreEqual(SocialRail.FooterClearance, layout.Bottom, "the rail stops above the page footer and its primary button");
            Assert.IsTrue(SocialRail.Fits(layout, 3, true, 1));
        }

        [TestCase(1, false, 0, 1)]
        [TestCase(3, false, 0, 3)]
        [TestCase(9, false, 0, 4)]
        [TestCase(0, false, 0, 1)]
        [TestCase(1, true, 4, 4)]
        [TestCase(3, true, 2, 2)]
        [TestCase(2, true, 0, 1)]
        public void TheCountIsTheCouchLocallyAndTheOnlinePartyWhenThereIsOne(int couch, bool online, int members, int expected)
        {
            Assert.AreEqual(expected, SocialRail.PartyCount(couch, online, members));
            Assert.AreEqual(expected + "/4", SocialRail.CountText(SocialRail.PartyCount(couch, online, members)));
        }

        [Test]
        public void PortraitsAreYouAndCouchSeatsOrTheOnlineMembers()
        {
            var couch = SocialRail.Portraits("Sam", 2, false, null);
            CollectionAssert.AreEqual(new[] { "Sam", "Player 2", "Player 3" }, couch.Select(p => p.Name).ToArray());
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, couch.Select(p => p.Place).ToArray(), "seat colours follow the place");
            var online = SocialRail.Portraits("Sam", 2, true, new[] { "You · leader · ready", "Ana · ready" });
            CollectionAssert.AreEqual(new[] { "You", "Ana" }, online.Select(p => p.Name).ToArray());
            Assert.AreEqual(PartyPlates.LeaderTag + MapCardText.Separator + PartyPlates.ReadyTag, online[0].Tag);
            var waiting = SocialRail.Portraits("Sam", 0, true, new string[0]);
            CollectionAssert.AreEqual(new[] { "Sam" }, waiting.Select(p => p.Name).ToArray(), "an online party with no list yet still shows you");
        }

        [Test]
        public void HeadingSaysWhenThePartyIsOnline()
        {
            Assert.AreEqual("Party  2/4", SocialRail.Heading(2, false));
            Assert.AreEqual("Party  3/4" + MapCardText.Separator + "online", SocialRail.Heading(3, true));
        }

        [Test]
        public void StageOneShowsOnlyRecentAndLeavesRoomForLaterTabs()
        {
            CollectionAssert.AreEqual(new[] { SocialRail.Recent }, SocialRail.Tabs(SocialServices.None).ToArray());
            CollectionAssert.AreEqual(new[] { SocialRail.Recent }, SocialRail.Buttons(SocialServices.None).ToArray());
            Assert.IsFalse(SocialRail.Chat(SocialServices.None));
            var all = SocialServices.Friends | SocialServices.Requests | SocialServices.Nearby | SocialServices.Chat;
            CollectionAssert.AreEqual(new[] { SocialRail.Friends, SocialRail.Recent, SocialRail.Nearby, SocialRail.Requests }, SocialRail.Tabs(all).ToArray());
            CollectionAssert.AreEqual(new[] { SocialRail.Nearby, SocialRail.Recent, SocialRail.AddFriend }, SocialRail.Buttons(all).ToArray());
            Assert.IsTrue(SocialRail.Chat(all));
        }

        [Test]
        public void FocusRunsTopToBottom()
        {
            CollectionAssert.AreEqual(new[] { "Server", "Party", "Portrait 1", "Portrait 2", "Invite slot", "Recent" },
                SocialRail.FocusOrder(2, true, SocialRail.Buttons(SocialServices.None)).ToArray());
            CollectionAssert.AreEqual(new[] { "Server", "Party", "Portrait 1", "Portrait 2", "Portrait 3", "Portrait 4", "Recent" },
                SocialRail.FocusOrder(4, false, SocialRail.Buttons(SocialServices.None)).ToArray());
            Assert.AreEqual(SocialRail.Party, SocialRail.JumpTarget, "the jump key lands on the party, not the server pill");
        }

        [Test]
        public async Task AnOfflineBuildNeverChecks()
        {
            double now = 0;
            var check = new ServiceCheck(false, () => now);
            Assert.AreEqual(ServiceState.Offline, check.State);
            Assert.IsFalse(check.Due);
            int calls = 0;
            await check.Run(_ => { calls++; return Task.CompletedTask; }, CancellationToken.None);
            Assert.AreEqual(0, calls);
            Assert.AreEqual(ServiceText.Offline, ServiceText.Short(check.State, check.Milliseconds));
            Assert.AreEqual(ServiceText.Offline, ServiceText.Pill(check.State, check.Milliseconds, "asia"), "no region is claimed offline");
            Assert.AreEqual(ServiceText.OfflineLine, ServiceText.Line(check.State, check.Milliseconds, "eu", false, false));
            Assert.AreEqual(ServiceDot.None, ServiceText.Dot(check.State));
        }

        [Test]
        public async Task ACheckTimesTheCallAndWaitsBeforeTheNext()
        {
            double now = 10;
            var check = new ServiceCheck(true, () => now);
            Assert.AreEqual(ServiceState.NotChecked, check.State);
            Assert.AreEqual(ServiceText.NotChecked, ServiceText.Short(check.State, check.Milliseconds));
            Assert.AreEqual(ServiceText.NotChecked, ServiceText.Pill(check.State, check.Milliseconds, "us"), "a region is shown only beside a measured time");
            Assert.AreEqual(ServiceDot.Unknown, ServiceText.Dot(check.State));
            Assert.IsTrue(check.Due);
            var answer = new TaskCompletionSource<bool>();
            var running = check.Run(_ => answer.Task, CancellationToken.None);
            Assert.AreEqual(ServiceState.Checking, check.State);
            Assert.AreEqual(ServiceText.Checking, ServiceText.Short(check.State, check.Milliseconds));
            Assert.IsFalse(check.Due, "one check at a time");
            now = 10.12;
            answer.SetResult(true);
            await running;
            Assert.AreEqual(ServiceState.Reachable, check.State);
            Assert.AreEqual(120, check.Milliseconds);
            Assert.AreEqual("120 ms", ServiceText.Short(check.State, check.Milliseconds));
            Assert.AreEqual("Asia · 120 ms", ServiceText.Pill(check.State, check.Milliseconds, "asia"));
            Assert.AreEqual("Asia (by clock) · Service 120 ms", ServiceText.Line(check.State, check.Milliseconds, "asia", false, false));
            Assert.AreEqual("Asia (by clock) · Service 120 ms · In party", ServiceText.Line(check.State, check.Milliseconds, "asia", true, false));
            Assert.AreEqual("Asia (by clock) · Service 120 ms · Searching", ServiceText.Line(check.State, check.Milliseconds, "asia", true, true));
            Assert.AreEqual(ServiceDot.Good, ServiceText.Dot(check.State));
            now += ServiceCheck.Every - 1;
            Assert.IsFalse(check.Due, "the next check waits");
            now += 1.5;
            Assert.IsTrue(check.Due);
        }

        [Test]
        public async Task AFailedCallSaysNoReplyAndNeverShowsANumber()
        {
            double now = 0;
            var check = new ServiceCheck(true, () => now);
            await check.Run(_ => Task.FromException(new InvalidOperationException("down")), CancellationToken.None);
            Assert.AreEqual(ServiceState.NoReply, check.State);
            Assert.AreEqual(ServiceCheck.NotRecorded, check.Milliseconds);
            Assert.AreEqual(ServiceText.NoReply, ServiceText.Short(check.State, check.Milliseconds));
            Assert.AreEqual(ServiceText.NoReply, ServiceText.Pill(check.State, check.Milliseconds, "eu"));
            Assert.AreEqual("Europe (by clock) · No reply", ServiceText.Line(check.State, check.Milliseconds, "eu", false, false));
            Assert.AreEqual(ServiceDot.Bad, ServiceText.Dot(check.State));
            await check.Run(_ => throw new InvalidOperationException("thrown at once"), CancellationToken.None);
            Assert.AreEqual(ServiceState.NoReply, check.State, "not due yet, so nothing changed");
            now = ServiceCheck.Every;
            await check.Run(_ => throw new InvalidOperationException("thrown at once"), CancellationToken.None);
            Assert.AreEqual(ServiceState.NoReply, check.State, "a call that throws straight away is a failed check, not a crash");
        }

        [Test]
        public async Task ALaterFailureClearsTheOldNumber()
        {
            double now = 0;
            var check = new ServiceCheck(true, () => now);
            await check.Run(_ => { now += .05; return Task.CompletedTask; }, CancellationToken.None);
            Assert.AreEqual(50, check.Milliseconds);
            now += ServiceCheck.Every + 1;
            await check.Run(_ => Task.FromException(new TimeoutException()), CancellationToken.None);
            Assert.AreEqual(ServiceCheck.NotRecorded, check.Milliseconds, "an old ping is never shown as current");
        }

        [Test]
        public async Task ClosingTheLobbyMidCheckLeavesTheLastStateAlone()
        {
            double now = 0;
            var check = new ServiceCheck(true, () => now);
            using var stop = new CancellationTokenSource();
            var answer = new TaskCompletionSource<bool>();
            var running = check.Run(token => { token.Register(() => answer.TrySetCanceled()); return answer.Task; }, stop.Token);
            stop.Cancel();
            await running;
            Assert.AreEqual(ServiceState.NotChecked, check.State);
            Assert.AreEqual(ServiceCheck.NotRecorded, check.Milliseconds);
        }

        [Test]
        public void AServiceCheckNeedsAClock()
        {
            Assert.Throws<ArgumentNullException>(() => new ServiceCheck(true, null));
        }
    }
}
