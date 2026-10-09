using System;
using System.Linq;
using NUnit.Framework;

namespace Wreckabulary.Rules.Tests
{
    [TestFixture]
    public class SessionTests
    {
        [Test]
        public void AReconnectingPlayerGetsTheirSeatBack()
        {
            var roster = new PlayerRoster(4, 60);
            Assert.AreEqual(JoinResult.Joined, roster.Join("key-a", 0, 1, "Ana", out var ana));
            Assert.AreEqual(JoinResult.Joined, roster.Join("key-b", 0, 2, "Bo", out var bo));
            roster.JoiningOpen = false;

            var dropped = roster.Disconnect(1, 100);
            Assert.AreEqual(1, dropped.Count);
            Assert.AreEqual(SeatState.Disconnected, ana.State);

            Assert.AreEqual(JoinResult.Rejoined, roster.Join("key-a", 0, 7, "Ana", out var back));
            Assert.AreSame(ana, back);
            Assert.AreEqual(ana.PlayerId, back.PlayerId, "same player id, so the same letters and items");
            Assert.AreEqual(7UL, back.ClientId);
            Assert.AreEqual(SeatState.Connected, back.State);
            Assert.AreEqual(JoinResult.Closed, roster.Join("key-c", 0, 9, "Cy", out _), "no new players mid-round");
        }

        [Test]
        public void TheSeatIsKeptOnlyForTheGracePeriod()
        {
            var roster = new PlayerRoster(4, 60);
            roster.Join("key-a", 0, 1, "Ana", out var ana);
            roster.Disconnect(1, 100);
            Assert.AreEqual(0, roster.Expire(159.9).Count);
            var gone = roster.Expire(160);
            Assert.AreEqual(ana.PlayerId, gone.Single().PlayerId);
            Assert.AreEqual(SeatState.Left, ana.State);
            Assert.AreEqual(JoinResult.Joined, roster.Join("key-a", 0, 3, "Ana", out var fresh));
            Assert.AreNotEqual(ana.PlayerId, fresh.PlayerId, "player ids are never reused");
        }

        [Test]
        public void CouchPlayersShareAConnection()
        {
            var roster = new PlayerRoster(4, 60);
            roster.Join("pc-1", 0, 5, "P1", out var p1);
            roster.Join("pc-1", 1, 5, "P2", out var p2);
            Assert.AreNotEqual(p1.PlayerId, p2.PlayerId);
            Assert.AreEqual(JoinResult.AlreadyConnected, roster.Join("pc-1", 1, 5, "P2", out _));
            Assert.AreEqual(2, roster.Disconnect(5, 10).Count, "one dropped connection drops both seats");
        }

        [Test]
        public void TheRoomIsFullAtFour()
        {
            var roster = new PlayerRoster(4, 60);
            for (int i = 0; i < 4; i++) Assert.AreEqual(JoinResult.Joined, roster.Join("k" + i, 0, (ulong)i, null, out _));
            Assert.AreEqual(JoinResult.Full, roster.Join("k9", 0, 9, null, out _));
            roster.Leave(0);
            Assert.AreEqual(JoinResult.Joined, roster.Join("k9", 0, 9, null, out _));
            Assert.Throws<ArgumentException>(() => roster.Join("", 0, 1, null, out _));
        }

