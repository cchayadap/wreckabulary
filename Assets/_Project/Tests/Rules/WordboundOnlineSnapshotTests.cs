using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using NUnit.Framework;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Rules.Tests
{
    public class WordboundOnlineSnapshotTests
    {
        [Test] public void WireRejectsClaimedActorUnknownFieldsAndNumericCounters()
        {
            var command = new Dictionary<string, object> { ["sequence"] = "9007199254740993", ["epoch"] = "0",
                ["action"] = "PickUp", ["target"] = "item:1", ["revision"] = 0, ["slot"] = -1, ["value"] = "" };
            var frame = OnlineFusionWire.Encode(OnlineWireKind.Command, command);
            Assert.AreEqual("9007199254740993", OnlineFusionWire.Decode(frame).Body["sequence"].String());
            command["actor"] = "someone-else";
            Assert.Throws<FormatException>(() => OnlineFusionWire.Encode(OnlineWireKind.Command, command));
            command.Remove("actor"); command["sequence"] = 1;
            Assert.Throws<FormatException>(() => OnlineFusionWire.Encode(OnlineWireKind.Command, command));
            Assert.Throws<FormatException>(() => OnlineFusionWire.Decode(new byte[OnlineFusionWire.MaximumControlBytes + 1]));
        }
        [Test] public void PracticeSelectionUsesTheBoundedAuthenticatedCommandEnvelope()
        {
            var command = new Dictionary<string, object> { ["sequence"] = "1", ["epoch"] = "0",
                ["action"] = "PracticePuzzle", ["target"] = new string('a', 64), ["revision"] = 0, ["slot"] = -1, ["value"] = "" };
            var parsed = OnlineFusionWire.Decode(OnlineFusionWire.Encode(OnlineWireKind.Command, command));
            Assert.AreEqual("PracticePuzzle", parsed.Body["action"].String());
            Assert.AreEqual(new string('a', 64), parsed.Body["target"].String());
            command["playerId"] = "caller-supplied";
            Assert.Throws<FormatException>(() => OnlineFusionWire.Encode(OnlineWireKind.Command, command));
            command.Remove("playerId"); command["target"] = new string('a', 97);
            Assert.Throws<FormatException>(() => OnlineFusionWire.Encode(OnlineWireKind.Command, command));
        }
        [TestCase("Hold"), TestCase("Release")]
        public void DefenseIntentUsesTheSameBoundedAuthenticatedCommandEnvelope(string intent)
        {
            var command = new Dictionary<string, object> { ["sequence"] = "2", ["epoch"] = "0",
                ["action"] = "Block", ["target"] = "", ["revision"] = 0, ["slot"] = -1, ["value"] = intent };
            var parsed = OnlineFusionWire.Decode(OnlineFusionWire.Encode(OnlineWireKind.Command, command));
            Assert.AreEqual("Block", parsed.Body["action"].String());
            Assert.AreEqual(intent, parsed.Body["value"].String());
            command["playerId"] = "caller-supplied";
            Assert.Throws<FormatException>(() => OnlineFusionWire.Encode(OnlineWireKind.Command, command));
            command.Remove("playerId"); command["value"] = new string('x', 513);
            Assert.Throws<FormatException>(() => OnlineFusionWire.Encode(OnlineWireKind.Command, command));
        }
        [Test] public void MigrationPacketRoundTripsAcrossSeveralChunks()
        {
            var random = new Random(137);
            string source = string.Concat(Enumerable.Range(0, 50000).Select(_ => (char)random.Next(32, 126)));
            var packet = OnlineSnapshotPacket.Compress(source);
            Assert.Greater(packet.ChunkCount, 1);
            Assert.AreEqual(source, OnlineSnapshotPacket.Restore(
                Enumerable.Range(0, packet.ChunkCount).Select(packet.Chunk).ToArray(), packet.Length, packet.WireHash));
        }
        [Test] public void MigrationPacketRejectsMixedMissingAndModifiedChunks()
        {
            var packet = OnlineSnapshotPacket.Compress(new string('x', 20000));
            var chunks = Enumerable.Range(0, packet.ChunkCount).Select(packet.Chunk).ToArray();
            Assert.Throws<FormatException>(() => OnlineSnapshotPacket.Restore(Array.Empty<byte[]>(), packet.Length, packet.WireHash));
            Assert.Throws<FormatException>(() => OnlineSnapshotPacket.Restore(chunks, packet.Length, new string('0', 64)));
            chunks[0][10] ^= 1;
            Assert.Throws<FormatException>(() => OnlineSnapshotPacket.Restore(chunks, packet.Length, packet.WireHash));
        }
        [Test] public void MigrationPacketEnforcesRawCompressedAndInflationBounds()
        {
            Assert.Throws<FormatException>(() => OnlineSnapshotPacket.Compress(new string('x', OnlineSnapshotPacket.MaximumRawBytes + 1)));
            var random = new Random(7);
            string incompressible = Convert.ToBase64String(Enumerable.Range(0, 400000).Select(_ => (byte)random.Next(256)).ToArray());
            Assert.Throws<FormatException>(() => OnlineSnapshotPacket.Compress(incompressible));
            Assert.Throws<FormatException>(() => OnlineSnapshotPacket.Restore(new[] { new byte[4096] },
                OnlineSnapshotPacket.MaximumCompressedBytes + 1, new string('0', 64)));
            Assert.Throws<EncoderFallbackException>(() => OnlineSnapshotPacket.Compress("\ud800"));
        }
    }
}
