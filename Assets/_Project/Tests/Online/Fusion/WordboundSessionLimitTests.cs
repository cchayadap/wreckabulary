using System;
using System.Collections.Generic;
using System.Globalization;
using NUnit.Framework;
using Wreckabulary.Online;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Tests
{
    public sealed class WordboundSessionLimitTests
    {
        static readonly string Hash = new string('a', 64);
        static string Text(object value) => value is string text ? "\"" + text + "\"" : Convert.ToString(value, CultureInfo.InvariantCulture);
        static string Header(object count, object index, object length, string hash = null, string omit = null)
        {
            var fields = new List<string>();
            void Add(string name, string value) { if (name != omit) fields.Add("\"" + name + "\":" + value); }
            Add("mark", "\"ignored\""); Add("index", Text(index)); Add("count", Text(count)); Add("length", Text(length));
            Add("wireHash", Text(hash ?? Hash)); Add("data", "\"\"");
            return "{" + string.Join(",", fields) + "}";
        }
        static WordboundSnapshotChunkHeader Read(string json) => WordboundSnapshotChunkHeader.Read(Json.Parse(json, "snapshot-test"));

        [Test]
        public void ClientsShareTheHostStartDeadlineWithAShortGrace()
        {
            const long started = 1700000000000;
            Assert.IsFalse(WordboundStartDeadline.HostExpired(started, started + WordboundStartDeadline.HostMilliseconds - 1));
            Assert.IsTrue(WordboundStartDeadline.HostExpired(started, started + WordboundStartDeadline.HostMilliseconds));
            Assert.IsFalse(WordboundStartDeadline.ClientExpired(started, started + WordboundStartDeadline.HostMilliseconds));
            Assert.IsFalse(WordboundStartDeadline.ClientExpired(started, started + 94999));
            Assert.IsTrue(WordboundStartDeadline.ClientExpired(started, started + 95000));
            Assert.IsTrue(WordboundStartDeadline.ClientExpired(started, long.MaxValue / 2));
            Assert.IsFalse(WordboundStartDeadline.ClientExpired(started, 0));
            Assert.AreEqual(90000, WordboundStartDeadline.HostMilliseconds);
        }

        [Test]
        public void FreshSessionsJoiningAStartedMatchGetTheirOwnStartWindow()
        {
            const long started = 1700000000000, joined = started + 600000;
            Assert.IsTrue(WordboundStartDeadline.ClientExpired(started, joined), "The match-start deadline alone would fail a late join at once.");
            Assert.IsFalse(WordboundStartDeadline.ClientExpired(started, joined, joined));
            Assert.IsFalse(WordboundStartDeadline.ClientExpired(started, joined, joined + 94999));
            Assert.IsTrue(WordboundStartDeadline.ClientExpired(started, joined, joined + 95000));
            Assert.IsFalse(WordboundStartDeadline.ClientExpired(started, started - 5000, started + 94999), "A session opened before the match keeps the shared deadline.");
            Assert.IsTrue(WordboundStartDeadline.ClientExpired(started, started - 5000, started + 95000));
        }

        [Test]
        public void SnapshotChunkHeaderAcceptsTheHostEncoding()
        {
            var first = Read(Header(2, 0, 4097));
            Assert.AreEqual(2, first.Count); Assert.AreEqual(0, first.Index); Assert.AreEqual(4097, first.Length);
            Assert.AreEqual(Hash, first.WireHash);
            Assert.AreEqual(1, Read(Header(2, 1, 8192)).Index);
            var largest = Read(Header(OnlineSnapshotPacket.MaximumChunks, OnlineSnapshotPacket.MaximumChunks - 1, OnlineSnapshotPacket.MaximumCompressedBytes));
            Assert.AreEqual(OnlineSnapshotPacket.MaximumChunks - 1, largest.Index);
            var encoded = OnlineJson.Parse(OnlineJson.Encode(new Dictionary<string, object> {
                ["mark"] = "ignored", ["index"] = 0, ["count"] = 1, ["length"] = 41, ["wireHash"] = Hash, ["data"] = "" }));
            Assert.AreEqual(41, WordboundSnapshotChunkHeader.Read(encoded).Length);
        }

        [TestCase(0, 0, 41), TestCase(-1, 0, 41), TestCase(65, 0, 266240), TestCase(2147483647, 0, 41), TestCase(3000000000L, 0, 41)]
        [TestCase(1, -1, 41), TestCase(1, 1, 41), TestCase(2, 2, 4097), TestCase(1, 0, 0), TestCase(1, 0, 262145)]
        [TestCase(1, 0, 4097), TestCase(3, 0, 4097), TestCase(1.5, 0, 41), TestCase(1, 0.5, 41), TestCase("1", 0, 41)]
        public void SnapshotChunkHeaderFailsClosedOutsideItsBounds(object count, object index, object length)
        {
            Assert.Throws<FormatException>(() => Read(Header(count, index, length)));
        }

        [Test]
        public void SnapshotChunkHeaderRejectsMissingFieldsAndBadHashes()
        {
            foreach (string field in new[] { "count", "index", "length", "wireHash" })
                Assert.Throws<FormatException>(() => Read(Header(1, 0, 41, omit: field)), field);
            Assert.Throws<FormatException>(() => Read(Header(1, 0, 41, new string('A', 64))));
            Assert.Throws<FormatException>(() => Read(Header(1, 0, 41, new string('a', 63))));
            Assert.Throws<FormatException>(() => WordboundSnapshotChunkHeader.Read(null));
            Assert.Throws<FormatException>(() => WordboundSnapshotChunkHeader.Read(Json.Parse("[]", "snapshot-test")));
        }
    }
}