        [TestCase("192.168.1.23", 7777, 8)]
        [TestCase("192.168.0.1", 0, 8)]
        [TestCase("192.168.255.255", 65535, 8)]
        [TestCase("10.0.0.2", 7777, 10)]
        [TestCase("10.255.255.255", 65535, 10)]
        [TestCase("172.16.0.1", 7777, 10)]
        [TestCase("172.31.255.255", 65535, 10)]
        [TestCase("172.32.0.1", 7777, 12)]
        [TestCase("100.64.1.2", 7777, 12)]
        [TestCase("0.0.0.0", 0, 12)]
        [TestCase("255.255.255.255", 65535, 12)]
        public void RoomCodesRoundTrip(string ip, int port, int length)
        {
            string code = RoomCode.Encode(ip, (ushort)port);
            Assert.AreEqual(length + 1, code.Length, code);
            Assert.AreEqual('-', code[length / 2], code);
            Assert.IsTrue(RoomCode.TryDecode(code, out string gotIp, out ushort gotPort), code);
            Assert.AreEqual(ip, gotIp);
            Assert.AreEqual(port, (int)gotPort);
            Assert.IsTrue(RoomCode.TryDecode(code.ToLowerInvariant().Replace("-", " "), out gotIp, out _), "case and spacing don't matter");
            Assert.AreEqual(ip, gotIp);
        }

        [TestCase("192.168.1.23", "04BH-WR83")]
        [TestCase("10.0.0.2", "00001-1WRBQ")]
        [TestCase("172.20.5.9", "840M4-HWRBK")]
        [TestCase("81.2.69.160", "A414B8-0YC7BB")]
        public void TheCodeFormatIsStable(string ip, string code)
        {
            Assert.AreEqual(code, RoomCode.Encode(ip, 7777));
        }

        [Test]
        public void LookalikeCharactersAreForgiven()
        {
            foreach (string ip in new[] { "192.168.1.23", "10.1.10.100", "81.2.69.160" })
            {
                string code = RoomCode.Encode(ip, 7777);
                string typed = code.Replace('1', 'l').Replace('0', 'O');
                Assert.IsTrue(RoomCode.TryDecode(typed, out string got, out _), typed);
                Assert.AreEqual(ip, got);
                Assert.IsTrue(RoomCode.TryDecode(code.Replace('1', 'I'), out got, out _));
                Assert.AreEqual(ip, got);
            }
        }

        [Test]
        public void EveryTypoAndSwappedPairIsCaught()
        {
            const string digits = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
            var rng = new Random(7);
            int codes = 0, typos = 0, swaps = 0;
            for (int n = 0; n < 150; n++)
            {
                var b = new byte[4];
                rng.NextBytes(b);
                if (n % 3 == 0) { b[0] = 192; b[1] = 168; }
                else if (n % 3 == 1) b[0] = 10;
                string ip = $"{b[0]}.{b[1]}.{b[2]}.{b[3]}";
                string code = RoomCode.Encode(ip, (ushort)rng.Next(65536)).Replace("-", "");
                codes++;
                for (int i = 0; i < code.Length; i++)
                {
                    foreach (char c in digits)
                    {
                        if (c == code[i]) continue;
                        typos++;
                        string wrong = code.Substring(0, i) + c + code.Substring(i + 1);
                        Assert.IsFalse(RoomCode.TryDecode(wrong, out _, out _), $"{code} typed as {wrong} was accepted");
                    }
                    if (i + 1 < code.Length && code[i] != code[i + 1])
                    {
                        swaps++;
                        string swapped = code.Substring(0, i) + code[i + 1] + code[i] + code.Substring(i + 2);
                        Assert.IsFalse(RoomCode.TryDecode(swapped, out _, out _), $"{code} typed as {swapped} was accepted");
                    }
                }
            }
            Assert.Greater(typos, 30000, $"{codes} codes, {typos} typos, {swaps} swaps checked");
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("ABCDE-FGHJ")]
        [TestCase("ABCDE-FGHJKM")]
        [TestCase("ABCDE-FGHJU")]
        [TestCase("ABCDEF-GHJKMNP")]
        public void BadCodesAreRejected(string code)
        {
            Assert.IsFalse(RoomCode.TryDecode(code, out _, out _));
        }

        [TestCase("192.168.1")]
        [TestCase("192.168.1.256")]
        [TestCase("a.b.c.d")]
        [TestCase("::1")]
        public void OnlyIPv4AddressesEncode(string ip)
        {
            Assert.IsNull(RoomCode.Encode(ip, 7777));
        }
    }
}
